using Akka.Actor;
using Akka.Event;
using Universe.CpuUsage;

namespace StreamRefs.Agent.Actors;

/// <summary>
/// Collects CPU utilization data using https://github.com/devizer/Universe.CpuUsage
/// </summary>
public sealed class CpuCollector : ReceiveActor, IWithTimers
{
    private sealed class GatherMetrics
    {
        private GatherMetrics()
        {
        }

        public static GatherMetrics Instance { get; } = new();
    }

    public sealed record CpuUpdate(double CpuUsage, long TimeStamp);

    public ITimerScheduler Timers { get; set; } = null!;

    private DateTime _lastMeasurement = DateTime.UtcNow;
    private readonly IActorRef _metricAggregator;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    
    public CpuCollector(IActorRef metricAggregator)
    {
        _metricAggregator = metricAggregator;
        Receive<GatherMetrics>(metrics =>
        {
            var cpuUsage = CpuUsage.GetByProcess();
            if (cpuUsage.HasValue)
            {
                // convert the total microseconds to millicores
                var interval = DateTime.UtcNow - _lastMeasurement;
                if(interval.TotalMilliseconds > 0)
                {
                    var millicoreUsage = (cpuUsage.Value.TotalMicroSeconds / interval.TotalSeconds) * 0.001;
                    _log.Info("CPU usage: {0} mc", millicoreUsage);
                    _metricAggregator.Tell(new CpuUpdate(millicoreUsage, DateTime.UtcNow.Ticks));
                }
                
                _lastMeasurement = DateTime.UtcNow;
            }
            else
            {
                _log.Warning("Failed to get CPU usage");
            }
        });
    }
    
    protected override void PreStart()
    {
        Timers.StartPeriodicTimer("cpu-gatherer", GatherMetrics.Instance, TimeSpan.FromSeconds(2.5));
    }
}