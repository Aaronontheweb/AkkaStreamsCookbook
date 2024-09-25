// -----------------------------------------------------------------------
// <copyright file="SubscriptionCommands.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Streams;

namespace DurableSubscriptions.Shared;

public sealed record DataPage(SubscriberId SubscriberId, NonZeroInt PageId, IReadOnlyList<IProductEvent> Events)
    : IWithSubscriberId;

public static class SubscriptionMessages
{
    public sealed record RunSubscription(
        SubscriberId SubscriberId,
        NonZeroInt RequestedPageSize,
        string[] Tags,
        IActorRef Sink) : IWithSubscriberId;


    public sealed record AckPage(SubscriberId SubscriberId, NonZeroInt PageId) : IWithSubscriberId;
}