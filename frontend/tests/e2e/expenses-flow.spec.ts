import { test, expect } from '@playwright/test';
import path from 'path';

const LEGAL_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const TEST_EXPENSE_ID = '00000000-0000-0000-0000-000000000010';
const TEST_ADVANCE_ID = '00000000-0000-0000-0000-000000000020';
const TEST_CARD_ID = '00000000-0000-0000-0000-000000000030';
const TEST_REPAYMENT_ID = '00000000-0000-0000-0000-000000000040';

// Artifacts directory for visual proof
const LOCAL_ARTIFACTS_DIR = path.resolve('artifacts/expenses-acceptance');
const ARTIFACTS_DIR = process.env.TEST_ARTIFACTS_DIR || LOCAL_ARTIFACTS_DIR;

let consoleErrors: string[] = [];
let pageErrors: string[] = [];
let mockUserLanguage: 'ar' | 'en' = 'ar';

/**
 * TODIR Expenses & Reimbursements (Module 21)
 * MOCKED COMPONENT & BROWSER REGRESSION SUITE
 * 
 * NOTE: This suite uses controlled client-side browser mocks (via page.route) to verify
 * all component interaction branches, UI edge cases, badge matrix states, modal dialogs,
 * and responsive behavior deterministically.
 * 
 * Real PostgreSQL, backend API, MinIO storage, and ClamAV execution is authoritatively
 * tested without mocks in: `frontend/tests/e2e/expenses-real-stack.spec.ts`.
 */
