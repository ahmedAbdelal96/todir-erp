import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { fixedAssetsApi } from "../services/fixed-assets-api.ts";
import type {
  CreateAssetCategoryRequest,
  CreateAssetLocationRequest,
  CreateAssetRequest,
  AssignAssetRequest,
  ReturnAssetRequest,
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
  ReviewCapitalizationCandidateRequest,
  FixedAssetDto,
  CipProjectDto,
  DepreciationRunDto,
  TransferCustodyRequest,
  ReturnCustodyRequest,
  RecordAuditFindingRequest,
  ChangeEstimateRequest,
  AddCostRequest,
  ProposeImpairmentRequest,
  MassTransferCustodyRequest
} from "../types.ts";

export const fixedAssetKeys = {
  all: ["fixed-assets"] as const,
  categories: () => [...fixedAssetKeys.all, "categories"] as const,
  locations: (legalEntityId?: string) => [...fixedAssetKeys.all, "locations", legalEntityId] as const,
  assets: (filters: Record<string, unknown>) => [...fixedAssetKeys.all, "assets", filters] as const,
  assetDetail: (assetId: string) => [...fixedAssetKeys.all, "asset", assetId] as const,
  timeline: (assetId: string) => [...fixedAssetKeys.all, "timeline", assetId] as const,
  drilldown: (assetId: string, bookId?: string) => [...fixedAssetKeys.all, "drilldown", assetId, bookId] as const,
  assignments: (assetId: string, asOfDate?: string) => [...fixedAssetKeys.all, "assignments", assetId, asOfDate] as const,
  myAssets: (legalEntityId: string, asOfDate?: string) => [...fixedAssetKeys.all, "myAssets", legalEntityId, asOfDate] as const,
  employeeAssets: (employeeId: string, legalEntityId: string) => [...fixedAssetKeys.all, "employeeAssets", employeeId, legalEntityId] as const,
  documents: (assetId: string) => [...fixedAssetKeys.all, "documents", assetId] as const,
  books: (legalEntityId?: string) => [...fixedAssetKeys.all, "books", legalEntityId] as const,
  bookBalance: (assetId: string, bookId: string) => [...fixedAssetKeys.all, "bookBalance", assetId, bookId] as const,
  schedule: (assetId: string, bookId: string, from?: string, to?: string) => [...fixedAssetKeys.all, "schedule", assetId, bookId, from, to] as const,
  campaigns: (legalEntityId?: string, status?: string) => [...fixedAssetKeys.all, "campaigns", legalEntityId, status] as const,
  candidates: (legalEntityId?: string, status?: string) => [...fixedAssetKeys.all, "candidates", legalEntityId, status] as const,
  reconciliation: (bookId: string, legalEntityId: string, asOfDate?: string) => [...fixedAssetKeys.all, "reconciliation", bookId, legalEntityId, asOfDate] as const,
  registerReport: (params: Record<string, unknown>) => [...fixedAssetKeys.all, "registerReport", params] as const
};

// ==========================================
// Queries
// ==========================================

export function useAssetCategoriesQuery(limit: number = 100) {
  return useQuery({
    queryKey: [...fixedAssetKeys.categories(), limit],
    queryFn: () => fixedAssetsApi.listCategories(limit)
  });
}

export function useAssetLocationsQuery(legalEntityId?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.locations(legalEntityId),
    queryFn: () => fixedAssetsApi.listLocations(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useAssetsQuery(params: {
  legalEntityId?: string | undefined;
  state?: string | undefined;
  search?: string | undefined;
  limit?: number | undefined;
  offset?: number | undefined;
}) {
  return useQuery({
    queryKey: fixedAssetKeys.assets(params),
    queryFn: () => fixedAssetsApi.listAssets(params),
    enabled: Boolean(params.legalEntityId)
  });
}

export function useAssetTimelineQuery(assetId?: string, limit: number = 100) {
  return useQuery({
    queryKey: fixedAssetKeys.timeline(assetId ?? ""),
    queryFn: () => fixedAssetsApi.getAssetTimeline(assetId!, limit),
    enabled: Boolean(assetId)
  });
}

export function useAssetSourceDrilldownQuery(assetId?: string, bookId?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.drilldown(assetId ?? "", bookId),
    queryFn: () => fixedAssetsApi.listAssetSourceDrilldown(assetId!, bookId),
    enabled: Boolean(assetId)
  });
}

