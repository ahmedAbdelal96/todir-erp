import { useEffect, useRef } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { hrApi } from "../services/hr-api.ts";
import type {
  CreateEmployeeRequest,
  UpdateEmployeeRequest,
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

export const hrKeys = {
  all: ["hr"] as const,
  home: (legalEntityId: string) => [...hrKeys.all, "home", legalEntityId] as const,
  employees: () => [...hrKeys.all, "employees"] as const,
  employeeList: (params?: { search?: string | null | undefined; legalEntityId?: string | null | undefined; page?: number | null | undefined; pageSize?: number | null | undefined } | undefined) =>
    [...hrKeys.employees(), "list", params] as const,
  employeeDetail: (id: string, legalEntityId?: string) => [...hrKeys.employees(), "detail", id, legalEntityId] as const,
  employments: (employeeId: string, legalEntityId: string) => [...hrKeys.all, "employments", employeeId, legalEntityId] as const,
  departments: (legalEntityId: string) => [...hrKeys.all, "departments", legalEntityId] as const,
  positions: (legalEntityId: string) => [...hrKeys.all, "positions", legalEntityId] as const,
  assignments: (employmentId: string, legalEntityId: string) => [...hrKeys.all, "assignments", employmentId, legalEntityId] as const,
  contracts: (employmentId: string, legalEntityId: string) => [...hrKeys.all, "contracts", employmentId, legalEntityId] as const,
  contractAmendments: (contractId: string, legalEntityId: string) => [...hrKeys.all, "contract-amendments", contractId, legalEntityId] as const,
  compensation: (employmentId: string, legalEntityId: string) => [...hrKeys.all, "compensation", employmentId, legalEntityId] as const,
  insurance: (employeeId: string, employmentId: string, legalEntityId: string) => [...hrKeys.all, "insurance", employeeId, employmentId, legalEntityId] as const,
  documents: (employeeId: string, legalEntityId: string) => [...hrKeys.all, "documents", employeeId, legalEntityId] as const,
  documentCompleteness: (employeeId: string, legalEntityId: string) => [...hrKeys.all, "document-completeness", employeeId, legalEntityId] as const,
  documentRequirements: (legalEntityId: string) => [...hrKeys.all, "document-requirements", legalEntityId] as const,
  identifiers: (employeeId: string, legalEntityId: string) => [...hrKeys.all, "identifiers", employeeId, legalEntityId] as const,
  qualifications: (employeeId: string, legalEntityId: string) => [...hrKeys.all, "qualifications", employeeId, legalEntityId] as const,
  credentials: (employeeId: string, legalEntityId: string) => [...hrKeys.all, "credentials", employeeId, legalEntityId] as const,
  attendance: (params: { legalEntityId: string; from: string; to: string; employmentId?: string | null; status?: string | null; page?: number | null; pageSize?: number | null }) =>
    [...hrKeys.all, "attendance", params] as const,
  attendanceExceptions: (legalEntityId: string) => [...hrKeys.all, "attendance-exceptions", legalEntityId] as const,
  leaveTypes: (legalEntityId: string) => [...hrKeys.all, "leave-types", legalEntityId] as const,
  leavePolicies: (leaveTypeId: string, legalEntityId: string) => [...hrKeys.all, "leave-policies", leaveTypeId, legalEntityId] as const,
  leaveBalance: (employmentId: string, leaveTypeId: string, legalEntityId: string) => [...hrKeys.all, "leave-balance", employmentId, leaveTypeId, legalEntityId] as const,
  leaveRequests: (legalEntityId: string, employmentId?: string | null) => [...hrKeys.all, "leave-requests", legalEntityId, employmentId] as const,
  adjustments: (employmentId: string, legalEntityId: string) => [...hrKeys.all, "adjustments", employmentId, legalEntityId] as const,
  shifts: (legalEntityId: string) => [...hrKeys.all, "shifts", legalEntityId] as const,
  shiftAssignments: (employmentId: string, legalEntityId: string) => [...hrKeys.all, "shift-assignments", employmentId, legalEntityId] as const,
  workCalendars: (legalEntityId: string) => [...hrKeys.all, "work-calendars", legalEntityId] as const,
  holidays: (calendarId: string) => [...hrKeys.all, "holidays", calendarId] as const,
  timeline: (employeeId: string, legalEntityId: string) => [...hrKeys.all, "timeline", employeeId, legalEntityId] as const,
  onboarding: (employmentId: string, legalEntityId: string) => [...hrKeys.all, "onboarding", employmentId, legalEntityId] as const,
  offboarding: (employeeId: string, employmentId: string, legalEntityId: string) => [...hrKeys.all, "offboarding", employeeId, employmentId, legalEntityId] as const,
  myProfile: (legalEntityId: string) => [...hrKeys.all, "my-profile", legalEntityId] as const,
  myTeam: (legalEntityId: string) => [...hrKeys.all, "my-team", legalEntityId] as const,
  myTeamLeaveApprovals: (legalEntityId: string) => [...hrKeys.all, "my-team-leave-approvals", legalEntityId] as const
};

// Hooks

export function useHrHomeSummary(legalEntityId: string, asOfDate?: string) {
  return useQuery({
    queryKey: hrKeys.home(legalEntityId),
    queryFn: () => hrApi.getHomeSummary(legalEntityId, asOfDate),
    enabled: Boolean(legalEntityId)
  });
}

export function useEmployees(params?: { search?: string | null | undefined; legalEntityId?: string | null | undefined; page?: number | null | undefined; pageSize?: number | null | undefined } | undefined) {
  useHrDirectoryFreshness(params?.legalEntityId ?? undefined);
  return useQuery({
    queryKey: hrKeys.employeeList(params),
    queryFn: () => hrApi.listEmployees(params),
    // The backend's HR directory cache is revisioned and permission-scoped;
    // keep the browser window aligned with that bounded-freshness contract.
    staleTime: 15_000,
    gcTime: 5 * 60_000
  });
}

export function useEmployeeDetail(id: string, legalEntityId?: string) {
  useHrDirectoryFreshness(legalEntityId);
  return useQuery({
    queryKey: hrKeys.employeeDetail(id, legalEntityId),
    queryFn: () => hrApi.getEmployee(id, legalEntityId),
    enabled: Boolean(id),
    staleTime: 15_000,
    gcTime: 5 * 60_000
  });
}

export function useHrDirectoryFreshness(legalEntityId?: string, branchId?: string) {
  const queryClient = useQueryClient();
  const previousRevision = useRef<number | null>(null);
  const query = useQuery({
    queryKey: [...hrKeys.employees(), "freshness", legalEntityId ?? null, branchId ?? null],
    queryFn: () => hrApi.getEmployeeDirectoryFreshness(legalEntityId, branchId),
    // Tenant-wide HR views are a supported scope when no entity is selected.
    enabled: true,
    staleTime: 0,
    refetchInterval: 10_000,
    refetchIntervalInBackground: false,
    gcTime: 60_000
  });

  useEffect(() => {
    const revision = query.data?.revision;
    if (revision === undefined) return;
    if (previousRevision.current !== null && revision !== previousRevision.current)
      void queryClient.invalidateQueries({ queryKey: hrKeys.employees() });
    previousRevision.current = revision;
  }, [query.data?.revision, queryClient]);

  return query;
}

export function useCreateEmployeeMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateEmployeeRequest) => hrApi.createEmployee(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.employees() });
    }
  });
}

