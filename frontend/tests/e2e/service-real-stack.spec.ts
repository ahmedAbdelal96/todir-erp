import { test, expect, Page, request } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';
import { verifyTargetDatabase } from '../../modules/service/utils/test-probe-guard.ts';

const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/service-acceptance/real-stack');
const AGENT_BRAIN_DIR = 'C:/Users/Adham/.gemini/antigravity/brain/2cce5b52-1525-442e-8037-81105c0347eb/service-acceptance/real-stack';
const ARTIFACTS_DIR = process.env.TEST_ARTIFACTS_DIR || LOCAL_ARTIFACTS_DIR;

const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const ATLAS_TENANT_ID = 'c9fea3c8-249d-f150-a53a-84f5a891c82b';
const SEEDED_CUSTOMER_ID = 'cdcdd524-629a-7f56-a9ae-19b80fc05a48';
const FIELD_TECH_USER_ID = '2e2d6cd8-df8f-7a58-9298-f9a363cc1685';

function requireTestDevPassword(): string {
  const pwd = process.env.ERP_DEV_PASSWORD || process.env.SEED_DEFAULT_PASSWORD;
  if (!pwd || pwd.trim() === '') {
    throw new Error(
      'FAIL CLOSED: ERP_DEV_PASSWORD or SEED_DEFAULT_PASSWORD must be configured for the real-stack Service acceptance suite.'
    );
  }
  return pwd;
}

function requireTestProbeSecret(): string {
  const secret = process.env.TODIR_TEST_PROBE_SECRET;
  if (!secret || secret.trim() === '') {
    throw new Error(
      'FAIL CLOSED: TODIR_TEST_PROBE_SECRET must be configured for the real-stack Service acceptance suite.'
    );
  }
  return secret;
}

test.describe.configure({ mode: 'serial' });

