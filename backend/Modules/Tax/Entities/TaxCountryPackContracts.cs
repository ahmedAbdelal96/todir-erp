using NodaTime;

namespace Modules.Tax.Entities;

public sealed record TaxPackProvenance(string Authority, string SourceReference, string PackVersion, LocalDate EffectiveFrom, LocalDate? EffectiveTo, string ReviewStatus, string? Reviewer, Instant? ReviewedAt);
public sealed record TaxJurisdictionResult(string CountryCode, string JurisdictionCode, string SubdivisionType, string PlaceOfSupplyReason, TaxPackProvenance Provenance);
public sealed record TaxDateProviderResult(LocalDate Date, string Reason, string RuleVersion, TaxPackProvenance Provenance);
public sealed record TaxRegistrationValidation(bool IsValid, string? FailureCode, string? LegalRequirementCode, TaxPackProvenance Provenance);
public sealed record TaxDocumentRequirement(string Code, string Description, bool Required, TaxPackProvenance Provenance);

public enum TaxCountryPackStatus { Installed, Validated, Scheduled, Active, Superseded, Retired }
public sealed record TaxCountryPackCapabilityMetadata(bool Vat, bool Excise, bool Withholding, bool TaxGrouping, bool EInvoice);
public sealed record TaxCountryPackManifest(
    string PackId,
    string CountryCode,
    string PackVersion,
    string RequiredEngineContractVersion,
    TaxCountryPackStatus Status,
    LocalDate EffectiveFrom,
    LocalDate? EffectiveTo,
    string ReviewStatus,
    TaxCountryPackCapabilityMetadata Capabilities,
    IReadOnlyList<string> RuleSetReferences,
    string SourceAuthority,
    string SourceTitle,
    string SourceReference,
    LocalDate? PublicationDate,
    string? Reviewer,
    LocalDate? ReviewedOn,
    bool IsTestOnly = false);
public sealed record TaxCountryPackGoldenVector(
    string Scenario,
    string SourceReference,
    LocalDate EffectiveDate,
    TaxCalculationContext Input,
    TaxRuleCandidate Rule,
    TaxTreatment ExpectedTreatment,
    string ExpectedTaxableBase,
    string ExpectedTaxAmount,
    string? ExpectedLegalReasonCode,
    string PackVersion,
    string ReviewStatus);
public sealed record TaxCountryPackValidationIssue(string Code, string Message, string? ItemReference = null);
public sealed record TaxCountryPackValidationResult(bool Valid, IReadOnlyList<TaxCountryPackValidationIssue> Issues);

