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
  PlusIcon,
  SearchIcon,
  FilterIcon
} from "../../components/service-icons.tsx";
import { useServiceRequestsQuery } from "../../hooks/use-service-queries.ts";
import { NewRequestModal } from "../modals/new-request-modal.tsx";
import { RequestDetailDrawer } from "./request-detail-drawer.tsx";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";
import type { ServiceContext, ServiceRequestStatus } from "../../types.ts";

export interface ServiceRequestsTabProps {
  readonly legalEntityId: string;
}

export function ServiceRequestsTab({
  legalEntityId
}: ServiceRequestsTabProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const isMobile = useIsMobile(768);

  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [search, setSearch] = useState("");
  const [contextFilter, setContextFilter] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<string>("all");

  const [selectedRequestId, setSelectedRequestId] = useState<string | null>(null);
  const [isNewRequestOpen, setIsNewRequestOpen] = useState(false);

  const requestsQuery = useServiceRequestsQuery(legalEntityId, {
    context: contextFilter !== "all" ? (contextFilter as ServiceContext) : undefined,
    status: statusFilter !== "all" ? (statusFilter as ServiceRequestStatus) : undefined,
    page,
    pageSize
  });

  const items = requestsQuery.data?.items ?? [];
  const totalCount = requestsQuery.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  // Client-side search on subject / request number / customer name
  const filteredItems = items.filter((req) => {
    if (!search.trim()) return true;
    const q = search.toLowerCase();
    return (
      req.requestNumber.toLowerCase().includes(q) ||
      (req.subject || req.title || "").toLowerCase().includes(q) ||
      (req.customerName && req.customerName.toLowerCase().includes(q))
    );
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <Surface
        title={t("requests.title")}
        description={t("requests.subtitle")}
        headerActions={
          <Button
            variant="primary"
            onClick={() => setIsNewRequestOpen(true)}
            startIcon={<PlusIcon size={16} />}
          >
            {t("requests.newRequestTitle")}
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
              placeholder={t("requests.searchPlaceholder")}
            />
          </div>

          <div style={{ width: "160px" }}>
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

          <div style={{ width: "160px" }}>
            <Select
              value={statusFilter}
              onChange={(val) => setStatusFilter(val)}
              options={[
                { value: "all", label: t("common.allStatuses") },
                { value: "new", label: t("requests.statuses.new") },
                { value: "triaged", label: t("requests.statuses.triaged") },
                { value: "in_progress", label: t("requests.statuses.in_progress") },
                { value: "resolved", label: t("requests.statuses.resolved") },
                { value: "closed", label: t("requests.statuses.closed") }
              ]}
            />
          </div>
        </div>

        {/* Requests Table */}
        {requestsQuery.isLoading ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
            {t("common.loading")}
          </div>
        ) : filteredItems.length === 0 ? (
          <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
            <p style={{ margin: "0 0 0.5rem", fontSize: "1rem", fontWeight: 600 }}>
              {t("requests.noRequestsFound")}
            </p>
            <p style={{ margin: 0, fontSize: "0.875rem" }}>
              {t("requests.noRequestsDescription")}
            </p>
          </div>
        ) : (
          <div style={{ overflowX: "auto", marginTop: "1rem" }}>
            <Table isBordered isStriped>
              <thead>
                <tr>
                  <th>{t("requests.requestNumber")}</th>
                  <th>{t("requests.subject")}</th>
                  <th>{t("requests.context")}</th>
                  <th>{t("requests.customerOrAsset")}</th>
                  <th>{t("common.priority")}</th>
                  <th>{t("common.status")}</th>
                  <th>{t("requests.reportedAt")}</th>
                  <th style={{ textAlign: "center" }}>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {filteredItems.map((req) => (
                  <tr
                    key={req.id}
                    onClick={() => setSelectedRequestId(req.id)}
                    style={{ cursor: "pointer" }}
                  >
                    <td>
                      <strong style={{ color: "#0f172a" }}>{req.requestNumber}</strong>
                    </td>
                    <td>{req.subject}</td>
                    <td>
                      {req.context === "customer" ? t("contexts.customer") : t("contexts.internal")}
                    </td>
                    <td>{req.customerName || req.assetReference || "—"}</td>
                    <td>
                      <ServiceLifecycleBadge status={req.priority} />
                    </td>
                    <td>
                      <ServiceLifecycleBadge status={req.status} />
                    </td>
                    <td>{req.createdAtUtc ? new Date(req.createdAtUtc).toLocaleDateString() : "—"}</td>
                    <td style={{ textAlign: "center" }}>
                      <Button
                        variant="ghost"
                        size="compact"
                        onClick={(e) => {
                          e.stopPropagation();
                          setSelectedRequestId(req.id);
                        }}
                      >
                        {t("common.view")}
                      </Button>
                    </td>
                  </tr>
                ))}
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
      {isNewRequestOpen && (
        <NewRequestModal
          isOpen={isNewRequestOpen}
          onClose={() => setIsNewRequestOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}

      {/* 360 Detail Inspector */}
      {selectedRequestId && (
        <RequestDetailDrawer
          isOpen={Boolean(selectedRequestId)}
          onClose={() => setSelectedRequestId(null)}
          legalEntityId={legalEntityId}
          requestId={selectedRequestId}
        />
      )}
    </div>
  );
}
