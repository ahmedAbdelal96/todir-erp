import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { SalesTabNav, type SalesTabDef, type SalesTabId } from "../components/sales-tab-nav.tsx";
import { SalesOverviewTab } from "../features/overview/sales-overview-tab.tsx";
import { QuotationListTab } from "../features/quotations/quotation-list-tab.tsx";
import { NewQuotationModal } from "../features/quotations/new-quotation-modal.tsx";
import { QuotationDetailModal } from "../features/quotations/quotation-detail-modal.tsx";
import { OrderListTab } from "../features/orders/order-list-tab.tsx";
import { ReturnsTab } from "../features/returns/returns-tab.tsx";
import { SalesHistoryTab } from "../features/history/sales-history-tab.tsx";
import { SalesSettingsTab } from "../features/settings/sales-settings-tab.tsx";
import { useSalesQuotations } from "../hooks/use-sales-queries.ts";
import { useLegalEntityContext, useOptionalPermissions, useOptionalCapabilities } from "../../../core/runtime/runtime-provider.tsx";
import { useOutletContext, useSearchParams } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";

export function SalesPage(): React.JSX.Element {
  const { t } = useTranslation("sales");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const [searchParams] = useSearchParams();

  let contextEntityId: string | undefined;
  try {
    const context = useLegalEntityContext();
    contextEntityId = context?.legalEntityId;
  } catch {
    // Rendered outside RuntimeProvider
  }

  const legalEntityId = outletCtx?.legalEntityId ?? contextEntityId ?? "00000000-0000-0000-0000-000000000001";

  // Tab State
  const tabParam = searchParams.get("tab") as SalesTabId | null;
  const [activeTab, setActiveTab] = useState<SalesTabId>(tabParam ?? "overview");

  // Derive capability disclosure from backend authority / session / query params
  const distributionParam = searchParams.get("distribution");
  const isDistributionOff = distributionParam === "false" ||
    (typeof window !== "undefined" && window.sessionStorage?.getItem("erp.capability.sales.orders") === "false");

  const capabilities = useOptionalCapabilities();
  const permissions = useOptionalPermissions();

  const isOrderCapabilityActive = capabilities
    ? capabilities.isActivated("sales.orders") && !isDistributionOff
    : !isDistributionOff;

  const isReturnCapabilityActive = capabilities
    ? capabilities.isActivated("sales.returns") && !isDistributionOff
    : !isDistributionOff;

  const canViewOrders = isOrderCapabilityActive && (permissions ? permissions.hasPermission("sales.order.read") : true);
  const canViewReturns = isReturnCapabilityActive && (permissions ? permissions.hasPermission("sales.return.read") : true);

  const effectiveActiveTab = (!canViewOrders && activeTab === "orders") || (!canViewReturns && activeTab === "returns")
    ? "overview"
    : activeTab;

  // Quotation pagination & filter state
  const [page, setPage] = useState(1);
  const [stateFilter, setStateFilter] = useState<string | null>(null);

  // Modals state
  const [isNewQuotationOpen, setIsNewQuotationOpen] = useState(false);
  const [selectedQuotationId, setSelectedQuotationId] = useState<string | null>(null);

  const { data: quotesPage, isLoading: isQuotesLoading } = useSalesQuotations(legalEntityId, {
    page,
    pageSize: 50,
    state: stateFilter
  });

  const quotations = quotesPage?.items ?? [];
  const totalQuotationsCount = quotesPage?.totalCount ?? quotations.length;

  // Build Tab List based on simple vs advanced mode
  const tabs: readonly SalesTabDef[] = [
    { id: "overview", label: t("tabs.overview") },
    { id: "quotations", label: t("tabs.quotations"), badgeCount: quotations.length },
    ...(canViewOrders ? [{ id: "orders", label: t("tabs.orders"), isAdvanced: true } as const] : []),
    ...(canViewReturns ? [{ id: "returns", label: t("tabs.returns"), isAdvanced: true } as const] : []),
    { id: "history", label: t("tabs.history") },
    { id: "settings", label: t("tabs.settings") }
  ];

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
      {/* Module Title Banner & Mode Switcher */}
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

      {/* Primary Tab Navigation */}
      <SalesTabNav
        activeTab={effectiveActiveTab}
        onTabChange={setActiveTab}
        tabs={tabs}
      />

      {/* Tab Panels */}
      <div>
        {effectiveActiveTab === "overview" && (
          <SalesOverviewTab
            quotations={quotations}
            isLoading={isQuotesLoading}
            onSelectQuotation={(id) => setSelectedQuotationId(id)}
            onNewQuotation={() => setIsNewQuotationOpen(true)}
          />
        )}

        {effectiveActiveTab === "quotations" && (
          <QuotationListTab
            quotations={quotations}
            isLoading={isQuotesLoading}
            totalCount={totalQuotationsCount}
            page={page}
            onPageChange={setPage}
            onSelectQuotation={(id) => setSelectedQuotationId(id)}
            onNewQuotation={() => setIsNewQuotationOpen(true)}
            stateFilter={stateFilter}
            onStateFilterChange={setStateFilter}
          />
        )}

        {effectiveActiveTab === "orders" && canViewOrders && (
          <OrderListTab legalEntityId={legalEntityId} />
        )}

        {effectiveActiveTab === "returns" && canViewReturns && (
          <ReturnsTab legalEntityId={legalEntityId} />
        )}

        {effectiveActiveTab === "history" && (
          <SalesHistoryTab
            quotations={quotations}
            isLoading={isQuotesLoading}
            onSelectQuotation={(id) => setSelectedQuotationId(id)}
          />
        )}

        {effectiveActiveTab === "settings" && (
          <SalesSettingsTab legalEntityId={legalEntityId} />
        )}
      </div>

      {/* Creation Modal */}
      {isNewQuotationOpen && (
        <NewQuotationModal
          isOpen={isNewQuotationOpen}
          onClose={() => setIsNewQuotationOpen(false)}
          legalEntityId={legalEntityId}
          onCreated={(newId) => {
            setSelectedQuotationId(newId);
          }}
        />
      )}

      {/* Detail / Lifecycle Modal */}
      {selectedQuotationId && (
        <QuotationDetailModal
          quotationId={selectedQuotationId}
          legalEntityId={legalEntityId}
          onClose={() => setSelectedQuotationId(null)}
          onOrderCreated={() => {
            setSelectedQuotationId(null);
            if (canViewOrders) {
              setActiveTab("orders");
            }
          }}
        />
      )}
    </div>
  );
}
