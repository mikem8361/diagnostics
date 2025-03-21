// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//----------------------------------------------------------------------------
//
// Debugger engine interface subset implemented with ILLDBServices
//
//----------------------------------------------------------------------------

#ifndef __DBGENG_H__
#define __DBGENG_H__

#include <unknwn.h>
#include <rpc.h>
#include "lldbservices.h"
#include "arrayholder.h"

#ifdef __cplusplus
extern "C" {
#endif

class DebugClient
{
private:
    LONG m_ref;
    ILLDBServices* m_lldbservices;
    ILLDBServices2* m_lldbservices2;

public:
    DebugClient(ILLDBServices* lldbservices, ILLDBServices2* lldbservices2) : 
        m_ref(1),
        m_lldbservices(lldbservices),
        m_lldbservices2(lldbservices2)
    {
        m_lldbservices->AddRef();
        m_lldbservices2->AddRef();
    }

    ~DebugClient()
    {
        m_lldbservices->Release();
        m_lldbservices2->Release();
    }

    //----------------------------------------------------------------------------
    // IUnknown
    //----------------------------------------------------------------------------

    HRESULT 
    QueryInterface(
        REFIID InterfaceId,
        PVOID* Interface);

    ULONG AddRef();

    ULONG Release();

    //----------------------------------------------------------------------------
    // IDebugControl2
    //----------------------------------------------------------------------------

    // Checks for a user interrupt, such a Ctrl-C
    // or stop button.
    // This method is reentrant.
    HRESULT 
    GetInterrupt()
    {
        return m_lldbservices->GetInterrupt();
    }

    HRESULT 
    OutputVaList(
        ULONG mask,
        PCSTR format,
        va_list args)
    {
        return m_lldbservices->OutputVaList(mask, format, args);
    }

    HRESULT 
    ControlledOutputVaList(
        ULONG outputControl,
        ULONG mask,
        PCSTR format,
        va_list args)
    {
        return OutputVaList(mask, format, args);
    }

    // Returns information about the debuggee such
    // as user vs. kernel, dump vs. live, etc.
    HRESULT 
    GetDebuggeeType(
        PULONG debugClass,
        PULONG qualifier)
    {
        return m_lldbservices->GetDebuggeeType(debugClass, qualifier);
    }

    // Returns the page size for the currently executing
    // processor context.  The page size may vary between
    // processor types.
    HRESULT 
    GetPageSize(
        PULONG size)
    {
        return m_lldbservices->GetPageSize(size);
    }

    HRESULT 
    GetExecutingProcessorType(
        PULONG type)
    {
        return m_lldbservices->GetProcessorType(type);
    }

    HRESULT 
    GetActualProcessorType(
        PULONG type)
    {
        return m_lldbservices->GetProcessorType(type);
    }

    HRESULT 
    Execute(
        ULONG outputControl,
        PCSTR command,
        ULONG flags)
    {
        return m_lldbservices->Execute(outputControl, command, flags);
    }

    HRESULT 
    GetLastEventInformation(
        PULONG type,
        PULONG processId,
        PULONG threadId,
        PVOID extraInformation,
        ULONG extraInformationSize,
        PULONG extraInformationUsed,
        PSTR description,
        ULONG descriptionSize,
        PULONG descriptionUsed)
    {
        return m_lldbservices->GetLastEventInformation(type, processId, threadId, extraInformation, 
            extraInformationSize, extraInformationUsed, description, descriptionSize, descriptionUsed);
    }

    HRESULT 
    Disassemble(
        ULONG64 offset,
        ULONG flags,
        PSTR buffer,
        ULONG bufferSize,
        PULONG disassemblySize,
        PULONG64 endOffset)
    {
        return m_lldbservices->Disassemble(offset, flags, buffer, bufferSize, disassemblySize, endOffset);
    }

    //----------------------------------------------------------------------------
    // IDebugControl4
    //----------------------------------------------------------------------------

    // Stack tracing with a full initial context
    // and full context return for each frame.
    // The FrameContextsSize parameter is the total
    // byte size of FrameContexts.  FrameContextsEntrySize
    // gives the byte size of each entry in
    // FrameContexts.
    HRESULT
    GetContextStackTrace(
        PVOID startContext,
        ULONG startContextSize,
        PDEBUG_STACK_FRAME frames,
        ULONG framesSize,
        PVOID frameContexts,
        ULONG frameContextsSize,
        ULONG frameContextsEntrySize,
        PULONG framesFilled)
    {
        return m_lldbservices->GetContextStackTrace(startContext, startContextSize, frames, 
            framesSize, frameContexts, frameContextsSize, frameContextsEntrySize, framesFilled);
    }

    //----------------------------------------------------------------------------
    // IDebugDataSpaces
    //----------------------------------------------------------------------------

    HRESULT 
    ReadVirtual(
        ULONG64 offset,
        PVOID buffer,
        ULONG bufferSize,
        PULONG bytesRead)
    {
        return m_lldbservices->ReadVirtual(offset, buffer, bufferSize, bytesRead);
    }

    HRESULT 
    WriteVirtual(
        ULONG64 offset,
        PVOID buffer,
        ULONG bufferSize,
        PULONG bytesWritten)
    {
        return m_lldbservices->WriteVirtual(offset, buffer, bufferSize, bytesWritten);
    }

    //----------------------------------------------------------------------------
    // IDebugSymbols
    //----------------------------------------------------------------------------

    HRESULT 
    GetSymbolOptions(
        PULONG options)
    {
        return m_lldbservices->GetSymbolOptions(options);
    }

    HRESULT 
    GetNameByOffset(
        ULONG64 offset,
        PSTR nameBuffer,
        ULONG nameBufferSize,
        PULONG nameSize,
        PULONG64 displacement)
    {
        return m_lldbservices->GetNameByOffset(offset, nameBuffer, nameBufferSize, nameSize, displacement);
    }

    HRESULT 
    GetNumberModules(
        PULONG loaded,
        PULONG unloaded)
    {
        return m_lldbservices->GetNumberModules(loaded, unloaded);
    }

    HRESULT GetModuleByIndex(
        ULONG index,
        PULONG64 base)
    {
        return m_lldbservices->GetModuleByIndex(index, base);
    }

    HRESULT 
    GetModuleByModuleName(
        PCSTR name,
        ULONG startIndex,
        PULONG index,
        PULONG64 base)
    {
        return m_lldbservices->GetModuleByModuleName(name, startIndex, index, base);
    }

    HRESULT 
    GetModuleByOffset(
        ULONG64 offset,
        ULONG startIndex,
        PULONG index,
        PULONG64 base)
    {
        return m_lldbservices->GetModuleByOffset(offset, startIndex, index, base);
    }

    HRESULT 
    GetModuleNames(
        ULONG index,
        ULONG64 base,
        PSTR imageNameBuffer,
        ULONG imageNameBufferSize,
        PULONG imageNameSize,
        PSTR moduleNameBuffer,
        ULONG moduleNameBufferSize,
        PULONG moduleNameSize,
        PSTR loadedImageNameBuffer,
        ULONG loadedImageNameBufferSize,
        PULONG loadedImageNameSize)
    {
        return m_lldbservices->GetModuleNames(index, base, imageNameBuffer, imageNameBufferSize, imageNameSize, moduleNameBuffer,
            moduleNameBufferSize, moduleNameSize, loadedImageNameBuffer, loadedImageNameBufferSize, loadedImageNameSize);
    }

    HRESULT 
    GetModuleVersionInformation(
        ULONG index,
        ULONG64 base,
        PCSTR item,
        PVOID buffer,
        ULONG bufferSize,
        PULONG versionInfoSize)
    {
        return m_lldbservices2->GetModuleVersionInformation(index, base, item, buffer, bufferSize, versionInfoSize);
    }

    HRESULT 
    GetLineByOffset(
        ULONG64 offset,
        PULONG line,
        PSTR fileBuffer,
        ULONG fileBufferSize,
        PULONG fileSize,
        PULONG64 displacement)
    {
        return m_lldbservices->GetLineByOffset(offset, line, fileBuffer, fileBufferSize, fileSize, displacement);
    }
     
    HRESULT 
    GetSourceFileLineOffsets(
        PCSTR file,
        PULONG64 buffer,
        ULONG bufferLines,
        PULONG fileLines)
    {
        return m_lldbservices->GetSourceFileLineOffsets(file, buffer, bufferLines, fileLines);
    }

    // Uses the given file path and the source path
    // information to try and locate an existing file.
    // The given file path is merged with elements
    // of the source path and checked for existence.
    // If a match is found the element used is returned.
    // A starting element can be specified to restrict
    // the search to a subset of the path elements;
    // this can be useful when checking for multiple
    // matches along the source path.
    // The returned element can be 1, indicating
    // the file was found directly and not on the path.
    HRESULT 
    FindSourceFile(
        ULONG startElement,
        PCSTR file,
        ULONG flags,
        PULONG foundElement,
        PSTR buffer,
        ULONG bufferSize,
        PULONG foundSize)
    {
        return m_lldbservices->FindSourceFile(startElement, file, flags, foundElement, buffer, bufferSize, foundSize);
    }

    //----------------------------------------------------------------------------
    // IDebugSystemObjects
    //----------------------------------------------------------------------------

    HRESULT 
    GetCurrentProcessSystemId(
        PULONG id)
    {
        return m_lldbservices->GetCurrentProcessSystemId(id);
    }

    HRESULT 
    GetCurrentThreadId(
        PULONG id)
    {
        return m_lldbservices->GetCurrentThreadId(id);
    }

    HRESULT 
    SetCurrentThreadId(
        ULONG id)
    {
        return m_lldbservices->SetCurrentThreadId(id);
    }

    HRESULT 
    GetCurrentThreadSystemId(
        PULONG sysId)
    {
        return m_lldbservices->GetCurrentThreadSystemId(sysId);
    }

    HRESULT 
    GetThreadIdBySystemId(
        ULONG sysId,
        PULONG threadId)
    {
        return m_lldbservices->GetThreadIdBySystemId(sysId, threadId);
    }

    //----------------------------------------------------------------------------
    // IDebugRegisters
    //----------------------------------------------------------------------------

    HRESULT
    GetValueByName(
        PCSTR name,
        PDWORD_PTR debugValue)
    {
        return m_lldbservices->GetValueByName(name, debugValue);
    }

    HRESULT 
    GetInstructionOffset(
        PULONG64 offset)
    {
        return m_lldbservices->GetInstructionOffset(offset);
    }

    HRESULT 
    GetStackOffset(
        PULONG64 offset)
    {
        return m_lldbservices->GetStackOffset(offset);
    }

    HRESULT 
    GetFrameOffset(
        PULONG64 offset)
    {
        return m_lldbservices->GetFrameOffset(offset);
    }
};
 
MIDL_INTERFACE("d4366723-44df-4bed-8c7e-4c05424f4588")
IDebugControl2 : DebugClient
{
};

MIDL_INTERFACE("94e60ce9-9b41-4b19-9fc0-6d9eb35272b3")
IDebugControl4 : DebugClient
{
};

MIDL_INTERFACE("88f7dfab-3ea7-4c3a-aefb-c4e8106173aa")
IDebugDataSpaces : DebugClient
{
};

MIDL_INTERFACE("8c31e98c-983a-48a5-9016-6fe5d667a950")
IDebugSymbols : DebugClient
{
};

MIDL_INTERFACE("3a707211-afdd-4495-ad4f-56fecdf8163f")
IDebugSymbols2 : DebugClient
{
};

MIDL_INTERFACE("6b86fe2c-2c4f-4f0c-9da2-174311acc327")
IDebugSystemObjects : DebugClient
{
};

MIDL_INTERFACE("ce289126-9e84-45a7-937e-67bb18691493")
IDebugRegisters : DebugClient
{
};

typedef interface ILLDBServices* PDEBUG_CLIENT;
typedef interface IDebugControl2* PDEBUG_CONTROL2;
typedef interface IDebugControl4* PDEBUG_CONTROL4;
typedef interface IDebugDataSpaces* PDEBUG_DATA_SPACES;
typedef interface IDebugSymbols* PDEBUG_SYMBOLS;
typedef interface IDebugSymbols2* PDEBUG_SYMBOLS2;
typedef interface IDebugSystemObjects* PDEBUG_SYSTEM_OBJECTS;
typedef interface IDebugRegisters* PDEBUG_REGISTERS;

#ifdef __cplusplus
};
#endif

#endif // #ifndef __DBGENG_H__
