import { Component, Suspense, type ErrorInfo, type ReactNode, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useQueryClient } from "@tanstack/react-query";
import { Link, NavLink, Outlet, useNavigate } from "react-router-dom";
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
  SubledgerIcon,
  TaxIcon,
  SettingsIcon,
  BuildingIcon,
  GlobeIcon,
  ShieldCheckIcon,
  MapPinIcon,
  ChevronDownIcon,
  SearchIcon
} from "../../components/primitives/icons.tsx";
import { CommandPalette } from "../../components/primitives/command-palette.tsx";
import { NetworkStatusBanner } from "../../components/primitives/network-status-banner.tsx";
import { UndoToastProvider } from "../../components/primitives/toast-undo.tsx";
import { buildCommandPaletteItems } from "./command-palette-registry.ts";
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

const fallbackModuleIds = ["home", "users", "domain-settings", "directory", "catalog", "invoicing", "payments", "accounting", "migration", "finbase", "pricing", "subledger", "tax", "inventory", "sales", "purchasing", "hr", "preview", "settings"];

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
    case "inventory":
      return <CatalogIcon size={18} />;
    case "sales":
      return <CatalogIcon size={18} />;
    case "purchasing":
      return <CatalogIcon size={18} />;
    case "hr":
      return <UsersIcon size={18} />;
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
    case "subledger":
      return <SubledgerIcon size={18} />;
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

function formatEntityLabel(code: string, name: string): string {
  if (!name) return code || "—";
  const cleanName = name.replace(/^MAIN\s*[—–-]\s*/i, "").trim();
  if (!code || code === "MAIN" || code === "DEFAULT" || cleanName.includes(code)) {
    return cleanName;
  }
  return `${cleanName} (${code})`;
}

function formatBranchLabel(code: string, name: string, entityName?: string, isArabic?: boolean): string {
  if (!name) return code || "—";
  let cleanName = name.replace(/^MAIN\s*[—–-]\s*/i, "").trim();

  if (entityName) {
    const cleanEntity = entityName.replace(/^MAIN\s*[—–-]\s*/i, "").trim();
    if (cleanName.toLowerCase().startsWith(cleanEntity.toLowerCase())) {
      cleanName = cleanName.slice(cleanEntity.length).replace(/^[—–-\s]+/, "").trim();
    }
  }

  cleanName = cleanName.replace(/^[a-zA-Z0-9\s]+[—–-]\s*(Main Branch|Secondary Branch)/i, "$1");

  if (isArabic) {
    if (/^main\s*branch$/i.test(cleanName)) {
      cleanName = "الفرع الرئيسي";
    } else if (/^secondary\s*branch$/i.test(cleanName)) {
      cleanName = "الفرع الثانوي";
    }
  }

  if (!code || code === "MAIN" || code === "DEFAULT" || cleanName.includes(code)) {
    return cleanName || name;
  }
  return `${cleanName || name} (${code})`;
}

