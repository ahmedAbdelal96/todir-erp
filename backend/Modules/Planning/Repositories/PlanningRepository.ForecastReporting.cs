using Core.Transactions;
using Shared.Primitives.Identifiers;

namespace Modules.Planning;

public sealed partial class PlanningRepository
{
    public Task<VarianceDecompositionView> VarianceDecompositionAsync(TenantId tenant, Guid coordinateId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        await using var command = Cmd(c, "select c.id,c.legal_entity_id,c.period_start,c.metric_code,coalesce(i.amount,0),coalesce(cf.amount,0),coalesce(a.amount,0) from business_planning.planning_coordinate c left join business_planning.planning_input_fact i on i.tenant_id=c.tenant_id and i.coordinate_id=c.id left join business_planning.planning_calculated_fact cf on cf.tenant_id=c.tenant_id and cf.coordinate_id=c.id left join business_planning.planning_actual_snapshot_fact a on a.tenant_id=c.tenant_id and a.coordinate_id=c.id and a.snapshot_id=coalesce((select v.actual_snapshot_id from business_planning.plan_version v where v.tenant_id=c.tenant_id and v.id=c.plan_version_id),(select s.id from business_planning.planning_actual_snapshot s where s.tenant_id=c.tenant_id and s.plan_version_id=c.plan_version_id and s.state='Published' order by s.created_at desc,s.id desc limit 1)) where c.tenant_id=$1 and c.id=$2", tenant.Value, coordinateId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new PlanningException("planning.coordinate_not_found");
        var id = reader.GetGuid(0); var entity = reader.GetGuid(1); var period = reader.GetFieldValue<DateOnly>(2); var metric = reader.GetString(3); var budget = reader.GetDecimal(4); var forecast = reader.GetDecimal(5); var actual = reader.GetDecimal(6);
        var drivers = new[] { new VarianceDriverView(id, "BudgetToForecast", "Forecast minus budget", forecast - budget), new VarianceDriverView(id, "ForecastToActual", "Actual minus forecast", actual - forecast) };
        return new VarianceDecompositionView(id, entity, period, metric, budget, forecast, actual, forecast - budget, actual - forecast, actual - budget, Math.Abs(actual - budget), drivers);
    }, ct);

