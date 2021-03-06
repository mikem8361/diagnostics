// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.DefaultPort;

namespace Microsoft.Diagnostics.SOSConcord
{
    /// <summary>
    /// ITarget implementation for concord services
    /// </summary>
    internal sealed class TargetFromConcordServices : Target
    {
        internal readonly DkmProcess DkmProcess;

        /// <summary>
        /// Create a target instance from a DkmProcess instance
        /// </summary>
        internal TargetFromConcordServices(IHost host, DkmProcess process)
            : base(host, dumpPath: null)
        {
            DkmProcess = process;

            DkmProcessorArchitecture processor = process.SystemInformation.ProcessorArchitecture;
            Architecture = processor switch
            {
                DkmProcessorArchitecture.PROCESSOR_ARCHITECTURE_AMD64 => Architecture.X64,
                DkmProcessorArchitecture.PROCESSOR_ARCHITECTURE_INTEL => Architecture.X86,
                DkmProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM => Architecture.Arm,
                DkmProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM64 => Architecture.Arm64,
                _ => throw new PlatformNotSupportedException($"Machine type not supported: {processor}"),
            };
            IsDump = (process.SystemInformation.Flags & DkmSystemInformationFlags.DumpFile) != 0;
            OperatingSystem = process.BaseDebugMonitorId == DkmBaseDebugMonitorId.ManagedCoreDumpFile ? OSPlatform.Linux : OSPlatform.Windows;
            ProcessId = (uint?)process.LivePart?.Id;

            // Add the thread, memory, and module services
            _serviceContainerFactory.AddServiceFactory<IModuleService>((services) => new ModuleServiceFromConcordServices(services));
            _serviceContainerFactory.AddServiceFactory<IThreadService>((services) => new ThreadServiceFromConcordServices(services));
            _serviceContainerFactory.AddServiceFactory<IMemoryService>((_) => {
                IMemoryService memoryService = new MemoryServiceFromConcordServices(this);
                if (IsDump)
                {
                    ServiceContainerFactory clone = _serviceContainerFactory.Clone();
                    clone.RemoveServiceFactory<IMemoryService>();

                    // The underlying VS host doesn't map native modules into the address space
                    memoryService = new ImageMappingMemoryService(clone.Build(), memoryService, managed: false);
                }
                return memoryService;
            });

            // Add optional crash info service (currently only for Native AOT on Linux/MacOS).
            _serviceContainerFactory.AddServiceFactory<ICrashInfoService>((services) => SpecialDiagInfo.CreateCrashInfoServiceFromException(services));
            OnFlushEvent.Register(() => FlushService<ICrashInfoService>());

            Finished();
        }
    }
}
