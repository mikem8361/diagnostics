// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace SOS.Extensions
{
    public sealed unsafe class DebuggerOutputService : CallableCOMWrapper
    {
        private static Guid IID_IOutputService = new("30745290-DC07-4993-9B61-F6132CCEB663");

        private ref readonly IOutputServiceVTable VTable => ref Unsafe.AsRef<IOutputServiceVTable>(_vtable);

        public DebuggerOutputService(IntPtr punk)
            : base(new RefCountedFreeLibrary(IntPtr.Zero), IID_IOutputService, punk)
        {
        }

        public int GetOutputWidth() => (int)VTable.GetOutputWidth(Self);

        public bool SupportsDml => VTable.SupportsDml(Self) != 0;

        public void OutputString(OutputType type, string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            byte[] messageBytes = Encoding.ASCII.GetBytes(message + "\0");
            fixed (byte* messagePtr = messageBytes)
            {
                VTable.OutputString(Self, type, messagePtr);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct IOutputServiceVTable
        {
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint> GetOutputWidth;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint> SupportsDml;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, OutputType, byte*, void> OutputString;
        }
    }
}
