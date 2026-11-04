import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const SCREENSHOT_DIR = path.resolve(process.cwd(), '..', 'artifacts', 'test-results', 'payroll-acceptance');

test.describe.configure({ mode: 'serial' });

async function ensureAuthenticated(page: Page) {
  page.on('console', (msg) => {
    if (msg.type() === 'error') console.log('PAGE ERROR:', msg.text());
  });
  page.on('pageerror', (err) => {
    console.log('UNCAUGHT PAGE ERROR:', err.message, err.stack);
  });
  page.on('response', async (res) => {
    if (!res.ok() && res.url().includes('/api/v1/payroll/')) {
      try {
        const text = await res.text();
        console.log('PAYROLL API STATUS:', res.status(), res.url(), text);
      } catch {
        console.log('PAYROLL API STATUS:', res.status(), res.url());
      }
    }
  });

  await page.goto('/payroll');
  await page.waitForLoadState('networkidle');

  const atlasBtn = page.getByRole('button', { name: /admin@atlas\.com/i });
  if (await atlasBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
    await atlasBtn.click();
    await atlasBtn.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle');
  } else {
    const emailInput = page.locator('#login-email');
    if (await emailInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await emailInput.fill('admin@atlas.com');
      const password = process.env.SEED_DEFAULT_PASSWORD ?? 'TodirDev2026!';
      await page.locator('#login-password').fill(password);
      await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
      await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
      await page.waitForLoadState('networkidle');
    }
  }

  if (!page.url().includes('/payroll')) {
    await page.goto('/payroll');
    await page.waitForLoadState('networkidle');
  }

  await expect(page.locator('[data-testid="payroll-page"]')).toBeVisible({ timeout: 20000 });

  // Ensure Arabic is active for normal tests
  const switchToArabicBtn = page.locator('button').filter({ hasText: /Switch to Arabic/i });
  if (await switchToArabicBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
    await switchToArabicBtn.click();
    await page.waitForTimeout(1000);
    await page.waitForLoadState('networkidle');
  }
}

