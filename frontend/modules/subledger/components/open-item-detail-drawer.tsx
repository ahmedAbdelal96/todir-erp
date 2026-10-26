import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { FinancialStatusBadge } from "./financial-status-badge.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { Separator } from "../../../components/primitives/separator.tsx";
import type { PartyOpenItem } from "../types.ts";

export interface OpenItemDetailDrawerProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly item: PartyOpenItem | null | undefined;
  readonly onApply?: (item: PartyOpenItem) => void;
  readonly onWriteOff?: (item: PartyOpenItem) => void;
  readonly onTriggerReversal?: (applicationId: string) => void;
  readonly onTriggerWriteOff?: () => void;
}

export function OpenItemDetailDrawer({
  isOpen,
  onClose,
  item,
  onApply,
  onWriteOff,
  onTriggerReversal,
  onTriggerWriteOff
}: OpenItemDetailDrawerProps): React.JSX.Element | null {
  const { t } = useTranslation("subledger");

  if (!item) return null;

  const isDebit = item.polarity === "Debit";
  const isOpenItem = parseFloat(item.openTransactionAmount) > 0;

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      size="medium"
      title={
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "1rem" }}>
          <span>{item.sourceDisplayNumber || item.sourceReference}</span>
          <FinancialStatusBadge status={item.status} />
        </div>
      }
      description={t("drawer.title")}
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", width: "100%" }}>
          {parseFloat(item.settledTransactionAmount || "0") > 0 && onTriggerReversal && (
            <Button
              variant="secondary"
              onClick={() => {
                onTriggerReversal(`app-${item.id}`);
              }}
            >
              {t("reversal.title")}
            </Button>
          )}

          {isOpenItem && (onWriteOff || onTriggerWriteOff) && (
            <Button
              variant="secondary"
              onClick={() => {
                if (onWriteOff) onWriteOff(item);
                if (onTriggerWriteOff) onTriggerWriteOff();
                onClose();
              }}
            >
              {t("drawer.requestWriteOff")}
            </Button>
          )}

          {isOpenItem && onApply && (
            <Button
              variant="primary"
              onClick={() => {
                onApply(item);
                onClose();
              }}
            >
              {t("openItems.applyNow")}
            </Button>
          )}

          <Button variant="secondary" onClick={onClose}>
            {t("drawer.close")}
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: "0.5rem 0" }}>
        {/* Core Amounts Grid */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(3, 1fr)",
            gap: "1rem",
            padding: "1rem",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            borderRadius: "var(--radius-md, 0.5rem)",
            border: "1px solid var(--color-border-subtle, #e2e8f0)"
          }}
        >
          <div>
            <div style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--color-content-muted, #64748b)" }}>
              {t("openItems.originalAmount")}
            </div>
            <div style={{ fontSize: "1.125rem", fontWeight: 800, marginTop: "0.25rem", color: "var(--color-content-primary, #0f172a)" }}>
              <MoneyDisplay
                value={{ amount: item.originalTransactionAmount, currency: item.transactionCurrency }}
                displayCurrencyCode
              />
            </div>
          </div>

          <div>
            <div style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--color-content-muted, #64748b)" }}>
              {t("openItems.settledAmount")}
            </div>
            <div style={{ fontSize: "1.125rem", fontWeight: 800, marginTop: "0.25rem", color: "var(--color-status-success, #059669)" }}>
              <MoneyDisplay
                value={{ amount: item.settledTransactionAmount, currency: item.transactionCurrency }}
                displayCurrencyCode
              />
            </div>
          </div>

          <div>
            <div style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--color-content-muted, #64748b)" }}>
              {t("openItems.remainingAmount")}
            </div>
            <div style={{ fontSize: "1.125rem", fontWeight: 800, marginTop: "0.25rem", color: "var(--color-status-danger, #dc2626)" }}>
              <MoneyDisplay
                value={{ amount: item.openTransactionAmount, currency: item.transactionCurrency }}
                displayCurrencyCode
              />
            </div>
          </div>
        </div>

        <Separator />

        {/* Detailed Document Properties */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem", fontSize: "0.875rem" }}>
          <div>
            <span style={{ color: "var(--color-content-muted, #64748b)", fontWeight: 600 }}>
              {t("drawer.sourceType")}:
            </span>{" "}
            <strong style={{ color: "var(--color-content-primary, #0f172a)" }}>{item.sourceType}</strong>
          </div>

          <div>
            <span style={{ color: "var(--color-content-muted, #64748b)", fontWeight: 600 }}>
              {t("drawer.polarity")}:
            </span>{" "}
            <strong style={{ color: isDebit ? "var(--color-brand-primary, #102a43)" : "var(--color-status-info, #0284c7)" }}>
              {isDebit ? t("drawer.debit") : t("drawer.credit")}
            </strong>
          </div>

          <div>
            <span style={{ color: "var(--color-content-muted, #64748b)", fontWeight: 600 }}>
              {t("openItems.documentDate")}:
            </span>{" "}
            <span style={{ color: "var(--color-content-primary, #0f172a)", fontWeight: 600 }}><bdi>{item.documentDate}</bdi></span>
          </div>

          <div>
            <span style={{ color: "var(--color-content-muted, #64748b)", fontWeight: 600 }}>
              {t("openItems.dueDate")}:
            </span>{" "}
            <span style={{ color: "var(--color-content-primary, #0f172a)", fontWeight: 600 }}><bdi>{item.dueDate}</bdi></span>
          </div>

          <div>
            <span style={{ color: "var(--color-content-muted, #64748b)", fontWeight: 600 }}>
              {t("drawer.postingDate")}:
            </span>{" "}
            <span style={{ color: "var(--color-content-primary, #0f172a)", fontWeight: 600 }}><bdi>{item.postingDate}</bdi></span>
          </div>

          <div>
            <span style={{ color: "var(--color-content-muted, #64748b)", fontWeight: 600 }}>
              {t("drawer.controlAccount")}:
            </span>{" "}
            <span style={{ color: "var(--color-content-primary, #0f172a)", fontWeight: 600 }}>
              {item.controlAccountRole} ({item.controlAccountReference})
            </span>
          </div>

          {item.sourceScheduleReference && (
            <div>
              <span style={{ color: "var(--color-content-muted, #64748b)", fontWeight: 600 }}>
                {t("drawer.installmentSequence")}:
              </span>{" "}
              <strong style={{ color: "var(--color-brand-primary, #102a43)" }}>{item.sourceScheduleReference}</strong>
            </div>
          )}

          {item.baseCurrency !== item.transactionCurrency && (
            <div>
              <span style={{ color: "var(--color-content-muted, #64748b)", fontWeight: 600 }}>
                {t("drawer.historicFx")}:
              </span>{" "}
              <span style={{ color: "var(--color-content-primary, #0f172a)", fontWeight: 600 }}>
                {item.originalBaseAmount} {item.baseCurrency}
              </span>
            </div>
          )}
        </div>
      </div>
    </Dialog>
  );
}
