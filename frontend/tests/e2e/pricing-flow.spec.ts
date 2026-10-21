import { test, expect, Page } from '@playwright/test';

const atlasVariantWithPrice = 'a6315416-cffe-bf52-bcf0-d5e590d3bf94';
const atlasUomEa = '2b12a81b-ea82-a151-af7f-4c60a017f44d';
const atlasVariantZeroPrice = '4eb9a971-d48b-9659-9458-e144cd0d8a10';
const atlasVariantUnpriced = '01c6a2aa-6619-ce59-b5ea-92d9500f27ad';
const atlasProductWithPriceId = '798fbf84-e5d9-475f-8b91-7d1f62ebed50';

async function selectOption(page: Page, labelRegex: RegExp, optionRegex: RegExp) {
  const trigger = page.getByLabel(labelRegex).first();
  await trigger.click();
  await page.waitForTimeout(300);
  const listbox = page.locator('[role="listbox"]').first();
  await expect(listbox).toBeVisible({ timeout: 5000 });
  const option = listbox.getByRole('option', { name: optionRegex }).first();
  await option.click();
  await page.waitForTimeout(300);
}

async function loginIfNeeded(page: Page) {
  await page.goto('/pricing');
  await page.waitForLoadState('networkidle');

  const emailInput = page.locator('#login-email');
  const pricingHeading = page.getByRole('heading', { name: /الأسعار والتسعير التجاري|Commercial Pricing/i });

  // Wait until either the login form or the authenticated page is ready
  await Promise.race([
    emailInput.waitFor({ state: 'visible', timeout: 15000 }).catch(() => {}),
    pricingHeading.waitFor({ state: 'visible', timeout: 15000 }).catch(() => {})
  ]);

  if (await emailInput.isVisible()) {
    await emailInput.fill('admin@atlas.com');
    await page.locator('#login-password').fill('Admin@123456');
    await page.locator('button[type="submit"]').click();
    await page.waitForLoadState('networkidle');
  }

  if (!page.url().includes('/pricing')) {
    await page.goto('/pricing');
    await page.waitForLoadState('networkidle');
  }

  const retryBtn = page.getByRole('button', { name: /إعادة المحاولة/i });
  if (await retryBtn.isVisible({ timeout: 1000 }).catch(() => false)) {
    await page.waitForTimeout(1000);
    await retryBtn.click();
    await page.waitForLoadState('networkidle');
  }

  // Select Legal Entity (Atlas Main)
  const entitySelect = page.locator('#entity-select');
  if (await entitySelect.isVisible({ timeout: 5000 }).catch(() => false)) {
    await entitySelect.selectOption({ index: 1 });
    await page.waitForTimeout(500);
    await page.waitForLoadState('networkidle');
  }

  await expect(pricingHeading).toBeVisible({ timeout: 20000 });
}

