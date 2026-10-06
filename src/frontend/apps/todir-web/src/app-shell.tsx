import { Component, type ErrorInfo, type FormEvent, type ReactNode, useEffect, useMemo, useState } from "react";

type Language = "ar" | "en";
type Screen = "loading" | "signed-out" | "authenticated" | "forbidden" | "error";
type ModuleRegistration = { id: string; label: string; permission?: string };
type Session = { authenticated: boolean; tenantId: string };

const fallbackModules: ModuleRegistration[] = [
  { id: "home", label: "الرئيسية" },
  { id: "settings", label: "الإعدادات", permission: "platform.settings.read" },
];
const moduleLabels: Record<string, { ar: string; en: string; permission?: string }> = {
  home: { ar: "الرئيسية", en: "Home" },
  tenancy: { ar: "المؤسسات", en: "Tenants", permission: "platform.tenancy.read" },
  iam: { ar: "الهوية والعضويات", en: "Identity & memberships", permission: "platform.identity.read" },
  org: { ar: "الهيكل التنظيمي", en: "Organization", permission: "platform.organization.read" },
  settings: { ar: "الإعدادات", en: "Settings", permission: "platform.settings.read" },
};

export function AppShell() {
  const [language, setLanguage] = useState<Language>("ar");
  const [screen, setScreen] = useState<Screen>("loading");
  const [tenant, setTenant] = useState("");
  const [modules, setModules] = useState<ModuleRegistration[]>(fallbackModules);
  const [contextError, setContextError] = useState<string>();
  const [operation, setOperation] = useState<string>();
  const [error, setError] = useState<string>();

  const direction = language === "ar" ? "rtl" : "ltr";
  const labels = useMemo(() => language === "ar" ? {
    title: "مساحة العمل", tenant: "المؤسسة", entity: "الكيان القانوني", branch: "الفرع", switcher: "تبديل المؤسسة", language: "English", empty: "اختر مساحة عمل للبدء", loading: "جارٍ التحميل", progress: "جارٍ تحديث السياق", signOut: "تسجيل الخروج",
  } : {
    title: "Workspace", tenant: "Tenant", entity: "Legal entity", branch: "Branch", switcher: "Switch tenant", language: "العربية", empty: "Choose a workspace to begin", loading: "Loading", progress: "Updating context", signOut: "Sign out",
  }, [language]);

  async function loadSession() {
    setScreen("loading");
    try {
      const response = await fetch("/api/v1/session", { headers: { "X-Correlation-Id": crypto.randomUUID() } });
      if (response.status === 401) { setScreen("signed-out"); return; }
      if (response.status === 403) { setScreen("forbidden"); return; }
      if (!response.ok) throw new Error("session.load_failed");
      const session = await response.json() as Session;
      setTenant(session.tenantId);
      await loadNavigation();
      setScreen("authenticated");
    } catch {
      setError(language === "ar" ? "تعذر تحميل الجلسة" : "Could not load the session");
      setScreen("error");
    }
  }

  async function loadNavigation() {
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
    const home = candidates.find(item => item.id === "home") ?? fallbackModules[0];
    setModules([home, ...permitted]);
  }

  useEffect(() => { void loadSession(); }, []);

  async function switchTenant(value: string) {
    if (!value || value === tenant) return;
    setOperation(labels.progress);
    setContextError(undefined);
    try {
      const csrf = await requestCsrf();
      const response = await fetch("/api/v1/context/tenant", { method: "POST", headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID(), "X-CSRF-TOKEN": csrf }, body: JSON.stringify({ tenantId: value }) });
      if (response.status === 401) { setScreen("signed-out"); return; }
      if (response.status === 403) { setScreen("forbidden"); return; }
      if (!response.ok) throw new Error("context.update_failed");
      setTenant(value);
      await loadNavigation();
    } catch {
      setContextError(language === "ar" ? "تعذر تحديث السياق" : "Could not update context");
    } finally {
      setOperation(undefined);
    }
  }

  async function signOut() {
    const csrf = await requestCsrf();
    await fetch("/api/v1/auth/logout", { method: "POST", headers: { "X-CSRF-TOKEN": csrf, "X-Correlation-Id": crypto.randomUUID() } });
    setScreen("signed-out");
  }

  if (screen === "loading") return <StatusView direction={direction} message={labels.loading} />;
  if (screen === "signed-out") return <LoginView language={language} onAuthenticated={() => void loadSession()} />;
  if (screen === "forbidden") return <AccessView direction={direction} kind="forbidden" language={language} onRetry={() => void loadSession()} />;
  if (screen === "error") return <AccessView direction={direction} kind="error" language={language} message={error} onRetry={() => void loadSession()} />;

  return <div className="shell" dir={direction}>
    <header className="topbar">
      <a className="brand" href="/" aria-label="TODIR">TODIR</a>
      <div className="context-controls">
        <label htmlFor="tenant-select">{labels.tenant}</label>
        <select id="tenant-select" aria-label={labels.switcher} value={tenant} onChange={(event) => void switchTenant(event.target.value)}><option value={tenant}>{tenant}</option></select>
        <label htmlFor="entity-select">{labels.entity}</label>
        <select id="entity-select" defaultValue=""><option value="">—</option></select>
        <label htmlFor="branch-select">{labels.branch}</label>
        <select id="branch-select" defaultValue=""><option value="">—</option></select>
        <button type="button" onClick={() => setLanguage(language === "ar" ? "en" : "ar")}>{labels.language}</button>
        <button type="button" onClick={() => void signOut()}>{labels.signOut}</button>
      </div>
    </header>
    {operation ? <div className="operation-progress" role="status" aria-live="polite">{operation}</div> : null}
    <div className="layout">
      <aside className="sidebar" aria-label={language === "ar" ? "التنقل الرئيسي" : "Main navigation"}><nav>{modules.map(item => <a key={item.id} href={`#${item.id}`}>{item.label}</a>)}</nav></aside>
      <main className="content" aria-live="polite"><p className="eyebrow">{tenant}</p><h1>{labels.title}</h1>{contextError ? <div className="error" role="alert">{contextError}</div> : <div className="empty-state"><p>{labels.empty}</p></div>}</main>
    </div>
  </div>;
}

