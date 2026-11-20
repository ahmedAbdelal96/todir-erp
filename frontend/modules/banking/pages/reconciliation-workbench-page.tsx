import React from "react";
import { useTranslation } from "react-i18next";
import { useParams, useNavigate, useOutletContext } from "react-router-dom";
import { useOptionalLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useReconciliation } from "../hooks/use-banking-queries.ts";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { ReconciliationWorkbenchModal } from "../features/reconciliation/reconciliation-workbench-modal.tsx";

export function ReconciliationWorkbenchPage(): React.JSX.Element {
  const { sessionId } = useParams<{ sessionId: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation("banking");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || "";

  const { data: session, isLoading, error } = useReconciliation(legalEntityId, sessionId ?? "");

  if (isLoading) {
    return (
      <Surface padding="normal">
        <p>{t("common.loading", "جاري التحميل...")}</p>
      </Surface>
    );
  }

  if (error || !session) {
    return (
      <Surface padding="normal">
        <h2 style={{ color: "var(--color-status-danger)", marginBottom: "1rem" }}>
          {t("reconciliation.sessionNotFound", "جلسة التسوية غير موجودة أو تم إنهاؤها")}
        </h2>
        <Button variant="primary" onClick={() => navigate("/banking/reconciliations")}>
          {t("reconciliation.backToReconciliations", "العودة إلى قائمة التسويات")}
        </Button>
      </Surface>
    );
  }

  return (
    <div data-testid="reconciliation-workbench-page">
      <ReconciliationWorkbenchModal
        legalEntityId={legalEntityId}
        reconciliation={session}
        isOpen={true}
        onClose={() => navigate("/banking/reconciliations")}
      />
    </div>
  );
}
