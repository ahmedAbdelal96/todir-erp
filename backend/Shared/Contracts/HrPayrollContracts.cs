using Shared.Primitives.Identifiers;
using System.Text.Json;

namespace Shared.Contracts.Hr;

public interface IHumanResourcesPayrollCapability
{
    Task<HrPayrollPeriodSnapshotContract> CreatePeriodSnapshotAsync(TenantId tenantId, HrPayrollPeriodSnapshotRequest request, CancellationToken cancellationToken = default);
    Task<HrAttendancePeriodLockContract> LockAttendancePeriodAsync(TenantId tenantId, HrAttendancePeriodLockRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HrPayrollAttendanceCorrectionDelta>> ListAttendanceCorrectionDeltasAsync(TenantId tenantId, Guid snapshotId, CancellationToken cancellationToken = default);
    Task<Guid?> GetSelfEmploymentAsync(TenantId tenantId, Guid membershipId, Guid legalEntityId, CancellationToken cancellationToken = default);
}

public sealed record HrPayrollPeriodSnapshotRequest(Guid LegalEntityId, DateOnly PeriodStart, DateOnly PeriodEnd, DateTimeOffset CutoffAt);
public sealed record HrPayrollContractContext(Guid ContractId, string ContractType, DateOnly StartDate, DateOnly? EndDate, long Version);
public sealed record HrPayrollCompensationInput(Guid AgreementId, string CurrencyCode, decimal Amount, string PayFrequency, DateOnly EffectiveFrom, DateOnly? EffectiveTo, long Version);
public sealed record HrPayrollAdjustmentInput(Guid AdjustmentId, string TypeCode, decimal Amount, string CurrencyCode, string Recurrence, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record HrPayrollAttendanceSummary(int RecordCount, int WorkedMinutes, int LateMinutes, int EarlyLeaveMinutes, int OvertimeEligibleMinutes, int ApprovedOvertimeMinutes, int ExceptionCount);
public sealed record HrPayrollLeaveContext(Guid LeaveRequestId, Guid LeaveTypeId, string LeaveTypeCode, string StatusAtCutoff, string PayrollTreatment, int PolicyVersion, DateOnly RequestedFrom, DateOnly RequestedTo, decimal ChargedQuantityInPeriod, string Unit);
public sealed record HrPayrollAbsenceSummary(int UnexcusedAbsenceDays, int ApprovedLeaveAttendanceDays);
public sealed record HrPayrollInsuranceContext(string SchemeCode, string? ProviderCode, string Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? CountryProviderVersion);
public sealed record HrPayrollUnusedLeaveBalanceFact(Guid LeaveTypeId, string LeaveTypeCode, decimal Balance, string Unit);
public sealed record HrPayrollFinalExitContext(Guid OffboardingCaseId, string ReasonType, string ReasonCode, DateOnly? NoticeDate, DateOnly LastWorkingDate, DateOnly EffectiveEndDate, bool FinalPayrollRequired, IReadOnlyList<HrPayrollUnusedLeaveBalanceFact> UnusedLeaveBalances);
public sealed record HrPayrollEmploymentInput(Guid EmploymentId, Guid EmployeeId, string EmploymentNumber, string EmploymentStatus, IReadOnlyList<HrPayrollContractContext> Contracts, IReadOnlyList<HrPayrollCompensationInput> Compensation, IReadOnlyList<HrPayrollAdjustmentInput> ApprovedAdjustments, HrPayrollAttendanceSummary Attendance, IReadOnlyList<HrPayrollLeaveContext> Leave, HrPayrollAbsenceSummary Absence, IReadOnlyList<HrPayrollInsuranceContext> Insurance, HrPayrollFinalExitContext? FinalExit = null);
public sealed record HrPayrollPeriodSnapshotContract(Guid SnapshotId, int ContractVersion, Guid LegalEntityId, DateOnly PeriodStart, DateOnly PeriodEnd, DateTimeOffset CutoffAt, DateTimeOffset CreatedAt, string PayloadSha256, IReadOnlyList<HrPayrollEmploymentInput> Employments);
public sealed record HrMembershipDeactivationRequestedV1(Guid EmployeeId, Guid EmploymentId, Guid MembershipId, Guid LegalEntityId, DateOnly EffectiveOn, string ReasonCode, DateTimeOffset OccurredAt);
public sealed record HrAttendancePeriodLockRequest(Guid LegalEntityId, Guid PayrollRunId, string PayrollReference, DateOnly PeriodStart, DateOnly PeriodEnd, Guid SnapshotId);
public sealed record HrAttendancePeriodLockContract(Guid PayrollRunId, Guid SnapshotId, Guid LegalEntityId, DateOnly PeriodStart, DateOnly PeriodEnd, string PayrollReference, int LockedRecordCount, DateTimeOffset LockedAt);
public sealed record HrPayrollAttendanceCorrectionDelta(Guid DeltaId, Guid SnapshotId, Guid EmploymentId, Guid AttendanceRecordId, Guid CorrectionId, string PayrollReference, string ReasonCode, DateTimeOffset CreatedAt, JsonElement Before, JsonElement Proposed);