function LoginView({ language, onAuthenticated }: { language: Language; onAuthenticated: () => void }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [tenantId, setTenantId] = useState("");
  const [mfaCode, setMfaCode] = useState("");
  const [error, setError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);
  const arabic = language === "ar";
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setSubmitting(true); setError(undefined);
    try {
      const csrf = await requestCsrf();
      const response = await fetch("/api/v1/auth/login", { method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf, "X-Correlation-Id": crypto.randomUUID() }, body: JSON.stringify({ email, password, tenantId, mfaCode: mfaCode || undefined }) });
      if (response.status === 401) { setError(arabic ? "بيانات الدخول أو المؤسسة أو رمز MFA غير صحيحة" : "The credentials, tenant, or MFA code are invalid"); return; }
      if (!response.ok) throw new Error("auth.login_failed");
      onAuthenticated();
    } catch { setError(arabic ? "تعذر الاتصال بالخادم" : "Could not connect to the server"); }
    finally { setSubmitting(false); }
  }
  return <main className="auth-page" dir={arabic ? "rtl" : "ltr"}><form className="auth-card" onSubmit={(event) => void submit(event)}><p className="eyebrow">TODIR</p><h1>{arabic ? "تسجيل الدخول" : "Sign in"}</h1><label htmlFor="email">{arabic ? "البريد الإلكتروني" : "Email"}</label><input id="email" type="email" autoComplete="username" required value={email} onChange={event => setEmail(event.target.value)} /><label htmlFor="password">{arabic ? "كلمة المرور" : "Password"}</label><input id="password" type="password" autoComplete="current-password" required value={password} onChange={event => setPassword(event.target.value)} /><label htmlFor="login-tenant">{arabic ? "معرّف المؤسسة" : "Tenant ID"}</label><input id="login-tenant" required value={tenantId} onChange={event => setTenantId(event.target.value)} /><label htmlFor="mfa-code">{arabic ? "رمز التحقق متعدد العوامل (اختياري)" : "MFA code (if enabled)"}</label><input id="mfa-code" inputMode="numeric" autoComplete="one-time-code" value={mfaCode} onChange={event => setMfaCode(event.target.value)} />{error ? <div className="error" role="alert">{error}</div> : null}<button type="submit" disabled={submitting}>{submitting ? (arabic ? "جارٍ الدخول…" : "Signing in…") : (arabic ? "دخول" : "Sign in")}</button></form></main>;
}

function StatusView({ direction, message }: { direction: "rtl" | "ltr"; message: string }) { return <main className="status-page" dir={direction}><p role="status">{message}</p></main>; }

function AccessView({ direction, kind, language, message, onRetry }: { direction: "rtl" | "ltr"; kind: "forbidden" | "error"; language: Language; message?: string; onRetry: () => void }) {
  const arabic = language === "ar";
  return <main className="status-page" dir={direction}><div className="auth-card"><h1>{kind === "forbidden" ? (arabic ? "لا تملك الصلاحية" : "Access denied") : (arabic ? "حدث خطأ" : "Something went wrong")}</h1><p>{message ?? (arabic ? "لا يمكن متابعة العملية الحالية." : "The current operation cannot continue.")}</p><button type="button" onClick={onRetry}>{arabic ? "إعادة المحاولة" : "Retry"}</button></div></main>;
}

async function requestCsrf(): Promise<string> {
  const response = await fetch("/api/v1/auth/csrf", { headers: { "X-Correlation-Id": crypto.randomUUID() } });
  if (!response.ok) throw new Error("csrf.failed");
  const body = await response.json() as { token?: string };
  if (!body.token) throw new Error("csrf.missing");
  return body.token;
}

type BoundaryState = { failed: boolean };
export class ShellErrorBoundary extends Component<{ children: ReactNode }, BoundaryState> {
  state: BoundaryState = { failed: false };
  static getDerivedStateFromError(): BoundaryState { return { failed: true }; }
  componentDidCatch(_error: Error, _info: ErrorInfo): void { /* Host middleware owns correlation logging. */ }
  render() { return this.state.failed ? <AccessView direction="rtl" kind="error" language="ar" onRetry={() => this.setState({ failed: false })} /> : this.props.children; }
}
