import { test, expect, Page } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';

const MAIN_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const SAMPLE_PRODUCT_ID = 'a6315416-cffe-bf52-bcf0-d5e590d3bf94';

async function captureScreenshot(page: Page, filename: string, options?: { fullPage?: boolean }) {
  const primaryPath = path.resolve(process.cwd(), 'artifacts/test-results/visual-acceptance', filename);
  const rootPath = path.resolve(process.cwd(), '../artifacts/test-results/visual-acceptance', filename);
  fs.mkdirSync(path.dirname(primaryPath), { recursive: true });
  fs.mkdirSync(path.dirname(rootPath), { recursive: true });
  await page.screenshot({ path: primaryPath, ...options });
  try {
    fs.copyFileSync(primaryPath, rootPath);
  } catch {
    // Ignore if root path is identical or inaccessible
  }
}

async function selectInventoryTab(page: Page, workspaceLabel: string, tabLabel: string) {
  await page.getByRole('tab').filter({ hasText: workspaceLabel }).first().click();
  await page.getByRole('tab', { name: tabLabel, exact: true }).click();
}

const MOCK_WAREHOUSES = [
  {
    id: 'wh-01',
    legalEntityId: MAIN_ENTITY_ID,
    code: 'WH-MAIN',
    name: 'المستودع الرئيسي - الرياض',
    locationMode: 'WarehouseOnly',
    trackingMode: 'WarehouseOnly',
    status: 'Active',
    isActive: true,
    locationsCount: 0
  },
  {
    id: 'wh-02',
    legalEntityId: MAIN_ENTITY_ID,
    code: 'WH-JED',
    name: 'مستودع التوزيع - جدة',
    locationMode: 'HierarchicalLocations',
    trackingMode: 'HierarchicalLocations',
    status: 'Active',
    isActive: true,
    locationsCount: 5
  }
];

const MOCK_LOCATIONS_WH2 = [
  {
    id: 'loc-01',
    warehouseId: 'wh-02',
    code: 'ZONE-A',
    name: 'المنطقة أ - بضائع جافة',
    locationType: 'Zone',
    parentLocationId: null,
    canHoldStock: false,
    canReceiveInto: false,
    canPickFrom: false,
    isActive: true
  },
  {
    id: 'loc-02',
    warehouseId: 'wh-02',
    code: 'AISLE-01',
    name: 'الممر 1',
    parentLocationId: 'loc-01',
    locationType: 'Aisle',
    canHoldStock: false,
    canReceiveInto: false,
    canPickFrom: false,
    isActive: true
  },
  {
    id: 'loc-03',
    warehouseId: 'wh-02',
    code: 'RACK-01',
    name: 'الحامل 1',
    parentLocationId: 'loc-02',
    locationType: 'Rack',
    canHoldStock: false,
    canReceiveInto: false,
    canPickFrom: false,
    isActive: true
  },
  {
    id: 'loc-04',
    warehouseId: 'wh-02',
    code: 'A-01-01',
    name: 'الرف 1 الخانة 1',
    parentLocationId: 'loc-03',
    locationType: 'Bin',
    barcode: '62810010001',
    canHoldStock: true,
    canReceiveInto: true,
    canPickFrom: true,
    isActive: true
  },
  {
    id: 'loc-05',
    warehouseId: 'wh-02',
    code: 'A-01-02',
    name: 'الرف 1 الخانة 2',
    parentLocationId: 'loc-03',
    locationType: 'Bin',
    barcode: '62810010002',
    canHoldStock: true,
    canReceiveInto: true,
    canPickFrom: true,
    isActive: true
  }
];

const MOCK_POLICY = {
  legalEntityId: MAIN_ENTITY_ID,
  adjustmentApprovalMode: 'ApprovalRequired',
  separationOfDuties: true,
  operationalCutoffDate: null,
  version: 1
};

const MOCK_BALANCES = [
  {
    productVariantId: SAMPLE_PRODUCT_ID,
    warehouseId: 'wh-01',
    locationId: 'loc-main',
    stockStatus: 'Available',
    onHandBaseQuantity: 250,
    reservedBaseQuantity: 25,
    availableBaseQuantity: 225,
    version: 1,
    lastMovementSequence: 10
  },
  {
    productVariantId: 'SKU-ELEC-2002',
    warehouseId: 'wh-02',
    locationId: 'loc-04',
    stockStatus: 'Available',
    onHandBaseQuantity: 40,
    reservedBaseQuantity: 0,
    availableBaseQuantity: 40,
    version: 1,
    lastMovementSequence: 5
  }
];

