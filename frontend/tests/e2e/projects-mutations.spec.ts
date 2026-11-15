import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/projects-acceptance/mutations');
const AGENT_BRAIN_DIR = 'C:/Users/IT/.gemini/antigravity/brain/2b36897c-7b21-48b3-b339-9bb22437719d/projects-acceptance/mutations';
const ARTIFACTS_DIR = process.env.TEST_ARTIFACTS_DIR || LOCAL_ARTIFACTS_DIR;

const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const DELTA_ENTITY_ID = '7c9e6679-7425-40de-944b-e07fc1f90ae7';
const DEV_PASSWORD = process.env.ERP_DEV_PASSWORD ?? 'TodirDev2026!';

// Seeded capital project for Journey 08
const SEEDED_CAPITAL_PROJECT_ID = '01a1df8f-b324-7fe8-9db7-761f649d7811';

// Shared state across the serial mutation journey
let createdProjectId: string = '';
let createdProjectNumber: string = '';

test.describe.configure({ mode: 'serial' });

test.describe('TODIR Projects & Operations (Module 23) — Real-Stack Mutation Acceptance Suite', () => {
  test.setTimeout(180_000);

  test.beforeAll(() => {
    [LOCAL_ARTIFACTS_DIR, AGENT_BRAIN_DIR].forEach((dir) => {
      try {
        if (!fs.existsSync(dir)) {
          fs.mkdirSync(dir, { recursive: true });
        }
      } catch {
        // ignore directory creation error if outside local permissions
      }
    });
  });

  async function getCsrfToken(page: Page): Promise<string> {
    const csrfRes = await page.request.get('/api/v1/auth/csrf');
    const { token } = await csrfRes.json();
    return token;
  }

  async function loginAsAdmin(page: Page) {
    const token = await getCsrfToken(page);
    const loginRes = await page.request.post('/api/v1/auth/login', {
      data: { email: 'admin@atlas.com', password: DEV_PASSWORD },
      headers: { 'X-CSRF-TOKEN': token }
    });
    expect(loginRes.status()).toBe(200);

    await page.goto('/projects');
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('h1')).toContainText(/إدارة المشاريع|Projects/i, { timeout: 20000 });
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

  // --------------------------------------------------------------------------
  // Journey 01: Real Project Creation via UI Form & Persistence Assertions
  // --------------------------------------------------------------------------
  test('Journey 01 — Real Project Creation (Internal & Customer Projects) & PostgreSQL Persistence', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    const runId = Date.now().toString().slice(-4);
    const internalProjectName = `مشروع داخلي تطويري - تجربة E2E ${runId}`;
    const customerProjectName = `مشروع استشاري تجاري - قبول نهائي ${runId}`;

    // 1. Create Internal Project via Real UI Form
    await page.locator('[data-testid="btn-new-project"]').click();
    await expect(page.getByText('إنشاء مشروع جديد')).toBeVisible({ timeout: 10000 });

    const modal = page.locator('form#new-project-form');

    // Select InternalProject from Project Type dropdown inside modal
    await modal.locator('select').first().selectOption('InternalProject');

    // Fill form
    await modal.locator('input[type="text"]').first().fill(internalProjectName);

    // Set Dates
    const dateInputs = modal.locator('input[type="date"]');
    if (await dateInputs.count() >= 2) {
      await dateInputs.nth(0).fill('2026-11-01');
      await dateInputs.nth(1).fill('2027-04-30');
    }

    // Set Budget
    const numberInputs = modal.locator('input[type="number"]');
    if (await numberInputs.count() > 0) {
      await numberInputs.first().fill('500000');
    }

    // Intercept project creation POST
    const createPromise = page.waitForResponse(
      (res) => res.url().includes('/api/v1/projects/legal-entities/') && res.request().method() === 'POST'
    );

    // Click Save
    await page.locator('[data-testid="btn-save-project"]').click();

    // Verify 201 Created response from backend
    const createResponse = await createPromise;
    expect(createResponse.status()).toBe(201);
    const internalData = await createResponse.json();
    expect(internalData.id).toBeTruthy();
    expect(internalData.projectType).toBe('InternalProject');

    // 2. Create Commercial Customer Project with Real Directory Customer
    const csrfToken = await getCsrfToken(page);
    const custRes = await page.request.post(`/api/v1/projects/legal-entities/${ATLAS_ENTITY_ID}`, {
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        name: customerProjectName,
        projectType: 'CustomerProject',
        customerPartyId: '023b6cc2-2c8a-8756-b1e8-7664444e37e9',
        billingModel: 'TimeAndMaterials',
        startDate: '2026-11-01',
        targetEndDate: '2027-04-30',
        initialBudget: 750000,
        currencyCode: 'EGP',
        idempotencyKey: `prj-mut-${Date.now()}`
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(custRes.status()).toBe(201);
    const customerData = await custRes.json();

    expect(customerData.id).toBeTruthy();
    expect(customerData.projectNumber).toBeTruthy();
    expect(customerData.name).toBe(customerProjectName);

    createdProjectId = customerData.id;
    createdProjectNumber = customerData.projectNumber;

    // Navigate to the newly created customer project workspace
    await page.goto(`/projects/${createdProjectId}`);
    await page.waitForLoadState('domcontentloaded');

    // Assert Project Number, Name, Overview Strip, and Budget in UI
    await expect(page.getByText(createdProjectNumber)).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(customerProjectName)).toBeVisible();
    await expect(page.locator('[data-testid="project-overview-strip"]')).toBeVisible();

    await saveArtifact(page, 'mutation-01-project-created.png');
  });

  // --------------------------------------------------------------------------
  // Journey 02: Real Tasks, Milestones, and Deliverables Workflow
  // --------------------------------------------------------------------------
  test('Journey 02 — Tasks, Milestones, and Deliverables Creation & State Mutations', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}?tab=tasks`);
    await page.waitForLoadState('domcontentloaded');

    // 1. Create Task
    await page.locator('[data-testid="btn-add-task"]').click();
    await expect(page.getByRole('heading', { name: /إضافة مهمة جديدة/i })).toBeVisible();

    await page.locator('input[type="text"]').first().fill('مرحلة التصميم والتكامل التقني');

    const taskPromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/tasks`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-task"]').click();

    const taskRes = await taskPromise;
    expect(taskRes.status()).toBe(201);
    await expect(page.getByText('مرحلة التصميم والتكامل التقني')).toBeVisible({ timeout: 10000 });

    // 2. Create Milestone
    await page.locator('[data-testid="subtab-milestones"]').click();
    await page.locator('[data-testid="btn-add-milestone"]').click();
    await expect(page.getByRole('heading', { name: /معلم رئيسي/i })).toBeVisible();

    await page.locator('input[type="text"]').first().fill('اعتماد المخطط التقني');

    const milestonePromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/milestones`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-milestone"]').click();

    const milestoneRes = await milestonePromise;
    expect(milestoneRes.status()).toBe(201);
    await expect(page.getByText('اعتماد المخطط التقني')).toBeVisible({ timeout: 10000 });

    // 3. Create Deliverable
    await page.locator('[data-testid="subtab-deliverables"]').click();
    await page.locator('[data-testid="btn-add-deliverable"]').click();
    await expect(page.getByRole('heading', { name: /مخرج تسليم/i })).toBeVisible();

    await page.locator('input[type="text"]').first().fill('وثيقة المتطلبات البرمجية المعتمدة');

    const deliverablePromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/deliverables`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-deliverable"]').click();

    const deliverableRes = await deliverablePromise;
    expect(deliverableRes.status()).toBe(201);
    await expect(page.getByText('وثيقة المتطلبات البرمجية المعتمدة')).toBeVisible({ timeout: 10000 });

    // Verify persistence after reloading page
    await page.goto(`/projects/${createdProjectId}?tab=tasks`);
    await page.waitForLoadState('domcontentloaded');
    await page.locator('[data-testid="subtab-deliverables"]').click();
    await expect(page.getByText('وثيقة المتطلبات البرمجية المعتمدة')).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, 'mutation-02-tasks-milestones-deliverables.png');
  });

  // --------------------------------------------------------------------------
  // Journey 03: Team Member & Generic Resource Allocation
  // --------------------------------------------------------------------------
  test('Journey 03 — Team Member & Resource Requirement Allocation', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}?tab=team`);
    await page.waitForLoadState('domcontentloaded');

    // 1. Add Team Member
    await page.locator('[data-testid="btn-add-team"]').click();
    await expect(page.getByRole('heading', { name: /تعيين عضو في فريق المشروع|إضافة عضو لفريق العمل/i })).toBeVisible();

    const teamRateInput = page.locator('input[type="number"]');
    if (await teamRateInput.count() > 0) {
      await teamRateInput.first().fill('80'); // Allocation percent (<= 100)
    }

    const teamPromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/team`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-team"]').click();

    const teamRes = await teamPromise;
    expect(teamRes.status()).toBe(201);

    // 2. Add Resource Requirement
    await page.locator('[data-testid="subtab-requirements"]').click();
    await page.locator('[data-testid="btn-add-req"]').click();
    await expect(page.getByRole('heading', { name: /تحديد احتياج وظيفي|إضافة متطلب موارد/i })).toBeVisible();

    await page.locator('input[type="text"]').first().fill('Senior Lead Engineer');

    const reqPromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/resources/requirements`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-req"]').click();

    const reqRes = await reqPromise;
    expect(reqRes.status()).toBe(201);
    await expect(page.getByText('Senior Lead Engineer').first()).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, 'mutation-03-team-resources.png');
  });

  // --------------------------------------------------------------------------
  // Journey 04: Timesheets Lifecycle (Log -> Submit -> Approve -> Finalize)
  // --------------------------------------------------------------------------
  test('Journey 04 — Timesheets Full Lifecycle (Log, Submit, Approve, Finalize)', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}?tab=time`);
    await page.waitForLoadState('domcontentloaded');

    // 1. Log Time Entry
    await page.locator('[data-testid="btn-log-time"]').click();
    await expect(page.getByRole('heading', { name: /تسجيل ساعات عمل/i })).toBeVisible();

    const hoursInput = page.locator('input[type="number"]');
    if (await hoursInput.count() > 0) {
      await hoursInput.first().fill('8');
    }

    const descInput = page.locator('input[type="text"]').first();
    await descInput.fill('هندسة وبناء واجهات المشروع وفق نظام التصميم');

    const timePromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/time`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-time"]').click();

    const timeRes = await timePromise;
    expect(timeRes.status()).toBe(201);
    const timeData = await timeRes.json();
    const timeId = timeData.id;

    // Verify Draft row
    await expect(page.getByText(/مسودة/i).first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('[data-testid="btn-time-submit"]').first()).toBeVisible();

    // 2. Submit Action
    const submitPromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/time/${timeId}/submit`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-time-submit"]').first().click();
    const submitRes = await submitPromise;
    expect(submitRes.status()).toBe(200);

    // 3. Approve Action
    await expect(page.locator('[data-testid="btn-time-approve"]').first()).toBeVisible({ timeout: 10000 });
    const approvePromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/time/${timeId}/approve`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-time-approve"]').first().click();
    const approveRes = await approvePromise;
    expect(approveRes.status()).toBe(200);

    // 4. Finalize Action
    await expect(page.locator('[data-testid="btn-time-finalize"]').first()).toBeVisible({ timeout: 10000 });
    const finalizePromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/time/${timeId}/finalize`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-time-finalize"]').first().click();
    const finalizeRes = await finalizePromise;
    expect(finalizeRes.status()).toBe(200);

    await saveArtifact(page, 'mutation-04-timesheets-lifecycle.png');
  });

  // --------------------------------------------------------------------------
  // Journey 05: Budgets & Forecast Revisions
  // --------------------------------------------------------------------------
  test('Journey 05 — Budgets & Forecast Revisions', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}?tab=financials`);
    await page.waitForLoadState('domcontentloaded');

    // Create Forecast
    await page.locator('[data-testid="btn-create-forecast"]').click();
    await expect(page.getByRole('heading', { name: /تحديث تقدير التكلفة|تسجيل توقعات التكلفة/i })).toBeVisible();

    const numberInputs = page.locator('input[type="number"]');
    if (await numberInputs.count() > 0) {
      await numberInputs.first().fill('320000'); // ETC
    }

    const forecastPromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/forecasts`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-forecast"]').click();

    const forecastRes = await forecastPromise;
    expect(forecastRes.status()).toBe(201);

    // Verify KPI Cards update
    await expect(page.locator('[data-testid="kpi-etc"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-eac"]')).toBeVisible();

    await saveArtifact(page, 'mutation-05-financial-forecast.png');
  });

  // --------------------------------------------------------------------------
  // Journey 06: Billing & Invoicing Lifecycle
  // --------------------------------------------------------------------------
  test('Journey 06 — Billable Charge Creation & Draft Invoicing Request', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}?tab=billing`);
    await page.waitForLoadState('domcontentloaded');

    // 1. Create Charge
    await page.locator('[data-testid="btn-create-charge"]').click();
    await expect(page.getByRole('heading', { name: /إضافة تكلفة قابلة للفوترة|إنشاء قيد فوترة جديد/i })).toBeVisible();

    const descInput = page.locator('input[type="text"]').first();
    await descInput.fill('دفعة إنجاز المرحلة الفنية الأولى');

    const billableInput = page.locator('input[type="number"]').last();
    await billableInput.fill('120000');

    const chargePromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/charges`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-charge"]').click();

    const chargeRes = await chargePromise;
    expect(chargeRes.status()).toBe(201);
    const chargeData = await chargeRes.json();
    const createdChargeId = chargeData.id;

    await expect(page.locator('[data-testid="kpi-billing-eligible"]')).toBeVisible({ timeout: 10000 });

    // 2. Request Invoice Draft
    await page.locator('[data-testid="btn-request-invoice"]').click();
    await expect(page.getByRole('heading', { name: /طلب إصدار فاتورة/i })).toBeVisible();

    const chargeIdInput = page.locator('[data-testid="input-charge-id"]');
    if (await chargeIdInput.count() > 0 && createdChargeId) {
      await chargeIdInput.first().fill(createdChargeId);
    }

    const requestedAmountInput = page.locator('input[type="number"]').first();
    await requestedAmountInput.fill('120000');

    const invoicePromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/invoice-requests`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-invoice-req"]').click();

    const invoiceRes = await invoicePromise;
    expect(invoiceRes.status()).toBe(202);

    await saveArtifact(page, 'mutation-06-billing-invoicing.png');
  });

  // --------------------------------------------------------------------------
  // Journey 07: Change Orders Workflow & Committee Decision
  // --------------------------------------------------------------------------
  test('Journey 07 — Change Order Creation & Approval Decision', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}?tab=changes`);
    await page.waitForLoadState('domcontentloaded');

    // 1. Create Change Request
    await page.locator('[data-testid="btn-new-change"]').click();
    await expect(page.getByRole('heading', { name: /تقديم طلب تغيير|طلب أمر تغيير جديد/i })).toBeVisible();

    const titleInput = page.locator('input[type="text"]').first();
    await titleInput.fill('إضافة تكامل مع بوابات الدفع الإلكتروني');

    const numberInputs = page.locator('input[type="number"]');
    if (await numberInputs.count() >= 3) {
      await numberInputs.nth(2).fill('14'); // Schedule Delta (14 days)
    }

    const changePromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/changes`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-change"]').click();

    const changeRes = await changePromise;
    expect(changeRes.status()).toBe(201);

    await expect(page.getByText('إضافة تكامل مع بوابات الدفع الإلكتروني')).toBeVisible({ timeout: 10000 });

    // 2. Decide / Approve Change Request
    await page.locator('[data-testid="btn-open-decide-change"]').first().click();
    await expect(page.getByRole('heading', { name: /اعتماد أو رفض طلب التغيير|اتخاذ قرار بشأن أمر التغيير/i })).toBeVisible();

    const decidePromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/changes/`) && res.url().includes('/decision') && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-decide-approve"]').click();

    const decideRes = await decidePromise;
    expect(decideRes.status()).toBe(200);

    await saveArtifact(page, 'mutation-07-change-orders.png');
  });

  // --------------------------------------------------------------------------
  // Journey 08: CIP Capital Handoff on Live Capital Project
  // --------------------------------------------------------------------------
  test('Journey 08 — Capital Asset Handoff on Live Capital Project', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // Pre-create an unhanded-off Cost Fact on the seeded capital project
    const csrfToken = await getCsrfToken(page);
    const factId = `cf-e2e-${Date.now()}`;
    const factRes = await page.request.post(`/api/v1/projects/${SEEDED_CAPITAL_PROJECT_ID}/cost-facts`, {
      data: {
        projectId: SEEDED_CAPITAL_PROJECT_ID,
        sourceModule: 'fixed-assets',
        sourceType: 'ConstructionInProgress',
        sourceId: factId,
        costCategory: 'Equipment',
        originalCurrency: 'EGP',
        originalAmount: 150000,
        baseCurrency: 'EGP',
        baseAmount: 150000,
        factDate: new Date().toISOString().slice(0, 10),
        idempotencyKey: `cf-fact-${Date.now()}`
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(factRes.status()).toBe(201);
    const factData = await factRes.json();
    const costFactId = factData.id;

    await page.goto(`/projects/${SEEDED_CAPITAL_PROJECT_ID}?tab=capital`);
    await page.waitForLoadState('domcontentloaded');

    // Click Capital Handoff
    await page.locator('[data-testid="btn-capital-handoff"]').click();
    await expect(page.getByRole('heading', { name: /تسليم التكاليف الرأسمالية|تسليم الأصول الرأسمالية/i })).toBeVisible();

    const costFactInput = page.locator('input[type="text"]').first();
    await costFactInput.fill(costFactId);

    const amountInput = page.locator('input[type="number"]').first();
    await amountInput.fill('150000');

    const handoffPromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${SEEDED_CAPITAL_PROJECT_ID}/capital-handoff`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-capital-handoff"]').click();

    const handoffRes = await handoffPromise;
    expect(handoffRes.status()).toBe(202);

    await saveArtifact(page, 'mutation-08-capital-handoff.png');
  });

  // --------------------------------------------------------------------------
  // Journey 09: Financial Close Checklist & Exception Resolution
  // --------------------------------------------------------------------------
  test('Journey 09 — Close Checklist Verification & Close Exception Resolution', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}?tab=closing`);
    await page.waitForLoadState('domcontentloaded');

    // 1. Add Close Exception
    await page.locator('[data-testid="btn-add-exception"]').click();
    await expect(page.getByRole('heading', { name: /تسجيل استثناء إغلاق|إضافة استثناء إغلاق/i })).toBeVisible();

    const messageInput = page.locator('textarea').first();
    await messageInput.fill('تسوية عهدة تشغيلية مؤقتة قيد التدقيق المحاسبي');

    const exPromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/close-exceptions`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-close-exception"]').click();

    const exRes = await exPromise;
    expect(exRes.status()).toBe(201);

    await expect(page.getByText('تسوية عهدة تشغيلية مؤقتة قيد التدقيق المحاسبي')).toBeVisible({ timeout: 10000 });

    // 2. Resolve Close Exception
    await expect(page.locator('[data-testid="btn-resolve-exception"]').first()).toBeVisible({ timeout: 10000 });
    const resolvePromise = page.waitForResponse(
      (res) => res.url().includes('/resolve') && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-resolve-exception"]').first().click();

    const resolveRes = await resolvePromise;
    expect(resolveRes.status()).toBe(200);

    await saveArtifact(page, 'mutation-09-close-readiness-resolved.png');
  });

  // --------------------------------------------------------------------------
  // Journey 10: Negative Authorization & Cross-Entity Isolation
  // --------------------------------------------------------------------------
  test('Journey 10 — Negative Authorization, Legal Entity Scope Isolation & Safe 404 Handling', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    // 1. Attempt to create project with mismatched legal entity scope
    const csrfToken = await getCsrfToken(page);
    const mismatchRes = await page.request.post(`/api/v1/projects/legal-entities/${ATLAS_ENTITY_ID}`, {
      data: {
        legalEntityId: DELTA_ENTITY_ID, // Mismatched entity scope
        name: 'Unauthorized Mismatched Project',
        projectType: 'InternalProject',
        startDate: '2026-11-01',
        billingModel: 'NonBillable',
        idempotencyKey: `neg-auth-${Date.now()}`
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(mismatchRes.status()).toBe(400);
    const errBody = await mismatchRes.json();
    expect(errBody.code).toBe('projects.entity_scope_invalid');

    // 2. Non-existent Project ID should display clean friendly empty state
    await page.goto('/projects/00000000-0000-0000-0000-000000000000');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByText('لم يتم العثور على مشاريع')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /العودة لقائمة المشاريع|رجوع/i })).toBeVisible();

    await saveArtifact(page, 'mutation-10-negative-auth-isolation.png');
  });
});
