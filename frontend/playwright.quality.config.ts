import { defineConfig, devices } from '@playwright/test';

const acceptanceDb = process.env['TODIR_ACCEPTANCE_DB'] || 'todir_quality_acceptance';
const dbHost = process.env['TODIR_DB_HOST'] || 'localhost';
const dbPort = process.env['TODIR_DB_PORT'] || '15432';
const dbUser = process.env['TODIR_DB_USER'] || 'erp_app';
const dbPassword = process.env['TODIR_DB_PASSWORD'] || process.env['ERP_DEV_PASSWORD'];

if (!dbPassword || dbPassword.trim().length === 0) {
  throw new Error('FATAL: Database password not provided in environment (TODIR_DB_PASSWORD or ERP_DEV_PASSWORD).');
}

const diagnosticToken = process.env['TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN'];
if (!diagnosticToken || diagnosticToken.trim().length === 0) {
  throw new Error('FATAL: Acceptance diagnostic token not provided in environment (TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN).');
}

const connectionString =
  process.env['TODIR_ACCEPTANCE_DB_CONNECTION'] ||
  `Host=${dbHost};Port=${dbPort};Database=${acceptanceDb};Username=${dbUser};Password=${dbPassword}`;

// Fail-closed invariant: Refuse to execute if targeting protected developer database "todir"
if (
  !acceptanceDb ||
  acceptanceDb === 'todir' ||
  acceptanceDb === 'todir_dev' ||
  acceptanceDb === 'postgres' ||
  (!acceptanceDb.includes('acceptance') && !acceptanceDb.includes('test')) ||
  connectionString.includes('Database=todir;') ||
  connectionString.endsWith('Database=todir')
) {
  throw new Error(
    'FATAL: Quality acceptance test configuration refused to target protected database "todir". Must target a disposable acceptance database (e.g. "todir_quality_acceptance").'
  );
}

export default defineConfig({
  testDir: './tests/e2e',
  testMatch: /quality-real-stack\.spec\.ts/,
  fullyParallel: false,
  forbidOnly: !!process.env['CI'],
  retries: 0,
  workers: 1,
  reporter: 'html',
  expect: { timeout: 15_000 },
  use: {
    baseURL: 'http://127.0.0.1:5178',
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
  webServer: [
    {
      command: 'dotnet bin/Debug/net10.0/Api.dll --urls http://127.0.0.1:5088',
      cwd: '../backend/Api',
      url: 'http://127.0.0.1:5088/api/v1/auth/csrf',
      reuseExistingServer: false,
      stdout: 'pipe',
      stderr: 'pipe',
      timeout: 120 * 1000,
      env: {
        TODIR_DATABASE_CONNECTION: connectionString,
        TODIR_CORS_ORIGINS: 'http://127.0.0.1:5178,http://localhost:5178',
        ASPNETCORE_ENVIRONMENT: 'Development',
        TODIR_ENABLE_ACCEPTANCE_DIAGNOSTICS: 'true',
        TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN: diagnosticToken,
      },
    },
    {
      command: 'pnpm exec vite --host 127.0.0.1 --port 5178',
      url: 'http://127.0.0.1:5178/',
      reuseExistingServer: false,
      stdout: 'pipe',
      stderr: 'pipe',
      timeout: 120 * 1000,
      env: {
        NODE_OPTIONS: '--max-old-space-size=4096',
        VITE_API_PROXY_TARGET: 'http://127.0.0.1:5088',
        VITE_API_URL: 'http://127.0.0.1:5088',
      },
    },
  ],
});
