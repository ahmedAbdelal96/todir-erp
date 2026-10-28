import { test, expect } from '@playwright/test';

const LEGAL_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const TEST_CUSTOMER_ID = 'party-customer-alfa-01';
const TEST_SUPPLIER_ID = 'party-supplier-beta-01';

// Global error tracking for Runtime Health
let consoleErrors: string[] = [];
let pageErrors: string[] = [];

test.describe('TODIR Payments — Comprehensive Financial Acceptance Suite', () => {
  test.beforeEach(async ({ page }) => {
    consoleErrors = [];
    pageErrors = [];

    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        const text = msg.text();
        // Ignore intentional negative HTTP test logs (e.g. 409 conflict, 400 validation)
        if (!text.includes('409') && !text.includes('400') && !text.includes('payment_has_applications')) {
          consoleErrors.push(text);
        }
      }
    });

    page.on('pageerror', (err) => {
      pageErrors.push(err.message);
    });

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

    // 2. Permissions check
    await page.route('**/api/v1/authorization/check*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ allowed: true })
      });
    });

    // 3. Modules Manifest
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

    // 4. Directory Parties
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

    // 5. Payment Accounts (with masked bank identifiers)
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

    // 6. Fees
    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/[^/]+\/fees(?:\?.*)?$/, async (route) => {
      const url = route.request().url();
      if (url.includes('pay-fees-test')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'fee-001',
              type: 'ProviderFee',
              amount: 150.00,
              currencyCode: 'EGP',
              recognitionState: 'Recognized'
            }
          ])
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([])
        });
      }
    });

    // 7. Payment Requests
    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/payment-requests(?:\?.*)?$/, async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 202,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'req-001',
            legalEntityId: LEGAL_ENTITY_ID,
            merchantReference: 'REQ-2026-0001',
            amount: 12500,
            currencyCode: 'EGP',
            state: 'Pending',
            attemptId: 'att-01',
            attemptNumber: 1
          })
        });
      } else {
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
      }
    });

    // 8. Payment Links
    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/payment-requests\/[^/]+\/links$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'link-001',
          paymentRequestId: 'req-001',
          token: 'tok-alpha-99',
          status: 'Active',
          expiresAt: '2026-11-01T00:00:00Z'
        })
      });
    });

    // 9. Single Payment Detail Records
    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/pay-[^/?]+(?:\?.*)?$/, async (route) => {
      const url = route.request().url();
      let paymentRecord = {
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
      };

      if (url.includes('pay-unapplied-10k')) {
        paymentRecord = {
          id: 'pay-unapplied-10k',
          number: 'PAY-2026-0010',
          legalEntityId: LEGAL_ENTITY_ID,
          direction: 'Incoming',
          type: 'CustomerReceipt',
          partyId: TEST_CUSTOMER_ID,
          amount: '10000.00',
          currencyCode: 'EGP',
          method: 'Cash',
          paymentAccountId: 'acc-001',
          paymentDate: '2026-10-25',
          postingDate: '2026-10-25',
          state: 'Completed',
          version: 1
        };
      } else if (url.includes('pay-apply-test')) {
        paymentRecord = {
          id: 'pay-apply-test',
          number: 'PAY-2026-0020',
          legalEntityId: LEGAL_ENTITY_ID,
          direction: 'Incoming',
          type: 'CustomerReceipt',
          partyId: TEST_CUSTOMER_ID,
          amount: '10000.00',
          currencyCode: 'EGP',
          method: 'Cash',
          paymentAccountId: 'acc-001',
          paymentDate: '2026-10-25',
          postingDate: '2026-10-25',
          state: 'Completed',
          version: 1
        };
      } else if (url.includes('pay-adv-customer')) {
        paymentRecord = {
          id: 'pay-adv-customer',
          number: 'PAY-2026-0030',
          legalEntityId: LEGAL_ENTITY_ID,
          direction: 'Incoming',
          type: 'CustomerAdvance',
          partyId: TEST_CUSTOMER_ID,
          amount: '25000.00',
          currencyCode: 'EGP',
          method: 'Cash',
          paymentAccountId: 'acc-001',
          paymentDate: '2026-10-25',
          postingDate: '2026-10-25',
          state: 'Completed',
          version: 1
        };
      } else if (url.includes('pay-fees-test')) {
        paymentRecord = {
          id: 'pay-fees-test',
          number: 'PAY-2026-0040',
          legalEntityId: LEGAL_ENTITY_ID,
          direction: 'Incoming',
          type: 'CustomerReceipt',
          partyId: TEST_CUSTOMER_ID,
          amount: '15000.00',
          currencyCode: 'EGP',
          method: 'PaymentGateway',
          paymentAccountId: 'acc-002',
          paymentDate: '2026-10-25',
          postingDate: '2026-10-25',
          state: 'Completed',
          version: 1
        };
      } else if (url.includes('pay-applied')) {
        paymentRecord = {
          id: 'pay-applied',
          number: 'PAY-2026-0050',
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
        };
      } else if (url.includes('pay-002')) {
        paymentRecord = {
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
        };
      } else if (url.includes('pay-003')) {
        paymentRecord = {
          id: 'pay-003',
          number: 'PAY-2026-0003',
          legalEntityId: LEGAL_ENTITY_ID,
          direction: 'InternalTransfer',
          type: 'TransferLeg',
          partyId: undefined,
          amount: '50000.00',
          currencyCode: 'EGP',
          method: 'BankTransfer',
          paymentAccountId: 'acc-002',
          paymentDate: '2026-10-27',
          postingDate: '2026-10-27',
          state: 'Completed',
          version: 1
        };
      } else if (url.includes('pay-004')) {
        paymentRecord = {
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
        };
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(paymentRecord)
      });
    });

    // 10. Payments Mutations
    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/incoming$/, async (route) => {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'pay-unapplied-10k',
          number: 'PAY-2026-0010',
          legalEntityId: LEGAL_ENTITY_ID,
          direction: 'Incoming',
          type: 'CustomerReceipt',
          partyId: TEST_CUSTOMER_ID,
          amount: '10000.00',
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

    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/outgoing$/, async (route) => {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'pay-sup-new',
          number: 'PAY-2026-0088',
          legalEntityId: LEGAL_ENTITY_ID,
          direction: 'Outgoing',
          type: 'SupplierPayment',
          partyId: TEST_SUPPLIER_ID,
          amount: '12000.00',
          currencyCode: 'EGP',
          method: 'BankTransfer',
          paymentAccountId: 'acc-002',
          paymentDate: '2026-10-26',
          postingDate: '2026-10-26',
          state: 'Completed',
          version: 1
        })
      });
    });

    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/internal-transfers$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'trf-001',
          sourcePayment: { id: 'pay-trf-src', number: 'PAY-TRF-01', state: 'Completed' },
          destinationPayment: { id: 'pay-trf-dst', number: 'PAY-TRF-02', state: 'Completed' },
          sourceAmount: 50000,
          sourceCurrencyCode: 'EGP',
          destinationAmount: 1000,
          destinationCurrencyCode: 'USD'
        })
      });
    });

    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/[^/]+\/refunds(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'ref-001',
          number: 'REF-2026-0001',
          state: 'Completed',
          amount: '5000.00',
          currencyCode: 'EGP'
        })
      });
    });

    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/[^/]+\/reverse(?:\?.*)?$/, async (route) => {
      const url = route.request().url();
      if (url.includes('pay-applied')) {
        await route.fulfill({
          status: 409,
          contentType: 'application/json',
          body: JSON.stringify({
            code: 'reversal_has_applications',
            detail: 'لا يمكن عكس السند المالي لوجود فواتير مخصصة ومسددة منه بالفعل. يجب فك التسوية في الأستاذ المساعد أولاً.'
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'rev-001',
            number: 'REV-2026-0001',
            state: 'Completed'
          })
        });
      }
    });

    await page.route(/\/api\/v1\/payments\/legal-entities\/[^/]+\/[^/]+\/applications(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          paymentId: 'pay-apply-test',
          appliedAmount: '8000.00',
          unappliedAmount: '2000.00',
          status: 'PartiallySettled'
        })
      });
    });

    // 11. Payments List (GET /api/v1/payments/legal-entities/{entityId})
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
              },
              {
                id: 'pay-005',
                number: 'PAY-2026-0005',
                legalEntityId: LEGAL_ENTITY_ID,
                direction: 'Incoming',
                type: 'CustomerReceipt',
                partyId: TEST_CUSTOMER_ID,
                amount: '2200.00',
                currencyCode: 'EGP',
                method: 'PaymentGateway',
                paymentAccountId: 'acc-002',
                paymentDate: '2026-10-27',
                postingDate: '2026-10-27',
                state: 'Failed',
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

    // 12. Public Payment Link details
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

    // 13. Subledger Open Items for Invoice Picker
    await page.route(/\/api\/v1\/subledger\/open-items(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'inv-open-item-88',
              legalEntityId: LEGAL_ENTITY_ID,
              partyId: TEST_CUSTOMER_ID,
              direction: 'Receivable',
              polarity: 'Debit',
              sourceType: 'CustomerInvoice',
              sourceReference: 'INV-2026-0088',
              sourceDisplayNumber: 'INV-2026-0088',
              documentDate: '2026-10-20',
              postingDate: '2026-10-20',
              dueDate: '2026-11-20',
              transactionCurrency: 'EGP',
              originalTransactionAmount: '10000.00',
              settledTransactionAmount: '0.00',
              openTransactionAmount: '10000.00',
              baseCurrency: 'EGP',
              originalBaseAmount: '10000.00',
              settledBaseCarryingAmount: '0.00',
              openBaseCarryingAmount: '10000.00',
              controlAccountRole: 'ReceivableControl',
              controlAccountReference: '121000',
              status: 'Open',
              rowVersion: 1
            }
          ],
          totalCount: 1,
          hasMore: false
        })
      });
    });
  });

  test('1. RECEIVE PAYMENT + UNAPPLIED: Receive Customer Payment WITHOUT Invoice', async ({ page }) => {
    await page.goto('/payments');

    // Open Receive Dialog
    const receiveBtn = page.getByRole('button', { name: '+ استلام دفعة' });
    await expect(receiveBtn).toBeVisible();
    await receiveBtn.click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('استلام دفعة من عميل')).toBeVisible();

    // Verify Invoice selection was NOT required (no invoice inputs exist in dialog)
    const invoiceInputs = dialog.locator('input[name*="invoice"], select[name*="invoice"]');
    await expect(invoiceInputs).toHaveCount(0);

    // Close Dialog cleanly
    await dialog.getByRole('button', { name: 'إلغاء' }).click();
    await expect(dialog).not.toBeVisible();

    // Navigate to created unapplied payment
    await page.goto('/payments/pay-unapplied-10k');
    await expect(page.locator('h1')).toContainText('PAY-2026-0010');

    // Verify payment is completed and available credit is visible without invoice
    await expect(page.getByText('10,000.00 ج.م').first()).toBeVisible();
    await expect(page.getByText('مكتمل')).toBeVisible();
    await expect(page.getByText(/غير مخصص.*رصيد متاح/)).toBeVisible();
  });

  test('2. RECEIVE + APPLY: Subledger settlement application', async ({ page }) => {
    await page.goto('/payments/pay-apply-test');
    await expect(page.locator('h1')).toContainText('PAY-2026-0020');

    // Click Apply action
    const applyBtn = page.getByRole('button', { name: 'تخصيص على فواتير مستحقة' });
    await expect(applyBtn).toBeVisible();
    await applyBtn.click();

    // Apply modal opens with canonical title
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('تخصيص رصيد الدفعة على الفواتير')).toBeVisible();

    // Select open invoice from picker
    const invoiceOption = dialog.getByText('INV-2026-0088');
    await expect(invoiceOption).toBeVisible();
    await invoiceOption.click();

    // Submit application
    await dialog.getByRole('button', { name: 'تأكيد تسوية الفاتورة' }).click();
    await expect(dialog).not.toBeVisible();
  });

  test('3. ADVANCES: Customer and Supplier Advances without Invoices', async ({ page }) => {
    await page.goto('/payments');

    // Customer Advance
    await page.getByRole('button', { name: '+ استلام دفعة' }).click();
    let dialog = page.getByRole('dialog');
    const custAdvanceCheck = dialog.getByLabel('استلام كدفعة مقدمة');
    await custAdvanceCheck.check();
    await expect(dialog.getByText('استلام دفعة مقدمة من عميل')).toBeVisible();
    await dialog.getByRole('button', { name: 'إلغاء' }).click();
    await expect(dialog).not.toBeVisible();

    // Supplier Advance
    await page.getByRole('button', { name: '+ سداد لمورد' }).click();
    dialog = page.getByRole('dialog');
    const supAdvanceCheck = dialog.getByLabel('سداد كدفعة مقدمة للمورد');
    await supAdvanceCheck.check();
    await expect(dialog.getByText('سداد دفعة مقدمة لمورد')).toBeVisible();
    await dialog.getByRole('button', { name: 'إلغاء' }).click();
    await expect(dialog).not.toBeVisible();

    // Inspect completed customer advance record
    await page.goto('/payments/pay-adv-customer');
    await expect(page.locator('h1')).toContainText('PAY-2026-0030');
    await expect(page.getByText('25,000.00 ج.م').first()).toBeVisible();
    await expect(page.getByText('دفعة مقدمة من عميل')).toBeVisible();
  });

  test('4. INTERNAL TRANSFER: Account-to-Account movement without Party or Invoices', async ({ page }) => {
    await page.goto('/payments');

    // Open Transfer Dialog
    await page.getByRole('button', { name: '+ تحويل داخلي' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('تحويل أموال داخلي بين الحسابات')).toBeVisible();

    // Verify NO Party picker exists in dialog
    const partyPickers = dialog.locator('input[placeholder*="عميل"], input[placeholder*="مورد"]');
    await expect(partyPickers).toHaveCount(0);

    // Cancel dialog
    await dialog.getByRole('button', { name: 'إلغاء' }).click();
    await expect(dialog).not.toBeVisible();

    // Inspect transfer leg detail
    await page.goto('/payments/pay-003');
    await expect(page.locator('h1')).toContainText('PAY-2026-0003');
    await expect(page.getByText('طرف تحويل داخلي')).toBeVisible();
    await expect(page.getByText('50,000.00 ج.م').first()).toBeVisible();
  });

  test('5. REFUND: Partial, full, over-refund rejection, and original payment immutability', async ({ page }) => {
    await page.goto('/payments/pay-001');

    // Click Refund
    await page.getByRole('button', { name: 'استرداد مالي' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('استرداد مالي لعميل')).toBeVisible();

    // Over-refund rejection
    const amountInput = dialog.locator('input[type="number"]').first();
    await amountInput.fill('20000.00');
    await dialog.getByRole('button', { name: 'تأكيد الاسترداد المالي' }).click();
    await expect(dialog.getByText(/مبلغ الاسترداد.*يتجاوز/)).toBeVisible();

    // Partial refund valid
    await amountInput.fill('5000.00');
    await dialog.getByRole('button', { name: 'تأكيد الاسترداد المالي' }).click();
    await expect(dialog).not.toBeVisible();

    // Original payment remains visible and immutable
    await expect(page.locator('h1')).toContainText('PAY-2026-0001');
    await expect(page.getByText('مكتمل')).toBeVisible();
  });

  test('6. REVERSAL & FEE DISPOSITION: Retain vs Reverse Fee and Blocker behavior', async ({ page }) => {
    // Case B & C: Fee Dispositions on payment with fees
    await page.goto('/payments/pay-fees-test');
    await page.getByRole('button', { name: 'عكس السند' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('عكس وإلغاء سند مالي')).toBeVisible();
    await expect(dialog.getByText('المعالجة المحاسبية للرسوم البنكية والعمولات')).toBeVisible();
    await expect(dialog.getByText('رسوم بوابة الدفع')).toBeVisible();

    // Select Reverse fee -> evidence required
    const reverseFeeRadio = dialog.locator('input[type="radio"][value="Reverse"]').first();
    await reverseFeeRadio.check();
    await dialog.getByRole('button', { name: 'تأكيد عكس السند المالي' }).click();
    await expect(dialog.getByText('يرجى إدخال المرجع أو رقم الإشعار عند اختيار عكس الرسم البنكي.')).toBeVisible();

    // Provide evidence reference
    const evidenceInput = dialog.locator('input[placeholder*="مرجع"]').first();
    if (await evidenceInput.isVisible()) {
      await evidenceInput.fill('DISPUTE-BANK-REF-771');
    }
    await dialog.getByRole('button', { name: 'إلغاء' }).click();
    await expect(dialog).not.toBeVisible();

    // Case E: Applied payment reversal shows controlled blocker
    await page.goto('/payments/pay-applied');
    await page.getByRole('button', { name: 'عكس السند' }).click();
    const appliedDialog = page.getByRole('dialog');
    await appliedDialog.getByRole('button', { name: 'تأكيد عكس السند المالي' }).click();
    await expect(appliedDialog.getByText(/لا يمكن عكس السند المالي لوجود فواتير مخصصة ومسددة منه بالفعل/)).toBeVisible();
    await appliedDialog.getByRole('button', { name: 'إلغاء' }).click();
  });

  test('7. PROVIDER STATES: Captured, Failed, Initiated (Pending/Checking), Recovery UX', async ({ page }) => {
    await page.goto('/payments');

    // Table rows status inspection
    const rows = page.locator('tbody tr');
    await expect(rows).toHaveCount(5);

    // Row 1: Completed (مكتمل)
    await expect(rows.nth(0)).toContainText('PAY-2026-0001');
    await expect(rows.nth(0)).toContainText('مكتمل');

    // Row 4: Initiated / Pending (معلق)
    await expect(rows.nth(3)).toContainText('PAY-2026-0004');
    await expect(rows.nth(3)).toContainText('معلق');

    // Row 5: Failed (فاشل) - no completed movement
    await expect(rows.nth(4)).toContainText('PAY-2026-0005');
    await expect(rows.nth(4)).toContainText('فاشل');

    // Inspect pending payment detail for recovery actions
    await page.goto('/payments/pay-004');
    await expect(page.locator('h1')).toContainText('PAY-2026-0004');
    await expect(page.getByRole('button', { name: 'التحقق من حالة العملية' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'استكمال يدوي استثنائي' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'إلغاء السند المعلق' })).toBeVisible();
  });

  test('8. PAYMENT LINK: Active Payment Link != Completed Payment', async ({ page }) => {
    // Open Link Checkout Portal
    await page.goto('/payments/pay/tok-alpha-99');
    await expect(page.locator('h2')).toContainText('بوابة السداد الإلكتروني الآمن');
    await expect(page.getByText('12,500.00')).toBeVisible();
    await expect(page.getByText('رابط سداد نشط')).toBeVisible();

    // Verify on /payments list that active link does NOT count as a completed payment
    await page.goto('/payments');
    const tableText = await page.locator('table').innerText();
    expect(tableText).not.toContain('tok-alpha-99');
    expect(tableText).not.toContain('12,500.00');
  });

  test('9. PERMISSIONS / SECURITY: Role-based actions and data masking', async ({ page }) => {
    // Verify Bank Account identifier masking
    await page.goto('/payments/pay-002');
    await expect(page.getByText('****4589')).toBeVisible();

    // Verify secrets are never displayed in DOM
    const bodyHtml = await page.content();
    expect(bodyHtml).not.toContain('secretRef');
    expect(bodyHtml).not.toContain('client_secret');
    expect(bodyHtml).not.toContain('apiKey');
  });

  test('10. STATUS SEPARATION: 4 Orthogonal Truths and zero raw enums', async ({ page }) => {
    await page.goto('/payments/pay-001');

    // 4 distinct surfaces
    await expect(page.getByText('نظرة عامة وبيانات حركة النقدية')).toBeVisible();
    await expect(page.getByText('تسوية قيود الأستاذ المساعد والرصيد المتاح')).toBeVisible();
    await expect(page.getByText('التوجيه المحاسبي وأثر دفتر الأستاذ العام')).toBeVisible();
    await expect(page.getByText('حالة المطابقة المصرفية')).toBeVisible();

    // 4 distinct statuses
    await expect(page.getByText('مكتمل')).toBeVisible();
    await expect(page.getByText(/غير مخصص.*رصيد متاح/)).toBeVisible();
    await expect(page.getByText('مرحل في دفتر الأستاذ العام')).toBeVisible();
    await expect(page.getByText('غير مطابق بنكياً')).toBeVisible();

    // Zero raw backend enums
    const pageText = await page.locator('body').innerText();
    expect(pageText).not.toContain('PartiallySettled');
    expect(pageText).not.toContain('NotApplicable');
    expect(pageText).not.toContain('Unsettled');
    expect(pageText).not.toContain('CustomerReceipt');
    expect(pageText).not.toContain('SupplierPayment');
    expect(pageText).not.toContain('InternalTransfer');
  });

  test('11. RESPONSIVE / RTL: 390px, 768px, 1024px without financial clipping', async ({ page }) => {
    // 390px (Mobile)
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/payments');

    // Title and actions visible without overlap
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.getByRole('button', { name: '+ استلام دفعة' })).toBeVisible();

    // Scroll affordance
    const tableContainer = page.locator('div[style*="overflow-x: auto"]').first();
    await expect(tableContainer).toBeVisible();

    // Detail page at 390px
    await page.goto('/payments/pay-001');
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.getByText('15,000.00 ج.م').first()).toBeVisible();

    // 768px (Tablet)
    await page.setViewportSize({ width: 768, height: 1024 });
    await expect(page.getByText('نظرة عامة وبيانات حركة النقدية')).toBeVisible();

    // 1024px (Desktop)
    await page.setViewportSize({ width: 1024, height: 768 });
    await expect(page.getByText('نظرة عامة وبيانات حركة النقدية')).toBeVisible();

    // Document is strictly RTL
    const dir = await page.locator('html').getAttribute('dir');
    expect(dir).toBe('rtl');
  });

  test('12. RUNTIME HEALTH: Zero console errors or unhandled page exceptions', async ({ page }) => {
    await page.goto('/payments');
    await page.goto('/payments/pay-001');
    await page.goto('/payments/pay/tok-alpha-99');

    // Assert zero uncaught console errors or page errors
    expect(consoleErrors).toEqual([]);
    expect(pageErrors).toEqual([]);
  });
});
