import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { CountryPackStatusBadge } from "../components/country-pack-status-badge.tsx";
import { TaxStatusBadge } from "../components/tax-status-badge.tsx";
import { useCountryPackActionMutation } from "../hooks/tax-queries.ts";
import { extractTaxError } from "../services/tax-error-map.ts";
import type { TaxCountryPackDto } from "../types.ts";

export interface CountryPackActionDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly pack: TaxCountryPackDto;
  readonly action: "validate" | "schedule" | "activate" | "retire" | "rollback";
  readonly defaultEffectiveDate: string;
}

export function CountryPackActionDialog({
  isOpen,
  onClose,
  pack,
  action,
  defaultEffectiveDate
}: CountryPackActionDialogProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");

  const [effectiveFrom, setEffectiveFrom] = useState(defaultEffectiveDate);
  const [reason, setReason] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const mutation = useCountryPackActionMutation();

  const handleExecute = async () => {
    setErrorMessage(null);
    try {
      await mutation.mutateAsync({
        action,
        id: pack.id,
        request: {
          effectiveFrom: action === "schedule" || action === "activate" || action === "rollback" ? effectiveFrom : undefined,
          reason: action === "retire" ? (reason || undefined) : undefined
        }
      });
      onClose();
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
    }
  };

  const isStatutoryPending =
    pack.sourceKind !== "TestOnly" &&
    (pack.reviewStatus === "Pending" || pack.reviewStatus === "Unreviewed");

  const titles: Record<string, string> = {
    validate: t("countryPack.validateTitle"),
    schedule: t("countryPack.scheduleTitle"),
    activate: t("countryPack.activateTitle"),
    retire: t("countryPack.retireTitle"),
    rollback: t("countryPack.rollbackTitle")
  };

  const getConfirmLabel = (act: typeof action) => {
    switch (act) {
      case "validate":
        return t("countryPack.confirmValidate");
      case "schedule":
        return t("countryPack.confirmSchedule");
      case "activate":
        return t("countryPack.confirmActivate");
      case "retire":
        return t("countryPack.confirmRetire");
      case "rollback":
        return t("countryPack.confirmRollback");
    }
  };

  const footer = (
    <div style={{ display: "flex", gap: "var(--space-2)", justifyContent: "flex-end" }}>
      <Button variant="secondary" onPress={onClose}>
        {t("actions.cancel")}
      </Button>
      <Button
        variant={action === "retire" ? "danger" : "primary"}
        onPress={() => void handleExecute()}
        isLoading={mutation.isPending}
      >
        {getConfirmLabel(action)}
      </Button>
    </div>
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={titles[action] || t("countryPack.actionTitle")}
      description={t("countryPack.actionDescription")}
      footer={footer}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger-text, #991b1b)",
              fontSize: "0.875rem"
            }}
          >
            <strong>✕ {t("actions.errorTitle")}: </strong>
            <span>{errorMessage}</span>
          </div>
        )}

        {action === "activate" && isStatutoryPending && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-warning-subtle, #fffbeb)",
              color: "var(--color-status-warning-text, #92400e)",
              fontSize: "0.8125rem"
            }}
          >
            {t("countryPack.statutoryReviewBlockNotice")}
          </div>
        )}

        <div
          style={{
            padding: "0.875rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-subtle, #e2e8f0)",
            fontSize: "0.8125rem",
            display: "grid",
            gridTemplateColumns: "1fr 1fr",
            gap: "0.5rem"
          }}
        >
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("countryPack.country")}</span>
            <strong>{pack.countryCode} ({pack.packCode})</strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("countryPack.version")}</span>
            <strong>{t("registrations.versionTag", { version: pack.packVersion })}</strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("countryPack.technicalStatus")}</span>
            <TaxStatusBadge status={pack.status} type="countryPack" />
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("countryPack.statutoryReview")}</span>
            <CountryPackStatusBadge
              countryCode={pack.countryCode}
              sourceKind={pack.sourceKind}
              reviewStatus={pack.reviewStatus}
              technicalStatus={pack.status}
            />
          </div>
        </div>

        {(action === "schedule" || action === "activate" || action === "rollback") && (
          <LocalDateField
            label={t("countryPack.effectiveDate")}
            value={effectiveFrom}
            onChange={setEffectiveFrom}
            isRequired
          />
        )}

        {action === "retire" && (
          <TextField
            label={t("countryPack.retirementReason")}
            value={reason}
            onChange={setReason}
            isRequired
          />
        )}
      </div>
    </Dialog>
  );
}
