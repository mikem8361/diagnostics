// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace SOS.Hosting
{
    public sealed class OutputServiceWrapper : COMCallableIUnknown
    {
        private static Guid IID_IOutputService = new("30745290-DC07-4993-9B61-F6132CCEB663");

        private readonly IConsoleService _consoleService;

        public IntPtr IOutputService { get; }

        public OutputServiceWrapper(IConsoleService consoleService)
        {
            _consoleService = consoleService;

            VTableBuilder builder = AddInterface(IID_IOutputService, validate: false);
            builder.AddMethod(new GetOutputWidthDelegate(GetOutputWidth));
            builder.AddMethod(new SupportsDmlDelegate(SupportsDml));
            builder.AddMethod(new OutputStringDelegate(OutputString));

            IOutputService = builder.Complete();

            AddRef();
        }

        protected override void Destroy()
        {
            Trace.TraceInformation("OutputServiceWrapper.Destroy");
        }

        #region IOutputService

        /// <summary>
        /// Returns the output window width
        /// </summary>
        private uint GetOutputWidth(IntPtr self) => (uint)_consoleService.WindowWidth;

        private uint SupportsDml(IntPtr self) => _consoleService.SupportsDml ? 1U : 0U;

        private void OutputString(IntPtr self, OutputType type, string text) => _consoleService.WriteString(type, text);

        #endregion

        #region IOutputService delegates

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate uint GetOutputWidthDelegate(
            [In] IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate uint SupportsDmlDelegate(
            [In] IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void OutputStringDelegate(
            [In] IntPtr self,
            [In] OutputType type,
            [In, MarshalAs(UnmanagedType.LPStr)] string text);

        #endregion
    }
}
