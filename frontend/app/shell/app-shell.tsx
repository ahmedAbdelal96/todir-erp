import { Component, lazy, Suspense, type ErrorInfo, type ReactNode, useEffect, useMemo, useState } from "react";
import { MoneyField } from "../../components/fields/money-field.tsx";
import { MoneyDisplay } from "../../components/fields/money-display.tsx";
import { DataGrid, type DataGridColumn } from "../../components/grid/index.ts";
import { ComboBox } from "../../components/primitives/combobox.tsx";
import { Button } from "../../components/primitives/button.tsx";
import { Dialog } from "../../components/primitives/dialog.tsx";
import { PageHeader } from "../../components/pages/page-header.tsx";
import { UserMenu } from "./user-menu.tsx";
import {
  HomeIcon,
  UsersIcon,
  DomainIcon,
  DirectoryIcon,
  CatalogIcon,
  InvoicingIcon,
  PaymentsIcon,
  AccountingIcon,
  MigrationIcon,
  FinBaseIcon,
  SettingsIcon,
  BuildingIcon,
  GlobeIcon,
  SunMoonIcon,
  DensityIcon,
  LogOutIcon,
  ShieldCheckIcon
} from "../../components/primitives/icons.tsx";
import { AuthView } from "../auth/index.ts";
import { requestCsrf, logoutUser } from "../../core/api/auth-service.ts";

const PartyListPage = lazy(() => import("../../modules/directory/pages/party-list-page.tsx").then(m => ({ default: m.PartyListPage })));
const PartyRecordPage = lazy(() => import("../../modules/directory/pages/party-record-page.tsx").then(m => ({ default: m.PartyRecordPage })));
const ProductListPage = lazy(() => import("../../modules/catalog/pages/product-list-page.tsx").then(m => ({ default: m.ProductListPage })));
const ProductRecordPage = lazy(() => import("../../modules/catalog/pages/product-record-page.tsx").then(m => ({ default: m.ProductRecordPage })));
const InvoiceDocumentPage = lazy(() => import("../../modules/invoicing/pages/invoice-document-page.tsx").then(m => ({ default: m.InvoiceDocumentPage })));
const PaymentEntryDialog = lazy(() => import("../../modules/payments/features/payment-entry-dialog.tsx").then(m => ({ default: m.PaymentEntryDialog })));
const ManualJournalPage = lazy(() => import("../../modules/accounting/pages/manual-journal-page.tsx").then(m => ({ default: m.ManualJournalPage })));
const ImportWizardPage = lazy(() => import("../../modules/migration/pages/import-wizard-page.tsx").then(m => ({ default: m.ImportWizardPage })));
const FinBasePage = lazy(() => import("../../modules/finbase/pages/finbase-page.tsx").then(m => ({ default: m.FinBasePage })));
const UserListPage = lazy(() => import("../../modules/identity/pages/user-list-page.tsx").then(m => ({ default: m.UserListPage })));
const UserRecordPage = lazy(() => import("../../modules/identity/pages/user-record-page.tsx").then(m => ({ default: m.UserRecordPage })));
const LoginDomainSettingsPage = lazy(() => import("../../modules/identity/pages/login-domain-settings-page.tsx").then(m => ({ default: m.LoginDomainSettingsPage })));

type Language = "ar" | "en";
type Screen = "loading" | "signed-out" | "authenticated" | "forbidden" | "error";
type ModuleRegistration = { id: string; label: string; permission?: string | undefined };
type Session = {
  authenticated: boolean;
  tenantId: string;
  memberships?: Array<{ id: string; tenantId: string; displayName: string }> | undefined;
  context: { legalEntities: Array<{ id: string; code: string; name: string }>; branches: Array<{ id: string; legalEntityId: string; code: string; name: string }> };
};

const fallbackModules: ModuleRegistration[] = [
  { id: "home", label: "الرئيسية" },
  { id: "users", label: "المستخدمون", permission: "platform.identity.read" },
  { id: "domain-settings", label: "إعدادات تسجيل الدخول", permission: "platform.tenancy.read" },
  { id: "directory", label: "دليل الأطراف", permission: "directory.party.read" },
  { id: "catalog", label: "الكتالوج", permission: "catalog.product.read" },
  { id: "invoicing", label: "فواتير المبيعات", permission: "invoicing.invoices.read" },
  { id: "payments", label: "المدفوعات", permission: "payments.transactions.read" },
  { id: "accounting", label: "المحاسبة العامة", permission: "accounting.journals.read" },
  { id: "migration", label: "معالج الاستيراد", permission: "platform.migration.execute" },
  { id: "finbase", label: "الأساس المالي", permission: "finbase.currency.read" },
  { id: "preview", label: "معاينة النظام" },
  { id: "settings", label: "الإعدادات", permission: "platform.settings.read" },
];

