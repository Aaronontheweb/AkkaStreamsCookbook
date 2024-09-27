using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Cluster.Tools.Client;
using Akka.Hosting;
using Akka.Remote.Hosting;
using DurableSubscriptions.Client.Cli;
using Spectre.Console.Cli;
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
    var initialContacts = context.Configuration.GetSection("Akka:ClusterClientSettings:InitialContacts")
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


// Create the type registrar for Spectre.Console
var registrar = new TypeRegistrar(hostBuilder);

// Set up Spectre.Console CommandApp with DI
var app = new CommandApp(registrar);
app.Configure(config =>
{
    config.AddCommand<SubscribeCommand>("subscribe"); 
});

await app.RunAsync(Environment.GetCommandLineArgs().Skip(1));