export function useAssetAssignmentsQuery(assetId?: string, asOfDate?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.assignments(assetId ?? "", asOfDate),
    queryFn: () => fixedAssetsApi.listAssignments(assetId!, asOfDate),
    enabled: Boolean(assetId)
  });
}

export function useMyAssetsQuery(legalEntityId?: string, asOfDate?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.myAssets(legalEntityId ?? "", asOfDate),
    queryFn: () => fixedAssetsApi.listMyAssets(legalEntityId!, asOfDate),
    enabled: Boolean(legalEntityId)
  });
}

export function useEmployeeAssetsQuery(employeeId?: string, legalEntityId?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.employeeAssets(employeeId ?? "", legalEntityId ?? ""),
    queryFn: () => fixedAssetsApi.listEmployeeAssets(employeeId!, legalEntityId!),
    enabled: Boolean(employeeId && legalEntityId)
  });
}

export function useAssetDocumentsQuery(assetId?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.documents(assetId ?? ""),
    queryFn: () => fixedAssetsApi.listAssetDocuments(assetId!),
    enabled: Boolean(assetId)
  });
}

export function useAssetBooksQuery(legalEntityId?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.books(legalEntityId),
    queryFn: () => fixedAssetsApi.listBooks(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useAssetBookBalanceQuery(assetId?: string, bookId?: string, legalEntityId?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.bookBalance(assetId ?? "", bookId ?? ""),
    queryFn: () => fixedAssetsApi.getAssetBookBalance(assetId!, bookId!, legalEntityId!),
    enabled: Boolean(assetId && bookId && legalEntityId)
  });
}

export function useAssetDepreciationScheduleQuery(assetId?: string, bookId?: string, legalEntityId?: string, from?: string, to?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.schedule(assetId ?? "", bookId ?? "", from, to),
    queryFn: () => fixedAssetsApi.getDepreciationSchedule(assetId!, bookId!, legalEntityId!, from!, to!),
    enabled: Boolean(assetId && bookId && legalEntityId && from && to)
  });
}

export function useVerificationCampaignsQuery(legalEntityId?: string, status?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.campaigns(legalEntityId, status),
    queryFn: () => fixedAssetsApi.listVerificationCampaigns(legalEntityId, status),
    enabled: Boolean(legalEntityId)
  });
}

export function useCapitalizationCandidatesQuery(legalEntityId?: string, status?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.candidates(legalEntityId, status),
    queryFn: () => fixedAssetsApi.listCapitalizationCandidates(legalEntityId, status),
    enabled: Boolean(legalEntityId)
  });
}

export function useAssetGlReconciliationQuery(bookId?: string, legalEntityId?: string, asOfDate?: string) {
  return useQuery({
    queryKey: fixedAssetKeys.reconciliation(bookId ?? "", legalEntityId ?? "", asOfDate),
    queryFn: () => fixedAssetsApi.reconcileAssetBook(bookId!, legalEntityId!, asOfDate),
    enabled: Boolean(bookId && legalEntityId)
  });
}

export function useAssetRegisterReportQuery(params: {
  legalEntityId?: string | undefined;
  asOfDate?: string | undefined;
  bookId?: string | undefined;
  state?: string | undefined;
  search?: string | undefined;
  limit?: number | undefined;
  offset?: number | undefined;
}) {
  return useQuery({
    queryKey: fixedAssetKeys.registerReport(params),
    queryFn: () => fixedAssetsApi.listAssetRegisterReport({
      legalEntityId: params.legalEntityId!,
      asOfDate: params.asOfDate,
      bookId: params.bookId,
      state: params.state,
      search: params.search,
      limit: params.limit,
      offset: params.offset
    }),
    enabled: Boolean(params.legalEntityId)
  });
}

// ==========================================
// Mutations
// ==========================================

export function useCreateAssetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateAssetRequest) => fixedAssetsApi.createAsset(request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.assets({ legalEntityId: variables.legalEntityId }) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCreateCategoryMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateAssetCategoryRequest) => fixedAssetsApi.createCategory(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.categories() });
    }
  });
}

export function useCreateLocationMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateAssetLocationRequest) => fixedAssetsApi.createLocation(request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.locations(variables.legalEntityId) });
    }
  });
}

