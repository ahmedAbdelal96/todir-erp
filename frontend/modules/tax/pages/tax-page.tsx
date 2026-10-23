import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { TaxDataTable, type TaxTableColumn } from "../components/tax-data-table.tsx";
import {
  useTaxScope,
  useTaxPermissions,
  useTaxCapability,
  useTaxCoverage,
  useTaxRegistrations,
  useTaxProductProfiles,
  useTaxPartyProfiles,
  useTaxRuleSets,
  useTaxReferenceData,
  useTaxCountryPacks
} from "../hooks/tax-queries.ts";
import { TaxStatusBadge } from "../components/tax-status-badge.tsx";
import { TaxTreatmentDisplay } from "../components/tax-treatment-display.tsx";
import { CountryPackStatusBadge } from "../components/country-pack-status-badge.tsx";
import { TaxCapabilityModal } from "../features/tax-capability-modal.tsx";
import { RegistrationModal } from "../features/registration-modal.tsx";
import { RegistrationHistoryModal } from "../features/registration-history-modal.tsx";
import { RegistrationMembersModal } from "../features/registration-members-modal.tsx";
import { ProductProfileModal } from "../features/product-profile-modal.tsx";
import { PartyProfileModal } from "../features/party-profile-modal.tsx";
import { RuleSetModal } from "../features/rule-set-modal.tsx";
import { RuleModal } from "../features/rule-modal.tsx";
import { RuleSetValidateDialog } from "../features/rule-set-validate-dialog.tsx";
import { RuleSetActivateDialog } from "../features/rule-set-activate-dialog.tsx";
import { CountryPackActionDialog } from "../features/country-pack-action-dialog.tsx";
import { TaxSimulatorWidget } from "../features/tax-simulator-widget.tsx";
import { TaxImportExportModal } from "../features/tax-import-export-modal.tsx";
import type {
  TaxCountryPackDto,
  TaxProfileDto,
  TaxRegistrationDto,
  TaxRuleSetDto
} from "../types.ts";

