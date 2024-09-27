// -----------------------------------------------------------------------
// <copyright file="SubscribeCommand.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Channels;
using Akka.Actor;
using Akka.Cluster.Tools.Client;
using Akka.DependencyInjection;
using Akka.Hosting;
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
        // Split tags by comma and trim spaces
        var tagsArray = settings.Tags!.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .ToArray();

        var actorRegistry = ActorRegistry.For(_system);
        var runCommand = new SubscriptionMessages.RunSubscription(new SubscriberId(settings.SubscriberId!),
            new NonZeroInt(settings.PageSize), tagsArray, ActorRefs.Nobody);
        var clusterClient = await actorRegistry.GetAsync<ClusterClient>();
        var props = Props.Create(() => new ClientSubscriber(clusterClient, runCommand));
        var subscriber = _system.ActorOf(props, "subscriber");

        _ = ShutdownAppIfSubscriberDies();
        
        
        var channel = Channel.CreateUnbounded<(long ordering, IProductEvent e)>();
        subscriber.Tell(new SetSubscription(channel.Writer));
        
        AnsiConsole.Markup("[bold green]Starting to stream events for the following tags:[/]");
        foreach (var tag in tagsArray)
        {
            AnsiConsole.MarkupLine($"[yellow]- {tag}[/]");
        }

        AnsiConsole.MarkupLine($"[bold green]Subscriber ID:[/] [yellow]{settings.SubscriberId}[/]");
        AnsiConsole.MarkupLine($"[bold green]Page Size:[/] [yellow]{settings.PageSize}[/]");
        
        await foreach(var e in channel.Reader.ReadAllAsync(_lifetime.ApplicationStopping))
        {
            AnsiConsole.MarkupLine($"[green]{e}[/]");
        }
        
        return 0;

        async Task ShutdownAppIfSubscriberDies()
        {
            await subscriber.WatchAsync();
            _lifetime.StopApplication();
        }
    }
}