// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.TestHelpers;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Diagnostics
{
    public class StartInfo : IDisposable
    {
        public readonly ITestOutputHelper Output;
        public readonly TestConfiguration TestConfiguration;
        public readonly bool Launch;

        public int ProcessId { get; private set; }
        public IntPtr ResumeHandle { get; set; }
        public string PipeName { get; private set; }

        private readonly AutoResetEvent _createProcessEvent = new AutoResetEvent(false);
        private HResult _createProcessResult = HResult.E_FAIL;
        private NamedPipeServerStream _pipeServer;
        private Process _process;

        public StartInfo(ITestOutputHelper output, TestConfiguration config, bool launch)
        {
            Output = output;
            TestConfiguration = config;
            Launch = launch;
        }

        public void SetProcessId(int processId)
        {
            ProcessId = processId;
            try
            {
                _process = Process.GetProcessById(processId);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
            }
        }

        public void SetCreateProcessResult(HResult hr)
        {
            _createProcessResult = hr;
            _createProcessEvent.Set();
        }

        public HResult WaitForCreateProcess()
        {
            Assert.True(_createProcessEvent.WaitOne(TimeSpan.FromMinutes(5)));
            return _createProcessResult;
        }

        public void CreatePipeConnection()
        {
            PipeName = Guid.NewGuid().ToString();
            _pipeServer = new NamedPipeServerStream(PipeName);
        }

        public async Task<bool> WaitForDebuggee()
        {
            if (_pipeServer != null)
            {
                if (_process is null)
                {
                    return false;
                }
                try
                {
                    Task processExit = Task.Factory.StartNew(() => _process.WaitForExit(), TaskCreationOptions.LongRunning);
                    var source = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                    await Task.WhenAny(_pipeServer.WaitForConnectionAsync(source.Token), processExit);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
            return true;
        }

        public void Dispose()
        {
            Trace.TraceInformation("StartInfo: disposing process {0}", ProcessId);
            _pipeServer?.Dispose();
            _pipeServer = null;
            if (ResumeHandle != IntPtr.Zero)
            {
                DbgShimAPI.ResumeProcess(ResumeHandle);
                DbgShimAPI.CloseResumeHandle(ResumeHandle);
                ResumeHandle = IntPtr.Zero;
            }
            try
            {
                _process?.Kill();
                _process = null;
            }
            catch (Exception ex) when (ex is NotSupportedException || ex is InvalidOperationException)
            {
                Trace.TraceError(ex.ToString());
            }
        }
    }
}