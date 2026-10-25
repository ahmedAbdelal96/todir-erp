using Core.Messaging;
using Core.Transactions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Text.Json;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class ApiCompositionTests
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
        Assert.True(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/periods/{periodId}/reopen", out _));
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
        Assert.False(paths.TryGetProperty("/api/v1/ledger/facts", out _));
        Assert.False(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/facts", out _));
        Assert.False(paths.TryGetProperty("/api/v1/ledger/legal-entities/{entityId}/entries/{entryId}", out _));
        var numericFinancialFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "amount", "sourceAmount", "targetAmount", "rate", "cashIncrement", "percentage", "fixedAmount", "totalAmount"
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
}
