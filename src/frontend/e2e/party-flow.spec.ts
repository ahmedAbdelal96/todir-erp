import { test, expect } from '@playwright/test';

test.describe('Directory / Party Vertical Slice Browser Flow', () => {
  const mockParties = [
    {
      id: 'party-001',
      partyNumber: 'PRT-1001',
      partyType: 'Organization',
      status: 'Active',
      roles: ['Customer', 'Vendor'],
      displayName: 'شركة الأفق للتجارة والمقاولات',
      legalName: 'شركة الأفق للتجارة العامة ذ.م.م',
      taxId: '300123456700003',
      nationalId: null,
      primaryEmail: 'info@alofoq.example.com',
      primaryPhone: '+966 11 234 5678',
      createdAt: '2026-01-15',
    },
    {
      id: 'party-002',
      partyNumber: 'PRT-1002',
      partyType: 'Person',
      status: 'Active',
      roles: ['Customer'],
      displayName: 'محمد عبد الله الشمري',
      legalName: null,
      taxId: null,
      nationalId: '1023456789',
      primaryEmail: 'm.shammari@example.com',
      primaryPhone: '+966 50 123 4567',
      createdAt: '2026-02-10',
    },
  ];

  test.beforeEach(async ({ page }) => {
    // Mock authentication and session context
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: 'tenant-demo',
          memberships: [{ id: 'm1', tenantId: 'tenant-demo', displayName: 'مؤسسة التجارة' }],
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

    // Mock Directory Party API endpoints
    await page.route(/\/api\/v1\/parties/, async (route) => {
      const url = new URL(route.request().url());
      if (route.request().method() === 'GET') {
        const pathSegments = url.pathname.split('/');
        const lastSegment = pathSegments[pathSegments.length - 1];

        // Detail endpoint: /api/v1/parties/:id
        if (lastSegment && lastSegment !== 'parties') {
          const match = mockParties.find((p) => p.id === lastSegment) || mockParties[0];
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(match),
          });
          return;
        }

        // List endpoint
        const search = url.searchParams.get('search');
        let filtered = [...mockParties];
        if (search) {
          filtered = filtered.filter((p) => p.displayName.includes(search) || p.partyNumber.includes(search));
        }

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: filtered,
            totalCount: filtered.length,
          }),
        });
      } else if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        const created = {
          id: `party-${Date.now()}`,
          partyNumber: 'PRT-1003',
          ...body,
          status: 'Active',
          createdAt: '2026-10-09',
        };
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(created),
        });
      } else if (route.request().method() === 'PUT') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ ...mockParties[0], ...body }),
        });
      }
    });
  });

  test('should render Party list, filter records, and open party detail view', async ({ page }) => {
    await page.goto('/#directory');
    await page.waitForLoadState('networkidle');

    // Verify RTL and List Page Heading
    const html = page.locator('html');
    await expect(html).toHaveAttribute('dir', 'rtl');
    await expect(page.getByRole('heading', { name: 'دليل الأطراف التجارية' })).toBeVisible();

    // Verify DataGrid rendered rows
    const partyLink = page.getByRole('link', { name: 'شركة الأفق للتجارة والمقاولات' });
    await expect(partyLink).toBeVisible();
    await expect(page.getByText('PRT-1001')).toBeVisible();
    await expect(page.getByText('محمد عبد الله الشمري')).toBeVisible();

    // Verify Create Party Dialog opens
    const newPartyBtn = page.getByRole('button', { name: 'إضافة طرف جديد' });
    await expect(newPartyBtn).toBeVisible();
    await newPartyBtn.click();

    await expect(page.getByRole('heading', { name: 'إضافة طرف تجاري جديد' })).toBeVisible();
    await expect(page.getByLabel('الاسم التجاري')).toBeVisible();

    // Close Dialog
    const cancelBtn = page.getByRole('button', { name: 'إلغاء' });
    await cancelBtn.click();
    await expect(page.getByRole('heading', { name: 'إضافة طرف تجاري جديد' })).not.toBeVisible();

    // Click party link to navigate to record detail
    await partyLink.click();
    await page.waitForLoadState('networkidle');

    // Verify Record Page details
    await expect(page.getByText('البيانات القانونية والتعريفية')).toBeVisible();
    await expect(page.getByText('شركة الأفق للتجارة العامة ذ.م.م')).toBeVisible();
    await expect(page.getByText('300123456700003')).toBeVisible();

    // Verify Back navigation
    const backBtn = page.getByRole('button', { name: 'العودة' });
    await expect(backBtn).toBeVisible();
    await backBtn.click();
    await expect(page.getByRole('heading', { name: 'دليل الأطراف التجارية' })).toBeVisible();
  });
});
