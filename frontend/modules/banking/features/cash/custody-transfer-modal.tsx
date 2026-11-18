import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useTransferCustody } from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";

interface Props {
  readonly legalEntityId: string;
  readonly accountId: string;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function CustodyTransferModal({ legalEntityId, accountId, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const transferMutation = useTransferCustody(legalEntityId, accountId);

  const [toCustodianId, setToCustodianId] = useState("");
  const [reason, setReason] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!toCustodianId.trim() || !reason.trim()) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    try {
      await transferMutation.mutateAsync({
        toCustodianId: toCustodianId.trim(),
        reason: reason.trim()
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("cash.custodyModalTitle")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "460px" }}>
        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        <TextField
          label={t("cash.toCustodianLabel")}
          value={toCustodianId}
          onChange={(val) => setToCustodianId(val)}
          placeholder={t("cash.custodianPlaceholder")}
          isRequired
          autoFocus
        />

        <TextField
          label={t("cash.reasonLabel")}
          value={reason}
          onChange={(val) => setReason(val)}
          placeholder={t("cash.reasonPlaceholder")}
          isRequired
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={transferMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={transferMutation.isPending}>
            {transferMutation.isPending ? t("common.loading") : t("cash.custodySubmit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
