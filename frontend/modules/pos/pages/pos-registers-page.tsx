import React from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import { PosWorkspaceHeader } from "../components/pos-workspace-header.tsx";
import { PosRegistersTab } from "../features/registers/pos-registers-tab.tsx";
import { useRegistersQuery } from "../hooks/use-pos-queries.ts";

export function PosRegistersPage(): React.JSX.Element {
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

  const { data: registersData, refetch: refetchRegisters } = useRegistersQuery(legalEntityId);
  const registers = registersData || [];

  if (!legalEntityId) {
    return (
      <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
        {t("common.loading", "جاري التحميل...")}
      </div>
    );
  }

  return (
    <div
      data-testid="pos-registers-page"
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
        title={t("nav.registers")}
        subtitle={t("registers.subtitle", "إدارة وتكوين نقاط البيع وأجهزة الدفع الطرفية")}
        registers={registers}
      />

      <PosRegistersTab
        legalEntityId={legalEntityId}
        registers={registers}
        onRefetch={refetchRegisters}
      />
    </div>
  );
}
