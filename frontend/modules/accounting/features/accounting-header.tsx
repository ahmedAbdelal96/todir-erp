import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { LedgerModeBadge } from "../components/ledger-status-badges.tsx";
import { ModeSwitchModal } from "./mode-switch-modal.tsx";
import type { LedgerMode } from "../types.ts";

export interface AccountingHeaderProps {
  readonly legalEntityId: string;
  readonly mode?: LedgerMode | undefined;
  readonly modeVersion?: number | undefined;
  readonly canManageConfig: boolean;
}

export function AccountingHeader({
  legalEntityId,
  mode = "Managed",
  modeVersion = 1,
  canManageConfig
}: AccountingHeaderProps): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const [isModeModalOpen, setIsModeModalOpen] = useState(false);

  return (
    <Surface
      style={{
        padding: "1.25rem 1.5rem",
        marginBottom: "1.5rem",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        flexWrap: "wrap",
        gap: "1rem"
      }}
    >
      <div>
        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
          <h1 style={{ margin: 0, fontSize: "1.5rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("title")}
          </h1>
          <LedgerModeBadge mode={mode} />
        </div>
        <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
          {mode === "Managed" ? t("mode.managedDescription") : t("mode.fullDescription")}
        </p>
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
        {canManageConfig && (
          <Button variant="secondary" size="compact" onPress={() => setIsModeModalOpen(true)}>
            {t("mode.switchMode")}
          </Button>
        )}
      </div>

      <ModeSwitchModal
        isOpen={isModeModalOpen}
        onClose={() => setIsModeModalOpen(false)}
        legalEntityId={legalEntityId}
        currentMode={mode}
        version={modeVersion}
      />
    </Surface>
  );
}
