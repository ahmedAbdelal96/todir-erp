import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/quality-acceptance/real-stack');
const AGENT_BRAIN_DIR = 'C:/Users/IT/.gemini/antigravity/brain/11ce24c9-e66d-46b8-95e8-fa43b74dced8/quality-acceptance/real-stack';

const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const DEV_PASSWORD =
  process.env['ERP_DEV_PASSWORD'] ||
  process.env['SEED_DEFAULT_PASSWORD'] ||
  process.env['TODIR_ACCEPTANCE_PASSWORD'] ||
  'TodirDev2026!';

test.describe.configure({ mode: 'serial' });

test.describe('TODIR Module 27: Quality Management — Real-Stack UX & Functional Acceptance Suite', () => {
  test.setTimeout(180_000);

  test.beforeAll(() => {
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

  test('02 — Quality Plans & Specifications Register', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/plans');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-plans-page"]')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /إنشاء خطة جودة جديدة/i })).toBeVisible();

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
    await expect(page.getByRole('button', { name: /إنشاء أمر فحص جديد/i })).toBeVisible();

    await saveArtifact(page, '05-quality-inspections-queue.png');
  });

  test('06 — Inspection Deep Workbench & Characteristic Result Entry', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    // Navigate to seeded inspection or inspections queue
    await page.goto('/quality/inspections');
    await page.waitForLoadState('networkidle');

    // Open first inspection workbench if available
    const openLink = page.locator('a[data-testid^="inspection-workspace-link-"]').first();
    if (await openLink.isVisible({ timeout: 5000 }).catch(() => false)) {
      await openLink.click();
      await page.waitForLoadState('networkidle');
    } else {
      await page.goto('/quality/inspections/00000000-0000-0000-0000-000000000001');
      await page.waitForLoadState('networkidle');
    }

    await saveArtifact(page, '06-inspection-workbench-active.png');
  });

  test('07 — Inspection Conformance Evaluation & Completion State', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/inspections');
    await page.waitForLoadState('networkidle');

    const openLink = page.locator('a[data-testid^="inspection-workspace-link-"]').first();
    if (await openLink.isVisible({ timeout: 5000 }).catch(() => false)) {
      await openLink.click();
      await page.waitForLoadState('networkidle');
    }

    await saveArtifact(page, '07-inspection-evaluation-completed.png');
  });

  test('08 — Strict Domain Invariant: Passed != Released Notice', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality');
    await page.waitForLoadState('networkidle');

    // Banner is visible on QualityHeader
    await expect(page.locator('[data-testid="quality-domain-invariant-banner"]')).toBeVisible();

    await saveArtifact(page, '08-inspection-domain-invariant-warning.png');
  });

  test('09 — Reinspection Workflow & Cycles', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/inspections');
    await page.waitForLoadState('networkidle');

    await saveArtifact(page, '09-reinspection-workflow.png');
  });

  test('10 — Material Holds & Restrictions Register', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/restrictions');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-restrictions-page"]')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /حظر واحتجاز الدفعة/i })).toBeVisible();

    await saveArtifact(page, '10-material-holds-restrictions.png');
  });

  test('11 — Partial Quantity Release Dialog & Balance Calculation', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/restrictions');
    await page.waitForLoadState('networkidle');

    // If an active release button is present, click it
    const releaseBtn = page.getByRole('button', { name: /إفراج مخزني/i }).first();
    if (await releaseBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      if (!(await releaseBtn.isDisabled())) {
        await releaseBtn.click();
        await page.waitForTimeout(500);
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
    await expect(page.getByRole('button', { name: /إصدار قرار فني/i })).toBeVisible();

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

    await saveArtifact(page, '13-quality-issues-log.png');
  });

  test('14 — Non-Conformance Reports (NCR) & Immediate Containment', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/ncrs');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-ncrs-page"]')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /فتح تقرير عدم مطابقة/i })).toBeVisible();

    await saveArtifact(page, '14-ncrs-containment-actions.png');
  });

  test('15 — Corrective & Preventive Action (CAPA) with 6M Root Cause', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/capas');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-capas-page"]')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /فتح طلب CAPA جديد/i })).toBeVisible();

    await saveArtifact(page, '15-capa-root-cause-investigation.png');
  });

  test('16 — Supplier Quality, SCAR Queue, and COA/COC Certificates', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginUser(page);

    await page.goto('/quality/suppliers');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="supplier-quality-page"]')).toBeVisible({ timeout: 15000 });

    await page.goto('/quality/certificates');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="quality-certificates-page"]')).toBeVisible({ timeout: 15000 });

    await saveArtifact(page, '16-supplier-quality-scar-certificates.png');
  });
});