public sealed record TaxCountryPackPackage(TaxCountryPackManifest Manifest, IReadOnlyList<TaxRuleCandidate> Rules, IReadOnlyList<TaxCountryPackGoldenVector> GoldenVectors)
{
    public TaxCountryPackValidationResult Validate(string engineContractVersion, IReadOnlySet<string> availableRuleSetReferences)
    {
        var issues = new List<TaxCountryPackValidationIssue>();
        if (string.IsNullOrWhiteSpace(Manifest.PackId) || string.IsNullOrWhiteSpace(Manifest.CountryCode) || Manifest.CountryCode.Length != 2 || Manifest.CountryCode.Any(character => !char.IsAsciiLetterUpper(character)))
            issues.Add(new("tax.country_pack_manifest_invalid", "Pack identity and uppercase ISO country code are required."));
        if (string.IsNullOrWhiteSpace(Manifest.PackVersion) || string.IsNullOrWhiteSpace(Manifest.RequiredEngineContractVersion))
            issues.Add(new("tax.country_pack_manifest_invalid", "Pack and engine contract versions are required."));
        if (!string.Equals(Manifest.RequiredEngineContractVersion, engineContractVersion, StringComparison.Ordinal))
            issues.Add(new("tax.country_pack_incompatible", "The Country Pack requires a different Tax Engine contract version."));
        if (Manifest.EffectiveTo is { } end && end <= Manifest.EffectiveFrom)
            issues.Add(new("tax.country_pack_effective_period_invalid", "The Pack effective end must be later than its start."));
        if (Manifest.RuleSetReferences.Count == 0 || Manifest.RuleSetReferences.Any(reference => !availableRuleSetReferences.Contains(reference)))
            issues.Add(new("tax.country_pack_rule_set_missing", "Every declared Rule Set reference must resolve to an included or installed Rule Set."));
        if (string.IsNullOrWhiteSpace(Manifest.SourceAuthority) || string.IsNullOrWhiteSpace(Manifest.SourceTitle) || string.IsNullOrWhiteSpace(Manifest.SourceReference))
            issues.Add(new("tax.country_pack_evidence_missing", "Official source authority, title, and reference are required."));
        if (!Manifest.IsTestOnly && (Manifest.ReviewStatus != "Approved" || string.IsNullOrWhiteSpace(Manifest.Reviewer) || Manifest.ReviewedOn is null))
            issues.Add(new("tax.country_pack_review_required", "Statutory Pack content requires an approved review, reviewer, and review date."));
        if (Manifest.IsTestOnly && Manifest.ReviewStatus is not ("TestOnly" or "Approved"))
            issues.Add(new("tax.country_pack_test_label_required", "A reference Pack must be explicitly labelled TestOnly."));
        if (GoldenVectors.Count == 0)
            issues.Add(new("tax.country_pack_golden_vectors_missing", "At least one reviewed golden vector is required before validation."));
        foreach (var vector in GoldenVectors)
        {
            if (vector.PackVersion != Manifest.PackVersion || vector.EffectiveDate < Manifest.EffectiveFrom || Manifest.EffectiveTo is { } effectiveTo && vector.EffectiveDate >= effectiveTo)
                issues.Add(new("tax.country_pack_vector_invalid", "Golden vector version and effective date must match the Pack manifest.", vector.Scenario));
            if (string.IsNullOrWhiteSpace(vector.Scenario) || string.IsNullOrWhiteSpace(vector.SourceReference) || vector.ReviewStatus != (Manifest.IsTestOnly ? "TestOnly" : "Approved"))
                issues.Add(new("tax.country_pack_vector_evidence_missing", "Every vector needs a source and the required review status.", vector.Scenario));
            try
            {
                var result = TaxCalculator.Calculate(vector.Input, vector.EffectiveDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), "Registered", vector.Rule, Manifest.PackId, Manifest.PackVersion, null, null);
                var actualBase = result.TaxLines.Count == 0 ? "0" : result.TaxLines[0].TaxableBase;
                if (result.Treatment != vector.ExpectedTreatment || actualBase != vector.ExpectedTaxableBase || result.TaxTotal != vector.ExpectedTaxAmount || result.Explanation.LegalReasonCode != vector.ExpectedLegalReasonCode)
                    issues.Add(new("tax.country_pack_vector_failed", "The golden vector does not match the generic Tax Engine result.", vector.Scenario));
            }
            catch (TaxException exception) { issues.Add(new(exception.Code, exception.Message, vector.Scenario)); }
        }
        foreach (var group in Rules.GroupBy(rule => rule.EffectiveFrom))
        {
            try { _ = TaxRuleResolver.Select(group.Select(rule => new ResolvedTaxRuleCandidate(rule, Manifest.PackId, Manifest.PackVersion))); }
            catch (TaxException exception) { issues.Add(new(exception.Code, exception.Message)); }
        }
        foreach (var rule in Rules)
        foreach (var component in rule.Components)
            if (component.BaseComponentSequence is { } dependency && (dependency >= component.Sequence || !rule.Components.Any(candidate => candidate.Sequence == dependency)))
                issues.Add(new("tax.component_dependency_invalid", "Tax Stack dependencies must point to an existing earlier component.", component.TaxCode));
        return new(issues.Count == 0, issues);
    }
}

