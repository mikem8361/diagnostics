// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using System;

namespace Microsoft.Diagnostics.SOSConcord
{
    /// <summary>
    /// The one and only public class in the sample. This implements the IDkmCustomMessageForwardReceiver
    /// interface, which is how the sample is called.
    /// 
    /// Note that the list of interfaces implemented is defined here, and in 
    /// Microsoft.Diagnostics.SOSConcord.vsdconfigxml. Both lists need to be the same.
    /// </summary>
    public class SOSConcordService : IDkmCustomMessageForwardReceiver
    {
        public static readonly Guid guidSOSConcordCustomMessageId = new Guid("F190196A-8701-44E9-ACD5-606B005A471F");

        DkmCustomMessage IDkmCustomMessageForwardReceiver.SendLower(DkmCustomMessage customMessage)
        {
            return DkmCustomMessage.Create(customMessage.Connection, customMessage.Process, guidSOSConcordCustomMessageId, 1, "Test string", null);
        }
    }
}
