// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.DebugServices;
using Microsoft.VisualStudio.Debugger;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.SOSConcord
{
    /// <summary>
    /// Memory service implementation using the native debugger services
    /// </summary>
    internal class MemoryServiceFromConcordServices : IMemoryService
    {
        private readonly TargetFromConcordServices _target;

        /// <summary>
        /// Memory service constructor
        /// </summary>
        /// <param name="target">target instance</param>
        internal MemoryServiceFromConcordServices(TargetFromConcordServices target) 
        {
            Debug.Assert(target != null);
            _target = target;

            switch (target.Architecture)
            {
                case Architecture.X64:
                case Architecture.Arm64:
                    PointerSize = 8;
                    break;
                case Architecture.X86:
                case Architecture.Arm:
                    PointerSize = 4;
                    break;
            }
        }

        #region IMemoryService

        /// <summary>
        /// Returns the pointer size of the target
        /// </summary>
        public int PointerSize { get; }

        /// <summary>
        /// Read memory out of the target process.
        /// </summary>
        /// <param name="address">The address of memory to read</param>
        /// <param name="buffer">The buffer to read memory into</param>
        /// <param name="bytesRead">The number of bytes actually read out of the target process</param>
        /// <returns>true if any bytes were read at all, false if the read failed (and no bytes were read)</returns>
        public unsafe bool ReadMemory(ulong address, Span<byte> buffer, out int bytesRead)
        {
            try 
            {
                fixed (byte* bufferPtr = buffer)
                {
                    bytesRead = _target.DkmProcess.ReadMemory(address, DkmReadMemoryFlags.AllowPartialRead, bufferPtr, buffer.Length);
                }
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"DkmProcess.ReadMemory {address:X16} {buffer.Length:X8} FAILED {ex}");
            }
            bytesRead = 0;
            return false;
        }

        /// <summary>
        /// Write memory into target process for supported targets.
        /// </summary>
        /// <param name="address">The address of memory to write</param>
        /// <param name="buffer">The buffer to write</param>
        /// <param name="bytesWritten">The number of bytes successfully written</param>
        /// <returns>true if any bytes where written, false if write failed</returns>
        public bool WriteMemory(ulong address, Span<byte> buffer, out int bytesWritten)
        {
            try 
            {
                _target.DkmProcess.WriteMemory(address, buffer.ToArray());
                bytesWritten = buffer.Length;
                return true;
            }
            catch (Exception ex) 
            {
                Trace.TraceError($"DkmProcess.WriteMemory {address:X16} FAILED {ex}");
            }
            bytesWritten = 0;
            return false;
        }

        #endregion
    }
}
