# DurableSubscriptions          

## Prerequisites

- Docker installed on your machine.
- Ensure that the `start_postgres.sh` script has been run to initialize the PostgreSQL container with the necessary database before starting the application.

## Getting Started

## Step 1: Run the PostgreSQL Docker Container

Before launching the application, you need to set up the PostgreSQL database that the application depends on. To do this, run the following command to execute the `start_postgres.sh` script:

```bash
./start_postgres.sh
```

This will launch a Postgres instance on host port `5433`. This is needed by the server in order to run [Akka.Persistence.Sql](https://github.com/akkadotnet/Akka.Persistence.Sql), which powers the projections  and persistence in this sample.

## Step 2: Run the Application

This application is compromised of two parts:

1. `DurableSubscriptions.Server` - this is an [Akka.Cluster](https://getakka.net/articles/clustering/cluster-overview.html) application that generates fake `IProductEvent` data representing product purchase and stock events. It uses [Akka.Persistence tagging](https://getakka.net/articles/persistence/persistence-query.html#eventsbytag-and-currenteventsbytag) make events exposed via product type. The server is also responsible for running the "durable subscription" backend, memorizing each unique subscriber's position in the Akka.Persistence journal.
2. `DurableSubscriptions.Client` - this is a simple console application that connects to the server and subscribes to the product events. It uses the [`ClusterClient`](https://getakka.net/articles/clustering/cluster-client.html) to connect to 1 or more `DurableSubscriptions.Server` instances in order to receive a historical and live change feed. Each time you reboot a client and restart it with the same id, the server will have the client pick up exactly where it left off previously. The client will continue to receive new events until it's terminated.

To run the server, execute the following command (from the directory of this file):

```bash
dotnet run --p project DurableSubscriptions.Server
```

To run the client, execute the following command (from the directory of this file):

```bash
dotnet run --p project DurableSubscriptions.Client -- -- subscribe {tag1,tag2,tag3} --subscriber-id {yourSubscriberId} [--page-size 10]
```

### Available Tags

The tags that have data available are:

* `product-oil`
* `product-gold`
* `product-silver`
* `data-copper`
* `data-platinum`

You can use any arbitrary tags you want, but these are the ones for which we automatically generate data in the server.