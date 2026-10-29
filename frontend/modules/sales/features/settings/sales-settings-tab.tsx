import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { SalesCustomerPicker } from "../../components/sales-customer-picker.tsx";
import { useSaveCustomerProfileMutation } from "../../hooks/use-sales-queries.ts";
import { mapSalesError } from "../../services/sales-error-map.ts";

export interface SalesSettingsTabProps {
  readonly legalEntityId: string;
}

export function SalesSettingsTab({ legalEntityId }: SalesSettingsTabProps): React.JSX.Element {
  const { t } = useTranslation("sales");
  const [selectedPartyId, setSelectedPartyId] = useState("");
  const [quoteValidityDays, setQuoteValidityDays] = useState("");
  const [poRequired, setPoRequired] = useState(false);

  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const saveMutation = useSaveCustomerProfileMutation(legalEntityId, selectedPartyId);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setStatusMessage(null);

    if (!selectedPartyId) {
      setErrorMessage(t("quotations.customerRequired"));
      return;
    }

    try {
      await saveMutation.mutateAsync({
        legalEntityId,
        partyId: selectedPartyId,
        defaultQuoteValidityDays: Number(quoteValidityDays) > 0 ? Number(quoteValidityDays) : null,
        customerPoRequired: poRequired
      });
      setStatusMessage(t("settings.savedSuccess"));
    } catch (err) {
      const mapped = mapSalesError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", maxWidth: "680px" }}>
      <div>
        <h3 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700 }}>
          {t("settings.title")}
        </h3>
        <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("settings.subtitle")}
        </p>
      </div>

      <div
        style={{
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 0.75rem)",
          padding: "1.5rem",
          display: "flex",
          flexDirection: "column",
          gap: "1.25rem",
          boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)"
        }}
      >
        <form onSubmit={handleSave} style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          {errorMessage && (
            <div
              role="alert"
              style={{
                padding: "0.75rem 1rem",
                backgroundColor: "var(--color-status-danger-subtle)",
                border: "1px solid var(--color-status-danger)",
                borderRadius: "var(--radius-md)",
                color: "var(--color-status-danger)",
                fontSize: "0.875rem"
              }}
            >
              {errorMessage}
            </div>
          )}

          {statusMessage && (
            <div
              role="status"
              style={{
                padding: "0.75rem 1rem",
                backgroundColor: "var(--color-status-success-subtle)",
                border: "1px solid var(--color-status-success)",
                borderRadius: "var(--radius-md)",
                color: "var(--color-status-success)",
                fontSize: "0.875rem"
              }}
            >
              {statusMessage}
            </div>
          )}

          <div>
            <SalesCustomerPicker
              value={selectedPartyId}
              onChange={setSelectedPartyId}
              isRequired
              label={t("settings.selectCustomerToConfigure")}
            />
          </div>

          <TextField
            label={t("settings.defaultQuoteValidityDays")}
            type="number"
            value={quoteValidityDays}
            onChange={setQuoteValidityDays}
          />

          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <input
              type="checkbox"
              id="po-required-check"
              checked={poRequired}
              onChange={(e) => setPoRequired(e.target.checked)}
              style={{ width: "1.125rem", height: "1.125rem", cursor: "pointer" }}
            />
            <label htmlFor="po-required-check" style={{ fontSize: "0.875rem", fontWeight: 500, cursor: "pointer" }}>
              {t("settings.customerPoRequired")}
            </label>
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "0.5rem" }}>
            <Button
              variant="primary"
              type="submit"
              isDisabled={saveMutation.isPending || !selectedPartyId}
            >
              {saveMutation.isPending ? t("common.saving") : t("common.save")}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
