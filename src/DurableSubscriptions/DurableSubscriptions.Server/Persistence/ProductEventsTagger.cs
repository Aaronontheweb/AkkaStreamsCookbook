// -----------------------------------------------------------------------
// <copyright file="ProductEventsTagger.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Journal;
using DurableSubscriptions.Shared;

namespace DurableSubscriptions.Server.Persistence;

public sealed class ProductEventsTagger : IWriteEventAdapter
{
    public string Manifest(object evt)
    {
        return string.Empty;
    }

    public object ToJournal(object evt)
    {
        if (evt is IProductEvent pve)
        {
            return new Tagged(pve, new[] { DomainConstants.ProductTag(pve.ProductId) });
        }
        
        return evt;
    }
}