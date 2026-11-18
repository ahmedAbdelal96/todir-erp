import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { useMovementHistory } from "../../hooks/use-inventory-queries.ts";
import { toQty } from "../../utils.ts";
import type { StockBalance, Warehouse } from "../../types.ts";

interface HistoryCursor {
  readonly effectiveAt: string;
  readonly sequence: number;
  readonly id: string;
}

export interface ProductStockCardModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly productVariantId: string;
  readonly productName?: string;
  readonly sku?: string;
  readonly balances: readonly StockBalance[];
  readonly warehouses: readonly Warehouse[];
  readonly onReceiveClick?: () => void;
  readonly onIssueClick?: () => void;
  readonly onTransferClick?: () => void;
  readonly onHistoryClick?: () => void;
}

export function ProductStockCardModal({
  isOpen,
  onClose,
  legalEntityId,
  productVariantId,
  productName,
  sku,
  balances,
  warehouses,
  onReceiveClick,
  onIssueClick,
  onTransferClick,
  onHistoryClick
}: ProductStockCardModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");

  const productBalances = balances.filter(b => b.productVariantId === productVariantId);
  const productIdentity = productBalances[0];
  const pageSize = 25;
  const [historyCursor, setHistoryCursor] = useState<HistoryCursor | null>(null);
  const [cursorStack, setCursorStack] = useState<readonly (HistoryCursor | null)[]>([]);
  const { data: movementPage, isLoading: isMovementHistoryLoading, isFetching: isMovementHistoryFetching, isError: isMovementHistoryError } = useMovementHistory(legalEntityId, {
    productVariantId,
    limit: pageSize,
    ...(historyCursor
      ? {
          beforeEffectiveAt: historyCursor.effectiveAt,
          beforeSequence: historyCursor.sequence,
          beforeId: historyCursor.id
        }
      : {})
  });

  useEffect(() => {
    setHistoryCursor(null);
    setCursorStack([]);
  }, [productVariantId]);

  const movements = movementPage?.items ?? [];
  const lastMovement = movements[movements.length - 1];
  const isHistoryBusy = isMovementHistoryLoading || isMovementHistoryFetching;

  const goToNextMovementPage = () => {
    if (!lastMovement || !movementPage?.hasMore) return;
    setCursorStack(previous => [...previous, historyCursor]);
    setHistoryCursor({ effectiveAt: lastMovement.effectiveAt, sequence: lastMovement.sequence, id: lastMovement.id });
  };

  const goToPreviousMovementPage = () => {
    if (cursorStack.length === 0) return;
    const previousCursor = cursorStack[cursorStack.length - 1] ?? null;
    setCursorStack(previous => previous.slice(0, -1));
    setHistoryCursor(previousCursor);
  };
  const totalOnHand = productBalances.reduce((sum, b) => sum + b.onHandBaseQuantity, 0);
  const totalReserved = productBalances.reduce((sum, b) => sum + b.reservedBaseQuantity, 0);
  const totalAvailable = productBalances.reduce((sum, b) => sum + b.availableBaseQuantity, 0);

  const warehouseName = (id: string) => warehouses.find(w => w.id === id)?.name ?? id;

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("stock.cardTitle")}
      size="large"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: "0.25rem 0" }}>
        {/* Header Summary */}
        <div
          style={{
            padding: "1rem",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-default, #cbd5e1)",
            borderRadius: "var(--radius-md, 0.5rem)",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "0.75rem"
          }}
        >
          <div>
            <InventoryProductDisplay
              productVariantId={productVariantId}
              displayName={productName ?? productIdentity?.productDisplayName}
              sku={sku ?? productIdentity?.productSku}
            />
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", display: "block", marginTop: "0.25rem" }}>
              {t("stock.cardSubtitle")}
            </span>
          </div>
          <div style={{ display: "flex", gap: "1rem" }}>
            <div style={{ textAlign: "center" }}>
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("stock.onHand")}</div>
              <div style={{ fontSize: "1.125rem", fontWeight: 700 }}><QuantityDisplay value={toQty(totalOnHand)} /></div>
            </div>
            <div style={{ textAlign: "center" }}>
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("stock.reserved")}</div>
              <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "var(--color-status-warning, #d97706)" }}>
                <QuantityDisplay value={toQty(totalReserved)} />
              </div>
            </div>
            <div style={{ textAlign: "center" }}>
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("stock.available")}</div>
              <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "var(--color-status-success, #16a34a)" }}>
                <QuantityDisplay value={toQty(totalAvailable)} />
              </div>
            </div>
          </div>
        </div>

        {/* Action Shortcuts */}
        <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
          {onReceiveClick && (
            <Button variant="secondary" onPress={() => { onClose(); onReceiveClick(); }}>
              {t("quickActions.receive")}
            </Button>
          )}
          {onIssueClick && (
            <Button variant="secondary" onPress={() => { onClose(); onIssueClick(); }}>
              {t("quickActions.issue")}
            </Button>
          )}
          {onTransferClick && (
            <Button variant="secondary" onPress={() => { onClose(); onTransferClick(); }}>
              {t("quickActions.transfer")}
            </Button>
          )}
          {onHistoryClick && (
            <Button variant="secondary" onPress={() => { onClose(); onHistoryClick(); }}>
              {t("history.title")}
            </Button>
          )}
        </div>

        {/* Warehouse Breakdown */}
        <div>
          <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.9375rem", color: "var(--color-content-primary)" }}>
            {t("stock.warehouseBreakdown")}
          </h4>
          <div
            style={{
              overflowX: "auto",
              border: "1px solid var(--color-border-default)",
              borderRadius: "var(--radius-md)"
            }}
          >
            <table style={{ width: "100%", minWidth: "550px", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
                  <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("stock.warehouse")}</th>
                  <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("stock.status")}</th>
                  <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("stock.onHand")}</th>
                  <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("stock.reserved")}</th>
                  <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("stock.available")}</th>
                </tr>
              </thead>
              <tbody>
                {productBalances.length === 0 ? (
                  <tr>
                    <td colSpan={5} style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                      {t("stock.noBalances")}
                    </td>
                  </tr>
                ) : (
                  productBalances.map((b, idx) => (
                    <tr
                      key={`${b.warehouseId}-${b.locationId}-${b.stockStatus}-${idx}`}
                      style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}
                    >
                      <td style={{ padding: "0.625rem 0.75rem" }}>{warehouseName(b.warehouseId)}</td>
                      <td style={{ padding: "0.625rem 0.75rem" }}>
                        <InventoryStatusBadge status={b.stockStatus} />
                      </td>
                      <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 600 }}>
                        <QuantityDisplay value={toQty(b.onHandBaseQuantity)} />
                      </td>
                      <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", color: "var(--color-status-warning)" }}>
                        <QuantityDisplay value={toQty(b.reservedBaseQuantity)} />
                      </td>
                      <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 600, color: "var(--color-status-success)" }}>
                        <QuantityDisplay value={toQty(b.availableBaseQuantity)} />
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "0.75rem", flexWrap: "wrap", marginBottom: "0.5rem" }}>
            <div>
              <h4 style={{ margin: 0, fontSize: "0.9375rem", color: "var(--color-content-primary)" }}>
                {t("stock.movementHistory")}
              </h4>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("stock.movementHistorySubtitle")}
              </span>
            </div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("stock.pageSize", { count: pageSize })}
            </span>
          </div>

          {isMovementHistoryError ? (
            <div role="alert" style={{ padding: "0.75rem", border: "1px solid #f87171", borderRadius: "var(--radius-sm)", backgroundColor: "#fef2f2", color: "#b91c1c", fontSize: "0.875rem" }}>
              {t("stock.movementHistoryError")}
            </div>
          ) : isMovementHistoryLoading ? (
            <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.loading")}
            </div>
          ) : movements.length === 0 ? (
            <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)" }}>
              {t("stock.noMovements")}
            </div>
          ) : (
            <>
              <div style={{ overflowX: "auto", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)" }}>
                <table style={{ width: "100%", minWidth: "620px", borderCollapse: "collapse", fontSize: "0.8125rem" }}>
                  <thead>
                    <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
                      <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("history.date")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("history.operationType")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("stock.warehouse")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("stock.status")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("history.quantity")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {movements.map(movement => (
                      <tr key={movement.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", opacity: isMovementHistoryFetching ? 0.6 : 1 }}>
                        <td style={{ padding: "0.625rem 0.75rem", whiteSpace: "nowrap" }}><LocalDateDisplay value={movement.operationalDate} /></td>
                        <td style={{ padding: "0.625rem 0.75rem", whiteSpace: "nowrap" }}>{t(`movementTypes.${movement.movementType}` as any, { defaultValue: movement.movementType })}</td>
                        <td style={{ padding: "0.625rem 0.75rem" }}>{warehouseName(movement.warehouseId)}</td>
                        <td style={{ padding: "0.625rem 0.75rem", whiteSpace: "nowrap" }}><InventoryStatusBadge status={movement.stockStatus} /></td>
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 700, color: movement.signedBaseQuantity >= 0 ? "var(--color-status-success)" : "var(--color-status-danger)" }}><QuantityDisplay value={toQty(movement.signedBaseQuantity)} /></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "0.75rem", marginTop: "0.75rem" }}>
                <Button variant="secondary" onPress={goToPreviousMovementPage} isDisabled={cursorStack.length === 0 || isHistoryBusy}>
                  {t("stock.previousPage")}
                </Button>
                <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                  {t("stock.pageIndicator", { page: cursorStack.length + 1 })}
                </span>
                <Button variant="secondary" onPress={goToNextMovementPage} isDisabled={!movementPage?.hasMore || isHistoryBusy}>
                  {t("stock.nextPage")}
                </Button>
              </div>
            </>
          )}
        </div>
      </div>
    </Dialog>
  );
}
