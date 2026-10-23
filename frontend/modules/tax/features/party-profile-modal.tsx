import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { useSavePartyProfileMutation } from "../hooks/tax-queries.ts";
import { extractTaxError } from "../services/tax-error-map.ts";
import type { TaxCategoryItem, TaxProfileDto } from "../types.ts";

export interface PartyProfileModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly profile?: TaxProfileDto | null | undefined;
  readonly categories: readonly TaxCategoryItem[];
  readonly defaultEffectiveDate: string;
}

export function PartyProfileModal({
  isOpen,
  onClose,
  legalEntityId,
  profile,
  categories,
  defaultEffectiveDate
}: PartyProfileModalProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");

  const [partyId, setPartyId] = useState(profile?.partyId || "");
  const [countryCode, setCountryCode] = useState(profile?.countryCode || "EG");
  const [categoryId, setCategoryId] = useState(
    profile?.categoryId || categories.find((c) => c.scope === "Party")?.id || ""
  );
  const [treatment, setTreatment] = useState(profile?.treatment || "StandardRated");
  const [reverseChargeEligible, setReverseChargeEligible] = useState(
    Boolean(profile?.reverseChargeEligible)
  );
  const [registrationNumber, setRegistrationNumber] = useState(profile?.registrationNumber || "");
  const [registrationCountryCode, setRegistrationCountryCode] = useState(
    profile?.registrationCountryCode || "EG"
  );
  const [registrationStatus, setRegistrationStatus] = useState(
    profile?.registrationStatus || "Registered"
  );
  const [evidenceReference, setEvidenceReference] = useState(profile?.evidenceReference || "");
  const [effectiveFrom, setEffectiveFrom] = useState(defaultEffectiveDate);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const mutation = useSavePartyProfileMutation();
  const isEditing = Boolean(profile?.id);

  const partyCategories = categories.filter((c) => c.scope === "Party");

  const handleSave = async () => {
    if (!partyId) return;
    setErrorMessage(null);
    try {
      await mutation.mutateAsync({
        id: profile?.id,
        request: {
          partyId,
          legalEntityId: legalEntityId || null,
          countryCode: countryCode.toUpperCase(),
          categoryId: categoryId || null,
          treatment: treatment || null,
          reverseChargeEligible,
          registrationNumber: registrationNumber || null,
          registrationCountryCode: registrationCountryCode ? registrationCountryCode.toUpperCase() : null,
          registrationStatus: registrationStatus || null,
          evidenceReference: evidenceReference || null,
          effectiveFrom
        }
      });
      onClose();
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
    }
  };

  const footer = (
    <div style={{ display: "flex", gap: "var(--space-2)", justifyContent: "flex-end" }}>
      <Button variant="secondary" onPress={onClose}>
        {t("actions.cancel")}
      </Button>
      <Button variant="primary" onPress={handleSave} isLoading={mutation.isPending}>
        {isEditing ? t("partyProfiles.createVersionAction") : t("actions.save")}
      </Button>
    </div>
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={isEditing ? t("partyProfiles.editTitle") : t("partyProfiles.createTitle")}
      description={isEditing ? t("partyProfiles.editDescription") : t("partyProfiles.createDescription")}
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
            {errorMessage}
          </div>
        )}

        <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
          {t("partyProfiles.directoryNotice")}
        </p>

        <TextField
          label={t("partyProfiles.partyReference")}
          value={partyId}
          onChange={setPartyId}
          isRequired
          isDisabled={isEditing}
        />

        <TextField
          label={t("partyProfiles.countryCode")}
          value={countryCode}
          onChange={setCountryCode}
          isRequired
        />

        <Select
          label={t("partyProfiles.taxCategory")}
          selectedKey={categoryId}
          onSelectionChange={(key) => setCategoryId(key as string)}
          options={partyCategories.map((c) => ({
            id: c.id,
            label: `${c.name} (${c.code})`
          }))}
        />

        <Select
          label={t("partyProfiles.treatment")}
          selectedKey={treatment}
          onSelectionChange={(key) => setTreatment(key as string)}
          options={[
            { id: "StandardRated", label: t("treatment.standardRated") },
            { id: "ZeroRated", label: t("treatment.zeroRated") },
            { id: "Exempt", label: t("treatment.exempt") },
            { id: "OutOfScope", label: t("treatment.outOfScope") },
            { id: "ReverseCharge", label: t("treatment.reverseCharge") }
          ]}
        />

        <Checkbox
          isSelected={reverseChargeEligible}
          onChange={setReverseChargeEligible}
        >
          {t("partyProfiles.reverseChargeEligible")}
        </Checkbox>

        <TextField
          label={t("partyProfiles.registrationNumber")}
          value={registrationNumber}
          onChange={setRegistrationNumber}
        />

        <TextField
          label={t("partyProfiles.registrationCountryCode")}
          value={registrationCountryCode}
          onChange={setRegistrationCountryCode}
        />

        <Select
          label={t("partyProfiles.registrationStatus")}
          selectedKey={registrationStatus}
          onSelectionChange={(key) => setRegistrationStatus(key as string)}
          options={[
            { id: "Registered", label: t("registrationStatus.registered") },
            { id: "Pending", label: t("registrationStatus.pending") },
            { id: "Suspended", label: t("registrationStatus.suspended") }
          ]}
        />

        <TextField
          label={t("partyProfiles.evidenceReference")}
          value={evidenceReference}
          onChange={setEvidenceReference}
        />

        <LocalDateField
          label={t("partyProfiles.effectiveFrom")}
          value={effectiveFrom}
          onChange={setEffectiveFrom}
          isRequired
        />

        {isEditing && (
          <div
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-sm)",
              backgroundColor: "var(--color-status-info-subtle, #eff6ff)",
              color: "var(--color-status-info-text, #1e40af)",
              fontSize: "0.8125rem"
            }}
          >
            {t("partyProfiles.versioningNotice")}
          </div>
        )}
      </div>
    </Dialog>
  );
}
