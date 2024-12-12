// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.ICorDebugServices.Interop
{
    public unsafe class ICorDebugRegisterSet : CallableCOMWrapper
    {
        private static readonly Guid IID_ICorDebugRegisterSet = new("CC7BCB0B-8A68-11d2-983C-0000F808342D");

        private ref readonly ICorDebugRegisterSetVTable VTable => ref Unsafe.AsRef<ICorDebugRegisterSetVTable>(_vtable);

        public ICorDebugRegisterSet(IntPtr punk)
            : base(new RefCountedFreeLibrary(IntPtr.Zero), IID_ICorDebugRegisterSet, punk)
        {
        }

        public HResult GetThreadContext(uint contextSize, byte[] context)
        {
            fixed (byte* contextPtr = context)
            {
                return VTable.GetThreadContext(Self, (int)contextSize, contextPtr);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ICorDebugRegisterSetVTable
        {
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int> GetRegistersAvailable;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int> GetRegisters;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int> SetRegisters;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int, byte*, int> GetThreadContext;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int, byte*, int> SetThreadContext;
        }
    }
}
