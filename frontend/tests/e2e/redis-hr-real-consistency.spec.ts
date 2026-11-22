import { test, expect, type Browser, type Page } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";

const USER_A = process.env.REDIS_ACCEPTANCE_USER_A ?? "admin@atlas.com";
const USER_B = process.env.REDIS_ACCEPTANCE_USER_B ?? "hr@atlas.com";
const PASSWORD = process.env.REDIS_ACCEPTANCE_PASSWORD ?? process.env.SEED_DEFAULT_PASSWORD;
const UI_A = process.env.REDIS_ACCEPTANCE_UI_A ?? "http://127.0.0.1:5173";
const UI_B = process.env.REDIS_ACCEPTANCE_UI_B ?? "http://127.0.0.1:5174";
const LEGAL_ENTITY_ID = process.env.REDIS_ACCEPTANCE_LEGAL_ENTITY_ID;
const EVIDENCE_DIR = path.resolve(process.cwd(), "..", "artifacts", "test-results", "redis-hr-real-consistency");

test.describe.configure({ mode: "serial" });
test.setTimeout(180_000);

async function login(page: Page, baseUrl: string, email: string): Promise<void> {
  await page.goto(`${baseUrl}/hr/directory`);
  const emailInput = page.getByRole("textbox", { name: /البريد الإلكتروني|Email/i });
  // The session-context request can take several seconds on a cold local API;
  // wait for the anonymous form instead of sampling it before React mounts.
  await expect(emailInput).toBeVisible({ timeout: 60_000 });
  if (!PASSWORD) throw new Error("Set REDIS_ACCEPTANCE_PASSWORD or SEED_DEFAULT_PASSWORD for the real HR acceptance.");
  await emailInput.fill(email);
  await page.getByRole("textbox", { name: /كلمة المرور|Password/i }).fill(PASSWORD);
  await page.getByRole("button", { name: /تسجيل الدخول|Sign in/i }).click();
  await expect(emailInput).toBeHidden({ timeout: 30_000 });
  await expect(page.locator('[data-testid="hr-page"]')).toBeVisible({ timeout: 25_000 });
  await expect(page.locator('[data-testid="hr-tab-directory"]')).toBeVisible({ timeout: 15_000 });
}

async function loadDirectory(page: Page, baseUrl: string): Promise<{ readonly displayName: string; readonly employeeId: string; readonly totalCount: number }> {
  await page.goto(`${baseUrl}/hr/directory`);
  await expect(page.locator('[data-testid="employee-row"]').first()).toBeVisible({ timeout: 20_000 });
  const payload = await page.evaluate(async (entityId) => {
    const query = new URLSearchParams({ page: "1", pageSize: "20" });
    if (entityId) query.set("legalEntityId", entityId);
    const response = await fetch(`/api/v1/hr/employees?${query.toString()}`);
    if (!response.ok) throw new Error(`Directory request failed: ${response.status}`);
    return await response.json() as { items?: Array<{ id: string; displayName: string }>; totalCount?: number };
  }, LEGAL_ENTITY_ID ?? "");
  const employee = payload.items?.[0];
  if (!employee) throw new Error("The HR acceptance database returned no employee in the directory.");
  return { displayName: employee.displayName, employeeId: employee.id, totalCount: payload.totalCount ?? 0 };
}

async function configureEntity(page: Page): Promise<void> {
  if (LEGAL_ENTITY_ID) {
    await page.addInitScript((entityId) => {
      window.sessionStorage.setItem("erp.current.legalEntityId", entityId);
    }, LEGAL_ENTITY_ID);
  }
}

