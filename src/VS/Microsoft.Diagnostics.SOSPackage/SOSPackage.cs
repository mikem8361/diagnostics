// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft.Diagnostics.SOSConcord;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.DefaultPort;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Process = EnvDTE.Process;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Diagnostics.SOSPackage
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(guidSOSPackage)]
    public sealed class SOSPackage : AsyncPackage, IOleCommandTarget, VisualStudio.OLE.Interop.IServiceProvider, IVsCustomDebuggerEventHandler110
    {
        /// <summary>
        /// VS SOS package guid string.
        /// </summary>
        private const string guidSOSPackage = "b37816e5-3d6e-4b48-84fb-5223eab4e908";

        /// <summary>
        /// This command set guid (see .vsct)
        /// </summary>
        private static readonly Guid guidSOSPackageCmdSet = new("c0ecea67-cdd1-42ec-93e3-98de4d357fb5");

        /// <summary>
        /// The diag command id (see .vsct)
        /// </summary>
        private const int sosCommandId = 0x0100;

        internal DTE2 DTE { get; private set; }

        private IOleCommandTarget _packageCommandTarget;
        private CommandWindow _commandWindow;
        private DebuggerEvents _debuggerEvents;
        private IDebuggerInternal110 _debuggerInternal;
        private uint _outputServiceCookie;

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        [SuppressMessage("Reliability", "VSSDK006:Check services exist", Justification = "The services are checked for null")]
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            DTE = (DTE2)await GetServiceAsync(typeof(DTE)).ConfigureAwait(false);
            if (DTE is null)
            {
                throw new ArgumentException("DTE service not found");
            }
            _packageCommandTarget = (await GetServiceAsync(typeof(IOleCommandTarget)).ConfigureAwait(false)) as IOleCommandTarget;
            if (_packageCommandTarget is null)
            {
                throw new ArgumentException("IOleCommandTarget service not found");
            }
            _commandWindow = DTE.ToolWindows.CommandWindow;

            // Used to get the current process
            SVsShellDebugger debugger = (SVsShellDebugger)await GetServiceAsync(typeof(SVsShellDebugger)).ConfigureAwait(false);
            _debuggerInternal = (IDebuggerInternal110)debugger;

            IProfferService proffer = (IProfferService)ServiceProvider.GlobalProvider.GetService(typeof(IProfferService).GUID);
            if (proffer is null)
            {
                throw new ArgumentException("IProfferService not found");
            }
            Guid outputService = SOSConcordService.guidSOSPackageService;
            proffer.ProfferService(ref outputService, this, out _outputServiceCookie);

            // Hook any context changes
            _debuggerEvents = DTE.Events.DebuggerEvents;
            _debuggerEvents.OnContextChanged += OnContextChanged;
        }

        #endregion

        #region IOleCommandTarget

        int IOleCommandTarget.Exec(ref Guid cmdGroup, uint nCmdID, uint nCmdExecOpt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (cmdGroup == guidSOSPackageCmdSet)
            {
                switch (nCmdID)
                {
                    case sosCommandId:
                        return ExecuteCommand(nCmdExecOpt, pvaIn, pvaOut);

                    default:
                        Debug.Fail("Unknown command id");
                        return VSConstants.E_NOTIMPL;
                }
            }
            return _packageCommandTarget.Exec(cmdGroup, nCmdID, nCmdExecOpt, pvaIn, pvaOut);
        }

        int IOleCommandTarget.QueryStatus(ref Guid cmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (cmdGroup == guidSOSPackageCmdSet)
            {
                switch (prgCmds[0].cmdID)
                {
                    case sosCommandId:
                        prgCmds[0].cmdf |= (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_INVISIBLE);
                        return VSConstants.S_OK;

                    default:
                        Debug.Fail("Unknown command id");
                        return VSConstants.E_NOTIMPL;
                }
            }
            return _packageCommandTarget.QueryStatus(ref cmdGroup, cCmds, prgCmds, pCmdText);
        }

        private int ExecuteCommand(uint nCmdExecOpt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (IsQueryParameterList(pvaIn, pvaOut, nCmdExecOpt))
            {
                Marshal.GetNativeVariantForObject("", pvaOut);
                return VSConstants.E_FAIL;
            }
            int hr = EnsureString(pvaIn, out string commandLine);
            if (hr != VSConstants.S_OK)
            {
                Trace.TraceError("ExecuteCommand EnsureString FAILED {0:X8}", hr);
                return hr;
            }
            DkmProcess process = GetCurrentProcess();
            if (process is null)
            {
                _commandWindow.OutputString("No current process" + Environment.NewLine);
                return VSConstants.E_FAIL;
            }
            return SendMessage(process, SOSConcordServiceMessageId.ExecuteCommand, commandLine, GetCommandWindowWidth());
        }

        private static int EnsureString(IntPtr pvaIn, out string arguments)
        {
            arguments = null;
            if (pvaIn == IntPtr.Zero) {
                // No arguments.
                return VSConstants.E_INVALIDARG;
            }

            object vaInObject = Marshal.GetObjectForNativeVariant(pvaIn);
            if (vaInObject == null || vaInObject.GetType() != typeof(string)) {
                return VSConstants.E_INVALIDARG;
            }

            arguments = vaInObject as string;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Used to determine if the shell is querying for the parameter list.
        /// </summary>
        private static bool IsQueryParameterList(System.IntPtr _, System.IntPtr pvaOut, uint nCmdexecopt)
        {
            ushort lo = (ushort)(nCmdexecopt & (uint)0xffff);
            ushort hi = (ushort)(nCmdexecopt >> 16);
            if (lo == (ushort)OLECMDEXECOPT.OLECMDEXECOPT_SHOWHELP)
            {
                if (hi == VsMenus.VSCmdOptQueryParameterList)
                {
                    if (pvaOut != IntPtr.Zero) {
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion

        #region VisualStudio.OLE.Interop.IServiceProvider

        int VisualStudio.OLE.Interop.IServiceProvider.QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject)
        {
            if (guidService == SOSConcordService.guidSOSPackageService)
            {
                IntPtr unk = Marshal.GetIUnknownForObject(this);
                int hr = Marshal.QueryInterface(unk, ref riid, out ppvObject);
                Marshal.Release(unk);
                return hr;
            }
            ppvObject = IntPtr.Zero;
            return VSConstants.E_NOINTERFACE;
        }

        #endregion

        #region IVsCustomDebuggerEventHandler110

        int IVsCustomDebuggerEventHandler110.OnCustomDebugEvent(ref Guid processId, VsComponentMessage message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            switch ((SOSPackageMessageId)message.MessageCode)
            {
                case SOSPackageMessageId.OutputStringNormal:
                case SOSPackageMessageId.OutputStringWarning:
                case SOSPackageMessageId.OutputStringError:
                    if (message.Parameter1 is string text)
                    {
                        _commandWindow.OutputString(text);
                        return VSConstants.S_OK;
                    }
                    break;
            }

            return VSConstants.E_INVALIDARG;
        }

        #endregion

        private void OnContextChanged(Process newProcess, Program newProgram, EnvDTE.Thread newThread, EnvDTE.StackFrame newStackFrame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DkmProcess process = GetCurrentProcess();
            if (process != null)
            {
                if (SendMessage(process, SOSConcordServiceMessageId.SetContext, process.UniqueId.ToByteArray(), newThread?.ID) == VSConstants.S_OK)
                {
                    Trace.TraceInformation("OnContextChange process {0} ({1}) thread {2}", newProcess.ProcessID, newProcess?.Name, newThread?.ID);
                }
                else
                {
                    Trace.TraceError("OnContextChange FAILED process {0} thread {1}", newProcess.ProcessID, newThread?.ID);
                }
            }
        }

        private DkmProcess GetCurrentProcess()
        {
            DkmProcess process = null;
            IDebugProcess2 currentProcess = _debuggerInternal.CurrentProcess;
            if (currentProcess != null)
            {
                if (currentProcess.GetProcessId(out Guid processGuid) == VSConstants.S_OK)
                {
                    process = DkmProcess.FindProcess(processGuid);
                }
                if (process is null)
                {
                    Trace.TraceError("Can not find the DkmProcess instance");
                }
            }
            else
            {
                Trace.TraceError("No current AD7 process");
            }
            return process;
        }

        private static int SendMessage(DkmProcess process, SOSConcordServiceMessageId messageId, object parameter1, object parameter2)
        {
            try
            {
                DkmTransportConnection connection = process.Connection;
                DkmCustomMessage message = DkmCustomMessage.Create(connection, process, SOSConcordService.guidSOSConcordComponentId, (int)messageId, parameter1, parameter2);
                return message.SendLower().MessageCode;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return VSConstants.E_INVALIDARG;
            }
        }

        [SuppressMessage("Usage", "VSTHRD010:Invoke single-threaded types on Main thread", Justification = "Always called on main thread")]
        private int GetCommandWindowWidth()
        {
            try
            {
                return _commandWindow.Parent.Width;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return 0;
            }
        }
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("FFECCFD2-28FF-4B1C-B0E7-E9D668FCD18F")]
    internal interface IDebuggerInternal110
    {
        void Unused_slot0(); // LoadSymbolsForModules
        void Unused_slot1(); // DetachFromProcess
        void Unused_slot2(); // RunTileToStatement
        void Unused_slot3(); // RunThreadsToStatement
        void Unused_slot4(); // StartAsyncEvaluation
        void Unused_slot5(); // VerifyCanFastWWARefresh
        void Unused_slot6(); // FindSourceFile
        void Unused_slot7(); // BeginBusyNoPaint
        void Unused_slot8(); // EndBusyNoPaint
        IDebugProcess2 CurrentProcess
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
    }
}
