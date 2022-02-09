// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.FileFormats;
using Microsoft.FileFormats.PE;
using Microsoft.SymbolStore;
using Microsoft.SymbolStore.KeyGenerators;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace SOS.Hosting
{
    public sealed unsafe class LibraryProviderWrapper : COMCallableIUnknown
    {
        public static readonly Guid IID_ICLRDebuggingLibraryProvider = new Guid("3151C08D-4D09-4f9b-8838-2880BF18FE51");
        public static readonly Guid IID_ICLRDebuggingLibraryProvider2 = new Guid("E04E2FF1-DCFD-45D5-BCD1-16FFF2FAF7BA");
        public static readonly Guid IID_ICLRDebuggingLibraryProvider3 = new Guid("DE3AAB18-46A0-48B4-BF0D-2C336E69EA1B");

        public IntPtr ILibraryProvider { get; }

        private readonly OSPlatform _targetOS;
        private readonly ISymbolService _symbolService;
        private readonly string _dbiModulePath;
        private readonly string _dacModulePath;

        public LibraryProviderWrapper(ISymbolService symbolService, string dbiModulePath, string dacModulePath)
           : this(GetRunningOS(), symbolService, dbiModulePath, dacModulePath)
        {
        }

        public LibraryProviderWrapper(OSPlatform targetOS, ISymbolService symbolService, string dbiModulePath, string dacModulePath)
        {
            _targetOS = targetOS;
            _symbolService = symbolService;
            _dbiModulePath = dbiModulePath;
            _dacModulePath = dacModulePath;

            VTableBuilder builder = AddInterface(IID_ICLRDebuggingLibraryProvider, validate: false);
            builder.AddMethod(new ProvideLibraryDelegate(ProvideLibrary));
            ILibraryProvider = builder.Complete();

            builder = AddInterface(IID_ICLRDebuggingLibraryProvider2, validate: false);
            builder.AddMethod(new ProvideLibrary2Delegate(ProvideLibrary2));
            builder.Complete();

            builder = AddInterface(IID_ICLRDebuggingLibraryProvider3, validate: false);
            builder.AddMethod(new ProvideUnixLibraryDelegate(ProvideUnixLibrary));
            builder.Complete();

            AddRef();
        }

        private static OSPlatform GetRunningOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return OSPlatform.Windows;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return OSPlatform.Linux;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return OSPlatform.OSX;
            throw new NotSupportedException($"OS not supported {RuntimeInformation.OSDescription}");
        }

        protected override void Destroy()
        {
            Trace.TraceInformation("LibraryProviderWrapper.Destroy");
        }

        private HResult ProvideLibrary(
            IntPtr self,
            string fileName,
            uint timeStamp,
            uint sizeOfImage,
            out IntPtr moduleHandle)
        {
            Trace.TraceInformation($"LibraryProviderWrapper.ProvideLibrary {fileName} {timeStamp:X8} {sizeOfImage:X8}");
            try
            {
                // This should only be called when hosted on Windows because of the PAL module handle problems
                Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

                string modulePath = null;
                if (fileName.Contains(DbiName))
                {
                    if (_dbiModulePath is not null)
                    {
                        modulePath = _dbiModulePath;
                    }
                    else
                    {
                        modulePath = DownloadModule(DbiName, timeStamp, sizeOfImage);
                    }
                }
                // This needs to work for long named DAC's so remove the extension
                else if (fileName.Contains(Path.GetFileNameWithoutExtension(DacName)))
                {
                    if (_dacModulePath is not null)
                    {
                        modulePath = _dacModulePath;
                    }
                    else
                    {
                        modulePath = DownloadModule(DacName, timeStamp, sizeOfImage);
                    }
                }
                TestGetPEInfo(modulePath, timeStamp, sizeOfImage);
                moduleHandle = DataTarget.PlatformFunctions.LoadLibrary(modulePath);
                Trace.TraceInformation($"LibraryProviderWrapper.ProvideLibrary SUCCEEDED {modulePath}");
                return HResult.S_OK;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"LibraryProviderWrapper.ProvideLibrary {ex}");
            }
            Trace.TraceError($"LibraryProviderWrapper.ProvideLibrary FAILED");
            moduleHandle = IntPtr.Zero;
            return HResult.E_INVALIDARG;
        }

        private HResult ProvideLibrary2(
            IntPtr self,
            string fileName,
            uint timeStamp,
            uint sizeOfImage,
            out IntPtr modulePathOut)
        {
            Trace.TraceInformation($"LibraryProviderWrapper.ProvideLibrary2 {fileName} {timeStamp:X8} {sizeOfImage:X8}");
            try
            {
                string modulePath = null;
                if (fileName.Contains(DbiName))
                {
                    if (_dbiModulePath != null)
                    {
                        modulePath = _dbiModulePath; 
                    }
                    else 
                    {
                        modulePath = DownloadModule(DbiName, timeStamp, sizeOfImage);
                    }
                }
                // This needs to work for long named DAC's so remove the extension
                else if (fileName.Contains(Path.GetFileNameWithoutExtension(DacName)))
                {
                    if (_dacModulePath != null)
                    {
                        modulePath = _dacModulePath;
                    }
                    else
                    {
                        modulePath = DownloadModule(DacName, timeStamp, sizeOfImage);
                    }
                }
                // If this is called on Linux or MacOS don't verify. This should only happen if
                // these tests are run against an old dbgshim version.
                if (_targetOS == OSPlatform.Windows)
                {
                    TestGetPEInfo(modulePath, timeStamp, sizeOfImage);
                }
                modulePathOut = Marshal.StringToCoTaskMemUni(modulePath); 
                Trace.TraceInformation($"LibraryProviderWrapper.ProvideLibrary2 SUCCEEDED {modulePath}");
                return HResult.S_OK;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"LibraryProviderWrapper.ProvideLibrary2 {ex}");
            }
            Trace.TraceError("LibraryProviderWrapper.ProvideLibrary2 FAILED");
            modulePathOut = IntPtr.Zero;
            return HResult.E_INVALIDARG;
        }

        private HResult ProvideUnixLibrary(
            IntPtr self,
            string fileName,
            byte* buildIdBytes,
            int buildIdSize,
            out IntPtr modulePathOut)
        {
            try
            {
                byte[] buildId = Array.Empty<byte>();
                string modulePath = null;
                if (buildIdBytes != null && buildIdSize > 0)
                {
                    Span<byte> span = new Span<byte>(buildIdBytes, buildIdSize);
                    buildId = span.ToArray();
                }
                Trace.TraceInformation("LibraryProviderWrapper.ProvideUnixLibrary {0} {1}", fileName, string.Concat(buildId.Select((b) => b.ToString("x2"))));
                if (fileName.Contains(DbiName))
                {
                    if (_dbiModulePath != null)
                    {
                        modulePath = _dbiModulePath; 
                    }
                    else 
                    {
                        modulePath = DownloadModule(DbiName, buildId);
                    }
                }
                else if (fileName.Contains(DacName))
                {
                    if (_dacModulePath != null)
                    {
                        modulePath = _dacModulePath;
                    }
                    else
                    {
                        modulePath = DownloadModule(DacName, buildId);
                    }
                }
                TestBuildId(modulePath, buildId);
                modulePathOut = Marshal.StringToCoTaskMemUni(modulePath); 
                Trace.TraceInformation($"LibraryProviderWrapper.ProvideUnixLibrary SUCCEEDED {modulePath}");
                return HResult.S_OK;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"LibraryProviderWrapper.ProvideUnixLibrary {ex}");
            }
            Trace.TraceError("LibraryProviderWrapper.ProvideUnixLibrary FAILED");
            modulePathOut = IntPtr.Zero;
            return HResult.E_INVALIDARG;
        }

        private string DownloadModule(string moduleName, uint timeStamp, uint sizeOfImage)
        {
            Assert.True(timeStamp != 0 && sizeOfImage != 0);
            SymbolStoreKey key = PEFileKeyGenerator.GetKey(moduleName, timeStamp, sizeOfImage);
            Assert.NotNull(key);
            string downloadedPath = _symbolService.DownloadFile(key);
            Assert.NotNull(downloadedPath);
            return downloadedPath;
        }

        private string DownloadModule(string moduleName, byte[] buildId)
        {
            Assert.True(buildId.Length > 0);
            SymbolStoreKey key = null;
            OSPlatform platform;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // This is the cross-DAC case when OpenVirtualProcess calls on a Linux/MacOS dump. Should never
                // get here for a Windows dump or for live sessions (RegisterForRuntimeStartup, etc).
                platform = _targetOS;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                platform = OSPlatform.Linux;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                platform = OSPlatform.OSX;
            }
            else
            {
                throw new NotSupportedException($"OS not supported {RuntimeInformation.OSDescription}");
            }
            if (platform == OSPlatform.Linux)
            {
                key = ELFFileKeyGenerator.GetKeys(KeyTypeFlags.IdentityKey, moduleName, buildId, symbolFile: false, symbolFileName: null).SingleOrDefault();
            }
            else if (platform == OSPlatform.OSX)
            {
                key = MachOFileKeyGenerator.GetKeys(KeyTypeFlags.IdentityKey, moduleName, buildId, symbolFile: false, symbolFileName: null).SingleOrDefault();
            }
            Assert.NotNull(key);
            string downloadedPath = _symbolService.DownloadFile(key);
            Assert.NotNull(downloadedPath);
            return downloadedPath;
        }

        private void TestGetPEInfo(string filePath, uint timeStamp, uint sizeOfImage)
        {
            if (timeStamp != 0 && sizeOfImage != 0)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using Stream stream = Utilities.TryOpenFile(filePath);
                    if (stream is not null)
                    {
                        var peFile = new PEFile(new StreamAddressSpace(stream), false);
                        if (peFile.IsValid())
                        {
                            Assert.Equal(peFile.Timestamp, timeStamp);
                            Assert.Equal(peFile.SizeOfImage, sizeOfImage);
                            return;
                        }
                    }
                    throw new ArgumentException($"GetPEInfo {filePath} not valid PE file");
                }
            }
        }

        private void TestBuildId(string filePath, byte[] buildId)
        {
            if (buildId.Length > 0)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    using Utilities.ELFModule elfModule = Utilities.OpenELFFile(filePath);
                    if (elfModule is not null)
                    {
                        Assert.Equal(elfModule.BuildID, buildId);
                        return;
                    }
                    throw new ArgumentException($"TestBuildId {filePath} not valid ELF file");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    using Utilities.MachOModule machOModule = Utilities.OpenMachOFile(filePath);
                    if (machOModule is not null)
                    {
                        Assert.Equal(machOModule.Uuid, buildId);
                        return;
                    }
                    throw new ArgumentException($"TestBuildId {filePath} not valid MachO file");
                }
            }
        }

        private string DbiName
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "mscordbi.dll";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "libmscordbi.so";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "libmscordbi.dylib";
                throw new NotSupportedException($"OS not supported {RuntimeInformation.OSDescription}");
            }
        }

        private string DacName 
        {
            get 
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "mscordaccore.dll";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "libmscordaccore.so";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "libmscordaccore.dylib";
                throw new NotSupportedException($"OS not supported {RuntimeInformation.OSDescription}");
            }
        }

        #region ICLRDebuggingLibraryProvider* delegates

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate HResult ProvideLibraryDelegate(
            [In] IntPtr self,
            [In, MarshalAs(UnmanagedType.LPWStr)] string fileName,
            [In] uint timeStamp,
            [In] uint sizeOfImage,
            out IntPtr moduleHandle);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate HResult ProvideLibrary2Delegate(
            [In] IntPtr self,
            [In, MarshalAs(UnmanagedType.LPWStr)] string fileName,
            [In] uint timeStamp,
            [In] uint sizeOfImage,
            out IntPtr modulePath);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate HResult ProvideUnixLibraryDelegate(
            [In] IntPtr self,
            [In, MarshalAs(UnmanagedType.LPWStr)] string fileName,
            [In] byte* buildIdBytes,
            [In] int buildIdSize,
            out IntPtr modulePath);

        #endregion
    }
}
