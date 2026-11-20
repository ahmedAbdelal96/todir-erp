using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Core.Identity;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class EffectiveAccessSecurityIntegrationTests : DatabaseIntegrationTestBase
{
    [Fact]
    public async Task EffectiveAccessEndpointEnforcesAuthenticationScopeEntitlementAndLifecycle()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the effective access security test.");

        var appPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var tenant = TenantId.New();
        var identityId = IdentityId.New();
        var membershipId = MembershipId.New();
        var entityA = Guid.CreateVersion7();
        var entityB = Guid.CreateVersion7();
        var branchA = Guid.CreateVersion7();

        var roleEntityA = Guid.CreateVersion7();
        var roleBranchA = Guid.CreateVersion7();
        var roleInactiveAdmin = Guid.CreateVersion7();

        var email = $"sec-user-{identityId.Value:N}@sec-{tenant.Value:N}.local";
        var password = "SecurityAccessPassword123!";
        var passwordHash = IdentityConventions.HashPassword(password);
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "erp_app",
            Password = appPassword
        }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();

        // Setup test tenant, entities, branch, identity, membership, and roles
        var setupSql = $@"
            alter role erp_app password '{appPassword}';
            set role erp_owner;
            set app.tenant_id = '{tenant.Value}';

            insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version)
            values ('{tenant.Value}', 'sec-{tenant.Value:N}', 'Security Tenant', 'Active', 'sec-{tenant.Value:N}.local', 'sec-{tenant.Value:N}.local', now(), 1);

            insert into platform.legal_entity (id, tenant_id, code, legal_name, country_code, base_currency_code, timezone, locale, status, created_at, version)
            values
                ('{entityA}', '{tenant.Value}', 'ENT_A', 'Entity A', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1),
                ('{entityB}', '{tenant.Value}', 'ENT_B', 'Entity B', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1);

            insert into platform.branch (id, tenant_id, legal_entity_id, code, name, status, created_at, version)
            values ('{branchA}', '{tenant.Value}', '{entityA}', 'BR_A', 'Branch A', 'Active', now(), 1);

            insert into platform.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, password_hash, status, created_at, security_stamp, version)
            values ('{identityId.Value}', '{tenant.Value}', '{email}', '{email.ToUpperInvariant()}', 'sec-user-{identityId.Value:N}', 'Security User', true, '{passwordHash}', 'Active', now(), '{Guid.CreateVersion7()}', 1);

            insert into platform.membership (id, tenant_id, identity_id, display_name, status, created_at, version)
            values ('{membershipId.Value}', '{tenant.Value}', '{identityId.Value}', 'Security Member', 'Active', now(), 1);

            -- Role 1: Active role scoped to Entity A with Banking permissions
            insert into platform.role (id, tenant_id, code, name, is_system_template, is_active, version)
            values ('{roleEntityA}', '{tenant.Value}', 'entity-a-banking', 'Entity A Banking', false, true, 1);

            insert into platform.role_permission (tenant_id, role_id, permission_code)
            values
                ('{tenant.Value}', '{roleEntityA}', 'banking.accounts.read'),
                ('{tenant.Value}', '{roleEntityA}', 'banking.statements.read'),
                ('{tenant.Value}', '{roleEntityA}', 'banking.statements.import'),
                ('{tenant.Value}', '{roleEntityA}', 'banking.reconciliation.match'),
                ('{tenant.Value}', '{roleEntityA}', 'banking.cash.count');

            insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type, scope_id)
            values ('{tenant.Value}', '{membershipId.Value}', '{roleEntityA}', 'LegalEntity', '{entityA}');

            -- Role 2: Active role scoped to Branch A with Transfers permission
            insert into platform.role (id, tenant_id, code, name, is_system_template, is_active, version)
            values ('{roleBranchA}', '{tenant.Value}', 'branch-a-transfers', 'Branch A Transfers', false, true, 1);

            insert into platform.role_permission (tenant_id, role_id, permission_code)
            values ('{tenant.Value}', '{roleBranchA}', 'banking.transfers.initiate');

            insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type, scope_id)
            values ('{tenant.Value}', '{membershipId.Value}', '{roleBranchA}', 'Branch', '{branchA}');

            -- Role 3: INACTIVE admin role scoped to Tenant
            insert into platform.role (id, tenant_id, code, name, is_system_template, is_active, version)
            values ('{roleInactiveAdmin}', '{tenant.Value}', 'admin', 'Inactive Admin', false, false, 1);

            insert into platform.role_permission (tenant_id, role_id, permission_code)
            values ('{tenant.Value}', '{roleInactiveAdmin}', '*');

            insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type)
            values ('{tenant.Value}', '{membershipId.Value}', '{roleInactiveAdmin}', 'Tenant');

            -- Module instances: POS is Disabled for Entity A, Inventory is ReadOnly for Entity A
            insert into platform.module_instance (id, tenant_id, module_id, scope_type, scope_id, status, manifest_version, version)
            values
                ('{Guid.CreateVersion7()}', '{tenant.Value}', 'pos', 'legal_entity', '{entityA}', 'Disabled', '1.0.0', 1),
                ('{Guid.CreateVersion7()}', '{tenant.Value}', 'inventory', 'legal_entity', '{entityA}', 'ReadOnly', '1.0.0', 1);

            reset app.tenant_id;
            reset role;
        ";

        await ExecuteAsync(admin, setupSql);

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseEnvironment("Development")
                    .UseSetting("TODIR_DATABASE_CONNECTION", appConnectionString)
                    .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            // 1. Unauthenticated request must return 401 Unauthorized
            var unauthRes = await client.GetAsync("/api/v1/authorization/effective-access");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthRes.StatusCode);

            // Login as security test user
            await LoginAsync(client, email, password, tenant.Value);

            // 2. Query effective-access for Entity A (with branch)
            var resEntityA = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={entityA}&branchId={branchA}");
            Assert.Equal(HttpStatusCode.OK, resEntityA.StatusCode);
            var bodyEntityA = await resEntityA.Content.ReadFromJsonAsync<JsonElement>();

            // Assert tenant, membership, scope
            Assert.Equal(tenant.Value.ToString(), bodyEntityA.GetProperty("tenantId").GetString());
            Assert.Equal(membershipId.Value.ToString(), bodyEntityA.GetProperty("membershipId").GetString());
            Assert.Equal(entityA.ToString(), bodyEntityA.GetProperty("legalEntityId").GetString());
            Assert.Equal(branchA.ToString(), bodyEntityA.GetProperty("branchId").GetString());

            // 3. Inactive admin role must NOT make user isSuper!
            Assert.False(bodyEntityA.GetProperty("isSuper").GetBoolean(), "Inactive admin role must NOT grant isSuper");

            // 4. Scoped permissions check:
            var permissionsEntityA = bodyEntityA.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToList();
            Assert.Contains("banking.accounts.read", permissionsEntityA);
            Assert.Contains("banking.statements.read", permissionsEntityA);
            Assert.Contains("banking.statements.import", permissionsEntityA);
            Assert.Contains("banking.reconciliation.match", permissionsEntityA);
            Assert.Contains("banking.cash.count", permissionsEntityA);
            Assert.Contains("banking.transfers.initiate", permissionsEntityA);
            Assert.DoesNotContain("*", permissionsEntityA); // Inactive admin role permission must NOT be present!

            // 5. Query effective-access for Entity B (Entity A's role must NOT leak into Entity B!)
            var resEntityB = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={entityB}");
            Assert.Equal(HttpStatusCode.OK, resEntityB.StatusCode);
            var bodyEntityB = await resEntityB.Content.ReadFromJsonAsync<JsonElement>();

            var permissionsEntityB = bodyEntityB.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToList();
            Assert.DoesNotContain("banking.accounts.read", permissionsEntityB);
            Assert.DoesNotContain("banking.transfers.initiate", permissionsEntityB);
            Assert.False(bodyEntityB.GetProperty("isSuper").GetBoolean());

            // 6. Entitlement check: Core non-purchasable modules & system surfaces must be entitled
            var entitledModules = bodyEntityA.GetProperty("entitledModules").EnumerateArray().Select(m => m.GetString()!).ToList();
            Assert.Contains("banking", entitledModules);
            Assert.Contains("crm", entitledModules);
            Assert.Contains("directory", entitledModules);
            Assert.Contains("home", entitledModules);
            Assert.Contains("settings", entitledModules);

            // Purchasable modules without entitlement snapshot must NOT be in entitledModules
            Assert.DoesNotContain("payroll", entitledModules);
            Assert.DoesNotContain("manufacturing", entitledModules);
            Assert.DoesNotContain("fixed-assets", entitledModules);

            // 7. Lifecycle status check:
            var moduleStatuses = bodyEntityA.GetProperty("moduleStatuses");
            Assert.Equal("Disabled", moduleStatuses.GetProperty("pos").GetString());
            Assert.Equal("ReadOnly", moduleStatuses.GetProperty("inventory").GetString());
            Assert.Equal("Active", moduleStatuses.GetProperty("banking").GetString());
        }
        finally
        {
            var cleanupSql = $@"
                set role erp_owner;
                set app.tenant_id = '{tenant.Value}';
                delete from platform.module_instance where tenant_id = '{tenant.Value}';
                delete from platform.membership_role_assignment where tenant_id = '{tenant.Value}';
                delete from platform.role_permission where tenant_id = '{tenant.Value}';
                delete from platform.role where tenant_id = '{tenant.Value}';
                delete from platform.membership where tenant_id = '{tenant.Value}';
                delete from platform.identity where tenant_id = '{tenant.Value}';
                delete from platform.branch where tenant_id = '{tenant.Value}';
                delete from platform.legal_entity where tenant_id = '{tenant.Value}';
                delete from platform.tenant where id = '{tenant.Value}';
                reset app.tenant_id;
                reset role;
            ";
            await ExecuteAsync(admin, cleanupSql);
        }
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var response = await PostAsync(client, "/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authentication = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Authenticated", authentication.GetProperty("state").GetString());
        Assert.Equal(tenantId, authentication.GetProperty("tenantId").GetGuid());
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
}
