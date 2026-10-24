using Modules.Ledger.Entities;
using Modules.Ledger.DTOs;
using Modules.Ledger.Repositories;
using Npgsql;
using Shared.Contracts.Ledger;
using Xunit;

namespace Modules.Ledger.Tests;

public sealed class LedgerDimensionValidationIntegrationTests
{
    [Fact]
    public async Task RequiredDimensionMissingAndOptionalDimensionAbsentFollowPolicy()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var posting = LedgerTestSupport.CreateRepository(dataSource);
        var operations = new LedgerOperationsRepository(dataSource, new Core.Transactions.NpgsqlTransactionCoordinator(dataSource), posting, TimeProvider.System);
        var journal = await ReadGuidAsync(dataSource, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL'");
        var debit = (await posting.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"97{Guid.NewGuid():N}"[..10], "حساب بُعد مطلوب", "Required dimension account", AccountType.Asset, "Current", null), default)).Id;
        var credit = (await posting.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"98{Guid.NewGuid():N}"[..10], "حساب بُعد اختياري", "Optional dimension account", AccountType.Revenue, "Current", null), default)).Id;
        var requiredDefinition = await operations.CreateDimensionDefinitionAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"REQUIRED-{Guid.NewGuid():N}", "مطلوب", "Required"), Guid.CreateVersion7(), default);
        var optionalDefinition = await operations.CreateDimensionDefinitionAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"OPTIONAL-{Guid.NewGuid():N}", "اختياري", "Optional"), Guid.CreateVersion7(), default);
        var requiredValue = await operations.CreateDimensionValueAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(requiredDefinition, "REQ", "مطلوب", "Required"), Guid.CreateVersion7(), default);
        await operations.CreateDimensionPolicyAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(requiredDefinition, "Account", debit.ToString("D"), "Required"), Guid.CreateVersion7(), default);
        await operations.CreateDimensionPolicyAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(optionalDefinition, "Account", credit.ToString("D"), "Optional"), Guid.CreateVersion7(), default);

        var preparer = Guid.CreateVersion7();
        var missing = await operations.CreateGeneralJournalDraftAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, Request(journal, debit, credit, "required-missing"), preparer, default);
        await operations.SubmitGeneralJournalAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, missing.Id, preparer, Guid.CreateVersion7(), default);
        await operations.ApproveGeneralJournalAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, missing.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var exception = await Assert.ThrowsAsync<LedgerDomainException>(() => operations.PostGeneralJournalAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, missing.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), default));
        Assert.Equal("ledger.dimension_missing", exception.Code);

        var optional = await operations.CreateGeneralJournalDraftAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, Request(journal, debit, credit, "optional-absent", requiredValue), preparer, default);
        await operations.SubmitGeneralJournalAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, optional.Id, preparer, Guid.CreateVersion7(), default);
        await operations.ApproveGeneralJournalAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, optional.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var posted = await operations.PostGeneralJournalAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, optional.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        Assert.False(posted.AlreadyPosted);

        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        Assert.Equal(0, await ScalarLongAsync(connection, "select count(*) from finance_ledger.journal_line_dimension d join finance_ledger.journal_entry_line l on l.tenant_id=d.tenant_id and l.legal_entity_id=d.legal_entity_id and l.id=d.journal_entry_line_id where d.tenant_id=$1 and d.legal_entity_id=$2 and l.journal_entry_id=$3 and l.account_id=$4", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value, posted.JournalEntryId, credit));
    }

    [Fact]
    public async Task ForbiddenSuppliedInactiveAndUnknownDimensionValuesAreRejected()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var posting = LedgerTestSupport.CreateRepository(dataSource);
        var operations = new LedgerOperationsRepository(dataSource, new Core.Transactions.NpgsqlTransactionCoordinator(dataSource), posting, TimeProvider.System);
        var journal = await ReadGuidAsync(dataSource, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL'");
        var debit = (await posting.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"96{Guid.NewGuid():N}"[..10], "حساب ممنوع", "Forbidden dimension account", AccountType.Asset, "Current", null), default)).Id;
        var credit = (await posting.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"95{Guid.NewGuid():N}"[..10], "حساب مقابل", "Counter account", AccountType.Revenue, "Current", null), default)).Id;
        var definition = await operations.CreateDimensionDefinitionAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"FORBIDDEN-{Guid.NewGuid():N}", "ممنوع", "Forbidden"), Guid.CreateVersion7(), default);
        var value = await operations.CreateDimensionValueAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(definition, "FORBID", "ممنوع", "Forbidden"), Guid.CreateVersion7(), default);
        await operations.CreateDimensionPolicyAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(definition, "Account", debit.ToString("D"), "Forbidden"), Guid.CreateVersion7(), default);
        var preparer = Guid.CreateVersion7();
        var forbidden = await operations.CreateGeneralJournalDraftAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, Request(journal, debit, credit, "forbidden-supplied", value), preparer, default);
        await operations.SubmitGeneralJournalAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, forbidden.Id, preparer, Guid.CreateVersion7(), default);
        await operations.ApproveGeneralJournalAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, forbidden.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var forbiddenException = await Assert.ThrowsAsync<LedgerDomainException>(() => operations.PostGeneralJournalAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, forbidden.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), default));
        Assert.Equal("ledger.dimension_invalid", forbiddenException.Code);

        var inactiveDefinition = await operations.CreateDimensionDefinitionAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"INACTIVE-{Guid.NewGuid():N}", "غير نشط", "Inactive"), Guid.CreateVersion7(), default);
        var inactiveValue = await operations.CreateDimensionValueAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(inactiveDefinition, "OLD", "قديم", "Old"), Guid.CreateVersion7(), default);
        await ExecuteAsync(dataSource, "update finance_ledger.dimension_value set status='Archived' where tenant_id=$1 and legal_entity_id=$2 and id=$3", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value, inactiveValue);
        var inactiveException = await Assert.ThrowsAsync<LedgerDomainException>(() => operations.CreateGeneralJournalDraftAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, Request(journal, debit, credit, "inactive-value", inactiveValue), preparer, default));
        Assert.Equal("ledger.dimension_invalid", inactiveException.Code);

        var unknownException = await Assert.ThrowsAsync<LedgerDomainException>(() => operations.CreateGeneralJournalDraftAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, Request(journal, debit, credit, "unknown-value", Guid.CreateVersion7()), preparer, default));
        Assert.Equal("ledger.dimension_invalid", unknownException.Code);
    }

    private static CreateGeneralJournalDraftRequest Request(Guid journal, Guid debit, Guid credit, string suffix, Guid? debitDimension = null) =>
        new(journal, new(2026, 9, 1), $"Dimension validation {suffix}", "EGP", $"dimension-validation-{suffix}-{Guid.NewGuid():N}", [new(debit, 100m, 0m, "EGP", 100m, 0m, 100m, 0m, 1m, debitDimension is { } value ? [value] : [], "debit"), new(credit, 0m, 100m, "EGP", 0m, 100m, 0m, 100m, 1m, [], "credit")]);

    private static async Task<Guid> ReadGuidAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); command.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(NpgsqlDataSource dataSource, string sql, params object[] values)
    {
        await using var connection = await dataSource.OpenConnectionAsync(); await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant); await using var command = new NpgsqlCommand(sql, connection); foreach (var value in values) command.Parameters.AddWithValue(value); await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarLongAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection); foreach (var value in values) command.Parameters.AddWithValue(value); return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
