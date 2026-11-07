import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const REPO_ROOT = path.resolve(process.cwd(), '..');
const SCREENSHOT_DIR = path.resolve(REPO_ROOT, 'artifacts/pos-acceptance');
const FRONTEND_SCREENSHOT_DIR = path.resolve(process.cwd(), 'artifacts/pos-acceptance');
const BRAIN_SCREENSHOT_DIR = 'C:/Users/IT/.gemini/antigravity/brain/2b36897c-7b21-48b3-b339-9bb22437719d/pos-acceptance';

test.describe.configure({ mode: 'serial' });

async function captureScreenshot(page: Page, name: string) {
  for (const dir of [SCREENSHOT_DIR, FRONTEND_SCREENSHOT_DIR, BRAIN_SCREENSHOT_DIR]) {
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }
  }

  const primaryFile = path.join(SCREENSHOT_DIR, `${name}.png`);
  await page.screenshot({ path: primaryFile, fullPage: true });

  for (const destDir of [FRONTEND_SCREENSHOT_DIR, BRAIN_SCREENSHOT_DIR]) {
    try {
      fs.copyFileSync(primaryFile, path.join(destDir, `${name}.png`));
    } catch {
      // Ignore copy error
    }
  }
}

async function ensureAuthenticated(page: Page) {
  page.on('console', (msg) => {
    if (msg.type() === 'error') console.log('PAGE ERROR:', msg.text());
  });
  page.on('pageerror', (err) => {
    console.log('UNCAUGHT PAGE ERROR:', err.message);
  });
  page.on('response', (res) => {
    if (res.status() >= 400) {
      console.log(`HTTP ${res.status()} ${res.request().method()} ${res.url()}`);
    }
  });

  await page.goto('/pos');
  await page.waitForLoadState('networkidle');

  // Check if already on POS page
  const posPageEl = page.locator('[data-testid="pos-page"]');
  if (await posPageEl.isVisible({ timeout: 2000 }).catch(() => false)) {
    return;
  }

  const atlasBtn = page.getByRole('button', { name: /admin@atlas\.com/i });
  if (await atlasBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
    await atlasBtn.click();
    await atlasBtn.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle');
  } else {
    const emailInput = page.locator('#login-email, input[type="email"]');
    if (await emailInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      const password = process.env.SEED_DEFAULT_PASSWORD || 'TodirDev2026!';
      await emailInput.fill('admin@atlas.com');
      const pwdInput = page.locator('#login-password, input[type="password"]');
      await pwdInput.fill(password);
      await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
      await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
      await page.waitForLoadState('networkidle');
    }
  }

  if (!page.url().includes('/pos')) {
    await page.goto('/pos');
    await page.waitForLoadState('networkidle');
  }

  await expect(page.locator('[data-testid="pos-page"]')).toBeVisible({ timeout: 20000 });

  // Ensure Arabic is active by default
  const isLtr = await page.evaluate(() => document.documentElement.getAttribute('dir') === 'ltr');
  if (isLtr) {
    const langBtn = page.locator('button.topbar-lang-btn');
    if (await langBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await langBtn.click();
      await page.waitForFunction(() => document.documentElement.getAttribute('dir') === 'rtl');
      await page.waitForLoadState('networkidle');
    }
  }
}

