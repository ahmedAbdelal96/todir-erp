using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Todir.Platform.Identity;
using Todir.SharedKernel.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlatformApiFlowTests
{
    private static readonly string[] SampleModuleIds = ["sample"];
    private static readonly string[] EmptyFeatures = [];

    [Fact]
    public async Task PlatformMembershipFlowRunsThroughTheHttpPipeline()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the platform API flow gate.");
        var appPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();
        var administrator = IdentityId.New();
        var invitedIdentity = IdentityId.New();
        var administratorMembershipA = MembershipId.New();
        var administratorMembershipB = MembershipId.New();
        var invitedMembershipB = MembershipId.New();
        var roleId = Guid.CreateVersion7();
        var legalEntityId = Guid.CreateVersion7();
        var branchId = Guid.CreateVersion7();
        var adminEmail = $"api-admin-{administrator.Value:N}@example.com";
        var invitedEmail = $"api-invited-{invitedIdentity.Value:N}@example.com";
        var password = "PlatformApiFlow-Only-For-CI";
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = appPassword }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var adminHash = IdentityConventions.HashPassword(password);
        await ExecuteAsync(admin, $"alter role erp_app password '{appPassword}'; set role erp_owner; set app.tenant_id = '{tenantA.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('{tenantA.Value}', 'api-flow-a-{tenantA.Value:N}', 'API Flow A', 'Active', now(), 1); set app.tenant_id = '{tenantB.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('{tenantB.Value}', 'api-flow-b-{tenantB.Value:N}', 'API Flow B', 'Active', now(), 1); set app.tenant_id = '{tenantA.Value}'; insert into platform_org.legal_entity (id, tenant_id, code, legal_name, country_code, base_currency_code, timezone, locale, status, created_at, version) values ('{legalEntityId}', '{tenantA.Value}', 'API', 'API Flow Entity', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1); insert into platform_org.branch (id, tenant_id, legal_entity_id, code, name, status, created_at, version) values ('{branchId}', '{tenantA.Value}', '{legalEntityId}', 'MAIN', 'Main branch', 'Active', now(), 1); reset app.tenant_id; insert into platform_iam.identity (id, normalized_email, email_verified, password_hash, status, created_at, security_stamp, version) values ('{administrator.Value}', '{adminEmail.ToUpperInvariant()}', true, '{adminHash}', 'Active', now(), '{Guid.CreateVersion7()}', 1), ('{invitedIdentity.Value}', '{invitedEmail.ToUpperInvariant()}', true, '{adminHash}', 'Active', now(), '{Guid.CreateVersion7()}', 1); set app.tenant_id = '{tenantA.Value}'; insert into platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('{administratorMembershipA.Value}', '{tenantA.Value}', '{administrator.Value}', 'Platform administrator A', 'Active', now(), 1); set app.tenant_id = '{tenantB.Value}'; insert into platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('{administratorMembershipB.Value}', '{tenantB.Value}', '{administrator.Value}', 'Platform administrator B', 'Active', now(), 1), ('{invitedMembershipB.Value}', '{tenantB.Value}', '{invitedIdentity.Value}', 'Invited member B', 'Active', now(), 1); set app.tenant_id = '{tenantA.Value}'; insert into platform_authz.role (id, tenant_id, code, name, is_system_template, is_active, version) values ('{roleId}', '{tenantA.Value}', 'tenant-admin', 'Tenant administrator', false, true, 1); insert into platform_authz.role_permission (tenant_id, role_id, permission_code) values ('{tenantA.Value}', '{roleId}', 'platform.membership.manage'), ('{tenantA.Value}', '{roleId}', 'platform.organization.read'), ('{tenantA.Value}', '{roleId}', 'platform.role.manage'), ('{tenantA.Value}', '{roleId}', 'platform.module.view'), ('{tenantA.Value}', '{roleId}', 'platform.settings.read'), ('{tenantA.Value}', '{roleId}', 'platform.audit.view'); insert into platform_authz.membership_role_assignment (tenant_id, membership_id, role_id, scope_type) values ('{tenantA.Value}', '{administratorMembershipA.Value}', '{roleId}', 'Tenant'); reset app.tenant_id; reset role;");

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", appConnectionString)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
            using var administratorClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            using var invitedClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });

            await LoginAsync(administratorClient, adminEmail, password, tenantA.Value);
            var contextResponse = await administratorClient.GetAsync("/api/v1/session/context");
            Assert.Equal(HttpStatusCode.OK, contextResponse.StatusCode);
            var organization = await contextResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, organization.GetProperty("context").GetProperty("legalEntities").GetArrayLength());
            Assert.Equal(1, organization.GetProperty("context").GetProperty("branches").GetArrayLength());
            Assert.Equal(HttpStatusCode.OK, (await administratorClient.GetAsync($"/api/v1/tenants/{tenantA.Value:D}/organization")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await administratorClient.GetAsync($"/api/v1/tenants/{tenantA.Value:D}/roles")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await administratorClient.GetAsync($"/api/v1/tenants/{tenantA.Value:D}/modules")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await administratorClient.GetAsync($"/api/v1/tenants/{tenantA.Value:D}/settings")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await administratorClient.GetAsync($"/api/v1/tenants/{tenantA.Value:D}/audit")).StatusCode);
            var organizationSwitch = await PostAsync(administratorClient, "/api/v1/context/organization", new { legalEntityId, branchId });
            Assert.Equal(HttpStatusCode.NoContent, organizationSwitch.StatusCode);
            var memberships = await administratorClient.GetAsync($"/api/v1/tenants/{tenantA.Value:D}/memberships");
            Assert.Equal(HttpStatusCode.OK, memberships.StatusCode);

            var invitationResponse = await PostAsync(administratorClient, $"/api/v1/tenants/{tenantA.Value:D}/memberships/invitations", new { identityId = invitedIdentity.Value, displayName = "Invited member A", expiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
            Assert.Equal(HttpStatusCode.Created, invitationResponse.StatusCode);
            var invitation = await invitationResponse.Content.ReadFromJsonAsync<MembershipInvitation>();
            Assert.NotNull(invitation);

            var acceptResponse = await PostAsync(invitedClient, $"/api/v1/tenants/{tenantA.Value:D}/memberships/invitations/accept", new { token = invitation!.Token });
            Assert.Equal(HttpStatusCode.NoContent, acceptResponse.StatusCode);
            await LoginAsync(invitedClient, invitedEmail, password, tenantA.Value);

            var crossTenantResponse = await PostAsync(invitedClient, $"/api/v1/tenants/{tenantB.Value:D}/memberships/invitations", new { identityId = invitedIdentity.Value, displayName = "Must be denied", expiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
            Assert.Equal(HttpStatusCode.Forbidden, crossTenantResponse.StatusCode);

            var switchResponse = await PostAsync(invitedClient, "/api/v1/context/tenant", new { tenantId = tenantB.Value });
            Assert.Equal(HttpStatusCode.NoContent, switchResponse.StatusCode);
            var switchedSession = await invitedClient.GetFromJsonAsync<SessionResponse>("/api/v1/session");
            Assert.Equal(tenantB.Value, switchedSession!.TenantId);

            var switchBackResponse = await PostAsync(invitedClient, "/api/v1/context/tenant", new { tenantId = tenantA.Value });
            Assert.Equal(HttpStatusCode.NoContent, switchBackResponse.StatusCode);

            var suspendResponse = await PostAsync(administratorClient, $"/api/v1/tenants/{tenantA.Value:D}/memberships/{invitation.MembershipId:D}/suspend", null);
            Assert.Equal(HttpStatusCode.NoContent, suspendResponse.StatusCode);
            var suspendedSession = await invitedClient.GetAsync("/api/v1/session");
            Assert.Equal(HttpStatusCode.Unauthorized, suspendedSession.StatusCode);
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantA.Value}'; delete from platform_iam.membership_invitation where tenant_id = '{tenantA.Value}'; delete from platform_authz.membership_role_assignment where tenant_id = '{tenantA.Value}'; delete from platform_authz.role_permission where tenant_id = '{tenantA.Value}'; delete from platform_authz.role where tenant_id = '{tenantA.Value}'; delete from platform_iam.membership where tenant_id = '{tenantA.Value}'; delete from platform_org.branch where id = '{branchId}'; delete from platform_org.legal_entity where id = '{legalEntityId}'; delete from platform_tenancy.tenant where id = '{tenantA.Value}'; set app.tenant_id = '{tenantB.Value}'; delete from platform_iam.membership where tenant_id = '{tenantB.Value}'; delete from platform_tenancy.tenant where id = '{tenantB.Value}'; reset app.tenant_id; delete from platform_iam.identity where id in ('{administrator.Value}', '{invitedIdentity.Value}'); reset role;");
        }
    }

    [Fact]
    public async Task SampleEndpointRequiresEntitlementThenLoadsTheTenantSnapshot()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the Sample endpoint integration gate.");
        var appPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var tenant = TenantId.New();
        var identity = IdentityId.New();
        var membership = MembershipId.New();
        var roleId = Guid.CreateVersion7();
        var email = $"sample-api-{identity.Value:N}@example.com";
        var password = "SampleApiFlow-Only-For-CI";
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = appPassword }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var issued = DateTimeOffset.UtcNow;
        var validUntil = issued.AddMinutes(10);
        const string entitlementKey = "sample-api-entitlement-key";
        var hash = IdentityConventions.HashPassword(password);
        await ExecuteAsync(admin, $"alter role erp_app password '{appPassword}'; set role erp_owner; set app.tenant_id = '{tenant.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('{tenant.Value}', 'sample-api-{tenant.Value:N}', 'Sample API', 'Active', now(), 1); insert into platform_iam.identity (id, normalized_email, email_verified, password_hash, status, created_at, security_stamp, version) values ('{identity.Value}', '{email.ToUpperInvariant()}', true, '{hash}', 'Active', now(), '{Guid.CreateVersion7()}', 1); insert into platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('{membership.Value}', '{tenant.Value}', '{identity.Value}', 'Sample API administrator', 'Active', now(), 1); insert into platform_authz.role (id, tenant_id, code, name, is_system_template, is_active, version) values ('{roleId}', '{tenant.Value}', 'sample-admin', 'Sample administrator', false, true, 1); insert into platform_authz.role_permission (tenant_id, role_id, permission_code) values ('{tenant.Value}', '{roleId}', 'sample.item.create'); insert into platform_authz.membership_role_assignment (tenant_id, membership_id, role_id, scope_type) values ('{tenant.Value}', '{membership.Value}', '{roleId}', 'Tenant'); reset app.tenant_id; reset role;");

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", appConnectionString)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)
                .UseSetting("TODIR_ENTITLEMENT_HMAC_KEY", entitlementKey));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(client, email, password, tenant.Value);

            var denied = await PostAsync(client, "/api/v1/sample/items", new { name = "before-entitlement" });
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

            var signature = CreateEntitlementSignature(tenant, issued, validUntil, entitlementKey);
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenant.Value}'; insert into platform_entitlements.entitlement_snapshot (tenant_id, snapshot_version, issued_at, valid_until, key_id, modules, features, limits, scopes, signature) values ('{tenant.Value}', 1, '{issued:O}', '{validUntil:O}', 'current', '[\"sample\"]'::jsonb, '[]'::jsonb, '{{}}'::jsonb, '{{}}'::jsonb, '{signature}'); reset app.tenant_id; reset role;");

            var created = await PostAsync(client, "/api/v1/sample/items", new { name = "after-entitlement" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenant.Value}'; delete from sample.sample_projection where tenant_id = '{tenant.Value}'; delete from sample.sample_item where tenant_id = '{tenant.Value}'; delete from platform_messaging.outbox_message where tenant_id = '{tenant.Value}'; delete from platform_audit.audit_entry where tenant_id = '{tenant.Value}'; delete from platform_entitlements.entitlement_snapshot where tenant_id = '{tenant.Value}'; delete from platform_authz.membership_role_assignment where tenant_id = '{tenant.Value}'; delete from platform_authz.role_permission where tenant_id = '{tenant.Value}'; delete from platform_authz.role where tenant_id = '{tenant.Value}'; delete from platform_iam.membership where tenant_id = '{tenant.Value}'; delete from platform_iam.identity where id = '{identity.Value}'; delete from platform_tenancy.tenant where id = '{tenant.Value}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var response = await PostAsync(client, "/api/v1/auth/login", new { email, password, tenantId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object? body)
    {
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfResponse>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body ?? new { }) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await client.SendAsync(request);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string CreateEntitlementSignature(TenantId tenant, DateTimeOffset issued, DateTimeOffset validUntil, string key)
    {
        var canonical = JsonSerializer.Serialize(new { tenant = tenant.Value, snapshot = 1, issued, valid = validUntil, modules = SampleModuleIds, features = EmptyFeatures, limits = new Dictionary<string, long>() });
        return Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record CsrfResponse(string Token);
    private sealed record SessionResponse(bool Authenticated, Guid TenantId);
    private sealed record MembershipInvitation(Guid MembershipId, string Token, DateTimeOffset ExpiresAt);
}
