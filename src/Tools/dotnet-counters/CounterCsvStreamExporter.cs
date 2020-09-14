// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.EventPipe;
using System;
using System.IO;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring
{
    internal class CounterCsvStreamExporter : IMetricsLogger
    {
        private StringBuilder _builder;
        private int flushLength = 10_000; // Arbitrary length to flush
        private StreamWriter _outputWriter;

        public CounterCsvStreamExporter(Stream outputStream)
        {
            _outputWriter = new StreamWriter(outputStream, Encoding.UTF8, 4096, true);
        }

        public void PipelineStarted()
        {
            _builder = new StringBuilder();
            _builder.AppendLine("Timestamp,Provider,Counter Name,Counter Type,Mean/Increment");
        }

        public void PipelineStopped()
        {
            FlushToStream();
        }

        private void CounterPayloadReceived(string providerName, ICounterPayload payload)
        {
            if (_builder.Length > flushLength)
            {
                FlushToStream();
            }
            _builder.Append(DateTime.UtcNow.ToString() + ",");
            _builder.Append(providerName + ",");
            _builder.Append(payload.GetDisplay() + ",");
            _builder.Append(payload.GetCounterType() + ",");
            _builder.Append(payload.GetValue() + "\n");
        }

        private void FlushToStream()
        {
            if (_builder != null)
            {
                _outputWriter.Write(_builder.ToString());
                _outputWriter.Flush();
                _builder.Clear();
            }
        }

        public void LogMetrics(ICounterPayload metric)
        {
            CounterPayloadReceived(metric.GetProvider(), metric);
        }

        public void Dispose()
        {
            _outputWriter.Dispose();
        }
    }
}
