using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Modules.Ledger.Repositories;
using Npgsql;
using Shared.Contracts.Ledger;
using Xunit;

namespace Modules.Ledger.Tests;

public sealed class LedgerGeneralLedgerPaginationIntegrationTests
{
    [Fact]
    public async Task GeneralLedgerUsesStableMultiPageCursorAndHonorsFilterCombinations()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var posting = LedgerTestSupport.CreateRepository(dataSource);
        var operations = new LedgerOperationsRepository(dataSource, new Core.Transactions.NpgsqlTransactionCoordinator(dataSource), posting, TimeProvider.System);
        var journal = await ReadGuidAsync(dataSource, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL'");
        var debit = (await posting.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"94{Guid.NewGuid():N}"[..10], "حساب تقرير مدين", "Report debit", AccountType.Asset, "Current", null), default)).Id;
        var credit = (await posting.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"95{Guid.NewGuid():N}"[..10], "حساب تقرير دائن", "Report credit", AccountType.Revenue, "Current", null), default)).Id;
        var dimensionCode = $"REPORT-DIM-{Guid.NewGuid():N}";
        var definition = await operations.CreateDimensionDefinitionAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(dimensionCode, "تقرير", "Report dimension"), Guid.CreateVersion7(), default);
        var value = await operations.CreateDimensionValueAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(definition, "REPORT", "تقرير", "Report"), Guid.CreateVersion7(), default);
        var date = new DateOnly(2026, 5, 15);
        var sourceModule = $"ledger-pagination-{Guid.NewGuid():N}";
        var references = new List<string>();
        foreach (var index in Enumerable.Range(1, 9))
        {
            var reference = $"pagination-{index}-{Guid.NewGuid():N}";
            references.Add(reference);
            var payload = new ExplicitLedgerPostingPayload(journal, $"Pagination entry {index}", "EGP", [new(debit, 100m, 0m, "EGP", 100m, 0m, 100m, 0m, 1m, [new(dimensionCode, value, "REPORT")], "debit", reference), new(credit, 0m, 100m, "EGP", 0m, 100m, 0m, 100m, 1m, [], "credit", reference)]);
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(Guid.CreateVersion7(), LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, AccountingFactTypes.ManualGeneralJournalPosted, 1, sourceModule, reference, $"pagination-idempotency-{Guid.NewGuid():N}", date, date, payload, Guid.CreateVersion7(), Guid.CreateVersion7());
            await posting.PostAsync(fact, default);
        }

        var pages = new List<GeneralLedgerRowDto>();
        var pageCount = 0;
        string? cursor = null;
        do
        {
            var page = await operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, date, date, null, cursor, 3, default, journalId: journal, sourceModule: sourceModule);
            Assert.InRange(page.Items.Count, 1, 3);
            pages.AddRange(page.Items);
            pageCount++;
            if (page.NextCursor is not null) Assert.True(page.Items.Count == 3);
            cursor = page.NextCursor;
        } while (cursor is not null && pageCount < 20);

        Assert.True(pageCount >= 4);
        Assert.Equal(18, pages.Count);
        Assert.Equal(18, pages.Select(row => row.LineId).Distinct().Count());
        Assert.All(pages, row => Assert.Equal(sourceModule, row.SourceModule));
        for (var index = 1; index < pages.Count; index++) Assert.True(Compare(pages[index - 1], pages[index]) < 0, $"Cursor order regressed at {index}.");

        var accountRows = await ReadAllAsync(cursor => operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, null, null, debit, cursor, 4, default, sourceModule: sourceModule));
        Assert.Equal(9, accountRows.Count);
        Assert.All(accountRows, row => Assert.Equal(debit, row.AccountId));

        var dateRows = await ReadAllAsync(cursor => operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, date, date, null, cursor, 5, default, sourceModule: sourceModule));
        Assert.Equal(18, dateRows.Count);
        Assert.All(dateRows, row => Assert.Equal(date, row.PostingDate));

        var journalDateRows = await ReadAllAsync(cursor => operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, date, date, null, cursor, 5, default, journalId: journal, sourceModule: sourceModule));
        Assert.Equal(18, journalDateRows.Count);
        Assert.All(journalDateRows, row => Assert.Equal(sourceModule, row.SourceModule));

        var sourceRows = await ReadAllAsync(cursor => operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, null, null, null, cursor, 2, default, sourceReference: references[4]));
        Assert.Equal(2, sourceRows.Count);
        Assert.All(sourceRows, row => Assert.Equal(references[4], row.SourceReference));

        var dimensionRows = await ReadAllAsync(cursor => operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, null, null, null, cursor, 3, default, sourceModule: sourceModule, dimensionDefinitionId: definition, dimensionValueId: value));
        Assert.Equal(9, dimensionRows.Count);
        Assert.All(dimensionRows, row => Assert.Equal(debit, row.AccountId));

        var dimensionAccountRows = await ReadAllAsync(cursor => operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, date, date, debit, cursor, 2, default, journalId: journal, sourceModule: sourceModule, dimensionDefinitionId: definition, dimensionValueId: value));
        Assert.Equal(9, dimensionAccountRows.Count);
        Assert.All(dimensionAccountRows, row => Assert.Equal(debit, row.AccountId));
    }

    [Fact]
    public async Task GeneralLedgerOffsetPagesKeepRunningBalancesAndExposeStableTotals()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var posting = LedgerTestSupport.CreateRepository(dataSource);
        var operations = new LedgerOperationsRepository(dataSource, new Core.Transactions.NpgsqlTransactionCoordinator(dataSource), posting, TimeProvider.System);
        var journal = await ReadGuidAsync(dataSource, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL'");
        var debit = (await posting.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"96{Guid.NewGuid():N}"[..10], "حساب رصيد", "Balance account", AccountType.Asset, "Current", null), default)).Id;
        var credit = (await posting.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"97{Guid.NewGuid():N}"[..10], "حساب مقابل", "Offset account", AccountType.Revenue, "Current", null), default)).Id;
        var source = $"ledger-offset-{Guid.NewGuid():N}";
        for (var index = 1; index <= 4; index++)
        {
            var reference = $"offset-{index}-{Guid.NewGuid():N}";
            var payload = new ExplicitLedgerPostingPayload(journal, $"Offset entry {index}", "EGP", [new(debit, 10m, 0m, "EGP", 10m, 0m, 10m, 0m, 1m, [], "debit", reference), new(credit, 0m, 10m, "EGP", 0m, 10m, 0m, 10m, 1m, [], "credit", reference)]);
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(Guid.CreateVersion7(), LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, AccountingFactTypes.ManualGeneralJournalPosted, 1, source, reference, $"offset-idempotency-{Guid.NewGuid():N}", new DateOnly(2026, 5, 15), new DateOnly(2026, 5, 15), payload, Guid.CreateVersion7(), Guid.CreateVersion7());
            await posting.PostAsync(fact, default);
        }

        var first = await operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new DateOnly(2026, 5, 15), new DateOnly(2026, 5, 15), debit, null, 2, default, sourceModule: source, page: 1);
        var second = await operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new DateOnly(2026, 5, 15), new DateOnly(2026, 5, 15), debit, null, 2, default, sourceModule: source, page: 2);

        Assert.Equal(4, first.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(2, second.Items.Count);
        Assert.Equal(10m, first.Items[0].RunningBalance);
        Assert.Equal(20m, first.Items[1].RunningBalance);
        Assert.Equal(30m, second.Items[0].RunningBalance);
        Assert.Equal(40m, second.Items[1].RunningBalance);
    }

    private static async Task<List<GeneralLedgerRowDto>> ReadAllAsync(Func<string?, Task<GeneralLedgerPageDto>> read)
    {
        var rows = new List<GeneralLedgerRowDto>();
        string? cursor = null;
        for (var pageNumber = 0; pageNumber < 20; pageNumber++)
        {
            var page = await read(cursor);
            rows.AddRange(page.Items);
            cursor = page.NextCursor;
            if (cursor is null) return rows;
        }
        throw new Xunit.Sdk.XunitException("General ledger cursor did not terminate.");

    }

    private static int Compare(GeneralLedgerRowDto left, GeneralLedgerRowDto right)
    {
        var result = left.PostingDate.CompareTo(right.PostingDate); if (result != 0) return result;
        result = string.CompareOrdinal(left.EntryNumber, right.EntryNumber); if (result != 0) return result;
        result = left.Sequence.CompareTo(right.Sequence); if (result != 0) return result;
        return left.LineId.CompareTo(right.LineId);
    }

    private static async Task<Guid> ReadGuidAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var connection = await dataSource.OpenConnectionAsync(); await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant); await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); command.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); return (Guid)(await command.ExecuteScalarAsync())!;
    }
}
