import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import {
  useCreateTaxRegistrationMutation,
  useUpdateTaxRegistrationMutation,
  useDeregisterMutation
} from "../hooks/tax-queries.ts";
import { extractTaxError } from "../services/tax-error-map.ts";
import type { TaxRegistrationDto } from "../types.ts";

function normalizeRegistrationStatus(status: string | undefined): string {
  if (status === "Pending") return "PendingRegistration";
  if (status === "Suspended") return "RegistrationException";
  return status || "PendingRegistration";
}

export interface RegistrationModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly registration?: TaxRegistrationDto | null | undefined;
  readonly mode?: "create" | "edit" | "deregister" | undefined;
  readonly defaultCountryCode?: string | undefined;
  readonly defaultEffectiveDate: string;
}

export function RegistrationModal({
  isOpen,
  onClose,
  legalEntityId,
  registration,
  mode = "create",
  defaultCountryCode = "EG",
  defaultEffectiveDate
}: RegistrationModalProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");

  const [countryCode, setCountryCode] = useState(registration?.countryCode || defaultCountryCode);
  const [schemeCode, setSchemeCode] = useState(registration?.schemeCode || "DEVELOPMENT");
  const [registrationNumber, setRegistrationNumber] = useState(registration?.registrationNumber || "");
  const [status, setStatus] = useState(
    mode === "deregister" ? "Deregistered" : normalizeRegistrationStatus(registration?.status)
  );
  const [effectiveFrom, setEffectiveFrom] = useState(defaultEffectiveDate);
  const [authority, setAuthority] = useState(registration?.authority || "");
  const [evidenceReference, setEvidenceReference] = useState(registration?.evidenceReference || "");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const createMutation = useCreateTaxRegistrationMutation();
  const updateMutation = useUpdateTaxRegistrationMutation();
  const deregisterMutation = useDeregisterMutation();

  const isPending =
    createMutation.isPending || updateMutation.isPending || deregisterMutation.isPending;

  const handleSave = async () => {
    setErrorMessage(null);
    if (status === "Registered" && !registrationNumber.trim()) {
      setErrorMessage(t("errors.registrationNumberRequired"));
      return;
    }
    try {
      if (mode === "create") {
        await createMutation.mutateAsync({
          countryCode: countryCode.toUpperCase(),
          schemeCode,
          registrationNumber: registrationNumber || null,
          status,
          effectiveFrom,
          authority: authority || null,
          evidenceReference: evidenceReference || null,
          legalEntityId
        });
      } else if (mode === "deregister" && registration) {
        await deregisterMutation.mutateAsync({
          id: registration.id,
          legalEntityId,
          request: {
            status: "Deregistered",
            effectiveFrom,
            registrationNumber: registrationNumber || registration.registrationNumber || null,
            authority: authority || null,
            evidenceReference: evidenceReference || null
          }
        });
      } else if (mode === "edit" && registration) {
        await updateMutation.mutateAsync({
          id: registration.id,
          legalEntityId,
          request: {
            status,
            effectiveFrom,
            registrationNumber: registrationNumber || null,
            authority: authority || null,
            evidenceReference: evidenceReference || null
          }
        });
      }
      onClose();
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
    }
  };

  const modalTitle =
    mode === "create"
      ? t("registrations.createModalTitle")
      : mode === "deregister"
        ? t("registrations.deregisterModalTitle")
        : t("registrations.editModalTitle");

  const modalDescription =
    mode === "deregister"
      ? t("registrations.deregisterDescription")
      : t("registrations.modalDescription");

  const footer = (
    <div style={{ display: "flex", gap: "var(--space-2)", justifyContent: "flex-end" }}>
      <Button variant="secondary" onPress={onClose}>
        {t("actions.cancel")}
      </Button>
      <Button
        variant={mode === "deregister" ? "danger" : "primary"}
        onPress={handleSave}
        isLoading={isPending}
      >
        {mode === "deregister"
          ? t("registrations.deregisterAction")
          : t("actions.save")}
      </Button>
    </div>
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={modalTitle}
      description={modalDescription}
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

        {mode === "create" && (
          <>
            <TextField
              label={t("registrations.countryCode")}
              value={countryCode}
              onChange={setCountryCode}
              isRequired
            />
            <TextField
              label={t("registrations.schemeCode")}
              value={schemeCode}
              onChange={setSchemeCode}
              isRequired
            />
          </>
        )}

        {mode !== "deregister" && (
          <TextField
            label={t("registrations.registrationNumber")}
            value={registrationNumber}
            onChange={setRegistrationNumber}
              isRequired={status === "Registered"}
          />
        )}

        {mode !== "deregister" && (
          <Select
            label={t("registrations.status")}
            selectedKey={status}
            onSelectionChange={(key) => setStatus(key as string)}
            options={[
              { id: "Registered", label: t("registrationStatus.registered") },
              { id: "PendingRegistration", label: t("registrationStatus.pending") },
              { id: "RegistrationException", label: t("registrationStatus.exception") }
            ]}
          />
        )}

        <LocalDateField
          label={t("registrations.effectiveFrom")}
          value={effectiveFrom}
          onChange={setEffectiveFrom}
          isRequired
        />

        <TextField
          label={t("registrations.authority")}
          value={authority}
          onChange={setAuthority}
        />

        <TextField
          label={t("registrations.evidenceReference")}
          value={evidenceReference}
          onChange={setEvidenceReference}
        />
      </div>
    </Dialog>
  );
}
