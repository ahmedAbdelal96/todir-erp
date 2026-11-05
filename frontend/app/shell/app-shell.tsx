import { Component, Suspense, type ErrorInfo, type ReactNode, useEffect, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useTranslation } from "react-i18next";
import { useQueryClient } from "@tanstack/react-query";
import { Link, NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { UserMenu } from "./user-menu.tsx";
import { useUserDisplayPreferences } from "./use-user-display-preferences.ts";
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
import { useNavigationPreferences } from "./use-navigation-preferences.ts";
import { Dialog } from "../../components/primitives/dialog.tsx";
import { Tooltip, TooltipTrigger } from "../../components/primitives/tooltip.tsx";

type Screen = "loading" | "signed-out" | "authenticated" | "forbidden" | "error";
type Session = {
  authenticated: boolean;
  tenantId: string;
  identityId: string;
  memberships?: Array<{ id: string; tenantId: string; displayName: string }> | undefined;
  context: { displayName?: string; legalEntities: Array<{ id: string; code: string; name: string }>; branches: Array<{ id: string; legalEntityId: string; code: string; name: string }> };
};

const fallbackModuleIds = ["home", "users", "domain-settings", "directory", "catalog", "invoicing", "payments", "accounting", "migration", "finbase", "pricing", "subledger", "tax", "inventory", "sales", "purchasing", "hr", "payroll", "crm", "preview", "settings"];

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
    case "crm":
      return <UsersIcon size={18} />;
    case "payroll":
      return <PaymentsIcon size={18} />;
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
  const location = useLocation();
  const { t } = useTranslation(["common", "navigation"]);
  const language = currentLanguage();
  const direction = currentDirection();
  const [screen, setScreen] = useState<Screen>("loading");
  const [companyName, setCompanyName] = useState("شركة الأفق للتجارة");
  const [userEmail, setUserEmail] = useState(() => window.sessionStorage.getItem("erp.session.email") ?? "—");
  const [activeTenantId, setActiveTenantId] = useState("");
  const [activeIdentityId, setActiveIdentityId] = useState("");
  const [legalEntities, setLegalEntities] = useState<Session["context"]["legalEntities"]>([]);
  const [branches, setBranches] = useState<Session["context"]["branches"]>([]);
  const [legalEntity, setLegalEntity] = useState("");
  const [branch, setBranch] = useState("");
  const [modules, setModules] = useState<readonly ModuleNavigationRegistration[]>(resolveNavigation(fallbackModuleIds));
  const [contextError, setContextError] = useState<string | undefined>();
  const [operation, setOperation] = useState<string | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [isCommandPaletteOpen, setIsCommandPaletteOpen] = useState(false);
  const [isShortcutsOpen, setIsShortcutsOpen] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(() => Boolean(document.fullscreenElement));
  const [openSidebarPanel, setOpenSidebarPanel] = useState<"favorites" | "recent" | null>(null);
  const [recentPopoverPosition, setRecentPopoverPosition] = useState({ top: 0, left: 0 });
  const favoritesTriggerRef = useRef<HTMLButtonElement>(null);
  const recentTriggerRef = useRef<HTMLButtonElement>(null);
  const quickPopoverRef = useRef<HTMLDivElement>(null);
  const permCacheRef = useRef<Record<string, boolean>>({});
  const navigationPreferences = useNavigationPreferences(activeTenantId, activeIdentityId, screen === "authenticated");
  const displayPreferences = useUserDisplayPreferences(activeTenantId, activeIdentityId, screen === "authenticated");
  const theme = displayPreferences.preferences.theme;
  const density = displayPreferences.preferences.density;
  const isSidebarCollapsed = displayPreferences.preferences.sidebarCollapsed;
  const activeNavigationItem = modules
    .filter(item => item.path === "/" ? location.pathname === "/" : location.pathname === item.path || location.pathname.startsWith(`${item.path}/`))
    .sort((left, right) => right.path.length - left.path.length)[0];

  useEffect(() => {
    if (screen === "authenticated" && activeNavigationItem) {
      navigationPreferences.recordRecent(activeNavigationItem.id);
    }
  }, [screen, activeNavigationItem?.id, navigationPreferences.recordRecent]);

  useEffect(() => {
    function handleGlobalKeyDown(e: KeyboardEvent) {
      const target = e.target;
      const isEditing = target instanceof HTMLElement && (target.isContentEditable || target.matches("input, textarea, select, [role='textbox']"));
      if (isEditing) return;
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setIsCommandPaletteOpen((prev) => !prev);
      } else if (e.altKey && e.shiftKey && e.code === "KeyS") {
        e.preventDefault();
        displayPreferences.update({ sidebarCollapsed: !displayPreferences.preferences.sidebarCollapsed });
      } else if (e.shiftKey && e.code === "Slash") {
        e.preventDefault();
        setIsShortcutsOpen((open) => !open);
      }
    }
    window.addEventListener("keydown", handleGlobalKeyDown);
    return () => window.removeEventListener("keydown", handleGlobalKeyDown);
  }, [displayPreferences.preferences.sidebarCollapsed, displayPreferences.update]);

  useEffect(() => {
    if (screen === "authenticated" && language !== displayPreferences.preferences.language) {
      void changeLanguage(displayPreferences.preferences.language);
    }
  }, [displayPreferences.preferences.language, language, screen]);

  useEffect(() => {
    setOpenSidebarPanel(null);
  }, [location.pathname]);

  useLayoutEffect(() => {
    if (!openSidebarPanel) return;
    const updatePosition = () => {
      const trigger = openSidebarPanel === "favorites" ? favoritesTriggerRef.current : recentTriggerRef.current;
      if (!trigger) return;
      const rect = trigger.getBoundingClientRect();
      const popoverWidth = Math.min(320, window.innerWidth - 24);
      const preferredLeft = direction === "rtl" ? rect.left - popoverWidth - 8 : rect.right + 8;
      const left = Math.max(12, Math.min(preferredLeft, window.innerWidth - popoverWidth - 12));
      const popoverHeight = quickPopoverRef.current?.getBoundingClientRect().height ?? 0;
      const top = Math.max(12, Math.min(rect.top, window.innerHeight - popoverHeight - 12));
      setRecentPopoverPosition(current => current.top === top && current.left === left ? current : { top, left });
    };
    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target;
      if (target instanceof Node && !quickPopoverRef.current?.contains(target)
        && !recentTriggerRef.current?.contains(target) && !favoritesTriggerRef.current?.contains(target)) {
        setOpenSidebarPanel(null);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpenSidebarPanel(null);
        (openSidebarPanel === "favorites" ? favoritesTriggerRef.current : recentTriggerRef.current)?.focus();
      }
    };
    updatePosition();
    quickPopoverRef.current?.querySelector<HTMLElement>("a")?.focus();
    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    window.addEventListener("resize", updatePosition);
    window.addEventListener("scroll", updatePosition, true);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
      window.removeEventListener("resize", updatePosition);
      window.removeEventListener("scroll", updatePosition, true);
    };
  }, [direction, openSidebarPanel]);

  useEffect(() => {
    const syncFullscreenState = () => setIsFullscreen(Boolean(document.fullscreenElement));
    document.addEventListener("fullscreenchange", syncFullscreenState);
    return () => document.removeEventListener("fullscreenchange", syncFullscreenState);
  }, []);

  async function toggleFullscreen() {
    try {
      if (document.fullscreenElement) {
        await document.exitFullscreen();
      } else {
        await document.documentElement.requestFullscreen();
      }
    } catch {
      // The browser can reject fullscreen requests when its policy disallows them.
    }
  }

  const commandItems = buildCommandPaletteItems({
    modules,
    navigate,
    t
  });

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
      if (response.status === 401) { queryClient.clear(); setActiveTenantId(""); setActiveIdentityId(""); window.sessionStorage.removeItem("erp.session.email"); setUserEmail("—"); setScreen("signed-out"); return; }
      if (response.status === 403) { setScreen("forbidden"); return; }
      if (!response.ok) throw new Error("session.load_failed");
      const session = await response.json() as Session;
      setActiveTenantId(session.tenantId);
      setActiveIdentityId(session.identityId);
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
    setActiveTenantId("");
    setActiveIdentityId("");
    window.sessionStorage.removeItem("erp.session.email");
    setUserEmail("—");
    setScreen("signed-out");
  }

  if (screen === "loading") return <StatusView direction={direction} message={labels.loading} />;
  if (screen === "signed-out") return <AuthView onAuthenticated={(email) => { window.sessionStorage.setItem("erp.session.email", email); setUserEmail(email || "—"); queryClient.clear(); void loadSession(); }} />;
  if (screen === "forbidden") return <AccessView direction={direction} kind="forbidden" onRetry={() => void loadSession()} />;
  if (screen === "error") return <AccessView direction={direction} kind="error" message={error ?? undefined} onRetry={() => void loadSession()} />;

  const favorites = navigationPreferences.snapshot.favorites
    .map(key => modules.find(item => item.id === key))
    .filter((item): item is ModuleNavigationRegistration => item !== undefined && item.id !== activeNavigationItem?.id);
  const recentPages = navigationPreferences.snapshot.recent
    .map(key => modules.find(item => item.id === key))
    .filter((item): item is ModuleNavigationRegistration => item !== undefined && item.id !== activeNavigationItem?.id)
    .slice(0, 5);
  const quickPopoverItems = openSidebarPanel === "favorites" ? favorites : recentPages;
  const quickPopoverTitle = t(openSidebarPanel === "favorites" ? "common:shell.favorites" : "common:shell.recentPages");
  const renderNavigationLink = (item: ModuleNavigationRegistration, canToggleFavorite = false) => {
    const label = t(`navigation:${item.navigationKey}`);
    const isFavorite = navigationPreferences.snapshot.favorites.includes(item.id);
    return (
      <div className="sidebar-entry">
        <TooltipTrigger delay={500} isDisabled={!isSidebarCollapsed}>
          <NavLink
            to={item.path}
            end={item.id === "home"}
            aria-label={label}
            className={({ isActive }) => isActive ? "sidebar-link active" : "sidebar-link"}
          >
            <span className="sidebar-icon-container" style={{ display: "inline-flex", alignItems: "center", justifyContent: "center", width: "26px", height: "26px", borderRadius: "var(--radius-sm, 0.375rem)", backgroundColor: "rgba(15, 23, 42, 0.04)" }}>
              {getModuleIcon(item.icon)}
            </span>
            <span className="sidebar-link-label">{label}</span>
          </NavLink>
          <Tooltip placement={direction === "rtl" ? "left" : "right"}>{label}</Tooltip>
        </TooltipTrigger>
        {canToggleFavorite ? (
          <button
            type="button"
            className={`sidebar-favorite-toggle${isFavorite ? " is-favorite" : ""}`}
            aria-label={t(isFavorite ? "common:shell.removeFavorite" : "common:shell.addFavorite", { name: label })}
            title={t(isFavorite ? "common:shell.removeFavorite" : "common:shell.addFavorite", { name: label })}
            aria-pressed={isFavorite}
            disabled={navigationPreferences.isSavingFavorite}
            onClick={() => navigationPreferences.setFavorite(item.id, !isFavorite)}
          >
            <svg width="17" height="17" viewBox="0 0 24 24" fill={isFavorite ? "currentColor" : "none"} stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="m12 3 2.75 5.58 6.16.9-4.46 4.35 1.05 6.14L12 17.07l-5.5 2.9 1.05-6.14L3.1 9.48l6.16-.9L12 3Z" />
            </svg>
          </button>
        ) : null}
      </div>
    );
  };

  return (
    <UndoToastProvider>
      <div className="todir-app-shell shell" dir={direction} data-theme={theme} data-density={density} data-sidebar-collapsed={isSidebarCollapsed}>
        <header className="topbar">
        {/* Left Side (in LTR) / Right Side (in RTL): Brand & Company */}
        <div style={{ display: "flex", alignItems: "center", gap: "0.875rem" }}>
          <button
            type="button"
            className="sidebar-toggle-btn"
            aria-label={t(isSidebarCollapsed ? "common:shell.expandSidebar" : "common:shell.collapseSidebar")}
            aria-expanded={!isSidebarCollapsed}
            aria-controls="app-sidebar"
            title={t(isSidebarCollapsed ? "common:shell.expandSidebar" : "common:shell.collapseSidebar")}
            onClick={() => displayPreferences.update({ sidebarCollapsed: !isSidebarCollapsed })}
          >
            <svg className="sidebar-toggle-icon" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <rect x="3" y="4" width="18" height="16" rx="2" />
              <path d="M15 4v16" />
              <path d={isSidebarCollapsed ? "m10 9 3 3-3 3" : "m13 9-3 3 3 3"} />
            </svg>
          </button>
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
            onClick={() => displayPreferences.update({ language: language === "ar" ? "en" : "ar" })}
          >
            <GlobeIcon size={14} style={{ color: "#93c5fd" }} />
            <span>{labels.language}</span>
          </button>

          {/* Consolidated User Menu */}
          <UserMenu
            userName={t("common:shell.administrator")}
            userEmail={userEmail}
            onToggleLanguage={() => displayPreferences.update({ language: language === "ar" ? "en" : "ar" })}
            theme={theme}
            onToggleTheme={() => displayPreferences.update({ theme: theme === "default" ? "dark" : "default" })}
            density={density}
            onToggleDensity={() => displayPreferences.update({ density: density === "comfortable" ? "compact" : "comfortable" })}
            onOpenShortcuts={() => setIsShortcutsOpen(true)}
            onSignOut={() => void signOut()}
          />
          <button
            type="button"
            className="topbar-icon-btn"
            aria-label={t(isFullscreen ? "common:shell.exitFullscreen" : "common:shell.enterFullscreen")}
            title={t(isFullscreen ? "common:shell.exitFullscreen" : "common:shell.enterFullscreen")}
            aria-pressed={isFullscreen}
            onClick={() => void toggleFullscreen()}
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              {isFullscreen
                ? <path d="M8 3v5H3m13-5v5h5M3 16h5v5m13-5h-5v5" />
                : <path d="M8 3H3v5m13-5h5v5M3 16v5h5m13-5v5h-5" />}
            </svg>
          </button>
        </div>
      </header>

      <NetworkStatusBanner />

      {operation ? <div className="operation-progress" role="status" aria-live="polite">{operation}</div> : null}

      <div className="layout">
        <aside id="app-sidebar" className="sidebar" aria-label={t("common:accessibility.mainNavigation")}>
          <section className="sidebar-section sidebar-modules-section">
            <div className="sidebar-section-title sidebar-label">{t("common:shell.modules")}</div>
            <nav aria-label={t("common:shell.modules")}>
              {modules.map(item => <div key={item.id}>{renderNavigationLink(item, true)}</div>)}
            </nav>
          </section>
          <div className="sidebar-footer">
            {favorites.length ? (
              <section className="sidebar-section sidebar-quick-section">
                <button
                  ref={favoritesTriggerRef}
                  type="button"
                  className="sidebar-quick-trigger"
                  aria-label={t("common:shell.favorites")}
                  aria-haspopup="dialog"
                  aria-expanded={openSidebarPanel === "favorites"}
                  aria-controls="sidebar-quick-popover"
                  title={t("common:shell.favorites")}
                  onClick={() => setOpenSidebarPanel(panel => panel === "favorites" ? null : "favorites")}
                >
                  <svg width="17" height="17" viewBox="0 0 24 24" fill={openSidebarPanel === "favorites" ? "currentColor" : "none"} stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="m12 3 2.75 5.58 6.16.9-4.46 4.35 1.05 6.14L12 17.07l-5.5 2.9 1.05-6.14L3.1 9.48l6.16-.9L12 3Z" />
                  </svg>
                  <span className="sidebar-quick-trigger-label">{t("common:shell.favorites")}</span>
                  <span className="sidebar-quick-count" aria-label={String(favorites.length)}>{favorites.length}</span>
                </button>
              </section>
            ) : null}
            {recentPages.length ? (
              <section className="sidebar-section sidebar-quick-section" aria-label={t("common:shell.recentPages")}>
                <button
                  ref={recentTriggerRef}
                  type="button"
                  className="sidebar-quick-trigger"
                  aria-label={t("common:shell.recentPages")}
                  aria-haspopup="dialog"
                  aria-expanded={openSidebarPanel === "recent"}
                  aria-controls="sidebar-quick-popover"
                  title={t("common:shell.recentPages")}
                  onClick={() => setOpenSidebarPanel(panel => panel === "recent" ? null : "recent")}
                >
                  <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <circle cx="12" cy="12" r="9" />
                    <path d="M12 7v5l3 2" />
                  </svg>
                  <span className="sidebar-quick-trigger-label">{t("common:shell.recentPages")}</span>
                  <span className="sidebar-quick-count" aria-label={String(recentPages.length)}>{recentPages.length}</span>
                </button>
              </section>
            ) : null}
            {navigationPreferences.saveFailed ? <div className="sidebar-preference-error" role="alert">{t("common:shell.navigationPreferencesSaveFailed")}</div> : null}
          {displayPreferences.saveFailed ? <div className="sidebar-preference-error" role="alert">{t("common:shell.displayPreferencesSaveFailed")}</div> : null}
          </div>
        </aside>

        {openSidebarPanel && quickPopoverItems.length ? createPortal(
          <div
            ref={quickPopoverRef}
            id="sidebar-quick-popover"
            className="sidebar-quick-popover"
            role="dialog"
            aria-label={quickPopoverTitle}
            style={{ top: recentPopoverPosition.top, left: recentPopoverPosition.left }}
          >
            <div className="sidebar-quick-popover-title">{quickPopoverTitle}</div>
            <nav aria-label={quickPopoverTitle}>
              {quickPopoverItems.map(item => <div key={item.id} onClick={() => setOpenSidebarPanel(null)}>{renderNavigationLink(item)}</div>)}
            </nav>
          </div>,
          document.body
        ) : null}

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
      <Dialog
        isOpen={isShortcutsOpen}
        onOpenChange={setIsShortcutsOpen}
        title={t("common:shortcuts.title")}
        description={t("common:shortcuts.description")}
        size="small"
      >
        <div className="keyboard-shortcuts-list">
          <div className="keyboard-shortcut-row">
            <span>{t("common:shortcuts.openSearch")}</span>
            <kbd>Ctrl</kbd><span>+</span><kbd>K</kbd>
          </div>
          <div className="keyboard-shortcut-row">
            <span>{t("common:shortcuts.toggleSidebar")}</span>
            <kbd>Alt</kbd><span>+</span><kbd>Shift</kbd><span>+</span><kbd>S</kbd>
          </div>
          <div className="keyboard-shortcut-row">
            <span>{t("common:shortcuts.openHelp")}</span>
            <kbd>Shift</kbd><span>+</span><kbd>?</kbd>
          </div>
        </div>
      </Dialog>
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