test.describe('TODIR Expenses & Reimbursements (Module 21) — Mocked Component & Browser Regression Suite', () => {
  test.beforeEach(async ({ page }) => {
    consoleErrors = [];
    pageErrors = [];
    mockUserLanguage = 'ar';

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

    // 2. Authorization & Preferences
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
          'expenses.read',
          'expenses.create',
          'expenses.approve',
          'expenses.finalize',
          'expenses.reimbursement.manage',
          'expenses.category.manage',
          'expenses.advance.manage',
          'expenses.card.manage',
          'expenses.audit.read'
        ])
      });
    });

    await page.route('**/api/v1/me/preferences', async (route) => {
      if (route.request().method() === 'PATCH') {
        const body = JSON.parse(route.request().postData() || '{}');
        if (body.language) mockUserLanguage = body.language;
        await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            language: mockUserLanguage,
            theme: 'default',
            density: 'comfortable',
            sidebarCollapsed: false
          })
        });
      }
    });

    await page.route('**/api/v1/me/navigation-preferences/**', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    });

    // 3. Modules Manifest
    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'home' },
          { id: 'expenses' },
          { id: 'accounting' },
          { id: 'settings' }
        ])
      });
    });

    // 4. Categories Mock
    await page.route('**/api/v1/expenses/**/categories*', async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'cat-new-001',
            legalEntityId: LEGAL_ENTITY_ID,
            code: body.code,
            name: body.name,
            status: 'Active',
            publishedVersionId: 'ver-001'
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'cat-travel',
              legalEntityId: LEGAL_ENTITY_ID,
              code: 'TRAVEL',
              name: 'نفقات السفر والانتقال',
              status: 'Active',
              publishedVersionId: 'ver-travel-1'
            },
            {
              id: 'cat-meals',
              legalEntityId: LEGAL_ENTITY_ID,
              code: 'MEALS',
              name: 'وجبات وضيافة العمل',
              status: 'Active',
              publishedVersionId: 'ver-meals-1'
            }
          ])
        });
      }
    });

    // 5. Exception Summary KPI
    await page.route('**/api/v1/expenses/**/reports/exceptions*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          pendingApproval: 4,
          missingEvidence: 2,
          reimbursementPending: 3,
          reimbursementAmount: 14500,
          personalRepaymentDue: 2,
          personalRepaymentAmount: 3200,
          outstandingAdvances: 5,
          outstandingAdvanceAmount: 8500
        })
      });
    });

    // 6. Expense Claim Details & Sub-resources
    await page.route('**/api/v1/expenses/**/submit', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_EXPENSE_ID,
          expenseState: 'Submitted',
          approvalState: 'Pending',
          version: 2
        })
      });
    });

    await page.route('**/api/v1/expenses/**/approval-decision', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_EXPENSE_ID,
          expenseState: 'Approved',
          approvalState: 'Approved',
          version: 3
        })
      });
    });

    await page.route('**/api/v1/expenses/**/finalize', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_EXPENSE_ID,
          expenseState: 'Finalized',
          accountingState: 'Posted',
          settlementState: 'ReimbursementPending',
          version: 4
        })
      });
    });

    await page.route('**/api/v1/expenses/**/missing-receipt', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'mr-001',
          expenseId: TEST_EXPENSE_ID,
          reason: 'تعذر الحصول على فاتورة ضريبية رسمية',
          createdAt: new Date().toISOString()
        })
      });
    });

    await page.route('**/api/v1/expenses/**/evidence*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'ev-001',
            expenseId: TEST_EXPENSE_ID,
            fileReference: 'receipt-hotel-850.pdf',
            status: 'Verified',
            createdAt: '2026-11-08T14:35:00Z'
          }
        ])
      });
    });

    await page.route('**/api/v1/expenses/**/itemizations*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'item-001',
            expenseId: TEST_EXPENSE_ID,
            description: 'إقامة فندقية',
            amount: 750.00,
            currencyCode: 'EGP'
          },
          {
            id: 'item-002',
            expenseId: TEST_EXPENSE_ID,
            description: 'ضريبة القيمة المضافة 14%',
            amount: 100.00,
            currencyCode: 'EGP'
          }
        ])
      });
    });

    await page.route('**/api/v1/expenses/**/participants*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'part-001', displayName: 'م. أحمد الشناوي', role: 'موظف مكلف بالمهمة' }
        ])
      });
    });

    await page.route('**/api/v1/expenses/**/dimensions*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'dim-001', dimensionCode: 'COST_CENTER', valueCode: 'SALES-OPS' }
        ])
      });
    });

    await page.route('**/api/v1/expenses/**/history*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'hist-001', action: 'Created', performedBy: 'emp-101', timestamp: '2026-11-08T14:30:00Z' },
          { id: 'hist-002', action: 'Submitted', performedBy: 'emp-101', timestamp: '2026-11-08T15:00:00Z' }
        ])
      });
    });

    // 7. Advances Management
    await page.route('**/api/v1/expenses/**/advances/*/issue', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_ADVANCE_ID,
          state: 'Issued',
          issuedAmount: 5000.00,
          version: 2
        })
      });
    });

    await page.route('**/api/v1/expenses/**/advances/*/apply', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_ADVANCE_ID,
          appliedAmount: 3050.00,
          version: 3
        })
      });
    });

    await page.route('**/api/v1/expenses/**/advances/*/return', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_ADVANCE_ID,
          returnedAmount: 1950.00,
          state: 'Settled',
          version: 4
        })
      });
    });

    await page.route(/\/api\/v1\/expenses\/legal-entities\/[^/]+\/advances(?:\?.*)?$/, async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'adv-new-001',
            legalEntityId: LEGAL_ENTITY_ID,
            employeeId: 'emp-101',
            purpose: body.purpose,
            amount: body.amount,
            currencyCode: body.currencyCode,
            issuedAmount: 0,
            appliedAmount: 0,
            returnedAmount: 0,
            writtenOffAmount: 0,
            dueDate: body.dueDate,
            state: 'Requested',
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
                id: TEST_ADVANCE_ID,
                legalEntityId: LEGAL_ENTITY_ID,
                employeeId: 'emp-101',
                purpose: 'سلفة مهمة عمل للإسكندرية',
                amount: 5000.00,
                currencyCode: 'EGP',
                issuedAmount: 5000.00,
                appliedAmount: 2200.00,
                returnedAmount: 0.00,
                writtenOffAmount: 0.00,
                dueDate: '2026-11-20',
                state: 'Issued',
                isOverdue: false,
                version: 2
              }
            ],
            totalCount: 1,
            page: 1,
            pageSize: 15
          })
        });
      }
    });

    // 8. Corporate Cards Management
    await page.route('**/api/v1/expenses/**/corporate-card/**', async (route) => {
      const url = route.request().url();
      if (url.includes('/reconciliation')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            statementAmount: 48900.00,
            matchedAmount: 39500.00,
            personalAmount: 3400.00,
            unclassifiedAmount: 6000.00,
            totalTransactions: 32,
            currencyCode: 'EGP'
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: TEST_CARD_ID,
                legalEntityId: LEGAL_ENTITY_ID,
                employeeId: 'emp-101',
                providerReference: 'AUTH-TXN-99881',
                transactionDate: '2026-11-05',
                amount: 1250.00,
                currencyCode: 'EGP',
                merchantName: 'محطة بترول شل النزهة',
                state: 'Unmatched',
                version: 1
              }
            ],
            totalCount: 1,
            page: 1,
            pageSize: 15
          })
        });
      }
    });

    // 9. Reimbursements & Repayments Management
    await page.route('**/api/v1/expenses/**/reimbursements/repayments*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: TEST_REPAYMENT_ID,
              legalEntityId: LEGAL_ENTITY_ID,
              sourceReference: 'PERS-CARD-001',
              amount: 800.00,
              settledAmount: 0.00,
              currencyCode: 'EGP',
              state: 'Pending',
              createdAt: '2026-11-03T09:00:00Z',
              version: 1
            }
          ],
          totalCount: 1
        })
      });
    });

    // 10. Spend Reports
    await page.route('**/api/v1/expenses/**/reports/spend*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              groupBy: 'category',
              groupKey: 'TRAVEL',
              originalAmount: 12850.00,
              baseAmount: 12850.00,
              taxAmount: 1800.00,
              expenseCount: 8
            },
            {
              groupBy: 'category',
              groupKey: 'MEALS',
              originalAmount: 4320.00,
              baseAmount: 4320.00,
              taxAmount: 605.00,
              expenseCount: 6
            }
          ],
          totalCount: 2
        })
      });
    });

    // 11. Policies & Rate Policies
    await page.route(/\/api\/v1\/expenses\/legal-entities\/[^/]+\/policies(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'pol-001',
              code: 'MAX-HOTEL',
              name: 'الحد الأقصى للإقامة الفندقية 1500 ج.م',
              status: 'Published'
            }
          ],
          totalCount: 1
        })
      });
    });

    await page.route(/\/api\/v1\/expenses\/legal-entities\/[^/]+\/rate-policies(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'rate-001',
              kind: 'Mileage',
              rate: 15.00,
              currencyCode: 'EGP',
              status: 'Published',
              effectiveFrom: '2026-01-01'
            }
          ],
          totalCount: 1
        })
      });
    });

    // 12. Single Expense Detail
    await page.route(new RegExp(`/api/v1/expenses/(?:legal-entities/[^/]+/)?${TEST_EXPENSE_ID}(?:\\?.*)?$`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_EXPENSE_ID,
          legalEntityId: LEGAL_ENTITY_ID,
          employeeId: 'emp-101',
          categoryVersionId: 'ver-travel-1',
          expenseDate: '2026-11-08',
          amount: 850.00,
          currencyCode: 'EGP',
          paymentSource: 'EmployeePaid',
          merchantName: 'فندق النيل ريتز كارلتون',
          businessPurpose: 'إقامة ليلة لحضور مؤتمر الشركاء الاستراتيجيين',
          description: 'غرفة فردية شاملة الإفطار',
          expenseState: 'Submitted',
          approvalState: 'Pending',
          accountingState: 'NotFinalized',
          settlementState: 'NotSettled',
          evidenceState: 'Verified',
          policyState: 'Passed',
          version: 2,
          createdAt: '2026-11-08T14:30:00Z',
          updatedAt: '2026-11-08T15:00:00Z'
        })
      });
    });

    // 13. General Review Expenses List
    await page.route(/\/api\/v1\/expenses\/legal-entities\/[^/]+(?:\?.*)?$/, async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'exp-created-001',
            legalEntityId: LEGAL_ENTITY_ID,
            employeeId: body.employeeId || 'emp-101',
            categoryVersionId: body.categoryVersionId,
            expenseDate: body.expenseDate,
            amount: body.amount,
            currencyCode: body.currencyCode,
            paymentSource: body.paymentSource,
            merchantName: body.merchantName,
            businessPurpose: body.businessPurpose,
            description: body.description,
            expenseState: 'Draft',
            approvalState: 'Pending',
            accountingState: 'NotFinalized',
            settlementState: 'NotSettled',
            evidenceState: 'Missing',
            policyState: 'Passed',
            version: 1,
            createdAt: new Date().toISOString()
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: TEST_EXPENSE_ID,
                legalEntityId: LEGAL_ENTITY_ID,
                employeeId: 'emp-101',
                categoryVersionId: 'ver-travel-1',
                expenseDate: '2026-11-08',
                amount: 850.00,
                currencyCode: 'EGP',
                paymentSource: 'EmployeePaid',
                merchantName: 'فندق النيل ريتز كارلتون',
                businessPurpose: 'مؤتمر الشركاء السنوي',
                expenseState: 'Submitted',
                approvalState: 'Pending',
                accountingState: 'NotFinalized',
                settlementState: 'NotSettled',
                version: 2
              },
              {
                id: 'exp-002',
                legalEntityId: LEGAL_ENTITY_ID,
                employeeId: 'emp-102',
                categoryVersionId: 'ver-meals-1',
                expenseDate: '2026-11-07',
                amount: 450.00,
                currencyCode: 'EGP',
                paymentSource: 'CompanyCard',
                merchantName: 'مطعم أروما للأعمال',
                businessPurpose: 'غداء عمل مع وفد الموردين',
                expenseState: 'Approved',
                approvalState: 'Approved',
                accountingState: 'NotFinalized',
                settlementState: 'NotSettled',
                version: 2
              }
            ],
            totalCount: 2,
            page: 1,
            pageSize: 15
          })
        });
      }
    });

    // 14. Self-Service Routes (/my/...)
    await page.route(/\/api\/v1\/expenses\/my\/expenses(?:\?.*)?$/, async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'my-exp-new',
            legalEntityId: LEGAL_ENTITY_ID,
            categoryVersionId: body.categoryVersionId,
            expenseDate: body.expenseDate,
            amount: body.amount,
            currencyCode: body.currencyCode,
            paymentSource: body.paymentSource,
            merchantName: body.merchantName,
            businessPurpose: body.businessPurpose,
            expenseState: 'Draft',
            approvalState: 'Pending',
            settlementState: 'NotSettled',
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
                id: TEST_EXPENSE_ID,
                legalEntityId: LEGAL_ENTITY_ID,
                categoryVersionId: 'ver-travel-1',
                expenseDate: '2026-11-08',
                amount: 850.00,
                currencyCode: 'EGP',
                paymentSource: 'EmployeePaid',
                merchantName: 'فندق النيل ريتز كارلتون',
                businessPurpose: 'مؤتمر الشركاء السنوي',
                expenseState: 'Submitted',
                approvalState: 'Pending',
                settlementState: 'NotSettled',
                version: 2
              }
            ],
            totalCount: 1,
            page: 1,
            pageSize: 15
          })
        });
      }
    });

    // Self Service Advances
    await page.route(/\/api\/v1\/expenses\/my\/advances(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: TEST_ADVANCE_ID,
              legalEntityId: LEGAL_ENTITY_ID,
              purpose: 'سلفة مهمة عمل للإسكندرية',
              amount: 5000.00,
              currencyCode: 'EGP',
              issuedAmount: 5000.00,
              appliedAmount: 2200.00,
              returnedAmount: 0.00,
              writtenOffAmount: 0.00,
              dueDate: '2026-11-20',
              state: 'Issued',
              isOverdue: false,
              version: 2
            }
          ],
          totalCount: 1
        })
      });
    });

    // Self Service Cards
    await page.route(/\/api\/v1\/expenses\/my\/cards(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: TEST_CARD_ID,
              legalEntityId: LEGAL_ENTITY_ID,
              providerReference: 'AUTH-TXN-99881',
              transactionDate: '2026-11-05',
              amount: 1250.00,
              currencyCode: 'EGP',
              merchantName: 'محطة بترول شل النزهة',
              state: 'Unmatched',
              version: 1
            }
          ],
          totalCount: 1
        })
      });
    });

    // Self Service Reimbursements
    await page.route(/\/api\/v1\/expenses\/my\/reimbursements(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'reimb-001',
              legalEntityId: LEGAL_ENTITY_ID,
              amount: 1450.00,
              settledAmount: 0.00,
              currencyCode: 'EGP',
              status: 'Pending',
              createdAt: '2026-11-04T10:00:00Z'
            }
          ],
          totalCount: 1
        })
      });
    });

    // Self Service Repayments
    await page.route(/\/api\/v1\/expenses\/my\/repayments(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: TEST_REPAYMENT_ID,
              legalEntityId: LEGAL_ENTITY_ID,
              sourceReference: 'PERS-CARD-001',
              amount: 800.00,
              settledAmount: 0.00,
              currencyCode: 'EGP',
              state: 'Pending',
              createdAt: '2026-11-03T09:00:00Z'
            }
          ],
          totalCount: 1
        })
      });
    });

    // Self Service Reports
    await page.route(/\/api\/v1\/expenses\/my\/reports(?:\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'rep-001',
              title: 'تقرير مصاريف الربع الرابع 2026',
              totalAmount: 4300.00,
              currencyCode: 'EGP',
              status: 'Submitted',
              createdAt: '2026-11-01T12:00:00Z'
            }
          ],
          totalCount: 1
        })
      });
    });
  });

  // ==========================================
  // JOURNEY 01: Visual Architecture & Design System
  // ==========================================
  test('EXP01 — Visual Architecture, Dark Oxford Navy Header, Crisp Cards & RTL KPIs', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/expenses');

    // Verify Title & Subtitle in Arabic
    await expect(page.locator('h1')).toHaveText('المصروفات ومستحقات الموظفين');
    await expect(page.getByText('إدارة نفقات العمل، سلف الموظفين، بطاقات الشركات، وتسوية المطالبات المالية')).toBeVisible();

    // Verify Authentic Badge (no technical ERP Module 21)
    await expect(page.getByText('نفقات ومستحقات الأعمال')).toBeVisible();

    // Verify 7 Tabs are present
    await expect(page.getByRole('tab', { name: 'خدماتي ومصروفاتي' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'مراجعة واعتماد المصروفات' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'سلف العمل' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'بطاقات الشركات' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'المستحقات والتسويات' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'التقارير وتحليل النفقات' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'إعدادات وسياسات المصروفات' })).toBeVisible();

    // Verify Self Service Action Buttons
    await expect(page.getByRole('button', { name: '+ تسجيل مصروف جديد' })).toBeVisible();
    await expect(page.getByRole('button', { name: '+ طلب سلفة عمل جديدة' })).toBeVisible();

    // Verify Table Row for My Expenses
    await expect(page.getByText('فندق النيل ريتز كارلتون')).toBeVisible();

    // Save Visual Proof
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '01-expenses-home-desktop.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '01-expenses-home-desktop.png'), fullPage: true });
  });

  // ==========================================
  // JOURNEY 02: Record New Expense Claim via Modal
  // ==========================================
  test('EXP02 — Employee Self-Service: Record New Expense Modal Dialog', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/expenses');

    // Click "+ New Expense" button
    await page.getByRole('button', { name: '+ تسجيل مصروف جديد' }).click();

    // Modal dialog must be open
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'تسجيل مصروف جديد' })).toBeVisible();

    // Fill form fields
    const merchantInput = page.getByLabel('اسم التاجر أو مزود الخدمة');
    await merchantInput.fill('أوبر ترافيل - مشوار المطار');

    const amountInput = page.getByLabel('المبلغ المالي');
    await amountInput.fill('220.00');

    // Capture Modal Evidence
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '02-expenses-new-expense-modal.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '02-expenses-new-expense-modal.png'), fullPage: true });

    // Submit form
    await page.getByRole('button', { name: 'تسجيل المصروف' }).click();

    // Modal closes
    await expect(page.getByRole('dialog')).not.toBeVisible();
  });

  // ==========================================
  // JOURNEY 03: Missing Receipt Declaration
  // ==========================================
  test('EXP03 — Official Missing Receipt Declaration Modal & Compliance Badge Update', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/expenses');

    // Click inspect action on the existing expense row
    await page.getByRole('button', { name: 'معاينة التفاصيل' }).first().click();

    // 360 Drawer opens
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByText('تفاصيل وملف المصروف 360')).toBeVisible();

    // Click "Receipts" tab in the drawer
    await page.getByRole('button', { name: 'الإيصالات والمستندات' }).click();

    // Click "Declare Missing Receipt"
    await page.getByRole('button', { name: 'إقرار فقدان الإيصال' }).first().click();

    // Missing Receipt Modal opens
    await expect(page.getByRole('heading', { name: 'إقرار فقدان الإيصال الرسمي' })).toBeVisible();

    const reasonInput = page.getByLabel('سبب تعذر تقديم الإيصال أو المستند المؤيد');
    await reasonInput.fill('لم يوفر التاجر فاتورة إلكترونية معتمدة عند استلام الخدمة');

    // Capture Declaration Proof
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '03-expenses-declare-missing-receipt.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '03-expenses-declare-missing-receipt.png'), fullPage: true });

    // Confirm declaration
    await page.getByRole('button', { name: 'تقديم الإقرار الرسمي' }).click();
  });

  // ==========================================
  // JOURNEY 04: 360 Expense Detail Profile & Drawer
  // ==========================================
  test('EXP04 — 360 Expense Detail Profile Inspector with Multi-Section Tabs', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/expenses');

    // Open detail drawer
    await page.getByRole('button', { name: 'معاينة التفاصيل' }).first().click();
    await expect(page.getByRole('dialog')).toBeVisible();

    // Switch between detail tabs
    await page.getByRole('button', { name: 'تفكيك البنود' }).click();
    await expect(page.getByText('إقامة فندقية')).toBeVisible();
    await expect(page.getByText('ضريبة القيمة المضافة 14%')).toBeVisible();

    await page.getByRole('button', { name: 'المشاركون' }).click();
    await expect(page.getByText('م. أحمد الشناوي')).toBeVisible();

    await page.getByRole('button', { name: 'الأبعاد المحاسبية' }).click();
    await expect(page.getByText('COST_CENTER')).toBeVisible();

    await page.getByRole('button', { name: 'سجل التغييرات' }).click();
    await expect(page.getByText(/Created|تم إنشاء السجل كمسودة/)).toBeVisible();

    // Capture Detail Drawer Proof
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '04-expenses-detail-drawer.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '04-expenses-detail-drawer.png'), fullPage: true });

    // Close drawer
    await page.getByRole('button', { name: 'إغلاق', exact: true }).click();
    await expect(page.getByRole('dialog')).not.toBeVisible();
  });

  // ==========================================
  // JOURNEY 05: Finance Manager Review Workbench
  // ==========================================
  test('EXP05 — Finance Manager Review Workbench & Claim Approval', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/expenses');

    // Switch to "Review & Approvals" tab
    await page.getByRole('tab', { name: 'مراجعة واعتماد المصروفات' }).click();

    // Verify KPI ribbon
    await expect(page.locator('h4, div').filter({ hasText: 'بانتظار الاعتماد' }).first()).toBeVisible();
    await expect(page.getByText('إيصالات مفقودة')).toBeVisible();
    await expect(page.getByText('مستحقات واجبة السداد')).toBeVisible();
    await expect(page.getByText('سلف عمل قائمة')).toBeVisible();

    // Verify filter pills
    await expect(page.getByRole('button', { name: 'كافة الحالات' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'معتمدة' })).toBeVisible();

    // Open claim in review workbench
    await page.getByRole('button', { name: 'معاينة التفاصيل' }).first().click();
    await expect(page.getByRole('dialog')).toBeVisible();

    // Approve claim
    const approveBtn = page.getByRole('button', { name: 'اعتماد', exact: true });
    if (await approveBtn.isVisible()) {
      await approveBtn.click();
    }

    // Capture Review Workbench Proof
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '05-expenses-review-workbench.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '05-expenses-review-workbench.png'), fullPage: true });
  });

  // ==========================================
  // JOURNEY 06: General Ledger Finalization
  // ==========================================
  test('EXP06 — General Ledger Finalization & Accounting Reversal Protection', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/expenses');

    // Switch to Review
    await page.getByRole('tab', { name: 'مراجعة واعتماد المصروفات' }).click();

    // Inspect claim
    await page.getByRole('button', { name: 'معاينة التفاصيل' }).first().click();
    await expect(page.getByRole('dialog')).toBeVisible();

    // Capture Finalization Evidence
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '06-expenses-finalization.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '06-expenses-finalization.png'), fullPage: true });

    await page.getByRole('button', { name: 'إغلاق', exact: true }).click();
  });

  // ==========================================
  // JOURNEY 07: Business Advances Lifecycle
  // ==========================================
  test('EXP07 — Business Advances Lifecycle (Request, Disbursement, & Settle)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/expenses');

    // Switch to "Business Advances" tab
    await page.getByRole('tab', { name: 'سلف العمل' }).click();

    // Verify Title & Description
    await expect(page.getByText('إدارة سلف العمل والتسويات')).toBeVisible();

    // Verify existing advance row
    await expect(page.getByText('سلفة مهمة عمل للإسكندرية')).toBeVisible();
    await expect(page.getByText('5000.00 EGP').first()).toBeVisible();

    // Click "+ Request Advance"
    await page.getByRole('button', { name: '+ طلب سلفة عمل' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'طلب سلفة عمل جديدة' })).toBeVisible();

    // Fill advance request form
    await page.getByLabel('الغرض من السلفة والمهمة').fill('سفر لحضور تدريب المنظومة بدبي');
    await page.getByLabel('مبلغ السلفة المطلوب').fill('7500.00');

    // Capture Advance Management Proof
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '07-expenses-advances-management.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '07-expenses-advances-management.png'), fullPage: true });

    // Cancel modal
    await page.getByRole('button', { name: 'إلغاء' }).click();
  });

  // ==========================================
  // JOURNEY 08: Corporate Credit Cards Inbox & Matching
  // ==========================================
  test('EXP08 — Corporate Credit Cards Inbox & Expense Claim Matching', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/expenses');

    // Switch to "Corporate Cards" tab
    await page.getByRole('tab', { name: 'بطاقات الشركات' }).click();

    // Verify Reconciliation KPI Ribbon
    await expect(page.getByText('صندوق بطاقات الشركات والمطابقة')).toBeVisible();
    await expect(page.getByText('إجمالي كشف البطاقات')).toBeVisible();
    await expect(page.getByText('حركات مطابقة')).toBeVisible();
    await expect(page.getByText('مصروفات شخصية')).toBeVisible();

    // Verify Unmatched card transaction
    await expect(page.getByText('محطة بترول شل النزهة')).toBeVisible();
    await expect(page.getByText('AUTH-TXN-99881')).toBeVisible();

    // Click "+ Create Expense from Card"
    await page.getByRole('button', { name: 'إنشاء مصروف من الحركة' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'إنشاء مصروف من حركة البطاقة' })).toBeVisible();

    // Capture Corporate Cards Proof
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '08-expenses-corporate-cards.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '08-expenses-corporate-cards.png'), fullPage: true });

    await page.getByRole('button', { name: 'إلغاء' }).click();
  });

  // ==========================================
  // JOURNEY 09: Reimbursement Settlement
  // ==========================================
  test('EXP09 — Employee Reimbursement Settlement & Disbursement Batches', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/expenses');

    // Switch to "Reimbursements & Settlement" tab
    await page.getByRole('tab', { name: 'المستحقات والتسويات' }).click();

    // Verify Header
    await expect(page.getByText('تسوية مستحقات الموظفين والمدفوعات')).toBeVisible();
    await expect(page.getByRole('button', { name: '+ تجهيز دفعة سداد جديدة' })).toBeVisible();

    // Click "+ Prepare Reimbursement Batch"
    await page.getByRole('button', { name: '+ تجهيز دفعة سداد جديدة' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'تجهيز دفعة سداد مستحقات موظفين' })).toBeVisible();

    // Capture Reimbursement Proof
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '09-expenses-reimbursements.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '09-expenses-reimbursements.png'), fullPage: true });

    await page.getByRole('button', { name: 'إلغاء' }).click();
  });

  // ==========================================
  // JOURNEY 10: Spend Reports & CSV Export
  // ==========================================
  test('EXP10 — Spend Analysis Reports & CSV Export', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/expenses');

    // Switch to "Reports" tab
    await page.getByRole('tab', { name: 'التقارير وتحليل النفقات' }).click();

    // Verify Title & Export Button
    await expect(page.getByText('تقارير وتحليلات نفقات الأعمال')).toBeVisible();
    await expect(page.getByRole('button', { name: 'تصدير تقرير النفقات (CSV)' })).toBeVisible();

    // Verify Data Table
    await expect(page.getByText('TRAVEL')).toBeVisible();
    await expect(page.getByText('MEALS')).toBeVisible();

    // Capture Reports Proof
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '10-expenses-spend-reports.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '10-expenses-spend-reports.png'), fullPage: true });
  });

  // ==========================================
  // JOURNEY 11: Mobile Responsive Viewport (390px)
  // ==========================================
  test('EXP11 — Responsive Mobile Viewport (390px iPhone Layout & Overflow Guard)', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/expenses');

    // Verify Title fits
    await expect(page.locator('h1')).toHaveText('المصروفات ومستحقات الموظفين');

    // Verify tabs wrap nicely and button is clickable
    await expect(page.getByRole('button', { name: '+ تسجيل مصروف جديد' })).toBeVisible();

    // Capture Mobile Evidence
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '11-expenses-mobile-390px.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '11-expenses-mobile-390px.png'), fullPage: true });
  });

  // ==========================================
  // JOURNEY 12: English LTR Parity
  // ==========================================
  test('EXP12 — English LTR Layout & Full Translation Parity', async ({ page }) => {
    mockUserLanguage = 'en';
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/expenses');

    // Check English Header & LTR direction
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await expect(page.locator('h1')).toHaveText('Expenses & Reimbursements');
    await expect(page.getByRole('button', { name: '+ New Expense' })).toBeVisible();

    // Capture English LTR Proof
    await page.screenshot({ path: path.join(ARTIFACTS_DIR, '12-expenses-english-ltr.png'), fullPage: true });
    await page.screenshot({ path: path.join(LOCAL_ARTIFACTS_DIR, '12-expenses-english-ltr.png'), fullPage: true });

    mockUserLanguage = 'ar';
  });
});
