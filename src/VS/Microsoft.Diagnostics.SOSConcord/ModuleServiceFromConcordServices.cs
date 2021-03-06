// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.Diagnostics.SOSConcord
{
    /// <summary>
    /// Module service implementation for the native debugger services
    /// </summary>
    internal sealed class ModuleServiceFromConcordServices : ModuleService
    {
        private sealed class ModuleFromConcordServices : Module, IModuleSymbols
        {
            private readonly ModuleServiceFromConcordServices _moduleService;
            private readonly DkmModuleInstance _dkmModuleInstance;
            private Version _version;
            private string _versionString;

            public ModuleFromConcordServices(ModuleServiceFromConcordServices moduleService, DkmModuleInstance dkmModuleInstance, int moduleIndex)
                : base(moduleService.Services)
            {
                _moduleService = moduleService;
                _dkmModuleInstance = dkmModuleInstance;
                ModuleIndex = moduleIndex;
                FileName = dkmModuleInstance.FullName;
                ImageBase = dkmModuleInstance.BaseAddress;
                ImageSize = dkmModuleInstance.Size;
                IndexFileSize = dkmModuleInstance.Size;
                IndexTimeStamp = FileTimeToCrtTime(dkmModuleInstance.TimeDateStamp);

                _serviceContainer.AddService<IModuleSymbols>(this);
            }

            private static uint FileTimeToCrtTime(ulong filetime)
            {
                // Got this logic from msdn blog(search for "Date/Time Formats and Conversions")
                const ulong Jan1970FileTime = 116444736000000000;       // January 1st 1970's FileTime value
                const uint FormatConversionRatio = 10000000;            // Represent the ratio between TimeDateStamp's resolution(1second) and Filetime's resolution(100 nanoseconds)
                ulong crtTime = (filetime - Jan1970FileTime) / FormatConversionRatio;
                return (uint)crtTime;
            }

            #region IModule

            public override Version GetVersionData()
            {
                if (InitializeValue(Module.Flags.InitializeVersion))
                {
                    if (_moduleService.Target.OperatingSystem == OSPlatform.Windows)
                    {
                        DkmModuleVersion version = _dkmModuleInstance.Version;
                        int major = (int)(version.FileVersionMS >> 16);
                        int minor = (int)(version.FileVersionMS & 0xffff);
                        int build = (int)(version.FileVersionLS >> 16);
                        int revision = (int)(version.FileVersionLS & 0xffff);
                        _version = new Version(major, minor, build, revision);
                    }
                    else
                    {
                        _version = GetVersionInner();
                    }
                }
                return _version;
            }

            public override string GetVersionString()
            {
                if (InitializeValue(Module.Flags.InitializeProductVersion))
                {
                    if (_moduleService.Target.OperatingSystem == OSPlatform.Windows)
                    {
                        DkmModuleVersion version = _dkmModuleInstance.Version;
                        _versionString = version.FileVersionString;
                    }
                    else if (!IsPEImage)
                    {
                        _versionString = GetVersionStringInner();
                    }
                }
                return _versionString;
            }

            public override string LoadSymbols()
            {
                return _moduleService.SymbolService.DownloadSymbolFile(this);
            }

            #endregion

            #region IModuleSymbols

            bool IModuleSymbols.TryGetSymbolName(ulong address, out string symbol, out ulong displacement)
            {
                symbol = null;
                displacement = 0;
                return false;
            }

            bool IModuleSymbols.TryGetSymbolAddress(string name, out ulong address)
            {
                address = 0;
                return false;
            }

            bool IModuleSymbols.TryGetType(string typeName, out IType type)
            {
                type = null;
                return false;
            }

            SymbolStatus IModuleSymbols.GetSymbolStatus()
            {
                return SymbolStatus.Unknown;
            }

            #endregion

            protected override bool TryGetSymbolAddressInner(string name, out ulong address)
            {
                address = 0;
                return false;
            }

            protected override ModuleService ModuleService => _moduleService;
        }

        internal ModuleServiceFromConcordServices(IServiceProvider services)
            : base(services)
        {
        }

        /// <summary>
        /// Get/create the modules dictionary.
        /// </summary>
        protected override Dictionary<ulong, IModule> GetModulesInner()
        {
            Dictionary<ulong, IModule> modules = new();
            int index = 0;

            DkmProcess dkmProcess = ((TargetFromConcordServices)Target).DkmProcess;
            foreach (DkmRuntimeInstance dkmRuntime in dkmProcess.GetRuntimeInstances())
            {
                foreach (DkmModuleInstance dkmModuleinstance in dkmRuntime.GetModuleInstances())
                {
                    if (dkmModuleinstance.TagValue == DkmModuleInstance.Tag.NativeModuleInstance)
                    {
                        ModuleFromConcordServices module = new(this, dkmModuleinstance, index++);
                        modules.Add(dkmModuleinstance.BaseAddress, module);
                    }
                }
            }

            return modules;
        }
    }
}