const moduleLabels: Record<string, { ar: string; en: string; permission?: string }> = {
  home: { ar: "الرئيسية", en: "Home" },
  users: { ar: "المستخدمون", en: "Users", permission: "platform.identity.read" },
  "domain-settings": { ar: "إعدادات تسجيل الدخول", en: "Login Domain", permission: "platform.tenancy.read" },
  directory: { ar: "دليل الأطراف", en: "Directory", permission: "directory.party.read" },
  catalog: { ar: "الكتالوج", en: "Catalog", permission: "catalog.product.read" },
  invoicing: { ar: "فواتير المبيعات", en: "Invoices", permission: "invoicing.invoices.read" },
  payments: { ar: "المدفوعات", en: "Payments", permission: "payments.transactions.read" },
  accounting: { ar: "المحاسبة العامة", en: "Accounting", permission: "accounting.journals.read" },
  migration: { ar: "معالج الاستيراد", en: "Import Wizard", permission: "platform.migration.execute" },
  finbase: { ar: "الأساس المالي", en: "Financial foundation", permission: "finbase.currency.read" },
  preview: { ar: "معاينة النظام", en: "System Preview" },
  tenancy: { ar: "إعدادات الشركة", en: "Company", permission: "platform.tenancy.read" },
  iam: { ar: "المستخدمون", en: "Users", permission: "platform.identity.read" },
  org: { ar: "الهيكل التنظيمي", en: "Organization", permission: "platform.organization.read" },
  settings: { ar: "الإعدادات", en: "Settings", permission: "platform.settings.read" },
};

function getModuleIcon(id: string): React.JSX.Element {
  switch (id) {
    case "home":
      return <HomeIcon size={18} />;
    case "users":
    case "iam":
      return <UsersIcon size={18} />;
    case "domain-settings":
    case "tenancy":
      return <DomainIcon size={18} />;
    case "directory":
      return <DirectoryIcon size={18} />;
    case "catalog":
      return <CatalogIcon size={18} />;
    case "invoicing":
      return <InvoicingIcon size={18} />;
    case "payments":
      return <PaymentsIcon size={18} />;
    case "accounting":
      return <AccountingIcon size={18} />;
    case "migration":
      return <MigrationIcon size={18} />;
    case "finbase":
      return <FinBaseIcon size={18} />;
    case "preview":
      return <ShieldCheckIcon size={18} />;
    case "settings":
      return <SettingsIcon size={18} />;
    default:
      return <BuildingIcon size={18} />;
  }
}

