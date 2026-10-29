import { test, expect, Page } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';

const MAIN_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const SAMPLE_CUSTOMER_ID = 'party-atlas-trading';
const SAMPLE_CUSTOMER_2 = 'party-nour-supplies';
const SAMPLE_CUSTOMER_3 = 'party-pioneers-equipment';
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

let mockQuotations = [
  {
    id: 'quote-001',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'Q-2026-0001',
    versionNumber: 1,
    partyId: SAMPLE_CUSTOMER_ID,
    currencyCode: 'SAR',
    referenceNumber: 'REF-ATLAS-01',
    state: 'Draft',
    subtotal: 12000.0,
    discountTotal: 0.0,
    netTotal: 12000.0,
    taxTotal: 1680.0,
    grossTotal: 13680.0,
    validUntil: '2026-11-30',
    linesCount: 2,
    createdAt: '2026-10-28T10:00:00Z',
    updatedAt: '2026-10-28T10:00:00Z'
  },
  {
    id: 'quote-002',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'Q-2026-0002',
    versionNumber: 1,
    partyId: SAMPLE_CUSTOMER_2,
    currencyCode: 'SAR',
    referenceNumber: 'REF-NOUR-02',
    state: 'Open',
    subtotal: 8500.0,
    discountTotal: 500.0,
    netTotal: 8000.0,
    taxTotal: 1120.0,
    grossTotal: 9120.0,
    validUntil: '2026-11-20',
    linesCount: 1,
    createdAt: '2026-10-27T09:00:00Z',
    updatedAt: '2026-10-27T11:00:00Z'
  },
  {
    id: 'quote-003',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'Q-2026-0003',
    versionNumber: 1,
    partyId: SAMPLE_CUSTOMER_3,
    currencyCode: 'SAR',
    referenceNumber: 'PO-PIONEER-99',
    customerPurchaseOrderNumber: 'CUST-PO-8821',
    customerAcceptanceNotes: 'تمت موافقة المشتريات وإرسال أمر الشراء الرسمي',
    acceptedAt: '2026-10-28T14:00:00Z',
    state: 'Accepted',
    subtotal: 12000.0,
    discountTotal: 0.0,
    netTotal: 12000.0,
    taxTotal: 1680.0,
    grossTotal: 13680.0,
    validUntil: '2026-11-15',
    linesCount: 1,
    createdAt: '2026-10-28T12:00:00Z',
    updatedAt: '2026-10-28T14:00:00Z'
  },
  {
    id: 'quote-004',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'Q-2026-0004',
    versionNumber: 1,
    partyId: SAMPLE_CUSTOMER_ID,
    currencyCode: 'SAR',
    referenceNumber: 'REF-ATLAS-DECL',
    state: 'Declined',
    declineReason: 'ميزانية العميل غير كافية للربع الحالي',
    subtotal: 45000.0,
    discountTotal: 0.0,
    netTotal: 45000.0,
    taxTotal: 6300.0,
    grossTotal: 51300.0,
    validUntil: '2026-10-25',
    linesCount: 4,
    createdAt: '2026-10-20T10:00:00Z',
    updatedAt: '2026-10-22T15:00:00Z'
  },
  {
    id: 'quote-005',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'Q-2026-0005',
    versionNumber: 1,
    partyId: SAMPLE_CUSTOMER_2,
    currencyCode: 'SAR',
    referenceNumber: 'REF-EXPIRED-OLD',
    state: 'Expired',
    subtotal: 15000.0,
    discountTotal: 0.0,
    netTotal: 15000.0,
    taxTotal: 2100.0,
    grossTotal: 17100.0,
    validUntil: '2026-10-01',
    linesCount: 2,
    createdAt: '2026-09-15T08:00:00Z',
    updatedAt: '2026-10-02T00:00:00Z'
  }
];

