using Modules.Inventory.Services;

namespace Worker;

public sealed class InventoryReservationExpiryWorker(IServiceScopeFactory scopeFactory,
    ILogger<InventoryReservationExpiryWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> CycleCompleted = LoggerMessage.Define<int>(
        LogLevel.Information, new EventId(7301, "InventoryReservationsExpired"),
        "Inventory reservation expiry cycle completed; {Count} reservations expired.");
    private static readonly Action<ILogger, Exception?> CycleFailed = LoggerMessage.Define(LogLevel.Error,
        new EventId(7302, "InventoryReservationExpiryFailed"),
        "Inventory reservation expiry cycle failed; due reservations remain eligible for retry.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<InventoryService>()
                    .ExpireDueReservationsAsync(cancellationToken: stoppingToken);
                if (count > 0) CycleCompleted(logger, count, null);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { CycleFailed(logger, exception); }
        }
    }
}
