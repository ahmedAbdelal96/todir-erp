import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useQueryTab } from "../../../core/routing/use-query-tab.ts";
import { InventoryTabNav } from "../components/inventory-tab-nav.tsx";
import {
  InventoryWorkspaceNav,
  type InventoryWorkspaceItem
} from "../components/inventory-workspace-nav.tsx";
import type { InventoryTabItem } from "../components/inventory-tab-nav.tsx";
import { OverviewTab } from "../features/overview/overview-tab.tsx";
import { StockTab } from "../features/stock/stock-tab.tsx";
import { LocatorTab } from "../features/locator/locator-tab.tsx";
import { WarehousesTab } from "../features/warehouses/warehouses-tab.tsx";
import { TransfersTab } from "../features/transfers/transfers-tab.tsx";
import { ReservationsTab } from "../features/reservations/reservations-tab.tsx";
import { TraceabilityTab } from "../features/traceability/traceability-tab.tsx";
import { CountsTab } from "../features/counts/counts-tab.tsx";
import { AdjustmentsTab } from "../features/adjustments/adjustments-tab.tsx";
import { OpeningTab } from "../features/opening/opening-tab.tsx";
import { HistoryTab } from "../features/history/history-tab.tsx";
import { ValuationTab } from "../features/valuation/valuation-tab.tsx";
import { ReceiveStockModal } from "../features/operations/receive-stock-modal.tsx";
import { IssueStockModal } from "../features/operations/issue-stock-modal.tsx";
import { ChangeStatusModal } from "../features/operations/change-status-modal.tsx";
import { TransferModal } from "../features/transfers/transfer-modal.tsx";
import { CreateCountModal } from "../features/counts/create-count-modal.tsx";
import { CreateAdjustmentModal } from "../features/adjustments/create-adjustment-modal.tsx";
import {
  useInventoryScope,
  useInventoryPermissions,
  useWarehouses,
  useStockBalances,
  useNegativeStockConditions,
  useInventoryPolicy
} from "../hooks/use-inventory-queries.ts";

const inventoryTabIds = [
  "overview", "stock", "locator", "warehouses", "transfers", "reservations",
  "traceability", "counts", "adjustments", "opening", "history", "valuation"
] as const;
type InventoryTabId = typeof inventoryTabIds[number];

