import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import { PosWorkspaceHeader } from "../components/pos-workspace-header.tsx";
import { PosSessionsTab } from "../features/sessions/pos-sessions-tab.tsx";
import { useRegistersQuery, useCurrentSessionQuery } from "../hooks/use-pos-queries.ts";

export function PosSessionsPage(): React.JSX.Element {
  const { t } = useTranslation("pos");
  const outletContext = useOutletContext<WorkspaceOutletContext | undefined>();

  let contextEntityId: string | undefined;
  try {
    const context = useLegalEntityContext();
    contextEntityId = context?.legalEntityId;
  } catch {
    // outside provider
  }
  const legalEntityId =
    outletContext?.legalEntityId ||
    contextEntityId ||
    (typeof window !== "undefined" ? window.sessionStorage.getItem("erp.current.legalEntityId") || "" : "");

  const [selectedRegisterId, setSelectedRegisterId] = useState<string>(() => {
    return typeof window !== "undefined" ? window.sessionStorage.getItem("erp.pos.selectedRegisterId") || "" : "";
  });

  const { data: registersData, refetch: refetchRegisters } = useRegistersQuery(legalEntityId);
  const registers = registersData || [];
  const currentRegister = registers.find((r) => r.id === selectedRegisterId) || registers[0];

  const handleSelectRegister = (regId: string) => {
    setSelectedRegisterId(regId);
    if (typeof window !== "undefined") {
      window.sessionStorage.setItem("erp.pos.selectedRegisterId", regId);
    }
  };

  const { data: sessionData, refetch: refetchSession } = useCurrentSessionQuery(currentRegister?.id);

  const handleSessionChanged = () => {
    refetchSession();
    refetchRegisters();
  };

  if (!legalEntityId) {
    return (
      <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
        {t("common.loading", "جاري التحميل...")}
      </div>
    );
  }

  return (
    <div
      data-testid="pos-sessions-page"
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
      <PosWorkspaceHeader
        title={t("nav.sessions")}
        subtitle={t("session.managementSubtitle", "إدارة جلسات الصناديق وتسوية العهد وحركات النقدية")}
        currentRegister={currentRegister}
        registers={registers}
        session={sessionData}
        onSelectRegister={handleSelectRegister}
      />

      <PosSessionsTab
        legalEntityId={legalEntityId}
        registers={registers}
        currentRegister={currentRegister}
        activeSession={sessionData ?? null}
        onSessionChanged={handleSessionChanged}
      />
    </div>
  );
}
