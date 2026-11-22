export type EmployeeStatus = "Active" | "Inactive" | "Archived";
export type EmploymentStatus = "PreHire" | "Active" | "Suspended" | "NoticePeriod" | "Terminated" | "Ended";
export type EmploymentType = "FullTime" | "PartTime" | "Temporary" | "FixedTerm" | "Probationary" | "Intern" | "Seasonal" | "Other";

export interface EmployeeListItem {
  readonly id: string;
  readonly employmentNumber?: string | null;
  readonly displayName: string;
  readonly englishName?: string | null;
  readonly status: EmployeeStatus;
  readonly currentEmploymentStartDate?: string | null;
  readonly legalEntityId?: string | null;
}

export interface EmployeePage {
  readonly items: readonly EmployeeListItem[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

export interface EmployeeDirectoryFreshness {
  readonly revision: number;
}

/**
 * Authoritative manager-team projection returned by the paginated HR contract.
 * This intentionally contains organizational context rather than an inferred
 * employee status, which is not part of the direct-report projection.
 */
export interface HrTeamMemberView {
  readonly employeeId: string;
  readonly arabicName: string;
  readonly englishName?: string | null;
  readonly employmentId: string;
  readonly employmentNumber: string;
  readonly employmentStartDate: string;
  readonly departmentId?: string | null;
  readonly departmentName?: string | null;
  readonly positionId?: string | null;
  readonly positionTitle?: string | null;
}

export interface HrTeamPage {
  readonly items: readonly HrTeamMemberView[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

export interface EmploymentView {
  readonly id: string;
  readonly employeeId: string;
  readonly legalEntityId: string;
  readonly employmentNumber: string;
  readonly type: EmploymentType;
  readonly status: EmploymentStatus;
  readonly startDate: string;
  readonly endDate?: string | null;
  readonly version: number;
}

export interface EmployeeProfile {
  readonly id: string;
  readonly arabicName: string;
  readonly englishName?: string | null;
  readonly preferredName?: string | null;
  readonly status: EmployeeStatus;
  readonly version: number;
  readonly initialEmployment?: EmploymentView | null;
}

export interface UpdateEmployeeRequest {
  readonly arabicName: string;
  readonly englishName?: string | null;
  readonly preferredName?: string | null;
  readonly expectedVersion: number;
  readonly reason: string;
}

export interface OrganizationDefinition {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn?: string | null;
  readonly parentId?: string | null;
  readonly status: string;
  readonly version: number;
}

export interface AssignmentView {
  readonly id: string;
  readonly employmentId: string;
  readonly legalEntityId: string;
  readonly branchId?: string | null;
  readonly departmentId?: string | null;
  readonly positionId?: string | null;
  readonly managerEmploymentId?: string | null;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly reasonCode: string;
  readonly version: number;
  readonly workCalendarId?: string | null;
}

export interface EmploymentContractView {
  readonly id: string;
  readonly employmentId: string;
  readonly legalEntityId: string;
  readonly contractType: string;
  readonly startDate?: string | null;
  readonly endDate?: string | null;
  readonly probationStart?: string | null;
  readonly probationEnd?: string | null;
  readonly status: string;
  readonly workingHoursBasis?: string | null;
  readonly noticePeriodTerms?: string | null;
  readonly version: number;
}

export interface ContractAmendmentView {
  readonly id: string;
  readonly contractId: string;
  readonly changeType?: string;
  readonly amendmentType?: string;
  readonly effectiveDate: string;
  readonly termsSummary?: string;
  readonly reason?: string;
  readonly previousSnapshot?: unknown;
  readonly newTerms?: unknown;
  readonly fileRef?: string | null;
  readonly createdAt: string;
}

export interface CompensationAgreementView {
  readonly id: string;
  readonly employmentId: string;
  readonly currencyCode: string;
  readonly amount: number;
  readonly payFrequency: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly status: string;
  readonly contractId?: string | null;
  readonly reason: string;
  readonly evidenceFileRef?: string | null;
  readonly version: number;
}

export interface InsuranceEnrollmentView {
  readonly id: string;
  readonly employmentId: string;
  readonly schemeCode: string;
  readonly providerCode?: string | null;
  readonly maskedMemberNumber?: string | null;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly status: string;
  readonly countryProviderVersion?: string | null;
  readonly evidenceFileRef?: string | null;
  readonly version: number;
}

export interface EmployeeDocumentView {
  readonly id: string;
  readonly employeeId: string;
  readonly category: string;
  readonly sensitivity?: string;
  readonly titleAr?: string;
  readonly titleEn?: string | null;
  readonly fileRef: string;
  readonly sha256?: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly expiryDate?: string | null;
  readonly verificationStatus?: string;
  readonly status?: string;
  readonly isVerified?: boolean;
  readonly isSensitive?: boolean;
  readonly verificationNotes?: string | null;
  readonly employmentId?: string | null;
  readonly supersedesDocumentId?: string | null;
  readonly supersededByDocumentId?: string | null;
  readonly version: number;
  readonly createdAt?: string;
}

export interface EmployeeDocumentUploadIntentView {
  readonly fileRef: string;
  readonly url: string;
  readonly fields: Record<string, string>;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly expiresAt: string;
}

export interface EmployeeDocumentDownloadView {
  readonly url: string;
  readonly expiresAt: string;
  readonly fileName?: string | null;
}

export interface CreateEmployeeDocumentUploadRequest {
  readonly legalEntityId: string;
  readonly fileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly sensitivity: string;
  readonly employmentId?: string | null;
}

export interface CompleteEmployeeDocumentUploadRequest {
  readonly legalEntityId: string;
  readonly employmentId?: string | null;
}

export interface CreateEmployeeDocumentRequest {
  readonly legalEntityId: string;
  readonly category: string;
  readonly fileRef: string;
  readonly sensitivity: string;
  readonly expiryDate?: string | null;
  readonly supersedesDocumentId?: string | null;
  readonly employmentId?: string | null;
}

export interface VerifyEmployeeDocumentRequest {
  readonly expectedVersion: number;
  readonly verified: boolean;
  readonly reason: string;
}

export interface DocumentRequirementView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly category: string;
  readonly titleAr: string;
  readonly titleEn?: string | null;
  readonly isMandatory: boolean;
  readonly employmentTypeScope?: string | null;
}

export interface DocumentCompletenessView {
  readonly employeeId: string;
  readonly totalMandatory: number;
  readonly verifiedCount: number;
  readonly isComplete: boolean;
  readonly missingCategories: readonly string[];
  readonly expiringSoonCount: number;
}

export interface QualificationView {
  readonly id: string;
  readonly employeeId: string;
  readonly level: string;
  readonly titleAr: string;
  readonly titleEn?: string | null;
  readonly institution: string;
  readonly graduationYear?: number | null;
  readonly evidenceFileRef?: string | null;
  readonly version: number;
}

export interface CredentialView {
  readonly id: string;
  readonly employeeId: string;
  readonly credentialType: string;
  readonly nameAr: string;
  readonly nameEn?: string | null;
  readonly issuingAuthority: string;
  readonly issueDate: string;
  readonly expiryDate?: string | null;
  readonly isVerified: boolean;
  readonly evidenceFileRef?: string | null;
  readonly version: number;
}

export interface EmployeeIdentifierView {
  readonly id: string;
  readonly employeeId: string;
  readonly identifierType: string;
  readonly maskedValue: string;
  readonly isPrimary: boolean;
  readonly issueDate?: string | null;
  readonly expiryDate?: string | null;
  readonly status: string;
  readonly version: number;
}

export interface AttendanceRecordView {
  readonly id: string;
  readonly employeeId: string;
  readonly employmentId: string;
  readonly legalEntityId: string;
  readonly workDate: string;
  readonly workCalendarId?: string | null;
  readonly shiftAssignmentId?: string | null;
  readonly timeZoneId: string;
  readonly plannedStart?: string | null;
  readonly plannedEnd?: string | null;
  readonly plannedBreakMinutes: number;
  readonly graceMinutes: number;
  readonly firstIn?: string | null;
  readonly lastOut?: string | null;
  readonly workedMinutes: number;
  readonly lateMinutes: number;
  readonly earlyLeaveMinutes: number;
  readonly overtimeEligibleMinutes: number;
  readonly status: string;
  readonly lockState: string;
  readonly payrollReference?: string | null;
  readonly version: number;
}

export interface AttendancePage {
  readonly items: readonly AttendanceRecordView[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

export interface AttendanceExceptionView {
  readonly id: string;
  readonly employmentId: string;
  readonly attendanceRecordId?: string | null;
  readonly attendancePunchId?: string | null;
  readonly code: string;
  readonly status: string;
  readonly reason?: string | null;
  readonly createdAt: string;
}

export interface AttendanceCorrectionView {
  readonly id: string;
  readonly attendanceRecordId: string;
  readonly status: string;
  readonly reasonCode: string;
  readonly reason: string;
  readonly correctedBy: string;
  readonly createdAt: string;
}

export interface LeaveTypeView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn?: string | null;
  readonly category: string;
  readonly unit: string;
  readonly countryCode?: string | null;
  readonly countryProviderCode?: string | null;
  readonly status: string;
}

export interface LeavePolicyView {
  readonly id: string;
  readonly leaveTypeId: string;
  readonly version: number;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly countingMethod: string;
  readonly approvalRequired: boolean;
  readonly partialDayAllowed: boolean;
  readonly negativeBalancePolicy: string;
  readonly negativeBalanceLimit: number;
  readonly payrollTreatment: string;
  readonly accrualMode: string;
  readonly accrualAmount?: number | null;
  readonly carryoverLimit?: number | null;
  readonly carryoverExpiryMonths?: number | null;
  readonly eligibleEmploymentTypes: readonly string[];
  readonly countryProviderCode?: string | null;
}

export interface LeaveBalanceMovementView {
  readonly id: string;
  readonly effectiveDate: string;
  readonly type: string;
  readonly quantity: number;
  readonly sourceType: string;
  readonly sourceReference: string;
  readonly reason: string;
  readonly createdAt: string;
  readonly expiresOn?: string | null;
}

export interface LeaveBalanceView {
  readonly employmentId: string;
  readonly leaveTypeId: string;
  readonly available: number;
  readonly recentMovements: readonly LeaveBalanceMovementView[];
}

export interface LeaveRequestView {
  readonly id: string;
  readonly employeeId: string;
  readonly employmentId: string;
  readonly leaveTypeId: string;
  readonly policyId: string;
  readonly from: string;
  readonly to: string;
  readonly chargedQuantity: number;
  readonly status: string;
  readonly version: number;
  readonly reason?: string | null;
  readonly createdAt: string;
  readonly submittedAt?: string | null;
  readonly decidedBy?: string | null;
  readonly decidedAt?: string | null;
  readonly decisionReason?: string | null;
}

export interface HrAdjustmentView {
  readonly id: string;
  readonly employeeId: string;
  readonly employmentId: string;
  readonly kind: string;
  readonly adjustmentType: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly recurrence: string;
  readonly payrollImpact: string;
  readonly amount?: number | null;
  readonly currencyCode?: string | null;
  readonly reasonCode: string;
  readonly reason: string;
  readonly evidenceFileRef?: string | null;
  readonly status: string;
  readonly version: number;
  readonly createdAt: string;
  readonly approvedBy?: string | null;
  readonly approvedAt?: string | null;
  readonly decisionReason?: string | null;
}

export interface ShiftDefinitionView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn?: string | null;
  readonly startLocal: string;
  readonly endLocal: string;
  readonly isOvernight: boolean;
  readonly breakMinutes: number;
  readonly graceMinutes: number;
  readonly status: string;
  readonly version: number;
}

export interface ShiftAssignmentView {
  readonly id: string;
  readonly employmentId: string;
  readonly shiftDefinitionId: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly shiftName: string;
  readonly startLocal: string;
  readonly endLocal: string;
  readonly isOvernight: boolean;
  readonly breakMinutes: number;
  readonly graceMinutes: number;
}

export interface WorkCalendarView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn?: string | null;
  readonly timeZoneId: string;
  readonly status: string;
  readonly version: number;
}

export interface WorkCalendarHolidayView {
  readonly id: string;
  readonly calendarId: string;
  readonly date: string;
  readonly nameAr: string;
  readonly nameEn?: string | null;
  readonly sourceCode: string;
  readonly status: string;
  readonly version: number;
}

export interface TimelineEventView {
  readonly id: string;
  readonly employeeId: string;
  readonly employmentId?: string | null;
  readonly eventType: string;
  readonly effectiveOn: string;
  readonly occurredAt: string;
  readonly payload: Record<string, unknown>;
}

export interface HrHomeReminder {
  readonly id: string;
  readonly category: string;
  readonly title: string;
  readonly employeeId?: string | null;
  readonly employeeName?: string | null;
  readonly dueDate: string;
  readonly severity: "info" | "warning" | "danger";
}

export interface HrHomeSummary {
  readonly headcount?: number;
  readonly activeEmployeesCount?: number;
  readonly attendanceRecordsToday?: number;
  readonly presentToday?: number;
  readonly absentToday?: number;
  readonly openAttendanceExceptions?: number;
  readonly missingAttendanceCount?: number;
  readonly pendingLeaveApprovals?: number;
  readonly pendingLeaveApprovalsCount?: number;
  readonly documentsExpiringWithin30Days?: number;
  readonly expiringDocumentsCount?: number;
  readonly contractsExpiringWithin30Days?: number;
  readonly expiringContractsCount?: number;
  readonly newHiresWithin30Days?: number;
  readonly employeesInNoticePeriod?: number;
  readonly pendingOvertimeApprovals?: number;
  readonly payrollWarnings?: number;
  readonly reminders?: readonly HrHomeReminder[];
}

export interface OnboardingCaseView {
  readonly id: string;
  readonly employmentId: string;
  readonly legalEntityId: string;
  readonly titleAr: string;
  readonly status: string;
  readonly progressPercentage: number;
  readonly tasks: readonly OnboardingTaskView[];
}

export interface OnboardingTaskView {
  readonly id: string;
  readonly code: string;
  readonly titleAr: string;
  readonly titleEn?: string | null;
  readonly isRequired: boolean;
  readonly status: string;
  readonly completedAt?: string | null;
  readonly notes?: string | null;
  readonly version: number;
}

export interface OffboardingCaseView {
  readonly id: string;
  readonly employmentId: string;
  readonly legalEntityId: string;
  readonly reasonType: string;
  readonly reasonCode: string;
  readonly noticeDate?: string | null;
  readonly lastWorkingDate: string;
  readonly effectiveEndDate: string;
  readonly accessDeactivationOn: string;
  readonly finalPayrollRequired: boolean;
  readonly status: string;
  readonly terminationStatus: string;
  readonly version: number;
  readonly tasks: readonly OffboardingTaskView[];
}

export interface OffboardingTaskView {
  readonly id: string;
  readonly code: string;
  readonly titleAr: string;
  readonly dueDate?: string | null;
  readonly externalReference?: string | null;
  readonly status: string;
  readonly version: number;
}

// Creation and Command Requests
export interface CreateEmployeeRequest {
  readonly arabicName: string;
  readonly englishName?: string | null;
  readonly preferredName?: string | null;
  readonly membershipId?: string | null;
  readonly legalEntityId?: string | null;
  readonly employmentType?: EmploymentType | null;
  readonly startDate?: string | null;
  readonly endDate?: string | null;
}

export interface CreateEmploymentRequest {
  readonly legalEntityId: string;
  readonly employmentNumber?: string | null;
  readonly type: EmploymentType;
  readonly startDate: string;
  readonly endDate?: string | null;
}

export interface ChangeEmploymentStatusRequest {
  readonly newStatus: EmploymentStatus;
  readonly effectiveOn: string;
  readonly reasonCode: string;
  readonly expectedVersion: number;
  readonly offboarding?: {
    readonly reasonType: string;
    readonly noticeDate?: string | null;
    readonly lastWorkingDate: string;
    readonly finalPayrollRequired: boolean;
    readonly accessDeactivationTiming: string;
    readonly tasks?: readonly {
      readonly code: string;
      readonly titleAr: string;
      readonly dueDate?: string | null;
    }[];
  } | null;
}

export interface CreateAssignmentRequest {
  readonly legalEntityId: string;
  readonly branchId?: string | null;
  readonly departmentId?: string | null;
  readonly positionId?: string | null;
  readonly managerEmploymentId?: string | null;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly reasonCode: string;
  readonly workCalendarId?: string | null;
}

export interface CreateEmploymentContractRequest {
  readonly legalEntityId: string;
  readonly contractType: string;
  readonly startDate?: string | null;
  readonly endDate?: string | null;
  readonly probationStart?: string | null;
  readonly probationEnd?: string | null;
  readonly workingHoursBasis?: string | null;
  readonly noticePeriodTerms?: string | null;
}

export interface CreateCompensationAgreementRequest {
  readonly legalEntityId: string;
  readonly currencyCode: string;
  readonly amount: number;
  readonly payFrequency: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly contractId?: string | null;
  readonly reason?: string;
  readonly evidenceFileRef?: string | null;
}

export interface CreateInsuranceEnrollmentRequest {
  readonly legalEntityId: string;
  readonly schemeCode: string;
  readonly providerCode?: string | null;
  readonly memberNumber?: string | null;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly evidenceFileRef?: string | null;
}

export interface CreateAttendancePunchRequest {
  readonly legalEntityId: string;
  readonly occurredAt: string;
  readonly direction: "In" | "Out";
  readonly sourceType: string;
  readonly sourceReference?: string | null;
  readonly idempotencyKey: string;
}

export interface AttendanceCorrectionRequest {
  readonly expectedVersion: number;
  readonly firstIn?: string | null;
  readonly lastOut?: string | null;
  readonly reasonCode: string;
  readonly reason: string;
}

export interface CreateLeaveRequest {
  readonly legalEntityId: string;
  readonly leaveTypeId: string;
  readonly from: string;
  readonly to: string;
  readonly idempotencyKey: string;
  readonly requestedQuantity?: number | null;
  readonly reason?: string | null;
}

export interface LeaveRequestDecision {
  readonly expectedVersion: number;
  readonly reason?: string | null;
}

export interface AdjustLeaveBalanceRequest {
  readonly legalEntityId: string;
  readonly employmentId: string;
  readonly leaveTypeId: string;
  readonly effectiveDate: string;
  readonly quantity: number;
  readonly reason: string;
  readonly idempotencyKey: string;
}

export interface CreateHrAdjustmentRequest {
  readonly legalEntityId: string;
  readonly kind: "Allowance" | "DisciplinaryAction";
  readonly adjustmentType: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly recurrence: string;
  readonly payrollImpact: "None" | "SendToPayroll";
  readonly amount?: number | null;
  readonly currencyCode?: string | null;
  readonly reasonCode: string;
  readonly reason: string;
  readonly evidenceFileRef?: string | null;
}

export interface CreateDepartmentRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn?: string | null;
  readonly parentId?: string | null;
}

export interface CreatePositionRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly titleAr: string;
  readonly titleEn?: string | null;
}

export interface CreateShiftDefinitionRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn?: string | null;
  readonly startLocal: string;
  readonly endLocal: string;
  readonly isOvernight: boolean;
  readonly breakMinutes: number;
  readonly graceMinutes: number;
}

export interface AssignShiftRequest {
  readonly legalEntityId: string;
  readonly shiftDefinitionId: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
}

export interface CreateWorkCalendarRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn?: string | null;
  readonly timeZoneId: string;
  readonly weeklyPatterns: readonly {
    readonly dayOfWeek: number;
    readonly isWorkingDay: boolean;
    readonly localStart?: string | null;
    readonly localEnd?: string | null;
    readonly breakMinutes: number;
  }[];
}

export interface AddWorkCalendarHolidayRequest {
  readonly date: string;
  readonly nameAr: string;
  readonly nameEn?: string | null;
  readonly sourceCode: string;
}

export interface HrImportSummary {
  readonly totalRows: number;
  readonly importedRows: number;
  readonly alreadyImportedRows: number;
  readonly rejectedRows: number;
  readonly rows: readonly {
    readonly rowReference: string;
    readonly status: string;
    readonly issues: readonly string[];
    readonly warnings?: readonly string[] | null;
  }[];
}
