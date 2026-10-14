import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import type { Party, PartyRoleCode } from "../types.ts";
import { usePartyList, useCreateParty } from "../hooks/party-queries.ts";
import { partyApi } from "../services/party-api.ts";
import { ListPage } from "../../../components/pages/list-page.tsx";
import { DataGrid, type DataGridColumn } from "../../../components/grid/index.ts";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { FilterBar } from "../../../components/primitives/filter-bar.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Pagination } from "../../../components/primitives/pagination.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { ErrorState } from "../../../components/primitives/error-state.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { DirectoryIcon } from "../../../components/primitives/icons.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { PartyForm } from "../features/party-form.tsx";
import { QuickCreatePartyDialog } from "../features/quick-create-party-dialog.tsx";
import { DirectoryImportDialog } from "../features/directory-import-dialog.tsx";
import type { PartyFormData } from "../validation/party-schema.ts";
import { useToast } from "../../../components/primitives/toast.tsx";

export interface PartyListPageProps {
  readonly onSelectParty?: ((party: Party) => void) | undefined;
}

function toCreate(data: PartyFormData, primaryEmailLabel: string, primaryPhoneLabel: string) {
  return {
    partyType: data.partyType,
    displayName: data.displayName,
    legalName: data.legalName || undefined,
    tradeName: data.tradeName || undefined,
    nameAr: data.nameAr || undefined,
    nameEn: data.nameEn || undefined,
    firstName: data.firstName || undefined,
    middleName: data.middleName || undefined,
    lastName: data.lastName || undefined,
    countryCode: data.countryCode || undefined,
    primaryLanguage: data.primaryLanguage || undefined,
    roles: data.roles as PartyRoleCode[],
    identifiers: data.identifierType && data.identifierValue
      ? [{
          type: data.identifierType,
          value: data.identifierValue,
          countryCode: data.identifierCountryCode || "EG",
          isPrimary: true
        }]
      : [],
    contactPoints: [
      data.email ? { type: "Email" as const, value: data.email, label: primaryEmailLabel, isPrimary: true } : null,
      data.phone ? { type: "Phone" as const, value: data.phone, label: primaryPhoneLabel, isPrimary: true } : null
    ].filter((item): item is NonNullable<typeof item> => item !== null),
    addresses: data.addressLine1
      ? [{
          usage: "General" as const,
          countryCode: data.addressCountryCode ?? "EG",
          city: data.addressCity || undefined,
          line1: data.addressLine1,
          isDefault: true
        }]
      : []
  };
}

