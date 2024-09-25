// -----------------------------------------------------------------------
// <copyright file="SubscriberId.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace DurableSubscriptions.Shared;

public record struct SubscriberId(string Id);

public interface IWithSubscriberId
{
    SubscriberId SubscriberId { get; }
}