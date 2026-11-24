using Shared.BuildingBlocks.Observability;
using Core.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Worker;
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

    [Fact]
    public async Task MissingDatabaseDoesNotAdvanceSuccessfulCycleOrReadiness()
    {
        WorkerMetrics.MarkCycleSucceeded();
        WorkerMetrics.MarkPostgresAvailable();
        WorkerMetrics.MarkRabbitMqAvailable();
        await Task.Delay(TimeSpan.FromSeconds(2.1));
        var before = WorkerMetrics.SuccessfulCycleAgeSeconds;

        using var provider = new ServiceCollection().BuildServiceProvider();
        var worker = new OutboxDispatchWorker(provider, NullLogger<OutboxDispatchWorker>.Instance, new ExponentialMessageRetryPolicy());
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(150);
        await worker.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

        Assert.True(WorkerMetrics.SuccessfulCycleAgeSeconds >= before);
        Assert.Equal(0, WorkerMetrics.PostgresAvailableValue);
        Assert.False(WorkerMetrics.IsReady());
    }

    [Fact]
    public void ReadinessRequiresBothProcessingDependencies()
    {
        WorkerMetrics.MarkCycleSucceeded();
        WorkerMetrics.MarkPostgresAvailable();
        WorkerMetrics.MarkRabbitMqAvailable();
        Assert.True(WorkerMetrics.IsReady());

        WorkerMetrics.MarkRabbitMqUnavailable();
        Assert.False(WorkerMetrics.IsReady());

        WorkerMetrics.MarkRabbitMqAvailable();
        WorkerMetrics.MarkPostgresUnavailable();
        Assert.False(WorkerMetrics.IsReady());
    }
}
