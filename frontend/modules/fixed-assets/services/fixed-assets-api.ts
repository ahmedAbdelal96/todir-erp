import { httpClient } from "../../../core/api/http-client.ts";
import type {
  AssetCategoryView,
  AssetView,
  AssetAssignmentView,
  AssetLocationView,
  AssetHandoverView,
  AssetDocumentView,
  AssetScanView,
  AssetTimelineEventView,
  VerificationCampaignView,
  AssetBookView,
  DepreciationRunView,
  DisposalView,
  AssetSaleView,
  ReinstatementView,
  CapitalizationCandidateView,
  SourceAllocationView,
  SourceAllocationCorrectionView,
  CipAssetView,
  CipSplitView,
  GroupedAssetSplitView,
  CombineCipView,
  AssetBookCostChangeView,
  AssetBookEstimateChangeView,
  AssetDepreciationScheduleLineView,
  AssetImpairmentView,
  AssetRevaluationView,
  AssetBookBalanceView,
  AssetGlReconciliationView,
  AssetRegisterRowView,
  AssetSourceDrilldownView,
  MassTransferPreviewView,
  MassTransferView,
  CreateAssetCategoryRequest,
  CreateAssetRequest,
  AssignAssetRequest,
  ReturnAssetRequest,
  CreateAssetLocationRequest,
  CreateAssetHandoverRequest,
  AddAssetDocumentRequest,
  CreateVerificationCampaignRequest,
  VerifyAssetRequest,
  MassTransferRequest,
  CreateBookRequest,
  CreateAssetBookRequest,
  CreateDepreciationRunRequest,
  ReverseDepreciationRunRequest,
  DisposeAssetRequest,
  SellAssetRequest,
  ReinstateAssetRequest,
  AllocateCapitalizationCandidateRequest,
  CorrectSourceAllocationRequest,
  CreateCipAssetRequest,
  AddCipCostSourceRequest,
  CapitalizeCipRequest,
  SplitCipRequest,
  SplitGroupedAssetRequest,
  CombineCipRequest,
  ChangeAssetBookCostRequest,
  CreateAssetBookEstimateChangeRequest,
  CreateAssetImpairmentRequest,
  RequestAssetImpairmentApprovalRequest,
  CreateAssetRevaluationRequest,
  RequestAssetRevaluationApprovalRequest,
  ReviewCapitalizationCandidateRequest
} from "../types.ts";

const BASE = "/api/v1/fixed-assets";

