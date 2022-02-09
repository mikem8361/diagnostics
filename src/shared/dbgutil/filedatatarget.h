// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

class FileDataTarget : public ICorDebugDataTarget
{
public:
    FileDataTarget(const WCHAR* modulePath) : 
        m_ref(1)
    {
        m_file = _wfopen(modulePath, W("rb"));
    }

    virtual ~FileDataTarget() 
    {
        if (m_file != NULL)
        {
            PAL_fclose(m_file);
            m_file = NULL;
        }
    }

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
    // ICorDebugDataTarget
    //

    virtual HRESULT STDMETHODCALLTYPE GetPlatform(CorDebugPlatform * pPlatform)
    {
        return E_NOTIMPL;
    }

    virtual HRESULT STDMETHODCALLTYPE ReadVirtual( 
        CORDB_ADDRESS address,
        BYTE * pBuffer,
        ULONG32 request,
        ULONG32 * pcbRead)
    {
        if (m_file == NULL)
        {
            return E_FAIL;
        }
        if (PAL_fseek(m_file, address, SEEK_SET) != 0)
        {
            return E_FAIL;
        }
        size_t read = PAL_fread(pBuffer, 1, request, m_file);
        if (pcbRead != nullptr)
        { 
            *pcbRead = read;
        }
        return S_OK;
    }

    virtual HRESULT STDMETHODCALLTYPE GetThreadContext(
        DWORD dwThreadOSID,
        ULONG32 contextFlags,
        ULONG32 contextSize,
        BYTE * context)
    {
        return E_NOTIMPL;
    }

protected:
    LONG m_ref;
    PAL_FILE* m_file;
};