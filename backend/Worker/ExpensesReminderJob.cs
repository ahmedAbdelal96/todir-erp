using Modules.Expenses.Repositories;
using Modules.Hr.Repositories;
using Quartz;

namespace Worker;

/// <summary>Scans unresolved employee actions and emits idempotent Expenses reminder events.</summary>
[DisallowConcurrentExecution]
public sealed class ExpensesReminderJob(HrReminderRepository scopes, ExpenseRepository expenses, ILogger<ExpensesReminderJob> logger) : IJob
{
    private static readonly Action<ILogger, int, Guid, Guid, Exception?> RemindersInserted = LoggerMessage.Define<int, Guid, Guid>(LogLevel.Information, new EventId(7340, "ExpensesRemindersInserted"), "Expenses reminder scan created {ReminderCount} events for tenant {TenantId}, legal entity {LegalEntityId}.");
    private static readonly Action<ILogger, Guid, Guid, Exception?> ReminderScanFailed = LoggerMessage.Define<Guid, Guid>(LogLevel.Error, new EventId(7341, "ExpensesReminderScanFailed"), "Expenses reminder scan failed for tenant {TenantId}, legal entity {LegalEntityId}; unresolved actions remain eligible for the next run.");
    public static readonly JobKey Key = new("expenses-reminders", "expenses");
    public static readonly TriggerKey Trigger = new("expenses-reminders-quarter-hour", "expenses");

    public async Task Execute(IJobExecutionContext context)
    {
        foreach (var (tenant, entity) in await scopes.ListScheduledScopesAsync(context.CancellationToken))
        {
            try
            {
                var inserted = await expenses.ProcessReminderNotificationsAsync(tenant, entity, context.CancellationToken);
                if (inserted > 0) RemindersInserted(logger, inserted, tenant.Value, entity, null);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ReminderScanFailed(logger, tenant.Value, entity, exception);
            }
        }
    }
}
