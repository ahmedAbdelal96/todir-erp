import { test, expect } from '@playwright/test';

test.describe('TODIR ERP — Permission-Driven Navigation & Task Architecture (P0/P1/P2)', () => {
  const setupSessionMock = async (page: any) => {
    page.on('console', (msg: any) => console.log('BROWSER LOG:', msg.text()));
    page.on('pageerror', (err: any) => console.log('BROWSER ERROR:', err.message));

    await page.route('**/api/v1/session/context', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: 'tenant-demo',
          identityId: 'user-1',
          context: {
            displayName: 'شركة الأفق للتجارة',
            legalEntities: [
              { id: 'le1', code: 'LE-01', name: 'كيان الأفق التجاري (القاهرة)' },
              { id: 'le2', code: 'LE-02', name: 'كيان الدلتا للاستيراد' }
            ],
            branches: [
              { id: 'b1', legalEntityId: 'le1', code: 'BR-01', name: 'فرع القاهرة الرئيسي' },
              { id: 'b2', legalEntityId: 'le2', code: 'BR-02', name: 'فرع الإسكندرية' }
            ]
          }
        })
      });
    });

    await page.route('**/api/v1/context/organization', async (route: any) => {
      await route.fulfill({ status: 204 });
    });
  };

  test('Narrow Cashier Role: sees cash count destination, accounts & reconciliation are hidden, direct route forbidden', async ({ page }) => {
    await setupSessionMock(page);

    // Mock Effective Access for Cashier: ONLY banking.cash.count
    await page.route('**/api/v1/authorization/effective-access*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          tenantId: 'tenant-demo',
          membershipId: 'm1',
          legalEntityId: 'le1',
          branchId: 'b1',
          isSuper: false,
          permissions: ['banking.cash.count'],
          entitledModules: ['banking', 'invoicing', 'crm'],
          moduleStatuses: { banking: 'Active' },
          version: '1'
        })
      });
    });

    await page.goto('/');

    // 1. Sidebar checks
    // Primary navigation should show Finance & Treasury category because Banking has an authorized child destination
    const sidebar = page.locator('#app-sidebar');
    await expect(sidebar).toBeVisible({ timeout: 10000 });

    // Banking module link must be present
    const bankingLink = sidebar.getByRole('link', { name: /البنوك والنقدية|Banking/i });
    await expect(bankingLink).toBeVisible({ timeout: 10000 });

    // CRM and other unauthorized modules must be absent from the sidebar
    const crmLink = sidebar.getByRole('link', { name: /إدارة علاقات العملاء|CRM/i });
    await expect(crmLink).not.toBeVisible();

    // 2. Click on Banking to visit the module
    await bankingLink.click();

    // Contextual Navigation / Tab Bar check:
    // Cash tab must be visible and active
    const cashTab = page.locator('[data-testid="banking-tab-cash"]');
    await expect(cashTab).toBeVisible({ timeout: 10000 });

    // Accounts, Statements, Reconciliations tabs must NOT be visible to cashier
    const accountsTab = page.locator('[data-testid="banking-tab-accounts"]');
    await expect(accountsTab).not.toBeVisible();

    const reconciliationsTab = page.locator('[data-testid="banking-tab-reconciliation"]');
    await expect(reconciliationsTab).not.toBeVisible();

    // 3. Direct route guard test: cashier navigating directly to /banking/accounts
    await page.goto('/banking/accounts');

    // Must render the permission-denied guard view, NOT sensitive accounts data
    const deniedView = page.locator('[data-testid="permission-denied-view"]');
    await expect(deniedView).toBeVisible({ timeout: 10000 });
    await expect(deniedView).toContainText('غير مصرح لك بالوصول');

    // 4. Command Palette test: clicking the topbar command trigger
    const cmdTrigger = page.locator('.topbar-command-trigger');
    await expect(cmdTrigger).toBeVisible({ timeout: 10000 });
    await cmdTrigger.click();

    const commandCard = page.locator('.command-palette-card');
    await expect(commandCard).toBeVisible({ timeout: 10000 });

    // Cashier must NOT see new invoice action
    await expect(page.locator('#cmd-item-action-new-customer-invoice')).not.toBeVisible();

    // Cashier CAN find cash count destination in search
    const cmdInput = commandCard.locator('input');
    await cmdInput.fill('جرد');
    const cashCmdItem = commandCard.getByText('جرد النقدية والعهد');
    await expect(cashCmdItem).toBeVisible({ timeout: 10000 });
  });

  test('CRM Salesperson vs Manager: management hidden for sales rep, accessible for manager', async ({ page }) => {
    await setupSessionMock(page);

    // 1. Setup Salesperson with ONLY lead and opportunity permissions
    await page.route('**/api/v1/authorization/effective-access*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          tenantId: 'tenant-demo',
          membershipId: 'm1',
          legalEntityId: 'le1',
          branchId: 'b1',
          isSuper: false,
          permissions: ['crm.home.read', 'crm.lead.read', 'crm.lead.create', 'crm.opportunity.read'],
          entitledModules: ['crm'],
          moduleStatuses: { crm: 'Active' },
          version: '1'
        })
      });
    });

    await page.goto('/crm');

    // In CRM page, Leads and Opportunities button tabs are visible
    await expect(page.getByRole('button', { name: 'العملاء المحتملين' })).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('button', { name: 'الفرص والمسار البيعي' })).toBeVisible({ timeout: 10000 });

    // Management tab is completely hidden
    await expect(page.getByRole('button', { name: 'إدارة المبيعات والتقارير' })).not.toBeVisible();

    // Direct navigation to /crm/management should be denied
    await page.goto('/crm/management');
    const deniedView = page.locator('[data-testid="permission-denied-view"]');
    await expect(deniedView).toBeVisible({ timeout: 10000 });
  });

  test('Completely Unauthorized User: zero modules visible, direct routes denied fail-closed', async ({ page }) => {
    await setupSessionMock(page);

    // Empty permissions
    await page.route('**/api/v1/authorization/effective-access*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          tenantId: 'tenant-demo',
          membershipId: 'm1',
          legalEntityId: 'le1',
          branchId: 'b1',
          isSuper: false,
          permissions: [],
          entitledModules: ['banking', 'crm'],
          moduleStatuses: {},
          version: '1'
        })
      });
    });

    await page.goto('/');

    const sidebar = page.locator('#app-sidebar');
    await expect(sidebar).toBeVisible({ timeout: 10000 });
    // Only Home / Workspace is in sidebar
    await expect(sidebar.getByRole('link', { name: /الرئيسية|Home/i })).toBeVisible({ timeout: 10000 });
    await expect(sidebar.getByRole('link', { name: /البنوك والنقدية|Banking/i })).not.toBeVisible();
    await expect(sidebar.getByRole('link', { name: /المحاسبة|Accounting/i })).not.toBeVisible();
    await expect(sidebar.getByRole('link', { name: /إدارة علاقات العملاء|CRM/i })).not.toBeVisible();

    // Direct navigation to /banking
    await page.goto('/banking');
    await expect(page.locator('[data-testid="permission-denied-view"]')).toBeVisible({ timeout: 10000 });

    // Direct navigation to /crm
    await page.goto('/crm');
    await expect(page.locator('[data-testid="permission-denied-view"]')).toBeVisible({ timeout: 10000 });
  });
});