export function useUpdateEmployeeMutation(employeeId: string, legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateEmployeeRequest) => hrApi.updateEmployee(employeeId, legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.employees() });
      queryClient.invalidateQueries({ queryKey: hrKeys.employeeDetail(employeeId, legalEntityId) });
    }
  });
}

export function useEmployments(employeeId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.employments(employeeId, legalEntityId),
    queryFn: () => hrApi.listEmployments(employeeId, legalEntityId),
    enabled: Boolean(employeeId && legalEntityId)
  });
}

export function useCreateEmploymentMutation(employeeId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateEmploymentRequest) => hrApi.createEmployment(employeeId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.employees() });
      queryClient.invalidateQueries({ queryKey: hrKeys.employments(employeeId, legalEntityId) });
      queryClient.invalidateQueries({ queryKey: hrKeys.employeeDetail(employeeId, legalEntityId) });
    }
  });
}

export function useChangeEmploymentStatusMutation(employeeId: string, employmentId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ChangeEmploymentStatusRequest) => hrApi.changeEmploymentStatus(employeeId, employmentId, legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.employees() });
      queryClient.invalidateQueries({ queryKey: hrKeys.employments(employeeId, legalEntityId) });
      queryClient.invalidateQueries({ queryKey: hrKeys.employeeDetail(employeeId, legalEntityId) });
      queryClient.invalidateQueries({ queryKey: hrKeys.offboarding(employeeId, employmentId, legalEntityId) });
    }
  });
}

