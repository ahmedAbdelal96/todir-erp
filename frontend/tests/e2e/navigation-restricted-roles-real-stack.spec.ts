import { expect, Page, test } from "@playwright/test";

const password = process.env.TODIR_RESTRICTED_ACCEPTANCE_PASSWORD;
if (!password) {
  throw new Error("TODIR_RESTRICTED_ACCEPTANCE_PASSWORD is required for the isolated restricted-role acceptance suite.");
}

const tamperedEmployeeId = "00000000-0000-0000-0000-000000000099";

const accounts = {
  hr: "acceptance-hr@atlas.com",
  payroll: "acceptance-payroll@atlas.com",
  projects: "acceptance-projects@atlas.com",
  finance: "acceptance-finance@atlas.com"
} as const;

async function login(page: Page, email: string): Promise<void> {
  const csrf = await page.request.get("/api/v1/auth/csrf");
  expect(csrf.status()).toBe(200);
  const { token } = await csrf.json() as { token: string };
  const response = await page.request.post("/api/v1/auth/login", {
    data: { email, password },
    headers: { "X-CSRF-TOKEN": token }
  });
  expect(response.status()).toBe(200);
}

async function openCommandPalette(page: Page, query: string): Promise<void> {
  await page.getByRole("button", { name: /البحث السريع|quick search/i }).click();
  const input = page.locator('input[role="combobox"]');
  await expect(input).toBeVisible();
  await input.fill(query);
}

async function seedNavigationPreference(page: Page, endpoint: string, navigationKey: string): Promise<void> {
  const csrf = await page.request.get("/api/v1/auth/csrf");
  expect(csrf.status()).toBe(200);
  const { token } = await csrf.json() as { token: string };
  const response = await page.request.fetch(`/api/v1/me/navigation-preferences/${endpoint}`, {
    method: endpoint === "favorites" ? "PUT" : "POST",
    data: { navigationKey },
    headers: { "X-CSRF-TOKEN": token }
  });
  expect(response.status()).toBe(204);
}

async function assertQuickPanelExcludes(page: Page, forbiddenLabel: RegExp): Promise<void> {
  const favorites = page.locator('button.sidebar-quick-trigger[aria-label="المفضلة"]');
  if (await favorites.count()) {
    await favorites.click();
    await expect(page.locator("#sidebar-quick-popover")).not.toContainText(forbiddenLabel);
  }
  const recent = page.locator('button.sidebar-quick-trigger[aria-label="آخر الصفحات"]');
  if (await recent.count()) {
    await recent.click();
    await expect(page.locator("#sidebar-quick-popover")).not.toContainText(forbiddenLabel);
  }
}

async function assertForbidden(page: Page, route: string): Promise<void> {
  await page.goto(route);
  await page.waitForLoadState("domcontentloaded");
  await expect(page.locator('[data-testid="permission-denied-view"]')).toBeVisible({ timeout: 20_000 });
}

