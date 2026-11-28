import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";

export interface PlanningScopeOptions {
  readonly legalEntityId?: string | undefined;
}

export interface PlanningScope {
  readonly legalEntityId: string;
  readonly legalEntityCode: string | undefined;
  readonly legalEntityName: string | undefined;
  readonly currencyCode: string;
  readonly isReady: boolean;
  readonly today: string;
}

export function usePlanningScope(options?: PlanningScopeOptions): PlanningScope {
  let outletCtx: WorkspaceOutletContext | undefined;
  try {
    outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  } catch {
    // rendered outside router context
  }

  let runtimeLegalEntityId: string | undefined;
  let runtimeLegalEntityCode: string | undefined;
  let runtimeLegalEntityName: string | undefined;
  let runtimeCurrencyCode: string | undefined;
  try {
    const runtimeCtx = useLegalEntityContext();
    runtimeLegalEntityId = runtimeCtx?.legalEntityId;
    runtimeLegalEntityCode = runtimeCtx?.code;
    runtimeLegalEntityName = runtimeCtx?.name;
    runtimeCurrencyCode = (runtimeCtx as { currencyCode?: string } | undefined)?.currencyCode;
  } catch {
    // rendered outside RuntimeProvider
  }

  const legalEntityId = options?.legalEntityId || outletCtx?.legalEntityId || runtimeLegalEntityId || "";
  const legalEntityCode = outletCtx?.legalEntityCode ?? runtimeLegalEntityCode;
  const legalEntityName = outletCtx?.legalEntityName ?? runtimeLegalEntityName;
  const currencyCode = runtimeCurrencyCode || "EGP";
  const isReady = Boolean(legalEntityId && legalEntityId.length > 0);
  const today = new Date().toISOString().split("T")[0]!;

  return {
    legalEntityId,
    legalEntityCode,
    legalEntityName,
    currencyCode,
    isReady,
    today
  };
}
