// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal sealed class ExistenceEvaluator : EventEvaluator
    {
        public ExistenceEvaluator(string providerName, string eventName, string counterName, int counterIntervalMSec)
            : base(providerName, eventName, counterName, counterIntervalMSec)
        {
        }

        public override bool EvaluateCore(TraceEventData data)
        {
            return true;
        }
    }
}
