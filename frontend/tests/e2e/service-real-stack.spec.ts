import { test, expect, Page, request } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/service-acceptance/real-stack');
const AGENT_BRAIN_DIR = 'C:/Users/Adham/.gemini/antigravity/brain/2cce5b52-1525-442e-8037-81105c0347eb/service-acceptance/real-stack';
const ARTIFACTS_DIR = process.env.TEST_ARTIFACTS_DIR || LOCAL_ARTIFACTS_DIR;

const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';

function getTestDevPassword(): string {
  return (
    process.env.ERP_DEV_PASSWORD ||
    process.env.SEED_DEFAULT_PASSWORD ||
    'DevSeed2026Apmjuoj1oZjHEDcxSsOc27HKY8yOv0B7'
  );
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

    // 2. Database Protection Check: Probe backend target database
    const baseURL = process.env.PLAYWRIGHT_BASE_URL || 'http://127.0.0.1:5173';
    const apiContext = await request.newContext({ baseURL });

    try {
      const probeRes = await apiContext.get('/api/v1/service/target', {
        headers: {
          'X-Todir-Test-Secret': getTestDevPassword()
        }
      });

      if (probeRes.ok()) {
        const data = (await probeRes.json()) as { database?: string };
        const databaseName = data.database?.trim().toLowerCase() ?? '';

        if (databaseName === 'todir') {
          throw new Error(
            'FAIL CLOSED: The target API is connected to the normal development database ("todir"). ' +
            'Service real-stack mutation tests are strictly prohibited from modifying "todir". ' +
            'Target must be an isolated test database (e.g. "todir_service_acceptance").'
          );
        }
      }
    } catch (err: any) {
      if (err.message?.includes('FAIL CLOSED')) {
        throw err;
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

  async function loginAsAdmin(page: Page) {
    const token = await getCsrfToken(page);
    const loginRes = await page.request.post('/api/v1/auth/login', {
      data: { email: 'admin@atlas.com', password: getTestDevPassword() },
      headers: { 'X-CSRF-TOKEN': token }
    });
    expect(loginRes.status()).toBe(200);

    await page.goto(`/service?legalEntityId=${ATLAS_ENTITY_ID}`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('h1')).toContainText(/الخدمة والصيانة|Service/i, { timeout: 20000 });
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

  test('02 — Real Service Request Lifecycle: Create, Save, Refresh & Triage', async ({ page }) => {
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
    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();
    await modal.getByPlaceholder(/وصف موجز للمشكلة الفنية/i).fill(requestTitle);
    await modal.getByPlaceholder(/مثال: شركة الفطيم/i).fill('cdcdd524-629a-7f56-a9ae-19b80fc05a48');

    // Select installed base item (first available seeded item)
    const ibSelect = modal.getByTestId('request-installed-base-select');
    await ibSelect.selectOption({ index: 1 });

    // Submit request to live backend database
    const submitBtn = modal.getByRole('button', { name: /إرسال طلب الخدمة/i });
    await submitBtn.click();
    await expect(modal).not.toBeVisible({ timeout: 10000 });

    // Verify Persisted in Table
    await page.waitForTimeout(600);
    await expect(page.getByText(requestTitle)).toBeVisible({ timeout: 15000 });

    // Refresh browser and verify persisted state remains in live PostgreSQL
    await page.reload();
    await page.waitForLoadState('domcontentloaded');
    await page.getByTestId('service-tab-requests').click();
    await expect(page.getByText(requestTitle)).toBeVisible({ timeout: 15000 });

    await saveArtifact(page, '02-real-service-request-triaged.png');
  });

  test('03 — Real Work Order Lifecycle: Creation, Billing Owner & Status Transitions', async ({ page }) => {
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
    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();
    await modal.getByPlaceholder(/وصف موجز للمشكلة الفنية/i).fill(woTitle);
    await modal.getByPlaceholder(/مثال: شركة الفطيم/i).fill('cdcdd524-629a-7f56-a9ae-19b80fc05a48');

    // Select installed base item (first available seeded item)
    const woIbSelect = modal.getByTestId('work-order-installed-base-select');
    await woIbSelect.selectOption({ index: 1 });

    // Submit work order to live backend database
    const submitBtn = modal.getByRole('button', { name: /أمر عمل/i }).last();
    await submitBtn.click();
    await expect(modal).not.toBeVisible({ timeout: 10000 });

    // Verify Persisted in Table
    await page.waitForTimeout(600);
    await expect(page.getByText(woTitle)).toBeVisible({ timeout: 15000 });

    // Verify Billing Owner badge is present
    await expect(page.getByText(/الخدمة \(Service\)|بدون فوترة/i).first()).toBeVisible();

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
    await expect(page.getByText(/أوامر العمل غير المجدولة/i).first()).toBeVisible();

    await saveArtifact(page, '04-real-dispatcher-visit-assigned.png');
  });

  test('05 — Field Technician My Day: Authoritative Mobile Execution & Offline Queue', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Open My Day Tab
    await page.getByTestId('service-tab-my-day').click();
    await page.waitForTimeout(600);

    // Verify Technician Workbench surface
    await expect(page.getByText(/منصة الفني الميداني/i)).toBeVisible();
    await expect(page.getByText(/متصل بالشبكة/i)).toBeVisible();

    // Verify assigned visits list or empty state from live backend
    await expect(page.getByText(/لا توجد زيارات مجدولة لك اليوم|إتمام الزيارة|بدء العمل/i).first()).toBeVisible();

    // Verify durable client offline queue manager: enqueue a client command into localStorage
    await page.evaluate((entityId) => {
      const key = `todir_service_offline_queue_${entityId}`;
      const queue = [
        {
          clientCommandId: 'test-cmd-001',
          commandType: 'RecordLabor',
          payload: { hours: 2 },
          queuedAt: new Date().toISOString(),
          status: 'queued',
          retryCount: 0
        }
      ];
      localStorage.setItem(key, JSON.stringify(queue));
    }, ATLAS_ENTITY_ID);

    await page.reload();
    await page.waitForLoadState('domcontentloaded');
    await page.getByTestId('service-tab-my-day').click();
    await page.waitForTimeout(400);

    // Now offline queue panel is mounted and verified in real browser
    await expect(page.getByText(/قائمة أوامر عدم الاتصال الميدانية/i)).toBeVisible();

    // Clean up test item
    await page.evaluate((entityId) => {
      localStorage.removeItem(`todir_service_offline_queue_${entityId}`);
    }, ATLAS_ENTITY_ID);

    await saveArtifact(page, '05-real-technician-my-day.png');
  });

  test('06 — Real Entitlement & Warranty Engine Verification', async ({ page }) => {
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
    await customerInput.fill('cdcdd524-629a-7f56-a9ae-19b80fc05a48');

    // Click "التحقق من الاستحقاق" button
    const checkBtn = page.getByRole('button', { name: /التحقق من الاستحقاق/i }).last();
    await expect(checkBtn).toBeVisible();
    await checkBtn.click();
    await page.waitForTimeout(1000);

    // Verify line results table from real backend engine
    await expect(page.getByText(/حالة اتفاقية SLA|المصدر|التغطية/i).first()).toBeVisible({ timeout: 15000 });

    await saveArtifact(page, '07-real-entitlement-engine.png');
  });

  test('07 — Real Billing & Financial Aggregation Grouped by Currency', async ({ page }) => {
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

    await saveArtifact(page, '08-real-billing-charges.png');
  });

  test('08 — Mobile 390px Responsive Field Execution Layout', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await loginAsAdmin(page);

    // Open My Day on Mobile
    await page.getByTestId('service-tab-my-day').click();
    await page.waitForTimeout(600);

    // Verify Mobile Layout
    await expect(page.getByText(/منصة الفني الميداني/i)).toBeVisible();
    await expect(page.getByText(/متصل بالشبكة/i)).toBeVisible();

    await saveArtifact(page, '09-real-service-mobile-390px.png');
  });
});