export function AppShell() {
  const [language, setLanguage] = useState<Language>("ar");
  const [screen, setScreen] = useState<Screen>("loading");
  const [companyName, setCompanyName] = useState("شركة الأفق للتجارة");
  const [legalEntities, setLegalEntities] = useState<Session["context"]["legalEntities"]>([]);
  const [branches, setBranches] = useState<Session["context"]["branches"]>([]);
  const [legalEntity, setLegalEntity] = useState("");
  const [branch, setBranch] = useState("");
  const [modules, setModules] = useState<ModuleRegistration[]>(fallbackModules);
  const [contextError, setContextError] = useState<string | undefined>();
  const [operation, setOperation] = useState<string | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [currentView, setCurrentView] = useState<string>(() => {
    if (typeof window !== "undefined" && window.location.hash) {
      const hash = window.location.hash.replace("#", "");
      if (hash) return hash;
    }
    return "home";
  });
  const [selectedPartyId, setSelectedPartyId] = useState<string>("party-001");
  const [selectedProductId, setSelectedProductId] = useState<string>("");
  const [selectedUserId, setSelectedUserId] = useState<string>("");
  const [theme, setTheme] = useState<string>("default");
  const [density, setDensity] = useState<"comfortable" | "compact">("comfortable");

  const direction = language === "ar" ? "rtl" : "ltr";
  const labels = useMemo(() => language === "ar" ? {
    title: "مساحة العمل", entity: "الكيان القانوني", branch: "الفرع", language: "English", empty: "اختر مساحة عمل للبدء", loading: "جارٍ التحميل...", progress: "جارٍ تحديث السياق", signOut: "تسجيل الخروج",
  } : {
    title: "Workspace", entity: "Legal entity", branch: "Branch", language: "العربية", empty: "Choose a workspace to begin", loading: "Loading...", progress: "Updating context", signOut: "Sign out",
  }, [language]);

  useEffect(() => {
    const handleHash = () => {
      const hash = window.location.hash.replace("#", "");
      setCurrentView(hash || "home");
    };
    window.addEventListener("hashchange", handleHash);
    handleHash();
    return () => window.removeEventListener("hashchange", handleHash);
  }, []);

  async function loadSession() {
    setScreen("loading");
    try {
      const response = await fetch("/api/v1/session/context", { headers: { "X-Correlation-Id": crypto.randomUUID() } });
      if (response.status === 401) { setScreen("signed-out"); return; }
      if (response.status === 403) { setScreen("forbidden"); return; }
      if (!response.ok) throw new Error("session.load_failed");
      const session = await response.json() as Session;
      if (session.memberships?.[0]?.displayName) {
        setCompanyName(session.memberships[0].displayName);
      }
      setLegalEntities(session.context.legalEntities);
      setBranches(session.context.branches);
      setLegalEntity((current) => session.context.legalEntities.some(item => item.id === current) ? current : "");
      setBranch((current) => session.context.branches.some(item => item.id === current) ? current : "");
      await loadNavigation();
      setScreen("authenticated");
    } catch {
      setError(language === "ar" ? "تعذر تحميل الجلسة" : "Could not load the session");
      setScreen("error");
    }
  }

  async function loadNavigation() {
    try {
      const response = await fetch("/api/v1/modules", { headers: { "X-Correlation-Id": crypto.randomUUID() } });
      if (!response.ok) throw new Error("modules.load_failed");
      const registered = await response.json() as Array<{ id?: string }>;
      const candidates: ModuleRegistration[] = registered.flatMap(item => {
        const definition = moduleLabels[item.id ?? ""];
        return definition && item.id ? [{ id: item.id, label: definition[language], permission: definition.permission }] : [];
      });
      const permitted: ModuleRegistration[] = (await Promise.all(candidates.filter(item => item.permission).map(async item => {
        const permissionResponse = await fetch(`/api/v1/authorization/check?permission=${encodeURIComponent(item.permission!)}`, { headers: { "X-Correlation-Id": crypto.randomUUID() } });
        return permissionResponse.ok ? item : null;
      }))).filter((item): item is ModuleRegistration => item !== null);
      const home = candidates.find(item => item.id === "home") ?? fallbackModules[0]!;
      setModules([home, ...permitted]);
    } catch {
      setModules(fallbackModules.map(m => ({ ...m, label: moduleLabels[m.id]?.[language] ?? m.label })));
    }
  }

  useEffect(() => { void loadSession(); }, []);

  async function switchOrganization(nextLegalEntity: string, nextBranch: string) {
    setOperation(labels.progress);
    setContextError(undefined);
    try {
      const csrf = await requestCsrf();
      const response = await fetch("/api/v1/context/organization", { method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf, "X-Correlation-Id": crypto.randomUUID() }, body: JSON.stringify({ legalEntityId: nextLegalEntity || null, branchId: nextBranch || null }) });
      if (response.status === 401) { setScreen("signed-out"); return; }
      if (response.status === 403) { setScreen("forbidden"); return; }
      if (!response.ok) throw new Error("context.organization_update_failed");
      setLegalEntity(nextLegalEntity);
      setBranch(nextBranch);
      await loadNavigation();
    } catch {
      setContextError(language === "ar" ? "تعذر تحديث النطاق التنظيمي" : "Could not update the organization scope");
    } finally {
      setOperation(undefined);
    }
  }

  async function signOut() {
    await logoutUser();
    setScreen("signed-out");
  }

  if (screen === "loading") return <StatusView direction={direction} message={labels.loading} />;
  if (screen === "signed-out") return <AuthView language={language} onAuthenticated={() => void loadSession()} />;
  if (screen === "forbidden") return <AccessView direction={direction} kind="forbidden" language={language} onRetry={() => void loadSession()} />;
  if (screen === "error") return <AccessView direction={direction} kind="error" language={language} message={error ?? undefined} onRetry={() => void loadSession()} />;

  return (
    <div className="todir-app-shell shell" dir={direction} data-theme={theme} data-density={density}>
      <header className="topbar">
        {/* Left Side (in LTR) / Right Side (in RTL): Brand & Company */}
        <div style={{ display: "flex", alignItems: "center", gap: "0.875rem" }}>
          <a className="brand" href="/" aria-label="TODIR" onClick={(e) => { e.preventDefault(); setCurrentView("home"); window.location.hash = ""; }}>
            <span className="brand-icon" style={{ display: "inline-flex", alignItems: "center", justifyContent: "center", width: "32px", height: "32px", borderRadius: "var(--radius-md, 0.5rem)", background: "linear-gradient(135deg, var(--color-brand-primary, #4f46e5) 0%, var(--color-brand-primary-hover, #4338ca) 100%)", color: "#ffffff", fontWeight: 900, fontSize: "1rem", boxShadow: "0 2px 4px rgba(79, 70, 229, 0.25)" }}>
              T
            </span>
            <span style={{ fontSize: "1.125rem", fontWeight: 800, letterSpacing: "0.02em", color: "var(--color-content-primary, #0f172a)" }}>TODIR</span>
          </a>

          <span style={{ color: "var(--color-border-strong, #cbd5e1)" }}>|</span>

          <span
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: "0.375rem",
              padding: "0.25rem 0.625rem",
              borderRadius: "var(--radius-full, 9999px)",
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              fontSize: "0.8125rem",
              fontWeight: 600,
              color: "var(--color-content-secondary, #475569)",
              border: "1px solid var(--color-border-subtle, #f1f5f9)"
            }}
          >
            <BuildingIcon size={14} style={{ color: "var(--color-brand-primary, #4f46e5)" }} />
            {companyName}
          </span>
        </div>

        {/* Center / Operational Context (Legal Entity & Branch) */}
        <div className="context-controls">
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "0.375rem",
              backgroundColor: "var(--color-surface-subtle, #f1f5f9)",
              padding: "0.25rem 0.5rem",
              borderRadius: "var(--radius-md, 0.375rem)",
              border: "1px solid var(--color-border-default, #e2e8f0)"
            }}
          >
            <label htmlFor="entity-select" style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
              {labels.entity}
            </label>
            <select
              id="entity-select"
              aria-label={labels.entity}
              value={legalEntity}
              onChange={(event) => void switchOrganization(event.target.value, "")}
              style={{
                background: "transparent",
                border: "none",
                fontSize: "0.8125rem",
                fontWeight: 600,
                color: "var(--color-content-primary)",
                cursor: "pointer",
                outline: "none"
              }}
            >
              <option value="">—</option>
              {legalEntities.map(item => <option key={item.id} value={item.id}>{item.code} — {item.name}</option>)}
            </select>

            <span style={{ color: "var(--color-border-strong, #cbd5e1)" }}>|</span>

            <label htmlFor="branch-select" style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
              {labels.branch}
            </label>
            <select
              id="branch-select"
              aria-label={labels.branch}
              value={branch}
              onChange={(event) => void switchOrganization(legalEntity, event.target.value)}
              style={{
                background: "transparent",
                border: "none",
                fontSize: "0.8125rem",
                fontWeight: 600,
                color: "var(--color-content-primary)",
                cursor: "pointer",
                outline: "none"
              }}
            >
              <option value="">—</option>
              {branches.filter(item => !legalEntity || item.legalEntityId === legalEntity).map(item => <option key={item.id} value={item.id}>{item.code} — {item.name}</option>)}
            </select>
          </div>

          {/* Clean Language quick toggle */}
          <button
            type="button"
            onClick={() => setLanguage(language === "ar" ? "en" : "ar")}
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: "0.375rem",
              padding: "0.3125rem 0.625rem",
              fontSize: "0.8125rem",
              fontWeight: 600,
              borderRadius: "var(--radius-md, 0.375rem)",
              border: "1px solid var(--color-border-default, #e2e8f0)",
              backgroundColor: "var(--color-surface-card, #ffffff)",
              color: "var(--color-content-secondary, #475569)",
              cursor: "pointer",
              transition: "all 150ms ease"
            }}
          >
            <GlobeIcon size={14} />
            <span>{labels.language}</span>
          </button>

          {/* Consolidated User Menu */}
          <UserMenu
            userName="مسؤول النظام"
            userEmail="admin@atlas.com"
            language={language}
            onToggleLanguage={() => setLanguage(language === "ar" ? "en" : "ar")}
            theme={theme}
            onToggleTheme={() => setTheme(theme === "default" ? "sapphire-modern" : "default")}
            density={density}
            onToggleDensity={() => setDensity(density === "comfortable" ? "compact" : "comfortable")}
            onSignOut={() => void signOut()}
          />
        </div>
      </header>

      {operation ? <div className="operation-progress" role="status" aria-live="polite">{operation}</div> : null}

      <div className="layout">
        <aside className="sidebar" aria-label={language === "ar" ? "التنقل الرئيسي" : "Main navigation"}>
          <div style={{ padding: "0.25rem 0.5rem 0.75rem", fontSize: "0.6875rem", fontWeight: 700, letterSpacing: "0.08em", textTransform: "uppercase", color: "var(--color-content-muted, #94a3b8)" }}>
            {language === "ar" ? "الوحدات والخدمات" : "Modules & Apps"}
          </div>
          <nav>
            {modules.map(item => (
              <a
                key={item.id}
                href={`#${item.id}`}
                className={currentView === item.id ? "active" : undefined}
                onClick={() => setCurrentView(item.id)}
              >
                <span style={{ display: "inline-flex", alignItems: "center", color: currentView === item.id ? "var(--color-action-primary)" : "var(--color-content-muted)" }}>
                  {getModuleIcon(item.id)}
                </span>
                <span>{item.label}</span>
              </a>
            ))}
          </nav>
        </aside>

        <main className="content" aria-live="polite">
          {contextError ? <div className="error" role="alert">{contextError}</div> : null}
          <Suspense fallback={<div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>جارٍ التحميل...</div>}>
            {currentView === "users" || currentView === "iam" ? (
              <UserListPage
                onSelectUser={(user) => {
                  setSelectedUserId(user.id);
                  setCurrentView("user-record");
                }}
              />
            ) : currentView === "user-record" ? (
              <UserRecordPage
                userId={selectedUserId}
                onBack={() => setCurrentView("users")}
              />
            ) : currentView === "domain-settings" ? (
              <LoginDomainSettingsPage />
            ) : currentView === "directory" ? (
              <PartyListPage
                onSelectParty={(party) => {
                  setSelectedPartyId(party.id);
                  setCurrentView("party-record");
                }}
              />
            ) : currentView === "catalog" ? (
              <ProductListPage onSelectProduct={(product) => { setSelectedProductId(product.id); setCurrentView("catalog-record"); }} />
            ) : currentView === "catalog-record" ? (
              <ProductRecordPage productId={selectedProductId} onBack={() => setCurrentView("catalog")} />
            ) : currentView === "party-record" ? (
              <PartyRecordPage
                partyId={selectedPartyId}
                onBack={() => setCurrentView("directory")}
              />
            ) : currentView === "invoicing" ? (
              <InvoiceDocumentPage onBack={() => setCurrentView("home")} />
            ) : currentView === "payments" ? (
              <div style={{ padding: "1.5rem" }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1.5rem" }}>
                  <div>
                    <h1 style={{ margin: 0, fontSize: "1.5rem" }}>المدفوعات والمقبوضات</h1>
                    <p style={{ margin: "0.25rem 0 0 0", color: "var(--color-content-muted)" }}>إدارة العمليات المالية والتحويلات المصرفية</p>
                  </div>
                </div>
                <PaymentSection />
              </div>
            ) : currentView === "accounting" ? (
              <ManualJournalPage onBack={() => setCurrentView("home")} />
            ) : currentView === "migration" ? (
              <ImportWizardPage onFinish={() => setCurrentView("directory")} />
            ) : currentView === "finbase" ? (
              <FinBasePage />
            ) : currentView === "preview" ? (
              <ShowcasePreview density={density} />
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
                <PageHeader
                  title={labels.title}
                  subtitle={
                    language === "ar"
                      ? "نظام تخطيط وإدارة موارد المؤسسة المالي والتشغيلي الموحد."
                      : "Unified financial and operational enterprise resource planning system."
                  }
                  status={
                    <span
                      style={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: "0.375rem",
                        padding: "0.25rem 0.625rem",
                        borderRadius: "var(--radius-full, 9999px)",
                        backgroundColor: "var(--color-surface-subtle, #f1f5f9)",
                        fontSize: "0.75rem",
                        fontWeight: 700,
                        color: "var(--color-action-primary, #0f766e)",
                        border: "1px solid var(--color-border-default, #e2e8f0)"
                      }}
                    >
                      <BuildingIcon size={12} />
                      {companyName}
                    </span>
                  }
                />

                <div>
                  <h2 style={{ fontSize: "1.0625rem", fontWeight: 700, margin: "0 0 1rem 0", color: "var(--color-content-primary, #0f172a)" }}>
                    {language === "ar" ? "الوصول السريع للوحدات" : "Quick Module Launchpad"}
                  </h2>
                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
                      gap: "1rem"
                    }}
                  >
                    <button
                      type="button"
                      onClick={() => setCurrentView("users")}
                      style={launchpadCardStyle}
                      onMouseEnter={handleCardHover}
                      onMouseLeave={handleCardLeave}
                    >
                      <div style={cardIconContainerStyle}>
                        <UsersIcon size={22} />
                      </div>
                      <div style={{ flex: 1, textAlign: "start" }}>
                        <h3 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                          المستخدمون
                        </h3>
                        <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                          إدارة حسابات الفريق والصلاحيات
                        </p>
                      </div>
                    </button>

                    <button
                      type="button"
                      onClick={() => setCurrentView("directory")}
                      style={launchpadCardStyle}
                      onMouseEnter={handleCardHover}
                      onMouseLeave={handleCardLeave}
                    >
                      <div style={cardIconContainerStyle}>
                        <DirectoryIcon size={22} />
                      </div>
                      <div style={{ flex: 1, textAlign: "start" }}>
                        <h3 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                          دليل الأطراف
                        </h3>
                        <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                          العملاء والموردون وجهات الاتصال
                        </p>
                      </div>
                    </button>

                    <button
                      type="button"
                      onClick={() => setCurrentView("invoicing")}
                      style={launchpadCardStyle}
                      onMouseEnter={handleCardHover}
                      onMouseLeave={handleCardLeave}
                    >
                      <div style={cardIconContainerStyle}>
                        <InvoicingIcon size={22} />
                      </div>
                      <div style={{ flex: 1, textAlign: "start" }}>
                        <h3 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                          فواتير المبيعات
                        </h3>
                        <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                          الفواتير الضريبية والمطالبات
                        </p>
                      </div>
                    </button>

                    <button
                      type="button"
                      onClick={() => setCurrentView("accounting")}
                      style={launchpadCardStyle}
                      onMouseEnter={handleCardHover}
                      onMouseLeave={handleCardLeave}
                    >
                      <div style={cardIconContainerStyle}>
                        <AccountingIcon size={22} />
                      </div>
                      <div style={{ flex: 1, textAlign: "start" }}>
                        <h3 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                          قيود اليومية
                        </h3>
                        <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                          المحاسبة العامة ودفتر الأستاذ
                        </p>
                      </div>
                    </button>

                    <button
                      type="button"
                      onClick={() => setCurrentView("migration")}
                      style={launchpadCardStyle}
                      onMouseEnter={handleCardHover}
                      onMouseLeave={handleCardLeave}
                    >
                      <div style={cardIconContainerStyle}>
                        <MigrationIcon size={22} />
                      </div>
                      <div style={{ flex: 1, textAlign: "start" }}>
                        <h3 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                          معالج الاستيراد
                        </h3>
                        <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                          استيراد البيانات وتجهيز النظام
                        </p>
                      </div>
                    </button>
                  </div>
                </div>
              </div>
            )}
          </Suspense>
        </main>
      </div>
    </div>
  );
}

