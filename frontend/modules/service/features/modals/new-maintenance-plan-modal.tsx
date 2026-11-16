import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { useCreateMaintenancePlanMutation } from "../../hooks/use-service-queries.ts";

export interface NewMaintenancePlanModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewMaintenancePlanModal({
  isOpen,
  onClose,
  legalEntityId
}: NewMaintenancePlanModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const createPlanMutation = useCreateMaintenancePlanMutation(legalEntityId);

  const [planCode, setPlanCode] = useState(`PM-${Date.now().toString().slice(-5)}`);
  const [name, setName] = useState("");
  const [scheduleType, setScheduleType] = useState<"calendar" | "meter">("calendar");
  const [interval, setInterval] = useState("30");
  const [installedBaseItemId, setInstalledBaseItemId] = useState("");
  const [fixedAssetId, setFixedAssetId] = useState("");
  const [description, setDescription] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!planCode.trim() || !name.trim()) {
      setErrorMsg(t("validation.planCodeAndNameRequired"));
      return;
    }

    try {
      setErrorMsg(null);
      await (createPlanMutation.mutateAsync as any)({
        legalEntityId,
        planCode: planCode.trim(),
        name: name.trim(),
        scheduleType,
        calendarIntervalDays: scheduleType === "calendar" ? Number(interval) || 30 : undefined,
        meterInterval: scheduleType === "meter" ? Number(interval) || 500 : undefined,
        installedBaseItemId: installedBaseItemId.trim() || undefined,
        fixedAssetId: fixedAssetId.trim() || undefined,
        description: description.trim() || undefined
      });
      onClose();
      // Reset form
      setName("");
      setDescription("");
      setInstalledBaseItemId("");
      setFixedAssetId("");
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : t("common.genericError"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("preventive.newPlanTitle")}
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
              {t("preventive.planCode")} *
            </label>
            <TextField
              value={planCode}
              onChange={(val) => setPlanCode(val)}
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("preventive.scheduleType")} *
            </label>
            <Select
              value={scheduleType}
              onChange={(val) => setScheduleType(val as "calendar" | "meter")}
              options={[
                { value: "calendar", label: t("preventive.scheduleTypes.calendar") },
                { value: "meter", label: t("preventive.scheduleTypes.meter") }
              ]}
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("preventive.planName")} *
          </label>
          <TextField
            value={name}
            onChange={(val) => setName(val)}
            placeholder={t("preventive.planNamePlaceholder")}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {scheduleType === "calendar" ? t("preventive.intervalDays") : t("preventive.intervalMeterUnits")} *
            </label>
            <TextField
              value={interval}
              onChange={(val) => setInterval(val)}
              placeholder={scheduleType === "calendar" ? "30" : "500"}
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("installedBase.title")} / {t("workOrders.fixedAssetId")}
            </label>
            <TextField
              value={installedBaseItemId || fixedAssetId}
              onChange={(val) => {
                setInstalledBaseItemId(val);
                setFixedAssetId(val);
              }}
              placeholder={t("preventive.installedBasePlaceholder", "مثال: SN-GEN-88301")}
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
            placeholder={t("preventive.descriptionPlaceholder")}
            rows={3}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createPlanMutation.isPending}>
            {t("preventive.createPlan")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
