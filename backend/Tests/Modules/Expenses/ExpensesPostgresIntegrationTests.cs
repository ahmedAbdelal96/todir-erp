using Modules.Expenses.Repositories;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Modules.Expenses.Tests;

public sealed class ExpensesPostgresIntegrationTests
{
    private static readonly Guid Atlas = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");
    private static readonly Guid Delta = Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb");

    [ExpensesPostgresFact]
    public async Task SeededEmployeeExpenseHasSingleReimbursementObligation()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var setContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection);
        setContext.Transaction = transaction;
        setContext.Parameters.AddWithValue(Atlas.ToString("D"));
        await setContext.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand("select count(*), coalesce(sum(amount),0) from business_expenses.expense_reimbursement_obligation where tenant_id=$1 and status='Pending'", connection);
        command.Transaction = transaction;
        command.Parameters.AddWithValue(Atlas);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt64(0));
        Assert.Equal(320m, reader.GetDecimal(1));
    }

    [ExpensesPostgresFact]
    public async Task RuntimeTenantContextCannotReadOtherTenantExpenses()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var setContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection);
        setContext.Transaction = transaction;
        setContext.Parameters.AddWithValue(Delta.ToString("D"));
        await setContext.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand("select count(*) from business_expenses.expense where tenant_id=$1", connection);
        command.Transaction = transaction;
        command.Parameters.AddWithValue(Atlas);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0, reader.GetInt64(0));
    }

    private sealed class ExpensesPostgresFactAttribute : FactAttribute
    {
        public ExpensesPostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION"))) Skip = "Set TODIR_EXPENSES_TEST_CONNECTION for PostgreSQL acceptance."; }
    }
}
