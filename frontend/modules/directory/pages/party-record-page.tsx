import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import { useQueryTab } from "../../../core/routing/use-query-tab.ts";
import { DateTimeDisplay, LocalDateDisplay } from "../../../components/fields/index.ts";
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
import { useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import { auditActionLabel, auditActorLabel, auditChanges, auditEntityLabel, auditFieldChanges } from "../audit-format.ts";

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

type DirectoryTranslator = TFunction<"directory">;

function addressUsageLabel(usage: string, t: DirectoryTranslator): string {
  if (usage === "Billing") return t("partyRecord.addressUsage.billing");
  if (usage === "Shipping") return t("partyRecord.addressUsage.shipping");
  if (usage === "Registered") return t("partyRecord.addressUsage.registered");
  return t("partyRecord.addressUsage.general");
}

function identifierTypeLabel(type: string, t: DirectoryTranslator): string {
  if (type === "TaxId" || type === "Tax") return t("partyRecord.identifierType.tax");
  if (type === "CommercialRegister") return t("partyRecord.identifierType.commercialRegister");
  if (type === "National" || type === "NationalId") return t("partyRecord.identifierType.national");
  if (type === "InternalReference") return t("partyRecord.identifierType.internalReference");
  return t("partyRecord.identifierType.official");
}

function capabilityLabel(cap: string, t: DirectoryTranslator): string {
  if (cap === "CanSell") return t("partyRecord.capability.sell");
  if (cap === "CanPurchase") return t("partyRecord.capability.purchase");
  if (cap === "CanInvoice") return t("partyRecord.capability.invoice");
  if (cap === "CanPay") return t("partyRecord.capability.pay");
  return t("partyRecord.capability.generic");
}

import { PartyPicker } from "../components/party-picker.tsx";

function missingRequirementLabel(req: string, t: DirectoryTranslator): string {
  const normalized = req.trim();
  const lower = normalized.toLowerCase();
  if ((lower.includes("customer role") || lower.includes("customer")) && (lower.includes("legal identifier") || lower.includes("tax") || lower.includes("vat"))) {
    return t("partyRecord.missingRequirements.customerIdentifier");
  }
  if (lower.includes("customer role") || lower.includes("customer")) {
    return t("partyRecord.missingRequirements.customerRole");
  }
  if (lower.includes("supplier role") || lower.includes("supplier")) {
    return t("partyRecord.missingRequirements.supplierRole");
  }
  if (lower.includes("legal identifier") || lower.includes("tax") || lower.includes("vat") || lower.includes("commercial register")) {
    return t("partyRecord.missingRequirements.identifier");
  }
  if (lower.includes("shipping address")) {
    return t("partyRecord.missingRequirements.shippingAddress");
  }
  if (lower.includes("billing address") || lower.includes("address")) {
    return t("partyRecord.missingRequirements.billingAddress");
  }
  if (lower.includes("contact") || lower.includes("phone") || lower.includes("email")) {
    return t("partyRecord.missingRequirements.contactPoint");
  }
  return t("partyRecord.missingRequirements.generic");
}

function duplicateReasonLabel(reason: string, t: DirectoryTranslator): string {
  const lower = reason.toLowerCase();
  if (lower.includes("tax") || lower.includes("vat") || lower.includes("identifier")) return t("partyRecord.duplicateReasons.identifier");
  if (lower.includes("phone")) return t("partyRecord.duplicateReasons.phone");
  if (lower.includes("email")) return t("partyRecord.duplicateReasons.email");
  if (lower.includes("name")) return t("partyRecord.duplicateReasons.name");
  return t("partyRecord.duplicateReasons.generic");
}

function duplicateStatusLabel(status: string, t: DirectoryTranslator): string {
  if (status === "Pending") return t("partyRecord.duplicateStatus.pending");
  if (status === "Dismissed") return t("partyRecord.duplicateStatus.dismissed");
  if (status === "Confirmed") return t("partyRecord.duplicateStatus.confirmed");
  return t("partyRecord.duplicateStatus.review");
}

export function PartyRecordPage({ partyId = "", onBack }: PartyRecordPageProps): React.JSX.Element {
  const { t } = useTranslation("directory");
  const [activeTab, setActiveTab] = useQueryTab<Tab>({
    defaultTab: "overview",
    allowedTabs: ["overview", "contacts", "addresses", "identifiers", "audit"]
  });
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
  const [cpUsage, setCpUsage] = useState<string>("General");

  // Add alias form state
  const [aliasValue, setAliasValue] = useState("");
  const [aliasLang, setAliasLang] = useState("ar");

  const { showToast } = useToast();

  const { data: party, isLoading, error, refetch } = usePartyRecord(partyId);
  const { data: masterParty } = usePartyRecord(masterPartyId);
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

  // Keep this hook unconditional so the loading/error states cannot change the hook order.
  const perms = useOptionalPermissions();

  if (isLoading) {
    return (
      <div style={{ padding: "4rem 2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
        <p style={{ fontSize: "0.9375rem", fontWeight: 600 }}>{t("partyRecord.loading")}</p>
      </div>
    );
  }

  if (error || !party) {
    return (
      <div style={{ padding: "3rem 2rem", textAlign: "center" }}>
        <p style={{ color: "var(--color-status-danger)", fontWeight: 600, fontSize: "1rem" }}>
          {t("partyRecord.loadError")}
        </p>
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "center", marginTop: "1rem" }}>
          {onBack && (
            <Button variant="secondary" onPress={onBack}>
              {t("partyRecord.backToDirectory")}
            </Button>
          )}
          <Button variant="primary" onPress={() => void refetch()}>
            {t("partyRecord.retry")}
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

  const canEdit = perms?.hasPermission("directory.party.edit") ?? true;
  const canArchive = perms?.hasPermission("directory.party.archive") ?? true;
  const canMerge = perms?.hasPermission("directory.party.merge") ?? true;
  const canManageVisibility = perms?.hasPermission("directory.visibility.manage") ?? true;
  const canManageIdentifiers = perms?.hasPermission("directory.party.identifier.manage") ?? true;
  const canManageContacts = perms?.hasPermission("directory.contact.manage") ?? true;

  const handleToggleRole = async (roleCode: PartyRoleCode, currentlyActive: boolean) => {
    if (!canEdit) {
      showToast({ type: "warning", title: t("partyRecord.messages.permissionTitle"), description: t("partyRecord.messages.editPermissionRequired") });
      return;
    }
    try {
      if (currentlyActive) {
        await disableRole.mutateAsync({ id: party.id, role: roleCode });
        showToast({ type: "success", title: t("partyRecord.messages.roleUpdated"), description: t("partyRecord.messages.roleDisabled", { role: roleCode === "Customer" ? t("partyList.customer") : t("partyList.supplier") }) });
      } else {
        await addRole.mutateAsync({ id: party.id, role: roleCode, version: party.version });
        showToast({ type: "success", title: t("partyRecord.messages.roleEnabled"), description: t("partyRecord.messages.roleEnabledDescription", { role: roleCode === "Customer" ? t("partyList.customer") : t("partyList.supplier") }) });
      }
    } catch (err: unknown) {
      showToast({ type: "error", title: t("partyRecord.messages.roleChangeError"), description: t("partyRecord.messages.genericError") });
    }
  };

  const handleArchive = async () => {
    try {
      await archive.mutateAsync({ id: party.id, version: party.version });
      setArchiveConfirmOpen(false);
      showToast({ type: "success", title: t("partyRecord.messages.archived"), description: t("partyRecord.messages.archivedDescription", { name: party.displayName }) });
    } catch (err: unknown) {
      showToast({ type: "error", title: t("partyRecord.messages.archiveError"), description: t("partyRecord.messages.genericError") });
    }
  };

  const handleRestore = async () => {
    try {
      await restore.mutateAsync({ id: party.id, version: party.version });
      setRestoreConfirmOpen(false);
      showToast({ type: "success", title: t("partyRecord.messages.restored"), description: t("partyRecord.messages.restoredDescription", { name: party.displayName }) });
    } catch (err: unknown) {
      showToast({ type: "error", title: t("partyRecord.messages.restoreError"), description: t("partyRecord.messages.genericError") });
    }
  };

  const handleMerge = async () => {
    if (!masterPartyId.trim() || !masterParty) { showToast({ type: "warning", title: t("partyRecord.messages.selectionRequired"), description: t("partyRecord.messages.selectMaster") }); return; }
    if (masterParty.id === party.id) { showToast({ type: "warning", title: t("partyRecord.messages.invalidSelection"), description: t("partyRecord.messages.selectDifferentMaster") }); return; }
    if (!mergeReason.trim()) { showToast({ type: "warning", title: t("partyRecord.messages.missingDetails"), description: t("partyRecord.messages.mergeReasonRequired") }); return; }
    if (!mergeConfirmed) { showToast({ type: "warning", title: t("partyRecord.messages.confirmationRequired"), description: t("partyRecord.messages.mergeConfirmationRequired") }); return; }
    const req: MergePartyRequest = { masterPartyId, reason: mergeReason, confirmIrreversible: true };
    try {
      await mergeParty.mutateAsync({ sourcePartyId: party.id, input: req });
      setMergeOpen(false);
      showToast({ type: "success", title: t("partyRecord.messages.mergeSuccess"), description: t("partyRecord.messages.mergeSuccessDescription") });
      onBack?.();
    } catch (err: unknown) {
      showToast({ type: "error", title: t("partyRecord.messages.mergeError"), description: t("partyRecord.messages.genericError") });
    }
  };

  const handleAddAddress = async () => {
    if (!addrLine1.trim()) { showToast({ type: "warning", title: t("partyRecord.messages.missingDetails"), description: t("partyRecord.messages.addressRequired") }); return; }
    const input: AddressInput = { usage: addrUsage as AddressInput["usage"], countryCode: addrCountry, city: addrCity || undefined, line1: addrLine1, isDefault: party.addresses.length === 0 };
    try {
      await addAddress.mutateAsync({ id: party.id, input });
      setAddAddressOpen(false);
      setAddrLine1(""); setAddrCity(""); setAddrUsage("General");
      showToast({ type: "success", title: t("partyRecord.messages.addressAdded"), description: t("partyRecord.messages.addressAddedDescription") });
    } catch (err: unknown) {
      showToast({ type: "error", title: t("partyRecord.messages.addressError"), description: t("partyRecord.messages.genericError") });
    }
  };

  const handleAddContactPoint = async () => {
    if (!cpValue.trim()) { showToast({ type: "warning", title: t("partyRecord.messages.missingDetails"), description: t("partyRecord.messages.contactValueRequired") }); return; }
    const input: ContactPointInput = { type: cpType as ContactPointInput["type"], value: cpValue, label: cpLabel || undefined, isPrimary: party.contactPoints.length === 0 };
    try {
      await addContactPoint.mutateAsync({ id: party.id, input });
      setAddContactOpen(false);
      setCpValue(""); setCpLabel(""); setCpType("Email");
      showToast({ type: "success", title: t("partyRecord.messages.contactAdded"), description: t("partyRecord.messages.contactAddedDescription") });
    } catch (err: unknown) {
      showToast({ type: "error", title: t("partyRecord.messages.genericAddError"), description: t("partyRecord.messages.genericError") });
    }
  };

  const handleAddIdentifier = async () => {
    if (!identType || !identValue.trim()) { showToast({ type: "warning", title: t("partyRecord.messages.missingDetails"), description: t("partyRecord.messages.identifierRequired") }); return; }
    const input: IdentifierInput = { type: identType, value: identValue, isPrimary: party.identifiers.length === 0 };
    try {
      await addIdentifier.mutateAsync({ id: party.id, input });
      setAddIdentifierOpen(false);
      setIdentType(""); setIdentValue("");
      showToast({ type: "success", title: t("partyRecord.messages.identifierAdded"), description: t("partyRecord.messages.identifierAddedDescription") });
    } catch (err: unknown) {
      showToast({ type: "error", title: t("partyRecord.messages.genericAddError"), description: t("partyRecord.messages.genericError") });
    }
  };

  const handleAddContactPerson = async () => {
    if (!cpDisplayName.trim()) { showToast({ type: "warning", title: t("partyRecord.messages.missingDetails"), description: t("partyRecord.messages.contactPersonNameRequired") }); return; }
    const req: AddContactPersonRequest = {
      person: { displayName: cpDisplayName, jobTitle: cpJobTitle || undefined },
      link: { contactPersonId: crypto.randomUUID(), usage: cpUsage, isPrimary: (party.contactPersons?.length ?? 0) === 0 }
    };
    try {
      await addContactPerson.mutateAsync({ id: party.id, input: req });
      setAddContactPersonOpen(false);
      setCpDisplayName(""); setCpJobTitle(""); setCpUsage("General");
      showToast({ type: "success", title: t("partyRecord.messages.contactPersonAdded"), description: t("partyRecord.messages.contactPersonAddedDescription", { name: cpDisplayName }) });
    } catch {
      showToast({ type: "error", title: t("partyRecord.messages.contactPersonError"), description: t("partyRecord.messages.genericError") });
    }
  };

  const handleAddAlias = async () => {
    if (!aliasValue.trim()) { showToast({ type: "warning", title: t("partyRecord.messages.missingDetails"), description: t("partyRecord.messages.aliasRequired") }); return; }
    const input: AliasInput = { alias: aliasValue, languageCode: aliasLang, aliasType: "Alternate" };
    try {
      await addAlias.mutateAsync({ id: party.id, input });
      setAddAliasOpen(false);
      setAliasValue("");
      showToast({ type: "success", title: t("partyRecord.messages.aliasAdded"), description: t("partyRecord.messages.aliasAddedDescription") });
    } catch (err: unknown) {
      showToast({ type: "error", title: t("partyRecord.messages.genericAddError"), description: t("partyRecord.messages.genericError") });
    }
  };

  const handleSetVisibility = async () => {
    const input: VisibilityInput = { scopeType: visibilityScopeType as VisibilityInput["scopeType"] };
    try {
      await setVisibility.mutateAsync({ id: party.id, input });
      setVisibilityOpen(false);
      showToast({ type: "success", title: t("partyRecord.messages.visibilityUpdated"), description: t("partyRecord.messages.visibilityUpdatedDescription") });
    } catch (err: unknown) {
      showToast({ type: "error", title: t("partyRecord.messages.visibilityError"), description: t("partyRecord.messages.genericError") });
    }
  };

  const roleLabels = party.roles.filter(r => r.status === "Active").map(r => (r.code === "Customer" ? t("partyList.customer") : t("partyList.supplier")));

  const kpis = (
    <>
      <StatCard
        label={t("partyRecord.entityNature")}
        value={isOrganization ? t("partyRecord.organization") : t("partyRecord.individual")}
        description={isOrganization ? t("partyRecord.organizationDescription") : t("partyRecord.individualDescription")}
        icon={isOrganization ? <BuildingIcon size={22} /> : <UserIcon size={22} />}
      />

      <StatCard
        label={t("partyRecord.activeRoles")}
        value={roleLabels.join(" • ") || t("partyRecord.genericParty")}
        description={roleLabels.length > 1 ? t("partyRecord.dualRole") : t("partyRecord.singleRole")}
        icon={<DirectoryIcon size={22} />}
      />

      <StatCard
        label={t("partyRecord.readiness")}
        value={readiness?.ready ? t("partyRecord.ready") : t("partyRecord.incomplete")}
        description={
          readiness?.ready
            ? t("partyRecord.requiredDataComplete")
            : readiness?.missing.length
            ? t("partyRecord.missingSummary", { requirements: readiness.missing.map((item) => missingRequirementLabel(item, t)).join("، ") })
            : t("partyRecord.reviewPending")
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
      recordCode={isOrganization ? t("partyRecord.registeredOrganization") : t("partyRecord.registeredIndividual")}
      status={isArchived ? t("partyList.statusArchived") : isMerged ? t("partyList.statusMerged") : t("partyList.statusActive")}
      breadcrumbs={[
        { label: t("partyList.title"), href: "/directory", onClick: onBack },
        { label: party.displayName }
      ]}
      headerActions={
        <div style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
          {onBack && (
            <Button variant="secondary" onPress={onBack}>
              {t("partyRecord.backToList")}
            </Button>
          )}
          {!isArchived && !isMerged ? (
            <>
              {canManageVisibility && (
                <Button variant="secondary" onPress={() => setVisibilityOpen(true)}>
                  {t("partyRecord.visibility")}
                </Button>
              )}
              {canEdit && (
                <Button variant="primary" onPress={() => setEditing(true)}>
                  {t("partyRecord.edit")}
                </Button>
              )}
              {canMerge && (
                <Button variant="danger" onPress={() => setMergeOpen(true)}>
                  {t("partyRecord.merge")}
                </Button>
              )}
              {canArchive && (
                <Button variant="danger" onPress={() => setArchiveConfirmOpen(true)}>
                  {t("partyRecord.archive")}
                </Button>
              )}
            </>
          ) : isArchived && canArchive ? (
            <Button variant="primary" onPress={() => setRestoreConfirmOpen(true)}>
              {t("partyRecord.restoreFromArchive")}
            </Button>
          ) : null}
        </div>
      }
      kpiCards={kpis}
      auditInfo={{
        createdAt: party.createdAt ? <LocalDateDisplay value={party.createdAt} dateStyle="long" /> : undefined,
        createdBy: t("partyRecord.systemActor")
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        {/* Status banners */}
        {isArchived && (
          <div style={{ padding: "0.875rem 1.25rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-sunken, #f1f5f9)", border: "1px solid var(--color-border-default)", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
            <div>
              <span style={{ fontWeight: 700, fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>{t("partyRecord.archivedBannerTitle")}</span>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("partyRecord.archivedBannerDescription")}</p>
            </div>
            <Button variant="secondary" size="compact" onPress={() => setRestoreConfirmOpen(true)}>
              {t("partyRecord.restore")}
            </Button>
          </div>
        )}
        {isMerged && (
          <div style={{ padding: "0.875rem 1.25rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-status-info-subtle, #f0f9ff)", border: "1px solid var(--color-status-info, #3b82f6)" }}>
            <span style={{ fontWeight: 700, fontSize: "0.875rem", color: "var(--color-status-info-content, #0369a1)" }}>{t("partyRecord.mergedBannerTitle")}</span>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("partyRecord.mergedBannerDescription")}</p>
          </div>
        )}

        {/* Duplicate alerts — shown before tabs when present */}
        {duplicates && duplicates.length > 0 && (
          <Surface
            title={t("partyRecord.duplicateTitle")}
            description={t("partyRecord.duplicateDescription")}
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
                      ✓ {duplicateReasonLabel(d.reason, t)}
                    </span>
                    <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                      {duplicateStatusLabel(d.status, t)}
                      {d.confidence > 0.9 ? ` · ${t("partyRecord.confidence.strong")}` : d.confidence > 0.7 ? ` · ${t("partyRecord.confidence.possible")}` : ` · ${t("partyRecord.confidence.similar")}`}
                    </span>
                  </div>
                  <div style={{ display: "flex", gap: "0.5rem", flexShrink: 0, alignItems: "center" }}>
                    <Button
                      size="compact"
                      variant="secondary"
                      onPress={() => {
                        setMasterPartyId(d.candidatePartyId);
                        setMergeOpen(true);
                      }}
                    >
                      {t("partyRecord.viewAndCompare")}
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
                        {t("partyRecord.mergeRecords")}
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
            const labels: Record<Tab, string> = { overview: t("partyRecord.tabs.overview"), contacts: t("partyRecord.tabs.contacts"), addresses: t("partyRecord.tabs.addresses"), identifiers: t("partyRecord.tabs.identifiers"), audit: t("partyRecord.tabs.audit") };
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
            <Surface title={t("partyRecord.identityTitle")} description={t("partyRecord.identityDescription")}>
              <PropertyGrid columns={3}>
                <PropertyItem
                  label={t("partyRecord.legalType")}
                  value={isOrganization ? t("partyRecord.organizationType") : t("partyRecord.personType")}
                  icon={isOrganization ? <BuildingIcon size={14} /> : <UserIcon size={14} />}
                />
                <PropertyItem label={t("partyForm.legalName")} value={party.legalName || party.displayName} />
                {isOrganization && <PropertyItem label={t("partyForm.tradeName")} value={party.tradeName || party.displayName} />}
                {party.nameAr && <PropertyItem label={t("partyForm.nameAr")} value={party.nameAr} />}
                {party.nameEn && <PropertyItem label={t("partyForm.nameEn")} value={<bdi dir="ltr">{party.nameEn}</bdi>} />}
                {!isOrganization && party.firstName && (
                  <PropertyItem label={t("partyRecord.fullName")} value={`${party.firstName ?? ""} ${party.middleName ?? ""} ${party.lastName ?? ""}`.trim()} />
                )}
                <PropertyItem
                  label={t("partyForm.country")}
                  value={party.countryCode === "EG" ? t("partyForm.egypt") : party.countryCode === "SA" ? t("partyForm.saudi") : party.countryCode || "—"}
                />
                <PropertyItem
                  label={t("partyForm.correspondenceLanguage")}
                  value={party.primaryLanguage === "ar" ? t("partyRecord.dialogs.arabic") : t("partyRecord.dialogs.english")}
                />
                <PropertyItem
                  label={t("partyList.recordStatus")}
                  value={
                    <StatusBadge
                      status={isArchived ? "neutral" : isMerged ? "neutral" : "success"}
                      label={isArchived ? t("partyList.statusArchived") : isMerged ? t("partyList.statusMerged") : t("partyList.statusActive")}
                      size="small"
                    />
                  }
                />
              </PropertyGrid>
            </Surface>

            {/* Primary Identifiers & Contacts */}
            {party.identifiers.length > 0 && (
              <Surface
                title={t("partyRecord.identifiersAndContactsTitle")}
                description={t("partyRecord.identifiersAndContactsDescription")}
              >
                <PropertyGrid columns={3}>
                  {party.identifiers.map(ident => (
                    <PropertyItem
                      key={ident.id}
                      label={identifierTypeLabel(ident.type, t)}
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
                          {ident.verificationStatus === "Verified" ? t("partyRecord.verified") : t("partyRecord.verificationPending")}
                        </span>
                      }
                    />
                  ))}
                  {primaryEmail && (
                    <PropertyItem
                      label={t("partyForm.email")}
                      icon={<MailIcon size={14} />}
                      value={<bdi dir="ltr">{primaryEmail}</bdi>}
                    />
                  )}
                  {primaryPhone && (
                    <PropertyItem
                      label={t("partyForm.phone")}
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
                title={t("partyRecord.aliasesTitle")}
                description={t("partyRecord.aliasesDescription")}
                headerActions={
                  !isArchived && !isMerged && canEdit ? (
                    <Button size="compact" variant="secondary" onPress={() => setAddAliasOpen(true)}>
                      {t("partyRecord.addAlias")}
                    </Button>
                  ) : undefined
                }
              >
                {party.aliases && party.aliases.length > 0 ? (
                  <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                    {party.aliases.map(alias => (
                      <div key={alias.id} style={{ display: "flex", alignItems: "center", gap: "1rem", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
                        <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>{alias.alias}</span>
                        {alias.languageCode && <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{alias.languageCode === "ar" ? t("partyForm.arabicDefault") : t("partyForm.english")}</span>}
                        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginInlineStart: "auto" }}>{alias.aliasType === "Alternate" ? t("partyRecord.alternateName") : t("partyRecord.otherNameType")}</span>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>{t("partyRecord.noAliases")}</p>
                )}
              </Surface>
            )}

            {/* Roles */}
            <Surface title={t("partyRecord.rolesTitle")} description={t("partyRecord.rolesDescription")}>
              <div style={{ display: "flex", gap: "1.5rem", flexWrap: "wrap", alignItems: "center" }}>
                {(["Customer", "Supplier"] as PartyRoleCode[]).map(roleCode => {
                  const isActive = roleCode === "Customer" ? activeCustomer : activeSupplier;
                  const roleName = roleCode === "Customer" ? t("partyList.customer") : t("partyList.supplier");
                  const roleDesc = roleCode === "Customer" ? t("partyRecord.customerRoleDescription") : t("partyRecord.supplierRoleDescription");
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
                        <span style={{ fontWeight: 700, fontSize: "0.875rem" }}>{t("partyRecord.roleName", { role: roleName })}</span>
                        <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{roleDesc}</p>
                      </div>
                      <Button
                        size="compact"
                        variant={isActive ? "secondary" : "primary"}
                        isDisabled={isArchived || isMerged || addRole.isPending || disableRole.isPending}
                        onPress={() => handleToggleRole(roleCode, isActive)}
                      >
                        {isActive ? t("partyRecord.disableRole") : t("partyRecord.enableRole", { role: roleName })}
                      </Button>
                    </div>
                  );
                })}
              </div>
            </Surface>

            {/* Readiness */}
            {readiness && (
              <Surface title={t("partyRecord.readinessTitle")} description={t("partyRecord.readinessDescription")}>
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
                            {capabilityLabel(cap.capability, t)}
                          </span>
                          {!cap.ready && cap.missing.length > 0 && (
                            <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                              {t("partyRecord.missingSummary", { requirements: cap.missing.map((item) => missingRequirementLabel(item, t)).join("، ") })}
                            </p>
                          )}
                        </div>
                        <StatusBadge
                          status={cap.ready ? "success" : "warning"}
                          label={cap.ready ? t("partyRecord.complete") : t("partyRecord.missing")}
                          size="small"
                        />
                      </div>
                    ))
                  ) : (
                    <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", padding: "0.875rem" }}>
                      <StatusBadge status={readiness.ready ? "success" : "warning"} label={readiness.ready ? t("partyRecord.ready") : t("partyRecord.incomplete")} size="small" />
                      {!readiness.ready && readiness.missing.length > 0 && (
                        <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                          {t("partyRecord.missingSummary", { requirements: readiness.missing.map((item) => missingRequirementLabel(item, t)).join("، ") })}
                        </span>
                      )}
                    </div>
                  )}
                </div>
              </Surface>
            )}

            {/* Visibility */}
            <Surface
              title={t("partyRecord.visibilityTitle")}
              description={t("partyRecord.visibilityDescription")}
              headerActions={
                !isArchived && !isMerged && canManageVisibility ? (
                  <Button size="compact" variant="secondary" onPress={() => setVisibilityOpen(true)}>
                    {t("partyRecord.editVisibility")}
                  </Button>
                ) : undefined
              }
            >
              {party.visibility && party.visibility.length > 0 ? (
                <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                  {party.visibility.map(v => (
                    <div key={v.id} style={{ padding: "0.5rem 0.75rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)", fontSize: "0.875rem" }}>
                      {v.scopeType === "TenantWide" ? t("partyRecord.visibilityScopes.tenantWide") :
                       v.scopeType === "SelectedLegalEntities" ? t("partyRecord.visibilityScopes.selectedEntities") :
                       v.scopeType === "SingleLegalEntity" ? t("partyRecord.visibilityScopes.singleEntity") :
                       v.scopeType === "Branch" ? t("partyRecord.visibilityScopes.branch") : t("partyRecord.visibilityScopes.custom")}
                    </div>
                  ))}
                </div>
              ) : (
                <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>{t("partyRecord.defaultVisibility")}</p>
              )}
            </Surface>
          </>
        )}

        {/* TAB: Contacts */}
        {activeTab === "contacts" && (
          <>
            <Surface
              title={t("partyRecord.contactChannelsTitle")}
              description={t("partyRecord.contactChannelsDescription")}
              headerActions={
                !isArchived && !isMerged && canManageContacts ? (
                  <Button size="compact" variant="primary" onPress={() => setAddContactOpen(true)}>
                    {t("partyRecord.addContactMethod")}
                  </Button>
                ) : undefined
              }
            >
              {party.contactPoints.length > 0 ? (
                <PropertyGrid columns={2}>
                  {party.contactPoints.map(cp => (
                    <PropertyItem
                      key={cp.id}
                      label={cp.label || (cp.type === "Email" ? t("partyForm.email") : cp.type === "Phone" ? t("partyForm.phone") : cp.type === "Website" ? t("partyRecord.website") : t("partyRecord.contactMethod"))}
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
                      badge={cp.isPrimary ? <span style={{ fontSize: "0.6875rem", fontWeight: 700, padding: "0.125rem 0.375rem", borderRadius: "var(--radius-full)", backgroundColor: "var(--color-brand-subtle)", color: "var(--color-brand-primary)" }}>{t("partyRecord.primary")}</span> : undefined}
                    />
                  ))}
                </PropertyGrid>
              ) : (
                <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>{t("partyRecord.noContactMethods")}</p>
              )}
            </Surface>

            <Surface
              title={t("partyRecord.contactPersonsTitle")}
              description={t("partyRecord.contactPersonsDescription")}
              headerActions={
                !isArchived && !isMerged && canManageContacts ? (
                  <Button size="compact" variant="primary" onPress={() => setAddContactPersonOpen(true)}>
                    {t("partyRecord.addContactPerson")}
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
                      <StatusBadge status={cp.status === "Active" ? "success" : "neutral"} label={cp.status === "Active" ? t("partyList.statusActive") : t("partyRecord.inactive")} size="small" />
                    </div>
                  ))}
                </div>
              ) : (
                <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>{t("partyRecord.noContactPersons")}</p>
              )}
            </Surface>
          </>
        )}

        {/* TAB: Addresses */}
        {activeTab === "addresses" && (
          <Surface
            title={t("partyRecord.addressesTitle")}
            description={t("partyRecord.addressesDescription")}
            headerActions={
              !isArchived && !isMerged && canEdit ? (
                <Button size="compact" variant="primary" onPress={() => setAddAddressOpen(true)}>
                  {t("partyRecord.addAddress")}
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
                        <span style={{ fontWeight: 700, fontSize: "0.875rem" }}>{addressUsageLabel(addr.usage, t)}</span>
                        {addr.isDefault && <span style={{ fontSize: "0.6875rem", fontWeight: 700, padding: "0.125rem 0.375rem", borderRadius: "var(--radius-full)", backgroundColor: "var(--color-brand-subtle)", color: "var(--color-brand-primary)" }}>{t("partyRecord.default")}</span>}
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
              <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>{t("partyRecord.noAddresses")}</p>
            )}
          </Surface>
        )}

        {/* TAB: Identifiers */}
        {activeTab === "identifiers" && (
          <Surface
            title={t("partyRecord.officialIdentifiersTitle")}
            description={t("partyRecord.officialIdentifiersDescription")}
            headerActions={
              !isArchived && !isMerged && canManageIdentifiers ? (
                <Button size="compact" variant="primary" onPress={() => setAddIdentifierOpen(true)}>
                  {t("partyRecord.addIdentifier")}
                </Button>
              ) : undefined
            }
          >
            {party.identifiers.length > 0 ? (
              <PropertyGrid columns={2}>
                {party.identifiers.map(ident => (
                  <PropertyItem
                    key={ident.id}
                    label={identifierTypeLabel(ident.type, t)}
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
                        {ident.verificationStatus === "Verified" ? t("partyRecord.verified") : t("partyRecord.verificationPending")}
                      </span>
                    }
                  />
                ))}
              </PropertyGrid>
            ) : (
              <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>{t("partyRecord.noIdentifiers")}</p>
            )}
          </Surface>
        )}

        {/* TAB: Audit */}
        {activeTab === "audit" && (
          <Surface title={t("partyRecord.auditTitle")} description={t("partyRecord.auditDescription")}>
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
                      <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>{auditActionLabel(entry.action)}</span>
                      <p style={{ margin: "0.125rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{auditEntityLabel(entry.entityType)}</p>
                      {entry.actorName && <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("partyRecord.auditBy")} {entry.actorName}{auditActorLabel(entry.actorType) ? ` (${auditActorLabel(entry.actorType)})` : ""}</p>}
                      {(entry.changes?.length ? auditFieldChanges(entry.changes) : auditChanges(entry.afterJson)).map(change => (
                        <p key={`${change.label}:${change.beforeValue ?? ""}:${change.afterValue ?? ""}`} style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
                          {change.label}{change.beforeValue !== undefined && <> · {t("partyRecord.previousValue")} {change.beforeValue}</>}{change.afterValue !== undefined && <> · {t("partyRecord.newValue")} {change.afterValue}</>}
                        </p>
                      ))}
                      {entry.reason && <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("partyRecord.reason")} {entry.reason}</p>}
                    </div>
                    <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", whiteSpace: "nowrap", flexShrink: 0 }}>
                      <DateTimeDisplay value={entry.occurredAt} type="datetime" timeStyle="short" />
                    </span>
                  </div>
                ))}
              </div>
            ) : (
              <p style={{ color: "var(--color-content-muted)", fontSize: "0.875rem" }}>{t("partyRecord.noAuditEntries")}</p>
            )}
          </Surface>
        )}
      </div>

      {/* Edit Dialog */}
      <Dialog isOpen={editing} onOpenChange={setEditing} title={t("partyRecord.dialogs.editTitle")} description={t("partyRecord.dialogs.editDescription")} size="large">
        <PartyForm
          initialData={toForm(party)}
          onSubmit={async (formData: PartyFormData) => {
            try {
              let updatedParty = await update.mutateAsync({
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
              for (const role of ["Customer", "Supplier"] as const) {
                const isActive = updatedParty.roles.some((item) => item.code === role && item.status === "Active");
                const shouldBeActive = formData.roles.includes(role);
                if (shouldBeActive && !isActive) {
                  updatedParty = await addRole.mutateAsync({ id: party.id, role, version: updatedParty.version });
                } else if (!shouldBeActive && isActive) {
                  updatedParty = await disableRole.mutateAsync({ id: party.id, role });
                }
              }
              setEditing(false);
              showToast({ type: "success", title: t("partyRecord.dialogs.editSaved"), description: t("partyRecord.dialogs.editSavedDescription", { name: formData.displayName }) });
            } catch (err: unknown) {
              showToast({ type: "error", title: t("partyRecord.dialogs.editError"), description: t("partyRecord.dialogs.genericError") });
            }
          }}
          onCancel={() => setEditing(false)}
          isSubmitting={update.isPending || addRole.isPending || disableRole.isPending}
        />
      </Dialog>

      {/* Archive Confirmation */}
      <Dialog isOpen={archiveConfirmOpen} onOpenChange={setArchiveConfirmOpen} title={t("partyRecord.dialogs.archiveTitle")} description={t("partyRecord.dialogs.archiveDescription", { name: party.displayName })} size="medium">
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end", marginTop: "1.5rem" }}>
          <Button variant="secondary" onPress={() => setArchiveConfirmOpen(false)}>{t("partyRecord.dialogs.cancel")}</Button>
          <Button variant="danger" onPress={handleArchive} isDisabled={archive.isPending}>
            {archive.isPending ? t("partyRecord.dialogs.archiving") : t("partyRecord.dialogs.confirmArchive")}
          </Button>
        </div>
      </Dialog>

      {/* Restore Confirmation */}
      <Dialog isOpen={restoreConfirmOpen} onOpenChange={setRestoreConfirmOpen} title={t("partyRecord.dialogs.restoreTitle")} description={t("partyRecord.dialogs.restoreDescription", { name: party.displayName })} size="medium">
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end", marginTop: "1.5rem" }}>
          <Button variant="secondary" onPress={() => setRestoreConfirmOpen(false)}>{t("partyRecord.dialogs.cancel")}</Button>
          <Button variant="primary" onPress={handleRestore} isDisabled={restore.isPending}>
            {restore.isPending ? t("partyRecord.dialogs.restoring") : t("partyRecord.dialogs.confirmRestore")}
          </Button>
        </div>
      </Dialog>

      {/* Merge Dialog — irreversible action */}
      <Dialog
        isOpen={mergeOpen}
        onOpenChange={(open) => { if (!open) { setMergeOpen(false); setMasterPartyId(""); setMergeReason(""); setMergeConfirmed(false); } }}
        title={t("partyRecord.dialogs.mergeTitle")}
        description={t("partyRecord.dialogs.mergeDescription")}
        size="medium"
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <div style={{ padding: "0.75rem 1rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-status-warning-subtle, #fffbeb)", border: "1px solid var(--color-status-warning, #f59e0b)", fontSize: "0.875rem", color: "var(--color-status-warning-content, #92400e)", fontWeight: 600 }}>
            {t("partyRecord.dialogs.mergeWarning")}
          </div>
          <div>
            <PartyPicker
              label={t("partyRecord.dialogs.masterParty")}
              placeholder={t("partyRecord.dialogs.masterSearch")}
              value={masterPartyId}
              onChange={(id) => setMasterPartyId(id)}
              isRequired
              enableQuickCreate={false}
            />
          </div>
          {masterParty && masterParty.id !== party.id && (
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
                gap: "0.75rem",
                padding: "0.875rem",
                border: "1px solid var(--color-border-default)",
                borderRadius: "var(--radius-md)",
                backgroundColor: "var(--color-surface-sunken)"
              }}
            >
              {[
                { title: t("partyRecord.dialogs.sourceParty"), record: party },
                { title: t("partyRecord.dialogs.survivingParty"), record: masterParty }
              ].map(({ title, record }) => (
                <div key={title} style={{ minWidth: 0 }}>
                  <strong style={{ display: "block", fontSize: "0.75rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>{title}</strong>
                  <div style={{ fontWeight: 700, overflowWrap: "anywhere" }}>{record.displayName}</div>
                  <div style={{ fontSize: "0.75rem", color: "var(--color-content-secondary)", overflowWrap: "anywhere" }}>
                    {record.roles.filter((role) => role.status === "Active").map((role) => role.code === "Customer" ? t("partyRecord.dialogs.customer") : t("partyRecord.dialogs.supplier")).join("، ") || t("partyRecord.dialogs.genericParty")}
                  </div>
                  <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginTop: "0.25rem" }}>
                    {record.identifiers.length} {t("partyRecord.dialogs.identifierCount")} · {record.contactPersons?.length ?? 0} {t("partyRecord.dialogs.contactCount")} · {record.addresses.length} {t("partyRecord.dialogs.addressCount")}
                  </div>
                </div>
              ))}
            </div>
          )}
          <TextField
            label={t("partyRecord.dialogs.mergeReason")}
            placeholder={t("partyRecord.dialogs.mergeReasonPlaceholder")}
            value={mergeReason}
            onChange={setMergeReason}
            isRequired
          />
          <Checkbox isSelected={mergeConfirmed} onChange={setMergeConfirmed}>
            <span style={{ fontWeight: 600 }}>{t("partyRecord.dialogs.mergeConfirmation")}</span>
          </Checkbox>
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setMergeOpen(false)}>{t("partyRecord.dialogs.cancel")}</Button>
            <Button variant="danger" onPress={handleMerge} isDisabled={mergeParty.isPending || !mergeConfirmed}>
              {mergeParty.isPending ? t("partyRecord.dialogs.merging") : t("partyRecord.dialogs.confirmMerge")}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Add Address Dialog */}
      <Dialog isOpen={addAddressOpen} onOpenChange={setAddAddressOpen} title={t("partyRecord.dialogs.addAddressTitle")} size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <div>
            <Select label={t("partyRecord.dialogs.addressUsage")} selectedKey={addrUsage} onSelectionChange={(k) => setAddrUsage(String(k ?? "General"))}>
              <SelectItem id="General" textValue={t("partyRecord.dialogs.generalAddress")}>{t("partyRecord.dialogs.generalAddress")}</SelectItem>
              <SelectItem id="Billing" textValue={t("partyRecord.dialogs.billingAddress")}>{t("partyRecord.dialogs.billingAddress")}</SelectItem>
              <SelectItem id="Shipping" textValue={t("partyRecord.dialogs.shippingAddress")}>{t("partyRecord.dialogs.shippingAddress")}</SelectItem>
              <SelectItem id="Registered" textValue={t("partyRecord.dialogs.registeredAddress")}>{t("partyRecord.dialogs.registeredAddress")}</SelectItem>
            </Select>
          </div>
          <TextField label={t("partyRecord.dialogs.detailedAddress")} placeholder={t("partyRecord.dialogs.addressPlaceholder")} value={addrLine1} onChange={setAddrLine1} isRequired />
          <TextField label={t("partyRecord.dialogs.city")} placeholder={t("partyRecord.dialogs.cityPlaceholder")} value={addrCity} onChange={setAddrCity} />
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setAddAddressOpen(false)}>{t("partyRecord.dialogs.cancel")}</Button>
            <Button variant="primary" onPress={handleAddAddress} isDisabled={addAddress.isPending}>
              {addAddress.isPending ? t("partyRecord.dialogs.saving") : t("partyRecord.dialogs.saveAddress")}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Add Contact Point Dialog */}
      <Dialog isOpen={addContactOpen} onOpenChange={setAddContactOpen} title={t("partyRecord.dialogs.addContactTitle")} size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <div>
            <Select label={t("partyRecord.dialogs.contactType")} selectedKey={cpType} onSelectionChange={(k) => setCpType(String(k ?? "Email"))}>
              <SelectItem id="Email" textValue={t("partyRecord.dialogs.email")}>{t("partyRecord.dialogs.email")}</SelectItem>
              <SelectItem id="Phone" textValue={t("partyRecord.dialogs.phone")}>{t("partyRecord.dialogs.phone")}</SelectItem>
              <SelectItem id="Website" textValue={t("partyRecord.dialogs.website")}>{t("partyRecord.dialogs.website")}</SelectItem>
              <SelectItem id="Other" textValue={t("partyRecord.dialogs.other")}>{t("partyRecord.dialogs.other")}</SelectItem>
            </Select>
          </div>
          <TextField label={t("partyRecord.dialogs.value")} placeholder={cpType === "Email" ? "example@company.com" : cpType === "Phone" ? "+20 100 000 0000" : "https://"} value={cpValue} onChange={setCpValue} isRequired />
          <TextField label={t("partyRecord.dialogs.contactLabel")} placeholder={t("partyRecord.dialogs.contactLabelPlaceholder")} value={cpLabel} onChange={setCpLabel} />
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setAddContactOpen(false)}>{t("partyRecord.dialogs.cancel")}</Button>
            <Button variant="primary" onPress={handleAddContactPoint} isDisabled={addContactPoint.isPending}>
              {addContactPoint.isPending ? t("partyRecord.dialogs.saving") : t("partyRecord.dialogs.saveContact")}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Add Identifier Dialog */}
      <Dialog isOpen={addIdentifierOpen} onOpenChange={setAddIdentifierOpen} title={t("partyRecord.dialogs.addIdentifierTitle")} size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <div>
            <Select label={t("partyRecord.dialogs.identifierType")} selectedKey={identType || "none"} onSelectionChange={(k) => setIdentType(k === "none" ? "" : String(k ?? ""))}>
              <SelectItem id="none" textValue={t("partyRecord.dialogs.chooseIdentifier")}>{t("partyRecord.dialogs.chooseIdentifier")}</SelectItem>
              <SelectItem id="TaxId" textValue={t("partyRecord.dialogs.taxIdentifier")}>{t("partyRecord.dialogs.taxIdentifier")}</SelectItem>
              <SelectItem id="CommercialRegister" textValue={t("partyRecord.dialogs.commercialRegister")}>{t("partyRecord.dialogs.commercialRegister")}</SelectItem>
              <SelectItem id="NationalId" textValue={t("partyRecord.dialogs.nationalId")}>{t("partyRecord.dialogs.nationalId")}</SelectItem>
              <SelectItem id="InternalReference" textValue={t("partyRecord.dialogs.internalReference")}>{t("partyRecord.dialogs.internalReference")}</SelectItem>
            </Select>
          </div>
          <TextField label={t("partyRecord.dialogs.identifierValue")} placeholder={t("partyRecord.dialogs.identifierPlaceholder")} value={identValue} onChange={setIdentValue} isRequired />
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setAddIdentifierOpen(false)}>{t("partyRecord.dialogs.cancel")}</Button>
            <Button variant="primary" onPress={handleAddIdentifier} isDisabled={addIdentifier.isPending}>
              {addIdentifier.isPending ? t("partyRecord.dialogs.saving") : t("partyRecord.dialogs.saveIdentifier")}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Add Contact Person Dialog */}
      <Dialog isOpen={addContactPersonOpen} onOpenChange={setAddContactPersonOpen} title={t("partyRecord.dialogs.addContactPersonTitle")} description={t("partyRecord.dialogs.addContactPersonDescription")} size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <TextField label={t("partyRecord.dialogs.fullName")} placeholder={t("partyRecord.dialogs.personNamePlaceholder")} value={cpDisplayName} onChange={setCpDisplayName} isRequired />
          <TextField label={t("partyRecord.dialogs.jobTitle")} placeholder={t("partyRecord.dialogs.jobTitlePlaceholder")} value={cpJobTitle} onChange={setCpJobTitle} />
          <div>
            <Select label={t("partyRecord.dialogs.usageRelationship")} selectedKey={cpUsage} onSelectionChange={(k) => setCpUsage(String(k ?? "General"))}>
              <SelectItem id="General" textValue={t("partyRecord.dialogs.generalContact")}>{t("partyRecord.dialogs.generalContact")}</SelectItem>
              <SelectItem id="Billing" textValue={t("partyRecord.dialogs.billingContact")}>{t("partyRecord.dialogs.billingContact")}</SelectItem>
              <SelectItem id="Shipping" textValue={t("partyRecord.dialogs.shippingContact")}>{t("partyRecord.dialogs.shippingContact")}</SelectItem>
            </Select>
          </div>
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setAddContactPersonOpen(false)}>{t("partyRecord.dialogs.cancel")}</Button>
            <Button variant="primary" onPress={handleAddContactPerson} isDisabled={addContactPerson.isPending}>
              {addContactPerson.isPending ? t("partyRecord.dialogs.saving") : t("partyRecord.dialogs.saveContactPerson")}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Add Alias Dialog */}
      <Dialog isOpen={addAliasOpen} onOpenChange={setAddAliasOpen} title={t("partyRecord.dialogs.addAliasTitle")} description={t("partyRecord.dialogs.addAliasDescription")} size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <TextField label={t("partyRecord.dialogs.aliasName")} placeholder={t("partyRecord.dialogs.aliasPlaceholder")} value={aliasValue} onChange={setAliasValue} isRequired />
          <div>
            <Select label={t("partyRecord.dialogs.aliasLanguage")} selectedKey={aliasLang} onSelectionChange={(k) => setAliasLang(String(k ?? "ar"))}>
              <SelectItem id="ar" textValue={t("partyRecord.dialogs.arabic")}>{t("partyRecord.dialogs.arabic")}</SelectItem>
              <SelectItem id="en" textValue={t("partyRecord.dialogs.english")}>{t("partyRecord.dialogs.english")}</SelectItem>
            </Select>
          </div>
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setAddAliasOpen(false)}>{t("partyRecord.dialogs.cancel")}</Button>
            <Button variant="primary" onPress={handleAddAlias} isDisabled={addAlias.isPending}>
              {addAlias.isPending ? t("partyRecord.dialogs.saving") : t("partyRecord.dialogs.saveAlias")}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Visibility Dialog */}
      <Dialog isOpen={visibilityOpen} onOpenChange={setVisibilityOpen} title={t("partyRecord.dialogs.visibilityTitle")} description={t("partyRecord.dialogs.visibilityDescription")} size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.5rem" }}>
          <div>
            <Select label={t("partyRecord.dialogs.visibilityScope")} selectedKey={visibilityScopeType} onSelectionChange={(k) => setVisibilityScopeType(String(k ?? "TenantWide"))}>
              <SelectItem id="TenantWide" textValue={t("partyRecord.dialogs.allEntities")}>{t("partyRecord.dialogs.allEntities")}</SelectItem>
              <SelectItem id="SelectedLegalEntities" textValue={t("partyRecord.dialogs.selectedEntities")}>{t("partyRecord.dialogs.selectedEntities")}</SelectItem>
              <SelectItem id="SingleLegalEntity" textValue={t("partyRecord.dialogs.singleEntity")}>{t("partyRecord.dialogs.singleEntity")}</SelectItem>
            </Select>
          </div>
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setVisibilityOpen(false)}>{t("partyRecord.dialogs.cancel")}</Button>
            <Button variant="primary" onPress={handleSetVisibility} isDisabled={setVisibility.isPending}>
              {setVisibility.isPending ? t("partyRecord.dialogs.saving") : t("partyRecord.dialogs.saveVisibility")}
            </Button>
          </div>
        </div>
      </Dialog>
    </RecordPage>
  );
}

