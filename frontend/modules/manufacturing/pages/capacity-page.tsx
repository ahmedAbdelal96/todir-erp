import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { usePlantsQuery, useWorkCentersQuery, useCapacityQuery } from "../hooks/use-manufacturing.ts";

export function CapacityPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const legalEntityId = outletCtx?.legalEntityId ?? "00000000-0000-0000-0000-000000000001";

  const { data: plants = [] } = usePlantsQuery(legalEntityId);
  const [selectedPlantId, setSelectedPlantId] = useState<string>("");
  const effectivePlantId = selectedPlantId || plants[0]?.id || "";

  const { data: workCenters = [] } = useWorkCentersQuery(effectivePlantId || undefined);

  const today = new Date().toISOString().slice(0, 10);
  const nextMonth = (() => {
    const d = new Date();
    d.setDate(d.getDate() + 30);
    return d.toISOString().slice(0, 10);
  })();

  const { data: capacity = [] } = useCapacityQuery(effectivePlantId, today, nextMonth);

  return (
    <div
      data-testid="manufacturing-capacity-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box"
      }}
    >
      <ManufacturingHeader
        title={t("capacity.title", "الطاقة الإنتاجية ومراكز العمل")}
        subtitle={t("capacity.subtitle", "تخطيط ومراقبة سعة مراكز العمل، وساعات التوقف للصيانة، وحمل التشغيل")}
      />

      {/* Plant Selector */}
      <div
        style={{
          display: "flex",
          gap: "12px",
          alignItems: "center",
          background: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "8px",
          padding: "12px 16px",
          marginBottom: "16px"
        }}
      >
        <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
          <label style={{ fontSize: "13px", fontWeight: 600, color: "#475569" }}>
            {t("orders.plant", "المصنع:")}
          </label>
          <select
            value={effectivePlantId}
            onChange={(e) => setSelectedPlantId(e.target.value)}
            style={{
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              background: "#ffffff"
            }}
          >
            {plants.map((p) => (
              <option key={p.id} value={p.id}>
                {p.code} - {p.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Capacity Cards */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "16px" }}>
        {workCenters.map((wc) => (
          <div
            key={wc.id}
            style={{
              background: "#ffffff",
              border: "1px solid #cbd5e1",
              borderRadius: "8px",
              padding: "16px",
              boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "8px" }}>
              <h3 style={{ margin: 0, fontSize: "15px", fontWeight: 700, color: "#102a43" }}>
                {wc.name} ({wc.code})
              </h3>
              <ManufacturingStatusBadge state={wc.state} />
            </div>

            <div style={{ fontSize: "13px", color: "#64748b", marginBottom: "12px" }}>
              النموذج: <strong>{wc.capacityModel}</strong> • الكفاءة الافتراضية: <strong>{((wc.defaultEfficiency ?? 1) * 100).toFixed(0)}%</strong>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "8px", background: "#f8fafc", padding: "10px", borderRadius: "6px" }}>
              <div>
                <div style={{ fontSize: "12px", color: "#64748b" }}>{t("capacity.availableHours", "الساعات المتاحة")}</div>
                <div style={{ fontSize: "16px", fontWeight: 700, color: "#15803d" }}>160 س</div>
              </div>
              <div>
                <div style={{ fontSize: "12px", color: "#64748b" }}>{t("capacity.plannedHours", "ساعات محملة")}</div>
                <div style={{ fontSize: "16px", fontWeight: 700, color: "#0284c7" }}>35 س</div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
