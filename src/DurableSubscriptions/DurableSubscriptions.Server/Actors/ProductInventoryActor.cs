// -----------------------------------------------------------------------
// <copyright file="ProductInventoryActor.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Event;
using Akka.Persistence;
using DurableSubscriptions.Shared;

namespace DurableSubscriptions.Server.Actors;

public sealed record ProductInventoryState(ProductId ProductId, int CurrentStockLevel);

public static class ProductInventoryStateExtensions
{
    public static ProductInventoryState Apply(this ProductInventoryState state, IProductEvent @event)
    {
        switch (@event)
        {
            case ProductEvents.ProductPurchased purchased:
                return state with { CurrentStockLevel = state.CurrentStockLevel - purchased.Quantity };
            case ProductEvents.ProductStocked stocked:
                return state with { CurrentStockLevel = state.CurrentStockLevel + stocked.Quantity };
            default:
                throw new ArgumentOutOfRangeException(nameof(@event));
        }
    }
}

public sealed class ProductInventoryActor : ReceivePersistentActor
{
    // State: Track the product's inventory state
    private ProductInventoryState _state;

    // Logging adapter for the actor
    private readonly ILoggingAdapter _log = Context.GetLogger();

    // Constructor
    public ProductInventoryActor(ProductId productId)
    {
        _state = new ProductInventoryState(productId, 0); // Initial state with zero stock

        // Handle commands (received messages)
        Command<ProductEvents.ProductPurchased>(HandleProductPurchased);
        Command<ProductEvents.ProductStocked>(HandleProductStocked);
        Command<SaveSnapshotSuccess>(_ =>
        {
            DeleteSnapshots(new SnapshotSelectionCriteria(LastSequenceNr-1));
        });
        Command<DeleteSnapshotsSuccess>(_ => {}); // ignore

        // Handle recovery (replaying events from the journal)
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is ProductInventoryState state)
            {
                _state = state;
            }
        });
        Recover<ProductEvents.ProductPurchased>(evt => _state = _state.Apply(evt));
        Recover<ProductEvents.ProductStocked>(evt => _state = _state.Apply(evt));
    }

    // The unique persistence ID for this actor
    public override string PersistenceId => $"product-inventory-{_state.ProductId.Id}";

    // Handle the ProductPurchased command/event
    private void HandleProductPurchased(ProductEvents.ProductPurchased purchased)
    {
        if (purchased.Quantity <= _state.CurrentStockLevel)
        {
            // Persist the event
            Persist(purchased, evt =>
            {
                // Apply the event to update the state using extension method
                _state = _state.Apply(evt);
                _log.Info("[{0}] Processed purchase: {1} units purchased. Current stock: {2}",
                          PersistenceId, evt.Quantity, _state.CurrentStockLevel);
            });
        }
        else
        {
            _log.Warning("[{0}] Purchase failed. Not enough stock. Current stock: {1}", 
                         PersistenceId, _state.CurrentStockLevel);
        }
    }

    // Handle the ProductStocked command/event
    private void HandleProductStocked(ProductEvents.ProductStocked stocked)
    {
        // Persist the event
        Persist(stocked, evt =>
        {
            // Apply the event to update the state using extension method
            _state = _state.Apply(evt);
            _log.Info("[{0}] Stock updated: {1} units stocked. Current stock: {2}",
                      PersistenceId, evt.Quantity, _state.CurrentStockLevel);
            
            if(LastSequenceNr % 10 == 0)
            {
                 SaveSnapshot(_state);
            }
        });
    }
}