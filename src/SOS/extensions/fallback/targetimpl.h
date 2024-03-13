// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

#include <extensions.h>
#include <target.h>
#include "runtimeimpl.h"

extern bool IsWindowsTarget();

//----------------------------------------------------------------------------
// Local implementation of ITarget when the host doesn't provide it
//----------------------------------------------------------------------------
class Target : public ITarget
{
private:
    LONG m_ref;
    LPCSTR m_tmpPath;
    IDebuggerServices* m_debuggerServices;
#ifndef FEATURE_PAL
    Runtime* m_desktop;
#endif
    Runtime* m_netcore;

public:
    Target(IDebuggerServices* debuggerServices);
    virtual ~Target();

    HRESULT CreateInstance(IRuntime** ppRuntime);
#ifndef FEATURE_PAL
    bool SwitchRuntime(bool desktop);
#endif
    void DisplayStatus();

    //----------------------------------------------------------------------------
    // IUnknown
    //----------------------------------------------------------------------------

    HRESULT STDMETHODCALLTYPE QueryInterface(
        REFIID InterfaceId,
        PVOID* Interface);

    ULONG STDMETHODCALLTYPE AddRef();

    ULONG STDMETHODCALLTYPE Release();

    //----------------------------------------------------------------------------
    // ITarget
    //----------------------------------------------------------------------------

    OperatingSystem STDMETHODCALLTYPE GetOperatingSystem();

    HRESULT STDMETHODCALLTYPE GetService(REFIID serviceId, PVOID* ppService);

    LPCSTR STDMETHODCALLTYPE GetTempDirectory();

    HRESULT STDMETHODCALLTYPE GetRuntime(IRuntime** pRuntime);

    void STDMETHODCALLTYPE Flush();
};
