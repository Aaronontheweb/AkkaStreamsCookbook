// -----------------------------------------------------------------------
// <copyright file="ClientSubscriber.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Channels;
using Akka.Actor;
using Akka.Cluster.Tools.Client;
using Akka.Event;
using Akka.Hosting;
using DurableSubscriptions.Shared;

namespace DurableSubscriptions.Client.Actors;

public sealed record SetSubscription(ChannelWriter<(long ordering, IProductEvent e)> EventsChannel)
    : INoSerializationVerificationNeeded;

public sealed class ClientSubscriber : UntypedActor, IWithStash, IWithTimers
{
    // needs to be sent to us by Spectre.Console
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IActorRef _clusterClient;
    private IActorRef? _remotePublisher; // used to help us keep track if the SubscriberActor dies or moves
    private ChannelWriter<(long ordering, IProductEvent e)>? _eventsChannel;
    private readonly SubscriptionMessages.RunSubscription _runSubscription;

    public ClientSubscriber(IActorRef clusterClient,
        SubscriptionMessages.RunSubscription runSubscription)
    {
        // we need to make sure that the Sink is set to Self
        _runSubscription = runSubscription with {Sink = Self};
        _clusterClient = clusterClient;
    }

    protected override void PreStart()
    {
        TryStartSubscription();
    }

    private void TryStartSubscription()
    {
        _clusterClient.Tell(new ClusterClient.Send("/system/sharding/subscriptions",
            _runSubscription, localAffinity:true));
        Timers.StartSingleTimer("subscription-start-timeout", _runSubscription, TimeSpan.FromSeconds(5));
    }

    protected override void OnReceive(object message)
    {
        switch (message)
        {
            case SetSubscription setSubscription:
                _eventsChannel = setSubscription.EventsChannel;
                TryToTransitionToReady();
                break;
            case SubscriptionMessages.SubscriptionStarted:
                _log.Info("Successfully started subscription for {0} on-time", _runSubscription.SubscriberId);
                _remotePublisher = Sender;
                Context.Watch(_remotePublisher);
                Timers.CancelAll(); // just in case
                TryToTransitionToReady();
                break;
            case SubscriptionMessages.RunSubscription:
                _log.Warning("Failed to start subscription for {0} on-time - retrying...", _runSubscription.SubscriberId);
                TryStartSubscription();
                break;
            case SubscriptionMessages.SubscriptionTerminated:
            case Terminated:
                // ignore - old actor has died
                break;
            default:
                Stash.Stash();
                break;
        }

        return;

        void TryToTransitionToReady()
        {
            if (_eventsChannel is not null && _remotePublisher is not null)
            {
                Become(Ready);
                Stash.UnstashAll();
                Timers.CancelAll();
            }
        }
    }

    private void Ready(object message)
    {
        switch (message)
        {
            case DataPage dataPage:
                _log.Info("Received page {0} for {1} with {2} events", dataPage.PageId, dataPage.SubscriberId, dataPage.Events.Count);
                foreach (var evt in dataPage.Events)
                {
                    _eventsChannel!.TryWrite(evt);
                }
                _remotePublisher!.Tell(new SubscriptionMessages.AckPage(dataPage.SubscriberId, dataPage.PageId));
                break;
            case SubscriptionMessages.SubscriptionTerminated:
            {
                _log.Warning("Subscription for {0} has been terminated - restarting subscription", _runSubscription.SubscriberId);
                Become(Receive);
                TryStartSubscription();
                break;
            }
            case Terminated:
                _log.Warning("Remote publisher for {0} has died - restarting subscription", _runSubscription.SubscriberId);
                Become(Receive);
                TryStartSubscription();
                break;
            case SubscriptionMessages.RunSubscription:
                // ignore
                break;
        }
    }

    public IStash Stash { get; set; } = null!;
    public ITimerScheduler Timers { get; set; } = null!;
}