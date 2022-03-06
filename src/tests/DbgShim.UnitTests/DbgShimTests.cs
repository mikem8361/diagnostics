// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.TestHelpers;
using SOS.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions;

namespace Microsoft.Diagnostics
{
    public class DbgShimTests : IDisposable
    {
        private const string ListenerName = "DbgShimTests";

        public static IEnumerable<object[]> GetConfigurations(string key, string value)
        {
            return TestRunConfiguration.Instance.Configurations.Where((c) => key == null || c.AllSettings.GetValueOrDefault(key) == value).Select(c => new[] { c });
        }

        public static IEnumerable<object[]> Configurations => GetConfigurations("TestName", null);

        private ITestOutputHelper Output { get; }

        public DbgShimTests(ITestOutputHelper output)
        {
            Output = output;
            LoggingListener.EnableListener(output, ListenerName);
        }

        void IDisposable.Dispose() => Trace.Listeners.Remove(ListenerName);

        /// <summary>
        /// Test RegisterForRuntimeStartup for launch
        /// </summary>
        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task Launch1(TestConfiguration config)
        {
            await RemoteInvoke(config, static async (string xml) =>
            {
                using StartInfo startInfo = await StartDebuggee(xml, launch: true);
                TestRegisterForRuntimeStartup(startInfo, 1);

                // Once the debuggee is resumed now wait until it starts
                Assert.True(await startInfo.WaitForDebuggee());
            });
        }

        /// <summary>
        /// Test RegisterForRuntimeStartupEx for launch
        /// </summary>
        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task Launch2(TestConfiguration config)
        {
            await RemoteInvoke(config, static async (string xml) =>
            {
                using StartInfo startInfo = await StartDebuggee(xml, launch: true);
                TestRegisterForRuntimeStartup(startInfo, 2);

                // Once the debuggee is resumed now wait until it starts
                Assert.True(await startInfo.WaitForDebuggee());
            });
        }

        /// <summary>
        /// Test RegisterForRuntimeStartup3 for launch
        /// </summary>
        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task Launch3(TestConfiguration config)
        {
            DbgShimAPI.Initialize(config.DbgShimPath());
            if (!DbgShimAPI.IsRegisterForRuntimeStartup3Supported)
            {
                throw new SkipTestException("IsRegisterForRuntimeStartup3 not supported");
            }
            await RemoteInvoke(config, static async (string xml) => 
            {
                using StartInfo startInfo = await StartDebuggee(xml, launch: true);
                TestRegisterForRuntimeStartup(startInfo, 3);

                // Once the debuggee is resumed now wait until it starts
                Assert.True(await startInfo.WaitForDebuggee());
            });
        }

        /// <summary>
        /// Test RegisterForRuntimeStartup for attach 
        /// </summary>
        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task Attach1(TestConfiguration config)
        {
            await RemoteInvoke(config, static async (string xml) => 
            {
                using StartInfo startInfo = await StartDebuggee(xml, launch: false);
                TestRegisterForRuntimeStartup(startInfo, 1);
            });
        }

        /// <summary>
        /// Test RegisterForRuntimeStartupEx for attach 
        /// </summary>
        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task Attach2(TestConfiguration config)
        {
            await RemoteInvoke(config, static async (string xml) => 
            {
                using StartInfo startInfo = await StartDebuggee(xml, launch: false);
                TestRegisterForRuntimeStartup(startInfo, 2);
            });
        }

        /// <summary>
        /// Test RegisterForRuntimeStartup3 for attach
        /// </summary>
        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task Attach3(TestConfiguration config)
        {
            DbgShimAPI.Initialize(config.DbgShimPath());
            if (!DbgShimAPI.IsRegisterForRuntimeStartup3Supported)
            {
                throw new SkipTestException("IsRegisterForRuntimeStartup3 not supported");
            }
            await RemoteInvoke(config, static async (string xml) => 
            {
                using StartInfo startInfo = await StartDebuggee(xml, launch: false);
                TestRegisterForRuntimeStartup(startInfo, 3);
            });
        }

