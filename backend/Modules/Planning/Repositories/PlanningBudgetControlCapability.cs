using Core.Transactions;
using Npgsql;
using Shared.Primitives.Identifiers;
using System.Text.Json;

namespace Modules.Planning;

/// <summary>
/// Transactional budget-control owner. Source modules call this capability while their
/// own document transaction is open, so a rejected reservation rolls the source write
/// back with the control decision.
/// </summary>
public sealed class PlanningBudgetControlCapability : ITransactionalPlanningBudgetControlCapability
{
    public async Task<Guid?> ResolveApplicableBucketAsync(ITransactionContext context, TenantId tenantId,
        PlanningBudgetControlScope scope, CancellationToken cancellationToken = default)
    {
        if (scope.LegalEntityId == Guid.Empty || scope.PeriodStart == default || string.IsNullOrWhiteSpace(scope.MetricCode) || !IsObject(scope.DimensionKeyJson))
            throw new PlanningBudgetControlException("planning.budget_scope_invalid");

        if (scope.RequestedBucketId is { } requestedBucketId)
        {
            await using var command = new NpgsqlCommand("select b.id,b.legal_entity_id,b.period_start,b.metric_code,b.dimension_key::text,p.active,p.mode,p.control_horizon,(p.effective_from is null or p.effective_from <= $3) and (p.effective_to is null or p.effective_to >= $3),v.state from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id join business_planning.plan_version v on v.tenant_id=b.tenant_id and v.id=b.plan_version_id where b.tenant_id=$1 and b.id=$2 for update", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(requestedBucketId); command.Parameters.AddWithValue(scope.PeriodStart);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new PlanningBudgetControlException("planning.bucket_not_found");
            var bucketId = reader.GetGuid(0);
            var horizon = reader.GetString(7);
            if (reader.GetGuid(1) != scope.LegalEntityId) throw new PlanningBudgetControlException("planning.budget_scope_invalid");
            if (reader.GetFieldValue<DateOnly>(2) != NormalizeControlPeriod(scope.PeriodStart, horizon) || !string.Equals(reader.GetString(3), scope.MetricCode.Trim(), StringComparison.OrdinalIgnoreCase) || !JsonContains(scope.DimensionKeyJson, reader.GetString(4)))
                throw new PlanningBudgetControlException("planning.budget_bucket_context_invalid");
            if (!reader.GetBoolean(5) || !reader.GetBoolean(8) || !string.Equals(reader.GetString(9), "Published", StringComparison.OrdinalIgnoreCase)) throw new PlanningBudgetControlException("planning.budget_policy_inactive");
            if (string.Equals(reader.GetString(6), "None", StringComparison.OrdinalIgnoreCase)) throw new PlanningBudgetControlException("planning.budget_bucket_uncontrolled");
            return bucketId;
        }

        await using (var command = new NpgsqlCommand("select b.id,p.mode from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id join business_planning.plan_version v on v.tenant_id=b.tenant_id and v.id=b.plan_version_id where b.tenant_id=$1 and b.legal_entity_id=$2 and b.period_start=(case p.control_horizon when 'Annual' then make_date(extract(year from $3::date)::int,1,1) when 'Quarterly' then date_trunc('quarter',$3::date)::date else date_trunc('month',$3::date)::date end) and lower(b.metric_code)=lower($4) and $5::jsonb @> b.dimension_key and p.active=true and p.mode <> 'None' and v.state='Published' and (p.effective_from is null or p.effective_from <= $3) and (p.effective_to is null or p.effective_to >= $3) order by length(b.dimension_key::text) desc,b.id limit 2", context.Connection, context.Transaction))
        {
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(scope.LegalEntityId); command.Parameters.AddWithValue(scope.PeriodStart); command.Parameters.AddWithValue(scope.MetricCode.Trim()); command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Jsonb, scope.DimensionKeyJson);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var bucketId = reader.GetGuid(0);
                if (await reader.ReadAsync(cancellationToken)) throw new PlanningBudgetControlException("planning.budget_bucket_ambiguous");
                return bucketId;
            }
        }