export function useDepartments(legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.departments(legalEntityId),
    queryFn: () => hrApi.listDepartments(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useCreateDepartmentMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateDepartmentRequest) => hrApi.createDepartment(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.departments(legalEntityId) });
    }
  });
}

export function usePositions(legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.positions(legalEntityId),
    queryFn: () => hrApi.listPositions(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useCreatePositionMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePositionRequest) => hrApi.createPosition(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.positions(legalEntityId) });
    }
  });
}

export function useAssignments(employmentId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.assignments(employmentId, legalEntityId),
    queryFn: () => hrApi.listAssignments(employmentId, legalEntityId),
    enabled: Boolean(employmentId && legalEntityId)
  });
}

export function useCreateAssignmentMutation(employmentId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateAssignmentRequest) => hrApi.createAssignment(employmentId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.assignments(employmentId, legalEntityId) });
    }
  });
}

export function useContracts(employmentId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.contracts(employmentId, legalEntityId),
    queryFn: () => hrApi.listContracts(employmentId, legalEntityId),
    enabled: Boolean(employmentId && legalEntityId)
  });
}

export function useCreateContractMutation(employmentId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateEmploymentContractRequest) => hrApi.createContract(employmentId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.contracts(employmentId, legalEntityId) });
    }
  });
}

export function useActivateContractMutation(employmentId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ contractId, version }: { contractId: string; version: number }) =>
      hrApi.activateContract(contractId, legalEntityId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.contracts(employmentId, legalEntityId) });
    }
  });
}

export function useContractAmendments(contractId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.contractAmendments(contractId, legalEntityId),
    queryFn: () => hrApi.listContractAmendments(contractId, legalEntityId),
    enabled: Boolean(contractId && legalEntityId)
  });
}

export function useCreateContractAmendmentMutation(employmentId: string, contractId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: { amendmentType: string; effectiveDate: string; termsSummary: string; fileRef?: string | null }) =>
      hrApi.createContractAmendment(contractId, legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.contractAmendments(contractId, legalEntityId) });
      queryClient.invalidateQueries({ queryKey: hrKeys.contracts(employmentId, legalEntityId) });
    }
  });
}

export function useCompensationAgreements(employeeId: string, employmentId: string, legalEntityId: string, isAllowed = true) {
  return useQuery({
    queryKey: hrKeys.compensation(employmentId, legalEntityId),
    queryFn: () => hrApi.listCompensationAgreements(employeeId, employmentId, legalEntityId),
    enabled: Boolean(employeeId && employmentId && legalEntityId) && isAllowed
  });
}

export function useCreateCompensationMutation(employeeId: string, employmentId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateCompensationAgreementRequest) => hrApi.createCompensationAgreement(employeeId, employmentId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.compensation(employmentId, legalEntityId) });
    }
  });
}

export function useInsuranceEnrollments(employeeId: string, employmentId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.insurance(employeeId, employmentId, legalEntityId),
    queryFn: () => hrApi.listInsuranceEnrollments(employeeId, employmentId, legalEntityId),
    enabled: Boolean(employeeId && employmentId && legalEntityId)
  });
}

export function useCreateInsuranceMutation(employeeId: string, employmentId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateInsuranceEnrollmentRequest) => hrApi.createInsuranceEnrollment(employeeId, employmentId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.insurance(employeeId, employmentId, legalEntityId) });
    }
  });
}

