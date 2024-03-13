// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

#include <minipal/utils.h>
#include <cor.h>
#include <corsym.h>
#include <clrdata.h>
#include <corhdr.h>
#include <cordebug.h>
#include <xcordebug.h>
#include <xclrdata.h>
#include <string>
#include <host.h>
#include <hostservices.h>
#include <debuggerservices.h>
#include <outputservice.h>
#include <symbolservice.h>
#include <releaseholder.h>
#include <arrayholder.h>
#include <ntimageex.h>

#define CONVERT_FROM_SIGN_EXTENDED(offset) ((ULONG_PTR)(offset))

#ifndef HOST_WINDOWS
#define _strdup       strdup
#endif

interface IRuntime;

enum HostRuntimeFlavor
{
    None,
    NetCore,
    NetFx
};

extern BOOL IsHostingInitialized();
extern HRESULT InitializeHosting();
extern bool SetHostRuntime(HostRuntimeFlavor flavor, int major, int minor, LPCSTR hostRuntimeDirectory);
extern void GetHostRuntime(HostRuntimeFlavor& flavor, int& major, int& minor, LPCSTR& hostRuntimeDirectory);
extern bool GetAbsolutePath(const char* path, std::string& absolutePath);
extern const std::string GetFileName(const std::string& filePath);
extern void InternalOutputVaList(IOutputService::OutputType type, PCSTR format, va_list args);
extern void InternalWriteTraceVaList(IHost::TraceType type, PCSTR format, va_list args);

#ifdef __cplusplus
extern "C" {
#endif

class Extensions
{
protected:
    static Extensions* s_extensions;

    IHost* m_pHost;
    ITarget* m_pTarget;
    IDebuggerServices* m_pDebuggerServices;
    IOutputService* m_pOutputService;
    IHostServices* m_pHostServices;
    ISymbolService* m_pSymbolService;

public:
    Extensions(IDebuggerServices* pDebuggerServices, IOutputService* pOutputService);
    virtual ~Extensions();

    static void Initialize(IDebuggerServices* debuggerServices, IOutputService* outputService);
    static void Uninitialize();

    /// <summary>
    /// Return the singleton extensions instance
    /// </summary>
    /// <returns></returns>
    static Extensions* GetInstance()
    {
        return s_extensions;
    }

    /// <summary>
    /// The extension host initialize callback function
    /// </summary>
    /// <param name="punk">IUnknown</param>
    /// <returns>error code</returns>
    HRESULT InitializeHostServices(IUnknown* punk);

    /// <summary>
    /// Returns the debugger services instance
    /// </summary>
    IDebuggerServices* GetDebuggerServices() 
    { 
        return m_pDebuggerServices;
    }

    /// <summary>
    /// Returns the output service instance
    /// </summary>
    IOutputService* GetOutputService()
    {
        return m_pOutputService;
    }

    /// <summary>
    /// Returns the host service provider or null
    /// </summary>
    IHost* GetHost();

    /// <summary>
    /// Returns the extension service interface or null
    /// </summary>
    IHostServices* GetHostServices();

    /// <summary>
    /// Returns the symbol service instance
    /// </summary>
    ISymbolService* GetSymbolService();

    /// <summary>
    /// Check if a target flush is needed
    /// </summary>
    void FlushCheck();

    /// <summary>
    /// Create a new target with the extension services for  
    /// </summary>
    /// <returns>error result</returns>
    HRESULT CreateTarget();

    /// <summary>
    /// Creates and/or destroys the target based on the processId.
    /// </summary>
    /// <param name="processId">process id or 0 if none</param>
    /// <returns>error result</returns>
    HRESULT UpdateTarget(ULONG processId);

    /// <summary>
    /// Create a new target with the extension services for  
    /// </summary>
    void DestroyTarget();

    /// <summary>
    /// Returns the target instance
    /// </summary>
    ITarget* GetTarget();

    /// <summary>
    /// Flush the target
    /// </summary>
    void FlushTarget();

    /// <summary>
    /// Releases and clears the target 
    /// </summary>
    void ReleaseTarget();
};

inline IHost* GetHost()
{
    return Extensions::GetInstance()->GetHost();
}

inline ITarget* GetTarget()
{
    return Extensions::GetInstance()->GetTarget();
} 

inline void ReleaseTarget()
{
    Extensions::GetInstance()->ReleaseTarget();
}

inline IHostServices* GetHostServices()
{
    return Extensions::GetInstance()->GetHostServices();
}

inline IDebuggerServices* GetDebuggerServices()
{
    return Extensions::GetInstance()->GetDebuggerServices();
}

inline IOutputService* GetOutputService()
{
    return Extensions::GetInstance()->GetOutputService();
}

inline ISymbolService* GetSymbolService()
{
    return Extensions::GetInstance()->GetSymbolService();
}

#ifdef __cplusplus
}
#endif
