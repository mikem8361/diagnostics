// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.DefaultPort;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.SOSConcord
{
    /// <summary>
    /// ITarget implementation for concord services
    /// </summary>
    internal class TargetFromConcordServices : Target
    {
        internal readonly DkmProcess DkmProcess;
       
        /// <summary>
        /// Create a target instance from a DkmProcess instance
        /// </summary>
        internal TargetFromConcordServices(IHost host, int id, DkmProcess process)
            : base(host, id, dumpPath: null)
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
            IMemoryService rawMemoryService = new MemoryServiceFromConcordServices(this);
            ServiceProvider.AddServiceFactory<IModuleService>(() => new ModuleServiceFromConcordServices(this, rawMemoryService));
            ServiceProvider.AddServiceFactory<IThreadService>(() => new ThreadServiceFromConcordServices(this));
            ServiceProvider.AddServiceFactory<IMemoryService>(() => {
                IMemoryService memoryService = rawMemoryService;
                if (IsDump)
                {
                    memoryService = new ImageMappingMemoryService(this, memoryService);
                }
                return memoryService;
            });
        }
    }
}
