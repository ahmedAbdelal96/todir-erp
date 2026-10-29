import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { SalesCustomerPicker } from "../../components/sales-customer-picker.tsx";
import { SalesProductPicker } from "../../components/sales-product-picker.tsx";
import { SalesDataTable } from "../../components/sales-data-table.tsx";
import { useCreateQuotationMutation } from "../../hooks/use-sales-queries.ts";
import { mapSalesError } from "../../services/sales-error-map.ts";
import type { QuotationLineRequest } from "../../types.ts";

export interface NewQuotationModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly onCreated?: (id: string) => void;
}

interface EditableLine extends QuotationLineRequest {
  readonly key: string;
}

export function NewQuotationModal({
  isOpen,
  onClose,
  legalEntityId,
  onCreated
}: NewQuotationModalProps): React.JSX.Element {
  const { t } = useTranslation("sales");
  const createMutation = useCreateQuotationMutation(legalEntityId);

  const [partyId, setPartyId] = useState<string>("");
  const [currencyCode, setCurrencyCode] = useState<string>("EGP");
  const [quoteDate, setQuoteDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [validUntil, setValidUntil] = useState<string>("");
  const [customerReference, setCustomerReference] = useState<string>("");
  const [requestedDeliveryDate, setRequestedDeliveryDate] = useState<string>("");

  const [lines, setLines] = useState<readonly EditableLine[]>([
    {
      key: "line-1",
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      description: "",
      quantity: 10,
      unitPrice: 120,
      discountAmount: 0,
      uomCode: "EA"
    }
  ]);

  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const addLine = () => {
    setLines((prev) => [
      ...prev,
      {
        key: `line-${Date.now()}`,
        productVariantId: null,
        description: "",
        quantity: 1,
        unitPrice: 100,
        discountAmount: 0,
        uomCode: "EA"
      }
    ]);
  };

  const removeLine = (key: string) => {
    if (lines.length <= 1) return;
    setLines((prev) => prev.filter((l) => l.key !== key));
  };

  const updateLine = (key: string, patch: Partial<EditableLine>) => {
    setLines((prev) =>
      prev.map((l) => (l.key === key ? { ...l, ...patch } : l))
    );
  };

  const subtotal = lines.reduce((acc, l) => acc + (l.quantity * (l.unitPrice ?? 0)), 0);
  const totalDiscount = lines.reduce((acc, l) => acc + (l.discountAmount ?? 0), 0);
  const netTotal = subtotal - totalDiscount;
  const estimatedTax = netTotal * 0.14; // Egypt standard VAT reference for preview
  const estimatedGross = netTotal + estimatedTax;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!partyId) {
      setErrorMessage(t("quotations.customerRequired"));
      return;
    }

    if (lines.length === 0 || lines.some((l) => (!l.description && !l.productVariantId) || l.quantity <= 0)) {
      setErrorMessage(t("quotations.validLinesRequired"));
      return;
    }

    try {
      const quotationId = await createMutation.mutateAsync({
        legalEntityId,
        partyId,
        currencyCode: currencyCode.trim().toUpperCase(),
        quoteDate,
        validUntil: validUntil || null,
        customerReference: customerReference || null,
        requestedDeliveryDate: requestedDeliveryDate || null,
        lines: lines.map((l) => ({
          productVariantId: l.productVariantId || null,
          description: l.description || t("quotations.productOrDescription"),
          quantity: Number(l.quantity),
          unitPrice: l.unitPrice != null ? Number(l.unitPrice) : null,
          discountAmount: Number(l.discountAmount || 0),
          uomCode: l.uomCode || "EA"
        }))
      });

      onClose();
      if (onCreated) {
        onCreated(quotationId);
      }
    } catch (err) {
      const mapped = mapSalesError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => { if (!open) onClose(); }}
      title={t("quotations.newQuotation")}
      isDismissable
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1.25rem", minWidth: "600px", maxWidth: "850px" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem 1rem",
              backgroundColor: "var(--color-status-danger-subtle, #fef2f2)",
              border: "1px solid var(--color-status-danger, #ef4444)",
              borderRadius: "var(--radius-md)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        {/* Header Fields */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <SalesCustomerPicker
              value={partyId}
              onChange={setPartyId}
              isRequired
              label={t("quotations.customer")}
            />
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            <TextField
              label={t("quotations.currency")}
              value={currencyCode}
              onChange={setCurrencyCode}
              isRequired
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("quotations.quoteDate")}
            type="date"
            value={quoteDate}
            onChange={setQuoteDate}
            isRequired
          />

          <TextField
            label={t("quotations.validUntil")}
            type="date"
            value={validUntil}
            onChange={setValidUntil}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("quotations.customerReference")}
            value={customerReference}
            onChange={setCustomerReference}
            placeholder={t("quotations.customerReferencePlaceholder")}
          />

          <TextField
            label={t("quotations.requestedDeliveryDate")}
            type="date"
            value={requestedDeliveryDate}
            onChange={setRequestedDeliveryDate}
          />
        </div>

        {/* Lines Section */}
        <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <h4 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 600 }}>
              {t("quotations.lines")} ({lines.length})
            </h4>
            <Button variant="secondary" onPress={addLine} type="button">
              {t("quotations.addLine")}
            </Button>
          </div>

          <SalesDataTable minWidth="700px">
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-subtle)" }}>
                  <th style={{ padding: "0.5rem 0.75rem", fontSize: "0.75rem", fontWeight: 600 }}>{t("quotations.productOrDescription")}</th>
                  <th style={{ padding: "0.5rem 0.75rem", fontSize: "0.75rem", fontWeight: 600, width: "90px" }}>{t("quotations.quantity")}</th>
                  <th style={{ padding: "0.5rem 0.75rem", fontSize: "0.75rem", fontWeight: 600, width: "110px" }}>{t("quotations.unitPrice")}</th>
                  <th style={{ padding: "0.5rem 0.75rem", fontSize: "0.75rem", fontWeight: 600, width: "90px" }}>{t("quotations.discount")}</th>
                  <th style={{ padding: "0.5rem 0.75rem", fontSize: "0.75rem", fontWeight: 600, width: "110px", textAlign: "end" }}>{t("quotations.net")}</th>
                  <th style={{ padding: "0.5rem 0.75rem", width: "40px" }}></th>
                </tr>
              </thead>
              <tbody>
                {lines.map((l) => {
                  const lineNet = l.quantity * (l.unitPrice ?? 0) - (l.discountAmount ?? 0);
                  return (
                    <tr key={l.key} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                      <td style={{ padding: "0.5rem 0.75rem" }}>
                        <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
                          <SalesProductPicker
                            value={l.productVariantId}
                            onChange={(id, prod) => {
                              updateLine(l.key, {
                                productVariantId: id,
                                description: prod.name,
                                unitPrice: prod.listPrice ?? l.unitPrice,
                                uomCode: prod.uom
                              });
                            }}
                          />
                          <input
                            type="text"
                            value={l.description}
                            onChange={(e) => updateLine(l.key, { description: e.target.value })}
                            placeholder={t("quotations.descriptionPlaceholder")}
                            style={{
                              padding: "0.375rem 0.5rem",
                              borderRadius: "var(--radius-sm)",
                              border: "1px solid var(--color-border-default)",
                              fontSize: "0.8125rem",
                              width: "100%"
                            }}
                          />
                        </div>
                      </td>
                      <td style={{ padding: "0.5rem 0.75rem" }}>
                        <input
                          type="number"
                          min="1"
                          step="1"
                          value={l.quantity}
                          onChange={(e) => updateLine(l.key, { quantity: Number(e.target.value) })}
                          style={{
                            width: "100%",
                            padding: "0.375rem 0.5rem",
                            borderRadius: "var(--radius-sm)",
                            border: "1px solid var(--color-border-default)",
                            fontSize: "0.8125rem"
                          }}
                        />
                      </td>
                      <td style={{ padding: "0.5rem 0.75rem" }}>
                        <input
                          type="number"
                          min="0"
                          step="0.01"
                          value={l.unitPrice ?? 0}
                          onChange={(e) => updateLine(l.key, { unitPrice: Number(e.target.value) })}
                          style={{
                            width: "100%",
                            padding: "0.375rem 0.5rem",
                            borderRadius: "var(--radius-sm)",
                            border: "1px solid var(--color-border-default)",
                            fontSize: "0.8125rem"
                          }}
                        />
                      </td>
                      <td style={{ padding: "0.5rem 0.75rem" }}>
                        <input
                          type="number"
                          min="0"
                          step="0.01"
                          value={l.discountAmount ?? 0}
                          onChange={(e) => updateLine(l.key, { discountAmount: Number(e.target.value) })}
                          style={{
                            width: "100%",
                            padding: "0.375rem 0.5rem",
                            borderRadius: "var(--radius-sm)",
                            border: "1px solid var(--color-border-default)",
                            fontSize: "0.8125rem"
                          }}
                        />
                      </td>
                      <td style={{ padding: "0.5rem 0.75rem", textAlign: "end", fontWeight: 600, fontSize: "0.875rem" }}>
                        {lineNet.toFixed(2)}
                      </td>
                      <td style={{ padding: "0.5rem 0.75rem", textAlign: "center" }}>
                        {lines.length > 1 && (
                          <button
                            type="button"
                            onClick={() => removeLine(l.key)}
                            style={{
                              background: "transparent",
                              border: "none",
                              color: "var(--color-status-danger)",
                              cursor: "pointer",
                              fontWeight: "bold"
                            }}
                          >
                            ×
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </SalesDataTable>
        </div>

        {/* Pricing / Tax Preview Summary Box */}
        <div
          style={{
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            padding: "1rem",
            borderRadius: "var(--radius-md)",
            border: "1px solid var(--color-border-default)",
            display: "flex",
            flexDirection: "column",
            gap: "0.375rem",
            alignSelf: "flex-end",
            minWidth: "260px"
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.8125rem" }}>
            <span style={{ color: "var(--color-content-muted)" }}>{t("quotations.subtotal")}:</span>
            <span>{subtotal.toFixed(2)} {currencyCode}</span>
          </div>
          {totalDiscount > 0 && (
            <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.8125rem", color: "var(--color-status-danger)" }}>
              <span>{t("quotations.discount")}:</span>
              <span>-{totalDiscount.toFixed(2)} {currencyCode}</span>
            </div>
          )}
          <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.8125rem" }}>
            <span style={{ color: "var(--color-content-muted)" }}>{t("quotations.estimatedTax")}:</span>
            <span>{estimatedTax.toFixed(2)} {currencyCode}</span>
          </div>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              fontSize: "0.9375rem",
              fontWeight: 700,
              borderTop: "1px solid var(--color-border-default)",
              paddingTop: "0.375rem",
              color: "var(--color-brand-primary)"
            }}
          >
            <span>{t("quotations.estimatedTotal")}:</span>
            <span>{estimatedGross.toFixed(2)} {currencyCode}</span>
          </div>
        </div>

        {/* Dialog Action Buttons */}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("common.saving") : t("quotations.saveAsDraft")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
