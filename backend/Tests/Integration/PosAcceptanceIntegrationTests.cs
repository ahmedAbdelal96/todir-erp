using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PosAcceptanceIntegrationTests : DatabaseIntegrationTestBase
{
    private static readonly Guid AtlasTenant = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");
    private static readonly Guid DeltaTenant = Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb");
    private static readonly Guid AtlasEntity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
    private static readonly Guid DeltaEntity = Guid.Parse("bbdcc188-1acc-805e-8721-f43308540a4d");

    [Fact]
    public async Task CashSaleRetryAndRecoveryPersistExactlyOnceAcrossModules()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        var adminConnectionString = Required("TODIR_P04_ADMIN_CONNECTION");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);

        var registers = await GetJsonAsync(client, $"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}");
        var register = registers.EnumerateArray().Single();
        var registerId = register.GetProperty("id").GetGuid();
        var branchId = register.GetProperty("branchId").GetGuid();
        var sessionResponse = await client.GetAsync($"/api/v1/pos/sessions/current?registerId={registerId:D}");
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var session = await ReadJsonAsync(sessionResponse);
        var sessionId = session.GetProperty("id").GetGuid();

        var parties = await GetJsonAsync(client, "/api/v1/directory/parties?partyType=Customer&role=Customer&page=1&pageSize=1");
        var customerPartyId = parties.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid();
        var priceLists = await GetJsonAsync(client, $"/api/v1/pricing/price-lists?legalEntityId={AtlasEntity:D}");
        var priceListId = priceLists.EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RETAIL").GetProperty("id").GetGuid();
        var accounts = await GetJsonAsync(client, $"/api/v1/payments/legal-entities/{AtlasEntity:D}/accounts");
        var paymentAccountId = accounts.EnumerateArray()
            .Single(x => x.GetProperty("type").GetString() == "Cashbox" && x.GetProperty("branchId").GetGuid() == branchId)
            .GetProperty("id").GetGuid();

        using var cartResponse = await PostJsonAsync(client, "/api/v1/pos/carts", new { sessionId, currencyCode = "EGP", customerPartyId });
        Assert.Equal(HttpStatusCode.Created, cartResponse.StatusCode);
        var cart = await ReadJsonAsync(cartResponse);
        var cartId = cart.GetProperty("id").GetGuid();
        using var scanResponse = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/scan", new
        {
            skuOrBarcode = "EG-2-0001",
            quantity = 1m,
            legalEntityId = AtlasEntity,
            priceListId
        });
        Assert.Equal(HttpStatusCode.Created, scanResponse.StatusCode);
        var scanned = await ReadJsonAsync(scanResponse);
        var total = scanned.GetProperty("grandTotal").GetDecimal();
        Assert.True(total > 0m);

        var idempotencyKey = $"pos-acceptance-cash-{Guid.CreateVersion7():N}";
        var checkoutRequest = new { idempotencyKey, tenders = new[] { new { method = "Cash", amount = total, paymentAccountId } } };
        using var firstCheckoutResponse = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/checkout", checkoutRequest);
        Assert.Equal(HttpStatusCode.Accepted, firstCheckoutResponse.StatusCode);
        var firstCheckout = await ReadJsonAsync(firstCheckoutResponse);
        Assert.Equal("Completed", firstCheckout.GetProperty("state").GetString());
        Assert.Equal("Completed", firstCheckout.GetProperty("inventoryState").GetString());
        Assert.Equal("Completed", firstCheckout.GetProperty("documentState").GetString());
        Assert.Equal("Completed", firstCheckout.GetProperty("accountingState").GetString());

        using var retryResponse = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/checkout", checkoutRequest);
        Assert.Equal(HttpStatusCode.Accepted, retryResponse.StatusCode);
        var retry = await ReadJsonAsync(retryResponse);
        var checkoutId = firstCheckout.GetProperty("checkoutId").GetGuid();
        var saleId = firstCheckout.GetProperty("saleId").GetGuid();
        Assert.Equal(checkoutId, retry.GetProperty("checkoutId").GetGuid());
        Assert.Equal(saleId, retry.GetProperty("saleId").GetGuid());

        using var recoveryResponse = await PostJsonAsync(client, $"/api/v1/pos/checkouts/{checkoutId:D}/recover", new { });
        Assert.Equal(HttpStatusCode.OK, recoveryResponse.StatusCode);
        var recovery = await ReadJsonAsync(recoveryResponse);
        Assert.Equal("Completed", recovery.GetProperty("state").GetString());

        using var concurrentClientA = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        using var concurrentClientB = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await Task.WhenAll(
            LoginAsync(concurrentClientA, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant),
            LoginAsync(concurrentClientB, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant));
        var concurrentRecovery = await Task.WhenAll(
            PostJsonAsync(concurrentClientA, $"/api/v1/pos/checkouts/{checkoutId:D}/recover", new { }),
            PostJsonAsync(concurrentClientB, $"/api/v1/pos/checkouts/{checkoutId:D}/recover", new { }));
        Assert.All(concurrentRecovery, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        foreach (var response in concurrentRecovery)
        {
            using (response)
            {
                Assert.Equal("Completed", (await ReadJsonAsync(response)).GetProperty("state").GetString());
            }
        }

        var salesReport = await GetJsonAsync(client, $"/api/v1/pos/reports/sales?legalEntityId={AtlasEntity:D}&page=1&pageSize=10");
        Assert.Contains(salesReport.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == saleId);
        var paymentMix = await GetJsonAsync(client, $"/api/v1/pos/reports/payment-mix?legalEntityId={AtlasEntity:D}");
        Assert.Contains(paymentMix.GetProperty("items").EnumerateArray(), item => item.GetProperty("method").GetString() == "Cash" && item.GetProperty("captured").GetDecimal() >= total);
        var sessionSummary = await GetJsonAsync(client, $"/api/v1/pos/reports/session-summary?legalEntityId={AtlasEntity:D}&sessionId={sessionId:D}");
        Assert.True(sessionSummary.GetProperty("saleCount").GetInt64() >= 1);
        Assert.True(sessionSummary.GetProperty("cashCaptured").GetDecimal() >= total);

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_checkout where tenant_id=$1 and idempotency_key=$2", AtlasTenant, idempotencyKey));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_sale where tenant_id=$1 and checkout_id=$2", AtlasTenant, checkoutId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2 and state='Captured'", AtlasTenant, checkoutId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_invoicing.invoice where tenant_id=$1 and source_module='pos' and source_idempotency_key=$2 and lifecycle_state='Finalized'", AtlasTenant, $"pos-invoice:{idempotencyKey}"));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_inventory.inventory_operation where tenant_id=$1 and source_module='sales' and source_document_id=$2 and operation_type='Issue' and status='Confirmed'", AtlasTenant, saleId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from finance_ledger.accounting_fact f join business_invoicing.invoice i on i.tenant_id=f.tenant_id and i.id::text=f.source_reference where f.tenant_id=$1 and f.source_module='invoicing' and i.source_module='pos' and i.source_idempotency_key=$2 and f.status='Posted'", AtlasTenant, $"pos-invoice:{idempotencyKey}"));

        var saleLineId = await GuidScalarAsync(admin, "select id from business_pos.pos_sale_line where tenant_id=$1 and sale_id=$2 order by line_no limit 1", AtlasTenant, saleId);
        using var returnResponse = await PostJsonAsync(client, $"/api/v1/pos/sales/{saleId:D}/returns", new
        {
            originalSaleId = saleId,
            sessionId,
            lines = new[] { new { originalSaleLineId = saleLineId, quantity = 1m, amount = total } },
            reason = "POS acceptance return",
            disposition = "Restock"
        });
        Assert.Equal(HttpStatusCode.Created, returnResponse.StatusCode);
        var returnId = (await ReadJsonAsync(returnResponse)).GetGuid();
        using var refundResponse = await PostJsonAsync(client, $"/api/v1/pos/returns/{returnId:D}/refund", new { paymentAccountId, amount = total, reasonCode = "CustomerReturn" });
        Assert.Equal(HttpStatusCode.Accepted, refundResponse.StatusCode);
        using var creditNoteResponse = await PostJsonAsync(client, $"/api/v1/pos/returns/{returnId:D}/credit-note", new { });
        Assert.Equal(HttpStatusCode.Accepted, creditNoteResponse.StatusCode);
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_return where tenant_id=$1 and id=$2 and state='Completed' and refund_state='Completed' and credit_note_state='Completed'", AtlasTenant, returnId));
    }

    [Fact]
    public async Task SessionCloseWorkflowPersistsCountAndClosedState()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        var adminConnectionString = Required("TODIR_P04_ADMIN_CONNECTION");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, "admin@delta.com", Required("SEED_DEFAULT_PASSWORD"), DeltaTenant);

        var registers = await GetJsonAsync(client, $"/api/v1/pos/registers?legalEntityId={DeltaEntity:D}");
        var register = registers.EnumerateArray().First();
        var registerId = register.GetProperty("id").GetGuid();
        var sessionResponse = await client.GetAsync($"/api/v1/pos/sessions/current?registerId={registerId:D}");
        if (sessionResponse.StatusCode != HttpStatusCode.OK)
        {
            sessionResponse.Dispose();
            await using var identityConnection = new NpgsqlConnection(adminConnectionString);
            await identityConnection.OpenAsync();
            var cashierUserId = await GuidScalarAsync(identityConnection, "select id from platform.identity where tenant_id=$1 and email=$2", DeltaTenant, "admin@delta.com");
            using var open = await PostJsonAsync(client, "/api/v1/pos/sessions/open", new
            {
                registerId,
                terminalId = (Guid?)null,
                cashierUserId,
                businessDate = DateOnly.FromDateTime(DateTime.UtcNow),
                openingFloat = 0m,
                currencyCode = "EGP"
            });
            Assert.Equal(HttpStatusCode.Created, open.StatusCode);
            sessionResponse = await client.GetAsync($"/api/v1/pos/sessions/current?registerId={registerId:D}");
        }
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var session = await ReadJsonAsync(sessionResponse);
        var sessionId = session.GetProperty("id").GetGuid();
        var openingFloat = session.GetProperty("openingFloat").GetDecimal();

        using var startClose = await PostJsonAsync(client, $"/api/v1/pos/sessions/{sessionId:D}/start-close", new { });
        Assert.Equal(HttpStatusCode.OK, startClose.StatusCode);
        using var cashCount = await PostJsonAsync(client, $"/api/v1/pos/sessions/{sessionId:D}/cash-count", new { sessionId, countedCash = openingFloat });
        Assert.Equal(HttpStatusCode.OK, cashCount.StatusCode);
        using var close = await PostJsonAsync(client, $"/api/v1/pos/sessions/{sessionId:D}/close", new { sessionId, countedCash = openingFloat });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Equal("Closed", (await ReadJsonAsync(close)).GetProperty("status").GetString());

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_session where tenant_id=$1 and id=$2 and status='Closed' and counted_cash=opening_float and variance=0", DeltaTenant, sessionId));
    }

    [Fact]
    public async Task CardProviderWebhookAndRecoveryPersistExactlyOnce()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        var adminConnectionString = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var providerConfigId = await GuidScalarAsync(admin, "select id from finance_payments.provider_config where tenant_id=$1 and legal_entity_id=$2 and provider_code='reference' and environment='Sandbox' order by id limit 1", AtlasTenant, AtlasEntity);
        await using (var configure = new NpgsqlCommand("update finance_payments.provider_config set secret_ref=$3,capabilities=jsonb_set(capabilities,'{webhooks}','true'::jsonb) where tenant_id=$1 and id=$2", admin))
        {
            configure.Parameters.AddWithValue(AtlasTenant);
            configure.Parameters.AddWithValue(providerConfigId);
            configure.Parameters.AddWithValue("pos-acceptance-secret");
            await configure.ExecuteNonQueryAsync();
        }
        var secretVariable = EnvironmentProviderSecretVariable("pos-acceptance-secret");
        Environment.SetEnvironmentVariable(secretVariable, "pos-acceptance-webhook-key");

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);

        var registers = await GetJsonAsync(client, $"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}");
        var registerId = registers.EnumerateArray().First().GetProperty("id").GetGuid();
        var currentSession = await GetJsonAsync(client, $"/api/v1/pos/sessions/current?registerId={registerId:D}");
        var sessionId = currentSession.GetProperty("id").GetGuid();
        var customer = (await GetJsonAsync(client, "/api/v1/directory/parties?partyType=Customer&role=Customer&page=1&pageSize=1")).GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var priceListId = (await GetJsonAsync(client, $"/api/v1/pricing/price-lists?legalEntityId={AtlasEntity:D}")).EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RETAIL").GetProperty("id").GetGuid();

        using var cartResponse = await PostJsonAsync(client, "/api/v1/pos/carts", new { sessionId, currencyCode = "EGP", customerPartyId = customer });
        var cart = await ReadJsonAsync(cartResponse);
        var cartId = cart.GetProperty("id").GetGuid();
        using var scanResponse = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/scan", new { skuOrBarcode = "EG-2-0001", quantity = 1m, legalEntityId = AtlasEntity, priceListId });
        var scanned = await ReadJsonAsync(scanResponse);
        var total = scanned.GetProperty("grandTotal").GetDecimal();
        var idempotencyKey = $"pos-acceptance-card-{Guid.CreateVersion7():N}";
        using var checkoutResponse = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/checkout", new { idempotencyKey, tenders = new[] { new { method = "Card", amount = total, providerConfigId } } });
        Assert.Equal(HttpStatusCode.Accepted, checkoutResponse.StatusCode);
        var checkout = await ReadJsonAsync(checkoutResponse);
        Assert.Equal("RequiresAction", checkout.GetProperty("paymentState").GetString());
        var checkoutId = checkout.GetProperty("checkoutId").GetGuid();
        var saleId = checkout.GetProperty("saleId").GetGuid();

        var providerRequestId = await GuidScalarAsync(admin, "select provider_reference::uuid from business_pos.pos_checkout where tenant_id=$1 and id=$2", AtlasTenant, checkoutId);
        var merchantReference = await StringScalarAsync(admin, "select merchant_reference from finance_payments.payment_request where tenant_id=$1 and id=$2", AtlasTenant, providerRequestId);
        var transactionId = await StringScalarAsync(admin, "select provider_transaction_id from finance_payments.payment_attempt where tenant_id=$1 and payment_request_id=$2 order by attempt_number desc limit 1", AtlasTenant, providerRequestId);
        var webhook = JsonContent.Create(new { eventId = Guid.CreateVersion7(), merchantReference, transactionId, eventType = "payment.succeeded", occurredAt = DateTimeOffset.UtcNow });
        var rawWebhook = await webhook.ReadAsByteArrayAsync();
        using var webhookRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/payments/webhooks/reference/{AtlasTenant:D}/{providerConfigId:D}") { Content = new ByteArrayContent(rawWebhook) };
        webhookRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        webhookRequest.Headers.Add("X-Provider-Signature", "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("pos-acceptance-webhook-key"), rawWebhook)).ToLowerInvariant());
        using var webhookResponse = await client.SendAsync(webhookRequest);
        Assert.Equal(HttpStatusCode.NoContent, webhookResponse.StatusCode);

        using var recovery = await PostJsonAsync(client, $"/api/v1/pos/checkouts/{checkoutId:D}/recover", new { });
        Assert.True(recovery.IsSuccessStatusCode, await recovery.Content.ReadAsStringAsync());
        Assert.Equal("Completed", (await ReadJsonAsync(recovery)).GetProperty("state").GetString());
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_sale where tenant_id=$1 and id=$2", AtlasTenant, saleId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2 and method='Card' and state='Captured'", AtlasTenant, checkoutId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and source_type='PaymentRequest' and source_reference=$2 and business_state='Completed'", AtlasTenant, providerRequestId.ToString("D")));
    }

    [Fact]
    public async Task SplitTenderAndExchangePersistSeparateLinkedFacts()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        var adminConnectionString = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var providerConfigId = await GuidScalarAsync(admin, "select id from finance_payments.provider_config where tenant_id=$1 and legal_entity_id=$2 and provider_code='reference' and environment='Sandbox' order by id limit 1", AtlasTenant, AtlasEntity);
        await using (var configure = new NpgsqlCommand("update finance_payments.provider_config set secret_ref=$3,capabilities=jsonb_set(capabilities,'{webhooks}','true'::jsonb) where tenant_id=$1 and id=$2", admin))
        {
            configure.Parameters.AddWithValue(AtlasTenant);
            configure.Parameters.AddWithValue(providerConfigId);
            configure.Parameters.AddWithValue("pos-split-secret");
            await configure.ExecuteNonQueryAsync();
        }
        Environment.SetEnvironmentVariable(EnvironmentProviderSecretVariable("pos-split-secret"), "pos-split-webhook-key");

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);

        var register = (await GetJsonAsync(client, $"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}")).EnumerateArray().First();
        var registerId = register.GetProperty("id").GetGuid();
        var sessionId = (await GetJsonAsync(client, $"/api/v1/pos/sessions/current?registerId={registerId:D}")).GetProperty("id").GetGuid();
        var customerPartyId = (await GetJsonAsync(client, "/api/v1/directory/parties?partyType=Customer&role=Customer&page=1&pageSize=1")).GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var priceListId = (await GetJsonAsync(client, $"/api/v1/pricing/price-lists?legalEntityId={AtlasEntity:D}")).EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RETAIL").GetProperty("id").GetGuid();
        var accounts = await GetJsonAsync(client, $"/api/v1/payments/legal-entities/{AtlasEntity:D}/accounts");
        var cashAccountId = accounts.EnumerateArray().Single(x => x.GetProperty("type").GetString() == "Cashbox" && x.GetProperty("branchId").GetGuid() == register.GetProperty("branchId").GetGuid()).GetProperty("id").GetGuid();

        using var cartResponse = await PostJsonAsync(client, "/api/v1/pos/carts", new { sessionId, currencyCode = "EGP", customerPartyId });
        var cartId = (await ReadJsonAsync(cartResponse)).GetProperty("id").GetGuid();
        using var scanResponse = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/scan", new { skuOrBarcode = "EG-2-0001", quantity = 1m, legalEntityId = AtlasEntity, priceListId });
        var scanned = await ReadJsonAsync(scanResponse);
        var total = scanned.GetProperty("grandTotal").GetDecimal();
        var cardAmount = Math.Round(total / 2m, 2, MidpointRounding.AwayFromZero);
        var cashAmount = total - cardAmount;
        var idempotencyKey = $"pos-acceptance-split-{Guid.CreateVersion7():N}";
        using var checkoutResponse = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/checkout", new
        {
            idempotencyKey,
            tenders = new[]
            {
                new Modules.POS.TenderRequest("Card", cardAmount, ProviderConfigId: providerConfigId),
                new Modules.POS.TenderRequest("Cash", cashAmount, PaymentAccountId: cashAccountId)
            }
        });
        Assert.Equal(HttpStatusCode.Accepted, checkoutResponse.StatusCode);
        var checkout = await ReadJsonAsync(checkoutResponse);
        var checkoutId = checkout.GetProperty("checkoutId").GetGuid();
        var saleId = checkout.GetProperty("saleId").GetGuid();
        Assert.Equal("RequiresAction", checkout.GetProperty("paymentState").GetString());

        var providerRequestId = await GuidScalarAsync(admin, "select provider_reference::uuid from business_pos.pos_checkout where tenant_id=$1 and id=$2", AtlasTenant, checkoutId);
        var merchantReference = await StringScalarAsync(admin, "select merchant_reference from finance_payments.payment_request where tenant_id=$1 and id=$2", AtlasTenant, providerRequestId);
        var transactionId = await StringScalarAsync(admin, "select provider_transaction_id from finance_payments.payment_attempt where tenant_id=$1 and payment_request_id=$2 order by attempt_number desc limit 1", AtlasTenant, providerRequestId);
        var webhook = JsonContent.Create(new { eventId = Guid.CreateVersion7(), merchantReference, transactionId, eventType = "payment.succeeded", occurredAt = DateTimeOffset.UtcNow });
        var rawWebhook = await webhook.ReadAsByteArrayAsync();
        using var webhookRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/payments/webhooks/reference/{AtlasTenant:D}/{providerConfigId:D}") { Content = new ByteArrayContent(rawWebhook) };
        webhookRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        webhookRequest.Headers.Add("X-Provider-Signature", "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("pos-split-webhook-key"), rawWebhook)).ToLowerInvariant());
        using var webhookResponse = await client.SendAsync(webhookRequest);
        Assert.Equal(HttpStatusCode.NoContent, webhookResponse.StatusCode);

        using var recovery = await PostJsonAsync(client, $"/api/v1/pos/checkouts/{checkoutId:D}/recover", new { });
        Assert.True(recovery.IsSuccessStatusCode, await recovery.Content.ReadAsStringAsync());
        Assert.Equal("Completed", (await ReadJsonAsync(recovery)).GetProperty("state").GetString());
        Assert.Equal(2L, await ScalarAsync(admin, "select count(*) from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2 and state='Captured'", AtlasTenant, checkoutId));
        Assert.Equal(total, await DecimalScalarAsync(admin, "select coalesce(sum(captured_amount),0) from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2", AtlasTenant, checkoutId));

        var saleLineId = await GuidScalarAsync(admin, "select id from business_pos.pos_sale_line where tenant_id=$1 and sale_id=$2 order by line_no limit 1", AtlasTenant, saleId);
        using var replacementCartResponse = await PostJsonAsync(client, "/api/v1/pos/carts", new { sessionId, currencyCode = "EGP", customerPartyId });
        var replacementCartId = (await ReadJsonAsync(replacementCartResponse)).GetProperty("id").GetGuid();
        using var replacementScan = await PostJsonAsync(client, $"/api/v1/pos/carts/{replacementCartId:D}/scan", new { skuOrBarcode = "EG-2-0001", quantity = 1m, legalEntityId = AtlasEntity, priceListId });
        var replacementTotal = (await ReadJsonAsync(replacementScan)).GetProperty("grandTotal").GetDecimal();
        using var exchangeResponse = await PostJsonAsync(client, "/api/v1/pos/exchanges", new
        {
            Return = new { originalSaleId = saleId, sessionId, lines = new[] { new { originalSaleLineId = saleLineId, quantity = 1m, amount = total } }, reason = "POS acceptance exchange", disposition = "Restock" },
            Checkout = new { cartId = replacementCartId, idempotencyKey = $"pos-acceptance-exchange-{Guid.CreateVersion7():N}", tenders = new[] { new Modules.POS.TenderRequest("Cash", replacementTotal, PaymentAccountId: cashAccountId) } }
        });
        Assert.Equal(HttpStatusCode.Accepted, exchangeResponse.StatusCode);
        var exchange = await ReadJsonAsync(exchangeResponse);
        Assert.Equal("Completed", exchange.GetProperty("state").GetString());
        var exchangeId = exchange.GetProperty("exchangeId").GetGuid();
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_exchange_group where tenant_id=$1 and id=$2 and state='Completed'", AtlasTenant, exchangeId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_return where tenant_id=$1 and original_sale_id=$2 and state='Completed'", AtlasTenant, saleId));
    }

    [Fact]
    public async Task CreditSaleCreatesReceivableCheckoutWithoutPaymentEffect()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        var adminConnectionString = Required("TODIR_P04_ADMIN_CONNECTION");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);
        var register = (await GetJsonAsync(client, $"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}")).EnumerateArray().First();
        var sessionId = (await GetJsonAsync(client, $"/api/v1/pos/sessions/current?registerId={register.GetProperty("id").GetGuid():D}")).GetProperty("id").GetGuid();
        var customerPartyId = (await GetJsonAsync(client, "/api/v1/directory/parties?partyType=Customer&role=Customer&page=1&pageSize=1")).GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var priceListId = (await GetJsonAsync(client, $"/api/v1/pricing/price-lists?legalEntityId={AtlasEntity:D}")).EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RETAIL").GetProperty("id").GetGuid();
        using var cartResponse = await PostJsonAsync(client, "/api/v1/pos/carts", new { sessionId, currencyCode = "EGP", customerPartyId });
        var cartId = (await ReadJsonAsync(cartResponse)).GetProperty("id").GetGuid();
        using var scanResponse = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/scan", new { skuOrBarcode = "EG-2-0001", quantity = 1m, legalEntityId = AtlasEntity, priceListId });
        var total = (await ReadJsonAsync(scanResponse)).GetProperty("grandTotal").GetDecimal();
        var idempotencyKey = $"pos-acceptance-credit-{Guid.CreateVersion7():N}";
        using var checkout = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/checkout", new { idempotencyKey, tenders = new[] { new Modules.POS.TenderRequest("Credit", total) } });
        Assert.Equal(HttpStatusCode.Accepted, checkout.StatusCode);
        var body = await ReadJsonAsync(checkout);
        Assert.Equal("Completed", body.GetProperty("state").GetString());
        Assert.Equal("NotRequired", body.GetProperty("paymentState").GetString());
        var checkoutId = body.GetProperty("checkoutId").GetGuid();
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2 and method='Credit' and state='Accepted' and captured_amount=0", AtlasTenant, checkoutId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_invoicing.invoice where tenant_id=$1 and source_module='pos' and source_idempotency_key=$2 and lifecycle_state='Finalized'", AtlasTenant, $"pos-invoice:{idempotencyKey}"));
        Assert.Equal(0L, await ScalarAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and source_type='POS' and source_reference=$2", AtlasTenant, checkoutId.ToString("D")));
    }

    [Fact]
    public async Task OfflinePackageEnvelopeReplayAndCashProcessingAreIdempotent()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        var adminConnectionString = Required("TODIR_P04_ADMIN_CONNECTION");
        Environment.SetEnvironmentVariable("TODIR_POS_OFFLINE_SIGNING_SECRET", "pos-offline-acceptance-secret");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);

        var registers = await GetJsonAsync(client, $"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}");
        var registerId = registers.EnumerateArray().First().GetProperty("id").GetGuid();
        var deviceIdentity = "seed-terminal-atlas-trading";
        var session = await GetJsonAsync(client, $"/api/v1/pos/sessions/current?registerId={registerId:D}");
        var sessionId = session.GetProperty("id").GetGuid();
        var customerPartyId = (await GetJsonAsync(client, "/api/v1/directory/parties?partyType=Customer&role=Customer&page=1&pageSize=1")).GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var variant = await GetJsonAsync(client, $"/api/v1/pos/catalog/barcode/EG-2-0001?legalEntityId={AtlasEntity:D}");
        var priceListId = (await GetJsonAsync(client, $"/api/v1/pricing/price-lists?legalEntityId={AtlasEntity:D}")).EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RETAIL").GetProperty("id").GetGuid();
        var accounts = await GetJsonAsync(client, $"/api/v1/payments/legal-entities/{AtlasEntity:D}/accounts");
        var paymentAccountId = accounts.EnumerateArray().Single(x => x.GetProperty("type").GetString() == "Cashbox").GetProperty("id").GetGuid();
        var sourceSequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var package = await GetJsonAsync(client, $"/api/v1/pos/offline/package?registerId={registerId:D}&deviceIdentity={deviceIdentity}");
        Assert.True(package.GetProperty("version").GetInt64() > 0);
        var packageVersion = package.GetProperty("version").GetInt64();
        var packageSignature = package.GetProperty("signature").GetString()!;
        var idempotencyKey = $"pos-acceptance-offline-{Guid.CreateVersion7():N}";
        var payload = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            type = "CashSale",
            sessionId,
            currencyCode = "EGP",
            paymentAccountId,
            idempotencyKey,
            customerPartyId,
            lines = new[]
            {
                new
                {
                    variantId = variant.GetProperty("productVariantId").GetGuid(),
                    description = variant.GetProperty("displayName").GetString(),
                    uomCode = variant.GetProperty("baseUomCode").GetString(),
                    quantity = 1m,
                    unitPrice = 10m,
                    taxAmount = 0m,
                    discountAmount = 0m,
                    legalEntityId = AtlasEntity,
                    productTemplateId = variant.GetProperty("productTemplateId").GetGuid(),
                    unitOfMeasureId = variant.GetProperty("baseUomId").GetGuid(),
                    priceListId,
                    pricingDate = DateOnly.FromDateTime(DateTime.UtcNow)
                }
            }
        });
        var signature = Modules.POS.PosOfflineSecurity.SignEnvelope(packageSignature, registerId, deviceIdentity, sourceSequence, idempotencyKey, packageVersion, payload);
        var envelopeRequest = new
        {
            registerId,
            deviceIdentity,
            sourceSequence,
            idempotencyKey,
            packageVersion,
            payload,
            signature
        };
        using var sync = await PostJsonAsync(client, "/api/v1/pos/offline/sync", envelopeRequest);
        Assert.Equal(HttpStatusCode.Accepted, sync.StatusCode);
        var envelopeId = (await ReadJsonAsync(sync)).GetGuid();
        using var process = await PostJsonAsync(client, $"/api/v1/pos/offline/envelopes/{envelopeId:D}/process-cash-sale", new { });
        var processBody = await process.Content.ReadAsStringAsync();
        Assert.True(process.IsSuccessStatusCode, processBody);
        var processed = await ReadJsonAsync(process);
        Assert.Equal("Accepted", processed.GetProperty("state").GetString());
        using var replay = await PostJsonAsync(client, "/api/v1/pos/offline/sync", envelopeRequest);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal(envelopeId, (await ReadJsonAsync(replay)).GetGuid());
        using var replayProcess = await PostJsonAsync(client, $"/api/v1/pos/offline/envelopes/{envelopeId:D}/process-cash-sale", new { });
        Assert.Equal(HttpStatusCode.Accepted, replayProcess.StatusCode);

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_offline_envelope where tenant_id=$1 and id=$2 and state='Accepted'", AtlasTenant, envelopeId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_checkout where tenant_id=$1 and idempotency_key=$2", AtlasTenant, idempotencyKey));
    }

    [Fact]
    public async Task FailedCardTenderCanBeReplacedByCashWithoutDuplicateFinancialEffects()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        var adminConnectionString = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var providerConfigId = await GuidScalarAsync(admin, "select id from finance_payments.provider_config where tenant_id=$1 and legal_entity_id=$2 and provider_code='reference' and environment='Sandbox' order by id limit 1", AtlasTenant, AtlasEntity);
        await using (var configure = new NpgsqlCommand("update finance_payments.provider_config set secret_ref=$3,capabilities=jsonb_set(capabilities,'{webhooks}','true'::jsonb) where tenant_id=$1 and id=$2", admin))
        {
            configure.Parameters.AddWithValue(AtlasTenant);
            configure.Parameters.AddWithValue(providerConfigId);
            configure.Parameters.AddWithValue("pos-failed-replacement-secret");
            await configure.ExecuteNonQueryAsync();
        }
        Environment.SetEnvironmentVariable(EnvironmentProviderSecretVariable("pos-failed-replacement-secret"), "pos-failed-replacement-key");

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);
        var register = (await GetJsonAsync(client, $"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}")).EnumerateArray().First();
        var registerId = register.GetProperty("id").GetGuid();
        var sessionId = (await GetJsonAsync(client, $"/api/v1/pos/sessions/current?registerId={registerId:D}")).GetProperty("id").GetGuid();
        var customerPartyId = (await GetJsonAsync(client, "/api/v1/directory/parties?partyType=Customer&role=Customer&page=1&pageSize=1")).GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var priceListId = (await GetJsonAsync(client, $"/api/v1/pricing/price-lists?legalEntityId={AtlasEntity:D}")).EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RETAIL").GetProperty("id").GetGuid();
        var accounts = await GetJsonAsync(client, $"/api/v1/payments/legal-entities/{AtlasEntity:D}/accounts");
        var cashAccountId = accounts.EnumerateArray().Single(x => x.GetProperty("type").GetString() == "Cashbox" && x.GetProperty("branchId").GetGuid() == register.GetProperty("branchId").GetGuid()).GetProperty("id").GetGuid();
        using var cartResponse = await PostJsonAsync(client, "/api/v1/pos/carts", new { sessionId, currencyCode = "EGP", customerPartyId });
        var cartId = (await ReadJsonAsync(cartResponse)).GetProperty("id").GetGuid();
        using var scanResponse = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/scan", new { skuOrBarcode = "EG-2-0001", quantity = 1m, legalEntityId = AtlasEntity, priceListId });
        var total = (await ReadJsonAsync(scanResponse)).GetProperty("grandTotal").GetDecimal();
        var idempotencyKey = $"pos-acceptance-failed-replacement-{Guid.CreateVersion7():N}";
        using var checkoutResponse = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/checkout", new { idempotencyKey, tenders = new[] { new { method = "Card", amount = total, providerConfigId } } });
        Assert.Equal(HttpStatusCode.Accepted, checkoutResponse.StatusCode);
        var checkout = await ReadJsonAsync(checkoutResponse);
        var checkoutId = checkout.GetProperty("checkoutId").GetGuid();
        var saleId = checkout.GetProperty("saleId").GetGuid();
        var providerRequestId = await GuidScalarAsync(admin, "select provider_reference::uuid from business_pos.pos_checkout where tenant_id=$1 and id=$2", AtlasTenant, checkoutId);
        var merchantReference = await StringScalarAsync(admin, "select merchant_reference from finance_payments.payment_request where tenant_id=$1 and id=$2", AtlasTenant, providerRequestId);
        var transactionId = await StringScalarAsync(admin, "select provider_transaction_id from finance_payments.payment_attempt where tenant_id=$1 and payment_request_id=$2 order by attempt_number desc limit 1", AtlasTenant, providerRequestId);
        var webhook = JsonContent.Create(new { eventId = Guid.CreateVersion7(), merchantReference, transactionId, eventType = "payment.failed", occurredAt = DateTimeOffset.UtcNow });
        var rawWebhook = await webhook.ReadAsByteArrayAsync();
        using var webhookRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/payments/webhooks/reference/{AtlasTenant:D}/{providerConfigId:D}") { Content = new ByteArrayContent(rawWebhook) };
        webhookRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        webhookRequest.Headers.Add("X-Provider-Signature", "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("pos-failed-replacement-key"), rawWebhook)).ToLowerInvariant());
        using var webhookResponse = await client.SendAsync(webhookRequest);
        Assert.Equal(HttpStatusCode.NoContent, webhookResponse.StatusCode);
        using var failedRecovery = await PostJsonAsync(client, $"/api/v1/pos/checkouts/{checkoutId:D}/recover", new { });
        Assert.True(failedRecovery.IsSuccessStatusCode, await failedRecovery.Content.ReadAsStringAsync());
        Assert.Equal("PaymentFailed", (await ReadJsonAsync(failedRecovery)).GetProperty("state").GetString());

        using var replacement = await PostJsonAsync(client, $"/api/v1/pos/checkouts/{checkoutId:D}/tenders", new { method = "Cash", amount = total, paymentAccountId = cashAccountId });
        Assert.True(replacement.StatusCode == HttpStatusCode.Accepted, await replacement.Content.ReadAsStringAsync());
        Assert.Equal("Pending", (await ReadJsonAsync(replacement)).GetProperty("paymentState").GetString());
        using var recovered = await PostJsonAsync(client, $"/api/v1/pos/checkouts/{checkoutId:D}/recover", new { });
        Assert.True(recovered.IsSuccessStatusCode, await recovered.Content.ReadAsStringAsync());
        Assert.Equal("Completed", (await ReadJsonAsync(recovered)).GetProperty("state").GetString());
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2 and method='Card' and state='Failed'", AtlasTenant, checkoutId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2 and method='Cash' and state='Captured'", AtlasTenant, checkoutId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_sale where tenant_id=$1 and id=$2 and commercial_state='Completed'", AtlasTenant, saleId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_invoicing.invoice where tenant_id=$1 and source_document_id=$2 and lifecycle_state='Finalized'", AtlasTenant, saleId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_inventory.inventory_operation where tenant_id=$1 and source_document_id=$2 and operation_type='Issue' and status='Confirmed'", AtlasTenant, saleId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and lower(source_type)='pos' and source_reference=$2 and business_state='Completed'", AtlasTenant, checkoutId.ToString("D")));
    }

    [Fact]
    public async Task SessionClosureAndCheckoutRaceCannotLeaveAnOpenFinancialOperation()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        var adminConnectionString = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);
        var register = (await GetJsonAsync(client, $"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}")).EnumerateArray().Single();
        var registerId = register.GetProperty("id").GetGuid();
        var sessionId = (await GetJsonAsync(client, $"/api/v1/pos/sessions/current?registerId={registerId:D}")).GetProperty("id").GetGuid();
        var customerPartyId = (await GetJsonAsync(client, "/api/v1/directory/parties?partyType=Customer&role=Customer&page=1&pageSize=1")).GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var priceListId = (await GetJsonAsync(client, $"/api/v1/pricing/price-lists?legalEntityId={AtlasEntity:D}")).EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RETAIL").GetProperty("id").GetGuid();
        using var cartResponse = await PostJsonAsync(client, "/api/v1/pos/carts", new { sessionId, currencyCode = "EGP", customerPartyId });
        var cartId = (await ReadJsonAsync(cartResponse)).GetProperty("id").GetGuid();
        using var scanResponse = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/scan", new { skuOrBarcode = "EG-2-0001", quantity = 1m, legalEntityId = AtlasEntity, priceListId });
        var total = (await ReadJsonAsync(scanResponse)).GetProperty("grandTotal").GetDecimal();
        var accounts = await GetJsonAsync(client, $"/api/v1/payments/legal-entities/{AtlasEntity:D}/accounts");
        var cashAccountId = accounts.EnumerateArray().Single(x => x.GetProperty("type").GetString() == "Cashbox" && x.GetProperty("branchId").GetGuid() == register.GetProperty("branchId").GetGuid()).GetProperty("id").GetGuid();
        var idempotencyKey = $"pos-acceptance-session-race-{Guid.CreateVersion7():N}";
        var closeTask = PostJsonAsync(client, $"/api/v1/pos/sessions/{sessionId:D}/start-close", new { });
        var checkoutTask = PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/checkout", new { idempotencyKey, tenders = new[] { new { method = "Cash", amount = total, paymentAccountId = cashAccountId } } });
        await Task.WhenAll(closeTask, checkoutTask);
        using var close = await closeTask;
        using var checkout = await checkoutTask;
        var sessionStatus = await StringScalarAsync(admin, "select status from business_pos.pos_session where tenant_id=$1 and id=$2", AtlasTenant, sessionId);
        var unfinished = await ScalarAsync(admin, "select count(*) from business_pos.pos_checkout where tenant_id=$1 and cart_id=$2 and state not in ('Completed','PaymentFailed')", AtlasTenant, cartId);
        Assert.Equal(0L, unfinished);
        Assert.True(close.IsSuccessStatusCode || checkout.IsSuccessStatusCode);
        Assert.True(sessionStatus is "Open" or "Reconciling" or "Closed");
        await using var restore = new NpgsqlCommand("update business_pos.pos_session set status='Open',closed_at=null,counted_cash=null,variance=null,variance_reason=null,approved_by=null where tenant_id=$1 and id=$2", admin);
        restore.Parameters.AddWithValue(AtlasTenant); restore.Parameters.AddWithValue(sessionId); await restore.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task AnonymousAndCrossTenantUsersCannotReadAtlasRegister()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}")).StatusCode);

        using var unauthorized = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(unauthorized, "office@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);
        var unauthorizedRegisters = await unauthorized.GetAsync($"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}");
        Assert.NotEqual(HttpStatusCode.OK, unauthorizedRegisters.StatusCode);

        using var delta = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(delta, "admin@delta.com", Required("SEED_DEFAULT_PASSWORD"), DeltaTenant);
        var atlasRegisters = await delta.GetAsync($"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}");
        Assert.NotEqual(HttpStatusCode.OK, atlasRegisters.StatusCode);
    }

    [Fact]
    public async Task SeededCashierAndManagerPermissionsAreEnforcedByRuntimeApi()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var cashier = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(cashier, "pos-cashier@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);
        using var cashierRegisterRead = await cashier.GetAsync($"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}");
        Assert.Equal(HttpStatusCode.OK, cashierRegisterRead.StatusCode);
        using var cashierReportRead = await cashier.GetAsync($"/api/v1/pos/reports/sales?legalEntityId={AtlasEntity:D}&page=1&pageSize=10");
        Assert.NotEqual(HttpStatusCode.OK, cashierReportRead.StatusCode);
        var registerId = (await ReadJsonAsync(cashierRegisterRead)).EnumerateArray().First().GetProperty("id").GetGuid();
        var sessionId = (await GetJsonAsync(cashier, $"/api/v1/pos/sessions/current?registerId={registerId:D}")).GetProperty("id").GetGuid();
        using var cashierClose = await PostJsonAsync(cashier, $"/api/v1/pos/sessions/{sessionId:D}/start-close", new { });
        Assert.NotEqual(HttpStatusCode.OK, cashierClose.StatusCode);

        using var manager = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(manager, "pos-manager@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);
        using var managerReportRead = await manager.GetAsync($"/api/v1/pos/reports/sales?legalEntityId={AtlasEntity:D}&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, managerReportRead.StatusCode);

        using var supervisor = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(supervisor, "pos-supervisor@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);
        using var supervisorRegisterManage = await supervisor.PostAsync($"/api/v1/pos/registers", JsonContent.Create(new { legalEntityId = AtlasEntity, branchId = Guid.Empty, code = "UNAUTHORIZED", name = "Unauthorized", currencyCode = "EGP" }));
        Assert.NotEqual(HttpStatusCode.Created, supervisorRegisterManage.StatusCode);

        var priceListId = (await GetJsonAsync(cashier, $"/api/v1/pricing/price-lists?legalEntityId={AtlasEntity:D}")).EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RETAIL").GetProperty("id").GetGuid();
        using var cartResponse = await PostJsonAsync(cashier, "/api/v1/pos/carts", new { sessionId, currencyCode = "EGP" });
        var cartId = (await ReadJsonAsync(cartResponse)).GetProperty("id").GetGuid();
        using var scanResponse = await PostJsonAsync(cashier, $"/api/v1/pos/carts/{cartId:D}/scan", new { skuOrBarcode = "EG-2-0001", quantity = 1m, legalEntityId = AtlasEntity, priceListId });
        Assert.Equal(HttpStatusCode.Created, scanResponse.StatusCode);
        var cartDetails = await GetJsonAsync(cashier, $"/api/v1/pos/carts/{cartId:D}");
        var lineId = cartDetails.GetProperty("lines").EnumerateArray().Single().GetProperty("id").GetGuid();
        var cartVersion = cartDetails.GetProperty("cart").GetProperty("version").GetInt32();
        using var deniedDiscount = await PostJsonAsync(cashier, $"/api/v1/pos/carts/{cartId:D}/discount", new { lineId, amount = 1m, expectedVersion = cartVersion });
        Assert.Equal(HttpStatusCode.Conflict, deniedDiscount.StatusCode);
        using var overrideResponse = await PostJsonAsync(manager, "/api/v1/pos/overrides", new { scope = "Discount", targetId = cartId, reason = "Acceptance approval", expiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });
        Assert.Equal(HttpStatusCode.Created, overrideResponse.StatusCode);
        using var approvedDiscount = await PostJsonAsync(cashier, $"/api/v1/pos/carts/{cartId:D}/discount", new { lineId, amount = 1m, expectedVersion = cartVersion });
        Assert.Equal(HttpStatusCode.OK, approvedDiscount.StatusCode);
        using var cancelApproved = await PostJsonAsync(cashier, $"/api/v1/pos/carts/{cartId:D}/cancel", new { });
        Assert.Equal(HttpStatusCode.OK, cancelApproved.StatusCode);

        using var expiringCartResponse = await PostJsonAsync(cashier, "/api/v1/pos/carts", new { sessionId, currencyCode = "EGP" });
        var expiringCartId = (await ReadJsonAsync(expiringCartResponse)).GetProperty("id").GetGuid();
        using var expiringScan = await PostJsonAsync(cashier, $"/api/v1/pos/carts/{expiringCartId:D}/scan", new { skuOrBarcode = "EG-2-0001", quantity = 1m, legalEntityId = AtlasEntity, priceListId });
        var expiringDetails = await GetJsonAsync(cashier, $"/api/v1/pos/carts/{expiringCartId:D}");
        var expiringLineId = expiringDetails.GetProperty("lines").EnumerateArray().Single().GetProperty("id").GetGuid();
        var expiringVersion = expiringDetails.GetProperty("cart").GetProperty("version").GetInt32();
        using var expiringOverride = await PostJsonAsync(manager, "/api/v1/pos/overrides", new { scope = "Discount", targetId = expiringCartId, reason = "Short-lived approval", expiresAt = DateTimeOffset.UtcNow.AddMilliseconds(250) });
        Assert.Equal(HttpStatusCode.Created, expiringOverride.StatusCode);
        await Task.Delay(600);
        using var expiredDiscount = await PostJsonAsync(cashier, $"/api/v1/pos/carts/{expiringCartId:D}/discount", new { lineId = expiringLineId, amount = 1m, expectedVersion = expiringVersion });
        Assert.Equal(HttpStatusCode.Conflict, expiredDiscount.StatusCode);
        using var cancelExpired = await PostJsonAsync(cashier, $"/api/v1/pos/carts/{expiringCartId:D}/cancel", new { });
        Assert.Equal(HttpStatusCode.OK, cancelExpired.StatusCode);
    }

    [Fact]
    public async Task RepresentativeCatalogAndReportReadsStayBoundedUnderRepeatedRuntimeLoad()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, "pos-manager@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);
        var catalogPath = $"/api/v1/pos/catalog/barcode/EG-2-0001?legalEntityId={AtlasEntity:D}";
        var reportPath = $"/api/v1/pos/reports/sales?legalEntityId={AtlasEntity:D}&page=1&pageSize=25";
        for (var warmup = 0; warmup < 3; warmup++)
        {
            _ = await GetJsonAsync(client, catalogPath);
            _ = await GetJsonAsync(client, reportPath);
        }
        var catalogSamples = new List<double>();
        var reportSamples = new List<double>();
        for (var sample = 0; sample < 20; sample++)
        {
            var catalogWatch = System.Diagnostics.Stopwatch.StartNew();
            _ = await GetJsonAsync(client, catalogPath);
            catalogWatch.Stop();
            catalogSamples.Add(catalogWatch.Elapsed.TotalMilliseconds);
            var reportWatch = System.Diagnostics.Stopwatch.StartNew();
            _ = await GetJsonAsync(client, reportPath);
            reportWatch.Stop();
            reportSamples.Add(reportWatch.Elapsed.TotalMilliseconds);
        }
        static double Percentile(List<double> values, double percentile)
        {
            values.Sort();
            var index = Math.Clamp((int)Math.Ceiling(values.Count * percentile) - 1, 0, values.Count - 1);
            return values[index];
        }
        var catalogP50 = Percentile(catalogSamples, 0.50);
        var catalogP95 = Percentile(catalogSamples, 0.95);
        var reportP50 = Percentile(reportSamples, 0.50);
        var reportP95 = Percentile(reportSamples, 0.95);
        Console.WriteLine($"POS_PERF catalog_requests=20 p50_ms={catalogP50:F1} p95_ms={catalogP95:F1}; report_requests=20 page_size=25 p50_ms={reportP50:F1} p95_ms={reportP95:F1}; concurrency=sequential_web_testserver; errors=0");
        Assert.True(catalogP95 < 2000d);
        Assert.True(reportP95 < 2000d);
    }

    [Fact]
    public async Task LotAndSerialSalesReturnsReconcileThroughInventoryAndPosApis()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        var adminConnectionString = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant);
        var register = (await GetJsonAsync(client, $"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}")).EnumerateArray().First();
        var registerId = register.GetProperty("id").GetGuid();
        var sessionId = (await GetJsonAsync(client, $"/api/v1/pos/sessions/current?registerId={registerId:D}")).GetProperty("id").GetGuid();
        var customerPartyId = (await GetJsonAsync(client, "/api/v1/directory/parties?partyType=Customer&role=Customer&page=1&pageSize=1")).GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var priceListId = (await GetJsonAsync(client, $"/api/v1/pricing/price-lists?legalEntityId={AtlasEntity:D}")).EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RETAIL").GetProperty("id").GetGuid();
        var accounts = await GetJsonAsync(client, $"/api/v1/payments/legal-entities/{AtlasEntity:D}/accounts");
        var cashAccountId = accounts.EnumerateArray().Single(x => x.GetProperty("type").GetString() == "Cashbox" && x.GetProperty("branchId").GetGuid() == register.GetProperty("branchId").GetGuid()).GetProperty("id").GetGuid();
        var warehouseId = await GuidScalarAsync(admin, "select warehouse_id from business_pos.pos_register where tenant_id=$1 and id=$2", AtlasTenant, registerId);
        var warehouses = await GetJsonAsync(client, $"/api/v1/inventory/legal-entities/{AtlasEntity:D}/warehouses");
        var warehouse = warehouses.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == warehouseId);
        var locationId = warehouse.GetProperty("defaultLocationId").GetGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var lotCatalog = await GetJsonAsync(client, "/api/v1/pos/catalog/barcode/EG-1-0002?legalEntityId=" + AtlasEntity.ToString("D"));
        var lotVariantId = lotCatalog.GetProperty("productVariantId").GetGuid();
        var lotUomId = lotCatalog.GetProperty("baseUomId").GetGuid();
        using var lotCreate = await PostJsonAsync(client, $"/api/v1/inventory/legal-entities/{AtlasEntity:D}/lots", new { productVariantId = lotVariantId, lotNumber = $"POS-LOT-{Guid.CreateVersion7():N}", manufacturedOn = today, expiresOn = today.AddYears(1), supplierReference = "POS acceptance" });
        Assert.True(lotCreate.StatusCode == HttpStatusCode.Created, await lotCreate.Content.ReadAsStringAsync());
        var lotId = (await ReadJsonAsync(lotCreate)).GetProperty("id").GetGuid();
        using var lotReceipt = await PostJsonAsync(client, $"/api/v1/inventory/legal-entities/{AtlasEntity:D}/receipts/confirm", new { productVariantId = lotVariantId, warehouseId, locationId, enteredUomId = lotUomId, enteredQuantity = 2m, operationalDate = today, postingDate = today, idempotencyKey = $"pos-lot-receipt-{Guid.CreateVersion7():N}", sourceModule = "pos-acceptance", status = "Available", lotId });
        Assert.True(lotReceipt.StatusCode == HttpStatusCode.Created, await lotReceipt.Content.ReadAsStringAsync());
        var lotCart = await CreateAndScanAsync(client, sessionId, customerPartyId, priceListId, "EG-1-0002", lotId, null);
        var lotCheckout = await CheckoutCashAsync(client, lotCart.CartId, lotCart.Total, cashAccountId);
        var lotSaleLineId = await GuidScalarAsync(admin, "select id from business_pos.pos_sale_line where tenant_id=$1 and sale_id=$2 limit 1", AtlasTenant, lotCheckout.SaleId);
        using var lotReturn = await PostJsonAsync(client, "/api/v1/pos/returns", new { originalSaleId = lotCheckout.SaleId, sessionId, lines = new[] { new { originalSaleLineId = lotSaleLineId, quantity = 1m, amount = lotCart.Total, lotId } }, reason = "Lot reconciliation", disposition = "Restock" });
        Assert.True(lotReturn.StatusCode == HttpStatusCode.Created, await lotReturn.Content.ReadAsStringAsync());
        var lotReturnId = (await ReadJsonAsync(lotReturn)).GetGuid();
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_return where tenant_id=$1 and id=$2 and state='Completed'", AtlasTenant, lotReturnId));
        var lotBalance = (await GetJsonAsync(client, $"/api/v1/inventory/legal-entities/{AtlasEntity:D}/balances?productVariantId={lotVariantId:D}&warehouseId={warehouseId:D}&limit=100")).EnumerateArray().Single(x => x.GetProperty("lotId").GetGuid() == lotId);
        Assert.Equal(2m, lotBalance.GetProperty("onHandBaseQuantity").GetDecimal());

        var serialCatalog = await GetJsonAsync(client, "/api/v1/pos/catalog/barcode/EG-2-0003?legalEntityId=" + AtlasEntity.ToString("D"));
        var serialVariantId = serialCatalog.GetProperty("productVariantId").GetGuid();
        var serialUomId = serialCatalog.GetProperty("baseUomId").GetGuid();
        using var serialCreate = await PostJsonAsync(client, $"/api/v1/inventory/legal-entities/{AtlasEntity:D}/serials", new { productVariantId = serialVariantId, serialNumber = $"POS-SERIAL-{Guid.CreateVersion7():N}" });
        Assert.True(serialCreate.StatusCode == HttpStatusCode.Created, await serialCreate.Content.ReadAsStringAsync());
        var serialId = (await ReadJsonAsync(serialCreate)).GetProperty("id").GetGuid();
        using var serialReceipt = await PostJsonAsync(client, $"/api/v1/inventory/legal-entities/{AtlasEntity:D}/receipts/confirm", new { productVariantId = serialVariantId, warehouseId, locationId, enteredUomId = serialUomId, enteredQuantity = 1m, operationalDate = today, postingDate = today, idempotencyKey = $"pos-serial-receipt-{Guid.CreateVersion7():N}", sourceModule = "pos-acceptance", status = "Available", serialId });
        Assert.True(serialReceipt.StatusCode == HttpStatusCode.Created, await serialReceipt.Content.ReadAsStringAsync());
        var serialCart = await CreateAndScanAsync(client, sessionId, customerPartyId, priceListId, "EG-2-0003", null, serialId);
        var serialCheckout = await CheckoutCashAsync(client, serialCart.CartId, serialCart.Total, cashAccountId);
        var serialSaleLineId = await GuidScalarAsync(admin, "select id from business_pos.pos_sale_line where tenant_id=$1 and sale_id=$2 limit 1", AtlasTenant, serialCheckout.SaleId);
        using var serialReturn = await PostJsonAsync(client, "/api/v1/pos/returns", new { originalSaleId = serialCheckout.SaleId, sessionId, lines = new[] { new { originalSaleLineId = serialSaleLineId, quantity = 1m, amount = serialCart.Total, serialId } }, reason = "Serial reconciliation", disposition = "Restock" });
        Assert.True(serialReturn.StatusCode == HttpStatusCode.Created, await serialReturn.Content.ReadAsStringAsync());
        var serialBalance = (await GetJsonAsync(client, $"/api/v1/inventory/legal-entities/{AtlasEntity:D}/balances?productVariantId={serialVariantId:D}&warehouseId={warehouseId:D}&limit=100")).EnumerateArray().Single(x => x.GetProperty("serialId").GetGuid() == serialId);
        Assert.Equal(1m, serialBalance.GetProperty("onHandBaseQuantity").GetDecimal());
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_inventory.inventory_operation where tenant_id=$1 and source_document_id=$2 and operation_type='Issue' and status='Confirmed'", AtlasTenant, serialCheckout.SaleId));
    }

    [Fact]
    public async Task ConcurrentCashSalesForOneSerialHaveOneEconomicWinner()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        var adminConnectionString = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var clientA = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        using var clientB = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await Task.WhenAll(
            LoginAsync(clientA, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant),
            LoginAsync(clientB, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant));
        var register = (await GetJsonAsync(clientA, $"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}")).EnumerateArray().First();
        var registerId = register.GetProperty("id").GetGuid();
        var branchId = register.GetProperty("branchId").GetGuid();
        var sessionId = (await GetJsonAsync(clientA, $"/api/v1/pos/sessions/current?registerId={registerId:D}")).GetProperty("id").GetGuid();
        var customerPartyId = (await GetJsonAsync(clientA, "/api/v1/directory/parties?partyType=Customer&role=Customer&page=1&pageSize=1")).GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var priceListId = (await GetJsonAsync(clientA, $"/api/v1/pricing/price-lists?legalEntityId={AtlasEntity:D}")).EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RETAIL").GetProperty("id").GetGuid();
        var accounts = await GetJsonAsync(clientA, $"/api/v1/payments/legal-entities/{AtlasEntity:D}/accounts");
        var cashAccountId = accounts.EnumerateArray().Single(x => x.GetProperty("type").GetString() == "Cashbox" && x.GetProperty("branchId").GetGuid() == branchId).GetProperty("id").GetGuid();
        var serialCatalog = await GetJsonAsync(clientA, $"/api/v1/pos/catalog/barcode/EG-2-0003?legalEntityId={AtlasEntity:D}");
        var serialVariantId = serialCatalog.GetProperty("productVariantId").GetGuid();
        var serialUomId = serialCatalog.GetProperty("baseUomId").GetGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using var serialCreate = await PostJsonAsync(clientA, $"/api/v1/inventory/legal-entities/{AtlasEntity:D}/serials", new { productVariantId = serialVariantId, serialNumber = $"POS-RACE-{Guid.CreateVersion7():N}" });
        Assert.Equal(HttpStatusCode.Created, serialCreate.StatusCode);
        var serialId = (await ReadJsonAsync(serialCreate)).GetProperty("id").GetGuid();
        var warehouseId = await GuidScalarAsync(admin, "select warehouse_id from business_pos.pos_register where tenant_id=$1 and id=$2", AtlasTenant, registerId);
        var warehouse = await GetJsonAsync(clientA, $"/api/v1/inventory/legal-entities/{AtlasEntity:D}/warehouses");
        var locationId = warehouse.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == warehouseId).GetProperty("defaultLocationId").GetGuid();
        using var receipt = await PostJsonAsync(clientA, $"/api/v1/inventory/legal-entities/{AtlasEntity:D}/receipts/confirm", new
        {
            productVariantId = serialVariantId,
            warehouseId,
            locationId,
            enteredUomId = serialUomId,
            enteredQuantity = 1m,
            operationalDate = today,
            postingDate = today,
            idempotencyKey = $"pos-race-receipt-{Guid.CreateVersion7():N}",
            sourceModule = "pos-acceptance",
            status = "Available",
            serialId
        });
        Assert.Equal(HttpStatusCode.Created, receipt.StatusCode);
        var cartA = await CreateAndScanAsync(clientA, sessionId, customerPartyId, priceListId, "EG-2-0003", null, serialId);
        var cartB = await CreateAndScanAsync(clientB, sessionId, customerPartyId, priceListId, "EG-2-0003", null, serialId);
        var keyA = $"pos-serial-race-a-{Guid.CreateVersion7():N}";
        var keyB = $"pos-serial-race-b-{Guid.CreateVersion7():N}";
        var responses = await Task.WhenAll(
            PostJsonAsync(clientA, $"/api/v1/pos/carts/{cartA.CartId:D}/checkout", new { idempotencyKey = keyA, tenders = new[] { new { method = "Cash", amount = cartA.Total, paymentAccountId = cashAccountId } } }),
            PostJsonAsync(clientB, $"/api/v1/pos/carts/{cartB.CartId:D}/checkout", new { idempotencyKey = keyB, tenders = new[] { new { method = "Cash", amount = cartB.Total, paymentAccountId = cashAccountId } } }));
        var bodies = new List<JsonElement>();
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            bodies.Add(await ReadJsonAsync(response));
            response.Dispose();
        }
        Assert.Equal(1, bodies.Count(body => string.Equals(body.GetProperty("state").GetString(), "Completed", StringComparison.OrdinalIgnoreCase)));
        var checkoutIds = bodies.Select(body => body.GetProperty("checkoutId").GetGuid()).ToArray();
        var saleIds = bodies.Select(body => body.GetProperty("saleId").GetGuid()).Where(id => id != Guid.Empty).ToArray();
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_tender where tenant_id=$1 and checkout_id in (select unnest($2::uuid[])) and state='Captured'", AtlasTenant, checkoutIds));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_inventory.inventory_operation where tenant_id=$1 and source_document_id in (select unnest($2::uuid[])) and operation_type='Issue' and status='Confirmed'", AtlasTenant, saleIds));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and lower(source_type)='pos' and source_reference in (select unnest($2::text[])) and business_state='Completed'", AtlasTenant, new[] { $"pos-checkout:{keyA}", $"pos-checkout:{keyB}" }));
    }

    [Fact]
    public async Task ConcurrentReturnsForOneSaleLineHaveOneAcceptedQuantity()
    {
        var connectionString = Required("TODIR_DATABASE_CONNECTION");
        var adminConnectionString = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var clientA = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        using var clientB = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await Task.WhenAll(
            LoginAsync(clientA, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant),
            LoginAsync(clientB, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), AtlasTenant));
        var register = (await GetJsonAsync(clientA, $"/api/v1/pos/registers?legalEntityId={AtlasEntity:D}")).EnumerateArray().First();
        var registerId = register.GetProperty("id").GetGuid();
        var branchId = register.GetProperty("branchId").GetGuid();
        var sessionId = (await GetJsonAsync(clientA, $"/api/v1/pos/sessions/current?registerId={registerId:D}")).GetProperty("id").GetGuid();
        var customerPartyId = (await GetJsonAsync(clientA, "/api/v1/directory/parties?partyType=Customer&role=Customer&page=1&pageSize=1")).GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var priceListId = (await GetJsonAsync(clientA, $"/api/v1/pricing/price-lists?legalEntityId={AtlasEntity:D}")).EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RETAIL").GetProperty("id").GetGuid();
        var accounts = await GetJsonAsync(clientA, $"/api/v1/payments/legal-entities/{AtlasEntity:D}/accounts");
        var cashAccountId = accounts.EnumerateArray().Single(x => x.GetProperty("type").GetString() == "Cashbox" && x.GetProperty("branchId").GetGuid() == branchId).GetProperty("id").GetGuid();
        var cart = await CreateAndScanAsync(clientA, sessionId, customerPartyId, priceListId, "EG-2-0001", null, null);
        var sale = await CheckoutCashAsync(clientA, cart.CartId, cart.Total, cashAccountId);
        var saleLineId = await GuidScalarAsync(admin, "select id from business_pos.pos_sale_line where tenant_id=$1 and sale_id=$2 limit 1", AtlasTenant, sale.SaleId);
        var request = new { originalSaleId = sale.SaleId, sessionId, lines = new[] { new { originalSaleLineId = saleLineId, quantity = 1m, amount = cart.Total } }, reason = "Concurrent return acceptance", disposition = "Restock" };
        var responses = await Task.WhenAll(PostJsonAsync(clientA, "/api/v1/pos/returns", request), PostJsonAsync(clientB, "/api/v1/pos/returns", request));
        var created = responses.Count(x => x.StatusCode == HttpStatusCode.Created);
        var conflicts = responses.Count(x => x.StatusCode == HttpStatusCode.Conflict);
        var returnIds = new List<Guid>();
        foreach (var response in responses)
        {
            if (response.StatusCode == HttpStatusCode.Created) returnIds.Add((await ReadJsonAsync(response)).GetGuid());
            response.Dispose();
        }
        Assert.Equal(1, created);
        Assert.Equal(1, conflicts);
        Assert.Single(returnIds);
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_pos.pos_return_line where tenant_id=$1 and original_sale_line_id=$2 and quantity=1", AtlasTenant, saleLineId));
        Assert.Equal(1L, await ScalarAsync(admin, "select count(*) from business_inventory.inventory_operation where tenant_id=$1 and source_document_id=$2 and operation_type='Receipt' and status='Confirmed'", AtlasTenant, saleLineId));
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"Set {name} for POS acceptance tests.");

    private static async Task<(Guid CartId, decimal Total)> CreateAndScanAsync(HttpClient client, Guid sessionId, Guid customerPartyId, Guid priceListId, string sku, Guid? lotId, Guid? serialId)
    {
        using var cart = await PostJsonAsync(client, "/api/v1/pos/carts", new { sessionId, currencyCode = "EGP", customerPartyId });
        var cartId = (await ReadJsonAsync(cart)).GetProperty("id").GetGuid();
        using var scan = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/scan", new { skuOrBarcode = sku, quantity = 1m, legalEntityId = AtlasEntity, priceListId, lotId, serialId });
        Assert.True(scan.StatusCode == HttpStatusCode.Created, await scan.Content.ReadAsStringAsync());
        return (cartId, (await ReadJsonAsync(scan)).GetProperty("grandTotal").GetDecimal());
    }

    private static async Task<(Guid CheckoutId, Guid SaleId)> CheckoutCashAsync(HttpClient client, Guid cartId, decimal total, Guid paymentAccountId)
    {
        using var checkout = await PostJsonAsync(client, $"/api/v1/pos/carts/{cartId:D}/checkout", new { idempotencyKey = $"pos-acceptance-traceability-{Guid.CreateVersion7():N}", tenders = new[] { new { method = "Cash", amount = total, paymentAccountId } } });
        Assert.True(checkout.StatusCode == HttpStatusCode.Accepted, await checkout.Content.ReadAsStringAsync());
        var body = await ReadJsonAsync(checkout);
        if (!string.Equals(body.GetProperty("state").GetString(), "Completed", StringComparison.OrdinalIgnoreCase))
        {
            using var recovery = await PostJsonAsync(client, $"/api/v1/pos/checkouts/{body.GetProperty("checkoutId").GetGuid():D}/recover", new { });
            Assert.True(recovery.IsSuccessStatusCode, await recovery.Content.ReadAsStringAsync());
            body = await ReadJsonAsync(recovery);
        }
        Assert.Equal("Completed", body.GetProperty("state").GetString());
        return (body.GetProperty("checkoutId").GetGuid(), body.GetProperty("saleId").GetGuid());
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var csrf = await GetJsonAsync(client, "/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(tenantId, body.GetProperty("tenantId").GetGuid());
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, object body)
    {
        var csrf = await GetJsonAsync(client, "/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql, Guid tenantId, object value)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<decimal> DecimalScalarAsync(NpgsqlConnection connection, string sql, Guid tenantId, object value)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(value);
        return Convert.ToDecimal(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<Guid> GuidScalarAsync(NpgsqlConnection connection, string sql, Guid tenantId, object value)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> StringScalarAsync(NpgsqlConnection connection, string sql, Guid tenantId, object value)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(value);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static string EnvironmentProviderSecretVariable(string secretReference)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(secretReference));
        return "TODIR_PROVIDER_SECRET_" + Convert.ToHexString(digest);
    }
}
