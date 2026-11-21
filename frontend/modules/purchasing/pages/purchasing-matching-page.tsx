import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import { MatchingTab } from "../features/matching/matching-tab.tsx";
import { PurchaseOrderDetailModal } from "../features/orders/purchase-order-detail-modal.tsx";
import { AuthoritativeMatchModal } from "../features/orders/authoritative-action-modals.tsx";
import { usePurchaseOrders, usePurchaseOrderDetail } from "../hooks/use-purchasing-queries.ts";

export function PurchasingMatchingPage(): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();

  let contextEntityId: string | undefined;
  try {
    const context = useLegalEntityContext();
    contextEntityId = context?.legalEntityId;
  } catch {
    // Outside RuntimeProvider
  }
  const legalEntityId = outletCtx?.legalEntityId ?? contextEntityId ?? "";

  const permissions = useOptionalPermissions();
  const hasPriceReadPermission = permissions ? permissions.hasPermission("purchasing.price.read") : true;
  const hasMatchOverridePermission = permissions ? permissions.hasPermission("purchasing.match.override") : true;

  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [matchOrderId, setMatchOrderId] = useState<string | null>(null);

  const { data: ordersPage, isLoading } = usePurchaseOrders(legalEntityId, {
    page: 1,
    pageSize: 50
  });

  const { data: selectedOrderDetail } = usePurchaseOrderDetail(legalEntityId, selectedOrderId ?? "");
  const orders = ordersPage?.items ?? [];

  if (!legalEntityId) {
    return (
      <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
        {t("common.loading", "جاري التحميل...")}
      </div>
    );
  }

  return (
    <div
      data-testid="purchasing-matching-page"
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.5rem",
        padding: "1.5rem 2rem",
        backgroundColor: "var(--color-surface-page, #f0f4f8)",
        minHeight: "100vh"
      }}
    >
      <MatchingTab
        orders={orders}
        isLoading={isLoading}
        onOpenMatch={(orderItem) => setMatchOrderId(orderItem.id)}
        onSelectOrder={(id) => setSelectedOrderId(id)}
      />

      {selectedOrderDetail && (
        <PurchaseOrderDetailModal
          isOpen={Boolean(selectedOrderId)}
          onClose={() => setSelectedOrderId(null)}
          order={selectedOrderDetail}
          hasPriceReadPermission={hasPriceReadPermission}
          onOpenMatch={(ord) => setMatchOrderId(ord.id)}
        />
      )}

      <AuthoritativeMatchModal
        isOpen={Boolean(matchOrderId)}
        onClose={() => setMatchOrderId(null)}
        legalEntityId={legalEntityId}
        orderId={matchOrderId}
        hasMatchOverridePermission={hasMatchOverridePermission}
      />
    </div>
  );
}
