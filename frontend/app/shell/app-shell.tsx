import { Component, Suspense, type ErrorInfo, type ReactNode, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useQueryClient } from "@tanstack/react-query";
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
  TagIcon,
  TaxIcon,
  SettingsIcon,
  BuildingIcon,
  GlobeIcon,
  ShieldCheckIcon
} from "../../components/primitives/icons.tsx";
import { AuthView } from "../auth/index.ts";
import { requestCsrf, clearCsrfToken, logoutUser } from "../../core/api/auth-service.ts";
import { changeLanguage, currentDirection, currentLanguage, i18n } from "../../core/i18n/i18n.ts";
import { resolveNavigation } from "../router/navigation-registry.ts";
import type { ModuleNavigationRegistration, WorkspaceOutletContext } from "../router/route-contract.ts";

type Screen = "loading" | "signed-out" | "authenticated" | "forbidden" | "error";
type Session = {
  authenticated: boolean;
  tenantId: string;
  memberships?: Array<{ id: string; tenantId: string; displayName: string }> | undefined;
  context: { displayName?: string; legalEntities: Array<{ id: string; code: string; name: string }>; branches: Array<{ id: string; legalEntityId: string; code: string; name: string }> };
};

const fallbackModuleIds = ["home", "users", "domain-settings", "directory", "catalog", "invoicing", "payments", "accounting", "migration", "finbase", "pricing", "tax", "preview", "settings"];

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
    case "pricing":
      return <TagIcon size={18} />;
    case "tax":
      return <TaxIcon size={18} />;
    case "preview":
      return <ShieldCheckIcon size={18} />;
    case "settings":
      return <SettingsIcon size={18} />;
    default:
      return <BuildingIcon size={18} />;
  }
}

export function AppShell() {
  const queryClient = useQueryClient();
  const { t } = useTranslation(["common", "navigation"]);
  const language = currentLanguage();
  const [screen, setScreen] = useState<Screen>("loading");
  const [companyName, setCompanyName] = useState("شركة الأفق للتجارة");
  const [userEmail, setUserEmail] = useState(() => window.sessionStorage.getItem("erp.session.email") ?? "—");
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
  const permCacheRef = useRef<Record<string, boolean>>({});

  const direction = currentDirection();
  const labels = {
    title: t("common:shell.workspace"),
    entity: t("common:shell.legalEntity"),
    branch: t("common:shell.branch"),
    language: t(language === "ar" ? "common:language.switchToEnglish" : "common:language.switchToArabic"),
    empty: t("common:shell.emptyWorkspace"),
    loading: t("common:states.loading"),
    progress: t("common:shell.updatingContext"),
    signOut: t("common:shell.signOut")
  };

  async function loadSession() {
    setScreen("loading");
    try {
      let response = await fetch("/api/v1/session/context", { headers: { "X-Correlation-Id": crypto.randomUUID() } });
      for (let retries = 0; response.status === 429 && retries < 2; retries++) {
        await new Promise((resolve) => setTimeout(resolve, 2000 * (retries + 1)));
        response = await fetch("/api/v1/session/context", { headers: { "X-Correlation-Id": crypto.randomUUID() } });
      }
      if (response.status === 401) { queryClient.clear(); window.sessionStorage.removeItem("erp.session.email"); setUserEmail("—"); setScreen("signed-out"); return; }
      if (response.status === 403) { setScreen("forbidden"); return; }
      if (!response.ok) throw new Error("session.load_failed");
      const session = await response.json() as Session;
      setCompanyName(session.context.displayName || session.memberships?.[0]?.displayName || "شركة الأفق للتجارة");
      setLegalEntities(session.context.legalEntities);
      setBranches(session.context.branches);
      const storedEntityId = typeof window !== "undefined" ? window.sessionStorage.getItem("erp.current.legalEntityId") : null;
      const defaultEntity = (storedEntityId && session.context.legalEntities.some(item => item.id === storedEntityId) ? storedEntityId : null)
        || (session.context as { currentLegalEntityId?: string }).currentLegalEntityId
        || (session.context.legalEntities.some(item => item.id === legalEntity) ? legalEntity : session.context.legalEntities[0]?.id)
        || "";
      setLegalEntity(defaultEntity);
      if (defaultEntity && typeof window !== "undefined") {
        window.sessionStorage.setItem("erp.current.legalEntityId", defaultEntity);
      }
      const defaultBranch = (session.context as { currentBranchId?: string }).currentBranchId
        || session.context.branches.find(item => (!defaultEntity || item.legalEntityId === defaultEntity) && item.id === branch)?.id
        || session.context.branches.find(item => !defaultEntity || item.legalEntityId === defaultEntity)?.id
        || "";
      setBranch(defaultBranch);
      await loadNavigation();
      setScreen("authenticated");
    } catch {
      setError(t("common:shell.couldNotLoadSession"));
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
        if (permCacheRef.current[item.permission] !== undefined) {
          return permCacheRef.current[item.permission] ? item : null;
        }
        const permissionResponse = await fetch(`/api/v1/authorization/check?permission=${encodeURIComponent(item.permission)}`, { headers: { "X-Correlation-Id": crypto.randomUUID() } });
        permCacheRef.current[item.permission] = permissionResponse.ok;
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
      clearCsrfToken();
      const csrf = await requestCsrf();
      const response = await fetch("/api/v1/context/organization", { method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf, "X-Correlation-Id": crypto.randomUUID() }, body: JSON.stringify({ legalEntityId: nextLegalEntity || null, branchId: nextBranch || null }) });
      if (response.status === 401) { setScreen("signed-out"); return; }
      if (response.status === 403) { setScreen("forbidden"); return; }
      if (!response.ok) throw new Error("context.organization_update_failed");
      if (typeof window !== "undefined") {
        if (nextLegalEntity) {
          window.sessionStorage.setItem("erp.current.legalEntityId", nextLegalEntity);
        } else {
          window.sessionStorage.removeItem("erp.current.legalEntityId");
        }
      }
      setLegalEntity(nextLegalEntity);
      setBranch(nextBranch);
      await queryClient.invalidateQueries();
      await loadNavigation();
    } catch {
      setContextError(t("common:shell.couldNotUpdateScope"));
    } finally {
      setOperation(undefined);
    }
  }

  async function signOut() {
    permCacheRef.current = {};
    await logoutUser();
    queryClient.clear();
    window.sessionStorage.removeItem("erp.session.email");
    setUserEmail("—");
    setScreen("signed-out");
  }

  if (screen === "loading") return <StatusView direction={direction} message={labels.loading} />;
  if (screen === "signed-out") return <AuthView onAuthenticated={(email) => { window.sessionStorage.setItem("erp.session.email", email); setUserEmail(email || "—"); queryClient.clear(); void loadSession(); }} />;
  if (screen === "forbidden") return <AccessView direction={direction} kind="forbidden" onRetry={() => void loadSession()} />;
  if (screen === "error") return <AccessView direction={direction} kind="error" message={error ?? undefined} onRetry={() => void loadSession()} />;

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

          <span className="brand-divider" style={{ color: "var(--color-border-strong, #cbd5e1)" }}>|</span>

          <span
            className="brand-company"
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
            className="topbar-lang-btn"
            onClick={() => void changeLanguage(language === "ar" ? "en" : "ar")}
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
            userName={t("common:shell.administrator")}
            userEmail={userEmail}
            onToggleLanguage={() => void changeLanguage(language === "ar" ? "en" : "ar")}
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
        <aside className="sidebar" aria-label={t("common:accessibility.mainNavigation")}>
          <div className="sidebar-label" style={{ padding: "0.25rem 0.5rem 0.75rem", fontSize: "0.6875rem", fontWeight: 700, letterSpacing: "0.08em", textTransform: "uppercase", color: "var(--color-content-muted, #94a3b8)" }}>
            {t("common:shell.modules")}
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
                <span>{t(`navigation:${item.navigationKey}`)}</span>
              </NavLink>
            ))}
          </nav>
        </aside>

        <main className="content" aria-live="polite">
          {contextError ? <div className="error" role="alert">{contextError}</div> : null}
          <Suspense fallback={<div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>{labels.loading}</div>}>
            <Outlet context={{ density, legalEntityId: legalEntity, branchId: branch } satisfies WorkspaceOutletContext} />
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

