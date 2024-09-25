// -----------------------------------------------------------------------
// <copyright file="ProductId.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace DurableSubscriptions.Shared;

public interface IWithProductId
{
    ProductId ProductId { get; }
}

public record struct ProductId(string Id);