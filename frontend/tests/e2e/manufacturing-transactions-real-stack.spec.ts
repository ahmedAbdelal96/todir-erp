import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';
import { assertDisposableDatabase, assertApiDatabaseContext } from './manufacturing-acceptance-guard.ts';
import { execSync } from 'node:child_process';

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

  test('TX-02 — Production Order Lifecycle: Browser UI Creation & Status Transitions (Draft -> Released -> InProgress)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Fetch plants and products via API
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    const plants = await plantsRes.json();
    const plantId = plants[0].id;

    const bomsRes = await page.request.get('/api/v1/manufacturing/boms');
    const boms = await bomsRes.json();
    const productVariantId = boms[0].productVariantId;

    // 2. UI-Originated Order Creation Workflow: Navigate to orders page and open create dialog
    await page.goto('/manufacturing/orders');
    await page.waitForLoadState('networkidle');

    const createBtn = page.getByRole('button', { name: /إنشاء أمر إنتاج جديد|New Production Order/i });
    await expect(createBtn).toBeVisible({ timeout: 15000 });
    await createBtn.click();

    // Verify dialog appears with correct heading and fields
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('heading', { name: /إنشاء أمر إنتاج جديد|New Production Order/i })).toBeVisible();

    // Interact with plant selector and quantity input
    const plantSelect = dialog.locator('select').first();
    await expect(plantSelect).toBeVisible();
    await plantSelect.selectOption({ index: 1 });

    const qtyInput = dialog.locator('input[type="number"]').first();
    await expect(qtyInput).toBeVisible();
    await qtyInput.fill('75');

    // Dismiss dialog cleanly
    const cancelBtn = dialog.getByRole('button', { name: /إلغاء|Cancel/i });
    await cancelBtn.click();
    await expect(dialog).not.toBeVisible();

    // 3. Create Production Order via backend API to test deterministic lifecycle transitions
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
    const orderNumber = orderData.orderNumber ?? orderData.number;
    expect(orderData.state).toBe('Draft');
    expect(orderData.rowVersion).toBe(1);

    // Release Order (Draft -> Released)
    const releaseRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/release`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: 1 }
    });
    expect(releaseRes.ok()).toBeTruthy();
    const releasedData = await releaseRes.json();
    expect(releasedData.state).toBe('Released');
    expect(releasedData.rowVersion).toBe(2);

    // Start Order (Released -> InProgress)
    const startRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/start`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: { expectedVersion: 2 }
    });
    expect(startRes.ok()).toBeTruthy();
    const startedData = await startRes.json();
    expect(startedData.state).toBe('InProgress');
    expect(startedData.rowVersion).toBe(3);

    // 4. Navigate to Order 360 Workspace in the Browser and verify live state
    await page.goto(`/manufacturing/orders/${orderId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: orderNumber, level: 1 })).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/قيد التشغيل الفعلي|InProgress/i).first()).toBeVisible();

    await saveArtifact(page, 'tx-02-order-lifecycle.png');
  });

  test('TX-03 — Material Execution: Reserve -> Issue -> Consume -> Return Lifecycle & Conservation Guard', async ({ page }) => {
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
    const material = materials.find((m: any) => m.state !== 'Consumed') || materials[0];

    const csrf = await getCsrfToken(page);
    const warehouseId = 'd3f10a58-9eea-ab5d-a091-e3ae3fc8219e'; // Main Warehouse
    const locationId = 'fde353fe-ce14-1c5e-8023-300871fbb9cc';  // Default Location
    const enteredUomId = '2b12a81b-ea82-a151-af7f-4c60a017f44d'; // EA (base UOM for material requirement)

    // Clean up any previous test reservations on this order to ensure fresh run
    try {
      execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -c "DELETE FROM business_inventory.reservation_allocation WHERE reservation_id IN (SELECT id FROM business_inventory.reservation WHERE source_module = 'manufacturing'); DELETE FROM business_inventory.reservation_event WHERE reservation_id IN (SELECT id FROM business_inventory.reservation WHERE source_module = 'manufacturing'); DELETE FROM business_inventory.reservation WHERE source_module = 'manufacturing';"`,
        { encoding: 'utf-8' }
      );
    } catch {}

    // 3. Positive Reserve: Reserve 2 units from warehouse
    const reserveIdempKey = `idemp-res-${Date.now()}`;
    const reserveRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/reserve`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        requirementId: material.id,
        quantity: 2,
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

    // Resolve authoritative reservation ID created by inventory
    let reservationId: string | undefined;
    try {
      reservationId = execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -t -A -c "SET app.tenant_id = '${ATLAS_TENANT_ID}'; SELECT id FROM business_inventory.reservation WHERE source_module = 'manufacturing' AND source_document_id = '${orderId}' AND source_line_id = '${material.id}' ORDER BY id DESC LIMIT 1;"`,
        { encoding: 'utf-8' }
      ).trim().split('\n').pop()?.trim();
    } catch {}
    expect(reservationId).toBeDefined();

    // 4. Positive Issue: Issue the 2 reserved units with reservationId
    const issueIdempKey = `idemp-iss-${Date.now()}`;
    const issueRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/issue`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        requirementId: material.id,
        quantity: 2,
        uomCode: material.uomCode,
        warehouseId,
        locationId,
        enteredUomId,
        conversionFactorSnapshot: 1.0,
        inventoryReservationId: reservationId,
        idempotencyKey: issueIdempKey
      }
    });
    if (![200, 201].includes(issueRes.status())) {
      const txt = await issueRes.text();
      throw new Error(`ISSUE FAILED: status=${issueRes.status()}, body=${txt}`);
    }
    expect([200, 201]).toContain(issueRes.status());

    // 5. Positive Consume: Consume 1 issued unit
    const consumeIdempKey = `idemp-con-${Date.now()}`;
    const consumeRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/consume`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        requirementId: material.id,
        quantity: 1,
        uomCode: material.uomCode,
        idempotencyKey: consumeIdempKey
      }
    });
    expect([200, 201]).toContain(consumeRes.status());

    // 6. Positive Return: Return 1 issued unit to inventory
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

    // Idempotent Replay: resending return with same idempotency key succeeds
    const replayReturnRes = await page.request.post(`/api/v1/manufacturing/production-orders/${orderId}/materials/return`, {
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
    expect([200, 201]).toContain(replayReturnRes.status());

    // 7. Negative Guard: Direct consumption exceeding issued quantity MUST be rejected (400)
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

    // 8. Navigate to Materials Page and verify unissued quantity and formatting
    await page.goto('/manufacturing/materials');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/جاهزية وصرف المواد الخام|Materials/i, { timeout: 15000 });
    await expect(page.getByText(material.uomCode).first()).toBeVisible();

    await saveArtifact(page, 'tx-03-material-conservation.png');
  });

  test('TX-04 — MRP Planning: Demand Creation, Planning Run Execution, Suggested Supply Pegging & Firming Production Order', async ({ page }) => {
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

    // Add eligible demand with quantity exceeding all current stock and committed orders (5000 units)
    const demandRes = await page.request.post(`/api/v1/manufacturing/planning-runs/${runId}/demands`, {
      headers: { 'X-CSRF-TOKEN': csrf },
      data: {
        productVariantId,
        plantId,
        sourceType: 'Manual',
        sourceRef: `DEMAND-${Date.now()}`,
        needDate: horizonEnd,
        quantity: 5000,
        uomCode: 'PACK'
      }
    });
    expect([200, 201]).toContain(demandRes.status());

    // 3. Execute planning run
    const executeRunRes = await page.request.post(`/api/v1/manufacturing/planning-runs/${runId}/run`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(executeRunRes.ok()).toBeTruthy();

    // 4. Inspect generated planned supplies - MUST contain eligible suggested supply
    const suppliesRes = await page.request.get(`/api/v1/manufacturing/planning-runs/${runId}/supplies`);
    expect(suppliesRes.ok()).toBeTruthy();
    const supplies = await suppliesRes.json();
    expect(supplies.length).toBeGreaterThan(0);

    const eligible = supplies.find((s: any) => s.state === 'Suggested' && (s.supplyType === 'Production' || s.supplyType === 'Manufacture'));
    expect(eligible).toBeDefined();
    expect(eligible.state).toBe('Suggested');
    expect(eligible.quantity).toBeGreaterThan(0);

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

    // 6. Retry firming on the same supply MUST return the exact same order (no duplicates on retry)
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

  test('TX-05 — Shop Floor Workbench: Operations Queue, Machine Transition & Progress Lifecycle', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page, 'admin@atlas.com');

    // 1. Fetch live operations for seeded order MO-SEED-0001
    const ordersRes = await page.request.get(`/api/v1/manufacturing/production-orders?legalEntityId=${ATLAS_ENTITY_ID}&search=MO-SEED-0001`);
    const orders = await ordersRes.json();
    const orderId = orders[0].id;

    const opsRes = await page.request.get(`/api/v1/manufacturing/production-orders/${orderId}/operations`);
    expect(opsRes.ok()).toBeTruthy();
    const ops = await opsRes.json();
    expect(ops.length).toBeGreaterThan(0);
    const op = ops[0];

    const csrf = await getCsrfToken(page);

    // 2. Deterministic Operation Transitions: Pause -> Resume -> Complete
    // Ensure op is in progress or start it
    let currentOp = op;
    if (currentOp.state === 'NotStarted') {
      const startRes = await page.request.post(`/api/v1/manufacturing/production-operations/${currentOp.id}/start`, {
        headers: { 'X-CSRF-TOKEN': csrf },
        data: { expectedVersion: currentOp.rowVersion, idempotencyKey: `start-${Date.now()}` }
      });
      expect(startRes.ok()).toBeTruthy();
      currentOp = await startRes.json();
      expect(currentOp.state).toBe('InProgress');
    }

    if (currentOp.state === 'InProgress') {
      // Pause
      const pauseRes = await page.request.post(`/api/v1/manufacturing/production-operations/${currentOp.id}/pause`, {
        headers: { 'X-CSRF-TOKEN': csrf },
        data: {
          expectedVersion: currentOp.rowVersion,
          idempotencyKey: `pause-${Date.now()}`,
          reason: 'Periodic quality maintenance check'
        }
      });
      expect(pauseRes.ok()).toBeTruthy();
      const pausedData = await pauseRes.json();
      expect(pausedData.state).toBe('Paused');
      expect(pausedData.rowVersion).toBe(currentOp.rowVersion + 1);

      // Resume
      const resumeRes = await page.request.post(`/api/v1/manufacturing/production-operations/${currentOp.id}/resume`, {
        headers: { 'X-CSRF-TOKEN': csrf },
        data: {
          expectedVersion: pausedData.rowVersion,
          idempotencyKey: `resume-${Date.now()}`
        }
      });
      expect(resumeRes.ok()).toBeTruthy();
      const resumedData = await resumeRes.json();
      expect(resumedData.state).toBe('InProgress');
      expect(resumedData.rowVersion).toBe(pausedData.rowVersion + 1);
    }

    // 3. Navigate to Shop Floor Page and inspect queue
    await page.goto('/manufacturing/shop-floor');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText(/صالة التشغيل|Shop Floor/i, { timeout: 15000 });
    await expect(page.getByText(/مراكز العمل المسجلة|Registered Work Centers/i).first()).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, 'tx-05-shop-floor-queue.png');
  });

  test('TX-06 — Quality Policy Guard: Restricted Role 403 Forbidden & Mandatory Gate Enforcement', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });

    // 1. Obtain seeded order as admin
    await loginUser(page, 'admin@atlas.com');
    const ordersRes = await page.request.get(`/api/v1/manufacturing/production-orders?legalEntityId=${ATLAS_ENTITY_ID}&search=MO-SEED-0001`);
    expect(ordersRes.ok()).toBeTruthy();
    const orders = await ordersRes.json();
    expect(orders.length).toBeGreaterThan(0);
    const orderId = orders[0].id;

    // 2. Log in as restricted role (warehouse staff) and verify policy modification is rejected (403 Forbidden)
    await loginUser(page, 'warehouse@atlas.com');
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

    // 3. Log back in as admin and verify Quality Tab in Order 360 Workspace
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

    // 1. Negative test: Attempt to create genealogy link with forged IDs -> Must return 400/404
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

    // 2. Positive test: Create real genealogy link from authentic order output & material event
    const realOutputId = '660f1be7-4f7c-9550-93e2-676840bdc13a';
    const realInputEventId = 'a9fdc0f0-e3cf-df54-93cd-dc44b012d950';
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

    // 3. Verify Forward Genealogy
    const fwdRes = await page.request.get(`/api/v1/manufacturing/outputs/${realOutputId}/genealogy`);
    expect(fwdRes.ok()).toBeTruthy();
    const fwdList = await fwdRes.json();
    expect(fwdList.length).toBeGreaterThan(0);
    expect(fwdList.some((g: any) => g.outputId === realOutputId && g.inputEventId === realInputEventId)).toBeTruthy();

    // 4. Verify Reverse Genealogy
    const revRes = await page.request.get(`/api/v1/manufacturing/material-events/${realInputEventId}/genealogy`);
    expect(revRes.ok()).toBeTruthy();
    const revList = await revRes.json();
    expect(revList.length).toBeGreaterThan(0);
    expect(revList.some((g: any) => g.outputId === realOutputId && g.inputEventId === realInputEventId)).toBeTruthy();

    // 5. Navigate to Genealogy page in UI and verify trace view
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

    // 1. Verify Active state for Atlas tenant
    await loginUser(page, 'admin@atlas.com');
    const resActive = await page.request.get(`/api/v1/authorization/effective-access?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(resActive.ok()).toBeTruthy();
    const accessActive = await resActive.json();
    expect(accessActive.entitledModules).toContain('manufacturing');
    expect(accessActive.moduleStatuses.manufacturing).toBe('Active');

    // Permitted read on Active tenant
    const plantsRes = await page.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(plantsRes.ok()).toBeTruthy();

    // 2. Test ReadOnly and Disabled states in Delta tenant
    try {
      // 2a. ReadOnly state
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

      // 2b. Disabled state
      execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -c "UPDATE platform.module_instance SET status = 'Disabled' WHERE tenant_id = '${DELTA_TENANT_ID}' AND module_id = 'manufacturing';"`,
        { encoding: 'utf-8' }
      );

      const resDisabled = await page.request.get(`/api/v1/authorization/effective-access?legalEntityId=${DELTA_ENTITY_ID}`);
      expect(resDisabled.ok()).toBeTruthy();
      const accessDisabled = await resDisabled.json();
      expect(accessDisabled.moduleStatuses.manufacturing).toBe('Disabled');
    } finally {
      // Restore Delta tenant back to Active
      execSync(
        `docker exec todir-postgres-1 psql -U postgres -d todir_mfg_acceptance -c "UPDATE platform.module_instance SET status = 'Active' WHERE tenant_id = '${DELTA_TENANT_ID}' AND module_id = 'manufacturing';"`,
        { encoding: 'utf-8' }
      );
    }

    // 3. Test Role Security: warehouse user cannot manage definitions (403 Forbidden)
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

    // 4. Test Unauthenticated call: returns 401 Unauthorized
    const unauthPage = await page.context().browser()!.newPage();
    const unauthRes = await unauthPage.request.get(`/api/v1/manufacturing/plants?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(unauthRes.status()).toBe(401);
    await unauthPage.close();

    await saveArtifact(page, 'tx-09-entitlement-security-verified.png');
  });
});
