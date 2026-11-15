import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';

const LEGAL_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const TEST_PROJECT_ID = 'prj-00000000-0000-0000-0000-000000000001';
const TEST_CAPITAL_PROJECT_ID = 'prj-00000000-0000-0000-0000-000000000002';

const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/projects-acceptance');
const AGENT_BRAIN_DIR = 'C:/Users/IT/.gemini/antigravity/brain/2b36897c-7b21-48b3-b339-9bb22437719d/projects-acceptance';
const ARTIFACTS_DIR = process.env.TEST_ARTIFACTS_DIR || LOCAL_ARTIFACTS_DIR;

let consoleErrors: string[] = [];
let pageErrors: string[] = [];

test.describe('TODIR Projects & Operations (Module 23) — Mocked UI & Visual Interaction Suite', () => {
  test.beforeAll(() => {
    [LOCAL_ARTIFACTS_DIR, AGENT_BRAIN_DIR].forEach((dir) => {
      try {
        if (!fs.existsSync(dir)) {
          fs.mkdirSync(dir, { recursive: true });
        }
      } catch {
        // ignore directory creation error if outside local permissions
      }
    });
  });

  test.beforeEach(async ({ page }) => {
    consoleErrors = [];
    pageErrors = [];

    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        const text = msg.text();
        if (!text.includes('404') && !text.includes('favicon') && !text.includes('502') && !text.includes('Bad Gateway') && !text.includes('401')) {
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
            { id: 'atlas', tenantId: '00000000-0000-0000-0000-000000000000', displayName: 'مجموعة أطلس للمقاولات' }
          ],
          context: {
            displayName: 'مجموعة أطلس للمقاولات',
            legalEntities: [
              {
                id: LEGAL_ENTITY_ID,
                legalEntityId: LEGAL_ENTITY_ID,
                code: 'MAIN',
                name: 'مجموعة أطلس للمقاولات - المركز الرئيسي',
                countryCode: 'EG',
                baseCurrencyCode: 'EGP'
              }
            ],
            branches: [
              {
                id: 'b1',
                legalEntityId: LEGAL_ENTITY_ID,
                code: 'CAI',
                name: 'فرع القاهرة الرئيسي'
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
          'admin',
          'projects.read',
          'projects.create',
          'projects.update',
          'projects.financial.read',
          'projects.time.approve',
          'projects.close'
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

    await page.route('**/api/v1/me/navigation-preferences**', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    });

    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'home' },
          { id: 'projects' }
        ])
      });
    });

    // 2. Mock Projects Data
    const mockProjects = [
      {
        id: TEST_PROJECT_ID,
        legalEntityId: LEGAL_ENTITY_ID,
        projectNumber: 'PRJ-2026-001',
        name: 'برج الأمل السكني والتجاري',
        description: 'مشروع إنشاء وتشييد متكامل يشمل 20 طابقاً مع مواقف سفلية ومحلات تجارية',
        projectType: 'CustomerProject',
        customerPartyId: 'pty-00000000-0001',
        projectManagerEmployeeId: 'emp-00000000-0001',
        startDate: '2026-01-01',
        targetEndDate: '2027-06-30',
        actualEndDate: null,
        lifecycleState: 'Active',
        executionHealth: 'OnTrack',
        billingModel: 'TimeAndMaterials',
        currencyCode: 'EGP',
        version: 1,
        budget: 5000000,
        actualCost: 1850000,
        openCommitment: 450000,
        etc: 2700000,
        eac: 5000000,
        billingEligible: 620000,
        invoiced: 1200000,
        collected: 1000000,
        outstanding: 200000,
        sourceType: 'Quotation',
        sourceRef: 'QUO-2026-042'
      },
      {
        id: TEST_CAPITAL_PROJECT_ID,
        legalEntityId: LEGAL_ENTITY_ID,
        projectNumber: 'PRJ-2026-002',
        name: 'توسعة مصنع المواد الخرسانية',
        description: 'مشروع رأسمالي داخلي لتوسعة خط الإنتاج وتوريد خلاطات مركزية جديدة',
        projectType: 'CapitalProject',
        customerPartyId: null,
        projectManagerEmployeeId: 'emp-00000000-0002',
        startDate: '2026-02-15',
        targetEndDate: '2026-11-30',
        actualEndDate: null,
        lifecycleState: 'Active',
        executionHealth: 'OnTrack',
        billingModel: 'CostPlus',
        currencyCode: 'EGP',
        version: 1,
        budget: 3200000,
        actualCost: 1100000,
        openCommitment: 250000,
        etc: 1850000,
        eac: 3200000,
        billingEligible: 0,
        invoiced: 0,
        collected: 0,
        outstanding: 0,
        sourceType: 'Internal',
        sourceRef: 'CAP-2026-001'
      }
    ];

    // Mock Projects List Route
    await page.route(`**/api/v1/projects/legal-entities/${LEGAL_ENTITY_ID}**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: mockProjects,
          page: 1,
          pageSize: 20,
          totalCount: mockProjects.length,
          hasNext: false
        })
      });
    });

    // Mock Single Project Detail Route
    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}`, async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(mockProjects[0])
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(mockProjects[0])
        });
      }
    });

    // Mock Financial Summary
    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/financial-summary`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          budget: 5000000,
          actualCost: 1850000,
          openCommitment: 450000,
          etc: 2700000,
          eac: 5000000,
          billingEligible: 620000,
          invoiced: 1200000,
          collected: 1000000,
          outstanding: 200000,
          expectedMargin: 850000,
          revenueBasis: 'ContractValue',
          revenueBasisAmount: 6000000,
          currencyCode: 'EGP'
        })
      });
    });

    // Mock Tasks
    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/tasks**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'tsk-001',
              projectId: TEST_PROJECT_ID,
              title: 'حفر الأساسات والركائز الخرسانية',
              description: 'أعمال الحفر والصب الأولي للأساسات العميقة',
              status: 'Completed',
              priority: 'High',
              assignedEmployeeId: 'emp-001',
              plannedStart: '2026-01-05',
              plannedEnd: '2026-02-15',
              progressPercent: 100,
              sequenceNo: 1,
              version: 1
            },
            {
              id: 'tsk-002',
              projectId: TEST_PROJECT_ID,
              title: 'صب أعمدة وسقف الطابق الأرضي',
              description: 'شدات خشبية وحديد تسليح وصب خرسانة جاهزة',
              status: 'InProgress',
              priority: 'High',
              assignedEmployeeId: 'emp-002',
              plannedStart: '2026-02-16',
              plannedEnd: '2026-03-30',
              progressPercent: 65,
              sequenceNo: 2,
              version: 1
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });

    // Mock Milestones
    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/milestones**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'mls-001',
              projectId: TEST_PROJECT_ID,
              taskId: 'tsk-001',
              name: 'إتمام الأساسات الخرسانية واعتماد الاستشاري',
              dueDate: '2026-02-20',
              reachedDate: '2026-02-18',
              acceptedDate: '2026-02-22',
              status: 'Accepted',
              sequenceNo: 1,
              weightPercent: 20,
              version: 1
            },
            {
              id: 'mls-002',
              projectId: TEST_PROJECT_ID,
              taskId: 'tsk-002',
              name: 'إتمام الهيكل الخرساني للطابق الثالث',
              dueDate: '2026-05-15',
              reachedDate: null,
              acceptedDate: null,
              status: 'Planned',
              sequenceNo: 2,
              weightPercent: 25,
              version: 1
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });

    // Mock Deliverables
    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/deliverables**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'dlv-001',
              projectId: TEST_PROJECT_ID,
              taskId: 'tsk-001',
              name: 'تقرير اختبار جودة الخرسانة المعتمد من المختبر',
              title: 'تقرير اختبار جودة الخرسانة المعتمد من المختبر',
              description: 'شهادات تكسير المكعبات الخرسانية ومطابقة المواصفات',
              status: 'Accepted',
              submittedDate: '2026-02-19',
              acceptedDate: '2026-02-21',
              version: 1
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });

    // Mock Team
    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/team**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'tm-001',
              projectId: TEST_PROJECT_ID,
              employeeId: 'emp-00000000-0001',
              roleCode: 'مدير المشروع',
              allocationPercent: 100,
              activeFrom: '2026-01-01',
              activeTo: null,
              isManager: true,
              version: 1
            },
            {
              id: 'tm-002',
              projectId: TEST_PROJECT_ID,
              employeeId: 'emp-00000000-0002',
              roleCode: 'مهندس موقع مدني',
              allocationPercent: 100,
              activeFrom: '2026-01-10',
              activeTo: null,
              isManager: false,
              version: 1
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });

    // Mock Requirements & Allocations
    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/resources/requirements**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'req-001',
              projectId: TEST_PROJECT_ID,
              roleCode: 'مشرف كهرباء',
              quantity: 2,
              requiredFrom: '2026-04-01',
              requiredTo: '2026-08-30',
              status: 'Open',
              version: 1
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });

    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/resources/allocations**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'alc-001',
              projectId: TEST_PROJECT_ID,
              resourceRequirementId: 'req-001',
              employeeId: 'emp-003',
              resourceRef: 'م. أحمد الشريف',
              allocationPercent: 100,
              allocatedFrom: '2026-04-01',
              allocatedTo: '2026-08-30',
              status: 'Confirmed',
              version: 1
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });

    // Mock Time Entries
    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/time**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'tim-001',
              projectId: TEST_PROJECT_ID,
              taskId: 'tsk-002',
              employeeId: 'emp-002',
              workDate: '2026-03-10',
              durationMinutes: 480,
              billable: true,
              state: 'Approved',
              hourlyRate: 250,
              totalCharge: 2000,
              notes: 'إشراف مباشر على صب سقف الطابق الأول وفحص العينات',
              version: 1
            },
            {
              id: 'tim-002',
              projectId: TEST_PROJECT_ID,
              taskId: 'tsk-002',
              employeeId: 'emp-002',
              workDate: '2026-03-11',
              durationMinutes: 480,
              billable: true,
              state: 'Submitted',
              hourlyRate: 250,
              totalCharge: 2000,
              notes: 'فحص حديد التسليح واستلام الاستشاري للأعمدة',
              version: 1
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });

    // Mock Baselines, Documents & Timeline
    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/baselines**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'bsl-001',
              projectId: TEST_PROJECT_ID,
              baselineType: 'Scope',
              versionNo: 1,
              approvedAt: '2026-01-02',
              approvedBy: 'adm-001'
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });

    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/documents**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'doc-001',
              projectId: TEST_PROJECT_ID,
              documentRef: 'DOC-2026-CTR-01',
              documentType: 'Contract',
              sensitivity: 'Confidential',
              createdAt: '2026-01-01'
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });

    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/timeline**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'tml-001',
              projectId: TEST_PROJECT_ID,
              eventType: 'ProjectCreated',
              description: 'تم إنشاء المشروع ورسم خطة البداية',
              occurredAt: '2026-01-01T08:00:00Z',
              performedBy: 'م. أحمد خالد'
            },
            {
              id: 'tml-002',
              projectId: TEST_PROJECT_ID,
              eventType: 'MilestoneReached',
              description: 'تم اعتماد إتمام الأساسات الخرسانية',
              occurredAt: '2026-02-18T14:30:00Z',
              performedBy: 'الاستشاري المشرف'
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });

    // Mock Close Readiness & Exceptions
    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/close`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          projectId: TEST_PROJECT_ID,
          state: 'ExecutionCompleted',
          blockers: [
            'توجد ساعات عمل مرسلة لم تعتمد بعد'
          ]
        })
      });
    });

    await page.route(`**/api/v1/projects/${TEST_PROJECT_ID}/close-exceptions**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'exc-001',
              projectId: TEST_PROJECT_ID,
              exceptionType: 'TOLERANCE_OVERRUN',
              message: 'استثناء تجاوز مؤقت لنسبة الهامش في أعمال الحفر',
              resolvedAt: null
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });

    // Mock Resource Load & Templates
    await page.route(`**/api/v1/projects/resource-load**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            resourceRef: 'م. أحمد الشريف',
            totalAllocationPercent: 100,
            projectAllocations: [
              { projectNumber: 'PRJ-2026-001', allocationPercent: 100 }
            ]
          }
        ])
      });
    });

    await page.route(`**/api/v1/projects/templates**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'tpl-001',
              code: 'TPL-CONSTRUCTION',
              name: 'نموذج مشاريع التشييد والإنشاءات الكبرى',
              projectType: 'CustomerProject',
              version: 1,
              status: 'Active'
            }
          ],
          page: 1,
          pageSize: 20,
          hasNext: false
        })
      });
    });
  });

  const saveArtifact = async (page: any, filename: string) => {
    const localPath = path.join(LOCAL_ARTIFACTS_DIR, filename);
    await page.screenshot({ path: localPath, fullPage: true });

    try {
      const brainPath = path.join(AGENT_BRAIN_DIR, filename);
      fs.copyFileSync(localPath, brainPath);
    } catch {
      // brain directory copy optional
    }
  };

  test('01 — Project Portfolio Workspace & KPIs (RTL Arabic)', async ({ page }) => {
    await page.goto('/projects');
    await page.waitForLoadState('networkidle');

    // Verify Title & Subtitle
    await expect(page.locator('h1')).toContainText('إدارة المشاريع');
    
    // Verify Action Buttons
    await expect(page.locator('[data-testid="btn-new-project"]')).toBeVisible();
    await expect(page.locator('[data-testid="btn-from-sales"]')).toBeVisible();

    // Verify KPI Cards
    await expect(page.locator('[data-testid="kpi-total-projects"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-active-projects"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-customer-projects"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-ontrack-projects"]')).toBeVisible();

    // Verify Project Rows in Table
    await expect(page.getByText('PRJ-2026-001')).toBeVisible();
    await expect(page.getByText('برج الأمل السكني والتجاري')).toBeVisible();
    await expect(page.getByText('PRJ-2026-002')).toBeVisible();

    // Capture Visual Evidence
    await saveArtifact(page, '01-projects-portfolio-workspace.png');

    expect(consoleErrors.length).toBe(0);
    expect(pageErrors.length).toBe(0);
  });

  test('02 — Project Creation Dialogs (New Project & From Sales)', async ({ page }) => {
    await page.goto('/projects');
    await page.waitForLoadState('networkidle');

    // Open New Project Modal
    await page.locator('[data-testid="btn-new-project"]').click();
    await expect(page.getByText('إنشاء مشروع جديد')).toBeVisible();
    await expect(page.locator('input[type="text"]').first()).toBeVisible();

    // Capture New Project Dialog
    await saveArtifact(page, '02-projects-creation-modal.png');

    // Close Dialog
    await page.getByRole('button', { name: 'إلغاء' }).click();
    await expect(page.getByText('إنشاء مشروع جديد')).not.toBeVisible();

    expect(consoleErrors.length).toBe(0);
    expect(pageErrors.length).toBe(0);
  });

  test('03 — 360 Project Workspace — Financial Summary & KPIs', async ({ page }) => {
    await page.goto(`/projects/${TEST_PROJECT_ID}?tab=financials`);
    await page.waitForLoadState('networkidle');

    // Header & Badges
    await expect(page.getByText('برج الأمل السكني والتجاري')).toBeVisible();
    await expect(page.getByText('PRJ-2026-001')).toBeVisible();
    await expect(page.getByText('نشط وقيد التنفيذ')).toBeVisible();

    // Financial KPI Cards
    await expect(page.locator('[data-testid="kpi-budget"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-actual-cost"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-open-commitments"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-etc"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-eac"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-expected-margin"]')).toBeVisible();

    // Financial Actions
    await expect(page.locator('[data-testid="btn-record-cost"]')).toBeVisible();
    await expect(page.locator('[data-testid="btn-record-po"]')).toBeVisible();
    await expect(page.locator('[data-testid="btn-create-forecast"]')).toBeVisible();

    // Capture Financial Summary
    await saveArtifact(page, '03-projects-financial-summary.png');

    expect(consoleErrors.length).toBe(0);
    expect(pageErrors.length).toBe(0);
  });

  test('04 — Tasks, Milestones & Deliverables Workflow', async ({ page }) => {
    await page.goto(`/projects/${TEST_PROJECT_ID}?tab=tasks`);
    await page.waitForLoadState('networkidle');

    // Check Tasks
    await expect(page.getByText('حفر الأساسات والركائز الخرسانية')).toBeVisible();
    await expect(page.getByText('صب أعمدة وسقف الطابق الأرضي')).toBeVisible();
    await expect(page.locator('[data-testid="btn-add-task"]')).toBeVisible();

    // Check Milestones subtab
    await page.locator('[data-testid="subtab-milestones"]').click();
    await expect(page.getByText('إتمام الأساسات الخرسانية واعتماد الاستشاري')).toBeVisible();
    await expect(page.locator('[data-testid="btn-add-milestone"]')).toBeVisible();

    // Check Deliverables subtab
    await page.locator('[data-testid="subtab-deliverables"]').click();
    await expect(page.getByText('تقرير اختبار جودة الخرسانة المعتمد من المختبر')).toBeVisible();

    // Capture Tasks & Milestones
    await saveArtifact(page, '04-projects-tasks-milestones.png');

    expect(consoleErrors.length).toBe(0);
    expect(pageErrors.length).toBe(0);
  });

  test('05 — Team & Generic Resource Allocation', async ({ page }) => {
    await page.goto(`/projects/${TEST_PROJECT_ID}?tab=team`);
    await page.waitForLoadState('networkidle');

    // Verify Team Members
    await expect(page.getByRole('cell', { name: 'مدير المشروع' })).toBeVisible();
    await expect(page.getByText('مهندس موقع مدني')).toBeVisible();
    await expect(page.locator('[data-testid="btn-add-team"]')).toBeVisible();

    // Subtab: Requirements
    await page.locator('[data-testid="subtab-requirements"]').click();
    await expect(page.getByText('مشرف كهرباء')).toBeVisible();

    // Subtab: Allocations
    await page.locator('[data-testid="subtab-allocations"]').click();
    await expect(page.getByText('م. أحمد الشريف')).toBeVisible();

    // Capture Team & Allocations
    await saveArtifact(page, '05-projects-team-allocations.png');

    expect(consoleErrors.length).toBe(0);
    expect(pageErrors.length).toBe(0);
  });

  test('06 — Timesheets & Hourly Rate Approvals', async ({ page }) => {
    await page.goto(`/projects/${TEST_PROJECT_ID}?tab=time`);
    await page.waitForLoadState('networkidle');

    // Verify Timesheet Rows
    await expect(page.locator('[data-testid="btn-log-time"]')).toBeVisible();
    await expect(page.getByText('إشراف مباشر على صب سقف الطابق الأول وفحص العينات')).toBeVisible();
    await expect(page.getByText('فحص حديد التسليح واستلام الاستشاري للأعمدة')).toBeVisible();

    // Capture Timesheets
    await saveArtifact(page, '06-projects-timesheets.png');

    expect(consoleErrors.length).toBe(0);
    expect(pageErrors.length).toBe(0);
  });

  test('07 — Billing, Invoicing & Charge Generation', async ({ page }) => {
    await page.goto(`/projects/${TEST_PROJECT_ID}?tab=billing`);
    await page.waitForLoadState('networkidle');

    // Actions
    await expect(page.locator('[data-testid="btn-new-rule"]')).toBeVisible();
    await expect(page.locator('[data-testid="btn-add-plan"]')).toBeVisible();
    await expect(page.locator('[data-testid="btn-generate-charges"]')).toBeVisible();
    await expect(page.locator('[data-testid="btn-create-charge"]')).toBeVisible();
    await expect(page.locator('[data-testid="btn-request-invoice"]')).toBeVisible();

    // KPI Cards
    await expect(page.locator('[data-testid="kpi-billing-eligible"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-invoiced"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-collected"]')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-outstanding"]')).toBeVisible();

    // Capture Billing & Invoicing
    await saveArtifact(page, '07-projects-billing-invoicing.png');

    expect(consoleErrors.length).toBe(0);
    expect(pageErrors.length).toBe(0);
  });

  test('08 — Change Orders & CIP Capital Handoff', async ({ page }) => {
    await page.goto(`/projects/${TEST_PROJECT_ID}?tab=changes`);
    await page.waitForLoadState('networkidle');

    // Change Orders Tab
    await expect(page.locator('[data-testid="btn-new-change"]')).toBeVisible();
    await expect(page.locator('[data-testid="btn-create-baseline"]')).toBeVisible();

    // Capital Handoff Tab
    await page.locator('[data-testid="tab-capital"]').click();
    await expect(page.locator('[data-testid="btn-capital-handoff"]')).toBeVisible();

    // Capture Changes & Capital
    await saveArtifact(page, '08-projects-changes-capital.png');

    expect(consoleErrors.length).toBe(0);
    expect(pageErrors.length).toBe(0);
  });

  test('09 — Documents, Timeline Audit & Close Readiness Checklist', async ({ page }) => {
    await page.goto(`/projects/${TEST_PROJECT_ID}?tab=closing`);
    await page.waitForLoadState('networkidle');

    // Verify Blocker Banner
    await expect(page.getByText('توجد ساعات عمل مرسلة لم تعتمد بعد')).toBeVisible();
    await expect(page.locator('[data-testid="btn-add-exception"]')).toBeVisible();
    await expect(page.locator('[data-testid="btn-close-project"]')).toBeDisabled();

    // Capture Closing Checklist
    await saveArtifact(page, '09-projects-closing-checklist.png');

    expect(consoleErrors.length).toBe(0);
    expect(pageErrors.length).toBe(0);
  });

  test('10 — English LTR Viewport & Internationalization Parity', async ({ page }) => {
    // Set language to English
    await page.route('**/api/v1/me/preferences', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ language: 'en', theme: 'light' })
      });
    });

    await page.goto('/projects');
    await page.waitForLoadState('networkidle');

    // Verify English Headings
    await expect(page.locator('h1')).toContainText(/Projects/i);
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');

    // Capture English LTR
    await saveArtifact(page, '10-projects-english-ltr.png');

    expect(consoleErrors.length).toBe(0);
    expect(pageErrors.length).toBe(0);
  });

  test('11 — Mobile 390px Viewport Responsive Check', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/projects/${TEST_PROJECT_ID}?tab=financials`);
    await page.waitForLoadState('networkidle');

    // Verify Mobile Layout
    await expect(page.getByText('برج الأمل السكني والتجاري')).toBeVisible();
    await expect(page.locator('[data-testid="kpi-budget"]')).toBeVisible();

    // Capture Mobile Screenshot
    await saveArtifact(page, '11-projects-mobile-390px.png');

    expect(consoleErrors.length).toBe(0);
    expect(pageErrors.length).toBe(0);
  });
});
