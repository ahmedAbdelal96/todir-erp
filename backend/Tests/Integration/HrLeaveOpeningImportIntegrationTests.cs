using Core.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Hr.DTOs;
using Npgsql;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class HrLeaveOpeningImportIntegrationTests : DatabaseIntegrationTestBase
{
    [HrPostgresFact]
    public async Task OpeningEntitlementImportPreviewsCommitsIdempotentlyAndPreservesSourceEvidence()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for HR PostgreSQL integration tests.");
        var suffix = Guid.NewGuid().ToString("N");
        var password = $"HrLeaveImport-{suffix[..8]}1!";
        var email = $"hr-leave-import-{suffix}@atlas.com";
        var sourceSystem = $"HrImport{suffix[..8]}";
        var batchReference = $"batch-{suffix[..12]}";
        var fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(suffix))).ToLowerInvariant();
        var rowReference = "leave-row-1";
        var openingDate = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid tenantId;
        Guid legalEntityId;
        Guid employeeId;
        Guid employmentId;
        Guid leaveTypeId;
        var leaveTypeCode = $"OPEN-{suffix[..8].ToUpperInvariant()}";

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var transaction = await admin.BeginTransactionAsync();
            await using (var scope = new NpgsqlCommand("select t.id,e.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id and e.status='Active' where t.slug='atlas-trading' order by e.id limit 1", admin, transaction))
            await using (var reader = await scope.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The HR integration database must contain the canonical Atlas development seed.");
                tenantId = reader.GetGuid(0); legalEntityId = reader.GetGuid(1);
            }
            await using (var goLive = new NpgsqlCommand("insert into business_hr.legal_entity_go_live_configuration(tenant_id,legal_entity_id,go_live_date,employee_migration_strategy,attendance_history_boundary,leave_opening_boundary,document_migration_strategy,reason,updated_by,updated_at,version) values($1,$2,$3,'ActiveAndHistorical',null,$3,'AllAvailable','Opening leave balance migration test setup',$4,now(),1) on conflict(tenant_id,legal_entity_id) do update set go_live_date=excluded.go_live_date,employee_migration_strategy=excluded.employee_migration_strategy,leave_opening_boundary=excluded.leave_opening_boundary,document_migration_strategy=excluded.document_migration_strategy,reason=excluded.reason,updated_by=excluded.updated_by,updated_at=excluded.updated_at,version=business_hr.legal_entity_go_live_configuration.version+1", admin, transaction))
            {
                goLive.Parameters.AddWithValue(tenantId); goLive.Parameters.AddWithValue(legalEntityId); goLive.Parameters.AddWithValue(openingDate); goLive.Parameters.AddWithValue(Guid.CreateVersion7());
                await goLive.ExecuteNonQueryAsync();
            }
            await using (var employment = new NpgsqlCommand("select e.id,em.id from business_hr.employee e join business_hr.employment em on em.tenant_id=e.tenant_id and em.employee_id=e.id where e.tenant_id=$1 and e.status='Active' and em.legal_entity_id=$2 and em.status='Active' order by em.employment_number limit 1", admin, transaction))
            {
                employment.Parameters.AddWithValue(tenantId); employment.Parameters.AddWithValue(legalEntityId);
                await using var reader = await employment.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The canonical seed must contain an active HR employment for the opening-balance migration scenario.");
                employeeId = reader.GetGuid(0); employmentId = reader.GetGuid(1);
            }
            leaveTypeId = Guid.CreateVersion7();
            await using (var leaveType = new NpgsqlCommand("insert into business_hr.leave_type(id,tenant_id,legal_entity_id,code,name_ar,name_en,category,unit,status,created_by,created_at) values($1,$2,$3,$4,'رصيد افتتاحي للاختبار','Opening balance test','Annual','Days','Active',$5,now())", admin, transaction))
            {
                leaveType.Parameters.AddWithValue(leaveTypeId); leaveType.Parameters.AddWithValue(tenantId); leaveType.Parameters.AddWithValue(legalEntityId); leaveType.Parameters.AddWithValue(leaveTypeCode); leaveType.Parameters.AddWithValue(Guid.CreateVersion7());
                await leaveType.ExecuteNonQueryAsync();
            }
            await using (var legacyReference = new NpgsqlCommand("insert into business_hr.employee_import_reference(tenant_id,id,source_system,legacy_employee_id,batch_reference,file_sha256,row_reference,employee_id,employment_id,data_quality,imported_at,imported_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Complete',now(),$10)", admin, transaction))
            {
                legacyReference.Parameters.AddWithValue(tenantId); legacyReference.Parameters.AddWithValue(Guid.CreateVersion7()); legacyReference.Parameters.AddWithValue(sourceSystem); legacyReference.Parameters.AddWithValue("legacy-" + suffix[..12]);
                legacyReference.Parameters.AddWithValue("employee-batch-" + suffix[..8]); legacyReference.Parameters.AddWithValue(new string('b', 64)); legacyReference.Parameters.AddWithValue("employee-row");
                legacyReference.Parameters.AddWithValue(employeeId); legacyReference.Parameters.AddWithValue(employmentId); legacyReference.Parameters.AddWithValue(Guid.CreateVersion7());
                await legacyReference.ExecuteNonQueryAsync();
            }
            await CreateAccountAsync(admin, transaction, tenantId, email, password);
            await transaction.CommitAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, email, password, tenantId);
        var request = new HrLeaveOpeningImportRequest(sourceSystem, batchReference, fileHash,
        [new HrLeaveOpeningImportRow(rowReference, legalEntityId, "legacy-" + suffix[..12], leaveTypeCode, openingDate, 6.5m, "Approved leave entitlement at company migration cutoff")]);

        using var preview = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/leave-openings/preview", request);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewResult = await preview.Content.ReadFromJsonAsync<HrLeaveOpeningImportSummary>();
        Assert.NotNull(previewResult);
        Assert.Equal("Ready", Assert.Single(previewResult.Rows).Status);
        using var outsideBoundary = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/leave-openings/preview",
            request with { Rows = [request.Rows[0] with { EffectiveDate = openingDate.AddDays(-1) }] });
        Assert.Equal(HttpStatusCode.OK, outsideBoundary.StatusCode);
        Assert.Contains("hr.leave_opening_boundary_mismatch", Assert.Single((await outsideBoundary.Content.ReadFromJsonAsync<HrLeaveOpeningImportSummary>())!.Rows).Issues);

        using var committed = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/leave-openings/commit", request);
        Assert.True(committed.StatusCode == HttpStatusCode.OK, await committed.Content.ReadAsStringAsync());
        var commitResult = await committed.Content.ReadFromJsonAsync<HrLeaveOpeningImportSummary>();
        Assert.NotNull(commitResult);
        var imported = Assert.Single(commitResult.Rows);
        Assert.Equal("Imported", imported.Status);
        Assert.NotNull(imported.MovementId);

        using var replay = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/leave-openings/commit", request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayResult = await replay.Content.ReadFromJsonAsync<HrLeaveOpeningImportSummary>();
        Assert.NotNull(replayResult);
        Assert.Equal("AlreadyImported", Assert.Single(replayResult.Rows).Status);

        await using var verify = new NpgsqlConnection(adminConnectionString);
        await verify.OpenAsync();
        await using var evidence = new NpgsqlCommand("select movement.movement_type,movement.quantity,reference.file_sha256,reference.row_reference,(select count(*) from business_hr.leave_balance_movement duplicate where duplicate.tenant_id=movement.tenant_id and duplicate.employment_id=movement.employment_id and duplicate.leave_type_id=movement.leave_type_id and duplicate.movement_type='Opening') from business_hr.leave_balance_movement movement join business_hr.leave_import_reference reference on reference.tenant_id=movement.tenant_id and reference.movement_id=movement.id where movement.tenant_id=$1 and movement.id=$2", verify);
        evidence.Parameters.AddWithValue(tenantId); evidence.Parameters.AddWithValue(imported.MovementId!.Value);
        await using var evidenceReader = await evidence.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
        Assert.True(await evidenceReader.ReadAsync());
        Assert.Equal("Opening", evidenceReader.GetString(0));
        Assert.Equal(6.5m, evidenceReader.GetDecimal(1));
        Assert.Equal(fileHash, evidenceReader.GetString(2));
        Assert.Equal(rowReference, evidenceReader.GetString(3));
        Assert.Equal(1L, evidenceReader.GetInt64(4));
    }

    private static async Task CreateAccountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, string email, string password)
    {
        var identityId = Guid.CreateVersion7(); var membershipId = Guid.CreateVersion7();
        await using (var identity = new NpgsqlCommand("insert into platform.identity(id,tenant_id,email,normalized_email,login_prefix,name,email_verified,password_hash,must_change_password,status,created_at,security_stamp,version) values($1,$2,$3,$4,$5,'HR leave import integration',true,$6,false,'Active',now(),$7,1)", connection, transaction))
        {
            identity.Parameters.AddWithValue(identityId); identity.Parameters.AddWithValue(tenantId); identity.Parameters.AddWithValue(email); identity.Parameters.AddWithValue(IdentityConventions.NormalizeEmail(email));
            identity.Parameters.AddWithValue(email[..email.IndexOf('@')]); identity.Parameters.AddWithValue(IdentityConventions.HashPassword(password)); identity.Parameters.AddWithValue(Guid.CreateVersion7());
            await identity.ExecuteNonQueryAsync();
        }
        await using (var membership = new NpgsqlCommand("insert into platform.membership(id,tenant_id,identity_id,display_name,status,created_at,version) values($1,$2,$3,'HR leave import integration','Active',now(),1)", connection, transaction))
        {
            membership.Parameters.AddWithValue(membershipId); membership.Parameters.AddWithValue(tenantId); membership.Parameters.AddWithValue(identityId); await membership.ExecuteNonQueryAsync();
        }
        await using var assignment = new NpgsqlCommand("insert into platform.membership_role_assignment(tenant_id,membership_id,role_id,scope_type) select $1,$2,id,'Tenant' from platform.role where tenant_id=$1 and code='hr_privacy' and is_active", connection, transaction);
        assignment.Parameters.AddWithValue(tenantId); assignment.Parameters.AddWithValue(membershipId);
        if (await assignment.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("The canonical HR privacy role is missing from the HR integration seed.");
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var token = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(tenantId, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tenantId").GetGuid());
    }

    private static async Task<HttpResponseMessage> SendWithCsrfAsync<T>(HttpClient client, HttpMethod method, string path, T content)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(content) };
        request.Headers.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));
        return await client.SendAsync(request);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString() ?? throw new InvalidOperationException("The API did not issue a CSRF token.");
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
