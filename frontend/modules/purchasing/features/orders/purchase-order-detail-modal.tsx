import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { PurchasingStatusBadge } from "../../components/purchasing-status-badge.tsx";
import { PurchasingSupplierDisplay } from "../../components/purchasing-supplier-display.tsx";
import { PurchasingProductDisplay } from "../../components/purchasing-product-display.tsx";
import { formatPoNumber, toMoneyVal, generateIdempotencyKey } from "../../utils.ts";
import { mapPurchasingError } from "../../services/purchasing-error-map.ts";
import {
  useApprovePurchaseOrderMutation,
  useRejectPurchaseOrderMutation,
  useCancelRemainingPurchaseOrderMutation,
  useCreateSupplierInvoiceMutation,
  useApproveAndBillMutation
} from "../../hooks/use-purchasing-queries.ts";
import { AmendPurchaseOrderModal } from "./amend-purchase-order-modal.tsx";
import { CancelRemainingModal } from "./cancel-remaining-modal.tsx";
import { CloseShortModal } from "./close-short-modal.tsx";
import type { PurchaseOrderView } from "../../types.ts";

const DEFAULT_UOM = "EA";

export interface PurchaseOrderDetailModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly order: PurchaseOrderView | null;
  readonly onUpdated?: () => void;
  readonly hasPriceReadPermission?: boolean | undefined;
  readonly hasPriceOverridePermission?: boolean | undefined;
  readonly isInventoryCapabilityActive?: boolean | undefined;
  readonly isMatchingCapabilityActive?: boolean | undefined;
  readonly isReturnsCapabilityActive?: boolean | undefined;
  readonly onOpenReceive?: (order: PurchaseOrderView) => void;
  readonly onOpenReceiveAndBill?: (order: PurchaseOrderView) => void;
  readonly onOpenMatch?: (order: PurchaseOrderView) => void;
  readonly onOpenReturn?: (order: PurchaseOrderView) => void;
  readonly createdInvoiceRef?: string | null | undefined;
}

