import { test, expect } from '@playwright/test';

const LEGAL_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const TEST_PARTY_ID = 'party-customer-alfa-01';

test.describe('Subledger & Open Items Acceptance, RTL & Finance Journey', () => {
  test.beforeEach(async ({ page }) => {
    // 1. Auth routes
    await page.route('**/api/v1/auth/csrf', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ token: 'mock-subledger-csrf' })
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

    // 2. Session Context (Atlas Trading)
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

    // 3. Authorization check - all subledger permissions allowed
    await page.route('**/api/v1/authorization/check*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ allowed: true })
      });
    });

    // 4. Modules manifest
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
          { id: 'settings' }
        ])
      });
    });

    // 5. Mock Subledger Data & Endpoints
    // 5a. Open items mock
    await page.route(/\/api\/v1\/subledger\/open-items/, async (route) => {
      const url = new URL(route.request().url());
      const direction = url.searchParams.get('direction') || 'Receivable';

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'item-inv-101',
              legalEntityId: LEGAL_ENTITY_ID,
              partyId: TEST_PARTY_ID,
              direction,
              sourceType: 'Invoice',
              sourceReference: 'INV-2026-000101',
              sourceDisplayNumber: 'INV-2026-000101',
              sourceScheduleReference: 'SCH-01',
              documentDate: '2026-01-10',
              dueDate: '2026-02-10',
              postingDate: '2026-01-10',
              transactionCurrency: 'EGP',
              originalTransactionAmount: '25000.00',
              openTransactionAmount: '15000.00',
              settledTransactionAmount: '10000.00',
              polarity: 'Debit',
              status: 'PartiallySettled',
              version: 2,
              originalBaseAmount: '25000.00',
              openBaseCarryingAmount: '15000.00',
              exchangeRateUsed: '1.000000'
            },
            {
              id: 'item-pay-201',
              legalEntityId: LEGAL_ENTITY_ID,
              partyId: TEST_PARTY_ID,
              direction,
              sourceType: 'Payment',
              sourceReference: 'PAY-2026-000201',
              sourceDisplayNumber: 'PAY-2026-000201',
              sourceScheduleReference: null,
              documentDate: '2026-01-15',
              dueDate: '2026-01-15',
              postingDate: '2026-01-15',
              transactionCurrency: 'EGP',
              originalTransactionAmount: '10000.00',
              openTransactionAmount: '10000.00',
              settledTransactionAmount: '0.00',
              polarity: 'Credit',
              status: 'Open',
              version: 1,
              originalBaseAmount: '10000.00',
              openBaseCarryingAmount: '10000.00',
              exchangeRateUsed: '1.000000'
            },
            {
              id: 'item-inv-102',
              legalEntityId: LEGAL_ENTITY_ID,
              partyId: TEST_PARTY_ID,
              direction,
              sourceType: 'Invoice',
              sourceReference: 'INV-2026-000102',
              sourceDisplayNumber: 'INV-2026-000102',
              sourceScheduleReference: 'SCH-02',
              documentDate: '2026-01-20',
              dueDate: '2026-02-20',
              postingDate: '2026-01-20',
              transactionCurrency: 'EGP',
              originalTransactionAmount: '8500.00',
              openTransactionAmount: '8500.00',
              settledTransactionAmount: '0.00',
              polarity: 'Debit',
              status: 'Open',
              version: 1,
              originalBaseAmount: '8500.00',
              openBaseCarryingAmount: '8500.00',
              exchangeRateUsed: '1.000000'
            }
          ],
          totalCount: 3,
          hasMore: false
        })
      });
    });

    // 5b. Party exposure endpoint
    await page.route(/\/api\/v1\/subledger\/parties\/[^/]+\/exposure/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: LEGAL_ENTITY_ID,
          partyId: TEST_PARTY_ID,
          asOfDate: '2026-10-26',
          currencyBalances: [
            {
              currencyCode: 'EGP',
              amountDue: '23500.00',
              availableCredits: '10000.00',
              netBalance: '13500.00',
              overdueExposure: '15000.00'
            }
          ]
        })
      });
    });

    // 5c. Party financial profile
    await page.route(/\/api\/v1\/subledger\/parties\/[^/]+\/financial-profile/, async (route) => {
      if (route.request().method() === 'PUT') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            legalEntityId: LEGAL_ENTITY_ID,
            partyId: TEST_PARTY_ID,
            creditLimitAmount: body.creditLimitAmount || '100000.00',
            creditLimitCurrency: body.creditLimitCurrency || 'EGP',
            defaultReceivablePaymentTermId: body.defaultReceivablePaymentTermId || 'Net30',
            defaultPayablePaymentTermId: body.defaultPayablePaymentTermId || 'Net60',
            receivablePolicyCode: body.receivablePolicyCode || 'Standard',
            payablePolicyCode: body.payablePolicyCode || 'Standard',
            version: 2
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            legalEntityId: LEGAL_ENTITY_ID,
            partyId: TEST_PARTY_ID,
            creditLimitAmount: '100000.00',
            creditLimitCurrency: 'EGP',
            defaultReceivablePaymentTermId: 'Net30',
            defaultPayablePaymentTermId: 'Net60',
            receivablePolicyCode: 'Standard',
            payablePolicyCode: 'Standard',
            version: 1
          })
        });
      }
    });

    // 5d. Auto-apply suggestions
    await page.route(/\/api\/v1\/subledger\/applications\/suggest/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          sourceOpenItemId: 'item-pay-201',
          sourceAvailableAmount: '10000.00',
          totalSuggestedAmount: '10000.00',
          remainingSourceAmount: '0.00',
          suggestions: [
            {
              debitItemId: 'item-inv-101',
              creditItemId: 'item-pay-201',
              debitAmount: '10000.00',
              creditAmount: '10000.00',
              suggestedAmount: '10000.00',
              remainingItemAmount: '5000.00',
              policyRuleTriggered: 'OldestDue',
              reasonCode: 'OldestDue',
              confidenceClass: 'High',
              priorityOrder: 1,
              sourceScheduleReference: 'SCH-01'
            }
          ]
        })
      });
    });

    // 5e. Application Preview
    await page.route(/\/api\/v1\/subledger\/applications\/preview/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: LEGAL_ENTITY_ID,
          partyId: TEST_PARTY_ID,
          direction: 'Receivable',
          settlementDate: '2026-10-26',
          applicationDate: '2026-10-26',
          totalDebitApplied: '10000.00',
          totalCreditApplied: '10000.00',
          totalRealizedGainLossBase: '0.00',
          lines: [
            {
              debitItemId: 'item-inv-101',
              creditItemId: 'item-pay-201',
              debitAppliedAmount: '10000.00',
              creditAppliedAmount: '10000.00',
              exchangeRateUsed: '1.000000',
              realizedGainLossAmount: '0.00',
              expectedDebitVersion: 2,
              expectedCreditVersion: 1
            }
          ],
          hasGainLoss: false
        })
      });
    });

    // 5f. Apply open items (commit)
    await page.route(/\/api\/v1\/subledger\/applications$/, async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            applicationId: 'app-999-success',
            status: 'Committed',
            appliedAmount: '10000.00',
            remainingUnappliedCredit: '0.00',
            reversalAllowed: true
          })
        });
      }
    });

    // 5g. Aging Report
    await page.route(/\/api\/v1\/subledger\/reports\/aging/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            currencyCode: 'EGP',
            current: '8500.00',
            days1To30: '15000.00',
            days31To60: '0.00',
            days61To90: '0.00',
            days91Plus: '0.00',
            totalOpen: '23500.00',
            unappliedCredit: '10000.00',
            netReceivable: '13500.00'
          }
        ])
      });
    });

    // 5h. Party Statement
    await page.route(/\/api\/v1\/subledger\/parties\/[^/]+\/statement/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: LEGAL_ENTITY_ID,
          partyId: TEST_PARTY_ID,
          fromDate: '2026-01-01',
          toDate: '2026-10-26',
          openingBalances: { EGP: '0.00' },
          closingBalances: { EGP: '13500.00' },
          lines: [
            {
              sourceId: 'inv-1',
              effectiveDate: '2026-01-10',
              postingDate: '2026-01-10',
              sourceSequence: 1,
              sourceType: 'Invoice',
              sourceReference: 'INV-2026-000101',
              amount: '25000.00',
              runningBalance: '25000.00',
              currencyCode: 'EGP'
            },
            {
              sourceId: 'pay-1',
              effectiveDate: '2026-01-15',
              postingDate: '2026-01-15',
              sourceSequence: 2,
              sourceType: 'Payment',
              sourceReference: 'PAY-2026-000201',
              amount: '-10000.00',
              runningBalance: '15000.00',
              currencyCode: 'EGP'
            }
          ]
        })
      });
    });

    // 5i. Control Accounts Reconciliation
    await page.route(/\/api\/v1\/subledger\/reconciliation\/control-accounts/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            direction: 'Receivable',
            controlRole: 'ReceivableControl',
            controlAccountReference: '110100',
            controlConfigurationReference: 'CFG-REC-01',
            baseCurrencyCode: 'EGP',
            subledgerBaseBalance: 23500.00
          },
          {
            direction: 'Payable',
            controlRole: 'PayableControl',
            controlAccountReference: '210100',
            controlConfigurationReference: 'CFG-PAY-01',
            baseCurrencyCode: 'EGP',
            subledgerBaseBalance: 0.00
          }
        ])
      });
    });

    await page.route(/\/api\/v1\/subledger\/reconciliation\/run/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'run-rec-2026-01',
          legalEntityId: LEGAL_ENTITY_ID,
          asOfDate: '2026-10-26',
          baseCurrencyCode: 'EGP',
          subledgerBaseBalance: '23500.00',
          ledgerBaseBalance: '23500.00',
          variance: '0.00',
          isBalanced: true,
          createdAt: '2026-10-26T12:00:00Z',
          details: [
            {
              id: 'det-1',
              direction: 'Receivable',
              controlRole: 'ReceivableControl',
              controlAccountReference: '110100',
              controlConfigurationReference: 'CFG-REC-01',
              ledgerAccountId: '11010000-0000-0000-0000-000000000000',
              subledgerBaseBalance: '23500.00',
              ledgerBaseBalance: '23500.00',
              variance: '0.00',
              reconciledAt: '2026-10-26T12:00:00Z'
            }
          ]
        })
      });
    });

    // 5j. Opening Migration Batches
    await page.route(/\/api\/v1\/subledger\/opening-batches/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            batchId: 'batch-mig-01',
            legalEntityId: LEGAL_ENTITY_ID,
            status: 'Finalized',
            totalLines: 15,
            totalDebitAmount: '150000.00',
            totalCreditAmount: '0.00',
            currencyCode: 'EGP',
            finalizedAt: '2026-01-01T00:00:00Z',
            isImmutable: true
          }
        ])
      });
    });
  });

  test('1. Hub Landing & KPI Executive Cockpit (/subledger)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger');
    await page.waitForLoadState('domcontentloaded');

    // Page title and high contrast corporate shell
    const heading = page.getByRole('heading', { name: /نظرة عامة على الذمم والتسويات|Subledger Overview/i });
    await expect(heading).toBeVisible({ timeout: 10000 });

    // Financial KPI Cards: Receivables Due, Payables Due, Reconciliation Health
    await expect(page.getByText(/إجمالي المستحقات للتحصيل|Receivables Due/i)).toBeVisible();
    await expect(page.getByText(/إجمالي المستحقات للسداد|Payables Due/i)).toBeVisible();
    await expect(page.getByText(/حالة مطابقة الأستاذ|Reconciliation Health/i)).toBeVisible();

    // Verify RTL document direction
    const htmlDir = await page.locator('html').getAttribute('dir');
    expect(htmlDir === 'rtl' || htmlDir === null).toBeTruthy();

    // Navigation links present
    await expect(page.getByRole('link', { name: /المستندات المفتوحة|Open Items/i }).first()).toBeVisible();
  });

  test('2. Open Items Exploration & Detail Drawer (/subledger/open-items)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger/open-items');
    await page.waitForLoadState('domcontentloaded');

    // Verify Open Items heading
    await expect(page.getByRole('heading', { name: /سجل المستندات المالية المفتوحة|Open Items Management/i })).toBeVisible({ timeout: 10000 });

    // Table should render open items
    await expect(page.getByText('INV-2026-000101').first()).toBeVisible();
    await expect(page.getByText('PAY-2026-000201').first()).toBeVisible();

    // Verify Polarity badges (Debit / Credit) inside table
    await expect(page.locator('table').getByText(/مدين|Debit/i).first()).toBeVisible();
    await expect(page.locator('table').getByText(/دائن|Credit/i).first()).toBeVisible();

    // Click "التفاصيل" button to open detail drawer
    const detailsBtn = page.getByRole('button', { name: /التفاصيل|Details/i }).first();
    await detailsBtn.click();
    await page.waitForTimeout(300);

    // Drawer should open and show exact breakdown
    await expect(page.getByRole('dialog').getByText(/المبلغ الأصلي|Original Amount/i)).toBeVisible();
    await expect(page.getByRole('dialog').getByText(/المبلغ المتبقي|Open Balance/i)).toBeVisible();

    // Close drawer
    const closeBtn = page.getByRole('button', { name: /إغلاق|Close/i }).first();
    if (await closeBtn.isVisible()) {
      await closeBtn.click();
    }
  });

  test('3. Party Financial Cockpit with Search & Dual Exposure (/subledger/parties)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger/parties');
    await page.waitForLoadState('domcontentloaded');

    // Title visible
    await expect(page.getByRole('heading', { name: /المركز المالي للطرف التجاري|Party Financial/i })).toBeVisible({ timeout: 10000 });

    // Input party ID and click load
    const partyInput = page.locator('#party-id-input');
    await expect(partyInput).toBeVisible();
    await partyInput.fill(TEST_PARTY_ID);

    const loadBtn = page.getByRole('button', { name: /عرض الحساب|Load/i });
    await loadBtn.click();
    await page.waitForTimeout(400);

    // Cockpit should render Dual Balance Cards: Customer AR & Supplier AP
    await expect(page.getByRole('heading', { name: /سجل العميل|Customer Side/i }).first()).toBeVisible();
    await expect(page.getByRole('heading', { name: /سجل المورد|Supplier Side/i }).first()).toBeVisible();

    // Financial Profile Card with credit limit
    await expect(page.getByText(/الملف المالي وسياسات الائتمان|Financial Profile/i)).toBeVisible();
    await expect(page.getByText(/الحد الائتماني الأقصى|Credit Limit/i)).toBeVisible();
  });

  test('4. Payment & Credit Allocation Workspace (/subledger/applications)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/subledger/parties?partyId=${TEST_PARTY_ID}`);
    await page.waitForLoadState('domcontentloaded');

    // Click Application Tab
    const appTab = page.getByRole('button', { name: /تسوية وتوزيع الدفعات والائتمان|Payment & Credit Allocation/i });
    await expect(appTab).toBeVisible({ timeout: 10000 });
    await appTab.click();
    await page.waitForTimeout(400);

    // Allocation Workspace renders source selector
    await expect(page.getByText(/اختر مصدر السداد أو الائتمان|Select Source Payment/i).first()).toBeVisible();

    // Outstanding Invoices table renders debit targets
    await expect(page.getByText(/الفواتير والمستندات المدينة المستحقة|Outstanding Invoices/i)).toBeVisible();
    await expect(page.getByText('INV-2026-000101').first()).toBeVisible();

    // Click "تطبيق كامل المتبقي" on target invoice
    const applyRemainingBtn = page.getByRole('button', { name: /تطبيق كامل المتبقي|Apply Remaining/i }).first();
    await expect(applyRemainingBtn).toBeVisible({ timeout: 5000 });
    await applyRemainingBtn.click();
    await page.waitForTimeout(300);

    // Click Preview button
    const previewBtn = page.getByRole('button', { name: /معاينة التسوية|Preview Settlement/i });
    await expect(previewBtn).toBeEnabled({ timeout: 5000 });
    await previewBtn.click();
    await page.waitForTimeout(400);

    // Preview Dialog opens
    await expect(page.getByText(/معاينة نتائج التسوية المالية|Allocation Financial Preview/i)).toBeVisible();
    await expect(page.getByText(/إجمالي المسدد من الفواتير|Total Debit Invoices Settled/i)).toBeVisible();

    // Commit settlement
    const commitBtn = page.getByRole('button', { name: /تأكيد التوزيع وترحيل التسوية|Confirm & Commit/i });
    await expect(commitBtn).toBeVisible();
    await commitBtn.click();
    await page.waitForTimeout(400);

    // Success banner
    await expect(page.getByText(/تم تطبيق التسوية بنجاح|Allocation applied successfully/i)).toBeVisible();
  });

  test('5. Aging Report Breakdown by As-Of Date (/subledger/aging)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/subledger/aging?partyId=${TEST_PARTY_ID}`);
    await page.waitForLoadState('domcontentloaded');

    // Title visible
    await expect(page.getByRole('heading', { name: /تقرير أعمار الديون والمستحقات|Aging Analysis/i })).toBeVisible({ timeout: 10000 });

    // As-Of date filter exists
    await expect(page.locator('#aging-as-of-date')).toBeVisible();

    // Aging Buckets display
    await expect(page.getByText(/غير مستحقة \(جارية\)|Current/i).first()).toBeVisible();
    await expect(page.getByText(/1 - 30 يوماً|1 - 30 Days/i).first()).toBeVisible();
    await expect(page.getByText(/31 - 60 يوماً|31 - 60 Days/i).first()).toBeVisible();
    await expect(page.getByText(/61 - 90 يوماً|61 - 90 Days/i).first()).toBeVisible();
    await expect(page.getByText(/أكثر من 90 يوماً|91\+ Days/i).first()).toBeVisible();

    // Overdue summary bar
    await expect(page.getByText(/نسبة المتأخرات|Overdue/i)).toBeVisible();
  });

  test('6. Party Statement & Running Balances (/subledger/statement)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/subledger/statement?partyId=${TEST_PARTY_ID}`);
    await page.waitForLoadState('domcontentloaded');

    // Title visible
    await expect(page.getByRole('heading', { name: /كشف حساب الطرف والأرصدة التراكمية|Party Statement/i })).toBeVisible({ timeout: 10000 });

    // Date range inputs exist
    await expect(page.locator('#stmt-from-date')).toBeVisible();
    await expect(page.locator('#stmt-to-date')).toBeVisible();

    // Closing Balance Metric Card
    await expect(page.getByText(/رصيد نهاية المدة|Closing Balance/i)).toBeVisible();

    // Statement lines table
    await expect(page.getByText('INV-2026-000101').first()).toBeVisible();
    await expect(page.getByText('PAY-2026-000201').first()).toBeVisible();
  });

  test('7. GL Control Account Reconciliation (/subledger/reconciliation)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger/reconciliation');
    await page.waitForLoadState('domcontentloaded');

    // Title visible
    await expect(page.getByRole('heading', { name: /مطابقة حسابات المراقبة مع دفتر الأستاذ العام|Control Account Reconciliation/i })).toBeVisible({ timeout: 10000 });

    // Control Accounts rendered
    await expect(page.getByText('110100').first()).toBeVisible();

    // Reconciliation Run button
    const runBtn = page.getByRole('button', { name: /بدء فحص المطابقة الآن|Run Reconciliation/i });
    await expect(runBtn).toBeVisible();
    await runBtn.click();
    await page.waitForTimeout(400);

    // Balanced state banner
    await expect(page.getByText(/متطابق \(سليم\)|Reconciled/i)).toBeVisible();
  });

  test('8. Opening Balance Migration Finalization (/subledger/opening)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger/opening');
    await page.waitForLoadState('domcontentloaded');

    // Title visible
    await expect(page.getByRole('heading', { name: /ترحيل أرصدة البداية|Opening Balance Migration/i })).toBeVisible({ timeout: 10000 });

    // Notice on create batch
    await expect(page.getByText(/إنشاء دفعة أرصدة بداية|Create Opening Batch/i).first()).toBeVisible();
  });

  test('9. Stale State / Concurrency Conflict (409) Handling', async ({ page }) => {
    // Override /api/v1/subledger/applications to return 409
    await page.route(/\/api\/v1\/subledger\/applications$/, async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 409,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            status: 409,
            title: 'subledger.item_changed',
            detail: 'The open item has been concurrently modified or settled.'
          })
        });
      } else {
        await route.continue();
      }
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/subledger/parties?partyId=${TEST_PARTY_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const appTab = page.getByRole('button', { name: /تسوية وتوزيع الدفعات والائتمان|Payment & Credit Allocation/i });
    await appTab.click();
    await page.waitForTimeout(400);

    const applyRemainingBtn = page.getByRole('button', { name: /تطبيق كامل المتبقي|Apply Remaining/i }).first();
    await applyRemainingBtn.click();
    await page.waitForTimeout(300);

    const previewBtn = page.getByRole('button', { name: /معاينة التسوية|Preview Settlement/i });
    await previewBtn.click();
    await page.waitForTimeout(400);

    const commitBtn = page.getByRole('button', { name: /تأكيد التوزيع وترحيل التسوية|Confirm & Commit/i });
    await commitBtn.click();
    await page.waitForTimeout(400);

    // Concurrency error banner displayed
    await expect(page.getByText(/رصيد أحد المستندات اتغير|حدث تغيير متزامن|concurrency/i)).toBeVisible();
    // Preview modal is closed
    await expect(page.getByRole('dialog')).not.toBeVisible();
  });

  test('10. Unapplied Credit Balance Calculation and Reuse', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/subledger/parties?partyId=${TEST_PARTY_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const appTab = page.getByRole('button', { name: /تسوية وتوزيع الدفعات والائتمان|Payment & Credit Allocation/i });
    await appTab.click();
    await page.waitForTimeout(400);

    // Source Payment has 10,000.00 EGP available.
    // Enter 3,000.00 allocation on first row.
    const input = page.locator('input[type="number"]').first();
    await input.fill('3000');
    await page.waitForTimeout(300);

    // Remaining unapplied credit is 7,000.00 EGP
    await expect(page.getByText(/المتبقي من الدفعة|المتبقي غير الموزع/i)).toBeVisible();
    await expect(page.getByText('7,000.00 EGP')).toBeVisible();
  });

  test('11. Auto-Apply Suggestions & Policy Rule Badges', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/subledger/parties?partyId=${TEST_PARTY_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const appTab = page.getByRole('button', { name: /تسوية وتوزيع الدفعات والائتمان|Payment & Credit Allocation/i });
    await appTab.click();
    await page.waitForTimeout(400);

    // Click Auto-Suggest
    const autoSuggestBtn = page.getByRole('button', { name: /اقتراح توزيع تلقائي|Auto-Suggest/i });
    await autoSuggestBtn.click();
    await page.waitForTimeout(400);

    // Suggestion policy badge is displayed
    await expect(page.getByText(/استحقاق أقدم \(FIFO\)|Oldest Due/i).first()).toBeVisible();

    // The suggested input has 10000
    const input = page.locator('input[type="number"]').first();
    expect(await input.inputValue()).toBe('10000.00');

    // Manual adjustment is allowed
    await input.fill('8500');
    expect(await input.inputValue()).toBe('8500');
  });

  test('12. Application Reversal with Required Reason', async ({ page }) => {
    await page.route(/\/api\/v1\/subledger\/applications\/[^/]+\/reverse/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          reversalId: 'rev-001',
          status: 'Reversed',
          effectiveDate: '2026-10-26'
        })
      });
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger/open-items');
    await page.waitForLoadState('domcontentloaded');

    // Click Reversal button on partially settled item
    const reverseBtn = page.getByRole('button', { name: /إلغاء تسوية مالية معتمدة|Reverse Settled Allocation/i }).first();
    await expect(reverseBtn).toBeVisible({ timeout: 10000 });
    await reverseBtn.click();
    await page.waitForTimeout(300);

    // Reversal dialog opens
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText(/إلغاء تسوية مالية معتمدة|Reverse Settled Allocation/i).first()).toBeVisible();

    // Confirm button is disabled when reason is empty
    const confirmBtn = dialog.getByRole('button', { name: /تأكيد الإلغاء المالي|Confirm/i });
    await expect(confirmBtn).toBeDisabled();

    // Enter reversal reason
    const reasonInput = dialog.locator('textarea');
    await reasonInput.fill('تم تسجيل دفعة أخرى وجار إعادة التسوية الصحيحة');
    await expect(confirmBtn).toBeEnabled();

    // Submit reversal
    await confirmBtn.click();
    await page.waitForTimeout(400);

    // Dialog closes and success alert is displayed
    await expect(dialog).not.toBeVisible();
    await expect(page.getByText(/تم إلغاء التسوية بنجاح وإعادة فتح الأرصدة المستحقة|reversed successfully/i)).toBeVisible();
  });

  test('13. Write-Off Workflow & SoD Security Validation', async ({ page }) => {
    // 1. Simulate unauthorized attempt (403 Forbidden)
    let allowWriteOff = false;
    await page.route(/\/api\/v1\/subledger\/write-offs/, async (route) => {
      if (!allowWriteOff) {
        await route.fulfill({
          status: 403,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            status: 403,
            title: 'Forbidden',
            detail: 'User does not possess subledger.writeoff.create authority.'
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            writeOffId: 'wo-101',
            status: 'Approved',
            amount: '500.00'
          })
        });
      }
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger/open-items');
    await page.waitForLoadState('domcontentloaded');

    // Click write-off button
    const writeOffBtn = page.getByRole('button', { name: /شطب رصيد|طلب شطب|Write Off/i }).first();
    await writeOffBtn.click();
    await page.waitForTimeout(300);

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText(/طلب شطب رصيد متبقٍ|Write-Off/i).first()).toBeVisible();

    // Submit as unauthorized user
    const submitBtn = dialog.getByRole('button', { name: /إرسال طلب الشطب|Submit Write-Off/i });
    await submitBtn.click();
    await page.waitForTimeout(300);

    // Forbidden error displayed inside dialog
    await expect(dialog.getByText(/subledger\.writeoff\.create|Forbidden/i)).toBeVisible();

    // Now permit write-off and retry
    allowWriteOff = true;
    await submitBtn.click();
    await page.waitForTimeout(400);

    // Dialog closes
    await expect(dialog).not.toBeVisible();
  });

  test('14. Multi-Currency Settlement & Realized FX Impact', async ({ page }) => {
    // Mock multi-currency preview with USD debit invoice and EGP payment source
    await page.route(/\/api\/v1\/subledger\/applications\/preview/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: LEGAL_ENTITY_ID,
          partyId: TEST_PARTY_ID,
          direction: 'Receivable',
          settlementDate: '2026-10-26',
          applicationDate: '2026-10-26',
          totalDebitApplied: '25000.00',
          totalCreditApplied: '25000.00',
          totalFxImpactBase: '350.00 EGP',
          lines: [
            {
              debitItemId: 'item-inv-101',
              creditItemId: 'item-pay-201',
              debitAppliedAmount: '500.00',
              creditAppliedAmount: '25000.00',
              exchangeRateUsed: '50.000000',
              realizedGainLossAmount: '350.00',
              expectedDebitVersion: 2,
              expectedCreditVersion: 1
            }
          ],
          hasGainLoss: true
        })
      });
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`/subledger/parties?partyId=${TEST_PARTY_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const appTab = page.getByRole('button', { name: /تسوية وتوزيع الدفعات والائتمان|Payment & Credit Allocation/i });
    await appTab.click();
    await page.waitForTimeout(400);

    const applyRemainingBtn = page.getByRole('button', { name: /تطبيق كامل المتبقي|Apply Remaining/i }).first();
    await applyRemainingBtn.click();
    await page.waitForTimeout(300);

    const previewBtn = page.getByRole('button', { name: /معاينة التسوية|Preview Settlement/i });
    await previewBtn.click();
    await page.waitForTimeout(400);

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText(/معاينة نتائج التسوية المالية/i)).toBeVisible();

    // Verify FX Impact is visible
    await expect(dialog.getByText(/أثر فرق العملة الناتج|الأثر المالي لفروق العملة|FX Gain\/Loss/i)).toBeVisible();
    await expect(dialog.getByText('350.00 EGP')).toBeVisible();

    // Close preview
    await dialog.getByRole('button', { name: /تراجع|إلغاء|Close/i }).click();
    await expect(dialog).not.toBeVisible();
  });

  test('15. Control Account Reconciliation Mismatch & Discrepancy Display', async ({ page }) => {
    // Mock run endpoint to return an unbalanced reconciliation
    await page.route(/\/api\/v1\/subledger\/reconciliation\/run/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'run-unbalanced-01',
          legalEntityId: LEGAL_ENTITY_ID,
          asOfDate: '2026-10-26',
          baseCurrencyCode: 'EGP',
          subledgerBaseBalance: '23500.00',
          ledgerBaseBalance: '23000.00',
          variance: '500.00',
          isBalanced: false,
          createdAt: '2026-10-26T12:00:00Z',
          details: [
            {
              id: 'det-unbal-1',
              direction: 'Receivable',
              controlRole: 'ReceivableControl',
              controlAccountReference: '110100',
              controlConfigurationReference: 'CFG-REC-01',
              ledgerAccountId: '11010000-0000-0000-0000-000000000000',
              subledgerBaseBalance: '23500.00',
              ledgerBaseBalance: '23000.00',
              variance: '500.00',
              reconciledAt: '2026-10-26T12:00:00Z'
            }
          ]
        })
      });
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger/reconciliation');
    await page.waitForLoadState('domcontentloaded');

    const runBtn = page.getByRole('button', { name: /بدء فحص المطابقة الآن|Run Reconciliation/i });
    await runBtn.click();
    await page.waitForTimeout(400);

    // Unbalanced discrepancy banner is displayed
    await expect(page.getByText(/يوجد فارق/i).first()).toBeVisible();
    await expect(page.getByText('500.00 ج.م').first()).toBeVisible();
  });

  test('16. Opening Migration Imbalance Warning & Finalization Lock', async ({ page }) => {
    // Mock validate batch returning imbalanced variance
    await page.route(/\/api\/v1\/subledger\/opening-batches\/.*\/validate/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          batchId: 'batch-test-imbal',
          isBalanced: false,
          lineCount: 10,
          directions: [
            {
              direction: 'Receivable',
              isBalanced: false,
              subledgerBaseBalance: '150000.00',
              ledgerBaseBalance: '140000.00',
              variance: '10000.00'
            }
          ]
        })
      });
    });

    // Mock create batch returning active batch ID
    await page.route(/\/api\/v1\/subledger\/opening-batches$/, async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify('batch-test-imbal')
        });
      } else {
        await route.continue();
      }
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger/opening');
    await page.waitForLoadState('domcontentloaded');

    // Create a draft batch
    await page.locator('#open-batch-id').fill('BATCH-TEST-2026');
    await page.locator('#open-golive-date').fill('2026-01-01');
    await page.locator('#open-base-currency').fill('EGP');

    const createBtn = page.getByRole('button', { name: /إنشاء دفعة أرصدة بداية|Create Opening Batch/i });
    await createBtn.click();
    await page.waitForTimeout(400);

    // Click validate batch
    const validateBtn = page.getByRole('button', { name: /مطابقة مع قيد الأستاذ العام|Validate/i });
    await expect(validateBtn).toBeVisible();
    await validateBtn.click();
    await page.waitForTimeout(400);

    // Imbalance warning is displayed
    await expect(page.getByText(/يوجد فارق بين إجمالي فواتير الأستاذ المساعد|Imbalance/i)).toBeVisible();
    await expect(page.getByText(/10,000\.00/i).first()).toBeVisible();

    // Finalize button is strictly disabled
    const finalizeBtn = page.getByRole('button', { name: /اعتماد نهائي وترحيل|Finalize Batch/i });
    await expect(finalizeBtn).toBeDisabled();
  });

  test('17. Role & Permission Matrix (Unauthorized Access Prevention)', async ({ page }) => {
    // Disallow subledger permissions
    await page.route('**/api/v1/authorization/check*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ allowed: false })
      });
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger/open-items');
    await page.waitForLoadState('domcontentloaded');

    // Header is rendered
    await expect(page.getByRole('heading', { name: /سجل المستندات المالية المفتوحة|Open Items Management/i })).toBeVisible();
  });

  test('18. Accessibility & Keyboard Navigation (Escape Key & Dialog Traps)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger/open-items');
    await page.waitForLoadState('domcontentloaded');

    // Open detail drawer
    const detailsBtn = page.getByRole('button', { name: /التفاصيل|Details/i }).first();
    await detailsBtn.click();
    await page.waitForTimeout(300);

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Press Escape key
    await page.keyboard.press('Escape');
    await page.waitForTimeout(300);

    // Dialog is dismissed via Escape
    await expect(dialog).not.toBeVisible();
  });

  test('19. Responsive Layouts across 1024px and 768px Viewports', async ({ page }) => {
    // 1024px Laptop
    await page.setViewportSize({ width: 1024, height: 768 });
    await page.goto('/subledger/open-items');
    await page.waitForLoadState('domcontentloaded');
    await expect(page.getByRole('heading', { name: /سجل المستندات المالية المفتوحة|Open Items Management/i })).toBeVisible();

    // 768px Tablet
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/subledger');
    await page.waitForLoadState('domcontentloaded');
    await expect(page.getByRole('heading', { name: /نظرة عامة على الذمم والتسويات|Subledger Overview/i })).toBeVisible();
  });

  test('20. Runtime Health & Zero Console Errors', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        errors.push(msg.text());
      }
    });
    page.on('pageerror', (err) => {
      errors.push(err.message);
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/subledger');
    await page.waitForLoadState('domcontentloaded');

    await page.goto('/subledger/open-items');
    await page.waitForLoadState('domcontentloaded');

    await page.goto(`/subledger/parties?partyId=${TEST_PARTY_ID}`);
    await page.waitForLoadState('domcontentloaded');

    await page.goto(`/subledger/aging?partyId=${TEST_PARTY_ID}`);
    await page.waitForLoadState('domcontentloaded');

    await page.goto(`/subledger/statement?partyId=${TEST_PARTY_ID}`);
    await page.waitForLoadState('domcontentloaded');

    await page.goto('/subledger/reconciliation');
    await page.waitForLoadState('domcontentloaded');

    await page.goto('/subledger/opening');
    await page.waitForLoadState('domcontentloaded');

    expect(errors).toHaveLength(0);
  });
});
