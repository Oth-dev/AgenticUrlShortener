using AgenticUrlShortener.Api.Models;

namespace AgenticUrlShortener.Api.Repositories;

public interface IMetricsStore
{
    ReliabilityMetrics Snapshot();
    void RunStarted();
    void RunSucceeded(TimeSpan latency);
    void RunStopped(TimeSpan latency);
    void Retry();
    void Rollback();
    void Replan();
    void Recovery(TimeSpan duration);
}

public sealed class InMemoryMetricsStore : IMetricsStore
{
    private readonly object _lock = new();
    private readonly ReliabilityMetrics _m = new();
    private double _totalLatency;
    private long _latencySamples;
    private double _totalRecovery;
    private long _recoverySamples;

    public void RunStarted()
    {
        lock (_lock) _m.TotalRuns++;
    }

    public void RunSucceeded(TimeSpan latency)
    {
        lock (_lock)
        {
            _m.SuccessfulRuns++;
            AddLatency(latency);
        }
    }

    public void RunStopped(TimeSpan latency)
    {
        lock (_lock)
        {
            _m.FailedOrStoppedRuns++;
            AddLatency(latency);
        }
    }

    public void Retry() { lock (_lock) _m.RetryCount++; }
    public void Rollback() { lock (_lock) _m.RollbackCount++; }
    public void Replan() { lock (_lock) _m.ReplanCount++; }

    public void Recovery(TimeSpan duration)
    {
        lock (_lock)
        {
            _totalRecovery += duration.TotalMilliseconds;
            _recoverySamples++;
            _m.AverageRecoveryTimeMs =
                _totalRecovery / _recoverySamples;
        }
    }

    public ReliabilityMetrics Snapshot()
    {
        lock (_lock)
        {
            return new ReliabilityMetrics
            {
                TotalRuns = _m.TotalRuns,
                SuccessfulRuns = _m.SuccessfulRuns,
                FailedOrStoppedRuns = _m.FailedOrStoppedRuns,
                RetryCount = _m.RetryCount,
                RollbackCount = _m.RollbackCount,
                ReplanCount = _m.ReplanCount,
                AverageLatencyMs = _m.AverageLatencyMs,
                AverageRecoveryTimeMs = _m.AverageRecoveryTimeMs
            };
        }
    }

    private void AddLatency(TimeSpan latency)
    {
        _totalLatency += latency.TotalMilliseconds;
        _latencySamples++;
        _m.AverageLatencyMs = _totalLatency / _latencySamples;
    }
}
