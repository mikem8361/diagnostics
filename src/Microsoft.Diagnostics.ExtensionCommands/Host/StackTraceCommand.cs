// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.DebugServices;

namespace Microsoft.Diagnostics.ExtensionCommands
{
    [Command(Name = "stacktrace", Aliases = new string[] { "k" }, Help = "Unwind the current thread's stack.")]
    public unsafe class StackTraceCommand : CommandBase
    {
        [ServiceImport]
        public IModuleService ModuleService { get; set; }

        [ServiceImport]
        public IThreadService ThreadService { get; set; }

        [ServiceImport(Optional = true)]
        public StackTraceService StackTraceService { get; set; }

        [ServiceImport]
        public IThread CurrentThread { get; set; }

        public override void Invoke()
        {
            if (StackTraceService is null)
            {
                throw new DiagnosticsException($"Stack trace service not supported");
            }
            IThread thread = CurrentThread ?? throw new DiagnosticsException($"No current thread");
            Span<byte> context = thread.GetThreadContext().ToArray().AsSpan();
            ulong previousSp = 0;
            ulong previousIp = 0;

            WriteLine("SP               FP               IP");
            while (true)
            {
                if (!ThreadService.TryGetRegisterValue(context, ThreadService.InstructionPointerIndex, out ulong ip))
                {
                    throw new DiagnosticsException($"Can get instruction pointer from thread {thread.ThreadId}");
                }
                if (!ThreadService.TryGetRegisterValue(context, ThreadService.StackPointerIndex, out ulong sp))
                {
                    throw new DiagnosticsException($"Can get stack pointer from thread {thread.ThreadId}");
                }
                if (!ThreadService.TryGetRegisterValue(context, ThreadService.FramePointerIndex, out ulong fp))
                {
                    throw new DiagnosticsException($"Can get frame pointer from thread {thread.ThreadId}");
                }
                Write($"{sp:X16} {fp:X16} {ip:X16}");

                if (thread.Target.Architecture == Architecture.Arm64)
                {
                    // ARM64 can have frames with the same SP but different IPs. Increment sp so it gets added to the stack
                    // frames in the correct order and to prevent the below loop termination on non-increasing sp.
                    if (sp == previousSp && ip != previousIp)
                    {
                        sp++;
                    }
                }

                if (ip == 0 || sp <= previousSp)
                {
                    Trace.TraceError($"\nip == 0 or sp ({sp:X16}) not increasing");
                    break;
                }

                IModule module = ModuleService.GetModuleFromAddress(ip);
                if (module == null)
                {
                    Trace.TraceError($"\nCan find module for instruction pointer {ip:X16}");
                    break;
                }

                IModuleSymbols moduleSymbols = module.Services.GetService<IModuleSymbols>();
                if (moduleSymbols != null)
                {
                    if (moduleSymbols.TryGetSymbolName(ip, out string symbol, out ulong displacement))
                    {
                        Write($" {Path.GetFileName(module.FileName)}!{symbol} + {displacement}");
                    }
                }

                WriteLine(string.Empty);

                if (!StackTraceService.TryUnwind(context))
                {
                    Trace.TraceError("TryUnwind FAILED");
                    break;
                }

                previousSp = sp;
                previousIp = ip;
            }
        }
    }
}
