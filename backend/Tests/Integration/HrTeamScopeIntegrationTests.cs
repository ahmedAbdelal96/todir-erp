using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class HrTeamScopeIntegrationTests : DatabaseIntegrationTestBase
{
    [HrPostgresFact]
    public async Task ManagerSeesOnlyEffectiveDirectReportsAndCannotReadAnotherLegalEntity()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION to the non-owner erp_app role for HR PostgreSQL integration tests.");
        var seedPassword = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD")
            ?? throw new InvalidOperationException("Set SEED_DEFAULT_PASSWORD to the canonical disposable development seed password.");
        Guid tenantId;
        Guid legalEntityId;
        Guid otherLegalEntityId;
        Guid managerEmploymentId;
        Guid membershipId;
        HashSet<Guid> expectedEmployeeIds;

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var scope = new NpgsqlCommand("""
                select tenant.id,employment.legal_entity_id,employment.id,membership.id
                from platform.tenant tenant
                join platform.identity identity on identity.tenant_id=tenant.id and identity.normalized_email='HR-MANAGER@ATLAS.COM'
                join platform.membership membership on membership.tenant_id=identity.tenant_id and membership.identity_id=identity.id and membership.status='Active'
                join business_hr.employee employee on employee.tenant_id=membership.tenant_id and employee.membership_id=membership.id
                join business_hr.employment employment on employment.tenant_id=employee.tenant_id and employment.employee_id=employee.id
                where tenant.slug='atlas-trading' and employment.employment_number='EMP-000001'
                  and employment.status='Active' and employment.end_date is null
                limit 1
                """, admin);
            await using (var reader = await scope.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow))
            {
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The canonical Atlas seed must contain the HR manager linked to an active employee.");
                tenantId = reader.GetGuid(0);
                legalEntityId = reader.GetGuid(1);
                managerEmploymentId = reader.GetGuid(2);
                membershipId = reader.GetGuid(3);
            }

            await using (var otherEntity = new NpgsqlCommand("select id from platform.legal_entity where tenant_id=$1 and status='Active' and id<>$2 order by id limit 1", admin))
            {
                otherEntity.Parameters.AddWithValue(tenantId);
                otherEntity.Parameters.AddWithValue(legalEntityId);
                otherLegalEntityId = (Guid?)await otherEntity.ExecuteScalarAsync()
                    ?? throw new InvalidOperationException("The canonical Atlas seed must contain a second legal entity for manager scope testing.");
            }

            expectedEmployeeIds = [];
            await using var reports = new NpgsqlCommand("""
                select distinct employee.id
                from business_hr.employment_assignment assignment
                join business_hr.employment employment on employment.tenant_id=assignment.tenant_id and employment.id=assignment.employment_id
                join business_hr.employee employee on employee.tenant_id=employment.tenant_id and employee.id=employment.employee_id
                where assignment.tenant_id=$1 and assignment.legal_entity_id=$2 and assignment.manager_employment_id=$3
                  and assignment.effective_from<=current_date and (assignment.effective_to is null or assignment.effective_to>current_date)
                  and employment.status='Active' and employment.start_date<=current_date and employment.end_date is null
                """, admin);
            reports.Parameters.AddWithValue(tenantId);
            reports.Parameters.AddWithValue(legalEntityId);
            reports.Parameters.AddWithValue(managerEmploymentId);
            await using (var reader = await reports.ExecuteReaderAsync())
                while (await reader.ReadAsync()) expectedEmployeeIds.Add(reader.GetGuid(0));
        }

        Assert.NotEmpty(expectedEmployeeIds);
        Assert.NotEqual(Guid.Empty, membershipId);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, "hr-manager@atlas.com", seedPassword, tenantId);

        var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow);
        using var teamResponse = await client.GetAsync($"/api/v1/hr/me/team?legalEntityId={legalEntityId:D}&asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, teamResponse.StatusCode);
        var team = await teamResponse.Content.ReadFromJsonAsync<JsonElement>();
        var actualEmployeeIds = team.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("employeeId").GetGuid()).ToHashSet();
        Assert.True(expectedEmployeeIds.SetEquals(actualEmployeeIds));
        Assert.Equal(expectedEmployeeIds.Count, team.GetProperty("totalCount").GetInt32());
        var serializedTeam = team.GetRawText();
        Assert.DoesNotContain("salary", serializedTeam, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("compensation", serializedTeam, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("insuranceMember", serializedTeam, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nationalId", serializedTeam, StringComparison.OrdinalIgnoreCase);

        using var otherEntityResponse = await client.GetAsync($"/api/v1/hr/me/team?legalEntityId={otherLegalEntityId:D}&asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.Forbidden, otherEntityResponse.StatusCode);

        var guessedEmployeeId = expectedEmployeeIds.First();
        using var guessedProfile = await client.GetAsync($"/api/v1/hr/employees/{guessedEmployeeId:D}?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, guessedProfile.StatusCode);

        var firstTeamMember = team.GetProperty("items")[0];
        var teamEmploymentId = firstTeamMember.GetProperty("employmentId").GetGuid();
        using var identifiers = await client.GetAsync($"/api/v1/hr/employees/{guessedEmployeeId:D}/identifiers?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, identifiers.StatusCode);
        using var insurance = await client.GetAsync($"/api/v1/hr/employees/{guessedEmployeeId:D}/employments/{teamEmploymentId:D}/insurance?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, insurance.StatusCode);
        using var compensation = await client.GetAsync($"/api/v1/hr/employments/{teamEmploymentId:D}/compensation?employeeId={guessedEmployeeId:D}&legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, compensation.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        using var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var token = (await csrfResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()
            ?? throw new InvalidOperationException("The API did not issue a CSRF token.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", token);
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
