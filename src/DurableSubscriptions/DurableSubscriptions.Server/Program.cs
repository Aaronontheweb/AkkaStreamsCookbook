using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Hosting;
using Akka.Remote.Hosting;
using DurableSubscriptions.Server.Actors;
using DurableSubscriptions.Shared;
using LinqToDB;
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

hostBuilder.ConfigureLogging(builder =>
{
    builder.AddConsole();
});

hostBuilder.ConfigureServices((context, services) =>
{
    var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
    
    services.AddAkka("DurableSubs", (builder, sp) =>
    {
        builder.ConfigureLoggers(c =>
            {
                c.ClearLoggers();
                c.AddLoggerFactory();
                c.LogLevel = Akka.Event.LogLevel.InfoLevel;
            })
            .WithRemoting(new RemoteOptions { Port = 9914, HostName = "localhost" })
            .WithClustering(new ClusterOptions()
                { SeedNodes = ["akka.tcp://DurableSubs@localhost:9914"], Roles = ["subscriptions"] })
            .WithSqlPersistence(connectionString, ProviderName.PostgreSQL, tagStorageMode:TagMode.TagTable)
            .WithShardRegion<ProductInventoryActor>("products",
                s => Props.Create(() => new ProductInventoryActor(new ProductId(s))),
                HashCodeMessageExtractor.Create(50, EntityIdExtractor), new ShardOptions()
                {
                    StateStoreMode = StateStoreMode.DData,
                    Role = "subscriptions"
                })
            .WithActors((system, registry, resolver) =>
            {
                // populate some data
                var props = resolver.Props<ProductEventGenerator>();
                var generator = system.ActorOf(props, "event-generator");
            });
        return;

        string? EntityIdExtractor(object arg)
        {
            if(arg is IWithProductId withProductId)
            {
                return withProductId.ProductId.Id;
            }

            return null;
        }
    });
});

var host = hostBuilder.Build();

var completionTask = host.RunAsync();

await completionTask; // wait for the host to shut down