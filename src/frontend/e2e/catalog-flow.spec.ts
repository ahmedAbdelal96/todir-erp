import { test, expect } from '@playwright/test';

test.describe('Catalog / Product Master Browser Flow', () => {
  const uom = { id: 'uom-piece', code: 'PCS', nameAr: 'قطعة', nameEn: 'Piece', dimension: 'Count', allowFraction: false, precision: 0, status: 'Active' };
  const product = { id: 'product-001', defaultVariantId: 'variant-001', displayName: 'ورق A4', nature: 'Goods', sku: 'PAP-A4', baseUomCode: 'PCS', categoryName: 'قرطاسية', brandName: 'TODIR', status: 'Active', updatedAt: '2026-10-09T10:00:00Z', version: 1, barcodes: [], createdAt: '2026-10-09T10:00:00Z' };
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/session/context', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ authenticated: true, tenantId: 'tenant-demo', memberships: [{ id: 'm1', tenantId: 'tenant-demo', displayName: 'مؤسسة التجارة' }], context: { legalEntities: [], branches: [] } }) }));
    await page.route('**/api/v1/modules', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 'home' }, { id: 'catalog' }] ) }));
    await page.route('**/api/v1/authorization/check*', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ allowed: true }) }));
    await page.route('**/api/v1/catalog/uoms', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([uom]) }));
    await page.route('**/api/v1/catalog/categories', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 'category-001', name: 'قرطاسية', parentId: null, status: 'Active', version: 1 }]) }));
    await page.route('**/api/v1/catalog/brands', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 'brand-001', name: 'TODIR', status: 'Active', version: 1 }]) }));
    await page.route('**/api/v1/catalog/products*', async route => { if (route.request().method() === 'POST') return route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify(product) }); return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ items: [product], totalCount: 1, page: 1, pageSize: 25 }) }); });
  });
  test('lists a product and opens the create flow in RTL', async ({ page }) => {
    await page.goto('/#catalog'); await page.waitForLoadState('networkidle');
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl'); await expect(page.getByRole('heading', { name: 'كتالوج المنتجات والخدمات' })).toBeVisible(); await expect(page.getByRole('button', { name: 'ورق A4' })).toBeVisible(); await expect(page.getByText('PAP-A4')).toBeVisible();
    await page.getByRole('button', { name: 'إضافة منتج أو خدمة' }).click(); await expect(page.getByRole('heading', { name: 'إضافة منتج أو خدمة' })).toBeVisible(); await expect(page.getByText('الخدمة تستخدم وحدة قياس مثل ساعة أو زيارة دون افتراض مخزون.', { exact: false })).not.toBeVisible();
    await page.getByLabel('اسم المنتج / الخدمة').fill('استشارة'); await page.getByLabel('وحدة الأساس *').selectOption('uom-piece'); await page.getByRole('button', { name: 'حفظ المنتج' }).click(); await expect(page.getByRole('heading', { name: 'إضافة منتج أو خدمة' })).not.toBeVisible();
  });
});
