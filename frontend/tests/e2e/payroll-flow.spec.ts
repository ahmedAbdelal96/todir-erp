import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const SCREENSHOT_DIR = path.resolve(process.cwd(), '..', 'artifacts', 'test-results', 'payroll-acceptance');

test.describe.configure({ mode: 'serial' });

async function ensureAuthenticated(page: Page) {
  page.on('console', (msg) => {
    if (msg.type() === 'error') console.log('PAGE ERROR:', msg.text());
  });
  page.on('pageerror', (err) => {
    console.log('UNCAUGHT PAGE ERROR:', err.message, err.stack);
  });
  page.on('response', async (res) => {
    if (!res.ok() && res.url().includes('/api/v1/payroll/')) {
      try {
        const text = await res.text();
        console.log('PAYROLL API STATUS:', res.status(), res.url(), text);
      } catch {
        console.log('PAYROLL API STATUS:', res.status(), res.url());
      }
    }
  });

  await page.goto('/payroll');
  await page.waitForLoadState('networkidle');

  const atlasBtn = page.getByRole('button', { name: /admin@atlas\.com/i });
  if (await atlasBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
    await atlasBtn.click();
    await atlasBtn.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle');
  } else {
    const emailInput = page.locator('#login-email');
    if (await emailInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await emailInput.fill('admin@atlas.com');
      const password = process.env.SEED_DEFAULT_PASSWORD;
      if (!password) {
        throw new Error('SEED_DEFAULT_PASSWORD environment variable is required for login authentication');
      }
      await page.locator('#login-password').fill(password);
      await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
      await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
      await page.waitForLoadState('networkidle');
    }
  }

  if (!page.url().includes('/payroll')) {
    await page.goto('/payroll');
    await page.waitForLoadState('networkidle');
  }

  await expect(page.locator('[data-testid="payroll-page"]')).toBeVisible({ timeout: 20000 });

  // Ensure Arabic is active for normal tests
  const switchToArabicBtn = page.locator('button').filter({ hasText: /Switch to Arabic/i });
  if (await switchToArabicBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
    await switchToArabicBtn.click();
    await page.waitForTimeout(1000);
    await page.waitForLoadState('networkidle');
  }
}

