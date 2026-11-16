import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceLifecycleBadge } from "../../components/service-lifecycle-badge.tsx";
import {
  TruckIcon,
  ClockIcon,
  CheckCircleIcon,
  ClipboardCheckIcon,
  WrenchIcon,
  AlertCircleIcon
} from "../../components/service-icons.tsx";
import {
  useVisitsQuery,
  useStartVisitTravelMutation,
  useArriveVisitMutation,
  useStartVisitMutation,
  useCompleteVisitMutation
} from "../../hooks/use-service-queries.ts";
import { AnswerChecklistModal } from "../modals/answer-checklist-modal.tsx";
import { RecordDiagnosisModal } from "../modals/record-diagnosis-modal.tsx";
import { CreateReportModal } from "../modals/create-report-modal.tsx";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";
import type { VisitDto } from "../../types.ts";

export interface MyDayTabProps {
  readonly legalEntityId: string;
}

export function MyDayTab({ legalEntityId }: MyDayTabProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const isMobile = useIsMobile(768);

  const [technicianFilter, setTechnicianFilter] = useState("TECH-001");
  const [selectedVisitForChecklist, setSelectedVisitForChecklist] = useState<VisitDto | null>(null);
  const [selectedVisitForDiagnosis, setSelectedVisitForDiagnosis] = useState<VisitDto | null>(null);
  const [selectedVisitForReport, setSelectedVisitForReport] = useState<VisitDto | null>(null);

  // Queries & Mutations
  const visitsQuery = useVisitsQuery(legalEntityId, { pageSize: 50 });
  const startTravelMutation = useStartVisitTravelMutation(legalEntityId);
  const arriveMutation = useArriveVisitMutation(legalEntityId);
  const startWorkMutation = useStartVisitMutation(legalEntityId);
  const completeMutation = useCompleteVisitMutation(legalEntityId);

  const visits = visitsQuery.data?.items ?? [];

  // Active / upcoming visits
  const activeVisits = visits.filter(
    (v) => v.status !== "completed" && v.status !== "canceled"
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Technician Field Header */}
      <Surface
        title={t("technician.myDayTitle")}
        description={t("technician.myDaySubtitle")}
        variant="card"
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "1rem",
            padding: "0.5rem 0"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <span style={{ fontSize: "0.875rem", fontWeight: 600 }}>{t("dispatch.technicianId")}:</span>
            <div style={{ width: "160px" }}>
              <TextField
                value={technicianFilter}
                onChange={(val) => setTechnicianFilter(val)}
                placeholder={t("fields.technicianPlaceholder", "TECH-001")}
              />
            </div>
          </div>

          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "0.5rem",
              padding: "0.375rem 0.75rem",
              borderRadius: "9999px",
              backgroundColor: "#ecfdf5",
              border: "1px solid #a7f3d0",
              fontSize: "0.75rem",
              fontWeight: 600,
              color: "#065f46"
            }}
          >
            <span style={{ width: "8px", height: "8px", borderRadius: "50%", backgroundColor: "#059669" }} />
            {t("technician.onlineReady")}
          </div>
        </div>
      </Surface>

      {/* Assigned Visits Cards */}
      {visitsQuery.isLoading ? (
        <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
          {t("common.loading")}
        </div>
      ) : activeVisits.length === 0 ? (
        <Surface variant="card">
          <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
            <p style={{ margin: "0 0 0.5rem", fontSize: "1rem", fontWeight: 600 }}>
              {t("technician.noVisitsScheduledToday")}
            </p>
            <p style={{ margin: 0, fontSize: "0.875rem" }}>
              {t("technician.enjoyYourDay")}
            </p>
          </div>
        </Surface>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          {activeVisits.map((visit) => {
            const isTraveling = visit.status === "traveling";
            const isArrived = visit.status === "arrived";
            const isInProgress = visit.status === "in_progress";
            const isScheduled = visit.status === "scheduled";

            return (
              <Surface key={visit.id} variant="card" padding="normal">
                <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
                  {/* Card Header */}
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "flex-start",
                      flexWrap: "wrap",
                      gap: "0.5rem"
                    }}
                  >
                    <div>
                      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                        <strong style={{ fontSize: "1rem", color: "#0f172a" }}>
                          {visit.visitNumber || visit.id.slice(0, 8)}
                        </strong>
                        <span style={{ fontSize: "0.8125rem", color: "#64748b" }}>
                          ({(t as any)(`dispatch.types.${(visit.visitType || visit.locationType || "onsite").toLowerCase()}`)})
                        </span>
                      </div>
                      <div style={{ marginTop: "0.25rem", fontSize: "0.8125rem", color: "#64748b" }}>
                        {visit.scheduledStartUtc || visit.plannedStart ? new Date(visit.scheduledStartUtc || visit.plannedStart).toLocaleTimeString() : "—"} —{" "}
                        {visit.scheduledEndUtc || visit.plannedEnd ? new Date(visit.scheduledEndUtc || visit.plannedEnd).toLocaleTimeString() : "—"}
                      </div>
                    </div>

                    <ServiceLifecycleBadge state={visit.state || visit.status} />
                  </div>

                  {/* Operational Notes */}
                  {(visit.notes || visit.siteInstructions) && (
                    <div
                      style={{
                        padding: "0.75rem",
                        borderRadius: "0.375rem",
                        backgroundColor: "#f8fafc",
                        border: "1px solid #e2e8f0",
                        fontSize: "0.8125rem",
                        color: "#334155"
                      }}
                    >
                      {visit.notes || visit.siteInstructions}
                    </div>
                  )}

                  {/* Field Execution Action Pipeline */}
                  <div
                    style={{
                      display: "flex",
                      flexWrap: "wrap",
                      gap: "0.5rem",
                      paddingTop: "0.5rem",
                      borderTop: "1px solid #f1f5f9"
                    }}
                  >
                    {isScheduled && (
                      <Button
                        variant="primary"
                        onClick={() => startTravelMutation.mutate({ legalEntityId, visitId: visit.id })}
                        isLoading={startTravelMutation.isPending}
                        startIcon={<TruckIcon size={16} />}
                      >
                        {t("technician.actions.startTravel")}
                      </Button>
                    )}

                    {isTraveling && (
                      <Button
                        variant="primary"
                        onClick={() => arriveMutation.mutate({ legalEntityId, visitId: visit.id })}
                        isLoading={arriveMutation.isPending}
                        startIcon={<CheckCircleIcon size={16} />}
                      >
                        {t("technician.actions.arriveOnSite")}
                      </Button>
                    )}

                    {isArrived && (
                      <Button
                        variant="primary"
                        onClick={() => startWorkMutation.mutate({ legalEntityId, visitId: visit.id })}
                        isLoading={startWorkMutation.isPending}
                        startIcon={<WrenchIcon size={16} />}
                      >
                        {t("technician.actions.startWork")}
                      </Button>
                    )}

                    {isInProgress && (
                      <Button
                        variant="primary"
                        onClick={() => completeMutation.mutate({ legalEntityId, visitId: visit.id })}
                        isLoading={completeMutation.isPending}
                        startIcon={<CheckCircleIcon size={16} />}
                      >
                        {t("technician.actions.completeVisit")}
                      </Button>
                    )}

                    {/* In-field Quick Tools */}
                    <Button
                      variant="secondary"
                      size="compact"
                      onClick={() => setSelectedVisitForChecklist(visit)}
                      startIcon={<ClipboardCheckIcon size={14} />}
                    >
                      {t("workOrders.answerChecklistTitle")}
                    </Button>

                    <Button
                      variant="secondary"
                      size="compact"
                      onClick={() => setSelectedVisitForDiagnosis(visit)}
                      startIcon={<AlertCircleIcon size={14} />}
                    >
                      {t("workOrders.recordDiagnosisTitle")}
                    </Button>

                    <Button
                      variant="secondary"
                      size="compact"
                      onClick={() => setSelectedVisitForReport(visit)}
                      startIcon={<CheckCircleIcon size={14} />}
                    >
                      {t("reports.createReportTitle")}
                    </Button>
                  </div>
                </div>
              </Surface>
            );
          })}
        </div>
      )}

      {/* Field Modals */}
      {selectedVisitForChecklist && (
        <AnswerChecklistModal
          isOpen={Boolean(selectedVisitForChecklist)}
          onClose={() => setSelectedVisitForChecklist(null)}
          legalEntityId={legalEntityId}
          workOrderId={selectedVisitForChecklist.workOrderId}
        />
      )}

      {selectedVisitForDiagnosis && (
        <RecordDiagnosisModal
          isOpen={Boolean(selectedVisitForDiagnosis)}
          onClose={() => setSelectedVisitForDiagnosis(null)}
          legalEntityId={legalEntityId}
          workOrderId={selectedVisitForDiagnosis.workOrderId}
          visitId={selectedVisitForDiagnosis.id}
        />
      )}

      {selectedVisitForReport && (
        <CreateReportModal
          isOpen={Boolean(selectedVisitForReport)}
          onClose={() => setSelectedVisitForReport(null)}
          legalEntityId={legalEntityId}
          workOrderId={selectedVisitForReport.workOrderId}
        />
      )}
    </div>
  );
}
