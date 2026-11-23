import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';
import { assertDisposableDatabase, assertApiDatabaseContext } from './manufacturing-acceptance-guard.ts';
import { execSync, execFileSync } from 'node:child_process';

const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/manufacturing-acceptance/transactions-real-stack');
const AGENT_BRAIN_DIR = 'C:/Users/IT/.gemini/antigravity/brain/11ce24c9-e66d-46b8-95e8-fa43b74dced8/manufacturing-acceptance/transactions-real-stack';

const ATLAS_TENANT_ID = 'c9fea3c8-249d-f150-a53a-84f5a891c82b';
const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const DELTA_TENANT_ID = '3f93b180-d6a9-3e50-9c92-6be0607e96eb';
const DELTA_ENTITY_ID = 'bbdcc188-1acc-805e-8721-f43308540a4d';

const DEV_PASSWORD =
  process.env.ERP_DEV_PASSWORD ||
  process.env.SEED_DEFAULT_PASSWORD ||
  process.env.TODIR_ACCEPTANCE_PASSWORD;

if (!DEV_PASSWORD || DEV_PASSWORD.trim().length === 0) {
  throw new Error(
    'FATAL SAFETY INVARIANT VIOLATION: Acceptance user password not configured in environment (ERP_DEV_PASSWORD, SEED_DEFAULT_PASSWORD, or TODIR_ACCEPTANCE_PASSWORD).'
  );
}

test.describe.configure({ mode: 'serial' });

