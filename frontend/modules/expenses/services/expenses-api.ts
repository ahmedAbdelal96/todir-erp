import { httpClient, generateKey } from "../../../core/api/http-client.ts";
import type {
  ExpensePage,
  ExpenseDto,
  CreateExpenseRequest,
  CreateMyExpenseRequest,
  UpdateExpenseRequest,
  ExpenseActionRequest,
  ExpenseApprovalRequest,
  ExpenseApprovalDecisionRequest,
  ExpenseApprovalDto,
  ExpenseDuplicateCandidateDto,
  ExpenseExceptionSummaryDto,
  ExpenseSpendReportRowDto,
  ExpenseCategoryDto,
  CreateExpenseCategoryRequest,
  ExpenseCategoryVersionDto,
  CreateExpenseCategoryVersionRequest,
  PublishExpenseCategoryVersionRequest,
  ExpenseEvidenceDto,
  AddExpenseEvidenceRequest,
  CreateExpenseEvidenceUploadRequest,
  ExpenseEvidenceUploadIntentDto,
  CompleteExpenseEvidenceUploadRequest,
  ExpenseOcrJobDto,
  CompleteExpenseOcrManuallyRequest,
  DeclareMissingReceiptRequest,
  MissingReceiptDeclarationDto,
  ReimbursementDto,
  ExpenseItemizationDto,
  CreateExpenseItemizationRequest,
  ExpenseHistoryDto,
  ExpenseParticipantDto,
  AddExpenseParticipantRequest,
  ExpensePolicyEvaluationDto,
  ExpensePolicyExceptionDto,
  RequestExpensePolicyExceptionRequest,
  DecideExpensePolicyExceptionRequest,
  ExpensePolicyDto,
  ExpensePolicyVersionDto,
  CreateExpensePolicyRequest,
  CreateExpensePolicyVersionRequest,
  PublishExpensePolicyVersionRequest,
  ExpenseReportDto,
  CreateExpenseReportRequest,
  ExpenseReportItemDto,
  AddExpenseReportItemRequest,
  ExpenseReportHistoryDto,
  ImportExpensesRequest,
  ExpenseImportBatchDto,
  ExpenseImportRowDto,
  BusinessAdvanceDto,
  CreateBusinessAdvanceRequest,
  ApplyBusinessAdvanceRequest,
  ReturnBusinessAdvanceRequest,
  RequestBusinessAdvanceWriteOffRequest,
  DecideBusinessAdvanceWriteOffRequest,
  BusinessAdvanceWriteOffDto,
  IssueBusinessAdvanceRequest,
  ExpenseCardTransactionDto,
  ImportExpenseCardTransactionRequest,
  CreateExpenseFromCardTransactionRequest,
  ExpenseCardAdjustmentDto,
  ExpenseCardReconciliationDto,
  RecordExpenseCardAdjustmentRequest,
  ExpenseReimbursementBatchDto,
  PayExpenseReimbursementBatchRequest,
  MileageExpenseDetailDto,
  SetMileageDetailRequest,
  PerDiemExpenseDetailDto,
  SetPerDiemDetailRequest,
  ExpenseRatePolicyDto,
  CreateExpenseRatePolicyRequest,
  PublishExpenseRatePolicyRequest,
  ExpenseDimensionDto,
  SetExpenseDimensionRequest,
  MatchExpenseCardTransactionRequest,
  AllocateExpenseCardTransactionRequest,
  MarkExpenseCardPersonalRequest,
  ExpenseEmployeeRepaymentDto,
  SettleEmployeeRepaymentRequest
} from "../types.ts";

const BASE = "/api/v1/expenses";

