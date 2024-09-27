// -----------------------------------------------------------------------
// <copyright file="SubscribeCommand.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Channels;
using Akka.Actor;
using Akka.DependencyInjection;
using DurableSubscriptions.Client.Actors;
using DurableSubscriptions.Shared;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DurableSubscriptions.Client.Cli;

public sealed class SubscribeCommand : AsyncCommand<SubscribeSettings>
{
    private readonly ActorSystem _system;
    private readonly IHostApplicationLifetime _lifetime;

    public SubscribeCommand(ActorSystem system, IHostApplicationLifetime lifetime)
    {
        _system = system;
        _lifetime = lifetime;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SubscribeSettings settings)
    {
        var resolver = DependencyResolver.For(_system);
        var runCommand = new SubscriptionMessages.RunSubscription(new SubscriberId(settings.SubscriberId!),
            new NonZeroInt(settings.PageSize), settings.Tags!, ActorRefs.Nobody);
        var props = resolver.Props<ClientSubscriber>(runCommand);
        var subscriber = _system.ActorOf(props, "subscriber");
        
        var channel = Channel.CreateUnbounded<IProductEvent>();
        subscriber.Tell(new SetSubscription(channel.Writer));
        
        await foreach(var e in channel.Reader.ReadAllAsync(_lifetime.ApplicationStopping))
        {
            AnsiConsole.MarkupLine($"[green]{e}[/]");
        }
        
        return 0;
    }
}