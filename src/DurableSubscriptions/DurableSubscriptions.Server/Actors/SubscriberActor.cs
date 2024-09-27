// -----------------------------------------------------------------------
// <copyright file="SubscriberActor.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util.Internal;
using DurableSubscriptions.Shared;

namespace DurableSubscriptions.Server.Actors;

public sealed class SubscriberActor : UntypedPersistentActor, IWithTimers
{
    public override string PersistenceId { get; }
    private readonly IMaterializer _mat = Context.Materializer();
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private CancellationTokenSource? _subscriptionCancellation;
    private IActorRef? _remoteSubscriber;

    private AtomicCounter _pageId = new AtomicCounter(0);
    public SubscriberState State { get; private set; }

    public SubscriberActor(SubscriberId subscriberId)
    {
        PersistenceId = $"subscriber-{subscriberId.Id}";
        State = new SubscriberState(subscriberId);
    }

    protected override void OnCommand(object message)
    {
        switch (message)
        {
            case SubscriptionMessages.RunSubscription run:
            {
                HandleRun(run);
                break;
            }
            case Completed:
            {
                // ignore
                break;
            }
            case AckInternalPageStream:
            {
                // we received an ack from the remote subscriber even though the subscription was not running
                _log.Warning(
                    "Received ack from remote subscriber for even though subscription is not currently running");
                break;
            }
        }
    }

    private void HandleRun(SubscriptionMessages.RunSubscription run)
    {
        _remoteSubscriber = run.Sink;
        Context.Watch(_remoteSubscriber); // if they die or the connection does, we'll reset
        _remoteSubscriber.Tell(new SubscriptionMessages.SubscriptionStarted(State.SubscriberId));
        _subscriptionCancellation = new CancellationTokenSource();

        // update our state
        State = State.Apply(run);

        var readJournal = PersistenceQuery.Get(Context.System)
            .ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
        var self = Self;

        // for each tag, we need to start an EventsByTag query
        var sources = State.OffsetsPerTag.Select(c => readJournal.EventsByTag(c.Key, c.Value)).ToList();

        // merge the sources together
        var combined = StreamsHelper.CombineSources(sources);
        combined
            .Via(_subscriptionCancellation.Token.AsFlow<EventEnvelope>())
            .GroupedWithin(State.PageSize.Value, TimeSpan.FromSeconds(10))
            .Select(c => CreateDataPage(State.SubscriberId, c.ToList(), _pageId))
            .RunWith(Sink.ActorRefWithAck<DataPageStructure>(self, Start.Instance, AckInternalPageStream.Instance,
                Completed.Instance,
                ex => new Status.Failure(ex)), _mat);

        Become(RunningSubscription);
    }

    /// <summary>
    /// Occurs after the client subscribes to the stream.
    /// </summary>
    private void RunningSubscription(object message)
    {
        switch (message)
        {
            case DataPageStructure page:
            {
                Become(PendingPageAck(page, Sender));
                SchedulePageTimer(new AckTimeout(page.PageId, 0, 5));
                _remoteSubscriber.Tell(page.ToDataPage());
                break;
            }
            case SubscriptionMessages.RunSubscription run:
            {
                // we're already running a subscription, so we need to cancel it
                ResetSubscription();

                // start a new one
                HandleRun(run);
                break;
            }
            case Start:
            {
                // need to ACK the start of the stream
                Sender.Tell(AckInternalPageStream.Instance);
                break;
            }
            case Terminated t when t.ActorRef.Equals(_remoteSubscriber):
            {
                _log.Warning("Remote subscriber terminated. Resetting subscription.");
                ResetSubscription();
                Become(OnCommand);
                break;
            }
            case Status.Failure failure:
            {
                _log.Error(failure.Cause, "Failed to run subscription.");
                ResetSubscription();
                Become(OnCommand);
                break;
            }
            case Completed:
            {
                _log.Info("Local stream has terminated.");
                ResetSubscription();
                Become(OnCommand);
                break;
            }
        }
    }

    private void SchedulePageTimer(AckTimeout timeout)
    {
        if (timeout.RetryCount >= timeout.MaxRetries)
        {
            _log.Error("Failed to receive ack for page {0} after {1} attempts. Cancelling subscription.",
                timeout.PageId, timeout.MaxRetries);
            ResetSubscription();
            Become(OnCommand);
            return;
        }

        Timers.StartSingleTimer($"ack-timeout-{timeout.PageId}", timeout with { RetryCount = timeout.RetryCount + 1 },
            TimeSpan.FromSeconds(5));
    }

