using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class CatalogReadinessContractTests : DatabaseIntegrationTestBase {
    [Fact]
    public async Task ReadinessReturnsIdenticalNotFoundForAbsentAndCrossTenantProducts()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_CATALOG_ADMIN_CONNECTION for the Catalog readiness contract integration test.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for the Catalog readiness contract integration test.");
        var password = Environment.GetEnvironmentVariable("TODIR_CATALOG_TEST_PASSWORD")
            ?? throw new InvalidOperationException("Set TODIR_CATALOG_TEST_PASSWORD to the canonical Development Seed password for the Catalog readiness contract integration test.");
        var runtimePassword = new NpgsqlConnectionStringBuilder(runtimeConnectionString).Password
            ?? throw new InvalidOperationException("TODIR_DATABASE_CONNECTION must configure the runtime role password.");

        Guid atlasTenant;
        Guid deltaTenant;
        Guid atlasProductId;
        await using (var connection = new NpgsqlConnection(adminConnectionString))
        {
            await connection.OpenAsync();
            var escapedPassword = runtimePassword.Replace("'", "''", StringComparison.Ordinal);
            await using (var resetPassword = new NpgsqlCommand($"alter role erp_app password '{escapedPassword}'", connection))
                await resetPassword.ExecuteNonQueryAsync();
            await using (var tenants = new NpgsqlCommand("select id,normalized_login_domain from platform.tenant where normalized_login_domain in ('atlas.com','delta.com')", connection))
            await using (var reader = await tenants.ExecuteReaderAsync())
            {
                var found = new Dictionary<string, Guid>(StringComparer.Ordinal);
                while (await reader.ReadAsync()) found[reader.GetString(1)] = reader.GetGuid(0);
                atlasTenant = found.GetValueOrDefault("atlas.com");
                deltaTenant = found.GetValueOrDefault("delta.com");
            }
            Assert.NotEqual(Guid.Empty, atlasTenant);
            Assert.NotEqual(Guid.Empty, deltaTenant);
            await using var product = new NpgsqlCommand("select id from foundation_catalog.product_template where tenant_id=$1 and status='Active' order by id limit 1", connection);
            product.Parameters.AddWithValue(atlasTenant);
            atlasProductId = (Guid)(await product.ExecuteScalarAsync() ?? Guid.Empty);
            Assert.NotEqual(Guid.Empty, atlasProductId);
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var atlasClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        using var deltaClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(atlasClient, "admin@atlas.com", password, atlasTenant);
        await LoginAsync(deltaClient, "admin@delta.com", password, deltaTenant);

        var visibleDetail = await atlasClient.GetAsync($"/api/v1/catalog/products/{atlasProductId:D}");
        Assert.Equal(HttpStatusCode.OK, visibleDetail.StatusCode);
        Assert.Equal(atlasProductId, (await visibleDetail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        var visibleReadiness = await atlasClient.GetAsync($"/api/v1/catalog/products/{atlasProductId:D}/readiness");
        Assert.Equal(HttpStatusCode.OK, visibleReadiness.StatusCode);

        var missingProductId = Guid.CreateVersion7();
        var missingDetail = await atlasClient.GetAsync($"/api/v1/catalog/products/{missingProductId:D}");
        var missingDetailBody = await missingDetail.Content.ReadAsStringAsync();
        var missingReadiness = await atlasClient.GetAsync($"/api/v1/catalog/products/{missingProductId:D}/readiness");
        var missingReadinessBody = await missingReadiness.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, missingDetail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingReadiness.StatusCode);

        var crossTenantDetail = await deltaClient.GetAsync($"/api/v1/catalog/products/{atlasProductId:D}");
        var crossTenantDetailBody = await crossTenantDetail.Content.ReadAsStringAsync();
        var crossTenantReadiness = await deltaClient.GetAsync($"/api/v1/catalog/products/{atlasProductId:D}/readiness");
        var crossTenantReadinessBody = await crossTenantReadiness.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, crossTenantDetail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantReadiness.StatusCode);
        Assert.Equal(missingDetailBody, crossTenantDetailBody);
        Assert.Equal(missingReadinessBody, crossTenantReadinessBody);
        Assert.Equal(missingReadinessBody, crossTenantReadinessBody);
        Assert.DoesNotContain(atlasProductId.ToString("D"), crossTenantReadinessBody, StringComparison.OrdinalIgnoreCase);

        var visibleAgain = await atlasClient.GetAsync($"/api/v1/catalog/products/{atlasProductId:D}/readiness");
        Assert.Equal(HttpStatusCode.OK, visibleAgain.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfResponse>();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Authenticated", result.GetProperty("state").GetString());
        Assert.Equal(tenantId, result.GetProperty("tenantId").GetGuid());
    }

    private sealed record CsrfResponse(string Token);
}
