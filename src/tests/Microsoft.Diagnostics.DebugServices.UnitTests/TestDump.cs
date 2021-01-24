using Microsoft.Diagnostics.DebugServices.Implementation;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.TestHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.DebugServices.UnitTests
{
    public class TestDump : TestHost, IHost
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly SymbolService _symbolService;
        private DataTarget _dataTarget;

        public TestDump(TestConfiguration config)
            : base(config)
        {
            _serviceProvider = new ServiceProvider();
            _symbolService = new SymbolService(this);
            _serviceProvider.AddService<ISymbolService>(_symbolService);

            // Automatically enable symbol server support
            _symbolService.AddSymbolServer(msdl: true, symweb: false, symbolServerPath: null, authToken: null, timeoutInMinutes: 0);
            _symbolService.AddCachePath(_symbolService.DefaultSymbolCache);
        }

        protected override ITarget GetTarget()
        {
            _dataTarget = DataTarget.LoadDump(DumpFile);

            OSPlatform targetPlatform = _dataTarget.DataReader.TargetPlatform;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                targetPlatform = OSPlatform.OSX;
            }
            _symbolService.AddDirectoryPath(Path.GetDirectoryName(DumpFile));
            return new TargetFromDataReader(_dataTarget.DataReader, targetPlatform, this, DumpFile);
        }

        #region IHost

        public IServiceEvent OnShutdownEvent { get; } = new ServiceEvent();

        HostType IHost.HostType => HostType.DotnetDump;

        IServiceProvider IHost.Services => _serviceProvider;

        IEnumerable<ITarget> IHost.EnumerateTargets() => Target != null ? new ITarget[] { Target } : Array.Empty<ITarget>();

        ITarget IHost.CurrentTarget => Target;

        void IHost.SetCurrentTarget(int targetid) => throw new NotImplementedException();

        #endregion
    }
}
