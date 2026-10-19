import { test, expect } from '@playwright/test';

// Canonical UOMs
const uomPcs = { id: 'uom-pcs', code: 'PCS', nameAr: 'قطعة', nameEn: 'Piece', dimension: 'Count', allowFraction: false, precision: 0, status: 'Active' };
const uomHour = { id: 'uom-hr', code: 'HR', nameAr: 'ساعة', nameEn: 'Hour', dimension: 'Time', allowFraction: true, precision: 2, status: 'Active' };
const uomPack = { id: 'uom-pack', code: 'PACK', nameAr: 'طرد / باكت', nameEn: 'Pack', dimension: 'Count', allowFraction: false, precision: 0, status: 'Active' };
const uomCtn = { id: 'uom-ctn', code: 'CTN', nameAr: 'كرتونة', nameEn: 'Carton', dimension: 'Count', allowFraction: false, precision: 0, status: 'Active' };
const masterUoms = [uomPcs, uomHour, uomPack, uomCtn];

// Canonical Categories & Brands
const catFood = { id: 'cat-food', name: 'أغذية ومشروبات', parentId: null, status: 'Active', version: 1 };
const catServices = { id: 'cat-srv', name: 'خدمات مهنية', parentId: null, status: 'Active', version: 1 };
const catFashion = { id: 'cat-fsh', name: 'ملابس وأزياء', parentId: null, status: 'Active', version: 1 };
const masterCategories = [catFood, catServices, catFashion];

const brandTodir = { id: 'brand-todir', name: 'TODIR', status: 'Active', version: 1 };
const brandApex = { id: 'brand-apex', name: 'أبيكس', status: 'Active', version: 1 };
const masterBrands = [brandTodir, brandApex];

// Canonical Products
const distProduct = {
  id: 'dist-001',
  templateId: 'dist-001',
  defaultVariantId: 'var-dist-001',
  nature: 'Goods',
  displayName: 'مياه معدنية 500 مل',
  nameAr: 'مياه معدنية 500 مل',
  nameEn: 'Mineral Water 500ml',
  sku: 'WAT-500',
  baseUomId: 'uom-pcs',
  baseUomCode: 'PCS',
  categoryId: 'cat-food',
  categoryName: 'أغذية ومشروبات',
  brandId: 'brand-apex',
  brandName: 'أبيكس',
  status: 'Active',
  version: 2,
  createdAt: '2026-10-10T08:00:00Z',
  updatedAt: '2026-10-15T14:30:00Z',
  barcodes: [
    { id: 'bc-1', value: '628100000001', barcodeType: 'EAN13', isPrimary: true, variantId: 'var-dist-001', uomId: 'uom-pcs', createdAt: '2026-10-10T08:00:00Z' }
  ],
  variants: [
    { id: 'var-dist-001', sku: 'WAT-500', isDefault: true, status: 'Active', attributes: {} }
  ]
};

const srvProduct = {
  id: 'srv-001',
  templateId: 'srv-001',
  defaultVariantId: 'var-srv-001',
  nature: 'Service',
  displayName: 'استشارة مالية وضريبية',
  nameAr: 'استشارة مالية وضريبية',
  nameEn: 'Financial & Tax Advisory',
  sku: 'SRV-FIN-01',
  baseUomId: 'uom-hr',
  baseUomCode: 'HR',
  categoryId: 'cat-srv',
  categoryName: 'خدمات مهنية',
  status: 'Active',
  version: 1,
  createdAt: '2026-10-12T10:00:00Z',
  updatedAt: '2026-10-12T10:00:00Z',
  barcodes: [],
  variants: [
    { id: 'var-srv-001', sku: 'SRV-FIN-01', isDefault: true, status: 'Active', attributes: {} }
  ]
};

