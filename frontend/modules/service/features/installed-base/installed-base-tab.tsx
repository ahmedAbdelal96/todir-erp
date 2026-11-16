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
  WrenchIcon,
  PlusIcon,
  ClockIcon
} from "../../components/service-icons.tsx";
import {
  useInstalledBaseQuery,
  useMeterReadingsQuery,
  useComponentEventsQuery
} from "../../hooks/use-service-queries.ts";
import { NewInstalledBaseModal } from "../modals/new-installed-base-modal.tsx";
import { RecordMeterModal } from "../modals/record-meter-modal.tsx";
import type { InstalledBaseItemDto } from "../../types.ts";

export interface InstalledBaseTabProps {
  readonly legalEntityId: string;
}

export function InstalledBaseTab({ legalEntityId }: InstalledBaseTabProps): React.JSX.Element {
  const { t } = useTranslation("service");

  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");

  const [isNewEquipmentOpen, setIsNewEquipmentOpen] = useState(false);
  const [selectedItemForMeter, setSelectedItemForMeter] = useState<InstalledBaseItemDto | null>(null);

  const installedBaseQuery = useInstalledBaseQuery(legalEntityId, {
    status: statusFilter !== "all" ? statusFilter : undefined,
    page,
    pageSize
  });

  const items = installedBaseQuery.data?.items ?? [];
  const totalCount = installedBaseQuery.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const filteredItems = items.filter((item) => {
    if (!search.trim()) return true;
    const q = search.toLowerCase();
    const sn = item.serialNumber ?? "";
    const code = item.itemCode ?? item.name ?? "";
    const name = item.itemName ?? item.name ?? "";
    const cust = item.customerName ?? item.customerPartyId ?? "";
    const site = item.siteLocation ?? item.siteName ?? "";
    return (
      sn.toLowerCase().includes(q) ||
      code.toLowerCase().includes(q) ||
      name.toLowerCase().includes(q) ||
      cust.toLowerCase().includes(q) ||
      site.toLowerCase().includes(q)
    );
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <Surface
        title={t("installedBase.title")}
        description={t("installedBase.subtitle")}
        headerActions={
          <Button
            variant="primary"
            onClick={() => setIsNewEquipmentOpen(true)}
            startIcon={<PlusIcon size={16} />}
          >
            {t("installedBase.newEquipmentTitle")}
          </Button>
        }
        variant="card"
      >
        {/* Search & Filters */}
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
              placeholder={t("installedBase.searchPlaceholder")}
            />
          </div>

          <div style={{ width: "180px" }}>
            <Select
              value={statusFilter}
              onChange={(val) => setStatusFilter(val)}
              options={[
                { value: "all", label: t("common.allStatuses") },
                { value: "operational", label: t("installedBase.operational") },
                { value: "degraded", label: t("installedBase.degraded") },
                { value: "down", label: t("installedBase.down") },
                { value: "decommissioned", label: t("installedBase.decommissioned") }
              ]}
            />
          </div>
        </div>

        {/* Equipment Table */}
        {installedBaseQuery.isLoading ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
            {t("common.loading")}
          </div>
        ) : filteredItems.length === 0 ? (
          <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
            <p style={{ margin: "0 0 0.5rem", fontSize: "1rem", fontWeight: 600 }}>
              {t("installedBase.noEquipmentFound")}
            </p>
            <p style={{ margin: 0, fontSize: "0.875rem" }}>
              {t("installedBase.noEquipmentDescription")}
            </p>
          </div>
        ) : (
          <div style={{ overflowX: "auto", marginTop: "1rem" }}>
            <Table isBordered isStriped>
              <thead>
                <tr>
                  <th>{t("installedBase.serialNumber")}</th>
                  <th>{t("parts.itemId")}</th>
                  <th>{t("installedBase.modelName")}</th>
                  <th>{t("requests.customerName")}</th>
                  <th>{t("requests.siteLocation")}</th>
                  <th>{t("common.status")}</th>
                  <th>{t("installedBase.warrantyEnd")}</th>
                  <th style={{ textAlign: "center" }}>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {filteredItems.map((item) => (
                  <tr key={item.id}>
                    <td>
                      <strong style={{ color: "#0f172a" }}>{item.serialNumber || item.id.slice(0, 8)}</strong>
                    </td>
                    <td>{item.itemCode || item.name}</td>
                    <td>{item.itemName || item.name || "—"}</td>
                    <td>{item.customerName || item.customerPartyId || "—"}</td>
                    <td>{item.siteLocation || item.siteName || "—"}</td>
                    <td>
                      <ServiceLifecycleBadge state={item.state || item.status} />
                    </td>
                    <td>
                      {item.warrantyEndDate
                        ? new Date(item.warrantyEndDate).toLocaleDateString()
                        : "—"}
                    </td>
                    <td style={{ textAlign: "center" }}>
                      <Button
                        variant="secondary"
                        size="compact"
                        onClick={() => setSelectedItemForMeter(item)}
                        startIcon={<ClockIcon size={14} />}
                      >
                        {t("installedBase.recordMeterTitle")}
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

      {/* Modals */}
      {isNewEquipmentOpen && (
        <NewInstalledBaseModal
          isOpen={isNewEquipmentOpen}
          onClose={() => setIsNewEquipmentOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}

      {selectedItemForMeter && (
        <RecordMeterModal
          isOpen={Boolean(selectedItemForMeter)}
          onClose={() => setSelectedItemForMeter(null)}
          legalEntityId={legalEntityId}
          installedBaseItemId={selectedItemForMeter.id}
          serialNumber={selectedItemForMeter.serialNumber ?? undefined}
        />
      )}
    </div>
  );
}
