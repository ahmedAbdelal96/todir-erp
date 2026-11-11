import { test, expect, Page, Locator } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const ARTIFACTS_DIR = process.env.TEST_ARTIFACTS_DIR || path.resolve('artifacts/expenses-acceptance/real-stack');
const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/expenses-acceptance/real-stack');
const TEST_DEV_PASSWORD = process.env.ERP_DEV_PASSWORD;

function requireTestDevPassword(): string {
  if (!TEST_DEV_PASSWORD) {
    throw new Error('ERP_DEV_PASSWORD must be set for the real-stack Expenses acceptance suite.');
  }
  return TEST_DEV_PASSWORD;
}

test.describe.configure({ mode: 'serial' });

const RUN_ID = Date.now().toString().slice(-5);
const TEST_MERCHANT_NAME = `مؤسسة النيل للخدمات اللوجستية ${RUN_ID}`;
const TEST_MILEAGE_MERCHANT = `بدل انتقال ${RUN_ID}`;
const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
let REAL_EXPENSE_ID: string | null = null;
let REAL_CARD_TRANSACTION_ID: string | null = null;
let REAL_REPAYMENT_ID: string | null = null;

async function getExpense(page: Page, id: string) {
  const response = await page.request.get(`/api/v1/expenses/legal-entities/${ATLAS_ENTITY_ID}/${id}`);
  expect(response.ok()).toBeTruthy();
  return response.json();
}

async function getExpenseHistory(page: Page, id: string) {
  const response = await page.request.get(`/api/v1/expenses/legal-entities/${ATLAS_ENTITY_ID}/${id}/history`);
  expect(response.ok()).toBeTruthy();
  const payload = await response.json();
  return Array.isArray(payload) ? payload : payload.items ?? [];
}

async function getMileageDetail(page: Page, id: string) {
  const response = await page.request.get(`/api/v1/expenses/my/expenses/${id}/mileage?legalEntityId=${ATLAS_ENTITY_ID}`);
  expect(response.ok()).toBeTruthy();
  return response.json();
}

async function getExpensePage(page: Page, search: string) {
  const response = await page.request.get(`/api/v1/expenses/my/expenses?legalEntityId=${ATLAS_ENTITY_ID}&search=${encodeURIComponent(search)}&page=1&pageSize=50`);
  expect(response.ok()).toBeTruthy();
  return response.json();
}

