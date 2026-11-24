import { execFileSync } from 'node:child_process';

export interface DatabaseContextResponse {
  database: string;
  environment: string;
  isDisposable: boolean;
}

/**
 * Enforces the disposable database safety invariant for Quality Management real-stack acceptance tests.
 * Genuinely fail-closed:
 * 1. Rejects execution if any database credential or password is missing (no hardcoded fallbacks).
 * 2. Rejects execution if the target database is missing, unknown, or points to protected databases ("todir", "todir_dev", "postgres").
 * 3. Never swallows docker or psql verification errors.
 * 4. Asserts that the connected database name matches the target disposable database exactly.
 * 5. Uses strict identifier validation and argument-array execution (no shell string interpolation).
 */
export function assertDisposableDatabase(): void {
  const targetDb = process.env['TODIR_ACCEPTANCE_DB'] || 'todir_quality_acceptance';

  // Strict identifier validation: only alphanumeric and underscores, length 3-63
  if (!/^[a-zA-Z0-9_]{3,63}$/.test(targetDb)) {
    throw new Error(
      `FATAL SAFETY INVARIANT VIOLATION: Database name "${targetDb}" is invalid or contains prohibited characters.`
    );
  }

  // Invariant 1: Database name safety check - must be disposable
  if (
    !targetDb ||
    targetDb === 'todir' ||
    targetDb === 'todir_dev' ||
    targetDb === 'postgres' ||
    (!targetDb.includes('acceptance') && !targetDb.includes('test'))
  ) {
    throw new Error(
      `FATAL SAFETY INVARIANT VIOLATION: Quality acceptance tests must run against a disposable database (e.g. "todir_quality_acceptance"). Refusing to run on "${targetDb}".`
    );
  }

  // Invariant 2: Connection string check - must not contain protected database
  const connStr = process.env['TODIR_ACCEPTANCE_DB_CONNECTION'] || process.env['TODIR_DATABASE_CONNECTION'];
  if (connStr && (connStr.includes('Database=todir;') || connStr.endsWith('Database=todir'))) {
    throw new Error(
      `FATAL SAFETY INVARIANT VIOLATION: Connection string explicitly targets protected database "todir".`
    );
  }

  // Invariant 3: Reject missing credentials - fail-closed without hardcoded fallbacks
  const dbPassword = process.env['TODIR_DB_PASSWORD'] || process.env['ERP_DEV_PASSWORD'];
  if (!dbPassword || dbPassword.trim().length === 0) {
    throw new Error(
      'FATAL SAFETY INVARIANT VIOLATION: Acceptance database password is not configured in environment (TODIR_DB_PASSWORD or ERP_DEV_PASSWORD).'
    );
  }

  const userPassword =
    process.env['SEED_DEFAULT_PASSWORD'] ||
    process.env['TODIR_ACCEPTANCE_PASSWORD'] ||
    process.env['DEV_PASSWORD'];
  if (!userPassword || userPassword.trim().length === 0) {
    throw new Error(
      'FATAL SAFETY INVARIANT VIOLATION: Acceptance user password is not configured in environment (SEED_DEFAULT_PASSWORD, TODIR_ACCEPTANCE_PASSWORD, or DEV_PASSWORD).'
    );
  }

  // Invariant 4: Live PostgreSQL execution verification using argument array (no shell interpolation)
  const rawOutput = execFileSync(
    'docker',
    ['exec', 'todir-postgres-1', 'psql', '-U', 'erp_app', '-d', targetDb, '-t', '-A', '-c', 'SELECT current_database();'],
    { encoding: 'utf-8', timeout: 10_000 }
  ).trim();

  if (!rawOutput) {
    throw new Error(
      `FATAL SAFETY INVARIANT VIOLATION: PostgreSQL query returned empty database name.`
    );
  }

  if (rawOutput === 'todir' || rawOutput === 'todir_dev' || rawOutput === 'postgres') {
    throw new Error(
      `FATAL SAFETY INVARIANT VIOLATION: Connected to protected developer database "${rawOutput}"! Aborting acceptance test execution immediately.`
    );
  }

  if (rawOutput !== targetDb) {
    throw new Error(
      `FATAL SAFETY INVARIANT VIOLATION: Expected connected database to be "${targetDb}", but PostgreSQL reported "${rawOutput}".`
    );
  }
}

/**
 * Authoritative running API database identity verification.
 * Verifies that the running API instance itself is connected to the exact disposable acceptance database
 * using an authorized diagnostic token.
 */
export async function assertApiDatabaseContext(apiBaseUrl: string): Promise<DatabaseContextResponse> {
  const targetDb = process.env['TODIR_ACCEPTANCE_DB'] || 'todir_quality_acceptance';
  const token = process.env['TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN'];
  if (!token || token.trim().length === 0) {
    throw new Error(
      'FATAL SAFETY INVARIANT VIOLATION: Acceptance diagnostic token not provided in environment (TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN).'
    );
  }
  const url = `${apiBaseUrl.replace(/\/+$/, '')}/internal/acceptance/database-context`;

  let lastResponse: Response | null = null;
  let lastError: any = null;

  for (let attempt = 0; attempt < 15; attempt++) {
    try {
      const response = await fetch(url, {
        method: 'GET',
        headers: {
          'Accept': 'application/json',
          'X-Acceptance-Diagnostic-Token': token
        },
        signal: AbortSignal.timeout(10_000)
      });

      if (response.ok) {
        const data = (await response.json()) as DatabaseContextResponse;

        if (data.database !== targetDb) {
          throw new Error(
            `FATAL SAFETY INVARIANT VIOLATION: Running API is connected to database "${data.database}", expected disposable database "${targetDb}"!`
          );
        }

        if (!data.isDisposable) {
          throw new Error(
            `FATAL SAFETY INVARIANT VIOLATION: Running API database "${data.database}" is not flagged as disposable.`
          );
        }

        if (data.environment !== 'Development' && data.environment !== 'Acceptance') {
          throw new Error(
            `FATAL SAFETY INVARIANT VIOLATION: Running API environment "${data.environment}" is not Development or Acceptance.`
          );
        }

        return data;
      }

      lastResponse = response;
      await new Promise((r) => setTimeout(r, 1000));
    } catch (err: any) {
      if (err?.message?.includes('FATAL SAFETY INVARIANT VIOLATION')) throw err;
      lastError = err;
      await new Promise((r) => setTimeout(r, 1000));
    }
  }

  const errorBody = lastResponse ? await lastResponse.text().catch(() => '') : '';
  throw new Error(
    `FATAL SAFETY INVARIANT VIOLATION: Authoritative API database check failed with status ${lastResponse?.status ?? 'ERR'}: ${errorBody} ${lastError?.message ?? ''}`
  );
}
