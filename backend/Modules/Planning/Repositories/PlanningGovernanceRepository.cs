using Core.Transactions;
using Shared.Primitives.Identifiers;

namespace Modules.Planning;

public sealed partial class PlanningRepository
{
    public Task<PlanningTargetView> CreatePlanningTargetAsync(TenantId tenant, Guid actor, CreatePlanningTargetRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        if (request.TargetAmount < 0 || request.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.MetricCode) || request.CurrencyCode.Trim().Length != 3) throw new PlanningException("planning.target_invalid");
        await EnsureDraftPlanVersion(c, tenant.Value, request.PlanVersionId, token);
        if (!await Scalar<bool>(c, "select exists(select 1 from business_planning.planning_metric where tenant_id=$1 and code=$2 and active)", token, tenant.Value, request.MetricCode.Trim())) throw new PlanningException("planning.metric_not_found");
        var id = Guid.CreateVersion7();
        await Exec(c, "insert into business_planning.planning_target(tenant_id,id,plan_version_id,legal_entity_id,period_start,metric_code,target_amount,currency_code,reason,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", token, tenant.Value, id, request.PlanVersionId, request.LegalEntityId, request.PeriodStart, request.MetricCode.Trim(), request.TargetAmount, request.CurrencyCode.Trim().ToUpperInvariant(), Db(request.Reason), actor);
        await Audit(c, tenant.Value, actor, "planning_target_created", id, token);
        return await ReadPlanningTarget(c, tenant.Value, id, token) ?? throw new PlanningException("planning.target_not_found");
    }, ct);

    public Task<PlanningTargetView> CreatePlanningProposalAsync(TenantId tenant, Guid actor, CreatePlanningProposalRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        if (request.ProposedAmount < 0 || request.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.MetricCode) || request.CurrencyCode.Trim().Length != 3) throw new PlanningException("planning.proposal_invalid");
        await EnsureDraftPlanVersion(c, tenant.Value, request.PlanVersionId, token);
        var target = await Scalar<Guid?>(c, "select id from business_planning.planning_target where tenant_id=$1 and plan_version_id=$2 and legal_entity_id=$3 and period_start=$4 and metric_code=$5", token, tenant.Value, request.PlanVersionId, request.LegalEntityId, request.PeriodStart, request.MetricCode.Trim()) ?? throw new PlanningException("planning.target_not_found");
        var id = Guid.CreateVersion7();
        await Exec(c, "insert into business_planning.planning_proposal(tenant_id,id,target_id,department_ref,proposed_amount,currency_code,reason,created_by) values($1,$2,$3,$4,$5,$6,$7,$8) on conflict(tenant_id,target_id,department_ref) do update set proposed_amount=excluded.proposed_amount,currency_code=excluded.currency_code,reason=excluded.reason,state='Draft'", token, tenant.Value, id, target, Db(request.DepartmentRef), request.ProposedAmount, request.CurrencyCode.Trim().ToUpperInvariant(), Db(request.Reason), actor);
        var actualId = await Scalar<Guid>(c, "select id from business_planning.planning_proposal where tenant_id=$1 and target_id=$2 and department_ref is not distinct from $3", token, tenant.Value, target, Db(request.DepartmentRef));
        await Audit(c, tenant.Value, actor, "planning_proposal_created", actualId, token);
        return await ReadPlanningTarget(c, tenant.Value, target, token) ?? throw new PlanningException("planning.target_not_found");
    }, ct);

    public Task<PlanningTargetView> AdjustPlanningProposalAsync(TenantId tenant, Guid actor, Guid proposalId, AdjustPlanningProposalRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        if (request.AdjustedAmount < 0 || string.IsNullOrWhiteSpace(request.Reason)) throw new PlanningException("planning.adjustment_invalid");
        var target = await Scalar<Guid?>(c, "select t.id from business_planning.planning_proposal p join business_planning.planning_target t on t.tenant_id=p.tenant_id and t.id=p.target_id where p.tenant_id=$1 and p.id=$2", token, tenant.Value, proposalId) ?? throw new PlanningException("planning.proposal_not_found");
        var version = await Scalar<Guid>(c, "select plan_version_id from business_planning.planning_target where tenant_id=$1 and id=$2", token, tenant.Value, target); await EnsureDraftPlanVersion(c, tenant.Value, version, token);
        await Exec(c, "update business_planning.planning_proposal set adjusted_amount=$3,adjustment_reason=$4,adjusted_by=$5,adjusted_at=now(),state='Adjusted' where tenant_id=$1 and id=$2", token, tenant.Value, proposalId, request.AdjustedAmount, request.Reason.Trim(), actor);
        await Audit(c, tenant.Value, actor, "planning_proposal_adjusted", proposalId, token);
        return await ReadPlanningTarget(c, tenant.Value, target, token) ?? throw new PlanningException("planning.target_not_found");
    }, ct);

    private static async Task<PlanningTargetView?> ReadPlanningTarget(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct)
    {
        await using var command = Cmd(c, "select t.id,t.plan_version_id,t.legal_entity_id,t.period_start,t.metric_code,t.target_amount,p.proposed_amount,(p.proposed_amount-t.target_amount),p.adjusted_amount,t.currency_code,t.state from business_planning.planning_target t left join lateral (select p.proposed_amount,p.adjusted_amount from business_planning.planning_proposal p where p.tenant_id=t.tenant_id and p.target_id=t.id order by p.created_at desc,p.id desc limit 1) p on true where t.tenant_id=$1 and t.id=$2", tenant, id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new PlanningTargetView(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.GetString(4), reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6), reader.IsDBNull(7) ? null : reader.GetDecimal(7), reader.IsDBNull(8) ? null : reader.GetDecimal(8), reader.GetString(9).Trim(), reader.GetString(10)) : null;
    }

    private static async Task EnsureDraftPlanVersion(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct)
    {
        var state = await Scalar<string>(c, "select state from business_planning.plan_version where tenant_id=$1 and id=$2 for update", ct, tenant, id);
        if (state is null) throw new PlanningException("planning.version_not_found");
        if (!string.Equals(state, "Draft", StringComparison.Ordinal)) throw new PlanningException("planning.version_immutable");
    }

    private static async Task EnsureDraftModelVersion(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct)
    {
        var state = await Scalar<string>(c, "select state from business_planning.planning_model_version where tenant_id=$1 and id=$2", ct, tenant, id);
        if (state is null) throw new PlanningException("planning.model_version_not_found");
        if (!string.Equals(state, "Draft", StringComparison.Ordinal)) throw new PlanningException("planning.model_version_immutable");
    }
}
