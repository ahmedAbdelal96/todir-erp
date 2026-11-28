using System.Text.Json;
using System.Globalization;
using Core.Transactions;
using Modules.Planning;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlanningGovernanceConcurrencyAcceptanceTests
{
    private static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));

    [Fact(DisplayName = "BC2-T07 every published content mutation route rejects changes")]
    public async Task EveryPublishedContentMutationRouteRejectsChanges()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();

        var actor = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;
        var sourceVersion = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", AtlasTenant.Value))!;
        var entity = (Guid)(await ScalarAsync(admin, "select legal_entity_id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit 1", AtlasTenant.Value, sourceVersion))!;
        var repository = Repository(dataSource);
        var published = await PublishCloneAsync(repository, actor, sourceVersion);
        var json = JsonDocument.Parse("{}").RootElement.Clone();
        var period = new DateOnly(2027, 1, 1);
        var csv = $"legalEntityId,periodStart,metricCode,amount,currencyCode,explicitZero,sourceLabel\n{entity:D},2027-02-01,Opex,125.00,EGP,false,bc2-t07";

        var routes = new (string Name, Func<Task> Action)[]
        {
            ("grid write", async () => await repository.WriteGridAsync(AtlasTenant, actor, new(published, [new(entity, period, "OPEX", json, 1m, "EGP", false, "bc2-t07")], null), default)),
            ("calculation", async () => await repository.RecalculateCompleteAsync(AtlasTenant, actor, published, default)),
            ("csv import", async () => await repository.ImportCsvAsync(AtlasTenant, actor, new(published, csv, true), default)),
            ("assumption", async () => await repository.CreateAssumptionAsync(AtlasTenant, actor, new(published, "BC2_T07", "Published mutation", "EGP", null), default)),
            ("workforce", async () => await repository.AddWorkforceAsync(AtlasTenant, actor, new(published, entity, null, "BC2-T07", period, 1m, 100m, "EGP", "acceptance"), default)),
            ("OPEX", async () => await repository.AddOpexLineAsync(AtlasTenant, actor, new(published, entity, null, "BC2-T07", null, null, period, "Fixed", 100m, 0m, 0m, 0m, 0m, "EGP", "acceptance"), default)),
            ("CAPEX", async () => await repository.AddCapexAsync(AtlasTenant, actor, new(published, entity, "BC2-T07", "Equipment", null, 100m, "EGP", period, null, 1, "acceptance"), default)),
            ("revenue driver", async () => await repository.AddRevenueDriverAsync(AtlasTenant, new(published, entity, period, "BC2-T07", 1m, 100m, "EGP", "acceptance"), default)),
            ("working capital", async () => await repository.AddWorkingCapitalAsync(AtlasTenant, new(published, entity, period, 30m, 30m, 30m), default)),
            ("FX", async () => await repository.AddFxRateAsync(AtlasTenant, new(published, period, "USD", "EGP", 50m, "Planning", "acceptance"), default)),
            ("target", async () => await repository.CreatePlanningTargetAsync(AtlasTenant, actor, new(published, entity, period, "OPEX", 100m, "EGP", "acceptance"), default)),
            ("proposal", async () => await repository.CreatePlanningProposalAsync(AtlasTenant, actor, new(published, entity, null, period, "OPEX", 100m, "EGP", "acceptance"), default)),
        };

        foreach (var route in routes)
        {
            var error = await Record.ExceptionAsync(route.Action);
            var planningError = Assert.IsType<PlanningException>(error);
            Assert.Equal("planning.version_immutable", planningError.Code);
        }

        Assert.Equal("Published", await ScalarAsync(admin, "select state from business_planning.plan_version where tenant_id=$1 and id=$2", AtlasTenant.Value, published));
        Assert.Equal(0L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_input_fact f join business_planning.planning_coordinate c on c.tenant_id=f.tenant_id and c.id=f.coordinate_id where f.tenant_id=$1 and c.plan_version_id=$2 and f.source_label='bc2-t07'", AtlasTenant.Value, published), CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "BC2-T09 concurrent transfers conserve one approved outcome and remain idempotent")]
    public async Task ConcurrentTransfersConserveOneApprovedOutcome()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();

        var actor = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;
        var sourceVersion = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", AtlasTenant.Value))!;
        var entity = (Guid)(await ScalarAsync(admin, "select legal_entity_id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit 1", AtlasTenant.Value, sourceVersion))!;
        var repository = Repository(dataSource);
        var draft = await repository.ClonePlanVersionAsync(AtlasTenant, actor, sourceVersion, $"BC2-T09 {Guid.NewGuid():N}", default);
        var baselineId = await repository.CreateBaselineAsync(AtlasTenant, actor, new(draft.Id, "BC2-T09", 100m, "EGP"), default);
        var policyId = await repository.CreatePolicyAsync(AtlasTenant, actor, entity, $"BC2-T09 {Guid.NewGuid():N}", "None", "ActualOpenCommitmentProposed", 100m, default);
        var dimension = JsonDocument.Parse("{\"acceptance\":\"bc2-t09\"}").RootElement.Clone();
        var period = new DateOnly(2027, 1, 1);
        var sourceBucket = await repository.CreateBucketAsync(AtlasTenant, new(draft.Id, policyId, entity, period, "OPEX", dimension, 100m, "EGP"), default);
        var destinationBucketA = await repository.CreateBucketAsync(AtlasTenant, new(draft.Id, policyId, entity, period.AddMonths(1), "OPEX", dimension, 0m, "EGP"), default);
        var destinationBucketB = await repository.CreateBucketAsync(AtlasTenant, new(draft.Id, policyId, entity, period.AddMonths(2), "OPEX", dimension, 0m, "EGP"), default);
        var coordinates = await ReadCoordinatesAsync(admin, draft.Id, 3);
        Assert.Equal(3, coordinates.Count);

        var effectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-1);
        var transferA = await repository.CreateBudgetTransferAsync(AtlasTenant, actor, baselineId, new(coordinates[0], coordinates[1], 80m, "BC2-T09 A", sourceBucket, destinationBucketA, effectiveFrom), default);
        var transferB = await repository.CreateBudgetTransferAsync(AtlasTenant, actor, baselineId, new(coordinates[0], coordinates[2], 80m, "BC2-T09 B", sourceBucket, destinationBucketB, effectiveFrom), default);

        var outcomes = await Task.WhenAll(
            CaptureAsync(transferA, () => Repository(dataSource).ApproveTransferAsync(AtlasTenant, actor, transferA, default)),
            CaptureAsync(transferB, () => Repository(dataSource).ApproveTransferAsync(AtlasTenant, actor, transferB, default)));

        Assert.Equal(1, outcomes.Count(x => x.Succeeded));
        Assert.Equal(1, outcomes.Count(x => !x.Succeeded && x.ErrorCode == "planning.transfer_insufficient_budget"));
        Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.budget_transfer where tenant_id=$1 and baseline_id=$2 and state='Approved'", AtlasTenant.Value, baselineId), CultureInfo.InvariantCulture));
        Assert.Equal(80m, await ScalarAsync(admin, "select transfer_out from business_planning.budget_control_bucket where tenant_id=$1 and id=$2", AtlasTenant.Value, sourceBucket));
        Assert.Equal(80m, await ScalarAsync(admin, "select coalesce(sum(transfer_in+transfer_out),0) from business_planning.budget_control_bucket where tenant_id=$1 and id in ($2,$3)", AtlasTenant.Value, destinationBucketA, destinationBucketB));

        var approvedTransfer = outcomes.Single(x => x.Succeeded).TransferId;
        await repository.ApproveTransferAsync(AtlasTenant, actor, approvedTransfer, default);
        Assert.Equal(80m, await ScalarAsync(admin, "select transfer_out from business_planning.budget_control_bucket where tenant_id=$1 and id=$2", AtlasTenant.Value, sourceBucket));
        Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.budget_transfer where tenant_id=$1 and baseline_id=$2 and state='Approved'", AtlasTenant.Value, baselineId), CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "BC2-T13 grid batches rollback on duplicate coordinates and preserve explicit zero semantics")]
    public async Task GridBatchRollbackAndExplicitZeroSemanticsArePreserved()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();

        var actor = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;
        var sourceVersion = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", AtlasTenant.Value))!;
        var entity = (Guid)(await ScalarAsync(admin, "select legal_entity_id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit 1", AtlasTenant.Value, sourceVersion))!;
        var repository = Repository(dataSource);
        var draft = await repository.ClonePlanVersionAsync(AtlasTenant, actor, sourceVersion, $"BC2-T13 {Guid.NewGuid():N}", default);
        var json = JsonDocument.Parse("{\"acceptance\":\"bc2-t13\"}").RootElement.Clone();
        var period = new DateOnly(2028, 1, 1);
        var beforeCoordinates = Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2", AtlasTenant.Value, draft.Id), CultureInfo.InvariantCulture);
        var beforeFacts = Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_input_fact f join business_planning.planning_coordinate c on c.tenant_id=f.tenant_id and c.id=f.coordinate_id where f.tenant_id=$1 and c.plan_version_id=$2", AtlasTenant.Value, draft.Id), CultureInfo.InvariantCulture);
        var beforeVersion = Convert.ToInt64(await ScalarAsync(admin, "select row_version from business_planning.plan_version where tenant_id=$1 and id=$2", AtlasTenant.Value, draft.Id), CultureInfo.InvariantCulture);

        var duplicate = new GridCellRequest(entity, period, "Opex", json, 10m, "EGP", false, "bc2-t13-duplicate");
        var duplicateError = await Assert.ThrowsAsync<PlanningException>(() => repository.WriteGridAsync(AtlasTenant, actor, new(draft.Id, [duplicate, duplicate], null), default));
        Assert.Equal("planning.grid_duplicate_coordinate", duplicateError.Code);
        Assert.Equal(beforeCoordinates, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2", AtlasTenant.Value, draft.Id), CultureInfo.InvariantCulture));
        Assert.Equal(beforeFacts, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_input_fact f join business_planning.planning_coordinate c on c.tenant_id=f.tenant_id and c.id=f.coordinate_id where f.tenant_id=$1 and c.plan_version_id=$2", AtlasTenant.Value, draft.Id), CultureInfo.InvariantCulture));
        Assert.Equal(beforeVersion, Convert.ToInt64(await ScalarAsync(admin, "select row_version from business_planning.plan_version where tenant_id=$1 and id=$2", AtlasTenant.Value, draft.Id), CultureInfo.InvariantCulture));

        await repository.WriteGridAsync(AtlasTenant, actor, new(draft.Id, [new(entity, period, "Opex", json, 0m, "EGP", true, "bc2-t13-zero")], null), default);
        var zero = await repository.QueryGridAsync(AtlasTenant, new(draft.Id, entity, period, period, "Opex", 1, 50), default);
        var zeroCell = Assert.Single(zero, cell => cell.SourceLabel == "bc2-t13-zero");
        Assert.Equal(0m, zeroCell.Amount);
        Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_input_fact f join business_planning.planning_coordinate c on c.tenant_id=f.tenant_id and c.id=f.coordinate_id where f.tenant_id=$1 and c.plan_version_id=$2 and f.explicit_zero=true and f.amount=0", AtlasTenant.Value, draft.Id), CultureInfo.InvariantCulture));

        var staleVersion = Convert.ToInt64(await ScalarAsync(admin, "select row_version from business_planning.plan_version where tenant_id=$1 and id=$2", AtlasTenant.Value, draft.Id), CultureInfo.InvariantCulture) - 1;
        var staleError = await Assert.ThrowsAsync<PlanningException>(() => repository.WriteGridAsync(AtlasTenant, actor, new(draft.Id, [new(entity, period, "Opex", json, 5m, "EGP", false, "bc2-t13-stale")], staleVersion), default));
        Assert.Equal("planning.concurrency_stale", staleError.Code);
        Assert.Equal(0L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_input_fact f join business_planning.planning_coordinate c on c.tenant_id=f.tenant_id and c.id=f.coordinate_id where f.tenant_id=$1 and c.plan_version_id=$2 and f.source_label='bc2-t13-stale'", AtlasTenant.Value, draft.Id), CultureInfo.InvariantCulture));

        await repository.WriteGridAsync(AtlasTenant, actor, new(draft.Id, [new(entity, period, "Opex", json, null, "EGP", false, "bc2-t13-missing")], null), default);
        Assert.Equal(0L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_input_fact f join business_planning.planning_coordinate c on c.tenant_id=f.tenant_id and c.id=f.coordinate_id where f.tenant_id=$1 and c.plan_version_id=$2 and c.period_start=$3 and c.metric_code='Opex'", AtlasTenant.Value, draft.Id, period), CultureInfo.InvariantCulture));
        Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 and period_start=$3 and metric_code='Opex'", AtlasTenant.Value, draft.Id, period), CultureInfo.InvariantCulture));
    }

    private static PlanningRepository Repository(NpgsqlDataSource dataSource) => new(new NpgsqlTransactionCoordinator(dataSource));

    private static async Task<Guid> PublishCloneAsync(PlanningRepository repository, Guid actor, Guid sourceVersion)
    {
        var draft = await repository.ClonePlanVersionAsync(AtlasTenant, actor, sourceVersion, $"BC2-T07 {Guid.NewGuid():N}", default);
        await repository.AdvanceVersionAsync(AtlasTenant, actor, draft.Id, "submit", default);
        await repository.AdvanceVersionAsync(AtlasTenant, actor, draft.Id, "approve", default);
        await repository.AdvanceVersionAsync(AtlasTenant, actor, draft.Id, "publish", default);
        return draft.Id;
    }

    private static async Task<List<Guid>> ReadCoordinatesAsync(NpgsqlConnection connection, Guid planVersionId, int count)
    {
        var result = new List<Guid>();
        await using var command = new NpgsqlCommand("select id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit $3", connection);
        command.Parameters.AddWithValue(AtlasTenant.Value);
        command.Parameters.AddWithValue(planVersionId);
        command.Parameters.AddWithValue(count);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetGuid(0));
        return result;
    }

    private static async Task<(bool Succeeded, string? ErrorCode, Guid TransferId)> CaptureAsync(Guid transferId, Func<Task<BudgetCheckView>> action)
    {
        try
        {
            await action();
            return (true, null, transferId);
        }
        catch (PlanningException exception)
        {
            return (false, exception.Code, transferId);
        }
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
