// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.DebugServices.Implementation
{
    /// <summary>
    /// Module service implementation for clrmd
    /// </summary>
    public class ManagedModuleService : ModuleService, IManagedModuleService
    {
        private readonly IRuntime _runtime;

        public ManagedModuleService(IServiceProvider services, IRuntime runtime)
            : base(services)
        {
            _runtime = runtime;
        }

        /// <summary>
        /// Get/create the modules dictionary.
        /// </summary>
        protected override Dictionary<ulong, IModule> GetModulesInner()
        {
            Dictionary<ulong, IModule> modules = new();
            int moduleIndex = 0;

            ClrRuntime clrRuntime = _runtime.Services.GetService<ClrRuntime>();
            if (clrRuntime is not null)
            {
                foreach (ClrModule clrModule in clrRuntime.EnumerateModules())
                {
                    ModuleFromAddress module = new(this, moduleIndex, clrModule.ImageBase, clrModule.Size, clrModule.Name);
                    module.AddService(clrModule);
                    try
                    {
                        modules.Add(module.ImageBase, module);
                        moduleIndex++;
                    }
                    catch (ArgumentException)
                    {
                        Trace.TraceError($"GetModules(): duplicate module base '{module}' dup '{modules[module.ImageBase]}'");
                    }
                }
            }

            return modules;
        }
    }
}
