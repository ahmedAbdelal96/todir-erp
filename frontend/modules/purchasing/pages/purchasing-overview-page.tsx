import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useNavigate } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext, useOptionalPermissions, useOptionalCapabilities } from "../../../core/runtime/runtime-provider.tsx";
import { PurchasingOverviewTab } from "../features/overview/purchasing-overview-tab.tsx";
import { NewPurchaseOrderModal } from "../features/orders/new-purchase-order-modal.tsx";
import { PurchaseOrderDetailModal } from "../features/orders/purchase-order-detail-modal.tsx";
import {
  AuthoritativeReceiveModal,
  AuthoritativeReceiveAndBillModal,
  AuthoritativeMatchModal,
  AuthoritativeReturnModal
} from "../features/orders/authoritative-action-modals.tsx";
import { usePurchaseOrders, usePurchaseOrderDetail } from "../hooks/use-purchasing-queries.ts";

export function PurchasingOverviewPage(): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const navigate = useNavigate();

  let contextEntityId: string | undefined;
  try {
    const context = useLegalEntityContext();
    contextEntityId = context?.legalEntityId;
  } catch {
    // Outside RuntimeProvider
  }
  const legalEntityId = outletCtx?.legalEntityId ?? contextEntityId ?? "";

  const capabilities = useOptionalCapabilities();
  const permissions = useOptionalPermissions();

  const isInventoryCapabilityActive = Boolean(capabilities?.isActivated("inventory.receipts"));
  const isMatchingCapabilityActive = Boolean(capabilities?.isActivated("purchasing.matching"));
  const isReturnsCapabilityActive = Boolean(capabilities?.isActivated("purchasing.returns"));

  const hasPriceReadPermission = Boolean(permissions?.hasPermission("purchasing.price.read"));
  const hasPriceOverridePermission = Boolean(permissions?.hasPermission("purchasing.price.override"));
  const hasMatchOverridePermission = Boolean(permissions?.hasPermission("purchasing.match.override"));

  const [isNewOrderOpen, setIsNewOrderOpen] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [receiveOrderId, setReceiveOrderId] = useState<string | null>(null);
  const [receiveAndBillOrderId, setReceiveAndBillOrderId] = useState<string | null>(null);
  const [matchOrderId, setMatchOrderId] = useState<string | null>(null);
  const [returnOrderId, setReturnOrderId] = useState<string | null>(null);
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
      data-testid="purchasing-overview-page"
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.5rem",
        padding: "1.5rem 2rem",
        backgroundColor: "var(--color-surface-page, #f0f4f8)",
        minHeight: "100vh"
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h1 style={{ fontSize: "1.5rem", fontWeight: 700, margin: 0, color: "var(--color-content-primary, #0f172a)" }}>
            {t("overview.title")}
          </h1>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)" }}>
            {t("overview.subtitle")}
          </p>
        </div>
      </div>

      <PurchasingOverviewTab
        orders={orders}
        isLoading={isLoading}
        onSelectOrder={(id) => setSelectedOrderId(id)}
        onNewOrder={() => setIsNewOrderOpen(true)}
        hasPriceReadPermission={hasPriceReadPermission}
        isAdvanced={isInventoryCapabilityActive}
      />

      <NewPurchaseOrderModal
        isOpen={isNewOrderOpen}
        onClose={() => setIsNewOrderOpen(false)}
        legalEntityId={legalEntityId}
        onCreated={(id, invId) => {
          setSelectedOrderId(id);
          if (invId) setCreatedInvoiceRef(invId);
        }}
        hasPriceOverridePermission={hasPriceOverridePermission}
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
          hasPriceOverridePermission={hasPriceOverridePermission}
          isInventoryCapabilityActive={isInventoryCapabilityActive}
          isMatchingCapabilityActive={isMatchingCapabilityActive}
          isReturnsCapabilityActive={isReturnsCapabilityActive}
          onOpenReceive={(ord) => setReceiveOrderId(ord.id)}
          onOpenReceiveAndBill={(ord) => setReceiveAndBillOrderId(ord.id)}
          onOpenMatch={(ord) => setMatchOrderId(ord.id)}
          onOpenReturn={(ord) => setReturnOrderId(ord.id)}
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

      <AuthoritativeMatchModal
        isOpen={Boolean(matchOrderId)}
        onClose={() => setMatchOrderId(null)}
        legalEntityId={legalEntityId}
        orderId={matchOrderId}
        hasMatchOverridePermission={hasMatchOverridePermission}
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
