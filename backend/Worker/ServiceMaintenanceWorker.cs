using Modules.Service.Repositories;
using Modules.Service.Services;
using Modules.Service.DTOs;
using Shared.Primitives.Identifiers;

namespace Worker;

/// <summary>Runs bounded preventive generation and SLA monitoring for every active Service scope.</summary>
public sealed class ServiceMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<ServiceMaintenanceWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> CycleFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(7401, "ServiceMaintenanceCycleFailed"),
        "Service maintenance cycle failed; due work remains eligible for the next retry.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await RunCycleAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { CycleFailed(logger, exception); }
        }
    }

    public async Task RunCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<ServiceScheduledWorkRepository>();
        var service = services.GetRequiredService<ServiceService>();
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        foreach (var (tenant, _) in await repository.ListScheduledScopesAsync(cancellationToken))
        {
            await service.GenerateDueAsync(tenant, Guid.Empty, new GenerateDueRequest(today, today.AddDays(30)), cancellationToken);
            await service.MonitorSlaAsync(tenant, Guid.Empty, cancellationToken);
        }
    }
}
