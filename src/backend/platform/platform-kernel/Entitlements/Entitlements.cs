using Todir.SharedKernel.Identifiers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Todir.Platform.Entitlements;

public sealed record EntitlementSnapshot(TenantId TenantId, long SnapshotVersion, DateTimeOffset IssuedAt, DateTimeOffset ValidUntil, string KeyId, IReadOnlySet<string> Modules, IReadOnlySet<string> Features, IReadOnlyDictionary<string, long> Limits, string Signature);
public sealed record CapabilityContext(TenantId TenantId, string? ModuleId = null, string? ScopeId = null);
public interface IEntitlementGuard { bool IsEntitled(TenantId tenantId, string moduleId); void SetSnapshot(EntitlementSnapshot snapshot); }
public interface IEntitlementVerifier { bool Verify(EntitlementSnapshot snapshot); }
public sealed class HmacEntitlementVerifier(IReadOnlyDictionary<string, byte[]> keys) : IEntitlementVerifier
{
    public bool Verify(EntitlementSnapshot snapshot)
    {
        if (!keys.TryGetValue(snapshot.KeyId, out var key) || snapshot.ValidUntil <= DateTimeOffset.UtcNow) return false;
        var canonical = JsonSerializer.Serialize(new { tenant = snapshot.TenantId.Value, snapshot = snapshot.SnapshotVersion, issued = snapshot.IssuedAt, valid = snapshot.ValidUntil, modules = snapshot.Modules.OrderBy(value => value, StringComparer.Ordinal), features = snapshot.Features.OrderBy(value => value, StringComparer.Ordinal), limits = snapshot.Limits.OrderBy(pair => pair.Key, StringComparer.Ordinal) });
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(snapshot.Signature), HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonical))); } catch (FormatException) { return false; }
    }
}
public sealed class EntitlementGuard(IEntitlementVerifier? verifier = null) : IEntitlementGuard
{
    private readonly Dictionary<TenantId, EntitlementSnapshot> snapshots = [];
    private readonly object gate = new();
    public bool IsEntitled(TenantId tenantId, string moduleId) { lock (gate) return snapshots.TryGetValue(tenantId, out var s) && s.ValidUntil > DateTimeOffset.UtcNow && s.Modules.Contains(moduleId); }
    public void SetSnapshot(EntitlementSnapshot snapshot) { if (verifier is not null && !verifier.Verify(snapshot)) throw new InvalidOperationException("entitlement.invalid_signature"); lock (gate) if (!snapshots.TryGetValue(snapshot.TenantId, out var current) || snapshot.SnapshotVersion >= current.SnapshotVersion) snapshots[snapshot.TenantId] = snapshot; }
}
public interface ICapabilityProvider { string Capability { get; } }
public interface ICapabilityResolver { T Resolve<T>(CapabilityContext context) where T : class; bool IsAvailable<T>(CapabilityContext context) where T : class; }
public sealed class CapabilityResolver(IEnumerable<ICapabilityProvider> providers) : ICapabilityResolver
{
    private readonly IReadOnlyList<ICapabilityProvider> providers = providers.ToArray();
    public T Resolve<T>(CapabilityContext context) where T : class
    {
        var provider = providers.OfType<T>().FirstOrDefault(candidate => candidate is IScopedCapabilityProvider scoped ? scoped.IsAvailable(context) : true);
        return provider ?? throw new InvalidOperationException("capability.unavailable");
    }
    public bool IsAvailable<T>(CapabilityContext context) where T : class => providers.OfType<T>().Any(candidate => candidate is not IScopedCapabilityProvider scoped || scoped.IsAvailable(context));
}

public interface IScopedCapabilityProvider
{
    bool IsAvailable(CapabilityContext context);
}