        /// <summary>
        /// Test EnumerateCLRs/CloseCLREnumeration
        /// </summary>
        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task EnumerateCLRs(TestConfiguration config)
        {
            await RemoteInvoke(config, static async (string xml) =>
            {
                using StartInfo startInfo = await StartDebuggee(xml, launch: false);
                Trace.TraceInformation("EnumerateCLRs pid {0} START", startInfo.ProcessId);
                HResult hr = DbgShimAPI.EnumerateCLRs(startInfo.ProcessId, (IntPtr[] continueEventHandles, string[] moduleNames) =>
                {
                    Assert.True(continueEventHandles.Length == 1);
                    Assert.True(moduleNames.Length == 1);
                    for (int i = 0; i < continueEventHandles.Length; i++)
                    {
                        Trace.TraceInformation("EnumerateCLRs pid {0} {1:X16} {2}", startInfo.ProcessId, continueEventHandles[i].ToInt64(), moduleNames[i]);
                        AssertX.FileExists("ModuleFilePath", moduleNames[i], startInfo.Output);
                    }
                });
                AssertResult(hr);
                Trace.TraceInformation("EnumerateCLRs pid {0} DONE", startInfo.ProcessId);
            });
        }

        /// <summary>
        /// Test CreateVersionStringFromModule/CreateDebuggingInterfaceFromVersion
        /// </summary>
        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task CreateDebuggingInterfaceFromVersion(TestConfiguration config)
        {
            await RemoteInvoke(config, static async (string xml) =>
            {
                using StartInfo startInfo = await StartDebuggee(xml, launch: false);
                TestCreateDebuggingInterface(startInfo, 0);
            });
        }

        /// <summary>
        /// Test CreateVersionStringFromModule/CreateDebuggingInterfaceFromVersionEx
        /// </summary>
        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task CreateDebuggingInterfaceFromVersionEx(TestConfiguration config)
        {
            await RemoteInvoke(config, static async (string xml) =>
            {
                using StartInfo startInfo = await StartDebuggee(xml, launch: false);
                TestCreateDebuggingInterface(startInfo, 1);
            });
        }

        /// <summary>
        /// Test CreateVersionStringFromModule/CreateDebuggingInterfaceFromVersion2
        /// </summary>
        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task CreateDebuggingInterfaceFromVersion2(TestConfiguration config)
        {
            await RemoteInvoke(config, static async (string xml) =>
            {
                using StartInfo startInfo = await StartDebuggee(xml, launch: false);
                TestCreateDebuggingInterface(startInfo, 2);
            });
        }

        /// <summary>
        /// Test CreateVersionStringFromModule/CreateDebuggingInterfaceFromVersion3
        /// </summary>
        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task CreateDebuggingInterfaceFromVersion3(TestConfiguration config)
        {
            DbgShimAPI.Initialize(config.DbgShimPath());
            if (!DbgShimAPI.IsCreateDebuggingInterfaceFromVersion3Supported)
            {
                throw new SkipTestException("CreateDebuggingInterfaceFromVersion3 not supported");
            }
            await RemoteInvoke(config, static async (string xml) =>
            {
                using StartInfo startInfo = await StartDebuggee(xml, launch: false);
                TestCreateDebuggingInterface(startInfo, 3);
            });
        }

