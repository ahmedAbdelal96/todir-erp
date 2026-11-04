import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import {
  usePayrollStatutorySettings,
  useUpdateStatutorySettingsMutation
} from "../../hooks/use-payroll-queries.ts";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { StatutoryHistoryModal } from "./statutory-history-modal.tsx";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { StatutoryMode, ManualInputPolicy } from "../../types.ts";

export interface StatutorySettingsTabProps {
  readonly legalEntityId: string;
}

export function StatutorySettingsTab({
  legalEntityId
}: StatutorySettingsTabProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const { data: currentSettings, isLoading } = usePayrollStatutorySettings(legalEntityId);
  const updateMutation = useUpdateStatutorySettingsMutation(legalEntityId);

  const [taxMode, setTaxMode] = useState<StatutoryMode>("NotManaged");
  const [socialInsuranceMode, setSocialInsuranceMode] = useState<StatutoryMode>("NotManaged");
  const [manualInputPolicy, setManualInputPolicy] = useState<ManualInputPolicy>("Block");
  const [effectiveDate, setEffectiveDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState<string>("");
  const [historyOpen, setHistoryOpen] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  useEffect(() => {
    if (currentSettings) {
      setTaxMode(currentSettings.taxMode);
      setSocialInsuranceMode(currentSettings.insuranceMode);
      setManualInputPolicy(currentSettings.manualInputPolicy);
      if (currentSettings.effectiveFrom) {
        setEffectiveDate(currentSettings.effectiveFrom);
      }
    }
  }, [currentSettings]);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setSuccessMessage(null);

    try {
      await updateMutation.mutateAsync({
        taxMode,
        insuranceMode: socialInsuranceMode,
        manualInputPolicy,
        effectiveFrom: effectiveDate,
        expectedVersion: currentSettings?.version ?? 0
      });
      setSuccessMessage(t("settings.savedSuccess"));
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const modeOptions = [
    {
      id: "NotManaged",
      title: t("settings.modes.notManagedTitle"),
      desc: t("settings.modes.notManagedDesc")
    },
    {
      id: "Manual",
      title: t("settings.modes.manualTitle"),
      desc: t("settings.modes.manualDesc")
    },
    {
      id: "Automatic",
      title: t("settings.modes.automaticTitle"),
      desc: t("settings.modes.automaticDesc")
    }
  ];

  const policyOptions = [
    { id: "Block", label: t("settings.policy.block") },
    { id: "Skip", label: t("settings.policy.skip") }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Top Header Card */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          padding: "1.25rem 1.5rem",
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-lg)",
          border: "1px solid var(--color-border-default)",
          boxShadow: "var(--shadow-sm)"
        }}
      >
        <div>
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
            <h2
              style={{
                margin: 0,
                fontSize: "1.125rem",
                fontWeight: 700,
                color: "var(--color-content-primary)"
              }}
            >
              {t("settings.title")}
            </h2>
            {currentSettings && (
              <span
                style={{
                  fontSize: "0.75rem",
                  color: "var(--color-content-muted)",
                  background: "var(--color-surface-subtle)",
                  padding: "0.25rem 0.5rem",
                  borderRadius: "var(--radius-sm)",
                  border: "1px solid var(--color-border-default)"
                }}
              >
                {t("settings.version", { version: currentSettings.version })}
              </span>
            )}
          </div>
          <p
            style={{
              margin: "0.25rem 0 0 0",
              fontSize: "0.875rem",
              color: "var(--color-content-muted)"
            }}
          >
            {t("settings.subtitle")}
          </p>
        </div>

        <Button
          variant="secondary"
          onPress={() => setHistoryOpen(true)}
          style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem" }}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="10" />
            <polyline points="12 6 12 12 16 14" />
          </svg>
          {t("settings.viewHistory")}
        </Button>
      </div>

      {/* Compliance / NotManaged Disclaimer Banner */}
      <div
        style={{
          padding: "1rem 1.25rem",
          borderRadius: "var(--radius-md)",
          background: "var(--color-surface-subtle)",
          borderInlineStart: "4px solid var(--color-brand-primary)",
          border: "1px solid var(--color-border-default)",
          fontSize: "0.875rem",
          color: "var(--color-content-secondary)",
          lineHeight: 1.6
        }}
      >
        <strong style={{ color: "var(--color-content-primary)" }}>{t("settings.disclaimerTitle")}: </strong>
        {t("settings.disclaimerNotice")}
      </div>

      {/* Feedback Messages */}
      {errorMessage && (
        <div
          role="alert"
          style={{
            padding: "0.875rem 1rem",
            borderRadius: "var(--radius-md)",
            background: "var(--color-status-danger-subtle, #fee2e2)",
            color: "var(--color-status-danger, #b91c1c)",
            border: "1px solid var(--color-status-danger-border, #fecaca)",
            fontSize: "0.875rem"
          }}
        >
          {errorMessage}
        </div>
      )}

      {successMessage && (
        <div
          role="status"
          style={{
            padding: "0.875rem 1rem",
            borderRadius: "var(--radius-md)",
            background: "var(--color-status-success-subtle, #ecfdf5)",
            color: "var(--color-status-success, #065f46)",
            border: "1px solid var(--color-status-success-border, #a7f3d0)",
            fontSize: "0.875rem"
          }}
        >
          {successMessage}
        </div>
      )}

      {/* Configuration Form */}
      <form onSubmit={handleSave} style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(360px, 1fr))", gap: "1.5rem" }}>
          {/* Tax Mode Configuration Card */}
          <div
            style={{
              padding: "1.5rem",
              background: "var(--color-surface-card)",
              borderRadius: "var(--radius-lg)",
              border: "1px solid var(--color-border-default)",
              boxShadow: "var(--shadow-sm)",
              display: "flex",
              flexDirection: "column",
              gap: "1.25rem"
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <div>
                <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                  {t("settings.taxCardTitle")}
                </h3>
                <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                  {t("settings.taxCardSubtitle")}
                </p>
              </div>
              <PayrollStatusBadge status={taxMode} type="statutory" />
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
              {modeOptions.map((opt) => (
                <label
                  key={opt.id}
                  style={{
                    display: "flex",
                    alignItems: "flex-start",
                    gap: "0.75rem",
                    padding: "0.875rem 1rem",
                    borderRadius: "var(--radius-md)",
                    border: `1.5px solid ${taxMode === opt.id ? "var(--color-brand-primary)" : "var(--color-border-default)"}`,
                    background: taxMode === opt.id ? "var(--color-brand-primary-subtle)" : "var(--color-surface-card)",
                    cursor: "pointer",
                    transition: "all 0.15s ease"
                  }}
                >
                  <input
                    type="radio"
                    name="taxMode"
                    value={opt.id}
                    checked={taxMode === opt.id}
                    onChange={() => setTaxMode(opt.id as StatutoryMode)}
                    style={{ marginTop: "0.25rem" }}
                  />
                  <div style={{ display: "flex", flexDirection: "column" }}>
                    <span style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
                      {opt.title}
                    </span>
                    <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginTop: "0.125rem" }}>
                      {opt.desc}
                    </span>
                  </div>
                </label>
              ))}
            </div>
          </div>

          {/* Social Insurance Mode Configuration Card */}
          <div
            style={{
              padding: "1.5rem",
              background: "var(--color-surface-card)",
              borderRadius: "var(--radius-lg)",
              border: "1px solid var(--color-border-default)",
              boxShadow: "var(--shadow-sm)",
              display: "flex",
              flexDirection: "column",
              gap: "1.25rem"
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <div>
                <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                  {t("settings.insuranceCardTitle")}
                </h3>
                <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                  {t("settings.insuranceCardSubtitle")}
                </p>
              </div>
              <PayrollStatusBadge status={socialInsuranceMode} type="statutory" />
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
              {modeOptions.map((opt) => (
                <label
                  key={opt.id}
                  style={{
                    display: "flex",
                    alignItems: "flex-start",
                    gap: "0.75rem",
                    padding: "0.875rem 1rem",
                    borderRadius: "var(--radius-md)",
                    border: `1.5px solid ${socialInsuranceMode === opt.id ? "var(--color-brand-primary)" : "var(--color-border-default)"}`,
                    background: socialInsuranceMode === opt.id ? "var(--color-brand-primary-subtle)" : "var(--color-surface-card)",
                    cursor: "pointer",
                    transition: "all 0.15s ease"
                  }}
                >
                  <input
                    type="radio"
                    name="socialInsuranceMode"
                    value={opt.id}
                    checked={socialInsuranceMode === opt.id}
                    onChange={() => setSocialInsuranceMode(opt.id as StatutoryMode)}
                    style={{ marginTop: "0.25rem" }}
                  />
                  <div style={{ display: "flex", flexDirection: "column" }}>
                    <span style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
                      {opt.title}
                    </span>
                    <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginTop: "0.125rem" }}>
                      {opt.desc}
                    </span>
                  </div>
                </label>
              ))}
            </div>
          </div>
        </div>

        {/* Progressive Disclosure: Manual Input Policy */}
        {(taxMode === "Manual" || socialInsuranceMode === "Manual") && (
          <div
            style={{
              padding: "1.25rem 1.5rem",
              background: "var(--color-surface-card)",
              borderRadius: "var(--radius-lg)",
              border: "1px solid var(--color-border-default)",
              boxShadow: "var(--shadow-sm)",
              display: "flex",
              flexDirection: "column",
              gap: "1rem"
            }}
          >
            <div>
              <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                {t("settings.manualPolicyTitle")}
              </h3>
              <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("settings.manualPolicySubtitle")}
              </p>
            </div>

            <div style={{ maxWidth: "24rem" }}>
              <Select
                label={t("settings.policyLabel")}
                selectedKey={manualInputPolicy}
                onSelectionChange={(val) => {
                  if (val) setManualInputPolicy(String(val) as ManualInputPolicy);
                }}
                options={policyOptions}
              />
            </div>
          </div>
        )}

        {/* Effective Date and Audit Notes Card */}
        <div
          style={{
            padding: "1.25rem 1.5rem",
            background: "var(--color-surface-card)",
            borderRadius: "var(--radius-lg)",
            border: "1px solid var(--color-border-default)",
            boxShadow: "var(--shadow-sm)",
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
            gap: "1.25rem",
            alignItems: "end"
          }}
        >
          <LocalDateField
            label={t("settings.effectiveDate")}
            value={effectiveDate}
            onChange={setEffectiveDate}
            isRequired
            description={t("settings.effectiveDateDesc")}
          />

          <TextField
            label={t("settings.auditNotes")}
            value={notes}
            onChange={setNotes}
            placeholder={t("settings.auditNotesPlaceholder")}
            description={t("settings.auditNotesDesc")}
          />

          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              variant="primary"
              type="submit"
              isDisabled={updateMutation.isPending || isLoading}
              style={{ minWidth: "10rem" }}
            >
              {updateMutation.isPending ? t("actions.saving") : t("settings.saveAction")}
            </Button>
          </div>
        </div>
      </form>

      {/* Historical Versions Modal */}
      <StatutoryHistoryModal
        isOpen={historyOpen}
        onClose={() => setHistoryOpen(false)}
        legalEntityId={legalEntityId}
      />
    </div>
  );
}
