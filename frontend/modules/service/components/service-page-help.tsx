import React from "react";
import { useTranslation } from "react-i18next";
import { QuickPeekDrawer } from "../../../components/primitives/quick-peek-drawer.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import {
  WrenchIcon,
  CalendarIcon,
  TruckIcon,
  ShieldCheckIcon,
  ClipboardCheckIcon,
  ClockIcon,
  AlertCircleIcon
} from "./service-icons.tsx";

export interface ServicePageHelpProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function ServicePageHelp({ isOpen, onClose }: ServicePageHelpProps): React.JSX.Element {
  const { t } = useTranslation("service");

  return (
    <QuickPeekDrawer
      isOpen={isOpen}
      onClose={onClose}
      title={t("help.title")}
      subtitle={t("help.subtitle")}
      width="480px"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", padding: "1rem 0" }}>
        {/* Module Mission Section */}
        <div
          style={{
            padding: "1rem",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            borderRadius: "var(--radius-md, 0.5rem)",
            border: "1px solid var(--color-border-subtle, #e2e8f0)"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "0.5rem" }}>
            <WrenchIcon size={20} color="#0284c7" />
            <strong style={{ fontSize: "0.9375rem", color: "var(--color-content-primary, #0f172a)" }}>
              {t("title")}
            </strong>
          </div>
          <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-secondary, #475569)", lineHeight: 1.6 }}>
            {t("help.missionDescription")}
          </p>
        </div>

        {/* Roles & Workspaces */}
        <div>
          <h4 style={{ margin: "0 0 0.75rem", fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("help.rolesTitle")}
          </h4>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
            <div style={{ display: "flex", gap: "0.75rem" }}>
              <CalendarIcon size={18} color="#2563eb" />
              <div>
                <strong style={{ fontSize: "0.8125rem", color: "var(--color-content-primary, #0f172a)" }}>
                  {t("help.roles.dispatcher.title")}
                </strong>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-secondary, #475569)" }}>
                  {t("help.roles.dispatcher.desc")}
                </p>
              </div>
            </div>

            <div style={{ display: "flex", gap: "0.75rem" }}>
              <TruckIcon size={18} color="#059669" />
              <div>
                <strong style={{ fontSize: "0.8125rem", color: "var(--color-content-primary, #0f172a)" }}>
                  {t("help.roles.technician.title")}
                </strong>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-secondary, #475569)" }}>
                  {t("help.roles.technician.desc")}
                </p>
              </div>
            </div>

            <div style={{ display: "flex", gap: "0.75rem" }}>
              <ShieldCheckIcon size={18} color="#d97706" />
              <div>
                <strong style={{ fontSize: "0.8125rem", color: "var(--color-content-primary, #0f172a)" }}>
                  {t("help.roles.serviceManager.title")}
                </strong>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-secondary, #475569)" }}>
                  {t("help.roles.serviceManager.desc")}
                </p>
              </div>
            </div>

            <div style={{ display: "flex", gap: "0.75rem" }}>
              <ClipboardCheckIcon size={18} color="#7c3aed" />
              <div>
                <strong style={{ fontSize: "0.8125rem", color: "var(--color-content-primary, #0f172a)" }}>
                  {t("help.roles.maintenanceManager.title")}
                </strong>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-secondary, #475569)" }}>
                  {t("help.roles.maintenanceManager.desc")}
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Lifecycles & Best Practices */}
        <div>
          <h4 style={{ margin: "0 0 0.75rem", fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("help.lifecycleTitle")}
          </h4>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
            <div
              style={{
                padding: "0.75rem",
                borderRadius: "var(--radius-sm, 0.375rem)",
                backgroundColor: "#eff6ff",
                border: "1px solid #bfdbfe",
                fontSize: "0.8125rem"
              }}
            >
              <strong style={{ color: "#1e40af" }}>{t("help.workOrderLifecycleTitle")}:</strong>
              <p style={{ margin: "0.25rem 0 0", color: "#1e3a8a", fontSize: "0.75rem" }}>
                {t("help.workOrderLifecycleSteps")}
              </p>
            </div>

            <div
              style={{
                padding: "0.75rem",
                borderRadius: "var(--radius-sm, 0.375rem)",
                backgroundColor: "#ecfdf5",
                border: "1px solid #a7f3d0",
                fontSize: "0.8125rem"
              }}
            >
              <strong style={{ color: "#065f46" }}>{t("help.slaTitle")}:</strong>
              <p style={{ margin: "0.25rem 0 0", color: "#064e3b", fontSize: "0.75rem" }}>
                {t("help.slaDescription")}
              </p>
            </div>
          </div>
        </div>

        {/* Close Button */}
        <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose}>
            {t("common.close")}
          </Button>
        </div>
      </div>
    </QuickPeekDrawer>
  );
}
