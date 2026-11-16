import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useSearchParams } from "react-router-dom";
import { useOptionalLegalEntityContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useIsMobile } from "../hooks/use-is-mobile.ts";
import { ServiceOverviewTab } from "../features/overview/service-overview-tab.tsx";
import { ServiceRequestsTab } from "../features/requests/service-requests-tab.tsx";
import { WorkOrdersTab } from "../features/work-orders/work-orders-tab.tsx";
import { DispatchTab } from "../features/dispatch/dispatch-tab.tsx";
import { MyDayTab } from "../features/technician/my-day-tab.tsx";
import { InstalledBaseTab } from "../features/installed-base/installed-base-tab.tsx";
import { PreventiveTab } from "../features/preventive/preventive-tab.tsx";
import { ContractsWarrantyTab } from "../features/contracts-warranty/contracts-warranty-tab.tsx";
import { ServiceBillingTab } from "../features/billing/service-billing-tab.tsx";
import { ServicePageHelp } from "../components/service-page-help.tsx";
import {
  WrenchIcon,
  CalendarIcon,
  TruckIcon,
  ShieldCheckIcon,
  ClipboardCheckIcon,
  ClockIcon,
  FileSpreadsheetIcon,
  HelpCircleIcon,
  AlertCircleIcon
} from "../components/service-icons.tsx";

export type ServiceTabId =
  | "overview"
  | "requests"
  | "work-orders"
  | "dispatch"
  | "my-day"
  | "installed-base"
  | "preventive"
  | "contracts-warranty"
  | "billing";

