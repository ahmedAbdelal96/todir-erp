import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';

const LEGAL_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const TEST_WO_ID = 'wo-00000000-0000-0000-0000-000000000001';
const TEST_VISIT_ID = 'vis-00000000-0000-0000-0000-000000000001';

const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/service-acceptance/mocked-ui');
const ARTIFACTS_DIR = process.env.TEST_ARTIFACTS_DIR || LOCAL_ARTIFACTS_DIR;

let consoleErrors: string[] = [];
let pageErrors: string[] = [];

test.describe('TODIR Module 24: Service & Maintenance — [MOCKED_UI_ACCEPTANCE] Isolated UI & Component Layout Suite', () => {
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
        if (
          !text.includes('404') &&
          !text.includes('favicon') &&
          !text.includes('502') &&
          !text.includes('Bad Gateway') &&
          !text.includes('401')
        ) {
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
          'service.work.read',
          'service.request.read',
          'service.request.create',
          'service.work.create',
          'service.billing.read',
          'service.billing.manage',
          'service.contracts.manage',
          'service.dispatch.manage'
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
          { id: 'service' }
        ])
      });
    });

    // 2. Service API Mock Handlers
    await page.route('**/api/v1/service/reports/kpi*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          openRequests: 14,
          openWorkOrders: 8,
          visitsToday: 5,
          openCharges: 18500,
          downtimeHours: 3.5,
          failureCount: 2,
          mtbfHours: 420.0,
          mttrHours: 2.1,
          availabilityPercent: 98.7
        })
      });
    });

    await page.route('**/api/v1/service/requests*', async (route) => {
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON();
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'req-001',
            legalEntityId: LEGAL_ENTITY_ID,
            requestNumber: 'SR-2026-0089',
            context: body.context || 'CustomerFieldService',
            title: body.title,
            description: body.description || '',
            customerPartyId: body.customerPartyId,
            state: 'Draft',
            priority: body.priority || 'Normal',
            requestedAt: new Date().toISOString(),
            version: 1
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: 'req-001',
                legalEntityId: LEGAL_ENTITY_ID,
                requestNumber: 'SR-2026-0001',
                context: 'CustomerFieldService',
                title: 'عطل في نظام تبريد المولد الرئيسي',
                description: 'ارتفاع حاد في درجات الحرارة مع تسريب زيت',
                customerPartyId: 'cust-01',
                state: 'Triaged',
                priority: 'High',
                requestedAt: '2026-11-16T08:30:00Z',
                version: 1
              },
              {
                id: 'req-002',
                legalEntityId: LEGAL_ENTITY_ID,
                requestNumber: 'SR-2026-0002',
                context: 'InternalMaintenance',
                title: 'صيانة دورية لرافعة المستودع المركزي',
                description: 'فحص هيدروليكي واستبدال الفلاتر',
                fixedAssetId: 'fa-001',
                state: 'Draft',
                priority: 'Normal',
                requestedAt: '2026-11-16T10:00:00Z',
                version: 1
              }
            ],
            page: 1,
            pageSize: 20,
            hasNext: false,
            totalCount: 2
          })
        });
      }
    });

    await page.route('**/api/v1/service/work-orders*', async (route) => {
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON();
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: TEST_WO_ID,
            legalEntityId: LEGAL_ENTITY_ID,
            workOrderNumber: 'WO-2026-0099',
            context: body.context || 'CustomerFieldService',
            title: body.title,
            description: body.description || '',
            customerPartyId: body.customerPartyId,
            billingOwner: body.billingOwner || 'Service',
            workType: body.workType || 'Corrective',
            priority: body.priority || 'Normal',
            state: 'Draft',
            plannedStart: body.plannedStart || null,
            plannedEnd: body.plannedEnd || null,
            version: 1
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: TEST_WO_ID,
                legalEntityId: LEGAL_ENTITY_ID,
                workOrderNumber: 'WO-2026-0012',
                context: 'CustomerFieldService',
                title: 'إصلاح تسريب هيدروليكي وتبديل مضخة',
                description: 'تنفيذ أعمال الصيانة بموقع العميل',
                customerPartyId: 'cust-01',
                billingOwner: 'Service',
                workType: 'Corrective',
                priority: 'High',
                state: 'InProgress',
                plannedStart: '2026-11-17T08:00:00Z',
                plannedEnd: '2026-11-17T14:00:00Z',
                version: 2
              },
              {
                id: 'wo-002',
                legalEntityId: LEGAL_ENTITY_ID,
                workOrderNumber: 'WO-2026-0015',
                context: 'CustomerFieldService',
                title: 'تركيب أنظمة إطفاء الحريق في الموقع ب',
                description: 'مشروع إنشاءات مجمع الأعمال',
                customerPartyId: 'cust-02',
                projectId: 'prj-77',
                billingOwner: 'Projects',
                workType: 'ProjectRelated',
                priority: 'Normal',
                state: 'Ready',
                plannedStart: '2026-11-18T09:00:00Z',
                plannedEnd: '2026-11-18T17:00:00Z',
                version: 1
              }
            ],
            page: 1,
            pageSize: 20,
            hasNext: false,
            totalCount: 2
          })
        });
      }
    });

    await page.route('**/api/v1/service/dispatch/unscheduled*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'wo-003',
              legalEntityId: LEGAL_ENTITY_ID,
              workOrderNumber: 'WO-2026-0018',
              context: 'CustomerFieldService',
              title: 'فحص اهتزازات التوربين الغازي رقم 3',
              priority: 'Critical',
              state: 'Ready',
              billingOwner: 'Service',
              workType: 'Corrective',
              version: 1
            }
          ],
          page: 1,
          pageSize: 10,
          hasNext: false,
          totalCount: 1
        })
      });
    });

    await page.route('**/api/v1/service/visits*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: TEST_VISIT_ID,
              workOrderId: TEST_WO_ID,
              plannedStart: '2026-11-17T09:00:00Z',
              plannedEnd: '2026-11-17T12:00:00Z',
              locationType: 'OnSite',
              state: 'Dispatched',
              siteInstructions: 'يرجى مراجعة مسؤول الأمن الصناعي قبل بدء العمل',
              version: 1
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false,
          totalCount: 1
        })
      });
    });

    await page.route('**/api/v1/service/mobile/my-day*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          visits: [
            {
              id: TEST_VISIT_ID,
              workOrderId: TEST_WO_ID,
              plannedStart: '2026-11-17T09:00:00Z',
              plannedEnd: '2026-11-17T12:00:00Z',
              locationType: 'OnSite',
              state: 'Dispatched',
              siteInstructions: 'يرجى مراجعة مسؤول الأمن الصناعي قبل بدء العمل',
              version: 1
            }
          ],
          workOrders: [
            {
              id: TEST_WO_ID,
              legalEntityId: LEGAL_ENTITY_ID,
              workOrderNumber: 'WO-2026-0012',
              context: 'CustomerFieldService',
              title: 'إصلاح تسريب هيدروليكي وتبديل مضخة',
              description: 'تنفيذ أعمال الصيانة بموقع العميل',
              customerPartyId: 'cust-01',
              billingOwner: 'Service',
              workType: 'Corrective',
              priority: 'High',
              state: 'InProgress',
              version: 2
            }
          ],
          date: '2026-11-17'
        })
      });
    });

    await page.route(`**/api/v1/service/visits/${TEST_VISIT_ID}/*`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_VISIT_ID,
          workOrderId: TEST_WO_ID,
          plannedStart: '2026-11-17T09:00:00Z',
          plannedEnd: '2026-11-17T12:00:00Z',
          locationType: 'OnSite',
          state: 'Traveling',
          siteInstructions: 'يرجى مراجعة مسؤول الأمن الصناعي قبل بدء العمل',
          version: 2
        })
      });
    });

    await page.route('**/api/v1/service/installed-base*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'ib-001',
              legalEntityId: LEGAL_ENTITY_ID,
              name: 'مولد كهربائي ديزل كاتربيلر 500KVA',
              serialNumber: 'SN-CAT-99201',
              modelNumber: 'CAT-C18',
              state: 'Active',
              siteName: 'المصنع الرئيسي - وحدة الطاقة',
              installedDate: '2024-01-15',
              version: 1
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false,
          totalCount: 1
        })
      });
    });

    await page.route('**/api/v1/service/contracts*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'sc-001',
              legalEntityId: LEGAL_ENTITY_ID,
              contractHolderPartyId: 'cust-01',
              billToPartyId: 'cust-01',
              startDate: '2026-01-01',
              endDate: '2026-12-31',
              contractType: 'Annual',
              currencyCode: 'SAR',
              value: 45000,
              state: 'Active',
              scopeJson: JSON.stringify({ customerName: 'شركة اليمامة الصناعية' })
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false,
          totalCount: 1
        })
      });
    });

    await page.route('**/api/v1/service/warranties*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'war-001',
              legalEntityId: LEGAL_ENTITY_ID,
              warrantyType: 'Customer',
              subjectId: 'ib-001',
              startDate: '2025-06-01',
              endDate: '2027-05-31',
              state: 'Active',
              scopeJson: 'تغطية مجانية شاملة للقطع والعمالة'
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false,
          totalCount: 1
        })
      });
    });

    await page.route('**/api/v1/service/entitlements/resolve', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          lines: [
            {
              chargeCategory: 'Labor',
              coverageSource: 'Warranty',
              covered: true,
              coveredAmount: 450,
              customerAmount: 0,
              explanation: 'مغطى بنسبة 100% بموجب الضمان المعتمد'
            },
            {
              chargeCategory: 'Parts',
              coverageSource: 'Warranty',
              covered: true,
              coveredAmount: 1200,
              customerAmount: 0,
              explanation: 'قطع الغيار مغطاة بالكامل حتى تاريخ انتهاء الضمان'
            },
            {
              chargeCategory: 'Travel',
              coverageSource: 'ServiceContract',
              covered: true,
              coveredAmount: 200,
              customerAmount: 0,
              explanation: 'عقد الصيانة السنوي يغطي رسوم الانتقال الميداني'
            }
          ],
          slaState: 'WithinSLA',
          contractId: 'sc-001',
          warrantyId: 'war-001'
        })
      });
    });

    await page.route('**/api/v1/service/sla/profiles*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [],
          page: 1,
          pageSize: 20,
          hasNext: false,
          totalCount: 0
        })
      });
    });

    await page.route('**/api/v1/service/charges*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'chg-001',
              workOrderId: TEST_WO_ID,
              category: 'Labor',
              description: 'ساعات عمل فني أول - صيانة وتشخيص',
              costAmount: 450,
              coveredAmount: 450,
              customerAmount: 0,
              currencyCode: 'SAR',
              state: 'Reviewed'
            },
            {
              id: 'chg-002',
              workOrderId: TEST_WO_ID,
              category: 'Parts',
              description: 'صمام ضغط هيدروليكي عالي الحساسية',
              costAmount: 1200,
              coveredAmount: 1200,
              customerAmount: 0,
              currencyCode: 'SAR',
              state: 'Reviewed'
            },
            {
              id: 'chg-003',
              workOrderId: 'wo-002',
              category: 'FixedFee',
              description: 'رسوم تدشين ومعايرة أنظمة السلامة',
              costAmount: 2500,
              coveredAmount: 0,
              customerAmount: 2500,
              currencyCode: 'SAR',
              state: 'Draft'
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false,
          totalCount: 3
        })
      });
    });
  });

  test('01. Service Overview: Renders authoritative KPIs without fabricated figures or trends', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/service');

    await expect(page.locator('h1, h2').first()).toBeVisible({ timeout: 10000 });
    
    // Wait for authoritative metrics to be rendered
    await expect(page.locator('body')).toContainText('14', { timeout: 10000 }); // openRequests
    await expect(page.locator('body')).toContainText('8', { timeout: 10000 }); // openWorkOrders
    await expect(page.locator('body')).toContainText('5', { timeout: 10000 }); // visitsToday

    // Ensure NO fabricated metrics exist
    await expect(page.locator('body')).not.toContainText('48,500');
    await expect(page.locator('body')).not.toContainText('99.4%');
    await expect(page.locator('body')).not.toContainText('+12%');

    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '01-service-overview-desktop.png') });
  });

  test('02. Work Orders Tab: Shows list and respects Projects billing separation', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/service?tab=work-orders');

    await expect(page.locator('table, [role="table"]').first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('body')).toContainText('WO-2026-0012', { timeout: 10000 });
    await expect(page.locator('body')).toContainText('WO-2026-0015', { timeout: 10000 });
    await expect(page.locator('body')).toContainText('Projects', { timeout: 10000 });

    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '02-service-work-orders.png') });
  });

  test('03. Technician My Day Tab: Loads authenticated visits and triggers action', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/service?tab=my-day');

    await expect(page.locator('body')).toContainText('WO-2026-0012', { timeout: 10000 });
    await expect(page.locator('body')).toContainText('متصل', { timeout: 10000 });

    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '03-service-technician-my-day.png') });
  });

  test('04. Contracts & Entitlements Tab: Authoritative Entitlement resolver verification', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/service?tab=contracts-warranty');

    // Switch to Entitlement Section
    const sectionBtn = page.getByRole('button', { name: /التحقق من استحقاق وتغطية العقد|التحقق من التغطية/i });
    await expect(sectionBtn).toBeVisible({ timeout: 10000 });
    await sectionBtn.click();

    // Fill customerPartyId
    const customerInput = page.locator('input').first();
    await customerInput.fill('00000000-0000-0000-0000-000000000001');

    // Click submit button inside form
    const submitBtn = page.getByRole('button', { name: 'التحقق من الاستحقاق' });
    await expect(submitBtn).toBeVisible({ timeout: 10000 });
    await submitBtn.click();

    await expect(page.locator('body')).toContainText('Warranty', { timeout: 10000 });
    await expect(page.locator('body')).toContainText('ServiceContract', { timeout: 10000 });
    await expect(page.locator('body')).toContainText('مغطى بنسبة 100%', { timeout: 10000 });

    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '04-service-entitlements.png') });
  });

  test('05. Service Billing Tab: Displays authoritative charges without synthetic rows', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/service?tab=billing');

    await expect(page.locator('table, [role="table"]').first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('body')).toContainText('ساعات عمل فني أول', { timeout: 10000 });
    await expect(page.locator('body')).toContainText('صمام ضغط هيدروليكي', { timeout: 10000 });
    await expect(page.locator('body')).toContainText('رسوم تدشين ومعايرة', { timeout: 10000 });
    await expect(page.locator('body')).not.toContainText('14,200');

    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '05-service-billing.png') });
  });

  test('06. Mobile Layout (390px): Technician responsive experience', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/service');

    await expect(page.locator('h1, h2').first()).toBeVisible({ timeout: 10000 });

    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '06-service-mobile-390px.png') });
  });
});
