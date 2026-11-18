import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useCreateForecastVersion } from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";

interface Props {
  readonly legalEntityId: string;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function CreateForecastVersionModal({ legalEntityId, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const createMutation = useCreateForecastVersion(legalEntityId);

  const today = new Date().toISOString().slice(0, 10);
  const [asOfDate, setAsOfDate] = useState(today);
  const [horizonDays, setHorizonDays] = useState("30");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    try {
      await createMutation.mutateAsync({
        legalEntityId,
        asOfDate: asOfDate || today,
        horizonDays: parseInt(horizonDays, 10) || 30
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("forecast.versionModalTitle")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "440px" }}>
        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        <TextField
          label={t("forecast.asOfDateLabel")}
          type="date"
          value={asOfDate}
          onChange={(val) => setAsOfDate(val)}
          isRequired
        />

        <div>
          <Select
            label={t("forecast.horizonLabel")}
            selectedKey={horizonDays}
            onSelectionChange={(key) => setHorizonDays(key ? String(key) : "30")}
            options={[
              { id: "7", label: t("forecast.days7") },
              { id: "14", label: t("forecast.days14") },
              { id: "30", label: t("forecast.days30") },
              { id: "60", label: t("forecast.days60") },
              { id: "90", label: t("forecast.days90") }
            ]}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending}>
            {createMutation.isPending ? t("common.loading") : t("common.confirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
