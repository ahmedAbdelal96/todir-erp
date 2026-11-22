import { ApiError, httpClient } from "../../../core/api/http-client.ts";
import type {
  EmployeePage,
  EmployeeDirectoryFreshness,
  EmployeeListItem,
  HrTeamPage,
  EmployeeProfile,
  EmploymentView,
  AssignmentView,
  EmploymentContractView,
  ContractAmendmentView,
  CompensationAgreementView,
  InsuranceEnrollmentView,
  EmployeeDocumentView,
  EmployeeDocumentUploadIntentView,
  EmployeeDocumentDownloadView,
  CreateEmployeeDocumentUploadRequest,
  CompleteEmployeeDocumentUploadRequest,
  CreateEmployeeDocumentRequest,
  VerifyEmployeeDocumentRequest,
  DocumentCompletenessView,
  DocumentRequirementView,
  QualificationView,
  CredentialView,
  EmployeeIdentifierView,
  AttendancePage,
  AttendanceRecordView,
  AttendanceExceptionView,
  AttendanceCorrectionView,
  LeaveTypeView,
  LeavePolicyView,
  LeaveBalanceView,
  LeaveRequestView,
  HrAdjustmentView,
  ShiftDefinitionView,
  ShiftAssignmentView,
  WorkCalendarView,
  WorkCalendarHolidayView,
  TimelineEventView,
  HrHomeSummary,
  HrImportSummary,
  OnboardingCaseView,
  OffboardingCaseView,
  OrganizationDefinition,
  CreateEmployeeRequest,
  CreateEmploymentRequest,
  ChangeEmploymentStatusRequest,
  CreateAssignmentRequest,
  CreateEmploymentContractRequest,
  CreateCompensationAgreementRequest,
  CreateInsuranceEnrollmentRequest,
  CreateAttendancePunchRequest,
  AttendanceCorrectionRequest,
  CreateLeaveRequest,
  LeaveRequestDecision,
  AdjustLeaveBalanceRequest,
  CreateHrAdjustmentRequest,
  CreateDepartmentRequest,
  CreatePositionRequest,
  CreateShiftDefinitionRequest,
  AssignShiftRequest,
  CreateWorkCalendarRequest,
  AddWorkCalendarHolidayRequest
} from "../types.ts";

const BASE = "/api/v1/hr";