const launchpadCardStyle: React.CSSProperties = {
  backgroundColor: "var(--color-surface-card, #ffffff)",
  border: "1px solid var(--color-border-default, #e2e8f0)",
  borderRadius: "var(--radius-lg, 0.75rem)",
  padding: "1.25rem",
  display: "flex",
  alignItems: "center",
  gap: "1rem",
  cursor: "pointer",
  outline: "none",
  transition: "all 200ms cubic-bezier(0.4, 0, 0.2, 1)",
  boxShadow: "var(--shadow-xs, 0 1px 2px 0 rgba(0, 0, 0, 0.05))"
};

const cardIconContainerStyle: React.CSSProperties = {
  padding: "0.75rem",
  borderRadius: "var(--radius-md, 0.5rem)",
  backgroundColor: "var(--color-brand-primary-subtle, #eef2ff)",
  color: "var(--color-brand-primary, #4f46e5)",
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  flexShrink: 0,
  transition: "transform 200ms ease"
};

function handleCardHover(e: React.MouseEvent<HTMLButtonElement>) {
  e.currentTarget.style.borderColor = "var(--color-brand-primary, #4f46e5)";
  e.currentTarget.style.boxShadow = "var(--shadow-md, 0 4px 6px -1px rgba(0, 0, 0, 0.1))";
  e.currentTarget.style.transform = "translateY(-2px)";
}

