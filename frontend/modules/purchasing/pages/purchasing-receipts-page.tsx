import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import { ReceiptsTab } from "../features/receipts/receipts-tab.tsx";
import { PurchaseOrderDetailModal } from "../features/orders/purchase-order-detail-modal.tsx";
import {
  AuthoritativeReceiveModal,
  AuthoritativeReceiveAndBillModal
} from "../features/orders/authoritative-action-modals.tsx";
import { usePurchaseOrders, usePurchaseOrderDetail } from "../hooks/use-purchasing-queries.ts";

export function PurchasingReceiptsPage(): React.JSX.Element {
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
  const hasPriceReadPermission = Boolean(permissions?.hasPermission("purchasing.price.read"));

  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [receiveOrderId, setReceiveOrderId] = useState<string | null>(null);
  const [receiveAndBillOrderId, setReceiveAndBillOrderId] = useState<string | null>(null);
  const [createdInvoiceRef, setCreatedInvoiceRef] = useState<string | null>(null);

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
      data-testid="purchasing-receipts-page"
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.5rem",
        padding: "1.5rem 2rem",
        backgroundColor: "var(--color-surface-page, #f0f4f8)",
        minHeight: "100vh"
      }}
    >
      <ReceiptsTab
        orders={orders}
        isLoading={isLoading}
        onSelectOrder={(id) => setSelectedOrderId(id)}
        onOpenReceive={(orderItem) => setReceiveOrderId(orderItem.id)}
        onOpenReceiveAndBill={(orderItem) => setReceiveAndBillOrderId(orderItem.id)}
      />

      {selectedOrderDetail && (
        <PurchaseOrderDetailModal
          isOpen={Boolean(selectedOrderId)}
          onClose={() => {
            setSelectedOrderId(null);
            setCreatedInvoiceRef(null);
          }}
          order={selectedOrderDetail}
          hasPriceReadPermission={hasPriceReadPermission}
          onOpenReceive={(ord) => setReceiveOrderId(ord.id)}
          onOpenReceiveAndBill={(ord) => setReceiveAndBillOrderId(ord.id)}
          createdInvoiceRef={createdInvoiceRef}
        />
      )}

      <AuthoritativeReceiveModal
        isOpen={Boolean(receiveOrderId)}
        onClose={() => setReceiveOrderId(null)}
        legalEntityId={legalEntityId}
        orderId={receiveOrderId}
      />

      <AuthoritativeReceiveAndBillModal
        isOpen={Boolean(receiveAndBillOrderId)}
        onClose={() => setReceiveAndBillOrderId(null)}
        legalEntityId={legalEntityId}
        orderId={receiveAndBillOrderId}
        onCompleted={(_rcvId, invId) => {
          setCreatedInvoiceRef(invId);
        }}
      />
    </div>
  );
}
