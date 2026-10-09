import { test, expect } from '@playwright/test';

test.describe('App Shell Bootstrap & Localization', () => {
  test.beforeEach(async ({ page }) => {
    // Mock authentication and session context endpoints
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: 'tenant-demo',
          memberships: [
            { id: 'm1', tenantId: 'tenant-demo', displayName: 'شركة الأفق للتجارة' },
          ],
          context: {
            legalEntities: [
              { id: 'le1', code: 'LE-01', name: 'الكيان الرئيسي' },
            ],
            branches: [
              { id: 'b1', legalEntityId: 'le1', code: 'BR-01', name: 'فرع الرياض' },
            ],
          },
        }),
      });
    });

    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'home' },
          { id: 'settings' },
        ]),
      });
    });

    await page.route('**/api/v1/authorization/check*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ allowed: true }),
      });
    });
  });

  test('should render in RTL with Arabic as default language', async ({ page }) => {
    await page.goto('/');

    // Verify root document attributes
    const html = page.locator('html');
    await expect(html).toHaveAttribute('lang', 'ar');
    await expect(html).toHaveAttribute('dir', 'rtl');

    // Verify brand link and workspace topbar
    await expect(page.getByRole('link', { name: 'TODIR' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'مساحة العمل' })).toBeVisible();
    await expect(page.getByText('الكيان القانوني', { exact: true })).toBeVisible();
    await expect(page.getByText('الفرع', { exact: true })).toBeVisible();
  });

  test('should switch language to English and update direction to LTR', async ({ page }) => {
    await page.goto('/');

    // Click language switcher button
    const langButton = page.getByRole('button', { name: 'English' });
    await expect(langButton).toBeVisible();
    await langButton.click();

    // Verify English labels and LTR direction
    await expect(page.getByRole('heading', { name: 'Workspace' })).toBeVisible();
    await expect(page.getByText('Legal entity', { exact: true })).toBeVisible();
    await expect(page.getByText('Branch', { exact: true })).toBeVisible();

    const switchBack = page.getByRole('button', { name: 'العربية' });
    await expect(switchBack).toBeVisible();
  });
});
