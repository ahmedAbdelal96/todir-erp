import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { ServiceKpiCard } from "../../components/service-kpi-card.tsx";
import { ServiceLifecycleBadge } from "../../components/service-lifecycle-badge.tsx";
import {
  WrenchIcon,
  CalendarIcon,
  TruckIcon,
  ShieldCheckIcon,
  ClipboardCheckIcon,
  ClockIcon,
  AlertCircleIcon,
  PlusIcon
} from "../../components/service-icons.tsx";
import {
  useServiceKpiQuery,
  useServiceRequestsQuery,
  useWorkOrdersQuery,
  useVisitsQuery,
  useInstalledBaseQuery
} from "../../hooks/use-service-queries.ts";
import { NewRequestModal } from "../modals/new-request-modal.tsx";
import { NewWorkOrderModal } from "../modals/new-work-order-modal.tsx";
import { NewInstalledBaseModal } from "../modals/new-installed-base-modal.tsx";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";

export interface ServiceOverviewTabProps {
  readonly legalEntityId: string;
  readonly onNavigateTab?: (tabId: string) => void;
}

export function ServiceOverviewTab({
  legalEntityId,
  onNavigateTab
}: ServiceOverviewTabProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const isMobile = useIsMobile(768);

  const [isNewRequestOpen, setIsNewRequestOpen] = useState(false);
  const [isNewWorkOrderOpen, setIsNewWorkOrderOpen] = useState(false);
  const [isNewInstalledBaseOpen, setIsNewInstalledBaseOpen] = useState(false);

  const kpiQuery = useServiceKpiQuery(legalEntityId);
  const requestsQuery = useServiceRequestsQuery(legalEntityId, { page: 1, pageSize: 50 });
  const workOrdersQuery = useWorkOrdersQuery(legalEntityId, { page: 1, pageSize: 50 });
  const visitsQuery = useVisitsQuery(legalEntityId, { page: 1, pageSize: 50 });
  const installedBaseQuery = useInstalledBaseQuery(legalEntityId, { page: 1, pageSize: 50 });

  const kpi = kpiQuery.data;
  const totalRequests = kpi ? kpi.openRequests : (requestsQuery.data?.totalCount ?? 0);
  const openWorkOrdersCount = kpi ? kpi.openWorkOrders : (workOrdersQuery.data?.totalCount ?? 0);
  const visitsCount = kpi ? kpi.visitsToday : (visitsQuery.data?.totalCount ?? 0);
  const totalEquipment = installedBaseQuery.data?.totalCount ?? 0;
  const availabilityDisplay = kpi ? `${kpi.availabilityPercent.toFixed(1)}%` : undefined;

  const workOrders = workOrdersQuery.data?.items ?? [];
  const urgentOrders = workOrders.filter(
    (wo) => wo.priority === "urgent" || wo.priority === "high" || wo.priority === "Critical" || wo.priority === "High"
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* KPI Cards Row */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: isMobile ? "1fr 1fr" : "repeat(4, 1fr)",
          gap: "1rem"
        }}
      >
        <ServiceKpiCard
          label={t("overview.kpi.activeWorkOrders")}
          value={openWorkOrdersCount}
          icon={<WrenchIcon size={20} color="#2563eb" />}
          badgeText={`${workOrdersQuery.data?.totalCount ?? openWorkOrdersCount} ${t("overview.kpi.total")}`}
        />
        <ServiceKpiCard
          label={t("overview.kpi.openRequests")}
          value={totalRequests}
          icon={<AlertCircleIcon size={20} color="#d97706" />}
        />
        <ServiceKpiCard
          label={t("overview.kpi.scheduledVisits")}
          value={visitsCount}
          icon={<CalendarIcon size={20} color="#059669" />}
        />
        <ServiceKpiCard
          label={t("overview.kpi.registeredAssets")}
          value={totalEquipment}
          trend={availabilityDisplay}
          trendDirection="positive"
          icon={<ShieldCheckIcon size={20} color="#7c3aed" />}
          badgeText={t("overview.kpi.operationalAvailability")}
        />
      </div>

      {/* Quick Launchpad & Urgent Attention Surface */}
      <Surface
        title={t("overview.quickActionsTitle")}
        description={t("overview.quickActionsDesc")}
        variant="card"
      >
        <div
          style={{
            display: "flex",
            flexWrap: "wrap",
            gap: "0.75rem",
            padding: "0.5rem 0"
          }}
        >
          <Button
            variant="primary"
            onClick={() => setIsNewWorkOrderOpen(true)}
            startIcon={<PlusIcon size={16} />}
          >
            {t("workOrders.createWorkOrder")}
          </Button>
          <Button
            variant="secondary"
            onClick={() => setIsNewRequestOpen(true)}
            startIcon={<AlertCircleIcon size={16} />}
          >
            {t("requests.newRequestTitle")}
          </Button>
          <Button
            variant="secondary"
            onClick={() => setIsNewInstalledBaseOpen(true)}
            startIcon={<WrenchIcon size={16} />}
          >
            {t("installedBase.newEquipmentTitle")}
          </Button>
          {onNavigateTab && (
            <Button
              variant="tertiary"
              onClick={() => onNavigateTab("dispatch")}
              startIcon={<CalendarIcon size={16} />}
            >
              {t("dispatch.openWorkbench")}
            </Button>
          )}
        </div>
      </Surface>

      {/* Critical & High Priority Work Orders */}
      <Surface
        title={t("overview.urgentOrdersTitle")}
        description={t("overview.urgentOrdersDesc")}
        variant="card"
      >
        {urgentOrders.length === 0 ? (
          <div
            style={{
              padding: "2rem",
              textAlign: "center",
              color: "var(--color-content-muted, #64748b)",
              fontSize: "0.875rem"
            }}
          >
            {t("overview.noUrgentOrders")}
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <Table isBordered isStriped>
              <thead>
                <tr>
                  <th>{t("workOrders.workOrderNumber")}</th>
                  <th>{t("requests.subject")}</th>
                  <th>{t("requests.context")}</th>
                  <th>{t("workOrders.workOrderType")}</th>
                  <th>{t("common.priority")}</th>
                  <th>{t("common.status")}</th>
                </tr>
              </thead>
              <tbody>
                {urgentOrders.slice(0, 5).map((wo) => (
                  <tr key={wo.id}>
                    <td>
                      <strong style={{ color: "#0f172a" }}>{wo.workOrderNumber}</strong>
                    </td>
                    <td>{wo.subject}</td>
                    <td>
                      {wo.context === "customer" ? t("contexts.customer") : t("contexts.internal")}
                      {wo.customerName ? ` (${wo.customerName})` : ""}
                    </td>
                    <td>{(t as any)(`workOrders.types.${(wo.workOrderType || wo.workType || "corrective").toLowerCase()}`)}</td>
                    <td>
                      <ServiceLifecycleBadge status={wo.priority} />
                    </td>
                    <td>
                      <ServiceLifecycleBadge status={wo.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        )}
      </Surface>

      {/* Dialogs */}
      {isNewRequestOpen && (
        <NewRequestModal
          isOpen={isNewRequestOpen}
          onClose={() => setIsNewRequestOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}

      {isNewWorkOrderOpen && (
        <NewWorkOrderModal
          isOpen={isNewWorkOrderOpen}
          onClose={() => setIsNewWorkOrderOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}

      {isNewInstalledBaseOpen && (
        <NewInstalledBaseModal
          isOpen={isNewInstalledBaseOpen}
          onClose={() => setIsNewInstalledBaseOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}
    </div>
  );
}
