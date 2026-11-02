using Microsoft.Extensions.Logging;
using Modules.Hr.Repositories;
using Quartz;

namespace Worker;

/// <summary>Persists one idempotent in-app reminder per due HR item without changing its source record.</summary>
[DisallowConcurrentExecution]
public sealed class HrExpiryReminderJob(HrReminderRepository reminders, HrOffboardingLifecycleRepository offboarding, LeaveRequestRepository leaveRequests, TimeProvider clock, ILogger<HrExpiryReminderJob> logger) : IJob
{
    private static readonly Action<ILogger, int, Exception?> RemindersInserted = LoggerMessage.Define<int>(LogLevel.Information, new EventId(7310, "HrRemindersInserted"), "HR reminder scan created {ReminderCount} reminders for one legal entity.");
    private static readonly Action<ILogger, int, Exception?> TerminationsApplied = LoggerMessage.Define<int>(LogLevel.Information, new EventId(7312, "HrTerminationsApplied"), "HR lifecycle scan applied {TerminationCount} scheduled terminations.");
    private static readonly Action<ILogger, int, Exception?> AccrualsInserted = LoggerMessage.Define<int>(LogLevel.Information, new EventId(7313, "HrLeaveAccrualsInserted"), "HR lifecycle scan created {MovementCount} leave accrual movements.");
    private static readonly Action<ILogger, int, Exception?> CarryoversProcessed = LoggerMessage.Define<int>(LogLevel.Information, new EventId(7314, "HrLeaveCarryoversProcessed"), "HR lifecycle scan created {MovementCount} leave carryover and expiry movements.");
    private static readonly Action<ILogger, Guid, Guid, Exception?> LifecycleScanFailed = LoggerMessage.Define<Guid, Guid>(LogLevel.Error, new EventId(7311, "HrLifecycleScanFailed"), "HR lifecycle scan failed for tenant {TenantId}, legal entity {LegalEntityId}; due work remains eligible for the next daily retry.");

    public static readonly JobKey Key = new("hr-expiry-reminders", "hr");
    public static readonly TriggerKey Trigger = new("hr-expiry-reminders-daily", "hr");

    public async Task Execute(IJobExecutionContext context)
    {
        var scopes = await reminders.ListScheduledScopesAsync(context.CancellationToken);
        foreach (var (tenant, legalEntityId) in scopes)
        {
            var asOfDate = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            try
            {
                var terminated = await offboarding.ApplyDueTerminationsAsync(tenant, legalEntityId, asOfDate, context.CancellationToken);
                if (terminated > 0) TerminationsApplied(logger, terminated, null);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                LifecycleScanFailed(logger, tenant.Value, legalEntityId, exception);
            }
            try
            {
                var carryovers = await leaveRequests.ProcessScheduledCarryoversAsync(tenant, legalEntityId, asOfDate, context.CancellationToken);
                if (carryovers > 0) CarryoversProcessed(logger, carryovers, null);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                LifecycleScanFailed(logger, tenant.Value, legalEntityId, exception);
            }
            try
            {
                var accruals = await leaveRequests.AccrueScheduledPeriodsAsync(tenant, legalEntityId, asOfDate, context.CancellationToken);
                if (accruals > 0) AccrualsInserted(logger, accruals, null);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                LifecycleScanFailed(logger, tenant.Value, legalEntityId, exception);
            }
            try
            {
                var inserted = await reminders.ProcessDueAsync(tenant, legalEntityId, context.CancellationToken);
                if (inserted > 0) RemindersInserted(logger, inserted, null);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                LifecycleScanFailed(logger, tenant.Value, legalEntityId, exception);
            }
        }
    }
}
