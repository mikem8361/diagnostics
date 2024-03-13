// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Xml.Linq;
using Microsoft.Diagnostics.DebugServices;
using SOS.Hosting;
using SOS.Hosting.DbgEng.Interop;

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

        public void Write(string text) => _outputService.OutputString(DebuggerOutputService.OutputType.Normal, text);

        public void WriteWarning(string text) => _outputService.OutputString(DebuggerOutputService.OutputType.Warning, text);

        public void WriteError(string text) => _outputService.OutputString(DebuggerOutputService.OutputType.Error, text);

        public void WriteDml(string text) => _outputService.OutputString(DebuggerOutputService.OutputType.Dml, text);

        public void WriteDmlExec(string text, string cmd)
        {
            if (!SupportsDml || string.IsNullOrWhiteSpace(cmd))
            {
                Write(text);
            }
            else
            {
                string dml = $"<exec cmd=\"{DmlEscape(cmd)}\">{DmlEscape(text)}</exec>";
                WriteDml(dml);
            }
        }

        int IConsoleService.WindowWidth => _outputService.GetOutputWidth();

        public bool SupportsDml => _supportsDml ??= _outputService.SupportsDml;

        public CancellationToken CancellationToken { get; set; }

        #endregion

        private static string DmlEscape(string text) => string.IsNullOrWhiteSpace(text) ? text : new XText(text).ToString();
    }
}