public static class TaxCountryPackLifecycle
{
    public static TaxCountryPackStatus Transition(TaxCountryPackManifest manifest, TaxCountryPackStatus target, TaxCountryPackValidationResult? validation = null)
    {
        var allowed = manifest.Status switch
        {
            TaxCountryPackStatus.Installed => target == TaxCountryPackStatus.Validated,
            TaxCountryPackStatus.Validated => target is TaxCountryPackStatus.Scheduled or TaxCountryPackStatus.Active,
            TaxCountryPackStatus.Scheduled => target is TaxCountryPackStatus.Active or TaxCountryPackStatus.Retired,
            TaxCountryPackStatus.Active => target is TaxCountryPackStatus.Superseded or TaxCountryPackStatus.Retired,
            TaxCountryPackStatus.Superseded => target == TaxCountryPackStatus.Retired,
            _ => false
        };
        if (!allowed) throw new TaxException("tax.country_pack_transition_invalid", $"A Country Pack cannot transition from {manifest.Status} to {target}.");
        if (target is TaxCountryPackStatus.Validated or TaxCountryPackStatus.Scheduled or TaxCountryPackStatus.Active && validation?.Valid != true)
            throw new TaxException("tax.country_pack_validation_required", "Country Pack validation must pass before scheduling or activation.");
        return target;
    }
}

public static class TaxCountryPackTimeline
{
    public static void EnsureNoApprovedOverlap(IEnumerable<TaxCountryPackManifest> manifests)
    {
        foreach (var group in manifests.Where(manifest => manifest.ReviewStatus == "Approved").GroupBy(manifest => (manifest.CountryCode,manifest.PackId)))
        {
            var ordered = group.OrderBy(manifest => manifest.EffectiveFrom).ThenBy(manifest => manifest.PackVersion,StringComparer.Ordinal).ToArray();
            for (var index = 1; index < ordered.Length; index++)
                if (ordered[index - 1].EffectiveTo is null || ordered[index - 1].EffectiveTo > ordered[index].EffectiveFrom)
                    throw new TaxException("tax.country_pack_overlap", "Approved Country Pack effective periods cannot overlap; activate a new version at a future boundary.");
        }
    }

    public static TaxCountryPackManifest Schedule(TaxCountryPackManifest manifest, LocalDate effectiveFrom, TaxCountryPackValidationResult validation)
    {
        if (!validation.Valid) throw new TaxException("tax.country_pack_validation_required", "An invalid Country Pack cannot be scheduled.");
        if (manifest.Status is not (TaxCountryPackStatus.Validated or TaxCountryPackStatus.Active) || effectiveFrom < manifest.EffectiveFrom || manifest.EffectiveTo is { } end && effectiveFrom >= end)
            throw new TaxException("tax.country_pack_schedule_invalid", "The scheduled effective date must be inside the validated Pack period.");
        return manifest with { Status = TaxCountryPackStatus.Scheduled, EffectiveFrom = effectiveFrom };
    }

    public static IReadOnlyList<TaxCountryPackManifest> ActivateForFuture(IReadOnlyList<TaxCountryPackManifest> history, string packId, string version, LocalDate effectiveFrom, TaxCountryPackValidationResult validation)
    {
        if (!validation.Valid) throw new TaxException("tax.country_pack_validation_required", "An invalid Country Pack cannot be activated.");
        var selectedIndex = history.ToList().FindIndex(manifest => manifest.PackId == packId && manifest.PackVersion == version);
        if (selectedIndex < 0) throw new TaxException("tax.country_pack_not_found", "The requested Country Pack version is not installed.");
        var selected = history[selectedIndex];
        if (selected.ReviewStatus != "Approved" || selected.Status == TaxCountryPackStatus.Retired || effectiveFrom < selected.EffectiveFrom || selected.EffectiveTo is { } end && effectiveFrom >= end)
            throw new TaxException("tax.country_pack_activation_invalid", "Only an approved, eligible Pack version can be activated for a future date.");
        var result = history.ToArray();
        for (var index = 0; index < result.Length; index++)
        {
            var current = result[index];
            if (index == selectedIndex) { result[index] = current with { Status = TaxCountryPackStatus.Active, EffectiveFrom = effectiveFrom }; continue; }
            if (current.CountryCode == selected.CountryCode && current.Status == TaxCountryPackStatus.Active && current.EffectiveFrom < effectiveFrom && (current.EffectiveTo is null || current.EffectiveTo > effectiveFrom))
                result[index] = current with { Status = TaxCountryPackStatus.Superseded, EffectiveTo = effectiveFrom };
        }
        EnsureNoApprovedOverlap(result.Where(manifest => manifest.Status is TaxCountryPackStatus.Active or TaxCountryPackStatus.Superseded));
        return result;
    }