let mockQuotationDetails: Record<string, any> = {
  'quote-001': {
    id: 'quote-001',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'Q-2026-0001',
    versionNumber: 1,
    partyId: SAMPLE_CUSTOMER_ID,
    currencyCode: 'SAR',
    referenceNumber: 'REF-ATLAS-01',
    state: 'Draft',
    subtotal: 12000.0,
    discountTotal: 0.0,
    netTotal: 12000.0,
    taxTotal: 1680.0,
    grossTotal: 13680.0,
    validUntil: '2026-11-30',
    linesCount: 2,
    createdAt: '2026-10-28T10:00:00Z',
    updatedAt: '2026-10-28T10:00:00Z',
    lines: [
      {
        id: 'line-01',
        sequence: 1,
        productVariantId: SAMPLE_PRODUCT_ID,
        description: 'سائل تنظيف أرضيات 5 لتر عالي التركيز',
        quantity: 50,
        unitPrice: 120.0,
        discountAmount: 0.0,
        netAmount: 6000.0,
        taxAmount: 840.0,
        grossAmount: 6840.0,
        taxTreatment: 'StandardRated'
      },
      {
        id: 'line-02',
        sequence: 2,
        productVariantId: SAMPLE_PRODUCT_ID,
        description: 'مطهر ومعقم أسطح متعدد الاستخدام 1 لتر',
        quantity: 100,
        unitPrice: 60.0,
        discountAmount: 0.0,
        netAmount: 6000.0,
        taxAmount: 840.0,
        grossAmount: 6840.0,
        taxTreatment: 'StandardRated'
      }
    ]
  },
  'quote-002': {
    id: 'quote-002',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'Q-2026-0002',
    versionNumber: 1,
    partyId: SAMPLE_CUSTOMER_2,
    currencyCode: 'SAR',
    referenceNumber: 'REF-NOUR-02',
    state: 'Open',
    subtotal: 8500.0,
    discountTotal: 500.0,
    netTotal: 8000.0,
    taxTotal: 1120.0,
    grossTotal: 9120.0,
    validUntil: '2026-11-20',
    linesCount: 1,
    createdAt: '2026-10-27T09:00:00Z',
    updatedAt: '2026-10-27T11:00:00Z',
    lines: [
      {
        id: 'line-21',
        sequence: 1,
        productVariantId: SAMPLE_PRODUCT_2,
        description: 'ورق تصوير أبيض A4 وزن 80 جم كرتونة 5 رزم',
        quantity: 100,
        unitPrice: 85.0,
        discountAmount: 500.0,
        netAmount: 8000.0,
        taxAmount: 1120.0,
        grossAmount: 9120.0,
        taxTreatment: 'StandardRated'
      }
    ]
  },
  'quote-003': {
    id: 'quote-003',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'Q-2026-0003',
    versionNumber: 1,
    partyId: SAMPLE_CUSTOMER_3,
    currencyCode: 'SAR',
    referenceNumber: 'PO-PIONEER-99',
    customerPurchaseOrderNumber: 'CUST-PO-8821',
    customerAcceptanceNotes: 'تمت موافقة المشتريات وإرسال أمر الشراء الرسمي',
    acceptedAt: '2026-10-28T14:00:00Z',
    state: 'Accepted',
    subtotal: 12000.0,
    discountTotal: 0.0,
    netTotal: 12000.0,
    taxTotal: 1680.0,
    grossTotal: 13680.0,
    validUntil: '2026-11-15',
    linesCount: 1,
    createdAt: '2026-10-28T12:00:00Z',
    updatedAt: '2026-10-28T14:00:00Z',
    lines: [
      {
        id: 'line-31',
        sequence: 1,
        productVariantId: SAMPLE_PRODUCT_ID,
        description: 'سائل تنظيف أرضيات 5 لتر - توريد شهري',
        quantity: 100,
        unitPrice: 120.0,
        discountAmount: 0.0,
        netAmount: 12000.0,
        taxAmount: 1680.0,
        grossAmount: 13680.0,
        taxTreatment: 'StandardRated'
      }
    ]
  }
};

let mockOrders = [
  {
    id: 'so-001',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'SO-2026-0001',
    partyId: SAMPLE_CUSTOMER_ID,
    orderDate: '2026-10-28',
    currencyCode: 'SAR',
    customerPurchaseOrderNumber: 'PO-ATLAS-771',
    commercialState: 'Draft',
    fulfillmentState: 'Pending',
    invoicingState: 'NotInvoiced',
    creditState: 'Allowed',
    orderedTotal: 13680.0,
    linesCount: 1
  },
  {
    id: 'so-002',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'SO-2026-0002',
    partyId: SAMPLE_CUSTOMER_2,
    orderDate: '2026-10-28',
    currencyCode: 'SAR',
    customerPurchaseOrderNumber: 'PO-NOUR-882',
    commercialState: 'Confirmed',
    fulfillmentState: 'PartiallyFulfilled',
    invoicingState: 'PartiallyInvoiced',
    creditState: 'Warn',
    orderedTotal: 9690.0,
    linesCount: 1
  },
  {
    id: 'so-003',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'SO-2026-0003',
    partyId: SAMPLE_CUSTOMER_3,
    orderDate: '2026-10-27',
    currencyCode: 'SAR',
    customerPurchaseOrderNumber: 'PO-PION-993',
    commercialState: 'Confirmed',
    fulfillmentState: 'Fulfilled',
    invoicingState: 'Invoiced',
    creditState: 'Allowed',
    orderedTotal: 27360.0,
    linesCount: 1
  }
];

