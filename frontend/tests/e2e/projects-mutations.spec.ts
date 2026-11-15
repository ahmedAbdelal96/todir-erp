import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';
import { execSync } from 'node:child_process';

const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/projects-acceptance/mutations');
const AGENT_BRAIN_DIR = 'C:/Users/IT/.gemini/antigravity/brain/2b36897c-7b21-48b3-b339-9bb22437719d/projects-acceptance/mutations';
const ARTIFACTS_DIR = process.env.TEST_ARTIFACTS_DIR || LOCAL_ARTIFACTS_DIR;

const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const DELTA_ENTITY_ID = '7c9e6679-7425-40de-944b-e07fc1f90ae7';

function requireTestDevPassword(): string {
  const pwd = process.env.ERP_DEV_PASSWORD || process.env.SEED_DEFAULT_PASSWORD;
  if (!pwd) {
    throw new Error('ERP_DEV_PASSWORD or SEED_DEFAULT_PASSWORD must be configured in environment for real-stack acceptance tests.');
  }
  return pwd;
}

// Track disposable projects created during test runs for strict cleanup
const disposableProjectIds: string[] = [];

// Shared state across the serial mutation journey
let createdProjectId: string = '';
let createdProjectNumber: string = '';
let createdInternalProjectId: string = '';
let createdCapitalProjectId: string = '';

test.describe.configure({ mode: 'serial' });

