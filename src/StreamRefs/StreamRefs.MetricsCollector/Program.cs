using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StreamRefs.MetricsCollector.Actors;

var hostBuilder = new HostBuilder();

hostBuilder.ConfigureServices((context, services) =>
{
    services.AddAkka("MetricsCollector", (builder, sp) =>
    {
        builder.ConfigureLoggers(loggers =>
        {
            loggers.ClearLoggers();
        })
        .WithMetricAggregator()
        .WithSpectreConsoleActor();
    });
});

var host = hostBuilder.Build();

var completionTask = host.RunAsync();
    
await completionTask; // wait for the host to shut down