export function useActivateInsuranceMutation(employeeId: string, employmentId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ enrollmentId, version }: { enrollmentId: string; version: number }) =>
      hrApi.activateInsuranceEnrollment(employeeId, employmentId, enrollmentId, legalEntityId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.insurance(employeeId, employmentId, legalEntityId) });
    }
  });
}

export function useDocuments(employeeId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.documents(employeeId, legalEntityId),
    queryFn: () => hrApi.listDocuments(employeeId, legalEntityId),
    enabled: Boolean(employeeId && legalEntityId)
  });
}

export function useDocumentCompleteness(employeeId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.documentCompleteness(employeeId, legalEntityId),
    queryFn: () => hrApi.getDocumentCompleteness(employeeId, legalEntityId),
    enabled: Boolean(employeeId && legalEntityId)
  });
}

export function useDocumentRequirements(legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.documentRequirements(legalEntityId),
    queryFn: () => hrApi.listDocumentRequirements(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useCreateDocumentMutation(employeeId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: { legalEntityId: string; category: string; fileRef: string; sensitivity?: string | undefined; titleAr?: string | undefined; titleEn?: string | null | undefined; contentType?: string | undefined; sizeBytes?: number | undefined; expiryDate?: string | null | undefined; isSensitive?: boolean | undefined; employmentId?: string | null | undefined }) =>
      hrApi.createDocument(employeeId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.documents(employeeId, legalEntityId) });
      queryClient.invalidateQueries({ queryKey: hrKeys.documentCompleteness(employeeId, legalEntityId) });
    }
  });
}

export function useVerifyDocumentMutation(employeeId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ documentId, isVerified, notes, expectedVersion, verified, reason }: { documentId: string; isVerified?: boolean | undefined; notes?: string | null | undefined; expectedVersion: number; verified?: boolean | undefined; reason?: string | undefined }) =>
      hrApi.verifyDocument(employeeId, documentId, legalEntityId, { isVerified, notes, expectedVersion, verified, reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.documents(employeeId, legalEntityId) });
      queryClient.invalidateQueries({ queryKey: hrKeys.documentCompleteness(employeeId, legalEntityId) });
    }
  });
}

export function useDocumentDownloadMutation(employeeId: string, legalEntityId: string) {
  return useMutation({
    mutationFn: ({ documentId }: { documentId: string }) =>
      hrApi.getDocumentDownload(employeeId, documentId, legalEntityId)
  });
}

export function useIdentifiers(employeeId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.identifiers(employeeId, legalEntityId),
    queryFn: () => hrApi.listIdentifiers(employeeId, legalEntityId),
    enabled: Boolean(employeeId && legalEntityId)
  });
}

export function useCreateIdentifierMutation(employeeId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: { legalEntityId: string; identifierType: string; rawValue: string; isPrimary?: boolean; issueDate?: string | null; expiryDate?: string | null }) =>
      hrApi.createIdentifier(employeeId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.identifiers(employeeId, legalEntityId) });
    }
  });
}

export function useQualifications(employeeId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.qualifications(employeeId, legalEntityId),
    queryFn: () => hrApi.listQualifications(employeeId, legalEntityId),
    enabled: Boolean(employeeId && legalEntityId)
  });
}

export function useCreateQualificationMutation(employeeId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: { legalEntityId: string; level: string; titleAr: string; titleEn?: string | null; institution: string; graduationYear?: number | null; evidenceFileRef?: string | null }) =>
      hrApi.createQualification(employeeId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.qualifications(employeeId, legalEntityId) });
    }
  });
}

export function useCredentials(employeeId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.credentials(employeeId, legalEntityId),
    queryFn: () => hrApi.listCredentials(employeeId, legalEntityId),
    enabled: Boolean(employeeId && legalEntityId)
  });
}

export function useCreateCredentialMutation(employeeId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: { legalEntityId: string; credentialType: string; nameAr: string; nameEn?: string | null; issuingAuthority: string; issueDate: string; expiryDate?: string | null; evidenceFileRef?: string | null }) =>
      hrApi.createCredential(employeeId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.credentials(employeeId, legalEntityId) });
    }
  });
}

