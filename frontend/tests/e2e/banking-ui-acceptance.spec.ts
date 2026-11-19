import { test, expect, type Page } from '@playwright/test';

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
    await dialog.getByRole('button', { name: /تأكيد المطابقة|Confirm Match/ }).click();
    await expect(dialog.getByRole('button', { name: /تأكيد المطابقة|Confirm Match/ })).toBeVisible();
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
    await dialog.getByRole('spinbutton').first().evaluate((element) => (element as HTMLInputElement).focus());
    await page.keyboard.type('7');
    await dialog.getByLabel(/Reference|المرجع/).evaluate((element) => (element as HTMLInputElement).focus());
    await page.keyboard.type(`UI-TRANSFER-${Date.now()}`);
    const submitButton = dialog.locator('button[type="submit"]');
    const responsePromise = page.waitForResponse((response) => response.url().includes('/api/v1/banking/transfers') && response.request().method() === 'POST');
    await submitButton.evaluate((element) => (element as HTMLButtonElement).focus());
    await page.keyboard.press('Enter');
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
