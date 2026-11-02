import { httpClient } from "../../../core/api/http-client.ts";
import type {
  AccountingHealthView,
  ApproveGoLiveRequest,
  ClosePreviewRequest,
  CloseRunView,
  CloseCheckView,
  FinancialStatementDefinitionRequest,
  FinancialStatementDefinitionView,
  FinancialStatementView,
  FxRevaluationRequest,
  FxRevaluationReversalRequest,
  FxRevaluationRunView,
  GoLiveApprovalView,
  OwnerFinancialSummary,
  ReconciliationIssueView,
  ReconciliationIssuePageDto,
  ReconciliationPageDto,
  SubledgerReconciliationView,
  ReopenPeriodRequest,
  StatementKind
} from "../types.ts";

export const accountingApi = {
  getHealth: async (legalEntityId: string, asOf?: string): Promise<AccountingHealthView> => {
    const q = asOf ? `?asOf=${encodeURIComponent(asOf)}` : "";
    return httpClient<AccountingHealthView>(`/api/v1/accounting/legal-entities/${legalEntityId}/health${q}`);
  },

  getOwnerSummary: async (legalEntityId: string, asOf?: string): Promise<OwnerFinancialSummary> => {
    const q = asOf ? `?asOf=${encodeURIComponent(asOf)}` : "";
    return httpClient<OwnerFinancialSummary>(`/api/v1/accounting/legal-entities/${legalEntityId}/owner-summary${q}`);
  },

  runReconciliation: async (legalEntityId: string, type: string, asOf: string): Promise<SubledgerReconciliationView> => {
    return httpClient<SubledgerReconciliationView>(
      `/api/v1/accounting/legal-entities/${legalEntityId}/reconciliations/${encodeURIComponent(type)}?asOf=${encodeURIComponent(asOf)}`,
      { method: "POST" }
    );
  },

  listReconciliations: async (
    legalEntityId: string,
    params?: { type?: string; asOf?: string; limit?: number }
  ): Promise<readonly SubledgerReconciliationView[]> => {
    const search = new URLSearchParams();
    if (params?.type) search.set("type", params.type);
    if (params?.asOf) search.set("asOf", params.asOf);
    if (params?.limit) search.set("limit", String(params.limit));
    const qs = search.toString();
    return httpClient<readonly SubledgerReconciliationView[]>(
      `/api/v1/accounting/legal-entities/${legalEntityId}/reconciliations${qs ? `?${qs}` : ""}`
    );
  },

  listReconciliationsPage: async (
    legalEntityId: string,
    params?: { type?: string | undefined; asOf?: string | undefined; cursor?: string | undefined; page?: number | undefined; limit?: number | undefined }
  ): Promise<ReconciliationPageDto> => {
    const search = new URLSearchParams();
    if (params?.type) search.set("type", params.type);
    if (params?.asOf) search.set("asOf", params.asOf);
    if (params?.cursor) search.set("cursor", params.cursor);
    if (params?.page) search.set("page", String(params.page));
    if (params?.limit) search.set("limit", String(params.limit));
    const qs = search.toString();
    return httpClient<ReconciliationPageDto>(`/api/v1/accounting/legal-entities/${legalEntityId}/reconciliations/page${qs ? `?${qs}` : ""}`);
  },

  getReconciliation: async (legalEntityId: string, runId: string): Promise<SubledgerReconciliationView> => {
    return httpClient<SubledgerReconciliationView>(`/api/v1/accounting/legal-entities/${legalEntityId}/reconciliations/${runId}`);
  },

  listReconciliationIssues: async (
    legalEntityId: string,
    params?: { status?: string; limit?: number }
  ): Promise<readonly ReconciliationIssueView[]> => {
    const search = new URLSearchParams();
    if (params?.status) search.set("status", params.status);
    if (params?.limit) search.set("limit", String(params.limit));
    const qs = search.toString();
    return httpClient<readonly ReconciliationIssueView[]>(
      `/api/v1/accounting/legal-entities/${legalEntityId}/reconciliation-issues${qs ? `?${qs}` : ""}`
    );
  },

  listReconciliationIssuesPage: async (
    legalEntityId: string,
    params?: { status?: string | undefined; cursor?: string | undefined; page?: number | undefined; limit?: number | undefined }
  ): Promise<ReconciliationIssuePageDto> => {
    const search = new URLSearchParams();
    if (params?.status) search.set("status", params.status);
    if (params?.cursor) search.set("cursor", params.cursor);
    if (params?.page) search.set("page", String(params.page));
    if (params?.limit) search.set("limit", String(params.limit));
    const qs = search.toString();
    return httpClient<ReconciliationIssuePageDto>(`/api/v1/accounting/legal-entities/${legalEntityId}/reconciliation-issues/page${qs ? `?${qs}` : ""}`);
  },

  getCloseRun: async (legalEntityId: string, periodId: string): Promise<CloseRunView> => {
    return httpClient<CloseRunView>(`/api/v1/accounting/legal-entities/${legalEntityId}/close-runs/${periodId}`);
  },

  listCloseRunIssues: async (legalEntityId: string, periodId: string): Promise<readonly CloseCheckView[]> => {
    return httpClient<readonly CloseCheckView[]>(`/api/v1/accounting/legal-entities/${legalEntityId}/close-runs/${periodId}/issues`);
  },

  previewClose: async (legalEntityId: string, request: ClosePreviewRequest): Promise<CloseRunView> => {
    return httpClient<CloseRunView>(`/api/v1/accounting/legal-entities/${legalEntityId}/close/preview`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  closePeriod: async (legalEntityId: string, request: ClosePreviewRequest): Promise<CloseRunView> => {
    return httpClient<CloseRunView>(`/api/v1/accounting/legal-entities/${legalEntityId}/close`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  reopenPeriod: async (legalEntityId: string, periodId: string, request: ReopenPeriodRequest): Promise<CloseRunView> => {
    return httpClient<CloseRunView>(`/api/v1/accounting/legal-entities/${legalEntityId}/periods/${periodId}/reopen`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  approveGoLive: async (legalEntityId: string, request: ApproveGoLiveRequest): Promise<GoLiveApprovalView> => {
    return httpClient<GoLiveApprovalView>(`/api/v1/accounting/legal-entities/${legalEntityId}/opening/approve-go-live`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  previewFxRevaluation: async (legalEntityId: string, request: FxRevaluationRequest): Promise<FxRevaluationRunView> => {
    return httpClient<FxRevaluationRunView>(`/api/v1/accounting/legal-entities/${legalEntityId}/fx-revaluations/preview`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  postFxRevaluation: async (legalEntityId: string, runId: string): Promise<FxRevaluationRunView> => {
    return httpClient<FxRevaluationRunView>(`/api/v1/accounting/legal-entities/${legalEntityId}/fx-revaluations/${runId}/post`, {
      method: "POST"
    });
  },

  reverseFxRevaluation: async (
    legalEntityId: string,
    runId: string,
    request: FxRevaluationReversalRequest
  ): Promise<FxRevaluationRunView> => {
    return httpClient<FxRevaluationRunView>(`/api/v1/accounting/legal-entities/${legalEntityId}/fx-revaluations/${runId}/reverse`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  getFxRevaluation: async (legalEntityId: string, runId: string): Promise<FxRevaluationRunView> => {
    return httpClient<FxRevaluationRunView>(`/api/v1/accounting/legal-entities/${legalEntityId}/fx-revaluations/${runId}`);
  },

  getStatement: async (
    legalEntityId: string,
    kind: StatementKind,
    from: string,
    to: string
  ): Promise<FinancialStatementView> => {
    return httpClient<FinancialStatementView>(
      `/api/v1/accounting/legal-entities/${legalEntityId}/statements/${encodeURIComponent(kind)}?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`
    );
  },

  getStatementDefinition: async (legalEntityId: string, kind: string): Promise<FinancialStatementDefinitionView> => {
    return httpClient<FinancialStatementDefinitionView>(
      `/api/v1/accounting/legal-entities/${legalEntityId}/statement-definitions/${encodeURIComponent(kind)}`
    );
  },

  saveStatementDefinition: async (
    legalEntityId: string,
    request: FinancialStatementDefinitionRequest
  ): Promise<FinancialStatementDefinitionView> => {
    return httpClient<FinancialStatementDefinitionView>(
      `/api/v1/accounting/legal-entities/${legalEntityId}/statement-definitions`,
      {
        method: "PUT",
        body: JSON.stringify(request)
      }
    );
  }
};
