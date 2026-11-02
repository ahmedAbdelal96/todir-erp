using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrGoLiveConfigurationRepository(ITransactionCoordinator transactions, TimeProvider clock)
{
    internal static async Task<HrGoLiveConfigurationView?> GetWithinTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId, bool useLock, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand($"select legal_entity_id,go_live_date,employee_migration_strategy,attendance_history_boundary,leave_opening_boundary,leave_history_boundary,document_migration_strategy,reason,updated_at,version from business_hr.legal_entity_go_live_configuration where tenant_id=$1 and legal_entity_id=$2{(useLock ? " for share" : string.Empty)}", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    public Task<HrGoLiveConfigurationView?> GetAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        transactions.ExecuteAsync<HrGoLiveConfigurationView?>(tenant, (context, token) =>
            GetWithinTransactionAsync(context.Connection, context.Transaction, tenant, legalEntityId, false, token), ct);

    public Task<HrGoLiveConfigurationView> SaveAsync(TenantId tenant, Guid actor, Guid membership, Guid legalEntityId, HrGoLiveConfigurationRequest request, Guid correlationId, CancellationToken ct)
    {
        Validate(request);
        return transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            HrGoLiveConfigurationView? before;
            await using (var current = new NpgsqlCommand("select legal_entity_id,go_live_date,employee_migration_strategy,attendance_history_boundary,leave_opening_boundary,leave_history_boundary,document_migration_strategy,reason,updated_at,version from business_hr.legal_entity_go_live_configuration where tenant_id=$1 and legal_entity_id=$2 for update", context.Connection, context.Transaction))
            {
                current.Parameters.AddWithValue(tenant.Value); current.Parameters.AddWithValue(legalEntityId);
                await using var reader = await current.ExecuteReaderAsync(token);
                before = await reader.ReadAsync(token) ? Read(reader) : null;
            }
            var expected = before?.Version ?? 0;
            if (request.ExpectedVersion != expected) throw new HrException("hr.go_live_configuration_concurrency_conflict");
            var now = clock.GetUtcNow();
            var sql = before is null
            ? "insert into business_hr.legal_entity_go_live_configuration(tenant_id,legal_entity_id,go_live_date,employee_migration_strategy,attendance_history_boundary,leave_opening_boundary,leave_history_boundary,document_migration_strategy,reason,updated_by,updated_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,1) returning legal_entity_id,go_live_date,employee_migration_strategy,attendance_history_boundary,leave_opening_boundary,leave_history_boundary,document_migration_strategy,reason,updated_at,version"
                : "update business_hr.legal_entity_go_live_configuration set go_live_date=$3,employee_migration_strategy=$4,attendance_history_boundary=$5,leave_opening_boundary=$6,leave_history_boundary=$7,document_migration_strategy=$8,reason=$9,updated_by=$10,updated_at=$11,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and version=$12 returning legal_entity_id,go_live_date,employee_migration_strategy,attendance_history_boundary,leave_opening_boundary,leave_history_boundary,document_migration_strategy,reason,updated_at,version";
            HrGoLiveConfigurationView result;
            await using (var save = new NpgsqlCommand(sql, context.Connection, context.Transaction))
            {
                save.Parameters.AddWithValue(tenant.Value); save.Parameters.AddWithValue(legalEntityId); save.Parameters.AddWithValue(request.GoLiveDate);
                save.Parameters.AddWithValue(request.EmployeeMigrationStrategy); save.Parameters.AddWithValue((object?)request.AttendanceHistoryBoundary ?? DBNull.Value);
                save.Parameters.AddWithValue((object?)request.LeaveOpeningBoundary ?? DBNull.Value); save.Parameters.AddWithValue((object?)request.LeaveHistoryBoundary ?? DBNull.Value);
                save.Parameters.AddWithValue(request.DocumentMigrationStrategy); save.Parameters.AddWithValue(request.Reason.Trim()); save.Parameters.AddWithValue(actor); save.Parameters.AddWithValue(now);
                if (before is not null) save.Parameters.AddWithValue(expected);
                await using var reader = await save.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new HrException("hr.go_live_configuration_concurrency_conflict");
                result = Read(reader);
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membership, "hr", "hr.go_live_configuration.saved", "legal_entity_go_live_configuration", legalEntityId,
                    before is null ? null : JsonSerializer.Serialize(before), JsonSerializer.Serialize(result), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
            return result;
        }, ct);
    }

    private static void Validate(HrGoLiveConfigurationRequest request)
    {
        if (request.GoLiveDate == default || request.ExpectedVersion < 0 || request.Reason?.Trim().Length is < 1 or > 500)
            throw new HrException("hr.go_live_configuration_invalid");
        if (request.EmployeeMigrationStrategy is not ("ActiveOnly" or "ActiveAndHistorical")
            || request.DocumentMigrationStrategy is not ("None" or "AllAvailable"))
            throw new HrException("hr.go_live_configuration_strategy_invalid");
        if (request.AttendanceHistoryBoundary is { } attendance && attendance > request.GoLiveDate
            || request.LeaveOpeningBoundary is { } leave && leave > request.GoLiveDate
            || request.LeaveHistoryBoundary is { } leaveHistory && leaveHistory > request.GoLiveDate)
            throw new HrException("hr.go_live_configuration_boundary_invalid");
    }

    private static HrGoLiveConfigurationView Read(NpgsqlDataReader reader) =>
        new(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5),
            reader.GetString(6), reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8), reader.GetInt64(9));
}
