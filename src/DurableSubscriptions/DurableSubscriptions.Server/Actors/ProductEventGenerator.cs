// -----------------------------------------------------------------------
// <copyright file="ProductEventGenerator.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using DurableSubscriptions.Shared;
using static DurableSubscriptions.Server.DataSeeding.EventGenerator;

namespace DurableSubscriptions.Server.Actors;

/// <summary>
/// Periodically generates product events for the system.
/// </summary>
public class ProductEventGenerator : UntypedActor, IWithTimers
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IMaterializer _materializer = Context.Materializer();
    private readonly IActorRef _productInventoryActors;

    public ProductEventGenerator(IRequiredActor<ProductInventoryActor> productInventoryActors)
    {
        _productInventoryActors = productInventoryActors.ActorRef;
    }

    protected override void OnReceive(object message)
    {
        switch (message)
        {
            case DoEvents:
            {
                GenerateFakeEventStream();
                break;
            }
            case IProductEvent e:
            {
                // forward the event 
                _productInventoryActors.Tell(e);
                break;
            }
            case StreamCompleted c:
            {
                _log.Info("Processed {0} events", c.Count);
                break;
            }
            case Status.Failure f:
            {
                _log.Error(f.Cause, "Stream failed");
                break;
            }
        }
    }

    protected override void PreStart()
    {
        Timers.StartPeriodicTimer("generate-events", DoEvents.Instance, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    private void GenerateFakeEventStream()
    {
        var events = GenerateFakeEvents(Random.Shared.Next(10, 100)).ToList();
        var sink = Sink.ActorRef<IProductEvent>(Self, new StreamCompleted(events.Count), ex => new Status.Failure(ex));
        var src = Source.From(events)
            .Throttle(5, TimeSpan.FromSeconds(1), 10, ThrottleMode.Shaping)
            .RunWith(sink, _materializer);
    }

    public sealed class StreamCompleted(int count)
    {
        public int Count { get; } = count;
    }
    
    public sealed class DoEvents
    {
        public static readonly DoEvents Instance = new DoEvents();
        private DoEvents(){}
    }

    public ITimerScheduler Timers { get; set; } = null!;
}