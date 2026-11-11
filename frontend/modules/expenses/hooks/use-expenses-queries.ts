import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { expensesApi } from "../services/expenses-api.ts";
import type {
  CreateExpenseRequest,
  CreateMyExpenseRequest,
  UpdateExpenseRequest,
  ExpenseActionRequest,
  ExpenseApprovalRequest,
  ExpenseApprovalDecisionRequest,
  CreateExpenseCategoryRequest,
  CreateExpenseCategoryVersionRequest,
  PublishExpenseCategoryVersionRequest,
  AddExpenseEvidenceRequest,
  CreateExpenseEvidenceUploadRequest,
  CompleteExpenseEvidenceUploadRequest,
  CompleteExpenseOcrManuallyRequest,
  DeclareMissingReceiptRequest,
  CreateExpenseItemizationRequest,
  AddExpenseParticipantRequest,
  RequestExpensePolicyExceptionRequest,
  DecideExpensePolicyExceptionRequest,
  CreateExpensePolicyRequest,
  CreateExpensePolicyVersionRequest,
  PublishExpensePolicyVersionRequest,
  CreateExpenseReportRequest,
  AddExpenseReportItemRequest,
  ImportExpensesRequest,
  CreateBusinessAdvanceRequest,
  IssueBusinessAdvanceRequest,
  ApplyBusinessAdvanceRequest,
  ReturnBusinessAdvanceRequest,
  RequestBusinessAdvanceWriteOffRequest,
  DecideBusinessAdvanceWriteOffRequest,
  ImportExpenseCardTransactionRequest,
  CreateExpenseFromCardTransactionRequest,
  RecordExpenseCardAdjustmentRequest,
  MatchExpenseCardTransactionRequest,
  AllocateExpenseCardTransactionRequest,
  MarkExpenseCardPersonalRequest,
  SettleEmployeeRepaymentRequest,
  SetMileageDetailRequest,
  SetPerDiemDetailRequest,
  CreateExpenseRatePolicyRequest,
  PublishExpenseRatePolicyRequest,
  SetExpenseDimensionRequest,
  PayExpenseReimbursementBatchRequest
} from "../types.ts";

