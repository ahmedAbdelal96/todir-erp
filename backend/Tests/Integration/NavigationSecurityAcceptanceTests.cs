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
public sealed class NavigationSecurityAcceptanceTests : DatabaseIntegrationTestBase
{
    private static readonly object[] EmptyLines = [];

    [Fact]
    public async Task NavigationSecurityMatrixEnforcesAllAcceptanceScenarios()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for navigation security acceptance tests.");

        var appPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var tenant = TenantId.New();
        var identityId = IdentityId.New();
        var membershipId = MembershipId.New();

        var entityA = Guid.CreateVersion7();
        var entityB = Guid.CreateVersion7();
        var branchA1 = Guid.CreateVersion7();
        var branchA2 = Guid.CreateVersion7();

        var roleCashier = Guid.CreateVersion7();
        var roleCrmRep = Guid.CreateVersion7();
        var roleCrmMgr = Guid.CreateVersion7();
        var roleInvClerk = Guid.CreateVersion7();
        var roleInvSupervisor = Guid.CreateVersion7();
        var roleLedgerClerk = Guid.CreateVersion7();
        var roleLedgerMgr = Guid.CreateVersion7();
        var roleAccountingClose = Guid.CreateVersion7();
        var roleBranchA1Only = Guid.CreateVersion7();
        var roleInactiveSuper = Guid.CreateVersion7();

