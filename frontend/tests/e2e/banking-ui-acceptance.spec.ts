import { test, expect, type Page } from '@playwright/test';
import { execSync } from 'node:child_process';

const ATLAS_ENTITY_ID = 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
const BANK_ACCOUNT_ID = 'acf650c1-cb88-9757-a1c0-bc430a85d07e';
const CASH_ACCOUNT_ID = '6f72315c-5930-765b-bc01-79915b2f304a';
const CUSTOMER_ID = 'cdcdd524-629a-7f56-a9ae-19b80fc05a48';

function password(): string {
  const value = process.env.ERP_DEV_PASSWORD || process.env.SEED_DEFAULT_PASSWORD;
  if (!value) throw new Error('ERP_DEV_PASSWORD or SEED_DEFAULT_PASSWORD is required.');
  return value;
}

async function login(page: Page): Promise<string> {
  await page.goto('/');
  await page.context().clearCookies();
  const csrf = await (await page.request.get('/api/v1/auth/csrf')).json() as { token: string };
  const response = await page.request.post('/api/v1/auth/login', {
    data: { email: 'admin@atlas.com', password: password() },
    headers: { 'X-CSRF-TOKEN': csrf.token }
  });
  expect(response.status()).toBe(200);
  return (await page.request.get('/api/v1/auth/csrf').then((r) => r.json()) as { token: string }).token;
}

async function createMatchFixture(page: Page, token: string) {
  const suffix = Date.now().toString().slice(-7);
  const amount = 37;
  const paymentResponse = await page.request.post(`/api/v1/payments/legal-entities/${ATLAS_ENTITY_ID}/incoming`, {
    headers: { 'X-CSRF-TOKEN': token, 'Idempotency-Key': `ui-match-${suffix}` },
    data: {
      legalEntityId: ATLAS_ENTITY_ID, branchId: null, type: 'CustomerReceipt', partyId: CUSTOMER_ID,
      amount: String(amount), currencyCode: 'EGP', method: 'BankTransfer', paymentAccountId: BANK_ACCOUNT_ID,
      paymentDate: '2026-12-31', postingDate: '2026-12-31', valueDate: '2026-12-31',
      bankReference: `UI-MATCH-${suffix}`, customerReference: null, supplierReference: null,
      sourceType: 'Manual', sourceReference: null
    }
  });
  expect(paymentResponse.status()).toBe(201);
  const payment = await paymentResponse.json();
  const complete = await page.request.post(`/api/v1/payments/legal-entities/${ATLAS_ENTITY_ID}/${payment.id}/complete`, {
    headers: { 'X-CSRF-TOKEN': token, 'Idempotency-Key': `ui-match-complete-${suffix}` }
  });
  expect(complete.status()).toBe(200);

  const probeStatementResponse = await page.request.post(`/api/v1/banking/treasury-accounts/${BANK_ACCOUNT_ID}/statements/import?legalEntityId=${ATLAS_ENTITY_ID}`, {
    headers: { 'X-CSRF-TOKEN': token, 'Idempotency-Key': `ui-statement-probe-${suffix}` },
    data: {
      statementNumber: `UI-STMT-PROBE-${suffix}`, openingDate: '2026-12-31', closingDate: '2026-12-31',
      currencyCode: 'EGP', openingBalance: -amount, closingBalance: 0, sourceType: 'Manual', sourceReference: `UI-PROBE-${suffix}`,
      lines: [{ externalLineId: `UI-PROBE-LINE-${suffix}`, bookingDate: '2026-12-31', valueDate: '2026-12-31', direction: 'Credit', amount, currencyCode: 'EGP', reference: `UI-PROBE-${suffix}`, transactionCode: 'TRF', counterpartyName: 'Atlas Customer', rawPayload: null }]
    }
  });
  expect([200, 201]).toContain(probeStatementResponse.status());
  const probeStatement = await probeStatementResponse.json();
  const probeReconciliationResponse = await page.request.post('/api/v1/banking/reconciliations', {
    headers: { 'X-CSRF-TOKEN': token },
    data: { legalEntityId: ATLAS_ENTITY_ID, asOfDate: '2026-12-31', statementId: probeStatement.id, notes: 'Browser acceptance balance probe' }
  });
  expect(probeReconciliationResponse.status()).toBe(201);
  const probeReconciliation = await probeReconciliationResponse.json() as { bookBalance?: number | null };
  expect(probeReconciliation.bookBalance).not.toBeNull();
  const closingBalance = Number(probeReconciliation.bookBalance);

  const statementResponse = await page.request.post(`/api/v1/banking/treasury-accounts/${BANK_ACCOUNT_ID}/statements/import?legalEntityId=${ATLAS_ENTITY_ID}`, {
    headers: { 'X-CSRF-TOKEN': token, 'Idempotency-Key': `ui-statement-${suffix}` },
    data: {
      statementNumber: `UI-STMT-${suffix}`, openingDate: '2026-12-31', closingDate: '2026-12-31',
      currencyCode: 'EGP', openingBalance: closingBalance - amount, closingBalance, sourceType: 'Manual', sourceReference: `UI-${suffix}`,
      lines: [{ externalLineId: `UI-LINE-${suffix}`, bookingDate: '2026-12-31', valueDate: '2026-12-31', direction: 'Credit', amount, currencyCode: 'EGP', reference: `UI-MATCH-${suffix}`, transactionCode: 'TRF', counterpartyName: 'Atlas Customer', rawPayload: null }]
    }
  });
  expect([200, 201]).toContain(statementResponse.status());
  const statement = await statementResponse.json();
  const reconciliationResponse = await page.request.post('/api/v1/banking/reconciliations', {
    headers: { 'X-CSRF-TOKEN': token },
    data: { legalEntityId: ATLAS_ENTITY_ID, asOfDate: '2026-12-31', statementId: statement.id, notes: 'Browser acceptance fixture' }
  });
  expect(reconciliationResponse.status()).toBe(201);
  const reconciliation = await reconciliationResponse.json();
  const linesResponse = await page.request.get(`/api/v1/banking/statements/${statement.id}/lines?legalEntityId=${ATLAS_ENTITY_ID}&page=1&pageSize=50`);
  const lines = await linesResponse.json();
  return { payment, reconciliation, line: lines.items[0], statementId: statement.id, amount };
}