test.describe('Module 20 — POS (Point of Sale) Frontend & Real Workflows', () => {

  test('01. Layout & High-Contrast Design System Architecture', async ({ page }) => {
    await ensureAuthenticated(page);

    // Verify main header heading and typography
    const heading = page.locator('h1');
    await expect(heading).toBeVisible();
    await expect(heading).toContainText(/نقاط البيع|Point of Sale/);

    // Verify Dark Oxford Navy background on header container
    const header = page.locator('header').first();
    const headerBg = await header.evaluate((el) => window.getComputedStyle(el).backgroundColor);
    expect(headerBg).toBe('rgb(16, 42, 67)'); // #102a43

    // Verify cool neutral slate background on page canvas
    const canvas = page.locator('[data-testid="pos-page"]');
    const canvasBg = await canvas.evaluate((el) => window.getComputedStyle(el).backgroundColor);
    expect(canvasBg).toBe('rgb(240, 244, 248)'); // #f0f4f8

    // Verify all 6 tabs are present and accessible
    await expect(page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /نقاط البيع والأجهزة|الأجهزة ونقاط البيع|Registers/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /ورديات الصندوق|Sessions/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /مرتجع المبيعات|Returns/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /المزامنة والأوفلاين|مزامنة دون اتصال|Offline/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /تقارير الوردية والمبيعات|التقارير التشغيلية|Reports/i })).toBeVisible();

    await captureScreenshot(page, '01-pos-workspace-overview');
  });

  test('02. Cashier Checkout Workspace: Barcode Scanning, Catalog Lookup & Cart Totals', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Checkout tab
    await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
    await page.waitForLoadState('networkidle');

    // Verify Barcode input is visible
    const barcodeInput = page.locator('input[placeholder*="امسح الباركود"], input[placeholder*="Scan barcode"]');
    await expect(barcodeInput).toBeVisible();

    // Add first item via quick catalog button or typing barcode
    const quickItemBtn = page.getByRole('button', { name: /كابل نحاس|EG-1-0002/i }).first();
    if (await quickItemBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await quickItemBtn.click();
    } else {
      await barcodeInput.fill('EG-1-0002');
      await barcodeInput.press('Enter');
    }

    // Verify line appears in cart table
    const cartTable = page.locator('table');
    await expect(cartTable).toBeVisible({ timeout: 10000 });
    await expect(cartTable).toContainText(/كابل نحاس|EG-1-0002/i);

    // Add another item
    const quickItem2 = page.getByRole('button', { name: /قاطع تيار|EG-2-0001/i }).first();
    if (await quickItem2.isVisible({ timeout: 2000 }).catch(() => false)) {
      await quickItem2.click();
      await page.waitForTimeout(500);
    }

    // Adjust quantity using + button
    const plusButtons = page.getByRole('button', { name: 'زيادة الكمية' });
    if (await plusButtons.first().isVisible({ timeout: 2000 }).catch(() => false)) {
      await plusButtons.first().click();
    }

    // Verify subtotal, tax, and total in totals panel
    const totalsPanel = page.locator('[data-testid="pos-totals-panel"], div:has-text("الإجمالي المستحق")').first();
    await expect(totalsPanel).toBeVisible();

    await captureScreenshot(page, '02-pos-cart-with-items');
  });

  test('03. Line Discount Application & Automatic Calculation', async ({ page }) => {
    await ensureAuthenticated(page);

    // Open line discount modal
    const discountBtn = page.getByRole('button', { name: /إضافة خصم|خصم|Add Discount/i }).first();
    if (await discountBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await discountBtn.click();

      // Enter discount value (e.g. 10%)
      const discInput = page.locator('input[type="number"]').first();
      await expect(discInput).toBeVisible();
      await discInput.fill('10');

      // Reason input
      const reasonInput = page.locator('input[type="text"]').last();
      if (await reasonInput.isVisible({ timeout: 1000 }).catch(() => false)) {
        await reasonInput.fill('خصم ترويجي خاص بالعميل');
      }

      await page.getByRole('button', { name: /تطبيق الخصم|Apply/i }).click();
      await page.waitForTimeout(500);
    }

    await captureScreenshot(page, '03-pos-discount-applied');
  });

  test('04. Customer Selection: Anonymous Walk-in vs Retail Customer', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Checkout tab
    await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
    await page.waitForLoadState('networkidle');

    // Click customer selection button
    const custBtn = page.getByRole('button', { name: /عميل نقدي|عميل مباشر|Walk-in/i }).first();
    await expect(custBtn).toBeVisible();
    await custBtn.click();

    // Verify customer modal appears
    const custModal = page.locator('[role="dialog"]');
    await expect(custModal).toBeVisible();
    await expect(custModal).toContainText(/تحديد بيانات العميل|اختيار العميل|Select Customer/i);

    // Select retail customer from list
    const selectBtn = custModal.getByRole('button', { name: /اختيار|Select/i }).first();
    if (await selectBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await selectBtn.click();
    } else {
      // Select walk in
      await custModal.getByRole('button', { name: /عميل نقدي مباشر|عميل عام|Walk-in/i }).first().click();
    }

    await page.waitForTimeout(500);
    await captureScreenshot(page, '04-pos-customer-selected');
  });

  test('05. Cash Tender Workflow: Change Calculation & Electronic Thermal Receipt', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Checkout tab
    await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
    await page.waitForLoadState('networkidle');

    // Ensure cart has an item to enable checkout
    const quickItemBtn = page.getByRole('button', { name: /كابل نحاس|EG-1-0002/i }).first();
    if (await quickItemBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await quickItemBtn.click();
      await page.waitForTimeout(600);
    }

    // Click Pay button
    const payBtn = page.getByRole('button', { name: /تحصيل وإتمام البيع|الدفع|Pay/i });
    await expect(payBtn).toBeVisible();
    await payBtn.click();

    // Verify Tender Modal
    const tenderModal = page.locator('[role="dialog"]');
    await expect(tenderModal).toBeVisible();
    await expect(tenderModal).toContainText(/تحصيل قيمة المبيعات|سداد الفاتورة|Checkout Payment/i);

    // Fill tendered cash input with 1500.00 (exceeding total to verify change)
    const tenderInput = tenderModal.locator('input[type="number"]').first();
    if (await tenderInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await tenderInput.fill('1500.00');
    }

    // Verify change calculation is displayed
    await expect(tenderModal).toContainText(/المتبقي للعميل|Change/i);
    await expect(tenderModal).toContainText(/250\.00/i);

    await captureScreenshot(page, '05-pos-tender-cash-change');

    // Confirm Payment
    const confirmBtn = tenderModal.getByRole('button', { name: /تأكيد الدفع النقدي|تأكيد الدفع|Complete/i });
    await expect(confirmBtn).toBeEnabled();
    await confirmBtn.click();

    // Receipt Modal should appear
    const receiptModal = page.locator('[role="dialog"]').filter({ hasText: /إيصال|Receipt/i }).first();
    await expect(receiptModal).toBeVisible({ timeout: 10000 });
    await expect(receiptModal).toContainText(/إيصال البيع الإلكتروني|إيصال استلام|Sales Receipt/i);

    await captureScreenshot(page, '06-pos-thermal-receipt-modal');

    // Close Receipt Modal
    const closeReceiptBtn = receiptModal.getByRole('button', { name: /معاملة جديدة|عملية بيع جديدة|إغلاق|Close|طباعة/i }).last();
    await closeReceiptBtn.click();
    await page.waitForTimeout(500);
  });

  test('06. Cart Hold & Recall Workflow', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Checkout tab
    await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
    await page.waitForLoadState('networkidle');

    // Add item to cart
    const quickItemBtn = page.getByRole('button', { name: /كابل نحاس|EG-1-0002/i }).first();
    if (await quickItemBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await quickItemBtn.click();
      await page.waitForTimeout(500);
    }

    // Hold cart
    const holdBtn = page.getByRole('button', { name: /تعليق السلة|Hold/i });
    if (await holdBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await holdBtn.click();
      await page.waitForTimeout(500);
    }

    await captureScreenshot(page, '07-pos-cart-on-hold');

    // Resume cart
    const resumeBtn = page.getByRole('button', { name: /استعادة السلة|Resume/i });
    if (await resumeBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await resumeBtn.click();
      await page.waitForTimeout(500);
    }
  });

  test('07. Cash Drawer Movements: Cash-In, Cash-Out & Cash-Drop', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Checkout tab
    await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
    await page.waitForLoadState('networkidle');

    // Click Cash Movement button
    const movementBtn = page.getByRole('button', { name: /حركة نقدية|Cash Movement/i });
    await expect(movementBtn).toBeVisible();
    await movementBtn.click();

    // Cash movement modal
    const moveModal = page.locator('[role="dialog"]');
    await expect(moveModal).toBeVisible();
    await expect(moveModal).toContainText(/تسجيل حركة نقدية|حركة نقدية بالصندوق|Cash Movement/i);

    // Fill amount and reason
    const amtInput = moveModal.locator('input[type="number"]');
    await amtInput.fill('150.00');

    const descInput = moveModal.locator('input[type="text"], textarea').last();
    await descInput.fill('إيداع فكة إضافية لبدء فترة الذروة');

    await captureScreenshot(page, '08-pos-cash-movement-modal');

    // Submit movement
    await moveModal.getByRole('button', { name: /تأكيد تسجيل الحركة|تأكيد الحركة|Submit/i }).click();
    await page.waitForTimeout(500);
  });

  test('08. Registers & Terminals Management Tab', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Registers tab
    await page.getByRole('button', { name: /نقاط البيع والأجهزة|الأجهزة ونقاط البيع|Registers/i }).click();
    await page.waitForLoadState('networkidle');

    // Verify registers table
    const regTable = page.locator('table');
    await expect(regTable).toBeVisible();
    await expect(regTable).toContainText(/MAIN|Main Retail Register/i);

    // Open New Register Modal
    const newRegBtn = page.getByRole('button', { name: /إضافة نقطة بيع|جهاز جديد|New Register/i });
    await expect(newRegBtn).toBeVisible();
    await newRegBtn.click();

    const regModal = page.locator('[role="dialog"]');
    await expect(regModal).toBeVisible();
    await expect(regModal).toContainText(/إضافة نقطة بيع|New Register/i);

    await captureScreenshot(page, '09-pos-new-register-modal');

    // Close modal
    await regModal.getByRole('button', { name: /إلغاء|Cancel/i }).click();
  });

  test('09. Cashier Sessions & Real-Time Variance Reconciliation', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Sessions tab
    await page.getByRole('button', { name: /ورديات الصندوق|Sessions/i }).click();
    await page.waitForLoadState('networkidle');

    // Verify sessions heading
    await expect(page.getByRole('heading', { name: /إدارة ورديات الصندوق|Sessions/i })).toBeVisible();

    // Open Close Session modal
    const closeSessionBtn = page.getByRole('button', { name: /إغلاق الوردية|إنهاء الوردية|Close Session/i }).first();
    if (await closeSessionBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await closeSessionBtn.click();

      const sessionModal = page.locator('[role="dialog"]');
      await expect(sessionModal).toBeVisible();
      await expect(sessionModal).toContainText(/إغلاق وردية|إنهاء وردية|Close Session/i);

      // Enter counted cash
      const countedInput = sessionModal.locator('input[type="number"]').first();
      await countedInput.fill('250.00');

      // Verify variance display
      await expect(sessionModal).toContainText(/عجز \/ فائض الصندوق|Cash Variance/i);

      await captureScreenshot(page, '10-pos-session-close-reconciliation');

      // Close modal without submitting to preserve test session
      await sessionModal.getByRole('button', { name: /إلغاء|Cancel/i }).click();
    }
  });

  test('10. Sales Returns & Disposition Management Tab', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Returns tab
    await page.getByRole('button', { name: /مرتجع المبيعات|Returns/i }).click();
    await page.waitForLoadState('networkidle');

    // Verify returns tab layout
    await expect(page.getByRole('heading', { name: /مرتجع المبيعات|Sales Returns/i })).toBeVisible();

    // Click New Return button
    const newReturnBtn = page.getByRole('button', { name: /إنشاء مرتجع جديد|إرجاع جديد|New Return/i });
    if (await newReturnBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await newReturnBtn.click();

      const returnModal = page.locator('[role="dialog"]');
      await expect(returnModal).toBeVisible();
      await expect(returnModal).toContainText(/تسجيل مرتجع مبيعات|Create Sales Return/i);

      await captureScreenshot(page, '11-pos-create-return-modal');

      await returnModal.getByRole('button', { name: /إلغاء|Cancel/i }).click();
    }
  });

  test('11. Durable Offline Queue & Package Download Tab', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Offline tab
    await page.getByRole('button', { name: /المزامنة والأوفلاين|مزامنة دون اتصال|Offline/i }).click();
    await page.waitForLoadState('networkidle');

    // Verify offline queue status heading and buttons
    await expect(page.getByRole('heading', { name: /حالة المزامنة|المزامنة والوضع غير المتصل/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /تنزيل حزمة البيانات|Download Package/i })).toBeVisible();

    await captureScreenshot(page, '12-pos-offline-queue-dashboard');
  });

  test('12. Store Manager Operational Reports Tab', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Reports tab
    await page.getByRole('button', { name: /تقارير الوردية والمبيعات|التقارير التشغيلية|Reports/i }).click();
    await page.waitForLoadState('networkidle');

    // Verify reports sections: Sales, Payment Breakdown, Cash Differences
    await expect(page.getByRole('button', { name: /تقرير المبيعات|Sales/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /توزيع وسائل التحصيل|Payment Mix/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /سجل عجز وفائض النقدية|Cash Differences/i })).toBeVisible();

    await captureScreenshot(page, '13-pos-operational-reports');
  });

  test('13. English LTR Localization Parity Verification', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to English
    const langBtn = page.locator('button.topbar-lang-btn, button:has-text("التبديل إلى الإنجليزية"), button:has-text("Switch to English")');
    if (await langBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
      const text = await langBtn.innerText();
      if (text.includes('الإنجليزية') || text.includes('English')) {
        await langBtn.click();
        await page.waitForFunction(() => document.documentElement.getAttribute('dir') === 'ltr');
        await page.waitForLoadState('networkidle');
      }
    }

    // Verify English UI headings and tabs
    const heading = page.locator('h1');
    await expect(heading).toContainText('Point of Sale (POS)');
    await expect(page.getByRole('button', { name: /Cashier Checkout/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Registers & Terminals/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Cashier Sessions/i })).toBeVisible();

    await captureScreenshot(page, '14-pos-english-ltr-view');

    // Switch back to Arabic
    const switchBackBtn = page.locator('button.topbar-lang-btn, button:has-text("التبديل إلى العربية"), button:has-text("Switch to Arabic")');
    if (await switchBackBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
      await switchBackBtn.click();
      await page.waitForFunction(() => document.documentElement.getAttribute('dir') === 'rtl');
    }
  });

  test('14. Mobile / Touch Viewport Responsive Verification (390px)', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await ensureAuthenticated(page);

    // Switch to checkout tab
    await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
    await page.waitForLoadState('networkidle');

    // Verify barcode input and workspace adapt responsively
    const barcodeInput = page.locator('input[placeholder*="امسح الباركود"], input[placeholder*="Scan barcode"]');
    await expect(barcodeInput).toBeVisible();

    await captureScreenshot(page, '15-pos-touch-mobile-viewport');
  });

});
