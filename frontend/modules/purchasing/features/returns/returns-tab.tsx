import React from "react";
import { useTranslation } from "react-i18next";
import { PurchasingDataTable } from "../../components/purchasing-data-table.tsx";
import { PurchasingStatusBadge } from "../../components/purchasing-status-badge.tsx";
import { PurchasingSupplierDisplay } from "../../components/purchasing-supplier-display.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import type { PurchaseOrderListItem } from "../../types.ts";

function formatReturnNumber(id: string): string {
  const numPart = id.split("-")[1] ?? "001";
  return ["RET", "2026", numPart].join("-");
}

export interface ReturnsTabProps {
  readonly orders: PurchaseOrderListItem[];
  readonly isLoading: boolean;
  readonly onOpenNewReturn: (order: PurchaseOrderListItem) => void;
}

export function ReturnsTab({
  orders,
  isLoading,
  onOpenNewReturn
}: ReturnsTabProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");

  const sampleReturns = [
    {
      id: "ret-001",
      orderNumber: "PO-2026-0003",
      supplierPartyId: "party-supplier-delta",
      resolution: "PhysicalReturn",
      reason: "تلف جزئي في كراتين الشحن أثناء النقل",
      state: "Executed",
      inventoryOperation: "INV-RET-2026-0009",
      creditNote: null
    },
    {
      id: "ret-002",
      orderNumber: "PO-2026-0004",
      supplierPartyId: "party-supplier-gulf-service",
      resolution: "ServiceCredit",
      reason: "خصم تسوية لعدم إنجاز ساعات الدعم بالكامل",
      state: "Executed",
      inventoryOperation: null,
      creditNote: "CR-NOTE-2026-0004"
    }
  ];

  const getResolutionLabel = (resolution: string) => {
    switch (resolution) {
      case "PhysicalReturn":
        return t("returns.PhysicalReturn");
      case "SupplierCredit":
        return t("returns.SupplierCredit");
      case "ServiceCredit":
        return t("returns.ServiceCredit");
      default:
        return resolution;
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("returns.title")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("returns.subtitle")}
          </p>
        </div>
      </div>

      <PurchasingDataTable minWidth="56rem">
        <Table aria-label={t("returns.title")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("returns.returnId")}</TableHead>
              <TableHead>{t("orders.number")}</TableHead>
              <TableHead>{t("orders.supplier")}</TableHead>
              <TableHead>{t("returns.resolution")}</TableHead>
              <TableHead>{t("returns.reason")}</TableHead>
              <TableHead>{t("returns.status")}</TableHead>
              <TableHead>{t("returns.downstreamLink")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sampleReturns.map((ret) => (
              <TableRow key={ret.id}>
                <TableCell>
                  <span style={{ fontFamily: "monospace", fontWeight: 600, color: "var(--color-brand-primary)" }}>
                    {formatReturnNumber(ret.id)}
                  </span>
                </TableCell>
                <TableCell>
                  <span style={{ fontFamily: "monospace" }}>{ret.orderNumber}</span>
                </TableCell>
                <TableCell>
                  <PurchasingSupplierDisplay supplierPartyId={ret.supplierPartyId} />
                </TableCell>
                <TableCell>
                  <span style={{ fontWeight: 500 }}>{getResolutionLabel(ret.resolution)}</span>
                </TableCell>
                <TableCell>{ret.reason}</TableCell>
                <TableCell>
                  <PurchasingStatusBadge state={ret.state} kind="return" />
                </TableCell>
                <TableCell>
                  {ret.inventoryOperation && (
                    <span style={{ fontFamily: "monospace", fontSize: "0.8125rem", backgroundColor: "var(--color-surface-subtle)", padding: "0.125rem 0.375rem", borderRadius: "0.25rem" }}>
                      {ret.inventoryOperation}
                    </span>
                  )}
                  {ret.creditNote && (
                    <span style={{ fontFamily: "monospace", fontSize: "0.8125rem", backgroundColor: "var(--color-brand-primary-light)", color: "var(--color-brand-primary)", padding: "0.125rem 0.375rem", borderRadius: "0.25rem" }}>
                      {ret.creditNote}
                    </span>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </PurchasingDataTable>
    </div>
  );
}
