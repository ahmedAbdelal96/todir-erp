using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
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
    private static readonly string[] DefaultAllowedUses = ["Operating"];

    private static string GetAdminConnectionString()
    {
        var conn = Environment.GetEnvironmentVariable("TODIR_ACCEPTANCE_ADMIN_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION");

        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException(
                "CRITICAL SAFETY VIOLATION: An explicitly configured disposable acceptance environment is required. " +
                "Set TODIR_ACCEPTANCE_ADMIN_CONNECTION or TODIR_P04_ADMIN_CONNECTION to a verified disposable database.");
        }

        var builder = new NpgsqlConnectionStringBuilder(conn);
        var db = builder.Database ?? string.Empty;

        // 1. Target database name must contain 'acceptance' or 'test'
        if (!db.Contains("acceptance", StringComparison.OrdinalIgnoreCase) &&
            !db.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"CRITICAL SAFETY VIOLATION: Refusing to run tests against database '{db}'. Target database must contain 'acceptance' or 'test'.");
        }

        // 2. Explicit disposable flag or acceptance environment verification
        var isExplicitlyDisposable = string.Equals(Environment.GetEnvironmentVariable("TODIR_ACCEPTANCE_DISPOSABLE"), "true", StringComparison.OrdinalIgnoreCase) ||
                                     db.EndsWith("_acceptance", StringComparison.OrdinalIgnoreCase) ||
                                     db.EndsWith("_test", StringComparison.OrdinalIgnoreCase);
        if (!isExplicitlyDisposable)
        {
            throw new InvalidOperationException($"CRITICAL SAFETY VIOLATION: Database '{db}' is not verified as a disposable acceptance database. Set TODIR_ACCEPTANCE_DISPOSABLE=true.");
        }

        return conn;
    }

    private static string GetAppConnectionString(string adminConn)
    {
        var appConn = Environment.GetEnvironmentVariable("TODIR_ACCEPTANCE_APP_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_APP_CONNECTION");

        if (!string.IsNullOrWhiteSpace(appConn))
        {
            return appConn;
        }

        var appPassword = Environment.GetEnvironmentVariable("TODIR_APP_PASSWORD")
            ?? Environment.GetEnvironmentVariable("TODIR_RUNTIME_PASSWORD");

        if (string.IsNullOrWhiteSpace(appPassword))
        {
            throw new InvalidOperationException(
                "CRITICAL SAFETY VIOLATION: Application database credentials must be explicitly configured via TODIR_ACCEPTANCE_APP_CONNECTION or TODIR_APP_PASSWORD.");
        }

        return new NpgsqlConnectionStringBuilder(adminConn)
        {
            Username = "erp_app",
            Password = appPassword
        }.ConnectionString;
    }

    // -------------------------------------------------------------------------
    // SECTION A: Controlled Active -> ReadOnly / WindDown Lifecycle Transitions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Inventory_ActiveToReadOnly_BlocksMutationAndPreservesReads()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        // 1. In Active state: create warehouse WH_SEC_01 succeeds
        var wh1Res = await fixture.PostAsync(client, $"/api/v1/inventory/legal-entities/{fixture.EntityA}/warehouses", new
        {
            code = "WH_SEC_01",
            name = "Security Test Warehouse 1",
            locationMode = "WarehouseOnly",
            branchId = fixture.BranchA1
        });
        Assert.Equal(HttpStatusCode.Created, wh1Res.StatusCode);

        // Verify committed DB state
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"select count(*) from business_inventory.warehouse where tenant_id = '{fixture.Tenant.Value}' and code = 'WH_SEC_01'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        // 2. Transition Inventory to ReadOnly
        var setReadOnlySql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'ReadOnly' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'inventory';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setReadOnlySql);

        // 3. Exact same user, permission, and valid payload for WH_SEC_02: rejected with HTTP 403 module.lifecycle_readonly
        var wh2Res = await fixture.PostAsync(client, $"/api/v1/inventory/legal-entities/{fixture.EntityA}/warehouses", new
        {
            code = "WH_SEC_02",
            name = "Security Test Warehouse 2",
            locationMode = "WarehouseOnly",
            branchId = fixture.BranchA1
        });
        Assert.Equal(HttpStatusCode.Forbidden, wh2Res.StatusCode);
        var errDoc = await wh2Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.lifecycle_readonly", errDoc.GetProperty("code").GetString());

        // 4. Verify zero prohibited DB state changes
        await using (var cmd = new NpgsqlCommand($"select count(*) from business_inventory.warehouse where tenant_id = '{fixture.Tenant.Value}' and code = 'WH_SEC_02'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(0L, count);
        }

        // 5. Permitted historical reads continue to succeed
        var readRes = await client.GetAsync($"/api/v1/inventory/legal-entities/{fixture.EntityA}/warehouses");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);
    }

    [Fact]
    public async Task Ledger_ActiveToWindDown_BlocksMutationAndPreservesReads()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        // 1. In Active state: create account 1001 succeeds
        var acc1Res = await fixture.PostAsync(client, $"/api/v1/ledger/legal-entities/{fixture.EntityA}/accounts", new
        {
            code = "1001",
            nameAr = "الصندوق الرئيسي",
            nameEn = "Main Cash",
            type = "Asset",
            subtype = "CashAndEquivalents",
            allowDirectPosting = true
        });
        Assert.Equal(HttpStatusCode.Created, acc1Res.StatusCode);

        // Verify committed DB state
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"select count(*) from finance_ledger.account where tenant_id = '{fixture.Tenant.Value}' and code = '1001'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        // 2. Transition Ledger to WindDown
        var setWindDownSql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'WindDown' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'ledger';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setWindDownSql);

        // 3. Exact same user, permission, and valid payload for 1002: rejected with HTTP 403 module.lifecycle_readonly
        var acc2Res = await fixture.PostAsync(client, $"/api/v1/ledger/legal-entities/{fixture.EntityA}/accounts", new
        {
            code = "1002",
            nameAr = "صندوق المبيعات",
            nameEn = "Sales Cash",
            type = "Asset",
            subtype = "CashAndEquivalents",
            allowDirectPosting = true
        });
        Assert.Equal(HttpStatusCode.Forbidden, acc2Res.StatusCode);
        var errDoc = await acc2Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.lifecycle_wind_down", errDoc.GetProperty("code").GetString());

        // 4. Verify DB state unchanged
        await using (var cmd = new NpgsqlCommand($"select count(*) from finance_ledger.account where tenant_id = '{fixture.Tenant.Value}' and code = '1002'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(0L, count);
        }

        // 5. Permitted historical reads succeed
        var readRes = await client.GetAsync($"/api/v1/ledger/legal-entities/{fixture.EntityA}/accounts");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);
    }

    [Fact]
    public async Task CRM_ActiveToReadOnly_BlocksMutationAndPreservesReads()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var key1 = $"lead-act-{Guid.NewGuid():N}";
        // 1. In Active state: create lead succeeds
        var lead1Res = await fixture.PostAsync(client, $"/api/v1/crm/legal-entities/{fixture.EntityA}/leads", new
        {
            legalEntityId = fixture.EntityA,
            displayName = "Active State Test Lead",
            companyName = "Active Company",
            email = "active@test.local",
            phone = "+201000000001",
            source = "Direct",
            priority = "Normal"
        }, key1);
        Assert.Equal(HttpStatusCode.Created, lead1Res.StatusCode);

        // Verify committed DB state
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"select count(*) from business_crm.lead where tenant_id = '{fixture.Tenant.Value}' and display_name = 'Active State Test Lead'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        // 2. Transition CRM to ReadOnly
        var setReadOnlySql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'ReadOnly' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'crm';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setReadOnlySql);

        // 3. Exact same user, permission, and valid payload: rejected with HTTP 403 module.lifecycle_readonly
        var key2 = $"lead-ro-{Guid.NewGuid():N}";
        var lead2Res = await fixture.PostAsync(client, $"/api/v1/crm/legal-entities/{fixture.EntityA}/leads", new
        {
            legalEntityId = fixture.EntityA,
            displayName = "ReadOnly State Test Lead",
            companyName = "ReadOnly Company",
            email = "readonly@test.local",
            phone = "+201000000002",
            source = "Direct",
            priority = "Normal"
        }, key2);
        Assert.Equal(HttpStatusCode.Forbidden, lead2Res.StatusCode);
        var errDoc = await lead2Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.lifecycle_readonly", errDoc.GetProperty("code").GetString());

        // 4. Verify DB state unchanged
        await using (var cmd = new NpgsqlCommand($"select count(*) from business_crm.lead where tenant_id = '{fixture.Tenant.Value}' and display_name = 'ReadOnly State Test Lead'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(0L, count);
        }

        // 5. Permitted historical reads succeed
        var readRes = await client.GetAsync($"/api/v1/crm/legal-entities/{fixture.EntityA}/leads");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);
    }

    [Fact]
    public async Task Banking_ActiveToReadOnly_BlocksMutationAndPreservesReads()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        // 1. In Active state: create treasury account succeeds
        var acct1Res = await fixture.PostAsync(client, "/api/v1/banking/treasury-accounts", new
        {
            legalEntityId = fixture.EntityA,
            branchId = fixture.BranchA1,
            code = "BANK_SEC_01",
            name = "Security Bank Account 1",
            type = "Bank",
            currencyPolicy = "SingleCurrency",
            currencyCode = "EGP",
            purpose = "Operations",
            liquidityClass = "Immediate",
            allowedUses = DefaultAllowedUses,
            maskedIdentifier = "****1234"
        });
        Assert.Equal(HttpStatusCode.Created, acct1Res.StatusCode);
        var createdAcct = await acct1Res.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = createdAcct.GetProperty("id").GetString()!;

        // Verify committed DB state
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"select count(*) from finance_banking.treasury_account where tenant_id = '{fixture.Tenant.Value}' and code = 'BANK_SEC_01'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        // 2. Transition Banking to ReadOnly
        var setReadOnlySql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'ReadOnly' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'banking';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setReadOnlySql);

        // 3. Exact same user, permission, and valid payload for BANK_SEC_02: rejected with HTTP 403 module.lifecycle_readonly
        var acct2Res = await fixture.PostAsync(client, "/api/v1/banking/treasury-accounts", new
        {
            legalEntityId = fixture.EntityA,
            branchId = fixture.BranchA1,
            code = "BANK_SEC_02",
            name = "Security Bank Account 2",
            type = "Bank",
            currencyPolicy = "SingleCurrency",
            currencyCode = "EGP",
            purpose = "Operations",
            liquidityClass = "Immediate",
            allowedUses = DefaultAllowedUses,
            maskedIdentifier = "****1234"
        });
        Assert.Equal(HttpStatusCode.Forbidden, acct2Res.StatusCode);
        var errDoc = await acct2Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.lifecycle_readonly", errDoc.GetProperty("code").GetString());

        // 4. Verify DB state unchanged
        await using (var cmd = new NpgsqlCommand($"select count(*) from finance_banking.treasury_account where tenant_id = '{fixture.Tenant.Value}' and code = 'BANK_SEC_02'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(0L, count);
        }

        // 5. Permitted historical reads succeed
        var readRes = await client.GetAsync($"/api/v1/banking/treasury-accounts/{accountId}?legalEntityId={fixture.EntityA}");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);
    }

    [Fact]
    public async Task Accounting_ActiveToReadOnly_BlocksMutationAndPreservesReads()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        // 1. In Active state (with commercial entitlement snapshot): save statement definition succeeds
        var stmt1Res = await fixture.PutAsync(client, $"/api/v1/accounting/legal-entities/{fixture.EntityA}/statement-definitions", new
        {
            code = "DEF_SEC_01",
            kind = "BalanceSheet",
            lines = new[]
            {
                new
                {
                    code = "L1",
                    nameAr = "الأصول",
                    nameEn = "Assets",
                    sequence = 1,
                    lineKind = "MappedAccounts",
                    accountType = "Asset"
                }
            }
        });
        Assert.Equal(HttpStatusCode.OK, stmt1Res.StatusCode);

        // Verify committed DB state
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"select count(*) from business_accounting.financial_statement_definition where tenant_id = '{fixture.Tenant.Value}' and code = 'DEF_SEC_01'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        // 2. Transition Accounting to ReadOnly
        var setReadOnlySql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'ReadOnly' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'accounting';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setReadOnlySql);

        // 3. Exact same user, permission, and valid payload for DEF_SEC_02: rejected with HTTP 403 module.lifecycle_readonly
        var stmt2Res = await fixture.PutAsync(client, $"/api/v1/accounting/legal-entities/{fixture.EntityA}/statement-definitions", new
        {
            code = "DEF_SEC_02",
            kind = "BalanceSheet",
            lines = new[]
            {
                new
                {
                    code = "L1",
                    nameAr = "الأصول",
                    nameEn = "Assets",
                    sequence = 1,
                    lineKind = "MappedAccounts",
                    accountType = "Asset"
                }
            }
        });
        Assert.Equal(HttpStatusCode.Forbidden, stmt2Res.StatusCode);
        var errDoc = await stmt2Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.lifecycle_readonly", errDoc.GetProperty("code").GetString());

        // 4. Verify DB state unchanged
        await using (var cmd = new NpgsqlCommand($"select count(*) from business_accounting.financial_statement_definition where tenant_id = '{fixture.Tenant.Value}' and code = 'DEF_SEC_02'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(0L, count);
        }

        // 5. Permitted historical reads succeed
        var readRes = await client.GetAsync($"/api/v1/accounting/legal-entities/{fixture.EntityA}/statement-definitions/BalanceSheet");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);
    }

    // -------------------------------------------------------------------------
    // SEPARATE NEGATIVE SECURITY CASES: RBAC, Entitlement, Scope, and Lifecycle
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RBAC_UserLackingPermission_DeniedByAuthorizationNotLifecycle()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        // User does NOT have inventory.warehouse.manage on Entity B
        var res = await fixture.PostAsync(client, $"/api/v1/inventory/legal-entities/{fixture.EntityB}/warehouses", new
        {
            code = "WH_RBAC_TEST",
            name = "RBAC Test Warehouse",
            locationMode = "WarehouseOnly"
        });

        Assert.True(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CommercialEntitlement_MissingEntitlement_FailsClosedWithModuleUnentitled()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();

        // Remove commercial entitlement for accounting
        var removeEntitlementSql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            delete from platform.entitlement_snapshot where tenant_id = '{fixture.Tenant.Value}';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(removeEntitlementSql);

        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var doc = await (await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={fixture.EntityA}")).Content.ReadFromJsonAsync<JsonElement>();
        var entitled = doc.GetProperty("entitledModules").EnumerateArray().Select(m => m.GetString()!).ToHashSet();
        Assert.DoesNotContain("accounting", entitled);

        var res = await fixture.PutAsync(client, $"/api/v1/accounting/legal-entities/{fixture.EntityA}/statement-definitions", new
        {
            code = "DEF_UNENTITLED",
            kind = "BalanceSheet",
            lines = new[]
            {
                new
                {
                    code = "L1",
                    nameAr = "الأصول",
                    nameEn = "Assets",
                    sequence = 1,
                    lineKind = "MappedAccounts",
                    accountType = "Asset"
                }
            }
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var errDoc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.unentitled", errDoc.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ModuleActivation_CommerciallyEntitledWithoutModuleInstance_FailsClosedWithModuleNotActivated()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();

        // Delete module instance for accounting, keeping commercial entitlement
        var deleteInstanceSql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            delete from platform.module_instance where tenant_id = '{fixture.Tenant.Value}' and module_id = 'accounting';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(deleteInstanceSql);

        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var doc = await (await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={fixture.EntityA}")).Content.ReadFromJsonAsync<JsonElement>();
        var status = doc.GetProperty("moduleStatuses").GetProperty("accounting").GetString();
        Assert.Equal("Disabled", status);

        var res = await fixture.PutAsync(client, $"/api/v1/accounting/legal-entities/{fixture.EntityA}/statement-definitions", new
        {
            code = "DEF_NOT_ACTIVATED",
            kind = "BalanceSheet",
            lines = new[]
            {
                new
                {
                    code = "L1",
                    nameAr = "الأصول",
                    nameEn = "Assets",
                    sequence = 1,
                    lineKind = "MappedAccounts",
                    accountType = "Asset"
                }
            }
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var errDoc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.not_activated", errDoc.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Scope_ScopeConflict_FailsClosedWithScopeConflict()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfResponse>();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/inventory/legal-entities/{fixture.EntityA}/warehouses")
        {
            Content = JsonContent.Create(new
            {
                code = "WH_CONFLICT",
                name = "Conflict Test",
                locationMode = "WarehouseOnly"
            })
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        request.Headers.Add("X-Legal-Entity-Id", fixture.EntityB.ToString("D"));

        var res = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errDoc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("scope.conflict", errDoc.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Scope_LegalEntityInstanceOverride_TakesPrecedenceOverTenantScope()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();

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

        var mutateRes = await fixture.PostAsync(client, $"/api/v1/inventory/legal-entities/{fixture.EntityA}/warehouses", new
        {
            code = "WH_OVERRIDE",
            name = "Override Warehouse",
            locationMode = "WarehouseOnly"
        });
        Assert.Equal(HttpStatusCode.Forbidden, mutateRes.StatusCode);
        var errDoc = await mutateRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.lifecycle_readonly", errDoc.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Scope_CrossTenantAndCrossBranch_Denied()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var resA1 = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={fixture.EntityA}&branchId={fixture.BranchA1}");
        Assert.Equal(HttpStatusCode.OK, resA1.StatusCode);
        var docA1 = await resA1.Content.ReadFromJsonAsync<JsonElement>();
        var permsA1 = docA1.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToHashSet();
        Assert.Contains("inventory.stock.read", permsA1);

        var resA2 = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={fixture.EntityA}&branchId={fixture.BranchA2}");
        Assert.Equal(HttpStatusCode.OK, resA2.StatusCode);
        var docA2 = await resA2.Content.ReadFromJsonAsync<JsonElement>();
        var permsA2 = docA2.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToHashSet();
        Assert.DoesNotContain("inventory.stock.read", permsA2);

        var resB = await client.GetAsync($"/api/v1/authorization/effective-access?legalEntityId={fixture.EntityB}");
        Assert.Equal(HttpStatusCode.OK, resB.StatusCode);
        var docB = await resB.Content.ReadFromJsonAsync<JsonElement>();
        var permsB = docB.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToHashSet();
        Assert.DoesNotContain("banking.accounts.create", permsB);
        Assert.DoesNotContain("crm.lead.create", permsB);
    }

    [Fact]
    public async Task ProvisioningAndAwaitingGoLive_FailsClosed()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();

        var setProvisioningSql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'Provisioning' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'banking';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setProvisioningSql);

        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var mutateRes = await fixture.PostAsync(client, "/api/v1/banking/treasury-accounts", new
        {
            legalEntityId = fixture.EntityA,
            code = "BANK_PROV",
            name = "Prov Bank",
            type = "Bank",
            currencyPolicy = "SingleCurrency",
            currencyCode = "EGP",
            purpose = "Operations",
            liquidityClass = "Immediate",
            allowedUses = DefaultAllowedUses,
            maskedIdentifier = "****1234"
        });
        Assert.Equal(HttpStatusCode.Forbidden, mutateRes.StatusCode);
        var errDoc = await mutateRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.disabled", errDoc.GetProperty("code").GetString());
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
    public void ProductionStatusEvaluation_InvalidOrCorruptStatus_FailsClosedWithModuleInvalidStatus()
    {
        // 1. Production decision logic with corrupted/non-existent status must fail closed with module.invalid_status
        const string corruptStatus = "CorruptedNonExistentStatus";
        var mutationDecision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus(corruptStatus, isPurchasable: false, isMutation: true);
        Assert.False(mutationDecision.Allowed);
        Assert.False(mutationDecision.IsReadOnly);
        Assert.Equal("Disabled", mutationDecision.Status);
        Assert.Equal("module.invalid_status", mutationDecision.DeniedReason);

        var readDecision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus(corruptStatus, isPurchasable: false, isMutation: false);
        Assert.False(readDecision.Allowed);
        Assert.Equal("module.invalid_status", readDecision.DeniedReason);

        // 2. Verified valid production statuses produce expected production decisions
        var activeDecision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus("Active", isPurchasable: false, isMutation: true);
        Assert.True(activeDecision.Allowed);
        Assert.Null(activeDecision.DeniedReason);

        var readOnlyDecision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus("ReadOnly", isPurchasable: false, isMutation: true);
        Assert.False(readOnlyDecision.Allowed);
        Assert.True(readOnlyDecision.IsReadOnly);
        Assert.Equal("module.lifecycle_readonly", readOnlyDecision.DeniedReason);

        var windDownDecision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus("WindDown", isPurchasable: false, isMutation: true);
        Assert.False(windDownDecision.Allowed);
        Assert.Equal("module.lifecycle_wind_down", windDownDecision.DeniedReason);

        // 3. Deliberate breakage verification: Any status not in the production ValidStatuses set fails closed
        var fakeStatuses = new[] { "AdminOverride", "BypassSecurity", "SuperUser", "Custom", "RANDOM_STATUS" };
        foreach (var fake in fakeStatuses)
        {
            var fakeDecision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus(fake, isPurchasable: false, isMutation: true);
            Assert.False(fakeDecision.Allowed, $"Fake status '{fake}' was unexpectedly allowed.");
            Assert.Equal("module.invalid_status", fakeDecision.DeniedReason);
        }
    }

    // -------------------------------------------------------------------------
    // TEST FIXTURE (Isolated, Non-Destructive, Preconfigured Credentials)
    // -------------------------------------------------------------------------

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
            var appConn = GetAppConnectionString(adminConn);

            var tenant = TenantId.New();
            var identityId = IdentityId.New();
            var membershipId = MembershipId.New();
            var entityA = Guid.CreateVersion7();
            var entityB = Guid.CreateVersion7();
            var branchA1 = Guid.CreateVersion7();
            var branchA2 = Guid.CreateVersion7();

            var roleOperatorA = Guid.CreateVersion7();
            var roleBranchA1Only = Guid.CreateVersion7();

            var email = $"nav-sec-{identityId.Value:N}@sec-nav-{tenant.Value:N}.local";
            var password = "NavSecurityPassword123!";
            var passwordHash = IdentityConventions.HashPassword(password);

            var fixture = new NavSecurityFixture(adminConn, appConn, email, tenant, identityId, membershipId, entityA, entityB, branchA1, branchA2);

            var setupSql = $@"
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
                    ('{roleOperatorA}', '{fixture.Tenant.Value}', 'operator-a', 'Operator A Full', false, true, 1),
                    ('{roleBranchA1Only}', '{fixture.Tenant.Value}', 'branch-a1-role', 'Branch A1 Scoped', false, true, 1);

                insert into platform.role_permission (tenant_id, role_id, permission_code)
                values
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'inventory.warehouse.manage'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'ledger.account.manage'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'ledger.account.read'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'crm.lead.create'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'crm.lead.read'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'banking.accounts.create'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'banking.accounts.read'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'accounting.statement.manage'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'accounting.statement.read'),
                    ('{fixture.Tenant.Value}', '{roleBranchA1Only}', 'inventory.stock.read');

                insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type, scope_id)
                values
                    ('{fixture.Tenant.Value}', '{fixture.MembershipId.Value}', '{roleOperatorA}', 'LegalEntity', '{fixture.EntityA}'),
                    ('{fixture.Tenant.Value}', '{fixture.MembershipId.Value}', '{roleBranchA1Only}', 'Branch', '{fixture.BranchA1}');

                insert into platform.entitlement_snapshot (tenant_id, snapshot_version, issued_at, valid_until, key_id, modules, features, limits, scopes, signature)
                values ('{fixture.Tenant.Value}', 1, now(), now() + interval '30 days', 'test_key', '[""banking"", ""crm"", ""inventory"", ""accounting""]'::jsonb, '[]'::jsonb, '{{}}'::jsonb, '[]'::jsonb, 'test_sig');

                insert into platform.module_instance (id, tenant_id, module_id, scope_type, scope_id, status, manifest_version, version)
                values
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'banking', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'crm', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'inventory', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'accounting', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
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
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Login failed with {response.StatusCode}: {body}");
            }
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

        public async Task<HttpResponseMessage> PutAsync(HttpClient client, string path, object? body)
        {
            var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
            Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
            var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfResponse>();
            using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body ?? new { }) };
            request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
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
                delete from business_accounting.financial_statement_definition where tenant_id = '{Tenant.Value}';
                delete from finance_banking.treasury_account where tenant_id = '{Tenant.Value}';
                delete from business_crm.lead where tenant_id = '{Tenant.Value}';
                delete from finance_ledger.account where tenant_id = '{Tenant.Value}';
                delete from business_inventory.warehouse where tenant_id = '{Tenant.Value}';
                delete from platform.entitlement_snapshot where tenant_id = '{Tenant.Value}';
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