const MOCK_LOCATOR_ITEMS = [
  {
    productVariantId: SAMPLE_PRODUCT_ID,
    warehouseId: 'wh-02',
    warehouseCode: 'WH-JED',
    locationId: 'loc-04',
    locationPath: 'ZONE-A / AISLE-01 / RACK-01 / A-01-01',
    stockStatus: 'Available',
    lotId: 'LOT-2026-OCT',
    serialId: null,
    onHand: 100,
    reserved: 0,
    available: 100
  },
  {
    productVariantId: SAMPLE_PRODUCT_ID,
    warehouseId: 'wh-01',
    warehouseCode: 'WH-MAIN',
    locationId: 'loc-main',
    locationPath: 'DEFAULT',
    stockStatus: 'Available',
    lotId: null,
    serialId: null,
    onHand: 150,
    reserved: 25,
    available: 125
  }
];

const MOCK_PUTAWAY = {
  warehouseId: 'wh-02',
  locationId: 'loc-04',
  locationCode: 'A-01-01',
  reason: 'أقرب رف متاح للصنف'
};

const MOCK_PICKING = [
  {
    warehouseId: 'wh-02',
    locationId: 'loc-04',
    locationCode: 'A-01-01',
    availableQuantity: 50,
    reason: 'أقدم دفعة واردة'
  }
];

const MOCK_HISTORY = {
  items: [
    {
      id: 'mov-001',
      operationId: 'OP-2026-0045-RECEIVE',
      sequence: 1,
      productVariantId: SAMPLE_PRODUCT_ID,
      warehouseId: 'wh-01',
      locationId: 'loc-main',
      stockStatus: 'Available',
      lotId: 'LOT-2026-OCT',
      serialId: null,
      enteredQuantity: 100,
      enteredUomId: 'EA',
      conversionFactorSnapshot: 1,
      baseQuantity: 100,
      signedBaseQuantity: 100,
      movementType: 'Receipt',
      sourceModule: 'Purchasing',
      sourceDocumentId: 'PO-2026-0045',
      operationalDate: '2026-10-28',
      postingDate: '2026-10-28',
      effectiveAt: '2026-10-28T10:30:00Z'
    },
    {
      id: 'mov-002',
      operationId: 'OP-2026-0078-ISSUE',
      sequence: 2,
      productVariantId: SAMPLE_PRODUCT_ID,
      warehouseId: 'wh-01',
      locationId: 'loc-main',
      stockStatus: 'Available',
      lotId: 'LOT-2026-OCT',
      serialId: null,
      enteredQuantity: 20,
      enteredUomId: 'EA',
      conversionFactorSnapshot: 1,
      baseQuantity: 20,
      signedBaseQuantity: -20,
      movementType: 'Issue',
      sourceModule: 'Sales',
      sourceDocumentId: 'SO-2026-0782',
      operationalDate: '2026-10-28',
      postingDate: '2026-10-28',
      effectiveAt: '2026-10-28T11:45:00Z'
    }
  ],
  hasMore: false
};

const MOCK_VALUATION = {
  productVariantId: SAMPLE_PRODUCT_ID,
  quantity: 250,
  carryingValue: 189450.00,
  currencyCode: 'SAR',
  costingMethod: 'MovingAverage',
  asOfDate: '2026-10-28'
};

const MOCK_RECONCILIATION = {
  legalEntityId: MAIN_ENTITY_ID,
  asOfDate: '2026-10-28',
  inventoryValue: 189450.00,
  ledgerValue: 189450.00,
  difference: 0.00,
  currencyCode: 'SAR',
  status: 'Matched',
  diagnostics: []
};