export const expenseKeys = {
  all: ["expenses"] as const,

  // Categories
  categories: (legalEntityId: string) => [...expenseKeys.all, "categories", legalEntityId] as const,
  myCategories: (legalEntityId: string) => [...expenseKeys.all, "myCategories", legalEntityId] as const,

  // Expenses management
  list: (
    legalEntityId: string,
    params?: {
      employeeId?: string | null;
      state?: string | null;
      search?: string | null;
      page?: number;
      pageSize?: number;
    }
  ) => [...expenseKeys.all, "list", legalEntityId, params] as const,

  detail: (legalEntityId: string, id: string) => [...expenseKeys.all, "detail", legalEntityId, id] as const,
  duplicates: (legalEntityId: string, id: string, page?: number) =>
    [...expenseKeys.all, "duplicates", legalEntityId, id, page] as const,
  evidence: (legalEntityId: string, id: string) => [...expenseKeys.all, "evidence", legalEntityId, id] as const,
  ocr: (legalEntityId: string, id: string, jobId: string) =>
    [...expenseKeys.all, "ocr", legalEntityId, id, jobId] as const,
  missingReceipt: (legalEntityId: string, id: string) =>
    [...expenseKeys.all, "missingReceipt", legalEntityId, id] as const,
  itemizations: (legalEntityId: string, id: string) =>
    [...expenseKeys.all, "itemizations", legalEntityId, id] as const,
  participants: (legalEntityId: string, id: string) =>
    [...expenseKeys.all, "participants", legalEntityId, id] as const,
  policyExceptions: (legalEntityId: string, id: string, state?: string | null, page?: number) =>
    [...expenseKeys.all, "policyExceptions", legalEntityId, id, state, page] as const,
  history: (legalEntityId: string, id: string) => [...expenseKeys.all, "history", legalEntityId, id] as const,
  dimensions: (legalEntityId: string, id: string) => [...expenseKeys.all, "dimensions", legalEntityId, id] as const,

  // Policies
  policies: (legalEntityId: string, params?: { status?: string | null; search?: string | null; page?: number }) =>
    [...expenseKeys.all, "policies", legalEntityId, params] as const,
  policyVersions: (legalEntityId: string, policyId: string, page?: number) =>
    [...expenseKeys.all, "policyVersions", legalEntityId, policyId, page] as const,
  ratePolicies: (legalEntityId: string, params?: { kind?: string | null; status?: string | null; page?: number }) =>
    [...expenseKeys.all, "ratePolicies", legalEntityId, params] as const,
  myRatePolicies: (legalEntityId: string, params?: { kind?: string | null; page?: number }) =>
    [...expenseKeys.all, "myRatePolicies", legalEntityId, params] as const,

  // Reports
  reports: (legalEntityId: string, params?: { state?: string | null; search?: string | null; page?: number }) =>
    [...expenseKeys.all, "reports", legalEntityId, params] as const,
  reportDetail: (legalEntityId: string, id: string) => [...expenseKeys.all, "reportDetail", legalEntityId, id] as const,
  reportItems: (legalEntityId: string, id: string, page?: number) =>
    [...expenseKeys.all, "reportItems", legalEntityId, id, page] as const,
  reportHistory: (legalEntityId: string, id: string) =>
    [...expenseKeys.all, "reportHistory", legalEntityId, id] as const,
  exceptionSummary: (legalEntityId: string) => [...expenseKeys.all, "exceptionSummary", legalEntityId] as const,
  spendReport: (legalEntityId: string, params?: Record<string, unknown>) =>
    [...expenseKeys.all, "spendReport", legalEntityId, params] as const,

  // Advances
  advances: (legalEntityId: string, params?: Record<string, unknown>) =>
    [...expenseKeys.all, "advances", legalEntityId, params] as const,

  // Corporate Cards
  cards: (legalEntityId: string, params?: Record<string, unknown>) =>
    [...expenseKeys.all, "cards", legalEntityId, params] as const,
  cardReconciliation: (legalEntityId: string, params?: { from?: string | null; to?: string | null }) =>
    [...expenseKeys.all, "cardReconciliation", legalEntityId, params] as const,
  cardAdjustments: (legalEntityId: string, params?: Record<string, unknown>) =>
    [...expenseKeys.all, "cardAdjustments", legalEntityId, params] as const,

  // Reimbursement Batches
  reimbursementBatches: (legalEntityId: string) =>
    [...expenseKeys.all, "reimbursementBatches", legalEntityId] as const,

  // Repayments
  repayments: (legalEntityId: string, params?: Record<string, unknown>) =>
    [...expenseKeys.all, "repayments", legalEntityId, params] as const,

  // Imports
  importBatch: (legalEntityId: string, batchId: string) =>
    [...expenseKeys.all, "importBatch", legalEntityId, batchId] as const,
  importRows: (legalEntityId: string, batchId: string, status?: string | null, page?: number) =>
    [...expenseKeys.all, "importRows", legalEntityId, batchId, status, page] as const,

  // Self Service
  myExpenses: (legalEntityId: string, params?: Record<string, unknown>) =>
    [...expenseKeys.all, "myExpenses", legalEntityId, params] as const,
  myCards: (legalEntityId: string, params?: Record<string, unknown>) =>
    [...expenseKeys.all, "myCards", legalEntityId, params] as const,
  myAdvances: (legalEntityId: string, params?: Record<string, unknown>) =>
    [...expenseKeys.all, "myAdvances", legalEntityId, params] as const,
  myReimbursements: (legalEntityId: string, page?: number) =>
    [...expenseKeys.all, "myReimbursements", legalEntityId, page] as const,
  myRepayments: (legalEntityId: string, state?: string | null, page?: number) =>
    [...expenseKeys.all, "myRepayments", legalEntityId, state, page] as const,
  myReports: (legalEntityId: string, params?: Record<string, unknown>) =>
    [...expenseKeys.all, "myReports", legalEntityId, params] as const
};

