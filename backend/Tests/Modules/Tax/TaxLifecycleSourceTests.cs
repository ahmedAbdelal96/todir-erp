using Xunit;

namespace Tax.Tests;

public sealed class TaxLifecycleSourceTests
{
    [Fact]
    public void LifecycleRoutesUseSeparateReadManageAndActivationPermissions()
    {
        var routes = ReadRepositoryFile("backend/Modules/Tax/Controllers/TaxModuleExtensions.cs");
        Assert.Contains("RequireAuthorization()", routes, StringComparison.Ordinal);
        Assert.Contains("\"tax.registration.read\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"tax.registration.manage\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"tax.party_profile.read\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"tax.party_profile.manage\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"tax.product_profile.read\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"tax.product_profile.manage\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"tax.policy.read\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"tax.rule.read\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"tax.rule.manage\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"tax.rule.activate\"", routes, StringComparison.Ordinal);
    }

    [Fact]
    public void LifecycleMutationRepositoriesPersistAuditRows()
    {
        var registration = ReadRepositoryFile("backend/Modules/Tax/Repositories/TaxRegistrationRepository.cs");
        var profile = ReadRepositoryFile("backend/Modules/Tax/Repositories/TaxProfileRepository.cs");
        var rules = ReadRepositoryFile("backend/Modules/Tax/Repositories/TaxLifecycleRepository.cs");
        Assert.Contains("insert into foundation_tax.tax_audit", registration, StringComparison.Ordinal);
        Assert.Contains("insert into foundation_tax.tax_audit", profile, StringComparison.Ordinal);
        Assert.Contains("insert into foundation_tax.tax_audit", rules, StringComparison.Ordinal);
        Assert.Contains("RegistrationCreated", registration, StringComparison.Ordinal);
        Assert.Contains("RegistrationDeregistered", registration, StringComparison.Ordinal);
        Assert.Contains("RegistrationMembershipChanged", registration, StringComparison.Ordinal);
        Assert.Contains("PartyTaxProfileChanged", profile, StringComparison.Ordinal);
        Assert.Contains("ProductTaxProfileChanged", profile, StringComparison.Ordinal);
        Assert.Contains("RuleSetValidated", rules, StringComparison.Ordinal);
        Assert.Contains("RuleChanged", rules, StringComparison.Ordinal);
    }

    [Fact]
    public void LifecycleIntegrationEventsAreQueuedWithTransactionalMutations()
    {
        var registration = ReadRepositoryFile("backend/Modules/Tax/Repositories/TaxRegistrationRepository.cs");
        var profile = ReadRepositoryFile("backend/Modules/Tax/Repositories/TaxProfileRepository.cs");
        var rules = ReadRepositoryFile("backend/Modules/Tax/Repositories/TaxLifecycleRepository.cs");
        Assert.Contains("transactions.ExecuteAsync", registration, StringComparison.Ordinal);
        Assert.Contains("transactions.ExecuteAsync", profile, StringComparison.Ordinal);
        Assert.Contains("transactions.ExecuteAsync", rules, StringComparison.Ordinal);
        Assert.Contains("context.AddOutbox", registration, StringComparison.Ordinal);
        Assert.Contains("context.AddOutbox", profile, StringComparison.Ordinal);
        Assert.Contains("context.AddOutbox", rules, StringComparison.Ordinal);
        Assert.Contains("TaxRegistrationChangedV1", registration, StringComparison.Ordinal);
        Assert.Contains("PartyTaxProfileChangedV1", profile, StringComparison.Ordinal);
        Assert.Contains("ProductTaxProfileChangedV1", profile, StringComparison.Ordinal);
        Assert.Contains("TaxRuleSetActivatedV1", rules, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath)
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
