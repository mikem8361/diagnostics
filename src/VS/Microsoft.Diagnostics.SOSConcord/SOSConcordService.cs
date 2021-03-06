// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.Diagnostics.ExtensionCommands;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using SOS.Hosting;

namespace Microsoft.Diagnostics.SOSConcord
{
    /// <summary>
    /// This implements the IDkmCustomMessageForwardReceiver and IHost interfaces which is how the SOS infrastructure is called and initialized.
    /// </summary>
    public class SOSConcordService : Host, IDkmCustomMessageForwardReceiver, IDkmProcessCreateNotification, IDkmProcessExitNotification, IDkmProcessContinueNotification
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

        internal Dictionary<Guid, TargetFromConcordServices> Targets = new();

        private readonly ServiceManager _serviceManager;
        private readonly CommandService _commandService;
        private readonly ContextServiceFromConcordServices _contextService;
        private readonly ConsoleServiceFromConcordServices _consoleService;
        private ServiceContainer _serviceContainer;
        private object _lock = new();

        public SOSConcordService()
            : base(HostType.Vs)
        {
            // Enable the assembly resolver to get the right versions in the same directory as this assembly.
            AssemblyResolver.Enable();
            try
            {
                // Enable logging
                if (!DiagnosticLoggingService.Instance.IsEnabled)
                {
                    string logfile = null;
                    string assemblyPath = Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(assemblyPath))
                    {
                        logfile = Path.Combine(Path.GetDirectoryName(assemblyPath), "VSSOSLogging.txt");
                    }
                    DiagnosticLoggingService.Initialize(logfile);
                }
                Trace.TraceInformation($"SOSConcordServices.SOSConcordServices START");
                _serviceManager = new ServiceManager();
                _commandService = new CommandService("sos");
                _serviceManager.NotifyExtensionLoad.Register(_commandService.AddCommands);

                _consoleService = new ConsoleServiceFromConcordServices();
                FileLoggingConsoleService fileLoggingConsoleService = new(_consoleService);
                DiagnosticLoggingService.Instance.SetConsole(_consoleService, fileLoggingConsoleService);

                // Register all the services and commands in the Microsoft.Diagnostics.DebugServices.Implementation assembly
                _serviceManager.RegisterAssembly(typeof(Target).Assembly);

                // Register all the services and commands in the SOS.Hosting assembly
                _serviceManager.RegisterAssembly(typeof(SOSHost).Assembly);

                // Register all the services and commands in the Microsoft.Diagnostics.ExtensionCommands assembly
                _serviceManager.RegisterAssembly(typeof(ClrMDHelper).Assembly);

                // Display any extension assembly loads on console
                _serviceManager.NotifyExtensionLoad.Register((Assembly assembly) => fileLoggingConsoleService.WriteLine($"Loading extension {assembly.Location}"));
                _serviceManager.NotifyExtensionLoadFailure.Register((Exception ex) => fileLoggingConsoleService.WriteLine(ex.Message));

                // Load any extra extensions
                _serviceManager.LoadExtensions();

                // Loading extensions or adding service factories not allowed after this point.
                _serviceManager.FinalizeServices();

                _serviceContainer = _serviceManager.CreateServiceContainer(ServiceScope.Global, parent: null);
                _serviceContainer.AddService<IServiceManager>(_serviceManager);
                _serviceContainer.AddService<IHost>(this);

                _serviceContainer.AddService<IConsoleService>(fileLoggingConsoleService);
                _serviceContainer.AddService<IConsoleFileLoggingService>(fileLoggingConsoleService);
                _serviceContainer.AddService<IDiagnosticLoggingService>(DiagnosticLoggingService.Instance);
                _serviceContainer.AddService<ICommandService>(_commandService);

                _contextService = new ContextServiceFromConcordServices(this);
                _serviceContainer.AddService<IContextService>(_contextService);

                SymbolService symbolService = new(this);
                _serviceContainer.AddService<ISymbolService>(symbolService);

                // Automatically enable symbol server support and default cache
                symbolService.AddSymbolServer(retryCount: 3);
                symbolService.AddCachePath(symbolService.DefaultSymbolCache);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                throw;
            }
            Trace.TraceInformation($"SOSConcordServices.SOSConcordServices DONE");
        }

        #region IDkmCustomMessageForwardReceiver

        public DkmCustomMessage SendLower(DkmCustomMessage message)
        {
            int result = HResult.S_OK;
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
                                _commandService.Execute(string.IsNullOrWhiteSpace(commandLine) ? "help" : commandLine, _contextService.Services);
                            }
                            break;

                        case SOSConcordServiceMessageId.SetContext:
                            _contextService.SetContext((byte[])message.Parameter1, (int?)message.Parameter2);
                            break;

                        default:
                            result = HResult.E_INVALIDARG;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                result = HResult.E_INVALIDARG;
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

        private void CreateTarget(DkmProcess process)
        {
            if (!Targets.ContainsKey(process.UniqueId))
            {
                TargetFromConcordServices target = new(this, process);
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
                target.Destroy();
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