export const hrApi = {
  // Home & Reminders
  getHomeSummary: (legalEntityId: string, asOfDate?: string | undefined): Promise<HrHomeSummary> => {
    const params = new URLSearchParams({ legalEntityId });
    if (asOfDate) params.set("asOfDate", asOfDate);
    return httpClient<HrHomeSummary>(`${BASE}/home?${params.toString()}`);
  },

  // Employees Directory
  listEmployees: (params?: { search?: string | null | undefined; legalEntityId?: string | null | undefined; page?: number | null | undefined; pageSize?: number | null | undefined } | undefined): Promise<EmployeePage> => {
    const searchParams = new URLSearchParams();
    if (params?.search) searchParams.set("search", params.search);
    if (params?.legalEntityId) searchParams.set("legalEntityId", params.legalEntityId);
    if (params?.page != null) searchParams.set("page", String(params.page));
    if (params?.pageSize != null) searchParams.set("pageSize", String(params.pageSize));
    const query = searchParams.toString();
    return httpClient<EmployeePage>(`${BASE}/employees${query ? `?${query}` : ""}`);
  },

  getEmployeeDirectoryFreshness: (legalEntityId?: string | undefined, branchId?: string | undefined): Promise<EmployeeDirectoryFreshness> => {
    const params = new URLSearchParams();
    if (legalEntityId) params.set("legalEntityId", legalEntityId);
    if (branchId) params.set("branchId", branchId);
    const query = params.toString();
    return httpClient<EmployeeDirectoryFreshness>(`${BASE}/employees/freshness${query ? `?${query}` : ""}`);
  },

  getEmployee: (employeeId: string, legalEntityId?: string | undefined): Promise<EmployeeProfile> => {
    const query = legalEntityId ? `?legalEntityId=${encodeURIComponent(legalEntityId)}` : "";
    return httpClient<EmployeeProfile>(`${BASE}/employees/${encodeURIComponent(employeeId)}${query}`);
  },

  createEmployee: (request: CreateEmployeeRequest): Promise<EmployeeProfile> =>
    httpClient<EmployeeProfile>(`${BASE}/employees`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listEmployments: (employeeId: string, legalEntityId: string): Promise<readonly EmploymentView[]> =>
    httpClient<readonly EmploymentView[]>(`${BASE}/employees/${encodeURIComponent(employeeId)}/employments?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createEmployment: (employeeId: string, request: CreateEmploymentRequest): Promise<EmploymentView> =>
    httpClient<EmploymentView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/employments`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  changeEmploymentStatus: (employeeId: string, employmentId: string, legalEntityId: string, request: ChangeEmploymentStatusRequest): Promise<EmploymentView> =>
    httpClient<EmploymentView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/employments/${encodeURIComponent(employmentId)}/status?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Organization: Departments, Positions, Assignments
  listDepartments: (legalEntityId: string): Promise<readonly OrganizationDefinition[]> =>
    httpClient<readonly OrganizationDefinition[]>(`${BASE}/departments?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createDepartment: (request: CreateDepartmentRequest): Promise<OrganizationDefinition> =>
    httpClient<OrganizationDefinition>(`${BASE}/departments`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listPositions: (legalEntityId: string): Promise<readonly OrganizationDefinition[]> =>
    httpClient<readonly OrganizationDefinition[]>(`${BASE}/positions?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createPosition: (request: CreatePositionRequest): Promise<OrganizationDefinition> =>
    httpClient<OrganizationDefinition>(`${BASE}/positions`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listAssignments: (employmentId: string, legalEntityId: string): Promise<readonly AssignmentView[]> =>
    httpClient<readonly AssignmentView[]>(`${BASE}/employments/${encodeURIComponent(employmentId)}/assignments?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createAssignment: (employmentId: string, request: CreateAssignmentRequest): Promise<AssignmentView> =>
    httpClient<AssignmentView>(`${BASE}/employments/${encodeURIComponent(employmentId)}/assignments`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Contracts & Amendments
  listContracts: (employmentId: string, legalEntityId: string): Promise<readonly EmploymentContractView[]> =>
    httpClient<readonly EmploymentContractView[]>(`${BASE}/employments/${encodeURIComponent(employmentId)}/contracts?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createContract: (employmentId: string, request: CreateEmploymentContractRequest): Promise<EmploymentContractView> =>
    httpClient<EmploymentContractView>(`${BASE}/employments/${encodeURIComponent(employmentId)}/contracts`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  activateContract: (contractId: string, legalEntityId: string, _version?: number | undefined, signedFileRef?: string | null | undefined): Promise<EmploymentContractView> =>
    httpClient<EmploymentContractView>(`${BASE}/contracts/${encodeURIComponent(contractId)}/activate?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify({ signedFileRef: signedFileRef ?? null })
    }),

  listContractAmendments: (contractId: string, legalEntityId: string): Promise<readonly ContractAmendmentView[]> =>
    httpClient<readonly ContractAmendmentView[]>(`${BASE}/contracts/${encodeURIComponent(contractId)}/amendments?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createContractAmendment: (contractId: string, legalEntityId: string, request: { effectiveDate: string; changeType?: string | undefined; amendmentType?: string | undefined; reason?: string | undefined; newTerms?: unknown; termsSummary?: string | undefined; fileRef?: string | null | undefined }): Promise<ContractAmendmentView> => {
    const changeType = request.changeType ?? request.amendmentType ?? "Renewal";
    const reason = request.reason ?? request.termsSummary ?? "Contract Amendment";
    const newTerms = request.newTerms ?? { summary: request.termsSummary ?? "" };
    return httpClient<ContractAmendmentView>(`${BASE}/contracts/${encodeURIComponent(contractId)}/amendments?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify({
        effectiveDate: request.effectiveDate,
        changeType,
        newTerms,
        reason,
        fileRef: request.fileRef ?? null
      })
    });
  },

  // Compensation
  listCompensationAgreements: (employeeId: string, employmentId: string, legalEntityId: string): Promise<readonly CompensationAgreementView[]> =>
    httpClient<readonly CompensationAgreementView[]>(`${BASE}/employments/${encodeURIComponent(employmentId)}/compensation?employeeId=${encodeURIComponent(employeeId)}&legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createCompensationAgreement: (employeeId: string, employmentId: string, request: CreateCompensationAgreementRequest): Promise<CompensationAgreementView> =>
    httpClient<CompensationAgreementView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/employments/${encodeURIComponent(employmentId)}/compensation`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Insurance
  listInsuranceEnrollments: (employeeId: string, employmentId: string, legalEntityId: string): Promise<readonly InsuranceEnrollmentView[]> =>
    httpClient<readonly InsuranceEnrollmentView[]>(`${BASE}/employees/${encodeURIComponent(employeeId)}/employments/${encodeURIComponent(employmentId)}/insurance?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createInsuranceEnrollment: (employeeId: string, employmentId: string, request: CreateInsuranceEnrollmentRequest): Promise<InsuranceEnrollmentView> =>
    httpClient<InsuranceEnrollmentView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/employments/${encodeURIComponent(employmentId)}/insurance`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  activateInsuranceEnrollment: (employeeId: string, employmentId: string, enrollmentId: string, legalEntityId: string, version: number): Promise<InsuranceEnrollmentView> =>
    httpClient<InsuranceEnrollmentView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/employments/${encodeURIComponent(employmentId)}/insurance/${encodeURIComponent(enrollmentId)}/activate?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify({ expectedVersion: version })
    }),

  revealInsuranceMemberNumber: (employeeId: string, employmentId: string, enrollmentId: string, legalEntityId: string): Promise<{ memberNumber: string }> =>
    httpClient<{ memberNumber: string }>(`${BASE}/employees/${encodeURIComponent(employeeId)}/employments/${encodeURIComponent(employmentId)}/insurance/${encodeURIComponent(enrollmentId)}/reveal?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST"
    }),

  // Digital Employee File Documents
  listDocuments: (employeeId: string, legalEntityId: string): Promise<readonly EmployeeDocumentView[]> =>
    httpClient<readonly EmployeeDocumentView[]>(`${BASE}/employees/${encodeURIComponent(employeeId)}/documents?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  getDocumentCompleteness: (employeeId: string, legalEntityId: string): Promise<DocumentCompletenessView> =>
    httpClient<DocumentCompletenessView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/document-completeness?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  listDocumentRequirements: (legalEntityId: string): Promise<readonly DocumentRequirementView[]> =>
    httpClient<readonly DocumentRequirementView[]>(`${BASE}/document-requirements?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createDocumentUploadIntent: (employeeId: string, request: CreateEmployeeDocumentUploadRequest): Promise<EmployeeDocumentUploadIntentView> =>
    httpClient<EmployeeDocumentUploadIntentView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/documents/upload-intents`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  completeDocumentUpload: (employeeId: string, fileRef: string, request: CompleteEmployeeDocumentUploadRequest): Promise<{ fileRef: string; sha256: string; contentType: string; sizeBytes: number; ready: boolean }> =>
    httpClient<{ fileRef: string; sha256: string; contentType: string; sizeBytes: number; ready: boolean }>(`${BASE}/employees/${encodeURIComponent(employeeId)}/documents/uploads/${encodeURIComponent(fileRef)}/complete`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createDocument: (employeeId: string, request: { legalEntityId: string; category: string; fileRef: string; sensitivity?: string | undefined; expiryDate?: string | null | undefined; supersedesDocumentId?: string | null | undefined; employmentId?: string | null | undefined; titleAr?: string | undefined; titleEn?: string | null | undefined; contentType?: string | undefined; sizeBytes?: number | undefined; isSensitive?: boolean | undefined }): Promise<EmployeeDocumentView> => {
    const sensitivity = request.sensitivity ?? (request.isSensitive ? "Confidential" : "Normal");
    return httpClient<EmployeeDocumentView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/documents`, {
      method: "POST",
      body: JSON.stringify({
        legalEntityId: request.legalEntityId,
        category: request.category,
        fileRef: request.fileRef,
        sensitivity,
        expiryDate: request.expiryDate || null,
        supersedesDocumentId: request.supersedesDocumentId || null,
        employmentId: request.employmentId || null
      })
    });
  },

  getDocumentDownload: (employeeId: string, documentId: string, legalEntityId: string): Promise<EmployeeDocumentDownloadView> =>
    httpClient<EmployeeDocumentDownloadView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/documents/${encodeURIComponent(documentId)}/download?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  verifyDocument: (employeeId: string, documentId: string, legalEntityId: string, request: { expectedVersion: number; verified?: boolean | undefined; isVerified?: boolean | undefined; reason?: string | undefined; notes?: string | null | undefined }): Promise<EmployeeDocumentView> => {
    const verified = request.verified ?? request.isVerified ?? true;
    const reason = request.reason ?? request.notes ?? "Document verified by HR";
    return httpClient<EmployeeDocumentView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/documents/${encodeURIComponent(documentId)}/verify?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify({
        expectedVersion: request.expectedVersion,
        verified,
        reason
      })
    });
  },

  // Sensitive Identifiers
  listIdentifiers: (employeeId: string, legalEntityId: string): Promise<readonly EmployeeIdentifierView[]> =>
    httpClient<readonly EmployeeIdentifierView[]>(`${BASE}/employees/${encodeURIComponent(employeeId)}/identifiers?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createIdentifier: (employeeId: string, request: { legalEntityId: string; identifierType: string; rawValue: string; isPrimary?: boolean | undefined; issueDate?: string | null | undefined; expiryDate?: string | null | undefined }): Promise<EmployeeIdentifierView> =>
    httpClient<EmployeeIdentifierView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/identifiers`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  revealIdentifier: (employeeId: string, identifierId: string, legalEntityId: string): Promise<{ rawValue: string }> =>
    httpClient<{ rawValue: string }>(`${BASE}/employees/${encodeURIComponent(employeeId)}/identifiers/${encodeURIComponent(identifierId)}/reveal?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST"
    }),

  // Qualifications & Credentials
  listQualifications: (employeeId: string, legalEntityId: string): Promise<readonly QualificationView[]> =>
    httpClient<readonly QualificationView[]>(`${BASE}/employees/${encodeURIComponent(employeeId)}/qualifications?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createQualification: (employeeId: string, request: { legalEntityId: string; level: string; titleAr: string; titleEn?: string | null | undefined; institution: string; graduationYear?: number | null | undefined; evidenceFileRef?: string | null | undefined }): Promise<QualificationView> =>
    httpClient<QualificationView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/qualifications`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listCredentials: (employeeId: string, legalEntityId: string): Promise<readonly CredentialView[]> =>
    httpClient<readonly CredentialView[]>(`${BASE}/employees/${encodeURIComponent(employeeId)}/credentials?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createCredential: (employeeId: string, request: { legalEntityId: string; credentialType: string; nameAr: string; nameEn?: string | null | undefined; issuingAuthority: string; issueDate: string; expiryDate?: string | null | undefined; evidenceFileRef?: string | null | undefined }): Promise<CredentialView> =>
    httpClient<CredentialView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/credentials`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  verifyCredential: (employeeId: string, credentialId: string, legalEntityId: string, version: number): Promise<CredentialView> =>
    httpClient<CredentialView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/credentials/${encodeURIComponent(credentialId)}/verify?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify({ expectedVersion: version })
    }),

  // Attendance, Punches, Exceptions, Corrections
  listAttendanceRecords: (params: { legalEntityId: string; from: string; to: string; employmentId?: string | null | undefined; status?: string | null | undefined; page?: number | null | undefined; pageSize?: number | null | undefined }): Promise<AttendancePage> => {
    const searchParams = new URLSearchParams({
      legalEntityId: params.legalEntityId,
      from: params.from,
      to: params.to
    });
    if (params.employmentId) searchParams.set("employmentId", params.employmentId);
    if (params.status) searchParams.set("status", params.status);
    if (params.page != null) searchParams.set("page", String(params.page));
    if (params.pageSize != null) searchParams.set("pageSize", String(params.pageSize));
    return httpClient<AttendancePage>(`${BASE}/attendance-records?${searchParams.toString()}`);
  },

  recordPunch: (employmentId: string, request: CreateAttendancePunchRequest): Promise<{ punchId: string; duplicateSubmission: boolean; lateAfterPayrollLock: boolean; record: AttendanceRecordView }> =>
    httpClient<{ punchId: string; duplicateSubmission: boolean; lateAfterPayrollLock: boolean; record: AttendanceRecordView }>(`${BASE}/employments/${encodeURIComponent(employmentId)}/attendance/punches`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listAttendanceExceptions: (legalEntityId: string): Promise<readonly AttendanceExceptionView[]> =>
    httpClient<readonly AttendanceExceptionView[]>(`${BASE}/attendance-exceptions?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  correctAttendance: (recordId: string, legalEntityId: string, request: AttendanceCorrectionRequest): Promise<AttendanceCorrectionView> =>
    httpClient<AttendanceCorrectionView>(`${BASE}/attendance-records/${encodeURIComponent(recordId)}/corrections?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Leave Management
  listLeaveTypes: (legalEntityId: string): Promise<readonly LeaveTypeView[]> =>
    httpClient<readonly LeaveTypeView[]>(`${BASE}/leave/types?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  listLeavePolicies: (leaveTypeId: string, legalEntityId: string): Promise<readonly LeavePolicyView[]> =>
    httpClient<readonly LeavePolicyView[]>(`${BASE}/leave/types/${encodeURIComponent(leaveTypeId)}/policies?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  getLeaveBalance: (employmentId: string, leaveTypeId: string, legalEntityId: string): Promise<LeaveBalanceView> =>
    httpClient<LeaveBalanceView>(`${BASE}/employments/${encodeURIComponent(employmentId)}/leave-types/${encodeURIComponent(leaveTypeId)}/balance?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  listLeaveRequests: (params: { legalEntityId: string; employmentId?: string | null | undefined; status?: string | null | undefined; page?: number | undefined; pageSize?: number | undefined }): Promise<readonly LeaveRequestView[]> => {
    const searchParams = new URLSearchParams({ legalEntityId: params.legalEntityId });
    if (params.employmentId) searchParams.set("employmentId", params.employmentId);
    if (params.status) searchParams.set("status", params.status);
    searchParams.set("page", String(params.page ?? 1));
    searchParams.set("pageSize", String(params.pageSize ?? 50));
    return httpClient<readonly LeaveRequestView[]>(`${BASE}/leave/requests?${searchParams.toString()}`);
  },

  createLeaveRequest: (employmentId: string, request: CreateLeaveRequest): Promise<LeaveRequestView> =>
    httpClient<LeaveRequestView>(`${BASE}/employments/${encodeURIComponent(employmentId)}/leave-requests`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  approveLeaveRequest: (requestId: string, legalEntityId: string, decision: LeaveRequestDecision): Promise<LeaveRequestView> =>
    httpClient<LeaveRequestView>(`${BASE}/leave/requests/${encodeURIComponent(requestId)}/approve?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(decision)
    }),

  rejectLeaveRequest: (requestId: string, legalEntityId: string, decision: LeaveRequestDecision): Promise<LeaveRequestView> =>
    httpClient<LeaveRequestView>(`${BASE}/leave/requests/${encodeURIComponent(requestId)}/reject?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(decision)
    }),

  cancelLeaveRequest: (requestId: string, legalEntityId: string, decision: LeaveRequestDecision): Promise<LeaveRequestView> =>
    httpClient<LeaveRequestView>(`${BASE}/leave/requests/${encodeURIComponent(requestId)}/cancel?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(decision)
    }),

  adjustLeaveBalance: (request: AdjustLeaveBalanceRequest): Promise<void> =>
    httpClient<void>(`${BASE}/leave/balance-adjustments`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Adjustments & Allowances / Disciplinary
  listAdjustments: (employmentId: string, legalEntityId: string): Promise<readonly HrAdjustmentView[]> =>
    httpClient<readonly HrAdjustmentView[]>(`${BASE}/employments/${encodeURIComponent(employmentId)}/adjustments?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createAdjustment: (employeeId: string, employmentId: string, request: CreateHrAdjustmentRequest): Promise<HrAdjustmentView> =>
    httpClient<HrAdjustmentView>(`${BASE}/employees/${encodeURIComponent(employeeId)}/employments/${encodeURIComponent(employmentId)}/adjustments`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  submitAdjustment: (adjustmentId: string, legalEntityId: string, version: number): Promise<HrAdjustmentView> =>
    httpClient<HrAdjustmentView>(`${BASE}/adjustments/${encodeURIComponent(adjustmentId)}/submit?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify({ expectedVersion: version })
    }),

  approveAdjustment: (adjustmentId: string, legalEntityId: string, version: number, reason?: string | null | undefined): Promise<HrAdjustmentView> =>
    httpClient<HrAdjustmentView>(`${BASE}/adjustments/${encodeURIComponent(adjustmentId)}/approve?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify({ expectedVersion: version, reason })
    }),

  // Shifts & Calendars
  listShifts: (legalEntityId: string): Promise<readonly ShiftDefinitionView[]> =>
    httpClient<readonly ShiftDefinitionView[]>(`${BASE}/shifts?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createShift: (request: CreateShiftDefinitionRequest): Promise<ShiftDefinitionView> =>
    httpClient<ShiftDefinitionView>(`${BASE}/shifts`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listShiftAssignments: (employmentId: string, legalEntityId: string): Promise<readonly ShiftAssignmentView[]> =>
    httpClient<readonly ShiftAssignmentView[]>(`${BASE}/employments/${encodeURIComponent(employmentId)}/shift-assignments?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  assignShift: (employmentId: string, request: AssignShiftRequest): Promise<ShiftAssignmentView> =>
    httpClient<ShiftAssignmentView>(`${BASE}/employments/${encodeURIComponent(employmentId)}/shift-assignments`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listWorkCalendars: (legalEntityId: string): Promise<readonly WorkCalendarView[]> =>
    httpClient<readonly WorkCalendarView[]>(`${BASE}/work-calendars?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createWorkCalendar: (request: CreateWorkCalendarRequest): Promise<WorkCalendarView> =>
    httpClient<WorkCalendarView>(`${BASE}/work-calendars`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listHolidays: (calendarId: string): Promise<readonly WorkCalendarHolidayView[]> =>
    httpClient<readonly WorkCalendarHolidayView[]>(`${BASE}/work-calendars/${encodeURIComponent(calendarId)}/holidays`),

  addHoliday: (calendarId: string, request: AddWorkCalendarHolidayRequest): Promise<WorkCalendarHolidayView> =>
    httpClient<WorkCalendarHolidayView>(`${BASE}/work-calendars/${encodeURIComponent(calendarId)}/holidays`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Timeline
  getTimeline: (employeeId: string, legalEntityId: string): Promise<readonly TimelineEventView[]> =>
    httpClient<readonly TimelineEventView[]>(`${BASE}/employees/${encodeURIComponent(employeeId)}/timeline?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  // Onboarding & Offboarding
  getOnboardingCase: (employmentId: string, legalEntityId: string): Promise<OnboardingCaseView | null> =>
    httpClient<OnboardingCaseView | null>(`${BASE}/employments/${encodeURIComponent(employmentId)}/onboarding?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  getOffboardingCase: (employeeId: string, employmentId: string, legalEntityId: string): Promise<OffboardingCaseView | null> =>
    httpClient<OffboardingCaseView | null>(`${BASE}/employees/${encodeURIComponent(employeeId)}/employments/${encodeURIComponent(employmentId)}/offboarding?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  // Self-Service & Manager
  getMyProfile: async (legalEntityId: string): Promise<EmployeeProfile | null> => {
    try {
      return await httpClient<EmployeeProfile>(`${BASE}/me/profile?legalEntityId=${encodeURIComponent(legalEntityId)}`);
    } catch (error) {
      // An unlinked membership is the supported self-service empty state.
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    }
  },

  getMyTeam: (legalEntityId: string): Promise<HrTeamPage> =>
    httpClient<HrTeamPage>(`${BASE}/me/team?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  getMyTeamLeaveApprovals: (legalEntityId: string): Promise<readonly LeaveRequestView[]> =>
    httpClient<readonly LeaveRequestView[]>(`${BASE}/me/team/leave-approvals?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  // Imports Preview & Commit
  previewEmployeeImport: (request: unknown): Promise<HrImportSummary> =>
    httpClient<HrImportSummary>(`${BASE}/imports/employees/preview`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  commitEmployeeImport: (request: unknown): Promise<HrImportSummary> =>
    httpClient<HrImportSummary>(`${BASE}/imports/employees/commit`, {
      method: "POST",
      body: JSON.stringify(request)
    })
};
