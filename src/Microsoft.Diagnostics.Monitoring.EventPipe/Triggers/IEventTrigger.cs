// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal interface IEventTrigger<TEvent>
    {
        bool HasSatisfiedCondition(TEvent @event);
    }
}
