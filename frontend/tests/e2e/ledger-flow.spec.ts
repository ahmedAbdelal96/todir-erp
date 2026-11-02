import { test, expect } from '@playwright/test';

const MAIN_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const SECONDARY_ENTITY_ID = '00000000-0000-0000-0000-000000000002';

const MOCK_ACCOUNTS = [
  {
    id: '00000000-0000-0000-0000-000000000100',
    legalEntityId: MAIN_ENTITY_ID,
    code: '110000',
    nameAr: 'الأصول المتداولة',
    nameEn: 'Current Assets',
    type: 'Asset',
    subtype: 'Group',
    normalBalance: 'Debit',
    parentAccountId: null,
    allowDirectPosting: false,
    status: 'Active',
    version: 1
  },
  {
    id: '00000000-0000-0000-0000-000000000101',
    legalEntityId: MAIN_ENTITY_ID,
    code: '110100',
    nameAr: 'حسابات العملاء',
    nameEn: 'Accounts Receivable',
    type: 'Asset',
    subtype: 'Receivable',
    normalBalance: 'Debit',
    parentAccountId: '00000000-0000-0000-0000-000000000100',
    allowDirectPosting: true,
    status: 'Active',
    version: 1
  },
  {
    id: '00000000-0000-0000-0000-000000000102',
    legalEntityId: MAIN_ENTITY_ID,
    code: '110200',
    nameAr: 'النقدية والبنوك',
    nameEn: 'Cash and Banks',
    type: 'Asset',
    subtype: 'Cash',
    normalBalance: 'Debit',
    parentAccountId: '00000000-0000-0000-0000-000000000100',
    allowDirectPosting: true,
    status: 'Active',
    version: 1
  },
  {
    id: '00000000-0000-0000-0000-000000000103',
    legalEntityId: MAIN_ENTITY_ID,
    code: '110300',
    nameAr: 'أوراق القبض الاستثمارية',
    nameEn: 'Investment Notes Receivable',
    type: 'Asset',
    subtype: 'Cash',
    normalBalance: 'Debit',
    parentAccountId: '00000000-0000-0000-0000-000000000100',
    allowDirectPosting: true,
    status: 'Active',
    version: 1
  },
  {
    id: '00000000-0000-0000-0000-000000000400',
    legalEntityId: MAIN_ENTITY_ID,
    code: '410000',
    nameAr: 'إيرادات المبيعات',
    nameEn: 'Sales Revenue',
    type: 'Revenue',
    subtype: 'Sales',
    normalBalance: 'Credit',
    parentAccountId: null,
    allowDirectPosting: true,
    status: 'Active',
    version: 1
  }
];

const MOCK_JOURNALS = [
  {
    id: '00000000-0000-0000-0000-000000000201',
    legalEntityId: MAIN_ENTITY_ID,
    code: 'GENERAL',
    nameAr: 'دفتر القيود العامة',
    nameEn: 'General Journal',
    type: 'General',
    status: 'Active',
    version: 1
  },
  {
    id: '00000000-0000-0000-0000-000000000202',
    legalEntityId: MAIN_ENTITY_ID,
    code: 'SALES',
    nameAr: 'دفتر المبيعات',
    nameEn: 'Sales Journal',
    type: 'Sales',
    status: 'Active',
    version: 1
  }
];

const MOCK_FISCAL_YEARS = [
  {
    id: '00000000-0000-0000-0000-000000000301',
    legalEntityId: MAIN_ENTITY_ID,
    code: 'FY2026',
    startDate: '2026-01-01',
    endDate: '2026-12-31',
    status: 'Open',
    version: 1
  }
];

const MOCK_PERIODS = [
  {
    id: '00000000-0000-0000-0000-000000000310',
    fiscalYearId: '00000000-0000-0000-0000-000000000301',
    code: '01',
    startDate: '2026-01-01',
    endDate: '2026-01-31',
    sequence: 1,
    status: 'Open',
    version: 1
  },
  {
    id: '00000000-0000-0000-0000-000000000311',
    fiscalYearId: '00000000-0000-0000-0000-000000000301',
    code: '02',
    startDate: '2026-02-01',
    endDate: '2026-02-28',
    sequence: 2,
    status: 'Open',
    version: 1
  }
];

const MOCK_ENTRIES = [
  {
    id: '00000000-0000-0000-0000-000000000501',
    journalId: '00000000-0000-0000-0000-000000000201',
    factId: '00000000-0000-0000-0000-000000000601',
    entryNumber: 'JV-2026-0001',
    postingDate: '2026-01-15',
    periodDate: '2026-01-15',
    status: 'Posted',
    currencyCode: 'EGP',
    lines: [
      {
        sequence: 1,
        accountId: '00000000-0000-0000-0000-000000000102',
        debit: 25000,
        credit: 0,
        currencyCode: 'EGP',
        amount: 25000,
        role: 'Cash',
        description: 'تحصيل إيرادات مبيعات نقدية'
      },
      {
        sequence: 2,
        accountId: '00000000-0000-0000-0000-000000000400',
        debit: 0,
        credit: 25000,
        currencyCode: 'EGP',
        amount: 25000,
        role: 'SalesRevenue',
        description: 'إيراد مبيعات تجارية'
      }
    ]
  }
];

