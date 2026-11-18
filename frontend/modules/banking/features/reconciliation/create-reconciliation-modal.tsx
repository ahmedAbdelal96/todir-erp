import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useCreateReconciliation,
  useTreasuryAccounts,
  useBankStatements
} from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";

interface Props {
  readonly legalEntityId: string;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function CreateReconciliationModal({ legalEntityId, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const createMutation = useCreateReconciliation(legalEntityId);

  const { data: accountsData } = useTreasuryAccounts(legalEntityId);
  const accounts = accountsData?.items ?? [];

  const [accountId, setAccountId] = useState("");
  const activeAccountId = accountId || (accounts.length > 0 ? (accounts[0]?.id ?? "") : "");

  const { data: statementsData } = useBankStatements(legalEntityId, activeAccountId);
  const statements = statementsData?.items ?? [];

  const [statementId, setStatementId] = useState("");
  const activeStatementId = statementId || (statements.length > 0 ? (statements[0]?.id ?? "") : "");

  const today = new Date().toISOString().split("T")[0];
  const [asOfDate, setAsOfDate] = useState(today);
  const [notes, setNotes] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!activeStatementId || !asOfDate) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        legalEntityId,
        statementId: activeStatementId,
        asOfDate,
        notes: notes.trim() || null
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("reconciliation.createModalTitle")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "480px" }}>
        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        <div>
          <Select
            label={t("statements.selectAccount")}
            selectedKey={activeAccountId}
            onSelectionChange={(key) => {
              setAccountId(key ? String(key) : "");
              setStatementId("");
            }}
            options={accounts.map((acc) => ({
              id: acc.id,
              label: `${acc.name} (${acc.code})`
            }))}
          />
        </div>

        <div>
          <Select
            label={t("reconciliation.statementSelectLabel")}
            selectedKey={activeStatementId}
            onSelectionChange={(key) => setStatementId(key ? String(key) : "")}
            options={statements.map((s) => ({
              id: s.id,
              label: `${s.statementNumber} (${s.currencyCode}) - ${t("statements.lineCount")}: ${s.lineCount}`
            }))}
          />
        </div>

        <TextField
          label={t("reconciliation.asOfDateLabel")}
          type="date"
          value={asOfDate}
          onChange={(val) => setAsOfDate(val)}
          isRequired
        />

        <TextField
          label={t("reconciliation.notesLabel")}
          value={notes}
          onChange={(val) => setNotes(val)}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending || !activeStatementId}>
            {createMutation.isPending ? t("common.loading") : t("reconciliation.createSubmit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
