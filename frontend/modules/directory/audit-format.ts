import type { AuditFieldChange } from "./types.ts";
import { i18n, currentLanguage } from "../../core/i18n/i18n.ts";
import type { en as directoryResource } from "./i18n/en.ts";

type NestedKey<T> = { [Key in keyof T & string]: T[Key] extends string ? Key : `${Key}.${NestedKey<T[Key]>}` }[keyof T & string];

function translateAudit(key: NestedKey<typeof directoryResource>): string {
  return i18n.getFixedT(currentLanguage(), "directory")(key);
}

export interface AuditChange {
  readonly label: string;
  readonly beforeValue?: string;
  readonly afterValue?: string;
}

const fieldLabels: Readonly<Record<string, NestedKey<typeof directoryResource>>> = {
  alias: "partyRecord.audit.fields.alias",
  countryCode: "partyRecord.audit.fields.countryCode",
  displayName: "partyRecord.audit.fields.displayName",
  isPrimary: "partyRecord.audit.fields.isPrimary",
  languageCode: "partyRecord.audit.fields.languageCode",
  legalName: "partyRecord.audit.fields.legalName",
  nameAr: "partyRecord.audit.fields.nameAr",
  nameEn: "partyRecord.audit.fields.nameEn",
  partyType: "partyRecord.audit.fields.partyType",
  role: "partyRecord.audit.fields.role",
  scopeType: "partyRecord.audit.fields.scopeType",
  status: "partyRecord.audit.fields.status",
  tradeName: "partyRecord.audit.fields.tradeName",
  type: "partyRecord.audit.fields.type",
  usage: "partyRecord.audit.fields.usage"
};

const valueLabels: Readonly<Record<string, NestedKey<typeof directoryResource>>> = {
  Active: "partyRecord.audit.values.active",
  Archived: "partyRecord.audit.values.archived",
  Billing: "partyRecord.audit.values.billing",
  BusinessPartner: "partyRecord.audit.values.businessPartner",
  Carrier: "partyRecord.audit.values.carrier",
  CommercialRegistration: "partyRecord.audit.values.commercialRegister",
  CommercialRegister: "partyRecord.audit.values.commercialRegister",
  Contractor: "partyRecord.audit.values.contractor",
  Customer: "partyRecord.audit.values.customer",
  GovernmentEntity: "partyRecord.audit.values.governmentEntity",
  General: "partyRecord.audit.values.general",
  Individual: "partyRecord.audit.values.individual",
  Inactive: "partyRecord.audit.values.inactive",
  InternalReference: "partyRecord.audit.values.internalReference",
  Merged: "partyRecord.audit.values.merged",
  Organization: "partyRecord.audit.values.organization",
  Other: "partyRecord.audit.values.other",
  Registered: "partyRecord.audit.values.registered",
  Shipping: "partyRecord.audit.values.shipping",
  SelectedLegalEntities: "partyRecord.audit.values.selectedLegalEntities",
  SingleLegalEntity: "partyRecord.audit.values.singleLegalEntity",
  Supplier: "partyRecord.audit.values.supplier",
  TaxIdentifier: "partyRecord.audit.values.taxId",
  TaxId: "partyRecord.audit.values.taxId",
  TenantWide: "partyRecord.audit.values.tenantWide",
  VATRegistration: "partyRecord.audit.values.vatRegistration",
  Verified: "partyRecord.audit.values.verified",
  Unverified: "partyRecord.audit.values.unverified",
  EG: "partyRecord.audit.values.egypt",
  SA: "partyRecord.audit.values.saudiArabia",
  ar: "partyRecord.audit.values.arabic",
  en: "partyRecord.audit.values.english",
  true: "partyRecord.audit.values.yes",
  false: "partyRecord.audit.values.no"
};

const enumFields = new Set(["countryCode", "isPrimary", "languageCode", "partyType", "role", "scopeType", "status", "type", "usage"]);

