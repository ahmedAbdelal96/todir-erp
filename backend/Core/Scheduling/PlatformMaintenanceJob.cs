using Quartz;
using Core.Security;

namespace Core.Scheduling;

public interface IPlatformMaintenanceTask
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

public sealed class PlatformMaintenanceTask(ISupportAccessWorkflow supportAccessWorkflow) : IPlatformMaintenanceTask
{
    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return supportAccessWorkflow.ExpireDueAsync(cancellationToken);
    }
}

[DisallowConcurrentExecution]
public sealed class PlatformMaintenanceJob(IPlatformMaintenanceTask maintenanceTask) : IJob
{
    public static readonly JobKey Key = new("platform-maintenance", "platform");
    public static readonly TriggerKey Trigger = new("platform-maintenance-trigger", "platform");

    public Task Execute(IJobExecutionContext context) => maintenanceTask.ExecuteAsync(context.CancellationToken);
}
