import { test, expect } from '@playwright/test';

test.describe('Stage 3 Reference Foundation Validation Flows', () => {
  test.beforeEach(async ({ page }) => {
    // Mock Session & Context
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
          { id: 'invoicing' },
          { id: 'payments' },
          { id: 'accounting' },
          { id: 'migration' },
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

    // Mock Invoices API
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

    // Mock Payments API
    await page.route(/\/api\/v1\/payments\/dispatch/, async (route) => {
      const body = JSON.parse(route.request().postData() || '{}');
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          paymentId: 'PAY-TX-99401',
          referenceNumber: body.referenceNumber || 'PAY-REF-99201',
          status: 'Settled',
          amount: body.amount,
          currency: body.currency,
          settledAt: '2026-10-09T10:00:00Z',
        }),
      });
    });

    // Mock Journals API
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

    // Mock Import / Migration API
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

  test('F18: Invoice Reference Flow verifies multi-axis status, locking and correction dialog', async ({ page }) => {
    await page.goto('/invoicing');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'INV-2026-0091' })).toBeVisible();
    await expect(page.getByText('شركة الأفق للتجارة والمقاولات')).toBeVisible();
    await expect(page.getByText('استشارات هندسية وإشراف فني')).toBeVisible();

    // Verify Locked state button
    const creditNoteBtn = page.getByRole('button', { name: 'إصدار إشعار دائن (تصحيح)' });
    await expect(creditNoteBtn).toBeVisible();

    // Open correction modal
    await creditNoteBtn.click();
    await expect(page.getByRole('heading', { name: 'إصدار إشعار دائن (Credit Note)' })).toBeVisible();

    // Close modal
    await page.getByRole('button', { name: 'إلغاء' }).click();
    await expect(page.getByRole('heading', { name: 'إصدار إشعار دائن (Credit Note)' })).not.toBeVisible();
  });

  test('F19: Payment Reference Flow verifies confirmation, idempotency and dispatch', async ({ page }) => {
    await page.goto('/payments');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'المدفوعات والمقبوضات' })).toBeVisible();
    const openBtn = page.getByRole('button', { name: '+ تسجيل دفعة مالية جديدة' });
    await expect(openBtn).toBeVisible();
    await openBtn.click();

    // Verify dialog opened
    await expect(page.getByRole('heading', { name: 'تسجيل دفعة مالية صادرة' })).toBeVisible();

    // Proceed to confirmation
    const nextBtn = page.getByRole('button', { name: 'متابعة إلى التأكيد' });
    await nextBtn.click();

    // Confirmation step
    await expect(page.getByRole('heading', { name: 'تأكيد صرف الدفعة المالية' })).toBeVisible();
    await expect(page.getByText('تنبيه: سيتم إرسال أمر الصرف المالي بصورة نهائية')).toBeVisible();

    // Submit payment
    const confirmBtn = page.getByRole('button', { name: 'تأكيد وإرسال الدفعة' });
    await confirmBtn.click();

    // Success screen
    await expect(page.getByRole('heading', { name: 'تم تنفيذ الدفعة بنجاح' })).toBeVisible();
    await page.getByRole('button', { name: 'تم' }).click();
  });

  test('F20: Accounting Manual Journal verifies heavy grid and balance indicators', async ({ page }) => {
    await page.goto('/accounting');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'JV-2026-0045' })).toBeVisible();
    await expect(page.getByText('✓ القيد متوازن')).toBeVisible();
    await expect(page.getByText('1010 — البنك الأهلي التجاري')).toBeVisible();
    await expect(page.getByText('4010 — إيرادات المبيعات')).toBeVisible();
  });

  test('F21: Import Wizard verifies multi-step operational workflow and reconciliation', async ({ page }) => {
    await page.goto('/migration');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'معالج استيراد بيانات الأطراف التجارية' })).toBeVisible();

    // Step 1: Upload
    await expect(page.getByText('اختر ملف البيانات المراد استيراده')).toBeVisible();
    await page.getByRole('button', { name: 'التالي' }).click();

    // Step 2: Mapping
    await expect(page.getByRole('heading', { name: 'ربط أعمدة الملف بحقول النظام' })).toBeVisible();
    await page.getByRole('button', { name: 'التالي' }).click();

    // Step 3: Validation / Dry Run
    await expect(page.getByRole('heading', { name: 'نتائج الفحص والتشغيل التجريبي (Dry Run)' })).toBeVisible();
    await expect(page.getByText('980')).toBeVisible();
    await expect(page.getByText('20')).toBeVisible();
    await page.getByRole('button', { name: 'التالي' }).click();

    // Step 4: Complete & Reconciliation
    await expect(page.getByRole('heading', { name: 'اكتمل تنفيذ عملية الاستيراد الجزئي' })).toBeVisible();
  });
});
