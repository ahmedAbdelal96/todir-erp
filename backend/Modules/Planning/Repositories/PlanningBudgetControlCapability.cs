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
            await using var command = new NpgsqlCommand("select b.id,b.legal_entity_id,b.period_start,b.metric_code,b.dimension_key::text,p.active,p.mode,(p.effective_from is null or p.effective_from <= $3) and (p.effective_to is null or p.effective_to >= $3) from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where b.tenant_id=$1 and b.id=$2 for update", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(requestedBucketId); command.Parameters.AddWithValue(scope.PeriodStart);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new PlanningBudgetControlException("planning.bucket_not_found");
            var bucketId = reader.GetGuid(0);
            if (reader.GetGuid(1) != scope.LegalEntityId) throw new PlanningBudgetControlException("planning.budget_scope_invalid");
            if (reader.GetFieldValue<DateOnly>(2) != scope.PeriodStart || !string.Equals(reader.GetString(3), scope.MetricCode.Trim(), StringComparison.OrdinalIgnoreCase) || !JsonContains(scope.DimensionKeyJson, reader.GetString(4)))
                throw new PlanningBudgetControlException("planning.budget_bucket_context_invalid");
            if (!reader.GetBoolean(5) || !reader.GetBoolean(7)) throw new PlanningBudgetControlException("planning.budget_policy_inactive");
            if (string.Equals(reader.GetString(6), "None", StringComparison.OrdinalIgnoreCase)) throw new PlanningBudgetControlException("planning.budget_bucket_uncontrolled");
            return bucketId;
        }

        await using (var command = new NpgsqlCommand("select b.id,p.mode from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where b.tenant_id=$1 and b.legal_entity_id=$2 and b.period_start=$3 and lower(b.metric_code)=lower($4) and $5::jsonb @> b.dimension_key and p.active=true and p.mode <> 'None' and (p.effective_from is null or p.effective_from <= $3) and (p.effective_to is null or p.effective_to >= $3) order by length(b.dimension_key::text) desc,b.id limit 2", context.Connection, context.Transaction))
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

    public async Task<bool> HasOpenReservationAsync(ITransactionContext context, TenantId tenantId, Guid legalEntityId, string sourceModule,
        string sourceDocumentId, string sourceLineId, CancellationToken cancellationToken = default)
    {
        var reservation = await ReadSourceReservationAsync(context, tenantId, sourceModule, sourceDocumentId, sourceLineId, cancellationToken);
        if (reservation is not null && reservation.LegalEntityId != legalEntityId) throw new PlanningBudgetControlException("planning.budget_scope_invalid");
        return reservation?.State == "Reserved";
    }

    public async Task<PlanningBudgetReservationResult> UpsertReservationAsync(ITransactionContext context, TenantId tenantId,
        Guid actorId, PlanningBudgetReservationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0m || string.IsNullOrWhiteSpace(request.SourceModule) || string.IsNullOrWhiteSpace(request.SourceDocumentId) || string.IsNullOrWhiteSpace(request.SourceLineId) || string.IsNullOrWhiteSpace(request.SourceVersion) || string.IsNullOrWhiteSpace(request.CurrencyCode) || request.CurrencyCode.Trim().Length != 3)
            throw new PlanningBudgetControlException("planning.budget_source_invalid");

        var existing = await ReadSourceReservationAsync(context, tenantId, request.SourceModule, request.SourceDocumentId, request.SourceLineId, cancellationToken);
        var bucketId = request.BucketId != Guid.Empty ? request.BucketId : existing?.BucketId ?? throw new PlanningBudgetControlException("planning.budget_reservation_not_found");
        var bucket = await ReadBucketAsync(context, tenantId, bucketId, cancellationToken);
        if (bucket.LegalEntityId != request.LegalEntityId) throw new PlanningBudgetControlException("planning.budget_scope_invalid");
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(bucket.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase) ||
            (existing?.SourceCurrency is not null && !string.Equals(existing.SourceCurrency, currency, StringComparison.OrdinalIgnoreCase)))
            throw new PlanningBudgetControlException("planning.budget_currency_mismatch");
        var reservedExcludingSource = await ScalarAsync<decimal>(context,
            "select coalesce(sum(amount) filter (where reservation_kind='Manual'),0) + coalesce(sum(amount) filter (where reservation_kind in ('Precommitment','Commitment') and id <> coalesce($2::uuid,'00000000-0000-0000-0000-000000000000')),0) from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$3 and state='Reserved'",
            cancellationToken, tenantId.Value, existing?.Id, bucketId);
        var before = bucket.CurrentBudget - bucket.Actual - bucket.Commitment - bucket.Precommitment - reservedExcludingSource;
        var after = before - request.Amount;
        var thresholdExceeded = bucket.CurrentBudget <= 0m || (bucket.CurrentBudget - after) / Math.Max(bucket.CurrentBudget, 1m) * 100m > bucket.ThresholdPercent;
        var decision = after < 0m || thresholdExceeded ? bucket.Mode switch
        {
            "Block" => "Block",
            "RequireApproval" => "RequireApproval",
            "Warn" => "Warn",
            _ => "Allow"
        } : "Allow";
        if (decision == "Block") throw new PlanningBudgetControlException("planning.budget_insufficient");

        var reservationId = existing?.Id ?? Guid.CreateVersion7();
        var kind = request.Kind == PlanningBudgetReservationKind.Commitment ? "Commitment" : "Precommitment";
        if (existing is null)
        {
            await ExecuteAsync(context, "insert into business_planning.budget_reservation(tenant_id,id,bucket_id,source_module,source_document_id,source_line_id,source_version,amount,reservation_kind,source_currency) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", cancellationToken,
                tenantId.Value, reservationId, bucketId, request.SourceModule.Trim(), request.SourceDocumentId.Trim(), request.SourceLineId.Trim(), request.SourceVersion.Trim(), request.Amount, kind, currency);
        }
        else
        {
            await ExecuteAsync(context, "update business_planning.budget_reservation set bucket_id=$3,source_version=$4,amount=$5,reservation_kind=$6,source_currency=$7,state='Reserved',released_at=null where tenant_id=$1 and id=$2", cancellationToken,
                tenantId.Value, reservationId, bucketId, request.SourceVersion.Trim(), request.Amount, kind, currency);
        }
        await AuditAsync(context, tenantId, actorId, "budget_reservation_upserted", reservationId, cancellationToken);
        return new(reservationId, decision, after);
    }

    public async Task ReleaseReservationAsync(ITransactionContext context, TenantId tenantId, string sourceModule,
        string sourceDocumentId, string sourceLineId, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(context, "update business_planning.budget_reservation set state='Released',released_at=now() where tenant_id=$1 and source_module=$2 and source_document_id=$3 and source_line_id=$4 and state='Reserved'", cancellationToken,
            tenantId.Value, sourceModule.Trim(), sourceDocumentId.Trim(), sourceLineId.Trim());
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
        await ExecuteAsync(context, "insert into business_planning.budget_consumption_fact(tenant_id,id,bucket_id,source_module,source_document_id,source_line_id,source_version,amount,state) values($1,$2,$3,$4,$5,$6,$7,$8,'Actual') on conflict(tenant_id,source_module,source_document_id,source_line_id,source_version) do update set amount=excluded.amount,state='Actual'", cancellationToken,
            tenantId.Value, Guid.CreateVersion7(), reservation.BucketId, request.SourceModule.Trim(), request.SourceDocumentId.Trim(), request.SourceLineId.Trim(), request.SourceVersion.Trim(), request.Amount);
        await ExecuteAsync(context, "update business_planning.budget_control_bucket set actual_total=actual_total+$3,row_version=row_version+1 where tenant_id=$1 and id=$2", cancellationToken, tenantId.Value, reservation.BucketId, request.Amount);
        if (request.Amount == reservation.Amount)
            await ExecuteAsync(context, "update business_planning.budget_reservation set state='Consumed',released_at=null where tenant_id=$1 and id=$2 and state='Reserved'", cancellationToken, tenantId.Value, reservation.Id);
        else
            await ExecuteAsync(context, "update business_planning.budget_reservation set amount=amount-$3,source_version=$4,state='Reserved',released_at=null where tenant_id=$1 and id=$2 and state='Reserved'", cancellationToken, tenantId.Value, reservation.Id, request.Amount, request.SourceVersion.Trim());
        await AuditAsync(context, tenantId, actorId, "budget_reservation_consumed", reservation.Id, cancellationToken);
        return new(reservation.Id, request.Amount == reservation.Amount ? "Consumed" : "PartiallyConsumed", Math.Max(0m, reservation.Amount - request.Amount));
    }

    private static async Task<Bucket> ReadBucketAsync(ITransactionContext context, TenantId tenantId, Guid bucketId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select b.legal_entity_id,b.original_budget+b.revision_total+b.transfer_in-b.transfer_out,b.actual_total,b.commitment_total,b.precommitment_total,p.mode,p.threshold_percent,b.currency_code from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where b.tenant_id=$1 and b.id=$2 and p.active=true and (p.effective_from is null or p.effective_from <= current_date) and (p.effective_to is null or p.effective_to >= current_date) for update", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(bucketId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new PlanningBudgetControlException("planning.bucket_not_found");
        return new(reader.GetGuid(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetString(5), reader.GetDecimal(6), reader.GetString(7).Trim());
    }

    private static async Task<Reservation?> ReadSourceReservationAsync(ITransactionContext context, TenantId tenantId, string module, string document, string line, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select r.id,r.bucket_id,b.legal_entity_id,r.amount,r.state,r.source_currency from business_planning.budget_reservation r join business_planning.budget_control_bucket b on b.tenant_id=r.tenant_id and b.id=r.bucket_id where r.tenant_id=$1 and r.source_module=$2 and r.source_document_id=$3 and r.source_line_id=$4 order by r.created_at desc,r.id desc limit 1 for update", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(module.Trim()); command.Parameters.AddWithValue(document.Trim()); command.Parameters.AddWithValue(line.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5).Trim()) : null;
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

    private static async Task<T> ScalarAsync<T>(ITransactionContext context, string sql, CancellationToken ct, params object?[] values)
    {
        await using var command = Command(context, sql, values); var value = await command.ExecuteScalarAsync(ct);
        return (T)Convert.ChangeType(value ?? 0m, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
    private static async Task ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = Command(context, sql, values); await command.ExecuteNonQueryAsync(ct); }
    private static NpgsqlCommand Command(ITransactionContext context, string sql, params object?[] values) { var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); return command; }
    private static Task AuditAsync(ITransactionContext context, TenantId tenant, Guid actor, string action, Guid entity, CancellationToken ct) => ExecuteAsync(context, "insert into business_planning.planning_audit_event(tenant_id,id,actor_user_id,action,entity_id) values($1,$2,$3,$4,$5)", ct, tenant.Value, Guid.CreateVersion7(), actor, action, entity);
    private sealed record Bucket(Guid LegalEntityId, decimal CurrentBudget, decimal Actual, decimal Commitment, decimal Precommitment, string Mode, decimal ThresholdPercent, string CurrencyCode);
    private sealed record Reservation(Guid Id, Guid BucketId, Guid LegalEntityId, decimal Amount, string State, string? SourceCurrency);
}
