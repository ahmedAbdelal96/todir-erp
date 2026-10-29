import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { PurchasingSupplierPicker } from "../../components/purchasing-supplier-picker.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useSupplierProfile, useSaveSupplierProfileMutation } from "../../hooks/use-purchasing-queries.ts";

export interface SupplierProfileTabProps {
  readonly legalEntityId: string;
}

export function SupplierProfileTab({ legalEntityId }: SupplierProfileTabProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const [selectedSupplierId, setSelectedSupplierId] = useState<string>("party-supplier-delta");
  const [currency, setCurrency] = useState("SAR");
  const [leadTime, setLeadTime] = useState<number>(7);
  const [supplierRefRequired, setSupplierRefRequired] = useState(true);
  const [savedSuccess, setSavedSuccess] = useState(false);

  const { data: profile, isLoading } = useSupplierProfile(legalEntityId, selectedSupplierId);
  const saveMutation = useSaveSupplierProfileMutation(legalEntityId, selectedSupplierId);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    await saveMutation.mutateAsync({
      legalEntityId,
      supplierPartyId: selectedSupplierId,
      defaultCurrencyCode: currency,
      leadTimeDays: leadTime,
      supplierReferenceRequired: supplierRefRequired
    });
    setSavedSuccess(true);
    setTimeout(() => setSavedSuccess(false), 3000);
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", maxWidth: "44rem" }}>
      <div>
        <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
          {t("suppliers.profileTitle")}
        </h2>
        <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
          {t("suppliers.profileSubtitle")}
        </p>
      </div>

      <div
        style={{
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 0.75rem)",
          padding: "1.5rem"
        }}
      >
        <form onSubmit={handleSave} style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          <div>
            <PurchasingSupplierPicker
              value={selectedSupplierId}
              onChange={setSelectedSupplierId}
              isRequired
              label={t("suppliers.selectedSupplier")}
            />
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
                {t("suppliers.defaultCurrency")}
              </label>
              <select
                value={currency}
                onChange={(e) => setCurrency(e.target.value)}
                style={{
                  width: "100%",
                  padding: "0.5rem",
                  borderRadius: "var(--radius-sm)",
                  border: "1px solid var(--color-border-default)",
                  backgroundColor: "var(--color-surface-card)"
                }}
              >
                <option value="SAR">{t("suppliers.currencies.SAR")}</option>
                <option value="EGP">{t("suppliers.currencies.EGP")}</option>
                <option value="USD">{t("suppliers.currencies.USD")}</option>
              </select>
            </div>

            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
                {t("suppliers.leadTimeDays")}
              </label>
              <input
                type="number"
                min="0"
                value={leadTime}
                onChange={(e) => setLeadTime(parseInt(e.target.value, 10) || 0)}
                style={{
                  width: "100%",
                  padding: "0.5rem",
                  borderRadius: "var(--radius-sm)",
                  border: "1px solid var(--color-border-default)",
                  backgroundColor: "var(--color-surface-card)"
                }}
              />
            </div>
          </div>

          <div>
            <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", cursor: "pointer", fontSize: "0.875rem" }}>
              <input
                type="checkbox"
                checked={supplierRefRequired}
                onChange={(e) => setSupplierRefRequired(e.target.checked)}
              />
              <span>{t("suppliers.supplierRefRequired")}</span>
            </label>
          </div>

          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
            {savedSuccess ? (
              <span style={{ color: "var(--color-status-success)", fontSize: "0.875rem", fontWeight: 600 }}>
                ✓ {t("common.savedSuccessfully")}
              </span>
            ) : <span />}
            <Button variant="primary" type="submit">
              {t("common.save")}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
