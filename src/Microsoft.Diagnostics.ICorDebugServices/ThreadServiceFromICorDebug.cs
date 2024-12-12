// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.Diagnostics.ICorDebugServices.Interop;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.ICorDebugServices
{
    /// <summary>
    /// Provides thread and register info and values
    /// </summary>
    internal sealed class ThreadServiceFromICorDebug : ThreadService
    {
        private sealed class ThreadFromICorDebug : Thread, IDisposable
        {
            private readonly ICorDebugThread _thread;

            public ThreadFromICorDebug(ThreadService threadService, int index, uint id, ICorDebugThread thread)
                : base(threadService, index, id)
            {
                _thread = thread;
                _serviceContainer.AddService<ICorDebugThread>(thread);
            }

            protected override bool GetThreadContextInner(uint contextFlags, byte[] context)
            {
                SetContextFlags(contextFlags, context);
                HResult hr = _thread.GetRegisterSet(out ICorDebugRegisterSet set);
                if (hr.IsOK)
                {
                    hr = set.GetThreadContext((uint)context.Length, context);
                    set.Dispose();
                }
                return hr;
            }

            protected override ulong GetThreadTebInner() => 0;

            void IDisposable.Dispose()
            {
                _thread.Dispose();
                base.Dispose();
            }
        }

        private readonly ICorDebugProcess _process;

        internal ThreadServiceFromICorDebug(IServiceProvider services, ICorDebugProcess process)
            : base(services)
        {
            _process = process;
        }

        protected override IEnumerable<IThread> GetThreadsInner()
        {
            List<IThread> result = new();
            if (_process.EnumerateThreads(out ICorDebugEnum<ICorDebugThread> threads).IsOK)
            {
                int index = 0;
                foreach (ICorDebugThread thread in threads)
                {
                    if (thread.GetThreadId(out uint id).IsOK)
                    {
                        thread.GetDebugState(out CorDebugThreadState threadState);
                        thread.GetUserState(out CorDebugUserState userState);
                        Trace.TraceInformation($"Thread {index} {id:X4} {threadState} {userState}");

                        result.Add(new ThreadFromICorDebug(this, index, id, thread));
                        index++;
                    }
                }
                threads.Dispose();
            }
            return result;
        }
    }
}
