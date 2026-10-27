using Core.Transactions;
using System.Diagnostics;
using Core.Authorization;
using Core.Services;
using Modules.Subledger.Repositories;
using Modules.Subledger.Services;
using Modules.Ledger.Repositories;
using Modules.Ledger.Services;
using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Npgsql;
using Shared.Contracts.Subledger;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Directory;
using Shared.Contracts.Ledger;
using Shared.Primitives.Financial;
using Shared.Primitives.Identifiers;
using Xunit.Abstractions;
using Xunit;

namespace Subledger.Tests;

public sealed class SubledgerPostgresIntegrationTests
{
    private readonly ITestOutputHelper output;

    public SubledgerPostgresIntegrationTests(ITestOutputHelper output) => this.output = output;

    private static readonly TenantId Tenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    private static readonly TenantId OtherTenant = new(Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb"));
    private static readonly LegalEntityId Entity = new(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"));

    [Fact]
    public async Task HistoricalBalanceAndAgingQueriesExecuteAgainstRealPostgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var partyId = await FindActivePartyAsync(dataSource);
        var asOf = new DateOnly(2026, 10, 25);
        var balance = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, asOf), default);
        var aging = await repository.GetAgingAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, asOf), default);
        Assert.Equal(partyId, balance.PartyId);
        Assert.Equal(Entity.Value, balance.LegalEntityId);
        Assert.All(aging, bucket => Assert.Equal(3, bucket.CurrencyCode.Length));
    }

    [Fact]
    public async Task CurrentExposureUsesCommittedOpenAmountsAndFallsBackForFutureDatedApplications()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var partyId = Guid.CreateVersion7();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var suffix = Guid.NewGuid().ToString("N");
        var invoice = Snapshot($"SUBLEDGER-CURRENT-INVOICE-{suffix}", 100m) with { DocumentDate = today, PostingDate = today, DueDate = today.AddDays(30) };
        var payment = Snapshot($"SUBLEDGER-CURRENT-PAYMENT-{suffix}", 100m) with { DocumentDate = today, PostingDate = today, DueDate = today };
        var debitId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [invoice], default)).Single();
        var creditId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", [payment], default)).Single();

        async Task<PartyBalanceResult> CurrentBalance() => await repository.GetPartyBalanceAsync(Tenant,
            new(Entity, partyId, SubledgerDirectionContract.Receivable, today), default);
        async Task<IReadOnlyList<AgingBucketResult>> CurrentAging() => await repository.GetAgingAsync(Tenant,
            new(Entity, partyId, SubledgerDirectionContract.Receivable, today, null), default);
        static decimal Amount(PartyBalanceResult result, bool credits) => decimal.Parse(
            credits ? result.Currencies.Single(row => row.CurrencyCode == "EGP").AvailableCredits : result.Currencies.Single(row => row.CurrencyCode == "EGP").AmountDue,
            System.Globalization.CultureInfo.InvariantCulture);
        static decimal AgingAmount(IReadOnlyList<AgingBucketResult> result) => result.Where(row => row.CurrencyCode == "EGP")
            .Sum(row => decimal.Parse(row.Current, System.Globalization.CultureInfo.InvariantCulture)
                + decimal.Parse(row.Days1To30, System.Globalization.CultureInfo.InvariantCulture)
                + decimal.Parse(row.Days31To60, System.Globalization.CultureInfo.InvariantCulture)
                + decimal.Parse(row.Days61To90, System.Globalization.CultureInfo.InvariantCulture)
                + decimal.Parse(row.Days91Plus, System.Globalization.CultureInfo.InvariantCulture));

        var beforeApply = await CurrentBalance();
        Assert.Equal(100m, Amount(beforeApply, credits: false));
        Assert.Equal(100m, Amount(beforeApply, credits: true));
        Assert.Equal(100m, AgingAmount(await CurrentAging()));

        var application = await repository.ApplyAsync(Tenant,
            new(Entity, partyId, SubledgerDirectionContract.Receivable, $"current-apply-{suffix}", today, today,
                [new(debitId, creditId, "30")], Guid.CreateVersion7(), Guid.CreateVersion7()), [30m], 2,
            Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);
        var afterApply = await CurrentBalance();
        Assert.Equal(70m, Amount(afterApply, credits: false));
        Assert.Equal(70m, Amount(afterApply, credits: true));
        Assert.Equal(70m, AgingAmount(await CurrentAging()));

        await repository.ReverseAsync(Tenant,
            new(Entity, application.ApplicationIds.Single(), $"current-reverse-{suffix}", today, "Current exposure projection proof", Guid.CreateVersion7(), Guid.CreateVersion7()), default);
        var afterReversal = await CurrentBalance();
        Assert.Equal(100m, Amount(afterReversal, credits: false));
        Assert.Equal(100m, Amount(afterReversal, credits: true));
        Assert.Equal(100m, AgingAmount(await CurrentAging()));

        var futureReversalApplication = await repository.ApplyAsync(Tenant,
            new(Entity, partyId, SubledgerDirectionContract.Receivable, $"future-reversal-apply-{suffix}", today, today,
                [new(debitId, creditId, "10")], Guid.CreateVersion7(), Guid.CreateVersion7()), [10m], 2,
            Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);
        await repository.ReverseAsync(Tenant,
            new(Entity, futureReversalApplication.ApplicationIds.Single(), $"future-reversal-{suffix}", today.AddDays(1), "Future-dated reversal projection proof", Guid.CreateVersion7(), Guid.CreateVersion7()), default);
        Assert.Equal(90m, Amount(await CurrentBalance(), credits: false));
        Assert.Equal(90m, Amount(await CurrentBalance(), credits: true));
        Assert.Equal(90m, AgingAmount(await CurrentAging()));

        var advance = Snapshot($"SUBLEDGER-CURRENT-ADVANCE-{suffix}", 25m) with
        {
            DocumentDate = today, PostingDate = today, DueDate = today, SourceDisplayNumber = $"ADV-{suffix}"
        };
        await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Advance", [advance], default);
        Assert.Equal(115m, Amount(await CurrentBalance(), credits: true));

        var futureDebit = Snapshot($"SUBLEDGER-FUTURE-INVOICE-{suffix}", 40m) with { DocumentDate = today, PostingDate = today, DueDate = today.AddDays(30) };
        var futureCredit = Snapshot($"SUBLEDGER-FUTURE-PAYMENT-{suffix}", 40m) with { DocumentDate = today, PostingDate = today, DueDate = today };
        var futureDebitId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [futureDebit], default)).Single();
        var futureCreditId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", [futureCredit], default)).Single();
        await repository.ApplyAsync(Tenant,
            new(Entity, partyId, SubledgerDirectionContract.Receivable, $"future-apply-{suffix}", today.AddDays(1), today.AddDays(1),
                [new(futureDebitId, futureCreditId, "10")], Guid.CreateVersion7(), Guid.CreateVersion7()), [10m], 2,
            Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);

        var asOfToday = await CurrentBalance();
        Assert.Equal(130m, Amount(asOfToday, credits: false));
        Assert.Equal(155m, Amount(asOfToday, credits: true));
        Assert.Equal(130m, AgingAmount(await CurrentAging()));
    }

    [Fact]
    public async Task AvailableCreditUsesAConsistentBaseValueForMixedCurrencyExposure()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var partyId = Guid.CreateVersion7();
        await repository.SaveProfileAsync(Tenant, new(Entity, partyId, null, null, "250000", "EGP", null, null, 0), 250_000m, "EGP", default);
        await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [Snapshot($"SUBLEDGER-CREDIT-EGP-{Guid.NewGuid():N}", 10m)], default);
        var usdItem = Snapshot($"SUBLEDGER-CREDIT-USD-{Guid.NewGuid():N}", 1m) with
        {
            TransactionCurrency = "USD", OriginalTransactionAmount = "1", OriginalBaseAmount = "50",
            HistoricFxSnapshot = "{\"rate\":50,\"from\":\"USD\",\"to\":\"EGP\"}"
        };
        await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [usdItem], default);
        var service = new SubledgerService(repository, new ActivePartyDirectory(), new InstallmentFinanceBase(), new FixedEntityCurrency(),
            new NullAuthorizationSubjectStore(), new PermissionAuthorizer(), TimeProvider.System);
        var balance = await service.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, new(2026, 10, 25)), default);
        Assert.NotNull(balance.AvailableCreditAmount);
        var exposureInEgp = balance.Currencies.Sum(item => decimal.Parse(item.NetBalance, System.Globalization.CultureInfo.InvariantCulture)
            * (item.CurrencyCode == "USD" ? 50m : 1m));
        Assert.Equal(250_000m - exposureInEgp, decimal.Parse(balance.AvailableCreditAmount!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task HistoricalAgingReconstructsApplicationsAndLaterReversalsByEffectiveDate()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var partyId = await FindActivePartyAsync(dataSource);
        var suffix = Guid.NewGuid().ToString("N");
        var obligation = Snapshot($"SUBLEDGER-AGING-{suffix}", 100m) with { DocumentDate = new(2026, 1, 1), PostingDate = new(2026, 1, 1), DueDate = new(2026, 1, 10) };
        var credit = Snapshot($"SUBLEDGER-AGING-CREDIT-{suffix}", 50m) with { DocumentDate = new(2026, 1, 1), PostingDate = new(2026, 1, 1), DueDate = new(2026, 1, 1) };
        var jan10 = new DateOnly(2026, 1, 10);
        var jan20 = new DateOnly(2026, 1, 20);
        var feb2 = new DateOnly(2026, 2, 2);
        var baselineJan10 = await repository.GetAgingAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, jan10), default);
        var baselineBalance = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, jan20), default);
        var baselineCurrency = baselineBalance.Currencies.SingleOrDefault(item => item.CurrencyCode == "EGP");
        var baselineDue = baselineCurrency is null ? 0m : decimal.Parse(baselineCurrency.AmountDue, System.Globalization.CultureInfo.InvariantCulture);
        var baselineCredits = baselineCurrency is null ? 0m : decimal.Parse(baselineCurrency.AvailableCredits, System.Globalization.CultureInfo.InvariantCulture);
        var baselineStatement = await repository.GetStatementAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, new(2026, 1, 1), feb2), default);
        var baselineClosing = decimal.Parse(baselineStatement.ClosingBalances.GetValueOrDefault("EGP", "0"), System.Globalization.CultureInfo.InvariantCulture);
        var baselineFeb2 = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, feb2), default);
        var baselineFeb2Currency = baselineFeb2.Currencies.SingleOrDefault(item => item.CurrencyCode == "EGP");
        var baselineFeb2Due = baselineFeb2Currency is null ? 0m : decimal.Parse(baselineFeb2Currency.AmountDue, System.Globalization.CultureInfo.InvariantCulture);
        var baselineFeb2Credits = baselineFeb2Currency is null ? 0m : decimal.Parse(baselineFeb2Currency.AvailableCredits, System.Globalization.CultureInfo.InvariantCulture);
        var debitId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable, Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [obligation], default)).Single();
        var creditId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable, Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", [credit], default)).Single();
        var beforeApplication = await repository.GetAgingAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, jan20), default);
        var beforeBalance = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, jan20), default);
        var beforeCurrency = beforeBalance.Currencies.Single(item => item.CurrencyCode == "EGP");
        Assert.Equal(100m, decimal.Parse(beforeCurrency.AmountDue, System.Globalization.CultureInfo.InvariantCulture) - baselineDue);
        Assert.Equal(50m, decimal.Parse(beforeCurrency.AvailableCredits, System.Globalization.CultureInfo.InvariantCulture) - baselineCredits);
        static decimal Overdue(IReadOnlyList<AgingBucketResult> rows) => rows.Sum(x => decimal.Parse(x.Days1To30, System.Globalization.CultureInfo.InvariantCulture));
        static decimal Total(IReadOnlyList<AgingBucketResult> rows) => rows.Sum(x => decimal.Parse(x.Current, System.Globalization.CultureInfo.InvariantCulture) + decimal.Parse(x.Days1To30, System.Globalization.CultureInfo.InvariantCulture) + decimal.Parse(x.Days31To60, System.Globalization.CultureInfo.InvariantCulture) + decimal.Parse(x.Days61To90, System.Globalization.CultureInfo.InvariantCulture) + decimal.Parse(x.Days91Plus, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(100m, Total(await repository.GetAgingAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, jan10), default)) - Total(baselineJan10));

        var applied = await repository.ApplyAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, $"aging-apply-{suffix}", new(2026, 1, 15), new(2026, 1, 15), [new(debitId, creditId, "50")], Guid.CreateVersion7(), Guid.CreateVersion7()), [50m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);
        var afterApply = await repository.GetAgingAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, jan20), default);
        Assert.Equal(-50m, Overdue(afterApply) - Overdue(beforeApplication));
        var appliedBalance = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, jan20), default);
        var appliedCurrency = appliedBalance.Currencies.Single(item => item.CurrencyCode == "EGP");
        Assert.Equal(50m, decimal.Parse(appliedCurrency.AmountDue, System.Globalization.CultureInfo.InvariantCulture) - baselineDue);
        Assert.Equal(0m, decimal.Parse(appliedCurrency.AvailableCredits, System.Globalization.CultureInfo.InvariantCulture) - baselineCredits);

        var beforeReversal = await repository.GetAgingAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, feb2), default);
        var invalidReversal = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => repository.ReverseAsync(Tenant,
            new(Entity, applied.ApplicationIds.Single(), $"aging-invalid-reverse-{suffix}", new(2026, 1, 14), "Cannot reverse before application", Guid.CreateVersion7(), Guid.CreateVersion7()), default));
        Assert.Equal("subledger.reversal_date_invalid", invalidReversal.Code);
        await repository.ReverseAsync(Tenant, new(Entity, applied.ApplicationIds.Single(), $"aging-reverse-{suffix}", new(2026, 2, 1), "Historical fixture reversal", Guid.CreateVersion7(), Guid.CreateVersion7()), default);
        var afterReversal = await repository.GetAgingAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, feb2), default);
        Assert.Equal(50m, Overdue(afterReversal) - Overdue(beforeReversal));
        Assert.Equal(afterReversal, await repository.GetAgingAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, feb2), default));
        var reversedBalance = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, feb2), default);
        var reversedCurrency = reversedBalance.Currencies.Single(item => item.CurrencyCode == "EGP");
        Assert.Equal(100m, decimal.Parse(reversedCurrency.AmountDue, System.Globalization.CultureInfo.InvariantCulture) - baselineFeb2Due);
        Assert.Equal(50m, decimal.Parse(reversedCurrency.AvailableCredits, System.Globalization.CultureInfo.InvariantCulture) - baselineFeb2Credits);
        var statement = await repository.GetStatementAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, new(2026, 1, 1), feb2), default);
        var balance = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, feb2), default);
        Assert.Equal(50m, decimal.Parse(statement.ClosingBalances["EGP"], System.Globalization.CultureInfo.InvariantCulture) - baselineClosing);
        Assert.Equal(decimal.Parse(balance.Currencies.Single(item => item.CurrencyCode == "EGP").NetBalance, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(statement.ClosingBalances["EGP"], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task AdvancesAndCreditNotesRemainDistinctCreditsAndCanBeAppliedOrReversed()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var partyId = await FindActivePartyAsync(dataSource);
        var suffix = Guid.NewGuid().ToString("N");

        var invoice = await CreateAsync(repository, partyId, "Invoice", "Debit", $"SUBLEDGER-ADV-INVOICE-{suffix}", 15m);
        var advance = await CreateAsync(repository, partyId, "Advance", "Credit", $"SUBLEDGER-ADV-{suffix}", 20m);
        var useAdvance = await repository.ApplyAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, $"advance-apply-{suffix}", new(2026, 10, 25), new(2026, 10, 25), [new(invoice, advance, "15")], Guid.CreateVersion7(), Guid.CreateVersion7()), [15m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);
        Assert.Equal(5m, await ReadOpenAmountAsync(dataSource, advance));
        Assert.Equal(0m, await ReadOpenAmountAsync(dataSource, invoice));
        await repository.ReverseAsync(Tenant, new(Entity, useAdvance.ApplicationIds.Single(), $"advance-reverse-{suffix}", new(2026, 10, 26), "Advance application reversal", Guid.CreateVersion7(), Guid.CreateVersion7()), default);
        Assert.Equal(20m, await ReadOpenAmountAsync(dataSource, advance));
        Assert.Equal(15m, await ReadOpenAmountAsync(dataSource, invoice));

        var creditNote = await CreateAsync(repository, partyId, "CreditNote", "Credit", $"SUBLEDGER-CREDITNOTE-{suffix}", 2m);
        var applyCreditNote = await repository.ApplyAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, $"credit-note-apply-{suffix}", new(2026, 10, 27), new(2026, 10, 27), [new(invoice, creditNote, "2")], Guid.CreateVersion7(), Guid.CreateVersion7()), [2m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);
        Assert.Equal(13m, await ReadOpenAmountAsync(dataSource, invoice));
        Assert.Single(applyCreditNote.ApplicationIds);

        var bill = await CreateAsync(repository, partyId, "Bill", "Credit", $"SUBLEDGER-ADV-BILL-{suffix}", 15m, Modules.Subledger.Entities.SubledgerDirection.Payable);
        var supplierAdvance = await CreateAsync(repository, partyId, "Advance", "Debit", $"SUBLEDGER-AP-ADV-{suffix}", 20m, Modules.Subledger.Entities.SubledgerDirection.Payable);
        await repository.ApplyAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Payable, $"ap-advance-apply-{suffix}", new(2026, 10, 25), new(2026, 10, 25), [new(supplierAdvance, bill, "15")], Guid.CreateVersion7(), Guid.CreateVersion7()), [15m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);
        Assert.Equal(5m, await ReadOpenAmountAsync(dataSource, supplierAdvance));
        Assert.Equal(0m, await ReadOpenAmountAsync(dataSource, bill));

        var payableBill = await CreateAsync(repository, partyId, "Bill", "Credit", $"SUBLEDGER-CREDITNOTE-BILL-{suffix}", 15m, Modules.Subledger.Entities.SubledgerDirection.Payable);
        var supplierCreditNote = await CreateAsync(repository, partyId, "CreditNote", "Debit", $"SUBLEDGER-AP-CREDITNOTE-{suffix}", 2m, Modules.Subledger.Entities.SubledgerDirection.Payable);
        await repository.ApplyAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Payable, $"ap-credit-note-apply-{suffix}", new(2026, 10, 28), new(2026, 10, 28), [new(supplierCreditNote, payableBill, "2")], Guid.CreateVersion7(), Guid.CreateVersion7()), [2m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default);
        Assert.Equal(13m, await ReadOpenAmountAsync(dataSource, payableBill));
    }

    [Fact]
    public async Task AutoApplySuggestionsPreferExactReferenceThenOldestDueAndCarryVersions()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var partyId = await FindActivePartyAsync(dataSource);
        var suffix = Guid.NewGuid().ToString("N");
        var controlReference = $"MATCH-CONTROL-{suffix}";
        var controlConfiguration = $"MATCH-CONFIG-{suffix}";
        OpenItemSnapshotContract Isolated(OpenItemSnapshotContract value) => value with { ControlAccountReference = controlReference, ControlConfigurationReference = controlConfiguration };
        var oldInvoice = Isolated(Snapshot($"SUBLEDGER-MATCH-OLD-{suffix}", 50m)) with { DueDate = new(2026, 1, 1) };
        var exactReference = $"inv ref 22 {suffix}";
        var exactInvoice = Isolated(Snapshot($"SUBLEDGER-MATCH-EXACT-{suffix}", 40m)) with { SourceDisplayNumber = exactReference, DocumentDate = new(2026, 11, 1), DueDate = new(2026, 12, 1) };
        var foreignInvoice = Isolated(Snapshot($"SUBLEDGER-MATCH-FOREIGN-{suffix}", 30m)) with
        {
            TransactionCurrency = "USD", OriginalTransactionAmount = "30", OriginalBaseAmount = "1500",
            HistoricFxSnapshot = "{\"rate\":50}", DueDate = new(2025, 1, 1)
        };
        var debitIds = await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [oldInvoice, exactInvoice, foreignInvoice], default);
        var source = Isolated(Snapshot($"SUBLEDGER-MATCH-SOURCE-{suffix}", 80m)) with { SourceDisplayNumber = "PAY-22" };
        var sourceId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", [source], default)).Single();
        Assert.Equal(1L, await CountOutboxByCausationAsync(Tenant.Value, "OpenItemCreatedV1", sourceId));
        var service = new SubledgerService(repository, new ActivePartyDirectory(), new InstallmentFinanceBase(), new FixedEntityCurrency(),
            new AllPermissionSubjectStore(Tenant, Guid.Parse("10000000-0000-7000-8000-000000000001")), new PermissionAuthorizer(), TimeProvider.System);

        var result = await service.SuggestApplicationsAsync(Tenant, Guid.Parse("10000000-0000-7000-8000-000000000001"),
            new(Entity, sourceId, $"INV ref-22 {suffix}", "ReferenceMatchFirst", 10));

        Assert.Equal(80m, decimal.Parse(result.SourceAvailable, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("EGP", result.CurrencyCode);
        Assert.True(result.CrossCurrencyExcluded);
        Assert.Collection(result.Suggestions,
            exact => { Assert.Equal(debitIds[1], exact.DebitItemId); Assert.Equal("ExactReference", exact.ReasonCode); Assert.Equal("High", exact.ConfidenceClass); },
            oldest => { Assert.Equal(debitIds[0], oldest.DebitItemId); Assert.Equal("OldestDueFallback", oldest.ReasonCode); Assert.Equal("Low", oldest.ConfidenceClass); });
        Assert.Equal(0m, decimal.Parse(result.RemainingSource, System.Globalization.CultureInfo.InvariantCulture));
        Assert.All(result.Suggestions, line => Assert.True(line.ExpectedDebitVersion > 0 && line.ExpectedCreditVersion > 0));

        var oldestDue = await service.SuggestApplicationsAsync(Tenant, Guid.Parse("10000000-0000-7000-8000-000000000001"),
            new(Entity, sourceId, $"INV ref-22 {suffix}", "AutoOldestDue", 10));
        Assert.Equal(debitIds[0], oldestDue.Suggestions[0].DebitItemId);
        Assert.Equal("OldestDueFallback", oldestDue.Suggestions[0].ReasonCode);
        var oldestDocument = await service.SuggestApplicationsAsync(Tenant, Guid.Parse("10000000-0000-7000-8000-000000000001"),
            new(Entity, sourceId, $"INV ref-22 {suffix}", "AutoOldestDocument", 10));
        Assert.Equal(debitIds[0], oldestDocument.Suggestions[0].DebitItemId);
        var manual = await service.SuggestApplicationsAsync(Tenant, Guid.Parse("10000000-0000-7000-8000-000000000001"),
            new(Entity, sourceId, $"INV ref-22 {suffix}", "Manual", 10));
        Assert.Empty(manual.Suggestions);
        Assert.Equal(80m, decimal.Parse(manual.RemainingSource, System.Globalization.CultureInfo.InvariantCulture));

        var equalTarget = Isolated(Snapshot($"SUBLEDGER-MATCH-EQUAL-{suffix}", 25m)) with { SourceDisplayNumber = "REF-25" };
        var equalDebit = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [equalTarget], default)).Single();
        var equalSource = Isolated(Snapshot($"SUBLEDGER-MATCH-EQUAL-SOURCE-{suffix}", 25m));
        var equalCredit = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", [equalSource], default)).Single();
        var exactAmount = await service.SuggestApplicationsAsync(Tenant, Guid.Parse("10000000-0000-7000-8000-000000000001"),
            new(Entity, equalCredit, "ref25", "ReferenceMatchFirst", 5));
        Assert.Equal(equalDebit, Assert.Single(exactAmount.Suggestions).DebitItemId);
        Assert.Equal("ExactAmountAndReference", Assert.Single(exactAmount.Suggestions).ReasonCode);
    }

    [Fact]
    public async Task ApplicationCommitRejectsStaleSuggestionVersions()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var partyId = await FindActivePartyAsync(dataSource);
        var suffix = Guid.NewGuid().ToString("N");
        var debit = await CreateAsync(repository, partyId, "Invoice", "Debit", $"SUBLEDGER-STALE-DEBIT-{suffix}", 10m);
        var credit = await CreateAsync(repository, partyId, "Payment", "Credit", $"SUBLEDGER-STALE-CREDIT-{suffix}", 10m);
        var failure = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => repository.ApplyAsync(Tenant,
            new(Entity, partyId, SubledgerDirectionContract.Receivable, $"stale-suggestion-{suffix}", new(2026, 10, 25), new(2026, 10, 25),
                [new ApplicationLineCommand(debit, credit, "10", null, 999, 999)], Guid.CreateVersion7(), Guid.CreateVersion7()), [10m], 2,
            FinancialRoundingModeValue.HalfUp, default));
        Assert.Equal("subledger.item_changed", failure.Code);
        Assert.Equal(10m, await ReadOpenAmountAsync(dataSource, debit));
        Assert.Equal(10m, await ReadOpenAmountAsync(dataSource, credit));
    }

    [Fact]
    public async Task CrossCurrencyApplicationTracksBothTransactionAmountsAndReversesExactly()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var partyId = await FindActivePartyAsync(dataSource);
        var suffix = Guid.NewGuid().ToString("N");
        var asOf = new DateOnly(2026, 10, 25);
        var baseline = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, asOf), default);
        decimal BaselineDue(string currency) => baseline.Currencies.SingleOrDefault(item => item.CurrencyCode == currency) is { } item ? decimal.Parse(item.AmountDue, System.Globalization.CultureInfo.InvariantCulture) : 0m;
        decimal BaselineCredit(string currency) => baseline.Currencies.SingleOrDefault(item => item.CurrencyCode == currency) is { } item ? decimal.Parse(item.AvailableCredits, System.Globalization.CultureInfo.InvariantCulture) : 0m;
        var baselineStatement = await repository.GetStatementAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, asOf, asOf), default);
        var baselineUsdStatement = decimal.Parse(baselineStatement.ClosingBalances.GetValueOrDefault("USD", "0"), System.Globalization.CultureInfo.InvariantCulture);
        var baselineEgpStatement = decimal.Parse(baselineStatement.ClosingBalances.GetValueOrDefault("EGP", "0"), System.Globalization.CultureInfo.InvariantCulture);
        var usdInvoice = Snapshot($"SUBLEDGER-FX-INVOICE-{suffix}", 100m) with
        {
            TransactionCurrency = "USD", OriginalTransactionAmount = "100", OriginalBaseAmount = "5000",
            HistoricFxSnapshot = "{\"rate\":50,\"from\":\"USD\",\"to\":\"EGP\"}"
        };
        var egpPayment = Snapshot($"SUBLEDGER-FX-PAYMENT-{suffix}", 5000m);
        var debitId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [usdInvoice], default)).Single();
        var creditId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", [egpPayment], default)).Single();
        var application = new ApplyOpenItemsCommand(Entity, partyId, SubledgerDirectionContract.Receivable, $"fx-apply-{suffix}",
            new(2026, 10, 25), new(2026, 10, 25), [new(debitId, creditId, "40", "2000")], Guid.CreateVersion7(), Guid.CreateVersion7());
        var resolved = new ResolvedApplicationLineContract(debitId, creditId, "40", "2000",
            "{\"rate\":50,\"from\":\"USD\",\"to\":\"EGP\"}", "{\"rate\":1,\"from\":\"EGP\",\"to\":\"EGP\"}", "2000");
        var result = await repository.ApplyAsync(Tenant, application, [resolved], 2, FinancialRoundingModeValue.HalfUp, default);
        Assert.Equal(60m, await ReadOpenAmountAsync(dataSource, debitId));
        Assert.Equal(3000m, await ReadOpenAmountAsync(dataSource, creditId));
        var balance = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, asOf), default);
        Assert.Equal(60m, decimal.Parse(balance.Currencies.Single(item => item.CurrencyCode == "USD").AmountDue, System.Globalization.CultureInfo.InvariantCulture) - BaselineDue("USD"));
        Assert.Equal(3000m, decimal.Parse(balance.Currencies.Single(item => item.CurrencyCode == "EGP").AvailableCredits, System.Globalization.CultureInfo.InvariantCulture) - BaselineCredit("EGP"));
        var statement = await repository.GetStatementAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, asOf, asOf), default);
        Assert.Equal(60m, decimal.Parse(statement.ClosingBalances["USD"], System.Globalization.CultureInfo.InvariantCulture) - baselineUsdStatement);
        Assert.Equal(-3000m, decimal.Parse(statement.ClosingBalances["EGP"], System.Globalization.CultureInfo.InvariantCulture) - baselineEgpStatement);
        var reversed = await repository.ReverseAsync(Tenant, new(Entity, result.ApplicationIds.Single(), $"fx-reverse-{suffix}", new(2026, 10, 26),
            "Restore cross-currency settlement", Guid.CreateVersion7(), Guid.CreateVersion7()), default);
        Assert.Single(reversed.ApplicationIds);
        Assert.Equal(100m, await ReadOpenAmountAsync(dataSource, debitId));
        Assert.Equal(5000m, await ReadOpenAmountAsync(dataSource, creditId));
    }

    [Fact]
    public async Task CrossCurrencyLedgerFailureRollsBackApplicationAndBothOpenItemChanges()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System, new FailingLedgerPostingCapability());
        var partyId = await FindActivePartyAsync(dataSource);
        var suffix = Guid.NewGuid().ToString("N");
        var configurationReference = $"configuration:{await ReadCurrentLedgerConfigurationAsync(dataSource):D}";
        var usdInvoice = Snapshot($"SUBLEDGER-FX-ROLLBACK-INVOICE-{suffix}", 100m) with
        {
            TransactionCurrency = "USD", OriginalTransactionAmount = "100", OriginalBaseAmount = "5200",
            HistoricFxSnapshot = "{\"rate\":52,\"from\":\"USD\",\"to\":\"EGP\"}", ControlConfigurationReference = configurationReference
        };
        var egpPayment = Snapshot($"SUBLEDGER-FX-ROLLBACK-PAYMENT-{suffix}", 5000m) with { ControlConfigurationReference = configurationReference };
        var debitId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [usdInvoice], default)).Single();
        var creditId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", [egpPayment], default)).Single();
        var application = new ApplyOpenItemsCommand(Entity, partyId, SubledgerDirectionContract.Receivable, $"fx-rollback-{suffix}",
            new(2026, 10, 25), new(2026, 10, 25), [new(debitId, creditId, "100", "5000")], Guid.CreateVersion7(), Guid.CreateVersion7());
        var resolved = new ResolvedApplicationLineContract(debitId, creditId, "100", "5000",
            "{\"rate\":52,\"from\":\"USD\",\"to\":\"EGP\"}", "{\"rate\":1,\"from\":\"EGP\",\"to\":\"EGP\"}", "5000");
        var failure = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => repository.ApplyAsync(Tenant, application, [resolved], 2, FinancialRoundingModeValue.HalfUp, default));
        Assert.Equal("subledger.ledger_fx_post_failed", failure.Code);
        Assert.Equal(100m, await ReadOpenAmountAsync(dataSource, debitId));
        Assert.Equal(5000m, await ReadOpenAmountAsync(dataSource, creditId));
        Assert.Equal(0L, await CountApplicationsForItemsAsync(dataSource, [debitId, creditId]));
        Assert.Equal(0L, await CountBatchAsync(dataSource, "Apply", application.IdempotencyKey));
    }

    [Fact]
    public async Task CrossCurrencySettlementPostsRealizedFxToLedgerAndReversesWithOriginalEvidence()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var ledger = new LedgerRepository(dataSource, coordinator, new FixedEntityCurrency(), TimeProvider.System);
        var repository = new SubledgerRepository(dataSource, coordinator, TimeProvider.System, new LedgerTransactionCapability(ledger));
        var partyId = await FindActivePartyAsync(dataSource);
        var suffix = Guid.NewGuid().ToString("N");
        var configurationReference = $"configuration:{await ReadCurrentLedgerConfigurationAsync(dataSource):D}";
        var invoice = Snapshot($"SUBLEDGER-FX-LEDGER-INVOICE-{suffix}", 100m) with
        {
            TransactionCurrency = "USD", OriginalTransactionAmount = "100", OriginalBaseAmount = "5200",
            HistoricFxSnapshot = "{\"rate\":52,\"from\":\"USD\",\"to\":\"EGP\"}", ControlConfigurationReference = configurationReference
        };
        var payment = Snapshot($"SUBLEDGER-FX-LEDGER-PAYMENT-{suffix}", 5000m) with { ControlConfigurationReference = configurationReference };
        var debitId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [invoice], default)).Single();
        var creditId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", [payment], default)).Single();
        var application = new ApplyOpenItemsCommand(Entity, partyId, SubledgerDirectionContract.Receivable, $"fx-ledger-{suffix}",
            new(2026, 10, 25), new(2026, 10, 25), [new(debitId, creditId, "100", "5000")], Guid.CreateVersion7(), Guid.CreateVersion7());
        var resolved = new ResolvedApplicationLineContract(debitId, creditId, "100", "5000",
            "{\"rate\":52,\"from\":\"USD\",\"to\":\"EGP\"}", "{\"rate\":1,\"from\":\"EGP\",\"to\":\"EGP\"}", "5000");
        var result = await repository.ApplyAsync(Tenant, application, [resolved], 2, FinancialRoundingModeValue.HalfUp, default);
        Assert.Equal(0m, await ReadOpenAmountAsync(dataSource, debitId));
        Assert.Equal(0m, await ReadOpenBaseAmountAsync(dataSource, debitId));
        Assert.Equal(0m, await ReadOpenAmountAsync(dataSource, creditId));
        Assert.Equal(0m, await ReadOpenBaseAmountAsync(dataSource, creditId));
        Assert.Equal(200m, await ReadPostedFactTotalAsync(dataSource, "SettlementFxDifference", $"application:{result.ApplicationIds.Single():N}", "Settlement FX loss"));

        await repository.ReverseAsync(Tenant, new(Entity, result.ApplicationIds.Single(), $"fx-ledger-reverse-{suffix}", new(2026, 10, 26),
            "Reverse the realized FX settlement", Guid.CreateVersion7(), Guid.CreateVersion7()), default);
        Assert.Equal(100m, await ReadOpenAmountAsync(dataSource, debitId));
        Assert.Equal(5200m, await ReadOpenBaseAmountAsync(dataSource, debitId));
        Assert.Equal(5000m, await ReadOpenAmountAsync(dataSource, creditId));
        Assert.Equal(5000m, await ReadOpenBaseAmountAsync(dataSource, creditId));
        Assert.Equal(-200m, await ReadPostedFactTotalAsync(dataSource, "SettlementFxDifference", $"application-reversal:{result.ApplicationIds.Single():N}", "Settlement FX gain"));
        Assert.Equal(2L, await CountPostedFxFactsForApplicationAsync(dataSource, result.ApplicationIds.Single()));
    }

    [Fact]
    public async Task ApprovedWriteOffPostsLedgerFactEnforcesSeparationAndAppearsInHistoricalReports()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var ledger = new LedgerRepository(dataSource, coordinator, new FixedEntityCurrency(), TimeProvider.System);
        var repository = new SubledgerRepository(dataSource, coordinator, TimeProvider.System, new LedgerTransactionCapability(ledger));
        var partyId = await FindActivePartyAsync(dataSource);
        var suffix = Guid.NewGuid().ToString("N");
        var configurationReference = $"configuration:{await ReadCurrentLedgerConfigurationAsync(dataSource):D}";
        var itemSnapshot = Snapshot($"SUBLEDGER-WRITEOFF-{suffix}", 10m) with
        {
            TransactionCurrency = "USD", OriginalTransactionAmount = "10", OriginalBaseAmount = "500",
            HistoricFxSnapshot = "{\"rate\":50,\"from\":\"USD\",\"to\":\"EGP\"}", ControlConfigurationReference = configurationReference
        };
        var itemId = (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [itemSnapshot], default)).Single();
        var policyVersion = await ReadWriteOffPolicyVersionAsync(dataSource, "USD");
        var policy = await repository.SaveWriteOffPolicyAsync(Tenant,
            new(Entity, SubledgerDirectionContract.Receivable, "USD", "5", "1", policyVersion), 5m, 1m, "USD", default);
        Assert.False(policy.AutomaticWriteOffEnabled);
        var creator = Guid.CreateVersion7();
        var request = new CreateWriteOffCommand(Entity, itemId, "1", "ROUNDING", "Approved small difference", new(2026, 10, 26),
            $"writeoff-{suffix}", creator, Guid.CreateVersion7());
        var requestId = await repository.CreateWriteOffRequestAsync(Tenant, request, 1m, "USD", 2, "HalfUp", default);
        Assert.Equal(requestId, await repository.CreateWriteOffRequestAsync(Tenant, request, 1m, "USD", 2, "HalfUp", default));
        var selfApproval = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => repository.ApproveWriteOffAsync(Tenant,
            new(Entity, requestId, creator, Guid.CreateVersion7()), default));
        Assert.Equal("subledger.writeoff_sod_violation", selfApproval.Code);

        var before = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, new(2026, 10, 25)), default);
        var beforeCurrentAging = await repository.GetAgingAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, new(2026, 10, 26), "USD"), default);
        static decimal TotalAging(IReadOnlyList<AgingBucketResult> rows) => rows.Sum(row =>
            decimal.Parse(row.Current, System.Globalization.CultureInfo.InvariantCulture)
            + decimal.Parse(row.Days1To30, System.Globalization.CultureInfo.InvariantCulture)
            + decimal.Parse(row.Days31To60, System.Globalization.CultureInfo.InvariantCulture)
            + decimal.Parse(row.Days61To90, System.Globalization.CultureInfo.InvariantCulture)
            + decimal.Parse(row.Days91Plus, System.Globalization.CultureInfo.InvariantCulture));
        var approvedBy = Guid.CreateVersion7();
        var approval = await repository.ApproveWriteOffAsync(Tenant, new(Entity, requestId, approvedBy, Guid.CreateVersion7()), default);
        Assert.Equal(1m, decimal.Parse(approval.Amount, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(50m, decimal.Parse(approval.BaseAmount, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(9m, await ReadOpenAmountAsync(dataSource, itemId));
        Assert.Equal(450m, await ReadOpenBaseAmountAsync(dataSource, itemId));
        var replay = await repository.ApproveWriteOffAsync(Tenant, new(Entity, requestId, approvedBy, Guid.CreateVersion7()), default);
        Assert.True(replay.AlreadyCompleted);
        Assert.Equal(approval.Id, replay.Id);

        var historical = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, new(2026, 10, 25)), default);
        var current = await repository.GetPartyBalanceAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, new(2026, 10, 26)), default);
        var currentAging = await repository.GetAgingAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, new(2026, 10, 26), "USD"), default);
        Assert.Equal(-1m, TotalAging(currentAging) - TotalAging(beforeCurrentAging));
        Assert.Equal(decimal.Parse(before.Currencies.Single(row => row.CurrencyCode == "USD").AmountDue, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(historical.Currencies.Single(row => row.CurrencyCode == "USD").AmountDue, System.Globalization.CultureInfo.InvariantCulture));
        var statement = await repository.GetStatementAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, new(2026, 10, 25), new(2026, 10, 26)), default);
        var writeOffLine = Assert.Single(statement.Lines, line => line.SourceType == "WriteOff" && line.SourceId == approval.Id);
        Assert.Equal(-1m, decimal.Parse(writeOffLine.Amount, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(decimal.Parse(current.Currencies.Single(row => row.CurrencyCode == "USD").NetBalance, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(statement.ClosingBalances["USD"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1L, await CountWriteOffRecordsAsync(dataSource, requestId));
        Assert.Equal(2L, await CountWriteOffAuditAsync(dataSource, requestId));
        Assert.Equal(50m, await ReadWriteOffLedgerBaseAmountAsync(dataSource, approval.LedgerFactId));
    }

    [Fact]
    public async Task WriteOffLedgerFailureRollsBackAllBalanceAndApprovalChanges()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new SubledgerRepository(dataSource, coordinator, TimeProvider.System, new FailingLedgerPostingCapability());
        var partyId = await FindActivePartyAsync(dataSource);
        var suffix = Guid.NewGuid().ToString("N");
        var configurationReference = $"configuration:{await ReadCurrentLedgerConfigurationAsync(dataSource):D}";
        var itemId = await CreateAsync(repository, partyId, "Invoice", "Debit", $"SUBLEDGER-WRITEOFF-FAIL-{suffix}", 10m,
            controlConfigurationReference: configurationReference);
        var version = await ReadWriteOffPolicyVersionAsync(dataSource, "EGP");
        await repository.SaveWriteOffPolicyAsync(Tenant, new(Entity, SubledgerDirectionContract.Receivable, "EGP", "5", "1", version), 5m, 1m, "EGP", default);
        var creator = Guid.CreateVersion7();
        var request = new CreateWriteOffCommand(Entity, itemId, "1", "APPROVED_REASON", "Injected Ledger failure", new(2026, 10, 26),
            $"writeoff-fail-{suffix}", creator, Guid.CreateVersion7());
        var requestId = await repository.CreateWriteOffRequestAsync(Tenant, request, 1m, "EGP", 2, "HalfUp", default);
        var error = await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => repository.ApproveWriteOffAsync(Tenant,
            new(Entity, requestId, Guid.CreateVersion7(), Guid.CreateVersion7()), default));
        Assert.Equal("subledger.ledger_writeoff_post_failed", error.Code);
        Assert.Equal(10m, await ReadOpenAmountAsync(dataSource, itemId));
        Assert.Equal(10m, await ReadOpenBaseAmountAsync(dataSource, itemId));
        Assert.Equal(0L, await CountWriteOffRecordsAsync(dataSource, requestId));
        Assert.Equal(1L, await CountWriteOffAuditAsync(dataSource, requestId));
        Assert.Equal("PendingApproval", await ReadWriteOffRequestStatusAsync(dataSource, requestId));
    }

    [Fact]
    public async Task ControlReconciliationUsesLedgerContractAndPersistsMismatchDiagnostics()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var ledger = new LedgerRepository(dataSource, coordinator, new FixedEntityCurrency(), TimeProvider.System);
        var repository = new SubledgerRepository(dataSource, coordinator, TimeProvider.System, new LedgerTransactionCapability(ledger));
        var membership = Guid.CreateVersion7();
        var service = new SubledgerService(repository, new ActivePartyDirectory(), new InstallmentFinanceBase(), new FixedEntityCurrency(),
            new AllPermissionSubjectStore(Tenant, membership), new PermissionAuthorizer(), TimeProvider.System, ledger);

        var result = await service.ReconcileControlBalancesAsync(Tenant, membership,
            new(Entity, new(2026, 12, 31)), Guid.CreateVersion7(), Guid.CreateVersion7(), default);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.False(result.IsBalanced);
        Assert.NotEmpty(result.Details);
        Assert.Contains(result.Details, detail => detail.ContextResolved);
        Assert.Contains(result.Details, detail => !detail.ContextResolved || decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture) != 0m);
        Assert.Contains(result.Details, detail => detail.Direction == SubledgerDirectionContract.Receivable);
        Assert.Contains(result.Details, detail => detail.Direction == SubledgerDirectionContract.Payable);
        Assert.Equal(result.Details.Sum(detail => decimal.Parse(detail.SubledgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture)), decimal.Parse(result.SubledgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(result.Details.Sum(detail => decimal.Parse(detail.LedgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture)), decimal.Parse(result.LedgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1L, await CountReconciliationRunsAsync(dataSource, result.Id));
        var controlAccounts = await service.GetReconciliationControlAccountsAsync(Tenant, membership, new(Entity, new(2026, 12, 31)), default);
        Assert.NotEmpty(controlAccounts);
        var loadedRun = await service.GetReconciliationRunAsync(Tenant, membership, Entity, result.Id, default);
        Assert.NotNull(loadedRun);
        Assert.Equal(result.Details.Select(detail => detail.Id).Order(), loadedRun.Details.Select(detail => detail.Id).Order());
        Assert.Equal(result.Details.ToDictionary(detail => detail.Id, detail => decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture)),
            loadedRun.Details.ToDictionary(detail => detail.Id, detail => decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task OpeningSubledgerBatchReconcilesBothDirectionsAndFinalizesAtomically()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var ledger = new LedgerRepository(dataSource, coordinator, new FixedEntityCurrency(), TimeProvider.System);
        var ledgerOperations = new LedgerOperationsRepository(dataSource, coordinator, ledger, TimeProvider.System);
        var configId = await ReadCurrentLedgerConfigurationAsync(dataSource);
        var configurationReference = $"configuration:{configId:D}";
        var accounts = await ledger.ListAccountsAsync(Tenant, Entity, default);
        var receivableAccount = (await ledger.GetControlBalanceAsync(Tenant, Entity, "Receivable", configId, new(2026, 10, 25))).AccountId;
        var payableAccount = (await ledger.GetControlBalanceAsync(Tenant, Entity, "Payable", configId, new(2026, 10, 25))).AccountId;
        var cashAccount = accounts.Single(account => account.Code == "110000").Id;
        var beforeAr = await ledger.GetControlBalanceAsync(Tenant, Entity, "Receivable", configId, new(2026, 10, 25));
        var beforeAp = await ledger.GetControlBalanceAsync(Tenant, Entity, "Payable", configId, new(2026, 10, 25));
        // Start with valid positive party-control balances even when a shared development
        // database already contains unrelated journals from earlier integration runs.
        var targetAr = Math.Max(beforeAr.SignedBaseBalance + 100m, 100m);
        var targetAp = Math.Max(beforeAp.SignedBaseBalance + 50m, 50m);
        var deltaAr = targetAr - beforeAr.SignedBaseBalance;
        var deltaAp = targetAp - beforeAp.SignedBaseBalance;
        var lines = new List<OpeningBalanceLineRequest>();
        AddSignedLine(lines, receivableAccount, deltaAr, "EGP");
        AddSignedLine(lines, payableAccount, -deltaAp, "EGP");
        AddSignedLine(lines, cashAccount, -deltaAr + deltaAp, "EGP");
        var openingDate = new DateOnly(2026, 10, 25);
        var ledgerBatch = await ledgerOperations.CreateOpeningBatchAsync(Tenant, Entity,
            new($"SUBLEDGER-GL-OPEN-{Guid.NewGuid():N}", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant(), "MigrationTest", openingDate, lines),
            Guid.CreateVersion7(), default);
        await ledgerOperations.PostOpeningBatchAsync(Tenant, Entity, ledgerBatch, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        Assert.Equal(targetAr, (await ledger.GetControlBalanceAsync(Tenant, Entity, "Receivable", configId, openingDate)).SignedBaseBalance);
        Assert.Equal(targetAp, (await ledger.GetControlBalanceAsync(Tenant, Entity, "Payable", configId, openingDate)).SignedBaseBalance);

        var repository = new SubledgerRepository(dataSource, coordinator, TimeProvider.System);
        var membership = Guid.CreateVersion7();
        var service = new SubledgerService(repository, new ActivePartyDirectory(), new InstallmentFinanceBase(), new FixedEntityCurrency(),
            new AllPermissionSubjectStore(Tenant, membership), new PermissionAuthorizer(), TimeProvider.System, ledger);
        var partyId = await FindActivePartyAsync(dataSource);
        var actorId = Guid.CreateVersion7();
        var command = new CreateOpeningSubledgerBatchCommand(Entity, $"SUBLEDGER-OPEN-{Guid.NewGuid():N}", openingDate, "EGP", configurationReference,
            $"opening-batch-{Guid.NewGuid():N}", actorId, Guid.CreateVersion7());
        var batchId = await service.CreateOpeningSubledgerBatchAsync(Tenant, membership, command, default);
        var openingLines = new OpeningSubledgerLineCommand[]
        {
            new("a-ar-invoice", partyId, SubledgerDirectionContract.Receivable, SubledgerPolarityContract.Debit, "legacy-ar-100", openingDate, openingDate.AddDays(30), "EGP", (targetAr - 20m).ToString(System.Globalization.CultureInfo.InvariantCulture), "EGP", (targetAr - 20m).ToString(System.Globalization.CultureInfo.InvariantCulture), "Receivable", "role:Receivable", configurationReference),
            new("b-ar-foreign", partyId, SubledgerDirectionContract.Receivable, SubledgerPolarityContract.Debit, "legacy-ar-usd", openingDate, openingDate.AddDays(45), "USD", "0.6", "EGP", "30", "Receivable", "role:Receivable", configurationReference, "{\"rate\":50,\"from\":\"USD\",\"to\":\"EGP\"}"),
            new("c-ar-advance", partyId, SubledgerDirectionContract.Receivable, SubledgerPolarityContract.Credit, "legacy-ar-credit", openingDate, openingDate, "EGP", "10", "EGP", "10", "Receivable", "role:Receivable", configurationReference),
            new("d-ap-bill", partyId, SubledgerDirectionContract.Payable, SubledgerPolarityContract.Credit, "legacy-ap-60", openingDate, openingDate.AddDays(30), "EGP", (targetAp + 10m).ToString(System.Globalization.CultureInfo.InvariantCulture), "EGP", (targetAp + 10m).ToString(System.Globalization.CultureInfo.InvariantCulture), "Payable", "role:Payable", configurationReference),
            new("e-ap-advance", partyId, SubledgerDirectionContract.Payable, SubledgerPolarityContract.Debit, "legacy-ap-credit", openingDate, openingDate, "EGP", "10", "EGP", "10", "Payable", "role:Payable", configurationReference)
        };
        await service.AddOpeningSubledgerLinesAsync(Tenant, membership, Entity, batchId, openingLines, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var validation = await service.ValidateOpeningSubledgerBatchAsync(Tenant, membership, Entity, batchId, default);
        Assert.True(validation.IsBalanced);
        Assert.Equal(openingLines.Length, validation.LineCount);
        var finalized = await service.FinalizeOpeningSubledgerBatchAsync(Tenant, membership, Entity, batchId, actorId, Guid.CreateVersion7(), default);
        Assert.Equal("Finalized", finalized.Status);
        Assert.Equal(5, finalized.LineCount);
        Assert.Equal(5, finalized.OpenItemIds.Count);
        Assert.False(finalized.AlreadyCompleted);
        var replay = await service.FinalizeOpeningSubledgerBatchAsync(Tenant, membership, Entity, batchId, actorId, Guid.CreateVersion7(), default);
        Assert.True(replay.AlreadyCompleted);
        Assert.Equal(finalized.OpenItemIds.Order(), replay.OpenItemIds.Order());
        Assert.Equal(targetAr, await SumOpeningSubledgerBalancesAsync(dataSource, batchId, partyId, SubledgerDirectionContract.Receivable));
        Assert.Equal(targetAp, await SumOpeningSubledgerBalancesAsync(dataSource, batchId, partyId, SubledgerDirectionContract.Payable));

        var failingBatch = await service.CreateOpeningSubledgerBatchAsync(Tenant, membership, command with
        {
            BatchIdentity = $"SUBLEDGER-OPEN-FAIL-{Guid.NewGuid():N}", IdempotencyKey = $"opening-fail-{Guid.NewGuid():N}"
        }, default);
        await service.AddOpeningSubledgerLinesAsync(Tenant, membership, Entity, failingBatch, openingLines, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var collisionReference = $"OB-{failingBatch:N}-e-ap-advance";
        await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Payable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "OpeningBalance", [Snapshot(collisionReference, 10m, controlRole: "Payable")], default);
        var preFinalizeRows = await CountOpeningItemsBySourceAsync(dataSource, collisionReference);
        await Assert.ThrowsAsync<PostgresException>(() => service.FinalizeOpeningSubledgerBatchAsync(Tenant, membership, Entity, failingBatch, actorId, Guid.CreateVersion7(), default));
        Assert.Equal("Draft", (await repository.GetOpeningSubledgerBatchAsync(Tenant, Entity, failingBatch, default))!.Status);
        Assert.Equal(preFinalizeRows, await CountOpeningItemsBySourceAsync(dataSource, collisionReference));
        Assert.Equal(0L, await CountOpeningBatchLinksAsync(dataSource, failingBatch));
    }

    [Fact]
    public async Task LiveArApControlTotalsRemainExactlyEqualAcrossMultiPartyApplications()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var ledger = new LedgerRepository(dataSource, coordinator, new FixedEntityCurrency(), TimeProvider.System);
        var ledgerOperations = new LedgerOperationsRepository(dataSource, coordinator, ledger, TimeProvider.System);
        var entityId = new LegalEntityId(await FindSecondaryLegalEntityAsync(dataSource));
        var configurationId = await ReadCurrentLedgerConfigurationAsync(dataSource, entityId);
        var configurationReference = $"configuration:{configurationId:D}";
        var partyA = await FindActivePartyAsync(dataSource);
        var openingDate = new DateOnly(2026, 10, 25);
        var receivable = await ledger.GetControlBalanceAsync(Tenant, entityId, "Receivable", configurationId, new(2026, 10, 25));
        var payable = await ledger.GetControlBalanceAsync(Tenant, entityId, "Payable", configurationId, new(2026, 10, 25));
        var membershipId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var repository = new SubledgerRepository(dataSource, coordinator, TimeProvider.System, new LedgerTransactionCapability(ledger));
        var service = new SubledgerService(repository, new ActivePartyDirectory(), new InstallmentFinanceBase(), new FixedEntityCurrency(),
            new AllPermissionSubjectStore(Tenant, membershipId, entityId), new PermissionAuthorizer(), TimeProvider.System, ledger);
        var initial = await service.ReconcileControlBalancesAsync(Tenant, membershipId,
            new(entityId, new DateOnly(2026, 10, 25)), actorId, Guid.CreateVersion7(), default);
        var deltaAr = 100m;
        var deltaAp = 50m;
        var initialAr = initial.Details.FirstOrDefault(detail => detail.Direction == SubledgerDirectionContract.Receivable);
        var initialAp = initial.Details.FirstOrDefault(detail => detail.Direction == SubledgerDirectionContract.Payable);
        var targetAr = (initialAr is null ? 0m : decimal.Parse(initialAr.SubledgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture)) + deltaAr;
        var targetAp = (initialAp is null ? 0m : decimal.Parse(initialAp.SubledgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture)) + deltaAp;
        var accounts = await ledger.ListAccountsAsync(Tenant, entityId, default);
        var cashAccountId = accounts.Single(account => account.Code == "110000").Id;
        var ledgerLines = new List<OpeningBalanceLineRequest>();
        AddSignedLine(ledgerLines, receivable.AccountId, targetAr - receivable.SignedBaseBalance, "EGP");
        AddSignedLine(ledgerLines, payable.AccountId, -(targetAp - payable.SignedBaseBalance), "EGP");
        AddSignedLine(ledgerLines, cashAccountId, -(targetAr - receivable.SignedBaseBalance) + (targetAp - payable.SignedBaseBalance), "EGP");
        var ledgerBatch = await ledgerOperations.CreateOpeningBatchAsync(Tenant, entityId,
            new($"SL25-LIVE-OPEN-{Guid.NewGuid():N}", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant(), "SL25Acceptance", openingDate, ledgerLines),
            Guid.CreateVersion7(), default);
        await ledgerOperations.PostOpeningBatchAsync(Tenant, entityId, ledgerBatch, Guid.CreateVersion7(), Guid.CreateVersion7(), default);

        var partyB = await FindAnotherActivePartyAsync(dataSource, partyA);
        var batch = await service.CreateOpeningSubledgerBatchAsync(Tenant, membershipId,
            new(entityId, $"SL25-LIVE-SUB-{Guid.NewGuid():N}", openingDate, "EGP", configurationReference, $"sl25-live-{Guid.NewGuid():N}", actorId, Guid.CreateVersion7()), default);
        var lines = new OpeningSubledgerLineCommand[]
        {
            new("ar-a-invoice", partyA, SubledgerDirectionContract.Receivable, SubledgerPolarityContract.Debit, "SL25-AR-A", openingDate, openingDate.AddDays(30), "EGP", (deltaAr - 40m).ToString(System.Globalization.CultureInfo.InvariantCulture), "EGP", (deltaAr - 40m).ToString(System.Globalization.CultureInfo.InvariantCulture), "Receivable", "role:Receivable", configurationReference),
            new("ar-b-invoice", partyB, SubledgerDirectionContract.Receivable, SubledgerPolarityContract.Debit, "SL25-AR-B", openingDate, openingDate.AddDays(30), "EGP", "50", "EGP", "50", "Receivable", "role:Receivable", configurationReference),
            new("ar-a-advance", partyA, SubledgerDirectionContract.Receivable, SubledgerPolarityContract.Credit, "SL25-AR-ADV", openingDate, openingDate, "EGP", "10", "EGP", "10", "Receivable", "role:Receivable", configurationReference),
            new("ap-a-bill", partyA, SubledgerDirectionContract.Payable, SubledgerPolarityContract.Credit, "SL25-AP-A", openingDate, openingDate.AddDays(30), "EGP", (deltaAp - 15m).ToString(System.Globalization.CultureInfo.InvariantCulture), "EGP", (deltaAp - 15m).ToString(System.Globalization.CultureInfo.InvariantCulture), "Payable", "role:Payable", configurationReference),
            new("ap-b-bill", partyB, SubledgerDirectionContract.Payable, SubledgerPolarityContract.Credit, "SL25-AP-B", openingDate, openingDate.AddDays(30), "EGP", "25", "EGP", "25", "Payable", "role:Payable", configurationReference),
            new("ap-a-advance", partyA, SubledgerDirectionContract.Payable, SubledgerPolarityContract.Debit, "SL25-AP-ADV", openingDate, openingDate, "EGP", "10", "EGP", "10", "Payable", "role:Payable", configurationReference)
        };
        await service.AddOpeningSubledgerLinesAsync(Tenant, membershipId, entityId, batch, lines, actorId, Guid.CreateVersion7(), default);
        var openingValidation = await service.ValidateOpeningSubledgerBatchAsync(Tenant, membershipId, entityId, batch, default);
        Assert.True(openingValidation.IsBalanced, System.Text.Json.JsonSerializer.Serialize(openingValidation.Directions));
        var finalized = await service.FinalizeOpeningSubledgerBatchAsync(Tenant, membershipId, entityId, batch, actorId, Guid.CreateVersion7(), default);
        Assert.Equal("Finalized", finalized.Status);

        var before = await service.ReconcileControlBalancesAsync(Tenant, membershipId, new(entityId, openingDate), actorId, Guid.CreateVersion7(), default);
        Assert.True(before.IsBalanced, System.Text.Json.JsonSerializer.Serialize(before.Details));
        Assert.Equal(0m, decimal.Parse(before.Variance, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(targetAr, decimal.Parse(before.Details.Single(detail => detail.Direction == SubledgerDirectionContract.Receivable).SubledgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(targetAr, decimal.Parse(before.Details.Single(detail => detail.Direction == SubledgerDirectionContract.Receivable).LedgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(targetAp, decimal.Parse(before.Details.Single(detail => detail.Direction == SubledgerDirectionContract.Payable).SubledgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(targetAp, decimal.Parse(before.Details.Single(detail => detail.Direction == SubledgerDirectionContract.Payable).LedgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture));

        var invoiceAReference = $"SL25-AR-INVOICE-A-{Guid.CreateVersion7():N}";
        var invoiceAPayload = new CustomerInvoicePostedPayload(100m, 14m, 114m, "EGP", "CUSTOMER", "GENERAL", [new("VAT14", 14m, 14m, "OutputTax")]);
        var invoiceAFact = new AccountingFactEnvelope<CustomerInvoicePostedPayload>(Guid.CreateVersion7(), Tenant, entityId, AccountingFactTypes.CustomerInvoicePosted, 1, "sales", invoiceAReference, $"sl25-a-{Guid.CreateVersion7():N}", openingDate, openingDate, invoiceAPayload, Guid.CreateVersion7(), Guid.CreateVersion7());
        var invoiceAEntry = await ledger.PostCustomerInvoiceAsync(invoiceAFact, default);
        var invoiceAItem = Snapshot(invoiceAReference, 114m) with { ControlAccountReference = "role:Receivable", ControlConfigurationReference = configurationReference };
        var invoiceAOpenItemId = (await repository.CreateOpenItemsAsync(Tenant, entityId, partyA, Modules.Subledger.Entities.SubledgerDirection.Receivable, Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [invoiceAItem], default)).Single();
        Assert.NotEqual(Guid.Empty, invoiceAEntry.JournalEntryId);
        var afterInvoiceA = await service.ReconcileControlBalancesAsync(Tenant, membershipId, new(entityId, openingDate), actorId, Guid.CreateVersion7(), default);
        Assert.True(afterInvoiceA.IsBalanced, System.Text.Json.JsonSerializer.Serialize(afterInvoiceA.Details));
        Assert.All(afterInvoiceA.Details, detail => Assert.Equal(0m, decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture)));

        var cashLinesBefore = await CountJournalLinesByAccountAsync(dataSource, entityId, cashAccountId);
        var arApplicationId = await ApplyWithPreviewAsync(service, membershipId, entityId, partyA, SubledgerDirectionContract.Receivable,
            await ReadOpeningItemIdAsync(dataSource, batch, "ar-a-invoice"),
            await ReadOpeningItemIdAsync(dataSource, batch, "ar-a-advance"), "10", openingDate, actorId);
        var apApplicationId = await ApplyWithPreviewAsync(service, membershipId, entityId, partyA, SubledgerDirectionContract.Payable,
            await ReadOpeningItemIdAsync(dataSource, batch, "ap-a-advance"),
            await ReadOpeningItemIdAsync(dataSource, batch, "ap-a-bill"), "10", openingDate, actorId);
        Assert.Equal(cashLinesBefore, await CountJournalLinesByAccountAsync(dataSource, entityId, cashAccountId));

        var after = await service.ReconcileControlBalancesAsync(Tenant, membershipId, new(entityId, openingDate), actorId, Guid.CreateVersion7(), default);
        Assert.True(after.IsBalanced);
        Assert.Equal(0m, decimal.Parse(after.Variance, System.Globalization.CultureInfo.InvariantCulture));
        Assert.All(after.Details, detail => Assert.Equal(0m, decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture)));

        await service.ReverseAsync(Tenant, membershipId, new(entityId, arApplicationId, $"sl25-reverse-ar-{Guid.CreateVersion7():N}", openingDate.AddDays(1),
            "SL25 exact reconciliation reversal", actorId, Guid.CreateVersion7()), default);
        await service.ReverseAsync(Tenant, membershipId, new(entityId, apApplicationId, $"sl25-reverse-ap-{Guid.CreateVersion7():N}", openingDate.AddDays(1),
            "SL25 exact reconciliation reversal", actorId, Guid.CreateVersion7()), default);
        Assert.Equal(cashLinesBefore, await CountJournalLinesByAccountAsync(dataSource, entityId, cashAccountId));
        var reversed = await service.ReconcileControlBalancesAsync(Tenant, membershipId,
            new(entityId, openingDate.AddDays(1)), actorId, Guid.CreateVersion7(), default);
        Assert.True(reversed.IsBalanced, System.Text.Json.JsonSerializer.Serialize(reversed.Details));
        Assert.Equal(0m, decimal.Parse(reversed.Variance, System.Globalization.CultureInfo.InvariantCulture));
        Assert.All(reversed.Details, detail => Assert.Equal(0m, decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture)));

        var fxInvoice = Snapshot($"SL25-FX-INVOICE-{Guid.CreateVersion7():N}", 100m) with
        {
            TransactionCurrency = "USD", OriginalTransactionAmount = "100", OriginalBaseAmount = "5200",
            ControlAccountReference = "role:Receivable", ControlConfigurationReference = configurationReference,
            HistoricFxSnapshot = "{\"rate\":52,\"from\":\"USD\",\"to\":\"EGP\"}"
        };
        var fxPayment = Snapshot($"SL25-FX-PAYMENT-{Guid.CreateVersion7():N}", 5000m) with
        {
            ControlAccountReference = "role:Receivable", ControlConfigurationReference = configurationReference
        };
        var fxInvoiceId = (await repository.CreateOpenItemsAsync(Tenant, entityId, partyA, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [fxInvoice], default)).Single();
        var fxPaymentId = (await repository.CreateOpenItemsAsync(Tenant, entityId, partyA, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", [fxPayment], default)).Single();
        var revenueAccountId = accounts.Single(account => account.Code == "410100").Id;
        var fxOpeningLines = new List<OpeningBalanceLineRequest>();
        AddSignedLine(fxOpeningLines, receivable.AccountId, 200m, "EGP");
        AddSignedLine(fxOpeningLines, cashAccountId, 5000m, "EGP");
        AddSignedLine(fxOpeningLines, revenueAccountId, -5200m, "EGP");
        var fxOpeningBatch = await ledgerOperations.CreateOpeningBatchAsync(Tenant, entityId,
            new($"SL25-FX-OPEN-{Guid.CreateVersion7():N}", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant(),
                "SL25FxAcceptance", openingDate.AddDays(1), fxOpeningLines), Guid.CreateVersion7(), default);
        await ledgerOperations.PostOpeningBatchAsync(Tenant, entityId, fxOpeningBatch, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var beforeFx = await service.ReconcileControlBalancesAsync(Tenant, membershipId,
            new(entityId, openingDate.AddDays(1)), actorId, Guid.CreateVersion7(), default);
        Assert.True(beforeFx.IsBalanced, System.Text.Json.JsonSerializer.Serialize(beforeFx.Details));
        var fxCashLinesBefore = await CountJournalLinesByAccountAsync(dataSource, entityId, cashAccountId);
        var fxApplication = new ApplyOpenItemsCommand(entityId, partyA, SubledgerDirectionContract.Receivable,
            $"sl25-fx-application-{Guid.CreateVersion7():N}", openingDate.AddDays(1), openingDate.AddDays(1),
            [new(fxInvoiceId, fxPaymentId, "100", "5000")], actorId, Guid.CreateVersion7());
        var fxLine = new ResolvedApplicationLineContract(fxInvoiceId, fxPaymentId, "100", "5000",
            "{\"rate\":52,\"from\":\"USD\",\"to\":\"EGP\"}", "{\"rate\":1,\"from\":\"EGP\",\"to\":\"EGP\"}", "5000");
        var fxResult = await repository.ApplyAsync(Tenant, fxApplication, [fxLine], 2, FinancialRoundingModeValue.HalfUp, default);
        var fxApplicationId = Assert.Single(fxResult.ApplicationIds);
        Assert.Equal(0m, await ReadOpenAmountAsync(dataSource, entityId, fxInvoiceId));
        Assert.Equal(0m, await ReadOpenBaseAmountAsync(dataSource, entityId, fxInvoiceId));
        Assert.Equal(0m, await ReadOpenAmountAsync(dataSource, entityId, fxPaymentId));
        Assert.Equal(0m, await ReadOpenBaseAmountAsync(dataSource, entityId, fxPaymentId));
        Assert.Equal(200m, await ReadPostedFactTotalAsync(dataSource, entityId, "SettlementFxDifference", $"application:{fxApplicationId:N}", "Settlement FX loss"));
        Assert.Equal(fxCashLinesBefore, await CountJournalLinesByAccountAsync(dataSource, entityId, cashAccountId));
        var afterFx = await service.ReconcileControlBalancesAsync(Tenant, membershipId,
            new(entityId, openingDate.AddDays(1)), actorId, Guid.CreateVersion7(), default);
        Assert.True(afterFx.IsBalanced, System.Text.Json.JsonSerializer.Serialize(afterFx.Details));
        Assert.All(afterFx.Details, detail => Assert.Equal(0m, decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture)));

        await repository.ReverseAsync(Tenant, new(entityId, fxApplicationId, $"sl25-fx-reverse-{Guid.CreateVersion7():N}", openingDate.AddDays(2),
            "SL25 realized FX reversal", actorId, Guid.CreateVersion7()), default);
        Assert.Equal(-200m, await ReadPostedFactTotalAsync(dataSource, entityId, "SettlementFxDifference", $"application-reversal:{fxApplicationId:N}", "Settlement FX gain"));
        Assert.Equal(2L, await CountPostedFxFactsForApplicationAsync(dataSource, entityId, fxApplicationId));
        var afterFxReversal = await service.ReconcileControlBalancesAsync(Tenant, membershipId,
            new(entityId, openingDate.AddDays(2)), actorId, Guid.CreateVersion7(), default);
        Assert.True(afterFxReversal.IsBalanced, System.Text.Json.JsonSerializer.Serialize(afterFxReversal.Details));
        Assert.All(afterFxReversal.Details, detail => Assert.Equal(0m, decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture)));

        var mappingLifecycle = new LedgerMappingConfigurationRepository(dataSource, coordinator, new LedgerConfigurationCache(), TimeProvider.System);
        var receivableB = (await ledger.CreateAccountAsync(Tenant, entityId, new($"8{Guid.NewGuid():N}"[..13], "عملاء إصدار ب", "Receivables B", AccountType.Asset, "ReceivableControl", null, true), default)).Id;
        var payableB = (await ledger.CreateAccountAsync(Tenant, entityId, new($"7{Guid.NewGuid():N}"[..13], "موردون إصدار ب", "Payables B", AccountType.Liability, "PayableControl", null, true), default)).Id;
        var configBDraft = await mappingLifecycle.CreateDraftAsync(Tenant, entityId,
            new(DateOnly.FromDateTime(DateTime.UtcNow)), actorId, Guid.CreateVersion7(), default);
        await mappingLifecycle.UpdateDraftMappingAsync(Tenant, entityId, configBDraft.Id,
            configBDraft.Mappings.Single(mapping => mapping.RoleCode == "Receivable").MappingVersionId,
            new(receivableB), actorId, Guid.CreateVersion7(), default);
        await mappingLifecycle.UpdateDraftMappingAsync(Tenant, entityId, configBDraft.Id,
            configBDraft.Mappings.Single(mapping => mapping.RoleCode == "Payable").MappingVersionId,
            new(payableB), actorId, Guid.CreateVersion7(), default);
        var configBValidation = await mappingLifecycle.ValidateAsync(Tenant, entityId, configBDraft.Id, actorId, Guid.CreateVersion7(), default);
        Assert.True(configBValidation.Valid, System.Text.Json.JsonSerializer.Serialize(configBValidation.Errors));
        await mappingLifecycle.ActivateAsync(Tenant, entityId, configBDraft.Id, actorId, Guid.CreateVersion7(), default);
        var configurationB = configBDraft.Id;
        var configurationBReference = $"configuration:{configurationB:D}";
        var bReceivableControl = await ledger.GetControlBalanceAsync(Tenant, entityId, "Receivable", configurationB, openingDate.AddDays(2));
        var bPayableControl = await ledger.GetControlBalanceAsync(Tenant, entityId, "Payable", configurationB, openingDate.AddDays(2));
        Assert.Equal(receivableB, bReceivableControl.AccountId);
        Assert.Equal(payableB, bPayableControl.AccountId);

        const decimal deltaArB = 20m;
        const decimal deltaApB = 15m;
        var bLedgerLines = new List<OpeningBalanceLineRequest>();
        AddSignedLine(bLedgerLines, bReceivableControl.AccountId, deltaArB, "EGP");
        AddSignedLine(bLedgerLines, bPayableControl.AccountId, -deltaApB, "EGP");
        AddSignedLine(bLedgerLines, cashAccountId, -deltaArB + deltaApB, "EGP");
        var bLedgerBatch = await ledgerOperations.CreateOpeningBatchAsync(Tenant, entityId,
            new($"SL25-LIVE-OPEN-B-{Guid.NewGuid():N}", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant(), "SL25AcceptanceB", openingDate.AddDays(2), bLedgerLines),
            actorId, default);
        await ledgerOperations.PostOpeningBatchAsync(Tenant, entityId, bLedgerBatch, actorId, Guid.CreateVersion7(), default);
        var bSubledgerBatch = await service.CreateOpeningSubledgerBatchAsync(Tenant, membershipId,
            new(entityId, $"SL25-LIVE-SUB-B-{Guid.NewGuid():N}", openingDate.AddDays(2), "EGP", configurationBReference, $"sl25-live-b-{Guid.CreateVersion7():N}", actorId, Guid.CreateVersion7()), default);
        await service.AddOpeningSubledgerLinesAsync(Tenant, membershipId, entityId, bSubledgerBatch,
        [
            new("ar-b-new-context", partyA, SubledgerDirectionContract.Receivable, SubledgerPolarityContract.Debit, $"SL25-AR-B-{Guid.CreateVersion7():N}", openingDate.AddDays(2), openingDate.AddDays(32), "EGP", deltaArB.ToString(System.Globalization.CultureInfo.InvariantCulture), "EGP", deltaArB.ToString(System.Globalization.CultureInfo.InvariantCulture), "Receivable", "role:Receivable", configurationBReference),
            new("ap-b-new-context", partyB, SubledgerDirectionContract.Payable, SubledgerPolarityContract.Credit, $"SL25-AP-B-{Guid.CreateVersion7():N}", openingDate.AddDays(2), openingDate.AddDays(32), "EGP", deltaApB.ToString(System.Globalization.CultureInfo.InvariantCulture), "EGP", deltaApB.ToString(System.Globalization.CultureInfo.InvariantCulture), "Payable", "role:Payable", configurationBReference)
        ], actorId, Guid.CreateVersion7(), default);
        var bValidation = await service.ValidateOpeningSubledgerBatchAsync(Tenant, membershipId, entityId, bSubledgerBatch, default);
        Assert.True(bValidation.IsBalanced, System.Text.Json.JsonSerializer.Serialize(bValidation.Directions));
        await service.FinalizeOpeningSubledgerBatchAsync(Tenant, membershipId, entityId, bSubledgerBatch, actorId, Guid.CreateVersion7(), default);

        var invoiceBReference = $"SL25-AR-INVOICE-B-{Guid.CreateVersion7():N}";
        var invoiceBFact = new AccountingFactEnvelope<CustomerInvoicePostedPayload>(Guid.CreateVersion7(), Tenant, entityId, AccountingFactTypes.CustomerInvoicePosted, 1, "sales", invoiceBReference, $"sl25-b-{Guid.CreateVersion7():N}", openingDate.AddDays(2), openingDate.AddDays(2), invoiceAPayload, Guid.CreateVersion7(), Guid.CreateVersion7());
        var invoiceBEntry = await ledger.PostCustomerInvoiceAsync(invoiceBFact, default);
        var invoiceBItem = Snapshot(invoiceBReference, 114m) with { ControlAccountReference = "role:Receivable", ControlConfigurationReference = configurationBReference };
        var invoiceBOpenItemId = (await repository.CreateOpenItemsAsync(Tenant, entityId, partyB, Modules.Subledger.Entities.SubledgerDirection.Receivable, Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", [invoiceBItem], default)).Single();
        Assert.NotEqual(Guid.Empty, invoiceBEntry.JournalEntryId);

        Assert.Equal(configurationReference, await ReadOpenItemConfigurationReferenceAsync(dataSource, await ReadOpeningItemIdAsync(dataSource, batch, "ar-b-invoice")));
        Assert.Equal(configurationBReference, await ReadOpenItemConfigurationReferenceAsync(dataSource, await ReadOpeningItemIdAsync(dataSource, bSubledgerBatch, "ar-b-new-context")));
        var historicalContextBeforeWriteOff = await service.ReconcileControlBalancesAsync(Tenant, membershipId, new(entityId, openingDate.AddDays(2)), actorId, Guid.CreateVersion7(), default);
        Assert.True(historicalContextBeforeWriteOff.IsBalanced, System.Text.Json.JsonSerializer.Serialize(historicalContextBeforeWriteOff.Details));
        Assert.All(historicalContextBeforeWriteOff.Details, detail => Assert.Equal(0m, decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(configurationReference, await ReadOpenItemConfigurationReferenceAsync(dataSource, invoiceAOpenItemId));
        Assert.Equal(configurationBReference, await ReadOpenItemConfigurationReferenceAsync(dataSource, invoiceBOpenItemId));
        var postActivationApplication = await ApplyWithPreviewAsync(service, membershipId, entityId, partyA, SubledgerDirectionContract.Receivable,
            invoiceAOpenItemId, await ReadOpeningItemIdAsync(dataSource, batch, "ar-a-advance"), "5", openingDate.AddDays(2), actorId);
        await service.ReverseAsync(Tenant, membershipId, new(entityId, postActivationApplication, $"sl25-reverse-after-config-b-{Guid.CreateVersion7():N}", openingDate.AddDays(3),
            "SL25 historical A application reversal after B activation", actorId, Guid.CreateVersion7()), default);
        var afterPostActivationReversal = await service.ReconcileControlBalancesAsync(Tenant, membershipId, new(entityId, openingDate.AddDays(3)), actorId, Guid.CreateVersion7(), default);
        Assert.True(afterPostActivationReversal.IsBalanced, System.Text.Json.JsonSerializer.Serialize(afterPostActivationReversal.Details));
        Assert.All(afterPostActivationReversal.Details, detail => Assert.Equal(0m, decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture)));

        var writeOffItemId = await ReadOpeningItemIdAsync(dataSource, batch, "ar-b-invoice");
        var writeOffPolicyVersion = await ReadWriteOffPolicyVersionAsync(dataSource, entityId, "EGP");
        await repository.SaveWriteOffPolicyAsync(Tenant,
            new(entityId, SubledgerDirectionContract.Receivable, "EGP", "5", "1", writeOffPolicyVersion), 5m, 1m, "EGP", default);
        var beforeWriteOff = await service.ReconcileControlBalancesAsync(Tenant, membershipId,
            new(entityId, openingDate.AddDays(2)), actorId, Guid.CreateVersion7(), default);
        Assert.True(beforeWriteOff.IsBalanced, System.Text.Json.JsonSerializer.Serialize(beforeWriteOff.Details));
        var controlBeforeWriteOff = await ledger.GetControlBalanceAsync(Tenant, entityId, "Receivable", configurationId, openingDate.AddDays(2));
        var writeOffCreator = Guid.CreateVersion7();
        var writeOffRequest = new CreateWriteOffCommand(entityId, writeOffItemId, "1", "ROUNDING", "SL25 exact control reconciliation",
            openingDate.AddDays(2), $"sl25-writeoff-{Guid.CreateVersion7():N}", writeOffCreator, Guid.CreateVersion7());
        var writeOffRequestId = await repository.CreateWriteOffRequestAsync(Tenant, writeOffRequest, 1m, "EGP", 2, "HalfUp", default);
        var writeOffApproval = await repository.ApproveWriteOffAsync(Tenant,
            new(entityId, writeOffRequestId, Guid.CreateVersion7(), Guid.CreateVersion7()), default);
        Assert.Equal(49m, await ReadOpenAmountAsync(dataSource, entityId, writeOffItemId));
        Assert.Equal(49m, await ReadOpenBaseAmountAsync(dataSource, entityId, writeOffItemId));
        Assert.Equal(1m, await ReadWriteOffLedgerBaseAmountAsync(dataSource, entityId, writeOffApproval.LedgerFactId));
        var controlAfterWriteOff = await ledger.GetControlBalanceAsync(Tenant, entityId, "Receivable", configurationId, openingDate.AddDays(2));
        Assert.Equal(controlBeforeWriteOff.SignedBaseBalance - 1m, controlAfterWriteOff.SignedBaseBalance);
        var afterWriteOff = await service.ReconcileControlBalancesAsync(Tenant, membershipId,
            new(entityId, openingDate.AddDays(2)), actorId, Guid.CreateVersion7(), default);
        Assert.True(afterWriteOff.IsBalanced, System.Text.Json.JsonSerializer.Serialize(afterWriteOff.Details));
        Assert.All(afterWriteOff.Details, detail => Assert.Equal(0m, decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture)));
        foreach (var detail in afterWriteOff.Details)
            output.WriteLine($"SL25 live {detail.Direction}: Subledger={detail.SubledgerBaseBalance}; Ledger={detail.LedgerBaseBalance}; Difference={detail.Variance}");
    }

    [Fact]
    public async Task ScheduledSourceCreatesImmutableIndependentlySettleableInstallments()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var partyId = await FindActivePartyAsync(dataSource);
        var suffix = Guid.NewGuid().ToString("N");
        var paymentTermId = Guid.CreateVersion7();
        var service = new SubledgerService(repository, new ActivePartyDirectory(), new InstallmentFinanceBase(), new FixedEntityCurrency(),
            new NullAuthorizationSubjectStore(), new PermissionAuthorizer(), TimeProvider.System);
        var source = Snapshot($"SUBLEDGER-SCHEDULE-{suffix}", 100m) with { DueDate = new(2026, 10, 25) };
        var ids = await service.CreateScheduledReceivableOpenItemsAsync(Tenant,
            new(Entity, partyId, paymentTermId, source, Guid.CreateVersion7(), Guid.CreateVersion7()), default);
        Assert.Equal(3, ids.Count);
        var installments = await ReadInstallmentFactsByReferenceAsync(dataSource, source.SourceReference);
        Assert.Equal(["0001", "0002", "0003"], installments.Select(item => item.ScheduleReference));
        Assert.Equal([30m, 40m, 30m], installments.Select(item => item.Amount));
        Assert.Equal([new DateOnly(2026, 10, 25), new DateOnly(2026, 11, 24), new DateOnly(2026, 12, 24)], installments.Select(item => item.DueDate));
        Assert.Equal([30m, 40m, 30m], installments.Select(item => item.BaseAmount));
        Assert.All(installments, item => Assert.True(item.TermSnapshotPresent));
        var installmentPayment = await CreateAsync(repository, partyId, "Payment", "Credit", $"SUBLEDGER-SCHEDULE-PAYMENT-{suffix}", 30m);
        await repository.ApplyAsync(Tenant, new(Entity, partyId, SubledgerDirectionContract.Receivable, $"schedule-apply-{suffix}", new(2026, 10, 25), new(2026, 10, 25), [new(installments[0].Id, installmentPayment, "30")], Guid.CreateVersion7(), Guid.CreateVersion7()), [30m], 2, FinancialRoundingModeValue.HalfUp, default);
        Assert.Equal(0m, await ReadOpenAmountAsync(dataSource, installments[0].Id));
        Assert.Equal(40m, await ReadOpenAmountAsync(dataSource, installments[1].Id));
        Assert.Equal(30m, await ReadOpenAmountAsync(dataSource, installments[2].Id));
        var replay = await service.CreateScheduledReceivableOpenItemsAsync(Tenant,
            new(Entity, partyId, paymentTermId, source, Guid.CreateVersion7(), Guid.CreateVersion7()), default);
        Assert.Equal(ids.Order().ToArray(), replay.Order().ToArray());
    }

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
        Assert.Equal(1L, await CountOutboxByCausationAsync(Tenant.Value, "ApplicationConfirmedV1", applied.BatchId));
        Assert.Equal(1L, await CountAuditByActionAsync(Tenant.Value, "open_item_application_confirmed", applied.BatchId));
        await Assert.ThrowsAsync<Modules.Subledger.Entities.SubledgerException>(() => repository.ApplyAsync(Tenant,
            apply with { Lines = [new(debitId, creditId, "41")] }, [41m], 2, Shared.Primitives.Financial.FinancialRoundingModeValue.HalfUp, default));

        var reverse = new ReverseApplicationCommand(Entity, applied.ApplicationIds.Single(), $"subledger-reverse-{suffix}", new(2026, 10, 25), "Integration reversal", Guid.CreateVersion7(), Guid.CreateVersion7());
        var reversalAttempts = await Task.WhenAll(repository.ReverseAsync(Tenant, reverse, default), repository.ReverseAsync(Tenant, reverse, default));
        Assert.Single(reversalAttempts, result => !result.AlreadyApplied);
        Assert.Single(reversalAttempts, result => result.AlreadyApplied);
        Assert.Equal(reversalAttempts[0].ApplicationIds, reversalAttempts[1].ApplicationIds);
        Assert.Equal(1L, await CountReversalsForApplicationAsync(dataSource, applied.ApplicationIds.Single()));
        Assert.Equal(1L, await CountOutboxByCausationAsync(Tenant.Value, "ApplicationReversedV1", reversalAttempts.Single(result => !result.AlreadyApplied).BatchId));
        Assert.Equal(1L, await CountAuditByActionAsync(Tenant.Value, "open_item_application_reversed", reversalAttempts.Single(result => !result.AlreadyApplied).BatchId));
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
    [Trait("Category", "Performance")]
    public async Task HotPartyConcurrentApplicationLoadReportsLatencyConflictsAndIntegrity()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var hotPartyId = Guid.Parse("81e634ac-715c-6ada-adb8-ffce63272361");
        var dataset = await ReadScaleCountsAsync(dataSource, hotPartyId);
        var suffix = Guid.NewGuid().ToString("N");
        var sources = await repository.CreateOpenItemsAsync(Tenant, Entity, hotPartyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment",
            Enumerable.Range(0, 16).Select(index => Snapshot($"SUBLEDGER-PERF-SOURCE-{suffix}-{index}", 10m)).ToArray(), default);
        var targets = await repository.CreateOpenItemsAsync(Tenant, Entity, hotPartyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice",
            Enumerable.Range(0, 128).Select(index => Snapshot($"SUBLEDGER-PERF-TARGET-{suffix}-{index}", 1m)).ToArray(), default);
        var membershipId = Guid.Parse("10000000-0000-7000-8000-000000000001");
        var service = new SubledgerService(repository, new ActivePartyDirectory(), new InstallmentFinanceBase(), new FixedEntityCurrency(),
            new AllPermissionSubjectStore(Tenant, membershipId), new PermissionAuthorizer(), TimeProvider.System);

        var results = new LoadResult[128];
        var totalStarted = Stopwatch.GetTimestamp();
        await Parallel.ForEachAsync(Enumerable.Range(0, 128), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (index, _) =>
        {
            var group = index / 8;
            var started = Stopwatch.GetTimestamp();
            try
            {
                await service.ApplyAsync(Tenant, membershipId,
                    new(Entity, hotPartyId, SubledgerDirectionContract.Receivable, $"perf-{suffix}-{index}", new(2026, 10, 25), new(2026, 10, 25),
                        [new(targets[index], sources[group], "1", ExpectedDebitVersion: 1, ExpectedCreditVersion: 1)], Guid.CreateVersion7(), Guid.CreateVersion7()), _);
                results[index] = new LoadResult(Stopwatch.GetElapsedTime(started).TotalMilliseconds, null, false);
            }
            catch (Modules.Subledger.Entities.SubledgerException exception)
            {
                results[index] = new LoadResult(Stopwatch.GetElapsedTime(started).TotalMilliseconds, exception.Code, false);
            }
            catch (PostgresException exception)
            {
                results[index] = new LoadResult(Stopwatch.GetElapsedTime(started).TotalMilliseconds, exception.SqlState, exception.SqlState == "40P01");
            }
        });
        var elapsed = Stopwatch.GetElapsedTime(totalStarted).TotalSeconds;
        var latency = results.Select(result => result.Milliseconds).Order().ToArray();
        var successes = results.Count(result => result.ErrorCode is null);
        var staleConflicts = results.Count(result => result.ErrorCode == "subledger.item_changed");
        var deadlocks = results.Count(result => result.Deadlock);
        var unexpected = results.Where(result => result.ErrorCode is not null and not "subledger.item_changed").ToArray();
        var negativeRemaining = await CountNegativeOpenItemsAsync(dataSource, hotPartyId);
        var unexpectedSummary = string.Join(",", unexpected.GroupBy(result => result.ErrorCode).Select(group => $"{group.Key}:{group.Count()}"));

        output.WriteLine($"SUBLEDGER_CONCURRENCY datasetItems={dataset.Items} datasetApplications={dataset.Applications} hotPartyOpenItems={dataset.HotPartyOpenItems} operations=128 workers=8");
        output.WriteLine($"throughput={128d / elapsed:F2}/s p50={Percentile(latency, 0.50):F2}ms p95={Percentile(latency, 0.95):F2}ms p99={Percentile(latency, 0.99):F2}ms successes={successes} staleConflicts={staleConflicts} deadlocks={deadlocks} retries=0 negativeRemaining={negativeRemaining}");
        output.WriteLine($"otherErrors={unexpectedSummary}");
        Assert.Equal(16, successes);
        Assert.Equal(112, staleConflicts);
        Assert.Equal(0, deadlocks);
        Assert.Empty(unexpected);
        Assert.Equal(0, negativeRemaining);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task InteractiveApplicationBatchShapesReportLatencyAndThroughput()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var partyId = Guid.CreateVersion7();
        var membershipId = Guid.Parse("10000000-0000-7000-8000-000000000001");
        var service = new SubledgerService(repository, new ActivePartyDirectory(), new InstallmentFinanceBase(), new FixedEntityCurrency(),
            new AllPermissionSubjectStore(Tenant, membershipId), new PermissionAuthorizer(), TimeProvider.System);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        OpenItemSnapshotContract Item(string name, decimal amount, int dueOffset = 30) =>
            Snapshot($"SUBLEDGER-APP-PERF-{name}-{Guid.NewGuid():N}", amount) with
            {
                DocumentDate = today, PostingDate = today, DueDate = today.AddDays(dueOffset)
            };

        var singleDebits = await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", Enumerable.Range(0, 40).Select(i => Item($"1-1-D-{i}", 10m)).ToArray(), default);
        var singleCredits = await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", Enumerable.Range(0, 40).Select(i => Item($"1-1-C-{i}", 10m, 0)).ToArray(), default);
        var singleCommands = Enumerable.Range(0, 40).Select(i => new ApplyOpenItemsCommand(Entity, partyId, SubledgerDirectionContract.Receivable,
            $"small-1-1-{Guid.NewGuid():N}", today, today, [new(singleDebits[i], singleCredits[i], "10", ExpectedDebitVersion: 1, ExpectedCreditVersion: 1)],
            Guid.CreateVersion7(), Guid.CreateVersion7())).ToArray();

        var splitCredits = await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", Enumerable.Range(0, 24).Select(i => Item($"1-3-C-{i}", 30m, 0)).ToArray(), default);
        var splitDebits = await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", Enumerable.Range(0, 72).Select(i => Item($"1-3-D-{i}", 10m)).ToArray(), default);
        var splitCommands = Enumerable.Range(0, 24).Select(i => new ApplyOpenItemsCommand(Entity, partyId, SubledgerDirectionContract.Receivable,
            $"small-1-3-{Guid.NewGuid():N}", today, today,
            Enumerable.Range(0, 3).Select(target => new ApplicationLineCommand(splitDebits[i * 3 + target], splitCredits[i], "10", ExpectedDebitVersion: 1, ExpectedCreditVersion: 1)).ToArray(),
            Guid.CreateVersion7(), Guid.CreateVersion7())).ToArray();

        var manyCredits = await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", Enumerable.Range(0, 48).Select(i => Item($"many-C-{i}", 10m, 0)).ToArray(), default);
        var manyDebits = await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
            Modules.Subledger.Entities.OpenItemPolarity.Debit, "Invoice", Enumerable.Range(0, 48).Select(i => Item($"many-D-{i}", 10m)).ToArray(), default);
        var manyCommands = Enumerable.Range(0, 24).Select(i => new ApplyOpenItemsCommand(Entity, partyId, SubledgerDirectionContract.Receivable,
            $"small-many-many-{Guid.NewGuid():N}", today, today,
            [
                new(manyDebits[i * 2], manyCredits[i * 2], "5", ExpectedDebitVersion: 1, ExpectedCreditVersion: 1),
                new(manyDebits[i * 2], manyCredits[i * 2 + 1], "5", ExpectedDebitVersion: 1, ExpectedCreditVersion: 1),
                new(manyDebits[i * 2 + 1], manyCredits[i * 2], "5", ExpectedDebitVersion: 1, ExpectedCreditVersion: 1),
                new(manyDebits[i * 2 + 1], manyCredits[i * 2 + 1], "5", ExpectedDebitVersion: 1, ExpectedCreditVersion: 1)
            ], Guid.CreateVersion7(), Guid.CreateVersion7())).ToArray();

        async Task Measure(string scenario, ApplyOpenItemsCommand[] commands)
        {
            var warmupStarted = Stopwatch.GetTimestamp();
            await Parallel.ForEachAsync(Enumerable.Range(0, 8), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (index, token) =>
                await service.ApplyAsync(Tenant, membershipId, commands[index], token));
            var warmupElapsed = Stopwatch.GetElapsedTime(warmupStarted).TotalMilliseconds;
            var measuredCommands = commands.Skip(8).ToArray();
            var durations = new double[measuredCommands.Length];
            var errors = new string?[measuredCommands.Length];
            var started = Stopwatch.GetTimestamp();
            await Parallel.ForEachAsync(Enumerable.Range(0, measuredCommands.Length), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (index, token) =>
            {
                var operationStarted = Stopwatch.GetTimestamp();
                try { await service.ApplyAsync(Tenant, membershipId, measuredCommands[index], token); }
                catch (Exception exception) { errors[index] = exception is Modules.Subledger.Entities.SubledgerException subledger ? subledger.Code : exception.GetType().Name; }
                durations[index] = Stopwatch.GetElapsedTime(operationStarted).TotalMilliseconds;
            });
            var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
            var ordered = durations.Order().ToArray();
            var errorSummary = string.Join(',', errors.Where(error => error is not null));
            output.WriteLine($"SUBLEDGER_APPLICATION scenario={scenario} concurrency=8 warmupOperations=8 warmupElapsed={warmupElapsed:F2}ms operations={measuredCommands.Length} p50={Percentile(ordered, 0.50):F2}ms p95={Percentile(ordered, 0.95):F2}ms p99={Percentile(ordered, 0.99):F2} throughput={measuredCommands.Length / elapsed:F2}/s conflicts={errors.Count(error => error == "subledger.item_changed")} errors={errors.Count(error => error is not null)} other={errorSummary}");
            Assert.All(errors, error => Assert.Null(error));
        }

        await Measure("1-source-1-target", singleCommands);
        await Measure("1-source-3-targets", splitCommands);
        await Measure("many-to-many-2x2-4-lines", manyCommands);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task AutoApplySuggestionLatencyStaysBoundedForLargeParties()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var hotPartyId = Guid.Parse("81e634ac-715c-6ada-adb8-ffce63272361");
        var membershipId = Guid.Parse("10000000-0000-7000-8000-000000000001");
        var service = new SubledgerService(repository, new ActivePartyDirectory(), new InstallmentFinanceBase(), new FixedEntityCurrency(),
            new AllPermissionSubjectStore(Tenant, membershipId), new PermissionAuthorizer(), TimeProvider.System);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var suffix = Guid.NewGuid().ToString("N");
        async Task<Guid> CreateSource(string label, decimal amount)
        {
            var source = Snapshot($"SUBLEDGER-AUTO-PERF-{label}-{suffix}", amount) with
            {
                DocumentDate = today,
                PostingDate = today,
                DueDate = today,
                ControlAccountReference = "role:Receivable",
                ControlConfigurationReference = "configuration:performance-v1"
            };
            return (await repository.CreateOpenItemsAsync(Tenant, Entity, hotPartyId, Modules.Subledger.Entities.SubledgerDirection.Receivable,
                Modules.Subledger.Entities.OpenItemPolarity.Credit, "Payment", [source], default)).Single();
        }

        var exactSourceId = await CreateSource("exact", 100m);
        var oldestSourceId = await CreateSource("oldest", 5_000m);
        var largePartySourceId = await CreateSource("large", 5_000m);
        var dataset = await ReadScaleCountsAsync(dataSource, hotPartyId);

        async Task Measure(string scenario, Guid sourceId, string policy, string? reference, int expectedSuggestions)
        {
            var query = new AutoApplySuggestionQuery(Entity, sourceId, reference, policy, 50);
            var warmupDurations = new double[8];
            var warmupCounts = new int[8];
            await Parallel.ForEachAsync(Enumerable.Range(0, warmupDurations.Length), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (index, token) =>
            {
                var started = Stopwatch.GetTimestamp();
                var result = await service.SuggestApplicationsAsync(Tenant, membershipId, query, token);
                warmupDurations[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                warmupCounts[index] = result.Suggestions.Count;
            });
            Assert.All(warmupCounts, count => Assert.Equal(expectedSuggestions, count));
            var durations = new double[32];
            var suggestions = new int[32];
            await Parallel.ForEachAsync(Enumerable.Range(0, durations.Length), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (index, token) =>
            {
                var started = Stopwatch.GetTimestamp();
                var result = await service.SuggestApplicationsAsync(Tenant, membershipId, query, token);
                durations[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                suggestions[index] = result.Suggestions.Count;
            });
            var ordered = durations.Order().ToArray();
            output.WriteLine($"SUBLEDGER_AUTO_APPLY scenario={scenario} partyItems={dataset.Items} partyOpenItems={dataset.HotPartyOpenItems} warmupRequests=8 warmupP95={Percentile(warmupDurations.Order().ToArray(), 0.95):F2}ms requests={durations.Length} concurrency=8 candidateLimit=50 suggestionsReturned={suggestions[0]} maxSuggestions=50 p50={Percentile(ordered, 0.50):F2}ms p95={Percentile(ordered, 0.95):F2}ms p99={Percentile(ordered, 0.99):F2} errors=0");
            Assert.All(suggestions, count => Assert.Equal(expectedSuggestions, count));
        }

        await Measure("exact-reference", exactSourceId, "ReferenceMatchFirst", "HOT-1", 1);
        await Measure("oldest-due-fallback", oldestSourceId, "AutoOldestDue", null, 50);
        await Measure("large-party-mismatch-fallback", largePartySourceId, "ReferenceMatchFirst", $"NO-MATCH-{suffix}", 50);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task AgingLatencySeparatesCurrentHistoricalAndNormalPartyWork()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SUBLEDGER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_LEDGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_SUBLEDGER_TEST_CONNECTION to a migrated development database and runtime-equivalent erp_app connection.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var hotPartyId = Guid.Parse("81e634ac-715c-6ada-adb8-ffce63272361");
        var normalPartyId = Guid.Parse("05e91cb0-a5af-fd5e-929a-5dd9ba9fd5d6");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dataset = await ReadScaleCountsAsync(dataSource, hotPartyId);

        async Task Measure(string scenario, Guid partyId, DateOnly asOfDate, int expectedRows)
        {
            var query = new AgingQuery(Entity, partyId, SubledgerDirectionContract.Receivable, asOfDate, null);
            var warmupTimes = new double[8];
            var warmupRows = new int[8];
            await Parallel.ForEachAsync(Enumerable.Range(0, warmupTimes.Length), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (index, token) =>
            {
                var started = Stopwatch.GetTimestamp();
                var result = await repository.GetAgingAsync(Tenant, query, token);
                warmupTimes[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                warmupRows[index] = result.Count;
            });
            Assert.All(warmupRows, rows => Assert.Equal(expectedRows, rows));

            var durations = new double[32];
            var rowCounts = new int[durations.Length];
            await Parallel.ForEachAsync(Enumerable.Range(0, durations.Length), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (index, token) =>
            {
                var started = Stopwatch.GetTimestamp();
                var result = await repository.GetAgingAsync(Tenant, query, token);
                durations[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                rowCounts[index] = result.Count;
            });
            var ordered = durations.Order().ToArray();
            var partySize = partyId == hotPartyId ? dataset.HotPartyOpenItems.ToString(System.Globalization.CultureInfo.InvariantCulture) : "seeded-normal";
            output.WriteLine($"SUBLEDGER_AGING scenario={scenario} classification={(asOfDate == today ? "interactive-current-projection" : "historical-report")} partyItems={partySize} warmupRequests=8 warmupP95={Percentile(warmupTimes.Order().ToArray(), 0.95):F2}ms requests={durations.Length} concurrency=8 resultRows={rowCounts[0]} p50={Percentile(ordered, 0.50):F2}ms p95={Percentile(ordered, 0.95):F2}ms p99={Percentile(ordered, 0.99):F2} errors=0");
            Assert.All(rowCounts, rows => Assert.Equal(expectedRows, rows));
        }

        await Measure("current-hot-party", hotPartyId, today, 3);
        await Measure("historical-hot-party", hotPartyId, today.AddDays(-1), 3);
        await Measure("current-normal-party", normalPartyId, today, 2);
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
            var tenantTables = new List<string>();
            await using var catalog = new NpgsqlCommand("select c.relname,c.relrowsecurity,c.relforcerowsecurity,pg_get_userbyid(c.relowner)<>'erp_app' from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='finance_subledger' and c.relkind='r' and exists(select 1 from pg_attribute a where a.attrelid=c.oid and a.attname='tenant_id' and not a.attisdropped) order by c.relname", connection, transaction);
            await using (var reader = await catalog.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tenantTables.Add(reader.GetString(0));
                    Assert.True(reader.GetBoolean(1), $"RLS is disabled for finance_subledger.{reader.GetString(0)}");
                    Assert.True(reader.GetBoolean(2), $"FORCE RLS is disabled for finance_subledger.{reader.GetString(0)}");
                    Assert.True(reader.GetBoolean(3), $"erp_app owns finance_subledger.{reader.GetString(0)}");
                }
            }
            Assert.True(tenantTables.Count >= 13);
            foreach (var table in tenantTables)
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

    private static double Percentile(double[] sortedValues, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(percentile * sortedValues.Length) - 1, 0, sortedValues.Length - 1);
        return sortedValues[index];
    }

    private static async Task<long> CountNegativeOpenItemsAsync(NpgsqlDataSource dataSource, Guid partyId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            tenantContext.Parameters.AddWithValue(Tenant.Value.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and party_id=$3 and (open_transaction_amount<0 or open_base_carrying_amount<0)", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        command.Parameters.AddWithValue(partyId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<ScaleCounts> ReadScaleCountsAsync(NpgsqlDataSource dataSource, Guid partyId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            tenantContext.Parameters.AddWithValue(Tenant.Value.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select (select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2),(select count(*) from finance_subledger.open_item_application where tenant_id=$1 and legal_entity_id=$2),(select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and party_id=$3 and is_open)", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value);
        command.Parameters.AddWithValue(Entity.Value);
        command.Parameters.AddWithValue(partyId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    private sealed record LoadResult(double Milliseconds, string? ErrorCode, bool Deadlock);
    private sealed record ScaleCounts(long Items, long Applications, long HotPartyOpenItems);

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

    private static async Task<long> CountWriteOffRecordsAsync(NpgsqlDataSource dataSource, Guid requestId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*) from finance_subledger.write_off where tenant_id=$1 and legal_entity_id=$2 and request_id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(Entity.Value); command.Parameters.AddWithValue(requestId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountReconciliationRunsAsync(NpgsqlDataSource dataSource, Guid runId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*) from finance_subledger.control_reconciliation_run where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(Entity.Value); command.Parameters.AddWithValue(runId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddSignedLine(List<OpeningBalanceLineRequest> lines, Guid accountId, decimal signedDebitBalance, string currency)
    {
        if (signedDebitBalance == 0m) return;
        lines.Add(signedDebitBalance > 0m
            ? new(accountId, signedDebitBalance, 0m, currency, signedDebitBalance, 0m)
            : new(accountId, 0m, -signedDebitBalance, currency, 0m, -signedDebitBalance));
    }

    private static async Task<Guid> ReadCurrentLedgerConfigurationAsync(NpgsqlDataSource dataSource)
    {
        return await ReadCurrentLedgerConfigurationAsync(dataSource, Entity);
    }

    private static async Task<Guid> ReadCurrentLedgerConfigurationAsync(NpgsqlDataSource dataSource, LegalEntityId entityId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select active_configuration_version_id from finance_ledger.ledger_entity_configuration where tenant_id=$1 and legal_entity_id=$2", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(entityId.Value);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The Ledger accounting configuration is unavailable."));
    }

    private static async Task<Guid> FindSecondaryLegalEntityAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select id from platform.legal_entity where tenant_id=$1 and id<>$2 and status='Active' order by id limit 1", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(Entity.Value);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The PostgreSQL fixture has no second active legal entity."));
    }

    private static async Task<Guid> ReadOpenItemIdBySourceAsync(NpgsqlDataSource dataSource, LegalEntityId entityId, string sourceReference)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select id from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_reference=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(entityId.Value); command.Parameters.AddWithValue(sourceReference);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException($"The Subledger open item {sourceReference} was not found."));
    }

    private static async Task<Guid> ReadOpeningItemIdAsync(NpgsqlDataSource dataSource, Guid batchId, string rowKey)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select created_open_item_id from finance_subledger.opening_batch_item where tenant_id=$1 and batch_id=$2 and row_key=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(batchId); command.Parameters.AddWithValue(rowKey);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException($"The opening row {rowKey} was not finalized."));
    }

    private static async Task<string> ReadOpenItemConfigurationReferenceAsync(NpgsqlDataSource dataSource, Guid itemId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select control_configuration_reference from finance_subledger.party_open_item where tenant_id=$1 and id=$2", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(itemId);
        return (string)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException($"Open item {itemId} was not found."));
    }

    private static async Task<long> CountJournalLinesByAccountAsync(NpgsqlDataSource dataSource, LegalEntityId entityId, Guid accountId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*) from finance_ledger.journal_entry_line where tenant_id=$1 and legal_entity_id=$2 and account_id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(entityId.Value); command.Parameters.AddWithValue(accountId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<Guid> ApplyWithPreviewAsync(SubledgerService service, Guid membershipId, LegalEntityId entityId, Guid partyId,
        SubledgerDirectionContract direction, Guid debitItemId, Guid creditItemId, string amount, DateOnly effectiveDate, Guid actorId)
    {
        var command = new ApplyOpenItemsCommand(entityId, partyId, direction, $"sl25-application-{Guid.CreateVersion7():N}", effectiveDate, effectiveDate,
            [new(debitItemId, creditItemId, amount)], actorId, Guid.CreateVersion7());
        var preview = await service.PreviewAsync(Tenant, membershipId, command, default);
        var line = Assert.Single(preview.Lines);
        command = command with
        {
            Lines = [new(debitItemId, creditItemId, line.DebitAppliedAmount, line.CreditAppliedAmount, line.ExpectedDebitVersion, line.ExpectedCreditVersion)]
        };
        var result = await service.ApplyAsync(Tenant, membershipId, command, default);
        return Assert.Single(result.ApplicationIds);
    }

    private static async Task<decimal> SumOpeningSubledgerBalancesAsync(NpgsqlDataSource dataSource, Guid batchId, Guid partyId, SubledgerDirectionContract direction)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select coalesce(sum(case when i.polarity=case when i.direction='Receivable' then 'Debit' else 'Credit' end then i.open_base_carrying_amount else -i.open_base_carrying_amount end),0) from finance_subledger.opening_batch_item b join finance_subledger.party_open_item i on i.tenant_id=b.tenant_id and i.legal_entity_id=b.legal_entity_id and i.id=b.created_open_item_id where b.tenant_id=$1 and b.legal_entity_id=$2 and b.batch_id=$3 and i.party_id=$4 and i.direction=$5", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(Entity.Value); command.Parameters.AddWithValue(batchId); command.Parameters.AddWithValue(partyId); command.Parameters.AddWithValue(direction.ToString());
        return (decimal)(await command.ExecuteScalarAsync() ?? 0m);
    }

    private static async Task<long> CountOpeningItemsBySourceAsync(NpgsqlDataSource dataSource, string sourceReference)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type='OpeningBalance' and source_reference=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(Entity.Value); command.Parameters.AddWithValue(sourceReference);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountOpeningBatchLinksAsync(NpgsqlDataSource dataSource, Guid batchId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*) from finance_subledger.opening_batch_item where tenant_id=$1 and legal_entity_id=$2 and batch_id=$3 and created_open_item_id is not null", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(Entity.Value); command.Parameters.AddWithValue(batchId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountWriteOffAuditAsync(NpgsqlDataSource dataSource, Guid requestId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*) from finance_subledger.write_off_request_audit where tenant_id=$1 and legal_entity_id=$2 and request_id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(Entity.Value); command.Parameters.AddWithValue(requestId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<long> ReadWriteOffPolicyVersionAsync(NpgsqlDataSource dataSource, string currency)
        => await ReadWriteOffPolicyVersionAsync(dataSource, Entity, currency);

    private static async Task<long> ReadWriteOffPolicyVersionAsync(NpgsqlDataSource dataSource, LegalEntityId entityId, string currency)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select coalesce((select version from finance_subledger.write_off_policy where tenant_id=$1 and legal_entity_id=$2 and direction='Receivable' and currency_code=$3),0)", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(entityId.Value); command.Parameters.AddWithValue(currency);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string> ReadWriteOffRequestStatusAsync(NpgsqlDataSource dataSource, Guid requestId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select status from finance_subledger.write_off_request where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(Entity.Value); command.Parameters.AddWithValue(requestId);
        return (string?)await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Write-off request was not found.");
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

    private static async Task<long> CountOutboxByCausationAsync(Guid tenantId, string messageType, Guid causationId)
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION to verify transactional Subledger outbox records.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and message_type=$2 and causation_id=$3", connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(messageType);
        command.Parameters.AddWithValue(causationId);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<long> CountAuditByActionAsync(Guid tenantId, string action, Guid entityId)
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION to verify transactional Subledger audit records.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select count(*) from platform.audit_entry where tenant_id=$1 and action=$2 and entity_id=$3", connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(action);
        command.Parameters.AddWithValue(entityId);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
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

    private sealed class ActivePartyDirectory : IDirectoryCapabilities
    {
        public Task<bool> IsActivePartyAsync(TenantId tenantId, Guid partyId, Guid? legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlySet<Guid>> FindActivePartiesAsync(TenantId tenantId, IReadOnlyCollection<Guid> partyIds, Guid? legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<Guid>>(partyIds.ToHashSet());
        public Task<InvoicePartySnapshotContract?> ReadInvoiceSnapshotAsync(TenantId tenantId, Guid partyId, Guid legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<InvoicePartySnapshotContract?>(null);
    }

    private sealed class AllPermissionSubjectStore(TenantId tenant, Guid membership, LegalEntityId? entityScope = null) : IAuthorizationSubjectStore
    {
        public Task<AuthorizationSubject?> LoadAsync(TenantId tenantId, Guid membershipId, CancellationToken cancellationToken = default)
        {
            if (tenantId != tenant || membershipId != membership) return Task.FromResult<AuthorizationSubject?>(null);
            var roleId = Guid.CreateVersion7();
            var role = new Role(roleId, tenant, "test", "Test", false, true, 1);
            var assignment = new MembershipRoleAssignment(tenant, membership, roleId, PermissionScope.ForLegalEntity(tenant, entityScope ?? Entity));
            var codes = new[] { "subledger.reconciliation.run", "subledger.reconciliation.read", "subledger.writeoff.policy.manage", "subledger.writeoff.create", "subledger.writeoff.approve", "subledger.opening.manage", "subledger.opening.finalize", "subledger.application.create", "subledger.application.cross_currency", "subledger.application.reverse", "subledger.advance.create", "subledger.credit-note.create" };
            var permissions = codes.Select(code => new RolePermissionEntry(tenant, roleId, code)).ToArray();
            return Task.FromResult<AuthorizationSubject?>(new(tenant, membership, [assignment], [role], permissions, 1));
        }
    }

    private sealed class FixedEntityCurrency : ILegalEntityFinancialReadStore
    {
        public Task<string?> GetCodeAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("TEST");
        public Task<string?> GetBaseCurrencyCodeAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("EGP");
        public Task<string?> GetTimeZoneAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("Africa/Cairo");
    }

    private sealed class InstallmentFinanceBase : IFinanceBaseCapabilities
    {
        private static readonly FinancialRoundingPolicySnapshotContract Rounding = new(Guid.CreateVersion7(), 1, 2, "0.01", FinancialLineDocumentStrategy.RoundEachLineThenSum,
            Enum.GetValues<FinancialRoundingContext>().Select(context => new FinancialRoundingRuleSnapshotContract(context, FinancialRoundingMode.HalfEven)).ToArray());

        public Task<FinancialCalculationContextSnapshotContract?> ResolveCalculationContextAsync(TenantId tenantId, LegalEntityId legalEntityId, string transactionCurrencyCode, FinancialCalculationPurpose purpose, DateOnly businessDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<FinancialCalculationContextSnapshotContract?>(new(tenantId, legalEntityId, "EGP", transactionCurrencyCode, purpose, businessDate,
                new(legalEntityId.Value, Guid.CreateVersion7(), 7, false, 1), Rounding,
                transactionCurrencyCode == "USD" ? new(tenantId.Value, legalEntityId.Value, Guid.CreateVersion7(), "USD", "EGP", "50", purpose, businessDate, "IntegrationTest", 1, false, DateTimeOffset.UtcNow) : null,
                DateTimeOffset.UtcNow));

        public Task<PaymentTermSnapshotContract?> ResolvePaymentTermAsync(TenantId tenantId, Guid paymentTermId, DateOnly businessDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<PaymentTermSnapshotContract?>(new(paymentTermId, Guid.CreateVersion7(), "TEST-TERM", 1, businessDate, null, []));

        public Task<PaymentTermScheduleSnapshotContract?> CalculatePaymentScheduleAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid paymentTermId, string transactionCurrencyCode, string totalAmount, DateOnly basisDate, CancellationToken cancellationToken = default)
        {
            var term = new PaymentTermSnapshotContract(paymentTermId, Guid.Parse("50000000-0000-7000-8000-000000000001"), "TEST-30-40-30", 1, basisDate, null, []);
            var lines = new[]
            {
                new PaymentScheduleLineContract(1, basisDate, new("30", transactionCurrencyCode)),
                new PaymentScheduleLineContract(2, basisDate.AddDays(30), new("40", transactionCurrencyCode)),
                new PaymentScheduleLineContract(3, basisDate.AddDays(60), new("30", transactionCurrencyCode))
            };
            return Task.FromResult<PaymentTermScheduleSnapshotContract?>(new(term, lines, Rounding, DateTimeOffset.UtcNow));
        }

        public Task<CommercialPriceConversionContract> ConvertCommercialUnitPriceAsync(TenantId tenantId, LegalEntityId legalEntityId, string amount, string fromCurrencyCode, string toCurrencyCode, FinancialCalculationPurpose purpose, DateOnly businessDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommercialPriceConversionContract(amount, fromCurrencyCode, amount, toCurrencyCode, "1", null));
    }

    private sealed record ItemState(decimal OpenAmount, decimal SettledAmount, string Status, bool IsOpen);
    private sealed record ScheduledFact(Guid Id, string ScheduleReference, DateOnly DueDate, decimal Amount, decimal BaseAmount, bool TermSnapshotPresent);
    private sealed record InstallmentFacts(long Count, long DistinctScheduleCount, decimal TotalAmount);
    private sealed record ApplyAttempt(bool Applied);
    private sealed record ApplicationHistory(DateOnly ApplicationDate, DateTimeOffset ApplicationCreatedAt, DateOnly ReversalEffectiveDate, Guid OriginalApplicationId, Guid ReversalId);

    private sealed class FailingLedgerPostingCapability : ITransactionalLedgerPostingCapability
    {
        public Task<LedgerPostingResult> PostExplicitAsync(Core.Transactions.ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
            ExplicitLedgerPostingPayload payload, CancellationToken cancellationToken = default) => Task.FromException<LedgerPostingResult>(new InvalidOperationException("Injected Ledger failure."));

        public Task<LedgerPostingResult> PostSettlementFxDifferenceAsync(Core.Transactions.ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
            SettlementFxDifferencePayload payload, CancellationToken cancellationToken = default) => Task.FromException<LedgerPostingResult>(new InvalidOperationException("Injected Ledger failure."));

        public Task<LedgerPostingResult> PostSettlementWriteOffAsync(Core.Transactions.ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
            SettlementWriteOffPayload payload, CancellationToken cancellationToken = default) => Task.FromException<LedgerPostingResult>(new InvalidOperationException("Injected Ledger failure."));
    }

    private static async Task<Guid> CreateAsync(SubledgerRepository repository, Guid partyId, string sourceType, string polarity, string reference, decimal amount,
        Modules.Subledger.Entities.SubledgerDirection direction = Modules.Subledger.Entities.SubledgerDirection.Receivable, string? controlConfigurationReference = null)
    {
        var snapshot = Snapshot(reference, amount, null, direction == Modules.Subledger.Entities.SubledgerDirection.Receivable ? "Receivable" : "Payable");
        if (controlConfigurationReference is not null) snapshot = snapshot with { ControlConfigurationReference = controlConfigurationReference };
        return (await repository.CreateOpenItemsAsync(Tenant, Entity, partyId, direction,
            polarity == "Debit" ? Modules.Subledger.Entities.OpenItemPolarity.Debit : Modules.Subledger.Entities.OpenItemPolarity.Credit,
            sourceType, [snapshot], default)).Single();
    }

    private static OpenItemSnapshotContract Snapshot(string reference, decimal amount, string? schedule = null, string controlRole = "Receivable") => new(
        reference, schedule, reference, new(2026, 10, 25), new(2026, 10, 25), new(2026, 11, 24), "EGP", amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "EGP", amount.ToString(System.Globalization.CultureInfo.InvariantCulture), controlRole, $"{controlRole}-CONTROL", $"{controlRole}-CONFIG-V1", null, 1);

    private static async Task<decimal> ReadOpenAmountAsync(NpgsqlDataSource dataSource, Guid itemId)
        => await ReadOpenAmountAsync(dataSource, Entity, itemId);

    private static async Task<decimal> ReadOpenAmountAsync(NpgsqlDataSource dataSource, LegalEntityId entityId, Guid itemId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            tenant.Parameters.AddWithValue(Tenant.Value.ToString("D"));
            await tenant.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select open_transaction_amount from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(entityId.Value); command.Parameters.AddWithValue(itemId);
        return (decimal)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected open item was missing."));
    }

    private static async Task<decimal> ReadOpenBaseAmountAsync(NpgsqlDataSource dataSource, Guid itemId)
        => await ReadOpenBaseAmountAsync(dataSource, Entity, itemId);

    private static async Task<decimal> ReadOpenBaseAmountAsync(NpgsqlDataSource dataSource, LegalEntityId entityId, Guid itemId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select open_base_carrying_amount from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(entityId.Value); command.Parameters.AddWithValue(itemId);
        return (decimal)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected Subledger item was missing."));
    }

    private static async Task<decimal> ReadPostedFactTotalAsync(NpgsqlDataSource dataSource, string factType, string sourceReference, string accountRole)
        => await ReadPostedFactTotalAsync(dataSource, Entity, factType, sourceReference, accountRole);

    private static async Task<decimal> ReadPostedFactTotalAsync(NpgsqlDataSource dataSource, LegalEntityId entityId, string factType, string sourceReference, string accountRole)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select coalesce(sum(l.base_debit-l.base_credit),0) from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.fact_type=$3 and e.source_reference=$4 and l.account_role=$5", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(entityId.Value); command.Parameters.AddWithValue(factType); command.Parameters.AddWithValue(sourceReference); command.Parameters.AddWithValue(accountRole);
        return (decimal)(await command.ExecuteScalarAsync() ?? 0m);
    }

    private static async Task<decimal> ReadWriteOffLedgerBaseAmountAsync(NpgsqlDataSource dataSource, Guid factId)
        => await ReadWriteOffLedgerBaseAmountAsync(dataSource, Entity, factId);

    private static async Task<decimal> ReadWriteOffLedgerBaseAmountAsync(NpgsqlDataSource dataSource, LegalEntityId entityId, Guid factId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select coalesce(sum(l.base_debit+l.base_credit)/2,0) from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.fact_id=$3 and e.fact_type='SettlementWriteOff'", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(entityId.Value); command.Parameters.AddWithValue(factId);
        return (decimal)(await command.ExecuteScalarAsync() ?? 0m);
    }

    private static async Task<long> CountPostedFxFactsForApplicationAsync(NpgsqlDataSource dataSource, Guid applicationId)
        => await CountPostedFxFactsForApplicationAsync(dataSource, Entity, applicationId);

    private static async Task<long> CountPostedFxFactsForApplicationAsync(NpgsqlDataSource dataSource, LegalEntityId entityId, Guid applicationId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and fact_type='SettlementFxDifference' and (source_reference=$3 or source_reference=$4) and status='Posted'", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(entityId.Value);
        command.Parameters.AddWithValue($"application:{applicationId:N}"); command.Parameters.AddWithValue($"application-reversal:{applicationId:N}");
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
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

    private static async Task<IReadOnlyList<ScheduledFact>> ReadInstallmentFactsByReferenceAsync(NpgsqlDataSource dataSource, string sourceReference)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantForQueryAsync(connection, transaction);
        await using var command = new NpgsqlCommand("select id,source_schedule_reference,due_date,original_transaction_amount,original_base_amount,payment_term_snapshot is not null from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type='Invoice' and source_reference=$3 order by source_schedule_reference", connection, transaction);
        command.Parameters.AddWithValue(Tenant.Value); command.Parameters.AddWithValue(Entity.Value); command.Parameters.AddWithValue(sourceReference);
        var result = new List<ScheduledFact>(); await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetBoolean(5)));
        return result;
    }
}
