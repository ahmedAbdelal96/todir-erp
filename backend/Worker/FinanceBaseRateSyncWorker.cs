using Modules.FinanceBase.Services;

namespace Worker;

public sealed class FinanceBaseRateSyncWorker(IServiceScopeFactory scopeFactory, ILogger<FinanceBaseRateSyncWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> SyncFailed = LoggerMessage.Define(LogLevel.Error, new EventId(7101, "FinanceBaseRateSyncFailed"), "FinanceBase provider sync cycle failed; configured sources remain scheduled for retry.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sync = scope.ServiceProvider.GetRequiredService<IFinanceBaseRateSyncService>();
                await sync.SyncDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { SyncFailed(logger, exception); }
        }
    }
}
