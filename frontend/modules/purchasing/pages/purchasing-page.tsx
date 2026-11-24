import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { PurchasingTabNav, type PurchasingTabDef, type PurchasingTabId } from "../components/purchasing-tab-nav.tsx";
import { PurchasingOverviewTab } from "../features/overview/purchasing-overview-tab.tsx";
import { OrderListTab } from "../features/orders/order-list-tab.tsx";
import { NewPurchaseOrderModal } from "../features/orders/new-purchase-order-modal.tsx";
import { PurchaseOrderDetailModal } from "../features/orders/purchase-order-detail-modal.tsx";
import { ReceiptsTab } from "../features/receipts/receipts-tab.tsx";
import { MatchingTab } from "../features/matching/matching-tab.tsx";
import { ReturnsTab } from "../features/returns/returns-tab.tsx";
import { SupplierProfileTab } from "../features/suppliers/supplier-profile-tab.tsx";
import { PurchaseHistoryTab } from "../features/history/purchase-history-tab.tsx";
import { PurchaseSettingsTab } from "../features/settings/purchase-settings-tab.tsx";
import {
  AuthoritativeReceiveModal,
  AuthoritativeReceiveAndBillModal,
  AuthoritativeMatchModal,
  AuthoritativeReturnModal
} from "../features/orders/authoritative-action-modals.tsx";
import { usePurchaseOrders, usePurchaseOrderDetail } from "../hooks/use-purchasing-queries.ts";
import { useLegalEntityContext, useOptionalPermissions, useOptionalCapabilities } from "../../../core/runtime/runtime-provider.tsx";
import { useOutletContext, useSearchParams } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";

