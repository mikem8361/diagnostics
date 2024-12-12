// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.ICorDebugServices.Interop
{
    public unsafe class ICorDebugProcess : CallableCOMWrapper, IMemoryService
    {
        public static readonly Guid IID_ICorDebugProcess = new("3d6f5f64-7538-11d3-8d5b-00104b35e7ef");

        private ref readonly ICorDebugProcessVTable VTable => ref Unsafe.AsRef<ICorDebugProcessVTable>(_vtable);

        private ICorDebug _corDebug;

        public ICorDebugProcess(Architecture architecture, ICorDebug corDebug, IntPtr punk)
            : base(new RefCountedFreeLibrary(IntPtr.Zero), IID_ICorDebugProcess, punk)
        {
            _corDebug = corDebug;
            PointerSize = Utilities.GetPointerSizeFromArchitecture(architecture);
        }

        protected override void Dispose(bool disposing)
        {
            _corDebug?.Dispose();
            _corDebug = null;
            base.Dispose(disposing);
        }

        #region ICorDebugController

        public HResult Stop() => VTable.Stop(Self, uint.MaxValue);

        public HResult Continue(bool isOutOfBand) => VTable.Continue(Self, isOutOfBand ? 1 : 0);

        public HResult IsRunning(out bool isRunning)
        {
            int result;
            HResult hr = VTable.IsRunning(Self, &result);
            isRunning = result != 0;
            return hr;
        }

        public HResult EnumerateThreads(out ICorDebugEnum<ICorDebugThread> threads)
        {
            threads = default;
            IntPtr threadEnum;
            HResult hr = VTable.EnumerateThreads(Self, &threadEnum);
            if (hr)
            {
                threads = new ICorDebugEnum<ICorDebugThread>(threadEnum, (IntPtr thread) => new ICorDebugThread(thread));
            }
            return hr;
        }

        public HResult Detach() => VTable.Detach(Self);

        public HResult Terminate(uint exitCode) => VTable.Terminate(Self, exitCode);

        #endregion

        public HResult GetProcessId(out int id)
        {
            int process = 0;
            HResult hr = VTable.GetID(Self, &process);
            id = process;
            return hr;
        }

        public HResult GetThreadContext(uint threadId, uint contextSize, byte[] context)
        {
            fixed (byte* contextPtr = context)
            {
                return VTable.GetThreadContext(Self, threadId, (int)contextSize, contextPtr);
            }
        }

        #region IMemoryService

        public int PointerSize { get; }

        public bool ReadMemory(ulong address, Span<byte> buffer, out int bytesRead)
        {
            fixed (byte* bufferPtr = buffer)
            {
                int read = 0;
                VTable.ReadMemory(Self, address, buffer.Length, bufferPtr, &read);
                bytesRead = read;
                return bytesRead > 0;
            }
        }

        public bool WriteMemory(ulong address, Span<byte> buffer, out int bytesWritten)
        {
            fixed (byte* bufferPtr = buffer)
            {
                int written = 0;
                VTable.WriteMemory(Self, address, buffer.Length, bufferPtr, &written);
                bytesWritten = written;
                return bytesWritten > 0;
            }
        }

        #endregion

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ICorDebugProcessVTable
        {
            // ICorDebugController
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, int> Stop;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int, int> Continue;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int*, int> IsRunning;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int*, IntPtr, int> HasQueuedCallbacks;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumerateThreads;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, CorDebugThreadState, IntPtr, int> SetAllThreadsDebugState;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int> Detach;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, int> Terminate;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int> CanCommitChanges;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int> CommitChanges;
            // ICorDebugProcess
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int*, int> GetID;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetHandle;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int> GetThread;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumerateObjects;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong, int*, int> IsTransitionStub;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, int*, int> IsOSSuspended;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, int, byte*, int> GetThreadContext;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, int, byte*, int> SetThreadContext;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong, int, byte*, int*, int> ReadMemory;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong, int, byte*, int*, int> WriteMemory;
        }
    }
}
