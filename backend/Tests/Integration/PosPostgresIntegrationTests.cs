using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PosPostgresIntegrationTests : DatabaseIntegrationTestBase
{
    private static readonly Guid AtlasTenant = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");
    private static readonly string[] ReportIndexes = ["ix_pos_sale_report_order", "ix_pos_return_report_order"];

    [Fact]
    public async Task PosTablesUseForcedRlsAndRuntimeRoleCannotSeeRowsWithoutTenantContext()
    {
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for database integration tests.");
        await using var runtime = new NpgsqlConnection(runtimeConnectionString);
        await runtime.OpenAsync();

        await using (var empty = new NpgsqlCommand("select count(*) from business_pos.pos_register", runtime))
            Assert.Equal(0L, (long)(await empty.ExecuteScalarAsync())!);

        await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", runtime))
        {
            context.Parameters.AddWithValue(AtlasTenant.ToString("D"));
            await context.ExecuteNonQueryAsync();
        }

        await using (var visible = new NpgsqlCommand("select count(*) from business_pos.pos_register", runtime))
            Assert.True((long)(await visible.ExecuteScalarAsync())! > 0);

        const string requiredTables = "pos_register,pos_terminal,pos_session,pos_cash_movement,pos_cart,pos_cart_line,pos_checkout,pos_tender,pos_sale,pos_sale_line,pos_return,pos_return_line,pos_exchange_group,pos_manager_override,pos_offline_package,pos_offline_envelope,pos_sync_conflict";
        await using var policy = new NpgsqlCommand("select count(*) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='business_pos' and c.relkind='r' and c.relname = any(string_to_array($1,',')) and (not c.relrowsecurity or not c.relforcerowsecurity)", runtime);
        policy.Parameters.AddWithValue(requiredTables);
        Assert.Equal(0L, (long)(await policy.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task CheckoutIdempotencyIsTenantScopedAndExchangePermissionIsSeeded()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for database integration tests.");
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();

        await using (var index = new NpgsqlCommand("select count(*) from pg_indexes where schemaname='business_pos' and tablename='pos_checkout' and indexdef ilike '%(tenant_id, idempotency_key)%'", admin))
            Assert.Equal(1L, (long)(await index.ExecuteScalarAsync())!);

        await using var permission = new NpgsqlCommand("select count(*) from platform.role_permission where tenant_id=$1 and permission_code='pos.exchange.create'", admin);
        permission.Parameters.AddWithValue(AtlasTenant);
        Assert.True((long)(await permission.ExecuteScalarAsync())! > 0);
    }

    [Fact]
    public async Task PosReportOrderingIndexesAndRegisterBranchOwnershipArePresent()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for database integration tests.");
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();

        await using (var indexes = new NpgsqlCommand("select count(*) from pg_indexes where schemaname='business_pos' and indexname = any($1)", admin))
        {
            indexes.Parameters.AddWithValue(ReportIndexes);
            Assert.Equal(2L, (long)(await indexes.ExecuteScalarAsync())!);
        }

        await using var ownership = new NpgsqlCommand("select count(*) from business_pos.pos_register r join platform.branch b on b.tenant_id=r.tenant_id and b.id=r.branch_id where r.tenant_id=$1 and b.legal_entity_id<>r.legal_entity_id", admin);
        ownership.Parameters.AddWithValue(AtlasTenant);
        Assert.Equal(0L, (long)(await ownership.ExecuteScalarAsync())!);
    }
}
