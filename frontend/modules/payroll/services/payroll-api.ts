import { httpClient } from "../../../core/api/http-client.ts";
import type {
  PayrollGroup,
  CreatePayrollGroupRequest,
  PayrollPeriod,
  CreatePayrollPeriodRequest,
  PayrollRun,
  CreatePayrollRunRequest,
  PayrollEmployee,
  PayrollSnapshot,
  Payslip,
  PayslipsPage,
  PayrollInput,
  CreatePayrollInputRequest,
  EmployeeReceivable,
  CreateReceivableRequest,
  ReceivableDisbursement,
  PayrollLoanAction,
  PayrollLoanActionRequest,
  PayrollReportSummary,
  PayrollReportItem,
  PayrollAccountingReconciliation,
  PayrollImportRequest,
  PayrollImportResult,
  PayrollOpeningBalance,
  PayrollOpeningBalanceRequest,
  PayrollStatutorySettings,
  UpdatePayrollStatutorySettingsRequest,
  PayrollComponent,
  CreatePayrollComponentRequest,
  CreatePayrollComponentVersionRequest,
  PayrollComponentVersion,
  SalaryStructure,
  CreateSalaryStructureRequest,
  CreateSalaryStructureVersionRequest,
  SalaryStructureVersion,
  EmployeePayrollAssignment,
  CreatePayrollAssignmentRequest
} from "../types.ts";

const BASE = "/api/v1/payroll";

