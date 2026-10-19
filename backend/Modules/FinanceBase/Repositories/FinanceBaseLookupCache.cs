using Modules.FinanceBase.DTOs;
using Modules.FinanceBase.Entities;
using Shared.Primitives.Identifiers;

namespace Modules.FinanceBase.Repositories;

internal sealed class FinanceBaseLookupCache
{
    private readonly BoundedCache<string, bool> currencies = new(512);
    private readonly BoundedCache<(TenantId Tenant, LegalEntityId Entity), FinancialPolicyDto> policies = new(512);
    private readonly BoundedCache<RateKey, ExchangeRateCandidate> rates = new(1024);
    private readonly BoundedCache<(TenantId Tenant, Guid PolicyId), RoundingPolicySnapshot> roundingPolicies = new(512);

    public bool TryGetCurrency(string code, out bool exists) => currencies.TryGet(code, out exists);
    public void SetCurrency(string code, bool exists) => currencies.Set(code, exists, TimeSpan.FromHours(1));
    public bool TryGetPolicy(TenantId tenant, LegalEntityId entity, out FinancialPolicyDto? policy) => policies.TryGet((tenant, entity), out policy);
    public void SetPolicy(TenantId tenant, LegalEntityId entity, FinancialPolicyDto policy) => policies.Set((tenant, entity), policy, TimeSpan.FromSeconds(15));
    public void InvalidatePolicy(TenantId tenant, LegalEntityId entity) => policies.Remove((tenant, entity));
    public bool TryGetRoundingPolicy(TenantId tenant, Guid policyId, out RoundingPolicySnapshot? policy) => roundingPolicies.TryGet((tenant, policyId), out policy);
    public void SetRoundingPolicy(TenantId tenant, Guid policyId, RoundingPolicySnapshot policy) => roundingPolicies.Set((tenant, policyId), policy, TimeSpan.FromHours(1));

    public bool TryGetRate(TenantId tenant, LegalEntityId entity, string from, string to, ExchangeRatePurpose purpose, DateOnly date, long policyVersion, out ExchangeRateCandidate? rate)
        => rates.TryGet(new(tenant, entity, from, to, purpose, date, policyVersion), out rate);

    public void SetRate(TenantId tenant, LegalEntityId entity, string from, string to, ExchangeRatePurpose purpose, DateOnly date, long policyVersion, ExchangeRateCandidate rate)
        => rates.Set(new(tenant, entity, from, to, purpose, date, policyVersion), rate, TimeSpan.FromSeconds(5));

    public void InvalidateRates(TenantId tenant, LegalEntityId entity, string? from = null, string? to = null, ExchangeRatePurpose? purpose = null)
        => rates.RemoveWhere(key => key.Tenant == tenant && key.Entity == entity && (from is null || key.From == from) && (to is null || key.To == to) && (purpose is null || key.Purpose == purpose));

    private sealed record RateKey(TenantId Tenant, LegalEntityId Entity, string From, string To, ExchangeRatePurpose Purpose, DateOnly Date, long PolicyVersion);

    private sealed class BoundedCache<TKey, TValue>(int capacity) where TKey : notnull
    {
        private readonly object gate = new();
        private readonly Dictionary<TKey, (TValue Value, DateTimeOffset Expires)> entries = [];
        private readonly Queue<TKey> insertionOrder = [];

        public bool TryGet(TKey key, out TValue? value)
        {
            lock (gate)
            {
                if (entries.TryGetValue(key, out var entry) && entry.Expires > DateTimeOffset.UtcNow)
                {
                    value = entry.Value;
                    return true;
                }
                entries.Remove(key);
                value = default;
                return false;
            }
        }

        public void Set(TKey key, TValue value, TimeSpan lifetime)
        {
            lock (gate)
            {
                if (!entries.ContainsKey(key)) insertionOrder.Enqueue(key);
                while (entries.Count >= capacity && insertionOrder.TryDequeue(out var oldest)) entries.Remove(oldest);
                entries[key] = (value, DateTimeOffset.UtcNow.Add(lifetime));
            }
        }

        public void Remove(TKey key)
        {
            lock (gate) entries.Remove(key);
        }

        public void RemoveWhere(Func<TKey, bool> predicate)
        {
            lock (gate)
                foreach (var key in entries.Keys.Where(predicate).ToArray()) entries.Remove(key);
        }
    }
}