export const expensesApi = {
  // --- Category Management ---
  listCategories: (legalEntityId: string): Promise<readonly ExpenseCategoryDto[]> =>
    httpClient<readonly ExpenseCategoryDto[]>(`${BASE}/legal-entities/${legalEntityId}/categories`),

  listMyCategories: (legalEntityId: string): Promise<readonly ExpenseCategoryDto[]> =>
    httpClient<readonly ExpenseCategoryDto[]>(`${BASE}/my/categories?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createCategory: (legalEntityId: string, request: CreateExpenseCategoryRequest): Promise<ExpenseCategoryDto> =>
    httpClient<ExpenseCategoryDto>(`${BASE}/legal-entities/${legalEntityId}/categories`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  archiveCategory: (legalEntityId: string, categoryId: string): Promise<ExpenseCategoryDto> =>
    httpClient<ExpenseCategoryDto>(`${BASE}/legal-entities/${legalEntityId}/categories/${categoryId}/archive`, {
      method: "POST"
    }),

  createCategoryVersion: (
    legalEntityId: string,
    categoryId: string,
    request: CreateExpenseCategoryVersionRequest
  ): Promise<ExpenseCategoryVersionDto> =>
    httpClient<ExpenseCategoryVersionDto>(`${BASE}/legal-entities/${legalEntityId}/categories/${categoryId}/versions`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  publishCategoryVersion: (
    legalEntityId: string,
    versionId: string,
    request: PublishExpenseCategoryVersionRequest
  ): Promise<ExpenseCategoryVersionDto> =>
    httpClient<ExpenseCategoryVersionDto>(`${BASE}/legal-entities/${legalEntityId}/category-versions/${versionId}/publish`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // --- Expenses (Entity / Finance Workspaces) ---
  listExpenses: (
    legalEntityId: string,
    params?: {
      employeeId?: string | null;
      state?: string | null;
      search?: string | null;
      page?: number;
      pageSize?: number;
    }
  ): Promise<ExpensePage<ExpenseDto>> => {
    const q = new URLSearchParams();
    if (params?.employeeId) q.set("employeeId", params.employeeId);
    if (params?.state) q.set("state", params.state);
    if (params?.search) q.set("search", params.search);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseDto>>(`${BASE}/legal-entities/${legalEntityId}?${q.toString()}`);
  },

  createExpense: (legalEntityId: string, request: CreateExpenseRequest): Promise<ExpenseDto> => {
    const idempotencyKey = generateKey();
    return httpClient<ExpenseDto>(`${BASE}/legal-entities/${legalEntityId}`, {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
      body: JSON.stringify(request)
    });
  },

  getExpense: (legalEntityId: string, id: string): Promise<ExpenseDto> =>
    httpClient<ExpenseDto>(`${BASE}/legal-entities/${legalEntityId}/${id}`),

  listDuplicates: (
    legalEntityId: string,
    id: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseDuplicateCandidateDto>> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseDuplicateCandidateDto>>(
      `${BASE}/legal-entities/${legalEntityId}/${id}/duplicates?${q.toString()}`
    );
  },

  updateExpense: (legalEntityId: string, id: string, request: UpdateExpenseRequest): Promise<ExpenseDto> =>
    httpClient<ExpenseDto>(`${BASE}/legal-entities/${legalEntityId}/${id}`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  deleteExpense: (legalEntityId: string, id: string): Promise<{ readonly deleted: boolean }> =>
    httpClient<{ readonly deleted: boolean }>(`${BASE}/legal-entities/${legalEntityId}/${id}`, {
      method: "DELETE"
    }),

  submitExpense: (legalEntityId: string, id: string, request: ExpenseActionRequest): Promise<ExpenseDto> =>
    httpClient<ExpenseDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/submit`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  finalizeExpense: (legalEntityId: string, id: string, request: ExpenseActionRequest): Promise<ExpenseDto> =>
    httpClient<ExpenseDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/finalize`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  reverseExpense: (legalEntityId: string, id: string, request: ExpenseActionRequest): Promise<ExpenseDto> =>
    httpClient<ExpenseDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/reverse`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // --- Evidence, Upload, OCR ---
  listEvidence: (legalEntityId: string, id: string): Promise<readonly ExpenseEvidenceDto[]> =>
    httpClient<readonly ExpenseEvidenceDto[]>(`${BASE}/legal-entities/${legalEntityId}/${id}/evidence`),

  createEvidenceUploadIntent: (
    legalEntityId: string,
    id: string,
    request: CreateExpenseEvidenceUploadRequest
  ): Promise<ExpenseEvidenceUploadIntentDto> =>
    httpClient<ExpenseEvidenceUploadIntentDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/evidence/upload-intent`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  completeEvidenceUpload: (
    legalEntityId: string,
    id: string,
    request: CompleteExpenseEvidenceUploadRequest
  ): Promise<ExpenseEvidenceDto> =>
    httpClient<ExpenseEvidenceDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/evidence/complete`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  addEvidence: (legalEntityId: string, id: string, request: AddExpenseEvidenceRequest): Promise<ExpenseEvidenceDto> =>
    httpClient<ExpenseEvidenceDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/evidence`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  requestOcr: (legalEntityId: string, id: string, evidenceId: string): Promise<ExpenseOcrJobDto> =>
    httpClient<ExpenseOcrJobDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/evidence/${evidenceId}/ocr`, {
      method: "POST"
    }),

  getOcr: (legalEntityId: string, id: string, jobId: string): Promise<ExpenseOcrJobDto> =>
    httpClient<ExpenseOcrJobDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/ocr/${jobId}`),

  retryOcr: (legalEntityId: string, id: string, jobId: string): Promise<ExpenseOcrJobDto> =>
    httpClient<ExpenseOcrJobDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/ocr/${jobId}/retry`, {
      method: "POST"
    }),

  completeOcrManually: (
    legalEntityId: string,
    id: string,
    jobId: string,
    request: CompleteExpenseOcrManuallyRequest
  ): Promise<ExpenseOcrJobDto> =>
    httpClient<ExpenseOcrJobDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/ocr/${jobId}/manual`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  getEvidenceDownloadUrl: (
    legalEntityId: string,
    id: string,
    evidenceId: string
  ): Promise<{ readonly url: string }> =>
    httpClient<{ readonly url: string }>(`${BASE}/legal-entities/${legalEntityId}/${id}/evidence/${evidenceId}/download`),

  // --- Missing Receipt Declarations ---
  getMissingReceipt: (legalEntityId: string, id: string): Promise<MissingReceiptDeclarationDto | null> =>
    httpClient<MissingReceiptDeclarationDto | null>(`${BASE}/legal-entities/${legalEntityId}/${id}/missing-receipt`),

  declareMissingReceipt: (
    legalEntityId: string,
    id: string,
    request: DeclareMissingReceiptRequest
  ): Promise<MissingReceiptDeclarationDto> =>
    httpClient<MissingReceiptDeclarationDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/missing-receipt`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // --- Itemizations ---
  listItemizations: (legalEntityId: string, id: string): Promise<readonly ExpenseItemizationDto[]> =>
    httpClient<readonly ExpenseItemizationDto[]>(`${BASE}/legal-entities/${legalEntityId}/${id}/itemizations`),

  addItemization: (
    legalEntityId: string,
    id: string,
    request: CreateExpenseItemizationRequest
  ): Promise<ExpenseItemizationDto> =>
    httpClient<ExpenseItemizationDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/itemizations`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  deleteItemization: (legalEntityId: string, id: string, itemizationId: string): Promise<{ readonly deleted: boolean }> =>
    httpClient<{ readonly deleted: boolean }>(
      `${BASE}/legal-entities/${legalEntityId}/${id}/itemizations/${itemizationId}`,
      { method: "DELETE" }
    ),

  // --- Participants ---
  listParticipants: (legalEntityId: string, id: string): Promise<readonly ExpenseParticipantDto[]> =>
    httpClient<readonly ExpenseParticipantDto[]>(`${BASE}/legal-entities/${legalEntityId}/${id}/participants`),

  addParticipant: (
    legalEntityId: string,
    id: string,
    request: AddExpenseParticipantRequest
  ): Promise<ExpenseParticipantDto> =>
    httpClient<ExpenseParticipantDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/participants`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // --- Policy Evaluation & Exceptions ---
  evaluatePolicy: (legalEntityId: string, id: string): Promise<ExpensePolicyEvaluationDto> =>
    httpClient<ExpensePolicyEvaluationDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/evaluate-policy`, {
      method: "POST"
    }),

  listPolicyExceptions: (
    legalEntityId: string,
    id: string,
    params?: { state?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpensePolicyExceptionDto>> => {
    const q = new URLSearchParams();
    if (params?.state) q.set("state", params.state);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpensePolicyExceptionDto>>(
      `${BASE}/legal-entities/${legalEntityId}/${id}/policy-exceptions?${q.toString()}`
    );
  },

  requestPolicyException: (
    legalEntityId: string,
    id: string,
    request: RequestExpensePolicyExceptionRequest
  ): Promise<ExpensePolicyExceptionDto> =>
    httpClient<ExpensePolicyExceptionDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/policy-exceptions`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  decidePolicyException: (
    legalEntityId: string,
    id: string,
    exceptionId: string,
    request: DecideExpensePolicyExceptionRequest
  ): Promise<ExpensePolicyExceptionDto> =>
    httpClient<ExpensePolicyExceptionDto>(
      `${BASE}/legal-entities/${legalEntityId}/${id}/policy-exceptions/${exceptionId}/decision`,
      {
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  // --- Approvals ---
  requestApproval: (legalEntityId: string, id: string, request: ExpenseApprovalRequest): Promise<ExpenseApprovalDto> =>
    httpClient<ExpenseApprovalDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/approval/request`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  decideApproval: (
    legalEntityId: string,
    id: string,
    request: ExpenseApprovalDecisionRequest
  ): Promise<ExpenseApprovalDto> =>
    httpClient<ExpenseApprovalDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/approval/decision`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // --- History ---
  listHistory: (legalEntityId: string, id: string): Promise<readonly ExpenseHistoryDto[]> =>
    httpClient<readonly ExpenseHistoryDto[]>(`${BASE}/legal-entities/${legalEntityId}/${id}/history`),

  // --- Policy Administration ---
  listPolicies: (
    legalEntityId: string,
    params?: { status?: string | null; search?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpensePolicyDto>> => {
    const q = new URLSearchParams();
    if (params?.status) q.set("status", params.status);
    if (params?.search) q.set("search", params.search);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpensePolicyDto>>(`${BASE}/legal-entities/${legalEntityId}/policies?${q.toString()}`);
  },

  createPolicy: (legalEntityId: string, request: CreateExpensePolicyRequest): Promise<ExpensePolicyDto> =>
    httpClient<ExpensePolicyDto>(`${BASE}/legal-entities/${legalEntityId}/policies`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listPolicyVersions: (
    legalEntityId: string,
    policyId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpensePolicyVersionDto>> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpensePolicyVersionDto>>(
      `${BASE}/legal-entities/${legalEntityId}/policies/${policyId}/versions?${q.toString()}`
    );
  },

  createPolicyVersion: (
    legalEntityId: string,
    policyId: string,
    request: CreateExpensePolicyVersionRequest
  ): Promise<ExpensePolicyVersionDto> =>
    httpClient<ExpensePolicyVersionDto>(`${BASE}/legal-entities/${legalEntityId}/policies/${policyId}/versions`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  publishPolicyVersion: (
    legalEntityId: string,
    versionId: string,
    request: PublishExpensePolicyVersionRequest
  ): Promise<ExpensePolicyVersionDto> =>
    httpClient<ExpensePolicyVersionDto>(`${BASE}/legal-entities/${legalEntityId}/policy-versions/${versionId}/publish`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  retirePolicyVersion: (legalEntityId: string, versionId: string): Promise<ExpensePolicyVersionDto> =>
    httpClient<ExpensePolicyVersionDto>(`${BASE}/legal-entities/${legalEntityId}/policy-versions/${versionId}/retire`, {
      method: "POST"
    }),

  // --- Expense Reports ---
  listReports: (
    legalEntityId: string,
    params?: { state?: string | null; search?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseReportDto>> => {
    const q = new URLSearchParams();
    if (params?.state) q.set("state", params.state);
    if (params?.search) q.set("search", params.search);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseReportDto>>(`${BASE}/legal-entities/${legalEntityId}/reports?${q.toString()}`);
  },

  getReport: (legalEntityId: string, reportId: string): Promise<ExpenseReportDto> =>
    httpClient<ExpenseReportDto>(`${BASE}/legal-entities/${legalEntityId}/reports/${reportId}`),

  createReport: (legalEntityId: string, request: CreateExpenseReportRequest): Promise<ExpenseReportDto> =>
    httpClient<ExpenseReportDto>(`${BASE}/legal-entities/${legalEntityId}/reports`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listReportItems: (
    legalEntityId: string,
    reportId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseReportItemDto>> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseReportItemDto>>(
      `${BASE}/legal-entities/${legalEntityId}/reports/${reportId}/items?${q.toString()}`
    );
  },

  addReportItem: (
    legalEntityId: string,
    reportId: string,
    request: AddExpenseReportItemRequest
  ): Promise<ExpenseReportItemDto> =>
    httpClient<ExpenseReportItemDto>(`${BASE}/legal-entities/${legalEntityId}/reports/${reportId}/items`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  removeReportItem: (legalEntityId: string, reportId: string, expenseId: string): Promise<{ readonly removed: boolean }> =>
    httpClient<{ readonly removed: boolean }>(
      `${BASE}/legal-entities/${legalEntityId}/reports/${reportId}/items/${expenseId}`,
      { method: "DELETE" }
    ),

  listReportHistory: (legalEntityId: string, reportId: string): Promise<readonly ExpenseReportHistoryDto[]> =>
    httpClient<readonly ExpenseReportHistoryDto[]>(`${BASE}/legal-entities/${legalEntityId}/reports/${reportId}/history`),

  transitionReport: (
    legalEntityId: string,
    reportId: string,
    action: "submit" | "approve" | "reject" | "finalize",
    request: ExpenseActionRequest
  ): Promise<ExpenseReportDto> =>
    httpClient<ExpenseReportDto>(`${BASE}/legal-entities/${legalEntityId}/reports/${reportId}/${action}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // --- Reports & Exception Workbench ---
  getExceptionSummary: (legalEntityId: string): Promise<ExpenseExceptionSummaryDto> =>
    httpClient<ExpenseExceptionSummaryDto>(`${BASE}/legal-entities/${legalEntityId}/reports/exceptions`),

  getSpendReport: (
    legalEntityId: string,
    params?: {
      groupBy?: string | null;
      from?: string | null;
      to?: string | null;
      search?: string | null;
      page?: number;
      pageSize?: number;
    }
  ): Promise<ExpensePage<ExpenseSpendReportRowDto>> => {
    const q = new URLSearchParams();
    if (params?.groupBy) q.set("groupBy", params.groupBy);
    if (params?.from) q.set("from", params.from);
    if (params?.to) q.set("to", params.to);
    if (params?.search) q.set("search", params.search);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseSpendReportRowDto>>(
      `${BASE}/legal-entities/${legalEntityId}/reports/spend?${q.toString()}`
    );
  },

  exportReportsCsvUrl: (legalEntityId: string, params?: { state?: string | null; search?: string | null }): string => {
    const q = new URLSearchParams();
    if (params?.state) q.set("state", params.state);
    if (params?.search) q.set("search", params.search);
    return `${BASE}/legal-entities/${legalEntityId}/reports/export?${q.toString()}`;
  },

  // --- Import Expenses ---
  importExpenses: (legalEntityId: string, request: ImportExpensesRequest): Promise<ExpenseImportBatchDto> =>
    httpClient<ExpenseImportBatchDto>(`${BASE}/legal-entities/${legalEntityId}/imports/expenses`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  getImportBatch: (legalEntityId: string, batchId: string): Promise<ExpenseImportBatchDto> =>
    httpClient<ExpenseImportBatchDto>(`${BASE}/legal-entities/${legalEntityId}/imports/expenses/${batchId}`),

  listImportRows: (
    legalEntityId: string,
    batchId: string,
    params?: { status?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseImportRowDto>> => {
    const q = new URLSearchParams();
    if (params?.status) q.set("status", params.status);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseImportRowDto>>(
      `${BASE}/legal-entities/${legalEntityId}/imports/expenses/${batchId}/rows?${q.toString()}`
    );
  },

  // --- Business Advances ---
  listAdvances: (
    legalEntityId: string,
    params?: {
      state?: string | null;
      employeeId?: string | null;
      overdue?: boolean | null;
      page?: number;
      pageSize?: number;
    }
  ): Promise<ExpensePage<BusinessAdvanceDto>> => {
    const q = new URLSearchParams();
    if (params?.state) q.set("state", params.state);
    if (params?.employeeId) q.set("employeeId", params.employeeId);
    if (params?.overdue !== undefined && params?.overdue !== null) q.set("overdue", String(params.overdue));
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<BusinessAdvanceDto>>(`${BASE}/legal-entities/${legalEntityId}/advances?${q.toString()}`);
  },

  createAdvance: (legalEntityId: string, request: CreateBusinessAdvanceRequest): Promise<BusinessAdvanceDto> =>
    httpClient<BusinessAdvanceDto>(`${BASE}/legal-entities/${legalEntityId}/advances`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  approveAdvance: (legalEntityId: string, id: string, request: ExpenseActionRequest): Promise<BusinessAdvanceDto> =>
    httpClient<BusinessAdvanceDto>(`${BASE}/legal-entities/${legalEntityId}/advances/${id}/approve`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  issueAdvance: (legalEntityId: string, id: string, request: IssueBusinessAdvanceRequest): Promise<BusinessAdvanceDto> =>
    httpClient<BusinessAdvanceDto>(`${BASE}/legal-entities/${legalEntityId}/advances/${id}/issue`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  settleAdvance: (legalEntityId: string, id: string, request: ExpenseActionRequest): Promise<BusinessAdvanceDto> =>
    httpClient<BusinessAdvanceDto>(`${BASE}/legal-entities/${legalEntityId}/advances/${id}/settle`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  applyAdvance: (legalEntityId: string, id: string, request: ApplyBusinessAdvanceRequest): Promise<BusinessAdvanceDto> =>
    httpClient<BusinessAdvanceDto>(`${BASE}/legal-entities/${legalEntityId}/advances/${id}/apply`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  returnAdvance: (legalEntityId: string, id: string, request: ReturnBusinessAdvanceRequest): Promise<BusinessAdvanceDto> =>
    httpClient<BusinessAdvanceDto>(`${BASE}/legal-entities/${legalEntityId}/advances/${id}/return-payment`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  requestAdvanceWriteOff: (
    legalEntityId: string,
    id: string,
    request: RequestBusinessAdvanceWriteOffRequest
  ): Promise<BusinessAdvanceWriteOffDto> =>
    httpClient<BusinessAdvanceWriteOffDto>(`${BASE}/legal-entities/${legalEntityId}/advances/${id}/write-offs/request`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  decideAdvanceWriteOff: (
    legalEntityId: string,
    id: string,
    request: DecideBusinessAdvanceWriteOffRequest
  ): Promise<BusinessAdvanceWriteOffDto> =>
    httpClient<BusinessAdvanceWriteOffDto>(`${BASE}/legal-entities/${legalEntityId}/advances/${id}/write-offs/decide`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // --- Corporate Cards ---
  listCardTransactions: (
    legalEntityId: string,
    params?: { state?: string | null; search?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseCardTransactionDto>> => {
    const q = new URLSearchParams();
    if (params?.state) q.set("state", params.state);
    if (params?.search) q.set("search", params.search);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseCardTransactionDto>>(
      `${BASE}/legal-entities/${legalEntityId}/corporate-card/inbox?${q.toString()}`
    );
  },

  getCardReconciliation: (
    legalEntityId: string,
    params?: { from?: string | null; to?: string | null }
  ): Promise<ExpenseCardReconciliationDto> => {
    const q = new URLSearchParams();
    if (params?.from) q.set("from", params.from);
    if (params?.to) q.set("to", params.to);
    return httpClient<ExpenseCardReconciliationDto>(
      `${BASE}/legal-entities/${legalEntityId}/corporate-card/reconciliation?${q.toString()}`
    );
  },

  importCardTransaction: (
    legalEntityId: string,
    request: ImportExpenseCardTransactionRequest
  ): Promise<ExpenseCardTransactionDto> =>
    httpClient<ExpenseCardTransactionDto>(`${BASE}/legal-entities/${legalEntityId}/corporate-card/transactions/import`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createExpenseFromCard: (
    legalEntityId: string,
    cardId: string,
    request: CreateExpenseFromCardTransactionRequest
  ): Promise<ExpenseDto> =>
    httpClient<ExpenseDto>(
      `${BASE}/legal-entities/${legalEntityId}/corporate-card/transactions/${cardId}/create-expense`,
      {
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  clearCardTransaction: (legalEntityId: string, cardId: string): Promise<ExpenseCardTransactionDto> =>
    httpClient<ExpenseCardTransactionDto>(
      `${BASE}/legal-entities/${legalEntityId}/corporate-card/transactions/${cardId}/clear`,
      { method: "POST" }
    ),

  ignoreCardTransaction: (legalEntityId: string, cardId: string): Promise<ExpenseCardTransactionDto> =>
    httpClient<ExpenseCardTransactionDto>(
      `${BASE}/legal-entities/${legalEntityId}/corporate-card/transactions/${cardId}/ignore`,
      { method: "POST" }
    ),

  matchCard: (legalEntityId: string, cardId: string, request: MatchExpenseCardTransactionRequest): Promise<ExpenseCardTransactionDto> =>
    httpClient<ExpenseCardTransactionDto>(
      `${BASE}/legal-entities/${legalEntityId}/corporate-card/transactions/${cardId}/match`,
      {
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  unmatchCard: (legalEntityId: string, cardId: string): Promise<ExpenseCardTransactionDto> =>
    httpClient<ExpenseCardTransactionDto>(
      `${BASE}/legal-entities/${legalEntityId}/corporate-card/transactions/${cardId}/unmatch`,
      { method: "POST" }
    ),

  allocateCard: (
    legalEntityId: string,
    cardId: string,
    request: AllocateExpenseCardTransactionRequest
  ): Promise<ExpenseCardTransactionDto> =>
    httpClient<ExpenseCardTransactionDto>(
      `${BASE}/legal-entities/${legalEntityId}/corporate-card/transactions/${cardId}/allocate`,
      {
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  markCardPersonal: (
    legalEntityId: string,
    cardId: string,
    request: MarkExpenseCardPersonalRequest
  ): Promise<ExpenseCardTransactionDto> =>
    httpClient<ExpenseCardTransactionDto>(
      `${BASE}/legal-entities/${legalEntityId}/corporate-card/transactions/${cardId}/personal`,
      {
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  recordCardAdjustment: (
    legalEntityId: string,
    cardId: string,
    request: RecordExpenseCardAdjustmentRequest
  ): Promise<ExpenseCardAdjustmentDto> =>
    httpClient<ExpenseCardAdjustmentDto>(
      `${BASE}/legal-entities/${legalEntityId}/corporate-card/transactions/${cardId}/adjustments`,
      {
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  listCardAdjustments: (
    legalEntityId: string,
    params?: {
      cardTransactionId?: string | null;
      kind?: string | null;
      state?: string | null;
      search?: string | null;
      page?: number;
      pageSize?: number;
    }
  ): Promise<ExpensePage<ExpenseCardAdjustmentDto>> => {
    const q = new URLSearchParams();
    if (params?.cardTransactionId) q.set("cardTransactionId", params.cardTransactionId);
    if (params?.kind) q.set("kind", params.kind);
    if (params?.state) q.set("state", params.state);
    if (params?.search) q.set("search", params.search);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseCardAdjustmentDto>>(
      `${BASE}/legal-entities/${legalEntityId}/corporate-card/adjustments?${q.toString()}`
    );
  },

  // --- Employee Repayments ---
  listRepayments: (
    legalEntityId: string,
    params?: { employeeId?: string | null; state?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseEmployeeRepaymentDto>> => {
    const q = new URLSearchParams();
    if (params?.employeeId) q.set("employeeId", params.employeeId);
    if (params?.state) q.set("state", params.state);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseEmployeeRepaymentDto>>(
      `${BASE}/legal-entities/${legalEntityId}/employee-repayments?${q.toString()}`
    );
  },

  settleRepayment: (
    legalEntityId: string,
    repaymentId: string,
    request: SettleEmployeeRepaymentRequest
  ): Promise<ExpenseEmployeeRepaymentDto> =>
    httpClient<ExpenseEmployeeRepaymentDto>(
      `${BASE}/legal-entities/${legalEntityId}/employee-repayments/${repaymentId}/settle`,
      {
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  // --- Mileage & Per Diem & Rate Policies ---
  setMileage: (legalEntityId: string, id: string, request: SetMileageDetailRequest): Promise<MileageExpenseDetailDto> =>
    httpClient<MileageExpenseDetailDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/mileage`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  setMyMileage: (legalEntityId: string, id: string, request: SetMileageDetailRequest): Promise<MileageExpenseDetailDto> =>
    httpClient<MileageExpenseDetailDto>(`${BASE}/my/expenses/${id}/mileage?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  getMileage: (legalEntityId: string, id: string): Promise<MileageExpenseDetailDto> =>
    httpClient<MileageExpenseDetailDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/mileage`),

  setPerDiem: (legalEntityId: string, id: string, request: SetPerDiemDetailRequest): Promise<PerDiemExpenseDetailDto> =>
    httpClient<PerDiemExpenseDetailDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/per-diem`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  setMyPerDiem: (legalEntityId: string, id: string, request: SetPerDiemDetailRequest): Promise<PerDiemExpenseDetailDto> =>
    httpClient<PerDiemExpenseDetailDto>(`${BASE}/my/expenses/${id}/per-diem?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  listRatePolicies: (
    legalEntityId: string,
    params?: { kind?: string | null; status?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseRatePolicyDto>> => {
    const q = new URLSearchParams();
    if (params?.kind) q.set("kind", params.kind);
    if (params?.status) q.set("status", params.status);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseRatePolicyDto>>(
      `${BASE}/legal-entities/${legalEntityId}/rate-policies?${q.toString()}`
    );
  },

  listMyRatePolicies: (
    legalEntityId: string,
    params?: { kind?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseRatePolicyDto>> => {
    const q = new URLSearchParams({ legalEntityId });
    if (params?.kind) q.set("kind", params.kind);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseRatePolicyDto>>(`${BASE}/my/rate-policies?${q.toString()}`);
  },

  createRatePolicy: (legalEntityId: string, request: CreateExpenseRatePolicyRequest): Promise<ExpenseRatePolicyDto> =>
    httpClient<ExpenseRatePolicyDto>(`${BASE}/legal-entities/${legalEntityId}/rate-policies`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  publishRatePolicy: (
    legalEntityId: string,
    id: string,
    request: PublishExpenseRatePolicyRequest
  ): Promise<ExpenseRatePolicyDto> =>
    httpClient<ExpenseRatePolicyDto>(`${BASE}/legal-entities/${legalEntityId}/rate-policies/${id}/publish`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // --- Dimensions ---
  listDimensions: (legalEntityId: string, id: string): Promise<readonly ExpenseDimensionDto[]> =>
    httpClient<readonly ExpenseDimensionDto[]>(`${BASE}/legal-entities/${legalEntityId}/${id}/dimensions`),

  setDimension: (legalEntityId: string, id: string, request: SetExpenseDimensionRequest): Promise<ExpenseDimensionDto> =>
    httpClient<ExpenseDimensionDto>(`${BASE}/legal-entities/${legalEntityId}/${id}/dimensions`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  // --- Reimbursement Batches ---
  listReimbursementBatches: (legalEntityId: string): Promise<readonly ExpenseReimbursementBatchDto[]> =>
    httpClient<readonly ExpenseReimbursementBatchDto[]>(
      `${BASE}/legal-entities/${legalEntityId}/reimbursements/batches`
    ),

  prepareReimbursementBatch: (legalEntityId: string, currency?: string): Promise<ExpenseReimbursementBatchDto> => {
    const q = new URLSearchParams();
    if (currency) q.set("currency", currency);
    return httpClient<ExpenseReimbursementBatchDto>(
      `${BASE}/legal-entities/${legalEntityId}/reimbursements/prepare?${q.toString()}`,
      { method: "POST" }
    );
  },

  payReimbursementBatch: (
    legalEntityId: string,
    batchId: string,
    request: PayExpenseReimbursementBatchRequest
  ): Promise<ExpenseReimbursementBatchDto> =>
    httpClient<ExpenseReimbursementBatchDto>(
      `${BASE}/legal-entities/${legalEntityId}/reimbursements/${batchId}/pay`,
      {
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  // ==========================================
  // --- Employee Self-Service (/my) Routes ---
  // ==========================================
  listMyExpenses: (
    legalEntityId: string,
    params?: { state?: string | null; search?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseDto>> => {
    const q = new URLSearchParams();
    q.set("legalEntityId", legalEntityId);
    if (params?.state) q.set("state", params.state);
    if (params?.search) q.set("search", params.search);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseDto>>(`${BASE}/my/expenses?${q.toString()}`);
  },

  createMyExpense: (request: CreateMyExpenseRequest): Promise<ExpenseDto> => {
    const idempotencyKey = generateKey();
    return httpClient<ExpenseDto>(`${BASE}/my/expenses`, {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
      body: JSON.stringify(request)
    });
  },

  deleteMyExpense: (legalEntityId: string, id: string): Promise<{ readonly deleted: boolean }> => {
    const q = new URLSearchParams({ legalEntityId });
    return httpClient<{ readonly deleted: boolean }>(`${BASE}/my/expenses/${id}?${q.toString()}`, {
      method: "DELETE"
    });
  },

  submitMyExpense: (id: string, legalEntityId: string, request: ExpenseActionRequest): Promise<ExpenseDto> => {
    const q = new URLSearchParams({ legalEntityId });
    return httpClient<ExpenseDto>(`${BASE}/my/expenses/${id}/submit?${q.toString()}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  declareMissingReceiptSelf: (
    id: string,
    legalEntityId: string,
    request: DeclareMissingReceiptRequest
  ): Promise<MissingReceiptDeclarationDto> => {
    const q = new URLSearchParams({ legalEntityId });
    return httpClient<MissingReceiptDeclarationDto>(`${BASE}/my/expenses/${id}/missing-receipt?${q.toString()}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  requestApprovalSelf: (id: string, legalEntityId: string, request: ExpenseApprovalRequest): Promise<ExpenseApprovalDto> => {
    const q = new URLSearchParams({ legalEntityId });
    return httpClient<ExpenseApprovalDto>(`${BASE}/my/expenses/${id}/approval/request?${q.toString()}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  requestPolicyExceptionSelf: (
    id: string,
    legalEntityId: string,
    request: RequestExpensePolicyExceptionRequest
  ): Promise<ExpensePolicyExceptionDto> => {
    const q = new URLSearchParams({ legalEntityId });
    return httpClient<ExpensePolicyExceptionDto>(`${BASE}/my/expenses/${id}/policy-exceptions?${q.toString()}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  listMyCards: (
    legalEntityId: string,
    params?: { state?: string | null; search?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseCardTransactionDto>> => {
    const q = new URLSearchParams({ legalEntityId });
    if (params?.state) q.set("state", params.state);
    if (params?.search) q.set("search", params.search);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseCardTransactionDto>>(`${BASE}/my/corporate-card/transactions?${q.toString()}`);
  },

  createExpenseFromCardSelf: (
    cardId: string,
    legalEntityId: string,
    request: CreateExpenseFromCardTransactionRequest
  ): Promise<ExpenseDto> => {
    const q = new URLSearchParams({ legalEntityId });
    return httpClient<ExpenseDto>(
      `${BASE}/my/corporate-card/transactions/${cardId}/create-expense?${q.toString()}`,
      {
        method: "POST",
        body: JSON.stringify(request)
      }
    );
  },

  listMyAdvances: (
    legalEntityId: string,
    params?: { state?: string | null; overdue?: boolean | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<BusinessAdvanceDto>> => {
    const q = new URLSearchParams({ legalEntityId });
    if (params?.state) q.set("state", params.state);
    if (params?.overdue !== undefined && params?.overdue !== null) q.set("overdue", String(params.overdue));
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<BusinessAdvanceDto>>(`${BASE}/my/advances?${q.toString()}`);
  },

  createMyAdvance: (request: CreateBusinessAdvanceRequest): Promise<BusinessAdvanceDto> =>
    httpClient<BusinessAdvanceDto>(`${BASE}/my/advances`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listMyReimbursements: (
    legalEntityId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<ExpensePage<ReimbursementDto>> => {
    const q = new URLSearchParams({ legalEntityId });
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ReimbursementDto>>(`${BASE}/my/reimbursements?${q.toString()}`);
  },

  listMyRepayments: (
    legalEntityId: string,
    params?: { state?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseEmployeeRepaymentDto>> => {
    const q = new URLSearchParams({ legalEntityId });
    if (params?.state) q.set("state", params.state);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseEmployeeRepaymentDto>>(`${BASE}/my/repayments?${q.toString()}`);
  },

  listMyReports: (
    legalEntityId: string,
    params?: { state?: string | null; search?: string | null; page?: number; pageSize?: number }
  ): Promise<ExpensePage<ExpenseReportDto>> => {
    const q = new URLSearchParams({ legalEntityId });
    if (params?.state) q.set("state", params.state);
    if (params?.search) q.set("search", params.search);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<ExpensePage<ExpenseReportDto>>(`${BASE}/my/reports?${q.toString()}`);
  }
};
