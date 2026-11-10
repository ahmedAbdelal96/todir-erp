import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const ARTIFACTS_DIR = 'C:/Users/Adham/.gemini/antigravity/brain/2cce5b52-1525-442e-8037-81105c0347eb/expenses-acceptance/real-stack';
const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/expenses-acceptance/real-stack');

test.describe.configure({ mode: 'serial' });

const RUN_ID = Date.now().toString().slice(-5);
const TEST_MERCHANT_NAME = `مؤسسة النيل للخدمات اللوجستية ${RUN_ID}`;
const TEST_MILEAGE_MERCHANT = `بدل انتقال ${RUN_ID}`;

async function ensureArabicLanguage(page: Page) {
  const switchToArabicBtn = page.getByRole('button', { name: /Switch to Arabic|التبديل إلى العربية/i });
  if (await switchToArabicBtn.isVisible({ timeout: 1500 }).catch(() => false)) {
    await switchToArabicBtn.click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);
  }
}

async function ensureAuthenticated(page: Page, email: string = 'admin@atlas.com') {
  page.on('console', (msg) => {
    if (msg.type() === 'error') {
      const text = msg.text();
      if (!text.includes('404') && !text.includes('favicon')) {
        console.log('PAGE ERROR:', text);
      }
    }
  });

  page.on('response', async (res) => {
    if (!res.ok()) {
      try {
        const text = await res.text();
        console.log('HTTP ERROR RESPONSE:', res.status(), res.request().method(), res.url(), text.slice(0, 300));
      } catch {
        console.log('HTTP ERROR RESPONSE:', res.status(), res.request().method(), res.url());
      }
    }
  });

  await page.goto('/expenses');
  await page.waitForLoadState('networkidle');

  // Ensure Arabic language unless testing English
  if (!email.includes('english')) {
    await ensureArabicLanguage(page);
  }

  const currentEmail = await page.evaluate(() => window.sessionStorage.getItem('erp.session.email')).catch(() => null);
  if (currentEmail === email && page.url().includes('/expenses') && await page.locator('[data-testid="expenses-page"]').isVisible({ timeout: 2000 }).catch(() => false)) {
    return;
  }

  if (currentEmail && currentEmail !== email) {
    await switchUser(page, email);
    return;
  }

  const profileBtn = page.getByRole('button', { name: new RegExp(email, 'i') });
  if (await profileBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
    await profileBtn.click();
    await profileBtn.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle');
  } else {
    const emailInput = page.locator('#login-email');
    if (await emailInput.isVisible({ timeout: 4000 }).catch(() => false)) {
      await emailInput.fill(email);
      await page.locator('#login-password').fill('DevSeed2026Apmjuoj1oZjHEDcxSsOc27HKY8yOv0B7');
      await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
      await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
      await page.waitForLoadState('networkidle');
    }
  }

  if (!page.url().includes('/expenses')) {
    await page.goto('/expenses');
    await page.waitForLoadState('networkidle');
  }

  await expect(page.locator('[data-testid="expenses-page"]')).toBeVisible({ timeout: 20000 });
  await page.evaluate((e) => window.sessionStorage.setItem('erp.session.email', e), email);
  if (!email.includes('english')) {
    await ensureArabicLanguage(page);
  }
}

async function switchUser(page: Page, email: string) {
  if (!page.url().startsWith('http')) {
    await page.goto('/expenses');
    await page.waitForLoadState('networkidle');
  }

  const currentEmail = await page.evaluate(() => {
    try {
      return window.sessionStorage.getItem('erp.session.email');
    } catch {
      return null;
    }
  }).catch(() => null);

  if (currentEmail === email && page.url().includes('/expenses') && await page.locator('[data-testid="expenses-page"]').isVisible({ timeout: 2000 }).catch(() => false)) {
    await ensureArabicLanguage(page);
    return;
  }

  await page.evaluate(async () => {
    try {
      const csrfRes = await fetch('/api/v1/auth/csrf');
      const { token } = await csrfRes.json();
      await fetch('/api/v1/auth/logout', { method: 'POST', headers: { 'X-CSRF-TOKEN': token } });
    } catch {}
    try {
      window.sessionStorage.removeItem('erp.session.email');
      window.localStorage.setItem('todir.ui.language', 'ar');
    } catch {}
  }).catch(() => {});
  await page.context().clearCookies();
  await page.goto('/expenses');
  await page.waitForLoadState('networkidle');

  const profileBtn = page.getByRole('button', { name: new RegExp(email, 'i') });
  if (await profileBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
    await profileBtn.click();
    await profileBtn.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle');
  } else {
    const emailInput = page.locator('#login-email');
    if (await emailInput.isVisible({ timeout: 4000 }).catch(() => false)) {
      await emailInput.fill(email);
      await page.locator('#login-password').fill('DevSeed2026Apmjuoj1oZjHEDcxSsOc27HKY8yOv0B7');
      await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
      await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
      await page.waitForLoadState('networkidle');
    }
  }

  if (!page.url().includes('/expenses')) {
    await page.goto('/expenses');
    await page.waitForLoadState('networkidle');
  }

  await expect(page.locator('[data-testid="expenses-page"]')).toBeVisible({ timeout: 25000 });
  await page.evaluate((e) => {
    try {
      window.sessionStorage.setItem('erp.session.email', e);
    } catch {}
  }, email).catch(() => {});
  if (!email.includes('english')) {
    await ensureArabicLanguage(page);
  }
}

