using Npgsql;

namespace DbMigrator.SeedData;

public sealed record SeedTenant(Guid Id, string Slug, string DisplayName, string LoginDomain, string LegalName, string? TradeName, string BaseCurrency);

public sealed class SeedContext(NpgsqlConnection connection, NpgsqlTransaction transaction, SeedTenant tenant, DateTimeOffset asOf) : IAsyncDisposable
{
    public NpgsqlConnection Connection { get; } = connection;
    public NpgsqlTransaction Transaction { get; } = transaction;
    public SeedTenant Tenant { get; } = tenant;
    public DateTimeOffset AsOf { get; } = asOf;
    public Guid EntityId => SeedIds.TenantEntity(Tenant.Id);
    public Guid BranchId => SeedIds.TenantBranch(Tenant.Id);
    public Guid AdministratorId => SeedIds.Identity(Tenant.Id, "admin");
    public Guid AdministratorMembershipId => SeedIds.Membership(Tenant.Id, "admin");

    public static async Task<SeedContext> OpenAsync(NpgsqlConnection connection, SeedTenant tenant, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var role = new NpgsqlCommand("set local role erp_owner", connection, transaction))
                await role.ExecuteNonQueryAsync(cancellationToken);
            await using (var context = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
            {
                context.Parameters.AddWithValue(tenant.Id.ToString("D"));
                await context.ExecuteNonQueryAsync(cancellationToken);
            }
            return new SeedContext(connection, transaction, tenant, asOf);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task<int> ExecuteAsync(string sql, CancellationToken cancellationToken, params object?[] values)
    {
        await using var command = new NpgsqlCommand(sql, Connection, Transaction);
        for (var index = 0; index < values.Length; index++) command.Parameters.AddWithValue(values[index] ?? DBNull.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<T> ScalarAsync<T>(string sql, CancellationToken cancellationToken, params object?[] values)
    {
        await using var command = new NpgsqlCommand(sql, Connection, Transaction);
        for (var index = 0; index < values.Length; index++) command.Parameters.AddWithValue(values[index] ?? DBNull.Value);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull) throw new InvalidOperationException("Development seed expected a database value but received none.");
        return (T)Convert.ChangeType(result, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task CommitAsync(CancellationToken cancellationToken) => await Transaction.CommitAsync(cancellationToken);

    public async Task RollbackAsync(CancellationToken cancellationToken) => await Transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => Transaction.DisposeAsync();
}
