import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { CreateAdjustmentModal } from "./create-adjustment-modal.tsx";
import { useInventoryPolicy, useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import { toQty } from "../../utils.ts";
import type { AdjustmentDirection, AdjustmentReason, Warehouse } from "../../types.ts";

export interface AdjustmentItemView {
  readonly id: string;
  readonly productVariantId: string;
  readonly warehouseName: string;
  readonly quantity: number;
  readonly direction: AdjustmentDirection;
  readonly reason: AdjustmentReason;
  readonly status: string;
  readonly creator?: string;
  readonly approver?: string | null;
}

export interface AdjustmentsTabProps {
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
  readonly canApprove?: boolean;
  readonly canPost?: boolean;
}

export function AdjustmentsTab({
  legalEntityId,
  warehouses,
  canApprove = true,
  canPost = true
}: AdjustmentsTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  const { data: policy } = useInventoryPolicy(legalEntityId);
  const isApprovalRequired = policy?.adjustmentApprovalMode === "ApprovalRequired";

  const [isNewAdjustmentOpen, setIsNewAdjustmentOpen] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Seeded / active adjustments representing the full approval lifecycle
  const [adjustmentsList, setAdjustmentsList] = useState<readonly AdjustmentItemView[]>([
    {
      id: "ADJ-2026-004",
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      warehouseName: "المستودع الرئيسي - الرياض (WH-MAIN)",
      quantity: 5,
      direction: "Increase",
      reason: "FoundStock",
      status: "Posted",
      creator: "أحمد المحاسب",
      approver: "مدير المستودع"
    },
    {
      id: "ADJ-2026-003",
      productVariantId: "SKU-ELEC-2002",
      warehouseName: "مستودع التوزيع - جدة (WH-JED)",
      quantity: 2,
      direction: "Decrease",
      reason: "Damage",
      status: "Approved",
      creator: "أحمد المحاسب",
      approver: "مدير المستودع"
    },
    {
      id: "ADJ-2026-002",
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      warehouseName: "المستودع الرئيسي - الرياض (WH-MAIN)",
      quantity: 1,
      direction: "Decrease",
      reason: "CountDifference",
      status: "PendingApproval",
      creator: "أحمد المحاسب",
      approver: "—"
    },
    {
      id: "ADJ-2026-001",
      productVariantId: "SKU-OFFICE-1001",
      warehouseName: "المستودع الرئيسي - الرياض (WH-MAIN)",
      quantity: 3,
      direction: "Increase",
      reason: "DataCorrection",
      status: "Draft",
      creator: "أحمد المحاسب",
      approver: "—"
    }
  ]);

  const { approveAdjustmentRequest, postAdjustmentRequest } = useInventoryMutations(legalEntityId);

  const handleSubmitForApproval = (id: string) => {
    setAdjustmentsList(prev => prev.map(a => a.id === id ? { ...a, status: "PendingApproval" } : a));
  };

  const handleApprove = async (id: string) => {
    setErrorMessage(null);
    try {
      await approveAdjustmentRequest.mutateAsync(id);
      setAdjustmentsList(prev => prev.map(a => a.id === id ? { ...a, status: "Approved", approver: "مدير المستودع" } : a));
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  const handlePost = async (id: string) => {
    setErrorMessage(null);
    try {
      await postAdjustmentRequest.mutateAsync(id);
      setAdjustmentsList(prev => prev.map(a => a.id === id ? { ...a, status: "Posted" } : a));
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  const handleCreated = (item: {
    id: string;
    productVariantId: string;
    warehouseName: string;
    quantity: number;
    direction: AdjustmentDirection;
    reason: AdjustmentReason;
    status: string;
  }) => {
    setAdjustmentsList(prev => [
      {
        ...item,
        creator: "أحمد المحاسب",
        approver: item.status === "Posted" ? "مدير المستودع" : "—"
      },
      ...prev
    ]);
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {errorMessage && (
        <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
          {errorMessage}
        </div>
      )}

      {/* Policy banner */}
      <div
        style={{
          padding: "1rem",
          backgroundColor: isApprovalRequired ? "#fffbeb" : "var(--color-surface-subtle, #f8fafc)",
          border: `1px solid ${isApprovalRequired ? "#fde68a" : "var(--color-border-default)"}`,
          borderRadius: "var(--radius-md)",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "0.75rem"
        }}
      >
        <div>
          <span style={{ fontWeight: 600, fontSize: "0.875rem", color: "var(--color-brand-primary)" }}>
            {t("adjustments.approvalMode")}:{" "}
          </span>
          <span style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {isApprovalRequired
              ? t("adjustments.approvalModes.ApprovalRequired")
              : t("adjustments.approvalModes.DirectConfirm")}
          </span>
          {policy?.operationalCutoffDate && (
            <span style={{ display: "block", fontSize: "0.75rem", color: "var(--color-content-subtle)", marginTop: "0.25rem" }}>
              {t("errors.beforeOperationalCutoff")}: {policy.operationalCutoffDate}
            </span>
          )}
        </div>
      </div>

      {/* Header & New Adjustment */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          padding: "1rem",
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-md)"
        }}
      >
        <div>
          <h3 style={{ margin: "0 0 0.25rem", fontSize: "1.125rem", color: "var(--color-brand-primary)" }}>
            {t("adjustments.title")}
          </h3>
        </div>
        <Button variant="primary" onPress={() => setIsNewAdjustmentOpen(true)}>
          + {t("adjustments.newAdjustment")}
        </Button>
      </div>

      {/* Adjustments Table */}
      {adjustmentsList.length === 0 ? (
        <EmptyState title={t("adjustments.title")} description={t("common.empty")} />
      ) : (
        <InventoryDataTable minWidth="100%" ariaLabel={t("adjustments.title")}>
          <thead>
            <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
              <th style={{ padding: "0.625rem 0.5rem", textAlign: "start", fontSize: "0.8125rem", whiteSpace: "nowrap" }}>{t("adjustments.adjustmentNumber")}</th>
              <th style={{ padding: "0.625rem 0.5rem", textAlign: "start", fontSize: "0.8125rem" }}>{t("adjustments.product")}</th>
              <th style={{ padding: "0.625rem 0.5rem", textAlign: "start", fontSize: "0.8125rem" }}>{t("adjustments.warehouse")}</th>
              <th style={{ padding: "0.625rem 0.5rem", textAlign: "start", fontSize: "0.8125rem", whiteSpace: "nowrap" }}>{t("adjustments.direction")}</th>
              <th style={{ padding: "0.625rem 0.5rem", textAlign: "start", fontSize: "0.8125rem", whiteSpace: "nowrap" }}>{t("adjustments.reason")}</th>
              <th style={{ padding: "0.625rem 0.5rem", textAlign: "end", fontSize: "0.8125rem", whiteSpace: "nowrap" }}>{t("adjustments.quantity")}</th>
              <th style={{ padding: "0.625rem 0.5rem", textAlign: "start", fontSize: "0.8125rem", whiteSpace: "nowrap" }}>{t("adjustments.creator")}</th>
              <th style={{ padding: "0.625rem 0.5rem", textAlign: "start", fontSize: "0.8125rem", whiteSpace: "nowrap" }}>{t("adjustments.approver")}</th>
              <th style={{ padding: "0.625rem 0.5rem", textAlign: "start", fontSize: "0.8125rem" }}>{t("adjustments.stockEffect")}</th>
              <th style={{ padding: "0.625rem 0.5rem", textAlign: "center", fontSize: "0.8125rem", whiteSpace: "nowrap" }}>{t("adjustments.status")}</th>
              <th style={{ padding: "0.625rem 0.5rem", textAlign: "center", fontSize: "0.8125rem", whiteSpace: "nowrap" }}>{t("stock.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {adjustmentsList.map((adj) => (
              <tr key={adj.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                <td style={{ padding: "0.625rem 0.5rem", fontWeight: 700, color: "var(--color-brand-primary)", whiteSpace: "nowrap" }}>
                  {adj.id}
                </td>
                <td style={{ padding: "0.625rem 0.5rem" }}>
                  <InventoryProductDisplay productVariantId={adj.productVariantId} />
                </td>
                <td style={{ padding: "0.625rem 0.5rem", fontSize: "0.8125rem" }}>{adj.warehouseName}</td>
                <td style={{ padding: "0.625rem 0.5rem", whiteSpace: "nowrap" }}>
                  <span style={{
                    padding: "0.25rem 0.5rem",
                    borderRadius: "var(--radius-sm)",
                    fontSize: "0.75rem",
                    fontWeight: 600,
                    backgroundColor: adj.direction === "Increase" ? "#dcfce7" : "#fee2e2",
                    color: adj.direction === "Increase" ? "#15803d" : "#b91c1c"
                  }}>
                    {t(`adjustments.directions.${adj.direction}` as any)}
                  </span>
                </td>
                <td style={{ padding: "0.625rem 0.5rem", fontSize: "0.8125rem", whiteSpace: "nowrap" }}>
                  {t(`adjustments.reasons.${adj.reason}` as any)}
                </td>
                <td style={{ padding: "0.625rem 0.5rem", textAlign: "end", fontWeight: 700, whiteSpace: "nowrap" }}>
                  <QuantityDisplay value={toQty(adj.quantity)} />
                </td>
                <td style={{ padding: "0.625rem 0.5rem", fontSize: "0.8125rem", color: "var(--color-content-muted)", whiteSpace: "nowrap" }}>
                  {adj.creator ?? "—"}
                </td>
                <td style={{ padding: "0.625rem 0.5rem", fontSize: "0.8125rem", color: "var(--color-content-muted)", whiteSpace: "nowrap" }}>
                  {adj.approver ?? "—"}
                </td>
                <td style={{ padding: "0.625rem 0.5rem", fontSize: "0.8125rem" }}>
                  {adj.status === "Posted" ? (
                    <span style={{ color: "var(--color-status-success)", fontWeight: 600 }}>
                      <QuantityDisplay value={toQty(adj.direction === "Increase" ? adj.quantity : -adj.quantity)} /> {t("adjustments.stockEffectPosted")}
                    </span>
                  ) : (
                    <span style={{ color: "var(--color-content-muted)", fontStyle: "italic" }}>
                      {t("adjustments.stockEffectPending")}
                    </span>
                  )}
                </td>
                <td style={{ padding: "0.625rem 0.5rem", textAlign: "center", whiteSpace: "nowrap" }}>
                  <InventoryStatusBadge status={adj.status} type="adjustment" />
                </td>
                <td style={{ padding: "0.625rem 0.5rem", textAlign: "center", whiteSpace: "nowrap" }}>
                  <div style={{ display: "flex", gap: "0.375rem", justifyContent: "center" }}>
                    {adj.status === "Draft" && (
                      <Button variant="secondary" onPress={() => handleSubmitForApproval(adj.id)}>
                        {t("adjustments.submitForApproval")}
                      </Button>
                    )}
                    {adj.status === "PendingApproval" && canApprove && (
                      <Button variant="primary" onPress={() => handleApprove(adj.id)}>
                        {t("adjustments.approve")}
                      </Button>
                    )}
                    {adj.status === "Approved" && canPost && (
                      <Button variant="primary" onPress={() => handlePost(adj.id)}>
                        {t("adjustments.post")}
                      </Button>
                    )}
                    {adj.status === "Posted" && (
                      <span style={{ color: "var(--color-status-success)", fontWeight: 600, fontSize: "0.8125rem" }}>
                        ✓ {t("adjustments.completed")}
                      </span>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </InventoryDataTable>
      )}

      {isNewAdjustmentOpen && (
        <CreateAdjustmentModal
          isOpen={isNewAdjustmentOpen}
          onClose={() => setIsNewAdjustmentOpen(false)}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
          isApprovalRequired={isApprovalRequired}
          onAdjustmentCreated={handleCreated}
        />
      )}
    </div>
  );
}
