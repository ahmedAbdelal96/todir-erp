import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { PurchasingTabNav, type PurchasingTabDef, type PurchasingTabId } from "../components/purchasing-tab-nav.tsx";
import { PurchasingOverviewTab } from "../features/overview/purchasing-overview-tab.tsx";
import { OrderListTab } from "../features/orders/order-list-tab.tsx";
import { NewPurchaseOrderModal } from "../features/orders/new-purchase-order-modal.tsx";
import { PurchaseOrderDetailModal } from "../features/orders/purchase-order-detail-modal.tsx";
import { ReceiptsTab } from "../features/receipts/receipts-tab.tsx";
import { ReceiveOrderModal } from "../features/receipts/receive-order-modal.tsx";
import { ReceiveAndBillModal } from "../features/receipts/receive-and-bill-modal.tsx";
import { MatchingTab } from "../features/matching/matching-tab.tsx";
import { MatchOrderModal } from "../features/matching/match-order-modal.tsx";
import { ReturnsTab } from "../features/returns/returns-tab.tsx";
import { NewSupplierReturnModal } from "../features/returns/new-supplier-return-modal.tsx";
import { SupplierProfileTab } from "../features/suppliers/supplier-profile-tab.tsx";
import { PurchaseHistoryTab } from "../features/history/purchase-history-tab.tsx";
import { PurchaseSettingsTab } from "../features/settings/purchase-settings-tab.tsx";
import { usePurchaseOrders, usePurchaseOrderDetail } from "../hooks/use-purchasing-queries.ts";
import { useLegalEntityContext, useOptionalPermissions, useOptionalCapabilities } from "../../../core/runtime/runtime-provider.tsx";
import { useOutletContext, useSearchParams } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import type { PurchaseOrderListItem, PurchaseOrderView } from "../types.ts";

