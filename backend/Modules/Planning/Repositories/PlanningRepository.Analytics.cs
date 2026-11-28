using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Core.Transactions;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Planning;

public sealed partial class PlanningRepository
{
    public static IReadOnlyCollection<Guid> GetCsvLegalEntityIds(string csv)
    {
        try { return ParseCsv(Guid.Empty, csv).Rows.Select(row => row.LegalEntityId).Distinct().ToArray(); }
        catch (PlanningException) { return []; }
    }

    public Task<CalculationRunView> RecalculateCompleteAsync(TenantId tenant, Guid actor, Guid planVersionId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        var started = Stopwatch.GetTimestamp();
        var state = await ReadVersionState(c, tenant.Value, planVersionId, token) ?? throw new PlanningException("planning.version_not_found");
        if (state.State != "Draft") throw new PlanningException("planning.version_immutable");

        var run = Guid.CreateVersion7();
        var inputRevision = await ReadInputRevisionAsync(c, tenant.Value, planVersionId, token);
        await Exec(c, "insert into business_planning.planning_calculation_run(tenant_id,id,plan_version_id,state,input_watermark) values($1,$2,'Running',$3)", token, tenant.Value, run, inputRevision.ToString(CultureInfo.InvariantCulture));
        await c.Transaction.SaveAsync("planning_calculation", token);
        try
        {
            var values = await ReadPlanningAmountsAsync(c, tenant.Value, planVersionId, token);
            await ReadRevenueAmountsAsync(c, tenant.Value, planVersionId, values, token);
            var formulas = await ReadFormulasAsync(c, tenant.Value, planVersionId, token);
            var graph = formulas.ToDictionary(x => x.Code, x => (IReadOnlyCollection<string>)x.Dependencies, StringComparer.OrdinalIgnoreCase);
            PlanningFormulaEngine.EnsureAcyclic(graph);
            var order = TopologicalOrder(formulas, graph);
            var contexts = values.Keys.Select(x => (x.Entity, x.Period)).Distinct().ToArray();

            foreach (var context in contexts)
            {
                var contextValues = values.Where(x => x.Key.Entity == context.Entity && x.Key.Period == context.Period)
                    .ToDictionary(x => x.Key.Metric, x => x.Value, StringComparer.OrdinalIgnoreCase);
                foreach (var formula in order)
                {
                    var output = PlanningFormulaEngine.Evaluate(formula.Expression, contextValues);
                    contextValues[formula.OutputMetricCode] = output;
                    var dimension = JsonDocument.Parse("{}");
                    var signature = Signature(context.Entity, context.Period, formula.OutputMetricCode, dimension.RootElement);
                    var coordinateId = await Scalar<Guid?>(c, "select id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 and legal_entity_id=$3 and period_start=$4 and metric_code=$5 and signature=$6", token, tenant.Value, planVersionId, context.Entity, context.Period, formula.OutputMetricCode, signature) ?? Guid.CreateVersion7();
                    if (await Scalar<bool>(c, "select not exists(select 1 from business_planning.planning_coordinate where tenant_id=$1 and id=$2)", token, tenant.Value, coordinateId))
                        await Exec(c, "insert into business_planning.planning_coordinate(tenant_id,id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key,signature) values($1,$2,$3,$4,$5,$6,'{}'::jsonb,$7)", token, tenant.Value, coordinateId, planVersionId, context.Entity, context.Period, formula.OutputMetricCode, signature);
                    await Exec(c, "insert into business_planning.planning_calculated_fact(tenant_id,id,coordinate_id,amount,currency_code,calculation_run_id,formula_code) values($1,$2,$3,$4,coalesce((select currency_code from business_planning.planning_input_fact where tenant_id=$1 and coordinate_id=$3),'EGP'),$5,$6) on conflict(tenant_id,coordinate_id) do update set amount=excluded.amount,calculation_run_id=excluded.calculation_run_id,formula_code=excluded.formula_code", token, tenant.Value, Guid.CreateVersion7(), coordinateId, output, run, formula.Code);
                    await Exec(c, "insert into business_planning.planning_calculation_trace(tenant_id,id,calculation_run_id,coordinate_id,formula_code,inputs,output_amount) values($1,$2,$3,$4,$5,$6::jsonb,$7)", token, tenant.Value, Guid.CreateVersion7(), run, coordinateId, JsonSerializer.Serialize(contextValues), output);
                    values[(context.Entity, context.Period, formula.OutputMetricCode)] = output;
                }
            }

            var currentRevision = await ReadInputRevisionAsync(c, tenant.Value, planVersionId, token);
            if (currentRevision != inputRevision) throw new PlanningException("planning.calculation_stale");
            await Exec(c, "update business_planning.planning_calculation_run set state='Succeeded',completed_at=now(),error_detail=null where tenant_id=$1 and id=$2", token, tenant.Value, run);
            await Audit(c, tenant.Value, actor, "planning_calculation_succeeded", run, token);
            PlanningMetrics.CalculationDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>("outcome", "succeeded"));
            return new CalculationRunView(run, planVersionId, "Succeeded", values.Count, DateTimeOffset.UtcNow, null);
        }
        catch (Exception exception)
        {
            await c.Transaction.RollbackAsync("planning_calculation", token);
            var error = exception is PlanningException planning ? planning.Code : "planning.calculation_failed";
            await Exec(c, "update business_planning.planning_calculation_run set state='Failed',completed_at=now(),error_detail=$3 where tenant_id=$1 and id=$2", token, tenant.Value, run, error);
            await Audit(c, tenant.Value, actor, "planning_calculation_failed", run, token);
            PlanningMetrics.Failure("calculation", error);
            PlanningMetrics.CalculationDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>("outcome", "failed"));
            return new CalculationRunView(run, planVersionId, "Failed", 0, DateTimeOffset.UtcNow, error);
        }
    }, ct);

    public async Task<CalculationRunView> RetryCalculationAsync(TenantId tenant, Guid actor, Guid calculationRunId, CancellationToken ct)
    {
        var planVersionId = await coordinator.ExecuteAsync(tenant, async (c, token) => await Scalar<Guid?>(c, "select plan_version_id from business_planning.planning_calculation_run where tenant_id=$1 and id=$2 and state='Failed'", token, tenant.Value, calculationRunId) ?? throw new PlanningException("planning.calculation_retry_invalid"), ct);
        return await RecalculateCompleteAsync(tenant, actor, planVersionId, ct);
    }

    public Task<PlanVersionView> AdvanceRollingForecastAsync(TenantId tenant, Guid actor, RollingForecastRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        if (request.HorizonMonths is < 1 or > 60) throw new PlanningException("planning.forecast_horizon_invalid");
        var source = await GetPlanVersion(c, tenant.Value, request.SourceVersionId, token) ?? throw new PlanningException("planning.version_not_found");
        await EnsurePlanVersionAccess(c, tenant.Value, request.SourceVersionId, actor, "Read", token);
        if (source.ActualCutoff is { } existingCutoff && request.ActualCutoff < existingCutoff)
            throw new PlanningException("planning.forecast_cutoff_regression");
        // A Budget can be the governed baseline for the first rolling Forecast; subsequent
        // rolls remain Forecast versions.  Neither source kind is mutated by this operation.
        if (source.VersionKind is not ("Forecast" or "Budget")) throw new PlanningException("planning.forecast_source_invalid");
        var next = Guid.CreateVersion7();
        await Exec(c, "insert into business_planning.plan_version(tenant_id,id,cycle_id,model_version_id,name,version_kind,state,scenario_id,actual_cutoff,actual_snapshot_id,baseline_version_id,created_by) select tenant_id,$3,cycle_id,model_version_id,$4,'Forecast','Draft',scenario_id,$5,actual_snapshot_id,$2,$6 from business_planning.plan_version where tenant_id=$1 and id=$2", token, tenant.Value, request.SourceVersionId, next, string.IsNullOrWhiteSpace(request.Name) ? source.Name + " Rolling" : request.Name.Trim(), request.ActualCutoff, actor);
        await Exec(c, "insert into business_planning.planning_coordinate(tenant_id,id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key,signature) select tenant_id,gen_random_uuid(),$3,legal_entity_id,period_start,metric_code,dimension_key,signature||':roll:'||$3::text from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 and period_start > $4 and period_start <= ($4 + ($5 || ' months')::interval)::date", token, tenant.Value, request.SourceVersionId, next, request.ActualCutoff, request.HorizonMonths);
        await Exec(c, "insert into business_planning.planning_input_fact(tenant_id,id,coordinate_id,amount,currency_code,value_kind,source_label,explicit_zero,source_watermark,updated_by) select f.tenant_id,gen_random_uuid(),c2.id,f.amount,f.currency_code,f.value_kind,f.source_label,f.explicit_zero,f.source_watermark,$4 from business_planning.planning_input_fact f join business_planning.planning_coordinate c1 on c1.tenant_id=f.tenant_id and c1.id=f.coordinate_id join business_planning.planning_coordinate c2 on c2.tenant_id=c1.tenant_id and c2.plan_version_id=$3 and c2.signature=c1.signature||':roll:'||$3::text where f.tenant_id=$1 and c1.plan_version_id=$2 and c1.period_start > $5 and c1.period_start <= ($5 + ($6 || ' months')::interval)::date", token, tenant.Value, request.SourceVersionId, next, actor, request.ActualCutoff, request.HorizonMonths);
        await Audit(c, tenant.Value, actor, "rolling_forecast_advanced", next, token);
        return await GetPlanVersion(c, tenant.Value, next, token) ?? throw new PlanningException("planning.version_not_found");
    }, ct);

    public Task<CsvImportView> ImportCsvAsync(TenantId tenant, Guid actor, CsvImportRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        ParsedCsv parsed;
        try { parsed = ParseCsv(request.PlanVersionId, request.Csv); }
        catch (PlanningException) { PlanningMetrics.ImportFailures.Add(1, new KeyValuePair<string, object?>("format", "csv")); throw; }
        if (parsed.View.Errors.Count > 0 || !request.Commit) return parsed.View;
        var state = await ReadVersionState(c, tenant.Value, request.PlanVersionId, token) ?? throw new PlanningException("planning.version_not_found");
        if (state.State != "Draft") throw new PlanningException("planning.version_immutable");
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in parsed.Rows)
        {
            var signature = Signature(row.LegalEntityId, row.PeriodStart, row.MetricCode, row.DimensionKey);
            if (!signatures.Add(signature)) throw new PlanningException("planning.import_duplicate_coordinate");
            await EnsureGridCellMutable(c, tenant.Value, request.PlanVersionId, row.LegalEntityId, row.PeriodStart, row.MetricCode, signature, row.DimensionKey, token);
            var coordinate = await Scalar<Guid?>(c, "select id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 and legal_entity_id=$3 and period_start=$4 and metric_code=$5 and signature=$6", token, tenant.Value, request.PlanVersionId, row.LegalEntityId, row.PeriodStart, row.MetricCode, signature)
                ?? await Scalar<Guid?>(c, "select id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 and legal_entity_id=$3 and period_start=$4 and metric_code=$5 and dimension_key=$6::jsonb order by id desc limit 1", token, tenant.Value, request.PlanVersionId, row.LegalEntityId, row.PeriodStart, row.MetricCode, Json(row.DimensionKey))
                ?? Guid.CreateVersion7();
            await Exec(c, "insert into business_planning.planning_coordinate(tenant_id,id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key,signature) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8) on conflict do nothing", token, tenant.Value, coordinate, request.PlanVersionId, row.LegalEntityId, row.PeriodStart, row.MetricCode, Json(row.DimensionKey), signature);
            await Exec(c, "insert into business_planning.planning_input_fact(tenant_id,id,coordinate_id,amount,currency_code,value_kind,source_label,explicit_zero,updated_by) values($1,$2,$3,$4,$5,'Input',$6,$7,$8) on conflict(tenant_id,coordinate_id) do update set amount=excluded.amount,currency_code=excluded.currency_code,source_label=excluded.source_label,explicit_zero=excluded.explicit_zero,updated_by=excluded.updated_by,updated_at=now()", token, tenant.Value, Guid.CreateVersion7(), coordinate, row.Amount, row.CurrencyCode, Db(row.SourceLabel), row.ExplicitZero, actor);
        }
        await Audit(c, tenant.Value, actor, "planning_csv_import_committed", request.PlanVersionId, token);
        return parsed.View with { Committed = true };
    }, ct);

    public Task<IReadOnlyList<RollupLineView>> QueryRollupAsync(TenantId tenant, RollupRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        if (request.LegalEntityIds.Count == 0 || request.LegalEntityIds.Count > 100 || request.LegalEntityIds.Distinct().Count() != request.LegalEntityIds.Count) throw new PlanningException("planning.rollup_scope_invalid");
        if (request.From > request.To || string.IsNullOrWhiteSpace(request.MetricCode) || string.IsNullOrWhiteSpace(request.ReportingCurrencyCode) || request.ReportingCurrencyCode.Trim().Length != 3) throw new PlanningException("planning.rollup_request_invalid");
        var rollupEntityCount = await Scalar<long>(c, "select count(*) from platform.legal_entity where tenant_id=$1 and id=any($2)", token, tenant.Value, request.LegalEntityIds.ToArray());
        if (rollupEntityCount != request.LegalEntityIds.Count) throw new PlanningException("planning.entity_scope_invalid");
        var result = new List<RollupLineView>();
        var sourceRows = new List<RollupSeed>();
        await using var command = Cmd(c, "select c.id,c.legal_entity_id,c.period_start,coalesce(cf.amount,i.amount,0),coalesce(cf.currency_code,i.currency_code,'EGP') from business_planning.planning_coordinate c left join business_planning.planning_input_fact i on i.tenant_id=c.tenant_id and i.coordinate_id=c.id left join business_planning.planning_calculated_fact cf on cf.tenant_id=c.tenant_id and cf.coordinate_id=c.id where c.tenant_id=$1 and c.plan_version_id=$2 and c.legal_entity_id = any($3) and c.metric_code=$4 and c.period_start between $5 and $6 order by c.legal_entity_id,c.period_start", tenant.Value, request.PlanVersionId, request.LegalEntityIds.ToArray(), request.MetricCode, request.From, request.To);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            sourceRows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetDecimal(3), reader.GetString(4).Trim().ToUpperInvariant()));
        }
        await reader.CloseAsync();
        foreach (var sourceRow in sourceRows)
        {
            var entity = sourceRow.Entity; var period = sourceRow.Period; var sourceAmount = sourceRow.Amount; var sourceCurrency = sourceRow.Currency; var reportingCurrency = request.ReportingCurrencyCode.Trim().ToUpperInvariant();
            decimal rate = string.Equals(sourceCurrency, reportingCurrency, StringComparison.OrdinalIgnoreCase) ? 1m : await Scalar<decimal?>(c, "select rate from business_planning.planning_fx_rate where tenant_id=$1 and plan_version_id=$2 and period_start=$3 and from_currency=$4 and to_currency=$5 order by rate_type='Scenario' desc limit 1", token, tenant.Value, request.PlanVersionId, period, sourceCurrency, reportingCurrency) ?? throw new PlanningException("planning.fx_rate_missing");
            result.Add(new(sourceRow.CoordinateId, entity, period, request.MetricCode, sourceAmount, rate, decimal.Round(sourceAmount * rate, 4, MidpointRounding.AwayFromZero), sourceCurrency, reportingCurrency, "planning_coordinate_input_or_calculated"));
        }
        return (IReadOnlyList<RollupLineView>)result;
    }, ct);

    public Task<IReadOnlyList<StatementLineView>> QueryIntegratedStatementAsync(TenantId tenant, StatementRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        if (request.From > request.To) throw new PlanningException("planning.statement_period_invalid");
        if (string.IsNullOrWhiteSpace(request.CurrencyCode) || request.CurrencyCode.Trim().Length != 3) throw new PlanningException("planning.currency_invalid");
        var data = await ReadPlanningAmountsAsync(c, tenant.Value, request.PlanVersionId, token, request.LegalEntityId, request.From, request.To);
        var workforce = await ReadWorkforceCostsAsync(c, tenant.Value, request.PlanVersionId, request.LegalEntityId, request.From, request.To, token);
        var opex = await ReadOpexCostsAsync(c, tenant.Value, request.PlanVersionId, request.LegalEntityId, request.From, request.To, token);
        var capex = await ReadCapexCostsAsync(c, tenant.Value, request.PlanVersionId, request.LegalEntityId, request.From, request.To, token);
        var workingCapital = await ReadWorkingCapitalAsync(c, tenant.Value, request.PlanVersionId, request.LegalEntityId, request.From, request.To, token);
        var result = new List<StatementLineView>();
        var openingCash = data.GetValueOrDefault((request.LegalEntityId, request.From, "OpeningCash"));
        var openingEquity = data.GetValueOrDefault((request.LegalEntityId, request.From, "OpeningEquity"));
        decimal priorCash = openingCash, priorAr = 0m, priorInventory = 0m, priorAp = 0m, netPpe = 0m, retained = openingEquity;
        for (var period = new DateOnly(request.From.Year, request.From.Month, 1); period <= request.To; period = period.AddMonths(1))
        {
            decimal Get(string metric) => data.GetValueOrDefault((request.LegalEntityId, period, metric));
            var revenue = Get("Revenue"); var cogs = Get("Cogs") + Get("COGS"); var opexAmount = opex.TryGetValue(period, out var dedicatedOpex) ? dedicatedOpex : Get("Opex") + Get("OPEX");
            var workforceCost = workforce.GetValueOrDefault(period); var capexAmount = capex.Where(x => x.PurchasePeriod == period).Sum(x => x.Amount);
            var depreciation = capex.Where(x => x.InServicePeriod is { } service && service <= period).Sum(x => decimal.Round(x.Amount / x.UsefulLifeMonths, 4, MidpointRounding.AwayFromZero));
            var financing = Get("FinancingCost"); var tax = Get("Tax"); var netIncome = revenue - cogs - opexAmount - workforceCost - depreciation - financing - tax;
            var wc = workingCapital.GetValueOrDefault(period) ?? new WorkingCapitalSeed(0m, 0m, 0m); var ar = decimal.Round(revenue * wc.Dso / 30m, 4); var ap = decimal.Round((cogs + opexAmount + workforceCost) * wc.Dpo / 30m, 4); var inventory = decimal.Round(cogs * wc.InventoryDays / 30m, 4);
            var cfo = netIncome - (ar - priorAr) - (inventory - priorInventory) + (ap - priorAp); var cfi = -capexAmount; var cff = Get("FinancingCashFlow"); var closingCash = priorCash + cfo + cfi + cff;
            netPpe += capexAmount - depreciation; retained += netIncome; var assets = closingCash + ar + inventory + netPpe; var liabilities = ap + Get("Debt"); var balanceDifference = decimal.Round(assets - liabilities - retained, 4);
            if (request.StatementType.Equals("P&L", StringComparison.OrdinalIgnoreCase) || request.StatementType.Equals("IncomeStatement", StringComparison.OrdinalIgnoreCase))
            {
                Add(result, "P&L", "Revenue", period, revenue, "Revenue driver/input/canonical Actual mapping", true); Add(result, "P&L", "Cogs", period, cogs, "COGS input/canonical Actual mapping", true); Add(result, "P&L", "Opex", period, opexAmount, "Dedicated OPEX category/driver workflow", true); Add(result, "P&L", "Workforce", period, workforceCost, "Aggregate workforce plan", true); Add(result, "P&L", "Depreciation", period, depreciation, "CapEx planned in-service schedule", true); Add(result, "P&L", "NetIncome", period, netIncome, "Integrated P&L", true);
            }
            else if (request.StatementType.Equals("BalanceSheet", StringComparison.OrdinalIgnoreCase))
            {
                Add(result, "BalanceSheet", "Cash", period, closingCash, "Integrated Cash Flow closing cash", balanceDifference == 0m); Add(result, "BalanceSheet", "AccountsReceivable", period, ar, "Revenue × DSO / 30", balanceDifference == 0m); Add(result, "BalanceSheet", "Inventory", period, inventory, "COGS × inventory days / 30", balanceDifference == 0m); Add(result, "BalanceSheet", "NetFixedAssets", period, netPpe, "CapEx less planned depreciation", balanceDifference == 0m); Add(result, "BalanceSheet", "AccountsPayable", period, ap, "Operating cost × DPO / 30", balanceDifference == 0m); Add(result, "BalanceSheet", "Equity", period, retained, "Opening equity plus retained projected net income", balanceDifference == 0m); Add(result, "BalanceSheet", "BalanceCheck", period, balanceDifference, "Assets - liabilities - equity", balanceDifference == 0m);
            }
            else if (request.StatementType.Equals("CashFlow", StringComparison.OrdinalIgnoreCase))
            {
                Add(result, "CashFlow", "OpeningCash", period, priorCash, "Prior projected closing cash", true); Add(result, "CashFlow", "OperatingCashFlow", period, cfo, "Net income and working-capital movements", true); Add(result, "CashFlow", "InvestingCashFlow", period, cfi, "Planned CapEx", true); Add(result, "CashFlow", "FinancingCashFlow", period, cff, "Explicit financing cash-flow input", true); Add(result, "CashFlow", "ClosingCash", period, closingCash, "Opening cash plus operating/investing/financing movements", true);
            }
            else throw new PlanningException("planning.statement_type_invalid");
            priorCash = closingCash; priorAr = ar; priorInventory = inventory; priorAp = ap;
        }
        var calculationRunId = Guid.CreateVersion7();
        await Exec(c, "insert into business_planning.planning_calculation_run(tenant_id,id,plan_version_id,state,input_watermark,completed_at) values($1,$2,$3,'Succeeded',$4,now())", token, tenant.Value, calculationRunId, request.PlanVersionId, $"statement:{request.StatementType}:{request.From:yyyy-MM-dd}:{request.To:yyyy-MM-dd}");
        foreach (var line in result)
        {
            await Exec(c, "insert into business_planning.planning_statement_projection(tenant_id,id,plan_version_id,legal_entity_id,statement_type,line_code,period_start,amount,source_basis,calculation_run_id) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10) on conflict(tenant_id,plan_version_id,legal_entity_id,statement_type,line_code,period_start) do update set amount=excluded.amount,source_basis=excluded.source_basis,calculation_run_id=excluded.calculation_run_id,created_at=now()", token, tenant.Value, Guid.CreateVersion7(), request.PlanVersionId, request.LegalEntityId, line.StatementType, line.LineCode, line.PeriodStart, line.Amount, line.SourceBasis, calculationRunId);
        }
        return (IReadOnlyList<StatementLineView>)result;
    }, ct);

    private static void Add(List<StatementLineView> result, string type, string line, DateOnly period, decimal amount, string basis, bool balanced) => result.Add(new(type, line, period, amount, amount, basis, balanced));

    private static async Task<Dictionary<(Guid Entity, DateOnly Period, string Metric), decimal>> ReadPlanningAmountsAsync(ITransactionContext c, Guid tenant, Guid version, CancellationToken ct, Guid? entity = null, DateOnly? from = null, DateOnly? to = null)
    {
        var result = new Dictionary<(Guid, DateOnly, string), decimal>();
        await using var command = Cmd(c, "select c.legal_entity_id,c.period_start,c.metric_code,coalesce(cf.amount,i.amount,0) from business_planning.planning_coordinate c left join business_planning.planning_input_fact i on i.tenant_id=c.tenant_id and i.coordinate_id=c.id left join business_planning.planning_calculated_fact cf on cf.tenant_id=c.tenant_id and cf.coordinate_id=c.id where c.tenant_id=$1 and c.plan_version_id=$2 and ($3::uuid is null or c.legal_entity_id=$3) and ($4::date is null or c.period_start >= $4) and ($5::date is null or c.period_start <= $5)", tenant, version, Db(entity), Db(from), Db(to));
        await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result[(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.GetString(2))] = reader.GetDecimal(3); return result;
    }

    private static Task<long> ReadInputRevisionAsync(ITransactionContext c, Guid tenant, Guid version, CancellationToken ct) => Scalar<long>(c, "select coalesce(max(i.row_version),0) from business_planning.planning_input_fact i join business_planning.planning_coordinate x on x.tenant_id=i.tenant_id and x.id=i.coordinate_id where i.tenant_id=$1 and x.plan_version_id=$2", ct, tenant, version);

    private static async Task ReadRevenueAmountsAsync(ITransactionContext c, Guid tenant, Guid version, Dictionary<(Guid Entity, DateOnly Period, string Metric), decimal> values, CancellationToken ct)
    {
        await using var command = Cmd(c, "select legal_entity_id,period_start,sum(volume*price) from business_planning.revenue_plan_driver where tenant_id=$1 and plan_version_id=$2 group by legal_entity_id,period_start", tenant, version); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) values[(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), "Revenue")] = reader.GetDecimal(2);
    }

    private static async Task<List<FormulaSeed>> ReadFormulasAsync(ITransactionContext c, Guid tenant, Guid version, CancellationToken ct)
    {
        var list = new List<FormulaSeed>(); await using var command = Cmd(c, "select f.code,f.expression,f.output_metric_code,coalesce(array_agg(d.dependency_code order by d.dependency_code) filter (where d.dependency_code is not null),'{}') from business_planning.planning_formula f join business_planning.plan_version v on v.tenant_id=f.tenant_id and v.model_version_id=f.model_version_id left join business_planning.planning_formula_dependency d on d.tenant_id=f.tenant_id and d.formula_id=f.id where f.tenant_id=$1 and v.id=$2 and f.active group by f.id,f.code,f.expression,f.output_metric_code", tenant, version); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(new FormulaSeed(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<string[]>(3))); return list;
    }

    private static List<FormulaSeed> TopologicalOrder(IReadOnlyList<FormulaSeed> formulas, IReadOnlyDictionary<string, IReadOnlyCollection<string>> graph)
    {
        var result = new List<FormulaSeed>(); var pending = formulas.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        while (pending.Count > 0)
        {
            var ready = pending.Values.Where(x => x.Dependencies.All(d => !pending.ContainsKey(d))).OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ToArray();
            if (ready.Length == 0) throw new PlanningException("planning.formula_cycle");
            result.AddRange(ready); foreach (var formula in ready) pending.Remove(formula.Code);
        }
        return result;
    }

    private static async Task<Dictionary<DateOnly, decimal>> ReadWorkforceCostsAsync(ITransactionContext c, Guid tenant, Guid version, Guid entity, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var result = new Dictionary<DateOnly, decimal>(); await using var command = Cmd(c, "select period_start,sum(fte*average_cost) from business_planning.workforce_plan_line where tenant_id=$1 and plan_version_id=$2 and legal_entity_id=$3 and period_start between $4 and $5 group by period_start", tenant, version, entity, from, to); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result[reader.GetFieldValue<DateOnly>(0)] = reader.GetDecimal(1); return result;
    }

    private static async Task<Dictionary<DateOnly, decimal>> ReadOpexCostsAsync(ITransactionContext c, Guid tenant, Guid version, Guid entity, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var result = new Dictionary<DateOnly, decimal>();
        var dedicated = new List<OpexIdentity>();
        await using (var command = Cmd(c, "select period_start,department_ref,category_code,account_ref,cost_center_ref,planned_amount from business_planning.opex_plan_line where tenant_id=$1 and plan_version_id=$2 and legal_entity_id=$3 and period_start between $4 and $5", tenant, version, entity, from, to))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                dedicated.Add(new(reader.GetFieldValue<DateOnly>(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetDecimal(5)));
            }
        }

        foreach (var line in dedicated)
            result[line.Period] = result.GetValueOrDefault(line.Period) + line.Amount;

        // Generic grid OPEX remains valid for identities not replaced by a dedicated line.
        // Replacement is coordinate-scoped: a Facilities/6100/HQ line must not suppress an
        // unrelated generic OPEX coordinate in the same period.
        await using (var command = Cmd(c, "select c.period_start,c.dimension_key::text,coalesce(cf.amount,i.amount,0) from business_planning.planning_coordinate c left join business_planning.planning_input_fact i on i.tenant_id=c.tenant_id and i.coordinate_id=c.id left join business_planning.planning_calculated_fact cf on cf.tenant_id=c.tenant_id and cf.coordinate_id=c.id where c.tenant_id=$1 and c.plan_version_id=$2 and c.legal_entity_id=$3 and c.metric_code in ('Opex','OPEX') and c.period_start between $4 and $5", tenant, version, entity, from, to))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var period = reader.GetFieldValue<DateOnly>(0);
                var dimension = JsonDocument.Parse(reader.GetString(1)).RootElement;
                if (!dedicated.Any(x => x.Period == period && x.Matches(dimension)))
                    result[period] = result.GetValueOrDefault(period) + reader.GetDecimal(2);
            }
        }
        return result;
    }

    private static async Task<List<CapexSeed>> ReadCapexCostsAsync(ITransactionContext c, Guid tenant, Guid version, Guid entity, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var result = new List<CapexSeed>(); await using var command = Cmd(c, "select planned_amount,purchase_period,in_service_period,useful_life_months from business_planning.capex_plan_item where tenant_id=$1 and plan_version_id=$2 and legal_entity_id=$3 and purchase_period <= $5 and (in_service_period is null or in_service_period <= $5) and state in ('Draft','Prioritized','Approved')", tenant, version, entity, from, to); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result.Add(new CapexSeed(reader.GetDecimal(0), reader.GetFieldValue<DateOnly>(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2), reader.GetInt32(3))); return result;
    }

    private static async Task<Dictionary<DateOnly, WorkingCapitalSeed>> ReadWorkingCapitalAsync(ITransactionContext c, Guid tenant, Guid version, Guid entity, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var result = new Dictionary<DateOnly, WorkingCapitalSeed>(); await using var command = Cmd(c, "select period_start,dso,dpo,inventory_days from business_planning.working_capital_assumption where tenant_id=$1 and plan_version_id=$2 and legal_entity_id=$3 and period_start between $4 and $5", tenant, version, entity, from, to); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result[reader.GetFieldValue<DateOnly>(0)] = new WorkingCapitalSeed(reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3)); return result;
    }

    private static ParsedCsv ParseCsv(Guid version, string csv)
    {
        var errors = new List<string>(); var rows = new List<CsvRow>(); var lines = csv.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length is 0 or > 5001) return new(new(version, false, 0, ["planning.import_row_limit"]), rows);
        for (var i = 0; i < lines.Length; i++)
        {
            string[] fields;
            try { fields = ParseCsvLine(lines[i]); }
            catch (PlanningException) { errors.Add($"row:{i + 1}:planning.import_row_invalid"); continue; }
            if (i == 0 && fields.Length > 0 && fields[0].Equals("legalEntityId", StringComparison.OrdinalIgnoreCase)) continue;
            if (fields.Length < 5 || !Guid.TryParse(fields[0], out var entity) || !DateOnly.TryParse(fields[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var period) || !decimal.TryParse(fields[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || string.IsNullOrWhiteSpace(fields[2]) || fields[4].Trim().Length != 3) { errors.Add($"row:{i + 1}:planning.import_row_invalid"); continue; }
            var explicitZero = fields.Length > 5 && bool.TryParse(fields[5], out var zero) && zero; rows.Add(new CsvRow(entity, period, fields[2].Trim(), JsonDocument.Parse("{}").RootElement.Clone(), amount, fields[4].Trim().ToUpperInvariant(), explicitZero, fields.Length > 6 ? fields[6].Trim() : null));
        }
        return new(new CsvImportView(version, false, rows.Count, errors), rows);
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"') { value.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted) { fields.Add(value.ToString()); value.Clear(); }
            else value.Append(character);
        }
        if (quoted) throw new PlanningException("planning.import_row_invalid");
        fields.Add(value.ToString());
        return fields.ToArray();
    }

    private sealed record FormulaSeed(string Code, string Expression, string OutputMetricCode, IReadOnlyList<string> Dependencies);
    private sealed record WorkingCapitalSeed(decimal Dso, decimal Dpo, decimal InventoryDays);
    private sealed record CapexSeed(decimal Amount, DateOnly PurchasePeriod, DateOnly? InServicePeriod, int UsefulLifeMonths);
    private sealed record RollupSeed(Guid CoordinateId, Guid Entity, DateOnly Period, decimal Amount, string Currency);
    private sealed record CsvRow(Guid LegalEntityId, DateOnly PeriodStart, string MetricCode, JsonElement DimensionKey, decimal Amount, string CurrencyCode, bool ExplicitZero, string? SourceLabel);
    private sealed record OpexIdentity(DateOnly Period, Guid? DepartmentRef, string CategoryCode, string? AccountRef, string? CostCenterRef, decimal Amount)
    {
        public bool Matches(JsonElement dimension)
        {
            if (dimension.ValueKind != JsonValueKind.Object) return false;
            return MatchesGuid(dimension, "departmentRef", DepartmentRef)
                && MatchesText(dimension, "categoryCode", CategoryCode)
                && MatchesText(dimension, "accountRef", AccountRef)
                && MatchesText(dimension, "costCenterRef", CostCenterRef)
                && (DepartmentRef is not null || AccountRef is not null || CostCenterRef is not null || dimension.TryGetProperty("categoryCode", out _));
        }

        private static bool MatchesGuid(JsonElement value, string property, Guid? expected)
        {
            if (!value.TryGetProperty(property, out var actual)) return expected is null;
            return Guid.TryParse(actual.GetString(), out var parsed) && expected == parsed;
        }

        private static bool MatchesText(JsonElement value, string property, string? expected)
        {
            if (!value.TryGetProperty(property, out var actual)) return expected is null;
            return string.Equals(actual.GetString(), expected, StringComparison.OrdinalIgnoreCase);
        }
    }
    private sealed record ParsedCsv(CsvImportView View, IReadOnlyList<CsvRow> Rows);
}
