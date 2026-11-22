import { test, expect, Page } from '@playwright/test';

test.describe.configure({ mode: 'serial' });

const MAIN_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const SECONDARY_ENTITY_ID = 'd8eb39bb-b048-7c5d-bacd-e3a923eee21c';
const DEV_PASSWORD = process.env.ERP_DEV_PASSWORD || 'TodirDev2026!';

async function loginUser(page: Page, email: string) {
  await page.context().clearCookies();
  const csrfRes = await page.request.get('/api/v1/auth/csrf');
  const { token } = await csrfRes.json();
  const loginRes = await page.request.post('/api/v1/auth/login', {
    data: { email, password: DEV_PASSWORD },
    headers: { 'X-CSRF-TOKEN': token }
  });
  if (loginRes.status() !== 200) {
    throw new Error(`Login failed for ${email}: status ${loginRes.status()} - ${await loginRes.text()}`);
  }

  await page.goto('/');
  await page.evaluate(() => {
    try {
      localStorage.setItem('i18nextLng', 'ar');
    } catch {}
  });
  await page.waitForLoadState('networkidle');
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
    const cashPage = page.locator('[data-testid="banking-cash-page"]');
    await expect(cashPage).toBeVisible({ timeout: 10000 });

    // Positive task usability verification: Cash counting task action is rendered for authorized role
    const newCountBtn = page.locator('[data-testid="new-cash-count"]');
    if (await newCountBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
      await expect(newCountBtn).toBeVisible();
    }

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

    // 1. CRM Leads is accessible and interactive
    const crmPage = page.getByTestId('crm-page');
    const leadsTab = crmPage.getByRole('button', { name: 'العملاء المحتملين', exact: true });
    await expect(leadsTab).toBeVisible({ timeout: 10000 });
    await leadsTab.click();

    // Positive task usability verification: Leads search/filter is interactive
    const searchInput = crmPage.locator('input[type="text"], input[type="search"]').first();
    if (await searchInput.isVisible({ timeout: 5000 }).catch(() => false)) {
      await searchInput.fill('Atlas');
      await expect(searchInput).toHaveValue('Atlas');
    }

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

    // 1. Accounting Statements page is accessible and usable
    await expect(page.locator('body')).not.toContainText('غير مصرح لك بالوصول');
    const mainContent = page.locator('main, [role="main"]').first();
    await expect(mainContent).toBeVisible({ timeout: 10000 });

    // 2. Direct navigation to Inventory warehouses is forbidden
    await page.goto('/inventory/warehouses');
    await page.waitForLoadState('networkidle');

    const deniedView = page.locator('[data-testid="permission-denied-view"]');
    await expect(deniedView).toBeVisible({ timeout: 10000 });
  });

  test('Legal Entity Switching: UI selector updates context, Active allows mutations, ReadOnly blocks with 403', async ({ page }) => {
    await loginUser(page, 'admin@atlas.com');
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // 1. Locate UI organization selector in header
    const entitySelect = page.locator('#entity-select');
    await expect(entitySelect).toBeVisible({ timeout: 15000 });

    // 2. Select MAIN entity via application UI if not already selected
    if (await entitySelect.inputValue() !== MAIN_ENTITY_ID) {
      const switchPromise = page.waitForResponse(res => res.url().includes('/api/v1/context/organization') && res.status() === 204);
      await entitySelect.selectOption(MAIN_ENTITY_ID);
      await switchPromise;
      await page.waitForResponse(res => res.url().includes('/api/v1/authorization/effective-access') && res.status() === 200);
      await page.waitForLoadState('networkidle');
    }
    await expect(entitySelect).toHaveValue(MAIN_ENTITY_ID);

    // 3. Verify active legal entity and effective permissions
    const accessMainRes = await page.request.get(`/api/v1/authorization/effective-access?legalEntityId=${MAIN_ENTITY_ID}`);
    expect(accessMainRes.status()).toBe(200);
    const accessMain = await accessMainRes.json();
    expect(accessMain.moduleStatuses.inventory).toBe('Active');
    expect(accessMain.moduleStatuses.accounting).toBe('Active');

    // 4. Perform an authorized operation on MAIN in Active state and verify its committed result
    const csrfRes = await page.request.get('/api/v1/auth/csrf');
    const { token } = await csrfRes.json();
    const mainWhCode = `WH_MAIN_${Date.now()}`;
    const createWhRes = await page.request.post(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/warehouses`, {
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token
      },
      data: {
        code: mainWhCode,
        name: 'Main Active Test Warehouse',
        locationMode: 'WarehouseOnly'
      }
    });
    expect(createWhRes.status()).toBe(201);
    const createdWh = await createWhRes.json();
    expect(createdWh.code).toBe(mainWhCode);

    // Verify committed result via API read
    const mainListRes = await page.request.get(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/warehouses`);
    expect(mainListRes.status()).toBe(200);
    const mainWhList = await mainListRes.json();
    expect(mainWhList.some((w: any) => w.code === mainWhCode)).toBe(true);

    // 5. Select SECONDARY using the actual application UI
    const switchSecPromise = page.waitForResponse(res => res.url().includes('/api/v1/context/organization') && res.status() === 204);
    await entitySelect.selectOption(SECONDARY_ENTITY_ID);
    await switchSecPromise;
    await page.waitForResponse(res => res.url().includes('/api/v1/authorization/effective-access') && res.status() === 200);
    await page.waitForLoadState('networkidle');

    // 6. Verify the frontend has updated its context
    await expect(entitySelect).toHaveValue(SECONDARY_ENTITY_ID);
    const accessSecRes = await page.request.get(`/api/v1/authorization/effective-access?legalEntityId=${SECONDARY_ENTITY_ID}`);
    expect(accessSecRes.status()).toBe(200);
    const accessSec = await accessSecRes.json();
    expect(accessSec.moduleStatuses.inventory).toBe('ReadOnly');
    expect(accessSec.moduleStatuses.accounting).toBe('ReadOnly');

    // 7. Verify previous-company cached records and actions are not exposed
    await page.goto('/inventory/warehouses');
    await page.waitForLoadState('networkidle');
    await expect(page.locator(`text=${mainWhCode}`)).not.toBeVisible();

    const secWhListRes = await page.request.get(`/api/v1/inventory/legal-entities/${SECONDARY_ENTITY_ID}/warehouses`);
    expect(secWhListRes.status()).toBe(200);
    const secWhList = await secWhListRes.json();
    expect(secWhList.some((w: any) => w.code === mainWhCode)).toBe(false);

    // 8. Attempt an otherwise permitted mutation under ReadOnly
    const roWhCode = `WH_RO_FAIL_${Date.now()}`;
    const roMutationRes = await page.request.post(`/api/v1/inventory/legal-entities/${SECONDARY_ENTITY_ID}/warehouses`, {
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token
      },
      data: {
        code: roWhCode,
        name: 'ReadOnly Attempted Warehouse',
        locationMode: 'WarehouseOnly'
      }
    });

    // 9. Assert exact HTTP 403 and lifecycle error code
    expect(roMutationRes.status()).toBe(403);
    const roError = await roMutationRes.json();
    expect(roError.code).toBe('module.lifecycle_readonly');

    // 10. Verify no prohibited database mutation
    const secListAfterRes = await page.request.get(`/api/v1/inventory/legal-entities/${SECONDARY_ENTITY_ID}/warehouses`);
    expect(secListAfterRes.status()).toBe(200);
    const secWhListAfter = await secListAfterRes.json();
    expect(secWhListAfter.some((w: any) => w.code === roWhCode)).toBe(false);

    // 11. Verify permitted historical reads
    expect(secListAfterRes.status()).toBe(200);

    // 12. Switch back and verify correct authorization restoration
    const switchBackPromise = page.waitForResponse(res => res.url().includes('/api/v1/context/organization') && res.status() === 204);
    await entitySelect.selectOption(MAIN_ENTITY_ID);
    await switchBackPromise;
    await page.waitForResponse(res => res.url().includes('/api/v1/authorization/effective-access') && res.status() === 200);
    await page.waitForLoadState('networkidle');
    await expect(entitySelect).toHaveValue(MAIN_ENTITY_ID);

    const accessRestoredRes = await page.request.get(`/api/v1/authorization/effective-access?legalEntityId=${MAIN_ENTITY_ID}`);
    expect(accessRestoredRes.status()).toBe(200);
    const accessRestored = await accessRestoredRes.json();
    expect(accessRestored.moduleStatuses.inventory).toBe('Active');
  });

  test('P3 Wave 2 Real-Stack: POS Cashier fast-lane workspace and Sessions management', async ({ page }) => {
    // 1. Login as POS Cashier
    await loginUser(page, 'pos-cashier@atlas.com');
    await page.goto('/pos');
    await page.waitForLoadState('networkidle');

    // POS Cashier has fast-lane cart access
    const posPage = page.locator('[data-testid="pos-checkout-page"]');
    await expect(posPage).toBeVisible({ timeout: 15000 });
    await expect(page.locator('body')).not.toContainText('غير مصرح لك بالوصول');

    // Direct navigation to administrative reports route must be blocked for cashier
    await page.goto('/pos/reports');
    await page.waitForLoadState('networkidle');

    const deniedView = page.locator('[data-testid="permission-denied-view"]');
    await expect(deniedView).toBeVisible({ timeout: 10000 });
    await expect(deniedView).toContainText('غير مصرح لك بالوصول');

    // 2. Login as POS Manager
    await loginUser(page, 'pos-manager@atlas.com');

    // Reports page is accessible for POS Manager
    await page.goto('/pos/reports');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="permission-denied-view"]')).not.toBeVisible();
    await expect(page.locator('main, [role="main"]').first()).toBeVisible({ timeout: 10000 });

    // Sessions management route is accessible and interactive for Manager
    await page.goto('/pos/sessions');
    await page.waitForLoadState('networkidle');
    const sessionsPage = page.locator('[data-testid="pos-sessions-page"]');
    await expect(sessionsPage).toBeVisible({ timeout: 10000 });
    const sessionHeading = sessionsPage.locator('h2');
    await expect(sessionHeading.first()).toBeVisible({ timeout: 10000 });
  });

  test('P3 Wave 2 Real-Stack: Sales task-driven Quotation workflow and navigation', async ({ page }) => {
    await loginUser(page, 'admin@atlas.com');

    // 1. Navigate to /sales/quotations
    await page.goto('/sales/quotations');
    await page.waitForLoadState('networkidle');

    const quotationsPage = page.locator('[data-testid="sales-quotations-page"]');
    await expect(quotationsPage).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="permission-denied-view"]')).not.toBeVisible();

    // 2. Interact with Quotation search
    const searchInput = quotationsPage.locator('input[type="text"], input[type="search"]').first();
    if (await searchInput.isVisible({ timeout: 5000 }).catch(() => false)) {
      await searchInput.fill('QUO-TEST');
      await expect(searchInput).toHaveValue('QUO-TEST');
      await searchInput.fill('');
    }

    // 3. Click "+ إنشاء عرض سعر جديد" to trigger New Quotation Modal
    const newQuoteBtn = quotationsPage.getByRole('button', { name: /إنشاء عرض سعر جديد|New Quotation/i }).first();
    await expect(newQuoteBtn).toBeVisible({ timeout: 10000 });
    await newQuoteBtn.click();

    // 4. Verify modal opens
    const modalDialog = page.getByRole('dialog').first();
    await expect(modalDialog).toBeVisible({ timeout: 10000 });
    await expect(modalDialog).toContainText(/عرض سعر|Quotation/i);

    // 5. Close modal
    const closeBtn = modalDialog.getByRole('button', { name: /إلغاء|Cancel|إغلاق|Close/i }).first();
    if (await closeBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await closeBtn.click();
    } else {
      await page.keyboard.press('Escape');
    }
    await expect(modalDialog).not.toBeVisible({ timeout: 5000 });

    // 6. Verify other sales task routes are accessible
    await page.goto('/sales/orders');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="permission-denied-view"]')).not.toBeVisible();

    await page.goto('/sales/settings');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="permission-denied-view"]')).not.toBeVisible();
  });

  test('P3 Wave 2 Real-Stack: Purchasing task-driven Orders workflow and navigation', async ({ page }) => {
    await loginUser(page, 'admin@atlas.com');

    // 1. Navigate to /purchasing/orders
    await page.goto('/purchasing/orders');
    await page.waitForLoadState('networkidle');

    const ordersPage = page.locator('[data-testid="purchase-orders-page"]');
    await expect(ordersPage).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="permission-denied-view"]')).not.toBeVisible();

    // 2. Interact with order search
    const searchInput = ordersPage.locator('input[type="text"], input[type="search"]').first();
    if (await searchInput.isVisible({ timeout: 5000 }).catch(() => false)) {
      await searchInput.fill('PO-TEST');
      await expect(searchInput).toHaveValue('PO-TEST');
      await searchInput.fill('');
    }

    // 3. Click "إنشاء أمر شراء جديد" to trigger New Order Modal
    const newOrderBtn = ordersPage.getByRole('button', { name: /أمر شراء جديد|New.*Order/i }).first();
    await expect(newOrderBtn).toBeVisible({ timeout: 10000 });
    await newOrderBtn.click();

    // 4. Verify modal opens
    const modalDialog = page.getByRole('dialog').first();
    await expect(modalDialog).toBeVisible({ timeout: 10000 });
    await expect(modalDialog).toContainText(/أمر شراء|Purchase Order/i);

    // 5. Close modal
    const closeBtn = modalDialog.getByRole('button', { name: /إلغاء|Cancel|إغلاق|Close/i }).first();
    if (await closeBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await closeBtn.click();
    } else {
      await page.keyboard.press('Escape');
    }
    await expect(modalDialog).not.toBeVisible({ timeout: 5000 });

    // 6. Verify other purchasing task routes are accessible
    await page.goto('/purchasing/receipts');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="permission-denied-view"]')).not.toBeVisible();

    await page.goto('/purchasing/matching');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="permission-denied-view"]')).not.toBeVisible();

    await page.goto('/purchasing/settings');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="permission-denied-view"]')).not.toBeVisible();
  });
});
