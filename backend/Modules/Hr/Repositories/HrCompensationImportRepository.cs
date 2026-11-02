using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrCompensationImportRepository(ITransactionCoordinator transactions, TimeProvider clock)
{
    private static readonly string[] Frequencies = ["Hourly", "Daily", "Weekly", "Biweekly", "Monthly", "Quarterly", "Annual", "Other"];

    public Task<HrCompensationImportSummary> PreviewAsync(TenantId tenant, HrCompensationImportRequest request, CancellationToken ct) => RunAsync(tenant, Guid.Empty, Guid.Empty, Guid.Empty, request, false, ct);
    public Task<HrCompensationImportSummary> CommitAsync(TenantId tenant, Guid actor, Guid membership, Guid correlation, HrCompensationImportRequest request, CancellationToken ct) =>
        RunAsync(tenant, actor, membership, correlation, request, true, ct);

    private async Task<HrCompensationImportSummary> RunAsync(TenantId tenant, Guid actor, Guid membership, Guid correlation, HrCompensationImportRequest request, bool commit, CancellationToken ct)
    {
        ValidateEnvelope(request);
        var results = new List<HrCompensationImportRowResult>(request.Rows.Count);
        foreach (var row in request.Rows)
        {
            var issues = ValidateRow(row);
            if (row.Status == "Active" && request.Rows.Any(other => !ReferenceEquals(row, other)
                && string.Equals(row.LegacyEmployeeId?.Trim(), other.LegacyEmployeeId?.Trim(), StringComparison.Ordinal)
                && other.Status == "Active" && RangesOverlap(row.EffectiveFrom, row.EffectiveTo, other.EffectiveFrom, other.EffectiveTo)))
                issues.Add("hr.compensation_dates_overlap");
            if (issues.Count != 0) { results.Add(new(row.RowReference, "Rejected", null, issues)); continue; }
            try
            {
                results.Add(await transactions.ExecuteAsync<HrCompensationImportRowResult>(tenant, async (context, token) =>
                {
                    var payloadHash = Hash(row);
                    await using (var existing = new NpgsqlCommand("select agreement_id,row_payload_sha256 from business_hr.employment_compensation_import_reference where tenant_id=$1 and source_system=$2 and legacy_agreement_id=$3", context.Connection, context.Transaction))
                    {
                        existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.SourceSystem.Trim()); existing.Parameters.AddWithValue(row.LegacyAgreementId.Trim());
                        await using var reader = await existing.ExecuteReaderAsync(token);
                        if (await reader.ReadAsync(token)) return string.Equals(reader.GetString(1), payloadHash, StringComparison.Ordinal)
                            ? new(row.RowReference, "AlreadyImported", reader.GetGuid(0), []) : new(row.RowReference, "Rejected", null, ["hr.import_row_conflict"]);
                    }
                    var employment = await ResolveEmploymentAsync(context.Connection, context.Transaction, tenant, request.SourceSystem.Trim(), row, token);
                    if (employment.Id is null) return new(row.RowReference, "Rejected", null, ["hr.import_employee_not_found"]);
                    if (employment.LegalEntityId != row.LegalEntityId) return new(row.RowReference, "Rejected", null, ["hr.import_legal_entity_mismatch"]);
                    if (await HrGoLiveConfigurationRepository.GetWithinTransactionAsync(context.Connection, context.Transaction, tenant, row.LegalEntityId, commit, token) is null)
                        return new(row.RowReference, "Rejected", null, ["hr.go_live_configuration_required"]);
                    if (row.Status == "Active" && await HasOverlapAsync(context.Connection, context.Transaction, tenant, employment.Id.Value, row, token))
                        return new(row.RowReference, "Rejected", null, ["hr.compensation_dates_overlap"]);
                    Guid? contractId = null;
                    if (row.LegacyContractId is { Length: > 0 })
                    {
                        await using var contract = new NpgsqlCommand("select contract_id from business_hr.employment_contract_import_reference where tenant_id=$1 and source_system=$2 and legacy_contract_id=$3 and employment_id=$4", context.Connection, context.Transaction);
                        contract.Parameters.AddWithValue(tenant.Value); contract.Parameters.AddWithValue(request.SourceSystem.Trim()); contract.Parameters.AddWithValue(row.LegacyContractId.Trim()); contract.Parameters.AddWithValue(employment.Id.Value);
                        contractId = (Guid?)await contract.ExecuteScalarAsync(token);
                        if (contractId is null) return new(row.RowReference, "Rejected", null, ["hr.import_contract_not_found"]);
                    }
                    if (!commit) return new(row.RowReference, "Ready", null, []);

                    var now = clock.GetUtcNow(); var agreementId = Guid.CreateVersion7();
                    await using (var insert = new NpgsqlCommand("insert into business_hr.employment_compensation_agreement(id,tenant_id,employment_id,legal_entity_id,currency_code,amount,pay_frequency,effective_from,effective_to,status,contract_id,reason,evidence_file_ref,created_by,created_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,null,$13,$14,1)", context.Connection, context.Transaction))
                    {
                        insert.Parameters.AddWithValue(agreementId); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employment.Id.Value); insert.Parameters.AddWithValue(row.LegalEntityId);
                        insert.Parameters.AddWithValue(row.CurrencyCode.Trim().ToUpperInvariant()); insert.Parameters.AddWithValue(row.Amount); insert.Parameters.AddWithValue(row.PayFrequency); insert.Parameters.AddWithValue(row.EffectiveFrom);
                        insert.Parameters.AddWithValue((object?)row.EffectiveTo ?? DBNull.Value); insert.Parameters.AddWithValue(row.Status); insert.Parameters.AddWithValue((object?)contractId ?? DBNull.Value);
                        insert.Parameters.AddWithValue(row.Reason.Trim()); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(token);
                    }
                    await using (var reference = new NpgsqlCommand("insert into business_hr.employment_compensation_import_reference(tenant_id,id,source_system,legacy_agreement_id,batch_reference,file_sha256,row_reference,row_payload_sha256,employment_id,agreement_id,data_quality,imported_at,imported_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", context.Connection, context.Transaction))
                    {
                        reference.Parameters.AddWithValue(tenant.Value); reference.Parameters.AddWithValue(Guid.CreateVersion7()); reference.Parameters.AddWithValue(request.SourceSystem.Trim()); reference.Parameters.AddWithValue(row.LegacyAgreementId.Trim());
                        reference.Parameters.AddWithValue(request.BatchReference.Trim()); reference.Parameters.AddWithValue(request.FileSha256.ToLowerInvariant()); reference.Parameters.AddWithValue(row.RowReference.Trim()); reference.Parameters.AddWithValue(payloadHash);
                        reference.Parameters.AddWithValue(employment.Id.Value); reference.Parameters.AddWithValue(agreementId); reference.Parameters.AddWithValue(row.DataQuality); reference.Parameters.AddWithValue(now); reference.Parameters.AddWithValue(actor);
                        await reference.ExecuteNonQueryAsync(token);
                    }
                    var payload = JsonSerializer.Serialize(new { agreementId, row.LegacyAgreementId, row.CurrencyCode, row.Amount, row.PayFrequency, row.EffectiveFrom, row.EffectiveTo, row.Status, row.DataQuality, contractId });
                    var eventId = Guid.CreateVersion7();
                    await using (var eventInsert = new NpgsqlCommand("insert into business_hr.employment_event(tenant_id,id,employee_id,employment_id,event_type,effective_on,actor_id,payload,correlation_id,occurred_at) values($1,$2,$3,$4,'CompensationHistoryImported',$5,$6,$7::jsonb,$8,$9)", context.Connection, context.Transaction))
                    {
                        eventInsert.Parameters.AddWithValue(tenant.Value); eventInsert.Parameters.AddWithValue(eventId); eventInsert.Parameters.AddWithValue(employment.EmployeeId); eventInsert.Parameters.AddWithValue(employment.Id.Value);
                        eventInsert.Parameters.AddWithValue(row.EffectiveFrom); eventInsert.Parameters.AddWithValue(actor); eventInsert.Parameters.AddWithValue(payload); eventInsert.Parameters.AddWithValue(correlation); eventInsert.Parameters.AddWithValue(now); await eventInsert.ExecuteNonQueryAsync(token);
                    }
                    await using (var projection = new NpgsqlCommand("insert into business_hr.employee_timeline_projection(tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload) values($1,$2,$3,$4,'CompensationHistoryImported',$5,$6,$7::jsonb)", context.Connection, context.Transaction))
                    {
                        projection.Parameters.AddWithValue(tenant.Value); projection.Parameters.AddWithValue(eventId); projection.Parameters.AddWithValue(employment.EmployeeId); projection.Parameters.AddWithValue(employment.Id.Value);
                        projection.Parameters.AddWithValue(row.EffectiveFrom); projection.Parameters.AddWithValue(now); projection.Parameters.AddWithValue(payload); await projection.ExecuteNonQueryAsync(token);
                    }
                    await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, row.LegalEntityId, null, "Human", actor, membership, "hr", "hr.compensation.history.imported", "employment_compensation_agreement", agreementId, null, payload, null, "hr-api", correlation, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
                    return new(row.RowReference, "Imported", agreementId, []);
                }, ct));
            }
            catch (Exception exception) when (commit && exception is HrException or PostgresException { SqlState: "23503" or "23505" or "23514" or "23P01" })
            {
                results.Add(new(row.RowReference, "Rejected", null, [exception is PostgresException { MessageText: "hr.compensation_dates_overlap" } ? "hr.compensation_dates_overlap" : "hr.import_row_invalid"]));
            }
        }
        var imported = results.Count(item => item.Status == "Imported"); var already = results.Count(item => item.Status == "AlreadyImported"); var rejected = results.Count(item => item.Status == "Rejected");
        return new(results.Count, imported, already, rejected, results);
    }

    private static void ValidateEnvelope(HrCompensationImportRequest request)
    {
        if (request.Rows is null || request.Rows.Count is 0 or > 100 || request.Rows.Any(row => row is null)) throw new HrException("hr.import_row_count_invalid");
        if (string.IsNullOrWhiteSpace(request.SourceSystem) || request.SourceSystem.Trim().Length > 80 || string.IsNullOrWhiteSpace(request.BatchReference) || request.BatchReference.Trim().Length > 160) throw new HrException("hr.import_source_invalid");
        if (request.FileSha256 is null || request.FileSha256.Length != 64 || !request.FileSha256.All(Uri.IsHexDigit)) throw new HrException("hr.import_file_hash_invalid");
        if (request.Rows.Select(row => row.RowReference?.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Rows.Count || request.Rows.Select(row => row.LegacyAgreementId?.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Rows.Count) throw new HrException("hr.import_row_reference_invalid");
    }

    private static List<string> ValidateRow(HrCompensationImportRow row)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(row.RowReference) || row.RowReference.Trim().Length > 160 || string.IsNullOrWhiteSpace(row.LegacyAgreementId) || row.LegacyAgreementId.Trim().Length > 160) issues.Add("hr.import_row_reference_invalid");
        if (string.IsNullOrWhiteSpace(row.LegacyEmployeeId) || row.LegacyEmployeeId.Trim().Length > 160 || row.LegalEntityId == Guid.Empty) issues.Add("hr.import_employee_reference_invalid");
        if (row.CurrencyCode?.Length != 3 || row.CurrencyCode.Any(static c => c is < 'A' or > 'Z') || row.Amount <= 0 || row.Amount > 999999999999999m || !Frequencies.Contains(row.PayFrequency, StringComparer.Ordinal) || row.EffectiveFrom == default || row.EffectiveTo < row.EffectiveFrom || row.Status is not ("Draft" or "Active" or "Superseded" or "Ended") || string.IsNullOrWhiteSpace(row.Reason) || row.Reason.Trim().Length > 1000) issues.Add("hr.compensation_import_terms_invalid");
        if (row.Status is "Superseded" or "Ended" && row.EffectiveTo is null || row.Status == "Draft" && row.EffectiveTo is not null) issues.Add("hr.compensation_import_status_invalid");
        if (row.DataQuality is not ("Complete" or "Partial" or "UnknownHistory")) issues.Add("hr.import_data_quality_invalid");
        return issues;
    }

    private static async Task<(Guid? Id, Guid EmployeeId, Guid? LegalEntityId)> ResolveEmploymentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, string source, HrCompensationImportRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select e.id,e.employee_id,e.legal_entity_id from business_hr.employee_import_reference r join business_hr.employment e on e.tenant_id=r.tenant_id and e.id=r.employment_id where r.tenant_id=$1 and r.source_system=$2 and r.legacy_employee_id=$3", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(source); command.Parameters.AddWithValue(row.LegacyEmployeeId.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2)) : (null, Guid.Empty, null);
    }

    private static async Task<bool> HasOverlapAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employmentId, HrCompensationImportRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from business_hr.employment_compensation_agreement c where c.tenant_id=$1 and c.employment_id=$2 and c.status='Active' and daterange(c.effective_from,c.effective_to,'[]') && daterange($3::date,$4::date,'[]'))", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(row.EffectiveFrom); command.Parameters.AddWithValue((object?)row.EffectiveTo ?? DBNull.Value);
        return await command.ExecuteScalarAsync(ct) is true;
    }

    private static bool RangesOverlap(DateOnly leftFrom, DateOnly? leftTo, DateOnly rightFrom, DateOnly? rightTo) => leftFrom <= (rightTo ?? DateOnly.MaxValue) && rightFrom <= (leftTo ?? DateOnly.MaxValue);
    private static string Hash(HrCompensationImportRow row) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { row.RowReference, legacyAgreementId = row.LegacyAgreementId.Trim(), legacyEmployeeId = row.LegacyEmployeeId.Trim(), row.LegalEntityId, currencyCode = row.CurrencyCode.Trim().ToUpperInvariant(), row.Amount, row.PayFrequency, row.EffectiveFrom, row.EffectiveTo, row.Status, reason = row.Reason.Trim(), row.DataQuality, row.LegacyContractId })))).ToLowerInvariant();
}
