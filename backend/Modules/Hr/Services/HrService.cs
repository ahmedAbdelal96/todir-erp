using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Modules.Hr.Repositories;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Services;

public sealed class HrService(HrRepository repository, HrImportRepository imports, HrAssignmentImportRepository assignmentImports, HrContractImportRepository contractImports, HrCompensationImportRepository compensationImports, HrInsuranceImportRepository insuranceImports, HrDocumentImportRepository documentImports, HrGoLiveConfigurationRepository goLiveConfigurations, HrReminderRepository reminders, HrHomeRepository home, HrTeamRepository team, HrReportsRepository reports, WorkCalendarRepository calendars, ShiftRepository shifts, HrAdjustmentRepository adjustments, LeavePolicyRepository leavePolicies, AttendanceRepository attendance, AttendanceImportRepository attendanceImports, LeaveRequestRepository leaveRequests, OnboardingRepository onboarding, OvertimeRepository overtime, HrDocumentRequirementRepository documentRequirements)
{
    public static HrException? TranslatePersistenceException(Exception exception) => HrRepositoryExceptionTranslator.Translate(exception);

    public Task<HrHomeDashboardView> GetHomeSummaryAsync(TenantId tenant, Guid legalEntityId, DateOnly asOfDate, CancellationToken ct) =>
        home.GetSummaryAsync(tenant, legalEntityId, asOfDate, ct);

    public Task<HrReminderPage> ListRemindersAsync(TenantId tenant, Guid legalEntityId, DateOnly asOf, int page, int pageSize, CancellationToken ct) =>
        reminders.ListAsync(tenant, legalEntityId, asOf, page, pageSize, ct);

    public Task<HrTeamPage> ListMyTeamAsync(TenantId tenant, Guid membershipId, Guid legalEntityId, DateOnly asOfDate, int page, int pageSize, CancellationToken ct) =>
        team.ListDirectReportsAsync(tenant, membershipId, legalEntityId, asOfDate, page, pageSize, ct);

    public Task<IReadOnlyList<ManagerLeaveApprovalView>> ListManagerLeaveApprovalsAsync(TenantId tenant, Guid membershipId, Guid legalEntityId, DateOnly asOfDate, int page, int pageSize, CancellationToken ct) =>
        team.ListPendingLeaveApprovalsAsync(tenant, membershipId, legalEntityId, asOfDate, page, pageSize, ct);

    public Task<HrAttendanceReportPage> GetAttendanceReportAsync(TenantId tenant, Guid legalEntityId, DateOnly from, DateOnly to, int page, int pageSize, CancellationToken ct) =>
        reports.GetAttendanceReportAsync(tenant, legalEntityId, from, to, page, pageSize, ct);

    public Task<HrLeaveBalanceReportPage> GetLeaveBalanceReportAsync(TenantId tenant, Guid legalEntityId, DateOnly asOfDate, int page, int pageSize, CancellationToken ct) =>
        reports.GetLeaveBalanceReportAsync(tenant, legalEntityId, asOfDate, page, pageSize, ct);

    public Task<HrExpiryReportPage> GetExpiryReportAsync(TenantId tenant, Guid legalEntityId, DateOnly asOfDate, int page, int pageSize, CancellationToken ct) =>
        reports.GetExpiryReportAsync(tenant, legalEntityId, asOfDate, page, pageSize, ct);

