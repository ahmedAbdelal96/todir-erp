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
        Guid expenseId;
        Guid categoryVersionId;
        Guid cardTransactionId;
        string cardProviderReference;
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
            await using var expense = new NpgsqlCommand("select id from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 order by created_at limit 1", connection);
            expense.Parameters.AddWithValue(tenantId);
            expense.Parameters.AddWithValue(legalEntityId);
            expenseId = (Guid)(await expense.ExecuteScalarAsync() ?? throw new InvalidOperationException("Atlas seed expense is missing."));
            await using var category = new NpgsqlCommand("select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", connection);
            category.Parameters.AddWithValue(tenantId);
            category.Parameters.AddWithValue(legalEntityId);
            categoryVersionId = (Guid)(await category.ExecuteScalarAsync() ?? throw new InvalidOperationException("Atlas expense category version is missing."));
            cardTransactionId = Guid.CreateVersion7();
            cardProviderReference = "api-self-card-" + Guid.CreateVersion7().ToString("N");
            await using var card = new NpgsqlCommand("insert into business_expenses.expense_card_transaction(tenant_id,id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id) values($1,$2,$3,$4,'API self card',date '2026-11-02',88,'EGP','Cleared',$5)", connection);
            card.Parameters.AddWithValue(tenantId);
            card.Parameters.AddWithValue(cardTransactionId);
            card.Parameters.AddWithValue(legalEntityId);
            card.Parameters.AddWithValue(cardProviderReference);
            card.Parameters.AddWithValue(employeeId);
            await card.ExecuteNonQueryAsync();
            await using var grant = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select m.tenant_id,a.role_id,p.permission_code from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id join platform.membership_role_assignment a on a.tenant_id=m.tenant_id and a.membership_id=m.id cross join (values ('expenses.read'),('expenses.create'),('expenses.submit')) p(permission_code) where m.tenant_id=$1 and i.email=$2 on conflict do nothing", connection);
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

        var cards = await client.GetAsync($"/api/v1/expenses/my/corporate-card/transactions?legalEntityId={legalEntityId:D}&search={cardProviderReference}&page=1&pageSize=10");
        var cardsPayload = await cards.Content.ReadAsStringAsync();
        Assert.True(cards.StatusCode == HttpStatusCode.OK, $"Self-service card read failed with {(int)cards.StatusCode}: {cardsPayload}");
        var cardsBody = JsonSerializer.Deserialize<JsonElement>(cardsPayload);
        Assert.Equal(1, cardsBody.GetProperty("totalCount").GetInt32());
        Assert.Equal(cardTransactionId, cardsBody.GetProperty("items")[0].GetProperty("id").GetGuid());

        var cardExpense = await PostWithCsrfAsync(client, $"/api/v1/expenses/my/corporate-card/transactions/{cardTransactionId:D}/create-expense?legalEntityId={legalEntityId:D}", JsonContent.Create(new { categoryVersionId, businessPurpose = "Authenticated corporate-card acceptance" }));
        var cardExpensePayload = await cardExpense.Content.ReadAsStringAsync();
        Assert.True(cardExpense.StatusCode == HttpStatusCode.Created, $"Self-service card expense failed with {(int)cardExpense.StatusCode}: {cardExpensePayload}");
        var cardExpenseBody = JsonSerializer.Deserialize<JsonElement>(cardExpensePayload);
        Assert.Equal(88m, cardExpenseBody.GetProperty("amount").GetDecimal());
        Assert.Equal("CorporateCard", cardExpenseBody.GetProperty("paymentSource").GetString());

        using var create = JsonContent.Create(new
        {
            legalEntityId,
            merchantName = "API self-service journey",
            expenseDate = "2026-11-01",
            amount = 125m,
            currencyCode = "EGP",
            paymentSource = "EmployeePaid",
            categoryVersionId,
            businessPurpose = "Authenticated employee acceptance"
        });
        var created = await PostWithCsrfAsync(client, "/api/v1/expenses/my/expenses", create);
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"Self-service create failed with {(int)created.StatusCode}: {createdPayload}");
        var createdBody = JsonSerializer.Deserialize<JsonElement>(createdPayload);
        var createdId = createdBody.GetProperty("id").GetGuid();
        var missing = await PostWithCsrfAsync(client, $"/api/v1/expenses/my/expenses/{createdId:D}/missing-receipt?legalEntityId={legalEntityId:D}", JsonContent.Create(new { reason = "Receipt unavailable during API acceptance" }));
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        var submitted = await PostWithCsrfAsync(client, $"/api/v1/expenses/my/expenses/{createdId:D}/submit?legalEntityId={legalEntityId:D}", JsonContent.Create(new { version = createdBody.GetProperty("version").GetInt64() }));
        var submittedPayload = await submitted.Content.ReadAsStringAsync();
        Assert.True(submitted.StatusCode == HttpStatusCode.OK, $"Self-service submit failed with {(int)submitted.StatusCode}: {submittedPayload}");
        Assert.Equal("Submitted", JsonSerializer.Deserialize<JsonElement>(submittedPayload).GetProperty("expenseState").GetString());

        // Self-service employees must not use the entity-wide route to read another
        // employee's expense, even when the role has the generic expenses.read grant.
        var direct = await client.GetAsync($"/api/v1/expenses/legal-entities/{legalEntityId:D}/{expenseId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, direct.StatusCode);
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
