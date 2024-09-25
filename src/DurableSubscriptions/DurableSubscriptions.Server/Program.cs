using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Microsoft.Extensions.Hosting;

var hostBuilder = new HostBuilder();

hostBuilder.ConfigureServices((context, services) =>
{
    services.AddAkka("DurableSubs", (builder, sp) =>
    {
        builder.ConfigureLoggers(loggers => { loggers.ClearLoggers(); })
            .WithRemoting(new RemoteOptions { Port = 9914, HostName = "localhost" })
            .WithClustering(new ClusterOptions() { SeedNodes = ["akka.tcp://DurableSubs@localhost:9914"], Roles = ["subscriptions"]});
    });
});

var host = hostBuilder.Build();

var completionTask = host.RunAsync();

await completionTask; // wait for the host to shut down