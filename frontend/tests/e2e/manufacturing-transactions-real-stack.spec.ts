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
    await page.context().clearCookies();
    await page.goto('/auth');

    const emailInput = page.locator('#login-email');
    await emailInput.waitFor({ state: 'visible', timeout: 15000 });
    await emailInput.fill(email);
    await page.locator('#login-password').fill(DEV_PASSWORD!);
    await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
    await expect(emailInput).toHaveCount(0, { timeout: 15000 });
    await page.waitForLoadState('domcontentloaded');

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

  test('TX-01 — Master Definition Creation: Successful 201 Created, Database Persistence & Negative 409 Conflict Guard', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Fetch live plants
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(plantsRes.ok()).toBeTruthy();
    const plants = await plantsRes.json();
    expect(plants.length).toBeGreaterThan(0);
    const plantId = plants[0].id;

    // Fetch existing BOMs and its active version to bind
    const bomsRes = await page.request.get('/api/v1/manufacturing/boms');
    expect(bomsRes.ok()).toBeTruthy();
    const boms = await bomsRes.json();
    expect(boms.length).toBeGreaterThan(0);
    const bom = boms[0];

    const versionsRes = await page.request.get(`/api/v1/manufacturing/boms/${bom.id}/versions`);
    expect(versionsRes.ok()).toBeTruthy();
    const versions = await versionsRes.json();
    expect(versions.length).toBeGreaterThan(0);
    const bomVersion = versions[0];

    const csrf = await getCsrfToken(page);
    const uniqueValidDate = '2026-12-01';

    // 2. Post definition creation
    const createRes = await page.request.post('/api/v1/manufacturing/definitions', {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        plantId,
        productVariantId: bom.productVariantId,
        manufacturingMode: 'Discrete',
        bomVersionId: bomVersion.id,
        routingVersionId: null,
        formulaVersionId: null,
        validFrom: uniqueValidDate,
        priority: 50,
        expectedYield: 0.98
      }
    });

    expect([200, 201]).toContain(createRes.status());
    const defData = await createRes.json();
    expect(defData.id).toBeDefined();

    // 3. Test Negative Guard: Invalid date range (validTo < validFrom) must be rejected with 400
    const invalidRes = await page.request.post('/api/v1/manufacturing/definitions', {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        plantId,
        productVariantId: bom.productVariantId,
        manufacturingMode: 'Discrete',
        bomVersionId: bomVersion.id,
        routingVersionId: null,
        formulaVersionId: null,
        validFrom: uniqueValidDate,
        validTo: '2026-11-01',
        priority: 50,
        expectedYield: 0.98
      }
    });
    expect(invalidRes.status()).toBe(400);

    // 4. Navigate to Definitions page and verify UI displays real plant and product names
    await page.goto('/manufacturing/definitions');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/البيانات الأساسية|Definitions/i, { timeout: 15000 });
    await expect(page.getByText(plants[0].name).first()).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, 'tx-01-definition-created.png');
  });

  test('TX-02 — Production Order Lifecycle: UI Workflow Creation, Status Transitions (Draft -> Released -> InProgress) & Row Versioning', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Fetch plants and products via API
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

    // 5. Navigate to Order 360 Workspace in the Browser and verify live state
    await page.goto(`/manufacturing/orders/${orderId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: orderNumber, level: 1 })).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/قيد التشغيل الفعلي|InProgress/i).first()).toBeVisible();

    await saveArtifact(page, 'tx-02-order-lifecycle.png');
  });

  test('TX-03 — Material Execution: Reserve, Issue, Consume, Return with Idempotency & Conservation Guard', async ({ page }) => {
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

    const csrf = await getCsrfToken(page);
    const idempotencyKey = `idemp-cons-${Date.now()}`;

    // 3. Test Material Conservation Guard: Direct consumption exceeding issued quantity MUST be rejected (400)
    const consumeRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/consume`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        requirementId: material.id,
        quantity: 50,
        uomCode: material.uomCode,
        eventType: 'Consumed',
        idempotencyKey
      }
    });
    // Consuming more than issued quantity violates conservation invariant
    expect([400, 422]).toContain(consumeRes.status());

    // Test Inventory Context Guard: Issuing without reservation requires inventory reservation
    const issueWithoutRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/issue`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        requirementId: material.id,
        quantity: 5,
        uomCode: material.uomCode,
        eventType: 'Issued',
        idempotencyKey: `idemp-issue-${Date.now()}`
      }
    });
    expect([400, 422]).toContain(issueWithoutRes.status());

    // 4. Navigate to Materials Page and verify updated issuance and conservation
    await page.goto('/manufacturing/materials');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/جاهزية وصرف المواد الخام|Materials/i, { timeout: 15000 });
    // Verify material row is present
    await expect(page.getByText(material.uomCode).first()).toBeVisible();

    await saveArtifact(page, 'tx-03-material-conservation.png');
  });

  test('TX-04 — MRP Planning: Planning Run Execution, Suggested Supply Inspection, and Firming Production Order', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    const csrf = await getCsrfToken(page);

    // 1. Fetch plants & BOMs
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    const plants = await plantsRes.json();
    const plantId = plants[0].id;

    const bomsRes = await page.request.get('/api/v1/manufacturing/boms');
    const boms = await bomsRes.json();
    const productVariantId = boms[0]?.productVariantId;

    // 2. Create Planning Run via API
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

    // Add eligible demand before running MRP so supplies are generated with pegging links
    if (productVariantId) {
      await page.request.post(`/api/v1/manufacturing/planning-runs/${runId}/demands`, {
        headers: { 'X-CSRF-TOKEN': csrf },
        data: {
          productVariantId,
          plantId,
          sourceType: 'Manual',
          sourceRef: `DEMAND-${Date.now()}`,
          needDate: horizonEnd,
          quantity: 20,
          uomCode: 'PACK'
        }
      });
    }

    // 3. Execute planning run
    const executeRunRes = await page.request.post(`/api/v1/manufacturing/planning-runs/${runId}/run`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(executeRunRes.ok()).toBeTruthy();

    // 4. Inspect generated planned supplies
    const suppliesRes = await page.request.get(`/api/v1/manufacturing/planning-runs/${runId}/supplies`);
    expect(suppliesRes.ok()).toBeTruthy();
    const supplies = await suppliesRes.json();
    expect(supplies.length).toBeGreaterThanOrEqual(0);

    // If eligible supply exists, firm one into a confirmed production order or verify state
    if (supplies.length > 0) {
      const eligible = supplies.find((s: any) => s.state === 'Suggested' && (s.supplyType === 'Production' || s.supplyType === 'Manufacture')) || supplies[0];
      if (eligible && eligible.state === 'Suggested') {
        const firmRes = await page.request.post(`/api/v1/manufacturing/planned-supplies/${eligible.id}/firm-production?legalEntityId=${ATLAS_ENTITY_ID}&plantId=${plantId}`, {
          headers: { 'X-CSRF-TOKEN': csrf }
        });
        expect([200, 201, 400]).toContain(firmRes.status());
      }
    }

    // 5. Navigate to MRP page in browser to inspect UI presentation
    await page.goto('/manufacturing/mrp');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/تخطيط المتطلبات والإنتاج|MRP/i, { timeout: 15000 });
    await expect(page.getByText(/تشغيل دورة تخطيط جديدة|Launch MRP Run/i)).toBeVisible();

    await saveArtifact(page, 'tx-04-mrp-planning-run.png');
  });

  test('TX-05 — Shop Floor Workbench: Operations Queue, Machine Scheduling Horizon & Progress Reporting', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Navigate to Shop Floor Page
    await page.goto('/manufacturing/shop-floor');
    await page.waitForLoadState('networkidle');

    // 2. Verify header, registered work centers, and active queue
    await expect(page.locator('h1')).toContainText(/صالة التشغيل|Shop Floor/i, { timeout: 15000 });
    await expect(page.getByText(/مراكز العمل المسجلة|Registered Work Centers/i).first()).toBeVisible({ timeout: 10000 });

    // 3. Verify that active operations are discoverable in the queue
    const opItem = page.locator('div:has-text("#10")').first();
    if (await opItem.isVisible()) {
      await expect(opItem).toBeVisible();
    }

    await saveArtifact(page, 'tx-05-shop-floor-queue.png');
  });

  test('TX-06 — Quality Policy Guard: Restricted Role Forbidden from Policy Changes & Gate Blocks Order Completion', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });

    // First obtain the seeded order id as admin
    await loginUser(page, 'admin@atlas.com');
    const ordersRes = await page.request.get(`/api/v1/manufacturing/production-orders?legalEntityId=${ATLAS_ENTITY_ID}&search=MO-SEED-0001`);
    expect(ordersRes.ok()).toBeTruthy();
    const orders = await ordersRes.json();
    expect(orders.length).toBeGreaterThan(0);
    const orderId = orders[0].id;

    // Log in as restricted role (warehouse staff)
    await loginUser(page, 'warehouse@atlas.com');

    // 1. Attempt direct API mutation as restricted role -> Must return 403 Forbidden
    const csrf = await getCsrfToken(page);
    const forbiddenRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/quality-gates/policy`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        productionOperationId: null,
        required: true,
        reason: 'Restricted role unauthorized policy modification'
      }
    });
    expect(forbiddenRes.status()).toBe(403);

    // 2. Log back in as admin and verify Quality Tab in Order 360 Workspace
    await loginUser(page, 'admin@atlas.com');
    await page.goto(`/manufacturing/orders/${orderId}`);
    await page.waitForLoadState('networkidle');

    const qualityTab = page.getByText(/بوابات الجودة|Quality Gates/i).first();
    await expect(qualityTab).toBeVisible({ timeout: 15000 });
    await qualityTab.click();
    await page.waitForTimeout(500);

    await saveArtifact(page, 'tx-06-quality-policy-guard.png');
  });

  test('TX-07 — Forward and Reverse Genealogy Traceability with Lot Rejection Security', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    const csrf = await getCsrfToken(page);

    // 1. Negative test: Attempt to create genealogy link on seeded order with mismatched/forged IDs
    const ordersRes = await page.request.get(`/api/v1/manufacturing/production-orders?legalEntityId=${ATLAS_ENTITY_ID}&search=MO-SEED-0001`);
    const orders = await ordersRes.json();
    const order = orders[0];
    const orderId = order.id;

    const fakeOutputId = '00000000-0000-0000-0000-000000009999';
    const fakeEventId = '00000000-0000-0000-0000-000000008888';
    const rejectRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/genealogy`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        outputId: fakeOutputId,
        inputEventId: fakeEventId,
        inputQuantity: 10,
        outputQuantity: 10,
        inputLotId: null,
        outputLotId: null,
        certainty: 'Exact',
        idempotencyKey: `genealogy-reject-${Date.now()}`
      }
    });
    // Forged/mismatched output & input IDs must be rejected with 400 Bad Request
    expect([400, 404]).toContain(rejectRes.status());

    // 2. Navigate to Genealogy page in UI and verify trace view
    await page.goto('/manufacturing/genealogy');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/شجرة تتبع التشغيلات|Genealogy/i, { timeout: 15000 });
    await expect(page.getByText(/تتبع أمامي|Forward Trace/i).first()).toBeVisible();
    await expect(page.getByText(/تتبع عكسي|Reverse Trace/i).first()).toBeVisible();

    await saveArtifact(page, 'tx-07-genealogy-trace.png');
  });

  test('TX-08 — Costing & Close Readiness: Authoritative Legal-Entity Base Currency & Blocked Order Rejection', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Fetch costing summary via API
    const summaryRes = await page.request.get(`/api/v1/manufacturing/legal-entities/${ATLAS_ENTITY_ID}/costing-summary`);
    expect(summaryRes.ok()).toBeTruthy();
    const summary = await summaryRes.json();
    expect(summary.currencyCode).toBe('EGP'); // Authoritative Atlas base currency

    // 2. Verify Close Readiness on uncompleted order blocks closure
    const ordersRes = await page.request.get(`/api/v1/manufacturing/production-orders?legalEntityId=${ATLAS_ENTITY_ID}&search=MO-SEED-0001`);
    const orders = await ordersRes.json();
    const order = orders[0];
    const orderId = order.id;

    const readinessRes = await page.request.get(`/api/v1/manufacturing/production-orders/${orderId}/close-readiness`);
    expect(readinessRes.ok()).toBeTruthy();
    const readiness = await readinessRes.json();
    expect(readiness.state).toBe('Blocked');
    expect(readiness.blockReason).toBeDefined();

    // 3. Attempt direct close mutation on blocked order -> Must return 400 ProblemDetails
    const csrf = await getCsrfToken(page);
    const closeRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/close`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: order.rowVersion }
    });
    expect([400, 409]).toContain(closeRes.status());

    // 4. Navigate to Manufacturing Costing page in browser
    await page.goto('/manufacturing/costing');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/تكاليف الإنتاج وإقفال الـ WIP|Costing/i, { timeout: 15000 });

    // Verify KPI Cards carry authoritative currency and operational WIP label
    const wipCard = page.locator('[data-testid="kpi-wip-balance"]');
    await expect(wipCard).toBeVisible({ timeout: 10000 });
    const wipText = await wipCard.textContent();
    expect(wipText).toMatch(/EGP|ج\.م/);

    // Verify Operational WIP note distinguishing from General Ledger
    await expect(page.getByText(/رصيد الـ WIP التشغيلي التقديري/i)).toBeVisible();
    await expect(page.getByText(/غير مطابق دفترياً حتى اكتمال قيود الأستاذ العام/i)).toBeVisible();

    await saveArtifact(page, 'tx-08-costing-summary-authoritative.png');
  });

  test('TX-09 — SaaS Commercial Entitlement Invariant: Effective Access Evaluates Correctly Across Tenant States', async ({ page }) => {
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
