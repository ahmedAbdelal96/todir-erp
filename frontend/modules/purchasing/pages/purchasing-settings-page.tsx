import React from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext, useOptionalCapabilities } from "../../../core/runtime/runtime-provider.tsx";
import { PurchaseSettingsTab } from "../features/settings/purchase-settings-tab.tsx";

export function PurchasingSettingsPage(): React.JSX.Element {
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

  const capabilities = useOptionalCapabilities();
  const isAdvanced = capabilities
    ? capabilities.isActivated("inventory.receipts") || capabilities.isActivated("purchasing.matching")
    : true;

  if (!legalEntityId) {
    return (
      <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
        {t("common.loading", "جاري التحميل...")}
      </div>
    );
  }

  return (
    <div
      data-testid="purchasing-settings-page"
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.5rem",
        padding: "1.5rem 2rem",
        backgroundColor: "var(--color-surface-page, #f0f4f8)",
        minHeight: "100vh"
      }}
    >
      <PurchaseSettingsTab legalEntityId={legalEntityId} isAdvanced={isAdvanced} />
    </div>
  );
}
