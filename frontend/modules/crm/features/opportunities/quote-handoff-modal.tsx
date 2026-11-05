import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { generateKey } from "../../../../core/api/http-client.ts";
import { useCreateQuotationMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Opportunity, QuoteHandoffLine } from "../../types.ts";

const DEFAULT_CURRENCY = "EGP";

export interface QuoteHandoffModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly opportunity: Opportunity | null;
  readonly onQuoted?: (quotationId: string) => void;
}

export function QuoteHandoffModal({
  isOpen,
  onClose,
  legalEntityId,
  opportunity,
  onQuoted
}: QuoteHandoffModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const quoteMutation = useCreateQuotationMutation(legalEntityId, opportunity?.id || "");

  const [quoteDate, setQuoteDate] = useState(new Date().toISOString().slice(0, 10));
  const [validUntil, setValidUntil] = useState(new Date(Date.now() + 15 * 86400000).toISOString().slice(0, 10));
  const [customerReference, setCustomerReference] = useState("");
  const [lines, setLines] = useState<QuoteHandoffLine[]>([
    {
      description: opportunity?.name || t("crm.opportunities.quoteDefaultLine", "توريد وخدمات تجارية"),
      quantity: 1,
      unitPrice: opportunity?.estimatedAmount || 10000,
      discountAmount: 0
    }
  ]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleAddLine = () => {
    setLines((prev) => [
      ...prev,
      { description: "", quantity: 1, unitPrice: 0, discountAmount: 0 }
    ]);
  };

  const handleUpdateLine = (index: number, field: keyof QuoteHandoffLine, val: unknown) => {
    setLines((prev) =>
      prev.map((l, i) => (i === index ? { ...l, [field]: val } : l))
    );
  };

  const handleRemoveLine = (index: number) => {
    if (lines.length <= 1) return;
    setLines((prev) => prev.filter((_, i) => i !== index));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!opportunity) return;
    if (lines.length === 0 || lines.some((l) => !l.description.trim())) {
      setErrorMessage(t("errors.quoteLinesRequired"));
      return;
    }

    try {
      const res = await quoteMutation.mutateAsync({
        idempotencyKey: generateKey(),
        quoteDate,
        validUntil: validUntil || null,
        currencyCode: opportunity.currencyCode || "EGP",
        customerReference: customerReference.trim() || null,
        lines: lines.map((l) => ({
          ...l,
          quantity: Number(l.quantity) || 1,
          unitPrice: Number(l.unitPrice) || 0,
          discountAmount: Number(l.discountAmount) || 0
        }))
      });

      onClose();
      if (onQuoted && res.id) {
        onQuoted(res.id);
      }
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const totalAmount = lines.reduce(
    (sum, l) => sum + (Number(l.quantity) || 1) * (Number(l.unitPrice) || 0) - (Number(l.discountAmount) || 0),
    0
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("opportunities.quoteHandoffModal.title")}
      size="large"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={onClose}
            isDisabled={quoteMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="quote-handoff-form"
            isDisabled={quoteMutation.isPending}
          >
            {quoteMutation.isPending ? t("actions.saving") : t("opportunities.quoteHandoffModal.submit")}
          </Button>
        </>
      }
    >
      <form id="quote-handoff-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.625rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              background: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.8125rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <div style={{ backgroundColor: "var(--color-surface-subtle)", padding: "0.75rem", borderRadius: "var(--radius-sm)" }}>
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("opportunities.quoteHandoffModal.instruction")}
          </span>
          <div style={{ marginTop: "0.25rem", fontWeight: 600 }}>
            {opportunity?.name} — {opportunity?.estimatedAmount} {opportunity?.currencyCode}
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
          <TextField
            label={t("opportunities.quoteHandoffModal.quoteDate")}
            value={quoteDate}
            onChange={setQuoteDate}
            type="date"
            isRequired
          />
          <TextField
            label={t("opportunities.quoteHandoffModal.validUntil")}
            value={validUntil}
            onChange={setValidUntil}
            type="date"
          />
          <TextField
            label={t("opportunities.quoteHandoffModal.customerReference")}
            value={customerReference}
            onChange={setCustomerReference}
            placeholder={t("opportunities.quoteHandoffModal.referencePlaceholder")}
          />
        </div>

        {/* Lines */}
        <div>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem" }}>
            <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
              {t("opportunities.quoteHandoffModal.linesTitle")}
            </span>
            <Button size="compact" variant="secondary" onPress={handleAddLine}>
              + {t("opportunities.quoteHandoffModal.addLine")}
            </Button>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            {lines.map((line, idx) => (
              <div
                key={idx}
                style={{
                  display: "grid",
                  gridTemplateColumns: "2fr 1fr 1fr 1fr auto",
                  gap: "0.5rem",
                  alignItems: "end",
                  backgroundColor: "var(--color-surface-card)",
                  padding: "0.5rem",
                  border: "1px solid var(--color-border-default)",
                  borderRadius: "var(--radius-sm)"
                }}
              >
                <TextField
                  label={t("opportunities.quoteHandoffModal.lineDescription")}
                  value={line.description}
                  onChange={(v: string) => handleUpdateLine(idx, "description", v)}
                  isRequired
                />
                <TextField
                  label={t("opportunities.quoteHandoffModal.quantity")}
                  value={String(line.quantity)}
                  onChange={(v: string) => handleUpdateLine(idx, "quantity", v)}
                  type="number"
                  isRequired
                />
                <TextField
                  label={t("opportunities.quoteHandoffModal.unitPrice")}
                  value={String(line.unitPrice)}
                  onChange={(v: string) => handleUpdateLine(idx, "unitPrice", v)}
                  type="number"
                  isRequired
                />
                <TextField
                  label={t("opportunities.quoteHandoffModal.discount")}
                  value={String(line.discountAmount || 0)}
                  onChange={(v: string) => handleUpdateLine(idx, "discountAmount", v)}
                  type="number"
                />
                {lines.length > 1 && (
                  <Button
                    size="compact"
                    variant="secondary"
                    onPress={() => handleRemoveLine(idx)}
                    style={{ color: "var(--color-status-danger)", marginBottom: "0.25rem" }}
                  >
                    ✕
                  </Button>
                )}
              </div>
            ))}
          </div>

          <div style={{ textAlign: "end", marginTop: "0.75rem", fontWeight: 700, fontSize: "0.9375rem" }}>
            {t("opportunities.quoteHandoffModal.totalQuote")}: {totalAmount.toLocaleString()} {opportunity?.currencyCode || DEFAULT_CURRENCY}
          </div>
        </div>
      </form>
    </Dialog>
  );
}
