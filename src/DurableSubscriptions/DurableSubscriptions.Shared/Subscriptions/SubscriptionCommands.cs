// -----------------------------------------------------------------------
// <copyright file="SubscriptionCommands.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace DurableSubscriptions.Shared;

public static class SubscriptionMessages
{
    public sealed record RunSubscription(SubscriberId SubscriberId, NonZeroInt RequestedPageSize) : IWithSubscriberId;
    
    public sealed record DataPage(SubscriberId SubscriberId, NonZeroInt PageId, IReadOnlyList<IProductEvent> Events) : IWithSubscriberId;

    public sealed record AckPage(SubscriberId SubscriberId, NonZeroInt PageId) : IWithSubscriberId;
}