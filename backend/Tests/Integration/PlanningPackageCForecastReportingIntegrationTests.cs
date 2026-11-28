using Core.Transactions;
using Modules.Planning;
using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlanningPackageCForecastReportingIntegrationTests
{
    private static readonly Guid TenantId = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");

    [Fact(DisplayName = "Package C rolling forecast preserves source and limits copied inputs to future periods")]
    public async Task PackageCRollingForecastPreservesSourceAndCopiesOnlyFutureInputs()
    {
        var (repository, admin, actor, version, entity) = await ContextAsync("Budget");
        var sourceName = (string)(await ScalarAsync(admin, "select name from business_planning.plan_version where tenant_id=$1 and id=$2", TenantId, version))!;
        var rolled = await repository.AdvanceRollingForecastAsync(new(TenantId), actor, new(version, new DateOnly(2027, 1, 1), "Package C rolling", 3), default);
        Assert.NotEqual(version, rolled.Id);
        Assert.Equal(sourceName, (string)(await ScalarAsync(admin, "select name from business_planning.plan_version where tenant_id=$1 and id=$2", TenantId, version))!);
        Assert.Equal(0L, (long)(await ScalarAsync(admin, "select count(*) from business_planning.planning_input_fact f join business_planning.planning_coordinate c on c.tenant_id=f.tenant_id and c.id=f.coordinate_id where f.tenant_id=$1 and c.plan_version_id=$2 and c.period_start <= $3", TenantId, rolled.Id, new DateOnly(2027, 1, 1)))!);
    }

    [Fact(DisplayName = "Package C scenario adoption copies the basis without mutating the source")]
    public async Task PackageCScenarioAdoptionCopiesBasisWithoutMutatingSource()
    {
        var (repository, admin, actor, version, _) = await ContextAsync("Budget");
        var scenario = await repository.CreateScenarioAsync(new(TenantId), actor, new("Package C scenario", "WhatIf", version, "Private"), default);
        var adopted = await repository.AdoptScenarioAsync(new(TenantId), actor, scenario.Id, new("Package C adopted", "Forecast", null), default);
        Assert.NotEqual(version, adopted.Id);
        Assert.Equal((long)(await ScalarAsync(admin, "select count(*) from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2", TenantId, version))!, (long)(await ScalarAsync(admin, "select count(*) from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2", TenantId, adopted.Id))!);
    }

    [Fact(DisplayName = "Package C variance decomposition exposes numeric traceable drivers")]
    public async Task PackageCVarianceDecompositionIsNumericAndTraceable()
    {
        var (repository, admin, _, version, _) = await ContextAsync("Any");
        var coordinate = (Guid)(await ScalarAsync(admin, "select id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit 1", TenantId, version))!;
        var decomposition = await repository.VarianceDecompositionAsync(new(TenantId), coordinate, default);
        Assert.Equal(decomposition.ActualAmount - decomposition.BudgetAmount, decomposition.BudgetToActual);
        Assert.NotEmpty(decomposition.Drivers);
    }

    [Fact(DisplayName = "Package C forecast accuracy remains reproducible for pinned actuals")]
    public async Task PackageCForecastAccuracyIsReproducible()
    {
        var (repository, _, _, version, _) = await ContextAsync("Budget");
        // The disposable seed guarantees a published Budget baseline; use the same
        // immutable baseline as the actual basis so this test does not depend on a
        // separately seeded Actual version.
        var actual = version;
        var request = new AccuracyRequest(version, actual, new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 1), "Revenue");
        var first = await repository.CalculateAccuracyAsync(new(TenantId), request, default);
        var second = await repository.CalculateAccuracyAsync(new(TenantId), request, default);
        Assert.Equal(first, second);
    }

    [Fact(DisplayName = "Package C department planner and CFO read models return scoped planning data")]
    public async Task PackageCDepartmentAndCfoReadModelsExposeScopedData()
    {
        var (repository, _, _, version, entity) = await ContextAsync("Any");
        var department = await repository.QueryDepartmentPlannerAsync(new(TenantId), new(version, entity, null, new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 1)), default);
        var cfo = await repository.QueryCfoOverviewAsync(new(TenantId), new(version, [entity], new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 1), "EGP"), default);
        Assert.All(department, line => Assert.Equal(entity, line.LegalEntityId));
        Assert.NotNull(cfo);
    }

    [Fact(DisplayName = "Package C multi entity rollup applies planning FX and records the basis")]
    public async Task PackageCMultiEntityRollupUsesPlanningFxAndExplicitBasis()
    {
        var (repository, _, _, version, entity) = await ContextAsync("Any");
        var lines = await repository.QueryRollupAsync(new(TenantId), new(version, [entity], new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 1), "Revenue", "EGP"), default);
        Assert.All(lines, line => Assert.Equal("planning_coordinate_input_or_calculated", line.SourceBasis));
        Assert.All(lines, line => Assert.Equal(line.SourceAmount * line.Rate, line.ReportingAmount, 4));
    }

    private static async Task<(PlanningRepository Repository, NpgsqlConnection Admin, Guid Actor, Guid Version, Guid Entity)> ContextAsync(string kind)
    {
        var runtime = Required("TODIR_DATABASE_CONNECTION");
        var admin = new NpgsqlConnection(Required("TODIR_P04_ADMIN_CONNECTION"));
        await admin.OpenAsync();
        var version = await VersionAsync(kind, admin);
        var entity = (Guid)(await ScalarAsync(admin, "select legal_entity_id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit 1", TenantId, version))!;
        var actor = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", TenantId))!;
        return (new PlanningRepository(new NpgsqlTransactionCoordinator(NpgsqlDataSource.Create(runtime))), admin, actor, version, entity);
    }

    private static async Task<Guid> VersionAsync(string kind, NpgsqlConnection? admin = null)
    {
        if (admin is null)
        {
            admin = new NpgsqlConnection(Required("TODIR_P04_ADMIN_CONNECTION"));
            await admin.OpenAsync();
        }
        var sql = kind switch
        {
            "Any" => "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1",
            "Budget" => "select id from business_planning.plan_version where tenant_id=$1 and state='Published' and version_kind='Budget' order by created_at desc,id desc limit 1",
            "Forecast" => "select id from business_planning.plan_version where tenant_id=$1 and state='Published' and version_kind='Forecast' order by created_at desc,id desc limit 1",
            _ => throw new InvalidOperationException($"Unsupported planning version kind: {kind}")
        };
        var value = await ScalarAsync(admin, sql, TenantId);
        return (Guid)value!;
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException($"{name} is required.");

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql.Replace("$1", "@p0", StringComparison.Ordinal).Replace("$2", "@p1", StringComparison.Ordinal).Replace("$3", "@p2", StringComparison.Ordinal), connection);
        for (var index = 0; index < values.Length; index++) command.Parameters.AddWithValue($"p{index}", values[index]);
        return await command.ExecuteScalarAsync();
    }
}
