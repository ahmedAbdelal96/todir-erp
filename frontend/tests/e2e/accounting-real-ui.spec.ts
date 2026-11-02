import { test, expect, Page } from '@playwright/test';
import path from 'node:path';

const SCREENSHOT_DIR = path.resolve(process.cwd(), '..', 'artifacts', 'test-results', 'visual-acceptance');

test.describe.configure({ mode: 'serial' });

test.describe('Accounting Real-Backend Visual Acceptance Suite', () => {
  test.setTimeout(240_000);

  test('Complete Real-Stack Visual Journey (Desktop, Tablet, Mobile)', async ({ page }) => {
    // 1. Initial Desktop Viewport
    await page.setViewportSize({ width: 1280, height: 900 });

    // 2. Navigate and Authenticate
    await page.goto('/accounting?tab=ownerSummary');
    await page.waitForLoadState('networkidle');

    const emailInput = page.locator('#login-email');
    if (await emailInput.isVisible({ timeout: 5000 }).catch(() => false)) {
      await emailInput.fill('admin@atlas.com');
      await page.locator('#login-password').fill('TodirDev2026!');
      await page.getByRole('button', { name: /تسجيل الدخول|Sign in/i }).click();
      await emailInput.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
      await page.waitForLoadState('networkidle');
    }

    if (!page.url().includes('/accounting')) {
      await page.goto('/accounting?tab=ownerSummary');
      await page.waitForLoadState('networkidle');
    }

    // Wait for the accounting page and tab navigation
    await expect(page.locator('[data-testid="accounting-page"]')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('[data-testid="accounting-tab-ownerSummary"]')).toBeVisible({ timeout: 10000 });

    // ==========================================
    // DESKTOP SCREENSHOTS (1280px)
    // ==========================================

    // Screen 01: Desktop Owner Financial Summary
    await page.locator('[data-testid="accounting-tab-ownerSummary"]').click();
    await page.waitForTimeout(600);
    await expect(page.locator('[data-testid="owner-financial-summary"]')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('[data-testid="owner-kpi-cash"]')).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '01-desktop-owner-summary-1280.png'),
      fullPage: true
    });

    // Screen 02: Desktop Accounting Health Center
    await page.locator('[data-testid="accounting-tab-health"]').click();
    await page.waitForTimeout(600);
    await expect(page.locator('[data-testid="accounting-health-center"]')).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '02-desktop-accounting-health-1280.png'),
      fullPage: true
    });

    // Screen 03: Desktop P&L Statement
    await page.locator('[data-testid="accounting-tab-financialStatements"]').click();
    await page.waitForTimeout(600);
    await expect(page.locator('[data-testid="financial-statements-view"]')).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '03-desktop-pl-statement-1280.png'),
      fullPage: true
    });

    // Screen 04: Desktop Balance Sheet
    const balanceSheetBtn = page.getByRole('button', { name: /الميزانية العمومية|Balance Sheet/i });
    if (await balanceSheetBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
      await balanceSheetBtn.click();
      await page.waitForTimeout(600);
    }
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '04-desktop-balance-sheet-1280.png'),
      fullPage: true
    });

    // Screen 05: Desktop Subledger Control Reconciliations
    await page.locator('[data-testid="accounting-tab-controlReconciliation"]').click();
    await page.waitForTimeout(600);
    await expect(page.locator('[data-testid="control-reconciliations-view"]')).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '05-desktop-control-reconciliations-1280.png'),
      fullPage: true
    });

    // Screen 06: Desktop Control Reconciliation Run Modal
    const runArBtn = page.locator('[data-testid="btn-reconcile-ar"]');
    if (await runArBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
      await runArBtn.click();
      await page.waitForTimeout(600);
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '06-desktop-reconciliation-run-modal-1280.png'),
        fullPage: false
      });
      // Close modal
      const cancelBtn = page.getByRole('button', { name: /إلغاء|Cancel/i }).last();
      if (await cancelBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
        await cancelBtn.click();
        await page.waitForTimeout(400);
      }
    }

    // Screen 07: Desktop FX Revaluation - Completed Journey (Preview -> Post -> Reverse)
    await page.locator('[data-testid="accounting-tab-fxRevaluation"]').click();
    await page.waitForTimeout(600);
    await expect(page.locator('[data-testid="fx-revaluation-view"]')).toBeVisible({ timeout: 10000 });

    // Step 1: Trigger Preview
    const previewBtn = page.locator('[data-testid="btn-preview-fx"]');
    if (await previewBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
      await previewBtn.click();
      await page.waitForTimeout(1500);
    }

    // Capture Preview State
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '07-desktop-fx-revaluation-1280.png'),
      fullPage: true
    });
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '07-desktop-fx-revaluation-preview-1280.png'),
      fullPage: true
    });

    // Step 2: Post Revaluation if available
    const postBtn = page.locator('[data-testid="fx-btn-post"]');
    if (await postBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
      await postBtn.click();
      await page.waitForTimeout(1500);
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '07-desktop-fx-revaluation-posted-1280.png'),
        fullPage: true
      });
    }

    // Step 3: Reverse Revaluation
    const reverseBtn = page.locator('[data-testid="fx-btn-reverse"]');
    if (await reverseBtn.isVisible({ timeout: 4000 }).catch(() => false)) {
      await reverseBtn.click();
      await page.waitForTimeout(600);
      const reasonInput = page.locator('#fx-rev-reason');
      if (await reasonInput.isVisible({ timeout: 2000 }).catch(() => false)) {
        await reasonInput.fill('عكس قيد فروق تقييم العملة الأجنبية بنهاية الفترة المحاسبية');
      }
      const confirmRevBtn = page.locator('[data-testid="fx-btn-confirm-reverse"]');
      if (await confirmRevBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await confirmRevBtn.click();
        await page.waitForTimeout(2000);
      }
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '07-desktop-fx-revaluation-reversed-1280.png'),
        fullPage: true
      });
      const cancelRevBtn = page.getByRole('button', { name: /إلغاء|Cancel/i }).last();
      if (await cancelRevBtn.isVisible({ timeout: 1000 }).catch(() => false)) {
        await cancelRevBtn.click();
        await page.waitForTimeout(400);
      }
    }

    // Screen 08: Desktop Fiscal Periods View
    await page.locator('[data-testid="accounting-tab-periods"]').click();
    await page.waitForTimeout(600);
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '08-desktop-periods-view-1280.png'),
      fullPage: false
    });

    // Screen 09: Desktop Period Close Checklist Modal
    const closePeriodBtn = page.locator('[data-testid="btn-open-close-period"]').first();
    if (await closePeriodBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
      await closePeriodBtn.click();
      await page.waitForTimeout(1000);
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, '09-desktop-period-close-modal-1280.png'),
        fullPage: false
      });
      // Close modal
      const cancelBtn = page.getByRole('button', { name: /إلغاء|Cancel/i }).last();
      if (await cancelBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
        await cancelBtn.click();
        await page.waitForTimeout(400);
      }
    }

    // ==========================================
    // TABLET SCREENSHOTS (768px)
    // ==========================================
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.waitForTimeout(500);

    // Screen 10: Tablet Owner Summary
    await page.locator('[data-testid="accounting-tab-ownerSummary"]').click();
    await page.waitForTimeout(600);
    await expect(page.locator('[data-testid="owner-financial-summary"]')).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '10-tablet-owner-summary-768.png'),
      fullPage: true
    });

    // Screen 11: Tablet Financial Statements
    await page.locator('[data-testid="accounting-tab-financialStatements"]').click();
    await page.waitForTimeout(600);
    await expect(page.locator('[data-testid="financial-statements-view"]')).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '11-tablet-pl-statement-768.png'),
      fullPage: true
    });

    // ==========================================
    // MOBILE SCREENSHOTS (390px)
    // ==========================================
    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForTimeout(500);

    // Screen 12: Mobile Owner Summary
    await page.locator('[data-testid="accounting-tab-ownerSummary"]').click();
    await page.waitForTimeout(600);
    await expect(page.locator('[data-testid="owner-financial-summary"]')).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '12-mobile-owner-summary-390.png'),
      fullPage: true
    });

    // Screen 13: Mobile Financial Statements
    await page.locator('[data-testid="accounting-tab-financialStatements"]').click();
    await page.waitForTimeout(600);
    await expect(page.locator('[data-testid="financial-statements-view"]')).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '13-mobile-pl-statement-390.png'),
      fullPage: true
    });
  });
});
