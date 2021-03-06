// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.DebugServices.Implementation;

namespace Microsoft.Diagnostics.SOSConcord
{
    /// <summary>
    /// Provides the context services on native debuggers
    /// </summary>
    internal sealed class ContextServiceFromConcordServices : ContextService
    {
        private readonly SOSConcordService _concordService;

        internal ContextServiceFromConcordServices(SOSConcordService concordService)
            : base(concordService)
        {
            _concordService = concordService;
        }

        public void SetContext(byte[] processGuid, int? threadId)
        {
            if (processGuid is not null)
            {
                if (_concordService.Targets.TryGetValue(new Guid(processGuid), out TargetFromConcordServices target))
                {
                    base.SetCurrentTarget(target);

                    if (threadId.HasValue)
                    {
                        IThread thread = target.Services.GetService<IThreadService>().GetThreadFromId((uint)threadId.Value);
                        if (thread is not null)
                        {
                            base.SetCurrentThread(thread);
                        }
                    }
                }
            }
        }
    }
}
