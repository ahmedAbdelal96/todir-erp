import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import { ReturnsTab } from "../features/returns/returns-tab.tsx";
import { AuthoritativeReturnModal } from "../features/orders/authoritative-action-modals.tsx";
import { usePurchaseOrders } from "../hooks/use-purchasing-queries.ts";

export function PurchasingReturnsPage(): React.JSX.Element {
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

  const [returnOrderId, setReturnOrderId] = useState<string | null>(null);

  const { data: ordersPage, isLoading } = usePurchaseOrders(legalEntityId, {
    page: 1,
    pageSize: 50
  });

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
      data-testid="purchasing-returns-page"
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.5rem",
        padding: "1.5rem 2rem",
        backgroundColor: "var(--color-surface-page, #f0f4f8)",
        minHeight: "100vh"
      }}
    >
      <ReturnsTab
        orders={orders}
        isLoading={isLoading}
        onOpenNewReturn={(orderItem) => setReturnOrderId(orderItem.id)}
      />

      <AuthoritativeReturnModal
        isOpen={Boolean(returnOrderId)}
        onClose={() => setReturnOrderId(null)}
        legalEntityId={legalEntityId}
        orderId={returnOrderId}
      />
    </div>
  );
}
