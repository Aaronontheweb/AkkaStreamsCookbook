// -----------------------------------------------------------------------
//  <copyright file="MetricAggregator.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Channels;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using StreamRefs.Shared;

namespace StreamRefs.MetricsCollector.Actors;

public static class MetricAggregatorExtensions
{
    public static AkkaConfigurationBuilder WithMetricAggregator(this AkkaConfigurationBuilder builder)
    {
        builder.WithActors((system, registry, resolver) =>
        {
            var metricAggregator = system.ActorOf(Props.Create(() => new MetricAggregator("metrics-collector")), "metric-aggregator");
            registry.Register<MetricAggregator>(metricAggregator);
        });
        return builder;
    }
}

public sealed class MetricAggregator : ReceiveActor
{
    public sealed class RequestMetricsFeed
    {
        private RequestMetricsFeed()
        {
        }

        public static RequestMetricsFeed Instance { get; } = new();
    }

    /// <summary>
    /// Provides a feed of metrics to the requesting actor.
    /// </summary>
    /// <remarks>
    /// Typically, only requested once by the process.
    /// </remarks>
    public sealed record MetricsFeed(ChannelReader<MetricEvent> Reader);

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IMaterializer _materializer = ActorMaterializer.Create(Context);
    private readonly CancellationTokenSource _cancellation = new();
    private ChannelReader<MetricEvent> _metricsReader;
    private Sink<MetricEvent, NotUsed> _finalSink;

    private readonly SubscriberId _subscriberId;

    public MetricAggregator(string subscriberId)
    {
        _subscriberId = new SubscriberId(subscriberId);

        Receive<RequestMetricsFeed>(_ =>
        {
            _log.Debug("Received request for metrics feed from {0}", Sender);
            Sender.Tell(new MetricsFeed(_metricsReader));
        });
        
        Receive<MetricCommands.PingServer>(p =>
        {
            _log.Info("Received ping from {0}", p.MyAddress);
            Sender.Tell(new MetricCommands.SubscribeToMetrics(_subscriberId, p.MyAddress));
        });
        
        Receive<MetricCommands.PushMetrics>(p =>
        {
            _log.Info("Received metrics from {0}", p.NodeAddress);
            
            // run the streamed metrics into the final sink
            p.MetricsSource.Source.RunWith(_finalSink, _materializer);
            
            Sender.Tell(new MetricCommands.ReceivingMetrics(_subscriberId, p.NodeAddress));
        });
    }

    protected override void PreStart()
    {
        // create an Akka.Streams MergeHub that will be used to aggregate all of the various metrics
        var (reader, sink) = ChannelSink.AsReader<MetricEvent>(100, true, BoundedChannelFullMode.DropOldest)
            .PreMaterialize(Context.Materializer());

        // metrics reader will be used to provide a feed of metrics to the requesting actor
        _metricsReader = reader;

        // create a MergeHub that will be used to aggregate all of the various metrics
        _finalSink = MergeHub.Source<MetricEvent>(perProducerBufferSize: 10)
            .Via(_cancellation.Token.AsFlow<MetricEvent>())
            .To(sink)
            .Run(_materializer);
    }
}