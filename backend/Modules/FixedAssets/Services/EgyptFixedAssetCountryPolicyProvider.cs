using Shared.Contracts.FixedAssets;
using Shared.Primitives.Identifiers;

namespace Modules.FixedAssets.Services;

/// <summary>
/// Country-pack adapter for the initial Egypt fixed-assets policy seam. Core asset workflows
/// consume the contract and remain country-neutral; legal and tax policy belongs here.
/// </summary>
public sealed class EgyptFixedAssetCountryPolicyProvider : IFixedAssetCountryPolicyProvider, IFixedAssetRevaluationProvider
{
    public const string Code = "EGY_FIXED_ASSETS";
    public const string Version = "2026.1";

    public string ProviderCode => Code;
    public string ProviderVersion => Version;

    public Task<FixedAssetPolicyDefaults?> GetBookDefaultsAsync(TenantId tenantId, Guid legalEntityId, string bookCode, CancellationToken cancellationToken = default)
    {
        if (legalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(bookCode)) return Task.FromResult<FixedAssetPolicyDefaults?>(null);
        var normalized = bookCode.Trim().ToUpperInvariant();
        var defaults = normalized is "EG-TAX" or "TAX-EG"
            ? new FixedAssetPolicyDefaults(Code, Version, "StraightLine", "FullMonth", 60, 0m)
            : normalized == "CORP"
                ? new FixedAssetPolicyDefaults(Code, Version, "StraightLine", "FullMonth", 48, 0m)
                : null;
        return Task.FromResult(defaults);
    }

    public Task<bool> ValidateAsync(TenantId tenantId, Guid legalEntityId, Guid assetId, Guid bookId, decimal proposedGrossCost, decimal proposedResidualValue, DateOnly effectiveDate, CancellationToken cancellationToken = default)
    {
        var valid = tenantId.Value != Guid.Empty
            && legalEntityId != Guid.Empty
            && assetId != Guid.Empty
            && bookId != Guid.Empty
            && proposedGrossCost >= 0m
            && proposedResidualValue >= 0m
            && proposedResidualValue <= proposedGrossCost
            && effectiveDate != default;
        return Task.FromResult(valid);
    }
}