export function auditActionLabel(action: string): string {
  const labels: Readonly<Record<string, NestedKey<typeof directoryResource>>> = {
    AddressAdded: "partyRecord.audit.actions.addressAdded", AliasAdded: "partyRecord.audit.actions.aliasAdded", Archived: "partyRecord.audit.actions.archived",
    ContactPersonAdded: "partyRecord.audit.actions.contactPersonAdded", ContactPointAdded: "partyRecord.audit.actions.contactPointAdded", Created: "partyRecord.audit.actions.created",
    IdentifierAdded: "partyRecord.audit.actions.identifierAdded", Merged: "partyRecord.audit.actions.merged", Restored: "partyRecord.audit.actions.restored",
    RoleAdded: "partyRecord.audit.actions.roleAdded", RoleDisabled: "partyRecord.audit.actions.roleDisabled", Updated: "partyRecord.audit.actions.updated",
    VisibilitySet: "partyRecord.audit.actions.visibilitySet", party_address_added: "partyRecord.audit.actions.addressAdded", party_alias_added: "partyRecord.audit.actions.aliasAdded",
    party_archived: "partyRecord.audit.actions.archived", party_contact_person_added: "partyRecord.audit.actions.contactPersonAdded", party_contact_point_added: "partyRecord.audit.actions.contactPointAdded",
    party_created: "partyRecord.audit.actions.created", party_identity_changed: "partyRecord.audit.actions.identityChanged", party_identifier_added: "partyRecord.audit.actions.identifierAdded",
    party_merged: "partyRecord.audit.actions.merged", party_restored: "partyRecord.audit.actions.restored", party_role_changed: "partyRecord.audit.actions.roleChanged",
    party_visibility_changed: "partyRecord.audit.actions.visibilitySet"
  };
  return translateAudit(labels[action] ?? "partyRecord.audit.unknownAction");
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
    const labelKey = fieldLabels[normalizedKey];
    const label = labelKey ? translateAudit(labelKey) : undefined;
    if (!label || /(?:^|_)(?:id|identifier)$/i.test(key) || /(?:party|contact|entity|source|master).*id/i.test(key)) return [];
    if (nextValue === null || nextValue === undefined || typeof nextValue === "object") return [];

    const rawValue = String(nextValue);
    const valueKey = valueLabels[rawValue];
    const localizedValue = valueKey ? translateAudit(valueKey) : (enumFields.has(normalizedKey) ? translateAudit("partyRecord.audit.unknownValue") : rawValue);
    return [{ label, afterValue: localizedValue }];
  });
}

export function auditFieldChanges(changes?: readonly AuditFieldChange[] | null): readonly AuditChange[] {
  if (!changes) return [];

  return changes.flatMap(change => {
    const normalizedKey = change.field[0]?.toLowerCase() + change.field.slice(1);
    const labelKey = fieldLabels[normalizedKey];
    const label = labelKey ? translateAudit(labelKey) : undefined;
    if (!label || /(?:^|_)(?:id|identifier)$/i.test(change.field) || /(?:party|contact|entity|source|master).*id/i.test(change.field)) return [];

    const format = (value: unknown): string | undefined => {
      if (value === null || value === undefined || typeof value === "object") return undefined;
      const rawValue = String(value);
      const valueKey = valueLabels[rawValue];
      return valueKey ? translateAudit(valueKey) : (enumFields.has(normalizedKey) ? translateAudit("partyRecord.audit.unknownValue") : rawValue);
    };
    const beforeValue = format(change.beforeValue);
    const afterValue = format(change.afterValue);
    if (beforeValue === undefined && afterValue === undefined) return [];
    return [{ label, ...(beforeValue === undefined ? {} : { beforeValue }), ...(afterValue === undefined ? {} : { afterValue }) }];
  });
}

export function auditActorLabel(actorType?: string | null): string | undefined {
  if (!actorType) return undefined;
  const labels: Readonly<Record<string, NestedKey<typeof directoryResource>>> = { Human: "partyRecord.audit.actors.human", System: "partyRecord.audit.actors.system", Service: "partyRecord.audit.actors.service", External: "partyRecord.audit.actors.external" };
  const key = labels[actorType];
  return key ? translateAudit(key) : undefined;
}

export function auditEntityLabel(entityType: string): string {
  return translateAudit(entityType.toLowerCase() === "party" ? "partyRecord.audit.entityParty" : "partyRecord.audit.entityRelatedItem");
}
