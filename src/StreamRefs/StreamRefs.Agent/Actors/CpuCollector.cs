// -----------------------------------------------------------------------
// <copyright file="CpuCollector.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Event;
using Universe.CpuUsage;

namespace StreamRefs.Agent.Actors;

/// <summary>
///     Collects CPU utilization data using https://github.com/devizer/Universe.CpuUsage
/// </summary>
public sealed class CpuCollector : ReceiveActor, IWithTimers
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IActorRef _metricAggregator;

    private double _lastCpuUsage = 0.0;
    private DateTime _lastMeasurementTime = DateTime.UtcNow;

    public CpuCollector(IActorRef metricAggregator)
    {
        _metricAggregator = metricAggregator;
        Receive<GatherMetrics>(metrics =>
        {
            var cpuUsage = CpuUsage.GetByProcess();
            if (cpuUsage.HasValue)
            {
                // convert the total microseconds to millicores
                var interval = DateTime.UtcNow - _lastMeasurementTime;
                var usageSinceLastMeasurement = cpuUsage.Value.TotalMicroSeconds - _lastCpuUsage;
                if (interval.TotalMilliseconds > 0)
                {
                    var millicoreUsage = usageSinceLastMeasurement / interval.TotalSeconds * 0.001;
                    _log.Info("CPU usage: {0} mc", millicoreUsage);
                    _metricAggregator.Tell(new CpuUpdate(millicoreUsage, DateTime.UtcNow.Ticks));
                    _lastCpuUsage = cpuUsage.Value.TotalMicroSeconds;
                }

                _lastMeasurementTime = DateTime.UtcNow;
            }
            else
            {
                _log.Warning("Failed to get CPU usage");
            }
        });
    }

    public ITimerScheduler Timers { get; set; } = null!;

    protected override void PreStart()
    {
        Timers.StartPeriodicTimer("cpu-gatherer", GatherMetrics.Instance, TimeSpan.FromSeconds(2.5));
    }

    private sealed class GatherMetrics
    {
        private GatherMetrics()
        {
        }

        public static GatherMetrics Instance { get; } = new();
    }

    public sealed record CpuUpdate(double CpuUsage, long TimeStamp);
}