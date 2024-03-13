// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

#include <stdarg.h>
#include <unknwn.h>

#ifdef __cplusplus
extern "C" {
#endif

/// <summary>
/// There are both global and per-command instances available. It normally implemented in native code
/// except for the dotnet-dump or VS hosts which is implemented in managed.
/// </summary>
MIDL_INTERFACE("30745290-DC07-4993-9B61-F6132CCEB663")
IOutputService : public IUnknown
{
public:
    // Must match OutputType in Microsoft.Diagnostics.DebugServices
    enum OutputType
    {
        Normal          = 0,
        Error           = 1,
        Warning         = 2,
        Logging         = 3,    // Used when logging to console is enabled. Allows the command output capture to ignore SOS logging output.
        Dml             = 4,    // Ignored if DML isn't supported or enabled.
    };

    virtual ULONG STDMETHODCALLTYPE GetOutputWidth() = 0;

    virtual ULONG STDMETHODCALLTYPE SupportsDml() = 0;

    virtual void STDMETHODCALLTYPE OutputString(
        OutputType outputType,
        PCSTR message) = 0;
};

#ifdef __cplusplus
};
#endif