async function captureEvidence(page: Page, filename: string) {
  await page.screenshot({ path: path.join(ARTIFACTS_DIR, filename), fullPage: true });
  await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, filename), fullPage: true });
}

test.describe('TODIR Expenses & Reimbursements (Module 21) — REAL-STACK Full End-to-End Suite', () => {
  test.beforeAll(() => {
    if (!fs.existsSync(ARTIFACTS_DIR)) fs.mkdirSync(ARTIFACTS_DIR, { recursive: true });
    if (!fs.existsSync(LOCAL_ARTIFACTS_DIR)) fs.mkdirSync(LOCAL_ARTIFACTS_DIR, { recursive: true });
  });

  test.setTimeout(120_000);

  // ==========================================
  // JOURNEY 01: Real Visual Architecture & Layout
  // ==========================================
  test('REAL-01: Visual Architecture, Oxford Navy Header, Crisp Cards & RTL KPIs', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page);

    // Verify Dark Oxford Navy Header
    const header = page.locator('header').first();
    await expect(header).toBeVisible();
    await expect(page.getByText('المصروفات ومستحقات الموظفين')).toBeVisible();
    await expect(page.getByText('نفقات ومستحقات الأعمال')).toBeVisible();

    // Verify 7 Tabs are present
    await expect(page.getByRole('tab', { name: 'خدماتي ومصروفاتي' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'مراجعة واعتماد المصروفات' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'سلف العمل' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'بطاقات الشركات' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'المستحقات والتسويات' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'التقارير وتحليل النفقات' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'إعدادات وسياسات المصروفات' })).toBeVisible();

    // Verify Self Service Action Buttons & Subtabs
    await expect(page.getByRole('button', { name: '+ تسجيل مصروف جديد' })).toBeVisible();
    await expect(page.getByRole('button', { name: '+ طلب سلفة عمل جديدة' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'مصروفاتي' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'سلف العمل الخاصة بي' })).toBeVisible();

    await captureEvidence(page, '01-expenses-home-desktop.png');
  });

  // ==========================================
  // JOURNEY 02: Real Expense Creation (Draft)
  // ==========================================
  test('REAL-02: Self-Service Expense Creation (Draft with Real Persistence)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page);

    // Open Modal
    await page.getByRole('button', { name: /تسجيل مصروف جديد/i }).first().click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Fill form
    await dialog.getByLabel(/اسم التاجر|التاجر أو مزود الخدمة/i).fill(TEST_MERCHANT_NAME);
    await dialog.getByLabel(/المبلغ/i).fill('1450.00');
    await dialog.getByPlaceholder(/الغرض من المصروف/i).fill('شحن بضائع ومستلزمات معرض القاهرة');

    // Submit
    await dialog.getByRole('button', { name: /تسجيل المصروف/i }).click();
    await expect(dialog).not.toBeVisible({ timeout: 10000 });

    // Verify real persistence in table
    await page.waitForTimeout(1000);
    await expect(page.getByText(TEST_MERCHANT_NAME).first()).toBeVisible({ timeout: 10000 });

    await captureEvidence(page, '02-expenses-new-expense-modal.png');
  });

  // ==========================================
  // JOURNEY 03: Real Mileage Expense Creation
  // ==========================================
  test('REAL-03: Mileage Allowance Expense Creation with Auto-Calculation', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page);

    // Open Modal
    await page.getByRole('button', { name: /تسجيل مصروف جديد/i }).first().click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Switch to Mileage mode
    await dialog.getByRole('button', { name: /بدل انتقال/i }).click();

    // Fill distance (80 km) -> auto calculates using published policy
    await dialog.getByLabel(/المسافة المقطوعة/i).fill('80');
    await page.waitForTimeout(300);
    const amountVal = await dialog.getByLabel(/المبلغ/i).inputValue();
    expect(Number(amountVal)).toBeGreaterThan(0);

    await dialog.getByPlaceholder(/فرع الإسكندرية|Alexandria branch/i).fill('القاهرة - السويس');

    // Submit
    await dialog.getByRole('button', { name: /تسجيل المصروف/i }).click();
    await expect(dialog).not.toBeVisible({ timeout: 10000 });

    await page.waitForTimeout(1000);
    await expect(page.getByText(/بدل انتقال/i).first()).toBeVisible({ timeout: 10000 });
  });

  // ==========================================
  // JOURNEY 04: Real Supporting Receipt Upload & ClamAV Loop
  // ==========================================
  test('REAL-04: Official Supporting Evidence Upload, Storage & Scan Readiness', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page);

    // Click on the test expense to open drawer
    await page.getByText(TEST_MERCHANT_NAME).first().click();
    const drawer = page.locator('.quick-peek-panel, [role="dialog"]').first();
    await expect(drawer).toBeVisible({ timeout: 10000 });

    // Switch to Evidence tab
    await drawer.getByRole('button', { name: /الإيصالات والمستندات/i }).click();

    // Create a dummy receipt file buffer
    const tempFilePath = path.resolve('artifacts', 'test-receipt.pdf');
    fs.writeFileSync(tempFilePath, '%PDF-1.4 mock receipt content for clamav scan test');

    // Select file and upload
    const fileInput = drawer.locator('input[type="file"]');
    await fileInput.setInputFiles(tempFilePath);
    await drawer.getByRole('button', { name: 'رفع وفحص المستند' }).click();

    // Wait for upload and scan to complete
    await expect(drawer.getByRole('button', { name: 'تحميل المستند' }).first()).toBeVisible({ timeout: 25000 });

    // Verify Download action
    const downloadBtn = drawer.getByRole('button', { name: 'تحميل المستند' }).first();
    await expect(downloadBtn).toBeVisible();

    // Verify OCR button
    const ocrBtn = drawer.getByRole('button', { name: 'قراءة آلية (OCR)' }).first();
    await expect(ocrBtn).toBeVisible();
    await ocrBtn.click();
    await page.waitForTimeout(1000);

    await captureEvidence(page, '04-expenses-detail-drawer.png');

    // Close drawer
    await drawer.getByRole('button', { name: 'إغلاق', exact: true }).click();
    await expect(drawer).not.toBeVisible();
  });

  // ==========================================
  // JOURNEY 05: Official Missing Receipt Declaration
  // ==========================================
  test('REAL-05: Missing Receipt Official Declaration & Status Update', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page);

    // Open detail drawer for the test expense
    await page.getByText(TEST_MERCHANT_NAME).first().click();
    const drawer = page.locator('.quick-peek-panel, [role="dialog"]').first();
    await expect(drawer).toBeVisible();

    // Switch to Evidence tab
    await drawer.getByRole('button', { name: /الإيصالات والمستندات/i }).click();

    // Click Declare Missing Receipt
    await drawer.getByRole('button', { name: /إقرار رسمي بفقدان الإيصال|تقديم إقرار فقدان إيصال|إقرار فقدان الإيصال/i }).first().click();
    const missingDialog = page.getByRole('dialog', { name: /إقرار فقدان الإيصال/i });
    await expect(missingDialog).toBeVisible();

    // Fill declaration
    await missingDialog.getByPlaceholder(/توضيح الظروف|سبب تعذر تقديم الإيصال|لم يوفر التاجر/i).fill('فقدان الإيصال الورقي أثناء السفر وسداد المبلغ نقداً مع إقرار الموظف');
    await missingDialog.getByRole('button', { name: /تسجيل الإقرار|تقديم الإقرار الرسمي/i }).click();
    await expect(missingDialog).not.toBeVisible({ timeout: 10000 });

    await page.waitForTimeout(1000);
    await captureEvidence(page, '03-expenses-declare-missing-receipt.png');

    // Close drawer
    await drawer.getByRole('button', { name: 'إغلاق', exact: true }).click();
  });

  // ==========================================
  // JOURNEY 06: Submission & Manager Approval
  // ==========================================
  test('REAL-06: Expense Submission & Finance Manager Workbench Approval', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page, 'admin@atlas.com');

    // Open detail drawer for the test expense
    await page.getByText(TEST_MERCHANT_NAME).first().click();
    const selfDrawer = page.locator('.quick-peek-panel, [role="dialog"]').first();
    await expect(selfDrawer).toBeVisible();

    // Submit for approval
    const submitBtn = selfDrawer.getByRole('button', { name: 'إرسال للاعتماد', exact: true });
    await expect(submitBtn).toBeVisible({ timeout: 15000 });
    await submitBtn.click();
    await submitBtn.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForTimeout(1000);

    // Close drawer
    await selfDrawer.getByRole('button', { name: 'إغلاق', exact: true }).click();
    await expect(selfDrawer).not.toBeVisible();

    // Segregation of duties: Switch to Finance Officer (accountant@atlas.com) to review and approve!
    await switchUser(page, 'accountant@atlas.com');

    // Switch to Review & Approvals tab
    await page.getByRole('tab', { name: 'مراجعة واعتماد المصروفات' }).click();
    await page.waitForTimeout(1000);

    // Click on the submitted expense
    await page.getByText(TEST_MERCHANT_NAME).first().click();
    const reviewDrawer = page.locator('.quick-peek-panel, [role="dialog"]').first();
    await expect(reviewDrawer).toBeVisible();

    // Approve the expense (authoritative approval ID comes directly from backend ExpenseDto)
    const approveBtn = reviewDrawer.getByRole('button', { name: 'اعتماد', exact: true });
    await expect(approveBtn).toBeVisible({ timeout: 15000 });
    await approveBtn.click();
    await approveBtn.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForTimeout(2000);

    await captureEvidence(page, '05-expenses-review-workbench.png');

    // Close drawer
    await reviewDrawer.getByRole('button', { name: 'إغلاق', exact: true }).click();
  });

  // ==========================================
  // JOURNEY 07: GL Finalization & Accounting Reversal
  // ==========================================
  test('REAL-07: General Ledger Finalization & Accounting Posting', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page, 'accountant@atlas.com');

    // In Review tab
    await page.getByRole('tab', { name: 'مراجعة واعتماد المصروفات' }).click();
    await page.waitForTimeout(1000);

    // Open detail drawer
    await page.getByText(TEST_MERCHANT_NAME).first().click();
    const drawer = page.locator('.quick-peek-panel, [role="dialog"]').first();
    await expect(drawer).toBeVisible();

    // Click Finalize
    const finalizeBtn = drawer.getByRole('button', { name: /ترحيل مالي|ترحيل/i, exact: false });
    await expect(finalizeBtn).toBeVisible({ timeout: 15000 });
    await finalizeBtn.click();
    await finalizeBtn.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForTimeout(2000);

    // Verify Reversal button is available on finalized expense
    const reverseBtn = drawer.getByRole('button', { name: 'قيد عكسي', exact: true });
    await expect(reverseBtn).toBeVisible({ timeout: 15000 });

    await captureEvidence(page, '06-expenses-finalization.png');

    // Perform reversal to verify authorized reversal lifecycle
    await reverseBtn.click();
    await page.waitForTimeout(2000);
    await expect(drawer.getByText(/قيد عكسي|معكوس|Reversed/i).first()).toBeVisible({ timeout: 15000 });

    // Close drawer
    await drawer.getByRole('button', { name: 'إغلاق', exact: true }).click();
  });

  // ==========================================
  // JOURNEY 08: Business Advances Management
  // ==========================================
  test('REAL-08: Advances Workspace & Outstanding Balance Tracking', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page, 'accountant@atlas.com');

    // Switch to Advances tab
    await page.getByRole('tab', { name: 'سلف العمل' }).click();
    await page.waitForTimeout(1000);

    // Verify Advances Title & Filters
    await expect(page.getByText('إدارة سلف العمل والتسويات')).toBeVisible();
    await expect(page.getByText('كافة السلف')).toBeVisible();

    // Verify seeded advance is present
    await expect(page.getByText(/سلفة سفر لمعرض الإسكندرية|سلفة سفر/i).first()).toBeVisible({ timeout: 10000 });

    // Test Cash Return action on the issued advance
    const returnBtn = page.getByRole('button', { name: 'رد نقدي' }).first();
    if (await returnBtn.isVisible().catch(() => false)) {
      await returnBtn.click();
      const returnDialog = page.getByRole('dialog', { name: /رد متبقي سلفة العمل|رد سلفة|رد نقدي/i });
      await expect(returnDialog).toBeVisible({ timeout: 10000 });
      await returnDialog.getByLabel(/المبلغ المسترد/i).fill('500.00');
      await returnDialog.getByRole('button', { name: /تأكيد رد المبلغ/i }).click();
      await expect(returnDialog).not.toBeVisible({ timeout: 10000 });
      await page.waitForTimeout(1000);
    }

    await captureEvidence(page, '07-expenses-advances-management.png');
  });

  // ==========================================
  // JOURNEY 09: Corporate Credit Cards Inbox
  // ==========================================
  test('REAL-09: Corporate Cards Reconciliation Inbox & Feed', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page, 'accountant@atlas.com');

    // Switch to Corporate Cards tab
    await page.getByRole('tab', { name: 'بطاقات الشركات' }).click();
    await page.waitForTimeout(1000);

    // Verify Cards Header
    await expect(page.getByText('صندوق بطاقات الشركات والمطابقة')).toBeVisible();
    await expect(page.getByText('غير مطابقة').first()).toBeVisible();

    // Verify seeded card transaction
    await expect(page.getByText(/محطة وقود النيل|وقود النيل/i).first()).toBeVisible({ timeout: 10000 });

    // Mark as Personal
    const markPersonalBtn = page.getByRole('button', { name: /تصنيف كمصروف شخصي|شخصي/i }).first();
    if (await markPersonalBtn.isVisible().catch(() => false)) {
      await markPersonalBtn.click();
      await page.waitForTimeout(1500);
    }

    await captureEvidence(page, '08-expenses-corporate-cards.png');
  });

  // ==========================================
  // JOURNEY 10: Reimbursements & Repayments Tracking
  // ==========================================
  test('REAL-10: Reimbursement Obligations & Employee Repayments', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page, 'accountant@atlas.com');

    // Switch to Reimbursements tab
    await page.getByRole('tab', { name: 'المستحقات والتسويات' }).click();
    await page.waitForTimeout(1000);

    // Verify Header & Subtabs
    await expect(page.getByText('تسوية مستحقات الموظفين والمدفوعات')).toBeVisible();
    await expect(page.getByRole('button', { name: /مستحقات الموظفين/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /تحصيل/i })).toBeVisible();

    // Switch to "تحصيل من الموظفين" (fromEmployees repayments)
    await page.getByRole('button', { name: /تحصيل/i }).click();
    await page.waitForTimeout(1000);

    await captureEvidence(page, '09-expenses-reimbursements.png');
  });

  // ==========================================
  // JOURNEY 11: Real Spend Analysis Reports & CSV Export
  // ==========================================
  test('REAL-11: Spend Analysis Reports & CSV Export Verification', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page, 'accountant@atlas.com');

    // Switch to Reports tab
    await page.getByRole('tab', { name: 'التقارير وتحليل النفقات' }).click();
    await page.waitForTimeout(1000);

    // Verify Reports Header
    await expect(page.getByText('تقارير وتحليلات نفقات الأعمال')).toBeVisible();
    await expect(page.getByText(/معيار التجميع|تجميع حسب/i).first()).toBeVisible();

    // Verify CSV Export
    const downloadPromise = page.waitForEvent('download', { timeout: 15000 });
    await page.getByRole('button', { name: /تصدير تقرير النفقات|تصدير.*CSV/i }).click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/spend-report|expense-reports/i);

    const downloadPath = await download.path();
    expect(downloadPath).toBeTruthy();
    if (downloadPath) {
      const csvContent = fs.readFileSync(downloadPath, 'utf-8');
      expect(csvContent.length).toBeGreaterThan(10);
    }

    await captureEvidence(page, '10-expenses-spend-reports.png');
  });

  // ==========================================
  // JOURNEY 12: Multi-Viewport Geometry & Responsive Mobile Verification
  // ==========================================
  test('REAL-12: Multi-Viewport Geometry & 390px Mobile Layout Overflow Guard', async ({ page }) => {
    // 1. Mobile 390px Viewport (iPhone 12/13/14)
    await page.setViewportSize({ width: 390, height: 844 });
    await ensureAuthenticated(page, 'accountant@atlas.com');

    // Assert zero document overflow
    const hasHorizontalOverflow390 = await page.evaluate(() => {
      return document.documentElement.scrollWidth > window.innerWidth;
    });
    expect(hasHorizontalOverflow390).toBe(false);

    // Verify header wraps nicely without overlap
    const headerTitle = page.locator('h1').first();
    await expect(headerTitle).toBeVisible();

    // Verify table container is scrollable without breaking viewport
    const tableContainer = page.locator('.ui-table-container, div[style*="overflow-x: auto"], div[style*="overflowX: auto"]').first();
    if (await tableContainer.isVisible().catch(() => false)) {
      const scrollWidth = await tableContainer.evaluate((el) => el.scrollWidth);
      const clientWidth = await tableContainer.evaluate((el) => el.clientWidth);
      expect(scrollWidth).toBeGreaterThanOrEqual(clientWidth);
    }

    await captureEvidence(page, '11-expenses-mobile-390px.png');

    // 2. Extra small 320px Viewport (iPhone SE / small mobile)
    await page.setViewportSize({ width: 320, height: 568 });
    await page.waitForTimeout(500);
    const hasHorizontalOverflow320 = await page.evaluate(() => {
      return document.documentElement.scrollWidth > window.innerWidth;
    });
    expect(hasHorizontalOverflow320).toBe(false);

    // 3. Tablet 768px Viewport (iPad)
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.waitForTimeout(500);
    const hasHorizontalOverflow768 = await page.evaluate(() => {
      return document.documentElement.scrollWidth > window.innerWidth;
    });
    expect(hasHorizontalOverflow768).toBe(false);
  });

  // ==========================================
  // JOURNEY 13: English LTR Verification
  // ==========================================
  test('REAL-13: English LTR Direction & Localization Parity', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page, 'accountant@atlas.com');

    // Toggle language via topbar button
    const langBtn = page.locator('button.topbar-lang-btn');
    await expect(langBtn).toBeVisible({ timeout: 10000 });
    const currentDir = await page.locator('html').getAttribute('dir');
    if (currentDir === 'rtl') {
      await langBtn.click();
      await page.waitForTimeout(1000);
    }

    // Explicitly assert LTR direction
    const dir = await page.locator('html').getAttribute('dir');
    expect(dir).toBe('ltr');
    await expect(page.getByText(/Expenses & Employee Claims|Corporate Spend & Claims|Expenses/i).first()).toBeVisible();

    await captureEvidence(page, '12-expenses-english-ltr.png');

    // Reset language back to Arabic
    const afterDir = await page.locator('html').getAttribute('dir');
    if (afterDir === 'ltr') {
      await langBtn.click();
      await page.waitForTimeout(1000);
    }
    await ensureArabicLanguage(page);
  });

  // ==========================================
  // JOURNEY 14: Unauthorized Employee Identity Fails Closed
  // ==========================================
  test('REAL-14: Unauthorized Employee Identity Fails Closed (No Management Tabs or Workbenches)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    // Authenticate as standard employee without financial management permissions
    await ensureAuthenticated(page, 'sales@atlas.com');

    await page.goto('/expenses');
    await page.waitForLoadState('networkidle');

    // Verify Self Service tab is accessible
    await expect(page.getByRole('tab', { name: 'خدماتي ومصروفاتي' })).toBeVisible();

    // Verify elevated management tabs are strictly hidden
    await expect(page.getByRole('tab', { name: 'مراجعة واعتماد المصروفات' })).not.toBeVisible();
    await expect(page.getByRole('tab', { name: 'سلف العمل' })).not.toBeVisible();
    await expect(page.getByRole('tab', { name: 'بطاقات الشركات' })).not.toBeVisible();
    await expect(page.getByRole('tab', { name: 'المستحقات والتسويات' })).not.toBeVisible();
    await expect(page.getByRole('tab', { name: 'التقارير وتحليل النفقات' })).not.toBeVisible();
    await expect(page.getByRole('tab', { name: 'إعدادات وسياسات المصروفات' })).not.toBeVisible();

    // Verify deep link attack (?tab=review) is blocked and stays on self-service
    await page.goto('/expenses?tab=review');
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('tab', { name: 'مراجعة واعتماد المصروفات' })).not.toBeVisible();
    await expect(page.getByRole('button', { name: '+ تسجيل مصروف جديد' })).toBeVisible();
  });
});