const MOCK_COUNT_LINES = {
  items: [{
    id: 'line-1',
    productVariantId: SAMPLE_PRODUCT_ID,
    warehouseId: 'wh-01',
    locationId: 'loc-01',
    stockStatus: 'Available',
    lotId: 'LOT-2026-OCT',
    expected: 100,
    counted: 99,
    difference: -1,
    enteredBy: 'أمين المستودع',
    enteredAt: '2026-10-28T09:00:00Z',
    productDisplayName: 'سائل تنظيف أرضيات 5 لتر',
    productSku: 'SKU-CLEAN-005',
    baseUomCode: 'EA'
  }],
  totalCount: 1,
  page: 1,
  pageSize: 25
};

test.describe('TODIR Inventory — Complete Frontend E2E & Visual Acceptance Suite', () => {
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
                countryCode: 'SA',
                baseCurrencyCode: 'SAR'
              }
            ],
            branches: []
          },
          permissions: [
            'inventory.stock.read',
            'inventory.history.read',
            'inventory.cost.read',
            'inventory.valuation.read',
            'inventory.reconciliation.read',
            'inventory.receipt.confirm',
            'inventory.issue.confirm',
            'inventory.transfer.create',
            'inventory.transfer.ship',
            'inventory.transfer.receive',
            'inventory.reservation.create',
            'inventory.reservation.release',
            'inventory.count.create',
            'inventory.count.enter',
            'inventory.count.post',
            'inventory.adjustment.create',
            'inventory.adjustment.approve',
            'inventory.adjustment.post',
            'inventory.warehouse.manage',
            'inventory.location.manage',
            'inventory.profile.manage',
            'inventory.policy.manage',
            'inventory.traceability.manage',
            'inventory.opening.post',
            'inventory.stock.status.change',
            'inventory.operation.reverse',
            'inventory.stock.negative.override'
          ]
        })
      });
    });

    // Authorization check
    await page.route('**/api/v1/authorization/check*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ allowed: true })
      });
    });

    // Modules
    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ id: 'home' }, { id: 'inventory' }, { id: 'settings' }])
      });
    });

    // Inventory mock endpoints
    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/warehouses`), async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'wh-new',
            legalEntityId: MAIN_ENTITY_ID,
            code: body.code || 'WH-NEW',
            name: body.name || 'مستودع جديد',
            trackingMode: body.locationTrackingMode || 'WarehouseOnly',
            isActive: true,
            locationsCount: 0
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_WAREHOUSES)
        });
      }
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/warehouses/wh-02/locations`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_LOCATIONS_WH2)
      });
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/warehouses/wh-01/locations`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/policy`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_POLICY)
      });
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/negative-stock-conditions`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/balances`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_BALANCES)
      });
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/products/${SAMPLE_PRODUCT_ID}/locator`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_LOCATOR_ITEMS)
      });
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/products/${SAMPLE_PRODUCT_ID}/putaway-suggestion`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_PUTAWAY)
      });
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/products/${SAMPLE_PRODUCT_ID}/picking-suggestions`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_PICKING)
      });
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/history`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_HISTORY)
      });
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/products/${SAMPLE_PRODUCT_ID}/valuation`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_VALUATION)
      });
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/reconciliation`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_RECONCILIATION)
      });
    });

    await page.route(new RegExp(`/api/v1/inventory/legal-entities/${MAIN_ENTITY_ID}/counts/cnt-seed-001/lines`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_COUNT_LINES)
      });
    });
  });

  test('01 to 06: Overview, Stock Balances, Stock Card, Locator, Warehouses & Hierarchy', async ({ page }) => {
    // 01-inventory-overview-final.png
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/inventory');
    await expect(page.getByRole('heading', { name: 'المخزون المستودعي', level: 1 })).toBeVisible();
    await expect(page.getByText('أصناف برصيد فعلي')).toBeVisible();
    await expect(page.getByText('أصناف بها حجوزات')).toBeVisible();

    await captureScreenshot(page, '01-inventory-overview-final.png', { fullPage: true });

    // 02-stock-list-final.png
    await selectInventoryTab(page, 'المخزون', 'أرصدة المخزون');
    await expect(page.getByRole('cell', { name: 'المستودع الرئيسي - الرياض' })).toBeVisible();
    await expect(page.getByText('سائل تنظيف أرضيات 5 لتر').first()).toBeVisible();
    await expect(page.getByText('SKU-CLEAN-005').first()).toBeVisible();
    await expect(page.getByText('225.00 EA')).toBeVisible();

    await captureScreenshot(page, '02-stock-list-final.png', { fullPage: true });

    // 03-product-stock-card-final.png
    const cardBtn = page.getByRole('button', { name: 'بطاقة الصنف' }).first();
    await cardBtn.click();
    await expect(page.getByText('بطاقة حركة ورصيد الصنف')).toBeVisible();
    await expect(page.getByRole('dialog').getByText('سائل تنظيف أرضيات 5 لتر')).toBeVisible();
    await expect(page.getByRole('dialog').getByText('المستودع الرئيسي - الرياض').first()).toBeVisible();

    await captureScreenshot(page, '03-product-stock-card-final.png');

    // Close modal via Escape
    await page.keyboard.press('Escape');

    // 04-product-locator-final.png & product-locator-final-2.png
    await selectInventoryTab(page, 'المخزون', 'محدد مواقع الأصناف');
    await expect(page.getByText('ZONE-A / AISLE-01 / RACK-01 / A-01-01')).toBeVisible();
    await expect(page.getByText('بدون موقع تخزين داخلي')).toBeVisible();
    await expect(page.getByText('مقترحات سحب الصنف (الالتقاط)')).toBeVisible();
    await expect(page.getByText('سائل تنظيف أرضيات 5 لتر').first()).toBeVisible();

    await captureScreenshot(page, '04-product-locator-final.png', { fullPage: true });
    await captureScreenshot(page, 'product-locator-final-2.png', { fullPage: true });

    // 05-warehouses
    await selectInventoryTab(page, 'السجل والتهيئة', 'المستودعات والمواقع');
    await expect(page.getByText('WH-MAIN')).toBeVisible();
    await expect(page.getByText('WH-JED')).toBeVisible();
    await expect(page.getByText('مستودع مباشر بدون مواقع داخلية')).toBeVisible();

    // 06-hierarchical-locations (select WH-JED to view its 5 hierarchical locations)
    await page.getByRole('button', { name: 'مستودع التوزيع - جدة' }).click();
    await expect(page.getByText('ZONE-A')).toBeVisible();
    await expect(page.getByText('A-01-01')).toBeVisible();
  });

  test('07 to 10: Receive Stock, Issue Stock, Transfer In-Transit, Reservation', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/inventory');

    // 07-receive-stock-final.png
    await page.getByRole('button', { name: '+ استلام مخزني' }).first().click();
    await expect(page.getByRole('heading', { name: 'استلام مخزني جديد' })).toBeVisible();
    await expect(page.getByRole('dialog').getByRole('button', { name: 'فتح نافذة البحث المتقدم في الكتالوج' })).toBeVisible();

    await captureScreenshot(page, '07-receive-stock-final.png');

    await page.keyboard.press('Escape');

    // 08-issue-stock-final.png
    await page.getByRole('button', { name: 'صرف مخزني' }).first().click();
    await expect(page.getByRole('heading', { name: 'صرف مخزني جديد' })).toBeVisible();
    await expect(page.getByRole('dialog').getByRole('button', { name: 'فتح نافذة البحث المتقدم في الكتالوج' })).toBeVisible();

    await captureScreenshot(page, '08-issue-stock-final.png');

    await page.keyboard.press('Escape');

    // 09-transfer-final.png
    await selectInventoryTab(page, 'العمليات المخزنية', 'التحويلات المستودعية');
    await page.getByRole('button', { name: '+ إنشاء تحويل جديد' }).click();
    await expect(page.getByRole('heading', { name: 'إنشاء تحويل جديد' })).toBeVisible();
    await expect(page.getByRole('dialog').getByText('سائل تنظيف أرضيات 5 لتر')).toBeVisible();

    await captureScreenshot(page, '09-transfer-final.png');

    await page.keyboard.press('Escape');

    // 10-reservation-final.png
    await selectInventoryTab(page, 'العمليات المخزنية', 'حجوزات المخزون');
    await page.getByRole('button', { name: '+ إنشاء حجز يدوي' }).click();
    await expect(page.getByRole('heading', { name: 'إنشاء حجز يدوي' })).toBeVisible();
    await expect(page.getByRole('dialog').getByText('سائل تنظيف أرضيات 5 لتر')).toBeVisible();

    await captureScreenshot(page, '10-reservation-final.png');

    await page.keyboard.press('Escape');
  });

  test('11 to 15: Lot & Serial Traceability, Physical Count, Adjustments, Opening Inventory', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/inventory');

    // 11-lot-detail-final.png
    await selectInventoryTab(page, 'الجرد والتتبع', 'التشغيلات والأرقام التسلسلية');
    await expect(page.getByText('LOT-2026-MED-01')).toBeVisible();
    // Open Lot Detail Modal via "عرض التفاصيل" button
    await page.getByRole('button', { name: 'عرض التفاصيل' }).first().click();
    await expect(page.getByRole('heading', { name: 'تفاصيل التشغيلة والصلاحية' })).toBeVisible();
    await expect(page.getByRole('dialog').getByText('LOT-2026-MED-01')).toBeVisible();
    await expect(page.getByRole('dialog').getByText('سائل تنظيف أرضيات 5 لتر')).toBeVisible();
    await expect(page.getByRole('dialog').getByText('توزيع المخزون بالمستودعات والمواقع')).toBeVisible();

    await captureScreenshot(page, '11-lot-detail-final.png');

    await page.keyboard.press('Escape');

    // 12-serial-detail-final.png & serial-detail-final-2.png (switch subtab to Serials)
    await page.getByRole('button', { name: /الأرقام التسلسلية للقطع/ }).click();
    await expect(page.getByText('SN-90081-A')).toBeVisible();
    // Open Serial Detail Modal via "عرض التفاصيل" button
    await page.getByRole('button', { name: 'عرض التفاصيل' }).first().click();
    await expect(page.getByRole('heading', { name: 'تفاصيل الرقم التسلسلي' })).toBeVisible();
    await expect(page.getByRole('dialog').getByText('SN-90081-A')).toBeVisible();
    await expect(page.getByRole('dialog').getByText('سائل تنظيف أرضيات 5 لتر')).toBeVisible();
    await expect(page.getByRole('dialog').getByText('تم الاستلام')).toBeVisible();
    await expect(page.getByRole('dialog').getByText('سجل الحركات والتتبع')).toBeVisible();

    await captureScreenshot(page, '12-serial-detail-final.png');
    await captureScreenshot(page, 'serial-detail-final-2.png');

    await page.keyboard.press('Escape');

    // 13-physical-count-final.png
    await selectInventoryTab(page, 'الجرد والتتبع', 'الجرد الفعلي');
    await expect(page.getByText('COUNT-SEED-001')).toBeVisible();

    // Open count detail modal
    await page.getByRole('button', { name: 'إدخال نتائج العد' }).first().click();
    await expect(page.getByRole('dialog').getByRole('heading', { name: 'إدخال نتائج الجرد' })).toBeVisible();
    await expect(page.getByRole('dialog').getByText('سائل تنظيف أرضيات 5 لتر')).toBeVisible();
    await expect(page.getByRole('dialog').getByText('SKU-CLEAN-005')).toBeVisible();

    await captureScreenshot(page, '13-physical-count-final.png');

    await page.keyboard.press('Escape');

    // adjustment-approval-final-3.png (shows full lifecycle: Draft, PendingApproval, Approved, Posted)
    await selectInventoryTab(page, 'الجرد والتتبع', 'التسويات المخزنية');
    await expect(page.getByText('ADJ-2026-004')).toBeVisible();
    await expect(page.getByText('مسودة', { exact: true })).toBeVisible();
    await expect(page.getByText('بانتظار الاعتماد', { exact: true })).toBeVisible();
    await expect(page.getByText('معتمد', { exact: true })).toBeVisible();
    await expect(page.getByText('مرحل', { exact: true })).toBeVisible();
    await expect(page.getByText('أحمد المحاسب').first()).toBeVisible();

    // Ensure status column is fully visible without any edge clipping
    await page.getByText('بانتظار الاعتماد', { exact: true }).first().scrollIntoViewIfNeeded();

    await captureScreenshot(page, '14-adjustment-approval-final.png', { fullPage: true });
    await captureScreenshot(page, 'adjustment-approval-final-2.png', { fullPage: true });
    await captureScreenshot(page, 'adjustment-approval-final-3.png', { fullPage: true });

    // 15-opening-inventory-final.png
    await selectInventoryTab(page, 'السجل والتهيئة', 'رصيد أول المدة');
    await page.getByRole('button', { name: '+ تسجيل رصيد بداية المدة' }).click();
    await expect(page.getByRole('heading', { name: 'تسجيل رصيد بداية المدة' })).toBeVisible();
    await expect(page.getByRole('dialog').getByText('سائل تنظيف أرضيات 5 لتر')).toBeVisible();

    await captureScreenshot(page, '15-opening-inventory-final.png');

    await page.keyboard.press('Escape');
  });

  test('16 to 19: Movement History, Operation Detail, Valuation, GL Reconciliation', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/inventory');

    // 16-movement-history-final.png & movement-history-final-2.png
    await selectInventoryTab(page, 'السجل والتهيئة', 'سجل الحركات الدائم');
    await expect(page.getByText('استلام مخزني').first()).toBeVisible();
    await expect(page.getByText('صرف مخزني').first()).toBeVisible();
    await expect(page.getByText('سائل تنظيف أرضيات 5 لتر').first()).toBeVisible();

    await captureScreenshot(page, '16-movement-history-final.png', { fullPage: true });
    await captureScreenshot(page, 'movement-history-final-2.png', { fullPage: true });

    // 17-operation-detail-final.png
    await page.getByRole('button', { name: 'تفاصيل العملية' }).first().click();
    await expect(page.getByRole('heading', { name: /تفاصيل العملية المخزنية/ })).toBeVisible();
    await expect(page.getByRole('dialog').getByText('سائل تنظيف أرضيات 5 لتر')).toBeVisible();

    await captureScreenshot(page, '17-operation-detail-final.png');

    await page.keyboard.press('Escape');

    // 18-valuation-final.png
    await selectInventoryTab(page, 'السجل والتهيئة', 'التقييم ومطابقة الأستاذ العام');
    await expect(page.getByText('المتوسط المرجح المتحرك').first()).toBeVisible();
    await expect(page.getByText('189,450.00').first()).toBeVisible();
    await expect(page.getByText('سائل تنظيف أرضيات 5 لتر').first()).toBeVisible();

    await captureScreenshot(page, '18-valuation-final.png', { fullPage: true });

    // 19-inventory-gl-reconciliation-final.png
    await expect(page.getByText('مطابق تماماً')).toBeVisible();

    await captureScreenshot(page, '19-inventory-gl-reconciliation-final.png', { fullPage: true });
  });

  test('20 to 24: Responsive Viewports (390 Mobile, 768 Tablet, 1024 Desktop, 1280 Overview)', async ({ page }) => {
    // 20-mobile-product-locator-390.png
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/inventory');
    await selectInventoryTab(page, 'المخزون', 'محدد مواقع الأصناف');
    await expect(page.getByText('ZONE-A / AISLE-01 / RACK-01 / A-01-01')).toBeVisible();

    await captureScreenshot(page, '20-mobile-product-locator-390.png', { fullPage: true });

    // 21-mobile-receive-390.png
    await selectInventoryTab(page, 'المخزون', 'نظرة عامة والمؤشرات');
    await page.getByRole('button', { name: '+ استلام مخزني' }).first().click();
    await expect(page.getByRole('heading', { name: 'استلام مخزني جديد' })).toBeVisible();

    await captureScreenshot(page, '21-mobile-receive-390.png');

    await page.keyboard.press('Escape');

    // 22-tablet-stock-768.png
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/inventory');
    await selectInventoryTab(page, 'المخزون', 'أرصدة المخزون');
    await expect(page.getByText('225.00 EA')).toBeVisible();

    await captureScreenshot(page, '22-tablet-stock-768.png', { fullPage: true });

    // 23-desktop-stock-1024.png
    await page.setViewportSize({ width: 1024, height: 768 });
    await page.goto('/inventory');
    await selectInventoryTab(page, 'المخزون', 'أرصدة المخزون');
    await expect(page.getByText('225.00 EA')).toBeVisible();

    await captureScreenshot(page, '23-desktop-stock-1024.png', { fullPage: true });

    // 24-desktop-overview-1280.png
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/inventory');
    await expect(page.getByRole('heading', { name: 'المخزون المستودعي', level: 1 })).toBeVisible();
    await expect(page.getByText('أصناف برصيد فعلي')).toBeVisible();

    await captureScreenshot(page, '24-desktop-overview-1280.png', { fullPage: true });
  });
});
