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
  PayrollInput,
  CreatePayrollInputRequest,
  EmployeeReceivable,
  CreateReceivableRequest,
  ReceivableDisbursement,
  PayrollLoanAction,
  PayrollLoanActionRequest,
  PayrollReportSummary,
  PayrollImportRequest,
  PayrollImportResult,
  PayrollOpeningBalance,
  PayrollOpeningBalanceRequest,
  PayrollStatutorySettings,
  UpdatePayrollStatutorySettingsRequest
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
  listPayslips: (legalEntityId: string, params?: { page?: number; pageSize?: number; employmentId?: string; search?: string }): Promise<readonly Payslip[]> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    if (params?.employmentId) q.set("employmentId", params.employmentId);
    if (params?.search) q.set("search", params.search);
    return httpClient<readonly Payslip[]>(`${BASE}/legal-entities/${legalEntityId}/payslips?${q.toString()}`);
  },

  listSelfPayslips: (legalEntityId: string, params?: { page?: number; pageSize?: number }): Promise<readonly Payslip[]> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
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

  requestDisbursement: (legalEntityId: string, receivableId: string): Promise<ReceivableDisbursement> =>
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
  getReport: (legalEntityId: string, params?: { from?: string; to?: string; runType?: string; page?: number; pageSize?: number }): Promise<PayrollReportSummary> => {
    const q = new URLSearchParams();
    if (params?.from) q.set("from", params.from);
    if (params?.to) q.set("to", params.to);
    if (params?.runType) q.set("runType", params.runType);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<PayrollReportSummary>(`${BASE}/legal-entities/${legalEntityId}/reports/payroll?${q.toString()}`);
  },

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
    })
};
