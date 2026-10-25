using Core.Transactions;
using Modules.Subledger.Repositories;
using Npgsql;
using Shared.Contracts.Subledger;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Subledger.Tests;

public sealed class SubledgerPostgresIntegrationTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    private static readonly TenantId OtherTenant = new(Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb"));
    private static readonly LegalEntityId Entity = new(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"));

    [Fact]
    public async Task RealPostgresCreatesAppliesRetriesReversesAndSerializesCompetingApplications()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var partyId = await FindActivePartyAsync(dataSource);
        var suffix = Guid.NewGuid().ToString("N");
        var debitId = await CreateAsync(repository, partyId, "Invoice", "Debit", $"SUBLEDGER-AR-{suffix}", 100m);
        var creditId = await CreateAsync(repository, partyId, "Payment", "Credit", $"SUBLEDGER-AR-CREDIT-{suffix}", 100m);
        Assert.Equal(0L, await CountVisibleFromTenantAsync(dataSource, Guid.CreateVersion7(), debitId));
        var payment = Snapshot($"SUBLEDGER-AR-CREDIT-{suffix}", 100m);
        var replay = await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable, Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", [payment], default);
        Assert.Equal(creditId, replay.Single());
        await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => repository.CreateOpenItemsAsync(Tenant, Entity, partyId,
            Modules.Subledger.Entities.SubledgerDirection.Receivable, Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment",
            [Snapshot($"SUBLEDGER-AR-CREDIT-{suffix}", 101m)], default));

        var apply = new ApplyOpenItemsCommand(Entity, partyId, SubledgerDirectionContract.Receivable, $"subledger-apply-{suffix}", new(2026, 10, 25), new(2026, 10, 25), [new(debitId, creditId, "40")], Guid.CreateVersion7(), Guid.CreateVersion7());
        var applied = await repository.ApplyAsync(Tenant, apply, [40m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);
        var retry = await repository.ApplyAsync(Tenant, apply, [40m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);
        Assert.False(applied.AlreadyApplied);
        Assert.True(retry.AlreadyApplied);
        Assert.Single(retry.ApplicationIds);
        await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => repository.ApplyAsync(Tenant,
            apply with { Lines = [new(debitId, creditId, "41")] }, [41m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default));

        var reverse = new ReverseApplicationCommand(Entity, applied.ApplicationIds.Single(), $"subledger-reverse-{suffix}", new(2026, 10, 25), "Integration reversal", Guid.CreateVersion7(), Guid.CreateVersion7());
        var reversalAttempts = await Task.WhenAll(repository.ReverseAsync(Tenant, reverse, default), repository.ReverseAsync(Tenant, reverse, default));
        Assert.Single(reversalAttempts, result => !result.AlreadyApplied);
        Assert.Single(reversalAttempts, result => result.AlreadyApplied);
        Assert.Equal(reversalAttempts[0].ApplicationIds, reversalAttempts[1].ApplicationIds);
        Assert.Equal(1L, await CountReversalsForApplicationAsync(dataSource, applied.ApplicationIds.Single()));
        Assert.Equal(100m, await ReadOpenAmountAsync(dataSource, debitId));
        Assert.Equal(100m, await ReadOpenAmountAsync(dataSource, creditId));

        var installmentSource = $"SUBLEDGER-INSTALLMENT-{suffix}";
        var installments = await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice",
            [Snapshot(installmentSource, 30m, "A"), Snapshot(installmentSource, 40m, "B"), Snapshot(installmentSource, 30m, "C")], default);
        Assert.Equal(3, installments.Distinct().Count());
        var installmentSnapshots = new[] { Snapshot(installmentSource, 30m, "A"), Snapshot(installmentSource, 40m, "B"), Snapshot(installmentSource, 30m, "C") };
        var installmentReplay = await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", installmentSnapshots, default);
        Assert.Equal(installments, installmentReplay);
        var installmentFacts = await ReadInstallmentFactsAsync(dataSource, installmentSource);
        Assert.Equal(3, installmentFacts.Count);
        Assert.Equal(3, installmentFacts.DistinctScheduleCount);
        Assert.Equal(100m, installmentFacts.TotalAmount);

        var billId = await CreateAsync(repository, partyId, "Bill", "Credit", $"SUBLEDGER-AP-BILL-{suffix}", 100m,
            Modules.Subledger.Entities.SubledgerDirection.Payable);
        var supplierPaymentId = await CreateAsync(repository, partyId, "Payment", "Debit", $"SUBLEDGER-AP-PAYMENT-{suffix}", 100m,
            Modules.Subledger.Entities.SubledgerDirection.Payable);
        var payableApplication = await repository.ApplyAsync(Tenant,
            new ApplyOpenItemsCommand(Entity, partyId, SubledgerDirectionContract.Payable, $"subledger-ap-apply-{suffix}", new(2026, 10, 25), new(2026, 10, 25), [new(supplierPaymentId, billId, "60")], Guid.CreateVersion7(), Guid.CreateVersion7()),
            [60m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);
        Assert.Equal(40m, await ReadOpenAmountAsync(dataSource, billId));
        var payableReversal = await repository.ReverseAsync(Tenant,
            new ReverseApplicationCommand(Entity, payableApplication.ApplicationIds.Single(), $"subledger-ap-reverse-{suffix}", new(2026, 10, 25), "AP integration reversal", Guid.CreateVersion7(), Guid.CreateVersion7()), default);
        Assert.Single(payableReversal.ApplicationIds);
        Assert.Equal(100m, await ReadOpenAmountAsync(dataSource, billId));

        var target = await CreateAsync(repository, partyId, "Invoice", "Debit", $"SUBLEDGER-RACE-TARGET-{suffix}", 100m);
        var sourceA = await CreateAsync(repository, partyId, "Payment", "Credit", $"SUBLEDGER-RACE-A-{suffix}", 80m);
        var sourceB = await CreateAsync(repository, partyId, "Payment", "Credit", $"SUBLEDGER-RACE-B-{suffix}", 80m);
        var attempts = new[] { sourceA, sourceB }.Select((source, index) => repository.ApplyAsync(Tenant,
            new ApplyOpenItemsCommand(Entity, partyId, SubledgerDirectionContract.Receivable, $"subledger-race-{suffix}-{index}", new(2026, 10, 25), new(2026, 10, 25), [new(target, source, "70")], Guid.CreateVersion7(), Guid.CreateVersion7()),
            [70m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default));
        var outcomes = await Task.WhenAll(attempts.Select(async task =>
        {
            try { await task; return true; }
            catch (Modules.Subledger.Entities.SubledgerException) { return false; }
        }));
        Assert.Single(outcomes, success => success);
        Assert.Equal(30m, await ReadOpenAmountAsync(dataSource, target));
        Assert.Equal(70m, await SumAppliedAsync(dataSource, target));
        Assert.Equal(1L, await CountBatchAsync(dataSource, "Apply", $"subledger-race-{suffix}-0") + await CountBatchAsync(dataSource, "Apply", $"subledger-race-{suffix}-1"));
        Assert.True((await ReadItemStateAsync(dataSource, Entity, sourceA)).OpenAmount >= 0m);
        Assert.True((await ReadItemStateAsync(dataSource, Entity, sourceB)).OpenAmount >= 0m);
    }

    [Fact]
    public async Task RuntimeRoleRlsHidesSubledgerRowsWithoutOwningTheSchema()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("select current_user, r.rolsuper, r.rolbypassrls, c.relrowsecurity, c.relforcerowsecurity from pg_roles r join pg_class c on c.oid='finance_subledger.party_open_item'::regclass where r.rolname=current_user", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("erp_app", reader.GetString(0));
        Assert.False(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.True(reader.GetBoolean(4));
    }

    [Fact]
    public async Task EveryTenantOwnedSubledgerTableHidesOtherTenantDataAndMutations()
    {
        var (dataSource, repository) = CreateRuntimeRepository();
        await using (dataSource)
        {
            var party = await FindPartyWithoutProfileAsync(dataSource);
            var suffix = Guid.NewGuid().ToString("N");
            await repository.SaveProfileAsync(Tenant, new(Entity, party, null, null, null, null, null, null, 0), null, null, default);
            var debit = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-RLS-D-{suffix}", 1_000m);
            var credit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-RLS-C-{suffix}", 1_000m);
            var application = await ApplyAsync(repository, $"subledger-rls-apply-{suffix}", [new(debit, credit, "500")], party);
            await repository.ReverseAsync(Tenant, new ReverseApplicationCommand(Entity, application.ApplicationIds.Single(), $"subledger-rls-reverse-{suffix}", new(2026, 10, 25), "RLS fixture", Guid.CreateVersion7(), Guid.CreateVersion7()), default);

            await using var connection = await dataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
            {
                setTenant.Parameters.AddWithValue(OtherTenant.Value.ToString("D"));
                await setTenant.ExecuteNonQueryAsync();
            }
            await using var catalog = new NpgsqlCommand("select count(*), bool_and(c.relrowsecurity), bool_and(c.relforcerowsecurity), bool_and(pg_get_userbyid(c.relowner)<>'erp_app') from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='finance_subledger' and c.relkind='r'", connection, transaction);
            await using (var reader = await catalog.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync());
                Assert.Equal(5, reader.GetInt64(0));
                Assert.True(reader.GetBoolean(1));
                Assert.True(reader.GetBoolean(2));
                Assert.True(reader.GetBoolean(3));
            }
            foreach (var table in new[] { "party_financial_profile", "party_open_item", "application_batch", "open_item_application", "application_reversal" })
            {
                await using var count = new NpgsqlCommand($"select count(*) from finance_subledger.{table} where tenant_id=$1", connection, transaction);
                count.Parameters.AddWithValue(Tenant.Value);
                Assert.Equal(0L, Convert.ToInt64(await count.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
            }
            await using var itemMutation = new NpgsqlCommand("update finance_subledger.party_open_item set open_transaction_amount=0 where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
            itemMutation.Parameters.AddWithValue(Tenant.Value);
            itemMutation.Parameters.AddWithValue(Entity.Value);
            itemMutation.Parameters.AddWithValue(debit);
            Assert.Equal(0, await itemMutation.ExecuteNonQueryAsync());
            await using var profileMutation = new NpgsqlCommand("update finance_subledger.party_financial_profile set version=version+1 where tenant_id=$1 and legal_entity_id=$2 and party_id=$3", connection, transaction);
            profileMutation.Parameters.AddWithValue(Tenant.Value);
            profileMutation.Parameters.AddWithValue(Entity.Value);
            profileMutation.Parameters.AddWithValue(party);
            Assert.Equal(0, await profileMutation.ExecuteNonQueryAsync());
            await transaction.RollbackAsync();
        }
    }

    [Fact]
    public async Task TenantEntityPartyAndHistoricalControlBoundariesRejectWithoutChangingBalances()
    {
        var (dataSource, repository) = CreateRuntimeRepository();
        await using (dataSource)
        {
            var party = await FindActivePartyAsync(dataSource);
            var otherParty = await FindAnotherActivePartyAsync(dataSource, party);
            var otherEntity = await FindAnotherEntityAsync(dataSource);
            var suffix = Guid.NewGuid().ToString("N");

            var entityCredit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-ENTITY-CREDIT-{suffix}", 10_000m);
            var entityDebit = await CreateForEntityAsync(repository, otherEntity, party, "Invoice", "Debit", $"SUBLEDGER-ENTITY-DEBIT-{suffix}", 10_000m);
            var crossEntityKey = $"subledger-entity-isolation-{suffix}";
            var crossEntity = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => ApplyAsync(repository, crossEntityKey,
                [new(entityDebit, entityCredit, "10000")], party));
            Assert.Equal("subledger.open_item_not_found", crossEntity.Code);
            Assert.Equal(0L, await CountBatchAsync(dataSource, "Apply", crossEntityKey));
            Assert.Equal(0L, await CountApplicationsForItemsAsync(dataSource, [entityDebit, entityCredit]));
            Assert.Equal(10_000m, (await ReadItemStateAsync(dataSource, Entity, entityCredit)).OpenAmount);
            Assert.Equal(10_000m, (await ReadItemStateAsync(dataSource, otherEntity, entityDebit)).OpenAmount);

            var originalDebit = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-ENTITY-REVERSE-DEBIT-{suffix}", 10_000m);
            var originalCredit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-ENTITY-REVERSE-CREDIT-{suffix}", 10_000m);
            var original = await ApplyAsync(repository, $"subledger-entity-original-{suffix}", [new(originalDebit, originalCredit, "4000")], party);
            var wrongEntityReverse = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => repository.ReverseAsync(Tenant,
                new ReverseApplicationCommand(otherEntity, original.ApplicationIds.Single(), $"subledger-entity-reverse-{suffix}", new(2026, 10, 25), "Wrong legal entity", Guid.CreateVersion7(), Guid.CreateVersion7()), default));
            Assert.Equal("subledger.application_not_found", wrongEntityReverse.Code);
            Assert.Equal(6_000m, (await ReadItemStateAsync(dataSource, Entity, originalDebit)).OpenAmount);
            Assert.Equal(6_000m, (await ReadItemStateAsync(dataSource, Entity, originalCredit)).OpenAmount);
            Assert.Equal(0L, await CountReversalsForApplicationAsync(dataSource, original.ApplicationIds.Single()));

            var partyCredit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-PARTY-CREDIT-{suffix}", 10_000m);
            var otherPartyDebit = await CreateAsync(repository, otherParty, "Invoice", "Debit", $"SUBLEDGER-PARTY-DEBIT-{suffix}", 10_000m);
            var partyKey = $"subledger-party-isolation-{suffix}";
            var partyMismatch = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => ApplyAsync(repository, partyKey,
                [new(otherPartyDebit, partyCredit, "10000")], party));
            Assert.Equal("subledger.party_mismatch", partyMismatch.Code);
            Assert.Equal(0L, await CountBatchAsync(dataSource, "Apply", partyKey));
            Assert.Equal(0L, await CountApplicationsForItemsAsync(dataSource, [partyCredit, otherPartyDebit]));
            Assert.Equal(10_000m, (await ReadItemStateAsync(dataSource, Entity, partyCredit)).OpenAmount);
            Assert.Equal(10_000m, (await ReadItemStateAsync(dataSource, Entity, otherPartyDebit)).OpenAmount);

            var controlDebit = await CreateWithControlAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-CONTROL-DEBIT-{suffix}", 2_000m, "AR-CONTROL-A", "AR-CONFIG-V1");
            var controlCredit = await CreateWithControlAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-CONTROL-CREDIT-{suffix}", 2_000m, "AR-CONTROL-B", "AR-CONFIG-V2");
            var controlKey = $"subledger-control-context-{suffix}";
            var controlMismatch = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => ApplyAsync(repository, controlKey,
                [new(controlDebit, controlCredit, "2000")], party));
            Assert.Equal("subledger.control_context_mismatch", controlMismatch.Code);
            Assert.Equal(0L, await CountBatchAsync(dataSource, "Apply", controlKey));
            Assert.Equal(0L, await CountApplicationsForItemsAsync(dataSource, [controlDebit, controlCredit]));
            Assert.Equal(2_000m, (await ReadItemStateAsync(dataSource, Entity, controlDebit)).OpenAmount);
            Assert.Equal(2_000m, (await ReadItemStateAsync(dataSource, Entity, controlCredit)).OpenAmount);

            var profileParty = await FindPartyWithoutProfileAsync(dataSource, party);
            var profile = await repository.SaveProfileAsync(Tenant, new(Entity, profileParty, null, null, null, null, null, null, 0), null, null, default);
            Assert.NotNull(await repository.GetProfileAsync(Tenant, Entity, profileParty, default));
            Assert.Null(await repository.GetProfileAsync(Tenant, otherEntity, profileParty, default));
            var wrongEntityProfile = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => repository.SaveProfileAsync(Tenant,
                new(otherEntity, profileParty, null, null, null, null, null, null, profile.Version), null, null, default));
            Assert.Equal("subledger.profile_changed", wrongEntityProfile.Code);
            Assert.Equal(profile.Id, (await repository.GetProfileAsync(Tenant, Entity, profileParty, default))!.Id);
        }
    }

    [Fact]
    public async Task SettlementMatrixPersistsFullPartialManyToManyAndUnappliedBalances()
    {
        var (dataSource, repository) = CreateRuntimeRepository();
        await using (dataSource)
        {
            var party = await FindActivePartyAsync(dataSource);
            var suffix = Guid.NewGuid().ToString("N");

            var fullDebit = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-FULL-D-{suffix}", 10_000m);
            var fullCredit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-FULL-C-{suffix}", 10_000m);
            var fullKey = $"subledger-full-{suffix}";
            var full = await ApplyAsync(repository, fullKey, [new(fullDebit, fullCredit, "10000")], party);
            Assert.Single(full.ApplicationIds);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, fullDebit), 0m, 10_000m, "Settled", false);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, fullCredit), 0m, 10_000m, "Settled", false);
            Assert.Equal(1L, await CountBatchAsync(dataSource, "Apply", fullKey));
            Assert.Equal(1L, await CountApplicationsForBatchAsync(dataSource, full.BatchId));

            var partialDebit = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-PARTIAL-D-{suffix}", 10_000m);
            var partialCredit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-PARTIAL-C-{suffix}", 4_000m);
            var partial = await ApplyAsync(repository, $"subledger-partial-{suffix}", [new(partialDebit, partialCredit, "4000")], party);
            Assert.Single(partial.ApplicationIds);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, partialDebit), 6_000m, 4_000m, "PartiallySettled", true);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, partialCredit), 0m, 4_000m, "Settled", false);

            var oneCredit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-ONE-CREDIT-{suffix}", 10_000m);
            var debitA = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-ONE-CREDIT-A-{suffix}", 3_000m);
            var debitB = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-ONE-CREDIT-B-{suffix}", 4_000m);
            var debitC = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-ONE-CREDIT-C-{suffix}", 8_000m);
            var manyDebitKey = $"subledger-one-to-many-{suffix}";
            var manyDebit = await ApplyAsync(repository, manyDebitKey, [new(debitA, oneCredit, "3000"), new(debitB, oneCredit, "4000"), new(debitC, oneCredit, "3000")], party);
            Assert.Equal(3, manyDebit.ApplicationIds.Count);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, oneCredit), 0m, 10_000m, "Settled", false);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, debitA), 0m, 3_000m, "Settled", false);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, debitB), 0m, 4_000m, "Settled", false);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, debitC), 5_000m, 3_000m, "PartiallySettled", true);
            Assert.Equal(1L, await CountBatchAsync(dataSource, "Apply", manyDebitKey));
            Assert.Equal(3L, await CountApplicationsForBatchAsync(dataSource, manyDebit.BatchId));

            var oneDebit = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-MANY-CREDIT-D-{suffix}", 10_000m);
            var creditA = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-MANY-CREDIT-A-{suffix}", 3_000m);
            var creditB = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-MANY-CREDIT-B-{suffix}", 2_000m);
            var creditC = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-MANY-CREDIT-C-{suffix}", 1_500m);
            var manyCredit = await ApplyAsync(repository, $"subledger-many-to-one-{suffix}", [new(oneDebit, creditA, "3000"), new(oneDebit, creditB, "2000"), new(oneDebit, creditC, "1500")], party);
            Assert.Equal(3, manyCredit.ApplicationIds.Count);
            Assert.Equal(3_500m, (await ReadItemStateAsync(dataSource, Entity, oneDebit)).OpenAmount);
            Assert.Equal(3L, await CountApplicationsForBatchAsync(dataSource, manyCredit.BatchId));

            var overDebit = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-OVERPAY-D-{suffix}", 8_000m);
            var overCredit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-OVERPAY-C-{suffix}", 10_000m);
            var overpayment = await ApplyAsync(repository, $"subledger-unapplied-{suffix}", [new(overDebit, overCredit, "8000")], party);
            Assert.Single(overpayment.ApplicationIds);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, overDebit), 0m, 8_000m, "Settled", false);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, overCredit), 2_000m, 8_000m, "PartiallySettled", true);

            var payableBill = await CreateAsync(repository, party, "Bill", "Credit", $"SUBLEDGER-AP-BILL-{suffix}", 10_000m, Modules.Subledger.Entities.SubledgerDirection.Payable);
            var payablePayment = await CreateAsync(repository, party, "Payment", "Debit", $"SUBLEDGER-AP-PAYMENT-{suffix}", 10_000m, Modules.Subledger.Entities.SubledgerDirection.Payable);
            var payablePartial = await ApplyAsync(repository, $"subledger-ap-partial-{suffix}", [new(payablePayment, payableBill, "4000")], party, SubledgerDirectionContract.Payable);
            Assert.Single(payablePartial.ApplicationIds);
            Assert.Equal(6_000m, (await ReadItemStateAsync(dataSource, Entity, payableBill)).OpenAmount);
            Assert.Equal(6_000m, (await ReadItemStateAsync(dataSource, Entity, payablePayment)).OpenAmount);
            var payableFull = await ApplyAsync(repository, $"subledger-ap-full-{suffix}", [new(payablePayment, payableBill, "6000")], party, SubledgerDirectionContract.Payable);
            Assert.Single(payableFull.ApplicationIds);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, payableBill), 0m, 10_000m, "Settled", false);
            AssertItem(await ReadItemStateAsync(dataSource, Entity, payablePayment), 0m, 10_000m, "Settled", false);
            var apReverse = await repository.ReverseAsync(Tenant, new ReverseApplicationCommand(Entity, payableFull.ApplicationIds.Single(), $"subledger-ap-reverse-{suffix}", new(2026, 10, 25), "AP reversal", Guid.CreateVersion7(), Guid.CreateVersion7()), default);
            Assert.Single(apReverse.ApplicationIds);
            Assert.Equal(6_000m, (await ReadItemStateAsync(dataSource, Entity, payableBill)).OpenAmount);
            Assert.Equal(6_000m, (await ReadItemStateAsync(dataSource, Entity, payablePayment)).OpenAmount);
        }
    }

    [Fact]
    public async Task SourceContentionOppositeRequestOrderAndConcurrentIdempotencyStayAtomic()
    {
        var (dataSource, repository) = CreateRuntimeRepository();
        await using (dataSource)
        {
            var party = await FindActivePartyAsync(dataSource);
            var suffix = Guid.NewGuid().ToString("N");
            var contestedCredit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-SOURCE-RACE-C-{suffix}", 10_000m);
            var sourceDebitA = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-SOURCE-RACE-A-{suffix}", 7_000m);
            var sourceDebitB = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-SOURCE-RACE-B-{suffix}", 7_000m);
            var sourceResults = await Task.WhenAll(AttemptApplyAsync(repository, $"subledger-source-race-a-{suffix}", [new(sourceDebitA, contestedCredit, "7000")], party),
                AttemptApplyAsync(repository, $"subledger-source-race-b-{suffix}", [new(sourceDebitB, contestedCredit, "7000")], party));
            Assert.Single(sourceResults, result => result.Applied);
            Assert.Single(sourceResults, result => !result.Applied);
            Assert.Equal(1L, await CountBatchAsync(dataSource, "Apply", $"subledger-source-race-a-{suffix}") + await CountBatchAsync(dataSource, "Apply", $"subledger-source-race-b-{suffix}"));
            Assert.True((await ReadItemStateAsync(dataSource, Entity, contestedCredit)).OpenAmount >= 0m);
            Assert.InRange(await SumAppliedAsync(dataSource, contestedCredit), 0m, 10_000m);
            var sourceState = await ReadItemStateAsync(dataSource, Entity, contestedCredit);
            Assert.InRange(sourceState.OpenAmount, 0m, 10_000m);

            var orderedDebitA = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-ORDER-D1-{suffix}", 6_000m);
            var orderedDebitB = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-ORDER-D2-{suffix}", 6_000m);
            var orderedCreditA = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-ORDER-C1-{suffix}", 6_000m);
            var orderedCreditB = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-ORDER-C2-{suffix}", 6_000m);
            var orderedA = ApplyAsync(repository, $"subledger-order-a-{suffix}", [new(orderedDebitA, orderedCreditA, "6000"), new(orderedDebitB, orderedCreditB, "6000")], party);
            var orderedB = ApplyAsync(repository, $"subledger-order-b-{suffix}", [new(orderedDebitB, orderedCreditB, "6000"), new(orderedDebitA, orderedCreditA, "6000")], party);
            var orderOutcomes = await Task.WhenAll(AttemptAsync(orderedA), AttemptAsync(orderedB));
            Assert.Single(orderOutcomes, outcome => outcome.Applied);
            Assert.Single(orderOutcomes, outcome => !outcome.Applied);
            Assert.Equal(1L, await CountBatchAsync(dataSource, "Apply", $"subledger-order-a-{suffix}") + await CountBatchAsync(dataSource, "Apply", $"subledger-order-b-{suffix}"));
            Assert.Equal(2L, await CountApplicationsForItemsAsync(dataSource, [orderedDebitA, orderedDebitB, orderedCreditA, orderedCreditB]));
            foreach (var id in new[] { orderedDebitA, orderedDebitB, orderedCreditA, orderedCreditB }) AssertItem(await ReadItemStateAsync(dataSource, Entity, id), 0m, 6_000m, "Settled", false);

            var idempotentDebit = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-IDEMPOTENT-D-{suffix}", 5_000m);
            var idempotentCredit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-IDEMPOTENT-C-{suffix}", 5_000m);
            var idempotencyKey = $"subledger-concurrent-idempotency-{suffix}";
            var idempotentAttempts = await Task.WhenAll(ApplyAsync(repository, idempotencyKey, [new(idempotentDebit, idempotentCredit, "3000")], party),
                ApplyAsync(repository, idempotencyKey, [new(idempotentDebit, idempotentCredit, "3000")], party));
            Assert.Single(idempotentAttempts, result => !result.AlreadyApplied);
            Assert.Single(idempotentAttempts, result => result.AlreadyApplied);
            Assert.Single(idempotentAttempts.Select(result => result.BatchId).Distinct());
            Assert.Single(idempotentAttempts.SelectMany(result => result.ApplicationIds).Distinct());
            Assert.Equal(1L, await CountBatchAsync(dataSource, "Apply", idempotencyKey));
            var conflict = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => ApplyAsync(repository, idempotencyKey,
                [new(idempotentDebit, idempotentCredit, "4000")], party));
            Assert.Equal("subledger.application_conflict", conflict.Code);
            Assert.Equal(3_000m, (await ReadItemStateAsync(dataSource, Entity, idempotentDebit)).SettledAmount);
            Assert.Equal(1L, await CountBatchAsync(dataSource, "Apply", idempotencyKey));
        }
    }

    [Fact]
    public async Task PersistenceFailureAfterBatchInsertRollsBackAllSettlementState()
    {
        var (dataSource, repository) = CreateRuntimeRepository();
        await using (dataSource)
        {
            var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
                ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION to inject a PostgreSQL persistence failure.");
            var party = await FindActivePartyAsync(dataSource);
            var suffix = Guid.NewGuid().ToString("N");
            var debit = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-ATOMIC-D-{suffix}", 5_000m);
            var credit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-ATOMIC-C-{suffix}", 5_000m);
            var key = $"subledger-atomic-failure-{suffix}";
            var triggerName = $"integration_fail_{suffix}";
            var functionName = $"integration_fail_function_{suffix}";
            await using (var admin = new NpgsqlConnection(adminConnectionString))
            {
                await admin.OpenAsync();
                await using var install = new NpgsqlCommand($"create function finance_subledger.{functionName}() returns trigger language plpgsql as $$ begin raise exception 'controlled Subledger persistence failure' using errcode='P0001'; end $$; create trigger {triggerName} before insert on finance_subledger.open_item_application for each row execute function finance_subledger.{functionName}()", admin);
                await install.ExecuteNonQueryAsync();
            }

            try
            {
                await Assert.ThrowsAsync<PostgresException>(() => ApplyAsync(repository, key, [new(debit, credit, "3000")], party));
                Assert.Equal(0L, await CountBatchAsync(dataSource, "Apply", key));
                Assert.Equal(0L, await CountApplicationsForItemsAsync(dataSource, [debit, credit]));
                AssertItem(await ReadItemStateAsync(dataSource, Entity, debit), 5_000m, 0m, "Open", true);
                AssertItem(await ReadItemStateAsync(dataSource, Entity, credit), 5_000m, 0m, "Open", true);
            }
            finally
            {
                await using var admin = new NpgsqlConnection(adminConnectionString);
                await admin.OpenAsync();
                await using var remove = new NpgsqlCommand($"drop trigger if exists {triggerName} on finance_subledger.open_item_application; drop function if exists finance_subledger.{functionName}()", admin);
                await remove.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task ReversalRestoresExactBalancesAndPreservesHistoricalEvidence()
    {
        var (dataSource, repository) = CreateRuntimeRepository();
        await using (dataSource)
        {
            var party = await FindActivePartyAsync(dataSource);
            var suffix = Guid.NewGuid().ToString("N");
            var debit = await CreateAsync(repository, party, "Invoice", "Debit", $"SUBLEDGER-HISTORY-D-{suffix}", 10_000m);
            var credit = await CreateAsync(repository, party, "Payment", "Credit", $"SUBLEDGER-HISTORY-C-{suffix}", 6_000m);
            var applicationDate = new DateOnly(2026, 10, 20);
            var apply = new ApplyOpenItemsCommand(Entity, party, SubledgerDirectionContract.Receivable, $"subledger-history-apply-{suffix}", applicationDate, new(2026, 10, 21), [new(debit, credit, "4000")], Guid.CreateVersion7(), Guid.CreateVersion7());
            var applied = await repository.ApplyAsync(Tenant, apply, [4_000m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);
            var applicationId = applied.ApplicationIds.Single();
            Assert.Equal(6_000m, (await ReadItemStateAsync(dataSource, Entity, debit)).OpenAmount);
            Assert.Equal(2_000m, (await ReadItemStateAsync(dataSource, Entity, credit)).OpenAmount);
            var effectiveDate = new DateOnly(2026, 10, 25);
            var reverseCommand = new ReverseApplicationCommand(Entity, applicationId, $"subledger-history-reverse-{suffix}", effectiveDate, "Restore original balance", Guid.CreateVersion7(), Guid.CreateVersion7());
            var reversed = await repository.ReverseAsync(Tenant, reverseCommand, default);
            Assert.Single(reversed.ApplicationIds);
            Assert.Equal(applicationId, reversed.ApplicationIds.Single());
            Assert.Equal(10_000m, (await ReadItemStateAsync(dataSource, Entity, debit)).OpenAmount);
            Assert.Equal(6_000m, (await ReadItemStateAsync(dataSource, Entity, credit)).OpenAmount);

            var history = await ReadApplicationHistoryAsync(dataSource, applicationId);
            Assert.Equal(applicationDate, history.ApplicationDate);
            Assert.NotEqual(default, history.ApplicationCreatedAt);
            Assert.Equal(effectiveDate, history.ReversalEffectiveDate);
            Assert.Equal(applicationId, history.OriginalApplicationId);
            Assert.NotEqual(Guid.Empty, history.ReversalId);

            var duplicate = await repository.ReverseAsync(Tenant, reverseCommand, default);
            Assert.True(duplicate.AlreadyApplied);
            Assert.Equal(reversed.BatchId, duplicate.BatchId);
            Assert.Equal(1L, await CountReversalsForApplicationAsync(dataSource, applicationId));
            var duplicateKey = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => repository.ReverseAsync(Tenant,
                reverseCommand with { IdempotencyKey = $"subledger-history-reverse-again-{suffix}" }, default));
            Assert.Equal("subledger.application_already_reversed", duplicateKey.Code);
            Assert.Equal(10_000m, (await ReadItemStateAsync(dataSource, Entity, debit)).OpenAmount);
            Assert.Equal(6_000m, (await ReadItemStateAsync(dataSource, Entity, credit)).OpenAmount);
        }
    }

    private static async Task<Guid> FindActivePartyAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            setTenant.Parameters.AddWithValue(Tenant.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select p.id from foundation_directory.party p join foundation_directory.party_visibility v on v.tenant_id=p.tenant_id and v.party_id=p.id where p.tenant_id=$1 and p.status='Active' and (v.scope_type='TenantWide' or v.legal_entity_id=$2) order by p.id limit 1", connection);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        var result = await command.ExecuteScalarAsync();
        return result is Guid id ? id : throw new InvalidOperationException("The Subledger PostgreSQL fixture has no active party visible in the Atlas legal entity.");
    }

    private static (NpgsqlDataSource DataSource, SubledgerRepository Repository) CreateRuntimeRepository()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        var dataSource = NpgsqlDataSource.Create(connectionString);
        return (dataSource, new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System));
    }

    private static async Task<Guid> FindAnotherActivePartyAsync(NpgsqlDataSource dataSource, Guid excludedPartyId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            setTenant.Parameters.AddWithValue(Tenant.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select p.id from foundation_directory.party p join foundation_directory.party_visibility v on v.tenant_id=p.tenant_id and v.party_id=p.id where p.tenant_id=$1 and p.id<>$2 and p.status='Active' and (v.scope_type='TenantWide' or v.legal_entity_id=$3) order by p.id limit 1", connection);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(excludedPartyId);
        command.Parameters.AddWithValue(Entity.Value);
        return await command.ExecuteScalarAsync() is Guid id ? id : throw new InvalidOperationException("The Subledger PostgreSQL fixture has no second active party visible in the Atlas legal entity.");
    }

    private static async Task<Guid> FindPartyWithoutProfileAsync(NpgsqlDataSource dataSource, Guid? excludedPartyId = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            setTenant.Parameters.AddWithValue(Tenant.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select p.id from foundation_directory.party p join foundation_directory.party_visibility v on v.tenant_id=p.tenant_id and v.party_id=p.id where p.tenant_id=$1 and p.status='Active' and (v.scope_type='TenantWide' or v.legal_entity_id=$2) and ($3::uuid is null or p.id<>$3) and not exists(select 1 from finance_subledger.party_financial_profile f where f.tenant_id=p.tenant_id and f.legal_entity_id=$2 and f.party_id=p.id) order by p.id limit 1", connection);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        command.Parameters.AddWithValue(excludedPartyId is null ? DBNull.Value : excludedPartyId.Value);
        return await command.ExecuteScalarAsync() is Guid id ? id : throw new InvalidOperationException("No active party without a Subledger profile remains in the test tenant.");
    }

    private static async Task<LegalEntityId> FindAnotherEntityAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            setTenant.Parameters.AddWithValue(Tenant.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select id from platform.legal_entity where tenant_id=$1 and id<>$2 order by id limit 1", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        var result = await command.ExecuteScalarAsync();
        return result is Guid id ? new LegalEntityId(id) : throw new InvalidOperationException("The Subledger PostgreSQL fixture requires a second Atlas legal entity.");
    }

    private static Task<Guid> CreateForEntityAsync(SubledgerRepository repository, LegalEntityId entity, Guid partyId, string sourceType, string polarity, string reference, decimal amount,
        Modules.Subledger.Entities.SubledgerDirection direction = Modules.Subledger.Entities.SubledgerDirection.Receivable) =>
        CreateWithParametersAsync(repository, entity, partyId, sourceType, polarity, reference, amount, direction, null, null);

    private static Task<Guid> CreateWithControlAsync(SubledgerRepository repository, Guid partyId, string sourceType, string polarity, string reference, decimal amount,
        string controlReference, string configurationReference) =>
        CreateWithParametersAsync(repository, Entity, partyId, sourceType, polarity, reference, amount, Modules.Subledger.Entities.SubledgerDirection.Receivable, controlReference, configurationReference);

    private static async Task<Guid> CreateWithParametersAsync(SubledgerRepository repository, LegalEntityId entity, Guid partyId, string sourceType, string polarity, string reference, decimal amount,
        Modules.Subledger.Entities.SubledgerDirection direction, string? controlReference, string? configurationReference)
    {
        var controlRole = direction == Modules.Subledger.Entities.SubledgerDirection.Receivable ? "Receivable" : "Payable";
        var snapshot = new OpenItemSnapshotContract(reference, null, reference, new(2026, 10, 25), new(2026, 10, 25), new(2026, 11, 24), "EGP",
            amount.ToString(System.Globalization.CultureInfo.InvariantCulture), "EGP", amount.ToString(System.Globalization.CultureInfo.InvariantCulture), controlRole,
            controlReference ?? $"{controlRole}-CONTROL", configurationReference ?? $"{controlRole}-CONFIG-V1", null, 1);
        var ids = await repository.CreateOpenItemsAsync(Tenant, entity, partyId, direction,
            polarity == "Debit" ? Modules.Subledger.Entities.OpenItemPolarity.Debit : Modules.Subledger.Entities.OpenItemPolarity.Credit,
            sourceType, [snapshot], default);
        return ids.Single();
    }

    private static Task<SubledgerApplicationResult> ApplyAsync(SubledgerRepository repository, string key, IReadOnlyList<ApplicationLineCommand> lines, Guid partyId,
        SubledgerDirectionContract direction = SubledgerDirectionContract.Receivable) =>
        repository.ApplyAsync(Tenant, new ApplyOpenItemsCommand(Entity, partyId, direction, key, new(2026, 10, 25), new(2026, 10, 25), lines, Guid.CreateVersion7(), Guid.CreateVersion7()),
            lines.Select(line => decimal.Parse(line.Amount, System.Globalization.CultureInfo.InvariantCulture)).ToArray(), 2,
            Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);

    private static async Task<ApplyAttempt> AttemptApplyAsync(SubledgerRepository repository, string key, IReadOnlyList<ApplicationLineCommand> lines, Guid partyId)
    {
        try { await ApplyAsync(repository, key, lines, partyId); return new(true); }
        catch (Modules.Subledger.Entities.SubledgerException) { return new(false); }
        catch (PostgresException exception) when (exception.SqlState == "40P01" || exception.SqlState == "40001") { return new(false); }
    }

    private static async Task<ApplyAttempt> AttemptAsync(Task<SubledgerApplicationResult> application)
    {
        try { await application; return new(true); }
        catch (Modules.Subledger.Entities.SubledgerException) { return new(false); }
        catch (PostgresException exception) when (exception.SqlState == "40P01" || exception.SqlState == "40001") { return new(false); }
    }

    private static async Task<ItemState> ReadItemStateAsync(NpgsqlDataSource dataSource, LegalEntityId entity, Guid itemId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            setTenant.Parameters.AddWithValue(Tenant.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select open_transaction_amount,settled_transaction_amount,status,is_open from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(entity.Value);
        command.Parameters.AddWithValue(itemId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("Expected Subledger item was not visible to its tenant.");
        return new(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetString(2), reader.GetBoolean(3));
    }

    private static async Task<long> CountBatchAsync(NpgsqlDataSource dataSource, string operation, string key)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*) from finance_subledger.application_batch where tenant_id=$1 and legal_entity_id=$2 and operation=$3 and idempotency_key=$4", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        command.Parameters.AddWithValue(operation);
        command.Parameters.AddWithValue(key);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountApplicationsForBatchAsync(NpgsqlDataSource dataSource, Guid batchId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*) from finance_subledger.open_item_application where tenant_id=$1 and legal_entity_id=$2 and batch_id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        command.Parameters.AddWithValue(batchId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountApplicationsForItemsAsync(NpgsqlDataSource dataSource, Guid[] itemIds)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*) from finance_subledger.open_item_application where tenant_id=$1 and legal_entity_id=$2 and (debit_item_id=any($3) or credit_item_id=any($3))", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        command.Parameters.AddWithValue(itemIds);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<InstallmentFacts> ReadInstallmentFactsAsync(NpgsqlDataSource dataSource, string sourceReference)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*),count(distinct source_schedule_reference),coalesce(sum(original_transaction_amount),0) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type='Invoice' and source_reference=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        command.Parameters.AddWithValue(sourceReference);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new(reader.GetInt64(0), reader.GetInt64(1), reader.GetDecimal(2));
    }

    private static async Task<long> CountReversalsForApplicationAsync(NpgsqlDataSource dataSource, Guid applicationId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*) from finance_subledger.application_reversal where tenant_id=$1 and legal_entity_id=$2 and original_application_id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        command.Parameters.AddWithValue(applicationId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<decimal> SumAppliedAsync(NpgsqlDataSource dataSource, Guid itemId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select coalesce(sum(case when debit_item_id=$3 then debit_applied_amount else credit_applied_amount end),0) from finance_subledger.open_item_application where tenant_id=$1 and legal_entity_id=$2 and (debit_item_id=$3 or credit_item_id=$3)", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        command.Parameters.AddWithValue(itemId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<ApplicationHistory> ReadApplicationHistoryAsync(NpgsqlDataSource dataSource, Guid applicationId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select a.application_date,a.created_at,r.effective_date,r.original_application_id,r.id from finance_subledger.open_item_application a join finance_subledger.application_reversal r on r.tenant_id=a.tenant_id and r.legal_entity_id=a.legal_entity_id and r.original_application_id=a.id where a.tenant_id=$1 and a.legal_entity_id=$2 and a.id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        command.Parameters.AddWithValue(applicationId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("Original application and reversal history were not preserved.");
        return new(reader.GetFieldValue<DateOnly>(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetFieldValue<DateOnly>(2), reader.GetGuid(3), reader.GetGuid(4));
    }

    private static async Task SetTenantForQueryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction);
        setTenant.Parameters.AddWithValue(Tenant.Value.ToString("D"));
        await setTenant.ExecuteNonQueryAsync();
    }

    private static void AssertItem(ItemState state, decimal openAmount, decimal settledAmount, string status, bool isOpen)
    {
        Assert.Equal(openAmount, state.OpenAmount);
        Assert.Equal(settledAmount, state.SettledAmount);
        Assert.Equal(status, state.Status);
        Assert.Equal(isOpen, state.IsOpen);
        Assert.True(state.OpenAmount >= 0m);
    }

    private sealed record ItemState(decimal OpenAmount, decimal SettledAmount, string Status, bool IsOpen);
    private sealed record InstallmentFacts(long Count, long DistinctScheduleCount, decimal TotalAmount);
    private sealed record ApplyAttempt(bool Applied);
    private sealed record ApplicationHistory(DateOnly ApplicationDate, DateTimeOffset ApplicationCreatedAt, DateOnly ReversalEffectiveDate, Guid OriginalApplicationId, Guid ReversalId);

    private static async Task<Guid> CreateAsync(SubledgerRepository repository, Guid partyId, string sourceType, string polarity, string reference, decimal amount,
        Modules.Subledger.Entities.SubledgerDirection direction = Modules.Subledger.Entities.SubledgerDirection.Receivable) =>
        (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId,
            direction,
            polarity == "Debit" ? Modules.Subledger.Entities.OpenItemPolarity.Debit : Modules.Subledger.Entities.OpenItemPolarity.Credit,
            sourceType, [Snapshot(reference, amount, null, direction == Modules.Subledger.Entities.SubledgerDirection.Receivable ? "Receivable" : "Payable")], default)).Single();

    private static OpenItemSnapshotContract Snapshot(string reference, decimal amount, string? schedule = null, string controlRole = "Receivable") => new(
        reference, schedule, reference, new(2026, 10, 25), new(2026, 10, 25), new(2026, 11, 24), "EGP", amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "EGP", amount.ToString(System.Globalization.CultureInfo.InvariantCulture), controlRole, $"{controlRole}-CONTROL", $"{controlRole}-CONFIG-V1", null, 1);

    private static async Task<decimal> ReadOpenAmountAsync(NpgsqlDataSource dataSource, Guid itemId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            tenant.Parameters.AddWithValue(Tenant.Value.ToString("D"));
            await tenant.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select open_transaction_amount from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(Entity.Value); command.Parameters.AddWithValue(itemId);
        return (decimal)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected open item was missing."));
    }

    private static async Task<long> CountVisibleFromTenantAsync(NpgsqlDataSource dataSource, Guid tenantId, Guid itemId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            tenant.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenant.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select count(*) from finance_subledger.party_open_item where id=$1", connection, transaction);
        command.Parameters.AddWithValue(itemId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
