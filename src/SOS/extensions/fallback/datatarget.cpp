// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "datatarget.h"
#include <remotememoryservice.h>

DataTarget::DataTarget(IDebuggerServices* debuggerServices, ULONG64 baseAddress) :
    m_ref(0),
    m_debuggerServices(debuggerServices),
    m_baseAddress(baseAddress)
{
}

STDMETHODIMP
DataTarget::QueryInterface(
    THIS_
    REFIID InterfaceId,
    PVOID* Interface
    )
{
    if (InterfaceId == IID_IUnknown ||
        InterfaceId == IID_ICLRDataTarget)
    {
        *Interface = (ICLRDataTarget*)this;
        AddRef();
        return S_OK;
    }
    else if (InterfaceId == IID_ICLRDataTarget2)
    {
        *Interface = (ICLRDataTarget2*)this;
        AddRef();
        return S_OK;
    }
    else if (InterfaceId == IID_ICorDebugDataTarget4)
    {
        *Interface = (ICorDebugDataTarget4*)this;
        AddRef();
        return S_OK;
    }
    else if (InterfaceId == IID_ICLRRuntimeLocator)
    {
        *Interface = (ICLRRuntimeLocator*)this;
        AddRef();
        return S_OK;
    }
    else
    {
        *Interface = NULL;
        return E_NOINTERFACE;
    }
}

STDMETHODIMP_(ULONG)
DataTarget::AddRef(
    THIS
    )
{
    LONG ref = InterlockedIncrement(&m_ref);    
    return ref;
}

STDMETHODIMP_(ULONG)
DataTarget::Release(
    THIS
    )
{
    LONG ref = InterlockedDecrement(&m_ref);
    if (ref == 0)
    {
        delete this;
    }
    return ref;
}

HRESULT STDMETHODCALLTYPE
DataTarget::GetMachineType(
    /* [out] */ ULONG32 *machine)
{
    return m_debuggerServices->GetProcessorType((PULONG)machine);
}

HRESULT STDMETHODCALLTYPE
DataTarget::GetPointerSize(
    /* [out] */ ULONG32 *size)
{
    ULONG machine;
    HRESULT hr = m_debuggerServices->GetProcessorType(&machine);
    if (FAILED(hr))
    {
        return hr;
    }
    switch (machine)
    {
        case IMAGE_FILE_MACHINE_AMD64:
        case IMAGE_FILE_MACHINE_ARM64:
        case IMAGE_FILE_MACHINE_RISCV64:
        case IMAGE_FILE_MACHINE_LOONGARCH64:
            *size = 8;
            break;
        case IMAGE_FILE_MACHINE_ARM:
        case IMAGE_FILE_MACHINE_THUMB:
        case IMAGE_FILE_MACHINE_ARMNT:
        case IMAGE_FILE_MACHINE_I386:
            *size = 4;
            break;
    }
    return S_OK;
}

HRESULT STDMETHODCALLTYPE
DataTarget::GetImageBase(
    /* [string][in] */ LPCWSTR name,
    /* [out] */ CLRDATA_ADDRESS *base)
{
    CHAR lpstr[MAX_LONGPATH];
    int name_length = WideCharToMultiByte(CP_ACP, 0, name, -1, lpstr, MAX_LONGPATH, NULL, NULL);
    if (name_length == 0)
    {
        return E_FAIL;
    }
#ifndef FEATURE_PAL
    // Remove the extension on Windows/dbgeng.
    CHAR *lp = strrchr(lpstr, '.');
    if (lp != nullptr)
    {
        *lp = '\0';
    }
#endif
    return m_debuggerServices->GetModuleByModuleName(lpstr, 0, NULL, base);
}

HRESULT STDMETHODCALLTYPE
DataTarget::ReadVirtual(
    /* [in] */ CLRDATA_ADDRESS address,
    /* [length_is][size_is][out] */ PBYTE buffer,
    /* [in] */ ULONG32 request,
    /* [optional][out] */ ULONG32 *done)
{
    address = CONVERT_FROM_SIGN_EXTENDED(address);
    HRESULT hr = m_debuggerServices->ReadVirtual(address, (PVOID)buffer, request, (PULONG)done);
    if (FAILED(hr)) 
    {
        //ExtDbgOut("DataTarget::ReadVirtual FAILED %08x address %08llx size %08x\n", hr, address, request);
    }
    return hr;
}

