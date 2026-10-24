using Core.Transactions;
using Modules.Ledger.Entities;
using Modules.Ledger.Repositories;
using Npgsql;
using Shared.Contracts.Ledger;
using Xunit;

namespace Modules.Ledger.Tests;

public sealed class LedgerAtomicityIntegrationTests
{
    [Fact]
    public async Task SourceStateFailureRollsBackTheWholePostingTransaction()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var fixture = await CreateFixtureAsync(dataSource);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var messageId = Guid.CreateVersion7();

        await Assert.ThrowsAsync<PostgresException>(() => coordinator.ExecuteAsync(LedgerTestSupport.AtlasTenant, async (context, ct) =>
        {
            await ExecuteAsync(context, "insert into platform.inbox_message (consumer_endpoint,message_id,tenant_id,received_at) values ($1,$2,$3,now())", "ledger.atomicity.source", messageId, LedgerTestSupport.AtlasTenant.Value);
            await ExecuteAsync(context, "insert into platform.inbox_message (consumer_endpoint,message_id,tenant_id,received_at) values ($1,$2,$3,now())", "ledger.atomicity.source", messageId, LedgerTestSupport.AtlasTenant.Value);
            return 0;
        }));

        await AssertNoResidueAsync(dataSource, fixture, messageId);
    }

    [Fact]
    public async Task AccountingFactPersistenceFailureRollsBackSourceAndPostingRows()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var fixture = await CreateFixtureAsync(dataSource);
        await using (var connection = await dataSource.OpenConnectionAsync())
        {
            await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
            await using var command = new NpgsqlCommand("insert into finance_ledger.accounting_fact (tenant_id,legal_entity_id,id,fact_type,fact_version,source_module,source_reference,idempotency_key,business_date,posting_date,payload_jsonb,payload_hash,status,correlation_id,actor_id,received_at) values ($1,$2,$3,$4,1,$5,$6,$7,'2026-01-15','2026-01-15','{}','preexisting','Received',$8,$9,now())", connection);
            command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); command.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); command.Parameters.AddWithValue(Guid.CreateVersion7()); command.Parameters.AddWithValue(fixture.Fact.FactType); command.Parameters.AddWithValue("atomicity-seed"); command.Parameters.AddWithValue($"seed-{Guid.NewGuid():N}"); command.Parameters.AddWithValue(fixture.Fact.IdempotencyKey); command.Parameters.AddWithValue(fixture.Fact.CorrelationId); command.Parameters.AddWithValue(fixture.Fact.ActorId); await command.ExecuteNonQueryAsync();
        }

        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        await Assert.ThrowsAsync<LedgerDomainException>(() => coordinator.ExecuteAsync(LedgerTestSupport.AtlasTenant, async (context, ct) =>
        {
            await InsertInboxAsync(context, fixture);
            await fixture.Repository.PostExplicitInTransactionAsync(context, fixture.Fact, fixture.Payload, ct);
            return 0;
        }));

        await AssertNoResidueAsync(dataSource, fixture);
    }

    [Fact]
    public async Task ValidationFailureRollsBackBeforeAnyAccountingRowsCommit()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var fixture = await CreateFixtureAsync(dataSource);
        var invalidPayload = fixture.Payload with { Lines = [fixture.Payload.Lines[0], fixture.Payload.Lines[1] with { BaseCredit = 99m, TransactionCredit = 99m, Credit = 99m }] };
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);

        await Assert.ThrowsAsync<LedgerDomainException>(() => coordinator.ExecuteAsync(LedgerTestSupport.AtlasTenant, async (context, ct) =>
        {
            await InsertInboxAsync(context, fixture);
            await fixture.Repository.PostExplicitInTransactionAsync(context, fixture.Fact, invalidPayload, ct);
            return 0;
        }));

        await AssertNoResidueAsync(dataSource, fixture);
    }

    [Fact]
    public async Task JournalHeaderFailureRollsBackThePersistedFactAndSourceState()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var fixture = await CreateFixtureAsync(dataSource);
        fixture = fixture with { Payload = fixture.Payload with { JournalId = Guid.CreateVersion7() } };
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);

        await Assert.ThrowsAsync<PostgresException>(() => coordinator.ExecuteAsync(LedgerTestSupport.AtlasTenant, async (context, ct) =>
        {
            await InsertInboxAsync(context, fixture);
            await fixture.Repository.PostExplicitInTransactionAsync(context, fixture.Fact, fixture.Payload, ct);
            return 0;
        }));

        await AssertNoResidueAsync(dataSource, fixture);
    }

    [Fact]
    public async Task JournalLineFailureRollsBackTheHeaderFirstLineFactAndSourceState()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var fixture = await CreateFixtureAsync(dataSource);
        var invalidLine = fixture.Payload.Lines[1] with { TransactionDebit = 0m, TransactionCredit = 0m };
        var invalidPayload = fixture.Payload with { Lines = [fixture.Payload.Lines[0], invalidLine] };
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);

        await Assert.ThrowsAsync<PostgresException>(() => coordinator.ExecuteAsync(LedgerTestSupport.AtlasTenant, async (context, ct) =>
        {
            await InsertInboxAsync(context, fixture);
            await fixture.Repository.PostExplicitInTransactionAsync(context, fixture.Fact, invalidPayload, ct);
            return 0;
        }));

        await AssertNoResidueAsync(dataSource, fixture);
    }

    [Fact]
    public async Task AuditFailureRollsBackTheSuccessfulPostingAndQueuedOutbox()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var fixture = await CreateFixtureAsync(dataSource);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var foreignTenant = Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb");

        await Assert.ThrowsAsync<PostgresException>(() => coordinator.ExecuteAsync(LedgerTestSupport.AtlasTenant, async (context, ct) =>
        {
            await InsertInboxAsync(context, fixture);
            await fixture.Repository.PostExplicitInTransactionAsync(context, fixture.Fact, fixture.Payload, ct);
            await ExecuteAsync(context, "insert into platform.audit_entry (id,tenant_id,legal_entity_id,actor_type,actor_id,module,action,entity_type,entity_id,source,correlation_id,occurred_at) values ($1,$2,$3,'User',$4,'ledger','atomicity.audit_failure','AccountingFact',$5,'test',$6,now())", Guid.CreateVersion7(), foreignTenant, LedgerTestSupport.AtlasEntity.Value, fixture.Fact.ActorId, fixture.Fact.FactId, fixture.Fact.CorrelationId);
            return 0;
        }));

        await AssertNoResidueAsync(dataSource, fixture);
    }

    [Fact]
    public async Task OutboxFailureRollsBackThePostedFactHeaderLinesAuditAndSourceState()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var fixture = await CreateFixtureAsync(dataSource);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var conflictingOutboxId = Guid.CreateVersion7();
        await using (var connection = await dataSource.OpenConnectionAsync())
        {
            await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
            await using var command = new NpgsqlCommand("insert into platform.outbox_message (id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,status) values ($1,'AtomicityConflict','{}',$2,now(),$3,$4,'ledger','Pending')", connection);
            command.Parameters.AddWithValue(conflictingOutboxId); command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); command.Parameters.AddWithValue(Guid.CreateVersion7()); command.Parameters.AddWithValue(Guid.CreateVersion7()); await command.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<PostgresException>(() => coordinator.ExecuteAsync(LedgerTestSupport.AtlasTenant, async (context, ct) =>
        {
            await InsertInboxAsync(context, fixture);
            await fixture.Repository.PostExplicitInTransactionAsync(context, fixture.Fact, fixture.Payload, ct);
            context.AddOutbox(new OutboxMessage(conflictingOutboxId, "AtomicityConflict", "{}", LedgerTestSupport.AtlasTenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "ledger"));
            return 0;
        }));

        await AssertNoResidueAsync(dataSource, fixture);
    }

    private sealed record AtomicFixture(LedgerRepository Repository, AccountingFactEnvelope<IAccountingFactPayload> Fact, ExplicitLedgerPostingPayload Payload, Guid SourceMessageId);

    private static async Task<AtomicFixture> CreateFixtureAsync(NpgsqlDataSource dataSource)
    {
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var debit = (await repository.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"99{Guid.NewGuid():N}"[..10], "اختبار الذرية", "Atomicity test", AccountType.Asset, "Current", null), default)).Id;
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        var journal = await ScalarGuidAsync(connection, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL'", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value);
        var credit = await ScalarGuidAsync(connection, "select id from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and code='410100'", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value);
        var sourceReference = $"atomicity-{Guid.NewGuid():N}";
        var sourceMessageId = Guid.CreateVersion7();
        var payload = new ExplicitLedgerPostingPayload(journal, "Atomicity proof", "EGP", [new(debit, 100m, 0m, "EGP", 100m, 0m, 100m, 0m, 1m, [], "debit", sourceReference), new(credit, 0m, 100m, "EGP", 0m, 100m, 0m, 100m, 1m, [], "credit", sourceReference)]);
        var fact = new AccountingFactEnvelope<IAccountingFactPayload>(Guid.CreateVersion7(), LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, AccountingFactTypes.ManualGeneralJournalPosted, 1, "ledger-atomicity", sourceReference, $"atomicity-idempotency-{Guid.NewGuid():N}", new(2026, 1, 15), new(2026, 1, 15), payload, Guid.CreateVersion7(), Guid.CreateVersion7());
        return new(repository, fact, payload, sourceMessageId);
    }

    private static async Task InsertInboxAsync(ITransactionContext context, AtomicFixture fixture) => await ExecuteAsync(context, "insert into platform.inbox_message (consumer_endpoint,message_id,tenant_id,received_at) values ($1,$2,$3,now())", "ledger.atomicity.source", fixture.SourceMessageId, LedgerTestSupport.AtlasTenant.Value);

    private static async Task AssertNoResidueAsync(NpgsqlDataSource dataSource, AtomicFixture fixture, Guid? sourceMessageId = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        Assert.Equal(0, await ScalarLongAsync(connection, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and (id=$2 or source_reference=$3)", LedgerTestSupport.AtlasTenant.Value, fixture.Fact.FactId, fixture.Fact.SourceReference));
        Assert.Equal(0, await ScalarLongAsync(connection, "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and (fact_id=$2 or source_reference=$3)", LedgerTestSupport.AtlasTenant.Value, fixture.Fact.FactId, fixture.Fact.SourceReference));
        Assert.Equal(0, await ScalarLongAsync(connection, "select count(*) from finance_ledger.journal_entry_line l join finance_ledger.journal_entry e on e.tenant_id=l.tenant_id and e.id=l.journal_entry_id where e.tenant_id=$1 and (e.fact_id=$2 or e.source_reference=$3)", LedgerTestSupport.AtlasTenant.Value, fixture.Fact.FactId, fixture.Fact.SourceReference));
        Assert.Equal(0, await ScalarLongAsync(connection, "select count(*) from platform.audit_entry where tenant_id=$1 and entity_id=$2", LedgerTestSupport.AtlasTenant.Value, fixture.Fact.FactId));
        Assert.Equal(0, await ScalarLongAsync(connection, "select count(*) from platform.outbox_message where tenant_id=$1 and causation_id=$2", LedgerTestSupport.AtlasTenant.Value, fixture.Fact.FactId));
        if (sourceMessageId is { } messageId) Assert.Equal(0, await ScalarLongAsync(connection, "select count(*) from platform.inbox_message where tenant_id=$1 and message_id=$2", LedgerTestSupport.AtlasTenant.Value, messageId));
        else Assert.Equal(0, await ScalarLongAsync(connection, "select count(*) from platform.inbox_message where tenant_id=$1 and message_id=$2", LedgerTestSupport.AtlasTenant.Value, fixture.SourceMessageId));
    }

    private static async Task ExecuteAsync(ITransactionContext context, string sql, params object?[] values)
    {
        await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction);
        foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> ScalarGuidAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> ScalarLongAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
