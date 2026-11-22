import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  workers: process.env['CI'] ? 1 : 1,
  reporter: 'html',
  // The first navigation compiles the routed module graph in local Vite; keep
  // assertions deterministic when four workers cold-start different routes.
  expect: { timeout: 10_000 },
  use: {
    baseURL: process.env['PLAYWRIGHT_BASE_URL'] || 'http://127.0.0.1:5173',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        launchOptions: {
          args: ['--no-sandbox', '--disable-gpu'],
        },
      },
    },
  ],
  webServer: process.env['PLAYWRIGHT_BASE_URL']
    ? undefined
    : [
        {
          command: 'dotnet ../backend/Api/bin/Debug/net10.0/Api.dll --urls http://127.0.0.1:5080',
          url: 'http://127.0.0.1:5080/api/v1/auth/csrf',
          reuseExistingServer: true,
          timeout: 120 * 1000,
          env: {
            TODIR_DATABASE_CONNECTION: 'Host=localhost;Port=15432;Database=todir;Username=erp_app;Password=todir-app-dev',
            TODIR_CORS_ORIGINS: 'http://127.0.0.1:5173,http://localhost:5173',
            ASPNETCORE_ENVIRONMENT: 'Development'
          }
        },
        {
          command: 'pnpm exec vite --host 127.0.0.1 --port 5173',
          url: 'http://127.0.0.1:5173/',
          reuseExistingServer: true,
          timeout: 120 * 1000,
          env: {
            NODE_OPTIONS: '--max-old-space-size=4096',
          },
        }
      ],
});
