import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { PurchasingDataTable } from "../../components/purchasing-data-table.tsx";
import { PurchasingStatusBadge } from "../../components/purchasing-status-badge.tsx";
import { PurchasingSupplierDisplay } from "../../components/purchasing-supplier-display.tsx";
import { formatPoNumber } from "../../utils.ts";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Select, SelectItem } from "../../../../components/primitives/select.tsx";
import type { PurchaseOrderListItem } from "../../types.ts";

export interface MatchingTabProps {
  readonly orders: PurchaseOrderListItem[];
  readonly isLoading: boolean;
  readonly onOpenMatch: (order: PurchaseOrderListItem) => void;
  readonly onSelectOrder: (id: string) => void;
}

export function MatchingTab({
  orders,
  isLoading,
  onOpenMatch,
  onSelectOrder
}: MatchingTabProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const [filterState, setFilterState] = useState<string>("");

  const eligibleOrders = orders.filter((o) => o.state !== "Draft" && o.state !== "Cancelled");
  const filteredOrders = filterState
    ? eligibleOrders.filter((o) => (o.matchingState || "NotRequired") === filterState)
    : eligibleOrders;

  const matchOptions = [
    { value: "", label: t("matching.filterAll") },
    { value: "Matched", label: t("status.Matched") },
    { value: "WithinTolerance", label: t("status.WithinTolerance") },
    { value: "Variance", label: t("status.Variance") },
    { value: "Blocked", label: t("status.Blocked") },
    { value: "ApprovedException", label: t("status.ApprovedException") }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("matching.title")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("matching.subtitle")}
          </p>
        </div>

        <div style={{ minWidth: "12rem" }}>
          <Select
            aria-label={t("matching.filterByState")}
            selectedKey={filterState}
            onSelectionChange={(key) => setFilterState(String(key ?? ""))}
          >
            {matchOptions.map((item) => (
              <SelectItem key={item.value} id={item.value}>{item.label}</SelectItem>
            ))}
          </Select>
        </div>
      </div>

      <PurchasingDataTable minWidth="56rem">
        <Table aria-label={t("matching.title")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("orders.number")}</TableHead>
              <TableHead>{t("orders.supplier")}</TableHead>
              <TableHead>{t("orders.orderDate")}</TableHead>
              <TableHead>{t("orders.matchingState")}</TableHead>
              <TableHead>{t("orders.invoicingState")}</TableHead>
              <TableHead>{t("orders.receiptState")}</TableHead>
              <TableHead style={{ minWidth: "12rem", textAlign: "center" }}>{t("common.actions")}</TableHead>
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
                <TableCell>
                  <PurchasingStatusBadge state={order.matchingState || "WithinTolerance"} kind="matching" />
                </TableCell>
                <TableCell>
                  <PurchasingStatusBadge state={order.invoicingState || "NotInvoiced"} kind="invoicing" />
                </TableCell>
                <TableCell>
                  <PurchasingStatusBadge state={order.receiptState || "NotReceived"} kind="receipt" />
                </TableCell>
                <TableCell style={{ minWidth: "12rem", whiteSpace: "nowrap" }}>
                  <div style={{ display: "flex", gap: "0.5rem", justifyContent: "center", whiteSpace: "nowrap" }}>
                    <Button
                      variant="primary"
                      onPress={() => onOpenMatch(order)}
                      type="button"
                    >
                      {t("matching.evaluate")}
                    </Button>
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

            {filteredOrders.length === 0 && !isLoading && (
              <TableRow>
                <TableCell colSpan={7} style={{ textAlign: "center", padding: "3rem" }}>
                  <p style={{ margin: 0, color: "var(--color-content-muted)" }}>
                    {t("matching.noMatches")}
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
