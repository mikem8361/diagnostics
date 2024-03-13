// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "targetimpl.h"
#include "hostimpl.h"

Host* Host::s_host = nullptr;

//----------------------------------------------------------------------------
// Host
//----------------------------------------------------------------------------

Host::Host(IDebuggerServices* debuggerServices) :
    m_ref(1),
    m_target(nullptr),
    m_debuggerServices(debuggerServices)
{ 
    debuggerServices->AddRef();
    s_host = this;
}

Host::~Host()
{
    if (m_target != nullptr)
    {
        m_target->Release();
        m_target = nullptr;
    }
    if (m_debuggerServices != nullptr)
    {
        m_debuggerServices->Release();
        m_debuggerServices = nullptr;
    }
    s_host = nullptr;
}

#ifndef FEATURE_PAL
bool Host::SwitchRuntime(bool desktop)
{
    if (s_host != nullptr)
    {
        return s_host->m_target->SwitchRuntime(desktop);
    }
    return false;
}
#endif

void Host::DisplayStatus()
{
    if (s_host != nullptr)
    {
        s_host->m_target->DisplayStatus();
    }
}

//----------------------------------------------------------------------------
// IUnknown
//----------------------------------------------------------------------------

HRESULT Host::QueryInterface(
    REFIID InterfaceId,
    PVOID* Interface
    )
{
    if (InterfaceId == __uuidof(IUnknown) ||
        InterfaceId == __uuidof(IHost))
    {
        *Interface = (IHost*)this;
        AddRef();
        return S_OK;
    }
    else
    {
        *Interface = NULL;
        return E_NOINTERFACE;
    }
}

ULONG Host::AddRef()
{
    LONG ref = InterlockedIncrement(&m_ref);    
    return ref;
}

ULONG Host::Release()
{
    LONG ref = InterlockedDecrement(&m_ref);
    if (ref == 0)
    {
        delete this;
    }
    return ref;
}

//----------------------------------------------------------------------------
// IHost
//----------------------------------------------------------------------------

IHost::HostType Host::GetHostType()
{
#ifdef FEATURE_PAL
    return HostType::Lldb;
#else
    return HostType::DbgEng;
#endif
}

HRESULT Host::GetService(REFIID serviceId, PVOID* ppService)
{
    return E_NOINTERFACE;
}

HRESULT Host::GetCurrentTarget(ITarget** ppTarget)
{
    if (ppTarget == nullptr)
    {
        return E_INVALIDARG;
    }
    if (m_target == nullptr)
    {
        m_target = new Target(m_debuggerServices);
    }
    m_target->AddRef();
    *ppTarget = m_target;
    return S_OK;
}

void Host::WriteTrace(IHost::TraceType type, PCSTR message)
{
    GetOutputService()->OutputString(IOutputService::OutputType::Logging, message);
}
