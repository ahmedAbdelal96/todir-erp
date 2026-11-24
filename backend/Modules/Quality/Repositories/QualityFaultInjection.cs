using System;
using System.Threading;

namespace Modules.Quality.Repositories;

/// <summary>
/// Scoped fault-injection mechanism for testing Quality-Inventory transaction boundaries.
/// Uses AsyncLocal to ensure faults are strictly isolated to the calling test execution context
/// and cannot leak across concurrent tasks, threads, or tenants.
/// </summary>
public static class QualityFaultInjection
{
    private static readonly AsyncLocal<Action<Guid, Guid>?> postInventoryHook = new();

    public static IDisposable SetPostInventoryPreQualityConfirmationHook(Action<Guid, Guid> hook)
    {
        postInventoryHook.Value = hook;
        return new HookScope();
    }

    internal static void OnPostInventoryPreQualityConfirmation(Guid restrictionId, Guid operationId)
    {
        postInventoryHook.Value?.Invoke(restrictionId, operationId);
    }

    private sealed class HookScope : IDisposable
    {
        public void Dispose()
        {
            postInventoryHook.Value = null;
        }
    }
}
