// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.DebugServices.Implementation
{
    public sealed class DumpTargetFactory : IDumpTargetFactory
    {
        private readonly IHost _host;

        public DumpTargetFactory (IHost host)
        {
            _host = host;
        }

        public ITarget OpenDump(string fileName)
        {
            DataTarget dataTarget;
            OSPlatform targetPlatform;
            try
            {
                fileName = Path.GetFullPath(fileName);
                dataTarget = DataTarget.LoadDump(fileName);
                targetPlatform = dataTarget.DataReader.TargetPlatform;
            }
            catch (Exception ex)
            {
                throw new DiagnosticsException(ex.Message, ex);
            }

            try
            {
                if (targetPlatform != OSPlatform.OSX &&
                    (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                     dataTarget.DataReader.EnumerateModules().Any((module) => Path.GetExtension(module.FileName) == ".dylib")))
                {
                    targetPlatform = OSPlatform.OSX;
                }
                return new TargetFromDataReader(dataTarget, targetPlatform, _host, fileName);
            }
            catch (Exception)
            {
                dataTarget.Dispose();
                throw;
            }
        }
    }
}
