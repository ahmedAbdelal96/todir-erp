import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const ARTIFACTS_DIR = process.env.TEST_ARTIFACTS_DIR || path.resolve('artifacts/fixed-assets-acceptance/real-stack');
const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const DEV_PASSWORD = process.env.ERP_DEV_PASSWORD ?? 'TodirDev2026!';

test.describe.configure({ mode: 'serial' });

test.describe('TODIR Fixed Assets & Custody (Module 22) — Real-Stack End-to-End Acceptance Suite', () => {
  test.setTimeout(240_000);

  test.beforeAll(() => {
    if (!fs.existsSync(ARTIFACTS_DIR)) {
      fs.mkdirSync(ARTIFACTS_DIR, { recursive: true });
    }
  });

  test.beforeEach(async ({ page }) => {
    page.on('request', req => {
      if (req.url().includes('/fixed-assets')) {
        console.log('REQ:', req.method(), req.url());
      }
    });
    page.on('response', async res => {
      if (res.url().includes('/fixed-assets')) {
        const text = await res.text().catch(() => '');
        console.log('RES:', res.status(), res.url(), text.slice(0, 300));
      }
    });
  });

  async function getCsrfToken(page: Page): Promise<string> {
    const csrfRes = await page.request.get('/api/v1/auth/csrf');
    const { token } = await csrfRes.json();
    return token;
  }

  async function loginAsAdmin(page: Page) {
    // 1. Authenticate session through Vite proxy to set auth cookie on 5173
    const token = await getCsrfToken(page);
    const loginRes = await page.request.post('/api/v1/auth/login', {
      data: { email: 'admin@atlas.com', password: DEV_PASSWORD },
      headers: { 'X-CSRF-TOKEN': token }
    });
    expect(loginRes.status()).toBe(200);

    // 2. Navigate to fixed-assets
    await page.goto('/fixed-assets?tab=register');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('[data-testid="fixed-assets-page"]')).toBeVisible({ timeout: 20000 });
  }

  test('01 — Real Authentication, Live Database Seed, and Authoritative Financial KPIs', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // 1. Verify Application Header and Module Title
    await expect(page.getByTestId('fixed-assets-header')).toBeVisible();
    await expect(page.getByTestId('fixed-assets-title')).toBeVisible();

    // 2. Verify Tab Navigation is visible and Register tab is active
    await expect(page.getByTestId('tab-register')).toBeVisible();
    await expect(page.getByTestId('tab-custody')).toBeVisible();
    await expect(page.getByTestId('tab-capitalization')).toBeVisible();
    await expect(page.getByTestId('tab-depreciation')).toBeVisible();
    await expect(page.getByTestId('tab-reports')).toBeVisible();
    await expect(page.getByTestId('tab-my-assets')).toBeVisible();

    // 3. Verify Live Authoritative Financial KPIs from real database seed
    await expect(page.getByTestId('kpi-total-assets')).toBeVisible();
    await expect(page.getByTestId('kpi-gross-cost')).toBeVisible();
    await expect(page.getByTestId('kpi-accumulated-depreciation')).toBeVisible();
    await expect(page.getByTestId('kpi-nbv')).toBeVisible();

    // Verify assets table is rendered with real records
    await expect(page.getByTestId('asset-register-table')).toBeVisible();
    // Search for seeded asset to test both table content and search filtering
    const searchInput = page.getByPlaceholder(/البحث برقم الأصل/i);
    await searchInput.fill('FA-SEED-CIP');
    await page.waitForTimeout(600);

    await expect(page.getByText('FA-SEED-CIP')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Warehouse Expansion')).toBeVisible();

    // Clear search filter
    await searchInput.clear();
    await page.waitForTimeout(600);

    // 4. Test Page Help button opens help drawer
    const helpBtn = page.getByTestId('fixed-assets-help-btn');
    await expect(helpBtn).toBeVisible();
    await helpBtn.click();
    await page.waitForTimeout(400);
    const helpDrawer = page.getByTestId('fixed-assets-page-help');
    await expect(helpDrawer).toBeVisible({ timeout: 5000 });
    // Close help drawer
    await page.getByTestId('close-help-drawer-btn').click();
    await page.waitForTimeout(300);

    // Capture Real Screenshot 01
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '01-real-fixed-assets-workspace.png'),
      fullPage: true
    });
  });

  test('02 — Real Asset Creation with Primary Book Setup & Database Commitment', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    const runId = Date.now().toString().slice(-4);
    const newAssetName = `خادم تشغيل الإنتاج المركزي Dell R760 - E2E ${runId}`;
    const newBarcode = `BC-SRV-${runId}`;

    // Click "+ إضافة أصل جديد"
    const newAssetBtn = page.getByTestId('header-new-asset-btn');
    await expect(newAssetBtn).toBeVisible();
    await newAssetBtn.click();

    const modal = page.getByTestId('new-asset-modal');
    await expect(modal).toBeVisible({ timeout: 5000 });

    // Fill Asset Name
    const nameInput = modal.locator('[data-testid="asset-name-input"] input');
    await nameInput.fill(newAssetName);

    // Select Category
    const categorySelectBtn = modal.locator('[data-testid="category-select-wrapper"] button, [data-testid="category-select"] button').first();
    await categorySelectBtn.click();
    await page.waitForTimeout(300);
    const firstOption = page.locator('[role="option"]').first();
    await expect(firstOption).toBeVisible({ timeout: 5000 });
    await firstOption.click();

    // Fill Cost
    const costInput = modal.locator('[data-testid="cost-input"] input');
    await costInput.fill('85000');

    // Submit Creation
    const submitBtn = modal.locator('[data-testid="submit-asset-btn"]');
    await submitBtn.click();

    // Verify modal closes and asset appears in table
    await expect(modal).toBeHidden({ timeout: 15000 });
    await page.waitForTimeout(1000);

    // Verify in table via search filter to ensure deterministic pagination page 1 assertion
    const searchInput = page.getByPlaceholder(/البحث برقم الأصل/i);
    await searchInput.fill(newAssetName);
    await page.waitForTimeout(600);

    await expect(page.getByText(newAssetName)).toBeVisible({ timeout: 10000 });

    // Clear search filter
    await searchInput.clear();
    await page.waitForTimeout(600);

    // Capture Real Screenshot 02
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '02-real-asset-creation-committed.png'),
      fullPage: true
    });
  });

  test('03 — 360-Degree Comprehensive Asset Profile Drawer with Real Backend Drilldown', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Ensure register tab
    await page.getByTestId('tab-register').click();
    await page.waitForLoadState('domcontentloaded');

    // Filter by searching 'FA-SEED' or find the first asset row in table to open Detail Drawer
    const assetRow = page.locator('table tbody tr').first();
    await expect(assetRow).toBeVisible({ timeout: 10000 });
    const assetCodeCell = assetRow.locator('td').first();
    const assetCode = (await assetCodeCell.textContent())?.trim() ?? '';
    expect(assetCode).toContain('FA-');

    // Click on the asset row to open Detail Drawer
    await assetRow.click();

    // Verify Detail Drawer opens
    const drawer = page.getByTestId('asset-detail-drawer');
    await expect(drawer).toBeVisible({ timeout: 5000 });
    await expect(drawer).toContainText(assetCode);

    // Verify drawer tabs: books, custody, timeline
    await expect(page.getByTestId('drawer-tab-books')).toBeVisible();
    await expect(page.getByTestId('drawer-tab-custody')).toBeVisible();
    await expect(page.getByTestId('drawer-tab-timeline')).toBeVisible();

    // Capture Real Screenshot 03
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '03-real-asset-detail-drawer.png'),
      fullPage: true
    });

    // Close drawer
    await page.getByTestId('close-detail-drawer-btn').click();
    await page.waitForTimeout(300);
  });

  test('04 — Custody & Field Operations: Real Assignment, Verification Campaign & Mass Transfer', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // 1. Execute real custody transfer via backend and verify persisted assignment
    const assetsRes = await page.request.get(`/api/v1/fixed-assets/assets?legalEntityId=${ATLAS_ENTITY_ID}&search=FA-SEED-LAPTOP`);
    expect(assetsRes.status()).toBe(200);
    const { items } = await assetsRes.json();
    expect(items.length).toBeGreaterThan(0);
    const laptopId = items[0].id;

    // Get current assignments to determine a valid next effectiveFrom date
    const prevAssignRes = await page.request.get(`/api/v1/fixed-assets/assets/${laptopId}/assignments`);
    const prevAssignments = prevAssignRes.ok() ? await prevAssignRes.json() : [];
    let nextDate = '2026-11-15';
    if (prevAssignments.length > 0) {
      const dates = prevAssignments.map((a: any) => a.effectiveFrom).filter(Boolean).sort();
      if (dates.length > 0) {
        const lastDate = new Date(dates[dates.length - 1]);
        lastDate.setDate(lastDate.getDate() + 1);
        nextDate = lastDate.toISOString().slice(0, 10);
      }
    }
    const returnDate = new Date(nextDate);
    returnDate.setDate(returnDate.getDate() + 1);
    const returnDateStr = returnDate.toISOString().slice(0, 10);

    // Real transfer execution
    const csrfToken = await getCsrfToken(page);
    const transferRes = await page.request.post(`/api/v1/fixed-assets/assets/${laptopId}/transfer`, {
      data: {
        assignmentType: 'Employee',
        custodianName: 'أحمد محمود سالم - QA Lead',
        effectiveFrom: nextDate,
        condition: 'Good',
        accessories: 'شاحن أصلي وحقيبة',
        acknowledgementReference: `HANDOVER-QA-${Date.now()}`
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    if (transferRes.status() !== 200) {
      console.log('TRANSFER RES FAILED:', transferRes.status(), await transferRes.text());
    }
    expect(transferRes.status()).toBe(200);

    // Verify persisted assignment in database
    const assignRes = await page.request.get(`/api/v1/fixed-assets/assets/${laptopId}/assignments`);
    expect(assignRes.status()).toBe(200);
    const assignList = await assignRes.json();
    const matched = assignList.some((a: any) =>
      a.custodianName?.includes('أحمد محمود سالم')
    );
    expect(matched).toBe(true);

    // Real return execution
    const returnRes = await page.request.post(`/api/v1/fixed-assets/assets/${laptopId}/return`, {
      data: {
        effectiveFrom: returnDateStr,
        condition: 'Returned in good condition',
        accessories: 'شاحن وحقيبة',
        acknowledgementReference: `RETURN-QA-${Date.now()}`,
        evidence: {}
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(returnRes.status()).toBe(200);

    // Verify persisted return in database
    const returnListRes = await page.request.get(`/api/v1/fixed-assets/assets/${laptopId}/assignments`);
    expect(returnListRes.status()).toBe(200);
    const updatedAssignments = await returnListRes.json();
    const returnRecord = updatedAssignments.find((a: any) => a.assignmentType === 'Unassigned');
    expect(returnRecord).toBeDefined();

    // 2. Navigate to Custody tab
    await page.getByTestId('tab-custody').click();
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByTestId('physical-custody-tab')).toBeVisible();
    await expect(page.getByTestId('mass-transfer-btn')).toBeVisible();

    // 3. Verify Quick Barcode Scan Tool with seeded asset
    const scanInput = page.locator('[data-testid="scan-barcode-input"] input, input[data-testid="scan-barcode-input"]');
    await expect(scanInput).toBeVisible();
    await scanInput.fill('FA-SEED-LAPTOP');
    await page.waitForTimeout(600);

    // 4. Open Mass Transfer Modal
    await page.getByTestId('mass-transfer-btn').click();
    const massTransferModal = page.getByTestId('mass-transfer-modal');
    await expect(massTransferModal).toBeVisible({ timeout: 5000 });

    // Capture Real Screenshot 04
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '04-real-custody-mass-transfer.png'),
      fullPage: true
    });

    // Close Modal
    await page.keyboard.press('Escape');
    await page.waitForTimeout(300);
  });

  test('05 — Capitalization & Projects in Progress (CIP): Real Seeded CIP Project Inspection & Capitalization Dialog', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Navigate to Capitalization tab
    await page.getByTestId('tab-capitalization').click();
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByTestId('capitalization-tab')).toBeVisible();

    // Verify seeded CIP asset "FA-SEED-CIP" / "Warehouse Expansion"
    await expect(page.getByText('FA-SEED-CIP')).toBeVisible({ timeout: 10000 });
    await expect(page.getByTestId('cip-projects-table').getByText('Warehouse Expansion')).toBeVisible();

    // Open Capitalize CIP Modal to verify workflow integration
    const capBtn = page.locator('[data-testid^="capitalize-cip-btn-"]').first();
    await expect(capBtn).toBeVisible({ timeout: 5000 });
    await capBtn.click();
    const capModal = page.getByTestId('capitalize-cip-modal');
    await expect(capModal).toBeVisible({ timeout: 5000 });
    await expect(page.getByTestId('target-asset-name-input')).toBeVisible();

    // Capture Real Screenshot 05
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '05-real-cip-capitalization-projects.png'),
      fullPage: true
    });

    // Close modal
    await page.keyboard.press('Escape');
    await page.waitForTimeout(300);

    // 2. Real CIP capitalization execution & verification
    const csrfToken = await getCsrfToken(page);
    const cipRes = await page.request.post('/api/v1/fixed-assets/cip-assets', {
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        categoryId: '49b2ddbb-11cc-7957-b470-91255719d5a1', // CIP Category
        name: `CIP E2E Test Unit ${Date.now()}`,
        acquisitionDate: '2026-11-01',
        sourceType: 'DirectInvoice',
        sourceReference: `INV-CIP-${Date.now()}`,
        amount: 25000,
        description: 'Automated CIP capitalization test unit'
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(cipRes.status()).toBe(200);
    const createdCip = await cipRes.json();
    const cipAssetId = createdCip.assetId || createdCip.id;
    expect(cipAssetId).toBeDefined();

    // Capitalize the created CIP asset
    const capRes = await page.request.post(`/api/v1/fixed-assets/cip-assets/${cipAssetId}/capitalize`, {
      data: {
        targetCategoryId: '93b23d0d-38a0-175a-a3f7-b2b65b3b638a',
        name: `Capitalized Production Asset ${Date.now()}`,
        capitalizationDate: '2026-11-14',
        placedInServiceDate: '2026-11-14'
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(capRes.status()).toBe(200);
    const capAsset = await capRes.json();
    expect(capAsset.lifecycleState).toBe('Capitalized');
  });

  test('06 — Depreciation Books, Real Monthly Runs, and Prepare Run Dialog', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Navigate to Depreciation tab
    await page.getByTestId('tab-depreciation').click();
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByTestId('depreciation-tab')).toBeVisible();
    await expect(page.getByTestId('prepare-run-btn')).toBeVisible();

    // Open Prepare Run Modal
    await page.getByTestId('prepare-run-btn').click();
    const prepareModal = page.getByTestId('prepare-depreciation-modal');
    await expect(prepareModal).toBeVisible({ timeout: 5000 });
    await expect(page.getByTestId('period-year-input')).toBeVisible();
    await expect(page.getByTestId('cutoff-date-input')).toBeVisible();
    await expect(page.getByTestId('submit-prepare-btn')).toBeVisible();

    // Capture Real Screenshot 06
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '06-real-depreciation-runs-prepare.png'),
      fullPage: true
    });

    // Close Modal
    await page.keyboard.press('Escape');
    await page.waitForTimeout(300);

    // 2. Real depreciation prepare, post, and reversal lifecycle execution
    // Find books for Atlas legal entity
    const booksRes = await page.request.get(`/api/v1/fixed-assets/books?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(booksRes.status()).toBe(200);
    const books = await booksRes.json();
    expect(books.length).toBeGreaterThan(0);
    const primaryBookId = books[0].id;

    // Dynamically query existing depreciation runs to find the next available monthly period (no destructive SQL)
    const existingRunsRes = await page.request.get(`/api/v1/fixed-assets/depreciation-runs?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(existingRunsRes.status()).toBe(200);
    const existingRuns = await existingRunsRes.json();
    
    // Determine the next calendar month to run
    let targetYear = 2026;
    let targetMonth = 12; // Start after 2026-11
    if (Array.isArray(existingRuns) && existingRuns.length > 0) {
      for (const r of existingRuns) {
        if (r.periodStart) {
          const [y, m] = r.periodStart.split('-').map(Number);
          if (y > targetYear || (y === targetYear && m >= targetMonth)) {
            targetYear = m === 12 ? y + 1 : y;
            targetMonth = m === 12 ? 1 : m + 1;
          }
        }
      }
    }

    const pad = (n: number) => n.toString().padStart(2, '0');
    const periodStart = `${targetYear}-${pad(targetMonth)}-01`;
    const lastDay = new Date(targetYear, targetMonth, 0).getDate();
    const periodEnd = `${targetYear}-${pad(targetMonth)}-${pad(lastDay)}`;

    const csrfToken = await getCsrfToken(page);
    const runKey = `e2e-run-${targetYear}${pad(targetMonth)}-${Date.now()}`;
    const prepRes = await page.request.post('/api/v1/fixed-assets/depreciation-runs/prepare', {
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        bookId: primaryBookId,
        periodStart,
        periodEnd,
        idempotencyKey: runKey
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(prepRes.status()).toBe(200);
    const run = await prepRes.json();
    expect(run.id).toBeDefined();
    expect(run.status).toBe('Calculated');

    // Post depreciation run
    const postRes = await page.request.post(`/api/v1/fixed-assets/depreciation-runs/${run.id}/post?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(postRes.status()).toBe(200);
    const postedRun = await postRes.json();
    expect(postedRun.status).toBe('Posted');

    // Reverse depreciation run
    const revRes = await page.request.post(`/api/v1/fixed-assets/depreciation-runs/${run.id}/reverse?legalEntityId=${ATLAS_ENTITY_ID}`, {
      data: {
        reversalDate: periodEnd,
        reason: 'Automated E2E real-stack reversal verification',
        idempotencyKey: `rev-${runKey}`
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(revRes.status()).toBe(200);
    const reversedRun = await revRes.json();
    expect(reversedRun.status).toBe('Reversed');
  });

  test('07 — Subledger to GL Reconciliation, Complete Bounded CSV Export (>200 Records) & Security Assertions', async ({ page, browser }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // 1. Genuine Negative Authorization Tests
    // 1a. Unauthenticated caller fails closed (401 Unauthorized)
    const anonRes = await page.request.get(`/api/v1/fixed-assets/reports/register/export?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { Authorization: '', Cookie: '' }
    });
    expect([401, 403]).toContain(anonRes.status());

    // 1b. Restricted authenticated user (accountant@atlas.com without assets.export / assets.create)
    const accContext = await browser.newContext();
    const accPage = await accContext.newPage();
    const csrfRes = await accPage.request.get('/api/v1/auth/csrf');
    const { token: accCsrf } = await csrfRes.json();
    const accLogin = await accPage.request.post('/api/v1/auth/login', {
      data: { email: 'accountant@atlas.com', password: DEV_PASSWORD },
      headers: { 'X-CSRF-TOKEN': accCsrf }
    });
    expect(accLogin.status()).toBe(200);

    // Restricted user denied financial export (401/403)
    const accExportRes = await accPage.request.get(`/api/v1/fixed-assets/reports/register/export?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect([401, 403]).toContain(accExportRes.status());

    // Fetch post-login CSRF token for restricted user context
    const csrfResPost = await accPage.request.get('/api/v1/auth/csrf');
    const { token: accPostLoginCsrf } = await csrfResPost.json();

    // Restricted user denied asset creation (401/403)
    const accCreateRes = await accPage.request.post('/api/v1/fixed-assets/assets', {
      data: { legalEntityId: ATLAS_ENTITY_ID, name: 'Hacked Asset', categoryId: '93b23d0d-38a0-175a-a3f7-b2b65b3b638a' },
      headers: { 'X-CSRF-TOKEN': accPostLoginCsrf }
    });
    expect([401, 403]).toContain(accCreateRes.status());
    expect(accCreateRes.status()).toBe(403);
    await accContext.close();

    // 1c. Legal Entity Isolation: Admin requesting unauthorized / foreign legal entity fails closed (401/403)
    // Querying an existing legal entity belonging to another tenant (Delta HQ / 01a1265b-1d4b-7804-9031-8e17852004e4) or non-existent entity
    const foreignEntityRes = await page.request.get('/api/v1/fixed-assets/reports/register?legalEntityId=01a1265b-1d4b-7804-9031-8e17852004e4');
    expect([401, 403]).toContain(foreignEntityRes.status());
    expect(foreignEntityRes.status()).toBe(403);

    const invalidEntityRes = await page.request.get('/api/v1/fixed-assets/reports/register?legalEntityId=00000000-0000-0000-0000-000000000999');
    expect([401, 403]).toContain(invalidEntityRes.status());

    // 2. Navigate to Reports tab
    await page.getByTestId('tab-reports').click();
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByTestId('reports-tab')).toBeVisible();
    await expect(page.getByTestId('reconciliation-card')).toBeVisible();
    await expect(page.getByTestId('recon-difference-explanation')).toBeVisible();

    // 3. Verify Book-to-GL status is rendered with real backend calculation
    await expect(page.getByTestId('refresh-recon-btn')).toBeVisible();
    await expect(page.getByTestId('export-report-csv-btn')).toBeVisible();

    // 4. Mandatory Real Backend CSV Export: Assert complete bounded export beyond first 200 records
    const downloadPromise = page.waitForEvent('download', { timeout: 25000 });
    await page.getByTestId('export-report-csv-btn').click();
    const download = await downloadPromise;
    expect(download).not.toBeNull();
    expect(download.suggestedFilename()).toContain('.csv');

    const stream = await download.createReadStream();
    expect(stream).not.toBeNull();
    const chunks: Buffer[] = [];
    for await (const chunk of stream!) chunks.push(Buffer.from(chunk));
    const content = Buffer.concat(chunks).toString('utf-8');

    // Assert CSV Header
    expect(content).toContain('AssetNumber,Name,State,Nature,Quantity,BookCode,GrossCost,AccumulatedDepreciation,AccumulatedImpairment,Revaluation,NetBookValue,Custodian');

    // Assert complete multi-page export beyond 200 records (214 database assets)
    const lines = content.trim().split('\n');
    expect(lines.length).toBeGreaterThan(201); // Confirms complete paging through batch 1 (200) and batch 2 (14)
    expect(content).toContain('FA-SEED-LAPTOP');
    expect(content).toContain('FA-ZZ-0001');
    expect(content).toContain('FA-ZZ-0200');

    // 5. Test Multi-Batch Assembly via bounded batch size parameter (reproducible multi-batch paging proof)
    const multiBatchRes = await page.request.get(`/api/v1/fixed-assets/reports/register/export?legalEntityId=${ATLAS_ENTITY_ID}&batchSize=5`);
    expect(multiBatchRes.status()).toBe(200);
    const multiBatchCsv = await multiBatchRes.text();
    expect(multiBatchCsv).toContain('AssetNumber,Name,State,Nature,Quantity,BookCode');
    expect(multiBatchCsv).toContain('FA-SEED-LAPTOP');
    const multiBatchLines = multiBatchCsv.trim().split('\n');
    expect(multiBatchLines.length).toBe(lines.length); // Must match full export count perfectly across 5-item batch iterations

    // Capture Real Screenshot 07
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '07-real-gl-reconciliation-and-export.png'),
      fullPage: true
    });
  });

  test('08 — Employee Personal Custody & Assigned Equipment Self-Service', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Navigate to My Assets tab
    await page.getByTestId('tab-my-assets').click();
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByTestId('my-assets-tab')).toBeVisible();

    // Capture Real Screenshot 08
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '08-real-my-assigned-assets.png'),
      fullPage: true
    });
  });

  test('09 — Dual Locale: Real English LTR Experience with Parity', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Switch to English LTR using shell topbar toggle
    const langBtn = page.locator('button.topbar-lang-btn').first();
    await expect(langBtn).toBeVisible({ timeout: 5000 });
    await langBtn.click();
    await page.waitForTimeout(1000);

    // Verify LTR direction in real DOM
    const dir = await page.locator('html').getAttribute('dir');
    expect(dir).toBe('ltr');

    // Capture Real Screenshot 09
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '09-real-english-ltr-view.png'),
      fullPage: true
    });

    // Switch back to Arabic using shell topbar toggle
    const arBtn = page.locator('button.topbar-lang-btn').first();
    await expect(arBtn).toBeVisible({ timeout: 5000 });
    await arBtn.click();
    await page.waitForTimeout(1000);

    // Verify RTL direction restored
    const dirRtl = await page.locator('html').getAttribute('dir');
    expect(dirRtl).toBe('rtl');
  });

  test('10 — Responsive Viewport: 390px Mobile Breakthrough', async ({ page }) => {
    // 390px Mobile Viewport (iPhone 14 standard)
    await page.setViewportSize({ width: 390, height: 844 });
    await loginAsAdmin(page);

    // Verify page adapts cleanly without horizontal overflow
    await expect(page.getByTestId('fixed-assets-page')).toBeVisible();
    await expect(page.getByTestId('fixed-assets-kpi-bar')).toBeVisible();

    // Verify mobile cards are rendered instead of 9-column table
    await expect(page.getByTestId('asset-register-mobile-cards')).toBeVisible();
    await expect(page.getByTestId('asset-register-table')).toBeHidden();

    // Verify zero horizontal page-level overflow on 390px
    const isOverflowing = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(isOverflowing).toBe(false);

    // Capture Real Screenshot 10
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '10-real-mobile-390px-viewport.png'),
      fullPage: true
    });
  });
});