export function AppShell() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
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
  const [isCommandPaletteOpen, setIsCommandPaletteOpen] = useState(false);
  const permCacheRef = useRef<Record<string, boolean>>({});

  useEffect(() => {
    function handleGlobalKeyDown(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setIsCommandPaletteOpen((prev) => !prev);
      }
    }
    window.addEventListener("keydown", handleGlobalKeyDown);
    return () => window.removeEventListener("keydown", handleGlobalKeyDown);
  }, []);

  const commandItems = buildCommandPaletteItems({
    modules,
    navigate,
    t
  });

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
    <UndoToastProvider>
      <div className="todir-app-shell shell" dir={direction} data-theme={theme} data-density={density}>
        <header className="topbar">
        {/* Left Side (in LTR) / Right Side (in RTL): Brand & Company */}
        <div style={{ display: "flex", alignItems: "center", gap: "0.875rem" }}>
          <Link className="brand" to="/" aria-label="TODIR">
            <span className="brand-icon" style={{ display: "inline-flex", alignItems: "center", justifyContent: "center", width: "32px", height: "32px", borderRadius: "var(--radius-md, 0.5rem)", background: "linear-gradient(135deg, #2563eb 0%, #3b82f6 100%)", color: "#ffffff", fontWeight: 900, fontSize: "1rem", boxShadow: "0 2px 8px rgba(37, 99, 235, 0.45)" }}>
              T
            </span>
            <span style={{ fontSize: "1.125rem", fontWeight: 800, letterSpacing: "0.04em", color: "#ffffff" }}>TODIR</span>
          </Link>

          <span className="brand-divider" style={{ color: "rgba(255, 255, 255, 0.25)" }}>|</span>

          <span
            className="brand-company"
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: "0.375rem",
              padding: "0.25rem 0.625rem",
              borderRadius: "var(--radius-full, 9999px)",
              backgroundColor: "rgba(255, 255, 255, 0.12)",
              fontSize: "0.8125rem",
              fontWeight: 600,
              color: "#ffffff",
              border: "1px solid rgba(255, 255, 255, 0.22)",
              backdropFilter: "blur(8px)"
            }}
          >
            <BuildingIcon size={14} style={{ color: "#93c5fd" }} />
            {companyName}
          </span>
        </div>

        {/* Central Spotlight Search & Command Palette Trigger */}
        <button
          type="button"
          className="topbar-command-trigger"
          onClick={() => setIsCommandPaletteOpen(true)}
          aria-label={t("common:commandPalette.openAria", "البحث السريع والأوامر")}
        >
          <SearchIcon size={16} style={{ color: "#93c5fd", flexShrink: 0 }} />
          <span>{t("common:commandPalette.triggerPlaceholder", "البحث في النظام والأوامر...")}</span>
          <span className="topbar-command-shortcut">
            <kbd>{t("common:commandPalette.shortcutCtrl", "Ctrl")}</kbd> + <kbd>{t("common:commandPalette.shortcutK", "K")}</kbd>
          </span>
        </button>

        {/* Operational Context Controls (Legal Entity & Branch) */}
        <div className="context-controls">
          {/* 1. Legal Entity / Company Selector */}
          <div className="context-pill">
            <BuildingIcon size={14} style={{ color: "#93c5fd", flexShrink: 0 }} />
            <label htmlFor="entity-select" className="context-pill-label">
              {labels.entity}
            </label>
            <div className="context-pill-select-wrapper">
              <select
                id="entity-select"
                aria-label={labels.entity}
                value={legalEntity}
                onChange={(event) => void switchOrganization(event.target.value, "")}
                className="context-pill-select"
              >
                <option value="">—</option>
                {legalEntities.map((item) => (
                  <option key={item.id} value={item.id}>
                    {formatEntityLabel(item.code, item.name)}
                  </option>
                ))}
              </select>
              <ChevronDownIcon size={12} className="context-pill-chevron" />
            </div>
          </div>

          {/* 2. Branch Selector */}
          <div className="context-pill">
            <MapPinIcon size={14} style={{ color: "#6ee7b7", flexShrink: 0 }} />
            <label htmlFor="branch-select" className="context-pill-label">
              {labels.branch}
            </label>
            <div className="context-pill-select-wrapper">
              <select
                id="branch-select"
                aria-label={labels.branch}
                value={branch}
                onChange={(event) => void switchOrganization(legalEntity, event.target.value)}
                className="context-pill-select"
              >
                <option value="">—</option>
                {branches
                  .filter((item) => !legalEntity || item.legalEntityId === legalEntity)
                  .map((item) => (
                    <option key={item.id} value={item.id}>
                      {formatBranchLabel(
                        item.code,
                        item.name,
                        legalEntities.find((l) => l.id === legalEntity)?.name,
                        language === "ar"
                      )}
                    </option>
                  ))}
              </select>
              <ChevronDownIcon size={12} className="context-pill-chevron" />
            </div>
          </div>

          {/* Clean Language quick toggle */}
          <button
            type="button"
            className="topbar-lang-btn"
            onClick={() => void changeLanguage(language === "ar" ? "en" : "ar")}
          >
            <GlobeIcon size={14} style={{ color: "#93c5fd" }} />
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

      <NetworkStatusBanner />

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
                <span className="sidebar-icon-container" style={{ display: "inline-flex", alignItems: "center", justifyContent: "center", width: "26px", height: "26px", borderRadius: "var(--radius-sm, 0.375rem)", backgroundColor: "rgba(15, 23, 42, 0.04)" }}>
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

      <CommandPalette
        isOpen={isCommandPaletteOpen}
        onClose={() => setIsCommandPaletteOpen(false)}
        items={commandItems}
      />
    </div>
    </UndoToastProvider>
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
