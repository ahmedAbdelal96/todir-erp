import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useNavigate } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { PosWorkspaceHeader } from "../components/pos-workspace-header.tsx";
import { PosCheckoutWorkspace } from "../features/checkout/pos-checkout-workspace.tsx";
import { useRegistersQuery, useCurrentSessionQuery } from "../hooks/use-pos-queries.ts";

export function PosCheckoutPage(): React.JSX.Element {
  const { t } = useTranslation("pos");
  const navigate = useNavigate();
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

  const { data: registersData, isLoading: isRegistersLoading } = useRegistersQuery(legalEntityId);
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
      data-testid="pos-checkout-page"
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
        title={t("nav.checkout")}
        subtitle={t("page.subtitle")}
        currentRegister={currentRegister}
        registers={registers}
        session={sessionData}
        onSelectRegister={handleSelectRegister}
      />

      {currentRegister && sessionData?.status === "Open" ? (
        <PosCheckoutWorkspace
          legalEntityId={legalEntityId}
          register={currentRegister}
          session={sessionData}
          onOpenSessionRequired={() => navigate("/pos/sessions")}
          onManageSessions={() => navigate("/pos/sessions")}
        />
      ) : isRegistersLoading ? (
        <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
          {t("common.loading", "جاري التحميل...")}
        </div>
      ) : !currentRegister ? (
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
          <Button variant="primary" onClick={() => navigate("/pos/registers")}>
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
          <Button variant="primary" onClick={() => navigate("/pos/sessions")}>
            {t("session.openNewSession")}
          </Button>
        </div>
      )}
    </div>
  );
}
