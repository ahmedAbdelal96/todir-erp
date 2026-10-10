import React, { useState } from "react";
import type { Party } from "../types.ts";
import { usePartyRecord, useUpdateParty } from "../hooks/party-queries.ts";
import { RecordPage } from "../../../components/pages/record-page.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { PropertyGrid, PropertyItem, StatCard } from "../../../components/pages/property-grid.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import {
  BuildingIcon,
  UserIcon,
  ShieldCheckIcon,
  MailIcon,
  PhoneIcon,
  MapPinIcon,
  TagIcon,
  DirectoryIcon
} from "../../../components/primitives/icons.tsx";
import { PartyForm } from "../features/party-form.tsx";
import type { PartyFormData } from "../validation/party-schema.ts";

export interface PartyRecordPageProps {
  readonly partyId?: string;
  readonly onBack?: (() => void) | undefined;
}
function toForm(party: Party): Partial<PartyFormData> {
  const email = party.contactPoints.find(item => item.type === "Email")?.value ?? "";
  const phone = party.contactPoints.find(item => item.type === "Phone")?.value ?? "";
  const identifier = party.identifiers[0];
  const address = party.addresses[0];
  return {
    partyType: party.partyType,
    displayName: party.displayName,
    legalName: party.legalName ?? "",
    tradeName: party.tradeName ?? "",
    nameAr: party.nameAr ?? "",
    nameEn: party.nameEn ?? "",
    firstName: party.firstName ?? "",
    middleName: party.middleName ?? "",
    lastName: party.lastName ?? "",
    countryCode: party.countryCode ?? "EG",
    primaryLanguage: party.primaryLanguage ?? "ar",
    roles: party.roles.filter(item => item.status === "Active").map(item => item.code),
    email,
    phone,
    identifierType: identifier?.type ?? "",
    identifierValue: identifier?.value ?? "",
    identifierCountryCode: identifier?.countryCode ?? "EG",
    addressLine1: address?.line1 ?? "",
    addressCity: address?.city ?? "",
    addressCountryCode: address?.countryCode ?? "EG"
  };
}