function handleCardLeave(e: React.MouseEvent<HTMLButtonElement>) {
  e.currentTarget.style.borderColor = "var(--color-border-default, #e2e8f0)";
  e.currentTarget.style.boxShadow = "var(--shadow-xs, 0 1px 2px 0 rgba(0, 0, 0, 0.05))";
  e.currentTarget.style.transform = "translateY(0)";
}

function StatusView({ direction, message }: { direction: string; message: string }) {
  return (
    <div dir={direction} style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh" }}>
      <p role="status">{message}</p>
    </div>
  );
}

function AccessView({ direction, kind, language, message, onRetry }: { direction: string; kind: "forbidden" | "error"; language: Language; message?: string | undefined; onRetry: () => void }) {
  const isArabic = language === "ar";
  const title = kind === "forbidden" ? (isArabic ? "غير مصرح لك بالوصول" : "Access Denied") : (isArabic ? "حدث خطأ غير متوقع" : "An error occurred");
  const fallbackMessage = kind === "forbidden" ? (isArabic ? "لا تملك الصلاحيات الكافية للوصول إلى هذا المورد." : "You do not have permission to access this resource.") : (isArabic ? "تعذر تحميل البيانات المطلوبة." : "Could not load the requested data.");
  return (
    <div dir={direction} className="status-page">
      <div className="auth-card" style={{ textAlign: "center" }}>
        <h2 style={{ margin: "0 0 0.5rem 0", color: kind === "forbidden" ? "var(--color-status-warning)" : "var(--color-status-danger)" }}>{title}</h2>
        <p style={{ margin: "0 0 1.5rem 0", color: "var(--color-content-muted)" }}>{message || fallbackMessage}</p>
        <button type="button" onClick={onRetry} style={{ padding: "0.5rem 1.25rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)", background: "var(--color-surface-subtle)", cursor: "pointer", fontWeight: 600 }}>
          {isArabic ? "إعادة المحاولة" : "Retry"}
        </button>
      </div>
    </div>
  );
}

