using Core.Services;
using Core.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Hr.Repositories;
using Modules.Hr.Services;
using Npgsql;
using Shared.Primitives.Identifiers;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class HrReportExportIntegrationTests : DatabaseIntegrationTestBase
{
    [HrPostgresFact]
    public async Task ExpiryExportIsBoundedCsvAndWritesAuditEvidence()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for HR PostgreSQL integration tests.");
        Guid tenantId;
        Guid legalEntityId;
        Guid actorId;
        Guid membershipId;
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var scope = new NpgsqlCommand("select t.id,e.id,i.id,m.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id join platform.membership m on m.tenant_id=t.id and m.status='Active' join platform.identity i on i.tenant_id=m.tenant_id and i.id=m.identity_id and i.status='Active' where t.slug='atlas-trading' and e.status='Active' order by e.id,m.id limit 1", admin);
            await using var reader = await scope.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new InvalidOperationException("The HR integration database must contain the canonical Atlas development seed.");
            tenantId = reader.GetGuid(0); legalEntityId = reader.GetGuid(1); actorId = reader.GetGuid(2); membershipId = reader.GetGuid(3);
        }

        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var reports = new HrReportsRepository(dataSource, TimeProvider.System);
        var exports = new HrReportExportService(reports, new NpgsqlAuditWriter(dataSource), TimeProvider.System);
        var correlationId = Guid.CreateVersion7();
        var result = await exports.ExportExpiriesAsync(new TenantId(tenantId), actorId, membershipId, legalEntityId,
            DateOnly.FromDateTime(DateTime.UtcNow), correlationId, CancellationToken.None);
        var csv = System.Text.Encoding.UTF8.GetString(result.Content);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, result.Content[..3]);
        Assert.Equal("hr-expiries-" + DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + ".csv", result.FileName);
        Assert.Contains("تاريخ الانتهاء", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("encrypted", csv, StringComparison.OrdinalIgnoreCase);
        await using var auditConnection = new NpgsqlConnection(adminConnectionString);
        await auditConnection.OpenAsync();
        await using var audit = new NpgsqlCommand("select count(*) from platform.audit_entry where tenant_id=$1 and correlation_id=$2 and module='hr' and action='hr.report.exported' and entity_type='hr_report'", auditConnection);
        audit.Parameters.AddWithValue(tenantId); audit.Parameters.AddWithValue(correlationId);
        Assert.Equal(1L, Convert.ToInt64(await audit.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
    }

    [HrPostgresFact]
    public async Task ExpiryExportRequiresReportAndSeparateExportPermissions()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        Guid tenantId;
        Guid legalEntityId;
        var suffix = Guid.CreateVersion7().ToString("N");
        var password = $"HrExport-Test-{suffix[..8]}1!";
        var authorizedEmail = $"hr-export-{suffix}@atlas.com";
        var restrictedEmail = $"hr-report-{suffix}@atlas.com";
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var scope = new NpgsqlCommand("select t.id,e.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id and e.status='Active' where t.slug='atlas-trading' order by e.id limit 1", connection, transaction))
            await using (var reader = await scope.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The HR integration database must contain the canonical Atlas development seed.");
                tenantId = reader.GetGuid(0); legalEntityId = reader.GetGuid(1);
            }
            await CreateAccountAsync(connection, transaction, tenantId, legalEntityId, authorizedEmail, password, "hr_privacy");
            await CreateAccountAsync(connection, transaction, tenantId, legalEntityId, restrictedEmail, password, "hr_officer");
            await transaction.CommitAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var path = $"/api/v1/hr/reports/expiries/export?legalEntityId={legalEntityId:D}&asOfDate={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);

        using var authorized = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(authorized, authorizedEmail, password, tenantId);
        using var exported = await authorized.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.Equal("text/csv", exported.Content.Headers.ContentType?.MediaType);
        Assert.Contains("تاريخ الانتهاء", await exported.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var restricted = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(restricted, restrictedEmail, password, tenantId);
        Assert.Equal(HttpStatusCode.Forbidden, (await restricted.GetAsync(path)).StatusCode);
        var revealPath = $"/api/v1/hr/employees/{Guid.CreateVersion7():D}/identifiers/{Guid.CreateVersion7():D}/reveal?legalEntityId={legalEntityId:D}";
        using var csrfResponse = await restricted.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var revealRequest = new HttpRequestMessage(HttpMethod.Post, revealPath);
        revealRequest.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        using var revealResponse = await restricted.SendAsync(revealRequest);
        Assert.Equal(HttpStatusCode.Forbidden, revealResponse.StatusCode);
        using var balanceAdjustmentRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/hr/leave/balance-adjustments")
        {
            Content = JsonContent.Create(new { legalEntityId, employmentId = Guid.CreateVersion7(), leaveTypeId = Guid.CreateVersion7(), effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow), quantity = 1000m, reason = "Unauthorized entitlement change", idempotencyKey = "unauthorized-leave-adjustment" })
        };
        balanceAdjustmentRequest.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        using var balanceAdjustmentResponse = await restricted.SendAsync(balanceAdjustmentRequest);
        Assert.Equal(HttpStatusCode.Forbidden, balanceAdjustmentResponse.StatusCode);
    }

    private static async Task CreateAccountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid legalEntityId, string email, string password, string roleCode)
    {
        var identityId = Guid.CreateVersion7();
        var membershipId = Guid.CreateVersion7();
        var normalizedEmail = IdentityConventions.NormalizeEmail(email);
        await using (var identity = new NpgsqlCommand("insert into platform.identity(id,tenant_id,email,normalized_email,login_prefix,name,email_verified,password_hash,must_change_password,status,created_at,security_stamp,version) values($1,$2,$3,$4,$5,'HR export integration',true,$6,false,'Active',now(),$7,1)", connection, transaction))
        {
            identity.Parameters.AddWithValue(identityId); identity.Parameters.AddWithValue(tenantId); identity.Parameters.AddWithValue(email); identity.Parameters.AddWithValue(normalizedEmail);
            identity.Parameters.AddWithValue(email[..email.IndexOf('@')]); identity.Parameters.AddWithValue(IdentityConventions.HashPassword(password)); identity.Parameters.AddWithValue(Guid.CreateVersion7());
            await identity.ExecuteNonQueryAsync();
        }
        await using (var membership = new NpgsqlCommand("insert into platform.membership(id,tenant_id,identity_id,display_name,status,default_legal_entity_id,created_at,version) values($1,$2,$3,'HR export integration','Active',$4,now(),1)", connection, transaction))
        {
            membership.Parameters.AddWithValue(membershipId); membership.Parameters.AddWithValue(tenantId); membership.Parameters.AddWithValue(identityId); membership.Parameters.AddWithValue(legalEntityId);
            await membership.ExecuteNonQueryAsync();
        }
        await using var assignment = new NpgsqlCommand("insert into platform.membership_role_assignment(tenant_id,membership_id,role_id,scope_type,scope_id) select $1,$2,id,'LegalEntity',$4 from platform.role where tenant_id=$1 and code=$3 and is_active", connection, transaction);
        assignment.Parameters.AddWithValue(tenantId); assignment.Parameters.AddWithValue(membershipId); assignment.Parameters.AddWithValue(roleCode); assignment.Parameters.AddWithValue(legalEntityId);
        if (await assignment.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException($"The canonical role '{roleCode}' is missing from the HR integration seed.");
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        using var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(tenantId, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tenantId").GetGuid());
    }

    private sealed class HrPostgresFactAttribute : FactAttribute
    {
        public HrPostgresFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("TODIR_HR_TEST_DATABASE"), "1", StringComparison.Ordinal))
                Skip = "Set TODIR_HR_TEST_DATABASE=1 only for a disposable PostgreSQL database with the canonical development seed.";
        }
    }
}
