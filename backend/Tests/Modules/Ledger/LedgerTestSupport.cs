using Core.Services;
using Core.Transactions;
using Modules.Ledger.Repositories;
using Npgsql;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;
using System.Globalization;

namespace Modules.Ledger.Tests;

internal static class LedgerTestSupport
{
    internal static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    internal static readonly LegalEntityId AtlasEntity = new(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"));

    internal static string RequireConnection(string name = "TODIR_LEDGER_TEST_CONNECTION") =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {name} for Ledger acceptance tests.");

    internal static LedgerRepository CreateRepository(NpgsqlDataSource dataSource) =>
        new(dataSource, new NpgsqlTransactionCoordinator(dataSource), new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);

    internal static AccountingFactEnvelope<CustomerInvoicePostedPayload> Invoice(
        string key,
        DateOnly? postingDate = null,
        decimal net = 100m,
        decimal tax = 14m,
        IReadOnlyList<InvoiceTaxComponent>? taxes = null) =>
        new(Guid.CreateVersion7(), AtlasTenant, AtlasEntity, AccountingFactTypes.CustomerInvoicePosted, 1, "sales", $"INV-LEDGER-{key}", $"ledger-{key}", new(2026, 1, 15), postingDate ?? new(2026, 1, 15), new(net, tax, net + tax, "EGP", "CUSTOMER", "GENERAL", taxes ?? [new("VAT14", 14m, 14m, "OutputTax")]), Guid.CreateVersion7(), Guid.CreateVersion7());

    internal static async Task<long> ScalarLongAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    internal static async Task SetTenantAsync(NpgsqlConnection connection, TenantId tenant)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        command.Parameters.AddWithValue(tenant.Value.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }
}
