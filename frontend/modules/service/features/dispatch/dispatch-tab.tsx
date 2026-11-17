import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { ServiceLifecycleBadge } from "../../components/service-lifecycle-badge.tsx";
import {
  CalendarIcon,
  TruckIcon,
  PlusIcon
} from "../../components/service-icons.tsx";
import {
  useWorkOrdersQuery,
  useVisitsQuery,
  useCancelVisitMutation
} from "../../hooks/use-service-queries.ts";
import { NewVisitModal } from "../modals/new-visit-modal.tsx";
import { AssignTechnicianModal } from "../modals/assign-technician-modal.tsx";
import { formatDateTime } from "../../../../components/formatting/date-formatter.ts";
import type { VisitDto, WorkOrderDto } from "../../types.ts";

export interface DispatchTabProps {
  readonly legalEntityId: string;
}

export function DispatchTab({ legalEntityId }: DispatchTabProps): React.JSX.Element {
  const { t } = useTranslation("service");

  const [activeSection, setActiveSection] = useState<"visits" | "unscheduled">("visits");
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState("all");

  // Modals state
  const [selectedWorkOrderForVisit, setSelectedWorkOrderForVisit] = useState<WorkOrderDto | null>(null);
  const [selectedVisitForAssign, setSelectedVisitForAssign] = useState<VisitDto | null>(null);

  // Queries
  const visitsQuery = useVisitsQuery(legalEntityId, {
    ...(statusFilter !== "all" ? { status: statusFilter } : {}),
    page,
    pageSize: 15
  });

  const unscheduledWorkOrdersQuery = useWorkOrdersQuery(legalEntityId, {
    status: "draft",
    pageSize: 50
  });

  const cancelVisitMutation = useCancelVisitMutation(legalEntityId);

  const visits = visitsQuery.data?.items ?? [];
  const totalCount = visitsQuery.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / 15));

  const unscheduledOrders = unscheduledWorkOrdersQuery.data?.items ?? [];

  const handleCancelVisit = async (visitId: string) => {
    try {
      await cancelVisitMutation.mutateAsync({
        legalEntityId,
        visitId,
        reason: "Canceled by dispatcher"
      });
    } catch {}
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Workbench Navigation Ribbon */}
      <div style={{ display: "flex", gap: "0.5rem" }}>
        <Button
          variant={activeSection === "visits" ? "primary" : "secondary"}
          onClick={() => setActiveSection("visits")}
          startIcon={<CalendarIcon size={16} />}
        >
          {t("dispatch.allVisits")} ({totalCount})
        </Button>
        <Button
          variant={activeSection === "unscheduled" ? "primary" : "secondary"}
          onClick={() => setActiveSection("unscheduled")}
          startIcon={<TruckIcon size={16} />}
        >
          {t("dispatch.unscheduledWorkOrders")} ({unscheduledOrders.length})
        </Button>
      </div>

      {activeSection === "visits" && (
        <Surface
          title={t("dispatch.scheduledVisitsTitle")}
          description={t("dispatch.scheduledVisitsDesc")}
          variant="card"
        >
          {/* Filter Bar */}
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: "1rem"
            }}
          >
            <div style={{ width: "200px" }}>
              <Select
                value={statusFilter}
                onChange={(val) => setStatusFilter(val)}
                options={[
                  { value: "all", label: t("common.allStatuses") },
                  { value: "scheduled", label: t("dispatch.statuses.scheduled") },
                  { value: "traveling", label: t("dispatch.statuses.traveling") },
                  { value: "arrived", label: t("dispatch.statuses.arrived") },
                  { value: "in_progress", label: t("dispatch.statuses.in_progress") },
                  { value: "completed", label: t("dispatch.statuses.completed") },
                  { value: "canceled", label: t("dispatch.statuses.canceled") }
                ]}
              />
            </div>
          </div>

          {visitsQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
              {t("common.loading")}
            </div>
          ) : visits.length === 0 ? (
            <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
              {t("dispatch.noVisitsScheduled")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <Table isBordered isStriped>
                <thead>
                  <tr>
                    <th>{t("dispatch.visitNumber")}</th>
                    <th>{t("dispatch.visitType")}</th>
                    <th>{t("dispatch.scheduledStart")}</th>
                    <th>{t("dispatch.scheduledEnd")}</th>
                    <th>{t("common.status")}</th>
                    <th style={{ textAlign: "center" }}>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {visits.map((v) => (
                    <tr key={v.id}>
                      <td><strong>{v.visitNumber || v.id.slice(0, 8)}</strong></td>
                      <td>{(t as any)(`dispatch.types.${(v.visitType || v.locationType || "onsite").toLowerCase()}`)}</td>
                      <td>{formatDateTime(v.scheduledStartUtc || v.plannedStart, { locale: "ar-EG" })}</td>
                      <td>{formatDateTime(v.scheduledEndUtc || v.plannedEnd, { locale: "ar-EG" })}</td>
                      <td><ServiceLifecycleBadge state={v.state || v.status} /></td>
                      <td style={{ textAlign: "center" }}>
                        <div style={{ display: "flex", gap: "0.5rem", justifyContent: "center" }}>
                          {(v.state === "Planned" || v.status === "scheduled") && (
                            <>
                              <Button
                                variant="secondary"
                                size="compact"
                                onClick={() => setSelectedVisitForAssign(v)}
                              >
                                {t("dispatch.assignTechnicianTitle")}
                              </Button>
                              <Button
                                variant="ghost"
                                size="compact"
                                onClick={() => handleCancelVisit(v.id)}
                                isLoading={cancelVisitMutation.isPending}
                              >
                                {t("common.cancel")}
                              </Button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          )}

          {totalPages > 1 && (
            <div style={{ display: "flex", justifyContent: "center", marginTop: "1.5rem" }}>
              <Pagination
                page={page}
                totalPages={totalPages}
                onPageChange={(p) => setPage(p)}
              />
            </div>
          )}
        </Surface>
      )}

      {activeSection === "unscheduled" && (
        <Surface
          title={t("dispatch.unscheduledWorkOrders")}
          description={t("dispatch.unscheduledWorkOrdersDesc")}
          variant="card"
        >
          {unscheduledWorkOrdersQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
              {t("common.loading")}
            </div>
          ) : unscheduledOrders.length === 0 ? (
            <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
              {t("dispatch.noUnscheduledOrders")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <Table isBordered isStriped>
                <thead>
                  <tr>
                    <th>{t("workOrders.workOrderNumber")}</th>
                    <th>{t("requests.subject")}</th>
                    <th>{t("requests.context")}</th>
                    <th>{t("requests.customerOrAsset")}</th>
                    <th>{t("common.priority")}</th>
                    <th style={{ textAlign: "center" }}>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {unscheduledOrders.map((wo) => (
                    <tr key={wo.id}>
                      <td><strong>{wo.workOrderNumber}</strong></td>
                      <td>{wo.subject}</td>
                      <td>{wo.context === "customer" ? t("contexts.customer") : t("contexts.internal")}</td>
                      <td>{wo.customerName || wo.fixedAssetId || "—"}</td>
                      <td><ServiceLifecycleBadge status={wo.priority} /></td>
                      <td style={{ textAlign: "center" }}>
                        <Button
                          variant="primary"
                          size="compact"
                          onClick={() => setSelectedWorkOrderForVisit(wo)}
                          startIcon={<CalendarIcon size={14} />}
                        >
                          {t("dispatch.scheduleVisit")}
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          )}
        </Surface>
      )}

      {/* Modals */}
      {selectedWorkOrderForVisit && (
        <NewVisitModal
          isOpen={Boolean(selectedWorkOrderForVisit)}
          onClose={() => setSelectedWorkOrderForVisit(null)}
          legalEntityId={legalEntityId}
          workOrderId={selectedWorkOrderForVisit.id}
          workOrderNumber={selectedWorkOrderForVisit.workOrderNumber}
        />
      )}

      {selectedVisitForAssign && (
        <AssignTechnicianModal
          isOpen={Boolean(selectedVisitForAssign)}
          onClose={() => setSelectedVisitForAssign(null)}
          legalEntityId={legalEntityId}
          visitId={selectedVisitForAssign.id}
        />
      )}
    </div>
  );
}
