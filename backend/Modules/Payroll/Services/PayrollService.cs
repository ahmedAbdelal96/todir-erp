using Modules.Payroll.DTOs;
using Modules.Payroll.Repositories;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Hr;

namespace Modules.Payroll.Services;

public sealed class PayrollService(PayrollRepository repository, IHumanResourcesPayrollCapability hrPayroll)
{
    public Task<IReadOnlyList<PayrollGroupDto>> ListGroupsAsync(TenantId tenant, Guid entity, CancellationToken ct) => repository.ListGroupsAsync(tenant, entity, ct);
    public Task<PayrollGroupDto> CreateGroupAsync(TenantId tenant, CreatePayrollGroupRequest request, CancellationToken ct) => repository.CreateGroupAsync(tenant, request, ct);
    public Task<PayrollPeriodDto> CreatePeriodAsync(TenantId tenant, Guid entity, CreatePayrollPeriodRequest request, CancellationToken ct) => repository.CreatePeriodAsync(tenant, entity, request, ct);
    public Task<IReadOnlyList<PayrollPeriodDto>> ListPeriodsAsync(TenantId tenant, Guid entity, Guid group, CancellationToken ct) => repository.ListPeriodsAsync(tenant, entity, group, ct);
    public Task<IReadOnlyList<PayrollRunDto>> ListRunsAsync(TenantId tenant, Guid entity, int page, int size, CancellationToken ct) => repository.ListRunsAsync(tenant, entity, page, size, ct);
    public Task<PayrollRunDto> CreateRunAsync(TenantId tenant, Guid entity, CreatePayrollRunRequest request, CancellationToken ct) => repository.CreateRunAsync(tenant, entity, request, ct);
    public Task<PayrollRunDto?> GetRunAsync(TenantId tenant, Guid entity, Guid run, CancellationToken ct) => repository.GetRunAsync(tenant, entity, run, ct);
    public async Task<PayrollSnapshotDto> CaptureInputsAsync(TenantId tenant, Guid entity, Guid run, CancellationToken ct)
    {
        var context = await repository.GetRunContextAsync(tenant, entity, run, ct) ?? throw new PayrollRepositoryException("payroll.run_not_found");
        var cutoff = new DateTimeOffset(context.InputCutoff.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var snapshot = await hrPayroll.CreatePeriodSnapshotAsync(tenant, new(entity, context.PeriodStart, context.PeriodEnd, cutoff), ct);
        var stored = await repository.SaveHrSnapshotAsync(tenant, entity, run, snapshot, ct);
        await hrPayroll.LockAttendancePeriodAsync(tenant, new(entity, run, context.Run.RunNumber, context.PeriodStart, context.PeriodEnd, snapshot.SnapshotId), ct);
        return stored;
    }
    public Task<IReadOnlyList<PayrollEmployeeDto>> ListRunEmployeesAsync(TenantId tenant, Guid entity, Guid run, CancellationToken ct) => repository.ListRunEmployeesAsync(tenant, entity, run, ct);
    public Task CalculateRunAsync(TenantId tenant, Guid entity, Guid run, CancellationToken ct) => repository.CalculateRunAsync(tenant, entity, run, ct);
    public Task FinalizeRunAsync(TenantId tenant, Guid entity, Guid run, CancellationToken ct) => repository.FinalizeRunAsync(tenant, entity, run, ct);
    public Task<IReadOnlyList<PayslipDto>> ListPayslipsAsync(TenantId tenant, Guid entity, CancellationToken ct) => repository.ListPayslipsAsync(tenant, entity, ct);
    public Task<PayrollInputDto> CreateInputAsync(TenantId tenant, CreatePayrollInputRequest request, CancellationToken ct) => repository.CreateInputAsync(tenant, request, ct);
    public Task<PayrollInputDto> ApproveInputAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.ApproveInputAsync(tenant, entity, id, ct);
    public Task<PayrollInputDto?> GetInputAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.GetInputAsync(tenant, entity, id, ct);
    public Task<EmployeeReceivableDto> CreateReceivableAsync(TenantId tenant, CreateReceivableRequest request, CancellationToken ct) => repository.CreateReceivableAsync(tenant, request, ct);
    public Task<EmployeeReceivableDto> ApproveReceivableAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.ApproveReceivableAsync(tenant, entity, id, ct);
    public Task<ReceivableDisbursementDto> RequestReceivableDisbursementAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.RequestReceivableDisbursementAsync(tenant, entity, id, ct);
    public Task<PayrollStatutorySettingsDto> GetStatutorySettingsAsync(TenantId tenant, Guid entity, CancellationToken ct) => repository.GetStatutorySettingsAsync(tenant, entity, ct);
    public Task<IReadOnlyList<PayrollStatutorySettingsDto>> ListStatutorySettingsHistoryAsync(TenantId tenant, Guid entity, CancellationToken ct) => repository.ListStatutorySettingsHistoryAsync(tenant, entity, ct);
    public Task<PayrollStatutorySettingsDto> UpdateStatutorySettingsAsync(TenantId tenant, Guid entity, Guid actor, UpdatePayrollStatutorySettingsRequest request, CancellationToken ct) => repository.UpdateStatutorySettingsAsync(tenant, entity, actor, request, ct);
}
