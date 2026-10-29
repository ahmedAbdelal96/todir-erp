import React from "react";
import { useTranslation } from "react-i18next";
import { PurchasingDataTable } from "../../components/purchasing-data-table.tsx";
import { PurchasingStatusBadge } from "../../components/purchasing-status-badge.tsx";
import { PurchasingSupplierDisplay } from "../../components/purchasing-supplier-display.tsx";
import { formatPoNumber } from "../../utils.ts";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import type { PurchaseOrderListItem } from "../../types.ts";

export interface ReceiptsTabProps {
  readonly orders: PurchaseOrderListItem[];
  readonly isLoading: boolean;
  readonly onSelectOrder: (id: string) => void;
  readonly onOpenReceive: (order: PurchaseOrderListItem) => void;
  readonly onOpenReceiveAndBill?: (order: PurchaseOrderListItem) => void;
}

export function ReceiptsTab({
  orders,
  isLoading,
  onSelectOrder,
  onOpenReceive,
  onOpenReceiveAndBill
}: ReceiptsTabProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");

  const stockOrders = orders.filter((o) => o.state !== "Draft" && o.state !== "Cancelled");

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("receipts.title")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("receipts.subtitle")}
          </p>
        </div>
      </div>

      <PurchasingDataTable minWidth="100%">
        <Table aria-label={t("receipts.title")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("orders.number")}</TableHead>
              <TableHead>{t("orders.supplier")}</TableHead>
              <TableHead>{t("orders.orderDate")}</TableHead>
              <TableHead>{t("orders.receiptState")}</TableHead>
              <TableHead>{t("orders.commercialState")}</TableHead>
              <TableHead>{t("common.actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {stockOrders.map((order) => (
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
                <TableCell>
                  <PurchasingStatusBadge state={order.receiptState || "NotReceived"} kind="receipt" />
                </TableCell>
                <TableCell>
                  <PurchasingStatusBadge state={order.state} kind="commercial" />
                </TableCell>
                <TableCell>
                  <div style={{ display: "flex", gap: "0.5rem" }}>
                    {order.receiptState !== "Received" && (
                      <Button
                        variant="primary"
                        onPress={() => onOpenReceive(order)}
                        type="button"
                      >
                        {t("orders.receiveStock")}
                      </Button>
                    )}
                    {order.receiptState !== "Received" && onOpenReceiveAndBill && (
                      <Button
                        variant="secondary"
                        onPress={() => onOpenReceiveAndBill(order)}
                        type="button"
                      >
                        {t("orders.receiveAndBill")}
                      </Button>
                    )}
                    <Button
                      variant="secondary"
                      onPress={() => onSelectOrder(order.id)}
                      type="button"
                    >
                      {t("common.details")}
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}

            {stockOrders.length === 0 && !isLoading && (
              <TableRow>
                <TableCell colSpan={6} style={{ textAlign: "center", padding: "3rem" }}>
                  <p style={{ margin: 0, color: "var(--color-content-muted)" }}>
                    {t("receipts.noReceipts")}
                  </p>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </PurchasingDataTable>
    </div>
  );
}
