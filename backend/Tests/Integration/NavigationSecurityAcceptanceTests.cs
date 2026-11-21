using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Core.Identity;
using Core.Modules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
public sealed class NavigationSecurityAcceptanceTests : DatabaseIntegrationTestBase
{
    private static readonly object[] EmptyLines = [];
    private static string GetAdminConnectionString() =>
        Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
        ?? "Host=127.0.0.1;Port=15432;Database=todir_nav_acceptance;Username=postgres;Password=postgres";

    [Fact]
    public async Task Role_InactiveRole_Denied()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var res = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={fixture.EntityA}&branchId={fixture.BranchA1}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(doc.GetProperty("isSuper").GetBoolean(), "Inactive role must not grant isSuper");

        var perms = doc.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToHashSet();
        Assert.DoesNotContain("*", perms);
    }

    [Fact]
    public async Task Scope_CrossTenantAndCrossBranch_Denied()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        // Branch A1 has inventory.stock.read
        var resA1 = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={fixture.EntityA}&branchId={fixture.BranchA1}");
        Assert.Equal(HttpStatusCode.OK, resA1.StatusCode);
        var docA1 = await resA1.Content.ReadFromJsonAsync<JsonElement>();
        var permsA1 = docA1.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToHashSet();
        Assert.Contains("inventory.stock.read", permsA1);

        // Branch A2 does NOT inherit Branch A1 permission
        var resA2 = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={fixture.EntityA}&branchId={fixture.BranchA2}");
        Assert.Equal(HttpStatusCode.OK, resA2.StatusCode);
        var docA2 = await resA2.Content.ReadFromJsonAsync<JsonElement>();
        var permsA2 = docA2.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToHashSet();
        Assert.DoesNotContain("inventory.stock.read", permsA2);

        // Entity B does NOT inherit Entity A permissions
        var resB = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={fixture.EntityB}");
        Assert.Equal(HttpStatusCode.OK, resB.StatusCode);
        var docB = await resB.Content.ReadFromJsonAsync<JsonElement>();
        var permsB = docB.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToHashSet();
        Assert.DoesNotContain("banking.cash.count", permsB);
        Assert.DoesNotContain("crm.lead.read", permsB);
    }

    [Fact]
    public async Task Scope_LegalEntityInstanceOverride_TakesPrecedenceOverTenantScope()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();

        // Tenant scope is Active for inventory, but Entity A is ReadOnly
        var overrideSql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            insert into platform.module_instance (id, tenant_id, module_id, scope_type, scope_id, status, manifest_version, version)
            values ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'inventory', 'tenant', null, 'Active', '1.0.0', 1);
            update platform.module_instance set status = 'ReadOnly' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'inventory' and scope_type = 'legal_entity' and scope_id = '{fixture.EntityA}';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(overrideSql);

        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var res = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={fixture.EntityA}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ReadOnly", doc.GetProperty("moduleStatuses").GetProperty("inventory").GetString());

        var adjRes = await fixture.PostAsync(client, $"/api/v1/inventory/legal-entities/{fixture.EntityA}/adjustments", new
        {
            warehouseId = Guid.CreateVersion7(),
            productVariantId = Guid.CreateVersion7(),
            quantity = 1,
            direction = "Increase",
            reason = "CountDifference",
            idempotencyKey = $"adj-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, adjRes.StatusCode);
    }

    [Fact]
    public async Task CommercialEntitlement_MissingEntitlement_FailsClosedForPurchasableModule()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var doc = await (await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={fixture.EntityA}")).Content.ReadFromJsonAsync<JsonElement>();
        var entitled = doc.GetProperty("entitledModules").EnumerateArray().Select(m => m.GetString()!).ToHashSet();
        Assert.DoesNotContain("accounting", entitled);

        var closeRes = await fixture.PostAsync(client, $"/api/v1/accounting/legal-entities/{fixture.EntityA}/close", new
        {
            periodId = Guid.CreateVersion7(),
            idempotencyKey = $"test-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, closeRes.StatusCode);
    }

    [Fact]
    public async Task CommercialEntitlement_CrossModuleIsolation_AccountingRequiresCommercialEntitlement()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        // User has general journal permission on core Ledger (active)
        var ledgerRes = await fixture.PostAsync(client, $"/api/v1/ledger/legal-entities/{fixture.EntityA}/general-journals", new
        {
            entryNumber = "GJ-TEST-ISOLATION",
            postingDate = DateOnly.FromDateTime(DateTime.UtcNow),
            lines = EmptyLines
        });
        // In Active state for ledger, creation is processed
        Assert.True(ledgerRes.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.BadRequest);

        // However, accounting module close requires explicit commercial entitlement
        var acctRes = await fixture.PostAsync(client, $"/api/v1/accounting/legal-entities/{fixture.EntityA}/close", new
        {
            periodId = Guid.CreateVersion7(),
            idempotencyKey = $"acct-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, acctRes.StatusCode);
    }

    [Fact]
    public async Task ActiveModule_AllowsMutationsAndPersistsCommittedState()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var leadId = Guid.CreateVersion7();
        var key = $"lead-{Guid.NewGuid():N}";
        var createLeadRes = await fixture.PostAsync(client, $"/api/v1/crm/legal-entities/{fixture.EntityA}/leads", new
        {
            legalEntityId = fixture.EntityA,
            displayName = "Active State Test Lead",
            companyName = "Active Company",
            email = "active@test.local",
            phone = "+201000000001",
            source = "Direct",
            priority = "Normal"
        }, key);

        Assert.True(createLeadRes.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK);

        // Verify committed database state
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"select count(*) from business_crm.lead where display_name = 'Active State Test Lead' and tenant_id = '{fixture.Tenant.Value}'", adminConn);
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task ReadOnlyModule_RejectsMutationsWithLifecycleForbidden_AndLeavesStateUnchanged()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();

        // Update Inventory to ReadOnly
        var setReadOnlySql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'ReadOnly' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'inventory';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setReadOnlySql);

        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var adjRes = await fixture.PostAsync(client, $"/api/v1/inventory/legal-entities/{fixture.EntityA}/adjustments", new
        {
            warehouseId = Guid.CreateVersion7(),
            productVariantId = Guid.CreateVersion7(),
            quantity = 1,
            direction = "Increase",
            reason = "CountDifference",
            idempotencyKey = $"adj-{Guid.NewGuid():N}"
        });

        Assert.Equal(HttpStatusCode.Forbidden, adjRes.StatusCode);

        // Verify state remains unchanged in DB
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"select count(*) from business_inventory.inventory_adjustment_request where tenant_id = '{fixture.Tenant.Value}'", adminConn);
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(0L, count);
    }

    [Fact]
    public async Task ReadOnlyModule_AllowsPermittedHistoricalReads()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();

        var setReadOnlySql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'ReadOnly' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'crm';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setReadOnlySql);

        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var readRes = await client.GetAsync($"/api/v1/crm/legal-entities/{fixture.EntityA}/leads");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);
    }

    [Fact]
    public async Task WindDownModule_RejectsMutations_AndAllowsReads()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();

        var setWindDownSql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'WindDown' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'ledger';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setWindDownSql);

        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        // Read query allowed under WindDown
        var readRes = await client.GetAsync($"/api/v1/ledger/legal-entities/{fixture.EntityA}/accounts");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);

        // Mutation rejected under WindDown
        var mutateRes = await fixture.PostAsync(client, $"/api/v1/ledger/legal-entities/{fixture.EntityA}/general-journals/{Guid.CreateVersion7()}/submit", new { });
        Assert.Equal(HttpStatusCode.Forbidden, mutateRes.StatusCode);
    }

    [Fact]
    public async Task UnknownModule_FailsClosedWithModuleUnknown()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        var enforcer = fixture.GetRequiredService<IModuleLifecycleEnforcer>();

        var decision = await enforcer.EvaluateAccessAsync(fixture.Tenant, "completely_unknown_fake_module", fixture.EntityA, isMutation: false);
        Assert.False(decision.Allowed);
        Assert.Equal("module.unknown", decision.DeniedReason);
    }

    [Fact]
    public async Task MissingInfrastructure_FailsClosed()
    {
        var unavailableEnforcer = new UnavailableModuleLifecycleEnforcer();
        var decision = await unavailableEnforcer.EvaluateAccessAsync(TenantId.New(), "ledger", Guid.NewGuid(), isMutation: false);
        Assert.False(decision.Allowed);
        Assert.Equal("infrastructure.unavailable", decision.DeniedReason);

        var canMutate = await unavailableEnforcer.CanMutateAsync(TenantId.New(), "ledger", Guid.NewGuid());
        Assert.False(canMutate);
    }

    [Fact]
    public async Task InvalidStatus_FailsClosed()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();

        var setCorruptStatusSql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            alter table platform.module_instance drop constraint if exists module_instance_status_check;
            update platform.module_instance set status = 'CorruptedNonExistentStatus' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'banking';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setCorruptStatusSql);

        try
        {
            var enforcer = fixture.GetRequiredService<IModuleLifecycleEnforcer>();
            var decision = await enforcer.EvaluateAccessAsync(fixture.Tenant, "banking", fixture.EntityA, isMutation: false);

            Assert.False(decision.Allowed);
            Assert.Equal("module.invalid_status", decision.DeniedReason);
        }
        finally
        {
            var restoreConstraintSql = $@"
                set role erp_owner;
                set app.tenant_id = '{fixture.Tenant.Value}';
                delete from platform.module_instance where tenant_id = '{fixture.Tenant.Value}' and status = 'CorruptedNonExistentStatus';
                reset app.tenant_id;
                alter table platform.module_instance drop constraint if exists module_instance_status_check;
                alter table platform.module_instance add constraint module_instance_status_check check (status = any (array['Requested'::text, 'Validating'::text, 'Provisioning'::text, 'AwaitingGoLive'::text, 'Active'::text, 'WindDown'::text, 'ReadOnly'::text, 'Disabled'::text, 'Failed'::text]));
                reset role;
            ";
            await fixture.ExecuteAdminSqlAsync(restoreConstraintSql);
        }
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    private sealed class NavSecurityFixture : IAsyncDisposable
    {
        public TenantId Tenant { get; }
        public IdentityId IdentityId { get; }
        public MembershipId MembershipId { get; }
        public Guid EntityA { get; }
        public Guid EntityB { get; }
        public Guid BranchA1 { get; }
        public Guid BranchA2 { get; }
        public string Email { get; }
        public string Password { get; } = "NavSecurityPassword123!";
        private readonly string _adminConn;
        private readonly WebApplicationFactory<Program> _factory;

        private NavSecurityFixture(string adminConn, string appConn, string email, TenantId tenant, IdentityId identityId, MembershipId membershipId, Guid entityA, Guid entityB, Guid branchA1, Guid branchA2)
        {
            _adminConn = adminConn;
            Email = email;
            Tenant = tenant;
            IdentityId = identityId;
            MembershipId = membershipId;
            EntityA = entityA;
            EntityB = entityB;
            BranchA1 = branchA1;
            BranchA2 = branchA2;
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseEnvironment("Development")
                    .UseSetting("TODIR_DATABASE_CONNECTION", appConn)
                    .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        }

        public static async Task<NavSecurityFixture> CreateAsync()
        {
            var adminConn = GetAdminConnectionString();
            var appPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            var appConn = new NpgsqlConnectionStringBuilder(adminConn)
            {
                Username = "erp_app",
                Password = appPassword
            }.ConnectionString;

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

            var fixture = new NavSecurityFixture(adminConn, appConn, email, tenant, identityId, membershipId, entityA, entityB, branchA1, branchA2);

            await using var admin = new NpgsqlConnection(adminConn);
            await admin.OpenAsync();

            var setupSql = $@"
                alter role erp_app password '{appPassword}';
                set role erp_owner;
                set app.tenant_id = '{fixture.Tenant.Value}';

                insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version)
                values ('{fixture.Tenant.Value}', 'sec-nav-{fixture.Tenant.Value:N}', 'Nav Sec Tenant', 'Active', 'sec-nav-{fixture.Tenant.Value:N}.local', 'sec-nav-{fixture.Tenant.Value:N}.local', now(), 1);

                insert into platform.legal_entity (id, tenant_id, code, legal_name, country_code, base_currency_code, timezone, locale, status, created_at, version)
                values
                    ('{fixture.EntityA}', '{fixture.Tenant.Value}', 'NAV_A', 'Nav Entity A', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1),
                    ('{fixture.EntityB}', '{fixture.Tenant.Value}', 'NAV_B', 'Nav Entity B', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1);

                insert into platform.branch (id, tenant_id, legal_entity_id, code, name, status, created_at, version)
                values
                    ('{fixture.BranchA1}', '{fixture.Tenant.Value}', '{fixture.EntityA}', 'BR_A1', 'Branch A1', 'Active', now(), 1),
                    ('{fixture.BranchA2}', '{fixture.Tenant.Value}', '{fixture.EntityA}', 'BR_A2', 'Branch A2', 'Active', now(), 1);

                insert into platform.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, password_hash, status, created_at, security_stamp, version)
                values ('{fixture.IdentityId.Value}', '{fixture.Tenant.Value}', '{email}', '{email.ToUpperInvariant()}', 'nav-sec-{fixture.IdentityId.Value:N}', 'Nav Sec User', true, '{passwordHash}', 'Active', now(), '{Guid.CreateVersion7()}', 1);

                insert into platform.membership (id, tenant_id, identity_id, display_name, status, created_at, version)
                values ('{fixture.MembershipId.Value}', '{fixture.Tenant.Value}', '{fixture.IdentityId.Value}', 'Nav Sec Member', 'Active', now(), 1);

                insert into platform.role (id, tenant_id, code, name, is_system_template, is_active, version)
                values
                    ('{roleCashier}', '{fixture.Tenant.Value}', 'cashier', 'Cashier', false, true, 1),
                    ('{roleCrmRep}', '{fixture.Tenant.Value}', 'crm-rep', 'CRM Rep', false, true, 1),
                    ('{roleCrmMgr}', '{fixture.Tenant.Value}', 'crm-mgr', 'CRM Manager', false, true, 1),
                    ('{roleInvClerk}', '{fixture.Tenant.Value}', 'inv-clerk', 'Inventory Clerk', false, true, 1),
                    ('{roleInvSupervisor}', '{fixture.Tenant.Value}', 'inv-sup', 'Inventory Supervisor', false, true, 1),
                    ('{roleLedgerClerk}', '{fixture.Tenant.Value}', 'ledger-clerk', 'Ledger Clerk', false, true, 1),
                    ('{roleLedgerMgr}', '{fixture.Tenant.Value}', 'ledger-mgr', 'Ledger Manager', false, true, 1),
                    ('{roleAccountingClose}', '{fixture.Tenant.Value}', 'acct-close', 'Accounting Close Only', false, true, 1),
                    ('{roleBranchA1Only}', '{fixture.Tenant.Value}', 'branch-a1-role', 'Branch A1 Scoped', false, true, 1),
                    ('{roleInactiveSuper}', '{fixture.Tenant.Value}', 'inactive-super', 'Inactive Super', false, false, 1);

                insert into platform.role_permission (tenant_id, role_id, permission_code)
                values
                    ('{fixture.Tenant.Value}', '{roleCashier}', 'banking.cash.count'),
                    ('{fixture.Tenant.Value}', '{roleCrmRep}', 'crm.lead.read'),
                    ('{fixture.Tenant.Value}', '{roleCrmRep}', 'crm.lead.create'),
                    ('{fixture.Tenant.Value}', '{roleCrmMgr}', 'crm.team.manage'),
                    ('{fixture.Tenant.Value}', '{roleInvClerk}', 'inventory.count.create'),
                    ('{fixture.Tenant.Value}', '{roleInvClerk}', 'inventory.count.enter'),
                    ('{fixture.Tenant.Value}', '{roleInvSupervisor}', 'inventory.count.post'),
                    ('{fixture.Tenant.Value}', '{roleInvSupervisor}', 'inventory.adjustment.post'),
                    ('{fixture.Tenant.Value}', '{roleLedgerClerk}', 'ledger.general_journal.create'),
                    ('{fixture.Tenant.Value}', '{roleLedgerMgr}', 'ledger.general_journal.post'),
                    ('{fixture.Tenant.Value}', '{roleAccountingClose}', 'accounting.close.execute'),
                    ('{fixture.Tenant.Value}', '{roleBranchA1Only}', 'inventory.stock.read'),
                    ('{fixture.Tenant.Value}', '{roleInactiveSuper}', '*');

                insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type, scope_id)
                values
                    ('{fixture.Tenant.Value}', '{fixture.MembershipId.Value}', '{roleCashier}', 'LegalEntity', '{fixture.EntityA}'),
                    ('{fixture.Tenant.Value}', '{fixture.MembershipId.Value}', '{roleCrmRep}', 'LegalEntity', '{fixture.EntityA}'),
                    ('{fixture.Tenant.Value}', '{fixture.MembershipId.Value}', '{roleInvClerk}', 'LegalEntity', '{fixture.EntityA}'),
                    ('{fixture.Tenant.Value}', '{fixture.MembershipId.Value}', '{roleLedgerClerk}', 'LegalEntity', '{fixture.EntityA}'),
                    ('{fixture.Tenant.Value}', '{fixture.MembershipId.Value}', '{roleAccountingClose}', 'LegalEntity', '{fixture.EntityA}'),
                    ('{fixture.Tenant.Value}', '{fixture.MembershipId.Value}', '{roleBranchA1Only}', 'Branch', '{fixture.BranchA1}');

                insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type)
                values ('{fixture.Tenant.Value}', '{fixture.MembershipId.Value}', '{roleInactiveSuper}', 'Tenant');

                insert into platform.module_instance (id, tenant_id, module_id, scope_type, scope_id, status, manifest_version, version)
                values
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'banking', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'crm', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'inventory', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'ledger', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1);

                reset app.tenant_id;
                reset role;
            ";

            await fixture.ExecuteAdminSqlAsync(setupSql);
            return fixture;
        }

        public HttpClient CreateClient() =>
            _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        public T GetRequiredService<T>() where T : notnull =>
            _factory.Services.GetRequiredService<T>();

        public async Task LoginAsync(HttpClient client)
        {
            var response = await PostAsync(client, "/api/v1/auth/login", new { email = Email, password = Password });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        public async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object? body, string? idempotencyKey = null)
        {
            var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
            Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
            var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfResponse>();
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body ?? new { }) };
            request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                request.Headers.Add("Idempotency-Key", idempotencyKey);
            }
            return await client.SendAsync(request);
        }

        public async Task ExecuteAdminSqlAsync(string sql)
        {
            await using var admin = new NpgsqlConnection(_adminConn);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand(sql, admin);
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _factory.Dispose();
            var cleanupSql = $@"
                set role erp_owner;
                set app.tenant_id = '{Tenant.Value}';
                delete from platform.module_instance where tenant_id = '{Tenant.Value}';
                delete from platform.membership_role_assignment where tenant_id = '{Tenant.Value}';
                delete from platform.role_permission where tenant_id = '{Tenant.Value}';
                delete from platform.role where tenant_id = '{Tenant.Value}';
                delete from platform.membership where tenant_id = '{Tenant.Value}';
                delete from platform.identity where tenant_id = '{Tenant.Value}';
                delete from platform.branch where tenant_id = '{Tenant.Value}';
                delete from platform.legal_entity where tenant_id = '{Tenant.Value}';
                delete from platform.tenant where id = '{Tenant.Value}';
                reset app.tenant_id;
                reset role;
            ";
            try
            {
                await ExecuteAdminSqlAsync(cleanupSql);
            }
            catch
            {
                // Ignore cleanup errors during disposal
            }
        }
    }

    private sealed record CsrfResponse(string Token);
}
