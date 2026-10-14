import React, { useState } from "react";
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

function toCreate(data: PartyFormData) {
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
      data.email ? { type: "Email" as const, value: data.email, label: "البريد الرئيسي", isPrimary: true } : null,
      data.phone ? { type: "Phone" as const, value: data.phone, label: "الهاتف الرئيسي", isPrimary: true } : null
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

function partyStatusLabel(status: string): string {
  if (status === "Active") return "نشط";
  if (status === "Archived") return "مؤرشف";
  if (status === "Blocked") return "محظور";
  if (status === "Merged") return "مدموج";
  if (status === "Draft") return "مسودة";
  return "حالة غير متاحة";
}

export function PartyListPage({ onSelectParty }: PartyListPageProps): React.JSX.Element {
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
        title: "تم تصدير البيانات بنجاح",
        description: "تم تجهيز ملف CSV وتنزيله."
      });
    } catch (err: unknown) {
      showToast({
        type: "error",
        title: "تعذر تصدير السجلات",
        description: (err as Error)?.message || "حدث خطأ أثناء التصدير."
      });
    } finally {
      setIsExporting(false);
    }
  };

  const columns: DataGridColumn<Party>[] = [
    {
      id: "displayName",
      header: "الطرف التجاري",
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
      header: "النوع",
      width: 120,
      cellRenderer: ({ row }) => (
        <span style={{ fontSize: "0.8125rem", fontWeight: 500, color: "var(--color-content-secondary)" }}>
          {row.partyType === "Organization" ? "منشأة / شركة" : "فرد مستقل"}
        </span>
      )
    },
    {
      id: "roles",
      header: "الأدوار",
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
                {r.code === "Customer" ? "عميل" : "مورد"}
              </span>
            ))}
          </div>
        );
      }
    },
    {
      id: "status",
      header: "الحالة",
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
      header: "آخر تحديث",
      width: 130,
      cellRenderer: ({ row }) => (
        <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
          {new Date(row.updatedAt).toLocaleDateString("ar-EG")}
        </span>
      )
    }
  ];

  const activeFilters = (partyType ? 1 : 0) + (role ? 1 : 0) + (status ? 1 : 0) + (search ? 1 : 0);

  return (
    <ListPage
      title="دليل العلاقات التجارية"
      subtitle="إدارة موحدة للعملاء والموردين وجهات الاتصال وسجلات التوثيق الرسمية"
      breadcrumbs={[{ label: "دليل العلاقات التجارية" }, { label: "العملاء والموردون" }]}
      primaryAction={{
        label: "إضافة طرف تجاري",
        onClick: () => setIsQuickCreateOpen(true)
      }}
      secondaryActions={
        <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
          <Button variant="secondary" size="compact" onPress={() => setIsImportOpen(true)}>
            استيراد سجلات
          </Button>
          <Button variant="secondary" size="compact" onPress={handleExportCsv} isDisabled={isExporting}>
            {isExporting ? "جارٍ التصدير..." : "تصدير CSV"}
          </Button>
        </div>
      }
      filterBar={
        <FilterBar
          searchInput={
            <SearchField
              ariaLabel="البحث في الأطراف والعملاء والموردين"
              placeholder="ابحث بالاسم، السجل الضريبي، البريد، أو الهاتف..."
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
              label="طبيعة الكيان"
              selectedKey={partyType}
              onSelectionChange={handlePartyTypeChange}
            >
              <SelectItem id="" textValue="كل الكيانات">كل الكيانات</SelectItem>
              <SelectItem id="Organization" textValue="منشآت وشركات">منشآت وشركات</SelectItem>
              <SelectItem id="Person" textValue="أفراد مستقلون">أفراد مستقلون</SelectItem>
            </Select>
          </div>

          <div style={{ minWidth: "150px" }}>
            <Select
              label="الدور التجاري"
              selectedKey={role}
              onSelectionChange={handleRoleChange}
            >
              <SelectItem id="" textValue="كل الأدوار">كل الأدوار</SelectItem>
              <SelectItem id="Customer" textValue="العملاء فقط">العملاء فقط</SelectItem>
              <SelectItem id="Supplier" textValue="الموردون فقط">الموردون فقط</SelectItem>
            </Select>
          </div>

          <div style={{ minWidth: "140px" }}>
            <Select
              label="حالة السجل"
              selectedKey={status}
              onSelectionChange={handleStatusChange}
            >
              <SelectItem id="" textValue="الكل (نشط ومؤرشف)">الكل (نشط ومؤرشف)</SelectItem>
              <SelectItem id="Active" textValue="النشطة فقط">النشطة فقط</SelectItem>
              <SelectItem id="Archived" textValue="المؤرشفة">المؤرشفة</SelectItem>
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
            language="ar"
          />
        ) : undefined
      }
    >
      {error ? (
        <ErrorState
          title="تعذر تحميل دليل الأطراف التجارية"
          message="حدث خطأ أثناء جلب قائمة الأطراف من الخادم. يرجى إعادة المحاولة."
          onRetry={() => void refetch()}
        />
      ) : !isLoading && parties.length === 0 ? (
        <EmptyState
          icon={<DirectoryIcon size={28} />}
          title="لا توجد أطراف تجارية مسجلة"
          description={
            activeFilters > 0
              ? "لم يتم العثور على أطراف تجارية تطابق معايير وفلاتر البحث المحددة."
              : "ابدأ بتسجيل أول عميل أو مورد لفتح الحسابات والمعاملات التجارية للمؤسسة."
          }
          action={
            <Button variant="primary" onPress={() => setIsQuickCreateOpen(true)}>
              إضافة طرف تجاري جديد
            </Button>
          }
        />
      ) : (
        <DataGrid<Party>
          columns={columns}
          staticRows={parties}
          locale="ar"
          selectable="single"
          enableRtl={true}
          loading={isLoading}
          hasActiveFilters={activeFilters > 0}
          loadingMessage={isLoading ? "جارٍ تحميل السجلات..." : undefined}
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
        title="التسجيل المفصل لطرف تجاري"
        description="تسجيل كامل وموسع للهوية التجارية، الأدوار، المعرفات الضريبية، والعناوين التفصيلية."
        size="large"
      >
        <PartyForm
          onSubmit={async (formData) => {
            try {
              const created = await create.mutateAsync(toCreate(formData));
              setIsFullCreateOpen(false);
              showToast({
                type: "success",
                title: "تمت إضافة الطرف التجاري بنجاح",
                description: `تم حفظ ${created.displayName} في الدليل.`
              });
              if (onSelectParty) {
                onSelectParty(created);
              }
            } catch (err: unknown) {
              showToast({
                type: "error",
                title: "تعذر حفظ الطرف التجاري",
                description: (err as Error)?.message || "حدث خطأ أثناء الاتصال بالخادم."
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
