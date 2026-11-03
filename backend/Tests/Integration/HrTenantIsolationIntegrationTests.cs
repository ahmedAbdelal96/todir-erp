using Npgsql;
using Modules.Hr.DTOs;
using Modules.Hr.Repositories;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class HrTenantIsolationIntegrationTests : DatabaseIntegrationTestBase
{
    [HrPostgresFact]
    public async Task EmployeeRowsRequireAndRespectTheActiveTenantContext()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimePassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        Guid atlasTenant;
        Guid deltaTenant;
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using (var setPassword = new NpgsqlCommand($"alter role erp_app password '{runtimePassword}'", admin))
                await setPassword.ExecuteNonQueryAsync();
            await using var find = new NpgsqlCommand("select id from platform.tenant where slug in ('atlas-trading','delta-supplies') order by slug", admin);
            await using var reader = await find.ExecuteReaderAsync();
            var tenants = new List<Guid>();
            while (await reader.ReadAsync()) tenants.Add(reader.GetGuid(0));
            if (tenants.Count != 2) throw new InvalidOperationException("The disposable HR integration database must contain both canonical development tenants.");
            atlasTenant = tenants[0];
            deltaTenant = tenants[1];
        }

        var runtimeConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "erp_app",
            Password = runtimePassword
        }.ConnectionString;
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        await AssertNoTenantCannotSeeEmployeesAsync(dataSource);
        await AssertTenantCanSeeOnlyItsEmployeesAsync(dataSource, atlasTenant, deltaTenant);
        await AssertTenantCanSeeOnlyItsEmployeesAsync(dataSource, deltaTenant, atlasTenant);
    }

    private static async Task AssertNoTenantCannotSeeEmployeesAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var query = new NpgsqlCommand("select count(*) from business_hr.employee", connection, transaction);
        Assert.Equal(0L, Convert.ToInt64(await query.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        await using var requirements = new NpgsqlCommand("select count(*) from business_hr.employee_document_requirement", connection, transaction);
        Assert.Equal(0L, Convert.ToInt64(await requirements.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        await transaction.RollbackAsync();
    }

    private static async Task AssertTenantCanSeeOnlyItsEmployeesAsync(NpgsqlDataSource dataSource, Guid tenantId, Guid otherTenantId)
    {
        Guid legalEntityId;
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            setTenant.Parameters.AddWithValue(tenantId.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();
        }

        await using var ownRows = new NpgsqlCommand("select count(*) from business_hr.employee where tenant_id=$1", connection, transaction);
        ownRows.Parameters.AddWithValue(tenantId);
        Assert.True(Convert.ToInt64(await ownRows.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) > 0);

        await using (var entity = new NpgsqlCommand("select legal_entity_id from business_hr.employment where tenant_id=$1 and employment_number='EMP-000001' order by start_date desc,id limit 1", connection, transaction))
        {
            entity.Parameters.AddWithValue(tenantId);
            legalEntityId = (Guid?)await entity.ExecuteScalarAsync() ?? throw new InvalidOperationException("The canonical HR seed must include an active legal entity.");
        }

        await using var ownRequirements = new NpgsqlCommand("select count(*) from business_hr.employee_document_requirement where tenant_id=$1 and enabled", connection, transaction);
        ownRequirements.Parameters.AddWithValue(tenantId);
        Assert.True(Convert.ToInt64(await ownRequirements.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) > 0);

        await using var foreignRequirements = new NpgsqlCommand("select count(*) from business_hr.employee_document_requirement where tenant_id=$1", connection, transaction);
        foreignRequirements.Parameters.AddWithValue(otherTenantId);
        Assert.Equal(0L, Convert.ToInt64(await foreignRequirements.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));

        await using var foreignRows = new NpgsqlCommand("select count(*) from business_hr.employee where tenant_id=$1", connection, transaction);
        foreignRows.Parameters.AddWithValue(otherTenantId);
        Assert.Equal(0L, Convert.ToInt64(await foreignRows.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        await transaction.RollbackAsync();

        var repository = new HrRepository(null!, dataSource, null!, null!, null!, null!, null!);
        var tenant = new TenantId(tenantId);
        var scopedCodeSearch = await repository.ListEmployeesAsync(tenant, new EmployeePageRequest("EMP-000001", legalEntityId, 1, 10), CancellationToken.None);
        Assert.Contains(scopedCodeSearch.Items, employee => employee.EmploymentNumber == "EMP-000001");
        Assert.True(scopedCodeSearch.TotalCount > 0);

        var unscopedCodeSearch = await repository.ListEmployeesAsync(tenant, new EmployeePageRequest("EMP-000001", null, 1, 10), CancellationToken.None);
        Assert.Contains(unscopedCodeSearch.Items, employee => employee.Id == scopedCodeSearch.Items.Single(item => item.EmploymentNumber == "EMP-000001").Id);
        Assert.True(unscopedCodeSearch.TotalCount > 0);

        var scopedPage = await repository.ListEmployeesAsync(tenant, new EmployeePageRequest(null, legalEntityId, 1, 10), CancellationToken.None);
        Assert.True(scopedPage.TotalCount >= scopedPage.Items.Count);
        Assert.All(scopedPage.Items, employee => Assert.Equal(legalEntityId, employee.LegalEntityId));

        var allEmployeesPage = await repository.ListEmployeesAsync(tenant, new EmployeePageRequest(null, null, 1, 10), CancellationToken.None);
        Assert.True(allEmployeesPage.TotalCount >= allEmployeesPage.Items.Count);
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
