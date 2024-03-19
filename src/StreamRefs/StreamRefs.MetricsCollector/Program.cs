// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Hosting;
using Akka.Remote.Hosting;
using Microsoft.Extensions.Hosting;
using StreamRefs.MetricsCollector.Actors;

var hostBuilder = new HostBuilder();

hostBuilder.ConfigureServices((context, services) =>
{
    services.AddAkka("MetricsCollector", (builder, sp) =>
    {
        builder.ConfigureLoggers(loggers => { loggers.ClearLoggers(); })
            .WithRemoting(new RemoteOptions { Port = 9912, HostName = "localhost" })
            .WithMetricAggregator()
            .WithSpectreConsoleActor();
    });
});

var host = hostBuilder.Build();

var completionTask = host.RunAsync();

await completionTask; // wait for the host to shut down