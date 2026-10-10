import { test, expect } from '@playwright/test';

test.describe('FinBase financial foundation flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/session/context', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ authenticated: true, tenantId: 'tenant-demo', memberships: [{ id: 'm1', tenantId: 'tenant-demo', displayName: 'شركة الأفق للتجارة' }], context: { legalEntities: [{ id: 'le1', code: 'LE-01', name: 'الكيان الرئيسي' }], branches: [] } }) }));
    await page.route('**/api/v1/modules', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 'home' }, { id: 'finbase' }]) }));
    await page.route('**/api/v1/authorization/check*', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ allowed: true }) }));
    await page.route('**/api/v1/finbase/currencies', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ code: 'EGP', numericCode: 818, nameAr: 'جنيه مصري', nameEn: 'Egyptian Pound', minorUnit: 2, status: 'Active', datasetVersion: '2026-01' }, { code: 'USD', numericCode: 840, nameAr: 'دولار أمريكي', nameEn: 'US Dollar', minorUnit: 2, status: 'Active', datasetVersion: '2026-01' }]) }));
    await page.route('**/api/v1/finbase/payment-schedule/preview', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ sequence: 1, amount: '33.33', dueDate: '2026-01-01', allocation: 'Percentage' }, { sequence: 2, amount: '66.67', dueDate: '2026-01-31', allocation: 'Balance' }]) }));
  });

  test('renders the Arabic financial foundation and exact schedule preview', async ({ page }) => {
    await page.goto('/#finbase');
    await expect(page.getByRole('heading', { name: 'الأساس المالي' })).toBeVisible();
    await expect(page.getByText('EGP · جنيه مصري')).toBeVisible();
    await expect(page.getByRole('button', { name: 'حساب 33.33% ثم الرصيد' })).toBeVisible();
    await page.getByRole('button', { name: 'حساب 33.33% ثم الرصيد' }).click();
    await expect(page.getByText('66.67')).toBeVisible();
  });
});