export function TaxPage(): React.JSX.Element {
  const { t } = useTranslation("tax");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId, countryCode, today } = useTaxScope({ legalEntityId: outletCtx?.legalEntityId });
  const permissions = useTaxPermissions();

  const [activeTab, setActiveTab] = useState<string>("overview");

  // Modals state
  const [isCapabilityModalOpen, setIsCapabilityModalOpen] = useState(false);
  const [isRegistrationModalOpen, setIsRegistrationModalOpen] = useState(false);
  const [registrationModalMode, setRegistrationModalMode] = useState<"create" | "edit" | "deregister">("create");
  const [selectedRegistration, setSelectedRegistration] = useState<TaxRegistrationDto | null>(null);
  const [isRegHistoryModalOpen, setIsRegHistoryModalOpen] = useState(false);
  const [isRegMembersModalOpen, setIsRegMembersModalOpen] = useState(false);

  const [isProductModalOpen, setIsProductModalOpen] = useState(false);
  const [selectedProductProfile, setSelectedProductProfile] = useState<TaxProfileDto | null>(null);

  const [isPartyModalOpen, setIsPartyModalOpen] = useState(false);
  const [selectedPartyProfile, setSelectedPartyProfile] = useState<TaxProfileDto | null>(null);

  const [isRuleSetModalOpen, setIsRuleSetModalOpen] = useState(false);
  const [selectedRuleSet, setSelectedRuleSet] = useState<TaxRuleSetDto | null>(null);
  const [isRuleModalOpen, setIsRuleModalOpen] = useState(false);
  const [isValidateDialogOpen, setIsValidateDialogOpen] = useState(false);
  const [isActivateDialogOpen, setIsActivateDialogOpen] = useState(false);

  const [selectedCountryPack, setSelectedCountryPack] = useState<TaxCountryPackDto | null>(null);
  const [packAction, setPackAction] = useState<"validate" | "schedule" | "activate" | "retire" | "rollback">("validate");
  const [isPackActionDialogOpen, setIsPackActionDialogOpen] = useState(false);

  const [isImportExportModalOpen, setIsImportExportModalOpen] = useState(false);

  // Queries
  const { data: capabilityDto } = useTaxCapability(legalEntityId, today);
  const isTaxDisabled = capabilityDto?.capability === "Disabled";

  const { data: coverage, isLoading: isCoverageLoading } = useTaxCoverage({
    legalEntityId,
    at: today,
    countryCode
  });

  const { data: registrations, isLoading: isRegLoading } = useTaxRegistrations(legalEntityId);
  const { data: productProfiles, isLoading: isProductLoading } = useTaxProductProfiles({ legalEntityId });
  const { data: partyProfiles, isLoading: isPartyLoading } = useTaxPartyProfiles({ legalEntityId });
  const { data: ruleSets, isLoading: isRuleSetsLoading } = useTaxRuleSets(legalEntityId);
  const { data: refData } = useTaxReferenceData(legalEntityId);
  const { data: countryPacks, isLoading: isPacksLoading } = useTaxCountryPacks();

  // Tab definitions
  const tabs = [
    { id: "overview", label: t("tabs.overview") },
    { id: "registrations", label: t("tabs.registrations") },
    { id: "productProfiles", label: t("tabs.productProfiles") },
    { id: "partyProfiles", label: t("tabs.partyProfiles") },
    { id: "rules", label: t("tabs.rules") },
    { id: "countryPacks", label: t("tabs.countryPacks") },
    { id: "simulator", label: t("tabs.simulator") }
  ];

  // Helper actions
  const handleOpenRegistration = (reg: TaxRegistrationDto, mode: "edit" | "deregister") => {
    setSelectedRegistration(reg);
    setRegistrationModalMode(mode);
    setIsRegistrationModalOpen(true);
  };

  const handleOpenPackAction = (pack: TaxCountryPackDto, action: "validate" | "schedule" | "activate" | "retire" | "rollback") => {
    setSelectedCountryPack(pack);
    setPackAction(action);
    setIsPackActionDialogOpen(true);
  };

  // Columns for Registrations
  const registrationColumns: readonly TaxTableColumn<TaxRegistrationDto>[] = [
    {
      key: "schemeCode",
      header: t("registrations.scheme"),
      render: (item) => <span style={{ fontWeight: 600 }}>{item.schemeCode}</span>
    },
    {
      key: "countryCode",
      header: t("registrations.country"),
      width: "80px",
      render: (item) => <span>{item.countryCode}</span>
    },
    {
      key: "status",
      header: t("registrations.status"),
      width: "140px",
      render: (item) => <TaxStatusBadge status={item.status} type="registration" />
    },
    {
      key: "registrationNumber",
      header: t("registrations.registrationNumber"),
      render: (item) => item.registrationNumber ? <code>{item.registrationNumber}</code> : <span style={{ color: "var(--color-content-muted)" }}>{t("registrations.notAssigned")}</span>
    },
    {
      key: "effectiveFrom",
      header: t("registrations.effectiveFrom"),
      render: (item) => <span>{item.effectiveFrom}</span>
    },
    {
      key: "version",
      header: t("registrations.version"),
      width: "90px",
      render: (item) => <span>{t("registrations.versionTag", { version: item.version })}</span>
    },
    {
      key: "actions",
      header: "",
      width: "360px",
      render: (item) => (
        <div style={{ display: "flex", gap: "0.5rem", justifyContent: "flex-end", flexWrap: "nowrap", whiteSpace: "nowrap" }}>
          <Button
            variant="ghost"
            size="compact"
            onClick={() => { setSelectedRegistration(item); setIsRegHistoryModalOpen(true); }}
          >
            {t("registrations.historyAction")}
          </Button>
          <Button
            variant="ghost"
            size="compact"
            onClick={() => { setSelectedRegistration(item); setIsRegMembersModalOpen(true); }}
          >
            {t("registrations.membersAction")}
          </Button>
          {permissions.canManageRegistrations && item.status !== "Deregistered" && (
            <>
              <Button
                variant="secondary"
                size="compact"
                onClick={() => handleOpenRegistration(item, "edit")}
              >
                {t("actions.edit")}
              </Button>
              <Button
                variant="danger"
                size="compact"
                onClick={() => handleOpenRegistration(item, "deregister")}
              >
                {t("registrations.deregisterAction")}
              </Button>
            </>
          )}
        </div>
      )
    }
  ];

  // Columns for Product Profiles
  const productColumns: readonly TaxTableColumn<TaxProfileDto>[] = [
    {
      key: "productTemplateId",
      header: t("productProfiles.product"),
      render: (item) => (
        <div style={{ display: "flex", flexDirection: "column" }}>
          <span style={{ fontWeight: 600 }}>{item.productVariantId ? t("productProfiles.variantScope") : t("productProfiles.templateScope")}</span>
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontFamily: "var(--font-mono, monospace)" }}>
            {item.productVariantId || item.productTemplateId}
          </span>
        </div>
      )
    },
    {
      key: "scope",
      header: t("productProfiles.scope"),
      width: "130px",
      render: (item) => (
        <StatusBadge
          status={item.productVariantId ? "Variant" : "Template"}
          intent={item.productVariantId ? "info" : "neutral"}
          label={item.productVariantId ? t("productProfiles.scopeVariant") : t("productProfiles.scopeTemplate")}
        />
      )
    },
    {
      key: "categoryId",
      header: t("productProfiles.category"),
      render: (item) => {
        const cat = refData?.categories?.find((c) => c.id === item.categoryId);
        return <span>{cat ? `${cat.name} (${cat.code})` : item.categoryId || "—"}</span>;
      }
    },
    {
      key: "effectiveFrom",
      header: t("productProfiles.effectiveFrom"),
      render: (item) => <span>{item.effectiveFrom}</span>
    },
    {
      key: "version",
      header: t("productProfiles.version"),
      width: "90px",
      render: (item) => <span>{t("registrations.versionTag", { version: item.version })}</span>
    },
    {
      key: "actions",
      header: "",
      render: (item) =>
        permissions.canManageProductProfiles ? (
          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              variant="secondary"
              size="compact"
              onClick={() => { setSelectedProductProfile(item); setIsProductModalOpen(true); }}
            >
              {t("productProfiles.newVersionAction")}
            </Button>
          </div>
        ) : null
    }
  ];

  // Columns for Party Profiles
  const partyColumns: readonly TaxTableColumn<TaxProfileDto>[] = [
    {
      key: "partyId",
      header: t("partyProfiles.party"),
      render: (item) => (
        <div style={{ display: "flex", flexDirection: "column" }}>
          <span style={{ fontWeight: 600 }}>{t("partyProfiles.partyLabel")}</span>
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontFamily: "var(--font-mono, monospace)" }}>
            {item.partyId}
          </span>
        </div>
      )
    },
    {
      key: "countryCode",
      header: t("partyProfiles.country"),
      width: "80px",
      render: (item) => <span>{item.countryCode}</span>
    },
    {
      key: "treatment",
      header: t("partyProfiles.treatment"),
      render: (item) => (
        <TaxTreatmentDisplay
          treatment={item.treatment}
          isReverseCharge={Boolean(item.reverseChargeEligible)}
        />
      )
    },
    {
      key: "registrationNumber",
      header: t("partyProfiles.registrationNumber"),
      render: (item) => item.registrationNumber ? <code>{item.registrationNumber}</code> : <span>—</span>
    },
    {
      key: "effectiveFrom",
      header: t("partyProfiles.effectiveFrom"),
      render: (item) => <span>{item.effectiveFrom}</span>
    },
    {
      key: "version",
      header: t("partyProfiles.version"),
      width: "90px",
      render: (item) => <span>{t("registrations.versionTag", { version: item.version })}</span>
    },
    {
      key: "actions",
      header: "",
      render: (item) =>
        permissions.canManagePartyProfiles ? (
          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              variant="secondary"
              size="compact"
              onClick={() => { setSelectedPartyProfile(item); setIsPartyModalOpen(true); }}
            >
              {t("partyProfiles.newVersionAction")}
            </Button>
          </div>
        ) : null
    }
  ];

  // Columns for Rule Sets
  const ruleSetColumns: readonly TaxTableColumn<TaxRuleSetDto>[] = [
    {
      key: "countryCode",
      header: t("rules.country"),
      width: "80px",
      render: (item) => <span style={{ fontWeight: 600 }}>{item.countryCode}</span>
    },
    {
      key: "version",
      header: t("rules.version"),
      width: "90px",
      render: (item) => <span>{t("registrations.versionTag", { version: item.version })}</span>
    },
    {
      key: "status",
      header: t("rules.status"),
      width: "140px",
      render: (item) => <TaxStatusBadge status={item.status} type="ruleSet" />
    },
    {
      key: "sourceKind",
      header: t("rules.sourceKind"),
      render: (item) => (
        <StatusBadge
          status={item.sourceKind}
          intent={item.sourceKind === "TenantConfiguration" ? "info" : "neutral"}
          label={t(`rules.source.${item.sourceKind}` as any, { defaultValue: item.sourceKind })}
        />
      )
    },
    {
      key: "effectiveFrom",
      header: t("rules.effectiveFrom"),
      render: (item) => <span>{item.effectiveFrom}</span>
    },
    {
      key: "actions",
      header: "",
      width: "360px",
      render: (item) => (
        <div style={{ display: "flex", gap: "0.5rem", justifyContent: "flex-end", flexWrap: "nowrap", whiteSpace: "nowrap" }}>
          {item.status === "Draft" && permissions.canManageRules && (
            <>
              <Button
                variant="ghost"
                size="compact"
                onClick={() => { setSelectedRuleSet(item); setIsRuleModalOpen(true); }}
              >
                + {t("rules.addRuleAction")}
              </Button>
              <Button
                variant="secondary"
                size="compact"
                onClick={() => { setSelectedRuleSet(item); setIsValidateDialogOpen(true); }}
              >
                {t("rules.validateAction")}
              </Button>
            </>
          )}

          {item.status === "Draft" && permissions.canActivateRules && (
            <Button
              variant="primary"
              size="compact"
              onClick={() => { setSelectedRuleSet(item); setIsActivateDialogOpen(true); }}
            >
              {t("rules.activateAction")}
            </Button>
          )}

          {item.status === "Active" && (
            <span style={{ fontSize: "0.75rem", color: "var(--color-status-success-text)", alignSelf: "center", fontWeight: 600 }}>
              ✓ {t("rules.activeReadOnly")}
            </span>
          )}
        </div>
      )
    }
  ];

  // Columns for Country Packs
  const packColumns: readonly TaxTableColumn<TaxCountryPackDto>[] = [
    {
      key: "countryCode",
      header: t("countryPack.country"),
      width: "80px",
      render: (item) => <span style={{ fontWeight: 600 }}>{item.countryCode}</span>
    },
    {
      key: "packCode",
      header: t("countryPack.packCode"),
      render: (item) => (
        <div style={{ display: "flex", flexDirection: "column" }}>
          <span style={{ fontWeight: 600 }}>{item.packCode}</span>
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("registrations.versionTag", { version: item.packVersion })}</span>
        </div>
      )
    },
    {
      key: "status",
      header: t("countryPack.statusHeading"),
      render: (item) => (
        <CountryPackStatusBadge
          countryCode={item.countryCode}
          sourceKind={item.sourceKind}
          reviewStatus={item.reviewStatus}
          technicalStatus={item.status}
        />
      )
    },
    {
      key: "effectiveFrom",
      header: t("countryPack.effectiveFrom"),
      render: (item) => <span>{item.effectiveFrom}</span>
    },
    {
      key: "actions",
      header: "",
      width: "360px",
      render: (item) => (
        <div style={{ display: "flex", gap: "0.5rem", justifyContent: "flex-end", flexWrap: "nowrap", whiteSpace: "nowrap" }}>
          {permissions.canManageCountryPacks && (
            <Button
              variant="ghost"
              size="compact"
              onClick={() => handleOpenPackAction(item, "validate")}
            >
              {t("countryPack.validateAction")}
            </Button>
          )}
          {permissions.canActivateCountryPacks && item.status !== "Active" && (
            <Button
              variant="primary"
              size="compact"
              onClick={() => handleOpenPackAction(item, "activate")}
            >
              {t("countryPack.activateAction")}
            </Button>
          )}
          {permissions.canActivateCountryPacks && item.status === "Active" && (
            <Button
              variant="danger"
              size="compact"
              onClick={() => handleOpenPackAction(item, "retire")}
            >
              {t("countryPack.retireAction")}
            </Button>
          )}
        </div>
      )
    }
  ];

  return (
    <div style={{ padding: "1.5rem", display: "flex", flexDirection: "column", gap: "1.5rem", maxWidth: "1400px", margin: "0 auto" }}>
      {/* Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h1 style={{ margin: "0 0 0.375rem", fontSize: "1.5rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("hub.title")}
          </h1>
          <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("hub.subtitle")}
          </p>
        </div>

        <div style={{ display: "flex", gap: "0.75rem", alignItems: "center" }}>
          <TaxStatusBadge
            status={capabilityDto?.capability || "Disabled"}
            type="capability"
          />

          {permissions.canConfigureCapability && (
            <Button
              variant="secondary"
              size="compact"
              onClick={() => setIsCapabilityModalOpen(true)}
            >
              {isTaxDisabled ? t("capability.enableAction") : t("capability.configureAction")}
            </Button>
          )}

          {permissions.canImport && (
            <Button
              variant="ghost"
              size="compact"
              onClick={() => setIsImportExportModalOpen(true)}
            >
              {t("importExport.openAction")}
            </Button>
          )}
        </div>
      </div>

      {/* TAX DISABLED EXPERIENCE */}
      {isTaxDisabled && (
        <Surface variant="card">
          <div style={{ padding: "2rem", textAlign: "center", display: "flex", flexDirection: "column", alignItems: "center", gap: "1rem", maxWidth: "600px", margin: "0 auto" }}>
            <div
              style={{
                width: "48px",
                height: "48px",
                borderRadius: "var(--radius-full)",
                backgroundColor: "var(--color-surface-subtle, #f1f5f9)",
                display: "grid",
                placeItems: "center",
                fontSize: "1.5rem"
              }}
            >
              🏢
            </div>
            <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 600 }}>
              {t("disabledExperience.title")}
            </h2>
            <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)", lineHeight: 1.6 }}>
              {t("disabledExperience.description")}
            </p>
            {permissions.canConfigureCapability && (
              <Button
                variant="primary"
                onClick={() => setIsCapabilityModalOpen(true)}
              >
                {t("disabledExperience.enableButton")}
              </Button>
            )}
          </div>
        </Surface>
      )}

      {/* TAX ENABLED EXPERIENCE */}
      {!isTaxDisabled && (
        <>
          {/* Navigation Tabs */}
          <div
            style={{
              display: "flex",
              gap: "0.5rem",
              borderBottom: "1px solid var(--color-border-subtle, #e5e7eb)",
              paddingBottom: "0.5rem",
              overflowX: "auto"
            }}
          >
            {tabs.map((tab) => (
              <Button
                key={tab.id}
                variant={activeTab === tab.id ? "primary" : "ghost"}
                size="compact"
                onClick={() => setActiveTab(tab.id)}
              >
                {tab.label}
              </Button>
            ))}
          </div>

          {/* TAB: OVERVIEW / READINESS */}
          {activeTab === "overview" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
              {isCoverageLoading ? (
                <div style={{ display: "flex", justifyContent: "center", padding: "3rem" }}>
                  <Spinner size="large" />
                </div>
              ) : (
                <>
                  {/* Readiness Health Banner */}
                  <Surface variant="card">
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
                      <div>
                        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 600 }}>
                            {t("overview.readinessTitle")}
                          </h2>
                          <TaxStatusBadge status={coverage?.health || "Ready"} type="health" />
                        </div>
                        <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
                          {t("overview.readinessSubtitle")}
                        </p>
                      </div>

                      <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                        {t("overview.asOfDate")}: <strong>{today}</strong>
                      </div>
                    </div>
                  </Surface>

                  {/* Readiness Grid Cards */}
                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
                      gap: "1rem"
                    }}
                  >
                    <Surface variant="card">
                      <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", textTransform: "uppercase" }}>
                          {t("overview.registrationStatus")}
                        </span>
                        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                          <span style={{ fontWeight: 600, fontSize: "1rem" }}>{coverage?.registrationStatus || "—"}</span>
                          <StatusBadge
                            status={coverage?.registrationEffective ? "Effective" : "Inactive"}
                            intent={coverage?.registrationEffective ? "success" : "danger"}
                            label={coverage?.registrationEffective ? t("health.healthy") : t("health.blocked")}
                          />
                        </div>
                      </div>
                    </Surface>

                    <Surface variant="card">
                      <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", textTransform: "uppercase" }}>
                          {t("overview.countryPackStatus")}
                        </span>
                        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                          <span style={{ fontWeight: 600, fontSize: "1rem" }}>{coverage?.countryPackStatus || "—"}</span>
                          <CountryPackStatusBadge
                            countryCode={countryCode}
                            sourceKind="TestOnly"
                            reviewStatus="Pending"
                            technicalStatus={coverage?.countryPackStatus || "Installed"}
                          />
                        </div>
                      </div>
                    </Surface>

                    <Surface variant="card">
                      <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", textTransform: "uppercase" }}>
                          {t("overview.productCoverage")}
                        </span>
                        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                          <span style={{ fontWeight: 600, fontSize: "1rem" }}>{coverage?.productClassificationCoverage || "—"}</span>
                          <StatusBadge
                            status={coverage?.productClassificationCoverage === "Complete" ? "Complete" : "Pending"}
                            intent={coverage?.productClassificationCoverage === "Complete" ? "success" : "warning"}
                            label={coverage?.productClassificationCoverage === "Complete" ? t("health.healthy") : t("health.warning")}
                          />
                        </div>
                      </div>
                    </Surface>

                    <Surface variant="card">
                      <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", textTransform: "uppercase" }}>
                          {t("overview.activeRuleSet")}
                        </span>
                        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                          <span style={{ fontWeight: 600, fontSize: "1rem" }}>{coverage?.activeRuleSetStatus || "—"}</span>
                          <StatusBadge
                            status={coverage?.activeRuleSetStatus === "Active" ? "Active" : "None"}
                            intent={coverage?.activeRuleSetStatus === "Active" ? "success" : "warning"}
                            label={coverage?.activeRuleSetStatus === "Active" ? t("ruleSetStatus.active") : t("health.warning")}
                          />
                        </div>
                      </div>
                    </Surface>
                  </div>

                  {/* Readiness Issues / Attention Items */}
                  {coverage?.issues && coverage.issues.length > 0 && (
                    <Surface variant="card">
                      <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                        <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 600, color: "var(--color-status-warning-text, #92400e)" }}>
                          {t("overview.issuesTitle")} ({coverage.issues.length})
                        </h3>
                        <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                          {coverage.issues.map((issue, idx) => (
                            <div
                              key={idx}
                              style={{
                                padding: "0.75rem 1rem",
                                borderRadius: "var(--radius-sm)",
                                backgroundColor: "var(--color-status-warning-subtle, #fffbeb)",
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                                fontSize: "0.875rem"
                              }}
                            >
                              <div>
                                <span style={{ fontWeight: 600 }}>{issue.message}</span>
                                {issue.businessReference && (
                                  <span style={{ marginInlineStart: "0.5rem", color: "var(--color-content-muted)" }}>
                                    ({issue.businessReference})
                                  </span>
                                )}
                              </div>
                              <span style={{ fontSize: "0.75rem", fontFamily: "var(--font-mono, monospace)", color: "var(--color-content-muted)" }}>
                                {issue.code}
                              </span>
                            </div>
                          ))}
                        </div>
                      </div>
                    </Surface>
                  )}
                </>
              )}
            </div>
          )}

          {/* TAB: REGISTRATIONS */}
          {activeTab === "registrations" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <div>
                  <h2 style={{ margin: "0 0 0.25rem", fontSize: "1.125rem", fontWeight: 600 }}>
                    {t("registrations.sectionTitle")}
                  </h2>
                  <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
                    {t("registrations.sectionDescription")}
                  </p>
                </div>
                {permissions.canManageRegistrations && (
                  <Button
                    variant="primary"
                    size="compact"
                    onClick={() => { setSelectedRegistration(null); setRegistrationModalMode("create"); setIsRegistrationModalOpen(true); }}
                  >
                    + {t("registrations.createAction")}
                  </Button>
                )}
              </div>

              {isRegLoading ? (
                <div style={{ display: "flex", justifyContent: "center", padding: "3rem" }}>
                  <Spinner size="large" />
                </div>
              ) : !registrations || registrations.length === 0 ? (
                <Surface variant="card">
                  <div style={{ padding: "2.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("registrations.noRegistrations")}
                  </div>
                </Surface>
              ) : (
                <Surface variant="card">
                  <TaxDataTable
                    data={registrations}
                    columns={registrationColumns}
                    rowKey={(item) => item.id}
                  />
                </Surface>
              )}
            </div>
          )}

          {/* TAB: PRODUCT PROFILES */}
          {activeTab === "productProfiles" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <div>
                  <h2 style={{ margin: "0 0 0.25rem", fontSize: "1.125rem", fontWeight: 600 }}>
                    {t("productProfiles.sectionTitle")}
                  </h2>
                  <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
                    {t("productProfiles.sectionDescription")}
                  </p>
                </div>
                {permissions.canManageProductProfiles && (
                  <Button
                    variant="primary"
                    size="compact"
                    onClick={() => { setSelectedProductProfile(null); setIsProductModalOpen(true); }}
                  >
                    + {t("productProfiles.createAction")}
                  </Button>
                )}
              </div>

              {isProductLoading ? (
                <div style={{ display: "flex", justifyContent: "center", padding: "3rem" }}>
                  <Spinner size="large" />
                </div>
              ) : !productProfiles || productProfiles.length === 0 ? (
                <Surface variant="card">
                  <div style={{ padding: "2.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("productProfiles.noProfiles")}
                  </div>
                </Surface>
              ) : (
                <Surface variant="card">
                  <TaxDataTable
                    data={productProfiles}
                    columns={productColumns}
                    rowKey={(item) => item.id}
                  />
                </Surface>
              )}
            </div>
          )}

          {/* TAB: PARTY PROFILES */}
          {activeTab === "partyProfiles" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <div>
                  <h2 style={{ margin: "0 0 0.25rem", fontSize: "1.125rem", fontWeight: 600 }}>
                    {t("partyProfiles.sectionTitle")}
                  </h2>
                  <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
                    {t("partyProfiles.sectionDescription")}
                  </p>
                </div>
                {permissions.canManagePartyProfiles && (
                  <Button
                    variant="primary"
                    size="compact"
                    onClick={() => { setSelectedPartyProfile(null); setIsPartyModalOpen(true); }}
                  >
                    + {t("partyProfiles.createAction")}
                  </Button>
                )}
              </div>

              {isPartyLoading ? (
                <div style={{ display: "flex", justifyContent: "center", padding: "3rem" }}>
                  <Spinner size="large" />
                </div>
              ) : !partyProfiles || partyProfiles.length === 0 ? (
                <Surface variant="card">
                  <div style={{ padding: "2.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("partyProfiles.noProfiles")}
                  </div>
                </Surface>
              ) : (
                <Surface variant="card">
                  <TaxDataTable
                    data={partyProfiles}
                    columns={partyColumns}
                    rowKey={(item) => item.id}
                  />
                </Surface>
              )}
            </div>
          )}

          {/* TAB: RULES & POLICIES */}
          {activeTab === "rules" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <div>
                  <h2 style={{ margin: "0 0 0.25rem", fontSize: "1.125rem", fontWeight: 600 }}>
                    {t("rules.sectionTitle")}
                  </h2>
                  <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
                    {t("rules.sectionDescription")}
                  </p>
                </div>
                {permissions.canManageRules && (
                  <Button
                    variant="primary"
                    size="compact"
                    onClick={() => setIsRuleSetModalOpen(true)}
                  >
                    + {t("rules.createSetAction")}
                  </Button>
                )}
              </div>

              {isRuleSetsLoading ? (
                <div style={{ display: "flex", justifyContent: "center", padding: "3rem" }}>
                  <Spinner size="large" />
                </div>
              ) : !ruleSets || ruleSets.length === 0 ? (
                <Surface variant="card">
                  <div style={{ padding: "2.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("rules.noRuleSets")}
                  </div>
                </Surface>
              ) : (
                <Surface variant="card">
                  <TaxDataTable
                    data={ruleSets}
                    columns={ruleSetColumns}
                    rowKey={(item) => item.id}
                  />
                </Surface>
              )}
            </div>
          )}

          {/* TAB: COUNTRY PACKS */}
          {activeTab === "countryPacks" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div>
                <h2 style={{ margin: "0 0 0.25rem", fontSize: "1.125rem", fontWeight: 600 }}>
                  {t("countryPack.sectionTitle")}
                </h2>
                <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
                  {t("countryPack.sectionDescription")}
                </p>
              </div>

              <div
                role="note"
                style={{
                  padding: "0.875rem 1rem",
                  borderRadius: "var(--radius-md)",
                  backgroundColor: "var(--color-status-warning-subtle, #fffbeb)",
                  border: "1px solid var(--color-status-warning-border, #fef3c7)",
                  color: "var(--color-status-warning-text, #92400e)",
                  fontSize: "0.8125rem",
                  lineHeight: 1.5
                }}
              >
                <strong>{t("countryPack.statutoryNoticeTitle")}: </strong>
                <span>{t("countryPack.statutoryNoticeDescription")}</span>
              </div>

              {isPacksLoading ? (
                <div style={{ display: "flex", justifyContent: "center", padding: "3rem" }}>
                  <Spinner size="large" />
                </div>
              ) : !countryPacks || countryPacks.length === 0 ? (
                <Surface variant="card">
                  <div style={{ padding: "2.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("countryPack.noPacks")}
                  </div>
                </Surface>
              ) : (
                <Surface variant="card">
                  <TaxDataTable
                    data={countryPacks}
                    columns={packColumns}
                    rowKey={(item) => item.id}
                  />
                </Surface>
              )}
            </div>
          )}

          {/* TAB: SIMULATOR */}
          {activeTab === "simulator" && (
            <TaxSimulatorWidget
              legalEntityId={legalEntityId}
              defaultDate={today}
              defaultCurrency={refData?.policies?.[0]?.policyCode === "DEVELOPMENT" ? "EGP" : "EGP"}
            />
          )}
        </>
      )}

      {/* MODALS */}
      <TaxCapabilityModal
        isOpen={isCapabilityModalOpen}
        onClose={() => setIsCapabilityModalOpen(false)}
        legalEntityId={legalEntityId}
        currentCapability={capabilityDto?.capability || "Disabled"}
        defaultEffectiveDate={today}
      />

      <RegistrationModal
        isOpen={isRegistrationModalOpen}
        onClose={() => setIsRegistrationModalOpen(false)}
        legalEntityId={legalEntityId}
        registration={selectedRegistration}
        mode={registrationModalMode}
        defaultCountryCode={countryCode}
        defaultEffectiveDate={today}
      />

      {selectedRegistration && (
        <RegistrationHistoryModal
          isOpen={isRegHistoryModalOpen}
          onClose={() => setIsRegHistoryModalOpen(false)}
          registrationId={selectedRegistration.id}
          legalEntityId={legalEntityId}
        />
      )}

      {selectedRegistration && (
        <RegistrationMembersModal
          isOpen={isRegMembersModalOpen}
          onClose={() => setIsRegMembersModalOpen(false)}
          registrationId={selectedRegistration.id}
          legalEntityId={legalEntityId}
          defaultEffectiveDate={today}
          canManage={permissions.canManageRegistrations}
        />
      )}

      <ProductProfileModal
        isOpen={isProductModalOpen}
        onClose={() => setIsProductModalOpen(false)}
        legalEntityId={legalEntityId}
        profile={selectedProductProfile}
        categories={refData?.categories || []}
        defaultEffectiveDate={today}
      />

      <PartyProfileModal
        isOpen={isPartyModalOpen}
        onClose={() => setIsPartyModalOpen(false)}
        legalEntityId={legalEntityId}
        profile={selectedPartyProfile}
        categories={refData?.categories || []}
        defaultEffectiveDate={today}
      />

      <RuleSetModal
        isOpen={isRuleSetModalOpen}
        onClose={() => setIsRuleSetModalOpen(false)}
        legalEntityId={legalEntityId}
        defaultCountryCode={countryCode}
        defaultEffectiveDate={today}
      />

      {selectedRuleSet && (
        <RuleModal
          isOpen={isRuleModalOpen}
          onClose={() => setIsRuleModalOpen(false)}
          ruleSetId={selectedRuleSet.id}
          legalEntityId={legalEntityId}
          categories={refData?.categories || []}
          defaultEffectiveDate={today}
        />
      )}

      {selectedRuleSet && (
        <RuleSetValidateDialog
          isOpen={isValidateDialogOpen}
          onClose={() => setIsValidateDialogOpen(false)}
          ruleSetId={selectedRuleSet.id}
          legalEntityId={legalEntityId}
        />
      )}

      {selectedRuleSet && (
        <RuleSetActivateDialog
          isOpen={isActivateDialogOpen}
          onClose={() => setIsActivateDialogOpen(false)}
          ruleSet={selectedRuleSet}
          legalEntityId={legalEntityId}
        />
      )}

      {selectedCountryPack && (
        <CountryPackActionDialog
          isOpen={isPackActionDialogOpen}
          onClose={() => setIsPackActionDialogOpen(false)}
          pack={selectedCountryPack}
          action={packAction}
          defaultEffectiveDate={today}
        />
      )}

      <TaxImportExportModal
        isOpen={isImportExportModalOpen}
        onClose={() => setIsImportExportModalOpen(false)}
        legalEntityId={legalEntityId}
        defaultEffectiveDate={today}
      />
    </div>
  );
}
