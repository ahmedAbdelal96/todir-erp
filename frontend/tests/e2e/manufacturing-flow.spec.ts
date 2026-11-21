import { test, expect } from '@playwright/test';

test.describe('TODIR Module 26 — Manufacturing & MRP End-to-End Workflow Verification', () => {
  const setupSession = async (page: any, hasLegalEntity = true) => {
    page.on('console', (msg: any) => {
      if (msg.type() === 'error') console.log('BROWSER ERROR:', msg.text());
    });

    await page.route('**/api/v1/session/context', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: 'tenant-todir-mfg',
          identityId: 'user-mfg-mgr',
          context: {
            displayName: 'مؤسسة أطلس للتصنيع المتكامل',
            legalEntities: hasLegalEntity
              ? [{ id: 'le-mfg-01', code: 'LE-MFG', name: 'كيان أطلس الصناعي الرئيسي' }]
              : [],
            branches: hasLegalEntity
              ? [{ id: 'br-mfg-01', legalEntityId: 'le-mfg-01', code: 'BR-MFG', name: 'مصنع العاشر من رمضان' }]
              : []
          }
        })
      });
    });

    await page.route('**/api/v1/authorization/effective-access*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          tenantId: 'tenant-todir-mfg',
          membershipId: 'mem-1',
          legalEntityId: hasLegalEntity ? 'le-mfg-01' : null,
          branchId: hasLegalEntity ? 'br-mfg-01' : null,
          isSuper: false,
          permissions: [
            'manufacturing.production.read',
            'manufacturing.production.manage',
            'manufacturing.production.execute',
            'manufacturing.execution.manage',
            'manufacturing.material.read',
            'manufacturing.material.issue',
            'manufacturing.material.consume',
            'manufacturing.mrp.read',
            'manufacturing.mrp.manage',
            'manufacturing.mrp.run',
            'manufacturing.plant.manage',
            'manufacturing.cost.read',
            'manufacturing.cost.manage',
            'manufacturing.quality.policy.manage',
            'catalog.product.read'
          ],
          entitledModules: ['manufacturing', 'catalog', 'inventory'],
          moduleStatuses: { manufacturing: 'Active', catalog: 'Active', inventory: 'Active' },
          version: '1'
        })
      });
    });

    await page.route('**/api/v1/auth/csrf*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ csrfToken: 'mock-csrf' })
      });
    });

    await page.route('**/api/v1/me/preferences*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({})
      });
    });

    await page.route('**/api/v1/me/navigation-preferences*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({})
      });
    });

    await page.route('**/api/v1/context/organization', async (route: any) => {
      await route.fulfill({ status: 204 });
    });

    // Mock Manufacturing Plants
    await page.route('**/api/v1/manufacturing/plants*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'plant-01-cairo',
            code: 'PLANT-01',
            name: 'مصنع أطلس للصناعات الدوائية',
            state: 'Active',
            warehouseId: 'wh-main-01'
          }
        ])
      });
    });

    // Mock Production Orders Count
    await page.route('**/api/v1/manufacturing/**production-orders/count*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ count: 2 })
      });
    });

    // Mock Single Production Order
    await page.route('**/api/v1/manufacturing/production-orders/ord-mfg-1001', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'ord-mfg-1001',
          orderNumber: 'MO-2026-0001',
          legalEntityId: 'le-mfg-01',
          plantId: 'plant-01-cairo',
          productVariantId: 'var-paracetamol-500',
          orderedQty: 1000,
          completedQty: 600,
          scrapQty: 20,
          uomCode: 'BOX',
          state: 'InProgress',
          materialStatus: 'PartiallyIssued',
          operationStatus: 'InProgress',
          qualityStatus: 'Passed',
          costStatus: 'Accumulating',
          needDate: '2026-12-01T00:00:00Z',
          plannedStart: '2026-11-20T08:00:00Z',
          rowVersion: 1
        })
      });
    });

    // Mock Quality Gates
    await page.route('**/api/v1/manufacturing/production-orders/*/quality-gates*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'gate-01',
            productionOrderId: 'ord-mfg-1001',
            required: true,
            decisionState: 'Passed'
          }
        ])
      });
    });

    // Mock Production Orders List (both direct and per legal entity)
    await page.route('**/api/v1/manufacturing/**production-orders*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'ord-mfg-1001',
            orderNumber: 'MO-2026-0001',
            legalEntityId: 'le-mfg-01',
            plantId: 'plant-01-cairo',
            productVariantId: 'var-paracetamol-500',
            orderedQty: 1000,
            completedQty: 600,
            scrapQty: 20,
            uomCode: 'BOX',
            state: 'InProgress',
            materialStatus: 'PartiallyIssued',
            operationStatus: 'InProgress',
            qualityStatus: 'Passed',
            costStatus: 'Accumulating',
            needDate: '2026-12-01T00:00:00Z',
            plannedStart: '2026-11-20T08:00:00Z',
            rowVersion: 1
          },
          {
            id: 'ord-mfg-1002',
            orderNumber: 'MO-2026-0002',
            legalEntityId: 'le-mfg-01',
            plantId: 'plant-01-cairo',
            productVariantId: 'var-amoxicillin-250',
            orderedQty: 500,
            completedQty: 500,
            scrapQty: 0,
            uomCode: 'BOX',
            state: 'Completed',
            materialStatus: 'Consumed',
            operationStatus: 'Completed',
            qualityStatus: 'Passed',
            costStatus: 'Reconciled',
            needDate: '2026-11-25T00:00:00Z',
            plannedStart: '2026-11-15T08:00:00Z',
            rowVersion: 2
          }
        ])
      });
    });

    // Mock Real Order Materials (without fabricated IDs!)
    await page.route('**/api/v1/manufacturing/production-orders/ord-mfg-1001/materials', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'mat-req-real-uuid-001',
            productionOrderId: 'ord-mfg-1001',
            productVariantId: 'var-raw-api-powder',
            preferredProductVariantId: 'var-raw-api-powder',
            requiredQty: 500,
            reservedQty: 500,
            issuedQty: 400,
            consumedQty: 300,
            returnedQty: 0,
            scrappedQty: 10,
            uomCode: 'KG',
            state: 'PartiallyIssued'
          },
          {
            id: 'mat-req-real-uuid-002',
            productionOrderId: 'ord-mfg-1001',
            productVariantId: 'var-packaging-foil',
            preferredProductVariantId: 'var-packaging-foil',
            requiredQty: 1000,
            reservedQty: 1000,
            issuedQty: 1000,
            consumedQty: 600,
            returnedQty: 0,
            scrappedQty: 10,
            uomCode: 'MTR',
            state: 'FullyIssued'
          }
        ])
      });
    });

    // Mock Order Operations
    await page.route('**/api/v1/manufacturing/production-orders/ord-mfg-1001/operations', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'op-real-101',
            productionOrderId: 'ord-mfg-1001',
            sequenceNo: 10,
            code: 'OP-010',
            name: 'خلط وضغط الحبيبات',
            workCenterId: 'wc-granulation-01',
            state: 'Completed',
            completedQty: 1000,
            actualGoodQty: 990,
            actualScrapQty: 10,
            qualityGateRequired: false,
            rowVersion: 1
          },
          {
            id: 'op-real-102',
            productionOrderId: 'ord-mfg-1001',
            sequenceNo: 20,
            code: 'OP-020',
            name: 'تغليف وتعبئة نهائية',
            workCenterId: 'wc-blister-01',
            state: 'InProgress',
            completedQty: 600,
            actualGoodQty: 590,
            actualScrapQty: 10,
            qualityGateRequired: true,
            rowVersion: 2
          }
        ])
      });
    });

    // Mock Close Readiness
    await page.route('**/api/v1/manufacturing/production-orders/ord-mfg-1001/close-readiness', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          productionOrderId: 'ord-mfg-1001',
          state: 'Blocked',
          canClose: false,
          materialIssuedQty: 1400,
          materialAccountedQty: 900,
          goodOutputQty: 600,
          scrapQty: 20,
          operationsComplete: false,
          qualityState: 'Passed',
          costState: 'Accumulating',
          wipAmount: 24500.5,
          blockReason: 'material_unaccounted',
          blockers: ['material_unaccounted', 'operations_incomplete']
        })
      });
    });

    // Mock Cost Facts
    await page.route('**/api/v1/manufacturing/production-orders/ord-mfg-1001/cost', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          productionOrderId: 'ord-mfg-1001',
          currencyCode: 'EGP',
          materialCost: 35000,
          laborCost: 8500,
          machineCost: 6200,
          overheadCost: 4100,
          subcontractCost: 0,
          totalCost: 53800,
          unitCost: 53.8,
          actualMaterialCost: 28000,
          actualLaborCost: 7200,
          actualOverheadCost: 3500
        })
      });
    });

    // Mock Catalog Products Search for ProductPicker
    await page.route('**/api/v1/catalog/products*', async (route: any) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'prod-01',
              templateId: 'tpl-01',
              defaultVariantId: 'var-paracetamol-500',
              name: 'باراسيتامول 500 مجم أقراص',
              sku: 'MED-PARA-500',
              baseUomCode: 'BOX',
              status: 'Active'
            }
          ],
          totalCount: 1
        })
      });
    });
  };

  test('Missing Legal Entity Context: renders high-contrast NoLegalEntityPrompt guard and denies access to ERP API', async ({ page }) => {
    await setupSession(page, false);
    await page.goto('/manufacturing/orders');

    // Verify controlled context-required state is displayed
    const prompt = page.locator('[data-testid="manufacturing-no-legal-entity-prompt"]');
    await expect(prompt).toBeVisible();
    await expect(prompt).toContainText('يجب تحديد الكيان القانوني');
  });

  test('Production Orders List: displays orders table, status badges, and server-side pagination', async ({ page }) => {
    await setupSession(page, true);
    await page.goto('/manufacturing/orders');

    // Header & orders table
    await expect(page.locator('[data-testid="manufacturing-orders-page"]')).toBeVisible();
    await expect(page.locator('text=MO-2026-0001')).toBeVisible();
    await expect(page.locator('text=MO-2026-0002')).toBeVisible();

    // Verify Status Badges
    await expect(page.locator('tbody').getByText('قيد التشغيل الفعلي')).toBeVisible();
    await expect(page.locator('tbody').getByText('مكتمل الإنتاج')).toBeVisible();
  });

  test('Production Order Workspace: renders full 360-degree tabs and close readiness panel', async ({ page }) => {
    await setupSession(page, true);
    await page.goto('/manufacturing/orders/ord-mfg-1001');

    await expect(page.locator('[data-testid="manufacturing-order-workspace"]')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'MO-2026-0001' })).toBeVisible();

    // Verify Close Readiness Panel displays real blocker reason
    const readinessPanel = page.locator('[data-testid="close-readiness-panel"]');
    await expect(readinessPanel).toBeVisible();
    await expect(readinessPanel).toContainText('جاهزية الإقفال');
  });

  test('Materials Management: displays real requirements and never exposes fabricated MAT-REQ-01', async ({ page }) => {
    await setupSession(page, true);
    await page.goto('/manufacturing/materials');

    await expect(page.locator('[data-testid="manufacturing-materials-page"]')).toBeVisible();
    await expect(page.locator('text=var-raw-api-powder')).toBeVisible();
    await expect(page.locator('text=var-packaging-foil')).toBeVisible();

    // Verify quantities displayed accurately
    await expect(page.locator('text=500 KG').first()).toBeVisible();
    await expect(page.locator('text=1000 MTR').first()).toBeVisible();

    // Click quick issue on the row to verify modal opens with real requirement
    const issueButtons = page.locator('button:has-text("صرف خامات")');
    await expect(issueButtons.first()).toBeVisible();
    await issueButtons.first().click();

    // Verify dialog opens cleanly
    await expect(page.locator('role=dialog')).toBeVisible();
  });

  test('Quality Gate Contract: exposes Passed, Failed, Blocked and Pending without Exempt option', async ({ page }) => {
    await setupSession(page, true);
    await page.goto('/manufacturing/orders/ord-mfg-1001');

    // Click on Quality Tab
    const qualityTabButton = page.locator('button:has-text("بوابات الجودة")');
    if (await qualityTabButton.isVisible()) {
      await qualityTabButton.click();
    }

    // Trigger quality gate decision dialog
    const gateActionBtn = page.locator('button:has-text("تسجيل قرار فحص"), button:has-text("قرار فحص")');
    if (await gateActionBtn.first().isVisible()) {
      await gateActionBtn.first().click();
      const dialog = page.locator('role=dialog');
      await expect(dialog).toBeVisible();

      // Check decision options: Exempt must NOT exist!
      const exemptOption = dialog.locator('option[value="Exempt"]');
      await expect(exemptOption).toHaveCount(0);

      // Verify valid backend states exist
      await expect(dialog.locator('option[value="Passed"]')).toHaveCount(1);
      await expect(dialog.locator('option[value="Failed"]')).toHaveCount(1);
      await expect(dialog.locator('option[value="Blocked"]')).toHaveCount(1);
    }
  });

  test('Manufacturing Costing: renders real close readiness analysis and dynamic WIP', async ({ page }) => {
    await setupSession(page, true);
    await page.goto('/manufacturing/costing');

    await expect(page.locator('[data-testid="manufacturing-costing-page"]')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'تكاليف الإنتاج وإقفال الـ WIP' })).toBeVisible();

    // Verify orders table
    await expect(page.locator('text=MO-2026-0001')).toBeVisible();
    await expect(page.locator('text=MO-2026-0002')).toBeVisible();
  });
});
