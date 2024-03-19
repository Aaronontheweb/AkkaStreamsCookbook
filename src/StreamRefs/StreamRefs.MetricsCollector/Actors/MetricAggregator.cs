// -----------------------------------------------------------------------
// <copyright file="MetricAggregator.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
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
using Debug = System.Diagnostics.Debug;

namespace StreamRefs.MetricsCollector.Actors;

public static class MetricAggregatorExtensions
{
    public static AkkaConfigurationBuilder WithMetricAggregator(this AkkaConfigurationBuilder builder)
    {
        builder.WithActors((system, registry, resolver) =>
        {
            var metricAggregator = system.ActorOf(Props.Create(() => new MetricAggregator("metrics-collector")),
                "metric-aggregator");
            registry.Register<MetricAggregator>(metricAggregator);
        });
        return builder;
    }
}

public sealed class MetricAggregator : ReceiveActor
{
    private readonly CancellationTokenSource _cancellation = new();

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IMaterializer _materializer = Context.Materializer();

    private readonly SubscriberId _subscriberId;
    private Sink<MetricEvent, NotUsed> _finalSink;
    private ChannelReader<MetricEvent> _metricsReader;

    public MetricAggregator(string subscriberId)
    {
        _subscriberId = new SubscriberId(subscriberId);

        Receive<RequestMetricsFeed>(_ =>
        {
            _log.Debug("Received request for metrics feed from {0}", Sender);
            Debug.Assert(_metricsReader != null, nameof(_metricsReader) + " != null");
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
            .PreMaterialize(_materializer);

        // metrics reader will be used to provide a feed of metrics to the requesting actor
        _metricsReader = reader;

        // create a MergeHub that will be used to aggregate all of the various metrics
        _finalSink = MergeHub.Source<MetricEvent>(10)
            .Via(_cancellation.Token.AsFlow<MetricEvent>())
            .To(sink)
            .Run(_materializer);
    }

    protected override void PostStop()
    {
        // shut down the graph (which will shut down anyway since they're all children of this actor)
        _cancellation.Cancel();
    }

    public sealed class RequestMetricsFeed
    {
        private RequestMetricsFeed()
        {
        }

        public static RequestMetricsFeed Instance { get; } = new();
    }

    /// <summary>
    ///     Provides a feed of metrics to the requesting actor.
    /// </summary>
    /// <remarks>
    ///     Typically, only requested once by the process.
    /// </remarks>
    public sealed record MetricsFeed(ChannelReader<MetricEvent> Reader);
}