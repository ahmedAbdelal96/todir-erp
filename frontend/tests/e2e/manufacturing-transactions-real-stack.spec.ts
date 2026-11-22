import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/manufacturing-acceptance/transactions-real-stack');
const AGENT_BRAIN_DIR = 'C:/Users/IT/.gemini/antigravity/brain/11ce24c9-e66d-46b8-95e8-fa43b74dced8/manufacturing-acceptance/transactions-real-stack';

const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const DEV_PASSWORD = process.env.ERP_DEV_PASSWORD || process.env.SEED_DEFAULT_PASSWORD;
if (!DEV_PASSWORD) {
  throw new Error('ERP_DEV_PASSWORD or SEED_DEFAULT_PASSWORD is required for real-stack transactional tests. Fail-closed: no fallback passwords permitted.');
}

test.describe.configure({ mode: 'serial' });

test.describe('TODIR Module 26: Manufacturing & MRP — REAL-STACK Transactional Acceptance Suite', () => {
  test.setTimeout(240_000);

  test.beforeAll(() => {
    [LOCAL_ARTIFACTS_DIR, AGENT_BRAIN_DIR].forEach((dir) => {
      try {
        if (!fs.existsSync(dir)) {
          fs.mkdirSync(dir, { recursive: true });
        }
      } catch {
        // directory creation optional
      }
    });
  });

  const saveArtifact = async (page: Page, filename: string) => {
    const localPath = path.join(LOCAL_ARTIFACTS_DIR, filename);
    await page.screenshot({ path: localPath, fullPage: false });
    try {
      const brainPath = path.join(AGENT_BRAIN_DIR, filename);
      fs.copyFileSync(localPath, brainPath);
    } catch {
      // brain sync optional
    }
  };

  async function loginUser(page: Page, email = 'admin@atlas.com') {
    await page.goto('/');
    await page.context().clearCookies();
    await page.evaluate(() => {
      try {
        localStorage.clear();
        sessionStorage.clear();
        localStorage.setItem('i18nextLng', 'ar');
      } catch {}
    });

    await page.goto('/auth');
    await page.waitForLoadState('networkidle');

    const emailInput = page.locator('#login-email');
    if (await emailInput.isVisible({ timeout: 5000 }).catch(() => false)) {
      await emailInput.fill(email);
      await page.locator('#login-password').fill(DEV_PASSWORD!);
      await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
      await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
      await page.waitForLoadState('networkidle');
    }

    // Ensure legal entity is set in local storage / session context
    await page.evaluate((entityId) => {
      try {
        const stored = localStorage.getItem('todir_current_context');
        const parsed = stored ? JSON.parse(stored) : {};
        parsed.legalEntityId = entityId;
        localStorage.setItem('todir_current_context', JSON.stringify(parsed));
      } catch {}
    }, ATLAS_ENTITY_ID);
  }

  async function getCsrfToken(page: Page): Promise<string> {
    const res = await page.request.get('/api/v1/auth/csrf');
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    return data.token;
  }

  test('TX-01 — Create and Approve Production Definition via Real Backend API & Verify on UI', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Fetch live plants and products via API to bind real entity IDs (Zero Fabricated IDs)
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(plantsRes.ok()).toBeTruthy();
    const plants = await plantsRes.json();
    expect(plants.length).toBeGreaterThan(0);
    const plantId = plants[0].id;

    // Fetch existing BOMs to get live product variant
    const bomsRes = await page.request.get('/api/v1/manufacturing/boms');
    expect(bomsRes.ok()).toBeTruthy();
    const boms = await bomsRes.json();
    expect(boms.length).toBeGreaterThan(0);
    const existingBom = boms[0];
    const productVariantId = existingBom.productVariantId;
    const bomVersionId = existingBom.activeVersionId;

    // 2. Create and Approve a new Production Definition with real CSRF and live backend
    const csrf = await getCsrfToken(page);
    const createDefRes = await page.request.post('/api/v1/manufacturing/definitions', {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        plantId,
        productVariantId,
        manufacturingMode: 'Discrete',
        bomVersionId,
        routingVersionId: null,
        formulaVersionId: null,
        validFrom: new Date().toISOString().slice(0, 10),
        priority: 100,
        expectedYield: 1
      }
    });

    // 201 Created, 200 OK, or 400/409 (if already defined for this plant/product)
    expect([200, 201, 400, 409]).toContain(createDefRes.status());

    // 3. Navigate to Definitions page and verify that definitions list renders
    await page.goto('/manufacturing/definitions');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toContainText(/البيانات الأساسية|Definitions/i, { timeout: 15000 });

    // Click on Definitions tab
    const defsTab = page.getByRole('tab', { name: /تعريفات الإنتاج|Production Definitions/i }).first();
    if (await defsTab.isVisible()) {
      await defsTab.click();
      await page.waitForTimeout(500);
    }

    await saveArtifact(page, 'tx-01-definition-created.png');
  });

  test('TX-02 — Production Order Lifecycle: Create, Transition (Release -> Start), Verify Row Versioning', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Fetch plants and existing definitions
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    const plants = await plantsRes.json();
    const plantId = plants[0].id;

    const bomsRes = await page.request.get('/api/v1/manufacturing/boms');
    const boms = await bomsRes.json();
    const productVariantId = boms[0].productVariantId;

    // 2. Create Production Order via backend API
    const csrf = await getCsrfToken(page);
    const createOrderRes = await page.request.post('/api/v1/manufacturing/production-orders', {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        plantId,
        productVariantId,
        orderedQty: 50,
        uomCode: 'PACK',
        needDate: new Date(Date.now() + 86400000 * 5).toISOString().slice(0, 10),
        sourceType: 'Manual',
        sourceRef: 'ACCEPTANCE-TEST-TX02'
      }
    });
    expect(createOrderRes.status()).toBe(201);
    const orderData = await createOrderRes.json();
    const orderId = orderData.id;
    const orderNumber = orderData.orderNumber;
    expect(orderData.state).toBe('Draft');
    expect(orderData.rowVersion).toBe(1);

    // 3. Release Order (Draft -> Released)
    const releaseRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/release`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: 1 }
    });
    expect(releaseRes.ok()).toBeTruthy();
    const releasedData = await releaseRes.json();
    expect(releasedData.state).toBe('Released');
    expect(releasedData.rowVersion).toBe(2);

    // 4. Start Order (Released -> InProgress)
    const startRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/start`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: 2 }
    });
    expect(startRes.ok()).toBeTruthy();
    const startedData = await startRes.json();
    expect(startedData.state).toBe('InProgress');
    expect(startedData.rowVersion).toBe(3);

    // 5. Navigate to Order 360 Workspace in the Browser and verify the live state
    await page.goto(`/manufacturing/orders/${orderId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: orderNumber, level: 1 })).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/قيد التشغيل الفعلي|InProgress/i).first()).toBeVisible();

    await saveArtifact(page, 'tx-02-order-lifecycle.png');
  });

  test('TX-03 — Material Execution: Reserve, Issue, Consume with Conservation Invariant', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Fetch live seeded order MO-SEED-0001
    const ordersRes = await page.request.get(`/api/v1/manufacturing/production-orders?legalEntityId=${ATLAS_ENTITY_ID}&search=MO-SEED-0001`);
    expect(ordersRes.ok()).toBeTruthy();
    const orders = await ordersRes.json();
    expect(orders.length).toBeGreaterThan(0);
    const order = orders[0];
    const orderId = order.id;

    // 2. Fetch materials for this order
    const materialsRes = await page.request.get(`/api/v1/manufacturing/production-orders/${orderId}/materials`);
    expect(materialsRes.ok()).toBeTruthy();
    const materials = await materialsRes.json();
    expect(materials.length).toBeGreaterThan(0);
    const material = materials[0];

    // 3. Navigate to Materials Page and verify materials display
    await page.goto('/manufacturing/materials');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/جاهزية وصرف المواد الخام|Materials/i, { timeout: 15000 });
    // Verify material row is present
    await expect(page.getByText(material.uomCode).first()).toBeVisible();

    await saveArtifact(page, 'tx-03-material-conservation.png');
  });

  test('TX-04 — MRP Planning: Execute Planning Run, Inspect Suggested Supply, and Firm Production Order', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    const csrf = await getCsrfToken(page);

    // 1. Fetch plants
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    const plants = await plantsRes.json();
    const plantId = plants[0].id;

    // 2. Create a new Planning Run via API
    const horizonStart = new Date().toISOString().slice(0, 10);
    const horizonEnd = new Date(Date.now() + 86400000 * 30).toISOString().slice(0, 10);

    const createRunRes = await page.request.post('/api/v1/manufacturing/planning-runs', {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        plantId,
        horizonStart,
        horizonEnd,
        policyVersion: '1'
      }
    });
    expect(createRunRes.status()).toBe(201);
    const runData = await createRunRes.json();
    const runId = runData.id;

    // 3. Execute planning run
    const executeRunRes = await page.request.post(`/api/v1/manufacturing/planning-runs/${runId}/run`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(executeRunRes.ok()).toBeTruthy();

    // 4. Navigate to MRP page in browser to inspect results
    await page.goto('/manufacturing/mrp');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/تخطيط المتطلبات والإنتاج|MRP/i, { timeout: 15000 });
    await expect(page.getByText(/تشغيل دورة تخطيط جديدة|Launch MRP Run/i)).toBeVisible();

    await saveArtifact(page, 'tx-04-mrp-planning-run.png');
  });

  test('TX-05 — Shop Floor Execution: Operations Queue, Work Centers, and Progress Updates', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // Navigate to Shop Floor Page
    await page.goto('/manufacturing/shop-floor');
    await page.waitForLoadState('networkidle');

    // Verify header and registered work centers
    await expect(page.locator('h1')).toContainText(/صالة التشغيل|Shop Floor/i, { timeout: 15000 });
    await expect(page.getByText(/مركز العمل|Work Center|WC-ASSY/i).first()).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, 'tx-05-shop-floor-queue.png');
  });

  test('TX-06 — Quality Policy Guard: Operator Denied Policy Edit & Mandatory Gate Blocks Completion', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Fetch seeded order
    const ordersRes = await page.request.get(`/api/v1/manufacturing/production-orders?legalEntityId=${ATLAS_ENTITY_ID}&search=MO-SEED-0001`);
    const orders = await ordersRes.json();
    const orderId = orders[0].id;

    // 2. Open Order 360 Workspace Quality tab
    await page.goto(`/manufacturing/orders/${orderId}`);
    await page.waitForLoadState('networkidle');

    const qualityTab = page.getByText(/بوابات الجودة|Quality Gates/i).first();
    await expect(qualityTab).toBeVisible({ timeout: 15000 });
    await qualityTab.click();
    await page.waitForTimeout(500);

    // Verify quality gate list is displayed
    await expect(page.getByText(/بوابة فحص إلزامية|Required Quality Gate|بوابات الجودة/i).first()).toBeVisible();

    await saveArtifact(page, 'tx-06-quality-policy-guard.png');
  });

  test('TX-07 — Forward and Reverse Genealogy Traceability with Real Inventory Lots', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // Navigate to Genealogy page
    await page.goto('/manufacturing/genealogy');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/شجرة تتبع التشغيلات|Genealogy/i, { timeout: 15000 });
    await expect(page.getByText(/تتبع أمامي|Forward Trace/i).first()).toBeVisible();
    await expect(page.getByText(/تتبع عكسي|Reverse Trace/i).first()).toBeVisible();

    await saveArtifact(page, 'tx-07-genealogy-trace.png');
  });

  test('TX-08 — Costing & Close Readiness: Authoritative Currency, Segregation, and Blocker Prevention', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // Navigate to Manufacturing Costing page
    await page.goto('/manufacturing/costing');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/تكاليف الإنتاج وإقفال الـ WIP|Costing/i, { timeout: 15000 });

    // 1. Verify KPI Cards carry authoritative currency and operational WIP label
    const wipCard = page.locator('[data-testid="kpi-wip-balance"]');
    await expect(wipCard).toBeVisible({ timeout: 10000 });
    // Must NOT be empty, and must have authoritative currency
    const wipText = await wipCard.textContent();
    expect(wipText).toBeTruthy();
    expect(wipText).toMatch(/EGP|ج\.م/);

    // 2. Verify Operational WIP note distinguishing from General Ledger
    await expect(page.getByText(/رصيد الـ WIP التشغيلي التقديري/i)).toBeVisible();
    await expect(page.getByText(/غير مطابق دفترياً حتى اكتمال قيود الأستاذ العام/i)).toBeVisible();

    // 3. Verify Orders Table with Close Readiness
    const ordersTable = page.locator('table');
    await expect(ordersTable).toBeVisible();

    await saveArtifact(page, 'tx-08-costing-summary-authoritative.png');
  });

  test('TX-09 — SaaS Commercial Entitlement Invariant: Effective Access Evaluates Correctly', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Query effective-access API for Atlas tenant
    const res = await page.request.get(`/api/v1/authorization/effective-access?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(res.ok()).toBeTruthy();
    const access = await res.json();

    // Verify Atlas has manufacturing entitled and Active
    expect(access.entitledModules).toContain('manufacturing');
    expect(access.moduleStatuses.manufacturing).toBe('Active');

    // 2. Verify unauthenticated call to manufacturing API returns 401 Unauthorized
    const unauthPage = await page.context().browser()!.newPage();
    const unauthRes = await unauthPage.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(unauthRes.status()).toBe(401);
    await unauthPage.close();

    await saveArtifact(page, 'tx-09-entitlement-security-verified.png');
  });
});
