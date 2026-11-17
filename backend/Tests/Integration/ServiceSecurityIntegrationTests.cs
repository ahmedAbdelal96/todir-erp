using Core.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Service.DTOs;
using Modules.Service.Services;
using Npgsql;
using Shared.Primitives.Identifiers;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IntegrationTests;

[Collection("database-integration")]
public sealed class ServiceSecurityIntegrationTests
{
    private static readonly TenantId Atlas = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    private static readonly TenantId Delta = new(Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb"));
    private static readonly string[] TechPermissions = ["platform.organization.read", "service.work.read", "service.work.execute"];
    private static readonly string[] DispatcherPermissions = ["platform.organization.read", "service.work.read", "service.work.execute", "service.dispatch.manage"];
    private static readonly string[] DefaultTestPermissions = ["platform.organization.read", "service.work.read", "service.work.execute", "service.documents.manage"];

    [Fact(DisplayName = "Service HTTP resource scope, files, costs and offline assignment are enforced")]
    public async Task AuthenticatedHttpSecurityIsResourceScoped()
    {
        var connectionString = ServiceAcceptanceDatabaseGuard.RequireConfiguredConnection();
        await ServiceAcceptanceDatabaseGuard.VerifyAsync(connectionString);
        var suffix = Guid.CreateVersion7().ToString("N");
        var password = $"Service-Security-{suffix[..8]}1!";
        var atlasEmail = $"service-security-atlas-{suffix}@atlas.com";
        var deltaEmail = $"service-security-delta-{suffix}@delta.com";
        var atlasEntity = await ScalarAsync<Guid>(connectionString, "select id from platform.legal_entity where tenant_id=$1 and code='MAIN' and status='Active'", Atlas.Value);
        var secondaryEntity = await ScalarAsync<Guid>(connectionString, "select id from platform.legal_entity where tenant_id=$1 and code='SECONDARY' and status='Active'", Atlas.Value);
        var deltaEntity = await ScalarAsync<Guid>(connectionString, "select id from platform.legal_entity where tenant_id=$1 and status='Active' order by id limit 1", Delta.Value);
        var roleCode = $"service_http_security_{suffix}";
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await CreateRoleAsync(connection, transaction, Atlas.Value, roleCode);
            await CreateRoleAsync(connection, transaction, Delta.Value, roleCode);
            var atlasActor = await CreateAccountAsync(connection, transaction, Atlas.Value, atlasEntity, atlasEmail, password, roleCode);
            var deltaActor = await CreateAccountAsync(connection, transaction, Delta.Value, deltaEntity, deltaEmail, password, roleCode);
            await transaction.CommitAsync();

            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
            var service = factory.Services.GetRequiredService<ServiceService>();
            var secondaryWork = await service.CreateWorkOrderAsync(Atlas, atlasActor, Guid.CreateVersion7(),
                new CreateWorkOrderRequest(secondaryEntity, "InternalMaintenance", "Security secondary entity work", null, null, null, null, null, null), CancellationToken.None);
            var fieldWork = await service.CreateWorkOrderAsync(Atlas, atlasActor, Guid.CreateVersion7(),
                new CreateWorkOrderRequest(atlasEntity, "InternalMaintenance", "Security scoped work", null, null, null, null, null, null), CancellationToken.None);
            var visit = await service.CreateVisitAsync(Atlas, atlasActor, fieldWork.Id,
                new CreateVisitRequest(DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow.AddHours(1)), CancellationToken.None);
            await service.AssignAsync(Atlas, atlasActor, visit.Id, new CreateAssignmentRequest(null, atlasActor, DisplayName: "Restricted technician"), CancellationToken.None);
            var fileId = await CreateReadyFileAsync(connectionString, Atlas.Value, fieldWork.Id, atlasActor, "internal");
            var wrongOwnerFileId = await CreateReadyFileAsync(connectionString, Atlas.Value, secondaryWork.Id, atlasActor, "internal");
            var deltaWork = await service.CreateWorkOrderAsync(Delta, deltaActor, Guid.CreateVersion7(),
                new CreateWorkOrderRequest(deltaEntity, "InternalMaintenance", "Delta security work", null, null, null, null, null, null), CancellationToken.None);
            var crossTenantFileId = await CreateReadyFileAsync(connectionString, Delta.Value, deltaWork.Id, deltaActor, "internal");

            using var atlasClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(atlasClient, atlasEmail, password, Atlas.Value);
            Assert.Equal(HttpStatusCode.OK, (await atlasClient.GetAsync($"/api/v1/service/work-orders/{fieldWork.Id:D}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await atlasClient.GetAsync($"/api/v1/service/work-orders/{fieldWork.Id:D}/charges")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await atlasClient.GetAsync($"/api/v1/service/work-orders/{secondaryWork.Id:D}")).StatusCode);

            var report = await PostWithCsrfAsync(atlasClient, $"/api/v1/service/work-orders/{fieldWork.Id:D}/report", new { contentJson = "{\"result\":\"pass\"}", signedFileId = fileId });
            Assert.Equal(HttpStatusCode.Created, report.StatusCode);
            var wrongOwner = await PostWithCsrfAsync(atlasClient, $"/api/v1/service/work-orders/{fieldWork.Id:D}/report", new { contentJson = "{\"result\":\"wrong-owner\"}", signedFileId = wrongOwnerFileId });
            Assert.Equal(HttpStatusCode.BadRequest, wrongOwner.StatusCode);
            var crossTenant = await PostWithCsrfAsync(atlasClient, $"/api/v1/service/work-orders/{fieldWork.Id:D}/report", new { contentJson = "{\"result\":\"cross-tenant\"}", signedFileId = crossTenantFileId });
            Assert.Equal(HttpStatusCode.BadRequest, crossTenant.StatusCode);
            var forged = await PostWithCsrfAsync(atlasClient, $"/api/v1/service/work-orders/{fieldWork.Id:D}/report", new { contentJson = "{\"result\":\"forged\"}", signedFileId = Guid.CreateVersion7() });
            Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);

            var offline = new { clientCommandId = Guid.CreateVersion7(), commandType = "visit.start", workOrderId = fieldWork.Id, visitId = visit.Id, expectedVersion = 1L, payloadJson = "{}" };
            Assert.Equal(HttpStatusCode.Accepted, (await PostWithCsrfAsync(atlasClient, "/api/v1/service/mobile/commands", offline)).StatusCode);
            await ExecuteAsync(connectionString, Atlas, "update business_service.work_assignment set state='Removed' where tenant_id=$1 and visit_id=$2", visit.Id);
            var revoked = await PostWithCsrfAsync(atlasClient, "/api/v1/service/mobile/commands", new { offline.clientCommandId, offline.commandType, offline.workOrderId, offline.visitId, offline.expectedVersion, offline.payloadJson });
            Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);

            using var deltaClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(deltaClient, deltaEmail, password, Delta.Value);
            Assert.Equal(HttpStatusCode.Forbidden, (await deltaClient.GetAsync($"/api/v1/service/work-orders/{fieldWork.Id:D}")).StatusCode);
        }
    }

    [Fact(DisplayName = "Technician resource-level authorization: access strictly scoped to assigned visits, discovery restricted, revocation immediate")]
    public async Task TechnicianResourceLevelAuthorizationEnforced()
    {
        var connectionString = ServiceAcceptanceDatabaseGuard.RequireConfiguredConnection();
        await ServiceAcceptanceDatabaseGuard.VerifyAsync(connectionString);
        var suffix = Guid.CreateVersion7().ToString("N");
        var password = $"Tech-Security-{suffix[..8]}1!";
        var techAEmail = $"tech-a-{suffix}@atlas.com";
        var techBEmail = $"tech-b-{suffix}@atlas.com";
        var dispatcherEmail = $"dispatcher-{suffix}@atlas.com";

        var atlasEntity = await ScalarAsync<Guid>(connectionString, "select id from platform.legal_entity where tenant_id=$1 and code='MAIN' and status='Active'", Atlas.Value);
        var techRoleCode = $"tech_role_{suffix}";
        var dispatcherRoleCode = $"dispatcher_role_{suffix}";

        Guid techAId;
        Guid techBId;
        Guid dispatcherId;

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await CreateRoleAsync(connection, transaction, Atlas.Value, techRoleCode, TechPermissions);
            await CreateRoleAsync(connection, transaction, Atlas.Value, dispatcherRoleCode, DispatcherPermissions);

            techAId = await CreateAccountAsync(connection, transaction, Atlas.Value, atlasEntity, techAEmail, password, techRoleCode);
            techBId = await CreateAccountAsync(connection, transaction, Atlas.Value, atlasEntity, techBEmail, password, techRoleCode);
            dispatcherId = await CreateAccountAsync(connection, transaction, Atlas.Value, atlasEntity, dispatcherEmail, password, dispatcherRoleCode);
            await transaction.CommitAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        var service = factory.Services.GetRequiredService<ServiceService>();

        // 1. Create a Work Order and Visit
        var workOrder = await service.CreateWorkOrderAsync(Atlas, dispatcherId, Guid.CreateVersion7(),
            new CreateWorkOrderRequest(atlasEntity, "InternalMaintenance", "Technician authorization test work order", null, null, null, null, null, null), CancellationToken.None);

        var visit = await service.CreateVisitAsync(Atlas, dispatcherId, workOrder.Id,
            new CreateVisitRequest(DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow.AddHours(2)), CancellationToken.None);

        // 2. Assign visit to Technician A
        var assignment = await service.AssignAsync(Atlas, dispatcherId, visit.Id,
            new CreateAssignmentRequest(null, techAId, DisplayName: "Technician Alpha"), CancellationToken.None);

        // 3. Authenticate Technician A
        using var clientTechA = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(clientTechA, techAEmail, password, Atlas.Value);

        // Tech A CAN read assigned Visit (200 OK)
        var readByTechA = await clientTechA.GetAsync($"/api/v1/service/visits/{visit.Id:D}");
        Assert.Equal(HttpStatusCode.OK, readByTechA.StatusCode);

        // Tech A CAN mutate assigned Visit (travel transition)
        var travelByTechA = await PostWithCsrfAsync(clientTechA, $"/api/v1/service/visits/{visit.Id:D}/travel", new { version = visit.Version });
        Assert.Equal(HttpStatusCode.OK, travelByTechA.StatusCode);

        // Tech A CAN discover assigned visit in list
        var listByTechA = await clientTechA.GetAsync("/api/v1/service/visits");
        Assert.Equal(HttpStatusCode.OK, listByTechA.StatusCode);
        var visitsTechA = await listByTechA.Content.ReadFromJsonAsync<JsonElement>();
        var itemsTechA = visitsTechA.TryGetProperty("items", out var itA) ? itA : visitsTechA;
        var visitIdsTechA = itemsTechA.EnumerateArray().Select(v => v.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(visit.Id, visitIdsTechA);

        // 4. Authenticate Technician B (NOT assigned to visit)
        using var clientTechB = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(clientTechB, techBEmail, password, Atlas.Value);

        // Tech B CANNOT directly read Tech A's visit (403 Forbidden)
        var readByTechB = await clientTechB.GetAsync($"/api/v1/service/visits/{visit.Id:D}");
        Assert.Equal(HttpStatusCode.Forbidden, readByTechB.StatusCode);

        // Tech B CANNOT mutate Tech A's visit (403 Forbidden)
        var arriveByTechB = await PostWithCsrfAsync(clientTechB, $"/api/v1/service/visits/{visit.Id:D}/arrive", new { version = visit.Version + 1L });
        Assert.Equal(HttpStatusCode.Forbidden, arriveByTechB.StatusCode);

        // Tech B CANNOT discover Tech A's visit in list (discovery isolation)
        var listByTechB = await clientTechB.GetAsync("/api/v1/service/visits");
        Assert.Equal(HttpStatusCode.OK, listByTechB.StatusCode);
        var visitsTechB = await listByTechB.Content.ReadFromJsonAsync<JsonElement>();
        var itemsTechB = visitsTechB.TryGetProperty("items", out var itB) ? itB : visitsTechB;
        var visitIdsTechB = itemsTechB.EnumerateArray().Select(v => v.GetProperty("id").GetGuid()).ToList();
        Assert.DoesNotContain(visit.Id, visitIdsTechB);

        // 5. Revocation takes effect immediately: Dispatcher revokes Tech A's assignment
        using var clientDispatcher = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(clientDispatcher, dispatcherEmail, password, Atlas.Value);

        var revokeResp = await PostWithCsrfAsync(clientDispatcher, $"/api/v1/service/visits/{visit.Id:D}/assignments/{assignment.Id:D}/revoke");
        Assert.Equal(HttpStatusCode.OK, revokeResp.StatusCode);

        // Tech A immediate check: direct read now returns 403 Forbidden!
        var readRevokedTechA = await clientTechA.GetAsync($"/api/v1/service/visits/{visit.Id:D}");
        Assert.Equal(HttpStatusCode.Forbidden, readRevokedTechA.StatusCode);

        // Tech A immediate check: mutation now returns 403 Forbidden!
        var arriveRevokedTechA = await PostWithCsrfAsync(clientTechA, $"/api/v1/service/visits/{visit.Id:D}/arrive", new { version = visit.Version + 1L });
        Assert.Equal(HttpStatusCode.Forbidden, arriveRevokedTechA.StatusCode);

        // 6. Dispatcher retains broad access to the visit and list
        var readByDispatcher = await clientDispatcher.GetAsync($"/api/v1/service/visits/{visit.Id:D}");
        Assert.Equal(HttpStatusCode.OK, readByDispatcher.StatusCode);

        var listByDispatcher = await clientDispatcher.GetAsync("/api/v1/service/visits");
        Assert.Equal(HttpStatusCode.OK, listByDispatcher.StatusCode);
        var visitsDispatcher = await listByDispatcher.Content.ReadFromJsonAsync<JsonElement>();
        var itemsDispatcher = visitsDispatcher.TryGetProperty("items", out var itD) ? itD : visitsDispatcher;
        var visitIdsDispatcher = itemsDispatcher.EnumerateArray().Select(v => v.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(visit.Id, visitIdsDispatcher);
    }

    private static async Task CreateRoleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, string code, string[]? permissions = null)
    {
        await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            context.Parameters.AddWithValue(tenantId.ToString("D")); await context.ExecuteNonQueryAsync();
        }
        var roleId = Guid.CreateVersion7();
        await using (var role = new NpgsqlCommand("insert into platform.role(id,tenant_id,code,name,is_system_template,is_active,version) values($1,$2,$3,'Service HTTP security test',false,true,1)", connection, transaction))
        {
            role.Parameters.AddWithValue(roleId); role.Parameters.AddWithValue(tenantId); role.Parameters.AddWithValue(code); await role.ExecuteNonQueryAsync();
        }
        var perms = permissions ?? DefaultTestPermissions;
        foreach (var permission in perms)
        {
            await using var entry = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) values($1,$2,$3)", connection, transaction);
            entry.Parameters.AddWithValue(tenantId); entry.Parameters.AddWithValue(roleId); entry.Parameters.AddWithValue(permission); await entry.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Guid> CreateAccountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid legalEntityId, string email, string password, string roleCode)
    {
        await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            context.Parameters.AddWithValue(tenantId.ToString("D")); await context.ExecuteNonQueryAsync();
        }
        var identityId = Guid.CreateVersion7(); var membershipId = Guid.CreateVersion7();
        await using (var identity = new NpgsqlCommand("insert into platform.identity(id,tenant_id,email,normalized_email,login_prefix,name,email_verified,password_hash,must_change_password,status,created_at,security_stamp,version) values($1,$2,$3,$4,$5,'Service security test',true,$6,false,'Active',now(),$7,1)", connection, transaction))
        {
            identity.Parameters.AddWithValue(identityId); identity.Parameters.AddWithValue(tenantId); identity.Parameters.AddWithValue(email); identity.Parameters.AddWithValue(IdentityConventions.NormalizeEmail(email)); identity.Parameters.AddWithValue(email[..email.IndexOf('@')]); identity.Parameters.AddWithValue(IdentityConventions.HashPassword(password)); identity.Parameters.AddWithValue(Guid.CreateVersion7()); await identity.ExecuteNonQueryAsync();
        }
        await using (var membership = new NpgsqlCommand("insert into platform.membership(id,tenant_id,identity_id,display_name,status,default_legal_entity_id,created_at,version) values($1,$2,$3,'Service security test','Active',$4,now(),1)", connection, transaction))
        {
            membership.Parameters.AddWithValue(membershipId); membership.Parameters.AddWithValue(tenantId); membership.Parameters.AddWithValue(identityId); membership.Parameters.AddWithValue(legalEntityId); await membership.ExecuteNonQueryAsync();
        }
        await using var assignment = new NpgsqlCommand("insert into platform.membership_role_assignment(tenant_id,membership_id,role_id,scope_type,scope_id) select $1,$2,id,'LegalEntity',$4 from platform.role where tenant_id=$1 and code=$3 and is_active", connection, transaction);
        assignment.Parameters.AddWithValue(tenantId); assignment.Parameters.AddWithValue(membershipId); assignment.Parameters.AddWithValue(roleCode); assignment.Parameters.AddWithValue(legalEntityId); if (await assignment.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Service security role was not assigned.");
        return identityId;
    }

    private static async Task<Guid> CreateReadyFileAsync(string connectionString, Guid tenantId, Guid workOrderId, Guid actor, string classification)
    {
        var fileId = Guid.CreateVersion7(); await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, tenantId);
        await using var command = new NpgsqlCommand("insert into platform.file_record(id,tenant_id,owner_module,owner_entity_type,owner_entity_id,object_key,original_name,content_type,size,sha256,status,classification,created_by,created_at) values($1,$2,'service','work_order',$3,$4,'signed-report.pdf','application/pdf',5,$5,'Ready',$6,$7,now())", connection);
        command.Parameters.AddWithValue(fileId); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(workOrderId); command.Parameters.AddWithValue($"tenant/{tenantId:D}/service/work_order/{workOrderId:D}/{fileId:D}"); command.Parameters.AddWithValue(new string('b', 64)); command.Parameters.AddWithValue(classification); command.Parameters.AddWithValue(actor); await command.ExecuteNonQueryAsync(); return fileId;
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object? body = null)
    {
        using var csrfResponse = await client.GetAsync("/api/v1/auth/csrf"); var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = body != null ? JsonContent.Create(body) : JsonContent.Create(new { }) }; request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString()); return await client.SendAsync(request);
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        using var csrfResponse = await client.GetAsync("/api/v1/auth/csrf"); var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) }; request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString()); using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(tenantId, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tenantId").GetGuid());
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, values[0] is Guid guid ? guid : throw new InvalidOperationException("Tenant parameter required.")); await using var command = new NpgsqlCommand(sql, connection); foreach (var value in values) command.Parameters.AddWithValue(value); return (T)Convert.ChangeType(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected scalar."), typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(string connectionString, TenantId tenant, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, tenant.Value); await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenant.Value); foreach (var value in values) command.Parameters.AddWithValue(value); await command.ExecuteNonQueryAsync();
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, Guid tenant)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenant.ToString("D")); await command.ExecuteNonQueryAsync();
    }
}
