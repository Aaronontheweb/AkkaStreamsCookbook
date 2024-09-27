// -----------------------------------------------------------------------
// <copyright file="SubscriberState.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using Akka.Persistence.Query;
using DurableSubscriptions.Shared;

namespace DurableSubscriptions.Server.Actors;

/// <summary>
/// This gets persisted to the journal and represents the current state of the subscriber.
/// </summary>
/// <param name="SubscriberId">The subscriber id</param>
public sealed record SubscriberState(SubscriberId SubscriberId)
{
    public NonZeroInt PageSize { get; init; } = new(10);
    
    public Dictionary<string, Offset> OffsetsPerTag { get; init; } = new();
}

public sealed record DataPageStructure(
    SubscriberId SubscriberId,
    Dictionary<string, Offset> OffsetsPerTag,
    List<(long offset, IProductEvent e)> Events,
    NonZeroInt PageId): IWithSubscriberId
{
    public DataPage ToDataPage() => new(SubscriberId, PageId, Events);
} 

public static class SubscriberStateExtensions
{
    public static SubscriberState Apply(this SubscriberState state, SubscriptionMessages.RunSubscription run)
    {
        var tags = run.Tags;
        var pageSize = run.RequestedPageSize;

        // update the subscription state with the new page size
        // and add any new tags to the list of tags we're tracking
        var removedTags = state.OffsetsPerTag.Keys.Except(tags).ToList();
        var addedTags = tags.Except(state.OffsetsPerTag.Keys).ToList();
        
        // remove old tags, add new ones
        var newOffsets = state.OffsetsPerTag
            .Where(x => !removedTags.Contains(x.Key))
            .Concat(addedTags.Select(x => new KeyValuePair<string, Offset>(x, Offset.NoOffset())))
            .ToDictionary();
        
        state = state with {PageSize = pageSize, OffsetsPerTag = newOffsets};

        return state;
    }
    
    public static SubscriberState Apply(this SubscriberState state, DataPageStructure page)
    {
        // need to merge the offsets from the data page into the current state
        // there's a good chance not every page will have every tag
        var newOffsets = state.OffsetsPerTag
            .Select(x =>
            {
                if (page.OffsetsPerTag.TryGetValue(x.Key, out var newOffset))
                {
                    return new KeyValuePair<string, Offset>(x.Key, newOffset);
                }

                return x;
            })
            .ToDictionary();
        return state with {OffsetsPerTag = newOffsets};
    }
}