        [SkippableTheory, MemberData(nameof(GetConfigurations), "TestName", "OpenVirtualProcess")]
        public async Task OpenVirtualProcess(TestConfiguration config)
        {
            if (!config.AllSettings.ContainsKey("DumpFile"))
            {
                throw new SkipTestException("OpenVirtualProcessTest: No dump file");
            }
            await RemoteInvoke(config, static (string xml) =>
            {
                AfterInvoke(xml, out TestConfiguration cfg, out ITestOutputHelper output);

                DbgShimAPI.Initialize(cfg.DbgShimPath());
                AssertResult(DbgShimAPI.CLRCreateInstance(out ICLRDebugging clrDebugging));
                Assert.NotNull(clrDebugging);

                TestDump testDump = new(cfg);
                ITarget target = testDump.Target;
                IRuntimeService runtimeService = target.Services.GetService<IRuntimeService>();
                IRuntime runtime = runtimeService.EnumerateRuntimes().Single();

                CorDebugDataTargetWrapper dataTarget = new(target.Services);
                LibraryProviderWrapper libraryProvider = new(target.OperatingSystem, runtime.GetDbiFilePath(), runtime.GetDacFilePath());
                ClrDebuggingVersion maxDebuggerSupportedVersion = new()
                {
                    StructVersion = 0,
                    Major = 4,
                    Minor = 0,
                    Build = 0,
                    Revision = 0,
                };
                HResult hr = clrDebugging.OpenVirtualProcess(
                    runtime.RuntimeModule.ImageBase,
                    dataTarget.ICorDebugDataTarget,
                    libraryProvider.ILibraryProvider,
                    maxDebuggerSupportedVersion,
                    in RuntimeWrapper.IID_ICorDebugProcess,
                    out IntPtr corDebugProcess,
                    out ClrDebuggingVersion version,
                    out ClrDebuggingProcessFlags flags);

                AssertResult(hr);
                Assert.NotEqual(IntPtr.Zero, corDebugProcess);
                Assert.Equal(1, COMHelper.Release(corDebugProcess));
                Assert.Equal(0, COMHelper.Release(corDebugProcess));
                Assert.Equal(0, clrDebugging.Release());
                return Task.CompletedTask;
            });
        }

        #region Helper functions

        private static async Task<StartInfo> StartDebuggee(string xml, bool launch)
        {
            AfterInvoke(xml, out TestConfiguration config, out ITestOutputHelper output);

            StartInfo startInfo = new(output, config, launch);
            string debuggeeName = config.DebuggeeName();

            Assert.NotNull(debuggeeName);
            Assert.NotNull(config.DbgShimPath());

            DbgShimAPI.Initialize(config.DbgShimPath());

            // Restore and build the debuggee
            DebuggeeConfiguration debuggeeConfig = await DebuggeeCompiler.Execute(config, debuggeeName, startInfo.Output);

            // Build the debuggee command line
            StringBuilder commandLine = new();

            // Get the full launch command line (includes the host if required)
            if (!string.IsNullOrWhiteSpace(config.HostExe))
            {
                commandLine.Append(config.HostExe);
                commandLine.Append(" ");
                if (!string.IsNullOrWhiteSpace(config.HostArgs))
                {
                    commandLine.Append(config.HostArgs);
                    commandLine.Append(" ");
                }
            }
            commandLine.Append(debuggeeConfig.BinaryExePath);

            startInfo.CreatePipeConnection();
            commandLine.Append(" ");
            commandLine.Append(startInfo.PipeName);

            Trace.TraceInformation("CreateProcessForLaunch {0} {1} {2}", launch, commandLine.ToString(), startInfo.PipeName);
            AssertResult(DbgShimAPI.CreateProcessForLaunch(commandLine.ToString(), launch, currentDirectory: null, out int processId, out IntPtr resumeHandle));
            Assert.NotEqual(IntPtr.Zero, resumeHandle);
            Trace.TraceInformation("CreateProcessForLaunch pid {0} {1}", processId, commandLine.ToString());

            startInfo.SetProcessId(processId);
            startInfo.ResumeHandle = resumeHandle;

            // Wait for debuggee to start if attach/run
            if (!launch)
            {
                Assert.True(await startInfo.WaitForDebuggee());
            }
            Trace.TraceInformation("CreateProcessForLaunch pid {0} DONE", processId);
            return startInfo;
        }

