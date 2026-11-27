using Core.Transactions;
using Modules.Planning;
using Npgsql;
using System.Text.Json;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlanningBc03AnalyticsIntegrationTests
{
    private static readonly Guid TenantId = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");

    [Fact(DisplayName = "BC3 integrated statements reconcile and CSV import remains atomic")]
    public async Task IntegratedStatementsReconcileAndCsvImportRemainsAtomic()
    {
        var runtime = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        var sourceVersion = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", TenantId))!;
        var entity = (Guid)(await ScalarAsync(admin, "select legal_entity_id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit 1", TenantId, sourceVersion))!;
        var actor = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", TenantId))!;
        var repository = new PlanningRepository(new NpgsqlTransactionCoordinator(dataSource));
        var draft = await repository.ClonePlanVersionAsync(new(TenantId), actor, sourceVersion, $"BC03 analytics {Guid.NewGuid():N}", default);

        var period = new DateOnly(2027, 1, 1);
        var json = JsonDocument.Parse("{}").RootElement;
        await repository.WriteGridAsync(new(TenantId), actor, new(draft.Id,
        [
            new(entity, period, "Revenue", json, 1000m, "EGP", false, "golden-vector"),
            new(entity, period, "Cogs", json, 400m, "EGP", false, "golden-vector"),
            new(entity, period, "Opex", json, 100m, "EGP", false, "golden-vector"),
            new(entity, period, "OpeningCash", json, 1000m, "EGP", false, "golden-vector"),
            new(entity, period, "OpeningEquity", json, 1000m, "EGP", false, "golden-vector")
        ], null), default);
        await repository.AddWorkingCapitalAsync(new(TenantId), new(draft.Id, entity, period, 30m, 30m, 0m), default);

        var pnl = await repository.QueryIntegratedStatementAsync(new(TenantId), new(draft.Id, entity, period, period, "P&L", "EGP"), default);
        Assert.Equal(500m, Assert.Single(pnl, x => x.LineCode == "NetIncome").Amount);
        var balanceSheet = await repository.QueryIntegratedStatementAsync(new(TenantId), new(draft.Id, entity, period, period, "BalanceSheet", "EGP"), default);
        Assert.All(balanceSheet, line => Assert.True(line.Balanced, $"{line.LineCode} was not balanced: {line.Amount}"));
        var cashFlow = await repository.QueryIntegratedStatementAsync(new(TenantId), new(draft.Id, entity, period, period, "CashFlow", "EGP"), default);
        Assert.Equal(1000m, Assert.Single(cashFlow, x => x.LineCode == "ClosingCash").Amount);

        var csv = $"legalEntityId,periodStart,metricCode,amount,currencyCode,explicitZero,sourceLabel\n{entity:D},2027-02-01,Opex,250.00,EGP,false,csv-test";
        var preview = await repository.ImportCsvAsync(new(TenantId), actor, new(draft.Id, csv, false), default);
        Assert.False(preview.Committed);
        Assert.Equal(1, preview.AcceptedRows);
        var committed = await repository.ImportCsvAsync(new(TenantId), actor, new(draft.Id, csv, true), default);
        Assert.True(committed.Committed);
        Assert.Equal(1, committed.AcceptedRows);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException($"{name} is required.");

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        for (var index = 0; index < values.Length; index++) command.Parameters.AddWithValue($"p{index}", values[index]);
        command.CommandText = sql.Replace("$1", "@p0", StringComparison.Ordinal).Replace("$2", "@p1", StringComparison.Ordinal).Replace("$3", "@p2", StringComparison.Ordinal);
        return await command.ExecuteScalarAsync();
    }
}
