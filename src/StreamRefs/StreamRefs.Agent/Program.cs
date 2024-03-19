// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Microsoft.Extensions.Hosting;
using StreamRefs.Agent.Actors;

var hostBuilder = new HostBuilder();

// TODO: parse from appSettings.json
var serverAddress = Address.Parse("akka.tcp://MetricsCollector@localhost:9912");

hostBuilder.ConfigureServices((context, services) =>
{
    services.AddAkka("MetricsAgent", (builder, sp) =>
    {
        builder
            .WithRemoting(new RemoteOptions { Port = 0, HostName = "localhost" })
            .WithActors((system, registry) =>
            {
                var metricAggregator = system.ActorOf(Props.Create(() => new MetricAggregator(serverAddress)),
                    "metric-aggregator");
                registry.Register<MetricAggregator>(metricAggregator);

                var cpuCollector =
                    system.ActorOf(Props.Create(() => new CpuCollector(metricAggregator)), "cpu-collector");
            });
    });
});

var host = hostBuilder.Build();

await host.RunAsync(); // wait for the host to shut down