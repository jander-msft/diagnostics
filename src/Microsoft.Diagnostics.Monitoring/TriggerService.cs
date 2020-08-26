using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.AspNet;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring
{
    internal class TriggerService : ITriggerServiceInternal
    {
        private readonly TriggerConfiguration _config;
        private readonly IList<Tuple<Guid, DiagnosticsMonitor, Task>> _data =
            new List<Tuple<Guid, DiagnosticsMonitor, Task>>();

        public TriggerService(IOptions<TriggerConfiguration> config)
        {
            _config = config.Value;
        }

        public async Task RegisterAsync(IpcEndpointInfo info, CancellationToken token)
        {
            if (_config.Enabled)
            {
                var config = new AggregateSourceConfiguration(
                    //new GCSourceConfiguration(EventLevel.Verbose)
                    new MetricSourceConfiguration(metricIntervalSeconds: 1)
                    );
                config.RequestRundownOverride = false;

                var monitor = new DiagnosticsMonitor(config);
                var stream = await monitor.ProcessEvents(
                    new DiagnosticsClient(info.Endpoint),
                    Timeout.InfiniteTimeSpan,
                    CancellationToken.None);
                var task = Task.Run(() => StartTriggers(stream, monitor.CurrentProcessingTask));

                _data.Add(Tuple.Create(info.RuntimeInstanceCookie, monitor, task));
            }
        }

        private async Task StartTriggers(Stream stream, Task processingTask)
        {
            ThreadPool.GetMaxThreads(out int workerThreads, out int completionPortThreads);
            Debug.WriteLine("Worker Threads: " + workerThreads);
            Debug.WriteLine("Completion Threads: " + completionPortThreads);

            var eventSource = new EventPipeEventSource(stream);

            //eventSource.Clr.GCStart += Clr_GCStart;
            //eventSource.Clr.GCStop += Clr_GCStop;

            //eventSource.Clr.All += EventSource_AllEvents;
            eventSource.Dynamic.All += EventSource_AllEvents;

            eventSource.Process();

            Debug.WriteLine($"All events: {sw.ElapsedMilliseconds} ms");
            Debug.WriteLine($"AllocTick events: {swAllocTick.ElapsedMilliseconds} ms");

            await processingTask;
        }

        private Stopwatch sw = new Stopwatch();
        private Stopwatch swAllocTick = new Stopwatch();

        private void EventSource_AllEvents(TraceEvent obj)
        {
            sw.Start();
            //if (obj.OpcodeName == "AllocationTick")
            //{
            //    swAllocTick.Start();
                Debug.WriteLine(obj.ProviderName + "|" + obj.TaskName + "|" + obj.OpcodeName + "|" + obj.EventName + "|0x" + ((long)obj.Keywords).ToString("X16"));
                for (int i = 0; i < obj.PayloadNames.Length; i++)
                {
                    Debug.WriteLine("Payload[" + i + "," + obj.PayloadNames[i] + "] = " + obj.PayloadString(i));
                }
            //    Debug.WriteLine("ToString: " + obj.ToString());
            //    swAllocTick.Stop();
            //}
            sw.Stop();
        }

        private readonly IDictionary<long, DateTime> _map = new Dictionary<long, DateTime>();

        private void Clr_GCStop(Tracing.Parsers.Clr.GCEndTraceData obj)
        {
            DateTime startTimeStamp = _map[obj.Count];
            _map.Remove(obj.Count);
            Debug.WriteLine(obj.TimeStamp - startTimeStamp);
        }

        private void Clr_GCStart(Tracing.Parsers.Clr.GCStartTraceData obj)
        {
            _map.Add(obj.Count, obj.TimeStamp);
        }
    }
}
