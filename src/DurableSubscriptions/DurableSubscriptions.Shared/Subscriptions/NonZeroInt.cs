// -----------------------------------------------------------------------
// <copyright file="NonZeroInt.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace DurableSubscriptions.Shared;

public readonly struct NonZeroInt : IEquatable<NonZeroInt>, IComparable<NonZeroInt>, IComparable
{
    public int Value { get; }

    public NonZeroInt(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than zero.");

        Value = value;
    }

    public bool Equals(NonZeroInt other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is NonZeroInt other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value;
    }

    public static bool operator ==(NonZeroInt left, NonZeroInt right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(NonZeroInt left, NonZeroInt right)
    {
        return !left.Equals(right);
    }

    public int CompareTo(NonZeroInt other)
    {
        return Value.CompareTo(other.Value);
    }

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj)) return 1;
        return obj is NonZeroInt other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(NonZeroInt)}");
    }

    public static bool operator <(NonZeroInt left, NonZeroInt right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(NonZeroInt left, NonZeroInt right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(NonZeroInt left, NonZeroInt right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(NonZeroInt left, NonZeroInt right)
    {
        return left.CompareTo(right) >= 0;
    }
}