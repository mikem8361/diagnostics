// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.ICorDebugServices.Interop
{
    public unsafe class ICorDebugEnum<T> : CallableCOMWrapper, IEnumerable<T>
        where T : class
    {
        private static readonly Guid IID_ICorDebugEnum = new("CC7BCB01-8A68-11d2-983C-0000F808342D");

        private ref readonly ICorDebugEnumVTable VTable => ref Unsafe.AsRef<ICorDebugEnumVTable>(_vtable);

        private readonly Func<IntPtr, T> _factory;

        public ICorDebugEnum(IntPtr punk, Func<IntPtr, T> factory)
            : base(new RefCountedFreeLibrary(IntPtr.Zero), IID_ICorDebugEnum, punk)
        {
            _factory = factory;
        }

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

        private sealed class Enumerator : IEnumerator<T>
        {
            private readonly ICorDebugEnum<T> _debugEnum;
            private T _current;

            internal Enumerator(ICorDebugEnum<T> debugEnum)
            {
                _debugEnum = debugEnum;
                Reset();
            }

            object IEnumerator.Current { get { return ((IEnumerator<T>)this).Current; } }

            T IEnumerator<T>.Current
            {
                get { return _current; }
            }

            public bool MoveNext()
            {
                IntPtr current = IntPtr.Zero;
                int count = 0;
                HResult hr = _debugEnum.VTable.Next(_debugEnum.Self, 1, &current, &count);
                if (hr.IsOK)
                {
                    _current = _debugEnum._factory(current);
                    return true;
                }
                return false;
            }

            public void Reset()
            {
                _debugEnum.VTable.Reset(_debugEnum.Self);
            }

            public void Dispose()
            {
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ICorDebugEnumVTable
        {
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int, int> Skip;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int> Reset;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> Clone;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int*, int> GetCount;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr*, int*, int> Next;
        }
    }
}
