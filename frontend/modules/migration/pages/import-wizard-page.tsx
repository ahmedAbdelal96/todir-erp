import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { currentIntlLocale } from "../../../core/i18n/i18n.ts";
import { WizardPage, type WizardStep } from "../../../components/pages/wizard-page.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { useCreateImportJob, useDryRunImport, useExecuteImport, useImportJob } from "../hooks/import-queries.ts";
import type { FieldMapping, TargetFieldDefinition } from "../types.ts";

export interface ImportWizardPageProps {
  readonly onFinish?: (() => void) | undefined;
}

const SAMPLE_SOURCE_COLUMNS = ["customer_name", "official_legal_name", "vat_number", "type", "email_address", "phone_no"];

export function ImportWizardPage({ onFinish }: ImportWizardPageProps): React.JSX.Element {
  const { t } = useTranslation("migration");
  const formatCount = new Intl.NumberFormat(currentIntlLocale());
  const [currentStepIndex, setCurrentStepIndex] = useState(0);
  const [selectedFile, setSelectedFile] = useState<string>("parties_batch_2026.csv");
  const [mappings, setMappings] = useState<FieldMapping>({
    customer_name: "displayName",
    official_legal_name: "legalName",
    vat_number: "taxId",
    type: "partyType",
    email_address: "primaryEmail",
    phone_no: "primaryPhone"
  });

  const [jobId, setJobId] = useState<string | null>("job-imp-001");
  const { data: job } = useImportJob(jobId);

  const createJobMutation = useCreateImportJob();
  const dryRunMutation = useDryRunImport();
  const executeMutation = useExecuteImport();
  const steps: readonly WizardStep[] = [
    { id: "upload", title: t("wizard.uploadTitle"), description: t("wizard.uploadDescription") },
    { id: "mapping", title: t("wizard.mappingTitle"), description: t("wizard.mappingDescription") },
    { id: "validation", title: t("wizard.validationTitle"), description: t("wizard.validationStepDescription") },
    { id: "execution", title: t("wizard.executionTitle"), description: t("wizard.executionDescription") }
  ];
  const targetFields: readonly TargetFieldDefinition[] = [
    { id: "displayName", label: t("wizard.displayName"), isRequired: true, example: "شركة الأفق للتجارة" },
    { id: "legalName", label: t("wizard.legalName"), isRequired: false, example: "شركة الأفق للتجارة العامة ذ.م.م" },
    { id: "taxId", label: t("wizard.taxId"), isRequired: false, example: "300123456700003" },
    { id: "partyType", label: t("wizard.partyType"), isRequired: true, example: "Organization" },
    { id: "primaryEmail", label: t("wizard.email"), isRequired: false, example: "info@example.com" },
    { id: "primaryPhone", label: t("wizard.phone"), isRequired: false, example: "+966 50 123 4567" }
  ];

  const handleNext = async () => {
    if (currentStepIndex === 0) {
      if (!jobId) {
        const created = await createJobMutation.mutateAsync({
          fileName: selectedFile,
          entityType: "Parties"
        });
        setJobId(created.id);
      }
      setCurrentStepIndex(1);
    } else if (currentStepIndex === 1) {
      if (jobId) {
        await dryRunMutation.mutateAsync({
          jobId,
          mappings
        });
      }
      setCurrentStepIndex(2);
    } else if (currentStepIndex === 2) {
      if (jobId) {
        await executeMutation.mutateAsync({
          jobId,
          mappings
        });
      }
      setCurrentStepIndex(3);
    }
  };

  const handleBack = () => {
    if (currentStepIndex > 0) {
      setCurrentStepIndex(currentStepIndex - 1);
    }
  };

  const handleMappingChange = (sourceCol: string, targetFieldId: string) => {
    setMappings((prev) => ({
      ...prev,
      [sourceCol]: targetFieldId
    }));
  };

  return (
    <WizardPage
      title={t("wizard.title")}
      steps={steps}
      currentStepIndex={currentStepIndex}
      onNext={handleNext}
      onBack={handleBack}
      onComplete={onFinish}
      isSubmitting={dryRunMutation.isPending || executeMutation.isPending}
    >
      {currentStepIndex === 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
          <div
            style={{
              border: "2px dashed var(--color-border-default)",
              borderRadius: "var(--radius-lg)",
              padding: "2.5rem 1.5rem",
              textAlign: "center",
              backgroundColor: "var(--color-surface-subtle)"
            }}
          >
            <div style={{ fontSize: "2.5rem", marginBottom: "0.5rem" }}>📁</div>
            <h3 style={{ margin: "0 0 0.5rem 0" }}>{t("wizard.selectFile")}</h3>
            <p style={{ margin: "0 0 1rem 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
              {t("wizard.supportedFormats")}
            </p>
            <div style={{ display: "inline-block", padding: "0.5rem 1rem", backgroundColor: "var(--color-surface-card)", borderRadius: "var(--radius-md)", border: "1px solid var(--color-border-default)" }}>
              {t("wizard.selectedFile")}: <strong>{selectedFile}</strong> ({formatCount.format(1000)} {t("wizard.records")})
            </div>
          </div>
        </div>
      )}

      {currentStepIndex === 1 && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          <div>
            <h3 style={{ margin: "0 0 0.25rem 0" }}>{t("wizard.mapColumnsTitle")}</h3>
            <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
              {t("wizard.mapColumnsDescription")}
            </p>
          </div>

          <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ backgroundColor: "var(--color-surface-subtle)", borderBottom: "1px solid var(--color-border-default)" }}>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("wizard.sourceColumn")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("wizard.targetField")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("wizard.fieldStatus")}</th>
                </tr>
              </thead>
              <tbody>
                {SAMPLE_SOURCE_COLUMNS.map((col) => {
                  const currentTarget = mappings[col] ?? "";
                  const targetDef = targetFields.find((f) => f.id === currentTarget);
                  return (
                    <tr key={col} style={{ borderBottom: "1px solid var(--color-border-default)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600, fontFamily: "var(--font-mono)" }}>
                        {col}
                      </td>
                      <td style={{ padding: "0.5rem 1rem", width: "300px" }}>
                        <Select
                          aria-label={t("wizard.mapColumn", { column: col })}
                          size="compact"
                          selectedKey={currentTarget}
                          onSelectionChange={(key) => handleMappingChange(col, String(key ?? ""))}
                          options={[
                            { id: "", label: t("wizard.ignoreColumn") },
                            ...targetFields.map((f) => ({
                              id: f.id,
                              label: `${f.label} ${f.isRequired ? "*" : ""}`
                            }))
                          ]}
                        />
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        {targetDef?.isRequired ? (
                          <span style={{ color: "var(--color-brand-primary)", fontWeight: 500, fontSize: "0.75rem" }}>
                            {t("wizard.requiredField")}
                          </span>
                        ) : (
                          <span style={{ color: "var(--color-content-muted)", fontSize: "0.75rem" }}>
                            {t("wizard.optionalField")}
                          </span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {currentStepIndex === 2 && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          <div>
            <h3 style={{ margin: "0 0 0.25rem 0" }}>{t("wizard.validationResults")}</h3>
            <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
              {t("wizard.validationDescription")}
            </p>
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: "1rem" }}>
            <div style={{ padding: "1rem", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-md)", textAlign: "center" }}>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("wizard.totalRecords")}</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 700 }}>{formatCount.format(1000)}</div>
            </div>
            <div style={{ padding: "1rem", backgroundColor: "var(--color-surface-success-subtle, #ecfdf5)", color: "var(--color-status-success)", borderRadius: "var(--radius-md)", textAlign: "center" }}>
              <div style={{ fontSize: "0.875rem" }}>{t("wizard.validRecords")}</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 700 }}>{formatCount.format(980)}</div>
            </div>
            <div style={{ padding: "1rem", backgroundColor: "var(--color-surface-danger-subtle, #fef2f2)", color: "var(--color-status-danger)", borderRadius: "var(--radius-md)", textAlign: "center" }}>
              <div style={{ fontSize: "0.875rem" }}>{t("wizard.invalidRecords")}</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 700 }}>{formatCount.format(20)}</div>
            </div>
          </div>

          <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", padding: "1rem" }}>
            <h4 style={{ margin: "0 0 0.75rem 0", fontSize: "0.875rem" }}>{t("wizard.sampleErrors")}</h4>
            <ul style={{ margin: 0, paddingInlineStart: "1.25rem", fontSize: "0.8125rem", color: "var(--color-status-danger)", display: "flex", flexDirection: "column", gap: "0.375rem" }}>
              <li>{t("wizard.sampleErrorTax", { row: formatCount.format(42) })}</li>
              <li>{t("wizard.sampleErrorEmail", { row: formatCount.format(115) })}</li>
              <li>{t("wizard.sampleErrorName", { row: formatCount.format(890) })}</li>
            </ul>
          </div>
        </div>
      )}

      {currentStepIndex === 3 && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", textAlign: "center", padding: "1rem 0" }}>
          <div style={{ fontSize: "3rem" }}>🎉</div>
          <div>
            <h2 style={{ margin: "0 0 0.5rem 0" }}>{t("wizard.completeTitle")}</h2>
            <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.9375rem" }}>
              {t("wizard.completeDescription", { created: formatCount.format(980), errors: formatCount.format(20) })}
            </p>
          </div>

          <div style={{ display: "inline-flex", justifyContent: "center", gap: "1rem" }}>
            <Button variant="secondary" onPress={() => {}}>
              {t("wizard.downloadErrors")}
            </Button>
            {onFinish && (
              <Button variant="primary" onPress={onFinish}>
                {t("wizard.backToDirectory")}
              </Button>
            )}
          </div>
        </div>
      )}
    </WizardPage>
  );
}
