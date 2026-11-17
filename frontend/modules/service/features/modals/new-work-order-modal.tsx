import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { PartyPicker } from "../../../directory/public.ts";
import {
  useCreateWorkOrderMutation,
  useInstalledBaseQuery
} from "../../hooks/use-service-queries.ts";
import type {
  WorkOrderContext,
  WorkOrderType,
  ServicePriority,
  WorkOrderBillingOwner
} from "../../types.ts";

export interface NewWorkOrderModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewWorkOrderModal({
  isOpen,
  onClose,
  legalEntityId
}: NewWorkOrderModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const createMutation = useCreateWorkOrderMutation(legalEntityId);
  const installedBaseQuery = useInstalledBaseQuery(legalEntityId, { pageSize: 100 });
  const installedItems = installedBaseQuery.data?.items ?? [];

  const [context, setContext] = useState<WorkOrderContext>("CustomerFieldService");
  const [workType, setWorkType] = useState<WorkOrderType>("Corrective");
  const [priority, setPriority] = useState<ServicePriority>("Normal");
  const [billingOwner, setBillingOwner] = useState<WorkOrderBillingOwner>("None");
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [customerPartyId, setCustomerPartyId] = useState("");
  const [installedBaseId, setInstalledBaseId] = useState("");
  const [fixedAssetId, setFixedAssetId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [plannedStart, setPlannedStart] = useState("");
  const [plannedEnd, setPlannedEnd] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) {
      setErrorMsg(t("validation.subjectRequired", "يرجى إدخال عنوان أمر العمل"));
      return;
    }

    try {
      setErrorMsg(null);
      await createMutation.mutateAsync({
        legalEntityId,
        context,
        workType,
        priority,
        billingOwner,
        title: title.trim(),
        description: description.trim() || undefined,
        customerPartyId: context === "CustomerFieldService" && customerPartyId.trim() ? customerPartyId.trim() : undefined,
        installedBaseId: installedBaseId.trim() || undefined,
        fixedAssetId: context === "InternalMaintenance" && fixedAssetId.trim() ? fixedAssetId.trim() : undefined,
        projectId: billingOwner === "Projects" && projectId.trim() ? projectId.trim() : undefined,
        plannedStart: plannedStart.trim() ? new Date(plannedStart).toISOString() : undefined,
        plannedEnd: plannedEnd.trim() ? new Date(plannedEnd).toISOString() : undefined
      });
      onClose();
      setTitle("");
      setDescription("");
      setCustomerPartyId("");
      setInstalledBaseId("");
      setFixedAssetId("");
      setProjectId("");
      setPlannedStart("");
      setPlannedEnd("");
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
      title={t("workOrders.newWorkOrderTitle")}
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
              onChange={(val) => setContext(val as WorkOrderContext)}
              options={[
                { value: "CustomerFieldService", label: t("contexts.customer", "خدمة عملاء ميدانية") },
                { value: "InternalMaintenance", label: t("contexts.internal", "صيانة مرافق وأصول داخلية") }
              ]}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.workOrderType")} *
            </label>
            <Select
              value={workType}
              onChange={(val) => setWorkType(val as WorkOrderType)}
              options={[
                { value: "Corrective", label: t("workOrders.types.corrective") },
                { value: "Preventive", label: t("workOrders.types.preventive") },
                { value: "Installation", label: t("workOrders.types.installation") },
                { value: "Inspection", label: t("workOrders.types.inspection") },
                { value: "Emergency", label: t("workOrders.types.emergency") }
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
                { value: "Low", label: t("priorities.low") },
                { value: "Normal", label: t("priorities.medium") },
                { value: "High", label: t("priorities.high") },
                { value: "Critical", label: t("priorities.urgent") }
              ]}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.billingOwner", "جهة ملكية الفوترة")} *
            </label>
            <Select
              value={billingOwner}
              onChange={(val) => setBillingOwner(val as WorkOrderBillingOwner)}
              options={[
                { value: "None", label: t("billing.owners.none", "بدون فوترة (ضمان / داخلي)") },
                { value: "Service", label: t("billing.owners.service", "إدارة الخدمة (خاضع لفوترة الخدمة)") },
                { value: "Projects", label: t("billing.owners.projects", "إدارة المشاريع (تكامل مشاريع)") }
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
            <PartyPicker
              label={t("requests.customerName")}
              value={customerPartyId || undefined}
              onChange={(partyId) => setCustomerPartyId(partyId)}
              roleFilter="Customer"
              enableQuickCreate={false}
              placeholder={t("requests.customerPlaceholder")}
            />

            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                {t("installedBase.title")}
              </label>
              <Select
                value={installedBaseId}
                data-testid="work-order-installed-base-select"
                onChange={(val) => {
                  setInstalledBaseId(val);
                  const selected = installedItems.find((i) => i.id === val);
                  if (selected?.customerPartyId && !customerPartyId) {
                    setCustomerPartyId(selected.customerPartyId);
                  }
                }}
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

        {billingOwner === "Projects" && (
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.projectId", "معرف المشروع (ProjectId)")} *
            </label>
            <TextField
              value={projectId}
              onChange={(val) => setProjectId(val)}
              placeholder={t("workOrders.projectIdPlaceholder", "PRJ-UUID")}
            />
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.plannedStart", "تاريخ البدء المخطط")}
            </label>
            <TextField
              value={plannedStart}
              onChange={(val) => setPlannedStart(val)}
              placeholder={t("workOrders.dateTimePlaceholder", "YYYY-MM-DDTHH:mm")}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.plannedEnd", "تاريخ الانتهاء المخطط")}
            </label>
            <TextField
              value={plannedEnd}
              onChange={(val) => setPlannedEnd(val)}
              placeholder={t("workOrders.dateTimePlaceholder", "YYYY-MM-DDTHH:mm")}
            />
          </div>
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
            {t("workOrders.createWorkOrder")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
