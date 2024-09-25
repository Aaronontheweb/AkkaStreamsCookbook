// -----------------------------------------------------------------------
// <copyright file="NonZeroInt.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace DurableSubscriptions.Shared;

public readonly struct NonZeroInt
{
    public int Value { get; }

    public NonZeroInt(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than zero.");

        Value = value;
    }
}