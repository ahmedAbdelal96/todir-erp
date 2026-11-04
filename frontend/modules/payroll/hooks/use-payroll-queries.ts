import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { payrollApi } from "../services/payroll-api.ts";
import type {
  CreatePayrollGroupRequest,
  CreatePayrollPeriodRequest,
  CreatePayrollRunRequest,
  CreatePayrollInputRequest,
  CreateReceivableRequest,
  PayrollLoanActionRequest,
  PayrollImportRequest,
  PayrollOpeningBalanceRequest,
  UpdatePayrollStatutorySettingsRequest,
  CreatePayrollComponentRequest,
  CreateSalaryStructureRequest,
  CreatePayrollAssignmentRequest
} from "../types.ts";

export const payrollQueryKeys = {
  all: ["payroll"] as const,
  groups: (legalEntityId: string) => [...payrollQueryKeys.all, "groups", legalEntityId] as const,
  periods: (legalEntityId: string, groupId: string) => [...payrollQueryKeys.all, "periods", legalEntityId, groupId] as const,
  runs: (legalEntityId: string) => [...payrollQueryKeys.all, "runs", legalEntityId] as const,
  run: (legalEntityId: string, runId: string) => [...payrollQueryKeys.all, "run", legalEntityId, runId] as const,
  runEmployees: (legalEntityId: string, runId: string) => [...payrollQueryKeys.all, "run-employees", legalEntityId, runId] as const,
  payslips: (legalEntityId: string) => [...payrollQueryKeys.all, "payslips", legalEntityId] as const,
  selfPayslips: (legalEntityId: string) => [...payrollQueryKeys.all, "self-payslips", legalEntityId] as const,
  reports: (legalEntityId: string) => [...payrollQueryKeys.all, "reports", legalEntityId] as const,
  openingBalances: (legalEntityId: string) => [...payrollQueryKeys.all, "opening-balances", legalEntityId] as const,
  components: (legalEntityId: string, params?: object) => [...payrollQueryKeys.all, "components", legalEntityId, params] as const,
  salaryStructures: (legalEntityId: string, params?: object) => [...payrollQueryKeys.all, "salary-structures", legalEntityId, params] as const,
  employeeAssignments: (legalEntityId: string, params?: object) => [...payrollQueryKeys.all, "employee-assignments", legalEntityId, params] as const,
  statutorySettings: (legalEntityId: string) => [...payrollQueryKeys.all, "statutory-settings", legalEntityId] as const,
  statutoryHistory: (legalEntityId: string) => [...payrollQueryKeys.all, "statutory-history", legalEntityId] as const
};

export function usePayrollComponents(legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; status?: string }) {
  return useQuery({
    queryKey: payrollQueryKeys.components(legalEntityId, params),
    queryFn: () => payrollApi.listComponents(legalEntityId, params),
    retry: false,
    enabled: Boolean(legalEntityId)
  });
}

export function useSalaryStructures(legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; status?: string }) {
  return useQuery({
    queryKey: payrollQueryKeys.salaryStructures(legalEntityId, params),
    queryFn: () => payrollApi.listSalaryStructures(legalEntityId, params),
    retry: false,
    enabled: Boolean(legalEntityId)
  });
}

export function useEmployeeAssignments(legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; status?: string }) {
  return useQuery({
    queryKey: payrollQueryKeys.employeeAssignments(legalEntityId, params),
    queryFn: () => payrollApi.listEmployeeAssignments(legalEntityId, params),
    retry: false,
    enabled: Boolean(legalEntityId)
  });
}