test.describe('Ledger & Accounting UX Acceptance & Visual Suite', () => {
  test.beforeEach(async ({ page }) => {
    // Auth routes
    await page.route('**/api/v1/auth/csrf', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ token: 'mock-csrf' }) });
    });
    await page.route('**/api/v1/auth/login', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ state: 'Authenticated', authenticated: true, tenantId: '00000000-0000-0000-0000-000000000000' })
      });
    });
    await page.route('**/api/v1/context/organization', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true }) });
    });

    // Session Context
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: '00000000-0000-0000-0000-000000000000',
          memberships: [{ id: 'atlas', tenantId: '00000000-0000-0000-0000-000000000000', displayName: 'شركة أطلس للتجارة' }],
          context: {
            displayName: 'شركة أطلس للتجارة',
            legalEntities: [
              {
                id: MAIN_ENTITY_ID,
                legalEntityId: MAIN_ENTITY_ID,
                code: 'MAIN',
                name: 'شركة أطلس للتجارة — الكيان الرئيسي',
                countryCode: 'EG',
                baseCurrencyCode: 'EGP'
              }
            ],
            branches: []
          },
          permissions: [
            'ledger.account.read', 'ledger.account.manage',
            'ledger.journal.read', 'ledger.journal.manage',
            'ledger.period.read', 'ledger.period.manage', 'ledger.period.soft_close', 'ledger.period.close', 'ledger.period.reopen',
            'ledger.lock.read', 'ledger.lock.manage',
            'ledger.entry.read', 'ledger.entry.reverse', 'ledger.entry.explain',
            'ledger.general_journal.create', 'ledger.general_journal.edit', 'ledger.general_journal.submit', 'ledger.general_journal.approve', 'ledger.general_journal.post',
            'ledger.dimension.read', 'ledger.dimension.manage',
            'ledger.opening.read', 'ledger.opening.manage', 'ledger.opening.post',
            'ledger.reports.read', 'ledger.configuration.read', 'ledger.configuration.manage'
          ]
        })
      });
    });

    // Modules
    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ id: 'home' }, { id: 'accounting' }, { id: 'settings' }])
      });
    });

    // Ledger Endpoints
    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/readiness`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: MAIN_ENTITY_ID,
          mode: 'Managed',
          state: 'Ready',
          capabilities: [
            { capability: 'invoicing', state: 'Ready', reasons: [] },
            { capability: 'payments', state: 'Ready', reasons: [] },
            { capability: 'inventory', state: 'Ready', reasons: [] },
            { capability: 'payroll', state: 'Ready', reasons: [] },
            { capability: 'general_journal', state: 'Ready', reasons: [] },
            { capability: 'period_close', state: 'Ready', reasons: [] }
          ],
          version: 1
        })
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/mode`), async (route) => {
      if (route.request().method() === 'PUT') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ legalEntityId: MAIN_ENTITY_ID, mode: body.mode || 'Full', version: 2 })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ legalEntityId: MAIN_ENTITY_ID, mode: 'Full', version: 1 })
        });
      }
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/accounts$`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_ACCOUNTS)
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/accounts/search`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: MOCK_ACCOUNTS, nextCursor: null })
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/accounts/.*/workspace`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          account: MOCK_ACCOUNTS[1],
          recentActivity: [
            {
              entryId: MOCK_ENTRIES[0].id,
              lineId: '00000000-0000-0000-0000-000000000701',
              entryNumber: MOCK_ENTRIES[0].entryNumber,
              postingDate: MOCK_ENTRIES[0].postingDate,
              accountId: MOCK_ACCOUNTS[1].id,
              accountCode: MOCK_ACCOUNTS[1].code,
              accountName: MOCK_ACCOUNTS[1].nameAr,
              debit: 25000,
              credit: 0,
              baseDebit: 25000,
              baseCredit: 0,
              currencyCode: 'EGP',
              sourceModule: 'sales',
              sourceReference: 'INV-2026-001',
              sequence: 1
            }
          ],
          balance: {
            accountId: MOCK_ACCOUNTS[1].id,
            debit: 25000,
            credit: 0,
            balance: 25000,
            currencyCode: 'EGP'
          },
          dimensions: [],
          configuration: [],
          audit: []
        })
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/journals`), async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_JOURNALS) });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/fiscal-years`), async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_FISCAL_YEARS) });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/periods$`), async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_PERIODS) });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/periods/.*/close-readiness`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          periodId: MOCK_PERIODS[0].id,
          ready: true,
          checks: [{ code: 'CHK1', message: 'All transactions for period 01 are posted' }],
          warnings: [],
          blockers: []
        })
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/locks`), async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/entries(\\?.*)?$`), async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_ENTRIES) });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/entries/page(\\?.*)?$`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: MOCK_ENTRIES, nextCursor: null })
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/entries/.*/explanation`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          entryId: MOCK_ENTRIES[0].id,
          entryNumber: MOCK_ENTRIES[0].entryNumber,
          journalId: MOCK_ENTRIES[0].journalId,
          postingDate: MOCK_ENTRIES[0].postingDate,
          accountingPeriod: '01',
          factType: 'CustomerInvoicePosted',
          factVersion: 1,
          postingRuleVersionId: '00000000-0000-0000-0000-000000000801',
          accountingConfigurationVersionId: '00000000-0000-0000-0000-000000000802',
          baseCurrencyCode: 'EGP',
          sourceModule: 'sales',
          sourceReference: 'INV-2026-001',
          lines: [
            {
              lineId: '00000000-0000-0000-0000-000000000701',
              sequence: 1,
              accountRole: 'Cash',
              accountId: MOCK_ACCOUNTS[2].id,
              accountCode: MOCK_ACCOUNTS[2].code,
              accountName: MOCK_ACCOUNTS[2].nameAr,
              debit: 25000,
              credit: 0,
              currencyCode: 'EGP',
              baseDebit: 25000,
              baseCredit: 0,
              whyThisAccount: 'Rule CUSTOMER_INVOICE_V1 mapped Cash role to account 110200',
              dimensions: []
            },
            {
              lineId: '00000000-0000-0000-0000-000000000702',
              sequence: 2,
              accountRole: 'SalesRevenue',
              accountId: MOCK_ACCOUNTS[4].id,
              accountCode: MOCK_ACCOUNTS[4].code,
              accountName: MOCK_ACCOUNTS[4].nameAr,
              debit: 0,
              credit: 25000,
              currencyCode: 'EGP',
              baseDebit: 0,
              baseCredit: 25000,
              whyThisAccount: 'Rule CUSTOMER_INVOICE_V1 mapped SalesRevenue role to account 410000',
              dimensions: []
            }
          ]
        })
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/reports/trial-balance`), async (route) => {
      const rows = [
        {
          accountId: MOCK_ACCOUNTS[2].id,
          accountCode: MOCK_ACCOUNTS[2].code,
          accountName: MOCK_ACCOUNTS[2].nameAr,
          debit: 25000,
          credit: 0,
          balance: 25000,
          currencyCode: 'EGP'
        },
        {
          accountId: MOCK_ACCOUNTS[4].id,
          accountCode: MOCK_ACCOUNTS[4].code,
          accountName: MOCK_ACCOUNTS[4].nameAr,
          debit: 0,
          credit: 25000,
          balance: 25000,
          currencyCode: 'EGP'
        }
      ];
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(route.request().url().includes('/page')
          ? { items: rows, nextCursor: null, totalCount: rows.length, totalDebit: 25000, totalCredit: 25000 }
          : rows)
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/reports/general-ledger`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              entryId: MOCK_ENTRIES[0].id,
              lineId: '00000000-0000-0000-0000-000000000701',
              entryNumber: MOCK_ENTRIES[0].entryNumber,
              postingDate: MOCK_ENTRIES[0].postingDate,
              accountId: MOCK_ACCOUNTS[2].id,
              accountCode: MOCK_ACCOUNTS[2].code,
              accountName: MOCK_ACCOUNTS[2].nameAr,
              debit: 25000,
              credit: 0,
              baseDebit: 25000,
              baseCredit: 0,
              currencyCode: 'EGP',
              sourceModule: 'sales',
              sourceReference: 'INV-2026-001',
              sequence: 1
            }
          ],
          nextCursor: null
        })
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/reports/reconcile-balances`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ healthy: true, differences: [] })
      });
    });

    // General Journal Draft and Post Endpoints
    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/general-journals$`), async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        const hasControlAccount = body.lines?.some((l: any) => l.accountId === '00000000-0000-0000-0000-000000000101');
        if (hasControlAccount) {
          await route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({
              code: 'ledger.control_account_manual_post_blocked',
              message: 'Manual posting to control account is blocked'
            })
          });
          return;
        }
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ id: '00000000-0000-0000-0000-000000000999' })
        });
      }
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/general-journals/00000000-0000-0000-0000-000000000999$`), async (route) => {
      if (route.request().method() === 'PUT') {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true }) });
      }
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/general-journals/00000000-0000-0000-0000-000000000999/submit`), async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ status: 'Submitted' }) });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/general-journals/00000000-0000-0000-0000-000000000999/approve`), async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ status: 'Approved' }) });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/general-journals/00000000-0000-0000-0000-000000000999/post`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          entryId: '00000000-0000-0000-0000-000000000502',
          entryNumber: 'JV-2026-0002'
        })
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/posting-preview`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          postingDate: '2026-10-24',
          totalDebit: 25000,
          totalCredit: 25000,
          baseCurrencyCode: 'EGP',
          lines: []
        })
      });
    });

    // Reversal Endpoints
    let isReversed = false;
    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/entries/.*/reverse`), async (route) => {
      if (isReversed) {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            code: 'ledger.already_reversed',
            message: 'Entry has already been reversed'
          })
        });
        return;
      }
      isReversed = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          entryId: '00000000-0000-0000-0000-000000000503',
          entryNumber: 'JV-2026-0003'
        })
      });
    });

    // Opening Balances Endpoints
    let openingPosted = false;
    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/opening-balances$`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify('00000000-0000-0000-0000-000000000888')
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/opening-balances/.*/preview`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          batchId: '00000000-0000-0000-0000-000000000888',
          totalDebit: 50000,
          totalCredit: 50000,
          balanced: true,
          validationErrors: [],
          lines: []
        })
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/opening-balances/.*/post`), async (route) => {
      if (openingPosted) {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            code: 'ledger.opening_already_posted',
            message: 'Opening balances have already been posted'
          })
        });
        return;
      }
      openingPosted = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          entryId: '00000000-0000-0000-0000-000000000889',
          entryNumber: 'OB-2026-0001'
        })
      });
    });
  });

  test('Journey 1: Accounting Overview & Readiness State', async ({ page }) => {
    await page.goto('/accounting');
    await expect(page.getByText('المحاسبة العامة ودفتر الأستاذ')).toBeVisible();
    await expect(page.getByText('حالة الجاهزية المحاسبية')).toBeVisible();
    await expect(page.getByText('جاهزية التوجيه المحاسبي مكتملة').first()).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-overview-rtl.png',
      fullPage: true
    });
  });

  test('Journey 2: Chart of Accounts Search and Workspace Modal', async ({ page }) => {
    await page.goto('/accounting');
    await page.getByRole('tab', { name: 'دليل الحسابات' }).evaluate((tab: HTMLElement) => tab.click());

    await expect(page.getByText('110000')).toBeVisible();
    await expect(page.getByText('حسابات العملاء', { exact: true })).toBeVisible();

    const detailsButton = page.getByRole('button', { name: 'تفاصيل وسجل الحساب' }).first();
    await detailsButton.evaluate((btn: HTMLElement) => btn.click());
    await expect(page.getByRole('heading', { name: /110100/ })).toBeVisible();
    await expect(page.getByRole('dialog').getByText('حسابات مدينة / حسابات العملاء')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-account-workspace-rtl.png'
    });
  });

  test('Journey 3: Fiscal Periods and Close Readiness Dialog', async ({ page }) => {
    await page.goto('/accounting');
    await page.getByRole('tab', { name: 'السنوات والفترات المالية' }).evaluate((tab: HTMLElement) => tab.click());

    await expect(page.getByText('السنوات المالية والفترات المحاسبية')).toBeVisible();
    await expect(page.getByText('مفتوحة للترحيل').first()).toBeVisible();

    await page.getByRole('button', { name: 'فحص جاهزية الإقفال' }).first().evaluate((btn: HTMLElement) => btn.click());
    await expect(page.getByText('فحص جاهزية إقفال الفترة')).toBeVisible();
    await expect(page.getByText('الفترة جاهزة للإقفال النهائي')).toBeVisible();
    await expect(page.getByText(/جميع المعاملات.*مرحلة بالكامل/)).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-period-readiness-rtl.png'
    });
  });

  test('Journey 4: Journal Entries Explorer and Why This Account Explanation', async ({ page }) => {
    await page.goto('/accounting');
    await page.getByRole('tab', { name: 'قيود اليومية المرحلة' }).evaluate((tab: HTMLElement) => tab.click());

    await expect(page.getByText('JV-2026-0001')).toBeVisible();

    const explainButton = page.getByRole('button', { name: 'لماذا تم اختيار هذا الحساب؟' }).first();
    await explainButton.click();
    await expect(page.getByText('شرح القيد وقواعد التوجيه المحاسبي')).toBeVisible();
    await expect(page.getByText('CustomerInvoicePosted')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-why-this-account-rtl.png'
    });
  });

  test('Journey 5: Trial Balance and General Ledger Reports', async ({ page }) => {
    await page.goto('/accounting');
    await page.getByRole('tab', { name: 'ميزان المراجعة' }).evaluate((tab: HTMLElement) => tab.click());

    await expect(page.getByRole('heading', { name: 'ميزان المراجعة' })).toBeVisible();
    await expect(page.getByText('المجاميع الإجمالية لميزان المراجعة')).toBeVisible();
    await expect(page.getByText('ميزان المراجعة متوازن')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-trial-balance-rtl.png',
      fullPage: true
    });

    await page.getByRole('tab', { name: 'دفتر الأستاذ العام' }).evaluate((tab: HTMLElement) => tab.click());
    await expect(page.getByRole('heading', { name: 'دفتر الأستاذ العام' })).toBeVisible();
    await expect(page.getByText('JV-2026-0001')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-general-ledger-rtl.png',
      fullPage: true
    });
  });

  test('Journey 6: General Journal 3-Line Creation, Draft Lifecycle, and Posting UX', async ({ page }) => {
    await page.goto('/accounting');
    await page.getByRole('tab', { name: 'قيد اليومية اليدوي' }).evaluate((tab: HTMLElement) => tab.click());

    await expect(page.getByRole('heading', { name: 'قيد اليومية العام اليدوي' })).toBeVisible();

    // Fill Entry Description
    const descInput = page.getByPlaceholder('تسوية إثبات مصاريف عمومية، تحويلات بين الحسابات...');
    await descInput.fill('قيد تسوية عام متوازن لثلاثة بنود مع مراكز التكلفة');

    // Add a 3rd line
    await page.getByRole('button', { name: 'إضافة سطر محاسبي جديد' }).click();

    // Line 1: Select Cash (110200), Debit 15000
    const accountPickers = page.getByRole('combobox', { name: 'الحساب المالي' });
    await accountPickers.nth(0).fill('110200');
    await page.getByRole('option', { name: /110200/ }).click();
    const numberInputs = page.locator('input[type="number"]');
    await numberInputs.nth(0).fill('15000');

    // Line 2: Select Sales Revenue (410000), Credit 25000
    await accountPickers.nth(1).fill('410000');
    await page.getByRole('option', { name: /410000/ }).click();
    await numberInputs.nth(3).fill('25000');

    // Line 3: Select Notes Receivable (110300), Debit 10000
    await accountPickers.nth(2).fill('110300');
    await page.getByRole('option', { name: /110300/ }).click();
    await numberInputs.nth(4).fill('10000');

    // Fill dimensions
    const dimSelectors = page.locator('select[aria-label="قيمة البعد"]');
    if (await dimSelectors.count() >= 3) {
      await dimSelectors.nth(0).selectOption({ index: 1 });
      await dimSelectors.nth(2).selectOption({ index: 3 });
    }

    // Verify balanced
    await expect(page.getByText('القيد متوازن تماماً')).toBeVisible();

    // 1. Save Draft
    const saveDraftBtn = page.getByRole('button', { name: 'حفظ المسودة' });
    await expect(saveDraftBtn).toBeVisible();
    await saveDraftBtn.evaluate((b: HTMLElement) => b.click());
    await expect(page.getByText('مسودة').first()).toBeVisible();

    // 2. Submit Draft
    const submitBtn = page.getByRole('button', { name: 'إرسال للاعتماد' });
    await expect(submitBtn).toBeVisible();
    await submitBtn.evaluate((b: HTMLElement) => b.click());
    await expect(page.getByText('مقدم للاعتماد').first()).toBeVisible();

    // 3. Approve Draft
    const approveBtn = page.getByRole('button', { name: 'اعتماد القيد' });
    await expect(approveBtn).toBeVisible();
    await approveBtn.evaluate((b: HTMLElement) => b.click());
    await expect(page.getByText('معتمد').first()).toBeVisible();

    // 4. Post Entry
    const postBtn = page.getByRole('button', { name: 'ترحيل القيد إلى دفتر الأستاذ' });
    await expect(postBtn).toBeVisible();
    await postBtn.evaluate((b: HTMLElement) => b.click());

    // Verify automatically navigated to Posted Journal Entries tab upon successful post
    await expect(page.getByRole('heading', { name: 'سجل قيود اليومية المرحلة' })).toBeVisible();

    // Visual screenshot of General Journal workflow in RTL
    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-general-journal-rtl.png',
      fullPage: true
    });
  });

  test('Journey 7: Entry Reversal Workflow & Duplicate Protection', async ({ page }) => {
    await page.goto('/accounting');
    await page.getByRole('tab', { name: 'قيود اليومية المرحلة' }).evaluate((tab: HTMLElement) => tab.click());

    await expect(page.getByText('JV-2026-0001')).toBeVisible();

    // Open Reversal Modal
    const reverseButton = page.getByRole('button', { name: 'عكس القيد' }).first();
    await reverseButton.click();

    await expect(page.getByRole('heading', { name: /عكس القيد المحاسبي/ })).toBeVisible();
    await expect(page.getByText('عكس القيد سينشئ قيداً محاسبياً عكسياً متوازناً جديداً')).toBeVisible();

    // Fill Reason
    const reasonInput = page.getByPlaceholder('خطأ في التوجيه، إلغاء المعاملة، إلخ...');
    await reasonInput.fill('تصحيح خطأ محاسبي وإعادة تصنيف الحساب');

    // Confirm Reversal
    await page.getByRole('button', { name: 'تأكيد إنشاء قيد العكس' }).click();

    // Modal closed
    await expect(page.getByRole('heading', { name: /عكس القيد المحاسبي/ })).not.toBeVisible();

    // Visual screenshot of Reversal
    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-reversal-rtl.png'
    });
  });

  test('Journey 8: Opening Balances Preview & Commit Workflow', async ({ page }) => {
    await page.goto('/accounting');
    await expect(page.getByText('المحاسبة العامة ودفتر الأستاذ')).toBeVisible();

    await page.getByRole('tab', { name: 'الأرصدة الافتتاحية' }).evaluate((tab: HTMLElement) => tab.click());
    await expect(page.getByRole('heading', { name: 'الأرصدة الافتتاحية للمنشأة' })).toBeVisible();

    // Fill Legacy System
    const legacyInput = page.getByLabel('النظام السابق / المصدر');
    await legacyInput.fill('Legacy-ERP-Migrated');

    // Select Account 1: Cash 110200, Debit 50000
    const accountPickers = page.getByRole('combobox', { name: 'الحساب المالي' });
    await accountPickers.nth(0).click();
    await accountPickers.nth(0).fill('110200');
    await expect(page.getByRole('option', { name: /110200/ })).toBeVisible();
    await page.getByRole('option', { name: /110200/ }).click();
    const numberInputs = page.locator('input[type="number"]');
    await numberInputs.nth(0).fill('50000');

    // Select Account 2: Sales 410000, Credit 50000
    await accountPickers.nth(1).click();
    await accountPickers.nth(1).fill('410000');
    await expect(page.getByRole('option', { name: /410000/ })).toBeVisible();
    await page.getByRole('option', { name: /410000/ }).click();
    await numberInputs.nth(3).fill('50000');

    // Verify balanced
    await expect(page.getByText('القيد متوازن تماماً')).toBeVisible();

    // Click Preview
    const previewBtn = page.getByRole('button', { name: 'فحص وتدقيق التوازن (معاينة)' });
    await previewBtn.evaluate((b: HTMLElement) => b.click());

    await expect(page.getByText('نتائج تدقيق دفعة الأرصدة الافتتاحية')).toBeVisible();
    await expect(page.getByText('القيد متوازن تماماً').nth(1)).toBeVisible();

    // Click Post
    const postBtn = page.getByRole('button', { name: 'اعتماد وترحيل الأرصدة الافتتاحية' });
    await postBtn.evaluate((b: HTMLElement) => b.click());

    await expect(page.getByText('تم ترحيل الأرصدة الافتتاحية بنجاح إلى الدفاتر')).toBeVisible();
    await expect(page.getByText('OB-2026-0001')).toBeVisible();

    // Visual screenshot of Opening Balances in RTL
    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-opening-balances-rtl.png',
      fullPage: true
    });
  });

  test('Journey 9: Permission-Aware Protection for Read-Only Users', async ({ page }) => {
    // Override permissions to read-only
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: '00000000-0000-0000-0000-000000000000',
          memberships: [{ id: 'atlas', tenantId: '00000000-0000-0000-0000-000000000000', displayName: 'شركة أطلس للتجارة' }],
          context: {
            displayName: 'شركة أطلس للتجارة',
            legalEntities: [
              {
                id: MAIN_ENTITY_ID,
                legalEntityId: MAIN_ENTITY_ID,
                code: 'MAIN',
                name: 'شركة أطلس للتجارة — الكيان الرئيسي',
                countryCode: 'EG',
                baseCurrencyCode: 'EGP'
              }
            ],
            branches: []
          },
          permissions: ['ledger.account.read', 'ledger.entry.read', 'ledger.reports.read']
        })
      });
    });

    await page.goto('/accounting');
    await expect(page.getByText('المحاسبة العامة ودفتر الأستاذ')).toBeVisible();

    // Mode switch button is hidden
    await expect(page.getByRole('button', { name: 'تغيير نمط المحاسبة' })).not.toBeVisible();

    // Reversal action in Entries table is hidden
    await page.getByRole('tab', { name: 'قيود اليومية المرحلة' }).evaluate((tab: HTMLElement) => tab.click());
    await expect(page.getByText('JV-2026-0001')).toBeVisible();
    await expect(page.getByRole('button', { name: 'عكس القيد' })).not.toBeVisible();

    // Mutating actions in General Journal are hidden
    await page.getByRole('tab', { name: 'قيد اليومية اليدوي' }).evaluate((tab: HTMLElement) => tab.click());
    await expect(page.getByRole('button', { name: 'حفظ المسودة' })).not.toBeVisible();
    await expect(page.getByRole('button', { name: 'ترحيل القيد إلى دفتر الأستاذ' })).not.toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-permissions-rtl.png',
      fullPage: true
    });
  });

  test('Journey 10: English LTR Visual Acceptance Suite', async ({ page }) => {
    // Set language to English in localStorage before navigation
    await page.addInitScript(() => {
      window.localStorage.setItem('todir.ui.language', 'en');
    });

    await page.goto('/accounting');

    // Verify LTR direction and English title
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await expect(page.getByText('General Accounting & Ledger')).toBeVisible();
    await expect(page.getByText('Accounting Engine Readiness')).toBeVisible();

    // 1. Overview LTR
    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-overview-ltr.png',
      fullPage: true
    });

    // 2. Chart of Accounts LTR
    await page.getByRole('tab', { name: 'Chart of Accounts' }).evaluate((tab: HTMLElement) => tab.click());
    await expect(page.getByRole('heading', { name: 'Chart of Accounts' })).toBeVisible();
    await expect(page.getByText('110000').first()).toBeVisible();
    const ltrNameCell = page.getByText('Current Assets').locator('xpath=ancestor::td').first();
    const ltrBox = await ltrNameCell.boundingBox();
    expect(ltrBox).not.toBeNull();
    expect(ltrBox!.width).toBeGreaterThanOrEqual(180);
    const ltrRow = page.getByText('110000').locator('xpath=ancestor::tr').first();
    const ltrRowBox = await ltrRow.boundingBox();
    expect(ltrRowBox!.height).toBeLessThan(120);

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-coa-ltr.png',
      fullPage: true
    });

    // 3. General Journal LTR
    await page.getByRole('tab', { name: 'Manual General Journal' }).evaluate((tab: HTMLElement) => tab.click());
    await expect(page.getByRole('heading', { name: 'Manual General Journal' })).toBeVisible();
    await expect(page.getByText('Save Draft')).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-general-journal-ltr.png',
      fullPage: true
    });

    // 4. Entries & Why This Account LTR
    await page.getByRole('tab', { name: 'Posted Journal Entries' }).evaluate((tab: HTMLElement) => tab.click());
    await expect(page.getByText('JV-2026-0001')).toBeVisible();
    const whyBtn = page.getByRole('button', { name: 'Why This Account?' }).first();
    await whyBtn.click();
    await expect(page.getByText('Entry Explanation & Determination Rules')).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-why-this-account-ltr.png'
    });
    // Close explanation modal
    await page.getByRole('button', { name: 'Close' }).first().click();

    // 5. Opening Balances LTR
    await page.getByRole('tab', { name: 'Opening Balances' }).evaluate((tab: HTMLElement) => tab.click());
    await expect(page.getByRole('heading', { name: 'Opening Balances' })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-opening-balances-ltr.png',
      fullPage: true
    });

    // 6. Trial Balance LTR
    await page.getByRole('tab', { name: 'Trial Balance' }).evaluate((tab: HTMLElement) => tab.click());
    await expect(page.getByRole('heading', { name: 'Trial Balance' })).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-trial-balance-ltr.png',
      fullPage: true
    });

    // 7. General Ledger LTR
    await page.getByRole('tab', { name: 'General Ledger' }).evaluate((tab: HTMLElement) => tab.click());
    await expect(page.getByRole('heading', { name: 'General Ledger' })).toBeVisible();
    const glContainer = page.locator('.ui-datagrid').first();
    await glContainer.evaluate((el: HTMLElement) => el.scrollTo({ left: el.scrollWidth, behavior: 'instant' }));
    const whyActionBtn = page.getByRole('button', { name: 'Why This Account?' }).first();
    await expect(whyActionBtn).toBeVisible();
    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-general-ledger-ltr.png',
      fullPage: true
    });
  });

  test('Journey 11: Multi-Currency & Trial Balance to General Ledger Drill-Down', async ({ page }) => {
    await page.goto('/accounting');
    await page.getByRole('tab', { name: 'ميزان المراجعة' }).evaluate((tab: HTMLElement) => tab.click());

    await expect(page.getByRole('heading', { name: 'ميزان المراجعة' })).toBeVisible();

    // Drill down from account row to General Ledger using "كشف الحركة"
    const accountDrillLink = page.getByRole('button', { name: 'كشف الحركة' }).first();
    await expect(accountDrillLink).toBeVisible();
    await accountDrillLink.click();

    // Verify navigated to General Ledger
    await expect(page.getByRole('heading', { name: 'دفتر الأستاذ العام' })).toBeVisible();
    await expect(page.getByText('JV-2026-0001')).toBeVisible();

    // Open Entry Explanation to verify multi-currency details
    const explainButton = page.getByRole('button', { name: 'لماذا تم اختيار هذا الحساب؟' }).first();
    await explainButton.evaluate((btn: HTMLElement) => btn.click());

    await expect(page.getByText('شرح القيد وقواعد التوجيه المحاسبي')).toBeVisible();
    await expect(page.getByText('EGP').first()).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-multi-currency-rtl.png'
    });
  });

  test('Journey 12: Viewport & Layout Overflow Regression Suite', async ({ page }) => {
    const viewports = [
      { width: 1280, height: 720, label: '1280x720' },
      { width: 1440, height: 900, label: '1440x900' }
    ];

    for (const vp of viewports) {
      await page.setViewportSize({ width: vp.width, height: vp.height });

      // RTL Checks
      await page.goto('/accounting');
      await page.evaluate(() => {
        window.localStorage.setItem('todir.ui.language', 'ar');
      });
      await page.reload();
      await expect(page.getByText('المحاسبة العامة ودفتر الأستاذ')).toBeVisible();

      const tabsToTest = [
        { name: 'نظرة عامة والجاهزية', shot: vp.width === 1440 ? 'artifacts/test-results/visual-acceptance/ledger-overview-rtl.png' : null },
        { name: 'دليل الحسابات', shot: null },
        { name: 'قيد اليومية اليدوي', shot: vp.width === 1440 ? 'artifacts/test-results/visual-acceptance/ledger-general-journal-rtl.png' : null },
        { name: 'الأرصدة الافتتاحية', shot: vp.width === 1440 ? 'artifacts/test-results/visual-acceptance/ledger-opening-balances-rtl.png' : null },
        { name: 'قيود اليومية المرحلة', shot: null },
        { name: 'ميزان المراجعة', shot: vp.width === 1440 ? 'artifacts/test-results/visual-acceptance/ledger-trial-balance-rtl.png' : null },
        { name: 'دفتر الأستاذ العام', shot: null }
      ];

      for (const t of tabsToTest) {
        await page.getByRole('tab', { name: t.name }).evaluate((tab: HTMLElement) => tab.click());
        await page.waitForTimeout(100);

        // Assert no horizontal document overflow
        const overflow = await page.evaluate(() => {
          return document.documentElement.scrollWidth - document.documentElement.clientWidth;
        });
        expect(overflow, `Overflow detected on tab ${t.name} at ${vp.label} RTL`).toBeLessThanOrEqual(1);

        if (t.shot) {
          await page.screenshot({ path: t.shot, fullPage: true });
        }
      }

      // Focused CoA visual/layout usability assertion in RTL
      await page.getByRole('tab', { name: 'دليل الحسابات' }).evaluate((tab: HTMLElement) => tab.click());
      await expect(page.getByText('110000').first()).toBeVisible();
      const rtlCell = page.getByText('الأصول المتداولة').locator('xpath=ancestor::td').first();
      const rtlBox = await rtlCell.boundingBox();
      expect(rtlBox, `Account name cell missing at ${vp.label} RTL`).not.toBeNull();
      expect(rtlBox!.width, `Account Name cell too narrow at ${vp.label} RTL: ${rtlBox!.width}px`).toBeGreaterThanOrEqual(180);
      const rtlRow = page.getByText('110000').locator('xpath=ancestor::tr').first();
      const rtlRowBox = await rtlRow.boundingBox();
      expect(rtlRowBox!.height, `Row height excessively tall at ${vp.label} RTL: ${rtlRowBox!.height}px`).toBeLessThan(120);
      expect(rtlRowBox!.height, `Row height too small at ${vp.label} RTL: ${rtlRowBox!.height}px`).toBeGreaterThan(30);

      // Verify internal table container exists and bounds table
      const rtlTableContainer = page.locator('.accounting-table-container').first();
      await expect(rtlTableContainer).toBeVisible();

      if (vp.width === 1280) {
        await page.screenshot({
          path: 'artifacts/test-results/visual-acceptance/ledger-coa-rtl.png',
          fullPage: true
        });
      }

      // Check Explanation Modal in RTL
      await page.getByRole('tab', { name: 'قيود اليومية المرحلة' }).evaluate((tab: HTMLElement) => tab.click());
      const explainBtn = page.getByRole('button', { name: 'لماذا تم اختيار هذا الحساب؟' }).first();
      await expect(explainBtn).toBeVisible();
      await explainBtn.click();
      await expect(page.getByText('شرح القيد وقواعد التوجيه المحاسبي')).toBeVisible();
      if (vp.width === 1440) {
        await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/ledger-why-this-account-rtl.png' });
      }
      await page.getByRole('button', { name: 'إغلاق' }).first().click();

      // LTR Checks
      await page.evaluate(() => {
        window.localStorage.setItem('todir.ui.language', 'en');
      });
      await page.reload();
      await expect(page.getByText('General Accounting & Ledger')).toBeVisible();

      const ltrTabs = [
        { name: 'Overview & Readiness', shot: vp.width === 1440 ? 'artifacts/test-results/visual-acceptance/ledger-overview-ltr.png' : null },
        { name: 'Manual General Journal', shot: vp.width === 1440 ? 'artifacts/test-results/visual-acceptance/ledger-general-journal-ltr.png' : null },
        { name: 'Opening Balances', shot: vp.width === 1440 ? 'artifacts/test-results/visual-acceptance/ledger-opening-balances-ltr.png' : null },
        { name: 'Trial Balance', shot: vp.width === 1440 ? 'artifacts/test-results/visual-acceptance/ledger-trial-balance-ltr.png' : null }
      ];

      for (const t of ltrTabs) {
        await page.getByRole('tab', { name: t.name }).evaluate((tab: HTMLElement) => tab.click());
        await page.waitForTimeout(100);

        const overflow = await page.evaluate(() => {
          return document.documentElement.scrollWidth - document.documentElement.clientWidth;
        });
        expect(overflow, `Overflow detected on tab ${t.name} at ${vp.label} LTR`).toBeLessThanOrEqual(1);

        if (t.shot) {
          await page.screenshot({ path: t.shot, fullPage: true });
        }
      }

      // Focused CoA visual/layout usability assertion in LTR
      await page.getByRole('tab', { name: 'Chart of Accounts' }).evaluate((tab: HTMLElement) => tab.click());
      await expect(page.getByText('110000').first()).toBeVisible();
      const ltrCell = page.getByText('Current Assets').locator('xpath=ancestor::td').first();
      const ltrBox = await ltrCell.boundingBox();
      expect(ltrBox, `Account name cell missing at ${vp.label} LTR`).not.toBeNull();
      expect(ltrBox!.width, `Account Name cell too narrow at ${vp.label} LTR: ${ltrBox!.width}px`).toBeGreaterThanOrEqual(180);
      const ltrRow = page.getByText('110000').locator('xpath=ancestor::tr').first();
      const ltrRowBox = await ltrRow.boundingBox();
      expect(ltrRowBox!.height, `Row height excessively tall at ${vp.label} LTR: ${ltrRowBox!.height}px`).toBeLessThan(120);
      expect(ltrRowBox!.height, `Row height too small at ${vp.label} LTR: ${ltrRowBox!.height}px`).toBeGreaterThan(30);

      // Verify internal table container exists and bounds table
      const ltrTableContainer = page.locator('.accounting-table-container').first();
      await expect(ltrTableContainer).toBeVisible();

      if (vp.width === 1280) {
        await page.screenshot({
          path: 'artifacts/test-results/visual-acceptance/ledger-coa-ltr.png',
          fullPage: true
        });
      }

      // Check Explanation Modal in LTR
      await page.getByRole('tab', { name: 'Posted Journal Entries' }).evaluate((tab: HTMLElement) => tab.click());
      const explainBtnLtr = page.getByRole('button', { name: 'Why This Account?' }).first();
      await explainBtnLtr.click();
      await expect(page.getByText('Entry Explanation & Determination Rules')).toBeVisible();
      if (vp.width === 1440) {
        await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/ledger-why-this-account-ltr.png' });
      }
      await page.getByRole('button', { name: 'Close' }).first().click();
    }
  });

  test('Journey 13: Tab Navigation Behavior & Scroll Affordance', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.goto('/accounting');
    await expect(page.getByText('المحاسبة العامة ودفتر الأستاذ')).toBeVisible();

    // Verify first tab is selected
    const overviewTab = page.getByRole('tab', { name: 'نظرة عامة والجاهزية' });
    await expect(overviewTab).toHaveAttribute('aria-selected', 'true');

    // Scroll tab navigation using scroll affordance button
    const scrollNextBtn = page.getByRole('button', { name: 'التمرير للتالي' });
    if (await scrollNextBtn.isVisible()) {
      await scrollNextBtn.click();
    }

    // Click last tab (reconciliation)
    const lastTab = page.getByRole('tab', { name: 'مطابقة التجميعات' });
    await lastTab.evaluate((t: HTMLElement) => t.click());
    await expect(lastTab).toHaveAttribute('aria-selected', 'true');
    await expect(page.getByRole('heading', { name: 'فحص مطابقة التجميعات المحاسبية' })).toBeVisible();

    // Capture tab navigation screenshot
    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/ledger-navigation-behavior.png'
    });
  });
});
