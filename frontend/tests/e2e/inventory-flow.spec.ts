import { test, expect } from '@playwright/test';

const MAIN_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const SAMPLE_PRODUCT_ID = 'a6315416-cffe-bf52-bcf0-d5e590d3bf94';

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
    reason: 'أقدم دفعة واردة (FIFO)'
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

const MOCK_COUNT_LINES = [
  {
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
    enteredAt: '2026-10-28T09:00:00Z'
  }
];

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
    // 01-inventory-overview.png
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/inventory');
    await expect(page.getByRole('heading', { name: 'المخزون المستودعي', level: 1 })).toBeVisible();
    await expect(page.getByText('إجمالي الرصيد الفعلي')).toBeVisible();
    await expect(page.getByText('290.00 EA').first()).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/01-inventory-overview.png',
      fullPage: true
    });

    // 02-stock-list.png
    await page.getByRole('tab', { name: 'أرصدة المخزون' }).click();
    await expect(page.getByRole('cell', { name: 'المستودع الرئيسي - الرياض' })).toBeVisible();
    await expect(page.getByText('225.00 EA')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/02-stock-list.png',
      fullPage: true
    });

    // 03-product-stock-card.png
    const cardBtn = page.getByRole('button', { name: 'بطاقة الصنف' }).first();
    await cardBtn.click();
    await expect(page.getByText('بطاقة حركة ورصيد الصنف')).toBeVisible();
    await expect(page.getByRole('dialog').getByText('المستودع الرئيسي - الرياض')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/03-product-stock-card.png'
    });

    // Close modal via Escape
    await page.keyboard.press('Escape');

    // 04-product-locator.png
    await page.getByRole('tab', { name: 'محدد مواقع الأصناف' }).click();
    await expect(page.getByText('ZONE-A / AISLE-01 / RACK-01 / A-01-01')).toBeVisible();
    await expect(page.getByText('مقترحات سحب الصنف (الالتقاط)')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/04-product-locator.png',
      fullPage: true
    });

    // 05-warehouses.png
    await page.getByRole('tab', { name: 'المستودعات والمواقع' }).click();
    await expect(page.getByText('WH-MAIN')).toBeVisible();
    await expect(page.getByText('WH-JED')).toBeVisible();
    await expect(page.getByText('مستودع مباشر (بدون مواقع داخلية)')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/05-warehouses.png',
      fullPage: true
    });

    // 06-hierarchical-locations.png (select WH-JED to view its 5 hierarchical locations)
    await page.getByRole('button', { name: 'مستودع التوزيع - جدة' }).click();
    await expect(page.getByText('ZONE-A')).toBeVisible();
    await expect(page.getByText('A-01-01')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/06-hierarchical-locations.png',
      fullPage: true
    });
  });

  test('07 to 10: Receive Stock, Issue Stock, Transfer In-Transit, Reservation', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/inventory');

    // 07-receive-stock.png
    await page.getByRole('button', { name: '+ استلام مخزني' }).first().click();
    await expect(page.getByRole('heading', { name: 'استلام مخزني جديد' })).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/07-receive-stock.png'
    });

    await page.keyboard.press('Escape');

    // 08-issue-stock.png
    await page.getByRole('button', { name: 'صرف مخزني' }).first().click();
    await expect(page.getByRole('heading', { name: 'صرف مخزني جديد' })).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/08-issue-stock.png'
    });

    await page.keyboard.press('Escape');

    // 09-transfer-in-transit.png
    await page.getByRole('tab', { name: 'التحويلات المستودعية' }).click();
    await page.getByRole('button', { name: '+ إنشاء تحويل جديد' }).click();
    await expect(page.getByRole('heading', { name: 'إنشاء تحويل جديد' })).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/09-transfer-in-transit.png'
    });

    await page.keyboard.press('Escape');

    // 10-reservation.png
    await page.getByRole('tab', { name: 'حجوزات المخزون' }).click();
    await page.getByRole('button', { name: '+ إنشاء حجز يدوي' }).click();
    await expect(page.getByRole('heading', { name: 'إنشاء حجز يدوي' })).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/10-reservation.png'
    });

    await page.keyboard.press('Escape');
  });

  test('11 to 15: Lot & Serial Traceability, Physical Count, Adjustments, Opening Inventory', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/inventory');

    // 11-lot-detail.png
    await page.getByRole('tab', { name: 'التشغيلات والأرقام التسلسلية' }).click();
    await page.getByRole('button', { name: '+ إضافة تشغيلة جديدة' }).click();
    await expect(page.getByRole('heading', { name: 'إضافة تشغيلة جديدة' })).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/11-lot-detail.png'
    });

    await page.keyboard.press('Escape');

    // 12-serial-detail.png (switch subtab to Serials)
    await page.getByRole('button', { name: /الأرقام التسلسلية للقطع/ }).click();
    await page.getByRole('button', { name: '+ تسجيل رقم تسلسلي جديد' }).click();
    await expect(page.getByRole('heading', { name: 'تسجيل رقم تسلسلي جديد' })).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/12-serial-detail.png'
    });

    await page.keyboard.press('Escape');

    // 13-physical-count.png
    await page.getByRole('tab', { name: 'الجرد الفعلي' }).click();
    await expect(page.getByText('COUNT-SEED-001')).toBeVisible();

    // Open count detail modal
    await page.getByRole('button', { name: 'إدخال نتائج العد' }).first().click();
    await expect(page.getByRole('dialog').getByRole('heading', { name: 'جلسات الجرد الفعلي للمستودع' })).toBeVisible();
    await expect(page.getByText(SAMPLE_PRODUCT_ID)).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/13-physical-count.png'
    });

    await page.keyboard.press('Escape');

    // 14-adjustment-approval.png
    await page.getByRole('tab', { name: 'التسويات المخزنية' }).click();
    await page.getByRole('button', { name: '+ إجراء تسوية جديدة' }).click();
    await expect(page.getByRole('heading', { name: 'إجراء تسوية جديدة' })).toBeVisible();
    await expect(page.getByText('يلزم اعتماد إداري مع فصل المهام')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/14-adjustment-approval.png'
    });

    await page.keyboard.press('Escape');

    // 15-opening-inventory.png
    await page.getByRole('tab', { name: 'رصيد أول المدة' }).click();
    await page.getByRole('button', { name: '+ تسجيل رصيد بداية المدة' }).click();
    await expect(page.getByRole('heading', { name: 'تسجيل رصيد بداية المدة' })).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/15-opening-inventory.png'
    });

    await page.keyboard.press('Escape');
  });

  test('16 to 19: Movement History, Operation Detail, Valuation, GL Reconciliation', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/inventory');

    // 16-movement-history.png
    await page.getByRole('tab', { name: 'سجل الحركات الدائم' }).click();
    await expect(page.getByText('Receipt')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/16-movement-history.png',
      fullPage: true
    });

    // 17-operation-detail.png
    await page.getByRole('button', { name: 'تفاصيل العملية' }).first().click();
    await expect(page.getByRole('heading', { name: /تفاصيل العملية المخزنية/ })).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/17-operation-detail.png'
    });

    await page.keyboard.press('Escape');

    // 18-valuation.png
    await page.getByRole('tab', { name: 'التقييم ومطابقة الأستاذ العام' }).click();
    await expect(page.getByText('189,450.00').first()).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/18-valuation.png',
      fullPage: true
    });

    // 19-inventory-gl-reconciliation.png
    await expect(page.getByText('مطابق تماماً')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/19-inventory-gl-reconciliation.png',
      fullPage: true
    });
  });

  test('20 to 24: Responsive Viewports (390 Mobile, 768 Tablet, 1024 Desktop, 1280 Overview)', async ({ page }) => {
    // 20-mobile-product-locator-390.png
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/inventory');
    await page.getByRole('tab', { name: 'محدد مواقع الأصناف' }).click();
    await expect(page.getByText('ZONE-A / AISLE-01 / RACK-01 / A-01-01')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/20-mobile-product-locator-390.png',
      fullPage: true
    });

    // 21-mobile-receive-390.png
    await page.getByRole('tab', { name: 'نظرة عامة والمؤشرات' }).click();
    await page.getByRole('button', { name: '+ استلام مخزني' }).first().click();
    await expect(page.getByRole('heading', { name: 'استلام مخزني جديد' })).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/21-mobile-receive-390.png'
    });

    await page.keyboard.press('Escape');

    // 22-tablet-stock-768.png
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/inventory');
    await page.getByRole('tab', { name: 'أرصدة المخزون' }).click();
    await expect(page.getByText('225.00 EA')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/22-tablet-stock-768.png',
      fullPage: true
    });

    // 23-desktop-stock-1024.png
    await page.setViewportSize({ width: 1024, height: 768 });
    await page.goto('/inventory');
    await page.getByRole('tab', { name: 'أرصدة المخزون' }).click();
    await expect(page.getByText('225.00 EA')).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/23-desktop-stock-1024.png',
      fullPage: true
    });

    // 24-desktop-overview-1280.png
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/inventory');
    await expect(page.getByRole('heading', { name: 'المخزون المستودعي', level: 1 })).toBeVisible();

    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/24-desktop-overview-1280.png',
      fullPage: true
    });
  });
});