export const payrollApi = {
  // Groups
  listGroups: (legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string }): Promise<readonly PayrollGroup[]> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    if (params?.search) q.set("search", params.search);
    return httpClient<readonly PayrollGroup[]>(`${BASE}/legal-entities/${legalEntityId}/groups?${q.toString()}`);
  },

  createGroup: (legalEntityId: string, request: CreatePayrollGroupRequest): Promise<PayrollGroup> =>
    httpClient<PayrollGroup>(`${BASE}/legal-entities/${legalEntityId}/groups`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Periods
  listPeriods: (legalEntityId: string, groupId: string, params?: { page?: number; pageSize?: number; status?: string }): Promise<readonly PayrollPeriod[]> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    if (params?.status) q.set("status", params.status);
    return httpClient<readonly PayrollPeriod[]>(`${BASE}/legal-entities/${legalEntityId}/groups/${groupId}/periods?${q.toString()}`);
  },

  createPeriod: (legalEntityId: string, groupId: string, request: CreatePayrollPeriodRequest): Promise<PayrollPeriod> =>
    httpClient<PayrollPeriod>(`${BASE}/legal-entities/${legalEntityId}/groups/${groupId}/periods`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Runs
  listRuns: (legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; runType?: string; state?: string }): Promise<readonly PayrollRun[]> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    if (params?.search) q.set("search", params.search);
    if (params?.runType) q.set("runType", params.runType);
    if (params?.state) q.set("state", params.state);
    return httpClient<readonly PayrollRun[]>(`${BASE}/legal-entities/${legalEntityId}/runs?${q.toString()}`);
  },

  createRun: (legalEntityId: string, request: CreatePayrollRunRequest): Promise<PayrollRun> =>
    httpClient<PayrollRun>(`${BASE}/legal-entities/${legalEntityId}/runs`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  getRun: (legalEntityId: string, runId: string): Promise<PayrollRun> =>
    httpClient<PayrollRun>(`${BASE}/legal-entities/${legalEntityId}/runs/${runId}`),

  listRunEmployees: (legalEntityId: string, runId: string, params?: { page?: number; pageSize?: number; status?: string; search?: string }): Promise<readonly PayrollEmployee[]> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    if (params?.status) q.set("status", params.status);
    if (params?.search) q.set("search", params.search);
    return httpClient<readonly PayrollEmployee[]>(`${BASE}/legal-entities/${legalEntityId}/runs/${runId}/employees?${q.toString()}`);
  },

  captureInputs: (legalEntityId: string, runId: string): Promise<PayrollSnapshot> =>
    httpClient<PayrollSnapshot>(`${BASE}/legal-entities/${legalEntityId}/runs/${runId}/capture-inputs`, {
      method: "POST"
    }),

  calculateRun: (legalEntityId: string, runId: string): Promise<{ status: string }> =>
    httpClient<{ status: string }>(`${BASE}/legal-entities/${legalEntityId}/runs/${runId}/calculate`, {
      method: "POST"
    }),

  approveRun: (legalEntityId: string, runId: string): Promise<PayrollRun> =>
    httpClient<PayrollRun>(`${BASE}/legal-entities/${legalEntityId}/runs/${runId}/approve`, {
      method: "POST"
    }),

  finalizeRun: (legalEntityId: string, runId: string): Promise<{ status: string }> =>
    httpClient<{ status: string }>(`${BASE}/legal-entities/${legalEntityId}/runs/${runId}/finalize`, {
      method: "POST"
    }),

  // Payslips
  listPayslips: (legalEntityId: string, params?: { page?: number | undefined; pageSize?: number | undefined; employmentId?: string | undefined; search?: string | undefined } | undefined): Promise<PayslipsPage> => {
    const q = new URLSearchParams();
    if (params?.page != null) q.set("page", String(params.page));
    if (params?.pageSize != null) q.set("pageSize", String(params.pageSize));
    if (params?.employmentId) q.set("employmentId", params.employmentId);
    if (params?.search) q.set("search", params.search);
    return httpClient<PayslipsPage>(`${BASE}/legal-entities/${legalEntityId}/payslips?${q.toString()}`);
  },

  exportPayslips: (legalEntityId: string, params?: { employmentId?: string | undefined; search?: string | undefined } | undefined): Promise<readonly Payslip[]> => {
    const q = new URLSearchParams();
    if (params?.employmentId) q.set("employmentId", params.employmentId);
    if (params?.search) q.set("search", params.search);
    q.set("format", "json");
    return httpClient<readonly Payslip[]>(`${BASE}/legal-entities/${legalEntityId}/payslips/export?${q.toString()}`);
  },

  listSelfPayslips: (legalEntityId: string, params?: { page?: number | undefined; pageSize?: number | undefined } | undefined): Promise<readonly Payslip[]> => {
    const q = new URLSearchParams();
    if (params?.page != null) q.set("page", String(params.page));
    if (params?.pageSize != null) q.set("pageSize", String(params.pageSize));
    return httpClient<readonly Payslip[]>(`${BASE}/legal-entities/${legalEntityId}/self/payslips?${q.toString()}`);
  },

  // Inputs
  createInput: (legalEntityId: string, request: CreatePayrollInputRequest): Promise<PayrollInput> =>
    httpClient<PayrollInput>(`${BASE}/legal-entities/${legalEntityId}/inputs`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  approveInput: (legalEntityId: string, inputId: string): Promise<PayrollInput> =>
    httpClient<PayrollInput>(`${BASE}/legal-entities/${legalEntityId}/inputs/${inputId}/approve`, {
      method: "POST"
    }),

  cancelInput: (legalEntityId: string, inputId: string): Promise<PayrollInput> =>
    httpClient<PayrollInput>(`${BASE}/legal-entities/${legalEntityId}/inputs/${inputId}/cancel`, {
      method: "POST"
    }),

  listInputs: (legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; status?: string }): Promise<readonly PayrollInput[]> => {
    const q = new URLSearchParams();
    if (params?.page != null) q.set("page", String(params.page));
    if (params?.pageSize != null) q.set("pageSize", String(params.pageSize));
    if (params?.search) q.set("search", params.search);
    if (params?.status) q.set("status", params.status);
    return httpClient<readonly PayrollInput[]>(`${BASE}/legal-entities/${legalEntityId}/inputs?${q.toString()}`);
  },

  // Receivables (Loans & Advances)
  createReceivable: (legalEntityId: string, request: CreateReceivableRequest): Promise<EmployeeReceivable> =>
    httpClient<EmployeeReceivable>(`${BASE}/legal-entities/${legalEntityId}/employee-receivables`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  approveReceivable: (legalEntityId: string, receivableId: string): Promise<EmployeeReceivable> =>
    httpClient<EmployeeReceivable>(`${BASE}/legal-entities/${legalEntityId}/employee-receivables/${receivableId}/approve`, {
      method: "POST"
    }),

  listReceivables: (legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; status?: string }): Promise<readonly EmployeeReceivable[]> => {
    const q = new URLSearchParams();
    if (params?.page != null) q.set("page", String(params.page));
    if (params?.pageSize != null) q.set("pageSize", String(params.pageSize));
    if (params?.search) q.set("search", params.search);
    if (params?.status) q.set("status", params.status);
    return httpClient<readonly EmployeeReceivable[]>(`${BASE}/legal-entities/${legalEntityId}/employee-receivables?${q.toString()}`);
  },

  requestDisbursement: (legalEntityId: string, receivableId: string): Promise<ReceivableDisbursement> =>
    httpClient<ReceivableDisbursement>(`${BASE}/legal-entities/${legalEntityId}/employee-receivables/${receivableId}/disburse`, {
      method: "POST"
    }),

  disburseReceivable: (legalEntityId: string, receivableId: string): Promise<ReceivableDisbursement> =>
    httpClient<ReceivableDisbursement>(`${BASE}/legal-entities/${legalEntityId}/employee-receivables/${receivableId}/disburse`, {
      method: "POST"
    }),

  completeDisbursement: (legalEntityId: string, disbursementId: string, providerReference?: string): Promise<ReceivableDisbursement> => {
    const q = providerReference ? `?providerReference=${encodeURIComponent(providerReference)}` : "";
    return httpClient<ReceivableDisbursement>(`${BASE}/legal-entities/${legalEntityId}/employee-receivables/disbursements/${disbursementId}/complete${q}`, {
      method: "POST"
    });
  },

  failDisbursement: (legalEntityId: string, disbursementId: string, failureCode?: string): Promise<ReceivableDisbursement> => {
    const q = failureCode ? `?failureCode=${encodeURIComponent(failureCode)}` : "";
    return httpClient<ReceivableDisbursement>(`${BASE}/legal-entities/${legalEntityId}/employee-receivables/disbursements/${disbursementId}/fail${q}`, {
      method: "POST"
    });
  },

  applyLoanAction: (legalEntityId: string, receivableId: string, request: PayrollLoanActionRequest): Promise<PayrollLoanAction> =>
    httpClient<PayrollLoanAction>(`${BASE}/legal-entities/${legalEntityId}/employee-receivables/${receivableId}/actions`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Reports
  getReport: (legalEntityId: string, params?: { from?: string | undefined; to?: string | undefined; runType?: string | undefined; search?: string | undefined; page?: number | undefined; pageSize?: number | undefined } | undefined): Promise<PayrollReportSummary> => {
    const q = new URLSearchParams();
    if (params?.from) q.set("from", params.from);
    if (params?.to) q.set("to", params.to);
    if (params?.runType) q.set("runType", params.runType);
    if (params?.search) q.set("search", params.search);
    if (params?.page != null) q.set("page", String(params.page));
    if (params?.pageSize != null) q.set("pageSize", String(params.pageSize));
    return httpClient<PayrollReportSummary>(`${BASE}/legal-entities/${legalEntityId}/reports/payroll?${q.toString()}`);
  },

  exportReport: (legalEntityId: string, params?: { from?: string | undefined; to?: string | undefined; runType?: string | undefined; search?: string | undefined } | undefined): Promise<readonly PayrollReportItem[]> => {
    const q = new URLSearchParams();
    if (params?.from) q.set("from", params.from);
    if (params?.to) q.set("to", params.to);
    if (params?.runType) q.set("runType", params.runType);
    if (params?.search) q.set("search", params.search);
    q.set("format", "json");
    return httpClient<readonly PayrollReportItem[]>(`${BASE}/legal-entities/${legalEntityId}/reports/payroll/export?${q.toString()}`);
  },

  getAccountingState: (legalEntityId: string, runId: string): Promise<PayrollAccountingReconciliation> =>
    httpClient<PayrollAccountingReconciliation>(`${BASE}/legal-entities/${legalEntityId}/runs/${runId}/accounting`),

  reconcileAccounting: (legalEntityId: string, runId: string): Promise<PayrollAccountingReconciliation> =>
    httpClient<PayrollAccountingReconciliation>(`${BASE}/legal-entities/${legalEntityId}/runs/${runId}/reconcile-accounting`, {
      method: "POST"
    }),

  // Imports
  importInputsPreview: (legalEntityId: string, request: PayrollImportRequest): Promise<PayrollImportResult> =>
    httpClient<PayrollImportResult>(`${BASE}/legal-entities/${legalEntityId}/imports/inputs/preview`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  importInputsCommit: (legalEntityId: string, request: PayrollImportRequest): Promise<PayrollImportResult> =>
    httpClient<PayrollImportResult>(`${BASE}/legal-entities/${legalEntityId}/imports/inputs/commit`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Opening Balances
  listOpeningBalances: (legalEntityId: string, params?: { page?: number; pageSize?: number; employmentId?: string; balanceType?: string; search?: string }): Promise<readonly PayrollOpeningBalance[]> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    if (params?.employmentId) q.set("employmentId", params.employmentId);
    if (params?.balanceType) q.set("balanceType", params.balanceType);
    if (params?.search) q.set("search", params.search);
    return httpClient<readonly PayrollOpeningBalance[]>(`${BASE}/legal-entities/${legalEntityId}/opening-balances?${q.toString()}`);
  },

  createOpeningBalance: (legalEntityId: string, request: PayrollOpeningBalanceRequest): Promise<PayrollOpeningBalance> =>
    httpClient<PayrollOpeningBalance>(`${BASE}/legal-entities/${legalEntityId}/opening-balances`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  approveOpeningBalance: (legalEntityId: string, openingBalanceId: string): Promise<PayrollOpeningBalance> =>
    httpClient<PayrollOpeningBalance>(`${BASE}/legal-entities/${legalEntityId}/opening-balances/${openingBalanceId}/approve`, {
      method: "POST"
    }),

  // Statutory Settings
  getStatutorySettings: (legalEntityId: string): Promise<PayrollStatutorySettings> =>
    httpClient<PayrollStatutorySettings>(`${BASE}/legal-entities/${legalEntityId}/settings/statutory`),

  listStatutorySettingsHistory: (legalEntityId: string, params?: { page?: number; pageSize?: number }): Promise<readonly PayrollStatutorySettings[]> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<readonly PayrollStatutorySettings[]>(`${BASE}/legal-entities/${legalEntityId}/settings/statutory/history?${q.toString()}`);
  },

  updateStatutorySettings: (legalEntityId: string, request: UpdatePayrollStatutorySettingsRequest): Promise<PayrollStatutorySettings> =>
    httpClient<PayrollStatutorySettings>(`${BASE}/legal-entities/${legalEntityId}/settings/statutory`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  // Components
  listComponents: (legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; status?: string }): Promise<readonly PayrollComponent[]> => {
    const q = new URLSearchParams();
    if (params?.page != null) q.set("page", String(params.page));
    if (params?.pageSize != null) q.set("pageSize", String(params.pageSize));
    if (params?.search) q.set("search", params.search);
    if (params?.status) q.set("status", params.status);
    return httpClient<readonly PayrollComponent[]>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/components?${q.toString()}`);
  },

  createComponent: (legalEntityId: string, request: CreatePayrollComponentRequest): Promise<PayrollComponent> =>
    httpClient<PayrollComponent>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/components`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createComponentVersion: (legalEntityId: string, componentId: string, request: CreatePayrollComponentVersionRequest): Promise<PayrollComponentVersion> =>
    httpClient<PayrollComponentVersion>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/components/${componentId}/versions`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  publishComponentVersion: (legalEntityId: string, componentVersionId: string): Promise<PayrollComponentVersion> =>
    httpClient<PayrollComponentVersion>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/component-versions/${componentVersionId}/publish`, {
      method: "POST"
    }),

  // Salary Structures
  listSalaryStructures: (legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; status?: string }): Promise<readonly SalaryStructure[]> => {
    const q = new URLSearchParams();
    if (params?.page != null) q.set("page", String(params.page));
    if (params?.pageSize != null) q.set("pageSize", String(params.pageSize));
    if (params?.search) q.set("search", params.search);
    if (params?.status) q.set("status", params.status);
    return httpClient<readonly SalaryStructure[]>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/salary-structures?${q.toString()}`);
  },

  createSalaryStructure: (legalEntityId: string, request: CreateSalaryStructureRequest): Promise<SalaryStructure> =>
    httpClient<SalaryStructure>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/salary-structures`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createSalaryStructureVersion: (legalEntityId: string, structureId: string, request: CreateSalaryStructureVersionRequest): Promise<SalaryStructureVersion> =>
    httpClient<SalaryStructureVersion>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/salary-structures/${structureId}/versions`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  publishSalaryStructureVersion: (legalEntityId: string, versionId: string): Promise<SalaryStructureVersion> =>
    httpClient<SalaryStructureVersion>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/salary-structure-versions/${versionId}/publish`, {
      method: "POST"
    }),

  // Employee Assignments
  listEmployeeAssignments: (legalEntityId: string, params?: { page?: number; pageSize?: number; search?: string; status?: string }): Promise<readonly EmployeePayrollAssignment[]> => {
    const q = new URLSearchParams();
    if (params?.page != null) q.set("page", String(params.page));
    if (params?.pageSize != null) q.set("pageSize", String(params.pageSize));
    if (params?.search) q.set("search", params.search);
    if (params?.status) q.set("status", params.status);
    return httpClient<readonly EmployeePayrollAssignment[]>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/assignments?${q.toString()}`);
  },

  createEmployeeAssignment: (legalEntityId: string, employmentId: string, request: CreatePayrollAssignmentRequest): Promise<EmployeePayrollAssignment> =>
    httpClient<EmployeePayrollAssignment>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/employments/${employmentId}/assignments`, {
      method: "POST",
      body: JSON.stringify(request)
    })
};
