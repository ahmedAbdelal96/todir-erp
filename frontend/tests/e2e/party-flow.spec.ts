import { test, expect } from '@playwright/test';

test.describe('Directory / Party Vertical Slice Browser Flow', () => {
  const mockParties = [
    {
      id: 'party-001',
      partyType: 'Organization',
      status: 'Active',
      roles: [
        { code: 'Customer', status: 'Active', version: 1 },
        { code: 'Supplier', status: 'Active', version: 1 }
      ],
      displayName: 'شركة الأفق للتجارة والمقاولات',
      legalName: 'شركة الأفق للتجارة العامة ذ.م.م',
      identifiers: [
        {
          id: 'identifier-001',
          type: 'TaxId',
          value: '300123456700003',
          countryCode: 'EG',
          isPrimary: true,
          verificationStatus: 'Verified'
        }
      ],
      contactPoints: [
        { id: 'contact-001', type: 'Email', value: 'info@alofoq.example.com', isPrimary: true },
        { id: 'contact-002', type: 'Phone', value: '+201012345678', isPrimary: true }
      ],
      addresses: [
        {
          id: 'address-001',
          usage: 'General',
          countryCode: 'EG',
          city: 'القاهرة',
          line1: 'شارع التجارة 15',
          isDefault: true
        }
      ],
      createdAt: '2026-01-15T10:00:00Z',
      updatedAt: '2026-01-15T10:00:00Z',
      version: 1
    },
    {
      id: 'party-002',
      partyType: 'Person',
      status: 'Active',
      roles: [{ code: 'Customer', status: 'Active', version: 1 }],
      displayName: 'محمد عبد الله الشمري',
      legalName: null,
      identifiers: [
        {
          id: 'identifier-002',
          type: 'NationalId',
          value: '1023456789',
          countryCode: 'EG',
          isPrimary: true,
          verificationStatus: 'Verified'
        }
      ],
      contactPoints: [{ id: 'contact-003', type: 'Email', value: 'm.shammari@example.com', isPrimary: true }],
      addresses: [],
      createdAt: '2026-02-10T10:00:00Z',
      updatedAt: '2026-02-10T10:00:00Z',
      version: 1
    }
  ];

  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/session/context', async (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: 'tenant-demo',
          memberships: [{ id: 'm1', tenantId: 'tenant-demo', displayName: 'مؤسسة التجارة' }],
          context: { legalEntities: [], branches: [] }
        })
      })
    );

    await page.route('**/api/v1/modules', async (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ id: 'home' }, { id: 'directory' }, { id: 'preview' }, { id: 'settings' }])
      })
    );

    await page.route('**/api/v1/authorization/check*', async (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ allowed: true }) })
    );

    await page.route('**/api/v1/directory/parties/*', async (route) => {
      const url = new URL(route.request().url());
      const path = url.pathname;

      if (path.endsWith('/readiness')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ partyId: 'party-001', ready: true, missing: [], warnings: [] })
        });
      }

      if (path.endsWith('/duplicate-candidates')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([])
        });
      }

      const id = path.split('/').pop();
      const party = mockParties.find((p) => p.id === id) ?? mockParties[0];
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(party) });
    });

    await page.route(/\/api\/v1\/directory\/parties(\?.*)?$/, async (route) => {
      if (route.request().method() === 'GET') {
        const url = new URL(route.request().url());
        const search = url.searchParams.get('search');
        const items = search ? mockParties.filter((p) => p.displayName.includes(search)) : mockParties;
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items, totalCount: items.length, page: 1, pageSize: 25 })
        });
      }

      return route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(mockParties[0])
      });
    });
  });

  test('should render Party list in RTL, navigate to detail, and support create modal', async ({ page }) => {
    await page.goto('/directory');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.getByRole('heading', { name: 'دليل العلاقات التجارية' })).toBeVisible();

    const partyButton = page.getByRole('button', { name: 'شركة الأفق للتجارة والمقاولات' });
    await expect(partyButton).toBeVisible();
    await expect(page.getByText('محمد عبد الله الشمري')).toBeVisible();

    // Verify Create modal workflow
    await page.getByRole('button', { name: 'إضافة طرف تجاري' }).click();
    await expect(page.getByRole('heading', { name: 'إضافة طرف تجاري جديد' })).toBeVisible();
    await expect(page.getByText('الاسم التجاري / المعروض *')).toBeVisible();

    // Cancel modal
    await page.getByRole('button', { name: 'إلغاء التعديل' }).click();
    await expect(page.getByRole('heading', { name: 'إضافة طرف تجاري جديد' })).not.toBeVisible();

    // Open detail record
    await partyButton.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'الهوية والحالة التجارية' })).toBeVisible();
    await expect(page.getByText('شركة الأفق للتجارة العامة ذ.م.م')).toBeVisible();
    await expect(page.getByText('300123456700003')).toBeVisible();

    // Return back to list
    await page.getByRole('button', { name: 'العودة' }).click();
    await expect(page.getByRole('heading', { name: 'دليل العلاقات التجارية' })).toBeVisible();
  });
});
