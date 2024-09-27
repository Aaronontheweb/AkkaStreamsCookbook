// -----------------------------------------------------------------------
// <copyright file="SubscribeSettings.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DurableSubscriptions.Client.Cli;

public sealed class SubscribeSettings : CommandSettings
{
    [CommandArgument(0, "[tags]")]
    [Description("A list of tags to subscribe to, separated by spaces (e.g., tag1 tag2 tag3)")]
    public string[]? Tags { get; set; }

    public override ValidationResult Validate()
    {
        if (Tags == null || Tags.Length == 0)
        {
            return ValidationResult.Error("You must provide at least one tag.");
        }

        foreach (var tag in Tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return ValidationResult.Error("Tags cannot be empty or whitespace.");
            }
        }

        return ValidationResult.Success();
    }
}