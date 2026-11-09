using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class ExpensesApiIntegrationTests : DatabaseIntegrationTestBase
{
    [Fact]
    public async Task PolicyCreationUsesLegalEntityRouteAndRejectsPayloadScopeMismatch()
    {
        var adminConnection = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for Expenses API integration tests.");
        var runtimeConnection = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for Expenses API integration tests.");
        var password = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_PASSWORD")
            ?? Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD")
            ?? throw new InvalidOperationException("Set TODIR_EXPENSES_TEST_PASSWORD or SEED_DEFAULT_PASSWORD for Expenses API integration tests.");

        Guid tenantId;
        Guid legalEntityId;
        Guid otherLegalEntityId;
        await using (var connection = new NpgsqlConnection(adminConnection))
        {
            await connection.OpenAsync();
            await using var tenant = new NpgsqlCommand("select id from platform.tenant where slug='atlas-trading'", connection);
            tenantId = (Guid)(await tenant.ExecuteScalarAsync() ?? throw new InvalidOperationException("Atlas tenant is missing from the canonical seed."));
            await using var entities = new NpgsqlCommand("select id from platform.legal_entity where tenant_id=$1 and status='Active' order by id", connection);
            entities.Parameters.AddWithValue(tenantId);
            await using var reader = await entities.ExecuteReaderAsync();
            var ids = new List<Guid>();
            while (await reader.ReadAsync()) ids.Add(reader.GetGuid(0));
            await reader.CloseAsync();
            if (ids.Count < 2) throw new InvalidOperationException("Expenses API scope acceptance requires two active Atlas legal entities.");
            legalEntityId = ids[0];
            otherLegalEntityId = ids[1];
            await using var grant = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,id,'expenses.category.manage' from platform.role where tenant_id=$1 and code='administrator' on conflict do nothing", connection);
            grant.Parameters.AddWithValue(tenantId);
            await grant.ExecuteNonQueryAsync();
            await using var reset = new NpgsqlCommand("update platform.identity set password_hash=$1,must_change_password=false,status='Active',security_stamp=gen_random_uuid(),version=version+1 where tenant_id=$2 and email='admin@atlas.com'", connection);
            reset.Parameters.AddWithValue(IdentityConventions.HashPassword(password));
            reset.Parameters.AddWithValue(tenantId);
            await reset.ExecuteNonQueryAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, password, tenantId);

        var code = "API-" + Guid.CreateVersion7().ToString("N");
        using var valid = JsonContent.Create(new { legalEntityId, code, name = "API policy scope acceptance" });
        var created = await PostWithCsrfAsync(client, $"/api/v1/expenses/legal-entities/{legalEntityId:D}/policies", valid);
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"Expected 201 but received {(int)created.StatusCode}: {createdPayload}");
        var createdBody = JsonSerializer.Deserialize<JsonElement>(createdPayload);
        Assert.Equal(legalEntityId, createdBody.GetProperty("legalEntityId").GetGuid());

        using var mismatched = JsonContent.Create(new { legalEntityId = otherLegalEntityId, code = code + "-MISMATCH", name = "Invalid cross-entity policy" });
        var rejected = await PostWithCsrfAsync(client, $"/api/v1/expenses/legal-entities/{legalEntityId:D}/policies", mismatched);
        var rejectedPayload = await rejected.Content.ReadAsStringAsync();
        Assert.True(rejected.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden, $"Unexpected mismatch status {(int)rejected.StatusCode}: {rejectedPayload}");
        if (rejected.StatusCode == HttpStatusCode.BadRequest) Assert.Contains("expenses.entity_scope_invalid", rejectedPayload, StringComparison.Ordinal);

        using var crossPayload = JsonContent.Create(new { legalEntityId, code = code + "-CROSS", name = "Invalid alternate entity policy" });
        var crossRoute = await PostWithCsrfAsync(client, $"/api/v1/expenses/legal-entities/{otherLegalEntityId:D}/policies", crossPayload);
        var crossPayloadText = await crossRoute.Content.ReadAsStringAsync();
        Assert.True(crossRoute.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden, $"Unexpected cross-entity status {(int)crossRoute.StatusCode}: {crossPayloadText}");
        if (crossRoute.StatusCode == HttpStatusCode.BadRequest) Assert.Contains("expenses.entity_scope_invalid", crossPayloadText, StringComparison.Ordinal);

        using var reportContent = JsonContent.Create(new { legalEntityId, employeeId = (Guid?)null, title = "API report detail acceptance", currencyCode = "EGP" });
        var report = await PostWithCsrfAsync(client, $"/api/v1/expenses/legal-entities/{legalEntityId:D}/reports", reportContent);
        var reportPayload = await report.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, report.StatusCode);
        var reportBody = JsonSerializer.Deserialize<JsonElement>(reportPayload);
        var reportId = reportBody.GetProperty("id").GetGuid();
        var detail = await client.GetAsync($"/api/v1/expenses/legal-entities/{legalEntityId:D}/reports/{reportId:D}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailBody = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(reportId, detailBody.GetProperty("id").GetGuid());
        var crossDetail = await client.GetAsync($"/api/v1/expenses/legal-entities/{otherLegalEntityId:D}/reports/{reportId:D}");
        Assert.True(crossDetail.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden, $"Unexpected cross-entity detail status {(int)crossDetail.StatusCode}");
    }

    [Fact]
    public async Task EmployeeSelfServiceReadUsesAuthenticatedMembershipScope()
    {
        var adminConnection = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for Expenses API integration tests.");
        var runtimeConnection = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for Expenses API integration tests.");
        var password = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_PASSWORD")
            ?? Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD")
            ?? throw new InvalidOperationException("Set TODIR_EXPENSES_TEST_PASSWORD or SEED_DEFAULT_PASSWORD for Expenses API integration tests.");

        Guid tenantId;
        Guid legalEntityId;
        Guid employeeId;
        string employeeEmail;
        await using (var connection = new NpgsqlConnection(adminConnection))
        {
            await connection.OpenAsync();
            await using var ids = new NpgsqlCommand("select t.id,(select id from platform.legal_entity where tenant_id=t.id and status='Active' order by id limit 1),(select e.id from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id join business_hr.employee e on e.tenant_id=m.tenant_id and e.membership_id=m.id where i.tenant_id=t.id order by i.email limit 1),(select i.email from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id join business_hr.employee e on e.tenant_id=m.tenant_id and e.membership_id=m.id where i.tenant_id=t.id order by i.email limit 1) from platform.tenant t where t.slug='atlas-trading'", connection);
            await using var reader = await ids.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Atlas seed tenant or office employee is missing.");
            tenantId = reader.GetGuid(0);
            legalEntityId = reader.GetGuid(1);
            employeeId = reader.GetGuid(2);
            employeeEmail = reader.GetString(3);
            await reader.CloseAsync();
            await using var grant = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select m.tenant_id,a.role_id,'expenses.read' from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id join platform.membership_role_assignment a on a.tenant_id=m.tenant_id and a.membership_id=m.id where m.tenant_id=$1 and i.email=$2 on conflict do nothing", connection);
            grant.Parameters.AddWithValue(tenantId);
            grant.Parameters.AddWithValue(employeeEmail);
            await grant.ExecuteNonQueryAsync();
            await using var reset = new NpgsqlCommand("update platform.identity set password_hash=$1,must_change_password=false,status='Active',security_stamp=gen_random_uuid(),version=version+1 where tenant_id=$2 and email=$3", connection);
            reset.Parameters.AddWithValue(IdentityConventions.HashPassword(password));
            reset.Parameters.AddWithValue(tenantId);
            reset.Parameters.AddWithValue(employeeEmail);
            await reset.ExecuteNonQueryAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, password, tenantId, employeeEmail);
        var response = await client.GetAsync($"/api/v1/expenses/my/expenses?legalEntityId={legalEntityId:D}&page=1&pageSize=10");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Self-service read failed with {(int)response.StatusCode}: {payload}");
        var body = JsonSerializer.Deserialize<JsonElement>(payload);
        Assert.True(body.TryGetProperty("items", out var items));
        foreach (var item in items.EnumerateArray())
            Assert.Equal(employeeId, item.GetProperty("employeeId").GetGuid());
    }

    private static Task LoginAsync(HttpClient client, string password, Guid tenantId) => LoginAsync(client, password, tenantId, "admin@atlas.com");

    private static async Task LoginAsync(HttpClient client, string password, Guid tenantId, string email)
    {
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfResponse>();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        var response = await client.SendAsync(request);
        var responsePayload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Login failed with {(int)response.StatusCode}: {responsePayload}");
        var result = JsonSerializer.Deserialize<JsonElement>(responsePayload);
        Assert.Equal("Authenticated", result.GetProperty("state").GetString());
        Assert.Equal(tenantId, result.GetProperty("tenantId").GetGuid());
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, HttpContent content)
    {
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfResponse>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await client.SendAsync(request);
    }

    private sealed record CsrfResponse(string Token);
}