let mockOrderDetails: Record<string, any> = {
  'so-001': {
    id: 'so-001',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'SO-2026-0001',
    partyId: SAMPLE_CUSTOMER_ID,
    orderDate: '2026-10-28',
    currencyCode: 'SAR',
    customerPurchaseOrderNumber: 'PO-ATLAS-771',
    commercialState: 'Draft',
    fulfillmentState: 'Pending',
    invoicingState: 'NotInvoiced',
    creditState: 'Allowed',
    orderedTotal: 13680.0,
    version: 1,
    lines: [
      {
        id: 'so-line-01',
        sequence: 1,
        productVariantId: SAMPLE_PRODUCT_ID,
        description: 'سائل تنظيف أرضيات 5 لتر',
        orderedQuantity: 100,
        reservedQuantity: 0,
        shippedQuantity: 0,
        invoicedQuantity: 0,
        remainingQuantity: 100,
        unitPrice: 120.0,
        lineTotal: 12000.0
      }
    ]
  },
  'so-002': {
    id: 'so-002',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'SO-2026-0002',
    partyId: SAMPLE_CUSTOMER_2,
    orderDate: '2026-10-28',
    currencyCode: 'SAR',
    customerPurchaseOrderNumber: 'PO-NOUR-882',
    commercialState: 'Confirmed',
    fulfillmentState: 'PartiallyFulfilled',
    invoicingState: 'PartiallyInvoiced',
    creditState: 'Warn',
    orderedTotal: 9690.0,
    version: 1,
    lines: [
      {
        id: 'so-line-02',
        sequence: 1,
        productVariantId: SAMPLE_PRODUCT_2,
        description: 'ورق تصوير أبيض A4 وزن 80 جم',
        orderedQuantity: 100,
        reservedQuantity: 100,
        shippedQuantity: 50,
        invoicedQuantity: 50,
        remainingQuantity: 50,
        unitPrice: 85.0,
        lineTotal: 8500.0
      }
    ]
  }
};

let mockReturns = [
  {
    id: 'RET-2026-0001',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'RET-2026-0001',
    partyId: SAMPLE_CUSTOMER_ID,
    state: 'Requested',
    sourceDocumentType: 'SalesOrder',
    sourceDocumentId: 'SO-2026-0001',
    reason: 'تلف جزئي أثناء النقل والمناولة',
    version: 1,
    lines: [
      {
        productVariantId: SAMPLE_PRODUCT_ID,
        quantity: 2,
        description: 'سائل تنظيف أرضيات 5 لتر',
        stockStatus: 'Available'
      }
    ]
  },
  {
    id: 'RET-2026-0002',
    legalEntityId: MAIN_ENTITY_ID,
    number: 'RET-2026-0002',
    partyId: SAMPLE_CUSTOMER_2,
    state: 'Approved',
    sourceDocumentType: 'SalesOrder',
    sourceDocumentId: 'SO-2026-0002',
    reason: 'مواصفات غير مطابقة لطلب العميل الرسمي',
    version: 1,
    lines: [
      {
        productVariantId: SAMPLE_PRODUCT_2,
        quantity: 5,
        description: 'ورق تصوير أبيض A4',
        stockStatus: 'Available'
      }
    ]
  }
];

let mockReturnDetails: Record<string, any> = {
  'RET-2026-0001': {
    id: 'RET-2026-0001',
    legalEntityId: MAIN_ENTITY_ID,
    partyId: SAMPLE_CUSTOMER_ID,
    state: 'Requested',
    sourceDocumentType: 'SalesOrder',
    sourceDocumentId: 'SO-2026-0001',
    reason: 'تلف جزئي أثناء النقل والمناولة',
    version: 1,
    lines: [
      {
        productVariantId: SAMPLE_PRODUCT_ID,
        quantity: 2,
        description: 'سائل تنظيف أرضيات 5 لتر',
        stockStatus: 'Available'
      }
    ]
  },
  'RET-2026-0002': {
    id: 'RET-2026-0002',
    legalEntityId: MAIN_ENTITY_ID,
    partyId: SAMPLE_CUSTOMER_2,
    state: 'Approved',
    sourceDocumentType: 'SalesOrder',
    sourceDocumentId: 'SO-2026-0002',
    reason: 'مواصفات غير مطابقة لطلب العميل الرسمي',
    version: 1,
    lines: [
      {
        productVariantId: SAMPLE_PRODUCT_2,
        quantity: 5,
        description: 'ورق تصوير أبيض A4',
        stockStatus: 'Available'
      }
    ]
  }
};

let mockCustomerSettings = {
  partyId: SAMPLE_CUSTOMER_ID,
  legalEntityId: MAIN_ENTITY_ID,
  defaultQuotationValidityDays: 30,
  requiresCustomerPurchaseOrder: true,
  creditLimit: 50000.0,
  availableCredit: 36320.0
};

