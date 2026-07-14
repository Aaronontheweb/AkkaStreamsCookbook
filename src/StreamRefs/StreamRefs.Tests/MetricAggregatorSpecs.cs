using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Akka.Streams;
using Akka.Streams.Dsl;
using StreamRefs.MetricsCollector.Actors;
using StreamRefs.Shared;

namespace StreamRefs.Tests;

public class MetricAggregatorSpecs : TestKit
{
    public MetricAggregatorSpecs(ITestOutputHelper helper) : base(output:helper)
    {
    }
    
    [Fact]
    public async Task ShouldReceiveMetricAggregatorEvents()
    {
        // arrange
        var aggregator = await ActorRegistry.GetAsync<MetricAggregator>(TestContext.Current.CancellationToken);
        var metricEvents = new List<MetricEvent>()
        {
            new MetricEvent(new NodeAddress("localhost", 2001), DateTime.UtcNow.Ticks, MetricMeasure.Cpu, 0.5),
            new MetricEvent(new NodeAddress("localhost", 2002), DateTime.UtcNow.Ticks, MetricMeasure.Cpu, 0.5)
        };

        var simpleSource = Source.From(metricEvents);
        var (sourceRefTask, sink) = Akka.Streams.Dsl.StreamRefs.SourceRef<MetricEvent>()
                .PreMaterialize(Sys.Materializer());
        
        // publish the metric events into the sink
        simpleSource.RunWith(sink, Sys.Materializer());
        
        // simulate a remote SourceRef
        var sourceRef = await sourceRefTask;
        
        // act
        aggregator.Tell(MetricAggregator.RequestMetricsFeed.Instance, TestActor);
        var metricFeed = await ExpectMsgAsync<MetricAggregator.MetricsFeed>(cancellationToken: TestContext.Current.CancellationToken);

        aggregator.Tell(new MetricCommands.PushMetrics(new SubscriberId("fake"), new NodeAddress("localhost", 2001), sourceRef));
        await ExpectMsgAsync<MetricCommands.ReceivingMetrics>(cancellationToken: TestContext.Current.CancellationToken);
        
        // assert
        // we should be able to receive the same events from the aggregator
        var actualEvents = new List<MetricEvent>();
        var reader = metricFeed.Reader;
        
        // read two events only (channel does not terminate)
        for (var i = 0; i < 2; i++)
        {
            var next = await reader.ReadAsync(TestContext.Current.CancellationToken);
            actualEvents.Add(next);
        }
        
        Assert.Equivalent(metricEvents, actualEvents);
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithMetricAggregator();
    }
}