const fshProduct = {
  id: 'fsh-001',
  templateId: 'fsh-001',
  defaultVariantId: 'var-fsh-001',
  nature: 'Goods',
  displayName: 'قميص قطني كلاسيكي',
  nameAr: 'قميص قطني كلاسيكي',
  nameEn: 'Classic Cotton Shirt',
  sku: 'SHIRT-CLS',
  baseUomId: 'uom-pcs',
  baseUomCode: 'PCS',
  categoryId: 'cat-fsh',
  categoryName: 'ملابس وأزياء',
  status: 'Active',
  version: 3,
  createdAt: '2026-10-08T09:00:00Z',
  updatedAt: '2026-10-16T11:00:00Z',
  barcodes: [],
  variants: [
    { id: 'var-fsh-001', sku: 'SHIRT-CLS-RED-S', isDefault: true, status: 'Active', attributes: { 'COLOR': 'RED', 'SIZE': 'S' } },
    { id: 'var-fsh-002', sku: 'SHIRT-CLS-RED-M', isDefault: false, status: 'Active', attributes: { 'COLOR': 'RED', 'SIZE': 'M' } },
    { id: 'var-fsh-003', sku: 'SHIRT-CLS-BLU-S', isDefault: false, status: 'Active', attributes: { 'COLOR': 'BLU', 'SIZE': 'S' } },
    { id: 'var-fsh-004', sku: 'SHIRT-CLS-BLU-M', isDefault: false, status: 'Active', attributes: { 'COLOR': 'BLU', 'SIZE': 'M' } }
  ]
};

const allProducts = [distProduct, srvProduct, fshProduct];

const distPackagingUoms = [
  { id: 'puom-1', variantId: 'var-dist-001', uomId: 'uom-pack', uomCode: 'PACK', uomNameAr: 'طرد / باكت', uomNameEn: 'Pack', factorToBase: 6, allowsSale: true, allowsPurchase: true, allowsWarehouse: true, barcode: '628100123456' },
  { id: 'puom-2', variantId: 'var-dist-001', uomId: 'uom-ctn', uomCode: 'CTN', uomNameAr: 'كرتونة', uomNameEn: 'Carton', factorToBase: 24, allowsSale: true, allowsPurchase: true, allowsWarehouse: true, barcode: '628100654321' }
];

const distReferences = [
  { id: 'ref-1', productId: 'dist-001', partyType: 'Customer', referenceType: 'Customer', referenceCode: 'CUST-REF-WAT', notes: 'كود المنتج في شبكة فروع كارفور' },
  { id: 'ref-2', productId: 'dist-001', partyType: 'Supplier', referenceType: 'Supplier', referenceCode: 'SUPP-REF-99', notes: 'رمز المصنع الوطني للتعبئة' }
];

const fshAttributes = [
  {
    attribute: { id: 'attr-col', code: 'COLOR', nameAr: 'اللون', nameEn: 'Color', isVariantDefining: true, status: 'Active', version: 1 },
    isRequired: true,
    displayOrder: 1,
    values: [
      { id: 'val-red', attributeId: 'attr-col', code: 'RED', nameAr: 'أحمر', nameEn: 'Red', status: 'Active', version: 1 },
      { id: 'val-blu', attributeId: 'attr-col', code: 'BLU', nameAr: 'أزرق', nameEn: 'Blue', status: 'Active', version: 1 }
    ]
  },
  {
    attribute: { id: 'attr-siz', code: 'SIZE', nameAr: 'المقاس', nameEn: 'Size', isVariantDefining: true, status: 'Active', version: 1 },
    isRequired: true,
    displayOrder: 2,
    values: [
      { id: 'val-s', attributeId: 'attr-siz', code: 'S', nameAr: 'صغير (S)', nameEn: 'Small (S)', status: 'Active', version: 1 },
      { id: 'val-m', attributeId: 'attr-siz', code: 'M', nameAr: 'متوسط (M)', nameEn: 'Medium (M)', status: 'Active', version: 1 }
    ]
  }
];

