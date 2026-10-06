using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Collections.Concurrent;
using Todir.BuildingBlocks.Observability;

namespace Todir.Platform.Modules;

public sealed record ModuleDependencies(IReadOnlyList<string> Hard, IReadOnlyList<string> OptionalCapabilities);
public sealed record ModuleOwnership(string Schema, IReadOnlyList<string> Entities);
public sealed record ModuleCapabilities(IReadOnlyList<string> Provides, IReadOnlyList<string> Consumes);
public sealed record ModuleMessaging(IReadOnlyList<string> Publishes, IReadOnlyList<string> Consumes);
public sealed record ModuleLifecycle(bool SupportsGoLive, string DeactivationPolicy);
public sealed record ModuleManifest(
    string Id, string Version, int ContractsVersion, string Layer, string Type, bool Purchasable, string Scope,
    ModuleDependencies Dependencies, ModuleOwnership Owns, IReadOnlyList<string> Permissions,
    ModuleCapabilities Capabilities, ModuleMessaging Messaging, ModuleLifecycle Lifecycle);

public interface IModuleRegistry
{
    ModuleManifest Find(string moduleId);
    IReadOnlyCollection<ModuleManifest> GetAll();
    IReadOnlyList<string> GetHardDependencies(string moduleId);
    IReadOnlyList<string> GetDependents(string moduleId);
}

public sealed class ModuleRegistry : IModuleRegistry
{
    private readonly Dictionary<string, ModuleManifest> modules;
    public ModuleRegistry(IEnumerable<ModuleManifest> manifests)
    {
        modules = manifests.ToDictionary(m => m.Id, StringComparer.Ordinal);
        if (modules.Count != manifests.Count()) throw new InvalidOperationException("module.duplicate_id");
        foreach (var module in modules.Values)
        {
            if (string.IsNullOrWhiteSpace(module.Id) || module.Id.Any(char.IsUpper)) throw new InvalidOperationException("module.invalid_id");
            foreach (var dependency in module.Dependencies.Hard) if (!modules.ContainsKey(dependency)) throw new InvalidOperationException($"module.missing_dependency:{dependency}");
        }
        ValidateAcyclic();
        var schemas = modules.Values.GroupBy(m => m.Owns.Schema).Where(g => g.Count() > 1).ToArray();
        if (schemas.Length > 0) throw new InvalidOperationException("module.duplicate_schema");
    }

    public ModuleManifest Find(string moduleId) => modules.TryGetValue(moduleId, out var value) ? value : throw new KeyNotFoundException(moduleId);
    public IReadOnlyCollection<ModuleManifest> GetAll() => modules.Values.ToArray();
    public IReadOnlyList<string> GetHardDependencies(string moduleId) => Find(moduleId).Dependencies.Hard;
    public IReadOnlyList<string> GetDependents(string moduleId) => modules.Values.Where(m => m.Dependencies.Hard.Contains(moduleId, StringComparer.Ordinal)).Select(m => m.Id).ToArray();
    public IReadOnlyList<string> TopologicalOrder() { var result = new List<string>(); var visited = new HashSet<string>(); foreach (var id in modules.Keys) Visit(id, visited, new HashSet<string>(), result); return result; }

    private void Visit(string id, HashSet<string> visited, HashSet<string> active, List<string> result)
    {
        if (visited.Contains(id)) return;
        if (!active.Add(id)) throw new InvalidOperationException("module.dependency_cycle");
        foreach (var dependency in Find(id).Dependencies.Hard) Visit(dependency, visited, active, result);
        active.Remove(id); visited.Add(id); result.Add(id);
    }
    private void ValidateAcyclic() => TopologicalOrder();
    public static ModuleManifest Load(string json) => JsonSerializer.Deserialize<ModuleManifest>(json, JsonOptions) ?? throw new InvalidOperationException("module.invalid_manifest");
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
}

