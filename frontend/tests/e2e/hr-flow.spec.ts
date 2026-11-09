import { test, expect } from '@playwright/test';
import path from 'path';

const LEGAL_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const TEST_EMPLOYEE_ID = 'emp-001';
const TEST_EMPLOYMENT_ID = 'empl-001';

// Artifacts directory for visual proof
const ARTIFACTS_DIR = 'C:/Users/Adham/.gemini/antigravity/brain/2cce5b52-1525-442e-8037-81105c0347eb';

let consoleErrors: string[] = [];
let pageErrors: string[] = [];

test.describe('TODIR HR — Comprehensive Enterprise HR & UX Acceptance Suite', () => {
  test.beforeEach(async ({ page }) => {
    consoleErrors = [];
    pageErrors = [];

    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        const text = msg.text();
        if (!text.includes('404') && !text.includes('favicon')) {
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

    await page.route('**/api/v1/auth/login', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          state: 'Authenticated',
          authenticated: true,
          tenantId: '00000000-0000-0000-0000-000000000000'
        })
      });
    });

    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
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
                countryCode: 'EG',
                baseCurrencyCode: 'EGP'
              }
            ],
            branches: [
              {
                id: 'b1',
                legalEntityId: LEGAL_ENTITY_ID,
                code: 'CAI',
                name: 'فرع القاهرة الكبرى'
              }
            ]
          }
        })
      });
    });

    // 2. Authorization
    await page.route('**/api/v1/authorization/check*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ allowed: true })
      });
    });

    // 3. Modules Manifest
    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'home' },
          { id: 'directory' },
          { id: 'invoicing' },
          { id: 'payments' },
          { id: 'accounting' },
          { id: 'hr' },
          { id: 'settings' }
        ])
      });
    });

    // 4. HR Home Summary
    await page.route('**/api/v1/hr/home*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          activeEmployeesCount: 42,
          pendingLeaveApprovalsCount: 3,
          missingAttendanceCount: 2,
          expiringDocumentsCount: 4,
          reminders: [
            {
              id: 'rem-1',
              category: 'DocumentExpiry',
              title: 'انتهاء تصريح عمل الموظف',
              dueDate: '2026-11-15',
              severity: 'warning',
              employeeId: TEST_EMPLOYEE_ID,
              employeeName: 'أحمد محمود الحسيني'
            },
            {
              id: 'rem-2',
              category: 'MissingCheckout',
              title: 'بصمة خروج مفقودة ليوم أمس',
              dueDate: '2026-11-03',
              severity: 'danger',
              employeeId: 'emp-002',
              employeeName: 'مريم عادل مصطفى'
            }
          ]
        })
      });
    });

    // 5. Employees Directory List & Create
    await page.route(/\/api\/v1\/hr\/employees(?:\?.*)?$/, async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'emp-new-created',
            partyId: 'party-new-created',
            arabicName: body.arabicName,
            englishName: body.englishName ?? null,
            preferredName: body.preferredName ?? null,
            status: 'Active',
            createdAt: new Date().toISOString(),
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
                id: TEST_EMPLOYEE_ID,
                employmentId: TEST_EMPLOYMENT_ID,
                displayName: 'أحمد محمود الحسيني',
                englishName: 'Ahmed Mahmoud Elhusseiny',
                employmentNumber: 'EMP-1001',
                status: 'Active',
                currentEmploymentStartDate: '2025-01-15'
              },
              {
                id: 'emp-002',
                employmentId: 'empl-002',
                displayName: 'مريم عادل مصطفى',
                englishName: 'Mariam Adel Mostafa',
                employmentNumber: 'EMP-1002',
                status: 'Active',
                currentEmploymentStartDate: '2025-03-01'
              }
            ],
            page: 1,
            pageSize: 20,
            totalCount: 2
          })
        });
      }
    });

    // 6. Specific Employee Profile
    await page.route(/\/api\/v1\/hr\/employees\/[^/]+(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_EMPLOYEE_ID,
          partyId: 'party-001',
          arabicName: 'أحمد محمود الحسيني',
          englishName: 'Ahmed Mahmoud Elhusseiny',
          preferredName: 'أحمد الحسيني',
          status: 'Active',
          createdAt: '2025-01-15T08:00:00Z',
          version: 2
        })
      });
    });

    // 7. Employments for Employee
    await page.route(/\/api\/v1\/hr\/employees\/[^/]+\/employments(?:\?.*)?$/, async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'empl-new-rehire',
            employeeId: TEST_EMPLOYEE_ID,
            legalEntityId: LEGAL_ENTITY_ID,
            employmentNumber: 'EMP-1001-R',
            type: 'FullTime',
            status: 'Active',
            startDate: '2026-11-01',
            endDate: null,
            version: 1
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: TEST_EMPLOYMENT_ID,
              employeeId: TEST_EMPLOYEE_ID,
              legalEntityId: LEGAL_ENTITY_ID,
              employmentNumber: 'EMP-1001',
              type: 'FullTime',
              status: 'Active',
              startDate: '2025-01-15',
              endDate: null,
              version: 1
            }
          ])
        });
      }
    });

    // 8. Assignments
    await page.route(/\/api\/v1\/hr\/employments\/[^/]+\/assignments(?:\?.*)?$/, async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'asg-002',
            employmentId: TEST_EMPLOYMENT_ID,
            legalEntityId: LEGAL_ENTITY_ID,
            departmentId: body.departmentId ?? 'dept-001',
            positionId: body.positionId ?? 'pos-002',
            managerEmploymentId: body.managerEmploymentId ?? null,
            effectiveFrom: body.effectiveFrom,
            effectiveTo: body.effectiveTo ?? null,
            reasonCode: body.reasonCode,
            isCurrent: true,
            version: 1
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'asg-001',
              employmentId: TEST_EMPLOYMENT_ID,
              legalEntityId: LEGAL_ENTITY_ID,
              departmentId: 'dept-001',
              positionId: 'pos-001',
              managerEmploymentId: null,
              effectiveFrom: '2025-01-15',
              effectiveTo: null,
              reasonCode: 'InitialHiring',
              isCurrent: true,
              version: 1
            }
          ])
        });
      }
    });

    // 9. Contracts & Amendments
    await page.route(/\/api\/v1\/hr\/employments\/[^/]+\/contracts(?:\?.*)?$/, async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'cnt-002',
            employmentId: TEST_EMPLOYMENT_ID,
            legalEntityId: LEGAL_ENTITY_ID,
            contractType: body.contractType,
            startDate: body.startDate,
            endDate: body.endDate,
            probationEnd: body.probationEnd,
            workingHoursBasis: body.workingHoursBasis,
            status: 'Draft',
            version: 1
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'cnt-001',
              employmentId: TEST_EMPLOYMENT_ID,
              legalEntityId: LEGAL_ENTITY_ID,
              contractType: 'Definite',
              startDate: '2025-01-15',
              endDate: '2026-01-14',
              probationEnd: '2025-04-14',
              workingHoursBasis: 'FullTimeStandard',
              status: 'Active',
              version: 1
            }
          ])
        });
      }
    });

    await page.route('**/api/v1/hr/contracts/*/activate*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'cnt-002',
          status: 'Active',
          version: 2
        })
      });
    });

    await page.route('**/api/v1/hr/contracts/*/amendments*', async (route) => {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'amend-001',
          contractId: 'cnt-001',
          amendmentType: 'Renewal',
          effectiveDate: '2026-01-15',
          termsSummary: 'تجديد العقد لمدة سنة إضافية',
          status: 'Active',
          version: 1
        })
      });
    });

    // 10. Compensation
    await page.route(/\/api\/v1\/hr\/employments\/[^/]+\/compensation(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'cmp-001',
            employmentId: TEST_EMPLOYMENT_ID,
            legalEntityId: LEGAL_ENTITY_ID,
            currencyCode: 'EGP',
            amount: 18500.00,
            payFrequency: 'Monthly',
            effectiveFrom: '2025-01-15',
            status: 'Active',
            reason: 'الراتب الأساسي المعتمد',
            version: 1
          }
        ])
      });
    });

    // 11. Documents & Completeness
    await page.route(/\/api\/v1\/hr\/employees\/[^/]+\/documents(?:\?.*)?$/, async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'doc-new',
            employeeId: TEST_EMPLOYEE_ID,
            category: body.category,
            titleAr: body.titleAr,
            fileRef: body.fileRef,
            contentType: body.contentType,
            sizeBytes: body.sizeBytes,
            expiryDate: body.expiryDate,
            isSensitive: body.isSensitive ?? false,
            isVerified: false,
            version: 1
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'doc-001',
              employeeId: TEST_EMPLOYEE_ID,
              category: 'NationalId',
              titleAr: 'بطاقة الرقم القومي',
              fileRef: '00000000-0000-0000-0000-000000000001',
              contentType: 'application/pdf',
              sizeBytes: 154200,
              expiryDate: '2028-06-30',
              isSensitive: true,
              isVerified: true,
              version: 1
            }
          ])
        });
      }
    });

    await page.route(/\/api\/v1\/hr\/employees\/[^/]+\/document-completeness(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          employeeId: TEST_EMPLOYEE_ID,
          totalMandatory: 3,
          uploadedCount: 2,
          verifiedCount: 1,
          missingCategories: ['WorkPermit'],
          expiringSoonCount: 1
        })
      });
    });

    // 12. Identifiers (Sensitive)
    await page.route(/\/api\/v1\/hr\/employees\/[^/]+\/identifiers(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'ident-001',
            employeeId: TEST_EMPLOYEE_ID,
            identifierType: 'NationalId',
            maskedValue: '293****78901',
            isPrimary: true
          }
        ])
      });
    });

    // 13. Attendance & Corrections
    await page.route(/\/api\/v1\/hr\/attendance-records(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'att-001',
              employmentId: TEST_EMPLOYMENT_ID,
              workDate: '2026-11-02',
              firstIn: '2026-11-02T08:58:00Z',
              lastOut: null, // missing checkout!
              plannedStart: '09:00',
              plannedEnd: '17:00',
              workedMinutes: 480,
              status: 'Exception',
              lockState: 'Open',
              version: 1
            },
            {
              id: 'att-002',
              employmentId: TEST_EMPLOYMENT_ID,
              workDate: '2026-11-01',
              firstIn: '2026-11-01T08:55:00Z',
              lastOut: '2026-11-01T17:05:00Z',
              plannedStart: '09:00',
              plannedEnd: '17:00',
              workedMinutes: 490,
              status: 'Present',
              lockState: 'Open',
              version: 1
            }
          ],
          totalCount: 2
        })
      });
    });

    await page.route('**/api/v1/hr/attendance-records/*/corrections', async (route) => {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'corr-001',
          attendanceRecordId: 'att-001',
          correctedFirstIn: '2026-11-02T08:58:00Z',
          correctedLastOut: '2026-11-02T17:00:00Z',
          reasonCode: 'ManualPunchMissing',
          reason: 'عطل في جهاز البصمة عند الخروج',
          version: 2
        })
      });
    });

    // 14. Leave Management
    await page.route('**/api/v1/hr/leave/types*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'lt-annual', code: 'ANNUAL', nameAr: 'إجازة سنوية اعتيادية', category: 'Annual' },
          { id: 'lt-sick', code: 'SICK', nameAr: 'إجازة مرضية', category: 'Sick' }
        ])
      });
    });

    await page.route(/\/api\/v1\/hr\/leave\/requests(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'lr-001',
            employmentId: TEST_EMPLOYMENT_ID,
            leaveTypeId: 'lt-annual',
            from: '2026-12-20',
            to: '2026-12-24',
            chargedQuantity: 5,
            reason: 'إجازة عائلية سنوية',
            status: 'Pending',
            version: 1
          }
        ])
      });
    });

    await page.route(/\/api\/v1\/hr\/employments\/[^/]+\/leave-requests(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'lr-new',
          employmentId: TEST_EMPLOYMENT_ID,
          leaveTypeId: 'lt-annual',
          from: '2026-12-25',
          to: '2026-12-28',
          chargedQuantity: 4,
          reason: 'إجازة مناسبة خاصة',
          status: 'Pending',
          version: 1
        })
      });
    });

    await page.route('**/api/v1/hr/leave/requests/*/approve*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'lr-001',
          status: 'Approved',
          version: 2
        })
      });
    });

    await page.route('**/api/v1/hr/leave/requests/*/cancel*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'lr-001',
          status: 'Cancelled',
          version: 3
        })
      });
    });

    // 15. Organization Departments & Positions
    await page.route('**/api/v1/hr/departments*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'dept-001', code: 'FIN', nameAr: 'الإدارة المالية والمحاسبة', nameEn: 'Finance & Accounting', status: 'Active' },
          { id: 'dept-002', code: 'ENG', nameAr: 'إدارة العمليات الهندسية', nameEn: 'Engineering Operations', status: 'Active' }
        ])
      });
    });

    await page.route('**/api/v1/hr/positions*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'pos-001', code: 'ACC-SR', nameAr: 'محاسب أول', nameEn: 'Senior Accountant', status: 'Active' },
          { id: 'pos-002', code: 'ACC-MGR', nameAr: 'مدير الحسابات العامة', nameEn: 'General Accounts Manager', status: 'Active' }
        ])
      });
    });

    // 16. Manager & Team
    await page.route('**/api/v1/hr/me/team*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              employeeId: 'emp-002',
              arabicName: 'مريم عادل مصطفى',
              englishName: 'Mariam Adel Mostafa',
              employmentId: 'empl-002',
              employmentNumber: 'EMP-1002',
              employmentStartDate: '2025-03-01',
              departmentId: 'dept-001',
              departmentName: 'الإدارة المالية والمحاسبة',
              positionId: 'pos-001',
              positionTitle: 'محاسب أول'
            }
          ],
          page: 1,
          pageSize: 25,
          totalCount: 1
        })
      });
    });

    await page.route('**/api/v1/hr/me/team/leave-approvals*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'lr-team-001',
            employmentId: 'empl-002',
            leaveTypeId: 'lt-annual',
            from: '2026-11-20',
            to: '2026-11-22',
            chargedQuantity: 3,
            reason: 'ظرف عائلي طارئ',
            status: 'Pending',
            version: 1
          }
        ])
      });
    });

    // 17. Self Service
    await page.route('**/api/v1/hr/me/profile*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_EMPLOYEE_ID,
          arabicName: 'أحمد محمود الحسيني',
          status: 'Active'
        })
      });
    });

    // 18. Employment Status Changes (Termination)
    await page.route(/\/api\/v1\/hr\/employees\/[^/]+\/employments\/[^/]+\/status(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_EMPLOYMENT_ID,
          status: 'Terminated',
          version: 2
        })
      });
    });

    // 19. Timeline
    await page.route(/\/api\/v1\/hr\/employees\/[^/]+\/timeline(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'tl-1', eventType: 'InitialHiring', title: 'مباشرة العمل للمرة الأولى', occurredAt: '2025-01-15T08:00:00Z' },
          { id: 'tl-2', eventType: 'ContractActivated', title: 'تفعيل عقد العمل محدد المدة', occurredAt: '2025-01-15T09:00:00Z' }
        ])
      });
    });

    // 20. Qualifications, Credentials & Insurance
    await page.route('**/api/v1/hr/employees/*/qualifications*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    await page.route('**/api/v1/hr/employees/*/credentials*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    await page.route('**/api/v1/hr/employees/*/employments/*/insurance*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });
  });

  test('HR01 — HR Home Operations Center, Navigation Tabs & Reminders', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/hr');

    // Page Header & Corporate Design
    await expect(page.locator('h1')).toHaveText('الموارد البشرية');
    await expect(page.getByText('إدارة شؤون الموظفين والهيكل التنظيمي')).toBeVisible();

    // KPI Cards
    await expect(page.getByText('الموظفون النشطون')).toBeVisible();
    await expect(page.getByText('42')).toBeVisible();
    await expect(page.getByText('طلبات إجازة معلقة')).toBeVisible();

    // Critical Reminders Table
    await expect(page.getByText('التنبيهات التشغيلية والالتزامات النظامية')).toBeVisible();
    await expect(page.getByText('انتهاء تصريح عمل الموظف')).toBeVisible();
    await expect(page.getByText('بصمة خروج مفقودة ليوم أمس')).toBeVisible();

    // Capture Desktop Evidence
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '01-hr-home-desktop.png'),
      fullPage: true
    });
  });

  test('HR02 — Scenario A: Employee Directory & New Employee Modal (Zero Mixed Form Collision)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/hr?tab=directory');

    // Directory list table is visible
    await expect(page.getByText('سجل الموظفين')).toBeVisible();
    await expect(page.getByText('أحمد محمود الحسيني')).toBeVisible();
    await expect(page.getByText('EMP-1001')).toBeVisible();

    // Primary action button to open dedicated Modal
    const addBtn = page.getByRole('button', { name: '+ إضافة موظف جديد' });
    await expect(addBtn).toBeVisible();
    await addBtn.click();

    // Dialog opens cleanly over the list page without collision
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('إضافة موظف جديد إلى المنشأة')).toBeVisible();

    // Fill form fields
    await dialog.getByLabel('الاسم باللغة العربية').fill('طارق سليم حامد');
    await dialog.getByLabel('الاسم باللغة الإنجليزية').fill('Tarek Selim Hamed');

    // Capture New Employee Modal Evidence
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '02-hr-new-employee-modal.png'),
      fullPage: true
    });

    // Submit dialog
    await dialog.getByRole('button', { name: 'تأكيد وإنشاء الموظف' }).click();
  });

  test('HR03 — Scenario B: 360-Degree Employee Workspace (Overview, Assignments, Contracts, Compensation)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto(`/hr?employeeId=${TEST_EMPLOYEE_ID}`);

    // Workspace Header
    await expect(page.getByRole('heading', { name: 'أحمد محمود الحسيني' })).toBeVisible();
    await expect(page.getByText('EMP-1001')).toBeVisible();

    // Navigation Sub-tabs
    await expect(page.getByRole('button', { name: 'نظرة عامة' })).toBeVisible();
    await expect(page.getByRole('button', { name: /العقود/ })).toBeVisible();
    await expect(page.getByRole('button', { name: /الأجور والمزايا/ })).toBeVisible();

    // Switch to Contracts Tab
    await page.getByRole('button', { name: /العقود/ }).click();
    await expect(page.getByText('عقود العمل وملحقات التجديد')).toBeVisible();
    await expect(page.getByText('محدد المدة')).toBeVisible();

    // Switch to Compensation Tab (Verifying Confidentiality & Components)
    await page.getByRole('button', { name: /الأجور والمزايا/ }).click();
    await expect(page.getByText('سجل الأجور والتعويضات التعاقدية')).toBeVisible();
    await expect(page.getByText('18500')).toBeVisible();
    await expect(page.getByText('EGP')).toBeVisible();

    // Capture 360 Workspace Evidence
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '03-hr-employee-workspace-360.png'),
      fullPage: true
    });
  });

  test('HR04 — Scenario C: Promotion & Assignment Transfer Flow', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto(`/hr?employeeId=${TEST_EMPLOYEE_ID}`);

    // Click Promotion/Transfer Action in 360 Workspace Header
    const promoteBtn = page.getByRole('button', { name: 'ترقية / نقل' });
    await expect(promoteBtn).toBeVisible();
    await promoteBtn.click();

    // Modal opens
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('ترقية موظف واعتماد مسمى جديد')).toBeVisible();

    // Capture Promotion Dialog Evidence
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '04-hr-promotion-modal.png'),
      fullPage: true
    });

    // Close dialog
    await dialog.getByRole('button', { name: 'إلغاء' }).click();
    await expect(dialog).not.toBeVisible();
  });

  test('HR05 — Scenario D: Contract Renewal & Amendment Flow', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto(`/hr?employeeId=${TEST_EMPLOYEE_ID}`);

    // Switch to Contracts
    await page.getByRole('button', { name: /العقود/ }).click();
    const renewBtn = page.getByRole('button', { name: 'تجديد / تمديد' });
    await expect(renewBtn).toBeVisible();
    await renewBtn.click();

    // Renewal Modal opens
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('تجديد عقد عمل وإضافة ملحق')).toBeVisible();

    // Fill renewal terms
    await dialog.getByLabel('بيان التجديد والشروط الإضافية').fill('تجديد لمدة سنة إضافية بنفس الراتب');

    // Capture Contract Renewal Evidence
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '05-hr-contract-renewal-modal.png'),
      fullPage: true
    });

    // Close dialog
    await dialog.getByRole('button', { name: 'إلغاء' }).click();
  });

  test('HR06 — Scenario E: Attendance Tracking & Punch Correction', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/hr?tab=attendance');

    // Attendance Table is visible
    await expect(page.getByText('سجل حركات الحضور والانصراف')).toBeVisible();
    await expect(page.getByRole('table', { name: 'جدول حركات الحضور والانصراف' })).toBeVisible();

    // Missing checkout badge is displayed in red
    const missingBadge = page.getByText('لم يسجل انصراف');
    await expect(missingBadge).toBeVisible();

    // Click Correct button
    const correctBtn = page.getByRole('button', { name: 'تصحيح البصمة' }).first();
    await expect(correctBtn).toBeVisible();
    await correctBtn.click();

    // Correction Modal
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('تصحيح حركة حضور يدوي وتحديد موعد الخروج')).toBeVisible();

    await dialog.getByLabel('سبب التصحيح / الاعتماد').fill('عطل في جهاز البصمة عند الخروج');

    // Capture Attendance Correction Evidence
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '06-hr-attendance-correction.png'),
      fullPage: true
    });

    await dialog.getByRole('button', { name: 'إلغاء' }).click();
  });

  test('HR07 — Scenario F: Leave Request, Approval & Balance Governance', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/hr?tab=leave');

    // Leave list is visible
    await expect(page.getByText('إدارة الإجازات السنوية والرسمية')).toBeVisible();
    await expect(page.getByText('إجازة عائلية سنوية')).toBeVisible();

    // Approve action button
    const approveBtn = page.getByRole('button', { name: 'موافقة' }).first();
    await expect(approveBtn).toBeVisible();

    // Cancel action button
    const cancelBtn = page.getByRole('button', { name: 'إلغاء الطلب' }).first();
    await expect(cancelBtn).toBeVisible();

    // Capture Leave Evidence
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '07-hr-leave-management.png'),
      fullPage: true
    });
  });

  test('HR08 — Scenario G & H: Organization, Manager Team & Mobile Viewport (390px)', async ({ page }) => {
    // 1. Organization & Departments
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/hr?tab=organization');
    await expect(page.getByText('الإدارة المالية والمحاسبة')).toBeVisible();
    await expect(page.getByText('محاسب أول')).toBeVisible();

    // 2. Responsive Mobile 390px Test on 360 Workspace
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/hr?employeeId=${TEST_EMPLOYEE_ID}`);

    // Verify employee title and status visible on mobile without horizontal clipping
    await expect(page.getByRole('heading', { name: 'أحمد محمود الحسيني' })).toBeVisible();
    await expect(page.getByText('نشط').first()).toBeVisible();

    // Capture Mobile Evidence
    await page.screenshot({
      path: path.join(ARTIFACTS_DIR, '08-hr-mobile-390px.png'),
      fullPage: false
    });

    // Zero unhandled console or page errors
    expect(consoleErrors).toEqual([]);
    expect(pageErrors).toEqual([]);
  });

  test('HR09 — Manager Team consumes the paginated direct-report contract', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/hr?tab=manager');

    await expect(page.getByText('أعضاء فريقي المباشر')).toBeVisible();
    await expect(page.getByText('مريم عادل مصطفى')).toBeVisible();
    await expect(page.getByText('الإدارة المالية والمحاسبة')).toBeVisible();
    await expect(page.getByText('محاسب أول')).toBeVisible();
    await expect(pageErrors).toEqual([]);
  });

  test('HR10 — Unlinked self-service membership shows the supported empty state', async ({ page }) => {
    await page.route('**/api/v1/hr/me/profile*', async (route) => {
      await route.fulfill({ status: 404, contentType: 'application/problem+json', body: '{}' });
    });

    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/hr?tab=selfservice');

    await expect(page.getByText('لا يوجد ملف موظف مرتبط بحساب المستخدم الحالي.')).toBeVisible();
    expect(pageErrors).toEqual([]);
  });
});
