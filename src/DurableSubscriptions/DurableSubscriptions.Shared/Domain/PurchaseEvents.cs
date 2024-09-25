// -----------------------------------------------------------------------
// <copyright file="PurchaseEvents.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace DurableSubscriptions.Shared;

public interface IProductEvent : IWithProductId{}


public static class ProductEvents
{
    public sealed record ProductPurchased(ProductId ProductId, int Quantity, double PricePerUnit) : IProductEvent;
    public sealed record ProductStocked(ProductId ProductId, int Quantity): IProductEvent;
}