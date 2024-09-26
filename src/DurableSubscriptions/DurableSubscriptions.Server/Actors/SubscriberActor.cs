// -----------------------------------------------------------------------
// <copyright file="SubscriberActor.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Streams.Implementation;
using Akka.Util.Internal;
using DurableSubscriptions.Shared;

namespace DurableSubscriptions.Server.Actors;

public sealed class SubscriberActor : UntypedPersistentActor, IWithTimers
{
    public override string PersistenceId { get; }
    private readonly IMaterializer _mat = Context.Materializer();
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private CancellationTokenSource? _subscriptionCancellation;
    private IActorRef? _remoteSubscriber;
    
    private AtomicCounter _pageId = new AtomicCounter(0);
    public SubscriberState State { get; private set; } 
    
    public SubscriberActor(SubscriberId subscriberId)
    {
        PersistenceId = $"subscriber-{subscriberId.Id}";
        State = new SubscriberState(subscriberId);
    }
    
    protected override void OnCommand(object message)
    {
        switch (message)
        {
            case SubscriptionMessages.RunSubscription run:
            {
                HandleRun(run);
                break;
            }
            case Completed:
            {
                // ignore
                break;
            }
        }
    }

    private void HandleRun(SubscriptionMessages.RunSubscription run)
    {
        Context.Watch(_remoteSubscriber); // if they die or the connection does, we'll reset
        _remoteSubscriber = run.Sink;
        _subscriptionCancellation = new CancellationTokenSource();
        
        // update our state
        State = State.Apply(run);
        
        var readJournal = PersistenceQuery.Get(Context.System)
            .ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
        var self = Self;
        
        // for each tag, we need to start an EventsByTag query
        var sources = State.OffsetsPerTag.Select(c => readJournal.EventsByTag(c.Key, c.Value)).ToList();
        
        // merge the sources together
        var combined = StreamsHelper.CombineSources(sources);
        combined
            .Via(_subscriptionCancellation.Token.AsFlow<EventEnvelope>())
            .GroupedWithin(State.PageSize.Value, TimeSpan.FromSeconds(10))
            .Select(c => CreateDataPage(c.ToList(), _pageId))
            .RunWith(Sink.ActorRefWithAck<DataPageStructure>(self, Start.Instance, PageAck.Instance, Completed.Instance,
                ex => new Status.Failure(ex)), _mat);
        
        Become(RunningSubscription);
    }
    
    /// <summary>
    /// Occurs after the client subscribes to the stream.
    /// </summary>
    private void RunningSubscription(object message)
    {
        switch (message)
        {
            case DataPageStructure page:
            {
                Become(PendingPageAck(page));
                
                _remoteSubscriber.Tell(page);
                break;
            }
        }
    }

    private Receive PendingPageAck(DataPageStructure currentPage)
    {
        return s =>
        {
            switch (s)
            {
                default:
                    return false;
            }
        };
    }

    protected override void OnRecover(object message)
    {
        switch (message)
        {
            case SnapshotOffer { Snapshot: SubscriberState state }:
                State = state;
                break;
            case SubscriberState state:
                State = state;
                break;
        }
    }

    private sealed class Start
    {
        public static readonly Start Instance = new();
        private Start()
        {
        }
    }
    
    private sealed class PageAck
    {
        public static readonly PageAck Instance = new();
        private PageAck()
        {
        }
    }
    
    private sealed class Completed : IDeadLetterSuppression
    {
        public static readonly Completed Instance = new();
        private Completed()
        {
        }
    }

    private sealed record AckTimeout(int RetryCount, int MaxRetries);
    
    public static DataPageStructure CreateDataPage(IReadOnlyList<EventEnvelope> events, AtomicCounter pageIdCounter)
    {
        // grab the largest offset per tag - bearing in mind there can be multiple tags per event
        var tagData = new Dictionary<string, Offset>();
        foreach(var e in events)
        {
            foreach (var t in e.Tags)
            {
                if(tagData.TryGetValue(t, out var current))
                {
                    if(e.Offset.CompareTo(current) > 0)
                        tagData[t] = e.Offset;
                }
                else
                {
                    tagData[t] = e.Offset;
                }
            }
        }
        
        // ok, now filter all the events, so we include only the IProductEvent
        var productEvents = events.Select(e => e.Event).OfType<IProductEvent>().ToList();
        
        return new DataPageStructure(tagData, productEvents, new NonZeroInt(pageIdCounter.IncrementAndGet()));
    }

    public ITimerScheduler Timers { get; set; } = null!;
}