using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Core.Entitlements;
using Core.Identity;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlatformApiFlowTests
{
    private static readonly string[] SampleModuleIds = ["sample"];
    private static readonly string[] EmptyFeatures = [];

    [Fact]
    public async Task TenantBoundUserAdministrationRunsThroughTheHttpPipeline()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the platform API flow gate.");
        var appPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var tenant = TenantId.New();
        var administrator = IdentityId.New();
        var administratorMembership = MembershipId.New();
        var roleId = Guid.CreateVersion7();
        var legalEntityId = Guid.CreateVersion7();
        var branchId = Guid.CreateVersion7();
        var adminEmail = $"api-admin-{administrator.Value:N}@api-flow-{tenant.Value:N}.local";
        var password = "PlatformApiFlow-Only-For-CI1";
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = appPassword }.ConnectionString;
        Guid? createdUserId = null;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var adminHash = IdentityConventions.HashPassword(password);
        await ExecuteAsync(admin, $"alter role erp_app password '{appPassword}'; set role erp_owner; set app.tenant_id = '{tenant.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenant.Value}', 'api-flow-{tenant.Value:N}', 'API Flow', 'Active', 'api-flow-{tenant.Value:N}.local', 'api-flow-{tenant.Value:N}.local', now(), 1); insert into platform_org.legal_entity (id, tenant_id, code, legal_name, country_code, base_currency_code, timezone, locale, status, created_at, version) values ('{legalEntityId}', '{tenant.Value}', 'API', 'API Flow Entity', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1); insert into platform_org.branch (id, tenant_id, legal_entity_id, code, name, status, created_at, version) values ('{branchId}', '{tenant.Value}', '{legalEntityId}', 'MAIN', 'Main branch', 'Active', now(), 1); insert into platform_iam.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, password_hash, status, created_at, security_stamp, version) values ('{administrator.Value}', '{tenant.Value}', '{adminEmail}', '{adminEmail.ToUpperInvariant()}', 'api-admin-{administrator.Value:N}', 'Platform administrator', true, '{adminHash}', 'Active', now(), '{Guid.CreateVersion7()}', 1); insert into platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('{administratorMembership.Value}', '{tenant.Value}', '{administrator.Value}', 'Platform administrator', 'Active', now(), 1); insert into platform_authz.role (id, tenant_id, code, name, is_system_template, is_active, version) values ('{roleId}', '{tenant.Value}', 'tenant-admin', 'Tenant administrator', false, true, 1); insert into platform_authz.role_permission (tenant_id, role_id, permission_code) values ('{tenant.Value}', '{roleId}', 'platform.identity.manage'), ('{tenant.Value}', '{roleId}', 'platform.identity.read'), ('{tenant.Value}', '{roleId}', 'platform.organization.read'), ('{tenant.Value}', '{roleId}', 'platform.role.manage'), ('{tenant.Value}', '{roleId}', 'platform.module.view'), ('{tenant.Value}', '{roleId}', 'platform.settings.read'), ('{tenant.Value}', '{roleId}', 'platform.audit.view'), ('{tenant.Value}', '{roleId}', 'platform.tenancy.read'); insert into platform_authz.membership_role_assignment (tenant_id, membership_id, role_id, scope_type) values ('{tenant.Value}', '{administratorMembership.Value}', '{roleId}', 'Tenant'); reset app.tenant_id; reset role;");

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development").UseSetting("TODIR_DATABASE_CONNECTION", appConnectionString).UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(client, adminEmail, password, tenant.Value);
            var contextResponse = await client.GetAsync("/api/v1/session/context");
            Assert.Equal(HttpStatusCode.OK, contextResponse.StatusCode);
            var context = await contextResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, context.GetProperty("context").GetProperty("legalEntities").GetArrayLength());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/tenants/{tenant.Value:D}/organization")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/tenants/{tenant.Value:D}/roles")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/tenants/{tenant.Value:D}/modules")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/tenants/{tenant.Value:D}/settings")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/tenants/{tenant.Value:D}/audit")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(client, "/api/v1/context/organization", new { legalEntityId, branchId })).StatusCode);

            var create = await PostAsync(client, $"/api/v1/tenants/{tenant.Value:D}/users", new { name = "أحمد محمد", emailPrefix = "ahmed" });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<JsonElement>();
            createdUserId = created.GetProperty("user").GetProperty("id").GetGuid();
            Assert.Equal($"ahmed@api-flow-{tenant.Value:N}.local", created.GetProperty("user").GetProperty("email").GetString());
            Assert.False(string.IsNullOrWhiteSpace(created.GetProperty("temporaryPassword").GetString()));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/tenants/{tenant.Value:D}/users")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/tenants/{tenant.Value:D}/users/{createdUserId:D}")).StatusCode);
            var domain = await client.GetAsync($"/api/v1/tenants/{tenant.Value:D}/login-domain");
            Assert.Equal(HttpStatusCode.OK, domain.StatusCode);
            Assert.Equal($"api-flow-{tenant.Value:N}.local", (await domain.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("loginDomain").GetString());
            var reset = await PostAsync(client, $"/api/v1/tenants/{tenant.Value:D}/users/{createdUserId:D}/password-reset", null);
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
            Assert.False(string.IsNullOrWhiteSpace((await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("temporaryPassword").GetString()));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenant.Value}'; delete from platform_audit.audit_entry where tenant_id = '{tenant.Value}'; delete from platform_authz.membership_role_assignment where tenant_id = '{tenant.Value}'; delete from platform_authz.role_permission where tenant_id = '{tenant.Value}'; delete from platform_authz.role where tenant_id = '{tenant.Value}'; delete from platform_iam.membership where tenant_id = '{tenant.Value}'; delete from platform_iam.identity where tenant_id = '{tenant.Value}'; delete from platform_org.branch where id = '{branchId}'; delete from platform_org.legal_entity where id = '{legalEntityId}'; delete from platform_tenancy.tenant where id = '{tenant.Value}'; reset app.tenant_id; reset role;");
        }
    }

    [Fact]
    public async Task SampleEndpointRequiresEntitlementThenLoadsTheTenantSnapshot()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the Sample endpoint integration gate.");
        var appPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var tenant = TenantId.New(); var identity = IdentityId.New(); var membership = MembershipId.New(); var roleId = Guid.CreateVersion7();
        var domain = $"sample-api-{tenant.Value:N}.local"; var email = $"sample-api-{identity.Value:N}@{domain}"; var password = "SampleApiFlow-Only-For-CI1";
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = appPassword }.ConnectionString;
        await using var admin = new NpgsqlConnection(adminConnectionString); await admin.OpenAsync();
        var issued = TruncateToPostgresPrecision(DateTimeOffset.UtcNow); var validUntil = issued.AddMinutes(10); const string entitlementKey = "sample-api-entitlement-key"; var hash = IdentityConventions.HashPassword(password);
        await ExecuteAsync(admin, $"alter role erp_app password '{appPassword}'; set role erp_owner; set app.tenant_id = '{tenant.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenant.Value}', 'sample-api-{tenant.Value:N}', 'Sample API', 'Active', '{domain}', '{domain}', now(), 1); insert into platform_iam.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, password_hash, status, created_at, security_stamp, version) values ('{identity.Value}', '{tenant.Value}', '{email}', '{email.ToUpperInvariant()}', 'sample-api-{identity.Value:N}', 'Sample API administrator', true, '{hash}', 'Active', now(), '{Guid.CreateVersion7()}', 1); insert into platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('{membership.Value}', '{tenant.Value}', '{identity.Value}', 'Sample API administrator', 'Active', now(), 1); insert into platform_authz.role (id, tenant_id, code, name, is_system_template, is_active, version) values ('{roleId}', '{tenant.Value}', 'sample-admin', 'Sample administrator', false, true, 1); insert into platform_authz.role_permission (tenant_id, role_id, permission_code) values ('{tenant.Value}', '{roleId}', 'sample.item.create'); insert into platform_authz.membership_role_assignment (tenant_id, membership_id, role_id, scope_type) values ('{tenant.Value}', '{membership.Value}', '{roleId}', 'Tenant'); reset app.tenant_id; reset role;");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development").UseSetting("TODIR_DATABASE_CONNECTION", appConnectionString).UseSetting("TODIR_RABBITMQ_HOST", string.Empty).UseSetting("TODIR_ENTITLEMENT_HMAC_KEY", entitlementKey));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true }); await LoginAsync(client, email, password, tenant.Value);
            Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(client, "/api/v1/sample/items", new { name = "before-entitlement" })).StatusCode);
            var signature = CreateEntitlementSignature(tenant, issued, validUntil, entitlementKey);
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenant.Value}'; insert into platform_entitlements.entitlement_snapshot (tenant_id, snapshot_version, issued_at, valid_until, key_id, modules, features, limits, scopes, signature) values ('{tenant.Value}', 1, '{issued:O}', '{validUntil:O}', 'current', '[\"sample\"]'::jsonb, '[]'::jsonb, '{{}}'::jsonb, '{{}}'::jsonb, '{signature}'); reset app.tenant_id; reset role;");
            await using (var verificationDataSource = NpgsqlDataSource.Create(appConnectionString)) { var persisted = await new NpgsqlEntitlementSnapshotStore(verificationDataSource).LoadLatestAsync(tenant); Assert.NotNull(persisted); Assert.Equal(signature, persisted!.Signature); Assert.True(new HmacEntitlementVerifier(new Dictionary<string, byte[]> { ["current"] = Encoding.UTF8.GetBytes(entitlementKey) }).Verify(persisted)); }
            var created = await PostAsync(client, "/api/v1/sample/items", new { name = "after-entitlement" }); var body = await created.Content.ReadAsStringAsync(); Assert.True(created.StatusCode == HttpStatusCode.Created, $"Expected Created but got {created.StatusCode}: {body}");
        }
        finally { await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenant.Value}'; delete from sample.sample_projection where tenant_id = '{tenant.Value}'; delete from sample.sample_item where tenant_id = '{tenant.Value}'; delete from platform_messaging.outbox_message where tenant_id = '{tenant.Value}'; delete from platform_audit.audit_entry where tenant_id = '{tenant.Value}'; delete from platform_entitlements.entitlement_snapshot where tenant_id = '{tenant.Value}'; delete from platform_authz.membership_role_assignment where tenant_id = '{tenant.Value}'; delete from platform_authz.role_permission where tenant_id = '{tenant.Value}'; delete from platform_authz.role where tenant_id = '{tenant.Value}'; delete from platform_iam.membership where tenant_id = '{tenant.Value}'; delete from platform_iam.identity where id = '{identity.Value}'; delete from platform_tenancy.tenant where id = '{tenant.Value}'; reset app.tenant_id; reset role;"); }
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var response = await PostAsync(client, "/api/v1/auth/login", new { email, password }); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var authentication = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("Authenticated", authentication.GetProperty("state").GetString()); Assert.Equal(tenantId, authentication.GetProperty("tenantId").GetGuid());
    }
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object? body) { var csrfResponse = await client.GetAsync("/api/v1/auth/csrf"); Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode); var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfResponse>(); using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body ?? new { }) }; request.Headers.Add("X-CSRF-TOKEN", csrf!.Token); return await client.SendAsync(request); }
    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql) { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
    private static string CreateEntitlementSignature(TenantId tenant, DateTimeOffset issued, DateTimeOffset validUntil, string key) { var canonical = JsonSerializer.Serialize(new { tenant = tenant.Value, snapshot = 1, issued, valid = validUntil, modules = SampleModuleIds, features = EmptyFeatures, limits = new Dictionary<string, long>() }); return Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(canonical))); }
    private static DateTimeOffset TruncateToPostgresPrecision(DateTimeOffset value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerMicrosecond, value.Offset);
    private sealed record CsrfResponse(string Token);
}
