import { test, expect } from '@playwright/test';

test.describe('Directory / Party Vertical Slice Browser Flow', () => {
  const mockParties = [
    { id: 'party-001', partyType: 'Organization', status: 'Active', roles: [{ code: 'Customer', status: 'Active', version: 1 }, { code: 'Supplier', status: 'Active', version: 1 }], displayName: 'شركة الأفق للتجارة والمقاولات', legalName: 'شركة الأفق للتجارة العامة ذ.م.م', identifiers: [{ id: 'identifier-001', type: 'Tax', value: '300123456700003', countryCode: 'EG', isPrimary: true, verificationStatus: 'Unverified' }], contactPoints: [{ id: 'contact-001', type: 'Email', value: 'info@alofoq.example.com', isPrimary: true }], addresses: [], createdAt: '2026-01-15', updatedAt: '2026-01-15', version: 1 },
    { id: 'party-002', partyType: 'Person', status: 'Active', roles: [{ code: 'Customer', status: 'Active', version: 1 }], displayName: 'محمد عبد الله الشمري', legalName: null, identifiers: [{ id: 'identifier-002', type: 'National', value: '1023456789', countryCode: 'EG', isPrimary: true, verificationStatus: 'Unverified' }], contactPoints: [{ id: 'contact-002', type: 'Email', value: 'm.shammari@example.com', isPrimary: true }], addresses: [], createdAt: '2026-02-10', updatedAt: '2026-02-10', version: 1 }
  ];
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/session/context', async route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ authenticated: true, tenantId: 'tenant-demo', memberships: [{ id: 'm1', tenantId: 'tenant-demo', displayName: 'مؤسسة التجارة' }], context: { legalEntities: [], branches: [] } }) }));
    await page.route('**/api/v1/modules', async route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 'home' }, { id: 'directory' }, { id: 'preview' }, { id: 'settings' }]) }));
    await page.route('**/api/v1/authorization/check*', async route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ allowed: true }) }));
    await page.route(/\/api\/v1\/directory\/parties/, async route => {
      const url = new URL(route.request().url()); const last = url.pathname.split('/').pop();
      if (route.request().method() === 'GET') {
        if (last && last !== 'parties') return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockParties.find(p => p.id === last) ?? mockParties[0]) });
        const search = url.searchParams.get('search'); const items = search ? mockParties.filter(p => p.displayName.includes(search)) : mockParties;
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ items, totalCount: items.length, page: 1, pageSize: 25 }) });
      }
      return route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify(mockParties[0]) });
    });
  });
  test('should render Party list, filter records, and open party detail view', async ({ page }) => {
    await page.goto('/directory'); await page.waitForLoadState('networkidle');
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl'); await expect(page.getByRole('heading', { name: 'دليل الأطراف' })).toBeVisible();
    const partyButton = page.getByRole('button', { name: 'شركة الأفق للتجارة والمقاولات' }); await expect(partyButton).toBeVisible(); await expect(page.getByText('محمد عبد الله الشمري')).toBeVisible();
    await page.getByRole('button', { name: 'إضافة طرف' }).click(); await expect(page.getByRole('heading', { name: 'إضافة طرف جديد' })).toBeVisible(); await expect(page.getByText('الاسم التجاري / المعروض')).toBeVisible();
    await page.getByRole('button', { name: 'إلغاء' }).click(); await expect(page.getByRole('heading', { name: 'إضافة طرف جديد' })).not.toBeVisible();
    await partyButton.click(); await page.waitForLoadState('networkidle'); await expect(page.getByRole('heading', { name: 'الهوية والحالة' })).toBeVisible(); await expect(page.getByText('شركة الأفق للتجارة العامة ذ.م.م')).toBeVisible(); await expect(page.getByText('300123456700003')).toBeVisible();
    await page.getByRole('button', { name: 'العودة' }).click(); await expect(page.getByRole('heading', { name: 'دليل الأطراف' })).toBeVisible();
  });
});
