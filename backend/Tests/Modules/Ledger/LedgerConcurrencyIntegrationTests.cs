using Npgsql;
using Xunit;

namespace Modules.Ledger.Tests;

public sealed class LedgerConcurrencyIntegrationTests
{
    [Fact]
    public async Task ConcurrentDuplicateFactCreatesOneEntryAndOneOutboxMessage()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var fact = LedgerTestSupport.Invoice($"CONCURRENT-{Guid.NewGuid():N}");
        var first = LedgerTestSupport.CreateRepository(dataSource);
        var second = LedgerTestSupport.CreateRepository(dataSource);
        var results = await Task.WhenAll(first.PostCustomerInvoiceAsync(fact), second.PostCustomerInvoiceAsync(fact));
        Assert.Equal(results[0].JournalEntryId, results[1].JournalEntryId);
        Assert.Contains(results, result => !result.AlreadyPosted);
        Assert.Contains(results, result => result.AlreadyPosted);
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        Assert.Equal(1L, await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and fact_id=$2", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("fact", fact.FactId)));
        Assert.Equal(1L, await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from platform.outbox_message where tenant_id=$1 and message_type='JournalEntryPostedV1' and payload->>'factId'=$2", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("fact", fact.FactId.ToString("D"))));
    }

    [Fact]
    public async Task ResponseLossRetryReturnsTheCommittedEntryWithoutAnotherEvent()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var fact = LedgerTestSupport.Invoice($"RESPONSE-LOSS-{Guid.NewGuid():N}");
        var committed = await repository.PostCustomerInvoiceAsync(fact);
        var retried = await repository.PostCustomerInvoiceAsync(fact);
        Assert.True(retried.AlreadyPosted);
        Assert.Equal(committed.JournalEntryId, retried.JournalEntryId);
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        Assert.Equal(1L, await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and fact_id=$2", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("fact", fact.FactId)));
        Assert.Equal(1L, await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from platform.outbox_message where tenant_id=$1 and message_type='JournalEntryPostedV1' and payload->>'factId'=$2", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("fact", fact.FactId.ToString("D"))));
    }
}
