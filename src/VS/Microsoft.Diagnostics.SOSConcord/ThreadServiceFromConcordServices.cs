// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.Diagnostics.SOSConcord
{
    /// <summary>
    /// Provides thread and register info and values
    /// </summary>
    internal sealed class ThreadServiceFromConcordServices : ThreadService
    {
        internal sealed class ThreadFromConcordServices : Thread
        {
            internal readonly DkmThread DkmThread;

            internal ThreadFromConcordServices(ThreadService threadService, int index, DkmThread thread)
                : base(threadService, index, (uint)thread.SystemPart.Id)
            {
                DkmThread = thread;
            }

            protected override bool GetThreadContextInner(uint contextFlags, byte[] context)
            {
                try
                {
                    DkmThread.GetContext(unchecked((int)contextFlags), context);
                    return true;
                }
                catch (DkmException ex)
                {
                    Trace.TraceError(ex.ToString());
                    return false;
                }
            }

            protected override ulong GetThreadTebInner()
            {
                try
                {
                    return DkmThread.TebAddress;
                }
                catch (DkmException ex)
                {
                    Trace.TraceError(ex.ToString());
                    return 0;
                }
            }
        }

        internal ThreadServiceFromConcordServices(IServiceProvider services)
            : base(services)
        {
        }

        protected override bool GetThreadContext(uint threadId, uint contextFlags, byte[] context) => throw new NotImplementedException();

        protected override ulong GetThreadTeb(uint threadId) => throw new NotImplementedException();

        protected override IEnumerable<IThread> GetThreadsInner()
        {
            int index = 0;
            foreach (DkmThread thread in ((TargetFromConcordServices)Target).DkmProcess.GetThreads())
            {
                yield return new ThreadFromConcordServices(this, index++, thread);
            }
        }
    }
}
