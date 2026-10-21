using Modules.Pricing.Repositories;

namespace Worker;

public sealed class PricingScheduledActivationWorker(IServiceScopeFactory scopeFactory, ILogger<PricingScheduledActivationWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> CycleFailed = LoggerMessage.Define(LogLevel.Error, new EventId(7201, "PricingScheduledActivationFailed"), "Pricing scheduled activation cycle failed; due versions remain eligible for retry.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PricingRepository>().ActivateDueVersionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { CycleFailed(logger, exception); }
        }
    }
}
