using Core.Transactions;
using Shared.Primitives.Identifiers;

namespace Modules.Planning;

public sealed partial class PlanningRepository
{
    public Task<OpexPlanLineView> AddOpexLineAsync(TenantId tenant, Guid actor, OpexPlanLineRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        await EnsureDraftPlanVersion(c, tenant.Value, request.PlanVersionId, token);
        var line = CalculateOpex(request);
        var existing = await Scalar<Guid?>(c, "select id from business_planning.opex_plan_line where tenant_id=$1 and plan_version_id=$2 and legal_entity_id=$3 and department_ref is not distinct from $4 and category_code=$5 and period_start=$6 and account_ref is not distinct from $7 and cost_center_ref is not distinct from $8", token, tenant.Value, request.PlanVersionId, request.LegalEntityId, Db(request.DepartmentRef), line.CategoryCode, request.PeriodStart, Db(request.AccountRef), Db(request.CostCenterRef));
        if (existing is { } id)
        {
            await Exec(c, "update business_planning.opex_plan_line set method=$4,base_amount=$5,driver_value=$6,rate=$7,growth_percent=$8,inflation_percent=$9,planned_amount=$10,currency_code=$11,source=$12 where tenant_id=$1 and id=$2 and plan_version_id=$3", token, tenant.Value, id, request.PlanVersionId, line.Method, line.BaseAmount, line.DriverValue, line.Rate, line.GrowthPercent, line.InflationPercent, line.PlannedAmount, line.CurrencyCode, line.Source);
            await Audit(c, tenant.Value, actor, "opex_plan_line_updated", id, token);
            return await ReadOpexLine(c, tenant.Value, id, token) ?? throw new PlanningException("planning.opex_not_found");
        }

        var created = Guid.CreateVersion7();
        await Exec(c, "insert into business_planning.opex_plan_line(tenant_id,id,plan_version_id,legal_entity_id,department_ref,category_code,account_ref,cost_center_ref,period_start,method,base_amount,driver_value,rate,growth_percent,inflation_percent,planned_amount,currency_code,source,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19)", token, tenant.Value, created, request.PlanVersionId, request.LegalEntityId, Db(request.DepartmentRef), line.CategoryCode, Db(request.AccountRef), Db(request.CostCenterRef), request.PeriodStart, line.Method, line.BaseAmount, line.DriverValue, line.Rate, line.GrowthPercent, line.InflationPercent, line.PlannedAmount, line.CurrencyCode, line.Source, actor);
        await Audit(c, tenant.Value, actor, "opex_plan_line_created", created, token);
        return new OpexPlanLineView(created, request.PlanVersionId, request.LegalEntityId, request.DepartmentRef, line.CategoryCode, request.AccountRef, request.CostCenterRef, request.PeriodStart, line.Method, line.BaseAmount, line.DriverValue, line.Rate, line.GrowthPercent, line.InflationPercent, line.PlannedAmount, line.CurrencyCode, line.Source);
    }, ct);

    public Task<IReadOnlyList<OpexPlanLineView>> ListOpexLinesAsync(TenantId tenant, Guid planVersionId, Guid? legalEntityId, DateOnly? from, DateOnly? to, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        var list = new List<OpexPlanLineView>();
        await using var command = Cmd(c, "select id,plan_version_id,legal_entity_id,department_ref,category_code,account_ref,cost_center_ref,period_start,method,base_amount,driver_value,rate,growth_percent,inflation_percent,planned_amount,currency_code,source from business_planning.opex_plan_line where tenant_id=$1 and plan_version_id=$2 and ($3::uuid is null or legal_entity_id=$3) and ($4::date is null or period_start >= $4) and ($5::date is null or period_start <= $5) order by period_start,category_code,id limit 5000", tenant.Value, planVersionId, Db(legalEntityId), Db(from), Db(to));
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) list.Add(ReadOpex(reader));
        return (IReadOnlyList<OpexPlanLineView>)list;
    }, ct);

    private static OpexCalculation CalculateOpex(OpexPlanLineRequest request)
    {
        var method = request.Method.Trim();
        if (method is not ("Fixed" or "Variable" or "Recurring" or "DriverBased")) throw new PlanningException("planning.opex_method_invalid");
        if (string.IsNullOrWhiteSpace(request.CategoryCode) || request.CategoryCode.Trim().Length > 80) throw new PlanningException("planning.opex_category_invalid");
        if (request.BaseAmount < 0 || request.DriverValue < 0 || request.Rate < 0 || request.GrowthPercent < -100 || request.InflationPercent < -100) throw new PlanningException("planning.opex_input_invalid");
        var raw = method is "Variable" or "DriverBased" ? request.DriverValue * request.Rate : request.BaseAmount;
        var amount = decimal.Round(raw * (1m + request.GrowthPercent / 100m) * (1m + request.InflationPercent / 100m), 4, MidpointRounding.AwayFromZero);
        if (amount < 0) throw new PlanningException("planning.opex_input_invalid");
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (currency.Length != 3) throw new PlanningException("planning.currency_invalid");
        return new(method, request.BaseAmount, request.DriverValue, request.Rate, request.GrowthPercent, request.InflationPercent, amount, request.CategoryCode.Trim(), currency, string.IsNullOrWhiteSpace(request.Source) ? "Manual" : request.Source.Trim());
    }

    private static async Task<OpexPlanLineView?> ReadOpexLine(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct)
    {
        await using var command = Cmd(c, "select id,plan_version_id,legal_entity_id,department_ref,category_code,account_ref,cost_center_ref,period_start,method,base_amount,driver_value,rate,growth_percent,inflation_percent,planned_amount,currency_code,source from business_planning.opex_plan_line where tenant_id=$1 and id=$2", tenant, id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadOpex(reader) : null;
    }

    private static OpexPlanLineView ReadOpex(Npgsql.NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetFieldValue<DateOnly>(7), reader.GetString(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11), reader.GetDecimal(12), reader.GetDecimal(13), reader.GetDecimal(14), reader.GetString(15).Trim(), reader.GetString(16));

    private sealed record OpexCalculation(string Method, decimal BaseAmount, decimal DriverValue, decimal Rate, decimal GrowthPercent, decimal InflationPercent, decimal PlannedAmount, string CategoryCode, string CurrencyCode, string Source);
}
