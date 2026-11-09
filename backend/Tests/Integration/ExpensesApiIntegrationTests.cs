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
    public async Task AuthenticatedExpenseImportIsIdempotentAndReportsPersistedRowOutcome()
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
        Guid categoryVersionId;
        await using (var connection = new NpgsqlConnection(adminConnection))
        {
            await connection.OpenAsync();
            await using var ids = new NpgsqlCommand("select t.id,(select id from platform.legal_entity where tenant_id=t.id and status='Active' order by id limit 1),(select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=t.id and c.legal_entity_id=(select id from platform.legal_entity where tenant_id=t.id and status='Active' order by id limit 1) and v.status='Published' order by v.effective_from desc limit 1) from platform.tenant t where t.slug='atlas-trading'", connection);
            await using var reader = await ids.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Atlas seed tenant, legal entity, or expense category is missing.");
            tenantId = reader.GetGuid(0);
            legalEntityId = reader.GetGuid(1);
            categoryVersionId = reader.GetGuid(2);
            await reader.CloseAsync();
            await using var grant = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,id,'expenses.create' from platform.role where tenant_id=$1 and code='administrator' on conflict do nothing", connection);
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

        var idempotencyKey = "api-import-" + Guid.CreateVersion7().ToString("N");
        var externalReference = "api-ext-" + Guid.CreateVersion7().ToString("N");
        using var content = JsonContent.Create(new
        {
            legalEntityId,
            idempotencyKey,
            rows = new[]
            {
                new
                {
                    externalReference,
                    employeeId = (Guid?)null,
                    merchantName = "API import acceptance",
                    expenseDate = "2026-11-09",
                    amount = 42.50m,
                    currencyCode = "EGP",
                    paymentSource = "CompanyPaid",
                    categoryVersionId,
                    businessPurpose = "Authenticated replay proof"
                }
            }
        });
        var first = await PostWithCsrfAsync(client, $"/api/v1/expenses/legal-entities/{legalEntityId:D}/imports/expenses", content);
        var firstPayload = await first.Content.ReadAsStringAsync();
        Assert.True(first.StatusCode == HttpStatusCode.Created, $"Import failed with {(int)first.StatusCode}: {firstPayload}");
        var firstBody = JsonSerializer.Deserialize<JsonElement>(firstPayload);
        Assert.Equal(1, firstBody.GetProperty("totalRows").GetInt32());
        Assert.Equal(1, firstBody.GetProperty("createdCount").GetInt32());
        var batchId = firstBody.GetProperty("id").GetGuid();

        using var replayContent = JsonContent.Create(new
        {
            legalEntityId,
            idempotencyKey,
            rows = new[]
            {
                new
                {
                    externalReference,
                    employeeId = (Guid?)null,
                    merchantName = "API import acceptance",
                    expenseDate = "2026-11-09",
                    amount = 42.50m,
                    currencyCode = "EGP",
                    paymentSource = "CompanyPaid",
                    categoryVersionId,
                    businessPurpose = "Authenticated replay proof"
                }
            }
        });
        var replay = await PostWithCsrfAsync(client, $"/api/v1/expenses/legal-entities/{legalEntityId:D}/imports/expenses", replayContent);
        var replayPayload = await replay.Content.ReadAsStringAsync();
        Assert.True(replay.StatusCode == HttpStatusCode.Created, $"Import replay failed with {(int)replay.StatusCode}: {replayPayload}");
        Assert.Equal(batchId, JsonSerializer.Deserialize<JsonElement>(replayPayload).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task AccountantCanDecideExpenseApprovalButEmployeeCannotUseApprovalAuthority()
    {
        var adminConnection = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for Expenses API integration tests.");
        var runtimeConnection = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for Expenses API integration tests.");
        var password = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_PASSWORD")
            ?? Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD")
            ?? throw new InvalidOperationException("Set TODIR_EXPENSES_TEST_PASSWORD or SEED_DEFAULT_PASSWORD for Expenses API integration tests.");

        Guid tenantId, legalEntityId, categoryVersionId, employeeId;
        string employeeEmail;
        await using (var connection = new NpgsqlConnection(adminConnection))
        {
            await connection.OpenAsync();
            await using var ids = new NpgsqlCommand("select t.id,(select id from platform.legal_entity where tenant_id=t.id and status='Active' order by id limit 1),(select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=t.id and c.legal_entity_id=(select id from platform.legal_entity where tenant_id=t.id and status='Active' order by id limit 1) and v.status='Published' order by v.effective_from desc limit 1),(select e.id from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id join business_hr.employee e on e.tenant_id=m.tenant_id and e.membership_id=m.id where i.tenant_id=t.id order by i.email limit 1),(select i.email from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id join business_hr.employee e on e.tenant_id=m.tenant_id and e.membership_id=m.id where i.tenant_id=t.id order by i.email limit 1) from platform.tenant t where t.slug='atlas-trading'", connection);
            await using var reader = await ids.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Atlas seed tenant, entity, category, and employee are required.");
            tenantId = reader.GetGuid(0); legalEntityId = reader.GetGuid(1); categoryVersionId = reader.GetGuid(2); employeeId = reader.GetGuid(3); employeeEmail = reader.GetString(4);
            await reader.CloseAsync();
            await using var reset = new NpgsqlCommand("update platform.identity set password_hash=$1,must_change_password=false,status='Active',security_stamp=gen_random_uuid(),version=version+1 where tenant_id=$2 and email in ('admin@atlas.com','accountant@atlas.com',$3)", connection);
            reset.Parameters.AddWithValue(IdentityConventions.HashPassword(password)); reset.Parameters.AddWithValue(tenantId); reset.Parameters.AddWithValue(employeeEmail); await reset.ExecuteNonQueryAsync();
            await using var grant = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,a.role_id,p.permission_code from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id join platform.membership_role_assignment a on a.tenant_id=m.tenant_id and a.membership_id=m.id cross join (values ('expenses.create'),('expenses.submit'),('expenses.read')) p(permission_code) where m.tenant_id=$1 and i.email=$2 on conflict do nothing", connection);
            grant.Parameters.AddWithValue(tenantId); grant.Parameters.AddWithValue(employeeEmail); await grant.ExecuteNonQueryAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(admin, password, tenantId);
        using var forgedEmployee = JsonContent.Create(new { legalEntityId, employeeId = Guid.CreateVersion7(), merchantName = "Forged employee acceptance", expenseDate = "2026-11-09", amount = 61m, currencyCode = "EGP", paymentSource = "EmployeePaid", categoryVersionId, businessPurpose = "Scope validation" });
        var forgedResponse = await PostWithCsrfAsync(admin, $"/api/v1/expenses/legal-entities/{legalEntityId:D}", forgedEmployee);
        Assert.Equal(HttpStatusCode.BadRequest, forgedResponse.StatusCode);
        Assert.Equal("expenses.employee_scope_invalid", (await forgedResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var create = JsonContent.Create(new { legalEntityId, employeeId, merchantName = "Approval authority acceptance", expenseDate = "2026-11-09", amount = 61m, currencyCode = "EGP", paymentSource = "EmployeePaid", categoryVersionId, businessPurpose = "Authority separation" });
        var created = await PostWithCsrfAsync(admin, $"/api/v1/expenses/legal-entities/{legalEntityId:D}", create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(); var expenseId = createdBody.GetProperty("id").GetGuid(); var version = createdBody.GetProperty("version").GetInt64();
        using var missingReceipt = JsonContent.Create(new { reason = "Supplier receipt is pending for authority test" });
        Assert.Equal(HttpStatusCode.Created, (await PostWithCsrfAsync(admin, $"/api/v1/expenses/legal-entities/{legalEntityId:D}/{expenseId:D}/missing-receipt", missingReceipt)).StatusCode);
        using var employee = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(employee, password, tenantId, employeeEmail);
        using var submit = JsonContent.Create(new { version }); Assert.Equal(HttpStatusCode.OK, (await PostWithCsrfAsync(employee, $"/api/v1/expenses/my/expenses/{expenseId:D}/submit?legalEntityId={legalEntityId:D}", submit)).StatusCode);
        using var request = await PostWithCsrfAsync(employee, $"/api/v1/expenses/my/expenses/{expenseId:D}/approval/request?legalEntityId={legalEntityId:D}", JsonContent.Create(new { reason = "Authority separation" }));
        var requestPayload = await request.Content.ReadAsStringAsync(); Assert.True(request.StatusCode == HttpStatusCode.OK, $"Approval request failed with {(int)request.StatusCode}: {requestPayload}"); var approval = JsonSerializer.Deserialize<JsonElement>(requestPayload); var requestId = approval.GetProperty("requestId").GetGuid();

        using var accountant = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(accountant, password, tenantId, "accountant@atlas.com");
        var decision = await PostWithCsrfAsync(accountant, $"/api/v1/expenses/legal-entities/{legalEntityId:D}/{expenseId:D}/approval/decision", JsonContent.Create(new { requestId, decision = "Approved", reason = "Finance reviewed" }));
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);

        var denied = await PostWithCsrfAsync(employee, $"/api/v1/expenses/legal-entities/{legalEntityId:D}/{expenseId:D}/approval/decision", JsonContent.Create(new { requestId, decision = "Rejected", reason = "IDOR attempt" }));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
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
        Guid foreignEmployeeId;
        Guid expenseId;
        Guid foreignExpenseId;
        long foreignExpenseVersion;
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
            await using var foreignEmployee = new NpgsqlCommand("select e.id from business_hr.employee e join business_hr.employment employment on employment.tenant_id=e.tenant_id and employment.employee_id=e.id where e.tenant_id=$1 and employment.legal_entity_id=$2 and e.id<>$3 and e.status='Active' and employment.status in ('Active','Suspended','NoticePeriod') order by e.id limit 1", connection);
            foreignEmployee.Parameters.AddWithValue(tenantId);
            foreignEmployee.Parameters.AddWithValue(legalEntityId);
            foreignEmployee.Parameters.AddWithValue(employeeId);
            foreignEmployeeId = (Guid)(await foreignEmployee.ExecuteScalarAsync() ?? throw new InvalidOperationException("Atlas seed requires a second active employee for self-service IDOR acceptance."));
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
        using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(admin, password, tenantId);
        using var foreignCreate = JsonContent.Create(new { legalEntityId, employeeId = foreignEmployeeId, merchantName = "Foreign employee API acceptance", expenseDate = "2026-11-09", amount = 73m, currencyCode = "EGP", paymentSource = "EmployeePaid", categoryVersionId, businessPurpose = "Cross-employee scope proof" });
        var foreignCreated = await PostWithCsrfAsync(admin, $"/api/v1/expenses/legal-entities/{legalEntityId:D}", foreignCreate);
        var foreignCreatedPayload = await foreignCreated.Content.ReadAsStringAsync();
        Assert.True(foreignCreated.StatusCode == HttpStatusCode.Created, $"Foreign employee expense creation failed with {(int)foreignCreated.StatusCode}: {foreignCreatedPayload}");
        var foreignCreatedBody = JsonSerializer.Deserialize<JsonElement>(foreignCreatedPayload);
        foreignExpenseId = foreignCreatedBody.GetProperty("id").GetGuid();
        foreignExpenseVersion = foreignCreatedBody.GetProperty("version").GetInt64();
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

        var advanceResponse = await PostWithCsrfAsync(client, "/api/v1/expenses/my/advances", JsonContent.Create(new
        {
            legalEntityId,
            employeeId = Guid.NewGuid(),
            purpose = "Authenticated employee advance",
            amount = 300m,
            currencyCode = "EGP",
            settlementDueDate = "2026-12-01"
        }));
        var advancePayload = await advanceResponse.Content.ReadAsStringAsync();
        Assert.True(advanceResponse.StatusCode == HttpStatusCode.Created, $"Self-service advance failed with {(int)advanceResponse.StatusCode}: {advancePayload}");
        var advanceBody = JsonSerializer.Deserialize<JsonElement>(advancePayload);
        Assert.Equal(employeeId, advanceBody.GetProperty("employeeId").GetGuid());
        Assert.Equal("Requested", advanceBody.GetProperty("state").GetString());

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
        var foreignMissing = await PostWithCsrfAsync(client, $"/api/v1/expenses/my/expenses/{foreignExpenseId:D}/missing-receipt?legalEntityId={legalEntityId:D}", JsonContent.Create(new { reason = "Cross-employee mutation attempt" }));
        Assert.True(foreignMissing.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"Unexpected cross-employee missing-receipt status {(int)foreignMissing.StatusCode}");
        var foreignSubmit = await PostWithCsrfAsync(client, $"/api/v1/expenses/my/expenses/{foreignExpenseId:D}/submit?legalEntityId={legalEntityId:D}", JsonContent.Create(new { version = foreignExpenseVersion }));
        Assert.True(foreignSubmit.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"Unexpected cross-employee submit status {(int)foreignSubmit.StatusCode}");
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
