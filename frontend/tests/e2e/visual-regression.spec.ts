import { test, expect } from '@playwright/test';

test.describe('Deterministic Visual Regression', () => {
  test.use({
    viewport: { width: 1280, height: 800 },
  });

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
            {
              id: 'party-002',
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
          ],
          totalCount: 2,
        }),
      });
    });
    await page.route(/\/api\/v1\/invoices/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'inv-001',
          invoiceNumber: 'INV-2026-0091',
          issueDate: '2026-10-01',
          dueDate: '2026-10-31',
          customerPartyId: 'party-001',
          customerName: 'شركة الأفق للتجارة والمقاولات',
          currency: 'SAR',
          items: [
            {
              id: 'line-1',
              description: 'استشارات هندسية وإشراف فني',
              quantity: '5.500',
              uom: 'ساعة',
              unitPrice: '450.0000',
              discountRate: '10.00',
              taxRate: '15.00',
              lineSubtotal: '2227.50',
              lineTotal: '2561.625',
            },
          ],
          subtotal: '2475.00',
          totalDiscount: '247.50',
          totalTax: '334.125',
          totalAmount: '2561.625',
          lifecycleStatus: 'Issued',
          approvalStatus: 'Approved',
          complianceStatus: 'Compliant',
          accountingStatus: 'Posted',
          isLocked: true,
          lockedReason: 'المستند مرحل ومقيد محاسبياً',
          etag: '"w/20261009-v1"',
          createdAt: '2026-10-01',
          createdBy: 'أحمد عبد العال',
        }),
      });
    });

    await page.route(/\/api\/v1\/journals/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'jrnl-001',
          entryNumber: 'JV-2026-0045',
          date: '2026-10-09',
          description: 'قيد إثبات إيرادات مبيعات نقدية',
          status: 'Posted',
          period: '2026-10',
          isPeriodClosed: false,
          lines: [
            {
              id: 'l1',
              accountCode: '1010',
              accountName: 'البنك الأهلي التجاري',
              description: 'تحصيل نقدي بالبنك',
              debit: '15000.00',
              credit: '0.00',
            },
            {
              id: 'l2',
              accountCode: '4010',
              accountName: 'إيرادات المبيعات',
              description: 'إيرادات مبيعات البضائع',
              debit: '0.00',
              credit: '15000.00',
            },
          ],
          debitTotal: '15000.00',
          creditTotal: '15000.00',
          isBalanced: true,
          postedAt: '2026-10-09T10:00:00Z',
          postedBy: 'المدير المالي',
        }),
      });
    });

    await page.route(/\/api\/v1\/imports/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'job-imp-001',
          entityType: 'Parties',
          fileName: 'parties_batch_2026.csv',
          totalRows: 1000,
          processedRows: 1000,
          successCount: 980,
          errorCount: 20,
          status: 'Completed',
          errors: [],
          createdAt: '2026-10-09T08:00:00Z',
        }),
      });
    });
  });

  test('visual regression: Arabic RTL AppShell', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveScreenshot('app-shell-rtl.png', {
      animations: 'disabled',
      maxDiffPixelRatio: 0.05,
    });
  });

  test('visual regression: Directory Party List Page', async ({ page }) => {
    await page.goto('/directory');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveScreenshot('party-list-rtl.png', {
      animations: 'disabled',
      maxDiffPixelRatio: 0.05,
    });
  });

  test('visual regression: Design System Showcase (MoneyField & DataGrid)', async ({ page }) => {
    await page.goto('/preview');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveScreenshot('design-system-preview.png', {
      animations: 'disabled',
      maxDiffPixelRatio: 0.05,
    });
  });

  test('visual regression: Alternate Theme & Compact Density', async ({ page }) => {
    await page.goto('/preview');
    await page.waitForLoadState('networkidle');

    // Toggle theme and density
    const themeBtn = page.getByRole('button', { name: 'السمة البديلة' });
    if (await themeBtn.isVisible()) {
      await themeBtn.click();
    }

    const densityBtn = page.getByRole('button', { name: 'كثافة مدمجة' });
    if (await densityBtn.isVisible()) {
      await densityBtn.click();
    }

    await expect(page).toHaveScreenshot('alternate-theme-compact.png', {
      animations: 'disabled',
      maxDiffPixelRatio: 0.05,
    });
  });

  test('visual regression: Invoice Document Page', async ({ page }) => {
    await page.goto('/invoicing');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveScreenshot('invoice-document-rtl.png', {
      animations: 'disabled',
      maxDiffPixelRatio: 0.05,
    });
  });

  test('visual regression: Manual Journal Accounting Grid', async ({ page }) => {
    await page.goto('/accounting');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveScreenshot('manual-journal-rtl.png', {
      animations: 'disabled',
      maxDiffPixelRatio: 0.05,
    });
  });

  test('visual regression: Import Wizard Workflow', async ({ page }) => {
    await page.goto('/migration');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveScreenshot('import-wizard-rtl.png', {
      animations: 'disabled',
      maxDiffPixelRatio: 0.05,
    });
  });
});
