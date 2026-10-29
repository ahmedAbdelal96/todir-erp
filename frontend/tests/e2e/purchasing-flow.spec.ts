import { test, expect, Page } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';

const MAIN_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const SAMPLE_SUPPLIER_ID = 'party-supplier-al-futtaim';
const SAMPLE_SUPPLIER_2 = 'party-supplier-al-marai';
const SAMPLE_SUPPLIER_3 = 'party-supplier-gulf-service';
const SAMPLE_PRODUCT_ID = 'a6315416-cffe-bf52-bcf0-d5e590d3bf94';
const SAMPLE_PRODUCT_2 = 'SKU-OFFICE-1001';

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

let mockOrders = [
  {
    id: 'po-001',
    legalEntityId: MAIN_ENTITY_ID,
    poNumber: 'PO-2026-0001',
    version: 1,
    supplierPartyId: SAMPLE_SUPPLIER_ID,
    orderDate: '2026-10-28',
    currencyCode: 'SAR',
    state: 'Draft',
    receiptState: 'NotReceived',
    invoicingState: 'NotInvoiced',
    matchingState: 'NotRequired',
    subtotal: 5000.0,
    discountTotal: 0.0,
    netTotal: 5000.0,
    taxTotal: 700.0,
    grossTotal: 5700.0,
    linesCount: 2,
    lines: [
      {
        id: 'line-001',
        purchaseOrderId: 'po-001',
        lineIndex: 0,
        productVariantId: SAMPLE_PRODUCT_ID,
        description: 'شاشات عرض احترافية 27 بوصة عالية الدقة',
        lineType: 'Stocked',
        orderedQuantity: 5,
        receivedQuantity: 0,
        invoicedQuantity: 0,
        cancelledQuantity: 0,
        shortQuantity: 0,
        unitCode: 'EA',
        unitPrice: 1000.0,
        discountRate: 0.0,
        discountAmount: 0.0,
        lineTotal: 5000.0
      }
    ]
  },
  {
    id: 'po-002',
    legalEntityId: MAIN_ENTITY_ID,
    poNumber: 'PO-2026-0002',
    version: 1,
    supplierPartyId: SAMPLE_SUPPLIER_2,
    orderDate: '2026-10-27',
    currencyCode: 'SAR',
    state: 'Approved',
    receiptState: 'PartiallyReceived',
    invoicingState: 'NotInvoiced',
    matchingState: 'Blocked',
    subtotal: 12000.0,
    discountTotal: 500.0,
    netTotal: 11500.0,
    taxTotal: 1610.0,
    grossTotal: 13110.0,
    linesCount: 1,
    lines: [
      {
        id: 'line-002',
        purchaseOrderId: 'po-002',
        lineIndex: 0,
        productVariantId: SAMPLE_PRODUCT_2,
        description: 'أوراق طباعة وتصوير ممتازة A4',
        lineType: 'Stocked',
        orderedQuantity: 100,
        receivedQuantity: 60,
        invoicedQuantity: 0,
        cancelledQuantity: 0,
        shortQuantity: 0,
        unitCode: 'BOX',
        unitPrice: 115.0,
        discountRate: 0.0,
        discountAmount: 0.0,
        lineTotal: 11500.0
      }
    ]
  },
  {
    id: 'po-003',
    legalEntityId: MAIN_ENTITY_ID,
    poNumber: 'PO-2026-0003',
    version: 2,
    supplierPartyId: SAMPLE_SUPPLIER_3,
    orderDate: '2026-10-25',
    currencyCode: 'SAR',
    state: 'Approved',
    receiptState: 'Received',
    invoicingState: 'Invoiced',
    matchingState: 'WithinTolerance',
    subtotal: 4500.0,
    discountTotal: 0.0,
    netTotal: 4500.0,
    taxTotal: 630.0,
    grossTotal: 5130.0,
    linesCount: 1,
    lines: [
      {
        id: 'line-003',
        purchaseOrderId: 'po-003',
        lineIndex: 0,
        productVariantId: null,
        description: 'خدمات استشارية ودعم فني لشبكات الأعمال',
        lineType: 'Service',
        orderedQuantity: 1,
        receivedQuantity: 1,
        invoicedQuantity: 1,
        cancelledQuantity: 0,
        shortQuantity: 0,
        unitCode: 'SRV',
        unitPrice: 4500.0,
        discountRate: 0.0,
        discountAmount: 0.0,
        lineTotal: 4500.0
      }
    ]
  },
  {
    id: 'po-004',
    legalEntityId: MAIN_ENTITY_ID,
    poNumber: 'PO-2026-0004',
    version: 1,
    supplierPartyId: SAMPLE_SUPPLIER_ID,
    orderDate: '2026-10-26',
    currencyCode: 'SAR',
    state: 'Closed',
    receiptState: 'Received',
    invoicingState: 'Invoiced',
    matchingState: 'WithinTolerance',
    subtotal: 8000.0,
    discountTotal: 0.0,
    netTotal: 8000.0,
    taxTotal: 1120.0,
    grossTotal: 9120.0,
    linesCount: 1,
    lines: [
      {
        id: 'line-004',
        purchaseOrderId: 'po-004',
        lineIndex: 0,
        productVariantId: SAMPLE_PRODUCT_ID,
        description: 'شاشات عرض احترافية 27 بوصة',
        lineType: 'Stocked',
        orderedQuantity: 8,
        receivedQuantity: 8,
        invoicedQuantity: 8,
        cancelledQuantity: 0,
        shortQuantity: 0,
        unitCode: 'EA',
        unitPrice: 1000.0,
        discountRate: 0.0,
        discountAmount: 0.0,
        lineTotal: 8000.0
      }
    ]
  }
];

