// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using Microsoft.Diagnostics.DebugServices;

namespace SOS.Extensions
{
    internal sealed class ConsoleServiceFromDebuggerServices : IConsoleService
    {
        private readonly DebuggerOutputService _outputService;
        private bool? _supportsDml;

        public ConsoleServiceFromDebuggerServices(DebuggerOutputService outputService)
        {
            _outputService = outputService;
        }

        #region IConsoleService

        int IConsoleService.WindowWidth => _outputService.GetOutputWidth();

        public bool SupportsDml => _supportsDml ??= _outputService.SupportsDml;

        CancellationToken IConsoleService.CancellationToken { get; set; }

        void IConsoleService.WriteString(OutputType type, string text) => _outputService.OutputString(type, text);

        #endregion
    }
}