    public Task<EmployeeProfile> CreateEmployeeAsync(TenantId tenant, Guid actor, Guid membershipId, CreateEmployeeRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CreateEmployeeAsync(tenant, actor, membershipId, request, correlationId, ct);

    public Task<EmployeeMembershipLinkView> LinkEmployeeMembershipAsync(TenantId tenant, Guid actor, Guid actingMembershipId, Guid employeeId,
        EmployeeMembershipLinkRequest request, Guid correlationId, CancellationToken ct) =>
        repository.LinkMembershipAsync(tenant, actor, actingMembershipId, employeeId, request, correlationId, ct);

    public Task<HrEmployeeImportSummary> PreviewEmployeeImportAsync(TenantId tenant, HrEmployeeImportRequest request, CancellationToken ct) =>
        imports.PreviewEmployeesAsync(tenant, request, ct);

    public Task<HrEmployeeImportSummary> CommitEmployeeImportAsync(TenantId tenant, Guid actor, Guid membershipId, Guid correlationId, HrEmployeeImportRequest request, CancellationToken ct) =>
        imports.CommitEmployeesAsync(tenant, actor, membershipId, correlationId, request, ct);

    public Task<HrAssignmentImportSummary> PreviewAssignmentImportAsync(TenantId tenant, HrAssignmentImportRequest request, CancellationToken ct) =>
        assignmentImports.PreviewAsync(tenant, request, ct);

    public Task<HrAssignmentImportSummary> CommitAssignmentImportAsync(TenantId tenant, Guid actor, Guid membershipId, Guid correlationId, HrAssignmentImportRequest request, CancellationToken ct) =>
        assignmentImports.CommitAsync(tenant, actor, membershipId, correlationId, request, ct);

    public Task<HrContractImportSummary> PreviewContractImportAsync(TenantId tenant, HrContractImportRequest request, CancellationToken ct) =>
        contractImports.PreviewAsync(tenant, request, ct);

    public Task<HrContractImportSummary> CommitContractImportAsync(TenantId tenant, Guid actor, Guid membershipId, Guid correlationId, HrContractImportRequest request, CancellationToken ct) =>
        contractImports.CommitAsync(tenant, actor, membershipId, correlationId, request, ct);

    public Task<HrCompensationImportSummary> PreviewCompensationImportAsync(TenantId tenant, HrCompensationImportRequest request, CancellationToken ct) =>
        compensationImports.PreviewAsync(tenant, request, ct);

    public Task<HrCompensationImportSummary> CommitCompensationImportAsync(TenantId tenant, Guid actor, Guid membershipId, Guid correlationId, HrCompensationImportRequest request, CancellationToken ct) =>
        compensationImports.CommitAsync(tenant, actor, membershipId, correlationId, request, ct);

    public Task<HrInsuranceImportSummary> PreviewInsuranceImportAsync(TenantId tenant, HrInsuranceImportRequest request, CancellationToken ct) =>
        insuranceImports.PreviewAsync(tenant, request, ct);

    public Task<HrInsuranceImportSummary> CommitInsuranceImportAsync(TenantId tenant, Guid actor, Guid membershipId, Guid correlationId, HrInsuranceImportRequest request, CancellationToken ct) =>
        insuranceImports.CommitAsync(tenant, actor, membershipId, correlationId, request, ct);

    public Task<HrDocumentImportSummary> PreviewDocumentImportAsync(TenantId tenant, HrDocumentImportRequest request, CancellationToken ct) =>
        documentImports.PreviewAsync(tenant, request, ct);

    public Task<HrDocumentImportSummary> CommitDocumentImportAsync(TenantId tenant, Guid actor, Guid membershipId, Guid correlationId, HrDocumentImportRequest request, CancellationToken ct) =>
        documentImports.CommitAsync(tenant, actor, membershipId, correlationId, request, ct);

    public Task<HrGoLiveConfigurationView?> GetGoLiveConfigurationAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        goLiveConfigurations.GetAsync(tenant, legalEntityId, ct);

    public Task<HrGoLiveConfigurationView> SaveGoLiveConfigurationAsync(TenantId tenant, Guid actor, Guid membershipId, Guid legalEntityId, HrGoLiveConfigurationRequest request, Guid correlationId, CancellationToken ct) =>
        goLiveConfigurations.SaveAsync(tenant, actor, membershipId, legalEntityId, request, correlationId, ct);

    public Task<EmployeePage> ListEmployeesAsync(TenantId tenant, EmployeePageRequest request, Guid membershipId, int permissionVersion, CancellationToken ct) =>
        repository.ListEmployeesAsync(tenant, request, membershipId, permissionVersion, ct);

    public Task<EmployeeIdentifierView> CreateIdentifierAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateEmployeeIdentifierRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CreateIdentifierAsync(tenant, actor, membershipId, employeeId, request, correlationId, ct);

    public Task<EmployeeDocumentView> CreateDocumentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateEmployeeDocumentRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CreateDocumentAsync(tenant, actor, membershipId, employeeId, request, correlationId, ct);

    public Task<EmployeeDocumentUploadIntentView> CreateDocumentUploadIntentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateEmployeeDocumentUploadRequest request, CancellationToken ct) =>
        repository.CreateDocumentUploadIntentAsync(tenant, actor, membershipId, employeeId, request, ct);