export function InventoryPage(): React.JSX.Element {
  const { t } = useTranslation("inventory");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId } = useInventoryScope({ legalEntityId: outletCtx?.legalEntityId });
  const permissions = useInventoryPermissions();

  const [activeTab, setActiveTab] = useQueryTab({
    defaultTab: "overview",
    allowedTabs: inventoryTabIds
  });

  // Operational modal triggers
  const [isReceiveOpen, setIsReceiveOpen] = useState(false);
  const [isIssueOpen, setIsIssueOpen] = useState(false);
  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [isCountOpen, setIsCountOpen] = useState(false);
  const [isAdjustOpen, setIsAdjustOpen] = useState(false);
  const [isChangeStatusOpen, setIsChangeStatusOpen] = useState(false);

  // Queries
  const { data: warehouses = [] } = useWarehouses(legalEntityId);
  const { data: balances = [] } = useStockBalances(legalEntityId);
  const { data: negativeConditions = [] } = useNegativeStockConditions(legalEntityId);
  const { data: policy } = useInventoryPolicy(legalEntityId);

  const isApprovalRequired = policy?.adjustmentApprovalMode === "ApprovalRequired";

  const workspaceGroups: readonly (InventoryWorkspaceItem & { readonly tabs: readonly InventoryTabItem[] })[] = [
    {
      id: "stock",
      label: t("workspaces.stock.label"),
      description: t("workspaces.stock.description"),
      tabs: [
        { id: "overview", label: t("tabs.overview") },
        { id: "stock", label: t("tabs.stock") },
        { id: "locator", label: t("tabs.locator") }
      ]
    },
    {
      id: "operations",
      label: t("workspaces.operations.label"),
      description: t("workspaces.operations.description"),
      tabs: [
        { id: "transfers", label: t("tabs.transfers") },
        { id: "reservations", label: t("tabs.reservations") }
      ]
    },
    {
      id: "control",
      label: t("workspaces.control.label"),
      description: t("workspaces.control.description"),
      tabs: [
        { id: "traceability", label: t("tabs.traceability") },
        { id: "counts", label: t("tabs.counts") },
        { id: "adjustments", label: t("tabs.adjustments") }
      ]
    },
    {
      id: "records",
      label: t("workspaces.records.label"),
      description: t("workspaces.records.description"),
      tabs: [
        { id: "warehouses", label: t("tabs.warehouses") },
        { id: "opening", label: t("tabs.opening") },
        { id: "history", label: t("tabs.history") },
        { id: "valuation", label: t("tabs.valuation") }
      ]
    }
  ];

  const activeWorkspace = workspaceGroups.find((workspace) =>
    workspace.tabs.some((tab) => tab.id === activeTab)
  ) ?? workspaceGroups[0];

  const handleWorkspaceSelect = (workspaceId: string) => {
    const workspace = workspaceGroups.find((item) => item.id === workspaceId);
    const firstTab = workspace?.tabs[0];
    if (firstTab && inventoryTabIds.some((id) => id === firstTab.id)) {
      setActiveTab(firstTab.id as InventoryTabId);
    }
  };

  const handleTabSelect = (tabId: string) => {
    if (inventoryTabIds.some((id) => id === tabId)) {
      setActiveTab(tabId as InventoryTabId);
    }
  };

  return (
    <div style={{ padding: "1.25rem 1.5rem", width: "100%", maxWidth: "1600px", margin: "0 auto", boxSizing: "border-box", minWidth: 0 }}>
      {/* Module Header */}
      <header
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          marginBottom: "1.25rem",
          paddingBottom: "1rem",
          borderBottom: "1px solid var(--color-border-default)"
        }}
      >
        <div>
          <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
            {t("title")}
          </h1>
          <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("subtitle")}
          </p>
        </div>

        <div style={{ display: "flex", gap: "0.5rem" }}>
          <button
            type="button"
            onClick={() => setIsChangeStatusOpen(true)}
            style={{
              padding: "0.5rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              border: "1px solid var(--color-border-default)",
              backgroundColor: "var(--color-surface-card)",
              color: "var(--color-content-primary)",
              cursor: "pointer",
              fontSize: "0.8125rem",
              fontWeight: 500
            }}
          >
            {t("operations.statusChangeTitle")}
          </button>
        </div>
      </header>

      {/* Workspace Navigation: four task-oriented areas keep all existing features accessible. */}
      <InventoryWorkspaceNav
        ariaLabel={t("workspaceNavLabel")}
        workspaces={workspaceGroups}
        activeWorkspace={activeWorkspace?.id ?? "stock"}
        onSelectWorkspace={handleWorkspaceSelect}
      />

      {/* Sub-navigation for the selected workspace. */}
      <InventoryTabNav
        tabs={activeWorkspace?.tabs ?? []}
        activeTab={activeTab}
        onSelectTab={handleTabSelect}
      />

      {/* Tab Panels */}
      <main id="inventory-tab-content" role="region" aria-live="polite">
        {activeTab === "overview" && (
          <OverviewTab
            legalEntityId={legalEntityId}
            balances={balances}
            warehouses={warehouses}
            negativeConditions={negativeConditions}
            canReadCost={permissions.canReadCost}
            onOpenReceive={() => setIsReceiveOpen(true)}
            onOpenIssue={() => setIsIssueOpen(true)}
            onOpenTransfer={() => setIsTransferOpen(true)}
            onOpenCount={() => setIsCountOpen(true)}
            onOpenAdjust={() => setIsAdjustOpen(true)}
          />
        )}

        {activeTab === "stock" && (
          <StockTab
            legalEntityId={legalEntityId}
            balances={balances}
            warehouses={warehouses}
            onOpenReceive={() => setIsReceiveOpen(true)}
            onOpenIssue={() => setIsIssueOpen(true)}
            onOpenTransfer={() => setIsTransferOpen(true)}
          />
        )}

        {activeTab === "locator" && (
          <LocatorTab
            legalEntityId={legalEntityId}
            canReadCost={permissions.canReadCost}
          />
        )}

        {activeTab === "warehouses" && (
          <WarehousesTab
            legalEntityId={legalEntityId}
            warehouses={warehouses}
          />
        )}

        {activeTab === "transfers" && (
          <TransfersTab
            legalEntityId={legalEntityId}
            warehouses={warehouses}
          />
        )}

        {activeTab === "reservations" && (
          <ReservationsTab
            legalEntityId={legalEntityId}
            warehouses={warehouses}
          />
        )}

        {activeTab === "traceability" && (
          <TraceabilityTab
            legalEntityId={legalEntityId}
          />
        )}

        {activeTab === "counts" && (
          <CountsTab
            legalEntityId={legalEntityId}
            warehouses={warehouses}
          />
        )}

        {activeTab === "adjustments" && (
          <AdjustmentsTab
            legalEntityId={legalEntityId}
            warehouses={warehouses}
            canApprove={permissions.canApproveAdjustment}
            canPost={permissions.canPostAdjustment}
            canManageProfile={permissions.canManageProfiles}
          />
        )}

        {activeTab === "opening" && (
          <OpeningTab
            legalEntityId={legalEntityId}
            warehouses={warehouses}
            canReadCost={permissions.canReadCost}
          />
        )}

        {activeTab === "history" && (
          <HistoryTab
            legalEntityId={legalEntityId}
            warehouses={warehouses}
            canReverse={permissions.canReverseOperation}
          />
        )}

        {activeTab === "valuation" && (
          <ValuationTab
            legalEntityId={legalEntityId}
            canReadCost={permissions.canReadCost}
            warehouses={warehouses}
          />
        )}
      </main>

      {/* Global Modals for Fast Actions */}
      {isReceiveOpen && (
        <ReceiveStockModal
          isOpen={isReceiveOpen}
          onClose={() => setIsReceiveOpen(false)}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
          canReadCost={permissions.canReadCost}
          canManageProfile={permissions.canManageProfiles}
        />
      )}

      {isIssueOpen && (
        <IssueStockModal
          isOpen={isIssueOpen}
          onClose={() => setIsIssueOpen(false)}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
          canOverrideNegative={permissions.canOverrideNegative}
        />
      )}

      {isChangeStatusOpen && (
        <ChangeStatusModal
          isOpen={isChangeStatusOpen}
          onClose={() => setIsChangeStatusOpen(false)}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
        />
      )}

      {isTransferOpen && (
        <TransferModal
          isOpen={isTransferOpen}
          onClose={() => setIsTransferOpen(false)}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
        />
      )}

      {isCountOpen && (
        <CreateCountModal
          isOpen={isCountOpen}
          onClose={() => setIsCountOpen(false)}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
        />
      )}

      {isAdjustOpen && (
        <CreateAdjustmentModal
          isOpen={isAdjustOpen}
          onClose={() => setIsAdjustOpen(false)}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
          isApprovalRequired={isApprovalRequired}
          canManageProfile={permissions.canManageProfiles}
        />
      )}
    </div>
  );
}
