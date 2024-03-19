using System.Runtime.CompilerServices;

namespace StreamRefs.MetricsCollector;

public static class TimeHelpers
{
    public static string PrettyPrint(this DateTime time)
        => (DateTime.UtcNow - time).ToElapsed();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToElapsed(this TimeSpan time)
    {
        var ticks = time.Ticks;

        if (ticks > TimeSpan.TicksPerDay)
            return $"{time.Days} d";
        if (ticks > TimeSpan.TicksPerHour)
            return $"{time.Hours} h";
        if (ticks > TimeSpan.TicksPerMinute)
            return $"{time.Minutes} m";

        return $"{time.Seconds} s";
    }
}