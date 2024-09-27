using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Akka.Cluster.Tools.Client;
using Akka.Hosting;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Hosting;
using Akka.Remote.Hosting;
using DurableSubscriptions.Server.Actors;
using DurableSubscriptions.Server.Persistence;
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

hostBuilder.ConfigureLogging(builder => { builder.AddConsole(); });

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
            .WithSqlPersistence(connectionString, ProviderName.PostgreSQL, tagStorageMode: TagMode.TagTable,
                journalBuilder: (j) =>
                    j.AddWriteEventAdapter<ProductEventsTagger>("product-events-tagger",
                        new[] { typeof(IProductEvent) }))
            .WithShardRegion<ProductInventoryActor>("products",
                s => Props.Create(() => new ProductInventoryActor(new ProductId(s))),
                HashCodeMessageExtractor.Create(50, EntityIdExtractor), new ShardOptions()
                {
                    StateStoreMode = StateStoreMode.DData,
                    Role = "subscriptions"
                })
            .WithActors((system, _, resolver) =>
            {
                // populate some data
                var props = resolver.Props<ProductEventGenerator>();
                var generator = system.ActorOf(props, "event-generator");
            })
            .WithShardRegion<SubscriberActor>("subscriptions",
                s => Props.Create(() => new SubscriberActor(new SubscriberId(s))),
                HashCodeMessageExtractor.Create(50, SubscriberIdExtractor), new ShardOptions()
                {
                    StateStoreMode = StateStoreMode.DData,
                    Role = "subscriptions",
                    ShouldPassivateIdleEntities = false
                })
            .WithClusterClientReceptionist(role:"subscriptions")
            .AddStartup((system, registry) =>
            {
                // register the subscriber actor shardRegion
                var receptionist = ClusterClientReceptionist.Get(system);
                
                // this will register the /system/sharding/subscriptions path
                receptionist.RegisterService(registry.Get<SubscriberActor>());
            });
        return;

        string? EntityIdExtractor(object arg)
        {
            if (arg is IWithProductId withProductId)
            {
                return withProductId.ProductId.Id;
            }

            return null;
        }
        
        string? SubscriberIdExtractor(object arg)
        {
            if (arg is IWithSubscriberId withSubscriberId)
            {
                return withSubscriberId.SubscriberId.Id;
            }

            return null;
        }
    });
});

var host = hostBuilder.Build();

var completionTask = host.RunAsync();

await completionTask; // wait for the host to shut down