        var email = $"nav-sec-{identityId.Value:N}@sec-nav-{tenant.Value:N}.local";
        var password = "NavSecurityPassword123!";
        var passwordHash = IdentityConventions.HashPassword(password);
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "erp_app",
            Password = appPassword
        }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();

        var setupSql = $@"
            alter role erp_app password '{appPassword}';
            set role erp_owner;
            set app.tenant_id = '{tenant.Value}';

            insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version)
            values ('{tenant.Value}', 'sec-nav-{tenant.Value:N}', 'Nav Sec Tenant', 'Active', 'sec-nav-{tenant.Value:N}.local', 'sec-nav-{tenant.Value:N}.local', now(), 1);

            insert into platform.legal_entity (id, tenant_id, code, legal_name, country_code, base_currency_code, timezone, locale, status, created_at, version)
            values
                ('{entityA}', '{tenant.Value}', 'NAV_A', 'Nav Entity A', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1),
                ('{entityB}', '{tenant.Value}', 'NAV_B', 'Nav Entity B', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1);

            insert into platform.branch (id, tenant_id, legal_entity_id, code, name, status, created_at, version)
            values
                ('{branchA1}', '{tenant.Value}', '{entityA}', 'BR_A1', 'Branch A1', 'Active', now(), 1),
                ('{branchA2}', '{tenant.Value}', '{entityA}', 'BR_A2', 'Branch A2', 'Active', now(), 1);

            insert into platform.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, password_hash, status, created_at, security_stamp, version)
            values ('{identityId.Value}', '{tenant.Value}', '{email}', '{email.ToUpperInvariant()}', 'nav-sec-{identityId.Value:N}', 'Nav Sec User', true, '{passwordHash}', 'Active', now(), '{Guid.CreateVersion7()}', 1);

            insert into platform.membership (id, tenant_id, identity_id, display_name, status, created_at, version)
            values ('{membershipId.Value}', '{tenant.Value}', '{identityId.Value}', 'Nav Sec Member', 'Active', now(), 1);

            -- Roles
            insert into platform.role (id, tenant_id, code, name, is_system_template, is_active, version)
            values
                ('{roleCashier}', '{tenant.Value}', 'cashier', 'Cashier', false, true, 1),
                ('{roleCrmRep}', '{tenant.Value}', 'crm-rep', 'CRM Rep', false, true, 1),
                ('{roleCrmMgr}', '{tenant.Value}', 'crm-mgr', 'CRM Manager', false, true, 1),
                ('{roleInvClerk}', '{tenant.Value}', 'inv-clerk', 'Inventory Clerk', false, true, 1),
                ('{roleInvSupervisor}', '{tenant.Value}', 'inv-sup', 'Inventory Supervisor', false, true, 1),
                ('{roleLedgerClerk}', '{tenant.Value}', 'ledger-clerk', 'Ledger Clerk', false, true, 1),
                ('{roleLedgerMgr}', '{tenant.Value}', 'ledger-mgr', 'Ledger Manager', false, true, 1),
                ('{roleAccountingClose}', '{tenant.Value}', 'acct-close', 'Accounting Close Only', false, true, 1),
                ('{roleBranchA1Only}', '{tenant.Value}', 'branch-a1-role', 'Branch A1 Scoped', false, true, 1),
                ('{roleInactiveSuper}', '{tenant.Value}', 'inactive-super', 'Inactive Super', false, false, 1);

            -- Role Permissions
            insert into platform.role_permission (tenant_id, role_id, permission_code)
            values
                ('{tenant.Value}', '{roleCashier}', 'banking.cash.count'),
                ('{tenant.Value}', '{roleCrmRep}', 'crm.lead.read'),
                ('{tenant.Value}', '{roleCrmRep}', 'crm.lead.create'),
                ('{tenant.Value}', '{roleCrmMgr}', 'crm.team.manage'),
                ('{tenant.Value}', '{roleInvClerk}', 'inventory.count.create'),
                ('{tenant.Value}', '{roleInvClerk}', 'inventory.count.enter'),
                ('{tenant.Value}', '{roleInvSupervisor}', 'inventory.count.post'),
                ('{tenant.Value}', '{roleInvSupervisor}', 'inventory.adjustment.post'),
                ('{tenant.Value}', '{roleLedgerClerk}', 'ledger.general_journal.create'),
                ('{tenant.Value}', '{roleLedgerMgr}', 'ledger.general_journal.post'),
                ('{tenant.Value}', '{roleAccountingClose}', 'accounting.close.execute'),
                ('{tenant.Value}', '{roleBranchA1Only}', 'inventory.stock.read'),
                ('{tenant.Value}', '{roleInactiveSuper}', '*');

            -- Membership Role Assignments
            insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type, scope_id)
            values
                ('{tenant.Value}', '{membershipId.Value}', '{roleCashier}', 'LegalEntity', '{entityA}'),
                ('{tenant.Value}', '{membershipId.Value}', '{roleCrmRep}', 'LegalEntity', '{entityA}'),
                ('{tenant.Value}', '{membershipId.Value}', '{roleInvClerk}', 'LegalEntity', '{entityA}'),
                ('{tenant.Value}', '{membershipId.Value}', '{roleLedgerClerk}', 'LegalEntity', '{entityA}'),
                ('{tenant.Value}', '{membershipId.Value}', '{roleAccountingClose}', 'LegalEntity', '{entityA}'),
                ('{tenant.Value}', '{membershipId.Value}', '{roleBranchA1Only}', 'Branch', '{branchA1}');

            -- Inactive role assigned at Tenant scope
            insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type)
            values ('{tenant.Value}', '{membershipId.Value}', '{roleInactiveSuper}', 'Tenant');

            -- Module instances: Inventory and Ledger start Active for Entity A
            insert into platform.module_instance (id, tenant_id, module_id, scope_type, scope_id, status, manifest_version, version)
            values
                ('{Guid.CreateVersion7()}', '{tenant.Value}', 'banking', 'legal_entity', '{entityA}', 'Active', '1.0.0', 1),
                ('{Guid.CreateVersion7()}', '{tenant.Value}', 'crm', 'legal_entity', '{entityA}', 'Active', '1.0.0', 1),
                ('{Guid.CreateVersion7()}', '{tenant.Value}', 'inventory', 'legal_entity', '{entityA}', 'Active', '1.0.0', 1),
                ('{Guid.CreateVersion7()}', '{tenant.Value}', 'ledger', 'legal_entity', '{entityA}', 'Active', '1.0.0', 1);

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

            // 1. Anonymous Access Denied -> 401
            var unauthRes = await client.GetAsync("/api/v1/authorization/effective-access");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthRes.StatusCode);

            // Login
            await LoginAsync(client, email, password, tenant.Value);

            // 2. Inactive role does not grant super or '*' permission
            var resEntityA = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={entityA}&branchId={branchA1}");
            Assert.Equal(HttpStatusCode.OK, resEntityA.StatusCode);
            var docA = await resEntityA.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(docA.GetProperty("isSuper").GetBoolean(), "Inactive role must not grant isSuper");

            var permsA = docA.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToHashSet();
            Assert.DoesNotContain("*", permsA);

            // 3. Cashier permissions: has banking.cash.count, lacks banking.transfers.initiate and banking.reconciliation.match
            Assert.Contains("banking.cash.count", permsA);
            Assert.DoesNotContain("banking.transfers.initiate", permsA);
            Assert.DoesNotContain("banking.reconciliation.match", permsA);

            // 4. CRM Rep vs Manager: has lead.read and lead.create, lacks team.manage
            Assert.Contains("crm.lead.read", permsA);
            Assert.Contains("crm.lead.create", permsA);
            Assert.DoesNotContain("crm.team.manage", permsA);

            // 5. Inventory Clerk vs Supervisor: has count.create/enter, lacks count.post and adjustment.post
            Assert.Contains("inventory.count.create", permsA);
            Assert.Contains("inventory.count.enter", permsA);
            Assert.DoesNotContain("inventory.count.post", permsA);
            Assert.DoesNotContain("inventory.adjustment.post", permsA);

            // 6. Ledger Clerk vs Manager: has general_journal.create, lacks general_journal.post
            Assert.Contains("ledger.general_journal.create", permsA);
            Assert.DoesNotContain("ledger.general_journal.post", permsA);

            // 7. Branch scoping: Branch A1 has inventory.stock.read
            Assert.Contains("inventory.stock.read", permsA);

            // When querying Branch A2: inventory.stock.read must NOT be present (scoped to Branch A1)
            var resBranchA2 = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={entityA}&branchId={branchA2}");
            Assert.Equal(HttpStatusCode.OK, resBranchA2.StatusCode);
            var docBranchA2 = await resBranchA2.Content.ReadFromJsonAsync<JsonElement>();
            var permsBranchA2 = docBranchA2.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToHashSet();
            Assert.DoesNotContain("inventory.stock.read", permsBranchA2);

            // 8. Legal Entity scoping: Entity B does NOT inherit Entity A permissions
            var resEntityB = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={entityB}");
            Assert.Equal(HttpStatusCode.OK, resEntityB.StatusCode);
            var docB = await resEntityB.Content.ReadFromJsonAsync<JsonElement>();
            var permsB = docB.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToHashSet();
            Assert.DoesNotContain("banking.cash.count", permsB);
            Assert.DoesNotContain("crm.lead.read", permsB);
            Assert.DoesNotContain("inventory.count.create", permsB);

            // 9. Accounting Close without commercial entitlement:
            // User holds accounting.close.execute, but tenant has no entitlement snapshot for accounting module
            var entitledA = docA.GetProperty("entitledModules").EnumerateArray().Select(m => m.GetString()!).ToHashSet();
            Assert.DoesNotContain("accounting", entitledA);

            // Accounting close API endpoint must return 403 Forbidden due to lack of entitlement
            var closeRes = await PostAsync(client, $"/api/v1/accounting/legal-entities/{entityA}/close", new
            {
                periodId = Guid.CreateVersion7(),
                idempotencyKey = $"test-{Guid.NewGuid():N}"
            });
            Assert.Equal(HttpStatusCode.Forbidden, closeRes.StatusCode);

            // 10. Update module instance to ReadOnly for Inventory and Ledger
            var updateToReadOnlySql = $@"
                set role erp_owner;
                set app.tenant_id = '{tenant.Value}';
                update platform.module_instance set status = 'ReadOnly' where tenant_id = '{tenant.Value}' and module_id in ('inventory', 'ledger');
                reset app.tenant_id;
                reset role;
            ";
            await ExecuteAsync(admin, updateToReadOnlySql);

            // Effective access endpoint must reflect ReadOnly lifecycle
            var resReadOnly = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={entityA}");
            Assert.Equal(HttpStatusCode.OK, resReadOnly.StatusCode);
            var docReadOnly = await resReadOnly.Content.ReadFromJsonAsync<JsonElement>();
            var statuses = docReadOnly.GetProperty("moduleStatuses");
            Assert.Equal("ReadOnly", statuses.GetProperty("inventory").GetString());
            Assert.Equal("ReadOnly", statuses.GetProperty("ledger").GetString());

            // 11. Backend Mutation Rejection in ReadOnly:
            // Inventory adjustment mutation endpoint rejected with 403 Forbidden
            var adjRes = await PostAsync(client, $"/api/v1/inventory/legal-entities/{entityA}/adjustments", new
            {
                warehouseId = Guid.CreateVersion7(),
                productVariantId = Guid.CreateVersion7(),
                quantity = 1,
                direction = "Increase",
                reason = "CountDifference",
                idempotencyKey = $"adj-{Guid.NewGuid():N}"
            });
            Assert.Equal(HttpStatusCode.Forbidden, adjRes.StatusCode);

            // Inventory count posting endpoint rejected with 403 Forbidden
            var countPostRes = await PostAsync(client, $"/api/v1/inventory/legal-entities/{entityA}/counts/{Guid.CreateVersion7()}/post", new
            {
                idempotencyKey = $"post-{Guid.NewGuid():N}"
            });
            Assert.Equal(HttpStatusCode.Forbidden, countPostRes.StatusCode);

            // Ledger journal creation endpoint rejected with 403 Forbidden
            var ledgerDraftRes = await PostAsync(client, $"/api/v1/ledger/legal-entities/{entityA}/general-journals", new
            {
                entryNumber = "GJ-TEST-001",
                postingDate = DateOnly.FromDateTime(DateTime.UtcNow),
                lines = EmptyLines
            });
            Assert.Equal(HttpStatusCode.Forbidden, ledgerDraftRes.StatusCode);

            // 12. Update module instance to WindDown for Ledger
            var updateToWindDownSql = $@"
                set role erp_owner;
                set app.tenant_id = '{tenant.Value}';
                update platform.module_instance set status = 'WindDown' where tenant_id = '{tenant.Value}' and module_id = 'ledger';
                reset app.tenant_id;
                reset role;
            ";
            await ExecuteAsync(admin, updateToWindDownSql);

            // Ledger journal submission also rejected under WindDown
            var ledgerSubmitRes = await PostAsync(client, $"/api/v1/ledger/legal-entities/{entityA}/general-journals/{Guid.CreateVersion7()}/submit", new { });
            Assert.Equal(HttpStatusCode.Forbidden, ledgerSubmitRes.StatusCode);

            // 13. Suspend Membership: user must be immediately rejected
            var suspendMembershipSql = $@"
                set role erp_owner;
                set app.tenant_id = '{tenant.Value}';
                update platform.membership set status = 'Suspended' where tenant_id = '{tenant.Value}' and id = '{membershipId.Value}';
                reset app.tenant_id;
                reset role;
            ";
            await ExecuteAsync(admin, suspendMembershipSql);

            var resSuspended = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={entityA}");
            // Either 403 Forbidden or empty/nullified access
            Assert.True(resSuspended.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized ||
                (resSuspended.IsSuccessStatusCode && (await resSuspended.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("permissions").GetArrayLength() == 0));
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