public enum ModuleInstanceStatus { Requested, Validating, Provisioning, AwaitingGoLive, Active, WindDown, ReadOnly, Disabled, Failed }
public sealed record ModuleInstance(string Id, string TenantId, string ModuleId, string ScopeType, string? ScopeId, ModuleInstanceStatus Status, long Version);
public sealed record ModuleImpactReport(IReadOnlyList<string> HardDependents, IReadOnlyList<string> OptionalCapabilitiesLost, IReadOnlyList<string> OpenOperations, bool RequiresGoLive);

public interface IModuleLifecycleContributor
{
    string ModuleId { get; }
    Task ValidateActivationAsync(ModuleInstance instance, CancellationToken cancellationToken = default);
    Task ProvisionAsync(ModuleInstance instance, CancellationToken cancellationToken = default);
    Task PrepareGoLiveAsync(ModuleInstance instance, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task ValidateDeactivationAsync(ModuleInstance instance, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task WindDownAsync(ModuleInstance instance, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class ModuleLifecycleService
{
    private readonly IModuleRegistry registry;
    private readonly IReadOnlyDictionary<string, IModuleLifecycleContributor> contributors;
    private readonly ConcurrentDictionary<string, LifecycleExecution> executions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.Ordinal);

    public ModuleLifecycleService(IModuleRegistry registry, IEnumerable<IModuleLifecycleContributor>? contributors = null)
    {
        this.registry = registry;
        this.contributors = (contributors ?? []).ToDictionary(contributor => contributor.ModuleId, StringComparer.Ordinal);
    }

    public ModuleInstance Activate(ModuleInstance instance, bool entitled)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (!entitled) throw new InvalidOperationException("module.not_entitled");
            if (instance.Status is ModuleInstanceStatus.Active or ModuleInstanceStatus.Provisioning or ModuleInstanceStatus.AwaitingGoLive) return instance;
            var dependencies = registry.GetHardDependencies(instance.ModuleId);
            if (dependencies.Count > 0 && instance.Status == ModuleInstanceStatus.Requested) return instance with { Status = ModuleInstanceStatus.Validating, Version = instance.Version + 1 };
            return instance with { Status = registry.Find(instance.ModuleId).Lifecycle.SupportsGoLive ? ModuleInstanceStatus.AwaitingGoLive : ModuleInstanceStatus.Active, Version = instance.Version + 1 };
        }
        finally
        {
            TodirMetrics.ModuleActivationDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>("module", instance.ModuleId));
        }
    }

    public async Task<ModuleInstance> ActivateAsync(ModuleInstance instance, bool entitled, string operationKey, IReadOnlyCollection<ModuleInstance>? dependencyInstances = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        if (!entitled) throw new InvalidOperationException("module.not_entitled");
        var manifest = registry.Find(instance.ModuleId);
        var missing = manifest.Dependencies.Hard.Where(dependency => dependencyInstances?.Any(candidate => candidate.TenantId == instance.TenantId && candidate.ModuleId == dependency && candidate.Status == ModuleInstanceStatus.Active) != true).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"module.dependency_not_active:{missing[0]}");

