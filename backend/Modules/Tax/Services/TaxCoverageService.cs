using Modules.Tax.DTOs;

namespace Modules.Tax.Services;

public sealed record TaxCoverageFacts(
    Guid LegalEntityId,
    DateOnly At,
    string CountryCode,
    string Capability,
    string CountryPackStatus,
    string? RegistrationStatus,
    bool RegistrationOutOfPeriod,
    string PolicyStatus,
    string RoundingStatus,
    string ProductCoverage,
    string PartyReadiness,
    bool HasRuleSet,
    bool HasRuleConflict,
    bool HasApplicableRule,
    Guid? ProductTemplateId,
    Guid? PartyId);

/// <summary>Turns persisted readiness facts into the actionable Coverage result used by the API.</summary>
public static class TaxCoverageService
{
    public static TaxCoverageResult Evaluate(TaxCoverageFacts facts)
    {
        var issues = new List<TaxCoverageIssue>();
        if (facts.Capability != "Enabled") issues.Add(new("TaxCapabilityDisabled", "Tax is not enabled for this Legal Entity on the selected date."));
        if (facts.CountryPackStatus == "Missing") issues.Add(new("CountryPackMissing", $"No reviewed Country Pack covers {facts.CountryCode} on the selected date.", facts.CountryCode));
        else if (facts.CountryPackStatus == "Incompatible") issues.Add(new("CountryPackIncompatible", "The available Country Pack has not passed review.", facts.CountryCode));
        if (facts.RegistrationStatus is null && !facts.RegistrationOutOfPeriod) issues.Add(new("RegistrationMissing", "Add an active tax registration for this Legal Entity and scheme."));
        if (facts.RegistrationOutOfPeriod || facts.RegistrationStatus is not null and not "Registered") issues.Add(new("RegistrationNotEffective", "The registration does not cover the selected Tax Date."));
        if (facts.PolicyStatus == "Missing") issues.Add(new("RoundingPolicyMissing", "Configure the Legal Entity tax rounding policy."));
        if (facts.ProductCoverage == "Missing") issues.Add(new("ProductClassificationMissing", "Assign a Tax Category to the selected product for this date."));
        if (facts.ProductCoverage == "Expired") issues.Add(new("ProductClassificationExpired", "The product's Tax Category expired before the selected Tax Date."));
        if (facts.PartyReadiness == "Missing") issues.Add(new("PartyTaxDataMissing", "Add the selected party's Tax Profile for this date.", facts.PartyId?.ToString("D")));
        if (!facts.HasRuleSet) issues.Add(new("RuleSetMissing", "No active Tax Rule Set covers this Legal Entity, country, scheme, and date."));
        else if (facts.HasRuleConflict) issues.Add(new("RuleConflict", "The active Rule Set has equal-rank rules that produce contradictory outcomes."));
        else if ((facts.ProductTemplateId is not null || facts.PartyId is not null) && !facts.HasApplicableRule) issues.Add(new("MissingRule", "The active Rule Set has no applicable rule for this classified scenario."));
        var blockedCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "TaxCapabilityDisabled", "CountryPackMissing", "CountryPackIncompatible", "RegistrationMissing", "RegistrationNotEffective",
            "ProductClassificationMissing", "ProductClassificationExpired", "PartyTaxDataMissing", "RuleSetMissing", "MissingRule", "RuleConflict", "RoundingPolicyMissing"
        };
        var health = issues.Any(issue => blockedCodes.Contains(issue.Code)) ? "Blocked" : issues.Count == 0 ? "Green" : "Warning";
        return new(facts.LegalEntityId, facts.At, health, facts.Capability, facts.CountryPackStatus, facts.RegistrationStatus ?? "Missing",
            facts.RegistrationStatus == "Registered", facts.PolicyStatus == "Missing" ? "Missing" : "Configured",
            facts.RoundingStatus, facts.ProductCoverage, facts.PartyReadiness,
            facts.HasRuleSet ? "Active" : "Missing", issues);
    }
}
