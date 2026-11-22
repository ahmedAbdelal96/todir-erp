import { test, expect } from '@playwright/test';

const DEV_PASSWORD = process.env.ERP_DEV_PASSWORD ?? 'TodirDev2026!';

test.describe('TODIR ERP — P3 Waves 3–5 direct navigation real-stack smoke', () => {
  test.setTimeout(120_000);

  test('admin can resolve every Wave 3–5 task route without a permission denial', async ({ page }) => {
    const csrfRes = await page.request.get('/api/v1/auth/csrf');
    expect(csrfRes.status()).toBe(200);
    const { token } = await csrfRes.json();
    const loginRes = await page.request.post('/api/v1/auth/login', {
      data: { email: 'admin@atlas.com', password: DEV_PASSWORD },
      headers: { 'X-CSRF-TOKEN': token }
    });
    expect(loginRes.status()).toBe(200);

    const taskRoutes = [
      '/fixed-assets/register',
      '/expenses/self-service',
      '/invoicing/customer-invoices',
      '/payments/incoming',
      '/hr/attendance',
      '/payroll/runs',
      '/projects/resource-load',
      '/service/dispatch'
    ];

    const pageErrors: string[] = [];
    let currentRoute = '';
    page.on('pageerror', error => pageErrors.push(`${currentRoute}: ${error.message}`));
    page.on('console', message => {
      if (message.type() === 'error' && message.text().includes('input.trim is not a function')) {
        pageErrors.push(`${currentRoute}: ${message.text()}`);
      }
    });

    for (const route of taskRoutes) {
      currentRoute = route;
      await page.goto(route);
      await page.waitForLoadState('networkidle');
      await expect(page).toHaveURL(new RegExp(`${route.replace('/', '\\/')}(?:\\?.*)?$`));
      await expect(page.locator('[data-testid="permission-denied-view"]')).not.toBeVisible();
    }

    // Legacy root links must canonicalize through the registered destination
    // while preserving unrelated filters; a guarded route must ignore a
    // conflicting query tab rather than render the requested protected task.
    currentRoute = '/hr?tab=leave&view=compact';
    await page.goto(currentRoute);
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/\/hr\/leave\?view=compact$/);
    await expect(page.locator('[data-testid="hr-page"]')).toBeVisible();

    currentRoute = '/hr/leave?tab=directory&view=compact';
    await page.goto(currentRoute);
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/\/hr\/leave\?tab=directory&view=compact$/);
    await expect(page.locator('[data-testid="hr-tab-leave"]')).toHaveCSS('font-weight', '600');
    await expect(page.locator('[data-testid="hr-tab-directory"]')).toHaveCSS('font-weight', '500');

    expect(pageErrors, `No route should crash during Wave 3–5 navigation: ${pageErrors.join(' | ')}`).toEqual([]);
  });
});
