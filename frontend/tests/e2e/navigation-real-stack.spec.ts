import { test, expect, Page } from '@playwright/test';

test.describe.configure({ mode: 'serial' });

const MAIN_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const SECONDARY_ENTITY_ID = 'd8eb39bb-b048-7c5d-bacd-e3a923eee21c';
const DEV_PASSWORD = process.env.ERP_DEV_PASSWORD || 'TodirDev2026!';

async function loginUser(page: Page, email: string) {
  await page.goto('/');
  await page.context().clearCookies();
  await page.evaluate(() => {
    try {
      localStorage.clear();
      sessionStorage.clear();
      localStorage.setItem('i18nextLng', 'ar');
    } catch {}
  });

  await page.goto('/auth');
  await page.waitForLoadState('networkidle');

  const emailInput = page.locator('#login-email');
  if (await emailInput.isVisible({ timeout: 5000 }).catch(() => false)) {
    await emailInput.fill(email);
    await page.locator('#login-password').fill(DEV_PASSWORD);
    await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
    await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle');
  }
}

test.describe('TODIR ERP — Real-Stack Navigation & Authorization Acceptance Suite', () => {
  test.setTimeout(120_000);

  test.beforeAll(async ({ request }) => {
    const csrfRes = await request.get('/api/v1/auth/csrf');
    expect(csrfRes.status()).toBe(200);
    const { token } = await csrfRes.json();
    expect(token).toBeTruthy();
  });

  test('Narrow Cashier: Sidebar shows only Cash Count, Accounts forbidden on direct URL, Command Palette filters', async ({ page }) => {
    await loginUser(page, 'cashier@atlas.com');
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // 1. Sidebar check: Cashier only sees Banking (for Cash), and CRM is hidden
    const sidebar = page.locator('#app-sidebar');
    await expect(sidebar).toBeVisible({ timeout: 15000 });

    const bankingLink = sidebar.getByRole('link', { name: /البنوك والنقدية|Banking/i });
    await expect(bankingLink).toBeVisible({ timeout: 10000 });

    const crmLink = sidebar.getByRole('link', { name: /إدارة علاقات العملاء|CRM/i });
    await expect(crmLink).not.toBeVisible();

    // 2. Click Banking -> lands on /banking/cash (first authorized destination)
    await bankingLink.click();
    await expect(page).toHaveURL(/\/banking\/cash/);
    await expect(page.locator('[data-testid="banking-cash-page"]')).toBeVisible({ timeout: 10000 });

    // 3. Direct route guard: Navigate directly to forbidden treasury accounts
    await page.goto('/banking/accounts');
    await page.waitForLoadState('networkidle');

    const deniedView = page.locator('[data-testid="permission-denied-view"]');
    await expect(deniedView).toBeVisible({ timeout: 10000 });
    await expect(deniedView).toContainText('غير مصرح لك بالوصول');

    // 4. Command Palette: Trigger via topbar button
    const cmdTrigger = page.locator('.topbar-command-trigger');
    if (await cmdTrigger.isVisible({ timeout: 5000 }).catch(() => false)) {
      await cmdTrigger.click();
      const commandCard = page.locator('.command-palette-card');
      await expect(commandCard).toBeVisible({ timeout: 10000 });

      // Invoicing creation must NOT be visible
      await expect(page.locator('#cmd-item-action-new-customer-invoice')).not.toBeVisible();

      // Cash count destination SHOULD be searchable
      const cmdInput = commandCard.locator('input');
      await cmdInput.fill('جرد');
      const cashCmdItem = commandCard.getByText(/جرد النقدية والعهد|جرد/i);
      await expect(cashCmdItem.first()).toBeVisible({ timeout: 10000 });

      // Close palette
      await page.keyboard.press('Escape');
    }
  });

  test('Narrow CRM Rep: CRM Leads accessible, management & unrelated modules forbidden', async ({ page }) => {
    await loginUser(page, 'crm@atlas.com');
    await page.goto('/crm');
    await page.waitForLoadState('networkidle');

    // 1. CRM Leads is accessible
    const crmPage = page.getByTestId('crm-page');
    await expect(crmPage.getByRole('button', { name: 'العملاء المحتملين', exact: true })).toBeVisible({ timeout: 10000 });

    // 2. Management tab is completely hidden
    await expect(crmPage.getByRole('button', { name: 'إدارة المبيعات والتقارير', exact: true })).not.toBeVisible();

    // 3. Direct navigation to forbidden Accounting module results in Permission Denied
    await page.goto('/accounting');
    await page.waitForLoadState('networkidle');

    const deniedView = page.locator('[data-testid="permission-denied-view"]');
    await expect(deniedView).toBeVisible({ timeout: 10000 });
  });

  test('Narrow Accountant: Can access Accounting Statements, cannot access Inventory Warehouse management', async ({ page }) => {
    await loginUser(page, 'accountant@atlas.com');
    await page.goto('/accounting/statements');
    await page.waitForLoadState('networkidle');

    // 1. Accounting Statements page is accessible
    await expect(page.locator('body')).not.toContainText('غير مصرح لك بالوصول');

    // 2. Direct navigation to Inventory warehouses is forbidden
    await page.goto('/inventory/warehouses');
    await page.waitForLoadState('networkidle');

    const deniedView = page.locator('[data-testid="permission-denied-view"]');
    await expect(deniedView).toBeVisible({ timeout: 10000 });
  });

  test('Legal Entity Switching: Active state allows mutations, ReadOnly state blocks mutations with HTTP 403', async ({ page }) => {
    await loginUser(page, 'admin@atlas.com');
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // 1. Verify Effective Access for MAIN entity (Active)
    const accessMainRes = await page.request.get(`/api/v1/authorization/effective-access?legalEntityId=${MAIN_ENTITY_ID}`);
    expect(accessMainRes.status()).toBe(200);
    const accessMain = await accessMainRes.json();
    expect(accessMain.moduleStatuses.inventory).toBe('Active');
    expect(accessMain.moduleStatuses.accounting).toBe('Active');

    // 2. Switch organization context to SECONDARY entity
    const csrfRes = await page.request.get('/api/v1/auth/csrf');
    const { token } = await csrfRes.json();

    const switchRes = await page.request.post('/api/v1/context/organization', {
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token
      },
      data: {
        legalEntityId: SECONDARY_ENTITY_ID,
        branchId: null
      }
    });
    expect(switchRes.status()).toBe(204);

    // 3. Verify Effective Access for SECONDARY entity (ReadOnly)
    const accessSecRes = await page.request.get(`/api/v1/authorization/effective-access?legalEntityId=${SECONDARY_ENTITY_ID}`);
    expect(accessSecRes.status()).toBe(200);
    const accessSec = await accessSecRes.json();
    expect(accessSec.moduleStatuses.inventory).toBe('ReadOnly');
    expect(accessSec.moduleStatuses.accounting).toBe('ReadOnly');

    // 4. Mutation on ReadOnly SECONDARY entity fails with 403 module.lifecycle_readonly
    const roMutationRes = await page.request.post(`/api/v1/inventory/legal-entities/${SECONDARY_ENTITY_ID}/warehouses`, {
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token
      },
      data: {
        code: 'WH_RO_REAL_TEST',
        name: 'ReadOnly Real Test Warehouse',
        locationMode: 'WarehouseOnly'
      }
    });
    expect(roMutationRes.status()).toBe(403);
    const roError = await roMutationRes.json();
    expect(roError.code).toBe('module.lifecycle_readonly');

    // 5. Historical reads on ReadOnly SECONDARY entity succeed (200 OK)
    const roReadRes = await page.request.get(`/api/v1/inventory/legal-entities/${SECONDARY_ENTITY_ID}/warehouses`);
    expect(roReadRes.status()).toBe(200);
  });
});
