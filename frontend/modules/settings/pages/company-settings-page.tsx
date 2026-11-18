import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import { SettingsPage } from "../../../components/pages/settings-page.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useOptionalLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { InventoryPolicySettings } from "../features/inventory-policy-settings.tsx";
import { SettingsTabNav, type SettingsTabItem } from "../components/settings-tab-nav.tsx";
import { SettingsWorkspaceNav, type SettingsWorkspaceItem } from "../components/settings-workspace-nav.tsx";

type SettingsWorkspace = "company" | "finance" | "commercial" | "operations";

const workspaceIds: readonly SettingsWorkspace[] = ["company", "finance", "commercial", "operations"];

export function CompanySettingsPage(): React.JSX.Element {
  const { t } = useTranslation("settings");
  const outletContext = useOutletContext<WorkspaceOutletContext | undefined>();
  const legalEntity = useOptionalLegalEntityContext();
  const [activeWorkspace, setActiveWorkspace] = useState<SettingsWorkspace>("operations");
  const [activeTab, setActiveTab] = useState("inventoryPolicy");

  const entityLabel = outletContext?.legalEntityName
    ? `${outletContext.legalEntityName}${outletContext.legalEntityCode ? ` (${outletContext.legalEntityCode})` : ""}`
    : legalEntity?.name
      ? `${legalEntity.name} (${legalEntity.code})`
      : outletContext?.legalEntityId || legalEntity?.legalEntityId
        ? t("scope.selected")
        : t("scope.unknown");

  const workspaces: readonly SettingsWorkspaceItem[] = workspaceIds.map((id) => ({
    id,
    label: t(`workspaces.${id}.title`),
    description: t(`workspaces.${id}.description`)
  }));

  const tabsByWorkspace: Record<SettingsWorkspace, readonly SettingsTabItem[]> = {
    company: [{ id: "companyProfile", label: t("tabs.companyProfile") }],
    finance: [
      { id: "financialFoundation", label: t("tabs.financialFoundation") },
      { id: "tax", label: t("tabs.tax") }
    ],
    commercial: [
      { id: "sales", label: t("tabs.sales") },
      { id: "purchasing", label: t("tabs.purchasing") }
    ],
    operations: [
      { id: "inventoryPolicy", label: t("tabs.inventoryPolicy") },
      { id: "warehouses", label: t("tabs.warehouses") },
      { id: "counting", label: t("tabs.counting") },
      { id: "traceability", label: t("tabs.traceability") }
    ]
  };

  const currentTabs = tabsByWorkspace[activeWorkspace];
  const hasInventorySettings = activeWorkspace === "operations" && activeTab === "inventoryPolicy";

  const handleWorkspaceChange = (workspace: string) => {
    const nextWorkspace = workspace as SettingsWorkspace;
    setActiveWorkspace(nextWorkspace);
    setActiveTab(tabsByWorkspace[nextWorkspace][0]?.id ?? "");
  };

  return (
    <SettingsPage title={t("title")} subtitle={t("subtitle")}>
      <Alert variant="info" title={t("scope.currentEntity")}>
        <span>{entityLabel}</span>
      </Alert>

      <SettingsWorkspaceNav
        ariaLabel={t("workspaces.ariaLabel")}
        workspaces={workspaces}
        activeWorkspace={activeWorkspace}
        onSelectWorkspace={handleWorkspaceChange}
      />

      <div id="settings-workspace-content" role="tabpanel" aria-labelledby={activeWorkspace} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: 0 }}>
        <SettingsTabNav tabs={currentTabs} activeTab={activeTab} panelId="settings-section-content" onSelectTab={setActiveTab} />
        <div id="settings-section-content" role="tabpanel" aria-labelledby={activeTab} style={{ minWidth: 0 }}>
          {hasInventorySettings ? (
            <InventoryPolicySettings />
          ) : (
            <Alert variant="info" title={t("sectionUnavailable.title")}>
              {t("sectionUnavailable.description")}
            </Alert>
          )}
        </div>
      </div>
    </SettingsPage>
  );
}
