using Akka.Actor;
using Akka.Persistence.Query;
using Akka.Util.Internal;
using DurableSubscriptions.Server.Actors;
using DurableSubscriptions.Shared;
using LanguageExt;

namespace DurableSubscriptions.Tests.Subscriptions;

public class SubscriptionStateSpecs
{
    public static readonly SubscriberId TestSubscriber = new("TestId");
    public static readonly NonZeroInt RequestedPageSize = new(10);

    public static readonly SubscriptionMessages.RunSubscription SubRequest1 = new(TestSubscriber, RequestedPageSize,
        ["test1", "test3", "test4"], ActorRefs.Nobody);

    // Added test2, lost test 3, keep test1 and 4 - and a bigger page size
    public static readonly SubscriptionMessages.RunSubscription SubRequest2 = new(TestSubscriber, new NonZeroInt(15),
        ["test1", "test2", "test3"], ActorRefs.Nobody);

    [Fact]
    public void ShouldRemoveUnusedTags()
    {
        // arrange
        var initial = new SubscriberState(TestSubscriber);

        // act1
        var updated = initial.Apply(SubRequest1);
        Assert.Equivalent(SubRequest1.Tags, updated.OffsetsPerTag.Keys);
        Assert.Equal(RequestedPageSize, updated.PageSize);

        // act2
        var updated2 = updated.Apply(SubRequest2);
        Assert.Equivalent(SubRequest2.Tags, updated2.OffsetsPerTag.Keys);
        Assert.Equal(new NonZeroInt(15), updated2.PageSize);
    }

    // create a test that ensures that data pages only include the highest offsets for each tag
    [Fact]
    public void ShouldComputeDataPageCorrectly()
    {
        // arrange
        var productId = new ProductId("foo");
        var e = new ProductEvents.ProductPurchased(productId, 10, 10d);
        var tag1Events = Enumerable.Range(0, 4).Select(
            c => new EventEnvelope(Offset.Sequence(c), "test1", c,
                e, DateTime.UtcNow.Ticks, ["test1"])).ToList();
        var tag2Events = Enumerable.Range(1, 5).Select(
            c => new EventEnvelope(Offset.Sequence(c), "test2", c,
                e, DateTime.UtcNow.Ticks, ["test2"])).ToList();
        var tag1And3Events = Enumerable.Range(7, 11).Select(
            c => new EventEnvelope(Offset.Sequence(c), "test1", c,
                e, DateTime.UtcNow.Ticks, ["test1", "test3"])).ToList() ?? throw new ArgumentNullException("Enumerable.Range(7, 11).Select(\n            c => new EventEnvelope(Offset.Sequence(c), \"test1\", c,\n                e, DateTime.UtcNow.Ticks, [\"test1\", \"test3\"])).ToList()");
        
        var combinedEvents = tag1Events.Concat(tag2Events).Concat(tag1And3Events).ToList();
        
        var initial = new SubscriberState(TestSubscriber).Apply(SubRequest1 with { Tags = ["test1", "test2", "test3", "test4"
        ]});
        var atomicCounter = new AtomicCounter(0);
        
        // act
        var dataPage1 = SubscriberActor.CreateDataPage(TestSubscriber, combinedEvents, atomicCounter);
        var updatedState = initial.Apply(dataPage1);
        
        // assert
        Assert.Equivalent(new[] { "test1", "test2", "test3" }, dataPage1.OffsetsPerTag.Keys);

        // check the offsets in the data page for each tag
        Assert.Equal(Offset.Sequence(17), dataPage1.OffsetsPerTag["test1"]);
        Assert.Equal(Offset.Sequence(5), dataPage1.OffsetsPerTag["test2"]);
        Assert.Equal(Offset.Sequence(17), dataPage1.OffsetsPerTag["test3"]);
        Assert.Equal(new NonZeroInt(1), dataPage1.PageId);
        Assert.Equivalent(combinedEvents.Select(c => c.Event), dataPage1.Events.Select(c => c.e));

        // check that the updated delivery state is correct

        // we have an extra tag, test4, that wasn't used in the event stream  - that offset should remain at 0
        Assert.Equivalent(new[] { "test1", "test2", "test3", "test4" }, updatedState.OffsetsPerTag.Keys);
        Assert.Equal(Offset.Sequence(17), updatedState.OffsetsPerTag["test1"]);
        Assert.Equal(Offset.Sequence(5), updatedState.OffsetsPerTag["test2"]);
        Assert.Equal(Offset.Sequence(17), updatedState.OffsetsPerTag["test3"]);
        Assert.Equal(Offset.NoOffset(), updatedState.OffsetsPerTag["test4"]);
    }
}