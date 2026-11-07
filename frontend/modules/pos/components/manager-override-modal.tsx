import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { ShieldKeyIcon } from "./pos-icons.tsx";

export interface ManagerOverrideModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly scope: string;
  readonly targetId: string;
  readonly onAuthorize: (reason: string) => Promise<void>;
  readonly isSubmitting?: boolean | undefined;
}

export function ManagerOverrideModal({
  isOpen,
  onClose,
  scope,
  onAuthorize,
  isSubmitting = false,
}: ManagerOverrideModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pos");
  const [reason, setReason] = useState<string>("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      setError(t("override.reasonRequired"));
      return;
    }
    setError(null);
    try {
      await onAuthorize(reason.trim());
      setReason("");
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : t("override.authFailed"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("override.title")}
      description={t("override.subtitle")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: "0.75rem",
            padding: "0.75rem 1rem",
            borderRadius: "var(--radius-md)",
            background: "rgba(239, 68, 68, 0.08)",
            border: "1px solid rgba(239, 68, 68, 0.2)",
          }}
        >
          <ShieldKeyIcon size={24} style={{ color: "var(--color-status-danger)", flexShrink: 0 }} />
          <div style={{ fontSize: "0.875rem", color: "var(--color-content-default)" }}>
            <strong>{t("override.protectedScope")}:</strong> {scope}
          </div>
        </div>

        <TextField
          label={t("override.reasonLabel")}
          placeholder={t("override.reasonPlaceholder")}
          value={reason}
          onChange={(v) => {
            setReason(v);
            setError(null);
          }}
          autoFocus
        />

        {error && (
          <div style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }} role="alert">
            {error}
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onClick={onClose} isDisabled={isSubmitting}>
            {t("common.cancel")}
          </Button>
          <Button variant="danger" type="submit" isDisabled={isSubmitting || !reason.trim()}>
            {isSubmitting ? t("common.submitting") : t("override.authorizeAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
