using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util.Internal;
using StreamRefs.Shared;
using Debug = System.Diagnostics.Debug;

namespace StreamRefs.Agent.Actors;

public class MetricAggregator : ReceiveActor, IWithStash, IWithTimers
{
    private readonly Address _metricsCollectorAddress;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly NodeAddress _nodeAddress;
    private readonly IMaterializer _materializer = ActorMaterializer.Create(Context);
    public IStash Stash { get; set; }
    public ITimerScheduler Timers { get; set; }

    // our Source we're going to use to stream metrics to the collector
    private IActorRef? _metricSource;
    
    private class DoConnect
    {
        private DoConnect()
        {
        }

        public static DoConnect Instance { get; } = new();
    }

    public MetricAggregator(Address metricsCollectorAddress)
    {
        _metricsCollectorAddress = metricsCollectorAddress;
        var addr = Context.System.AsInstanceOf<ExtendedActorSystem>().Provider.DefaultAddress;
        
        Debug.Assert(addr.Port != null, "addr.Port != null");
        _nodeAddress = new NodeAddress(addr.Host, addr.Port.Value);
        
        Self.Tell(DoConnect.Instance);
        Connecting();
    }

    private void Connecting()
    {
        Receive<DoConnect>(_ =>
        {
            if(_metricSource != null)
            {
                // kill the old stream
               Context.Stop(_metricSource);
               _metricSource = null;
            }
            
            _log.Info("Connecting to metrics collector at {0}", _metricsCollectorAddress);
            MessageServer(new MetricCommands.PingServer(_nodeAddress));
            Timers.StartSingleTimer("connect-timeout", DoConnect.Instance, TimeSpan.FromSeconds(5));
        });
        
        ReceiveAsync<MetricCommands.SubscribeToMetrics>(async response =>
        {
            _log.Info("Connected to metrics collector at {0}", _metricsCollectorAddress);

            var (t, metricsSink) = Akka.Streams.Dsl.StreamRefs.SourceRef<MetricEvent>()
                .PreMaterialize(_materializer);

            var sourceRef = await t;
            
            var (metricsRef, source) = Source.ActorRef<MetricEvent>(100, OverflowStrategy.DropHead)
                .PreMaterialize(_materializer);
            _metricSource = metricsRef;
            
            // connect the source and sink
            source
                .RunWith(metricsSink, _materializer);
            
            _log.Info("Attempting to push metrics to {0}", _metricsCollectorAddress);
            
            MessageServer(new MetricCommands.PushMetrics(response.SubscriberId, _nodeAddress, sourceRef));
        });
        
        Receive<MetricCommands.ReceivingMetrics>(response =>
        {
            _log.Info("Pushing metrics to collector at {0}", _metricsCollectorAddress);
            Become(Connected);
            Stash.UnstashAll();
            Timers.Cancel("connect-timeout");

            // actor will die if stream terminates
            Context.Watch(_metricSource);
        });
        
        ReceiveAny(_ => Stash.Stash());
    }

    private void Connected()
    {
        Receive<CpuCollector.CpuUpdate>(update =>
        {
            _log.Debug("Received CPU update: {0}", update.CpuUsage);
            _metricSource?.Tell(new MetricEvent(_nodeAddress, update.TimeStamp, MetricMeasure.Cpu, update.CpuUsage));
        });

        Receive<Terminated>(t =>
        {
            _log.Warning("Metrics source terminated, reconnecting...");
            Self.Tell(DoConnect.Instance);
            Become(Connecting);
        });
    }

    private void MessageServer(object msg)
    {
        var selection = Context.ActorSelection(_metricsCollectorAddress + "/user/metric-aggregator");
        selection.Tell(msg);
    }
}