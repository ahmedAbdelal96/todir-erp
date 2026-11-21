import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import { PosWorkspaceHeader } from "../components/pos-workspace-header.tsx";
import { PosOfflineTab } from "../features/offline/pos-offline-tab.tsx";
import { useRegistersQuery, useCurrentSessionQuery } from "../hooks/use-pos-queries.ts";

export function PosOfflinePage(): React.JSX.Element {
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

  const { data: registersData } = useRegistersQuery(legalEntityId);
  const registers = registersData || [];
  const currentRegister = registers.find((r) => r.id === selectedRegisterId) || registers[0];

  const handleSelectRegister = (regId: string) => {
    setSelectedRegisterId(regId);
    if (typeof window !== "undefined") {
      window.sessionStorage.setItem("erp.pos.selectedRegisterId", regId);
    }
  };

  const { data: sessionData } = useCurrentSessionQuery(currentRegister?.id);

  if (!legalEntityId) {
    return (
      <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
        {t("common.loading", "جاري التحميل...")}
      </div>
    );
  }

  return (
    <div
      data-testid="pos-offline-page"
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
        title={t("nav.offline")}
        subtitle={t("offline.subtitle", "مزامنة العمليات غير المتصلة وإدارة طوابير التخزين المؤقت")}
        currentRegister={currentRegister}
        registers={registers}
        session={sessionData}
        onSelectRegister={handleSelectRegister}
      />

      <PosOfflineTab
        registerId={currentRegister?.id || ""}
      />
    </div>
  );
}
