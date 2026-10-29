import React from "react";
import { useTranslation } from "react-i18next";
import { PurchasingKpiCard } from "../../components/purchasing-kpi-card.tsx";
import { PurchasingStatusBadge } from "../../components/purchasing-status-badge.tsx";
import { PurchasingSupplierDisplay } from "../../components/purchasing-supplier-display.tsx";
import { formatPoNumber, toMoneyVal } from "../../utils.ts";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import type { PurchaseOrderListItem } from "../../types.ts";

export interface PurchasingOverviewTabProps {
  readonly orders: PurchaseOrderListItem[];
  readonly isLoading: boolean;
  readonly onSelectOrder: (id: string) => void;
  readonly onNewOrder: () => void;
  readonly hasPriceReadPermission?: boolean | undefined;
  readonly isAdvanced?: boolean | undefined;
}

export function PurchasingOverviewTab({
  orders,
  isLoading,
  onSelectOrder,
  onNewOrder,
  hasPriceReadPermission = true,
  isAdvanced = false
}: PurchasingOverviewTabProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");

  const draftCount = orders.filter((o) => o.state === "Draft").length;
  const openCount = orders.filter((o) => o.state === "Approved" || o.state === "Open" || o.state === "PartiallyCompleted").length;
  const recentInvoicedCount = orders.filter((o) => o.invoicingState === "Invoiced" || o.invoicingState === "PartiallyInvoiced").length;
  const partialReceiptCount = orders.filter((o) => o.receiptState === "PartiallyReceived").length;
  const varianceCount = orders.filter((o) => o.matchingState === "Variance").length;

  const totalCommitment = orders.reduce((sum, o) => sum + (o.netTotal || 0), 0);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Primary KPI Row */}
      <div style={{ display: "flex", flexWrap: "wrap", gap: "1rem" }}>
        <PurchasingKpiCard
          title={t("overview.draftOrders")}
          value={draftCount}
          subtitle={t("overview.draftOrdersDesc")}
          tone="default"
        />
        <PurchasingKpiCard
          title={t("overview.openOrders")}
          value={openCount}
          subtitle={t("overview.openOrdersDesc")}
          tone="primary"
        />
        {hasPriceReadPermission && (
          <PurchasingKpiCard
            title={t("overview.commercialCommitment")}
            value={`${toMoneyVal(totalCommitment).amount} ${toMoneyVal(totalCommitment).currency}`}
            subtitle={t("overview.commercialCommitmentDesc")}
            tone="success"
          />
        )}
        <PurchasingKpiCard
          title={t("overview.recentlyInvoiced")}
          value={recentInvoicedCount}
          subtitle={t("overview.recentlyInvoicedDesc")}
          tone="default"
        />
        {isAdvanced && (
          <>
            <PurchasingKpiCard
              title={t("overview.partialReceipts")}
              value={partialReceiptCount}
              subtitle={t("overview.partialReceiptsDesc")}
              tone="warning"
            />
            <PurchasingKpiCard
              title={t("overview.matchingExceptions")}
              value={varianceCount}
              subtitle={t("overview.matchingExceptionsDesc")}
              tone={varianceCount > 0 ? "danger" : "default"}
            />
          </>
        )}
      </div>

      {/* Quick Action & Recent Orders Card */}
      <div
        style={{
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 0.75rem)",
          boxShadow: "var(--shadow-elevation-low)",
          padding: "1.5rem",
          display: "flex",
          flexDirection: "column",
          gap: "1rem"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "0.5rem" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("overview.recentOrders")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("overview.recentOrdersSubtitle")}
            </p>
          </div>
          <Button variant="primary" onPress={onNewOrder} type="button">
            + {t("orders.newOrder")}
          </Button>
        </div>

        {/* Table */}
        <div style={{ overflowX: "auto" }}>
          <Table aria-label={t("overview.recentOrders")}>
            <TableHeader>
              <TableRow>
                <TableHead>{t("orders.number")}</TableHead>
                <TableHead>{t("orders.supplier")}</TableHead>
                <TableHead>{t("orders.orderDate")}</TableHead>
                {hasPriceReadPermission && <TableHead>{t("orders.netTotal")}</TableHead>}
                <TableHead>{t("orders.commercialState")}</TableHead>
                <TableHead>{t("orders.receiptState")}</TableHead>
                <TableHead>{t("orders.invoicingState")}</TableHead>
                <TableHead>{t("common.actions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {orders.slice(0, 5).map((order) => (
                <TableRow key={order.id}>
                  <TableCell>
                    <span style={{ fontFamily: "monospace", fontWeight: 600, color: "var(--color-brand-primary)" }}>
                      {order.poNumber || formatPoNumber(order.id)}
                    </span>
                  </TableCell>
                  <TableCell>
                    <PurchasingSupplierDisplay supplierPartyId={order.supplierPartyId} />
                  </TableCell>
                  <TableCell>
                    <LocalDateDisplay value={order.orderDate} />
                  </TableCell>
                  {hasPriceReadPermission && (
                    <TableCell>
                      <MoneyDisplay value={toMoneyVal(order.netTotal, order.currencyCode)} />
                    </TableCell>
                  )}
                  <TableCell>
                    <PurchasingStatusBadge state={order.state} kind="commercial" />
                  </TableCell>
                  <TableCell>
                    <PurchasingStatusBadge state={order.receiptState || "NotReceived"} kind="receipt" />
                  </TableCell>
                  <TableCell>
                    <PurchasingStatusBadge state={order.invoicingState || "NotInvoiced"} kind="invoicing" />
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="secondary"
                      onPress={() => onSelectOrder(order.id)}
                      type="button"
                    >
                      {t("common.details")}
                    </Button>
                  </TableCell>
                </TableRow>
              ))}

              {orders.length === 0 && !isLoading && (
                <TableRow>
                  <TableCell colSpan={hasPriceReadPermission ? 8 : 7} style={{ textAlign: "center", padding: "2rem" }}>
                    <p style={{ margin: "0 0 1rem", color: "var(--color-content-muted)" }}>
                      {t("orders.noOrders")}
                    </p>
                    <Button variant="primary" onPress={onNewOrder} type="button">
                      + {t("orders.newOrder")}
                    </Button>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </div>
      </div>
    </div>
  );
}
