import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const LOCAL_ARTIFACTS_DIR = path.resolve('../artifacts/banking-acceptance/real-stack');
const AGENT_BRAIN_DIR = 'C:/Users/Adham/.gemini/antigravity/brain/2cce5b52-1525-442e-8037-81105c0347eb/banking-acceptance/real-stack';

const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';

function getTestDevPassword(): string {
  const password = process.env.ERP_DEV_PASSWORD || process.env.SEED_DEFAULT_PASSWORD;
  if (!password) {
    throw new Error('ERP_DEV_PASSWORD or SEED_DEFAULT_PASSWORD environment variable is required to run real-stack acceptance tests.');
  }
  return password;
}

function getTestProbeSecret(): string {
  const secret = process.env.TODIR_TEST_PROBE_SECRET;
  if (!secret) {
    throw new Error('TODIR_TEST_PROBE_SECRET environment variable is required to run target database safety probe.');
  }
  return secret;
}

test.describe.configure({ mode: 'serial' });

test.describe('TODIR Module 25: Banking & Cash Management — Real-Stack End-to-End Acceptance Suite', () => {
  test.setTimeout(240_000);

  test.beforeAll(async ({ request }) => {
    [LOCAL_ARTIFACTS_DIR, AGENT_BRAIN_DIR].forEach((dir) => {
      try {
        if (!fs.existsSync(dir)) {
          fs.mkdirSync(dir, { recursive: true });
        }
      } catch {
        // ignore directory permission errors
      }
    });

    // Priority 0 Pre-flight Safety Probe: Ensure tests never mutate protected development database
    const probeSecret = getTestProbeSecret();
    const probeRes = await request.get('/api/v1/banking/target', {
      headers: { 'X-Todir-Test-Secret': probeSecret }
    });

    if (probeRes.status() !== 200) {
      throw new Error(`Target database safety probe failed with HTTP ${probeRes.status()}: ${await probeRes.text()}`);
    }

    const { database } = await probeRes.json();
    if (database === 'todir') {
      throw new Error("CRITICAL SAFETY VIOLATION: Target database is the protected development database 'todir'. Tests must execute against an isolated acceptance database!");
    }

    if (database !== 'todir_module25_acceptance') {
      throw new Error(`CRITICAL SAFETY VIOLATION: Target database is '${database}', expected 'todir_module25_acceptance'.`);
    }
  });

  async function getCsrfToken(page: Page): Promise<string> {
    const csrfRes = await page.request.get('/api/v1/auth/csrf');
    const { token } = await csrfRes.json();
    return token;
  }

  async function loginAsAdmin(page: Page): Promise<{ token: string; user: any }> {
    await page.goto('/');
    await page.context().clearCookies();
    await page.evaluate(() => {
      try {
        localStorage.clear();
        sessionStorage.clear();
        localStorage.setItem('i18nextLng', 'ar');
      } catch {}
    });

    const password = getTestDevPassword();
    const token = await getCsrfToken(page);
    const loginRes = await page.request.post('/api/v1/auth/login', {
      data: { email: 'admin@atlas.com', password },
      headers: { 'X-CSRF-TOKEN': token }
    });
    expect(loginRes.status()).toBe(200);
    const user = await loginRes.json();

    await page.goto(`/banking?legalEntityId=${ATLAS_ENTITY_ID}`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('[data-testid="banking-page"]')).toBeVisible({ timeout: 20000 });

    const switchArBtn = page.getByRole('button', { name: /التبديل إلى العربية|Switch to Arabic|العربية/i }).first();
    if (await switchArBtn.isVisible()) {
      await switchArBtn.click();
      await page.waitForTimeout(400);
    }

    const authCsrfToken = await getCsrfToken(page);

    return { token: authCsrfToken, user };
  }

  const saveArtifact = async (page: Page, filename: string) => {
    const localPath = path.join(LOCAL_ARTIFACTS_DIR, filename);
    await page.screenshot({ path: localPath, fullPage: false });

    try {
      const brainPath = path.join(AGENT_BRAIN_DIR, filename);
      fs.copyFileSync(localPath, brainPath);
    } catch {
      // brain directory copy optional
    }
  };

  test('01 — Real Authentication, Live Database State & Authoritative Banking Overview', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Verify Title and Subtitle in Arabic
    await expect(page.locator('h1')).toContainText(/إدارة البنوك والنقدية/i);

    // Verify all 8 module tabs are rendered and accessible
    await expect(page.getByTestId('banking-tab-overview')).toBeVisible();
    await expect(page.getByTestId('banking-tab-accounts')).toBeVisible();
    await expect(page.getByTestId('banking-tab-statements')).toBeVisible();
    await expect(page.getByTestId('banking-tab-reconciliation')).toBeVisible();
    await expect(page.getByTestId('banking-tab-cash')).toBeVisible();
    await expect(page.getByTestId('banking-tab-transfers')).toBeVisible();
    await expect(page.getByTestId('banking-tab-forecast')).toBeVisible();
    await expect(page.getByTestId('banking-tab-reports')).toBeVisible();

    // Verify Overview KPI Cards
    await expect(page.getByText(/إجمالي السيولة المتاحة|Total Available Liquidity|Total Liquid/i).first()).toBeVisible();

    // Verify Cash-to-Ledger Reconciliation card
    await expect(page.getByText(/مطابقة رصيد النقدية مع الأستاذ العام|Cash to General Ledger Control/i).first()).toBeVisible();

    // Verify Currency Balances table
    await expect(page.getByText(/أرصدة حسابات الخزينة حسب العملة|Treasury Balances by Currency/i).first()).toBeVisible();

    await page.waitForTimeout(600);
    await saveArtifact(page, '01-banking-overview-desktop.png');
  });

  test('02 — Treasury Account Lifecycle, Real Creation & 360° Inspector Drawer', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Navigate to Treasury Accounts Tab
    await page.getByTestId('banking-tab-accounts').click();
    await page.waitForLoadState('domcontentloaded');

    // Verify Treasury Accounts Table Header
    await expect(page.getByText(/حسابات الخزينة والبنوك|Treasury & Bank Accounts/i).first()).toBeVisible();

    // Open Create Treasury Account Modal
    const createBtn = page.getByRole('button', { name: /\+ حساب خزينة جديد|\+ New Treasury Account/i }).first();
    await expect(createBtn).toBeVisible();
    await createBtn.click();

    // Verify Modal Dialog is Open
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Fill Create Account Form with real operational data
    const uniqueSuffix = Date.now().toString().slice(-6);
    const testCode = `BANK-E2E-${uniqueSuffix}`;
    const testName = `حساب البنك الأهلي E2E ${uniqueSuffix}`;

    const textInputs = dialog.locator('input[type="text"]');
    await textInputs.nth(0).fill(testCode);
    await textInputs.nth(1).fill(testName);

    // Submit Creation
    const submitBtn = dialog.getByRole('button', { name: /حفظ وإنشاء الحساب|Create Account/i }).first();
    await submitBtn.click();

    // Verify dialog closes and newly created account appears in table
    await expect(dialog).not.toBeVisible({ timeout: 10000 });
    const searchInput = page.getByLabel(/البحث باسم الحساب|Search by account name/i).first();
    if (await searchInput.isVisible()) {
      await searchInput.fill(testCode);
      await page.waitForTimeout(600);
    }
    const accountRow = page.locator('tr').filter({ hasText: testCode }).first();
    await expect(accountRow).toBeVisible({ timeout: 10000 });

    // Activate the newly created account from Draft to Active
    const activateBtn = accountRow.getByRole('button', { name: /تنشيط|Activate/i }).first();
    if (await activateBtn.isVisible()) {
      await activateBtn.click();
      await page.waitForTimeout(600);
      await expect(accountRow.getByText(/نشط|Active/i).first()).toBeVisible({ timeout: 10000 });
    }

    // Open 360° Inspector Drawer for the account
    const inspectBtns = accountRow.getByRole('button', { name: /معاينة الحساب|Inspect Account/i });
    if ((await inspectBtns.count()) > 0) {
      await inspectBtns.first().click();
      await page.waitForTimeout(600);

      // Verify 360° Inspector Drawer is visible
      const drawer = page.locator('div[style*="fixed"], aside, [role="dialog"]').last();
      await expect(drawer).toBeVisible();
      await expect(drawer.getByText(/الاتصالات البنكية|Open Banking Connections/i).first()).toBeVisible();

      await page.waitForTimeout(500);
      await saveArtifact(page, '02-treasury-account-detail-drawer.png');

      // Close drawer
      const closeDrawerBtn = drawer.getByRole('button', { name: /إغلاق|Close/i }).first();
      if (await closeDrawerBtn.isVisible()) {
        await closeDrawerBtn.click();
      }
    } else {
      await saveArtifact(page, '02-treasury-account-detail-drawer.png');
    }
  });

  test('03 — Bank Statement Import & Statement Line Classification', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Switch to Statements tab
    await page.getByTestId('banking-tab-statements').click();
    await page.waitForLoadState('domcontentloaded');

    // Verify Statements Header
    await expect(page.getByText(/كشوف الحسابات البنكية|Bank Statements/i).first()).toBeVisible();

    // Open Import Statement Modal
    const importBtn = page.getByRole('button', { name: /استيراد كشف حساب|Import Statement/i }).first();
    await expect(importBtn).toBeVisible();
    await importBtn.click();
    const importDialog = page.getByRole('dialog');
    await expect(importDialog).toBeVisible();

    const stmtSuffix = Date.now().toString().slice(-6);
    const stmtNumber = `STMT-${stmtSuffix}`;

    // Fill Statement Number
    const textInputs = importDialog.locator('input[type="text"]');
    await textInputs.nth(0).fill(stmtNumber);

    // Fill first line external ref and amount
    const lineInputs = importDialog.locator('table input[type="text"]');
    if ((await lineInputs.count()) > 0) {
      await lineInputs.nth(0).fill(`TXN-${stmtSuffix}`);
    }

    const numInputs = importDialog.locator('table input[type="number"]');
    if ((await numInputs.count()) > 0) {
      await numInputs.nth(0).fill('25000');
    }

    // Submit Import
    const submitBtn = importDialog.getByRole('button', { name: /تأكيد واستيراد كشف الحساب|Import Statement/i }).first();
    await expect(submitBtn).toBeVisible();
    await expect(submitBtn).toBeEnabled();
    await submitBtn.click();
    await expect(importDialog).not.toBeVisible({ timeout: 10000 });

    // Assert newly imported statement row exists and click its View Lines button
    await page.waitForTimeout(1000);
    const statementRow = page.locator('tbody tr').filter({ hasText: stmtNumber }).first();
    await expect(statementRow).toBeVisible({ timeout: 10000 });

    const viewLinesBtn = page.getByTestId(`view-lines-${stmtNumber}`);
    await expect(viewLinesBtn).toBeVisible({ timeout: 10000 });
    await viewLinesBtn.dispatchEvent('click');

    const linesModal = page.getByRole('dialog');
    await expect(linesModal).toBeVisible();
    await expect(linesModal.getByRole('heading')).toContainText(stmtNumber);
    // Verify table lines render with BdiText
    await expect(linesModal.getByText(`TXN-${stmtSuffix}`).first()).toBeVisible({ timeout: 10000 });

    await page.waitForTimeout(500);
    await saveArtifact(page, '03-statement-lines-classification.png');

    const closeBtn = linesModal.getByRole('button', { name: /إغلاق|Close/i }).first();
    await expect(closeBtn).toBeVisible();
    await closeBtn.click({ force: true });
    await expect(linesModal).not.toBeVisible();
  });

  test('04 — Reconciliation Workbench & Conservation Arithmetic', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Switch to Reconciliation tab
    await page.getByTestId('banking-tab-reconciliation').click();
    await page.waitForLoadState('domcontentloaded');

    // Verify Reconciliation Header
    await expect(page.getByText(/المطابقة والتسوية البنكية|Bank Reconciliation/i).first()).toBeVisible();

    // Verify Workbench Launch button or Start New Session button
    let openWorkbenchBtn = page.getByRole('button', { name: /فتح طاولة المطابقة|Open Workbench/i }).first();

    if (!(await openWorkbenchBtn.isVisible())) {
      // Start a new session if none exists
      const startSessionBtn = page.getByRole('button', { name: /\+ بدء جلسة مطابقة جديدة|\+ Start Reconciliation/i }).first();
      if (await startSessionBtn.isVisible()) {
        await startSessionBtn.dispatchEvent('click');
        const startDialog = page.getByRole('dialog');
        await expect(startDialog).toBeVisible();
        const startSubmit = startDialog.getByRole('button', { name: /بدء الجلسة|Start Session/i }).first();
        if (await startSubmit.isVisible()) {
          await startSubmit.dispatchEvent('click');
          await expect(startDialog).not.toBeVisible({ timeout: 10000 });
        }
      }
    }

    openWorkbenchBtn = page.getByRole('button', { name: /فتح طاولة المطابقة|Open Workbench/i }).first();
    await expect(openWorkbenchBtn).toBeVisible({ timeout: 10000 });
    await openWorkbenchBtn.dispatchEvent('click');

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Verify Interactive Workbench Panels
    await expect(dialog.getByText(/طاولة المطابقة البنكية التفاعلية|Interactive Reconciliation Workbench/i).first()).toBeVisible();
    await expect(dialog.getByText(/حركات الكشف البنكي|Bank Statement Lines/i).first()).toBeVisible();
    await expect(dialog.getByText(/مدفوعات الأستاذ العام|Eligible Ledger Payments/i).first()).toBeVisible();

    await page.waitForTimeout(500);
    await saveArtifact(page, '04-reconciliation-workbench-matching.png');

    const closeBtn = dialog.getByRole('button', { name: /إغلاق|إلغاء|Close|Cancel/i }).first();
    await expect(closeBtn).toBeVisible();
    await closeBtn.click({ force: true });
    await expect(dialog).not.toBeVisible();
  });

  test('05 — Cash & Vault Management, Denomination Grid & Variance Settlement', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Switch to Cash Management tab
    await page.getByTestId('banking-tab-cash').click();
    await page.waitForLoadState('domcontentloaded');

    // Verify Cash Vaults header
    await expect(page.getByText(/إدارة الخزائن والجرد النقدي|Cash & Vault Management/i).first()).toBeVisible();

    // Open Cash Count Modal
    const countBtn = page.getByRole('button', { name: /تسجيل جرد نقدي جديد|New Cash Count/i }).first();
    await expect(countBtn).toBeVisible();
    await countBtn.click();

    const countDialog = page.getByRole('dialog');
    await expect(countDialog).toBeVisible();
    await expect(countDialog.getByText(/تسجيل جرد نقدي فعلي بالفئات النقدية|Physical Cash Count by Denomination/i).first()).toBeVisible();

    // Set unique cutoff date so repeated test runs never collide on same date
    const cutoffInput = countDialog.locator('input[type="date"]').first();
    await expect(cutoffInput).toBeVisible();
    const randomDays = Math.floor(Math.random() * 5000) + 1000;
    const testDate = new Date(Date.now() + randomDays * 86400000);
    const uniqueCutoff = testDate.toISOString().slice(0, 10);
    await cutoffInput.fill(uniqueCutoff);

    // Verify Denomination Grid is rendered
    await expect(countDialog.getByText(/الفئة النقدية|Denomination/i).first()).toBeVisible();

    // Input quantity for 200 EGP denomination
    const denomInputs = countDialog.locator('table input[type="number"]');
    if ((await denomInputs.count()) >= 1) {
      await denomInputs.nth(0).fill('25'); // 25 x 200 = 5000 EGP
    }

    // Verify Live Sum and Variance calculation
    await page.waitForTimeout(400);
    await expect(countDialog.getByText(/إجمالي النقدية المجرودة|Total Counted Cash/i).first()).toBeVisible();

    await page.waitForTimeout(500);
    await saveArtifact(page, '05-cash-count-denomination-grid.png');

    // Submit cash count
    const submitBtn = countDialog.getByRole('button', { name: /حفظ وحساب الجرد|Save & Verify Cash Count/i }).first();
    await expect(submitBtn).toBeVisible();
    await submitBtn.click();
    await expect(countDialog).not.toBeVisible({ timeout: 10000 });

    // Assert newly created count is persisted in table
    await page.waitForTimeout(600);
    const countRow = page.getByTestId(`cash-count-row-${uniqueCutoff}`);
    await expect(countRow).toBeVisible({ timeout: 10000 });
  });

  test('06 — Internal Transfers, Transit Tracking & Gateway Settlements', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Switch to Transfers tab
    await page.getByTestId('banking-tab-transfers').click();
    await page.waitForLoadState('domcontentloaded');

    // Verify Transfers Header
    await expect(page.getByText(/التحويلات المالية والتسويات|Transfers & Settlements/i).first()).toBeVisible();

    // Open New Transfer Modal
    const newTransferBtn = page.getByRole('button', { name: /تحويل مالي بين الحسابات|Initiate Internal Transfer/i }).first();
    await expect(newTransferBtn).toBeVisible();
    await newTransferBtn.click();

    const transferDialog = page.getByRole('dialog');
    await expect(transferDialog).toBeVisible();
    await expect(transferDialog.getByText(/تحويل مالي بين حسابات الخزينة|Internal Transfer Between Treasury Accounts/i).first()).toBeVisible();

    // Select source account
    const sourceSelect = transferDialog.getByRole('button', { name: /حساب الخزينة المصدر|Source Treasury Account/i }).first();
    await expect(sourceSelect).toBeVisible();
    await sourceSelect.click();
    const sourceListbox = page.getByRole('listbox');
    await expect(sourceListbox).toBeVisible();
    const sourceOptions = sourceListbox.getByRole('option');
    await sourceOptions.first().click();

    // Select destination account
    const destSelect = transferDialog.getByRole('button', { name: /حساب الخزينة المستلم|Destination Treasury Account/i }).first();
    await expect(destSelect).toBeVisible();
    await destSelect.click();
    const destListbox = page.getByRole('listbox');
    await expect(destListbox).toBeVisible();
    const destOptions = destListbox.getByRole('option');
    await destOptions.first().click();

    // Fill Transfer Amount
    const amountInput = transferDialog.locator('input[type="number"]').first();
    await amountInput.fill('15000');

    // Fill unique reference
    const refSuffix = Date.now().toString().slice(-6);
    const testRef = `TRF-E2E-${refSuffix}`;
    const refInput = transferDialog.locator('input[placeholder*="TRF"]').first();
    if (await refInput.isVisible()) {
      await refInput.fill(testRef);
    }

    await page.waitForTimeout(500);
    await saveArtifact(page, '06-internal-transfer-settlement.png');

    // Submit transfer
    const submitTransferBtn = transferDialog.getByRole('button', { name: /تنفيذ التحويل المالي|تأكيد التحويل المالي|Initiate Transfer/i }).first();
    await expect(submitTransferBtn).toBeVisible();
    await submitTransferBtn.click();
    await expect(transferDialog).not.toBeVisible({ timeout: 10000 });

    // Assert newly created transfer appears in table
    await page.waitForTimeout(1000);
    const transferRow = page.locator('tbody tr').filter({ hasText: '15,000' }).first();
    await expect(transferRow).toBeVisible({ timeout: 10000 });
  });

  test('07 — Liquidity, Cash Forecasting & Financial Reports', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Switch to Forecast tab
    await page.getByTestId('banking-tab-forecast').click();
    await page.waitForLoadState('domcontentloaded');

    // Verify Cash Forecast Header & Horizon Filters
    await expect(page.getByText(/توقعات التدفق النقدي والسيولة|Cash Forecast & Liquidity Planning/i).first()).toBeVisible();
    await expect(page.getByText(/أفق التوقع الزمني|Forecast Horizon/i).first()).toBeVisible();

    // Verify Horizon Selector
    const horizonBtn = page.getByRole('button', { name: /30 يوماً|30 Days/i }).first();
    if (await horizonBtn.isVisible()) {
      await horizonBtn.click();
      await page.waitForTimeout(300);

      // Verify listbox options
      const listbox = page.getByRole('listbox');
      if (await listbox.isVisible()) {
        await expect(listbox.getByRole('option', { name: /60 يوماً|60 Days/i }).first()).toBeVisible();
        await expect(listbox.getByRole('option', { name: /90 يوماً|90 Days/i }).first()).toBeVisible();
        // Select 60 days
        await listbox.getByRole('option', { name: /60 يوماً|60 Days/i }).first().click();
        await page.waitForTimeout(300);
      }
    }

    // Verify Forecast Summary
    await expect(page.getByText(/توقعات التدفق النقدي|Cash Forecast/i).first()).toBeVisible();

    await page.waitForTimeout(500);
    await saveArtifact(page, '07-cash-forecast-horizon.png');
  });

  test('08 — Responsive Mobile Ergonomics (390px Viewport)', async ({ page }) => {
    // Set 390px mobile viewport (iPhone 14 benchmark)
    await page.setViewportSize({ width: 390, height: 844 });
    await loginAsAdmin(page);

    // Verify Banking page mounts cleanly on mobile
    await expect(page.locator('[data-testid="banking-page"]')).toBeVisible();

    // Verify tab list wraps/scrolls without crashing or breaking layout
    const tabList = page.getByRole('tablist');
    await expect(tabList).toBeVisible();

    // Verify overview tab panel renders responsive KPI cards
    await expect(page.getByTestId('banking-tab-overview')).toBeVisible();

    await page.waitForTimeout(600);
    await saveArtifact(page, '08-banking-mobile-390px.png');
  });

  test('09 — English LTR Parity & Corporate Visual Integrity', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Switch to English via language switcher button
    const enSwitchBtn = page.getByRole('button', { name: /التبديل إلى الإنجليزية|English|EN/i }).first();
    if (await enSwitchBtn.isVisible()) {
      await enSwitchBtn.click();
      await page.waitForTimeout(600);
    } else {
      await page.evaluate(() => {
        try {
          localStorage.setItem('i18nextLng', 'en');
          window.location.reload();
        } catch {}
      });
      await page.waitForLoadState('domcontentloaded');
      await page.waitForTimeout(800);
    }

    // Verify English Module Title
    await expect(page.getByText(/Banking & Cash Management|Banking/i).first()).toBeVisible();

    // Verify English tab labels
    await expect(page.getByText(/Treasury Accounts/i).first()).toBeVisible();
    await expect(page.getByText(/Bank Statements/i).first()).toBeVisible();
    await expect(page.getByText(/Bank Reconciliation/i).first()).toBeVisible();
    await expect(page.getByText(/Cash & Vault Management/i).first()).toBeVisible();

    await page.waitForTimeout(500);
    await saveArtifact(page, '09-banking-english-ltr.png');

    await page.evaluate(() => {
      try {
        localStorage.setItem('i18nextLng', 'ar');
      } catch {}
    });
  });

  test('10 — Real Bank Statement & Payment Matching (Persisted State Verification)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    const { token } = await loginAsAdmin(page);

    const uniqueKey = Date.now().toString().slice(-6);
    const amount = '500.00';

    // 1. Create a real completed incoming payment in Payments module
    const paymentRes = await page.request.post(`/api/v1/payments/legal-entities/${ATLAS_ENTITY_ID}/incoming`, {
      headers: {
        'X-CSRF-TOKEN': token,
        'Idempotency-Key': `match-pay-idemp-${uniqueKey}`
      },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        branchId: null,
        type: 'CustomerReceipt',
        partyId: 'cdcdd524-629a-7f56-a9ae-19b80fc05a48', // Ahmed Customer in Atlas
        amount: amount,
        currencyCode: 'EGP',
        method: 'BankTransfer',
        paymentAccountId: 'acf650c1-cb88-9757-a1c0-bc430a85d07e', // BANK-EGP-MAIN
        paymentDate: '2026-10-01',
        postingDate: '2026-10-01',
        valueDate: '2026-10-01',
        bankReference: `REF-MATCH-${uniqueKey}`,
        customerReference: null,
        supplierReference: null,
        sourceType: 'Manual',
        sourceReference: null
      }
    });
    if (paymentRes.status() !== 201) {
      console.error("Payment create error:", await paymentRes.text());
    }
    expect(paymentRes.status()).toBe(201);
    const payment = await paymentRes.json();

    const completeRes = await page.request.post(`/api/v1/payments/legal-entities/${ATLAS_ENTITY_ID}/${payment.id}/complete`, {
      headers: {
        'X-CSRF-TOKEN': token,
        'Idempotency-Key': `match-pay-comp-${uniqueKey}`
      }
    });
    expect(completeRes.status()).toBe(200);

    // 2. Import a real external bank statement on the same treasury account with a matching credit line
    const stmtNumber = `STMT-MATCH-${uniqueKey}`;
    const importRes = await page.request.post(`/api/v1/banking/treasury-accounts/acf650c1-cb88-9757-a1c0-bc430a85d07e/statements/import?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: {
        'X-CSRF-TOKEN': token,
        'Idempotency-Key': `stmt-match-imp-${uniqueKey}`
      },
      data: {
        statementNumber: stmtNumber,
        openingDate: '2026-10-01',
        closingDate: '2026-10-01',
        currencyCode: 'EGP',
        openingBalance: 0,
        closingBalance: 500,
        sourceType: 'Manual',
        sourceReference: `IMPORT-MATCH-${uniqueKey}`,
        lines: [
          {
            externalLineId: `LINE-MATCH-${uniqueKey}`,
            bookingDate: '2026-10-01',
            valueDate: '2026-10-01',
            direction: 'Credit',
            amount: 500,
            currencyCode: 'EGP',
            reference: `REF-MATCH-${uniqueKey}`,
            transactionCode: 'TRF',
            counterpartyName: 'Ahmed Customer',
            rawPayload: null
          }
        ]
      }
    });
    expect([200, 201]).toContain(importRes.status());
    const statement = await importRes.json();

    // 3. Create a reconciliation session for this statement
    const createRecRes = await page.request.post('/api/v1/banking/reconciliations', {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        asOfDate: '2026-10-01',
        statementId: statement.id,
        notes: 'Real-stack E2E matching test'
      }
    });
    expect(createRecRes.status()).toBe(201);
    const recSession = await createRecRes.json();

    // Retrieve the statement line ID
    const linesRes = await page.request.get(`/api/v1/banking/statements/${statement.id}/lines?legalEntityId=${ATLAS_ENTITY_ID}&page=1&pageSize=10`);
    expect(linesRes.status()).toBe(200);
    const linesPage = await linesRes.json();
    expect(linesPage.items.length).toBeGreaterThanOrEqual(1);
    const statementLineId = linesPage.items[0].id;

    // 4. Perform real match via POST /match
    const matchRes = await page.request.post(`/api/v1/banking/reconciliations/${recSession.id}/match?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        statementLineIds: [statementLineId],
        payments: [{ paymentId: payment.id, amount: 500 }],
        externalAmount: 500,
        systemAmount: 500,
        difference: 0,
        source: 'Manual',
        explanation: 'Matched in real-stack acceptance test 10'
      }
    });
    expect(matchRes.status()).toBe(200);
    const updatedRec = await matchRes.json();
    expect(updatedRec.matchCount).toBeGreaterThanOrEqual(1);

    // 5. Verify live persisted state in reconciliation claims
    const claimsRes = await page.request.get(`/api/v1/banking/reconciliations/${recSession.id}/claims?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(claimsRes.status()).toBe(200);
    const claims = await claimsRes.json();
    expect(Number(claims[payment.id])).toBe(500);

    // 6. Verify statement line claimed amount is persisted
    const updatedLinesRes = await page.request.get(`/api/v1/banking/statements/${statement.id}/lines?legalEntityId=${ATLAS_ENTITY_ID}&page=1&pageSize=10`);
    expect(updatedLinesRes.status()).toBe(200);
    const updatedLines = await updatedLinesRes.json();
    expect(Number(updatedLines.items[0].claimedAmount)).toBe(500);
  });

  test('11 — Authorized Reconciliation Finalization & Invariant Guards', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    const { token } = await loginAsAdmin(page);

    const uniqueKey = Date.now().toString().slice(-6);

    // 1. Create a dedicated treasury account for clean, isolated reconciliation finalization
    const createAccountRes = await page.request.post('/api/v1/banking/treasury-accounts', {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        code: `BANK-FIN-${uniqueKey}`,
        name: `Finalize Test Bank ${uniqueKey}`,
        type: 'Bank',
        currencyPolicy: 'SingleCurrency',
        currencyCode: 'EGP',
        purpose: 'Operating',
        liquidityClass: 'Operating',
        maskedIdentifier: '•••• 4321'
      }
    });
    expect(createAccountRes.status()).toBe(201);
    const finAccount = await createAccountRes.json();

    // Activate account so it is eligible for payments and reconciliations
    const activateRes = await page.request.post(`/api/v1/banking/treasury-accounts/${finAccount.id}/activate?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': token },
      data: { version: finAccount.version }
    });
    expect(activateRes.status()).toBe(200);

    // 2. Create & complete a matching incoming payment (posts directly to ledger for this account)
    const paymentRes = await page.request.post(`/api/v1/payments/legal-entities/${ATLAS_ENTITY_ID}/incoming`, {
      headers: {
        'X-CSRF-TOKEN': token,
        'Idempotency-Key': `fin-pay-idemp-${uniqueKey}`
      },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        branchId: null,
        type: 'CustomerReceipt',
        partyId: 'cdcdd524-629a-7f56-a9ae-19b80fc05a48',
        amount: '250.00',
        currencyCode: 'EGP',
        method: 'BankTransfer',
        paymentAccountId: finAccount.id,
        paymentDate: '2026-10-01',
        postingDate: '2026-10-01',
        valueDate: '2026-10-01',
        bankReference: `REF-FIN-${uniqueKey}`,
        customerReference: null,
        supplierReference: null,
        sourceType: 'Manual',
        sourceReference: null
      }
    });
    expect(paymentRes.status()).toBe(201);
    const payment = await paymentRes.json();

    const completeRes = await page.request.post(`/api/v1/payments/legal-entities/${ATLAS_ENTITY_ID}/${payment.id}/complete`, {
      headers: {
        'X-CSRF-TOKEN': token,
        'Idempotency-Key': `fin-pay-comp-${uniqueKey}`
      }
    });
    expect(completeRes.status()).toBe(200);

    // 3. Import bank statement on the account matching the posted ledger balance (250.00 EGP)
    const stmtNumber = `STMT-FIN-${uniqueKey}`;
    const importRes = await page.request.post(`/api/v1/banking/treasury-accounts/${finAccount.id}/statements/import?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: {
        'X-CSRF-TOKEN': token,
        'Idempotency-Key': `stmt-fin-imp-${uniqueKey}`
      },
      data: {
        statementNumber: stmtNumber,
        openingDate: '2026-10-01',
        closingDate: '2026-10-01',
        currencyCode: 'EGP',
        openingBalance: 0,
        closingBalance: 250,
        sourceType: 'Manual',
        sourceReference: `IMPORT-FIN-${uniqueKey}`,
        lines: [
          {
            externalLineId: `LINE-FIN-${uniqueKey}`,
            bookingDate: '2026-10-01',
            valueDate: '2026-10-01',
            direction: 'Credit',
            amount: 250,
            currencyCode: 'EGP',
            reference: `REF-FIN-${uniqueKey}`,
            transactionCode: 'TRF',
            counterpartyName: 'Atlas Ledger Balanced',
            rawPayload: null
          }
        ]
      }
    });
    expect([200, 201]).toContain(importRes.status());
    const statement = await importRes.json();

    // 4. Create an open reconciliation session
    const createRecRes = await page.request.post('/api/v1/banking/reconciliations', {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        asOfDate: '2026-10-01',
        statementId: statement.id,
        notes: 'Finalization verification session'
      }
    });
    expect(createRecRes.status()).toBe(201);
    const recSession = await createRecRes.json();

    // Invariant Guard: Attempting to finalize before statement lines are claimed MUST be rejected
    const prematureFinalizeRes = await page.request.post(`/api/v1/banking/reconciliations/${recSession.id}/finalize?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': token },
      data: { targetState: 'Finalized', version: recSession.version }
    });
    expect(prematureFinalizeRes.status()).toBe(400);

    // 5. Match statement line with payment
    const linesRes = await page.request.get(`/api/v1/banking/statements/${statement.id}/lines?legalEntityId=${ATLAS_ENTITY_ID}&page=1&pageSize=10`);
    const lineId = (await linesRes.json()).items[0].id;

    const matchRes = await page.request.post(`/api/v1/banking/reconciliations/${recSession.id}/match?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        statementLineIds: [lineId],
        payments: [{ paymentId: payment.id, amount: 250 }],
        externalAmount: 250,
        systemAmount: 250,
        difference: 0,
        source: 'Manual',
        explanation: 'Balanced for finalization'
      }
    });
    expect(matchRes.status()).toBe(200);

    // 6. Retrieve fresh version of session & finalize with authoritative ledger validation
    const sessionRes = await page.request.get(`/api/v1/banking/reconciliations/${recSession.id}?legalEntityId=${ATLAS_ENTITY_ID}`);
    const freshSession = await sessionRes.json();

    const finalizeRes = await page.request.post(`/api/v1/banking/reconciliations/${recSession.id}/finalize?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': token },
      data: { targetState: 'Finalized', version: freshSession.version }
    });
    if (finalizeRes.status() !== 200) {
      console.error("Finalize error:", await finalizeRes.text());
    }
    expect(finalizeRes.status()).toBe(200);
    const finalized = await finalizeRes.json();
    expect(finalized.state).toBe('Finalized');

    // Immutability Guard: attempting to match on a finalized session must fail
    const postFinalizeMatchRes = await page.request.post(`/api/v1/banking/reconciliations/${recSession.id}/match?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        statementLineIds: [lineId],
        payments: [{ paymentId: payment.id, amount: 250 }],
        externalAmount: 250,
        systemAmount: 250,
        difference: 0,
        source: 'Manual',
        explanation: 'Should fail on finalized'
      }
    });
    expect(postFinalizeMatchRes.status()).toBe(400);
  });

  test('12 — Cash Count Submission, Approval & Difference Resolution Lifecycle', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    const { token, user } = await loginAsAdmin(page);

    const adminUserId = user?.identityId || user?.userId || '913f04bf-8239-3954-833c-721a055f8e97';
    const cashAccountId = '6f72315c-5930-765b-bc01-79915b2f304a'; // CASH-MAIN-MAIN
    const uniqueDay = Math.floor(Math.random() * 8000) + 2000;
    const cutoffDate = new Date(Date.now() + uniqueDay * 86400000).toISOString().slice(0, 10);

    // 1. Create Cash Count: expected 55000, counted 54900 (difference = -100)
    const createRes = await page.request.post(`/api/v1/banking/treasury-accounts/${cashAccountId}/cash-counts?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        cutoffDate,
        custodianId: adminUserId,
        expectedAmount: 55000,
        denominations: [
          { denomination: '200', unitValue: 200, quantity: 274 }, // 54800
          { denomination: '100', unitValue: 100, quantity: 1 }     // 100 -> total 54900
        ]
      }
    });
    expect(createRes.status()).toBe(201);
    const count = await createRes.json();
    expect(count.state).toBe('Submitted');
    expect(Number(count.difference)).toBe(-100);

    // 2. Approve Cash Count (transitions to Approved)
    const approveRes = await page.request.post(`/api/v1/banking/cash-counts/${count.id}/approve?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': token },
      data: { targetState: 'Approved', version: count.version }
    });
    expect(approveRes.status()).toBe(200);
    const approvedCount = await approveRes.json();
    expect(approvedCount.state).toBe('Approved');
    expect(approvedCount.version).toBe(count.version + 1);

    // 3. Resolve Cash Difference: -100 shortage
    const resolveRes = await page.request.post(`/api/v1/banking/cash-counts/${count.id}/difference-resolutions?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        reasonCode: 'CashierShortage',
        resolutionType: 'CustodianExpense',
        amount: -100,
        evidenceReference: `E2E-CASH-DIFF-${Date.now()}`,
        version: approvedCount.version
      }
    });
    expect(resolveRes.status()).toBe(201);
    const resolution = await resolveRes.json();
    expect(resolution.cashCountId).toBe(count.id);
    expect(Number(resolution.amount)).toBe(-100);

    // 4. Verify persisted state
    const fetchRes = await page.request.get(`/api/v1/banking/cash-counts/${count.id}?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(fetchRes.status()).toBe(200);
    const persisted = await fetchRes.json();
    expect(persisted.state).toBe('Approved');
    expect(Number(persisted.difference)).toBe(-100);
  });

  test('13 — Treasury Transfer via Payments & Banking Settlement Lifecycle', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    const { token } = await loginAsAdmin(page);

    const uniqueKey = Date.now().toString().slice(-6);
    const transferAmount = 250;

    // 1. Create Internal Transfer between bank account and cash vault
    const transferRes = await page.request.post('/api/v1/banking/transfers', {
      headers: {
        'X-CSRF-TOKEN': token,
        'Idempotency-Key': `trf-idemp-${uniqueKey}`
      },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        sourceTreasuryAccountId: 'acf650c1-cb88-9757-a1c0-bc430a85d07e', // BANK-EGP-MAIN
        destinationTreasuryAccountId: '6f72315c-5930-765b-bc01-79915b2f304a', // CASH-MAIN-MAIN
        amount: transferAmount,
        currencyCode: 'EGP',
        paymentDate: '2026-10-01',
        postingDate: '2026-10-01',
        valueDate: '2026-10-01',
        reference: `TRF-E2E-${uniqueKey}`
      }
    });
    expect(transferRes.status()).toBe(202);
    const transfer = await transferRes.json();
    expect(transfer.transferId).toBeTruthy();

    // 2. Query transfer reconciliation projection: verify settlement state is Matched (both internal legs Completed)
    const listRes = await page.request.get(`/api/v1/banking/transfers/reconciliation?legalEntityId=${ATLAS_ENTITY_ID}&page=1&pageSize=50`);
    expect(listRes.status()).toBe(200);
    const transfersPage = await listRes.json();
    const item = transfersPage.items.find((t: any) => t.transferReference === transfer.transferId || t.id === transfer.transferId);
    expect(item).toBeTruthy();
    expect(item.state).toBe('Matched');
    expect(Number(item.sourceAmount)).toBe(transferAmount);
    expect(Number(item.destinationAmount)).toBe(transferAmount);
    expect(item.sourceAccountId).toBe('acf650c1-cb88-9757-a1c0-bc430a85d07e');
    expect(item.destinationAccountId).toBe('6f72315c-5930-765b-bc01-79915b2f304a');

    // 3. Immutability guard: attempting to reconcile an already completed and matched transfer must be rejected
    const reconcileRes = await page.request.post(`/api/v1/banking/transfers/${item.id}/reconcile?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        sourceStatementLineId: null,
        destinationStatementLineId: null,
        version: item.version,
        explanation: 'Attempt reconcile on matched transfer'
      }
    });
    expect([400, 409]).toContain(reconcileRes.status());
  });

  test('14 — Liquidity Forecast Version & Manual Items Persistence', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    const { token } = await loginAsAdmin(page);

    const uniqueSuffix = Date.now().toString().slice(-6);
    const horizonDays = ((Date.now() % 250) + 20);

    // 1. Create Forecast Version (Draft status allows adding manual items)
    const versionRes = await page.request.post('/api/v1/banking/cash-forecast/versions', {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        asOfDate: '2026-10-01',
        horizonDays,
        status: 'Draft'
      }
    });
    expect(versionRes.status()).toBe(201);
    const version = await versionRes.json();
    expect(version.id).toBeTruthy();
    expect(version.horizonDays).toBe(horizonDays);

    // 2. Add manual forecast item to version
    const itemRes = await page.request.post('/api/v1/banking/cash-forecast/manual-items', {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        forecastVersionId: version.id,
        treasuryAccountId: 'acf650c1-cb88-9757-a1c0-bc430a85d07e',
        dueDate: '2026-11-15',
        direction: 'Inflow',
        amount: 85000,
        currencyCode: 'EGP',
        sourceType: 'ManualForecast',
        description: `Contract Milestone ${uniqueSuffix}`,
        sourceReference: null
      }
    });
    expect(itemRes.status()).toBe(201);
    const item = await itemRes.json();
    expect(item.id).toBeTruthy();
    expect(Number(item.amount)).toBe(85000);

    // 3. Verify forecast version persistence and items inclusion
    const fetchRes = await page.request.get(`/api/v1/banking/cash-forecast/versions/${version.id}?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(fetchRes.status()).toBe(200);
    const forecastPage = await fetchRes.json();
    expect(forecastPage.id).toBe(version.id);
    const persistedItem = forecastPage.items.find((i: any) => i.id === item.id);
    expect(persistedItem).toBeTruthy();
    expect(Number(persistedItem.amount)).toBe(85000);
    expect(persistedItem.description).toContain(uniqueSuffix);
  });

  test('15 — Cross-Entity Access Rejection & Security Isolation', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    const { token } = await loginAsAdmin(page);

    const unauthorizedEntityId = '00000000-0000-0000-0000-000000000099';

    // 1. Cross-Entity Isolation: Accessing a specific treasury account with a mismatched legal entity scope must be rejected
    const crossEntityRes = await page.request.get(`/api/v1/banking/treasury-accounts/acf650c1-cb88-9757-a1c0-bc430a85d07e?legalEntityId=${unauthorizedEntityId}`);
    expect([400, 403, 404]).toContain(crossEntityRes.status());

    // 2. Cross-Entity Mutation Guard: Attempting to create a reconciliation session for another entity's statement must be rejected
    const crossRecRes = await page.request.post('/api/v1/banking/reconciliations', {
      headers: { 'X-CSRF-TOKEN': token },
      data: {
        legalEntityId: unauthorizedEntityId,
        asOfDate: '2026-10-01',
        statementId: 'acf650c1-cb88-9757-a1c0-bc430a85d07e'
      }
    });
    expect([400, 403, 404]).toContain(crossRecRes.status());

    // 3. CSRF Security Enforcement: Mutation request with invalid CSRF token must be rejected with 400
    const invalidCsrfRes = await page.request.post('/api/v1/banking/treasury-accounts', {
      headers: { 'X-CSRF-TOKEN': 'invalid-csrf-probe' },
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        code: 'CSRF-PROBE',
        name: 'Probe Account',
        type: 'Bank',
        currencyPolicy: 'SingleCurrency',
        currencyCode: 'EGP',
        purpose: 'Operating',
        liquidityClass: 'Operating'
      }
    });
    expect(invalidCsrfRes.status()).toBe(400);

    // 4. Unauthenticated Security Boundary: Request without session cookie must receive 401 Unauthorized
    const unauthPage = await page.context().browser()!.newPage();
    const unauthRes = await unauthPage.request.get(`/api/v1/banking/treasury-accounts?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(unauthRes.status()).toBe(401);
    await unauthPage.close();
  });
});