    public static IReadOnlyList<TaxCountryPackManifest> RollbackForFuture(IReadOnlyList<TaxCountryPackManifest> history, string packId, string version, LocalDate effectiveFrom, TaxCountryPackValidationResult validation)
    {
        var target = history.FirstOrDefault(manifest => manifest.PackId == packId && manifest.PackVersion == version);
        if (target is null || target.Status != TaxCountryPackStatus.Superseded)
            throw new TaxException("tax.country_pack_rollback_invalid", "Rollback must reactivate a retained prior Pack version.");
        return ActivateForFuture(history, packId, version, effectiveFrom, validation);
    }

    public static TaxCountryPackManifest? Resolve(IEnumerable<TaxCountryPackManifest> history, string countryCode, LocalDate taxDate)
    {
        var matching = history.Where(manifest => manifest.CountryCode == countryCode && manifest.Status is TaxCountryPackStatus.Active or TaxCountryPackStatus.Superseded && manifest.EffectiveFrom <= taxDate && (manifest.EffectiveTo is null || taxDate < manifest.EffectiveTo)).ToArray();
        if (matching.Length > 1) throw new TaxException("tax.country_pack_conflict", "More than one active Country Pack version covers the requested Tax Date.");
        return matching.SingleOrDefault();
    }
}

/// <summary>Trusted, locally loaded country content. Implementations must be reviewed and versioned.</summary>
public interface ITaxCountryPack
{
    string CountryCode { get; }
    string PackCode { get; }
    string Version { get; }
    TaxCountryPackManifest Manifest { get; }
    TaxCountryPackPackage ValidationPackage { get; }
    TaxPackProvenance Provenance { get; }
    TaxDateProviderResult ResolveTaxDate(TaxCalculationContext context);
    TaxJurisdictionResult ResolveJurisdiction(TaxCalculationContext context, LocalDate taxDate);
    TaxRegistrationValidation ValidateRegistration(string schemeCode, string? registrationNumber, LocalDate taxDate);
    IReadOnlyList<TaxDocumentRequirement> GetDocumentRequirements(string documentKind, TaxTreatment treatment, LocalDate taxDate);
    IReadOnlyList<TaxRuleCandidate> GetApprovedRules(LocalDate taxDate);
}

public interface ITaxCountryPackRegistry
{
    ITaxCountryPack? Find(string countryCode, LocalDate at);
    IReadOnlyCollection<ITaxCountryPack> GetReviewedPacks();
}

public sealed class TaxCountryPackRegistry : ITaxCountryPackRegistry
{
    private readonly ITaxCountryPack[] reviewedPacks;

    public TaxCountryPackRegistry(IEnumerable<ITaxCountryPack> packs)
    {
        reviewedPacks = packs.Where(pack => pack.Provenance.ReviewStatus == "Approved").ToArray();
        var ordered = reviewedPacks.OrderBy(pack => pack.CountryCode,StringComparer.Ordinal).ThenBy(pack => pack.Provenance.EffectiveFrom).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            var previous = ordered[index - 1]; var current = ordered[index];
            if (string.Equals(previous.CountryCode,current.CountryCode,StringComparison.OrdinalIgnoreCase) && (previous.Provenance.EffectiveTo is null || previous.Provenance.EffectiveTo > current.Provenance.EffectiveFrom))
                throw new TaxException("tax.country_pack_overlap", "Reviewed Country Pack effective periods cannot overlap for one country.");
        }
    }

    public ITaxCountryPack? Find(string countryCode, LocalDate at) => reviewedPacks
        .Where(pack => string.Equals(pack.CountryCode,countryCode,StringComparison.OrdinalIgnoreCase) && pack.Provenance.EffectiveFrom <= at && (pack.Provenance.EffectiveTo is null || at < pack.Provenance.EffectiveTo))
        .OrderByDescending(pack => pack.Provenance.EffectiveFrom)
        .SingleOrDefault();

    public IReadOnlyCollection<ITaxCountryPack> GetReviewedPacks() => reviewedPacks;
}
