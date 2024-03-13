// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "exts.h"
#ifndef FEATURE_PAL
#include "dbgengservices.h"
#endif

/**********************************************************************\
 * Called when the managed host or plug-in loads/initializes SOS.
\**********************************************************************/
extern "C" HRESULT STDMETHODCALLTYPE SOSInitializeByHost(IUnknown* punk, IDebuggerServices* debuggerServices, IOutputService* outputService)
{
    IHost* host = nullptr;
    HRESULT hr;

    if (punk != nullptr)
    {
        hr = punk->QueryInterface(__uuidof(IHost), (void**)&host);
        if (FAILED(hr)) {
            return hr;
        }
    }
    hr = SOSExtensions::Initialize(host, debuggerServices, outputService);
    if (FAILED(hr)) {
        return hr;
    }
#ifndef FEATURE_PAL
    // When SOS is hosted on dotnet-dump on Windows, the ExtensionApis are not set so 
    // the expression evaluation function needs to be supplied.
    if (GetExpression == nullptr)
    {
        GetExpression = ([](const char* message) {
		    ISymbolService* symbolService = GetSymbolService();
		    if (symbolService == nullptr)
		    {
		        return (ULONG64)0;
		    }
            return symbolService->GetExpressionValue(message);
        });
    }
#endif
    return S_OK;
}

/**********************************************************************\
 * Called when the managed host or plug-in exits.
\**********************************************************************/
extern "C" void STDMETHODCALLTYPE SOSUninitializeByHost()
{
    OnUnloadTask::Run();
}

/**********************************************************************\
 * Extensions for the Windows SOS or the Linux or MacOS libsos side.
\**********************************************************************/

SOSExtensions::SOSExtensions(IDebuggerServices* debuggerServices, IOutputService* outputService, IHost* host) :
    Extensions(debuggerServices, outputService)
{
    m_pHost = host;
    OnUnloadTask::Register(SOSExtensions::Uninitialize);
}

#ifndef FEATURE_PAL
SOSExtensions::~SOSExtensions()
{
    if (m_pDebuggerServices != nullptr)
    {
        ((DbgEngServices*)m_pDebuggerServices)->Uninitialize();
        m_pDebuggerServices->Release();
        m_pDebuggerServices = nullptr;
    }
}
#endif

#ifndef FEATURE_PAL
HRESULT
SOSExtensions::Initialize(IDebugClient* client)
{
    if (s_extensions == nullptr)
    {
        DbgEngServices* debuggerServices = new DbgEngServices(client);
        HRESULT hr = debuggerServices->Initialize();
        if (FAILED(hr)) {
            return hr;
        }
        s_extensions = new SOSExtensions(debuggerServices, debuggerServices, nullptr);
    }
    return S_OK;
}
#endif

HRESULT
SOSExtensions::Initialize(IHost* host, IDebuggerServices* debuggerServices, IOutputService* outputService)
{
    if (s_extensions == nullptr) 
    {
        s_extensions = new SOSExtensions(debuggerServices, outputService, host);
    }
    return S_OK;
}

/// <summary>
/// Returns the runtime or fails if no target or current runtime
/// </summary>
/// <param name="ppRuntime">runtime instance</param>
/// <returns>error code</returns>
HRESULT
GetRuntime(IRuntime** ppRuntime)
{
    Extensions* extensions = Extensions::GetInstance();
    ITarget* target = extensions->GetTarget();
    if (target == nullptr)
    {
        return E_FAIL;
    }
    // Flush here only on Windows under dbgeng. The lldb sos plugin handles it for Linux/MacOS.
#ifndef FEATURE_PAL
    extensions->FlushCheck();
#endif
    return target->GetRuntime(ppRuntime);
}
