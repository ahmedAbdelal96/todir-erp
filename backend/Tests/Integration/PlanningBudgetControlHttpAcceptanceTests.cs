using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Identity;
using Core.Transactions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Planning;
using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlanningBudgetControlHttpAcceptanceTests : DatabaseIntegrationTestBase
{
    private static readonly Guid AtlasTenant = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");

    [Fact(DisplayName = "BC-T09 authenticated budget override request and approval persist through HTTP")]
    public async Task AuthenticatedOverrideRequestAndApprovalPersistThroughHttp()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var password = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD") ?? "Bc01Http20261125";
        var policyId = Guid.CreateVersion7();
        var bucketId = Guid.CreateVersion7();
        var dimensionKey = $"{{\"bc01\":\"{bucketId:D}\"}}";
        Guid entityId;

        await using (var connection = new NpgsqlConnection(adminConnection))
        {
            await connection.OpenAsync();
            entityId = (Guid)(await ScalarAsync(connection, "select id from platform.legal_entity where tenant_id=$1 and status='Active' order by id limit 1", AtlasTenant))!;
            var planVersionId = (Guid)(await ScalarAsync(connection, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by id limit 1", AtlasTenant))!;
            var adminHash = IdentityConventions.HashPassword(password);
            await ExecuteAsync(connection, "update platform.identity set password_hash=$1,must_change_password=false,status='Active',security_stamp=gen_random_uuid(),version=version+1 where tenant_id=$2 and email in ('admin@atlas.com','accountant@atlas.com','expense-manager@atlas.com')", adminHash, AtlasTenant);
            await ExecuteAsync(connection, "insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,id,'planning.budget_control.override.approve' from platform.role where tenant_id=$1 and code='accountant' on conflict do nothing", AtlasTenant);
            await ExecuteAsync(connection, "insert into business_planning.budget_control_policy(tenant_id,id,legal_entity_id,name,mode,basis,threshold_percent,active,control_horizon,carryforward_mode,negative_budget_allowed,shadow_mode,opening_reconciled,effective_from) values($1,$2,$3,'BC-T09 authenticated override','RequireApproval','CommitmentAndActual',100,true,'Monthly','None',false,false,true,'2027-03-01')", AtlasTenant, policyId, entityId);
            await ExecuteAsync(connection, "insert into business_planning.budget_control_bucket(tenant_id,id,policy_id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key,original_budget,currency_code) values($1,$2,$3,$4,$5,'2027-03-01','OPEX',$6::jsonb,20000,'EGP')", AtlasTenant, bucketId, policyId, planVersionId, entityId, dimensionKey);
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var requester = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        using var approver = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        using var unauthorized = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(requester, "admin@atlas.com", password);
        await LoginAsync(approver, "accountant@atlas.com", password);
        await LoginAsync(unauthorized, "expense-manager@atlas.com", password);

        var sourceDocumentId = $"bc01-http-{Guid.CreateVersion7():N}";
        var request = new
        {
            bucketId,
            sourceModule = "purchasing",
            sourceDocumentId,
            sourceLineId = "purchase-order",
            sourceVersion = "1",
            amount = 30000m,
            currencyCode = "EGP",
            reason = "Authenticated BC01 budget-control override",
            validUntil = (DateTimeOffset?)null
        };
        using var checkResponse = await PostWithCsrfAsync(requester, "/api/v1/planning/budget/check", new
        {
            bucketId,
            proposedAmount = 30000m,
            sourceModule = "purchasing",
            sourceDocumentId,
            sourceLineId = "purchase-order",
            sourceVersion = "1",
            reserve = false
        });
        var checkBody = await ReadJsonAsync(checkResponse);
        Assert.Equal(HttpStatusCode.OK, checkResponse.StatusCode);
        Assert.Equal("RequireApproval", checkBody.GetProperty("decision").GetString());

        using var requestResponse = await PostWithCsrfAsync(requester, "/api/v1/planning/budget/overrides", request);
        var requestBody = await ReadJsonAsync(requestResponse);
        Assert.Equal(HttpStatusCode.Created, requestResponse.StatusCode);
        var overrideId = requestBody.GetProperty("id").GetGuid();
        Assert.Equal("Pending", requestBody.GetProperty("state").GetString());
        Assert.NotEqual(Guid.Empty, requestBody.GetProperty("approvalRequestId").GetGuid());

        using var pendingResponse = await requester.GetAsync($"/api/v1/planning/budget/overrides/{overrideId:D}");
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        Assert.Equal("Pending", (await ReadJsonAsync(pendingResponse)).GetProperty("state").GetString());

        using var selfApproval = await PostWithCsrfAsync(requester, $"/api/v1/planning/budget/overrides/{overrideId:D}/decision", new { decision = "Approved", reason = "Self approval must be rejected" });
        Assert.True(selfApproval.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden);
        Assert.Equal("Pending", (await ReadJsonAsync(await requester.GetAsync($"/api/v1/planning/budget/overrides/{overrideId:D}"))).GetProperty("state").GetString());

        using var unauthorizedApproval = await PostWithCsrfAsync(unauthorized, $"/api/v1/planning/budget/overrides/{overrideId:D}/decision", new { decision = "Approved", reason = "Unauthorized approval must be rejected" });
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedApproval.StatusCode);

        using var decisionResponse = await PostWithCsrfAsync(approver, $"/api/v1/planning/budget/overrides/{overrideId:D}/decision", new { decision = "Approved", reason = "Independent authenticated approval" });
        var decisionBody = await ReadJsonAsync(decisionResponse);
        Assert.Equal(HttpStatusCode.OK, decisionResponse.StatusCode);
        Assert.Equal("Approved", decisionBody.GetProperty("state").GetString());
        Assert.NotEqual(Guid.Empty, decisionBody.GetProperty("approvedBy").GetGuid());

        await using (var dataSource = NpgsqlDataSource.Create(runtimeConnection))
        {
            var coordinator = new NpgsqlTransactionCoordinator(dataSource);
            var capability = new PlanningBudgetControlCapability();
            var reservation = await coordinator.ExecuteAsync(new(AtlasTenant), (context, token) => capability.UpsertReservationAsync(context, new(AtlasTenant), Guid.NewGuid(), new(entityId, bucketId, "purchasing", sourceDocumentId, "purchase-order", "1", 30000m, "EGP", PlanningBudgetReservationKind.Commitment, new(2027, 3, 1)), token), default);
            Assert.Equal("Allow", reservation.Decision);
            await coordinator.ExecuteAsync(new(AtlasTenant), (context, token) => capability.UpsertReservationAsync(context, new(AtlasTenant), Guid.NewGuid(), new(entityId, bucketId, "purchasing", sourceDocumentId, "purchase-order", "1", 30000m, "EGP", PlanningBudgetReservationKind.Commitment, new(2027, 3, 1)), token), default);
        }

        await using var verify = new NpgsqlConnection(adminConnection); await verify.OpenAsync();
        await using var row = new NpgsqlCommand("select o.state,o.requested_by,o.approved_by,a.state from business_planning.budget_override o join platform.approval_request a on a.tenant_id=o.tenant_id and a.id=o.approval_request_id where o.tenant_id=$1 and o.id=$2", verify);
        row.Parameters.AddWithValue(AtlasTenant); row.Parameters.AddWithValue(overrideId);
        await using var reader = await row.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Approved", reader.GetString(0));
        Assert.NotEqual(reader.GetGuid(1), reader.GetGuid(2));
        Assert.Equal("Approved", reader.GetString(3));
        await reader.CloseAsync();
        Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(verify, "select count(*) from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$2 and source_module='purchasing' and source_document_id=$3 and source_line_id='purchase-order'", AtlasTenant, bucketId, sourceDocumentId), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(30000m, await ScalarAsync(verify, "select used_amount from business_planning.budget_override where tenant_id=$1 and id=$2", AtlasTenant, overrideId));
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException($"{name} is required.");

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        using var csrf = await client.GetAsync("/api/v1/auth/csrf"); csrf.EnsureSuccessStatusCode();
        var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        using var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object body)
    {
        using var csrf = await client.GetAsync("/api/v1/auth/csrf"); csrf.EnsureSuccessStatusCode();
        var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    { using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone(); }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    { await using var command = new NpgsqlCommand(sql, connection); foreach (var value in values) command.Parameters.AddWithValue(value); return await command.ExecuteScalarAsync(); }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params object[] values)
    { await using var command = new NpgsqlCommand(sql, connection); foreach (var value in values) command.Parameters.AddWithValue(value); await command.ExecuteNonQueryAsync(); }
}
