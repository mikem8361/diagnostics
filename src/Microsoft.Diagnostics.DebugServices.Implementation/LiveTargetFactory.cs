// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.DebugServices.Implementation
{
    public sealed class LiveTargetFactory : ILiveTargetFactory
    {
        private readonly IHost _host;

        public LiveTargetFactory (IHost host)
        {
            _host = host;
        }

        public ITarget Attach (int processId)
        {
            DataTarget dataTarget;
            OSPlatform targetPlatform;
            try
            {
                dataTarget = DataTarget.AttachToProcess(processId, suspend: true);
                targetPlatform = dataTarget.DataReader.TargetPlatform;
            }
            catch (Exception ex)
            {
                throw new DiagnosticsException(ex.Message, ex);
            }
            try
            {
                return new TargetFromDataReader(dataTarget, targetPlatform, _host, dumpPath: null);
            }
            catch (Exception)
            {
                dataTarget.Dispose();
                throw;
            }
        }
    }
}
