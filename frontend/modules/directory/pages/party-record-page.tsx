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
  const [editing, setEditing] = useState(false);
  const { data: party, isLoading, error } = usePartyRecord(partyId);
  const update = useUpdateParty();

  if (isLoading) {
    return <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>جارٍ تحميل بيانات الطرف...</div>;
  }

  if (error || !party) {
    return (
      <div style={{ padding: "2rem", textAlign: "center" }}>
        <p style={{ color: "var(--color-status-danger)", fontWeight: 600 }}>تعذر تحميل السجل المطلوب.</p>
        {onBack ? <Button variant="secondary" onPress={onBack}>العودة</Button> : null}
      </div>
    );
  }

  return (
    <RecordPage
      title={party.displayName}
      recordCode={party.id}
      status={party.status === "Active" ? "success" : "neutral"}
      breadcrumbs={[{ label: "دليل الأطراف", href: "#directory" }, { label: party.displayName }]}
      headerActions={
        <div style={{ display: "flex", gap: "0.5rem" }}>
          {onBack && <Button variant="secondary" onPress={onBack}>العودة</Button>}
          <Button variant="primary" onPress={() => setEditing(true)}>تعديل البيانات</Button>
        </div>
      }
      auditInfo={{ createdAt: party.createdAt, createdBy: "النظام" }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <FormSection title="الهوية والحالة">
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1.25rem" }}>
            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>النوع:</span>
              <div style={{ fontWeight: 600, marginTop: "0.25rem" }}>{party.partyType === "Organization" ? "منشأة / شركة" : "فرد"}</div>
            </div>
            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>الاسم القانوني:</span>
              <div style={{ fontWeight: 600, marginTop: "0.25rem" }}>{party.legalName || "—"}</div>
            </div>
            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>الحالة:</span>
              <div style={{ fontWeight: 600, marginTop: "0.25rem" }}>{party.status === "Active" ? "نشط" : party.status}</div>
            </div>
            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>الأدوار:</span>
              <div style={{ fontWeight: 600, marginTop: "0.25rem" }}>{party.roles.map(item => (item.code === "Customer" ? "عميل" : "مورد")).join("، ") || "—"}</div>
            </div>
          </div>
        </FormSection>

        <FormSection title="المعرفات والسجلات">
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            {party.identifiers.length ? (
              party.identifiers.map(item => (
                <div key={item.id} style={{ display: "flex", alignItems: "center", gap: "0.75rem", fontSize: "0.875rem" }}>
                  <strong>{item.type}:</strong>
                  <bdi style={{ fontFamily: "var(--font-mono)" }}>{item.value}</bdi>
                  <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>({item.verificationStatus})</span>
                </div>
              ))
            ) : (
              <span style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>لا توجد معرفات مسجلة.</span>
            )}
          </div>
        </FormSection>

        <FormSection title="قنوات الاتصال والعناوين">
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: "1.25rem" }}>
            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>قنوات الاتصال:</span>
              <div style={{ marginTop: "0.375rem" }}>
                {party.contactPoints.length ? (
                  party.contactPoints.map(item => (
                    <div key={item.id} style={{ fontSize: "0.875rem", marginBottom: "0.25rem" }}>
                      <strong>{item.type === "Email" ? "البريد" : "الهاتف"}:</strong> <bdi dir="ltr">{item.value}</bdi>
                    </div>
                  ))
                ) : (
                  <span style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>لا توجد قنوات اتصال.</span>
                )}
              </div>
            </div>

            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>العناوين المسجلة:</span>
              <div style={{ marginTop: "0.375rem" }}>
                {party.addresses.length ? (
                  party.addresses.map(item => (
                    <div key={item.id} style={{ fontSize: "0.875rem", marginBottom: "0.25rem" }}>
                      {item.line1} — {item.city ?? ""} ({item.countryCode})
                    </div>
                  ))
                ) : (
                  <span style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>لا توجد عناوين مسجلة.</span>
                )}
              </div>
            </div>
          </div>
        </FormSection>
      </div>

      <Dialog
        isOpen={editing}
        onOpenChange={setEditing}
        title="تعديل هوية الطرف"
        description="يحافظ التعديل على رقم الإصدار للتزامن المتفائل."
        size="large"
      >
        <PartyForm
          initialData={toForm(party)}
          onSubmit={async (data: PartyFormData) => {
            await update.mutateAsync({ id: party.id, input: { ...data, version: party.version } });
            setEditing(false);
          }}
          onCancel={() => setEditing(false)}
          isSubmitting={update.isPending}
        />
      </Dialog>
    </RecordPage>
  );
}
