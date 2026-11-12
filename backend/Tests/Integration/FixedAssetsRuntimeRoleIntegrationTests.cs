using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class FixedAssetsRuntimeRoleIntegrationTests : DatabaseIntegrationTestBase
{
    private const string TenantId = "c9fea3c8-249d-f150-a53a-84f5a891c82b";
    private const string LegalEntityId = "aad8a34b-344c-9e5b-8d58-57f182fc6528";

    [Fact]
    public async Task AuthenticatedApiUsesRuntimeRoleAndForcedRlsRemainsTenantScoped()
    {
        var adminConnection = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for Fixed Assets runtime-role verification.");
        var runtimeConnection = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for Fixed Assets runtime-role verification.");
        var password = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD")
            ?? throw new InvalidOperationException("Set SEED_DEFAULT_PASSWORD for Fixed Assets runtime-role verification.");

        await using (var admin = new NpgsqlConnection(adminConnection))
        {
            await admin.OpenAsync();
            await using var count = new NpgsqlCommand("select count(*) from business_assets.fixed_asset where tenant_id=$1 and legal_entity_id=$2", admin);
            count.Parameters.AddWithValue(Guid.Parse(TenantId));
            count.Parameters.AddWithValue(Guid.Parse(LegalEntityId));
            Assert.True((long)(await count.ExecuteScalarAsync() ?? 0L) > 0L);
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        Assert.NotNull(csrf);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email = "admin@atlas.com", password }) };
        login.Headers.Add("X-CSRF-TOKEN", csrf.Token);
        using var loginResponse = await client.SendAsync(login);
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        Assert.True(loginResponse.StatusCode == HttpStatusCode.OK, loginBody);

        using var list = await client.GetAsync($"/api/v1/fixed-assets/assets?legalEntityId={LegalEntityId}&limit=10");
        var listBody = await list.Content.ReadAsStringAsync();
        Assert.True(list.StatusCode == HttpStatusCode.OK, listBody);
        using var assets = JsonDocument.Parse(listBody);
        Assert.True(assets.RootElement.GetArrayLength() > 0);
        var firstAsset = assets.RootElement[0];
        var firstAssetId = firstAsset.GetProperty("id").GetGuid();
        using var scan = await client.GetAsync($"/api/v1/fixed-assets/assets/{firstAssetId:D}/scan");
        var scanBody = await scan.Content.ReadAsStringAsync();
        Assert.True(scan.StatusCode == HttpStatusCode.OK, scanBody);
        using var documents = await client.GetAsync($"/api/v1/fixed-assets/assets/{firstAssetId:D}/documents");
        var documentsBody = await documents.Content.ReadAsStringAsync();
        Assert.True(documents.StatusCode == HttpStatusCode.OK, documentsBody);

        using var categories = await client.GetAsync("/api/v1/fixed-assets/categories?limit=20");
        var categoriesBody = await categories.Content.ReadAsStringAsync();
        Assert.True(categories.StatusCode == HttpStatusCode.OK, categoriesBody);
        using var categoryJson = JsonDocument.Parse(categoriesBody);
        Assert.True(categoryJson.RootElement.GetArrayLength() >= 1);

        using var locations = await client.GetAsync($"/api/v1/fixed-assets/locations?legalEntityId={LegalEntityId}");
        var locationsBody = await locations.Content.ReadAsStringAsync();
        Assert.True(locations.StatusCode == HttpStatusCode.OK, locationsBody);

        using var books = await client.GetAsync($"/api/v1/fixed-assets/books?legalEntityId={LegalEntityId}");
        var booksBody = await books.Content.ReadAsStringAsync();
        Assert.True(books.StatusCode == HttpStatusCode.OK, booksBody);
        using var bookJson = JsonDocument.Parse(booksBody);
        Assert.True(bookJson.RootElement.GetArrayLength() >= 1);

        using var candidates = await client.GetAsync($"/api/v1/fixed-assets/capitalization-candidates?legalEntityId={LegalEntityId}&limit=20");
        var candidatesBody = await candidates.Content.ReadAsStringAsync();
        Assert.True(candidates.StatusCode == HttpStatusCode.OK, candidatesBody);

        await using var runtime = new NpgsqlConnection(runtimeConnection);
        await runtime.OpenAsync();
        await using var hidden = new NpgsqlCommand("select count(*) from business_assets.fixed_asset", runtime);
        Assert.Equal(0L, (long)(await hidden.ExecuteScalarAsync() ?? -1L));
        await using var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", runtime);
        tenantContext.Parameters.AddWithValue(Guid.Parse(TenantId).ToString("D"));
        await tenantContext.ExecuteNonQueryAsync();
        await using var visible = new NpgsqlCommand("select count(*) from business_assets.fixed_asset", runtime);
        Assert.True((long)(await visible.ExecuteScalarAsync() ?? 0L) > 0L);
        await using var delete = new NpgsqlCommand("delete from business_assets.fixed_asset where tenant_id=$1", runtime);
        delete.Parameters.AddWithValue(Guid.Parse(TenantId));
        await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
    }

    private sealed record CsrfResponse(string Token);
}