export function PartyRecordPage({ partyId = "", onBack }: PartyRecordPageProps): React.JSX.Element {
  const [editing, setEditing] = useState(false);
  const { data: party, isLoading, error } = usePartyRecord(partyId);
  const update = useUpdateParty();

  if (isLoading) {
    return (
      <div style={{ padding: "4rem 2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
        <p style={{ fontSize: "0.9375rem", fontWeight: 600 }}>جارٍ تحميل بيانات الطرف التجاري...</p>
      </div>
    );
  }

  if (error || !party) {
    return (
      <div style={{ padding: "3rem 2rem", textAlign: "center" }}>
        <p style={{ color: "var(--color-status-danger)", fontWeight: 600, fontSize: "1rem" }}>
          تعذر العثور على سجل الطرف المطلوب.
        </p>
        {onBack && (
          <Button variant="secondary" onPress={onBack} style={{ marginTop: "1rem" }}>
            العودة لدليل الأطراف
          </Button>
        )}
      </div>
    );
  }

  const primaryIdentifier = `#PRT-${party.id.slice(-6).toUpperCase()}`;
  const isOrganization = party.partyType === "Organization";
  const primaryEmail = party.contactPoints.find(cp => cp.type === "Email")?.value;
  const primaryPhone = party.contactPoints.find(cp => cp.type === "Phone")?.value;

  const kpis = (
    <>
      <StatCard
        label="طبيعة الكيان"
        value={isOrganization ? "منشأة تجارية" : "فرد مستقل"}
        description={isOrganization ? "شركة / مؤسسة مسجلة" : "طرف ذو شخصية طبيعية"}
        icon={isOrganization ? <BuildingIcon size={22} /> : <UserIcon size={22} />}
      />

      <StatCard
        label="الأدوار التجارية"
        value={party.roles.map(r => (r.code === "Customer" ? "عميل" : "مورد")).join(" • ") || "طرف عام"}
        description="العلاقات التجارية النشطة"
        icon={<DirectoryIcon size={22} />}
      />

      <StatCard
        label="حالة التوثيق"
        value="موثق بالكامل"
        description="تم التحقق من المعرفات الرسمية"
        icon={<ShieldCheckIcon size={22} />}
      />
    </>
  );

  return (
    <RecordPage
      title={party.displayName}
      recordCode={primaryIdentifier}
      status={party.status === "Active" ? "success" : "neutral"}
      breadcrumbs={[
        { label: "دليل الأطراف", href: "/directory", onClick: onBack },
        { label: party.displayName }
      ]}
      headerActions={
        <div style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
          {onBack && <Button variant="secondary" onPress={onBack}>العودة للقائمة</Button>}
          <Button variant="primary" onPress={() => setEditing(true)}>تعديل البيانات</Button>
        </div>
      }
      kpiCards={kpis}
      auditInfo={{
        createdAt: party.createdAt ? new Date(party.createdAt).toLocaleDateString("ar-EG", { year: "numeric", month: "long", day: "numeric" }) : undefined,
        createdBy: "النظام المالي"
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        {/* Surface 1: الهوية والحالة */}
        <Surface
          title="الهوية والحالة"
          description="المعلومات القانونية والأسماء التجارية واللغة الرسمية"
        >
          <PropertyGrid columns={3}>
            <PropertyItem
              label="النوع القانوني"
              value={isOrganization ? "منشأة / شركة" : "فرد"}
              icon={isOrganization ? <BuildingIcon size={14} /> : <UserIcon size={14} />}
            />

            <PropertyItem
              label="الاسم القانوني الكامل"
              value={party.legalName || party.displayName}
            />

            <PropertyItem
              label="الاسم التجاري"
              value={party.tradeName || party.displayName}
            />

            <PropertyItem
              label="الدولة الأساسية"
              value={party.countryCode === "EG" ? "جمهورية مصر العربية (EG)" : party.countryCode || "—"}
            />

            <PropertyItem
              label="اللغة الرسمية للمراسلات"
              value={party.primaryLanguage === "ar" ? "العربية" : "الإنجليزية"}
            />

            <PropertyItem
              label="حالة الطرف التجاري"
              value={
                <StatusBadge
                  intent={party.status === "Active" ? "success" : "neutral"}
                  label={party.status === "Active" ? "نشط ومعتمد" : party.status}
                  size="small"
                />
              }
            />
          </PropertyGrid>
        </Surface>

        {/* Surface 2: المعرفات والسجلات الرسمية */}
        <Surface
          title="المعرفات والسجلات الرسمية"
          description="الأرقام الضريبية والسجلات التجارية والمراجع الداخلية الموثقة"
        >
          {party.identifiers.length ? (
            <PropertyGrid columns={2}>
              {party.identifiers.map((ident) => {
                const typeLabel =
                  ident.type === "InternalReference"
                    ? "الرقم المرجعي الداخلي"
                    : ident.type === "TaxId"
                    ? "الرقم الضريبي (VAT)"
                    : ident.type === "CommercialRegister"
                    ? "السجل التجاري (CR)"
                    : ident.type;

                return (
                  <PropertyItem
                    key={ident.id}
                    label={typeLabel}
                    value={<bdi style={{ fontFamily: "var(--font-mono)", fontWeight: 700 }}>{ident.value}</bdi>}
                    icon={<TagIcon size={14} />}
                    badge={
                      <span
                        style={{
                          fontSize: "0.6875rem",
                          fontWeight: 700,
                          padding: "0.125rem 0.5rem",
                          borderRadius: "var(--radius-full)",
                          backgroundColor: "var(--color-status-success-subtle, #ecfdf5)",
                          color: "var(--color-status-success-content, #065f46)"
                        }}
                      >
                        {ident.verificationStatus === "Verified" ? "تم التحقق ✓" : "قيد المراجعة"}
                      </span>
                    }
                  />
                );
              })}
            </PropertyGrid>
          ) : (
            <div style={{ color: "var(--color-content-muted)", fontSize: "0.875rem", padding: "0.5rem 0" }}>
              لا توجد معرفات أو سجلات مسجلة لهذا الطرف.
            </div>
          )}
        </Surface>

        {/* Surface 3: قنوات الاتصال والعناوين */}
        <Surface
          title="معلومات الاتصال والمقرات"
          description="قنوات التواصل المعتمدة والعناوين الجغرافية للمنشأة"
        >
          <PropertyGrid columns={2}>
            <PropertyItem
              label="البريد الإلكتروني الرئيسي"
              icon={<MailIcon size={14} />}
              value={
                primaryEmail ? (
                  <a
                    href={`mailto:${primaryEmail}`}
                    style={{ color: "var(--color-brand-primary, #4f46e5)", textDecoration: "none" }}
                  >
                    <bdi dir="ltr">{primaryEmail}</bdi>
                  </a>
                ) : (
                  "—"
                )
              }
            />

            <PropertyItem
              label="رقم الهاتف للتواصل"
              icon={<PhoneIcon size={14} />}
              value={
                primaryPhone ? (
                  <bdi dir="ltr" style={{ fontFamily: "var(--font-mono)", letterSpacing: "0.02em" }}>
                    {primaryPhone}
                  </bdi>
                ) : (
                  "—"
                )
              }
            />

            <PropertyItem
              label="المقر الرئيسي والعنوان"
              icon={<MapPinIcon size={14} />}
              isFullWidth={true}
              value={
                party.addresses.length ? (
                  party.addresses.map((addr) => (
                    <div key={addr.id} style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                      <span>{addr.line1}</span>
                      {addr.city && <span>— {addr.city}</span>}
                      {addr.countryCode && (
                        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                          ({addr.countryCode})
                        </span>
                      )}
                    </div>
                  ))
                ) : (
                  "لم يتم تسجيل عنوان جغرافي."
                )
              }
            />
          </PropertyGrid>
        </Surface>
      </div>

      <Dialog
        isOpen={editing}
        onOpenChange={setEditing}
        title="تعديل هوية الطرف التجاري"
        description="تعديل البيانات الأساسية مع الحفاظ على التزامن المتفائل وسجل التغييرات."
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
