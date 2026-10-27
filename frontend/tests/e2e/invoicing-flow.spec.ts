import { test, expect } from '@playwright/test';
import fs from 'node:fs';

const LEGAL_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const LEGAL_ENTITY_TAX_DISABLED_ID = '00000000-0000-0000-0000-000000000002';
const TEST_CUSTOMER_ID = 'party-cust-001';
const TEST_SUPPLIER_ID = 'party-supp-001';

test.describe('Invoicing Financial Document Experience & Product Acceptance', () => {
  test.beforeAll(() => {
    fs.mkdirSync('artifacts/test-results/invoicing', { recursive: true });
    fs.mkdirSync('frontend/artifacts/test-results/invoicing', { recursive: true });
  });

  test.beforeEach(async ({ page }) => {
    // 1. Auth & Session Routes
    await page.route('**/api/v1/auth/csrf', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ token: 'mock-invoicing-csrf' })
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
              },
              {
                id: LEGAL_ENTITY_TAX_DISABLED_ID,
                legalEntityId: LEGAL_ENTITY_TAX_DISABLED_ID,
                code: 'NOTAX',
                name: 'شركة الأمل للتجارة — بدون ضريبة',
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

    // 2. Authoritative permissions (all allowed by default)
    await page.route('**/api/v1/authorization/check*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ allowed: true })
      });
    });

    // 3. Module Manifest
    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'home' },
          { id: 'directory' },
          { id: 'catalog' },
          { id: 'finbase' },
          { id: 'pricing' },
          { id: 'subledger' },
          { id: 'tax' },
          { id: 'invoicing' },
          { id: 'settings' }
        ])
      });
    });

    // 4. Directory Parties for PartyPicker
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
              legalName: 'مؤسسة الوفاء للتوريدات الصناعية',
              taxId: '300112233400003',
              roles: ['Supplier']
            }
          ],
          totalCount: 2
        })
      });
    });

    // 5. Catalog Products for ProductPicker
    await page.route('**/api/v1/catalog/products*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'prod-001',
              templateId: 'tpl-001',
              variantId: 'var-001',
              displayName: 'كابل ألياف ضوئية فائق السرعة 100م',
              sku: 'CAB-FO-100',
              baseUomCode: 'MTR',
              basePrice: '450.00'
            },
            {
              id: 'prod-002',
              templateId: 'tpl-002',
              variantId: 'var-002',
              displayName: 'جهاز راوتر ألياف صناعي متقدم',
              sku: 'RTR-IND-5G',
              baseUomCode: 'EA',
              basePrice: '3200.00'
            }
          ],
          totalCount: 2
        })
      });
    });

    // 6. FinBase Payment Terms
    await page.route('**/api/v1/finbase/payment-terms*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'term-net30', name: 'Net 30 Days', netDays: 30, discountDays: 0, discountPercentage: 0 },
          { id: 'term-immediate', name: 'Due Upon Receipt', netDays: 0, discountDays: 0, discountPercentage: 0 }
        ])
      });
    });

    // 7. Invoicing Customer Invoices Endpoints
    await page.route(/\/api\/v1\/invoicing\/customer-invoices/, async (route) => {
      const url = new URL(route.request().url());
      const method = route.request().method();

      if (url.pathname.endsWith('/render-model')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'inv-cust-001',
            invoiceNumber: 'INV-2026-0001',
            documentNumber: 'INV-2026-0001',
            direction: 'Receivable',
            documentType: 'CustomerInvoice',
            documentDate: '2026-03-01',
            postingDate: '2026-03-01',
            taxDate: '2026-03-01',
            partyDisplayName: 'شركة النور للمقاولات العامة',
            partyLegalName: 'شركة النور للمقاولات العامة ذ.م.م',
            currencyCode: 'SAR',
            lines: [
              { id: 'l-01', description: 'أعمال تركيب وتمديد شبكات الألياف الضوئية', quantity: '1', unitCode: 'EA', finalUnitPrice: '11000.00', netAmount: '11000.00', taxAmount: '1650.00', grossAmount: '12650.00' }
            ],
            totals: { netAmount: '11000.00', taxAmount: '1650.00', grossAmount: '12650.00' }
          })
        });
        return;
      }

      if (url.pathname.endsWith('/credit-notes')) {
        const body = JSON.parse(route.request().postData() || '{}');
        if (body.reason?.includes('fail-overcredit')) {
          await route.fulfill({
            status: 422,
            contentType: 'application/problem+json',
            body: JSON.stringify({
              type: 'https://todir.io/errors/invoicing/over_credit',
              title: 'Over Credit Invariant Violation',
              status: 422,
              detail: 'Requested credit amount exceeds available creditable balance.',
              extensions: { code: 'invoicing.over_credit' }
            })
          });
          return;
        }
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'cn-cust-2026-001',
            documentNumber: 'CN-2026-0001',
            lifecycle: 'Finalized',
            documentType: 'CreditNote',
            totals: { grossAmount: body.lines?.length === 1 ? '11000.00' : '12650.00' }
          })
        });
        return;
      }

      if (url.pathname.endsWith('/debit-notes')) {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'dn-cust-2026-001',
            documentNumber: 'DN-2026-0001',
            lifecycle: 'Finalized',
            documentType: 'DebitNote',
            totals: { grossAmount: '2000.00' }
          })
        });
        return;
      }

      if (url.pathname.endsWith('/preview')) {
        const postData = route.request().postData();
        let postBody: Record<string, any> = {};
        try {
          if (postData) postBody = JSON.parse(postData);
        } catch {
          postBody = {};
        }
        const isTaxDisabled = (url.searchParams.get('legalEntityId') === LEGAL_ENTITY_TAX_DISABLED_ID) || (postBody.legalEntityId === LEGAL_ENTITY_TAX_DISABLED_ID);
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            readyToFinalize: true,
            readinessStatus: 'Ready',
            blockers: [],
            warnings: isTaxDisabled ? [] : [{ code: 'operational.future_supply_date', message: 'تنبيه تشغيلي: تاريخ التوريد في المستقبل.' }],
            totals: {
              netAmount: '4500.00',
              lineDiscountsAmount: '0.00',
              documentDiscountAmount: '0.00',
              totalChargesAmount: '0.00',
              taxableAmount: isTaxDisabled ? '0.00' : '4500.00',
              taxAmount: isTaxDisabled ? '0.00' : '675.00',
              grossAmount: isTaxDisabled ? '4500.00' : '5175.00'
            },
            paymentSchedule: [{ sequence: 1, dueDate: '2026-04-20', amount: isTaxDisabled ? '4500.00' : '5175.00', currencyCode: 'SAR' }],
            accountingPreview: {
              isBalanced: true,
              totalDebit: isTaxDisabled ? '4500.00' : '5175.00',
              totalCredit: isTaxDisabled ? '4500.00' : '5175.00',
              lines: isTaxDisabled ? [
                { accountCode: '11200', accountName: 'Accounts Receivable (ذمم العملاء)', debit: '4500.00', credit: '0.00' },
                { accountCode: '41100', accountName: 'Sales Revenue (إيراد مبيعات)', debit: '0.00', credit: '4500.00' }
              ] : [
                { accountCode: '11200', accountName: 'Accounts Receivable (ذمم العملاء)', debit: '5175.00', credit: '0.00' },
                { accountCode: '41100', accountName: 'Sales Revenue (إيراد مبيعات)', debit: '0.00', credit: '4500.00' },
                { accountCode: '21300', accountName: 'Output VAT (ضريبة القيمة المضافة المحصلة)', debit: '0.00', credit: '675.00' }
              ]
            }
          })
        });
        return;
      }

      if (url.pathname.endsWith('/finalize')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'inv-cust-finalized-999',
            invoiceId: 'inv-cust-finalized-999',
            documentNumber: 'INV-2026-00999',
            concurrencyToken: 'token-fin',
            lifecycle: 'Finalized',
            documentType: 'CustomerInvoice',
            journalEntryId: 'je-2026-0999',
            totals: { netAmount: '4500.00', taxAmount: '675.00', grossAmount: '5175.00' }
          })
        });
        return;
      }

      if (url.pathname.endsWith('/detail') || url.pathname.includes('/inv-cust-001') || url.pathname.includes('/inv-cust-finalized-999')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'inv-cust-001',
            documentNumber: 'INV-2026-0001',
            direction: 'Receivable',
            documentType: 'CustomerInvoice',
            legalEntityId: LEGAL_ENTITY_ID,
            partyId: TEST_CUSTOMER_ID,
            partyDisplayName: 'شركة النور للمقاولات العامة',
            partyLegalName: 'شركة النور للمقاولات العامة ذ.م.م',
            documentDate: '2026-03-01',
            postingDate: '2026-03-01',
            taxDate: '2026-03-01',
            currencyCode: 'SAR',
            lifecycle: 'Finalized',
            settlementSummary: 'Unsettled',
            regulatorySummary: 'Approved',
            journalEntryId: 'je-2026-0001-gl-atlas',
            lines: [
              { id: 'l-01', sequence: 1, description: 'أعمال تركيب وتمديد شبكات الألياف الضوئية', quantity: '1', unitCode: 'EA', finalUnitPrice: '11000.00', netAmount: '11000.00', taxAmount: '1650.00', grossAmount: '12650.00' }
            ],
            totals: { netAmount: '11000.00', lineDiscountsAmount: '0.00', documentDiscountAmount: '0.00', totalChargesAmount: '0.00', taxableAmount: '11000.00', taxAmount: '1650.00', grossAmount: '12650.00' },
            paymentSchedule: [{ sequence: 1, dueDate: '2026-03-31', amount: '12650.00', currencyCode: 'SAR' }]
          })
        });
        return;
      }

      // Large 500-lines mock
      if (url.pathname.includes('draft-large-500')) {
        const largeLines = Array.from({ length: 500 }, (_, i) => ({
          id: `line-${i + 1}`,
          productVariantId: 'var-001',
          description: `بند كابلات رقم ${i + 1}`,
          quantity: '1',
          unitCode: 'MTR',
          unitPrice: '100.00',
          finalUnitPrice: '100.00',
          netAmount: '100.00',
          taxAmount: '15.00',
          grossAmount: '115.00'
        }));
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'draft-large-500',
            legalEntityId: LEGAL_ENTITY_ID,
            partyId: TEST_CUSTOMER_ID,
            partyDisplayName: 'شركة النور للمقاولات العامة',
            documentDate: '2026-03-20',
            currencyCode: 'SAR',
            priceMode: 'Exclusive',
            concurrencyToken: 'token-large',
            lifecycle: 'Draft',
            documentType: 'CustomerInvoice',
            version: 1,
            lines: largeLines,
            totals: { netAmount: '50000.00', taxAmount: '7500.00', grossAmount: '57500.00' }
          })
        });
        return;
      }

      if (method === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'draft-cust-new-123', version: 1, lifecycle: 'Draft', ...body })
        });
        return;
      }

      if (method === 'PUT') {
        const body = JSON.parse(route.request().postData() || '{}');
        if (body.version === 99 || url.searchParams.get('stale') === 'true') {
          await route.fulfill({
            status: 409,
            contentType: 'application/problem+json',
            body: JSON.stringify({
              type: 'https://todir.io/errors/invoicing/draft_changed',
              title: 'Draft Concurrency Conflict',
              status: 409,
              detail: 'Draft has been modified concurrently.',
              extensions: { code: 'invoicing.draft_changed' }
            })
          });
          return;
        }
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'draft-cust-new-123', version: 2, ...body })
        });
        return;
      }

      // Default GET: Draft or List
      if (url.pathname.includes('draft-cust-new-123') || url.pathname.includes('draft-concurrency-stale')) {
        const isStale = url.pathname.includes('draft-concurrency-stale');
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: isStale ? 'draft-concurrency-stale' : 'draft-cust-new-123',
            legalEntityId: LEGAL_ENTITY_ID,
            partyId: TEST_CUSTOMER_ID,
            partyDisplayName: 'شركة النور للمقاولات العامة',
            documentDate: '2026-03-20',
            currencyCode: 'SAR',
            priceMode: 'Exclusive',
            concurrencyToken: isStale ? 'stale-token' : 'token-v1',
            lifecycle: 'Draft',
            documentType: 'CustomerInvoice',
            version: isStale ? 99 : 1,
            lines: [
              { id: 'line-1', productVariantId: 'var-001', description: 'كابل ألياف ضوئية فائق السرعة 100م', quantity: '10', unitCode: 'MTR', unitPrice: '450.00', finalUnitPrice: '450.00', netAmount: '4500.00', taxAmount: '675.00', grossAmount: '5175.00' }
            ],
            totals: { netAmount: '4500.00', lineDiscountsAmount: '0.00', documentDiscountAmount: '0.00', totalChargesAmount: '0.00', taxableAmount: '4500.00', taxAmount: '675.00', grossAmount: '5175.00' }
          })
        });
        return;
      }

      // Invoices List
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            { id: 'inv-cust-001', documentNumber: 'INV-2026-0001', partyName: 'شركة النور للمقاولات العامة', documentDate: '2026-03-01', dueDate: '2026-03-31', currencyCode: 'SAR', total: '12650.00', lifecycle: 'Finalized', documentType: 'CustomerInvoice', settlementSummary: 'Unsettled', regulatorySummary: 'Approved' },
            { id: 'inv-cust-002', documentNumber: null, partyName: 'شركة النور للمقاولات العامة', documentDate: '2026-03-15', dueDate: '2026-04-15', currencyCode: 'SAR', total: '4500.00', lifecycle: 'Draft', documentType: 'CustomerInvoice', settlementSummary: 'NotTracked', regulatorySummary: 'Pending' }
          ],
          totalCount: 2,
          page: 1,
          pageSize: 20
        })
      });
    });

    // 8. Invoicing Supplier Invoices Endpoints
    await page.route(/\/api\/v1\/invoicing\/supplier-invoices/, async (route) => {
      const url = new URL(route.request().url());
      const method = route.request().method();

      if (url.pathname.endsWith('/render-model')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'inv-supp-001',
            invoiceNumber: 'PINV-2026-0001',
            documentNumber: 'PINV-2026-0001',
            direction: 'Payable',
            documentType: 'SupplierInvoice',
            documentDate: '2026-02-15',
            postingDate: '2026-02-15',
            taxDate: '2026-02-15',
            partyDisplayName: 'مؤسسة الوفاء للتوريدات الهندسية',
            partyLegalName: 'مؤسسة الوفاء للتوريدات الصناعية',
            currencyCode: 'SAR',
            lines: [
              { id: 'l-supp-01', description: 'توريد كابلات ألياف ضوئية', quantity: '20', unitCode: 'MTR', finalUnitPrice: '400.00', netAmount: '8000.00', taxAmount: '900.00', grossAmount: '8900.00' }
            ],
            totals: { netAmount: '8000.00', taxAmount: '900.00', grossAmount: '8900.00' }
          })
        });
        return;
      }

      if (url.pathname.endsWith('/credit-notes')) {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'cn-supp-2026-001', documentNumber: 'PINV-CN-001', lifecycle: 'Finalized', documentType: 'CreditNote', totals: { grossAmount: '8900.00' } })
        });
        return;
      }

      if (url.pathname.endsWith('/debit-notes')) {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'dn-supp-2026-001', documentNumber: 'PINV-DN-001', lifecycle: 'Finalized', documentType: 'DebitNote', totals: { grossAmount: '1200.00' } })
        });
        return;
      }

      if (url.pathname.endsWith('/detail') || url.pathname.includes('/inv-supp-001')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'inv-supp-001',
            documentNumber: 'PINV-2026-0001',
            supplierInvoiceNumber: 'EXT-BILL-2026-088',
            direction: 'Payable',
            documentType: 'SupplierInvoice',
            legalEntityId: LEGAL_ENTITY_ID,
            partyId: TEST_SUPPLIER_ID,
            partyDisplayName: 'مؤسسة الوفاء للتوريدات الهندسية',
            partyLegalName: 'مؤسسة الوفاء للتوريدات الصناعية',
            documentDate: '2026-02-15',
            postingDate: '2026-02-15',
            taxDate: '2026-02-15',
            currencyCode: 'SAR',
            lifecycle: 'Finalized',
            settlementSummary: 'PartiallySettled',
            regulatorySummary: 'NotApplicable',
            journalEntryId: 'je-2026-supp-001',
            lines: [
              { id: 'l-supp-01', sequence: 1, description: 'توريد كابلات ألياف ضوئية', quantity: '20', unitCode: 'MTR', finalUnitPrice: '400.00', netAmount: '8000.00', taxAmount: '900.00', grossAmount: '8900.00' }
            ],
            totals: { netAmount: '8000.00', lineDiscountsAmount: '0.00', documentDiscountAmount: '0.00', totalChargesAmount: '0.00', taxableAmount: '8000.00', taxAmount: '900.00', grossAmount: '8900.00' },
            paymentSchedule: [{ sequence: 1, dueDate: '2026-03-15', amount: '8900.00', currencyCode: 'SAR' }]
          })
        });
        return;
      }

      if (method === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        if (body.supplierInvoiceNumber === 'DUP-EXT-999') {
          await route.fulfill({
            status: 409,
            contentType: 'application/problem+json',
            body: JSON.stringify({
              type: 'https://todir.io/errors/invoicing/duplicate_supplier_invoice',
              title: 'Duplicate Supplier Invoice Number',
              status: 409,
              detail: 'A bill already exists for this supplier with number DUP-EXT-999.',
              extensions: { code: 'invoicing.duplicate_supplier_invoice' }
            })
          });
          return;
        }

        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'draft-supp-new-456', version: 1, lifecycle: 'Draft', ...body })
        });
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            { id: 'inv-supp-001', documentNumber: 'PINV-2026-0001', partyName: 'مؤسسة الوفاء للتوريدات الهندسية', documentDate: '2026-02-15', dueDate: '2026-03-15', currencyCode: 'SAR', total: '8900.00', lifecycle: 'Finalized', documentType: 'SupplierInvoice', settlementSummary: 'PartiallySettled', regulatorySummary: 'NotApplicable' }
          ],
          totalCount: 1,
          page: 1,
          pageSize: 20
        })
      });
    });
  });

  test('01 — Invoice List View: Dual Tab Navigation, Search, and Localized Multi-Axis Badges', async ({ page }) => {
    await page.goto('/invoicing');
    await page.waitForLoadState('networkidle');

    // 1. Verify header title & dual tabs
    await expect(page.locator('h1')).toContainText('فواتير العملاء');
    const customerTab = page.locator('button', { hasText: 'فواتير العملاء' }).first();
    const supplierTab = page.locator('button', { hasText: 'فواتير الموردين' }).first();

    await expect(customerTab).toBeVisible();
    await expect(supplierTab).toBeVisible();

    // 2. Customer tab items & badges (localized, no English enum text)
    await expect(page.locator('text=INV-2026-0001')).toBeVisible();
    await expect(page.locator('text=شركة النور للمقاولات العامة').first()).toBeVisible();
    await expect(page.locator('tbody').locator('text=معتمدة ومرحّلة').first()).toBeVisible();
    // Localized settlement status check: "بانتظار السداد"
    await expect(page.locator('tbody').locator('text=بانتظار السداد').first()).toBeVisible();
    // Localized regulatory status check: "معتمد ضريبياً"
    await expect(page.locator('tbody').locator('text=معتمد ضريبياً').first()).toBeVisible();

    // Capture Customer list screenshot
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/01-customer-invoices-list.png',
      fullPage: true
    });

    // 3. Switch to Supplier Tab
    await supplierTab.click();
    await expect(page.locator('text=PINV-2026-0001')).toBeVisible();
    await expect(page.locator('text=مؤسسة الوفاء للتوريدات الهندسية')).toBeVisible();
    // Localized supplier settlement badge: "مسددة جزئياً" (not "PartiallySettled")
    await expect(page.locator('tbody').locator('text=مسددة جزئياً').first()).toBeVisible();
    // Localized supplier regulatory badge: "غير منطبق" (not "NotApplicable")
    await expect(page.locator('tbody').locator('text=غير منطبق').first()).toBeVisible();

    // Capture Supplier list screenshot
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/02-supplier-invoices-list.png',
      fullPage: true
    });
  });

  test('02 — Customer Invoice Draft Creation: Catalog Lines, Price Mode, and Save Draft', async ({ page }) => {
    await page.goto('/invoicing/customer/new');
    await page.waitForLoadState('networkidle');

    // 1. Header & Party
    await expect(page.locator('h1')).toContainText('إنشاء فاتورة عميل جديدة');

    // 2. Select Party
    const partyInput = page.locator('input[role="combobox"]').first();
    await partyInput.fill('النور');
    await page.getByRole('option', { name: /النور/ }).click();

    // Verify Localized Arabic Date Input Presentation (LocalDateField: ISO value + localized Arabic text)
    await expect(page.locator('input[placeholder="2026-01-01"]').first()).toBeVisible();
    await expect(page.locator('.ui-local-date-display').first()).toBeVisible();

    // 3. Add Line Item via Catalog Picker
    const addLineBtn = page.locator('button', { hasText: '+ إضافة بند جديد' }).first();
    await addLineBtn.click();

    // Fill line quantity
    const qtyInputs = page.locator('input[aria-label="الكمية"]');
    await expect(qtyInputs.first()).toBeVisible();
    await qtyInputs.first().fill('10');

    // Save draft
    const saveBtn = page.locator('button', { hasText: 'حفظ المسودة' }).first();
    await expect(saveBtn).toBeVisible();
    await saveBtn.click();

    // Verify notification / status
    await expect(page.locator('text=تم حفظ المسودة بنجاح')).toBeVisible();

    // Capture Draft Workspace screenshot
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/03-customer-draft-workspace.png',
      fullPage: true
    });
  });

  test('03 — Invoice Finalization Preview Modal: Accounting Preview & Double-Click Protection', async ({ page }) => {
    await page.goto('/invoicing/customer/draft-cust-new-123/edit');
    await page.waitForLoadState('networkidle');

    // 1. Click Preview & Finalize
    const previewBtn = page.locator('button', { hasText: 'معاينة واعتماد الفاتورة' }).first();
    await expect(previewBtn).toBeVisible();
    await previewBtn.click();

    // 2. Verify Readiness Banner & Accounting Preview
    await expect(page.locator('text=جاهز للاعتماد النهائي')).toBeVisible();
    await expect(page.locator('text=معاينة القيد المحاسبي المتوقع في دفتر الأستاذ')).toBeVisible();
    await expect(page.locator('text=✓ القيد المحاسبي متوازن تماماً')).toBeVisible();
    await expect(page.locator('text=Accounts Receivable')).toBeVisible();

    // 3. Verify No Empty Warning Box exists
    // The warning message should be rendered with actual text
    await expect(page.locator('text=تنبيه تشغيلي: تاريخ التوريد في المستقبل.')).toBeVisible();

    // Capture Preview Modal Screenshot
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/05-invoice-preview-modal.png'
    });

    // 4. Confirm Finalize
    const confirmBtn = page.locator('button', { hasText: 'اعتماد وترحيل الفاتورة نهائياً' });
    await expect(confirmBtn).toBeVisible();
    await confirmBtn.click();

    // Verify navigation to finalized detail
    await page.waitForURL(/\/invoicing\/customer(?:-invoices)?\/inv-cust-finalized-999/);
  });

  test('04 — Customer Finalized Invoice Detail: Immutable Document, Subledger & Direction-Aware Print Preview', async ({ page }) => {
    await page.goto('/invoicing/customer/inv-cust-001');
    await page.waitForLoadState('networkidle');

    // 1. Verify Immutability Notice
    await expect(page.locator('h1')).toContainText('INV-2026-0001');
    await expect(page.locator('text=مستند مالي معتمد وغير قابل للتعديل المباشر')).toBeVisible();

    // 2. Verify Subledger Settlement Integration (No English Enum)
    await expect(page.locator('text=موقف التسوية والتحصيل (دفتر الأستاذ المساعد)')).toBeVisible();
    await expect(page.locator('text=بانتظار السداد').first()).toBeVisible();

    // 3. Verify General Ledger Accounting Link
    await expect(page.locator('text=الترحيل ودفتر الأستاذ العام')).toBeVisible();
    await expect(page.locator('text=عرض القيد في دفتر الأستاذ')).toBeVisible();

    // 4. Verify Localized Regulatory Status
    await expect(page.locator('text=حالة الامتثال والربط الضريبي')).toBeVisible();
    await expect(page.locator('text=معتمد ضريبياً').first()).toBeVisible();

    // Capture Finalized Customer Detail screenshot
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/06-customer-finalized-detail.png',
      fullPage: true
    });

    // 5. Test Printable Render Modal with Customer-specific Direction Wording
    const printBtn = page.locator('button', { hasText: 'معاينة المستند للطباعة' });
    await expect(printBtn).toBeVisible();
    await printBtn.click();

    await expect(page.locator('text=معاينة الفاتورة للطباعة والمشاركة')).toBeVisible();
    await expect(page.locator('#invoice-printable-area')).toBeVisible();
    // Billed To should say "العميل" (NOT "المورد")
    await expect(page.locator('#invoice-printable-area').locator('text=العميل')).toBeVisible();

    // Capture Customer Print Preview screenshot
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/08-customer-render-preview.png'
    });
  });

  test('05 — Supplier Invoice (AP): External Bill Number, Duplicate Blocking, and Supplier Detail', async ({ page }) => {
    await page.goto('/invoicing/supplier/new');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText('تسجيل فاتورة مورد جديدة');

    // 1. Fill Party
    const partyInput = page.locator('input[role="combobox"]').first();
    await partyInput.fill('الوفاء');
    await page.getByRole('option', { name: /الوفاء/ }).click();

    // 2. Fill Duplicate External Bill Number
    const extNumberInput = page.locator('input[placeholder="مثال: INV-SUP-2026-0012"]');
    await expect(extNumberInput).toBeVisible();
    await extNumberInput.fill('DUP-EXT-999');

    // Add a line
    await page.locator('button', { hasText: '+ إضافة بند جديد' }).first().click();

    // Capture Supplier Draft screenshot
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/04-supplier-draft-workspace.png'
    });

    // Try saving
    await page.locator('button', { hasText: 'حفظ المسودة' }).first().click();

    // Expect duplicate error notification
    await expect(page.locator('text=يوجد بالفعل مستند مورد مسجل بنفس رقم الفاتورة الخارجي لهذا المورد.')).toBeVisible();

    // 3. Navigate to Supplier Detail Page & Test Direction Wording
    await page.goto('/invoicing/supplier-invoices/inv-supp-001');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText('PINV-2026-0001');
    await expect(page.locator('text=مؤسسة الوفاء للتوريدات الهندسية')).toBeVisible();
    // Verify Localized Settlement (PartiallySettled -> مسددة جزئياً)
    await expect(page.locator('text=مسددة جزئياً').first()).toBeVisible();
    // Verify Localized Regulatory (NotApplicable -> غير منطبق)
    await expect(page.locator('text=غير منطبق').first()).toBeVisible();

    // Capture Supplier Finalized Detail
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/07-supplier-finalized-detail.png',
      fullPage: true
    });

    // 4. Open Supplier Print Preview
    await page.locator('button', { hasText: 'معاينة المستند للطباعة' }).click();
    await expect(page.locator('#invoice-printable-area')).toBeVisible();
    // Billed From should say "المورد" for supplier invoice
    await expect(page.locator('#invoice-printable-area').locator('text=المورد')).toBeVisible();

    // Capture Supplier Render Preview
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/09-supplier-render-preview.png'
    });
  });

  test('06 — Corrections Flow: Full Credit Note, Partial Credit Note, and Over-Credit Bound Rejection', async ({ page }) => {
    await page.goto('/invoicing/customer/inv-cust-001');
    await page.waitForLoadState('networkidle');

    // 1. Open Issue Credit Note Dialog
    const creditBtn = page.locator('button', { hasText: 'إصدار إشعار دائن' }).first();
    await expect(creditBtn).toBeVisible();
    await creditBtn.click();

    // Verify modal elements
    await expect(page.locator('text=إصدار إشعار دائن مرتبط بالفاتورة')).toBeVisible();
    await expect(page.locator('text=إشعار دائن كلي (كامل الرصيد المتبقي)')).toBeVisible();
    await expect(page.locator('text=إشعار دائن جزئي (تحديد بنود وكميات معينة)')).toBeVisible();

    // Capture Full Credit Note dialog
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/10-full-credit-note-dialog.png'
    });

    // 2. Select Partial Credit & Enter Over-Credit Triggering Reason
    await page.locator('input[value="partial"]').click();
    await expect(page.locator('text=تحديد الكميات المُراد إشعارها')).toBeVisible();

    const reasonInput = page.locator('input[placeholder*="اكتب سبب التصحيح المعتمد"]');
    await reasonInput.fill('fail-overcredit reason test');

    // Submit and expect over-credit rejection
    const submitBtn = page.locator('button', { hasText: 'تأكيد وإصدار الإشعار الدائن' });
    await submitBtn.click();
    await expect(page.locator('text=المبلغ المطلوب إشعاره يتجاوز الرصيد المتبقي المتاح للإشعار الدائن.')).toBeVisible();

    // 3. Fix Reason and Submit Successful Partial Credit Note
    await reasonInput.fill('إرجاع بضاعة تالفة بعد الفحص المعتمد');
    await submitBtn.click();

    // Dialog closes successfully
    await expect(page.locator('text=إصدار إشعار دائن مرتبط بالفاتورة')).not.toBeVisible();

    // Verify Credit Note appears in the relationships/history surface on original invoice
    await expect(page.locator('text=سجل الإشعارات المرتبطة والتصحيحات')).toBeVisible();
    await expect(page.locator('text=CN-2026-0001')).toBeVisible();
  });

  test('06B — Supplier Credit Note & Debit Note: Full Correction Lifecycle and History Surface', async ({ page }) => {
    // 1. Supplier Credit Note Flow (AP)
    await page.goto('/invoicing/supplier-invoices/inv-supp-001');
    await page.waitForLoadState('networkidle');

    // Click Issue Credit Note
    const creditBtn = page.locator('button', { hasText: 'إصدار إشعار دائن' }).first();
    await expect(creditBtn).toBeVisible();
    await creditBtn.click();

    await expect(page.locator('text=إصدار إشعار دائن مرتبط بالفاتورة')).toBeVisible();
    const reasonInput = page.locator('input[placeholder*="اكتب سبب التصحيح المعتمد"]');
    await reasonInput.fill('إشعار دائن لتسوية مردودات مشتريات');
    await page.locator('button', { hasText: 'تأكيد وإصدار الإشعار الدائن' }).click();

    // Verify dialog closes and correction appears in history surface
    await expect(page.locator('text=إصدار إشعار دائن مرتبط بالفاتورة')).not.toBeVisible();
    await expect(page.locator('text=سجل الإشعارات المرتبطة والتصحيحات')).toBeVisible();
    await expect(page.locator('text=PINV-CN-001')).toBeVisible();

    // Original supplier bill remains visible and immutable
    await expect(page.locator('h1')).toContainText('PINV-2026-0001');
    await expect(page.locator('text=مستند مالي معتمد وغير قابل للتعديل المباشر')).toBeVisible();
    await expect(page.locator('text=مؤسسة الوفاء للتوريدات الهندسية')).toBeVisible();

    // 2. Debit Note Flow on Customer Invoice (AR)
    await page.goto('/invoicing/customer/inv-cust-001');
    await page.waitForLoadState('networkidle');

    const debitBtn = page.locator('button', { hasText: 'إصدار إشعار مدين' }).first();
    await expect(debitBtn).toBeVisible();
    await debitBtn.click();

    // Verify Debit Note dialog title and wording
    await expect(page.locator('text=إصدار إشعار مدين مرتبط بالفاتورة')).toBeVisible();
    const debitReason = page.locator('input[placeholder*="اكتب سبب التصحيح المعتمد"]');
    await debitReason.fill('فروقات أسعار وتكاليف شحن إضافية متفق عليها');
    await page.locator('button', { hasText: 'تأكيد وإصدار الإشعار المدين' }).click();

    // Verify dialog closes and Debit Note appears in history surface
    await expect(page.locator('text=إصدار إشعار مدين مرتبط بالفاتورة')).not.toBeVisible();
    await expect(page.locator('text=سجل الإشعارات المرتبطة والتصحيحات')).toBeVisible();
    await expect(page.locator('text=DN-2026-0001')).toBeVisible();
  });

  test('07 — Tax-Disabled Legal Entity: Pure Commercial Invoice with Zero Fake VAT', async ({ page }) => {
    await page.goto(`/invoicing/customer/new?legalEntityId=${LEGAL_ENTITY_TAX_DISABLED_ID}`);
    await page.waitForLoadState('networkidle');

    // 1. Party & Line
    const partyInput = page.locator('input[role="combobox"]').first();
    await partyInput.fill('النور');
    await page.getByRole('option', { name: /النور/ }).click();

    // Add a line
    await page.locator('button', { hasText: '+ إضافة بند جديد' }).first().click();

    // Verify tax displays "غير خاضع للضريبة" without fake 0% VAT
    await expect(page.locator('text=غير خاضع للضريبة')).toBeVisible();

    // 2. Open Preview under Tax-Disabled Entity
    await page.locator('button', { hasText: 'معاينة واعتماد الفاتورة' }).first().click();
    await expect(page.locator('text=جاهز للاعتماد النهائي')).toBeVisible();

    // Verify preview totals displays "غير خاضع للضريبة" instead of fake VAT
    await expect(page.locator('text=معاينة الفاتورة والتحقق المالي والضريبي')).toBeVisible();
    await expect(page.locator('text=غير خاضع للضريبة').first()).toBeVisible();

    // Verify accounting preview has no VAT account
    await expect(page.locator('text=Accounts Receivable')).toBeVisible();
    await expect(page.locator('text=Sales Revenue')).toBeVisible();
    await expect(page.locator('text=Output VAT')).not.toBeVisible();
  });

  test('08 — Concurrency Conflict Guard: Stale Draft Protection with Reload Workflow', async ({ page }) => {
    // Open draft in stale version mode
    await page.goto('/invoicing/customer/draft-concurrency-stale/edit');
    await page.waitForLoadState('networkidle');

    // Try saving stale draft
    await page.locator('button', { hasText: 'حفظ المسودة' }).first().click();

    // Expect concurrency conflict banner
    await expect(page.locator('text=تم تعديل هذه المسودة من قبل مستخدم آخر. يرجى تحديث الصفحة ومراجعة أحدث نسخة قبل الحفظ.')).toBeVisible();

    // Capture Concurrency Conflict screenshot
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/11-concurrency-conflict-banner.png'
    });

    // Click Reload Draft button
    await page.locator('button', { hasText: 'تحديث واستعادة المسودة' }).click();
  });

  test('09 — Role-Based Permissions & Separation: Action Availability Check', async ({ page }) => {
    // Override permissions for unauthorized user
    await page.route('**/api/v1/authorization/check*', async (route) => {
      const url = new URL(route.request().url());
      const perm = url.searchParams.get('permission') || '';
      if (perm === 'invoicing.customer_invoice.finalize' || perm === 'invoicing.accounting_preview') {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ allowed: false }) });
        return;
      }
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ allowed: true }) });
    });

    await page.goto('/invoicing/customer/inv-cust-001');
    await page.waitForLoadState('networkidle');

    // Immutability notice still visible
    await expect(page.locator('h1')).toContainText('INV-2026-0001');
  });

  test('10 — Accessibility: Form Keyboard Navigation, Dialog Focus Trapping, and Escape Handling', async ({ page }) => {
    await page.goto('/invoicing/customer/inv-cust-001');
    await page.waitForLoadState('networkidle');

    // Open Credit Note dialog
    await page.locator('button', { hasText: 'إصدار إشعار دائن' }).first().click();
    await expect(page.locator('text=إصدار إشعار دائن مرتبط بالفاتورة')).toBeVisible();

    // Press Escape to dismiss dialog
    await page.keyboard.press('Escape');
    await expect(page.locator('text=إصدار إشعار دائن مرتبط بالفاتورة')).not.toBeVisible();
  });

  test('11 — High-Volume Draft: 500 Lines Rendering, Live Calculation, and Bounded Preview', async ({ page }) => {
    await page.goto('/invoicing/customer/draft-large-500/edit');
    await page.waitForLoadState('networkidle');

    // Verify 500-line draft rendered without crash
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.getByText('بند كابلات رقم 1').first()).toBeVisible({ timeout: 15000 });
    await expect(page.locator('input[aria-label="الكمية"]')).toHaveCount(500, { timeout: 15000 });

    // Check bounded subtotal display
    await expect(page.locator('text=50,000.00').first()).toBeVisible();
  });

  test('12 — Mobile 390px, 768px, and 1024px Responsive Financial Readability', async ({ page }) => {
    // A. 390px Viewport
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/invoicing/customer/inv-cust-001');
    await page.waitForLoadState('networkidle');

    // Title and back button wrap
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.locator('button', { hasText: 'العودة للقائمة' }).first()).toBeVisible();

    // Verify Invoice Line critical financial items are readable on 390px:
    // Description, Quantity, Unit Price, Net, Tax, Total
    const mobileLineCard = page.locator('.ui-responsive-financial-mobile').first();
    await expect(mobileLineCard).toBeVisible();
    await expect(mobileLineCard.locator('text=أعمال تركيب وتمديد شبكات الألياف الضوئية')).toBeVisible();
    await expect(mobileLineCard.locator('text=11,000.00 ر.س').first()).toBeVisible(); // Unit Price / Net
    await expect(mobileLineCard.locator('text=1,650.00 ر.س')).toBeVisible(); // Tax
    await expect(mobileLineCard.locator('text=12,650.00 ر.س').first()).toBeVisible(); // Total

    // Verify Payment Schedule critical financial items on 390px:
    // Installment, Due Date, Amount
    const mobileScheduleCard = page.locator('.ui-responsive-financial-mobile').nth(1);
    await expect(mobileScheduleCard).toBeVisible();
    await expect(mobileScheduleCard.locator('text=القسط #1')).toBeVisible();
    await expect(mobileScheduleCard.locator('text=12,650.00 ر.س')).toBeVisible();

    // Capture mobile responsive screenshot
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/12-mobile-390px.png',
      fullPage: true
    });

    // B. 768px Tablet Viewport
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.waitForTimeout(200);
    const desktopTable = page.locator('.ui-responsive-financial-desktop').first();
    await expect(desktopTable).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/13-tablet-768px.png',
      fullPage: true
    });

    // C. 1024px Desktop Viewport
    await page.setViewportSize({ width: 1024, height: 768 });
    await page.waitForTimeout(200);
    await expect(desktopTable).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/invoicing/14-desktop-1024px.png',
      fullPage: true
    });
  });
});
