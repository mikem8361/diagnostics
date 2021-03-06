// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Diagnostics.DebugServices;

namespace Microsoft.Diagnostics.SOSConcord
{
    internal sealed class ConsoleServiceFromConcordServices : IConsoleService
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

        public void WriteDml(string text) => Write(text);

        public void WriteDmlExec(string text, string _) => Write(text);

        public CancellationToken CancellationToken { get; set; }

        int IConsoleService.WindowWidth => ConsoleWidth;

        #endregion
    }
}
