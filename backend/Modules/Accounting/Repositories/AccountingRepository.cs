using System.Text.Json;
using Modules.Accounting.DTOs;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Accounting.Repositories;

public sealed class AccountingRepository(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<ReconciliationView> SaveReconciliationAsync(TenantId tenant, Guid entity, string type, DateOnly asOf, decimal? source, decimal? ledger, decimal? difference, string? currency, string status, IReadOnlyList<string> diagnostics, Guid actor, Guid correlation, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("insert into business_accounting.reconciliation_run(tenant_id,legal_entity_id,id,reconciliation_type,as_of_date,source_balance,ledger_balance,difference,currency_code,status,source_version,diagnostics,created_by,correlation_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'live',$11::jsonb,$12,$13,$14)", connection);
        Add(command, tenant.Value, entity, id, type, asOf, source, ledger, difference, currency, status, JsonSerializer.Serialize(diagnostics), actor, correlation, now); await command.ExecuteNonQueryAsync(ct);
        return new(id, type, asOf, source, ledger, difference, currency, status, diagnostics, now);
    }

    public async Task<CloseRunView> SaveCloseRunAsync(TenantId tenant, Guid entity, Guid periodId, string idempotencyKey, string payloadHash, string status, string? reason, Guid actor, IReadOnlyList<CloseCheckView> checks, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct); await using var transaction = await connection.BeginTransactionAsync(ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using (var command = new NpgsqlCommand("insert into business_accounting.close_run(tenant_id,legal_entity_id,id,period_id,idempotency_key,payload_hash,status,requested_by,reason,completed_at,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,case when $7='Closed' then $10 else null end,$10,$10) on conflict(tenant_id,legal_entity_id,period_id,idempotency_key) do update set status=excluded.status,reason=excluded.reason,completed_at=case when excluded.status='Closed' then excluded.updated_at else business_accounting.close_run.completed_at end,updated_at=excluded.updated_at,row_version=business_accounting.close_run.row_version+1 where business_accounting.close_run.payload_hash=excluded.payload_hash returning id,created_at", connection, transaction))
        {
            Add(command, tenant.Value, entity, id, periodId, idempotencyKey, payloadHash, status, actor, reason, now);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) { id = reader.GetGuid(0); now = reader.GetFieldValue<DateTimeOffset>(1); }
            else
            {
                await reader.DisposeAsync();
                await using var existing = new NpgsqlCommand("select payload_hash from business_accounting.close_run where tenant_id=$1 and legal_entity_id=$2 and period_id=$3 and idempotency_key=$4", connection, transaction);
                Add(existing, tenant.Value, entity, periodId, idempotencyKey);
                var persistedHash = (string?)await existing.ExecuteScalarAsync(ct);
                if (persistedHash is not null && !string.Equals(persistedHash, payloadHash, StringComparison.Ordinal))
                    throw new AccountingRepositoryException("accounting.idempotency_conflict");
                throw new AccountingRepositoryException("accounting.close_run_retry_required");
            }
        }
        await using (var delete = new NpgsqlCommand("delete from business_accounting.close_check where tenant_id=$1 and close_run_id=$2", connection, transaction)) { Add(delete, tenant.Value, id); await delete.ExecuteNonQueryAsync(ct); }
        foreach (var check in checks)
        { await using var command = new NpgsqlCommand("insert into business_accounting.close_check(tenant_id,close_run_id,id,check_code,severity,status,details,created_at) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8)", connection, transaction); Add(command, tenant.Value, id, Guid.CreateVersion7(), check.Code, check.Severity, check.Status, JsonSerializer.Serialize(new { check.Message }), now); await command.ExecuteNonQueryAsync(ct); }
        await transaction.CommitAsync(ct); return new(id, periodId, status, checks, now);
    }

    private async Task<NpgsqlConnection> OpenAsync(TenantId tenant, CancellationToken ct)
    { var connection = await dataSource.OpenConnectionAsync(ct); await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); return connection; }
    private static void Add(NpgsqlCommand command, params object?[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }
}

public sealed class AccountingRepositoryException(string code) : Exception(code) { public string Code { get; } = code; }
