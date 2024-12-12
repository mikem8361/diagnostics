// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.Diagnostics.ICorDebugServices.Interop;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;
using SOS;

namespace Microsoft.Diagnostics.ICorDebugServices
{
    public sealed class LiveTargetFactory(IHost host) : ILiveTargetFactory
    {
        private uint CORDBG_E_LIBRARY_PROVIDER_ERROR = 0x80131C43;
        private readonly IHost _host = host;

        public ITarget Attach(int processId)
        {
            Architecture architecture = RuntimeInformation.OSArchitecture;
            OSPlatform platform;
            string dbgShimName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                platform = OSPlatform.Windows;
                dbgShimName = "dbgshim.dll";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                platform = OSPlatform.Linux;
                dbgShimName = "libdbgshim.so";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                platform = OSPlatform.OSX;
                dbgShimName = "libdbgshim.dylib";
            }
            else
            {
                throw new NotSupportedException($"OS not supported {RuntimeInformation.OSDescription}");
            }

            try
            {
                Process.GetProcessById(processId);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                throw new DiagnosticsException($"Invalid process {processId}", ex);
            }

            string dbgShimPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), InstallHelper.GetRid());
            DbgShimAPI.Initialize(Path.Combine(dbgShimPath, dbgShimName));

            ICorDebugProcess corDebugProcess = RegisterForRuntimeStartup(processId, architecture);
            Debug.Assert(corDebugProcess is not null);
            try
            {
                return new TargetFromICorDebug(_host, platform, architecture, processId, corDebugProcess);
            }
            catch (Exception)
            {
                corDebugProcess.Dispose();
                throw;
            }
        }

        private ICorDebugProcess RegisterForRuntimeStartup(int processId, Architecture architecture)
        {
            ICorDebugProcess corDebugProcess = null;
            AutoResetEvent waitCallback = new(false);
            (IntPtr, GCHandle) unregister = (IntPtr.Zero, default);
            HResult hr = HResult.S_OK;
            HResult callbackResult = HResult.S_OK;
            ICorDebug corDebug = null;

            Trace.TraceInformation("RegisterForRuntimeStartup pid {0} START", processId);
            try
            {
                DbgShimAPI.RuntimeStartupCallbackDelegate callback = (ICorDebug cordbg, object parameter, HResult hr) => {
                    Trace.TraceInformation("RegisterForRuntimeStartup in callback pid {0} hr {1:X}", processId, hr);
                    corDebug = cordbg;
                    callbackResult = hr;
                    waitCallback.Set();
                };

                hr = DbgShimAPI.RegisterForRuntimeStartup(pid: processId, parameter: IntPtr.Zero, out unregister, callback);
                if (!hr.IsOK)
                {
                    Trace.TraceError($"DbgShimAPI.RegisterForRuntimeStartup FAILED {hr}");
                    throw new DiagnosticsException($"Invalid process {processId}");
                }

                Trace.TraceInformation("RegisterForRuntimeStartup waiting for callback");
                bool waitResult = waitCallback.WaitOne(15_000);
                Trace.TraceInformation($"RegisterForRuntimeStartup after callback {waitResult}");

                if (!waitResult)
                {
                    throw new DiagnosticsException($"Attach to process {processId} FAILED");
                }

                if (!callbackResult.IsOK)
                {
                    Trace.TraceError($"DbgShim.RegisterForRuntimeStartup callback FAILED {callbackResult}");
                    if (callbackResult == CORDBG_E_LIBRARY_PROVIDER_ERROR)
                    {
                        throw new DiagnosticsException($"Attach to process {processId} FAILED - could not download DAC or DBI module");
                    }
                    throw new DiagnosticsException($"Attach to process {processId} FAILED");
                }

                hr = corDebug.Initialize();
                if (!hr.IsOK)
                {
                    throw new DiagnosticsException($"ICorDebug.Initialize FAILED {hr}");
                }

                // Required even though we don't actually use any of the callbacks
                using ManagedCallbackWrapper managedCallback = new();
                hr = corDebug.SetManagedHandler(managedCallback.ICorDebugManagedCallback);
                if (!hr.IsOK)
                {
                    throw new DiagnosticsException($"ICorDebug.SetManagedHandler FAILED {hr}");
                }

                Trace.TraceInformation("RegisterForRuntimeStartup before DebugActiveProcess");
                hr = corDebug.DebugActiveProcess(processId, out IntPtr process);
                Trace.TraceInformation($"RegisterForRuntimeStartup after DebugActiveProcess {hr}");
                if (!hr.IsOK)
                {
                    throw new DiagnosticsException($"ICorDebug.DebugActiveProcess FAILED {hr}");
                }

                corDebugProcess = new(architecture, corDebug, process);
                hr = corDebugProcess.Stop();
                if (!hr.IsOK)
                {
                    throw new DiagnosticsException($"ICorDebugProcess.Stop FAILED {hr}");
                }
                corDebugProcess.AddRef();
            }
            catch (Exception)
            {
                corDebugProcess?.Detach();
                corDebugProcess?.Dispose();
                throw;
            }
            finally
            {
                if (unregister.Item1 != IntPtr.Zero)
                {
                    DbgShimAPI.UnregisterForRuntimeStartup(unregister);
                }
                Trace.TraceInformation("RegisterForRuntimeStartup pid {0} DONE", processId);
            }
            return corDebugProcess;
        }
    }
}
