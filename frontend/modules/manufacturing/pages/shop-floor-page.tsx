import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { NoLegalEntityPrompt } from "../components/no-legal-entity-prompt.tsx";
import { useManufacturingScope } from "../hooks/use-manufacturing-scope.ts";
import { OperationActionDialog } from "../features/shop-floor/operation-action-dialog.tsx";
import { OutputReportDialog } from "../features/orders/output-report-dialog.tsx";
import { ScrapReportDialog } from "../features/orders/scrap-report-dialog.tsx";
import { usePlantsQuery, useWorkCentersQuery, useShopFloorQueueQuery } from "../hooks/use-manufacturing.ts";
import { Button } from "../../../components/primitives/button.tsx";
import type { ProductionOperation } from "../types.ts";

export function ShopFloorPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const { legalEntityId, isReady } = useManufacturingScope();

  const { data: plants = [] } = usePlantsQuery(legalEntityId);
  const [selectedPlantId, setSelectedPlantId] = useState<string>("");
  const effectivePlantId = selectedPlantId || plants[0]?.id || "";

  const { data: workCenters = [] } = useWorkCentersQuery(effectivePlantId || undefined);

  // Today horizon
  const today = new Date().toISOString().slice(0, 10);
  const nextWeek = (() => {
    const d = new Date();
    d.setDate(d.getDate() + 7);
    return d.toISOString().slice(0, 10);
  })();

  const { data: queue = [], isLoading } = useShopFloorQueueQuery(effectivePlantId, today, nextWeek);

  const [selectedOperation, setSelectedOperation] = useState<{ op: ProductionOperation; action: "start" | "pause" | "resume" | "complete" } | null>(null);
  const [outputOrderId, setOutputOrderId] = useState<string | null>(null);
  const [scrapOrderId, setScrapOrderId] = useState<string | null>(null);

  if (!isReady) {
    return <NoLegalEntityPrompt />;
  }

  return (
    <div
      data-testid="manufacturing-shop-floor-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box"
      }}
    >
      <ManufacturingHeader
        title={t("shopFloor.title")}
        subtitle={t("shopFloor.subtitle")}
      />

      {/* Plant & Center Selector Bar */}
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
            {t("orders.plant")}:
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

        <div style={{ fontSize: "13px", color: "#64748b" }}>
          {t("shopFloor.registeredCenters")}: <strong>{workCenters.length}</strong>
        </div>
      </div>

      {/* Shop Floor Queue Cards */}
      <div style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
        {queue.length === 0 ? (
          <div
            style={{
              background: "#ffffff",
              border: "1px solid #cbd5e1",
              borderRadius: "8px",
              padding: "48px 16px",
              textAlign: "center",
              color: "#64748b"
            }}
          >
            {isLoading ? t("common.loading") : t("shopFloor.noOperationsScheduled")}
          </div>
        ) : (
          queue.map((op) => {
            const wc = workCenters.find((w) => w.id === op.workCenterId);
            return (
              <div
                key={op.id}
                style={{
                  background: "#ffffff",
                  border: "1px solid #cbd5e1",
                  borderRadius: "8px",
                  padding: "16px",
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  flexWrap: "wrap",
                  gap: "12px",
                  boxShadow: "0 1px 2px rgba(0,0,0,0.04)"
                }}
              >
                <div>
                  <div style={{ display: "flex", alignItems: "center", gap: "8px", marginBottom: "4px" }}>
                    <span style={{ fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
                      #{op.sequenceNo} {op.name} {op.code ? `(${op.code})` : ""}
                    </span>
                    <ManufacturingStatusBadge state={op.state} />
                  </div>
                  <div style={{ fontSize: "13px", color: "#64748b" }}>
                    {t("shopFloor.center")}: <strong>{wc ? `${wc.code} - ${wc.name}` : t("shopFloor.generalNotSpecified")}</strong> • {t("shopFloor.setupTime")}: {op.standardSetupMinutes ?? 0} {t("shopFloor.minutesUnit")} • {t("shopFloor.runTimeUnit")}: {op.standardRunMinutesPerUnit ?? 0} {t("shopFloor.minutesUnit")}
                  </div>
                  <div style={{ fontSize: "13px", color: "#334155", marginTop: "4px" }}>
                    {t("shopFloor.orderRef")}: <code>{op.productionOrderId.slice(0, 8)}</code> • {t("shopFloor.actualTime")}: {(op.actualLaborMinutes ?? op.actualMinutes ?? 0) + (op.actualMachineMinutes ?? 0)} {t("shopFloor.minutesLong")}
                  </div>
                </div>

                <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
                  {op.state === "Ready" && (
                    <Button
                      variant="primary"
                      onClick={() => setSelectedOperation({ op, action: "start" })}
                    >
                      {t("shopFloor.startOperation")}
                    </Button>
                  )}
                  {op.state === "InProgress" && (
                    <>
                      <Button
                        variant="secondary"
                        onClick={() => setSelectedOperation({ op, action: "pause" })}
                      >
                        {t("shopFloor.pauseOperation")}
                      </Button>
                      <Button
                        variant="primary"
                        onClick={() => setSelectedOperation({ op, action: "complete" })}
                      >
                        {t("shopFloor.completeOperation")}
                      </Button>
                      <Button
                        variant="secondary"
                        onClick={() => setOutputOrderId(op.productionOrderId)}
                      >
                        {t("shopFloor.recordOutput")}
                      </Button>
                      <Button
                        variant="secondary"
                        onClick={() => setScrapOrderId(op.productionOrderId)}
                      >
                        {t("shopFloor.reportScrap")}
                      </Button>
                    </>
                  )}
                  {op.state === "Paused" && (
                    <Button
                      variant="primary"
                      onClick={() => setSelectedOperation({ op, action: "resume" })}
                    >
                      {t("shopFloor.resumeOperation")}
                    </Button>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>

      {selectedOperation && (
        <OperationActionDialog
          isOpen={Boolean(selectedOperation)}
          onClose={() => setSelectedOperation(null)}
          orderId={selectedOperation.op.productionOrderId}
          operationId={selectedOperation.op.id}
          operationName={selectedOperation.op.name}
          action={selectedOperation.action}
          currentVersion={selectedOperation.op.version ?? selectedOperation.op.rowVersion ?? 1}
        />
      )}

      {outputOrderId && (
        <OutputReportDialog
          isOpen={Boolean(outputOrderId)}
          onClose={() => setOutputOrderId(null)}
          orderId={outputOrderId}
        />
      )}

      {scrapOrderId && (
        <ScrapReportDialog
          isOpen={Boolean(scrapOrderId)}
          onClose={() => setScrapOrderId(null)}
          orderId={scrapOrderId}
        />
      )}
    </div>
  );
}
