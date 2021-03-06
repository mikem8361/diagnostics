// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Diagnostics.VSPackage
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
    [Guid(guidVSPackage)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class VSPackage : AsyncPackage, IOleCommandTarget
    {
        /// <summary>
        /// VSPackage guid string.
        /// </summary>
        private const string guidVSPackage = "b37816e5-3d6e-4b48-84fb-5223eab4e908";

        /// <summary>
        /// This command set guid (see .vsct)
        /// </summary>
        private static readonly Guid guidVSPackageCmdSet = new Guid("c0ecea67-cdd1-42ec-93e3-98de4d357fb5");

        /// <summary>
        /// The diag command id (see .vsct)
        /// </summary>
        private const int sosCommandId = 0x0100;

        internal DTE2 DTE { get; private set; }

        IOleCommandTarget _packageCommandTarget;
        CommandWindow _commandWindow;

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            DTE = (DTE2)await GetServiceAsync(typeof(DTE));
            if (DTE == null) {
                throw new Exception("DTE service not found");
            }
            _packageCommandTarget = (await GetServiceAsync(typeof(IOleCommandTarget))) as IOleCommandTarget;
            if (_packageCommandTarget == null) {
                throw new Exception("IOleCommandTarget service not found");
            }
            _commandWindow = DTE.ToolWindows.CommandWindow;
        }

        #endregion

        #region IOleCommandTarget

        int IOleCommandTarget.Exec(ref Guid cmdGroup, uint nCmdID, uint nCmdExecOpt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (cmdGroup == VSPackage.guidVSPackageCmdSet) {
                switch (nCmdID) {
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

            if (cmdGroup == guidVSPackageCmdSet) {
                switch (prgCmds[0].cmdID) {
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

        int ExecuteCommand(uint nCmdExecOpt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (IsQueryParameterList(pvaIn, pvaOut, nCmdExecOpt)) {
                Marshal.GetNativeVariantForObject("", pvaOut);
                return VSConstants.E_FAIL;
            }

            int hr = EnsureString(pvaIn, out string commandLine);
            if (hr != VSConstants.S_OK) {
                return hr;
            }

            //DispatchCommand(commandLine);

            return VSConstants.S_OK;
        }

        static int EnsureString(IntPtr pvaIn, out string arguments)
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
        static bool IsQueryParameterList(System.IntPtr pvaIn, System.IntPtr pvaOut, uint nCmdexecopt)
        {
            ushort lo = (ushort)(nCmdexecopt & (uint)0xffff);
            ushort hi = (ushort)(nCmdexecopt >> 16);
            if (lo == (ushort)OLECMDEXECOPT.OLECMDEXECOPT_SHOWHELP) {
                if (hi == VsMenus.VSCmdOptQueryParameterList) {
                    if (pvaOut != IntPtr.Zero) {
                        return true;
                    }
                }
            }
            return false;
        }

        internal int GetCommandWindowWidth()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try {
                return _commandWindow.Parent.Width;
            }
            catch (Exception ex) {
                Trace.TraceError(ex.ToString());
                return 0;
            }
        }

        internal void WriteCommandWindow(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _commandWindow.OutputString(text);
        }

        #endregion
    }
}
