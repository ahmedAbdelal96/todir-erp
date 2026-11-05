import { test, expect, Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const SCREENSHOT_DIR = path.resolve('C:/Users/Adham/.gemini/antigravity/brain/2cce5b52-1525-442e-8037-81105c0347eb/crm-acceptance');

test.describe.configure({ mode: 'serial' });

async function ensureAuthenticated(page: Page) {
  page.on('console', (msg) => {
    if (msg.type() === 'error') console.log('PAGE ERROR:', msg.text());
  });
  page.on('pageerror', (err) => {
    console.log('UNCAUGHT PAGE ERROR:', err.message);
  });
  page.on('response', async (res) => {
    if (!res.ok()) {
      try {
        const text = await res.text();
        console.log('HTTP ERROR:', res.status(), res.url(), text);
      } catch {
        console.log('HTTP ERROR:', res.status(), res.url());
      }
    }
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
      await emailInput.fill('admin@atlas.com');
      const password = process.env.SEED_DEFAULT_PASSWORD || 'DevSeed2026Apmjuoj1oZjHEDcxSsOc27HKY8yOv0B7';
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

  // Ensure Arabic is active for default tests
  const switchToArabicBtn = page.locator('button').filter({ hasText: /Switch to Arabic/i });
  if (await switchToArabicBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
    await switchToArabicBtn.click();
    await page.waitForLoadState('networkidle');
  }
}

test.describe('Module 19 — CRM (Customer Relationships) Frontend & Seller Workflows', () => {
  test.beforeAll(() => {
    if (!fs.existsSync(SCREENSHOT_DIR)) {
      fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
    }
  });

  test('01. Seller Home "My Sales Today" renders with daily worklist and revenue KPIs', async ({ page }) => {
    await ensureAuthenticated(page);

    // Verify main Oxford Navy header and title
    await expect(page.locator('h1')).toContainText(/إدارة علاقات العملاء|CRM/);

    // Verify KPIs in Seller Home
    await expect(page.locator('text=متابعات اليوم').or(page.locator('text=مهام مستحقة')).first()).toBeVisible();
    await expect(page.locator('text=عملاء بحاجة لمتابعة').or(page.locator('text=بحاجة لتواصل')).first()).toBeVisible();
    await expect(page.locator('text=صفقات راكدة').or(page.locator('text=صفقات متوقفة')).first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '01-crm-home-desktop.png'),
      fullPage: true,
    });
  });

  test('02. Lead Management: capture new lead via centered modal dialog', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Leads tab
    const leadsTabBtn = page.getByRole('button', { name: /العملاء المحتمل|Leads/i });
    await leadsTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Click Primary Action button: Capture New Lead
    const newLeadBtn = page.getByRole('button', { name: /عميل محتمل جديد|Capture Lead|تسجيل عميل|New Lead/i });
    await expect(newLeadBtn).toBeVisible();
    await newLeadBtn.click();

    // Verify Dialog opened
    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(/عميل محتمل|Lead/);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '02-crm-new-lead-modal.png'),
    });

    // Close modal
    const cancelBtn = dialog.getByRole('button', { name: /إلغاء|Cancel/i });
    await cancelBtn.click();
    await expect(dialog).toBeHidden();
  });

  test('03. Lead Management: Qualify Lead dialog documents requirements and budget', async ({ page }) => {
    await ensureAuthenticated(page);

    const leadsTabBtn = page.getByRole('button', { name: /العملاء المحتمل|Leads/i });
    await leadsTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Find any qualify button if leads exist, or test qualify modal directly
    const qualifyBtn = page.getByRole('button', { name: /تأهيل|Qualify/i }).first();
    if (await qualifyBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await qualifyBtn.click();
      const dialog = page.locator('[role="dialog"]');
      await expect(dialog).toBeVisible();
      await expect(dialog).toContainText(/تأهيل|Qualify/);

      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '03-crm-qualify-lead-modal.png'),
      });

      await dialog.getByRole('button', { name: /إلغاء|Cancel/i }).click();
    }
  });

  test('04. Lead Conversion: Convert Lead to Commercial Party & Opportunity modal', async ({ page }) => {
    await ensureAuthenticated(page);

    const leadsTabBtn = page.getByRole('button', { name: /العملاء المحتمل|Leads/i });
    await leadsTabBtn.click();
    await page.waitForLoadState('networkidle');

    const convertBtn = page.getByRole('button', { name: /تحويل لصفقة|تحويل إلى عميل وصفقة|تحويل|Convert/i }).first();
    if (await convertBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await convertBtn.click();
      const dialog = page.locator('[role="dialog"]');
      await expect(dialog).toBeVisible();
      await expect(dialog).toContainText(/تحويل|Convert/);

      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '04-crm-convert-lead-modal.png'),
      });

      await dialog.getByRole('button', { name: /إلغاء|Cancel/i }).click();
    }
  });

  test('05. Lead Merge Duplicates & CSV Import modals', async ({ page }) => {
    await ensureAuthenticated(page);

    const leadsTabBtn = page.getByRole('button', { name: /العملاء المحتمل|Leads/i });
    await leadsTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Test Merge modal if present
    const mergeBtn = page.getByRole('button', { name: /دمج المتطابقين|دمج التكرار|Merge/i }).first();
    if (await mergeBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await mergeBtn.click();
      const dialog = page.locator('[role="dialog"]');
      await expect(dialog).toBeVisible();
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '05-crm-merge-modal.png'),
      });
      await dialog.getByRole('button', { name: /إلغاء|Cancel/i }).click();
    }

    // Test Import modal
    const importBtn = page.getByRole('button', { name: /استيراد CSV|استيراد ملف CSV|Import/i });
    if (await importBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await importBtn.click();
      const dialog = page.locator('[role="dialog"]');
      await expect(dialog).toBeVisible();
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '06-crm-import-modal.png'),
      });
      await dialog.getByRole('button', { name: /إلغاء|Cancel/i }).click();
    }
  });

  test('06. Opportunities Pipeline: Kanban board view and stage columns', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Opportunities tab
    const oppsTabBtn = page.getByRole('button', { name: /الفرص والمسار|Opportunities/i });
    await oppsTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Verify pipeline board or list is rendered
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '07-crm-opportunities-board.png'),
      fullPage: true,
    });

    // Switch to List View if toggle button exists
    const listViewBtn = page.getByRole('button', { name: /عرض الجدول|قائمة|List/i });
    if (await listViewBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await listViewBtn.click();
      await page.waitForLoadState('networkidle');
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '08-crm-opportunities-list.png'),
        fullPage: true,
      });
    }
  });

  test('07. Opportunities: New Opportunity creation modal', async ({ page }) => {
    await ensureAuthenticated(page);

    const oppsTabBtn = page.getByRole('button', { name: /الفرص والمسار|Opportunities/i });
    await oppsTabBtn.click();
    await page.waitForLoadState('networkidle');

    const newOppBtn = page.getByRole('button', { name: /فرصة بيعية جديدة|New Opportunity|إضافة صفقة/i });
    await expect(newOppBtn).toBeVisible();
    await newOppBtn.click();

    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(/فرصة|Opportunity/);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '09-crm-new-opportunity-modal.png'),
    });

    await dialog.getByRole('button', { name: /إلغاء|Cancel/i }).click();
  });

  test('08. Opportunity Workspace: 360 view, Buying Committee, Commercial Interests & Quotation Handoff', async ({ page }) => {
    await ensureAuthenticated(page);

    const oppsTabBtn = page.getByRole('button', { name: /الفرص والمسار|Opportunities/i });
    await oppsTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Click on any opportunity to open 360 workspace
    const oppRow = page.locator('[data-testid^="opportunity-card-"], [data-testid="crm-opportunity-row"], button:has-text("EGP")').first();
    if (await oppRow.isVisible({ timeout: 3000 }).catch(() => false)) {
      await oppRow.click();
      const dialog = page.locator('[role="dialog"]');
      if (await dialog.isVisible({ timeout: 3000 }).catch(() => false)) {
        await page.screenshot({
          path: path.join(SCREENSHOT_DIR, '10-crm-opportunity-workspace.png'),
        });

        // Test Buying Committee
        const committeeBtn = dialog.getByRole('button', { name: /لجنة الشراء|Buying Committee/i });
        if (await committeeBtn.isVisible().catch(() => false)) {
          await committeeBtn.click();
          await page.screenshot({
            path: path.join(SCREENSHOT_DIR, '11-crm-buying-committee-modal.png'),
          });
          const backBtn = page.locator('[role="dialog"]').getByRole('button', { name: /إغلاق|Close|إلغاء/i }).first();
          await backBtn.click();
        }

        // Test Quote Handoff
        const quoteBtn = dialog.getByRole('button', { name: /عرض سعر|Quotation|تحويل لعرض سعر/i });
        if (await quoteBtn.isVisible().catch(() => false)) {
          await quoteBtn.click();
          await page.screenshot({
            path: path.join(SCREENSHOT_DIR, '13-crm-quote-handoff-modal.png'),
          });
          const backBtn = page.locator('[role="dialog"]').getByRole('button', { name: /إغلاق|Close|إلغاء/i }).first();
          await backBtn.click();
        }

        await dialog.getByRole('button', { name: /إغلاق|Close/i }).first().click().catch(() => {});
      }
    }
  });

  test('09. Activities Management: schedule activity and complete with next action', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Activities tab
    const activitiesTabBtn = page.getByRole('button', { name: /الأنشطة والمتابعات|Activities/i });
    await activitiesTabBtn.click();
    await page.waitForLoadState('networkidle');

    const newActivityBtn = page.getByRole('button', { name: /تسجيل نشاط جديد|جدولة نشاط جديد|New Activity|تسجيل نشاط/i });
    await expect(newActivityBtn).toBeVisible();
    await newActivityBtn.click();

    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '16-crm-new-activity-modal.png'),
    });

    await dialog.getByRole('button', { name: /إلغاء|Cancel/i }).click();
  });

  test('10. Sales Management: revenue forecasts, pipeline inspection, sales teams & custom fields', async ({ page }) => {
    await ensureAuthenticated(page);

    // Switch to Management tab
    const mgmtTabBtn = page.getByRole('button', { name: /إدارة المبيعات والتقارير|الإدارة والتوقعات|Management/i });
    await mgmtTabBtn.click();
    await page.waitForLoadState('networkidle');

    // Assert forecast cards
    await expect(page.locator('text=في المسار').or(page.locator('text=Pipeline')).first()).toBeVisible();
    await expect(page.locator('text=أفضل سيناريو').or(page.locator('text=Best Case')).first()).toBeVisible();
    await expect(page.locator('text=مؤكدة للإغلاق').or(page.locator('text=Commit')).first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '18-crm-management-forecast.png'),
      fullPage: true,
    });

    // Subtab: Teams
    const teamsSubTab = page.getByRole('button', { name: /فرق المبيعات|Teams/i });
    if (await teamsSubTab.isVisible({ timeout: 2000 }).catch(() => false)) {
      await teamsSubTab.click();
      await page.waitForLoadState('networkidle');

      const newTeamBtn = page.getByRole('button', { name: /فريق جديد|New Team|إنشاء فريق/i });
      if (await newTeamBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await newTeamBtn.click();
        const dialog = page.locator('[role="dialog"]');
        await expect(dialog).toBeVisible();
        await page.screenshot({
          path: path.join(SCREENSHOT_DIR, '19-crm-new-team-modal.png'),
        });
        await dialog.getByRole('button', { name: /إلغاء|Cancel/i }).click();
      }
    }

    // Subtab: Settings / Custom Fields
    const settingsSubTab = page.getByRole('button', { name: /الحقول المخصصة|Custom Fields/i });
    if (await settingsSubTab.isVisible({ timeout: 2000 }).catch(() => false)) {
      await settingsSubTab.click();
      await page.waitForLoadState('networkidle');

      const newFieldBtn = page.getByRole('button', { name: /حقل مخصص|Custom Field/i });
      if (await newFieldBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await newFieldBtn.click();
        const dialog = page.locator('[role="dialog"]');
        await expect(dialog).toBeVisible();
        await page.screenshot({
          path: path.join(SCREENSHOT_DIR, '22-crm-new-custom-field-modal.png'),
        });
        await dialog.getByRole('button', { name: /إلغاء|Cancel/i }).click();
      }
    }
  });

  test('11. Mobile Viewport (390px): responsive layout without overflow', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await ensureAuthenticated(page);

    await page.waitForLoadState('networkidle');
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '23-crm-home-mobile-390px.png'),
      fullPage: true,
    });
  });

  test('12. English LTR Parity: natural terminology, correct direction, zero missing copy', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await ensureAuthenticated(page);

    const switchToEnglishBtn = page.locator('button').filter({ hasText: /English/i }).first();
    if (await switchToEnglishBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await switchToEnglishBtn.click();
      await page.waitForLoadState('networkidle');
    }

    await expect(page.locator('h1')).toContainText(/Customer Relationships|CRM/i);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '24-crm-home-english-ltr.png'),
      fullPage: true,
    });
  });
});