HRESULT STDMETHODCALLTYPE
DataTarget::WriteVirtual(
    /* [in] */ CLRDATA_ADDRESS address,
    /* [size_is][in] */ PBYTE buffer,
    /* [in] */ ULONG32 request,
    /* [optional][out] */ ULONG32 *done)
{
    return m_debuggerServices->WriteVirtual(address, (PVOID)buffer, request, (PULONG)done);
}

HRESULT STDMETHODCALLTYPE
DataTarget::GetTLSValue(
    /* [in] */ ULONG32 threadID,
    /* [in] */ ULONG32 index,
    /* [out] */ CLRDATA_ADDRESS* value)
{
    return E_NOTIMPL;
}

HRESULT STDMETHODCALLTYPE
DataTarget::SetTLSValue(
    /* [in] */ ULONG32 threadID,
    /* [in] */ ULONG32 index,
    /* [in] */ CLRDATA_ADDRESS value)
{
    return E_NOTIMPL;
}

HRESULT STDMETHODCALLTYPE
DataTarget::GetCurrentThreadID(
    /* [out] */ ULONG32 *threadID)
{
    return m_debuggerServices->GetCurrentThreadSystemId((PULONG)threadID);
}

HRESULT STDMETHODCALLTYPE
DataTarget::GetThreadContext(
    /* [in] */ ULONG32 threadID,
    /* [in] */ ULONG32 contextFlags,
    /* [in] */ ULONG32 contextSize,
    /* [out, size_is(contextSize)] */ PBYTE context)
{
    return m_debuggerServices->GetThreadContextBySystemId(threadID, contextFlags, contextSize, context);
}

HRESULT STDMETHODCALLTYPE
DataTarget::SetThreadContext(
    /* [in] */ ULONG32 threadID,
    /* [in] */ ULONG32 contextSize,
    /* [out, size_is(contextSize)] */ PBYTE context)
{
    return E_NOTIMPL;
}

HRESULT STDMETHODCALLTYPE
DataTarget::Request(
    /* [in] */ ULONG32 reqCode,
    /* [in] */ ULONG32 inBufferSize,
    /* [size_is][in] */ BYTE *inBuffer,
    /* [in] */ ULONG32 outBufferSize,
    /* [size_is][out] */ BYTE *outBuffer)
{
    return E_NOTIMPL;
}

// ICLRDataTarget2

HRESULT STDMETHODCALLTYPE 
DataTarget::AllocVirtual(
    /* [in] */ CLRDATA_ADDRESS addr,
    /* [in] */ ULONG32 size,
    /* [in] */ ULONG32 typeFlags,
    /* [in] */ ULONG32 protectFlags,
    /* [out] */ CLRDATA_ADDRESS* virt)
{
    HRESULT hr;
    ReleaseHolder<IRemoteMemoryService> remote;
    if (FAILED(hr = m_debuggerServices->QueryInterface(__uuidof(IRemoteMemoryService), (void**)&remote)))
    {
        return hr;
    }
    return remote->AllocVirtual(addr, size, typeFlags, protectFlags, virt);
}
        
HRESULT STDMETHODCALLTYPE 
DataTarget::FreeVirtual(
    /* [in] */ CLRDATA_ADDRESS addr,
    /* [in] */ ULONG32 size,
    /* [in] */ ULONG32 typeFlags)
{
    HRESULT hr;
    ReleaseHolder<IRemoteMemoryService> remote;
    if (FAILED(hr = m_debuggerServices->QueryInterface(__uuidof(IRemoteMemoryService), (void**)&remote)))
    {
        return hr;
    }
    return remote->FreeVirtual(addr, size, typeFlags);
}

// ICorDebugDataTarget4

HRESULT STDMETHODCALLTYPE 
DataTarget::VirtualUnwind(
    /* [in] */ DWORD threadId,
    /* [in] */ ULONG32 contextSize,
    /* [in, out, size_is(contextSize)] */ PBYTE context)
{
#ifdef FEATURE_PAL
    return m_debuggerServices->VirtualUnwind(threadId, contextSize, context);
#else
    return E_NOTIMPL;
#endif
}

// ICLRRuntimeLocator

HRESULT STDMETHODCALLTYPE 
DataTarget::GetRuntimeBase(
    /* [out] */ CLRDATA_ADDRESS* baseAddress)
{
    *baseAddress = m_baseAddress;
    return S_OK;
}