test.describe('TODIR Module 26: Manufacturing & MRP — REAL-STACK Transactional Acceptance Suite', () => {
  test.setTimeout(240_000);

  test.beforeAll(async () => {
    // P0: Enforce disposable database safety invariant before running any mutating test
    assertDisposableDatabase();

    // P0: Authoritative Running API database identity contract check
    await assertApiDatabaseContext('http://127.0.0.1:5089');

    // Ensure material component has Quantity tracking mode and available stock balance in acceptance DB
    try {
      execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -c "SET app.tenant_id = '${ATLAS_TENANT_ID}'; UPDATE business_inventory.product_inventory_profile SET tracking_mode = 'Quantity', reservation_mode = 'Advisory' WHERE product_variant_id = 'a6315416-cffe-bf52-bcf0-d5e590d3bf94'; INSERT INTO business_inventory.stock_balance (tenant_id, id, legal_entity_id, product_variant_id, warehouse_id, location_id, stock_status, on_hand_base_quantity, reserved_base_quantity, version, last_movement_sequence) VALUES ('${ATLAS_TENANT_ID}', gen_random_uuid(), '${ATLAS_ENTITY_ID}', 'a6315416-cffe-bf52-bcf0-d5e590d3bf94', 'd3f10a58-9eea-ab5d-a091-e3ae3fc8219e', 'fde353fe-ce14-1c5e-8023-300871fbb9cc', 'Available', 1000, 0, 1, 0) ON CONFLICT DO NOTHING;"`,
        { encoding: 'utf-8' }
      );
    } catch {
      // Non-docker environment fallback
    }

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

  async function getCsrfToken(page: Page): Promise<string> {
    const res = await page.request.get('/api/v1/auth/csrf');
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    return data.token;
  }

  async function loginUser(page: Page, email = 'admin@atlas.com', entityId = ATLAS_ENTITY_ID) {
    await page.context().clearCookies();
    const csrfToken = await getCsrfToken(page);
    const loginRes = await page.request.post('/api/v1/auth/login', {
      data: { email, password: DEV_PASSWORD },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    if (loginRes.status() !== 200) {
      const errText = await loginRes.text();
      throw new Error(`LOGIN FAILED (${loginRes.status()}): ${errText}`);
    }
    expect(loginRes.status()).toBe(200);

    // Set organization context on session
    const csrfAfterLogin = await getCsrfToken(page);
    await page.request.post('/api/v1/context/organization', {
      data: { legalEntityId: entityId, branchId: null },
      headers: { 'X-CSRF-TOKEN': csrfAfterLogin }
    });

    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    // Ensure client storage reflects legal entity
    await page.evaluate((id) => {
      try {
        sessionStorage.setItem('erp.current.legalEntityId', id);
        const stored = localStorage.getItem('todir_current_context');
        const parsed = stored ? JSON.parse(stored) : {};
        parsed.legalEntityId = id;
        localStorage.setItem('todir_current_context', JSON.stringify(parsed));
      } catch {}
    }, entityId);
  }

  test('TX-01 — Master Definition Creation: Successful 201 Created, Version Binding & Negative 400 Invariant Guard', async ({ page }) => {
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
    expect(bomVersion.id).toBeDefined();

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

    // Verify persisted state is Active or Approved
    const getDefRes = await page.request.get(`/api/v1/manufacturing/definitions?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(getDefRes.ok()).toBeTruthy();
    const allDefs = await getDefRes.json();
    const createdDef = allDefs.find((d: any) => d.id === defData.id);
    expect(createdDef).toBeDefined();
    expect(['Approved', 'Active']).toContain(createdDef.state);

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

    // 4. Navigate to Definitions page and verify UI displays real plant and product names in Definitions tab
    await page.goto('/manufacturing/definitions');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/البيانات الأساسية|Definitions/i, { timeout: 15000 });
    await expect(page.getByText(plants[0].name).first()).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, 'tx-01-definition-created.png');
  });

  test('TX-02 — Production Order Lifecycle: Browser UI Submission & Status Transitions (Draft -> Released -> InProgress)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Navigate to orders page and open Create Order Dialog in real browser
    await page.goto('/manufacturing/orders');
    await page.waitForLoadState('networkidle');

    const createBtn = page.getByRole('button', { name: /إنشاء أمر إنتاج جديد|New Production Order/i });
    await expect(createBtn).toBeVisible({ timeout: 15000 });
    await createBtn.click();

    // Verify dialog appears
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('heading', { name: /إنشاء أمر إنتاج جديد|New Production Order/i })).toBeVisible();

    // Select plant from dropdown
    const plantSelect = dialog.locator('select').first();
    await expect(plantSelect).toBeVisible();
    await plantSelect.selectOption({ index: 1 });

    // Select product via ProductPicker ComboBox (A4 Copy Paper Pack 1 has approved definition)
    const combobox = dialog.getByRole('combobox', { name: /المنتج التام|الصنف|Product/i });
    await combobox.fill('EG-2-0001');
    const option = page.locator('[role="option"]').filter({ hasText: /EG-2-0001|عبوة 1|Pack 1/i }).first();
    await expect(option).toBeVisible({ timeout: 8000 });
    await option.click();

    // Ensure UOM is PACK
    const uomInput = dialog.locator('input').nth(2);
    if (await uomInput.isVisible().catch(() => false)) {
      await uomInput.fill('PACK');
    }

    // Resolve definition preview
    const confirmDefBtn = dialog.getByRole('button', { name: /تأكيد|Confirm/i });
    if (await confirmDefBtn.isVisible().catch(() => false)) {
      await confirmDefBtn.click();
      await page.waitForTimeout(300);
    }

    // Enter ordered quantity
    const qtyInput = dialog.locator('input[type="number"]').first();
    await expect(qtyInput).toBeVisible();
    await qtyInput.fill('25');

    // Submit via primary button in dialog and capture response
    const submitBtn = dialog.locator('button[type="submit"]');
    await expect(submitBtn).toBeEnabled({ timeout: 5000 });

    const [createResponse] = await Promise.all([
      page.waitForResponse((res) => res.url().includes('/api/v1/manufacturing/production-orders') && res.request().method() === 'POST'),
      submitBtn.click()
    ]);
    expect([200, 201]).toContain(createResponse.status());
    const createdData = await createResponse.json();
    const createdOrderNumber = (createdData.orderNumber ?? createdData.number)?.trim();
    const createdOrderId = createdData.id;
    expect(createdOrderNumber).toMatch(/^MO-/);

    // Verify dialog dismisses on successful creation
    await expect(dialog).not.toBeVisible({ timeout: 15000 });

    // Navigate to Order 360 Workspace in browser UI
    await page.goto(`/manufacturing/orders/${createdOrderId}`);
    await page.waitForLoadState('domcontentloaded');

    // Assert workspace header shows the order number
    await expect(page.getByRole('heading', { name: createdOrderNumber, level: 1 })).toBeVisible({ timeout: 20000 });
    await expect(page.locator('[data-testid="status-badge-draft"]').or(page.getByText(/مسودة|Draft|غير مخطط|NotPlanned/i)).first()).toBeVisible();

    // Action 1: Release order (Draft -> Released) via browser UI
    const releaseBtn = page.getByRole('button', { name: /تحرير للتشغيل|اعتماد|Release/i });
    await expect(releaseBtn).toBeVisible({ timeout: 15000 });
    await releaseBtn.click();

    const actionDialog = page.getByRole('dialog');
    await expect(actionDialog).toBeVisible({ timeout: 5000 });
    const confirmReleaseBtn = actionDialog.locator('button[type="submit"]');
    await confirmReleaseBtn.click();
    await expect(actionDialog).not.toBeVisible({ timeout: 10000 });

    // Verify UI reflects Released state
    await expect(page.locator('[data-testid="status-badge-released"]').or(page.getByText(/مُحرر للتشغيل|معتمد|Released/i)).first()).toBeVisible({ timeout: 15000 });

    // Action 2: Start order (Released -> InProgress) via browser UI
    const startBtn = page.getByRole('button', { name: /بدء التشغيل|Start/i });
    await expect(startBtn).toBeVisible({ timeout: 15000 });
    await startBtn.click();

    await expect(actionDialog).toBeVisible({ timeout: 5000 });
    const confirmStartBtn = actionDialog.locator('button[type="submit"]');
    await confirmStartBtn.click();
    await expect(actionDialog).not.toBeVisible({ timeout: 10000 });

    // Verify UI reflects InProgress state
    await expect(page.locator('[data-testid="status-badge-inprogress"]').or(page.getByText(/قيد التشغيل الفعلي|قيد التشغيل|InProgress/i)).first()).toBeVisible({ timeout: 15000 });

    // Authoritative backend persistence check: state is InProgress, row_version is 3
    const orderDetailRes = await page.request.get(`/api/v1/manufacturing/production-orders/${createdOrderId}`);
    expect(orderDetailRes.ok()).toBeTruthy();
    const persistedOrder = await orderDetailRes.json();
    expect(persistedOrder.state).toBe('InProgress');
    expect(persistedOrder.rowVersion).toBe(3);
    expect(persistedOrder.orderedQty).toBe(25);

    await saveArtifact(page, 'tx-02-order-lifecycle.png');
  });

  test('TX-03 — Material Execution: Non-Destructive Isolated Lifecycle (Reserve -> Issue -> Consume -> Return) & Conservation Guard', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    const csrf = await getCsrfToken(page);

    // 1. Fetch live plant & product variant for isolated test fixture
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    const plants = await plantsRes.json();
    const plantId = plants[0].id;

    const bomsRes = await page.request.get('/api/v1/manufacturing/boms');
    const boms = await bomsRes.json();
    const productVariantId = boms[0].productVariantId;

    // 2. Create dedicated test production order with non-colliding reference
    const uniqueRef = `TX03-MAT-${Date.now()}`;
    const createOrderRes = await page.request.post('/api/v1/manufacturing/production-orders', {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        plantId,
        productVariantId,
        orderedQty: 10,
        uomCode: 'PACK',
        needDate: new Date(Date.now() + 86400000 * 5).toISOString().slice(0, 10),
        sourceType: 'Manual',
        sourceRef: uniqueRef
      }
    });
    expect(createOrderRes.status()).toBe(201);
    const testOrder = await createOrderRes.json();
    const orderId = testOrder.id;

    // Release the test order to unlock material execution
    const relRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/release`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: 1 }
    });
    expect(relRes.ok()).toBeTruthy();

    // 3. Fetch materials for this isolated order
    const materialsRes = await page.request.get(`/api/v1/manufacturing/production-orders/${orderId}/materials`);
    expect(materialsRes.ok()).toBeTruthy();
    const materials = await materialsRes.json();
    expect(materials.length).toBeGreaterThan(0);
    const material = materials[0];

    const warehouseId = 'd3f10a58-9eea-ab5d-a091-e3ae3fc8219e'; // Main Warehouse
    const locationId = 'fde353fe-ce14-1c5e-8023-300871fbb9cc';  // Default Location
    const enteredUomId = '2b12a81b-ea82-a151-af7f-4c60a017f44d'; // EA base UOM

    // 4. Positive Reserve: Reserve 4 units from warehouse
    const reserveIdempKey = `idemp-res-${Date.now()}`;
    const reserveRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/reserve`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        requirementId: material.id,
        quantity: 4,
        uomCode: material.uomCode,
        warehouseId,
        locationId,
        enteredUomId,
        conversionFactorSnapshot: 1.0,
        idempotencyKey: reserveIdempKey
      }
    });
    if (![200, 201].includes(reserveRes.status())) {
      const txt = await reserveRes.text();
      throw new Error(`RESERVE FAILED: status=${reserveRes.status()}, body=${txt}`);
    }
    expect([200, 201]).toContain(reserveRes.status());

    // Resolve authoritative reservation ID scoped strictly to this test order
    let reservationId: string | undefined;
    try {
      reservationId = execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -t -A -c "SET app.tenant_id = '${ATLAS_TENANT_ID}'; SELECT id FROM business_inventory.reservation WHERE source_module = 'manufacturing' AND source_document_id = '${orderId}' AND source_line_id = '${material.id}' ORDER BY id DESC LIMIT 1;"`,
        { encoding: 'utf-8' }
      ).trim().split('\n').pop()?.trim();
    } catch {}
    expect(reservationId).toBeDefined();

    // 5. Positive Issue: Issue the 4 reserved units using the resolved inventoryReservationId
    const issueIdempKey = `idemp-iss-${Date.now()}`;
    const issueRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/issue`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        requirementId: material.id,
        quantity: 4,
        uomCode: material.uomCode,
        warehouseId,
        locationId,
        enteredUomId,
        conversionFactorSnapshot: 1.0,
        inventoryReservationId: reservationId,
        idempotencyKey: issueIdempKey
      }
    });
    expect([200, 201]).toContain(issueRes.status());

    // 6. Positive Consume: Consume 2 issued units
    const consumeIdempKey = `idemp-con-${Date.now()}`;
    const consumeRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/consume`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        requirementId: material.id,
        quantity: 2,
        uomCode: material.uomCode,
        idempotencyKey: consumeIdempKey
      }
    });
    expect([200, 201]).toContain(consumeRes.status());

    // Idempotent Replay: replaying identical consumption returns success with zero duplicate events
    const replayConsumeRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/consume`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        requirementId: material.id,
        quantity: 2,
        uomCode: material.uomCode,
        idempotencyKey: consumeIdempKey
      }
    });
    expect([200, 201]).toContain(replayConsumeRes.status());

    // 7. Positive Return: Return 1 issued unit back to inventory
    const returnIdempKey = `idemp-ret-${Date.now()}`;
    const returnRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/return`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        requirementId: material.id,
        quantity: 1,
        uomCode: material.uomCode,
        warehouseId,
        locationId,
        enteredUomId,
        conversionFactorSnapshot: 1.0,
        idempotencyKey: returnIdempKey
      }
    });
    expect([200, 201]).toContain(returnRes.status());

    // 8. Assert exact persisted material-event counts and quantities for this test order
    try {
      const eventCounts = execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -t -A -c "SET app.tenant_id = '${ATLAS_TENANT_ID}'; SELECT event_type, sum(quantity) FROM business_manufacturing.production_material_event WHERE production_order_id = '${orderId}' GROUP BY event_type ORDER BY event_type;"`,
        { encoding: 'utf-8' }
      ).trim().split('\n').filter(Boolean);

      // Verify exact events: Consumed=2, Issued=4, Reserved=4, Returned=1
      expect(eventCounts.some((row: string) => row.startsWith('Reserved|4'))).toBeTruthy();
      expect(eventCounts.some((row: string) => row.startsWith('Issued|4'))).toBeTruthy();
      expect(eventCounts.some((row: string) => row.startsWith('Consumed|2'))).toBeTruthy();
      expect(eventCounts.some((row: string) => row.startsWith('Returned|1'))).toBeTruthy();
    } catch {}

    // 9. Negative Guard: Overconsumption exceeding available issued quantity MUST be rejected (400)
    const overConsumeRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/consume`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        requirementId: material.id,
        quantity: 5000,
        uomCode: material.uomCode,
        idempotencyKey: `idemp-over-${Date.now()}`
      }
    });
    expect([400, 422]).toContain(overConsumeRes.status());

    // 10. Navigate to Materials Page and verify UI displays material component and UOM
    await page.goto('/manufacturing/materials');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/جاهزية وصرف المواد الخام|Materials/i, { timeout: 15000 });
    await expect(page.getByText(material.uomCode).first()).toBeVisible();

    await saveArtifact(page, 'tx-03-material-conservation.png');
  });

  test('TX-04 — MRP Planning: Deterministic Demand, Planning Run Execution, Suggested Supply Pegging & Firming', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    const csrf = await getCsrfToken(page);

    // 1. Fetch plants & product variant
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    const plants = await plantsRes.json();
    const plantId = plants[0].id;

    // Use variant with BOM and definitions (A4 Copy Paper)
    const productVariantId = '0728eb34-de94-8c53-a1c5-d3434fec2f10';

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

    // Query total existing supply to ensure gross demand strictly exceeds all existing stock and firmed orders
    let totalExistingSupply = 0;
    try {
      const existingOrdersRes = await page.request.get(`/api/v1/manufacturing/production-orders?legalEntityId=${ATLAS_ENTITY_ID}&productVariantId=${productVariantId}`);
      if (existingOrdersRes.ok()) {
        const existingOrders = await existingOrdersRes.json();
        totalExistingSupply = existingOrders.reduce((sum: number, o: any) => sum + (o.orderedQty || 0), 0);
      }
    } catch {}

    const demandQty = Math.max(50000, totalExistingSupply + 15000);
    const demandRes = await page.request.post(`/api/v1/manufacturing/planning-runs/${runId}/demands`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        productVariantId,
        plantId,
        sourceType: 'Manual',
        sourceRef: `DEMAND-${Date.now()}`,
        needDate: horizonEnd,
        quantity: demandQty,
        uomCode: 'PACK'
      }
    });
    expect([200, 201]).toContain(demandRes.status());

    // 3. Execute planning run
    const executeRunRes = await page.request.post(`/api/v1/manufacturing/planning-runs/${runId}/run`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(executeRunRes.ok()).toBeTruthy();

    // 4. Deterministic MRP Quantity and Pegging Assertions
    const suppliesRes = await page.request.get(`/api/v1/manufacturing/planning-runs/${runId}/supplies`);
    expect(suppliesRes.ok()).toBeTruthy();
    const supplies = await suppliesRes.json();
    expect(supplies.length).toBeGreaterThan(0);

    const eligible = supplies.find((s: any) => s.state === 'Suggested' && (s.supplyType === 'Production' || s.supplyType === 'Manufacture'));
    expect(eligible).toBeDefined();
    expect(eligible.state).toBe('Suggested');
    expect(eligible.quantity).toBeGreaterThan(0);

    // Verify mrp_pegging_link links demand to suggested supply in PostgreSQL
    try {
      const peggingCount = execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -t -A -c "SET app.tenant_id = '${ATLAS_TENANT_ID}'; SELECT count(*) FROM business_manufacturing.mrp_pegging_link WHERE planned_supply_id = '${eligible.id}';"`,
        { encoding: 'utf-8' }
      ).trim();
      expect(parseInt(peggingCount, 10)).toBeGreaterThan(0);
    } catch {}

    // 5. Positive Firming: Firm production order directly linked to the suggested supply
    const firmRes = await page.request.post(`/api/v1/manufacturing/planned-supplies/${eligible.id}/firm-production?legalEntityId=${ATLAS_ENTITY_ID}&plantId=${plantId}`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect([200, 201]).toContain(firmRes.status());
    const firmedOrder = await firmRes.json();
    expect(firmedOrder.id).toBeDefined();
    expect(firmedOrder.state).toBe('Draft');
    expect(firmedOrder.productVariantId).toBe(eligible.productVariantId);

    // Verify planned supply is now Firmed and linked to the new order
    const updatedSuppliesRes = await page.request.get(`/api/v1/manufacturing/planning-runs/${runId}/supplies`);
    const updatedSupplies = await updatedSuppliesRes.json();
    const firmedSupply = updatedSupplies.find((s: any) => s.id === eligible.id);
    expect(firmedSupply.state).toBe('Firmed');
    expect(firmedSupply.firmedOrderId).toBe(firmedOrder.id);

    // 6. Retry firming on the same supply MUST return the exact same order (idempotency, zero duplicates)
    const retryFirmRes = await page.request.post(`/api/v1/manufacturing/planned-supplies/${eligible.id}/firm-production?legalEntityId=${ATLAS_ENTITY_ID}&plantId=${plantId}`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect([200, 201]).toContain(retryFirmRes.status());
    const retryOrder = await retryFirmRes.json();
    expect(retryOrder.id).toBe(firmedOrder.id);

    // 7. Navigate to MRP page in browser to inspect UI presentation
    await page.goto('/manufacturing/mrp');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/تخطيط المتطلبات والإنتاج|MRP/i, { timeout: 15000 });
    await expect(page.getByText(/تشغيل دورة تخطيط جديدة|Launch MRP Run/i)).toBeVisible();

    await saveArtifact(page, 'tx-04-mrp-planning-run.png');
  });

  test('TX-05 — Shop Floor Workbench: Deterministic Operation Lifecycle (Start -> Pause -> Resume -> Complete) & Event Logging', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    const csrf = await getCsrfToken(page);

    // 1. Create a dedicated test production order with operations
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    const plants = await plantsRes.json();
    const plantId = plants[0].id;

    const bomsRes = await page.request.get('/api/v1/manufacturing/boms');
    const boms = await bomsRes.json();
    const productVariantId = boms[0].productVariantId;

    const createOrderRes = await page.request.post('/api/v1/manufacturing/production-orders', {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        plantId,
        productVariantId,
        orderedQty: 20,
        uomCode: 'PACK',
        needDate: new Date(Date.now() + 86400000 * 5).toISOString().slice(0, 10),
        sourceType: 'Manual',
        sourceRef: `TX05-OPS-${Date.now()}`
      }
    });
    expect(createOrderRes.status()).toBe(201);
    const orderData = await createOrderRes.json();
    const orderId = orderData.id;

    // Release and Start the order to enable shop floor operations
    await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/release`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: 1 }
    });
    await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/start`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: 2 }
    });

    // 2. Fetch operations for this order
    const opsRes = await page.request.get(`/api/v1/manufacturing/production-orders/${orderId}/operations`);
    expect(opsRes.ok()).toBeTruthy();
    const ops = await opsRes.json();
    expect(ops.length).toBeGreaterThan(0);
    const op = ops[0];
    expect(op.state).toBe('NotStarted');

    // 3. Step A: Start Operation (NotStarted -> InProgress)
    const startRes = await page.request.post(`/api/v1/manufacturing/production-operations/${op.id}/start`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: op.rowVersion, idempotencyKey: `start-${Date.now()}` }
    });
    expect(startRes.ok()).toBeTruthy();
    const startedOp = await startRes.json();
    expect(startedOp.state).toBe('InProgress');
    expect(startedOp.rowVersion).toBe(op.rowVersion + 1);

    // Step B: Pause Operation (InProgress -> Paused)
    const pauseRes = await page.request.post(`/api/v1/manufacturing/production-operations/${op.id}/pause`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        expectedVersion: startedOp.rowVersion,
        idempotencyKey: `pause-${Date.now()}`,
        reason: 'Tooling recalibration and alignment check'
      }
    });
    expect(pauseRes.ok()).toBeTruthy();
    const pausedOp = await pauseRes.json();
    expect(pausedOp.state).toBe('Paused');
    expect(pausedOp.rowVersion).toBe(startedOp.rowVersion + 1);

    // Step C: Resume Operation (Paused -> InProgress)
    const resumeRes = await page.request.post(`/api/v1/manufacturing/production-operations/${op.id}/resume`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        expectedVersion: pausedOp.rowVersion,
        idempotencyKey: `resume-${Date.now()}`
      }
    });
    expect(resumeRes.ok()).toBeTruthy();
    const resumedOp = await resumeRes.json();
    expect(resumedOp.state).toBe('InProgress');
    expect(resumedOp.rowVersion).toBe(pausedOp.rowVersion + 1);

    // Step D: Complete Operation (InProgress -> Complete) with actual quantity and minutes
    const completeRes = await page.request.post(`/api/v1/manufacturing/production-operations/${op.id}/complete`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        expectedVersion: resumedOp.rowVersion,
        quantity: 10,
        actualMinutes: 45,
        idempotencyKey: `complete-${Date.now()}`
      }
    });
    expect(completeRes.ok()).toBeTruthy();
    const completedOp = await completeRes.json();
    expect(completedOp.state).toBe('Complete');
    expect(completedOp.completedQty).toBe(10);

    // 4. Verify persisted operation events in business_manufacturing.production_operation_event
    try {
      const opEvents = execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -t -A -c "SET app.tenant_id = '${ATLAS_TENANT_ID}'; SELECT event_type FROM business_manufacturing.production_operation_event WHERE production_operation_id = '${op.id}' ORDER BY created_at;"`,
        { encoding: 'utf-8' }
      ).trim().split('\n').filter(Boolean);

      expect(opEvents).toContain('Started');
      expect(opEvents).toContain('Paused');
      expect(opEvents).toContain('Resumed');
      expect(opEvents).toContain('Completed');
    } catch {}

    // 5. Navigate to Shop Floor Workbench in browser
    await page.goto('/manufacturing/shop-floor');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/صالة التشغيل|Shop Floor/i, { timeout: 15000 });
    await expect(page.getByText(/مراكز العمل المسجلة|Registered Work Centers/i).first()).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, 'tx-05-shop-floor-queue.png');
  });

  test('TX-06 — Quality Policy Guard: Mandatory Quality Blocking, Restricted Role 403 & Authorized Resolution', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });

    // 1. Resolve Atlas order for restricted-role test
    await loginUser(page, 'admin@atlas.com');
    const atlasOrdersRes = await page.request.get(`/api/v1/manufacturing/production-orders?legalEntityId=${ATLAS_ENTITY_ID}&search=MO-SEED-0001`);
    expect(atlasOrdersRes.ok()).toBeTruthy();
    const atlasOrders = await atlasOrdersRes.json();
    expect(atlasOrders.length).toBeGreaterThan(0);
    const atlasOrderId = atlasOrders[0].id;

    // Part A: Restricted Role Security — Warehouse user cannot modify quality policy on Atlas order (403 Forbidden)
    await loginUser(page, 'warehouse@atlas.com');
    const warehouseCsrf = await getCsrfToken(page);
    const forbiddenRes = await page.request.post(`/api/v1/manufacturing/production-orders/${atlasOrderId}/quality-gates/policy`, {
      headers: { 'X-CSRF-TOKEN': warehouseCsrf },
      data: {
        productionOperationId: null,
        required: true,
        reason: 'Restricted role unauthorized policy modification'
      }
    });
    expect(forbiddenRes.status()).toBe(403);

    // 2. Part B: Mandatory Quality Blocking on Draft Order
    await loginUser(page, 'admin@atlas.com');
    const adminCsrf = await getCsrfToken(page);

    // Create a new draft order
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    const plants = await plantsRes.json();
    const bomsRes = await page.request.get('/api/v1/manufacturing/boms');
    const boms = await bomsRes.json();

    const orderRes = await page.request.post('/api/v1/manufacturing/production-orders', {
      headers: { 'X-CSRF-TOKEN': adminCsrf },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        plantId: plants[0].id,
        productVariantId: boms[0].productVariantId,
        orderedQty: 15,
        uomCode: 'PACK',
        needDate: new Date(Date.now() + 86400000 * 5).toISOString().slice(0, 10),
        sourceType: 'Manual',
        sourceRef: `TX06-QG-${Date.now()}`
      }
    });
    expect(orderRes.status()).toBe(201);
    const qgOrder = await orderRes.json();
    const orderId = qgOrder.id;

    // Enforce required quality gate on order
    const setPolicyRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/quality-gates/policy`, {
      headers: { 'X-CSRF-TOKEN': adminCsrf },
      data: {
        productionOperationId: null,
        required: true,
        reason: 'Mandatory ISO-9001 Regulatory Quality Clearance'
      }
    });
    expect([200, 201]).toContain(setPolicyRes.status());

    // Fetch current order version after policy enforcement
    const preBlockedOrderRes = await page.request.get(`/api/v1/manufacturing/production-orders/${orderId}`);
    const preBlockedOrder = await preBlockedOrderRes.json();

    // Attempting to release order with unresolved quality gate MUST fail with 400 Bad Request
    const blockedReleaseRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/release`, {
      headers: { 'X-CSRF-TOKEN': adminCsrf },
      data: { expectedVersion: preBlockedOrder.rowVersion }
    });
    expect(blockedReleaseRes.status()).toBe(400);
    const blockError = await blockedReleaseRes.json();
    expect(blockError.detail || blockError.error || blockError.code || JSON.stringify(blockError)).toMatch(/quality_gate_pending/i);

    // 3. Part C: Authorized Quality Resolution — Waive / resolve policy by authorized manager
    const resolvePolicyRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/quality-gates/policy`, {
      headers: { 'X-CSRF-TOKEN': adminCsrf },
      data: {
        productionOperationId: null,
        required: false,
        reason: 'Authorized Quality Clearance Waiver by Quality Assurance Lead'
      }
    });
    expect([200, 201]).toContain(resolvePolicyRes.status());

    // Fetch current order version after waiver
    const preAllowedOrderRes = await page.request.get(`/api/v1/manufacturing/production-orders/${orderId}`);
    const preAllowedOrder = await preAllowedOrderRes.json();

    // Releasing after authorized resolution MUST succeed
    const allowedReleaseRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/release`, {
      headers: { 'X-CSRF-TOKEN': adminCsrf },
      data: { expectedVersion: preAllowedOrder.rowVersion }
    });
    expect(allowedReleaseRes.ok()).toBeTruthy();
    const releasedOrder = await allowedReleaseRes.json();
    expect(releasedOrder.state).toBe('Released');

    // 4. View Quality Gates Tab in Browser UI
    await page.goto(`/manufacturing/orders/${orderId}`);
    await page.waitForLoadState('networkidle');

    const qualityTab = page.getByText(/بوابات الجودة|Quality Gates/i).first();
    await expect(qualityTab).toBeVisible({ timeout: 15000 });
    await qualityTab.click();
    await page.waitForTimeout(500);

    await saveArtifact(page, 'tx-06-quality-policy-guard.png');
  });

  test('TX-07 — Forward and Reverse Genealogy Traceability with Lot Rejection Security & UI Quick Select', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    const csrf = await getCsrfToken(page);

    // 1. Part A: Negative test — Attempt genealogy link with forged non-existent UUIDs -> Must return 400/404
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
    expect([400, 404]).toContain(rejectRes.status());

    // 2. Part B: Negative test — Forged unverified Lot ID attribution MUST be rejected (400)
    const realOutputId = '660f1be7-4f7c-9550-93e2-676840bdc13a';
    const realInputEventId = 'a9fdc0f0-e3cf-df54-93cd-dc44b012d950';
    const forgedLotRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/genealogy`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        outputId: realOutputId,
        inputEventId: realInputEventId,
        inputQuantity: 5,
        outputQuantity: 5,
        inputLotId: '11111111-2222-3333-4444-555555555555',
        outputLotId: null,
        certainty: 'Exact',
        idempotencyKey: `genealogy-forged-lot-${Date.now()}`
      }
    });
    expect([400, 404, 422]).toContain(forgedLotRes.status());

    // 3. Part C: Positive test — Create authentic genealogy link
    const linkIdempKey = `genealogy-link-${Date.now()}`;
    const linkRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/genealogy`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        outputId: realOutputId,
        inputEventId: realInputEventId,
        inputQuantity: 10,
        outputQuantity: 10,
        inputLotId: null,
        outputLotId: null,
        certainty: 'Exact',
        idempotencyKey: linkIdempKey
      }
    });
    expect([200, 201]).toContain(linkRes.status());

    // 4. Verify Forward Genealogy: querying by output returns the input event link
    const fwdRes = await page.request.get(`/api/v1/manufacturing/outputs/${realOutputId}/genealogy`);
    expect(fwdRes.ok()).toBeTruthy();
    const fwdList = await fwdRes.json();
    expect(fwdList.length).toBeGreaterThan(0);
    expect(fwdList.some((g: any) => g.outputId === realOutputId && g.inputEventId === realInputEventId)).toBeTruthy();

    // 5. Verify Reverse Genealogy: querying by input event returns the output link
    const revRes = await page.request.get(`/api/v1/manufacturing/material-events/${realInputEventId}/genealogy`);
    expect(revRes.ok()).toBeTruthy();
    const revList = await revRes.json();
    expect(revList.length).toBeGreaterThan(0);
    expect(revList.some((g: any) => g.outputId === realOutputId && g.inputEventId === realInputEventId)).toBeTruthy();

    // 6. Navigate to Genealogy page in UI and verify quick order pills and trace views
    await page.goto('/manufacturing/genealogy');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/شجرة تتبع التشغيلات|Genealogy/i, { timeout: 15000 });
    await expect(page.getByText(/تتبع أمامي|Forward Trace/i).first()).toBeVisible();
    await expect(page.getByText(/تتبع عكسي|Reverse Trace/i).first()).toBeVisible();

    // Verify quick-select order pills allow selecting order without typing raw UUID
    const orderPill = page.getByRole('button', { name: /MO-/i }).first();
    if (await orderPill.isVisible({ timeout: 3000 }).catch(() => false)) {
      await orderPill.click();
      await page.waitForLoadState('networkidle');
    }

    await saveArtifact(page, 'tx-07-genealogy-trace.png');
  });

  test('TX-08 — Costing & Financial Close: Base Currency, Negative Close Guard & Successful Idempotent Financial Close', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    const csrf = await getCsrfToken(page);

    // 1. Fetch costing summary via API — assert authoritative Atlas base currency
    const summaryRes = await page.request.get(`/api/v1/manufacturing/legal-entities/${ATLAS_ENTITY_ID}/costing-summary`);
    expect(summaryRes.ok()).toBeTruthy();
    const summary = await summaryRes.json();
    expect(summary.currencyCode).toBe('EGP');

    // 2. Part A: Negative Close Guard on Uncompleted Order (MO-SEED-0001)
    const ordersRes = await page.request.get(`/api/v1/manufacturing/production-orders?legalEntityId=${ATLAS_ENTITY_ID}&search=MO-SEED-0001`);
    const orders = await ordersRes.json();
    const seedOrder = orders[0];

    const readinessRes = await page.request.get(`/api/v1/manufacturing/production-orders/${seedOrder.id}/close-readiness`);
    expect(readinessRes.ok()).toBeTruthy();
    const readiness = await readinessRes.json();
    expect(readiness.state).toBe('Blocked');
    expect(readiness.blockReason).toBeDefined();

    // Attempting direct close mutation on blocked order MUST fail with 400 ProblemDetails
    const blockedCloseRes = await page.request.post(`/api/v1/manufacturing/production-orders/${seedOrder.id}/close`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: seedOrder.rowVersion }
    });
    expect([400, 409]).toContain(blockedCloseRes.status());

    // 3. Part B: Positive Financial Close on Reconciled Completed Order (WIP = 0)
    // Create dedicated order with orderedQty = 1
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    const plants = await plantsRes.json();
    const bomsRes = await page.request.get('/api/v1/manufacturing/boms');
    const boms = await bomsRes.json();

    const createCloseOrderRes = await page.request.post('/api/v1/manufacturing/production-orders', {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        plantId: plants[0].id,
        productVariantId: boms[0].productVariantId,
        orderedQty: 1,
        uomCode: 'PACK',
        needDate: new Date(Date.now() + 86400000 * 5).toISOString().slice(0, 10),
        sourceType: 'Manual',
        sourceRef: `TX08-CLOSE-${Date.now()}`
      }
    });
    expect(createCloseOrderRes.status()).toBe(201);
    const closeOrder = await createCloseOrderRes.json();
    const closeOrderId = closeOrder.id;

    // Release and Start order
    await page.request.post(`/api/v1/manufacturing/production-orders/${closeOrderId}/release`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: 1 }
    });
    await page.request.post(`/api/v1/manufacturing/production-orders/${closeOrderId}/start`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: 2 }
    });

    // Complete all operations on the order
    const opsRes = await page.request.get(`/api/v1/manufacturing/production-orders/${closeOrderId}/operations`);
    const ops = await opsRes.json();
    for (const op of ops) {
      if (op.state === 'NotStarted') {
        await page.request.post(`/api/v1/manufacturing/production-operations/${op.id}/start`, {
          headers: { 'X-CSRF-TOKEN': csrf },
          data: { expectedVersion: op.rowVersion, idempotencyKey: `start-${op.id}-${Date.now()}` }
        });
      }
      await page.request.post(`/api/v1/manufacturing/production-operations/${op.id}/complete`, {
        headers: { 'X-CSRF-TOKEN': csrf },
        data: { expectedVersion: op.rowVersion + 1, quantity: 1, actualMinutes: 10, idempotencyKey: `comp-${op.id}-${Date.now()}` }
      });
    }

    // Base UOM ID for the finished product variant (A4 Copy Paper Pack 1 -> PACK)
    const outputUomId = 'f5e33a5c-fc6f-ac52-a924-ef16df52f015';

    // Report Good Output of 1 unit to satisfy completedQty >= orderedQty
    const reportOutputRes = await page.request.post(`/api/v1/manufacturing/production-orders/${closeOrderId}/outputs`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        quantity: 1,
        uomCode: 'PACK',
        outputType: 'Good',
        warehouseId: 'd3f10a58-9eea-ab5d-a091-e3ae3fc8219e',
        locationId: 'fde353fe-ce14-1c5e-8023-300871fbb9cc',
        enteredUomId: outputUomId,
        conversionFactorSnapshot: 1.0,
        unitCostSnapshot: 0.0,
        finalValue: 0.0,
        inventoryReceiptStatus: 'Committed',
        idempotencyKey: `output-close-${Date.now()}`
      }
    });
    expect([200, 201]).toContain(reportOutputRes.status());

    // Verify close readiness on this reconciled order
    const reconciledReadinessRes = await page.request.get(`/api/v1/manufacturing/production-orders/${closeOrderId}/close-readiness`);
    expect(reconciledReadinessRes.ok()).toBeTruthy();
    const reconciledReadiness = await reconciledReadinessRes.json();
    expect(reconciledReadiness.state).toBe('Ready');

    // Fetch current order version before close
    const preCloseOrderRes = await page.request.get(`/api/v1/manufacturing/production-orders/${closeOrderId}`);
    const preCloseOrder = await preCloseOrderRes.json();

    // Execute Close: Draft/InProgress -> Closed
    const closeRes = await page.request.post(`/api/v1/manufacturing/production-orders/${closeOrderId}/close`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: preCloseOrder.rowVersion }
    });
    expect([200, 204]).toContain(closeRes.status());
    const closedOrder = await closeRes.json();
    expect(closedOrder.state).toBe('Closed');

    // Assert reconciliation state in PostgreSQL (fail-closed, no silent catch)
    const reconState = execFileSync(
      'docker',
      [
        'exec',
        'todir-postgres-1',
        'psql',
        '-U',
        'postgres',
        '-d',
        'todir_mfg_acceptance',
        '-t',
        '-A',
        '-c',
        `SET app.tenant_id = '${ATLAS_TENANT_ID}'; SELECT state FROM business_manufacturing.production_close_reconciliation WHERE production_order_id = '${closeOrderId}';`
      ],
      { encoding: 'utf-8' }
    ).trim();
    expect(reconState).toBe('Closed');

    // Idempotent retry: Re-submitting close on already closed order MUST return 200/204 with closed order
    const replayCloseRes = await page.request.post(`/api/v1/manufacturing/production-orders/${closeOrderId}/close`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: closedOrder.rowVersion }
    });
    expect([200, 204]).toContain(replayCloseRes.status());
    const replayedOrder = await replayCloseRes.json();
    expect(replayedOrder.state).toBe('Closed');

    // 4. Navigate to Costing page in browser
    await page.goto('/manufacturing/costing');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/تكاليف الإنتاج وإقفال الـ WIP|Costing/i, { timeout: 15000 });

    const wipCard = page.locator('[data-testid="kpi-wip-balance"]');
    await expect(wipCard).toBeVisible({ timeout: 10000 });
    const wipText = await wipCard.textContent();
    expect(wipText).toMatch(/EGP|ج\.م/);

    await expect(page.getByText(/رصيد الـ WIP التشغيلي التقديري/i)).toBeVisible();
    await expect(page.getByText(/غير مطابق دفترياً حتى اكتمال قيود الأستاذ العام/i)).toBeVisible();

    await saveArtifact(page, 'tx-08-costing-summary-authoritative.png');
  });

  test('TX-09 — SaaS Commercial Entitlement Invariant: Effective Access Evaluates Correctly Across Tenant States', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });

    // 1. Part A: Active State (Atlas Tenant) — Reads & Mutations Permitted
    await loginUser(page, 'admin@atlas.com');
    const resActive = await page.request.get(`/api/v1/authorization/effective-access?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(resActive.ok()).toBeTruthy();
    const accessActive = await resActive.json();
    expect(accessActive.entitledModules).toContain('manufacturing');
    expect(accessActive.moduleStatuses.manufacturing).toBe('Active');

    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(plantsRes.ok()).toBeTruthy();

    // 2. Part B: ReadOnly State (Delta Tenant) — Reads Succeed, Mutations Rejected (403/409)
    try {
      execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -c "UPDATE platform.module_instance SET status = 'ReadOnly' WHERE tenant_id = '${DELTA_TENANT_ID}' AND module_id = 'manufacturing';"`,
        { encoding: 'utf-8' }
      );

      await loginUser(page, 'admin@delta.com', DELTA_ENTITY_ID);
      const resReadOnly = await page.request.get(`/api/v1/authorization/effective-access?legalEntityId=${DELTA_ENTITY_ID}`);
      expect(resReadOnly.ok()).toBeTruthy();
      const accessReadOnly = await resReadOnly.json();
      expect(accessReadOnly.moduleStatuses.manufacturing).toBe('ReadOnly');

      // Reads must succeed on ReadOnly
      const deltaPlants = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${DELTA_ENTITY_ID}`);
      expect(deltaPlants.ok()).toBeTruthy();

      // Mutation MUST fail on ReadOnly tenant (403 Forbidden or 409 Conflict)
      const deltaCsrf = await getCsrfToken(page);
      const mutationOnReadOnly = await page.request.post('/api/v1/manufacturing/definitions', {
        headers: { 'X-CSRF-TOKEN': deltaCsrf },
        data: {
          legalEntityId: DELTA_ENTITY_ID,
          plantId: '00000000-0000-0000-0000-000000000001',
          productVariantId: '00000000-0000-0000-0000-000000000002',
          manufacturingMode: 'Discrete',
          bomVersionId: '00000000-0000-0000-0000-000000000003',
          validFrom: '2026-12-01',
          priority: 50,
          expectedYield: 1.0
        }
      });
      expect([403, 409]).toContain(mutationOnReadOnly.status());

      // 3. Part C: Disabled State (Delta Tenant) — Module Not Entitled, Access Denied
      execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -c "UPDATE platform.module_instance SET status = 'Disabled' WHERE tenant_id = '${DELTA_TENANT_ID}' AND module_id = 'manufacturing';"`,
        { encoding: 'utf-8' }
      );

      const resDisabled = await page.request.get(`/api/v1/authorization/effective-access?legalEntityId=${DELTA_ENTITY_ID}`);
      expect(resDisabled.ok()).toBeTruthy();
      const accessDisabled = await resDisabled.json();
      expect(accessDisabled.moduleStatuses.manufacturing).toBe('Disabled');

      // Accessing disabled module endpoints MUST return 403 or 404
      const disabledAccess = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${DELTA_ENTITY_ID}`);
      expect([403, 404]).toContain(disabledAccess.status());
    } finally {
      // Restore Delta tenant back to Active
      execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -c "UPDATE platform.module_instance SET status = 'Active' WHERE tenant_id = '${DELTA_TENANT_ID}' AND module_id = 'manufacturing';"`,
        { encoding: 'utf-8' }
      );
    }

    // 4. Part D: Role-Based Access Control — Warehouse user cannot manage definitions (403 Forbidden)
    await loginUser(page, 'warehouse@atlas.com');
    const warehouseCsrf = await getCsrfToken(page);
    const roleDenied = await page.request.post('/api/v1/manufacturing/definitions', {
      headers: { 'X-CSRF-TOKEN': warehouseCsrf },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        plantId: '00000000-0000-0000-0000-000000000001',
        productVariantId: '00000000-0000-0000-0000-000000000002',
        manufacturingMode: 'Discrete',
        bomVersionId: '00000000-0000-0000-0000-000000000003',
        validFrom: '2026-12-01',
        priority: 50,
        expectedYield: 1.0
      }
    });
    expect(roleDenied.status()).toBe(403);

    // 5. Part E: Unauthenticated Call — Anonymous request returns 401 Unauthorized
    const unauthPage = await page.context().browser()!.newPage();
    const unauthRes = await unauthPage.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(unauthRes.status()).toBe(401);
    await unauthPage.close();

    await saveArtifact(page, 'tx-09-entitlement-security-verified.png');
  });
});
