import { test, expect } from '@playwright/test';

const atlasParty = {
  id: 'atlas-party-001',
  partyType: 'Organization',
  status: 'Active',
  roles: [
    { code: 'Customer', status: 'Active', version: 2 },
    { code: 'Supplier', status: 'Active', version: 1 }
  ],
  displayName: 'شركة أطلس للتوزيع',
  legalName: 'شركة أطلس للتوزيع والتجارة ذ.م.م',
  nameAr: 'شركة أطلس للتوزيع',
  nameEn: 'Atlas Distribution Co.',
  identifiers: [
    { id: 'atlas-vat', type: 'TaxId', value: '300123456700003', countryCode: 'EG', isPrimary: true, verificationStatus: 'Verified' },
    { id: 'atlas-cr', type: 'CommercialRegister', value: '145902', countryCode: 'EG', isPrimary: false, verificationStatus: 'Verified' }
  ],
  contactPoints: [
    { id: 'atlas-email', type: 'Email', value: 'info@atlas.example', isPrimary: true },
    { id: 'atlas-phone', type: 'Phone', value: '+201012345678', isPrimary: true }
  ],
  contactPersons: [
    { id: 'atlas-cp-1', displayName: 'مريم حسن', role: 'المبيعات', status: 'Active' },
    { id: 'atlas-cp-2', displayName: 'خالد سامح', role: 'المشتريات', status: 'Active' }
  ],
  addresses: [
    { id: 'atlas-billing', usage: 'Billing', countryCode: 'EG', city: 'القاهرة', line1: 'شارع النيل 12', isDefault: true },
    { id: 'atlas-shipping', usage: 'Shipping', countryCode: 'EG', city: 'الجيزة', line1: 'منطقة المخازن 4', isDefault: false }
  ],
  createdAt: '2026-01-15T10:00:00Z',
  updatedAt: '2026-10-10T10:00:00Z',
  version: 3
};

const duplicateParty = {
  ...atlasParty,
  id: 'atlas-party-002',
  displayName: 'أطلس للتوزيع',
  legalName: 'أطلس للتوزيع',
  roles: [{ code: 'Customer', status: 'Active', version: 1 }],
  identifiers: [{ id: 'dup-vat', type: 'TaxId', value: '300123456700003', countryCode: 'EG', isPrimary: true, verificationStatus: 'Verified' }],
  contactPersons: [],
  addresses: [{ id: 'dup-address', usage: 'General', countryCode: 'EG', city: 'القاهرة', line1: 'شارع النيل 12', isDefault: true }]
};

