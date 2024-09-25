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
using Akka.Util.Internal;
using DurableSubscriptions.Shared;

namespace DurableSubscriptions.Server.Actors;

public sealed class SubscriberActor : UntypedPersistentActor
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
    
    private static DataPageStructure CreateDataPage(IReadOnlyList<EventEnvelope> events, AtomicCounter pageIdCounter)
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
                        tagData[e.PersistenceId] = e.Offset;
                }
                else
                {
                    tagData[e.PersistenceId] = e.Offset;
                }
            }
        }
        
        // ok, now filter all the events, so we include only the IProductEvent
        var productEvents = events.Select(e => e.Event).OfType<IProductEvent>().ToList();
        
        return new DataPageStructure(tagData, productEvents, new NonZeroInt(pageIdCounter.GetAndIncrement()));
    }
}

public sealed record DataPageStructure(Dictionary<string, Offset> OffsetsPerTag, List<IProductEvent> Events, NonZeroInt PageId);

/// <summary>
/// This gets persisted to the journal and represents the current state of the subscriber.
/// </summary>
/// <param name="SubscriberId">The subscriber id</param>
public sealed record SubscriberState(SubscriberId SubscriberId)
{
    public NonZeroInt PageSize { get; init; } = new NonZeroInt(10);
    
    public Dictionary<string, Offset> OffsetsPerTag { get; init; } = new Dictionary<string, Offset>();
}

public static class SubscriberStateExtensions
{
    public static SubscriberState Apply(this SubscriberState state, SubscriptionMessages.RunSubscription run)
    {
        var tags = run.Tags;
        var pageSize = run.RequestedPageSize;
        var subscriberId = run.SubscriberId;
        
        // update the subscription state with the new page size
        // and add any new tags to the list of tags we're tracking
        var removedTags = state.OffsetsPerTag.Keys.Except(tags).ToImmutableList();
        var addedTags = tags.Except(state.OffsetsPerTag.Keys).ToImmutableList();
        
        // remove old tags, add new ones
        var newOffsets = state.OffsetsPerTag
            .Where(x => !removedTags.Contains(x.Key))
            .Concat(addedTags.Select(x => new KeyValuePair<string, Offset>(x, Offset.NoOffset())))
            .ToDictionary();
        
        state = state with {PageSize = pageSize, OffsetsPerTag = newOffsets};

        return state;
    }
}