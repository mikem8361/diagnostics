// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.ICorDebugServices.Interop
{
    [Flags]
    public enum CorDebugUserState
    {
        USER_STOP_REQUESTED = 0x01,
        USER_SUSPEND_REQUESTED = 0x02,
        USER_BACKGROUND = 0x04,
        USER_UNSTARTED = 0x08,
        USER_STOPPED = 0x10,
        USER_WAIT_SLEEP_JOIN = 0x20,
        USER_SUSPENDED = 0x40,

        // An "unsafe point" is a place where the thread may block a Garbage Collection (GC).
        // Debug events may be dispatched from unsafe points, but suspending a thread at
        // an unsafe spot will very likely cause a deadlock (until the thread is resumed).
        // This is a function of the thread's IP and the available GC info. The exact details
        // of what is safe and unsafe is unspecified and highly determined by jit/gc implementation details.
        USER_UNSAFE_POINT = 0x80,

        // indicates that this thread is a threadpool thread
        USER_THREADPOOL = 0x100,
    }
}
