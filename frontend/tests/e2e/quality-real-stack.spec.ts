import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import { assertDisposableDatabase, assertApiDatabaseContext } from './quality-acceptance-guard.ts';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const LOCAL_ARTIFACTS_DIR = path.resolve(__dirname, '../../../artifacts/quality-acceptance/real-stack');
const AGENT_BRAIN_DIR = 'C:/Users/IT/.gemini/antigravity/brain/11ce24c9-e66d-46b8-95e8-fa43b74dced8/quality-acceptance/real-stack';

const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const DEV_PASSWORD =
  process.env['TODIR_ACCEPTANCE_PASSWORD'] ||
  process.env['SEED_DEFAULT_PASSWORD'] ||
  'TodirDev2026!';

test.describe('TODIR Module 27: Quality Management — Real-Stack Transactional Acceptance Suite', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(180_000);

  test.beforeAll(async () => {
    // Invariant: Genuinely fail-closed disposable database safety guard
    assertDisposableDatabase();

    [LOCAL_ARTIFACTS_DIR, AGENT_BRAIN_DIR].forEach((dir) => {
      try {
        if (!fs.existsSync(dir)) {
          fs.mkdirSync(dir, { recursive: true });
        }
      } catch {
        // ignore directory creation error
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
      // brain copy optional
    }
  };

  async function getCsrfToken(page: Page): Promise<string> {
    const res = await page.request.get('/api/v1/auth/csrf');
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    return data.token;
  }

  async function loginUser(page: Page, email = 'admin@atlas.com', entityId = ATLAS_ENTITY_ID) {
    // Assert API database context is disposable
    await assertApiDatabaseContext('http://127.0.0.1:5088');

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

    // Ensure client storage reflects legal entity & Arabic language
    await page.evaluate((id) => {
      try {
        localStorage.setItem('i18nextLng', 'ar');
        sessionStorage.setItem('erp.current.legalEntityId', id);
        const stored = localStorage.getItem('todir_current_context');
        const parsed = stored ? JSON.parse(stored) : {};
        parsed.legalEntityId = id;
        localStorage.setItem('todir_current_context', JSON.stringify(parsed));
      } catch {}
    }, entityId);
  }

  // Shared state across tests
  let createdPlanId: string = '';
  let createdInspectionId: string = '';
  let createdInspectionNumber: string = '';
  let createdHoldId: string = '';

  test('01 — Quality Overview & KPI Priority Ribbon', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-overview-page"]')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="quality-kpi-ribbon"]')).toBeVisible();

    // Verify truthful queue scope label in KPI ribbon
    await expect(page.locator('[data-testid="kpi-pending-inspections"]')).toContainText('في طابور العمل المباشر');

    await saveArtifact(page, '01-quality-overview.png');
  });

  test('02 — Quality Plans Register & Version Activation', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/plans');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-plans-page"]')).toBeVisible({ timeout: 15000 });
    const createBtn = page.getByRole('button', { name: /إنشاء خطة جودة جديدة/i });
    await expect(createBtn).toBeVisible();

    const planCode = `QP-REAL-${Date.now()}-${Math.floor(Math.random() * 10000)}`;
    await createBtn.click();
    await page.waitForTimeout(300);

    const planCodeInput = page.getByLabel(/الكود/i).or(page.locator('input[type="text"]').first());
    await planCodeInput.fill(planCode);
    const planNameInput = page.getByLabel(/الاسم/i).or(page.locator('input[type="text"]').nth(1));
    await planNameInput.fill('خطة فحص الجودة الآلية المعتمدة للتشغيل');

    const submitPlanBtn = page.getByRole('button', { name: /حفظ|إنشاء/i }).last();
    await submitPlanBtn.click();

    // Assert dialog closes
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend persistence via API
    const plansRes = await page.request.get(`/api/v1/quality/plans?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(plansRes.ok()).toBeTruthy();
    const plansData = await plansRes.json();
    const persistedPlan = plansData.items.find((p: { code: string }) => p.code === planCode);
    expect(persistedPlan).toBeDefined();
    expect(persistedPlan.name).toBe('خطة فحص الجودة الآلية المعتمدة للتشغيل');
    createdPlanId = persistedPlan.id;

    await saveArtifact(page, '02-quality-plans-register.png');
  });

  test('03 — Characteristics Catalog & Test Limits', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/plans');
    await page.waitForLoadState('networkidle');

    // Switch to Characteristics tab
    await page.getByRole('button', { name: /خصائص الفحص/i }).click();
    await page.waitForTimeout(400);

    const createCharBtn = page.getByRole('button', { name: /إضافة خاصية فحص/i });
    await expect(createCharBtn).toBeVisible();
    await createCharBtn.click();
    await page.waitForTimeout(300);

    const charCode = `QC-DIM-${Date.now()}-${Math.floor(Math.random() * 10000)}`;
    const codeInput = page.locator('input[type="text"]').first();
    await codeInput.fill(charCode);
    const nameInput = page.locator('input[type="text"]').nth(1);
    await nameInput.fill('فحص دقة الأبعاد والمقاومة الميكانيكية');

    const saveCharBtn = page.getByRole('button', { name: /حفظ|إضافة/i }).last();
    await saveCharBtn.click();

    // Assert dialog closes
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend persistence
    const charsRes = await page.request.get(`/api/v1/quality/characteristics?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(charsRes.ok()).toBeTruthy();
    const charsData = await charsRes.json();
    const persistedChar = charsData.items.find((c: { code: string }) => c.code === charCode);
    expect(persistedChar).toBeDefined();

    await saveArtifact(page, '03-quality-characteristics-catalog.png');
  });

  test('04 — Sampling Plans & AQL Methods', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/plans');
    await page.waitForLoadState('networkidle');

    // Switch to Sampling Plans tab
    await page.getByRole('button', { name: /خطط.*العينات|سحب العينات/i }).click();
    await page.waitForTimeout(400);

    const createSamplingBtn = page.getByRole('button', { name: /إنشاء خطة عينات/i });
    await expect(createSamplingBtn).toBeVisible();

    await createSamplingBtn.click();
    await page.waitForTimeout(300);

    const samplingCode = `SP-AQL-${Date.now()}-${Math.floor(Math.random() * 10000)}`;
    const codeInput = page.locator('input[type="text"]').first();
    await codeInput.fill(samplingCode);
    const nameInput = page.locator('input[type="text"]').nth(1);
    await nameInput.fill('خطة سحب عينات الفحص الشامل المعتمدة');

    const saveSamplingBtn = page.getByRole('button', { name: /حفظ|إنشاء/i }).last();
    await saveSamplingBtn.click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend persistence
    const samplingRes = await page.request.get(`/api/v1/quality/sampling-plans?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(samplingRes.ok()).toBeTruthy();
    const samplingData = await samplingRes.json();
    const persistedSampling = samplingData.items.find((s: { code: string }) => s.code === samplingCode);
    expect(persistedSampling).toBeDefined();

    await saveArtifact(page, '04-quality-sampling-plans.png');
  });

  test('05 — Inspections Queue with Status & Event Filtering', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/inspections');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-inspections-page"]')).toBeVisible({ timeout: 15000 });
    const createInspBtn = page.getByRole('button', { name: /إنشاء أمر فحص جديد/i });
    await expect(createInspBtn).toBeVisible();

    await createInspBtn.click();
    await page.waitForTimeout(400);

    const lotRef = `LOT-TX-${Date.now()}-${Math.floor(Math.random() * 10000)}`;
    const lotQtyInput = page.getByLabel(/حجم التشغيلة|كمية الدفعة/i).or(page.locator('input[type="number"]')).first();
    await lotQtyInput.fill('150');
    const lotRefInput = page.getByLabel(/رقم التشغيلة|اللوت/i).or(page.locator('input[type="text"]').last());
    await lotRefInput.fill(lotRef);

    const saveInspBtn = page.getByRole('button', { name: /إنشاء أمر الفحص|حفظ/i }).last();
    await saveInspBtn.click();

    // Assert dialog closes
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend persistence
    const inspRes = await page.request.get(`/api/v1/quality/inspections?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(inspRes.ok()).toBeTruthy();
    const inspData = await inspRes.json();
    const persisted = inspData.items.find((i: { lotQuantity: number }) => i.lotQuantity === 150) ?? inspData.items[0];
    expect(persisted).toBeDefined();
    expect(persisted.state).toBe('InProgress');
    expect(persisted.lotQuantity).toBe(150);
    createdInspectionId = persisted.id;
    createdInspectionNumber = persisted.number;

    // Verify exact lotReference persistence via authoritative detail endpoint
    const detailRes = await page.request.get(`/api/v1/quality/inspections/${createdInspectionId}`);
    expect(detailRes.ok()).toBeTruthy();
    const detailData = await detailRes.json();
    expect(detailData.lotReference).toBe(lotRef);

    await saveArtifact(page, '05-quality-inspections-queue.png');
  });

  test('06 — Inspection Deep Workbench & Characteristic Result Entry', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    expect(createdInspectionId).toBeTruthy();
    await page.goto(`/quality/inspections/${createdInspectionId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="inspection-workspace-page"]')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="inspection-domain-invariant-notice"]')).toBeVisible();

    const charsTable = page.locator('[data-testid="inspection-characteristics-table"]');
    await expect(charsTable).toBeVisible({ timeout: 8000 });

    // Record measurement on characteristic
    const recordBtn = page.getByRole('button', { name: /تسجيل نتائج القياس|تسجيل القياس/i }).first();
    await expect(recordBtn).toBeVisible();
    await recordBtn.click();
    await page.waitForTimeout(300);

    const valInput = page.locator('input[type="number"]').first();
    await expect(valInput).toBeVisible();
    await valInput.fill('24.5');

    const evidenceInput = page.locator('input[type="text"]').first();
    if (await evidenceInput.isVisible()) {
      await evidenceInput.fill('DOC-LAB-CERT-2026');
    }

    const instrumentInput = page.locator('input[type="text"]').last();
    if (await instrumentInput.isVisible()) {
      await instrumentInput.fill('CAL-GAUGE-01');
    }

    const saveResBtn = page.getByRole('button', { name: /تسجيل النتيجة|حفظ/i }).last();
    await saveResBtn.click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend persistence of inspection measurement
    const detailRes = await page.request.get(`/api/v1/quality/inspections/${createdInspectionId}`);
    expect(detailRes.ok()).toBeTruthy();
    const detailData = await detailRes.json();
    expect(detailData.results.length).toBeGreaterThan(0);
    expect(detailData.results[0].numericValue).toBe(24.5);

    await saveArtifact(page, '06-inspection-workbench-active.png');
  });

  test('07 — Inspection Conformance Evaluation & Completion State', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto(`/quality/inspections/${createdInspectionId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="inspection-workspace-page"]')).toBeVisible({ timeout: 15000 });

    const completeBtn = page.getByRole('button', { name: /إتمام واعتماد الفحص/i });
    await expect(completeBtn).toBeVisible();
    await completeBtn.click();
    await page.waitForTimeout(300);

    const notesInput = page.locator('input[type="text"]').last();
    if (await notesInput.isVisible()) {
      await notesInput.fill('اعتماد الفحص المخبري والمطابقة القياسية');
    }

    const confirmBtn = page.getByRole('button', { name: /تأكيد واعتماد الإتمام|تأكيد/i }).last();
    await confirmBtn.click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend completion
    const detailRes = await page.request.get(`/api/v1/quality/inspections/${createdInspectionId}`);
    expect(detailRes.ok()).toBeTruthy();
    const detailData = await detailRes.json();
    expect(detailData.state).toBe('Completed');

    await saveArtifact(page, '07-inspection-evaluation-completed.png');
  });

  test('08 — Strict Domain Invariant: Passed != Released Notice', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/restrictions');
    await page.waitForLoadState('networkidle');

    // Banner is visible on QualityRestrictionsPage
    await expect(page.locator('[data-testid="quality-domain-invariant-banner"]')).toBeVisible();

    await saveArtifact(page, '08-inspection-domain-invariant-warning.png');
  });

  test('09 — Reinspection Workflow & Cycles', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto(`/quality/inspections/${createdInspectionId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="inspection-workspace-page"]')).toBeVisible({ timeout: 15000 });

    const reinspectBtn = page.getByRole('button', { name: /إعادة فحص/i });
    await expect(reinspectBtn).toBeVisible();
    await reinspectBtn.click();
    await page.waitForTimeout(300);

    const reasonInput = page.locator('input[type="text"]').last();
    await expect(reasonInput).toBeVisible();
    await reasonInput.fill('إعادة فحص دورة التحقق الفني بعد المعايرة');

    const submitBtn = page.getByRole('button', { name: /تأكيد إعادة الفحص|تأكيد|حفظ/i }).last();
    await submitBtn.click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend state: original inspection superseded
    const detailRes = await page.request.get(`/api/v1/quality/inspections/${createdInspectionId}`);
    expect(detailRes.ok()).toBeTruthy();
    const detailData = await detailRes.json();
    expect(detailData.state).toBe('Superseded');

    await saveArtifact(page, '09-reinspection-workflow.png');
  });

  test('10 — Material Holds & Restrictions Register', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/restrictions');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-restrictions-page"]')).toBeVisible({ timeout: 15000 });
    const createHoldBtn = page.getByRole('button', { name: /حظر واحتجاز الدفعة/i });
    await expect(createHoldBtn).toBeVisible();

    await createHoldBtn.click();
    await page.waitForTimeout(300);

    // Verify server-side inspection search and live available stock indicator
    const searchInput = page.locator('[data-testid="inspection-search-input"] input').or(page.getByLabel(/بحث في أوامر الفحص/i));
    if (await searchInput.isVisible()) {
      await searchInput.fill(createdInspectionNumber || 'QI-SEED-0001');
      await page.waitForTimeout(400);
    }
    await expect(page.locator('[data-testid="available-stock-indicator"]')).toBeVisible({ timeout: 10000 });

    // Verify insufficient stock validation and fail-closed button protection
    const qtyInput = page.getByLabel(/الكمية المحتجزة/i).or(page.locator('input[type="number"]')).first();
    await qtyInput.fill('999999');
    await expect(page.locator('[data-testid="insufficient-stock-alert"]')).toBeVisible();
    const submitHoldBtn = page.getByRole('button', { name: /تطبيق الحظر|حفظ/i }).last();
    await expect(submitHoldBtn).toBeDisabled();

    // Restore valid quantity within available stock (100)
    await qtyInput.fill('100');
    await expect(page.locator('[data-testid="insufficient-stock-alert"]')).not.toBeVisible();

    const reasonCode = `HOLD-QA-${Date.now()}-${Math.floor(Math.random() * 10000)}`;
    const reasonCodeInput = page.getByLabel(/كود وسبب الحظر/i).first();
    await reasonCodeInput.fill(reasonCode);

    await expect(submitHoldBtn).toBeEnabled();
    await submitHoldBtn.click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend persistence with confirmed owner inventory hold
    const holdsRes = await page.request.get(`/api/v1/quality/restrictions?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(holdsRes.ok()).toBeTruthy();
    const holdsData = await holdsRes.json();
    const persistedHold = holdsData.items.find((h: { reasonCode: string }) => h.reasonCode === reasonCode);
    expect(persistedHold).toBeDefined();
    expect(persistedHold.quantity).toBe(100);
    expect(persistedHold.status).toBe('Active');
    expect(persistedHold.ownerExecutionStatus).toBe('Confirmed');
    createdHoldId = persistedHold.id;

    // Exercise the documentary administrative path through the authenticated browser
    // context. It must preserve the inspection link while leaving Inventory untouched.
    const balancesBeforeAdminRes = await page.request.get(`/api/v1/inventory/legal-entities/${ATLAS_ENTITY_ID}/balances`);
    expect(balancesBeforeAdminRes.ok()).toBeTruthy();
    const balancesBeforeAdmin = await balancesBeforeAdminRes.json();

    await createHoldBtn.click();
    await page.waitForTimeout(300);
    const holdScopeSelect = page.getByRole('button', { name: /حظر مخزني فعلي/i });
    await expect(holdScopeSelect).toBeVisible();
    await holdScopeSelect.click();
    await page.getByRole('option', { name: /حظر إداري ومستندي/i }).click();
    const administrativeReason = `ADMIN-HOLD-QA-${Date.now()}-${Math.floor(Math.random() * 10000)}`;
    await page.getByLabel(/كود وسبب الحظر/i).first().fill(administrativeReason);
    await page.getByRole('button', { name: /تطبيق الحظر|حفظ/i }).last().click();
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    const adminHoldsRes = await page.request.get(`/api/v1/quality/restrictions?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(adminHoldsRes.ok()).toBeTruthy();
    const adminHolds = await adminHoldsRes.json();
    const administrativeHold = adminHolds.items.find((h: { reasonCode: string }) => h.reasonCode === administrativeReason);
    expect(administrativeHold).toBeDefined();
    expect(administrativeHold.ownerExecutionStatus).toBe('NotRequired');
    const administrativeDetailRes = await page.request.get(`/api/v1/quality/restrictions/${administrativeHold.id}`);
    expect(administrativeDetailRes.ok()).toBeTruthy();
    const administrativeDetail = await administrativeDetailRes.json();
    expect(administrativeDetail.inspectionId).toBeTruthy();
    expect(administrativeDetail.productVariantId).toBeNull();
    expect(administrativeDetail.quantity).toBeNull();

    const balancesAfterAdminRes = await page.request.get(`/api/v1/inventory/legal-entities/${ATLAS_ENTITY_ID}/balances`);
    expect(balancesAfterAdminRes.ok()).toBeTruthy();
    expect(await balancesAfterAdminRes.json()).toEqual(balancesBeforeAdmin);

    await saveArtifact(page, '10-material-holds-restrictions.png');
  });

  test('11 — Partial Quantity Release Dialog & Balance Calculation', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/restrictions');
    await page.waitForLoadState('networkidle');

    expect(createdHoldId).toBeTruthy();

    // Query restriction details to identify the product variant, warehouse, and location
    const holdDetailRes = await page.request.get(`/api/v1/quality/restrictions/${createdHoldId}`);
    expect(holdDetailRes.ok()).toBeTruthy();
    const holdDetail = await holdDetailRes.json();
    const variantId = holdDetail.productVariantId;
    const warehouseId = holdDetail.warehouseId;
    const locationId = holdDetail.locationId;
    const lotId = holdDetail.lotId;
    const serialId = holdDetail.serialId;

    const matchStock = (b: { stockStatus: string; warehouseId: string; locationId?: string; lotId?: string; serialId?: string; onHandBaseQuantity: number }, status: string) =>
      b.stockStatus === status &&
      b.warehouseId === warehouseId &&
      (!locationId || b.locationId === locationId) &&
      (!lotId || b.lotId === lotId) &&
      (!serialId || b.serialId === serialId);

    // Get baseline inventory balances before release
    const balBeforeRes = await page.request.get(`/api/v1/inventory/legal-entities/${ATLAS_ENTITY_ID}/balances?productVariantId=${variantId}`);
    expect(balBeforeRes.ok()).toBeTruthy();
    const balancesBefore = await balBeforeRes.json();
    const availBefore = Number(balancesBefore.find((b: any) => matchStock(b, 'Available'))?.onHandBaseQuantity ?? 0);
    const holdBefore = Number(balancesBefore.find((b: any) => matchStock(b, 'QualityHold'))?.onHandBaseQuantity ?? 0);

    // Target the hold created by Scenario 10; the acceptance database may retain
    // earlier fixture rows and list order is not an identity guarantee.
    const holdRow = page.getByRole('row').filter({ hasText: holdDetail.reasonCode });
    const releaseBtn = holdRow.getByRole('button', { name: /إفراج مخزني/i });
    await expect(releaseBtn).toBeVisible();
    await releaseBtn.click();
    await page.waitForTimeout(300);

    const qtyInput = page.getByLabel(/كمية الإفراج/i).or(page.locator('input[type="number"]')).first();
    await expect(qtyInput).toBeVisible();
    await qtyInput.fill('25');

    const reasonInput = page.getByLabel(/مبرر وأسباب الإفراج/i).or(page.locator('input[type="text"]').first());
    await reasonInput.fill('إفراج جزئي بعد استكمال الفحص المرحلي');

    const confirmReleaseBtn = page.getByRole('button', { name: /تأكيد الإفراج|تأكيد/i }).last();
    await confirmReleaseBtn.click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend release persistence and confirmed owner execution status
    const holdsRes = await page.request.get(`/api/v1/quality/restrictions?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(holdsRes.ok()).toBeTruthy();
    const holdsData = await holdsRes.json();
    const releasedHold = holdsData.items.find((h: { id: string }) => h.id === createdHoldId);
    expect(releasedHold).toBeDefined();
    expect(releasedHold.releasedQuantity).toBe(25);
    expect(releasedHold.ownerExecutionStatus).toBe('Confirmed');

    // Strict Inventory Balance Reconciliation: exact delta verification matching exact stock position
    const balAfterRes = await page.request.get(`/api/v1/inventory/legal-entities/${ATLAS_ENTITY_ID}/balances?productVariantId=${variantId}`);
    expect(balAfterRes.ok()).toBeTruthy();
    const balancesAfter = await balAfterRes.json();
    const availAfter = Number(balancesAfter.find((b: any) => matchStock(b, 'Available'))?.onHandBaseQuantity ?? 0);
    const holdAfter = Number(balancesAfter.find((b: any) => matchStock(b, 'QualityHold'))?.onHandBaseQuantity ?? 0);

    expect(holdAfter).toBe(holdBefore - 25);
    expect(availAfter).toBe(availBefore + 25);

    await saveArtifact(page, '11-partial-quantity-release-dialog.png');
  });

  test('12 — Dispositions Queue & MRB Technical Decisions (Hold Source Preserved)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/restrictions');
    await page.waitForLoadState('networkidle');

    expect(createdHoldId).toBeTruthy();
    const targetHoldDetailRes = await page.request.get(`/api/v1/quality/restrictions/${createdHoldId}`);
    expect(targetHoldDetailRes.ok()).toBeTruthy();
    const targetHoldDetail = await targetHoldDetailRes.json();
    const targetHoldRow = page.getByRole('row').filter({ hasText: targetHoldDetail.reasonCode });

    // Launch disposition directly from hold row
    const dispRowBtn = targetHoldRow.getByRole('button', { name: /قرار فني/i });
    await expect(dispRowBtn).toBeVisible();
    await dispRowBtn.click();
    await page.waitForTimeout(300);

    // Verify source hold banner is rendered in dialog
    await expect(page.locator('[data-testid="disposition-hold-source-banner"]')).toBeVisible();

    const reasonInput = page.getByLabel(/المبرر والأسباب الفنية/i).or(page.locator('input[type="text"]').last());
    await reasonInput.fill('قرار فني معتمد للاستخدام بالحالة الراهنة');

    const saveDispBtn = page.getByRole('button', { name: /^حفظ$|حفظ القرار كمسودة/i });
    await saveDispBtn.click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend disposition creation
    const dispRes = await page.request.get(`/api/v1/quality/dispositions?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(dispRes.ok()).toBeTruthy();
    const dispData = await dispRes.json();
    const persistedDisp = dispData.items.find((d: { reason: string }) => d.reason.includes('قرار فني معتمد'));
    expect(persistedDisp).toBeDefined();
    expect(persistedDisp.state).toBe('Draft');

    // Go to dispositions page to capture full queue and execute lifecycle
    await page.goto('/quality/dispositions');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="quality-dispositions-page"]')).toBeVisible({ timeout: 15000 });

    const targetDispositionRow = page.locator(`[data-testid="quality-disposition-row-${persistedDisp.id}"]`);

    // Approve the disposition
    const approveBtn = targetDispositionRow.getByRole('button', { name: /اعتماد/i });
    await expect(approveBtn).toBeVisible();
    await approveBtn.click();
    await page.waitForTimeout(300);

    const approveDialog = page.getByRole('dialog');
    await expect(approveDialog).toBeVisible();

    const approveNotes = approveDialog.getByLabel(/ملاحظات الاعتماد/i).or(approveDialog.locator('input[type="text"]').first());
    if (await approveNotes.isVisible()) {
      await approveNotes.fill('معتمد من مدير الجودة');
    }
    const confirmApproveBtn = approveDialog.getByRole('button', { name: /تأكيد|اعتماد/i });
    await confirmApproveBtn.click();
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Read balances immediately before execution
    const holdDetailRes = await page.request.get(`/api/v1/quality/restrictions/${createdHoldId}`);
    const holdDetail = await holdDetailRes.json();
    const variantId = holdDetail.productVariantId;
    const warehouseId = holdDetail.warehouseId;
    const locationId = holdDetail.locationId;
    const lotId = holdDetail.lotId;
    const serialId = holdDetail.serialId;

    const matchStock = (b: { stockStatus: string; warehouseId: string; locationId?: string; lotId?: string; serialId?: string; onHandBaseQuantity: number }, status: string) =>
      b.stockStatus === status &&
      b.warehouseId === warehouseId &&
      (!locationId || b.locationId === locationId) &&
      (!lotId || b.lotId === lotId) &&
      (!serialId || b.serialId === serialId);

    const balBeforeExecRes = await page.request.get(`/api/v1/inventory/legal-entities/${ATLAS_ENTITY_ID}/balances?productVariantId=${variantId}`);
    const balBeforeExec = await balBeforeExecRes.json();
    const holdBeforeExec = Number(balBeforeExec.find((b: any) => matchStock(b, 'QualityHold'))?.onHandBaseQuantity ?? 0);
    const availBeforeExec = Number(balBeforeExec.find((b: any) => matchStock(b, 'Available'))?.onHandBaseQuantity ?? 0);

    // Execute the disposition to trigger owner integration
    const executeBtn = targetDispositionRow.getByRole('button', { name: /تنفيذ.*أثر/i });
    await expect(executeBtn).toBeVisible();
    await executeBtn.click();
    await page.waitForTimeout(600);

    // Verify disposition is executed and confirmed
    const dispExecRes = await page.request.get(`/api/v1/quality/dispositions?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(dispExecRes.ok()).toBeTruthy();
    const dispExecData = await dispExecRes.json();
    const executedDisp = dispExecData.items.find((d: { id: string }) => d.id === persistedDisp.id);
    expect(executedDisp).toBeDefined();
    expect(executedDisp.state).toBe('Executed');
    expect(executedDisp.ownerExecutionStatus).toBe('Confirmed');

    // Verify underlying hold was fully released
    const finalHoldRes = await page.request.get(`/api/v1/quality/restrictions?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(finalHoldRes.ok()).toBeTruthy();
    const finalHoldData = await finalHoldRes.json();
    const finalHold = finalHoldData.items.find((h: { id: string }) => h.id === createdHoldId);
    expect(finalHold).toBeDefined();
    expect(finalHold.releasedQuantity).toBe(100);
    expect(finalHold.status).toBe('Released');

    // Assert real inventory balances reflect full disposition release (remaining 75 released)
    const balAfterExecRes = await page.request.get(`/api/v1/inventory/legal-entities/${ATLAS_ENTITY_ID}/balances?productVariantId=${variantId}`);
    expect(balAfterExecRes.ok()).toBeTruthy();
    const balAfterExec = await balAfterExecRes.json();
    const holdAfterExec = Number(balAfterExec.find((b: any) => matchStock(b, 'QualityHold'))?.onHandBaseQuantity ?? 0);
    const availAfterExec = Number(balAfterExec.find((b: any) => matchStock(b, 'Available'))?.onHandBaseQuantity ?? 0);

    expect(holdAfterExec).toBe(holdBeforeExec - 75);
    expect(availAfterExec).toBe(availBeforeExec + 75);

    await saveArtifact(page, '12-quality-dispositions-mrb.png');
  });

  test('13 — Quality Issues Log & Defect Classification', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/ncrs');
    await page.waitForLoadState('networkidle');

    // Switch to Issues tab
    await page.getByRole('button', { name: /مشاكل الجودة|سجل المشاكل/i }).click();
    await page.waitForTimeout(400);

    const createIssueBtn = page.getByRole('button', { name: /تسجيل مشكلة جودة/i });
    await expect(createIssueBtn).toBeVisible();
    await createIssueBtn.click();
    await page.waitForTimeout(300);

    const issueTitle = `رصد تشققات مجهرية في دفعات السحب ${Date.now()}-${Math.floor(Math.random() * 10000)}`;
    const titleInput = page.getByLabel(/عنوان المشكلة/i).or(page.locator('input[type="text"]').first());
    await titleInput.fill(issueTitle);

    const descInput = page.locator('textarea').first();
    await descInput.fill('تجاوز نسبة التشقق السطحي الحد المسموح هندسياً');

    const saveIssueBtn = page.getByRole('button', { name: /حفظ|تسجيل/i }).last();
    await saveIssueBtn.click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend persistence
    const issueRes = await page.request.get(`/api/v1/quality/issues?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(issueRes.ok()).toBeTruthy();
    const issueData = await issueRes.json();
    const persisted = issueData.items.find((i: { title: string }) => i.title === issueTitle);
    expect(persisted).toBeDefined();

    await saveArtifact(page, '13-quality-issues-log.png');
  });

  test('14 — Non-Conformance Reports (NCR) & Immediate Containment', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/ncrs');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-ncrs-page"]')).toBeVisible({ timeout: 15000 });
    const createNcrBtn = page.getByRole('button', { name: /فتح تقرير عدم مطابقة/i });
    await expect(createNcrBtn).toBeVisible();

    await createNcrBtn.click();
    await page.waitForTimeout(300);

    const ncrTitle = `تقرير عدم مطابقة لشحنة الألومنيوم ${Date.now()}-${Math.floor(Math.random() * 10000)}`;
    const titleInput = page.getByLabel(/عنوان تقرير عدم المطابقة|عنوان/i).or(page.locator('input[type="text"]').first());
    await titleInput.fill(ncrTitle);

    const descInput = page.locator('textarea').first();
    await descInput.fill('انحراف الأبعاد الهندسية عن المخطط القياسي');

    const saveNcrBtn = page.getByRole('button', { name: /حفظ|فتح/i }).last();
    await saveNcrBtn.click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend persistence
    const ncrRes = await page.request.get(`/api/v1/quality/ncrs?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(ncrRes.ok()).toBeTruthy();
    const ncrData = await ncrRes.json();
    const persistedNcr = ncrData.items.find((n: { title: string }) => n.title === ncrTitle);
    expect(persistedNcr).toBeDefined();

    await saveArtifact(page, '14-ncrs-containment-actions.png');
  });

  test('15 — Corrective & Preventive Action (CAPA) with 6M Root Cause', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/capas');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-capas-page"]')).toBeVisible({ timeout: 15000 });
    const createCapaBtn = page.getByRole('button', { name: /فتح طلب CAPA جديد/i });
    await expect(createCapaBtn).toBeVisible();

    await createCapaBtn.click();
    await page.waitForTimeout(300);

    const capaTitle = `خطة تصحيحية لمنع انحراف المعايرة الحرارية ${Date.now()}-${Math.floor(Math.random() * 10000)}`;
    const titleInput = page.getByLabel(/عنوان طلب|عنوان/i).or(page.locator('input[type="text"]').first());
    await titleInput.fill(capaTitle);

    const problemInput = page.locator('textarea').first();
    await problemInput.fill('تذبذب درجات حرارة أفران المعالجة الحرارية خلال الوردية المسائية');

    const saveCapaBtn = page.getByRole('button', { name: /حفظ|فتح/i }).last();
    await saveCapaBtn.click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend persistence
    const capaRes = await page.request.get(`/api/v1/quality/capas?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(capaRes.ok()).toBeTruthy();
    const capaData = await capaRes.json();
    const persistedCapa = capaData.items.find((c: { title: string }) => c.title === capaTitle);
    expect(persistedCapa).toBeDefined();

    await saveArtifact(page, '15-capa-root-cause-investigation.png');
  });

  test('16 — Supplier Quality, SCAR Queue, and COA/COC Certificates', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/supplier-quality');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="supplier-quality-page"]')).toBeVisible({ timeout: 15000 });

    // Open certificates page
    await page.goto('/quality/certificates');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-certificates-page"]')).toBeVisible({ timeout: 15000 });

    const inspectionsRes = await page.request.get(`/api/v1/quality/inspections?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(inspectionsRes.ok()).toBeTruthy();
    const inspectionsData = await inspectionsRes.json();
    const expectedCertificateInspection = inspectionsData.items.find((item: { decisionState?: string }) => item.decisionState === 'Passed');
    expect(expectedCertificateInspection).toBeDefined();

    const issueCertBtn = page.getByRole('button', { name: /إصدار شهادة جودة/i });
    await expect(issueCertBtn).toBeVisible();

    await issueCertBtn.click();
    await page.waitForTimeout(300);

    const certificateCustomerReference = `PO-CUST-QA16-${Date.now()}-${Math.floor(Math.random() * 100000)}`;
    const custRef = page.getByLabel(/مرجع العميل/i).or(page.locator('input[type="text"]').last());
    if (await custRef.isVisible()) {
      await custRef.fill(certificateCustomerReference);
    }

    const saveCertBtn = page.getByRole('button', { name: /إصدار شهادة جودة|إصدار الشهادة|حفظ/i }).last();
    await saveCertBtn.click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 8000 });

    // Assert backend persistence with exact certificate identity and status
    const certsRes = await page.request.get(`/api/v1/quality/certificates?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(certsRes.ok()).toBeTruthy();
    const certsData = await certsRes.json();
    expect(certsData.items.length).toBeGreaterThan(0);
    const persistedCert = certsData.items.find((c: { customerReference?: string }) => c.customerReference === certificateCustomerReference);
    expect(persistedCert).toBeDefined();
    expect(persistedCert.state).toBe('Issued');
    expect(persistedCert.certificateType).toBe('COA');
    expect(persistedCert.inspectionId).toBe(expectedCertificateInspection.id);

    const certificateDetailRes = await page.request.get(`/api/v1/quality/certificates/${persistedCert.id}`);
    expect(certificateDetailRes.ok()).toBeTruthy();
    const certificateDetail = await certificateDetailRes.json();
    expect(certificateDetail.customerReference).toBe(certificateCustomerReference);
    expect(certificateDetail.inspectionId).toBe(expectedCertificateInspection.id);
    expect(certificateDetail.state).toBe('Issued');
    expect(certificateDetail.certificateType).toBe('COA');
    expect(certificateDetail.snapshot).toBeTruthy();
    expect(certificateDetail.snapshot.InspectionId).toBe(expectedCertificateInspection.id);
    expect(certificateDetail.snapshot.inspection.Number).toBe(expectedCertificateInspection.number);
    expect(certificateDetail.snapshot.inspection.DecisionState).toBe('Passed');

    await saveArtifact(page, '16-supplier-quality-scar-certificates.png');
  });

  test('17 — Multi-Viewport & Arabic/English Responsive Verification', async ({ page }) => {
    await loginUser(page);

    // Desktop Viewport (1440px)
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/quality');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="quality-overview-page"]')).toBeVisible();
    const desktopOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(desktopOverflow).toBe(false);

    // Tablet Viewport (768px) - demonstrate genuine responsiveness
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/quality');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="quality-overview-page"]')).toBeVisible();
    const tabletOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(tabletOverflow).toBe(false);

    // Verify CAPA action buttons responsive layout on tablet
    await page.goto('/quality/capas');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="quality-capas-page"]')).toBeVisible();
    const capaOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(capaOverflow).toBe(false);

    // Capture tablet artifact to demonstrate genuine responsiveness
    await saveArtifact(page, '17-multi-viewport-rtl-ltr.png');

    // Mobile Viewport (375px)
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/quality');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="quality-overview-page"]')).toBeVisible();
    const mobileOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(mobileOverflow).toBe(false);

    // Arabic RTL Direction Verification
    const dirAr = await page.locator('html').getAttribute('dir');
    expect(dirAr).toBe('rtl');

    // English LTR Verification
    await page.setViewportSize({ width: 1440, height: 900 });
    const langBtn = page.locator('button.topbar-lang-btn').first();
    await expect(langBtn).toBeVisible({ timeout: 10000 });
    const currentDir = await page.locator('html').getAttribute('dir');
    if (currentDir === 'rtl') {
      await langBtn.click();
      await page.waitForTimeout(1000);
    }
    const dirEn = await page.locator('html').getAttribute('dir');
    expect(dirEn).toBe('ltr');

    // Restore Arabic
    await langBtn.click();
    await page.waitForTimeout(1000);
    expect(await page.locator('html').getAttribute('dir')).toBe('rtl');
  });

  test('18 — Permission & Lifecycle Guard Verification', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });

    // 1. Authorized administrator (admin@atlas.com) has full operational access
    await loginUser(page, 'admin@atlas.com');
    await page.goto('/quality/plans');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="quality-plans-page"]')).toBeVisible();

    const adminCreateBtn = page.getByRole('button', { name: /إنشاء خطة جودة جديدة/i });
    await expect(adminCreateBtn).toBeEnabled();

    // 2. Restricted user without quality permissions (sales@atlas.com)
    await loginUser(page, 'sales@atlas.com');
    await page.goto('/quality/plans');
    await page.waitForLoadState('networkidle');

    // Verify UI disables or hides mutating primary actions for unauthorized user
    const salesCreateBtn = page.getByRole('button', { name: /إنشاء خطة جودة جديدة/i });
    if (await salesCreateBtn.isVisible()) {
      await expect(salesCreateBtn).toBeDisabled();
    }

    // Direct mutation attempt via API as sales@atlas.com is strictly rejected with 403 Forbidden
    const csrfToken = await getCsrfToken(page);
    const deniedRes = await page.request.post('/api/v1/quality/plans', {
      data: { code: `DENIED-${Date.now()}-${Math.floor(Math.random() * 10000)}`, name: 'Unauthorized Quality Plan', legalEntityId: ATLAS_ENTITY_ID },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(deniedRes.status()).toBe(403);

    // Save screenshot AS restricted user (sales@atlas.com) to demonstrate genuine access control and disabled UI
    await saveArtifact(page, '18-permission-lifecycle-protection.png');

    // Restore admin session for subsequent tests
    await loginUser(page, 'admin@atlas.com');
  });

  test('19 — Keyset Pagination & Cursor Navigation', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/inspections');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="quality-inspections-page"]')).toBeVisible({ timeout: 15000 });

    // Verify Previous is disabled on first page
    const prevBtn = page.getByRole('button', { name: /السابق/i }).first();
    await expect(prevBtn).toBeDisabled();

    // Capture complete record row texts on Page 1 from real PostgreSQL records
    const page1Rows = page.locator('table tbody tr');
    const page1Count = await page1Rows.count();
    expect(page1Count).toBeGreaterThan(0);
    const page1Texts: string[] = [];
    for (let i = 0; i < page1Count; i++) {
      page1Texts.push(await page1Rows.nth(i).innerText());
    }

    // Verify Next button is enabled and navigate to Page 2
    const nextBtn = page.getByRole('button', { name: /التالي/i }).first();
    await expect(nextBtn).toBeEnabled();
    await nextBtn.click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);

    // Verify Page 2 items loaded
    const page2Rows = page.locator('table tbody tr');
    const page2Count = await page2Rows.count();
    expect(page2Count).toBeGreaterThan(0);
    const page2Texts: string[] = [];
    for (let i = 0; i < page2Count; i++) {
      page2Texts.push(await page2Rows.nth(i).innerText());
    }

    // Assert complete record identity separation: ZERO duplicates between page 1 and page 2
    const duplicates = page1Texts.filter(t => page2Texts.includes(t));
    expect(duplicates).toHaveLength(0);

    // Verify Previous is now enabled on Page 2 and navigate back to Page 1
    await expect(prevBtn).toBeEnabled();
    await prevBtn.click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);

    // Verify exactly restored to Page 1 records in exact same order
    const restoredRows = page.locator('table tbody tr');
    expect(await restoredRows.count()).toBe(page1Count);
    for (let i = 0; i < page1Count; i++) {
      expect(await restoredRows.nth(i).innerText()).toBe(page1Texts[i]);
    }
    await expect(prevBtn).toBeDisabled();
  });

  test('20 — Fail-Closed Security & Access Invariant Verification', async ({ playwright, page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    // Verify inspections page renders securely for authenticated admin
    await page.goto('/quality/inspections');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="quality-inspections-page"]')).toBeVisible();

    // 1. Anonymous request without session/cookies is strictly rejected (401 or 403)
    const anonymousContext = await playwright.request.newContext();
    const anonRes = await anonymousContext.post('http://127.0.0.1:5088/api/v1/quality/plans', {
      data: { code: 'ANON-01', name: 'Anonymous Plan', legalEntityId: ATLAS_ENTITY_ID }
    });
    expect([401, 403]).toContain(anonRes.status());

    // 2. Authenticated user lacking quality write permissions (sales@atlas.com) is denied (403)
    await loginUser(page, 'sales@atlas.com');
    const salesCsrf = await getCsrfToken(page);
    const unauthRes = await page.request.post('/api/v1/quality/plans', {
      data: { code: 'UNAUTH-01', name: 'Unauthorized Plan', legalEntityId: ATLAS_ENTITY_ID },
      headers: { 'X-CSRF-TOKEN': salesCsrf }
    });
    expect(unauthRes.status()).toBe(403);

    // 3. Same-Tenant Cross-Legal-Entity Isolation:
    // Records belonging to MAIN entity must never be visible under SECONDARY entity (d8eb39bb-b048-7c5d-bacd-e3a923eee21c)
    await loginUser(page, 'admin@atlas.com');
    const SECONDARY_ENTITY_ID = 'd8eb39bb-b048-7c5d-bacd-e3a923eee21c';

    // 3a. Direct query with mismatched legalEntityId while session is locked to MAIN entity is strictly denied (403 Forbidden)
    const crossEntityDirectRes = await page.request.get(`/api/v1/quality/inspections?legalEntityId=${SECONDARY_ENTITY_ID}`);
    expect(crossEntityDirectRes.status()).toBe(403);

    // 3b. Switch session context to SECONDARY entity; verify MAIN entity inspection is strictly isolated (zero leakage)
    const adminCsrf = await getCsrfToken(page);
    const switchRes = await page.request.post('/api/v1/context/organization', {
      data: { legalEntityId: SECONDARY_ENTITY_ID },
      headers: { 'X-CSRF-TOKEN': adminCsrf }
    });
    expect(switchRes.status()).toBe(204);

    const secondaryInspectionsRes = await page.request.get(`/api/v1/quality/inspections?legalEntityId=${SECONDARY_ENTITY_ID}`);
    expect(secondaryInspectionsRes.ok()).toBeTruthy();
    const secondaryInspections = await secondaryInspectionsRes.json();
    if (createdInspectionId) {
      const leakedInspection = secondaryInspections.items?.find((i: { id: string }) => i.id === createdInspectionId);
      expect(leakedInspection).toBeUndefined();
    }

    // Restore session context back to MAIN entity
    const restoreRes = await page.request.post('/api/v1/context/organization', {
      data: { legalEntityId: ATLAS_ENTITY_ID },
      headers: { 'X-CSRF-TOKEN': adminCsrf }
    });
    expect(restoreRes.status()).toBe(204);

    // 4. Cross-Tenant Isolation: delta admin (admin@delta.com) attempting to mutate Atlas legal entity is rejected
    await loginUser(page, 'admin@delta.com');
    const deltaCsrf = await getCsrfToken(page);
    const crossTenantRes = await page.request.post('/api/v1/quality/plans', {
      data: { code: 'CROSS-TENANT-01', name: 'Cross Tenant Plan', legalEntityId: ATLAS_ENTITY_ID },
      headers: { 'X-CSRF-TOKEN': deltaCsrf }
    });
    expect([400, 401, 403, 404]).toContain(crossTenantRes.status());

    // 5. Restore admin@atlas.com and verify none of the unauthorized attempts created database records
    await loginUser(page, 'admin@atlas.com');
    const checkRes = await page.request.get(`/api/v1/quality/plans?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(checkRes.ok()).toBeTruthy();
    const checkData = await checkRes.json();
    const unauthorizedCodes = ['ANON-01', 'UNAUTH-01', 'CROSS-TENANT-01'];
    const leaked = checkData.items.filter((p: { code: string }) => unauthorizedCodes.includes(p.code));
    expect(leaked).toHaveLength(0);
  });
});
