using Xunit;

namespace Tax.Tests;

public sealed class TaxPass3ASourceTests
{
    [Fact]
    public void CoverageUsesExplicitContextSharedConflictValidatorAndDistinctReadinessIssues()
    {
        var routes = Read("backend/Modules/Tax/Controllers/TaxModuleExtensions.cs");
        var repository = Read("backend/Modules/Tax/Repositories/TaxProfileRepository.cs");
        var coverageService = Read("backend/Modules/Tax/Services/TaxCoverageService.cs");
        Assert.Contains("/coverage", routes, StringComparison.Ordinal);
        Assert.Contains("new(legalEntityId, at, countryCode", routes, StringComparison.Ordinal);
        Assert.Contains("TaxRuleConflictValidator.HasConflictsAsync", repository, StringComparison.Ordinal);
        Assert.Contains("ProductClassificationExpired", coverageService, StringComparison.Ordinal);
        Assert.Contains("RegistrationNotEffective", coverageService, StringComparison.Ordinal);
        Assert.Contains("RuleSetMissing", coverageService, StringComparison.Ordinal);
        Assert.Contains("MissingRule", coverageService, StringComparison.Ordinal);
        Assert.Contains("RuleConflict", coverageService, StringComparison.Ordinal);
        Assert.Contains("ValidationPackage", repository, StringComparison.Ordinal);
        Assert.Contains("TaxCalculator.CalculationVersion", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileImportsRequireBothImportAndProfilePermissionsAndCommitAtomically()
    {
        var routes = Read("backend/Modules/Tax/Controllers/TaxModuleExtensions.cs");
        var repository = Read("backend/Modules/Tax/Repositories/TaxProfileImportRepository.cs");
        Assert.Contains("tax.import", routes, StringComparison.Ordinal);
        Assert.Contains("tax.product_profile.manage", routes, StringComparison.Ordinal);
        Assert.Contains("tax.party_profile.manage", routes, StringComparison.Ordinal);
        Assert.Contains("transactions.ExecuteAsync<TaxProfileImportCommitDto>", repository, StringComparison.Ordinal);
        Assert.Contains("Accepted preview rows commit atomically", repository, StringComparison.Ordinal);
        Assert.Contains("TaxProfilesImportedV1", repository, StringComparison.Ordinal);
        Assert.Contains("DuplicateInput", repository, StringComparison.Ordinal);
        Assert.Contains("CreateNewVersion", repository, StringComparison.Ordinal);
        Assert.Contains("IDirectoryImportCapabilities", repository, StringComparison.Ordinal);
        Assert.Contains("ResolveVariantsBySkuAsync", repository, StringComparison.Ordinal);
        Assert.Contains("ResolveVariantsByExternalReferenceAsync", repository, StringComparison.Ordinal);
        Assert.Contains("ValidateRegistration", repository, StringComparison.Ordinal);
        var previewBody = repository[repository.IndexOf("PreviewAsync", StringComparison.Ordinal)..repository.IndexOf("CommitAsync", StringComparison.Ordinal)];
        Assert.DoesNotContain("insert into foundation_tax.product_tax_profile", previewBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("insert into foundation_tax.party_tax_profile", previewBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TaxProfilesImportedV1", repository, StringComparison.Ordinal);
        Assert.Contains("for update", repository, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pg_advisory_xact_lock", repository, StringComparison.Ordinal);
        Assert.Contains("set effective_to=$3", repository, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("supersedes_id", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportUsesBusinessReferencesAndSeparateExportAndReadPermissions()
    {
        var routes = Read("backend/Modules/Tax/Controllers/TaxModuleExtensions.cs");
        var repository = Read("backend/Modules/Tax/Repositories/TaxProfileImportRepository.cs");
        Assert.Contains("tax.export", routes, StringComparison.Ordinal);
        Assert.Contains("tax.party_profile.read", routes, StringComparison.Ordinal);
        Assert.Contains("tax.product_profile.read", routes, StringComparison.Ordinal);
        Assert.Contains("GetVariantReferencesBatchAsync", repository, StringComparison.Ordinal);
        Assert.Contains("FindStablePartyReferencesAsync", repository, StringComparison.Ordinal);
        Assert.Contains("systemcode", Read("backend/Modules/Directory/Repositories/DirectoryRepository.cs"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProductScope,ProductReference,SKU,TaxCategoryCode", repository, StringComparison.Ordinal);
        Assert.Contains("PartyReference,RegistrationCountry", repository, StringComparison.Ordinal);
        Assert.Contains("RegistrationNumber", repository, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Repository source file was not found: {relativePath}");
    }
}
