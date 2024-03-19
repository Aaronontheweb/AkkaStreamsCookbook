// -----------------------------------------------------------------------
//  <copyright file="MetricCommands.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Streams;

namespace StreamRefs.Shared;


public record struct NodeAddress(string Host, int Port);

public enum MetricMeasure
{
    Cpu
}

public record struct MetricEvent(NodeAddress Node, long TimeStamp, MetricMeasure Measure, double Value);

public readonly struct SubscriberId(string Id);

public interface ISubscriptionCommand
{
    SubscriberId SubscriberId { get; }
}

public static class MetricCommands
{
    public sealed record PingServer(NodeAddress MyAddress);
    
    /// <summary>
    /// Response to a <see cref="PingServer"/> command.
    /// </summary>
    /// <param name="SubscriberId">The server's unique subscriber id.</param>
    /// <param name="NodeAddress">The address of the contacting node - used for correlation.</param>
    public sealed record SubscribeToMetrics(SubscriberId SubscriberId, NodeAddress NodeAddress) : ISubscriptionCommand;
    
    public sealed record PushMetrics(SubscriberId SubscriberId, NodeAddress NodeAddress, ISourceRef<MetricEvent> MetricsSource) : ISubscriptionCommand;
    
    public sealed record ReceivingMetrics(SubscriberId SubscriberId, NodeAddress NodeAddress) : ISubscriptionCommand;
}