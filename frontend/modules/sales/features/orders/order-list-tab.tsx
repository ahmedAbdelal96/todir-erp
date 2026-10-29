import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { SearchField } from "../../../../components/primitives/search-field.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { SalesDataTable } from "../../components/sales-data-table.tsx";
import { SalesStatusBadge } from "../../components/sales-status-badge.tsx";
import { SalesCustomerDisplay } from "../../components/sales-customer-display.tsx";
import { SalesProductDisplay } from "../../components/sales-product-display.tsx";
import {
  useCancelOrderRemainingMutation,
  useConfirmOrderMutation,
  useCreateOrderInvoiceMutation,
  useFulfillOrderMutation,
  useReleaseReservationMutation,
  useSalesOrder,
  useSalesOrders
} from "../../hooks/use-sales-queries.ts";
import { mapSalesError } from "../../services/sales-error-map.ts";
import type { SalesOrderListItem } from "../../types.ts";

export interface OrderListTabProps {
  readonly legalEntityId: string;
  readonly isAdvancedDistributor?: boolean;
}

export function OrderListTab({ legalEntityId }: OrderListTabProps): React.JSX.Element {
  const { t } = useTranslation("sales");
  const [page, setPage] = useState(1);
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);

  const { data: ordersPage, isLoading } = useSalesOrders(legalEntityId, { page, pageSize: 50 });
  const orders = ordersPage?.items ?? [];

  const filteredOrders = orders.filter((o) => {
    if (!searchQuery.trim()) return true;
    const q = searchQuery.toLowerCase();
    return o.number.toLowerCase().includes(q) || o.partyId.toLowerCase().includes(q);
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header and Search */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div style={{ width: "280px" }}>
          <SearchField
            value={searchQuery}
            onChange={setSearchQuery}
            placeholder={t("orders.searchPlaceholder")}
            ariaLabel={t("orders.title")}
          />
        </div>
        <div>
          <span style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("orders.advancedNotice")}
          </span>
        </div>
      </div>

      {/* Main Table Card */}
      <div
        style={{
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 0.75rem)",
          padding: "1.25rem 1.5rem",
          display: "flex",
          flexDirection: "column",
          gap: "1rem",
          boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)"
        }}
      >
        {isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
          </div>
        ) : filteredOrders.length === 0 ? (
          <div style={{ padding: "3rem 1rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            <p style={{ margin: 0, fontSize: "0.9375rem" }}>{t("orders.noOrdersFound")}</p>
          </div>
        ) : (
          <SalesDataTable minWidth="900px">
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-subtle)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("orders.number")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("orders.customer")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("orders.commercialState")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("orders.fulfillmentState")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("orders.invoicingState")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "end" }}>{t("orders.orderedTotal")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "center" }}>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {filteredOrders.map((o: SalesOrderListItem) => (
                  <tr key={o.id} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600, fontFamily: "monospace", fontSize: "0.875rem", color: "var(--color-brand-primary)", whiteSpace: "nowrap" }}>
                      {o.number}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <SalesCustomerDisplay partyId={o.partyId} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <SalesStatusBadge kind="commercialState" status={o.commercialState} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <SalesStatusBadge kind="fulfillmentState" status={o.fulfillmentState} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <SalesStatusBadge kind="invoicingState" status={o.invoicingState} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 600, fontSize: "0.875rem" }}>
                      {(o.orderedTotal ?? 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                      <Button
                        variant="secondary"
                        onPress={() => setSelectedOrderId(o.id)}
                        type="button"
                      >
                        {t("common.manage")}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </SalesDataTable>
        )}
      </div>

      {/* Order Detail Modal */}
      {selectedOrderId && (
        <OrderDetailModal
          legalEntityId={legalEntityId}
          orderId={selectedOrderId}
          onClose={() => setSelectedOrderId(null)}
        />
      )}
    </div>
  );
}

interface OrderDetailModalProps {
  readonly legalEntityId: string;
  readonly orderId: string;
  readonly onClose: () => void;
}

