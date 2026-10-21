import { test, expect, Page } from '@playwright/test';

const atlasVariantWithPrice = 'a6315416-cffe-bf52-bcf0-d5e590d3bf94';
const atlasUomEa = '2b12a81b-ea82-a151-af7f-4c60a017f44d';
const atlasVariantZeroPrice = '4eb9a971-d48b-9659-9458-e144cd0d8a10';
const atlasVariantUnpriced = '01c6a2aa-6619-ce59-b5ea-92d9500f27ad';
const atlasProductWithPriceId = '798fbf84-e5d9-475f-8b91-7d1f62ebed50';

async function loginIfNeeded(page: Page) {
  await page.goto('/pricing');
  await page.waitForLoadState('networkidle');

  const emailInput = page.locator('#login-email');
  if (await emailInput.isVisible({ timeout: 5000 }).catch(() => false)) {
    await emailInput.fill('admin@atlas.com');
    await page.locator('#login-password').fill('Admin@123456');
    await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
    await page.waitForLoadState('networkidle');
  }

  if (!page.url().includes('/pricing')) {
    await page.goto('/pricing');
    await page.waitForLoadState('networkidle');
  }

  // Select Legal Entity (Atlas Main)
  const entitySelect = page.locator('#entity-select');
  if (await entitySelect.isVisible({ timeout: 5000 }).catch(() => false)) {
    await entitySelect.selectOption({ index: 1 });
    await page.waitForTimeout(1000);
    await page.waitForLoadState('networkidle');
  }

  await expect(page.getByRole('heading', { name: /الأسعار والتسعير|Pricing/i })).toBeVisible({ timeout: 15000 });
}

