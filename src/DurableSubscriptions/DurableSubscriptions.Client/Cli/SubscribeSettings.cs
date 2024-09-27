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
    public string? Tags { get; set; }

    [CommandOption("-s|--subscriber-id")]
    [Description("Subscriber ID for the subscription")]
    public string? SubscriberId { get; set; }

    [CommandOption("-p|--page-size")]
    [Description("Page size for the data")]
    public int PageSize { get; set; } = 10;

    public override ValidationResult Validate()
    {
        // Validate Tags
        if (string.IsNullOrWhiteSpace(Tags))
        {
            return ValidationResult.Error("You must provide at least one tag.");
        }

        // Split tags by comma and trim spaces
        var tagsArray = Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .ToArray();

        if (tagsArray.Length == 0)
        {
            return ValidationResult.Error("Invalid tag format. Provide at least one tag.");
        }

        foreach (var tag in tagsArray)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return ValidationResult.Error("Tags cannot be empty or whitespace.");
            }
        }

        // Validate SubscriberId
        if (string.IsNullOrWhiteSpace(SubscriberId))
        {
            return ValidationResult.Error("Subscriber ID must be provided and cannot be empty.");
        }

        // Validate PageSize
        if (PageSize <= 0)
        {
            return ValidationResult.Error("Page size must be a positive integer greater than zero.");
        }

        return ValidationResult.Success();
    }
}