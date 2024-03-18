// -----------------------------------------------------------------------
//  <copyright file="SpectreConsoleActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Channels;
using Akka.Actor;
using Spectre.Console;
using StreamRefs.Shared;

namespace StreamRefs.MetricsCollector.Actors;

/// <summary>
/// This actor is responsible for rendering the metrics to the console.
/// </summary>
public sealed class SpectreConsoleActor : ReceiveActor
{
    private readonly IActorRef _metricAggregator;
    private ChannelReader<MetricEvent>? _channelReader;

    private class Run
    {
        private Run()
        {
            
        }
        public static readonly Run Instance = new();
    }
    
    public SpectreConsoleActor(IActorRef metricAggregator)
    {
        _metricAggregator = metricAggregator;
        WaitingForMetrics();
    }
    
    private void WaitingForMetrics()
    {
        Receive<MetricAggregator.MetricsFeed>(feed =>
        {
            _channelReader = feed.Reader;
            Become(Running);
            Self.Tell(Run.Instance);
        });
    }
    
    private void Running()
    {
        Receive<Run>(feed =>
        {
            var table = new Table().Expand().BorderColor(Color.Grey);
            table.AddColumn("Address").AddColumn("Time").AddColumn("CPU");
            
            AnsiConsole.MarkupLine("Press [yellow]CTRL+C[/] to exit");
            AnsiConsole.Live(table)
                .AutoClear(true)
                .Overflow(VerticalOverflow.Ellipsis)
                .Cropping(VerticalOverflowCropping.Bottom)
                .StartAsync(async ctx =>
                {
                    await foreach(var c in _channelReader!.ReadAllAsync())
                    {
                        table.AddRow(c.NodeAddress.ToString(), c.TimeStamp.ToString(), c.Value.ToString());
                        ctx.Refresh();
                    }
                });
        });
    }

    protected override void PreStart()
    {
        // request the metrics feed from the aggregator
        _metricAggregator.Tell(MetricAggregator.RequestMetricsFeed.Instance);
    }
}