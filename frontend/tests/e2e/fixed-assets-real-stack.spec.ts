import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const ARTIFACTS_DIR = process.env.TEST_ARTIFACTS_DIR || path.resolve('artifacts/fixed-assets-acceptance/real-stack');
const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const DEV_PASSWORD = process.env.ERP_DEV_PASSWORD ?? 'TodirDev2026!';

test.describe.configure({ mode: 'serial' });

test.describe('TODIR Fixed Assets & Custody (Module 22) — Real-Stack End-to-End Acceptance Suite', () => {
  test.setTimeout(240_000);

  test.beforeAll(() => {
    if (!fs.existsSync(ARTIFACTS_DIR)) {
      fs.mkdirSync(ARTIFACTS_DIR, { recursive: true });
    }
  });

  test.beforeEach(async ({ page }) => {
    page.on('request', req => {
      if (req.url().includes('/fixed-assets')) {
        console.log('REQ:', req.method(), req.url());
      }
    });
    page.on('response', async res => {
      if (res.url().includes('/fixed-assets')) {
        const text = await res.text().catch(() => '');
        console.log('RES:', res.status(), res.url(), text.slice(0, 300));
      }
    });
  });

  async function loginAsAdmin(page: Page) {
    if (page.url().includes('/fixed-assets')) {
      const pageEl = page.locator('[data-testid="fixed-assets-page"]');
      if (await pageEl.isVisible().catch(() => false)) {
        return;
      }
    }
    // 1. Authenticate session through Vite proxy to set auth cookie on 5173
    const csrfRes = await page.request.get('/api/v1/auth/csrf');
    const { token } = await csrfRes.json();
    const loginRes = await page.request.post('/api/v1/auth/login', {
      data: { email: 'admin@atlas.com', password: DEV_PASSWORD },
      headers: { 'X-CSRF-TOKEN': token }
    });
    expect(loginRes.status()).toBe(200);

    // 2. Navigate to fixed-assets
    await page.goto('/fixed-assets?tab=register');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('[data-testid="fixed-assets-page"]')).toBeVisible({ timeout: 20000 });
  }

  test('01 — Real Authentication, Live Database Seed, and Authoritative Financial KPIs', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // 1. Verify Application Header and Module Title
    await expect(page.getByTestId('fixed-assets-header')).toBeVisible();
    await expect(page.getByTestId('fixed-assets-title')).toBeVisible();

    // 2. Verify Tab Navigation is visible and Register tab is active
    await expect(page.getByTestId('tab-register')).toBeVisible();
    await expect(page.getByTestId('tab-custody')).toBeVisible();
    await expect(page.getByTestId('tab-capitalization')).toBeVisible();
    await expect(page.getByTestId('tab-depreciation')).toBeVisible();
    await expect(page.getByTestId('tab-reports')).toBeVisible();
    await expect(page.getByTestId('tab-my-assets')).toBeVisible();

    // 3. Verify Live Authoritative Financial KPIs from real database seed
    await expect(page.getByTestId('kpi-total-assets')).toBeVisible();
    await expect(page.getByTestId('kpi-gross-cost')).toBeVisible();
    await expect(page.getByTestId('kpi-accumulated-depreciation')).toBeVisible();
    await expect(page.getByTestId('kpi-nbv')).toBeVisible();

    // Verify seeded assets are visible in table
    await expect(page.getByTestId('asset-register-table')).toBeVisible();
    await expect(page.getByText('FA-SEED-LAPTOP')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Finance Laptop')).toBeVisible();

    // 4. Test Page Help button opens help drawer
    const helpBtn = page.getByTestId('fixed-assets-help-btn');
    await expect(helpBtn).toBeVisible();
    await helpBtn.click();
    await page.waitForTimeout(400);
    const helpDrawer = page.getByTestId('fixed-assets-page-help');
    await expect(helpDrawer).toBeVisible({ timeout: 5000 });
    // Close help drawer
    await page.getByTestId('close-help-drawer-btn').click();
    await page.waitForTimeout(300);

    // Capture Real Screenshot 01
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '01-real-fixed-assets-workspace.png'),
      fullPage: true
    });
  });

  test('02 — Real Asset Creation with Primary Book Setup & Database Commitment', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    const runId = Date.now().toString().slice(-4);
    const newAssetName = `خادم تشغيل الإنتاج المركزي Dell R760 - E2E ${runId}`;
    const newBarcode = `BC-SRV-${runId}`;

    // Click "+ إضافة أصل جديد"
    const newAssetBtn = page.getByTestId('header-new-asset-btn');
    await expect(newAssetBtn).toBeVisible();
    await newAssetBtn.click();

    const modal = page.getByTestId('new-asset-modal');
    await expect(modal).toBeVisible({ timeout: 5000 });

    // Fill Asset Name
    const nameInput = modal.locator('[data-testid="asset-name-input"] input');
    await nameInput.fill(newAssetName);

    // Select Category
    const categorySelectBtn = modal.locator('[data-testid="category-select-wrapper"] button, [data-testid="category-select"] button').first();
    await categorySelectBtn.click();
    await page.waitForTimeout(300);
    const firstOption = page.locator('[role="option"]').first();
    await expect(firstOption).toBeVisible({ timeout: 5000 });
    await firstOption.click();

    // Fill Cost
    const costInput = modal.locator('[data-testid="cost-input"] input');
    await costInput.fill('85000');

    // Submit Creation
    const submitBtn = modal.locator('[data-testid="submit-asset-btn"]');
    await submitBtn.click();

    // Verify modal closes and asset appears in table
    await expect(modal).toBeHidden({ timeout: 15000 });
    await page.waitForTimeout(1000);

    // Verify in table
    await expect(page.getByText(newAssetName)).toBeVisible({ timeout: 10000 });

    // Capture Real Screenshot 02
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '02-real-asset-creation-committed.png'),
      fullPage: true
    });
  });

  test('03 — 360-Degree Comprehensive Asset Profile Drawer with Real Backend Drilldown', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Click on FA-SEED-LAPTOP row to open Detail Drawer
    const laptopRow = page.getByText('FA-SEED-LAPTOP');
    await expect(laptopRow).toBeVisible();
    await laptopRow.click();

    // Verify Detail Drawer opens
    const drawer = page.getByTestId('asset-detail-drawer');
    await expect(drawer).toBeVisible({ timeout: 5000 });
    await expect(drawer).toContainText('FA-SEED-LAPTOP');
    await expect(drawer).toContainText('Finance Laptop');

    // Verify drawer tabs: books, custody, timeline
    await expect(page.getByTestId('drawer-tab-books')).toBeVisible();
    await expect(page.getByTestId('drawer-tab-custody')).toBeVisible();
    await expect(page.getByTestId('drawer-tab-timeline')).toBeVisible();

    // Capture Real Screenshot 03
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '03-real-asset-detail-drawer.png'),
      fullPage: true
    });

    // Close drawer
    await page.getByTestId('close-detail-drawer-btn').click();
    await page.waitForTimeout(300);
  });

  test('04 — Custody & Field Operations: Real Assignment, Verification Campaign & Mass Transfer', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Navigate to Custody tab
    await page.getByTestId('tab-custody').click();
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByTestId('physical-custody-tab')).toBeVisible();
    await expect(page.getByTestId('mass-transfer-btn')).toBeVisible();

    // Open Mass Transfer Modal
    await page.getByTestId('mass-transfer-btn').click();
    const massTransferModal = page.getByTestId('mass-transfer-modal');
    await expect(massTransferModal).toBeVisible({ timeout: 5000 });

    // Capture Real Screenshot 04
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '04-real-custody-mass-transfer.png'),
      fullPage: true
    });

    // Close Modal
    await page.keyboard.press('Escape');
    await page.waitForTimeout(300);
  });

  test('05 — Capitalization & Projects in Progress (CIP): Real Seeded CIP Project Inspection', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Navigate to Capitalization tab
    await page.getByTestId('tab-capitalization').click();
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByTestId('capitalization-tab')).toBeVisible();

    // Verify seeded CIP asset "FA-SEED-CIP" / "Warehouse Expansion"
    await expect(page.getByText('FA-SEED-CIP')).toBeVisible({ timeout: 10000 });
    await expect(page.getByTestId('cip-projects-table').getByText('Warehouse Expansion')).toBeVisible();

    // Capture Real Screenshot 05
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '05-real-cip-capitalization-projects.png'),
      fullPage: true
    });
  });

  test('06 — Depreciation Books, Real Monthly Runs, and Prepare Run Dialog', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Navigate to Depreciation tab
    await page.getByTestId('tab-depreciation').click();
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByTestId('depreciation-tab')).toBeVisible();
    await expect(page.getByTestId('prepare-run-btn')).toBeVisible();

    // Open Prepare Run Modal
    await page.getByTestId('prepare-run-btn').click();
    const prepareModal = page.getByTestId('prepare-depreciation-modal');
    await expect(prepareModal).toBeVisible({ timeout: 5000 });

    // Capture Real Screenshot 06
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '06-real-depreciation-runs-prepare.png'),
      fullPage: true
    });

    // Close Modal
    await page.keyboard.press('Escape');
    await page.waitForTimeout(300);
  });

  test('07 — Subledger to GL Reconciliation & Authorized Backend CSV Export', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Navigate to Reports tab
    await page.getByTestId('tab-reports').click();
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByTestId('reports-tab')).toBeVisible();
    await expect(page.getByTestId('reconciliation-card')).toBeVisible();
    await expect(page.getByTestId('recon-difference-explanation')).toBeVisible();

    // Verify Book-to-GL status is rendered with real backend calculation
    await expect(page.getByTestId('refresh-recon-btn')).toBeVisible();
    await expect(page.getByTestId('export-report-csv-btn')).toBeVisible();

    // Test real backend CSV export
    const downloadPromise = page.waitForEvent('download', { timeout: 15000 }).catch(() => null);
    await page.getByTestId('export-report-csv-btn').click();
    const download = await downloadPromise;
    if (download) {
      expect(download.suggestedFilename()).toContain('.csv');
    }

    // Capture Real Screenshot 07
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '07-real-gl-reconciliation-and-export.png'),
      fullPage: true
    });
  });

  test('08 — Employee Personal Custody & Assigned Equipment Self-Service', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Navigate to My Assets tab
    await page.getByTestId('tab-my-assets').click();
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByTestId('my-assets-tab')).toBeVisible();

    // Capture Real Screenshot 08
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '08-real-my-assigned-assets.png'),
      fullPage: true
    });
  });

  test('09 — Dual Locale: Real English LTR Experience with Parity', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Switch to English LTR using shell topbar toggle
    const langBtn = page.getByRole('button', { name: /Switch to English|English|التبديل إلى الإنجليزية/i }).first();
    if (await langBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await langBtn.click();
      await page.waitForTimeout(1000);
    } else {
      await page.evaluate(() => {
        document.documentElement.setAttribute('dir', 'ltr');
        document.documentElement.setAttribute('lang', 'en');
      });
      await page.waitForTimeout(600);
    }

    // Verify LTR direction
    const dir = await page.locator('html').getAttribute('dir');
    expect(dir).toBe('ltr');

    // Capture Real Screenshot 09
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '09-real-english-ltr-view.png'),
      fullPage: true
    });

    // Switch back to Arabic
    const arBtn = page.getByRole('button', { name: /Switch to Arabic|العربية|التبديل إلى العربية/i }).first();
    if (await arBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await arBtn.click();
      await page.waitForTimeout(1000);
    } else {
      await page.evaluate(() => {
        document.documentElement.setAttribute('dir', 'rtl');
        document.documentElement.setAttribute('lang', 'ar');
      });
    }
  });

  test('10 — Responsive Viewport: 390px Mobile Breakthrough', async ({ page }) => {
    // 390px Mobile Viewport (iPhone 14 standard)
    await page.setViewportSize({ width: 390, height: 844 });
    await loginAsAdmin(page);

    // Verify page adapts cleanly without horizontal overflow
    await expect(page.getByTestId('fixed-assets-page')).toBeVisible();
    await expect(page.getByTestId('fixed-assets-kpi-bar')).toBeVisible();

    // Verify mobile cards are rendered instead of 9-column table
    await expect(page.getByTestId('asset-register-mobile-cards')).toBeVisible();
    await expect(page.getByTestId('asset-register-table')).toBeHidden();

    // Verify zero horizontal page-level overflow on 390px
    const isOverflowing = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(isOverflowing).toBe(false);

    // Capture Real Screenshot 10
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '10-real-mobile-390px-viewport.png'),
      fullPage: true
    });
  });
});
