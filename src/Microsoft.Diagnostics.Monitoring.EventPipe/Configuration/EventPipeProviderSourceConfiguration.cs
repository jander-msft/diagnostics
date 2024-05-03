// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.NETCore.Client;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    public sealed class EventPipeProviderSourceConfiguration : MonitoringSourceConfiguration
    {
        private readonly IEnumerable<EventPipeProvider> _providers;
        private readonly int _bufferSizeInMB;
        private bool _requestRundown;

        public EventPipeProviderSourceConfiguration(bool requestRundown = true, int bufferSizeInMB = 256, params EventPipeProvider[] providers)
        {
            _providers = providers;
            _requestRundown = requestRundown;
            _bufferSizeInMB = bufferSizeInMB;
        }

        public override IList<EventPipeProvider> GetProviders()
        {
            return _providers.ToList();
        }

        public override bool IncludeDefaultRundownKeywords => _requestRundown;

        public override int BufferSizeInMB => _bufferSizeInMB;
    }
}
