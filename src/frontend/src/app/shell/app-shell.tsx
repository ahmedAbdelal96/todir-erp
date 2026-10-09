import { Component, lazy, Suspense, type ErrorInfo, type FormEvent, type ReactNode, useEffect, useMemo, useState } from "react";
import { MoneyField } from "../../design-system/fields/money-field.tsx";
import { MoneyDisplay } from "../../design-system/fields/money-display.tsx";
import { DataGrid, type DataGridColumn } from "../../design-system/grid/index.ts";
import { ComboBox } from "../../design-system/primitives/combobox.tsx";
import { Button } from "../../design-system/primitives/button.tsx";
import { Dialog } from "../../design-system/primitives/dialog.tsx";

const PartyListPage = lazy(() => import("../../modules/directory/pages/party-list-page.tsx").then(m => ({ default: m.PartyListPage })));
const PartyRecordPage = lazy(() => import("../../modules/directory/pages/party-record-page.tsx").then(m => ({ default: m.PartyRecordPage })));
const ProductListPage = lazy(() => import("../../modules/catalog/pages/product-list-page.tsx").then(m => ({ default: m.ProductListPage })));
const ProductRecordPage = lazy(() => import("../../modules/catalog/pages/product-record-page.tsx").then(m => ({ default: m.ProductRecordPage })));
const InvoiceDocumentPage = lazy(() => import("../../modules/invoicing/pages/invoice-document-page.tsx").then(m => ({ default: m.InvoiceDocumentPage })));
const PaymentEntryDialog = lazy(() => import("../../modules/payments/features/payment-entry-dialog.tsx").then(m => ({ default: m.PaymentEntryDialog })));
const ManualJournalPage = lazy(() => import("../../modules/accounting/pages/manual-journal-page.tsx").then(m => ({ default: m.ManualJournalPage })));
const ImportWizardPage = lazy(() => import("../../modules/migration/pages/import-wizard-page.tsx").then(m => ({ default: m.ImportWizardPage })));
const FinBasePage = lazy(() => import("../../modules/finbase/pages/finbase-page.tsx").then(m => ({ default: m.FinBasePage })));

type Language = "ar" | "en";
type Screen = "loading" | "signed-out" | "authenticated" | "forbidden" | "error";
type ModuleRegistration = { id: string; label: string; permission?: string | undefined };
type Session = {
  authenticated: boolean;
  tenantId: string;
  memberships: Array<{ id: string; tenantId: string; displayName: string }>;
  context: { legalEntities: Array<{ id: string; code: string; name: string }>; branches: Array<{ id: string; legalEntityId: string; code: string; name: string }> };
};

const fallbackModules: ModuleRegistration[] = [
  { id: "home", label: "الرئيسية" },
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
  directory: { ar: "دليل الأطراف", en: "Directory", permission: "directory.party.read" },
  catalog: { ar: "الكتالوج", en: "Catalog", permission: "catalog.product.read" },
  invoicing: { ar: "فواتير المبيعات", en: "Invoices", permission: "invoicing.invoices.read" },
  payments: { ar: "المدفوعات", en: "Payments", permission: "payments.transactions.read" },
  accounting: { ar: "المحاسبة العامة", en: "Accounting", permission: "accounting.journals.read" },
  migration: { ar: "معالج الاستيراد", en: "Import Wizard", permission: "platform.migration.execute" },
  finbase: { ar: "الأساس المالي", en: "Financial foundation", permission: "finbase.currency.read" },
  preview: { ar: "معاينة النظام", en: "System Preview" },
  tenancy: { ar: "المؤسسات", en: "Tenants", permission: "platform.tenancy.read" },
  iam: { ar: "الهوية والعضويات", en: "Identity & memberships", permission: "platform.identity.read" },
  org: { ar: "الهيكل التنظيمي", en: "Organization", permission: "platform.organization.read" },
  settings: { ar: "الإعدادات", en: "Settings", permission: "platform.settings.read" },
};

