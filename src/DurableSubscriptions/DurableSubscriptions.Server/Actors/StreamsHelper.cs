// -----------------------------------------------------------------------
// <copyright file="StreamsHelper.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka;
using Akka.Streams.Dsl;

namespace DurableSubscriptions.Server.Actors;

public sealed class StreamsHelper
{
    public static Source<T, NotUsed> CombineSources<T>(List<Source<T, NotUsed>> sources)
    {
        var combinedSource = sources.Count switch
        {
            0 => Source.Empty<T>(),
            1 => sources[0],
            _ => Source.Combine(sources[0], sources[1], i => new Merge<T, T>(i), sources.Skip(2).ToArray())
        };

        return combinedSource;
    }
}