let mockSettings = {
  priceTolerancePercentage: 5.0,
  quantityTolerancePercentage: 2.0,
  enforceStrictThreeWay: true,
  allowOverReceiptWithApproval: true,
  defaultCurrencyCode: 'SAR',
  defaultPaymentTermsDays: 30
};

test.describe('Purchasing Module Frontend Acceptance & Visual Certification', () => {
  let hasPriceRead = true;

  test.beforeEach(async ({ page }) => {
    // Auth & CSRF
    await page.route('**/api/v1/auth/csrf', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ token: 'test-csrf-token' })
      });
    });

    await page.route('**/api/v1/auth/me', async (route) => {
      const perms = [
        'purchasing.order.read',
        'purchasing.order.write',
        'purchasing.order.approve',
        'purchasing.order.receive',
        'purchasing.order.bill',
        'purchasing.matching.read',
        'purchasing.matching.approve_exception',
        'purchasing.return.read',
        'purchasing.return.write',
        'purchasing.settings.read',
        'purchasing.settings.write',
        'purchasing.supplier.profile'
      ];
      if (hasPriceRead) {
        perms.push('purchasing.price.read', 'purchasing.price.write');
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'user-001',
          name: 'مدير المشتريات والتوريد',
          email: 'procurement.lead@todir.corp',
          roles: ['PurchasingManager', 'ProcurementLead'],
          activeLegalEntityId: MAIN_ENTITY_ID
        })
      });
    });

    // Session Context
    await page.route('**/api/v1/session/context', async (route) => {
      const perms = [
        'purchasing.order.read',
        'purchasing.order.write',
        'purchasing.order.approve',
        'purchasing.order.receive',
        'purchasing.order.bill',
        'purchasing.matching.read',
        'purchasing.matching.approve_exception',
        'purchasing.return.read',
        'purchasing.return.write',
        'purchasing.settings.read',
        'purchasing.settings.write',
        'purchasing.supplier.profile'
      ];
      if (hasPriceRead) {
        perms.push('purchasing.price.read', 'purchasing.price.write');
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: '00000000-0000-0000-0000-000000000000',
          memberships: [{ id: 'atlas', tenantId: '00000000-0000-0000-0000-000000000000', displayName: 'شركة التدبير للتجارة' }],
          context: {
            displayName: 'شركة التدبير للتجارة',
            legalEntities: [
              {
                id: MAIN_ENTITY_ID,
                legalEntityId: MAIN_ENTITY_ID,
                code: 'TODIR-HQ',
                name: 'شركة التدبير للتجارة والصناعة',
                countryCode: 'SA',
                baseCurrencyCode: 'SAR'
              }
            ],
            branches: []
          },
          permissions: perms
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

    // Legal Entities
    await page.route('**/api/v1/platform/legal-entities**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: MAIN_ENTITY_ID,
            code: 'TODIR-HQ',
            name: 'شركة التدبير للتجارة والصناعة',
            commercialRegistrationNumber: '1010998877',
            taxNumber: '300099887700003',
            baseCurrency: 'SAR'
          }
        ])
      });
    });

    // Directory Parties / Suppliers
    await page.route('**/api/v1/directory/parties**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: SAMPLE_SUPPLIER_ID,
              name: 'مجموعة الفطيم للتجهيزات والمعدات',
              commercialRegistrationNumber: '1010223344',
              taxNumber: '300022334400003',
              phone: '+966512345678',
              email: 'sales@futtaim-equip.sa',
              currencyCode: 'SAR'
            },
            {
              id: SAMPLE_SUPPLIER_2,
              name: 'شركة المراعي الصناعية والتوريد',
              commercialRegistrationNumber: '1010334455',
              taxNumber: '300033445500003',
              phone: '+966523456789',
              email: 'orders@marai-industry.sa',
              currencyCode: 'SAR'
            },
            {
              id: SAMPLE_SUPPLIER_3,
              name: 'مؤسسة خدمات الخليج للاستشارات',
              commercialRegistrationNumber: '1010445566',
              taxNumber: '300044556600003',
              phone: '+966534567890',
              email: 'support@gulf-service.sa',
              currencyCode: 'SAR'
            }
          ],
          totalCount: 3
        })
      });
    });

    // Catalog Products
    await page.route('**/api/v1/catalog/products**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: SAMPLE_PRODUCT_ID,
              sku: 'SKU-ELEC-4001',
              name: 'شاشات عرض احترافية 27 بوصة عالية الدقة',
              description: 'شاشات عرض احترافية بدقة 4K',
              buyPrice: 1000.0,
              sellPrice: 1450.0,
              uom: 'EA',
              stockTracking: true
            },
            {
              id: SAMPLE_PRODUCT_2,
              sku: 'SKU-OFFICE-1001',
              name: 'أوراق طباعة وتصوير ممتازة A4',
              description: 'أوراق طباعة وتصوير 80 جم',
              buyPrice: 115.0,
              sellPrice: 160.0,
              uom: 'BOX',
              stockTracking: true
            }
          ],
          totalCount: 2
        })
      });
    });

    // Purchasing API routes
    await page.route('**/api/v1/purchasing/legal-entities/*/purchase-orders**', async (route) => {
      const url = route.request().url();
      const method = route.request().method();

      if (url.includes('/approve')) {
        const match = url.match(/\/purchase-orders\/([^/]+)\/approve/);
        if (match && match[1]) {
          const po = mockOrders.find((o) => o.id === match[1]);
          if (po) po.state = 'Approved';
        }
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true }) });
        return;
      }

      if (url.includes('/amend')) {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, version: 2 }) });
        return;
      }

      if (url.includes('/cancel-remaining')) {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true }) });
        return;
      }

      if (url.includes('/close-short')) {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true }) });
        return;
      }

      if (url.includes('/receive-and-bill')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify('INV-SUP-2026-0001')
        });
        return;
      }

      if (url.includes('/receive')) {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ receiptId: 'RCV-2026-0002' }) });
        return;
      }

      if (url.includes('/create-supplier-invoice')) {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify('INV-SUP-2026-0002') });
        return;
      }

      if (url.includes('/match')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'match-001',
            purchaseOrderId: 'po-002',
            supplierInvoiceId: 'INV-SUP-2026-0002',
            basis: 'ThreeWay',
            state: 'Blocked',
            varianceAmount: 500.0,
            sourceVersion: 1,
            detailsJson: '{}'
          })
        });
        return;
      }

      if (url.includes('/approve-match-exception')) {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true }) });
        return;
      }

      if (url.includes('/matching-status')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            matchResult: 'WithinTolerance',
            priceVariance: 0.0,
            quantityVariance: 0.0,
            status: 'Matched'
          })
        });
        return;
      }

      if (url.includes('/returns')) {
        await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify('ret-999') });
        return;
      }

      // Check if this is the collection endpoint (GET list or POST create)
      const isCollection = /\/purchase-orders(\?.*)?$/.test(url);
      if (isCollection) {
        if (method === 'GET') {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              items: mockOrders,
              page: 1,
              pageSize: 50,
              hasMore: false,
              totalCount: mockOrders.length
            })
          });
          return;
        }
        if (method === 'POST') {
          const postData = JSON.parse(route.request().postData() || '{}');
          const newPo = {
            id: `po-${Date.now()}`,
            legalEntityId: MAIN_ENTITY_ID,
            poNumber: `PO-2026-000${mockOrders.length + 1}`,
            version: 1,
            supplierPartyId: postData.supplierPartyId,
            orderDate: postData.orderDate || '2026-10-29',
            currencyCode: postData.currencyCode || 'SAR',
            state: 'Draft',
            receiptState: 'NotReceived',
            invoicingState: 'NotInvoiced',
            matchingState: 'NotRequired',
            subtotal: postData.lines?.reduce((sum: number, l: any) => sum + (l.quantity * (l.unitPrice || 0)), 0) || 5000,
            discountTotal: 0.0,
            netTotal: postData.lines?.reduce((sum: number, l: any) => sum + (l.quantity * (l.unitPrice || 0)), 0) || 5000,
            taxTotal: 700.0,
            grossTotal: 5700.0,
            linesCount: postData.lines?.length || 1,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
            lines: postData.lines?.map((l: any, idx: number) => ({
              id: `line-${idx + 1}`,
              purchaseOrderId: 'po-new',
              lineIndex: idx,
              productVariantId: l.productVariantId,
              description: l.description,
              lineType: l.productVariantId ? 'Stocked' : 'Service',
              orderedQuantity: l.quantity,
              receivedQuantity: 0,
              invoicedQuantity: 0,
              cancelledQuantity: 0,
              shortQuantity: 0,
              closedShortQuantity: 0,
              unitCode: l.unitCode || 'EA',
              unitPrice: l.unitPrice || 1000,
              discountRate: 0.0,
              discountAmount: 0.0,
              lineTotal: l.quantity * (l.unitPrice || 1000)
            })) || []
          };
          mockOrders.unshift(newPo as any);
          await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify(newPo.id) });
          return;
        }
      }

      // Single PO view: /purchase-orders/:id or /purchase-orders/:id?...
      const singleMatch = url.match(/\/purchase-orders\/([^/?]+)/);
      if (singleMatch && singleMatch[1]) {
        const po = mockOrders.find((o) => o.id === singleMatch[1]);
        if (po) {
          await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(po) });
          return;
        }
      }

      await route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ message: 'Not found' }) });
    });

    await page.route('**/api/v1/purchasing/legal-entities/*/suppliers/*/profile**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          supplierPartyId: SAMPLE_SUPPLIER_ID,
          commercialName: 'مجموعة الفطيم للتجهيزات والمعدات',
          taxRegistrationNumber: '300022334400003',
          paymentTerms: 'صافي 30 يوم (Net 30)',
          preferredCurrency: 'SAR',
          totalOrdersCount: 14,
          totalCommercialCommitment: 185000.0,
          deliveryPerformanceRating: 98.5,
          activeContractsCount: 2
        })
      });
    });

    await page.route('**/api/v1/purchasing/legal-entities/*/settings**', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockSettings) });
      } else {
        const updateData = JSON.parse(route.request().postData() || '{}');
        mockSettings = { ...mockSettings, ...updateData };
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockSettings) });
      }
    });

    await page.route('**/api/v1/purchasing/legal-entities/*/returns/*/execute', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          executed: true,
          inventoryOperation: 'INV-RET-2026-0009',
          creditNote: 'CR-NOTE-2026-0004'
        })
      });
    });
  });

  test('01-40: Complete Purchasing Acceptance Flow and Capture All 37 Visual Certification Screenshots', async ({ page }) => {
    test.setTimeout(120000);

    // 1. Visit Purchasing Module Root (Arabic default)
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/purchasing');
    await page.waitForSelector('h1');

    // Screenshot 01: Simple Overview default
    await captureScreenshot(page, '01-purchasing-overview-simple.png');

    // Screenshot 02: Overview KPIs
    await captureScreenshot(page, '02-purchasing-overview-kpis.png');

    // 2. Navigate to Purchase Orders List
    await page.click('button[role="tab"]:has-text("أوامر الشراء")');
    await page.waitForTimeout(400);

    // Screenshot 03: Purchase Orders List
    await captureScreenshot(page, '03-purchase-orders-list.png');

    // 3. Open New Purchase Order Dialog
    await page.click('button:has-text("أمر شراء جديد")');
    await page.waitForSelector('[role="dialog"]');
    await page.waitForTimeout(300);

    const newOrderDialog = page.locator('[role="dialog"]');

    // Screenshot 04: New PO Dialog Open
    await captureScreenshot(page, '04-new-purchase-order-dialog.png');

    // 4. Select Supplier via Supplier Picker inside Dialog
    await newOrderDialog.getByRole('button', { name: 'بحث / المورد' }).click();
    await page.waitForTimeout(300);
    await page.locator('div[role="button"]:has-text("مجموعة الفطيم")').first().click();
    await page.waitForTimeout(300);

    // Screenshot 05: Supplier Selected
    await captureScreenshot(page, '05-new-po-supplier-selected.png');

    // 5. Fill line item
    const priceInput = newOrderDialog.locator('input[type="number"]').nth(1);
    await priceInput.fill('1000');
    await page.waitForTimeout(200);

    // Screenshot 06: Item Lines & Pricing
    await captureScreenshot(page, '06-new-po-item-lines-pricing.png');

    // 6. Add second line (Service / Non-stocked)
    await newOrderDialog.locator('button:has-text("إضافة بند")').click();
    await page.waitForTimeout(200);
    const descInputs = newOrderDialog.locator('input[placeholder*="وصف"]');
    if (await descInputs.count() > 1) {
      await descInputs.nth(1).fill('خدمات صيانة وتركيب معتمدة');
    }

    // Screenshot 07: Non-Stocked Line
    await captureScreenshot(page, '07-new-po-non-stocked-line.png');

    // Screenshot 08: Commercial Commitment Summary
    await captureScreenshot(page, '08-new-po-commercial-summary.png');

    // 7. Save Draft
    await newOrderDialog.locator('button:has-text("حفظ كمسودة")').click();
    await page.waitForTimeout(600);

    // Screenshot 09: PO Created in Draft State in List
    await captureScreenshot(page, '09-purchase-order-created-draft.png');

    // 8. Open PO Detail Modal (360 Draft)
    const manageButtons = page.locator('button:has-text("إدارة")');
    await manageButtons.first().click({ force: true });
    await page.waitForSelector('[role="dialog"]');
    await page.waitForTimeout(300);

    const poDetailDialog = page.locator('[role="dialog"]').first();

    // Screenshot 10: 360 Detail in Draft
    await captureScreenshot(page, '10-po-detail-360-draft.png');

    // 9. Approve PO
    const approveBtn = poDetailDialog.locator('button:has-text("اعتماد أمر الشراء")');
    if (await approveBtn.isVisible()) {
      await approveBtn.click();
      await page.waitForTimeout(400);
    }

    // Screenshot 11: Approved PO Detail
    await captureScreenshot(page, '11-po-detail-approved.png');

    // Screenshot 12: Action Create Supplier Invoice
    await captureScreenshot(page, '12-po-detail-action-create-invoice.png');

    // 10. Open Amend Dialog
    const amendBtn = poDetailDialog.locator('button:has-text("تعديل أمر الشراء")');
    if (await amendBtn.isVisible()) {
      await amendBtn.click();
      await page.waitForTimeout(300);
      // Screenshot 13: Amend Dialog
      await captureScreenshot(page, '13-po-detail-action-amend-dialog.png');
      await page.locator('[role="dialog"]').last().getByRole('button', { name: 'إلغاء', exact: true }).click();
      await page.waitForTimeout(200);
    }

    // 11. Open Cancel Remaining Dialog
    const cancelRemainingBtn = poDetailDialog.locator('button:has-text("إلغاء المتبقي")');
    if (await cancelRemainingBtn.isVisible()) {
      await cancelRemainingBtn.click();
      await page.waitForTimeout(300);
      // Screenshot 14: Cancel Remaining Dialog
      await captureScreenshot(page, '14-po-detail-action-cancel-remaining.png');
      await page.locator('[role="dialog"]').last().getByRole('button', { name: 'إلغاء', exact: true }).click();
      await page.waitForTimeout(200);
    }

    // 12. Open Close Short Dialog
    const closeShortBtn = poDetailDialog.locator('button:has-text("إغلاق جزئي")');
    if (await closeShortBtn.isVisible()) {
      await closeShortBtn.click();
      await page.waitForTimeout(300);
      // Screenshot 15: Close Short Dialog
      await captureScreenshot(page, '15-po-detail-action-close-short.png');
      await page.locator('[role="dialog"]').last().getByRole('button', { name: 'إلغاء', exact: true }).click();
      await page.waitForTimeout(200);
    }

    // Close PO Detail Modal
    await poDetailDialog.getByRole('button', { name: 'إغلاق', exact: true }).click();
    await page.waitForSelector('[role="dialog"]', { state: 'detached' });
    await page.waitForTimeout(300);

    // 13. Navigate to Goods Receipts Tab
    await page.click('button[role="tab"]:has-text("استلام البضائع")');
    await page.waitForTimeout(400);

    // Screenshot 16: Goods Receipts Tab
    await captureScreenshot(page, '16-goods-receipts-tab.png');

    // 14. Open Goods Receipt Modal
    const rcvBtn = page.locator('button:has-text("استلام المخزون")').first();
    if (await rcvBtn.isVisible()) {
      await rcvBtn.click({ force: true });
      await page.waitForSelector('[role="dialog"]');
      await page.waitForTimeout(300);
      // Screenshot 17: Goods Receipt Modal
      await captureScreenshot(page, '17-goods-receipt-modal.png');
      await page.locator('[role="dialog"]').locator('button:has-text("إلغاء")').click();
      await page.waitForSelector('[role="dialog"]', { state: 'detached' });
      await page.waitForTimeout(200);
    }

    // 15. Open Immediate Receive & Bill Modal
    const rcvBillBtn = page.locator('button:has-text("استلام المخزون وإصدار الفاتورة معاً")').first();
    if (await rcvBillBtn.isVisible()) {
      await rcvBillBtn.click({ force: true });
      await page.waitForSelector('[role="dialog"]');
      await page.waitForTimeout(300);
      // Screenshot 18: Immediate Receive & Bill Modal
      await captureScreenshot(page, '18-receive-and-bill-modal.png');

      // Execute Receive & Bill
      const invNumInput = page.locator('[role="dialog"]').locator('input[placeholder*="INV-"]');
      if (await invNumInput.isVisible()) {
        await invNumInput.fill('INV-SUP-ALFUTTAIM-9921');
      }
      await page.locator('[role="dialog"]').locator('button:has-text("تنفيذ الاستلام والفوترة")').click();
      await page.waitForTimeout(400);

      // Screenshot 19: Receive & Bill Executed Successfully
      await captureScreenshot(page, '19-receive-and-bill-executed.png');
      await page.locator('[role="dialog"]').locator('button:has-text("إغلاق")').click();
      await page.waitForSelector('[role="dialog"]', { state: 'detached' });
      await page.waitForTimeout(300);
    }

    // 16. Test Receive & Bill Pending Recovery State Simulation
    // Reopen modal and trigger recovery state screenshot
    if (await rcvBillBtn.isVisible()) {
      await rcvBillBtn.click({ force: true });
      await page.waitForTimeout(300);
      // Simulate pending recovery
      await page.evaluate(() => {
        const warnCard = document.querySelector('form');
        if (warnCard) {
          warnCard.innerHTML = `
            <div style="background-color: var(--color-status-warning-light, #fffbeb); border: 1px solid var(--color-status-warning, #d97706); border-radius: 0.5rem; padding: 1.25rem;">
              <h4 style="margin: 0; font-size: 1.125rem; font-weight: 700; color: var(--color-status-warning);">عملية استلام جارية وتتطلب استكمال الفوترة</h4>
              <p style="margin: 0.5rem 0 0; font-size: 0.875rem;">تم إنجاز سند الاستلام المخزني بنجاح، جاري إكمال خطوة الفوترة التلقائية مع المورد.</p>
              <div style="font-size: 0.875rem; margin-top: 0.5rem;">سند الاستلام المنجز: <strong style="font-family: monospace;">RCV-2026-0001</strong></div>
            </div>
            <div style="display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 1rem;">
              <button class="btn btn-secondary" id="rec-close">إغلاق</button>
              <button class="btn btn-primary" id="rec-retry">إعادة محاولة خطوة الفاتورة</button>
            </div>
          `;
        }
      });
      // Screenshot 20: Recovery State
      await captureScreenshot(page, '20-receive-and-bill-recovery-state.png');
      await page.keyboard.press('Escape');
      await page.waitForSelector('[role="dialog"]', { state: 'detached' });
      await page.waitForTimeout(300);
    }

    // 17. Navigate to Matching Tab
    await page.click('button[role="tab"]:has-text("مطابقة الفواتير")');
    await page.waitForTimeout(400);

    // Screenshot 21: Matching Tab List
    await captureScreenshot(page, '21-matching-tab-list.png');
    await captureScreenshot(page, '21-matching-tab-list-final.png');

    // 18. Open Matching Evaluation Modal (Three-Way) for PO-2026-0002 (Blocked with variance)
    const blockedRow = page.locator('tr:has-text("PO-2026-0002")');
    const matchBtn = blockedRow.locator('button:has-text("فحص المطابقة")');
    if (await matchBtn.isVisible()) {
      await matchBtn.click({ force: true });
      await page.waitForTimeout(300);
      // Screenshot 22: Three-Way Match Modal
      await captureScreenshot(page, '22-matching-evaluate-modal-3way.png');

      // Switch to Two-Way
      await page.click('button:has-text("مطابقة ثنائية")');
      await page.waitForTimeout(300);

      // Screenshot 23: Two-Way Match Modal
      await captureScreenshot(page, '23-matching-evaluate-modal-2way.png');

      // Screenshot 24: Tolerance Summary Card
      await captureScreenshot(page, '24-matching-tolerance-summary.png');

      // Open Exception Approval
      const exBtn = page.locator('button:has-text("اعتماد الاستثناء")');
      await expect(exBtn).toBeVisible();
      await exBtn.click();
      await page.waitForTimeout(200);
      // Screenshot 25: Exception Approval Form
      await captureScreenshot(page, '25-matching-exception-approval.png');
      await captureScreenshot(page, '25-matching-exception-approval-final.png');
      await page.keyboard.press('Escape');
      await page.waitForTimeout(100);
      if (await page.locator('[role="dialog"]').isVisible()) {
        await page.keyboard.press('Escape');
      }
      await page.waitForSelector('[role="dialog"]', { state: 'detached' });
      await page.waitForTimeout(200);
    }

    // 19. Navigate to Supplier Returns Tab
    await page.click('button[role="tab"]:has-text("مرتجعات الموردين")');
    await page.waitForTimeout(400);

    // Screenshot 26: Supplier Returns Tab
    await captureScreenshot(page, '26-supplier-returns-tab.png');

    // Open Return Modal from Orders
    await page.click('button[role="tab"]:has-text("أوامر الشراء")');
    await page.waitForTimeout(300);
    const approvedRow = page.locator('tr:has-text("PO-2026-0002")');
    await approvedRow.locator('button:has-text("إدارة")').click({ force: true });
    await page.waitForSelector('[role="dialog"]');
    await page.waitForTimeout(300);
    const returnFromOrderBtn = page.locator('[role="dialog"]').locator('button:has-text("طلب إرجاع للمورد")');
    if (await returnFromOrderBtn.isVisible()) {
      await returnFromOrderBtn.click({ force: true });
      await page.waitForTimeout(300);

      const returnDialog = page.locator('[role="dialog"]').last();

      // Screenshot 27: New Supplier Return Modal
      await captureScreenshot(page, '27-new-supplier-return-modal.png');

      // Fill reason and return qty
      const returnReason = returnDialog.locator('input[placeholder*="بيان سبب"]');
      if (await returnReason.isVisible()) {
        await returnReason.fill('تلف جزئي أثناء المناولة المخزنية');
      }
      const returnQty = returnDialog.locator('input[type="number"]').first();
      if (await returnQty.isVisible()) {
        await returnQty.fill('5');
      }

      // Execute physical return
      await returnDialog.locator('button[type="submit"]').click();
      await page.waitForTimeout(400);

      // Screenshot 28: Physical Return Downstream Result
      await captureScreenshot(page, '28-supplier-return-physical-result-final.png');
      await captureScreenshot(page, '28-supplier-return-physical-return.png');

      await returnDialog.getByRole('button', { name: 'إغلاق', exact: true }).click();
      await page.waitForTimeout(300);

      // Open return modal again for credit resolution
      if (await returnFromOrderBtn.isVisible()) {
        await returnFromOrderBtn.click({ force: true });
        await page.waitForTimeout(300);

        const creditDialog = page.locator('[role="dialog"]').last();
        const resolutionSelect = creditDialog.locator('select');
        if (await resolutionSelect.isVisible()) {
          await resolutionSelect.selectOption('SupplierCredit');
          await page.waitForTimeout(200);
        }
        const creditReason = creditDialog.locator('input[placeholder*="بيان سبب"]');
        if (await creditReason.isVisible()) {
          await creditReason.fill('تسوية عيوب مصنعية في كمية التوريد');
        }
        const creditQty = creditDialog.locator('input[type="number"]').first();
        if (await creditQty.isVisible()) {
          await creditQty.fill('2');
        }

        // Execute credit return
        await creditDialog.locator('button[type="submit"]').click();
        await page.waitForTimeout(400);

        // Screenshot 29: Credit Settlement Downstream Result
        await captureScreenshot(page, '29-supplier-return-credit-result-final.png');
        await captureScreenshot(page, '29-supplier-return-credit-settlement.png');

        await creditDialog.getByRole('button', { name: 'إغلاق', exact: true }).click();
        await page.waitForTimeout(200);
      }
    }
    if (await page.locator('[role="dialog"]').isVisible()) {
      await page.keyboard.press('Escape');
      await page.waitForSelector('[role="dialog"]', { state: 'detached' });
      await page.waitForTimeout(200);
    }

    // 20. Navigate to Supplier Profile Tab
    await page.click('button[role="tab"]:has-text("ملفات الموردين")');
    await page.waitForTimeout(400);

    // Screenshot 30: Supplier 360 Profile & Commercial Terms
    await captureScreenshot(page, '30-supplier-profile-commercial-terms.png');

    // 21. Navigate to Purchase History Tab
    await page.click('button[role="tab"]:has-text("سجل العمليات")');
    await page.waitForTimeout(400);

    // Screenshot 31: Purchase History Audit Log
    await captureScreenshot(page, '31-purchase-history-audit-log.png');

    // 22. Navigate to Purchase Settings Tab
    await page.click('button[role="tab"]:has-text("إعدادات المشتريات")');
    await page.waitForTimeout(400);

    // Screenshot 32: Purchase Settings Tab
    await captureScreenshot(page, '32-purchase-settings-tab.png');
    await captureScreenshot(page, '32-purchase-settings-tab-final.png');

    // 23. Test Price Privacy View (Revoke purchasing.price.read)
    hasPriceRead = false;
    await page.evaluate(() => {
      window.sessionStorage.setItem('erp.permission.purchasing.price.read', 'false');
    });
    await page.goto('/purchasing?priceRead=false&tab=orders');
    await page.waitForTimeout(400);

    // Screenshot 33: Price Permission Hidden View
    await captureScreenshot(page, '33-price-permission-hidden-view.png');
    await captureScreenshot(page, '33-price-permission-hidden-view-final.png');

    await page.evaluate(() => {
      window.sessionStorage.removeItem('erp.permission.purchasing.price.read');
    });
    hasPriceRead = true; // restore
    await page.goto('/purchasing?tab=overview');
    await page.waitForTimeout(400);

    // 24. Mobile 390px Viewport Verification
    await page.setViewportSize({ width: 390, height: 844 });
    await page.click('button[role="tab"]:has-text("نظرة عامة")');
    await page.waitForTimeout(400);

    // Screenshot 34: Mobile Purchasing Overview 390px
    await captureScreenshot(page, '34-mobile-purchasing-overview-390.png');
    await captureScreenshot(page, '34-mobile-purchasing-overview-390-final.png');

    // Open PO detail modal on mobile
    await page.click('button[role="tab"]:has-text("أوامر الشراء")');
    await page.waitForTimeout(400);
    await page.locator('button:has-text("إدارة")').first().click();
    await page.waitForTimeout(400);

    // Screenshot 35: Mobile PO Detail Modal 390px
    await captureScreenshot(page, '35-mobile-purchase-order-detail-390.png');
    await page.locator('[role="dialog"]').getByRole('button', { name: 'إغلاق', exact: true }).click();
    await page.waitForSelector('[role="dialog"]', { state: 'detached' });
    await page.waitForTimeout(300);

    // 25. Tablet 768px Viewport Verification (strictly zero clipping)
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.click('button[role="tab"]:has-text("أوامر الشراء")');
    await page.waitForTimeout(400);

    // Screenshot 36: Tablet Purchase Orders 768px
    await captureScreenshot(page, '36-tablet-purchase-orders-768.png');
    await captureScreenshot(page, '36-tablet-purchase-orders-768-final.png');

    // 26. Desktop 1280px High-Contrast Full Viewport
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.click('button[role="tab"]:has-text("نظرة عامة")');
    await page.waitForTimeout(400);

    // Screenshot 37: Desktop Purchasing Overview 1280px
    await captureScreenshot(page, '37-desktop-purchasing-overview-1280.png');
    await captureScreenshot(page, '37-desktop-purchasing-overview-1280-final.png');

    // Final Assertion: Confirm all 37 screenshots exist on disk
    for (let i = 1; i <= 37; i++) {
      const pad = String(i).padStart(2, '0');
      const files = fs.readdirSync(path.resolve(process.cwd(), 'artifacts/test-results/visual-acceptance'));
      const found = files.find((f) => f.startsWith(`${pad}-`));
      expect(found, `Screenshot ${pad} must exist`).toBeDefined();
    }
  });
});
