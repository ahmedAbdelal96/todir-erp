import React, { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { NoLegalEntityPrompt } from "../components/no-legal-entity-prompt.tsx";
import { useManufacturingScope } from "../hooks/use-manufacturing-scope.ts";
import { OperationActionDialog } from "../features/shop-floor/operation-action-dialog.tsx";
import { OutputReportDialog } from "../features/orders/output-report-dialog.tsx";
import { ScrapReportDialog } from "../features/orders/scrap-report-dialog.tsx";
import { usePlantsQuery, useWorkCentersQuery, useShopFloorQueueQuery, useOrdersQuery } from "../hooks/use-manufacturing.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { BdiText } from "../../../components/formatting/bidi-text.tsx";
import type { ProductionOperation } from "../types.ts";

export function ShopFloorPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const { legalEntityId, isReady } = useManufacturingScope();

  const { data: plants = [] } = usePlantsQuery(legalEntityId);
  const [selectedPlantId, setSelectedPlantId] = useState<string>("");
  const effectivePlantId = selectedPlantId || plants[0]?.id || "";

  const { data: workCenters = [] } = useWorkCentersQuery(effectivePlantId || undefined);
  const { data: orders = [] } = useOrdersQuery(legalEntityId);

  // Horizon & Status filters
  const [horizon, setHorizon] = useState<"activeOverdue" | "today" | "thisWeek" | "thisMonth">("activeOverdue");
  const [statusFilter, setStatusFilter] = useState<string>("");

  const { fromDate, toDate } = useMemo(() => {
    const now = new Date();
    const todayStr = now.toISOString().slice(0, 10);
    if (horizon === "today") {
      return { fromDate: todayStr, toDate: todayStr };
    }
    if (horizon === "thisWeek") {
      const nextWeek = new Date(now);
      nextWeek.setDate(nextWeek.getDate() + 7);
      return { fromDate: todayStr, toDate: nextWeek.toISOString().slice(0, 10) };
    }
    if (horizon === "thisMonth") {
      const nextMonth = new Date(now);
      nextMonth.setDate(nextMonth.getDate() + 30);
      return { fromDate: todayStr, toDate: nextMonth.toISOString().slice(0, 10) };
    }
    // "activeOverdue": includes all active previous orders and the next 14 days
    const past = new Date(now);
    past.setDate(past.getDate() - 60);
    const future = new Date(now);
    future.setDate(future.getDate() + 14);
    return { fromDate: past.toISOString().slice(0, 10), toDate: future.toISOString().slice(0, 10) };
  }, [horizon]);

  const { data: queue = [], isLoading } = useShopFloorQueueQuery(
    effectivePlantId,
    fromDate,
    toDate,
    statusFilter || undefined
  );

  const [selectedOperation, setSelectedOperation] = useState<{ op: ProductionOperation; action: "start" | "pause" | "resume" | "complete" } | null>(null);
  const [outputOrderId, setOutputOrderId] = useState<string | null>(null);
  const [scrapOrderId, setScrapOrderId] = useState<string | null>(null);

  if (!isReady) {
    return <NoLegalEntityPrompt />;
  }

  const getOrderNumber = (orderId: string): string => {
    const found = orders.find((o) => o.id === orderId);
    return found?.orderNumber ?? found?.number ?? "—";
  };

  return (
    <div
      data-testid="manufacturing-shop-floor-page"
      style={{
        padding: "1rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box",
        overflowX: "hidden"
      }}
    >
      <ManufacturingHeader
        title={t("shopFloor.title")}
        subtitle={t("shopFloor.subtitle")}
      />

      {/* Plant & Filter Bar */}
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
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

        <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
          <label style={{ fontSize: "13px", fontWeight: 600, color: "#475569" }}>
            {t("shopFloor.filterHorizon")}:
          </label>
          <select
            value={horizon}
            onChange={(e) => setHorizon(e.target.value as any)}
            style={{
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              background: "#ffffff"
            }}
          >
            <option value="activeOverdue">{t("shopFloor.horizonActiveOverdue")}</option>
            <option value="today">{t("shopFloor.horizonToday")}</option>
            <option value="thisWeek">{t("shopFloor.horizonWeek")}</option>
            <option value="thisMonth">{t("shopFloor.horizonMonth")}</option>
          </select>
        </div>

        <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
          <label style={{ fontSize: "13px", fontWeight: 600, color: "#475569" }}>
            {t("shopFloor.filterState")}:
          </label>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            style={{
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              background: "#ffffff"
            }}
          >
            <option value="">{t("shopFloor.allStates")}</option>
            <option value="InProgress">{t("status.InProgress")}</option>
            <option value="Ready">{t("status.Ready")}</option>
            <option value="Paused">{t("status.Paused")}</option>
            <option value="Completed">{t("status.Completed")}</option>
          </select>
        </div>

        <div style={{ fontSize: "13px", color: "#64748b", marginInlineStart: "auto" }}>
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
            <div style={{ fontSize: "32px", marginBottom: "8px" }}>🏭</div>
            <h4 style={{ margin: "0 0 6px", fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
              {isLoading ? t("common.loading") : t("shopFloor.noOperationsScheduled")}
            </h4>
            <p style={{ margin: "0 0 16px", fontSize: "13px", color: "#64748b", maxWidth: "500px", marginInline: "auto" }}>
              {t("shopFloor.emptyFilterAdvice")}
            </p>
            {(horizon !== "activeOverdue" || statusFilter !== "") && (
              <Button
                variant="secondary"
                size="small"
                onClick={() => {
                  setHorizon("activeOverdue");
                  setStatusFilter("");
                }}
              >
                {t("shopFloor.resetFilters")}
              </Button>
            )}
          </div>
        ) : (
          queue.map((op) => {
            const wc = workCenters.find((w) => w.id === op.workCenterId);
            const orderNumber = getOrderNumber(op.productionOrderId);

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
                    {t("shopFloor.orderRef")}: <strong style={{ color: "#0284c7" }}><BdiText>{orderNumber}</BdiText></strong> • {t("shopFloor.actualTime")}: {(op.actualLaborMinutes ?? op.actualMinutes ?? 0) + (op.actualMachineMinutes ?? 0)} {t("shopFloor.minutesLong")}
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
