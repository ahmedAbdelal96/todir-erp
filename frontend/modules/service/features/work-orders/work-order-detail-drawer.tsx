import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { QuickPeekDrawer } from "../../../../components/primitives/quick-peek-drawer.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { ServiceLifecycleBadge } from "../../components/service-lifecycle-badge.tsx";
import {
  useWorkOrderQuery,
  useWorkOrderPartsQuery,
  useWorkOrderChecklistsQuery,
  useWorkOrderDiagnosesQuery,
  useWorkOrderLaborQuery,
  useWorkOrderReportsQuery,
  useWorkOrderChargesQuery,
  useWorkOrderSlaClocksQuery,
  useWorkOrderSupplierRecoveriesQuery,
  useVisitsQuery,
  useReadyWorkOrderMutation,
  useScheduleWorkOrderMutation,
  useStartWorkOrderMutation,
  useCompleteWorkOrderMutation,
  useCloseWorkOrderMutation,
  useCancelWorkOrderMutation
} from "../../hooks/use-service-queries.ts";
import { NewVisitModal } from "../modals/new-visit-modal.tsx";
import { AddPartRequirementModal } from "../modals/add-part-requirement-modal.tsx";
import { PartActionModal } from "../modals/part-action-modal.tsx";
import { AnswerChecklistModal } from "../modals/answer-checklist-modal.tsx";
import { RecordDiagnosisModal } from "../modals/record-diagnosis-modal.tsx";
import { RecordLaborModal } from "../modals/record-labor-modal.tsx";
import { CreateReportModal } from "../modals/create-report-modal.tsx";
import { CreateChargeModal } from "../modals/create-charge-modal.tsx";
import {
  WrenchIcon,
  CalendarIcon,
  ClipboardCheckIcon,
  ClockIcon,
  CheckCircleIcon,
  AlertCircleIcon,
  PlusIcon
} from "../../components/service-icons.tsx";
import type { WorkOrderPartDto } from "../../types.ts";

export interface WorkOrderDetailDrawerProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly workOrderId: string | null;
}

type SubTab = "overview" | "visits" | "checklists" | "parts" | "labor" | "reports" | "charges" | "sla";

