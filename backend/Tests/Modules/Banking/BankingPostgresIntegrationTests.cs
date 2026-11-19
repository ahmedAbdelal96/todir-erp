using Modules.Banking.Repositories;
using Modules.Banking.DTOs;
using Modules.Banking.Entities;
using Shared.Primitives.Identifiers;
using Xunit;
using Npgsql;
using System.Text.Json;
using Shared.Contracts.Banking;
using Shared.Contracts.POS;

namespace Modules.Banking.Tests;

public sealed class BankingPostgresIntegrationTests
{
    [Fact]
    public async Task SeededTreasuryStatementsAndCashPositionAreReadThroughTheRepository()
    {
        var connectionString = RequireConnection();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");

        var accounts = await repository.ListAccountsAsync(tenant, entity, null, null, null, 1, 100, default);
        Assert.True(accounts.TotalCount >= 4);
        TreasuryAccountDto? accountWithStatement = null;
        StatementDto? statement = null;
        foreach (var account in accounts.Items)
        {
            var statements = (await repository.ListStatementsAsync(tenant, entity, account.Id, 1, 100, default)).Items;
            var candidate = statements.Count > 0 ? statements[0] : null;
            if (candidate is not null) { accountWithStatement = account; statement = candidate; break; }
        }
        Assert.NotNull(statement);
        Assert.NotNull(accountWithStatement);
        var lines = await repository.ListStatementLinesAsync(tenant, entity, statement!.Id, 1, 100, default);
        Assert.NotEmpty(lines.Items);
        var position = await repository.GetCashPositionAsync(tenant, entity, default);
        Assert.NotEmpty(position);
        Assert.All(position, item => Assert.False(string.IsNullOrWhiteSpace(item.BalanceBasis)));
    }

    [Fact]
    public async Task OpeningReconciliationPreservesMigrationProvenanceAndFinalizesOnlyWhenBalanced()
    {
        var connectionString = RequireConnection();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var account = (await repository.ListAccountsAsync(tenant, entity, null, null, null, 1, 100, default)).Items[0];
        var batch = Guid.CreateVersion7();
        var request = new CreateOpeningReconciliationRequest(entity, account.Id, batch, DateOnly.FromDateTime(DateTime.UtcNow.Date), account.CurrencyCode ?? "EGP", 1000m, 1025m, 25m, "LegacyBank", "LEGACY-ACCOUNT-001", $"MIG-{Guid.CreateVersion7():N}");
        var created = await repository.CreateOpeningReconciliationAsync(tenant, actor, request, default);
        Assert.Equal(0m, created.Difference);
        Assert.Equal("Draft", created.Status);
        Assert.Equal("LegacyBank", created.SourceSystem);
        var finalized = await repository.FinalizeOpeningReconciliationAsync(tenant, actor, entity, created.Id, new(created.Version), default);
        Assert.Equal("Finalized", finalized.Status);
        Assert.Equal(created.Id, (await repository.GetOpeningReconciliationAsync(tenant, entity, created.Id, default))!.Id);
    }