export const fixedAssetsApi = {
  // Categories
  listCategories: (limit: number = 100): Promise<readonly AssetCategoryView[]> =>
    httpClient<readonly AssetCategoryView[]>(`${BASE}/categories?limit=${limit}`),

  createCategory: (request: CreateAssetCategoryRequest): Promise<AssetCategoryView> =>
    httpClient<AssetCategoryView>(`${BASE}/categories`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Locations
  listLocations: (legalEntityId?: string): Promise<readonly AssetLocationView[]> => {
    const params = legalEntityId ? `?legalEntityId=${encodeURIComponent(legalEntityId)}` : "";
    return httpClient<readonly AssetLocationView[]>(`${BASE}/locations${params}`);
  },

  createLocation: (request: CreateAssetLocationRequest): Promise<AssetLocationView> =>
    httpClient<AssetLocationView>(`${BASE}/locations`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Assets Register
  listAssets: (params: {
    legalEntityId?: string;
    state?: string;
    search?: string;
    limit?: number;
    offset?: number;
  }): Promise<readonly AssetView[]> => {
    const q = new URLSearchParams();
    if (params.legalEntityId) q.set("legalEntityId", params.legalEntityId);
    if (params.state) q.set("state", params.state);
    if (params.search) q.set("search", params.search);
    if (params.limit !== undefined) q.set("limit", String(params.limit));
    if (params.offset !== undefined) q.set("offset", String(params.offset));
    return httpClient<readonly AssetView[]>(`${BASE}/assets?${q.toString()}`);
  },

  createAsset: (request: CreateAssetRequest): Promise<AssetView> =>
    httpClient<AssetView>(`${BASE}/assets`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Detail / Timeline / Drilldown
  getAssetTimeline: (assetId: string, limit: number = 100): Promise<readonly AssetTimelineEventView[]> =>
    httpClient<readonly AssetTimelineEventView[]>(`${BASE}/assets/${assetId}/timeline?limit=${limit}`),

  listAssetSourceDrilldown: (assetId: string, bookId?: string, limit: number = 100): Promise<readonly AssetSourceDrilldownView[]> => {
    const q = new URLSearchParams({ limit: String(limit) });
    if (bookId) q.set("bookId", bookId);
    return httpClient<readonly AssetSourceDrilldownView[]>(`${BASE}/assets/${assetId}/source-drilldown?${q.toString()}`);
  },

  // Scan & Tag Lookup
  getAssetScan: (assetId: string): Promise<AssetScanView> =>
    httpClient<AssetScanView>(`${BASE}/assets/${assetId}/scan`),

  scanAssetTag: (legalEntityId: string, assetTag: string): Promise<AssetScanView> =>
    httpClient<AssetScanView>(`${BASE}/assets/scan?legalEntityId=${encodeURIComponent(legalEntityId)}&assetTag=${encodeURIComponent(assetTag)}`),

  // Custody / Assignments / Return
  listAssignments: (assetId: string, asOfDate?: string): Promise<readonly AssetAssignmentView[]> => {
    const q = asOfDate ? `?asOfDate=${encodeURIComponent(asOfDate)}` : "";
    return httpClient<readonly AssetAssignmentView[]>(`${BASE}/assets/${assetId}/assignments${q}`);
  },

  assignAsset: (assetId: string, request: AssignAssetRequest): Promise<AssetAssignmentView> =>
    httpClient<AssetAssignmentView>(`${BASE}/assets/${assetId}/assignments`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  transferAsset: (assetId: string, request: AssignAssetRequest): Promise<AssetAssignmentView> =>
    httpClient<AssetAssignmentView>(`${BASE}/assets/${assetId}/transfer`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  returnAsset: (assetId: string, request: ReturnAssetRequest): Promise<AssetAssignmentView> =>
    httpClient<AssetAssignmentView>(`${BASE}/assets/${assetId}/return`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createHandover: (assetId: string, request: CreateAssetHandoverRequest): Promise<AssetHandoverView> =>
    httpClient<AssetHandoverView>(`${BASE}/assets/${assetId}/handovers`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Employee Self-Service & Custody Reads
  listMyAssets: (legalEntityId: string, asOfDate?: string): Promise<readonly AssetAssignmentView[]> => {
    const q = new URLSearchParams({ legalEntityId });
    if (asOfDate) q.set("asOfDate", asOfDate);
    return httpClient<readonly AssetAssignmentView[]>(`${BASE}/my-assets?${q.toString()}`);
  },

  listEmployeeAssets: (employeeId: string, legalEntityId: string, asOfDate?: string): Promise<readonly AssetAssignmentView[]> => {
    const q = new URLSearchParams({ legalEntityId });
    if (asOfDate) q.set("asOfDate", asOfDate);
    return httpClient<readonly AssetAssignmentView[]>(`${BASE}/employees/${employeeId}/assets?${q.toString()}`);
  },

  // Documents
  listAssetDocuments: (assetId: string): Promise<readonly AssetDocumentView[]> =>
    httpClient<readonly AssetDocumentView[]>(`${BASE}/assets/${assetId}/documents`),

  addAssetDocument: (assetId: string, request: AddAssetDocumentRequest): Promise<AssetDocumentView> =>
    httpClient<AssetDocumentView>(`${BASE}/assets/${assetId}/documents`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Mass Transfers
  previewMassTransfer: (request: MassTransferRequest): Promise<MassTransferPreviewView> =>
    httpClient<MassTransferPreviewView>(`${BASE}/mass-transfers/preview`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  executeMassTransfer: (request: MassTransferRequest): Promise<MassTransferView> =>
    httpClient<MassTransferView>(`${BASE}/mass-transfers`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Verification Campaigns
  listVerificationCampaigns: (legalEntityId?: string, status?: string, limit: number = 100): Promise<readonly VerificationCampaignView[]> => {
    const q = new URLSearchParams({ limit: String(limit) });
    if (legalEntityId) q.set("legalEntityId", legalEntityId);
    if (status) q.set("status", status);
    return httpClient<readonly VerificationCampaignView[]>(`${BASE}/verification/campaigns?${q.toString()}`);
  },

  createVerificationCampaign: (request: CreateVerificationCampaignRequest): Promise<VerificationCampaignView> =>
    httpClient<VerificationCampaignView>(`${BASE}/verification/campaigns`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  verifyAsset: (campaignId: string, assetId: string, request: VerifyAssetRequest): Promise<{ readonly status: string }> =>
    httpClient<{ readonly status: string }>(`${BASE}/verification/campaigns/${campaignId}/items/${assetId}/verify`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  closeVerificationCampaign: (campaignId: string): Promise<VerificationCampaignView> =>
    httpClient<VerificationCampaignView>(`${BASE}/verification/campaigns/${campaignId}/close`, {
      method: "POST",
      body: "{}"
    }),

  // Books
  listBooks: (legalEntityId?: string): Promise<readonly AssetBookView[]> => {
    const q = legalEntityId ? `?legalEntityId=${encodeURIComponent(legalEntityId)}` : "";
    return httpClient<readonly AssetBookView[]>(`${BASE}/books${q}`);
  },

  createBook: (request: CreateBookRequest): Promise<AssetBookView> =>
    httpClient<AssetBookView>(`${BASE}/books`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  addAssetBook: (assetId: string, request: CreateAssetBookRequest): Promise<{ readonly assetId: string; readonly bookId: string }> =>
    httpClient<{ readonly assetId: string; readonly bookId: string }>(`${BASE}/assets/${assetId}/books`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  getAssetBookBalance: (assetId: string, bookId: string, legalEntityId: string): Promise<AssetBookBalanceView> =>
    httpClient<AssetBookBalanceView>(`${BASE}/assets/${assetId}/books/${bookId}/balance?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  getDepreciationSchedule: (assetId: string, bookId: string, legalEntityId: string, from: string, to: string): Promise<readonly AssetDepreciationScheduleLineView[]> =>
    httpClient<readonly AssetDepreciationScheduleLineView[]>(`${BASE}/assets/${assetId}/books/${bookId}/schedule?legalEntityId=${encodeURIComponent(legalEntityId)}&from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`),

  // Depreciation Runs
  prepareDepreciationRun: (request: CreateDepreciationRunRequest): Promise<DepreciationRunView> =>
    httpClient<DepreciationRunView>(`${BASE}/depreciation-runs/prepare`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  postDepreciationRun: (runId: string, legalEntityId: string): Promise<DepreciationRunView> =>
    httpClient<DepreciationRunView>(`${BASE}/depreciation-runs/${runId}/post?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: "{}"
    }),

  reverseDepreciationRun: (runId: string, legalEntityId: string, request: ReverseDepreciationRunRequest): Promise<DepreciationRunView> =>
    httpClient<DepreciationRunView>(`${BASE}/depreciation-runs/${runId}/reverse?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Capitalization Candidates
  listCapitalizationCandidates: (legalEntityId?: string, status?: string, limit: number = 100): Promise<readonly CapitalizationCandidateView[]> => {
    const q = new URLSearchParams({ limit: String(limit) });
    if (legalEntityId) q.set("legalEntityId", legalEntityId);
    if (status) q.set("status", status);
    return httpClient<readonly CapitalizationCandidateView[]>(`${BASE}/capitalization-candidates?${q.toString()}`);
  },

  createCapitalizationCandidateFromInvoice: (legalEntityId: string, invoiceId: string): Promise<CapitalizationCandidateView> =>
    httpClient<CapitalizationCandidateView>(`${BASE}/capitalization-candidates/from-invoice`, {
      method: "POST",
      body: JSON.stringify({ legalEntityId, invoiceId })
    }),

  createCapitalizationCandidateFromInventory: (legalEntityId: string, productVariantId: string, asOfDate: string, serialId?: string): Promise<CapitalizationCandidateView> =>
    httpClient<CapitalizationCandidateView>(`${BASE}/capitalization-candidates/from-inventory`, {
      method: "POST",
      body: JSON.stringify({ legalEntityId, productVariantId, serialId, asOfDate })
    }),

  createCapitalizationCandidateFromExpense: (legalEntityId: string, expenseId: string): Promise<CapitalizationCandidateView> =>
    httpClient<CapitalizationCandidateView>(`${BASE}/capitalization-candidates/from-expense`, {
      method: "POST",
      body: JSON.stringify({ legalEntityId, expenseId })
    }),

  createCapitalizationCandidateFromPurchasing: (legalEntityId: string, purchaseOrderId: string): Promise<CapitalizationCandidateView> =>
    httpClient<CapitalizationCandidateView>(`${BASE}/capitalization-candidates/from-purchasing`, {
      method: "POST",
      body: JSON.stringify({ legalEntityId, purchaseOrderId })
    }),

  reviewCapitalizationCandidate: (candidateId: string, request: ReviewCapitalizationCandidateRequest): Promise<CapitalizationCandidateView> =>
    httpClient<CapitalizationCandidateView>(`${BASE}/capitalization-candidates/${candidateId}/review`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  allocateCapitalizationCandidate: (candidateId: string, request: AllocateCapitalizationCandidateRequest): Promise<SourceAllocationView> =>
    httpClient<SourceAllocationView>(`${BASE}/capitalization-candidates/${candidateId}/allocations`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  correctSourceAllocation: (candidateId: string, allocationId: string, request: CorrectSourceAllocationRequest): Promise<SourceAllocationCorrectionView> =>
    httpClient<SourceAllocationCorrectionView>(`${BASE}/capitalization-candidates/${candidateId}/allocations/${allocationId}/correct`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // CIP Operations
  createCipAsset: (request: CreateCipAssetRequest): Promise<CipAssetView> =>
    httpClient<CipAssetView>(`${BASE}/cip-assets`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  addCipCostSource: (assetId: string, request: AddCipCostSourceRequest): Promise<CipAssetView> =>
    httpClient<CipAssetView>(`${BASE}/cip-assets/${assetId}/cost-sources`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  capitalizeCip: (assetId: string, request: CapitalizeCipRequest): Promise<AssetView> =>
    httpClient<AssetView>(`${BASE}/cip-assets/${assetId}/capitalize`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  splitCip: (assetId: string, request: SplitCipRequest): Promise<CipSplitView> =>
    httpClient<CipSplitView>(`${BASE}/cip-assets/${assetId}/split`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  splitGroupedAsset: (assetId: string, request: SplitGroupedAssetRequest): Promise<GroupedAssetSplitView> =>
    httpClient<GroupedAssetSplitView>(`${BASE}/assets/${assetId}/split-grouped`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  combineCip: (request: CombineCipRequest): Promise<CombineCipView> =>
    httpClient<CombineCipView>(`${BASE}/cip-assets/combine`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Financial Changes (Cost, Estimate, Impairment, Revaluation)
  changeAssetBookCost: (assetId: string, bookId: string, request: ChangeAssetBookCostRequest): Promise<AssetBookCostChangeView> =>
    httpClient<AssetBookCostChangeView>(`${BASE}/assets/${assetId}/books/${bookId}/cost-change`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createAssetBookEstimateChange: (assetId: string, bookId: string, request: CreateAssetBookEstimateChangeRequest): Promise<AssetBookEstimateChangeView> =>
    httpClient<AssetBookEstimateChangeView>(`${BASE}/assets/${assetId}/books/${bookId}/estimate-change`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createAssetImpairment: (assetId: string, bookId: string, request: CreateAssetImpairmentRequest): Promise<AssetImpairmentView> =>
    httpClient<AssetImpairmentView>(`${BASE}/assets/${assetId}/books/${bookId}/impairments`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  requestAssetImpairmentApproval: (assetId: string, bookId: string, request: RequestAssetImpairmentApprovalRequest): Promise<{ readonly requestId: string }> =>
    httpClient<{ readonly requestId: string }>(`${BASE}/assets/${assetId}/books/${bookId}/impairment-approval-requests`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createAssetRevaluation: (assetId: string, bookId: string, request: CreateAssetRevaluationRequest): Promise<AssetRevaluationView> =>
    httpClient<AssetRevaluationView>(`${BASE}/assets/${assetId}/books/${bookId}/revaluations`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  requestAssetRevaluationApproval: (assetId: string, bookId: string, request: RequestAssetRevaluationApprovalRequest): Promise<{ readonly requestId: string }> =>
    httpClient<{ readonly requestId: string }>(`${BASE}/assets/${assetId}/books/${bookId}/revaluation-approval-requests`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Disposal & Reinstatement
  disposeAsset: (assetId: string, request: DisposeAssetRequest): Promise<DisposalView> =>
    httpClient<DisposalView>(`${BASE}/assets/${assetId}/disposal`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  sellAsset: (assetId: string, request: SellAssetRequest): Promise<AssetSaleView> =>
    httpClient<AssetSaleView>(`${BASE}/assets/${assetId}/sell`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  reinstateAsset: (assetId: string, request: ReinstateAssetRequest): Promise<ReinstatementView> =>
    httpClient<ReinstatementView>(`${BASE}/assets/${assetId}/reinstate`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Reconciliation & Reporting
  reconcileAssetBook: (bookId: string, legalEntityId: string, asOfDate?: string): Promise<AssetGlReconciliationView> => {
    const q = asOfDate ? `&asOfDate=${encodeURIComponent(asOfDate)}` : "";
    return httpClient<AssetGlReconciliationView>(`${BASE}/books/${bookId}/reconciliation?legalEntityId=${encodeURIComponent(legalEntityId)}${q}`);
  },

  listAssetRegisterReport: (params: {
    legalEntityId: string;
    asOfDate?: string;
    bookId?: string;
    state?: string;
    search?: string;
    limit?: number;
    offset?: number;
  }): Promise<{ readonly legalEntityId: string; readonly asOfDate: string; readonly items: readonly AssetRegisterRowView[] }> => {
    const q = new URLSearchParams({ legalEntityId: params.legalEntityId });
    if (params.asOfDate) q.set("asOfDate", params.asOfDate);
    if (params.bookId) q.set("bookId", params.bookId);
    if (params.state) q.set("state", params.state);
    if (params.search) q.set("search", params.search);
    if (params.limit !== undefined) q.set("limit", String(params.limit));
    if (params.offset !== undefined) q.set("offset", String(params.offset));
    return httpClient<{ readonly legalEntityId: string; readonly asOfDate: string; readonly items: readonly AssetRegisterRowView[] }>(`${BASE}/reports/register?${q.toString()}`);
  },

  getCountryBookDefaults: (legalEntityId: string, bookCode: string): Promise<Record<string, unknown>> =>
    httpClient<Record<string, unknown>>(`${BASE}/country-policy/book-defaults?legalEntityId=${encodeURIComponent(legalEntityId)}&bookCode=${encodeURIComponent(bookCode)}`)
};
