import React, { useState } from "react";
import type { Party } from "../types.ts";
import { usePartyRecord, useUpdateParty } from "../hooks/party-queries.ts";
import { RecordPage } from "../../../components/pages/record-page.tsx";
import { FormSection } from "../../../components/forms/form-section.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { PartyForm } from "../features/party-form.tsx";
import type { PartyFormData } from "../validation/party-schema.ts";

export interface PartyRecordPageProps { readonly partyId?: string; readonly onBack?: (() => void) | undefined; }
function toForm(party: Party): Partial<PartyFormData> { const email = party.contactPoints.find(item => item.type === "Email")?.value ?? ""; const phone = party.contactPoints.find(item => item.type === "Phone")?.value ?? ""; const identifier = party.identifiers[0]; const address = party.addresses[0]; return { partyType: party.partyType, displayName: party.displayName, legalName: party.legalName ?? "", tradeName: party.tradeName ?? "", nameAr: party.nameAr ?? "", nameEn: party.nameEn ?? "", firstName: party.firstName ?? "", middleName: party.middleName ?? "", lastName: party.lastName ?? "", countryCode: party.countryCode ?? "EG", primaryLanguage: party.primaryLanguage ?? "ar", roles: party.roles.filter(item => item.status === "Active").map(item => item.code), email, phone, identifierType: identifier?.type ?? "", identifierValue: identifier?.value ?? "", identifierCountryCode: identifier?.countryCode ?? "EG", addressLine1: address?.line1 ?? "", addressCity: address?.city ?? "", addressCountryCode: address?.countryCode ?? "EG" }; }

export function PartyRecordPage({ partyId = "", onBack }: PartyRecordPageProps): React.JSX.Element {
  const [editing, setEditing] = useState(false); const { data: party, isLoading, error } = usePartyRecord(partyId); const update = useUpdateParty();
  if (isLoading) return <div style={{ padding: "2rem", textAlign: "center" }}>جارٍ تحميل بيانات الطرف...</div>;
  if (error || !party) return <div style={{ padding: "2rem", textAlign: "center" }}><p>تعذر تحميل السجل.</p>{onBack ? <Button onPress={onBack}>العودة</Button> : null}</div>;
  return <RecordPage title={party.displayName} recordCode={party.id} status={party.status === "Active" ? "success" : "neutral"} breadcrumbs={[{ label: "دليل الأطراف", href: "#directory" }, { label: party.displayName }]} headerActions={<><Button variant="secondary" onPress={onBack}>العودة</Button><Button variant="primary" onPress={() => setEditing(true)}>تعديل</Button></>} auditInfo={{ createdAt: party.createdAt, createdBy: "النظام" }}>
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      <FormSection title="الهوية والحالة"><div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(220px,1fr))", gap: "1rem" }}><div><small>النوع</small><div>{party.partyType === "Organization" ? "منشأة / شركة" : "فرد"}</div></div><div><small>الاسم القانوني</small><div>{party.legalName || "—"}</div></div><div><small>الحالة</small><div>{party.status}</div></div><div><small>الأدوار</small><div>{party.roles.map(item => item.code).join("، ") || "—"}</div></div></div></FormSection>
      <FormSection title="المعرفات"><div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>{party.identifiers.length ? party.identifiers.map(item => <div key={item.id}><strong>{item.type}</strong>: <span style={{ fontFamily: "var(--font-mono)" }}>{item.value}</span> <small>{item.verificationStatus}</small></div>) : "لا توجد معرفات مسجلة."}</div></FormSection>
      <FormSection title="قنوات الاتصال"><div>{party.contactPoints.length ? party.contactPoints.map(item => <div key={item.id}>{item.type}: {item.value}</div>) : "لا توجد قنوات اتصال."}</div></FormSection>
      <FormSection title="العناوين"><div>{party.addresses.length ? party.addresses.map(item => <div key={item.id}>{item.usage}: {item.line1} — {item.city ?? ""}</div>) : "لا توجد عناوين."}</div></FormSection>
    </div>
    <Dialog isOpen={editing} onOpenChange={setEditing} title="تعديل هوية الطرف" description="يحافظ التعديل على رقم الإصدار للتزامن المتفائل." size="large"><PartyForm initialData={toForm(party)} onSubmit={async (data: PartyFormData) => { await update.mutateAsync({ id: party.id, input: { ...data, version: party.version } }); setEditing(false); }} onCancel={() => setEditing(false)} isSubmitting={update.isPending} /></Dialog>
  </RecordPage>;
}
