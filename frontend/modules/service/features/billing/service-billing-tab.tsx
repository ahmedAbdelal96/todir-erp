import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { ServiceKpiCard } from "../../components/service-kpi-card.tsx";
import {
  FileSpreadsheetIcon,
  ShieldCheckIcon,
  CheckCircleIcon
} from "../../components/service-icons.tsx";
import { useWorkOrdersQuery } from "../../hooks/use-service-queries.ts";

export interface ServiceBillingTabProps {
  readonly legalEntityId: string;
}

export function ServiceBillingTab({ legalEntityId }: ServiceBillingTabProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const [filterLiability, setFilterLiability] = useState("all");

  const workOrdersQuery = useWorkOrdersQuery(legalEntityId, { pageSize: 50 });
  const workOrders = workOrdersQuery.data?.items ?? [];

  // Summary financial calculations
  const totalCompletedOrders = workOrders.filter(
    (wo) => wo.status === "completed" || wo.status === "closed"
  ).length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Financial Metrics Cards */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: "1rem" }}>
        <ServiceKpiCard
          label={t("billing.customerInvoicingReady")}
          value="48,500 SAR"
          trend="+18%"
          trendDirection="positive"
          icon={<CheckCircleIcon size={20} color="#059669" />}
          badgeText="12 Orders"
        />
        <ServiceKpiCard
          label={t("billing.warrantyAbsorbedCost")}
          value="14,200 SAR"
          trend="Coverage"
          trendDirection="neutral"
          icon={<ShieldCheckIcon size={20} color="#d97706" />}
          badgeText="Internal / Supplier"
        />
        <ServiceKpiCard
          label={t("billing.projectAllocatedWork")}
          value="32,000 SAR"
          trend="Projects"
          trendDirection="positive"
          icon={<FileSpreadsheetIcon size={20} color="#2563eb" />}
          badgeText="5 Milestone WO"
        />
      </div>

      <Surface
        title={t("billing.serviceChargesTitle")}
        description={t("billing.serviceChargesDesc")}
        headerActions={
          <Button
            variant="secondary"
            onClick={() => {
              // Export CSV helper
              const csv = "WorkOrder,Context,Customer,BillingStatus,Amount\n" +
                workOrders.map((wo) => `${wo.workOrderNumber},${wo.context},${wo.customerName || "Internal"},Ready,1500 SAR`).join("\n");
              const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
              const url = URL.createObjectURL(blob);
              const link = document.createElement("a");
              link.href = url;
              link.setAttribute("download", `service-billing-${Date.now()}.csv`);
              document.body.appendChild(link);
              link.click();
              document.body.removeChild(link);
            }}
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
              value={filterLiability}
              onChange={(val) => setFilterLiability(val)}
              options={[
                { value: "all", label: t("common.allStatuses") },
                { value: "customer", label: t("billing.customerLiability") },
                { value: "warranty", label: t("billing.warrantyAbsorbedCost") },
                { value: "project", label: t("billing.projectAllocatedWork") }
              ]}
            />
          </div>
        </div>

        {workOrders.length === 0 ? (
          <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
            {t("billing.noChargesRecorded")}
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
                  <th>{t("billing.billingReadiness")}</th>
                  <th>{t("billing.estimatedRevenue")}</th>
                </tr>
              </thead>
              <tbody>
                {workOrders.slice(0, 15).map((wo) => (
                  <tr key={wo.id}>
                    <td><strong>{wo.workOrderNumber}</strong></td>
                    <td>{wo.subject}</td>
                    <td>{wo.context === "customer" ? t("contexts.customer") : t("contexts.internal")}</td>
                    <td>{wo.customerName || wo.fixedAssetId || "—"}</td>
                    <td>
                      <span
                        style={{
                          display: "inline-flex",
                          padding: "0.2rem 0.5rem",
                          borderRadius: "9999px",
                          fontSize: "0.75rem",
                          fontWeight: 600,
                          backgroundColor: wo.status === "completed" ? "#ecfdf5" : "#f1f5f9",
                          color: wo.status === "completed" ? "#065f46" : "#475569"
                        }}
                      >
                        {wo.status === "completed" ? t("billing.invoiceReady") : t("billing.pendingCompletion")}
                      </span>
                    </td>
                    <td>
                      <strong>
                        {wo.context === "customer" ? t("billing.sampleCustomerAmount", "1,500.00 SAR") : t("billing.sampleInternalAmount", "0.00 SAR (داخلي)")}
                      </strong>
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        )}
      </Surface>
    </div>
  );
}