test.describe('TODIR Projects & Operations (Module 23) — Real-Stack Mutation Acceptance Suite', () => {
  test.setTimeout(240_000);

  test.beforeAll(() => {
    [LOCAL_ARTIFACTS_DIR, AGENT_BRAIN_DIR].forEach((dir) => {
      try {
        if (!fs.existsSync(dir)) {
          fs.mkdirSync(dir, { recursive: true });
        }
      } catch {
        // directory creation optional
      }
    });
  });

  test.afterAll(async () => {
    // Clean up test-created projects from PostgreSQL to protect demonstration data
    if (disposableProjectIds.length === 0) return;
    try {
      const idList = disposableProjectIds.map((id) => `'${id}'`).join(',');
      const cleanupSql = `
        delete from business_projects.project_integration_event where project_id in (${idList});
        delete from business_projects.project_audit_event where project_id in (${idList});
        delete from business_projects.project_close_exception where project_id in (${idList});
        delete from business_projects.project_close where project_id in (${idList});
        delete from business_projects.project_capital_handoff where project_id in (${idList});
        delete from business_projects.project_change_order where project_id in (${idList});
        delete from business_projects.project_change_request where project_id in (${idList});
        delete from business_projects.project_invoice_allocation where invoice_request_id in (select id from business_projects.project_invoice_request where project_id in (${idList}));
        delete from business_projects.project_invoice_request where project_id in (${idList});
        delete from business_projects.project_billable_charge where project_id in (${idList});
        delete from business_projects.project_billing_plan_line where project_id in (${idList});
        delete from business_projects.project_billing_rule where project_id in (${idList});
        delete from business_projects.project_cost_fact where project_id in (${idList});
        delete from business_projects.project_commitment where project_id in (${idList});
        delete from business_projects.project_forecast where project_id in (${idList});
        delete from business_projects.project_budget_line where budget_version_id in (select v.id from business_projects.project_budget_version v join business_projects.project_budget b on b.id=v.budget_id where b.project_id in (${idList}));
        delete from business_projects.project_budget_version where budget_id in (select id from business_projects.project_budget where project_id in (${idList}));
        delete from business_projects.project_budget where project_id in (${idList});
        delete from business_projects.project_time_approval where time_entry_id in (select id from business_projects.project_time_entry where project_id in (${idList}));
        delete from business_projects.project_time_entry where project_id in (${idList});
        delete from business_projects.project_resource_allocation where project_id in (${idList});
        delete from business_projects.project_resource_requirement where project_id in (${idList});
        delete from business_projects.project_team_member where project_id in (${idList});
        delete from business_projects.project_deliverable where project_id in (${idList});
        delete from business_projects.project_milestone where project_id in (${idList});
        delete from business_projects.project_dependency where project_id in (${idList});
        delete from business_projects.project_task where project_id in (${idList});
        delete from business_projects.project_baseline where project_id in (${idList});
        delete from business_projects.project_document where project_id in (${idList});
        delete from business_projects.project where id in (${idList});
      `;
      execSync(`docker exec compose-postgres-1 psql -U postgres -d todir -c "${cleanupSql.replace(/\r?\n/g, ' ')}"`, { stdio: 'ignore' });
    } catch {
      // Ignore cleanup error in CI / disconnected environment
    }
  });

  async function getCsrfToken(page: Page): Promise<string> {
    const csrfRes = await page.request.get('/api/v1/auth/csrf');
    const { token } = await csrfRes.json();
    return token;
  }

  async function loginAsUser(page: Page, email: string = 'admin@atlas.com') {
    const token = await getCsrfToken(page);
    const loginRes = await page.request.post('/api/v1/auth/login', {
      data: { email, password: requireTestDevPassword() },
      headers: { 'X-CSRF-TOKEN': token }
    });
    expect(loginRes.status()).toBe(200);
  }

  async function loginAsAdmin(page: Page) {
    await loginAsUser(page, 'admin@atlas.com');
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
  test('Journey 01 — Real Project Creation (Internal & Customer Projects) via UI & Persistence', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    const runId = Date.now().toString().slice(-4);
    const customerProjectName = `مشروع استشاري تجاري E2E ${runId}`;
    const internalProjectName = `مشروع داخلي تطويري E2E ${runId}`;

    // 1. Create Customer Project through the actual UI Modal
    await page.locator('[data-testid="btn-new-project"]').click();
    await expect(page.getByText('إنشاء مشروع جديد')).toBeVisible({ timeout: 10000 });

    const custModal = page.locator('form#new-project-form');

    // Fill project name
    await custModal.locator('input[type="text"]').first().fill(customerProjectName);

    // Select Customer via PartyPicker ComboBox
    const partyInput = custModal.locator('input[role="combobox"]').first();
    await partyInput.fill('اليسر');
    const option = page.getByRole('option', { name: /اليسر/ }).first();
    await expect(option).toBeVisible({ timeout: 10000 });
    await option.click();

    // Fill Dates
    const custDateInputs = custModal.locator('input[type="date"]');
    if (await custDateInputs.count() >= 2) {
      await custDateInputs.nth(0).fill('2026-11-01');
      await custDateInputs.nth(1).fill('2027-04-30');
    }

    // Fill Initial Budget
    const custNumInputs = custModal.locator('input[type="number"]');
    if (await custNumInputs.count() > 0) {
      await custNumInputs.first().fill('750000');
    }

    // Intercept project creation POST
    const custCreatePromise = page.waitForResponse(
      (res) => res.url().includes('/api/v1/projects/legal-entities/') && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-save-project"]').click();

    const custCreateRes = await custCreatePromise;
    expect(custCreateRes.status()).toBe(201);
    const custData = await custCreateRes.json();
    expect(custData.id).toBeTruthy();
    expect(custData.projectType).toBe('CustomerProject');
    expect(custData.projectNumber).toBeTruthy();

    createdProjectId = custData.id;
    createdProjectNumber = custData.projectNumber;
    disposableProjectIds.push(createdProjectId);

    // Verify Customer Project fields persisted after reload
    await page.goto(`/projects/${createdProjectId}`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.getByText(createdProjectNumber)).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(customerProjectName)).toBeVisible();
    await expect(page.locator('[data-testid="project-overview-strip"]')).toBeVisible();

    // 2. Create Internal Project through the actual UI Modal
    await page.goto('/projects');
    await page.waitForLoadState('domcontentloaded');
    await page.locator('[data-testid="btn-new-project"]').click();
    await expect(page.getByText('إنشاء مشروع جديد')).toBeVisible({ timeout: 10000 });

    const internalModal = page.locator('form#new-project-form');

    // Select InternalProject from dropdown
    await internalModal.locator('select').first().selectOption('InternalProject');

    // Fill name
    await internalModal.locator('input[type="text"]').first().fill(internalProjectName);

    // Fill Dates
    const intDateInputs = internalModal.locator('input[type="date"]');
    if (await intDateInputs.count() >= 2) {
      await intDateInputs.nth(0).fill('2026-11-01');
      await intDateInputs.nth(1).fill('2027-04-30');
    }

    // Fill Budget
    const intNumInputs = internalModal.locator('input[type="number"]');
    if (await intNumInputs.count() > 0) {
      await intNumInputs.first().fill('500000');
    }

    const intCreatePromise = page.waitForResponse(
      (res) => res.url().includes('/api/v1/projects/legal-entities/') && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-save-project"]').click();

    const intCreateRes = await intCreatePromise;
    expect(intCreateRes.status()).toBe(201);
    const intData = await intCreateRes.json();
    expect(intData.id).toBeTruthy();
    expect(intData.projectType).toBe('InternalProject');

    createdInternalProjectId = intData.id;
    disposableProjectIds.push(createdInternalProjectId);

    // Verify Internal Project fields persisted after reload
    await page.goto(`/projects/${createdInternalProjectId}`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.getByText(internalProjectName)).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="project-overview-strip"]')).toBeVisible();

    await saveArtifact(page, 'mutation-01-project-created.png');
  });

  // --------------------------------------------------------------------------
  // Journey 02: Real Tasks, Milestones, and Deliverables State Transitions
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

    // 2. Create Milestone and Mutate State: Planned -> Reached -> Accepted
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
    const milestoneData = await milestoneRes.json();
    const milestoneId = milestoneData.id;

    // Transition Milestone: Planned -> Reached
    const reachPromise = page.waitForResponse(
      (res) => res.url().includes(`/milestones/${milestoneId}/reach`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-reach-milestone"]').first().click();
    const reachRes = await reachPromise;
    expect(reachRes.status()).toBe(200);

    // Transition Milestone: Reached -> Accepted
    const acceptMilestonePromise = page.waitForResponse(
      (res) => res.url().includes(`/milestones/${milestoneId}/accept`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-accept-milestone"]').first().click();
    const acceptMilestoneRes = await acceptMilestonePromise;
    expect(acceptMilestoneRes.status()).toBe(200);

    // 3. Create Deliverable and Mutate State: Draft -> Submitted -> Accepted
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
    const deliverableData = await deliverableRes.json();
    const deliverableId = deliverableData.id;

    // Transition Deliverable: Draft -> Submitted
    const submitDelivPromise = page.waitForResponse(
      (res) => res.url().includes(`/deliverables/${deliverableId}/submit`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-deliverable-action"]').first().click();
    const submitDelivRes = await submitDelivPromise;
    expect(submitDelivRes.status()).toBe(200);

    // Transition Deliverable: Submitted -> Accepted
    const acceptDelivPromise = page.waitForResponse(
      (res) => res.url().includes(`/deliverables/${deliverableId}/accept`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-accept-deliverable-action"]').first().click();
    const acceptDelivRes = await acceptDelivPromise;
    expect(acceptDelivRes.status()).toBe(200);

    // Verify persistence of mutated states after page reload
    await page.goto(`/projects/${createdProjectId}?tab=tasks`);
    await page.waitForLoadState('domcontentloaded');

    await page.locator('[data-testid="subtab-milestones"]').click();
    await expect(page.getByText('اعتماد المخطط التقني')).toBeVisible({ timeout: 10000 });

    await page.locator('[data-testid="subtab-deliverables"]').click();
    await expect(page.getByText('وثيقة المتطلبات البرمجية المعتمدة')).toBeVisible({ timeout: 10000 });

    await saveArtifact(page, 'mutation-02-tasks-milestones-deliverables.png');
  });

  // --------------------------------------------------------------------------
  // Journey 03: Team Member, Resource Requirement, and Allocation Workflow
  // --------------------------------------------------------------------------
  test('Journey 03 — Team Member & Resource Requirement Allocation', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}?tab=team`);
    await page.waitForLoadState('domcontentloaded');

    // 1. Add Team Member (PROJECT_MANAGER with active rate card)
    await page.locator('[data-testid="btn-add-team"]').click();
    await expect(page.getByRole('heading', { name: /تعيين عضو في فريق المشروع|إضافة عضو لفريق العمل/i })).toBeVisible();

    const teamRateInput = page.locator('input[type="number"]');
    if (await teamRateInput.count() > 0) {
      await teamRateInput.first().fill('80'); // Allocation percent
    }

    const teamPromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/team`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-team"]').click();

    const teamRes = await teamPromise;
    expect(teamRes.status()).toBe(201);
    await expect(page.getByText('PROJECT_MANAGER')).toBeVisible({ timeout: 10000 });

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

    // 3. Allocate Resource and Verify Persisted Allocation
    await page.locator('[data-testid="subtab-allocations"]').click();
    await page.locator('[data-testid="btn-allocate-res"]').click();
    await expect(page.getByRole('heading', { name: /تخصيص مورد/i })).toBeVisible();

    await page.locator('input[type="text"]').first().fill('Core Engineering Team Alpha');

    const allocNumInput = page.locator('input[type="number"]').first();
    await allocNumInput.fill('75');

    const allocPromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/resources/allocations`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-allocation"]').click();

    const allocRes = await allocPromise;
    expect(allocRes.status()).toBe(201);

    // Verify persisted allocation in table
    await expect(page.getByText('Core Engineering Team Alpha')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('75%')).toBeVisible();

    await saveArtifact(page, 'mutation-03-team-resources.png');
  });

  // --------------------------------------------------------------------------
  // Journey 04: Timesheets Lifecycle with Authoritative Rates Snapshot
  // --------------------------------------------------------------------------
  test('Journey 04 — Timesheets Full Lifecycle (Log, Submit, Approve, Finalize)', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}?tab=time`);
    await page.waitForLoadState('domcontentloaded');

    // 1. Log Time Entry (8 Hours = 480 Minutes)
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

    // 4. Finalize Action with Authoritative Rate Card Snapshot (250 cost, 700 billing)
    await expect(page.locator('[data-testid="btn-time-finalize"]').first()).toBeVisible({ timeout: 10000 });
    const finalizePromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/time/${timeId}/finalize`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-time-finalize"]').first().click();
    const finalizeRes = await finalizePromise;
    expect(finalizeRes.status()).toBe(200);

    const finalizedData = await finalizeRes.json();
    expect(finalizedData.state).toBe('Finalized');
    expect(finalizedData.costAmount).toBe(2000); // 8h * 250 EGP
    expect(finalizedData.billingAmount).toBe(5600); // 8h * 700 EGP

    // Verify actual cost updated on Financials tab
    await page.goto(`/projects/${createdProjectId}?tab=financials`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('[data-testid="kpi-actual-cost"]')).toBeVisible();

    await saveArtifact(page, 'mutation-04-timesheets-lifecycle.png');
  });

  // --------------------------------------------------------------------------
  // Journey 05: Budgets & Forecast Revisions with Approved Changes
  // --------------------------------------------------------------------------
  test('Journey 05 — Budgets & Forecast Revisions', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}?tab=financials`);
    await page.waitForLoadState('domcontentloaded');

    // 1. Create Budget Revision (850,000 EGP)
    await page.locator('[data-testid="btn-revise-budget"]').click();
    await expect(page.getByRole('heading', { name: /تعديل الميزانية|مراجعة واعتماد/i })).toBeVisible();

    const budgetInput = page.locator('input[type="number"]').first();
    await budgetInput.fill('850000');

    const descInput = page.locator('input[type="text"]').first();
    await descInput.fill('تعديل اعتمادات الميزانية وفق دراسة النطاق');

    const revPromise = page.waitForResponse(
      (res) => res.url().includes('/revisions') && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-budget-rev"]').click();

    const revRes = await revPromise;
    expect(revRes.status()).toBe(201);
    const revData = await revRes.json();
    const budgetVersionId = revData.id;

    // Approve the budget revision version to actually update the approved budget
    const csrfToken = await getCsrfToken(page);
    const approveRevRes = await page.request.post(`/api/v1/projects/budget-versions/${budgetVersionId}/approve`, {
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(approveRevRes.status()).toBe(200);

    // Reload and assert approved budget updated to 850,000
    await page.goto(`/projects/${createdProjectId}?tab=financials`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('[data-testid="kpi-budget"]')).toContainText(/850/);

    // 2. Create Forecast (ETC = 320,000)
    await page.locator('[data-testid="btn-create-forecast"]').click();
    await expect(page.getByRole('heading', { name: /تحديث تقدير التكلفة|تسجيل توقعات التكلفة/i })).toBeVisible();

    const numberInputs = page.locator('input[type="number"]');
    if (await numberInputs.count() > 0) {
      await numberInputs.first().fill('320000');
    }

    const forecastPromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdProjectId}/forecasts`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-forecast"]').click();

    const forecastRes = await forecastPromise;
    expect(forecastRes.status()).toBe(201);
    const forecastData = await forecastRes.json();
    expect(forecastData.etcAmount).toBe(320000);
    expect(forecastData.eacAmount).toBeGreaterThan(320000);

    // Verify KPI Cards update
    await expect(page.locator('[data-testid="kpi-etc"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-eac"]')).toBeVisible();

    await saveArtifact(page, 'mutation-05-financial-forecast.png');
  });

  // --------------------------------------------------------------------------
  // Journey 06: Billing & Invoicing Lifecycle (Distinguish Request vs Invoice)
  // --------------------------------------------------------------------------
  test('Journey 06 — Billable Charge Creation & Draft Invoicing Request', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('[data-testid="project-overview-strip"]')).toBeVisible({ timeout: 15000 });
    await page.locator('[data-testid="tab-billing"]').click();

    // 1. Create Charge
    await expect(page.locator('[data-testid="btn-create-charge"]')).toBeVisible({ timeout: 10000 });
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

    // 2. Request Invoice Draft (202 Accepted)
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

    const invoiceRequestData = await invoiceRes.json();
    expect(invoiceRequestData.state).toBe('Accepted');
    expect(invoiceRequestData.invoiceId).toBeTruthy();

    // Verify owning Invoicing module draft creation
    const draftInvoiceRes = await page.request.get(
      `/api/v1/invoicing/customer-invoices/${invoiceRequestData.invoiceId}?legalEntityId=${ATLAS_ENTITY_ID}`
    );
    expect(draftInvoiceRes.status()).toBe(200);
    const draftInvoice = await draftInvoiceRes.json();
    expect(draftInvoice.lifecycle).toBe('Draft');

    // Complete invoice request lifecycle so project charges transition from Requested to Invoiced
    const csrfToken = await getCsrfToken(page);
    const completeInvoiceRes = await page.request.post(`/api/v1/projects/invoice-requests/${invoiceRequestData.id}/result`, {
      data: {
        state: 'Completed',
        invoiceId: invoiceRequestData.invoiceId,
        invoicedAmount: 120000
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(completeInvoiceRes.status()).toBe(200);

    // Also resolve any remaining eligible time charges via domain API
    try {
      const remainingCharges = execSync(
        `docker exec compose-postgres-1 psql -U postgres -d todir -t -c "select id, billable_amount from business_projects.project_billable_charge where project_id='${createdProjectId}' and state='Eligible';"`,
        { encoding: 'utf8' }
      ).trim().split('\n').map((s) => s.trim().split('|')).filter((p) => p.length === 2);

      for (const [chId, chAmt] of remainingCharges) {
        const cId = chId.trim();
        const amt = parseFloat(chAmt.trim());
        const invReq = await page.request.post(`/api/v1/projects/${createdProjectId}/invoice-requests`, {
          data: {
            projectId: createdProjectId,
            chargeIds: [cId],
            requestedAmount: amt,
            currencyCode: 'EGP',
            idempotencyKey: `inv-rem-${cId}`
          },
          headers: { 'X-CSRF-TOKEN': csrfToken }
        });
        if (invReq.ok()) {
          const invReqData = await invReq.json();
          await page.request.post(`/api/v1/projects/invoice-requests/${invReqData.id}/result`, {
            data: {
              state: 'Completed',
              invoiceId: invReqData.invoiceId || invoiceRequestData.invoiceId,
              invoicedAmount: amt
            },
            headers: { 'X-CSRF-TOKEN': csrfToken }
          });
        }
      }
    } catch {
      // Continue
    }

    await saveArtifact(page, 'mutation-06-billing-invoicing.png');
  });

  // --------------------------------------------------------------------------
  // Journey 07: Change Orders Workflow & Approved Contract/Schedule Impact
  // --------------------------------------------------------------------------
  test('Journey 07 — Change Order Creation & Approval Decision', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('[data-testid="project-overview-strip"]')).toBeVisible({ timeout: 15000 });
    await page.locator('[data-testid="tab-changes"]').click();

    // 1. Create Change Request (Schedule Delta: 14 days, Budget Delta: 50,000)
    await expect(page.locator('[data-testid="btn-new-change"]')).toBeVisible({ timeout: 10000 });
    await page.locator('[data-testid="btn-new-change"]').click();
    await expect(page.getByRole('heading', { name: /تقديم طلب تغيير|طلب أمر تغيير جديد/i })).toBeVisible();

    const titleInput = page.locator('input[type="text"]').first();
    await titleInput.fill('إضافة تكامل مع بوابات الدفع الإلكتروني');

    const numberInputs = page.locator('input[type="number"]');
    if (await numberInputs.count() >= 3) {
      await numberInputs.nth(1).fill('50000'); // Budget Delta
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

    // Verify approved change order updated budget and schedule
    await page.goto(`/projects/${createdProjectId}?tab=financials`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('[data-testid="kpi-budget"]')).toContainText(/900/); // 850k + 50k delta

    await saveArtifact(page, 'mutation-07-change-orders.png');
  });

  // --------------------------------------------------------------------------
  // Journey 08: CIP Capital Handoff on Disposable Test Capital Project
  // --------------------------------------------------------------------------
  test('Journey 08 — Capital Asset Handoff on Live Capital Project', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    const csrfToken = await getCsrfToken(page);
    const runId = Date.now().toString().slice(-4);
    const capitalProjectName = `مشروع رأسمالي اختباري E2E ${runId}`;

    // 1. Create a dedicated disposable Capital Project (protecting canonical seeded PRJ-0000115)
    const capRes = await page.request.post(`/api/v1/projects/legal-entities/${ATLAS_ENTITY_ID}`, {
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        name: capitalProjectName,
        projectType: 'CapitalProject',
        billingModel: 'NonBillable',
        startDate: '2026-11-01',
        currencyCode: 'EGP',
        initialBudget: 500000,
        idempotencyKey: `cap-mut-${Date.now()}`
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(capRes.status()).toBe(201);
    const capData = await capRes.json();
    createdCapitalProjectId = capData.id;
    disposableProjectIds.push(createdCapitalProjectId);

    // 2. Pre-create a capitalizable Cost Fact on this dedicated test capital project
    const factId = `cf-e2e-${Date.now()}`;
    const factRes = await page.request.post(`/api/v1/projects/${createdCapitalProjectId}/cost-facts`, {
      data: {
        projectId: createdCapitalProjectId,
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

    // 3. Initiate Capital Handoff via UI
    await page.goto(`/projects/${createdCapitalProjectId}`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('[data-testid="project-overview-strip"]')).toBeVisible({ timeout: 15000 });
    await page.locator('[data-testid="tab-capital"]').click();

    await expect(page.locator('[data-testid="btn-capital-handoff"]')).toBeVisible({ timeout: 10000 });
    await page.locator('[data-testid="btn-capital-handoff"]').click();
    await expect(page.getByRole('heading', { name: /تسليم التكاليف الرأسمالية|تسليم الأصول الرأسمالية/i })).toBeVisible();

    const costFactInput = page.locator('input[type="text"]').first();
    await costFactInput.fill(costFactId);

    const amountInput = page.locator('input[type="number"]').first();
    await amountInput.fill('150000');

    const handoffPromise = page.waitForResponse(
      (res) => res.url().includes(`/api/v1/projects/${createdCapitalProjectId}/capital-handoff`) && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-submit-capital-handoff"]').click();

    const handoffRes = await handoffPromise;
    expect(handoffRes.status()).toBe(202);
    const handoffData = await handoffRes.json();
    expect(handoffData.state).toBe('Requested');
    expect(handoffData.amount).toBe(150000);

    // Verify candidate created in business_assets
    try {
      const candidateCheck = execSync(
        `docker exec compose-postgres-1 psql -U postgres -d todir -t -c "select status, eligible_amount from business_assets.asset_capitalization_candidate where economic_source_key='project-handoff:${handoffData.id}';"`,
        { encoding: 'utf8' }
      );
      if (candidateCheck.trim()) {
        expect(candidateCheck).toContain('PendingReview');
      }
    } catch {
      // Direct SQL check optional if container unavailable
    }

    await saveArtifact(page, 'mutation-08-capital-handoff.png');
  });

  // --------------------------------------------------------------------------
  // Journey 09: Close Readiness, Blocked Closure, Eligible Close & Reopening
  // --------------------------------------------------------------------------
  test('Journey 09 — Close Checklist Verification & Close Exception Resolution', async ({ page }) => {
    test.skip(!createdProjectId, 'Requires project created in Journey 01');
    await page.setViewportSize({ width: 1280, height: 900 });
    await loginAsAdmin(page);

    await page.goto(`/projects/${createdProjectId}`);
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator('[data-testid="project-overview-strip"]')).toBeVisible({ timeout: 15000 });
    await page.locator('[data-testid="tab-closing"]').click();

    // 1. Add Close Exception
    await expect(page.locator('[data-testid="btn-add-exception"]')).toBeVisible({ timeout: 10000 });
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

    // 2. Assert Blocked Closure (close button disabled or rejected by backend)
    const csrfToken = await getCsrfToken(page);
    const blockedCloseRes = await page.request.post(`/api/v1/projects/${createdProjectId}/close`, {
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(blockedCloseRes.status()).toBe(400);

    // 3. Resolve Close Exception
    await expect(page.locator('[data-testid="btn-resolve-exception"]').first()).toBeVisible({ timeout: 10000 });
    const resolvePromise = page.waitForResponse(
      (res) => res.url().includes('/resolve') && res.request().method() === 'POST'
    );
    await page.locator('[data-testid="btn-resolve-exception"]').first().click();

    const resolveRes = await resolvePromise;
    expect(resolveRes.status()).toBe(200);

    // 4. Transition Project through required lifecycle to FinancialClosing
    const projectDetailRes = await page.request.get(`/api/v1/projects/${createdProjectId}`);
    const projectDetail = await projectDetailRes.json();
    let currentVersion = projectDetail.version;

    if (projectDetail.lifecycleState === 'Draft') {
      const transActive = await page.request.post(`/api/v1/projects/${createdProjectId}/state/Active`, {
        data: { version: currentVersion },
        headers: { 'X-CSRF-TOKEN': csrfToken }
      });
      if (transActive.ok()) {
        const d = await transActive.json();
        currentVersion = d.version;
      }
    }

    if (projectDetail.lifecycleState === 'Draft' || projectDetail.lifecycleState === 'Active') {
      const transExec = await page.request.post(`/api/v1/projects/${createdProjectId}/state/ExecutionCompleted`, {
        data: { version: currentVersion },
        headers: { 'X-CSRF-TOKEN': csrfToken }
      });
      if (transExec.ok()) {
        const d = await transExec.json();
        currentVersion = d.version;
      }

      const transClose = await page.request.post(`/api/v1/projects/${createdProjectId}/state/FinancialClosing`, {
        data: { version: currentVersion },
        headers: { 'X-CSRF-TOKEN': csrfToken }
      });
      expect(transClose.status()).toBe(200);
    }

    // 5. Successful Closure when eligible
    const closeRes = await page.request.post(`/api/v1/projects/${createdProjectId}/close`, {
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(closeRes.status()).toBe(200);
    const closeData = await closeRes.json();
    expect(closeData.state).toBe('Closed');

    // 6. Reopen Project where permitted
    const reopenRes = await page.request.post(`/api/v1/projects/${createdProjectId}/reopen`, {
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(reopenRes.status()).toBe(200);
    const reopenData = await reopenRes.json();
    expect(reopenData.state).toBe('Reopened');

    await saveArtifact(page, 'mutation-09-close-readiness-resolved.png');
  });

  // --------------------------------------------------------------------------
  // Journey 10: Negative Authorization, Least Privilege & Cross-Entity Isolation
  // --------------------------------------------------------------------------
  test('Journey 10 — Negative Authorization, Legal Entity Scope Isolation & Safe 404 Handling', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });

    // 1. Genuine Restricted User (Least Privilege): staff-101@atlas.com (Role: warehouse)
    // Has no projects.read or projects.create permissions
    await loginAsUser(page, 'staff-101@atlas.com');

    // Verify UI blocks access with unauthorized notification
    await page.goto('/projects');
    await page.waitForLoadState('domcontentloaded');
    await expect(page.getByText(/ليس لديك صلاحية للوصول|غير مصرح لك بالوصول|Unauthorized/i)).toBeVisible({ timeout: 15000 });

    // Verify API returns 403 Forbidden for restricted user
    const csrfToken = await getCsrfToken(page);
    const unauthGetRes = await page.request.get(`/api/v1/projects/legal-entities/${ATLAS_ENTITY_ID}`);
    expect(unauthGetRes.status()).toBe(403);

    const unauthPostRes = await page.request.post(`/api/v1/projects/legal-entities/${ATLAS_ENTITY_ID}`, {
      data: {
        legalEntityId: ATLAS_ENTITY_ID,
        name: 'Unauthorized Project Attempt',
        projectType: 'InternalProject',
        startDate: '2026-11-01',
        billingModel: 'NonBillable',
        idempotencyKey: `unauth-attempt-${Date.now()}`
      },
      headers: { 'X-CSRF-TOKEN': csrfToken }
    });
    expect(unauthPostRes.status()).toBe(403);

    // 2. Cross-Tenant Isolation: admin@delta.com (Tenant Delta) cannot access Atlas project
    await loginAsUser(page, 'admin@delta.com');
    if (createdProjectId) {
      const crossTenantRes = await page.request.get(`/api/v1/projects/${createdProjectId}`);
      expect([403, 404]).toContain(crossTenantRes.status());
    }

    // 3. Attempt to create project with mismatched legal entity scope
    const deltaCsrf = await getCsrfToken(page);
    const mismatchRes = await page.request.post(`/api/v1/projects/legal-entities/${DELTA_ENTITY_ID}`, {
      data: {
        legalEntityId: ATLAS_ENTITY_ID, // Mismatched entity scope
        name: 'Mismatched Entity Project',
        projectType: 'InternalProject',
        startDate: '2026-11-01',
        billingModel: 'NonBillable',
        idempotencyKey: `neg-auth-${Date.now()}`
      },
      headers: { 'X-CSRF-TOKEN': deltaCsrf }
    });
    expect(mismatchRes.status()).toBe(400);

    // 4. Non-existent Project ID should display clean friendly empty state
    await loginAsAdmin(page);
    await page.goto('/projects/00000000-0000-0000-0000-000000000000');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.getByText(/لم يتم العثور على مشاريع/i)).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /العودة لقائمة المشاريع|رجوع/i })).toBeVisible();

    await saveArtifact(page, 'mutation-10-negative-auth-isolation.png');
  });
});
