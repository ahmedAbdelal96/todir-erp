import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import { PermissionSet } from "../../../core/runtime/permissions.ts";
import { loadSessionContext } from "../../../core/api/auth-service.ts";
import { useLedgerScope } from "./use-ledger-queries.ts";
import { accountingApi } from "../services/accounting-api.ts";
import type {
  ApproveGoLiveRequest,
  ClosePreviewRequest,
  FinancialStatementDefinitionRequest,
  FxRevaluationRequest,
  FxRevaluationReversalRequest,
  ReopenPeriodRequest,
  StatementKind
} from "../types.ts";

export const accountingQueryKeys = {
  all: ["accounting"] as const,
  health: (legalEntityId: string, asOf?: string) => ["accounting", "health", legalEntityId, asOf ?? ""] as const,
  ownerSummary: (legalEntityId: string, asOf?: string) => ["accounting", "ownerSummary", legalEntityId, asOf ?? ""] as const,
  reconciliations: (legalEntityId: string, params?: { type?: string; asOf?: string }) =>
    ["accounting", "reconciliations", legalEntityId, params?.type ?? "", params?.asOf ?? ""] as const,
  reconciliation: (legalEntityId: string, runId: string) => ["accounting", "reconciliation", legalEntityId, runId] as const,
  reconciliationIssues: (legalEntityId: string, status?: string) =>
    ["accounting", "reconciliationIssues", legalEntityId, status ?? ""] as const,
  closeRun: (legalEntityId: string, periodId: string) => ["accounting", "closeRun", legalEntityId, periodId] as const,
  closeRunIssues: (legalEntityId: string, periodId: string) => ["accounting", "closeRunIssues", legalEntityId, periodId] as const,
  fxRevaluation: (legalEntityId: string, runId: string) => ["accounting", "fxRevaluation", legalEntityId, runId] as const,
  statement: (legalEntityId: string, kind: StatementKind, from: string, to: string) =>
    ["accounting", "statement", legalEntityId, kind, from, to] as const,
  statementDefinition: (legalEntityId: string, kind: string) =>
    ["accounting", "statementDefinition", legalEntityId, kind] as const
};

export function useAccountingPermissions() {
  const permissions = useOptionalPermissions();
  const sessionQuery = useQuery({
    queryKey: ["session", "context"],
    queryFn: loadSessionContext,
    staleTime: 60_000
  });

  const sessionPerms = (sessionQuery.data as { permissions?: readonly string[] } | undefined)?.permissions;
  const permSet = permissions ?? (sessionPerms ? new PermissionSet(sessionPerms) : undefined);

  return {
    canReadHealth: permSet ? permSet.hasPermission("accounting.health.read") : true,
    canReadOwner: permSet ? permSet.hasPermission("accounting.owner.read") : true,
    canReadStatements: permSet ? permSet.hasPermission("accounting.statement.read") : true,
    canManageStatements: permSet ? permSet.hasPermission("accounting.statement.manage") : true,
    canReadReconciliation: permSet ? permSet.hasPermission("accounting.reconciliation.read") : true,
    canRunReconciliation: permSet ? permSet.hasPermission("accounting.reconciliation.run") : true,
    canReadFx: permSet ? permSet.hasPermission("accounting.fx-revaluation.read") : true,
    canPostFx: permSet ? permSet.hasPermission("accounting.fx-revaluation.post") : true,
    canClosePeriod: permSet ? permSet.hasPermission("accounting.close.execute") : true,
    canReopenPeriod: permSet ? permSet.hasPermission("accounting.close.reopen") : true,
    canApproveGoLive: permSet ? permSet.hasPermission("accounting.opening.approve") : true
  };
}

export function useAccountingHealth(asOf?: string) {
  const { legalEntityId } = useLedgerScope();
  const { canReadHealth } = useAccountingPermissions();

  return useQuery({
    queryKey: accountingQueryKeys.health(legalEntityId, asOf),
    queryFn: () => accountingApi.getHealth(legalEntityId, asOf),
    enabled: Boolean(legalEntityId) && canReadHealth
  });
}

export function useOwnerFinancialSummary(asOf?: string) {
  const { legalEntityId } = useLedgerScope();
  const { canReadOwner } = useAccountingPermissions();

  return useQuery({
    queryKey: accountingQueryKeys.ownerSummary(legalEntityId, asOf),
    queryFn: () => accountingApi.getOwnerSummary(legalEntityId, asOf),
    enabled: Boolean(legalEntityId) && canReadOwner
  });
}

export function useReconciliationsList(params?: { type?: string; asOf?: string; limit?: number }) {
  const { legalEntityId } = useLedgerScope();
  const { canReadReconciliation } = useAccountingPermissions();

  return useQuery({
    queryKey: accountingQueryKeys.reconciliations(legalEntityId, params),
    queryFn: () => accountingApi.listReconciliations(legalEntityId, params),
    enabled: Boolean(legalEntityId) && canReadReconciliation
  });
}

export function useReconciliationDetails(runId: string) {
  const { legalEntityId } = useLedgerScope();
  const { canReadReconciliation } = useAccountingPermissions();

  return useQuery({
    queryKey: accountingQueryKeys.reconciliation(legalEntityId, runId),
    queryFn: () => accountingApi.getReconciliation(legalEntityId, runId),
    enabled: Boolean(legalEntityId) && Boolean(runId) && canReadReconciliation
  });
}