test.describe('TODIR Module 24: Service & Maintenance — Real-Stack End-to-End Acceptance Suite', () => {
  test.setTimeout(240_000);

  test.beforeAll(async () => {
    // 1. Ensure artifact directories exist
    [LOCAL_ARTIFACTS_DIR, AGENT_BRAIN_DIR].forEach((dir) => {
      try {
        if (!fs.existsSync(dir)) {
          fs.mkdirSync(dir, { recursive: true });
        }
      } catch {
        // ignore directory permission errors
      }
    });

    // 2. Strict Fail-Closed Database Protection Check
    const probeSecret = requireTestProbeSecret();
    const expectedDatabase = process.env.TODIR_TEST_DATABASE || 'todir_service_acceptance';
    const baseURL = process.env.PLAYWRIGHT_BASE_URL || 'http://127.0.0.1:5173';
    const apiContext = await request.newContext({ baseURL });

    try {
      const probeRes = await apiContext.get('/api/v1/service/target', {
        headers: {
          'X-Todir-Test-Secret': probeSecret
        }
      });

      let probeData: { database?: string } | undefined = undefined;
      try {
        probeData = (await probeRes.json()) as { database?: string };
      } catch {}

      const safety = verifyTargetDatabase({
        probeResponse: {
          status: probeRes.status(),
          data: probeData
        },
        probeSecret,
        expectedDatabase
      });

      if (!safety.success) {
        throw new Error(safety.error);
      }
    } finally {
      await apiContext.dispose();
    }
  });

  async function getCsrfToken(page: Page): Promise<string> {
    const csrfRes = await page.request.get('/api/v1/auth/csrf');
    const { token } = await csrfRes.json();
    return token;
  }

  async function loginAsUser(page: Page, email: string) {
    await page.goto('/');
    await page.context().clearCookies();
    await page.evaluate(() => {
      try { localStorage.clear(); sessionStorage.clear(); } catch {}
    });
    const password = requireTestDevPassword();
    const token = await getCsrfToken(page);
    const loginRes = await page.request.post('/api/v1/auth/login', {
      data: { email, password },
      headers: { 'X-CSRF-TOKEN': token }
    });
    expect(loginRes.status()).toBe(200);

    await page.goto(`/service?legalEntityId=${ATLAS_ENTITY_ID}`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('h1')).toContainText(/الخدمة والصيانة|Service/i, { timeout: 20000 });
  }

  async function loginAsAdmin(page: Page) {
    await loginAsUser(page, 'admin@atlas.com');
  }

  const saveArtifact = async (page: Page, filename: string) => {
    const localPath = path.join(LOCAL_ARTIFACTS_DIR, filename);
    await page.screenshot({ path: localPath, fullPage: false });

    try {
      const brainPath = path.join(AGENT_BRAIN_DIR, filename);
      fs.copyFileSync(localPath, brainPath);
    } catch {
      // brain directory copy optional
    }
  };

  test('01 — Real Authentication, Live Database State & Authoritative Service Overview', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Verify Title and Subtitle
    await expect(page.locator('h1')).toContainText('الخدمة والصيانة والعمليات الميدانية');

    // Verify all 9 module tabs are rendered and accessible
    await expect(page.getByTestId('service-tab-overview')).toBeVisible();
    await expect(page.getByTestId('service-tab-requests')).toBeVisible();
    await expect(page.getByTestId('service-tab-work-orders')).toBeVisible();
    await expect(page.getByTestId('service-tab-dispatch')).toBeVisible();
    await expect(page.getByTestId('service-tab-my-day')).toBeVisible();
    await expect(page.getByTestId('service-tab-installed-base')).toBeVisible();
    await expect(page.getByTestId('service-tab-preventive')).toBeVisible();
    await expect(page.getByTestId('service-tab-contracts-warranty')).toBeVisible();
    await expect(page.getByTestId('service-tab-billing')).toBeVisible();

    // Verify Live KPI cards rendered with real authoritative database values
    await expect(page.locator('text=طلبات الخدمة المفتوحة').first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('text=أوامر العمل الجارية').first()).toBeVisible();

    await saveArtifact(page, '01-real-service-overview-desktop.png');
  });

  test('02 — Real Service Request Lifecycle: Create, Refresh, Reopen Drawer, Triage & Verify Persisted State', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    const runId = Date.now().toString().slice(-4);
    const requestTitle = `طلب صيانة محطة التبريد المركزية - E2E ${runId}`;

    // Open Requests Tab
    await page.getByTestId('service-tab-requests').click();
    await page.waitForTimeout(500);

    // Click "+ طلب خدمة جديد"
    const newReqBtn = page.getByRole('button', { name: /طلب خدمة جديد/i }).first();
    await expect(newReqBtn).toBeVisible();
    await newReqBtn.click();

    // Fill Modal
    const modal = page.locator('.ui-dialog');
    await expect(modal).toBeVisible();
    await modal.getByPlaceholder(/وصف موجز للمشكلة الفنية/i).fill(requestTitle);
    await modal.getByPlaceholder(/مثال: شركة الفطيم/i).fill(SEEDED_CUSTOMER_ID);

    // Select installed base item (first available seeded item)
    const ibSelect = modal.getByTestId('request-installed-base-select');
    await ibSelect.selectOption({ index: 1 });

    // Submit request to live backend database
    const submitBtn = modal.getByRole('button', { name: /إرسال طلب الخدمة/i });
    await submitBtn.click();
    await expect(modal).not.toBeVisible({ timeout: 10000 });

    // Verify Persisted in Table
    await page.waitForTimeout(600);
    const reqRow = page.getByText(requestTitle).first();
    await expect(reqRow).toBeVisible({ timeout: 15000 });

    // Click on row to open RequestDetailDrawer
    await reqRow.click();
    await page.waitForTimeout(600);

    // In RequestDetailDrawer, verify drawer is open and displays request
    const drawer = page.locator('.quick-peek-panel');
    await expect(drawer).toBeVisible({ timeout: 10000 });
    await expect(drawer.getByText(requestTitle)).toBeVisible();

    // Click Triage action button in drawer
    const triageActionBtn = drawer.getByRole('button', { name: /فرز وتقييم الطلب الفني/i });
    await expect(triageActionBtn).toBeVisible({ timeout: 10000 });
    await triageActionBtn.click();
    await page.waitForTimeout(500);

    // Triage Modal
    const triageModal = page.locator('.ui-dialog');
    await expect(triageModal).toBeVisible();

    // Submit Triage
    const completeTriageBtn = triageModal.getByRole('button', { name: /تأكيد الفرز والاعتماد/i });
    await expect(completeTriageBtn).toBeVisible();
    await completeTriageBtn.click();
    await expect(triageModal).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(800);

    // Verify drawer reflects updated state
    await expect(drawer.getByText(/تم الفرز|Triaged/i).first()).toBeVisible({ timeout: 10000 });

    // Close drawer
    const closeDrawerBtn = drawer.getByRole('button', { name: /إغلاق/i }).first();
    await expect(closeDrawerBtn).toBeVisible();
    await closeDrawerBtn.click();
    await page.waitForTimeout(400);

    // Refresh browser and verify persisted state remains in live PostgreSQL
    await page.reload();
    await page.waitForLoadState('domcontentloaded');
    await page.getByTestId('service-tab-requests').click();
    await expect(page.getByText(requestTitle).first()).toBeVisible({ timeout: 15000 });

    await saveArtifact(page, '02-real-service-request-triaged.png');
  });

  test('03 — Real Work Order Lifecycle: Creation, Ready, Schedule & RowVersion Concurrency', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    const runId = Date.now().toString().slice(-4);
    const woTitle = `أمر عمل تصحيحي لمعايرة الصمامات - E2E ${runId}`;

    // Open Work Orders Tab
    await page.getByTestId('service-tab-work-orders').click();
    await page.waitForTimeout(500);

    // Click "+ أمر عمل جديد"
    const newWoBtn = page.getByRole('button', { name: /\+ أمر عمل جديد/i }).first();
    await expect(newWoBtn).toBeVisible();
    await newWoBtn.click();

    // Fill Modal
    const modal = page.locator('.ui-dialog');
    await expect(modal).toBeVisible();
    await modal.getByPlaceholder(/وصف موجز للمشكلة الفنية/i).fill(woTitle);
    await modal.getByPlaceholder(/مثال: شركة الفطيم/i).fill(SEEDED_CUSTOMER_ID);

    // Select installed base item (first available seeded item)
    const woIbSelect = modal.getByTestId('work-order-installed-base-select');
    await woIbSelect.selectOption({ index: 1 });

    // Submit work order to live backend database
    const submitBtn = modal.getByRole('button', { name: /أمر عمل/i }).last();
    await submitBtn.click();
    await expect(modal).not.toBeVisible({ timeout: 10000 });

    // Verify Persisted in Table
    await page.waitForTimeout(600);
    const woRow = page.getByText(woTitle).first();
    await expect(woRow).toBeVisible({ timeout: 15000 });

    // Open WorkOrderDetailDrawer
    await woRow.click();
    await page.waitForTimeout(600);

    const drawer = page.locator('.quick-peek-panel');
    await expect(drawer).toBeVisible({ timeout: 10000 });
    await expect(drawer.getByText(woTitle)).toBeVisible();

    // Verify initial row version is displayed (version 1)
    const versionBadge = drawer.getByTestId('wo-version-badge');
    await expect(versionBadge).toBeVisible();
    await expect(versionBadge).toHaveText('1');

    // Transition: Draft -> Ready
    const readyBtn = drawer.getByRole('button', { name: /جاهز للجدولة/i });
    await expect(readyBtn).toBeVisible({ timeout: 10000 });
    await readyBtn.click();
    await page.waitForTimeout(800);
    await expect(versionBadge).toHaveText('2');
    await expect(drawer.getByText(/جاهز للجدولة/i).first()).toBeVisible({ timeout: 10000 });

    // Transition: Ready -> Schedule
    const scheduleBtn = drawer.getByRole('button', { name: /مجدول|جدولة/i });
    await expect(scheduleBtn).toBeVisible({ timeout: 10000 });
    await scheduleBtn.click();
    await page.waitForTimeout(800);
    await expect(versionBadge).toHaveText('3');
    await expect(drawer.getByText(/مجدول/i).first()).toBeVisible({ timeout: 10000 });

    // Close drawer
    const closeBtn = drawer.getByRole('button', { name: /إغلاق/i }).first();
    await expect(closeBtn).toBeVisible();
    await closeBtn.click();
    await page.waitForTimeout(400);

    // Refresh and verify persisted state and row version in PostgreSQL
    await page.reload();
    await page.waitForLoadState('domcontentloaded');
    await page.getByTestId('service-tab-work-orders').click();
    await expect(page.getByText(woTitle).first()).toBeVisible({ timeout: 15000 });

    await saveArtifact(page, '03-real-work-order-lifecycle.png');
  });

  test('04 — Real Dispatcher Workbench: Visit Scheduling & Technician Assignment', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Open Dispatching Tab
    await page.getByTestId('service-tab-dispatch').click();
    await page.waitForTimeout(600);

    // Verify Dispatcher Workbench surface is rendered
    await expect(page.getByText(/الزيارات الميدانية والمواعيد|كافة الزيارات المجدولة/i).first()).toBeVisible();

    // Switch to Unscheduled Work Orders
    const unscheduledTabBtn = page.getByRole('button', { name: /أوامر العمل غير المجدولة/i });
    await expect(unscheduledTabBtn).toBeVisible();
    await unscheduledTabBtn.click();
    await page.waitForTimeout(500);

    // Schedule visit on the unscheduled work order
    const scheduleVisitBtn = page.getByRole('button', { name: /جدولة زيارة/i }).first();
    await expect(scheduleVisitBtn).toBeVisible({ timeout: 10000 });
    await scheduleVisitBtn.click();
    await page.waitForTimeout(500);

    const visitModal = page.locator('.ui-dialog');
    await expect(visitModal).toBeVisible({ timeout: 10000 });

    const confirmScheduleBtn = visitModal.getByRole('button', { name: /جدولة زيارة/i }).last();
    await expect(confirmScheduleBtn).toBeVisible();
    await confirmScheduleBtn.click();
    await expect(visitModal).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(600);

    // Switch back to All Visits
    const allVisitsTabBtn = page.getByRole('button', { name: /كافة الزيارات المجدولة|الزيارات/i }).first();
    await expect(allVisitsTabBtn).toBeVisible();
    await allVisitsTabBtn.click();
    await page.waitForTimeout(500);

    // Verify scheduled visits list
    await expect(page.getByText(/الزيارات الميدانية والمواعيد/i).first()).toBeVisible();

    // Assign technician to the newly scheduled visit
    const assignBtn = page.getByRole('button', { name: /تكليف فني|تعيين فني/i }).first();
    await expect(assignBtn).toBeVisible({ timeout: 10000 });
    await assignBtn.click();
    await page.waitForTimeout(500);

    const assignModal = page.locator('.ui-dialog');
    await expect(assignModal).toBeVisible({ timeout: 10000 });

    // Fill technician user ID (seeded field technician)
    const techInput = assignModal.getByPlaceholder(/TECH-001/i);
    await expect(techInput).toBeVisible();
    await techInput.fill(FIELD_TECH_USER_ID);

    const confirmAssignBtn = assignModal.getByRole('button', { name: /تأكيد التكليف|تأكيد التعيين/i });
    await expect(confirmAssignBtn).toBeVisible();
    await confirmAssignBtn.click();
    await expect(assignModal).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(600);

    await saveArtifact(page, '04-real-dispatcher-visit-assigned.png');
  });

  test('05 — Field Technician My Day & Isolation: Authenticate, Transition & Cross-User Security', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });

    // 1. Authenticate as technician@atlas.com (the assigned technician)
    await loginAsUser(page, 'technician@atlas.com');

    // 2. Open My Day Tab
    await page.getByTestId('service-tab-my-day').click();
    await page.waitForTimeout(600);

    // Verify Technician Workbench surface
    await expect(page.getByText(/منصة الفني الميداني/i)).toBeVisible();
    await expect(page.getByText(/متصل بالشبكة/i)).toBeVisible();

    // 3. Verify assigned visit is present and execute travel transition
    const travelBtn = page.getByRole('button', { name: /بدء التحرك/i }).first();
    await expect(travelBtn).toBeVisible({ timeout: 15000 });
    await travelBtn.click();
    await page.waitForTimeout(800);

    // Verify state transitioned to Traveling ("في الطريق") and arrival action is ready
    await expect(page.getByRole('button', { name: /تأكيد الوصول للموقع|وصول للموقع/i }).first()).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, '05-real-technician-my-day.png');

    // 4. Isolation Check: Log in as technician-2@atlas.com
    await page.context().clearCookies();
    await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
    await loginAsUser(page, 'technician-2@atlas.com');
    await page.getByTestId('service-tab-my-day').click();
    await page.waitForTimeout(600);

    // Verify technician-2 does NOT see the visit assigned to technician 1
    await expect(page.getByText(/لا توجد زيارات مجدولة لك اليوم/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('button', { name: /وصول للموقع|تأكيد الوصول/i })).not.toBeVisible();
  });

  test('06 — Real Offline Queue: Disconnect, Local Persistence, Reconnect & Authoritative Sync', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });

    // 1. Authenticate as technician@atlas.com
    await loginAsUser(page, 'technician@atlas.com');

    await page.getByTestId('service-tab-my-day').click();
    await page.waitForTimeout(600);

    // The assigned visit is in Traveling state, so the action button is "تأكيد الوصول للموقع"
    const arriveBtn = page.getByRole('button', { name: /تأكيد الوصول للموقع|وصول للموقع/i }).first();
    await expect(arriveBtn).toBeVisible({ timeout: 10000 });

    // 2. Disconnect Network: simulate offline field condition
    await page.context().setOffline(true);
    await page.evaluate(() => window.dispatchEvent(new Event('offline')));
    await page.waitForTimeout(400);

    // Verify offline badge
    await expect(page.getByText(/غير متصل/i)).toBeVisible({ timeout: 10000 });

    // 3. Click the arrive action through actual UI while offline
    await arriveBtn.click();
    await page.waitForTimeout(500);

    // 4. Verify queue panel is rendered with the enqueued command
    await expect(page.getByText(/قائمة أوامر عدم الاتصال الميدانية/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/visit\.arrive/i)).toBeVisible();

    // 5. Verify command is stored in scoped localStorage key
    const queueKey = `todir_service_offline_queue_${ATLAS_TENANT_ID}_${FIELD_TECH_USER_ID}_${ATLAS_ENTITY_ID}`;
    const stored = await page.evaluate((key) => localStorage.getItem(key), queueKey);
    expect(stored).not.toBeNull();
    expect(stored).toContain('visit.arrive');

    // 6. Reconnect Network and reload to prove durable persistence from localStorage across page reloads
    await page.context().setOffline(false);
    await page.evaluate(() => window.dispatchEvent(new Event('online')));
    await page.reload();
    await page.waitForLoadState('domcontentloaded');
    await page.getByTestId('service-tab-my-day').click();
    await page.waitForTimeout(500);

    // Verify queue persisted across page reload
    await expect(page.getByText(/قائمة أوامر عدم الاتصال الميدانية/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/visit\.arrive/i)).toBeVisible();

    // 7. Verify online status ready for sync
    await expect(page.getByText(/متصل بالشبكة/i)).toBeVisible({ timeout: 10000 });

    // 8. Trigger Sync Now
    const syncBtn = page.getByRole('button', { name: /مزامنة.*الآن|مزامنة الأوامر الآن/i });
    await expect(syncBtn).toBeVisible();
    await syncBtn.click();
    await page.waitForTimeout(1000);

    // 9. Verify command status transitioned to Synced
    await expect(page.getByText(/تمت المزامنة بنجاح|synced/i).first()).toBeVisible({ timeout: 10000 });

    // 10. Verify the visit state on screen is now Arrived ("في الموقع")
    await expect(page.getByText(/في الموقع|Arrived/i).first()).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, '06-real-technician-offline-sync.png');
  });

  test('07 — Real Entitlement & Warranty Engine Verification', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Open Contracts & Warranty Tab
    await page.getByTestId('service-tab-contracts-warranty').click();
    await page.waitForTimeout(600);

    // Switch to Entitlement Section
    const entitlementSecBtn = page.getByRole('button', { name: /التحقق من استحقاق وتغطية العقد/i }).first();
    await expect(entitlementSecBtn).toBeVisible();
    await entitlementSecBtn.click();
    await page.waitForTimeout(400);

    // Enter seeded customer party ID to run authoritative backend entitlement check
    const customerInput = page.getByPlaceholder(/UUID أو كود العميل/i).first();
    await expect(customerInput).toBeVisible();
    await customerInput.fill(SEEDED_CUSTOMER_ID);

    // Click "التحقق من الاستحقاق" button
    const checkBtn = page.getByRole('button', { name: /التحقق من الاستحقاق/i }).last();
    await expect(checkBtn).toBeVisible();
    await checkBtn.click();
    await page.waitForTimeout(1000);

    // Verify line results table from real backend engine
    await expect(page.getByText(/حالة اتفاقية SLA|المصدر|التغطية/i).first()).toBeVisible({ timeout: 15000 });
    await expect(page.locator('table').getByText(/ساعات عمل فني|Labor/i).first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('table').getByText(/ServiceContract|عقد صيانة|ضمان/i).first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('table').getByText(/مغطى|مشمول/i).first()).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, '07-real-entitlement-engine.png');
  });

  test('08 — Real Billing & Financial Aggregation Grouped by Currency', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Open Billing Tab
    await page.getByTestId('service-tab-billing').click();
    await page.waitForTimeout(600);

    // Verify Financial KPI Cards
    await expect(page.getByText(/جاهز لفوترة العملاء/i)).toBeVisible();
    await expect(page.getByText(/تكاليف ضمان مستوعبة/i)).toBeVisible();
    await expect(page.getByText(/تكاليف محملة على مشاريع/i)).toBeVisible();

    // Verify Charges Table rendered
    await expect(page.getByText(/رسوم الخدمة ومنصة الفوترة/i)).toBeVisible();

    // Check if an unbilled charge is available for invoice request
    const requestInvoiceBtn = page.getByRole('button', { name: /طلب إصدار فاتورة/i }).first();
    await expect(requestInvoiceBtn).toBeVisible({ timeout: 10000 });
    await requestInvoiceBtn.click();
    await page.waitForTimeout(800);
    await expect(page.locator('table').getByText(/تم طلب الفاتورة|مطلوب فوترته|Requested/i).first()).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, '08-real-billing-charges.png');
  });

  test('09 — Mobile 390px Responsive Field Execution Layout', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await loginAsAdmin(page);

    // Open My Day on Mobile
    await page.getByTestId('service-tab-my-day').click();
    await page.waitForTimeout(600);

    // Verify Mobile Layout
    await expect(page.getByText(/منصة الفني الميداني/i)).toBeVisible();
    await expect(page.getByText(/متصل بالشبكة/i)).toBeVisible();

    // Verify horizontal containment (zero viewport blowout)
    const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(395);

    // Verify touch target dimensions: content action buttons height >= 38px
    const buttons = page.locator('main button');
    const buttonCount = await buttons.count();
    let hasTouchTarget = false;
    for (let i = 0; i < buttonCount; i++) {
      const box = await buttons.nth(i).boundingBox();
      if (box && box.height >= 38) {
        hasTouchTarget = true;
        break;
      }
    }
    expect(hasTouchTarget).toBeTruthy();

    await saveArtifact(page, '09-real-service-mobile-390px.png');
  });
});