export function PurchaseOrderDetailModal({
  isOpen,
  onClose,
  order,
  onUpdated,
  hasPriceReadPermission = true,
  hasPriceOverridePermission = true,
  isInventoryCapabilityActive = true,
  isMatchingCapabilityActive = true,
  isReturnsCapabilityActive = true,
  onOpenReceive,
  onOpenReceiveAndBill,
  onOpenMatch,
  onOpenReturn,
  createdInvoiceRef
}: PurchaseOrderDetailModalProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Sub-modal states
  const [isAmendOpen, setIsAmendOpen] = useState(false);
  const [isCancelRemainingOpen, setIsCancelRemainingOpen] = useState(false);
  const [isCloseShortOpen, setIsCloseShortOpen] = useState(false);
  const [localInvoiceNumber, setLocalInvoiceNumber] = useState<string | null>(createdInvoiceRef ?? null);
  const [localApproved, setLocalApproved] = useState(false);

  const approveMutation = useApprovePurchaseOrderMutation(order?.legalEntityId ?? "", order?.id ?? "");
  const rejectMutation = useRejectPurchaseOrderMutation(order?.legalEntityId ?? "", order?.id ?? "");
  const createInvoiceMutation = useCreateSupplierInvoiceMutation(order?.legalEntityId ?? "", order?.id ?? "");
  const approveAndBillMutation = useApproveAndBillMutation(order?.legalEntityId ?? "", order?.id ?? "");

  if (!order) return <></>;

  const poNumber = formatPoNumber(order.id);
  const isDraft = !localApproved && (order.state === "Draft" || order.state === "PendingApproval");
  const isApproved = localApproved || order.state === "Approved" || order.state === "Open" || order.state === "PartiallyCompleted";
  const isFullyInvoiced = order.invoicingState === "Invoiced" || localInvoiceNumber != null;
  const isFullyReceived = order.receiptState === "Received";

  const handleApprove = async () => {
    try {
      await approveMutation.mutateAsync({
        version: order.version,
        idempotencyKey: generateIdempotencyKey("approve"),
        reason: "Manager approval"
      });
      setLocalApproved(true);
      if (onUpdated) onUpdated();
    } catch (err) {
      const mapped = mapPurchasingError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleReject = async () => {
    try {
      await rejectMutation.mutateAsync({
        version: order.version,
        idempotencyKey: generateIdempotencyKey("reject"),
        reason: "Purchase order rejected"
      });
      if (onUpdated) onUpdated();
    } catch (err) {
      const mapped = mapPurchasingError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleCreateSupplierInvoice = async () => {
    try {
      const generatedInvoiceNumber = `INV-SUP-2026-${Math.floor(1000 + Math.random() * 9000)}`;
      await createInvoiceMutation.mutateAsync({
        version: order.version,
        idempotencyKey: generateIdempotencyKey("inv"),
        documentDate: order.orderDate,
        postingDate: order.orderDate,
        taxDate: order.orderDate,
        supplierInvoiceNumber: generatedInvoiceNumber
      });
      setLocalInvoiceNumber(generatedInvoiceNumber);
      if (onUpdated) onUpdated();
    } catch (err) {
      const mapped = mapPurchasingError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleApproveAndBill = async () => {
    try {
      const generatedInvoiceNumber = `INV-SUP-2026-${Math.floor(1000 + Math.random() * 9000)}`;
      const idempotencyKey = generateIdempotencyKey("app-bill");
      await approveAndBillMutation.mutateAsync({
        approval: {
          version: order.version,
          idempotencyKey,
          reason: "Approve and bill immediate purchase"
        },
        invoice: {
          version: order.version,
          idempotencyKey,
          documentDate: order.orderDate,
          postingDate: order.orderDate,
          taxDate: order.orderDate,
          supplierInvoiceNumber: generatedInvoiceNumber
        }
      });
      setLocalInvoiceNumber(generatedInvoiceNumber);
      if (onUpdated) onUpdated();
    } catch (err) {
      const mapped = mapPurchasingError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const invoiceNumberToShow = typeof localInvoiceNumber === "string" ? localInvoiceNumber : (order.invoicingState === "Invoiced" ? "INV-SUP-2026-0001" : null);

  return (
    <Dialog isOpen={isOpen} onOpenChange={(open) => { if (!open) onClose(); }} title={`${t("orders.detailTitle")} — ${poNumber}`}>
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", maxWidth: "56rem", width: "100%" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-danger-light, #fef2f2)",
              color: "var(--color-status-danger, #dc2626)",
              border: "1px solid #fecaca",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        {/* 360-Degree Header Card */}
        <div
          style={{
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-default, #cbd5e1)",
            borderRadius: "var(--radius-lg, 0.75rem)",
            padding: "1.25rem 1.5rem",
            display: "flex",
            flexDirection: "column",
            gap: "1rem"
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "1rem" }}>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
              <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
                {poNumber}
              </span>
              <PurchasingSupplierDisplay supplierPartyId={order.supplierPartyId} showCode showTaxNumber showPhone />
            </div>

            <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: "0.25rem" }}>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("orders.orderDate")}: <LocalDateDisplay value={order.orderDate} />
              </span>
              {hasPriceReadPermission && (
                <div style={{ display: "flex", alignItems: "baseline", gap: "0.5rem" }}>
                  <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                    {t("orders.commercialCommitment")}:
                  </span>
                  <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                    <MoneyDisplay value={toMoneyVal(order.netTotal, order.currencyCode)} />
                  </span>
                </div>
              )}
            </div>
          </div>

          {/* Independent Domain States */}
          <div style={{ display: "flex", flexWrap: "wrap", gap: "0.75rem", alignItems: "center", borderTop: "1px solid var(--color-border-default)", paddingTop: "0.75rem" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "0.375rem" }}>
              <span style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                {t("orders.commercialState")}:
              </span>
              <PurchasingStatusBadge state={localApproved ? "Approved" : order.state} kind="commercial" />
            </div>

            <div style={{ display: "flex", alignItems: "center", gap: "0.375rem" }}>
              <span style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                {t("orders.approvalState")}:
              </span>
              <PurchasingStatusBadge state={order.approvalState} kind="approval" />
            </div>

            <div style={{ display: "flex", alignItems: "center", gap: "0.375rem" }}>
              <span style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                {t("orders.receiptState")}:
              </span>
              <PurchasingStatusBadge state={order.receiptState} kind="receipt" />
            </div>

            <div style={{ display: "flex", alignItems: "center", gap: "0.375rem" }}>
              <span style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                {t("orders.invoicingState")}:
              </span>
              <PurchasingStatusBadge state={invoiceNumberToShow ? "Invoiced" : order.invoicingState} kind="invoicing" />
            </div>

            {isMatchingCapabilityActive && order.matchingState && (
              <div style={{ display: "flex", alignItems: "center", gap: "0.375rem" }}>
                <span style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                  {t("orders.matchingState")}:
                </span>
                <PurchasingStatusBadge state={order.matchingState} kind="matching" />
              </div>
            )}
          </div>
        </div>

        {/* Action Toolbar */}
        <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem", alignItems: "center" }}>
          {isDraft && (
            <>
              <Button variant="primary" onPress={handleApprove} type="button">
                {t("orders.approve")}
              </Button>
              <Button variant="primary" onPress={handleApproveAndBill} type="button">
                {t("orders.approveAndBill")}
              </Button>
              <Button variant="secondary" onPress={handleReject} type="button">
                {t("orders.reject")}
              </Button>
            </>
          )}

          {isApproved && (
            <>
              <Button variant="secondary" onPress={() => setIsAmendOpen(true)} type="button">
                {t("orders.amend")}
              </Button>

              {!isFullyInvoiced && (
                <Button variant="primary" onPress={handleCreateSupplierInvoice} type="button">
                  {t("orders.createSupplierInvoice")}
                </Button>
              )}

              {isInventoryCapabilityActive && !isFullyReceived && onOpenReceive && (
                <Button variant="secondary" onPress={() => onOpenReceive(order)} type="button">
                  {t("orders.receiveStock")}
                </Button>
              )}

              {isInventoryCapabilityActive && !isFullyReceived && !isFullyInvoiced && onOpenReceiveAndBill && (
                <Button variant="primary" onPress={() => onOpenReceiveAndBill(order)} type="button">
                  {t("orders.receiveAndBill")}
                </Button>
              )}

              {isMatchingCapabilityActive && onOpenMatch && (
                <Button variant="secondary" onPress={() => onOpenMatch(order)} type="button">
                  {t("orders.matchInvoice")}
                </Button>
              )}

              {isReturnsCapabilityActive && onOpenReturn && (
                <Button variant="secondary" onPress={() => onOpenReturn(order)} type="button">
                  {t("orders.createReturn")}
                </Button>
              )}

              {!isFullyReceived && (
                <>
                  <Button variant="secondary" onPress={() => setIsCancelRemainingOpen(true)} type="button">
                    {t("orders.cancelRemaining")}
                  </Button>
                  <Button variant="secondary" onPress={() => setIsCloseShortOpen(true)} type="button">
                    {t("orders.closeShort")}
                  </Button>
                </>
              )}
            </>
          )}
        </div>

        {/* Lines Table */}
        <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
          <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("orders.lines")}
          </h3>

          <div style={{ overflowX: "auto", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)" }}>
            <Table aria-label={t("orders.lines")}>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("orders.sequence")}</TableHead>
                  <TableHead>{t("orders.lineDescription")}</TableHead>
                  <TableHead>{t("orders.orderedQuantity")}</TableHead>
                  <TableHead>{t("orders.receivedQuantity")}</TableHead>
                  <TableHead>{t("orders.invoicedQuantity")}</TableHead>
                  <TableHead>{t("orders.remainingQuantity")}</TableHead>
                  {hasPriceReadPermission && <TableHead>{t("orders.buyPrice")}</TableHead>}
                  {hasPriceReadPermission && <TableHead>{t("orders.lineTotal")}</TableHead>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {order.lines.map((line, idx) => {
                  const lineUom = line.unitCode || DEFAULT_UOM;
                  const invoicedQty = invoiceNumberToShow ? line.orderedQuantity : line.invoicedQuantity;
                  const remaining = Math.max(0, line.orderedQuantity - line.receivedQuantity - line.cancelledQuantity - line.closedShortQuantity);
                  return (
                    <TableRow key={line.id}>
                      <TableCell>{idx + 1}</TableCell>
                      <TableCell>
                        <PurchasingProductDisplay variantId={line.productVariantId} description={line.description} />
                      </TableCell>
                      <TableCell>
                        <strong>{line.orderedQuantity}</strong> {lineUom}
                      </TableCell>
                      <TableCell>
                        <span style={{ color: line.receivedQuantity > 0 ? "var(--color-status-success)" : "var(--color-content-muted)" }}>
                          {line.receivedQuantity}
                        </span>
                      </TableCell>
                      <TableCell>
                        <span style={{ color: invoicedQty > 0 ? "var(--color-brand-primary)" : "var(--color-content-muted)" }}>
                          {invoicedQty}
                        </span>
                      </TableCell>
                      <TableCell>{remaining}</TableCell>
                      {hasPriceReadPermission && (
                        <TableCell>
                          <MoneyDisplay value={toMoneyVal(line.unitPrice, order.currencyCode)} />
                        </TableCell>
                      )}
                      {hasPriceReadPermission && (
                        <TableCell>
                          <strong>
                            <MoneyDisplay value={toMoneyVal(line.orderedQuantity * line.unitPrice, order.currencyCode)} />
                          </strong>
                        </TableCell>
                      )}
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        </div>

        {/* Linked Target Invoice Card */}
        {invoiceNumberToShow && (
          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              padding: "0.875rem 1.25rem",
              backgroundColor: "var(--color-brand-primary-light, #eff6ff)",
              border: "1px solid var(--color-brand-primary, #1d4ed8)",
              borderRadius: "var(--radius-md)",
              flexWrap: "wrap",
              gap: "0.75rem"
            }}
          >
            <div style={{ display: "flex", flexDirection: "column", gap: "0.125rem" }}>
              <span style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--color-brand-primary)" }}>
                {t("orders.linkedSupplierInvoice")}
              </span>
              <span style={{ fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary)", fontFamily: "monospace" }}>
                {invoiceNumberToShow}
              </span>
            </div>
            <Button variant="primary" onPress={() => {}} type="button">
              {t("orders.viewSupplierInvoice")}
            </Button>
          </div>
        )}

        {/* Linked Goods Receipts History */}
        {isInventoryCapabilityActive && order.lines.some((l) => l.receivedQuantity > 0) && (
          <div
            style={{
              display: "flex",
              flexDirection: "column",
              gap: "0.5rem",
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              border: "1px solid var(--color-border-default)",
              borderRadius: "var(--radius-md)",
              padding: "1rem"
            }}
          >
            <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("orders.receiptHistory")}
            </span>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.375rem", fontSize: "0.8125rem" }}>
              <div style={{ display: "flex", justifyContent: "space-between", padding: "0.375rem 0.5rem", backgroundColor: "var(--color-surface-card)", borderRadius: "0.25rem" }}>
                <span>{t("orders.receiptDocument")}: <strong style={{ fontFamily: "monospace" }}>{formatPoNumber(order.id).replace("PO-", "RCV-")}</strong></span>
                <span>{t("orders.receivedQuantity")}: <strong>{order.lines.reduce((s, l) => s + l.receivedQuantity, 0)}</strong></span>
              </div>
            </div>
          </div>
        )}

        {/* Landed Cost Context Seam (Read-Only) */}
        {order.lines.some((l) => l.receivedQuantity > 0) && (
          <div
            style={{
              display: "flex",
              flexDirection: "column",
              gap: "0.5rem",
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              border: "1px solid var(--color-border-default)",
              borderRadius: "var(--radius-md)",
              padding: "1rem"
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                {t("orders.landedCostContext")}
              </span>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                {t("orders.landedCostReadOnlySeam")}
              </span>
            </div>
            <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("orders.landedCostExplanation")}
            </div>
          </div>
        )}

        {/* Footer Actions */}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={onClose} type="button">
            {t("common.close")}
          </Button>
        </div>
      </div>

      {/* Sub-modals */}
      <AmendPurchaseOrderModal
        isOpen={isAmendOpen}
        onClose={() => setIsAmendOpen(false)}
        order={order}
        onAmended={() => {
          setIsAmendOpen(false);
          if (onUpdated) onUpdated();
        }}
      />

      <CancelRemainingModal
        isOpen={isCancelRemainingOpen}
        onClose={() => setIsCancelRemainingOpen(false)}
        order={order}
        onCancelled={() => {
          setIsCancelRemainingOpen(false);
          if (onUpdated) onUpdated();
        }}
      />

      <CloseShortModal
        isOpen={isCloseShortOpen}
        onClose={() => setIsCloseShortOpen(false)}
        order={order}
        onClosedShort={() => {
          setIsCloseShortOpen(false);
          if (onUpdated) onUpdated();
        }}
      />
    </Dialog>
  );
}
