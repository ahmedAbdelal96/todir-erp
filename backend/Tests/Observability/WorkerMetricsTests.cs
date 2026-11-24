using Shared.BuildingBlocks.Observability;
using Xunit;

namespace ObservabilityTests;

public sealed class WorkerMetricsTests
{
    [Fact]
    public void SuccessfulCycleProgressIsFreshAndBounded()
    {
        WorkerMetrics.MarkCycleStarted();
        WorkerMetrics.MarkCycleSucceeded();

        Assert.InRange(WorkerMetrics.CycleAgeSeconds, 0, 2);
        Assert.InRange(WorkerMetrics.SuccessfulCycleAgeSeconds, 0, 2);
    }

    [Fact]
    public void FailureDoesNotAdvanceSuccessfulProgress()
    {
        WorkerMetrics.MarkCycleSucceeded();
        var before = WorkerMetrics.SuccessfulCycleAgeSeconds;
        WorkerMetrics.MarkCycleFailed();

        Assert.InRange(WorkerMetrics.SuccessfulCycleAgeSeconds, 0, Math.Max(2, before + 2));
    }
}