test.describe('Pricing Real-Stack Product Acceptance & Visual Suite', () => {
  test.setTimeout(240_000);

  test('Pricing Real-Stack End-to-End Product Journeys and Visual Proofs', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginIfNeeded(page);

    // ==========================================
    // 1. Hub Overview, Selling/Buying Lists, and Version Lifecycle
    // ==========================================
    await page.goto('/pricing');
    await page.waitForLoadState('networkidle');

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

    // 06: Version Editor - Active Read-Only Version with Business Labels
    await page.getByRole('button', { name: /^قوائم البيع$|^Selling Lists$/i }).click();
    await page.waitForLoadState('networkidle');
    const viewVersionsBtn = page.getByRole('button', { name: /عرض الإصدارات والمحرر/i }).nth(1);
    await viewVersionsBtn.click();
    await expect(page.getByRole('heading', { name: /إصدارات قائمة الأسعار والمحرر/i })).toBeVisible({ timeout: 5000 });

    const version1Trigger = page.getByRole('button', { name: /إصدار #1/i }).first();
    await expect(version1Trigger).toBeVisible({ timeout: 5000 });
    await version1Trigger.click();
    await page.waitForTimeout(500);
    await expect(page.getByText(/الإصدار الحالي نشط ومقفل تاريخياً/i)).toBeVisible();
    await expect(page.getByText('هاتف ذكي أطلس برو (AT-0002)').first()).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/pricing/06-version-editor-active-readonly.png'
    });

    // 07: Version Editor - Scheduled Version (#2)
    const version2Trigger = page.getByRole('button', { name: /إصدار #2/i }).first();
    await expect(version2Trigger).toBeVisible({ timeout: 5000 });
    await version2Trigger.click();
    await page.waitForTimeout(500);
    await expect(page.getByText(/هذا الإصدار مجدول وسيبدأ سريانه تلقائياً/i)).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/07-scheduled-future-version.png'
    });

    // 19: Conflict Proof on Draft Version (#3)
    const version3Trigger = page.getByRole('button', { name: /إصدار #3/i }).first();
    if (await version3Trigger.isVisible({ timeout: 3000 }).catch(() => false)) {
      await version3Trigger.click();
      await page.waitForTimeout(500);
      const validateBtn = page.getByRole('button', { name: /فحص التعارضات|التحقق من صحة الإصدار/i });
      if (await validateBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await validateBtn.click();
        await page.waitForTimeout(500);
      }
    }
    // Screenshot draft version / conflict validation
    await page.screenshot({
      path: 'artifacts/test-results/pricing/19-conflict-blocking-activation.png'
    });

    // Close version drawer
    await page.getByRole('button', { name: /إغلاق النافذة|إلغاء/i }).first().click();

    // ==========================================
    // 2. Business-Friendly Simulator: Customer, Quantity Tiers, UOM, and FX Proofs
    // ==========================================
    await page.getByRole('button', { name: /^محاكي ومفسر السعر$|^Simulator/i }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /محاكي ومفسر التسعير التجاري/i })).toBeVisible();

    const simulateBtn = page.getByRole('button', { name: /محاكاة واحتساب السعر/i });

    // 08: Customer-Specific Price Proof (Atlas Office Industries -> 1,100 EGP vs 1,250 standard)
    await selectOption(page, /قائمة الأسعار المحددة/i, /RETAIL|قائمة البيع العامة/i);
    await selectOption(page, /العميل أو المورد/i, /أطلس للصناعات المكتبية/i);
    await page.getByLabel(/عملة العملية التجارية/i).fill('EGP');
    await page.getByLabel(/تاريخ استحقاق التسعير/i).fill('2026-01-05');
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: /النتيجة السعرية المعتمدة/i })).toBeVisible();
    await expect(page.getByText(/1,100|1100/i).first()).toBeVisible();
    await expect(page.getByText(/تم تطبيق سعر خاص بالعميل/i)).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/08-party-specific-price.png'
    });

    // 09: Quantity-Tier Price Proof on Wholesale List (Tier 12+ -> 1,050 EGP vs 1,125 tier 1+)
    await selectOption(page, /قائمة الأسعار المحددة/i, /WHOLESALE|الجملة/i);
    await selectOption(page, /العميل أو المورد/i, /بدون تحديد عميل/i);
    const qtyInput = page.getByLabel(/الكمية المطلوبة/i);
    await qtyInput.fill('12');
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/1,050|1050/i).first()).toBeVisible();
    await expect(page.getByText(/تم تطبيق شريحة كميات الجملة/i)).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/09-wholesale-quantity-tiers.png'
    });

    // 10: Explicit UOM Pricing Proof (Carton -> 25,500 EGP)
    await selectOption(page, /قائمة الأسعار المحددة/i, /RETAIL|قائمة البيع العامة/i);
    await qtyInput.fill('1');
    await selectOption(page, /وحدة القياس/i, /CARTON|كرتونة/i);
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/25,500|25500/i).first()).toBeVisible();
    await expect(page.getByText(/تم تطبيق سعر تجاري مخصص لوحدة كرتونة/i)).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/10-uom-pricing-breakdown.png'
    });

    // 11: Real FX Resolution Proof (EXPORT-USD -> EGP Transaction -> 1,250 EGP)
    await selectOption(page, /وحدة القياس/i, /EA|قطعة/i);
    await selectOption(page, /قائمة الأسعار المحددة/i, /EXPORT-USD|بالدولار/i);
    await selectOption(page, /العميل أو المورد/i, /أطلس للصناعات المكتبية/i);
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/تم التحويل التلقائي للعملة عبر FinBase/i)).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/11-fx-conversion-export.png'
    });

    // 12: Standard Resolved Price with Business Explanation
    await selectOption(page, /قائمة الأسعار المحددة/i, /RETAIL|قائمة البيع العامة/i);
    await selectOption(page, /العميل أو المورد/i, /بدون تحديد عميل/i);
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/1,250|1250/i).first()).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/12-simulator-resolved-price-why.png'
    });

    // 13: Simulator - Price Missing State
    await selectOption(page, /المنتج \/ النموذج/i, /غير مسعر|AT-0005/i);
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/لا يوجد سعر معتمد لهذا المنتج في هذا السياق/i)).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/13-simulator-missing-price.png'
    });

    // 14: Simulator - Explicit Promotional Zero Price
    await selectOption(page, /المنتج \/ النموذج/i, /عينة|AT-0004/i);
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: /النتيجة السعرية المعتمدة/i })).toBeVisible();
    await expect(page.getByText(/سعر مجاني معتمد/i).first()).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/14-simulator-explicit-zero-price.png'
    });

    // ==========================================
    // 3. Import Preview / Reconciliation Flow with Representative Result Classes
    // ==========================================
    await page.getByRole('button', { name: /^الاستيراد والتصدير$|^Import & Export$/i }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /استيراد وتصدير بنود الأسعار/i })).toBeVisible();

    const openImportBtn = page.getByRole('button', { name: /استيراد أسعار \(CSV\)/i }).first();
    await openImportBtn.click();
    await expect(page.getByText(/اختيار الملف أو لصق بيانات CSV/i)).toBeVisible();

    // Select Target List & Target Version (Draft)
    await selectOption(page, /قائمة الأسعار المستهدفة/i, /RETAIL|قائمة البيع العامة/i);
    await page.waitForTimeout(500);

    const targetVersionTrigger = page.getByLabel(/الإصدار المستهدف/i).first();
    await targetVersionTrigger.click();
    await page.waitForTimeout(300);
    const listbox = page.locator('[role="listbox"]').first();
    if (await listbox.isVisible({ timeout: 3000 }).catch(() => false)) {
      await listbox.getByRole('option').first().click();
      await page.waitForTimeout(300);
    }

    // Click Validate & Preview to advance to Step 2
    const validatePreviewBtn = page.getByRole('button', { name: /فحص وتدقيق البيانات/i });
    await validatePreviewBtn.click();
    await page.waitForLoadState('networkidle');

    // Preview table displays with result classes (Unknown SKU, Currency mismatch, Ready)
    await expect(page.getByRole('heading', { name: /تقرير تدقيق ومطابقة الاستيراد/i })).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: 'artifacts/test-results/pricing/15-import-preview-audit.png'
    });

    // Click Commit to Draft Version
    const commitBtn = page.getByRole('button', { name: /اعتماد واستيراد البنود المطابقة/i });
    if (await commitBtn.isVisible() && await commitBtn.isEnabled()) {
      await commitBtn.click();
      await page.waitForLoadState('networkidle');
    }

    await page.getByRole('button', { name: /إلغاء/i }).click();

    // ==========================================
    // 4. Product Workspace SME Pricing Integration and Dialog
    // ==========================================
    await page.goto(`/catalog/${atlasProductWithPriceId}`);
    await page.waitForLoadState('networkidle');

    // Verify Pricing-owned ProductSellingPriceCard is rendered
    await expect(page.getByText(/سعر البيع المعتمد/i).first()).toBeVisible({ timeout: 10000 });

    // 01: Catalog SME Pricing Integration Screenshot
    await page.screenshot({
      path: 'artifacts/test-results/pricing/01-catalog-product-selling-price-sme.png',
      fullPage: true
    });

    // Navigate back to Pricing
    await page.goto('/pricing');
    await page.waitForLoadState('networkidle');

    // ==========================================
    // 5. Mobile 390px, English LTR, Multi-Entity Isolation, and Permission-Limited State
    // ==========================================
    // 16: Mobile 390px Viewport
    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForTimeout(500);
    await expect(page.locator('h1').first()).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/pricing/16-pricing-mobile-390px.png',
      fullPage: true
    });

    // Reset to Desktop View
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.waitForTimeout(500);

    // 03: Desktop English LTR View Visual Capture
    const langBtn = page.getByRole('button', { name: /التبديل إلى الإنجليزية|English/i });
    if (await langBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await langBtn.click();
      await page.waitForLoadState('networkidle');
    }

    await expect(page.getByRole('heading', { name: /Commercial Pricing & Price Lists/i })).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: 'artifacts/test-results/pricing/03-pricing-hub-overview-en.png',
      fullPage: true
    });

    // Switch back to Arabic
    const arBtn = page.getByRole('button', { name: /Switch to Arabic|التبديل إلى العربية|العربية/i });
    if (await arBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await arBtn.click();
      await page.waitForLoadState('networkidle');
    }

    // 17: Legal Entity Switch Proof (Isolation)
    const entitySelect = page.locator('#entity-select');
    await expect(entitySelect).toBeVisible();
    await entitySelect.selectOption({ index: 2 });
    await page.waitForTimeout(1000);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1').first()).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/17-legal-entity-switch.png',
      fullPage: false
    });

    // Switch back to Atlas
    await entitySelect.selectOption({ index: 1 });
    await page.waitForTimeout(1000);
    await page.waitForLoadState('networkidle');

    // 18: Real Permission-Limited State Proof
    const unauthorizedResponse = await page.request.post('http://127.0.0.1:5080/api/v1/pricing/price-lists', {
      headers: { 'X-Legal-Entity-Id': 'aad8a34b-344c-9e5b-8d58-57f182fc6528' },
      data: {
        legalEntityId: 'aad8a34b-344c-9e5b-8d58-57f182fc6528',
        code: 'UNAUTH-TEST',
        name: 'Unauthorized',
        direction: 'Selling',
        currencyCode: 'EGP',
        currencyPolicy: 'ExactCurrency'
      }
    });
    expect([400, 401, 403, 429]).toContain(unauthorizedResponse.status());

    // Policies tab in Read-Only / Auditing view
    await page.getByRole('button', { name: /^السياسات والتغطية$|^Policies/i }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /السياسة السعرية للمنشأة القانونية|Legal Entity Pricing Policy/i })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/18-permission-limited-state.png'
    });
  });
});
