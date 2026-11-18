import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import { SettingsPage } from "../../../components/pages/settings-page.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useOptionalLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { InventoryPolicySettings } from "../features/inventory-policy-settings.tsx";

type SettingsSection = "inventory";

export function CompanySettingsPage(): React.JSX.Element {
  const { t } = useTranslation("settings");
  const outletContext = useOutletContext<WorkspaceOutletContext | undefined>();
  const legalEntity = useOptionalLegalEntityContext();
  const [activeSection, setActiveSection] = useState<SettingsSection>("inventory");

  const entityLabel = outletContext?.legalEntityName
    ? `${outletContext.legalEntityName}${outletContext.legalEntityCode ? ` (${outletContext.legalEntityCode})` : ""}`
    : legalEntity?.name
      ? `${legalEntity.name} (${legalEntity.code})`
      : outletContext?.legalEntityId || legalEntity?.legalEntityId
        ? t("scope.selected")
        : t("scope.unknown");

  return (
    <SettingsPage title={t("title")} subtitle={t("subtitle")}>
      <Alert variant="info" title={t("scope.currentEntity")}>
        <span>{entityLabel}</span>
      </Alert>

      <section aria-label={t("categories.title")} style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.125rem", color: "var(--color-content-primary)" }}>{t("categories.title")}</h2>
          <p style={{ margin: "0.35rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("categories.description")}</p>
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(15rem, 1fr))", gap: "0.75rem" }}>
          <button
            type="button"
            onClick={() => setActiveSection("inventory")}
            aria-pressed={activeSection === "inventory"}
            style={{ display: "flex", flexDirection: "column", alignItems: "flex-start", gap: "0.4rem", padding: "1rem", textAlign: "start", border: `1px solid ${activeSection === "inventory" ? "var(--color-action-primary)" : "var(--color-border-default)"}`, borderRadius: "var(--radius-md)", backgroundColor: activeSection === "inventory" ? "var(--color-surface-primary-subtle, #eff6ff)" : "var(--color-surface-card)", color: "var(--color-content-primary)", cursor: "pointer" }}
          >
            <span style={{ fontWeight: 700 }}>{t("categories.inventory.title")}</span>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("categories.inventory.description")}</span>
            <span style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--color-status-success)" }}>{t("categories.inventory.available")}</span>
          </button>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.4rem", padding: "1rem", border: "1px dashed var(--color-border-default)", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-subtle)" }}>
            <span style={{ fontWeight: 700, color: "var(--color-content-secondary)" }}>{t("categories.comingSoon")}</span>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("categories.description")}</span>
          </div>
        </div>
      </section>

      {activeSection === "inventory" && <InventoryPolicySettings />}
    </SettingsPage>
  );
}
