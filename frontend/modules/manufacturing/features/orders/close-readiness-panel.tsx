import React from "react";
import { useTranslation } from "react-i18next";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { ManufacturingStatusBadge } from "../../components/manufacturing-status-badge.tsx";
import { useCloseReadinessQuery } from "../../hooks/use-manufacturing.ts";

export interface CloseReadinessPanelProps {
  readonly orderId: string;
  readonly onCloseClick?: () => void;
}

export function CloseReadinessPanel({ orderId, onCloseClick }: CloseReadinessPanelProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const { data: readiness, isLoading } = useCloseReadinessQuery(orderId);

  if (isLoading) {
    return <div style={{ padding: "1rem", color: "var(--color-content-muted)" }}>{t("common.loading")}</div>;
  }

  if (!readiness) {
    return <div style={{ padding: "1rem" }}>{t("orders.noMatchingOrders")}</div>;
  }

  const isBlocked = !readiness.canClose;

  return (
    <div
      data-testid="close-readiness-panel"
      style={{
        padding: "1rem",
        border: `1px solid ${isBlocked ? "#ef4444" : "#10b981"}`,
        borderRadius: "var(--radius-md)",
        background: isBlocked ? "#fef2f2" : "#ecfdf5",
        display: "flex",
        flexDirection: "column",
        gap: "0.75rem"
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <div>
          <h4 style={{ margin: 0, fontSize: "0.9375rem", color: isBlocked ? "#b91c1c" : "#047857" }}>
            {t("workspace.closeReadiness")}
          </h4>
          <span style={{ fontSize: "0.75rem", color: isBlocked ? "#991b1b" : "#065f46" }}>
            {isBlocked
              ? t("costing.unpostedLedgerWarning")
              : t("costing.reconciliationSuccess")}
          </span>
        </div>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          {onCloseClick && !isBlocked ? (
            <Button size="small" variant="primary" onPress={onCloseClick}>
              {t("orders.actions.close")}
            </Button>
          ) : null}
        </div>
      </div>

      {readiness.blockers && readiness.blockers.length > 0 ? (
        <div
          data-testid="close-blocker-message"
          style={{
            padding: "0.5rem 0.75rem",
            background: "#ffffff",
            border: "1px solid #ef4444",
            borderRadius: "var(--radius-sm)",
            color: "#b91c1c",
            fontSize: "0.8125rem",
            fontWeight: 600
          }}
        >
          {t("costing.closeBlockersTitle")}: {readiness.blockers.join(", ")}
        </div>
      ) : null}

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: "0.5rem", fontSize: "0.75rem" }}>
        <div style={{ padding: "0.375rem 0.5rem", background: "#ffffff", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)" }}>
          <span style={{ color: "var(--color-content-muted)" }}>{t("materials.issued")}:</span>
          <div><strong><BdiText>{String(readiness.issuedMaterialCost)}</BdiText></strong></div>
        </div>
        <div style={{ padding: "0.375rem 0.5rem", background: "#ffffff", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)" }}>
          <span style={{ color: "var(--color-content-muted)" }}>{t("materials.consumed")}:</span>
          <div><strong><BdiText>{String(readiness.consumedMaterialCost)}</BdiText></strong></div>
        </div>
        <div style={{ padding: "0.375rem 0.5rem", background: "#ffffff", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)" }}>
          <span style={{ color: "var(--color-content-muted)" }}>{t("shopFloor.goodOutput")}:</span>
          <div><strong><BdiText>{String(readiness.goodOutputQty)}</BdiText></strong></div>
        </div>
        <div style={{ padding: "0.375rem 0.5rem", background: "#ffffff", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)" }}>
          <span style={{ color: "var(--color-content-muted)" }}>{t("shopFloor.scrapOutput")}:</span>
          <div><strong><BdiText>{String(readiness.scrapQty)}</BdiText></strong></div>
        </div>
        <div style={{ padding: "0.375rem 0.5rem", background: "#ffffff", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)" }}>
          <span style={{ color: "var(--color-content-muted)" }}>{t("workspace.operationsTab")}:</span>
          <div><strong>{readiness.allOperationsComplete ? t("status.Completed") : t("status.InProgress")}</strong></div>
        </div>
        <div style={{ padding: "0.375rem 0.5rem", background: "#ffffff", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)" }}>
          <span style={{ color: "var(--color-content-muted)" }}>{t("costing.wipBalance")}:</span>
          <div><strong><BdiText>{String(readiness.wipBalance)}</BdiText></strong></div>
        </div>
      </div>
    </div>
  );
}