const importPreviewSummary = {
  sessionId: 'imp-sess-100',
  batchId: 'imp-sess-100',
  totalRows: 3,
  validRows: 3,
  errorRows: 0,
  createCount: 2,
  updateCount: 1,
  errorCount: 0,
  rows: [
    { rowNumber: 1, sku: 'PRD-001', displayName: 'ورق تصوير أبيض A4', baseUomCode: 'EA', classification: 'Create', action: 'Create', issues: [] },
    { rowNumber: 2, sku: 'PRD-002', displayName: 'كابل كهرباء نحاسي 2.5 مم', baseUomCode: 'EA', classification: 'Create', action: 'Create', issues: [] },
    { rowNumber: 3, sku: 'SRV-001', displayName: 'خدمة صيانة دورية', baseUomCode: 'HOUR', classification: 'Update', action: 'Update', issues: [] }
  ]
};

const importCommitSummary = {
  sessionId: 'imp-sess-100',
  batchId: 'imp-sess-100',
  totalRows: 3,
  importedRows: 3,
  skippedRows: 0,
  failedRows: 0,
  createCount: 2,
  updateCount: 1,
  skippedCount: 0,
  items: []
};

test.describe('Catalog Canonical Acceptance & Visual Journey Suite', () => {
  test.beforeEach(async ({ page }) => {
    // Session context & modules
    await page.route('**/api/v1/session/context', route => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        authenticated: true,
        tenantId: 'tenant-demo',
        memberships: [{ id: 'm1', tenantId: 'tenant-demo', displayName: 'شركة الأفق للتجارة' }],
        context: { legalEntities: [{ id: 'le-1', name: 'الشركة الرئيسية' }], branches: [] }
      })
    }));

    await page.route('**/api/v1/modules', route => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([{ id: 'home' }, { id: 'catalog' }])
    }));

    await page.route('**/api/v1/authorization/check*', route => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ allowed: true })
    }));

    // Master definitions
    await page.route('**/api/v1/catalog/uoms', route => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(masterUoms)
    }));

    await page.route('**/api/v1/catalog/categories', route => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(masterCategories)
    }));

    await page.route('**/api/v1/catalog/brands', route => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(masterBrands)
    }));

    // Product endpoints
    await page.route(/\/api\/v1\/catalog\/products(\?.*)?$/, async route => {
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON();
        const created = {
          id: `new-${Date.now()}`,
          templateId: `new-${Date.now()}`,
          defaultVariantId: `var-new-${Date.now()}`,
          nature: body.nature || 'Goods',
          displayName: body.displayName,
          nameAr: body.nameAr || body.displayName,
          nameEn: body.nameEn || '',
          sku: body.sku || 'AUTO-GEN-01',
          baseUomId: body.baseUomId,
          baseUomCode: masterUoms.find(u => u.id === body.baseUomId)?.code || 'PCS',
          categoryId: body.categoryId || null,
          categoryName: masterCategories.find(c => c.id === body.categoryId)?.name || null,
          brandId: body.brandId || null,
          brandName: masterBrands.find(b => b.id === body.brandId)?.name || null,
          status: 'Active',
          version: 1,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
          barcodes: [],
          variants: [{ id: `var-new-${Date.now()}`, sku: body.sku || 'AUTO-GEN-01', isDefault: true, status: 'Active', attributes: {} }]
        };
        return route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify(created) });
      }

      const url = new URL(route.request().url());
      const search = url.searchParams.get('search')?.toLowerCase() || '';
      const nature = url.searchParams.get('nature') || '';

      let filtered = [...allProducts];
      if (nature) filtered = filtered.filter(p => p.nature === nature);
      if (search) filtered = filtered.filter(p => p.displayName.toLowerCase().includes(search) || (p.sku && p.sku.toLowerCase().includes(search)));

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: filtered, totalCount: filtered.length, page: 1, pageSize: 25 })
      });
    });

    // Product Detail and sub-resources
    await page.route(/\/api\/v1\/catalog\/products\/([^/?#]+)(\/.*)?$/, async route => {
      const url = new URL(route.request().url());
      const pathname = url.pathname;

      if (pathname.endsWith('/readiness')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ productId: 'dist-001', ready: true, missing: [], warnings: [] })
        });
      }

      if (pathname.endsWith('/references')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: distReferences, totalCount: distReferences.length, page: 1, pageSize: 25 })
        });
      }

      if (pathname.endsWith('/attributes')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(fshAttributes)
        });
      }

      if (pathname.endsWith('/visibility')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([{ legalEntityId: 'le-1', isVisible: true, canSell: true, canPurchase: true }])
        });
      }

      if (pathname.includes('/variants/') && pathname.endsWith('/uoms')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(distPackagingUoms[0])
        });
      }

      if (pathname.endsWith('/barcodes')) {
        return route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'bc-new', value: '628100999999', barcodeType: 'EAN13', isPrimary: false })
        });
      }

      if (pathname.endsWith('/archive') || pathname.endsWith('/restore')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ ...distProduct, status: pathname.endsWith('/archive') ? 'Archived' : 'Active' })
        });
      }

      // Exact product detail
      const segments = pathname.split('/');
      const id = decodeURIComponent(segments[segments.indexOf('products') + 1] || '');
      const prod = allProducts.find(p => p.id === id) || distProduct;
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(prod) });
    });

    // Variant UOMs listing
    await page.route(/\/api\/v1\/catalog\/variants\/([^/?#]+)\/uoms/, async route => {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(distPackagingUoms)
      });
    });

    // Import endpoints
    await page.route('**/api/v1/catalog/imports/preview', async route => {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(importPreviewSummary)
      });
    });

    await page.route('**/api/v1/catalog/imports/*/commit', async route => {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(importCommitSummary)
      });
    });

    await page.route('**/api/v1/catalog/export', async route => {
      return route.fulfill({
        status: 200,
        contentType: 'text/csv',
        body: 'sku,name,nature,uom\nWAT-500,مياه معدنية 500 مل,Goods,PCS'
      });
    });
  });

  test('executes complete Catalog user journeys and captures visual acceptance evidence', async ({ page }) => {
    test.setTimeout(60_000);

    // Set desktop standard viewport
    await page.setViewportSize({ width: 1440, height: 900 });

    // ----------------------------------------------------
    // SCENARIO 1: Product List — RTL Arabic & LTR English Parity
    // ----------------------------------------------------
    await page.goto('/catalog');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.getByRole('heading', { name: 'كتالوج المنتجات والخدمات' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'مياه معدنية 500 مل' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'استشارة مالية وضريبية' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'قميص قطني كلاسيكي' })).toBeVisible();

    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-list-desktop-rtl.png', fullPage: true });

    // Toggle to English LTR
    const langBtn = page.getByRole('button', { name: 'التبديل إلى الإنجليزية' });
    await expect(langBtn).toBeVisible();
    await langBtn.click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await expect(page.getByRole('heading', { name: 'Products and services catalog' })).toBeVisible();

    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-list-desktop-ltr.png', fullPage: true });

    // Switch back to Arabic RTL
    await page.getByRole('button', { name: 'Switch to Arabic' }).click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');

    // ----------------------------------------------------
    // SCENARIO 2: Quick Create — Simple Service (Zero Inventory UX)
    // ----------------------------------------------------
    await page.getByRole('button', { name: 'إضافة منتج أو خدمة جديدة' }).click();
    await expect(page.getByRole('heading', { name: 'إضافة منتج أو بضاعة جديدة' })).toBeVisible();

    // Select Service nature
    const natureSelect = page.getByLabel('نوع البند');
    await natureSelect.click();
    await page.getByRole('option', { name: 'خدمة' }).click();

    await expect(page.getByRole('heading', { name: 'إضافة خدمة جديدة' })).toBeVisible();
    // Fill Service details
    await page.getByLabel('اسم المنتج أو الخدمة').fill('خدمة دعم فني وتدريب');
    
    // Base UOM Select
    const uomSelect = page.getByLabel('وحدة القياس الأساسية');
    await uomSelect.click();
    await page.getByRole('option', { name: /ساعة/ }).click();

    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-quick-create-service-desktop.png' });

    // Close dialog
    await page.getByRole('button', { name: 'إلغاء' }).click();
    await expect(page.getByRole('heading', { name: 'إضافة خدمة جديدة' })).not.toBeVisible();

    // ----------------------------------------------------
    // SCENARIO 3: Quick Create — Simple Good with Advanced Fields
    // ----------------------------------------------------
    await page.getByRole('button', { name: 'إضافة منتج أو خدمة جديدة' }).click();
    await expect(page.getByRole('heading', { name: 'إضافة منتج أو بضاعة جديدة' })).toBeVisible();

    await page.getByLabel('اسم المنتج أو الخدمة').fill('دفتر فواتير قياسي A5');
    
    // Select Piece UOM
    const goodUomSelect = page.getByLabel('وحدة القياس الأساسية');
    await goodUomSelect.click();
    await page.getByRole('option', { name: /قطعة/ }).click();

    // Show Advanced fields
    await page.getByRole('button', { name: '+ إظهار الحقول المتقدمة والتصنيف' }).click();
    await page.getByLabel('رمز التخزين التعريفي (SKU) — اختياري').fill('NB-A5-INV');

    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-quick-create-good-desktop.png' });

    await page.getByRole('button', { name: 'إلغاء' }).click();

    // ----------------------------------------------------
    // SCENARIO 4: Product Workspace — Overview & Identity
    // ----------------------------------------------------
    await page.getByRole('button', { name: 'مياه معدنية 500 مل' }).click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'مياه معدنية 500 مل' })).toBeVisible();
    await expect(page.getByText('WAT-500').first()).toBeVisible();
    await expect(page.getByText('أغذية ومشروبات')).toBeVisible();

    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-product-record-overview-desktop.png', fullPage: true });

    // ----------------------------------------------------
    // SCENARIO 5: Distributor Packaging UOMs & Barcodes
    // ----------------------------------------------------
    await page.getByRole('tab', { name: 'وحدات التعبئة والباركود' }).click();
    await expect(page.getByRole('heading', { name: 'وحدات القياس والتعبئة البديلة' })).toBeVisible();
    await expect(page.getByText('1 PACK = 6 PCS')).toBeVisible();
    await expect(page.getByText('1 CTN = 24 PCS')).toBeVisible();

    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-distributor-packaging-uoms-desktop.png', fullPage: true });

    // Open Barcode Dialog
    await page.getByRole('button', { name: 'إضافة باركود جديد' }).click();
    await expect(page.getByRole('heading', { name: 'تسجيل باركود جديد للصنف' })).toBeVisible();

    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-barcode-dialog-desktop.png' });
    await page.getByRole('button', { name: 'إلغاء' }).click();

    // ----------------------------------------------------
    // SCENARIO 6: Fashion Product with Variants Matrix
    // ----------------------------------------------------
    await page.getByRole('button', { name: 'رجوع' }).click();
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'قميص قطني كلاسيكي' }).click();
    await page.waitForLoadState('networkidle');

    await page.getByRole('tab', { name: 'المتغيرات والألوان والمقاسات' }).click();
    await expect(page.getByRole('heading', { name: 'إدارة الموديلات والمتغيرات' })).toBeVisible();
    await expect(page.getByText('SHIRT-CLS-RED-S')).toBeVisible();
    await expect(page.getByText('SHIRT-CLS-BLU-M')).toBeVisible();

    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-fashion-variants-desktop.png', fullPage: true });

    // Open Variant Matrix Dialog
    await page.getByRole('button', { name: /إضافة متغير جديد/ }).click();
    await expect(page.getByRole('heading', { name: 'توليد وإدارة المتغيرات والموديلات' })).toBeVisible();

    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-variant-matrix-dialog-desktop.png' });
    await page.getByRole('button', { name: 'إلغاء' }).click();

    // ----------------------------------------------------
    // SCENARIO 7: External References (Customer / Supplier)
    // ----------------------------------------------------
    await page.getByRole('button', { name: 'رجوع' }).click();
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'مياه معدنية 500 مل' }).click();
    await page.waitForLoadState('networkidle');

    await page.getByRole('tab', { name: 'أكواد العملاء والموردين' }).click();
    await expect(page.getByRole('heading', { name: 'أكواد الربط مع العملاء والموردين' })).toBeVisible();
    await expect(page.getByText('CUST-REF-WAT')).toBeVisible();
    await expect(page.getByText('SUPP-REF-99')).toBeVisible();

    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-references-desktop.png', fullPage: true });

    // ----------------------------------------------------
    // SCENARIO 8: Multi-Entity Visibility Scoping Dialog
    // ----------------------------------------------------
    await page.getByRole('button', { name: 'نطاق الرؤية والشركات' }).click();
    await expect(page.getByRole('heading', { name: 'إدارة نطاق رؤية الصنف وتعدده' })).toBeVisible();

    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-visibility-dialog-desktop.png' });
    await page.getByRole('button', { name: 'إلغاء' }).click();

    // ----------------------------------------------------
    // SCENARIO 9: Catalog CSV Import Wizard
    // ----------------------------------------------------
    await page.getByRole('button', { name: 'رجوع' }).click();
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'استيراد كتالوج' }).click();
    await expect(page.getByRole('heading', { name: 'استيراد كتالوج المنتجات والخدمات (Excel / CSV)' })).toBeVisible();

    // Step 1: Upload
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-import-upload-desktop.png' });

    // Advance to Step 2: Mapping
    await page.getByRole('button', { name: 'المتابعة لمطابقة الأعمدة' }).click();
    await expect(page.getByText('قم بمطابقة أسماء الأعمدة في ملفك مع حقول النظام الأساسية:')).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-import-mapping-desktop.png' });

    // Advance to Step 3: Preview
    await page.getByRole('button', { name: 'التحقق ومعاينة الصفوف' }).click();
    await expect(page.getByText('إجمالي الصفوف')).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-import-preview-desktop.png' });

    // Confirm Import to Step 4: Reconciliation
    await page.getByRole('button', { name: 'بدء استيراد الأصناف المعتمدة' }).click();
    await expect(page.getByText('اكتملت عملية استيراد الكتالوج')).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-import-reconciliation-desktop.png' });

    await page.getByRole('button', { name: 'تم بنجاح' }).click();

    // ----------------------------------------------------
    // SCENARIO 10: Mobile 390px Responsive Viewport
    // ----------------------------------------------------
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/catalog');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'كتالوج المنتجات والخدمات' })).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-list-mobile-390.png', fullPage: true });

    await page.getByRole('button', { name: 'مياه معدنية 500 مل' }).click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'مياه معدنية 500 مل' })).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/catalog-record-mobile-390.png', fullPage: true });
  });
});

