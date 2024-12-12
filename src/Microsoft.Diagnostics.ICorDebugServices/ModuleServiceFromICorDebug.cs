// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.Diagnostics.ICorDebugServices.Interop;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.ICorDebugServices
{
    /// <summary>
    /// Module service implementation for the native debugger services
    /// </summary>
    internal sealed class ModuleServiceFromICorDebug : ModuleService
    {
        private sealed class ModuleFromICorDebug : Module
        {
            private readonly ModuleServiceFromICorDebug _moduleService;
            private Version _version;
            private string _versionString;

            public ModuleFromICorDebug(
                ModuleServiceFromICorDebug moduleService,
                int moduleIndex,
                string imageName,
                ulong imageBase,
                ulong imageSize)
                : base(moduleService.Services)
            {
                _moduleService = moduleService;
                ModuleIndex = moduleIndex;
                FileName = imageName ?? string.Empty;
                ImageBase = imageBase;
                ImageSize = imageSize;
                IndexFileSize = null;
                IndexTimeStamp = null;
            }

            public override void Dispose()
            {
                base.Dispose();
            }

            #region IModule

            public override Version GetVersionData()
            {
                if (InitializeValue(Module.Flags.InitializeVersion))
                {
                    _version = GetVersionInner();
                }
                return _version;
            }

            public override string GetVersionString()
            {
                if (InitializeValue(Module.Flags.InitializeProductVersion))
                {
                    _versionString = GetVersionStringInner();
                }
                return _versionString;
            }

            public override string LoadSymbols()
            {
                return _moduleService.SymbolService.DownloadSymbolFile(this);
            }

            #endregion

            protected override ModuleService ModuleService => _moduleService;
        }

        private readonly int _processId;

        internal ModuleServiceFromICorDebug(IServiceProvider services, int processId)
            : base(services)
        {
            _processId = processId;
        }

        /// <summary>
        /// Get/create the modules dictionary.
        /// </summary>
        protected override Dictionary<ulong, IModule> GetModulesInner()
        {
            Dictionary<ulong, IModule> modules = new();
            try
            {
                Process process = Process.GetProcessById(_processId);
                ProcessModuleCollection processModules = process.Modules;
                for (int i = 0; i < processModules.Count; i++)
                {
                    ProcessModule processModule = processModules[i];
                    ModuleFromICorDebug module = new(this, i, processModule.ModuleName, (ulong)processModule.BaseAddress, (ulong)processModule.ModuleMemorySize);
                    modules.Add(module.ImageBase, module);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or PlatformNotSupportedException)
            {
                Trace.TraceError(ex.ToString());
            }
            return modules;
        }
    }
}
