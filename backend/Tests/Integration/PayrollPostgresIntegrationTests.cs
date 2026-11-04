using Modules.Payroll.DTOs;
using Modules.Payroll.Repositories;
using Modules.Payroll.Services;
using Modules.Hr.Repositories;
using Core.Transactions;
using Npgsql;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Hr;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PayrollPostgresIntegrationTests : DatabaseIntegrationTestBase
{
    private static int periodSequence;

    [PayrollPostgresFact]
    public async Task RegularPayrollPersistsExactEmployeePayslipAndAccountingTotals()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await ReadFixtureAsync(admin); var period = NextTestPeriod(); var periodId = await CreatePeriodAsync(admin, fixture, period.Start, period.End);
        await using var dataSource = NpgsqlDataSource.Create(runtime); var repository = new PayrollRepository(dataSource, new PayrollCountryProviderRegistry()); var tenant = new TenantId(fixture.TenantId);
        var run = await repository.CreateRunAsync(tenant, fixture.LegalEntityId, new(fixture.PayrollGroupId, periodId, "Regular"), default);
        var snapshot = await BuildSnapshotAsync(admin, fixture.TenantId, fixture.LegalEntityId, periodId); await repository.SaveHrSnapshotAsync(tenant, fixture.LegalEntityId, run.Id, snapshot, default); await repository.CalculateRunAsync(tenant, fixture.LegalEntityId, run.Id, default); await repository.FinalizeRunAsync(tenant, fixture.LegalEntityId, run.Id, default);
        Assert.Equal(80L, await CountAsync(admin, "select count(*) from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2 and status='Calculated'", fixture.TenantId, run.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from business_payroll.payroll_run where tenant_id=$1 and id=$2 and finalization_state='Finalized'", fixture.TenantId, run.Id));
        Assert.Equal(await DecimalAsync(admin, "select coalesce(sum(gross),0) from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2", fixture.TenantId, run.Id), await DecimalAsync(admin, "select total_gross from business_payroll.payroll_run where tenant_id=$1 and id=$2", fixture.TenantId, run.Id));
        Assert.Equal(80L, await CountAsync(admin, "select count(*) from business_payroll.payslip where tenant_id=$1 and legal_entity_id=$2 and run_employee_id in (select id from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$3)", fixture.TenantId, fixture.LegalEntityId, run.Id));
        Assert.True(await DecimalAsync(admin, "select coalesce(sum(l.amount),0) from business_payroll.payroll_calculation_line l join business_payroll.payroll_run_employee e on e.tenant_id=l.tenant_id and e.id=l.run_employee_id where l.tenant_id=$1 and e.run_id=$2 and l.source='HRSnapshot'", fixture.TenantId, run.Id) > 0m);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from business_payroll.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and run_id=$3 and status='Ready'", fixture.TenantId, fixture.LegalEntityId, run.Id));
    }

    [PayrollPostgresFact]
    public async Task RetroPersistsExactDeltaAndLeavesOriginalPayslipImmutable()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await ReadFixtureAsync(admin); var period = NextTestPeriod(); var periodId = await CreatePeriodAsync(admin, fixture, period.Start, period.End);
        await using var dataSource = NpgsqlDataSource.Create(runtime); var repository = new PayrollRepository(dataSource, new PayrollCountryProviderRegistry()); var tenant = new TenantId(fixture.TenantId);
        var source = await repository.CreateRunAsync(tenant, fixture.LegalEntityId, new(fixture.PayrollGroupId, periodId, "Regular"), default); var originalSnapshot = await BuildSnapshotAsync(admin, fixture.TenantId, fixture.LegalEntityId, periodId); await repository.SaveHrSnapshotAsync(tenant, fixture.LegalEntityId, source.Id, originalSnapshot, default); await repository.CalculateRunAsync(tenant, fixture.LegalEntityId, source.Id, default); await repository.FinalizeRunAsync(tenant, fixture.LegalEntityId, source.Id, default);
        var originalGross = await DecimalAsync(admin, "select gross from business_payroll.payslip where tenant_id=$1 and legal_entity_id=$2 and employment_id=$3 order by finalized_at limit 1", fixture.TenantId, fixture.LegalEntityId, originalSnapshot.Employments[0].EmploymentId);
        var retro = await repository.CreateRunAsync(tenant, fixture.LegalEntityId, new(fixture.PayrollGroupId, periodId, "Retro", source.Id, periodId), default);
        var changed = originalSnapshot with { Employments = originalSnapshot.Employments.Select((item, index) => index == 0 ? item with { Compensation = [new(Guid.CreateVersion7(), "EGP", 31000m, "Monthly", originalSnapshot.PeriodStart, originalSnapshot.PeriodEnd, 2)] } : item).ToArray(), SnapshotId = Guid.CreateVersion7(), PayloadSha256 = "" };
        changed = changed with { PayloadSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(changed))).ToLowerInvariant() }; await repository.SaveHrSnapshotAsync(tenant, fixture.LegalEntityId, retro.Id, changed, default); await repository.CalculateRunAsync(tenant, fixture.LegalEntityId, retro.Id, default); await repository.FinalizeRunAsync(tenant, fixture.LegalEntityId, retro.Id, default);
        Assert.Equal(1000m, await DecimalAsync(admin, "select total_gross from business_payroll.payroll_run where tenant_id=$1 and id=$2", fixture.TenantId, retro.Id));
        Assert.Equal(originalGross, await DecimalAsync(admin, "select gross from business_payroll.payslip where tenant_id=$1 and legal_entity_id=$2 and employment_id=$3 order by finalized_at limit 1", fixture.TenantId, fixture.LegalEntityId, originalSnapshot.Employments[0].EmploymentId));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from business_payroll.payroll_run where tenant_id=$1 and source_run_id=$2", fixture.TenantId, source.Id));
    }

    [PayrollPostgresFact]
    public async Task FinalSettlementProratesExitLeaveRecoversLoanAndCreatesFinancialArtifacts()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await ReadFixtureAsync(admin);
        var settlementEmploymentId = await FindCleanEmploymentAsync(admin, fixture);
        const decimal expectedGross = 14000m;
        var period = NextTestPeriod();
        var periodId = await CreatePeriodAsync(admin, fixture, period.Start, period.End);
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        var repository = new PayrollRepository(dataSource, new PayrollCountryProviderRegistry());
        var tenant = new TenantId(fixture.TenantId);
        var opening = await repository.CreateOpeningBalanceAsync(tenant, fixture.LegalEntityId, Guid.CreateVersion7(), new(settlementEmploymentId, "Loan", 1000m, "EGP", period.Start.AddDays(-1), $"final-settlement-loan-{Guid.CreateVersion7():N}", 250m, 4), default);
        await repository.ApproveOpeningBalanceAsync(tenant, fixture.LegalEntityId, opening.Id, default);
        var settlementDate = period.Start.AddDays(14);
        var run = await repository.CreateRunAsync(tenant, fixture.LegalEntityId, new(fixture.PayrollGroupId, periodId, "FinalSettlement", SettlementEmploymentId: settlementEmploymentId, EffectiveDate: settlementDate, ReasonCode: "RESIGNATION"), default);
        var snapshot = await BuildSnapshotAsync(admin, fixture.TenantId, fixture.LegalEntityId, periodId);
        var finalExit = new HrPayrollFinalExitContext(Guid.CreateVersion7(), "Resignation", "RESIGNATION", null, settlementDate, settlementDate, true, []);
        var leave = new HrPayrollLeaveContext(Guid.CreateVersion7(), Guid.CreateVersion7(), "UNPAID", "Approved", "Unpaid", 1, period.Start.AddDays(9), period.Start.AddDays(10), 2m, "Days");
        var employment = snapshot.Employments.Single(item => item.EmploymentId == settlementEmploymentId) with { Leave = [leave], FinalExit = finalExit };
        snapshot = snapshot with { Employments = snapshot.Employments.Select(item => item.EmploymentId == settlementEmploymentId ? employment : item).ToArray(), PayloadSha256 = "" };
        snapshot = snapshot with { PayloadSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshot))).ToLowerInvariant() };
        await repository.SaveHrSnapshotAsync(tenant, fixture.LegalEntityId, run.Id, snapshot, default);
        await repository.CalculateRunAsync(tenant, fixture.LegalEntityId, run.Id, default);
        await repository.FinalizeRunAsync(tenant, fixture.LegalEntityId, run.Id, default);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2", fixture.TenantId, run.Id));
        Assert.Equal(expectedGross, await DecimalAsync(admin, "select gross from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2", fixture.TenantId, run.Id));
        Assert.Equal(1000m, await DecimalAsync(admin, "select deductions from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2", fixture.TenantId, run.Id));
        Assert.Equal(expectedGross - 1000m, await DecimalAsync(admin, "select net from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2", fixture.TenantId, run.Id));
        Assert.Equal(1000m, await DecimalAsync(admin, "select coalesce(sum(amount),0) from business_payroll.recovery_application where tenant_id=$1 and run_id=$2", fixture.TenantId, run.Id));
        Assert.Equal(expectedGross - 1000m, await DecimalAsync(admin, "select amount from business_payroll.payment_instruction where tenant_id=$1 and run_id=$2", fixture.TenantId, run.Id));
        Assert.Equal(expectedGross - 1000m, await DecimalAsync(admin, "select total_amount from business_payroll.accounting_fact where tenant_id=$1 and run_id=$2 and status='Ready'", fixture.TenantId, run.Id));
    }

    [PayrollPostgresFact]
    public async Task BonusOnlyLifecycleUsesOneTargetedEmployeeAndFinalizesExactFinancials()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await ReadFixtureAsync(adminConnectionString);
        var source = "payroll-bonus-lifecycle-" + Guid.CreateVersion7().ToString("N");
        var runId = Guid.Empty;
        var inputId = Guid.Empty;
        var snapshotId = Guid.Empty;
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new PayrollRepository(dataSource, new PayrollCountryProviderRegistry());
        var tenant = new TenantId(fixture.TenantId);
        try
        {
            var period = NextTestPeriod();
            var periodId = await CreatePeriodAsync(adminConnectionString, fixture, period.Start, period.End);
            var run = await repository.CreateRunAsync(tenant, fixture.LegalEntityId, new(fixture.PayrollGroupId, periodId, "BonusOnly"), default);
            runId = run.Id;
            var employees = await ReadSnapshotEmploymentsAsync(adminConnectionString, fixture.TenantId, fixture.LegalEntityId, periodId);
            Assert.Equal(80, employees.Count);
            var snapshotPayload = new HrPayrollPeriodSnapshotContract(Guid.CreateVersion7(), 1, fixture.LegalEntityId,
                period.Start, period.End, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                "", employees);
            var payloadHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshotPayload))).ToLowerInvariant();
            snapshotPayload = snapshotPayload with { PayloadSha256 = payloadHash };
            snapshotId = (await repository.SaveHrSnapshotAsync(tenant, fixture.LegalEntityId, runId, snapshotPayload, default)).SnapshotId;

            var import = new PayrollImportRequest("Acceptance", source, source, source,
                [new(1, "bonus-1", 725m, "EGP", employees[0].EmploymentId, "TRANSPORT", period.Start.AddDays(14))], runId);
            var imported = await repository.ImportInputsAsync(tenant, fixture.LegalEntityId, Guid.CreateVersion7(), import, true, default);
            inputId = imported.Rows.Single().InputId!.Value;
            await repository.ApproveInputAsync(tenant, fixture.LegalEntityId, inputId, default);
            await repository.CalculateRunAsync(tenant, fixture.LegalEntityId, runId, default);
            await repository.FinalizeRunAsync(tenant, fixture.LegalEntityId, runId, default);

            Assert.Equal(1L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2", fixture.TenantId, runId));
            Assert.Equal(1L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.payslip where tenant_id=$1 and legal_entity_id=$2 and run_employee_id in (select id from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$3)", fixture.TenantId, fixture.LegalEntityId, runId));
            Assert.Equal(1L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.payslip_line l join business_payroll.payslip p on p.tenant_id=l.tenant_id and p.id=l.payslip_id join business_payroll.payroll_run_employee e on e.tenant_id=p.tenant_id and e.id=p.run_employee_id where p.tenant_id=$1 and e.run_id=$2 and l.code='TRANSPORT' and l.amount=725", fixture.TenantId, runId));
            Assert.Equal(0L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.payslip_line l join business_payroll.payslip p on p.tenant_id=l.tenant_id and p.id=l.payslip_id join business_payroll.payroll_run_employee e on e.tenant_id=p.tenant_id and e.id=p.run_employee_id where p.tenant_id=$1 and e.run_id=$2 and l.code='BASE' and l.amount<>0", fixture.TenantId, runId));
            Assert.Equal(725m, await DecimalAsync(adminConnectionString, "select total_net from business_payroll.payroll_run where tenant_id=$1 and id=$2", fixture.TenantId, runId));
            Assert.Equal(1L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.payment_instruction where tenant_id=$1 and legal_entity_id=$2 and run_id=$3 and amount=725", fixture.TenantId, fixture.LegalEntityId, runId));
            Assert.Equal(1L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and run_id=$3 and total_amount=725 and status='Ready'", fixture.TenantId, fixture.LegalEntityId, runId));
            Assert.Equal(1L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.payroll_input where tenant_id=$1 and id=$2 and status='Consumed' and consumed_run_id=$3", fixture.TenantId, inputId, runId));

            // Verify authoritatively stored posting date in accounting fact payload
            var storedPostingDate = await ScalarStringAsync(adminConnectionString, "select (payload->>'postingDate') from business_payroll.accounting_fact where tenant_id=$1 and run_id=$2", fixture.TenantId, runId);
            Assert.False(string.IsNullOrWhiteSpace(storedPostingDate));

            // Verify outbox message payload contains authoritative posting date
            var outboxPostingDate = await ScalarStringAsync(adminConnectionString, "select (payload->>'postingDate') from platform.outbox_message where tenant_id=$1 and correlation_id=$2 and message_type='PayrollFinalizedV1'", fixture.TenantId, runId);
            Assert.False(string.IsNullOrWhiteSpace(outboxPostingDate));
            Assert.Equal(storedPostingDate, outboxPostingDate);
        }
        finally
        {
            await CleanupLifecycleAsync(adminConnectionString, fixture.TenantId, fixture.LegalEntityId, runId, inputId, snapshotId);
        }
    }

    [PayrollPostgresFact]
    public async Task ReadContractsReturnPersistedPayrollSetupInputsAndReceivablesWithBusinessIdentities()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await ReadFixtureAsync(adminConnectionString);
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new PayrollRepository(dataSource, new PayrollCountryProviderRegistry());
        var hr = new PayrollIntegrationRepository(new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var service = new PayrollService(repository, hr);
        var tenant = new TenantId(fixture.TenantId);
        var receivableId = Guid.Empty;

        try
        {
            var components = await service.ListComponentsAsync(tenant, 1, 1, "BASE", "Active", default);
            Assert.Single(components);
            Assert.Equal("BASE", components[0].Code);
            Assert.Equal("Active", components[0].Status);

            var secondComponentPage = await service.ListComponentsAsync(tenant, 2, 1, null, "Active", default);
            Assert.NotEmpty(secondComponentPage);
            Assert.NotEqual(components[0].Id, secondComponentPage[0].Id);

            var structures = await service.ListSalaryStructuresAsync(tenant, fixture.LegalEntityId, 1, 10, "STANDARD-EG", "Active", default);
            var structure = Assert.Single(structures);
            Assert.Equal("STANDARD-EG", structure.Code);
            Assert.Equal(4, structure.ComponentsCount);

            var assignments = await service.ListEmployeeAssignmentsAsync(tenant, fixture.LegalEntityId, 1, 1, null, "Active", default);
            var assignment = Assert.Single(assignments);
            Assert.NotEqual(Guid.Empty, assignment.EmploymentId);
            Assert.False(string.IsNullOrWhiteSpace(assignment.EmployeeName));
            Assert.False(string.IsNullOrWhiteSpace(assignment.EmploymentNumber));
            Assert.Equal("EGP", assignment.CurrencyCode);

            // Component search must work even when HR has no matching employee name/number.
            var inputs = await service.ListInputsAsync(tenant, fixture.LegalEntityId, 1, 50, "TRANSPORT", null, null, null, default);
            Assert.NotEmpty(inputs);
            Assert.Contains(inputs, input => input.ComponentCode == "TRANSPORT" && !string.IsNullOrWhiteSpace(input.ComponentName));
            Assert.Contains(inputs, input => !string.IsNullOrWhiteSpace(input.EmployeeName) && !string.IsNullOrWhiteSpace(input.EmploymentNumber));

            var created = await repository.CreateReceivableAsync(tenant,
                new CreateReceivableRequest(fixture.LegalEntityId, fixture.EmploymentId, "Advance", 321m, "EGP"), default);
            receivableId = created.Id;
            var receivables = await service.ListReceivablesAsync(tenant, fixture.LegalEntityId, 1, 50, "Advance", "Draft", null, default);
            var receivable = Assert.Single(receivables, item => item.Id == receivableId);
            Assert.Equal(321m, receivable.Principal);
            Assert.False(string.IsNullOrWhiteSpace(receivable.EmployeeName));
            Assert.False(string.IsNullOrWhiteSpace(receivable.EmploymentNumber));
        }
        finally
        {
            if (receivableId != Guid.Empty)
                await ExecuteAsync(adminConnectionString, "delete from business_payroll.employee_receivable where tenant_id=$1 and id=$2", fixture.TenantId, receivableId);
        }
    }

    [PayrollPostgresFact]
    public async Task ManagementContractsCreatePublishAndCancelPersistedPayrollSetup()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await ReadFixtureAsync(adminConnectionString);
        var suffix = Guid.CreateVersion7().ToString("N")[..10].ToUpperInvariant();
        var componentId = Guid.Empty; var componentVersionId = Guid.Empty; var structureId = Guid.Empty; var structureVersionId = Guid.Empty; var inputId = Guid.Empty;
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new PayrollRepository(dataSource, new PayrollCountryProviderRegistry());
        var tenant = new TenantId(fixture.TenantId);
        try
        {
            var component = await repository.CreateComponentAsync(tenant, new($"TEST_{suffix}", "Acceptance component", "Earning", "Credit", "Manual"), default);
            componentId = component.Id;
            using var formulaDocument = JsonDocument.Parse("{}");
            var version = await repository.CreateComponentVersionAsync(tenant, componentId, new(new DateOnly(2026, 1, 1), formulaDocument.RootElement.Clone()), default);
            componentVersionId = version.Id;
            Assert.Equal("Published", (await repository.PublishComponentVersionAsync(tenant, componentVersionId, default)).Status);

            var structure = await repository.CreateSalaryStructureAsync(tenant, new(fixture.LegalEntityId, $"TEST_{suffix}", "Acceptance structure", "EGP"), default);
            structureId = structure.Id;
            var structureVersion = await repository.CreateSalaryStructureVersionAsync(tenant, fixture.LegalEntityId, structureId, new(new DateOnly(2026, 1, 1), "EGP"), default);
            structureVersionId = structureVersion.Id;
            Assert.Equal("Published", (await repository.PublishSalaryStructureVersionAsync(tenant, fixture.LegalEntityId, structureVersionId, default)).Status);

            var input = await repository.CreateInputAsync(tenant, new(fixture.LegalEntityId, fixture.EmploymentId, componentId, new DateOnly(2026, 1, 15), 123m, "EGP", "Acceptance"), default);
            inputId = input.Id;
            Assert.Equal("Cancelled", (await repository.CancelInputAsync(tenant, fixture.LegalEntityId, inputId, default)).Status);
            Assert.Equal("Cancelled", (await repository.GetInputAsync(tenant, fixture.LegalEntityId, inputId, default))!.Status);
        }
        finally
        {
            await ExecuteAsync(adminConnectionString, "delete from business_payroll.payroll_input where tenant_id=$1 and id=$2", fixture.TenantId, inputId);
            await ExecuteAsync(adminConnectionString, "delete from business_payroll.salary_structure_version where tenant_id=$1 and id=$2", fixture.TenantId, structureVersionId);
            await ExecuteAsync(adminConnectionString, "delete from business_payroll.salary_structure where tenant_id=$1 and id=$2", fixture.TenantId, structureId);
            await ExecuteAsync(adminConnectionString, "delete from business_payroll.payroll_component_version where tenant_id=$1 and id=$2", fixture.TenantId, componentVersionId);
            await ExecuteAsync(adminConnectionString, "delete from business_payroll.payroll_component where tenant_id=$1 and id=$2", fixture.TenantId, componentId);
        }
    }

    [PayrollPostgresFact]
    public async Task PublishedStructureVersionCanBeSelectedAssignedAndUsedByPayrollCalculation()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await ReadFixtureAsync(adminConnectionString);
        var period = NextTestPeriod();
        var periodId = await CreatePeriodAsync(adminConnectionString, fixture, period.Start, period.End);
        var oldAssignmentId = await ReadAssignmentIdAsync(adminConnectionString, fixture.TenantId, fixture.EmploymentId);
        var baseComponentId = await ReadComponentIdAsync(adminConnectionString, fixture.TenantId, "BASE");
        var transportComponentId = await ReadComponentIdAsync(adminConnectionString, fixture.TenantId, "TRANSPORT");
        var baseVersionId = await ReadPublishedComponentVersionIdAsync(adminConnectionString, fixture.TenantId, "BASE");
        var transportVersionId = await ReadPublishedComponentVersionIdAsync(adminConnectionString, fixture.TenantId, "TRANSPORT");
        var newAssignmentId = Guid.Empty; var runId = Guid.Empty; var inputId = Guid.Empty; var structureId = Guid.Empty; var structureVersionId = Guid.Empty;
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new PayrollRepository(dataSource, new PayrollCountryProviderRegistry());
        var tenant = new TenantId(fixture.TenantId);
        try
        {
            await ExecuteAsync(adminConnectionString, "update business_payroll.employee_payroll_assignment set effective_to=$3 where tenant_id=$1 and id=$2", fixture.TenantId, oldAssignmentId, period.Start.AddDays(-1));
            var structure = await repository.CreateSalaryStructureAsync(tenant, new(fixture.LegalEntityId, $"CONFIG-{Guid.CreateVersion7():N}"[..16], "Configured acceptance structure", "EGP"), default);
            structureId = structure.Id;
            var version = await repository.CreateSalaryStructureVersionAsync(tenant, fixture.LegalEntityId, structureId, new(period.Start, "EGP"), default);
            structureVersionId = version.Id;
            await repository.CreateSalaryStructureComponentAsync(tenant, fixture.LegalEntityId, structureVersionId, new(baseComponentId, baseVersionId, 10), default);
            await repository.CreateSalaryStructureComponentAsync(tenant, fixture.LegalEntityId, structureVersionId, new(transportComponentId, transportVersionId, 20), default);
            await repository.PublishSalaryStructureVersionAsync(tenant, fixture.LegalEntityId, structureVersionId, default);
            Assert.Single(await repository.ListSalaryStructureVersionsAsync(tenant, fixture.LegalEntityId, structureId, 1, 20, period.Start, null, default));
            var configured = await repository.ListSalaryStructureComponentsAsync(tenant, fixture.LegalEntityId, structureVersionId, 1, 20, null, default);
            Assert.Equal(2, configured.Count);
            Assert.Equal("BASE", configured[0].ComponentCode);
            Assert.Equal("TRANSPORT", configured[1].ComponentCode);
            Assert.Contains(await repository.ListComponentVersionsAsync(tenant, transportComponentId, 1, 20, period.Start, "Published", default), item => item.Id == transportVersionId);

            var assignment = await repository.CreateEmployeeAssignmentAsync(tenant, fixture.EmploymentId, new(fixture.LegalEntityId, fixture.PayrollGroupId, structureVersionId, period.Start, null, 20000m, "EGP"), default);
            newAssignmentId = assignment.Id;
            var run = await repository.CreateRunAsync(tenant, fixture.LegalEntityId, new(fixture.PayrollGroupId, periodId, "Regular"), default); runId = run.Id;
            var snapshot = await BuildSnapshotAsync(adminConnectionString, fixture.TenantId, fixture.LegalEntityId, periodId);
            var target = snapshot.Employments.Select(item => item.EmploymentId == fixture.EmploymentId ? item with { Compensation = [new(Guid.CreateVersion7(), "EGP", 20000m, "Monthly", period.Start, period.End, 1)] } : item).ToArray();
            snapshot = snapshot with { Employments = target, PayloadSha256 = "" };
            snapshot = snapshot with { PayloadSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshot))).ToLowerInvariant() };
            await repository.SaveHrSnapshotAsync(tenant, fixture.LegalEntityId, runId, snapshot, default);
            var input = await repository.CreateInputAsync(tenant, new(fixture.LegalEntityId, fixture.EmploymentId, transportComponentId, period.Start.AddDays(1), 500m, "EGP", "Acceptance", $"configured-structure-{runId:D}"), default); inputId = input.Id;
            await repository.ApproveInputAsync(tenant, fixture.LegalEntityId, inputId, default);
            await repository.CalculateRunAsync(tenant, fixture.LegalEntityId, runId, default);
            await repository.FinalizeRunAsync(tenant, fixture.LegalEntityId, runId, default);
            Assert.Equal(20500m, await DecimalAsync(adminConnectionString, "select gross from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2 and employment_id=$3", fixture.TenantId, runId, fixture.EmploymentId));
            Assert.Equal(20000m, await DecimalAsync(adminConnectionString, "select amount from business_payroll.payroll_calculation_line l join business_payroll.payroll_run_employee e on e.tenant_id=l.tenant_id and e.id=l.run_employee_id join business_payroll.payroll_component c on c.tenant_id=l.tenant_id and c.id=l.component_id where l.tenant_id=$1 and e.run_id=$2 and e.employment_id=$3 and c.code='BASE'", fixture.TenantId, runId, fixture.EmploymentId));
            Assert.Equal(500m, await DecimalAsync(adminConnectionString, "select amount from business_payroll.payroll_calculation_line l join business_payroll.payroll_run_employee e on e.tenant_id=l.tenant_id and e.id=l.run_employee_id join business_payroll.payroll_component c on c.tenant_id=l.tenant_id and c.id=l.component_id where l.tenant_id=$1 and e.run_id=$2 and e.employment_id=$3 and c.code='TRANSPORT'", fixture.TenantId, runId, fixture.EmploymentId));
        }
        finally
        {
            if (inputId != Guid.Empty) await ExecuteAsync(adminConnectionString, "delete from business_payroll.payroll_input where tenant_id=$1 and id=$2", fixture.TenantId, inputId);
            if (newAssignmentId != Guid.Empty) await ExecuteAsync(adminConnectionString, "delete from business_payroll.employee_payroll_assignment where tenant_id=$1 and id=$2", fixture.TenantId, newAssignmentId);
            await ExecuteAsync(adminConnectionString, "update business_payroll.employee_payroll_assignment set effective_to=null where tenant_id=$1 and id=$2", fixture.TenantId, oldAssignmentId);
            if (structureVersionId != Guid.Empty) await ExecuteAsync(adminConnectionString, "delete from business_payroll.salary_structure_component where tenant_id=$1 and structure_version_id=$2", fixture.TenantId, structureVersionId);
            if (structureVersionId != Guid.Empty) await ExecuteAsync(adminConnectionString, "delete from business_payroll.salary_structure_version where tenant_id=$1 and id=$2", fixture.TenantId, structureVersionId);
            if (structureId != Guid.Empty) await ExecuteAsync(adminConnectionString, "delete from business_payroll.salary_structure where tenant_id=$1 and id=$2", fixture.TenantId, structureId);
        }
    }

    [PayrollPostgresFact]
    public async Task ImportRequiresExplicitApprovalAndOpeningLoanFeedsRecoverySchedule()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await ReadFixtureAsync(adminConnectionString);
        var source = "payroll-acceptance-" + Guid.CreateVersion7().ToString("N");
        var batchId = Guid.Empty;
        var inputId = Guid.Empty;
        var openingId = Guid.Empty;
        var runId = Guid.Empty;
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new PayrollRepository(dataSource, new PayrollCountryProviderRegistry());
        var tenant = new TenantId(fixture.TenantId);
        try
        {
            var period = NextTestPeriod();
            var periodId = await CreatePeriodAsync(adminConnectionString, fixture, period.Start, period.End);
            var input = new PayrollImportRequest("Acceptance", source, source, source,
                [new(1, "transport-1", 725m, "EGP", fixture.EmploymentId, "TRANSPORT", period.Start.AddDays(14))]);
            var run = await repository.CreateRunAsync(tenant, fixture.LegalEntityId,
                new(fixture.PayrollGroupId, periodId, "OffCycle"), default);
            runId = run.Id;
            input = input with { SupplementalRunId = runId };
            var imported = await repository.ImportInputsAsync(tenant, fixture.LegalEntityId, Guid.CreateVersion7(), input, true, default);
            batchId = imported.BatchId;
            inputId = imported.Rows.Single().InputId!.Value;
            Assert.Equal("Draft", (await repository.GetInputAsync(tenant, fixture.LegalEntityId, inputId, default))!.Status);
            await repository.ApproveInputAsync(tenant, fixture.LegalEntityId, inputId, default);
            Assert.Equal(1L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.payroll_input where tenant_id=$1 and id=$2 and supplemental_run_id=$3", fixture.TenantId, inputId, runId));
            var replay = await repository.ImportInputsAsync(tenant, fixture.LegalEntityId, Guid.CreateVersion7(), input, true, default);
            Assert.Equal("Committed", replay.Status);
            Assert.Equal(1L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.payroll_input where tenant_id=$1 and idempotency_key=$2", fixture.TenantId, $"payroll-import:{batchId:D}:transport-1"));

            var opening = await repository.CreateOpeningBalanceAsync(tenant, fixture.LegalEntityId, Guid.CreateVersion7(),
                new(fixture.EmploymentId, "Loan", 1000m, "EGP", new DateOnly(2026, 9, 30), source, 250m, 4), default);
            openingId = opening.Id;
            Assert.Equal("Approved", (await repository.ApproveOpeningBalanceAsync(tenant, fixture.LegalEntityId, openingId, default)).Status);
            Assert.Equal(1L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.employee_receivable where tenant_id=$1 and employment_id=$2 and principal=1000 and created_at >= (select created_at from business_payroll.payroll_opening_balance where id=$3)", fixture.TenantId, fixture.EmploymentId, openingId));
            Assert.Equal(4L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.recovery_installment i join business_payroll.employee_receivable r on r.id=i.receivable_id where i.tenant_id=$1 and r.employment_id=$2 and r.principal=1000 and r.created_at >= (select created_at from business_payroll.payroll_opening_balance where id=$3)", fixture.TenantId, fixture.EmploymentId, openingId));
            Assert.Single(await repository.ListOpeningBalancesAsync(tenant, fixture.LegalEntityId, 1, 10, fixture.EmploymentId, "Loan", source, default));
        }
        finally
        {
            await CleanupAsync(adminConnectionString, fixture.TenantId, batchId, inputId, openingId, runId);
        }
    }

    private static async Task<PayrollFixture> ReadFixtureAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select t.id,e.id,em.id,g.id,p.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id and e.status='Active' join business_hr.employment em on em.tenant_id=t.id and em.legal_entity_id=e.id and em.status='Active' join business_payroll.payroll_group g on g.tenant_id=t.id and g.legal_entity_id=e.id and g.status='Active' join business_payroll.payroll_period p on p.tenant_id=g.tenant_id and p.legal_entity_id=g.legal_entity_id and p.payroll_group_id=g.id and p.status='Open' where t.slug='atlas-trading' order by em.employment_number,p.period_end desc limit 1", connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("The canonical development seed must contain Atlas active payroll data.");
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4));
    }

    private static async Task<Guid> FindCleanEmploymentAsync(string connectionString, PayrollFixture fixture)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select e.id from business_hr.employment e where e.tenant_id=$1 and e.legal_entity_id=$2 and e.status='Active' and not exists (select 1 from business_payroll.employee_receivable r where r.tenant_id=e.tenant_id and r.legal_entity_id=e.legal_entity_id and r.employment_id=e.id) order by e.employment_number limit 1", connection);
        command.Parameters.AddWithValue(fixture.TenantId); command.Parameters.AddWithValue(fixture.LegalEntityId);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The payroll acceptance fixture requires an employment without an existing receivable."));
    }

    private static async Task<Guid> ReadAssignmentIdAsync(string connectionString, Guid tenantId, Guid employmentId)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select id from business_payroll.employee_payroll_assignment where tenant_id=$1 and employment_id=$2 order by effective_from desc limit 1", connection); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(employmentId);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The payroll acceptance fixture requires an existing assignment."));
    }

    private static async Task<Guid> ReadComponentIdAsync(string connectionString, Guid tenantId, string code)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select id from business_payroll.payroll_component where tenant_id=$1 and code=$2", connection); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(code);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException($"Payroll component {code} is missing from the canonical seed."));
    }

    private static async Task<Guid> ReadPublishedComponentVersionIdAsync(string connectionString, Guid tenantId, string code)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select v.id from business_payroll.payroll_component_version v join business_payroll.payroll_component c on c.tenant_id=v.tenant_id and c.id=v.component_id where v.tenant_id=$1 and c.code=$2 and v.status='Published' order by v.effective_from desc limit 1", connection); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(code);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException($"Published component version for {code} is missing from the canonical seed."));
    }

    private static async Task<IReadOnlyList<HrPayrollEmploymentInput>> ReadSnapshotEmploymentsAsync(string connectionString, Guid tenantId, Guid legalEntityId, Guid periodId)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var period = new NpgsqlCommand("select period_start,period_end from business_payroll.payroll_period where tenant_id=$1 and id=$2", connection); period.Parameters.AddWithValue(tenantId); period.Parameters.AddWithValue(periodId);
        await using var periodReader = await period.ExecuteReaderAsync(); if (!await periodReader.ReadAsync()) throw new InvalidOperationException("Payroll period missing."); var start = periodReader.GetFieldValue<DateOnly>(0); var end = periodReader.GetFieldValue<DateOnly>(1); await periodReader.CloseAsync();
        await using var command = new NpgsqlCommand("select em.id,em.employee_id,em.employment_number from business_hr.employment em where em.tenant_id=$1 and em.legal_entity_id=$2 and em.status='Active' order by em.employment_number limit 80", connection); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(legalEntityId);
        var result = new List<HrPayrollEmploymentInput>(); await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), "Active", [], [new(Guid.CreateVersion7(), "EGP", 30000m, "Monthly", start, end, 1)], [], new(0, 0, 0, 0, 0, 0, 0), [], new(0, 0), [])); return result;
    }

    private static async Task<HrPayrollPeriodSnapshotContract> BuildSnapshotAsync(string connectionString, Guid tenantId, Guid legalEntityId, Guid periodId)
    {
        var employments = await ReadSnapshotEmploymentsAsync(connectionString, tenantId, legalEntityId, periodId);
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await using var command = new NpgsqlCommand("select period_start,period_end from business_payroll.payroll_period where tenant_id=$1 and id=$2", connection); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(periodId); await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync(); var start = reader.GetFieldValue<DateOnly>(0); var end = reader.GetFieldValue<DateOnly>(1); var snapshot = new HrPayrollPeriodSnapshotContract(Guid.CreateVersion7(), 1, legalEntityId, start, end, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "", employments); return snapshot with { PayloadSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshot))).ToLowerInvariant() };
    }

    private static async Task<Guid> CreatePeriodAsync(string connectionString, PayrollFixture fixture, DateOnly start, DateOnly end)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        for (var attempt = 0; attempt < 365; attempt++)
        {
            var candidateStart = start.AddDays(attempt); var candidateEnd = end.AddDays(attempt); var id = Guid.CreateVersion7();
            await using var command = new NpgsqlCommand("insert into business_payroll.payroll_period(tenant_id,legal_entity_id,id,payroll_group_id,period_start,period_end,input_cutoff,calculation_date,planned_pay_date,status,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$6,$6,$6,'Open',now(),now())", connection);
            command.Parameters.AddWithValue(fixture.TenantId); command.Parameters.AddWithValue(fixture.LegalEntityId); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(fixture.PayrollGroupId); command.Parameters.AddWithValue(candidateStart); command.Parameters.AddWithValue(candidateEnd);
            try { await command.ExecuteNonQueryAsync(); return id; }
            catch (PostgresException ex) when (ex.SqlState == "23505" && ex.ConstraintName?.Contains("payroll_period", StringComparison.OrdinalIgnoreCase) == true) { }
        }
        throw new InvalidOperationException("Unable to allocate a collision-free payroll acceptance period.");
    }

    private static (DateOnly Start, DateOnly End) NextTestPeriod()
    {
        // Keep acceptance periods under the canonical NotManaged settings; the helper's
        // collision-resistant offset makes repeated local runs independent.
        var start = new DateOnly(2027, 1, 1).AddDays(Interlocked.Increment(ref periodSequence));
        return (start, start.AddDays(29));
    }

    private static async Task<decimal> DecimalAsync(string connectionString, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await using var command = new NpgsqlCommand(sql, connection); Add(command, values); return Convert.ToDecimal(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ScalarStringAsync(string connectionString, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection); Add(command, values);
        var res = await command.ExecuteScalarAsync();
        return res is DBNull or null ? null : res.ToString();
    }

    private static async Task CleanupLifecycleAsync(string connectionString, Guid tenantId, Guid legalEntityId, Guid runId, Guid inputId, Guid snapshotId)
    {
        // Finalized payroll is immutable by design. This acceptance test runs only against the disposable CI database;
        // retaining the completed rows is intentional evidence for the persisted financial assertions.
        await Task.CompletedTask;
    }

    private static async Task<long> CountAsync(string connectionString, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection); Add(command, values);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(string connectionString, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection); Add(command, values); await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupAsync(string connectionString, Guid tenantId, Guid batchId, Guid inputId, Guid openingId, Guid runId)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var sql in new[]
        {
            "delete from business_payroll.recovery_installment where tenant_id=$1 and receivable_id in (select id from business_payroll.employee_receivable where tenant_id=$1 and created_at >= (select created_at from business_payroll.payroll_opening_balance where tenant_id=$1 and id=$2))",
            "delete from business_payroll.employee_receivable where tenant_id=$1 and created_at >= (select created_at from business_payroll.payroll_opening_balance where tenant_id=$1 and id=$2)",
            "delete from business_payroll.payroll_opening_balance where tenant_id=$1 and id=$2",
            "delete from business_payroll.payroll_input where tenant_id=$1 and id=$2",
            "delete from business_payroll.payroll_import_row where tenant_id=$1 and batch_id=$2",
            "delete from business_payroll.payroll_import_batch where tenant_id=$1 and id=$2",
            "delete from business_payroll.payroll_run where tenant_id=$1 and id=$2"
        })
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction); command.Parameters.AddWithValue(tenantId); if (sql.Contains("$2")) command.Parameters.AddWithValue(sql.Contains("opening_balance") || sql.Contains("receivable") ? openingId : sql.Contains("payroll_input") ? inputId : sql.Contains("payroll_run where") ? runId : batchId); await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    private static void Add(NpgsqlCommand command, params object[] values) { foreach (var value in values) command.Parameters.AddWithValue(value); }
    private sealed record PayrollFixture(Guid TenantId, Guid LegalEntityId, Guid EmploymentId, Guid PayrollGroupId, Guid PeriodId);

    private sealed class PayrollPostgresFactAttribute : FactAttribute
    {
        public PayrollPostgresFactAttribute() { if (Environment.GetEnvironmentVariable("TODIR_PAYROLL_TEST_DATABASE") != "1") Skip = "Set TODIR_PAYROLL_TEST_DATABASE=1 only for the disposable PostgreSQL payroll integration gate."; }
    }
}
