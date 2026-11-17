import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { ServiceKpiCard } from "../../components/service-kpi-card.tsx";
import { ServiceLifecycleBadge } from "../../components/service-lifecycle-badge.tsx";
import {
  FileSpreadsheetIcon,
  ShieldCheckIcon,
  CheckCircleIcon,
  ClockIcon
} from "../../components/service-icons.tsx";
import {
  useAllChargesQuery,
  useServiceKpiQuery,
  useWorkOrdersQuery,
  useRequestChargeInvoiceMutation
} from "../../hooks/use-service-queries.ts";

export interface ServiceBillingTabProps {
  readonly legalEntityId: string;
}

export function ServiceBillingTab({ legalEntityId }: ServiceBillingTabProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const [filterState, setFilterState] = useState<string>("all");

  const kpiQuery = useServiceKpiQuery();
  const chargesQuery = useAllChargesQuery({
    legalEntityId,
    pageSize: 100,
    ...(filterState !== "all" ? { state: filterState } : {})
  });
  const workOrdersQuery = useWorkOrdersQuery(legalEntityId, { pageSize: 100 });
  const requestInvoiceMutation = useRequestChargeInvoiceMutation();

  const charges = chargesQuery.data?.items ?? [];
  const workOrders = workOrdersQuery.data?.items ?? [];

  // Map workOrderId to workOrder details for display
  const workOrderMap = new Map(workOrders.map((wo) => [wo.id, wo]));

  // Authoritative financial calculations from real data
  const totalCustomerBillable = charges.reduce((acc, c) => acc + (Number(c.customerAmount) || 0), 0);
  const totalCovered = charges.reduce((acc, c) => acc + (Number(c.coveredAmount) || 0), 0);
  const projectWorkOrdersCount = workOrders.filter((wo) => wo.billingOwner === "Projects").length;

  const defaultCurrency = charges[0]?.currencyCode || "EGP";

  const handleRequestInvoice = async (chargeId: string) => {
    try {
      await requestInvoiceMutation.mutateAsync(chargeId);
    } catch {
      // Error handled by mutation/toast
    }
  };

  const handleExportCsv = () => {
    if (charges.length === 0) return;
    const header = "ChargeId,WorkOrderNumber,Category,Description,CustomerAmount,CoveredAmount,Currency,State\n";
    const rows = charges
      .map((c) => {
        const wo = workOrderMap.get(c.workOrderId);
        const woNum = wo?.workOrderNumber || c.workOrderId;
        const desc = `"${(c.description || "").replace(/"/g, '""')}"`;
        return `${c.id},${woNum},${c.category},${desc},${c.customerAmount},${c.coveredAmount},${c.currencyCode},${c.state}`;
      })
      .join("\n");
    const blob = new Blob([header + rows], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.setAttribute("download", `service-charges-${legalEntityId}-${Date.now()}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Financial Metrics Cards */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: "1rem" }}>
        <ServiceKpiCard
          label={t("billing.customerInvoicingReady")}
          value={`${totalCustomerBillable.toLocaleString()} ${defaultCurrency}`}
          icon={<CheckCircleIcon size={20} color="#059669" />}
          badgeText={`${charges.filter((c) => c.state === "Reviewed" || c.state === "Requested").length} ${t("billing.chargesReady")}`}
        />
        <ServiceKpiCard
          label={t("billing.warrantyAbsorbedCost")}
          value={`${totalCovered.toLocaleString()} ${defaultCurrency}`}
          icon={<ShieldCheckIcon size={20} color="#d97706" />}
          badgeText={`${charges.filter((c) => Number(c.coveredAmount) > 0).length} ${t("billing.coveredItems")}`}
        />
        <ServiceKpiCard
          label={t("billing.projectAllocatedWork")}
          value={`${projectWorkOrdersCount}`}
          icon={<FileSpreadsheetIcon size={20} color="#2563eb" />}
          badgeText={t("billing.projectOwnedOrders")}
        />
      </div>

      <Surface
        title={t("billing.serviceChargesTitle")}
        description={t("billing.serviceChargesDesc")}
        headerActions={
          <Button
            variant="secondary"
            onClick={handleExportCsv}
            isDisabled={charges.length === 0}
            startIcon={<FileSpreadsheetIcon size={16} />}
          >
            {t("billing.exportReport")}
          </Button>
        }
        variant="card"
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
          <div style={{ width: "220px" }}>
            <Select
              value={filterState}
              onChange={(val) => setFilterState(val)}
              options={[
                { value: "all", label: t("common.allStatuses") },
                { value: "Draft", label: t("billing.states.draft") },
                { value: "Reviewed", label: t("billing.states.reviewed") },
                { value: "Requested", label: t("billing.states.requested") },
                { value: "Invoiced", label: t("billing.states.invoiced") }
              ]}
            />
          </div>
        </div>

        {chargesQuery.isLoading ? (
          <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
            {t("common.loading")}
          </div>
        ) : charges.length === 0 ? (
          <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
            {t("billing.noChargesRecorded")}
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <Table isBordered isStriped>
              <thead>
                <tr>
                  <th>{t("workOrders.workOrderNumber")}</th>
                  <th>{t("billing.chargeCategory")}</th>
                  <th>{t("requests.description")}</th>
                  <th>{t("billing.customerAmount")}</th>
                  <th>{t("billing.coveredAmount")}</th>
                  <th>{t("common.status")}</th>
                  <th style={{ textAlign: "center" }}>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {charges.map((charge) => {
                  const wo = workOrderMap.get(charge.workOrderId);
                  const isProjectOwned = wo?.billingOwner === "Projects";
                  const canRequestInvoice = (charge.state === "Reviewed" || charge.state === "Draft") && !isProjectOwned && Number(charge.customerAmount) > 0;

                  return (
                    <tr key={charge.id}>
                      <td><strong>{wo?.workOrderNumber || charge.workOrderId.slice(0, 8)}</strong></td>
                      <td>{charge.category}</td>
                      <td>{charge.description || "—"}</td>
                      <td>
                        <strong>{Number(charge.customerAmount).toLocaleString()} {charge.currencyCode}</strong>
                      </td>
                      <td>
                        {Number(charge.coveredAmount) > 0 ? (
                          <span style={{ color: "#059669" }}>
                            {Number(charge.coveredAmount).toLocaleString()} {charge.currencyCode}
                          </span>
                        ) : "0.00"}
                      </td>
                      <td>
                        <ServiceLifecycleBadge state={charge.state} />
                        {isProjectOwned && (
                          <span style={{ fontSize: "0.75rem", color: "#2563eb", display: "block", marginTop: "2px" }}>
                            {t("billing.projectOwnedNotice")}
                          </span>
                        )}
                      </td>
                      <td style={{ textAlign: "center" }}>
                        {canRequestInvoice && (
                          <Button
                            variant="primary"
                            size="compact"
                            onClick={() => handleRequestInvoice(charge.id)}
                            isLoading={requestInvoiceMutation.isPending}
                          >
                            {t("billing.requestInvoiceAction")}
                          </Button>
                        )}
                        {charge.state === "Invoiced" && charge.invoiceId && (
                          <span style={{ fontSize: "0.75rem", color: "#059669", fontWeight: 600 }}>
                            {t("billing.invoicedBadge")}
                          </span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </Table>
          </div>
        )}
      </Surface>
    </div>
  );
}
