import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCreateLeadMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { LeadPriority } from "../../types.ts";

export interface NewLeadModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly onCreated?: (leadId: string) => void;
}

export function NewLeadModal({
  isOpen,
  onClose,
  legalEntityId,
  onCreated
}: NewLeadModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const createMutation = useCreateLeadMutation(legalEntityId);

  const [displayName, setDisplayName] = useState("");
  const [companyName, setCompanyName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [source, setSource] = useState("Website");
  const [priority, setPriority] = useState<LeadPriority>("Normal");
  const [interest, setInterest] = useState("");
  const [notes, setNotes] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setDisplayName("");
    setCompanyName("");
    setEmail("");
    setPhone("");
    setSource("Website");
    setPriority("Normal");
    setInterest("");
    setNotes("");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!displayName.trim()) {
      setErrorMessage(t("errors.leadNameRequired"));
      return;
    }

    try {
      const created = await createMutation.mutateAsync({
        legalEntityId,
        displayName: displayName.trim(),
        companyName: companyName.trim() || null,
        email: email.trim() || null,
        phone: phone.trim() || null,
        source: source.trim() || "Website",
        priority,
        interest: interest.trim() || null,
        notes: notes.trim() || null
      });

      reset();
      onClose();
      if (onCreated && created.id) {
        onCreated(created.id);
      }
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const sourceOptions = [
    { id: "Website", label: t("leadSources.Website") },
    { id: "Referral", label: t("leadSources.Referral") },
    { id: "InboundCall", label: t("leadSources.InboundCall") },
    { id: "Campaign", label: t("leadSources.Campaign") },
    { id: "Exhibition", label: t("leadSources.Exhibition") },
    { id: "Other", label: t("leadSources.Other") }
  ];

  const priorityOptions = [
    { id: "High", label: t("priorities.High") },
    { id: "Normal", label: t("priorities.Normal") },
    { id: "Low", label: t("priorities.Low") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={t("leads.newLeadModal.title")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={() => {
              reset();
              onClose();
            }}
            isDisabled={createMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="new-lead-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("actions.saving") : t("leads.newLeadModal.submit")}
          </Button>
        </>
      }
    >
      <form id="new-lead-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.625rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              background: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.8125rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.75rem" }}>
          <TextField
            label={t("leads.fields.displayName")}
            value={displayName}
            onChange={setDisplayName}
            isRequired
            placeholder={t("leads.fields.displayNamePlaceholder")}
          />
          <TextField
            label={t("leads.fields.companyName")}
            value={companyName}
            onChange={setCompanyName}
            placeholder={t("leads.fields.companyNamePlaceholder")}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.75rem" }}>
          <TextField
            label={t("leads.fields.phone")}
            value={phone}
            onChange={setPhone}
            placeholder={t("leads.fields.phonePlaceholder")}
          />
          <TextField
            label={t("leads.fields.email")}
            value={email}
            onChange={setEmail}
            type="email"
            placeholder={t("leads.fields.emailPlaceholder")}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.75rem" }}>
          <Select
            label={t("leads.fields.source")}
            selectedKey={source}
            onSelectionChange={(key) => setSource(String(key))}
            options={sourceOptions}
          />
          <Select
            label={t("leads.fields.priority")}
            selectedKey={priority}
            onSelectionChange={(key) => setPriority(key as LeadPriority)}
            options={priorityOptions}
          />
        </div>

        <TextField
          label={t("leads.fields.interest")}
          value={interest}
          onChange={setInterest}
          placeholder={t("leads.fields.interestPlaceholder")}
        />

        <TextField
          label={t("leads.fields.notes")}
          value={notes}
          onChange={setNotes}
          placeholder={t("leads.fields.notesPlaceholder")}
        />
      </form>
    </Dialog>
  );
}