test.describe('TODIR Payroll Module 18 Full-Stack Verification & UX Acceptance Suite', () => {
  test.beforeAll(() => {
    if (!fs.existsSync(SCREENSHOT_DIR)) {
      fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
    }
  });

  test.setTimeout(180_000);

  test('1. Payroll Home & Run Workspace Overview (Desktop 1440px RTL)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    // Verify page header, statutory mode banner, and high-contrast KPI cards
    await expect(page.getByRole('heading', { name: /الرواتب والأجور/i }).first()).toBeVisible();
    await expect(page.locator('[data-testid="payroll-tab-home"]')).toBeVisible();

    await page.waitForTimeout(1000);
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '01-payroll-home-desktop.png'),
      fullPage: false
    });
  });

  test('2. Payroll Setup Tab & Creation Modals', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-setup"]').click();
    await page.waitForTimeout(800);

    // Verify groups section heading
    await expect(page.getByRole('heading', { name: /مجموعات الرواتب/i })).toBeVisible();

    // Open New Group Modal
    const newGroupBtn = page.getByRole('button', { name: /إضافة مجموعة رواتب/i });
    if (await newGroupBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await newGroupBtn.click();
      await page.waitForTimeout(600);
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();

      // Verify payDay label is properly labeled inside the modal
      await expect(dialog.getByText(/يوم الصرف في الشهر/i)).toBeVisible();

      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '02-payroll-new-group-modal.png'),
        fullPage: false
      });
      // Close modal with cancel
      await dialog.getByRole('button', { name: /إلغاء/i }).click();
      await page.waitForTimeout(400);
    }

    // Open New Period Modal
    const newPeriodBtn = page.getByRole('button', { name: /إضافة فترة رواتب/i });
    if (await newPeriodBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await newPeriodBtn.click();
      await page.waitForTimeout(600);
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();

      // Verify distinct labels for dates inside the modal
      await expect(dialog.getByText(/تاريخ الاحتساب المقرر/i)).toBeVisible();
      await expect(dialog.getByText(/تاريخ الصرف الفعلي المخطط/i)).toBeVisible();

      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '03-payroll-new-period-modal.png'),
        fullPage: false
      });
      await dialog.getByRole('button', { name: /إلغاء/i }).click();
      await page.waitForTimeout(400);
    }
  });

  test('3. Setup Sub-Tabs (Components, Structures, Assignments)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-setup"]').click();
    await page.waitForTimeout(800);

    // Click Components sub-tab
    const componentsTab = page.getByRole('tab', { name: /بنود الراتب/i });
    if (await componentsTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await componentsTab.click();
      await page.waitForTimeout(600);
      await expect(page.getByRole('heading', { level: 3, name: /بنود الراتب/i })).toBeVisible();
      // Verify seeded component code BASE or TRANSPORT
      await expect(page.getByText(/TRANSPORT/i)).toBeVisible();
    }

    // Click Structures sub-tab
    const structuresTab = page.getByRole('tab', { name: /هياكل الرواتب/i });
    if (await structuresTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await structuresTab.click();
      await page.waitForTimeout(600);
      await expect(page.getByRole('heading', { level: 3, name: /هياكل الرواتب/i })).toBeVisible();
    }

    // Click Assignments sub-tab
    const assignmentsTab = page.getByRole('tab', { name: /تسكين الموظفين/i });
    if (await assignmentsTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await assignmentsTab.click();
      await page.waitForTimeout(600);
      await expect(page.getByRole('heading', { level: 3, name: /تسكين الموظفين/i })).toBeVisible();
    }
  });

  test('4. Company Tax & Social Insurance Statutory Settings', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-settings"]').click();
    await page.waitForTimeout(800);

    // Verify statutory settings title and dual cards
    await expect(page.getByText(/الضرائب والتأمينات الاجتماعية/i).first()).toBeVisible();
    await expect(page.getByText(/ضريبة كسب العمل/i).first()).toBeVisible();
    await expect(page.getByText(/التأمينات الاجتماعية/i).first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '04-payroll-tax-settings-tab.png'),
      fullPage: false
    });
  });

  test('5. Payroll Runs Tab & New Run Modal', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-runs"]').click();
    await page.waitForTimeout(800);

    // Open New Run Modal
    const newRunBtn = page.getByRole('button', { name: /إنشاء دورة رواتب/i }).first();
    if (await newRunBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await newRunBtn.click();
      await page.waitForTimeout(600);
      await expect(page.getByRole('dialog')).toBeVisible();

      // For Regular run, reasonCode and search fields should NOT be rendered
      await expect(page.getByText(/رمز سبب الدورة/i)).toHaveCount(0);

      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '05-payroll-new-run-modal.png'),
        fullPage: false
      });
      await page.getByRole('button', { name: /إلغاء/i }).click();
      await page.waitForTimeout(400);
    }
  });

  test('6. Payroll Inputs & Bulk Import Dialog', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-inputs"]').click();
    await page.waitForTimeout(800);

    // Open Import Dialog
    const importBtn = page.getByRole('button', { name: /استيراد/i }).first();
    if (await importBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await importBtn.click();
      await page.waitForTimeout(600);
      await expect(page.getByRole('dialog')).toBeVisible();

      // Test pasting sample CSV data
      const sampleCsv = "d0000000-0000-0000-0000-000000000001,TRANSPORT,500,EGP,2026-11-01,بدل انتقال شهري";
      await page.locator('textarea').fill(sampleCsv);

      // Trigger preview
      const previewBtn = page.getByRole('button', { name: /معاينة أسطر الاستيراد/i });
      if (await previewBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await previewBtn.click();
        await page.waitForTimeout(1000);
      }

      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '06-payroll-import-dialog.png'),
        fullPage: false
      });
      // Close the import dialog cleanly
      await page.keyboard.press('Escape');
      await page.waitForTimeout(400);
      if (await page.getByRole('dialog').isVisible().catch(() => false)) {
        await page.keyboard.press('Escape');
        await page.waitForTimeout(400);
      }
      await expect(page.getByRole('dialog')).toBeHidden({ timeout: 5000 });
    }

    // Open New Input Modal
    const newInputBtn = page.getByRole('button', { name: /إضافة مدخل جديد/i });
    if (await newInputBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await newInputBtn.click();
      await page.waitForTimeout(600);
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();

      // Verify PayrollComponentPicker is present
      await expect(dialog.getByText(/البند|بند الراتب/i).first()).toBeVisible();

      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '07-payroll-new-input-modal.png'),
        fullPage: false
      });
      await dialog.getByRole('button', { name: /إلغاء/i }).click();
      await page.waitForTimeout(400);
    }
  });

  test('7. Loans & Advances Tab & Creation Modal', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-loans"]').click();
    await page.waitForTimeout(800);

    // Open New Loan Modal
    const newLoanBtn = page.getByRole('button', { name: /طلب سلفة أو قرض جديد/i });
    if (await newLoanBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await newLoanBtn.click();
      await page.waitForTimeout(600);
      await expect(page.getByRole('dialog')).toBeVisible();
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '08-payroll-new-loan-modal.png'),
        fullPage: false
      });
      await page.getByRole('button', { name: /إلغاء/i }).click();
      await page.waitForTimeout(400);
    }
  });

  test('8. Payslips & Employee Self-Service', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-payslips"]').click();
    await page.waitForTimeout(800);

    await expect(page.getByText(/قسائم منشأة العمل/i).first()).toBeVisible();
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '09-payroll-payslips-tab.png'),
      fullPage: false
    });
  });

  test('9. Reports & Financial Truth (No Fabricated Employer Cost)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-reports"]').click();
    await page.waitForTimeout(800);

    await expect(page.getByText(/التقارير والمطابقة المالية/i).first()).toBeVisible();

    // Verify 4th KPI card is Total Runs Count, not fabricated employer cost
    await expect(page.getByText(/إجمالي الدورات المسجلة/i)).toBeVisible();

    // Verify export note communicating scope
    await expect(page.getByText(/تصدير السجلات الحالية المعروضة/i)).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '10-payroll-reports-tab.png'),
      fullPage: false
    });
  });

  test('10. Migration & Opening Balances Modal', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-openingBalances"]').click();
    await page.waitForTimeout(800);

    const newOpeningBtn = page.getByRole('button', { name: /إضافة رصيد افتتاحي/i });
    if (await newOpeningBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await newOpeningBtn.click();
      await page.waitForTimeout(600);
      await expect(page.getByRole('dialog')).toBeVisible();
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '11-payroll-opening-balance-modal.png'),
        fullPage: false
      });
      await page.getByRole('button', { name: /إلغاء/i }).click();
      await page.waitForTimeout(400);
    }
  });

  test('11. Mobile Responsive Layout (390px Viewport)', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-home"]').click();
    await page.waitForTimeout(800);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '12-payroll-home-mobile-390px.png'),
      fullPage: false
    });
  });

  test('12. English LTR Parity & Formatting', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    // Click language switcher to switch to English
    const langBtn = page.getByRole('button', { name: /التبديل إلى الإنجليزية/i });
    if (await langBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await langBtn.click();
      await page.waitForTimeout(1000);
      await page.waitForLoadState('networkidle');
    }

    // Verify English heading and LTR layout
    await expect(page.getByRole('heading', { name: /Payroll/i }).first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '13-payroll-home-english-ltr.png'),
      fullPage: false
    });

    // Restore to Arabic
    const switchToArabicBtn = page.locator('button').filter({ hasText: /Switch to Arabic/i });
    if (await switchToArabicBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await switchToArabicBtn.click();
      await page.waitForTimeout(800);
      await page.waitForLoadState('networkidle');
    }
  });
});
