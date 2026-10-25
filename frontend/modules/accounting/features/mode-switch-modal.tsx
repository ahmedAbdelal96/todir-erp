import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { useChangeLedgerMode } from "../hooks/use-ledger-queries.ts";
import { mapLedgerError } from "../services/ledger-error-map.ts";
import type { LedgerMode } from "../types.ts";

export interface ModeSwitchModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly currentMode: LedgerMode;
  readonly version: number;
}

export function ModeSwitchModal({
  isOpen,
  onClose,
  legalEntityId,
  currentMode,
  version
}: ModeSwitchModalProps): React.JSX.Element | null {
  const { t, i18n } = useTranslation("accounting");
  const changeModeMutation = useChangeLedgerMode();
  const [targetMode, setTargetMode] = useState<LedgerMode>(currentMode === "Managed" ? "Full" : "Managed");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const currentLang = i18n.language?.startsWith("ar") ? "ar" : "en";

  const handleConfirm = async () => {
    setErrorMessage(null);
    try {
      await changeModeMutation.mutateAsync({
        legalEntityId,
        mode: targetMode,
        version
      });
      onClose();
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("mode.modalTitle")}
      size="medium"
      footer={
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="primary"
            isLoading={changeModeMutation.isPending}
            onPress={handleConfirm}
          >
            {t("mode.confirmSwitch")}
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <p style={{ margin: 0, color: "var(--color-content-secondary)", fontSize: "0.875rem" }}>
          {t("mode.modalExplanation")}
        </p>

        <div
          style={{
            padding: "0.875rem 1rem",
            backgroundColor: "var(--color-surface-sunken)",
            borderRadius: "var(--radius-md, 0.5rem)",
            border: "1px solid var(--color-border-default)",
            fontSize: "0.875rem"
          }}
        >
          <strong>{t("mode.noteTitle")}:</strong> {t("mode.noteBody")}
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
          <label style={{ fontSize: "0.875rem", fontWeight: 600 }}>{t("mode.selectMode")}:</label>
          <div style={{ display: "flex", gap: "1rem" }}>
            <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", cursor: "pointer" }}>
              <input
                type="radio"
                name="ledger-mode"
                value="Managed"
                checked={targetMode === "Managed"}
                onChange={() => setTargetMode("Managed")}
              />
              <span>{t("mode.managed")}</span>
            </label>
            <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", cursor: "pointer" }}>
              <input
                type="radio"
                name="ledger-mode"
                value="Full"
                checked={targetMode === "Full"}
                onChange={() => setTargetMode("Full")}
              />
              <span>{t("mode.full")}</span>
            </label>
          </div>
        </div>

        {errorMessage && (
          <div
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}
      </div>
    </Dialog>
  );
}