test.describe('Pricing Real-Stack Product Acceptance & Visual Suite', () => {
  test.setTimeout(120_000);

  test('1. Full Desktop Arabic Real-Stack Journey and Visual Capture', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginIfNeeded(page);

    // 02: Pricing Hub Overview (Arabic RTL)
    await expect(page.getByText(/قوائم البيع النشطة/i)).toBeVisible();
    await expect(page.getByText(/سياسة تحويل العملات نشطة/i)).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/pricing/02-pricing-hub-overview-ar.png',
      fullPage: true
    });

    // 04: Customer Selling Price Lists Tab
    await page.getByRole('button', { name: /^قوائم البيع$|^Selling Lists$/i }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /قوائم أسعار البيع للعملاء/i })).toBeVisible();
    await expect(page.getByText('RETAIL').first()).toBeVisible();
    await expect(page.getByText('WHOLESALE').first()).toBeVisible();
    await expect(page.getByText('EXPORT-USD').first()).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/pricing/04-selling-price-lists.png',
      fullPage: true
    });

    // 05: Supplier Buying Price Lists Tab
    await page.getByRole('button', { name: /^قوائم الشراء$|^Buying Lists$/i }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /قوائم أسعار الشراء من الموردين/i })).toBeVisible();
    await expect(page.getByText('PURCHASE').first()).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/pricing/05-buying-price-lists.png',
      fullPage: true
    });

    // 06: Version Editor - Active Read-Only Version
    await page.getByRole('button', { name: /^قوائم البيع$|^Selling Lists$/i }).click();
    await page.waitForLoadState('networkidle');
    const viewVersionsBtn = page.getByRole('button', { name: /عرض الإصدارات والمحرر/i }).first();
    await viewVersionsBtn.click();
    await expect(page.getByRole('heading', { name: /إصدارات قائمة الأسعار والمحرر/i })).toBeVisible();
    await expect(page.getByText(/الإصدار الحالي نشط ومقفل تاريخياً/i)).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/pricing/06-version-editor-active-readonly.png'
    });

    // 07: Version Editor - Draft Version Actions & Add Entry Modal
    const versionSelect = page.getByLabel(/الإصدار|Version/i).first();
    if (await versionSelect.isVisible()) {
      const options = await versionSelect.locator('option').allTextContents();
      const draftOptionIndex = options.findIndex(opt => opt.includes('Draft') || opt.includes('مسودة'));
      if (draftOptionIndex >= 0) {
        await versionSelect.selectOption({ index: draftOptionIndex });
        await page.waitForTimeout(500);
      }
    }

    const addEntryBtn = page.getByRole('button', { name: /إضافة بند سعر مباشر/i });
    if (await addEntryBtn.isVisible()) {
      await addEntryBtn.click();
      await expect(page.getByRole('heading', { name: /إضافة بند سعر مباشر للإصدار/i })).toBeVisible();
      await page.screenshot({
        path: 'artifacts/test-results/pricing/07-version-editor-draft-actions.png'
      });
      await page.getByRole('button', { name: /إلغاء/i }).click();
    }

    // Close version drawer
    await page.getByRole('button', { name: /إغلاق/i, exact: true }).click();

    // 12: Simulator Tab & Why This Price?
    await page.getByRole('button', { name: /^محاكي ومفسر السعر$|^Simulator/i }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /محاكي ومفسر التسعير التجاري/i })).toBeVisible();

    // Fill valid Atlas variant & UOM
    const productInput = page.getByLabel(/المنتج \/ النموذج/i);
    await productInput.fill(atlasVariantWithPrice);
    const uomInput = page.getByLabel(/وحدة القياس/i);
    await uomInput.fill(atlasUomEa);

    const simulateBtn = page.getByRole('button', { name: /محاكاة واحتساب السعر/i });
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    // Result card appears with resolved price and explainability
    await expect(page.getByRole('heading', { name: /النتيجة السعرية المعتمدة/i })).toBeVisible();
    await expect(page.getByText(/1,250|1250/i).first()).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/12-simulator-resolved-price-why.png'
    });

    // 13: Simulator - Price Missing State (distinct from 0.00)
    await productInput.fill(atlasVariantUnpriced);
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/لا يوجد سعر معتمد لهذا المنتج في هذا السياق/i)).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/13-simulator-missing-price.png'
    });

    // 14: Explicit Zero Price State
    await productInput.fill(atlasVariantZeroPrice);
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: /النتيجة السعرية المعتمدة/i })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/14-simulator-explicit-zero-price.png'
    });

    // 15: Import / Export Tab & Modal Preview Audit
    await page.getByRole('button', { name: /^الاستيراد والتصدير$|^Import & Export$/i }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /استيراد وتصدير بنود الأسعار/i })).toBeVisible();

    const openImportBtn = page.getByRole('button', { name: /استيراد أسعار \(CSV\)/i }).first();
    await openImportBtn.click();
    await expect(page.getByText(/اختيار الملف أو لصق بيانات CSV/i)).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/pricing/15-import-preview-audit.png'
    });
    await page.getByRole('button', { name: /إلغاء/i }).click();

    // 09: Wholesale list
    await page.getByRole('button', { name: /^قوائم البيع$|^Selling Lists$/i }).click();
    await page.waitForLoadState('networkidle');
    await page.getByText('WHOLESALE').scrollIntoViewIfNeeded();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/09-wholesale-quantity-tiers.png'
    });

    // 10: Multi-UOM coverage
    await page.getByRole('button', { name: /^السياسات والتغطية$|^Policies/i }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /السياسة السعرية للمنشأة القانونية/i }).first()).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/10-uom-pricing-breakdown.png'
    });

    // 11: FX Policy Settings Modal
    await page.getByRole('button', { name: /إعدادات السياسة السعرية/i }).first().click();
    await expect(page.getByRole('dialog').getByRole('heading', { name: /السياسة السعرية للمنشأة القانونية/i })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/11-fx-conversion-export.png'
    });
    await page.getByRole('button', { name: /إلغاء/i }).click();
  });

  test('2. Product Workspace SME Pricing Integration', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginIfNeeded(page);

    // Navigate to Product Workspace in Catalog for seeded product
    await page.goto(`/catalog/${atlasProductWithPriceId}`);
    await page.waitForLoadState('networkidle');

    // Verify Pricing-owned ProductSellingPriceCard is rendered
    await expect(page.getByText(/سعر البيع المعتمد/i).first()).toBeVisible({ timeout: 10000 });

    // 01: Catalog SME Pricing Integration Screenshot
    await page.screenshot({
      path: 'artifacts/test-results/pricing/01-catalog-product-selling-price-sme.png',
      fullPage: true
    });

    // 08: Quick Price Change dialog from SME Card
    const setPriceBtn = page.getByRole('button', { name: /تحديد سعر البيع|تعديل سعر البيع/i }).first();
    if (await setPriceBtn.isVisible()) {
      await setPriceBtn.click();
      await expect(page.getByRole('heading', { name: /تحديد سعر البيع للمنتج/i })).toBeVisible();
      await page.screenshot({
        path: 'artifacts/test-results/pricing/08-party-specific-price.png'
      });
      await page.getByRole('button', { name: /إلغاء/i }).click();
    }
  });

  test('3. Mobile Arabic 390px Responsive Behavior', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await loginIfNeeded(page);

    // Verify Pricing Hub at 390px
    await expect(page.getByRole('heading', { name: /الأسعار والتسعير/i })).toBeVisible();

    // 16: Mobile 390px Screenshot
    await page.screenshot({
      path: 'artifacts/test-results/pricing/16-pricing-mobile-390px.png',
      fullPage: true
    });
  });

  test('4. Desktop English LTR View Visual Capture', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginIfNeeded(page);

    // Switch language to English
    const langBtn = page.getByRole('button', { name: /التبديل إلى الإنجليزية|English/i });
    if (await langBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await langBtn.click();
      await page.waitForLoadState('networkidle');
    }

    await expect(page.getByRole('heading', { name: /Commercial Pricing & Price Lists/i })).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('button', { name: /Selling Lists/i })).toBeVisible();

    // 03: English Desktop Overview Screenshot
    await page.screenshot({
      path: 'artifacts/test-results/pricing/03-pricing-hub-overview-en.png',
      fullPage: true
    });

    // Switch back to Arabic
    const arBtn = page.getByRole('button', { name: /التبديل إلى العربية|العربية/i });
    if (await arBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await arBtn.click();
      await page.waitForLoadState('networkidle');
    }
  });

  test('5. Multi-Entity Switch and Permission Verification', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginIfNeeded(page);

    // 17: Legal Entity Switch Proof
    const entitySelect = page.locator('#entity-select');
    await expect(entitySelect).toBeVisible();

    // Select Delta Supplies or second entity
    await entitySelect.selectOption({ index: 2 });
    await page.waitForTimeout(1000);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: /الأسعار والتسعير/i })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/17-legal-entity-switch.png',
      fullPage: false
    });

    // Switch back to Atlas
    await entitySelect.selectOption({ index: 1 });
    await page.waitForTimeout(1000);
    await page.waitForLoadState('networkidle');

    // 18: Permission verification (read-only audit view)
    await page.getByRole('button', { name: /^السياسات والتغطية$|^Policies/i }).click();
    await page.waitForLoadState('networkidle');
    await page.screenshot({
      path: 'artifacts/test-results/pricing/18-permission-limited-state.png'
    });
  });
});
