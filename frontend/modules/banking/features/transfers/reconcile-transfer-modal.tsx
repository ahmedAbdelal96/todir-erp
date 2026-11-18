import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useReconcileTransfer } from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import type { TransferReconciliationDto } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
  readonly transfer: TransferReconciliationDto | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function ReconcileTransferModal({ legalEntityId, transfer, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const reconcileMutation = useReconcileTransfer(legalEntityId);

  const [sourceStatementLineId, setSourceStatementLineId] = useState("");
  const [destStatementLineId, setDestStatementLineId] = useState("");
  const [explanation, setExplanation] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen || !transfer) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    try {
      await reconcileMutation.mutateAsync({
        transferId: transfer.id,
        request: {
          sourceStatementLineId: sourceStatementLineId.trim() || null,
          destinationStatementLineId: destStatementLineId.trim() || null,
          version: transfer.version,
          explanation: explanation.trim() || null
        }
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={`${t("transfers.reconcileTransferModalTitle")} — ${transfer.transferReference}`}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "480px" }}>
        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        <TextField
          label={t("transfers.sourceLineLabel")}
          value={sourceStatementLineId}
          onChange={(val) => setSourceStatementLineId(val)}
          placeholder={t("transfers.statementLinePlaceholder")}
        />

        <TextField
          label={t("transfers.destLineLabel")}
          value={destStatementLineId}
          onChange={(val) => setDestStatementLineId(val)}
          placeholder={t("transfers.statementLinePlaceholder")}
        />

        <TextField
          label={t("reconciliation.explanationLabel")}
          value={explanation}
          onChange={(val) => setExplanation(val)}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={reconcileMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={reconcileMutation.isPending}>
            {reconcileMutation.isPending ? t("common.loading") : t("transfers.reconcileSubmit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