export function useAttendanceRecords(params: { legalEntityId: string; from: string; to: string; employmentId?: string | null; status?: string | null; page?: number | null; pageSize?: number | null }) {
  return useQuery({
    queryKey: hrKeys.attendance(params),
    queryFn: () => hrApi.listAttendanceRecords(params),
    enabled: Boolean(params.legalEntityId && params.from && params.to)
  });
}

export function useAttendancePunchMutation(employmentId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateAttendancePunchRequest) => hrApi.recordPunch(employmentId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.all });
    }
  });
}

export function useAttendanceExceptions(legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.attendanceExceptions(legalEntityId),
    queryFn: () => hrApi.listAttendanceExceptions(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useAttendanceCorrectionMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ recordId, request }: { recordId: string; request: AttendanceCorrectionRequest }) =>
      hrApi.correctAttendance(recordId, legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.all });
    }
  });
}

export function useLeaveTypes(legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.leaveTypes(legalEntityId),
    queryFn: () => hrApi.listLeaveTypes(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useLeaveBalance(employmentId: string, leaveTypeId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.leaveBalance(employmentId, leaveTypeId, legalEntityId),
    queryFn: () => hrApi.getLeaveBalance(employmentId, leaveTypeId, legalEntityId),
    enabled: Boolean(employmentId && leaveTypeId && legalEntityId)
  });
}

export function useLeaveRequests(legalEntityId: string, employmentId?: string | null | undefined) {
  return useQuery({
    queryKey: hrKeys.leaveRequests(legalEntityId, employmentId),
    queryFn: () => hrApi.listLeaveRequests({ legalEntityId, employmentId }),
    enabled: Boolean(legalEntityId)
  });
}

export function useCreateLeaveRequestMutation(legalEntityId: string, employmentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateLeaveRequest) => hrApi.createLeaveRequest(employmentId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.leaveRequests(legalEntityId, employmentId) });
      queryClient.invalidateQueries({ queryKey: hrKeys.all });
    }
  });
}

export function useApproveLeaveRequestMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ requestId, decision }: { requestId: string; decision: LeaveRequestDecision }) =>
      hrApi.approveLeaveRequest(requestId, legalEntityId, decision),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.all });
    }
  });
}

export function useRejectLeaveRequestMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ requestId, decision }: { requestId: string; decision: LeaveRequestDecision }) =>
      hrApi.rejectLeaveRequest(requestId, legalEntityId, decision),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.all });
    }
  });
}

export function useCancelLeaveRequestMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ requestId, decision }: { requestId: string; decision: LeaveRequestDecision }) =>
      hrApi.cancelLeaveRequest(requestId, legalEntityId, decision),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.all });
    }
  });
}

export function useAdjustments(employmentId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.adjustments(employmentId, legalEntityId),
    queryFn: () => hrApi.listAdjustments(employmentId, legalEntityId),
    enabled: Boolean(employmentId && legalEntityId)
  });
}

export function useCreateAdjustmentMutation(employeeId: string, employmentId: string, legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateHrAdjustmentRequest) => hrApi.createAdjustment(employeeId, employmentId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: hrKeys.adjustments(employmentId, legalEntityId) });
    }
  });
}

export function useShifts(legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.shifts(legalEntityId),
    queryFn: () => hrApi.listShifts(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useWorkCalendars(legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.workCalendars(legalEntityId),
    queryFn: () => hrApi.listWorkCalendars(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useTimeline(employeeId: string, legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.timeline(employeeId, legalEntityId),
    queryFn: () => hrApi.getTimeline(employeeId, legalEntityId),
    enabled: Boolean(employeeId && legalEntityId)
  });
}

export function useMyProfile(legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.myProfile(legalEntityId),
    queryFn: () => hrApi.getMyProfile(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useMyTeam(legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.myTeam(legalEntityId),
    queryFn: () => hrApi.getMyTeam(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useMyTeamLeaveApprovals(legalEntityId: string) {
  return useQuery({
    queryKey: hrKeys.myTeamLeaveApprovals(legalEntityId),
    queryFn: () => hrApi.getMyTeamLeaveApprovals(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}
