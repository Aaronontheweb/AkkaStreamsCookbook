﻿// -----------------------------------------------------------------------
// <copyright file="SpectreConsoleActor.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Channels;
using Akka.Actor;
using Akka.Hosting;
using Spectre.Console;
using StreamRefs.Shared;

namespace StreamRefs.MetricsCollector.Actors;

public record struct MetricData
{
    public double Cpu { get; init; }

    public DateTime LastUpdated { get; init; }
}

public static class SpectreConsoleActorExtensions
{
    public static AkkaConfigurationBuilder WithSpectreConsoleActor(this AkkaConfigurationBuilder builder)
    {
        builder.WithActors(async (system, registry, resolver) =>
        {
            var metricAggregator = await registry.GetAsync<MetricAggregator>();
            var consoleActor = system.ActorOf(Props.Create(() => new SpectreConsoleActor(metricAggregator)),
                "spectre-console");
        });
        return builder;
    }
}

/// <summary>
///     This actor is responsible for rendering the metrics to the console.
/// </summary>
public sealed class SpectreConsoleActor : ReceiveActor
{
    private readonly IActorRef _metricAggregator;
    private ChannelReader<MetricEvent>? _channelReader;

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
            var table = new Table().Centered().BorderColor(Color.Grey);
            table.AddColumn("Address").AddColumn("Last Update").AddColumn("CPU");

            AnsiConsole.MarkupLine("Press [yellow]CTRL+C[/] to exit");
            AnsiConsole.Live(table)
                .AutoClear(true)
                .Overflow(VerticalOverflow.Ellipsis)
                .Cropping(VerticalOverflowCropping.Bottom)
                .StartAsync(async ctx =>
                {
                    Dictionary<NodeAddress, MetricData> metrics = new();

                    table.AddEmptyRow();
                    ctx.Refresh();

                    await foreach (var c in _channelReader!.ReadAllAsync())
                    {
                        ProcessEvent(c, metrics);
                        ProcessTable(table, metrics);
                        
                        ctx.Refresh();

                        while(!_channelReader.TryPeek(out _))
                        {
                            // channel reader won't push events if everyone is dead - need
                            // to keep updating data table anyway
                            await Task.Delay(TimeSpan.FromSeconds(2));
                            
                            ProcessTable(table, metrics);
                        
                            ctx.Refresh();
                        }
                    }
                });
        });
        return;

        void ProcessEvent(in MetricEvent c, Dictionary<NodeAddress, MetricData> metrics)
        {
            // convert c.Timestamp from ticks to DateTime
            var timestamp = new DateTime(c.TimeStamp);
            if (!metrics.TryGetValue(c.Node, out var nodeData))
            {
                nodeData = new MetricData();
            }

            switch (c.Measure)
            {
                case MetricMeasure.Cpu:
                    nodeData = nodeData with { Cpu = c.Value };
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            nodeData = nodeData with { LastUpdated = timestamp };
            metrics[c.Node] = nodeData;
        }

        void ProcessTable(Table table, Dictionary<NodeAddress, MetricData> metrics)
        {
            table.Rows.Clear();

            // if we've had any nodes without an update in the last 60 seconds, remove them
            var now = DateTime.UtcNow;
            var toRemove = metrics.Where(x => (now - x.Value.LastUpdated).TotalSeconds > 60).ToList();
            foreach (var (node, _) in toRemove) metrics.Remove(node);

            foreach (var (node, data) in metrics)
            {
                // pick whichever timestamp is higher - the last update or the last time we received a message
                            
                // format data.Cpu into a string with only up to 2 numbers after decimal point
                table.AddRow($"{node.Host}:{node.Port}", data.LastUpdated.PrettyPrint(),
                    $"{data.Cpu:F2} mc");
            }
        }
    }

    protected override void PreStart()
    {
        // request the metrics feed from the aggregator
        _metricAggregator.Tell(MetricAggregator.RequestMetricsFeed.Instance);
    }

    private class Run
    {
        public static readonly Run Instance = new();

        private Run()
        {
        }
    }
}