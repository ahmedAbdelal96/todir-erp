import { Component, Suspense, type ErrorInfo, type ReactNode, useEffect, useMemo, useState } from "react";
import { Link, NavLink, Outlet } from "react-router-dom";
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
  ShieldCheckIcon
} from "../../components/primitives/icons.tsx";
import { AuthView } from "../auth/index.ts";
import { requestCsrf, logoutUser } from "../../core/api/auth-service.ts";
import { resolveNavigation } from "../router/navigation-registry.ts";
import type { ModuleNavigationRegistration, WorkspaceOutletContext } from "../router/route-contract.ts";

type Language = "ar" | "en";
type Screen = "loading" | "signed-out" | "authenticated" | "forbidden" | "error";
type Session = {
  authenticated: boolean;
  tenantId: string;
  memberships?: Array<{ id: string; tenantId: string; displayName: string }> | undefined;
  context: { legalEntities: Array<{ id: string; code: string; name: string }>; branches: Array<{ id: string; legalEntityId: string; code: string; name: string }> };
};

const fallbackModuleIds = ["home", "users", "domain-settings", "directory", "catalog", "invoicing", "payments", "accounting", "migration", "finbase", "preview", "settings"];

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
  const [modules, setModules] = useState<readonly ModuleNavigationRegistration[]>(resolveNavigation(fallbackModuleIds));
  const [contextError, setContextError] = useState<string | undefined>();
  const [operation, setOperation] = useState<string | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [theme, setTheme] = useState<string>("default");
  const [density, setDensity] = useState<"comfortable" | "compact">("comfortable");

  const direction = language === "ar" ? "rtl" : "ltr";
  const labels = useMemo(() => language === "ar" ? {
    title: "مساحة العمل", entity: "الكيان القانوني", branch: "الفرع", language: "English", empty: "اختر مساحة عمل للبدء", loading: "جارٍ التحميل...", progress: "جارٍ تحديث السياق", signOut: "تسجيل الخروج",
  } : {
    title: "Workspace", entity: "Legal entity", branch: "Branch", language: "العربية", empty: "Choose a workspace to begin", loading: "Loading...", progress: "Updating context", signOut: "Sign out",
  }, [language]);

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
      const registeredIds = registered.flatMap(item => item.id ? [item.id] : []);
      const candidates = resolveNavigation(registeredIds);
      const permitted = (await Promise.all(candidates.map(async item => {
        if (!item.permission) return item;
        const permissionResponse = await fetch(`/api/v1/authorization/check?permission=${encodeURIComponent(item.permission)}`, { headers: { "X-Correlation-Id": crypto.randomUUID() } });
        return permissionResponse.ok ? item : null;
      }))).filter((item): item is ModuleNavigationRegistration => item !== null);
      setModules(permitted);
    } catch {
      setModules(resolveNavigation(fallbackModuleIds));
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
          <Link className="brand" to="/" aria-label="TODIR">
            <span className="brand-icon" style={{ display: "inline-flex", alignItems: "center", justifyContent: "center", width: "32px", height: "32px", borderRadius: "var(--radius-md, 0.5rem)", background: "linear-gradient(135deg, var(--color-brand-primary, #4f46e5) 0%, var(--color-brand-primary-hover, #4338ca) 100%)", color: "#ffffff", fontWeight: 900, fontSize: "1rem", boxShadow: "0 2px 4px rgba(79, 70, 229, 0.25)" }}>
              T
            </span>
            <span style={{ fontSize: "1.125rem", fontWeight: 800, letterSpacing: "0.02em", color: "var(--color-content-primary, #0f172a)" }}>TODIR</span>
          </Link>

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
              <NavLink
                key={item.id}
                to={item.path}
                end={item.id === "home"}
                className={({ isActive }) => isActive ? "active" : undefined}
              >
                <span style={{ display: "inline-flex", alignItems: "center" }}>
                  {getModuleIcon(item.icon)}
                </span>
                <span>{item.label[language]}</span>
              </NavLink>
            ))}
          </nav>
        </aside>

        <main className="content" aria-live="polite">
          {contextError ? <div className="error" role="alert">{contextError}</div> : null}
          <Suspense fallback={<div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>جارٍ التحميل...</div>}>
            <Outlet context={{ language, density } satisfies WorkspaceOutletContext} />
          </Suspense>
        </main>
      </div>
    </div>
  );
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