    public Task<Shared.Contracts.Platform.PlatformFileUploadCompletion> CompleteDocumentUploadAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid legalEntityId, Guid? employmentId, Guid fileRef, CancellationToken ct) =>
        repository.CompleteDocumentUploadAsync(tenant, actor, membershipId, employeeId, legalEntityId, employmentId, fileRef, ct);

    public Task<IReadOnlyList<EmployeeDocumentView>> ListDocumentsAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, bool includeSensitive, CancellationToken ct) =>
        repository.ListDocumentsAsync(tenant, employeeId, legalEntityId, includeSensitive, ct);

    public Task<EmployeeDocumentDownloadView> CreateDocumentDownloadAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid documentId, Guid legalEntityId, bool includeSensitive, Guid correlationId, CancellationToken ct) =>
        repository.CreateDocumentDownloadAsync(tenant, actor, membershipId, employeeId, documentId, legalEntityId, includeSensitive, correlationId, ct);

    public Task<EmployeeDocumentView> VerifyDocumentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid documentId, Guid legalEntityId, VerifyEmployeeDocumentRequest request, Guid correlationId, CancellationToken ct) =>
        repository.VerifyDocumentAsync(tenant, actor, membershipId, employeeId, documentId, legalEntityId, request, correlationId, ct);

    public Task<RequiredDocumentRuleView> SaveRequiredDocumentRuleAsync(TenantId tenant, Guid actor, Guid membershipId, SaveRequiredDocumentRuleRequest request, Guid correlationId, CancellationToken ct) =>
        documentRequirements.SaveAsync(tenant, actor, membershipId, request, correlationId, ct);

    public Task<IReadOnlyList<RequiredDocumentRuleView>> ListRequiredDocumentRulesAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        documentRequirements.ListAsync(tenant, legalEntityId, ct);

    public Task<EmployeeFileCompletenessView> GetDocumentCompletenessAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, CancellationToken ct) =>
        documentRequirements.GetCompletenessAsync(tenant, employeeId, legalEntityId, ct);

    public Task<IReadOnlyList<EmployeeIdentifierView>> ListIdentifiersAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, CancellationToken ct) =>
        repository.ListIdentifiersAsync(tenant, employeeId, legalEntityId, ct);

    public Task<RevealedEmployeeIdentifier> RevealIdentifierAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid identifierId, Guid legalEntityId, Guid correlationId, CancellationToken ct) =>
        repository.RevealIdentifierAsync(tenant, actor, membershipId, employeeId, identifierId, legalEntityId, correlationId, ct);

    public Task<EmployeeIdentifierView> RevokeIdentifierAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid identifierId, Guid legalEntityId, RevokeEmployeeIdentifierRequest request, Guid correlationId, CancellationToken ct) =>
        repository.RevokeIdentifierAsync(tenant, actor, membershipId, employeeId, identifierId, legalEntityId, request, correlationId, ct);

    public Task<IReadOnlyList<EmployeeDuplicateSuggestion>> FindPossibleDuplicatesAsync(TenantId tenant, string arabicName, string? englishName, CancellationToken ct) =>
        repository.FindPossibleDuplicatesAsync(tenant, arabicName, englishName, ct);

    public Task<EmployeeProfile?> GetEmployeeAsync(TenantId tenant, Guid employeeId, Guid? legalEntityId, Guid membershipId, int permissionVersion, CancellationToken ct) =>
        repository.GetEmployeeAsync(tenant, employeeId, legalEntityId, membershipId, permissionVersion, ct);

    public Task<EmployeeProfile?> GetMyEmployeeAsync(TenantId tenant, Guid membershipId, Guid legalEntityId, CancellationToken ct) =>
        repository.GetMyEmployeeAsync(tenant, membershipId, legalEntityId, ct);

    public Task<EmploymentView> CreateEmploymentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateEmploymentRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CreateEmploymentAsync(tenant, actor, membershipId, employeeId, request, correlationId, ct);

    public Task<EmploymentView> ChangeEmploymentStatusAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, Guid legalEntityId, ChangeEmploymentStatusRequest request, Guid correlationId, CancellationToken ct) =>
        repository.ChangeEmploymentStatusAsync(tenant, actor, membershipId, employeeId, employmentId, legalEntityId, request, correlationId, ct);

    public Task<OffboardingCaseView?> GetOffboardingCaseAsync(TenantId tenant, Guid employeeId, Guid employmentId, Guid legalEntityId, CancellationToken ct) =>
        repository.GetOffboardingCaseAsync(tenant, employeeId, employmentId, legalEntityId, ct);

    public Task<OffboardingCaseView> CompleteOffboardingTaskAsync(TenantId tenant, Guid actor, Guid membershipId, Guid taskId, Guid legalEntityId, CompleteOffboardingTaskRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CompleteOffboardingTaskAsync(tenant, actor, membershipId, taskId, legalEntityId, request, correlationId, ct);

    public Task<IReadOnlyList<EmploymentView>> ListEmploymentsAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, CancellationToken ct) =>
        repository.ListEmploymentsAsync(tenant, employeeId, legalEntityId, ct);

    public Task<OrganizationDefinition> CreateDepartmentAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreateDepartmentRequest request, CancellationToken ct) =>
        repository.CreateDepartmentAsync(tenant, actor, legalEntityId, request, ct);

    public Task<OrganizationDefinition> CreatePositionAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreatePositionRequest request, CancellationToken ct) =>
        repository.CreatePositionAsync(tenant, actor, legalEntityId, request, ct);

    public Task<IReadOnlyList<OrganizationDefinition>> ListDepartmentsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        repository.ListDepartmentsAsync(tenant, legalEntityId, ct);

    public Task<IReadOnlyList<OrganizationDefinition>> ListPositionsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        repository.ListPositionsAsync(tenant, legalEntityId, ct);

    public Task<OrganizationDefinition> ArchiveDepartmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, Guid correlationId, CancellationToken ct) =>
        repository.ArchiveDepartmentAsync(tenant, actor, membershipId, id, legalEntityId, correlationId, ct);

    public Task<OrganizationDefinition> ArchivePositionAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, Guid correlationId, CancellationToken ct) =>
        repository.ArchivePositionAsync(tenant, actor, membershipId, id, legalEntityId, correlationId, ct);

    public Task<AssignmentView> CreateAssignmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employmentId, CreateAssignmentRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CreateAssignmentAsync(tenant, actor, membershipId, employmentId, request, correlationId, ct);

    public Task<IReadOnlyList<AssignmentView>> ListAssignmentsAsync(TenantId tenant, Guid employmentId, Guid legalEntityId, DateOnly? asOf, CancellationToken ct) =>
        repository.ListAssignmentsAsync(tenant, employmentId, legalEntityId, asOf, ct);

    public Task<ContractView> CreateContractAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employmentId, CreateContractRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CreateContractAsync(tenant, actor, membershipId, employmentId, request, correlationId, ct);

    public Task<IReadOnlyList<ContractView>> ListContractsAsync(TenantId tenant, Guid employmentId, Guid legalEntityId, CancellationToken ct) =>
        repository.ListContractsAsync(tenant, employmentId, legalEntityId, ct);

    public Task<ContractView> ActivateContractAsync(TenantId tenant, Guid actor, Guid membershipId, Guid contractId, Guid legalEntityId, ActivateContractRequest request, Guid correlationId, CancellationToken ct) =>
        repository.ActivateContractAsync(tenant, actor, membershipId, contractId, legalEntityId, request, correlationId, ct);

    public Task<ContractView> EndContractAsync(TenantId tenant, Guid actor, Guid membershipId, Guid contractId, Guid legalEntityId, EndContractRequest request, Guid correlationId, CancellationToken ct) =>
        repository.EndContractAsync(tenant, actor, membershipId, contractId, legalEntityId, request, correlationId, ct);

    public Task<ContractAmendmentView> CreateContractAmendmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid contractId, Guid legalEntityId, CreateContractAmendmentRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CreateContractAmendmentAsync(tenant, actor, membershipId, contractId, legalEntityId, request, correlationId, ct);

    public Task<IReadOnlyList<ContractAmendmentView>> ListContractAmendmentsAsync(TenantId tenant, Guid contractId, Guid legalEntityId, CancellationToken ct) =>
        repository.ListContractAmendmentsAsync(tenant, contractId, legalEntityId, ct);

    public Task<IReadOnlyList<EmployeeTimelineEvent>> GetTimelineAsync(TenantId tenant, Guid employeeId, Guid? legalEntityId, int limit, CancellationToken ct) =>
        repository.GetTimelineAsync(tenant, employeeId, legalEntityId, limit, ct);

    public Task<InsuranceEnrollmentView> CreateInsuranceEnrollmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, CreateInsuranceEnrollmentRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CreateInsuranceEnrollmentAsync(tenant, actor, membershipId, employeeId, employmentId, request, correlationId, ct);

    public Task<InsuranceEnrollmentView> ActivateInsuranceEnrollmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, Guid enrollmentId, Guid legalEntityId, InsuranceEnrollmentDecisionRequest request, Guid correlationId, CancellationToken ct) =>
        repository.ActivateInsuranceEnrollmentAsync(tenant, actor, membershipId, employeeId, employmentId, enrollmentId, legalEntityId, request, correlationId, ct);

    public Task<InsuranceEnrollmentView> RejectInsuranceEnrollmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, Guid enrollmentId, Guid legalEntityId, InsuranceEnrollmentDecisionRequest request, Guid correlationId, CancellationToken ct) =>
        repository.RejectInsuranceEnrollmentAsync(tenant, actor, membershipId, employeeId, employmentId, enrollmentId, legalEntityId, request, correlationId, ct);

    public Task<IReadOnlyList<InsuranceEnrollmentView>> ListInsuranceEnrollmentsAsync(TenantId tenant, Guid employeeId, Guid employmentId, Guid legalEntityId, CancellationToken ct) =>
        repository.ListInsuranceEnrollmentsAsync(tenant, employeeId, employmentId, legalEntityId, ct);

    public Task<RevealedInsuranceMemberNumber> RevealInsuranceNumberAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, Guid enrollmentId, Guid legalEntityId, Guid correlationId, CancellationToken ct) =>
        repository.RevealInsuranceNumberAsync(tenant, actor, membershipId, employeeId, employmentId, enrollmentId, legalEntityId, correlationId, ct);

    public Task<CompensationAgreementView> CreateCompensationAgreementAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, CreateCompensationAgreementRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CreateCompensationAgreementAsync(tenant, actor, membershipId, employeeId, employmentId, request, correlationId, ct);

    public Task<IReadOnlyList<CompensationAgreementView>> ListCompensationAgreementsAsync(TenantId tenant, Guid employeeId, Guid employmentId, Guid legalEntityId, CancellationToken ct) =>
        repository.ListCompensationAgreementsAsync(tenant, employeeId, employmentId, legalEntityId, ct);

    public Task<QualificationView> CreateQualificationAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateQualificationRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CreateQualificationAsync(tenant, actor, membershipId, employeeId, request, correlationId, ct);

    public Task<IReadOnlyList<QualificationView>> ListQualificationsAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, CancellationToken ct) =>
        repository.ListQualificationsAsync(tenant, employeeId, legalEntityId, ct);

    public Task<CredentialView> CreateCredentialAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateCredentialRequest request, Guid correlationId, CancellationToken ct) =>
        repository.CreateCredentialAsync(tenant, actor, membershipId, employeeId, request, correlationId, ct);

    public Task<IReadOnlyList<CredentialView>> ListCredentialsAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, CancellationToken ct) =>
        repository.ListCredentialsAsync(tenant, employeeId, legalEntityId, ct);

    public Task<CredentialView> VerifyCredentialAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid credentialId, Guid legalEntityId, VerifyCredentialRequest request, Guid correlationId, CancellationToken ct) =>
        repository.VerifyCredentialAsync(tenant, actor, membershipId, employeeId, credentialId, legalEntityId, request, correlationId, ct);

    public Task<WorkCalendarView> CreateWorkCalendarAsync(TenantId tenant, Guid actor, Guid membershipId, CreateWorkCalendarRequest request, Guid correlationId, CancellationToken ct) =>
        calendars.CreateAsync(tenant, actor, membershipId, request, correlationId, ct);

    public Task<IReadOnlyList<WorkCalendarView>> ListWorkCalendarsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        calendars.ListAsync(tenant, legalEntityId, ct);

    public Task<WorkCalendarHolidayView> AddWorkCalendarHolidayAsync(TenantId tenant, Guid actor, Guid membershipId, Guid calendarId, Guid legalEntityId, AddWorkCalendarHolidayRequest request, Guid correlationId, CancellationToken ct) =>
        calendars.AddHolidayAsync(tenant, actor, membershipId, calendarId, legalEntityId, request, correlationId, ct);

    public Task<IReadOnlyList<WorkCalendarHolidayView>> ListWorkCalendarHolidaysAsync(TenantId tenant, Guid calendarId, Guid legalEntityId, CancellationToken ct) =>
        calendars.ListHolidaysAsync(tenant, calendarId, legalEntityId, ct);

    public Task<WorkCalendarHolidayView> CancelWorkCalendarHolidayAsync(TenantId tenant, Guid actor, Guid membershipId, Guid holidayId, Guid legalEntityId, CancelWorkCalendarHolidayRequest request, Guid correlationId, CancellationToken ct) =>
        calendars.CancelHolidayAsync(tenant, actor, membershipId, holidayId, legalEntityId, request, correlationId, ct);

    public Task<WorkDayResolution?> ResolveWorkDayAsync(TenantId tenant, Guid employmentId, Guid legalEntityId, DateOnly date, CancellationToken ct) =>
        calendars.ResolveWorkDayAsync(tenant, employmentId, legalEntityId, date, ct);

    public Task<AttendancePunchResult> RecordAttendancePunchAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employmentId, CreateAttendancePunchRequest request, Guid correlationId, CancellationToken ct) =>
        attendance.RecordPunchAsync(tenant, actor, membershipId, employmentId, request, correlationId, ct);

    public async Task<HrAttendanceImportSummary> PreviewAttendanceImportAsync(TenantId tenant, HrAttendanceImportRequest request, CancellationToken ct) =>
        (await attendanceImports.PrepareAsync(tenant, request, ct)).Summary;

    public async Task<HrAttendanceImportSummary> CommitAttendanceImportAsync(TenantId tenant, Guid actor, Guid membershipId, HrAttendanceImportRequest request, Guid correlationId, CancellationToken ct)
    {
        var plan = await attendanceImports.PrepareAsync(tenant, request, ct);
        var rows = new List<HrAttendanceImportRowResult>(plan.Rows.Count);
        foreach (var prepared in plan.Rows)
        {
            if (prepared.Status != "Ready" || prepared.EmploymentId is not { } employmentId)
            {
                rows.Add(prepared.ToView());
                continue;
            }

            var punch = new CreateAttendancePunchRequest(plan.LegalEntityId, prepared.Row.OccurredAt, prepared.Row.Direction,
                "Imported", prepared.PunchSourceReference, prepared.IdempotencyKey);
            var evidence = new AttendanceImportEvidence(plan.SourceSystem, prepared.Row.SourceReference, plan.BatchReference,
                prepared.Row.RowReference, plan.FileSha256, prepared.PayloadHash);
            try
            {
                var result = await attendance.RecordImportedPunchAsync(tenant, actor, membershipId, employmentId, punch, evidence, correlationId, ct);
                var status = result.DuplicateSubmission ? "AlreadyImported" : "Imported";
                var warnings = prepared.Warnings.ToList();
                if (result.LateAfterPayrollLock) warnings.Add("hr.attendance_import_after_payroll_lock");
                rows.Add(prepared.ToView() with { Status = status, Warnings = warnings });
            }
            catch (HrException exception)
            {
                rows.Add(prepared.ToView() with { Status = "Rejected", Issues = [exception.Code] });
            }
        }

        return new(rows.Count, 0, rows.Count(row => row.Status == "Imported"), rows.Count(row => row.Status == "AlreadyImported"),
            rows.Count(row => row.Status == "Rejected"), rows);
    }

    public Task<AttendancePage> ListAttendanceAsync(TenantId tenant, AttendancePageRequest request, CancellationToken ct) =>
        attendance.ListAsync(tenant, request, ct);

    public Task<AttendanceCorrectionView> CorrectAttendanceAsync(TenantId tenant, Guid actor, Guid membershipId, Guid recordId, Guid legalEntityId, AttendanceCorrectionRequest request, Guid correlationId, CancellationToken ct) =>
        attendance.CorrectAsync(tenant, actor, membershipId, recordId, legalEntityId, request, correlationId, ct);

    public Task<IReadOnlyList<AttendanceExceptionView>> ListAttendanceExceptionsAsync(TenantId tenant, Guid legalEntityId, string? status, int page, int pageSize, CancellationToken ct) =>
        attendance.ListExceptionsAsync(tenant, legalEntityId, status, page, pageSize, ct);

    public Task<IReadOnlyList<AttendanceCorrectionView>> ListAttendanceCorrectionsAsync(TenantId tenant, Guid recordId, Guid legalEntityId, CancellationToken ct) =>
        attendance.ListCorrectionsAsync(tenant, recordId, legalEntityId, ct);

    public Task<ShiftDefinitionView> CreateShiftDefinitionAsync(TenantId tenant, Guid actor, Guid membershipId, CreateShiftDefinitionRequest request, Guid correlationId, CancellationToken ct) =>
        shifts.CreateDefinitionAsync(tenant, actor, membershipId, request, correlationId, ct);

    public Task<IReadOnlyList<ShiftDefinitionView>> ListShiftDefinitionsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        shifts.ListDefinitionsAsync(tenant, legalEntityId, ct);

    public Task<ShiftAssignmentView> AssignShiftAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employmentId, AssignShiftRequest request, Guid correlationId, CancellationToken ct) =>
        shifts.AssignAsync(tenant, actor, membershipId, employmentId, request, correlationId, ct);

    public Task<IReadOnlyList<ShiftAssignmentView>> ListShiftAssignmentsAsync(TenantId tenant, Guid employmentId, Guid legalEntityId, CancellationToken ct) =>
        shifts.ListAssignmentsAsync(tenant, employmentId, legalEntityId, ct);

    public Task<HrAdjustmentView> CreateHrAdjustmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, CreateHrAdjustmentRequest request, Guid correlationId, CancellationToken ct) =>
        adjustments.CreateAsync(tenant, actor, membershipId, employeeId, employmentId, request, correlationId, ct);

    public Task<IReadOnlyList<HrAdjustmentView>> ListHrAdjustmentsAsync(TenantId tenant, Guid employmentId, Guid legalEntityId, CancellationToken ct) =>
        adjustments.ListAsync(tenant, employmentId, legalEntityId, ct);

    public Task<HrAdjustmentView> SubmitHrAdjustmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, long expectedVersion, Guid correlationId, CancellationToken ct) =>
        adjustments.SubmitAsync(tenant, actor, membershipId, id, legalEntityId, expectedVersion, correlationId, ct);

    public Task<HrAdjustmentView> ApproveHrAdjustmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, long expectedVersion, Guid correlationId, CancellationToken ct) =>
        adjustments.ApproveAsync(tenant, actor, membershipId, id, legalEntityId, expectedVersion, correlationId, ct);

    public Task<HrAdjustmentView> RejectHrAdjustmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, long expectedVersion, string reason, Guid correlationId, CancellationToken ct) =>
        adjustments.RejectAsync(tenant, actor, membershipId, id, legalEntityId, expectedVersion, reason, correlationId, ct);

    public Task<HrAdjustmentView> CancelHrAdjustmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, long expectedVersion, string reason, Guid correlationId, CancellationToken ct) =>
        adjustments.CancelAsync(tenant, actor, membershipId, id, legalEntityId, expectedVersion, reason, correlationId, ct);

    public Task<LeaveTypeView> CreateLeaveTypeAsync(TenantId tenant, Guid actor, Guid membershipId, CreateLeaveTypeRequest request, Guid correlationId, CancellationToken ct) =>
        leavePolicies.CreateTypeAsync(tenant, actor, membershipId, request, correlationId, ct);

    public Task<IReadOnlyList<LeaveTypeView>> ListLeaveTypesAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        leavePolicies.ListTypesAsync(tenant, legalEntityId, ct);

    public IReadOnlyList<HrLeaveTypeTemplate> GetCountryLeaveTemplates(string providerCode) => leavePolicies.GetProviderLeaveTemplates(providerCode);

    public Task<LeavePolicyView> CreateLeavePolicyAsync(TenantId tenant, Guid actor, Guid membershipId, CreateLeavePolicyRequest request, Guid correlationId, CancellationToken ct) =>
        leavePolicies.CreatePolicyAsync(tenant, actor, membershipId, request, correlationId, ct);

    public Task<IReadOnlyList<LeavePolicyView>> ListLeavePoliciesAsync(TenantId tenant, Guid legalEntityId, Guid leaveTypeId, CancellationToken ct) =>
        leavePolicies.ListPoliciesAsync(tenant, legalEntityId, leaveTypeId, ct);

    public Task<LeaveRequestView> CreateLeaveRequestAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employmentId, CreateLeaveRequest request, Guid correlationId, CancellationToken ct) =>
        leaveRequests.CreateAsync(tenant, actor, membershipId, employmentId, request, correlationId, ct);

    public Task<LeaveRequestView> DecideLeaveRequestAsync(TenantId tenant, Guid actor, Guid membershipId, Guid requestId, Guid legalEntityId, LeaveRequestDecision decision, bool approve, Guid correlationId, CancellationToken ct) =>
        leaveRequests.DecideAsync(tenant, actor, membershipId, requestId, legalEntityId, decision, approve, correlationId, ct);

    public Task<LeaveRequestView> DecideManagerLeaveRequestAsync(TenantId tenant, Guid actor, Guid membershipId, Guid requestId, Guid legalEntityId, LeaveRequestDecision decision, bool approve, Guid correlationId, CancellationToken ct) =>
        leaveRequests.DecideAsync(tenant, actor, membershipId, requestId, legalEntityId, decision, approve, correlationId, ct, membershipId);

    public Task<IReadOnlyList<LeaveRequestView>> ListLeaveRequestsAsync(TenantId tenant, Guid legalEntityId, Guid? employmentId, string? status, int page, int pageSize, CancellationToken ct) =>
        leaveRequests.ListRequestsAsync(tenant, legalEntityId, employmentId, status, page, pageSize, ct);

    public Task<LeaveBalanceView> GetLeaveBalanceAsync(TenantId tenant, Guid employmentId, Guid leaveTypeId, Guid legalEntityId, CancellationToken ct) =>
        leaveRequests.GetBalanceAsync(tenant, employmentId, leaveTypeId, legalEntityId, ct);

    public Task<LeaveBalanceMovementView> AdjustLeaveBalanceAsync(TenantId tenant, Guid actor, Guid membershipId, AdjustLeaveBalanceRequest request, Guid correlationId, CancellationToken ct) =>
        leaveRequests.AdjustBalanceAsync(tenant, actor, membershipId, request, correlationId, ct);

    public Task<HrLeaveOpeningImportSummary> PreviewLeaveOpeningImportAsync(TenantId tenant, HrLeaveOpeningImportRequest request, CancellationToken ct) =>
        leaveRequests.PreviewLeaveOpeningsAsync(tenant, request, ct);

    public Task<HrLeaveOpeningImportSummary> CommitLeaveOpeningImportAsync(TenantId tenant, Guid actor, Guid membershipId, HrLeaveOpeningImportRequest request, Guid correlationId, CancellationToken ct) =>
        leaveRequests.CommitLeaveOpeningsAsync(tenant, actor, membershipId, correlationId, request, ct);

    public Task<int> AccrueLeaveAsync(TenantId tenant, Guid actor, Guid membershipId, Guid legalEntityId, AccrueLeaveRequest request, Guid correlationId, CancellationToken ct) =>
        leaveRequests.AccrueAsync(tenant, actor, membershipId, legalEntityId, request, correlationId, ct);

    public Task<OnboardingTemplateView> CreateOnboardingTemplateAsync(TenantId tenant, Guid actor, Guid membershipId, CreateOnboardingTemplateRequest request, Guid correlationId, CancellationToken ct) =>
        onboarding.CreateTemplateAsync(tenant, actor, membershipId, request, correlationId, ct);

    public Task<IReadOnlyList<OnboardingTemplateView>> ListOnboardingTemplatesAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) => onboarding.ListTemplatesAsync(tenant, legalEntityId, ct);

    public Task<OnboardingCaseView> CreateOnboardingCaseAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, CreateOnboardingCaseRequest request, Guid correlationId, CancellationToken ct) =>
        onboarding.CreateCaseAsync(tenant, actor, membershipId, employeeId, employmentId, request, correlationId, ct);

    public Task<OnboardingCaseView?> GetOnboardingCaseAsync(TenantId tenant, Guid employmentId, Guid legalEntityId, CancellationToken ct) => onboarding.GetCaseAsync(tenant, employmentId, legalEntityId, ct);

    public Task<OnboardingTaskView> CompleteOnboardingTaskAsync(TenantId tenant, Guid actor, Guid membershipId, Guid taskId, Guid legalEntityId, CompleteOnboardingTaskRequest request, Guid correlationId, CancellationToken ct) =>
        onboarding.CompleteTaskAsync(tenant, actor, membershipId, taskId, legalEntityId, request, correlationId, ct);

    public Task<OvertimeRequestView> CreateOvertimeRequestAsync(TenantId tenant, Guid actor, Guid membershipId, Guid recordId, CreateOvertimeRequest request, Guid correlationId, CancellationToken ct) =>
        overtime.CreateAsync(tenant, actor, membershipId, recordId, request, correlationId, ct);

    public Task<OvertimeRequestView> DecideOvertimeRequestAsync(TenantId tenant, Guid actor, Guid membershipId, Guid requestId, Guid legalEntityId, OvertimeRequestDecision request, bool approve, Guid correlationId, CancellationToken ct) =>
        overtime.DecideAsync(tenant, actor, membershipId, requestId, legalEntityId, request, approve, correlationId, ct);

    public Task<IReadOnlyList<OvertimeRequestView>> ListOvertimeRequestsAsync(TenantId tenant, Guid legalEntityId, string? status, int page, int pageSize, CancellationToken ct) =>
        overtime.ListAsync(tenant, legalEntityId, status, page, pageSize, ct);
}