    private void UnschedulePageTimer(NonZeroInt pageId)
    {
        Timers.Cancel($"ack-timeout-{pageId}");
    }

    private void ResetSubscription()
    {
        _subscriptionCancellation?.Cancel();
        _subscriptionCancellation?.Dispose();
        _subscriptionCancellation = null;
        if (_remoteSubscriber != null)
        {
            // let the subscriber know we're done
            _remoteSubscriber.Tell(new SubscriptionMessages.SubscriptionTerminated(State.SubscriberId));
            Context.Unwatch(_remoteSubscriber);
        }
    }

    private Receive PendingPageAck(DataPageStructure currentPage, IActorRef localStreamSender)
    {
        return s =>
        {
            switch (s)
            {
                case SubscriptionMessages.AckPage ackPage when ackPage.PageId == currentPage.PageId:
                {
                    _log.Debug("Received ack for page {0}", ackPage.PageId);
                    UnschedulePageTimer(currentPage.PageId);
                    State = State.Apply(currentPage);
                    Become(RunningSubscription);
                    localStreamSender.Tell(AckInternalPageStream.Instance);
                    return true;
                }
                case SubscriptionMessages.AckPage ackPage:
                {
                    _log.Warning("Received ack for page {0} but we were expecting ack for page {1}. Ignoring.",
                        ackPage.PageId, currentPage.PageId);
                    return true;
                }
                case AckTimeout timeout:
                {
                    _log.Warning("Failed to receive ack for page {0} after {1} attempts. Retrying.", timeout.PageId,
                        timeout.RetryCount);
                    SchedulePageTimer(timeout);
                    _remoteSubscriber.Tell(currentPage);
                    return true;
                }
                case Terminated t when t.ActorRef.Equals(_remoteSubscriber):
                {
                    _log.Warning("Remote subscriber terminated. Resetting subscription.");
                    ResetSubscription();
                    Become(OnCommand);
                    return true;
                }
                case Status.Failure failure:
                {
                    _log.Error(failure.Cause, "Failed to run subscription.");
                    ResetSubscription();
                    Become(OnCommand);
                    return true;
                }
                case Completed:
                {
                    _log.Info("Local stream has terminated.");
                    ResetSubscription();
                    Become(OnCommand);
                    return true;
                }
                default:
                    return false;
            }
        };
    }

    protected override void OnRecover(object message)
    {
        switch (message)
        {
            case SnapshotOffer { Snapshot: SubscriberState state }:
                State = state;
                break;
            case SubscriberState state:
                State = state;
                break;
        }
    }

    private sealed class Start
    {
        public static readonly Start Instance = new();

        private Start()
        {
        }
    }

    private sealed class AckInternalPageStream
    {
        public static readonly AckInternalPageStream Instance = new();

        private AckInternalPageStream()
        {
        }
    }

    private sealed class Completed : IDeadLetterSuppression
    {
        public static readonly Completed Instance = new();

        private Completed()
        {
        }
    }

    private sealed record AckTimeout(NonZeroInt PageId, int RetryCount, int MaxRetries);

    public static DataPageStructure CreateDataPage(SubscriberId subscriberId, IReadOnlyList<EventEnvelope> events,
        AtomicCounter pageIdCounter)
    {
        // grab the largest offset per tag - bearing in mind there can be multiple tags per event
        var tagData = new Dictionary<string, Offset>();
        foreach (var e in events)
        {
            foreach (var t in e.Tags)
            {
                if (tagData.TryGetValue(t, out var current))
                {
                    if (e.Offset.CompareTo(current) > 0)
                        tagData[t] = e.Offset;
                }
                else
                {
                    tagData[t] = e.Offset;
                }
            }
        }

        // ok, now filter all the events, so we include only the IProductEvent
        var productEvents = events.Select(e => (e.Offset.AsInstanceOf<Sequence>().Value, (IProductEvent)e.Event)).ToList();

        return new DataPageStructure(subscriberId, tagData, productEvents,
            new NonZeroInt(pageIdCounter.IncrementAndGet()));
    }

    public ITimerScheduler Timers { get; set; } = null!;

    protected override void PostStop()
    {
        // in the event that we're stopped, we need to let the remote subscriber know
        _remoteSubscriber?.Tell(new SubscriptionMessages.SubscriptionTerminated(State.SubscriberId));
    }
}