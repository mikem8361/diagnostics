// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Symbols;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.SOSConcord
{
    /// <summary>
    /// Module service implementation for the native debugger services
    /// </summary>
    internal class ModuleServiceFromConcordServices : ModuleService
    {
        class ModuleFromConcordServices : Module, IModuleSymbols
        {
            private readonly ModuleServiceFromConcordServices _moduleService;
            private readonly DkmModuleInstance _dkmModuleInstance;
            private VersionData _versionData;
            private string _versionString;

            public ModuleFromConcordServices(ModuleServiceFromConcordServices moduleService, DkmModuleInstance dkmModuleInstance, int moduleIndex)
                : base(moduleService.Target)
            {
                _moduleService = moduleService;
                _dkmModuleInstance = dkmModuleInstance;
                ModuleIndex = moduleIndex;
                FileName = dkmModuleInstance.FullName;
                ImageBase = dkmModuleInstance.BaseAddress;
                ImageSize = dkmModuleInstance.Size;
                IndexFileSize = dkmModuleInstance.Size;
                IndexTimeStamp = FileTimeToCrtTime(dkmModuleInstance.TimeDateStamp);
                ServiceProvider.AddService<IModuleSymbols>(this);
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

            public override int ModuleIndex { get; }

            public override string FileName { get; }

            public override ulong ImageBase { get; }

            public override ulong ImageSize { get; }

            public override uint? IndexFileSize { get; }

            public override uint? IndexTimeStamp { get; }

            public override VersionData VersionData
            {
                get
                {
                    if (InitializeValue(Module.Flags.InitializeVersion))
                    {
                        if (_moduleService.Target.OperatingSystem == OSPlatform.Windows)
                        {
                            DkmModuleVersion version = _dkmModuleInstance.Version;
                            int major = (int)(version.FileVersionMS >> 16);
                            int minor = (int)(version.FileVersionMS & 0xffff);
                            int revision = (int)(version.FileVersionLS >> 16);
                            int patch = (int)(version.FileVersionLS & 0xffff);
                            _versionData = new VersionData(major, minor, revision, patch);
                        }
                        else
                        {
                            _versionData = GetVersion();
                        }
                    }
                    return _versionData;
                }
            }

            public override string VersionString
            {
                get
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
                            _versionString = _moduleService.GetVersionString(ImageBase);
                        }
                    }
                    return _versionString;
                }
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

            #endregion

            protected override bool TryGetSymbolAddressInner(string name, out ulong address)
            {
                address = 0;
                return false;
            }

            protected override ModuleService ModuleService => _moduleService;
        }

        internal ModuleServiceFromConcordServices(TargetFromConcordServices target, IMemoryService rawMemoryService)
            : base(target, rawMemoryService)
        {
        }

        /// <summary>
        /// Get/create the modules dictionary.
        /// </summary>
        protected override Dictionary<ulong, IModule> GetModulesInner()
        {
            var modules = new Dictionary<ulong, IModule>();
            int index = 0;

            DkmProcess dkmProcess = ((TargetFromConcordServices)Target).DkmProcess;
            foreach (DkmRuntimeInstance dkmRuntime in dkmProcess.GetRuntimeInstances())
            {
                foreach (DkmModuleInstance dkmModuleinstance in dkmRuntime.GetModuleInstances())
                {
                    if (dkmModuleinstance.TagValue == DkmModuleInstance.Tag.NativeModuleInstance)
                    {
                        var module = new ModuleFromConcordServices(this, dkmModuleinstance, index++);
                        modules.Add(dkmModuleinstance.BaseAddress, module);
                    }
                }
            }

            return modules;
        }
    }
}
