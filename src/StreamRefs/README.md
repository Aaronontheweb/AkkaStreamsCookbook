# StreamRefs

[`StreamRef`s are how you span Akka.Streams over the network in Akka.NET](https://getakka.net/articles/streams/streamrefs.html), using Akka.Remote and / or Akka.Cluster.

There are two important flavors of `StreamRef`s:

* `ISinkRef<T>` - a remote Akka.Streams `Sink<T>` that accepts inputs and streams them over the network using Akka.Streams semantics.
* `ISourceRef<T>` - a remote Akka.Streams `Source<T>` that produces outputs and stream them over the network.

## Running This Sample

This particular sample uses the following projects:

* `StreamRefs.MetricsCollector` - a centralized [Akka.Remote](https://getakka.net/articles/remoting/index.html) server that runs on `localhost:9912`. It accepts `ISourceRef<MetricEvent>`s from the `StreamRefs.Agent` application and aggregates all of the outputs together into a [Spectre.Console live table](https://spectreconsole.net/live/live-display) showing CPU utilization for each Agent node.
* `StreamRefs.Agent` - a stand-alone metrics "agent" process that gathers CPU utilization data using [https://github.com/devizer/Universe.CpuUsage](https://github.com/devizer/Universe.CpuUsage), converts the data into millicore representation, and then transmits it as a stream using a `ISourceRef<MetricEvent>` that gets pushed to the server.

To run this sample:

1. Start the `StreamRefs.MetricsCollector` project and then;
2. Start as many `StreamRefs.Agent` processes as you want.