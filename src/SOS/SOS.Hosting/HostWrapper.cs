// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace SOS.Hosting
{
    public sealed class HostWrapper : COMCallableIUnknown
    {
        private static readonly Guid IID_IHost = new("E0CD8534-A88B-40D7-91BA-1B4C925761E9");

        private readonly IHost _host;

        public ServiceWrapper ServiceWrapper { get; } = new ServiceWrapper();

        public IntPtr IHost { get; }

        public HostWrapper(IHost host)
        {
            _host = host;

            VTableBuilder builder = AddInterface(IID_IHost, validate: false);
            builder.AddMethod(new GetHostTypeDelegate(GetHostType));
            builder.AddMethod(new GetServiceDelegate(ServiceWrapper.GetService));
            builder.AddMethod(new GetCurrentTargetDelegate(GetCurrentTarget));
            builder.AddMethod(new GetTempDirectoryDelegate(GetTempDirectory));
            builder.AddMethod(new WriteTraceDelegate(WriteTrace));
            IHost = builder.Complete();

            AddRef();
        }

        protected override void Destroy()
        {
            Trace.TraceInformation("HostWrapper.Destroy");
            ServiceWrapper.Dispose();
        }

        #region IHost

        /// <summary>
        /// Returns the host type
        /// </summary>
        private HostType GetHostType(IntPtr self) => _host.HostType;

        /// <summary>
        /// Returns the current target wrapper or null
        /// </summary>
        /// <param name="targetWrapper">target wrapper address returned</param>
        /// <returns>S_OK</returns>
        private int GetCurrentTarget(IntPtr self, out IntPtr targetWrapper)
        {
            IContextService contextService = _host.Services.GetService<IContextService>();
            ITarget target = contextService.GetCurrentTarget();
            TargetWrapper wrapper = target?.Services.GetService<TargetWrapper>();
            if (wrapper == null)
            {
                targetWrapper = IntPtr.Zero;
                return HResult.E_NOINTERFACE;
            }
            wrapper.AddRef();
            targetWrapper = wrapper.ITarget;
            return HResult.S_OK;
        }

        /// <summary>
        /// Returns the unique temporary directory for this debug session
        /// </summary>
        private string GetTempDirectory(
            IntPtr self)
        {
            return _host.GetTempDirectory();
        }

        /// <summary>
        /// Must match IHost::TraceType enum in host.h
        /// </summary>
        private enum TraceType
        {
            Information = 1,            // Trace.TraceInformation
            Warning = 2,                // Trace.TraceWarning
            Error = 3                   // Trace.TraceError
        }

        /// <summary>
        /// Native code logging support
        /// </summary>
        private void WriteTrace(
            IntPtr self,
            TraceType type,
            string message)
        {
            switch (type)
            {
                case TraceType.Information:
                    Trace.TraceInformation(message);
                    break;
                case TraceType.Warning:
                    Trace.TraceWarning(message);
                    break;
                case TraceType.Error:
                    Trace.TraceError(message);
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region IHost delegates

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate HostType GetHostTypeDelegate(
            [In] IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int GetServiceDelegate(
            [In] IntPtr self,
            [In] in Guid guid,
            [Out] out IntPtr ptr);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetCurrentTargetDelegate(
            [In] IntPtr self,
            [Out] out IntPtr target);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private delegate string GetTempDirectoryDelegate(
            [In] IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void WriteTraceDelegate(
            [In] IntPtr self,
            [In] TraceType type,
            [In, MarshalAs(UnmanagedType.LPStr)] string message);

        #endregion
    }
}
