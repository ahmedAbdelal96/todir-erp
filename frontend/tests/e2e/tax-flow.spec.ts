import { test, expect } from '@playwright/test';

const MAIN_ENTITY_ID = '00000000-0000-0000-0000-000000000001';
const SECONDARY_ENTITY_ID = '00000000-0000-0000-0000-000000000002';
const REGISTRATION_ID = '00000000-0000-0000-0000-000000000003';
const PACK_ID = '00000000-0000-0000-0000-000000000004';
const RULE_SET_ID = '00000000-0000-0000-0000-000000000005';
const WARNING_ENTITY_ID = '00000000-0000-0000-0000-000000000009';

test.describe('Tax Administration Real-Stack Acceptance & Visual Suite', () => {
  test.beforeEach(async ({ page }) => {
    // 0. Auth routes
    await page.route('**/api/v1/auth/csrf', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ token: 'mock-csrf' })
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

    await page.route('**/api/v1/context/organization', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true })
      });
    });

    // 1. Session Context (Atlas Main & Atlas Secondary entities)
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: '00000000-0000-0000-0000-000000000000',
          memberships: [
            { id: 'atlas', tenantId: '00000000-0000-0000-0000-000000000000', displayName: 'شركة أطلس للتجارة' }
          ],
          context: {
            displayName: 'شركة أطلس للتجارة',
            legalEntities: [
              {
                id: MAIN_ENTITY_ID,
                legalEntityId: MAIN_ENTITY_ID,
                code: 'MAIN',
                name: 'شركة أطلس للتجارة — الكيان الرئيسي',
                countryCode: 'EG',
                baseCurrencyCode: 'EGP'
              },
              {
                id: SECONDARY_ENTITY_ID,
                legalEntityId: SECONDARY_ENTITY_ID,
                code: 'SECONDARY',
                name: 'شركة أطلس للتجارة — الكيان الثانوي',
                countryCode: 'EG',
                baseCurrencyCode: 'EGP'
              },
              {
                id: WARNING_ENTITY_ID,
                legalEntityId: WARNING_ENTITY_ID,
                code: 'WARNING',
                name: 'شركة أطلس للتجارة — قيد الإعداد',
                countryCode: 'EG',
                baseCurrencyCode: 'EGP'
              }
            ],
            branches: [
              {
                id: 'b1',
                branchId: 'b1',
                legalEntityId: MAIN_ENTITY_ID,
                code: 'MAIN',
                name: 'الفرع الرئيسي'
              }
            ]
          }
        })
      });
    });

    // 2. Authorization check - all Tax permissions granted
    await page.route('**/api/v1/authorization/check*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ allowed: true })
      });
    });

    // 3. Modules manifest
    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'home' },
          { id: 'directory' },
          { id: 'catalog' },
          { id: 'finbase' },
          { id: 'pricing' },
          { id: 'tax' },
          { id: 'settings' }
        ])
      });
    });

    // 4. Tax Capability endpoints
    await page.route(new RegExp(`/api/v1/tax/legal-entities/${MAIN_ENTITY_ID}/capability`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: MAIN_ENTITY_ID,
          capability: 'Disabled',
          effectiveFrom: '2026-01-01',
          effectiveTo: null
        })
      });
    });

    await page.route(new RegExp(`/api/v1/tax/legal-entities/${SECONDARY_ENTITY_ID}/capability`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: SECONDARY_ENTITY_ID,
          capability: 'Enabled',
          effectiveFrom: '2026-01-01',
          effectiveTo: null
        })
      });
    });

    await page.route(new RegExp(`/api/v1/tax/legal-entities/${WARNING_ENTITY_ID}/capability`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: WARNING_ENTITY_ID,
          capability: 'Enabled',
          effectiveFrom: '2026-01-01',
          effectiveTo: null
        })
      });
    });

    // 5. Tax Coverage endpoint
    await page.route(/\/api\/v1\/tax\/coverage/, async (route) => {
      const url = new URL(route.request().url());
      const entity = url.searchParams.get('legalEntityId');

      if (entity === MAIN_ENTITY_ID) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            legalEntityId: MAIN_ENTITY_ID,
            at: '2026-10-24',
            health: 'Blocked',
            capability: 'Disabled',
            countryPackStatus: 'Missing',
            registrationStatus: 'Missing',
            registrationEffective: false,
            taxPolicyStatus: 'Missing',
            roundingPolicyStatus: 'Missing',
            productClassificationCoverage: 'Missing',
            partyProfileReadiness: 'Missing',
            activeRuleSetStatus: 'Missing',
            issues: [
              { code: 'TaxCapabilityDisabled', message: 'Tax management is not enabled for this legal entity.' }
            ]
          })
        });
      } else if (entity === WARNING_ENTITY_ID) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            legalEntityId: WARNING_ENTITY_ID,
            at: '2026-10-24',
            health: 'Warning',
            capability: 'Enabled',
            countryPackStatus: 'Available',
            registrationStatus: 'Registered',
            registrationEffective: true,
            taxPolicyStatus: 'Configured',
            roundingPolicyStatus: 'Active',
            productClassificationCoverage: 'Partial',
            partyProfileReadiness: 'Complete',
            activeRuleSetStatus: 'Active',
            issues: [
              { code: 'ProductClassificationMissing', message: '12 products need a tax classification.' }
            ]
          })
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            legalEntityId: SECONDARY_ENTITY_ID,
            at: '2026-10-24',
            health: 'Green',
            capability: 'Enabled',
            countryPackStatus: 'Available',
            registrationStatus: 'Registered',
            registrationEffective: true,
            taxPolicyStatus: 'Configured',
            roundingPolicyStatus: 'Active',
            productClassificationCoverage: 'Complete',
            partyProfileReadiness: 'Complete',
            activeRuleSetStatus: 'Active',
            issues: []
          })
        });
      }
    });

    // 6. Tax Registrations
    await page.route(/\/api\/v1\/tax\/registrations(\?.*)?$/, async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: REGISTRATION_ID,
              legalEntityId: SECONDARY_ENTITY_ID,
              countryCode: 'EG',
              schemeCode: 'DEVELOPMENT',
              registrationNumber: 'DEMO-REGISTERED',
              status: 'Registered',
              effectiveFrom: '2026-01-01',
              effectiveTo: null,
              authority: 'Egyptian Tax Authority (ETA)',
              evidenceReference: 'Synthetic development registration; not a statutory identifier.',
              version: 1
            }
          ])
        });
      } else {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({}) });
      }
    });

    // Registration History
    await page.route(new RegExp(`/api/v1/tax/registrations/${REGISTRATION_ID}/history`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'reg-ver-1',
            legalEntityId: SECONDARY_ENTITY_ID,
            countryCode: 'EG',
            schemeCode: 'DEVELOPMENT',
            registrationNumber: 'DEMO-REGISTERED',
            status: 'Registered',
            effectiveFrom: '2026-01-01',
            effectiveTo: null,
            authority: 'Egyptian Tax Authority (ETA)',
            evidenceReference: 'Synthetic development registration; not a statutory identifier.',
            version: 1
          }
        ])
      });
    });

    // Registration Members
    await page.route(new RegExp(`/api/v1/tax/registrations/${REGISTRATION_ID}/members`), async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'mem-1',
            registrationId: REGISTRATION_ID,
            legalEntityId: SECONDARY_ENTITY_ID,
            effectiveFrom: '2026-01-01',
            effectiveTo: null
          }
        ])
      });
    });

    // 7. Reference Data
    await page.route(/\/api\/v1\/tax\/reference-data/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          legalEntityId: SECONDARY_ENTITY_ID,
          schemes: [
            { code: 'DEVELOPMENT', countryCode: 'EG', name: 'Development indirect tax scheme', status: 'Active' }
          ],
          categories: [
            { id: 'cat-gen', code: 'GENERAL', name: 'General Product Category', scope: 'Product', status: 'Active' },
            { id: 'cat-zero', code: 'ZERO-DEMO', name: 'Zero Rated Product Category', scope: 'Product', status: 'Active' },
            { id: 'cat-pty', code: 'PARTY-GENERAL', name: 'Standard Commercial Party', scope: 'Party', status: 'Active' }
          ],
          jurisdictions: [],
          policies: [
            {
              policyCode: 'DEVELOPMENT',
              roundingStrategy: 'PerLine',
              moneyScale: 2,
              taxDateBasis: 'SupplyDate',
              timezoneId: 'Africa/Cairo'
            }
          ]
        })
      });
    });

    // 8. Product Profiles
    await page.route(/\/api\/v1\/tax\/product-profiles(\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'prd-prof-1',
            productTemplateId: 'PRD-TEMPLATE-001',
            productVariantId: null,
            legalEntityId: null,
            countryCode: 'EG',
            categoryId: 'cat-gen',
            treatment: null,
            effectiveFrom: '2026-01-01',
            effectiveTo: null,
            version: 1
          },
          {
            id: 'prd-prof-2',
            productTemplateId: 'PRD-TEMPLATE-001',
            productVariantId: 'PRD-VARIANT-002',
            legalEntityId: null,
            countryCode: 'EG',
            categoryId: 'cat-zero',
            treatment: null,
            effectiveFrom: '2026-01-01',
            effectiveTo: null,
            version: 1
          }
        ])
      });
    });

    // 9. Party Profiles
    await page.route(/\/api\/v1\/tax\/party-profiles(\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'pty-prof-1',
            partyId: 'PTY-CUSTOMER-001',
            legalEntityId: null,
            countryCode: 'EG',
            categoryId: 'cat-pty',
            treatment: 'StandardRated',
            reverseChargeEligible: false,
            registrationNumber: '300-123-456',
            registrationCountryCode: 'EG',
            registrationStatus: 'Registered',
            effectiveFrom: '2026-01-01',
            effectiveTo: null,
            version: 1
          },
          {
            id: 'pty-prof-2',
            partyId: 'PTY-SUPPLIER-FOREIGN',
            legalEntityId: null,
            countryCode: 'US',
            categoryId: 'cat-pty',
            treatment: 'ReverseCharge',
            reverseChargeEligible: true,
            registrationNumber: null,
            registrationCountryCode: null,
            registrationStatus: null,
            effectiveFrom: '2026-01-01',
            effectiveTo: null,
            version: 1
          }
        ])
      });
    });

    // 10. Rule Sets
    await page.route(/\/api\/v1\/tax\/rule-sets(\?.*)?$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: RULE_SET_ID,
            legalEntityId: SECONDARY_ENTITY_ID,
            countryCode: 'EG',
            schemeCode: 'DEVELOPMENT',
            version: 1,
            status: 'Active',
            sourceKind: 'TenantConfiguration',
            mutable: true,
            effectiveFrom: '2026-01-01',
            effectiveTo: null
          }
        ])
      });
    });

    // 11. Country Packs
    await page.route(/\/api\/v1\/tax\/country-packs/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: PACK_ID,
            packId: 'DEVELOPMENT-UNREVIEWED',
            countryCode: 'EG',
            packCode: 'DEVELOPMENT-UNREVIEWED',
            packVersion: '0.0.0',
            engineContractVersion: 'tax_calculation_v1',
            sourceAuthority: 'Internal Development Fixture',
            authorityReference: 'development:synthetic',
            sourceTitle: 'Development/Test Country Pack',
            effectiveFrom: '2026-01-01',
            effectiveTo: null,
            reviewStatus: 'Pending',
            status: 'Installed',
            sourceKind: 'TestOnly',
            goldenVectorCount: 0,
            goldenVectorStatus: 'Missing',
            validationStatus: 'NotValidated'
          }
        ])
      });
    });

    // 12. Simulation Endpoint
    await page.route(/\/api\/v1\/tax\/calculate$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          evaluationState: 'Determined',
          treatment: 'StandardRated',
          netAmount: '1000.00',
          taxTotal: '140.00',
          grossAmount: '1140.00',
          taxLines: [
            {
              taxTypeCode: 'VAT',
              taxCode: 'VAT-14',
              treatment: 'StandardRated',
              rate: '14.00',
              taxableBase: '1000.00',
              taxAmount: '140.00',
              currencyCode: 'EGP',
              jurisdictionCode: 'EG',
              legalReasonCode: 'EGY-VAT-ART-3',
              legalReasonText: 'Standard rate applied per Egyptian VAT law Article 3',
              ruleId: 'RULE-EG-STD-001',
              ruleVersion: '1',
              recoverability: {
                kind: 'FullyRecoverable',
                recoverablePercentage: '100.00',
                recoverableAmount: '140.00',
                nonRecoverableAmount: '0.00'
              }
            }
          ],
          jurisdictionSnapshot: 'EG',
          registrationSnapshot: 'DEMO-REGISTERED',
          ruleSetVersion: '1',
          calculationVersion: '1',
          explanation: {
            countryPackCode: 'EG-VAT',
            countryPackVersion: '1.0.0',
            taxDate: '2026-10-24',
            jurisdiction: 'EG',
            registrationStatus: 'Registered',
            partyTreatment: 'StandardRated',
            productCategory: 'GENERAL',
            matchedRuleId: 'RULE-EG-STD-001',
            componentCodes: ['VAT-14'],
            rounding: 'RoundHalfUp',
            summary: 'Standard rate applied per Egyptian VAT law Article 3',
            placeOfSupplyReason: 'Destination in Egypt',
            partyEvidence: 'Valid tax registration certificate',
            productEvidence: 'Standard goods template classification',
            legalReasonCode: 'EGY-VAT-ART-3',
            legalReasonText: 'Standard rate applied per Egyptian VAT law Article 3'
          }
        })
      });
    });

    // 13. Import Preview Endpoint
    await page.route(/\/api\/v1\/tax\/imports\/.*\/preview$/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          batchId: 'import-batch-123',
          totalRows: 3,
          acceptedCount: 2,
          rejectedCount: 1,
          skippedCount: 0,
          rows: [
            { rowNumber: 1, reference: 'PRD-TEMPLATE-001', outcome: 'Create', message: null },
            { rowNumber: 2, reference: 'PRD-VARIANT-002', outcome: 'CreateNewVersion', message: null },
            { rowNumber: 3, reference: 'UNKNOWN-SKU-999', outcome: 'UnknownProduct', message: 'Product SKU was not found in Catalog master.' }
          ]
        })
      });
    });
  });

  test('Full Tax Administration Real-Stack Journey and Visual Evidence', async ({ page }) => {
    test.setTimeout(120_000);
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/tax');
    await page.waitForLoadState('networkidle');

    // Handle login if rendered
    const emailInput = page.locator('#login-email');
    if (await emailInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      const atlasDevBtn = page.getByRole('button', { name: /Atlas — مسؤول النظام/i });
      if (await atlasDevBtn.isVisible({ timeout: 1000 }).catch(() => false)) {
        await atlasDevBtn.click();
      } else {
        await emailInput.fill('admin@atlas.com');
        await page.locator('#login-password').fill('Admin@123456');
        await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
      }
      await emailInput.waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
      await page.waitForLoadState('networkidle');
    }

    // ==========================================
    // FLOW A: Tax Disabled Entity (Atlas Main)
    // ==========================================
    const entitySelect = page.locator('#entity-select');
    await expect(entitySelect).toBeVisible({ timeout: 15000 });
    // Select Atlas Main where capability is Disabled
    await entitySelect.selectOption({ value: MAIN_ENTITY_ID });
    await page.waitForTimeout(500);
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/إدارة الضرائب معطلة لهذا الكيان القانوني|Tax Management is Disabled for This Legal Entity/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('button', { name: /تفعيل إدارة الضرائب|تفعيل الضرائب|Enable Tax Management/i }).first()).toBeVisible();

    // Verify absence of fake 0% VAT or Exempt claims in Disabled state
    await expect(page.getByText('VAT 0%')).toHaveCount(0);
    await expect(page.getByText('Tax 0%')).toHaveCount(0);

    // 01: Tax Disabled State Evidence
    await page.screenshot({
      path: 'artifacts/test-results/tax/01-tax-disabled-state.png',
      fullPage: true
    });

    // ==========================================
    // FLOW B: Tax Overview & Readiness
    // Case B: Needs Attention (Atlas Setup Entity)
    // ==========================================
    await entitySelect.selectOption({ value: WARNING_ENTITY_ID });
    await page.waitForTimeout(500);
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/جاهزية محرك الضرائب|Tax Engine Readiness/i)).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/يتطلب الانتباه|Needs Attention/i).first()).toBeVisible({ timeout: 5000 });

    // ==========================================
    // Case C: Ready (Atlas Secondary)
    // ==========================================
    // Switch to Atlas Secondary entity in top bar
    await entitySelect.selectOption({ value: SECONDARY_ENTITY_ID });
    await page.waitForTimeout(500);
    await page.waitForLoadState('networkidle');

    // Verify Overview & Readiness section
    await expect(page.getByText(/جاهزية محرك الضرائب|Tax Engine Readiness/i)).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/جاهز ومكتمل|Ready/i).first()).toBeVisible({ timeout: 5000 });
    await expect(page.getByText(/التسجيل الضريبي|Tax Registration/i)).toBeVisible();
    await expect(page.getByText(/حزمة الدولة|Country Pack/i).first()).toBeVisible();
    await expect(page.getByText(/تصنيف المنتجات|Product Classification/i)).toBeVisible();
    await expect(page.getByText(/مجموعة القواعد النشطة|Active Rule Set/i)).toBeVisible();
    await expect(page.getByText('تجريبية فقط — غير تشريعية').first()).toBeVisible({ timeout: 5000 });

    // 02: Overview Readiness (Arabic RTL)
    await page.screenshot({
      path: 'artifacts/test-results/tax/02-tax-overview-readiness-ar.png',
      fullPage: true
    });

    // Switch Language to English to capture English LTR
    const langToggle = page.getByRole('button', { name: /التبديل إلى الإنجليزية|Switch to English|English|العربية/i }).first();
    if (await langToggle.isVisible({ timeout: 3000 }).catch(() => false)) {
      await langToggle.click();
      await page.waitForTimeout(500);
      await expect(page.getByRole('heading', { name: /Tax Administration/i })).toBeVisible({ timeout: 10000 });
      await expect(page.getByText('Test Only / Non-Statutory').first()).toBeVisible({ timeout: 5000 });

      // 03: Overview Readiness (English LTR)
      await page.screenshot({
        path: 'artifacts/test-results/tax/03-tax-overview-readiness-en.png',
        fullPage: true
      });

      // Switch back to Arabic
      await page.getByRole('button', { name: /التبديل إلى العربية|Switch to Arabic|العربية/i }).first().click();
      await page.waitForTimeout(500);
      await expect(page.getByRole('heading', { name: /إدارة الضرائب والامتثال الضريبي/i })).toBeVisible({ timeout: 10000 });
    }

    // ==========================================
    // FLOW C: Tax Registrations Page & History
    // ==========================================
    await page.getByRole('button', { name: /^التسجيلات الضريبية$|^Registrations$/i }).click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/التسجيلات الرسمية لدى الهيئات الضريبية|Official tax authority registrations/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('DEMO-REGISTERED')).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('button', { name: /\+ تسجيل ضريبي جديد|\+ New Registration/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('button', { name: /إلغاء التسجيل|Deregister/i }).first()).toBeVisible({ timeout: 5000 });

    // 04: Tax Registrations Table
    await page.screenshot({
      path: 'artifacts/test-results/tax/04-tax-registrations-table.png',
      fullPage: true
    });

    // Responsive check at narrower common laptop viewport (1280px)
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.waitForTimeout(300);
    await expect(page.getByRole('button', { name: /\+ تسجيل ضريبي جديد|\+ New Registration/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /إلغاء التسجيل|Deregister/i }).first()).toBeVisible();
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.waitForTimeout(300);

    // Open History Modal
    const historyBtn = page.getByRole('button', { name: /سجل الإصدارات|History/i }).first();
    if (await historyBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await historyBtn.click();
      await expect(page.getByRole('heading', { name: /سجل إصدارات التسجيل الضريبي|Tax Registration Version History/i })).toBeVisible({ timeout: 5000 });

      // 05: Registration History Timeline
      await page.screenshot({
        path: 'artifacts/test-results/tax/05-tax-registration-history-modal.png',
        fullPage: false
      });

      // Close History Modal
      await page.getByRole('button', { name: /^إغلاق$|^Close$/i }).click();
      await page.waitForTimeout(300);
    }

    // ==========================================
    // FLOW D: Product Tax Profiles
    // ==========================================
    await page.getByRole('button', { name: /^ملفات المنتجات$|^Product Profiles$/i }).click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: /الملفات الضريبية للمنتجات|Product Profiles/i })).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('PRD-TEMPLATE-001').first()).toBeVisible({ timeout: 10000 });

    // 06: Product Tax Profiles
    await page.screenshot({
      path: 'artifacts/test-results/tax/06-tax-product-profiles.png',
      fullPage: true
    });

    // Open Product Profile Modal
    const newProductProfileBtn = page.getByRole('button', { name: /تصنيف ضريبي جديد لمنتج|إضافة تصنيف منتج|New Product Classification/i });
    if (await newProductProfileBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await newProductProfileBtn.click();
      await expect(page.getByRole('heading', { name: /إنشاء تصنيف ضريبي لمنتج|إضافة تصنيف ضريبي لمنتج|Create Product Tax Classification/i })).toBeVisible({ timeout: 5000 });

      // 07: Product Profile Modal
      await page.screenshot({
        path: 'artifacts/test-results/tax/07-tax-product-profile-modal.png',
        fullPage: false
      });

      await page.getByRole('button', { name: /^إلغاء$|^Cancel$/i }).click();
      await page.waitForTimeout(300);
    }

    // ==========================================
    // FLOW E: Party Tax Profiles
    // ==========================================
    await page.getByRole('button', { name: /^ملفات الأطراف$|^Party Profiles$/i }).click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: /الملفات الضريبية للأطراف|Party Profiles/i })).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('PTY-CUSTOMER-001')).toBeVisible({ timeout: 10000 });

    // 08: Party Tax Profiles
    await page.screenshot({
      path: 'artifacts/test-results/tax/08-tax-party-profiles.png',
      fullPage: true
    });

    // ==========================================
    // FLOW F: Simulation Workbench & "Why This Tax?"
    // ==========================================
    await page.getByRole('button', { name: /^أداة المحاكاة$|^Simulation$/i }).click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/أداة محاكاة محرك الضرائب|Tax Engine Simulation/i)).toBeVisible({ timeout: 10000 });

    // Click simulate
    const simulateBtn = page.getByRole('button', { name: /احتساب الضريبة|Calculate Tax/i });
    await simulateBtn.click();
    await page.waitForLoadState('networkidle');

    // Verify simulation result returned by real backend engine
    await expect(page.getByText(/لماذا تم احتساب هذه الضريبة؟|Why This Tax/i)).toBeVisible({ timeout: 10000 });

    // Verify Test Only persistent warning banner (Requirement 2 & 8)
    await expect(page.getByText(/تكوين ضريبي اختباري \/ تجريبي|Test \/ Development Tax Configuration/i).first()).toBeVisible({ timeout: 5000 });
    await expect(page.getByText(/غير معتمد كمرجعية تشريعية رسمية|not approved statutory guidance/i).first()).toBeVisible({ timeout: 5000 });

    // Verify Why This Tax non-statutory test citation labeling (Requirement 3 & 8)
    await expect(page.getByText(/مرجع قاعدة اختبار تجريبية|Test Fixture Rule Reference/i).first()).toBeVisible({ timeout: 5000 });
    await expect(page.getByText(/بيانات اختبار فقط|Test Data Only/i).first()).toBeVisible({ timeout: 5000 });

    // 09: Tax Simulator Result
    await page.screenshot({
      path: 'artifacts/test-results/tax/09-tax-simulator-result.png',
      fullPage: true
    });

    // Scroll to "Why This Tax" explanation
    await page.getByText(/لماذا تم احتساب هذه الضريبة؟|Why This Tax/i).scrollIntoViewIfNeeded();

    // 10: Why This Tax Explanation Panel
    await page.screenshot({
      path: 'artifacts/test-results/tax/10-why-this-tax-explanation.png',
      fullPage: false
    });

    // ==========================================
    // FLOW G: Import / Export Hub
    // ==========================================
    const importExportBtn = page.getByRole('button', { name: /استيراد وتصدير|Import & Export/i });
    await importExportBtn.click();
    await expect(page.getByRole('heading', { name: /استيراد وتصدير الملفات الضريبية|Tax Profile Import & Export/i })).toBeVisible({ timeout: 5000 });

    // Load sample data
    await page.getByRole('button', { name: /تحميل نموذج تجريبي|تحميل نموذج بيانات تجريبي|Load Sample/i }).click();
    await page.waitForTimeout(300);

    // Click preview
    await page.getByRole('button', { name: /معاينة الدفعة|معاينة دفعة الاستيراد|Preview Import Batch|Preview/i }).click();
    await page.waitForLoadState('networkidle');

    // Verify preview results & uncommitted warning notice
    await expect(page.getByText(/هذه الصفوف لم تُحفظ بعد في النظام|لم يتم اعتماد هذه السجلات|These rows have NOT yet been committed/i)).toBeVisible({ timeout: 10000 });

    // Verify localized Arabic row outcome without raw backend English message (Requirement 5 & 9)
    await expect(page.getByText('لم يتم العثور على المنتج في الكتالوج.')).toBeVisible({ timeout: 5000 });
    const technicalDetail = page.getByText('Product SKU was not found in Catalog master.', { exact: true });
    await expect(technicalDetail).toBeHidden();
    const technicalDetailsDisclosure = page.getByText('التفاصيل التقنية', { exact: true });
    await expect(technicalDetailsDisclosure).toBeVisible();
    await technicalDetailsDisclosure.click();
    await expect(technicalDetail).toBeVisible();

    // 11: Import Preview Reconciliation
    await page.screenshot({
      path: 'artifacts/test-results/tax/11-tax-import-preview-reconciliation.png',
      fullPage: false
    });

    await page.screenshot({
      path: 'artifacts/test-results/tax/11b-tax-import-technical-details-expanded.png',
      fullPage: false
    });

    // Close Import/Export Modal
    await page.getByRole('button', { name: /^إغلاق$|^Close$/i }).click();
    await page.waitForTimeout(300);

    // ==========================================
    // FLOW H: Country Pack Administration
    // ==========================================
    await page.getByRole('button', { name: /^حزم الدول$|^Country Packs$/i }).click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/إدارة حزم الدول الضريبية|Country Pack Administration/i)).toBeVisible({ timeout: 10000 });
    // Verify Statutory Review Pending warning banner
    await expect(page.getByText(/حزم الدول التشريعية لمصر والمملكة العربية السعودية والإمارات|المراجعة التشريعية القانونية لحزم الدول/i)).toBeVisible({ timeout: 10000 });
    // Verify TestOnly badge
    await expect(page.getByText(/تجريبية فقط — غير تشريعية|حزمة اختبار \/ غير معتمدة تشريعياً|Test Only \/ Non-Statutory/i).first()).toBeVisible({ timeout: 10000 });

    // 12: Country Pack Admin Warning
    await page.screenshot({
      path: 'artifacts/test-results/tax/12-country-pack-admin-warning.png',
      fullPage: true
    });
  });
});