        await using var strict = new NpgsqlCommand("select exists(select 1 from business_planning.budget_control_policy where tenant_id=$1 and legal_entity_id=$2 and active=true and mode in ('Block','RequireApproval') and (effective_from is null or effective_from <= $3) and (effective_to is null or effective_to >= $3))", context.Connection, context.Transaction);
        strict.Parameters.AddWithValue(tenantId.Value); strict.Parameters.AddWithValue(scope.LegalEntityId); strict.Parameters.AddWithValue(scope.PeriodStart);
        if (Convert.ToBoolean(await strict.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture))
            throw new PlanningBudgetControlException("planning.budget_bucket_required");
        return null;
    }

    private static DateOnly NormalizeControlPeriod(DateOnly sourcePeriod, string horizon) => horizon switch
    {
        "Annual" => new DateOnly(sourcePeriod.Year, 1, 1),
        "Quarterly" => new DateOnly(sourcePeriod.Year, ((sourcePeriod.Month - 1) / 3) * 3 + 1, 1),
        _ => new DateOnly(sourcePeriod.Year, sourcePeriod.Month, 1)
    };

    public async Task<bool> HasOpenReservationAsync(ITransactionContext context, TenantId tenantId, Guid legalEntityId, string sourceModule,
        string sourceDocumentId, string sourceLineId, CancellationToken cancellationToken = default)
    {
        var reservation = await ReadSourceReservationAsync(context, tenantId, sourceModule, sourceDocumentId, sourceLineId, cancellationToken);
        if (reservation is not null && reservation.LegalEntityId != legalEntityId) throw new PlanningBudgetControlException("planning.budget_scope_invalid");
        return reservation?.State == "Reserved";
    }

    public async Task<bool> HasReservationAsync(ITransactionContext context, TenantId tenantId, Guid legalEntityId, string sourceModule,
        string sourceDocumentId, string sourceLineId, CancellationToken cancellationToken = default)
    {
        var reservation = await ReadSourceReservationAsync(context, tenantId, sourceModule, sourceDocumentId, sourceLineId, cancellationToken);
        if (reservation is not null && reservation.LegalEntityId != legalEntityId) throw new PlanningBudgetControlException("planning.budget_scope_invalid");
        return reservation is not null;
    }

    public async Task<PlanningBudgetReservationResult> UpsertReservationAsync(ITransactionContext context, TenantId tenantId,
        Guid actorId, PlanningBudgetReservationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0m || string.IsNullOrWhiteSpace(request.SourceModule) || string.IsNullOrWhiteSpace(request.SourceDocumentId) || string.IsNullOrWhiteSpace(request.SourceLineId) || string.IsNullOrWhiteSpace(request.SourceVersion) || string.IsNullOrWhiteSpace(request.CurrencyCode) || request.CurrencyCode.Trim().Length != 3)
            throw new PlanningBudgetControlException("planning.budget_source_invalid");

        var existing = await ReadSourceReservationAsync(context, tenantId, request.SourceModule, request.SourceDocumentId, request.SourceLineId, cancellationToken);
        var bucketId = request.BucketId != Guid.Empty ? request.BucketId : existing?.BucketId ?? throw new PlanningBudgetControlException("planning.budget_reservation_not_found");
        var bucket = await ReadBucketAsync(context, tenantId, bucketId, request.ControlPeriodStart ?? DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        if (bucket.LegalEntityId != request.LegalEntityId) throw new PlanningBudgetControlException("planning.budget_scope_invalid");
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(bucket.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase) ||
            (existing?.SourceCurrency is not null && !string.Equals(existing.SourceCurrency, currency, StringComparison.OrdinalIgnoreCase)))
            throw new PlanningBudgetControlException("planning.budget_currency_mismatch");
        var manualReserved = await ScalarAsync<decimal>(context,
            "select coalesce(sum(amount),0) from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$3 and state='Reserved' and reservation_kind='Manual' and id <> coalesce($2::uuid,'00000000-0000-0000-0000-000000000000')",
            cancellationToken, tenantId.Value, existing?.Id, bucketId);
        var precommitmentReserved = await ScalarAsync<decimal>(context,
            "select coalesce(sum(amount),0) from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$3 and state='Reserved' and reservation_kind='Precommitment' and id <> coalesce($2::uuid,'00000000-0000-0000-0000-000000000000')",
            cancellationToken, tenantId.Value, existing?.Id, bucketId);
        var commitmentReserved = await ScalarAsync<decimal>(context,
            "select coalesce(sum(amount),0) from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$3 and state='Reserved' and reservation_kind='Commitment' and id <> coalesce($2::uuid,'00000000-0000-0000-0000-000000000000')",
            cancellationToken, tenantId.Value, existing?.Id, bucketId);
        // A successful owner reconciliation materializes authoritative source
        // totals into the bucket projection; do not count those reservations twice.
        var ownerReconciled = await ScalarAsync<bool>(context,
            "select exists(select 1 from business_planning.budget_control_owner_reconciliation where tenant_id=$1 and bucket_id=$2 and state='Succeeded' and discrepancy_amount=0)",
            cancellationToken, tenantId.Value, bucketId);
        if (ownerReconciled)
        {
            precommitmentReserved = 0m;
            commitmentReserved = 0m;
        }
        var before = bucket.Basis switch
        {
            "Precommitment" => bucket.CurrentBudget - manualReserved - bucket.Precommitment - precommitmentReserved,
            "Commitment" => bucket.CurrentBudget - manualReserved - bucket.Commitment - commitmentReserved,
            "Actual" => bucket.CurrentBudget - manualReserved - bucket.Actual,
            "ActualAndCommitment" => bucket.CurrentBudget - manualReserved - bucket.Actual - bucket.Commitment - commitmentReserved,
            "ActualPrecommitmentCommitment" => bucket.CurrentBudget - manualReserved - bucket.Actual - bucket.Precommitment - bucket.Commitment - precommitmentReserved - commitmentReserved,
            "ActualOpenCommitmentProposed" => bucket.CurrentBudget - manualReserved - bucket.Actual - bucket.Commitment - commitmentReserved,
            _ => bucket.CurrentBudget - manualReserved - bucket.Actual - bucket.Commitment - bucket.Precommitment - precommitmentReserved - commitmentReserved
        };
        var after = before - request.Amount;
        var thresholdExceeded = bucket.CurrentBudget <= 0m || (bucket.CurrentBudget - after) / Math.Max(bucket.CurrentBudget, 1m) * 100m > bucket.ThresholdPercent;
        var decision = (after < 0m && !bucket.NegativeBudgetAllowed) || thresholdExceeded ? bucket.Mode switch
        {
            "Block" => "Block",
            "RequireApproval" => "RequireApproval",
            "Warn" => "Warn",
            _ => "Allow"
        } : "Allow";
        if (bucket.ShadowMode && decision is not "Allow") decision = "Warn";
        if (decision is "Block" or "RequireApproval")
        {
            var overrideId = await ScalarAsync<Guid?>(context,
                "select id from business_planning.budget_override where tenant_id=$1 and bucket_id=$2 and source_module=$3 and source_document_id=$4 and source_line_id=$5 and source_version=$6 and currency_code=$7 and state='Approved' and used_amount+$8 <= amount and (valid_until is null or valid_until > now()) order by created_at,id limit 1 for update",
                cancellationToken, tenantId.Value, bucketId, request.SourceModule.Trim(), request.SourceDocumentId.Trim(), request.SourceLineId.Trim(), request.SourceVersion.Trim(), currency, request.Amount);
            if (overrideId is { } approvedOverride)
            {
                await ExecuteAsync(context, "update business_planning.budget_override set used_amount=used_amount+$3 where tenant_id=$1 and id=$2 and state='Approved' and used_amount+$3 <= amount", cancellationToken, tenantId.Value, approvedOverride, request.Amount);
                await AuditAsync(context, tenantId, actorId, "budget_override_used", approvedOverride, cancellationToken);
                decision = "Allow";
            }
        }
        if (decision == "Block") throw new PlanningBudgetControlException("planning.budget_insufficient");

        var reservationId = existing?.Id ?? Guid.CreateVersion7();
        var kind = request.Kind == PlanningBudgetReservationKind.Commitment ? "Commitment" : "Precommitment";
        if (existing is null)
        {
            await ExecuteAsync(context, "insert into business_planning.budget_reservation(tenant_id,id,bucket_id,source_module,source_document_id,source_line_id,source_version,amount,reservation_kind,source_currency) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", cancellationToken,
                tenantId.Value, reservationId, bucketId, request.SourceModule.Trim(), request.SourceDocumentId.Trim(), request.SourceLineId.Trim(), request.SourceVersion.Trim(), request.Amount, kind, currency);
            if (ownerReconciled) await AdjustMaterializedReservationTotalsAsync(context, tenantId, bucketId, kind, request.Amount, cancellationToken);
        }
        else
        {
            if (existing.State == "Reserved" && await IsOwnerReconciledAsync(context, tenantId, existing.BucketId, cancellationToken))
                await AdjustMaterializedReservationTotalsAsync(context, tenantId, existing.BucketId, existing.Kind, -existing.Amount, cancellationToken);
            await ExecuteAsync(context, "update business_planning.budget_reservation set bucket_id=$3,source_version=$4,amount=$5,reservation_kind=$6,source_currency=$7,state='Reserved',released_at=null where tenant_id=$1 and id=$2", cancellationToken,
                tenantId.Value, reservationId, bucketId, request.SourceVersion.Trim(), request.Amount, kind, currency);
            if (ownerReconciled) await AdjustMaterializedReservationTotalsAsync(context, tenantId, bucketId, kind, request.Amount, cancellationToken);
        }
        await AuditAsync(context, tenantId, actorId, "budget_reservation_upserted", reservationId, cancellationToken);
        return new(reservationId, decision, after);
    }

    public async Task ReleaseReservationAsync(ITransactionContext context, TenantId tenantId, string sourceModule,
        string sourceDocumentId, string sourceLineId, CancellationToken cancellationToken = default)
    {
        decimal amount; Guid bucketId; string kind;
        await using (var command = new NpgsqlCommand("select amount,bucket_id,reservation_kind from business_planning.budget_reservation where tenant_id=$1 and source_module=$2 and source_document_id=$3 and source_line_id=$4 and state='Reserved' order by created_at desc,id desc limit 1 for update", context.Connection, context.Transaction))
        {
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(sourceModule.Trim()); command.Parameters.AddWithValue(sourceDocumentId.Trim()); command.Parameters.AddWithValue(sourceLineId.Trim());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return;
            amount = reader.GetDecimal(0); bucketId = reader.GetGuid(1); kind = reader.GetString(2);
        }
        await ExecuteAsync(context, "update business_planning.budget_reservation set state='Released',released_at=now() where tenant_id=$1 and source_module=$2 and source_document_id=$3 and source_line_id=$4 and state='Reserved'", cancellationToken,
            tenantId.Value, sourceModule.Trim(), sourceDocumentId.Trim(), sourceLineId.Trim());
        if (await IsOwnerReconciledAsync(context, tenantId, bucketId, cancellationToken))
            await AdjustMaterializedReservationTotalsAsync(context, tenantId, bucketId, kind, -amount, cancellationToken);
    }

    public async Task<PlanningBudgetReservationResult> ConsumeReservationAsync(ITransactionContext context, TenantId tenantId,
        Guid actorId, PlanningBudgetConsumptionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0m || string.IsNullOrWhiteSpace(request.CurrencyCode) || request.CurrencyCode.Trim().Length != 3) throw new PlanningBudgetControlException("planning.amount_invalid");
        var reservation = await ReadSourceReservationAsync(context, tenantId, request.SourceModule, request.SourceDocumentId, request.SourceLineId, cancellationToken)
            ?? throw new PlanningBudgetControlException("planning.budget_reservation_not_found");
        if (reservation.LegalEntityId != request.LegalEntityId) throw new PlanningBudgetControlException("planning.budget_scope_invalid");
        if (reservation.SourceCurrency is not null && !string.Equals(reservation.SourceCurrency, request.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new PlanningBudgetControlException("planning.budget_currency_mismatch");
        if (request.Amount > reservation.Amount) throw new PlanningBudgetControlException("planning.budget_consumption_exceeds_reservation");
        var existingConsumption = await ReadConsumptionAsync(context, tenantId, request, cancellationToken);
        if (existingConsumption is not null)
        {
            if (existingConsumption.Value != request.Amount) throw new PlanningBudgetControlException("planning.budget_consumption_idempotency_conflict");
            return new(reservation.Id, reservation.State == "Consumed" ? "Consumed" : "PartiallyConsumed", reservation.State == "Reserved" ? reservation.Amount : 0m);
        }
        if (reservation.State != "Reserved") throw new PlanningBudgetControlException("planning.budget_reservation_not_open");
        await ExecuteAsync(context, "insert into business_planning.budget_consumption_fact(tenant_id,id,bucket_id,source_module,source_document_id,source_line_id,source_version,amount,state,source_currency) values($1,$2,$3,$4,$5,$6,$7,$8,'Actual',$9) on conflict(tenant_id,source_module,source_document_id,source_line_id,source_version) do update set amount=excluded.amount,state='Actual',source_currency=excluded.source_currency", cancellationToken,
            tenantId.Value, Guid.CreateVersion7(), reservation.BucketId, request.SourceModule.Trim(), request.SourceDocumentId.Trim(), request.SourceLineId.Trim(), request.SourceVersion.Trim(), request.Amount, request.CurrencyCode.Trim().ToUpperInvariant());
        await ExecuteAsync(context, "update business_planning.budget_control_bucket set actual_total=actual_total+$3,row_version=row_version+1 where tenant_id=$1 and id=$2", cancellationToken, tenantId.Value, reservation.BucketId, request.Amount);
        if (request.Amount == reservation.Amount)
            await ExecuteAsync(context, "update business_planning.budget_reservation set state='Consumed',released_at=null where tenant_id=$1 and id=$2 and state='Reserved'", cancellationToken, tenantId.Value, reservation.Id);
        else
            await ExecuteAsync(context, "update business_planning.budget_reservation set amount=amount-$3,source_version=$4,state='Reserved',released_at=null where tenant_id=$1 and id=$2 and state='Reserved'", cancellationToken, tenantId.Value, reservation.Id, request.Amount, request.SourceVersion.Trim());
        if (await IsOwnerReconciledAsync(context, tenantId, reservation.BucketId, cancellationToken))
            await AdjustMaterializedReservationTotalsAsync(context, tenantId, reservation.BucketId, reservation.Kind, -request.Amount, cancellationToken);
        await AuditAsync(context, tenantId, actorId, "budget_reservation_consumed", reservation.Id, cancellationToken);
        return new(reservation.Id, request.Amount == reservation.Amount ? "Consumed" : "PartiallyConsumed", Math.Max(0m, reservation.Amount - request.Amount));
    }

    public async Task<bool> ReverseConsumptionAsync(ITransactionContext context, TenantId tenantId, Guid actorId,
        PlanningBudgetCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0m || string.IsNullOrWhiteSpace(request.SourceModule) || string.IsNullOrWhiteSpace(request.SourceDocumentId)
            || string.IsNullOrWhiteSpace(request.SourceLineId) || string.IsNullOrWhiteSpace(request.CorrectionSourceModule)
            || string.IsNullOrWhiteSpace(request.CorrectionSourceDocumentId) || string.IsNullOrWhiteSpace(request.CorrectionSourceLineId)
            || string.IsNullOrWhiteSpace(request.CorrectionSourceVersion) || string.IsNullOrWhiteSpace(request.CurrencyCode)
            || request.CurrencyCode.Trim().Length != 3)
            throw new PlanningBudgetControlException("planning.budget_correction_invalid");

        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var correctionPrefix = request.CorrectionSourceVersion.Trim() + ":";
        var alreadyApplied = await ScalarAsync<decimal>(context,
            "select coalesce(sum(amount),0) from business_planning.budget_consumption_fact where tenant_id=$1 and source_module=$2 and source_document_id=$3 and source_line_id=$4 and source_version like $5 and state='Reversed'",
            cancellationToken, tenantId.Value, request.CorrectionSourceModule.Trim(), request.CorrectionSourceDocumentId.Trim(), request.CorrectionSourceLineId.Trim(), correctionPrefix + "%");
        if (alreadyApplied > request.Amount) throw new PlanningBudgetControlException("planning.budget_correction_idempotency_conflict");
        if (alreadyApplied == request.Amount) return true;
        var remainingToReverse = request.Amount - alreadyApplied;

        var originals = new List<ConsumptionSource>();
        await using (var command = new NpgsqlCommand("select id,bucket_id,amount,source_version,source_currency from business_planning.budget_consumption_fact where tenant_id=$1 and source_module=$2 and source_document_id=$3 and source_line_id=$4 and state='Actual' order by created_at,id for update", context.Connection, context.Transaction))
        {
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(request.SourceModule.Trim()); command.Parameters.AddWithValue(request.SourceDocumentId.Trim()); command.Parameters.AddWithValue(request.SourceLineId.Trim());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) originals.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4).Trim()));
        }
        if (originals.Count == 0) return false;

        var appliedNow = 0m;
        var correctionSequence = 0;
        foreach (var original in originals)
        {
            var corrected = await ScalarAsync<decimal>(context,
                "select coalesce(sum(amount),0) from business_planning.budget_consumption_fact where tenant_id=$1 and correction_of_id=$2 and state='Reversed'",
                cancellationToken, tenantId.Value, original.Id);
            var available = original.Amount - corrected;
            if (available <= 0m) continue;
            if (original.SourceCurrency is not null && !string.Equals(original.SourceCurrency, currency, StringComparison.OrdinalIgnoreCase))
                throw new PlanningBudgetControlException("planning.budget_currency_mismatch");
            var amount = Math.Min(available, remainingToReverse);
            var correctionVersion = $"{correctionPrefix}{original.Id:D}:{correctionSequence++}";
            await ExecuteAsync(context, "insert into business_planning.budget_consumption_fact(tenant_id,id,bucket_id,source_module,source_document_id,source_line_id,source_version,amount,state,correction_of_id,source_currency) values($1,$2,$3,$4,$5,$6,$7,$8,'Reversed',$9,$10)", cancellationToken,
                tenantId.Value, Guid.CreateVersion7(), original.BucketId, request.CorrectionSourceModule.Trim(), request.CorrectionSourceDocumentId.Trim(), request.CorrectionSourceLineId.Trim(), correctionVersion, amount, original.Id, currency);
            var changed = await ScalarAsync<long>(context, "update business_planning.budget_control_bucket set actual_total=actual_total-$3,row_version=row_version+1 where tenant_id=$1 and id=$2 and actual_total >= $3 returning 1", cancellationToken, tenantId.Value, original.BucketId, amount);
            if (changed != 1) throw new PlanningBudgetControlException("planning.budget_correction_exceeds_actual");
            appliedNow += amount;
            remainingToReverse -= amount;
            if (remainingToReverse <= 0m) break;
        }
        if (remainingToReverse > 0m) throw new PlanningBudgetControlException("planning.budget_correction_exceeds_actual");
        await AuditAsync(context, tenantId, actorId, "budget_consumption_reversed", Guid.CreateVersion7(), cancellationToken);
        return appliedNow > 0m;
    }

    private static async Task<Bucket> ReadBucketAsync(ITransactionContext context, TenantId tenantId, Guid bucketId, DateOnly controlPeriodStart, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select b.legal_entity_id,b.original_budget+b.revision_total+coalesce((select sum(t.amount) from business_planning.budget_transfer t where t.tenant_id=b.tenant_id and t.to_bucket_id=b.id and t.state='Approved' and t.effective_from <= $3),0)-coalesce((select sum(t.amount) from business_planning.budget_transfer t where t.tenant_id=b.tenant_id and t.from_bucket_id=b.id and t.state='Approved' and t.effective_from <= $3),0),b.actual_total,b.commitment_total,b.precommitment_total,p.mode,p.threshold_percent,b.currency_code,p.basis,p.control_horizon,p.negative_budget_allowed,p.opening_reconciled,p.strict_activation_at from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id join business_planning.plan_version v on v.tenant_id=b.tenant_id and v.id=b.plan_version_id where b.tenant_id=$1 and b.id=$2 and v.state='Published' and p.active=true and (p.effective_from is null or p.effective_from <= $3) and (p.effective_to is null or p.effective_to >= $3) for update", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(bucketId); command.Parameters.AddWithValue(controlPeriodStart);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new PlanningBudgetControlException("planning.bucket_not_found");
        var legalEntityId = reader.GetGuid(0); var currentBudget = reader.GetDecimal(1); var actual = reader.GetDecimal(2); var commitment = reader.GetDecimal(3); var precommitment = reader.GetDecimal(4); var mode = reader.GetString(5); var threshold = reader.GetDecimal(6); var currencyCode = reader.GetString(7).Trim(); var basis = reader.GetString(8); var horizon = reader.GetString(9); var negativeBudgetAllowed = reader.GetBoolean(10); var openingReconciled = reader.GetBoolean(11); var strictActivationAt = reader.IsDBNull(12) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(12);
        await reader.CloseAsync();
        var ownerReconciled = await ScalarAsync<bool>(context, "select exists(select 1 from business_planning.budget_control_owner_reconciliation where tenant_id=$1 and bucket_id=$2 and state='Succeeded' and discrepancy_amount=0)", ct, tenantId.Value, bucketId);
        if (string.Equals(mode, "Block", StringComparison.OrdinalIgnoreCase) && (!openingReconciled || !ownerReconciled || strictActivationAt is { } activationAt && activationAt > DateTimeOffset.UtcNow))
            throw new PlanningBudgetControlException("planning.budget_opening_reconciliation_required");
        var shadowMode = await ScalarAsync<bool>(context, "select shadow_mode from business_planning.budget_control_policy p join business_planning.budget_control_bucket b on b.tenant_id=p.tenant_id and b.policy_id=p.id where p.tenant_id=$1 and b.id=$2", ct, tenantId.Value, bucketId);
        return new(legalEntityId, currentBudget, actual, commitment, precommitment, mode, threshold, currencyCode, basis, horizon, negativeBudgetAllowed, shadowMode);
    }

    private static async Task<Reservation?> ReadSourceReservationAsync(ITransactionContext context, TenantId tenantId, string module, string document, string line, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select r.id,r.bucket_id,b.legal_entity_id,r.amount,r.state,r.source_currency,r.reservation_kind from business_planning.budget_reservation r join business_planning.budget_control_bucket b on b.tenant_id=r.tenant_id and b.id=r.bucket_id where r.tenant_id=$1 and r.source_module=$2 and r.source_document_id=$3 and r.source_line_id=$4 order by r.created_at desc,r.id desc limit 1 for update", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(module.Trim()); command.Parameters.AddWithValue(document.Trim()); command.Parameters.AddWithValue(line.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5).Trim(), reader.GetString(6)) : null;
    }

    private static async Task<decimal?> ReadConsumptionAsync(ITransactionContext context, TenantId tenantId, PlanningBudgetConsumptionRequest request, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select amount from business_planning.budget_consumption_fact where tenant_id=$1 and source_module=$2 and source_document_id=$3 and source_line_id=$4 and source_version=$5 for update", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(request.SourceModule.Trim()); command.Parameters.AddWithValue(request.SourceDocumentId.Trim()); command.Parameters.AddWithValue(request.SourceLineId.Trim()); command.Parameters.AddWithValue(request.SourceVersion.Trim());
        var value = await command.ExecuteScalarAsync(ct);
        return value is decimal amount ? amount : null;
    }

    private static bool IsObject(string json)
    {
        try { using var document = JsonDocument.Parse(json); return document.RootElement.ValueKind == JsonValueKind.Object; }
        catch (JsonException) { return false; }
    }

    private static bool JsonContains(string scopeJson, string bucketJson)
    {
        try
        {
            using var scopeDocument = JsonDocument.Parse(scopeJson); using var bucketDocument = JsonDocument.Parse(bucketJson);
            return Contains(scopeDocument.RootElement, bucketDocument.RootElement);
        }
        catch (JsonException) { return false; }
    }

    private static bool Contains(JsonElement scope, JsonElement bucket)
    {
        if (bucket.ValueKind != JsonValueKind.Object || scope.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in bucket.EnumerateObject())
        {
            if (!scope.TryGetProperty(property.Name, out var scopeValue) || scopeValue.GetRawText() != property.Value.GetRawText()) return false;
        }
        return true;
    }

    private static Task<bool> IsOwnerReconciledAsync(ITransactionContext context, TenantId tenant, Guid bucketId, CancellationToken ct) => ScalarAsync<bool>(context,
        "select exists(select 1 from business_planning.budget_control_owner_reconciliation where tenant_id=$1 and bucket_id=$2 and state='Succeeded' and discrepancy_amount=0)", ct, tenant.Value, bucketId);
    private static async Task AdjustMaterializedReservationTotalsAsync(ITransactionContext context, TenantId tenant, Guid bucketId, string kind, decimal delta, CancellationToken ct)
    {
        var changed = await ScalarAsync<int?>(context,
            "update business_planning.budget_control_bucket set commitment_total=commitment_total+case when $3='Commitment' then $4 else 0 end,precommitment_total=precommitment_total+case when $3='Precommitment' then $4 else 0 end,row_version=row_version+1 where tenant_id=$1 and id=$2 and commitment_total+case when $3='Commitment' then $4 else 0 end >= 0 and precommitment_total+case when $3='Precommitment' then $4 else 0 end >= 0 returning 1",
            ct, tenant.Value, bucketId, kind, delta);
        if (changed is null) throw new PlanningBudgetControlException("planning.budget_projection_inconsistent");
    }
    private static async Task<T> ScalarAsync<T>(ITransactionContext context, string sql, CancellationToken ct, params object?[] values)
    {
        await using var command = Command(context, sql, values); var value = await command.ExecuteScalarAsync(ct);
        if (value is null or DBNull) return default!;
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (target == typeof(Guid) && value is Guid guid) return (T)(object)guid;
        return (T)Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
    }
    private static async Task ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = Command(context, sql, values); await command.ExecuteNonQueryAsync(ct); }
    private static NpgsqlCommand Command(ITransactionContext context, string sql, params object?[] values) { var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); return command; }
    private static Task AuditAsync(ITransactionContext context, TenantId tenant, Guid actor, string action, Guid entity, CancellationToken ct) => ExecuteAsync(context, "insert into business_planning.planning_audit_event(tenant_id,id,actor_user_id,action,entity_id) values($1,$2,$3,$4,$5)", ct, tenant.Value, Guid.CreateVersion7(), actor, action, entity);
    private sealed record Bucket(Guid LegalEntityId, decimal CurrentBudget, decimal Actual, decimal Commitment, decimal Precommitment, string Mode, decimal ThresholdPercent, string CurrencyCode, string Basis, string ControlHorizon, bool NegativeBudgetAllowed, bool ShadowMode);
    private sealed record Reservation(Guid Id, Guid BucketId, Guid LegalEntityId, decimal Amount, string State, string? SourceCurrency, string Kind);
    private sealed record ConsumptionSource(Guid Id, Guid BucketId, decimal Amount, string SourceVersion, string? SourceCurrency);
}
