// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.EventPipe;
using System;
using System.IO;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring
{
    public class CounterJsonStreamExporter : IMetricsLogger
    {
        private string _processName;
        private StringBuilder builder;
        private int flushLength = 10_000; // Arbitrary length to flush
        private StreamWriter _outputWriter;

        public CounterJsonStreamExporter(Stream outputStream, string processName)
        {
            _outputWriter = new StreamWriter(outputStream, Encoding.UTF8, 4096, true);
            _processName = processName;
        }

        public void PipelineStarted()
        {
            builder = new StringBuilder();
            builder.Append($"{{ \"TargetProcess\": \"{_processName}\", ");
            builder.Append($"\"StartTime\": \"{DateTime.Now.ToString()}\", ");
            builder.Append($"\"Events\": [");
        }

        public void PipelineStopped()
        {
            builder.Remove(builder.Length - 1, 1); // Remove the last comma to ensure valid JSON format.
            builder.Append($"] }}");
            FlushToStream();
        }

        private void CounterPayloadReceived(string providerName, ICounterPayload payload)
        {
            if (builder.Length > flushLength)
            {
                FlushToStream();
            }
            builder.Append($"{{ \"timestamp\": \"{DateTime.Now.ToString("u")}\", ");
            builder.Append($" \"provider\": \"{providerName}\", ");
            builder.Append($" \"name\": \"{payload.GetDisplay()}\", ");
            builder.Append($" \"counterType\": \"{payload.GetCounterType()}\", ");
            builder.Append($" \"value\": {payload.GetValue()} }},");
        }

        private void FlushToStream()
        {
            _outputWriter.Write(builder.ToString());
            _outputWriter.Flush();
            builder.Clear();
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