    [Fact]
    public async Task WorkflowCommandsValidateStatementsForecastsAndMakerCheckerChanges()
    {
        var connectionString = RequireConnection();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var account = (await repository.ListAccountsAsync(tenant, entity, null, null, null, 1, 100, default)).Items[0];
        var currency = account.CurrencyCode ?? "EGP";
        var workflowKey = Guid.CreateVersion7().ToString("N");
        var statement = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, account.Id, $"workflow-{workflowKey}", new($"workflow-statement-{workflowKey}", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), currency, 0m, 100m, "Acceptance", null, [new($"workflow-line-{workflowKey}", DateOnly.FromDateTime(DateTime.UtcNow), null, "Credit", 100m, currency, "workflow", null, null, "{}")]), default);
        var line = (await repository.ListStatementLinesAsync(tenant, entity, statement.Id, 1, 100, default)).Items[0];

        var classified = await repository.ClassifyStatementLineAsync(tenant, actor, entity, line.Id, new("Receipt", "Customer receipt candidate"), default);
        Assert.Equal("Receipt", classified.Classification);
        var candidate = await repository.CreatePaymentProposalAsync(tenant, actor, entity, line.Id, new(null), default);
        if (candidate.Status == "Proposed") candidate = await repository.DecideBankOriginatedCandidateAsync(tenant, actor, Guid.CreateVersion7(), entity, candidate.Id, new BankOriginatedDecisionRequest("Approved", 1), default);
        Assert.Equal("Approved", candidate.Status);
        var validated = await repository.ValidateStatementAsync(tenant, actor, entity, statement.Id, new(false), default);
        Assert.Equal("Validated", validated.ProcessingState);

        var forecastDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(Random.Shared.Next(1000, 100000));
        var version = await repository.CreateForecastVersionAsync(tenant, actor, new(entity, forecastDate, 13, "Draft"), default);
        var item = await repository.CreateForecastItemAsync(tenant, actor, new(entity, version.Id, account.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), "Inflow", 125m, "EGP", "Manual", "Acceptance forecast", null), default);
        Assert.Equal(version.Id, (await repository.GetForecastAsync(tenant, entity, version.AsOfDate, version.HorizonDays, default))!.Id);
        Assert.Equal(version.Id, (await repository.GetForecastVersionAsync(tenant, entity, version.Id, default))!.Id);
        Assert.Equal("Estimated", item.Confidence);

        using var payload = JsonDocument.Parse("{\"maskedIdentifier\":\"****1234\"}");
        var change = await repository.CreateAccountChangeRequestAsync(tenant, actor, entity, account.Id, new(payload.RootElement.Clone(), "Update masked bank identifier", account.Version), default);
        var approved = await repository.DecideAccountChangeRequestAsync(tenant, actor, entity, change.Id, new("Approved", change.Version), default);
        Assert.Equal("Approved", approved.Status);

        var connection = await repository.CreateConnectionAsync(tenant, actor, entity, account.Id, new($"acceptance-{Guid.CreateVersion7():N}", "connector://acceptance"), default);
        var disconnected = await repository.ChangeConnectionStateAsync(tenant, actor, entity, connection.Id, "Disconnected", connection.Version, default);
        Assert.Equal("Disconnected", disconnected.Status);

        await using var outboxConnection = await dataSource.OpenConnectionAsync();
        await using (var tenantCommand = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", outboxConnection))
        { tenantCommand.Parameters.AddWithValue(tenant.Value.ToString("D")); await tenantCommand.ExecuteNonQueryAsync(); }
        await using var outbox = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and message_type='BankOriginatedFinancialEventApprovedV1' and causation_id=$2", outboxConnection);
        outbox.Parameters.AddWithValue(tenant.Value); outbox.Parameters.AddWithValue(candidate.Id);
        Assert.Equal(1L, (long)(await outbox.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task InternalTransferEvidenceMovesFromInTransitToMatchedWithTwoDistinctStatementLines()
    {
        var connectionString = RequireConnection();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var accounts = (await repository.ListAccountsAsync(tenant, entity, null, null, null, 1, 100, default)).Items.Where(item => item.CurrencyCode is not null).GroupBy(item => item.CurrencyCode).FirstOrDefault(group => group.Count() >= 2)?.Take(2).ToArray() ?? throw new InvalidOperationException("The acceptance seed must contain two same-currency treasury accounts.");
        Assert.Equal(2, accounts.Length);
        var currency = accounts[0].CurrencyCode!; var key = Guid.CreateVersion7().ToString("N"); var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var sourceStatement = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, accounts[0].Id, $"transfer-source-{key}", new($"transfer-source-{key}", date, date, currency, 0m, -50m, "Acceptance", null, [new($"transfer-source-line-{key}", date, null, "Debit", 50m, currency, "internal transfer out", null, null, "{}")]), default);
        var destinationStatement = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, accounts[1].Id, $"transfer-destination-{key}", new($"transfer-destination-{key}", date, date, currency, 0m, 50m, "Acceptance", null, [new($"transfer-destination-line-{key}", date, null, "Credit", 50m, currency, "internal transfer in", null, null, "{}")]), default);
        var sourceLine = (await repository.ListStatementLinesAsync(tenant, entity, sourceStatement.Id, 1, 10, default)).Items.Single();
        var destinationLine = (await repository.ListStatementLinesAsync(tenant, entity, destinationStatement.Id, 1, 10, default)).Items.Single();
        var transferId = Guid.CreateVersion7();
        await repository.RecordTransferProjectionAsync(tenant, entity, transferId, accounts[0].Id, accounts[1].Id, 50m, 50m, currency, currency, "InTransit", currency, null, default);
        var projection = (await repository.ListTransferReconciliationsAsync(tenant, entity, null, "InTransit", 1, 10, default)).Items.Single(item => item.TransferReference == transferId.ToString("D"));
        var inTransit = await repository.ReconcileTransferAsync(tenant, actor, entity, projection.Id, new(sourceLine.Id, null, projection.Version, "Source bank debit observed"), default);
        Assert.Equal("InTransit", inTransit.State);
        var matched = await repository.ReconcileTransferAsync(tenant, actor, entity, projection.Id, new(null, destinationLine.Id, inTransit.Version, "Destination bank credit observed"), default);
        Assert.Equal("Matched", matched.State);
        Assert.Equal(0m, matched.InTransitAmount);
        Assert.Equal(50m, (await repository.ListStatementLinesAsync(tenant, entity, sourceStatement.Id, 1, 10, default)).Items.Single().ClaimedAmount);
        Assert.Equal(50m, (await repository.ListStatementLinesAsync(tenant, entity, destinationStatement.Id, 1, 10, default)).Items.Single().ClaimedAmount);
    }

    [Fact]
    public async Task ExternalFeedReplayIsIdempotentAndConflictingEvidenceIsRejected()
    {
        var connectionString = RequireConnection();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var account = (await repository.ListAccountsAsync(tenant, entity, null, null, null, 1, 100, default)).Items[0];
        var externalId = $"feed-{Guid.CreateVersion7():N}";
        var contentHash = Guid.CreateVersion7().ToString("N") + Guid.CreateVersion7().ToString("N");
        var request = new ExternalFeedTransactionReceivedV1(entity, account.Id, "BankApi", externalId,
            contentHash,
            DateOnly.FromDateTime(DateTime.UtcNow), "Credit", 175.50m, "EGP", $"idempotency-{externalId}", "{\"provider\":\"acceptance\"}");

        var first = await repository.RecordExternalFeedTransactionAsync(tenant, request, default);
        var replay = await repository.RecordExternalFeedTransactionAsync(tenant, request, default);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal("Projected", replay.Status);

        var conflict = request with { ContentHash = "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd" };
        var exception = await Assert.ThrowsAsync<BankingException>(() => repository.RecordExternalFeedTransactionAsync(tenant, conflict, default));
        Assert.Equal("banking.feed_conflict", exception.Code);

        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var tenantCommand = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        { tenantCommand.Parameters.AddWithValue(tenant.Value.ToString("D")); await tenantCommand.ExecuteNonQueryAsync(); }
        await using var command = new NpgsqlCommand("select count(*) from finance_banking.external_feed_transaction where tenant_id=$1 and treasury_account_id=$2 and external_transaction_id=$3", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(account.Id); command.Parameters.AddWithValue(externalId);
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task PosSessionCloseProjectionIsIdempotentAndPreservesCloseEvidence()
    {
        var connectionString = RequireConnection();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var register = Guid.Parse("4471b1bd-cefa-505c-8065-4e1db34e28e7");
        var branch = Guid.Parse("a6bee03b-acdc-6656-935c-3c88a5d90e61");
        var session = Guid.CreateVersion7();
        var sourceMessage = Guid.CreateVersion7();
        var close = new PosSessionClosedV1(entity, register, branch, session, DateOnly.FromDateTime(DateTime.UtcNow), "EGP", 100m, 98m, -2m);

        await repository.ProjectPosSessionClosedAsync(tenant, close, sourceMessage, default);
        await repository.ProjectPosSessionClosedAsync(tenant, close, sourceMessage, default);

        var conflict = close with { CountedCash = 99m, Variance = -1m, };
        var exception = await Assert.ThrowsAsync<BankingException>(() => repository.ProjectPosSessionClosedAsync(tenant, conflict, Guid.CreateVersion7(), default));
        Assert.Equal("banking.pos_settlement_conflict", exception.Code);
    }

    [Fact]
    public async Task ProviderSettlementProjectionPreservesGrossFeesAdjustmentsAndNetPayout()
    {
        var connectionString = RequireConnection();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var account = (await repository.ListAccountsAsync(tenant, entity, null, Shared.Contracts.Banking.TreasuryAccountType.ProviderSettlement, null, 1, 100, default)).Items[0];
        var reference = $"settlement-{Guid.CreateVersion7():N}";
        var settlement = new ProviderSettlementImportedV1(entity, account.Id, "CardProvider", reference, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 17), "EGP", 1000m, 25m, -5m, 970m, $"bank-payout-{reference}");
        var messageId = Guid.CreateVersion7();

        await repository.RecordProviderSettlementAsync(tenant, settlement, messageId, default);
        await repository.RecordProviderSettlementAsync(tenant, settlement, messageId, default);

        var conflict = settlement with { NetPayoutAmount = 969m, AdjustmentsAmount = -6m };
        var exception = await Assert.ThrowsAsync<BankingException>(() => repository.RecordProviderSettlementAsync(tenant, conflict, Guid.CreateVersion7(), default));
        Assert.Equal("banking.provider_settlement_conflict", exception.Code);
    }

    [Fact]
    public async Task ReconciliationStoresManyLinesAndManySystemMovementsWithConservation()
    {
        var connectionString = RequireConnection();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var account = (await repository.ListAccountsAsync(tenant, entity, null, null, null, 1, 100, default)).Items[0];
        var matchKey = Guid.CreateVersion7().ToString("N");
        var imported = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, account.Id, $"match-{matchKey}", new($"match-statement-{matchKey}", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), "EGP", 0m, 100m, "Acceptance", null, [
            new($"line-a-{matchKey}", DateOnly.FromDateTime(DateTime.UtcNow), null, "Credit", 60m, "EGP", "a", null, null, "{}"),
            new($"line-b-{matchKey}", DateOnly.FromDateTime(DateTime.UtcNow), null, "Credit", 40m, "EGP", "b", null, null, "{}")]), default);
        var lines = (await repository.ListStatementLinesAsync(tenant, entity, imported.Id, 1, 10, default)).Items;
        await Assert.ThrowsAsync<BankingException>(() => repository.CreateReconciliationAsync(tenant, actor, entity, new(entity, DateOnly.FromDateTime(DateTime.UtcNow), imported.Id, 100m, "many-to-many acceptance"), default));
        return;
    }

    [Fact]
    public async Task SettlementListsConfirmPosCashAndMatchProviderPayoutWithOptimisticVersion()
    {
        var connectionString = RequireConnection();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var cash = (await repository.ListAccountsAsync(tenant, entity, null, null, null, 1, 100, default)).Items.First(account => account.Type is TreasuryAccountType.Cash or TreasuryAccountType.PettyCash);
        var session = Guid.CreateVersion7();
        await repository.ProjectPosSessionClosedAsync(tenant, new(entity, Guid.CreateVersion7(), Guid.CreateVersion7(), session, new(2026, 11, 18), cash.CurrencyCode ?? "EGP", 100m, 98m, -2m), Guid.CreateVersion7(), default);
        var pending = await repository.ListPosCashSettlementsAsync(tenant, entity, 1, 25, "Pending", default);
        var pos = Assert.Single(pending.Items, item => item.PosSessionId == session);
        var confirmed = await repository.ConfirmPosCashSettlementAsync(tenant, actor, entity, pos.Id, new(cash.Id, pos.Version), default);
        Assert.Equal("Confirmed", confirmed.Status);

        var bankAccounts = (await repository.ListAccountsAsync(tenant, entity, null, TreasuryAccountType.Bank, null, 1, 100, default)).Items;
        Assert.NotEmpty(bankAccounts);
        var bank = bankAccounts[0];
        var key = Guid.CreateVersion7().ToString("N");
        var statement = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, bank.Id, $"provider-match-{key}",
            new($"provider-match-statement-{key}", new(2026, 11, 18), new(2026, 11, 18), bank.CurrencyCode ?? "EGP", 0m, 970m, "Acceptance", null,
                [new($"provider-match-line-{key}", new(2026, 11, 18), null, "Credit", 970m, bank.CurrencyCode ?? "EGP", "provider payout", null, null, "{}")]), default);
        var line = Assert.Single((await repository.ListStatementLinesAsync(tenant, entity, statement.Id, 1, 10, default)).Items);
        var providerAccounts = (await repository.ListAccountsAsync(tenant, entity, null, TreasuryAccountType.ProviderSettlement, null, 1, 100, default)).Items;
        Assert.NotEmpty(providerAccounts);
        var provider = providerAccounts[0];
        var reference = $"provider-match-{key}";
        await repository.RecordProviderSettlementAsync(tenant, new(entity, provider.Id, "CardProvider", reference, new(2026, 11, 1), new(2026, 11, 18), bank.CurrencyCode ?? "EGP", 1000m, 25m, -5m, 970m, $"payout-{key}"), Guid.CreateVersion7(), default);
        var listed = Assert.Single((await repository.ListProviderSettlementsAsync(tenant, entity, 1, 25, "Imported", default)).Items, item => item.ProviderReference == reference);
        var matched = await repository.MatchProviderSettlementAsync(tenant, actor, entity, listed.Id, new(line.Id, listed.Version), default);
        Assert.Equal("Matched", matched.Status);
        Assert.Equal(line.Id, matched.StatementLineId);
    }

    [Fact]
    public async Task BankActivityReportIsBoundedAndPreservesRemainingClaimableAmount()
    {
        var connectionString = RequireConnection();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var account = (await repository.ListAccountsAsync(tenant, entity, null, TreasuryAccountType.Bank, null, 1, 100, default)).Items[0];
        var key = Guid.CreateVersion7().ToString("N");
        var statement = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, account.Id, $"report-{key}",
            new($"report-statement-{key}", new(2026, 11, 18), new(2026, 11, 18), account.CurrencyCode ?? "EGP", 0m, 125m, "Acceptance", null,
                [new($"report-line-{key}", new(2026, 11, 18), null, "Credit", 125m, account.CurrencyCode ?? "EGP", "cheque", "CHQ", null, "{}")]), default);
        var line = Assert.Single((await repository.ListStatementLinesAsync(tenant, entity, statement.Id, 1, 10, default)).Items);
        await repository.ClassifyStatementLineAsync(tenant, actor, entity, line.Id, new("ReturnedCheque", "returned cheque evidence"), default);
        var report = await repository.ListBankActivityAsync(tenant, entity, account.Id, "ReturnedCheque", "Credit", new(2026, 11, 18), new(2026, 11, 18), 1, 10, default);
        var activity = Assert.Single(report.Items, item => item.StatementLineId == line.Id);
        Assert.Equal(125m, activity.RemainingAmount);
        Assert.Equal(1, report.Page);
        Assert.False(report.HasMore);
    }

    [Fact]
    public async Task ReconciliationRejectsClientSuppliedBookBalanceWithoutLedgerEvidence()
    {
        var connectionString = RequireConnection();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var account = (await repository.ListAccountsAsync(tenant, entity, null, TreasuryAccountType.Bank, null, 1, 100, default)).Items[0];
        var key = Guid.CreateVersion7().ToString("N");
        var amounts = new List<decimal> { 10m, 20m, 30m, 40m, 100m };
        var statementLines = amounts.Select((amount, index) => new ImportStatementLineRequest($"cardinality-line-{key}-{index}", new(2026, 11, 18), null, "Credit", amount, account.CurrencyCode ?? "EGP", $"line-{index}", null, null, "{}")).ToArray();
        var statement = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, account.Id, $"cardinality-{key}",
            new($"cardinality-statement-{key}", new(2026, 11, 18), new(2026, 11, 18), account.CurrencyCode ?? "EGP", 0m, 200m, "Acceptance", null,
                statementLines), default);
        await Assert.ThrowsAsync<BankingException>(() => repository.CreateReconciliationAsync(tenant, actor, entity, new(entity, new(2026, 11, 18), statement.Id, 200m, "forged book balance"), default));
    }

    [Fact]
    public async Task ProviderSettlementMatchingEnforcesDirectionAccountCurrencyAndAmountInvariants()
    {
        var connectionString = RequireConnection();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var actor = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var bankAccounts = (await repository.ListAccountsAsync(tenant, entity, null, TreasuryAccountType.Bank, null, 1, 100, default)).Items;
        Assert.NotEmpty(bankAccounts);
        var bank = bankAccounts[0];

        var nonBankAccounts = (await repository.ListAccountsAsync(tenant, entity, null, null, null, 1, 100, default)).Items.Where(a => a.Type != TreasuryAccountType.Bank).ToList();
        Assert.NotEmpty(nonBankAccounts);
        var nonBank = nonBankAccounts[0];

        var providerAccounts = (await repository.ListAccountsAsync(tenant, entity, null, TreasuryAccountType.ProviderSettlement, null, 1, 100, default)).Items;
        Assert.NotEmpty(providerAccounts);
        var provider = providerAccounts[0];

        var key = Guid.CreateVersion7().ToString("N");
        var reference = $"inv-test-{key}";
        var payoutAmount = 500m;
        var currency = bank.CurrencyCode ?? "EGP";

        await repository.RecordProviderSettlementAsync(tenant, new(entity, provider.Id, "Stripe", reference, new(2026, 11, 1), new(2026, 11, 18), currency, 520m, 20m, 0m, payoutAmount, $"payout-{key}"), Guid.CreateVersion7(), default);
        var settlement = Assert.Single((await repository.ListProviderSettlementsAsync(tenant, entity, 1, 25, "Imported", default)).Items, item => item.ProviderReference == reference);

        // 1. Debit statement line must be rejected (only incoming bank credit is allowed)
        var debitStmt = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, bank.Id, $"debit-stmt-{key}",
            new($"debit-stmt-{key}", new(2026, 11, 18), new(2026, 11, 18), currency, 0m, 500m, "Acceptance", null,
                [new($"debit-line-{key}", new(2026, 11, 18), null, "Debit", payoutAmount, currency, "outgoing debit", null, null, "{}")]), default);
        var debitLine = Assert.Single((await repository.ListStatementLinesAsync(tenant, entity, debitStmt.Id, 1, 10, default)).Items);
        var debitEx = await Assert.ThrowsAsync<BankingException>(() => repository.MatchProviderSettlementAsync(tenant, actor, entity, settlement.Id, new(debitLine.Id, settlement.Version), default));
        Assert.Equal("banking.provider_settlement_direction_invalid", debitEx.Code);

        // Assert settlement and statement line are completely unmutated
        var unmutatedSettlement = Assert.Single((await repository.ListProviderSettlementsAsync(tenant, entity, 1, 25, "Imported", default)).Items, item => item.ProviderReference == reference);
        Assert.Equal("Imported", unmutatedSettlement.Status);
        Assert.Null(unmutatedSettlement.StatementLineId);
        var unmutatedDebitLine = Assert.Single((await repository.ListStatementLinesAsync(tenant, entity, debitStmt.Id, 1, 10, default)).Items);
        Assert.Equal(0m, unmutatedDebitLine.ClaimedAmount);

        // 2. Non-bank account line must be rejected
        var nonBankStmt = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, nonBank.Id, $"nonbank-stmt-{key}",
            new($"nonbank-stmt-{key}", new(2026, 11, 18), new(2026, 11, 18), currency, 0m, 500m, "Acceptance", null,
                [new($"nonbank-line-{key}", new(2026, 11, 18), null, "Credit", payoutAmount, currency, "cash line", null, null, "{}")]), default);
        var nonBankLine = Assert.Single((await repository.ListStatementLinesAsync(tenant, entity, nonBankStmt.Id, 1, 10, default)).Items);
        var nonBankEx = await Assert.ThrowsAsync<BankingException>(() => repository.MatchProviderSettlementAsync(tenant, actor, entity, settlement.Id, new(nonBankLine.Id, settlement.Version), default));
        Assert.Equal("banking.provider_settlement_account_invalid", nonBankEx.Code);

        // 3. Currency mismatch must be rejected
        var otherCurrency = currency == "USD" ? "EUR" : "USD";
        var currMismatchStmt = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, bank.Id, $"curr-stmt-{key}",
            new($"curr-stmt-{key}", new(2026, 11, 18), new(2026, 11, 18), otherCurrency, 0m, 500m, "Acceptance", null,
                [new($"curr-line-{key}", new(2026, 11, 18), null, "Credit", payoutAmount, otherCurrency, "other currency line", null, null, "{}")]), default);
        var currMismatchLine = Assert.Single((await repository.ListStatementLinesAsync(tenant, entity, currMismatchStmt.Id, 1, 10, default)).Items);
        var currEx = await Assert.ThrowsAsync<BankingException>(() => repository.MatchProviderSettlementAsync(tenant, actor, entity, settlement.Id, new(currMismatchLine.Id, settlement.Version), default));
        Assert.Equal("banking.provider_settlement_currency_mismatch", currEx.Code);

        // 4. Insufficient claimable amount must be rejected
        var insufficientStmt = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, bank.Id, $"insufficient-stmt-{key}",
            new($"insufficient-stmt-{key}", new(2026, 11, 18), new(2026, 11, 18), currency, 0m, 200m, "Acceptance", null,
                [new($"insufficient-line-{key}", new(2026, 11, 18), null, "Credit", 200m, currency, "insufficient line", null, null, "{}")]), default);
        var insufficientLine = Assert.Single((await repository.ListStatementLinesAsync(tenant, entity, insufficientStmt.Id, 1, 10, default)).Items);
        var amountEx = await Assert.ThrowsAsync<BankingException>(() => repository.MatchProviderSettlementAsync(tenant, actor, entity, settlement.Id, new(insufficientLine.Id, settlement.Version), default));
        Assert.Equal("banking.provider_settlement_amount_mismatch", amountEx.Code);

        // 5. Valid credit line match must succeed
        var validStmt = await repository.ImportStatementAsync(tenant, actor, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, bank.Id, $"valid-stmt-{key}",
            new($"valid-stmt-{key}", new(2026, 11, 18), new(2026, 11, 18), currency, 0m, payoutAmount, "Acceptance", null,
                [new($"valid-line-{key}", new(2026, 11, 18), null, "Credit", payoutAmount, currency, "valid credit line", null, null, "{}")]), default);
        var validLine = Assert.Single((await repository.ListStatementLinesAsync(tenant, entity, validStmt.Id, 1, 10, default)).Items);
        var matched = await repository.MatchProviderSettlementAsync(tenant, actor, entity, settlement.Id, new(validLine.Id, settlement.Version), default);
        Assert.Equal("Matched", matched.Status);
        Assert.Equal(validLine.Id, matched.StatementLineId);

        var updatedValidLine = Assert.Single((await repository.ListStatementLinesAsync(tenant, entity, validStmt.Id, 1, 10, default)).Items);
        Assert.Equal(payoutAmount, updatedValidLine.ClaimedAmount);

        // 6. Duplicate match attempt must be rejected
        var dupEx = await Assert.ThrowsAsync<BankingException>(() => repository.MatchProviderSettlementAsync(tenant, actor, entity, matched.Id, new(validLine.Id, matched.Version), default));
        Assert.Equal("banking.provider_settlement_already_matched", dupEx.Code);
    }

    private static string RequireConnection()
    {
        var value = Environment.GetEnvironmentVariable("TODIR_BANKING_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("TODIR_BANKING_TEST_CONNECTION is required for Banking PostgreSQL acceptance; silent skips are forbidden.");
        var builder = new NpgsqlConnectionStringBuilder(value);
        if ((builder.Port != 15432 && builder.Port != 15433) || !(builder.Database ?? string.Empty).Contains("acceptance", StringComparison.OrdinalIgnoreCase) || builder.Database == "todir") throw new InvalidOperationException("Banking acceptance requires the disposable acceptance PostgreSQL database on port 15432 or 15433.");
        return value;
    }
}
