// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.ExtensionCommands
{
    public class StackTraceService : IDisposable
    {
        [ServiceImport]
        public IMemoryService MemoryService { get; set; }

        [ServiceImport]
        public IModuleService ModuleService { get; set; }

        [ServiceImport]
        public IThreadService ThreadService { get; set; }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int DllMainDelegate(
            IntPtr instance,
            int reason,
            IntPtr reserved);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private unsafe delegate int PAL_VirtualUnwindOutOfProcDelegate(
            byte* context,
            byte* contextPointers,
            ulong* functionStart,
            UIntPtr baseAddress,
            IntPtr readMemoryCallback);

        // Native: typedef BOOL(*UnwindReadMemoryCallback)(PVOID address, PVOID buffer, SIZE_T size);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int UnwindReadMemoryCallbackDelegate(
            UIntPtr address,
            IntPtr buffer,
            UIntPtr size);

        private static readonly IntPtr _readMemoryCallback = Marshal.GetFunctionPointerForDelegate<UnwindReadMemoryCallbackDelegate>(UnwindReadMemory);
        private static IMemoryService _memoryService;
        private IntPtr _dacHandle = IntPtr.Zero;
        private PAL_VirtualUnwindOutOfProcDelegate _virtualUnwindOutOfProc;

        [ServiceExport(Scope = ServiceScope.Target)]
        public static StackTraceService Create(IRuntimeService runtimeService)
        {
            StackTraceService stackTraceService = new();
            foreach (IRuntime runtime in runtimeService.EnumerateRuntimes())
            {
                if (stackTraceService.TryInitialize(runtime))
                {
                    return stackTraceService;
                }
            }
            Trace.TraceError("StackTraceService: runtime, DAC or virtual unwind PAL function not found");
            return null;
        }

        public void Dispose()
        {
            if (_dacHandle != IntPtr.Zero)
            {
                DataTarget.PlatformFunctions.FreeLibrary(_dacHandle);
                _dacHandle = IntPtr.Zero;
            }
            _virtualUnwindOutOfProc = null;
        }

        public unsafe bool TryUnwind([In][Out]Span<byte> context)
        {
            _memoryService = MemoryService;
            try
            {
                if (_dacHandle == IntPtr.Zero || _virtualUnwindOutOfProc == null)
                {
                    return false;
                }
                if (!ThreadService.TryGetRegisterValue(context, ThreadService.InstructionPointerIndex, out ulong ip))
                {
                    Trace.TraceError("Can not get instruction pointer from context");
                    return false;
                }
                IModule module = ModuleService.GetModuleFromAddress(ip);
                if (module == null)
                {
                    Trace.TraceError($"Can not find module for instruction pointer {ip:X16}");
                    return false;
                }
                fixed (byte* contextPtr = context)
                {
                    int result = _virtualUnwindOutOfProc(contextPtr, null, null, new UIntPtr(module.ImageBase), _readMemoryCallback);
                    if (result == 0)
                    {
                        Trace.TraceError("PAL_VirtualUnwindOutOfProc FAILED");
                        return false;
                    }
                }
            }
            finally
            {
                _memoryService = null;
            }
            return true;
        }

        private bool TryInitialize(IRuntime runtime)
        {
            Debug.Assert(_dacHandle == IntPtr.Zero);
            Debug.Assert(_virtualUnwindOutOfProc is null);

            if (runtime.RuntimeType != RuntimeType.NetCore && runtime.RuntimeType != RuntimeType.SingleFile)
            {
                return false;
            }
            string dacFilePath = runtime.GetDacFilePath();
            if (dacFilePath is not null)
            {
                try
                {
                    _dacHandle = DataTarget.PlatformFunctions.LoadLibrary(dacFilePath);
                }
                catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
                {
                    Trace.TraceError($"LoadLibrary({dacFilePath}) FAILED {ex}");
                    return false;
                }
                Debug.Assert(_dacHandle != IntPtr.Zero);

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    DllMainDelegate dllMain = GetDelegateFunction<DllMainDelegate>(_dacHandle, "DllMain");
                    dllMain?.Invoke(_dacHandle, 1, IntPtr.Zero);
                }
                string functionName = (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "DAC_" : string.Empty) + "PAL_VirtualUnwindOutOfProc";
                _virtualUnwindOutOfProc = GetDelegateFunction<PAL_VirtualUnwindOutOfProcDelegate>(_dacHandle, functionName);
                if (_virtualUnwindOutOfProc == null)
                {
                    Trace.TraceError($"Failed to obtain DAC {functionName}");
                    DataTarget.PlatformFunctions.FreeLibrary(_dacHandle);
                    _dacHandle = IntPtr.Zero;
                    return false;
                }
            }
            else
            {
                Trace.TraceError($"Could not find matching DAC for this runtime: {runtime.RuntimeModule.FileName}");
                return false;
            }
            return true;
        }

        private static T GetDelegateFunction<T>(IntPtr library, string functionName)
            where T : Delegate
        {
            IntPtr functionAddress = DataTarget.PlatformFunctions.GetLibraryExport(library, functionName);
            if (functionAddress == IntPtr.Zero)
            {
                return default;
            }
            return (T)Marshal.GetDelegateForFunctionPointer(functionAddress, typeof(T));
        }

        private static unsafe int UnwindReadMemory(UIntPtr address, IntPtr buffer, UIntPtr size)
        {
            if (_memoryService is null)
            {
                return HResult.E_FAIL;
            }
            Span<byte> data = new(buffer.ToPointer(), (int)size.ToUInt32());
            return _memoryService.ReadMemory(address.ToUInt64(), data, out _) ? 1 : 0;
        }
    }
}
