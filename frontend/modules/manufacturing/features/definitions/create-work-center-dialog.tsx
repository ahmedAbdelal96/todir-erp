import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useCreateWorkCenterMutation, useCreateWorkCenterCalendarMutation, usePlantsQuery } from "../../hooks/use-manufacturing.ts";
import type { CapacityModel } from "../../types.ts";

export interface CreateWorkCenterDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function CreateWorkCenterDialog({ isOpen, onClose, legalEntityId }: CreateWorkCenterDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const createWorkCenterMutation = useCreateWorkCenterMutation();
  const createCalendarMutation = useCreateWorkCenterCalendarMutation();
  const plantsQuery = usePlantsQuery(legalEntityId);

  const [plantId, setPlantId] = useState("");
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [capacityModel, setCapacityModel] = useState<CapacityModel>("Finite");
  const [defaultEfficiency, setDefaultEfficiency] = useState("1.0");

  // Optional initial shift
  const [addCalendarShift, setAddCalendarShift] = useState(true);
  const [shiftStart, setShiftStart] = useState("08:00");
  const [shiftEnd, setShiftEnd] = useState("16:30");
  const [breakMinutes, setBreakMinutes] = useState("30");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const effectivePlantId = plantId || plantsQuery.data?.[0]?.id;
    if (!effectivePlantId || !code.trim() || !name.trim()) return;

    createWorkCenterMutation.mutate(
      {
        plantId: effectivePlantId,
        code: code.trim().toUpperCase(),
        name: name.trim(),
        capacityModel,
        defaultEfficiency: parseFloat(defaultEfficiency) || 1.0
      },
      {
        onSuccess: (workCenter) => {
          if (addCalendarShift) {
            // Add Monday-Thursday standard shifts (1 to 5)
            for (let day = 1; day <= 5; day++) {
              createCalendarMutation.mutate({
                workCenterId: workCenter.id,
                request: {
                  weekday: day,
                  shiftStart: `${shiftStart}:00`,
                  shiftEnd: `${shiftEnd}:00`,
                  effectiveFrom: new Date().toISOString().slice(0, 10),
                  breakMinutes: parseInt(breakMinutes, 10) || 30
                }
              });
            }
          }
          onClose();
        }
      }
    );
  };

  const isLoading = createWorkCenterMutation.isPending || createCalendarMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("definitions.addWorkCenter")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
          <label style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b" }}>
            {t("orders.plant")}
          </label>
          <select
            value={plantId || plantsQuery.data?.[0]?.id || ""}
            onChange={(e) => setPlantId(e.target.value)}
            style={{
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              background: "#ffffff"
            }}
            required
          >
            {plantsQuery.data?.map((plant) => (
              <option key={plant.id} value={plant.id}>
                {plant.code} - {plant.name}
              </option>
            ))}
          </select>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("definitions.workCenterCode")}
            value={code}
            onChange={(e) => setCode(e.target.value)}
            required
          />
          <TextField
            label={t("definitions.workCenterName")}
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            <label style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b" }}>
              {t("definitions.capacityModel")}
            </label>
            <select
              value={capacityModel}
              onChange={(e) => setCapacityModel(e.target.value as CapacityModel)}
              style={{
                padding: "8px 12px",
                borderRadius: "6px",
                border: "1px solid #cbd5e1",
                fontSize: "14px",
                background: "#ffffff"
              }}
            >
              <option value="Finite">{t("definitions.modelFinite")}</option>
              <option value="Infinite">{t("definitions.modelInfinite")}</option>
              <option value="RoughCut">{t("definitions.modelRoughCut")}</option>
            </select>
          </div>
          <TextField
            label={t("common.efficiency")}
            type="number"
            value={defaultEfficiency}
            onChange={(e) => setDefaultEfficiency(e.target.value)}
          />
        </div>

        <div style={{ borderTop: "1px solid #cbd5e1", paddingTop: "12px" }}>
          <label style={{ display: "flex", alignItems: "center", gap: "8px", fontSize: "13px", fontWeight: 600, color: "#1e293b", cursor: "pointer", marginBottom: "8px" }}>
            <input
              type="checkbox"
              checked={addCalendarShift}
              onChange={(e) => setAddCalendarShift(e.target.checked)}
            />
            {t("definitions.defaultCalendar")}
          </label>

          {addCalendarShift && (
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "8px", background: "#f8fafc", padding: "10px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
              <TextField
                label={t("common.shiftStart")}
                type="time"
                value={shiftStart}
                onChange={(e) => setShiftStart(e.target.value)}
              />
              <TextField
                label={t("common.shiftEnd")}
                type="time"
                value={shiftEnd}
                onChange={(e) => setShiftEnd(e.target.value)}
              />
              <TextField
                label={t("common.breakMinutes")}
                type="number"
                value={breakMinutes}
                onChange={(e) => setBreakMinutes(e.target.value)}
              />
            </div>
          )}
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isLoading}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" disabled={isLoading || !code || !name}>
            {isLoading ? t("common.loading") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
