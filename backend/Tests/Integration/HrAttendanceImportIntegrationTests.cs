using Core.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Modules.Hr.DTOs;
using Npgsql;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class HrAttendanceImportIntegrationTests : DatabaseIntegrationTestBase
{
    [HrPostgresFact]
    public async Task AttendanceImportPreviewsTimezoneAndBoundaryThenCommitsAndReplaysWithoutDuplicatePunches()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for HR PostgreSQL integration tests.");
        var suffix = Guid.CreateVersion7().ToString("N");
        var password = $"HrAttendance-{suffix[..8]}1!";
        var email = $"hr-attendance-import-{suffix}@atlas.com";
        Guid tenantId;
        Guid legalEntityId;
        Guid employeeId;
        Guid actorId;
        Guid membershipId;
        string employmentNumber;
        string timeZoneId;

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var transaction = await admin.BeginTransactionAsync();
            await using (var seeded = new NpgsqlCommand("select tenant.id,employment.legal_entity_id,employee.id,employment.employment_number,calendar.time_zone_id,identity.id from platform.tenant tenant join business_hr.employment employment on employment.tenant_id=tenant.id and employment.status='Active' join business_hr.employee employee on employee.tenant_id=employment.tenant_id and employee.id=employment.employee_id join business_hr.employment_assignment assignment on assignment.tenant_id=employment.tenant_id and assignment.employment_id=employment.id and assignment.legal_entity_id=employment.legal_entity_id and assignment.work_calendar_id is not null join business_hr.work_calendar calendar on calendar.tenant_id=assignment.tenant_id and calendar.id=assignment.work_calendar_id and calendar.status='Active' join platform.identity identity on identity.tenant_id=tenant.id and identity.normalized_email='ADMIN@ATLAS.COM' where tenant.slug='atlas-trading' and assignment.effective_from<=current_date and (assignment.effective_to is null or assignment.effective_to>current_date) order by employment.employment_number limit 1", admin, transaction))
            await using (var reader = await seeded.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow))
            {
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The HR integration database must contain an active Atlas employment with a work calendar.");
                tenantId = reader.GetGuid(0);
                legalEntityId = reader.GetGuid(1);
                employeeId = reader.GetGuid(2);
                employmentNumber = reader.GetString(3);
                timeZoneId = reader.GetString(4);
                actorId = reader.GetGuid(5);
            }
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var workDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime).AddDays(-2);
            await using (var permission = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,role.id,permission.code from platform.role role cross join unnest(array['hr.attendance.manage','hr.employee.document.manage','hr.employee.document.read']::text[]) permission(code) where role.tenant_id=$1 and role.code='hr_privacy' and role.is_active on conflict do nothing", admin, transaction))
            {
                permission.Parameters.AddWithValue(tenantId);
                await permission.ExecuteNonQueryAsync();
            }
            membershipId = await CreateAccountAsync(admin, transaction, tenantId, email, password);
            await using (var goLive = new NpgsqlCommand("insert into business_hr.legal_entity_go_live_configuration(tenant_id,legal_entity_id,go_live_date,employee_migration_strategy,attendance_history_boundary,leave_opening_boundary,leave_history_boundary,document_migration_strategy,reason,updated_by,updated_at,version) values($1,$2,$3,'ActiveAndHistorical',$4,null,null,'AllAvailable','Attendance import integration boundary', $5, now(),1) on conflict (tenant_id,legal_entity_id) do update set go_live_date=excluded.go_live_date,attendance_history_boundary=excluded.attendance_history_boundary,reason=excluded.reason,updated_by=excluded.updated_by,updated_at=greatest(now(),business_hr.legal_entity_go_live_configuration.updated_at+interval '1 microsecond'),version=business_hr.legal_entity_go_live_configuration.version+1", admin, transaction))
            {
                goLive.Parameters.AddWithValue(tenantId);
                goLive.Parameters.AddWithValue(legalEntityId);
                goLive.Parameters.AddWithValue(workDate.AddDays(2));
                goLive.Parameters.AddWithValue(workDate);
                goLive.Parameters.AddWithValue(actorId);
                await goLive.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }

        var zoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var importDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zoneInfo).DateTime).AddDays(-2);
        var sourceOne = $"terminal-{suffix}-in";
        var sourceTwo = $"terminal-{suffix}-out";
        var atNine = ToUtc(importDate, new TimeOnly(9, 0), zoneInfo);
        var atFive = ToUtc(importDate, new TimeOnly(17, 0), zoneInfo);
        var request = new HrAttendanceImportRequest(legalEntityId, "attendance-device-a", $"batch-{suffix[..16]}",
            Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(suffix))).ToLowerInvariant(),
            [
                new("punch-in", employmentNumber, atNine, "In", sourceOne),
                new("punch-out", employmentNumber, atFive, "Out", sourceTwo),
                new("duplicate-in", employmentNumber, atNine, "In", sourceOne),
                new("unknown-employee", "UNKNOWN-EMPLOYEE", atNine, "In", $"missing-{suffix}"),
                new("outside-boundary", employmentNumber, ToUtc(importDate.AddDays(1), new TimeOnly(9, 0), zoneInfo), "In", $"outside-{suffix}")
            ]);

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)
            .ConfigureLogging(logging => logging.AddConsole()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, email, password, tenantId);

        using var previewResponse = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/attendance/imports/preview", request);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<HrAttendanceImportSummary>();
        Assert.NotNull(preview);
        Assert.True(preview.ReadyRows == 2, string.Join(" | ", preview.Rows.Select(row => $"{row.RowReference}:{row.Status}:{string.Join(',', row.Issues)}")));
        Assert.Equal(3, preview.RejectedRows);
        Assert.Equal(timeZoneId, preview.Rows[0].TimeZoneId);
        Assert.Equal(importDate, preview.Rows[0].WorkDate);
        Assert.Contains("hr.attendance_import_duplicate_source_in_batch", preview.Rows[2].Issues);
        Assert.Contains("hr.employment_not_found_for_attendance_import", preview.Rows[3].Issues);
        Assert.Contains("hr.attendance_history_boundary_not_allowed", preview.Rows[4].Issues);

        using var commitResponse = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/attendance/imports/commit", request);
        Assert.True(commitResponse.StatusCode == HttpStatusCode.OK, await commitResponse.Content.ReadAsStringAsync());
        var committed = await commitResponse.Content.ReadFromJsonAsync<HrAttendanceImportSummary>();
        Assert.NotNull(committed);
        Assert.Equal(2, committed.ImportedRows);
        Assert.Equal(3, committed.RejectedRows);

        using var attendanceResponse = await client.GetAsync($"/api/v1/hr/attendance-records?legalEntityId={legalEntityId:D}&from={importDate:yyyy-MM-dd}&to={importDate:yyyy-MM-dd}&page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, attendanceResponse.StatusCode);
        var records = await attendanceResponse.Content.ReadFromJsonAsync<AttendancePage>();
        Assert.NotNull(records);
        var record = Assert.Single(records.Items, item => item.EmployeeId == employeeId);
        Assert.Equal("Present", record.Status);
        Assert.Equal(420, record.WorkedMinutes);

        using var replayResponse = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/attendance/imports/commit", request);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replay = await replayResponse.Content.ReadFromJsonAsync<HrAttendanceImportSummary>();
        Assert.NotNull(replay);
        Assert.Equal(2, replay.AlreadyImportedRows);
        Assert.Equal(3, replay.RejectedRows);

        await using var verify = new NpgsqlConnection(adminConnectionString);
        await verify.OpenAsync();
        await using var imported = new NpgsqlCommand("select count(*) from business_hr.attendance_import_reference where tenant_id=$1 and source_system='ATTENDANCE-DEVICE-A' and batch_reference=$2", verify);
        imported.Parameters.AddWithValue(tenantId);
        imported.Parameters.AddWithValue(request.BatchReference);
        Assert.Equal(2L, Convert.ToInt64(await imported.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));

        using var rulesResponse = await client.GetAsync($"/api/v1/hr/document-requirements?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, rulesResponse.StatusCode);
        var requirements = await rulesResponse.Content.ReadFromJsonAsync<IReadOnlyList<RequiredDocumentRuleView>>();
        Assert.NotNull(requirements);
        var nationalId = Assert.Single(requirements, rule => rule.Category == "National ID");
        var employmentContract = Assert.Single(requirements, rule => rule.Category == "Employment Contract");
        using var disableRequirement = await SendWithCsrfAsync(client, HttpMethod.Put, "/api/v1/hr/document-requirements",
            new SaveRequiredDocumentRuleRequest(legalEntityId, nationalId.Category, false, nationalId.Version, "Integration scenario"));
        Assert.Equal(HttpStatusCode.OK, disableRequirement.StatusCode);
        using var disableContractRequirement = await SendWithCsrfAsync(client, HttpMethod.Put, "/api/v1/hr/document-requirements",
            new SaveRequiredDocumentRuleRequest(legalEntityId, employmentContract.Category, false, employmentContract.Version, "Integration scenario"));
        Assert.Equal(HttpStatusCode.OK, disableContractRequirement.StatusCode);
        var proofCategory = $"Test Certificate {suffix[..8]}";
        using var addRequirement = await SendWithCsrfAsync(client, HttpMethod.Put, "/api/v1/hr/document-requirements",
            new SaveRequiredDocumentRuleRequest(legalEntityId, proofCategory, true, 0, "Integration scenario"));
        Assert.True(addRequirement.StatusCode == HttpStatusCode.OK, await addRequirement.Content.ReadAsStringAsync());
        using var incompleteResponse = await client.GetAsync($"/api/v1/hr/employees/{employeeId:D}/document-completeness?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, incompleteResponse.StatusCode);
        var incomplete = await incompleteResponse.Content.ReadFromJsonAsync<EmployeeFileCompletenessView>();
        Assert.NotNull(incomplete);
        Assert.Equal("Incomplete", incomplete.Status);
        Assert.Equal(1, incomplete.MissingRequiredCount);
        Assert.Equal(proofCategory, Assert.Single(incomplete.MissingCategories));
        var expiringBefore = incomplete.ExpiringDocumentCount;
        using var staleRequirement = await SendWithCsrfAsync(client, HttpMethod.Put, "/api/v1/hr/document-requirements",
            new SaveRequiredDocumentRuleRequest(legalEntityId, proofCategory, false, 0, "Stale integration update"));
        Assert.Equal(HttpStatusCode.Conflict, staleRequirement.StatusCode);

        var fileRef = Guid.CreateVersion7();
        var documentId = Guid.CreateVersion7();
        var fileBytes = new byte[] { 0x48, 0x52, 0x31 };
        var fileHash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var transaction = await admin.BeginTransactionAsync();
            await using (var file = new NpgsqlCommand("insert into platform.file_record(id,tenant_id,owner_module,owner_entity_type,owner_entity_id,object_key,original_name,content_type,size,sha256,status,classification,created_by,created_at) values($1,$2,'hr','employee',$3,$4,'required-proof.pdf','application/pdf',$5,$6,'Ready','confidential',$7,now())", admin, transaction))
            {
                file.Parameters.AddWithValue(fileRef); file.Parameters.AddWithValue(tenantId); file.Parameters.AddWithValue(employeeId);
                file.Parameters.AddWithValue($"tenant/{tenantId:D}/hr/employee/{employeeId:D}/{fileRef:D}");
                file.Parameters.AddWithValue((long)fileBytes.Length); file.Parameters.AddWithValue(fileHash); file.Parameters.AddWithValue(actorId);
                await file.ExecuteNonQueryAsync();
            }
            await using (var document = new NpgsqlCommand("insert into business_hr.employee_document(id,tenant_id,employee_id,category,sensitivity,file_ref,sha256,content_type,size_bytes,expiry_date,verification_status,status,created_by,created_at,version) values($1,$2,$3,$4,'Normal',$5,$6,'application/pdf',$7,current_date+15,'Verified','Active',$8,now(),1)", admin, transaction))
            {
                document.Parameters.AddWithValue(documentId); document.Parameters.AddWithValue(tenantId); document.Parameters.AddWithValue(employeeId);
                document.Parameters.AddWithValue(proofCategory); document.Parameters.AddWithValue(fileRef); document.Parameters.AddWithValue(fileHash);
                document.Parameters.AddWithValue((long)fileBytes.Length); document.Parameters.AddWithValue(actorId); await document.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        using var completeResponse = await client.GetAsync($"/api/v1/hr/employees/{employeeId:D}/document-completeness?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var complete = await completeResponse.Content.ReadFromJsonAsync<EmployeeFileCompletenessView>();
        Assert.NotNull(complete);
        Assert.Equal("Complete", complete.Status);
        Assert.Equal(0, complete.MissingRequiredCount);
        Assert.Equal(expiringBefore + 1, complete.ExpiringDocumentCount);
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    private static async Task<Guid> CreateAccountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, string email, string password)
    {
        var identityId = Guid.CreateVersion7();
        var membershipId = Guid.CreateVersion7();
        await using (var identity = new NpgsqlCommand("insert into platform.identity(id,tenant_id,email,normalized_email,login_prefix,name,email_verified,password_hash,must_change_password,status,created_at,security_stamp,version) values($1,$2,$3,$4,$5,'HR attendance integration',true,$6,false,'Active',now(),$7,1)", connection, transaction))
        {
            identity.Parameters.AddWithValue(identityId); identity.Parameters.AddWithValue(tenantId); identity.Parameters.AddWithValue(email); identity.Parameters.AddWithValue(IdentityConventions.NormalizeEmail(email));
            identity.Parameters.AddWithValue(email[..email.IndexOf('@')]); identity.Parameters.AddWithValue(IdentityConventions.HashPassword(password)); identity.Parameters.AddWithValue(Guid.CreateVersion7());
            await identity.ExecuteNonQueryAsync();
        }
        await using (var membership = new NpgsqlCommand("insert into platform.membership(id,tenant_id,identity_id,display_name,status,created_at,version) values($1,$2,$3,'HR attendance integration','Active',now(),1)", connection, transaction))
        {
            membership.Parameters.AddWithValue(membershipId); membership.Parameters.AddWithValue(tenantId); membership.Parameters.AddWithValue(identityId); await membership.ExecuteNonQueryAsync();
        }
        await using var assignment = new NpgsqlCommand("insert into platform.membership_role_assignment(tenant_id,membership_id,role_id,scope_type) select $1,$2,id,'Tenant' from platform.role where tenant_id=$1 and code='hr_privacy' and is_active", connection, transaction);
        assignment.Parameters.AddWithValue(tenantId); assignment.Parameters.AddWithValue(membershipId);
        if (await assignment.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("The canonical HR privacy role is missing.");
        return membershipId;
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
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()
            ?? throw new InvalidOperationException("The API did not issue a CSRF token.");
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
