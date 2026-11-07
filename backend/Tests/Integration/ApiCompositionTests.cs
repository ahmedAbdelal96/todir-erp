using Core.Messaging;
using Core.Transactions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class ApiCompositionTests : DatabaseIntegrationTestBase
{
    [Fact]
    public void ApiBuildsWithValidatedPostgresTransactionAndInboxServices()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION to the configured runtime database for the API composition gate.");

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("TODIR_DATABASE_CONNECTION", connectionString);
            builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty);
            builder.UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            });
        });

        var services = factory.Services;
        var dataSource = services.GetRequiredService<NpgsqlDataSource>();
        var expected = new NpgsqlConnectionStringBuilder(connectionString);
        var configured = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);
        Assert.Equal(expected.Host, configured.Host);
        Assert.Equal(expected.Port, configured.Port);
        Assert.Equal(expected.Database, configured.Database);
        Assert.Equal(expected.Username, configured.Username);
        Assert.IsType<NpgsqlTransactionCoordinator>(services.GetRequiredService<ITransactionCoordinator>());
        Assert.IsType<NpgsqlInboxStore>(services.GetRequiredService<IInboxStore>());
        Assert.NotNull(dataSource);
    }

    [Fact]
    public async Task DevelopmentOpenApiIncludesFinBaseRoutesAndFinancialAmountsAsStrings()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION to the configured runtime database for the OpenAPI contract gate.");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("TODIR_DATABASE_CONNECTION", connectionString);
            builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty);
        });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/finbase/currencies", out _));
        Assert.True(paths.TryGetProperty("/api/v1/finbase/legal-entities/{entityId}/currencies", out _));
        Assert.True(paths.TryGetProperty("/api/v1/finbase/exchange-rates", out _));
        Assert.True(paths.TryGetProperty("/api/v1/finbase/legal-entities/{entityId}/exchange-rates/{id}/approve", out _));
        Assert.True(paths.TryGetProperty("/api/v1/finbase/payment-terms", out _));
        Assert.True(paths.TryGetProperty("/api/v1/finbase/payment-schedule/preview", out _));
        Assert.True(paths.TryGetProperty("/api/v1/finbase/conversion/preview", out _));
        Assert.True(paths.TryGetProperty("/api/v1/finbase/rounding-policies", out _));
        Assert.True(paths.TryGetProperty("/api/v1/pricing/price-lists", out _));
        Assert.True(paths.TryGetProperty("/api/v1/pricing/resolve", out _));
        Assert.True(paths.TryGetProperty("/api/v1/pricing/resolve-batch", out _));
        Assert.True(paths.TryGetProperty("/api/v1/pricing/imports", out _));
        Assert.True(paths.TryGetProperty("/api/v1/pricing/coverage", out _));
        Assert.True(paths.TryGetProperty("/api/v1/tax/legal-entities/{entityId}/capability", out _));
        Assert.True(paths.TryGetProperty("/api/v1/tax/calculate", out _));
        Assert.True(paths.TryGetProperty("/api/v1/tax/calculate-batch", out _));
        Assert.True(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/managed-setup", out _));
        Assert.True(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/readiness", out _));
        Assert.True(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/mode", out _));
        Assert.True(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/accounts/search", out _));
        Assert.True(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/accounts/{accountId}/workspace", out _));
        Assert.True(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/periods/{periodId}/close-readiness", out _));
        Assert.True(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/posting-preview", out _));
        Assert.True(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/mapping-configurations/{configurationId}/simulate", out _));
        Assert.True(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/periods/{periodId}/reopen", out _));
        Assert.True(paths.TryGetProperty("/api/v1/accounting/legal-entities/{entityId}/health", out _));
        Assert.True(paths.TryGetProperty("/api/v1/accounting/legal-entities/{entityId}/owner-summary", out _));
        Assert.True(paths.TryGetProperty("/api/v1/accounting/legal-entities/{entityId}/reconciliations", out _));
        Assert.True(paths.TryGetProperty("/api/v1/accounting/legal-entities/{entityId}/reconciliations/{runId}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/accounting/legal-entities/{entityId}/reconciliation-issues", out _));
        Assert.True(paths.TryGetProperty("/api/v1/accounting/legal-entities/{entityId}/close-runs/{periodId}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/accounting/legal-entities/{entityId}/close-runs/{periodId}/issues", out _));
        Assert.True(paths.TryGetProperty("/api/v1/accounting/legal-entities/{entityId}/opening/approve-go-live", out _));
        Assert.True(paths.TryGetProperty("/api/v1/accounting/legal-entities/{entityId}/close/preview", out _));
        Assert.True(paths.TryGetProperty("/api/v1/accounting/legal-entities/{entityId}/close", out _));
        Assert.True(paths.TryGetProperty("/api/v1/subledger/open-items", out _));
        Assert.True(paths.TryGetProperty("/api/v1/subledger/parties/{partyId}/open-items", out _));
        Assert.True(paths.TryGetProperty("/api/v1/subledger/applications/suggest", out _));
        Assert.True(paths.TryGetProperty("/api/v1/subledger/applications/preview", out _));
        Assert.True(paths.TryGetProperty("/api/v1/subledger/applications", out _));
        Assert.True(paths.TryGetProperty("/api/v1/subledger/parties/{partyId}/financial-profile", out _));
        Assert.True(paths.TryGetProperty("/api/v1/subledger/parties/{partyId}/statement", out _));
        Assert.True(paths.TryGetProperty("/api/v1/subledger/reconciliation/control-accounts", out _));
        Assert.True(paths.TryGetProperty("/api/v1/subledger/reconciliation/run", out _));
        Assert.True(paths.TryGetProperty("/api/v1/subledger/opening-batches/{batchId}/finalize", out _));
        Assert.True(paths.TryGetProperty("/api/v1/invoicing/customer-invoices", out var customerInvoices));
        Assert.True(customerInvoices.TryGetProperty("get", out _));
        Assert.True(customerInvoices.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/v1/invoicing/customer-invoices/{id}/finalize", out _));
        Assert.True(paths.TryGetProperty("/api/v1/invoicing/supplier-invoices/{id}/finalize", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/incoming", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/outgoing", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/{paymentId}/complete", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/{paymentId}/applications", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/{paymentId}/refunds", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/{paymentId}/provider-refunds", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/{refundPaymentId}/provider-refund-status", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/{paymentId}/reverse", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/{paymentId}/fees", out var paymentFees));
        Assert.True(paymentFees.TryGetProperty("get", out _));
        var reverseSchema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ReversePaymentRequest");
        Assert.True(reverseSchema.GetProperty("properties").TryGetProperty("feeDispositions", out _));
        Assert.True(document.RootElement.GetProperty("components").GetProperty("schemas").TryGetProperty("PaymentFeeDispositionRequest", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/internal-transfers", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/providers", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/payment-requests", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/payment-requests/{requestId}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/payment-requests/{requestId}/attempts", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/payment-requests/{requestId}/status-sync", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/payment-requests/{requestId}/cancel", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/legal-entities/{entityId}/payment-requests/{requestId}/links", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/public-links/{token}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/payments/webhooks/reference/{tenantId}/{providerConfigId}", out _));
        var publicLinkSchema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("PublicPaymentLinkDetails").GetProperty("properties");
        Assert.False(publicLinkSchema.TryGetProperty("tenantId", out _));
        Assert.False(publicLinkSchema.TryGetProperty("requestId", out _));
        Assert.False(publicLinkSchema.TryGetProperty("merchantReference", out _));
        Assert.True(paths.TryGetProperty("/api/v1/invoicing/customer-invoices/{id}/render-model", out _));
        Assert.True(paths.TryGetProperty("/api/v1/invoicing/supplier-invoices/{id}/compliance-snapshot", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/warehouses", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/warehouses/{warehouseId}/status", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/negative-stock-conditions", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/locations/{locationId}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/reservations/{reservationId}/consume", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/reservations/{reservationId}/cancel", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/reservations/expire", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/adjustments", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/operations/reverse", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/products/{productVariantId}/putaway-suggestion", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/products/{productVariantId}/picking-suggestions", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/balances", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/history", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/receipts/confirm", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/issues/confirm", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/stock-status/change", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/reconciliation", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/scan/resolve", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/transfers/{transferId}/ship", out _));
        Assert.True(paths.TryGetProperty("/api/v1/inventory/legal-entities/{entityId}/counts/{countId}/post", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/leads/import", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/leads/merge", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/leads/export", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/opportunities/{id}/assign", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/opportunities/{id}/forecast-category", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/opportunities/{id}/interests", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/opportunities/{id}/contact-roles", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/opportunities/{id}/buying-committee", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/pipeline-inspection", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/reports/summary", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/forecast/snapshots", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/custom-fields", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/audit", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/external-events", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/teams/{teamId}/members", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/home", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/worklist", out _));
        Assert.True(paths.TryGetProperty("/api/v1/crm/legal-entities/{entityId}/attention", out _));
        Assert.False(paths.TryGetProperty("/api/v1/ledger/facts", out _));
        Assert.False(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/facts", out _));
        Assert.False(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/entries/{entryId}", out _));
        var posContractPaths = new[]
        {
            "/api/v1/pos/registers", "/api/v1/pos/registers/{registerId}", "/api/v1/pos/registers/{registerId}/activate", "/api/v1/pos/registers/{registerId}/deactivate",
            "/api/v1/pos/terminals/register", "/api/v1/pos/terminals/{terminalId}/rotate-credential", "/api/v1/pos/terminals/{terminalId}/revoke", "/api/v1/pos/terminals/{terminalId}/heartbeat",
            "/api/v1/pos/sessions/open", "/api/v1/pos/sessions/current", "/api/v1/pos/sessions/{sessionId}/start-close", "/api/v1/pos/sessions/{sessionId}/cash-count", "/api/v1/pos/sessions/{sessionId}/close",
            "/api/v1/pos/carts", "/api/v1/pos/carts/{cartId}", "/api/v1/pos/carts/{cartId}/scan", "/api/v1/pos/carts/{cartId}/lines", "/api/v1/pos/carts/{cartId}/lines/{lineId}", "/api/v1/pos/carts/{cartId}/customer", "/api/v1/pos/carts/{cartId}/discount", "/api/v1/pos/carts/{cartId}/hold", "/api/v1/pos/carts/{cartId}/resume", "/api/v1/pos/carts/{cartId}/cancel",
            "/api/v1/pos/catalog/search", "/api/v1/pos/catalog/barcode/{barcode}", "/api/v1/pos/carts/{cartId}/checkout", "/api/v1/pos/checkouts/{checkoutId}", "/api/v1/pos/checkouts/{checkoutId}/tenders", "/api/v1/pos/checkouts/{checkoutId}/payment-status/refresh", "/api/v1/pos/checkouts/{checkoutId}/complete", "/api/v1/pos/checkouts/{checkoutId}/recover",
            "/api/v1/pos/sales/{saleId}/returns", "/api/v1/pos/returns/{returnId}/lines", "/api/v1/pos/returns/{returnId}/complete", "/api/v1/pos/returns/{returnId}/refund",
            "/api/v1/pos/sessions/{sessionId}/cash-in", "/api/v1/pos/sessions/{sessionId}/cash-out", "/api/v1/pos/sessions/{sessionId}/cash-drop", "/api/v1/pos/sessions/{sessionId}/drawer-open",
            "/api/v1/pos/offline/package", "/api/v1/pos/offline/sync", "/api/v1/pos/offline/sync/{batchId}", "/api/v1/pos/offline/conflicts/{conflictId}/resolve",
            "/api/v1/pos/reports/session-summary", "/api/v1/pos/reports/store-summary", "/api/v1/pos/reports/sales", "/api/v1/pos/reports/returns", "/api/v1/pos/reports/payment-mix", "/api/v1/pos/reports/discounts", "/api/v1/pos/reports/cash-differences"
        };
        foreach (var path in posContractPaths) Assert.True(paths.TryGetProperty(path, out _), $"Missing POS contract route {path}");
        var numericFinancialFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "amount", "sourceAmount", "targetAmount", "rate", "cashIncrement", "percentage", "fixedAmount", "totalAmount", "feeAmount", "baseFeeAmount"
        };
        var checkedProperties = 0;
        foreach (var schema in document.RootElement.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("properties", out var properties)) continue;
            foreach (var property in properties.EnumerateObject())
            {
                if (!numericFinancialFields.Contains(property.Name)) continue;
                var type = property.Value.GetProperty("type");
                var isString = type.ValueKind == JsonValueKind.String
                    ? type.GetString() == "string"
                    : type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == "string");
                Assert.True(isString, $"Financial field {schema.Name}.{property.Name} must be serialized as a decimal string.");
                checkedProperties++;
            }
        }
        Assert.True(checkedProperties >= 8, $"Expected financial decimal contract fields in OpenAPI, found {checkedProperties}.");
    }

    [Fact]
    public async Task PosRegisterContractRequiresAuthenticationAndSupportsCanonicalAndScopedReads()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for the POS API contract gate.");
        const string legalEntityId = "aad8a34b-344c-9e5b-8d58-57f182fc6528";
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("TODIR_DATABASE_CONNECTION", connectionString);
            builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty);
        });

        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var anonymousResponse = await anonymous.GetAsync($"/api/v1/pos/registers?legalEntityId={legalEntityId}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var authenticated = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        using var csrfResponse = await authenticated.GetAsync("/api/v1/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();
        using var csrf = JsonDocument.Parse(await csrfResponse.Content.ReadAsStreamAsync());
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "admin@atlas.com", password = "TodirDev2026!" })
        };
        login.Headers.Add("X-CSRF-TOKEN", csrf.RootElement.GetProperty("token").GetString());
        var loginResponse = await authenticated.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var canonical = await authenticated.GetAsync($"/api/v1/pos/registers?legalEntityId={legalEntityId}");
        var scoped = await authenticated.GetAsync($"/api/v1/pos/legal-entities/{legalEntityId}/registers");
        Assert.Equal(HttpStatusCode.OK, canonical.StatusCode);
        Assert.Equal(HttpStatusCode.OK, scoped.StatusCode);
        Assert.NotEmpty(await canonical.Content.ReadAsStringAsync());
        Assert.NotEmpty(await scoped.Content.ReadAsStringAsync());
    }
}
