using Core.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class HrMembershipLinkIntegrationTests : DatabaseIntegrationTestBase
{
    [HrPostgresFact]
    public async Task AuthorizedRelinkUpdatesSelfServiceAccessAndWritesHistory()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for HR PostgreSQL integration tests.");
        var suffix = Guid.CreateVersion7().ToString("N");
        var password = $"HrLink-Test-{suffix[..8]}1!";
        var actorEmail = $"hr-link-actor-{suffix}@atlas.com";
        var restrictedEmail = $"hr-link-restricted-{suffix}@atlas.com";
        var targetEmail = $"hr-link-target-{suffix}@atlas.com";
        Guid tenantId;
        Guid legalEntityId;
        Guid otherLegalEntityId;
        Guid employeeId;
        Guid employmentId;
        Guid otherEmployeeId;
        Guid targetMembershipId;
        long expectedVersion;
        long otherExpectedVersion;

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
            await using (var otherEntity = new NpgsqlCommand("select id from platform.legal_entity where tenant_id=$1 and status='Active' and id<>$2 order by id limit 1", admin, transaction))
            {
                otherEntity.Parameters.AddWithValue(tenantId); otherEntity.Parameters.AddWithValue(legalEntityId);
                otherLegalEntityId = (Guid?)await otherEntity.ExecuteScalarAsync() ?? throw new InvalidOperationException("The canonical seed must contain a second legal entity for the HR authorization scenario.");
            }
            await using (var employee = new NpgsqlCommand("select e.id,e.version,em.id from business_hr.employee e join business_hr.employment em on em.tenant_id=e.tenant_id and em.employee_id=e.id where e.tenant_id=$1 and e.status='Active' and e.membership_id is null and em.legal_entity_id=$2 and em.status='Active' order by em.employment_number limit 1", admin, transaction))
            {
                employee.Parameters.AddWithValue(tenantId); employee.Parameters.AddWithValue(legalEntityId);
                await using var reader = await employee.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The canonical seed must contain an active unlinked employee for the HR membership-link scenario.");
                employeeId = reader.GetGuid(0); expectedVersion = reader.GetInt64(1); employmentId = reader.GetGuid(2);
            }
            await using (var otherEmployee = new NpgsqlCommand("select e.id,e.version from business_hr.employee e join business_hr.employment em on em.tenant_id=e.tenant_id and em.employee_id=e.id where e.tenant_id=$1 and e.status='Active' and e.membership_id is null and em.legal_entity_id=$2 and em.status='Active' and e.id<>$3 order by em.employment_number limit 1", admin, transaction))
            {
                otherEmployee.Parameters.AddWithValue(tenantId); otherEmployee.Parameters.AddWithValue(legalEntityId); otherEmployee.Parameters.AddWithValue(employeeId);
                await using var reader = await otherEmployee.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The canonical seed must contain another active unlinked employee for the HR membership uniqueness scenario.");
                otherEmployeeId = reader.GetGuid(0); otherExpectedVersion = reader.GetInt64(1);
            }
            await using (var permission = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,id,'hr.employee.link_user' from platform.role where tenant_id=$1 and code='hr_privacy' and is_active on conflict do nothing", admin, transaction))
            {
                permission.Parameters.AddWithValue(tenantId);
                await permission.ExecuteNonQueryAsync();
            }
            await using (var role = new NpgsqlCommand("select exists(select 1 from platform.role where tenant_id=$1 and code='hr_privacy' and is_active)", admin, transaction))
            {
                role.Parameters.AddWithValue(tenantId);
                if (!(bool)(await role.ExecuteScalarAsync() ?? false)) throw new InvalidOperationException("The canonical HR privacy role is missing from the HR integration seed.");
            }
            await CreateAccountAsync(admin, transaction, tenantId, legalEntityId, actorEmail, password, "hr_privacy", tenantScope: true);
            await CreateAccountAsync(admin, transaction, tenantId, legalEntityId, restrictedEmail, password, "hr_officer");
            targetMembershipId = await CreateAccountAsync(admin, transaction, tenantId, legalEntityId, targetEmail, password, "hr_employee_self");
            await transaction.CommitAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var restricted = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(restricted, restrictedEmail, password, tenantId);
        using var restrictedRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/hr/employees/{employeeId:D}/membership")
        {
            Content = JsonContent.Create(new { membershipId = targetMembershipId, expectedVersion, reason = "Permission must be required" })
        };
        restrictedRequest.Headers.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(restricted));
        using var restrictedResponse = await restricted.SendAsync(restrictedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, restrictedResponse.StatusCode);
        using var restrictedGoLiveConfiguration = await restricted.GetAsync($"/api/v1/hr/legal-entities/{legalEntityId:D}/go-live");
        Assert.Equal(HttpStatusCode.Forbidden, restrictedGoLiveConfiguration.StatusCode);
        using var salaryResponse = await restricted.GetAsync($"/api/v1/hr/employments/{employmentId:D}/compensation?employeeId={employeeId:D}&legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, salaryResponse.StatusCode);
        using var crossEntityRead = await restricted.GetAsync($"/api/v1/hr/employees/{employeeId:D}?legalEntityId={otherLegalEntityId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, crossEntityRead.StatusCode);

        using var actor = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(actor, actorEmail, password, tenantId);
        var profileToken = await GetCsrfTokenAsync(actor);
        using var profileCorrection = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/hr/employees/{employeeId:D}/profile?legalEntityId={legalEntityId:D}")
        {
            Content = JsonContent.Create(new { arabicName = "تصحيح ملف الموارد البشرية", englishName = "HR Profile Correction", preferredName = (string?)null, expectedVersion, reason = "Corrected canonical employee name" })
        };
        profileCorrection.Headers.Add("X-CSRF-TOKEN", profileToken);
        using var profileResponse = await actor.SendAsync(profileCorrection);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profileResult = await profileResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedVersion + 1, profileResult.GetProperty("version").GetInt64());
        expectedVersion++;

        var staleProfileToken = await GetCsrfTokenAsync(actor);
        using var staleProfileCorrection = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/hr/employees/{employeeId:D}/profile?legalEntityId={legalEntityId:D}")
        {
            Content = JsonContent.Create(new { arabicName = "تصحيح قديم", englishName = (string?)null, preferredName = (string?)null, expectedVersion = expectedVersion - 1, reason = "Stale profile correction must be rejected" })
        };
        staleProfileCorrection.Headers.Add("X-CSRF-TOKEN", staleProfileToken);
        using var staleProfileResponse = await actor.SendAsync(staleProfileCorrection);
        Assert.Equal(HttpStatusCode.Conflict, staleProfileResponse.StatusCode);

        var csrfToken = await GetCsrfTokenAsync(actor);
        using var relink = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/hr/employees/{employeeId:D}/membership")
        {
            Content = JsonContent.Create(new { membershipId = targetMembershipId, expectedVersion, reason = "Corrected employee account association" })
        };
        relink.Headers.Add("X-CSRF-TOKEN", csrfToken);
        using var relinkResponse = await actor.SendAsync(relink);
        Assert.Equal(HttpStatusCode.OK, relinkResponse.StatusCode);
        var result = await relinkResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("isLinked").GetBoolean());
        Assert.Equal(expectedVersion + 1, result.GetProperty("version").GetInt64());

        var duplicateToken = await GetCsrfTokenAsync(actor);
        using var duplicateLink = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/hr/employees/{otherEmployeeId:D}/membership")
        {
            Content = JsonContent.Create(new { membershipId = targetMembershipId, expectedVersion = otherExpectedVersion, reason = "Must not assign one account to two employees" })
        };
        duplicateLink.Headers.Add("X-CSRF-TOKEN", duplicateToken);
        using var duplicateResponse = await actor.SendAsync(duplicateLink);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var target = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(target, targetEmail, password, tenantId);
        using var profile = await target.GetAsync($"/api/v1/hr/me/profile?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal(employeeId, (await profile.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        using var guessedEmployee = await target.GetAsync($"/api/v1/hr/employees/{otherEmployeeId:D}?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, guessedEmployee.StatusCode);
        var targetProfileToken = await GetCsrfTokenAsync(target);
        using var unauthorizedProfileCorrection = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/hr/employees/{employeeId:D}/profile?legalEntityId={legalEntityId:D}")
        {
            Content = JsonContent.Create(new { arabicName = "محاولة غير مصرح بها", englishName = (string?)null, preferredName = (string?)null, expectedVersion, reason = "Self-service must not edit another employee" })
        };
        unauthorizedProfileCorrection.Headers.Add("X-CSRF-TOKEN", targetProfileToken);
        using var unauthorizedProfileResponse = await target.SendAsync(unauthorizedProfileCorrection);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedProfileResponse.StatusCode);

        await using var auditConnection = new NpgsqlConnection(adminConnectionString);
        await auditConnection.OpenAsync();
        await using var evidence = new NpgsqlCommand("select (select count(*) from business_hr.employment_event where tenant_id=$1 and employee_id=$2 and event_type='EmployeeMembershipLinked'),(select count(*) from business_hr.employment_event where tenant_id=$1 and employee_id=$2 and event_type='EmployeeProfileCorrected'),(select count(*) from platform.audit_entry where tenant_id=$1 and module='hr' and action='employee.membership_link_changed' and entity_id=$2),(select count(*) from platform.audit_entry where tenant_id=$1 and module='hr' and action='employee.profile_corrected' and entity_id=$2)", auditConnection);
        evidence.Parameters.AddWithValue(tenantId); evidence.Parameters.AddWithValue(employeeId);
        await using var evidenceReader = await evidence.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
        Assert.True(await evidenceReader.ReadAsync());
        Assert.Equal(1L, evidenceReader.GetInt64(0));
        Assert.Equal(1L, evidenceReader.GetInt64(1));
        Assert.Equal(1L, evidenceReader.GetInt64(2));
        Assert.Equal(1L, evidenceReader.GetInt64(3));
    }

    private static async Task<Guid> CreateAccountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid legalEntityId, string email, string password, string roleCode, bool tenantScope = false)
    {
        var identityId = Guid.CreateVersion7();
        var membershipId = Guid.CreateVersion7();
        var normalizedEmail = IdentityConventions.NormalizeEmail(email);
        await using (var identity = new NpgsqlCommand("insert into platform.identity(id,tenant_id,email,normalized_email,login_prefix,name,email_verified,password_hash,must_change_password,status,created_at,security_stamp,version) values($1,$2,$3,$4,$5,'HR membership link integration',true,$6,false,'Active',now(),$7,1)", connection, transaction))
        {
            identity.Parameters.AddWithValue(identityId); identity.Parameters.AddWithValue(tenantId); identity.Parameters.AddWithValue(email); identity.Parameters.AddWithValue(normalizedEmail);
            identity.Parameters.AddWithValue(email[..email.IndexOf('@')]); identity.Parameters.AddWithValue(IdentityConventions.HashPassword(password)); identity.Parameters.AddWithValue(Guid.CreateVersion7());
            await identity.ExecuteNonQueryAsync();
        }
        await using (var membership = new NpgsqlCommand("insert into platform.membership(id,tenant_id,identity_id,display_name,status,default_legal_entity_id,created_at,version) values($1,$2,$3,'HR membership link integration','Active',$4,now(),1)", connection, transaction))
        {
            membership.Parameters.AddWithValue(membershipId); membership.Parameters.AddWithValue(tenantId); membership.Parameters.AddWithValue(identityId); membership.Parameters.AddWithValue(legalEntityId);
            await membership.ExecuteNonQueryAsync();
        }
        await using var assignment = new NpgsqlCommand("insert into platform.membership_role_assignment(tenant_id,membership_id,role_id,scope_type,scope_id) select $1,$2,id,$4,$5::uuid from platform.role where tenant_id=$1 and code=$3 and is_active", connection, transaction);
        assignment.Parameters.AddWithValue(tenantId); assignment.Parameters.AddWithValue(membershipId); assignment.Parameters.AddWithValue(roleCode);
        assignment.Parameters.AddWithValue(tenantScope ? "Tenant" : "LegalEntity"); assignment.Parameters.AddWithValue(tenantScope ? DBNull.Value : legalEntityId);
        if (await assignment.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException($"The canonical role '{roleCode}' is missing from the HR integration seed.");
        return membershipId;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(tenantId, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tenantId").GetGuid());
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
