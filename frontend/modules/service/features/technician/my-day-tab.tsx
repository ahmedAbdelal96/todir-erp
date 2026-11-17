import React, { useState, useEffect } from "react";
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
  useMyDayQuery,
  useTransitionVisitMutation,
  useSubmitOfflineCommandMutation
} from "../../hooks/use-service-queries.ts";
import { AnswerChecklistModal } from "../modals/answer-checklist-modal.tsx";
import { RecordDiagnosisModal } from "../modals/record-diagnosis-modal.tsx";
import { CreateReportModal } from "../modals/create-report-modal.tsx";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";
import type { VisitView, WorkOrderView } from "../../types.ts";
import {
  loadOfflineQueue,
  enqueueOfflineCommand,
  replayOfflineQueue,
  clearSyncedCommands,
  dismissOfflineCommand,
  type StoredOfflineCommand
} from "../../utils/offline-queue.ts";

export interface MyDayTabProps {
  readonly legalEntityId: string;
}

export function MyDayTab({ legalEntityId }: MyDayTabProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const isMobile = useIsMobile(768);

  const todayStr = new Date().toISOString().slice(0, 10);
  const [selectedDate, setSelectedDate] = useState<string>(todayStr);
  const [isOnline, setIsOnline] = useState<boolean>(typeof navigator !== "undefined" ? navigator.onLine : true);
  const [offlineQueue, setOfflineQueue] = useState<StoredOfflineCommand[]>(() => loadOfflineQueue(legalEntityId));
  const [isSyncing, setIsSyncing] = useState<boolean>(false);

  const [selectedVisitForChecklist, setSelectedVisitForChecklist] = useState<VisitView | null>(null);
  const [selectedVisitForDiagnosis, setSelectedVisitForDiagnosis] = useState<VisitView | null>(null);
  const [selectedVisitForReport, setSelectedVisitForReport] = useState<VisitView | null>(null);

  const refreshQueue = React.useCallback(() => {
    setOfflineQueue(loadOfflineQueue(legalEntityId));
  }, [legalEntityId]);

  const handleSyncQueue = React.useCallback(async () => {
    if (isSyncing) return;
    setIsSyncing(true);
    try {
      await replayOfflineQueue(legalEntityId, (req) => submitOfflineMutation.mutateAsync(req));
      refreshQueue();
      myDayQuery.refetch();
    } finally {
      setIsSyncing(false);
    }
  }, [legalEntityId, isSyncing]);

  // Connectivity listener
  useEffect(() => {
    const handleOnline = () => {
      setIsOnline(true);
      handleSyncQueue();
    };
    const handleOffline = () => setIsOnline(false);
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);
    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, [handleSyncQueue]);

  // Authoritative authenticated user My Day query
  const myDayQuery = useMyDayQuery(selectedDate);
  const transitionMutation = useTransitionVisitMutation();
  const submitOfflineMutation = useSubmitOfflineCommandMutation();

  const visits = myDayQuery.data?.visits ?? [];
  const workOrders = myDayQuery.data?.workOrders ?? [];
  const workOrderMap = new Map<string, WorkOrderView>(workOrders.map((wo) => [wo.id, wo]));

  const handleVisitAction = async (
    visit: VisitView,
    action: "dispatch" | "travel" | "arrive" | "start" | "complete" | "no-access" | "cancel"
  ) => {
    if (!isOnline) {
      // Store in durable offline queue in localStorage
      enqueueOfflineCommand(legalEntityId, {
        commandType: `visit.${action}`,
        visitId: visit.id,
        workOrderId: visit.workOrderId,
        expectedVersion: visit.version,
        payloadJson: JSON.stringify({ action, occurredAt: new Date().toISOString() })
      });
      refreshQueue();
      return;
    }

    await transitionMutation.mutateAsync({
      visitId: visit.id,
      action,
      request: { version: visit.version }
    });
  };

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
            <span style={{ fontSize: "0.875rem", fontWeight: 600 }}>{t("technician.scheduleDate", "تاريخ العمل الميداني")}:</span>
            <div style={{ width: "160px" }}>
              <TextField
                value={selectedDate}
                onChange={(val) => setSelectedDate(val)}
                placeholder={t("technician.datePlaceholder", "YYYY-MM-DD")}
              />
            </div>
            <Button
              variant="secondary"
              size="compact"
              onClick={() => setSelectedDate(todayStr)}
            >
              {t("common.today", "اليوم")}
            </Button>
          </div>

          {/* Genuine Connectivity Badge */}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "0.5rem",
              padding: "0.375rem 0.75rem",
              borderRadius: "9999px",
              backgroundColor: isOnline ? "#ecfdf5" : "#fef2f2",
              border: `1px solid ${isOnline ? "#a7f3d0" : "#fecaca"}`,
              fontSize: "0.75rem",
              fontWeight: 600,
              color: isOnline ? "#065f46" : "#b91c1c"
            }}
          >
            <span
              style={{
                width: "8px",
                height: "8px",
                borderRadius: "50%",
                backgroundColor: isOnline ? "#059669" : "#dc2626"
              }}
            />
            {isOnline ? t("technician.onlineReady") : t("technician.offlineActive", "غير متصل (يعمل محلياً)")}
          </div>
        </div>
      </Surface>

      {/* Durable Offline Queue UI */}
      {offlineQueue.length > 0 && (
        <Surface
          title={t("technician.offlineQueueTitle")}
          description={t("technician.offlineQueueDesc")}
          headerActions={
            <div style={{ display: "flex", gap: "0.5rem" }}>
              <Button
                variant="primary"
                size="compact"
                isDisabled={!isOnline || isSyncing}
                isLoading={isSyncing}
                onClick={handleSyncQueue}
              >
                {t("technician.syncNow")}
              </Button>
              <Button
                variant="ghost"
                size="compact"
                onClick={() => {
                  clearSyncedCommands(legalEntityId);
                  refreshQueue();
                }}
              >
                {t("technician.clearSynced")}
              </Button>
            </div>
          }
          variant="card"
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            {offlineQueue.map((cmd) => (
              <div
                key={cmd.clientCommandId}
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  padding: "0.75rem 1rem",
                  borderRadius: "0.375rem",
                  backgroundColor: cmd.status === "conflict" ? "#fef2f2" : cmd.status === "synced" ? "#f0fdf4" : "#f8fafc",
                  border: `1px solid ${cmd.status === "conflict" ? "#fecaca" : cmd.status === "synced" ? "#bbf7d0" : "#e2e8f0"}`
                }}
              >
                <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
                  <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                    <strong style={{ fontSize: "0.875rem" }}>{cmd.commandType}</strong>
                    <span
                      style={{
                        padding: "0.15rem 0.4rem",
                        borderRadius: "0.25rem",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor: cmd.status === "synced" ? "#dcfce7" : cmd.status === "conflict" ? "#fee2e2" : "#e0e7ff",
                        color: cmd.status === "synced" ? "#166534" : cmd.status === "conflict" ? "#991b1b" : "#3730a3"
                      }}
                    >
                      {cmd.status === "synced"
                        ? t("technician.statusSynced")
                        : cmd.status === "conflict"
                        ? t("technician.statusConflict")
                        : cmd.status === "syncing"
                        ? t("technician.statusSyncing")
                        : t("technician.statusQueued")}
                    </span>
                  </div>
                  {cmd.errorMessage && (
                    <span style={{ fontSize: "0.75rem", color: "#dc2626" }}>{cmd.errorMessage}</span>
                  )}
                  <span style={{ fontSize: "0.75rem", color: "#94a3b8" }}>
                    {new Date(cmd.queuedAt).toLocaleTimeString()}
                  </span>
                </div>
                {(cmd.status === "conflict" || cmd.status === "failed") && (
                  <Button
                    variant="ghost"
                    size="compact"
                    onClick={() => {
                      dismissOfflineCommand(legalEntityId, cmd.clientCommandId);
                      refreshQueue();
                    }}
                  >
                    {t("common.delete")}
                  </Button>
                )}
              </div>
            ))}
          </div>
        </Surface>
      )}

      {/* Assigned Visits Cards */}
      {myDayQuery.isLoading ? (
        <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
          {t("common.loading")}
        </div>
      ) : visits.length === 0 ? (
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
          {visits.map((visit) => {
            const wo = workOrderMap.get(visit.workOrderId);
            const state = visit.state;
            const isPlanned = state === "Planned";
            const isDispatched = state === "Dispatched";
            const isTraveling = state === "Traveling";
            const isArrived = state === "Arrived";
            const isInProgress = state === "InProgress";

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
                          {wo?.workOrderNumber || visit.id.slice(0, 8)}
                        </strong>
                        <span style={{ fontSize: "0.8125rem", color: "#64748b" }}>
                          ({visit.locationType})
                        </span>
                        {wo && (
                          <span
                            style={{
                              padding: "0.15rem 0.4rem",
                              borderRadius: "4px",
                              fontSize: "0.75rem",
                              fontWeight: 600,
                              backgroundColor: wo.priority === "High" || wo.priority === "Critical" ? "#fef2f2" : "#f1f5f9",
                              color: wo.priority === "High" || wo.priority === "Critical" ? "#b91c1c" : "#475569"
                            }}
                          >
                            {wo.priority}
                          </span>
                        )}
                      </div>
                      {wo && (
                        <div style={{ marginTop: "0.25rem", fontSize: "0.875rem", fontWeight: 600, color: "#1e293b" }}>
                          {wo.title}
                        </div>
                      )}
                      <div style={{ marginTop: "0.25rem", fontSize: "0.8125rem", color: "#64748b" }}>
                        {new Date(visit.plannedStart).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} —{" "}
                        {new Date(visit.plannedEnd).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                      </div>
                    </div>

                    <ServiceLifecycleBadge state={visit.state} />
                  </div>

                  {/* Operational Notes */}
                  {visit.siteInstructions && (
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
                      {visit.siteInstructions}
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
                    {(isPlanned || isDispatched) && (
                      <Button
                        variant="primary"
                        onClick={() => handleVisitAction(visit, "travel")}
                        isLoading={transitionMutation.isPending}
                        startIcon={<TruckIcon size={16} />}
                      >
                        {t("technician.actions.startTravel")}
                      </Button>
                    )}

                    {isTraveling && (
                      <Button
                        variant="primary"
                        onClick={() => handleVisitAction(visit, "arrive")}
                        isLoading={transitionMutation.isPending}
                        startIcon={<CheckCircleIcon size={16} />}
                      >
                        {t("technician.actions.arriveOnSite")}
                      </Button>
                    )}

                    {isArrived && (
                      <Button
                        variant="primary"
                        onClick={() => handleVisitAction(visit, "start")}
                        isLoading={transitionMutation.isPending}
                        startIcon={<WrenchIcon size={16} />}
                      >
                        {t("technician.actions.startWork")}
                      </Button>
                    )}

                    {isInProgress && (
                      <Button
                        variant="primary"
                        onClick={() => handleVisitAction(visit, "complete")}
                        isLoading={transitionMutation.isPending}
                        startIcon={<CheckCircleIcon size={16} />}
                      >
                        {t("technician.actions.completeVisit")}
                      </Button>
                    )}

                    {(isArrived || isTraveling) && (
                      <Button
                        variant="secondary"
                        onClick={() => handleVisitAction(visit, "no-access")}
                        isLoading={transitionMutation.isPending}
                      >
                        {t("technician.actions.noAccess", "تعذر الوصول للموقع")}
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
