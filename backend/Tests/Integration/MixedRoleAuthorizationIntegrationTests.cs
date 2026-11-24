using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Core.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class MixedRoleAuthorizationIntegrationTests : DatabaseIntegrationTestBase
{
    [Fact]
    public async Task HttpAuthorizationUsesCurrentActiveRoleStateWithoutLogout()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the mixed-role authorization gate.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for the mixed-role authorization gate.");
        var originalAppPassword = new NpgsqlConnectionStringBuilder(runtimeConnectionString).Password;
        if (string.IsNullOrWhiteSpace(originalAppPassword)) throw new InvalidOperationException("TODIR_DATABASE_CONNECTION must include the runtime password for restoration.");

        var appPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var tenantId = TenantId.New();
        var identityId = IdentityId.New();
        var membershipId = MembershipId.New();
        var limitedRoleId = Guid.CreateVersion7();
        var sensitiveRoleId = Guid.CreateVersion7();
        var email = $"mixed-role-{identityId.Value:N}@mixed-{tenantId.Value:N}.local";
        var password = "MixedRoleAuthorizationPassword123!";
        var passwordHash = IdentityConventions.HashPassword(password);

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $@"
            alter role erp_app password '{appPassword}';
            set role erp_owner;
            set app.tenant_id = '{tenantId.Value}';
            insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version)
            values ('{tenantId.Value}', 'mixed-{tenantId.Value:N}', 'Mixed Role Tenant', 'Active', 'mixed-{tenantId.Value:N}.local', 'mixed-{tenantId.Value:N}.local', now(), 1);
            insert into platform.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, password_hash, status, created_at, security_stamp, version)
            values ('{identityId.Value}', '{tenantId.Value}', '{email}', '{email.ToUpperInvariant()}', 'mixed-role-{identityId.Value:N}', 'Mixed Role User', true, '{passwordHash}', 'Active', now(), '{Guid.CreateVersion7()}', 1);
            insert into platform.membership (id, tenant_id, identity_id, display_name, status, created_at, version)
            values ('{membershipId.Value}', '{tenantId.Value}', '{identityId.Value}', 'Mixed Role Member', 'Active', now(), 1);
            insert into platform.role (id, tenant_id, code, name, is_system_template, is_active, version)
            values
                ('{limitedRoleId}', '{tenantId.Value}', 'mixed-reader', 'Mixed Reader', false, true, 1),
                ('{sensitiveRoleId}', '{tenantId.Value}', 'mixed-sensitive', 'Mixed Sensitive', false, false, 1);
            insert into platform.role_permission (tenant_id, role_id, permission_code)
            values
                ('{tenantId.Value}', '{limitedRoleId}', 'platform.identity.read'),
                ('{tenantId.Value}', '{sensitiveRoleId}', 'platform.identity.manage');
            insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type)
            values
                ('{tenantId.Value}', '{membershipId.Value}', '{limitedRoleId}', 'Tenant'),
                ('{tenantId.Value}', '{membershipId.Value}', '{sensitiveRoleId}', 'Tenant');
            reset app.tenant_id;
            reset role;");

        try
        {
            var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Username = "erp_app",
                Password = appPassword
            }.ConnectionString;
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", appConnectionString)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            await LoginAsync(client, email, password, tenantId.Value);

            var deniedWhileInactive = await PostWithCsrfAsync(client, $"/api/v1/tenants/{tenantId.Value:D}/users", new
            {
                name = "Denied User",
                emailPrefix = $"denied-{Guid.CreateVersion7():N}",
                roleIds = Array.Empty<Guid>()
            });
            Assert.Equal(HttpStatusCode.Forbidden, deniedWhileInactive.StatusCode);

            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; update platform.role set is_active = true, version = version + 1 where tenant_id = '{tenantId.Value}' and id = '{sensitiveRoleId}'; reset app.tenant_id; reset role;");
            var allowedAfterActivation = await PostWithCsrfAsync(client, $"/api/v1/tenants/{tenantId.Value:D}/users", new
            {
                name = "Allowed User",
                emailPrefix = $"allowed-{Guid.CreateVersion7():N}",
                roleIds = Array.Empty<Guid>()
            });
            Assert.Equal(HttpStatusCode.Created, allowedAfterActivation.StatusCode);

            var effectiveAccess = await client.GetAsync("/api/v1/authorization/effective-access");
            Assert.Equal(HttpStatusCode.OK, effectiveAccess.StatusCode);
            var effectivePermissions = (await effectiveAccess.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Contains("platform.identity.manage", effectivePermissions);

            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; update platform.role set is_active = false, version = version + 1 where tenant_id = '{tenantId.Value}' and id = '{sensitiveRoleId}'; reset app.tenant_id; reset role;");
            var deniedAfterDeactivation = await PostWithCsrfAsync(client, $"/api/v1/tenants/{tenantId.Value:D}/users", new
            {
                name = "Denied Again",
                emailPrefix = $"denied-again-{Guid.CreateVersion7():N}",
                roleIds = Array.Empty<Guid>()
            });
            Assert.Equal(HttpStatusCode.Forbidden, deniedAfterDeactivation.StatusCode);
        }
        finally
        {
            await ExecuteAsync(admin, $@"
                set role erp_owner;
                set app.tenant_id = '{tenantId.Value}';
                delete from platform.audit_entry where tenant_id = '{tenantId.Value}';
                delete from platform.membership_role_assignment where tenant_id = '{tenantId.Value}';
                delete from platform.role_permission where tenant_id = '{tenantId.Value}';
                delete from platform.role where tenant_id = '{tenantId.Value}';
                delete from platform.membership where tenant_id = '{tenantId.Value}';
                delete from platform.identity where tenant_id = '{tenantId.Value}';
                delete from platform.tenant where id = '{tenantId.Value}';
                reset app.tenant_id;
                reset role;");
            await ExecuteAsync(admin, $"alter role erp_app password '{originalAppPassword.Replace("'", "''", StringComparison.Ordinal)}';");
        }
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var response = await PostWithCsrfAsync(client, "/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Authenticated", body.GetProperty("state").GetString());
        Assert.Equal(tenantId, body.GetProperty("tenantId").GetGuid());
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object body)
    {
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfResponse>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await client.SendAsync(request);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record CsrfResponse(string Token);
}
