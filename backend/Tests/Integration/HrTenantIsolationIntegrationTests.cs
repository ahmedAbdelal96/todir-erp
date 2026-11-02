using Npgsql;
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
        await transaction.RollbackAsync();
    }

    private static async Task AssertTenantCanSeeOnlyItsEmployeesAsync(NpgsqlDataSource dataSource, Guid tenantId, Guid otherTenantId)
    {
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

        await using var foreignRows = new NpgsqlCommand("select count(*) from business_hr.employee where tenant_id=$1", connection, transaction);
        foreignRows.Parameters.AddWithValue(otherTenantId);
        Assert.Equal(0L, Convert.ToInt64(await foreignRows.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        await transaction.RollbackAsync();
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
