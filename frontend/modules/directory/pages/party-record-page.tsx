import React, { useState } from "react";
import type { Party, PartyRoleCode } from "../types.ts";
import {
  usePartyRecord,
  useUpdateParty,
  useArchiveParty,
  useRestoreParty,
  useAddPartyRole,
  useDisablePartyRole,
  usePartyReadiness,
  usePartyDuplicates
} from "../hooks/party-queries.ts";
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
import { useToast } from "../../../components/primitives/toast.tsx";

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
  const [archiveConfirmOpen, setArchiveConfirmOpen] = useState(false);
  const [restoreConfirmOpen, setRestoreConfirmOpen] = useState(false);

  const { showToast } = useToast();

  const { data: party, isLoading, error, refetch } = usePartyRecord(partyId);
  const { data: readiness } = usePartyReadiness(partyId);
  const { data: duplicates } = usePartyDuplicates(partyId);

  const update = useUpdateParty();
  const archive = useArchiveParty();
  const restore = useRestoreParty();
  const addRole = useAddPartyRole();
  const disableRole = useDisablePartyRole();

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
          تعذر العثور على سجل الطرف المطلوب أو حدث خطأ في الاتصال.
        </p>
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "center", marginTop: "1rem" }}>
          {onBack && (
            <Button variant="secondary" onPress={onBack}>
              العودة لدليل الأطراف
            </Button>
          )}
          <Button variant="primary" onPress={() => void refetch()}>
            إعادة المحاولة
          </Button>
        </div>
      </div>
    );
  }

  const isOrganization = party.partyType === "Organization";
  const isArchived = party.status === "Archived";
  const primaryEmail = party.contactPoints.find(cp => cp.type === "Email")?.value;
  const primaryPhone = party.contactPoints.find(cp => cp.type === "Phone")?.value;

  const activeCustomer = party.roles.some(r => r.code === "Customer" && r.status === "Active");
  const activeSupplier = party.roles.some(r => r.code === "Supplier" && r.status === "Active");

  const handleToggleRole = async (roleCode: PartyRoleCode, currentlyActive: boolean) => {
    try {
      if (currentlyActive) {
        // Must keep at least one role
        const activeCount = (activeCustomer ? 1 : 0) + (activeSupplier ? 1 : 0);
        if (activeCount <= 1) {
          showToast({
            type: "warning",
            title: "تنبيه",
            description: "يجب الإبقاء على دور تجاري واحد نشط على الأقل (عميل أو مورد)."
          });
          return;
        }
        await disableRole.mutateAsync({ id: party.id, role: roleCode });
        showToast({
          type: "success",
          title: "تم تحديث الدور التجاري",
          description: `تم إيقاف دور ${roleCode === "Customer" ? "العميل" : "المورد"}.`
        });
      } else {
        await addRole.mutateAsync({ id: party.id, role: roleCode, version: party.version });
        showToast({
          type: "success",
          title: "تم تفعيل الدور التجاري",
          description: `أصبح الطرف مسجلاً كـ ${roleCode === "Customer" ? "عميل" : "مورد"}.`
        });
      }
    } catch (err: any) {
      showToast({
        type: "error",
        title: "تعذر تغيير الدور",
        description: err?.message || "حدث خطأ أثناء تعديل أدوار الطرف التجاري."
      });
    }
  };

  const handleArchive = async () => {
    try {
      await archive.mutateAsync({ id: party.id, version: party.version });
      setArchiveConfirmOpen(false);
      showToast({
        type: "success",
        title: "تم أرشفة السجل",
        description: `تم تحويل سجل ${party.displayName} إلى الأرشيف.`
      });
    } catch (err: any) {
      showToast({
        type: "error",
        title: "تعذر أرشفة السجل",
        description: err?.message || "حدث خطأ أثناء أرشفة الطرف التجاري."
      });
    }
  };

  const handleRestore = async () => {
    try {
      await restore.mutateAsync({ id: party.id, version: party.version });
      setRestoreConfirmOpen(false);
      showToast({
        type: "success",
        title: "تمت استعادة السجل",
        description: `أصبح سجل ${party.displayName} نشطاً في الدليل.`
      });
    } catch (err: any) {
      showToast({
        type: "error",
        title: "تعذر استعادة السجل",
        description: err?.message || "حدث خطأ أثناء استعادة الطرف التجاري."
      });
    }
  };

  const roleLabels = party.roles
    .filter(r => r.status === "Active")
    .map(r => (r.code === "Customer" ? "عميل" : "مورد"));

  const kpis = (
    <>
      <StatCard
        label="طبيعة الكيان"
        value={isOrganization ? "منشأة تجارية" : "فرد مستقل"}
        description={isOrganization ? "شركة / مؤسسة مسجلة" : "شخص ذو صفة طبيعية"}
        icon={isOrganization ? <BuildingIcon size={22} /> : <UserIcon size={22} />}
      />

      <StatCard
        label="الأدوار التجارية النشطة"
        value={roleLabels.join(" • ") || "طرف عام"}
        description={roleLabels.length > 1 ? "علاقة مزدوجة (عميل ومورد معاً)" : "علاقة تجارية مفردة"}
        icon={<DirectoryIcon size={22} />}
      />

      <StatCard
        label="جاهزية العمليات"
        value={readiness?.ready ? "جاهز للفوترة" : "بحاجة لاستكمال"}
        description={
          readiness?.ready
            ? "البيانات الرسمية مكتملة"
            : readiness?.missing.length
            ? `ينقص: ${readiness.missing.join("، ")}`
            : "سجل قيد المراجعة"
        }
        icon={<ShieldCheckIcon size={22} />}
      />
    </>
  );

  return (
    <RecordPage
      title={party.displayName}
      recordCode={isOrganization ? "منشأة مسجلة" : "فرد مسجل"}
      status={isArchived ? "neutral" : "success"}
      breadcrumbs={[
        { label: "دليل العلاقات التجارية", href: "/directory", onClick: onBack },
        { label: party.displayName }
      ]}
      headerActions={
        <div style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
          {onBack && (
            <Button variant="secondary" onPress={onBack}>
              العودة للقائمة
            </Button>
          )}

          {!isArchived ? (
            <>
              <Button variant="primary" onPress={() => setEditing(true)}>
                تعديل البيانات
              </Button>
              <Button variant="danger" onPress={() => setArchiveConfirmOpen(true)}>
                أرشفة الطرف
              </Button>
            </>
          ) : (
            <Button variant="primary" onPress={() => setRestoreConfirmOpen(true)}>
              استعادة من الأرشيف
            </Button>
          )}
        </div>
      }
      kpiCards={kpis}
      auditInfo={{
        createdAt: party.createdAt
          ? new Date(party.createdAt).toLocaleDateString("ar-EG", { year: "numeric", month: "long", day: "numeric" })
          : undefined,
        createdBy: "النظام التجاري الموحد"
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        {/* Banner if archived */}
        {isArchived && (
          <div
            style={{
              padding: "0.875rem 1.25rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-surface-sunken, #f1f5f9)",
              border: "1px solid var(--color-border-default)",
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between"
            }}
          >
            <div>
              <span style={{ fontWeight: 700, fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
                هذا السجل مؤرشف حالياً
              </span>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                لا يمكن إجراء معاملات مالية أو فواتير جديدة على السجلات المؤرشفة دون استعادتها.
              </p>
            </div>
            <Button variant="secondary" size="compact" onPress={() => setRestoreConfirmOpen(true)}>
              استعادة السجل للعمل
            </Button>
          </div>
        )}

        {/* Surface 1: الهوية والحالة */}
        <Surface
          title="الهوية والحالة التجارية"
          description="المعلومات الرسمية والأسماء المعتمدة ولغة المراسلات"
        >
          <PropertyGrid columns={3}>
            <PropertyItem
              label="النوع القانوني"
              value={isOrganization ? "منشأة / شركة تجارية" : "شخص / فرد مستقل"}
              icon={isOrganization ? <BuildingIcon size={14} /> : <UserIcon size={14} />}
            />

            <PropertyItem
              label="الاسم القانوني المسجل"
              value={party.legalName || party.displayName}
            />

            <PropertyItem
              label="الاسم التجاري / الشهرة"
              value={party.tradeName || party.displayName}
            />

            {party.nameAr && (
              <PropertyItem
                label="الاسم بالعربية"
                value={party.nameAr}
              />
            )}

            {party.nameEn && (
              <PropertyItem
                label="الاسم بالإنجليزية"
                value={<bdi dir="ltr">{party.nameEn}</bdi>}
              />
            )}

            <PropertyItem
              label="الدولة الأساسية"
              value={party.countryCode === "EG" ? "جمهورية مصر العربية (EG)" : party.countryCode || "—"}
            />

            <PropertyItem
              label="لغة المراسلات"
              value={party.primaryLanguage === "ar" ? "العربية" : "الإنجليزية"}
            />

            <PropertyItem
              label="حالة السجل في الدليل"
              value={
                <StatusBadge
                  status={isArchived ? "neutral" : "success"}
                  label={isArchived ? "مؤرشف" : "نشط ومعتمد"}
                  size="small"
                />
              }
            />
          </PropertyGrid>
        </Surface>

        {/* Surface 2: إدارة الأدوار التجارية السريعة */}
        <Surface
          title="الأدوار والعلاقات التجارية"
          description="التحكم في أدوار الطرف في النظام كعميل للمبيعات أو مورد للمشتريات"
        >
          <div style={{ display: "flex", gap: "1.5rem", flexWrap: "wrap", alignItems: "center" }}>
            <div
              style={{
                flex: "1 1 260px",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                padding: "0.875rem 1rem",
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: activeCustomer ? "var(--color-brand-subtle, #f5f3ff)" : "var(--color-surface-sunken)"
              }}
            >
              <div>
                <span style={{ fontWeight: 700, fontSize: "0.875rem" }}>دور العميل (Customer)</span>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  إصدار عروض أسعار، فواتير مبيعات وسندات قبض
                </p>
              </div>
              <Button
                size="compact"
                variant={activeCustomer ? "secondary" : "primary"}
                isDisabled={isArchived || addRole.isPending || disableRole.isPending}
                onPress={() => handleToggleRole("Customer", activeCustomer)}
              >
                {activeCustomer ? "إيقاف الدور" : "تفعيل كعميل"}
              </Button>
            </div>

            <div
              style={{
                flex: "1 1 260px",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                padding: "0.875rem 1rem",
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: activeSupplier ? "var(--color-status-info-subtle, #f0f9ff)" : "var(--color-surface-sunken)"
              }}
            >
              <div>
                <span style={{ fontWeight: 700, fontSize: "0.875rem" }}>دور المورد (Supplier)</span>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  أوامر شراء، فواتير مشتريات وسندات صرف
                </p>
              </div>
              <Button
                size="compact"
                variant={activeSupplier ? "secondary" : "primary"}
                isDisabled={isArchived || addRole.isPending || disableRole.isPending}
                onPress={() => handleToggleRole("Supplier", activeSupplier)}
              >
                {activeSupplier ? "إيقاف الدور" : "تفعيل كمورد"}
              </Button>
            </div>
          </div>
        </Surface>

        {/* Surface 3: المعرفات والسجلات الرسمية */}
        <Surface
          title="المعرفات والسجلات الرسمية"
          description="الأرقام الضريبية والسجلات التجارية والمراجع الداخلية الموثقة"
        >
          {party.identifiers.length ? (
            <PropertyGrid columns={2}>
              {party.identifiers.map((ident) => {
                const typeLabel =
                  ident.type === "TaxId" || ident.type === "Tax"
                    ? "الرقم الضريبي (VAT / Tax ID)"
                    : ident.type === "CommercialRegister"
                    ? "السجل التجاري (CR)"
                    : ident.type === "National" || ident.type === "NationalId"
                    ? "الرقم القومي / الهوية"
                    : ident.type === "InternalReference"
                    ? "الرقم المرجعي الداخلي"
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
                          backgroundColor:
                            ident.verificationStatus === "Verified"
                              ? "var(--color-status-success-subtle, #ecfdf5)"
                              : "var(--color-surface-sunken)",
                          color:
                            ident.verificationStatus === "Verified"
                              ? "var(--color-status-success-content, #065f46)"
                              : "var(--color-content-muted)"
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
              لا توجد معرفات أو سجلات مسجلة لهذا الطرف. يمكنك إضافة معرف رسمي عبر تعديل السجل.
            </div>
          )}
        </Surface>

        {/* Surface 4: قنوات الاتصال والعناوين */}
        <Surface
          title="معلومات الاتصال والمقرات"
          description="قنوات التواصل المعتمدة والعناوين الجغرافية للمنشأة"
        >
          <PropertyGrid columns={2}>
            <PropertyItem
              label="البريد الإلكتروني المعتمد"
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

        {/* Surface 5: فحص التكرارات المشتبه بها إن وجدت */}
        {duplicates && duplicates.length > 0 && (
          <Surface
            title="تنبيه: تشابه في السجلات التجارية"
            description="عثر النظام على سجلات قد تكون متطابقة أو مكررة في الدليل"
          >
            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
              {duplicates.map(d => (
                <div
                  key={d.id}
                  style={{
                    padding: "0.75rem 1rem",
                    borderRadius: "var(--radius-sm)",
                    backgroundColor: "var(--color-status-warning-subtle, #fffbeb)",
                    border: "1px solid var(--color-status-warning, #f59e0b)",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center"
                  }}
                >
                  <div>
                    <span style={{ fontWeight: 600, fontSize: "0.8125rem", color: "var(--color-status-warning-content, #92400e)" }}>
                      السبب: {d.reason}
                    </span>
                    <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginInlineStart: "0.75rem" }}>
                      درجة التطابق: {Math.round(d.confidence * 100)}%
                    </span>
                  </div>
                  <span style={{ fontSize: "0.75rem", fontWeight: 600 }}>الحالة: {d.status}</span>
                </div>
              ))}
            </div>
          </Surface>
        )}
      </div>

      {/* Edit Dialog */}
      <Dialog
        isOpen={editing}
        onOpenChange={setEditing}
        title="تعديل بيانات الطرف التجاري"
        description="تعديل بيانات الهوية وأرقام التواصل الرسمية مع التزامن الفوري."
        size="large"
      >
        <PartyForm
          initialData={toForm(party)}
          onSubmit={async (formData: PartyFormData) => {
            try {
              await update.mutateAsync({
                id: party.id,
                input: {
                  displayName: formData.displayName,
                  legalName: formData.legalName || undefined,
                  tradeName: formData.tradeName || undefined,
                  nameAr: formData.nameAr || undefined,
                  nameEn: formData.nameEn || undefined,
                  firstName: formData.firstName || undefined,
                  middleName: formData.middleName || undefined,
                  lastName: formData.lastName || undefined,
                  countryCode: formData.countryCode || undefined,
                  primaryLanguage: formData.primaryLanguage || undefined,
                  version: party.version
                }
              });
              setEditing(false);
              showToast({
                type: "success",
                title: "تم تحديث البيانات بنجاح",
                description: `تم حفظ تعديلات ${formData.displayName}.`
              });
            } catch (err: any) {
              showToast({
                type: "error",
                title: "تعذر حفظ التعديلات",
                description: err?.message || "حدث خطأ أثناء تعديل البيانات."
              });
            }
          }}
          onCancel={() => setEditing(false)}
          isSubmitting={update.isPending}
        />
      </Dialog>

      {/* Archive Confirmation Dialog */}
      <Dialog
        isOpen={archiveConfirmOpen}
        onOpenChange={setArchiveConfirmOpen}
        title="تأكيد أرشفة الطرف التجاري"
        description={`هل أنت متأكد من رغبتك في أرشفة "${party.displayName}"؟ سيتم إيقاف التعاملات الجديدة مع الحفاظ على السجل التاريخي والفواتير السابقة دون حذفها.`}
        size="medium"
      >
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end", marginTop: "1.5rem" }}>
          <Button variant="secondary" onPress={() => setArchiveConfirmOpen(false)}>
            إلغاء
          </Button>
          <Button variant="danger" onPress={handleArchive} isDisabled={archive.isPending}>
            {archive.isPending ? "جارٍ الأرشفة..." : "تأكيد الأرشفة"}
          </Button>
        </div>
      </Dialog>

      {/* Restore Confirmation Dialog */}
      <Dialog
        isOpen={restoreConfirmOpen}
        onOpenChange={setRestoreConfirmOpen}
        title="استعادة الطرف التجاري من الأرشيف"
        description={`هل ترغب في إعادة تفعيل "${party.displayName}"؟ سيتاح استخدامه في المعاملات والفواتير فوراً.`}
        size="medium"
      >
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end", marginTop: "1.5rem" }}>
          <Button variant="secondary" onPress={() => setRestoreConfirmOpen(false)}>
            إلغاء
          </Button>
          <Button variant="primary" onPress={handleRestore} isDisabled={restore.isPending}>
            {restore.isPending ? "جارٍ الاستعادة..." : "تأكيد الاستعادة"}
          </Button>
        </div>
      </Dialog>
    </RecordPage>
  );
}
