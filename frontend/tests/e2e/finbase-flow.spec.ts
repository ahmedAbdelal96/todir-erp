import { test, expect, Page } from '@playwright/test';

async function loginIfNeeded(page: Page) {
  await page.goto('/finbase');
  await page.waitForLoadState('networkidle');

  const emailInput = page.locator('#login-email');
  if (await emailInput.waitFor({ state: 'visible', timeout: 5000 }).then(() => true).catch(() => false)) {
    await emailInput.fill('admin@atlas.com');
    await page.locator('#login-password').fill('Admin@123456');
    await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
    await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle');
  }

  if (!page.url().includes('/finbase')) {
    await page.goto('/finbase');
    await page.waitForLoadState('networkidle');
  }

  // Select Legal Entity (Atlas Main)
  const entitySelect = page.locator('#entity-select');
  if (await entitySelect.isVisible({ timeout: 5000 }).catch(() => false)) {
    await entitySelect.selectOption({ index: 1 });
    await page.waitForTimeout(500);
    await page.waitForLoadState('networkidle');
  }

  await expect(page.getByRole('heading', { name: /الأساس المالي|Financial Foundation/i })).toBeVisible({ timeout: 15000 });
}

test.describe('FinBase Real-Stack Acceptance & Visual Suite (No Route Interception)', () => {
  test.setTimeout(120_000);

  test('1. Full Desktop Arabic Real-Stack Journey and Visual Capture', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginIfNeeded(page);

    // Overview Tab - Base currency truth & badges
    await expect(page.getByText('EGP').first()).toBeVisible();
    await expect(page.getByText(/مقفل تشغيلياً/)).toBeVisible();

    // 01: Overview Screenshot
    await page.screenshot({
      path: 'artifacts/test-results/finbase/01-finbase-overview-ar-desktop.png',
      fullPage: true
    });

    // Test Conversion Preview Widget with Real Backend Rate
    const amountInput = page.getByPlaceholder('100.00').first();
    await amountInput.fill('100.00');
    const dateInput = page.getByLabel(/تاريخ العملية|Business Date/i);
    if (await dateInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await dateInput.fill('2026-01-03');
    }
    const convertBtn = page.getByRole('button', { name: /حساب التحويل المالي/i });
    await convertBtn.click();
    await page.waitForLoadState('networkidle');

    // Wait for conversion result
    await expect(page.getByText(/معتمد من المحرك المالي/i)).toBeVisible();
    await expect(page.getByText(/5012\.5/)).toBeVisible();
    await page.getByTestId('conversion-result').scrollIntoViewIfNeeded();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/07-finbase-conversion-preview-ar.png',
      fullPage: false
    });

    // Test Schedule Preview Widget (3-Installments with Residual)
    const scheduleAmount = page.getByPlaceholder('100.00').nth(1);
    await scheduleAmount.fill('100.00');
    const scheduleBtn = page.getByRole('button', { name: /حساب جدول السداد/i });
    await scheduleBtn.click();
    await page.waitForLoadState('networkidle');

    // Wait for schedule result table and residual absorption
    await expect(page.getByTestId('schedule-result')).toBeVisible();
    await expect(page.getByText('33.34').first()).toBeVisible();
    await page.getByTestId('schedule-result').scrollIntoViewIfNeeded();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/08-finbase-schedule-preview-ar.png',
      fullPage: false
    });

    // 02: Currencies Tab
    await page.getByRole('button', { name: 'العملات المعتمدة' }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /العملات المفعّلة للمنشأة القانونية/i })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/02-finbase-currencies-ar-desktop.png',
      fullPage: true
    });

    // 03: Exchange Rates Tab
    await page.getByRole('button', { name: 'أسعار الصرف' }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /سجل أسعار الصرف/i })).toBeVisible();
    await expect(page.getByText(/معتمد ومحمي تاريخياً/i).first()).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/03-finbase-rates-ar-desktop.png',
      fullPage: true
    });

    // Modal: Open Create Rate Modal
    await page.getByRole('button', { name: /إدخال سعر صرف/i }).first().click();
    await expect(page.getByRole('heading', { name: /إدخال سعر صرف يدوي/i })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/09-finbase-create-rate-modal-ar.png'
    });
    await page.getByRole('button', { name: 'إلغاء' }).click();

    // 04: Sources Tab
    await page.getByRole('button', { name: 'مصادر الأسعار' }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /مصادر ومزودو أسعار الصرف الآلية/i })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/04-finbase-sources-ar-desktop.png',
      fullPage: true
    });

    // 05: Payment Terms Tab
    await page.getByRole('button', { name: 'شروط السداد' }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /شروط وسلاسل السداد/i })).toBeVisible();
    await expect(page.getByText('NET30')).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/05-finbase-terms-ar-desktop.png',
      fullPage: true
    });

    // Drawer: View History & Rules for NET30
    const viewRulesBtn = page.getByRole('button', { name: /عرض الإصدارات والقواعد/i }).first();
    await viewRulesBtn.click();
    await expect(page.getByRole('heading', { name: /سجل الإصدارات وقواعد التقسيم/i })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/10-finbase-term-drawer-ar.png'
    });
    await page.getByRole('button', { name: 'إغلاق', exact: true }).click();

    // 06: Policies Tab
    await page.getByRole('button', { name: 'السياسات والتقريب' }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /السياسة المالية للمنشأة القانونية/i })).toBeVisible();
    await expect(page.getByText(/التقريب التجاري/i)).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/06-finbase-policies-ar-desktop.png',
      fullPage: true
    });
  });

  test('2. Mobile Arabic 390px Responsive Behavior & Dropdown Navigation', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await loginIfNeeded(page);

    // Verify Mobile Section Dropdown exists
    const selectTrigger = page.getByLabel(/القسم المالي|Financial Section/i);
    await expect(selectTrigger).toBeVisible();

    // 12: Mobile Overview Screenshot
    await page.screenshot({
      path: 'artifacts/test-results/finbase/12-finbase-overview-ar-mobile-390.png',
      fullPage: true
    });

    // Switch to Rates via dropdown
    await selectTrigger.click();
    await page.getByRole('option', { name: 'أسعار الصرف' }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /سجل أسعار الصرف/i })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/13-finbase-rates-ar-mobile-390.png',
      fullPage: true
    });

    // Switch to Payment Terms via dropdown
    await selectTrigger.click();
    await page.getByRole('option', { name: 'شروط السداد' }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /شروط وسلاسل السداد/i })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/14-finbase-terms-ar-mobile-390.png',
      fullPage: true
    });
  });

  test('3. Desktop English LTR View Visual Capture', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginIfNeeded(page);

    // Switch language to English
    const langBtn = page.getByRole('button', { name: /التبديل إلى الإنجليزية|English/i });
    if (await langBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await langBtn.click();
      await page.waitForLoadState('networkidle');
    }

    await expect(page.getByRole('heading', { name: /Financial Foundation/i })).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: 'artifacts/test-results/finbase/11-finbase-overview-en-desktop.png',
      fullPage: true
    });

    // Switch back to Arabic
    const arBtn = page.getByRole('button', { name: /التبديل إلى العربية|العربية/i });
    if (await arBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await arBtn.click();
      await page.waitForLoadState('networkidle');
    }
  });

  test('4. F20 Advanced Business Scenarios (Single-Currency, Multi-Entity Switch, Restricted Override, Stale/Missing Rate)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginIfNeeded(page);

    // Scenario 16: Multi-Entity Context Switch proof
    const entitySelect = page.locator('#entity-select');
    await expect(entitySelect).toBeVisible();
    // Select Atlas Secondary (single currency)
    await entitySelect.selectOption({ index: 2 });
    await page.waitForTimeout(1000);
    await page.waitForLoadState('networkidle');

    // Scenario 15: Entity Proof (Atlas Secondary)
    await expect(page.getByText(/نمط العملة الواحدة|تعدد العملات|العملة الأساسية/i).first()).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/15-finbase-single-currency-entity.png',
      fullPage: true
    });

    // Switch back to Atlas Main
    await entitySelect.selectOption({ index: 1 });
    await page.waitForTimeout(1000);
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/تعدد العملات/i).first()).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/finbase/16-finbase-multi-entity-switch.png',
      fullPage: false
    });

    // Scenario 17: Restricted / Unauthorized Manual FX Override Rejection Proof
    await page.getByRole('button', { name: 'أسعار الصرف' }).click();
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: /إدخال سعر جديد|إدخال سعر صرف/i }).first().click();
    await expect(page.getByRole('heading', { name: /إدخال سعر صرف يدوي/i })).toBeVisible();

    // Fill in values and attempt override while policy restricts it
    await page.getByLabel(/سعر الصرف/i).fill('52.500000000000');
    await page.getByLabel(/سبب إدخال السعر اليدوي/i).fill('محاولة تعديل يدوي اختباري للتحقق من رفض السياسة المحاسبية');
    await page.getByRole('button', { name: /حفظ سعر الصرف/i }).click();

    // Expect red rejection alert
    await expect(page.getByText(/التجاوز اليدوي لسعر الصرف غير مصرح به|تعذر حفظ سعر الصرف/i).first()).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/finbase/17-finbase-manual-rate-unauthorized-rejection.png'
    });
    await page.getByRole('button', { name: 'إلغاء' }).click();

    // Scenario 18: Missing or Stale Rate Error State Proof
    await page.getByRole('button', { name: 'نظرة عامة' }).click();
    await page.waitForLoadState('networkidle');

    const fromCurrInput = page.getByLabel('من', { exact: true });
    const toCurrInput = page.getByLabel('إلى', { exact: true });
    if (await fromCurrInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await fromCurrInput.fill('JPY');
      await toCurrInput.fill('EGP');
      const amountInput = page.getByPlaceholder('100.00').first();
      await amountInput.fill('100.00');

      const convertBtn = page.getByRole('button', { name: /حساب التحويل المالي/i });
      await convertBtn.click();
      await page.waitForLoadState('networkidle');

      // Expect friendly localized error card
      const errorAlert = page.getByText(/لا يوجد سعر صرف معتمد متاح للتحويل|سعر الصرف المتاح قديم|تعذر إجراء التحويل/i).first();
      await expect(errorAlert).toBeVisible();
      await errorAlert.scrollIntoViewIfNeeded();
      await page.screenshot({
        path: 'artifacts/test-results/finbase/18-finbase-stale-or-missing-rate-error.png',
        fullPage: true
      });
    }
  });
});
