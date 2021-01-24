using Microsoft.Diagnostics.Repl;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.TestHelpers;
using SOS.Extensions;
using SOS.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.DebugServices.UnitTests
{
    public class TestDbgEng : TestHost
    {
        private static DbgEngController _controller;

        public TestDbgEng(TestConfiguration config)
            : base (config)
        {
        }

        protected override ITarget GetTarget()
        {
            HResult hr = Controller.Client.OpenDumpFile(DumpFile);
            if (hr != HResult.S_OK) {
                throw new DiagnosticsException($"OpenDumpFile({DumpFile} FAILED {hr:X8}");
            }
            Controller.ProcessEvents();

            string directory = Path.GetDirectoryName(DumpFile);
            string sympath = $"{directory};cache*;SRV*https://msdl.microsoft.com/download/symbols";
            hr = Controller.Symbols.SetSymbolPath(sympath);
            if (hr != HResult.S_OK) {
                Trace.TraceError($"SetSymbolPath({sympath}) FAILED {hr:X8}");
            }
            var symbolService = Host.Services.GetService<ISymbolService>();
            Trace.TraceInformation($"SymbolService: {symbolService}");

            return Host.CurrentTarget;
        }

        private DbgEngController Controller
        {
            get
            {
                _controller ??= new DbgEngController(DbgEngPath, SOSPath);
                return _controller;
            }
        }

        private static IHost Host => HostServices.Instance;

        private string DbgEngPath => TestConfiguration.MakeCanonicalPath(Config.AllSettings["DbgEngPath"]);

        private string SOSPath =>TestConfiguration.MakeCanonicalPath(Config.AllSettings["SOSPath"]);

        public override string ToString() => "DbgEng: " + DumpFile;

        class DbgEngController : IDebugOutputCallbacks
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            private delegate HResult DebugCreateDelegate(
                ref Guid interfaceId,
                [MarshalAs(UnmanagedType.IUnknown)] out object iinterface);

            private static readonly Guid _iidClient = new Guid("e3acb9d7-7ec2-4f0c-a0da-e81e0cbbe628");
            private readonly CharToLineConverter _converter;

            internal readonly IDebugClient Client;
            internal readonly IDebugControl Control;
            internal readonly IDebugSymbols2 Symbols;

            internal DbgEngController(string dbgengPath, string sosPath)
            {
                Trace.TraceInformation($"DbgEngController: {dbgengPath} {sosPath}");
                _converter = new CharToLineConverter((text) => {
                    Trace.TraceInformation(text);
                });
                IntPtr dbgengLibrary = DataTarget.PlatformFunctions.LoadLibrary(dbgengPath);
                var debugCreate = SOSHost.GetDelegateFunction<DebugCreateDelegate>(dbgengLibrary, "DebugCreate");
                if (debugCreate == null) {
                    throw new DiagnosticsException($"DebugCreate export not found");
                }
                Guid iid = _iidClient;
                HResult hr = debugCreate(ref iid, out object client);
                if (hr != HResult.S_OK) {
                    throw new DiagnosticsException($"DebugCreate FAILED {hr:X8}");
                }
                Client = (IDebugClient)client;
                Control = (IDebugControl)client;
                Symbols = (IDebugSymbols2)client;

                hr = Client.SetOutputCallbacks(this);
                if (hr != HResult.S_OK) {
                    throw new DiagnosticsException($"SetOutputCallbacks FAILED {hr:X8}");
                }

                // Load the sos extensions
                hr = Control.Execute(DEBUG_OUTCTL.ALL_CLIENTS, $".load {sosPath}", DEBUG_EXECUTE.DEFAULT);
                if (hr != HResult.S_OK) {
                    throw new DiagnosticsException($"Loading {sosPath} FAILED {hr:X8}");
                }

                // Initialize the extension host
                hr = HostServices.Initialize(sosPath);
                if (hr != HResult.S_OK) {
                    throw new DiagnosticsException($"HostServices.Initialize({sosPath}) FAILED {hr:X8}");
                }

                // Automatically enable symbol server support
                var symbolService = Host.Services.GetService<ISymbolService>();
                symbolService.AddSymbolServer(msdl: true, symweb: false, symbolServerPath: null, authToken: null, timeoutInMinutes: 0);
                symbolService.AddCachePath(symbolService.DefaultSymbolCache);
            }

            /// <summary>
            /// Wait for dbgeng events
            /// </summary>
            internal void ProcessEvents()
            {
                while (true) {
                    // Wait until the target stops
                    HResult hr = Control.WaitForEvent(DEBUG_WAIT.DEFAULT, uint.MaxValue);
                    if (hr == HResult.S_OK) {
                        Trace.TraceInformation("ProcessEvents.WaitForEvent returned status {0}", ExecutionStatus);
                        if (!IsTargetRunning()) {
                            Trace.TraceInformation("ProcessEvents target stopped");
                            break;
                        }
                    }
                    else {
                        Trace.TraceError("ProcessEvents.WaitForEvent FAILED {0:X8}", hr);
                        break;
                    }
                }
            }

            /// <summary>
            /// Returns true if the target is running code
            /// </summary>
            private bool IsTargetRunning()
            {
                switch (ExecutionStatus) {
                    case DEBUG_STATUS.GO:
                    case DEBUG_STATUS.GO_HANDLED:
                    case DEBUG_STATUS.GO_NOT_HANDLED:
                    case DEBUG_STATUS.STEP_OVER:
                    case DEBUG_STATUS.STEP_INTO:
                    case DEBUG_STATUS.STEP_BRANCH:
                        return true;
                }
                return false;
            }

            private DEBUG_STATUS ExecutionStatus
            {
                get {
                    HResult hr = Control.GetExecutionStatus(out DEBUG_STATUS status);
                    if (hr != HResult.S_OK) {
                        throw new DiagnosticsException($"GetExecutionStatus FAILED {hr:X8}");
                    }
                    return status;
                }
                set {
                    HResult hr = Control.SetExecutionStatus(value);
                    if (hr != HResult.S_OK) {
                        throw new DiagnosticsException($"SetExecutionStatus FAILED {hr:X8}");
                    }
                }
            }

            #region IDebugOutputCallbacks

            int IDebugOutputCallbacks.Output(DEBUG_OUTPUT mask, string text)
            {
                try
                {
                    _converter.Input(text);
                }
                catch (Exception)
                {
                }
                return 0;
            }

            #endregion
        }
    }
}
