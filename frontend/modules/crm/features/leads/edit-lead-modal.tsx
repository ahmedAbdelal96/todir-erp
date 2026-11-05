import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useUpdateLeadMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Lead, LeadPriority } from "../../types.ts";

export interface EditLeadModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly lead: Lead | null;
  readonly onUpdated?: () => void;
}

export function EditLeadModal({
  isOpen,
  onClose,
  legalEntityId,
  lead,
  onUpdated
}: EditLeadModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const updateMutation = useUpdateLeadMutation(legalEntityId, lead?.id || "");

  const [displayName, setDisplayName] = useState("");
  const [companyName, setCompanyName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [source, setSource] = useState("Website");
  const [priority, setPriority] = useState<LeadPriority>("Normal");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (lead) {
      setDisplayName(lead.displayName);
      setCompanyName(lead.companyName || "");
      setEmail(lead.email || "");
      setPhone(lead.phone || "");
      setSource(lead.source || "Website");
      setPriority(lead.priority || "Normal");
      setErrorMessage(null);
    }
  }, [lead]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!lead) return;
    if (!displayName.trim()) {
      setErrorMessage(t("errors.leadNameRequired"));
      return;
    }

    try {
      await updateMutation.mutateAsync({
        displayName: displayName.trim(),
        companyName: companyName.trim() || null,
        email: email.trim() || null,
        phone: phone.trim() || null,
        source: source.trim() || "Website",
        priority,
        version: lead.version
      });

      onClose();
      onUpdated?.();
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
        if (!open) onClose();
      }}
      title={t("leads.editLeadModal.title")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={onClose}
            isDisabled={updateMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="edit-lead-form"
            isDisabled={updateMutation.isPending}
          >
            {updateMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </>
      }
    >
      <form id="edit-lead-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
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
          />
          <TextField
            label={t("leads.fields.companyName")}
            value={companyName}
            onChange={setCompanyName}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.75rem" }}>
          <TextField
            label={t("leads.fields.phone")}
            value={phone}
            onChange={setPhone}
          />
          <TextField
            label={t("leads.fields.email")}
            value={email}
            onChange={setEmail}
            type="email"
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
      </form>
    </Dialog>
  );
}