export function useAssignAssetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: AssignAssetRequest }) =>
      fixedAssetsApi.assignAsset(assetId, request),
    onSuccess: (_, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.assignments(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.timeline(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useTransferAssetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: AssignAssetRequest }) =>
      fixedAssetsApi.transferAsset(assetId, request),
    onSuccess: (_, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.assignments(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.timeline(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useReturnAssetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: ReturnAssetRequest }) =>
      fixedAssetsApi.returnAsset(assetId, request),
    onSuccess: (_, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.assignments(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.timeline(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCreateHandoverMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: CreateAssetHandoverRequest }) =>
      fixedAssetsApi.createHandover(assetId, request),
    onSuccess: (_, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.assignments(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.timeline(assetId) });
    }
  });
}

export function useAddAssetDocumentMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: AddAssetDocumentRequest }) =>
      fixedAssetsApi.addAssetDocument(assetId, request),
    onSuccess: (_, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.documents(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.timeline(assetId) });
    }
  });
}

export function useCreateBookMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateBookRequest) => fixedAssetsApi.createBook(request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.books(variables.legalEntityId) });
    }
  });
}

export function useAddAssetBookMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: CreateAssetBookRequest }) =>
      fixedAssetsApi.addAssetBook(assetId, request),
    onSuccess: (_, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.assetDetail(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function usePrepareDepreciationRunMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateDepreciationRunRequest) => fixedAssetsApi.prepareDepreciationRun(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function usePostDepreciationRunMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ runId, legalEntityId }: { runId: string; legalEntityId: string }) =>
      fixedAssetsApi.postDepreciationRun(runId, legalEntityId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useReverseDepreciationRunMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ runId, legalEntityId, request }: { runId: string; legalEntityId: string; request: ReverseDepreciationRunRequest }) =>
      fixedAssetsApi.reverseDepreciationRun(runId, legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useReviewCapitalizationCandidateMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ candidateId, request }: { candidateId: string; request: ReviewCapitalizationCandidateRequest }) =>
      fixedAssetsApi.reviewCapitalizationCandidate(candidateId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useAllocateCapitalizationCandidateMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ candidateId, request }: { candidateId: string; request: AllocateCapitalizationCandidateRequest }) =>
      fixedAssetsApi.allocateCapitalizationCandidate(candidateId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCorrectSourceAllocationMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ candidateId, allocationId, request }: { candidateId: string; allocationId: string; request: CorrectSourceAllocationRequest }) =>
      fixedAssetsApi.correctSourceAllocation(candidateId, allocationId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCreateCipAssetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateCipAssetRequest) => fixedAssetsApi.createCipAsset(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useAddCipCostSourceMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: AddCipCostSourceRequest }) =>
      fixedAssetsApi.addCipCostSource(assetId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCapitalizeCipMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: CapitalizeCipRequest }) =>
      fixedAssetsApi.capitalizeCip(assetId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useSplitCipMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: SplitCipRequest }) =>
      fixedAssetsApi.splitCip(assetId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useSplitGroupedAssetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: SplitGroupedAssetRequest }) =>
      fixedAssetsApi.splitGroupedAsset(assetId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCombineCipMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CombineCipRequest) => fixedAssetsApi.combineCip(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useChangeAssetBookCostMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, bookId, request }: { assetId: string; bookId: string; request: ChangeAssetBookCostRequest }) =>
      fixedAssetsApi.changeAssetBookCost(assetId, bookId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCreateAssetBookEstimateChangeMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, bookId, request }: { assetId: string; bookId: string; request: CreateAssetBookEstimateChangeRequest }) =>
      fixedAssetsApi.createAssetBookEstimateChange(assetId, bookId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCreateAssetImpairmentMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, bookId, request }: { assetId: string; bookId: string; request: CreateAssetImpairmentRequest }) =>
      fixedAssetsApi.createAssetImpairment(assetId, bookId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useRequestAssetImpairmentApprovalMutation() {
  return useMutation({
    mutationFn: ({ assetId, bookId, request }: { assetId: string; bookId: string; request: RequestAssetImpairmentApprovalRequest }) =>
      fixedAssetsApi.requestAssetImpairmentApproval(assetId, bookId, request)
  });
}

export function useCreateAssetRevaluationMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, bookId, request }: { assetId: string; bookId: string; request: CreateAssetRevaluationRequest }) =>
      fixedAssetsApi.createAssetRevaluation(assetId, bookId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useRequestAssetRevaluationApprovalMutation() {
  return useMutation({
    mutationFn: ({ assetId, bookId, request }: { assetId: string; bookId: string; request: RequestAssetRevaluationApprovalRequest }) =>
      fixedAssetsApi.requestAssetRevaluationApproval(assetId, bookId, request)
  });
}

export function useDisposeAssetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: DisposeAssetRequest }) =>
      fixedAssetsApi.disposeAsset(assetId, request),
    onSuccess: (_, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.assetDetail(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.timeline(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useSellAssetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: SellAssetRequest }) =>
      fixedAssetsApi.sellAsset(assetId, request),
    onSuccess: (_, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.assetDetail(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.timeline(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useReinstateAssetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assetId, request }: { assetId: string; request: ReinstateAssetRequest }) =>
      fixedAssetsApi.reinstateAsset(assetId, request),
    onSuccess: (_, { assetId }) => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.assetDetail(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.timeline(assetId) });
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCreateVerificationCampaignMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateVerificationCampaignRequest) => fixedAssetsApi.createVerificationCampaign(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useVerifyAssetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ campaignId, assetId, request }: { campaignId: string; assetId: string; request: VerifyAssetRequest }) =>
      fixedAssetsApi.verifyAsset(campaignId, assetId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCloseVerificationCampaignMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (campaignId: string) => fixedAssetsApi.closeVerificationCampaign(campaignId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useExecuteMassTransferMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: MassTransferRequest) => fixedAssetsApi.executeMassTransfer(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

// ==========================================
// Module UI Convenience Wrappers & Aliases
// ==========================================

export function useFixedAssetsQuery(
  legalEntityId?: string | undefined,
  options?: {
    search?: string | undefined;
    state?: string | undefined;
    nature?: string | undefined;
    locationId?: string | undefined;
    pageNumber?: number | undefined;
    pageSize?: number | undefined;
  } | undefined
) {
  const query = useAssetsQuery({
    legalEntityId,
    search: options?.search,
    state: options?.state,
    limit: options?.pageSize ?? 50,
    offset: ((options?.pageNumber ?? 1) - 1) * (options?.pageSize ?? 50)
  });

  const rawItems = query.data ?? [];
  const transformed: FixedAssetDto[] = rawItems.map((a) => ({
    ...a,
    assetCode: a.assetNumber,
    assetName: a.name,
    categoryName: a.assetNature,
    nature: a.assetNature,
    inServiceDate: a.placedInServiceDate ?? a.acquisitionDate ?? null,
    depreciationMethod: "StraightLine"
  }));

  return {
    ...query,
    data: {
      items: transformed,
      totalCount: transformed.length
    }
  };
}

export function useFixedAssetCategoriesQuery(legalEntityId?: string) {
  const query = useAssetCategoriesQuery(100);
  const rawItems = query.data ?? [];
  const items = rawItems.map((c) => ({
    ...c,
    categoryName: c.name,
    categoryCode: c.code,
    nature: c.assetNature
  }));
  return {
    ...query,
    data: { items }
  };
}

export function useFixedAssetLocationsQuery(legalEntityId?: string) {
  const query = useAssetLocationsQuery(legalEntityId);
  const rawItems = query.data ?? [];
  const items = rawItems.map((l) => ({
    ...l,
    locationName: l.name,
    locationCode: l.code,
    assetCount: 0
  }));
  return {
    ...query,
    data: { items }
  };
}

export function useFixedAssetDetailQuery(legalEntityId: string, assetId: string) {
  const query = useAssetsQuery({ legalEntityId });
  const rawItems = query.data ?? [];
  const found = rawItems.find((a) => a.id === assetId);

  const asset: FixedAssetDto | null = found
    ? {
        ...found,
        assetCode: found.assetNumber,
        assetName: found.name,
        categoryName: found.assetNature,
        nature: found.assetNature,
        inServiceDate: found.placedInServiceDate ?? found.acquisitionDate ?? null,
        depreciationMethod: "StraightLine"
      }
    : null;

  return {
    ...query,
    data: asset
  };
}

export function useAssetCustodyHistoryQuery(legalEntityId: string, assetId: string) {
  const query = useAssetAssignmentsQuery(assetId);
  const raw = query.data ?? [];
  const items = raw.map((a) => ({
    id: a.id,
    actionType: a.assignmentType,
    custodianId: a.custodianName ?? a.employeeId ?? a.departmentId,
    locationName: a.locationId ?? "",
    actionDate: a.effectiveFrom,
    notes: a.condition ?? a.accessories
  }));
  return { ...query, data: { items } };
}

export function useCipProjectsQuery(legalEntityId?: string) {
  const query = useAssetsQuery({ legalEntityId, state: "UnderConstruction" });
  const raw = query.data ?? [];
  const items: CipProjectDto[] = raw.map((c) => ({
    ...c,
    assetId: c.id,
    totalCost: 0,
    costSourceCount: 0,
    projectCode: c.assetNumber,
    projectName: c.name,
    description: c.description ?? null,
    accumulatedCost: 0,
    estimatedCompletionDate: c.placedInServiceDate ?? null,
    status: c.lifecycleState
  }));
  return { ...query, data: { items } };
}

export function useDepreciationRunsQuery(legalEntityId?: string) {
  return useQuery({
    queryKey: [...fixedAssetKeys.all, "depreciationRuns", legalEntityId],
    queryFn: async (): Promise<{ items: readonly DepreciationRunDto[] }> => {
      const [runs, books] = await Promise.all([
        fixedAssetsApi.listDepreciationRuns(legalEntityId),
        legalEntityId ? fixedAssetsApi.listBooks(legalEntityId) : Promise.resolve([])
      ]);
      const bookMap = new Map(books.map((b) => [b.id, b]));
      const items: DepreciationRunDto[] = runs.map((r) => {
        const startDate = new Date(r.periodStart);
        const book = bookMap.get(r.bookId);
        return {
          ...r,
          periodYear: startDate.getFullYear(),
          periodMonth: startDate.getMonth() + 1,
          cutoffDate: r.periodEnd,
          description: `مسير إهلاك ${r.periodStart} - ${r.periodEnd}`,
          totalAssetsCount: r.lineCount,
          totalDepreciationAmount: r.totalAmount,
          currency: book?.currencyCode ?? "",
          glJournalEntryId: null
        };
      });
      return { items };
    }
  });
}

export function useBookGlReconciliationQuery(legalEntityId?: string, bookId?: string) {
  return useQuery({
    queryKey: [...fixedAssetKeys.all, "bookGlReconciliation", legalEntityId, bookId],
    queryFn: async () => {
      if (!legalEntityId) return null;
      let targetBookId = bookId;
      if (!targetBookId) {
        const books = await fixedAssetsApi.listBooks(legalEntityId);
        if (books.length > 0) {
          targetBookId = books[0]!.id;
        }
      }
      if (!targetBookId) return null;
      const rec = await fixedAssetsApi.reconcileAssetBook(targetBookId, legalEntityId);
      return {
        ...rec,
        subledgerAssetCost: rec.assetGrossCost,
        glAssetCost: rec.ledgerGrossCost,
        costVariance: rec.grossCostDifference,
        subledgerAccDep: rec.assetAccumulatedDepreciation,
        glAccDep: rec.ledgerAccumulatedDepreciation,
        accDepVariance: rec.accumulatedDepreciationDifference,
        isReconciled: rec.status === "Healthy"
      };
    },
    enabled: Boolean(legalEntityId)
  });
}

export function useMyAssignedAssetsQuery(legalEntityId?: string) {
  const query = useMyAssetsQuery(legalEntityId);
  const raw = query.data ?? [];
  const items: FixedAssetDto[] = raw.map((a) => ({
    id: a.assetId,
    legalEntityId: a.legalEntityId,
    categoryId: "",
    assetNumber: a.assetNumber,
    name: a.name,
    assetCode: a.assetNumber,
    assetName: a.name,
    categoryName: "أصل عهدة",
    lifecycleState: "Active",
    assetNature: "Equipment",
    trackingGranularity: "Individual",
    quantity: 1,
    createdAt: a.effectiveFrom,
    rowVersion: 1,
    inServiceDate: a.effectiveFrom,
    depreciationMethod: "StraightLine"
  }));
  return { ...query, data: { items } };
}

export function useTransferCustodyMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ assetId, payload }: { assetId: string; payload: TransferCustodyRequest }) => {
      return fixedAssetsApi.transferAsset(assetId, {
        assignmentType: payload.targetCustodianId ? "Employee" : "Location",
        employeeId: payload.targetCustodianId,
        departmentId: payload.targetDepartment,
        locationId: payload.targetLocationId,
        condition: payload.handoverNote,
        effectiveFrom: new Date().toISOString().slice(0, 10)
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useReturnCustodyMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ assetId, payload }: { assetId: string; payload: ReturnCustodyRequest }) => {
      return fixedAssetsApi.returnAsset(assetId, {
        locationId: payload.targetLocationId,
        condition: payload.condition,
        effectiveFrom: new Date().toISOString().slice(0, 10)
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useRecordAuditFindingMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ campaignId, payload }: { campaignId: string; payload: RecordAuditFindingRequest }) => {
      if (!payload.assetId) throw new Error("assetId is required");
      return fixedAssetsApi.verifyAsset(campaignId, payload.assetId, {
        result: payload.status,
        observedLocationId: payload.actualLocationId,
        notes: payload.notes
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useChangeEstimateMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ assetId, payload, bookId }: { assetId: string; payload: ChangeEstimateRequest; bookId?: string }) => {
      let targetBookId = bookId;
      if (!targetBookId && legalEntityId) {
        const books = await fixedAssetsApi.listBooks(legalEntityId);
        targetBookId = books[0]?.id;
      }
      if (!targetBookId) throw new Error("bookId is required");
      return fixedAssetsApi.createAssetBookEstimateChange(assetId, targetBookId, {
        legalEntityId: legalEntityId ?? "",
        effectiveFrom: payload.effectiveDate,
        usefulLifeMonths: payload.remainingLifeMonths,
        residualValue: payload.salvageValue,
        reason: payload.justification,
        idempotencyKey: `est-${assetId}-${Date.now()}`
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useAddCostMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ assetId, payload, bookId }: { assetId: string; payload: AddCostRequest; bookId?: string }) => {
      let targetBookId = bookId;
      if (!targetBookId && legalEntityId) {
        const books = await fixedAssetsApi.listBooks(legalEntityId);
        targetBookId = books[0]?.id;
      }
      if (!targetBookId) throw new Error("bookId is required");
      return fixedAssetsApi.changeAssetBookCost(assetId, targetBookId, {
        legalEntityId: legalEntityId ?? "",
        amount: payload.additionAmount,
        effectiveDate: payload.additionDate,
        reason: payload.description,
        idempotencyKey: `cost-${assetId}-${Date.now()}`
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useProposeImpairmentMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ assetId, payload, bookId }: { assetId: string; payload: ProposeImpairmentRequest; bookId?: string }) => {
      let targetBookId = bookId;
      if (!targetBookId && legalEntityId) {
        const books = await fixedAssetsApi.listBooks(legalEntityId);
        targetBookId = books[0]?.id;
      }
      if (!targetBookId) throw new Error("bookId is required");
      return fixedAssetsApi.requestAssetImpairmentApproval(assetId, targetBookId, {
        legalEntityId: legalEntityId ?? "",
        bookId: targetBookId,
        amount: payload.impairmentLoss,
        effectiveDate: payload.impairmentDate,
        reason: payload.reason
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useMassTransferMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: MassTransferCustodyRequest) => {
      return fixedAssetsApi.executeMassTransfer({
        legalEntityId: legalEntityId ?? "",
        assetIds: payload.assetIds,
        assignment: {
          assignmentType: payload.targetCustodianId ? "Employee" : "Location",
          employeeId: payload.targetCustodianId,
          departmentId: payload.targetDepartment,
          locationId: payload.targetLocationId,
          condition: payload.handoverNote,
          effectiveFrom: new Date().toISOString().slice(0, 10)
        },
        idempotencyKey: `mass-${Date.now()}`,
        reason: payload.handoverNote ?? "Mass transfer"
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useAllocateToCipMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ candidateId, cipProjectId, amount }: { candidateId: string; cipProjectId: string; amount?: number }) => {
      return fixedAssetsApi.addCipCostSource(cipProjectId, {
        sourceType: "Candidate",
        sourceReference: candidateId,
        amount: amount ?? 50000
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

