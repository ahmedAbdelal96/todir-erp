import React, { useState } from "react";
import type { Party, PartyRoleCode, AddressInput, ContactPointInput, IdentifierInput, AddContactPersonRequest, AliasInput, VisibilityInput, MergePartyRequest } from "../types.ts";
import {
  usePartyRecord,
  useUpdateParty,
  useArchiveParty,
  useRestoreParty,
  useAddPartyRole,
  useDisablePartyRole,
  usePartyReadiness,
  usePartyDuplicates,
  usePartyAudit,
  useAddPartyAddress,
  useAddPartyContactPoint,
  useAddPartyIdentifier,
  useAddPartyContactPerson,
  useAddPartyAlias,
  useSetPartyVisibility,
  useMergeParty
} from "../hooks/party-queries.ts";
import { RecordPage } from "../../../components/pages/record-page.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { PropertyGrid, PropertyItem, StatCard } from "../../../components/pages/property-grid.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
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
import { usePermissions } from "../../../core/runtime/runtime-provider.tsx";

export interface PartyRecordPageProps {
  readonly partyId?: string;
  readonly onBack?: (() => void) | undefined;
}

type Tab = "overview" | "contacts" | "addresses" | "identifiers" | "audit";

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

function addressUsageLabel(usage: string): string {
  if (usage === "Billing") return "عنوان الفواتير والمطالبات";
  if (usage === "Shipping") return "عنوان الشحن والتوصيل";
  if (usage === "Registered") return "العنوان القانوني المسجل";
  return "العنوان العام";
}

function identifierTypeLabel(type: string): string {
  if (type === "TaxId" || type === "Tax") return "الرقم الضريبي (VAT / Tax ID)";
  if (type === "CommercialRegister") return "السجل التجاري (CR)";
  if (type === "National" || type === "NationalId") return "الرقم القومي / الهوية";
  if (type === "InternalReference") return "الرقم المرجعي الداخلي";
  return type;
}

function capabilityLabel(cap: string): string {
  if (cap === "CanSell") return "البيع والفوترة";
  if (cap === "CanPurchase") return "الشراء والتوريد";
  if (cap === "CanInvoice") return "إصدار الفواتير";
  if (cap === "CanPay") return "المدفوعات";
  return cap;
}

function actionLabel(action: string): string {
  const map: Record<string, string> = {
    Created: "إنشاء السجل",
    Updated: "تحديث البيانات",
    Archived: "أرشفة السجل",
    Restored: "استعادة السجل",
    RoleAdded: "إضافة دور تجاري",
    RoleDisabled: "إيقاف دور تجاري",
    IdentifierAdded: "إضافة معرف رسمي",
    ContactPointAdded: "إضافة وسيلة تواصل",
    AddressAdded: "إضافة عنوان",
    ContactPersonAdded: "إضافة جهة اتصال",
    AliasAdded: "إضافة اسم بديل",
    VisibilitySet: "تعديل نطاق الرؤية",
    Merged: "دمج مع سجل آخر"
  };
  return map[action] ?? action;
}

