using Npgsql;

namespace DbMigrator.SeedData;

public static class DevelopmentSeedEnvironment
{
    public static string? Current => Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    public static void RequireSupported()
    {
        var environment = Current;
        if (string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(environment) || !(string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase) || string.Equals(environment, "Test", StringComparison.OrdinalIgnoreCase) || string.Equals(environment, "Testing", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Development seed is blocked. Set DOTNET_ENVIRONMENT=Development or Test; Production and an unspecified environment are never supported.");
    }
}

public static class SeedRunner
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    public static async Task<SeedRunSummary> RunAsync(NpgsqlConnection connection, CancellationToken cancellationToken = default)
    {
        DevelopmentSeedEnvironment.RequireSupported();
        var password = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD");
        if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("SEED_DEFAULT_PASSWORD is required for canonical development accounts. No fallback password is provided.");
        if (!Core.Identity.PasswordPolicy.IsValid(password)) throw new InvalidOperationException("SEED_DEFAULT_PASSWORD does not satisfy the existing password policy (12-128 characters with upper, lower, and numeric characters).");

        var protectedMfaSecret = Environment.GetEnvironmentVariable("SEED_MFA_SECRET_PROTECTED");
        var recoveryText = Environment.GetEnvironmentVariable("SEED_MFA_RECOVERY_CODE_DIGESTS");
        if (!string.IsNullOrWhiteSpace(protectedMfaSecret) != !string.IsNullOrWhiteSpace(recoveryText))
            throw new InvalidOperationException("SEED_MFA_SECRET_PROTECTED and SEED_MFA_RECOVERY_CODE_DIGESTS must be supplied together; MFA material is never generated or guessed by the seed.");
        var recoveryDigests = string.IsNullOrWhiteSpace(recoveryText) ? null : recoveryText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        await PlatformSeed.EnsureTenantsAsync(connection, AsOf, cancellationToken);
        var summaries = new List<SeedTenantSummary>();
        foreach (var tenant in PlatformSeed.Tenants)
        {
            var identity = await RunStageAsync(connection, tenant, context => IdentitySeed.SeedAsync(context, password, protectedMfaSecret, recoveryDigests, cancellationToken), cancellationToken);
            var directory = await RunStageAsync(connection, tenant, context => DirectorySeed.SeedAsync(context, cancellationToken), cancellationToken);
            var catalog = await RunStageAsync(connection, tenant, context => CatalogSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var finance = await RunStageAsync(connection, tenant, context => FinanceBaseSeed.SeedAsync(context, cancellationToken), cancellationToken);
            summaries.Add(new(tenant, identity, directory, catalog, finance));
        }
        return new(SeedIds.Version, summaries);
    }

    private static async Task<T> RunStageAsync<T>(NpgsqlConnection connection, SeedTenant tenant, Func<SeedContext, Task<T>> stage, CancellationToken cancellationToken)
    {
        await using var context = await SeedContext.OpenAsync(connection, tenant, AsOf, cancellationToken);
        try
        {
            var result = await stage(context);
            await context.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await context.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

public sealed record SeedTenantSummary(SeedTenant Tenant, SeedIdentitySummary Identity, SeedModuleSummary Directory, SeedModuleSummary Catalog, SeedFinanceSummary Finance);
public sealed record SeedRunSummary(string Version, IReadOnlyList<SeedTenantSummary> Tenants)
{
    public void Print(TextWriter output)
    {
        output.WriteLine($"Development Seed Ready (version {Version})");
        foreach (var tenant in Tenants)
        {
            output.WriteLine($"{tenant.Tenant.TradeName ?? tenant.Tenant.DisplayName}");
            output.WriteLine($"  Domain: {tenant.Tenant.LoginDomain}");
            output.WriteLine($"  Admin: admin@{tenant.Tenant.LoginDomain}");
            output.WriteLine($"  Users: {tenant.Identity.Total} (active {tenant.Identity.Active}, suspended {tenant.Identity.Suspended}, must-change-password {tenant.Identity.MustChangePassword}, mfa {(tenant.Identity.MfaEnabled ? "enabled" : "not seeded")})");
            output.WriteLine($"  Parties: {tenant.Directory.Total}");
            output.WriteLine($"  Products: {tenant.Catalog.Total}");
            output.WriteLine($"  Finance: {tenant.Finance.EnabledCurrencies} currencies, {tenant.Finance.PaymentTerms} payment terms");
        }
    }
}
