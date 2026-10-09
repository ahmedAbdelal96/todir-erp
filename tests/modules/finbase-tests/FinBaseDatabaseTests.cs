using Npgsql;
using System.Globalization;
using Xunit;

namespace Todir.Modules.FinBase.Tests;

public sealed class FinBaseDatabaseTests
{
    [Fact]
    public async Task SchemaHasControlledCurrencyReferenceAndForcedTenantRls()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var currency = new NpgsqlCommand("select count(*) from foundation_finbase.currency_reference where status='Active'", connection);
        Assert.True(Convert.ToInt32(await currency.ExecuteScalarAsync(), CultureInfo.InvariantCulture) >= 7);
        await using var rls = new NpgsqlCommand("select count(*) from pg_class where relnamespace='foundation_finbase'::regnamespace and relrowsecurity and relforcerowsecurity", connection);
        Assert.True(Convert.ToInt32(await rls.ExecuteScalarAsync(), CultureInfo.InvariantCulture) >= 7);
    }
}
