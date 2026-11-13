import React from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../components/primitives/button.tsx";
import {
  BuildingIcon,
  PlusIcon,
  HelpCircleIcon,
  DownloadIcon
} from "./fixed-assets-icons.tsx";

export interface FixedAssetsHeaderProps {
  readonly onOpenNewAssetModal: () => void;
  readonly onOpenHelpDrawer: () => void;
  readonly legalEntityName?: string | undefined;
  readonly canCreate?: boolean | undefined;
}

export function FixedAssetsHeader({
  onOpenNewAssetModal,
  onOpenHelpDrawer,
  legalEntityName,
  canCreate = true
}: FixedAssetsHeaderProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");

  return (
    <header
      data-testid="fixed-assets-header"
      style={{
        backgroundColor: "#102a43",
        color: "#ffffff",
        padding: "1.25rem 2rem",
        borderBottom: "1px solid rgba(255, 255, 255, 0.1)",
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "wrap",
        gap: "1rem"
      }}
    >
      <div>
        <div style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
          <BuildingIcon size={24} color="#60a5fa" />
          <h1 data-testid="fixed-assets-title" style={{ margin: 0, fontSize: "1.5rem", fontWeight: 700, color: "#ffffff" }}>
            {t("title", "منظومة الأصول الثابتة (Fixed Assets)")}
          </h1>
          {legalEntityName && (
            <span
              style={{
                backgroundColor: "rgba(255, 255, 255, 0.15)",
                padding: "0.2rem 0.6rem",
                borderRadius: "9999px",
                fontSize: "0.75rem",
                color: "#e2e8f0"
              }}
            >
              {legalEntityName}
            </span>
          )}
        </div>
        <p style={{ margin: "0.375rem 0 0", fontSize: "0.875rem", color: "#94a3b8" }}>
          {t("subtitle", "إدارة دورة حياة الأصول الرأسمالية، مسك الدفاتر المحاسبية والضريبية، ومسيرات الإهلاك وتتبع العهد الميدانية")}
        </p>
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
        <button
          type="button"
          onClick={onOpenHelpDrawer}
          data-testid="fixed-assets-help-btn"
          style={{
            display: "flex",
            alignItems: "center",
            gap: "0.375rem",
            backgroundColor: "transparent",
            color: "#94a3b8",
            border: "1px solid rgba(255, 255, 255, 0.2)",
            borderRadius: "0.375rem",
            padding: "0.5rem 0.75rem",
            fontSize: "0.8125rem",
            cursor: "pointer"
          }}
        >
          <HelpCircleIcon size={16} />
          <span>{t("common.help", "دليل المنظومة")}</span>
        </button>

        {canCreate && (
          <Button
            variant="primary"
            onClick={onOpenNewAssetModal}
            data-testid="header-new-asset-btn"
            style={{
              backgroundColor: "#2563eb",
              color: "#ffffff",
              border: "none",
              fontWeight: 600
            }}
          >
            <PlusIcon size={16} style={{ marginInlineEnd: "0.375rem" }} />
            {t("register.newAsset", "إضافة أصل جديد")}
          </Button>
        )}
      </div>
    </header>
  );
}
