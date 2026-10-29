import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { PurchasingDataTable } from "../../components/purchasing-data-table.tsx";
import { PurchasingStatusBadge } from "../../components/purchasing-status-badge.tsx";
import { PurchasingSupplierDisplay } from "../../components/purchasing-supplier-display.tsx";
import { formatPoNumber, toMoneyVal } from "../../utils.ts";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { SearchField } from "../../../../components/primitives/search-field.tsx";
import { Select, SelectItem } from "../../../../components/primitives/select.tsx";
import type { PurchaseOrderListItem } from "../../types.ts";

export interface OrderListTabProps {
  readonly orders: PurchaseOrderListItem[];
  readonly isLoading: boolean;
  readonly totalCount: number;
  readonly page: number;
  readonly onPageChange: (page: number) => void;
  readonly onSelectOrder: (id: string) => void;
  readonly onNewOrder: () => void;
  readonly stateFilter: string | null;
  readonly onStateFilterChange: (state: string | null) => void;
  readonly hasPriceReadPermission?: boolean | undefined;
}

export function OrderListTab({
  orders,
  isLoading,
  totalCount,
  page,
  onPageChange,
  onSelectOrder,
  onNewOrder,
  stateFilter,
  onStateFilterChange,
  hasPriceReadPermission = true
}: OrderListTabProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const [searchQuery, setSearchQuery] = useState("");

  const filteredOrders = orders.filter((order) => {
    if (!searchQuery.trim()) return true;
    const q = searchQuery.toLowerCase().trim();
    const poNum = (order.poNumber || formatPoNumber(order.id)).toLowerCase();
    return poNum.includes(q) || order.supplierPartyId.toLowerCase().includes(q);
  });

  const stateOptions = [
    { value: "", label: t("orders.filterAllStates") },
    { value: "Draft", label: t("status.Draft") },
    { value: "PendingApproval", label: t("status.PendingApproval") },
    { value: "Approved", label: t("status.Approved") },
    { value: "Open", label: t("status.Open") },
    { value: "PartiallyCompleted", label: t("status.PartiallyCompleted") },
    { value: "Completed", label: t("status.Completed") },
    { value: "Cancelled", label: t("status.Cancelled") },
    { value: "Closed", label: t("status.Closed") }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Filter & Action Bar */}
      <div
        style={{
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 0.75rem)",
          padding: "1rem 1.25rem",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem"
        }}
      >
        <div style={{ display: "flex", gap: "0.75rem", alignItems: "center", flexWrap: "wrap", flex: 1 }}>
          <div style={{ minWidth: "15rem", maxWidth: "22rem", flex: 1 }}>
            <SearchField
              value={searchQuery}
              onChange={setSearchQuery}
              placeholder={t("orders.searchPlaceholder")}
              aria-label={t("orders.searchPlaceholder")}
            />
          </div>

          <div style={{ minWidth: "11rem" }}>
            <Select
              aria-label={t("orders.filterByState")}
              selectedKey={stateFilter ?? ""}
              onSelectionChange={(key) => onStateFilterChange(key ? String(key) : null)}
            >
              {stateOptions.map((item) => (
                <SelectItem key={item.value} id={item.value}>{item.label}</SelectItem>
              ))}
            </Select>
          </div>
        </div>

        <Button variant="primary" onPress={onNewOrder} type="button">
          + {t("orders.newOrder")}
        </Button>
      </div>

      {/* Orders Table Container */}
      <PurchasingDataTable minWidth="100%">
        <Table aria-label={t("tabs.orders")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("orders.number")}</TableHead>
              <TableHead>{t("orders.supplier")}</TableHead>
              <TableHead>{t("orders.orderDate")}</TableHead>
              {hasPriceReadPermission && <TableHead>{t("orders.netTotal")}</TableHead>}
              <TableHead>{t("orders.commercialState")}</TableHead>
              <TableHead>{t("orders.receiptState")}</TableHead>
              <TableHead>{t("orders.invoicingState")}</TableHead>
              <TableHead>{t("orders.matchingState")}</TableHead>
              <TableHead>{t("common.actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {filteredOrders.map((order) => (
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
                  <PurchasingStatusBadge state={order.matchingState || "NotRequired"} kind="matching" />
                </TableCell>
                <TableCell>
                  <Button
                    variant="secondary"
                    onPress={() => onSelectOrder(order.id)}
                    type="button"
                  >
                    {t("common.manage")}
                  </Button>
                </TableCell>
              </TableRow>
            ))}

            {filteredOrders.length === 0 && !isLoading && (
              <TableRow>
                <TableCell colSpan={hasPriceReadPermission ? 9 : 8} style={{ textAlign: "center", padding: "3rem" }}>
                  <p style={{ margin: "0 0 1rem", fontSize: "0.9375rem", color: "var(--color-content-muted)" }}>
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
      </PurchasingDataTable>
    </div>
  );
}
