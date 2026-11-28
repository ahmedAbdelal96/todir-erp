using Core.Transactions;
using Modules.Planning;
using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlanningCalculationRecoveryAcceptanceTests
{
    private static readonly Guid TenantId = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");

    [Fact(DisplayName = "FP14 calculation recovery retries stale worker runs and rejects live runs")]
    public async Task CalculationRecoveryRetriesStaleWorkerRunsAndRejectsLiveRuns()
    {
        var runtime = Required("TODIR_DATABASE_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        await using var admin = new NpgsqlConnection(Required("TODIR_P04_ADMIN_CONNECTION"));
        await admin.OpenAsync();

        var sourceVersion = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", TenantId))!;
        var actor = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", TenantId))!;
        var repository = new PlanningRepository(new NpgsqlTransactionCoordinator(dataSource));
        var draft = await repository.ClonePlanVersionAsync(new(TenantId), actor, sourceVersion, $"FP14 recovery {Guid.CreateVersion7():N}", default);

        var staleRunId = Guid.CreateVersion7();
        await ExecuteAsync(admin, "insert into business_planning.planning_calculation_run(tenant_id,id,plan_version_id,state,input_watermark,started_at) values($1,$2,$3,'Running','stale-worker-recovery',now() - interval '31 minutes')", TenantId, staleRunId, draft.Id);

        var recovered = await repository.RetryCalculationAsync(new(TenantId), actor, staleRunId, default);

        Assert.Equal("Succeeded", recovered.State);
        Assert.Equal(draft.Id, recovered.PlanVersionId);
        Assert.Equal("Failed", await ScalarAsync(admin, "select state from business_planning.planning_calculation_run where tenant_id=$1 and id=$2", TenantId, staleRunId));
        Assert.Equal("planning.calculation_interrupted", await ScalarAsync(admin, "select error_detail from business_planning.planning_calculation_run where tenant_id=$1 and id=$2", TenantId, staleRunId));

        var liveRunId = Guid.CreateVersion7();
        await ExecuteAsync(admin, "insert into business_planning.planning_calculation_run(tenant_id,id,plan_version_id,state,input_watermark) values($1,$2,$3,'Running','live-worker')", TenantId, liveRunId, draft.Id);
        var liveError = await Assert.ThrowsAsync<PlanningException>(() => repository.RetryCalculationAsync(new(TenantId), actor, liveRunId, default));
        Assert.Equal("planning.calculation_retry_invalid", liveError.Code);
        Assert.Equal("Running", await ScalarAsync(admin, "select state from business_planning.planning_calculation_run where tenant_id=$1 and id=$2", TenantId, liveRunId));
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

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync();
    }
}
