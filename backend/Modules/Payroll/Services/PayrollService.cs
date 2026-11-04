using Modules.Payroll.DTOs;
using Modules.Payroll.Repositories;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Hr;

namespace Modules.Payroll.Services;

public sealed class PayrollService(PayrollRepository repository, IHumanResourcesPayrollCapability hrPayroll)
{
    public Task<IReadOnlyList<PayrollGroupDto>> ListGroupsAsync(TenantId tenant, Guid entity, int page, int size, string? search, CancellationToken ct) => repository.ListGroupsAsync(tenant, entity, page, size, search, ct);
    public Task<PayrollGroupDto> CreateGroupAsync(TenantId tenant, CreatePayrollGroupRequest request, CancellationToken ct) => repository.CreateGroupAsync(tenant, request, ct);
    public Task<PayrollPeriodDto> CreatePeriodAsync(TenantId tenant, Guid entity, CreatePayrollPeriodRequest request, CancellationToken ct) => repository.CreatePeriodAsync(tenant, entity, request, ct);
    public Task<IReadOnlyList<PayrollPeriodDto>> ListPeriodsAsync(TenantId tenant, Guid entity, Guid group, int page, int size, string? status, CancellationToken ct) => repository.ListPeriodsAsync(tenant, entity, group, page, size, status, ct);
    public Task<IReadOnlyList<PayrollRunDto>> ListRunsAsync(TenantId tenant, Guid entity, int page, int size, string? search, string? runType, string? state, CancellationToken ct) => repository.ListRunsAsync(tenant, entity, page, size, search, runType, state, ct);
    public Task<PayrollRunDto> CreateRunAsync(TenantId tenant, Guid entity, CreatePayrollRunRequest request, CancellationToken ct) => repository.CreateRunAsync(tenant, entity, request, ct);
    public Task<PayrollRunDto?> GetRunAsync(TenantId tenant, Guid entity, Guid run, CancellationToken ct) => repository.GetRunAsync(tenant, entity, run, ct);
    public Task<PayrollRunDto> ApproveRunAsync(TenantId tenant, Guid entity, Guid run, CancellationToken ct) => repository.ApproveRunAsync(tenant, entity, run, ct);
    public async Task<PayrollSnapshotDto> CaptureInputsAsync(TenantId tenant, Guid entity, Guid run, CancellationToken ct)
    {
        var context = await repository.GetRunContextAsync(tenant, entity, run, ct) ?? throw new PayrollRepositoryException("payroll.run_not_found");
        var cutoff = new DateTimeOffset(context.InputCutoff.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var snapshot = await hrPayroll.CreatePeriodSnapshotAsync(tenant, new(entity, context.PeriodStart, context.PeriodEnd, cutoff), ct);
        var stored = await repository.SaveHrSnapshotAsync(tenant, entity, run, snapshot, ct);
        await hrPayroll.LockAttendancePeriodAsync(tenant, new(entity, run, context.Run.RunNumber, context.PeriodStart, context.PeriodEnd, snapshot.SnapshotId), ct);
        return stored;
    }
    public Task<IReadOnlyList<PayrollEmployeeDto>> ListRunEmployeesAsync(TenantId tenant, Guid entity, Guid run, int page, int size, string? status, string? search, CancellationToken ct) => repository.ListRunEmployeesAsync(tenant, entity, run, page, size, status, search, ct);
    public Task CalculateRunAsync(TenantId tenant, Guid entity, Guid run, CancellationToken ct) => repository.CalculateRunAsync(tenant, entity, run, ct);
    public Task FinalizeRunAsync(TenantId tenant, Guid entity, Guid run, CancellationToken ct) => repository.FinalizeRunAsync(tenant, entity, run, ct);
    public async Task<PayslipsPageDto> ListPayslipsAsync(TenantId tenant, Guid entity, int page, int size, Guid? employmentId, string? search, CancellationToken ct)
    {
        IReadOnlyList<Guid>? matchingEmploymentIds = null;
        if (!string.IsNullOrWhiteSpace(search))
        {
            matchingEmploymentIds = await hrPayroll.SearchEmploymentIdsAsync(tenant, entity, search, ct);
        }
        var pageResult = await repository.ListPayslipsAsync(tenant, entity, page, size, employmentId, search, matchingEmploymentIds, ct);
        if (pageResult.Items.Count == 0) return pageResult;
        var empIds = pageResult.Items.Select(s => s.EmploymentId).Distinct().ToArray();
        var identities = await hrPayroll.ResolveEmploymentIdentitiesAsync(tenant, entity, empIds, ct);
        var enriched = pageResult.Items.Select(s => identities.TryGetValue(s.EmploymentId, out var ident)
            ? s with { EmployeeName = ident.DisplayName, EmploymentNumber = ident.EmploymentNumber }
            : s).ToArray();
        return pageResult with { Items = enriched };
    }
    public async Task<IReadOnlyList<PayslipDto>> ExportPayslipsAsync(TenantId tenant, Guid entity, Guid? employmentId, string? search, CancellationToken ct)
    {
        IReadOnlyList<Guid>? matchingEmploymentIds = null;
        if (!string.IsNullOrWhiteSpace(search))
        {
            matchingEmploymentIds = await hrPayroll.SearchEmploymentIdsAsync(tenant, entity, search, ct);
        }
        var slips = await repository.ExportPayslipsAsync(tenant, entity, employmentId, search, matchingEmploymentIds, 5000, ct);
        if (slips.Count == 0) return slips;
        var empIds = slips.Select(s => s.EmploymentId).Distinct().ToArray();
        var identities = await hrPayroll.ResolveEmploymentIdentitiesAsync(tenant, entity, empIds, ct);
        return slips.Select(s => identities.TryGetValue(s.EmploymentId, out var ident)
            ? s with { EmployeeName = ident.DisplayName, EmploymentNumber = ident.EmploymentNumber }
            : s).ToArray();
    }
    public async Task<IReadOnlyList<PayslipDto>> ListSelfPayslipsAsync(TenantId tenant, Guid membershipId, Guid entity, int page, int size, CancellationToken ct)
    {
        var employmentId = await hrPayroll.GetSelfEmploymentAsync(tenant, membershipId, entity, ct);
        if (employmentId is null) return [];
        var slips = await repository.ListSelfPayslipsAsync(tenant, entity, employmentId.Value, page, size, ct);
        if (slips.Count == 0) return slips;
        var identities = await hrPayroll.ResolveEmploymentIdentitiesAsync(tenant, entity, [employmentId.Value], ct);
        return slips.Select(s => identities.TryGetValue(s.EmploymentId, out var ident)
            ? s with { EmployeeName = ident.DisplayName, EmploymentNumber = ident.EmploymentNumber }
            : s).ToArray();
    }
    public Task<PayrollInputDto> CreateInputAsync(TenantId tenant, CreatePayrollInputRequest request, CancellationToken ct) => repository.CreateInputAsync(tenant, request, ct);
    public Task<PayrollInputDto> ApproveInputAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.ApproveInputAsync(tenant, entity, id, ct);
    public Task<PayrollInputDto?> GetInputAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.GetInputAsync(tenant, entity, id, ct);
    public Task<EmployeeReceivableDto> CreateReceivableAsync(TenantId tenant, CreateReceivableRequest request, CancellationToken ct) => repository.CreateReceivableAsync(tenant, request, ct);
    public Task<EmployeeReceivableDto> ApproveReceivableAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.ApproveReceivableAsync(tenant, entity, id, ct);
    public Task<ReceivableDisbursementDto> RequestReceivableDisbursementAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.RequestReceivableDisbursementAsync(tenant, entity, id, ct);
    public Task<ReceivableDisbursementDto> CompleteReceivableDisbursementAsync(TenantId tenant, Guid entity, Guid id, string? providerReference, CancellationToken ct) => repository.CompleteReceivableDisbursementAsync(tenant, entity, id, providerReference, ct);
    public Task<ReceivableDisbursementDto> FailReceivableDisbursementAsync(TenantId tenant, Guid entity, Guid id, string? failureCode, CancellationToken ct) => repository.FailReceivableDisbursementAsync(tenant, entity, id, failureCode, ct);
    public Task<PayrollLoanActionDto> ApplyLoanActionAsync(TenantId tenant, Guid entity, Guid receivableId, Guid actor, PayrollLoanActionRequest request, CancellationToken ct) => repository.ApplyLoanActionAsync(tenant, entity, receivableId, actor, request, ct);
    public Task<PayrollReportSummaryDto> GetReportAsync(TenantId tenant, Guid entity, DateOnly? from, DateOnly? to, string? runType, string? search, int page, int size, CancellationToken ct) => repository.GetReportAsync(tenant, entity, from, to, runType, search, page, size, ct);
    public Task<IReadOnlyList<PayrollReportItem>> ExportReportAsync(TenantId tenant, Guid entity, DateOnly? from, DateOnly? to, string? runType, string? search, CancellationToken ct) => repository.ExportReportAsync(tenant, entity, from, to, runType, search, 5000, ct);
    public Task WriteReportExportCsvAsync(TenantId tenant, Guid entity, DateOnly? from, DateOnly? to, string? runType, string? search, Stream output, CancellationToken ct) => repository.WriteReportExportCsvAsync(tenant, entity, from, to, runType, search, output, ct);
    public async Task WritePayslipsExportCsvAsync(TenantId tenant, Guid entity, Guid? employmentId, string? search, Stream output, CancellationToken ct)
    {
        IReadOnlyList<Guid>? matchingEmploymentIds = null;
        if (!string.IsNullOrWhiteSpace(search))
        {
            matchingEmploymentIds = await hrPayroll.SearchEmploymentIdsAsync(tenant, entity, search, ct);
        }
        await repository.WritePayslipsExportCsvAsync(tenant, entity, employmentId, search, matchingEmploymentIds, output, ct);
    }
    public Task<PayrollAccountingReconciliationDto> GetAccountingStateAsync(TenantId tenant, Guid entity, Guid runId, CancellationToken ct) => repository.GetAccountingStateAsync(tenant, entity, runId, ct);
    public Task<PayrollAccountingReconciliationDto> ReconcileAccountingAsync(TenantId tenant, Guid entity, Guid runId, Guid actorId, CancellationToken ct) => repository.ReconcileAccountingAsync(tenant, entity, runId, actorId, ct);
    public Task<PayrollImportResult> ImportInputsAsync(TenantId tenant, Guid entity, Guid actor, PayrollImportRequest request, bool commit, CancellationToken ct) => repository.ImportInputsAsync(tenant, entity, actor, request, commit, ct);
    public Task<PayrollOpeningBalanceDto> CreateOpeningBalanceAsync(TenantId tenant, Guid entity, Guid actor, PayrollOpeningBalanceRequest request, CancellationToken ct) => repository.CreateOpeningBalanceAsync(tenant, entity, actor, request, ct);
    public Task<IReadOnlyList<PayrollOpeningBalanceDto>> ListOpeningBalancesAsync(TenantId tenant, Guid entity, int page, int size, Guid? employmentId, string? balanceType, string? search, CancellationToken ct) => repository.ListOpeningBalancesAsync(tenant, entity, page, size, employmentId, balanceType, search, ct);
    public Task<PayrollOpeningBalanceDto> ApproveOpeningBalanceAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.ApproveOpeningBalanceAsync(tenant, entity, id, ct);
    public Task<PayrollStatutorySettingsDto> GetStatutorySettingsAsync(TenantId tenant, Guid entity, CancellationToken ct) => repository.GetStatutorySettingsAsync(tenant, entity, ct);
    public Task<IReadOnlyList<PayrollStatutorySettingsDto>> ListStatutorySettingsHistoryAsync(TenantId tenant, Guid entity, int page, int size, CancellationToken ct) => repository.ListStatutorySettingsHistoryAsync(tenant, entity, page, size, ct);
    public Task<PayrollStatutorySettingsDto> UpdateStatutorySettingsAsync(TenantId tenant, Guid entity, Guid actor, UpdatePayrollStatutorySettingsRequest request, CancellationToken ct) => repository.UpdateStatutorySettingsAsync(tenant, entity, actor, request, ct);
}
