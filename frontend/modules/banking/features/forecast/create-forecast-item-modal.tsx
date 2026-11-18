import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useCreateForecastItem } from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";

interface Props {
  readonly legalEntityId: string;
  readonly forecastVersionId: string;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function CreateForecastItemModal({ legalEntityId, forecastVersionId, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const createMutation = useCreateForecastItem(legalEntityId);

  const today = new Date().toISOString().slice(0, 10);
  const [dueDate, setDueDate] = useState(today);
  const [direction, setDirection] = useState<"Inflow" | "Outflow">("Inflow");
  const [amount, setAmount] = useState("25000");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [sourceType, setSourceType] = useState("CustomerContract");
  const [description, setDescription] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!description.trim()) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        legalEntityId,
        forecastVersionId,
        dueDate: dueDate || today,
        direction,
        amount: parseFloat(amount) || 0,
        currencyCode: currencyCode.trim(),
        sourceType: sourceType.trim(),
        description: description.trim()
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("forecast.itemModalTitle")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "480px" }}>
        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("forecast.dueDate")}
            type="date"
            value={dueDate}
            onChange={(val) => setDueDate(val)}
            isRequired
          />
          <div>
            <Select
              label={t("forecast.direction")}
              selectedKey={direction}
              onSelectionChange={(key) => setDirection((key as "Inflow" | "Outflow") || "Inflow")}
              options={[
                { id: "Inflow", label: t("forecast.inflow") },
                { id: "Outflow", label: t("forecast.outflow") }
              ]}
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("forecast.amount")}
            type="number"
            value={amount}
            onChange={(val) => setAmount(val)}
            isRequired
          />
          <TextField
            label={t("forecast.currency")}
            value={currencyCode}
            onChange={(val) => setCurrencyCode(val)}
            isRequired
          />
        </div>

        <TextField
          label={t("forecast.sourceType")}
          value={sourceType}
          onChange={(val) => setSourceType(val)}
          isRequired
        />

        <TextField
          label={t("forecast.description")}
          value={description}
          onChange={(val) => setDescription(val)}
          isRequired
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending}>
            {createMutation.isPending ? t("common.loading") : t("forecast.saveItem")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
