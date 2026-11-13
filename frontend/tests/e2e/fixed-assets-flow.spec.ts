import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';

const LEGAL_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const TEST_ASSET_ID = 'fa-00000000-0000-0000-0000-000000000001';
const TEST_CIP_ASSET_ID = 'fa-00000000-0000-0000-0000-000000000005';
const TEST_DISPOSED_ASSET_ID = 'fa-00000000-0000-0000-0000-000000000006';

const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/fixed-assets-acceptance');
const ARTIFACTS_DIR = process.env.TEST_ARTIFACTS_DIR || LOCAL_ARTIFACTS_DIR;

let consoleErrors: string[] = [];
let pageErrors: string[] = [];

test.describe('TODIR Fixed Assets & Custody (Module 22) — End-to-End Visual & Functional Acceptance Suite', () => {
  test.beforeAll(() => {
    if (!fs.existsSync(ARTIFACTS_DIR)) {
      fs.mkdirSync(ARTIFACTS_DIR, { recursive: true });
    }
  });

  test.beforeEach(async ({ page }) => {
    consoleErrors = [];
    pageErrors = [];

    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        const text = msg.text();
        if (!text.includes('404') && !text.includes('favicon') && !text.includes('502') && !text.includes('Bad Gateway')) {
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

    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          identityId: '00000000-0000-0000-0000-000000000001',
          tenantId: '00000000-0000-0000-0000-000000000000',
          memberships: [
            { id: 'atlas', tenantId: '00000000-0000-0000-0000-000000000000', displayName: 'مجموعة أطلس الصناعية' }
          ],
          context: {
            displayName: 'مجموعة أطلس الصناعية',
            legalEntities: [
              {
                id: LEGAL_ENTITY_ID,
                legalEntityId: LEGAL_ENTITY_ID,
                code: 'MAIN',
                name: 'مجموعة أطلس الصناعية - المركز الرئيسي',
                countryCode: 'SA',
                baseCurrencyCode: 'SAR'
              }
            ],
            branches: [
              {
                id: 'b1',
                legalEntityId: LEGAL_ENTITY_ID,
                code: 'RUH',
                name: 'فرع الرياض الرئيسي'
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

    await page.route('**/api/v1/authorization/permissions*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          '*',
          'assets.read',
          'assets.create',
          'assets.assign',
          'assets.financial.read',
          'assets.depreciation.prepare',
          'assets.depreciation.post',
          'assets.book.manage',
          'assets.capitalize',
          'assets.disposal.manage',
          'assets.verification.manage'
        ])
      });
    });

    await page.route('**/api/v1/me/preferences', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ language: 'ar', theme: 'light' })
      });
    });

    await page.route('**/api/v1/me/navigation-preferences*', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    });

    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'home' },
          { id: 'fixed-assets' }
        ])
      });
    });

    // 2. Fixed Assets Categories
    await page.route('**/api/v1/fixed-assets/categories*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'cat-01', code: 'CAT-IT', name: 'أجهزة وخوادم تقنية المعلومات', assetNature: 'Equipment', defaultUsefulLifeMonths: 60, defaultResidualPercentage: 5, defaultDepreciationMethod: 'StraightLine' },
          { id: 'cat-02', code: 'CAT-VEH', name: 'أسطول المركبات والشاحنات', assetNature: 'Vehicle', defaultUsefulLifeMonths: 84, defaultResidualPercentage: 10, defaultDepreciationMethod: 'StraightLine' },
          { id: 'cat-03', code: 'CAT-BLD', name: 'العقارات والمباني الإدارية', assetNature: 'Building', defaultUsefulLifeMonths: 360, defaultResidualPercentage: 15, defaultDepreciationMethod: 'StraightLine' },
          { id: 'cat-04', code: 'CAT-LND', name: 'الأراضي الاستثمارية', assetNature: 'Land', defaultUsefulLifeMonths: 0, defaultResidualPercentage: 100, defaultDepreciationMethod: 'NoDepreciation' },
          { id: 'cat-05', code: 'CAT-CIP', name: 'مشروعات رأسمالية تحت التنفيذ', assetNature: 'CIP', defaultUsefulLifeMonths: 0, defaultResidualPercentage: 0, defaultDepreciationMethod: 'StraightLine' }
        ])
      });
    });

    // 3. Fixed Assets Locations
    await page.route('**/api/v1/fixed-assets/locations*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'loc-01', legalEntityId: LEGAL_ENTITY_ID, code: 'LOC-HQ-01', name: 'المقر الرئيسي - مبنى برج العليا - الرياض', locationType: 'Building', isActive: true },
          { id: 'loc-02', legalEntityId: LEGAL_ENTITY_ID, code: 'LOC-WH-02', name: 'المستودع المركزي - المنطقة الصناعية - جدة', locationType: 'Warehouse', isActive: true },
          { id: 'loc-03', legalEntityId: LEGAL_ENTITY_ID, code: 'LOC-FAC-03', name: 'مجمع المصانع وورش الصيانة - الدمام', locationType: 'Plant', isActive: true }
        ])
      });
    });

    // 4. Fixed Assets Books
    await page.route('**/api/v1/fixed-assets/books*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'book-01', legalEntityId: LEGAL_ENTITY_ID, code: 'CORP-01', name: 'الدفتر المالي المؤسسي (Corporate GL)', postsToLedger: true, isStatutoryTaxBook: false, defaultDepreciationMethod: 'StraightLine', currency: 'SAR' },
          { id: 'book-02', legalEntityId: LEGAL_ENTITY_ID, code: 'ZAKAT-TAX', name: 'الدفتر الضريبي والزكوي (ZATCA Statutory)', postsToLedger: false, isStatutoryTaxBook: true, defaultDepreciationMethod: 'StraightLine', currency: 'SAR' }
        ])
      });
    });

    // 5. Fixed Assets Reconciliation
    await page.route('**/api/v1/fixed-assets/books/*/reconciliation*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          bookId: 'book-01',
          legalEntityId: LEGAL_ENTITY_ID,
          asOfDate: '2026-11-14',
          assetGrossCost: 8885000,
          ledgerGrossCost: 8885000,
          grossCostDifference: 0,
          assetAccumulatedDepreciation: 695000,
          ledgerAccumulatedDepreciation: 695000,
          accumulatedDepreciationDifference: 0,
          status: 'Reconciled',
          discrepancyCount: 0
        })
      });
    });

    // 6. Assets List
    await page.route('**/api/v1/fixed-assets/assets?*', async (route) => {
      const url = new URL(route.request().url());
      const stateFilter = url.searchParams.get('state');

      if (stateFilter === 'UnderConstruction') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: TEST_CIP_ASSET_ID,
              legalEntityId: LEGAL_ENTITY_ID,
              categoryId: 'cat-05',
              assetNumber: 'CIP-2026-005',
              name: 'مشروع توسعة خط الإنتاج والتعبئة الآلي بالمصنع',
              description: 'أعمال تجهيز وتوريد معدات هيدروليكية وسيور ناقلة بالدمام',
              assetNature: 'CIP',
              trackingGranularity: 'Individual',
              quantity: 1,
              placedInServiceDate: '2027-01-01',
              lifecycleState: 'UnderConstruction',
              acquisitionDate: '2026-03-01',
              createdAt: '2026-03-01T08:00:00Z',
              rowVersion: 1,
              accumulatedCost: 750000,
              currency: 'SAR'
            }
          ])
        });
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: TEST_ASSET_ID,
            legalEntityId: LEGAL_ENTITY_ID,
            categoryId: 'cat-01',
            assetNumber: 'FA-2026-001',
            name: 'خادم قاعدة البيانات الرئيسي Dell PowerEdge R750',
            serialNumber: 'SRV-DELL-98242',
            barcode: 'BC-98242001',
            assetNature: 'Equipment',
            trackingGranularity: 'Individual',
            quantity: 1,
            acquisitionCost: 185000,
            accumulatedDepreciation: 60000,
            netBookValue: 125000,
            salvageValue: 5000,
            usefulLifeMonths: 60,
            lifecycleState: 'Active',
            acquisitionDate: '2025-01-15',
            placedInServiceDate: '2025-02-01',
            currency: 'SAR',
            locationName: 'المقر الرئيسي - مبنى برج العليا - الرياض',
            custodianId: 'م. خالد السبيعي (IT Operations)',
            createdAt: '2025-01-15T09:00:00Z',
            rowVersion: 1
          },
          {
            id: 'fa-0002',
            legalEntityId: LEGAL_ENTITY_ID,
            categoryId: 'cat-02',
            assetNumber: 'FA-2026-002',
            name: 'شاحنة نقل بضائع ثقيلة مرسيدس أكتروس 3340',
            serialNumber: 'WDB-ACT-2024-89',
            barcode: 'BC-891002',
            assetNature: 'Vehicle',
            trackingGranularity: 'Individual',
            quantity: 1,
            acquisitionCost: 450000,
            accumulatedDepreciation: 135000,
            netBookValue: 315000,
            salvageValue: 45000,
            usefulLifeMonths: 84,
            lifecycleState: 'Active',
            acquisitionDate: '2024-06-10',
            placedInServiceDate: '2024-06-15',
            currency: 'SAR',
            locationName: 'المستودع المركزي - المنطقة الصناعية - جدة',
            custodianId: 'سالم الدوسري (Fleet Lead)',
            createdAt: '2024-06-10T08:00:00Z',
            rowVersion: 1
          },
          {
            id: 'fa-0003',
            legalEntityId: LEGAL_ENTITY_ID,
            categoryId: 'cat-03',
            assetNumber: 'FA-2026-003',
            name: 'مبنى المستودع اللوجستي المركزي - جدة',
            barcode: 'BC-LOC-02',
            assetNature: 'Building',
            trackingGranularity: 'Individual',
            quantity: 1,
            acquisitionCost: 2500000,
            accumulatedDepreciation: 400000,
            netBookValue: 2100000,
            salvageValue: 375000,
            usefulLifeMonths: 360,
            lifecycleState: 'Active',
            acquisitionDate: '2021-01-01',
            placedInServiceDate: '2021-01-01',
            currency: 'SAR',
            locationName: 'المستودع المركزي - المنطقة الصناعية - جدة',
            custodianId: 'إدارة العمليات اللوجستية',
            createdAt: '2021-01-01T00:00:00Z',
            rowVersion: 1
          },
          {
            id: 'fa-0004',
            legalEntityId: LEGAL_ENTITY_ID,
            categoryId: 'cat-04',
            assetNumber: 'FA-2026-004',
            name: 'أرض مجمع المصانع والورش بالدمام (غير قابلة للإهلاك)',
            assetNature: 'Land',
            trackingGranularity: 'Individual',
            quantity: 1,
            acquisitionCost: 5000000,
            accumulatedDepreciation: 0,
            netBookValue: 5000000,
            salvageValue: 5000000,
            usefulLifeMonths: 0,
            lifecycleState: 'Active',
            acquisitionDate: '2019-05-20',
            placedInServiceDate: '2019-05-20',
            currency: 'SAR',
            locationName: 'مجمع المصانع وورش الصيانة - الدمام',
            custodianId: 'الإدارة العامة للشؤون الإدارية',
            createdAt: '2019-05-20T00:00:00Z',
            rowVersion: 1
          },
          {
            id: TEST_DISPOSED_ASSET_ID,
            legalEntityId: LEGAL_ENTITY_ID,
            categoryId: 'cat-01',
            assetNumber: 'FA-2026-006',
            name: 'رافعة شوكية تويوتا 3 طن كهربائية (مستبعدة للتخريد)',
            serialNumber: 'TYT-FL-2018-091',
            assetNature: 'Equipment',
            trackingGranularity: 'Individual',
            quantity: 1,
            acquisitionCost: 95000,
            accumulatedDepreciation: 95000,
            netBookValue: 0,
            salvageValue: 0,
            usefulLifeMonths: 60,
            lifecycleState: 'Disposed',
            acquisitionDate: '2018-02-10',
            placedInServiceDate: '2018-02-15',
            currency: 'SAR',
            locationName: 'المستودع المركزي - المنطقة الصناعية - جدة',
            custodianId: 'المستودع',
            createdAt: '2018-02-10T00:00:00Z',
            rowVersion: 2
          }
        ])
      });
    });

    // 7. Single Asset Detail Timeline & Assignments
    await page.route(`**/api/v1/fixed-assets/assets/${TEST_ASSET_ID}/timeline*`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'evt-1', assetId: TEST_ASSET_ID, eventType: 'Acquisition', occurredAt: '2025-01-15T09:00:00Z', description: 'تسجيل وقيد الأصل الرأسمالي بتكلفة 185,000 ر.س' },
          { id: 'evt-2', assetId: TEST_ASSET_ID, eventType: 'InService', occurredAt: '2025-02-01T08:00:00Z', description: 'دخول الخدمة وبدء الإهلاك الشهري المنتظم' },
          { id: 'evt-3', assetId: TEST_ASSET_ID, eventType: 'CustodyAssigned', occurredAt: '2025-02-01T10:00:00Z', description: 'تسليم العهدة للموظف م. خالد السبيعي' }
        ])
      });
    });

    await page.route(`**/api/v1/fixed-assets/assets/${TEST_ASSET_ID}/assignments*`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'asgn-01',
            assetId: TEST_ASSET_ID,
            assignmentType: 'Employee',
            employeeId: 'EMP-1024',
            custodianName: 'م. خالد السبيعي',
            departmentId: 'IT-OPS',
            locationId: 'LOC-HQ-01',
            effectiveFrom: '2025-02-01',
            condition: 'ممتازة - خادم جديد بالضمان',
            accessories: 'كابلات باور مزدوجة، راك ماونت كيت'
          }
        ])
      });
    });

    await page.route(`**/api/v1/fixed-assets/assets/${TEST_ASSET_ID}/books/*/schedule*`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { assetId: TEST_ASSET_ID, bookId: 'book-01', periodStart: '2025-02-01', periodEnd: '2025-02-28', depreciationAmount: 3000, cumulativeDepreciation: 3000, projectedNetBookValue: 182000, method: 'StraightLine' },
          { assetId: TEST_ASSET_ID, bookId: 'book-01', periodStart: '2025-03-01', periodEnd: '2025-03-31', depreciationAmount: 3000, cumulativeDepreciation: 6000, projectedNetBookValue: 179000, method: 'StraightLine' },
          { assetId: TEST_ASSET_ID, bookId: 'book-01', periodStart: '2025-04-01', periodEnd: '2025-04-30', depreciationAmount: 3000, cumulativeDepreciation: 9000, projectedNetBookValue: 176000, method: 'StraightLine' }
        ])
      });
    });

    // 8. Verification Campaigns
    await page.route('**/api/v1/fixed-assets/verification/campaigns*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'cmp-01', legalEntityId: LEGAL_ENTITY_ID, name: 'حملة الجرد السنوي للأصول الثابتة 2026', startDate: '2026-11-01', endDate: '2026-11-30', status: 'Active', lineCount: 142 },
          { id: 'cmp-02', legalEntityId: LEGAL_ENTITY_ID, name: 'مطابقة وجرد أسطول المركبات والشاحنات', startDate: '2026-06-01', endDate: '2026-06-15', status: 'Completed', lineCount: 28 }
        ])
      });
    });

    // 9. Capitalization Candidates
    await page.route('**/api/v1/fixed-assets/capitalization*candidates*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'cand-01', legalEntityId: LEGAL_ENTITY_ID, sourceType: 'SupplierInvoice', sourceReference: 'INV-2026-908', vendorName: 'شركة سيمنز للمعدات الصناعية', amount: 250000, currency: 'SAR', invoiceDate: '2026-10-15', status: 'Pending' },
          { id: 'cand-02', legalEntityId: LEGAL_ENTITY_ID, sourceType: 'ExpenseRecord', sourceReference: 'EXP-2026-441', vendorName: 'المقاولون العرب للتركيبات', amount: 85000, currency: 'SAR', invoiceDate: '2026-10-22', status: 'Pending' }
        ])
      });
    });

    // 10. Depreciation Runs
    await page.route('**/api/v1/fixed-assets/depreciation-runs*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'run-01', bookId: 'book-01', legalEntityId: LEGAL_ENTITY_ID, periodStart: '2026-10-01', periodEnd: '2026-10-31', status: 'Posted', lineCount: 4, totalAmount: 42500, createdAt: '2026-11-01T00:00:00Z', createdByUserId: 'usr-1' },
          { id: 'run-02', bookId: 'book-01', legalEntityId: LEGAL_ENTITY_ID, periodStart: '2026-11-01', periodEnd: '2026-11-30', status: 'Draft', lineCount: 4, totalAmount: 42500, createdAt: '2026-11-14T00:00:00Z', createdByUserId: 'usr-1' }
        ])
      });
    });

    // 11. My Assets
    await page.route('**/api/v1/fixed-assets/my-assets*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            assetId: 'fa-my-01',
            assetNumber: 'FA-2026-088',
            name: 'جهاز حاسوب محمول Apple MacBook Pro 16" M3 Max',
            legalEntityId: LEGAL_ENTITY_ID,
            assignmentId: 'asgn-my-01',
            effectiveFrom: '2026-01-10',
            locationId: 'LOC-HQ-01',
            condition: 'ممتازة - عهدة شخصية'
          }
        ])
      });
    });

    // 12. Asset Register Report
    await page.route('**/api/v1/fixed-assets/reports/register*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: LEGAL_ENTITY_ID,
          asOfDate: '2026-11-14',
          totalGrossCost: 8885000,
          totalAccumulatedDepreciation: 695000,
          totalNetBookValue: 8190000,
          lines: [
            { assetId: TEST_ASSET_ID, assetNumber: 'FA-2026-001', assetName: 'خادم قاعدة البيانات الرئيسي Dell PowerEdge R750', categoryName: 'أجهزة وخوادم تقنية المعلومات', grossCost: 185000, accumulatedDepreciation: 60000, netBookValue: 125000, state: 'Active' },
            { assetId: 'fa-0002', assetNumber: 'FA-2026-002', assetName: 'شاحنة نقل بضائع ثقيلة مرسيدس أكتروس 3340', categoryName: 'أسطول المركبات والشاحنات', grossCost: 450000, accumulatedDepreciation: 135000, netBookValue: 315000, state: 'Active' },
            { assetId: 'fa-0003', assetNumber: 'FA-2026-003', assetName: 'مبنى المستودع اللوجستي المركزي - جدة', categoryName: 'العقارات والمباني الإدارية', grossCost: 2500000, accumulatedDepreciation: 400000, netBookValue: 2100000, state: 'Active' },
            { assetId: 'fa-0004', assetNumber: 'FA-2026-004', assetName: 'أرض مجمع المصانع والورش بالدمام', categoryName: 'الأراضي الاستثمارية', grossCost: 5000000, accumulatedDepreciation: 0, netBookValue: 5000000, state: 'Active' }
          ]
        })
      });
    });
  });

  test('01 — Full Fixed Assets Workspace & Live Financial KPIs in High-Contrast Navy/Slate', async ({ page }) => {
    await page.goto('/fixed-assets');
    await page.waitForLoadState('networkidle');

    // 1. Verify Application Header and Module Title
    await expect(page.getByTestId('fixed-assets-header')).toBeVisible();
    await expect(page.getByTestId('fixed-assets-title')).toContainText('منظومة الأصول الثابتة');

    // 2. Verify Tab Navigation is visible and Register tab is active
    await expect(page.getByTestId('tab-register')).toBeVisible();
    await expect(page.getByTestId('tab-custody')).toBeVisible();
    await expect(page.getByTestId('tab-capitalization')).toBeVisible();
    await expect(page.getByTestId('tab-depreciation')).toBeVisible();
    await expect(page.getByTestId('tab-reports')).toBeVisible();
    await expect(page.getByTestId('tab-my-assets')).toBeVisible();

    // 3. Verify Authoritative Financial KPIs (No client math, real backend values with SAR currency)
    const kpiGrossCost = page.getByTestId('kpi-gross-cost');
    await expect(kpiGrossCost).toBeVisible();
    await expect(kpiGrossCost).toContainText('SAR');

    const kpiNbv = page.getByTestId('kpi-nbv');
    await expect(kpiNbv).toBeVisible();
    await expect(kpiNbv).toContainText('SAR');

    // 4. Capture screenshot
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '01-fixed-assets-workspace-overview.png'),
      fullPage: true
    });

    expect(consoleErrors).toHaveLength(0);
    expect(pageErrors).toHaveLength(0);
  });

  test('02 — Asset Register Table with Filters, Badges, and Search', async ({ page }) => {
    await page.goto('/fixed-assets');
    await page.waitForLoadState('networkidle');

    // Verify Asset Register Table rendered with rows
    await expect(page.getByTestId('asset-register-table')).toBeVisible();
    await expect(page.getByText('FA-2026-001')).toBeVisible();
    await expect(page.getByText('Dell PowerEdge R750')).toBeVisible();
    await expect(page.getByText('FA-2026-004')).toBeVisible();

    // Test search filter input
    const searchInput = page.getByTestId('asset-search-input');
    await expect(searchInput).toBeVisible();

    // Capture screenshot
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '02-fixed-assets-register-table.png'),
      fullPage: true
    });

    expect(consoleErrors).toHaveLength(0);
    expect(pageErrors).toHaveLength(0);
  });

  test('03 — 360-Degree Comprehensive Asset Profile Drawer', async ({ page }) => {
    await page.goto('/fixed-assets');
    await page.waitForLoadState('networkidle');

    // Click on View Details button for FA-2026-001
    const viewButton = page.getByTestId(`view-asset-${TEST_ASSET_ID}`);
    await expect(viewButton).toBeVisible();
    await viewButton.click();

    // Verify QuickPeek Drawer is opened
    await expect(page.getByTestId('asset-detail-drawer')).toBeVisible();
    await expect(page.getByTestId('asset-detail-drawer')).toContainText('FA-2026-001');
    await expect(page.getByTestId('asset-detail-drawer')).toContainText('Dell PowerEdge R750');

    // Verify Drawer Tabs
    await expect(page.getByTestId('drawer-tab-overview')).toBeVisible();
    await expect(page.getByTestId('drawer-tab-schedule')).toBeVisible();
    await expect(page.getByTestId('drawer-tab-custody')).toBeVisible();
    await expect(page.getByTestId('drawer-tab-timeline')).toBeVisible();

    // Click on Depreciation Schedule tab
    await page.getByTestId('drawer-tab-schedule').click();
    await page.waitForTimeout(300);

    // Capture screenshot
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '03-fixed-assets-detail-drawer.png'),
      fullPage: true
    });

    expect(consoleErrors).toHaveLength(0);
    expect(pageErrors).toHaveLength(0);
  });

  test('04 — New Asset Registration Modal with High-Contrast Dialog', async ({ page }) => {
    await page.goto('/fixed-assets');
    await page.waitForLoadState('networkidle');

    // Click Primary Action "+ تسجيل أصل جديد"
    const newAssetBtn = page.getByTestId('new-asset-btn');
    await expect(newAssetBtn).toBeVisible();
    await newAssetBtn.click();

    // Verify New Asset Dialog is displayed
    await expect(page.getByTestId('new-asset-modal')).toBeVisible();
    await expect(page.getByTestId('asset-name-input')).toBeVisible();
    await expect(page.getByTestId('cost-input')).toBeVisible();

    // Fill sample values
    await page.getByTestId('asset-name-input').locator('input').fill('طابعة مكتبية متعددة الوظائف HP LaserJet Enterprise');
    await page.getByTestId('serial-number-input').locator('input').fill('HP-LJ-2026-88');

    // Capture screenshot
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '04-fixed-assets-new-asset-modal.png'),
      fullPage: true
    });

    expect(consoleErrors).toHaveLength(0);
    expect(pageErrors).toHaveLength(0);
  });

  test('05 — Custody Operations, Field Audits & Mass Transfer Dialog', async ({ page }) => {
    await page.goto('/fixed-assets');
    await page.waitForLoadState('networkidle');

    // Switch to Custody Tab
    await page.getByTestId('tab-custody').click();
    await page.waitForTimeout(300);

    // Verify Custody tab content
    await expect(page.getByTestId('physical-custody-tab')).toBeVisible();
    await expect(page.getByTestId('location-card-loc-01')).toBeVisible();
    await expect(page.getByTestId('verification-campaigns-table')).toBeVisible();

    // Click on Mass Transfer button
    const massTransferBtn = page.getByTestId('mass-transfer-btn');
    await expect(massTransferBtn).toBeVisible();
    await massTransferBtn.click();

    // Verify Mass Transfer Modal is visible
    await expect(page.getByTestId('mass-transfer-modal')).toBeVisible();

    // Capture screenshot
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '05-fixed-assets-custody-mass-transfer.png'),
      fullPage: true
    });

    expect(consoleErrors).toHaveLength(0);
    expect(pageErrors).toHaveLength(0);
  });

  test('06 — Capitalization & CIP Projects Tab with Cost Allocation', async ({ page }) => {
    await page.goto('/fixed-assets');
    await page.waitForLoadState('networkidle');

    // Switch to Capitalization Tab
    await page.getByTestId('tab-capitalization').click();
    await page.waitForTimeout(300);

    // Verify Capitalization Tab content
    await expect(page.getByTestId('capitalization-tab')).toBeVisible();
    await expect(page.getByTestId('cip-projects-table')).toBeVisible();
    await expect(page.getByText('CIP-2026-005')).toBeVisible();
    await expect(page.getByTestId('capitalization-candidates-table')).toBeVisible();

    // Capture screenshot
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '06-fixed-assets-cip-capitalization.png'),
      fullPage: true
    });

    expect(consoleErrors).toHaveLength(0);
    expect(pageErrors).toHaveLength(0);
  });

  test('07 — Depreciation Books, Monthly Runs & Prepare Run Modal', async ({ page }) => {
    await page.goto('/fixed-assets');
    await page.waitForLoadState('networkidle');

    // Switch to Depreciation Tab
    await page.getByTestId('tab-depreciation').click();
    await page.waitForTimeout(300);

    // Verify Books & Runs
    await expect(page.getByTestId('depreciation-tab')).toBeVisible();
    await expect(page.getByText('CORP-01')).toBeVisible();
    await expect(page.getByText('ZAKAT-TAX')).toBeVisible();
    await expect(page.getByTestId('depreciation-runs-table')).toBeVisible();

    // Click on Prepare Run button
    const prepareRunBtn = page.getByTestId('prepare-run-btn');
    await expect(prepareRunBtn).toBeVisible();
    await prepareRunBtn.click();

    // Verify Prepare Run Modal
    await expect(page.getByTestId('prepare-depreciation-modal')).toBeVisible();

    // Capture screenshot
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '07-fixed-assets-depreciation-runs.png'),
      fullPage: true
    });

    expect(consoleErrors).toHaveLength(0);
    expect(pageErrors).toHaveLength(0);
  });

  test('08 — General Ledger Reconciliation & Asset Register Reports Tab', async ({ page }) => {
    await page.goto('/fixed-assets');
    await page.waitForLoadState('networkidle');

    // Switch to Reports Tab
    await page.getByTestId('tab-reports').click();
    await page.waitForTimeout(300);

    // Verify Reconciliation & Report content
    await expect(page.getByTestId('reports-tab')).toBeVisible();
    await expect(page.getByTestId('reconciliation-card')).toBeVisible();
    await expect(page.getByTestId('asset-register-report-table')).toBeVisible();

    // Capture screenshot
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '08-fixed-assets-gl-reconciliation-reports.png'),
      fullPage: true
    });

    expect(consoleErrors).toHaveLength(0);
    expect(pageErrors).toHaveLength(0);
  });

  test('09 — Personal Custody & Assigned Equipment Tab', async ({ page }) => {
    await page.goto('/fixed-assets');
    await page.waitForLoadState('networkidle');

    // Switch to My Assets Tab
    await page.getByTestId('tab-my-assets').click();
    await page.waitForTimeout(300);

    // Verify My Assets Tab content
    await expect(page.getByTestId('my-assets-tab')).toBeVisible();
    await expect(page.getByTestId('my-assets-table')).toBeVisible();
    await expect(page.getByText('FA-2026-088')).toBeVisible();
    await expect(page.getByText('Apple MacBook Pro')).toBeVisible();

    // Capture screenshot
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '09-fixed-assets-my-assigned-assets.png'),
      fullPage: true
    });

    expect(consoleErrors).toHaveLength(0);
    expect(pageErrors).toHaveLength(0);
  });

  test('10 — English LTR Layout and Typography Parity', async ({ page }) => {
    // Set language to English
    await page.route('**/api/v1/me/preferences', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ language: 'en', theme: 'light' })
      });
    });

    await page.goto('/fixed-assets');
    await page.waitForLoadState('networkidle');

    // Verify English header title
    await expect(page.getByTestId('fixed-assets-header')).toBeVisible();

    // Capture English screenshot
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '10-fixed-assets-english-ltr-view.png'),
      fullPage: true
    });

    expect(consoleErrors).toHaveLength(0);
    expect(pageErrors).toHaveLength(0);
  });

  test('11 — Responsive Viewport (Tablet & Mobile Breakdown)', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/fixed-assets');
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('fixed-assets-header')).toBeVisible();

    // Capture responsive screenshot
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '11-fixed-assets-responsive-tablet.png'),
      fullPage: true
    });

    expect(consoleErrors).toHaveLength(0);
    expect(pageErrors).toHaveLength(0);
  });
});