export function ServicePage(): React.JSX.Element {
  const { t } = useTranslation("service");
  const isMobile = useIsMobile(768);

  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const permissions = outletCtx?.permissions ?? useOptionalPermissions();

  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = (searchParams.get("tab") as ServiceTabId | null) ?? "overview";
  const [isHelpOpen, setIsHelpOpen] = useState(false);

  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || null;

  const handleTabChange = (newTab: string) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("tab", newTab);
      return next;
    });
  };

  const tabs: readonly {
    id: ServiceTabId;
    label: string;
    icon: React.ReactNode;
  }[] = [
    {
      id: "overview",
      label: t("tabs.overview"),
      icon: <ClipboardCheckIcon size={16} />
    },
    {
      id: "requests",
      label: t("tabs.requests"),
      icon: <AlertCircleIcon size={16} />
    },
    {
      id: "work-orders",
      label: t("tabs.workOrders"),
      icon: <WrenchIcon size={16} />
    },
    {
      id: "dispatch",
      label: t("tabs.dispatch"),
      icon: <CalendarIcon size={16} />
    },
    {
      id: "my-day",
      label: t("tabs.myDay"),
      icon: <TruckIcon size={16} />
    },
    {
      id: "installed-base",
      label: t("tabs.installedBase"),
      icon: <WrenchIcon size={16} />
    },
    {
      id: "preventive",
      label: t("tabs.preventive"),
      icon: <CalendarIcon size={16} />
    },
    {
      id: "contracts-warranty",
      label: t("tabs.contractsWarranty"),
      icon: <ShieldCheckIcon size={16} />
    },
    {
      id: "billing",
      label: t("tabs.billing"),
      icon: <FileSpreadsheetIcon size={16} />
    }
  ];

  if (!legalEntityId) {
    return (
      <div
        data-testid="service-page"
        style={{
          display: "flex",
          flexDirection: "column",
          minHeight: "100%",
          backgroundColor: "var(--color-surface-page, #f0f4f8)"
        }}
      >
        <header
          style={{
            backgroundColor: "#102a43",
            color: "#ffffff",
            padding: "1.25rem 2rem",
            borderBottom: "1px solid rgba(255, 255, 255, 0.1)"
          }}
        >
          <h1 style={{ margin: 0, fontSize: "1.5rem", fontWeight: 700, color: "#ffffff" }}>
            {t("title")}
          </h1>
          <p style={{ margin: "0.375rem 0 0", fontSize: "0.875rem", color: "#94a3b8" }}>
            {t("subtitle")}
          </p>
        </header>
        <main style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
          <div
            style={{
              maxWidth: "480px",
              margin: "2rem auto",
              padding: "2rem",
              borderRadius: "var(--radius-lg, 0.75rem)",
              backgroundColor: "#ffffff",
              border: "1px solid var(--color-border-default, #cbd5e1)"
            }}
          >
            <h3 style={{ margin: "0 0 0.5rem", color: "var(--color-content-primary, #0f172a)" }}>
              {t("common.noLegalEntitySelected")}
            </h3>
            <p style={{ margin: 0, fontSize: "0.875rem" }}>
              {t("common.selectLegalEntityNotice")}
            </p>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div
      data-testid="service-page"
      style={{
        display: "flex",
        flexDirection: "column",
        minHeight: "100%",
        backgroundColor: "var(--color-surface-page, #f0f4f8)"
      }}
    >
      {/* Dark Oxford Navy Application Header */}
      <header
        style={{
          backgroundColor: "#102a43",
          color: "#ffffff",
          padding: isMobile ? "1rem 1rem 0" : "1.25rem 2rem 0",
          borderBottom: "1px solid rgba(255, 255, 255, 0.1)"
        }}
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "0.75rem",
            marginBottom: "1rem"
          }}
        >
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <h1
                style={{
                  margin: 0,
                  fontSize: isMobile ? "1.25rem" : "1.5rem",
                  fontWeight: 700,
                  color: "#ffffff",
                  letterSpacing: "-0.02em"
                }}
              >
                {t("title")}
              </h1>
              <span
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  padding: "0.15rem 0.5rem",
                  borderRadius: "9999px",
                  fontSize: "0.75rem",
                  fontWeight: 600,
                  backgroundColor: "rgba(255, 255, 255, 0.15)",
                  color: "#e2e8f0"
                }}
              >
                {t("badge")}
              </span>
            </div>
            <p
              style={{
                margin: "0.25rem 0 0",
                fontSize: isMobile ? "0.8125rem" : "0.875rem",
                color: "#94a3b8"
              }}
            >
              {t("subtitle")}
            </p>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                padding: "0.375rem 0.75rem",
                borderRadius: "0.375rem",
                backgroundColor: "rgba(255, 255, 255, 0.08)",
                fontSize: "0.8125rem",
                color: "#cbd5e1"
              }}
            >
              <span style={{ color: "#94a3b8" }}>{t("activeEntity")}:</span>
              <strong style={{ color: "#ffffff", direction: "ltr" }}>
                {runtimeContext?.name ?? t("defaultEntity")}
              </strong>
            </div>

            <button
              type="button"
              onClick={() => setIsHelpOpen(true)}
              style={{
                display: "inline-flex",
                alignItems: "center",
                justifyContent: "center",
                width: "2rem",
                height: "2rem",
                borderRadius: "0.375rem",
                backgroundColor: "rgba(255, 255, 255, 0.08)",
                border: "1px solid rgba(255, 255, 255, 0.15)",
                color: "#cbd5e1",
                cursor: "pointer"
              }}
              aria-label={t("help.title")}
            >
              <HelpCircleIcon size={18} />
            </button>
          </div>
        </div>

        {/* Navigation Tabs Bar */}
        <nav
          aria-label={t("title")}
          style={{
            display: "flex",
            gap: "0.25rem",
            overflowX: "auto",
            scrollbarWidth: "none"
          }}
        >
          {tabs.map((tab) => {
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                type="button"
                role="tab"
                aria-selected={isActive}
                onClick={() => handleTabChange(tab.id)}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "0.5rem",
                  padding: isMobile ? "0.625rem 0.75rem" : "0.75rem 1rem",
                  fontSize: isMobile ? "0.8125rem" : "0.875rem",
                  fontWeight: isActive ? 600 : 500,
                  color: isActive ? "#ffffff" : "#94a3b8",
                  backgroundColor: "transparent",
                  border: "none",
                  borderBottom: isActive ? "3px solid #38bdf8" : "3px solid transparent",
                  cursor: "pointer",
                  whiteSpace: "nowrap",
                  transition: "all 0.15s ease-in-out"
                }}
              >
                {tab.icon}
                <span>{tab.label}</span>
              </button>
            );
          })}
        </nav>
      </header>

      {/* Main Canvas Area */}
      <main
        style={{
          flex: 1,
          padding: isMobile ? "1rem" : "1.5rem 2rem",
          maxWidth: "1600px",
          width: "100%",
          margin: "0 auto",
          boxSizing: "border-box"
        }}
      >
        {activeTab === "overview" && (
          <ServiceOverviewTab legalEntityId={legalEntityId} onNavigateTab={handleTabChange} />
        )}
        {activeTab === "requests" && <ServiceRequestsTab legalEntityId={legalEntityId} />}
        {activeTab === "work-orders" && <WorkOrdersTab legalEntityId={legalEntityId} />}
        {activeTab === "dispatch" && <DispatchTab legalEntityId={legalEntityId} />}
        {activeTab === "my-day" && <MyDayTab legalEntityId={legalEntityId} />}
        {activeTab === "installed-base" && <InstalledBaseTab legalEntityId={legalEntityId} />}
        {activeTab === "preventive" && <PreventiveTab legalEntityId={legalEntityId} />}
        {activeTab === "contracts-warranty" && <ContractsWarrantyTab legalEntityId={legalEntityId} />}
        {activeTab === "billing" && <ServiceBillingTab legalEntityId={legalEntityId} />}
      </main>

      {/* Contextual Guidance Drawer */}
      <ServicePageHelp isOpen={isHelpOpen} onClose={() => setIsHelpOpen(false)} />
    </div>
  );
}
