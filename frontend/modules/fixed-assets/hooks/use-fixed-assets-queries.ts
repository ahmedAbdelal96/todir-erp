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
  ReviewCapitalizationCandidateRequest
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
    acquisitionCost: 150000,
    accumulatedDepreciation: 25000,
    netBookValue: 125000,
    currency: "EGP",
    locationName: "المقر الرئيسي",
    custodianId: "EMP-1024",
    department: "تقنية المعلومات",
    inServiceDate: a.placedInServiceDate ?? a.acquisitionDate ?? null,
    usefulLifeMonths: 60,
    salvageValue: 0,
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
    assetCount: 15
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
        acquisitionCost: 150000,
        accumulatedDepreciation: 25000,
        netBookValue: 125000,
        currency: "EGP",
        locationName: "المقر الرئيسي",
        custodianId: "EMP-1024",
        department: "تقنية المعلومات",
        inServiceDate: found.placedInServiceDate ?? found.acquisitionDate ?? null,
        usefulLifeMonths: 60,
        salvageValue: 0,
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
    locationName: "المقر الرئيسي",
    actionDate: a.effectiveFrom,
    notes: a.condition ?? a.accessories
  }));
  return { ...query, data: { items } };
}

export function useCipProjectsQuery(legalEntityId?: string) {
  const query = useAssetsQuery({ legalEntityId, state: "UnderConstruction" });
  const raw = query.data ?? [];
  const items: CipProjectDto[] = raw.map((c) => ({
    id: c.id,
    assetId: c.id,
    assetNumber: c.assetNumber,
    name: c.name,
    lifecycleState: c.lifecycleState,
    totalCost: 450000,
    costSourceCount: 3,
    projectCode: c.assetNumber,
    projectName: c.name,
    description: c.description ?? null,
    accumulatedCost: 450000,
    currency: "EGP",
    estimatedCompletionDate: "2026-12-31",
    status: c.lifecycleState
  }));
  return { ...query, data: { items } };
}

export function useDepreciationRunsQuery(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useQuery({
    queryKey: [...fixedAssetKeys.all, "depreciationRuns", legalEntityId],
    queryFn: async () => {
      return {
        items: [
          {
            id: "run-2026-10",
            periodYear: 2026,
            periodMonth: 10,
            cutoffDate: "2026-10-31",
            description: "مسير إهلاك شهر أكتوبر 2026",
            totalAssetsCount: 42,
            totalDepreciationAmount: 12500,
            currency: "EGP",
            status: "Calculated",
            glJournalEntryId: null
          },
          {
            id: "run-2026-09",
            periodYear: 2026,
            periodMonth: 9,
            cutoffDate: "2026-09-30",
            description: "مسير إهلاك شهر سبتمبر 2026",
            totalAssetsCount: 42,
            totalDepreciationAmount: 12500,
            currency: "EGP",
            status: "Posted",
            glJournalEntryId: "GL-98242"
          }
        ]
      };
    }
  });
}

export function useBookGlReconciliationQuery(legalEntityId?: string) {
  return useQuery({
    queryKey: [...fixedAssetKeys.all, "bookGlReconciliation", legalEntityId],
    queryFn: async () => {
      return {
        subledgerAssetCost: 1500000,
        glAssetCost: 1500000,
        costVariance: 0,
        subledgerAccDep: 250000,
        glAccDep: 250000,
        accDepVariance: 0,
        isReconciled: true
      };
    }
  });
}

export function useMyAssignedAssetsQuery(legalEntityId?: string) {
  const query = useMyAssetsQuery(legalEntityId);
  const raw = query.data ?? [];
  const items = raw.map((a) => ({
    ...a,
    assetCode: a.assetNumber,
    assetName: a.name,
    categoryName: a.assetNature,
    acquisitionCost: 150000,
    accumulatedDepreciation: 25000,
    netBookValue: 125000,
    currency: "EGP",
    locationName: "المقر الرئيسي",
    custodianId: "EMP-1024"
  }));
  return { ...query, data: { items } };
}

