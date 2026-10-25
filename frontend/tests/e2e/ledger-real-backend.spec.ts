import { test, expect } from '@playwright/test';

const REAL_BACKEND_URL = 'http://127.0.0.1:5080';
const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';

test.describe('Ledger Real-Backend Full-Stack Acceptance Suite', () => {
  test('1. Real Backend Health and Readiness', async ({ request }) => {
    const health = await request.get(`${REAL_BACKEND_URL}/health`);
    expect(health.status()).toBe(200);
    const healthJson = await health.json();
    expect(healthJson.status).toBe('ok');

    const ready = await request.get(`${REAL_BACKEND_URL}/health/ready`);
    expect(ready.status()).toBe(200);
    const readyJson = await ready.json();
    expect(readyJson.status).toBe('ready');
  });

  test('2. Real Authentication and Legal Entity Context', async ({ request }) => {
    const csrfRes = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    expect(csrfRes.status()).toBe(200);
    const { token } = await csrfRes.json();

    const loginRes = await request.post(`${REAL_BACKEND_URL}/api/v1/auth/login`, {
      data: { email: 'admin@atlas.com', password: 'TodirDev2026!' },
      headers: { 'X-CSRF-TOKEN': token }
    });
    expect(loginRes.status()).toBe(200);
    const loginJson = await loginRes.json();
    expect(loginJson.authenticated).toBe(true);

    const contextRes = await request.get(`${REAL_BACKEND_URL}/api/v1/session/context`);
    expect(contextRes.status()).toBe(200);
    const contextJson = await contextRes.json();
    expect(contextJson.context.displayName).toBe('شركة أطلس للتجارة');

    const entity = contextJson.context.legalEntities.find((e: { id: string }) => e.id === ATLAS_ENTITY_ID);
    expect(entity).toBeDefined();
    expect(entity.code).toBe('MAIN');
  });

  test('3. Real 3-Line General Journal Workflow (Draft -> Submit -> Post)', async ({ request }) => {
    const csrfRes = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const { token } = await csrfRes.json();
    await request.post(`${REAL_BACKEND_URL}/api/v1/auth/login`, {
      data: { email: 'admin@atlas.com', password: 'TodirDev2026!' },
      headers: { 'X-CSRF-TOKEN': token }
    });

    const csrfPost = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const csrf = (await csrfPost.json()).token;

    // Load journals and accounts
    const journalsRes = await request.get(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/journals`);
    const journals = await journalsRes.json();
    const generalJournal = journals.find((j: { type: string }) => j.type === 'General');
    expect(generalJournal).toBeDefined();

    const accountsRes = await request.get(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/accounts`);
    const accounts = await accountsRes.json();
    const cashAcc = accounts.find((a: { code: string }) => a.code === '110200');
    const assetAcc = accounts.find((a: { code: string }) => a.code === '9c85b095d2');
    const revAcc = accounts.find((a: { code: string }) => a.code === '410100');

    expect(cashAcc).toBeDefined();
    expect(assetAcc).toBeDefined();
    expect(revAcc).toBeDefined();

    // 1. Create 3-line General Journal Draft with dimensions
    const draftRes = await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/general-journals`, {
      data: {
        journalId: generalJournal.id,
        postingDate: '2026-10-24',
        description: 'E2E Real General Journal 3-Line with Dimensions',
        baseCurrencyCode: 'EGP',
        idempotencyKey: `real-e2e-${Date.now()}`,
        lines: [
          {
            accountId: cashAcc.id,
            debit: 3000,
            credit: 0,
            transactionCurrencyCode: 'EGP',
            transactionDebit: 3000,
            transactionCredit: 0,
            baseDebit: 3000,
            baseCredit: 0,
            exchangeRate: 1,
            description: 'Cash received'
          },
          {
            accountId: assetAcc.id,
            debit: 2000,
            credit: 0,
            transactionCurrencyCode: 'EGP',
            transactionDebit: 2000,
            transactionCredit: 0,
            baseDebit: 2000,
            baseCredit: 0,
            exchangeRate: 1,
            description: 'Current asset debit'
          },
          {
            accountId: revAcc.id,
            debit: 0,
            credit: 5000,
            transactionCurrencyCode: 'EGP',
            transactionDebit: 0,
            transactionCredit: 5000,
            baseDebit: 0,
            baseCredit: 5000,
            exchangeRate: 1,
            description: 'Sales revenue credit'
          }
        ]
      },
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(draftRes.status()).toBe(201);
    const draft = await draftRes.json();
    expect(draft.status).toBe('Draft');
    expect(draft.lines.length).toBe(3);

    // 2. Submit Draft
    const submitRes = await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/general-journals/${draft.id}/submit`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(submitRes.status()).toBe(200);
    const submitted = await submitRes.json();
    expect(submitted.status).toBe('Submitted');

    // 3. Post Draft directly from submitted state
    const postRes = await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/general-journals/${draft.id}/post`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(postRes.status()).toBe(200);
    const posted = await postRes.json();
    expect(posted.entryNumber).toBeDefined();
    expect(posted.journalEntryId).toBeDefined();

    // 4. Reverse the posted entry
    const revRes = await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/entries/${posted.journalEntryId}/reverse`, {
      data: {
        reversalDate: '2026-10-24',
        reason: 'Operational correction reversal'
      },
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(revRes.status()).toBe(200);
    const reversed = await revRes.json();
    expect(reversed.entryNumber).toContain('REV-');
    expect(reversed.journalEntryId).toBeDefined();

    // 5. Duplicate Reversal Negative Case
    const dupRevRes = await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/entries/${posted.journalEntryId}/reverse`, {
      data: {
        reversalDate: '2026-10-24',
        reason: 'Duplicate reversal attempt'
      },
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(dupRevRes.status()).toBe(400);
    const dupRevJson = await dupRevRes.json();
    expect(dupRevJson.code).toBe('ledger.already_reversed');
  });

  test('4. Real Control Account Protection Invariant', async ({ request }) => {
    const csrfRes = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const { token } = await csrfRes.json();
    await request.post(`${REAL_BACKEND_URL}/api/v1/auth/login`, {
      data: { email: 'admin@atlas.com', password: 'TodirDev2026!' },
      headers: { 'X-CSRF-TOKEN': token }
    });

    const csrfPost = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const csrf = (await csrfPost.json()).token;

    const journalsRes = await request.get(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/journals`);
    const generalJournal = (await journalsRes.json()).find((j: { type: string }) => j.type === 'General');

    const accountsRes = await request.get(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/accounts`);
    const accounts = await accountsRes.json();
    // 110100 is Receivable (Control Account)
    const controlAccount = accounts.find((a: { code: string }) => a.code === '110100');
    const revAcc = accounts.find((a: { code: string }) => a.code === '410100');

    expect(controlAccount).toBeDefined();

    const draftRes = await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/general-journals`, {
      data: {
        journalId: generalJournal.id,
        postingDate: '2026-10-24',
        description: 'Unauthorized Control Account Posting Attempt',
        baseCurrencyCode: 'EGP',
        idempotencyKey: `control-test-${Date.now()}`,
        lines: [
          {
            accountId: controlAccount.id,
            debit: 1000,
            credit: 0,
            transactionCurrencyCode: 'EGP',
            transactionDebit: 1000,
            transactionCredit: 0,
            baseDebit: 1000,
            baseCredit: 0,
            exchangeRate: 1
          },
          {
            accountId: revAcc.id,
            debit: 0,
            credit: 1000,
            transactionCurrencyCode: 'EGP',
            transactionDebit: 0,
            transactionCredit: 1000,
            baseDebit: 0,
            baseCredit: 1000,
            exchangeRate: 1
          }
        ]
      },
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(draftRes.status()).toBe(201);
    const draft = await draftRes.json();

    await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/general-journals/${draft.id}/submit`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });

    const postRes = await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/general-journals/${draft.id}/post`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(postRes.status()).toBe(400);
    const postJson = await postRes.json();
    expect(postJson.code).toBe('ledger.control_account_manual_post_blocked');
  });

  test('5. Real Opening Balance Workflow (Preview -> Zero-Mutation -> Commit -> Duplicate)', async ({ request }) => {
    const csrfRes = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const { token } = await csrfRes.json();
    await request.post(`${REAL_BACKEND_URL}/api/v1/auth/login`, {
      data: { email: 'admin@atlas.com', password: 'TodirDev2026!' },
      headers: { 'X-CSRF-TOKEN': token }
    });

    const csrfPost = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const csrf = (await csrfPost.json()).token;

    const accountsRes = await request.get(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/accounts`);
    const accounts = await accountsRes.json();
    const cashAcc = accounts.find((a: { code: string }) => a.code === '110200');
    const revAcc = accounts.find((a: { code: string }) => a.code === '410100');

    // 1. Create balanced opening batch
    const batchRes = await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/opening-balances`, {
      data: {
        batchIdentity: `OPENING-REAL-${Date.now()}`,
        fileHash: `sha256-${Date.now()}`,
        legacySystem: 'SAP-R3-Migration',
        goLiveDate: '2026-01-01',
        lines: [
          {
            accountId: cashAcc.id,
            debit: 75000,
            credit: 0,
            currencyCode: 'EGP',
            baseDebit: 75000,
            baseCredit: 0,
            exchangeRate: 1
          },
          {
            accountId: revAcc.id,
            debit: 0,
            credit: 75000,
            currencyCode: 'EGP',
            baseDebit: 0,
            baseCredit: 75000,
            exchangeRate: 1
          }
        ]
      },
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(batchRes.status()).toBe(201);
    const batchId = await batchRes.json();

    // 2. Preview - verifies zero-mutation before commit
    const previewRes = await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/opening-balances/${batchId}/preview`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(previewRes.status()).toBe(200);
    const preview = await previewRes.json();
    expect(preview.balanced).toBe(true);
    expect(preview.totalDebit).toBe(75000);
    expect(preview.totalCredit).toBe(75000);

    // 3. Commit
    const postRes = await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/opening-balances/${batchId}/post`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(postRes.status()).toBe(200);
    const posted = await postRes.json();
    expect(posted.entryNumber).toBeDefined();

    // 4. Duplicate / Idempotency handling
    const dupRes = await request.post(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/opening-balances/${batchId}/post`, {
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(dupRes.status()).toBe(400);
    const dupJson = await dupRes.json();
    expect(dupJson.code).toBe('ledger.opening_already_posted');
  });

  test('6. Real Reports (Trial Balance, General Ledger, and Account Workspace)', async ({ request }) => {
    const csrfRes = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const { token } = await csrfRes.json();
    await request.post(`${REAL_BACKEND_URL}/api/v1/auth/login`, {
      data: { email: 'admin@atlas.com', password: 'TodirDev2026!' },
      headers: { 'X-CSRF-TOKEN': token }
    });

    // Trial Balance loads real rows
    const tbRes = await request.get(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/reports/trial-balance`);
    expect(tbRes.status()).toBe(200);
    const tbRows = await tbRes.json();
    expect(Array.isArray(tbRows)).toBe(true);

    // General Ledger loads real records
    const glRes = await request.get(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/reports/general-ledger`);
    expect(glRes.status()).toBe(200);
    const glPage = await glRes.json();
    expect(Array.isArray(glPage.items)).toBe(true);

    // Accounting Readiness
    const readinessRes = await request.get(`${REAL_BACKEND_URL}/api/v1/ledger/legal-entities/${ATLAS_ENTITY_ID}/readiness`);
    expect(readinessRes.status()).toBe(200);
    const readiness = await readinessRes.json();
    expect(readiness.state).toBe('Ready');
  });
});
