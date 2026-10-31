using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class LedgerAuthorizationIntegrationTests : DatabaseIntegrationTestBase
{
    private static readonly Guid AtlasTenant = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");
    private static readonly Guid AtlasEntity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");

    [Fact]
    public async Task LedgerReadinessEnforcesAnonymousForbiddenAndAuthorizedAccess()
    {
        var connection = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the Ledger authorization gate.");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));

        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(ReadinessPath())).StatusCode);

        using var administrator = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(administrator, "admin@atlas.com");
        Assert.Equal(HttpStatusCode.OK, (await administrator.GetAsync(ReadinessPath())).StatusCode);

        using var accountant = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(accountant, "accountant@atlas.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync(ReadinessPath())).StatusCode);
    }

    private static string ReadinessPath() => $"/api/v1/ledger/legal-entities/{AtlasEntity:D}/readiness";

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        var password = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD")
            ?? throw new InvalidOperationException("Set SEED_DEFAULT_PASSWORD for the Ledger authorization gate.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
