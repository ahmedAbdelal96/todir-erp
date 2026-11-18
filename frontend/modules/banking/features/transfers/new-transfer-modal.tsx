import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useCreateTransfer,
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

export function NewTransferModal({ legalEntityId, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const createMutation = useCreateTransfer(legalEntityId);

  const { data: accountsData } = useTreasuryAccounts(legalEntityId);
  const accounts = accountsData?.items ?? [];

  const [sourceAccountId, setSourceAccountId] = useState("");
  const [destAccountId, setDestAccountId] = useState("");
  const [amount, setAmount] = useState("10000");
  const [currencyCode, setCurrencyCode] = useState("EGP");

  const today = new Date().toISOString().slice(0, 10);
  const [paymentDate, setPaymentDate] = useState(today);
  const [postingDate, setPostingDate] = useState(today);
  const [reference, setReference] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!sourceAccountId || !destAccountId || sourceAccountId === destAccountId) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        request: {
          legalEntityId,
          sourceTreasuryAccountId: sourceAccountId,
          destinationTreasuryAccountId: destAccountId,
          amount: parseFloat(amount) || 0,
          currencyCode: currencyCode.trim(),
          paymentDate: paymentDate || today,
          postingDate: postingDate || today,
          reference: reference.trim() || null
        }
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("transfers.newTransferModalTitle")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "480px" }}>
        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        <div>
          <Select
            label={t("transfers.sourceAccountLabel")}
            selectedKey={sourceAccountId}
            onSelectionChange={(key) => setSourceAccountId(key ? String(key) : "")}
            options={accounts.map((acc) => ({
              id: acc.id,
              label: `${acc.name} (${acc.code}) - ${acc.currencyCode || "EGP"}`
            }))}
          />
        </div>

        <div>
          <Select
            label={t("transfers.destinationAccountLabel")}
            selectedKey={destAccountId}
            onSelectionChange={(key) => setDestAccountId(key ? String(key) : "")}
            options={accounts.map((acc) => ({
              id: acc.id,
              label: `${acc.name} (${acc.code}) - ${acc.currencyCode || "EGP"}`
            }))}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("transfers.amountLabel")}
            type="number"
            value={amount}
            onChange={(val) => setAmount(val)}
            isRequired
          />
          <TextField
            label={t("transfers.currencyLabel")}
            value={currencyCode}
            onChange={(val) => setCurrencyCode(val)}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("transfers.paymentDateLabel")}
            type="date"
            value={paymentDate}
            onChange={(val) => setPaymentDate(val)}
            isRequired
          />
          <TextField
            label={t("transfers.postingDateLabel")}
            type="date"
            value={postingDate}
            onChange={(val) => setPostingDate(val)}
            isRequired
          />
        </div>

        <TextField
          label={t("transfers.referenceLabel")}
          value={reference}
          onChange={(val) => setReference(val)}
          placeholder={t("transfers.referencePlaceholder")}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending || !sourceAccountId || !destAccountId}>
            {createMutation.isPending ? t("common.loading") : t("transfers.transferSubmit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