        var executionKey = ExecutionKey(instance, operationKey);
        var gate = locks.GetOrAdd(executionKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var execution = executions.GetOrAdd(executionKey, _ => new LifecycleExecution(instance));
            if (execution.Completed) return execution.Instance;
            var contributor = contributors.GetValueOrDefault(instance.ModuleId);
            if (!execution.Validated)
            {
                execution.Instance = execution.Instance with { Status = ModuleInstanceStatus.Validating, Version = execution.Instance.Version + 1 };
                if (contributor is not null) await contributor.ValidateActivationAsync(execution.Instance, cancellationToken);
                execution.Validated = true;
            }
            if (!execution.Provisioned)
            {
                execution.Instance = execution.Instance with { Status = ModuleInstanceStatus.Provisioning, Version = execution.Instance.Version + 1 };
                if (contributor is not null) await contributor.ProvisionAsync(execution.Instance, cancellationToken);
                execution.Provisioned = true;
            }
            if (manifest.Lifecycle.SupportsGoLive)
            {
                execution.Instance = execution.Instance with { Status = ModuleInstanceStatus.AwaitingGoLive, Version = execution.Instance.Version + 1 };
                return execution.Instance;
            }
            execution.Instance = execution.Instance with { Status = ModuleInstanceStatus.Active, Version = execution.Instance.Version + 1 };
            execution.Completed = true;
            return execution.Instance;
        }
        finally { gate.Release(); }
    }

    public async Task<ModuleInstance> CompleteGoLiveAsync(ModuleInstance instance, string operationKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var executionKey = ExecutionKey(instance, operationKey);
        if (!executions.TryGetValue(executionKey, out var execution)) throw new InvalidOperationException("module.go_live_not_pending");
        var gate = locks.GetOrAdd(executionKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (execution.Completed) return execution.Instance;
            if (execution.Instance.Status != ModuleInstanceStatus.AwaitingGoLive) throw new InvalidOperationException("module.go_live_not_pending");
            if (contributors.GetValueOrDefault(instance.ModuleId) is { } contributor) await contributor.PrepareGoLiveAsync(execution.Instance, cancellationToken);
            execution.Instance = execution.Instance with { Status = ModuleInstanceStatus.Active, Version = execution.Instance.Version + 1 };
            execution.Completed = true;
            return execution.Instance;
        }
        finally { gate.Release(); }
    }

    public ModuleImpactReport CreateImpactReport(ModuleInstance instance, IReadOnlyCollection<ModuleInstance> knownInstances, IReadOnlyList<string>? optionalCapabilitiesLost = null, IReadOnlyList<string>? openOperations = null)
    {
        var hardDependents = registry.GetDependents(instance.ModuleId)
            .Where(moduleId => knownInstances.Any(candidate => candidate.TenantId == instance.TenantId && candidate.ModuleId == moduleId && candidate.Status is not ModuleInstanceStatus.Disabled and not ModuleInstanceStatus.Failed))
            .ToArray();
        var open = openOperations ?? [];
        return new ModuleImpactReport(hardDependents, optionalCapabilitiesLost ?? [], open, registry.Find(instance.ModuleId).Lifecycle.SupportsGoLive);
    }

    public async Task<ModuleInstance> DeactivateAsync(ModuleInstance instance, ModuleImpactReport impact, string operationKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        if (impact.HardDependents.Count > 0) throw new InvalidOperationException("module.has_dependents");
        var executionKey = ExecutionKey(instance, $"deactivate:{operationKey}");
        var gate = locks.GetOrAdd(executionKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var execution = executions.GetOrAdd(executionKey, _ => new LifecycleExecution(instance));
            if (execution.Completed) return execution.Instance;
            if (contributors.GetValueOrDefault(instance.ModuleId) is { } contributor)
            {
                await contributor.ValidateDeactivationAsync(execution.Instance, cancellationToken);
                if (impact.OpenOperations.Count > 0) await contributor.WindDownAsync(execution.Instance, cancellationToken);
            }
            var target = impact.OpenOperations.Count > 0
                ? ModuleInstanceStatus.WindDown
                : registry.Find(instance.ModuleId).Lifecycle.DeactivationPolicy.Equals("never_disable", StringComparison.OrdinalIgnoreCase)
                    ? ModuleInstanceStatus.ReadOnly
                    : ModuleInstanceStatus.Disabled;
            execution.Instance = execution.Instance with { Status = target, Version = execution.Instance.Version + 1 };
            execution.Completed = true;
            return execution.Instance;
        }
        finally { gate.Release(); }
    }

    public static ModuleInstance Deactivate(ModuleInstance instance, bool hasDependents) => hasDependents ? throw new InvalidOperationException("module.has_dependents") : instance with { Status = ModuleInstanceStatus.WindDown, Version = instance.Version + 1 };

    private static string ExecutionKey(ModuleInstance instance, string operationKey) => $"{instance.TenantId}:{instance.Id}:{operationKey}";

    private sealed class LifecycleExecution(ModuleInstance instance)
    {
        public ModuleInstance Instance { get; set; } = instance;
        public bool Validated { get; set; }
        public bool Provisioned { get; set; }
        public bool Completed { get; set; }
    }
}
