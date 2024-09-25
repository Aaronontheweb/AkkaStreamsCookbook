// -----------------------------------------------------------------------
// <copyright file="EventsGenerator.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Bogus;
using DurableSubscriptions.Shared;

namespace DurableSubscriptions.Server.DataSeeding;

using static DomainConstants; // Importing the static members of DomainConstants

public static class EventGenerator
{
    public static IEnumerable<IProductEvent> GenerateFakeEvents(int numberOfEvents)
    {
        // Create a faker for ProductPurchased event
        var productPurchasedFaker = new Faker<ProductEvents.ProductPurchased>()
            .CustomInstantiator(f =>
            {
                var productId = new ProductId(f.PickRandom(Products));
                var quantity = f.Random.Int(1, 100); // Random quantity between 1 and 100
                var pricePerUnit = f.Random.Double(10.0, 1000.0); // Random price between 10 and 1000
                return new ProductEvents.ProductPurchased(productId, quantity, pricePerUnit);
            });

        // Create a faker for ProductStocked event
        var productStockedFaker = new Faker<ProductEvents.ProductStocked>()
            .CustomInstantiator(f =>
            {
                var productId = new ProductId(f.PickRandom(Products));
                var quantity = f.Random.Int(50, 500); // Random quantity between 50 and 500
                return new ProductEvents.ProductStocked(productId, quantity);
            });

        // Generate a random mix of events
        for (int i = 0; i < numberOfEvents; i++)
        {
            // Randomly select which type of event to generate
            if (i % 2 == 0) // 50% chance for ProductPurchased
            {
                yield return productPurchasedFaker.Generate();
            }
            else // 50% chance for ProductStocked
            {
                yield return productStockedFaker.Generate();
            }
        }
    }
}