// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.DebugServices;
using System;
using System.Threading;

namespace Microsoft.Diagnostics.SOSConcord
{
    internal class ConsoleServiceFromConcordServices : IConsoleService
    {
        internal int ConsoleWidth { get; set; }

        internal ConsoleServiceFromConcordServices()
        {
        }

        #region IConsoleService

        public void Write(string text) => SOSConcordService.SendMessage(SOSPackageMessageId.OutputStringNormal, text);

        public void WriteWarning(string text) => SOSConcordService.SendMessage(SOSPackageMessageId.OutputStringWarning, text);

        public void WriteError(string text) => SOSConcordService.SendMessage(SOSPackageMessageId.OutputStringError, text);

        public bool SupportsDml => false;

        public void WriteDml(string text) => throw new NotSupportedException();

        public CancellationToken CancellationToken { get; set; }

        int IConsoleService.WindowWidth => ConsoleWidth;

        #endregion
    }
}
