import { test, expect } from '@playwright/test';

const LEGAL_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const TEST_CUSTOMER_ID = 'party-customer-alfa-01';
const TEST_SUPPLIER_ID = 'party-supplier-beta-01';

test.describe('TODIR Payments — Full E2E Journey & Acceptance', () => {
  test.beforeEach(async ({ page }) => {
    // 1. Auth & Session Routes
    await page.route('**/api/v1/auth/csrf', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ token: 'mock-csrf-token' })
      });
    });

    await page.route('**/api/v1/auth/login', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          state: 'Authenticated',
          authenticated: true,
          tenantId: '00000000-0000-0000-0000-000000000000'
        })
      });
    });

    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: '00000000-0000-0000-0000-000000000000',
          memberships: [
            { id: 'atlas', tenantId: '00000000-0000-0000-0000-000000000000', displayName: 'شركة أطلس للتجارة' }
          ],
          context: {
            displayName: 'شركة أطلس للتجارة',
            legalEntities: [
              {
                id: LEGAL_ENTITY_ID,
                legalEntityId: LEGAL_ENTITY_ID,
                code: 'MAIN',
                name: 'شركة أطلس للتجارة — الكيان الرئيسي',
                countryCode: 'EG',
                baseCurrencyCode: 'EGP'
              }
            ],
            branches: [
              {
                id: 'b1',
                branchId: 'b1',
                legalEntityId: LEGAL_ENTITY_ID,
                code: 'MAIN',
                name: 'الفرع الرئيسي'
              }
            ]
          }
        })
      });
    });

    await page.route('**/api/v1/authorization/check*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ allowed: true })
      });
    });

    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'home' },
          { id: 'directory' },
          { id: 'invoicing' },
          { id: 'payments' },
          { id: 'subledger' },
          { id: 'accounting' },
          { id: 'settings' }
        ])
      });
    });

    // Directory Parties
    await page.route(/(?:\/api\/v1\/directory\/parties|\/api\/v1\/parties)/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: TEST_CUSTOMER_ID,
              displayName: 'شركة النور للمقاولات العامة',
              legalName: 'شركة النور للمقاولات العامة ذ.م.م',
              taxId: '300998877600003',
              roles: ['Customer']
            },
            {
              id: TEST_SUPPLIER_ID,
              displayName: 'مؤسسة الوفاء للتوريدات الهندسية',
              legalName: 'مؤسسة الوفاء للتوريدات الهندسية ذ.م.م',
              taxId: '300112233400003',
              roles: ['Supplier']
            }
          ],
          totalCount: 2
        })
      });
    });

    // Payment Accounts
    await page.route('**/api/v1/payments/legal-entities/*/accounts*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'acc-001',
            legalEntityId: LEGAL_ENTITY_ID,
            code: 'CSH-01',
            name: 'خزينة الفرع الرئيسي',
            type: 'Cashbox',
            currencyPolicy: 'SingleCurrency',
            currencyCode: 'EGP',
            status: 'Active',
            version: 1
          },
          {
            id: 'acc-002',
            legalEntityId: LEGAL_ENTITY_ID,
            code: 'BNK-NBE',
            name: 'حساب البنك الأهلي — جاري',
            type: 'Bank',
            currencyPolicy: 'SingleCurrency',
            currencyCode: 'EGP',
            bankName: 'National Bank of Egypt',
            maskedIdentifier: '****4589',
            status: 'Active',
            version: 1
          },
          {
            id: 'acc-003',
            legalEntityId: LEGAL_ENTITY_ID,
            code: 'BNK-BM-USD',
            name: 'حساب بنك مصر — دولاري',
            type: 'Bank',
            currencyPolicy: 'SingleCurrency',
            currencyCode: 'USD',
            bankName: 'Banque Misr',
            maskedIdentifier: '****7712',
            status: 'Active',
            version: 1
          }
        ])
      });
    });

    // Fees
    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/[^/]+\/fees(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    // Payment Requests
    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/payment-requests(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'req-001',
              legalEntityId: LEGAL_ENTITY_ID,
              merchantReference: 'REQ-2026-0001',
              amount: 12500,
              currencyCode: 'EGP',
              state: 'Pending',
              attemptId: 'att-01',
              attemptNumber: 1
            }
          ],
          page: 1,
          pageSize: 50,
          hasMore: false
        })
      });
    });

    // Single Payment Record Details
    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/pay-001(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'pay-001',
          number: 'PAY-2026-0001',
          legalEntityId: LEGAL_ENTITY_ID,
          direction: 'Incoming',
          type: 'CustomerReceipt',
          partyId: TEST_CUSTOMER_ID,
          amount: '15000.00',
          currencyCode: 'EGP',
          method: 'Cash',
          paymentAccountId: 'acc-001',
          paymentDate: '2026-10-25',
          postingDate: '2026-10-25',
          state: 'Completed',
          version: 1
        })
      });
    });

    // Payments List
    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+(?:\?.*)?$/, async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: 'pay-001',
                number: 'PAY-2026-0001',
                legalEntityId: LEGAL_ENTITY_ID,
                direction: 'Incoming',
                type: 'CustomerReceipt',
                partyId: TEST_CUSTOMER_ID,
                amount: '15000.00',
                currencyCode: 'EGP',
                method: 'Cash',
                paymentAccountId: 'acc-001',
                paymentDate: '2026-10-25',
                postingDate: '2026-10-25',
                state: 'Completed',
                version: 1
              },
              {
                id: 'pay-002',
                number: 'PAY-2026-0002',
                legalEntityId: LEGAL_ENTITY_ID,
                direction: 'Outgoing',
                type: 'SupplierPayment',
                partyId: TEST_SUPPLIER_ID,
                amount: '32000.00',
                currencyCode: 'EGP',
                method: 'BankTransfer',
                paymentAccountId: 'acc-002',
                paymentDate: '2026-10-26',
                postingDate: '2026-10-26',
                state: 'Completed',
                version: 1
              },
              {
                id: 'pay-003',
                number: 'PAY-2026-0003',
                legalEntityId: LEGAL_ENTITY_ID,
                direction: 'InternalTransfer',
                type: 'TransferLeg',
                amount: '50000.00',
                currencyCode: 'EGP',
                method: 'BankTransfer',
                paymentAccountId: 'acc-002',
                paymentDate: '2026-10-27',
                postingDate: '2026-10-27',
                state: 'Completed',
                version: 1
              },
              {
                id: 'pay-004',
                number: 'PAY-2026-0004',
                legalEntityId: LEGAL_ENTITY_ID,
                direction: 'Incoming',
                type: 'CustomerReceipt',
                partyId: TEST_CUSTOMER_ID,
                amount: '4500.00',
                currencyCode: 'EGP',
                method: 'PaymentGateway',
                paymentAccountId: 'acc-002',
                paymentDate: '2026-10-27',
                postingDate: '2026-10-27',
                state: 'Pending',
                version: 1
              }
            ],
            page: 1,
            pageSize: 25,
            hasMore: false
          })
        });
      } else {
        await route.continue();
      }
    });

    // Public Payment Link details
    await page.route('**/api/v1/payments/public-links/tok-alpha-99*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          amount: 12500,
          currencyCode: 'EGP',
          expiresAt: '2026-11-01T00:00:00Z'
        })
      });
    });
  });

  test('1. Payments Landing Page: RTL, Oxford Navy header, filters & separate status badges', async ({ page }) => {
    await page.goto('/payments');

    // Page title and layout confirmation
    await expect(page.locator('h1')).toContainText(/المدفوعات والمقبوضات/);

    // Verify Navigation Tabs exist
    await expect(page.getByRole('button', { name: 'جميع العمليات' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'مقبوضات العملاء' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'مدفوعات الموردين' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'التحويلات الداخلية' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'حسابات الدفع والخزائن' })).toBeVisible();

    // Verify Table rows are present
    const rows = page.locator('tbody tr');
    await expect(rows).toHaveCount(4);

    // Verify separate status badges for First row (PAY-2026-0001)
    const firstRow = rows.first();
    await expect(firstRow).toContainText('PAY-2026-0001');
    await expect(firstRow).toContainText('مكتمل');

    // Verify no raw unlocalized backend enums in Arabic table
    const tableText = await page.locator('table').innerText();
    expect(tableText).not.toContain('PartiallySettled');
    expect(tableText).not.toContain('NotApplicable');
    expect(tableText).not.toContain('CustomerReceipt');
    expect(tableText).not.toContain('SupplierPayment');
    expect(tableText).not.toContain('InternalTransfer');
  });

  test('2. Strict Separation of Concerns: Primary Action Buttons trigger clean modals', async ({ page }) => {
    await page.goto('/payments');

    // Click Primary Action "+ استلام دفعة" (Receive Payment)
    const receiveBtn = page.getByRole('button', { name: '+ استلام دفعة' });
    await expect(receiveBtn).toBeVisible();
    await receiveBtn.click();

    // Verify Receive Dialog is open and distinct from browsing view
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('استلام دفعة من عميل')).toBeVisible();

    // Close Dialog cleanly using cancel button
    const cancelBtn = dialog.getByRole('button', { name: 'إلغاء' });
    await cancelBtn.click();
    await expect(dialog).not.toBeVisible();

    // Open Pay Supplier Dialog
    const payBtn = page.getByRole('button', { name: '+ سداد لمورد' });
    await payBtn.click();
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('سداد دفعة لمورد')).toBeVisible();
    await cancelBtn.click();
    await expect(dialog).not.toBeVisible();

    // Open Internal Transfer Dialog
    const transferBtn = page.getByRole('button', { name: '+ تحويل داخلي' });
    await transferBtn.click();
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('تحويل أموال داخلي بين الحسابات')).toBeVisible();
    await cancelBtn.click();
    await expect(dialog).not.toBeVisible();
  });

  test('3. Quick Peek Drawer & Printable Payment Voucher', async ({ page }) => {
    await page.goto('/payments');

    // Click on row action for preview / quick peek
    const firstRow = page.locator('tbody tr').first();
    const peekBtn = firstRow.getByRole('button', { name: 'معاينة سريعة' }).first();
    await peekBtn.click();

    // Drawer should open
    const drawer = page.locator('.quick-peek-panel');
    await expect(drawer).toBeVisible();
    await expect(drawer).toContainText('PAY-2026-0001');

    // Close drawer using escape
    await page.keyboard.press('Escape');
    await expect(drawer).not.toBeVisible();

    // Click Print Voucher / Receipt button
    const printBtn = firstRow.getByRole('button', { name: 'طباعة السند' }).first();
    await expect(printBtn).toBeVisible();
    await printBtn.click();
    const voucherModal = page.getByRole('dialog');
    await expect(voucherModal).toBeVisible();
    await expect(voucherModal).toContainText('سند قبض مالي');
    // Close voucher
    await page.keyboard.press('Escape');
    await expect(voucherModal).not.toBeVisible();
  });

  test('4. Immutability for Completed Payments: Detail Page Surfaces', async ({ page }) => {
    await page.goto('/payments/pay-001');

    // Verify Header and Number
    await expect(page.locator('h1')).toContainText('PAY-2026-0001');

    // Verify financial lifecycle actions exist for completed payment
    await expect(page.getByRole('button', { name: 'طباعة سند الصرف / القبض' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'استرداد مالي (Refund)' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'عكس السند (Reverse)' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'تخصيص على فواتير مستحقة' })).toBeVisible();

    // Verify the structured inspection surfaces exist
    await expect(page.getByText('نظرة عامة وبيانات حركة النقدية')).toBeVisible();
    await expect(page.getByText('تسوية قيود الأستاذ المساعد والرصيد المتاح')).toBeVisible();
    await expect(page.getByText('الرسوم البنكية والعمولات التشغيلية')).toBeVisible();
    await expect(page.getByText('التوجيه المحاسبي وأثر دفتر الأستاذ العام')).toBeVisible();
    await expect(page.getByText('حالة المطابقة المصرفية')).toBeVisible();
  });

  test('5. Public Payment Link Portal: Tokenized Checkout Experience', async ({ page }) => {
    await page.goto('/payments/pay/tok-alpha-99');

    // Verify Public Checkout layout
    await expect(page.locator('h2')).toContainText('بوابة السداد الإلكتروني الآمن');
    await expect(page.getByText('12,500.00')).toBeVisible();

    // Verify Pay Now button exists
    const paySubmitBtn = page.getByRole('button', { name: 'المتابعة لسداد الدفعة عبر البوابة الآمنة' });
    await expect(paySubmitBtn).toBeVisible();
  });

  test('6. Responsive verification: Mobile 390px layout remains clean and readable', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/payments');

    // Title should be visible
    await expect(page.locator('h1')).toBeVisible();

    // Table container should be scrollable / accessible without page breaking
    const tableContainer = page.locator('div[style*="overflow-x: auto"]').first();
    await expect(tableContainer).toBeVisible();

    // Primary action button should be visible
    const receiveBtn = page.getByRole('button', { name: '+ استلام دفعة' });
    await expect(receiveBtn).toBeVisible();
  });
});
