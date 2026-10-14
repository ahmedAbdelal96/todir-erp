import type { AuditFieldChange } from "./types.ts";

export interface AuditChange {
  readonly label: string;
  readonly beforeValue?: string;
  readonly afterValue?: string;
}

const fieldLabels: Readonly<Record<string, string>> = {
  alias: "الاسم البديل",
  countryCode: "الدولة",
  displayName: "الاسم المعروض",
  isPrimary: "جهة الاتصال الرئيسية",
  languageCode: "لغة الاسم",
  legalName: "الاسم القانوني",
  nameAr: "الاسم بالعربية",
  nameEn: "الاسم بالإنجليزية",
  partyType: "نوع الطرف",
  role: "الدور التجاري",
  scopeType: "نطاق الإتاحة",
  status: "الحالة",
  tradeName: "الاسم التجاري",
  type: "نوع المعرّف الرسمي",
  usage: "الاستخدام"
};

const valueLabels: Readonly<Record<string, string>> = {
  Active: "نشط",
  Archived: "مؤرشف",
  Billing: "الفوترة",
  BusinessPartner: "شريك تجاري",
  Carrier: "ناقل",
  CommercialRegistration: "السجل التجاري",
  CommercialRegister: "السجل التجاري",
  Contractor: "مقاول",
  Customer: "عميل",
  GovernmentEntity: "جهة حكومية",
  General: "عام",
  Individual: "فرد",
  Inactive: "غير نشط",
  InternalReference: "رقم مرجعي داخلي",
  Merged: "مدمج",
  Organization: "منشأة تجارية",
  Other: "أخرى",
  Registered: "العنوان القانوني المسجل",
  Shipping: "الشحن والتوصيل",
  SelectedLegalEntities: "جهات قانونية محددة",
  SingleLegalEntity: "جهة قانونية واحدة",
  Supplier: "مورد",
  TaxIdentifier: "الرقم الضريبي",
  TaxId: "الرقم الضريبي",
  TenantWide: "على مستوى المؤسسة",
  VATRegistration: "تسجيل ضريبة القيمة المضافة",
  Verified: "تم التحقق",
  Unverified: "لم يتم التحقق",
  EG: "مصر",
  SA: "السعودية",
  ar: "العربية",
  en: "الإنجليزية",
  true: "نعم",
  false: "لا"
};

const enumFields = new Set(["countryCode", "isPrimary", "languageCode", "partyType", "role", "scopeType", "status", "type", "usage"]);

export function auditActionLabel(action: string): string {
  const labels: Readonly<Record<string, string>> = {
    AddressAdded: "إضافة عنوان",
    AliasAdded: "إضافة اسم بديل",
    Archived: "أرشفة السجل",
    ContactPersonAdded: "إضافة جهة اتصال",
    ContactPointAdded: "إضافة وسيلة تواصل",
    Created: "إنشاء السجل",
    IdentifierAdded: "إضافة معرّف رسمي",
    Merged: "دمج السجل",
    Restored: "استعادة السجل",
    RoleAdded: "إضافة دور تجاري",
    RoleDisabled: "إيقاف دور تجاري",
    Updated: "تحديث البيانات",
    VisibilitySet: "تعديل نطاق الإتاحة",
    party_address_added: "إضافة عنوان",
    party_alias_added: "إضافة اسم بديل",
    party_archived: "أرشفة السجل",
    party_contact_person_added: "إضافة جهة اتصال",
    party_contact_point_added: "إضافة وسيلة تواصل",
    party_created: "إنشاء السجل",
    party_identity_changed: "تعديل بيانات الهوية",
    party_identifier_added: "إضافة معرّف رسمي",
    party_merged: "دمج السجل",
    party_restored: "استعادة السجل",
    party_role_changed: "تعديل دور تجاري",
    party_visibility_changed: "تعديل نطاق الإتاحة"
  };
  return labels[action] ?? "تغيير في سجل الطرف التجاري";
}

export function auditChanges(afterJson?: string | null): readonly AuditChange[] {
  if (!afterJson) return [];

  let value: unknown;
  try {
    value = JSON.parse(afterJson) as unknown;
  } catch {
    return [];
  }

  if (!value || typeof value !== "object" || Array.isArray(value)) return [];

  return Object.entries(value).flatMap(([key, nextValue]) => {
    const normalizedKey = key[0]?.toLowerCase() + key.slice(1);
    const label = fieldLabels[normalizedKey];
    if (!label || /(?:^|_)(?:id|identifier)$/i.test(key) || /(?:party|contact|entity|source|master).*id/i.test(key)) return [];
    if (nextValue === null || nextValue === undefined || typeof nextValue === "object") return [];

    const rawValue = String(nextValue);
    const localizedValue = valueLabels[rawValue] ?? (enumFields.has(normalizedKey) ? "غير محدد" : rawValue);
    return [{ label, afterValue: localizedValue }];
  });
}

export function auditFieldChanges(changes?: readonly AuditFieldChange[] | null): readonly AuditChange[] {
  if (!changes) return [];

  return changes.flatMap(change => {
    const normalizedKey = change.field[0]?.toLowerCase() + change.field.slice(1);
    const label = fieldLabels[normalizedKey];
    if (!label || /(?:^|_)(?:id|identifier)$/i.test(change.field) || /(?:party|contact|entity|source|master).*id/i.test(change.field)) return [];

    const format = (value: unknown): string | undefined => {
      if (value === null || value === undefined || typeof value === "object") return undefined;
      const rawValue = String(value);
      return valueLabels[rawValue] ?? (enumFields.has(normalizedKey) ? "غير محدد" : rawValue);
    };
    const beforeValue = format(change.beforeValue);
    const afterValue = format(change.afterValue);
    if (beforeValue === undefined && afterValue === undefined) return [];
    return [{ label, ...(beforeValue === undefined ? {} : { beforeValue }), ...(afterValue === undefined ? {} : { afterValue }) }];
  });
}

export function auditActorLabel(actorType?: string | null): string | undefined {
  if (!actorType) return undefined;
  const labels: Readonly<Record<string, string>> = { Human: "مستخدم", System: "النظام", Service: "خدمة آلية", External: "مصدر خارجي" };
  return labels[actorType];
}

export function auditEntityLabel(entityType: string): string {
  return entityType.toLowerCase() === "party" ? "سجل الطرف التجاري" : "عنصر مرتبط بالسجل";
}
