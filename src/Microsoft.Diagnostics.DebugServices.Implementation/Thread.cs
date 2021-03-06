// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.DebugServices.Implementation
{
    public class Thread : IThread, IDisposable
    {
        private readonly ThreadService _threadService;
        private byte[] _threadContext;
        private ulong? _teb;

        protected readonly ServiceContainer _serviceContainer;

        public Thread(ThreadService threadService, int index, uint id)
        {
            _threadService = threadService;
            ThreadIndex = index;
            ThreadId = id;
            _serviceContainer = threadService.Services.GetService<IServiceManager>().CreateServiceContainer(ServiceScope.Thread, threadService.Services);
            _serviceContainer.AddService<IThread>(this);
        }

        void IDisposable.Dispose()
        {
            _serviceContainer.RemoveService(typeof(IThread));
            _serviceContainer.DisposeServices();
        }

        #region IThread

        public int ThreadIndex { get; }

        public uint ThreadId { get; }

        public ITarget Target => _threadService.Target;

        public IServiceProvider Services => _serviceContainer;

        public bool TryGetRegisterValue(int index, out ulong value)
        {
            value = 0;

            if (_threadService.TryGetRegisterInfo(index, out RegisterInfo info))
            {
                try
                {
                    Span<byte> threadContext = new(GetThreadContext(), info.RegisterOffset, info.RegisterSize);
                    switch (info.RegisterSize)
                    {
                        case 1:
                            value = MemoryMarshal.Read<byte>(threadContext);
                            return true;
                        case 2:
                            value = MemoryMarshal.Read<ushort>(threadContext);
                            return true;
                        case 4:
                            value = MemoryMarshal.Read<uint>(threadContext);
                            return true;
                        case 8:
                            value = MemoryMarshal.Read<ulong>(threadContext);
                            return true;
                        default:
                            Trace.TraceError($"GetRegisterValue: 0x{ThreadId:X4} {info.RegisterName} invalid size {info.RegisterSize}");
                            break;
                    }
                }
                catch (DiagnosticsException ex)
                {
                    Trace.TraceError($"GetRegisterValue: 0x{ThreadId:X4} {info.RegisterName} {ex}");
                }
            }
            return false;
        }

        public byte[] GetThreadContext()
        {
            if (_threadContext is null)
            {
                _threadContext = new byte[_threadService.ContextSize];
                if (!GetThreadContextInner(_threadService.ContextFlags, _threadContext))
                {
                    throw new DiagnosticsException();
                }
            }
            return _threadContext;
        }

        protected virtual bool GetThreadContextInner(uint contextFlags, byte[] context) => _threadService.GetThreadContext(ThreadId, contextFlags, context);

        public ulong GetThreadTeb()
        {
            if (!_teb.HasValue)
            {
                _teb = GetThreadTebInner();
            }
            return _teb.Value;
        }

        protected virtual ulong GetThreadTebInner() => _threadService.GetThreadTeb(ThreadId);

        #endregion

        public override bool Equals(object obj)
        {
            IThread thread = (IThread)obj;
            return Target == thread.Target && ThreadId == thread.ThreadId;
        }

        public override int GetHashCode()
        {
            return Utilities.CombineHashCodes(Target.GetHashCode(), ThreadId.GetHashCode());
        }

        public override string ToString()
        {
            return $"#{ThreadIndex} {ThreadId:X8}";
        }
    }
}
