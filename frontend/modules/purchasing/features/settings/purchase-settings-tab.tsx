import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { usePurchasingSettings, useSavePurchasingSettingsMutation } from "../../hooks/use-purchasing-queries.ts";

export interface PurchaseSettingsTabProps {
  readonly legalEntityId: string;
  readonly isAdvanced?: boolean | undefined;
}

export function PurchaseSettingsTab({ legalEntityId, isAdvanced = false }: PurchaseSettingsTabProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");

  const [approvalRequired, setApprovalRequired] = useState(false);
  const [approvalThreshold, setApprovalThreshold] = useState<number>(10000);
  const [overReceiptPolicy, setOverReceiptPolicy] = useState<string>("Disallow");
  const [overReceiptTolerance, setOverReceiptTolerance] = useState<number>(0);
  const [invoiceWithoutReceipt, setInvoiceWithoutReceipt] = useState(true);
  const [threeWayMatching, setThreeWayMatching] = useState(false);
  const [invoiceEligibilityBasis, setInvoiceEligibilityBasis] = useState<string>("PO");
  const [savedSuccess, setSavedSuccess] = useState(false);

  const { data: settings } = usePurchasingSettings(legalEntityId);
  const saveMutation = useSavePurchasingSettingsMutation(legalEntityId);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    await saveMutation.mutateAsync({
      legalEntityId,
      approvalRequired,
      approvalThreshold,
      overReceiptPolicy,
      overReceiptTolerance,
      invoiceWithoutReceipt,
      threeWayMatching,
      buyerApproverSeparation: false,
      quantityTolerance: 0,
      priceAbsoluteTolerance: 10,
      pricePercentageTolerance: 1.0,
      taxTolerance: 5,
      invoiceEligibilityBasis
    });
    setSavedSuccess(true);
    setTimeout(() => setSavedSuccess(false), 3000);
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", maxWidth: "44rem" }}>
      <div>
        <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
          {t("settings.title")}
        </h2>
        <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
          {t("settings.subtitle")}
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
          {/* Approval Settings */}
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", cursor: "pointer", fontSize: "0.875rem", fontWeight: 600 }}>
              <input
                type="checkbox"
                checked={approvalRequired}
                onChange={(e) => setApprovalRequired(e.target.checked)}
              />
              <span>{t("settings.approvalRequired")}</span>
            </label>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {t("settings.approvalRequiredNotice")}
            </span>
          </div>

          {approvalRequired && (
            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
                {t("settings.approvalThreshold")} (SAR)
              </label>
              <input
                type="number"
                value={approvalThreshold}
                onChange={(e) => setApprovalThreshold(parseFloat(e.target.value) || 0)}
                style={{
                  width: "12rem",
                  padding: "0.5rem",
                  borderRadius: "var(--radius-sm)",
                  border: "1px solid var(--color-border-default)",
                  backgroundColor: "var(--color-surface-card)"
                }}
              />
            </div>
          )}

          {/* Over-receipt Policy */}
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600 }}>
              {t("settings.overReceiptPolicy")}
            </label>
            <select
              value={overReceiptPolicy}
              onChange={(e) => setOverReceiptPolicy(e.target.value)}
              style={{
                width: "100%",
                padding: "0.5rem",
                borderRadius: "var(--radius-sm)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: "var(--color-surface-card)"
              }}
            >
              <option value="Disallow">{t("settings.disallowOverReceipt")}</option>
              <option value="Tolerance">{t("settings.toleranceOverReceipt")}</option>
              <option value="Approval">{t("settings.approvalOverReceipt")}</option>
            </select>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {t("settings.overReceiptPolicyDesc")}
            </span>
          </div>

          {/* Invoice Eligibility */}
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600 }}>
              {t("settings.invoiceEligibilityBasis")}
            </label>
            <select
              value={invoiceEligibilityBasis}
              onChange={(e) => setInvoiceEligibilityBasis(e.target.value)}
              style={{
                width: "100%",
                padding: "0.5rem",
                borderRadius: "var(--radius-sm)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: "var(--color-surface-card)"
              }}
            >
              <option value="PO">{t("settings.basisPO")}</option>
              <option value="Receipt">{t("settings.basisReceipt")}</option>
            </select>
          </div>

          {/* Advanced Toggles */}
          {isAdvanced && (
            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
              <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", cursor: "pointer", fontSize: "0.875rem" }}>
                <input
                  type="checkbox"
                  checked={threeWayMatching}
                  onChange={(e) => setThreeWayMatching(e.target.checked)}
                />
                <span>{t("settings.threeWayMatching")}</span>
              </label>

              <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", cursor: "pointer", fontSize: "0.875rem" }}>
                <input
                  type="checkbox"
                  checked={invoiceWithoutReceipt}
                  onChange={(e) => setInvoiceWithoutReceipt(e.target.checked)}
                />
                <span>{t("settings.invoiceWithoutReceipt")}</span>
              </label>
            </div>
          )}

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