export function usePayrollGroups(legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string }) {
  return useQuery({
    queryKey: [...payrollQueryKeys.groups(legalEntityId), params],
    queryFn: () => payrollApi.listGroups(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function usePayrollPeriods(legalEntityId: string, groupId: string, params?: { page?: number; pageSize?: number; status?: string }) {
  return useQuery({
    queryKey: [...payrollQueryKeys.periods(legalEntityId, groupId), params],
    queryFn: () => payrollApi.listPeriods(legalEntityId, groupId, params),
    enabled: Boolean(legalEntityId) && Boolean(groupId)
  });
}

export function usePayrollRuns(legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; runType?: string; state?: string }) {
  return useQuery({
    queryKey: [...payrollQueryKeys.runs(legalEntityId), params],
    queryFn: () => payrollApi.listRuns(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function usePayrollRun(legalEntityId: string, runId: string | null | undefined) {
  return useQuery({
    queryKey: payrollQueryKeys.run(legalEntityId, runId ?? ""),
    queryFn: () => payrollApi.getRun(legalEntityId, runId ?? ""),
    enabled: Boolean(legalEntityId) && Boolean(runId)
  });
}

export function usePayrollRunEmployees(legalEntityId: string, runId: string | null | undefined, params?: { page?: number; pageSize?: number; status?: string; search?: string }) {
  return useQuery({
    queryKey: [...payrollQueryKeys.runEmployees(legalEntityId, runId ?? ""), params],
    queryFn: () => payrollApi.listRunEmployees(legalEntityId, runId ?? "", params),
    enabled: Boolean(legalEntityId) && Boolean(runId)
  });
}

export function usePayslips(legalEntityId: string, params?: { page?: number | undefined; pageSize?: number | undefined; employmentId?: string | undefined; search?: string | undefined } | undefined) {
  return useQuery({
    queryKey: [...payrollQueryKeys.payslips(legalEntityId), params],
    queryFn: () => payrollApi.listPayslips(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useSelfPayslips(legalEntityId: string, params?: { page?: number | undefined; pageSize?: number | undefined } | undefined) {
  return useQuery({
    queryKey: [...payrollQueryKeys.selfPayslips(legalEntityId), params],
    queryFn: () => payrollApi.listSelfPayslips(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function usePayrollReport(legalEntityId: string, params?: { from?: string | undefined; to?: string | undefined; runType?: string | undefined; search?: string | undefined; page?: number | undefined; pageSize?: number | undefined } | undefined) {
  return useQuery({
    queryKey: [...payrollQueryKeys.reports(legalEntityId), params],
    queryFn: () => payrollApi.getReport(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function usePayrollAccounting(legalEntityId: string, runId: string | null | undefined) {
  return useQuery({
    queryKey: [...payrollQueryKeys.all, "accounting", legalEntityId, runId ?? ""],
    queryFn: () => payrollApi.getAccountingState(legalEntityId, runId ?? ""),
    enabled: Boolean(legalEntityId) && Boolean(runId)
  });
}

export function useReconcileAccountingMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (runId: string) => payrollApi.reconcileAccounting(legalEntityId, runId),
    onSuccess: (_, runId) => {
      queryClient.invalidateQueries({ queryKey: payrollQueryKeys.reports(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: payrollQueryKeys.runs(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: payrollQueryKeys.run(legalEntityId, runId) });
      queryClient.invalidateQueries({ queryKey: [...payrollQueryKeys.all, "accounting", legalEntityId, runId] });
    }
  });
}

export function usePayrollOpeningBalances(legalEntityId: string, params?: { page?: number; pageSize?: number; employmentId?: string; balanceType?: string; search?: string }) {
  return useQuery({
    queryKey: [...payrollQueryKeys.openingBalances(legalEntityId), params],
    queryFn: () => payrollApi.listOpeningBalances(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function usePayrollInputs(legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; status?: string }) {
  return useQuery({
    queryKey: [...payrollQueryKeys.all, "inputs", legalEntityId, params],
    queryFn: () => payrollApi.listInputs(legalEntityId, params),
    retry: false,
    enabled: Boolean(legalEntityId)
  });
}

export function useReceivables(legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; status?: string }) {
  return useQuery({
    queryKey: [...payrollQueryKeys.all, "receivables", legalEntityId, params],
    queryFn: () => payrollApi.listReceivables(legalEntityId, params),
    retry: false,
    enabled: Boolean(legalEntityId)
  });
}

export function usePayrollStatutorySettings(legalEntityId: string) {
  return useQuery({
    queryKey: payrollQueryKeys.statutorySettings(legalEntityId),
    queryFn: () => payrollApi.getStatutorySettings(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function usePayrollStatutoryHistory(legalEntityId: string, params?: { page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: [...payrollQueryKeys.statutoryHistory(legalEntityId), params],
    queryFn: () => payrollApi.listStatutorySettingsHistory(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

// Mutations
export function useCreatePayrollGroupMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePayrollGroupRequest) => payrollApi.createGroup(legalEntityId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.groups(legalEntityId) });
    }
  });
}

export function useCreatePayrollPeriodMutation(legalEntityId: string, groupId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePayrollPeriodRequest) => payrollApi.createPeriod(legalEntityId, groupId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.periods(legalEntityId, groupId) });
    }
  });
}

export function useCreatePayrollRunMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePayrollRunRequest) => payrollApi.createRun(legalEntityId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.runs(legalEntityId) });
    }
  });
}

export function useCaptureInputsMutation(legalEntityId: string, runId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => payrollApi.captureInputs(legalEntityId, runId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.run(legalEntityId, runId) });
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.runEmployees(legalEntityId, runId) });
    }
  });
}

export function useCalculateRunMutation(legalEntityId: string, runId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => payrollApi.calculateRun(legalEntityId, runId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.run(legalEntityId, runId) });
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.runEmployees(legalEntityId, runId) });
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.runs(legalEntityId) });
    }
  });
}

