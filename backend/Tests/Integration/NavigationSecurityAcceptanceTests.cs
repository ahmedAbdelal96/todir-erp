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
            ?? Environment.GetEnvironmentVariable("TODIR_RUNTIME_PASSWORD")
            ?? "todir-app-dev";

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

    [Fact]
    public async Task Sales_ActiveToReadOnly_BlocksMutationAndPreservesReads()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var party1 = Guid.CreateVersion7();
        // 1. In Active state: save customer profile succeeds
        var profile1Res = await fixture.PutAsync(client, $"/api/v1/sales/legal-entities/{fixture.EntityA}/customer-profiles/{party1}", new
        {
            legalEntityId = fixture.EntityA,
            partyId = party1,
            customerPoRequired = false
        });
        Assert.Equal(HttpStatusCode.OK, profile1Res.StatusCode);

        // Verify committed DB state
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"select count(*) from business_sales.sales_customer_profile where tenant_id = '{fixture.Tenant.Value}' and party_id = '{party1}'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        // 2. Transition Sales to ReadOnly
        var setReadOnlySql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'ReadOnly' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'sales';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setReadOnlySql);

        // 3. Exact same user, permission, and valid payload for party2: rejected with HTTP 403 module.lifecycle_readonly
        var party2 = Guid.CreateVersion7();
        var profile2Res = await fixture.PutAsync(client, $"/api/v1/sales/legal-entities/{fixture.EntityA}/customer-profiles/{party2}", new
        {
            legalEntityId = fixture.EntityA,
            partyId = party2,
            customerPoRequired = true
        });
        Assert.Equal(HttpStatusCode.Forbidden, profile2Res.StatusCode);
        var errDoc = await profile2Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.lifecycle_readonly", errDoc.GetProperty("code").GetString());

        // 4. Verify DB state unchanged
        await using (var cmd = new NpgsqlCommand($"select count(*) from business_sales.sales_customer_profile where tenant_id = '{fixture.Tenant.Value}' and party_id = '{party2}'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(0L, count);
        }

        // 5. Permitted historical reads succeed
        var readRes = await client.GetAsync($"/api/v1/sales/legal-entities/{fixture.EntityA}/orders");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);
    }

    [Fact]
    public async Task Purchasing_ActiveToReadOnly_BlocksMutationAndPreservesReads()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        // 1. In Active state: save purchase settings succeeds
        var set1Res = await fixture.PutAsync(client, $"/api/v1/purchasing/legal-entities/{fixture.EntityA}/settings", new
        {
            legalEntityId = fixture.EntityA,
            approvalRequired = true,
            approvalThreshold = 50000m,
            overReceiptPolicy = "Disallow",
            overReceiptTolerance = 0m,
            invoiceWithoutReceipt = false,
            threeWayMatching = true,
            buyerApproverSeparation = true,
            quantityTolerance = 0.05m,
            priceAbsoluteTolerance = 10m,
            pricePercentageTolerance = 0.02m,
            taxTolerance = 1m,
            invoiceEligibilityBasis = "Receipt"
        });
        Assert.Equal(HttpStatusCode.OK, set1Res.StatusCode);

        // Verify committed DB state
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"select count(*) from business_purchasing.purchase_settings where tenant_id = '{fixture.Tenant.Value}' and legal_entity_id = '{fixture.EntityA}'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        // 2. Transition Purchasing to ReadOnly
        var setReadOnlySql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'ReadOnly' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'purchasing';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setReadOnlySql);

        // 3. Exact same user, permission, and valid payload: rejected with HTTP 403 module.lifecycle_readonly
        var set2Res = await fixture.PutAsync(client, $"/api/v1/purchasing/legal-entities/{fixture.EntityA}/settings", new
        {
            legalEntityId = fixture.EntityA,
            approvalRequired = false,
            approvalThreshold = (decimal?)null,
            overReceiptPolicy = "Allow",
            overReceiptTolerance = 0m,
            invoiceWithoutReceipt = true,
            threeWayMatching = false,
            buyerApproverSeparation = false,
            quantityTolerance = 0m,
            priceAbsoluteTolerance = 0m,
            pricePercentageTolerance = 0m,
            taxTolerance = 0m,
            invoiceEligibilityBasis = "PO"
        });
        Assert.Equal(HttpStatusCode.Forbidden, set2Res.StatusCode);
        var errDoc = await set2Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.lifecycle_readonly", errDoc.GetProperty("code").GetString());

        // 4. Permitted historical reads succeed
        var readRes = await client.GetAsync($"/api/v1/purchasing/legal-entities/{fixture.EntityA}/settings");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);
        var poReadRes = await client.GetAsync($"/api/v1/purchasing/legal-entities/{fixture.EntityA}/purchase-orders");
        Assert.Equal(HttpStatusCode.OK, poReadRes.StatusCode);
    }

    [Fact]
    public async Task POS_ActiveToReadOnly_BlocksMutationAndPreservesReads()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        // 1. In Active state: create register succeeds
        var reg1Res = await fixture.PostAsync(client, "/api/v1/pos/registers", new
        {
            legalEntityId = fixture.EntityA,
            branchId = fixture.BranchA1,
            code = "REG_SEC_01",
            name = "Security Register 1",
            currencyCode = "EGP",
            offlineEnabled = false
        });
        Assert.Equal(HttpStatusCode.Created, reg1Res.StatusCode);

        // Verify committed DB state
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"select count(*) from business_pos.pos_register where tenant_id = '{fixture.Tenant.Value}' and code = 'REG_SEC_01'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        // 2. Transition POS to ReadOnly
        var setReadOnlySql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            update platform.module_instance set status = 'ReadOnly' where tenant_id = '{fixture.Tenant.Value}' and module_id = 'pos';
            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setReadOnlySql);

        // 3. Exact same user, permission, and valid payload for REG_SEC_02: rejected with HTTP 403 module.lifecycle_readonly
        var reg2Res = await fixture.PostAsync(client, "/api/v1/pos/registers", new
        {
            legalEntityId = fixture.EntityA,
            branchId = fixture.BranchA1,
            code = "REG_SEC_02",
            name = "Security Register 2",
            currencyCode = "EGP",
            offlineEnabled = false
        });
        Assert.Equal(HttpStatusCode.Forbidden, reg2Res.StatusCode);
        var errDoc = await reg2Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("module.lifecycle_readonly", errDoc.GetProperty("code").GetString());

        // 4. Verify DB state unchanged
        await using (var cmd = new NpgsqlCommand($"select count(*) from business_pos.pos_register where tenant_id = '{fixture.Tenant.Value}' and code = 'REG_SEC_02'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(0L, count);
        }

        // 5. Permitted historical reads succeed
        var readRes = await client.GetAsync($"/api/v1/pos/registers?legalEntityId={fixture.EntityA}");
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
    // SECTION C: Real Business Workflow Acceptance (Sales, Purchasing, POS)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Sales_EndToEndCommercialWorkflow_QuotationLifecycleToSalesOrderConversion()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var customerId = Guid.CreateVersion7();

        // 1. Setup Customer Party in Directory
        var setupCustomerSql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';

            insert into foundation_directory.party (tenant_id, id, party_type, status, display_name, display_name_normalized, created_by, updated_by, created_at, updated_at, version)
            values ('{fixture.Tenant.Value}', '{customerId}', 'Organization', 'Active', 'Acme Commercial Corp', 'ACME COMMERCIAL CORP', '{fixture.IdentityId.Value}', '{fixture.IdentityId.Value}', now(), now(), 1);

            insert into foundation_directory.party_role (tenant_id, party_id, role_code, status, created_by, created_at, updated_at, version, active_from)
            values ('{fixture.Tenant.Value}', '{customerId}', 'Customer', 'Active', '{fixture.IdentityId.Value}', now(), now(), 1, CURRENT_DATE);

            insert into foundation_directory.party_visibility (tenant_id, party_id, scope_type, visibility_id)
            values ('{fixture.Tenant.Value}', '{customerId}', 'TenantWide', '{Guid.CreateVersion7()}');

            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setupCustomerSql);

        // 2. Create Quotation in Draft state
        var createQuoteRes = await fixture.PostAsync(client, $"/api/v1/sales/legal-entities/{fixture.EntityA}/quotations", new
        {
            legalEntityId = fixture.EntityA,
            branchId = fixture.BranchA1,
            partyId = customerId,
            currencyCode = "EGP",
            quoteDate = DateOnly.FromDateTime(DateTime.UtcNow),
            customerReference = "CUST-REF-001",
            lines = new[]
            {
                new
                {
                    description = "Commercial Advisory Package",
                    quantity = 5m,
                    unitPrice = 200m,
                    discountAmount = 0m,
                    uomCode = "EA"
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, createQuoteRes.StatusCode);
        var quoteId = (await createQuoteRes.Content.ReadFromJsonAsync<JsonElement>()).GetGuid();
        Assert.NotEqual(Guid.Empty, quoteId);

        // 3. Open Quotation (Draft -> Open)
        var openQuoteRes = await fixture.PostAsync(client, $"/api/v1/sales/legal-entities/{fixture.EntityA}/quotations/{quoteId}/open", new
        {
            version = 1L,
            reason = "Sent to client for approval"
        });
        Assert.Equal(HttpStatusCode.OK, openQuoteRes.StatusCode);

        // 4. Accept Quotation (Open -> Accepted)
        var acceptQuoteRes = await fixture.PostAsync(client, $"/api/v1/sales/legal-entities/{fixture.EntityA}/quotations/{quoteId}/accept", new
        {
            revisionNumber = 1,
            acceptanceMethod = "CustomerEmail",
            evidenceReference = "EMAIL-PO-2026",
            note = "Client confirmed agreement",
            idempotencyKey = $"acc-{quoteId:N}"
        });
        Assert.Equal(HttpStatusCode.OK, acceptQuoteRes.StatusCode);

        // 5. Convert Quotation to Sales Order
        var convertRes = await fixture.PostAsync(client, $"/api/v1/sales/legal-entities/{fixture.EntityA}/quotations/{quoteId}/convert-to-order", new
        {
            acceptedRevision = 1,
            idempotencyKey = $"conv-{quoteId:N}"
        });
        Assert.Equal(HttpStatusCode.Created, convertRes.StatusCode);
        var orderId = (await convertRes.Content.ReadFromJsonAsync<JsonElement>()).GetGuid();
        Assert.NotEqual(Guid.Empty, orderId);

        // 6. Get Order Details and Confirm Order
        var getOrderRes = await client.GetAsync($"/api/v1/sales/legal-entities/{fixture.EntityA}/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, getOrderRes.StatusCode);
        var orderDoc = await getOrderRes.Content.ReadFromJsonAsync<JsonElement>();
        var orderVersion = orderDoc.GetProperty("version").GetInt64();
        Assert.Equal("Draft", orderDoc.GetProperty("commercialState").GetString());

        var confirmRes = await fixture.PostAsync(client, $"/api/v1/sales/legal-entities/{fixture.EntityA}/orders/{orderId}/confirm", new
        {
            version = orderVersion,
            idempotencyKey = $"conf-{orderId:N}",
            reason = "Order confirmed for execution"
        });
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);

        // 7. Server-side Pagination Verification
        var quotesPageRes = await client.GetAsync($"/api/v1/sales/legal-entities/{fixture.EntityA}/quotations?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, quotesPageRes.StatusCode);
        var quotesPageDoc = await quotesPageRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(quotesPageDoc.GetProperty("items").GetArrayLength() >= 1);
        Assert.True(quotesPageDoc.GetProperty("totalCount").GetInt32() >= 1);

        var ordersPageRes = await client.GetAsync($"/api/v1/sales/legal-entities/{fixture.EntityA}/orders?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, ordersPageRes.StatusCode);
        var ordersPageDoc = await ordersPageRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(ordersPageDoc.GetProperty("items").GetArrayLength() >= 1);
        Assert.True(ordersPageDoc.GetProperty("totalCount").GetInt32() >= 1);

        // 8. Negative Authorization / Role Isolation Verification
        // Operator A does not have permission on Entity B
        var forbidRes = await fixture.PostAsync(client, $"/api/v1/sales/legal-entities/{fixture.EntityB}/orders/{orderId}/confirm", new
        {
            version = orderVersion + 1,
            idempotencyKey = $"conf-forbidden-{orderId:N}",
            reason = "Should be rejected"
        });
        Assert.True(forbidRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);

        // 9. Verify Committed PostgreSQL Database State
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();

        await using (var cmd = new NpgsqlCommand($"select state, current_revision_number from business_sales.quotation where tenant_id = '{fixture.Tenant.Value}' and id = '{quoteId}'", adminConn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Accepted", reader.GetString(0));
            Assert.Equal(1, reader.GetInt32(1));
        }

        await using (var cmd = new NpgsqlCommand($"select count(*) from business_sales.quotation_acceptance where tenant_id = '{fixture.Tenant.Value}' and quotation_id = '{quoteId}' and revision_number = 1", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        await using (var cmd = new NpgsqlCommand($"select commercial_state from business_sales.sales_order where tenant_id = '{fixture.Tenant.Value}' and id = '{orderId}'", adminConn))
        {
            var state = (string)(await cmd.ExecuteScalarAsync() ?? string.Empty);
            Assert.Equal("Confirmed", state);
        }

        await using (var cmd = new NpgsqlCommand($"select count(*) from business_sales.sales_conversion_link where tenant_id = '{fixture.Tenant.Value}' and source_id = '{quoteId}' and target_id = '{orderId}'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }
    }

    [Fact]
    public async Task Purchasing_EndToEndProcureToReceiptWorkflow_OrderApprovalAndReceivingHandoff()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var supplierId = Guid.CreateVersion7();

        // 1. Setup Supplier Party in Directory
        var setupSupplierSql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';

            insert into foundation_directory.party (tenant_id, id, party_type, status, display_name, display_name_normalized, created_by, updated_by, created_at, updated_at, version)
            values ('{fixture.Tenant.Value}', '{supplierId}', 'Organization', 'Active', 'Global Logistics Supplier', 'GLOBAL LOGISTICS SUPPLIER', '{fixture.IdentityId.Value}', '{fixture.IdentityId.Value}', now(), now(), 1);

            insert into foundation_directory.party_role (tenant_id, party_id, role_code, status, created_by, created_at, updated_at, version, active_from)
            values ('{fixture.Tenant.Value}', '{supplierId}', 'Supplier', 'Active', '{fixture.IdentityId.Value}', now(), now(), 1, CURRENT_DATE);

            insert into foundation_directory.party_visibility (tenant_id, party_id, scope_type, visibility_id)
            values ('{fixture.Tenant.Value}', '{supplierId}', 'TenantWide', '{Guid.CreateVersion7()}');

            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setupSupplierSql);

        // 2. Save Supplier Profile
        var saveProfileRes = await fixture.PutAsync(client, $"/api/v1/purchasing/legal-entities/{fixture.EntityA}/suppliers/{supplierId}/profile", new
        {
            legalEntityId = fixture.EntityA,
            supplierPartyId = supplierId,
            supplierCode = "SUP-E2E-01",
            defaultCurrencyCode = "EGP",
            supplierReferenceRequired = false
        });
        Assert.Equal(HttpStatusCode.OK, saveProfileRes.StatusCode);

        // 3. Create Purchase Order in PendingApproval state
        var createPoRes = await fixture.PostAsync(client, $"/api/v1/purchasing/legal-entities/{fixture.EntityA}/purchase-orders", new
        {
            legalEntityId = fixture.EntityA,
            branchId = fixture.BranchA1,
            supplierPartyId = supplierId,
            currencyCode = "EGP",
            orderDate = DateOnly.FromDateTime(DateTime.UtcNow),
            approvalRequired = true,
            lines = new[]
            {
                new
                {
                    description = "Procured Operating Inventory",
                    unitCode = "EA",
                    quantity = 10m,
                    unitPrice = 120m,
                    priceOverrideReason = "Agreed contract tariff"
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, createPoRes.StatusCode);
        var poId = (await createPoRes.Content.ReadFromJsonAsync<JsonElement>()).GetGuid();
        Assert.NotEqual(Guid.Empty, poId);

        // Verify initial PendingApproval state
        var getPoRes1 = await client.GetAsync($"/api/v1/purchasing/legal-entities/{fixture.EntityA}/purchase-orders/{poId}");
        Assert.Equal(HttpStatusCode.OK, getPoRes1.StatusCode);
        var poDoc1 = await getPoRes1.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingApproval", poDoc1.GetProperty("state").GetString());

        // 4. Approve Purchase Order
        var approvePoRes = await fixture.PostAsync(client, $"/api/v1/purchasing/legal-entities/{fixture.EntityA}/purchase-orders/{poId}/approve", new
        {
            version = 1L,
            idempotencyKey = $"po-appr-{poId:N}",
            reason = "Approved by procurement manager"
        });
        Assert.Equal(HttpStatusCode.OK, approvePoRes.StatusCode);

        // Verify Approved state
        var getPoRes2 = await client.GetAsync($"/api/v1/purchasing/legal-entities/{fixture.EntityA}/purchase-orders/{poId}");
        Assert.Equal(HttpStatusCode.OK, getPoRes2.StatusCode);
        var poDoc2 = await getPoRes2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", poDoc2.GetProperty("state").GetString());
        var poVersion2 = poDoc2.GetProperty("version").GetInt64();

        // 5. Create Supplier Invoice Link from Approved PO
        var createInvoiceRes = await fixture.PostAsync(client, $"/api/v1/purchasing/legal-entities/{fixture.EntityA}/purchase-orders/{poId}/create-supplier-invoice", new
        {
            version = poVersion2,
            idempotencyKey = $"sup-inv-{poId:N}",
            documentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            postingDate = DateOnly.FromDateTime(DateTime.UtcNow),
            taxDate = DateOnly.FromDateTime(DateTime.UtcNow),
            supplierInvoiceNumber = "SUP-INV-2026-001"
        });
        if (createInvoiceRes.StatusCode != HttpStatusCode.Created)
        {
            var err = await createInvoiceRes.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Create invoice failed: {createInvoiceRes.StatusCode} - {err}");
        }
        Assert.Equal(HttpStatusCode.Created, createInvoiceRes.StatusCode);
        var invoiceId = (await createInvoiceRes.Content.ReadFromJsonAsync<JsonElement>()).GetGuid();
        Assert.NotEqual(Guid.Empty, invoiceId);

        // Seed compliance snapshot for matching verification
        var poLineId = poDoc2.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var snapshotObj = new Shared.Contracts.Invoicing.InvoiceComplianceSnapshotContract(
            1,
            invoiceId,
            fixture.EntityA,
            "Payable",
            "Invoice",
            "SUP-INV-2026-001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow),
            null,
            "EGP",
            "Exclusive",
            "{}",
            null,
            null,
            "1200.00",
            "1200.00",
            "0.00",
            "0.00",
            "0.00",
            "1200.00",
            new[]
            {
                new Shared.Contracts.Invoicing.InvoiceFrozenLineContract(
                    Guid.CreateVersion7(),
                    1,
                    null,
                    "Procured Operating Inventory",
                    "10",
                    null,
                    "EA",
                    "120.00",
                    "120.00",
                    "{}",
                    "1200.00",
                    "0.00",
                    "1200.00",
                    Array.Empty<Shared.Contracts.Invoicing.InvoiceFrozenDiscountContract>(),
                    Array.Empty<Shared.Contracts.Invoicing.InvoiceFrozenTaxComponentContract>(),
                    poLineId.ToString("D")
                )
            },
            Array.Empty<Shared.Contracts.Invoicing.InvoiceFrozenChargeContract>(),
            Array.Empty<Shared.Contracts.Invoicing.InvoiceScheduleContract>()
        );
        var snapshotJson = JsonSerializer.Serialize(snapshotObj);
        await fixture.ExecuteAdminSqlAsync($@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';
            insert into business_invoicing.invoice_compliance_snapshot (tenant_id, legal_entity_id, invoice_id, schema_version, snapshot, created_at)
            values ('{fixture.Tenant.Value}', '{fixture.EntityA}', '{invoiceId}', 1, '{snapshotJson}'::jsonb, now());
            reset app.tenant_id;
            reset role;
        ");

        // 6. Execute Two-Way Matching between PO and Supplier Invoice
        var getPoRes3 = await client.GetAsync($"/api/v1/purchasing/legal-entities/{fixture.EntityA}/purchase-orders/{poId}");
        Assert.Equal(HttpStatusCode.OK, getPoRes3.StatusCode);
        var poDoc3 = await getPoRes3.Content.ReadFromJsonAsync<JsonElement>();
        var poVersion3 = poDoc3.GetProperty("version").GetInt64();

        var matchRes = await fixture.PostAsync(client, $"/api/v1/purchasing/legal-entities/{fixture.EntityA}/purchase-orders/{poId}/match", new
        {
            version = poVersion3,
            supplierInvoiceId = invoiceId,
            basis = "TwoWay",
            quantityTolerance = 0.5m,
            priceAbsoluteTolerance = 10m,
            pricePercentageTolerance = 5m,
            taxTolerance = 5m,
            idempotencyKey = $"match-{poId:N}"
        });
        Assert.Equal(HttpStatusCode.OK, matchRes.StatusCode);
        var matchDoc = await matchRes.Content.ReadFromJsonAsync<JsonElement>();
        var matchState = matchDoc.GetProperty("state").GetString();
        Assert.True(matchState is "Matched" or "WithinTolerance");

        // 7. Server-side Pagination Verification
        var poPageRes = await client.GetAsync($"/api/v1/purchasing/legal-entities/{fixture.EntityA}/purchase-orders?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, poPageRes.StatusCode);
        var poPageDoc = await poPageRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(poPageDoc.GetProperty("items").GetArrayLength() >= 1);
        Assert.True(poPageDoc.GetProperty("totalCount").GetInt32() >= 1);

        // 8. Negative Authorization / Role Isolation Verification
        var forbidRes = await fixture.PostAsync(client, $"/api/v1/purchasing/legal-entities/{fixture.EntityB}/purchase-orders/{poId}/approve", new
        {
            version = 1L,
            idempotencyKey = $"po-forbid-{poId:N}",
            reason = "Should fail cross-entity"
        });
        Assert.True(forbidRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);

        // 9. Verify Committed PostgreSQL Database State
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();

        await using (var cmd = new NpgsqlCommand($"select state, approval_state from business_purchasing.purchase_order where tenant_id = '{fixture.Tenant.Value}' and id = '{poId}'", adminConn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Approved", reader.GetString(0));
            Assert.Equal("Approved", reader.GetString(1));
        }

        await using (var cmd = new NpgsqlCommand($"select count(*) from business_purchasing.purchase_order_approval where tenant_id = '{fixture.Tenant.Value}' and purchase_order_id = '{poId}' and decision = 'Approved'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        await using (var cmd = new NpgsqlCommand($"select count(*) from business_purchasing.purchase_order_match_result where tenant_id = '{fixture.Tenant.Value}' and purchase_order_id = '{poId}'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }
    }

    [Fact]
    public async Task POS_EndToEndCheckoutWorkflow_SessionCartAndCashPaymentReconciliation()
    {
        await using var fixture = await NavSecurityFixture.CreateAsync();
        using var client = fixture.CreateClient();
        await fixture.LoginAsync(client);

        var paymentAccountId = Guid.CreateVersion7();
        var uomId = Guid.CreateVersion7();
        var templateId = Guid.CreateVersion7();
        var variantId = Guid.CreateVersion7();
        var priceListId = Guid.CreateVersion7();
        var priceListVersionId = Guid.CreateVersion7();
        var roundingPolicyId = Guid.CreateVersion7();

        // 1. Seed Payment Account, Catalog Item, Pricing, and Finbase Context in PostgreSQL
        var setupPosDataSql = $@"
            set role erp_owner;
            set app.tenant_id = '{fixture.Tenant.Value}';

            -- Finbase Rounding Policy & Financial Policy & Legal Entity Currency
            insert into foundation_finbase.financial_rounding_policy (tenant_id, id, code, name_ar, name_en, mode, money_scale, cash_increment, status, version, created_at, updated_at, line_document_strategy)
            values ('{fixture.Tenant.Value}', '{roundingPolicyId}', 'STANDARD', 'تقريب قياسي', 'Standard rounding', 'HalfUp', 2, 0.01000000, 'Active', 1, now(), now(), 'RoundEachLineThenSum');

            insert into foundation_finbase.financial_rounding_policy_rule (tenant_id, rounding_policy_id, rounding_context, mode)
            values
                ('{fixture.Tenant.Value}', '{roundingPolicyId}', 'MoneySettlement', 'HalfUp'),
                ('{fixture.Tenant.Value}', '{roundingPolicyId}', 'LineNetAmount', 'HalfUp'),
                ('{fixture.Tenant.Value}', '{roundingPolicyId}', 'DocumentSubtotal', 'HalfUp'),
                ('{fixture.Tenant.Value}', '{roundingPolicyId}', 'DocumentTotal', 'HalfUp'),
                ('{fixture.Tenant.Value}', '{roundingPolicyId}', 'FXBaseAmount', 'HalfUp'),
                ('{fixture.Tenant.Value}', '{roundingPolicyId}', 'PaymentScheduleLine', 'HalfUp'),
                ('{fixture.Tenant.Value}', '{roundingPolicyId}', 'PaymentAllocation', 'HalfUp'),
                ('{fixture.Tenant.Value}', '{roundingPolicyId}', 'CashRounding', 'HalfUp');

            insert into foundation_finbase.financial_policy (tenant_id, legal_entity_id, rounding_policy_id, exchange_rate_max_age_days, manual_rate_override_allowed, version, updated_at)
            values ('{fixture.Tenant.Value}', '{fixture.EntityA}', '{roundingPolicyId}', 7, false, 1, now());

            insert into foundation_finbase.legal_entity_currency (tenant_id, legal_entity_id, currency_code, enabled, allow_sales, allow_purchase, allow_payment, allow_manual_rate, status, version, created_at, updated_at)
            values ('{fixture.Tenant.Value}', '{fixture.EntityA}', 'EGP', true, true, true, true, false, 'Active', 1, now(), now());

            -- Payment Account & Method
            insert into finance_payments.payment_account (tenant_id, id, legal_entity_id, branch_id, code, account_type, status, currency_policy, currency_code, name, created_at, updated_at, version)
            values ('{fixture.Tenant.Value}', '{paymentAccountId}', '{fixture.EntityA}', '{fixture.BranchA1}', 'POS_CASH_01', 'Cashbox', 'Active', 'SingleCurrency', 'EGP', 'POS Drawer Cashbox', now(), now(), 1);

            insert into finance_payments.payment_method_config (tenant_id, id, legal_entity_id, method, status, effective_from, payment_account_id, branch_id, channel, created_at)
            values ('{fixture.Tenant.Value}', '{Guid.CreateVersion7()}', '{fixture.EntityA}', 'Cash', 'Active', CURRENT_DATE, '{paymentAccountId}', '{fixture.BranchA1}', 'POS', now());

            -- Catalog UOM, Template, Visibility, Variant, and Product UOM
            insert into foundation_catalog.uom (tenant_id, id, code, name_ar, name_en, dimension, allow_fraction, precision, status, version, created_at, updated_at)
            values ('{fixture.Tenant.Value}', '{uomId}', 'EA', 'قطعة', 'Each', 'Count', false, 0, 'Active', 1, now(), now());

            insert into foundation_catalog.product_template (tenant_id, id, display_name, display_name_normalized, name_ar, name_en, product_nature, status, created_at, updated_at, version, product_source, product_template_code)
            values ('{fixture.Tenant.Value}', '{templateId}', 'POS Retail Beverage', 'POS RETAIL BEVERAGE', 'مشروب تجزئة', 'POS Retail Beverage', 'Goods', 'Active', now(), now(), 1, 'Manual', 'PT-POS-E2E');

            insert into foundation_catalog.product_visibility (tenant_id, template_id, scope_type, visibility_id)
            values ('{fixture.Tenant.Value}', '{templateId}', 'TenantWide', '{Guid.CreateVersion7()}');

            insert into foundation_catalog.product_variant (tenant_id, id, product_template_id, sku, normalized_sku, is_default_variant, status, version, created_at, updated_at)
            values ('{fixture.Tenant.Value}', '{variantId}', '{templateId}', 'SKU-POS-E2E', 'SKU-POS-E2E', true, 'Active', 1, now(), now());

            insert into foundation_catalog.product_uom (tenant_id, id, variant_id, uom_id, factor_to_base, is_base, effective_from, status, version, usage_flags)
            values ('{fixture.Tenant.Value}', '{Guid.CreateVersion7()}', '{variantId}', '{uomId}', 1, true, now() - interval '1 day', 'Active', 1, '{{Selling,Buying}}');

            -- Pricing List, Version, and Price Entry
            insert into foundation_pricing.price_list (tenant_id, id, legal_entity_id, code, name, direction, currency_code, entity_scope_type, currency_policy, status, created_at, updated_at, version)
            values ('{fixture.Tenant.Value}', '{priceListId}', '{fixture.EntityA}', 'PL-POS-E2E', 'POS Retail Price List', 'Selling', 'EGP', 'SingleLegalEntity', 'ExactCurrency', 'Active', now(), now(), 1);

            insert into foundation_pricing.price_list_version (tenant_id, id, price_list_id, version_no, status, effective_from, created_by, created_at, version)
            values ('{fixture.Tenant.Value}', '{priceListVersionId}', '{priceListId}', 1, 'Draft', CURRENT_DATE, '{fixture.IdentityId.Value}', now(), 1);

            insert into foundation_pricing.price_entry (tenant_id, id, price_list_version_id, variant_id, uom_id, min_quantity, unit_price, status, version, created_at)
            values ('{fixture.Tenant.Value}', '{Guid.CreateVersion7()}', '{priceListVersionId}', '{variantId}', '{uomId}', 0, 50.00, 'Active', 1, now());

            update foundation_pricing.price_list_version set status = 'Active', activated_at = now() where id = '{priceListVersionId}';

            reset app.tenant_id;
            reset role;
        ";
        await fixture.ExecuteAdminSqlAsync(setupPosDataSql);

        // 2. Register Discovery & Terminal Registration
        var createRegRes = await fixture.PostAsync(client, "/api/v1/pos/registers", new
        {
            legalEntityId = fixture.EntityA,
            branchId = fixture.BranchA1,
            code = "REG_E2E_01",
            name = "E2E Checkout Register",
            currencyCode = "EGP",
            offlineEnabled = false
        });
        Assert.Equal(HttpStatusCode.Created, createRegRes.StatusCode);
        var regDoc = await createRegRes.Content.ReadFromJsonAsync<JsonElement>();
        var registerId = regDoc.GetProperty("id").GetGuid();

        var regTermRes = await fixture.PostAsync(client, $"/api/v1/pos/registers/{registerId}/terminals", new
        {
            registerId,
            deviceIdentity = "TERM_E2E_01",
            displayName = "E2E Terminal"
        });
        Assert.Equal(HttpStatusCode.Created, regTermRes.StatusCode);
        var termDoc = await regTermRes.Content.ReadFromJsonAsync<JsonElement>();
        var terminalId = termDoc.GetProperty("id").GetGuid();

        // 3. Open POS Session with Opening Float
        var openSessionRes = await fixture.PostAsync(client, $"/api/v1/pos/legal-entities/{fixture.EntityA}/sessions", new
        {
            registerId,
            terminalId,
            cashierUserId = fixture.IdentityId.Value,
            businessDate = DateOnly.FromDateTime(DateTime.UtcNow),
            openingFloat = 100.00m,
            currencyCode = "EGP"
        });
        Assert.Equal(HttpStatusCode.Created, openSessionRes.StatusCode);
        var sessionDoc = await openSessionRes.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = sessionDoc.GetProperty("id").GetGuid();
        Assert.Equal("Open", sessionDoc.GetProperty("status").GetString());
        Assert.Equal(100.00m, sessionDoc.GetProperty("openingFloat").GetDecimal());

        // 4. Create POS Cart
        var createCartRes = await fixture.PostAsync(client, "/api/v1/pos/carts", new
        {
            sessionId,
            currencyCode = "EGP"
        });
        Assert.Equal(HttpStatusCode.Created, createCartRes.StatusCode);
        var cartDoc = await createCartRes.Content.ReadFromJsonAsync<JsonElement>();
        var cartId = cartDoc.GetProperty("id").GetGuid();
        Assert.Equal("Active", cartDoc.GetProperty("state").GetString());

        // 5. Add Cart Line with authoritative pricing context
        var addLineRes = await fixture.PostAsync(client, $"/api/v1/pos/carts/{cartId}/lines", new
        {
            variantId,
            description = "POS Retail Beverage",
            uomCode = "EA",
            quantity = 2m,
            unitPrice = 50m,
            taxAmount = 0m,
            discountAmount = 0m,
            expectedVersion = 1,
            legalEntityId = fixture.EntityA,
            productTemplateId = templateId,
            unitOfMeasureId = uomId,
            priceListId = priceListId
        });
        Assert.Equal(HttpStatusCode.Created, addLineRes.StatusCode);
        var updatedCartDoc = await addLineRes.Content.ReadFromJsonAsync<JsonElement>();

        // Exact Money Math Verification: Subtotal + Tax - Discount = GrandTotal
        var subtotal = updatedCartDoc.GetProperty("subtotal").GetDecimal();
        var taxTotal = updatedCartDoc.GetProperty("taxTotal").GetDecimal();
        var discountTotal = updatedCartDoc.GetProperty("discountTotal").GetDecimal();
        var grandTotal = updatedCartDoc.GetProperty("grandTotal").GetDecimal();
        Assert.Equal(100.00m, subtotal);
        Assert.Equal(subtotal + taxTotal - discountTotal, grandTotal);

        // 6. Complete Cash Checkout
        var checkoutRes = await fixture.PostAsync(client, "/api/v1/pos/checkout", new
        {
            cartId,
            idempotencyKey = $"chk-{cartId:N}",
            tenders = new[]
            {
                new
                {
                    method = "Cash",
                    amount = grandTotal,
                    paymentAccountId = paymentAccountId
                }
            }
        });
        if (checkoutRes.StatusCode != HttpStatusCode.Accepted)
        {
            var err = await checkoutRes.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Checkout failed: {checkoutRes.StatusCode} - {err}");
        }
        Assert.Equal(HttpStatusCode.Accepted, checkoutRes.StatusCode);
        var checkoutDoc = await checkoutRes.Content.ReadFromJsonAsync<JsonElement>();
        var checkoutId = checkoutDoc.GetProperty("checkoutId").GetGuid();
        Assert.True(checkoutDoc.GetProperty("state").GetString() is "Completed" or "CompletionPending");
        Assert.Equal("Captured", checkoutDoc.GetProperty("paymentState").GetString());
        Assert.Equal(grandTotal, checkoutDoc.GetProperty("total").GetDecimal());

        // 7. Cash Count & Session Close
        var countedCash = 100.00m + grandTotal; // OpeningFloat + Cash captured
        var closeRes = await fixture.PostAsync(client, $"/api/v1/pos/sessions/{sessionId}/close", new
        {
            sessionId,
            countedCash,
            varianceReason = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, closeRes.StatusCode);
        var closedSessionDoc = await closeRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Closed", closedSessionDoc.GetProperty("status").GetString());
        Assert.Equal(0.00m, closedSessionDoc.GetProperty("variance").GetDecimal());

        // 8. Negative Role Isolation Verification
        // Attempting to query management reports on Entity B
        var forbidRes = await client.GetAsync($"/api/v1/pos/legal-entities/{fixture.EntityB}/reports/sales");
        Assert.True(forbidRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);

        // 9. Verify Committed PostgreSQL Database State
        await using var adminConn = new NpgsqlConnection(GetAdminConnectionString());
        await adminConn.OpenAsync();

        await using (var cmd = new NpgsqlCommand($"select state, total_amount from business_pos.pos_checkout where tenant_id = '{fixture.Tenant.Value}' and id = '{checkoutId}'", adminConn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetString(0) is "Completed" or "CompletionPending");
            Assert.Equal(grandTotal, reader.GetDecimal(1));
        }

        await using (var cmd = new NpgsqlCommand($"select count(*) from business_pos.pos_sale where tenant_id = '{fixture.Tenant.Value}' and checkout_id = '{checkoutId}'", adminConn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        await using (var cmd = new NpgsqlCommand($"select method, requested_amount from business_pos.pos_tender where tenant_id = '{fixture.Tenant.Value}' and checkout_id = '{checkoutId}'", adminConn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Cash", reader.GetString(0));
            Assert.Equal(grandTotal, reader.GetDecimal(1));
        }

        await using (var cmd = new NpgsqlCommand($"select status, variance from business_pos.pos_session where tenant_id = '{fixture.Tenant.Value}' and id = '{sessionId}'", adminConn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Closed", reader.GetString(0));
            Assert.Equal(0.00m, reader.GetDecimal(1));
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
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.customer_profile.manage'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.quotation.read'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.quotation.create'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.quotation.edit'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.quotation.open'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.quotation.accept'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.acceptance.record'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.quotation.convert_order'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.order.read'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.order.create'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.order.edit'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.order.confirm'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.price_override'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'sales.history.read'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.settings.manage'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.po.read'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.po.create'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.po.edit'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.po.approve'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.receipt.request'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.invoice.create_from_po'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.match.read'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.match.override'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.supplier_profile.manage'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.price.override'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.price.read'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'purchasing.history.read'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'pos.register.manage'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'pos.register.read'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'pos.session.open'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'pos.session.close'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'pos.cart.manage'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'pos.checkout.create'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'pos.cash.manage'),
                    ('{fixture.Tenant.Value}', '{roleOperatorA}', 'pos.report.read'),
                    ('{fixture.Tenant.Value}', '{roleBranchA1Only}', 'inventory.stock.read');

                insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type, scope_id)
                values
                    ('{fixture.Tenant.Value}', '{fixture.MembershipId.Value}', '{roleOperatorA}', 'LegalEntity', '{fixture.EntityA}'),
                    ('{fixture.Tenant.Value}', '{fixture.MembershipId.Value}', '{roleBranchA1Only}', 'Branch', '{fixture.BranchA1}');

                insert into platform.entitlement_snapshot (tenant_id, snapshot_version, issued_at, valid_until, key_id, modules, features, limits, scopes, signature)
                values ('{fixture.Tenant.Value}', 1, now(), now() + interval '30 days', 'test_key', '[""banking"", ""crm"", ""inventory"", ""accounting"", ""sales"", ""purchasing"", ""pos"", ""catalog"", ""pricing"", ""payments"", ""invoicing"", ""subledger""]'::jsonb, '[]'::jsonb, '{{}}'::jsonb, '[]'::jsonb, 'test_sig');

                insert into platform.module_instance (id, tenant_id, module_id, scope_type, scope_id, status, manifest_version, version)
                values
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'banking', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'crm', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'inventory', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'accounting', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'ledger', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'sales', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'purchasing', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'pos', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'catalog', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'pricing', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'payments', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'invoicing', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'subledger', 'tenant', null, 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'banking', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'crm', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'inventory', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'accounting', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'ledger', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'sales', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'purchasing', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'pos', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'catalog', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'pricing', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'payments', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'invoicing', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1),
                    ('{Guid.CreateVersion7()}', '{fixture.Tenant.Value}', 'subledger', 'legal_entity', '{fixture.EntityA}', 'Active', '1.0.0', 1);

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
                set session_replication_role = replica;
                set app.tenant_id = '{Tenant.Value}';

                -- Invoicing child tables
                delete from business_invoicing.invoice_line where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice_party_snapshot where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice_address_snapshot where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice_payment_schedule where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice_charge where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice_attachment where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice_relationship where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice_issued_artifact where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice_finalization_record where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice_number_reference where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice_list_projection where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice_compliance_snapshot where tenant_id = '{Tenant.Value}';
                delete from business_invoicing.invoice where tenant_id = '{Tenant.Value}';

                -- Sales child tables
                delete from business_sales.sales_credit_decision where tenant_id = '{Tenant.Value}';
                delete from business_sales.sales_order_reservation where tenant_id = '{Tenant.Value}';
                delete from business_sales.sales_reservation_consumption where tenant_id = '{Tenant.Value}';
                delete from business_sales.sales_fulfillment where tenant_id = '{Tenant.Value}';
                delete from business_sales.sales_document_state_history where tenant_id = '{Tenant.Value}';
                delete from business_sales.sales_conversion_link where tenant_id = '{Tenant.Value}';
                delete from business_sales.quotation_acceptance where tenant_id = '{Tenant.Value}';
                delete from business_sales.quotation_revision_line where tenant_id = '{Tenant.Value}';
                delete from business_sales.quotation_revision where tenant_id = '{Tenant.Value}';
                delete from business_sales.quotation where tenant_id = '{Tenant.Value}';
                delete from business_sales.sales_order_line where tenant_id = '{Tenant.Value}';
                delete from business_sales.sales_order where tenant_id = '{Tenant.Value}';
                delete from business_sales.sales_customer_profile where tenant_id = '{Tenant.Value}';

                -- Purchasing child tables
                delete from business_purchasing.purchase_order_match_result where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.purchase_order_receipt_link where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.purchase_order_invoice_link where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.purchase_order_amendment where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.purchase_order_approval where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.purchase_order_exception where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.purchase_order_state_history where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.purchase_order_workflow where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.purchase_order_list_projection where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.supplier_return_request where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.purchase_order_line where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.purchase_order where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.supplier_purchase_profile where tenant_id = '{Tenant.Value}';
                delete from business_purchasing.purchase_settings where tenant_id = '{Tenant.Value}';

                -- POS child tables
                delete from business_pos.pos_tender where tenant_id = '{Tenant.Value}';
                delete from business_pos.pos_sale_line where tenant_id = '{Tenant.Value}';
                delete from business_pos.pos_sale where tenant_id = '{Tenant.Value}';
                delete from business_pos.pos_checkout where tenant_id = '{Tenant.Value}';
                delete from business_pos.pos_cart_line where tenant_id = '{Tenant.Value}';
                delete from business_pos.pos_cart where tenant_id = '{Tenant.Value}';
                delete from business_pos.pos_cash_movement where tenant_id = '{Tenant.Value}';
                delete from business_pos.pos_session where tenant_id = '{Tenant.Value}';
                delete from business_pos.pos_terminal where tenant_id = '{Tenant.Value}';
                delete from business_pos.pos_register where tenant_id = '{Tenant.Value}';

                -- Banking child tables
                delete from finance_banking.treasury_account_balance_pocket where tenant_id = '{Tenant.Value}';
                delete from finance_banking.bank_account_profile where tenant_id = '{Tenant.Value}';
                delete from finance_banking.cash_account_profile where tenant_id = '{Tenant.Value}';
                delete from finance_banking.digital_wallet_profile where tenant_id = '{Tenant.Value}';
                delete from finance_banking.settlement_account_profile where tenant_id = '{Tenant.Value}';
                delete from finance_banking.treasury_account_change_request where tenant_id = '{Tenant.Value}';
                delete from finance_banking.treasury_account_connection where tenant_id = '{Tenant.Value}';
                delete from finance_banking.external_statement_line where tenant_id = '{Tenant.Value}';
                delete from finance_banking.external_statement where tenant_id = '{Tenant.Value}';
                delete from finance_banking.statement_import_batch where tenant_id = '{Tenant.Value}';
                delete from finance_banking.reconciliation_session where tenant_id = '{Tenant.Value}';
                delete from finance_banking.cash_count where tenant_id = '{Tenant.Value}';
                delete from finance_banking.cash_custodian_history where tenant_id = '{Tenant.Value}';
                delete from finance_banking.cash_position_projection where tenant_id = '{Tenant.Value}';
                delete from finance_banking.cash_forecast_item where tenant_id = '{Tenant.Value}';
                delete from finance_banking.external_feed_transaction where tenant_id = '{Tenant.Value}';
                delete from finance_banking.treasury_activity_projection where tenant_id = '{Tenant.Value}';
                delete from finance_banking.treasury_account_balance_projection where tenant_id = '{Tenant.Value}';
                delete from finance_banking.transfer_reconciliation_projection where tenant_id = '{Tenant.Value}';
                delete from finance_banking.settlement_account_projection where tenant_id = '{Tenant.Value}';
                delete from finance_banking.cash_forecast_projection where tenant_id = '{Tenant.Value}';
                delete from finance_banking.opening_reconciliation where tenant_id = '{Tenant.Value}';
                delete from finance_banking.pos_cash_settlement_projection where tenant_id = '{Tenant.Value}';
                delete from finance_banking.provider_settlement_projection where tenant_id = '{Tenant.Value}';
                delete from finance_payments.payment_method_config where tenant_id = '{Tenant.Value}';
                delete from finance_payments.payment where tenant_id = '{Tenant.Value}';
                delete from finance_payments.payment_account where tenant_id = '{Tenant.Value}';
                delete from finance_banking.treasury_account where tenant_id = '{Tenant.Value}';

                -- Accounting child tables
                delete from business_accounting.financial_statement_line where tenant_id = '{Tenant.Value}';
                delete from business_accounting.financial_statement_version where tenant_id = '{Tenant.Value}';
                delete from business_accounting.financial_statement_definition where tenant_id = '{Tenant.Value}';

                -- CRM, Ledger, Inventory child tables
                delete from business_crm.lead where tenant_id = '{Tenant.Value}';
                delete from finance_ledger.account_period_balance where tenant_id = '{Tenant.Value}';
                delete from finance_ledger.opening_balance_line where tenant_id = '{Tenant.Value}';
                delete from finance_ledger.general_journal_draft_line where tenant_id = '{Tenant.Value}';
                delete from finance_ledger.account_mapping_version where tenant_id = '{Tenant.Value}';
                delete from finance_ledger.journal_entry_line where tenant_id = '{Tenant.Value}';
                delete from finance_ledger.account where tenant_id = '{Tenant.Value}';

                delete from business_inventory.location where tenant_id = '{Tenant.Value}';
                delete from business_inventory.product_inventory_profile where tenant_id = '{Tenant.Value}';
                delete from business_inventory.reservation where tenant_id = '{Tenant.Value}';
                delete from business_inventory.inventory_transfer where tenant_id = '{Tenant.Value}';
                delete from business_inventory.physical_count where tenant_id = '{Tenant.Value}';
                delete from business_inventory.warehouse where tenant_id = '{Tenant.Value}';

                -- Finbase child tables
                delete from foundation_finbase.financial_policy where tenant_id = '{Tenant.Value}';
                delete from foundation_finbase.financial_rounding_policy_rule where tenant_id = '{Tenant.Value}';
                delete from foundation_finbase.financial_rounding_policy where tenant_id = '{Tenant.Value}';
                delete from foundation_finbase.legal_entity_currency where tenant_id = '{Tenant.Value}';

                -- Catalog, Pricing, Directory child tables
                delete from foundation_pricing.price_entry where tenant_id = '{Tenant.Value}';
                delete from foundation_pricing.price_list_version where tenant_id = '{Tenant.Value}';
                delete from foundation_pricing.price_list where tenant_id = '{Tenant.Value}';
                delete from foundation_catalog.product_visibility where tenant_id = '{Tenant.Value}';
                delete from foundation_catalog.product_uom where tenant_id = '{Tenant.Value}';
                delete from foundation_catalog.product_variant where tenant_id = '{Tenant.Value}';
                delete from foundation_catalog.product_template where tenant_id = '{Tenant.Value}';
                delete from foundation_catalog.uom where tenant_id = '{Tenant.Value}';
                delete from foundation_directory.party_visibility where tenant_id = '{Tenant.Value}';
                delete from foundation_directory.party_role where tenant_id = '{Tenant.Value}';
                delete from foundation_directory.party where tenant_id = '{Tenant.Value}';

                -- Platform messaging & audit
                delete from platform.outbox_message where tenant_id = '{Tenant.Value}';
                delete from platform.inbox_message where tenant_id = '{Tenant.Value}';
                delete from platform.audit_entry where tenant_id = '{Tenant.Value}';
                delete from platform.notification_delivery where tenant_id = '{Tenant.Value}';
                delete from platform.notification where tenant_id = '{Tenant.Value}';

                -- Platform kernel tables
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

                set session_replication_role = origin;
                reset app.tenant_id;
            ";
            await ExecuteAdminSqlAsync(cleanupSql);
        }
    }

    private sealed record CsrfResponse(string Token);
}
