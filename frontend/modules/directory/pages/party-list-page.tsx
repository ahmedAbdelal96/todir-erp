import React, { useState } from "react";
import type { Party, PartyRoleCode } from "../types.ts";
import { usePartyList, useCreateParty } from "../hooks/party-queries.ts";
import { ListPage } from "../../../components/pages/list-page.tsx";
import { DataGrid, type DataGridColumn } from "../../../components/grid/index.ts";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { PartyForm } from "../features/party-form.tsx";
import type { PartyFormData } from "../validation/party-schema.ts";

export interface PartyListPageProps { readonly onSelectParty?: ((party: Party) => void) | undefined; }
function toCreate(data: PartyFormData) { return { partyType: data.partyType, displayName: data.displayName, legalName: data.legalName, tradeName: data.tradeName, nameAr: data.nameAr, nameEn: data.nameEn, firstName: data.firstName, middleName: data.middleName, lastName: data.lastName, countryCode: data.countryCode, primaryLanguage: data.primaryLanguage, roles: data.roles as PartyRoleCode[], identifiers: data.identifierType && data.identifierValue ? [{ type: data.identifierType, value: data.identifierValue, countryCode: data.identifierCountryCode, isPrimary: true }] : [], contactPoints: [{ type: "Email" as const, value: data.email ?? "", isPrimary: true }, { type: "Phone" as const, value: data.phone ?? "", isPrimary: true }].filter(item => item.value), addresses: data.addressLine1 ? [{ usage: "General" as const, countryCode: data.addressCountryCode ?? "EG", city: data.addressCity, line1: data.addressLine1, isDefault: true }] : [] }; }

export function PartyListPage({ onSelectParty }: PartyListPageProps): React.JSX.Element {
  const [search, setSearch] = useState(""); const [partyType, setPartyType] = useState(""); const [role, setRole] = useState(""); const [isCreateOpen, setIsCreateOpen] = useState(false);
  const { data, isLoading, error } = usePartyList({ search: search || undefined, partyType: partyType || undefined, role: role || undefined }); const create = useCreateParty(); const parties = data?.items ?? [];
  const columns: DataGridColumn<Party>[] = [
    { id: "displayName", header: "الطرف", width: "1fr", cellRenderer: ({ row }) => <button type="button" onClick={() => onSelectParty?.(row)} style={{ border: 0, background: "none", color: "var(--color-brand-primary)", cursor: "pointer", fontWeight: 600 }}>{row.displayName}</button> },
    { id: "partyType", header: "النوع", width: 130, cellRenderer: ({ row }) => row.partyType === "Organization" ? "منشأة" : "فرد" },
    { id: "roles", header: "الأدوار", width: 190, cellRenderer: ({ row }) => row.roles.map(item => item.code === "Customer" ? "عميل" : "مورد").join("، ") },
    { id: "status", header: "الحالة", width: 120, cellRenderer: ({ row }) => <StatusBadge status={row.status === "Active" ? "success" : row.status === "Blocked" ? "warning" : "neutral"} label={row.status === "Active" ? "نشط" : row.status === "Archived" ? "مؤرشف" : row.status} size="small" /> },
    { id: "updatedAt", header: "آخر تحديث", width: 160, cellRenderer: ({ row }) => new Date(row.updatedAt).toLocaleDateString("ar-EG") }
  ];
  return <ListPage title="دليل الأطراف" subtitle="هوية موحدة للعملاء والموردين وسائر العلاقات" breadcrumbs={[{ label: "دليل الأطراف" }, { label: "الأطراف" }]} primaryAction={{ label: "إضافة طرف", onClick: () => setIsCreateOpen(true) }} filterBar={<div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}><input aria-label="البحث في الأطراف" placeholder="بحث بالاسم أو المعرف أو الاتصال" value={search} onChange={event => setSearch(event.target.value)} /><select aria-label="نوع الطرف" value={partyType} onChange={event => setPartyType(event.target.value)}><option value="">كل الأنواع</option><option value="Organization">منشأة</option><option value="Person">فرد</option></select><select aria-label="دور الطرف" value={role} onChange={event => setRole(event.target.value)}><option value="">كل الأدوار</option><option value="Customer">عميل</option><option value="Supplier">مورد</option></select></div>}>
    {error ? <div role="alert" style={{ padding: "1rem", color: "var(--color-status-danger)" }}>تعذر تحميل دليل الأطراف.</div> : null}<DataGrid<Party> columns={columns} staticRows={parties} selectable="single" loadingMessage={isLoading ? "جارٍ التحميل..." : undefined} onSelectionChange={rows => rows[0] && onSelectParty?.(rows[0])} />
    <Dialog isOpen={isCreateOpen} onOpenChange={setIsCreateOpen} title="إضافة طرف جديد" description="أنشئ Party واحدًا ثم أضف أدواره وبياناته المساندة." size="large"><PartyForm onSubmit={async data => { await create.mutateAsync(toCreate(data)); setIsCreateOpen(false); }} onCancel={() => setIsCreateOpen(false)} isSubmitting={create.isPending} /></Dialog>
  </ListPage>;
}