export function PurchasingPage(): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const [searchParams, setSearchParams] = useSearchParams();

  let contextEntityId: string | undefined;
  try {
    const context = useLegalEntityContext();
    contextEntityId = context?.legalEntityId;
  } catch {
    // Rendered outside RuntimeProvider
  }

  const legalEntityId = outletCtx?.legalEntityId ?? contextEntityId ?? "";

  // Tab State
  const tabParam = searchParams.get("tab") as PurchasingTabId | null;
  const activeTab: PurchasingTabId = tabParam && ["overview", "orders", "receipts", "matching", "returns", "suppliers", "history", "settings"].includes(tabParam)
    ? tabParam
    : "overview";
  const handleTabChange = (tab: PurchasingTabId) => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      next.set("tab", tab);
      return next;
    }, { replace: true });
  };

  // Capabilities & Permissions
  const inventoryParam = searchParams.get("inventory");
  const isInventoryExplicitlyOff = inventoryParam === "false" ||
    (typeof window !== "undefined" && window.sessionStorage?.getItem("erp.capability.inventory") === "false");

  const matchingParam = searchParams.get("matching");
  const isMatchingExplicitlyOff = matchingParam === "false" ||
    (typeof window !== "undefined" && window.sessionStorage?.getItem("erp.capability.purchasing.matching") === "false");

  const priceReadParam = searchParams.get("priceRead");
  const isPriceReadForbidden = priceReadParam === "false" ||
    (typeof window !== "undefined" && window.sessionStorage?.getItem("erp.permission.purchasing.price.read") === "false");

  const capabilities = useOptionalCapabilities();
  const permissions = useOptionalPermissions();

  const isInventoryCapabilityActive = capabilities
    ? capabilities.isActivated("inventory.receipts") && !isInventoryExplicitlyOff
    : false;

  const isMatchingCapabilityActive = capabilities
    ? capabilities.isActivated("purchasing.matching") && !isMatchingExplicitlyOff
    : false;

  const isReturnsCapabilityActive = capabilities
    ? capabilities.isActivated("purchasing.returns")
    : false;

  const hasPriceReadPermission = permissions
    ? permissions.hasPermission("purchasing.price.read") && !isPriceReadForbidden
    : false;

  const hasPriceOverridePermission = permissions
    ? permissions.hasPermission("purchasing.price.override")
    : false;

  const hasMatchOverridePermission = permissions
    ? permissions.hasPermission("purchasing.match.override")
    : false;

  // Pagination & Filters
  const [page, setPage] = useState(1);
  const [orderSearch, setOrderSearch] = useState("");
  const [stateFilter, setStateFilter] = useState<string | null>(null);

  // Modals State
  const [isNewOrderOpen, setIsNewOrderOpen] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [receiveOrderId, setReceiveOrderId] = useState<string | null>(null);
  const [receiveAndBillOrderId, setReceiveAndBillOrderId] = useState<string | null>(null);
  const [matchOrderId, setMatchOrderId] = useState<string | null>(null);
  const [returnOrderId, setReturnOrderId] = useState<string | null>(null);
  const [createdInvoiceRef, setCreatedInvoiceRef] = useState<string | null>(null);

  const { data: ordersPage, isLoading } = usePurchaseOrders(legalEntityId, {
    page,
    pageSize: 50,
    state: stateFilter,
    search: orderSearch || null
  });

  const { data: selectedOrderDetail } = usePurchaseOrderDetail(legalEntityId, selectedOrderId ?? "");

  const orders = ordersPage?.items ?? [];
  const totalOrdersCount = ordersPage?.totalCount ?? orders.length;

  // Build Tab List
  const tabs: readonly PurchasingTabDef[] = [
    { id: "overview", label: t("tabs.overview") },
    { id: "orders", label: t("tabs.orders"), badgeCount: orders.length },
    ...(isInventoryCapabilityActive ? [{ id: "receipts", label: t("tabs.receipts"), isAdvanced: true } as const] : []),
    ...(isMatchingCapabilityActive ? [{ id: "matching", label: t("tabs.matching"), isAdvanced: true } as const] : []),
    ...(isReturnsCapabilityActive ? [{ id: "returns", label: t("tabs.returns"), isAdvanced: true } as const] : []),
    { id: "suppliers", label: t("tabs.suppliers"), isAdvanced: true },
    { id: "history", label: t("tabs.history") },
    { id: "settings", label: t("tabs.settings") }
  ];

  const effectiveActiveTab = (!isInventoryCapabilityActive && activeTab === "receipts") ||
    (!isMatchingCapabilityActive && activeTab === "matching") ||
    (!isReturnsCapabilityActive && activeTab === "returns")
      ? "overview"
      : activeTab;

  if (!legalEntityId) {
    return (
      <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
        {t("common.loading", "جاري التحميل...")}
      </div>
    );
  }

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.5rem",
        padding: "1.5rem 2rem",
        backgroundColor: "var(--color-surface-page, #f0f4f8)",
        minHeight: "100vh"
      }}
    >
      {/* Header Banner */}
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

      {/* Tab Navigation */}
      <PurchasingTabNav
        activeTab={effectiveActiveTab}
        onTabChange={handleTabChange}
        tabs={tabs}
      />

      {/* Tab Content Panels */}
      <div>
        {effectiveActiveTab === "overview" && (
          <PurchasingOverviewTab
            orders={orders}
            isLoading={isLoading}
            onSelectOrder={(id) => setSelectedOrderId(id)}
            onNewOrder={() => setIsNewOrderOpen(true)}
            hasPriceReadPermission={hasPriceReadPermission}
            isAdvanced={isInventoryCapabilityActive}
          />
        )}

        {effectiveActiveTab === "orders" && (
          <OrderListTab
            orders={orders}
            isLoading={isLoading}
            totalCount={totalOrdersCount}
            page={page}
            onPageChange={setPage}
            onSelectOrder={(id) => setSelectedOrderId(id)}
            onNewOrder={() => setIsNewOrderOpen(true)}
            stateFilter={stateFilter}
            onStateFilterChange={(value) => { setStateFilter(value); setPage(1); }}
            searchQuery={orderSearch}
            onSearchQueryChange={(value) => { setOrderSearch(value); setPage(1); }}
            hasPriceReadPermission={hasPriceReadPermission}
          />
        )}

        {effectiveActiveTab === "receipts" && isInventoryCapabilityActive && (
          <ReceiptsTab
            orders={orders}
            isLoading={isLoading}
            onSelectOrder={(id) => setSelectedOrderId(id)}
            onOpenReceive={(orderItem) => setReceiveOrderId(orderItem.id)}
            onOpenReceiveAndBill={(orderItem) => setReceiveAndBillOrderId(orderItem.id)}
          />
        )}

        {effectiveActiveTab === "matching" && isMatchingCapabilityActive && (
          <MatchingTab
            orders={orders}
            isLoading={isLoading}
            onOpenMatch={(orderItem) => setMatchOrderId(orderItem.id)}
            onSelectOrder={(id) => setSelectedOrderId(id)}
          />
        )}

        {effectiveActiveTab === "returns" && isReturnsCapabilityActive && (
          <ReturnsTab
            orders={orders}
            isLoading={isLoading}
            onOpenNewReturn={(orderItem) => setReturnOrderId(orderItem.id)}
          />
        )}

        {effectiveActiveTab === "suppliers" && (
          <SupplierProfileTab legalEntityId={legalEntityId} />
        )}

        {effectiveActiveTab === "history" && (
          <PurchaseHistoryTab legalEntityId={legalEntityId} />
        )}

        {effectiveActiveTab === "settings" && (
          <PurchaseSettingsTab
            legalEntityId={legalEntityId}
            isAdvanced={isInventoryCapabilityActive || isMatchingCapabilityActive}
          />
        )}
      </div>

      {/* Modals */}
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