// ==========================================
// --- Queries ---
// ==========================================

export function useExpenseCategoriesQuery(legalEntityId: string, enabled = true) {
  return useQuery({
    queryKey: expenseKeys.categories(legalEntityId),
    queryFn: () => expensesApi.listCategories(legalEntityId),
    enabled: Boolean(legalEntityId) && enabled
  });
}

export function useMyExpenseCategoriesQuery(legalEntityId: string, enabled = true) {
  return useQuery({
    queryKey: expenseKeys.myCategories(legalEntityId),
    queryFn: () => expensesApi.listMyCategories(legalEntityId),
    enabled: Boolean(legalEntityId) && enabled
  });
}

export function useExpensesListQuery(
  legalEntityId: string,
  params?: {
    employeeId?: string | null;
    state?: string | null;
    search?: string | null;
    page?: number;
    pageSize?: number;
  }
) {
  return useQuery({
    queryKey: expenseKeys.list(legalEntityId, params),
    queryFn: () => expensesApi.listExpenses(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useExpenseDetailQuery(legalEntityId: string, id: string) {
  return useQuery({
    queryKey: expenseKeys.detail(legalEntityId, id),
    queryFn: () => expensesApi.getExpense(legalEntityId, id),
    enabled: Boolean(legalEntityId && id)
  });
}

export function useExpenseEvidenceQuery(legalEntityId: string, id: string) {
  return useQuery({
    queryKey: expenseKeys.evidence(legalEntityId, id),
    queryFn: () => expensesApi.listEvidence(legalEntityId, id),
    enabled: Boolean(legalEntityId && id)
  });
}

export function useExpenseItemizationsQuery(legalEntityId: string, id: string) {
  return useQuery({
    queryKey: expenseKeys.itemizations(legalEntityId, id),
    queryFn: () => expensesApi.listItemizations(legalEntityId, id),
    enabled: Boolean(legalEntityId && id)
  });
}

export function useExpenseParticipantsQuery(legalEntityId: string, id: string) {
  return useQuery({
    queryKey: expenseKeys.participants(legalEntityId, id),
    queryFn: () => expensesApi.listParticipants(legalEntityId, id),
    enabled: Boolean(legalEntityId && id)
  });
}

export function useExpenseHistoryQuery(legalEntityId: string, id: string) {
  return useQuery({
    queryKey: expenseKeys.history(legalEntityId, id),
    queryFn: () => expensesApi.listHistory(legalEntityId, id),
    enabled: Boolean(legalEntityId && id)
  });
}

export function useExpenseDimensionsQuery(legalEntityId: string, id: string) {
  return useQuery({
    queryKey: expenseKeys.dimensions(legalEntityId, id),
    queryFn: () => expensesApi.listDimensions(legalEntityId, id),
    enabled: Boolean(legalEntityId && id)
  });
}

export function useExpenseExceptionSummaryQuery(legalEntityId: string) {
  return useQuery({
    queryKey: expenseKeys.exceptionSummary(legalEntityId),
    queryFn: () => expensesApi.getExceptionSummary(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useExpenseSpendReportQuery(
  legalEntityId: string,
  params?: {
    groupBy?: string | null;
    from?: string | null;
    to?: string | null;
    search?: string | null;
    page?: number;
    pageSize?: number;
  }
) {
  return useQuery({
    queryKey: expenseKeys.spendReport(legalEntityId, params as Record<string, unknown>),
    queryFn: () => expensesApi.getSpendReport(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useExpenseAdvancesQuery(
  legalEntityId: string,
  params?: {
    state?: string | null;
    employeeId?: string | null;
    overdue?: boolean | null;
    page?: number;
    pageSize?: number;
  }
) {
  return useQuery({
    queryKey: expenseKeys.advances(legalEntityId, params as Record<string, unknown>),
    queryFn: () => expensesApi.listAdvances(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useExpenseCardTransactionsQuery(
  legalEntityId: string,
  params?: { state?: string | null; search?: string | null; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: expenseKeys.cards(legalEntityId, params as Record<string, unknown>),
    queryFn: () => expensesApi.listCardTransactions(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useExpenseCardReconciliationQuery(
  legalEntityId: string,
  params?: { from?: string | null; to?: string | null }
) {
  return useQuery({
    queryKey: expenseKeys.cardReconciliation(legalEntityId, params),
    queryFn: () => expensesApi.getCardReconciliation(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useExpenseRepaymentsQuery(
  legalEntityId: string,
  params?: { employeeId?: string | null; state?: string | null; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: expenseKeys.repayments(legalEntityId, params as Record<string, unknown>),
    queryFn: () => expensesApi.listRepayments(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useExpensePoliciesQuery(
  legalEntityId: string,
  params?: { status?: string | null; search?: string | null; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: expenseKeys.policies(legalEntityId, params),
    queryFn: () => expensesApi.listPolicies(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useExpenseRatePoliciesQuery(
  legalEntityId: string,
  params?: { kind?: string | null; status?: string | null; page?: number; pageSize?: number },
  enabled = true
) {
  return useQuery({
    queryKey: expenseKeys.ratePolicies(legalEntityId, params),
    queryFn: () => expensesApi.listRatePolicies(legalEntityId, params),
    enabled: Boolean(legalEntityId) && enabled
  });
}

export function useMyExpenseRatePoliciesQuery(
  legalEntityId: string,
  params?: { kind?: string | null; page?: number; pageSize?: number },
  enabled = true
) {
  return useQuery({
    queryKey: expenseKeys.myRatePolicies(legalEntityId, params),
    queryFn: () => expensesApi.listMyRatePolicies(legalEntityId, params),
    enabled: Boolean(legalEntityId) && enabled
  });
}

export function useExpenseReportsQuery(
  legalEntityId: string,
  params?: { state?: string | null; search?: string | null; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: expenseKeys.reports(legalEntityId, params),
    queryFn: () => expensesApi.listReports(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useExpenseReimbursementBatchesQuery(legalEntityId: string) {
  return useQuery({
    queryKey: expenseKeys.reimbursementBatches(legalEntityId),
    queryFn: () => expensesApi.listReimbursementBatches(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

// --- Self-Service Queries ---
export function useMyExpensesQuery(
  legalEntityId: string,
  params?: { state?: string | null; search?: string | null; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: expenseKeys.myExpenses(legalEntityId, params as Record<string, unknown>),
    queryFn: () => expensesApi.listMyExpenses(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useMyCardsQuery(
  legalEntityId: string,
  params?: { state?: string | null; search?: string | null; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: expenseKeys.myCards(legalEntityId, params as Record<string, unknown>),
    queryFn: () => expensesApi.listMyCards(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useMyAdvancesQuery(
  legalEntityId: string,
  params?: { state?: string | null; overdue?: boolean | null; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: expenseKeys.myAdvances(legalEntityId, params as Record<string, unknown>),
    queryFn: () => expensesApi.listMyAdvances(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useMyReimbursementsQuery(
  legalEntityId: string,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: expenseKeys.myReimbursements(legalEntityId, params?.page),
    queryFn: () => expensesApi.listMyReimbursements(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useMyRepaymentsQuery(
  legalEntityId: string,
  params?: { state?: string | null; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: expenseKeys.myRepayments(legalEntityId, params?.state, params?.page),
    queryFn: () => expensesApi.listMyRepayments(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useMyReportsQuery(
  legalEntityId: string,
  params?: { state?: string | null; search?: string | null; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: expenseKeys.myReports(legalEntityId, params as Record<string, unknown>),
    queryFn: () => expensesApi.listMyReports(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

// ==========================================
// --- Mutations ---
// ==========================================

export function useCreateExpenseMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateExpenseRequest) => expensesApi.createExpense(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useCreateMyExpenseMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateMyExpenseRequest) => expensesApi.createMyExpense(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myExpenses", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useUpdateExpenseMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateExpenseRequest) => expensesApi.updateExpense(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.detail(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myExpenses", legalEntityId] });
    }
  });
}

export function useSubmitExpenseMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ExpenseActionRequest) => expensesApi.submitExpense(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.detail(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myExpenses", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useSubmitMyExpenseMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ExpenseActionRequest) => expensesApi.submitMyExpense(id, legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.detail(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myExpenses", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useFinalizeExpenseMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ExpenseActionRequest) => expensesApi.finalizeExpense(legalEntityId, id, request),
    onSuccess: (data) => {
      queryClient.setQueryData(expenseKeys.detail(legalEntityId, id), data);
      queryClient.invalidateQueries({ queryKey: expenseKeys.detail(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myExpenses", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "spendReport", legalEntityId] });
    }
  });
}

export function useReverseExpenseMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ExpenseActionRequest) => expensesApi.reverseExpense(legalEntityId, id, request),
    onSuccess: (data) => {
      queryClient.setQueryData(expenseKeys.detail(legalEntityId, id), data);
      queryClient.invalidateQueries({ queryKey: expenseKeys.detail(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myExpenses", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useDeclareMissingReceiptMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: DeclareMissingReceiptRequest) =>
      expensesApi.declareMissingReceipt(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.detail(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: expenseKeys.missingReceipt(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useDeclareMissingReceiptSelfMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: DeclareMissingReceiptRequest) =>
      expensesApi.declareMissingReceiptSelf(id, legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.detail(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: expenseKeys.missingReceipt(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myExpenses", legalEntityId] });
    }
  });
}

export function useRequestApprovalMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ExpenseApprovalRequest) => expensesApi.requestApproval(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.detail(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useDecideApprovalMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ExpenseApprovalDecisionRequest) => expensesApi.decideApproval(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.detail(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myExpenses", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useRequestPolicyExceptionMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RequestExpensePolicyExceptionRequest) =>
      expensesApi.requestPolicyException(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.detail(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "policyExceptions", legalEntityId, id] });
    }
  });
}

export function useDecidePolicyExceptionMutation(legalEntityId: string, id: string, exceptionId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: DecideExpensePolicyExceptionRequest) =>
      expensesApi.decidePolicyException(legalEntityId, id, exceptionId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.detail(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "policyExceptions", legalEntityId, id] });
    }
  });
}

export function useAddItemizationMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateExpenseItemizationRequest) =>
      expensesApi.addItemization(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.itemizations(legalEntityId, id) });
    }
  });
}

export function useDeleteItemizationMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (itemizationId: string) => expensesApi.deleteItemization(legalEntityId, id, itemizationId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.itemizations(legalEntityId, id) });
    }
  });
}

export function useAddParticipantMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AddExpenseParticipantRequest) =>
      expensesApi.addParticipant(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.participants(legalEntityId, id) });
    }
  });
}

export function useCreateCategoryMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateExpenseCategoryRequest) => expensesApi.createCategory(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.categories(legalEntityId) });
    }
  });
}

export function useCreateCategoryVersionMutation(legalEntityId: string, categoryId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateExpenseCategoryVersionRequest) =>
      expensesApi.createCategoryVersion(legalEntityId, categoryId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.categories(legalEntityId) });
    }
  });
}

export function usePublishCategoryVersionMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ versionId, request }: { versionId: string; request: PublishExpenseCategoryVersionRequest }) =>
      expensesApi.publishCategoryVersion(legalEntityId, versionId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: expenseKeys.categories(legalEntityId) });
    }
  });
}

export function useCreateAdvanceMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateBusinessAdvanceRequest) => expensesApi.createAdvance(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "advances", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myAdvances", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useCreateMyAdvanceMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateBusinessAdvanceRequest) => expensesApi.createMyAdvance(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myAdvances", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "advances", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useApproveAdvanceMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ExpenseActionRequest }) =>
      expensesApi.approveAdvance(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "advances", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myAdvances", legalEntityId] });
    }
  });
}

export function useIssueAdvanceMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: IssueBusinessAdvanceRequest }) =>
      expensesApi.issueAdvance(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "advances", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myAdvances", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useApplyAdvanceMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ApplyBusinessAdvanceRequest }) =>
      expensesApi.applyAdvance(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "advances", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myAdvances", legalEntityId] });
    }
  });
}

