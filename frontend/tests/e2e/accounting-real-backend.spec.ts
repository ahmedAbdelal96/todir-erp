import { test, expect } from '@playwright/test';

const REAL_BACKEND_URL = 'http://127.0.0.1:5080';
const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';

test.describe('Accounting Real-Backend Full-Stack Verification Suite', () => {
  test('1. Real Accounting Health and Readiness', async ({ request }) => {
    const csrfRes = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const { token } = await csrfRes.json();
    const loginRes = await request.post(`${REAL_BACKEND_URL}/api/v1/auth/login`, {
      data: { email: 'admin@atlas.com', password: 'TodirDev2026!' },
      headers: { 'X-CSRF-TOKEN': token }
    });
    expect(loginRes.status()).toBe(200);

    const healthRes = await request.get(`${REAL_BACKEND_URL}/api/v1/accounting/legal-entities/${ATLAS_ENTITY_ID}/health`);
    expect(healthRes.status()).toBe(200);
    const health = await healthRes.json();
    expect(health.legalEntityId).toBe(ATLAS_ENTITY_ID);
    expect(health.items.length).toBeGreaterThan(0);
    expect(health.items.some((i: { code: string }) => i.code === 'ledger.readiness')).toBe(true);
  });

  test('2. Real Owner Financial Summary', async ({ request }) => {
    const csrfRes = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const { token } = await csrfRes.json();
    await request.post(`${REAL_BACKEND_URL}/api/v1/auth/login`, {
      data: { email: 'admin@atlas.com', password: 'TodirDev2026!' },
      headers: { 'X-CSRF-TOKEN': token }
    });

    const ownerRes = await request.get(`${REAL_BACKEND_URL}/api/v1/accounting/legal-entities/${ATLAS_ENTITY_ID}/owner-summary`);
    expect(ownerRes.status()).toBe(200);
    const owner = await ownerRes.json();
    expect(owner.legalEntityId).toBe(ATLAS_ENTITY_ID);
    expect(owner.currencyCode).toBe('EGP');
    expect(typeof owner.cashBookValue).toBe('number');
    expect(typeof owner.receivables).toBe('number');
    expect(typeof owner.payables).toBe('number');
    expect(typeof owner.profit).toBe('number');
    expect(['Healthy', 'Attention', 'Blocking']).toContain(owner.health);
  });

  test('3. Real Financial Statements (P&L and Balance Sheet)', async ({ request }) => {
    const csrfRes = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const { token } = await csrfRes.json();
    await request.post(`${REAL_BACKEND_URL}/api/v1/auth/login`, {
      data: { email: 'admin@atlas.com', password: 'TodirDev2026!' },
      headers: { 'X-CSRF-TOKEN': token }
    });

    // Profit & Loss
    const pnlRes = await request.get(`${REAL_BACKEND_URL}/api/v1/accounting/legal-entities/${ATLAS_ENTITY_ID}/statements/ProfitAndLoss?from=2026-01-01&to=2026-12-31`);
    expect(pnlRes.status()).toBe(200);
    const pnl = await pnlRes.json();
    expect(pnl.kind).toBe('ProfitAndLoss');
    expect(pnl.lines.length).toBeGreaterThan(0);

    // Balance Sheet
    const bsRes = await request.get(`${REAL_BACKEND_URL}/api/v1/accounting/legal-entities/${ATLAS_ENTITY_ID}/statements/BalanceSheet?from=2026-01-01&to=2026-12-31`);
    expect(bsRes.status()).toBe(200);
    const bs = await bsRes.json();
    expect(bs.kind).toBe('BalanceSheet');
    expect(bs.isBalanced).toBe(true);
  });

  test('4. Real Subledger Control Reconciliations (AR / AP / Inventory)', async ({ request }) => {
    const csrfRes = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const { token } = await csrfRes.json();
    await request.post(`${REAL_BACKEND_URL}/api/v1/auth/login`, {
      data: { email: 'admin@atlas.com', password: 'TodirDev2026!' },
      headers: { 'X-CSRF-TOKEN': token }
    });

    const listRes = await request.get(`${REAL_BACKEND_URL}/api/v1/accounting/legal-entities/${ATLAS_ENTITY_ID}/reconciliations`);
    expect(listRes.status()).toBe(200);
    const list = await listRes.json();
    expect(Array.isArray(list)).toBe(true);
  });

  test('5. Real FX Revaluation Preview', async ({ request }) => {
    const csrfRes = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const { token } = await csrfRes.json();
    await request.post(`${REAL_BACKEND_URL}/api/v1/auth/login`, {
      data: { email: 'admin@atlas.com', password: 'TodirDev2026!' },
      headers: { 'X-CSRF-TOKEN': token }
    });

    const csrfPost = await request.get(`${REAL_BACKEND_URL}/api/v1/auth/csrf`);
    const csrf = (await csrfPost.json()).token;

    const fxRes = await request.post(`${REAL_BACKEND_URL}/api/v1/accounting/legal-entities/${ATLAS_ENTITY_ID}/fx-revaluations/preview`, {
      data: {
        asOfDate: '2026-11-01',
        idempotencyKey: `real-fx-preview-${Date.now()}`
      },
      headers: { 'X-CSRF-TOKEN': csrf }
    });
    expect(fxRes.status()).toBe(200);
    const fx = await fxRes.json();
    expect(fx.asOfDate).toBe('2026-11-01');
    expect(Array.isArray(fx.lines)).toBe(true);
  });
});