function AccessView({ direction, kind, message, onRetry }: { direction: string; kind: "forbidden" | "error"; message?: string | undefined; onRetry: () => void }) {
  const { t } = useTranslation("common");
  const title = kind === "forbidden" ? t("access.deniedTitle") : t("access.errorTitle");
  const fallbackMessage = kind === "forbidden" ? t("access.deniedDescription") : t("access.errorDescription");
  return (
    <div dir={direction} className="status-page">
      <div className="auth-card" style={{ textAlign: "center" }}>
        <h2 style={{ margin: "0 0 0.5rem 0", color: kind === "forbidden" ? "var(--color-status-warning)" : "var(--color-status-danger)" }}>{title}</h2>
        <p style={{ margin: "0 0 1.5rem 0", color: "var(--color-content-muted)" }}>{message || fallbackMessage}</p>
        <button type="button" onClick={onRetry} style={{ padding: "0.5rem 1.25rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)", background: "var(--color-surface-subtle)", cursor: "pointer", fontWeight: 600 }}>
          {t("actions.retry")}
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
          <h2 style={{ color: "var(--color-status-danger)" }}>{i18n.t("common:access.shellErrorTitle")}</h2>
          <p style={{ color: "var(--color-content-muted)" }}>{i18n.t("common:access.shellErrorDescription")}</p>
          <div style={{ display: "flex", justifyContent: "center", gap: "0.75rem", flexWrap: "wrap" }}>
            <button
              type="button"
              onClick={() => window.location.reload()}
              style={{ padding: "0.5rem 1rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)", cursor: "pointer" }}
            >
              {i18n.t("common:actions.retry")}
            </button>
            <Link
              to="/"
              style={{ padding: "0.5rem 1rem", borderRadius: "var(--radius-sm)", background: "var(--color-brand-primary)", color: "white", textDecoration: "none" }}
            >
              {i18n.t("navigation:routeError.backHome")}
            </Link>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}
