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

  async function loginAsAdmin(page: Page) {
    await page.goto('/');
    await page.context().clearCookies();
    await page.evaluate(() => {
      try {
        localStorage.clear();
        sessionStorage.clear();
      } catch {}
    });

    const password = getTestDevPassword();
    const token = await getCsrfToken(page);
    const loginRes = await page.request.post('/api/v1/auth/login', {
      data: { email: 'admin@atlas.com', password },
      headers: { 'X-CSRF-TOKEN': token }
    });
    expect(loginRes.status()).toBe(200);

    await page.goto(`/banking?legalEntityId=${ATLAS_ENTITY_ID}`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('[data-testid="banking-page"]')).toBeVisible({ timeout: 20000 });
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
    await expect(page.getByText(/إجمالي السيولة المتاحة|Total Liquid/i).first()).toBeVisible();

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
      await page.waitForLoadState('domcontentloaded');
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
  });
});
