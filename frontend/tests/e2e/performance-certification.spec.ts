import { test, expect, type Page } from '@playwright/test';

const password = process.env.ERP_DEV_PASSWORD || 'TodirDev2026!';
const operationalRoutes = ['/invoicing', '/accounting', '/inventory', '/payments', '/hr', '/payroll', '/projects', '/pos'];

async function login(page: Page) {
  const csrf = await page.request.get('/api/v1/auth/csrf');
  expect(csrf.ok()).toBeTruthy();
  const { token } = await csrf.json();
  const response = await page.request.post('/api/v1/auth/login', {
    data: { email: 'admin@atlas.com', password },
    headers: { 'X-CSRF-TOKEN': token },
  });
  expect(response.status()).toBe(200);
}

test('operational route performance stays observable on the real stack', async ({ page }) => {
  test.setTimeout(120_000);
  await login(page);

  const measurements: Array<Record<string, unknown>> = [];
  for (const route of operationalRoutes) {
    await page.evaluate(() => performance.clearResourceTimings());
    const started = Date.now();
    const response = await page.goto(route, { waitUntil: 'domcontentloaded', timeout: 30_000 });
    await page.waitForTimeout(800);
    const measurement = await page.evaluate(() => {
      const navigation = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined;
      const resources = performance.getEntriesByType('resource') as PerformanceResourceTiming[];
      const scripts = resources.filter((entry) => entry.initiatorType === 'script');
      const longTasks = performance.getEntriesByType('longtask');
      return {
        domContentLoadedMs: navigation?.domContentLoadedEventEnd ?? null,
        loadEventMs: navigation?.loadEventEnd ?? null,
        resourceCount: resources.length,
        scriptCount: scripts.length,
        transferBytes: resources.reduce((sum, entry) => sum + (entry.transferSize || 0), 0),
        scriptBytes: scripts.reduce((sum, entry) => sum + (entry.transferSize || 0), 0),
        longTaskCount: longTasks.length,
        title: document.title,
      };
    });
    measurements.push({ route, httpStatus: response?.status() ?? null, wallClockMs: Date.now() - started, ...measurement });
    expect(response?.status() ?? 0).toBeLessThan(500);
    await expect(page.locator('body')).toBeVisible();
  }
  console.log(`PERFORMANCE_CERTIFICATION ${JSON.stringify(measurements)}`);
});