    public Task<IReadOnlyList<DepartmentPlannerLineView>> QueryDepartmentPlannerAsync(TenantId tenant, DepartmentPlannerRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        var result = new List<DepartmentPlannerLineView>();
        await using var command = Cmd(c, "select c.period_start,coalesce(sum(i.amount),0),coalesce(sum(cf.amount),0),coalesce(sum(a.amount),0),coalesce((select sum(b.commitment_total+b.precommitment_total) from business_planning.budget_control_bucket b where b.tenant_id=c.tenant_id and b.plan_version_id=c.plan_version_id and b.legal_entity_id=c.legal_entity_id and b.period_start=c.period_start),0),coalesce((select sum(b.original_budget+b.revision_total+b.transfer_in-b.transfer_out-b.actual_total-b.commitment_total-b.precommitment_total) from business_planning.budget_control_bucket b where b.tenant_id=c.tenant_id and b.plan_version_id=c.plan_version_id and b.legal_entity_id=c.legal_entity_id and b.period_start=c.period_start),0) from business_planning.planning_coordinate c left join business_planning.planning_input_fact i on i.tenant_id=c.tenant_id and i.coordinate_id=c.id left join business_planning.planning_calculated_fact cf on cf.tenant_id=c.tenant_id and cf.coordinate_id=c.id left join business_planning.planning_actual_snapshot_fact a on a.tenant_id=c.tenant_id and a.coordinate_id=c.id and a.snapshot_id=coalesce((select v.actual_snapshot_id from business_planning.plan_version v where v.tenant_id=c.tenant_id and v.id=c.plan_version_id),(select s.id from business_planning.planning_actual_snapshot s where s.tenant_id=c.tenant_id and s.plan_version_id=c.plan_version_id and s.state='Published' order by s.created_at desc,s.id desc limit 1)) where c.tenant_id=$1 and c.plan_version_id=$2 and c.legal_entity_id=$3 and ($4::date is null or c.period_start >= $4) and ($5::date is null or c.period_start <= $5) and ($6::uuid is null or c.dimension_key->>'departmentRef'=$6::text or c.dimension_key->>'departmentId'=$6::text) group by c.period_start,c.tenant_id,c.plan_version_id,c.legal_entity_id order by c.period_start", tenant.Value, request.PlanVersionId, request.LegalEntityId, Db(request.From), Db(request.To), Db(request.DepartmentRef));
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) { var budget = reader.GetDecimal(1); var forecast = reader.GetDecimal(2); var actual = reader.GetDecimal(3); result.Add(new(request.PlanVersionId, request.LegalEntityId, request.DepartmentRef, reader.GetFieldValue<DateOnly>(0), "All", budget, forecast, actual, actual - budget, Math.Abs(actual - budget), reader.GetDecimal(4), reader.GetDecimal(5), "ReadOnly")); }
        return (IReadOnlyList<DepartmentPlannerLineView>)result;
    }, ct);

    public Task<CfoOverviewView> QueryCfoOverviewAsync(TenantId tenant, CfoOverviewRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        if (request.LegalEntityIds.Count == 0 || request.LegalEntityIds.Count > 100) throw new PlanningException("planning.cfo_scope_invalid");
        var metrics = new List<CfoMetricView>();
        var metricRows = new List<(string Metric, DateOnly Period, decimal Forecast, decimal Actual, string Currency)>();
        await using var command = Cmd(c, "select c.metric_code,c.period_start,coalesce(sum(coalesce(cf.amount,i.amount)),0),coalesce(sum(a.amount),0),coalesce(i.currency_code,cf.currency_code,'EGP') from business_planning.planning_coordinate c left join business_planning.planning_input_fact i on i.tenant_id=c.tenant_id and i.coordinate_id=c.id left join business_planning.planning_calculated_fact cf on cf.tenant_id=c.tenant_id and cf.coordinate_id=c.id left join business_planning.planning_actual_snapshot_fact a on a.tenant_id=c.tenant_id and a.coordinate_id=c.id and a.snapshot_id=coalesce((select v.actual_snapshot_id from business_planning.plan_version v where v.tenant_id=c.tenant_id and v.id=c.plan_version_id),(select s.id from business_planning.planning_actual_snapshot s where s.tenant_id=c.tenant_id and s.plan_version_id=c.plan_version_id and s.state='Published' order by s.created_at desc,s.id desc limit 1)) where c.tenant_id=$1 and c.plan_version_id=$2 and c.legal_entity_id=any($3) and c.period_start between $4 and $5 group by c.metric_code,c.period_start,coalesce(i.currency_code,cf.currency_code,'EGP') order by c.metric_code,c.period_start", tenant.Value, request.PlanVersionId, request.LegalEntityIds.ToArray(), request.From, request.To);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) metricRows.Add((reader.GetString(0), reader.GetFieldValue<DateOnly>(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetString(4).Trim().ToUpperInvariant()));
        await reader.CloseAsync();
        var reportingCurrency = request.ReportingCurrencyCode.Trim().ToUpperInvariant();
        var metricTotals = new Dictionary<string, (decimal Forecast, decimal Actual, decimal ReportingForecast, decimal ReportingActual)>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in metricRows)
        {
            var forecastReporting = await ConvertAmountAsync(c, tenant.Value, request.PlanVersionId, row.Period, row.Forecast, row.Currency, reportingCurrency, token);
            var actualReporting = await ConvertAmountAsync(c, tenant.Value, request.PlanVersionId, row.Period, row.Actual, row.Currency, reportingCurrency, token);
            var total = metricTotals.GetValueOrDefault(row.Metric);
            metricTotals[row.Metric] = (total.Forecast + row.Forecast, total.Actual + row.Actual, total.ReportingForecast + forecastReporting, total.ReportingActual + actualReporting);
        }
        foreach (var pair in metricTotals.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            metrics.Add(new(pair.Key, pair.Value.Forecast, pair.Value.Actual, pair.Value.Actual - pair.Value.Forecast, pair.Value.ReportingForecast, pair.Value.ReportingActual, pair.Value.ReportingActual - pair.Value.ReportingForecast, reportingCurrency, "planning coordinate plus pinned actual snapshot"));
        var rollupLines = new List<RollupLineView>();
        await using var rollup = Cmd(c, "select c.legal_entity_id,c.period_start,c.metric_code,coalesce(cf.amount,i.amount,0),coalesce(cf.currency_code,i.currency_code,'EGP') from business_planning.planning_coordinate c left join business_planning.planning_input_fact i on i.tenant_id=c.tenant_id and i.coordinate_id=c.id left join business_planning.planning_calculated_fact cf on cf.tenant_id=c.tenant_id and cf.coordinate_id=c.id where c.tenant_id=$1 and c.plan_version_id=$2 and c.legal_entity_id=any($3) and c.period_start between $4 and $5 order by c.legal_entity_id,c.period_start,c.metric_code", tenant.Value, request.PlanVersionId, request.LegalEntityIds.ToArray(), request.From, request.To);
        await using var rollupReader = await rollup.ExecuteReaderAsync(token);
        var rollupRows = new List<(Guid Entity, DateOnly Period, string Metric, decimal Amount, string Currency)>();
        while (await rollupReader.ReadAsync(token)) rollupRows.Add((rollupReader.GetGuid(0), rollupReader.GetFieldValue<DateOnly>(1), rollupReader.GetString(2), rollupReader.GetDecimal(3), rollupReader.GetString(4).Trim().ToUpperInvariant()));
        await rollupReader.CloseAsync();
        foreach (var row in rollupRows)
        {
            var rate = string.Equals(row.Currency, reportingCurrency, StringComparison.OrdinalIgnoreCase) ? 1m : await Scalar<decimal?>(c, "select rate from business_planning.planning_fx_rate where tenant_id=$1 and plan_version_id=$2 and period_start=$3 and from_currency=$4 and to_currency=$5 order by rate_type='Scenario' desc limit 1", token, tenant.Value, request.PlanVersionId, row.Period, row.Currency, reportingCurrency) ?? throw new PlanningException("planning.fx_rate_missing");
            rollupLines.Add(new RollupLineView(row.Entity, row.Period, row.Metric, row.Amount, rate, decimal.Round(row.Amount * rate, 4, MidpointRounding.AwayFromZero), row.Currency, reportingCurrency, "planning coordinate input or calculated"));
        }
        return new CfoOverviewView(request.PlanVersionId, reportingCurrency, metrics, rollupLines);
    }, ct);

    private static async Task<decimal> ConvertAmountAsync(ITransactionContext c, Guid tenant, Guid version, DateOnly period, decimal amount, string source, string target, CancellationToken ct)
    {
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) return amount;
        var rate = await Scalar<decimal?>(c, "select rate from business_planning.planning_fx_rate where tenant_id=$1 and plan_version_id=$2 and period_start=$3 and from_currency=$4 and to_currency=$5 order by rate_type='Scenario' desc limit 1", ct, tenant, version, period, source, target) ?? throw new PlanningException("planning.fx_rate_missing");
        return decimal.Round(amount * rate, 4, MidpointRounding.AwayFromZero);
    }
}