export function PartyListPage({ onSelectParty }: PartyListPageProps): React.JSX.Element {
  const { t } = useTranslation("directory");
  const [search, setSearch] = useState("");
  const [partyType, setPartyType] = useState("");
  const [role, setRole] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [isQuickCreateOpen, setIsQuickCreateOpen] = useState(false);
  const [isFullCreateOpen, setIsFullCreateOpen] = useState(false);
  const [isImportOpen, setIsImportOpen] = useState(false);
  const [isExporting, setIsExporting] = useState(false);

  const { showToast } = useToast();

  const { data, isLoading, error, refetch } = usePartyList({
    search: search || undefined,
    partyType: partyType || undefined,
    role: role || undefined,
    status: status || undefined,
    page,
    pageSize
  });

  const create = useCreateParty();
  const statusKey = ({ Active: "statusActive", Archived: "statusArchived", Blocked: "statusBlocked", Merged: "statusMerged", Draft: "statusDraft" } as const);
  const partyStatusLabel = (status: string) => t(`partyList.${statusKey[status as keyof typeof statusKey] ?? "statusUnknown"}`);
  const parties = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const handleSearchChange = (val: string) => {
    setSearch(val);
    setPage(1);
  };

  const handlePartyTypeChange = (key: string | number | null) => {
    setPartyType(String(key ?? ""));
    setPage(1);
  };

  const handleRoleChange = (key: string | number | null) => {
    setRole(String(key ?? ""));
    setPage(1);
  };

  const handleStatusChange = (key: string | number | null) => {
    setStatus(String(key ?? ""));
    setPage(1);
  };

  const handleExportCsv = async () => {
    try {
      setIsExporting(true);
      const csv = await partyApi.exportCsv({
        ...(search ? { search } : {}),
        includeArchived: status === "Archived"
      });
      const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `directory-parties-${new Date().toISOString().slice(0, 10)}.csv`;
      link.click();
      URL.revokeObjectURL(url);
      showToast({
        type: "success",
        title: t("partyList.exportedTitle"),
        description: t("partyList.exportedDescription")
      });
    } catch (err: unknown) {
      showToast({
        type: "error",
        title: t("partyList.exportErrorTitle"),
        description: t("partyList.exportErrorDescription")
      });
    } finally {
      setIsExporting(false);
    }
  };

  const columns: DataGridColumn<Party>[] = [
    {
      id: "displayName",
      header: t("partyList.columnParty"),
      width: "1.5fr",
      minWidth: 200,
      cellRenderer: ({ row }) => (
        <div style={{ display: "flex", flexDirection: "column", gap: "0.125rem", padding: "0.25rem 0" }}>
          <button
            type="button"
            onClick={() => onSelectParty?.(row)}
            style={{
              border: 0,
              background: "none",
              color: "var(--color-brand-primary, #4f46e5)",
              cursor: "pointer",
              fontWeight: 600,
              fontSize: "0.875rem",
              padding: 0,
              textAlign: "start",
              textDecoration: "none"
            }}
          >
            {row.displayName}
          </button>
          {row.legalName && row.legalName !== row.displayName && (
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {row.legalName}
            </span>
          )}
        </div>
      )
    },
    {
      id: "partyType",
      header: t("partyList.columnType"),
      width: 120,
      cellRenderer: ({ row }) => (
        <span style={{ fontSize: "0.8125rem", fontWeight: 500, color: "var(--color-content-secondary)" }}>
          {row.partyType === "Organization" ? t("partyList.organization") : t("partyList.person")}
        </span>
      )
    },
    {
      id: "roles",
      header: t("partyList.columnRoles"),
      width: 180,
      cellRenderer: ({ row }) => {
        const activeRoles = row.roles.filter(r => r.status === "Active");
        if (!activeRoles.length) return <span style={{ color: "var(--color-content-muted)" }}>—</span>;
        return (
          <div style={{ display: "flex", gap: "0.375rem", flexWrap: "wrap", alignItems: "center" }}>
            {activeRoles.map(r => (
              <span
                key={r.code}
                style={{
                  fontSize: "0.6875rem",
                  fontWeight: 700,
                  padding: "0.125rem 0.5rem",
                  borderRadius: "var(--radius-sm, 0.25rem)",
                  backgroundColor: r.code === "Customer" ? "var(--color-brand-subtle, #eef2ff)" : "var(--color-status-info-subtle, #f0f9ff)",
                  color: r.code === "Customer" ? "var(--color-brand-primary, #4f46e5)" : "var(--color-status-info-content, #0369a1)"
                }}
              >
                {r.code === "Customer" ? t("partyList.customer") : t("partyList.supplier")}
              </span>
            ))}
          </div>
        );
      }
    },
    {
      id: "status",
      header: t("partyList.columnStatus"),
      width: 130,
      cellRenderer: ({ row }) => {
        const isArchived = row.status === "Archived";
        const isActive = row.status === "Active";
        return (
          <StatusBadge
            status={isActive ? "success" : isArchived ? "neutral" : "warning"}
            label={partyStatusLabel(row.status)}
            size="small"
          />
        );
      }
    },
    {
      id: "updatedAt",
      header: t("partyList.columnUpdated"),
      width: 160,
      minWidth: 160,
      cellRenderer: ({ row }) => (
        <LocalDateDisplay value={row.updatedAt} />
      )
    }
  ];

  const activeFilters = (partyType ? 1 : 0) + (role ? 1 : 0) + (status ? 1 : 0) + (search ? 1 : 0);

  return (
    <ListPage
      title={t("partyList.title")}
      subtitle={t("partyList.subtitle")}
      breadcrumbs={[{ label: t("partyList.breadcrumbParties") }, { label: t("partyList.breadcrumbCustomersSuppliers") }]}
      primaryAction={{
        label: t("partyList.addParty"),
        onClick: () => setIsQuickCreateOpen(true)
      }}
      secondaryActions={
        <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
          <Button variant="secondary" size="compact" onPress={() => setIsImportOpen(true)}>
            {t("partyList.import")}
          </Button>
          <Button variant="secondary" size="compact" onPress={handleExportCsv} isDisabled={isExporting}>
            {isExporting ? t("partyList.exporting") : t("partyList.export")}
          </Button>
        </div>
      }
      filterBar={
        <FilterBar
          searchInput={
            <SearchField
              ariaLabel={t("partyList.searchLabel")}
              placeholder={t("partyList.searchPlaceholder")}
              value={search}
              onChange={handleSearchChange}
            />
          }
          activeFilterCount={activeFilters}
          onResetFilters={() => {
            setSearch("");
            setPartyType("");
            setRole("");
            setStatus("");
            setPage(1);
          }}
        >
          <div style={{ minWidth: "150px" }}>
            <Select
              label={t("partyList.entityNature")}
              selectedKey={partyType}
              onSelectionChange={handlePartyTypeChange}
            >
              <SelectItem id="" textValue={t("partyList.allEntities")}>{t("partyList.allEntities")}</SelectItem>
              <SelectItem id="Organization" textValue={t("partyList.organizations")}>{t("partyList.organizations")}</SelectItem>
              <SelectItem id="Person" textValue={t("partyList.people")}>{t("partyList.people")}</SelectItem>
            </Select>
          </div>

          <div style={{ minWidth: "150px" }}>
            <Select
              label={t("partyList.businessRole")}
              selectedKey={role}
              onSelectionChange={handleRoleChange}
            >
              <SelectItem id="" textValue={t("partyList.allRoles")}>{t("partyList.allRoles")}</SelectItem>
              <SelectItem id="Customer" textValue={t("partyList.customersOnly")}>{t("partyList.customersOnly")}</SelectItem>
              <SelectItem id="Supplier" textValue={t("partyList.suppliersOnly")}>{t("partyList.suppliersOnly")}</SelectItem>
            </Select>
          </div>

          <div style={{ minWidth: "140px" }}>
            <Select
              label={t("partyList.recordStatus")}
              selectedKey={status}
              onSelectionChange={handleStatusChange}
            >
              <SelectItem id="" textValue={t("partyList.allStatuses")}>{t("partyList.allStatuses")}</SelectItem>
              <SelectItem id="Active" textValue={t("partyList.activeOnly")}>{t("partyList.activeOnly")}</SelectItem>
              <SelectItem id="Archived" textValue={t("partyList.archivedOnly")}>{t("partyList.archivedOnly")}</SelectItem>
            </Select>
          </div>
        </FilterBar>
      }
      pagination={
        totalCount > 0 ? (
          <Pagination
            page={page}
            totalPages={totalPages}
            totalItems={totalCount}
            pageSize={pageSize}
            pageSizeOptions={[10, 25, 50]}
            onPageChange={setPage}
            onPageSizeChange={(newSize) => {
              setPageSize(newSize);
              setPage(1);
            }}
          />
        ) : undefined
      }
    >
      {error ? (
        <ErrorState
          title={t("partyList.errorTitle")}
          message={t("partyList.errorDescription")}
          onRetry={() => void refetch()}
        />
      ) : !isLoading && parties.length === 0 ? (
        <EmptyState
          icon={<DirectoryIcon size={28} />}
          title={t("partyList.emptyTitle")}
          description={
            activeFilters > 0
              ? t("partyList.emptyFiltered")
              : t("partyList.emptyStart")
          }
          action={
            <Button variant="primary" onPress={() => setIsQuickCreateOpen(true)}>
              {t("partyList.addNewParty")}
            </Button>
          }
        />
      ) : (
        <DataGrid<Party>
          columns={columns}
          staticRows={parties}
          selectable="single"
          loading={isLoading}
          hasActiveFilters={activeFilters > 0}
          loadingMessage={isLoading ? t("partyList.loading") : undefined}
          onSelectionChange={(rows) => rows[0] && onSelectParty?.(rows[0])}
        />
      )}

      {/* Quick Create Dialog (Section 49 canonical pack) */}
      <QuickCreatePartyDialog
        isOpen={isQuickCreateOpen}
        onOpenChange={setIsQuickCreateOpen}
        onCreated={(created) => {
          void refetch();
          if (onSelectParty) {
            onSelectParty(created);
          }
        }}
        onOpenFullForm={() => setIsFullCreateOpen(true)}
      />

      {/* Full Detailed Party Creation Dialog */}
      <Dialog
        isOpen={isFullCreateOpen}
        onOpenChange={setIsFullCreateOpen}
        title={t("partyList.fullCreateTitle")}
        description={t("partyList.fullCreateDescription")}
        size="large"
      >
        <PartyForm
          onSubmit={async (formData) => {
            try {
              const created = await create.mutateAsync(toCreate(formData, t("partyList.primaryEmail"), t("partyList.primaryPhone")));
              setIsFullCreateOpen(false);
              showToast({
                type: "success",
                title: t("partyList.createSuccessTitle"),
                description: t("partyList.createSuccessDescription", { name: created.displayName })
              });
              if (onSelectParty) {
                onSelectParty(created);
              }
            } catch (err: unknown) {
              showToast({
                type: "error",
                title: t("partyList.createErrorTitle"),
                description: t("partyList.createErrorDescription")
              });
            }
          }}
          onCancel={() => setIsFullCreateOpen(false)}
          isSubmitting={create.isPending}
        />
      </Dialog>

      {/* Canonical Directory Import Wizard Dialog */}
      <DirectoryImportDialog
        isOpen={isImportOpen}
        onOpenChange={setIsImportOpen}
        onImportCompleted={() => {
          void refetch();
        }}
      />
    </ListPage>
  );
}
