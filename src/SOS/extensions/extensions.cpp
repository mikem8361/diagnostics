// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <windows.h>
#include <psapi.h>
#include <tchar.h>
#include <limits.h>
#include <target.h>
#include <hostimpl.h>
#include <arrayholder.h>
#include "extensions.h"

extern bool g_hostingInitialized;

Extensions* Extensions::s_extensions = nullptr;

/// <summary>
/// The extension host initialize callback function
/// </summary>
/// <param name="punk">IUnknown</param>
/// <returns>error code</returns>
extern "C" HRESULT InitializeHostServices(
    IUnknown* punk)
{
    g_hostingInitialized = true;
    return Extensions::GetInstance()->InitializeHostServices(punk);
}

void Extensions::Initialize(IDebuggerServices* debuggerServices, IOutputService* outputService)
{
    if (s_extensions == nullptr)
    {
        s_extensions = new Extensions(debuggerServices, outputService);
    }
}

void Extensions::Uninitialize()
{
    if (s_extensions != nullptr)
    {
        delete s_extensions;
        s_extensions = nullptr;
    }
}

/// <summary>
/// Creates a new Extensions instance
/// </summary>
/// <param name="pDebuggerServices">debugger service or nullptr</param>
/// <param name="pOutputService">global output service instance</param>
Extensions::Extensions(IDebuggerServices* pDebuggerServices, IOutputService* pOutputService) : 
    m_pHost(nullptr),
    m_pTarget(nullptr),
    m_pDebuggerServices(pDebuggerServices),
    m_pOutputService(pOutputService),
    m_pHostServices(nullptr),
    m_pSymbolService(nullptr)
{
    if (pDebuggerServices != nullptr)
    {
        pDebuggerServices->AddRef();
    }
    if (pOutputService != nullptr)
    {
        pOutputService->AddRef();
    }
}

/// <summary>
/// Cleans up the Extensions instance on debugger exit
/// </summary>
Extensions::~Extensions()
{
    DestroyTarget();
    if (m_pHost != nullptr)
    {
        m_pHost->Release();
        m_pHost = nullptr;
    }
    if (m_pOutputService != nullptr)
    {
        m_pOutputService->Release();
        m_pOutputService = nullptr;
    }
    if (m_pDebuggerServices != nullptr)
    {
        m_pDebuggerServices->Release();
        m_pDebuggerServices = nullptr;
    }
    if (m_pSymbolService != nullptr)
    {
        m_pSymbolService->Release();
        m_pSymbolService = nullptr;
    }
    if (m_pHostServices != nullptr)
    {
        m_pHostServices->Uninitialize();
        m_pHostServices->Release();
        m_pHostServices = nullptr;
    }
    s_extensions = nullptr;
}

/// <summary>
/// The extension host initialize callback function
/// </summary>
/// <param name="punk">IUnknown</param>
/// <returns>error code</returns>
HRESULT Extensions::InitializeHostServices(
    IUnknown* punk)
{
    if (m_pDebuggerServices == nullptr)
    {
        return E_INVALIDARG;
    }
    HRESULT hr = punk->QueryInterface(__uuidof(IHostServices), (void**)&m_pHostServices);
    if (FAILED(hr)) {
        return hr;
    }
    hr = m_pHostServices->GetHost(&m_pHost);
    if (FAILED(hr)) {
        return hr;
    }
    hr = m_pHostServices->RegisterDebuggerServices(m_pDebuggerServices);
    if (FAILED(hr)) {
        return hr;
    }
    ULONG processId = 0;
    if (FAILED(m_pDebuggerServices->GetCurrentProcessSystemId(&processId)))
    {
        m_pHostServices->DestroyTarget();
        return S_OK;
    }
    return m_pHostServices->UpdateTarget(processId);
}

/// <summary>
/// Returns the extension service interface or null
/// </summary>
IHostServices* Extensions::GetHostServices()
{
    if (m_pHostServices == nullptr)
    {
        IHost* host = GetHost();
        if (m_pHostServices == nullptr && host != nullptr)
        {
            host->GetService(__uuidof(IHostServices), (void**)&m_pHostServices);
        }
    }
    return m_pHostServices;
}

/// <summary>
/// Returns the host instance
/// 
/// * dotnet-dump - m_pHost has already been set by SOSInitializeByHost by SOS.Hosting
/// * lldb - m_pHost has already been set by SOSInitializeByHost by libsosplugin which gets it via the InitializeHostServices callback
/// * dbgeng - SOS.Extensions provides the instance via the InitializeHostServices callback
/// </summary>
IHost* Extensions::GetHost()
{
    if (m_pHost == nullptr)
    {
        // Initialize the hosting runtime which will call InitializeHostServices and set m_pHost to the host instance
        InitializeHosting();

        // Otherwise, use the local host instance (hostimpl.*) that creates a local target instance (targetimpl.*)
        if (m_pHost == nullptr)
        {
            m_pHost = new Host(GetDebuggerServices());
        }
    }
    return m_pHost;
}

/// <summary>
/// Check if a target flush is needed
/// </summary>
void Extensions::FlushCheck()
{
    if (m_pDebuggerServices != nullptr)
    {
        m_pDebuggerServices->FlushCheck();
    }
}

/// <summary>
/// Returns the symbol service instance
/// </summary>
ISymbolService* Extensions::GetSymbolService()
{
    if (m_pSymbolService == nullptr)
    {
        ITarget* target = GetTarget();
        if (target != nullptr)
        {
            target->GetService(__uuidof(ISymbolService), (void**)&m_pSymbolService);
        }
    }
    return m_pSymbolService;
}

