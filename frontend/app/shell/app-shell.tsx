import { Component, lazy, Suspense, type ErrorInfo, type ReactNode, useEffect, useMemo, useState } from "react";
import { MoneyField } from "../../components/fields/money-field.tsx";
import { MoneyDisplay } from "../../components/fields/money-display.tsx";
import { DataGrid, type DataGridColumn } from "../../components/grid/index.ts";
import { ComboBox } from "../../components/primitives/combobox.tsx";
import { Button } from "../../components/primitives/button.tsx";
import { Dialog } from "../../components/primitives/dialog.tsx";
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
        <div style={{ display: "flex", alignItems: "center", gap: "0.875rem" }}>
          <a className="brand" href="/" aria-label="TODIR">
            <span style={{ display: "inline-flex", alignItems: "center", justifyContent: "center", width: "32px", height: "32px", borderRadius: "var(--radius-md, 0.375rem)", backgroundColor: "var(--color-action-primary, #0f766e)", color: "#ffffff", fontWeight: 900, fontSize: "1rem" }}>
              T
            </span>
            <span>TODIR</span>
          </a>
          <span
            style={{
              padding: "0.25rem 0.625rem",
              borderRadius: "var(--radius-full, 9999px)",
              backgroundColor: "var(--color-surface-subtle, #f1f5f9)",
              fontSize: "0.8125rem",
              fontWeight: 600,
              color: "var(--color-content-secondary, #475569)",
              border: "1px solid var(--color-border-subtle, #e2e8f0)"
            }}
          >
            {companyName}
          </span>
        </div>

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
            <BuildingIcon size={14} style={{ color: "var(--color-content-muted)" }} />
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

          <div style={{ display: "flex", alignItems: "center", gap: "0.375rem" }}>
            <button
              type="button"
              onClick={() => setTheme(theme === "default" ? "sapphire-modern" : "default")}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.375rem",
                padding: "0.375rem 0.625rem",
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
              <SunMoonIcon size={14} />
              <span>{theme === "default" ? "السمة البديلة" : "السمة الافتراضية"}</span>
            </button>

            <button
              type="button"
              onClick={() => setDensity(density === "comfortable" ? "compact" : "comfortable")}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.375rem",
                padding: "0.375rem 0.625rem",
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
              <DensityIcon size={14} />
              <span>{density === "comfortable" ? "كثافة مدمجة" : "كثافة مريحة"}</span>
            </button>

            <button
              type="button"
              onClick={() => setLanguage(language === "ar" ? "en" : "ar")}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.375rem",
                padding: "0.375rem 0.625rem",
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

            <button
              type="button"
              onClick={() => void signOut()}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.375rem",
                padding: "0.375rem 0.75rem",
                fontSize: "0.8125rem",
                fontWeight: 600,
                borderRadius: "var(--radius-md, 0.375rem)",
                border: "1px solid var(--color-border-default, #e2e8f0)",
                backgroundColor: "var(--color-surface-card, #ffffff)",
                color: "var(--color-status-danger, #e11d48)",
                cursor: "pointer",
                transition: "all 150ms ease"
              }}
            >
              <LogOutIcon size={14} />
              <span>{labels.signOut}</span>
            </button>
          </div>
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
          <Suspense fallback={<div style={{ padding: "2rem", textAlign: "center" }}>جارٍ التحميل...</div>}>
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
              <div style={{ display: "flex", flexDirection: "column", gap: "1.75rem" }}>
                <div
                  style={{
                    backgroundColor: "var(--color-surface-card, #ffffff)",
                    borderRadius: "var(--radius-lg, 0.75rem)",
                    border: "1px solid var(--color-border-default, #e2e8f0)",
                    padding: "2rem",
                    boxShadow: "var(--shadow-sm, 0 1px 2px 0 rgba(0, 0, 0, 0.05))",
                    position: "relative",
                    overflow: "hidden"
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "0.5rem" }}>
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
                        color: "var(--color-action-primary, #0f766e)"
                      }}
                    >
                      <BuildingIcon size={12} />
                      {companyName}
                    </span>
                  </div>
                  <h1
                    style={{
                      margin: "0 0 0.5rem 0",
                      fontSize: "1.75rem",
                      fontWeight: 800,
                      color: "var(--color-content-primary, #0f172a)",
                      letterSpacing: "-0.02em"
                    }}
                  >
                    {labels.title}
                  </h1>
                  <p style={{ margin: 0, fontSize: "0.9375rem", color: "var(--color-content-muted, #64748b)" }}>
                    {language === "ar"
                      ? "نظام تخطيط وإدارة موارد المؤسسة المالي والتشغيلي الموحد."
                      : "Unified financial and operational enterprise resource planning system."}
                  </p>
                </div>

                <div>
                  <h2 style={{ fontSize: "1.125rem", fontWeight: 700, margin: "0 0 1rem 0", color: "var(--color-content-primary, #0f172a)" }}>
                    {language === "ar" ? "الوصول السريع للوحدات" : "Quick Module Launchpad"}
                  </h2>
                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
                      gap: "1rem"
                    }}
                  >
                    <div
                      style={{
                        backgroundColor: "var(--color-surface-card, #ffffff)",
                        border: "1px solid var(--color-border-default, #e2e8f0)",
                        borderRadius: "var(--radius-md, 0.5rem)",
                        padding: "1.25rem",
                        display: "flex",
                        flexDirection: "column",
                        gap: "0.75rem",
                        boxShadow: "var(--shadow-sm, 0 1px 2px 0 rgba(0, 0, 0, 0.05))"
                      }}
                    >
                      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                        <div style={{ padding: "0.5rem", borderRadius: "var(--radius-md, 0.375rem)", backgroundColor: "var(--color-surface-subtle, #f1f5f9)", color: "var(--color-action-primary, #0f766e)" }}>
                          <UsersIcon size={20} />
                        </div>
                        <div>
                          <h3 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700 }}>المستخدمون</h3>
                          <p style={{ margin: "0.125rem 0 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>إدارة حسابات الفريق والصلاحيات</p>
                        </div>
                      </div>
                      <Button variant="secondary" size="compact" onPress={() => setCurrentView("users")} style={{ width: "100%" }}>
                        المستخدمون
                      </Button>
                    </div>

                    <div
                      style={{
                        backgroundColor: "var(--color-surface-card, #ffffff)",
                        border: "1px solid var(--color-border-default, #e2e8f0)",
                        borderRadius: "var(--radius-md, 0.5rem)",
                        padding: "1.25rem",
                        display: "flex",
                        flexDirection: "column",
                        gap: "0.75rem",
                        boxShadow: "var(--shadow-sm, 0 1px 2px 0 rgba(0, 0, 0, 0.05))"
                      }}
                    >
                      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                        <div style={{ padding: "0.5rem", borderRadius: "var(--radius-md, 0.375rem)", backgroundColor: "var(--color-surface-subtle, #f1f5f9)", color: "var(--color-action-primary, #0f766e)" }}>
                          <DirectoryIcon size={20} />
                        </div>
                        <div>
                          <h3 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700 }}>دليل الأطراف</h3>
                          <p style={{ margin: "0.125rem 0 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>العملاء والموردون وجهات الاتصال</p>
                        </div>
                      </div>
                      <Button variant="secondary" size="compact" onPress={() => setCurrentView("directory")} style={{ width: "100%" }}>
                        دليل الأطراف
                      </Button>
                    </div>

                    <div
                      style={{
                        backgroundColor: "var(--color-surface-card, #ffffff)",
                        border: "1px solid var(--color-border-default, #e2e8f0)",
                        borderRadius: "var(--radius-md, 0.5rem)",
                        padding: "1.25rem",
                        display: "flex",
                        flexDirection: "column",
                        gap: "0.75rem",
                        boxShadow: "var(--shadow-sm, 0 1px 2px 0 rgba(0, 0, 0, 0.05))"
                      }}
                    >
                      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                        <div style={{ padding: "0.5rem", borderRadius: "var(--radius-md, 0.375rem)", backgroundColor: "var(--color-surface-subtle, #f1f5f9)", color: "var(--color-action-primary, #0f766e)" }}>
                          <InvoicingIcon size={20} />
                        </div>
                        <div>
                          <h3 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700 }}>فواتير المبيعات</h3>
                          <p style={{ margin: "0.125rem 0 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>الفواتير الضريبية والمطالبات</p>
                        </div>
                      </div>
                      <Button variant="secondary" size="compact" onPress={() => setCurrentView("invoicing")} style={{ width: "100%" }}>
                        فواتير المبيعات
                      </Button>
                    </div>

                    <div
                      style={{
                        backgroundColor: "var(--color-surface-card, #ffffff)",
                        border: "1px solid var(--color-border-default, #e2e8f0)",
                        borderRadius: "var(--radius-md, 0.5rem)",
                        padding: "1.25rem",
                        display: "flex",
                        flexDirection: "column",
                        gap: "0.75rem",
                        boxShadow: "var(--shadow-sm, 0 1px 2px 0 rgba(0, 0, 0, 0.05))"
                      }}
                    >
                      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                        <div style={{ padding: "0.5rem", borderRadius: "var(--radius-md, 0.375rem)", backgroundColor: "var(--color-surface-subtle, #f1f5f9)", color: "var(--color-action-primary, #0f766e)" }}>
                          <AccountingIcon size={20} />
                        </div>
                        <div>
                          <h3 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700 }}>قيود اليومية</h3>
                          <p style={{ margin: "0.125rem 0 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>المحاسبة العامة ودفتر الأستاذ</p>
                        </div>
                      </div>
                      <Button variant="secondary" size="compact" onPress={() => setCurrentView("accounting")} style={{ width: "100%" }}>
                        قيود اليومية
                      </Button>
                    </div>

                    <div
                      style={{
                        backgroundColor: "var(--color-surface-card, #ffffff)",
                        border: "1px solid var(--color-border-default, #e2e8f0)",
                        borderRadius: "var(--radius-md, 0.5rem)",
                        padding: "1.25rem",
                        display: "flex",
                        flexDirection: "column",
                        gap: "0.75rem",
                        boxShadow: "var(--shadow-sm, 0 1px 2px 0 rgba(0, 0, 0, 0.05))"
                      }}
                    >
                      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                        <div style={{ padding: "0.5rem", borderRadius: "var(--radius-md, 0.375rem)", backgroundColor: "var(--color-surface-subtle, #f1f5f9)", color: "var(--color-action-primary, #0f766e)" }}>
                          <MigrationIcon size={20} />
                        </div>
                        <div>
                          <h3 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700 }}>معالج الاستيراد</h3>
                          <p style={{ margin: "0.125rem 0 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>استيراد البيانات وتجهيز النظام</p>
                        </div>
                      </div>
                      <Button variant="secondary" size="compact" onPress={() => setCurrentView("migration")} style={{ width: "100%" }}>
                        معالج الاستيراد
                      </Button>
                    </div>
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

function PaymentSection() {
  const [isOpen, setIsOpen] = useState(false);
  const [latestPayment, setLatestPayment] = useState<any | null>(null);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
      <div style={{ display: "flex", gap: "1rem", alignItems: "center" }}>
        <Button variant="primary" onPress={() => setIsOpen(true)}>
          + تسجيل دفعة مالية جديدة
        </Button>
      </div>

      {latestPayment && (
        <div style={{ padding: "1rem", backgroundColor: "var(--color-surface-success-subtle, #ecfdf5)", color: "var(--color-status-success)", borderRadius: "var(--radius-md)" }}>
          تم تسجيل آخر دفعة بنجاح: {latestPayment.referenceNumber} بمبلغ {latestPayment.amount} {latestPayment.currency}
        </div>
      )}

      <div style={{ backgroundColor: "var(--color-surface-card)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", padding: "1.5rem" }}>
        <h3 style={{ margin: "0 0 0.5rem 0" }}>سجل المعاملات النقدية والتحويلات</h3>
        <p style={{ color: "var(--color-content-muted)", margin: 0 }}>
          انقر فوق زر تسجيل الدفعة للتحقق من تدفق الأمان المالي، ومفتاح Idempotency، وحماية الإرسال المتكرر.
        </p>
      </div>

      <PaymentEntryDialog
        isOpen={isOpen}
        onOpenChange={setIsOpen}
        onPaymentComplete={(res) => setLatestPayment(res)}
      />
    </div>
  );
}

function ShowcasePreview({ density }: { density: "comfortable" | "compact" }) {
  const [money, setMoney] = useState({ amount: "125000.50", currency: "SAR" });
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [selectedCity, setSelectedCity] = useState<string | number | null>("RUH");

  const sampleRows = [
    { id: "1", code: "INV-001", party: "شركة الأفق للتجارة", amount: "15,400.00 SAR", status: "Active" },
    { id: "2", code: "INV-002", party: "مؤسسة الرياض للحلول", amount: "8,920.00 SAR", status: "Active" },
  ];

  const columns: DataGridColumn<(typeof sampleRows)[0]>[] = [
    { id: "code", header: "رقم الفاتورة", width: 140 },
    { id: "party", header: "الطرف التجاري", width: "1fr" },
    { id: "amount", header: "المبلغ الإجمالي", width: 160, align: "end" },
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", padding: "1rem" }}>
      <h2>معاينة مكونات نظام التصميم</h2>
      <div data-testid="money-field-surface" style={{ maxWidth: "360px", background: "var(--color-surface-subtle)", padding: "1rem", borderRadius: "8px" }}>
        <h3>حقل المبالغ النقدية (MoneyField)</h3>
        <MoneyField
          label="المبلغ المالي"
          value={money}
          onChange={setMoney}
          size={density}
        />
        <div style={{ marginTop: "0.5rem", fontSize: "0.875rem" }}>
          العرض المنسق: <MoneyDisplay value={money} />
        </div>
      </div>

      <div data-testid="datagrid-surface" style={{ background: "#fff", padding: "1rem", borderRadius: "8px" }}>
        <h3>جدول البيانات (DataGrid)</h3>
        <DataGrid
          columns={columns}
          staticRows={sampleRows}
          density={density}
          selectable="single"
        />
      </div>

      <div style={{ display: "flex", gap: "1rem", alignItems: "center" }}>
        <Button variant="primary" size={density} onPress={() => setIsDialogOpen(true)}>
          فتح نافذة التأكيد
        </Button>
        <div style={{ width: "240px" }}>
          <ComboBox
            label="المدينة"
            selectedKey={selectedCity}
            onSelectionChange={setSelectedCity}
            options={[
              { id: "RUH", label: "الرياض (Riyadh)" },
              { id: "JED", label: "جدة (Jeddah)" },
              { id: "CAI", label: "القاهرة (Cairo)" },
            ]}
          />
        </div>
      </div>

      <Dialog
        isOpen={isDialogOpen}
        onOpenChange={setIsDialogOpen}
        title="تأكيد العملية"
        description="هل تود تأكيد حفظ الإعدادات المحددة؟"
      >
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end", marginTop: "1rem" }}>
          <Button variant="secondary" onPress={() => setIsDialogOpen(false)}>إلغاء</Button>
          <Button variant="primary" onPress={() => setIsDialogOpen(false)}>تأكيد</Button>
        </div>
      </Dialog>
    </div>
  );
}

function StatusView({ direction, message }: { direction: "rtl" | "ltr"; message: string }) { return <main className="status-page" dir={direction}><p role="status">{message}</p></main>; }

function AccessView({ direction, kind, language, message, onRetry }: { direction: "rtl" | "ltr"; kind: "forbidden" | "error"; language: Language; message?: string | undefined; onRetry: () => void }) {
  const arabic = language === "ar";
  return <main className="status-page" dir={direction}><div className="auth-card"><h1>{kind === "forbidden" ? (arabic ? "لا تملك الصلاحية" : "Access denied") : (arabic ? "حدث خطأ" : "Something went wrong")}</h1><p>{message ?? (arabic ? "لا يمكن متابعة العملية الحالية." : "The current operation cannot continue.")}</p><button type="button" onClick={onRetry}>{arabic ? "إعادة المحاولة" : "Retry"}</button></div></main>;
}

export class ShellErrorBoundary extends Component<{ children: ReactNode }, { hasError: boolean; error?: string }> {
  constructor(props: { children: ReactNode }) { super(props); this.state = { hasError: false }; }
  static getDerivedStateFromError(error: Error) { return { hasError: true, error: error.message }; }
  override componentDidCatch(error: Error, errorInfo: ErrorInfo) { console.error("Shell crashed:", error, errorInfo); }
  override render() {
    if (!this.state.hasError) return this.props.children;
    return <main className="status-page" dir="rtl"><div className="auth-card"><h1>حدث خطأ غير متوقع</h1><p>تعذر عرض الواجهة بسبب خطأ غير معالج.</p><p className="meta">{this.state.error}</p><button type="button" onClick={() => window.location.reload()}>إعادة تحميل الصفحة</button></div></main>;
  }
}
