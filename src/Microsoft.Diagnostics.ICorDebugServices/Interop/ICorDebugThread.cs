// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.ICorDebugServices.Interop
{
    public unsafe class ICorDebugThread : CallableCOMWrapper
    {
        private static readonly Guid IID_ICorDebugThread = new("938c6d66-7fb6-4f69-b389-425b8987329b");

        private ref readonly ICorDebugThreadVTable VTable => ref Unsafe.AsRef<ICorDebugThreadVTable>(_vtable);

        public ICorDebugThread(IntPtr punk)
            : base(new RefCountedFreeLibrary(IntPtr.Zero), IID_ICorDebugThread, punk)
        {
        }

        public HResult GetThreadId(out uint id)
        {
            uint thread = 0;
            HResult hr = VTable.GetID(Self, &thread);
            id = thread;
            return hr;
        }

        public HResult GetDebugState(out CorDebugThreadState threadState)
        {
            CorDebugThreadState state;
            HResult hr = VTable.GetDebugState(Self, &state);
            threadState = state;
            return hr;
        }

        public HResult SetDebugState(CorDebugThreadState threadState)
        {
            return VTable.SetDebugState(Self, threadState);
        }

        public HResult GetUserState(out CorDebugUserState userState)
        {
            CorDebugUserState state;
            HResult hr = VTable.GetUserState(Self, &state);
            userState = state;
            return hr;
        }

        public HResult GetRegisterSet(out ICorDebugRegisterSet registerSet)
        {
            registerSet = default;
            IntPtr set = IntPtr.Zero;
            HResult hr = VTable.GetRegisterSet(Self, &set);
            if (hr.IsOK)
            {
                registerSet = new ICorDebugRegisterSet(set);
            }
            return hr;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ICorDebugThreadVTable
        {
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetProcess;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint*, int> GetID;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetHandle;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetAppDomain;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, CorDebugThreadState, int> SetDebugState;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, CorDebugThreadState*, int> GetDebugState;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, CorDebugUserState*, int> GetUserState;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetCurrentException;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int> ClearCurrentException;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> CreateStepper;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumerateChains;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetActiveChain;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetActiveFrame;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetRegisterSet;
        }
    }
}
