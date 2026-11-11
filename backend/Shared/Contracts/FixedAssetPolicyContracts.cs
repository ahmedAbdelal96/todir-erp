using Shared.Primitives.Identifiers;

namespace Shared.Contracts.FixedAssets;

public sealed record FixedAssetPolicyDefaults(string ProviderCode, string ProviderVersion, string DepreciationMethod, string Convention, int? UsefulLifeMonths, decimal ResidualValue);

public interface IFixedAssetCountryPolicyProvider
{
    Task<FixedAssetPolicyDefaults?> GetBookDefaultsAsync(TenantId tenantId, Guid legalEntityId, string bookCode, CancellationToken cancellationToken = default);
}

public interface IFixedAssetRevaluationProvider
{
    string ProviderCode { get; }
    string ProviderVersion { get; }
    Task<bool> ValidateAsync(TenantId tenantId, Guid legalEntityId, Guid assetId, Guid bookId, decimal proposedGrossCost, decimal proposedResidualValue, DateOnly effectiveDate, CancellationToken cancellationToken = default);
}
