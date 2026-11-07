import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";
import {
  CashRegisterIcon,
  DrawerIcon,
  ReturnIcon,
  OfflineIcon,
} from "../components/pos-icons.tsx";
import { PosCheckoutWorkspace } from "../features/checkout/pos-checkout-workspace.tsx";
import { PosSessionsTab } from "../features/sessions/pos-sessions-tab.tsx";
import { PosRegistersTab } from "../features/registers/pos-registers-tab.tsx";
import { PosReturnsTab } from "../features/returns/pos-returns-tab.tsx";
import { PosOfflineTab } from "../features/offline/pos-offline-tab.tsx";
import { PosReportsTab } from "../features/reports/pos-reports-tab.tsx";
import { useRegistersQuery, useCurrentSessionQuery } from "../hooks/use-pos-queries.ts";

export type PosTab = "checkout" | "sessions" | "registers" | "returns" | "offline" | "reports";

export function PosPage(): React.JSX.Element {
  const { t } = useTranslation("pos");
  const outletContext = useOutletContext<WorkspaceOutletContext | undefined>();
  const legalEntityId = outletContext?.legalEntityId || "00000000-0000-0000-0000-000000000000";

  const [activeTab, setActiveTab] = useState<PosTab>("checkout");

  // Query registers
  const { data: registersData, refetch: refetchRegisters, isLoading: isRegistersLoading } = useRegistersQuery(legalEntityId);
  const registers = registersData || [];
  const defaultRegister = registers[0];

  // Query active session for register
  const { data: sessionData, refetch: refetchSession } = useCurrentSessionQuery(defaultRegister?.id);

  const handleSessionChanged = () => {
    refetchSession();
    refetchRegisters();
  };

  return (
    <div
      data-testid="pos-page"
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.25rem",
        padding: "1.25rem",
        maxWidth: "96rem",
        margin: "0 auto",
        width: "100%",
        boxSizing: "border-box",
        backgroundColor: "#f0f4f8",
        minHeight: "100%",
      }}
    >
      {/* Oxford Navy Header */}
      <header
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          backgroundColor: "#102a43",
          borderRadius: "var(--radius-lg, 0.75rem)",
          padding: "1rem 1.5rem",
          color: "#ffffff",
          boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -2px rgba(0, 0, 0, 0.1)",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
          <div
            style={{
              padding: "0.625rem",
              backgroundColor: "rgba(255, 255, 255, 0.12)",
              borderRadius: "var(--radius-md, 0.5rem)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              border: "1px solid rgba(255, 255, 255, 0.2)",
            }}
          >
            <CashRegisterIcon size={24} />
          </div>
          <div>
            <h1 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 800, color: "#ffffff", letterSpacing: "-0.01em" }}>
              {t("page.title")}
            </h1>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "#94a3b8" }}>
              {t("page.subtitle")}
            </p>
          </div>
        </div>

        {defaultRegister && (
          <div
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: "0.75rem",
              fontSize: "0.8125rem",
              color: "#e2e8f0",
              backgroundColor: "rgba(255, 255, 255, 0.08)",
              padding: "0.4rem 0.85rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "1px solid rgba(255, 255, 255, 0.15)",
            }}
          >
            <span>{defaultRegister.name} ({defaultRegister.code})</span>
            <span style={{ color: "#94a3b8" }}>•</span>
            <span style={{ color: sessionData?.status === "Open" ? "#4ade80" : "#fbbf24", fontWeight: 600 }}>
              {sessionData?.status === "Open" ? t("workspace.online") : t("page.noActiveSessionTitle")}
            </span>
          </div>
        )}
      </header>

      {/* Navigation & Tab Selection Bar */}
      <div
        role="tablist"
        aria-label={t("page.title")}
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          backgroundColor: "#ffffff",
          borderRadius: "var(--radius-lg)",
          border: "1px solid #cbd5e1",
          padding: "0.5rem 0.75rem",
          boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
        }}
      >
        <div style={{ display: "flex", gap: "0.35rem", flexWrap: "wrap" }}>
          <Button
            variant={activeTab === "checkout" ? "primary" : "ghost"}
            size="compact"
            onClick={() => setActiveTab("checkout")}
            style={{ display: "flex", gap: "0.35rem", alignItems: "center" }}
          >
            <CashRegisterIcon size={16} />
            <span>{t("nav.checkout")}</span>
          </Button>

          <Button
            variant={activeTab === "sessions" ? "primary" : "ghost"}
            size="compact"
            onClick={() => setActiveTab("sessions")}
            style={{ display: "flex", gap: "0.35rem", alignItems: "center" }}
          >
            <DrawerIcon size={16} />
            <span>{t("nav.sessions")}</span>
          </Button>

          <Button
            variant={activeTab === "registers" ? "primary" : "ghost"}
            size="compact"
            onClick={() => setActiveTab("registers")}
          >
            {t("nav.registers")}
          </Button>

          <Button
            variant={activeTab === "returns" ? "primary" : "ghost"}
            size="compact"
            onClick={() => setActiveTab("returns")}
            style={{ display: "flex", gap: "0.35rem", alignItems: "center" }}
          >
            <ReturnIcon size={16} />
            <span>{t("nav.returns")}</span>
          </Button>

          <Button
            variant={activeTab === "offline" ? "primary" : "ghost"}
            size="compact"
            onClick={() => setActiveTab("offline")}
            style={{ display: "flex", gap: "0.35rem", alignItems: "center" }}
          >
            <OfflineIcon size={16} />
            <span>{t("nav.offline")}</span>
          </Button>

          <Button
            variant={activeTab === "reports" ? "primary" : "ghost"}
            size="compact"
            onClick={() => setActiveTab("reports")}
          >
            {t("nav.reports")}
          </Button>
        </div>

        {/* Store / Entity indicator */}
        <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", paddingInlineEnd: "0.5rem" }}>
          <span>{t("page.legalEntity")}: </span>
          <strong style={{ color: "var(--color-content-default)" }}>
            <BidiText>{legalEntityId.slice(0, 8)}</BidiText>
          </strong>
        </div>
      </div>

      {/* Main Tab Content */}
      {activeTab === "checkout" && (
        defaultRegister && sessionData ? (
          <PosCheckoutWorkspace
            legalEntityId={legalEntityId}
            register={defaultRegister}
            session={sessionData}
            onOpenSessionRequired={() => setActiveTab("sessions")}
            onManageSessions={() => setActiveTab("sessions")}
          />
        ) : isRegistersLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
          </div>
        ) : !defaultRegister ? (
          <div
            style={{
              padding: "3rem",
              textAlign: "center",
              backgroundColor: "#ffffff",
              borderRadius: "var(--radius-lg)",
              border: "1px dashed #cbd5e1",
            }}
          >
            <h3>{t("page.noRegisterFound")}</h3>
            <p style={{ color: "var(--color-content-muted)", marginBottom: "1.5rem" }}>
              {t("page.noRegisterFoundPrompt")}
            </p>
            <Button variant="primary" onClick={() => setActiveTab("registers")}>
              {t("registers.newRegisterAction")}
            </Button>
          </div>
        ) : (
          <div
            style={{
              padding: "3rem",
              textAlign: "center",
              backgroundColor: "#ffffff",
              borderRadius: "var(--radius-lg)",
              border: "1px dashed #cbd5e1",
            }}
          >
            <h3>{t("page.noActiveSessionTitle")}</h3>
            <p style={{ color: "var(--color-content-muted)", marginBottom: "1.5rem" }}>
              {t("page.noActiveSessionPrompt")}
            </p>
            <Button variant="primary" onClick={() => setActiveTab("sessions")}>
              {t("session.openNewSession")}
            </Button>
          </div>
        )
      )}

      {activeTab === "sessions" && (
        <PosSessionsTab
          legalEntityId={legalEntityId}
          registers={registers}
          currentRegister={defaultRegister}
          activeSession={sessionData ?? null}
          onSessionChanged={handleSessionChanged}
        />
      )}

      {activeTab === "registers" && (
        <PosRegistersTab
          legalEntityId={legalEntityId}
          registers={registers}
          onRefetch={refetchRegisters}
        />
      )}

      {activeTab === "returns" && (
        <PosReturnsTab
          legalEntityId={legalEntityId}
          activeSession={sessionData ?? null}
        />
      )}

      {activeTab === "offline" && (
        <PosOfflineTab
          registerId={defaultRegister?.id || "00000000-0000-0000-0000-000000000000"}
        />
      )}

      {activeTab === "reports" && (
        <PosReportsTab
          legalEntityId={legalEntityId}
        />
      )}
    </div>
  );
}
