using Core.Services;
using Core.Transactions;
using Modules.Ledger.Repositories;
using Npgsql;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Modules.Ledger.Tests;

public sealed class LedgerPostingIntegrationTests
{
    [Fact]
    public async Task InvoicePostingPersistsBalancedImmutableResultAndIsIdempotent()
    {
        var connectionString = LedgerTestSupport.RequireConnection();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var tenant = LedgerTestSupport.AtlasTenant;
        var entity = LedgerTestSupport.AtlasEntity;
        var engine = LedgerTestSupport.CreateRepository(dataSource);
        var testKey = Guid.NewGuid().ToString("N");
        var fact = new AccountingFactEnvelope<CustomerInvoicePostedPayload>(
            Guid.CreateVersion7(), tenant, entity, AccountingFactTypes.CustomerInvoicePosted, 1, "sales", $"INV-LEDGER-TEST-{testKey}", $"ledger-test-invoice-{testKey}", new(2026, 1, 15), new(2026, 1, 15), new(100m, 14m, 114m, "EGP", "CUSTOMER", "GENERAL", [new InvoiceTaxComponent("VAT14", 14m, 14m, "OutputTax")]), Guid.CreateVersion7(), Guid.CreateVersion7());
        var first = await engine.PostCustomerInvoiceAsync(fact);
        var second = await engine.PostCustomerInvoiceAsync(fact);
        Assert.False(first.AlreadyPosted);
        Assert.True(second.AlreadyPosted);
        Assert.Equal(first.JournalEntryId, second.JournalEntryId);
        Assert.Contains(first.Determinations!, item => item.AccountRole == "Receivable" && item.LegalEntityId == entity.Value);
        Assert.Contains(first.Determinations!, item => item.AccountRole == "SalesRevenue");
        Assert.Contains(first.Determinations!, item => item.AccountRole == "OutputTax");
        var conflict = fact with { IdempotencyKey = $"ledger-test-conflict-{testKey}", Payload = fact.Payload with { NetAmount = 101m, GrossAmount = 115m } };
        var conflictException = await Assert.ThrowsAsync<Modules.Ledger.Entities.LedgerDomainException>(() => engine.PostCustomerInvoiceAsync(conflict));
        Assert.Equal("ledger.fact_duplicate_conflict", conflictException.Code);

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var setting = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); setting.Parameters.AddWithValue(tenant.Value.ToString("D")); await setting.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand("select count(*),coalesce(sum(debit),0),coalesce(sum(credit),0) from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2", connection); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(first.JournalEntryId);
        await using (var reader = await command.ExecuteReaderAsync()) { Assert.True(await reader.ReadAsync()); Assert.Equal(3, reader.GetInt64(0)); Assert.Equal(reader.GetDecimal(1), reader.GetDecimal(2)); }
        await using var outbox = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and message_type='JournalEntryPostedV1' and payload->>'entryNumber'=$2", connection); outbox.Parameters.AddWithValue(tenant.Value); outbox.Parameters.AddWithValue(first.EntryNumber); Assert.Equal(1L, (long)(await outbox.ExecuteScalarAsync())!);
        await using var mutation = new NpgsqlCommand("update finance_ledger.journal_entry set description='tampered' where tenant_id=$1 and id=$2", connection); mutation.Parameters.AddWithValue(tenant.Value); mutation.Parameters.AddWithValue(first.JournalEntryId); var exception = await Assert.ThrowsAsync<PostgresException>(() => mutation.ExecuteNonQueryAsync()); Assert.Equal("55000", exception.SqlState);
    }
    [Fact]
    public async Task BankOriginatedFeeUsesSemanticMappingsAndIsIdempotent()
    {
        var connectionString = LedgerTestSupport.RequireConnection();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var key = Guid.NewGuid().ToString("N");
        var payload = new BankOriginatedFinancialEventPayload("BankFee", "Debit", 12.50m, "EGP", "treasury-account-acceptance");
        var fact = new AccountingFactEnvelope<IAccountingFactPayload>(Guid.CreateVersion7(), LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity,
            AccountingFactTypes.BankOriginatedFinancialEventApproved, 1, "banking", $"bank-originated:{key}", $"bank-originated:{key}",
            new(2026, 1, 16), new(2026, 1, 16), payload, Guid.CreateVersion7(), Guid.CreateVersion7());
        var first = await coordinator.ExecuteAsync(LedgerTestSupport.AtlasTenant, (context, ct) => repository.PostBankOriginatedInTransactionAsync(context, fact, payload, ct));
        var second = await coordinator.ExecuteAsync(LedgerTestSupport.AtlasTenant, (context, ct) => repository.PostBankOriginatedInTransactionAsync(context, fact, payload, ct));
        Assert.False(first.AlreadyPosted);
        Assert.True(second.AlreadyPosted);

        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        await using var command = new NpgsqlCommand("select count(*),coalesce(sum(debit),0),coalesce(sum(credit),0) from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2", connection);
        command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); command.Parameters.AddWithValue(first.JournalEntryId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2L, reader.GetInt64(0));
        Assert.Equal(reader.GetDecimal(1), reader.GetDecimal(2));
    }
}
