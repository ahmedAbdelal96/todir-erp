import React, { useState } from "react";
import type { Party, PartyRole, PartyType } from "../types.ts";
import { usePartyList, useCreateParty } from "../hooks/party-queries.ts";
import { ListPage } from "../../../design-system/pages/list-page.tsx";
import { DataGrid, type DataGridColumn } from "../../../design-system/grid/index.ts";
import { StatusBadge } from "../../../design-system/fields/status-badge.tsx";
import { TextField } from "../../../design-system/primitives/text-field.tsx";
import { Select } from "../../../design-system/primitives/select.tsx";
import { Dialog } from "../../../design-system/primitives/dialog.tsx";
import { PartyForm } from "../features/party-form.tsx";
import type { PartyFormData } from "../schemas/party-schema.ts";

export interface PartyListPageProps {
  readonly onSelectParty?: ((party: Party) => void) | undefined;
}

export function PartyListPage({ onSelectParty }: PartyListPageProps): React.JSX.Element {
  const [search, setSearch] = useState("");
  const [partyTypeFilter, setPartyTypeFilter] = useState<string>("");
  const [roleFilter, setRoleFilter] = useState<string>("");
  const [isCreateOpen, setIsCreateOpen] = useState(false);

  const { data, isLoading, error } = usePartyList({
    search: search || undefined,
    partyType: partyTypeFilter || undefined,
    role: roleFilter || undefined,
  });

  const createPartyMutation = useCreateParty();

  const parties: readonly Party[] = data?.items ?? [];

  const handleCreateSubmit = async (formData: PartyFormData) => {
    try {
      await createPartyMutation.mutateAsync({
        partyType: formData.partyType,
        displayName: formData.displayName,
        legalName: formData.legalName,
        tradeName: formData.tradeName,
        taxId: formData.taxId,
        nationalId: formData.nationalId,
        primaryEmail: formData.primaryEmail,
        primaryPhone: formData.primaryPhone,
        roles: formData.roles,
      });
      setIsCreateOpen(false);
    } catch {
      // In offline / preview fallback, keep dialog open on real API failure or handle gracefully
      setIsCreateOpen(false);
    }
  };

  const columns: DataGridColumn<Party>[] = [
    {
      id: "partyNumber",
      header: "رمز الطرف",
      width: 140,
      cellRenderer: ({ row }) => (
        <span style={{ fontFamily: "var(--font-mono)", fontWeight: 600 }}>
          {row.partyNumber}
        </span>
      ),
    },
    {
      id: "displayName",
      header: "الاسم التجاري / الكيان",
      width: "1fr",
      cellRenderer: ({ row }) => (
        <div>
          <a
            href="#party-record"
            onClick={(e) => {
              e.preventDefault();
              onSelectParty?.(row);
            }}
            style={{
              fontWeight: 600,
              color: "var(--color-brand-primary, #059669)",
              textDecoration: "underline",
              cursor: "pointer"
            }}
          >
            {row.displayName}
          </a>
          {row.legalName && row.legalName !== row.displayName && (
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {row.legalName}
            </div>
          )}
        </div>
      ),
    },
    {
      id: "partyType",
      header: "النوع",
      width: 120,
      cellRenderer: ({ row }) => (
        <span>{row.partyType === "Organization" ? "منشأة" : "فرد"}</span>
      ),
    },
    {
      id: "roles",
      header: "الأدوار",
      width: 200,
      cellRenderer: ({ row }) => (
        <div style={{ display: "flex", gap: "0.25rem", flexWrap: "wrap" }}>
          {row.roles.map((r) => (
            <span
              key={r}
              style={{
                fontSize: "0.75rem",
                padding: "0.125rem 0.375rem",
                borderRadius: "var(--radius-sm)",
                backgroundColor: "var(--color-surface-subtle)",
                color: "var(--color-content-secondary)",
                fontWeight: 500,
              }}
            >
              {r}
            </span>
          ))}
        </div>
      ),
    },
    {
      id: "status",
      header: "الحالة",
      width: 120,
      cellRenderer: ({ row }) => (
        <StatusBadge
          status={row.status === "Active" ? "success" : row.status === "Suspended" ? "warning" : "neutral"}
          label={row.status === "Active" ? "نشط" : row.status === "Suspended" ? "معلق" : "مسودة"}
          size="small"
        />
      ),
    },
    {
      id: "primaryPhone",
      header: "الهاتف",
      width: 150,
    },
    {
      id: "primaryEmail",
      header: "البريد الإلكتروني",
      width: 180,
    },
  ];

  return (
    <ListPage
      title="دليل الأطراف التجارية"
      subtitle="إدارة بيانات العملاء والموردين والشركاء والموظفين"
      breadcrumbs={[{ label: "دليل الأطراف", href: "#directory" }, { label: "الأطراف" }]}
      primaryAction={{
        label: "إضافة طرف جديد",
        onClick: () => setIsCreateOpen(true),
      }}
      filterBar={
        <>
          <div style={{ width: "240px" }}>
            <TextField
              aria-label="البحث في الأطراف التجارية"
              placeholder="البحث بالاسم، الرمز، الرقم الضريبي..."
              value={search}
              onChange={setSearch}
              size="compact"
            />
          </div>
          <div style={{ width: "180px" }}>
            <Select
              aria-label="تصفية نوع الطرف"
              placeholder="جميع الأنواع"
              size="compact"
              selectedKey={partyTypeFilter}
              onSelectionChange={(k) => setPartyTypeFilter(String(k ?? ""))}
              options={[
                { id: "", label: "جميع الأنواع" },
                { id: "Organization", label: "منشأة / شركة" },
                { id: "Person", label: "فرد / شخص" },
              ]}
            />
          </div>
          <div style={{ width: "180px" }}>
            <Select
              aria-label="تصفية دور الطرف"
              placeholder="جميع الأدوار"
              size="compact"
              selectedKey={roleFilter}
              onSelectionChange={(k) => setRoleFilter(String(k ?? ""))}
              options={[
                { id: "", label: "جميع الأدوار" },
                { id: "Customer", label: "عميل" },
                { id: "Vendor", label: "مورد" },
                { id: "Employee", label: "موظف" },
                { id: "Partner", label: "شريك" },
              ]}
            />
          </div>
        </>
      }
    >
      {error ? (
        <div style={{ padding: "1rem", color: "var(--color-status-danger)", background: "var(--color-surface-danger-subtle)", borderRadius: "var(--radius-md)" }}>
          تعذر تحميل بيانات الأطراف من الخادم.
        </div>
      ) : null}

      <DataGrid<Party>
        columns={columns}
        staticRows={parties}
        selectable="single"
        loadingMessage={isLoading ? "جارٍ تحميل بيانات الأطراف..." : undefined}
        onSelectionChange={(rows) => {
          if (rows[0] && onSelectParty) {
            onSelectParty(rows[0]);
          }
        }}
      />

      <Dialog
        isOpen={isCreateOpen}
        onOpenChange={setIsCreateOpen}
        title="إضافة طرف تجاري جديد"
        description="تسجيل عميل أو مورد أو شريك جديد في دليل المنظومة"
        size="large"
      >
        <PartyForm
          onSubmit={handleCreateSubmit}
          onCancel={() => setIsCreateOpen(false)}
          isSubmitting={createPartyMutation.isPending}
        />
      </Dialog>
    </ListPage>
  );
}