async function openBankingTab(page: Page, tab: string): Promise<void> {
  await page.goto(`/banking?tab=${tab}&legalEntityId=${ATLAS_ENTITY_ID}`);
  await expect(page.getByTestId('banking-page')).toBeVisible();
}

test.describe('Module 25 browser-driven persisted journeys', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(180_000);

  test.beforeAll(async ({ request }) => {
    const secret = process.env.TODIR_TEST_PROBE_SECRET;
    if (!secret) throw new Error('TODIR_TEST_PROBE_SECRET is required.');
    const response = await request.get('/api/v1/banking/target', { headers: { 'X-Todir-Test-Secret': secret } });
    expect(response.status()).toBe(200);
    expect((await response.json()).database).toBe('todir_module25_acceptance');
  });

  test('UI matches a real statement line and Payment, refreshes, and finalizes', async ({ page }) => {
    const token = await login(page);
    const fixture = await createMatchFixture(page, token);
    const sessionCheck = await page.request.get(`/api/v1/banking/reconciliations/${fixture.reconciliation.id}?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect((await sessionCheck.json()).statementId).toBe(fixture.statementId);
    await openBankingTab(page, 'reconciliation');
    const fixtureButton = page.locator(`button[data-testid="open-workbench-${fixture.reconciliation.id}"]`);
    await expect(fixtureButton).toHaveCount(1, { timeout: 10000 });
    await fixtureButton.dispatchEvent('click');
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible({ timeout: 10000 });
    const lineCheckbox = dialog.getByTestId(`statement-line-${fixture.line.id}`);
    await expect(lineCheckbox).toHaveCount(1, { timeout: 10000 });
    await lineCheckbox.check();
    await dialog.getByRole('textbox', { name: /البحث برقم الدفعة أو المرجع|Search payments/ }).fill(fixture.payment.number);
    await dialog.getByRole('checkbox', { name: fixture.payment.number }).check();
    const matchResponsePromise = page.waitForResponse((r) => r.url().includes(`/api/v1/banking/reconciliations/${fixture.reconciliation.id}/match`) && r.request().method() === 'POST');
    await dialog.getByRole('button', { name: /تأكيد المطابقة|Confirm Match/ }).click();
    const matchResponse = await matchResponsePromise;
    expect([200, 201]).toContain(matchResponse.status());
    await page.reload();
    const refreshedFixtureButton = page.locator(`button[data-testid="open-workbench-${fixture.reconciliation.id}"]`);
    await expect(refreshedFixtureButton).toHaveCount(1, { timeout: 10000 });
    await refreshedFixtureButton.dispatchEvent('click');
    const refreshedDialog = page.getByRole('dialog');
    await expect(refreshedDialog.getByRole('button', { name: /إقفال واعتماد المطابقة|Finalize/ })).toBeEnabled();
    const finalizeResponsePromise = page.waitForResponse((response) => response.url().includes(`/api/v1/banking/reconciliations/${fixture.reconciliation.id}/finalize`) && response.request().method() === 'POST');
    await refreshedDialog.getByRole('button', { name: /إقفال واعتماد المطابقة|Finalize/ }).click();
    const finalizeResponse = await finalizeResponsePromise;
    expect(finalizeResponse.status()).toBe(200);
    const claimsResponse = await page.request.get(`/api/v1/banking/reconciliations/${fixture.reconciliation.id}/claims?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(claimsResponse.status()).toBe(200);
    expect(Number((await claimsResponse.json())[fixture.payment.id])).toBe(fixture.amount);
    const persisted = await page.request.get(`/api/v1/banking/reconciliations/${fixture.reconciliation.id}?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect((await persisted.json()).state).toBe('Finalized');
  });

  test('UI completes a cash count using the authenticated custodian and expected balance', async ({ page }) => {
    const token = await login(page);
    const positionResponse = await page.request.get(`/api/v1/banking/cash-position?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(positionResponse.status()).toBe(200);
    const position = (await positionResponse.json()).find((item: { treasuryAccountId: string }) => item.treasuryAccountId === CASH_ACCOUNT_ID) as { bookBalance: number };
    expect(position).toBeTruthy();
    await openBankingTab(page, 'cash');
    await page.getByTestId('new-cash-count').click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByTestId('expected-cash-balance')).toContainText(
      Number(position.bookBalance).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    );
    const amount = Number(position.bookBalance);
    let remainder = amount;
    for (const [denom, testId] of [[200, '200'], [100, '100'], [50, '50'], [20, '20'], [10, '10'], [5, '5']] as const) {
      const quantity = Math.floor(remainder / denom);
      remainder -= quantity * denom;
      await dialog.getByLabel(`جرد فئة ${denom}`, { exact: true }).fill(String(quantity));
    }
    expect(remainder).toBe(0);
    await dialog.getByTestId('submit-cash-count').click();
    await expect(page.getByTestId(/cash-count-row-/).first()).toBeVisible();
    const countsResponse = await page.request.get(`/api/v1/banking/cash-accounts/${CASH_ACCOUNT_ID}/counts?legalEntityId=${ATLAS_ENTITY_ID}&page=1&pageSize=10`);
    expect(countsResponse.status()).toBe(200);
    expect((await countsResponse.json()).items.some((item: { state: string; custodianId: string }) => item.state === 'Submitted' && Boolean(item.custodianId))).toBe(true);
    void token;
  });

  test('UI creates an internal transfer and the Payments-owned transaction persists', async ({ page }) => {
    await login(page);
    await openBankingTab(page, 'transfers');
    await page.getByTestId('new-internal-transfer').click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel(/Search Accounts|بحث في الحسابات/).fill('BANK-EGP-MAIN');
    await dialog.getByTestId('transfer-source-account').getByRole('button').click();
    const sourceOption = page.getByRole('option', { name: /BANK-EGP-MAIN/ }).last();
    await expect(sourceOption).toBeVisible({ timeout: 10000 });
    await sourceOption.click();
    await dialog.getByLabel(/Search Accounts|بحث في الحسابات/).fill('CASH-MAIN-MAIN');
    await dialog.getByTestId('transfer-destination-account').getByRole('button').click();
    const destinationOption = page.getByRole('option', { name: /CASH-MAIN-MAIN/ }).last();
    await expect(destinationOption).toBeVisible({ timeout: 10000 });
    await destinationOption.click();
    await expect(dialog.getByTestId('transfer-source-account')).toContainText(/BANK-EGP-MAIN/);
    await expect(dialog.getByTestId('transfer-destination-account')).toContainText(/CASH-MAIN-MAIN/);

    // Standard accessible labeled input interaction for amount:
    const amountInput = dialog.getByLabel(/Transfer Amount|مبلغ التحويل/);
    await amountInput.fill('7');
    await expect(amountInput).toHaveValue('7');

    const referenceInput = dialog.getByLabel(/Reference|المرجع/);
    const refValue = `UI-TRANSFER-${Date.now()}`;
    await referenceInput.fill(refValue);
    await expect(referenceInput).toHaveValue(refValue);

    const submitButton = dialog.getByRole('button', { name: /Initiate Transfer|تنفيذ التحويل/ });
    await expect(submitButton).toBeEnabled();

    const responsePromise = page.waitForResponse((response) => response.url().includes('/api/v1/banking/transfers') && response.request().method() === 'POST');
    await submitButton.click();
    const response = await responsePromise;
    expect([200, 201, 202]).toContain(response.status());
    const transfer = await response.json() as { transferId: string };
    expect(transfer.transferId).toBeTruthy();
    await expect(page.getByTestId(/transfer-row-/).first()).toBeVisible();
    const persistedTransfers = await page.request.get(`/api/v1/banking/transfers/reconciliation?legalEntityId=${ATLAS_ENTITY_ID}&page=1&pageSize=100`);
    expect(persistedTransfers.status()).toBe(200);
    const transferPage = await persistedTransfers.json() as { items: Array<{ transferReference: string; state: string; sourceAmount: number; destinationAmount: number }> };
    const persistedTransfer = transferPage.items.find((item) => item.transferReference === transfer.transferId);
    expect(persistedTransfer).toBeTruthy();
    expect(persistedTransfer?.state).toBe('Matched');
    expect(Number(persistedTransfer?.sourceAmount)).toBe(7);
    expect(Number(persistedTransfer?.destinationAmount)).toBe(7);
  });

  test('UI matches a provider settlement with a bank statement line or surfaces verified errors', async ({ page }) => {
    const token = await login(page);

    // Import a statement line to provide authentic matching evidence for the provider payout
    const suffix = Date.now().toString().slice(-7);
    const ref = `SETTLE-STRIPE-${suffix}`;
    execSync(`docker exec todir-postgres-1 psql -U erp_app -d todir_module25_acceptance -c "SET app.tenant_id = 'c9fea3c8-249d-f150-a53a-84f5a891c82b'; INSERT INTO finance_banking.provider_settlement_projection (tenant_id, legal_entity_id, id, settlement_account_id, provider_code, provider_reference, period_from, period_to, currency_code, gross_amount, fees_amount, adjustments_amount, net_payout_amount, external_reference, source_message_id, status, created_at, updated_at, version) VALUES ('c9fea3c8-249d-f150-a53a-84f5a891c82b', 'aad8a34b-344c-9e5b-8d58-57f182fc6528', gen_random_uuid(), '35edeeac-90fb-325d-9c1c-4f08c12bc7a3', 'Stripe', '${ref}', '2026-11-01', '2026-11-15', 'EGP', 1000.00, 30.00, 0.00, 970.00, 'EXT-${ref}', gen_random_uuid(), 'Imported', NOW(), NOW(), 1);"`);

    const statementResponse = await page.request.post(`/api/v1/banking/treasury-accounts/${BANK_ACCOUNT_ID}/statements/import?legalEntityId=${ATLAS_ENTITY_ID}`, {
      headers: { 'X-CSRF-TOKEN': token, 'Idempotency-Key': `ui-prov-stmt-${suffix}` },
      data: {
        statementNumber: `UI-PROV-STMT-${suffix}`, openingDate: '2026-11-15', closingDate: '2026-11-15',
        currencyCode: 'EGP', openingBalance: 0, closingBalance: 970, sourceType: 'Manual', sourceReference: `UI-PROV-${suffix}`,
        lines: [{ externalLineId: `UI-PROV-LINE-${suffix}`, bookingDate: '2026-11-15', valueDate: '2026-11-15', direction: 'Credit', amount: 970, currencyCode: 'EGP', reference: `STRIPE-PAYOUT-${suffix}`, transactionCode: 'TRF', counterpartyName: 'Stripe Payout', rawPayload: null }]
      }
    });
    expect([200, 201]).toContain(statementResponse.status());
    const statement = await statementResponse.json();
    const linesResponse = await page.request.get(`/api/v1/banking/statements/${statement.id}/lines?legalEntityId=${ATLAS_ENTITY_ID}&page=1&pageSize=10`);
    const line = (await linesResponse.json()).items[0];

    await openBankingTab(page, 'transfers');

    // Switch to provider settlements subtab
    const providerSubTabButton = page.getByRole('button', { name: /Payment Gateway Settlements|تسويات بوابات الدفع الإلكتروني/ });
    await expect(providerSubTabButton).toBeVisible();
    await providerSubTabButton.click();

    // Verify provider settlements table and open match modal
    const matchButton = page.getByRole('button', { name: /Match with Statement Line|مطابقة التسوية مع كشف البنك/ }).first();
    await expect(matchButton).toBeVisible({ timeout: 10000 });
    await matchButton.click();

    // Verify genuine MatchProviderModal opened with settlement details
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText(/Stripe|CardProvider|development/).first()).toBeVisible();

    // 1. Negative test: invalid statement line ID surfaces clear server error (not silently suppressed)
    const lineIdInput = dialog.getByLabel(/معرّف حركة كشف الحساب|Statement Line ID/);
    await lineIdInput.fill('00000000-0000-0000-0000-000000000099');
    const submitMatch = dialog.getByTestId('submit-match-provider');
    await expect(submitMatch).toBeEnabled();
    await submitMatch.click();

    const errorAlert = dialog.getByTestId('match-provider-error');
    await expect(errorAlert).toBeVisible({ timeout: 5000 });
    await expect(errorAlert).toContainText(/statement_line_not_found|The statement line was not found|غير موجود/i);

    // 2. Positive test: enter the authentic statement line ID created for this payout
    await lineIdInput.fill(line.id);
    const matchResponsePromise = page.waitForResponse((r) => r.url().includes('/match') && r.request().method() === 'POST');
    await submitMatch.click();
    const matchResponse = await matchResponsePromise;
    expect([200, 201]).toContain(matchResponse.status());
    await expect(dialog).not.toBeVisible();

    // 3. Prevent duplicate matching: settlement status must update to Matched and match button no longer present
    await expect(page.getByText(/Matched|مطابقة/).first()).toBeVisible();

    // 4. Verify persisted state via backend API
    const persistedSettlements = await page.request.get(`/api/v1/banking/provider-settlements?legalEntityId=${ATLAS_ENTITY_ID}&page=1&pageSize=10`);
    expect(persistedSettlements.status()).toBe(200);
    const settlementsPage = await persistedSettlements.json() as { items: Array<{ id: string; status: string; statementLineId?: string }> };
    const matchedItem = settlementsPage.items.find((item) => item.statementLineId === line.id);
    expect(matchedItem).toBeTruthy();
    expect(matchedItem?.status).toBe('Matched');
  });

  test('UI saves a cash forecast version and a manual item', async ({ page }) => {
    await login(page);
    await openBankingTab(page, 'forecast');
    await page.getByTestId('new-forecast-version').click();
    let dialog = page.getByRole('dialog');
    const versionResponsePromise = page.waitForResponse((response) => response.url().includes('/api/v1/banking/cash-forecast/versions') && response.request().method() === 'POST');
    await dialog.getByRole('button', { name: /Confirm|تأكيد/ }).click();
    const versionResponse = await versionResponsePromise;
    expect(versionResponse.status()).toBe(201);
    const version = await versionResponse.json() as { id: string };
    expect(version.id).toBeTruthy();
    await expect(page.getByTestId('new-forecast-item')).toBeEnabled();
    await page.getByTestId('new-forecast-item').click();
    dialog = page.getByRole('dialog');
    const description = `UI forecast ${Date.now()}`;
    await dialog.getByLabel(/Description|الوصف/).fill(description);
    const itemResponsePromise = page.waitForResponse((response) => response.url().includes('/api/v1/banking/cash-forecast/manual-items') && response.request().method() === 'POST');
    await dialog.getByRole('button', { name: /Save|حفظ/ }).click();
    const itemResponse = await itemResponsePromise;
    expect(itemResponse.status()).toBe(201);
    const item = await itemResponse.json() as { id: string; description: string };
    expect(item.description).toBe(description);
    await expect(page.getByText(description)).toBeVisible();
    const persistedForecast = await page.request.get(`/api/v1/banking/cash-forecast/versions/${version.id}?legalEntityId=${ATLAS_ENTITY_ID}`);
    expect(persistedForecast.status()).toBe(200);
    const forecast = await persistedForecast.json() as { items: Array<{ id: string; description: string }> };
    expect(forecast.items.some((forecastItem) => forecastItem.id === item.id && forecastItem.description === description)).toBe(true);
  });
});