function OrderDetailModal({ legalEntityId, orderId, onClose }: OrderDetailModalProps): React.JSX.Element {
  const { t } = useTranslation("sales");
  const { data: order, isLoading } = useSalesOrder(legalEntityId, orderId);

  const confirmMutation = useConfirmOrderMutation(legalEntityId, orderId);
  const releaseResMutation = useReleaseReservationMutation(legalEntityId, orderId);
  const cancelRemainingMutation = useCancelOrderRemainingMutation(legalEntityId, orderId);
  const fulfillMutation = useFulfillOrderMutation(legalEntityId, orderId);
  const invoiceMutation = useCreateOrderInvoiceMutation(legalEntityId, orderId);

  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const handleConfirm = async () => {
    setActionError(null);
    try {
      await confirmMutation.mutateAsync({
        version: order!.version,
        idempotencyKey: `ord-conf-${order!.id}-${Date.now()}`
      });
      setActionSuccess(t("orders.confirmedSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleReleaseReservation = async () => {
    setActionError(null);
    try {
      await releaseResMutation.mutateAsync({
        version: order!.version,
        idempotencyKey: `ord-rel-${order!.id}-${Date.now()}`
      });
      setActionSuccess(t("orders.reservationReleasedSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleFulfillAll = async () => {
    setActionError(null);
    try {
      const fulfillableLines = order!.lines
        .filter((l) => l.remainingQuantity > 0)
        .map((l) => ({
          orderLineId: l.id,
          quantity: l.remainingQuantity,
          warehouseId: order!.warehouseId ?? undefined
        }));

      if (fulfillableLines.length === 0) {
        setActionError(t("orders.nothingToFulfill"));
        return;
      }

      await fulfillMutation.mutateAsync({
        version: order!.version,
        idempotencyKey: `ord-ful-${order!.id}-${Date.now()}`,
        lines: fulfillableLines
      });
      setActionSuccess(t("orders.fulfilledSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleCreateInvoice = async () => {
    setActionError(null);
    try {
      await invoiceMutation.mutateAsync({
        version: order!.version,
        idempotencyKey: `ord-inv-${order!.id}-${Date.now()}`
      });
      setActionSuccess(t("orders.invoicedSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleCancelRemaining = async () => {
    setActionError(null);
    try {
      await cancelRemainingMutation.mutateAsync({
        version: order!.version,
        idempotencyKey: `ord-canc-${order!.id}-${Date.now()}`,
        reason: "CancelledRemaining"
      });
      setActionSuccess(t("orders.cancelledRemainingSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  return (
    <Dialog
      isOpen
      onOpenChange={(open) => { if (!open) onClose(); }}
      title={order ? `${t("orders.detailTitle")} — ${order.number}` : t("orders.detailTitle")}
      isDismissable
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", minWidth: "650px", maxWidth: "900px" }}>
        {isLoading && (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
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
              border: "1px solid var(--color-status-success)",
              borderRadius: "var(--radius-md)",
              color: "var(--color-status-success)",
              fontSize: "0.875rem"
            }}
          >
            {actionSuccess}
          </div>
        )}

        {order && (
          <>
            {/* Header info badge strip */}
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                padding: "1rem 1.25rem",
                borderRadius: "var(--radius-lg)",
                border: "1px solid var(--color-border-default)",
                flexWrap: "wrap",
                gap: "1rem"
              }}
            >
              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                  {t("orders.customer")}
                </span>
                <SalesCustomerDisplay partyId={order.partyId} />
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                  {t("orders.orderDate")}
                </span>
                <strong style={{ fontSize: "0.875rem" }}>{order.orderDate}</strong>
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                  {t("orders.commercialState")}
                </span>
                <SalesStatusBadge kind="commercialState" status={order.commercialState} />
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                  {t("orders.fulfillmentState")}
                </span>
                <SalesStatusBadge kind="fulfillmentState" status={order.fulfillmentState} />
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                  {t("orders.creditState")}
                </span>
                <SalesStatusBadge kind="creditState" status={order.creditState} />
              </div>
            </div>

            {/* Lifecycle Command Toolbar */}
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
              {order.commercialState === "Draft" && (
                <Button
                  variant="primary"
                  onPress={handleConfirm}
                  type="button"
                  isDisabled={confirmMutation.isPending}
                >
                  {t("orders.confirmOrder")}
                </Button>
              )}

              {order.commercialState === "Confirmed" && (
                <>
                  <Button
                    variant="primary"
                    onPress={handleFulfillAll}
                    type="button"
                    isDisabled={fulfillMutation.isPending}
                  >
                    {t("orders.fulfillShipment")}
                  </Button>

                  <Button
                    variant="secondary"
                    onPress={handleCreateInvoice}
                    type="button"
                    isDisabled={invoiceMutation.isPending}
                  >
                    {t("orders.createInvoice")}
                  </Button>

                  <Button
                    variant="secondary"
                    onPress={handleReleaseReservation}
                    type="button"
                    isDisabled={releaseResMutation.isPending}
                  >
                    {t("orders.releaseReservation")}
                  </Button>

                  <Button
                    variant="secondary"
                    onPress={handleCancelRemaining}
                    type="button"
                    isDisabled={cancelRemainingMutation.isPending}
                  >
                    {t("orders.cancelRemaining")}
                  </Button>
                </>
              )}
            </div>

            {/* Order Lines */}
            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
              <h4 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 600 }}>
                {t("orders.lines")} ({order.lines?.length ?? 0})
              </h4>
              <SalesDataTable minWidth="750px">
                <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start" }}>
                  <thead>
                    <tr style={{ borderBottom: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-subtle)" }}>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600 }}>#</th>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("orders.productOrDescription")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "end" }}>{t("orders.ordered")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "end" }}>{t("orders.fulfilled")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "end" }}>{t("orders.invoiced")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "end" }}>{t("orders.remaining")}</th>
                      <th style={{ padding: "0.625rem 0.75rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "end" }}>{t("orders.unitPrice")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(order.lines ?? []).map((l) => (
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
                          {l.orderedQuantity}
                        </td>
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontSize: "0.875rem" }}>
                          {l.fulfilledQuantity}
                        </td>
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontSize: "0.875rem" }}>
                          {l.invoicedQuantity}
                        </td>
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 600, fontSize: "0.875rem", color: l.remainingQuantity > 0 ? "var(--color-status-warning)" : "inherit" }}>
                          {l.remainingQuantity}
                        </td>
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontSize: "0.875rem" }}>
                          {l.unitPrice.toFixed(2)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </SalesDataTable>
            </div>
          </>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={onClose} type="button">
            {t("common.close")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
