import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";

export interface ManufacturingScopeOptions {
  readonly legalEntityId?: string | undefined;
}

export interface ManufacturingScope {
  readonly legalEntityId: string | undefined;
  readonly legalEntityCode: string | undefined;
  readonly legalEntityName: string | undefined;
  readonly isReady: boolean;
  readonly today: string;
}

export function useManufacturingScope(options?: ManufacturingScopeOptions): ManufacturingScope {
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
  // NEVER fallback to "00000000-0000-0000-0000-000000000001" or any fabricated UUID.
  const legalEntityId = options?.legalEntityId ?? outletCtx?.legalEntityId ?? runtimeLegalEntityId;
  const legalEntityCode = outletCtx?.legalEntityCode ?? runtimeLegalEntityCode;
  const legalEntityName = outletCtx?.legalEntityName ?? runtimeLegalEntityName;
  const isReady = Boolean(legalEntityId);
  const today = new Date().toISOString().slice(0, 10);

  return {
    legalEntityId,
    legalEntityCode,
    legalEntityName,
    isReady,
    today
  };
}
