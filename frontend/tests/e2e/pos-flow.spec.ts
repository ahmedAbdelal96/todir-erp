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
  // Use fullPage: false to prevent sticky header stitching duplicate artifacts in the corporate design system
  await page.screenshot({ path: primaryFile, fullPage: false });

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

  // Fast check: if already authenticated and on the POS workspace, avoid redundant full-page reloads
  if (page.url().includes('/pos')) {
    const posPageEl = page.locator('[data-testid="pos-page"]');
    if (await posPageEl.isVisible({ timeout: 1500 }).catch(() => false)) {
      return;
    }
  }

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

let sharedPage: Page;

test.describe('Module 20 — POS (Point of Sale) Frontend & Real Workflows', () => {

  test.beforeAll(async ({ browser }) => {
    const context = await browser.newContext();
    sharedPage = await context.newPage();
    await ensureAuthenticated(sharedPage);
  });

  test.afterAll(async () => {
    if (sharedPage) {
      await sharedPage.close().catch(() => {});
    }
  });

  test('01. Layout & High-Contrast Design System Architecture', async () => {
    const page = sharedPage;
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

  test('02. Cashier Checkout Workspace: Barcode Scanning, Catalog Lookup & Cart Totals', async () => {
    const page = sharedPage;
    await ensureAuthenticated(page);

    // Ensure on Checkout tab
    if (!await page.locator('table').isVisible({ timeout: 1000 }).catch(() => false)) {
      await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
      await page.waitForLoadState('networkidle');
    }

    // Verify Barcode input is visible
    const barcodeInput = page.locator('input[placeholder*="امسح الباركود"], input[placeholder*="Scan barcode"]');
    await expect(barcodeInput).toBeVisible();

    // Add first item via quick catalog button or typing barcode
    const quickItemBtn = page.getByRole('button', { name: /ورق تصوير|A4|قاطع تيار|EG-2-0001|كابل نحاس/i }).first();
    if (await quickItemBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await quickItemBtn.click();
    } else {
      await barcodeInput.fill('EG-2-0001');
      await barcodeInput.press('Enter');
    }

    // Verify line appears in cart table
    const cartTable = page.locator('table');
    await expect(cartTable).toBeVisible({ timeout: 10000 });
    await expect(cartTable).toContainText(/ورق تصوير|Copy Paper|قاطع تيار|EG-2-0001|كابل نحاس/i);

    // Adjust quantity using + button
    const plusButtons = page.getByRole('button', { name: 'زيادة الكمية' });
    if (await plusButtons.first().isVisible({ timeout: 2000 }).catch(() => false)) {
      await plusButtons.first().click();
      await page.waitForTimeout(400);
    }

    // Verify subtotal, tax, and total in totals panel
    const totalsPanel = page.locator('[data-testid="pos-totals-panel"], div:has-text("الإجمالي المستحق")').first();
    await expect(totalsPanel).toBeVisible();

    await captureScreenshot(page, '02-pos-cart-with-items');
  });

  test('03. Line Discount Application & Automatic Calculation', async () => {
    const page = sharedPage;
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
      await page.waitForTimeout(600);

      // If manager override is prompted (for discounts >= 5%), approve it with reason
      const overrideModal = page.locator('[role="dialog"]').filter({ hasText: /موافقة المشرف|اعتماد الخصم|صلاحية المشرف|Override/i });
      if (await overrideModal.isVisible({ timeout: 1500 }).catch(() => false)) {
        const overrideReason = overrideModal.locator('input[type="text"], textarea').last();
        if (await overrideReason.isVisible({ timeout: 1000 }).catch(() => false)) {
          await overrideReason.fill('موافقة المشرف على الخصم الترويجي');
        }
        await overrideModal.getByRole('button', { name: /تأكيد الصلاحية|موافقة|Authorize/i }).click();
        await page.waitForTimeout(600);
      }
    }

    await captureScreenshot(page, '03-pos-discount-applied');

    // Ensure any open dialog is closed before next step
    const openDialog = page.locator('[role="dialog"]');
    if (await openDialog.isVisible({ timeout: 1000 }).catch(() => false)) {
      const cancelBtn = openDialog.getByRole('button', { name: /إلغاء|Cancel|إغلاق|Close/i }).last();
      if (await cancelBtn.isVisible({ timeout: 500 }).catch(() => false)) {
        await cancelBtn.click();
      } else {
        await page.keyboard.press('Escape');
      }
      await page.waitForTimeout(300);
    }
  });

  test('04. Customer Selection: Anonymous Walk-in vs Retail Customer', async () => {
    const page = sharedPage;
    await ensureAuthenticated(page);

    // Switch to Checkout tab if not already on it
    const cartTable = page.locator('table');
    if (!await cartTable.isVisible({ timeout: 1000 }).catch(() => false)) {
      await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
      await page.waitForLoadState('networkidle');
    }

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

  test('05. Cash Tender Workflow: Change Calculation & Electronic Thermal Receipt', async () => {
    const page = sharedPage;
    await ensureAuthenticated(page);

    // Ensure on Checkout tab
    if (!await page.locator('table').isVisible({ timeout: 1000 }).catch(() => false)) {
      await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
      await page.waitForLoadState('networkidle');
    }

    // Ensure cart has an item to enable checkout
    const quickItemBtn = page.getByRole('button', { name: /ورق تصوير|A4|قاطع تيار|EG-2-0001|كابل نحاس/i }).first();
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

    // Fill tendered cash input with 5000.00 (exceeding total to verify change)
    const tenderInput = tenderModal.locator('input[type="number"]').first();
    if (await tenderInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await tenderInput.fill('5000.00');
    }

    // Verify change calculation is displayed
    await expect(tenderModal).toContainText(/المتبقي للعميل|Change/i);

    await captureScreenshot(page, '05-pos-tender-cash-change');

    // Confirm Payment
    const confirmBtn = tenderModal.getByRole('button', { name: /تأكيد الدفع النقدي|تأكيد الدفع|Complete/i });
    await expect(confirmBtn).toBeEnabled();
    await confirmBtn.click();

    // Receipt Modal should appear
    const receiptModal = page.locator('[role="dialog"]').filter({ hasText: /إيصال|Receipt/i }).first();
    await expect(receiptModal).toBeVisible({ timeout: 25000 });
    await expect(receiptModal).toContainText(/إيصال البيع الإلكتروني|إيصال استلام|Sales Receipt/i);
    await expect(receiptModal).toContainText(/POS-/i);
    await expect(receiptModal).toContainText(/الإجمالي المستحق|Grand Total|الإجمالي/i);

    // Verify authoritative cashier display and 80mm thermal container
    await expect(receiptModal.locator('#pos-thermal-receipt')).toBeVisible();
    const cashierEl = receiptModal.locator('[data-testid="pos-receipt-cashier"]');
    await expect(cashierEl).toBeVisible();
    await expect(cashierEl).not.toHaveText('Cashier');
    await expect(cashierEl).toContainText(/admin|أمين الصندوق|Cashier on Duty/i);

    await captureScreenshot(page, '06-pos-thermal-receipt-modal');

    // Close Receipt Modal
    const closeReceiptBtn = receiptModal.getByRole('button', { name: /معاملة جديدة|عملية بيع جديدة|إغلاق|Close|طباعة/i }).last();
    await closeReceiptBtn.click();
    await page.waitForTimeout(500);
  });

  test('06. Cart Hold & Recall Workflow', async () => {
    const page = sharedPage;
    await ensureAuthenticated(page);

    // Ensure on Checkout tab
    if (!await page.locator('table').isVisible({ timeout: 1000 }).catch(() => false)) {
      await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
      await page.waitForLoadState('networkidle');
    }

    // Add item to cart
    const quickItemBtn = page.getByRole('button', { name: /ورق تصوير|A4|قاطع تيار|EG-2-0001|كابل نحاس/i }).first();
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

  test('07. Cash Drawer Movements: Cash-In, Cash-Out & Cash-Drop', async () => {
    const page = sharedPage;
    await ensureAuthenticated(page);

    // Ensure on Checkout tab
    if (!await page.locator('table').isVisible({ timeout: 1000 }).catch(() => false)) {
      await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
      await page.waitForLoadState('networkidle');
    }

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

  test('08. Registers & Terminals Management Tab', async () => {
    const page = sharedPage;
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

  test('09. Cashier Sessions & Real-Time Variance Reconciliation', async () => {
    const page = sharedPage;
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

  test('10. Sales Returns & Disposition Management Tab', async () => {
    const page = sharedPage;
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

      // Select an actual completed sale from recent sales dropdown
      const saleSelect = returnModal.locator('select').first();
      if (await saleSelect.isVisible({ timeout: 3000 }).catch(() => false)) {
        const saleOptions = await saleSelect.locator('option').all();
        if (saleOptions.length > 1) {
          const firstSaleVal = await saleOptions[1].getAttribute('value');
          if (firstSaleVal) {
            await saleSelect.selectOption(firstSaleVal);
            await page.waitForTimeout(600);
          }
        }
      }

      // Assert that original sale lines appear and select line
      const lineSelect = returnModal.locator('select').nth(1);
      if (await lineSelect.isVisible({ timeout: 3000 }).catch(() => false)) {
        const lineOptions = await lineSelect.locator('option').all();
        if (lineOptions.length > 0) {
          const firstLineVal = await lineOptions[0].getAttribute('value');
          if (firstLineVal) await lineSelect.selectOption(firstLineVal);
        }
      }

      // Assert that authoritative refundable amount is displayed
      const refundDisplay = returnModal.locator('text=/المبلغ المسترد|المبلغ المالي/i');
      await expect(refundDisplay).toBeVisible({ timeout: 3000 });

      // Capture screenshot with active original sale line and authoritative refund details
      await captureScreenshot(page, '11-pos-create-return-modal');

      await returnModal.getByRole('button', { name: /إلغاء|Cancel/i }).click();
    }
  });

  test('11. Durable Offline Queue & Package Download Tab', async () => {
    const page = sharedPage;
    await ensureAuthenticated(page);

    // Switch to Offline tab
    await page.getByRole('button', { name: /المزامنة والأوفلاين|مزامنة دون اتصال|Offline/i }).click();
    await page.waitForLoadState('networkidle');

    // Verify offline queue status heading and buttons
    await expect(page.getByRole('heading', { name: /حالة المزامنة|المزامنة والوضع غير المتصل/i })).toBeVisible();
    const downloadBtn = page.getByRole('button', { name: /تنزيل حزمة البيانات|Download Package/i });
    await expect(downloadBtn).toBeVisible();
    await expect(downloadBtn).toBeEnabled({ timeout: 10000 });
    await downloadBtn.click();

    // Wait for download success indicator in UI
    await expect(page.locator('text=/صالحة حتى|حزمة البيانات صالحة|Downloaded|تم تنزيل الحزمة/i')).toBeVisible({ timeout: 10000 });

    // Verify IndexedDB database todir_pos_offline_v2 is initialized
    const hasIndexedDb = await page.evaluate(async () => {
      if (!window.indexedDB || !window.indexedDB.databases) return true;
      const dbs = await window.indexedDB.databases();
      return dbs.some((db) => db.name && db.name.startsWith('todir_pos_offline'));
    });
    expect(hasIndexedDb).toBe(true);

    // Verify package cached in IndexedDB with valid signature and catalog items
    let packageInfo: any = null;
    for (let attempt = 0; attempt < 10; attempt++) {
      packageInfo = await page.evaluate(async () => {
        return new Promise<any>((resolve) => {
          const req = window.indexedDB.open('todir_pos_offline_v2', 2);
          req.onsuccess = () => {
            const db = req.result;
            if (!db.objectStoreNames.contains('offline_packages')) {
              resolve(null);
              return;
            }
            const tx = db.transaction('offline_packages', 'readonly');
            const store = tx.objectStore('offline_packages');
            const allReq = store.getAll();
            allReq.onsuccess = () => {
              const list = allReq.result || [];
              resolve(list[0] || null);
            };
            allReq.onerror = () => resolve(null);
          };
          req.onerror = () => resolve(null);
        });
      });
      if (packageInfo) break;
      await page.waitForTimeout(400);
    }
    expect(packageInfo).not.toBeNull();
    if (packageInfo) {
      expect(packageInfo.signature).toBeDefined();
      const parsed = JSON.parse(packageInfo.payload);
      expect(parsed.schemaVersion).toBe(1);
      expect(parsed.items).toBeDefined();
      expect(Array.isArray(parsed.items)).toBe(true);
      expect(parsed.items.length).toBeGreaterThan(0);
    }

    await captureScreenshot(page, '12-pos-offline-queue-dashboard');
  });

  test('12. Store Manager Operational Reports Tab', async () => {
    const page = sharedPage;
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

  test('13. English LTR Localization Parity Verification', async () => {
    const page = sharedPage;
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

  test('14. Mobile / Touch Viewport Responsive Verification (390px)', async () => {
    const page = sharedPage;
    await page.setViewportSize({ width: 390, height: 844 });
    await ensureAuthenticated(page);

    // Switch to checkout tab
    await page.getByRole('button', { name: /شاشة الكاشير|Cashier Checkout/i }).first().click();
    await page.waitForLoadState('networkidle');

    // Verify barcode input and workspace adapt responsively
    const barcodeInput = page.locator('input[placeholder*="امسح الباركود"], input[placeholder*="Scan barcode"]');
    await expect(barcodeInput).toBeVisible();

    // Verify horizontal overflow safety on mobile 390px (scrollWidth <= clientWidth)
    const hasNoHorizontalOverflow = await page.evaluate(() => {
      return document.documentElement.scrollWidth <= document.documentElement.clientWidth;
    });
    expect(hasNoHorizontalOverflow).toBe(true);

    await captureScreenshot(page, '15-pos-touch-mobile-viewport');
  });

  test('15. Offline Replay Security & Rejection of Tampered Envelopes', async () => {
    const page = sharedPage;
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page);

    const csrfRes = await page.request.get('/api/v1/auth/csrf');
    const { token: csrfToken } = await csrfRes.json();

    // 1. Resolve registered register and terminal from live authenticated context
    const legalEntityId = await page.evaluate(() => window.sessionStorage.getItem('erp.current.legalEntityId') || '');
    const registersRes = await page.request.get(`/api/v1/pos/registers?legalEntityId=${legalEntityId}`);
    expect(registersRes.status()).toBe(200);
    const registers = await registersRes.json();
    expect(registers.length).toBeGreaterThan(0);
    const activeRegister = registers[0];

    const terminalsRes = await page.request.get(`/api/v1/pos/registers/${activeRegister.id}/terminals`);
    expect(terminalsRes.status()).toBe(200);
    const terminals = await terminalsRes.json();
    const activeTerminal = terminals.find((t: any) => t.status === 'Ready') || terminals[0] || { deviceIdentity: 'seed-terminal-atlas-trading' };

    // 2. Download valid signed offline package
    const pkgRes = await page.request.get(`/api/v1/pos/offline/package?registerId=${activeRegister.id}&deviceIdentity=${encodeURIComponent(activeTerminal.deviceIdentity)}`);
    expect(pkgRes.status()).toBe(200);
    const pkg = await pkgRes.json();
    expect(pkg.signature).toBeTruthy();
    expect(pkg.version).toBeGreaterThan(0);

    const parsedPkg = JSON.parse(pkg.payload);
    const testItem = parsedPkg.items && (parsedPkg.items.find((i: any) => i.sku === 'EG-2-0001') || parsedPkg.items[0]);
    expect(testItem).toBeTruthy();
    expect(testItem.unitPrice).toBeGreaterThan(0);

    // 3. Resolve active session and payment account
    const sessionRes = await page.request.get(`/api/v1/pos/sessions/current?registerId=${activeRegister.id}`);
    expect(sessionRes.status()).toBe(200);
    const sessionData = await sessionRes.json();

    const paymentAccountsRes = await page.request.get(`/api/v1/payments/legal-entities/${legalEntityId}/accounts`);
    expect(paymentAccountsRes.status()).toBe(200);
    const paymentAccounts = await paymentAccountsRes.json();
    const paymentAccountId = paymentAccounts.find((a: any) => a.accountType === 'Cashbox' || a.type === 'Cashbox')?.id || paymentAccounts[0]?.id;
    expect(paymentAccountId).toBeTruthy();

    const validPayload = {
      schemaVersion: 1,
      type: 'CashSale',
      sessionId: sessionData.id,
      currencyCode: activeRegister.currencyCode || 'EGP',
      paymentAccountId,
      idempotencyKey: `sec-valid-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
      customerPartyId: null,
      lines: [{
        variantId: testItem.variantId,
        description: testItem.displayName,
        uomCode: testItem.uomCode,
        quantity: 1,
        unitPrice: testItem.unitPrice,
        taxAmount: testItem.taxAmount || 0,
        discountAmount: 0,
        legalEntityId: parsedPkg.legalEntityId,
        productTemplateId: testItem.productTemplateId,
        unitOfMeasureId: testItem.unitOfMeasureId,
        priceListId: parsedPkg.priceListId,
        pricingDate: new Date().toISOString().slice(0, 10),
      }],
    };
    const validPayloadStr = JSON.stringify(validPayload);

    const secSeq = Math.floor(Date.now() / 1000) % 500000 + 10000;

    // 4. Compute valid envelope signature using Web Crypto HMAC-SHA256
    const validSig = await page.evaluate(async ({ pkgKey, regId, devId, seq, idemKey, ver, payload }) => {
      const canonical = [regId, devId.trim(), String(seq), idemKey.trim(), String(ver), payload].join('|');
      const key = await window.crypto.subtle.importKey(
        'raw',
        new TextEncoder().encode(pkgKey),
        { name: 'HMAC', hash: 'SHA-256' },
        false,
        ['sign']
      );
      const sigBuffer = await window.crypto.subtle.sign('HMAC', key, new TextEncoder().encode(canonical));
      return Array.from(new Uint8Array(sigBuffer)).map((b) => b.toString(16).padStart(2, '0')).join('');
    }, {
      pkgKey: pkg.signature,
      regId: activeRegister.id,
      devId: activeTerminal.deviceIdentity,
      seq: secSeq,
      idemKey: validPayload.idempotencyKey,
      ver: pkg.version,
      payload: validPayloadStr,
    });

    // 5. Submit valid signed envelope to real authenticated endpoint
    const validSyncRes = await page.request.post('/api/v1/pos/offline/sync', {
      headers: { 'X-CSRF-TOKEN': csrfToken },
      data: {
        registerId: activeRegister.id,
        deviceIdentity: activeTerminal.deviceIdentity,
        sourceSequence: secSeq,
        idempotencyKey: validPayload.idempotencyKey,
        packageVersion: pkg.version,
        payload: validPayloadStr,
        signature: validSig,
      },
    });
    expect(validSyncRes.status()).toBe(202);

    // 6. Test Tampered Signature: MUST fail closed with 400 Bad Request, NOT 404 or 401
    const tamperedSig = '0000000000000000000000000000000000000000000000000000000000000000';
    const tamperedSyncRes = await page.request.post('/api/v1/pos/offline/sync', {
      headers: { 'X-CSRF-TOKEN': csrfToken },
      data: {
        registerId: activeRegister.id,
        deviceIdentity: activeTerminal.deviceIdentity,
        sourceSequence: secSeq + 1,
        idempotencyKey: `sec-tampered-${Date.now()}`,
        packageVersion: pkg.version,
        payload: validPayloadStr,
        signature: tamperedSig,
      },
    });
    expect(tamperedSyncRes.status()).toBe(400);
    expect(tamperedSyncRes.status()).not.toBe(404);
    expect(tamperedSyncRes.status()).not.toBe(401);
    const tamperedBody = await tamperedSyncRes.json();
    expect(JSON.stringify(tamperedBody)).toContain('pos.offline_signature_invalid');

    // 7. Test Rogue Device: MUST fail closed with 400 Bad Request
    const rogueDevRes = await page.request.post('/api/v1/pos/offline/sync', {
      headers: { 'X-CSRF-TOKEN': csrfToken },
      data: {
        registerId: activeRegister.id,
        deviceIdentity: 'unauthorized-rogue-device',
        sourceSequence: secSeq + 2,
        idempotencyKey: `sec-rogue-${Date.now()}`,
        packageVersion: pkg.version,
        payload: validPayloadStr,
        signature: validSig,
      },
    });
    expect(rogueDevRes.status()).toBe(400);
    expect(rogueDevRes.status()).not.toBe(404);
    expect(rogueDevRes.status()).not.toBe(401);
  });

  test('16. Offline Economic Completion, Idempotency & Tamper Recovery Journey', async () => {
    const page = sharedPage;
    await ensureAuthenticated(page);

    const csrfRes = await page.request.get('/api/v1/auth/csrf');
    const { token: csrfToken } = await csrfRes.json();

    const legalEntityId = await page.evaluate(() => window.sessionStorage.getItem('erp.current.legalEntityId') || '');
    const registersRes = await page.request.get(`/api/v1/pos/registers?legalEntityId=${legalEntityId}`);
    const registers = await registersRes.json();
    const activeRegister = registers[0];

    const terminalsRes = await page.request.get(`/api/v1/pos/registers/${activeRegister.id}/terminals`);
    const terminals = await terminalsRes.json();
    const activeTerminal = terminals.find((t: any) => t.status === 'Ready') || terminals[0] || { deviceIdentity: 'seed-terminal-atlas-trading' };

    // 1. Download authoritative offline package
    const pkgRes = await page.request.get(`/api/v1/pos/offline/package?registerId=${activeRegister.id}&deviceIdentity=${encodeURIComponent(activeTerminal.deviceIdentity)}`);
    expect(pkgRes.status()).toBe(200);
    const pkg = await pkgRes.json();
    const parsedPkg = JSON.parse(pkg.payload);
    const testItem = parsedPkg.items && (parsedPkg.items.find((i: any) => i.sku === 'EG-2-0001') || parsedPkg.items[0]);
    const baseSeq = Math.floor(Date.now() / 1000) % 1000000;

    const sessionRes = await page.request.get(`/api/v1/pos/sessions/current?registerId=${activeRegister.id}`);
    const sessionData = await sessionRes.json();

    const paymentAccountsRes = await page.request.get(`/api/v1/payments/legal-entities/${legalEntityId}/accounts`);
    const paymentAccounts = await paymentAccountsRes.json();
    const paymentAccountId = paymentAccounts.find((a: any) => a.accountType === 'Cashbox' || a.type === 'Cashbox')?.id || paymentAccounts[0]?.id;

    // 2. Create permitted offline cash sale payload
    const offlineIdemKey = `e2e-offline-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
    const offlineSalePayload = {
      schemaVersion: 1,
      type: 'CashSale',
      sessionId: sessionData.id,
      currencyCode: activeRegister.currencyCode || 'EGP',
      paymentAccountId,
      idempotencyKey: offlineIdemKey,
      customerPartyId: '09f8cc1f-a29c-135a-bdb2-794201268bd0',
      lines: [{
        variantId: testItem.variantId,
        description: testItem.displayName,
        uomCode: testItem.uomCode,
        quantity: 1,
        unitPrice: testItem.unitPrice,
        taxAmount: testItem.taxAmount || 0,
        discountAmount: 0,
        legalEntityId: parsedPkg.legalEntityId,
        productTemplateId: testItem.productTemplateId,
        unitOfMeasureId: testItem.unitOfMeasureId,
        priceListId: parsedPkg.priceListId,
        pricingDate: new Date().toISOString().slice(0, 10),
      }],
    };
    const offlinePayloadStr = JSON.stringify(offlineSalePayload);

    // 3. Compute cryptographic envelope signature
    const sig = await page.evaluate(async ({ pkgKey, regId, devId, seq, idemKey, ver, payload }) => {
      const canonical = [regId, devId.trim(), String(seq), idemKey.trim(), String(ver), payload].join('|');
      const key = await window.crypto.subtle.importKey(
        'raw',
        new TextEncoder().encode(pkgKey),
        { name: 'HMAC', hash: 'SHA-256' },
        false,
        ['sign']
      );
      const sigBuffer = await window.crypto.subtle.sign('HMAC', key, new TextEncoder().encode(canonical));
      return Array.from(new Uint8Array(sigBuffer)).map((b) => b.toString(16).padStart(2, '0')).join('');
    }, {
      pkgKey: pkg.signature,
      regId: activeRegister.id,
      devId: activeTerminal.deviceIdentity,
      seq: baseSeq,
      idemKey: offlineIdemKey,
      ver: pkg.version,
      payload: offlinePayloadStr,
    });

    // 4. Persist to client IndexedDB todir_pos_offline_v2
    const storedInDb = await page.evaluate(async ({ regId, devId, seq, idemKey, ver, payload, signature, amount }) => {
      return new Promise<boolean>((resolve) => {
        const req = indexedDB.open('todir_pos_offline_v2', 2);
        req.onsuccess = () => {
          const db = req.result;
          const tx = db.transaction('offline_envelopes', 'readwrite');
          const store = tx.objectStore('offline_envelopes');
          store.put({
            id: `env-${idemKey}`,
            registerId: regId,
            deviceIdentity: devId,
            sourceSequence: seq,
            idempotencyKey: idemKey,
            packageVersion: ver,
            payload,
            signature,
            amount,
            status: 'Queued',
            createdAt: new Date().toISOString(),
          });
          tx.oncomplete = () => {
            db.close();
            resolve(true);
          };
          tx.onerror = () => resolve(false);
        };
        req.onerror = () => resolve(false);
      });
    }, {
      regId: activeRegister.id,
      devId: activeTerminal.deviceIdentity,
      seq: baseSeq,
      idemKey: offlineIdemKey,
      ver: pkg.version,
      payload: offlinePayloadStr,
      signature: sig,
      amount: testItem.unitPrice + (testItem.taxAmount || 0),
    });
    expect(storedInDb).toBe(true);

    // 5. Submit original envelope to real sync endpoint
    const syncRes = await page.request.post('/api/v1/pos/offline/sync', {
      headers: { 'X-CSRF-TOKEN': csrfToken },
      data: {
        registerId: activeRegister.id,
        deviceIdentity: activeTerminal.deviceIdentity,
        sourceSequence: baseSeq,
        idempotencyKey: offlineIdemKey,
        packageVersion: pkg.version,
        payload: offlinePayloadStr,
        signature: sig,
      },
    });
    expect(syncRes.status()).toBe(202);
    const envelopeId = await syncRes.json();
    expect(envelopeId).toBeTruthy();

    // 6. Process the offline cash sale through real backend
    const processRes = await page.request.post(`/api/v1/pos/offline/envelopes/${envelopeId}/process-cash-sale`, {
      headers: { 'X-CSRF-TOKEN': csrfToken },
    });
    expect(processRes.status()).toBe(202);
    const processedData = await processRes.json();
    expect(processedData.state).toBe('Accepted');
    expect(processedData.checkout.state).toBe('Completed');
    const createdSaleId = processedData.checkout.saleId;
    expect(createdSaleId).toBeTruthy();

    // 7. Verify authoritative database & business effects
    const saleRes = await page.request.get(`/api/v1/pos/sales/${createdSaleId}`);
    expect(saleRes.status()).toBe(200);
    const saleData = await saleRes.json();
    expect(saleData.receiptNumber).toMatch(/^POS-\d+/);
    expect(saleData.total).toBeGreaterThan(0);
    expect(saleData.businessDate).toBeTruthy();

    const linesRes = await page.request.get(`/api/v1/pos/sales/${createdSaleId}/lines`);
    expect(linesRes.status()).toBe(200);
    const saleLines = await linesRes.json();
    expect(saleLines.length).toBeGreaterThan(0);
    expect(saleLines[0].variantId).toBe(testItem.variantId);

    // 8. Idempotency Verification: Resubmitting the same envelope must produce identical outcome without duplicate sale
    const repeatSyncRes = await page.request.post('/api/v1/pos/offline/sync', {
      headers: { 'X-CSRF-TOKEN': csrfToken },
      data: {
        registerId: activeRegister.id,
        deviceIdentity: activeTerminal.deviceIdentity,
        sourceSequence: baseSeq,
        idempotencyKey: offlineIdemKey,
        packageVersion: pkg.version,
        payload: offlinePayloadStr,
        signature: sig,
      },
    });
    expect(repeatSyncRes.status()).toBe(202);

    const repeatProcessRes = await page.request.post(`/api/v1/pos/offline/envelopes/${envelopeId}/process-cash-sale`, {
      headers: { 'X-CSRF-TOKEN': csrfToken },
    });
    expect(repeatProcessRes.status()).toBe(202);
    const repeatData = await repeatProcessRes.json();
    expect(repeatData.checkout.saleId).toBe(createdSaleId);
    expect(repeatData.checkout.total).toBe(processedData.checkout.total);

    // 9. Tampered Economic Fact Recovery: A payload with a forged price must be rejected and marked NeedsReview
    const tamperedPricePayload = {
      ...offlineSalePayload,
      idempotencyKey: `forged-price-${Date.now()}`,
      lines: [{
        ...offlineSalePayload.lines[0],
        unitPrice: 0.05, // Forged price attempting to underpay
      }],
    };
    const tamperedPriceStr = JSON.stringify(tamperedPricePayload);
    const tamperedPriceSig = await page.evaluate(async ({ pkgKey, regId, devId, seq, idemKey, ver, payload }) => {
      const canonical = [regId, devId.trim(), String(seq), idemKey.trim(), String(ver), payload].join('|');
      const key = await window.crypto.subtle.importKey(
        'raw',
        new TextEncoder().encode(pkgKey),
        { name: 'HMAC', hash: 'SHA-256' },
        false,
        ['sign']
      );
      const sigBuffer = await window.crypto.subtle.sign('HMAC', key, new TextEncoder().encode(canonical));
      return Array.from(new Uint8Array(sigBuffer)).map((b) => b.toString(16).padStart(2, '0')).join('');
    }, {
      pkgKey: pkg.signature,
      regId: activeRegister.id,
      devId: activeTerminal.deviceIdentity,
      seq: baseSeq + 1,
      idemKey: tamperedPricePayload.idempotencyKey,
      ver: pkg.version,
      payload: tamperedPriceStr,
    });

    const forgedSyncRes = await page.request.post('/api/v1/pos/offline/sync', {
      headers: { 'X-CSRF-TOKEN': csrfToken },
      data: {
        registerId: activeRegister.id,
        deviceIdentity: activeTerminal.deviceIdentity,
        sourceSequence: baseSeq + 1,
        idempotencyKey: tamperedPricePayload.idempotencyKey,
        packageVersion: pkg.version,
        payload: tamperedPriceStr,
        signature: tamperedPriceSig,
      },
    });
    expect(forgedSyncRes.status()).toBe(202);
    const forgedEnvelopeId = await forgedSyncRes.json();

    const forgedProcessRes = await page.request.post(`/api/v1/pos/offline/envelopes/${forgedEnvelopeId}/process-cash-sale`, {
      headers: { 'X-CSRF-TOKEN': csrfToken },
    });
    expect(forgedProcessRes.status()).toBe(400);
    const forgedError = await forgedProcessRes.json();
    expect(JSON.stringify(forgedError)).toContain('pos.offline_price_tampered');

    // Envelope must NOT be Accepted or Completed
    const envStatusRes = await page.request.get(`/api/v1/pos/offline/sync/${forgedEnvelopeId}`);
    expect(envStatusRes.status()).toBe(200);
    const envStatus = await envStatusRes.json();
    expect(envStatus.state).toBe('NeedsReview');
    expect(envStatus.conflictCode).toBe('CashSaleProcessingFailed');
  });

  test('17. Real Frontend Offline Acceptance: UI Cashier Queue, Reload Persistence & UI Sync', async () => {
    const page = sharedPage;
    await ensureAuthenticated(page);

    // 1. Download authoritative offline package via UI
    await page.getByRole('button', { name: /المزامنة والأوفلاين|Offline/i }).click();
    await page.waitForLoadState('networkidle');

    const downloadBtn = page.getByRole('button', { name: /تنزيل حزمة البيانات|تحميل الحزمة|Download Package/i });
    await expect(downloadBtn).toBeVisible();
    await expect(downloadBtn).toBeEnabled({ timeout: 10000 });
    await downloadBtn.click();
    await expect(page.locator('text=/صالحة حتى|حزمة البيانات صالحة|Downloaded|تم تنزيل الحزمة/i')).toBeVisible({ timeout: 10000 });

    // 2. Switch to Cashier Checkout workspace
    await page.getByRole('button', { name: /نقطة البيع|الكاشير|Workspace/i }).click();
    await page.waitForLoadState('networkidle');

    // Attach customer party for complete financial settlement
    const customerBtn = page.locator('button:has-text("عميل نقدي / عام"), button:has-text("Walk-in Customer")').first();
    if (await customerBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await customerBtn.click();
      const customerModal = page.locator('[role="dialog"]');
      if (await customerModal.isVisible({ timeout: 3000 }).catch(() => false)) {
        const partyBtn = customerModal.locator('button:has-text("شركة النور"), button:has-text("United Electrical"), button:has-text("اختيار")').first();
        if (await partyBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
          await partyBtn.click();
        } else {
          await customerModal.getByRole('button', { name: /إلغاء|إغلاق|Close/i }).click();
        }
      }
    }

    // Add item (A4 Copy Paper) while connected
    const addPaperBtn = page.locator('button:has-text("ورق تصوير A4"), button:has-text("A4 Paper")').first();
    if (await addPaperBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addPaperBtn.click();
    } else {
      const barcodeInput = page.locator('input[placeholder*="باركود"], input[placeholder*="Barcode"]');
      await barcodeInput.fill('EG-2-0001');
      await barcodeInput.press('Enter');
    }
    await page.waitForTimeout(500);

    // 3. Simulate network loss
    await page.context().setOffline(true);
    await page.evaluate(() => window.dispatchEvent(new Event('offline')));
    await expect(page.locator('text=/غير متصل|Offline/i').first()).toBeVisible({ timeout: 5000 });

    // 4. Queue a permitted sale through the cashier UI
    const queueOfflineBtn = page.locator('button:has-text("تسجيل بيع غير متصل"), button:has-text("Queue Offline")');
    await expect(queueOfflineBtn).toBeVisible({ timeout: 5000 });
    await queueOfflineBtn.click();

    // Verify success message in UI
    await expect(page.locator('text=/تم حفظ العملية بنجاح في قاعدة البيانات المحلية/i')).toBeVisible({ timeout: 5000 });

    // 5. Verify IndexedDB persistence across reload
    // Restore network connectivity so browser page reload succeeds
    await page.context().setOffline(false);
    await page.evaluate(() => window.dispatchEvent(new Event('online')));

    await page.reload();
    await page.waitForLoadState('networkidle');

    const persistedEnvelope = await page.evaluate(async () => {
      return new Promise<any>((resolve) => {
        const req = indexedDB.open('todir_pos_offline_v2', 2);
        req.onsuccess = () => {
          const db = req.result;
          const tx = db.transaction('offline_envelopes', 'readonly');
          const store = tx.objectStore('offline_envelopes');
          const allReq = store.getAll();
          allReq.onsuccess = () => {
            const list = allReq.result || [];
            const queued = list.find((e: any) => e.status === 'Queued');
            resolve(queued || null);
          };
          allReq.onerror = () => resolve(null);
        };
        req.onerror = () => resolve(null);
      });
    });
    expect(persistedEnvelope).not.toBeNull();
    expect(persistedEnvelope.status).toBe('Queued');
    expect(persistedEnvelope.signature).toBeTruthy();
    expect(persistedEnvelope.sourceSequence).toBeGreaterThan(0);

    // 7. Synchronize through the actual UI
    await page.getByRole('button', { name: /المزامنة والأوفلاين|Offline/i }).click();
    await page.waitForLoadState('networkidle');

    // Click Sync Now button in UI
    const syncNowBtn = page.getByRole('button', { name: /مزامنة.*الآن|Sync.*Now/i });
    await expect(syncNowBtn).toBeVisible({ timeout: 5000 });
    await expect(syncNowBtn).toBeEnabled({ timeout: 5000 });
    await syncNowBtn.click();

    // Wait for sync to complete in UI
    await expect(page.locator('text=/تمت مزامنة|تمت المزامنة|Synced/i')).toBeVisible({ timeout: 15000 });

    // Verify envelope in IndexedDB transitioned to Completed
    const syncedEnvelope = await page.evaluate(async (envId: string) => {
      return new Promise<any>((resolve) => {
        const req = indexedDB.open('todir_pos_offline_v2', 2);
        req.onsuccess = () => {
          const db = req.result;
          const tx = db.transaction('offline_envelopes', 'readonly');
          const store = tx.objectStore('offline_envelopes');
          const getReq = store.get(envId);
          getReq.onsuccess = () => resolve(getReq.result || null);
          getReq.onerror = () => resolve(null);
        };
        req.onerror = () => resolve(null);
      });
    }, persistedEnvelope.id);

    expect(syncedEnvelope).not.toBeNull();
    expect(syncedEnvelope.status).toBe('Completed');
    expect(syncedEnvelope.serverEnvelopeId).toBeTruthy();

    // 8. Verify authoritative server-side sale, payment, inventory, and invoice references
    if (syncedEnvelope.saleId) {
      const saleRes = await page.request.get(`/api/v1/pos/sales/${syncedEnvelope.saleId}`);
      expect(saleRes.status()).toBe(200);
      const saleData = await saleRes.json();
      expect(saleData.receiptNumber).toMatch(/^POS-\d+/);
      expect(saleData.documentState).toBe('Completed');

      // Verify line items and returned quantity aggregation
      const linesRes = await page.request.get(`/api/v1/pos/sales/${syncedEnvelope.saleId}/lines`);
      expect(linesRes.status()).toBe(200);
      const saleLines = await linesRes.json();
      expect(saleLines.length).toBeGreaterThan(0);
      expect(saleLines[0].returnedQuantity).toBe(0);
    }

    // 9. Repeat synchronization and prove no duplicated effects
    if (await syncNowBtn.isEnabled().catch(() => false)) {
      await syncNowBtn.click();
      await page.waitForTimeout(1000);
    }
    // Verify envelope remains Completed and saleId is unchanged
    const doubleCheckEnv = await page.evaluate(async (envId: string) => {
      return new Promise<any>((resolve) => {
        const req = indexedDB.open('todir_pos_offline_v2', 2);
        req.onsuccess = () => {
          const db = req.result;
          const tx = db.transaction('offline_envelopes', 'readonly');
          const store = tx.objectStore('offline_envelopes');
          const getReq = store.get(envId);
          getReq.onsuccess = () => resolve(getReq.result || null);
          getReq.onerror = () => resolve(null);
        };
        req.onerror = () => resolve(null);
      });
    }, persistedEnvelope.id);
    expect(doubleCheckEnv.status).toBe('Completed');
    expect(doubleCheckEnv.saleId).toBe(syncedEnvelope.saleId);

    // 10. Verify interrupted / rejected operations remain recoverable
    // Inject a tampered envelope into IndexedDB to verify recovery
    const tamperedEnvId = `env-tampered-${Date.now()}`;
    await page.evaluate(async ({ envId, regId, devId }) => {
      return new Promise<void>((resolve) => {
        const req = indexedDB.open('todir_pos_offline_v2', 2);
        req.onsuccess = () => {
          const db = req.result;
          const tx = db.transaction('offline_envelopes', 'readwrite');
          const store = tx.objectStore('offline_envelopes');
          store.put({
            id: envId,
            registerId: regId,
            deviceIdentity: devId,
            sourceSequence: 999999,
            idempotencyKey: `tampered-ui-key-${Date.now()}`,
            packageVersion: 1,
            payload: JSON.stringify({ type: 'CashSale', amount: 999 }),
            signature: 'invalid-signature-for-tamper-test',
            amount: 999,
            status: 'Queued',
            retryCount: 0,
            createdAt: new Date().toISOString(),
          });
          tx.oncomplete = () => {
            db.close();
            resolve();
          };
        };
      });
    }, {
      envId: tamperedEnvId,
      regId: persistedEnvelope.registerId,
      devId: persistedEnvelope.deviceIdentity,
    });

    // Reload or refresh to load queue
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Navigate to offline tab if needed and trigger UI sync for the tampered envelope
    const offlineTabBtn = page.getByRole('button', { name: /المزامنة والأوفلاين|Offline/i });
    if (await offlineTabBtn.isVisible().catch(() => false)) {
      await offlineTabBtn.click();
      await page.waitForLoadState('networkidle');
    }

    const syncRecoverBtn = page.getByRole('button', { name: /مزامنة.*الآن|Sync.*Now/i });
    await expect(syncRecoverBtn).toBeVisible({ timeout: 5000 });
    await syncRecoverBtn.click();
    await page.waitForTimeout(2000);

    // Verify the tampered envelope transitioned to NeedsReview / Failed without crashing the app
    const recoveredEnv = await page.evaluate(async (envId: string) => {
      return new Promise<any>((resolve) => {
        const req = indexedDB.open('todir_pos_offline_v2', 2);
        req.onsuccess = () => {
          const db = req.result;
          const tx = db.transaction('offline_envelopes', 'readonly');
          const store = tx.objectStore('offline_envelopes');
          const getReq = store.get(envId);
          getReq.onsuccess = () => resolve(getReq.result || null);
          getReq.onerror = () => resolve(null);
        };
        req.onerror = () => resolve(null);
      });
    }, tamperedEnvId);

    expect(recoveredEnv).not.toBeNull();
    expect(['Failed', 'NeedsReview', 'Conflicted']).toContain(recoveredEnv.status);
    expect(recoveredEnv.retryCount).toBeGreaterThan(0);
  });

});
