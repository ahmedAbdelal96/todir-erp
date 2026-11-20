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

    // The cashier is directly taken to /banking/cash (their first permitted destination)
    await expect(page).toHaveURL(/\/banking\/cash/);
    await expect(page.locator('[data-testid="banking-cash-page"]')).toBeVisible({ timeout: 10000 });

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

  test('Narrow Inventory Count Clerk: lands on /inventory/counts, root redirects without 403, forbidden tasks denied', async ({ page }) => {
    await setupSessionMock(page);

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
          permissions: ['inventory.count.create', 'inventory.count.enter'],
          entitledModules: ['inventory'],
          moduleStatuses: { inventory: 'Active' },
          version: '1'
        })
      });
    });

    await page.route('**/api/v1/inventory/legal-entities/*/counts*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 25 })
      });
    });

    await page.route('**/api/v1/inventory/legal-entities/*/warehouses', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ id: 'wh-1', code: 'WH01', name: 'المستودع الرئيسي' }])
      });
    });

    // 1. Visit root /inventory directly
    await page.goto('/inventory');

    // 2. Deterministic redirect to /inventory/counts (NOT 403 Access Denied)
    await expect(page).toHaveURL(/\/inventory\/counts/);
    await expect(page.locator('[data-testid="permission-denied-view"]')).not.toBeVisible();
    await expect(page.locator('[data-testid="inventory-counts-page"]')).toBeVisible({ timeout: 10000 });

    // 3. Direct access to forbidden sub-routes is denied
    await page.goto('/inventory/balances');
    await expect(page.locator('[data-testid="permission-denied-view"]')).toBeVisible({ timeout: 10000 });

    await page.goto('/inventory/transfers');
    await expect(page.locator('[data-testid="permission-denied-view"]')).toBeVisible({ timeout: 10000 });
  });

  test('Narrow Journal Operator: root /accounting redirects to /accounting/journals without 403', async ({ page }) => {
    await setupSessionMock(page);

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
          permissions: ['ledger.general_journal.post', 'ledger.journal.read'],
          entitledModules: ['accounting', 'ledger'],
          moduleStatuses: { accounting: 'Active', ledger: 'Active' },
          version: '1'
        })
      });
    });

    await page.route('**/api/v1/ledger/legal-entities/*/journals', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    await page.route('**/api/v1/ledger/legal-entities/*/entries*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [], totalCount: 0 })
      });
    });

    // 1. Visit root /accounting directly
    await page.goto('/accounting');

    // 2. Deterministic redirect to /accounting/journals (NOT 403 Access Denied)
    await expect(page).toHaveURL(/\/accounting\/journals/);
    await expect(page.locator('[data-testid="permission-denied-view"]')).not.toBeVisible();
    await expect(page.locator('[data-testid="accounting-journals-page"]')).toBeVisible({ timeout: 10000 });

    // 3. Direct access to forbidden statements is denied
    await page.goto('/accounting/statements');
    await expect(page.locator('[data-testid="permission-denied-view"]')).toBeVisible({ timeout: 10000 });
  });

  test('Banking Reconciliation Workbench: renders full-page workbench without modal dialog overlay', async ({ page }) => {
    await setupSessionMock(page);

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
          permissions: ['banking.reconciliation.read', 'banking.reconciliation.match', 'banking.reconciliation.finalize'],
          entitledModules: ['banking'],
          moduleStatuses: { banking: 'Active' },
          version: '1'
        })
      });
    });

    await page.route('**/api/v1/banking/reconciliations/rec-123*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'rec-123',
          sessionNumber: 1,
          treasuryAccountId: 'ta-1',
          statementId: 'stmt-1',
          asOfDate: '2026-11-20',
          externalBalance: 50000,
          bookBalance: 50000,
          difference: 0,
          matchCount: 1,
          state: 'Open'
        })
      });
    });

    await page.route('**/api/v1/banking/accounts/ta-1*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'ta-1',
          name: 'حساب بنك مصر التجاري',
          currencyCode: 'EGP'
        })
      });
    });

    await page.route('**/api/v1/banking/statements/stmt-1/lines*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            { id: 'line-1', transactionDate: '2026-11-20', description: 'إيداع نقدي', amount: 5000, direction: 'Credit', currencyCode: 'EGP' }
          ],
          totalCount: 1
        })
      });
    });

    await page.route('**/api/v1/banking/reconciliations/rec-123/suggestions*', async (route: any) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
    });

    await page.route('**/api/v1/banking/reconciliations/rec-123/claims*', async (route: any) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({}) });
    });

    await page.route('**/api/v1/payments*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            { id: 'pay-1', paymentNumber: 'PAY-001', amount: 5000, currency: 'EGP', direction: 'Incoming', state: 'Completed' }
          ],
          totalCount: 1
        })
      });
    });

    await page.goto('/banking/reconciliations/rec-123');

    // Must render the full-width page workbench
    const workbenchPage = page.locator('[data-testid="reconciliation-workbench-page"]');
    await expect(workbenchPage).toBeVisible({ timeout: 10000 });

    // Must NOT have a modal dialog backdrop overlay covering the screen
    await expect(page.locator('.dialog-backdrop')).not.toBeVisible();

    // Finalize action button exists
    const finalizeBtn = page.locator('[data-testid="finalize-reconciliation"]');
    await expect(finalizeBtn).toBeVisible();
  });
});
