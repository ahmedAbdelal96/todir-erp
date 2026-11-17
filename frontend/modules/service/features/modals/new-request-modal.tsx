import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import {
  useCreateServiceRequestMutation,
  useInstalledBaseQuery
} from "../../hooks/use-service-queries.ts";
import type { ServiceRequestContext, ServicePriority } from "../../types.ts";

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
  const installedBaseQuery = useInstalledBaseQuery(legalEntityId, { pageSize: 100 });
  const installedItems = installedBaseQuery.data?.items ?? [];

  const [context, setContext] = useState<ServiceRequestContext>("CustomerFieldService");
  const [priority, setPriority] = useState<ServicePriority>("Normal");
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [customerPartyId, setCustomerPartyId] = useState("");
  const [installedBaseId, setInstalledBaseId] = useState("");
  const [fixedAssetId, setFixedAssetId] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) {
      setErrorMsg(t("validation.subjectRequired", "يرجى إدخال عنوان أو موضوع البلاغ"));
      return;
    }

    try {
      setErrorMsg(null);
      await createMutation.mutateAsync({
        legalEntityId,
        context,
        title: title.trim(),
        description: description.trim() || undefined,
        customerPartyId: context === "CustomerFieldService" && customerPartyId.trim() ? customerPartyId.trim() : undefined,
        installedBaseId: installedBaseId.trim() || undefined,
        fixedAssetId: context === "InternalMaintenance" && fixedAssetId.trim() ? fixedAssetId.trim() : undefined,
        priority,
        requestedAt: new Date().toISOString()
      });
      onClose();
      setTitle("");
      setDescription("");
      setCustomerPartyId("");
      setInstalledBaseId("");
      setFixedAssetId("");
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : t("common.genericError"));
    }
  };

  const installedBaseOptions = [
    { value: "", label: t("installedBase.selectAsset", "اختر المعدة أو الأصل...") },
    ...installedItems.map((item) => ({
      value: item.id,
      label: `${item.name} (${item.serialNumber || item.modelNumber || item.id.slice(0, 8)})`
    }))
  ];

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
              {t("requests.context")} *
            </label>
            <Select
              value={context}
              onChange={(val) => setContext(val as ServiceRequestContext)}
              options={[
                { value: "CustomerFieldService", label: t("contexts.customer", "خدمة عملاء ميدانية") },
                { value: "InternalMaintenance", label: t("contexts.internal", "صيانة مرافق وأصول داخلية") }
              ]}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("common.priority")} *
            </label>
            <Select
              value={priority}
              onChange={(val) => setPriority(val as ServicePriority)}
              options={[
                { value: "Low", label: t("priorities.low") },
                { value: "Normal", label: t("priorities.medium") },
                { value: "High", label: t("priorities.high") },
                { value: "Critical", label: t("priorities.urgent") }
              ]}
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("requests.subject")} *
          </label>
          <TextField
            value={title}
            onChange={(val) => setTitle(val)}
            placeholder={t("requests.subjectPlaceholder")}
            isRequired
          />
        </div>

        {context === "CustomerFieldService" ? (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                {t("requests.customerPartyId", "معرف العميل (CustomerPartyId)")}
              </label>
              <TextField
                value={customerPartyId}
                onChange={(val) => setCustomerPartyId(val)}
                placeholder={t("requests.customerPlaceholder")}
              />
            </div>

            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                {t("installedBase.title")}
              </label>
              <Select
                value={installedBaseId}
                onChange={(val) => setInstalledBaseId(val)}
                options={installedBaseOptions}
              />
            </div>
          </div>
        ) : (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                {t("workOrders.fixedAssetId")}
              </label>
              <TextField
                value={fixedAssetId}
                onChange={(val) => setFixedAssetId(val)}
                placeholder={t("installedBase.fixedAssetPlaceholder", "FA-00192")}
              />
            </div>

            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                {t("installedBase.title")}
              </label>
              <Select
                value={installedBaseId}
                onChange={(val) => setInstalledBaseId(val)}
                options={installedBaseOptions}
              />
            </div>
          </div>
        )}

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