test.describe('Catalog Real-Stack Canonical Seed Acceptance (No Route Interception)', () => {
  test('authenticates with real backend and captures unfiltered canonical seed catalog list', async ({ page }) => {
    test.setTimeout(90_000);
    // Explicitly NO page.route interception in this suite
    await page.setViewportSize({ width: 1440, height: 900 });

    // Navigate to /catalog
    await page.goto('/catalog');
    await page.waitForLoadState('networkidle');

    // If redirected to login (AuthView), log in with real Atlas admin credentials
    const emailInput = page.locator('#login-email');
    if (await emailInput.isVisible({ timeout: 5000 }).catch(() => false)) {
      await emailInput.fill('admin@atlas.com');
      await page.locator('#login-password').fill('Admin@123456');
      await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
      await page.waitForLoadState('networkidle');
    }

    // Ensure on /catalog with real live session
    if (!page.url().includes('/catalog')) {
      await page.goto('/catalog');
      await page.waitForLoadState('networkidle');
    }

    await expect(page.getByRole('heading', { name: 'كتالوج المنتجات والخدمات' })).toBeVisible({ timeout: 15000 });

    // Wait for the real product rows and total count to appear from the database
    await expect(page.getByRole('grid')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText('113')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('row').nth(1)).toBeVisible({ timeout: 15000 });

    // Capture unfiltered real desktop list screenshot
    await page.screenshot({
      path: 'artifacts/test-results/visual-acceptance/catalog-list-canonical-seed-real-desktop.png',
      fullPage: true
    });
  });
});
