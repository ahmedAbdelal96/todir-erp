import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";

export interface QualityScopeOptions {
  readonly legalEntityId?: string | undefined;
}

export interface QualityScope {
  readonly legalEntityId: string;
  readonly legalEntityCode: string | undefined;
  readonly legalEntityName: string | undefined;
  readonly isReady: boolean;
  readonly today: string;
}

export function useQualityScope(options?: QualityScopeOptions): QualityScope {
  let outletCtx: WorkspaceOutletContext | undefined;
  try {
    outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  } catch {
    // rendered outside router context
  }

  let runtimeLegalEntityId: string | undefined;
  let runtimeLegalEntityCode: string | undefined;
  let runtimeLegalEntityName: string | undefined;
  try {
    const runtimeCtx = useLegalEntityContext();
    runtimeLegalEntityId = runtimeCtx?.legalEntityId;
    runtimeLegalEntityCode = runtimeCtx?.code;
    runtimeLegalEntityName = runtimeCtx?.name;
  } catch {
    // rendered outside RuntimeProvider
  }

  // Strictly prioritize options -> outletCtx -> runtime context.
  // Never fallback to hardcoded mock UUIDs.
  const rawId = options?.legalEntityId || outletCtx?.legalEntityId || runtimeLegalEntityId || "";
  const legalEntityId: string = rawId;
  const legalEntityCode = outletCtx?.legalEntityCode ?? runtimeLegalEntityCode;
  const legalEntityName = outletCtx?.legalEntityName ?? runtimeLegalEntityName;

  const isReady = Boolean(legalEntityId && legalEntityId.length > 0);
  const today: string = new Date().toISOString().split("T")[0]!;

  return {
    legalEntityId,
    legalEntityCode,
    legalEntityName,
    isReady,
    today
  };
}
