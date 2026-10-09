import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test.describe('Automated Accessibility Gate (A11y)', () => {
  test.beforeEach(async ({ page }) => {
    // Mock authentication and session context
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: 'tenant-demo',
          memberships: [{ id: 'm1', tenantId: 'tenant-demo', displayName: 'شركة الأفق للتجارة' }],
          context: {
            legalEntities: [{ id: 'le1', code: 'LE-01', name: 'الكيان الرئيسي' }],
            branches: [{ id: 'b1', legalEntityId: 'le1', code: 'BR-01', name: 'فرع الرياض' }],
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
          { id: 'directory' },
          { id: 'preview' },
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

    await page.route('**/api/v1/directory/parties*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'party-001',
              partyType: 'Organization',
              status: 'Active',
              roles: [{ code: 'Customer', status: 'Active', version: 1 }, { code: 'Supplier', status: 'Active', version: 1 }],
              displayName: 'شركة الأفق للتجارة والمقاولات',
              legalName: 'شركة الأفق للتجارة العامة ذ.م.م',
              taxId: '300123456700003',
              nationalId: null,
              primaryEmail: 'info@alofoq.example.com',
              primaryPhone: '+966 11 234 5678',
              createdAt: '2026-01-15',
            },
          ],
          totalCount: 1,
        }),
      });
    });
  });

  test('AppShell and Topbar should have zero critical a11y violations', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(['color-contrast']) // Color contrast evaluated under theme token audit
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('Directory / Party List Page should pass a11y scan', async ({ page }) => {
    await page.goto('/#directory');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(['color-contrast'])
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('Design System Showcase (MoneyField, DataGrid, ComboBox, Dialog) should pass a11y scan', async ({ page }) => {
    await page.goto('/#preview');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(['color-contrast'])
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('Invoice Document Page should pass a11y scan', async ({ page }) => {
    await page.goto('/#invoicing');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(['color-contrast'])
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('Manual Journal Page should pass a11y scan', async ({ page }) => {
    await page.goto('/#accounting');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(['color-contrast'])
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('Import Wizard Page should pass a11y scan', async ({ page }) => {
    await page.goto('/#migration');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(['color-contrast'])
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);
  });
});
