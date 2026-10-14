using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class DirectoryDetailContractTests
{
    [Fact]
    public async Task DetailReturnsVisiblePartyAndIdenticalNotFoundForMissingOrOtherTenantParty()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the Directory detail contract integration test.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for the Directory detail contract integration test.");
        const string password = "DirectoryContract-Only-For-Integration1";
        var configuredAppConnection = new NpgsqlConnectionStringBuilder(runtimeConnectionString);
        var configuredAppPassword = configuredAppConnection.Password
            ?? throw new InvalidOperationException("TODIR_DATABASE_CONNECTION must configure the runtime role password.");
        var testAppPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        configuredAppConnection.Password = testAppPassword;
        var testRuntimeConnectionString = configuredAppConnection.ConnectionString;
        var suffix = Guid.CreateVersion7().ToString("N");
        var atlasEmail = $"directory-contract-{suffix}@atlas.com";
        var deltaEmail = $"directory-contract-{suffix}@delta.com";
        var atlasIdentity = Guid.CreateVersion7();
        var deltaIdentity = Guid.CreateVersion7();
        var atlasMembership = Guid.CreateVersion7();
        var deltaMembership = Guid.CreateVersion7();
        var atlasRole = Guid.CreateVersion7();
        var deltaRole = Guid.CreateVersion7();
        var atlasTenant = Guid.Empty;
        var deltaTenant = Guid.Empty;
        var atlasPartyId = Guid.Empty;
        var atlasPartyName = string.Empty;
        var usersCreated = false;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await SetApplicationPasswordAsync(admin, testAppPassword);
        try
        {
            await using var transaction = await admin.BeginTransactionAsync();
            await using (var tenants = new NpgsqlCommand("select id, normalized_login_domain from platform.tenant where normalized_login_domain in ('atlas.com','delta.com')", admin, transaction))
            await using (var reader = await tenants.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1) == "atlas.com") atlasTenant = reader.GetGuid(0);
                    if (reader.GetString(1) == "delta.com") deltaTenant = reader.GetGuid(0);
                }
            }
            Assert.NotEqual(Guid.Empty, atlasTenant);
            Assert.NotEqual(Guid.Empty, deltaTenant);
            await using (var party = new NpgsqlCommand("select p.id,p.display_name from foundation_directory.party p join foundation_directory.party_visibility v on v.tenant_id=p.tenant_id and v.party_id=p.id where p.tenant_id=$1 and v.scope_type='TenantWide' and p.status<>'Merged' order by p.id limit 1", admin, transaction))
            {
                party.Parameters.AddWithValue(atlasTenant);
                await using var reader = await party.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync(), "Canonical Atlas seed must include a tenant-wide visible Party.");
                atlasPartyId = reader.GetGuid(0);
                atlasPartyName = reader.GetString(1);
            }

            await using (var setRole = new NpgsqlCommand("set role erp_owner", admin, transaction)) await setRole.ExecuteNonQueryAsync();
            await CreateDirectoryReaderAsync(admin, transaction, atlasTenant, atlasIdentity, atlasMembership, atlasRole, atlasEmail, suffix, password);
            await CreateDirectoryReaderAsync(admin, transaction, deltaTenant, deltaIdentity, deltaMembership, deltaRole, deltaEmail, suffix, password);
            await using (var resetRole = new NpgsqlCommand("reset role", admin, transaction)) await resetRole.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            usersCreated = true;
        }
        catch
        {
            await SetApplicationPasswordAsync(admin, configuredAppPassword);
            throw;
        }

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", testRuntimeConnectionString)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
            using var atlasClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(atlasClient, atlasEmail, password, atlasTenant);
            var visibleResponse = await atlasClient.GetAsync($"/api/v1/directory/parties/{atlasPartyId:D}");
            Assert.Equal(HttpStatusCode.OK, visibleResponse.StatusCode);
            var visibleBody = await visibleResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(atlasPartyId, visibleBody.GetProperty("id").GetGuid());
            Assert.Equal(atlasPartyName, visibleBody.GetProperty("displayName").GetString());
            var readiness = await atlasClient.GetAsync($"/api/v1/directory/parties/{atlasPartyId:D}/readiness");
            Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
            Assert.Equal(atlasPartyId, (await readiness.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("partyId").GetGuid());

            var missingId = Guid.CreateVersion7();
            var absentResponse = await atlasClient.GetAsync($"/api/v1/directory/parties/{missingId:D}");
            var absentBody = await absentResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.NotFound, absentResponse.StatusCode);

            using var deltaClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(deltaClient, deltaEmail, password, deltaTenant);
            var crossTenantResponse = await deltaClient.GetAsync($"/api/v1/directory/parties/{atlasPartyId:D}");
            var crossTenantBody = await crossTenantResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.NotFound, crossTenantResponse.StatusCode);
            Assert.Equal(absentBody, crossTenantBody);
            Assert.DoesNotContain(atlasPartyName, crossTenantBody, StringComparison.Ordinal);
            Assert.DoesNotContain(atlasPartyId.ToString("D"), crossTenantBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                if (usersCreated)
                {
                    await using var cleanup = new NpgsqlConnection(adminConnectionString);
                    await cleanup.OpenAsync();
                    await using var transaction = await cleanup.BeginTransactionAsync();
                    foreach (var (tenantId, identityId, membershipId, roleId) in new[]
                    {
                        (atlasTenant, atlasIdentity, atlasMembership, atlasRole),
                        (deltaTenant, deltaIdentity, deltaMembership, deltaRole)
                    })
                    {
                        await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", cleanup, transaction);
                        setTenant.Parameters.AddWithValue(tenantId.ToString("D"));
                        await setTenant.ExecuteNonQueryAsync();
                        await using var setRole = new NpgsqlCommand("set role erp_owner", cleanup, transaction);
                        await setRole.ExecuteNonQueryAsync();
                        await DeleteDirectoryReaderAsync(cleanup, transaction, tenantId, identityId, membershipId, roleId);
                        await using var resetRole = new NpgsqlCommand("reset role", cleanup, transaction);
                        await resetRole.ExecuteNonQueryAsync();
                    }
                    await transaction.CommitAsync();
                }
            }
            finally { await SetApplicationPasswordAsync(admin, configuredAppPassword); }
        }
    }

    private static async Task CreateDirectoryReaderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid identityId, Guid membershipId, Guid roleId, string email, string suffix, string password)
    {
        await using (var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            setTenant.Parameters.AddWithValue(tenantId.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();
        }
        await using (var identity = new NpgsqlCommand("insert into platform.identity (id,tenant_id,email,normalized_email,login_prefix,name,email_verified,password_hash,status,created_at,security_stamp,version) values ($1,$2,$3,$4,$5,'Directory contract reader',true,$6,'Active',now(),$7,1)", connection, transaction))
        {
            identity.Parameters.AddWithValue(identityId); identity.Parameters.AddWithValue(tenantId); identity.Parameters.AddWithValue(email); identity.Parameters.AddWithValue(email.ToUpperInvariant()); identity.Parameters.AddWithValue($"directory-contract-{suffix}"); identity.Parameters.AddWithValue(IdentityConventions.HashPassword(password)); identity.Parameters.AddWithValue(Guid.CreateVersion7());
            await identity.ExecuteNonQueryAsync();
        }
        await using (var membership = new NpgsqlCommand("insert into platform.membership (id,tenant_id,identity_id,display_name,status,created_at,version) values ($1,$2,$3,'Directory contract reader','Active',now(),1)", connection, transaction))
        {
            membership.Parameters.AddWithValue(membershipId); membership.Parameters.AddWithValue(tenantId); membership.Parameters.AddWithValue(identityId); await membership.ExecuteNonQueryAsync();
        }
        await using (var role = new NpgsqlCommand("insert into platform.role (id,tenant_id,code,name,is_system_template,is_active,version) values ($1,$2,$3,'Directory contract reader',false,true,1)", connection, transaction))
        {
            role.Parameters.AddWithValue(roleId); role.Parameters.AddWithValue(tenantId); role.Parameters.AddWithValue($"directory-contract-{suffix}"); await role.ExecuteNonQueryAsync();
        }
        await using (var permission = new NpgsqlCommand("insert into platform.role_permission (tenant_id,role_id,permission_code) values ($1,$2,'directory.party.read')", connection, transaction))
        {
            permission.Parameters.AddWithValue(tenantId); permission.Parameters.AddWithValue(roleId); await permission.ExecuteNonQueryAsync();
        }
        await using var assignment = new NpgsqlCommand("insert into platform.membership_role_assignment (tenant_id,membership_id,role_id,scope_type) values ($1,$2,$3,'Tenant')", connection, transaction);
        assignment.Parameters.AddWithValue(tenantId); assignment.Parameters.AddWithValue(membershipId); assignment.Parameters.AddWithValue(roleId); await assignment.ExecuteNonQueryAsync();
    }

    private static async Task DeleteDirectoryReaderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid identityId, Guid membershipId, Guid roleId)
    {
        foreach (var (sql, id) in new[]
        {
            ("delete from platform.membership_role_assignment where tenant_id=$1 and membership_id=$2", membershipId),
            ("delete from platform.role_permission where tenant_id=$1 and role_id=$2", roleId),
            ("delete from platform.role where tenant_id=$1 and id=$2", roleId),
            ("delete from platform.membership where tenant_id=$1 and id=$2", membershipId),
            ("delete from platform.identity where tenant_id=$1 and id=$2", identityId)
        })
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(id); await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task SetApplicationPasswordAsync(NpgsqlConnection connection, string password)
    {
        var escaped = password.Replace("'", "''", StringComparison.Ordinal);
        await using var command = new NpgsqlCommand($"alter role erp_app password '{escaped}'", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var response = await PostAsync(client, "/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Authenticated", result.GetProperty("state").GetString());
        Assert.Equal(tenantId, result.GetProperty("tenantId").GetGuid());
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
    {
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfResponse>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await client.SendAsync(request);
    }

    private sealed record CsrfResponse(string Token);
}