export function useReconciliationIssues(params?: { status?: string; limit?: number }) {
  const { legalEntityId } = useLedgerScope();
  const { canReadReconciliation } = useAccountingPermissions();

  return useQuery({
    queryKey: accountingQueryKeys.reconciliationIssues(legalEntityId, params?.status),
    queryFn: () => accountingApi.listReconciliationIssues(legalEntityId, params),
    enabled: Boolean(legalEntityId) && canReadReconciliation
  });
}

export function useFinancialStatement(kind: StatementKind, from: string, to: string) {
  const { legalEntityId } = useLedgerScope();
  const { canReadStatements } = useAccountingPermissions();

  return useQuery({
    queryKey: accountingQueryKeys.statement(legalEntityId, kind, from, to),
    queryFn: () => accountingApi.getStatement(legalEntityId, kind, from, to),
    enabled: Boolean(legalEntityId) && Boolean(from) && Boolean(to) && canReadStatements
  });
}

export function useStatementDefinition(kind: string) {
  const { legalEntityId } = useLedgerScope();
  const { canManageStatements } = useAccountingPermissions();

  return useQuery({
    queryKey: accountingQueryKeys.statementDefinition(legalEntityId, kind),
    queryFn: () => accountingApi.getStatementDefinition(legalEntityId, kind),
    enabled: Boolean(legalEntityId) && Boolean(kind) && canManageStatements
  });
}

export function useCloseRun(periodId: string) {
  const { legalEntityId } = useLedgerScope();

  return useQuery({
    queryKey: accountingQueryKeys.closeRun(legalEntityId, periodId),
    queryFn: () => accountingApi.getCloseRun(legalEntityId, periodId),
    enabled: Boolean(legalEntityId) && Boolean(periodId)
  });
}

export function useCloseRunIssues(periodId: string) {
  const { legalEntityId } = useLedgerScope();

  return useQuery({
    queryKey: accountingQueryKeys.closeRunIssues(legalEntityId, periodId),
    queryFn: () => accountingApi.listCloseRunIssues(legalEntityId, periodId),
    enabled: Boolean(legalEntityId) && Boolean(periodId)
  });
}

export function useFxRevaluation(runId: string) {
  const { legalEntityId } = useLedgerScope();
  const { canReadFx } = useAccountingPermissions();

  return useQuery({
    queryKey: accountingQueryKeys.fxRevaluation(legalEntityId, runId),
    queryFn: () => accountingApi.getFxRevaluation(legalEntityId, runId),
    enabled: Boolean(legalEntityId) && Boolean(runId) && canReadFx
  });
}

export function useRunReconciliationMutation() {
  const { legalEntityId } = useLedgerScope();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ type, asOf }: { type: string; asOf: string }) =>
      accountingApi.runReconciliation(legalEntityId, type, asOf),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounting"] });
    }
  });
}

export function usePreviewCloseMutation() {
  const { legalEntityId } = useLedgerScope();

  return useMutation({
    mutationFn: (request: ClosePreviewRequest) => accountingApi.previewClose(legalEntityId, request)
  });
}

export function useClosePeriodMutation() {
  const { legalEntityId } = useLedgerScope();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: ClosePreviewRequest) => accountingApi.closePeriod(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounting"] });
      queryClient.invalidateQueries({ queryKey: ["ledger"] });
    }
  });
}

export function useReopenPeriodMutation() {
  const { legalEntityId } = useLedgerScope();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ periodId, request }: { periodId: string; request: ReopenPeriodRequest }) =>
      accountingApi.reopenPeriod(legalEntityId, periodId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounting"] });
      queryClient.invalidateQueries({ queryKey: ["ledger"] });
    }
  });
}

export function useApproveGoLiveMutation() {
  const { legalEntityId } = useLedgerScope();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: ApproveGoLiveRequest) => accountingApi.approveGoLive(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounting"] });
      queryClient.invalidateQueries({ queryKey: ["ledger"] });
    }
  });
}

export function usePreviewFxRevaluationMutation() {
  const { legalEntityId } = useLedgerScope();

  return useMutation({
    mutationFn: (request: FxRevaluationRequest) => accountingApi.previewFxRevaluation(legalEntityId, request)
  });
}

export function usePostFxRevaluationMutation() {
  const { legalEntityId } = useLedgerScope();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (runId: string) => accountingApi.postFxRevaluation(legalEntityId, runId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounting"] });
      queryClient.invalidateQueries({ queryKey: ["ledger"] });
    }
  });
}

export function useReverseFxRevaluationMutation() {
  const { legalEntityId } = useLedgerScope();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ runId, request }: { runId: string; request: FxRevaluationReversalRequest }) =>
      accountingApi.reverseFxRevaluation(legalEntityId, runId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounting"] });
      queryClient.invalidateQueries({ queryKey: ["ledger"] });
    }
  });
}

export function useSaveStatementDefinitionMutation() {
  const { legalEntityId } = useLedgerScope();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: FinancialStatementDefinitionRequest) =>
      accountingApi.saveStatementDefinition(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounting"] });
    }
  });
}