test.describe.configure({ mode: "serial" });
test.describe("restricted-role navigation on the real local stack", () => {
  test.setTimeout(120_000);

  test("HR employee keeps self-service and leave identity-bound", async ({ page }) => {
    await login(page, accounts.hr);

    await page.goto("/hr/self-service");
    await page.waitForLoadState("domcontentloaded");
    await expect(page.locator('[data-testid="hr-self-service-tab"]')).toBeVisible({ timeout: 20_000 });

    const employeeRequests: string[] = [];
    page.on("request", request => {
      if (request.url().includes("/api/v1/hr/employees/")) employeeRequests.push(request.url());
    });
    await page.goto(`/hr/leave?employeeId=${tamperedEmployeeId}&view=compact`);
    await page.waitForLoadState("domcontentloaded");
    await expect(page).toHaveURL(/\/hr\/leave\?view=compact$/);
    await expect(page.locator('[data-testid="hr-leave-tab"]')).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('[data-testid="employee-workspace"]')).toHaveCount(0);
    expect(employeeRequests.some(url => url.includes(tamperedEmployeeId))).toBe(false);

    await assertForbidden(page, "/hr/directory");

    await page.goto("/hr/self-service");
    await page.waitForLoadState("domcontentloaded");
    await openCommandPalette(page, "دليل الموظفين");
    await expect(page.locator("#command-palette-list [role=option]")).toHaveCount(0);

    // Server-side preference tampering must not resurrect an unauthorized destination.
    await seedNavigationPreference(page, "favorites", "hr-directory");
    await seedNavigationPreference(page, "recent", "hr-directory");
    await page.evaluate(() => window.localStorage.clear());
    await page.reload();
    await page.waitForLoadState("domcontentloaded");
    await expect(page.locator('[data-testid="hr-self-service-tab"]')).toBeVisible({ timeout: 20_000 });
    await assertQuickPanelExcludes(page, /دليل الموظفين|Employee Directory/i);
  });

  test("Payroll employee sees own payslips but not runs or setup", async ({ page }) => {
    await login(page, accounts.payroll);

    await page.goto("/payroll/payslips");
    await page.waitForLoadState("domcontentloaded");
    await expect(page.locator('[data-testid="payroll-page"]')).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('[data-testid="payroll-tab-payslips"]')).toBeVisible();
    await assertForbidden(page, "/payroll/runs");
    await assertForbidden(page, "/payroll/setup");

    await page.goto("/payroll/payslips");
    await openCommandPalette(page, "إدارة دورات الرواتب");
    await expect(page.locator("#command-palette-list [role=option]")).toHaveCount(0);
  });

  test("Projects operator can load resources without project-read access", async ({ page }) => {
    await login(page, accounts.projects);
    const projectRequests: string[] = [];
    page.on("request", request => {
      if (request.url().includes("/api/v1/projects/")) projectRequests.push(request.url());
    });

    await page.goto("/projects/resource-load");
    await page.waitForLoadState("domcontentloaded");
    await expect(page.locator('[data-testid="projects-resource-load-tab"]')).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('[data-testid="permission-denied-view"]')).toHaveCount(0);
    expect(projectRequests.some(url => url.includes("/resource-load"))).toBe(true);
    expect(projectRequests.some(url => /\/legal-entities\/[^/]+(?:$|\?)/.test(url))).toBe(false);
    expect(projectRequests.some(url => url.includes("/portfolio") || url.includes("/templates"))).toBe(false);

    await assertForbidden(page, "/projects/templates");
    await page.goto("/projects/resource-load");
    await openCommandPalette(page, "محفظة المشاريع");
    await expect(page.locator("#command-palette-list [role=option]")).toHaveCount(0);
    await page.keyboard.press("Escape");

    await page.getByRole("button", { name: "التبديل إلى الإنجليزية" }).click();
    await expect(page.locator("html")).toHaveAttribute("lang", "en");
    await expect(page.locator("html")).toHaveAttribute("dir", "ltr");
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.locator('[data-testid="projects-resource-load-tab"]')).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
  });

  test("Finance operator keeps limited Expenses and Payments navigation", async ({ page }) => {
    await login(page, accounts.finance);

    await page.goto("/expenses/self-service");
    await page.waitForLoadState("domcontentloaded");
    await expect(page.locator('[data-testid="expenses-page"]')).toBeVisible({ timeout: 20_000 });
    await assertForbidden(page, "/expenses/review");

    await page.goto("/payments");
    await page.waitForLoadState("domcontentloaded");
    await expect(page.getByRole("heading", { name: /المدفوعات|Payments/i }).first()).toBeVisible({ timeout: 20_000 });
    await assertForbidden(page, "/payments/accounts");

    await page.goto("/expenses/self-service");
    await openCommandPalette(page, "مراجعة واعتماد المصروفات");
    await expect(page.locator("#command-palette-list [role=option]")).toHaveCount(0);
  });
});
