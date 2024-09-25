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
using DurableSubscriptions.Shared;

namespace DurableSubscriptions.Server.Actors;

public sealed class SubscriberActor : UntypedPersistentActor
{
    public override string PersistenceId { get; }
    private readonly IMaterializer _mat = Context.Materializer();
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private CancellationTokenSource? _subscriptionCancellation;
    private IActorRef? _remoteSubscriber;
    
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
        CombineSources(sources);
    }

    public static Source<T, NotUsed> CombineSources<T>(List<Source<T, NotUsed>> sources)
    {
        var combinedSource = sources.Count switch
        {
            0 => Source.Empty<T>(),
            1 => sources[0],
            _ => Source.Combine(sources[0], sources[1], i => new Merge<T, T>(i), sources.Skip(2).ToArray())
        };

        return combinedSource;
    }

    /// <summary>
    /// Occurs after the client subscribes to the stream.
    /// </summary>
    private void RunningSubscription(object message)
    {
        
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
}

public sealed record DataPageStructure(Dictionary<string, Offset> OffsetsPerTag, NonZeroInt PageId);

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