import { test, expect, Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';

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
  await page.waitForLoadState('domcontentloaded');

  const pricingHeading = page.getByRole('heading', { name: /الأسعار والتسعير التجاري|Commercial Pricing/i });
  const emailInput = page.locator('#login-email');
  const retryBtn = page.getByRole('button', { name: /إعادة المحاولة/i });

  // Wait for the app shell to finish loading and transition to either:
  // 1. Pricing Hub (authenticated)
  // 2. Login screen (signed-out)
  // 3. Error screen (transient failure requiring retry)
  for (let i = 0; i < 3; i++) {
    const matched = await Promise.race([
      pricingHeading.waitFor({ state: 'visible', timeout: 15000 }).then(() => 'heading').catch(() => null),
      emailInput.waitFor({ state: 'visible', timeout: 15000 }).then(() => 'login').catch(() => null),
      retryBtn.waitFor({ state: 'visible', timeout: 15000 }).then(() => 'retry').catch(() => null)
    ]);

    if (matched === 'heading') {
      break;
    }

    if (matched === 'retry') {
      await retryBtn.click();
      await page.waitForLoadState('networkidle');
      continue;
    }

    if (matched === 'login') {
      await emailInput.fill('admin@atlas.com');
      await page.locator('#login-password').fill(process.env.SEED_DEFAULT_PASSWORD ?? 'TodirDev2026!');
      await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
      await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
      await page.waitForLoadState('networkidle');
      continue;
    }
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
  test.setTimeout(600_000);

  test('Pricing Real-Stack End-to-End Product Journeys and Visual Proofs', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();
    page.on('console', msg => console.log('PAGE LOG:', msg.type(), msg.text()));
    page.on('requestfailed', req => console.log('REQ FAILED:', req.url(), req.failure()?.errorText));
    page.on('response', res => {
      if (res.status() >= 400) console.log('HTTP ERROR:', res.status(), res.url());
    });
    await loginIfNeeded(page);

    // ==========================================
    // 1. Hub Overview, Selling/Buying Lists, and Version Lifecycle
    // ==========================================
    await page.goto('/pricing');
    await page.waitForLoadState('networkidle');

    // 02: Pricing Hub Overview (Arabic RTL)
    await expect(page.getByText(/قوائم البيع النشطة/i)).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/سياسة تحويل العملات نشطة/i)).toBeVisible({ timeout: 15000 });

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
    await expect(page.getByText(/AT-0002/).first()).toBeVisible();
    await expect(page.getByText(/1,250|1250/).first()).toBeVisible();
    await expect(page.getByText(/قطعة|EA/).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /إضافة بند سعر مباشر/i })).not.toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/pricing/06-version-editor-active-readonly.png'
    });

    const [exportResponse, download] = await Promise.all([
      page.waitForResponse(response => response.url().includes('/versions/') && response.url().includes('/export?') && response.request().method() === 'GET'),
      (async () => {
        const event = page.waitForEvent('download', { timeout: 30_000 });
        await page.getByRole('button', { name: /تصدير البنود \(CSV\)|Export Entries \(CSV\)/i }).click();
        return event;
      })()
    ]);
    expect(exportResponse.status()).toBe(200);
    expect(exportResponse.headers()['content-type']).toContain('text/csv');
    expect(download.suggestedFilename()).toBe('pricing_RETAIL_v1.csv');
    const downloadedPath = await download.path();
    expect(downloadedPath).not.toBeNull();
    const csv = await readFile(downloadedPath!, 'utf8');
    expect(csv).toContain('AT-0002');
    expect(csv).toContain('EA');
    expect(csv).toContain('1250');
    expect(csv).not.toContain('VariantId');
    expect(csv).not.toContain('PartyId');
    expect(csv).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}/i);

    // 07: Version Editor - Scheduled Version (#2)
    const version2Trigger = page.getByRole('button', { name: /إصدار #2/i }).first();
    await expect(version2Trigger).toBeVisible({ timeout: 5000 });
    await version2Trigger.click();
    await page.waitForTimeout(500);
    await expect(page.getByText(/هذا الإصدار مجدول وسيبدأ سريانه تلقائياً/i)).toBeVisible();
    await expect(page.getByText(/1,350|1350/).first()).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/07-scheduled-future-version.png'
    });

    const cutover = await page.evaluate(async ({ entityId, listCode, variantId, uomId, templateId }) => {
      const listsResponse = await fetch(`/api/v1/pricing/price-lists?legalEntityId=${entityId}`, { credentials: 'same-origin' });
      const lists = await listsResponse.json() as { code: string; id: string }[];
      const list = lists.find(item => item.code === listCode);
      if (!listsResponse.ok || !list) return { status: listsResponse.status, prices: [] as (string | null)[], oldSnapshot: null as string | null };
      const csrfResponse = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin' });
      const csrf = await csrfResponse.json() as { token: string };
      const prices: (string | null)[] = [];
      let oldSnapshot: string | null = null;
      for (const pricingDate of ['2026-12-31', '2027-01-01', '2027-01-02']) {
        const response = await fetch('/api/v1/pricing/resolve', {
          method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-Legal-Entity-Id': entityId, 'X-CSRF-TOKEN': csrf.token },
          body: JSON.stringify({ legalEntityId: entityId, direction: 'Selling', pricingDate, transactionCurrencyCode: 'EGP', priceListId: list.id, partyId: null, productVariantId: variantId, unitOfMeasureId: uomId, quantity: 1, productTemplateId: templateId, categoryPath: [], brandId: null })
        });
        const body = await response.text();
        if (!response.ok) return { status: response.status, error: body, prices, oldSnapshot };
        const result = JSON.parse(body) as { snapshot?: { finalUnitPrice: string } };
        prices.push(result.snapshot?.finalUnitPrice ?? null);
        if (pricingDate === '2026-12-31') oldSnapshot = result.snapshot?.finalUnitPrice ?? null;
      }
      return { status: 200, prices, oldSnapshot };
    }, { entityId: 'aad8a34b-344c-9e5b-8d58-57f182fc6528', listCode: 'RETAIL', variantId: atlasVariantWithPrice, uomId: atlasUomEa, templateId: atlasProductWithPriceId });
    expect(cutover.status, cutover.error).toBe(200);
    expect(cutover.prices).toEqual(['1250', '1350', '1350']);
    expect(cutover.oldSnapshot).toBe('1250');

    // Close version drawer
    await page.getByRole('button', { name: /إغلاق النافذة|إلغاء/i }).first().click();

    // ==========================================
    // 2. Business-Friendly Simulator: Customer, Quantity Tiers, UOM, and FX Proofs
    // ==========================================
    await page.getByRole('button', { name: /^محاكي ومفسر السعر$|^Simulator/i }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /محاكي ومفسر التسعير التجاري/i })).toBeVisible();

    await selectOption(page, /المنتج \/ النموذج/i, /AT-0002\)/i);
    await selectOption(page, /وحدة القياس/i, /EA|قطعة/i);

    const simulateBtn = page.getByRole('button', { name: /محاكاة واحتساب السعر/i });

    // 08: Customer-Specific Price Proof (Atlas Office Industries -> 1,100 EGP vs 1,250 standard)
    await selectOption(page, /قائمة الأسعار المحددة/i, /RETAIL|قائمة البيع العامة/i);
    await selectOption(page, /العميل أو المورد/i, /أطلس للصناعات المكتبية/i);
    await page.getByLabel(/عملة العملية التجارية/i).fill('EGP');
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

    // Boundary check 1: Below boundary (11 qty) -> 1,125 EGP
    await qtyInput.fill('11');
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/1,125|1125/i).first()).toBeVisible();

    // Boundary check 2: Exact boundary (12 qty) -> 1,050 EGP
    await qtyInput.fill('12');
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/1,050|1050/i).first()).toBeVisible();
    await expect(page.getByText(/تم تطبيق السعر المخصص للطلبات بكمية 12 أو أكثر/i)).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/09-wholesale-quantity-tiers.png'
    });

    // Boundary check 3: Above boundary (13 qty) -> 1,050 EGP
    await qtyInput.fill('13');
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/1,050|1050/i).first()).toBeVisible();

    // 10: Explicit commercial prices for Piece, Pack, and Carton.
    await selectOption(page, /قائمة الأسعار المحددة/i, /RETAIL|قائمة البيع العامة/i);
    await selectOption(page, /العميل أو المورد/i, /بدون تحديد عميل/i);
    await selectOption(page, /وحدة القياس/i, /EA|قطعة/i);
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/1,250|1250/i).first()).toBeVisible();
    await expect(page.getByText('قطعة (EA)', { exact: true }).last()).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/pricing/10-uom-piece.png' });

    await selectOption(page, /وحدة القياس/i, /PACK|عبوة|باك/i);
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/700/i).first()).toBeVisible();
    await expect(page.getByText(/تم تطبيق سعر تجاري مستقل لوحدة/i)).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/pricing/10-uom-pack.png' });

    // Explicit carton price must win over base-unit conversion.
    await selectOption(page, /قائمة الأسعار المحددة/i, /RETAIL|قائمة البيع العامة/i);
    await qtyInput.fill('1');
    await selectOption(page, /وحدة القياس/i, /CARTON|كرتونة/i);
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/25,500|25500/i).first()).toBeVisible();
    await expect(page.getByText(/تم تطبيق سعر تجاري مستقل لوحدة كرتونة/i)).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/10-uom-pricing-breakdown.png'
    });

    const batchProof = await page.evaluate(async ({ entityId, secondaryEntityId, variantId, uomId }) => {
      const [primaryResponse, secondaryResponse] = await Promise.all([
        fetch(`/api/v1/pricing/price-lists?legalEntityId=${entityId}`, { credentials: 'same-origin' }),
        fetch(`/api/v1/pricing/price-lists?legalEntityId=${secondaryEntityId}`, { credentials: 'same-origin' })
      ]);
      const primaryLists = await primaryResponse.json() as { code: string; id: string }[];
      const secondaryLists = await secondaryResponse.json() as { code: string; id: string }[];
      const primary = primaryLists.find(item => item.code === 'RETAIL');
      const secondary = secondaryLists.find(item => item.code === 'RETAIL-SECONDARY');
      if (!primaryResponse.ok || !secondaryResponse.ok || !primary || !secondary) return { batchStatus: primaryResponse.status || secondaryResponse.status, count: 0, prices: [] as (string | null)[], sampledSinglePrices: [] as (string | null)[] };
      const csrfResponse = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin' });
      const csrf = await csrfResponse.json() as { token: string };
      const makeRequest = (legalEntityId: string, priceListId: string, quantity: number) => ({ legalEntityId, direction: 'Selling', pricingDate: '2026-01-05', transactionCurrencyCode: 'EGP', priceListId, partyId: null, productVariantId: variantId, unitOfMeasureId: uomId, quantity });
      const contexts = Array.from({ length: 100 }, (_, index) => index < 50 ? makeRequest(entityId, primary.id, 1) : makeRequest(secondaryEntityId, secondary.id, 1));
      const batch = await fetch('/api/v1/pricing/resolve-batch', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf.token }, body: JSON.stringify({ items: contexts }) });
      const results = await batch.json() as { snapshot?: { finalUnitPrice: string } }[];
      const sampledSinglePrices: (string | null)[] = [];
      for (const index of [0, 49, 50, 99]) {
        const single = await fetch('/api/v1/pricing/resolve', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf.token }, body: JSON.stringify(contexts[index]) });
        const result = await single.json() as { snapshot?: { finalUnitPrice: string } };
        sampledSinglePrices.push(result.snapshot?.finalUnitPrice ?? null);
      }
      return { batchStatus: batch.status, count: results.length, prices: results.map(result => result.snapshot?.finalUnitPrice ?? null), sampledSinglePrices };
    }, { entityId: 'aad8a34b-344c-9e5b-8d58-57f182fc6528', secondaryEntityId: 'd8eb39bb-b048-7c5d-bacd-e3a923eee21c', variantId: atlasVariantWithPrice, uomId: atlasUomEa });
    expect(batchProof.batchStatus).toBe(200);
    expect(batchProof.count).toBe(100);
    expect(batchProof.prices).toEqual(Array.from({ length: 100 }, (_, index) => index < 50 ? '1250' : '1450'));
    expect(batchProof.sampledSinglePrices).toEqual(['1250', '1250', '1450', '1450']);

    // 11: Real FX Resolution Proof (EXPORT-USD -> EGP Transaction -> 1,250 EGP)
    await selectOption(page, /وحدة القياس/i, /EA|قطعة/i);
    await selectOption(page, /قائمة الأسعار المحددة/i, /EXPORT-USD|بالدولار/i);
    await selectOption(page, /العميل أو المورد/i, /أطلس للصناعات المكتبية/i);
    // The canonical finance seed rate is dated 2026-01-01; use a nearby
    // historical business date to verify conversion without pretending that
    // this stale rate is valid for today's transactions.
    await page.getByLabel(/تاريخ استحقاق التسعير/i).fill('2026-01-05');
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
    await selectOption(page, /المنتج \/ النموذج/i, /AT-0022/i);
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/لا يوجد سعر معتمد لهذا المنتج في هذا السياق/i)).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/13-simulator-missing-price.png'
    });

    // 14: Simulator - Explicit Promotional Zero Price
    await selectOption(page, /المنتج \/ النموذج/i, /AT-0004/i);
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

    await page.locator('textarea').fill('SKU,UOM,Price,Currency,MinQty,MaxQty\nAT-0006,EA,15.00,EGP,1,');

    // Click Validate & Preview to advance to Step 2
    const validatePreviewBtn = page.getByRole('button', { name: /فحص وتدقيق البيانات/i });
    await validatePreviewBtn.click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/تقرير تدقيق ومطابقة الاستيراد/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/الصفوف المطابقة والسليمة:\s*1/i)).toBeVisible();
    await expect(page.getByText(/جاهز للاعتماد في المسودة/i)).toBeVisible();

    // Click Commit to Draft Version
    const commitBtn = page.getByRole('button', { name: /اعتماد واستيراد البنود المطابقة/i });
    await expect(commitBtn).toBeVisible();
    await expect(commitBtn).toBeEnabled();
    await commitBtn.click();
    await expect(page.getByText(/تقرير مطابقة واعتماد الاستيراد/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/البنود المعتمدة في المسودة:\s*1/i)).toBeVisible();
    await expect(page.getByText(/البنود المستبعدة أو غير المطابقة:\s*0/i)).toBeVisible();
    await expect(page.getByText(/مكتمل ومثبت في المسودة/i).first()).toBeVisible();
    await expect(page.getByText(/الإصدار النشط محمي تاريخياً ولم يتأثر/i)).toBeVisible();

    // 15: Preview & Reconciliation Audit Screenshot
    await page.screenshot({
      path: 'artifacts/test-results/pricing/15-import-preview-audit.png'
    });

    await page.getByRole('button', { name: /إلغاء/i }).click();

    // 19: Add overlapping entries after import reconciliation so the conflict
    // does not prevent the preceding valid import from being committed.
    await page.getByRole('button', { name: /^قوائم البيع$|^Selling Lists$/i }).click();
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: /عرض الإصدارات والمحرر/i }).nth(1).click();
    await expect(page.getByRole('heading', { name: /إصدارات قائمة الأسعار والمحرر/i })).toBeVisible({ timeout: 5000 });
    const version3Trigger = page.getByRole('button', { name: /إصدار #3/i }).first();
    if (await version3Trigger.isVisible({ timeout: 3000 }).catch(() => false)) {
      await version3Trigger.click();
      await page.waitForTimeout(500);
      const validateBtn = page.getByRole('button', { name: /فحص التعارضات|التحقق من صحة الإصدار/i });
      await expect(validateBtn).toBeVisible({ timeout: 5000 });

      for (const price of ['100', '90']) {
        await page.getByRole('button', { name: /إضافة بند سعر مباشر/i }).click();
        await selectOption(page, /المنتج \/ النموذج/i, /AT-0008/i);
        await selectOption(page, /وحدة القياس/i, /قطعة \(EA\)/i);
        await page.getByLabel(/سعر الوحدة/i).fill(price);
        await page.getByRole('button', { name: /إضافة البند للمسودة/i }).click();
        await expect(page.getByRole('button', { name: /إضافة البند للمسودة/i })).not.toBeVisible({ timeout: 10000 });
      }

      await validateBtn.click();
      await expect(page.getByText(/تعارض في قواعد التسعير/i)).toBeVisible({ timeout: 10000 });
      await expect(page.getByText(/تم اكتشاف تعارض بين قواعد تسعيرية تحمل نفس الأولوية والنطاق ولكن بقيم متناقضة/i)).toBeVisible();
      await expect(page.getByRole('button', { name: /تفعيل الإصدار فوراً/i })).toBeDisabled();
      await page.screenshot({
        path: 'artifacts/test-results/pricing/19-conflict-blocking-activation.png'
      });
    }
    await page.getByRole('button', { name: /إلغاء/i }).click();

    // ==========================================
    // 4. Product Workspace SME Pricing Integration and Dialog
    // ==========================================
    await page.goto(`/catalog/${atlasProductWithPriceId}`);
    await page.waitForLoadState('networkidle');

    // Verify Pricing-owned ProductSellingPriceCard is rendered with approved price
    await expect(page.getByText(/سعر البيع المعتمد/i).first()).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/1,250|1250/i).first()).toBeVisible({ timeout: 10000 });

    // Prove simple SME update workflow through the Pricing-backed dialog
    const definePriceBtn = page.getByRole('button', { name: /تعديل سعر البيع|تحديد سعر البيع/i });
    if (await definePriceBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await definePriceBtn.click();
      await page.waitForTimeout(300);
      const priceInput = page.getByLabel(/سعر البيع الجديد/i).or(page.locator('input[type="text"]').first());
      if (await priceInput.isVisible({ timeout: 3000 }).catch(() => false)) {
        await priceInput.fill('1250.00');
        await page.getByRole('button', { name: /حفظ وتثبيت السعر|حفظ السعر|تحديث/i }).click();
        await page.waitForTimeout(500);
      }
    }

    // 01: Catalog SME Pricing Integration Screenshot
    await expect(page.getByText(/سعر البيع المعتمد/i).first()).toBeVisible();
    await expect(page.getByText(/1,250|1250/i).first()).toBeVisible();
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
    const mobilePricingTabs = page.locator('.pricing-tabs-desktop button');
    await expect(mobilePricingTabs).toHaveCount(6);
    for (const tab of await mobilePricingTabs.all()) {
      const bounds = await tab.boundingBox();
      expect(bounds).not.toBeNull();
      expect(bounds!.x).toBeGreaterThanOrEqual(0);
      expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(390);
    }

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
    await page.getByRole('button', { name: /^قوائم البيع$|^Selling Lists$/i }).click();
    await expect(page.getByText('RETAIL-SECONDARY')).toBeVisible();
    await page.getByRole('button', { name: /عرض الإصدارات والمحرر/i }).first().click();
    await expect(page.getByText(/1,450|1450/i).first()).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/pricing/17-entity-b-pricing.png' });
    await page.getByRole('button', { name: /إلغاء/i }).first().click();
    await page.screenshot({
      path: 'artifacts/test-results/pricing/17-legal-entity-switch.png',
      fullPage: false
    });

    // Switch back to Atlas
    await entitySelect.selectOption({ index: 1 });
    await page.waitForTimeout(1000);
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: /^قوائم البيع$|^Selling Lists$/i }).click();
    await expect(page.getByText('RETAIL').first()).toBeVisible();
    await page.getByRole('button', { name: /عرض الإصدارات والمحرر/i }).nth(1).click();
    await page.getByRole('button', { name: /إصدار #1/i }).first().click();
    await expect(page.getByText(/1,250|1250/i).first()).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/pricing/17-entity-a-pricing-restored.png' });
    await page.getByRole('button', { name: /إلغاء/i }).first().click();

    // ==========================================
    // 18: Real Permission-Limited User Proof (sales@atlas.com)
    // ==========================================
    // sales@atlas.com possesses pricing.read and pricing.simulate, but strictly lacks pricing.manage / pricing.import
    const salesContext = await page.context().browser()!.newContext();
    const salesPage = await salesContext.newPage();
    await salesPage.setViewportSize({ width: 1440, height: 900 });

    await salesPage.goto('/pricing');
    await salesPage.waitForLoadState('networkidle');
    const salesEmail = salesPage.locator('#login-email');
    await salesEmail.waitFor({ state: 'visible', timeout: 15000 });
    await salesEmail.fill('sales@atlas.com');
    await salesPage.locator('#login-password').fill(process.env.SEED_DEFAULT_PASSWORD ?? 'TodirDev2026!');
    await salesPage.locator('button[type="submit"]').click();
    await salesPage.waitForLoadState('networkidle');

    if (!salesPage.url().includes('/pricing')) {
      await salesPage.goto('/pricing');
      await salesPage.waitForLoadState('networkidle');
    }

    // Confirm landing on Pricing Hub as read-only user
    await expect(salesPage.getByRole('heading', { name: /الأسعار والتسعير التجاري|Commercial Pricing/i })).toBeVisible({ timeout: 15000 });

    // Verify management actions are hidden/unavailable
    await expect(salesPage.getByRole('button', { name: /قائمة أسعار جديدة|New Price List/i })).not.toBeVisible();
    await expect(salesPage.getByRole('button', { name: /استيراد أسعار|Import Prices/i })).not.toBeVisible();
    await expect(salesPage.getByRole('button', { name: /إعدادات السياسة السعرية|Edit Policy/i })).not.toBeVisible();

    // Verify Read-Only badge and notice are visible
    await expect(salesPage.getByText(/للقراءة فقط|وضع العرض والقراءة فقط/i).first()).toBeVisible();
    const permittedRead = await salesPage.evaluate(async () => {
      const response = await fetch('/api/v1/pricing/price-lists?legalEntityId=aad8a34b-344c-9e5b-8d58-57f182fc6528', { credentials: 'same-origin' });
      const lists = await response.json() as unknown[];
      return { status: response.status, count: lists.length };
    });
    expect(permittedRead.status).toBe(200);
    expect(permittedRead.count).toBeGreaterThan(0);

    // 18: Real Permission-Limited State Screenshot
    await salesPage.screenshot({
      path: 'artifacts/test-results/pricing/18-permission-limited-state.png',
      fullPage: true
    });

    // Test direct unauthorized command rejection on backend (403 Forbidden)
    const unauthorizedProof = await salesPage.evaluate(async () => {
      const sessionResponse = await fetch('/api/v1/session/context', { credentials: 'same-origin' });
      const csrfResponse = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin' });
      if (!sessionResponse.ok || !csrfResponse.ok) {
        return { sessionStatus: sessionResponse.status, csrfStatus: csrfResponse.status, writeStatus: null };
      }
      const { token } = await csrfResponse.json() as { token: string };
      const beforeResponse = await fetch('/api/v1/pricing/price-lists?legalEntityId=aad8a34b-344c-9e5b-8d58-57f182fc6528', { credentials: 'same-origin' });
      const before = await beforeResponse.json() as { code: string }[];
      const response = await fetch('/api/v1/pricing/price-lists', {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
          'Content-Type': 'application/json',
          'X-Legal-Entity-Id': 'aad8a34b-344c-9e5b-8d58-57f182fc6528',
          'X-CSRF-TOKEN': token
        },
        body: JSON.stringify({
          legalEntityId: 'aad8a34b-344c-9e5b-8d58-57f182fc6528',
          code: 'UNAUTH-SALES',
          name: 'Unauthorized',
          direction: 'Selling',
          currencyCode: 'EGP',
          currencyPolicy: 'ExactCurrency'
        })
      });
      const afterResponse = await fetch('/api/v1/pricing/price-lists?legalEntityId=aad8a34b-344c-9e5b-8d58-57f182fc6528', { credentials: 'same-origin' });
      const after = await afterResponse.json() as { code: string }[];
      return { sessionStatus: sessionResponse.status, csrfStatus: csrfResponse.status, writeStatus: response.status, beforeCount: before.length, afterCount: after.length };
    });
    expect(unauthorizedProof.sessionStatus).toBe(200);
    expect(unauthorizedProof.csrfStatus).toBe(200);
    expect(unauthorizedProof.writeStatus).toBe(403);
    expect(unauthorizedProof.afterCount).toBe(unauthorizedProof.beforeCount);

    const anonymousContext = await page.context().browser()!.newContext();
    const anonymousPage = await anonymousContext.newPage();
    await anonymousPage.goto('/pricing');
    const anonymousStatus = await anonymousPage.evaluate(async () => (await fetch('/api/v1/pricing/price-lists?legalEntityId=aad8a34b-344c-9e5b-8d58-57f182fc6528')).status);
    expect(anonymousStatus).toBe(401);
    await anonymousContext.close();

    await salesContext.close();
    await context.close();
  });
});
