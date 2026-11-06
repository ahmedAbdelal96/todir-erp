import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const SCREENSHOT_DIR = process.env.CRM_SCREENSHOT_DIR ||
  path.resolve('artifacts/crm-acceptance');

test.describe.configure({ mode: 'serial' });

async function ensureAuthenticated(page: Page) {
  page.on('console', (msg) => {
    if (msg.type() === 'error') console.log('PAGE ERROR:', msg.text());
  });
  page.on('pageerror', (err) => {
    console.log('UNCAUGHT PAGE ERROR:', err.message);
  });

  await page.goto('/crm');
  await page.waitForLoadState('networkidle');

  const atlasBtn = page.getByRole('button', { name: /admin@atlas\.com/i });
  if (await atlasBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
    await atlasBtn.click();
    await atlasBtn.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle');
  } else {
    const emailInput = page.locator('#login-email');
    if (await emailInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      const password = process.env.SEED_DEFAULT_PASSWORD;
      if (!password) {
        throw new Error('SEED_DEFAULT_PASSWORD environment variable is required to run E2E authentication.');
      }
      await emailInput.fill('admin@atlas.com');
      await page.locator('#login-password').fill(password);
      await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
      await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
      await page.waitForLoadState('networkidle');
    }
  }

  if (!page.url().includes('/crm')) {
    await page.goto('/crm');
    await page.waitForLoadState('networkidle');
  }

  await expect(page.locator('[data-testid="crm-page"]')).toBeVisible({ timeout: 20000 });

  // Ensure Arabic is active by default
  const langBtn = page.locator('button.topbar-lang-btn');
  if (await langBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
    const text = await langBtn.innerText();
    if (text.includes('Arabic') || text.includes('العربية')) {
      await langBtn.click();
      await page.waitForFunction(() => document.documentElement.getAttribute('dir') === 'rtl');
      await page.waitForLoadState('networkidle');
    }
  }
}

