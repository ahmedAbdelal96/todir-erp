import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import type { InvoiceLifecycle, InvoiceDocumentType } from "../types.ts";

export interface InvoiceStatusGroupProps {
  readonly lifecycle: InvoiceLifecycle;
  readonly documentType?: InvoiceDocumentType | undefined;
  readonly settlementSummary?: string | null | undefined;
  readonly regulatorySummary?: string | null | undefined;
  readonly journalEntryId?: string | null | undefined;
}

export function getSettlementStatus(summary: string | null | undefined, t: any): string {
  if (!summary) return summary ?? "";
  const s = summary.toLowerCase();
  if (s === "unsettled" || s === "unpaid" || s === "open") return t("status.unsettled");
  if (s === "partiallysettled" || s === "partially_settled") return t("status.partiallySettled");
  if (s === "settled" || s === "fullysettled" || s === "paid") return t("status.settled");
  if (s === "nottracked" || s === "not_tracked") return t("status.notTracked");
  if (s === "oversettled") return t("status.overSettled");
  return summary;
}

export function getRegulatoryStatus(summary: string | null | undefined, t: any): string {
  if (!summary) return summary ?? "";
  const r = summary.toLowerCase();
  if (r === "approved") return t("status.approved");
  if (r === "cleared") return t("status.cleared");
  if (r === "reported") return t("status.reported");
  if (r === "submitted") return t("status.submitted");
  if (r === "pending") return t("status.pending");
  if (r === "rejected") return t("status.rejected");
  if (r === "warning") return t("status.warning");
  if (r === "notapplicable" || r === "not_applicable" || r === "na") return t("status.notApplicable");
  return summary;
}

export function InvoiceStatusGroup({
  lifecycle,
  documentType = "Invoice",
  settlementSummary,
  regulatorySummary,
  journalEntryId
}: InvoiceStatusGroupProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");

  // Lifecycle status
  const lifecycleIntent = lifecycle === "Finalized" ? "success" : lifecycle === "Cancelled" ? "danger" : "warning";
  const lifecycleLabel = lifecycle === "Finalized" ? t("list.finalized") : lifecycle === "Cancelled" ? t("list.cancelled") : t("list.draft");

  // Document type badge
  const docTypeLabel =
    documentType === "CreditNote"
      ? t("detail.creditNoteBadge")
      : documentType === "DebitNote"
        ? t("detail.debitNoteBadge")
        : undefined;

  // Settlement status
  let settlementIntent: "neutral" | "warning" | "info" | "success" = "neutral";
  const settlementDisplayLabel = getSettlementStatus(settlementSummary, t);
  if (settlementSummary) {
    const s = settlementSummary.toLowerCase();
    if (s.includes("paid") || s.includes("settled")) {
      settlementIntent = s.includes("partially") ? "info" : "success";
    } else if (s.includes("open") || s.includes("unpaid")) {
      settlementIntent = "warning";
    }
  }

  // Regulatory status
  let regulatoryIntent: "neutral" | "info" | "success" | "warning" | "danger" = "neutral";
  const regulatoryDisplayLabel = getRegulatoryStatus(regulatorySummary, t);
  if (regulatorySummary) {
    const r = regulatorySummary.toLowerCase();
    if (r.includes("cleared")) regulatoryIntent = "success";
    else if (r.includes("review") || r.includes("warning")) regulatoryIntent = "warning";
    else if (r.includes("rejected")) regulatoryIntent = "danger";
    else if (r.includes("submitted") || r.includes("reported")) regulatoryIntent = "info";
  }

  return (
    <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
      {docTypeLabel && (
        <span
          style={{
            fontSize: "0.75rem",
            fontWeight: 700,
            padding: "0.15rem 0.5rem",
            borderRadius: "var(--radius-sm, 0.25rem)",
            backgroundColor: documentType === "CreditNote" ? "#fef3c7" : "#e0f2fe",
            color: documentType === "CreditNote" ? "#92400e" : "#0369a1",
            border: `1px solid ${documentType === "CreditNote" ? "#fde68a" : "#bae6fd"}`
          }}
        >
          {docTypeLabel}
        </span>
      )}

      <StatusBadge
        status={lifecycle}
        label={lifecycleLabel}
        intent={lifecycleIntent}
      />

      {lifecycle === "Finalized" && journalEntryId && (
        <StatusBadge
          status="Posted"
          label={t("document.postedLocked")}
          intent="info"
        />
      )}

      {settlementDisplayLabel && (
        <StatusBadge
          status={settlementSummary ?? "Settlement"}
          label={settlementDisplayLabel}
          intent={settlementIntent}
        />
      )}

      {regulatoryDisplayLabel && (
        <StatusBadge
          status={regulatorySummary ?? "Regulatory"}
          label={regulatoryDisplayLabel}
          intent={regulatoryIntent}
        />
      )}
    </div>
  );
}
