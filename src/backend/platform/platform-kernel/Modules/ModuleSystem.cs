using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
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
    Task ValidateActivationAsync(ModuleInstance instance, CancellationToken cancellationToken = default);
    Task ProvisionAsync(ModuleInstance instance, CancellationToken cancellationToken = default);
    Task ValidateDeactivationAsync(ModuleInstance instance, CancellationToken cancellationToken = default);
}

public sealed class ModuleLifecycleService(IModuleRegistry registry)
{
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
    public static ModuleInstance Deactivate(ModuleInstance instance, bool hasDependents) => hasDependents ? throw new InvalidOperationException("module.has_dependents") : instance with { Status = ModuleInstanceStatus.WindDown, Version = instance.Version + 1 };
}
