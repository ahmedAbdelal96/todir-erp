import { test, expect } from '@playwright/test';

test.describe('Identity, Authentication & User Management Journeys', () => {
  test.beforeEach(async ({ page }) => {
    // Mock CSRF endpoint
    await page.route('**/api/v1/auth/csrf', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ token: 'mock-csrf-token' }),
      });
    });

    // Mock Authorization check endpoint
    await page.route('**/api/v1/authorization/check*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ allowed: true }),
      });
    });

    // Mock Modules endpoint
    await page.route('**/api/v1/modules', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'home' },
          { id: 'users' },
          { id: 'domain-settings' },
          { id: 'directory' },
        ]),
      });
    });

    // Mock Company Login Domain
    await page.route('**/api/v1/tenants/*/login-domain', async (route) => {
      if (route.request().method() === 'PUT') {
        const body = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ loginDomain: body.loginDomain || 'atlasgroup.com' }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ loginDomain: 'atlas.com' }),
        });
      }
    });

    // Mock Roles endpoint
    await page.route('**/api/v1/tenants/*/roles*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: '0192e2b3-5757-7928-86d4-8356f1dcbe50', code: 'Accountant', name: 'محاسب عام', isSystemTemplate: true, isActive: true, version: 1, permissions: ['accounting.journals.read'] },
          { id: '0192e2b3-5757-7928-86d4-8356f1dcbe51', code: 'SalesAgent', name: 'مندوب مبيعات', isSystemTemplate: true, isActive: true, version: 1, permissions: ['invoicing.invoices.read'] },
        ]),
      });
    });
  });

  test('JOURNEY 1 & 8: Company Admin manages users and company login domain', async ({ page }) => {
    // State: Logged-in admin
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: '0192e2b3-5757-7928-86d4-8356f1dcbe01',
          memberships: [{ id: 'm1', tenantId: '0192e2b3-5757-7928-86d4-8356f1dcbe01', displayName: 'شركة الأطلس للتجارة' }],
          context: {
            legalEntities: [{ id: 'le1', code: 'LE-01', name: 'الكيان الرئيسي' }],
            branches: [{ id: 'b1', legalEntityId: 'le1', code: 'BR-01', name: 'الفرع الرئيسي' }],
          },
        }),
      });
    });

    let currentUsers = [
      {
        id: 'usr-1',
        tenantId: '0192e2b3-5757-7928-86d4-8356f1dcbe01',
        name: 'سارة خالد',
        email: 'sara@atlas.com',
        loginPrefix: 'sara',
        status: 'Active',
        mustChangePassword: false,
        createdAt: '2026-01-10T10:00:00Z',
      },
    ];

    await page.route('**/api/v1/tenants/*/users', async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        const newUser = {
          id: 'usr-2',
          tenantId: '0192e2b3-5757-7928-86d4-8356f1dcbe01',
          name: body.name,
          email: `${body.emailPrefix}@atlas.com`,
          loginPrefix: body.emailPrefix,
          status: 'Active',
          mustChangePassword: true,
          createdAt: new Date().toISOString(),
        };
        currentUsers.push(newUser);
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            user: newUser,
            temporaryPassword: 'Temp#Password2026!',
          }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: currentUsers, totalCount: currentUsers.length, page: 1, pageSize: currentUsers.length }),
        });
      }
    });

    await page.goto('/users');

    // Verify User List Page renders in Arabic RTL
    await expect(page.getByRole('heading', { name: 'المستخدمون' })).toBeVisible();
    await expect(page.getByText('سارة خالد')).toBeVisible();
    await expect(page.getByText('sara@atlas.com')).toBeVisible();

    // Click "إضافة مستخدم"
    const addUserButton = page.getByRole('button', { name: 'إضافة مستخدم' });
    await expect(addUserButton).toBeVisible();
    await addUserButton.click();

    // Verify Create User Modal has non-editable domain badge @atlas.com
    await expect(page.getByRole('heading', { name: 'إضافة مستخدم جديد' })).toBeVisible();
    await expect(page.getByText('@atlas.com', { exact: true })).toBeVisible();

    // Fill user name and email prefix
    await page.getByLabel('الاسم الكامل').fill('أحمد محمد');
    await page.getByPlaceholder('ahmed.ali').fill('ahmed.ali');

    // Submit form
    await page.getByRole('button', { name: 'إضافة المستخدم' }).click();

    // Verify One-Time Temporary Password is displayed with copy action
    await expect(page.getByRole('heading', { name: 'تم إنشاء المستخدم بنجاح' })).toBeVisible();
    await expect(page.getByText('Temp#Password2026!')).toBeVisible();
    await expect(page.getByRole('button', { name: 'نسخ كلمة المرور' })).toBeVisible();

    // Close modal
    await page.getByRole('button', { name: 'إتمام وإغلاق' }).click();

    // Verify new user appears in list
    await expect(page.getByText('أحمد محمد')).toBeVisible();
    await expect(page.getByText('ahmed.ali@atlas.com')).toBeVisible();

    // Navigate to Login Domain Settings
    await page.goto('/domain-settings');
    await expect(page.getByRole('heading', { name: 'إعدادات تسجيل الدخول للشركة' })).toBeVisible();
    await expect(page.getByText('@atlas.com', { exact: true })).toBeVisible();

    // Open change domain dialog
    await page.getByRole('button', { name: 'تغيير نطاق تسجيل الدخول...' }).click();
    await expect(page.getByRole('heading', { name: 'تغيير نطاق تسجيل الدخول للشركة' })).toBeVisible();

    // Fill new domain and submit
    await page.getByPlaceholder('atlasgroup.com').fill('atlasgroup.com');
    await page.getByRole('button', { name: 'تأكيد تغيير النطاق' }).click();

    // Verify success confirmation
    await expect(page.getByRole('heading', { name: 'تم تغيير نطاق تسجيل الدخول' })).toBeVisible();
    await expect(page.getByText('@atlasgroup.com', { exact: true })).toBeVisible();
  });

  test('JOURNEYS 2, 3, 4, 5: Progressive Login -> Initial Password Change -> MFA -> Authenticated Session', async ({ page }) => {
    let currentAuthStep = 0; // 0: signed-out, 1: password-change-challenge, 2: mfa-challenge, 3: authenticated

    await page.route('**/api/v1/session/context', async (route) => {
      if (currentAuthStep === 3) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            authenticated: true,
            tenantId: '0192e2b3-5757-7928-86d4-8356f1dcbe01',
            memberships: [{ id: 'm1', tenantId: '0192e2b3-5757-7928-86d4-8356f1dcbe01', displayName: 'شركة الأطلس للتجارة' }],
            context: {
              legalEntities: [{ id: 'le1', code: 'LE-01', name: 'الكيان الرئيسي' }],
              branches: [{ id: 'b1', legalEntityId: 'le1', code: 'BR-01', name: 'الفرع الرئيسي' }],
            },
          }),
        });
      } else {
        await route.fulfill({ status: 401, body: '' });
      }
    });

    await page.route('**/api/v1/auth/login', async (route) => {
      const body = JSON.parse(route.request().postData() || '{}');
      if (body.email === 'ahmed.ali@atlas.com' && body.password === 'Temp#Password2026!') {
        currentAuthStep = 1;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            state: 'ChangePassword',
            challengeToken: 'mock-pw-challenge-token-12345678901234567890',
          }),
        });
      } else {
        await route.fulfill({
          status: 401,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            type: 'about:blank',
            title: 'Authentication failed',
            status: 401,
            extensions: { code: 'auth.invalid_credentials' },
          }),
        });
      }
    });

    await page.route('**/api/v1/auth/password/initial', async (route) => {
      const body = JSON.parse(route.request().postData() || '{}');
      if (body.challengeToken && body.newPassword === 'My#PermanentPass2026!') {
        currentAuthStep = 2;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            state: 'VerifyMfa',
            challengeToken: 'mock-mfa-challenge-token-12345678901234567890',
          }),
        });
      } else {
        await route.fulfill({
          status: 400,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            type: 'about:blank',
            title: 'Password change failed',
            status: 400,
            extensions: { code: 'auth.password_too_short' },
          }),
        });
      }
    });

    await page.route('**/api/v1/auth/mfa/verify', async (route) => {
      const body = JSON.parse(route.request().postData() || '{}');
      if (body.challengeToken && body.code === '123456') {
        currentAuthStep = 3;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            state: 'Authenticated',
            authenticated: true,
            tenantId: '0192e2b3-5757-7928-86d4-8356f1dcbe01',
          }),
        });
      } else {
        await route.fulfill({
          status: 401,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            type: 'about:blank',
            title: 'MFA failed',
            status: 401,
            extensions: { code: 'auth.invalid_mfa_code' },
          }),
        });
      }
    });

    await page.goto('/');

    // 1. Initial Login Screen - verify clean UI without Tenant or MFA inputs
    await expect(page.getByRole('heading', { name: 'تسجيل الدخول' })).toBeVisible();
    await expect(page.getByLabel('البريد الإلكتروني')).toBeVisible();
    await expect(page.getByLabel('كلمة المرور', { exact: true })).toBeVisible();
    await expect(page.locator('#login-tenant')).toHaveCount(0); // ZERO Tenant inputs!
    await expect(page.locator('#mfa-code')).toHaveCount(0); // ZERO initial MFA inputs!

    // Verify password field starts with type="password" and eye toggle button can show/hide
    const passwordInput = page.locator('#login-password');
    await expect(passwordInput).toHaveAttribute('type', 'password');
    const toggleButton = page.getByRole('button', { name: 'إظهار كلمة المرور' });
    await expect(toggleButton).toBeVisible();
    await toggleButton.click();
    await expect(passwordInput).toHaveAttribute('type', 'text');
    await page.getByRole('button', { name: 'إخفاء كلمة المرور' }).click();
    await expect(passwordInput).toHaveAttribute('type', 'password');

    // Fill login credentials
    await page.getByLabel('البريد الإلكتروني').fill('ahmed.ali@atlas.com');
    await page.getByLabel('كلمة المرور', { exact: true }).fill('Temp#Password2026!');
    await page.getByRole('button', { name: 'تسجيل الدخول' }).click();

    // 2. Progressive Initial Password Change Screen
    await expect(page.getByRole('heading', { name: 'تعيين كلمة المرور الجديدة' })).toBeVisible();
    await expect(page.getByLabel('كلمة المرور الجديدة', { exact: true })).toBeVisible();
    await expect(page.getByLabel('تأكيد كلمة المرور الجديدة')).toBeVisible();

    // Fill matching new password
    await page.getByLabel('كلمة المرور الجديدة', { exact: true }).fill('My#PermanentPass2026!');
    await page.getByLabel('تأكيد كلمة المرور الجديدة').fill('My#PermanentPass2026!');
    await page.getByRole('button', { name: 'حفظ كلمة المرور والمتابعة' }).click();

    // 3. Progressive MFA Screen
    await expect(page.getByRole('heading', { name: 'التحقق من هويتك' })).toBeVisible();
    await expect(page.getByLabel('رمز التحقق')).toBeVisible();

    // Fill 6-digit MFA code
    await page.getByLabel('رمز التحقق').fill('123456');
    await page.getByRole('button', { name: 'تحقق والدخول' }).click();

    // 4. Authenticated Single-Company Workspace
    await expect(page.getByRole('heading', { name: 'مساحة العمل' })).toBeVisible();
    await expect(page.getByText('شركة الأطلس للتجارة').first()).toBeVisible();
    await expect(page.locator('#tenant-select')).toHaveCount(0); // ZERO company selector!
  });

  test('JOURNEYS 6 & 7: Deactivation, Reactivation & Password Reset Lifecycle', async ({ page }) => {
    await page.route('**/api/v1/session/context', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          authenticated: true,
          tenantId: '0192e2b3-5757-7928-86d4-8356f1dcbe01',
          memberships: [{ id: 'm1', tenantId: '0192e2b3-5757-7928-86d4-8356f1dcbe01', displayName: 'شركة الأطلس للتجارة' }],
          context: {
            legalEntities: [{ id: 'le1', code: 'LE-01', name: 'الكيان الرئيسي' }],
            branches: [{ id: 'b1', legalEntityId: 'le1', code: 'BR-01', name: 'الفرع الرئيسي' }],
          },
        }),
      });
    });

    let targetUser = {
      id: 'usr-target',
      tenantId: '0192e2b3-5757-7928-86d4-8356f1dcbe01',
      name: 'محمود حسن',
      email: 'mahmoud@atlas.com',
      loginPrefix: 'mahmoud',
      status: 'Active',
      mustChangePassword: false,
      createdAt: '2026-02-01T08:00:00Z',
    };

    await page.route('**/api/v1/tenants/*/users', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [targetUser], totalCount: 1, page: 1, pageSize: 1 }),
      });
    });

    await page.route('**/api/v1/tenants/*/users/*/deactivate', async (route) => {
      targetUser.status = 'Suspended';
      await route.fulfill({ status: 204 });
    });

    await page.route('**/api/v1/tenants/*/users/*/reactivate', async (route) => {
      targetUser.status = 'Active';
      await route.fulfill({ status: 204 });
    });

    await page.route('**/api/v1/tenants/*/users/*/password-reset', async (route) => {
      targetUser.mustChangePassword = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ temporaryPassword: 'New#ResetPass2026!' }),
      });
    });

    await page.goto('/users');

    // 1. Deactivate User
    await expect(page.getByText('محمود حسن')).toBeVisible();
    await expect(page.getByText('نشط', { exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'تعطيل' }).click();
    const deactivateDialog = page.getByRole('dialog');
    await expect(deactivateDialog.getByRole('heading', { name: 'تعطيل المستخدم' })).toBeVisible();
    await deactivateDialog.getByRole('button', { name: 'تعطيل المستخدم' }).click();

    // 2. Reactivate User
    await expect(page.getByRole('button', { name: 'إعادة تفعيل' })).toBeVisible();
    await page.getByRole('button', { name: 'إعادة تفعيل' }).click();
    const reactivateDialog = page.getByRole('dialog');
    await expect(reactivateDialog.getByRole('heading', { name: 'إعادة تفعيل المستخدم' })).toBeVisible();
    await reactivateDialog.getByRole('button', { name: 'إعادة تفعيل المستخدم' }).click();

    // 3. Reset Password
    await page.getByRole('table').getByRole('button', { name: 'إعادة تعيين كلمة المرور' }).click();
    const resetDialog = page.getByRole('dialog');
    await expect(resetDialog.getByRole('heading', { name: 'إعادة تعيين كلمة المرور' })).toBeVisible();
    await resetDialog.getByRole('button', { name: 'إعادة تعيين كلمة المرور' }).click();

    // Verify New Temporary Password Display
    await expect(resetDialog.getByRole('heading', { name: 'تمت إعادة تعيين كلمة المرور' })).toBeVisible();
    await expect(resetDialog.getByText('New#ResetPass2026!')).toBeVisible();
    await resetDialog.getByRole('button', { name: 'إتمام وإغلاق' }).click();
  });
});