export function useReturnAdvanceMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ReturnBusinessAdvanceRequest }) =>
      expensesApi.returnAdvance(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "advances", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myAdvances", legalEntityId] });
    }
  });
}

export function useRequestAdvanceWriteOffMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: RequestBusinessAdvanceWriteOffRequest }) =>
      expensesApi.requestAdvanceWriteOff(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "advances", legalEntityId] });
    }
  });
}

export function useDecideAdvanceWriteOffMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: DecideBusinessAdvanceWriteOffRequest }) =>
      expensesApi.decideAdvanceWriteOff(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "advances", legalEntityId] });
    }
  });
}

export function useMatchCardMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cardId, request }: { cardId: string; request: MatchExpenseCardTransactionRequest }) =>
      expensesApi.matchCard(legalEntityId, cardId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "cards", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "cardReconciliation", legalEntityId] });
    }
  });
}

export function useCreateExpenseFromCardMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cardId, request }: { cardId: string; request: CreateExpenseFromCardTransactionRequest }) =>
      expensesApi.createExpenseFromCard(legalEntityId, cardId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "cards", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myExpenses", legalEntityId] });
    }
  });
}

export function useCreateExpenseFromCardSelfMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cardId, request }: { cardId: string; request: CreateExpenseFromCardTransactionRequest }) =>
      expensesApi.createExpenseFromCardSelf(cardId, legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myCards", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myExpenses", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
    }
  });
}

