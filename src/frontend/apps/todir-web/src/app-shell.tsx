import { useEffect, useMemo, useState } from "react";

type Language = "ar" | "en";
type ModuleRegistration = { id: string; label: string; permission?: string };

const modules: ModuleRegistration[] = [
  { id: "home", label: "الرئيسية" },
  { id: "settings", label: "الإعدادات", permission: "platform.settings.read" },
];

export function AppShell() {
  const [language, setLanguage] = useState<Language>("ar");
  const [tenant, setTenant] = useState("Tenant A");
  const [contextError, setContextError] = useState<string | undefined>();
  const [csrfToken, setCsrfToken] = useState<string>();
  const direction = language === "ar" ? "rtl" : "ltr";
  const labels = useMemo(() => language === "ar" ? { title: "مساحة عمل TODIR", tenant: "المؤسسة", switcher: "تبديل المؤسسة", language: "English", empty: "اختر مساحة عمل للبدء" } : { title: "TODIR workspace", tenant: "Tenant", switcher: "Switch tenant", language: "العربية", empty: "Choose a workspace to begin" }, [language]);

  useEffect(() => { void fetch("/api/v1/auth/csrf").then((response) => response.json() as Promise<{ token?: string }>).then((body) => setCsrfToken(body.token)).catch(() => undefined); }, []);

  async function switchTenant(value: string) {
    setTenant(value);
    setContextError(undefined);
    try {
      const response = await fetch("/api/v1/context/tenant", { method: "POST", headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID(), ...(csrfToken ? { "X-CSRF-TOKEN": csrfToken } : {}) }, body: JSON.stringify({ tenantId: value }) });
      if (!response.ok) setContextError(language === "ar" ? "تعذر تحديث السياق" : "Could not update context");
    } catch {
      setContextError(language === "ar" ? "تعذر الاتصال بالخادم" : "Could not connect to the server");
    }
  }

  return <div className="shell" dir={direction}>
    <header className="topbar">
      <a className="brand" href="/" aria-label="TODIR">TODIR</a>
      <div className="context-controls">
        <label htmlFor="tenant-select">{labels.tenant}</label>
        <select id="tenant-select" aria-label={labels.switcher} value={tenant} onChange={(event) => void switchTenant(event.target.value)}>
          <option>Tenant A</option><option>Tenant B</option>
        </select>
        <button type="button" onClick={() => setLanguage(language === "ar" ? "en" : "ar")}>{labels.language}</button>
      </div>
    </header>
    <div className="layout">
      <aside className="sidebar" aria-label={language === "ar" ? "التنقل الرئيسي" : "Main navigation"}>
        <nav>{modules.map((item) => <a key={item.id} href={`#${item.id}`}>{item.label}</a>)}</nav>
      </aside>
      <main className="content" aria-live="polite">
        <p className="eyebrow">{tenant}</p>
        <h1>{labels.title}</h1>
        {contextError ? <div className="error" role="alert">{contextError}</div> : <div className="empty-state"><p>{labels.empty}</p></div>}
      </main>
    </div>
  </div>;
}
