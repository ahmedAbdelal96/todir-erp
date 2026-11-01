import { test, expect, Page } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';

const MAIN_ENTITY_ID = '00000000-0000-0000-0000-000000000001';

async function captureScreenshot(page: Page, filename: string, options?: { fullPage?: boolean }) {
  const primaryPath = path.resolve(process.cwd(), 'artifacts/test-results/visual-acceptance', filename);
  const rootPath = path.resolve(process.cwd(), '../artifacts/test-results/visual-acceptance', filename);
  fs.mkdirSync(path.dirname(primaryPath), { recursive: true });
  fs.mkdirSync(path.dirname(rootPath), { recursive: true });
  await page.screenshot({ path: primaryPath, ...options });
  try {
    fs.copyFileSync(primaryPath, rootPath);
  } catch {
    // Ignore copy error
  }
}

test.describe('TODIR Accounting Frontend & Executive UX Suite', () => {
  test.beforeEach(async ({ page }) => {
    page.on('console', msg => console.log('BROWSER CONSOLE:', msg.type(), msg.text()));
    page.on('requestfailed', req => console.log('REQ FAILED:', req.method(), req.url(), req.failure()?.errorText));
    page.on('response', res => {
      if (res.status() >= 400) {
        console.log('HTTP ERROR:', res.status(), res.url());
      }
    });

    // CSRF token
    await page.route('**/api/v1/auth/csrf', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ token: 'mock-csrf-token' })
      });
    });

    // Session context
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          user: {
            id: 'user-001',
            email: 'cfo@atlas.com',
            displayName: 'أحمد محمود — المدير المالي'
          },
          context: {
            tenantId: 'c9fea3c8-249d-f150-a53a-84f5a891c82b',
            tenantName: 'مجموعة أطلس الدولية',
            legalEntities: [
              {
                id: MAIN_ENTITY_ID,
                legalEntityId: MAIN_ENTITY_ID,
                code: 'MAIN',
                name: 'شركة أطلس للتجارة العامة والتوزيع',
                countryCode: 'EG',
                baseCurrencyCode: 'EGP'
              }
            ],
            branches: []
          },
          permissions: [
            'accounting.health.read',
            'accounting.owner.read',
            'accounting.statement.read',
            'accounting.statement.manage',
            'accounting.reconciliation.read',
            'accounting.reconciliation.run',
            'accounting.fx-revaluation.read',
            'accounting.fx-revaluation.post',
            'accounting.close.execute',
            'accounting.close.reopen',
            'accounting.opening.approve',
            'ledger.account.read',
            'ledger.account.manage',
            'ledger.journal.read',
            'ledger.journal.manage',
            'ledger.period.read',
            'ledger.period.manage',
            'ledger.period.soft_close',
            'ledger.period.close',
            'ledger.period.reopen',
            'ledger.lock.read',
            'ledger.lock.manage',
            'ledger.entry.read',
            'ledger.entry.reverse',
            'ledger.entry.explain',
            'ledger.general_journal.create',
            'ledger.general_journal.edit',
            'ledger.general_journal.submit',
            'ledger.general_journal.approve',
            'ledger.general_journal.post',
            'ledger.dimension.read',
            'ledger.dimension.manage',
            'ledger.opening.read',
            'ledger.opening.manage',
            'ledger.opening.post',
            'ledger.reports.read',
            'ledger.configuration.read',
            'ledger.configuration.manage'
          ]
        })
      });
    });

    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ id: 'home' }, { id: 'accounting' }, { id: 'sales' }, { id: 'purchasing' }])
      });
    });

    // Ledger Engine & Readiness
    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/readiness`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: MAIN_ENTITY_ID,
          mode: 'Full',
          state: 'Ready',
          capabilities: [
            { capability: 'invoicing', state: 'Ready', reasons: [] },
            { capability: 'payments', state: 'Ready', reasons: [] },
            { capability: 'inventory', state: 'Ready', reasons: [] },
            { capability: 'general_journal', state: 'Ready', reasons: [] },
            { capability: 'period_close', state: 'Ready', reasons: [] }
          ],
          version: 1
        })
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/mode`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ mode: 'Full', version: 1 })
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/entries`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'ent-101',
            entryNumber: 'JE-2026-000101',
            postingDate: '2026-11-01',
            sourceModule: 'Sales',
            sourceReference: 'INV-2026-0042',
            totalDebit: 13680.0,
            totalCredit: 13680.0,
            currencyCode: 'EGP',
            linesCount: 3,
            state: 'Posted'
          },
          {
            id: 'ent-102',
            entryNumber: 'JE-2026-000102',
            postingDate: '2026-11-01',
            sourceModule: 'Purchasing',
            sourceReference: 'BILL-2026-0019',
            totalDebit: 45000.0,
            totalCredit: 45000.0,
            currencyCode: 'EGP',
            linesCount: 2,
            state: 'Posted'
          }
        ])
      });
    });

    // Accounting Health
    await page.route(new RegExp(`/api/v1/accounting/legal-entities/${MAIN_ENTITY_ID}/health`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: MAIN_ENTITY_ID,
          asOfDate: '2026-11-01',
          status: 'Healthy',
          items: [
            {
              code: 'ledger.readiness',
              severity: 'Info',
              status: 'Pass',
              message: 'كافة تكوينات التوجيه المحاسبي ودفاتر الأستاذ مكتملة وجاهزة للعمل.',
              source: 'Ledger'
            },
            {
              code: 'ledger.draft_entries',
              severity: 'Info',
              status: 'Pass',
              message: 'لا توجد قيود مسودة معلقة غير مرحلة تؤثر على دقة التقارير.',
              source: 'Ledger'
            },
            {
              code: 'subledger.reconciliation',
              severity: 'Info',
              status: 'Pass',
              message: 'دفاتر الأستاذ المساعدة (العملاء، الموردون، المخزون) متطابقة تماماً مع الأستاذ العام.',
              source: 'Subledger'
            },
            {
              code: 'accounting.trial_balance_balanced',
              severity: 'Info',
              status: 'Pass',
              message: 'ميزان المراجعة متوازن بالكامل (إجمالي المدين = إجمالي الدائن).',
              source: 'Accounting'
            },
            {
              code: 'accounting.period_locks',
              severity: 'Attention',
              status: 'Warning',
              message: 'الفترة المالية الحالية (نوفمبر 2026) مفتوحة للتسجيل، يرجى مراجعة إغلاق شهر أكتوبر.',
              source: 'Period'
            }
          ]
        })
      });
    });

    // Owner Summary
    await page.route(new RegExp(`/api/v1/accounting/legal-entities/${MAIN_ENTITY_ID}/owner-summary`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: MAIN_ENTITY_ID,
          asOfDate: '2026-11-01',
          currencyCode: 'EGP',
          cashBookValue: 3450250.75,
          receivables: 1850400.0,
          payables: 920150.5,
          inventoryValue: 4210000.0,
          profit: 895300.25,
          health: 'Healthy'
        })
      });
    });

    // Financial Statements - Profit & Loss
    await page.route(new RegExp(`/api/v1/accounting/legal-entities/${MAIN_ENTITY_ID}/statements/ProfitAndLoss`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          kind: 'ProfitAndLoss',
          title: 'قائمة الأرباح والخسائر المعتمدة',
          fromDate: '2026-01-01',
          toDate: '2026-11-01',
          currencyCode: 'EGP',
          total: 895300.25,
          lines: [
            { code: 'REV', name: 'إيرادات المبيعات والنشاط التجاري', nameAr: 'إيرادات المبيعات والنشاط التجاري', nameEn: 'Revenue', accountType: 'Revenue', lineKind: 'MappedAccounts', amount: 8450000.0, accountIds: [] },
            { code: 'COGS', name: 'تكلفة البضاعة المباعة', nameAr: 'تكلفة البضاعة المباعة', nameEn: 'COGS', accountType: 'Expense', lineKind: 'MappedAccounts', amount: -5120000.0, accountIds: [] },
            { code: 'GROSS_PROFIT', name: 'مجمل الربح التجاري', nameAr: 'مجمل الربح التجاري', nameEn: 'Gross Profit', accountType: 'Revenue', lineKind: 'Subtotal', amount: 3330000.0, accountIds: [] },
            { code: 'OPEX_SALES', name: 'مصروفات البيع والتسويق', nameAr: 'مصروفات البيع والتسويق', nameEn: 'Selling Expenses', accountType: 'Expense', lineKind: 'MappedAccounts', amount: -650000.0, accountIds: [] },
            { code: 'OPEX_GEN', name: 'المصروفات العمومية والإدارية', nameAr: 'المصروفات العمومية والإدارية', nameEn: 'G&A Expenses', accountType: 'Expense', lineKind: 'MappedAccounts', amount: -1120000.0, accountIds: [] },
            { code: 'NET_INCOME', name: 'صافي الربح بعد الضريبة', nameAr: 'صافي الربح بعد الضريبة', nameEn: 'Net Income', accountType: 'Revenue', lineKind: 'Arithmetic', amount: 895300.25, accountIds: [] }
          ],
          isBalanced: true
        })
      });
    });

    // Financial Statements - Balance Sheet
    await page.route(new RegExp(`/api/v1/accounting/legal-entities/${MAIN_ENTITY_ID}/statements/BalanceSheet`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          kind: 'BalanceSheet',
          title: 'قائمة المركز المالي المعتمدة',
          fromDate: '2026-01-01',
          toDate: '2026-11-01',
          currencyCode: 'EGP',
          total: 14310650.75,
          lines: [
            { code: 'ASSETS_CURR', name: 'الأصول المتداولة (النقدية، العملاء، المخزون)', nameAr: 'الأصول المتداولة (النقدية، العملاء، المخزون)', nameEn: 'Current Assets', accountType: 'Asset', lineKind: 'MappedAccounts', amount: 9510650.75, accountIds: [] },
            { code: 'ASSETS_FIXED', name: 'الأصول الثابتة وغير المتداولة', nameAr: 'الأصول الثابتة وغير المتداولة', nameEn: 'Fixed Assets', accountType: 'Asset', lineKind: 'MappedAccounts', amount: 4800000.0, accountIds: [] },
            { code: 'TOTAL_ASSETS', name: 'إجمالي الأصول', nameAr: 'إجمالي الأصول', nameEn: 'Total Assets', accountType: 'Asset', lineKind: 'Subtotal', amount: 14310650.75, accountIds: [] },
            { code: 'TOTAL_LIAB_EQUITY', name: 'إجمالي الالتزامات وحقوق الملكية', nameAr: 'إجمالي الالتزامات وحقوق الملكية', nameEn: 'Total Liabilities & Equity', accountType: 'Liability', lineKind: 'Subtotal', amount: 14310650.75, accountIds: [] }
          ],
          isBalanced: true
        })
      });
    });

    // Control Reconciliations List
    await page.route(new RegExp(`/api/v1/accounting/legal-entities/${MAIN_ENTITY_ID}/reconciliations(\\?.*)?$`), async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'rec-run-99',
            type: 'AR',
            asOfDate: '2026-11-01',
            sourceBalance: 1850400.0,
            ledgerBalance: 1850400.0,
            difference: 0.0,
            currencyCode: 'EGP',
            status: 'Reconciled',
            diagnostics: ['Subledger active transactions verified', 'Balance matched control account 110100'],
            createdAt: '2026-11-01T12:00:00Z'
          })
        });
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'rec-run-01',
            type: 'AR',
            asOfDate: '2026-11-01',
            sourceBalance: 1850400.0,
            ledgerBalance: 1850400.0,
            difference: 0.0,
            currencyCode: 'EGP',
            status: 'Reconciled',
            diagnostics: ['مطابقة حساب مراقبة العملاء مع سجلات المبيعات مكتملة'],
            createdAt: '2026-11-01T10:00:00Z'
          },
          {
            id: 'rec-run-02',
            type: 'AP',
            asOfDate: '2026-11-01',
            sourceBalance: 920150.5,
            ledgerBalance: 920150.5,
            difference: 0.0,
            currencyCode: 'EGP',
            status: 'Reconciled',
            diagnostics: ['مطابقة حساب مراقبة الموردين مع الفواتير المعتمدة مكتملة'],
            createdAt: '2026-11-01T10:05:00Z'
          },
          {
            id: 'rec-run-03',
            type: 'Inventory',
            asOfDate: '2026-11-01',
            sourceBalance: 4210000.0,
            ledgerBalance: 4210000.0,
            difference: 0.0,
            currencyCode: 'EGP',
            status: 'Reconciled',
            diagnostics: ['مطابقة تقييم المخزون المستودعي مع الحساب العام 110400'],
            createdAt: '2026-11-01T10:10:00Z'
          }
        ])
      });
    });

    // Control Reconciliation Issues
    await page.route(new RegExp(`/api/v1/accounting/legal-entities/${MAIN_ENTITY_ID}/reconciliations/issues`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'issue-01',
            reconciliationRunId: 'rec-run-01',
            code: 'DIFF_UNPOSTED_RECEIPT',
            severity: 'Info',
            status: 'Resolved',
            createdAt: '2026-10-25T08:00:00Z',
            resolvedAt: '2026-10-25T09:30:00Z'
          }
        ])
      });
    });

    // FX Revaluation
    await page.route('**/api/v1/accounting/**/fx-revaluations/preview', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'fx-run-preview-01',
          asOfDate: '2026-11-01',
          status: 'Preview',
          createdAt: '2026-11-01T10:00:00Z',
          autoReversalDate: '2026-12-01',
          lines: [
            {
              id: 'fx-line-01',
              openItemId: 'item-usd-inv-01',
              direction: 'Receivable',
              transactionCurrency: 'USD',
              openTransactionAmount: 10000.0,
              baseCurrency: 'EGP',
              carryingBaseAmount: 485000.0,
              exchangeRate: 51.2,
              revaluedBaseAmount: 512000.0,
              differenceAmount: 27000.0,
              differenceRole: 'Gain',
              exchangeRateSnapshot: '2026-11-01'
            },
            {
              id: 'fx-line-02',
              openItemId: 'item-eur-bill-02',
              direction: 'Payable',
              transactionCurrency: 'EUR',
              openTransactionAmount: 5000.0,
              baseCurrency: 'EGP',
              carryingBaseAmount: 260000.0,
              exchangeRate: 54.5,
              revaluedBaseAmount: 272500.0,
              differenceAmount: -12500.0,
              differenceRole: 'Loss',
              exchangeRateSnapshot: '2026-11-01'
            }
          ]
        })
      });
    });

    // Periods & Fiscal Years
    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/fiscal-years`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'fy-2026',
            year: 2026,
            startDate: '2026-01-01',
            endDate: '2026-12-31',
            status: 'Open'
          }
        ])
      });
    });

    await page.route(new RegExp(`/api/v1/ledger/legal-entities/${MAIN_ENTITY_ID}/periods`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'period-2026-10',
            fiscalYearId: 'fy-2026',
            periodNumber: 10,
            nameAr: 'أكتوبر 2026',
            nameEn: 'October 2026',
            startDate: '2026-10-01',
            endDate: '2026-10-31',
            status: 'Open'
          },
          {
            id: 'period-2026-11',
            fiscalYearId: 'fy-2026',
            periodNumber: 11,
            nameAr: 'نوفمبر 2026',
            nameEn: 'November 2026',
            startDate: '2026-11-01',
            endDate: '2026-11-30',
            status: 'Open'
          }
        ])
      });
    });

    // Close preview endpoint
    await page.route('**/api/v1/accounting/**/close/preview', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'close-run-preview-10',
          periodId: 'period-2026-10',
          status: 'Ready',
          checks: [
            { code: 'DRAFT_ENTRIES', severity: 'Info', status: 'Pass', message: 'لا توجد قيود مسودة معلقة في الفترة.' },
            { code: 'SUBLEDGER_RECONCILED', severity: 'Info', status: 'Pass', message: 'دفاتر الأستاذ المساعدة متطابقة بالكامل.' },
            { code: 'INVENTORY_CUTOFF', severity: 'Info', status: 'Pass', message: 'حركات المخزون مرحلة حتى تاريخ الإغلاق.' },
            { code: 'TRIAL_BALANCE', severity: 'Info', status: 'Pass', message: 'ميزان المراجعة متوازن تماماً.' }
          ],
          createdAt: '2026-11-01T12:00:00Z'
        })
      });
    });
  });

  test('01. Owner Financial Visibility Screen (Executive KPI Cards & Financial Summary)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/accounting?tab=ownerSummary');
    await page.waitForLoadState('networkidle');

    // Verify Owner Financial View is visible
    const ownerView = page.locator('[data-testid="owner-financial-view"]');
    await expect(ownerView).toBeVisible();

    // Verify KPI Cards are rendered
    await expect(page.locator('[data-testid="owner-kpi-cash"]')).toBeVisible();
    await expect(page.locator('[data-testid="owner-kpi-receivables"]')).toBeVisible();
    await expect(page.locator('[data-testid="owner-kpi-payables"]')).toBeVisible();
    await expect(page.locator('[data-testid="owner-kpi-inventory"]')).toBeVisible();
    await expect(page.locator('[data-testid="owner-kpi-profit"]')).toBeVisible();

    // Verify Health status badge
    await expect(page.getByText('البيانات متطابقة وسليمة 100%')).toBeVisible();

    // Capture visual acceptance screenshot
    await captureScreenshot(page, 'accounting-01-owner-financial-view.png');
  });

  test('02. Accounting Health & Operational Control Center', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/accounting?tab=health');
    await page.waitForLoadState('networkidle');

    const healthView = page.locator('[data-testid="accounting-health-view"]');
    await expect(healthView).toBeVisible();

    // Verify overall status badge
    await expect(page.getByText('النظام المحاسبي سليم ومتسق بالكامل')).toBeVisible();

    // Verify diagnostic checklist items
    await expect(page.getByText('كافة تكوينات التوجيه المحاسبي ودفاتر الأستاذ مكتملة وجاهزة للعمل.')).toBeVisible();
    await expect(page.getByText('دفاتر الأستاذ المساعدة (العملاء، الموردون، المخزون) متطابقة تماماً مع الأستاذ العام.')).toBeVisible();

    // Capture visual acceptance screenshot
    await captureScreenshot(page, 'accounting-02-health-control-center.png');
  });

  test('03. Financial Statements: Profit & Loss and Balance Sheet', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 850 });
    await page.goto('/accounting?tab=financialStatements');
    await page.waitForLoadState('networkidle');

    const statementsView = page.locator('[data-testid="financial-statements-view"]');
    await expect(statementsView).toBeVisible();

    // Verify P&L lines
    await expect(page.getByText('قائمة الأرباح والخسائر المعتمدة')).toBeVisible();
    await expect(page.getByText('إيرادات المبيعات والنشاط التجاري')).toBeVisible();
    await expect(page.getByText('مجمل الربح التجاري')).toBeVisible();
    await expect(page.getByText('صافي الربح بعد الضريبة')).toBeVisible();

    // Capture P&L screenshot
    await captureScreenshot(page, 'accounting-03-pnl-statement.png');

    // Switch to Balance Sheet by clicking tab
    const balanceSheetTab = page.getByRole('button', { name: 'قائمة المركز المالي (الميزانية العمومية)' });
    await balanceSheetTab.click();
    await page.waitForLoadState('networkidle');

    // Verify Balance Sheet lines and Balanced indicator
    await expect(page.getByText('قائمة المركز المالي المعتمدة')).toBeVisible();
    await expect(page.getByText('إجمالي الأصول')).toBeVisible();
    await expect(page.getByText('الميزانية متزنة: الأصول = الالتزامات + حقوق الملكية')).toBeVisible();

    // Capture Balance Sheet screenshot
    await captureScreenshot(page, 'accounting-04-balance-sheet.png');
  });

  test('04. Subledger Control Reconciliations (AR, AP, Inventory)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 850 });
    await page.goto('/accounting?tab=controlReconciliation');
    await page.waitForLoadState('networkidle');

    const recView = page.locator('[data-testid="control-reconciliations-view"]');
    await expect(recView).toBeVisible();

    // Verify reconciliation action cards
    await expect(page.getByText('مطابقة العملاء (AR)')).toBeVisible();
    await expect(page.getByText('مطابقة الموردين (AP)')).toBeVisible();
    await expect(page.getByText('مطابقة المخزون (Inventory)')).toBeVisible();

    // Verify past reconciliation runs table
    await expect(page.locator('[data-testid="reconciliation-row-rec-run-01"]')).toBeVisible();
    await expect(page.locator('[data-testid="reconciliation-row-rec-run-02"]')).toBeVisible();
    await expect(page.locator('[data-testid="reconciliation-row-rec-run-03"]')).toBeVisible();

    // Capture screenshot
    await captureScreenshot(page, 'accounting-05-control-reconciliations.png');
  });

  test('05. Currency Revaluation (FX Workspace & Preview Lines)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 850 });
    await page.goto('/accounting?tab=fxRevaluation');
    await page.waitForLoadState('networkidle');

    const fxView = page.locator('[data-testid="fx-revaluation-view"]');
    await expect(fxView).toBeVisible();

    // Click preview button
    const previewBtn = page.getByRole('button', { name: /معاينة فروق أسعار الصرف/i });
    await previewBtn.click();
    await page.waitForLoadState('networkidle');

    // Verify revaluation lines appeared
    await expect(page.getByText('Receivable')).toBeVisible();
    await expect(page.getByText('Payable')).toBeVisible();
    await expect(page.getByText('27,000.00 EGP')).toBeVisible();

    // Capture screenshot
    await captureScreenshot(page, 'accounting-06-fx-revaluation.png');
  });

  test('06. Period Close & Pre-flight Invariant Verification Modal', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 850 });
    await page.goto('/accounting?tab=periods');
    await page.waitForLoadState('networkidle');

    // Click Close Period action button on period October 2026
    const closeBtn = page.getByRole('button', { name: 'إقفال نهائي' }).first();
    await closeBtn.click();

    // Modal should be visible
    const modal = page.locator('[data-testid="period-close-modal"]');
    await expect(modal).toBeVisible();

    // Verify pre-flight checks are rendered
    await expect(page.getByText('قائمة فحوصات وضوابط الإقفال')).toBeVisible();
    await expect(page.getByText('لا توجد قيود مسودة معلقة في الفترة.')).toBeVisible();
    await expect(page.getByText('دفاتر الأستاذ المساعدة متطابقة بالكامل.')).toBeVisible();

    // Capture screenshot
    await captureScreenshot(page, 'accounting-07-period-close-modal.png');
  });

  test('07. Tablet & Mobile Responsive Verification', async ({ page }) => {
    // Tablet (768px)
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/accounting?tab=ownerSummary');
    await page.waitForLoadState('networkidle');
    await captureScreenshot(page, 'accounting-08-tablet-768.png');

    // Mobile (390px)
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/accounting?tab=ownerSummary');
    await page.waitForLoadState('networkidle');
    await captureScreenshot(page, 'accounting-09-mobile-390.png');
  });
});