export function WorkOrderDetailDrawer({
  isOpen,
  onClose,
  legalEntityId,
  workOrderId
}: WorkOrderDetailDrawerProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const [activeSubTab, setActiveSubTab] = useState<SubTab>("overview");

  // Sub-resource modals
  const [isNewVisitOpen, setIsNewVisitOpen] = useState(false);
  const [isAddPartOpen, setIsAddPartOpen] = useState(false);
  const [selectedPartForAction, setSelectedPartForAction] = useState<WorkOrderPartDto | null>(null);
  const [isAnswerChecklistOpen, setIsAnswerChecklistOpen] = useState(false);
  const [isRecordDiagnosisOpen, setIsRecordDiagnosisOpen] = useState(false);
  const [isRecordLaborOpen, setIsRecordLaborOpen] = useState(false);
  const [isCreateReportOpen, setIsCreateReportOpen] = useState(false);
  const [isCreateChargeOpen, setIsCreateChargeOpen] = useState(false);

  // Queries
  const workOrderQuery = useWorkOrderQuery(workOrderId ?? "");
  const partsQuery = useWorkOrderPartsQuery(workOrderId ?? "");
  const checklistsQuery = useWorkOrderChecklistsQuery(workOrderId ?? "");
  const diagnosesQuery = useWorkOrderDiagnosesQuery(workOrderId ?? "");
  const laborQuery = useWorkOrderLaborQuery(workOrderId ?? "");
  const reportsQuery = useWorkOrderReportsQuery(workOrderId ?? "");
  const chargesQuery = useWorkOrderChargesQuery(workOrderId ?? "");
  const slaClocksQuery = useWorkOrderSlaClocksQuery(workOrderId ?? "");
  const recoveriesQuery = useWorkOrderSupplierRecoveriesQuery(workOrderId ?? "");
  const visitsQuery = useVisitsQuery(legalEntityId, { ...(workOrderId ? { workOrderId } : {}), pageSize: 50 });

  // Lifecycle mutations
  const readyMutation = useReadyWorkOrderMutation(legalEntityId);
  const scheduleMutation = useScheduleWorkOrderMutation(legalEntityId);
  const startMutation = useStartWorkOrderMutation(legalEntityId);
  const completeMutation = useCompleteWorkOrderMutation(legalEntityId);
  const closeMutation = useCloseWorkOrderMutation(legalEntityId);
  const cancelMutation = useCancelWorkOrderMutation(legalEntityId);

  const wo = workOrderQuery.data;

  if (!wo) {
    return (
      <QuickPeekDrawer isOpen={isOpen} onClose={onClose} title={t("workOrders.detail")}>
        <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
          {workOrderQuery.isLoading ? t("common.loading") : t("workOrders.noWorkOrderSelected")}
        </div>
      </QuickPeekDrawer>
    );
  }

  const handleReady = async () => {
    try {
      await readyMutation.mutateAsync({ legalEntityId, workOrderId: wo.id, version: wo.version });
    } catch {}
  };

  const handleSchedule = async () => {
    try {
      await scheduleMutation.mutateAsync({ legalEntityId, workOrderId: wo.id, version: wo.version });
    } catch {}
  };

  const handleStart = async () => {
    try {
      await startMutation.mutateAsync({ legalEntityId, workOrderId: wo.id, version: wo.version });
    } catch {}
  };

  const handleComplete = async () => {
    try {
      await completeMutation.mutateAsync({ legalEntityId, workOrderId: wo.id, version: wo.version });
    } catch {}
  };

  const handleClose = async () => {
    try {
      await closeMutation.mutateAsync({ legalEntityId, workOrderId: wo.id, version: wo.version });
    } catch {}
  };

  const handleCancel = async () => {
    try {
      await cancelMutation.mutateAsync({
        legalEntityId,
        workOrderId: wo.id,
        version: wo.version,
        reason: "Canceled by user"
      });
    } catch {}
  };

  const chargesItems = (chargesQuery.data as any)?.items ?? (chargesQuery.data as any) ?? [];

  const tabs: readonly { id: SubTab; label: string; count?: number }[] = [
    { id: "overview", label: t("workOrders.subtabs.overview") },
    { id: "visits", label: t("workOrders.subtabs.visits"), count: visitsQuery.data?.totalCount ?? 0 },
    { id: "checklists", label: t("workOrders.subtabs.checklists"), count: checklistsQuery.data?.length ?? 0 },
    { id: "parts", label: t("workOrders.subtabs.parts"), count: partsQuery.data?.length ?? 0 },
    { id: "labor", label: t("workOrders.subtabs.labor"), count: laborQuery.data?.length ?? 0 },
    { id: "reports", label: t("workOrders.subtabs.reports"), count: reportsQuery.data?.length ?? 0 },
    { id: "charges", label: t("workOrders.subtabs.charges"), count: chargesItems.length ?? 0 },
    { id: "sla", label: t("workOrders.subtabs.sla"), count: slaClocksQuery.data?.length ?? 0 }
  ];

  return (
    <>
      <QuickPeekDrawer
        isOpen={isOpen}
        onClose={onClose}
        title={`${wo.workOrderNumber} — ${wo.subject || wo.title}`}
        subtitle={`${(t as any)("workOrders.types." + (wo.workOrderType || wo.workType))} | ${wo.context === "customer" || wo.context === "CustomerFieldService" ? t("contexts.customer") : t("contexts.internal")}`}
        width="720px"
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "0.5rem 0" }}>
          {/* Status & Action Ribbon */}
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              flexWrap: "wrap",
              gap: "0.75rem",
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              border: "1px solid var(--color-border-subtle, #e2e8f0)"
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
              <div>
                <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("common.status")}:</span>
                <div style={{ marginTop: "0.125rem" }}>
                  <ServiceLifecycleBadge status={wo.state || wo.status} />
                </div>
              </div>
              <div>
                <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("common.priority")}:</span>
                <div style={{ marginTop: "0.125rem" }}>
                  <ServiceLifecycleBadge status={wo.priority} />
                </div>
              </div>
              <div>
                <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("common.version", "النسخة")}:</span>
                <div style={{ marginTop: "0.125rem", fontSize: "0.875rem", fontWeight: 700, color: "#1e293b" }} data-testid="wo-version-badge">
                  {wo.version ?? 1}
                </div>
              </div>
            </div>

            {/* Lifecycle Transition Buttons */}
            {(() => {
              const currentState = (wo.state || wo.status || "").toLowerCase();
              return (
                <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
                  {currentState === "draft" && (
                    <Button
                      variant="primary"
                      size="compact"
                      onClick={handleReady}
                      isLoading={readyMutation.isPending}
                    >
                      {t("states.ready", "جاهز للجدولة")}
                    </Button>
                  )}
                  {currentState === "ready" && (
                    <Button
                      variant="primary"
                      size="compact"
                      onClick={handleSchedule}
                      isLoading={scheduleMutation.isPending}
                    >
                      {t("states.scheduled", "جدولة أمر العمل")}
                    </Button>
                  )}
                  {currentState === "scheduled" && (
                    <Button
                      variant="primary"
                      size="compact"
                      onClick={handleStart}
                      isLoading={startMutation.isPending}
                    >
                      {t("workOrders.actions.start")}
                    </Button>
                  )}
                  {currentState === "inprogress" && (
                    <Button
                      variant="primary"
                      size="compact"
                      onClick={handleComplete}
                      isLoading={completeMutation.isPending}
                    >
                      {t("workOrders.actions.complete")}
                    </Button>
                  )}
                  {currentState === "technicalcompleted" && (
                    <Button
                      variant="primary"
                      size="compact"
                      onClick={handleClose}
                      isLoading={closeMutation.isPending}
                    >
                      {t("workOrders.actions.close")}
                    </Button>
                  )}
                  {currentState !== "closed" && !currentState.startsWith("cancel") && (
                    <Button
                      variant="danger"
                      size="compact"
                      onClick={handleCancel}
                      isLoading={cancelMutation.isPending}
                    >
                      {t("workOrders.actions.cancel")}
                    </Button>
                  )}
                </div>
              );
            })()}
          </div>

          {/* Subtabs Navigation */}
          <div
            style={{
              display: "flex",
              gap: "0.25rem",
              borderBottom: "1px solid var(--color-border-default, #cbd5e1)",
              overflowX: "auto"
            }}
          >
            {tabs.map((tab) => {
              const isActive = activeSubTab === tab.id;
              return (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => setActiveSubTab(tab.id)}
                  style={{
                    padding: "0.5rem 0.75rem",
                    fontSize: "0.8125rem",
                    fontWeight: isActive ? 600 : 500,
                    color: isActive ? "#0284c7" : "#64748b",
                    backgroundColor: "transparent",
                    border: "none",
                    borderBottom: isActive ? "2px solid #0284c7" : "2px solid transparent",
                    cursor: "pointer",
                    whiteSpace: "nowrap"
                  }}
                >
                  {tab.label} {tab.count !== undefined ? `(${tab.count})` : ""}
                </button>
              );
            })}
          </div>

          {/* Subtab Contents */}
          {activeSubTab === "overview" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "1fr 1fr",
                  gap: "1rem",
                  padding: "1rem",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  backgroundColor: "#ffffff",
                  border: "1px solid var(--color-border-default, #cbd5e1)"
                }}
              >
                <div>
                  <span style={{ fontSize: "0.75rem", color: "#64748b" }}>
                    {wo.context === "customer" ? t("requests.customerName") : t("workOrders.fixedAssetId")}:
                  </span>
                  <div style={{ fontWeight: 600, marginTop: "0.25rem", color: "#0f172a" }}>
                    {wo.customerName || wo.fixedAssetId || "—"}
                  </div>
                </div>

                <div>
                  <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("requests.siteLocation")}:</span>
                  <div style={{ fontWeight: 600, marginTop: "0.25rem", color: "#0f172a" }}>
                    {wo.siteLocation || "—"}
                  </div>
                </div>

                <div>
                  <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("workOrders.projectId")}:</span>
                  <div style={{ fontWeight: 600, marginTop: "0.25rem", color: "#0f172a" }}>
                    {wo.projectId || "—"}
                  </div>
                </div>

                <div>
                  <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("requests.reportedAt")}:</span>
                  <div style={{ fontWeight: 600, marginTop: "0.25rem", color: "#0f172a" }}>
                    {wo.createdAtUtc ? new Date(wo.createdAtUtc).toLocaleDateString() : "—"}
                  </div>
                </div>
              </div>

              {wo.description && (
                <div
                  style={{
                    padding: "1rem",
                    borderRadius: "var(--radius-md, 0.5rem)",
                    backgroundColor: "#ffffff",
                    border: "1px solid var(--color-border-default, #cbd5e1)"
                  }}
                >
                  <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.875rem", fontWeight: 700 }}>
                    {t("requests.description")}
                  </h4>
                  <p style={{ margin: 0, fontSize: "0.8125rem", color: "#334155", lineHeight: 1.6 }}>
                    {wo.description}
                  </p>
                </div>
              )}
            </div>
          )}

          {activeSubTab === "visits" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div style={{ display: "flex", justifyContent: "flex-end" }}>
                <Button
                  variant="primary"
                  size="compact"
                  onClick={() => setIsNewVisitOpen(true)}
                  startIcon={<PlusIcon size={16} />}
                >
                  {t("dispatch.scheduleVisitTitle")}
                </Button>
              </div>

              {visitsQuery.data?.items?.length === 0 ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
                  {t("dispatch.noVisitsScheduled")}
                </div>
              ) : (
                <Table isBordered isStriped>
                  <thead>
                    <tr>
                      <th>{t("dispatch.visitNumber")}</th>
                      <th>{t("dispatch.visitType")}</th>
                      <th>{t("dispatch.scheduledStart")}</th>
                      <th>{t("dispatch.scheduledEnd")}</th>
                      <th>{t("common.status")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {visitsQuery.data?.items?.map((v) => (
                      <tr key={v.id}>
                        <td><strong>{v.visitNumber || v.id.slice(0, 8)}</strong></td>
                        <td>{(t as any)(`dispatch.types.${(v.visitType || v.locationType || "onsite").toLowerCase()}`)}</td>
                        <td>{v.scheduledStartUtc || v.plannedStart ? new Date(v.scheduledStartUtc || v.plannedStart).toLocaleString() : "—"}</td>
                        <td>{v.scheduledEndUtc || v.plannedEnd ? new Date(v.scheduledEndUtc || v.plannedEnd).toLocaleString() : "—"}</td>
                        <td><ServiceLifecycleBadge state={v.state || v.status} /></td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              )}
            </div>
          )}

          {activeSubTab === "checklists" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <Button
                  variant="secondary"
                  size="compact"
                  onClick={() => setIsRecordDiagnosisOpen(true)}
                >
                  {t("workOrders.recordDiagnosisTitle")}
                </Button>
                <Button
                  variant="primary"
                  size="compact"
                  onClick={() => setIsAnswerChecklistOpen(true)}
                  startIcon={<PlusIcon size={16} />}
                >
                  {t("workOrders.answerChecklistTitle")}
                </Button>
              </div>

              {/* Diagnoses List */}
              {diagnosesQuery.data && diagnosesQuery.data.length > 0 && (
                <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem" }}>
                  <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.875rem" }}>{t("workOrders.diagnoses")}</h4>
                  <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                    {diagnosesQuery.data.map((d) => (
                      <div key={d.id} style={{ fontSize: "0.8125rem", padding: "0.5rem", backgroundColor: "#fff", border: "1px solid #e2e8f0", borderRadius: "0.25rem" }}>
                        <div><strong>{d.symptom || d.failureCode}</strong>: {d.rootCause}</div>
                        {d.resolutionCode && <div style={{ color: "#059669", marginTop: "0.25rem" }}>✓ {d.resolutionCode}</div>}
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Checklist Answers Table */}
              {checklistsQuery.data?.length === 0 ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
                  {t("workOrders.noChecklistsAnswered")}
                </div>
              ) : (
                <Table isBordered isStriped>
                  <thead>
                    <tr>
                      <th>{t("workOrders.itemCode")}</th>
                      <th>{t("workOrders.checklistPrompt")}</th>
                      <th>{t("common.result")}</th>
                      <th>{t("workOrders.measurementValue")}</th>
                      <th>{t("workOrders.uom")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {checklistsQuery.data?.map((chk) => (
                      <tr key={chk.id}>
                        <td><strong>{chk.itemCode}</strong></td>
                        <td>{chk.prompt || "—"}</td>
                        <td>
                          {chk.passed ? (
                            <span style={{ color: "#059669", fontWeight: 600 }}>✓ {t("workOrders.pass")}</span>
                          ) : (
                            <span style={{ color: "#dc2626", fontWeight: 600 }}>✗ {t("workOrders.fail")}</span>
                          )}
                        </td>
                        <td>{chk.value || "—"}</td>
                        <td>{chk.uom || "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              )}
            </div>
          )}

          {activeSubTab === "parts" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div style={{ display: "flex", justifyContent: "flex-end" }}>
                <Button
                  variant="primary"
                  size="compact"
                  onClick={() => setIsAddPartOpen(true)}
                  startIcon={<PlusIcon size={16} />}
                >
                  {t("parts.addPartRequirementTitle")}
                </Button>
              </div>

              {partsQuery.data?.length === 0 ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
                  {t("parts.noPartsRequested")}
                </div>
              ) : (
                <Table isBordered isStriped>
                  <thead>
                    <tr>
                      <th>{t("parts.itemId")}</th>
                      <th>{t("requests.description")}</th>
                      <th>{t("parts.required")}</th>
                      <th>{t("parts.reserved")}</th>
                      <th>{t("parts.issued")}</th>
                      <th>{t("parts.consumed")}</th>
                      <th style={{ textAlign: "center" }}>{t("common.actions")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {partsQuery.data?.map((p) => (
                      <tr key={p.id}>
                        <td><strong>{p.itemId}</strong></td>
                        <td>{p.description || "—"}</td>
                        <td>{p.requiredQuantity} {p.uom}</td>
                        <td>{p.reservedQuantity}</td>
                        <td>{p.issuedQuantity}</td>
                        <td>{p.consumedQuantity}</td>
                        <td style={{ textAlign: "center" }}>
                          <Button
                            variant="secondary"
                            size="compact"
                            onClick={() => setSelectedPartForAction(p)}
                          >
                            {t("parts.partActionTitle")}
                          </Button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              )}
            </div>
          )}

          {activeSubTab === "labor" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div style={{ display: "flex", justifyContent: "flex-end" }}>
                <Button
                  variant="primary"
                  size="compact"
                  onClick={() => setIsRecordLaborOpen(true)}
                  startIcon={<PlusIcon size={16} />}
                >
                  {t("labor.recordLaborTitle")}
                </Button>
              </div>

              {laborQuery.data?.length === 0 ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
                  {t("labor.noLaborRecorded")}
                </div>
              ) : (
                <Table isBordered isStriped>
                  <thead>
                    <tr>
                      <th>{t("dispatch.technicianId")}</th>
                      <th>{t("labor.activityType")}</th>
                      <th>{t("labor.hours")}</th>
                      <th>{t("labor.hourlyRate")}</th>
                      <th>{t("billing.customerLiability")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {laborQuery.data?.map((l) => (
                      <tr key={l.id}>
                        <td><strong>{l.technicianId}</strong></td>
                        <td>{(t as any)(`labor.activities.${l.activityType}`)}</td>
                        <td>{l.hours} {t("contracts.hours", "ساعة")}</td>
                        <td>{l.hourlyRate} {l.currency}</td>
                        <td>{l.isBillable ? t("common.yes", "نعم") : "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              )}
            </div>
          )}

          {activeSubTab === "reports" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div style={{ display: "flex", justifyContent: "flex-end" }}>
                <Button
                  variant="primary"
                  size="compact"
                  onClick={() => setIsCreateReportOpen(true)}
                  startIcon={<PlusIcon size={16} />}
                >
                  {t("reports.createReportTitle")}
                </Button>
              </div>

              {reportsQuery.data?.length === 0 ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
                  {t("reports.noReportsCreated")}
                </div>
              ) : (
                <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                  {reportsQuery.data?.map((rep) => (
                    <div
                      key={rep.id}
                      style={{
                        padding: "1rem",
                        borderRadius: "var(--radius-md, 0.5rem)",
                        backgroundColor: "#ffffff",
                        border: "1px solid var(--color-border-default, #cbd5e1)"
                      }}
                    >
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                        <strong style={{ fontSize: "0.875rem" }}>{t("reports.title")}</strong>
                        <span style={{ fontSize: "0.75rem", color: "#64748b" }}>
                          {rep.createdAtUtc || rep.createdAt ? new Date(rep.createdAtUtc || rep.createdAt).toLocaleString() : "—"}
                        </span>
                      </div>
                      <p style={{ margin: "0.5rem 0", fontSize: "0.8125rem", color: "#334155" }}>
                        {rep.summary}
                      </p>
                      <div style={{ display: "flex", gap: "1rem", fontSize: "0.75rem", color: "#64748b" }}>
                        <span>{t("reports.customerRepresentative")}: {rep.customerRepresentative || "—"}</span>
                        <span>{t("reports.signatureCaptured")}: {rep.signatureCaptured ? t("reports.signed", "تم التوقيع") : "—"}</span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {activeSubTab === "charges" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div style={{ display: "flex", justifyContent: "flex-end" }}>
                <Button
                  variant="primary"
                  size="compact"
                  onClick={() => setIsCreateChargeOpen(true)}
                  startIcon={<PlusIcon size={16} />}
                >
                  {t("billing.newChargeTitle")}
                </Button>
              </div>

              {chargesItems.length === 0 ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
                  {t("billing.noChargesRecorded")}
                </div>
              ) : (
                <Table isBordered isStriped>
                  <thead>
                    <tr>
                      <th>{t("billing.chargeType")}</th>
                      <th>{t("requests.description")}</th>
                      <th>{(t as any)("billing.costAmount", "التكلفة")}</th>
                      <th>{(t as any)("billing.coveredAmount", "المغطى")}</th>
                      <th>{t("billing.customerLiability")}</th>
                      <th>{t("common.status")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {chargesItems.map((c: any) => (
                      <tr key={c.id}>
                        <td>{(t as any)(`billing.types.${(c.category || c.chargeType || "fee").toLowerCase()}`)}</td>
                        <td>{c.description}</td>
                        <td>{c.costAmount ?? 0} {c.currencyCode || "SAR"}</td>
                        <td>{c.coveredAmount ?? 0} {c.currencyCode || "SAR"}</td>
                        <td><strong>{c.customerAmount ?? 0} {c.currencyCode || "SAR"}</strong></td>
                        <td><ServiceLifecycleBadge state={c.state || c.status} /></td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              )}
            </div>
          )}

          {activeSubTab === "sla" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <h4 style={{ margin: 0, fontSize: "0.875rem" }}>{t("help.slaTitle")}</h4>
              {slaClocksQuery.data?.length === 0 ? (
                <div style={{ padding: "1.5rem", textAlign: "center", color: "#64748b" }}>
                  {t("contracts.noSlaClocks")}
                </div>
              ) : (
                <Table isBordered isStriped>
                  <thead>
                    <tr>
                      <th>{t("contracts.clockType")}</th>
                      <th>{t("contracts.slaTargetHours")}</th>
                      <th>{t("contracts.slaDeadline")}</th>
                      <th>{t("common.status")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {slaClocksQuery.data?.map((clock: any) => (
                      <tr key={clock.id}>
                        <td>{clock.clockType}</td>
                        <td>{clock.targetHours ?? 4} {t("contracts.hours", "ساعة")}</td>
                        <td>{new Date(clock.deadlineUtc || clock.dueAt || Date.now()).toLocaleString()}</td>
                        <td>
                          {clock.isBreached ? (
                            <span style={{ color: "#dc2626", fontWeight: 600 }}>{t("sla.breached", "تم التجاوز")}</span>
                          ) : clock.isStopped ? (
                            <span style={{ color: "#059669", fontWeight: 600 }}>{t("sla.completed", "مكتمل")}</span>
                          ) : (
                            <span style={{ color: "#2563eb", fontWeight: 600 }}>{t("sla.running", "قيد الحساب")}</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              )}
            </div>
          )}
        </div>
      </QuickPeekDrawer>

      {/* Sub-resource Modals */}
      {isNewVisitOpen && (
        <NewVisitModal
          isOpen={isNewVisitOpen}
          onClose={() => setIsNewVisitOpen(false)}
          legalEntityId={legalEntityId}
          workOrderId={wo.id}
          workOrderNumber={wo.workOrderNumber}
        />
      )}

      {isAddPartOpen && (
        <AddPartRequirementModal
          isOpen={isAddPartOpen}
          onClose={() => setIsAddPartOpen(false)}
          legalEntityId={legalEntityId}
          workOrderId={wo.id}
        />
      )}

      {selectedPartForAction && (
        <PartActionModal
          isOpen={Boolean(selectedPartForAction)}
          onClose={() => setSelectedPartForAction(null)}
          legalEntityId={legalEntityId}
          part={selectedPartForAction}
        />
      )}

      {isAnswerChecklistOpen && (
        <AnswerChecklistModal
          isOpen={isAnswerChecklistOpen}
          onClose={() => setIsAnswerChecklistOpen(false)}
          legalEntityId={legalEntityId}
          workOrderId={wo.id}
        />
      )}

      {isRecordDiagnosisOpen && (
        <RecordDiagnosisModal
          isOpen={isRecordDiagnosisOpen}
          onClose={() => setIsRecordDiagnosisOpen(false)}
          legalEntityId={legalEntityId}
          workOrderId={wo.id}
        />
      )}

      {isRecordLaborOpen && (
        <RecordLaborModal
          isOpen={isRecordLaborOpen}
          onClose={() => setIsRecordLaborOpen(false)}
          legalEntityId={legalEntityId}
          workOrderId={wo.id}
        />
      )}

      {isCreateReportOpen && (
        <CreateReportModal
          isOpen={isCreateReportOpen}
          onClose={() => setIsCreateReportOpen(false)}
          legalEntityId={legalEntityId}
          workOrderId={wo.id}
        />
      )}

      {isCreateChargeOpen && (
        <CreateChargeModal
          isOpen={isCreateChargeOpen}
          onClose={() => setIsCreateChargeOpen(false)}
          legalEntityId={legalEntityId}
          workOrderId={wo.id}
        />
      )}
    </>
  );
}