test.describe('Directory visual acceptance corrections', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/session/context', route => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ authenticated: true, tenantId: 'tenant-atlas', memberships: [{ id: 'atlas', tenantId: 'tenant-atlas', displayName: 'شركة أطلس' }], context: { legalEntities: [], branches: [] } })
    }));
    await page.route('**/api/v1/modules', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 'home' }, { id: 'directory' }, { id: 'settings' }]) }));
    await page.route('**/api/v1/authorization/check*', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ allowed: true }) }));
    await page.route(/\/api\/v1\/directory\/parties(\?.*)?$/, async route => {
      const search = new URL(route.request().url()).searchParams.get('search');
      const items = search ? [atlasParty, duplicateParty].filter(p => p.displayName.includes(search) || p.legalName.includes(search)) : [atlasParty, duplicateParty];
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ items, totalCount: items.length, page: 1, pageSize: 25 }) });
    });
    await page.route('**/api/v1/directory/parties/**', async route => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith('/readiness')) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ partyId: atlasParty.id, ready: false, missing: ['Customer role. Legal identifier'], warnings: [] }) });
      }
      if (path.endsWith('/duplicate-candidates')) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: duplicateParty.id, displayName: duplicateParty.displayName, reason: 'Tax identifier match', status: 'Pending' }]) });
      }
      if (path.endsWith('/audit')) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 'audit-1', action: 'Updated', entityType: 'Party', entityId: atlasParty.id, reason: 'تصحيح الاسم التجاري بعد مراجعة العميل', occurredAt: '2026-10-10T10:00:00Z', afterJson: '{"displayName":"شركة أطلس للتوزيع"}' }]) });
      }
      const id = path.split('/').pop();
      const party = id === duplicateParty.id ? duplicateParty : atlasParty;
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(party) });
    });
    await page.route('**/api/v1/directory/parties/import', async route => {
      const body = route.request().postDataJSON() as { commit?: boolean };
      const committed = Boolean(body.commit);
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ operationId: 'import-atlas-001', committed, rows: [
          { externalReference: 'IMP-001', action: committed ? 'Created' : 'Create', partyId: committed ? atlasParty.id : null, errors: [], possibleMatches: [] },
          { externalReference: 'IMP-002', action: committed ? 'Review' : 'Review', partyId: null, errors: [], possibleMatches: [duplicateParty.id] }
        ] })
      });
    });
    await page.route('**/api/v1/directory/parties/*/merge', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ masterPartyId: atlasParty.id, sourcePartyId: duplicateParty.id, status: 'Merged', contributors: [] }) }));
  });

  test('captures corrected desktop directory states', async ({ page }) => {
    await page.goto('/directory');
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-list-desktop.png', fullPage: true });

    await page.getByRole('button', { name: 'استيراد سجلات' }).click();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-import-upload-desktop.png', fullPage: true });
    await page.getByRole('button', { name: 'استخدام بيانات تجريبية جاهزة للمعاينة' }).click();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-import-mapping-desktop.png', fullPage: true });
    await page.getByRole('button', { name: /فحص البيانات/ }).click();
    await expect(page.getByText('3. التحقق ومراجعة التكرار')).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-import-duplicate-review-desktop.png', fullPage: true });
    await page.getByRole('button', { name: 'معاينة التغييرات قبل التنفيذ' }).click();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-import-preview-desktop.png', fullPage: true });
    await page.getByRole('button', { name: 'تأكيد واستيراد السجلات' }).click();
    await expect(page.getByText('اكتملت عملية استيراد الأطراف التجارية بنجاح')).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-import-reconciliation-desktop.png', fullPage: true });

    await page.getByRole('button', { name: 'Close dialog' }).click();
    await page.getByRole('button', { name: 'شركة أطلس للتوزيع' }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: 'شركة أطلس للتوزيع', exact: true })).toBeVisible();
    await expect(page.getByText('بحاجة لاستكمال')).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-record-evidence-desktop.png', fullPage: true });
    await page.getByRole('button', { name: 'جهات الاتصال' }).click();
    await expect(page.getByText('مريم حسن')).toBeVisible();
    await expect(page.getByText('خالد سامح')).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-contact-persons-desktop.png', fullPage: true });
    await page.getByRole('button', { name: 'العناوين' }).click();
    await expect(page.getByText('شارع النيل 12')).toBeVisible();
    await expect(page.getByText('منطقة المخازن 4')).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-billing-shipping-addresses-desktop.png', fullPage: true });
    await page.getByRole('button', { name: 'النظرة العامة' }).click();
    await page.getByRole('button', { name: 'دمج السجل', exact: true }).click();
    await expect(page.getByText('السجل التجاري الرئيسي (الطرف الباقي المستهدف)')).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-merge-picker-desktop.png', fullPage: true });
    await page.getByRole('button', { name: 'Close dialog' }).click();
    await page.getByRole('button', { name: 'سجل الأنشطة' }).click();
    await expect(page.getByText('تصحيح الاسم التجاري بعد مراجعة العميل')).toBeVisible();
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-audit-history-desktop.png', fullPage: true });
  });

  test('captures corrected 390px directory and login surfaces', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/directory');
    await page.waitForLoadState('networkidle');
    const directoryWidth = await page.locator('body').evaluate(element => element.scrollWidth);
    expect(directoryWidth).toBeLessThanOrEqual(390);
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/directory-mobile-390.png', fullPage: true });

    await page.unroute('**/api/v1/session/context');
    await page.route('**/api/v1/session/context', route => route.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ authenticated: false }) }));
    await page.reload();
    await page.waitForLoadState('networkidle');
    const loginWidth = await page.locator('body').evaluate(element => element.scrollWidth);
    expect(loginWidth).toBeLessThanOrEqual(390);
    await page.screenshot({ path: 'artifacts/test-results/visual-acceptance/login-mobile-390.png', fullPage: true });
  });
});
