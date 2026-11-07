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
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"Set {name} for POS acceptance tests.");

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
