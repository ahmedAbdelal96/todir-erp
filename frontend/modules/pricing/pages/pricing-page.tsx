import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import {
  usePricingScope,
  usePriceLists,
  usePricingPolicy,
  usePricingCoverage
} from "../hooks/pricing-queries.ts";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import { CreatePriceListModal } from "../features/create-price-list-modal.tsx";
import { VersionEditorDrawer } from "../features/version-editor-drawer.tsx";
import { PricingSimulatorWidget } from "../features/pricing-simulator-widget.tsx";
import { PricingImportModal } from "../features/pricing-import-modal.tsx";
import { PricingPolicyModal } from "../features/pricing-policy-modal.tsx";
import type { PriceListDto, PricingDirection } from "../types.ts";

export function PricingPage(): React.JSX.Element {
  const { t, i18n } = useTranslation("pricing");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId } = usePricingScope({ legalEntityId: outletCtx?.legalEntityId });

  const permissions = useOptionalPermissions();
  const canManage = permissions ? permissions.hasAnyPermission("pricing.manage", "pricing.write", "pricing.*", "*", "admin") : true;

  const [activeTab, setActiveTab] = useState("overview");

  // Dialog states
  const [isCreateListOpen, setIsCreateListOpen] = useState(false);
  const [createDirection, setCreateDirection] = useState<PricingDirection>("Selling");
  const [isVersionEditorOpen, setIsVersionEditorOpen] = useState(false);
  const [selectedPriceList, setSelectedPriceList] = useState<PriceListDto | null>(null);
  const [isImportOpen, setIsImportOpen] = useState(false);
  const [isPolicyOpen, setIsPolicyOpen] = useState(false);

  const { data: priceLists, isLoading: listsLoading } = usePriceLists(legalEntityId);
  const { data: policy } = usePricingPolicy(legalEntityId);

  const sellingLists = priceLists?.filter((l) => l.direction === "Selling") || [];
  const buyingLists = priceLists?.filter((l) => l.direction === "Buying") || [];
  const defaultSelling = sellingLists.find((l) => l.code === "RETAIL" || (l as { isDefault?: boolean }).isDefault) || sellingLists[0];

  const pricingDate = "2026-01-05";
  const { data: coverage } = usePricingCoverage(legalEntityId, defaultSelling?.id ?? "", pricingDate);

  const handleOpenVersions = (list: PriceListDto) => {
    setSelectedPriceList(list);
    setIsVersionEditorOpen(true);
  };

  const tabOptions = [
    { id: "overview", label: t("tabs.overview") },
    { id: "selling", label: t("tabs.selling") },
    { id: "buying", label: t("tabs.buying") },
    { id: "simulator", label: t("tabs.simulator") },
    { id: "importExport", label: t("tabs.importExport") },
    { id: "policies", label: t("tabs.policies") }
  ];

  if (listsLoading && !priceLists) {
    return (
      <div style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "360px" }}>
        <Spinner size="large" />
      </div>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)", padding: "var(--space-4)" }}>
      {/* HEADER */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "var(--space-3)" }}>
        <div>
          <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)", flexWrap: "wrap" }}>
            <h1 style={{ margin: 0, fontSize: "var(--font-size-2xl)", fontWeight: 700 }}>
              {t("title")}
            </h1>
            <StatusBadge intent="neutral" label={t("entityLockedBadge")} size="small" />
            <StatusBadge
              intent={policy?.currencyPolicy === "AllowFinBaseConversion" ? "info" : "neutral"}
              label={
                policy?.currencyPolicy === "AllowFinBaseConversion"
                  ? t("activeCurrencyPolicyBadge")
                  : t("singleCurrencyPolicyBadge")
              }
              size="small"
            />
          </div>
          <p style={{ margin: "var(--space-1) 0 0", fontSize: "var(--font-size-sm)", color: "var(--color-content-secondary)" }}>
            {t("subtitle")}
          </p>
        </div>

        {canManage ? (
          <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)", flexWrap: "wrap" }}>
            <Button
              variant="secondary"
              size="compact"
              onPress={() => setIsImportOpen(true)}
            >
              {t("actions.importPrices")}
            </Button>

            <Button
              variant="secondary"
              size="compact"
              onPress={() => setIsPolicyOpen(true)}
            >
              {t("actions.editPolicy")}
            </Button>

            <Button
              variant="primary"
              size="compact"
              onPress={() => {
                setCreateDirection("Selling");
                setIsCreateListOpen(true);
              }}
            >
              + {t("actions.newPriceList")}
            </Button>
          </div>
        ) : (
          <StatusBadge intent="neutral" label={t("permissions.readOnlyBadge")} />
        )}
      </div>

      {!canManage && (
        <Alert variant="info" title={t("permissions.readOnlyBadge")}>
          {t("permissions.readOnlyNotice")}
        </Alert>
      )}

      {/* NAVIGATION TABS / MOBILE SELECTOR */}
      <div className="pricing-tabs-container">
        {/* Mobile View: Select Dropdown */}
        <div className="pricing-tabs-mobile" style={{ display: "none" }}>
          <Select
            label={t("tabs.overview")}
            selectedKey={activeTab}
            onSelectionChange={(k) => setActiveTab(k as string)}
            options={tabOptions}
          />
        </div>

        {/* Desktop View: Tab Buttons */}
        <div className="pricing-tabs-desktop" style={{ display: "flex", gap: "var(--space-1)", borderBottom: "1px solid var(--color-border-subtle)", paddingBottom: "var(--space-1)", overflowX: "auto" }}>
          {tabOptions.map((tab) => {
            const isSelected = tab.id === activeTab;
            return (
              <button
                key={tab.id}
                type="button"
                onClick={() => setActiveTab(tab.id)}
                style={{
                  padding: "var(--space-2) var(--space-3)",
                  borderRadius: "var(--radius-sm)",
                  border: "none",
                  backgroundColor: isSelected ? "var(--color-brand-primary-subtle)" : "transparent",
                  color: isSelected ? "var(--color-brand-primary)" : "var(--color-content-secondary)",
                  fontWeight: isSelected ? 700 : 500,
                  fontSize: "var(--font-size-sm)",
                  cursor: "pointer",
                  whiteSpace: "nowrap"
                }}
              >
                {tab.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* TAB 1: OVERVIEW */}
      {activeTab === "overview" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
          {/* Key Metrics */}
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "var(--space-3)" }}>
            <Surface>
              <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                {t("overview.metrics.activeSellingLists")}
              </div>
              <div style={{ fontSize: "var(--font-size-2xl)", fontWeight: 700, margin: "var(--space-1) 0" }}>
                {sellingLists.length}
              </div>
              <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                {t("overview.metrics.activeSellingListsHint")}
              </div>
            </Surface>

            <Surface>
              <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                {t("overview.metrics.activeBuyingLists")}
              </div>
              <div style={{ fontSize: "var(--font-size-2xl)", fontWeight: 700, margin: "var(--space-1) 0" }}>
                {buyingLists.length}
              </div>
              <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                {t("overview.metrics.activeBuyingListsHint")}
              </div>
            </Surface>

            <Surface>
              <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                {t("overview.metrics.priceGroups")}
              </div>
              <div style={{ fontSize: "var(--font-size-2xl)", fontWeight: 700, margin: "var(--space-1) 0" }} dir="ltr">
                {priceLists ? `${priceLists.length}` : "—"}
              </div>
              <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                {t("overview.metrics.priceGroupsHint")}
              </div>
            </Surface>

            <Surface>
              <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                {t("overview.metrics.coverageRatio")}
              </div>
              <div style={{ fontSize: "var(--font-size-2xl)", fontWeight: 700, margin: "var(--space-1) 0", color: "var(--color-brand-primary)" }} dir="ltr">
                {coverage ? `${coverage.productCount}` : "—"}
              </div>
              <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                {t("overview.metrics.coverageRatioHint")}
              </div>
            </Surface>
          </div>

          {/* Small Business SME Card */}
          <Surface title={t("overview.smeCard.title")}>
            <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-2)" }}>
              <p style={{ margin: 0, fontSize: "var(--font-size-sm)", color: "var(--color-content-secondary)", lineHeight: 1.6 }}>
                {t("overview.smeCard.description")}
              </p>
              <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)", marginTop: "var(--space-1)" }}>
                <Button
                  variant="secondary"
                  size="compact"
                  onPress={() => setActiveTab("simulator")}
                >
                  {t("overview.smeCard.quickAction")}
                </Button>
                <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                  {t("overview.entityScopeNotice")}
                </span>
              </div>
            </div>
          </Surface>

          {/* Quick Simulator Surface */}
          <PricingSimulatorWidget legalEntityId={legalEntityId} />
        </div>
      )}

      {/* TAB 2: SELLING PRICE LISTS */}
      {activeTab === "selling" && (
        <Surface
          title={t("lists.sellingTitle")}
          headerActions={
            canManage ? (
              <Button
                variant="primary"
                size="compact"
                onPress={() => {
                  setCreateDirection("Selling");
                  setIsCreateListOpen(true);
                }}
              >
                + {t("lists.createSellingAction")}
              </Button>
            ) : undefined
          }
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
            <p style={{ margin: 0, fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
              {t("lists.sellingDescription")}
            </p>

            <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "var(--font-size-sm)" }}>
                <thead>
                  <tr style={{ backgroundColor: "var(--color-surface-subtle)", textAlign: "start" }}>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.codeHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.nameHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.currencyHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.currencyPolicyHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.isDefaultHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.statusHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.actionsHeader")}</th>
                  </tr>
                </thead>
                <tbody>
                  {sellingLists.map((list) => {
                    const isDefault = list.code === "RETAIL" || (list as { isDefault?: boolean }).isDefault;
                    return (
                      <tr key={list.id} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                        <td style={{ padding: "10px 14px", fontWeight: 700 }} dir="ltr">{list.code}</td>
                        <td style={{ padding: "10px 14px", fontWeight: 600 }}>{list.name}</td>
                        <td style={{ padding: "10px 14px" }} dir="ltr">{list.currencyCode}</td>
                        <td style={{ padding: "10px 14px", fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                          {t(`lists.currencyPolicies.${list.currencyPolicy}`, list.currencyPolicy)}
                        </td>
                        <td style={{ padding: "10px 14px" }}>
                          <StatusBadge
                            intent={isDefault ? "success" : "neutral"}
                            label={isDefault ? t("lists.table.defaultBadge") : t("lists.table.notDefaultBadge")}
                          />
                        </td>
                        <td style={{ padding: "10px 14px" }}>
                          <StatusBadge
                            intent={list.status === "Active" ? "success" : "neutral"}
                            label={t(`lists.status.${list.status}`, list.status)}
                          />
                        </td>
                        <td style={{ padding: "10px 14px" }}>
                          <Button
                            variant="secondary"
                            size="compact"
                            onPress={() => handleOpenVersions(list)}
                          >
                            {t("lists.table.viewVersionsAction")}
                          </Button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </Surface>
      )}

      {/* TAB 3: BUYING PRICE LISTS */}
      {activeTab === "buying" && (
        <Surface
          title={t("lists.buyingTitle")}
          headerActions={
            canManage ? (
              <Button
                variant="primary"
                size="compact"
                onPress={() => {
                  setCreateDirection("Buying");
                  setIsCreateListOpen(true);
                }}
              >
                + {t("lists.createBuyingAction")}
              </Button>
            ) : undefined
          }
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
            <p style={{ margin: 0, fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
              {t("lists.buyingDescription")}
            </p>

            <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "var(--font-size-sm)" }}>
                <thead>
                  <tr style={{ backgroundColor: "var(--color-surface-subtle)", textAlign: "start" }}>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.codeHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.nameHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.currencyHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.currencyPolicyHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.isDefaultHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.statusHeader")}</th>
                    <th style={{ padding: "10px 14px" }}>{t("lists.table.actionsHeader")}</th>
                  </tr>
                </thead>
                <tbody>
                  {buyingLists.map((list) => {
                    const isDefault = list.code === "PURCHASE" || (list as { isDefault?: boolean }).isDefault;
                    return (
                      <tr key={list.id} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                        <td style={{ padding: "10px 14px", fontWeight: 700 }} dir="ltr">{list.code}</td>
                        <td style={{ padding: "10px 14px", fontWeight: 600 }}>{list.name}</td>
                        <td style={{ padding: "10px 14px" }} dir="ltr">{list.currencyCode}</td>
                        <td style={{ padding: "10px 14px", fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                          {t(`lists.currencyPolicies.${list.currencyPolicy}`, list.currencyPolicy)}
                        </td>
                        <td style={{ padding: "10px 14px" }}>
                          <StatusBadge
                            intent={isDefault ? "success" : "neutral"}
                            label={isDefault ? t("lists.table.defaultBadge") : t("lists.table.notDefaultBadge")}
                          />
                        </td>
                        <td style={{ padding: "10px 14px" }}>
                          <StatusBadge
                            intent={list.status === "Active" ? "success" : "neutral"}
                            label={t(`lists.status.${list.status}`, list.status)}
                          />
                        </td>
                        <td style={{ padding: "10px 14px" }}>
                          <Button
                            variant="secondary"
                            size="compact"
                            onPress={() => handleOpenVersions(list)}
                          >
                            {t("lists.table.viewVersionsAction")}
                          </Button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </Surface>
      )}

      {/* TAB 4: SIMULATOR */}
      {activeTab === "simulator" && (
        <PricingSimulatorWidget legalEntityId={legalEntityId} />
      )}

      {/* TAB 5: IMPORT / EXPORT */}
      {activeTab === "importExport" && (
        <Surface title={t("importExport.title")}>
          <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
            <p style={{ margin: 0, fontSize: "var(--font-size-sm)", color: "var(--color-content-secondary)" }}>
              {t("importExport.description")}
            </p>

            <div style={{ display: "flex", gap: "var(--space-3)", flexWrap: "wrap", marginTop: "var(--space-2)" }}>
              {canManage && (
                <Button variant="primary" onPress={() => setIsImportOpen(true)}>
                  {t("actions.importPrices")}
                </Button>
              )}
              <Button
                variant="secondary"
                onPress={() => {
                  if (defaultSelling) handleOpenVersions(defaultSelling);
                }}
              >
                {t("actions.exportPrices")}
              </Button>
            </div>
          </div>
        </Surface>
      )}

      {/* TAB 6: POLICIES & COVERAGE */}
      {activeTab === "policies" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
          <Surface
            title={t("policies.policyCardTitle")}
            headerActions={
              canManage ? (
                <Button variant="secondary" size="compact" onPress={() => setIsPolicyOpen(true)}>
                  {t("actions.editPolicy")}
                </Button>
              ) : undefined
            }
          >
            <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-2)", fontSize: "var(--font-size-sm)" }}>
              <div>
                <span style={{ color: "var(--color-content-muted)" }}>{t("policies.currencyPolicyLabel")}: </span>
                <strong style={{ marginInlineStart: "var(--space-1)" }}>
                  {t(`lists.currencyPolicies.${policy?.currencyPolicy}`, policy?.currencyPolicy || "—")}
                </strong>
              </div>
              <div>
                <span style={{ color: "var(--color-content-muted)" }}>{t("policies.allowUomDerivedLabel")}: </span>
                <strong>{policy?.allowUomDerivedPrice ? "✓" : "✕"}</strong>
              </div>
            </div>
          </Surface>

          <Surface title={t("policies.coverageCardTitle")}>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "var(--space-3)" }}>
              <div style={{ padding: "var(--space-3)", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                  {t("policies.coverageStats.productsWithPrice")}
                </div>
                <div style={{ fontSize: "var(--font-size-xl)", fontWeight: 700, marginTop: "var(--space-1)" }}>
                  {coverage?.productCount ?? 1}
                </div>
              </div>

              <div style={{ padding: "var(--space-3)", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                  {t("policies.coverageStats.missingPriceCount")}
                </div>
                <div style={{ fontSize: "var(--font-size-xl)", fontWeight: 700, marginTop: "var(--space-1)", color: "var(--color-status-warning)" }}>
                  {coverage?.missingCandidateCount ?? 0}
                </div>
              </div>

              <div style={{ padding: "var(--space-3)", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                  {t("policies.coverageStats.explicitZeroCount")}
                </div>
                <div style={{ fontSize: "var(--font-size-xl)", fontWeight: 700, marginTop: "var(--space-1)" }}>
                  {coverage?.explicitZeroCount ?? 1}
                </div>
              </div>

              <div style={{ padding: "var(--space-3)", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                  {t("policies.coverageStats.entryCount")}
                </div>
                <div style={{ fontSize: "var(--font-size-xl)", fontWeight: 700, marginTop: "var(--space-1)" }}>
                  {coverage?.entryCount ?? 5}
                </div>
              </div>
            </div>
          </Surface>
        </div>
      )}

      {/* MODALS */}
      <CreatePriceListModal
        isOpen={isCreateListOpen}
        onClose={() => setIsCreateListOpen(false)}
        legalEntityId={legalEntityId}
        defaultDirection={createDirection}
      />

      <VersionEditorDrawer
        isOpen={isVersionEditorOpen}
        onClose={() => {
          setIsVersionEditorOpen(false);
          setSelectedPriceList(null);
        }}
        priceList={selectedPriceList}
        legalEntityId={legalEntityId}
      />

      <PricingImportModal
        isOpen={isImportOpen}
        onClose={() => setIsImportOpen(false)}
        legalEntityId={legalEntityId}
      />

      <PricingPolicyModal
        isOpen={isPolicyOpen}
        onClose={() => setIsPolicyOpen(false)}
        legalEntityId={legalEntityId}
      />
    </div>
  );
}