export function PartyRecordPage({ partyId = "", onBack }: PartyRecordPageProps): React.JSX.Element {
  const [activeTab, setActiveTab] = useState<Tab>("overview");
  const [editing, setEditing] = useState(false);
  const [archiveConfirmOpen, setArchiveConfirmOpen] = useState(false);
  const [restoreConfirmOpen, setRestoreConfirmOpen] = useState(false);
  const [mergeOpen, setMergeOpen] = useState(false);
  const [addAddressOpen, setAddAddressOpen] = useState(false);
  const [addContactOpen, setAddContactOpen] = useState(false);
  const [addIdentifierOpen, setAddIdentifierOpen] = useState(false);
  const [addContactPersonOpen, setAddContactPersonOpen] = useState(false);
  const [addAliasOpen, setAddAliasOpen] = useState(false);
  const [visibilityOpen, setVisibilityOpen] = useState(false);

  // Merge form state
  const [masterPartyId, setMasterPartyId] = useState("");
  const [mergeReason, setMergeReason] = useState("");
  const [mergeConfirmed, setMergeConfirmed] = useState(false);

  // Visibility form state
  const [visibilityScopeType, setVisibilityScopeType] = useState<string>("TenantWide");

  // Add address form state
  const [addrUsage, setAddrUsage] = useState<string>("General");
  const [addrLine1, setAddrLine1] = useState("");
  const [addrCity, setAddrCity] = useState("");
  const [addrCountry, setAddrCountry] = useState("EG");

  // Add contact point form state
  const [cpType, setCpType] = useState<string>("Email");
  const [cpValue, setCpValue] = useState("");
  const [cpLabel, setCpLabel] = useState("");

  // Add identifier form state
  const [identType, setIdentType] = useState("");
  const [identValue, setIdentValue] = useState("");

  // Add contact person form state
  const [cpDisplayName, setCpDisplayName] = useState("");
  const [cpJobTitle, setCpJobTitle] = useState("");

  // Add alias form state
  const [aliasValue, setAliasValue] = useState("");
  const [aliasLang, setAliasLang] = useState("ar");

  const { showToast } = useToast();

  const { data: party, isLoading, error, refetch } = usePartyRecord(partyId);
  const { data: readiness } = usePartyReadiness(partyId);
  const { data: duplicates } = usePartyDuplicates(partyId);
  const { data: auditEntries } = usePartyAudit(partyId);

  const update = useUpdateParty();
  const archive = useArchiveParty();
  const restore = useRestoreParty();
  const addRole = useAddPartyRole();
  const disableRole = useDisablePartyRole();
  const addAddress = useAddPartyAddress();
  const addContactPoint = useAddPartyContactPoint();
  const addIdentifier = useAddPartyIdentifier();
  const addContactPerson = useAddPartyContactPerson();
  const addAlias = useAddPartyAlias();
  const setVisibility = useSetPartyVisibility();
  const mergeParty = useMergeParty();

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
  const isMerged = party.status === "Merged";
  const primaryEmail = party.contactPoints.find(cp => cp.type === "Email")?.value;
  const primaryPhone = party.contactPoints.find(cp => cp.type === "Phone")?.value;

  const activeCustomer = party.roles.some(r => r.code === "Customer" && r.status === "Active");
  const activeSupplier = party.roles.some(r => r.code === "Supplier" && r.status === "Active");

  let perms;
  try {
    perms = usePermissions();
  } catch {
    perms = null;
  }
  const canEdit = perms?.hasPermission("directory.party.edit") ?? true;
  const canArchive = perms?.hasPermission("directory.party.archive") ?? true;
  const canMerge = perms?.hasPermission("directory.party.merge") ?? true;
  const canManageVisibility = perms?.hasPermission("directory.visibility.manage") ?? true;
  const canManageIdentifiers = perms?.hasPermission("directory.party.identifier.manage") ?? true;
  const canManageContacts = perms?.hasPermission("directory.contact.manage") ?? true;

  const handleToggleRole = async (roleCode: PartyRoleCode, currentlyActive: boolean) => {
    if (!canEdit) {
      showToast({ type: "warning", title: "صلاحية غير كافية", description: "لا تملك صلاحية تعديل بيانات وأدوار الطرف التجاري." });
      return;
    }
    try {
      if (currentlyActive) {
        await disableRole.mutateAsync({ id: party.id, role: roleCode });
        showToast({ type: "success", title: "تم تحديث الدور التجاري", description: `تم إيقاف دور ${roleCode === "Customer" ? "العميل" : "المورد"}.` });
      } else {
        await addRole.mutateAsync({ id: party.id, role: roleCode, version: party.version });
        showToast({ type: "success", title: "تم تفعيل الدور التجاري", description: `أصبح الطرف مسجلاً كـ ${roleCode === "Customer" ? "عميل" : "مورد"}.` });
      }
    } catch (err: unknown) {
      showToast({ type: "error", title: "تعذر تغيير الدور", description: (err as Error)?.message || "حدث خطأ." });
    }
  };

  const handleArchive = async () => {
    try {
      await archive.mutateAsync({ id: party.id, version: party.version });
      setArchiveConfirmOpen(false);
      showToast({ type: "success", title: "تم أرشفة السجل", description: `تم تحويل سجل ${party.displayName} إلى الأرشيف.` });
    } catch (err: unknown) {
      showToast({ type: "error", title: "تعذر أرشفة السجل", description: (err as Error)?.message || "حدث خطأ." });
    }
  };

  const handleRestore = async () => {
    try {
      await restore.mutateAsync({ id: party.id, version: party.version });
      setRestoreConfirmOpen(false);
      showToast({ type: "success", title: "تمت استعادة السجل", description: `أصبح سجل ${party.displayName} نشطاً في الدليل.` });
    } catch (err: unknown) {
      showToast({ type: "error", title: "تعذر استعادة السجل", description: (err as Error)?.message || "حدث خطأ." });
    }
  };

  const handleMerge = async () => {
    if (!masterPartyId.trim()) { showToast({ type: "warning", title: "بيانات ناقصة", description: "يجب إدخال معرّف السجل الرئيسي للدمج." }); return; }
    if (!mergeReason.trim()) { showToast({ type: "warning", title: "بيانات ناقصة", description: "يجب إدخال سبب الدمج للتوثيق." }); return; }
    if (!mergeConfirmed) { showToast({ type: "warning", title: "تأكيد مطلوب", description: "يجب تأكيد أن عملية الدمج لا يمكن التراجع عنها." }); return; }
    const req: MergePartyRequest = { masterPartyId, reason: mergeReason, confirmIrreversible: true };
    try {
      await mergeParty.mutateAsync({ sourcePartyId: party.id, input: req });
      setMergeOpen(false);
      showToast({ type: "success", title: "تم الدمج بنجاح", description: "تم دمج هذا السجل مع السجل الرئيسي. سيتم إعادة توجيه كل الإشارات تلقائياً." });
      onBack?.();
    } catch (err: unknown) {
      showToast({ type: "error", title: "تعذر الدمج", description: (err as Error)?.message || "حدث خطأ أثناء عملية الدمج." });
    }
  };

  const handleAddAddress = async () => {
    if (!addrLine1.trim()) { showToast({ type: "warning", title: "بيانات ناقصة", description: "يرجى إدخال العنوان التفصيلي." }); return; }
    const input: AddressInput = { usage: addrUsage as AddressInput["usage"], countryCode: addrCountry, city: addrCity || undefined, line1: addrLine1, isDefault: party.addresses.length === 0 };
    try {
      await addAddress.mutateAsync({ id: party.id, input });
      setAddAddressOpen(false);
      setAddrLine1(""); setAddrCity(""); setAddrUsage("General");
      showToast({ type: "success", title: "تمت إضافة العنوان", description: "تم حفظ العنوان الجديد في سجل الطرف التجاري." });
    } catch (err: unknown) {
      showToast({ type: "error", title: "تعذر إضافة العنوان", description: (err as Error)?.message || "حدث خطأ." });
    }
  };

  const handleAddContactPoint = async () => {
    if (!cpValue.trim()) { showToast({ type: "warning", title: "بيانات ناقصة", description: "يرجى إدخال قيمة وسيلة التواصل." }); return; }
    const input: ContactPointInput = { type: cpType as ContactPointInput["type"], value: cpValue, label: cpLabel || undefined, isPrimary: party.contactPoints.length === 0 };
    try {
      await addContactPoint.mutateAsync({ id: party.id, input });
      setAddContactOpen(false);
      setCpValue(""); setCpLabel(""); setCpType("Email");
      showToast({ type: "success", title: "تمت إضافة وسيلة التواصل", description: "تم حفظ بيانات التواصل الجديدة." });
    } catch (err: unknown) {
      showToast({ type: "error", title: "تعذر الإضافة", description: (err as Error)?.message || "حدث خطأ." });
    }
  };

  const handleAddIdentifier = async () => {
    if (!identType || !identValue.trim()) { showToast({ type: "warning", title: "بيانات ناقصة", description: "يرجى تحديد نوع المعرف وإدخال قيمته." }); return; }
    const input: IdentifierInput = { type: identType, value: identValue, isPrimary: party.identifiers.length === 0 };
    try {
      await addIdentifier.mutateAsync({ id: party.id, input });
      setAddIdentifierOpen(false);
      setIdentType(""); setIdentValue("");
      showToast({ type: "success", title: "تمت إضافة المعرف الرسمي", description: "تم تسجيل المعرف في سجل الطرف التجاري." });
    } catch (err: unknown) {
      showToast({ type: "error", title: "تعذر الإضافة", description: (err as Error)?.message || "حدث خطأ." });
    }
  };

  const handleAddContactPerson = async () => {
    if (!cpDisplayName.trim()) { showToast({ type: "warning", title: "بيانات ناقصة", description: "يرجى إدخال اسم جهة الاتصال." }); return; }
    const req: AddContactPersonRequest = { person: { displayName: cpDisplayName, jobTitle: cpJobTitle || undefined } };
    try {
      await addContactPerson.mutateAsync({ id: party.id, input: req });
      setAddContactPersonOpen(false);
      setCpDisplayName(""); setCpJobTitle("");
      showToast({ type: "success", title: "تمت إضافة جهة الاتصال", description: `تم تسجيل ${cpDisplayName} كجهة اتصال لهذا الطرف التجاري.` });
    } catch (err: unknown) {
      showToast({ type: "error", title: "تعذر الإضافة", description: (err as Error)?.message || "حدث خطأ." });
    }
  };

  const handleAddAlias = async () => {
    if (!aliasValue.trim()) { showToast({ type: "warning", title: "بيانات ناقصة", description: "يرجى إدخال الاسم البديل." }); return; }
    const input: AliasInput = { alias: aliasValue, languageCode: aliasLang, aliasType: "Alternate" };
    try {
      await addAlias.mutateAsync({ id: party.id, input });
      setAddAliasOpen(false);
      setAliasValue("");
      showToast({ type: "success", title: "تمت إضافة الاسم البديل", description: "تم تسجيل الاسم البديل / التجاري للطرف." });
    } catch (err: unknown) {
      showToast({ type: "error", title: "تعذر الإضافة", description: (err as Error)?.message || "حدث خطأ." });
    }
  };

  const handleSetVisibility = async () => {
    const input: VisibilityInput = { scopeType: visibilityScopeType as VisibilityInput["scopeType"] };
    try {
      await setVisibility.mutateAsync({ id: party.id, input });
      setVisibilityOpen(false);
      showToast({ type: "success", title: "تم تحديث نطاق الرؤية", description: "سيظهر هذا السجل وفق الإعداد الجديد." });
    } catch (err: unknown) {
      showToast({ type: "error", title: "تعذر تحديث نطاق الرؤية", description: (err as Error)?.message || "حدث خطأ." });
    }
  };

  const roleLabels = party.roles.filter(r => r.status === "Active").map(r => (r.code === "Customer" ? "عميل" : "مورد"));

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
        description={roleLabels.length > 1 ? "علاقة مزدوجة (عميل ومورد)" : "علاقة تجارية مفردة"}
        icon={<DirectoryIcon size={22} />}
      />

      <StatCard
        label="جاهزية العمليات"
        value={readiness?.ready ? "جاهز للعمليات" : "بحاجة لاستكمال"}
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

  const tabStyle = (tab: Tab): React.CSSProperties => ({
    padding: "0.5rem 1rem",
    border: "none",
    background: "none",
    cursor: "pointer",
    fontWeight: activeTab === tab ? 700 : 400,
    fontSize: "0.875rem",
    color: activeTab === tab ? "var(--color-brand-primary, #4f46e5)" : "var(--color-content-secondary)",
    borderBottom: activeTab === tab ? "2px solid var(--color-brand-primary, #4f46e5)" : "2px solid transparent",
    whiteSpace: "nowrap" as const,
  });

  return (
    <RecordPage
      title={party.displayName}
      recordCode={isOrganization ? "منشأة مسجلة" : "فرد مسجل"}
      status={isArchived ? "neutral" : isMerged ? "neutral" : "success"}
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
          {!isArchived && !isMerged ? (
            <>
              {canManageVisibility && (
                <Button variant="secondary" onPress={() => setVisibilityOpen(true)}>
                  نطاق الرؤية
                </Button>
              )}
              {canEdit && (
                <Button variant="primary" onPress={() => setEditing(true)}>
                  تعديل البيانات
                </Button>
              )}
              {canMerge && (
                <Button variant="danger" onPress={() => setMergeOpen(true)}>
                  دمج السجل
                </Button>
              )}
              {canArchive && (
                <Button variant="danger" onPress={() => setArchiveConfirmOpen(true)}>
                  أرشفة الطرف
                </Button>
              )}
            </>
          ) : isArchived && canArchive ? (
            <Button variant="primary" onPress={() => setRestoreConfirmOpen(true)}>
              استعادة من الأرشيف
            </Button>
          ) : null}
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
        {/* Status banners */}
        {isArchived && (
          <div style={{ padding: "0.875rem 1.25rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-sunken, #f1f5f9)", border: "1px solid var(--color-border-default)", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
            <div>
              <span style={{ fontWeight: 700, fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>هذا السجل مؤرشف حالياً</span>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>لا يمكن إجراء معاملات مالية أو فواتير جديدة على السجلات المؤرشفة دون استعادتها.</p>
            </div>
            <Button variant="secondary" size="compact" onPress={() => setRestoreConfirmOpen(true)}>
              استعادة السجل للعمل
            </Button>
          </div>
        )}
        {isMerged && (
          <div style={{ padding: "0.875rem 1.25rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-status-info-subtle, #f0f9ff)", border: "1px solid var(--color-status-info, #3b82f6)" }}>
            <span style={{ fontWeight: 700, fontSize: "0.875rem", color: "var(--color-status-info-content, #0369a1)" }}>تم دمج هذا السجل مع سجل آخر</span>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>جميع الإشارات إلى هذا السجل سيتم توجيهها تلقائياً إلى السجل الرئيسي.</p>
          </div>
        )}

        {/* Duplicate alerts — shown before tabs when present */}
        {duplicates && duplicates.length > 0 && (
          <Surface
            title="تنبيه: تشابه في السجلات التجارية"
            description="عثر النظام على سجلات قد تكون متطابقة أو مكررة في الدليل. يرجى مراجعتها واتخاذ الإجراء المناسب."
          >
            <div style={{ display: "flex", flexDirection: "column", gap: "0.625rem" }}>
              {duplicates.map(d => (
                <div
                  key={d.id}
                  style={{
                    padding: "0.875rem 1rem",
                    borderRadius: "var(--radius-sm)",
                    backgroundColor: "var(--color-status-warning-subtle, #fffbeb)",
                    border: "1px solid var(--color-status-warning, #f59e0b)",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "flex-start",
                    gap: "1rem",
                    flexWrap: "wrap"
                  }}
                >
                  <div style={{ flex: 1 }}>
                    <span style={{ fontWeight: 700, fontSize: "0.875rem", color: "var(--color-status-warning-content, #92400e)", display: "block" }}>
                      ✓ {d.reason}
                    </span>
                    <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                      {d.status === "Pending" ? "في انتظار المراجعة" : d.status === "Dismissed" ? "تم تجاهله" : d.status}
                      {d.confidence > 0.9 ? " · تطابق قوي جداً" : d.confidence > 0.7 ? " · تطابق محتمل" : " · تشابه ملحوظ"}
                    </span>
                  </div>
                  <div style={{ display: "flex", gap: "0.5rem", flexShrink: 0, alignItems: "center" }}>
                    <Button
                      size="compact"
                      variant="secondary"
                      onPress={() => {
                        if (onBack) {
                          onBack();
                        }
                      }}
                    >
                      عرض ومقارنة
                    </Button>
                    {canMerge && (
                      <Button
                        size="compact"
                        variant="secondary"
                        onPress={() => {
                          setMasterPartyId(d.candidatePartyId);
                          setMergeOpen(true);
                        }}
                      >
                        دمج السجلات
                      </Button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </Surface>
        )}

        {/* Workspace Tabs */}
        <div style={{ borderBottom: "1px solid var(--color-border-default)", display: "flex", gap: 0, overflowX: "auto" }}>
          {(["overview", "contacts", "addresses", "identifiers", "audit"] as Tab[]).map(tab => {
            const labels: Record<Tab, string> = { overview: "النظرة العامة", contacts: "جهات الاتصال", addresses: "العناوين", identifiers: "المعرفات والسجلات", audit: "سجل الأنشطة" };
            return (
              <button key={tab} type="button" style={tabStyle(tab)} onClick={() => setActiveTab(tab)}>
                {labels[tab]}
              </button>
            );
          })}
        </div>

        {/* TAB: Overview */}
        {activeTab === "overview" && (
          <>
            {/* Identity */}
            <Surface title="الهوية والحالة التجارية" description="المعلومات الرسمية والأسماء المعتمدة ولغة المراسلات">
              <PropertyGrid columns={3}>
                <PropertyItem
                  label="النوع القانوني"
                  value={isOrganization ? "منشأة / شركة تجارية" : "شخص / فرد مستقل"}
                  icon={isOrganization ? <BuildingIcon size={14} /> : <UserIcon size={14} />}
                />
                <PropertyItem label="الاسم القانوني المسجل" value={party.legalName || party.displayName} />
                {isOrganization && <PropertyItem label="الاسم التجاري / الشهرة" value={party.tradeName || party.displayName} />}
                {party.nameAr && <PropertyItem label="الاسم بالعربية" value={party.nameAr} />}
                {party.nameEn && <PropertyItem label="الاسم بالإنجليزية" value={<bdi dir="ltr">{party.nameEn}</bdi>} />}
                {!isOrganization && party.firstName && (
                  <PropertyItem label="الاسم الأول" value={`${party.firstName ?? ""} ${party.middleName ?? ""} ${party.lastName ?? ""}`.trim()} />
                )}
                <PropertyItem
                  label="الدولة الأساسية"
                  value={party.countryCode === "EG" ? "جمهورية مصر العربية" : party.countryCode === "SA" ? "المملكة العربية السعودية" : party.countryCode || "—"}
                />
                <PropertyItem
                  label="لغة المراسلات"
                  value={party.primaryLanguage === "ar" ? "العربية" : "الإنجليزية"}
                />
                <PropertyItem
                  label="حالة السجل"
                  value={
                    <StatusBadge
                      status={isArchived ? "neutral" : isMerged ? "neutral" : "success"}
                      label={isArchived ? "مؤرشف" : isMerged ? "مدموج" : "نشط"}
                      size="small"
                    />
                  }
                />
              </PropertyGrid>
            </Surface>

            {/* Primary Identifiers & Contacts */}
            {party.identifiers.length > 0 && (
              <Surface
                title="المعرفات الرسمية والتواصل المباشر"
                description="الرقم الضريبي والسجل التجاري وقنوات التواصل المعتمدة"
              >
                <PropertyGrid columns={3}>
                  {party.identifiers.map(ident => (
                    <PropertyItem
                      key={ident.id}
                      label={identifierTypeLabel(ident.type)}
                      value={<bdi style={{ fontFamily: "var(--font-mono)", fontWeight: 700 }}>{ident.value}</bdi>}
                      icon={<TagIcon size={14} />}
                      badge={
                        <span
                          style={{
                            fontSize: "0.6875rem",
                            fontWeight: 700,
                            padding: "0.125rem 0.5rem",
                            borderRadius: "var(--radius-full)",
                            backgroundColor: ident.verificationStatus === "Verified" ? "var(--color-status-success-subtle, #ecfdf5)" : "var(--color-surface-sunken)",
                            color: ident.verificationStatus === "Verified" ? "var(--color-status-success-content, #065f46)" : "var(--color-content-muted)"
                          }}
                        >
                          {ident.verificationStatus === "Verified" ? "تم التحقق ✓" : "قيد المراجعة"}
                        </span>
                      }
                    />
                  ))}
                  {primaryEmail && (
                    <PropertyItem
                      label="البريد الإلكتروني المعتمد"
                      icon={<MailIcon size={14} />}
                      value={<bdi dir="ltr">{primaryEmail}</bdi>}
                    />
                  )}
                  {primaryPhone && (
                    <PropertyItem
                      label="الهاتف للتواصل"
                      icon={<PhoneIcon size={14} />}
                      value={<bdi dir="ltr" style={{ fontFamily: "var(--font-mono)" }}>{primaryPhone}</bdi>}
                    />
                  )}
                </PropertyGrid>
              </Surface>
            )}

            {/* Aliases */}
            {(party.aliases && party.aliases.length > 0 || true) && (
              <Surface
                title="الأسماء البديلة والتجارية"
                description="اسماء بديلة أو تجارية إضافية معتمدة لهذا الطرف"
                headerActions={
                  !isArchived && !isMerged && canEdit ? (
                    <Button size="compact" variant="secondary" onPress={() => setAddAliasOpen(true)}>
                      إضافة اسم بديل
                    </Button>
                  ) : undefined
                }
              >
                {party.aliases && party.aliases.length > 0 ? (
                  <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                    {party.aliases.map(alias => (
                      <div key={alias.id} style={{ display: "flex", alignItems: "center", gap: "1rem", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
                        <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>{alias.alias}</span>
                        {alias.languageCode && <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{alias.languageCode === "ar" ? "عربي" : "إنجليزي"}</span>}
                        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginInlineStart: "auto" }}>{alias.aliasType === "Alternate" ? "اسم بديل" : alias.aliasType}</span>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>لا توجد أسماء بديلة مسجلة.</p>
                )}
              </Surface>
            )}

            {/* Roles */}
            <Surface title="الأدوار والعلاقات التجارية" description="التحكم في أدوار الطرف في النظام كعميل للمبيعات أو مورد للمشتريات">
              <div style={{ display: "flex", gap: "1.5rem", flexWrap: "wrap", alignItems: "center" }}>
                {(["Customer", "Supplier"] as PartyRoleCode[]).map(roleCode => {
                  const isActive = roleCode === "Customer" ? activeCustomer : activeSupplier;
                  const roleNameAr = roleCode === "Customer" ? "عميل" : "مورد";
                  const roleDesc = roleCode === "Customer" ? "إصدار عروض أسعار، فواتير مبيعات وسندات قبض" : "أوامر شراء، فواتير مشتريات وسندات صرف";
                  return (
                    <div
                      key={roleCode}
                      style={{
                        flex: "1 1 260px",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        padding: "0.875rem 1rem",
                        borderRadius: "var(--radius-md)",
                        border: "1px solid var(--color-border-default)",
                        backgroundColor: isActive
                          ? roleCode === "Customer" ? "var(--color-brand-subtle, #f5f3ff)" : "var(--color-status-info-subtle, #f0f9ff)"
                          : "var(--color-surface-sunken)"
                      }}
                    >
                      <div>
                        <span style={{ fontWeight: 700, fontSize: "0.875rem" }}>دور {roleNameAr}</span>
                        <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{roleDesc}</p>
                      </div>
                      <Button
                        size="compact"
                        variant={isActive ? "secondary" : "primary"}
                        isDisabled={isArchived || isMerged || addRole.isPending || disableRole.isPending}
                        onPress={() => handleToggleRole(roleCode, isActive)}
                      >
                        {isActive ? "إيقاف الدور" : `تفعيل كـ${roleNameAr}`}
                      </Button>
                    </div>
                  );
                })}
              </div>
            </Surface>

            {/* Readiness */}
            {readiness && (
              <Surface title="جاهزية الطرف للعمليات التجارية" description="تقييم مدى اكتمال بيانات هذا الطرف لممارسة كل نوع من العمليات">
                <div style={{ display: "flex", flexDirection: "column", gap: "0.625rem" }}>
                  {readiness.capabilities && readiness.capabilities.length > 0 ? (
                    readiness.capabilities.map(cap => (
                      <div
                        key={cap.capability}
                        style={{
                          display: "flex",
                          alignItems: "flex-start",
                          gap: "0.75rem",
                          padding: "0.625rem 0.875rem",
                          borderRadius: "var(--radius-sm)",
                          border: "1px solid var(--color-border-subtle)",
                          backgroundColor: cap.ready ? "var(--color-status-success-subtle, #ecfdf5)" : "var(--color-surface-sunken)"
                        }}
                      >
                        <span style={{ fontSize: "1rem" }}>{cap.ready ? "✓" : "○"}</span>
                        <div style={{ flex: 1 }}>
                          <span style={{ fontWeight: 600, fontSize: "0.875rem", color: cap.ready ? "var(--color-status-success-content, #065f46)" : "var(--color-content-secondary)" }}>
                            {capabilityLabel(cap.capability)}
                          </span>
                          {!cap.ready && cap.missing.length > 0 && (
                            <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                              ينقص: {cap.missing.join("، ")}
                            </p>
                          )}
                        </div>
                        <StatusBadge
                          status={cap.ready ? "success" : "warning"}
                          label={cap.ready ? "مكتمل" : "ناقص"}
                          size="small"
                        />
                      </div>
                    ))
                  ) : (
                    <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", padding: "0.875rem" }}>
                      <StatusBadge status={readiness.ready ? "success" : "warning"} label={readiness.ready ? "جاهز للعمليات" : "بيانات ناقصة"} size="small" />
                      {!readiness.ready && readiness.missing.length > 0 && (
                        <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>ينقص: {readiness.missing.join("، ")}</span>
                      )}
                    </div>
                  )}
                </div>
              </Surface>
            )}

            {/* Visibility */}
            <Surface
              title="نطاق الرؤية في المنظومة"
              description="تحديد الجهات القانونية التي يمكنها رؤية واستخدام هذا السجل"
              headerActions={
                !isArchived && !isMerged && canManageVisibility ? (
                  <Button size="compact" variant="secondary" onPress={() => setVisibilityOpen(true)}>
                    تعديل النطاق
                  </Button>
                ) : undefined
              }
            >
              {party.visibility && party.visibility.length > 0 ? (
                <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                  {party.visibility.map(v => (
                    <div key={v.id} style={{ padding: "0.5rem 0.75rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)", fontSize: "0.875rem" }}>
                      {v.scopeType === "TenantWide" ? "مرئي لجميع الجهات القانونية في المؤسسة" :
                       v.scopeType === "SelectedLegalEntities" ? "مرئي لجهات قانونية محددة" :
                       v.scopeType === "SingleLegalEntity" ? "مرئي لجهة قانونية واحدة فقط" : v.scopeType}
                    </div>
                  ))}
                </div>
              ) : (
                <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>النطاق الافتراضي: مرئي لجميع الجهات القانونية في المؤسسة.</p>
              )}
            </Surface>
          </>
        )}

        {/* TAB: Contacts */}
        {activeTab === "contacts" && (
          <>
            <Surface
              title="قنوات الاتصال المعتمدة"
              description="أرقام الهواتف وعناوين البريد الإلكتروني والمواقع الإلكترونية المسجلة لهذا الطرف"
              headerActions={
                !isArchived && !isMerged && canManageContacts ? (
                  <Button size="compact" variant="primary" onPress={() => setAddContactOpen(true)}>
                    إضافة وسيلة تواصل
                  </Button>
                ) : undefined
              }
            >
              {party.contactPoints.length > 0 ? (
                <PropertyGrid columns={2}>
                  {party.contactPoints.map(cp => (
                    <PropertyItem
                      key={cp.id}
                      label={cp.label || (cp.type === "Email" ? "البريد الإلكتروني" : cp.type === "Phone" ? "الهاتف" : cp.type === "Website" ? "الموقع الإلكتروني" : "وسيلة تواصل")}
                      icon={cp.type === "Email" ? <MailIcon size={14} /> : cp.type === "Phone" ? <PhoneIcon size={14} /> : undefined}
                      value={
                        cp.type === "Email" ? (
                          <a href={`mailto:${cp.value}`} style={{ color: "var(--color-brand-primary, #4f46e5)", textDecoration: "none" }}>
                            <bdi dir="ltr">{cp.value}</bdi>
                          </a>
                        ) : cp.type === "Phone" ? (
                          <bdi dir="ltr" style={{ fontFamily: "var(--font-mono)", letterSpacing: "0.02em" }}>{cp.value}</bdi>
                        ) : <bdi dir="ltr">{cp.value}</bdi>
                      }
                      badge={cp.isPrimary ? <span style={{ fontSize: "0.6875rem", fontWeight: 700, padding: "0.125rem 0.375rem", borderRadius: "var(--radius-full)", backgroundColor: "var(--color-brand-subtle)", color: "var(--color-brand-primary)" }}>رئيسي</span> : undefined}
                    />
                  ))}
                </PropertyGrid>
              ) : (
                <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>لا توجد وسائل تواصل مسجلة.</p>
              )}
            </Surface>

            <Surface
              title="جهات الاتصال المباشرة"
              description="الأشخاص المسؤولون عن التواصل المباشر وإدارة العلاقة التجارية مع هذا الطرف"
              headerActions={
                !isArchived && !isMerged && canManageContacts ? (
                  <Button size="compact" variant="primary" onPress={() => setAddContactPersonOpen(true)}>
                    إضافة جهة اتصال
                  </Button>
                ) : undefined
              }
            >
              {party.contactPersons && party.contactPersons.length > 0 ? (
                <div style={{ display: "flex", flexDirection: "column", gap: "0.625rem" }}>
                  {party.contactPersons.map(cp => (
                    <div key={cp.id} style={{ display: "flex", alignItems: "center", gap: "1rem", padding: "0.75rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
                      <div style={{ width: 36, height: 36, borderRadius: "50%", backgroundColor: "var(--color-brand-subtle)", display: "flex", alignItems: "center", justifyContent: "center", fontWeight: 700, fontSize: "0.875rem", color: "var(--color-brand-primary)", flexShrink: 0 }}>
                        {cp.displayName.charAt(0)}
                      </div>
                      <div style={{ flex: 1 }}>
                        <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>{cp.displayName}</span>
                        {cp.jobTitle && <p style={{ margin: "0.125rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{cp.jobTitle}{cp.department ? ` — ${cp.department}` : ""}</p>}
                      </div>
                      <StatusBadge status={cp.status === "Active" ? "success" : "neutral"} label={cp.status === "Active" ? "نشط" : "غير نشط"} size="small" />
                    </div>
                  ))}
                </div>
              ) : (
                <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>لا توجد جهات اتصال شخصية مسجلة لهذا الطرف.</p>
              )}
            </Surface>
          </>
        )}

        {/* TAB: Addresses */}
        {activeTab === "addresses" && (
          <Surface
            title="العناوين الجغرافية المسجلة"
            description="عناوين الفوترة والشحن والمقر الرئيسي المسجلة لهذا الطرف التجاري"
            headerActions={
              !isArchived && !isMerged && canEdit ? (
                <Button size="compact" variant="primary" onPress={() => setAddAddressOpen(true)}>
                  إضافة عنوان
                </Button>
              ) : undefined
            }
          >
            {party.addresses.length > 0 ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                {party.addresses.map(addr => (
                  <div key={addr.id} style={{ padding: "0.875rem 1rem", borderRadius: "var(--radius-md)", border: "1px solid var(--color-border-default)", display: "flex", alignItems: "flex-start", gap: "0.875rem" }}>
                    <MapPinIcon size={18} />
                    <div style={{ flex: 1 }}>
                      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "0.25rem" }}>
                        <span style={{ fontWeight: 700, fontSize: "0.875rem" }}>{addressUsageLabel(addr.usage)}</span>
                        {addr.isDefault && <span style={{ fontSize: "0.6875rem", fontWeight: 700, padding: "0.125rem 0.375rem", borderRadius: "var(--radius-full)", backgroundColor: "var(--color-brand-subtle)", color: "var(--color-brand-primary)" }}>افتراضي</span>}
                      </div>
                      <p style={{ margin: 0, fontSize: "0.875rem" }}>{addr.line1}</p>
                      {addr.line2 && <p style={{ margin: "0.125rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{addr.line2}</p>}
                      <p style={{ margin: "0.125rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                        {[addr.city, addr.region, addr.postalCode, addr.countryCode].filter(Boolean).join("، ")}
                      </p>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>لا توجد عناوين جغرافية مسجلة.</p>
            )}
          </Surface>
        )}

        {/* TAB: Identifiers */}
        {activeTab === "identifiers" && (
          <Surface
            title="المعرفات والسجلات الرسمية"
            description="الأرقام الضريبية والسجلات التجارية والمراجع الداخلية الموثقة"
            headerActions={
              !isArchived && !isMerged && canManageIdentifiers ? (
                <Button size="compact" variant="primary" onPress={() => setAddIdentifierOpen(true)}>
                  إضافة معرف
                </Button>
              ) : undefined
            }
          >
            {party.identifiers.length > 0 ? (
              <PropertyGrid columns={2}>
                {party.identifiers.map(ident => (
                  <PropertyItem
                    key={ident.id}
                    label={identifierTypeLabel(ident.type)}
                    value={<bdi style={{ fontFamily: "var(--font-mono)", fontWeight: 700 }}>{ident.value}</bdi>}
                    icon={<TagIcon size={14} />}
                    badge={
                      <span
                        style={{
                          fontSize: "0.6875rem",
                          fontWeight: 700,
                          padding: "0.125rem 0.5rem",
                          borderRadius: "var(--radius-full)",
                          backgroundColor: ident.verificationStatus === "Verified" ? "var(--color-status-success-subtle, #ecfdf5)" : "var(--color-surface-sunken)",
                          color: ident.verificationStatus === "Verified" ? "var(--color-status-success-content, #065f46)" : "var(--color-content-muted)"
                        }}
                      >
                        {ident.verificationStatus === "Verified" ? "تم التحقق ✓" : "قيد المراجعة"}
                      </span>
                    }
                  />
                ))}
              </PropertyGrid>
            ) : (
              <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>لا توجد معرفات أو سجلات مسجلة لهذا الطرف.</p>
            )}
          </Surface>
        )}

        {/* TAB: Audit */}
        {activeTab === "audit" && (
          <Surface title="سجل الأنشطة والتغييرات" description="تاريخ كامل لجميع العمليات التي أُجريت على هذا السجل">
            {auditEntries && auditEntries.length > 0 ? (
              <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
                {auditEntries.map((entry, idx) => (
                  <div
                    key={entry.id}
                    style={{
                      display: "flex",
                      alignItems: "flex-start",
                      gap: "0.875rem",
                      padding: "0.875rem 0",
                      borderBottom: idx < auditEntries.length - 1 ? "1px solid var(--color-border-subtle)" : "none"
                    }}
                  >
                    <div style={{ width: 8, height: 8, borderRadius: "50%", backgroundColor: "var(--color-brand-primary, #4f46e5)", marginTop: "0.375rem", flexShrink: 0 }} />
                    <div style={{ flex: 1 }}>
                      <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>{actionLabel(entry.action)}</span>
                      {entry.reason && <p style={{ margin: "0.125rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>السبب: {entry.reason}</p>}
                    </div>
                    <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", whiteSpace: "nowrap", flexShrink: 0 }}>
                      {new Date(entry.occurredAt).toLocaleDateString("ar-EG", { year: "numeric", month: "short", day: "numeric" })}
                    </span>
                  </div>
                ))}
              </div>
            ) : (
              <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>لا توجد سجلات أنشطة متاحة.</p>
            )}
          </Surface>
        )}
      </div>

      {/* Edit Dialog */}
      <Dialog isOpen={editing} onOpenChange={setEditing} title="تعديل بيانات الطرف التجاري" description="تعديل بيانات الهوية والأسماء الرسمية مع التزامن الفوري." size="large">
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
              showToast({ type: "success", title: "تم تحديث البيانات بنجاح", description: `تم حفظ تعديلات ${formData.displayName}.` });
            } catch (err: unknown) {
              showToast({ type: "error", title: "تعذر حفظ التعديلات", description: (err as Error)?.message || "حدث خطأ." });
            }
          }}
          onCancel={() => setEditing(false)}
          isSubmitting={update.isPending}
        />
      </Dialog>

      {/* Archive Confirmation */}
      <Dialog isOpen={archiveConfirmOpen} onOpenChange={setArchiveConfirmOpen} title="تأكيد أرشفة الطرف التجاري" description={`هل أنت متأكد من رغبتك في أرشفة "${party.displayName}"؟ سيتم إيقاف التعاملات الجديدة مع الحفاظ على السجل التاريخي كاملاً.`} size="medium">
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end", marginTop: "1.5rem" }}>
          <Button variant="secondary" onPress={() => setArchiveConfirmOpen(false)}>إلغاء</Button>
          <Button variant="danger" onPress={handleArchive} isDisabled={archive.isPending}>
            {archive.isPending ? "جارٍ الأرشفة..." : "تأكيد الأرشفة"}
          </Button>
        </div>
      </Dialog>

      {/* Restore Confirmation */}
      <Dialog isOpen={restoreConfirmOpen} onOpenChange={setRestoreConfirmOpen} title="استعادة الطرف التجاري من الأرشيف" description={`هل ترغب في إعادة تفعيل "${party.displayName}"؟ سيتاح استخدامه في المعاملات والفواتير فوراً.`} size="medium">
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end", marginTop: "1.5rem" }}>
          <Button variant="secondary" onPress={() => setRestoreConfirmOpen(false)}>إلغاء</Button>
          <Button variant="primary" onPress={handleRestore} isDisabled={restore.isPending}>
            {restore.isPending ? "جارٍ الاستعادة..." : "تأكيد الاستعادة"}
          </Button>
        </div>
      </Dialog>

      {/* Merge Dialog — irreversible action */}
      <Dialog
        isOpen={mergeOpen}
        onOpenChange={(open) => { if (!open) { setMergeOpen(false); setMasterPartyId(""); setMergeReason(""); setMergeConfirmed(false); } }}
        title="دمج هذا السجل مع سجل آخر"
        description="عملية الدمج لا يمكن التراجع عنها. سيصبح هذا السجل مؤرشفاً ويتم توجيه جميع الإشارات إلى السجل الرئيسي تلقائياً."
        size="medium"
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <div style={{ padding: "0.75rem 1rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-status-warning-subtle, #fffbeb)", border: "1px solid var(--color-status-warning, #f59e0b)", fontSize: "0.875rem", color: "var(--color-status-warning-content, #92400e)", fontWeight: 600 }}>
            ⚠ تحذير: هذا الإجراء لا يمكن التراجع عنه بعد التأكيد.
          </div>
          <TextField
            label="معرّف السجل الرئيسي (الطرف الذي سيُدمج فيه)"
            placeholder="أدخل معرّف السجل الرئيسي..."
            value={masterPartyId}
            onChange={setMasterPartyId}
          />
          <TextField
            label="سبب الدمج (للتوثيق)"
            placeholder="مثال: سجل مكرر بنفس الرقم الضريبي"
            value={mergeReason}
            onChange={setMergeReason}
          />
          <Checkbox isSelected={mergeConfirmed} onChange={setMergeConfirmed}>
            <span style={{ fontWeight: 600 }}>أفهم وأؤكد أن عملية الدمج لا يمكن التراجع عنها وأن هذا السجل سيُغلق نهائياً.</span>
          </Checkbox>
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setMergeOpen(false)}>إلغاء</Button>
            <Button variant="danger" onPress={handleMerge} isDisabled={mergeParty.isPending || !mergeConfirmed}>
              {mergeParty.isPending ? "جارٍ الدمج..." : "تأكيد الدمج"}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Add Address Dialog */}
      <Dialog isOpen={addAddressOpen} onOpenChange={setAddAddressOpen} title="إضافة عنوان جغرافي جديد" size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <div>
            <Select label="استخدام العنوان" selectedKey={addrUsage} onSelectionChange={(k) => setAddrUsage(String(k ?? "General"))}>
              <SelectItem id="General" textValue="عنوان عام">عنوان عام</SelectItem>
              <SelectItem id="Billing" textValue="عنوان الفواتير والمطالبات">عنوان الفواتير والمطالبات</SelectItem>
              <SelectItem id="Shipping" textValue="عنوان الشحن والتوصيل">عنوان الشحن والتوصيل</SelectItem>
              <SelectItem id="Registered" textValue="العنوان القانوني المسجل">العنوان القانوني المسجل</SelectItem>
            </Select>
          </div>
          <TextField label="العنوان التفصيلي *" placeholder="الشارع، رقم المبنى، الحي" value={addrLine1} onChange={setAddrLine1} isRequired />
          <TextField label="المدينة" placeholder="مثال: القاهرة، الرياض" value={addrCity} onChange={setAddrCity} />
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setAddAddressOpen(false)}>إلغاء</Button>
            <Button variant="primary" onPress={handleAddAddress} isDisabled={addAddress.isPending}>
              {addAddress.isPending ? "جارٍ الحفظ..." : "حفظ العنوان"}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Add Contact Point Dialog */}
      <Dialog isOpen={addContactOpen} onOpenChange={setAddContactOpen} title="إضافة وسيلة تواصل جديدة" size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <div>
            <Select label="نوع وسيلة التواصل" selectedKey={cpType} onSelectionChange={(k) => setCpType(String(k ?? "Email"))}>
              <SelectItem id="Email" textValue="بريد إلكتروني">بريد إلكتروني</SelectItem>
              <SelectItem id="Phone" textValue="هاتف">هاتف</SelectItem>
              <SelectItem id="Website" textValue="موقع إلكتروني">موقع إلكتروني</SelectItem>
              <SelectItem id="Other" textValue="أخرى">أخرى</SelectItem>
            </Select>
          </div>
          <TextField label="القيمة *" placeholder={cpType === "Email" ? "example@company.com" : cpType === "Phone" ? "+20 100 000 0000" : "https://"} value={cpValue} onChange={setCpValue} isRequired />
          <TextField label="التسمية / الوصف (اختياري)" placeholder="مثال: البريد الرئيسي، هاتف المبيعات" value={cpLabel} onChange={setCpLabel} />
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setAddContactOpen(false)}>إلغاء</Button>
            <Button variant="primary" onPress={handleAddContactPoint} isDisabled={addContactPoint.isPending}>
              {addContactPoint.isPending ? "جارٍ الحفظ..." : "حفظ وسيلة التواصل"}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Add Identifier Dialog */}
      <Dialog isOpen={addIdentifierOpen} onOpenChange={setAddIdentifierOpen} title="إضافة معرف رسمي جديد" size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <div>
            <Select label="نوع المعرف الرسمي *" selectedKey={identType || "none"} onSelectionChange={(k) => setIdentType(k === "none" ? "" : String(k ?? ""))}>
              <SelectItem id="none" textValue="— اختر نوع المعرف —">— اختر نوع المعرف —</SelectItem>
              <SelectItem id="TaxId" textValue="الرقم الضريبي (VAT / Tax ID)">الرقم الضريبي (VAT / Tax ID)</SelectItem>
              <SelectItem id="CommercialRegister" textValue="السجل التجاري (CR)">السجل التجاري (CR)</SelectItem>
              <SelectItem id="NationalId" textValue="الرقم القومي / الهوية الوطنية">الرقم القومي / الهوية الوطنية</SelectItem>
              <SelectItem id="InternalReference" textValue="كود مرجعي داخلي">كود مرجعي داخلي</SelectItem>
            </Select>
          </div>
          <TextField label="قيمة المعرف *" placeholder="مثال: 300123456700003" value={identValue} onChange={setIdentValue} isRequired />
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setAddIdentifierOpen(false)}>إلغاء</Button>
            <Button variant="primary" onPress={handleAddIdentifier} isDisabled={addIdentifier.isPending}>
              {addIdentifier.isPending ? "جارٍ الحفظ..." : "حفظ المعرف"}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Add Contact Person Dialog */}
      <Dialog isOpen={addContactPersonOpen} onOpenChange={setAddContactPersonOpen} title="إضافة جهة اتصال مباشرة" description="تسجيل شخص مسؤول يمثل هذا الطرف التجاري في التواصل اليومي." size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <TextField label="الاسم الكامل *" placeholder="مثال: أحمد محمود" value={cpDisplayName} onChange={setCpDisplayName} isRequired />
          <TextField label="المنصب الوظيفي" placeholder="مثال: مدير المشتريات" value={cpJobTitle} onChange={setCpJobTitle} />
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setAddContactPersonOpen(false)}>إلغاء</Button>
            <Button variant="primary" onPress={handleAddContactPerson} isDisabled={addContactPerson.isPending}>
              {addContactPerson.isPending ? "جارٍ الحفظ..." : "حفظ جهة الاتصال"}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Add Alias Dialog */}
      <Dialog isOpen={addAliasOpen} onOpenChange={setAddAliasOpen} title="إضافة اسم بديل / تجاري" description="تسجيل اسم بديل أو اسم تجاري إضافي يُعرف به هذا الطرف." size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <TextField label="الاسم البديل *" placeholder="مثال: Atlas General Trading" value={aliasValue} onChange={setAliasValue} isRequired />
          <div>
            <Select label="لغة الاسم البديل" selectedKey={aliasLang} onSelectionChange={(k) => setAliasLang(String(k ?? "ar"))}>
              <SelectItem id="ar" textValue="العربية">العربية</SelectItem>
              <SelectItem id="en" textValue="الإنجليزية">الإنجليزية</SelectItem>
            </Select>
          </div>
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setAddAliasOpen(false)}>إلغاء</Button>
            <Button variant="primary" onPress={handleAddAlias} isDisabled={addAlias.isPending}>
              {addAlias.isPending ? "جارٍ الحفظ..." : "حفظ الاسم البديل"}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Visibility Dialog */}
      <Dialog isOpen={visibilityOpen} onOpenChange={setVisibilityOpen} title="تعديل نطاق الرؤية" description="تحديد الجهات القانونية التي يمكنها رؤية واستخدام هذا السجل في العمليات." size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <div>
            <Select label="نطاق الرؤية" selectedKey={visibilityScopeType} onSelectionChange={(k) => setVisibilityScopeType(String(k ?? "TenantWide"))}>
              <SelectItem id="TenantWide" textValue="مرئي لجميع الجهات القانونية في المؤسسة">مرئي لجميع الجهات القانونية في المؤسسة</SelectItem>
              <SelectItem id="SelectedLegalEntities" textValue="مرئي لجهات قانونية محددة فقط">مرئي لجهات قانونية محددة فقط</SelectItem>
              <SelectItem id="SingleLegalEntity" textValue="مرئي لجهة قانونية واحدة فقط">مرئي لجهة قانونية واحدة فقط</SelectItem>
            </Select>
          </div>
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setVisibilityOpen(false)}>إلغاء</Button>
            <Button variant="primary" onPress={handleSetVisibility} isDisabled={setVisibility.isPending}>
              {setVisibility.isPending ? "جارٍ الحفظ..." : "حفظ إعدادات الرؤية"}
            </Button>
          </div>
        </div>
      </Dialog>
    </RecordPage>
  );
}

