import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { ServiceLifecycleBadge } from "../../components/service-lifecycle-badge.tsx";
import { PlusIcon } from "../../components/service-icons.tsx";
import { useWorkOrdersQuery } from "../../hooks/use-service-queries.ts";
import { NewWorkOrderModal } from "../modals/new-work-order-modal.tsx";
import { WorkOrderDetailDrawer } from "./work-order-detail-drawer.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";
import type { ServiceContext, WorkOrderStatus, WorkOrderType } from "../../types.ts";

export interface WorkOrdersTabProps {
  readonly legalEntityId: string;
}

export function WorkOrdersTab({ legalEntityId }: WorkOrdersTabProps): React.JSX.Element {
  const { t } = useTranslation("service");

  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [search, setSearch] = useState("");
  const [contextFilter, setContextFilter] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [typeFilter, setTypeFilter] = useState<string>("all");

  const [selectedWorkOrderId, setSelectedWorkOrderId] = useState<string | null>(null);
  const [isNewWorkOrderOpen, setIsNewWorkOrderOpen] = useState(false);

  const workOrdersQuery = useWorkOrdersQuery(legalEntityId, {
    context: contextFilter !== "all" ? (contextFilter as ServiceContext) : undefined,
    status: statusFilter !== "all" ? (statusFilter as WorkOrderStatus) : undefined,
    page,
    pageSize
  });

  const items = workOrdersQuery.data?.items ?? [];
  const totalCount = workOrdersQuery.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  // Client filtering on search & type
  const filteredItems = items.filter((wo) => {
    const woType = (wo.workOrderType || wo.workType || "").toString().toLowerCase();
    if (typeFilter !== "all" && woType !== typeFilter.toLowerCase()) return false;
    if (!search.trim()) return true;
    const q = search.toLowerCase();
    return (
      wo.workOrderNumber.toLowerCase().includes(q) ||
      (wo.subject || wo.title || "").toLowerCase().includes(q) ||
      (wo.customerName && wo.customerName.toLowerCase().includes(q)) ||
      (wo.fixedAssetId && wo.fixedAssetId.toLowerCase().includes(q))
    );
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <Surface
        title={t("workOrders.title")}
        description={t("workOrders.subtitle")}
        headerActions={
          <Button
            variant="primary"
            onClick={() => setIsNewWorkOrderOpen(true)}
            startIcon={<PlusIcon size={16} />}
          >
            {t("workOrders.createWorkOrder")}
          </Button>
        }
        variant="card"
      >
        {/* Filters and Search Bar */}
        <div
          style={{
            display: "flex",
            flexWrap: "wrap",
            gap: "1rem",
            alignItems: "center",
            padding: "0.5rem 0 1.25rem",
            borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)"
          }}
        >
          <div style={{ flex: 1, minWidth: "220px" }}>
            <TextField
              value={search}
              onChange={(val) => setSearch(val)}
              placeholder={t("workOrders.searchPlaceholder")}
            />
          </div>

          <div style={{ width: "150px" }}>
            <Select
              value={contextFilter}
              onChange={(val) => setContextFilter(val)}
              options={[
                { value: "all", label: t("common.allContexts") },
                { value: "customer", label: t("contexts.customer") },
                { value: "internal", label: t("contexts.internal") }
              ]}
            />
          </div>

          <div style={{ width: "150px" }}>
            <Select
              value={typeFilter}
              onChange={(val) => setTypeFilter(val)}
              options={[
                { value: "all", label: t("common.allTypes") },
                { value: "corrective", label: t("workOrders.types.corrective") },
                { value: "preventive", label: t("workOrders.types.preventive") },
                { value: "installation", label: t("workOrders.types.installation") },
                { value: "inspection", label: t("workOrders.types.inspection") }
              ]}
            />
          </div>

          <div style={{ width: "150px" }}>
            <Select
              value={statusFilter}
              onChange={(val) => setStatusFilter(val)}
              options={[
                { value: "all", label: t("common.allStatuses") },
                { value: "draft", label: t("workOrders.statuses.draft") },
                { value: "scheduled", label: t("workOrders.statuses.scheduled") },
                { value: "in_progress", label: t("workOrders.statuses.in_progress") },
                { value: "completed", label: t("workOrders.statuses.completed") },
                { value: "closed", label: t("workOrders.statuses.closed") },
                { value: "canceled", label: t("workOrders.statuses.canceled") }
              ]}
            />
          </div>
        </div>

        {/* Work Orders Table */}
        {workOrdersQuery.isLoading ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
            {t("common.loading")}
          </div>
        ) : filteredItems.length === 0 ? (
          <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
            <p style={{ margin: "0 0 0.5rem", fontSize: "1rem", fontWeight: 600 }}>
              {t("workOrders.noWorkOrdersFound")}
            </p>
            <p style={{ margin: 0, fontSize: "0.875rem" }}>
              {t("workOrders.noWorkOrdersDescription")}
            </p>
          </div>
        ) : (
          <div style={{ overflowX: "auto", marginTop: "1rem" }}>
            <Table isBordered isStriped>
              <thead>
                <tr>
                  <th>{t("workOrders.workOrderNumber")}</th>
                  <th>{t("requests.subject")}</th>
                  <th>{t("requests.context")}</th>
                  <th>{t("workOrders.workOrderType")}</th>
                  <th>{t("billing.billingOwner", "جهة الفوترة")}</th>
                  <th>{t("requests.customerOrAsset")}</th>
                  <th>{t("common.priority")}</th>
                  <th>{t("common.status")}</th>
                  <th>{t("requests.reportedAt")}</th>
                  <th style={{ textAlign: "center" }}>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {filteredItems.map((wo) => {
                  const typeKey = String(wo.workOrderType || wo.workType || "").toLowerCase();
                  return (
                    <tr
                      key={wo.id}
                      onClick={() => setSelectedWorkOrderId(wo.id)}
                      style={{ cursor: "pointer" }}
                    >
                      <td>
                        <strong style={{ color: "#0f172a" }}>{wo.workOrderNumber}</strong>
                      </td>
                      <td>{wo.subject || wo.title}</td>
                      <td>
                        {wo.context === "customer" || wo.context === "CustomerFieldService" ? t("contexts.customer") : t("contexts.internal")}
                      </td>
                      <td>{typeKey ? t(`workOrders.types.${typeKey}` as any, wo.workOrderType || wo.workType) : "—"}</td>
                      <td>
                        <span
                          style={{
                            padding: "0.2rem 0.5rem",
                            borderRadius: "0.25rem",
                            fontSize: "0.75rem",
                            fontWeight: 600,
                            backgroundColor: wo.billingOwner === "Projects" ? "#e0f2fe" : wo.billingOwner === "Service" ? "#dcfce7" : "#f1f5f9",
                            color: wo.billingOwner === "Projects" ? "#0369a1" : wo.billingOwner === "Service" ? "#15803d" : "#475569"
                          }}
                        >
                          {wo.billingOwner === "Projects"
                            ? t("billing.owners.projects")
                            : wo.billingOwner === "Service"
                            ? t("billing.owners.service")
                            : t("billing.owners.none")}
                        </span>
                      </td>
                      <td>{wo.customerName || wo.fixedAssetId || "—"}</td>
                      <td>
                        <ServiceLifecycleBadge state={wo.priority} />
                      </td>
                      <td>
                        <ServiceLifecycleBadge state={wo.status || wo.state} />
                      </td>
                      <td>{formatDate(wo.createdAtUtc || wo.plannedStart, { locale: "ar-EG" })}</td>
                    <td style={{ textAlign: "center" }}>
                      <Button
                        variant="ghost"
                        size="compact"
                        onClick={(e) => {
                          e.stopPropagation();
                          setSelectedWorkOrderId(wo.id);
                        }}
                      >
                        {t("common.view")}
                      </Button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
            </Table>
          </div>
        )}

        {/* Pagination */}
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

      {/* Creation Modal */}
      {isNewWorkOrderOpen && (
        <NewWorkOrderModal
          isOpen={isNewWorkOrderOpen}
          onClose={() => setIsNewWorkOrderOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}

      {/* 360 Detail Inspector */}
      {selectedWorkOrderId && (
        <WorkOrderDetailDrawer
          isOpen={Boolean(selectedWorkOrderId)}
          onClose={() => setSelectedWorkOrderId(null)}
          legalEntityId={legalEntityId}
          workOrderId={selectedWorkOrderId}
        />
      )}
    </div>
  );
}
