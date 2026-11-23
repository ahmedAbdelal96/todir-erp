import test, { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { assertDisposableDatabase, assertApiDatabaseContext } from '../e2e/manufacturing-acceptance-guard.ts';

describe('Manufacturing Acceptance Guard: Fail-Closed Protection Tests', () => {
  const originalEnv = { ...process.env };

  const restoreEnv = () => {
    process.env = { ...originalEnv };
  };

  it('rejects execution if target database is protected "todir"', () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir';
    process.env['TODIR_DB_PASSWORD'] = 'test-pass';
    process.env['SEED_DEFAULT_PASSWORD'] = 'test-user-pass';

    assert.throws(
      () => assertDisposableDatabase(),
      /FATAL SAFETY INVARIANT VIOLATION: Manufacturing acceptance tests must run against a disposable database.*Refusing to run on "todir"/
    );
    restoreEnv();
  });

  it('rejects execution if target database is protected "todir_dev"', () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir_dev';
    process.env['TODIR_DB_PASSWORD'] = 'test-pass';
    process.env['SEED_DEFAULT_PASSWORD'] = 'test-user-pass';

    assert.throws(
      () => assertDisposableDatabase(),
      /Refusing to run on "todir_dev"/
    );
    restoreEnv();
  });

  it('rejects execution if target database does not contain "acceptance" or "test"', () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'production_db';
    process.env['TODIR_DB_PASSWORD'] = 'test-pass';
    process.env['SEED_DEFAULT_PASSWORD'] = 'test-user-pass';

    assert.throws(
      () => assertDisposableDatabase(),
      /Refusing to run on "production_db"/
    );
    restoreEnv();
  });

  it('rejects execution if database name contains invalid characters or injection attempt', () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir_mfg_acceptance; rm -rf /';
    process.env['TODIR_DB_PASSWORD'] = 'test-pass';
    process.env['SEED_DEFAULT_PASSWORD'] = 'test-user-pass';

    assert.throws(
      () => assertDisposableDatabase(),
      /Database name ".*" is invalid or contains prohibited characters/
    );
    restoreEnv();
  });

  it('rejects execution if database connection string targets "todir"', () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir_mfg_acceptance';
    process.env['TODIR_ACCEPTANCE_DB_CONNECTION'] = 'Host=localhost;Database=todir;Username=erp_app';
    process.env['TODIR_DB_PASSWORD'] = 'test-pass';
    process.env['SEED_DEFAULT_PASSWORD'] = 'test-user-pass';

    assert.throws(
      () => assertDisposableDatabase(),
      /Connection string explicitly targets protected database "todir"/
    );
    restoreEnv();
  });

  it('rejects execution if database password is missing from environment', () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir_mfg_acceptance';
    delete process.env['TODIR_DB_PASSWORD'];
    delete process.env['ERP_DEV_PASSWORD'];
    process.env['SEED_DEFAULT_PASSWORD'] = 'test-user-pass';

    assert.throws(
      () => assertDisposableDatabase(),
      /Acceptance database password is not configured in environment/
    );
    restoreEnv();
  });

  it('rejects execution if user seed password is missing from environment', () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir_mfg_acceptance';
    process.env['TODIR_DB_PASSWORD'] = 'test-pass';
    delete process.env['SEED_DEFAULT_PASSWORD'];
    delete process.env['TODIR_ACCEPTANCE_PASSWORD'];
    delete process.env['DEV_PASSWORD'];

    assert.throws(
      () => assertDisposableDatabase(),
      /Acceptance user password is not configured in environment/
    );
    restoreEnv();
  });

  it('rejects API check if diagnostic token is missing or empty in environment', async () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir_mfg_acceptance';
    delete process.env['TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN'];

    await assert.rejects(
      async () => assertApiDatabaseContext('http://127.0.0.1:5089'),
      /Acceptance diagnostic token not provided in environment/
    );
    restoreEnv();
  });

  it('rejects API check if API database does not match target acceptance database', async () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir_mfg_acceptance';
    process.env['TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN'] = 'test-token';

    // Mock fetch to simulate API returning wrong database
    const originalFetch = globalThis.fetch;
    globalThis.fetch = async () => ({
      ok: true,
      json: async () => ({
        database: 'todir',
        currentUser: 'erp_app',
        host: 'localhost',
        port: 5432,
        environment: 'Development',
        isDisposable: false
      })
    } as any);

    try {
      await assert.rejects(
        async () => assertApiDatabaseContext('http://127.0.0.1:5089'),
        /Running API is connected to database "todir", expected disposable database "todir_mfg_acceptance"/
      );
    } finally {
      globalThis.fetch = originalFetch;
      restoreEnv();
    }
  });

  it('rejects API check if API is not in Development or Acceptance environment', async () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir_mfg_acceptance';
    process.env['TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN'] = 'test-token';

    const originalFetch = globalThis.fetch;
    globalThis.fetch = async () => ({
      ok: true,
      json: async () => ({
        database: 'todir_mfg_acceptance',
        currentUser: 'erp_app',
        host: 'localhost',
        port: 5432,
        environment: 'Production',
        isDisposable: true
      })
    } as any);

    try {
      await assert.rejects(
        async () => assertApiDatabaseContext('http://127.0.0.1:5089'),
        /Running API environment "Production" is not Development or Acceptance/
      );
    } finally {
      globalThis.fetch = originalFetch;
      restoreEnv();
    }
  });

  it('rejects API check if API reports isDisposable is false', async () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir_mfg_acceptance';
    process.env['TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN'] = 'test-token';

    const originalFetch = globalThis.fetch;
    globalThis.fetch = async () => ({
      ok: true,
      json: async () => ({
        database: 'todir_mfg_acceptance',
        environment: 'Development',
        isDisposable: false
      })
    } as any);

    try {
      await assert.rejects(
        async () => assertApiDatabaseContext('http://127.0.0.1:5089'),
        /Running API database "todir_mfg_acceptance" is not flagged as disposable/
      );
    } finally {
      globalThis.fetch = originalFetch;
      restoreEnv();
    }
  });

  it('rejects API check if diagnostic request returns 401 Unauthorized', async () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir_mfg_acceptance';
    process.env['TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN'] = 'test-token';

    const originalFetch = globalThis.fetch;
    globalThis.fetch = async () => ({
      ok: false,
      status: 401,
      text: async () => 'Unauthorized'
    } as any);

    try {
      await assert.rejects(
        async () => assertApiDatabaseContext('http://127.0.0.1:5089'),
        /Authoritative API database check failed with status 401/
      );
    } finally {
      globalThis.fetch = originalFetch;
      restoreEnv();
    }
  });

  it('rejects API check if diagnostic request returns 403 Forbidden', async () => {
    process.env['TODIR_ACCEPTANCE_DB'] = 'todir_mfg_acceptance';
    process.env['TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN'] = 'test-token';

    const originalFetch = globalThis.fetch;
    globalThis.fetch = async () => ({
      ok: false,
      status: 403,
      text: async () => 'Forbidden'
    } as any);

    try {
      await assert.rejects(
        async () => assertApiDatabaseContext('http://127.0.0.1:5089'),
        /Authoritative API database check failed with status 403/
      );
    } finally {
      globalThis.fetch = originalFetch;
      restoreEnv();
    }
  });
});