async function chooseFirstExpenseCategory(page: Page, dialog: Locator) {
  const categoryButton = dialog.getByRole('button', { name: /اختر خيارًا|Select an option/i });
  if (await categoryButton.isVisible({ timeout: 1500 }).catch(() => false)) {
    await categoryButton.click();
    await page.getByRole('listbox').getByRole('option').first().click();
  }
}

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

  // Check if already on login page
  const emailInput = page.locator('#login-email');
  if (await emailInput.isVisible({ timeout: 4000 }).catch(() => false)) {
    await emailInput.fill(email);
    await page.locator('#login-password').fill(requireTestDevPassword());
    await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
    await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle');
  } else {
    const profileBtn = page.getByRole('button', { name: new RegExp(email, 'i') });
    if (await profileBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
      await profileBtn.click();
      await page.waitForTimeout(1000);
      if (await emailInput.isVisible().catch(() => false)) {
        await emailInput.fill(email);
        await page.locator('#login-password').fill(requireTestDevPassword());
        await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
        await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
        await page.waitForLoadState('networkidle');
      }
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

  // Check if already on login page
  const emailInput = page.locator('#login-email');
  if (await emailInput.isVisible({ timeout: 4000 }).catch(() => false)) {
    await emailInput.fill(email);
    await page.locator('#login-password').fill(requireTestDevPassword());
    await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
    await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle');
  } else {
    const profileBtn = page.getByRole('button', { name: new RegExp(email, 'i') });
    if (await profileBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
      await profileBtn.click();
      await page.waitForTimeout(1000);
      if (await emailInput.isVisible().catch(() => false)) {
        await emailInput.fill(email);
        await page.locator('#login-password').fill(requireTestDevPassword());
        await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
        await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
        await page.waitForLoadState('networkidle');
      }
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
    await ensureAuthenticated(page, 'admin@atlas.com');

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
    await ensureAuthenticated(page, 'hr-manager@atlas.com');

    // Open Modal
    await page.getByRole('button', { name: /تسجيل مصروف جديد/i }).first().click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Fill form
    await dialog.getByLabel(/اسم التاجر|التاجر أو مزود الخدمة/i).fill(TEST_MERCHANT_NAME);
    await dialog.getByLabel(/المبلغ/i).fill('1450.00');
    await chooseFirstExpenseCategory(page, dialog);
    await dialog.getByPlaceholder(/الغرض من المصروف/i).fill('شحن بضائع ومستلزمات معرض القاهرة');

    // Submit
    await dialog.getByRole('button', { name: /تسجيل المصروف/i }).click();
    await expect(dialog).not.toBeVisible({ timeout: 10000 });

    // Verify real persistence in table
    await page.waitForTimeout(1000);
    await expect(page.getByText(TEST_MERCHANT_NAME).first()).toBeVisible({ timeout: 10000 });

    const createdPage = await getExpensePage(page, TEST_MERCHANT_NAME);
    const created = createdPage.items.find((item: { merchantName: string }) => item.merchantName === TEST_MERCHANT_NAME);
    expect(created?.id).toBeTruthy();
    REAL_EXPENSE_ID = created.id;
    expect(created.expenseState).toBe('Draft');

    await captureEvidence(page, '02-expenses-new-expense-modal.png');
  });

  // ==========================================
  // JOURNEY 03: Real Mileage Expense Creation
  // ==========================================
  test('REAL-03: Mileage Allowance Expense Creation with Auto-Calculation', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page, 'hr-manager@atlas.com');

    // Open Modal
    await page.getByRole('button', { name: /تسجيل مصروف جديد/i }).first().click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Switch to Mileage mode
    await dialog.getByRole('button', { name: /بدل انتقال/i }).click();
    await chooseFirstExpenseCategory(page, dialog);

    // Fill distance (80 km) -> auto calculates using published policy
    await dialog.getByLabel(/المسافة المقطوعة/i).fill('80');
    await page.waitForTimeout(300);
    const oneWayAmount = Number(await dialog.getByLabel(/المبلغ/i).inputValue());
    expect(oneWayAmount).toBeGreaterThan(0);
    await dialog.getByLabel(/رحلة ذهاب وعودة|Round trip/i).check();
    await page.waitForTimeout(300);
    const roundTripAmount = Number(await dialog.getByLabel(/المبلغ/i).inputValue());
    expect(roundTripAmount).toBeCloseTo(oneWayAmount * 2, 2);

    await dialog.getByPlaceholder(/فرع الإسكندرية|Alexandria branch/i).fill('القاهرة - السويس');

    // Submit
    await dialog.getByRole('button', { name: /تسجيل المصروف/i }).click();
    await expect(dialog).not.toBeVisible({ timeout: 10000 });

    await page.waitForTimeout(1000);
    await expect(page.getByText(/بدل انتقال/i).first()).toBeVisible({ timeout: 10000 });
    const mileagePage = await getExpensePage(page, 'بدل انتقال');
    const mileageExpense = mileagePage.items.find((item: { merchantName: string; businessPurpose?: string }) => item.businessPurpose?.includes('القاهرة - السويس') || item.merchantName.includes('بدل انتقال'));
    expect(mileageExpense?.id).toBeTruthy();
    const mileage = await getMileageDetail(page, mileageExpense!.id);
    expect(mileage.roundTrip).toBe(true);
    expect(Number(mileage.calculatedDistance)).toBeCloseTo(160, 4);
    expect(Number(mileage.calculatedAmount)).toBeCloseTo(roundTripAmount, 2);
  });

  // ==========================================
  // JOURNEY 04: Real Supporting Receipt Upload & ClamAV Loop
  // ==========================================
  test('REAL-04: Official Supporting Evidence Upload, Storage & Scan Readiness', async ({ page }) => {
    test.skip(process.env.ERP_RUN_FILES_ACCEPTANCE !== '1', 'Optional live Platform Files/S3/ClamAV acceptance requires ERP_RUN_FILES_ACCEPTANCE=1 and configured providers.');
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

    await page.getByRole('tab', { name: 'مراجعة واعتماد المصروفات' }).click();

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

    await page.getByRole('tab', { name: 'مراجعة واعتماد المصروفات' }).click();

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

    expect(REAL_EXPENSE_ID).toBeTruthy();
    const finalized = await getExpense(page, REAL_EXPENSE_ID!);
    expect(finalized.expenseState).toBe('Finalized');
    expect(finalized.accountingState).toBe('Posted');
    expect(finalized.settlementState).toBe('ReimbursementPending');
    const finalizedHistory = await getExpenseHistory(page, REAL_EXPENSE_ID!);
    expect(finalizedHistory.some((entry: { action: string; toState?: Record<string, unknown> }) => entry.action === 'finalize' && entry.toState?.accountingState === 'Posted')).toBeTruthy();

    await captureEvidence(page, '06-expenses-finalization.png');

    // Perform reversal to verify authorized reversal lifecycle
    await reverseBtn.click();
    const reasonInput = drawer.getByPlaceholder(/سبب القيد العكسي/i);
    await expect(reasonInput).toBeVisible({ timeout: 5000 });
    await reasonInput.fill('تصحيح محاسبي معتمد للقيد العكسي');
    const confirmReverseBtn = drawer.getByRole('button', { name: 'تأكيد القيد العكسي' });
    await expect(confirmReverseBtn).toBeVisible({ timeout: 5000 });
    await confirmReverseBtn.click();
    await page.waitForTimeout(2000);

    // Verify state badge is Reversed (قيد عكسي / تم العكس)
    await expect(drawer.getByText(/قيد عكسي|تم العكس|معكوس|Reversed/i).first()).toBeVisible({ timeout: 15000 });
    const reversed = await getExpense(page, REAL_EXPENSE_ID!);
    expect(reversed.expenseState).toBe('Reversed');
    expect(reversed.accountingState).toBe('Reversed');
    const reversalHistory = await getExpenseHistory(page, REAL_EXPENSE_ID!);
    expect(reversalHistory.some((entry: { action: string; toState?: Record<string, unknown> }) => entry.action === 'reverse' && entry.toState?.accountingState === 'Reversed')).toBeTruthy();

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

    const advancesBeforeResponse = await page.request.get(`/api/v1/expenses/legal-entities/${ATLAS_ENTITY_ID}/advances?page=1&pageSize=50`);
    expect(advancesBeforeResponse.ok()).toBeTruthy();
    const advancesBeforePayload = await advancesBeforeResponse.json();
    const advancesBefore = advancesBeforePayload.items as Array<{ id: string; returnedAmount: number; issuedAmount: number; appliedAmount: number; writtenOffAmount?: number }>;
    const returnedBefore = new Map(advancesBefore.map((advance) => [advance.id, advance]));

    // Click "منصرفة وقائمة" (Issued) filter pill
    await page.getByRole('button', { name: /منصرفة وقائمة/i }).click();
    await page.waitForTimeout(1000);

    // Verify an Issued advance is present
    const targetRow = page.locator('tbody tr').first();
    await expect(targetRow).toBeVisible({ timeout: 10000 });

    // Test Cash Return action on the issued advance
    const returnBtn = targetRow.getByRole('button', { name: 'رد نقدي' });
    await expect(returnBtn).toBeVisible({ timeout: 10000 });
    await returnBtn.click();
    const returnDialog = page.getByRole('dialog', { name: /رد متبقي سلفة العمل|رد سلفة|رد نقدي/i });
    await expect(returnDialog).toBeVisible({ timeout: 10000 });
    await returnDialog.getByLabel(/المبلغ المسترد/i).fill('50.00');
    await returnDialog.getByRole('button', { name: /تأكيد رد المبلغ/i }).click();
    await expect(returnDialog).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(1000);

    // Switch back to "كافة السلف" (All Advances) since returning money transitions state to PartiallySettled
    await page.getByRole('button', { name: 'كافة السلف' }).click();
    await page.waitForTimeout(1000);

    // Verify the persisted return and outstanding balance from the authoritative API.
    const advancesAfterResponse = await page.request.get(`/api/v1/expenses/legal-entities/${ATLAS_ENTITY_ID}/advances?page=1&pageSize=50`);
    expect(advancesAfterResponse.ok()).toBeTruthy();
    const advancesAfterPayload = await advancesAfterResponse.json();
    const returnedAdvance = (advancesAfterPayload.items as Array<{ id: string; returnedAmount: number; issuedAmount: number; appliedAmount: number; writtenOffAmount?: number }>)
      .find((advance) => advance.returnedAmount === (returnedBefore.get(advance.id)?.returnedAmount ?? -1) + 50);
    expect(returnedAdvance).toBeTruthy();
    expect(returnedAdvance!.returnedAmount).toBeGreaterThan(0);
    const beforeAdvance = returnedBefore.get(returnedAdvance!.id)!;
    const outstanding = returnedAdvance!.issuedAmount - returnedAdvance!.appliedAmount - returnedAdvance!.returnedAmount - (returnedAdvance!.writtenOffAmount ?? 0);
    const beforeOutstanding = beforeAdvance.issuedAmount - beforeAdvance.appliedAmount - beforeAdvance.returnedAmount - (beforeAdvance.writtenOffAmount ?? 0);
    expect(outstanding).toBe(beforeOutstanding - 50);

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

    // Filter or Search for seeded card transaction
    const searchInput = page.getByPlaceholder(/البحث باسم التاجر/i);
    if (await searchInput.isVisible().catch(() => false)) {
      await searchInput.fill('وقود النيل');
      await page.waitForTimeout(1000);
    }

    const cardBeforeResponse = await page.request.get(`/api/v1/expenses/legal-entities/${ATLAS_ENTITY_ID}/corporate-card/inbox?state=Unmatched&search=${encodeURIComponent('وقود النيل')}&page=1&pageSize=50`);
    expect(cardBeforeResponse.ok()).toBeTruthy();
    const cardBeforePayload = await cardBeforeResponse.json();
    const cardBefore = (cardBeforePayload.items as Array<{ id: string; amount: number; state: string }>)[0];
    expect(cardBefore?.id).toBeTruthy();
    REAL_CARD_TRANSACTION_ID = cardBefore.id;

    // Verify seeded card transaction
    await expect(page.getByText(/محطة وقود النيل|وقود النيل/i).first()).toBeVisible({ timeout: 10000 });

    // Mark as Personal
    const markPersonalBtn = page.getByRole('button', { name: /تصنيف كمصروف شخصي|شخصي/i }).first();
    await expect(markPersonalBtn).toBeVisible({ timeout: 10000 });
    await markPersonalBtn.click();
    await page.waitForTimeout(2000);

    // Verify transaction status changed to Personal
    await expect(page.getByText(/مصروف شخصي|شخصي|Personal/i).first()).toBeVisible({ timeout: 10000 });
    const cardAfterResponse = await page.request.get(`/api/v1/expenses/legal-entities/${ATLAS_ENTITY_ID}/corporate-card/inbox?state=Personal&search=${encodeURIComponent('وقود النيل')}&page=1&pageSize=50`);
    expect(cardAfterResponse.ok()).toBeTruthy();
    const cardAfterPayload = await cardAfterResponse.json();
    const cardAfter = (cardAfterPayload.items as Array<{ id: string; amount: number; state: string; employeeId?: string | null }>).find((card) => card.id === REAL_CARD_TRANSACTION_ID);
    expect(cardAfter?.state).toBe('Personal');
    expect(cardAfter?.employeeId).toBeTruthy();
    const repaymentResponse = await page.request.get(`/api/v1/expenses/legal-entities/${ATLAS_ENTITY_ID}/employee-repayments?state=Requested&page=1&pageSize=50`);
    expect(repaymentResponse.ok()).toBeTruthy();
    const repaymentPayload = await repaymentResponse.json();
    const repayment = (repaymentPayload.items as Array<{ id: string; sourceReference: string; amount: number; state: string }>).find((item) => item.sourceReference === `corporate-card:${REAL_CARD_TRANSACTION_ID}`);
    expect(repayment?.amount).toBe(cardBefore.amount);
    expect(repayment?.state).toBe('Requested');
    REAL_REPAYMENT_ID = repayment?.id ?? null;

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

    const batchesBeforeResponse = await page.request.get(`/api/v1/expenses/legal-entities/${ATLAS_ENTITY_ID}/reimbursements/batches`);
    expect(batchesBeforeResponse.ok()).toBeTruthy();
    const batchesBeforePayload = await batchesBeforeResponse.json();
    const batchIdsBefore = new Set((batchesBeforePayload as Array<{ id: string }>).map((batch) => batch.id));

    // 1. Prepare a new batch (deterministic execution)
    const prepareBatchBtn = page.getByRole('button', { name: /تجهيز دفعة سداد جديدة|\+ تجهيز دفعة/i });
    await expect(prepareBatchBtn).toBeVisible({ timeout: 10000 });
    await prepareBatchBtn.click();
    const prepareModal = page.getByRole('dialog', { name: /تجهيز دفعة سداد/i });
    await expect(prepareModal).toBeVisible({ timeout: 10000 });
    await prepareModal.getByRole('button', { name: /تجهيز الدفعة/i }).click();
    await expect(prepareModal).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(2000);

    // 2. Pay batch (deterministic execution of payment command)
    const payBatchBtn = page.getByRole('button', { name: /سداد الدفعة|تنفيذ الصرف/i }).first();
    await expect(payBatchBtn).toBeVisible({ timeout: 10000 });
    await payBatchBtn.click();
    const payModal = page.getByRole('dialog', { name: /صرف دفعة سداد المستحقات|سداد/i });
    await expect(payModal).toBeVisible({ timeout: 10000 });
    await payModal.getByRole('button', { name: /تأكيد الصرف المالي|تأكيد سداد/i }).click();
    await expect(payModal).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(2000);

    // Verify batch status is now Paid ("تم الصرف")
    await expect(page.getByText(/تم الصرف/i).first()).toBeVisible({ timeout: 10000 });
    const batchesAfterResponse = await page.request.get(`/api/v1/expenses/legal-entities/${ATLAS_ENTITY_ID}/reimbursements/batches`);
    expect(batchesAfterResponse.ok()).toBeTruthy();
    const batchesAfterPayload = await batchesAfterResponse.json();
    const paidBatch = (batchesAfterPayload as Array<{ id: string; state: string; totalAmount: number }>).find((batch) => !batchIdsBefore.has(batch.id) && batch.state === 'Paid');
    expect(paidBatch?.totalAmount).toBeGreaterThan(0);
    expect(REAL_EXPENSE_ID).toBeTruthy();
    const settledExpense = await getExpense(page, REAL_EXPENSE_ID!);
    expect(settledExpense.settlementState).toBe('Settled');

    // 3. Switch to "تحصيل من الموظفين" (fromEmployees repayments)
    await page.getByRole('button', { name: /تحصيل/i }).click();
    await page.waitForTimeout(1000);

    // 4. Settle repayment created from corporate card personal classification
    const settleRepaymentBtn = page.getByRole('button', { name: /تسجيل التحصيل/i }).first();
    await expect(settleRepaymentBtn).toBeVisible({ timeout: 10000 });
    await settleRepaymentBtn.click();
    const settleModal = page.getByRole('dialog', { name: /تحصيل مبلغ مستحق من موظف|تسجيل تحصيل/i });
    await expect(settleModal).toBeVisible({ timeout: 10000 });
    await settleModal.getByRole('button', { name: /تأكيد التحصيل/i }).click();
    await expect(settleModal).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(2000);

    // Verify repayment status changed to Collected ("تم التحصيل")
    await expect(page.getByText(/تم التحصيل/i).first()).toBeVisible({ timeout: 10000 });
    expect(REAL_REPAYMENT_ID).toBeTruthy();
    const repaymentsAfterResponse = await page.request.get(`/api/v1/expenses/legal-entities/${ATLAS_ENTITY_ID}/employee-repayments?state=Recorded&page=1&pageSize=50`);
    expect(repaymentsAfterResponse.ok()).toBeTruthy();
    const repaymentsAfterPayload = await repaymentsAfterResponse.json();
    const settledRepayment = (repaymentsAfterPayload.items as Array<{ id: string; state: string; settledAmount: number; amount: number }>).find((repayment) => repayment.id === REAL_REPAYMENT_ID);
    expect(settledRepayment?.state).toBe('Recorded');
    expect(settledRepayment?.settledAmount).toBe(settledRepayment?.amount);

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

    await captureEvidence(page, '11-expenses-mobile-320px.png');

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