export function AppShell() {
  const [language, setLanguage] = useState<Language>("ar");
  const [screen, setScreen] = useState<Screen>("loading");
  const [tenant, setTenant] = useState("");
  const [memberships, setMemberships] = useState<Session["memberships"]>([]);
  const [legalEntities, setLegalEntities] = useState<Session["context"]["legalEntities"]>([]);
  const [branches, setBranches] = useState<Session["context"]["branches"]>([]);
  const [legalEntity, setLegalEntity] = useState("");
  const [branch, setBranch] = useState("");
  const [modules, setModules] = useState<ModuleRegistration[]>(fallbackModules);
  const [contextError, setContextError] = useState<string | undefined>();
  const [operation, setOperation] = useState<string | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [currentView, setCurrentView] = useState<string>("home");
  const [selectedPartyId, setSelectedPartyId] = useState<string>("party-001");
  const [selectedProductId, setSelectedProductId] = useState<string>("");
  const [theme, setTheme] = useState<string>("default");
  const [density, setDensity] = useState<"comfortable" | "compact">("comfortable");

  const direction = language === "ar" ? "rtl" : "ltr";
  const labels = useMemo(() => language === "ar" ? {
    title: "مساحة العمل", tenant: "المؤسسة", entity: "الكيان القانوني", branch: "الفرع", switcher: "تبديل المؤسسة", language: "English", empty: "اختر مساحة عمل للبدء", loading: "جارٍ التحميل", progress: "جارٍ تحديث السياق", signOut: "تسجيل الخروج",
  } : {
    title: "Workspace", tenant: "Tenant", entity: "Legal entity", branch: "Branch", switcher: "Switch tenant", language: "العربية", empty: "Choose a workspace to begin", loading: "Loading", progress: "Updating context", signOut: "Sign out",
  }, [language]);

  useEffect(() => {
    const handleHash = () => {
      const hash = window.location.hash.replace("#", "");
      if (hash) {
        setCurrentView(hash);
      }
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
      setTenant(session.tenantId);
      setMemberships(session.memberships);
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
      await loadSession();
    } catch {
      setContextError(language === "ar" ? "تعذر تحديث السياق" : "Could not update context");
    } finally {
      setOperation(undefined);
    }
  }

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
    const csrf = await requestCsrf();
    await fetch("/api/v1/auth/logout", { method: "POST", headers: { "X-CSRF-TOKEN": csrf, "X-Correlation-Id": crypto.randomUUID() } });
    setScreen("signed-out");
  }

  if (screen === "loading") return <StatusView direction={direction} message={labels.loading} />;
  if (screen === "signed-out") return <LoginView language={language} onAuthenticated={() => void loadSession()} />;
  if (screen === "forbidden") return <AccessView direction={direction} kind="forbidden" language={language} onRetry={() => void loadSession()} />;
  if (screen === "error") return <AccessView direction={direction} kind="error" language={language} message={error ?? undefined} onRetry={() => void loadSession()} />;

  return (
    <div className="todir-app-shell shell" dir={direction} data-theme={theme} data-density={density}>
      <header className="topbar">
        <a className="brand" href="/" aria-label="TODIR">TODIR</a>
        <div className="context-controls">
          <label htmlFor="tenant-select">{labels.tenant}</label>
          <select id="tenant-select" aria-label={labels.switcher} value={tenant} onChange={(event) => void switchTenant(event.target.value)}>{memberships.map(item => <option key={item.tenantId} value={item.tenantId}>{item.displayName}</option>)}</select>
          <label htmlFor="entity-select">{labels.entity}</label>
          <select id="entity-select" aria-label={labels.entity} value={legalEntity} onChange={(event) => void switchOrganization(event.target.value, "")}><option value="">—</option>{legalEntities.map(item => <option key={item.id} value={item.id}>{item.code} — {item.name}</option>)}</select>
          <label htmlFor="branch-select">{labels.branch}</label>
          <select id="branch-select" aria-label={labels.branch} value={branch} onChange={(event) => void switchOrganization(legalEntity, event.target.value)}><option value="">—</option>{branches.filter(item => !legalEntity || item.legalEntityId === legalEntity).map(item => <option key={item.id} value={item.id}>{item.code} — {item.name}</option>)}</select>
          <button type="button" onClick={() => setTheme(theme === "default" ? "sapphire-modern" : "default")}>
            {theme === "default" ? "السمة البديلة" : "السمة الافتراضية"}
          </button>
          <button type="button" onClick={() => setDensity(density === "comfortable" ? "compact" : "comfortable")}>
            {density === "comfortable" ? "كثافة مدمجة" : "كثافة مريحة"}
          </button>
          <button type="button" onClick={() => setLanguage(language === "ar" ? "en" : "ar")}>{labels.language}</button>
          <button type="button" onClick={() => void signOut()}>{labels.signOut}</button>
        </div>
      </header>
      {operation ? <div className="operation-progress" role="status" aria-live="polite">{operation}</div> : null}
      <div className="layout">
        <aside className="sidebar" aria-label={language === "ar" ? "التنقل الرئيسي" : "Main navigation"}>
          <nav>
            {modules.map(item => (
              <a
                key={item.id}
                href={`#${item.id}`}
                className={currentView === item.id ? "active" : undefined}
                onClick={() => setCurrentView(item.id)}
              >
                {item.label}
              </a>
            ))}
          </nav>
        </aside>
        <main className="content" aria-live="polite">
          {contextError ? <div className="error" role="alert">{contextError}</div> : null}
          <Suspense fallback={<div style={{ padding: "2rem", textAlign: "center" }}>جارٍ التحميل...</div>}>
            {currentView === "directory" ? (
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
              <div>
                <p className="eyebrow">{tenant}</p>
                <h1>{labels.title}</h1>
                <div className="empty-state">
                  <p>{labels.empty}</p>
                  <div style={{ marginTop: "1rem", display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
                    <Button variant="primary" onPress={() => setCurrentView("directory")}>
                      دليل الأطراف
                    </Button>
                    <Button variant="secondary" onPress={() => setCurrentView("invoicing")}>
                      فواتير المبيعات
                    </Button>
                    <Button variant="secondary" onPress={() => setCurrentView("accounting")}>
                      قيود اليومية
                    </Button>
                    <Button variant="secondary" onPress={() => setCurrentView("migration")}>
                      معالج الاستيراد
                    </Button>
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

function LoginView({ language, onAuthenticated }: { language: Language; onAuthenticated: () => void }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [tenantId, setTenantId] = useState("");
  const [mfaCode, setMfaCode] = useState("");
  const [error, setError] = useState<string | undefined>();
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

function AccessView({ direction, kind, language, message, onRetry }: { direction: "rtl" | "ltr"; kind: "forbidden" | "error"; language: Language; message?: string | undefined; onRetry: () => void }) {
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

export class ShellErrorBoundary extends Component<{ children: ReactNode }, { hasError: boolean; error?: string }> {
  constructor(props: { children: ReactNode }) { super(props); this.state = { hasError: false }; }
  static getDerivedStateFromError(error: Error) { return { hasError: true, error: error.message }; }
  override componentDidCatch(error: Error, errorInfo: ErrorInfo) { console.error("Shell crashed:", error, errorInfo); }
  override render() {
    if (!this.state.hasError) return this.props.children;
    return <main className="status-page" dir="rtl"><div className="auth-card"><h1>حدث خطأ غير متوقع</h1><p>تعذر عرض الواجهة بسبب خطأ غير معالج.</p><p className="meta">{this.state.error}</p><button type="button" onClick={() => window.location.reload()}>إعادة تحميل الصفحة</button></div></main>;
  }
}
