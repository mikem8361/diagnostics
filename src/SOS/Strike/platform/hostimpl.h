// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

#include "host.h"

//----------------------------------------------------------------------------
// Local implementation of IHost
//----------------------------------------------------------------------------
class Host : public IHost
{
private:
    LONG m_ref;
    ITarget* m_target;
    IDebuggerServices* m_debuggerServices;

public:
    Host(IDebuggerServices* debuggerServices);
    virtual ~Host();

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
