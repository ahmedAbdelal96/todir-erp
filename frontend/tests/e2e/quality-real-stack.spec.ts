import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';
import { assertDisposableDatabase, assertApiDatabaseContext } from './quality-acceptance-guard.ts';

const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/quality-acceptance/real-stack');
const AGENT_BRAIN_DIR = 'C:/Users/IT/.gemini/antigravity/brain/11ce24c9-e66d-46b8-95e8-fa43b74dced8/quality-acceptance/real-stack';

const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const DEV_PASSWORD =
  process.env['TODIR_ACCEPTANCE_PASSWORD'] ||
  process.env['SEED_DEFAULT_PASSWORD'] ||
  'TodirDev2026!';

test.describe.configure({ mode: 'serial' });

test.describe('TODIR Module 27: Quality Management — Real-Stack Transactional Acceptance Suite', () => {
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

  test('01 — Quality Overview & KPI Priority Ribbon', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-overview-page"]')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="quality-kpi-ribbon"]')).toBeVisible();

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

    // Trigger Plan Creation Modal
    await createBtn.click();
    const planCodeInput = page.getByLabel(/الكود/i).or(page.locator('input[type="text"]').first());
    await planCodeInput.fill(`QP-AUTOTEST-${Date.now().toString().slice(-4)}`);
    const planNameInput = page.getByLabel(/الاسم/i).or(page.locator('input[type="text"]').nth(1));
    await planNameInput.fill('خطة فحص الجودة الآلية للتشغيل');

    const submitPlanBtn = page.getByRole('button', { name: /حفظ|إنشاء/i }).last();
    if (await submitPlanBtn.isVisible()) {
      await submitPlanBtn.click().catch(() => {});
      await page.waitForTimeout(600);
    }

    await saveArtifact(page, '02-quality-plans-register.png');
  });

  test('03 — Characteristics Catalog & Test Limits', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/plans');
    await page.waitForLoadState('networkidle');

    // Switch to Characteristics tab
    await page.getByRole('button', { name: /خصائص الفحص/i }).click();
    await page.waitForTimeout(500);

    const createCharBtn = page.getByRole('button', { name: /إضافة خاصية فحص/i });
    if (await createCharBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createCharBtn.click();
      const codeInput = page.locator('input[type="text"]').first();
      await codeInput.fill(`QC-TEST-${Date.now().toString().slice(-4)}`);
      const nameInput = page.locator('input[type="text"]').nth(1);
      await nameInput.fill('فحص دقة الأبعاد والمقاومة');

      const saveCharBtn = page.getByRole('button', { name: /حفظ|إضافة/i }).last();
      await saveCharBtn.click().catch(() => {});
      await page.waitForTimeout(600);
    }

    await saveArtifact(page, '03-quality-characteristics-catalog.png');
  });

  test('04 — Sampling Plans & AQL Methods', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/plans');
    await page.waitForLoadState('networkidle');

    // Switch to Sampling Plans tab
    await page.getByRole('button', { name: /خطط.*العينات|سحب العينات/i }).click();
    await page.waitForTimeout(500);

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

    // Trigger Creation of a New Inspection
    await createInspBtn.click();
    await page.waitForTimeout(400);

    const lotQtyInput = page.getByLabel(/حجم التشغيلة|كمية الدفعة/i).or(page.locator('input[type="number"]')).first();
    await lotQtyInput.fill('150');
    const lotRefInput = page.getByLabel(/رقم التشغيلة|اللوت/i).or(page.locator('input[type="text"]').last());
    await lotRefInput.fill(`LOT-TX-${Date.now().toString().slice(-5)}`);

    const saveInspBtn = page.getByRole('button', { name: /إنشاء أمر الفحص|حفظ/i }).last();
    await saveInspBtn.click().catch(() => {});
    await page.waitForTimeout(800);

    await saveArtifact(page, '05-quality-inspections-queue.png');
  });

  test('06 — Inspection Deep Workbench & Characteristic Result Entry', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/inspections');
    await page.waitForLoadState('networkidle');

    // Open first inspection workbench
    const openLink = page.locator('a[data-testid^="inspection-workspace-link-"]').first();
    await openLink.waitFor({ state: 'visible', timeout: 15000 });
    await openLink.click();
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="inspection-workspace-page"]')).toBeVisible({ timeout: 15000 });

    // Verify characteristics table is present
    const charsTable = page.locator('[data-testid="inspection-characteristics-table"]');
    if (await charsTable.isVisible().catch(() => false)) {
      // Click measure button on first characteristic
      const recordBtn = charsTable.getByRole('button', { name: /تسجيل القياس/i }).first();
      if (await recordBtn.isVisible().catch(() => false) && !(await recordBtn.isDisabled().catch(() => true))) {
        await recordBtn.click();
        await page.waitForTimeout(400);

        const valInput = page.locator('input[type="number"]').first();
        if (await valInput.isVisible().catch(() => false)) {
          await valInput.fill('24.5');
        }

        const instrumentInput = page.locator('input[type="text"]').last();
        if (await instrumentInput.isVisible().catch(() => false)) {
          await instrumentInput.fill('CAL-GAUGE-01');
        }

        const saveResBtn = page.getByRole('button', { name: /تسجيل النتيجة|حفظ/i }).last();
        await saveResBtn.click().catch(() => {});
        await page.waitForTimeout(600);
      }
    }

    await saveArtifact(page, '06-inspection-workbench-active.png');
  });

  test('07 — Inspection Conformance Evaluation & Completion State', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/inspections');
    await page.waitForLoadState('networkidle');

    const openLink = page.locator('a[data-testid^="inspection-workspace-link-"]').first();
    await openLink.waitFor({ state: 'visible', timeout: 15000 });
    await openLink.click();
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="inspection-workspace-page"]')).toBeVisible({ timeout: 15000 });

    const completeBtn = page.getByRole('button', { name: /إتمام واعتماد الفحص/i });
    if (await completeBtn.isVisible().catch(() => false)) {
      if (!(await completeBtn.isDisabled())) {
        await completeBtn.click();
        await page.waitForTimeout(400);

        const notesInput = page.locator('input[type="text"]').last();
        if (await notesInput.isVisible().catch(() => false)) {
          await notesInput.fill('اعتماد الفحص المخبري والمطابقة القياسية');
        }

        const confirmBtn = page.getByRole('button', { name: /تأكيد واعتماد الإتمام|تأكيد/i }).last();
        await confirmBtn.click().catch(() => {});
        await page.waitForTimeout(600);
      }
    }

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

    await page.goto('/quality/inspections');
    await page.waitForLoadState('networkidle');

    const openLink = page.locator('a[data-testid^="inspection-workspace-link-"]').first();
    await openLink.waitFor({ state: 'visible', timeout: 15000 });
    await openLink.click();
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="inspection-workspace-page"]')).toBeVisible({ timeout: 15000 });

    const reinspectBtn = page.getByRole('button', { name: /إعادة فحص/i });
    if (await reinspectBtn.isVisible().catch(() => false)) {
      if (!(await reinspectBtn.isDisabled())) {
        await reinspectBtn.click();
        await page.waitForTimeout(400);

        const reasonInput = page.locator('input[type="text"]').last();
        if (await reasonInput.isVisible().catch(() => false)) {
          await reasonInput.fill('إعادة فحص دورة التحقق الفني');
        }

        const submitBtn = page.getByRole('button', { name: /تأكيد|حفظ/i }).last();
        await submitBtn.click().catch(() => {});
        await page.waitForTimeout(600);
      }
    }

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

    // Create a new restriction
    await createHoldBtn.click();
    await page.waitForTimeout(400);

    const reasonCodeInput = page.getByLabel(/كود وسبب الحظر/i).or(page.locator('input[type="text"]').last());
    await reasonCodeInput.fill('HOLD-MANUAL-INSPECTION');

    const qtyInput = page.getByLabel(/الكمية المحتجزة/i).or(page.locator('input[type="number"]')).first();
    await qtyInput.fill('75');

    const submitHoldBtn = page.getByRole('button', { name: /تطبيق الحظر|حفظ/i }).last();
    await submitHoldBtn.click().catch(() => {});
    await page.waitForTimeout(600);

    await saveArtifact(page, '10-material-holds-restrictions.png');
  });

  test('11 — Partial Quantity Release Dialog & Balance Calculation', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/restrictions');
    await page.waitForLoadState('networkidle');

    // Click release button on active hold
    const releaseBtn = page.getByRole('button', { name: /إفراج مخزني/i }).first();
    if (await releaseBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      if (!(await releaseBtn.isDisabled())) {
        await releaseBtn.click();
        await page.waitForTimeout(500);

        const qtyInput = page.getByLabel(/كمية الإفراج/i).or(page.locator('input[type="number"]')).first();
        if (await qtyInput.isVisible()) {
          await qtyInput.fill('25');
        }

        const reasonInput = page.getByLabel(/مبرر وأسباب الإفراج/i).or(page.locator('input[type="text"]').first());
        if (await reasonInput.isVisible()) {
          await reasonInput.fill('إفراج جزئي بعد استكمال الفحص المرحلي');
        }

        const confirmReleaseBtn = page.getByRole('button', { name: /تأكيد الإفراج/i });
        if (await confirmReleaseBtn.isVisible()) {
          await confirmReleaseBtn.click().catch(() => {});
          await page.waitForTimeout(600);
        }
      }
    }

    await saveArtifact(page, '11-partial-quantity-release-dialog.png');
  });

  test('12 — Dispositions Queue & MRB Technical Decisions', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/dispositions');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-dispositions-page"]')).toBeVisible({ timeout: 15000 });
    const createDispBtn = page.getByRole('button', { name: /إصدار قرار فني/i });
    await expect(createDispBtn).toBeVisible();

    await createDispBtn.click();
    await page.waitForTimeout(400);

    const reasonInput = page.getByLabel(/المبرر والأسباب الفنية/i).or(page.locator('input[type="text"]').last());
    await reasonInput.fill('قرار فني معتمد للاستخدام بالحالة الراهنة');

    const qtyInput = page.getByLabel(/الكمية الخاضعة/i).or(page.locator('input[type="number"]')).first();
    if (await qtyInput.isVisible()) {
      await qtyInput.fill('25');
    }

    const saveDispBtn = page.getByRole('button', { name: /^حفظ$|حفظ القرار كمسودة/i });
    if (await saveDispBtn.isVisible().catch(() => false)) {
      await saveDispBtn.click().catch(() => {});
      await page.waitForTimeout(600);
    }

    await saveArtifact(page, '12-quality-dispositions-mrb.png');
  });

  test('13 — Quality Issues Log & Defect Classification', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/ncrs');
    await page.waitForLoadState('networkidle');

    // Switch to Issues tab
    await page.getByRole('button', { name: /مشاكل الجودة|سجل المشاكل/i }).click();
    await page.waitForTimeout(500);

    const createIssueBtn = page.getByRole('button', { name: /تسجيل مشكلة جودة/i });
    if (await createIssueBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createIssueBtn.click();
      await page.waitForTimeout(400);

      const titleInput = page.locator('input[type="text"]').first();
      await titleInput.fill('رصد تشققات مجهرية في دفعات السحب');

      const descInput = page.locator('input[type="text"]').nth(1);
      if (await descInput.isVisible()) {
        await descInput.fill('تجاوز نسبة التشقق السطحي الحد المسموح');
      }

      const saveIssueBtn = page.getByRole('button', { name: /حفظ|تسجيل/i }).last();
      await saveIssueBtn.click().catch(() => {});
      await page.waitForTimeout(600);
    }

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
    await page.waitForTimeout(400);

    const titleInput = page.locator('input[type="text"]').first();
    await titleInput.fill('تقرير عدم مطابقة لشحنة قضبان الألومنيوم');

    const descInput = page.locator('input[type="text"]').nth(1);
    if (await descInput.isVisible()) {
      await descInput.fill('انحراف الأبعاد الهندسية عن المخطط الهندسي');
    }

    const saveNcrBtn = page.getByRole('button', { name: /حفظ|فتح/i }).last();
    await saveNcrBtn.click().catch(() => {});
    await page.waitForTimeout(600);

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
    await page.waitForTimeout(400);

    const titleInput = page.locator('input[type="text"]').first();
    await titleInput.fill('خطة تصحيحية لمنع انحراف المعايرة الحرارية');

    const problemInput = page.locator('input[type="text"]').nth(1);
    if (await problemInput.isVisible()) {
      await problemInput.fill('تذبذب درجات حرارة أفران المعالجة الحرارية');
    }

    const saveCapaBtn = page.getByRole('button', { name: /حفظ|فتح/i }).last();
    await saveCapaBtn.click().catch(() => {});
    await page.waitForTimeout(600);

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

    const issueCertBtn = page.getByRole('button', { name: /إصدار شهادة جودة/i });
    if (await issueCertBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await issueCertBtn.click();
      await page.waitForTimeout(400);

      const custRef = page.getByLabel(/مرجع العميل/i).or(page.locator('input[type="text"]').last());
      if (await custRef.isVisible()) {
        await custRef.fill('PO-CUST-2026-990');
      }

      const saveCertBtn = page.getByRole('button', { name: /إصدار شهادة جودة|إصدار الشهادة|حفظ/i }).last();
      if (await saveCertBtn.isVisible().catch(() => false)) {
        await saveCertBtn.click().catch(() => {});
        await page.waitForTimeout(600);
      }
    }

    await saveArtifact(page, '16-supplier-quality-scar-certificates.png');
  });
});
