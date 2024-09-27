// -----------------------------------------------------------------------
// <copyright file="ClientSubscriber.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Channels;
using Akka.Actor;
using Akka.Cluster.Tools.Client;
using Akka.Hosting;
using DurableSubscriptions.Shared;

namespace DurableSubscriptions.Client.Actors;

public static class SubscriberMessages
{
    public sealed record SetSubscription(ChannelWriter<IProductEvent> EventsChannel)
        : INoSerializationVerificationNeeded;

    public sealed record AttemptToConnectToCluster() : INoSerializationVerificationNeeded;
}

public class ClientSubscriber : UntypedActor, IWithStash, IWithTimers
{
    // needs to be sent to us by Spectre.Console
    private readonly IActorRef _clusterClient;
    private ChannelWriter<IProductEvent>? _eventsChannel;
    private readonly SubscriptionMessages.RunSubscription _runSubscription;

    public ClientSubscriber(IRequiredActor<ClusterClient> clusterClient,
        SubscriptionMessages.RunSubscription runSubscription)
    {
        _runSubscription = runSubscription;
        _clusterClient = clusterClient.ActorRef;
    }

    protected override void PreStart()
    {
        _clusterClient.Tell(new ClusterClient.SendToAll("/system/sharding/subscriptions",
            new SubscriberMessages.AttemptToConnectToCluster()));
    }

    protected override void OnReceive(object message)
    {
        switch (message)
        {
            case SubscriberMessages.SetSubscription setSubscription:
                _eventsChannel = setSubscription.EventsChannel;
                Stash.UnstashAll();
                Become(Ready);
                break;
            default:
                Stash.Stash();
                break;
        }
    }

    private void Ready(object message)
    {
    }

    public IStash Stash { get; set; } = null!;
    public ITimerScheduler Timers { get; set; } = null!;
}