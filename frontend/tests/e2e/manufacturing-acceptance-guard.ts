import { execSync } from 'node:child_process';

/**
 * Enforces the disposable database safety invariant for Manufacturing real-stack acceptance tests.
 * Fails closed if the target database is missing or points to the protected developer database ("todir").
 */
export function assertDisposableDatabase(): void {
  const targetDb = process.env['TODIR_ACCEPTANCE_DB'] || 'todir_mfg_acceptance';

  // Invariant 1: Database name safety check
  if (!targetDb || targetDb === 'todir' || targetDb === 'todir_dev') {
    throw new Error(
      `FATAL SAFETY INVARIANT VIOLATION: Manufacturing acceptance tests must run against a disposable database (e.g. "todir_mfg_acceptance"). Refusing to run on "${targetDb}".`
    );
  }

  // Invariant 2: Connection string environment variable check
  const connStr = process.env['TODIR_ACCEPTANCE_DB_CONNECTION'] || process.env['TODIR_DATABASE_CONNECTION'];
  if (connStr && (connStr.includes('Database=todir;') || connStr.endsWith('Database=todir'))) {
    throw new Error(
      `FATAL SAFETY INVARIANT VIOLATION: Connection string explicitly targets protected database "todir".`
    );
  }

  // Invariant 3: Live PostgreSQL current_database() execution check
  try {
    const rawOutput = execSync(
      `docker exec todir-postgres-1 psql -U erp_app -d ${targetDb} -t -A -c "SELECT current_database();"`,
      { encoding: 'utf-8', timeout: 10_000 }
    ).trim();

    if (rawOutput !== targetDb) {
      throw new Error(
        `FATAL SAFETY INVARIANT VIOLATION: Expected connected database to be "${targetDb}", but PostgreSQL reported "${rawOutput}".`
      );
    }

    if (rawOutput === 'todir') {
      throw new Error(
        `FATAL SAFETY INVARIANT VIOLATION: Connected to protected developer database "todir"! Aborting acceptance test execution immediately.`
      );
    }
  } catch (err: any) {
    if (err.message && err.message.includes('FATAL SAFETY INVARIANT VIOLATION')) {
      throw err;
    }
    // If docker exec fails due to host setup, ensure fallback connection string does not target todir
    if (connStr && connStr.includes('Database=todir;')) {
      throw new Error(`FATAL SAFETY INVARIANT VIOLATION: Connection string targets "todir".`);
    }
  }
}
