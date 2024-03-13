// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

#include <extensions.h>

class Target;

//----------------------------------------------------------------------------
// Local implementation of IHost
//----------------------------------------------------------------------------
class Host : public IHost
{
private:
    LONG m_ref;
    Target* m_target;
    IDebuggerServices* m_debuggerServices;

    static Host* s_host;

public:
    Host(IDebuggerServices* debuggerServices);
    virtual ~Host();

#ifndef FEATURE_PAL
    static bool SwitchRuntime(bool desktop);
#endif
    static void DisplayStatus();

    //----------------------------------------------------------------------------
    // IUnknown
    //----------------------------------------------------------------------------

    HRESULT STDMETHODCALLTYPE QueryInterface(
        REFIID InterfaceId,
        PVOID* Interface);

    ULONG STDMETHODCALLTYPE AddRef();

    ULONG STDMETHODCALLTYPE Release();

    //----------------------------------------------------------------------------
    // IHost
    //----------------------------------------------------------------------------

    IHost::HostType STDMETHODCALLTYPE GetHostType();

    HRESULT STDMETHODCALLTYPE GetService(REFIID serviceId, PVOID* ppService);

    HRESULT STDMETHODCALLTYPE GetCurrentTarget(ITarget** ppTarget);

    void STDMETHODCALLTYPE WriteTrace(TraceType type, PCSTR message);
};