export function PurchasingPage(): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const [searchParams] = useSearchParams();

  const toOrderView = (item: PurchaseOrderListItem): PurchaseOrderView => ({
    ...item,
    legalEntityId: "00000000-0000-0000-0000-000000000001",
    version: item.version || 1,
    approvalState: item.approvalState ?? "Approved",
    receiptState: item.receiptState ?? "NotReceived",
    invoicingState: item.invoicingState ?? "NotInvoiced",
    matchingState: item.matchingState ?? "Matched",
    createdAt: item.updatedAt || new Date().toISOString(),
    lines: item.lines && item.lines.length > 0 ? item.lines : [
      {
        id: "line-default",
        sequence: 1,
        productVariantId: "00000000-0000-0000-0000-000000000001",
        description: t("orders.serviceItemDefault"),
        orderedQuantity: 10,
        receivedQuantity: item.receiptState === "Received" ? 10 : 0,
        invoicedQuantity: item.invoicingState === "Invoiced" ? 10 : 0,
        cancelledQuantity: 0,
        closedShortQuantity: 0,
        unitPrice: 100,
        unitCode: "PCS"
      }
    ]
  });

  let contextEntityId: string | undefined;
  try {
    const context = useLegalEntityContext();
    contextEntityId = context?.legalEntityId;
  } catch {
    // Rendered outside RuntimeProvider
  }

  const legalEntityId = outletCtx?.legalEntityId ?? contextEntityId ?? "00000000-0000-0000-0000-000000000001";

  // Tab State
  const tabParam = searchParams.get("tab") as PurchasingTabId | null;
  const [activeTab, setActiveTab] = useState<PurchasingTabId>(tabParam ?? "overview");

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
    : !isInventoryExplicitlyOff;

  const isMatchingCapabilityActive = capabilities
    ? capabilities.isActivated("purchasing.matching") && !isMatchingExplicitlyOff
    : !isMatchingExplicitlyOff;

  const isReturnsCapabilityActive = capabilities
    ? capabilities.isActivated("purchasing.returns")
    : true;

  const hasPriceReadPermission = permissions
    ? permissions.hasPermission("purchasing.price.read") && !isPriceReadForbidden
    : !isPriceReadForbidden;

  const hasPriceOverridePermission = permissions
    ? permissions.hasPermission("purchasing.price.override")
    : true;

  const hasMatchOverridePermission = permissions
    ? permissions.hasPermission("purchasing.match.override")
    : true;

  // Pagination & Filters
  const [page, setPage] = useState(1);
  const [stateFilter, setStateFilter] = useState<string | null>(null);

  // Modals State
  const [isNewOrderOpen, setIsNewOrderOpen] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [orderToReceive, setOrderToReceive] = useState<PurchaseOrderView | null>(null);
  const [orderToReceiveAndBill, setOrderToReceiveAndBill] = useState<PurchaseOrderView | null>(null);
  const [orderToMatch, setOrderToMatch] = useState<PurchaseOrderView | null>(null);
  const [orderToReturn, setOrderToReturn] = useState<PurchaseOrderView | null>(null);
  const [createdInvoiceRef, setCreatedInvoiceRef] = useState<string | null>(null);

  const { data: ordersPage, isLoading } = usePurchaseOrders(legalEntityId, {
    page,
    pageSize: 50,
    state: stateFilter
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
        onTabChange={setActiveTab}
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
            onStateFilterChange={setStateFilter}
            hasPriceReadPermission={hasPriceReadPermission}
          />
        )}

        {effectiveActiveTab === "receipts" && isInventoryCapabilityActive && (
          <ReceiptsTab
            orders={orders}
            isLoading={isLoading}
            onSelectOrder={(id) => setSelectedOrderId(id)}
            onOpenReceive={(orderItem) => {
              setOrderToReceive(toOrderView(orderItem));
            }}
            onOpenReceiveAndBill={(orderItem) => {
              setOrderToReceiveAndBill(toOrderView(orderItem));
            }}
          />
        )}

        {effectiveActiveTab === "matching" && isMatchingCapabilityActive && (
          <MatchingTab
            orders={orders}
            isLoading={isLoading}
            onOpenMatch={(orderItem) => {
              setOrderToMatch(toOrderView(orderItem));
            }}
            onSelectOrder={(id) => setSelectedOrderId(id)}
          />
        )}

        {effectiveActiveTab === "returns" && isReturnsCapabilityActive && (
          <ReturnsTab
            orders={orders}
            isLoading={isLoading}
            onOpenNewReturn={(orderItem) => {
              setOrderToReturn(toOrderView(orderItem));
            }}
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
          onOpenReceive={(ord) => setOrderToReceive(ord)}
          onOpenReceiveAndBill={(ord) => setOrderToReceiveAndBill(ord)}
          onOpenMatch={(ord) => setOrderToMatch(ord)}
          onOpenReturn={(ord) => setOrderToReturn(ord)}
          createdInvoiceRef={createdInvoiceRef}
        />
      )}

      {orderToReceive && (
        <ReceiveOrderModal
          isOpen={Boolean(orderToReceive)}
          onClose={() => setOrderToReceive(null)}
          order={orderToReceive}
        />
      )}

      {orderToReceiveAndBill && (
        <ReceiveAndBillModal
          isOpen={Boolean(orderToReceiveAndBill)}
          onClose={() => setOrderToReceiveAndBill(null)}
          order={orderToReceiveAndBill}
          onCompleted={(rcvId, invId) => {
            setCreatedInvoiceRef(invId);
          }}
        />
      )}

      {orderToMatch && (
        <MatchOrderModal
          isOpen={Boolean(orderToMatch)}
          onClose={() => setOrderToMatch(null)}
          order={orderToMatch}
          hasMatchOverridePermission={hasMatchOverridePermission}
        />
      )}

      {orderToReturn && (
        <NewSupplierReturnModal
          isOpen={Boolean(orderToReturn)}
          onClose={() => setOrderToReturn(null)}
          order={orderToReturn}
        />
      )}
    </div>
  );
}
