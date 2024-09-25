// -----------------------------------------------------------------------
// <copyright file="SubscriptionStreamSpecs.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Streams.Dsl;
using Akka.TestKit.Xunit2;
using DurableSubscriptions.Server.Actors;
using FluentAssertions;
using Xunit.Abstractions;

namespace DurableSubscriptions.Tests.Subscriptions;

public class SubscriptionStreamSpecs : TestKit
{
    public SubscriptionStreamSpecs(ITestOutputHelper output) : base(output: output)
    {
        
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ShouldCombineNumberOfSources(int sourceCount)
    {
        // arrange
        var sources = Enumerable.Range(0, sourceCount).Select(i => Source.Single(i)).ToList();
        var combined = StreamsHelper.CombineSources(sources);
        
        // act
        var result = await combined.RunWith(Sink.Seq<int>(), Sys);
        
        // assert
        result.Should().BeEquivalentTo(Enumerable.Range(0, sourceCount));
    }
}