/// <summary>
/// Create a new target with the extension services for  
/// </summary>
/// <returns>error result</returns>
HRESULT Extensions::CreateTarget()
{
    if (m_pHostServices != nullptr) 
    {
        return m_pHostServices->CreateTarget();
    }
    return S_OK;
}

/// <summary>
/// Create a new target with the extension services for  
/// </summary>
/// <returns>error result</returns>
HRESULT Extensions::UpdateTarget(ULONG processId)
{
    if (m_pHostServices != nullptr) 
    {
        return m_pHostServices->UpdateTarget(processId);
    }
    return S_OK;
}

/// <summary>
/// Flush the target instance
/// </summary>
void Extensions::FlushTarget()
{
    if (m_pHostServices != nullptr) 
    {
        m_pHostServices->FlushTarget();
    }
}

/// <summary>
/// Create a new target with the extension services for  
/// </summary>
void Extensions::DestroyTarget()
{
    ReleaseTarget();
    if (m_pHostServices != nullptr) 
    {
        m_pHostServices->DestroyTarget();
    }
}

/// <summary>
/// Returns the target instance
/// </summary>
ITarget* Extensions::GetTarget()
{
    if (m_pTarget == nullptr)
    {
        GetHost()->GetCurrentTarget(&m_pTarget);
    }
    return m_pTarget;
}

/// <summary>
/// Releases and clears the target 
/// </summary>
void Extensions::ReleaseTarget()
{
    if (m_pTarget != nullptr)
    {
        m_pTarget->Release();
        m_pTarget = nullptr;
    }
}

/// <summary>
/// Helper function to get the absolute path from a relative one
/// </summary>
/// <param name="path">relative path</param>
/// <param name="absolutePath">absolute path output</param>
/// <returns>true success, false invalid path</returns>
bool GetAbsolutePath(const char* path, std::string& absolutePath)
{
    ArrayHolder<char> fullPath = new char[MAX_LONGPATH];
#ifdef FEATURE_PAL
    if (realpath(path, fullPath) != nullptr && fullPath[0] != '\0')
#else
    if (GetFullPathNameA(path, MAX_LONGPATH, fullPath, nullptr) != 0)
#endif
    {
        absolutePath.assign(fullPath);
        return true;
    }
    return false;
}

/// <summary>
//  Returns just the file name portion of a file path
/// </summary>
/// <param name="filePath">full path to get file name</param>
/// <returns>just the file name</returns>
const std::string
GetFileName(const std::string& filePath)
{
    size_t last = filePath.rfind(DIRECTORY_SEPARATOR_STR_A);
    if (last != std::string::npos) {
        last++;
    }
    else {
        last = 0;
    }
    return filePath.substr(last);
}

/// <summary>
/// Internal logging function
/// </summary>
void InternalWriteTrace(IHost::TraceType type, PCSTR message)
{
    IHost* host = GetHost();
    if (host != nullptr)
    {
        host->WriteTrace(type, message);
    }
    else
    {
        GetOutputService()->OutputString(IOutputService::OutputType::Logging, message);
    }
}

/// <summary>
/// Internal formatted logging helper function
/// </summary>
void InternalWriteTraceVaList(IHost::TraceType type, PCSTR format, va_list args)
{
    char str[1024];
    va_list argsCopy;
    va_copy(argsCopy, args);

    // Try and format our string into a fixed buffer first and see if it fits
    int length = vsnprintf(str, sizeof(str), format, args);
    if (length > 0)
    {
        if (length < sizeof(str))
        {
            InternalWriteTrace(type, str);
        }
        else
        {
            // Our stack buffer wasn't big enough to contain the entire formatted string
            char *str_ptr = (char*)::malloc(length + 1);
            if (str_ptr != nullptr)
            {
                vsnprintf(str_ptr, length + 1, format, argsCopy);
                InternalWriteTrace(type, str_ptr);
                ::free(str_ptr);
            }
        }
    }
    else
    {
        InternalWriteTrace(type, "FORMATING ERROR: ");
        InternalWriteTrace(type, format);
    }
}

/// <summary>
/// Internal formatted output helper function. Doesn't support format chars like %S.
/// </summary>
void InternalOutputVaList(IOutputService::OutputType type, PCSTR format, va_list args)
{
    char str[1024];
    va_list argsCopy;
    va_copy(argsCopy, args);

    // Try and format our string into a fixed buffer first and see if it fits
    int length = vsnprintf(str, sizeof(str), format, args);
    if (length > 0)
    {
        if (length < sizeof(str))
        {
            GetOutputService()->OutputString(type, str);
        }
        else
        {
            // Our stack buffer wasn't big enough to contain the entire formatted string
            char *str_ptr = (char*)::malloc(length + 1);
            if (str_ptr != nullptr)
            {
                if (vsnprintf(str_ptr, length + 1, format, argsCopy) > 0)
                {
                    GetOutputService()->OutputString(type, str_ptr);
                }
                ::free(str_ptr);
            }
        }
    }
    else
    {
        GetOutputService()->OutputString(type, "FORMATING ERROR: ");
        GetOutputService()->OutputString(type, format);
    }
}

/// <summary>
/// Internal trace output for extensions library. It sends the output to the console because
/// sending to the managed logging will cause recursion.
/// </summary>
void TraceHostingError(PCSTR format, ...)
{
    va_list args;
    va_start(args, format);
    GetOutputService()->OutputString(IOutputService::OutputType::Error, "SOS_HOSTING: ");
    InternalOutputVaList(IOutputService::OutputType::Error, format, args);
    va_end(args);
}