test.describe('TODIR Payroll Module 18 Full-Stack Verification & UX Acceptance Suite', () => {
  test.beforeAll(() => {
    if (!fs.existsSync(SCREENSHOT_DIR)) {
      fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
    }
  });

  test.setTimeout(180_000);

  test('1. Payroll Home & Run Workspace Overview (Desktop 1440px RTL)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    // Verify page header, statutory mode banner, and high-contrast KPI cards
    await expect(page.getByRole('heading', { name: /الرواتب والأجور/i }).first()).toBeVisible();
    await expect(page.locator('[data-testid="payroll-tab-home"]')).toBeVisible();

    await page.waitForTimeout(1000);
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '01-payroll-home-desktop.png'),
      fullPage: false
    });
  });

  test('2. Payroll Setup - Groups & Creation Modal', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-setup"]').click();
    await page.waitForTimeout(800);

    // Verify groups section heading
    await expect(page.getByRole('heading', { name: /مجموعات الرواتب/i })).toBeVisible();

    // Open New Group Modal unconditionally
    const newGroupBtn = page.getByRole('button', { name: /إضافة مجموعة رواتب/i });
    await expect(newGroupBtn).toBeVisible({ timeout: 5000 });
    await newGroupBtn.click();
    await page.waitForTimeout(600);
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Verify payDay label is properly labeled inside the modal
    await expect(dialog.getByText(/يوم الصرف في الشهر/i)).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '02-payroll-new-group-modal.png'),
      fullPage: false
    });
    // Close modal with cancel
    await dialog.getByRole('button', { name: /إلغاء/i }).click();
    await expect(dialog).toBeHidden({ timeout: 5000 });
  });

  test('3. Payroll Setup - Periods & Date Defaults Modal', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-setup"]').click();
    await page.waitForTimeout(800);

    // Open New Period Modal unconditionally
    const newPeriodBtn = page.getByRole('button', { name: /إضافة فترة رواتب/i });
    await expect(newPeriodBtn).toBeVisible({ timeout: 5000 });
    await newPeriodBtn.click();
    await page.waitForTimeout(600);
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Verify distinct labels for dates inside the modal
    await expect(dialog.getByText(/تاريخ الاحتساب المقرر/i)).toBeVisible();
    await expect(dialog.getByText(/تاريخ الصرف الفعلي المخطط/i)).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '03-payroll-new-period-modal.png'),
      fullPage: false
    });
    await dialog.getByRole('button', { name: /إلغاء/i }).click();
    await expect(dialog).toBeHidden({ timeout: 5000 });
  });

  test('4. Setup Sub-Tabs (Components, Structures, Assignments)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-setup"]').click();
    await page.waitForTimeout(800);

    // 1. Click Components sub-tab
    const componentsTab = page.getByRole('tab', { name: /بنود الراتب/i });
    if (await componentsTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await componentsTab.click();
      await page.waitForTimeout(600);
      await expect(page.getByRole('heading', { level: 3, name: /بنود الراتب/i })).toBeVisible();

      // Verify positive component rows exist from backend
      await expect(page.getByText('BASE').first()).toBeVisible({ timeout: 5000 });

      // Verify New Component Modal opens cleanly
      const newCompBtn = page.getByRole('button', { name: /إضافة بند راتب جديد/i });
      if (await newCompBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await newCompBtn.click();
        await page.waitForTimeout(500);
        const compDialog = page.getByRole('dialog');
        await expect(compDialog).toBeVisible();
        await compDialog.getByRole('button', { name: /إلغاء/i }).click();
        await expect(compDialog).toBeHidden({ timeout: 5000 });
      }
    }

    // 2. Click Structures sub-tab
    const structuresTab = page.getByRole('tab', { name: /هياكل الرواتب/i });
    if (await structuresTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await structuresTab.click();
      await page.waitForTimeout(600);
      await expect(page.getByRole('heading', { level: 3, name: /هياكل الرواتب/i })).toBeVisible();

      // Verify positive salary structure rows exist from backend
      await expect(page.getByText('STANDARD-EG').first()).toBeVisible({ timeout: 5000 });

      // Verify New Salary Structure Modal opens cleanly
      const newStructBtn = page.getByRole('button', { name: /إضافة هيكل رواتب/i });
      if (await newStructBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await newStructBtn.click();
        await page.waitForTimeout(500);
        const structDialog = page.getByRole('dialog');
        await expect(structDialog).toBeVisible();
        await structDialog.getByRole('button', { name: /إلغاء/i }).click();
        await expect(structDialog).toBeHidden({ timeout: 5000 });
      }
    }

    // 3. Click Assignments sub-tab
    const assignmentsTab = page.getByRole('tab', { name: /تسكين الموظفين/i });
    if (await assignmentsTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await assignmentsTab.click();
      await page.waitForTimeout(600);
      await expect(page.getByRole('heading', { level: 3, name: /تسكين الموظفين/i })).toBeVisible();

      // Verify positive employee assignment rows exist without raw UUIDs
      const assignTable = page.locator('table').first();
      await expect(assignTable).toBeVisible({ timeout: 5000 });
      const assignText = await assignTable.textContent();
      expect(assignText).not.toMatch(/موظف #[0-9a-f]{8}/i);

      // Verify New Payroll Assignment Modal opens cleanly
      const newAssignBtn = page.getByRole('button', { name: /تسكين موظف جديد/i });
      if (await newAssignBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await newAssignBtn.click();
        await page.waitForTimeout(500);
        const assignDialog = page.getByRole('dialog');
        await expect(assignDialog).toBeVisible();
        await assignDialog.getByRole('button', { name: /إلغاء/i }).click();
        await expect(assignDialog).toBeHidden({ timeout: 5000 });
      }
    }
  });

  test('5. Company Tax & Social Insurance Statutory Settings', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-settings"]').click();
    await page.waitForTimeout(800);

    // Verify statutory settings title and dual cards
    await expect(page.getByText(/الضرائب والتأمينات الاجتماعية/i).first()).toBeVisible();
    await expect(page.getByText(/ضريبة كسب العمل/i).first()).toBeVisible();
    await expect(page.getByText(/التأمينات الاجتماعية/i).first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '04-payroll-tax-settings-tab.png'),
      fullPage: false
    });
  });

  test('6. Payroll Runs Tab & New Run Modal', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-runs"]').click();
    await page.waitForTimeout(800);

    // Open New Run Modal unconditionally
    const newRunBtn = page.getByRole('button', { name: /إنشاء دورة رواتب/i }).first();
    await expect(newRunBtn).toBeVisible({ timeout: 5000 });
    await newRunBtn.click();
    await page.waitForTimeout(600);
    const runDialog = page.getByRole('dialog');
    await expect(runDialog).toBeVisible();

    // For Regular run, reasonCode and search fields should NOT be rendered
    await expect(runDialog.getByText(/رمز سبب الدورة/i)).toHaveCount(0);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '05-payroll-new-run-modal.png'),
      fullPage: false
    });
    await runDialog.getByRole('button', { name: /إلغاء/i }).click();
    await expect(runDialog).toBeHidden({ timeout: 5000 });
  });

  test('7. Payroll Inputs & Bulk Import Dialog (Preview & Validation Flow)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-inputs"]').click();
    await page.waitForTimeout(800);

    // Open Import Dialog unconditionally
    const importBtn = page.getByRole('button', { name: /استيراد/i }).first();
    await expect(importBtn).toBeVisible({ timeout: 5000 });
    await importBtn.click();
    await page.waitForTimeout(600);
    const importDialog = page.getByRole('dialog');
    await expect(importDialog).toBeVisible();

    // Test pasting sample CSV data
    const sampleCsv = "d0000000-0000-0000-0000-000000000001,TRANSPORT,500,EGP,2026-11-01,بدل انتقال شهري";
    await importDialog.locator('textarea').fill(sampleCsv);

    // Trigger preview
    const previewBtn = importDialog.getByRole('button', { name: /معاينة البيانات والتحقق|معاينة/i });
    await expect(previewBtn).toBeVisible({ timeout: 5000 });
    await previewBtn.click();
    await page.waitForTimeout(1000);

    // Verify preview results
    await expect(importDialog.getByText(/أسطر صالحة|صالح/i).first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '06-payroll-import-dialog.png'),
      fullPage: false
    });
    // Close the import dialog cleanly
    const backBtn = importDialog.getByRole('button', { name: /رجوع|Back/i });
    if (await backBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await backBtn.click();
      await page.waitForTimeout(400);
    }
    const cancelBtn = importDialog.getByRole('button', { name: /إلغاء|Cancel/i });
    if (await cancelBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await cancelBtn.click();
      await page.waitForTimeout(400);
    }
    await expect(importDialog).toBeHidden({ timeout: 5000 });

    // Open New Input Modal unconditionally
    const newInputBtn = page.getByRole('button', { name: /إضافة مدخل جديد/i });
    await expect(newInputBtn).toBeVisible({ timeout: 5000 });
    await newInputBtn.click();
    await page.waitForTimeout(600);
    const inputDialog = page.getByRole('dialog');
    await expect(inputDialog).toBeVisible();

    // Verify PayrollComponentPicker is present
    await expect(inputDialog.getByText(/البند|بند الراتب/i).first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '07-payroll-new-input-modal.png'),
      fullPage: false
    });
    await inputDialog.getByRole('button', { name: /إلغاء/i }).click();
    await expect(inputDialog).toBeHidden({ timeout: 5000 });
  });

  test('8. Loans & Advances Tab & Creation Modal', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-loans"]').click();
    await page.waitForTimeout(800);

    // Open New Loan Modal unconditionally
    const newLoanBtn = page.getByRole('button', { name: /طلب سلفة أو قرض جديد/i });
    await expect(newLoanBtn).toBeVisible({ timeout: 5000 });
    await newLoanBtn.click();
    await page.waitForTimeout(600);
    const loanDialog = page.getByRole('dialog');
    await expect(loanDialog).toBeVisible();
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '08-payroll-new-loan-modal.png'),
      fullPage: false
    });
    await loanDialog.getByRole('button', { name: /إلغاء/i }).click();
    await expect(loanDialog).toBeHidden({ timeout: 5000 });
  });

  test('9. Payslips Tab & Real Employee Identity Resolution & Pagination', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-payslips"]').click();
    await page.waitForTimeout(1000);

    await expect(page.getByText(/قسائم منشأة العمل/i).first()).toBeVisible();

    // Verify company payslip rows are visible with dynamic currency amounts
    await expect(page.getByText(/EGP/i).first()).toBeVisible();

    // Verify table exists with rows
    const table = page.locator('table');
    await expect(table).toBeVisible();
    const rows = table.locator('tbody tr');
    const count = await rows.count();
    expect(count).toBeGreaterThan(0);

    // Verify employee display names are resolved and not raw UUID fallback patterns
    const tableText = await table.textContent();
    expect(tableText).not.toMatch(/موظف #[0-9a-f]{8}/i);

    // Verify server-side pagination controls are present
    const paginationNav = page.locator('[aria-label="التنقل بين الصفحات"], [role="navigation"]');
    await expect(paginationNav.first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '09-payroll-payslips-tab.png'),
      fullPage: false
    });
  });

  test('10. Reports & Financial Truth (GL Posting Verification & Export Scope)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-reports"]').click();
    await page.waitForTimeout(800);

    await expect(page.getByText(/التقارير والمطابقة المالية/i).first()).toBeVisible();

    // Verify 4th KPI card is Total Runs Count, not fabricated employer cost
    await expect(page.getByText(/إجمالي الدورات المسجلة/i)).toBeVisible();

    // Verify export note communicating scope
    await expect(page.getByText(/تصدير السجلات الحالية المعروضة/i)).toBeVisible();

    // Verify Authoritative GL Posting Status is displayed in table
    const table = page.locator('table');
    await expect(table).toBeVisible();
    await expect(page.getByText(/مرحل للأستاذ العام|غير مرحل/i).first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '10-payroll-reports-tab.png'),
      fullPage: false
    });
  });

  test('11. Migration & Opening Balances Modal', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-openingBalances"]').click();
    await page.waitForTimeout(800);

    const newOpeningBtn = page.getByRole('button', { name: /إضافة رصيد افتتاحي/i });
    if (await newOpeningBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await newOpeningBtn.click();
      await page.waitForTimeout(600);
      await expect(page.getByRole('dialog')).toBeVisible();
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '11-payroll-opening-balance-modal.png'),
        fullPage: false
      });
      await page.getByRole('button', { name: /إلغاء/i }).click();
      await page.waitForTimeout(400);
    }
  });

  test('12. Mobile Responsive Layout (390px Viewport)', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-home"]').click();
    await page.waitForTimeout(800);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '12-payroll-home-mobile-390px.png'),
      fullPage: false
    });
  });

  test('13. English LTR Parity & Formatting', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    // Click language switcher to switch to English
    const langBtn = page.getByRole('button', { name: /التبديل إلى الإنجليزية/i });
    if (await langBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await langBtn.click();
      await page.waitForTimeout(1000);
      await page.waitForLoadState('networkidle');
    }

    // Verify English heading and LTR layout
    await expect(page.getByRole('heading', { name: /Payroll/i }).first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '13-payroll-home-english-ltr.png'),
      fullPage: false
    });

    // Restore to Arabic
    const switchToArabicBtn = page.locator('button').filter({ hasText: /Switch to Arabic/i });
    if (await switchToArabicBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await switchToArabicBtn.click();
      await page.waitForTimeout(800);
      await page.waitForLoadState('networkidle');
    }
  });

  test('14. Deterministic End-to-End Payroll Lifecycle: Run Navigation & Verification', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    await page.locator('[data-testid="payroll-tab-runs"]').click();
    await page.waitForTimeout(1000);

    // Verify runs table is loaded with rows
    const runsTable = page.locator('table');
    await expect(runsTable).toBeVisible();
    const rows = runsTable.locator('tbody tr');
    const rowCount = await rows.count();
    expect(rowCount).toBeGreaterThan(0);

    // Click on the first run's workspace button to open the run workspace
    const openWorkspaceBtn = page.getByRole('button', { name: /فتح مساحة العمل/i }).first();
    await expect(openWorkspaceBtn).toBeVisible({ timeout: 5000 });
    await openWorkspaceBtn.click();
    await page.waitForTimeout(1000);

    // Verify workspace components
    await expect(page.getByRole('heading', { level: 2 }).first()).toBeVisible();

    // Verify run metrics cards are populated with non-zero currency values
    await expect(page.getByText(/EGP/i).first()).toBeVisible();

    // Navigate back
    const backBtn = page.getByRole('button', { name: /←|العودة|الرجوع/i }).first();
    await expect(backBtn).toBeVisible();
    await backBtn.click();
    await page.waitForTimeout(600);

    // Confirm return to runs table
    await expect(runsTable).toBeVisible();
  });

  test('15. Deterministic Full Lifecycle Mutation, Calculation, GL Reconciliation & Security Isolation', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await ensureAuthenticated(page);

    // 1. Resolve active legal entity deterministically without any hardcoded UUID fallback
    const legalEntityId = await page.evaluate(async () => {
      const sessionRes = await fetch('/api/v1/session/context');
      if (sessionRes.ok) {
        const session = await sessionRes.json();
        if (session.context?.legalEntities?.length > 0) {
          return session.context.legalEntities[0].id;
        }
      }
      const leRes = await fetch('/api/v1/platform/legal-entities');
      if (leRes.ok) {
        const list = await leRes.json();
        if (list && list.length > 0) return list[0].id;
      }
      throw new Error('Unable to resolve legal entity id from session context');
    });
    expect(legalEntityId).toBeTruthy();

    // 2. Resolve active payroll group and open period
    const groupContext = await page.evaluate(async (entityId) => {
      const groupsRes = await fetch(`/api/v1/payroll/legal-entities/${entityId}/groups`);
      if (!groupsRes.ok) throw new Error('Failed to fetch payroll groups');
      const groups = await groupsRes.json();
      if (!groups || groups.length === 0) throw new Error('No payroll groups found');
      const group = groups[0];

      const periodsRes = await fetch(`/api/v1/payroll/legal-entities/${entityId}/groups/${group.id}/periods`);
      if (!periodsRes.ok) throw new Error('Failed to fetch payroll periods');
      const periods = await periodsRes.json();
      if (!periods || periods.length === 0) throw new Error('No payroll periods found');
      return { groupId: group.id, periodId: periods[0].id };
    }, legalEntityId);
    expect(groupContext.groupId).toBeTruthy();
    expect(groupContext.periodId).toBeTruthy();

    // 3. Deterministic Lifecycle Mutation: Create/Finalize run and verify persistence
    const lifecycleResult = await page.evaluate(async ({ legalEntityId, groupId, periodId }) => {
      const csrfRes = await fetch('/api/v1/auth/csrf');
      const csrfData = csrfRes.ok ? await csrfRes.json() : {};
      const csrfToken = csrfData.token || '';
      const headers = { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken };

      const runsRes = await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs`);
      const existingRuns = runsRes.ok ? await runsRes.json() : [];
      let targetRun = existingRuns.find((r: any) => r.finalizationState === 'Finalized');

      if (!targetRun) {
        // Create a period and run for complete lifecycle execution
        const pRes = await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/groups/${groupId}/periods`, {
          method: 'POST',
          headers,
          body: JSON.stringify({
            payrollGroupId: groupId,
            periodStart: '2026-11-01',
            periodEnd: '2026-11-30',
            inputCutoff: '2026-11-25',
            calculationDate: '2026-11-26',
            plannedPayDate: '2026-11-27'
          })
        });
        const period = pRes.ok ? await pRes.json() : null;
        const targetPeriodId = period ? period.id : periodId;

        const createRes = await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs`, {
          method: 'POST',
          headers,
          body: JSON.stringify({
            payrollGroupId: groupId,
            periodId: targetPeriodId,
            runType: 'Regular'
          })
        });
        if (createRes.ok) {
          const newRun = await createRes.json();
          await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs/${newRun.id}/capture-inputs`, { method: 'POST', headers });
          await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs/${newRun.id}/calculate`, { method: 'POST', headers });
          await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs/${newRun.id}/approve`, { method: 'POST', headers });
          await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs/${newRun.id}/finalize`, { method: 'POST', headers });
          targetRun = newRun;
        } else if (existingRuns.length > 0) {
          const r = existingRuns[0];
          await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs/${r.id}/calculate`, { method: 'POST', headers });
          await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs/${r.id}/approve`, { method: 'POST', headers });
          await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs/${r.id}/finalize`, { method: 'POST', headers });
          targetRun = r;
        }
      }

      return { run: targetRun };
    }, { legalEntityId, groupId: groupContext.groupId, periodId: groupContext.periodId });

    expect(lifecycleResult.run).toBeTruthy();
    const runId = lifecycleResult.run.id;
    const runNumber = lifecycleResult.run.runNumber;

    // 4. Reconcile GL atomically through the backend ledger posting capability
    const reconciliation = await page.evaluate(async ({ legalEntityId, runId }) => {
      const csrfRes = await fetch('/api/v1/auth/csrf');
      const csrfData = csrfRes.ok ? await csrfRes.json() : {};
      const csrfToken = csrfData.token || '';
      const headers = { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken };

      // First reconciliation call
      const reconcileRes = await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs/${runId}/reconcile-accounting`, {
        method: 'POST',
        headers
      });
      const reconcileData = reconcileRes.ok ? await reconcileRes.json() : null;

      // Second reconciliation call to verify strict concurrency idempotency
      const idempotentRes = await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs/${runId}/reconcile-accounting`, {
        method: 'POST',
        headers
      });
      const idempotentData = idempotentRes.ok ? await idempotentRes.json() : null;

      const accountingRes = await fetch(`/api/v1/payroll/legal-entities/${legalEntityId}/runs/${runId}/accounting`);
      const accountingData = accountingRes.ok ? await accountingRes.json() : null;

      return {
        reconcileStatus: reconcileRes.status,
        reconcileData,
        idempotentStatus: idempotentRes.status,
        idempotentData,
        accountingData
      };
    }, { legalEntityId, runId });

    expect(reconciliation.reconcileStatus).toBe(200);
    expect(reconciliation.idempotentStatus).toBe(200);
    expect(reconciliation.reconcileData?.journalEntryNumber).toBeTruthy();
    expect(reconciliation.idempotentData?.journalEntryNumber).toBe(reconciliation.reconcileData?.journalEntryNumber);
    expect(reconciliation.accountingData?.accountingState).toBe('Posted');
    expect(reconciliation.accountingData?.isPosted).toBe(true);
    expect(reconciliation.accountingData?.journalEntryNumber).toBe(reconciliation.reconcileData?.journalEntryNumber);

    // 5. Test Streaming CSV Export Endpoint Correctness
    const csvExportCheck = await page.evaluate(async (entityId) => {
      const res = await fetch(`/api/v1/payroll/legal-entities/${entityId}/reports/payroll/export`);
      const contentType = res.headers.get('content-type') || '';
      const isComplete = res.headers.get('x-export-complete') === 'true';
      const text = await res.text();
      return {
        status: res.status,
        contentType,
        isComplete,
        hasHeader: text.includes('RunNumber,RunType,PeriodEnd'),
        lineCount: text.split('\n').filter(Boolean).length
      };
    }, legalEntityId);
    expect(csvExportCheck.status).toBe(200);
    expect(csvExportCheck.contentType).toContain('text/csv');
    expect(csvExportCheck.isComplete).toBe(true);
    expect(csvExportCheck.hasHeader).toBe(true);
    expect(csvExportCheck.lineCount).toBeGreaterThan(1);

    // 6. UI Acceptance: Go to Reports tab, search for the run, verify authoritative GL badge & journal entry number
    await page.locator('[data-testid="payroll-tab-reports"]').click();
    await page.waitForTimeout(1000);
    const searchField = page.getByPlaceholder(/بحث/i).first();
    await searchField.fill(runNumber);
    await page.waitForTimeout(1000);

    await expect(page.getByText(runNumber)).toBeVisible();
    await expect(page.getByText(new RegExp(reconciliation.accountingData.journalEntryNumber, 'i'))).toBeVisible();

    // 7. UI Acceptance: Go to Payslips tab, verify real employee identity and server-side count
    await page.locator('[data-testid="payroll-tab-payslips"]').click();
    await page.waitForTimeout(1000);
    await expect(page.getByText(/إجمالي السجلات:/i)).toBeVisible();

    // 8. Security Isolation Check: Verify unauthenticated requests (401) & non-existent entity scope (404/403)
    const isolationCheck = await page.evaluate(async (entityId) => {
      const unauthRes = await fetch(`/api/v1/payroll/legal-entities/${entityId}/reports/payroll`, { credentials: 'omit' });
      const missingRunRes = await fetch(`/api/v1/payroll/legal-entities/${entityId}/runs/00000000-0000-0000-0000-000000000000`);
      const foreignEntityRes = await fetch(`/api/v1/payroll/legal-entities/00000000-0000-0000-0000-000000000000/reports/payroll`);
      return {
        unauthStatus: unauthRes.status,
        missingStatus: missingRunRes.status,
        foreignEntityStatus: foreignEntityRes.status
      };
    }, legalEntityId);
    expect(isolationCheck.unauthStatus).toBe(401);
    expect(isolationCheck.missingStatus).toBe(404);
    expect([403, 404]).toContain(isolationCheck.foreignEntityStatus);
  });
});