export function useApproveRunMutation(legalEntityId: string, runId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => payrollApi.approveRun(legalEntityId, runId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.run(legalEntityId, runId) });
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.runs(legalEntityId) });
    }
  });
}

export function useFinalizeRunMutation(legalEntityId: string, runId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => payrollApi.finalizeRun(legalEntityId, runId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.run(legalEntityId, runId) });
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.runs(legalEntityId) });
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.payslips(legalEntityId) });
    }
  });
}

export function useCreatePayrollInputMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePayrollInputRequest) => payrollApi.createInput(legalEntityId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useApprovePayrollInputMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (inputId: string) => payrollApi.approveInput(legalEntityId, inputId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useCreateReceivableMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateReceivableRequest) => payrollApi.createReceivable(legalEntityId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useApproveReceivableMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (receivableId: string) => payrollApi.approveReceivable(legalEntityId, receivableId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useRequestDisbursementMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (receivableId: string) => payrollApi.requestDisbursement(legalEntityId, receivableId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useCompleteDisbursementMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ disbursementId, providerReference }: { disbursementId: string; providerReference?: string }) =>
      payrollApi.completeDisbursement(legalEntityId, disbursementId, providerReference),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useFailDisbursementMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ disbursementId, failureCode }: { disbursementId: string; failureCode?: string }) =>
      payrollApi.failDisbursement(legalEntityId, disbursementId, failureCode),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useApplyLoanActionMutation(legalEntityId: string, receivableId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: PayrollLoanActionRequest) => payrollApi.applyLoanAction(legalEntityId, receivableId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useImportInputsPreviewMutation(legalEntityId: string) {
  return useMutation({
    mutationFn: (request: PayrollImportRequest) => payrollApi.importInputsPreview(legalEntityId, request)
  });
}

export function useImportInputsCommitMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: PayrollImportRequest) => payrollApi.importInputsCommit(legalEntityId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useCreateOpeningBalanceMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: PayrollOpeningBalanceRequest) => payrollApi.createOpeningBalance(legalEntityId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.openingBalances(legalEntityId) });
    }
  });
}

export function useApproveOpeningBalanceMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (openingBalanceId: string) => payrollApi.approveOpeningBalance(legalEntityId, openingBalanceId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.openingBalances(legalEntityId) });
    }
  });
}

export function useUpdateStatutorySettingsMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdatePayrollStatutorySettingsRequest) => payrollApi.updateStatutorySettings(legalEntityId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.statutorySettings(legalEntityId) });
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.statutoryHistory(legalEntityId) });
    }
  });
}

export function useCreatePayrollComponentMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePayrollComponentRequest) => payrollApi.createComponent(legalEntityId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useCreateSalaryStructureMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSalaryStructureRequest) => payrollApi.createSalaryStructure(legalEntityId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useCreateEmployeeAssignmentMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employmentId, request }: { employmentId: string; request: CreatePayrollAssignmentRequest }) =>
      payrollApi.createEmployeeAssignment(legalEntityId, employmentId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useCancelPayrollInputMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (inputId: string) => payrollApi.cancelInput(legalEntityId, inputId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}

export function useDisburseReceivableMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (receivableId: string) => payrollApi.disburseReceivable(legalEntityId, receivableId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: payrollQueryKeys.all });
    }
  });
}
