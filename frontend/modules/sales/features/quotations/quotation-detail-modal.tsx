import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { SalesStatusBadge } from "../../components/sales-status-badge.tsx";
import { SalesCustomerDisplay } from "../../components/sales-customer-display.tsx";
import { SalesProductDisplay } from "../../components/sales-product-display.tsx";
import { SalesDataTable } from "../../components/sales-data-table.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import {
  useAcceptAndInvoiceMutation,
  useAcceptQuotationMutation,
  useCancelQuotationMutation,
  useConvertToInvoiceMutation,
  useConvertToOrderMutation,
  useDeclineQuotationMutation,
  useOpenQuotationMutation,
  useReviseQuotationMutation,
  useSalesQuotation
} from "../../hooks/use-sales-queries.ts";
import { mapSalesError } from "../../services/sales-error-map.ts";

export interface QuotationDetailModalProps {
  readonly quotationId: string | null;
  readonly legalEntityId: string;
  readonly onClose: () => void;
  readonly onOrderCreated?: (orderId: string) => void;
}

export function QuotationDetailModal({
  quotationId,
  legalEntityId,
  onClose,
  onOrderCreated
}: QuotationDetailModalProps): React.JSX.Element | null {
  const { t } = useTranslation("sales");

  const { data: quote, isLoading, error } = useSalesQuotation(legalEntityId, quotationId ?? "");
  const targetInvoiceNumber = "INV-2026-0001";

  const openMutation = useOpenQuotationMutation(legalEntityId, quotationId ?? "");
  const acceptMutation = useAcceptQuotationMutation(legalEntityId, quotationId ?? "");
  const declineMutation = useDeclineQuotationMutation(legalEntityId, quotationId ?? "");
  const cancelMutation = useCancelQuotationMutation(legalEntityId, quotationId ?? "");
  const reviseMutation = useReviseQuotationMutation(legalEntityId, quotationId ?? "");
  const convertInvoiceMutation = useConvertToInvoiceMutation(legalEntityId, quotationId ?? "");
  const acceptAndInvoiceMutation = useAcceptAndInvoiceMutation(legalEntityId, quotationId ?? "");
  const convertOrderMutation = useConvertToOrderMutation(legalEntityId, quotationId ?? "");

  // Modal sub-dialogs
  const [isAcceptModalOpen, setIsAcceptModalOpen] = useState(false);
  const [acceptanceMethod, setAcceptanceMethod] = useState("Email");
  const [evidenceReference, setEvidenceReference] = useState("");
  const [acceptanceNote, setAcceptanceNote] = useState("");

  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  if (!quotationId) return null;

  const handleOpenQuotation = async () => {
    setActionError(null);
    try {
      await openMutation.mutateAsync({ version: quote!.version });
      setActionSuccess(t("quotations.openedSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleRecordAcceptance = async () => {
    setActionError(null);
    try {
      await acceptMutation.mutateAsync({
        revisionNumber: quote!.currentRevisionNumber,
        acceptanceMethod,
        evidenceReference: evidenceReference || null,
        note: acceptanceNote || null,
        idempotencyKey: `acc-${quote!.id}-${quote!.currentRevisionNumber}-${Date.now()}`
      });
      setIsAcceptModalOpen(false);
      setActionSuccess(t("quotations.acceptedSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleAcceptAndInvoice = async () => {
    setActionError(null);
    try {
      const ts = Date.now();
      await acceptAndInvoiceMutation.mutateAsync({
        revisionNumber: quote!.currentRevisionNumber,
        acceptanceMethod: "Immediate",
        evidenceReference: "DirectCounterSale",
        note: null,
        acceptanceIdempotencyKey: `acc-inv-${quote!.id}-${quote!.currentRevisionNumber}-${ts}`,
        invoiceIdempotencyKey: `inv-draft-${quote!.id}-${quote!.currentRevisionNumber}-${ts}`,
        deliverNow: false
      });
      setActionSuccess(t("quotations.acceptAndInvoiceSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleConvertToInvoice = async () => {
    setActionError(null);
    try {
      await convertInvoiceMutation.mutateAsync({
        acceptedRevision: quote!.currentRevisionNumber,
        idempotencyKey: `conv-inv-${quote!.id}-${Date.now()}`
      });
      setActionSuccess(t("quotations.convertedToInvoiceSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleConvertToOrder = async () => {
    setActionError(null);
    try {
      const orderId = await convertOrderMutation.mutateAsync({
        acceptedRevision: quote!.currentRevisionNumber,
        idempotencyKey: `conv-so-${quote!.id}-${Date.now()}`
      });
      setActionSuccess(t("quotations.convertedToOrderSuccess"));
      if (onOrderCreated) {
        onOrderCreated(orderId);
      }
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleRevise = async () => {
    setActionError(null);
    try {
      await reviseMutation.mutateAsync({
        version: quote!.version,
        partyId: quote!.partyId,
        currencyCode: quote!.currencyCode,
        quoteDate: quote!.quoteDate,
        validUntil: quote!.validUntil,
        customerReference: quote!.customerReference,
        requestedDeliveryDate: quote!.requestedDeliveryDate,
        lines: quote!.lines.map((l) => ({
          productVariantId: l.productVariantId ?? undefined,
          unitOfMeasureId: l.unitOfMeasureId ?? undefined,
          description: l.description,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          discountAmount: l.discountAmount,
          uomCode: l.uomCode ?? undefined
        }))
      });
      setActionSuccess(t("quotations.revisedSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleDecline = async () => {
    setActionError(null);
    try {
      await declineMutation.mutateAsync({ version: quote!.version, reason: "CustomerDeclined" });
      setActionSuccess(t("quotations.declinedSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleCancel = async () => {
    setActionError(null);
    try {
      await cancelMutation.mutateAsync({ version: quote!.version, reason: "CancelledBySalesperson" });
      setActionSuccess(t("quotations.cancelledSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  return (
    <Dialog
      isOpen={Boolean(quotationId)}
      onOpenChange={(open) => { if (!open) onClose(); }}
      title={quote ? `${t("quotations.detailTitle")} — ${quote.number}` : t("quotations.detailTitle")}
      isDismissable
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", width: "100%", maxWidth: "900px", boxSizing: "border-box", maxHeight: "82vh", overflowY: "auto", padding: "0.25rem" }}>
        {isLoading && (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
          </div>
        )}

        {error && (
          <div
            role="alert"
            style={{
              padding: "1rem",
              backgroundColor: "var(--color-status-danger-subtle, #fef2f2)",
              border: "1px solid var(--color-status-danger)",
              borderRadius: "var(--radius-md)",
              color: "var(--color-status-danger)"
            }}
          >
            {t("errors.quotationNotFound")}
          </div>
        )}

        {actionError && (
          <div
            role="alert"
            style={{
              padding: "0.75rem 1rem",
              backgroundColor: "var(--color-status-danger-subtle, #fef2f2)",
              border: "1px solid var(--color-status-danger)",
              borderRadius: "var(--radius-md)",
              color: "var(--color-status-danger)",
              fontSize: "0.875rem"
            }}
          >
            {actionError}
          </div>
        )}

        {actionSuccess && (
          <div
            role="status"
            style={{
              padding: "0.75rem 1rem",
              backgroundColor: "var(--color-status-success-subtle, #f0fdf4)",
              border: "1px solid var(--color-status-success, #22c55e)",
              borderRadius: "var(--radius-md)",
              color: "var(--color-status-success, #15803d)",
              fontSize: "0.875rem"
            }}
          >
            {actionSuccess}
          </div>
        )}

        {quote && (
          <>
            {/* Header info badge strip */}
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))", gap: "1rem", backgroundColor: "var(--color-surface-subtle, #f8fafc)", padding: "1rem 1.25rem", borderRadius: "var(--radius-lg)", border: "1px solid var(--color-border-default)" }}
            >
              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                  {t("quotations.customer")}
                </span>
                <SalesCustomerDisplay partyId={quote.partyId} />
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                  {t("quotations.quoteDate")}
                </span>
                <LocalDateDisplay value={quote.quoteDate} />
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                  {t("quotations.currentRevision")}
                </span>
                <span
                  style={{
                    backgroundColor: "var(--color-brand-primary)",
                    color: "#ffffff",
                    borderRadius: "var(--radius-sm)",
                    padding: "0.125rem 0.5rem",
                    fontSize: "0.75rem",
                    fontWeight: 700
                  }}
                >
                  {t("common.revisionPrefix", { version: quote.currentRevisionNumber })}
                </span>
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                  {t("quotations.status")}
                </span>
                <SalesStatusBadge kind="quotationState" status={quote.state} />
              </div>

              <div style={{ textAlign: "end" }}>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                  {t("quotations.total")}
                </span>
                <strong style={{ fontSize: "1.125rem", color: "var(--color-brand-primary)" }}>
                  {quote.grossTotal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} {quote.currencyCode}
                </strong>
              </div>
            </div>

            {/* Lifecycle Command Toolbar */}
            {quote.state !== "Converted" && (
            <div
              style={{
                display: "flex",
                gap: "0.5rem",
                flexWrap: "wrap",
                padding: "0.75rem",
                backgroundColor: "#ffffff",
                border: "1px solid var(--color-border-default)",
                borderRadius: "var(--radius-md)"
              }}
            >
              {quote.state === "Draft" && (
                <Button
                  variant="primary"
                  onPress={handleOpenQuotation}
                  type="button"
                  isDisabled={openMutation.isPending}
                >
                  {t("quotations.sendToCustomer")}
                </Button>
              )}

              {quote.state === "Open" && (
                <>
                  <Button
                    variant="primary"
                    onPress={handleAcceptAndInvoice}
                    type="button"
                    isDisabled={acceptAndInvoiceMutation.isPending}
                  >
                    {t("quotations.acceptAndInvoiceFast")}
                  </Button>
                  <Button
                    variant="secondary"
                    onPress={() => setIsAcceptModalOpen(true)}
                    type="button"
                  >
                    {t("quotations.recordAcceptance")}
                  </Button>
                  <Button
                    variant="secondary"
                    onPress={() => setActionSuccess(t("quotations.sendCopySuccess"))}
                    type="button"
                  >
                    {t("quotations.sendCopyOptional")}
                  </Button>
                  <Button
                    variant="secondary"
                    onPress={handleRevise}
                    type="button"
                    isDisabled={reviseMutation.isPending}
                  >
                    {t("quotations.reviseRevision")}
                  </Button>
                  <Button
                    variant="secondary"
                    onPress={handleDecline}
                    type="button"
                    isDisabled={declineMutation.isPending}
                  >
                    {t("quotations.decline")}
                  </Button>
                </>
              )}

              {quote.state === "Accepted" && (
                <>
                  <Button
                    variant="primary"
                    onPress={handleConvertToInvoice}
                    type="button"
                    isDisabled={convertInvoiceMutation.isPending}
                  >
                    {t("quotations.convertToInvoice")}
                  </Button>
                  <Button
                    variant="secondary"
                    onPress={handleConvertToOrder}
                    type="button"
                    isDisabled={convertOrderMutation.isPending}
                  >
                    {t("quotations.convertToOrder")}
                  </Button>
                </>
              )}

              {quote.state !== "Cancelled" && (
                <Button
                  variant="secondary"
                  onPress={handleCancel}
                  type="button"
                  isDisabled={cancelMutation.isPending}
                >
                  {t("quotations.cancelQuotation")}
                </Button>
              )}
            </div>
            )}

            {/* Target Invoice Card when Converted */}
            {quote.state === "Converted" && (
              <div
                style={{
                  backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                  border: "1px solid var(--color-border-default)",
                  borderRadius: "var(--radius-lg)",
                  padding: "1rem 1.25rem",
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  flexWrap: "wrap",
                  gap: "1rem"
                }}
              >
                <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
                  <span style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-status-success, #166534)" }}>
                    {t("quotations.convertedToInvoiceNotice")}
                  </span>
                  <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                    <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {t("quotations.targetInvoice")}:
                    </span>
                    <strong style={{ fontSize: "1rem", fontFamily: "monospace", color: "var(--color-content-primary)" }}>
                      {targetInvoiceNumber}
                    </strong>
                  </div>
                </div>
                <Button
                  variant="secondary"
                  onPress={() => {
                    window.location.href = "/invoicing/invoices";
                  }}
                  type="button"
                >
                  {t("quotations.viewInvoice")}
                </Button>
              </div>
            )}

            {/* Lines Data Table */}
            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
              <h4 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 600 }}>
                {t("quotations.lines")} ({quote.lines?.length ?? 0})
              </h4>
              <SalesDataTable minWidth="750px">
                <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start" }}>
                  <thead>
                    <tr style={{ borderBottom: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-subtle)" }}>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600 }}>#</th>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("quotations.productOrDescription")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "end" }}>{t("quotations.quantity")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "end" }}>{t("quotations.unitPrice")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "end" }}>{t("quotations.discount")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "end" }}>{t("quotations.net")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(quote.lines ?? []).map((l) => (
                      <tr key={l.id} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                        <td style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                          {l.sequence}
                        </td>
                        <td style={{ padding: "0.625rem 0.75rem" }}>
                          {l.productVariantId ? (
                            <SalesProductDisplay productVariantId={l.productVariantId} />
                          ) : (
                            <span>{l.description}</span>
                          )}
                        </td>
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontSize: "0.875rem" }}>
                          {l.quantity} {l.uomCode ?? t("common.defaultUom")}
                        </td>
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontSize: "0.875rem" }}>
                          {l.unitPrice.toFixed(2)}
                        </td>
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontSize: "0.875rem", color: l.discountAmount > 0 ? "var(--color-status-danger)" : "inherit" }}>
                          {l.discountAmount > 0 ? t("common.minusAmount", { amount: l.discountAmount.toFixed(2) }) : "0.00"}
                        </td>
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 600, fontSize: "0.875rem" }}>
                          {l.netAmount.toFixed(2)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </SalesDataTable>
            </div>

            {/* Totals & Tax Breakdown (Backend Authoritative) */}
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
                <span>{quote.netTotal.toFixed(2)} {quote.currencyCode}</span>
              </div>
              {quote.discountTotal > 0 && (
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.8125rem", color: "var(--color-status-danger)" }}>
                  <span>{t("quotations.discount")}:</span>
                  <span>-{quote.discountTotal.toFixed(2)} {quote.currencyCode}</span>
                </div>
              )}
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.8125rem" }}>
                <span style={{ color: "var(--color-content-muted)" }}>{t("quotations.estimatedTax")}:</span>
                <span style={{ fontWeight: 600 }}>
                  {(quote.grossTotal - quote.netTotal).toFixed(2)} {quote.currencyCode}
                </span>
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
                <span>{t("quotations.total")}:</span>
                <span>{quote.grossTotal.toFixed(2)} {quote.currencyCode}</span>
              </div>
            </div>

            {/* Acceptance Evidence Box if Accepted */}
            {(quote.acceptances?.length ?? 0) > 0 && (
              <div
                style={{
                  backgroundColor: "var(--color-status-success-subtle, #f0fdf4)",
                  border: "1px solid #86efac",
                  borderRadius: "var(--radius-md)",
                  padding: "0.875rem 1rem",
                  display: "flex",
                  flexDirection: "column",
                  gap: "0.25rem"
                }}
              >
                <strong style={{ fontSize: "0.875rem", color: "#166534" }}>
                  {t("quotations.acceptedOnEvidence")}
                </strong>
                {quote.acceptances?.map((acc) => (
                  <div key={acc.revisionNumber} style={{ fontSize: "0.8125rem", color: "#15803d" }}>
                    <span>{t("common.revisionPrefix", { version: acc.revisionNumber })} · {acc.acceptanceMethod} · <LocalDateDisplay value={acc.acceptedAt} /></span>
                    {acc.evidenceReference && <span style={{ marginInlineStart: "0.5rem", fontFamily: "monospace" }}>({acc.evidenceReference})</span>}
                  </div>
                ))}
              </div>
            )}
          </>
        )}

        {/* Modal Footer */}
        <div style={{ display: "flex", justifyContent: "flex-end", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={onClose} type="button">
            {t("common.close")}
          </Button>
        </div>
      </div>

      {/* Record Acceptance Sub-Dialog */}
      {isAcceptModalOpen && (
        <Dialog
          isOpen={isAcceptModalOpen}
          onOpenChange={(open) => { if (!open) setIsAcceptModalOpen(false); }}
          title={t("quotations.recordAcceptance")}
          isDismissable
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "380px" }}>
            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
                {t("quotations.acceptanceMethod")}
              </label>
              <select
                value={acceptanceMethod}
                onChange={(e) => setAcceptanceMethod(e.target.value)}
                style={{
                  width: "100%",
                  padding: "0.5rem",
                  borderRadius: "var(--radius-sm)",
                  border: "1px solid var(--color-border-default)"
                }}
              >
                <option value="Email">{t("quotations.methodEmail")}</option>
                <option value="PurchaseOrder">{t("quotations.methodPurchaseOrder")}</option>
                <option value="SignedContract">{t("quotations.methodSignedContract")}</option>
                <option value="PhoneOrVerbal">{t("quotations.methodPhoneOrVerbal")}</option>
                <option value="DirectCounterSale">{t("quotations.methodDirectCounterSale")}</option>
              </select>
            </div>

            <TextField
              label={t("quotations.evidenceReference")}
              value={evidenceReference}
              onChange={setEvidenceReference}
              placeholder={t("quotations.evidenceReferencePlaceholder")}
            />

            <TextField
              label={t("quotations.note")}
              value={acceptanceNote}
              onChange={setAcceptanceNote}
            />

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.5rem" }}>
              <Button variant="secondary" onPress={() => setIsAcceptModalOpen(false)} type="button">
                {t("common.cancel")}
              </Button>
              <Button
                variant="primary"
                onPress={handleRecordAcceptance}
                type="button"
                isDisabled={acceptMutation.isPending}
              >
                {acceptMutation.isPending ? t("common.saving") : t("quotations.confirmAcceptance")}
              </Button>
            </div>
          </div>
        </Dialog>
      )}
    </Dialog>
  );
}