export function useMarkCardPersonalMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cardId, request }: { cardId: string; request: MarkExpenseCardPersonalRequest }) =>
      expensesApi.markCardPersonal(legalEntityId, cardId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "cards", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "repayments", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function useRecordCardAdjustmentMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cardId, request }: { cardId: string; request: RecordExpenseCardAdjustmentRequest }) =>
      expensesApi.recordCardAdjustment(legalEntityId, cardId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "cards", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "cardAdjustments", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "cardReconciliation", legalEntityId] });
    }
  });
}

export function useSettleRepaymentMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ repaymentId, request }: { repaymentId: string; request: SettleEmployeeRepaymentRequest }) =>
      expensesApi.settleRepayment(legalEntityId, repaymentId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "repayments", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myRepayments", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}

export function usePrepareBatchMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (currency?: string) => expensesApi.prepareReimbursementBatch(legalEntityId, currency),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: expenseKeys.reimbursementBatches(legalEntityId) });
    }
  });
}

export function usePayBatchMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ batchId, request }: { batchId: string; request: PayExpenseReimbursementBatchRequest }) =>
      expensesApi.payReimbursementBatch(legalEntityId, batchId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "myReimbursements", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: expenseKeys.reimbursementBatches(legalEntityId) });
    }
  });
}

export function useCreatePolicyMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateExpensePolicyRequest) => expensesApi.createPolicy(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "policies", legalEntityId] });
    }
  });
}

export function useCreatePolicyVersionMutation(legalEntityId: string, policyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateExpensePolicyVersionRequest) =>
      expensesApi.createPolicyVersion(legalEntityId, policyId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "policyVersions", legalEntityId, policyId] });
    }
  });
}

export function usePublishPolicyVersionMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ versionId, request }: { versionId: string; request: PublishExpensePolicyVersionRequest }) =>
      expensesApi.publishPolicyVersion(legalEntityId, versionId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "policies", legalEntityId] });
    }
  });
}

export function useCreateRatePolicyMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateExpenseRatePolicyRequest) => expensesApi.createRatePolicy(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "ratePolicies", legalEntityId] });
    }
  });
}

export function usePublishRatePolicyMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: PublishExpenseRatePolicyRequest }) =>
      expensesApi.publishRatePolicy(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "ratePolicies", legalEntityId] });
    }
  });
}

export function useImportExpensesMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ImportExpensesRequest) => expensesApi.importExpenses(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "list", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...expenseKeys.all, "exceptionSummary", legalEntityId] });
    }
  });
}
