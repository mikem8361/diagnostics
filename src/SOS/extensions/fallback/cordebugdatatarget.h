// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef __cordebugdatatarget_h__
#define __cordebugdatatarget_h__

/**********************************************************************\
* Data target for the debugged process. Provided to OpenVirtualProcess 
* in order to get an ICorDebugProcess back.
\**********************************************************************/
class CorDebugDataTarget : public ICorDebugMutableDataTarget, public ICorDebugDataTarget4
{
protected:
    LONG m_ref;
    IDebuggerServices* m_debuggerServices;

public:
    CorDebugDataTarget(IDebuggerServices* debuggerServices) :
        m_ref(1),
        m_debuggerServices(debuggerServices)
    {
    }

    virtual ~CorDebugDataTarget() {}

    virtual HRESULT STDMETHODCALLTYPE QueryInterface(
        REFIID InterfaceId,
        PVOID* pInterface)
    {
        if (InterfaceId == IID_IUnknown)
        {
            *pInterface = static_cast<IUnknown *>(static_cast<ICorDebugDataTarget *>(this));
        }
        else if (InterfaceId == IID_ICorDebugDataTarget)
        {
            *pInterface = static_cast<ICorDebugDataTarget *>(this);
        }
        else if (InterfaceId == IID_ICorDebugMutableDataTarget)
        {
            *pInterface = static_cast<ICorDebugMutableDataTarget *>(this);
        }
        else if (InterfaceId == IID_ICorDebugDataTarget4)
        {
            *pInterface = static_cast<ICorDebugDataTarget4 *>(this);
        }
        else
        {
            *pInterface = NULL;
            return E_NOINTERFACE;
        }

        AddRef();
        return S_OK;
    }
    
    virtual ULONG STDMETHODCALLTYPE AddRef()
    {
        return InterlockedIncrement(&m_ref);    
    }

    virtual ULONG STDMETHODCALLTYPE Release()
    {
        LONG ref = InterlockedDecrement(&m_ref);
        if (ref == 0)
        {
            delete this;
        }
        return ref;
    }

    //
    // ICorDebugDataTarget.
    //

    virtual HRESULT STDMETHODCALLTYPE GetPlatform(
        CorDebugPlatform * pPlatform)
    {
        ULONG platformKind;
        HRESULT hr = m_debuggerServices->GetProcessorType(&platformKind);
        if (FAILED(hr)) {
            return hr;
        }
        IDebuggerServices::OperatingSystem os;
        hr = m_debuggerServices->GetOperatingSystem(&os);
        if (FAILED(hr)) {
            return hr;
        }
        if (os == IDebuggerServices::OperatingSystem::Windows)
        {
            switch (platformKind)
            {
                case IMAGE_FILE_MACHINE_I386:
                    *pPlatform = CORDB_PLATFORM_WINDOWS_X86;
                    break;
                case IMAGE_FILE_MACHINE_AMD64:
                    *pPlatform = CORDB_PLATFORM_WINDOWS_AMD64;
                    break;
                case IMAGE_FILE_MACHINE_ARMNT:
                    *pPlatform = CORDB_PLATFORM_WINDOWS_ARM;
                    break;
                case IMAGE_FILE_MACHINE_ARM64:
                    *pPlatform = CORDB_PLATFORM_WINDOWS_ARM64;
                    break;
                default:
                    return E_FAIL;
            }
        }
        else
        {
            switch (platformKind)
            {
                case IMAGE_FILE_MACHINE_I386:
                    *pPlatform = CORDB_PLATFORM_POSIX_X86;
                    break;
                case IMAGE_FILE_MACHINE_AMD64:
                    *pPlatform = CORDB_PLATFORM_POSIX_AMD64;
                    break;
                case IMAGE_FILE_MACHINE_ARMNT:
                    *pPlatform = CORDB_PLATFORM_POSIX_ARM;
                    break;
                case IMAGE_FILE_MACHINE_ARM64:
                    *pPlatform = CORDB_PLATFORM_POSIX_ARM64;
                    break;
                case IMAGE_FILE_MACHINE_RISCV64:
                    *pPlatform = CORDB_PLATFORM_POSIX_RISCV64;
                    break;
                case IMAGE_FILE_MACHINE_LOONGARCH64:
                    *pPlatform = CORDB_PLATFORM_POSIX_LOONGARCH64;
                    break;
                default:
                    return E_FAIL;
            }
        }
        return S_OK;
    }

    virtual HRESULT STDMETHODCALLTYPE ReadVirtual( 
        CORDB_ADDRESS address,
        BYTE * pBuffer,
        ULONG32 request,
        ULONG32 * pcbRead)
    {
        address = CONVERT_FROM_SIGN_EXTENDED(address);
        HRESULT hr = m_debuggerServices->ReadVirtual(address, pBuffer, request, (PULONG)pcbRead);
        if (FAILED(hr)) 
        {
            //ExtDbgOut("CorDebugDataTarget::ReadVirtual FAILED %08x address %p size %08x\n", hr, address, request);
        }
        return hr;
    }

    virtual HRESULT STDMETHODCALLTYPE GetThreadContext(
        DWORD dwThreadOSID,
        ULONG32 contextFlags,
        ULONG32 contextSize,
        BYTE * context)
    {
        return m_debuggerServices->GetThreadContextBySystemId(dwThreadOSID, contextFlags, contextSize, context);
    }

    //
    // ICorDebugMutableDataTarget.
    //

    virtual HRESULT STDMETHODCALLTYPE WriteVirtual(
        CORDB_ADDRESS address,
        const BYTE * pBuffer,
        ULONG32 bytesRequested)
    {
        return m_debuggerServices->WriteVirtual(address, (PVOID)pBuffer, bytesRequested, NULL);
    }

    virtual HRESULT STDMETHODCALLTYPE SetThreadContext(
        DWORD dwThreadID,
        ULONG32 contextSize,
        const BYTE * pContext)
    {
        return E_NOTIMPL;
    }

    virtual HRESULT STDMETHODCALLTYPE ContinueStatusChanged(
        DWORD dwThreadId,
        CORDB_CONTINUE_STATUS continueStatus)
    {
        return E_NOTIMPL;
    }

    //
    // ICorDebugDataTarget4
    //
    virtual HRESULT STDMETHODCALLTYPE VirtualUnwind(
        DWORD threadId,
        ULONG32 contextSize,
        PBYTE context)
    {
#ifdef FEATURE_PAL
        return m_debuggerServices->VirtualUnwind(threadId, contextSize, context);
#else 
        return E_NOTIMPL;
#endif
    }
};

#endif // __cordebugdatatarget_h__
