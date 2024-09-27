// See https://aka.ms/new-console-template for more information

using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Cluster.Tools.Client;
using Akka.Hosting;
using Akka.Remote.Hosting;
using DurableSubscriptions.Client.Actors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

var hostBuilder = new HostBuilder();

hostBuilder.ConfigureAppConfiguration((context, builder) =>
{
    builder
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{environment}.json", true)
        .AddEnvironmentVariables();
});

hostBuilder.ConfigureLogging(builder => { builder.AddConsole(); });

hostBuilder.ConfigureServices((context, services) =>
{
    // extract the initial contact points from config
    var initialContacts = context.Configuration.GetSection("Akka:ClusterClientSettings:IntialContacts")
        .Get<string[]>()
        .Select(Address.Parse)
        .ToArray();
    
    // if the initial contacts is empty, throw an exception
    if (initialContacts.Length == 0)
    {
        throw new InvalidOperationException("No initial contacts were provided in the configuration.");
    }
    
    services.AddAkka("DurableSubs", (builder, sp) =>
    {
        builder.ConfigureLoggers(c =>
            {
                c.ClearLoggers();
                c.AddLoggerFactory();
                c.LogLevel = Akka.Event.LogLevel.InfoLevel;
            })
            .WithRemoting(new RemoteOptions { Port = 0, HostName = "localhost" })
            .WithClusterClient<ClusterClient>(initialContacts);
    });
});

var host = hostBuilder.Build();

var completionTask = host.RunAsync();

await completionTask; // wait for the host to shut down