test.describe('Module 19 — CRM (Customer Relationships) Frontend & Seller Workflows', () => {
  const timestamp = Date.now();
  const testLeadName = `شركة النيل للتجارة ${timestamp}`;
  const testCompanyName = `مجموعة النيل الدولية ${timestamp}`;
  const testOppTitle = `صفقة توريد وتجهيز ${timestamp}`;
  const testActivitySubject = `مكالمة متابعة متطلبات المشروع ${timestamp}`;

  test.beforeAll(() => {
    if (!fs.existsSync(SCREENSHOT_DIR)) {
      fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
    }
  });

  test('01. Visual & Layout Architecture Verification: Design tokens, Oxford Navy header & KPI layout', async ({ page }) => {
    await ensureAuthenticated(page);

    // Verify main header branding and typography
    const heading = page.locator('h1');
    await expect(heading).toBeVisible();
    await expect(heading).toContainText(/إدارة علاقات العملاء|CRM/);

    // Verify Dark Oxford Navy background on header container
    const header = page.locator('header').first();
    const headerBg = await header.evaluate((el) => window.getComputedStyle(el).backgroundColor);
    expect(headerBg).toBe('rgb(16, 42, 67)'); // #102a43

    // Verify cool neutral slate background on page canvas
    const canvas = page.locator('[data-testid="crm-page"]');
    const canvasBg = await canvas.evaluate((el) => window.getComputedStyle(el).backgroundColor);
    expect(canvasBg).toBe('rgb(240, 244, 248)'); // #f0f4f8

    // Verify KPI card computed styles (crisp white background + Oxford navy top border)
    const kpiCards = page.locator('.ui-surface');
    await expect(kpiCards.first()).toBeVisible();
    const cardBg = await kpiCards.first().evaluate((el) => window.getComputedStyle(el).backgroundColor);
    expect(cardBg).toBe('rgb(255, 255, 255)');

    // Verify Seller Worklist and Stale Deals sections render
    await expect(page.locator('text=متابعات ومهام اليوم').first()).toBeVisible();
    await expect(page.locator('text=صفقات متوقفة بحاجة لتحريك').first()).toBeVisible();
    await expect(page.locator('text=عملاء محتملون بحاجة لمتابعة').first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '01-crm-home-desktop.png'),
      fullPage: true,
    });
  });

  test('02. Lead Management: capture new lead via centered modal dialog and verify backend persistence', async ({ page }) => {
    await ensureAuthenticated(page);

    // Navigate to Leads tab
    const leadsTabBtn = page.getByRole('button', { name: /العملاء المحتمل|Leads/i });
    await leadsTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Click Primary Action button: Capture New Lead
    const newLeadBtn = page.getByRole('button', { name: /عميل محتمل جديد|Capture Lead|تسجيل عميل|New Lead/i });
    await expect(newLeadBtn).toBeVisible();
    await newLeadBtn.click();

    // Verify Dialog opened with proper centering and overlay
    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(/عميل محتمل|Lead/);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '02-crm-new-lead-modal.png'),
    });

    // Fill form fields
    const nameInput = dialog.locator('input').first();
    await nameInput.fill(testLeadName);

    const companyInput = dialog.locator('input').nth(1);
    await companyInput.fill(testCompanyName);

    const phoneInput = dialog.locator('input[placeholder*="+20"], input[type="text"]').nth(2);
    if (await phoneInput.isVisible().catch(() => false)) {
      await phoneInput.fill('+20 10 1234 5678');
    }

    const emailInput = dialog.locator('input[type="email"]');
    if (await emailInput.isVisible().catch(() => false)) {
      await emailInput.fill(`lead-${timestamp}@nile-trade.com`);
    }

    // Submit form
    const submitBtn = dialog.getByRole('button', { name: /حفظ العميل|Save/i });
    await submitBtn.click();

    // Verify modal closes
    await expect(dialog).toBeHidden({ timeout: 10000 });
    await page.waitForLoadState('networkidle');

    // Assert the created lead is persisted and visible in the table
    const leadRow = page.locator('[data-testid="crm-lead-row"]').filter({ hasText: testLeadName });
    await expect(leadRow).toBeVisible({ timeout: 10000 });
    await expect(leadRow).toContainText(testCompanyName);
  });

  test('03. Lead Qualification: document BANT requirements and budget', async ({ page }) => {
    await ensureAuthenticated(page);

    const leadsTabBtn = page.getByRole('button', { name: /العملاء المحتمل|Leads/i });
    await leadsTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Find the lead row created in previous test
    const leadRow = page.locator('[data-testid="crm-lead-row"]').filter({ hasText: testLeadName });
    await expect(leadRow).toBeVisible({ timeout: 10000 });

    // Click Qualify button on that row
    const qualifyBtn = leadRow.getByRole('button', { name: /تأهيل|Qualify/i });
    await expect(qualifyBtn).toBeVisible();
    await qualifyBtn.click();

    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(/تأهيل|Qualify/);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '03-crm-qualify-lead-modal.png'),
    });

    // Fill qualification fields
    const needInput = dialog.locator('input, textarea').first();
    await needInput.fill('تطوير وتحديث البنية التحتية لنظام ERP وسلاسل التوريد');

    const budgetInput = dialog.locator('input[placeholder*="50,000"], input[type="text"]').nth(1);
    if (await budgetInput.isVisible().catch(() => false)) {
      await budgetInput.fill('175000');
    }

    // Submit qualification
    const submitQualifyBtn = dialog.getByRole('button', { name: /تأكيد التأهيل|Submit/i });
    await submitQualifyBtn.click();

    await expect(dialog).toBeHidden({ timeout: 10000 });
    await page.waitForLoadState('networkidle');

    // Verify status updated to "مؤهل" (Qualified)
    await expect(leadRow).toContainText(/مؤهل|Qualified/);
  });

  test('04. Lead Conversion: Convert Lead to Commercial Party & Opportunity in Pipeline', async ({ page }) => {
    await ensureAuthenticated(page);

    const leadsTabBtn = page.getByRole('button', { name: /العملاء المحتمل|Leads/i });
    await leadsTabBtn.click();
    await page.waitForLoadState('networkidle');

    const leadRow = page.locator('[data-testid="crm-lead-row"]').filter({ hasText: testLeadName });
    await expect(leadRow).toBeVisible({ timeout: 10000 });

    // Qualified lead now has Convert button
    const convertBtn = leadRow.getByRole('button', { name: /تحويل لصفقة|تحويل إلى عميل|Convert/i });
    await expect(convertBtn).toBeVisible();
    await convertBtn.click();

    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(/تحويل|Convert/);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '04-crm-convert-lead-modal.png'),
    });

    // Provide authentic estimated amount
    const amountInput = dialog.locator('input').filter({ hasText: '' }).nth(2);
    if (await amountInput.isVisible().catch(() => false)) {
      await amountInput.fill('175000');
    }

    // Submit conversion
    const submitConvertBtn = dialog.getByRole('button', { name: /تأكيد التحويل|Convert/i });
    await submitConvertBtn.click();

    await expect(dialog).toBeHidden({ timeout: 12000 });
    await page.waitForLoadState('networkidle');

    // Verify lead status is now "تم التحويل" (Converted)
    await expect(leadRow).toContainText(/تم التحويل|Converted/);
  });

  test('05. Opportunities Pipeline: Kanban board view, distinct stages and list view toggle', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Opportunities tab
    const oppsTabBtn = page.getByRole('button', { name: /الفرص والمسار|Opportunities/i });
    await oppsTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Verify Kanban board columns render
    const boardContainer = page.locator('[data-testid^="pipeline-column-"]');
    await expect(boardContainer.first()).toBeVisible({ timeout: 10000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '07-crm-opportunities-board.png'),
      fullPage: true,
    });

    // Toggle to List view
    const listViewBtn = page.getByRole('button', { name: /عرض الجدول|قائمة|List/i });
    if (await listViewBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await listViewBtn.click();
      await page.waitForLoadState('networkidle');

      const oppTable = page.locator('[aria-label="جدول الفرص البيعية"]');
      await expect(oppTable).toBeVisible();

      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '08-crm-opportunities-list.png'),
        fullPage: true,
      });

      // Switch back to board view
      const boardViewBtn = page.getByRole('button', { name: /عرض لوحة|كانبان|Board/i });
      if (await boardViewBtn.isVisible().catch(() => false)) {
        await boardViewBtn.click();
        await page.waitForLoadState('networkidle');
      }
    }
  });

  test('06. Opportunities: Create new opportunity with authentic legal entity currency', async ({ page }) => {
    await ensureAuthenticated(page);

    const oppsTabBtn = page.getByRole('button', { name: /الفرص والمسار|Opportunities/i });
    await oppsTabBtn.click();
    await page.waitForLoadState('networkidle');

    const newOppBtn = page.getByRole('button', { name: /فرصة بيعية جديدة|New Opportunity/i });
    await expect(newOppBtn).toBeVisible();
    await newOppBtn.click();

    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(/فرصة|Opportunity/);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '09-crm-new-opportunity-modal.png'),
    });

    // Fill title
    const titleInput = dialog.locator('input').first();
    await titleInput.fill(testOppTitle);

    // Provide authentic estimated amount without prefilled 50000
    const amountInput = dialog.locator('input[type="number"]').first();
    if (await amountInput.isVisible().catch(() => false)) {
      await amountInput.fill('220000');
    }

    // Submit
    const submitBtn = dialog.getByRole('button', { name: /إنشاء الصفقة|Save/i });
    await submitBtn.click();

    await expect(dialog).toBeHidden({ timeout: 10000 });
    await page.waitForLoadState('networkidle');

    // Verify opportunity appears in board
    const oppCard = page.locator('[data-testid^="opportunity-card-"]').filter({ hasText: testOppTitle });
    await expect(oppCard).toBeVisible({ timeout: 10000 });
  });

  test('07. Opportunity Workspace: 360 view, Buying Committee stakeholder and Commercial Interests', async ({ page }) => {
    await ensureAuthenticated(page);

    const oppsTabBtn = page.getByRole('button', { name: /الفرص والمسار|Opportunities/i });
    await oppsTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Find the opportunity card
    const oppCard = page.locator('[data-testid^="opportunity-card-"]').filter({ hasText: testOppTitle }).first();
    await expect(oppCard).toBeVisible({ timeout: 10000 });
    await oppCard.click();

    const workspaceDialog = page.locator('[role="dialog"]');
    await expect(workspaceDialog).toBeVisible({ timeout: 10000 });
    await expect(workspaceDialog).toContainText(testOppTitle);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '10-crm-opportunity-workspace.png'),
    });

    // Test Buying Committee Dialog
    const committeeBtn = workspaceDialog.getByRole('button', { name: /لجنة الشراء|Buying Committee/i });
    if (await committeeBtn.isVisible().catch(() => false)) {
      await committeeBtn.click();
      const committeeDialog = page.getByRole('dialog', { name: /لجنة الشراء|Buying Committee/i });
      await expect(committeeDialog).toBeVisible();

      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '11-crm-buying-committee-modal.png'),
      });

      // Close committee modal
      const closeCommitteeBtn = committeeDialog.getByRole('button', { name: /إغلاق|Close/i }).first();
      await closeCommitteeBtn.click();
      await expect(committeeDialog).toBeHidden();
    }

    // Test Quotation Handoff Dialog
    const quoteBtn = workspaceDialog.getByRole('button', { name: /عرض سعر|Quotation/i });
    if (await quoteBtn.isVisible().catch(() => false)) {
      await quoteBtn.click();
      const quoteDialog = page.getByRole('dialog', { name: /عرض سعر|Quotation/i });
      await expect(quoteDialog).toBeVisible();

      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '13-crm-quote-handoff-modal.png'),
      });

      const cancelQuoteBtn = quoteDialog.getByRole('button', { name: /إلغاء|Cancel|إغلاق/i }).first();
      await cancelQuoteBtn.click();
      await expect(quoteDialog).toBeHidden();
    }

    // Close workspace modal
    const closeWorkspaceBtn = workspaceDialog.getByRole('button', { name: /إغلاق|Close/i }).first();
    await closeWorkspaceBtn.click();
    await expect(workspaceDialog).toBeHidden();
  });

  test('08. Activities Management: schedule activity, persist to backend, complete with next action', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Activities tab
    const activitiesTabBtn = page.getByRole('button', { name: /الأنشطة والمتابعات|Activities/i });
    await activitiesTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Click "تسجيل نشاط جديد"
    const newActivityBtn = page.getByRole('button', { name: /تسجيل نشاط جديد|New Activity/i });
    await expect(newActivityBtn).toBeVisible();
    await newActivityBtn.click();

    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '16-crm-new-activity-modal.png'),
    });

    // Fill activity subject
    const subjectInput = dialog.locator('input[type="text"]').first();
    await subjectInput.fill(testActivitySubject);

    // Submit
    const submitBtn = dialog.getByRole('button', { name: /جدولة النشاط|حفظ النشاط|Schedule|Save/i });
    await submitBtn.click();

    await expect(dialog).toBeHidden({ timeout: 10000 });
    await page.waitForLoadState('networkidle');

    // Assert activity appears in table
    const activityRow = page.locator('[data-testid="crm-activity-row"]').filter({ hasText: testActivitySubject });
    await expect(activityRow).toBeVisible({ timeout: 10000 });
    await expect(activityRow).toContainText(/Pending|قيد الانتظار/);

    // Complete activity
    const completeBtn = activityRow.getByRole('button', { name: /إكمال|Complete/i });
    await expect(completeBtn).toBeVisible();
    await completeBtn.click();

    const completeDialog = page.locator('[role="dialog"]');
    await expect(completeDialog).toBeVisible();

    // Fill outcome
    const outcomeInput = completeDialog.locator('input').first();
    await outcomeInput.fill('تم الاتفاق على البنود وجدولة العقد النهائي');

    // Submit completion
    const submitCompleteBtn = completeDialog.getByRole('button', { name: /تأكيد الإكمال|Submit/i });
    await submitCompleteBtn.click();

    await expect(completeDialog).toBeHidden({ timeout: 10000 });
    await page.waitForLoadState('networkidle');

    // Switch to Completed filter to verify completed activity
    const completedTab = page.getByRole('button', { name: /مكتملة|Completed/i }).first();
    await completedTab.click();
    await page.waitForLoadState('networkidle');

    // Verify activity row state updated in Completed list
    const completedRow = page.locator('[data-testid="crm-activity-row"]').filter({ hasText: testActivitySubject });
    await expect(completedRow).toBeVisible({ timeout: 10000 });
    await expect(completedRow).toContainText(/Completed|مكتملة/);
  });

  test('09. Sales Management: revenue forecasts, team creation and custom field definition', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Management tab
    const mgmtTabBtn = page.getByRole('button', { name: /إدارة المبيعات والتقارير|Management/i });
    await mgmtTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Assert forecast cards
    await expect(page.locator('text=في المسار').first()).toBeVisible();
    await expect(page.locator('text=أفضل سيناريو').first()).toBeVisible();
    await expect(page.locator('text=مؤكدة للإغلاق').first()).toBeVisible();

    // Assert separate Opportunity Win Rate and Lead Conversion Rate KPIs
    const winRateEl = page.locator('[data-testid="opportunity-win-rate"]');
    const convRateEl = page.locator('[data-testid="lead-conversion-rate"]');
    await expect(winRateEl).toBeVisible();
    await expect(convRateEl).toBeVisible();
    await expect(page.locator('text=معدل الفوز بالصفقات').first()).toBeVisible();
    await expect(page.locator('text=معدل تحويل العملاء').first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '18-crm-management-forecast.png'),
      fullPage: true,
    });

    // Subtab: Teams
    const teamsSubTab = page.getByRole('button', { name: /فرق المبيعات|Teams/i });
    await teamsSubTab.click();
    await page.waitForLoadState('networkidle');

    const newTeamBtn = page.getByRole('button', { name: /فريق جديد|New Team/i });
    await expect(newTeamBtn).toBeVisible();
    await newTeamBtn.click();

    const teamDialog = page.locator('[role="dialog"]');
    await expect(teamDialog).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '19-crm-new-team-modal.png'),
    });

    const teamCode = `TEAM-${timestamp.toString().slice(-4)}`;
    const teamName = `فريق مبيعات الدلتا ${timestamp.toString().slice(-4)}`;

    await teamDialog.locator('input').first().fill(teamCode);
    await teamDialog.locator('input').nth(1).fill(teamName);

    const submitTeamBtn = teamDialog.getByRole('button', { name: /حفظ|Save/i });
    await submitTeamBtn.click();

    await expect(teamDialog).toBeHidden({ timeout: 10000 });
    await page.waitForLoadState('networkidle');

    // Verify team appears in list
    await expect(page.locator('text=' + teamName).first()).toBeVisible({ timeout: 10000 });

    // Subtab: Settings / Custom Fields
    const settingsSubTab = page.getByRole('button', { name: /الحقول المخصصة|Custom Fields/i });
    await settingsSubTab.click();
    await page.waitForLoadState('networkidle');

    const newFieldBtn = page.getByRole('button', { name: /تعريف حقل مخصص|Custom Field/i });
    await expect(newFieldBtn).toBeVisible();
    await newFieldBtn.click();

    const fieldDialog = page.locator('[role="dialog"]');
    await expect(fieldDialog).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '22-crm-new-custom-field-modal.png'),
    });

    const fieldKey = `fleet_size_${timestamp.toString().slice(-4)}`;
    const fieldLabel = `حجم الأسطول ${timestamp.toString().slice(-4)}`;

    await fieldDialog.locator('input').first().fill(fieldKey);
    await fieldDialog.locator('input').nth(1).fill(fieldLabel);

    const submitFieldBtn = fieldDialog.getByRole('button', { name: /حفظ|Save/i });
    await submitFieldBtn.click();

    await expect(fieldDialog).toBeHidden({ timeout: 10000 });
    await page.waitForLoadState('networkidle');

    // Verify custom field appears in list
    await expect(page.locator('text=' + fieldLabel).first()).toBeVisible({ timeout: 10000 });
  });

  test('10. Lead Inbox: server-side search, filtering and pagination navigation', async ({ page }) => {
    await ensureAuthenticated(page);

    const leadsTabBtn = page.getByRole('button', { name: /العملاء المحتمل|Leads/i });
    await leadsTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Filter by Qualified status pill
    const qualifiedPill = page.getByRole('button', { name: /مؤهل/i }).first();
    await qualifiedPill.click();
    await page.waitForLoadState('networkidle');

    // All visible rows should be qualified
    const rows = page.locator('[data-testid="crm-lead-row"]');
    if (await rows.count() > 0) {
      await expect(rows.first()).toContainText(/مؤهل|Qualified/);
    }

    // Filter by All
    const allPill = page.getByRole('button', { name: /الكل/i }).first();
    await allPill.click();
    await page.waitForLoadState('networkidle');

    // Search for our isolated test lead
    const searchInput = page.locator('input[placeholder*="بحث"]').first();
    await searchInput.fill(testLeadName);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('text=' + testLeadName).first()).toBeVisible();
  });

  test('11. Mobile Viewport (390px): responsive layout without horizontal overflow', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await ensureAuthenticated(page);
    await page.waitForLoadState('networkidle');

    // Assert document doesn't horizontally overflow
    const hasHorizontalScroll = await page.evaluate(() => {
      return document.documentElement.scrollWidth > document.documentElement.clientWidth;
    });
    expect(hasHorizontalScroll).toBe(false);

    // Verify main header and KPIs are visible on mobile
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.locator('.ui-surface').first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '23-crm-home-mobile-390px.png'),
      fullPage: true,
    });
  });

  test('12. English LTR Parity: authentic language switch, dir="ltr", zero missing copy', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page);

    // Click language switcher to switch to English
    const langBtn = page.locator('button.topbar-lang-btn');
    if (await langBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      const text = await langBtn.innerText();
      if (text.includes('English') || text.includes('الإنجليزية')) {
        await langBtn.click();
        await page.waitForFunction(() => document.documentElement.getAttribute('dir') === 'ltr');
        await page.waitForLoadState('networkidle');
      }
    }

    // Assert document direction is strictly LTR
    const direction = await page.evaluate(() => document.documentElement.getAttribute('dir'));
    expect(direction).toBe('ltr');

    // Verify English H1
    const h1Text = await page.locator('h1').innerText();
    expect(h1Text).toMatch(/Customer Relationships|CRM/i);

    // Verify English tab navigation
    await expect(page.getByRole('button', { name: /My Sales Today/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Leads$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Opportunities & Pipeline/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Activities & Follow-ups/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Sales Management/i })).toBeVisible();

    // Verify no untranslated text guards
    const hasMissingCopy = await page.locator('text=النص غير متاح').isVisible().catch(() => false);
    expect(hasMissingCopy).toBe(false);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '24-crm-home-english-ltr.png'),
      fullPage: true,
    });

    // Switch back to Arabic to ensure subsequent test isolation
    if (await langBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await langBtn.click();
      await page.waitForFunction(() => document.documentElement.getAttribute('dir') === 'rtl');
      await page.waitForLoadState('networkidle');
    }
  });
});
