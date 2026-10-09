import React, { useState } from "react";
import type { Party } from "../types.ts";
import { usePartyRecord, useUpdateParty } from "../hooks/party-queries.ts";
import { RecordPage } from "../../../design-system/pages/record-page.tsx";
import { FormSection } from "../../../design-system/forms/form-section.tsx";
import { Button } from "../../../design-system/primitives/button.tsx";
import { Dialog } from "../../../design-system/primitives/dialog.tsx";
import { PartyForm } from "../features/party-form.tsx";
import type { PartyFormData } from "../schemas/party-schema.ts";

export interface PartyRecordPageProps {
  readonly partyId?: string | undefined;
  readonly onBack?: (() => void) | undefined;
}

export function PartyRecordPage({ partyId = "party-001", onBack }: PartyRecordPageProps): React.JSX.Element {
  const [isEditOpen, setIsEditOpen] = useState(false);

  const { data: party, isLoading, error } = usePartyRecord(partyId);
  const updatePartyMutation = useUpdateParty();

  if (isLoading) {
    return <div style={{ padding: "2rem", textAlign: "center" }}>جارٍ تحميل تفاصيل الطرف...</div>;
  }

  if (error || !party) {
    return (
      <div style={{ padding: "2rem", textAlign: "center" }}>
        <p>تعذر تحميل بيانات السجل أو أن السجل غير موجود.</p>
        {onBack && <Button onPress={onBack}>العودة للقائمة</Button>}
      </div>
    );
  }

  const handleEditSubmit = async (formData: PartyFormData) => {
    try {
      await updatePartyMutation.mutateAsync({
        id: party.id,
        input: formData,
      });
      setIsEditOpen(false);
    } catch {
      setIsEditOpen(false);
    }
  };

  return (
    <RecordPage
      title={party.displayName}
      recordCode={party.partyNumber}
      status={party.status === "Active" ? "success" : party.status === "Suspended" ? "warning" : "neutral"}
      breadcrumbs={[
        { label: "دليل الأطراف", href: "#directory" },
        { label: "الأطراف", href: "#parties", onClick: onBack },
        { label: party.displayName },
      ]}
      headerActions={
        <>
          {onBack && <Button variant="secondary" onPress={onBack}>العودة</Button>}
          <Button variant="primary" onPress={() => setIsEditOpen(true)}>تعديل البيانات</Button>
        </>
      }
      auditInfo={{
        createdAt: party.createdAt,
        createdBy: "النظام الرئيسي",
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        <FormSection title="البيانات القانونية والتعريفية">
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
            <div>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>الاسم القانوني</div>
              <div style={{ fontWeight: 600 }}>{party.legalName || "—"}</div>
            </div>
            <div>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>نوع الطرف</div>
              <div style={{ fontWeight: 600 }}>{party.partyType === "Organization" ? "منشأة / شركة" : "فرد"}</div>
            </div>
            <div>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>الرقم الضريبي</div>
              <div style={{ fontWeight: 600, fontFamily: "var(--font-mono)" }}>{party.taxId || "—"}</div>
            </div>
            <div>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>الهوية الوطنية / الإقامة</div>
              <div style={{ fontWeight: 600, fontFamily: "var(--font-mono)" }}>{party.nationalId || "—"}</div>
            </div>
          </div>
        </FormSection>

        <FormSection title="بيانات الاتصال">
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
            <div>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>البريد الإلكتروني الأساسي</div>
              <div>{party.primaryEmail || "—"}</div>
            </div>
            <div>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>رقم الهاتف الأساسي</div>
              <div style={{ direction: "ltr", textAlign: "end" }}>{party.primaryPhone || "—"}</div>
            </div>
          </div>
        </FormSection>
      </div>

      <Dialog
        isOpen={isEditOpen}
        onOpenChange={setIsEditOpen}
        title="تعديل بيانات الطرف التجاري"
        description={`تحديث بيانات السجل: ${party.displayName}`}
        size="large"
      >
        <PartyForm
          initialData={{
            ...party,
            roles: [...party.roles]
          }}
          onSubmit={handleEditSubmit}
          onCancel={() => setIsEditOpen(false)}
          isSubmitting={updatePartyMutation.isPending}
        />
      </Dialog>
    </RecordPage>
  );
}