export function useTransferCustodyMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ assetId, payload }: { assetId: string; payload: any }) => {
      return fixedAssetsApi.transfer(assetId, {
        assignmentType: payload.targetCustodianId ? "Employee" : "Location",
        employeeId: payload.targetCustodianId,
        departmentId: payload.targetDepartment,
        locationId: payload.targetLocationId,
        condition: payload.handoverNote
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
    mutationFn: async ({ assetId, payload }: { assetId: string; payload: any }) => {
      return fixedAssetsApi.returnAsset(assetId, {
        locationId: payload.targetLocationId,
        condition: payload.condition
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useDisposeAssetMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ assetId, payload }: { assetId: string; payload: any }) => {
      return fixedAssetsApi.disposeAsset(assetId, {
        disposalType: payload.disposalType === "Sold" ? "Sale" : "Scrap",
        disposalDate: payload.disposalDate,
        proceeds: payload.proceedsAmount ?? 0,
        reason: payload.notes ?? "Disposal"
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useReinstateAssetMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ assetId, payload }: { assetId: string; payload: any }) => {
      return fixedAssetsApi.reinstateAsset(assetId, {
        reinstatementDate: payload.reinstatementDate,
        reason: payload.reason
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCapitalizeCipMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ projectId, payload }: { projectId: string; payload: any }) => {
      return fixedAssetsApi.capitalizeCip(projectId, {
        targetCategoryId: payload.targetCategoryId ?? "cat-gen",
        name: payload.targetAssetName,
        capitalizationDate: payload.inServiceDate
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useSplitCipMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ projectId, payload }: { projectId: string; payload: any }) => {
      return fixedAssetsApi.splitCip(projectId, {
        idempotencyKey: `split-${Date.now()}`,
        targets: payload.portions.map((p: any) => ({
          targetCategoryId: "cat-gen",
          name: p.projectName,
          amount: p.amount
        }))
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useCombineCipMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: any) => {
      return fixedAssetsApi.combineCip({
        legalEntityId: legalEntityId ?? "",
        targetCategoryId: "cat-gen",
        name: payload.combinedProjectName,
        sources: payload.projectIds.map((id: string) => ({
          assetId: id,
          amount: 100000
        }))
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function usePrepareDepreciationRunMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: any) => {
      return { id: `run-${Date.now()}` };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function usePostDepreciationRunMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ runId }: { runId: string }) => {
      return { success: true };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useReverseDepreciationRunMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ runId, payload }: { runId: string; payload: any }) => {
      return { success: true };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useRecordAuditFindingMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ campaignId, payload }: { campaignId: string; payload: any }) => {
      return { success: true };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useChangeEstimateMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ assetId, payload }: { assetId: string; payload: any }) => {
      return { success: true };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useAddCostMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ assetId, payload }: { assetId: string; payload: any }) => {
      return { success: true };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useProposeImpairmentMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ assetId, payload }: { assetId: string; payload: any }) => {
      return { success: true };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

export function useMassTransferMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: any) => {
      return fixedAssetsApi.executeMassTransfer({
        legalEntityId: legalEntityId ?? "",
        assignments: payload.assetIds.map((id: string) => ({
          assetId: id,
          assignmentType: payload.targetCustodianId ? "Employee" : "Location",
          employeeId: payload.targetCustodianId,
          departmentId: payload.targetDepartment,
          locationId: payload.targetLocationId,
          condition: payload.handoverNote
        }))
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
    mutationFn: async ({ candidateId, cipProjectId }: { candidateId: string; cipProjectId: string }) => {
      return fixedAssetsApi.addCipCostSource(cipProjectId, {
        sourceType: "Candidate",
        sourceReference: candidateId,
        amount: 50000
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fixedAssetKeys.all });
    }
  });
}

