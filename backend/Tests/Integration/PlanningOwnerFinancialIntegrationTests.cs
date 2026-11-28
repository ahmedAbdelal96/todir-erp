using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.FixedAssets.Services;
using Modules.Payroll.Repositories;
using Modules.Payroll.Services;
using Npgsql;
using Shared.Contracts.Hr;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlanningOwnerFinancialIntegrationTests
{
    private static readonly Guid Tenant = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");
    private static readonly Guid LegalEntity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");

    [Fact(DisplayName = "FP52 all registered owner modules expose scoped planning facts")]
    public async Task AllRegisteredOwnerModulesExposeScopedPlanningFacts()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        var tenant = new TenantId(Tenant);
        var sources = new Shared.Contracts.Planning.IPlanningOwnerReconciliationSource[]
        {
            new Modules.Banking.Repositories.BankingPlanningOwnerSource(dataSource),
            new Modules.CRM.Repositories.CrmPlanningOwnerSource(dataSource),
            new Modules.FinanceBase.Repositories.FinanceBasePlanningOwnerSource(dataSource),
            new Modules.Hr.Repositories.HrPlanningOwnerSource(dataSource),
            new Modules.Inventory.Repositories.InventoryPlanningOwnerSource(dataSource),
            new Modules.Manufacturing.Repositories.ManufacturingPlanningOwnerSource(dataSource),
            new Modules.Projects.Repositories.ProjectsPlanningOwnerSource(dataSource),
            new Modules.Sales.Repositories.SalesPlanningOwnerSource(dataSource),
            new Modules.Subledger.Repositories.SubledgerPlanningOwnerSource(dataSource),
            new Modules.Ledger.Repositories.LedgerOwnerReconciliationSource(dataSource),
            new Modules.Payroll.Repositories.PayrollOwnerReconciliationSource(dataSource),
            new Modules.Expenses.Repositories.ExpensesOwnerReconciliationSource(dataSource),
            new Modules.FixedAssets.Repositories.FixedAssetsOwnerReconciliationSource(dataSource),
            new Modules.Purchasing.Repositories.PurchasingOwnerReconciliationSource(dataSource)
        };

        var moduleNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var facts = await source.ListFinancialFactsAsync(tenant, LegalEntity);
            var replay = await source.ListFinancialFactsAsync(tenant, LegalEntity);
            Assert.Equal(facts.Count, replay.Count);
            Assert.Equal(
                facts.Select(FactFingerprint).OrderBy(value => value, StringComparer.Ordinal),
                replay.Select(FactFingerprint).OrderBy(value => value, StringComparer.Ordinal));
            Assert.All(facts, fact =>
            {
                Assert.Equal(LegalEntity, fact.LegalEntityId);
                Assert.False(string.IsNullOrWhiteSpace(fact.SourceModule));
                Assert.False(string.IsNullOrWhiteSpace(fact.SourceVersion));
                Assert.False(string.IsNullOrWhiteSpace(fact.RecognitionIdentity));
                Assert.False(string.IsNullOrWhiteSpace(fact.MetricCode));
                Assert.False(string.IsNullOrWhiteSpace(fact.CurrencyCode));
                Assert.False(string.IsNullOrWhiteSpace(fact.SourceKind));
            });
            Assert.True(moduleNames.Add(source.SourceModule), $"Duplicate owner source registration: {source.SourceModule}");
        }

        Assert.Equal(sources.Length, moduleNames.Count);
    }

    private static string FactFingerprint(Shared.Contracts.Planning.PlanningOwnerFinancialFact fact) =>
        string.Join('|', fact.SourceModule, fact.SourceDocumentId, fact.SourceLineId, fact.SourceVersion,
            fact.RecognitionIdentity, fact.LegalEntityId, fact.PeriodStart, fact.MetricCode,
            fact.DimensionKeyJson, fact.SourceKind, fact.Amount, fact.CurrencyCode, fact.FiscalBasis);

    [Fact(DisplayName = "BC2-T15 Payroll and Fixed Assets owner facts reconcile with Ledger exactly once")]
    public async Task PayrollAndFixedAssetsOwnerFactsReconcileWithLedgerExactlyOnce()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();

        var actor = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", Tenant)
            ?? throw new InvalidOperationException("Atlas administrator is missing."));
        var payrollGroup = (Guid)(await ScalarAsync(admin, "select id from business_payroll.payroll_group where tenant_id=$1 and legal_entity_id=$2 and status='Active' order by id limit 1", Tenant, LegalEntity)
            ?? throw new InvalidOperationException("Atlas payroll group is missing."));
        var category = (Guid)(await ScalarAsync(admin, "select id from business_assets.asset_category where tenant_id=$1 and code='LAPTOP'", Tenant)
            ?? throw new InvalidOperationException("The seeded LAPTOP asset category is missing."));
        var bookDefinition = (Guid)(await ScalarAsync(admin, "select id from business_assets.asset_book_definition where tenant_id=$1 and legal_entity_id=$2 and posts_to_ledger order by id limit 1", Tenant, LegalEntity)
            ?? throw new InvalidOperationException("A ledger-posting asset book is missing."));
        var assetId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var fiscalYearId = Guid.CreateVersion7();
        var accountingPeriodId = Guid.CreateVersion7();
        var isolatedYear = 9000 + Random.Shared.Next(0, 900);
        var period = BuildIsolatedPeriod(isolatedYear);
        while (Convert.ToInt64(await ScalarAsync(admin, "select count(*) from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and start_date=$3", Tenant, LegalEntity, period.Start), System.Globalization.CultureInfo.InvariantCulture) != 0)
        {
            if (++isolatedYear > 9899) throw new InvalidOperationException("No isolated BC2-T15 accounting year is available.");
            period = BuildIsolatedPeriod(isolatedYear);
        }
        var assetNumber = $"BC2-T15-{assetId:N}";
        var payrollRunId = Guid.Empty;
        var depreciationRunId = Guid.Empty;

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        await using var scope = factory.Services.CreateAsyncScope();
        var payroll = scope.ServiceProvider.GetRequiredService<PayrollRepository>();
        var assets = scope.ServiceProvider.GetRequiredService<FixedAssetService>();
        var tenant = new TenantId(Tenant);

        try
        {
            await ExecuteAsync(admin, "insert into finance_ledger.fiscal_year(tenant_id,legal_entity_id,id,code,start_date,end_date,status,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,'Open',1,now(),now())", Tenant, LegalEntity, fiscalYearId, $"BC2-T15-{fiscalYearId:N}", period.Start, period.End);
            await ExecuteAsync(admin, "insert into finance_ledger.accounting_period(tenant_id,legal_entity_id,id,fiscal_year_id,code,start_date,end_date,sequence_no,status,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,1,'Open',1,now(),now())", Tenant, LegalEntity, accountingPeriodId, fiscalYearId, $"BC2-T15-{accountingPeriodId:N}", period.Start, period.End);
            await ExecuteAsync(admin, "insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,lifecycle_state,asset_nature,tracking_granularity,acquisition_date,capitalization_date,placed_in_service_date,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,'BC2-T15 depreciation asset','Operational','Equipment','Individual',$6,$6,$6,1,$7,now(),1)", Tenant, assetId, LegalEntity, category, assetNumber, period.Start, actor);
            await ExecuteAsync(admin, "insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version) values($1,$2,$3,$4,12000,0,36,'StraightLine',$5,now(),1)", Tenant, bookId, assetId, bookDefinition, period.Start);

            var payrollPeriod = await payroll.CreatePeriodAsync(tenant, LegalEntity,
                new Modules.Payroll.DTOs.CreatePayrollPeriodRequest(payrollGroup, period.Start, period.End, period.InputCutoff, period.End, period.PlannedPayDate), default);
            var run = await payroll.CreateRunAsync(tenant, LegalEntity,
                new Modules.Payroll.DTOs.CreatePayrollRunRequest(payrollGroup, payrollPeriod.Id), default);
            payrollRunId = run.Id;
            var payrollSnapshot = await BuildSnapshotAsync(adminConnection, Tenant, LegalEntity, period.Start, period.End);
            await payroll.SaveHrSnapshotAsync(tenant, LegalEntity, run.Id, payrollSnapshot, default);
            await payroll.CalculateRunAsync(tenant, LegalEntity, run.Id, default);
            var calculatedRun = await payroll.GetRunAsync(tenant, LegalEntity, run.Id, default);
            Assert.Equal("Calculated", calculatedRun?.CalculationState);
            await payroll.ApproveRunAsync(tenant, LegalEntity, run.Id, default);
            await payroll.FinalizeRunAsync(tenant, LegalEntity, run.Id, default);
            var payrollPosting = await payroll.ReconcileAccountingAsync(tenant, LegalEntity, run.Id, actor, default);
            Assert.True(payrollPosting.IsPosted);

            var depreciation = await assets.PrepareDepreciationRunAsync(tenant, actor,
                new Modules.FixedAssets.CreateDepreciationRunRequest(LegalEntity, bookDefinition, period.Start, period.End, $"bc2-t15:{assetId:D}"), default);
            depreciationRunId = depreciation.Id;
            Assert.True(depreciation.TotalAmount > 0m);
            await assets.PostDepreciationRunAsync(tenant, actor, depreciation.Id, default);

            var payrollSource = new Modules.Payroll.Repositories.PayrollOwnerReconciliationSource(dataSource);
            var fixedAssetSource = new Modules.FixedAssets.Repositories.FixedAssetsOwnerReconciliationSource(dataSource);
            var expensesSource = new Modules.Expenses.Repositories.ExpensesOwnerReconciliationSource(dataSource);
            var ledgerSource = new Modules.Ledger.Repositories.LedgerOwnerReconciliationSource(dataSource);
            var payrollFacts = await payrollSource.ListFinancialFactsAsync(tenant, LegalEntity, default);
            var fixedFacts = await fixedAssetSource.ListFinancialFactsAsync(tenant, LegalEntity, default);
            var expenseFacts = await expensesSource.ListFinancialFactsAsync(tenant, LegalEntity, default);
            var ledgerFacts = await ledgerSource.ListFinancialFactsAsync(tenant, LegalEntity, default);
            Assert.NotEmpty(payrollFacts);
            Assert.NotEmpty(fixedFacts);
            Assert.NotEmpty(expenseFacts);
            var payrollFact = Assert.Single(payrollFacts, f => f.RecognitionIdentity == $"payroll:{payrollRunId:D}");
            var fixedFact = Assert.Single(fixedFacts, f => f.RecognitionIdentity == $"depreciation-run:{depreciationRunId:D}:book:{bookId:D}");
            var payrollLedger = Assert.Single(ledgerFacts, f => f.RecognitionIdentity == payrollFact.RecognitionIdentity);
            var fixedLedger = Assert.Single(ledgerFacts, f => f.RecognitionIdentity == fixedFact.RecognitionIdentity);
            foreach (var expenseFact in expenseFacts)
                Assert.Contains(ledgerFacts, fact => fact.RecognitionIdentity == expenseFact.RecognitionIdentity && fact.Amount == expenseFact.Amount && fact.CurrencyCode == expenseFact.CurrencyCode);
            Assert.Equal(payrollLedger.Amount, payrollFact.Amount);
            Assert.Equal(fixedLedger.Amount, fixedFact.Amount);
            Assert.Equal("OPEX", payrollFact.MetricCode);
            Assert.Equal("DEPRECIATION", fixedFact.MetricCode);
            Assert.Equal("EGP", payrollFact.CurrencyCode);
            Assert.Equal("EGP", fixedFact.CurrencyCode);

            var planVersionId = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", Tenant)
                ?? throw new InvalidOperationException("A published Planning version is missing."));
            var repository = new Modules.Planning.PlanningRepository(
                new Core.Transactions.NpgsqlTransactionCoordinator(dataSource),
                ownerSources: [ledgerSource, payrollSource, fixedAssetSource, expensesSource]);
            var first = await repository.RefreshActualsAsync(tenant, actor, new("seed-v1", $"bc2-t15:{Guid.CreateVersion7():N}", null, planVersionId), default);
            var snapshot = (Guid)(await ScalarAsync(admin, "select snapshot_id from business_planning.actual_refresh_run where tenant_id=$1 and id=$2", Tenant, first))!;
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_actual_snapshot_fact where tenant_id=$1 and snapshot_id=$2 and recognition_identity=$3", Tenant, snapshot, payrollFact.RecognitionIdentity!), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_actual_snapshot_fact where tenant_id=$1 and snapshot_id=$2 and recognition_identity=$3", Tenant, snapshot, fixedFact.RecognitionIdentity!), System.Globalization.CultureInfo.InvariantCulture));

            var replay = await repository.RefreshActualsAsync(tenant, actor, new("seed-v1", "different-caller-watermark", null, planVersionId), default);
            Assert.Equal(first, replay);

            await assets.ReverseDepreciationRunAsync(tenant, actor, depreciationRunId,
                new Modules.FixedAssets.ReverseDepreciationRunRequest(period.End, "BC2-T15 correction", $"bc2-t15-reversal:{depreciationRunId:D}"), default);
            var corrected = await repository.RefreshActualsAsync(tenant, actor, new("seed-v1", $"bc2-t15-correction:{Guid.CreateVersion7():N}", null, planVersionId), default);
            var correctedSnapshot = (Guid)(await ScalarAsync(admin, "select snapshot_id from business_planning.actual_refresh_run where tenant_id=$1 and id=$2", Tenant, corrected))!;
            var netDepreciation = await ScalarAsync(admin, "select coalesce(sum(amount),0) from business_planning.planning_actual_snapshot_fact where tenant_id=$1 and snapshot_id=$2 and source_module='ledger' and (source_reference=$3 or source_reference=$4)", Tenant, correctedSnapshot, $"depreciation-run:{depreciationRunId:D}:book:{bookId:D}", $"depreciation-reversal:{depreciationRunId:D}:book:{bookId:D}");
            Assert.Equal(0m, netDepreciation);
        }
        finally
        {
            if (payrollRunId != Guid.Empty)
            {
                // Finalized payroll and its posted Ledger evidence are immutable by contract; the
                // disposable acceptance database retains those rows as the financial proof.
            }
            if (depreciationRunId != Guid.Empty)
            {
                // Posted depreciation and reversal journals are immutable; retain them in the
                // disposable database so the exact correction evidence remains inspectable.
            }
            if (depreciationRunId != Guid.Empty)
            {
                await ExecuteAsync(admin, "delete from business_assets.asset_depreciation_run_line where tenant_id=$1 and run_id=$2", Tenant, depreciationRunId);
                await ExecuteAsync(admin, "delete from business_assets.asset_depreciation_run where tenant_id=$1 and id=$2", Tenant, depreciationRunId);
            }
            if (bookId != Guid.Empty)
            {
                await ExecuteAsync(admin, "delete from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2", Tenant, bookId);
                await ExecuteAsync(admin, "delete from business_assets.asset_book where tenant_id=$1 and id=$2", Tenant, bookId);
            }
            await ExecuteAsync(admin, "delete from business_assets.fixed_asset where tenant_id=$1 and id=$2", Tenant, assetId);
            if (payrollRunId != Guid.Empty)
            {
                await ExecuteAsync(admin, "delete from platform.outbox_message where tenant_id=$1 and causation_id=$2", Tenant, payrollRunId);
                var finalizationState = (string?)await ScalarAsync(admin, "select finalization_state from business_payroll.payroll_run where tenant_id=$1 and id=$2", Tenant, payrollRunId);
                if (!string.Equals(finalizationState, "Finalized", StringComparison.Ordinal))
                {
                    await ExecuteAsync(admin, "delete from business_payroll.payslip_line where tenant_id=$1 and payslip_id in (select id from business_payroll.payslip where tenant_id=$1 and run_employee_id in (select id from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2))", Tenant, payrollRunId);
                    await ExecuteAsync(admin, "delete from business_payroll.payslip where tenant_id=$1 and run_employee_id in (select id from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2)", Tenant, payrollRunId);
                    await ExecuteAsync(admin, "delete from business_payroll.payroll_calculation_line where tenant_id=$1 and run_employee_id in (select id from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2)", Tenant, payrollRunId);
                    await ExecuteAsync(admin, "delete from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2", Tenant, payrollRunId);
                    await ExecuteAsync(admin, "delete from business_payroll.accounting_fact where tenant_id=$1 and run_id=$2", Tenant, payrollRunId);
                    await ExecuteAsync(admin, "delete from business_payroll.payroll_hr_snapshot where tenant_id=$1 and payroll_run_id=$2", Tenant, payrollRunId);
                    await ExecuteAsync(admin, "delete from business_payroll.payroll_run where tenant_id=$1 and id=$2", Tenant, payrollRunId);
                    await ExecuteAsync(admin, "delete from business_payroll.payroll_period where tenant_id=$1 and payroll_group_id=$2 and period_start=$3", Tenant, payrollGroup, period.Start);
                }
            }
            // The period is intentionally retained when posted journals reference it; the
            // disposable acceptance database keeps the complete financial evidence immutable.
        }
    }

    private static async Task<HrPayrollPeriodSnapshotContract> BuildSnapshotAsync(string connectionString, Guid tenantId, Guid legalEntityId, DateOnly start, DateOnly end)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var employeeCommand = new NpgsqlCommand("select em.id,em.employee_id,em.employment_number from business_hr.employment em where em.tenant_id=$1 and em.legal_entity_id=$2 and em.status='Active' order by em.employment_number limit 80", connection);
        employeeCommand.Parameters.AddWithValue(tenantId); employeeCommand.Parameters.AddWithValue(legalEntityId);
        var employments = new List<HrPayrollEmploymentInput>();
        await using var employeeReader = await employeeCommand.ExecuteReaderAsync();
        while (await employeeReader.ReadAsync())
        {
            employments.Add(new(employeeReader.GetGuid(0), employeeReader.GetGuid(1), employeeReader.GetString(2), "Active", [],
                [new(Guid.CreateVersion7(), "EGP", 30000m, "Monthly", start, end, 1)], [],
                new(0, 0, 0, 0, 0, 0, 0), [], new(0, 0), []));
        }

        var snapshot = new HrPayrollPeriodSnapshotContract(Guid.CreateVersion7(), 1, legalEntityId, start, end,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "", employments);
        return snapshot with { PayloadSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshot))).ToLowerInvariant() };
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i]);
        return await command.ExecuteScalarAsync();
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i]);
        await command.ExecuteNonQueryAsync();
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"Set {name} for the disposable PostgreSQL acceptance test.");

    private static (DateOnly Start, DateOnly End, DateOnly InputCutoff, DateOnly PlannedPayDate) BuildIsolatedPeriod(int year)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 1, 28);
        while (end.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday) end = end.AddDays(-1);
        return (start, end, start.AddDays(Math.Min(10, end.DayNumber - start.DayNumber)), end);
    }
}
