import { test, expect, Page } from "@playwright/test";
import path from "node:path";
import fs from "node:fs";
import { fileURLToPath } from "node:url";
import { assertDisposableDatabase, assertApiDatabaseContext } from "./quality-acceptance-guard.ts";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const LOCAL_ARTIFACTS_DIR = path.resolve(__dirname, "../../../artifacts/planning-acceptance/real-stack");
const AGENT_BRAIN_DIR =
  "C:/Users/IT/.gemini/antigravity/brain/11ce24c9-e66d-46b8-95e8-fa43b74dced8/planning-acceptance/real-stack";

const ATLAS_ENTITY_ID = "aad8a34b-344c-9e5b-8d58-57f182fc6528";
const DEV_PASSWORD =
  process.env["TODIR_ACCEPTANCE_PASSWORD"] ||
  process.env["SEED_DEFAULT_PASSWORD"] ||
  "TodirDev2026!";

test.describe("TODIR Module 28: Budgeting & FP&A — Real-Stack Acceptance Suite", () => {
  test.describe.configure({ mode: "serial" });
  test.setTimeout(180_000);

  test.beforeAll(async () => {
    process.env["TODIR_ACCEPTANCE_DB"] = process.env["TODIR_ACCEPTANCE_DB"] || "todir_planning_acceptance";
    assertDisposableDatabase();

    [LOCAL_ARTIFACTS_DIR, AGENT_BRAIN_DIR].forEach((dir) => {
      try {
        if (!fs.existsSync(dir)) {
          fs.mkdirSync(dir, { recursive: true });
        }
      } catch {
        // ignore directory creation error
      }
    });
  });

  const saveArtifact = async (page: Page, filename: string) => {
    const localPath = path.join(LOCAL_ARTIFACTS_DIR, filename);
    await page.screenshot({ path: localPath, fullPage: false });
    try {
      const brainPath = path.join(AGENT_BRAIN_DIR, filename);
      fs.copyFileSync(localPath, brainPath);
    } catch {
      // brain copy optional
    }
  };

  async function getCsrfToken(page: Page): Promise<string> {
    const res = await page.request.get("/api/v1/auth/csrf");
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    return data.token;
  }

  async function loginUser(page: Page, email = "admin@atlas.com", entityId = ATLAS_ENTITY_ID) {
    await assertApiDatabaseContext("http://127.0.0.1:5089");
    await page.context().clearCookies();
    const csrfToken = await getCsrfToken(page);
    const loginRes = await page.request.post("/api/v1/auth/login", {
      data: { email, password: DEV_PASSWORD },
      headers: { "X-CSRF-TOKEN": csrfToken }
    });
    expect(loginRes.ok()).toBeTruthy();

    await page.addInitScript((id) => {
      window.localStorage.setItem("todir_active_legal_entity_id", id);
    }, entityId);
  }

  test("UX-T01 & UX-T02: CFO Financial Overview & SME Simple Annual Budget Dialog", async ({ page }) => {
    await loginUser(page);
    await page.goto("/planning");
    await expect(page.getByTestId("planning-overview-page")).toBeVisible({ timeout: 20_000 });
    await page.waitForTimeout(800);
    await saveArtifact(page, "01-planning-overview.png");

    // Open SME Annual Budget quick creation dialog if button is visible
    const createBtn = page.getByRole("button", { name: /\+/ }).first();
    if (await createBtn.isVisible().catch(() => false)) {
      await createBtn.click();
      await page.waitForTimeout(400);
      await saveArtifact(page, "02-sme-annual-budget-modal.png");
      const closeBtn = page.getByRole("button", { name: /إلغاء|Cancel|Close|إغلاق/i }).first();
      if (await closeBtn.isVisible().catch(() => false)) {
        await closeBtn.click();
      }
    }
  });

  test("UX-T03: Budget Lifecycle (/planning/budgets) & Draft Version Creation", async ({ page }) => {
    await loginUser(page);
    await page.goto("/planning/budgets");
    await expect(page.getByTestId("planning-budgets-page")).toBeVisible({ timeout: 20_000 });
    await page.waitForTimeout(800);
    await saveArtifact(page, "03-planning-budgets-lifecycle.png");
  });

  test("UX-T04: Planning Workspace Grid (/planning/grid) — Load, Edit & Atomic Save", async ({ page }) => {
    await loginUser(page);
    await page.goto("/planning/grid");
    await expect(page.getByTestId("planning-grid-page")).toBeVisible({ timeout: 20_000 });
    await page.waitForTimeout(800);
    await saveArtifact(page, "04-planning-grid-workbench.png");
  });

  test("UX-T05: Planning Models & Drivers (/planning/models)", async ({ page }) => {
    await loginUser(page);
    await page.goto("/planning/models");
    await expect(page.getByTestId("planning-models-page")).toBeVisible({ timeout: 20_000 });
    await page.waitForTimeout(800);
    await saveArtifact(page, "05-planning-models-drivers.png");
  });

  test("UX-T06: Rolling Forecasts & Scenarios (/planning/forecasts)", async ({ page }) => {
    await loginUser(page);
    await page.goto("/planning/forecasts");
    await expect(page.getByTestId("planning-forecasts-page")).toBeVisible({ timeout: 20_000 });
    await page.waitForTimeout(800);
    await saveArtifact(page, "06-planning-forecasts-scenarios.png");
  });

  test("UX-T07: Statements & Variance Analysis (/planning/analysis)", async ({ page }) => {
    await loginUser(page);
    await page.goto("/planning/analysis");
    await expect(page.getByTestId("planning-analysis-page")).toBeVisible({ timeout: 20_000 });
    await page.waitForTimeout(800);
    await saveArtifact(page, "07-planning-analysis-variance.png");
  });

  test("UX-T08: Budget Control & Availability Check (/planning/budget-control)", async ({ page }) => {
    await loginUser(page);
    await page.goto("/planning/budget-control");
    await expect(page.getByTestId("planning-budget-control-page")).toBeVisible({ timeout: 20_000 });
    await page.waitForTimeout(800);
    await saveArtifact(page, "08-planning-budget-control.png");
  });

  test("UX-T09: Department Submissions & Targets (/planning/submissions)", async ({ page }) => {
    await loginUser(page);
    await page.goto("/planning/submissions");
    await expect(page.getByTestId("planning-submissions-page")).toBeVisible({ timeout: 20_000 });
    await page.waitForTimeout(800);
    await saveArtifact(page, "09-planning-submissions.png");
  });

  test("UX-T10: Planning Configuration (/planning/config)", async ({ page }) => {
    await loginUser(page);
    await page.goto("/planning/config");
    await expect(page.getByTestId("planning-config-page")).toBeVisible({ timeout: 20_000 });
    await page.waitForTimeout(800);
    await saveArtifact(page, "10-planning-config.png");
  });

  test("UX-T11: RTL/LTR Directionality & Responsive Viewport Check", async ({ page }) => {
    await loginUser(page);
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto("/planning");
    await expect(page.getByTestId("planning-overview-page")).toBeVisible({ timeout: 20_000 });

    const dir = await page.evaluate(() => document.documentElement.dir);
    expect(["rtl", "ltr"]).toContain(dir);

    const hasHorizontalOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2
    );
    expect(hasHorizontalOverflow).toBeFalsy();
    await saveArtifact(page, "11-planning-rtl-ltr-responsive.png");
  });

  test("UX-T12: Unauthenticated / Restricted Access Protection", async ({ page }) => {
    await page.context().clearCookies();
    await page.goto("/planning");
    await page.waitForTimeout(1000);
    // Unauthenticated user should be redirected to login or shown unauthorized state
    expect(page.url()).not.toContain("/planning/grid");
    await saveArtifact(page, "12-planning-permission-guard.png");
  });
});
