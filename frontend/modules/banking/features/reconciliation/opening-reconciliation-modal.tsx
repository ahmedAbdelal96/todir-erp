import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useCreateOpeningReconciliation,
  useTreasuryAccounts
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

export function OpeningReconciliationModal({ legalEntityId, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const createMutation = useCreateOpeningReconciliation(legalEntityId);

  const { data: accountsData } = useTreasuryAccounts(legalEntityId, { pageSize: 100 });
  const accounts = accountsData?.items ?? [];

  const [accountId, setAccountId] = useState("");
  const activeAccountId = accountId || (accounts.length > 0 ? (accounts[0]?.id ?? "") : "");

  const today = new Date().toISOString().slice(0, 10);
  const [openingDate, setOpeningDate] = useState(today);
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [bookBalance, setBookBalance] = useState("100000");
  const [externalBalance, setExternalBalance] = useState("100000");
  const [inTransitAmount, setInTransitAmount] = useState("0");
  const [ledgerBatchId, setLedgerBatchId] = useState("");
  const [sourceSystem, setSourceSystem] = useState("LegacyERP");
  const [batchId] = useState(`BATCH-INIT-${Date.now()}`);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!activeAccountId || !ledgerBatchId.trim()) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        legalEntityId,
        treasuryAccountId: activeAccountId,
        ledgerOpeningBatchId: ledgerBatchId.trim(),
        openingAsOfDate: openingDate,
        currencyCode: currencyCode.trim(),
        bookOpeningBalance: parseFloat(bookBalance) || 0,
        externalOpeningBalance: parseFloat(externalBalance) || 0,
        outstandingInTransitAmount: parseFloat(inTransitAmount) || 0,
        sourceSystem: sourceSystem.trim(),
        batchId: batchId.trim()
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("reconciliation.newOpeningButton")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "500px" }}>
        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        <div>
          <Select
            label={t("statements.selectAccount")}
            selectedKey={activeAccountId}
            onSelectionChange={(key) => setAccountId(key ? String(key) : "")}
            options={accounts.map((acc) => ({
              id: acc.id,
              label: `${acc.name} (${acc.code})`
            }))}
          />
        </div>

        <TextField
          label={t("reconciliation.ledgerBatch")}
          value={ledgerBatchId}
          onChange={(val) => setLedgerBatchId(val)}
          placeholder={t("reconciliation.ledgerBatchPlaceholder")}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("reconciliation.asOfDateLabel")}
            type="date"
            value={openingDate}
            onChange={(val) => setOpeningDate(val)}
            isRequired
          />
          <TextField
            label={t("statements.currencyLabel")}
            value={currencyCode}
            onChange={(val) => setCurrencyCode(val)}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("reconciliation.bookBalance")}
            type="number"
            value={bookBalance}
            onChange={(val) => setBookBalance(val)}
            isRequired
          />
          <TextField
            label={t("reconciliation.externalBalance")}
            type="number"
            value={externalBalance}
            onChange={(val) => setExternalBalance(val)}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("overview.totalInTransit")}
            type="number"
            value={inTransitAmount}
            onChange={(val) => setInTransitAmount(val)}
          />
          <TextField
            label={t("reconciliation.sourceSystemLabel")}
            value={sourceSystem}
            onChange={(val) => setSourceSystem(val)}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending || !activeAccountId}>
            {createMutation.isPending ? t("common.loading") : t("common.confirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