        private static void TestRegisterForRuntimeStartup(StartInfo startInfo, int api)
        {
            AutoResetEvent wait = new AutoResetEvent(false);
            string applicationGroupId =  null;
            IntPtr unregisterToken = IntPtr.Zero;
            HResult result = HResult.S_OK;
            HResult callbackResult = HResult.S_OK;
            Exception callbackException = null;
            ICorDebug corDebug = null;

            Trace.TraceInformation("RegisterForRuntimeStartup pid {0} launch {1} api {2} START", startInfo.ProcessId, startInfo.Launch, api);

            DbgShimAPI.RuntimeStartupCallbackDelegate callback = (ICorDebug cordbg, object parameter, HResult hr) => {
                Trace.TraceInformation("RegisterForRuntimeStartup callback pid {0} hr {1:X}", startInfo.ProcessId, hr);
                corDebug = cordbg;
                callbackResult = hr;
                try
                {
                    // Only check the ICorDebug instance if success
                    if (hr)
                    {
                        TestICorDebug(startInfo, cordbg);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                    callbackException = ex;
                }
                wait.Set();
            };

            switch (api)
            {
                case 1:
                    result = DbgShimAPI.RegisterForRuntimeStartup(startInfo.ProcessId, parameter: IntPtr.Zero, out unregisterToken, callback);
                    break;
                case 2:
                    result = DbgShimAPI.RegisterForRuntimeStartupEx(startInfo.ProcessId, applicationGroupId, parameter: IntPtr.Zero, out unregisterToken, callback);
                    break;
                case 3:
                    LibraryProviderWrapper libraryProvider = new(startInfo.TestConfiguration.DbiModulePath(), startInfo.TestConfiguration.DacModulePath());
                    Console.WriteLine("Hit any key {0}", Process.GetCurrentProcess().Id);
                    Console.ReadLine();
                    result = DbgShimAPI.RegisterForRuntimeStartup3(startInfo.ProcessId, applicationGroupId, parameter: IntPtr.Zero, libraryProvider.ILibraryProvider, out unregisterToken, callback);
                    break;
                default:
                    throw new ArgumentException(nameof(api));
            }

            if (startInfo.Launch && startInfo.ResumeHandle != IntPtr.Zero) 
            {
                Trace.TraceInformation("RegisterForRuntimeStartup pid {0} ResumeProcess", startInfo.ProcessId);
                AssertResult(DbgShimAPI.ResumeProcess(startInfo.ResumeHandle));
            }

            AssertResult(result);

            Trace.TraceInformation("RegisterForRuntimeStartup pid {0} waiting for callback", startInfo.ProcessId);
            Assert.True(wait.WaitOne(TimeSpan.FromMinutes(5)));
            Trace.TraceInformation("RegisterForRuntimeStartup pid {0} after callback wait", startInfo.ProcessId);
            
            AssertResult(DbgShimAPI.UnregisterForRuntimeStartup(unregisterToken));
            Assert.Null(callbackException);

            switch (api)
            {
                case 1:
                case 2:
                    // The old APIs fail on single file apps
                    Assert.Equal(!startInfo.TestConfiguration.PublishSingleFile, callbackResult);
                    break;
                case 3:
                    // The new API should always succeed
                    AssertResult(callbackResult);
                    break;
            }

            if (callbackResult)
            {
                AssertResult(startInfo.WaitForCreateProcess());
                Assert.Equal(0, corDebug.Release());
            }

            Trace.TraceInformation("RegisterForRuntimeStartup pid {0} DONE", startInfo.ProcessId);
        }

        private static void TestCreateDebuggingInterface(StartInfo startInfo, int api)
        {
            Trace.TraceInformation("TestCreateDebuggingInterface pid {0} api {1} START", startInfo.ProcessId, api);
            HResult hr = DbgShimAPI.EnumerateCLRs(startInfo.ProcessId, (IntPtr[] continueEventHandles, string[] moduleNames) =>
            {
                Assert.True(continueEventHandles.Length == 1);
                Assert.True(moduleNames.Length == 1);
                for (int i = 0; i < continueEventHandles.Length; i++)
                {
                    Trace.TraceInformation("TestCreateDebuggingInterface pid {0} {1:X16} {2}", startInfo.ProcessId, continueEventHandles[i].ToInt64(), moduleNames[i]);
                    AssertX.FileExists("ModuleFilePath", moduleNames[i], startInfo.Output);

                    AssertResult(DbgShimAPI.CreateVersionStringFromModule(startInfo.ProcessId, moduleNames[i], out string versionString));
                    Trace.TraceInformation("TestCreateDebuggingInterface pid {0} version string {1}", startInfo.ProcessId, versionString);
                    Assert.False(string.IsNullOrWhiteSpace(versionString));

                    ICorDebug corDebug = null;
                    string applicationGroupId = null;
                    HResult result;
                    switch (api)
                    {
                        case 0:
                            result = DbgShimAPI.CreateDebuggingInterfaceFromVersion(versionString, out corDebug);
                            break;
                        case 1:
                            result = DbgShimAPI.CreateDebuggingInterfaceFromVersionEx(DbgShimAPI.CorDebugLatestVersion, versionString, out corDebug);
                            break;
                        case 2:
                            result = DbgShimAPI.CreateDebuggingInterfaceFromVersion2(DbgShimAPI.CorDebugLatestVersion, versionString, applicationGroupId, out corDebug);
                            break;
                        case 3:
                            LibraryProviderWrapper libraryProvider = new(startInfo.TestConfiguration.DbiModulePath(), startInfo.TestConfiguration.DacModulePath());
                            result = DbgShimAPI.CreateDebuggingInterfaceFromVersion3(DbgShimAPI.CorDebugLatestVersion, versionString, applicationGroupId, libraryProvider.ILibraryProvider, out corDebug);
                            break;
                        default:
                            throw new ArgumentException(nameof(api));
                    }

                    switch (api)
                    {
                        case 0:
                        case 1:
                        case 2:
                            // The old APIs fail on single file apps
                            Assert.Equal(!startInfo.TestConfiguration.PublishSingleFile, result);
                            break;
                        case 3:
                            // The new API should always succeed
                            AssertResult(result);
                            break;
                    }

                    if (result)
                    {
                        TestICorDebug(startInfo, corDebug);
                        AssertResult(startInfo.WaitForCreateProcess());
                        Assert.Equal(0, corDebug.Release());
                    }
                    else
                    {
                        Trace.TraceInformation("TestCreateDebuggingInterface pid {0} FAILED {1}", startInfo.ProcessId, result);
                    }
                }
            });
            AssertResult(hr);
            Trace.TraceInformation("TestCreateDebuggingInterface pid {0} DONE", startInfo.ProcessId);
        }

        private static readonly Guid IID_ICorDebugProcess = new Guid("3D6F5F64-7538-11D3-8D5B-00104B35E7EF");

        private static void TestICorDebug(StartInfo startInfo, ICorDebug corDebug)
        {
            Assert.NotNull(corDebug);
            AssertResult(corDebug.Initialize());
            ManagedCallbackWrapper managedCallback = new(startInfo);
            AssertResult(corDebug.SetManagedHandler(managedCallback.ICorDebugManagedCallback));
            AssertResult(corDebug.DebugActiveProcess(startInfo.ProcessId, out IntPtr process));
            AssertResult(COMHelper.QueryInterface(process, IID_ICorDebugProcess, out IntPtr icdp));
            Assert.True(icdp != IntPtr.Zero);
            COMHelper.Release(icdp);
        }

        private async Task RemoteInvoke(TestConfiguration config, Func<string, Task> method)
        {
            int exitCode = await RemoteExecutorHelper.RemoteInvoke(Output, config, method);
            Assert.Equal(0, exitCode);
        }

        private static void AfterInvoke(string xml, out TestConfiguration config, out ITestOutputHelper output)
        {
            config = TestConfiguration.Deserialize(xml);
            output = TestRunner.ConfigureLogging(config, new ConsoleTestOutputHelper(), "DbgShim.UnitTests");
            LoggingListener.EnableListener(output, ListenerName);
        }

        private static void AssertResult(HResult hr)
        {
            Xunit.Assert.Equal<HResult>(HResult.S_OK, hr);
        }

        #endregion
    }

    public static class DbgShimTestExtensions
    {
        public static string DbgShimPath(this TestConfiguration config)
        {
            return TestConfiguration.MakeCanonicalPath(config.GetValue("DbgShimPath"));
        }

        public static string DbiModulePath(this TestConfiguration config)
        {
            return TestConfiguration.MakeCanonicalPath(config.GetValue("DbiModulePath"));
        }

        public static string DacModulePath(this TestConfiguration config)
        {
            return TestConfiguration.MakeCanonicalPath(config.GetValue("DacModulePath"));
        }

        public static string DebuggeeName(this TestConfiguration config)
        {
            return config.GetValue("DebuggeeName");
        }
    }
}
