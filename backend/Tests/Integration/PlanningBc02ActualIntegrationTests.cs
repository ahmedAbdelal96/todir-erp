using Modules.Ledger.Repositories;
using Modules.Planning;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlanningBc02ActualIntegrationTests
{
    private static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));

    [Fact(DisplayName = "BC2-T01 authoritative Ledger actual refresh is immutable and idempotent")]
    public async Task AuthoritativeLedgerActualRefreshIsImmutableAndIdempotent()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        var ledgerSource = new LedgerOwnerReconciliationSource(dataSource);
        var sourceFacts = await ledgerSource.ListFinancialFactsAsync(AtlasTenant, null);
        Assert.NotEmpty(sourceFacts);

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        var planVersionId = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", AtlasTenant.Value))!;
        var actorId = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;

        var repository = new PlanningRepository(new Core.Transactions.NpgsqlTransactionCoordinator(dataSource), ownerSources: [ledgerSource]);
        var first = await repository.RefreshActualsAsync(AtlasTenant, actorId, new("seed-v1", "caller-watermark-is-not-authoritative", null, planVersionId), default);
        var replay = await repository.RefreshActualsAsync(AtlasTenant, actorId, new("seed-v1", "a-different-caller-watermark", null, planVersionId), default);

        Assert.Equal(first, replay);
        var run = await repository.GetActualRefreshRunAsync(AtlasTenant, first, default);
        Assert.Equal("Succeeded", run.State);

        var snapshotId = (Guid)(await ScalarAsync(admin, "select snapshot_id from business_planning.actual_refresh_run where tenant_id=$1 and id=$2", AtlasTenant.Value, run.Id))!;
        Assert.Equal(sourceFacts.Count, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_actual_snapshot_fact where tenant_id=$1 and snapshot_id=$2", AtlasTenant.Value, snapshotId), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(sourceFacts.Sum(f => f.Amount), await ScalarAsync(admin, "select total_amount from business_planning.planning_actual_snapshot where tenant_id=$1 and id=$2", AtlasTenant.Value, snapshotId));
        Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.actual_refresh_run where tenant_id=$1 and id=$2 and state='Succeeded'", AtlasTenant.Value, first), System.Globalization.CultureInfo.InvariantCulture));

        var suppliedFact = new ActualFactRequest(planVersionId, sourceFacts[0].LegalEntityId, sourceFacts[0].PeriodStart, sourceFacts[0].MetricCode, System.Text.Json.JsonDocument.Parse("{}").RootElement, 1m, sourceFacts[0].CurrencyCode, "caller", "caller-fact");
        var rejected = await Assert.ThrowsAsync<PlanningException>(() => repository.RefreshActualsAsync(AtlasTenant, actorId, new("seed-v1", "caller", [suppliedFact], planVersionId), default));
        Assert.Equal("planning.actual_refresh_source_contract_required", rejected.Code);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"{name} is required.");

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return await command.ExecuteScalarAsync();
    }
}
