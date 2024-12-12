// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.Diagnostics.ICorDebugServices.Interop;

namespace Microsoft.Diagnostics.ICorDebugServices
{
    /// <summary>
    /// ITarget implementation for the ClrMD IDataReader
    /// </summary>
    public class TargetFromICorDebug : Target
    {
        private readonly ICorDebugProcess _process;

        /// <summary>
        /// Create a target instance from a ICorDebugProcess instance
        /// </summary>
        /// <param name="host">the host instance</param>
        /// <param name="platform">target operating system</param>
        /// <param name="architecture">target architecture</param>
        /// <param name="processId">target process id</param>
        /// <param name="process">ICorDebugProcess instance for target</param>
        /// <exception cref="DiagnosticsException">can not construct target instance</exception>
        public TargetFromICorDebug(IHost host, OSPlatform platform, Architecture architecture, int processId, ICorDebugProcess process)
            : base(host, dumpPath: null)
        {
            Debug.Assert(process is not null);
            OperatingSystem = platform;
            Architecture = architecture;
            ProcessId = (uint)processId;
            _process = process;

            // Add the thread, memory, and module services
            _serviceContainerFactory.AddServiceFactory<IThreadService>((services) => new ThreadServiceFromICorDebug(services, process));
            _serviceContainerFactory.AddServiceFactory<IModuleService>((services) => new ModuleServiceFromICorDebug(services, processId));
            _serviceContainerFactory.AddServiceFactory<IMemoryService>((_) => process);
            _serviceContainerFactory.AddServiceFactory<ICorDebugProcess>((_) => process);

            Finished();
        }

        public override void Destroy()
        {
            _process.Detach();
            _process.Dispose();
            base.Destroy();
        }
    }
}
