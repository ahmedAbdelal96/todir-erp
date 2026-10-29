import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { PurchasingStatusBadge } from "../../components/purchasing-status-badge.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { useMatchPurchaseOrderMutation } from "../../hooks/use-purchasing-queries.ts";
import { mapPurchasingError } from "../../services/purchasing-error-map.ts";
import { toMoneyVal, generateIdempotencyKey } from "../../utils.ts";
import type { PurchaseOrderView } from "../../types.ts";

const DEFAULT_UOM = "EA";

export interface MatchOrderModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly order: PurchaseOrderView;
  readonly onMatched?: () => void;
  readonly hasMatchOverridePermission?: boolean | undefined;
}

export function MatchOrderModal({
  isOpen,
  onClose,
  order,
  onMatched,
  hasMatchOverridePermission = true
}: MatchOrderModalProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const [basis, setBasis] = useState<"TwoWay" | "ThreeWay">("ThreeWay");
  const [isApprovingException, setIsApprovingException] = useState(false);
  const [exceptionReason, setExceptionReason] = useState("");
  const [matchResultState, setMatchResultState] = useState<string | null>(order.matchingState || null);
  const [varianceAmount, setVarianceAmount] = useState<number>(order.matchingState === "Variance" ? 150.0 : 0);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const matchMutation = useMatchPurchaseOrderMutation(order.legalEntityId, order.id);

  const handleEvaluateMatch = async (approveException = false) => {
    setErrorMessage(null);
    try {
      const result = await matchMutation.mutateAsync({
        version: order.version,
        supplierInvoiceId: "00000000-0000-0000-0000-000000000001",
        basis,
        quantityTolerance: 0,
        priceAbsoluteTolerance: 10,
        pricePercentageTolerance: 1.0,
        taxTolerance: 5,
        idempotencyKey: generateIdempotencyKey("match"),
        approveException,
        reason: approveException ? exceptionReason.trim() : null
      });

      setMatchResultState(result.state);
      setVarianceAmount(result.varianceAmount);
      setIsApprovingException(false);
      if (onMatched) onMatched();
    } catch (err) {
      const mapped = mapPurchasingError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  return (
    <Dialog isOpen={isOpen} onOpenChange={(open) => { if (!open) onClose(); }} title={t("matching.evaluateTitle")}>
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", maxWidth: "52rem" }}>
        {errorMessage && (
          <div role="alert" style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        {/* Basis Switcher */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "0.75rem" }}>
          <div style={{ display: "flex", gap: "0.5rem" }}>
            <button
              type="button"
              onClick={() => setBasis("ThreeWay")}
              style={{
                padding: "0.5rem 1rem",
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: basis === "ThreeWay" ? "var(--color-brand-primary-light)" : "var(--color-surface-card)",
                color: basis === "ThreeWay" ? "var(--color-brand-primary)" : "var(--color-content-primary)",
                fontWeight: 600,
                fontSize: "0.875rem",
                cursor: "pointer"
              }}
            >
              {t("matching.threeWayBasis")}
            </button>
            <button
              type="button"
              onClick={() => setBasis("TwoWay")}
              style={{
                padding: "0.5rem 1rem",
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: basis === "TwoWay" ? "var(--color-brand-primary-light)" : "var(--color-surface-card)",
                color: basis === "TwoWay" ? "var(--color-brand-primary)" : "var(--color-content-primary)",
                fontWeight: 600,
                fontSize: "0.875rem",
                cursor: "pointer"
              }}
            >
              {t("matching.twoWayBasis")}
            </button>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("matching.currentResult")}:
            </span>
            <PurchasingStatusBadge state={matchResultState || "WithinTolerance"} kind="matching" />
          </div>
        </div>

        {/* Side-by-Side Comparison Table */}
        <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflowX: "auto" }}>
          <Table aria-label={t("matching.evaluateTitle")}>
            <TableHeader>
              <TableRow>
                <TableHead>{t("orders.lineDescription")}</TableHead>
                <TableHead>{t("matching.poQuantity")}</TableHead>
                {basis === "ThreeWay" && <TableHead>{t("matching.receivedQuantity")}</TableHead>}
                <TableHead>{t("matching.invoicedQuantity")}</TableHead>
                <TableHead>{t("matching.poUnitPrice")}</TableHead>
                <TableHead>{t("matching.invoicedUnitPrice")}</TableHead>
                <TableHead>{t("matching.variance")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {order.lines.map((line) => {
                const uom = line.unitCode || DEFAULT_UOM;
                const invoiceQty = line.invoicedQuantity > 0 ? line.invoicedQuantity : line.orderedQuantity;
                const invoicePrice = line.unitPrice;
                const qtyDiff = (basis === "ThreeWay" ? line.receivedQuantity : line.orderedQuantity) - invoiceQty;
                return (
                  <TableRow key={line.id}>
                    <TableCell>{line.description}</TableCell>
                    <TableCell>{line.orderedQuantity} {uom}</TableCell>
                    {basis === "ThreeWay" && <TableCell>{line.receivedQuantity} {uom}</TableCell>}
                    <TableCell>{invoiceQty} {uom}</TableCell>
                    <TableCell><MoneyDisplay value={toMoneyVal(line.unitPrice, order.currencyCode)} /></TableCell>
                    <TableCell><MoneyDisplay value={toMoneyVal(invoicePrice, order.currencyCode)} /></TableCell>
                    <TableCell>
                      <span style={{ color: qtyDiff !== 0 ? "var(--color-status-warning)" : "var(--color-status-success)", fontWeight: 600 }}>
                        {qtyDiff === 0 ? t("matching.matched") : `${qtyDiff} ${uom}`}
                      </span>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>

        {/* Tolerance & Variance Summary Card */}
        <div
          style={{
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-default)",
            borderRadius: "var(--radius-md)",
            padding: "1rem",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "0.5rem"
          }}
        >
          <div>
            <span style={{ fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
              {t("matching.tolerances")}:
            </span>
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginTop: "0.125rem" }}>
              <span>{t("matching.priceToleranceSummary")}</span>
            </div>
          </div>

          <div style={{ textAlign: "right" }}>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("matching.totalVariance")}:
            </span>
            <div style={{ fontSize: "1.125rem", fontWeight: 700, color: varianceAmount > 0 ? "var(--color-status-warning)" : "var(--color-status-success)" }}>
              <MoneyDisplay value={toMoneyVal(varianceAmount, order.currencyCode)} />
            </div>
          </div>
        </div>

        {/* Exception Approval Form when Variance exists */}
        {isApprovingException && (
          <div style={{ border: "1px solid var(--color-status-warning)", backgroundColor: "var(--color-status-warning-light, #fffbeb)", borderRadius: "var(--radius-md)", padding: "1rem" }}>
            <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-status-warning)" }}>
              {t("matching.approveExceptionTitle")}
            </h4>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
              <label style={{ fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
                {t("matching.exceptionReason")} *
              </label>
              <textarea
                required
                rows={2}
                value={exceptionReason}
                onChange={(e) => setExceptionReason(e.target.value)}
                placeholder={t("matching.exceptionReasonPlaceholder")}
                style={{
                  width: "100%",
                  padding: "0.5rem",
                  borderRadius: "var(--radius-sm)",
                  border: "1px solid var(--color-border-default)",
                  backgroundColor: "var(--color-surface-card)"
                }}
              />
              <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
                <Button variant="secondary" onPress={() => setIsApprovingException(false)} type="button">
                  {t("common.cancel")}
                </Button>
                <Button
                  variant="primary"
                  isDisabled={!exceptionReason.trim()}
                  onPress={() => handleEvaluateMatch(true)}
                  type="button"
                >
                  {t("matching.confirmExceptionApproval")}
                </Button>
              </div>
            </div>
          </div>
        )}

        {/* Action Buttons */}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={onClose} type="button">
            {t("common.close")}
          </Button>

          {!isApprovingException && (
            <>
              <Button variant="secondary" onPress={() => handleEvaluateMatch(false)} type="button">
                {t("matching.reevaluate")}
              </Button>

              {hasMatchOverridePermission && (
                <Button variant="primary" onPress={() => setIsApprovingException(true)} type="button">
                  {t("matching.approveException")}
                </Button>
              )}
            </>
          )}
        </div>
      </div>
    </Dialog>
  );
}
