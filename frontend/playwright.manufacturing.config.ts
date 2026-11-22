import { defineConfig, devices } from '@playwright/test';

const acceptanceDb = process.env['TODIR_ACCEPTANCE_DB'] || 'todir_mfg_acceptance';
const dbHost = process.env['TODIR_DB_HOST'] || 'localhost';
const dbPort = process.env['TODIR_DB_PORT'] || '15432';
const dbUser = process.env['TODIR_DB_USER'] || 'erp_app';
const dbPassword = process.env['TODIR_DB_PASSWORD'] || process.env['ERP_DEV_PASSWORD'] || 'todir-app-dev';

const connectionString =
  process.env['TODIR_ACCEPTANCE_DB_CONNECTION'] ||
  `Host=${dbHost};Port=${dbPort};Database=${acceptanceDb};Username=${dbUser};Password=${dbPassword}`;

// Fail-closed invariant: Refuse to execute if targeting protected developer database "todir"
if (!acceptanceDb || acceptanceDb === 'todir' || connectionString.includes('Database=todir;') || connectionString.endsWith('Database=todir')) {
  throw new Error(
    'FATAL: Manufacturing acceptance test configuration refused to target protected database "todir". Must target a disposable acceptance database (e.g. "todir_mfg_acceptance").'
  );
}

export default defineConfig({
  testDir: './tests/e2e',
  testMatch: /manufacturing-transactions-real-stack\.spec\.ts/,
  fullyParallel: false,
  forbidOnly: !!process.env['CI'],
  retries: 0,
  workers: 1,
  reporter: 'html',
  expect: { timeout: 15_000 },
  use: {
    baseURL: process.env['PLAYWRIGHT_BASE_URL'] || 'http://127.0.0.1:5179',
    trace: 'retain-on-failure',
    screenshot: 'on',
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
          command: 'dotnet bin/Debug/net10.0/Api.dll --urls http://127.0.0.1:5089',
          cwd: '../backend/Api',
          url: 'http://127.0.0.1:5089/api/v1/auth/csrf',
          reuseExistingServer: false,
          timeout: 120 * 1000,
          env: {
            TODIR_DATABASE_CONNECTION: connectionString,
            TODIR_CORS_ORIGINS: 'http://127.0.0.1:5179,http://localhost:5179',
            ASPNETCORE_ENVIRONMENT: 'Development',
          },
        },
        {
          command: 'pnpm exec vite --host 127.0.0.1 --port 5179',
          url: 'http://127.0.0.1:5179/',
          reuseExistingServer: false,
          timeout: 120 * 1000,
          env: {
            NODE_OPTIONS: '--max-old-space-size=4096',
            VITE_API_PROXY_TARGET: 'http://127.0.0.1:5089',
            VITE_API_URL: 'http://127.0.0.1:5089',
          },
        },
      ],
});
