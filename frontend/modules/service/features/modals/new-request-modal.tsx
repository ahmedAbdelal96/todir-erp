import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { useCreateServiceRequestMutation } from "../../hooks/use-service-queries.ts";
import type { ServiceContext, ServicePriority } from "../../types.ts";

export interface NewRequestModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewRequestModal({
  isOpen,
  onClose,
  legalEntityId
}: NewRequestModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const createMutation = useCreateServiceRequestMutation(legalEntityId);

  const [requestNumber, setRequestNumber] = useState(`SR-${Date.now().toString().slice(-6)}`);
  const [context, setContext] = useState<ServiceContext>("customer");
  const [priority, setPriority] = useState<ServicePriority>("medium");
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [siteLocation, setSiteLocation] = useState("");
  const [assetReference, setAssetReference] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!subject.trim()) {
      setErrorMsg(t("validation.subjectRequired"));
      return;
    }

    try {
      setErrorMsg(null);
      await createMutation.mutateAsync({
        legalEntityId,
        requestNumber: requestNumber.trim(),
        context,
        priority,
        subject: subject.trim(),
        description: description.trim() || undefined,
        customerName: context === "customer" ? customerName.trim() || undefined : undefined,
        siteLocation: context === "customer" ? siteLocation.trim() || undefined : undefined,
        assetReference: context === "internal" ? assetReference.trim() || undefined : undefined
      });
      onClose();
      // Reset form
      setSubject("");
      setDescription("");
      setCustomerName("");
      setSiteLocation("");
      setAssetReference("");
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : t("common.genericError"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("requests.newRequestTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-sm, 0.375rem)",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              color: "#b91c1c",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("requests.requestNumber")} *
            </label>
            <TextField
              value={requestNumber}
              onChange={(val) => setRequestNumber(val)}
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("requests.context")} *
            </label>
            <Select
              value={context}
              onChange={(val) => setContext(val as ServiceContext)}
              options={[
                { value: "customer", label: t("contexts.customer") },
                { value: "internal", label: t("contexts.internal") }
              ]}
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("common.priority")} *
            </label>
            <Select
              value={priority}
              onChange={(val) => setPriority(val as ServicePriority)}
              options={[
                { value: "low", label: t("priorities.low") },
                { value: "medium", label: t("priorities.medium") },
                { value: "high", label: t("priorities.high") },
                { value: "urgent", label: t("priorities.urgent") }
              ]}
            />
          </div>

          {context === "customer" ? (
            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                {t("requests.customerName")}
              </label>
              <TextField
                value={customerName}
                onChange={(val) => setCustomerName(val)}
                placeholder={t("requests.customerPlaceholder")}
              />
            </div>
          ) : (
            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                {t("requests.assetReference")}
              </label>
              <TextField
                value={assetReference}
                onChange={(val) => setAssetReference(val)}
                placeholder={t("requests.assetPlaceholder")}
              />
            </div>
          )}
        </div>

        {context === "customer" && (
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("requests.siteLocation")}
            </label>
            <TextField
              value={siteLocation}
              onChange={(val) => setSiteLocation(val)}
              placeholder={t("requests.sitePlaceholder")}
            />
          </div>
        )}

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("requests.subject")} *
          </label>
          <TextField
            value={subject}
            onChange={(val) => setSubject(val)}
            placeholder={t("requests.subjectPlaceholder")}
            isRequired
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("requests.description")}
          </label>
          <TextArea
            value={description}
            onChange={(val: any) => setDescription(typeof val === 'string' ? val : val?.target?.value ?? '')}
            placeholder={t("requests.descriptionPlaceholder")}
            rows={3}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createMutation.isPending}>
            {t("requests.submitRequest")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