test("two real HR users see a profile correction without refresh across two API instances", async ({ browser }: { browser: Browser }) => {
  test.skip(!PASSWORD, "Set REDIS_ACCEPTANCE_PASSWORD or SEED_DEFAULT_PASSWORD to run the real-stack acceptance.");
  fs.mkdirSync(EVIDENCE_DIR, { recursive: true });

  const contextA = await browser.newContext();
  const contextB = await browser.newContext();
  const pageA = await contextA.newPage();
  const pageB = await contextB.newPage();
  await configureEntity(pageA);
  await configureEntity(pageB);

  try {
    await login(pageA, UI_A, USER_A);
    const first = await loadDirectory(pageA, UI_A);
    await pageA.getByText(first.displayName, { exact: true }).first().click();
    await expect(pageA.locator('[data-testid="employee-workspace"]')).toBeVisible({ timeout: 20_000 });
    await expect(pageA.getByRole("heading", { name: first.displayName, exact: true })).toBeVisible();

    await login(pageB, UI_B, USER_B);
    const second = await loadDirectory(pageB, UI_B);
    expect(second.employeeId).toBe(first.employeeId);

    const nextPage = pageB.getByRole("button", { name: /التالي|Next/i });
    await expect(nextPage).toBeVisible({ timeout: 10_000 });
    await expect(nextPage).toBeEnabled();
    await nextPage.click();
    await expect(pageB.getByRole("button", { name: /الصفحة 2|Page 2/i })).toHaveAttribute("aria-current", "page");
    const previousPage = pageB.getByRole("button", { name: /السابق|Previous/i });
    await previousPage.click();
    await expect(pageB.getByRole("button", { name: /الصفحة 1|Page 1/i })).toHaveAttribute("aria-current", "page");

    const search = pageB.getByPlaceholder(/البحث بالاسم|Search by name/i);
    await search.fill(first.displayName);
    await expect(pageB.getByText(first.displayName, { exact: true }).first()).toBeVisible({ timeout: 15_000 });
    await pageB.getByText(first.displayName, { exact: true }).first().click();
    await expect(pageB.locator('[data-testid="employee-workspace"]')).toBeVisible({ timeout: 20_000 });
    await expect(pageB.locator('[data-testid="workspace-action-edit-profile"]')).toBeVisible({ timeout: 15_000 });

    const newName = `اختبار اتساق Redis ${Date.now()}`;
    const dialog = pageB.getByRole("dialog");
    await pageB.locator('[data-testid="workspace-action-edit-profile"]').click();
    await expect(dialog).toBeVisible();
    const fields = dialog.locator("input");
    await fields.nth(0).fill(newName);
    await fields.nth(3).fill("Real two-user Redis freshness acceptance");

    const startedAt = Date.now();
    const updateResponsePromise = pageB.waitForResponse((response) =>
      response.request().method() === "PUT" && response.url().includes(`/api/v1/hr/employees/${first.employeeId}/profile`),
    { timeout: 20_000 });
    await dialog.getByRole("button", { name: /حفظ التعديل|Save change/i }).click();
    const updateResponse = await updateResponsePromise;
    expect(updateResponse.status()).toBe(200);
    const updated = await updateResponse.json() as { arabicName: string; version: number };
    expect(updated.arabicName).toBe(newName);
    expect(updated.version).toBeGreaterThan(0);

    await expect(dialog).toBeHidden({ timeout: 15_000 });
    await expect(pageA.getByRole("heading", { name: newName, exact: true })).toBeVisible({ timeout: 35_000 });
    const freshnessLatencyMs = Date.now() - startedAt;
    await expect(pageA.getByRole("heading", { name: first.displayName, exact: true })).toHaveCount(0);

    await pageB.getByRole("button", { name: /العودة لسجل الموظفين|العودة إلى الدليل|Back to directory/i }).click();
    await expect(search).toBeVisible({ timeout: 15_000 });
    await search.fill(newName);
    await expect(pageB.getByText(newName, { exact: true }).first()).toBeVisible({ timeout: 15_000 });

    await pageA.screenshot({ path: path.join(EVIDENCE_DIR, "user-a-live-update.png"), fullPage: true });
    await pageB.screenshot({ path: path.join(EVIDENCE_DIR, "user-b-updated-record.png"), fullPage: true });
    fs.writeFileSync(path.join(EVIDENCE_DIR, "acceptance.json"), JSON.stringify({
      userA: USER_A,
      userB: USER_B,
      apiInstanceA: UI_A,
      apiInstanceB: UI_B,
      employeeId: first.employeeId,
      originalName: first.displayName,
      updatedName: newName,
      totalCount: first.totalCount,
      explicitLegalEntity: LEGAL_ENTITY_ID ?? null,
      freshnessLatencyMs,
      manualRefreshUsed: false,
      verifiedAtUtc: new Date().toISOString()
    }, null, 2));
  } finally {
    await contextA.close().catch(() => undefined);
    await contextB.close().catch(() => undefined);
  }
});
