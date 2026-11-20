import { test, expect } from '@playwright/test';

test.describe('App Shell Bootstrap & Localization', () => {
  test.beforeEach(async ({ page }) => {
    // Mock authentication and session context endpoints
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: 'tenant-demo',
          memberships: [
            { id: 'm1', tenantId: 'tenant-demo', displayName: 'شركة الأفق للتجارة' },
          ],
          context: {
            displayName: 'الشركة من السياق النشط',
            legalEntities: [
              { id: 'le1', code: 'LE-01', name: 'الكيان الرئيسي' },
            ],
            branches: [
              { id: 'b1', legalEntityId: 'le1', code: 'BR-01', name: 'فرع الرياض' },
            ],
          },
        }),
      });
    });

    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'home' },
          { id: 'settings' },
        ]),
      });
    });

    await page.route('**/api/v1/authorization/check*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ allowed: true }),
      });
    });

    await page.route('**/api/v1/authorization/effective-access*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          tenantId: 'tenant-demo',
          membershipId: 'm1',
          legalEntityId: 'le1',
          branchId: 'b1',
          isSuper: true,
          permissions: ['*'],
          entitledModules: ['home', 'settings'],
          moduleStatuses: {},
          version: '1'
        }),
      });
    });
  });

  test('should render in RTL with Arabic as default language', async ({ page }) => {
    await page.goto('/');

    // Verify root document attributes
    const html = page.locator('html');
    await expect(html).toHaveAttribute('lang', 'ar');
    await expect(html).toHaveAttribute('dir', 'rtl');

    // Verify brand link and workspace topbar
    await expect(page.getByRole('link', { name: 'TODIR' })).toBeVisible();
    await expect(page.getByText('الشركة من السياق النشط', { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'مساحة العمل' })).toBeVisible();
    await expect(page.getByText('الكيان القانوني', { exact: true })).toBeVisible();
    await expect(page.getByText('الفرع', { exact: true })).toBeVisible();
  });

  test('should switch language to English and update direction to LTR', async ({ page }) => {
    await page.goto('/');

    // Click language switcher button
    const langButton = page.getByRole('button', { name: 'التبديل إلى الإنجليزية' });
    await expect(langButton).toBeVisible();
    await langButton.click();

    // Verify English labels and LTR direction
    await expect(page.getByRole('heading', { name: 'Workspace' })).toBeVisible();
    await expect(page.getByText('Legal entity', { exact: true })).toBeVisible();
    await expect(page.getByText('Branch', { exact: true })).toBeVisible();

    const html = page.locator('html');
    await expect(html).toHaveAttribute('lang', 'en');
    await expect(html).toHaveAttribute('dir', 'ltr');

    const switchBack = page.getByRole('button', { name: 'Switch to Arabic' });
    await expect(switchBack).toBeVisible();

    await page.reload();
    await expect(page.getByRole('heading', { name: 'Workspace' })).toBeVisible();
    await expect(html).toHaveAttribute('lang', 'en');
    await expect(html).toHaveAttribute('dir', 'ltr');

    await page.getByRole('button', { name: 'Switch to Arabic' }).click();
    await expect(page.getByRole('heading', { name: 'مساحة العمل' })).toBeVisible();
    await expect(html).toHaveAttribute('lang', 'ar');
    await expect(html).toHaveAttribute('dir', 'rtl');
  });

  test('keeps the login surface bilingual and persists its selected language', async ({ page }) => {
    await page.unroute('**/api/v1/session/context');
    await page.route('**/api/v1/session/context', route => route.fulfill({
      status: 401,
      contentType: 'application/json',
      body: JSON.stringify({ authenticated: false })
    }));
    await page.route('**/api/v1/auth/csrf', route => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ token: 'csrf-test-token' })
    }));
    await page.route('**/api/v1/auth/login', route => route.fulfill({
      status: 401,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Authentication failed', status: 401, code: 'auth.invalid_credentials' })
    }));
    await page.goto('/');

    await expect(page.getByRole('heading', { name: 'تسجيل الدخول' })).toBeVisible();
    const atlasProfile = page.getByRole('button', { name: /Atlas — مسؤول النظام/ });
    const profileName = atlasProfile.locator('bdi').first();
    const profileEmail = atlasProfile.locator('bdi').last();
    await expect(profileName).toHaveAttribute('dir', 'auto');
    await expect(profileName).toHaveCSS('unicode-bidi', 'isolate');
    await expect(profileEmail).toHaveAttribute('dir', 'ltr');
    await expect(profileEmail).toHaveAttribute('lang', 'en');
    await expect(profileEmail).toHaveCSS('unicode-bidi', 'isolate');
    await page.getByRole('button', { name: 'التبديل إلى الإنجليزية' }).click();
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
    await expect(page.getByText('Email address')).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');

    await page.getByLabel('Email address').fill('invalid@example.test');
    await page.getByRole('textbox', { name: 'Password' }).fill('InvalidPassword2026!');
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByText('The email or password is incorrect.')).toBeVisible();
    await page.getByRole('button', { name: 'Switch to Arabic' }).click();
    await expect(page.getByText('البريد الإلكتروني أو كلمة المرور غير صحيحة.')).toBeVisible();
    await page.getByRole('button', { name: 'التبديل إلى الإنجليزية' }).click();

    await page.reload();
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
  });

  test('should preserve a real browser path on direct navigation', async ({ page }) => {
    await page.goto('/settings');

    await expect(page).toHaveURL(/\/settings$/);
    await expect(page).not.toHaveURL(/#/);
    await expect(page.getByRole('heading', { name: 'إعدادات الشركة' })).toBeVisible();
  });

  test('captures redesigned executive topbar evidence', async ({ page }) => {
    // Override session context with Delta Supplies realistic operational data
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: 'tenant-delta',
          memberships: [
            { id: 'm1', tenantId: 'tenant-delta', displayName: 'شركة دلتا للتوريدات' },
          ],
          context: {
            displayName: 'شركة دلتا للتوريدات',
            legalEntities: [
              { id: 'le1', code: 'MAIN', name: 'شركة دلتا للتوريدات' },
              { id: 'le2', code: 'SECONDARY', name: 'شركة دلتا للتوريدات — الإسكندرية' }
            ],
            branches: [
              { id: 'b1', legalEntityId: 'le1', code: 'MAIN', name: 'Delta Supplies — Main Branch' },
              { id: 'b2', legalEntityId: 'le1', code: 'BR-02', name: 'فرع الجيزة' }
            ],
          },
        }),
      });
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    // Verify redesigned header elements
    const topbar = page.locator('.topbar');
    await expect(topbar).toBeVisible();

    // Capture screenshot of the topbar
    await topbar.screenshot({
      path: 'test-results/appheader-redesign.png'
    });
  });

  test('opens command palette with Ctrl+K and verifies spotlight search', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    // 1. Verify search trigger button is visible in topbar
    const triggerBtn = page.locator('.topbar-command-trigger');
    await expect(triggerBtn).toBeVisible();

    // 2. Click or press Ctrl+K to open
    await page.keyboard.press('Control+k');

    // 3. Command palette dialog should be visible
    const palette = page.locator('.command-palette-card');
    await expect(palette).toBeVisible();

    // 4. Type search term in Arabic
    const input = page.locator('input[role="combobox"]');
    await input.fill('فاتورة');

    // 5. Verify action is filtered
    await expect(page.getByText('إنشاء فاتورة مبيعات جديدة')).toBeVisible();

    // 6. Capture visual proof
    await page.screenshot({
      path: 'test-results/command-palette-preview.png'
    });

    // 7. Press Escape to close
    await page.keyboard.press('Escape');
    await expect(palette).not.toBeVisible();
  });
});
