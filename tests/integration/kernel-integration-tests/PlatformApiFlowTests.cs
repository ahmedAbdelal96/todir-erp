using System.Net;
using System.Net.Http.Json;
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
        var adminEmail = $"api-admin-{administrator.Value:N}@example.com";
        var invitedEmail = $"api-invited-{invitedIdentity.Value:N}@example.com";
        var password = "PlatformApiFlow-Only-For-CI";
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = appPassword }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var adminHash = IdentityConventions.HashPassword(password);
        await ExecuteAsync(admin, $"alter role erp_app password '{appPassword}'; set role erp_owner; insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('{tenantA.Value}', 'api-flow-a-{tenantA.Value:N}', 'API Flow A', 'Active', now(), 1), ('{tenantB.Value}', 'api-flow-b-{tenantB.Value:N}', 'API Flow B', 'Active', now(), 1); insert into platform_iam.identity (id, normalized_email, email_verified, password_hash, status, created_at, security_stamp, version) values ('{administrator.Value}', '{adminEmail.ToUpperInvariant()}', true, '{adminHash}', 'Active', now(), '{Guid.CreateVersion7()}', 1), ('{invitedIdentity.Value}', '{invitedEmail.ToUpperInvariant()}', true, '{adminHash}', 'Active', now(), '{Guid.CreateVersion7()}', 1); insert into platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('{administratorMembershipA.Value}', '{tenantA.Value}', '{administrator.Value}', 'Platform administrator A', 'Active', now(), 1), ('{administratorMembershipB.Value}', '{tenantB.Value}', '{administrator.Value}', 'Platform administrator B', 'Active', now(), 1), ('{invitedMembershipB.Value}', '{tenantB.Value}', '{invitedIdentity.Value}', 'Invited member B', 'Active', now(), 1); insert into platform_authz.role (id, tenant_id, code, name, is_system_template, is_active, version) values ('{roleId}', '{tenantA.Value}', 'tenant-admin', 'Tenant administrator', false, true, 1); insert into platform_authz.role_permission (tenant_id, role_id, permission_code) values ('{tenantA.Value}', '{roleId}', 'platform.membership.manage'); insert into platform_authz.membership_role_assignment (tenant_id, membership_id, role_id, scope_type) values ('{tenantA.Value}', '{administratorMembershipA.Value}', '{roleId}', 'Tenant'); reset role;");

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", appConnectionString)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
            using var administratorClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            using var invitedClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });

            await LoginAsync(administratorClient, adminEmail, password, tenantA.Value);
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
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantA.Value}'; delete from platform_iam.membership_invitation where tenant_id = '{tenantA.Value}'; delete from platform_authz.membership_role_assignment where tenant_id = '{tenantA.Value}'; delete from platform_authz.role_permission where tenant_id = '{tenantA.Value}'; delete from platform_authz.role where tenant_id = '{tenantA.Value}'; delete from platform_iam.membership where tenant_id in ('{tenantA.Value}', '{tenantB.Value}'); delete from platform_iam.identity where id in ('{administrator.Value}', '{invitedIdentity.Value}'); delete from platform_tenancy.tenant where id in ('{tenantA.Value}', '{tenantB.Value}'); reset app.tenant_id; reset role;");
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

    private sealed record CsrfResponse(string Token);
    private sealed record SessionResponse(bool Authenticated, Guid TenantId);
    private sealed record MembershipInvitation(Guid MembershipId, string Token, DateTimeOffset ExpiresAt);
}