export class ShellErrorBoundary extends Component<{ children: ReactNode }, { hasError: boolean; error: Error | null }> {
  constructor(props: { children: ReactNode }) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error) {
    return { hasError: true, error };
  }

  override componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error("Uncaught application shell error:", error, errorInfo);
  }

  override render() {
    if (this.state.hasError) {
      return (
        <div style={{ padding: "2rem", textAlign: "center", direction: "rtl", fontFamily: "var(--font-sans)" }}>
          <h2 style={{ color: "var(--color-status-danger)" }}>حدث خطأ غير متوقع في واجهة النظام</h2>
          <p style={{ color: "var(--color-content-muted)" }}>{this.state.error?.message || "يرجى إعادة تحميل الصفحة والمحاولة مرة أخرى."}</p>
          <button
            type="button"
            onClick={() => window.location.reload()}
            style={{ padding: "0.5rem 1rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)", cursor: "pointer" }}
          >
            إعادة تحميل الصفحة
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}

function PaymentSection() {
  const [open, setOpen] = useState(false);
  return (
    <div>
      <Button variant="primary" onPress={() => setOpen(true)}>+ تسجيل دفعة مالية جديدة</Button>
      <PaymentEntryDialog isOpen={open} onOpenChange={setOpen} onPaymentComplete={() => setOpen(false)} />
    </div>
  );
}

function ShowcasePreview({ density }: { density: "comfortable" | "compact" }) {
  const [amount, setAmount] = useState<{ amount: string; currency: string }>({ amount: "12500.50", currency: "SAR" });
  const [selectedCity, setSelectedCity] = useState<string | null>("ruh");
  const [isDialogOpen, setIsDialogOpen] = useState(false);

  const sampleColumns: DataGridColumn<{ id: string; code: string; name: string; balance: string }>[] = [
    { id: "code", header: "كود الحساب", width: 120 },
    { id: "name", header: "اسم الحساب", width: "1fr" },
    { id: "balance", header: "الرصيد الحالي", width: 160, align: "end" }
  ];

  const sampleRows = [
    { id: "1", code: "1010", name: "الصندوق الرئيسي", balance: "25,400.00 ر.س" },
    { id: "2", code: "1020", name: "بنك الراجحي - جاري", balance: "148,920.50 ر.س" },
    { id: "3", code: "1030", name: "البنك الأهلي التجاري", balance: "82,150.00 ر.س" },
    { id: "4", code: "1210", name: "ذمم العملاء المدينة", balance: "63,200.00 ر.س" }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <PageHeader
        title="معاينة عناصر التصميم ومكونات المنظومة"
        subtitle="فحص مباشر للمكونات التفاعلية، الحقول النقدية، وجداول البيانات في بيئة التشغيل الفعلية"
      />

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "1.25rem" }}>
        <div style={{ backgroundColor: "var(--color-surface-card)", padding: "1.25rem", borderRadius: "var(--radius-md)", border: "1px solid var(--color-border-default)" }}>
          <h3 style={{ margin: "0 0 1rem 0", fontSize: "1rem" }}>حقل الإدخال النقدي الدقيق (MoneyField)</h3>
          <MoneyField
            label="المبلغ المالي المستحق"
            value={amount}
            onChange={setAmount}
            description="يدعم الأرقام الدقيقة بدون كسور عائمة"
          />
          <div style={{ marginTop: "1rem", fontSize: "0.875rem" }}>
            <span>القيمة المسجلة: </span>
            <MoneyDisplay value={amount} displayCurrencyCode />
          </div>
        </div>

        <div style={{ backgroundColor: "var(--color-surface-card)", padding: "1.25rem", borderRadius: "var(--radius-md)", border: "1px solid var(--color-border-default)" }}>
          <h3 style={{ margin: "0 0 1rem 0", fontSize: "1rem" }}>قائمة الاختيار والبحث (ComboBox)</h3>
          <ComboBox
            label="المدينة / الفرع"
            placeholder="اختر المدينة..."
            selectedKey={selectedCity}
            onSelectionChange={(k) => setSelectedCity(k as string)}
            options={[
              { id: "ruh", label: "الرياض (المقر الرئيسي)" },
              { id: "jed", label: "جدة (فرع المنطقة الغربية)" },
              { id: "dmm", label: "الدمام (فرع المنطقة الشرقية)" }
            ]}
          />
          <div style={{ marginTop: "1rem" }}>
            <Button variant="secondary" onPress={() => setIsDialogOpen(true)}>فتح نافذة تأكيد تجريبية</Button>
          </div>
        </div>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
        <h3 style={{ margin: 0, fontSize: "1rem" }}>جدول البيانات عالي الكفاءة (DataGrid)</h3>
        <DataGrid
          columns={sampleColumns}
          staticRows={sampleRows}
          density={density}
          enableRtl={true}
        />
      </div>

      <Dialog
        isOpen={isDialogOpen}
        onOpenChange={setIsDialogOpen}
        title="تأكيد العملية المالية"
        description="هل أنت متأكد من رغبتك في إتمام ترحيل القيد المحاسبي؟ هذه العملية غير قابلة للتراجع."
        size="medium"
      >
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1.5rem" }}>
          <Button variant="ghost" onPress={() => setIsDialogOpen(false)}>إلغاء</Button>
          <Button variant="primary" onPress={() => setIsDialogOpen(false)}>تأكيد الترحيل</Button>
        </div>
      </Dialog>
    </div>
  );
}
