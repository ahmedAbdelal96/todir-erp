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
    private static readonly DateTimeOffset AsOf = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

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
            var hr = await RunStageAsync(connection, tenant, context => HrSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var expenses = await RunStageAsync(connection, tenant, context => ExpensesSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var payroll = await RunStageAsync(connection, tenant, context => PayrollSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var catalog = await RunStageAsync(connection, tenant, context => CatalogSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var finance = await RunStageAsync(connection, tenant, context => FinanceBaseSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var pricing = await RunStageAsync(connection, tenant, context => PricingSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var tax = await RunStageAsync(connection, tenant, context => TaxSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var ledger = await RunStageAsync(connection, tenant, context => LedgerSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var subledger = await RunStageAsync(connection, tenant, context => SubledgerSeed.SeedAsync(context, cancellationToken), cancellationToken);
            _ = await RunStageAsync(connection, tenant, context => LedgerOpeningSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var invoicing = await RunStageAsync(connection, tenant, context => InvoicingSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var payments = await RunStageAsync(connection, tenant, context => PaymentsSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var inventory = await RunStageAsync(connection, tenant, context => InventorySeed.SeedAsync(context, cancellationToken), cancellationToken);
            var sales = await RunStageAsync(connection, tenant, context => SalesSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var crm = await RunStageAsync(connection, tenant, context => CrmSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var purchasing = await RunStageAsync(connection, tenant, context => PurchasingSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var accounting = await RunStageAsync(connection, tenant, context => AccountingSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var pos = await RunStageAsync(connection, tenant, context => PosSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var fixedAssets = await RunStageAsync(connection, tenant, context => FixedAssetsSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var projects = await RunStageAsync(connection, tenant, context => ProjectsSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var service = await RunStageAsync(connection, tenant, context => ServiceSeed.SeedAsync(context, cancellationToken), cancellationToken);
            var banking = await RunStageAsync(connection, tenant, context => BankingSeed.SeedAsync(context, cancellationToken), cancellationToken);
            summaries.Add(new(tenant, identity, directory, hr, expenses, payroll, catalog, finance, pricing, tax, ledger, subledger, invoicing, payments, inventory, sales, crm, purchasing, accounting, pos, fixedAssets, projects, service, banking));
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

public sealed record SeedTenantSummary(SeedTenant Tenant, SeedIdentitySummary Identity, SeedModuleSummary Directory, HrSeedSummary Hr, ExpensesSeedSummary Expenses, PayrollSeedSummary Payroll, SeedModuleSummary Catalog, SeedFinanceSummary Finance, SeedPricingSummary Pricing, SeedTaxSummary Tax, SeedLedgerSummary Ledger, SeedSubledgerSummary Subledger, SeedInvoiceSummary Invoicing, SeedPaymentSummary Payments, SeedInventorySummary Inventory, SeedSalesSummary Sales, CrmSeedSummary Crm, SeedPurchasingSummary Purchasing, SeedAccountingSummary Accounting, PosSeedSummary Pos, FixedAssetsSeedSummary FixedAssets, ProjectsSeedSummary Projects, ServiceSeedSummary Service, SeedBankingSummary Banking);
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
            output.WriteLine($"  HR: {tenant.Hr.Employees} employees, {tenant.Hr.Departments} departments, {tenant.Hr.Positions} positions, {tenant.Hr.ActiveEmployments} active employments, {tenant.Hr.TerminatedEmployments} terminated employment, {tenant.Hr.Contracts} contracts, {tenant.Hr.Renewals} renewal scenario, {tenant.Hr.CompensationRecords} compensation records, {tenant.Hr.InsuranceDrafts} insurance drafts, {tenant.Hr.Qualifications} qualifications, {tenant.Hr.Credentials} credentials, {tenant.Hr.EmployeeDocuments} employee documents");
            output.WriteLine($"  Expenses: {tenant.Expenses.Categories} categories, {tenant.Expenses.Expenses} expenses, {tenant.Expenses.FinalizedExpenses} finalized with reimbursement obligation");
            output.WriteLine($"  Payroll: {tenant.Payroll.Groups} groups, {tenant.Payroll.Periods} periods, {tenant.Payroll.Assignments} employee assignments, {tenant.Payroll.Components} components, {tenant.Payroll.Inputs} approved inputs");
            output.WriteLine($"  Products: {tenant.Catalog.Total}");
            output.WriteLine($"  Finance: {tenant.Finance.EnabledCurrencies} currencies, {tenant.Finance.PaymentTerms} payment terms");
            output.WriteLine($"  Pricing: {tenant.Pricing.PriceLists} lists, {tenant.Pricing.Entries} entries, {tenant.Pricing.ExplicitZeroPrices} explicit zero prices, {tenant.Pricing.ScheduledVersions} scheduled versions");
            output.WriteLine($"  Tax: {tenant.Tax.CapabilityEntities} legal entities (disabled and enabled), {tenant.Tax.RegisteredDevelopmentFixtures} synthetic registration, {tenant.Tax.UnreviewedCountryPacks} pack awaiting official review, {tenant.Tax.Categories} classifications");
            output.WriteLine($"  Ledger: {tenant.Ledger.LegalEntities} legal entities, {tenant.Ledger.Accounts} accounts, {tenant.Ledger.Periods} periods, invoice rule {(tenant.Ledger.HasInvoiceRule ? "ready" : "missing")}");
            output.WriteLine($"  Subledger: {tenant.Subledger.OpenItems} open items, {tenant.Subledger.Installments} installments, {tenant.Subledger.FinancialProfiles} financial profiles");
            output.WriteLine($"  Invoicing: {tenant.Invoicing.Drafts} drafts ({tenant.Invoicing.CustomerDrafts} customer, {tenant.Invoicing.SupplierDrafts} supplier)");
            output.WriteLine($"  Payments: {tenant.Payments.PaymentAccounts} operational accounts, {tenant.Payments.MethodConfigurations} enabled method configurations, {tenant.Payments.PendingProviderRequests} reference-provider requests awaiting customer action");
            output.WriteLine($"  Inventory: {tenant.Inventory.Warehouses} warehouses, {tenant.Inventory.ProductProfiles} profiles, {tenant.Inventory.ConfirmedMovements} movements, {tenant.Inventory.ActiveReservations} reservation, {tenant.Inventory.PostedCountAdjustments} posted count adjustment");
            output.WriteLine($"  Sales: {tenant.Sales.Quotations} quotations ({tenant.Sales.Draft} draft, {tenant.Sales.Open} open, {tenant.Sales.Accepted} accepted)");
            output.WriteLine($"  CRM: {tenant.Crm.Pipelines} pipeline, {tenant.Crm.Leads} leads, {tenant.Crm.Opportunities} opportunities");
            output.WriteLine($"  Purchasing: {tenant.Purchasing.PurchaseOrders} purchase orders ({tenant.Purchasing.PendingApproval} pending approval, {tenant.Purchasing.Approved} approved)");
            output.WriteLine($"  Accounting: {tenant.Accounting.GoLiveStatus} setup, {tenant.Accounting.StatementDefinitions} active statement definitions");
            output.WriteLine($"  POS: {tenant.Pos.Registers} registers, {tenant.Pos.Terminals} terminals, {tenant.Pos.OpenSessions} open sessions, {tenant.Pos.HeldCarts} held carts");
            output.WriteLine($"  Fixed Assets: {tenant.FixedAssets.Categories} categories, {tenant.FixedAssets.Assets} assets, {tenant.FixedAssets.Books} books");
            output.WriteLine($"  Projects: {tenant.Projects.Projects} projects, {tenant.Projects.Tasks} tasks, {tenant.Projects.TimeEntries} time entries, {tenant.Projects.CostFacts} cost facts, {tenant.Projects.BillableCharges} billable charges");
            output.WriteLine($"  Service: {tenant.Service.InstalledBaseItems} installed base, {tenant.Service.Requests} requests, {tenant.Service.WorkOrders} work orders, {tenant.Service.Visits} visits, {tenant.Service.Contracts} contracts, {tenant.Service.Plans} maintenance plans");
            output.WriteLine($"  Banking: {tenant.Banking.Statements} statements, {tenant.Banking.Reconciliations} reconciliation, {tenant.Banking.CashCounts} cash count, {tenant.Banking.Forecasts} forecast");
        }
    }
}
