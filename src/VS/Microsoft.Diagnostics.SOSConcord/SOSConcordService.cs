// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.Diagnostics.ExtensionCommands;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using SOS.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.SOSConcord
{
    /// <summary>
    /// This implements the IDkmCustomMessageForwardReceiver and IHost interfaces which is how the SOS infrastructure is called and initialized.
    /// </summary>
    public class SOSConcordService :  IHost, IDkmCustomMessageForwardReceiver, IDkmProcessCreateNotification, IDkmProcessExitNotification, IDkmProcessContinueNotification
    {
        /// <summary>
        /// The guid of this concord component/extension
        /// </summary>
        public static readonly Guid guidSOSConcordComponentId = new("144350B5-17AC-486F-B269-68126EED4C52");

        /// <summary>
        /// The VS service proffered by the SOS package
        /// </summary>
        public static readonly Guid guidSOSPackageService = new("C3DE8B01-7038-452A-ADF0-B39F23CB80C4");

        private static readonly Guid guidSOSConcordCustomMessageId = new("F190196A-8701-44E9-ACD5-606B005A471F");

        internal Dictionary<Guid, TargetFromConcordServices> Targets = new Dictionary<Guid, TargetFromConcordServices>();

        private readonly ServiceProvider _serviceProvider;
        private readonly CommandService _commandService;
        private readonly SymbolService _symbolService;
        private readonly ContextServiceFromConcordServices _contextService;
        private readonly ConsoleServiceFromConcordServices _consoleService;
        private object _lock = new();
        private int _targetIdFactory;

        public SOSConcordService()
        {
            // Enable the assembly resolver to get the right versions in the same directory as this assembly.
            AssemblyResolver.Enable();

            // Enable logging
            string logfile = null;
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                logfile = Path.Combine(Path.GetDirectoryName(assemblyPath), "VSSOSLogging.txt");
            }
            DiagnosticLoggingService.Initialize(logfile);

            Trace.TraceInformation($"SOSConcordServices.SOSConcordServices START");
            _serviceProvider = new ServiceProvider(); 
            _symbolService = new SymbolService(this);
            _commandService = new CommandService("sos");

            _serviceProvider.AddService<IHost>(this);
            _serviceProvider.AddService<ICommandService>(_commandService);
            _serviceProvider.AddService<ISymbolService>(_symbolService);

            _consoleService = new ConsoleServiceFromConcordServices();
            var fileLoggingConsoleService = new FileLoggingConsoleService(_consoleService);
            _serviceProvider.AddService<IConsoleService>(fileLoggingConsoleService);
            _serviceProvider.AddService<IConsoleFileLoggingService>(fileLoggingConsoleService);

            DiagnosticLoggingService.Instance.SetConsole(_consoleService, fileLoggingConsoleService);
            _serviceProvider.AddService<IDiagnosticLoggingService>(DiagnosticLoggingService.Instance);

            _contextService = new ContextServiceFromConcordServices(this);
            _serviceProvider.AddService<IContextService>(_contextService);

            _commandService.AddCommands(new Assembly[] { typeof(ClrMDHelper).Assembly });
            _commandService.AddCommands(new Assembly[] { typeof(SOSHost).Assembly });
            _serviceProvider.AddServiceFactory<SOSLibrary>(() => SOSLibrary.Create(this));
            _serviceProvider.AddServiceFactory<SOSHost>(() => new SOSHost(_contextService.Services));

            _contextService.ServiceProvider.AddServiceFactory<ClrMDHelper>(() => {
                ClrRuntime clrRuntime = _contextService.Services.GetService<ClrRuntime>();
                return clrRuntime != null ? new ClrMDHelper(clrRuntime) : null;
            });

            // Automatically enable symbol server support and default cache
            _symbolService.AddSymbolServer(msdl: true, symweb: false, symbolServerPath: null, authToken: null, timeoutInMinutes: 0);
            _symbolService.AddCachePath(_symbolService.DefaultSymbolCache);
            Trace.TraceInformation($"SOSConcordServices.SOSConcordServices DONE");
        }

        #region IDkmCustomMessageForwardReceiver

        public DkmCustomMessage SendLower(DkmCustomMessage message)
        {
            int result = HResult.E_INVALIDARG;
            try
            {
                lock (_lock)
                {
                    switch ((SOSConcordServiceMessageId)message.MessageCode)
                    {
                        case SOSConcordServiceMessageId.ExecuteCommand:
                            if (message.Parameter1 is string commandLine && message.Parameter2 is int consoleWidth)
                            {
                                _consoleService.ConsoleWidth = consoleWidth;
                                if (string.IsNullOrWhiteSpace(commandLine))
                                {
                                    if (_commandService.DisplayHelp(null, _contextService.Services))
                                    {
                                        result = HResult.S_OK;
                                    }
                                }
                                else
                                {
                                    if (_commandService.Execute(commandLine, _contextService.Services))
                                    {
                                        result = HResult.S_OK;
                                    }
                                }
                            }
                            break;

                        case SOSConcordServiceMessageId.SetContext:
                            _contextService.SetContext((byte[])message.Parameter1, (int?)message.Parameter2);
                            result = HResult.S_OK;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return DkmCustomMessage.Create(message.Connection, message.Process, guidSOSConcordCustomMessageId, result, null, null);
        }

        #endregion

        #region IDkmProcessCreateNotification, IDkmProcessExitNotification, IDkmProcessContinueNotification

        public void OnProcessCreate(DkmProcess process, DkmWorkList workList, DkmEventDescriptor eventDescriptor)
        {
            Trace.TraceInformation($"SOSConcordServices.OnProcessCreate {process.UniqueId}");
            try
            {
                lock (_lock)
                {
                    CreateTarget(process);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
        }

        public void OnProcessExit(DkmProcess process, int exitCode, DkmEventDescriptor eventDescriptor)
        {
            Trace.TraceInformation($"SOSConcordServices.OnProcessExit {process.UniqueId}");
            try
            {
                lock (_lock)
                {
                    DestroyTarget(process);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
        }

        public void OnProcessContinue(DkmProcess process)
        {
            Trace.TraceInformation($"SOSConcordServices.OnProcessContinue {process.UniqueId}");
            try
            {
                lock (_lock)
                {
                    FlushTarget(process);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
        }

        #endregion

        #region IHost

        public IServiceEvent OnShutdownEvent { get; } = new ServiceEvent();

        public HostType HostType => HostType.Vs;

        public IServiceProvider Services => _serviceProvider;

        public IEnumerable<ITarget> EnumerateTargets() => Targets.Values;

        public void DestroyTarget(ITarget target)
        {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }
            Trace.TraceInformation("IHost.DestroyTarget #{0}", target.Id);
            if (target is TargetFromConcordServices targetFromConcordServices)
            {
                DkmProcess process = targetFromConcordServices.DkmProcess;
                Trace.TraceInformation($"SOSConcordServices.DestroyTarget {process.UniqueId}");
                DestroyTarget(process);
            }
        }

        #endregion

        private void CreateTarget(DkmProcess process)
        {
            if (!Targets.ContainsKey(process.UniqueId))
            {
                TargetFromConcordServices target = new(this, _targetIdFactory++, process);
                Targets.Add(process.UniqueId, target);
                _contextService.SetCurrentTarget(target);
            }
        }

        private void DestroyTarget(DkmProcess process)
        {
            if (Targets.TryGetValue(process.UniqueId, out TargetFromConcordServices target))
            { 
                _contextService.ClearCurrentTarget();
                Targets.Remove(process.UniqueId);
                if (target is IDisposable disposable) {
                    disposable.Dispose();
                }
            }
        }

        private void FlushTarget(DkmProcess process)
        {
            if (Targets.TryGetValue(process.UniqueId, out TargetFromConcordServices target))
            {
                target.Flush();
            }
        }

        internal static void SendMessage(SOSPackageMessageId messageId, object parameter)
        {
            DkmCustomMessage.Create(null, null, guidSOSConcordCustomMessageId, (int)messageId, parameter, null).SendToVsService(guidSOSPackageService, IsBlocking: false);
        }
    }
}