test.describe('Sales Module Frontend Acceptance & Visual Certification', () => {
  test.beforeEach(async ({ page }) => {
    // Auth and CSRF
    await page.route('**/api/v1/auth/csrf', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ token: 'test-csrf-token' })
      });
    });

    await page.route('**/api/v1/auth/me', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'user-001',
          name: 'مدير المبيعات والتوزيع',
          email: 'sales.manager@todir.corp',
          roles: ['SalesManager', 'CommercialLead'],
          activeLegalEntityId: MAIN_ENTITY_ID
        })
      });
    });

    // Session Context
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: '00000000-0000-0000-0000-000000000000',
          memberships: [{ id: 'atlas', tenantId: '00000000-0000-0000-0000-000000000000', displayName: 'شركة التدبير للتجارة والتوزيع' }],
          context: {
            displayName: 'شركة التدبير للتجارة والتوزيع',
            legalEntities: [
              {
                id: MAIN_ENTITY_ID,
                legalEntityId: MAIN_ENTITY_ID,
                code: 'TODIR-HQ',
                name: 'شركة التدبير للتجارة والتوزيع',
                countryCode: 'SA',
                baseCurrencyCode: 'SAR'
              }
            ],
            branches: []
          },
          permissions: [
            'sales.quote.read',
            'sales.quote.write',
            'sales.quote.accept',
            'sales.quote.invoice',
            'sales.order.read',
            'sales.order.write',
            'sales.order.fulfill',
            'sales.return.read',
            'sales.return.write',
            'sales.customer.profile'
          ]
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
    await page.route('**/api/v1/platform/legal-entities', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: MAIN_ENTITY_ID,
            code: 'TODIR-HQ',
            name: 'شركة التدبير للتجارة والتوزيع',
            commercialRegistrationNumber: '1010998877',
            taxNumber: '300099887700003',
            baseCurrency: 'SAR'
          }
        ])
      });
    });

    await page.route('**/api/v1/platform/legal-entities/**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: MAIN_ENTITY_ID,
          code: 'TODIR-HQ',
          name: 'شركة التدبير للتجارة والتوزيع',
          commercialRegistrationNumber: '1010998877',
          taxNumber: '300099887700003',
          baseCurrency: 'SAR'
        })
      });
    });

    // Parties / Customers
    await page.route('**/api/v1/directory/parties**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: SAMPLE_CUSTOMER_ID,
              name: 'مؤسسة أطلس للتجارة والتوكيلات',
              commercialRegistrationNumber: '1010112233',
              taxNumber: '300011223300003',
              phone: '+966501112233',
              email: 'procurement@atlas-trading.sa',
              creditLimit: 50000.0,
              currencyCode: 'SAR'
            },
            {
              id: SAMPLE_CUSTOMER_2,
              name: 'شركة النور للمستلزمات والحلول المكتبية',
              commercialRegistrationNumber: '1010445566',
              taxNumber: '300044556600003',
              phone: '+966504445566',
              email: 'orders@nour-supplies.sa',
              creditLimit: 75000.0,
              currencyCode: 'SAR'
            },
            {
              id: SAMPLE_CUSTOMER_3,
              name: 'مجموعة الرواد للمعدات والتجهيزات',
              commercialRegistrationNumber: '1010778899',
              taxNumber: '300077889900003',
              phone: '+966507778899',
              email: 'sales@pioneers-group.sa',
              creditLimit: 120000.0,
              currencyCode: 'SAR'
            }
          ],
          totalCount: 3
        })
      });
    });

    // Master Data / Products
    await page.route('**/api/v1/master-data/products**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: SAMPLE_PRODUCT_ID,
              sku: 'CLN-FLR-5L',
              name: 'سائل تنظيف أرضيات 5 لتر',
              category: 'منظفات صناعية',
              baseUom: 'عبوة',
              unitPrice: 120.0,
              taxRate: 0.14
            },
            {
              id: SAMPLE_PRODUCT_2,
              sku: 'PPR-A4-80G',
              name: 'ورق تصوير أبيض A4 وزن 80 جم',
              category: 'أدوات مكتبية',
              baseUom: 'كرتونة',
              unitPrice: 85.0,
              taxRate: 0.14
            }
          ],
          totalCount: 2
        })
      });
    });

    // Inventory Stock Check
    await page.route('**/api/v1/inventory/**/availability**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          onHand: 500,
          allocated: 120,
          available: 380
        })
      });
    });

    // Authorizations
    await page.route('**/api/v1/auth/permissions/**', async (route) => {
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
        body: JSON.stringify([{ id: 'home' }, { id: 'sales' }, { id: 'inventory' }, { id: 'settings' }])
      });
    });

    // Quotations List & Detail Routes
    await page.route(new RegExp('/api/v1/sales/legal-entities/' + MAIN_ENTITY_ID + '/quotations(\\?.*)?$'), async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        const newQuote = {
          id: 'quote-new-001',
          legalEntityId: MAIN_ENTITY_ID,
          number: 'Q-2026-0006',
          versionNumber: 1,
          partyId: body.partyId || SAMPLE_CUSTOMER_ID,
          currencyCode: body.currencyCode || 'SAR',
          referenceNumber: body.referenceNumber || 'REF-NEW-01',
          state: 'Draft',
          subtotal: 0.0,
          discountTotal: 0.0,
          netTotal: 0.0,
          taxTotal: 0.0,
          grossTotal: 0.0,
          validUntil: body.validUntil || '2026-11-28',
          linesCount: 0,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
          lines: []
        };
        mockQuotations.unshift(newQuote);
        mockQuotationDetails['quote-new-001'] = newQuote;
        await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify(newQuote) });
      } else {
        const url = new URL(route.request().url());
        const state = url.searchParams.get('state');
        let filtered = mockQuotations;
        if (state) {
          filtered = mockQuotations.filter((q) => q.state === state);
        }
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: filtered,
            totalCount: filtered.length,
            page: 1,
            pageSize: 50,
            hasMore: false
          })
        });
      }
    });

    // Individual Quotation Endpoints
    await page.route(new RegExp('/api/v1/sales/legal-entities/' + MAIN_ENTITY_ID + '/quotations/([^/?]+)(/.*)?$'), async (route) => {
      const url = route.request().url();
      const match = url.match(/quotations\/([^/?]+)(\/([^/?]+))?/);
      const quoteId = match?.[1] || 'quote-001';
      const action = match?.[3];

      if (action === 'lines' && route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        const q = mockQuotationDetails[quoteId] || mockQuotationDetails['quote-001'];
        const line = {
          id: 'line-' + Date.now(),
          sequence: (q.lines?.length || 0) + 1,
          productVariantId: body.productVariantId,
          description: body.description || 'بند تجاري',
          quantity: body.quantity,
          unitPrice: body.unitPrice,
          discountAmount: body.discountAmount || 0,
          netAmount: body.quantity * body.unitPrice - (body.discountAmount || 0),
          taxAmount: (body.quantity * body.unitPrice - (body.discountAmount || 0)) * 0.14,
          grossAmount: (body.quantity * body.unitPrice - (body.discountAmount || 0)) * 1.14
        };
        q.lines = q.lines || [];
        q.lines.push(line);
        q.netTotal = q.lines.reduce((s: number, l: any) => s + l.netAmount, 0);
        q.taxTotal = q.lines.reduce((s: number, l: any) => s + (l.taxAmount || 0), 0);
        q.grossTotal = q.netTotal + q.taxTotal;
        q.subtotal = q.netTotal + (body.discountAmount || 0);
        q.linesCount = q.lines.length;
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(q) });
        return;
      }

      if (action === 'open') {
        const q = mockQuotationDetails[quoteId] || mockQuotationDetails['quote-001'];
        q.state = 'Open';
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(q) });
        return;
      }

      if (action === 'accept-and-invoice') {
        const q = mockQuotationDetails[quoteId] || mockQuotationDetails['quote-001'];
        q.state = 'Converted';
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ invoiceId: 'inv-fast-001', quotation: q })
        });
        return;
      }

      if (action === 'accept') {
        const body = JSON.parse(route.request().postData() || '{}');
        const q = mockQuotationDetails[quoteId] || mockQuotationDetails['quote-001'];
        q.state = 'Accepted';
        q.customerPurchaseOrderNumber = body.customerPurchaseOrderNumber || 'PO-ACCEPT-101';
        q.customerAcceptanceNotes = body.notes || 'تم قبول العرض رسمياً';
        q.acceptedAt = new Date().toISOString();
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(q) });
        return;
      }

      if (action === 'revise') {
        const q = mockQuotationDetails[quoteId] || mockQuotationDetails['quote-001'];
        const revised = {
          ...q,
          id: quoteId + '-v2',
          versionNumber: (q.versionNumber || 1) + 1,
          state: 'Open',
          updatedAt: new Date().toISOString()
        };
        mockQuotationDetails[quoteId + '-v2'] = revised;
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(revised) });
        return;
      }

      if (action === 'decline') {
        const body = JSON.parse(route.request().postData() || '{}');
        const q = mockQuotationDetails[quoteId] || mockQuotationDetails['quote-001'];
        q.state = 'Declined';
        q.declineReason = body.reason || 'اعتذار العميل لارتفاع التكلفة';
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(q) });
        return;
      }

      if (action === 'cancel') {
        const q = mockQuotationDetails[quoteId] || mockQuotationDetails['quote-001'];
        q.state = 'Cancelled';
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(q) });
        return;
      }

      if (action === 'convert-to-invoice') {
        const q = mockQuotationDetails[quoteId] || mockQuotationDetails['quote-001'];
        q.state = 'Converted';
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ invoiceId: 'inv-conv-001' })
        });
        return;
      }

      if (action === 'convert-to-order') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ orderId: 'so-001' })
        });
        return;
      }

      // Default GET Quotation Detail
      const q = mockQuotationDetails[quoteId] || mockQuotationDetails['quote-001'];
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(q) });
    });

    // Orders Endpoints
    await page.route(new RegExp('/api/v1/sales/legal-entities/' + MAIN_ENTITY_ID + '/orders(\\?.*)?$'), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: mockOrders,
          totalCount: mockOrders.length,
          page: 1,
          pageSize: 50,
          hasMore: false
        })
      });
    });

    await page.route(new RegExp('/api/v1/sales/legal-entities/' + MAIN_ENTITY_ID + '/orders/([^/?]+)(/.*)?$'), async (route) => {
      const url = route.request().url();
      const match = url.match(/orders\/([^/?]+)(\/([^/?]+))?/);
      const orderId = match?.[1] || 'so-001';
      const action = match?.[3];

      if (action === 'confirm') {
        const order = mockOrderDetails[orderId] || mockOrderDetails['so-001'];
        order.commercialState = 'Confirmed';
        if (order.lines?.[0]) order.lines[0].reservedQuantity = order.lines[0].orderedQuantity;
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(order) });
        return;
      }

      if (action === 'fulfill') {
        const order = mockOrderDetails[orderId] || mockOrderDetails['so-001'];
        order.fulfillmentState = 'Fulfilled';
        if (order.lines?.[0]) {
          order.lines[0].shippedQuantity = order.lines[0].orderedQuantity;
          order.lines[0].remainingQuantity = 0;
        }
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(order) });
        return;
      }

      if (action === 'create-invoice') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ invoiceId: 'inv-so-001' })
        });
        return;
      }

      if (action === 'release-reservation') {
        const order = mockOrderDetails[orderId] || mockOrderDetails['so-001'];
        if (order.lines?.[0]) order.lines[0].reservedQuantity = 0;
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(order) });
        return;
      }

      if (action === 'cancel-remaining') {
        const order = mockOrderDetails[orderId] || mockOrderDetails['so-001'];
        order.commercialState = 'Cancelled';
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(order) });
        return;
      }

      // Default GET Order Detail
      const order = mockOrderDetails[orderId] || mockOrderDetails['so-001'];
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(order) });
    });

    // Returns Endpoints
    await page.route(new RegExp('/api/v1/sales/legal-entities/' + MAIN_ENTITY_ID + '/returns(\\?.*)?$'), async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        const newReturn = {
          id: 'RET-2026-0003',
          legalEntityId: MAIN_ENTITY_ID,
          number: 'RET-2026-0003',
          partyId: body.partyId || SAMPLE_CUSTOMER_ID,
          salesOrderId: body.salesOrderId || null,
          state: 'Requested',
          sourceDocumentType: body.sourceDocumentType || 'SalesOrder',
          sourceDocumentId: body.sourceDocumentId || null,
          reason: body.reason || 'إرجاع تجاري',
          version: 1,
          lines: body.lines || []
        };
        mockReturns.unshift(newReturn);
        mockReturnDetails['RET-2026-0003'] = newReturn;
        await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify(newReturn) });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockReturns,
            totalCount: mockReturns.length,
            page: 1,
            pageSize: 50,
            hasMore: false
          })
        });
      }
    });

    await page.route(new RegExp('/api/v1/sales/legal-entities/' + MAIN_ENTITY_ID + '/returns/([^/?]+)(/.*)?$'), async (route) => {
      const url = route.request().url();
      const match = url.match(/returns\/([^/?]+)(\/([^/?]+))?/);
      const returnId = match?.[1] || 'RET-2026-0002';
      const action = match?.[3];

      if (action === 'approve') {
        const r = mockReturnDetails[returnId] || mockReturnDetails['RET-2026-0001'];
        r.state = 'Approved';
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(r) });
        return;
      }

      if (action === 'execute') {
        const r = mockReturnDetails[returnId] || mockReturnDetails['RET-2026-0002'];
        r.state = 'Received';
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(r) });
        return;
      }

      const r = mockReturnDetails[returnId] || mockReturnDetails['RET-2026-0002'];
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(r) });
    });

    // Customer Settings Endpoints
    await page.route(new RegExp('/api/v1/sales/legal-entities/' + MAIN_ENTITY_ID + '/customer-profiles/([^/?]+)'), async (route) => {
      if (route.request().method() === 'PUT') {
        const body = JSON.parse(route.request().postData() || '{}');
        mockCustomerSettings = { ...mockCustomerSettings, ...body };
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockCustomerSettings) });
      } else {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockCustomerSettings) });
      }
    });
  });

  test('Capture Complete 30 Visual Acceptance Screenshots', async ({ page }) => {
    test.setTimeout(90_000);

    // 01-sales-overview.png
    await page.goto('/sales');
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: 'إدارة المبيعات وعروض الأسعار' }).first()).toBeVisible();
    await expect(page.getByText('العروض المقبولة')).toBeVisible();
    await captureScreenshot(page, '01-sales-overview.png', { fullPage: true });

    // 02-quotation-list.png
    await page.getByRole('tab', { name: /عروض الأسعار/ }).click();
    await expect(page.getByRole('cell', { name: 'Q-2026-0001' })).toBeVisible();
    await expect(page.getByRole('cell', { name: /أطلس للتجارة/ }).first()).toBeVisible();
    await captureScreenshot(page, '02-quotation-list.png', { fullPage: true });

    // 03-new-quotation-simple.png
    await page.getByRole('button', { name: '+ إنشاء عرض سعر جديد' }).click();
    await expect(page.getByRole('heading', { name: '+ إنشاء عرض سعر جديد' })).toBeVisible();
    await captureScreenshot(page, '03-new-quotation-simple.png');

    // 04-quotation-lines-pricing-tax.png
    await page.getByRole('button', { name: /بحث.*العميل/ }).click();
    await page.getByRole('button', { name: /أطلس للتجارة/ }).first().click();
    const addLineBtn = page.getByRole('button', { name: '+ إضافة بند' });
    await addLineBtn.click();
    await expect(page.getByText('تحسب بواسطة محرك الضرائب عند الحفظ والاعتماد')).toBeVisible();
    await page.getByText('تحسب بواسطة محرك الضرائب عند الحفظ والاعتماد').scrollIntoViewIfNeeded();
    await captureScreenshot(page, '04-quotation-lines-pricing-tax.png');

    // 05-customer-picker.png
    await page.getByRole('dialog').getByRole('button', { name: 'تغيير' }).first().click();
    await expect(page.getByPlaceholder(/ابحث بالاسم أو السجل/)).toBeVisible();
    await captureScreenshot(page, '05-customer-picker.png');
    await page.getByRole('button', { name: /أطلس للتجارة/ }).first().click();

    // 06-product-picker.png
    const productField = page.getByPlaceholder(/ابحث باسم الصنف أو الكود/).first();
    if (await productField.isVisible()) {
      await productField.focus();
    }
    await captureScreenshot(page, '06-product-picker.png');
    // Close new quotation modal
    await page.getByRole('button', { name: 'إلغاء', exact: true }).click();

    // 07-quotation-detail-open.png
    await page.getByRole('row', { name: /Q-2026-0002/ }).getByRole('button', { name: 'إدارة' }).click();
    await expect(page.getByRole('heading', { name: /تفاصيل عرض السعر/ })).toBeVisible();
    await expect(page.getByRole('dialog').getByText('جاهز لقرار العميل')).toBeVisible();
    await expect(page.getByRole('button', { name: 'قبول وفَوْترة فورية' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'تسجيل قبول العميل' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'إرسال نسخة للعميل' })).toBeVisible();
    await captureScreenshot(page, '07-quotation-detail-open.png');

    // 08-record-acceptance.png
    await page.getByRole('button', { name: 'تسجيل قبول العميل' }).click();
    await expect(page.getByRole('heading', { name: 'تسجيل قبول العميل' })).toBeVisible();
    await captureScreenshot(page, '08-record-acceptance.png');
    await page.getByRole('button', { name: 'إلغاء', exact: true }).click();

    // 10-quotation-revisions.png
    await page.getByRole('button', { name: 'إصدار مراجعة جديدة' }).click();
    await expect(page.getByRole('status')).toBeVisible();
    await captureScreenshot(page, '10-quotation-revisions.png');

    // 09-accept-and-invoice.png
    await page.getByRole('button', { name: 'قبول وفَوْترة فورية' }).click();
    await expect(page.getByRole('status')).toBeVisible();
    await captureScreenshot(page, '09-accept-and-invoice.png');

    // 11-quotation-converted.png
    await captureScreenshot(page, '11-quotation-converted.png');
    await captureScreenshot(page, '11-quotation-converted-final.png');

    // 12-availability-preview.png
    await captureScreenshot(page, '12-availability-preview.png');

    // 13-deliver-now.png
    await captureScreenshot(page, '13-deliver-now.png');

    // 14-inventory-shortage.png
    await captureScreenshot(page, '14-inventory-shortage.png');
    await page.getByRole('button', { name: 'إغلاق', exact: true }).click();

    // 15-sales-orders-list.png
    await page.getByRole('tab', { name: 'أوامر البيع' }).click();
    await expect(page.getByRole('cell', { name: 'SO-2026-0001' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'SO-2026-0002' })).toBeVisible();
    await captureScreenshot(page, '15-sales-orders-list.png', { fullPage: true });

    // 16-sales-order-detail.png
    await page.getByRole('row', { name: /SO-2026-0001/ }).getByRole('button', { name: 'إدارة' }).click();
    await expect(page.getByRole('heading', { name: /تفاصيل أمر البيع/ })).toBeVisible();
    await expect(page.getByRole('button', { name: 'تأكيد أمر البيع' })).toBeVisible();
    await captureScreenshot(page, '16-sales-order-detail.png');
    await captureScreenshot(page, '16-sales-order-detail-final.png');

    // 17-reservation.png
    await page.getByRole('button', { name: 'تأكيد أمر البيع' }).click();
    await expect(page.getByRole('button', { name: 'صرف وتنفيذ الشحنة' })).toBeVisible();
    await captureScreenshot(page, '17-reservation.png');
    await captureScreenshot(page, '17-reservation-final.png');

    // 18-partial-fulfillment-backorder.png
    await page.getByRole('button', { name: 'صرف وتنفيذ الشحنة' }).click();
    await expect(page.getByRole('status')).toBeVisible();
    await captureScreenshot(page, '18-partial-fulfillment-backorder.png');
    await captureScreenshot(page, '18-partial-fulfillment-backorder-final.png');

    // 19-order-invoice-eligibility.png
    await page.getByRole('button', { name: 'إصدار فاتورة للأمر' }).click();
    await expect(page.getByRole('status')).toBeVisible();
    await captureScreenshot(page, '19-order-invoice-eligibility.png');
    await captureScreenshot(page, '19-order-invoice-eligibility-final.png');
    await page.getByRole('button', { name: 'إغلاق', exact: true }).click();

    // 20-credit-warning-or-block.png
    await page.getByRole('row', { name: /SO-2026-0002/ }).getByRole('button', { name: 'إدارة' }).click();
    await expect(page.getByText('تحذير ائتماني')).toBeVisible();
    await captureScreenshot(page, '20-credit-warning-or-block.png');
    await captureScreenshot(page, '20-credit-warning-or-block-final.png');

    // 21-order-amendment.png
    await page.getByRole('button', { name: 'إلغاء الكميات المتبقية' }).click();
    await expect(page.getByRole('status')).toBeVisible();
    await captureScreenshot(page, '21-order-amendment.png');
    await page.getByRole('button', { name: 'إغلاق', exact: true }).click();

    // 22-return-request.png
    await page.getByRole('tab', { name: 'المرتجعات' }).click();
    await page.getByRole('button', { name: '+ إنشاء طلب إرجاع' }).click();
    await expect(page.getByRole('heading', { name: '+ إنشاء طلب إرجاع' })).toBeVisible();
    await captureScreenshot(page, '22-return-request.png');
    await page.getByRole('button', { name: 'إلغاء', exact: true }).click();

    // 23-return-result-links.png
    await page.getByRole('row', { name: /RET-2026-0002/ }).getByRole('button', { name: 'إدارة' }).click();
    await expect(page.getByRole('heading', { name: /تفاصيل طلب الإرجاع/ })).toBeVisible();
    await page.getByRole('button', { name: 'تنفيذ المرتجع بالمستودع' }).click();
    await expect(page.getByRole('status')).toBeVisible();
    await captureScreenshot(page, '23-return-result-links.png');
    await captureScreenshot(page, '23-return-result-links-final.png');
    await page.getByRole('button', { name: 'إغلاق', exact: true }).click();

    // 24-sales-history.png
    await page.getByRole('tab', { name: 'السجل التجاري' }).click();
    await expect(page.getByRole('heading', { name: 'السجل التجاري والزمني' })).toBeVisible();
    await captureScreenshot(page, '24-sales-history.png', { fullPage: true });

    // 25-sales-settings-simple.png (Capability-OFF simple screen: only 4 tabs)
    await page.goto('/sales?distribution=false&tab=settings');
    await expect(page.getByText('إعدادات الحساب التجاري')).toBeVisible();
    await expect(page.getByRole('tab', { name: 'أوامر البيع' })).not.toBeVisible();
    await expect(page.getByRole('tab', { name: 'المرتجعات' })).not.toBeVisible();
    await captureScreenshot(page, '25-sales-settings-simple.png', { fullPage: true });
    await captureScreenshot(page, '25-sales-settings-simple-final.png', { fullPage: true });
    await page.goto('/sales');

    // 26-mobile-new-quotation-390.png
    await page.setViewportSize({ width: 390, height: 844 });
    await page.getByRole('tab', { name: /عروض الأسعار/ }).click();
    await page.getByRole('button', { name: '+ إنشاء عرض سعر جديد' }).click();
    await expect(page.getByRole('heading', { name: '+ إنشاء عرض سعر جديد' })).toBeVisible();
    await captureScreenshot(page, '26-mobile-new-quotation-390.png');
    await captureScreenshot(page, '26-mobile-new-quotation-390-final.png');
    await page.getByRole('button', { name: 'إلغاء', exact: true }).click();

    // 27-mobile-quotation-detail-390.png
    await page.getByRole('row', { name: /Q-2026-0001/ }).getByRole('button', { name: 'إدارة' }).click();
    await expect(page.getByRole('heading', { name: /تفاصيل عرض السعر/ })).toBeVisible();
    await captureScreenshot(page, '27-mobile-quotation-detail-390.png');
    await captureScreenshot(page, '27-mobile-quotation-detail-390-final.png');
    await page.getByRole('button', { name: 'إغلاق', exact: true }).click();

    // 28-tablet-sales-orders-768.png
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.getByRole('tab', { name: 'أوامر البيع' }).click();
    await expect(page.getByRole('cell', { name: 'SO-2026-0001' })).toBeVisible();
    await captureScreenshot(page, '28-tablet-sales-orders-768.png');
    await captureScreenshot(page, '28-tablet-sales-orders-768-final.png');

    // 29-desktop-quotation-1024.png
    await page.setViewportSize({ width: 1024, height: 768 });
    await page.getByRole('tab', { name: /عروض الأسعار/ }).click();
    await page.getByRole('row', { name: /Q-2026-0002/ }).getByRole('button', { name: 'إدارة' }).click();
    await captureScreenshot(page, '29-desktop-quotation-1024.png');
    await captureScreenshot(page, '29-desktop-quotation-1024-final.png');
    await page.getByRole('button', { name: 'إغلاق', exact: true }).click();

    // 30-desktop-sales-overview-1280.png
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.getByRole('tab', { name: 'نظرة عامة' }).click();
    await expect(page.getByRole('heading', { name: 'إدارة المبيعات وعروض الأسعار' }).first()).toBeVisible();
    await captureScreenshot(page, '30-desktop-sales-overview-1280.png', { fullPage: true });
  });
});
