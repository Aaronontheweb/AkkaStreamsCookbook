using Akka.Actor;
using DurableSubscriptions.Server.Actors;
using DurableSubscriptions.Shared;
using FluentAssertions;

namespace DurableSubscriptions.Tests.Subscriptions;

public class SubscriptionStateSpecs
{
    public static readonly SubscriberId TestSubscriber = new SubscriberId("TestId");
    public static readonly NonZeroInt RequestedPageSize = new(10);

    public static readonly SubscriptionMessages.RunSubscription SubRequest1 = new(TestSubscriber, RequestedPageSize,
        new[] { "test1", "test3", "test4" }, ActorRefs.Nobody);

    // Added test2, lost test 3, keep test1 and 4 - and a bigger page size
    public static readonly SubscriptionMessages.RunSubscription SubRequest2 = new(TestSubscriber, new NonZeroInt(15),
        new[] { "test1", "test2", "test3" }, ActorRefs.Nobody);
    
    [Fact]
    public void ShouldRemoveUnusedTags()
    {
        // arrange
        var initial = new SubscriberState(TestSubscriber);
        
        // act1
        var updated = initial.Apply(SubRequest1);
        updated.OffsetsPerTag.Keys.Should().BeEquivalentTo(SubRequest1.Tags);
        updated.PageSize.Should().Be(RequestedPageSize);
        
        // act2
        var updated2 = updated.Apply(SubRequest2);
        updated2.OffsetsPerTag.Keys.Should().BeEquivalentTo(SubRequest2.Tags);
        updated2.PageSize.Should().Be(new NonZeroInt(15));
    }
}