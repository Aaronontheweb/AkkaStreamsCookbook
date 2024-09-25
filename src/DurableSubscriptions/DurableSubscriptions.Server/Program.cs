using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Akka.Remote.Hosting;
using DurableSubscriptions.Server.Actors;
using DurableSubscriptions.Shared;
using Microsoft.Extensions.Hosting;

var hostBuilder = new HostBuilder();

hostBuilder.ConfigureServices((context, services) =>
{
    services.AddAkka("DurableSubs", (builder, sp) =>
    {
        builder.ConfigureLoggers(loggers => { loggers.ClearLoggers(); })
            .WithRemoting(new RemoteOptions { Port = 9914, HostName = "localhost" })
            .WithClustering(new ClusterOptions()
                { SeedNodes = ["akka.tcp://DurableSubs@localhost:9914"], Roles = ["subscriptions"] })
            .WithShardRegion<ProductInventoryActor>("products",
                s => Props.Create(() => new ProductInventoryActor(new ProductId(s))), 
                HashCodeMessageExtractor.Create(50, EntityIdExtractor), new ShardOptions()
                {
                    StateStoreMode = StateStoreMode.DData,
                    Role = "subscriptions"
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