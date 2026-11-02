using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class LeavePolicyRepository(ITransactionCoordinator transactions, NpgsqlDataSource dataSource, TimeProvider clock, IHrCountryProviderRegistry countryProviders)
{
    public async Task<LeaveTypeView> CreateTypeAsync(TenantId tenant, Guid actor, Guid membershipId, CreateLeaveTypeRequest request, Guid correlationId, CancellationToken ct)
    {
        var code = request.Code?.Trim().ToUpperInvariant(); var name = request.NameAr?.Trim(); var nameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        var category = request.Category?.Trim(); var unit = request.Unit?.Trim(); var country = string.IsNullOrWhiteSpace(request.CountryCode) ? null : request.CountryCode.Trim().ToUpperInvariant(); var provider = string.IsNullOrWhiteSpace(request.CountryProviderCode) ? null : request.CountryProviderCode.Trim();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 40 || string.IsNullOrWhiteSpace(name) || name.Length > 160 || nameEn?.Length > 160
            || category is not ("Annual" or "Sick" or "Unpaid" or "Emergency" or "Parental" or "Study" or "Other") || unit is not ("Days" or "Hours")
            || country is not null && (country.Length != 2 || !country.All(char.IsAsciiLetterUpper)) || provider?.Length > 80)
            throw new HrException("hr.leave_type_invalid");
        if (provider is not null && countryProviders.Find(provider) is null) throw new HrException("hr.country_provider_not_installed");
        return await transactions.ExecuteAsync<LeaveTypeView>(tenant, async (context, token) =>
        {
            var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            await using var insert = new NpgsqlCommand("insert into business_hr.leave_type(id,tenant_id,legal_entity_id,code,name_ar,name_en,category,unit,country_code,country_provider_code,status,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'Active',$11,$12)", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(code); insert.Parameters.AddWithValue(name); insert.Parameters.AddWithValue((object?)nameEn ?? DBNull.Value); insert.Parameters.AddWithValue(category); insert.Parameters.AddWithValue(unit); insert.Parameters.AddWithValue((object?)country ?? DBNull.Value); insert.Parameters.AddWithValue((object?)provider ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "hr.leave_type.created", "leave_type", id, null, JsonSerializer.Serialize(new { code, name, nameEn, category, unit, country, provider }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
            return new(id, request.LegalEntityId, code, name, nameEn, category!, unit!, country, provider, "Active");
        }, ct);
    }

    public async Task<IReadOnlyList<LeaveTypeView>> ListTypesAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,code,name_ar,name_en,category,unit,country_code,country_provider_code,status from business_hr.leave_type where tenant_id=$1 and legal_entity_id=$2 order by code,id limit 200", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); var rows = new List<LeaveTypeView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7)?.Trim(), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetString(9)));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    public IReadOnlyList<HrLeaveTypeTemplate> GetProviderLeaveTemplates(string providerCode)
    {
        var provider = countryProviders.Find(providerCode);
        return provider?.GetLeaveTypeTemplates() ?? throw new HrException("hr.country_provider_not_installed");
    }

    public async Task<LeavePolicyView> CreatePolicyAsync(TenantId tenant, Guid actor, Guid membershipId, CreateLeavePolicyRequest request, Guid correlationId, CancellationToken ct)
    {
        var mode = request.AccrualMode?.Trim(); var counting = request.CountingMethod?.Trim(); var negative = request.NegativeBalancePolicy?.Trim(); var payrollTreatment = request.PayrollTreatment?.Trim();
        var eligibleTypes = (request.EligibleEmploymentTypes ?? []).Distinct(StringComparer.Ordinal).ToArray();
        var requestedProvider = string.IsNullOrWhiteSpace(request.CountryProviderCode) ? null : request.CountryProviderCode.Trim();
        if (request.LeaveTypeId == Guid.Empty || request.EffectiveFrom == default || request.EffectiveTo is { } end && end < request.EffectiveFrom
            || counting is not ("WorkingDays" or "CalendarDays") || negative is not ("Disallow" or "AllowWithinLimit" or "RequireApproval" or "Allow")
            || payrollTreatment is not ("Paid" or "Unpaid" or "ProviderDetermined")
            || request.NegativeBalanceLimit < 0 || (negative == "AllowWithinLimit" ? request.NegativeBalanceLimit <= 0 : request.NegativeBalanceLimit != 0)
            || mode is not ("None" or "Manual" or "Monthly" or "Annual" or "CountryProvider")
            || (mode is "Monthly" or "Annual" ? request.AccrualAmount is null or <= 0 : request.AccrualAmount is not null)
            || mode == "CountryProvider" && string.IsNullOrWhiteSpace(request.CountryProviderCode)
            || payrollTreatment == "ProviderDetermined" && string.IsNullOrWhiteSpace(request.CountryProviderCode)
            || request.CarryoverLimit is < 0 || request.CarryoverExpiryMonths is < 1 or > 120
            || request.CarryoverLimit is > 0 && request.CarryoverExpiryMonths is null
            || eligibleTypes.Any(type => !Enum.TryParse<EmploymentType>(type, out _)))
            throw new HrException("hr.leave_policy_invalid");
        if (requestedProvider is not null && countryProviders.Find(requestedProvider) is null) throw new HrException("hr.country_provider_not_installed");

        return await transactions.ExecuteAsync<LeavePolicyView>(tenant, async (context, token) =>
        {
            await using (var exists = new NpgsqlCommand("select 1 from business_hr.leave_type where tenant_id=$1 and id=$2 and legal_entity_id=$3 and status='Active'", context.Connection, context.Transaction))
            {
                exists.Parameters.AddWithValue(tenant.Value); exists.Parameters.AddWithValue(request.LeaveTypeId); exists.Parameters.AddWithValue(request.LegalEntityId);
                if (await exists.ExecuteScalarAsync(token) is null) throw new HrException("hr.leave_type_not_found");
            }
            await using (var lockCommand = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1::text || ':' || $2::text || ':leave-policy',0))", context.Connection, context.Transaction))
            {
                lockCommand.Parameters.AddWithValue(tenant.Value); lockCommand.Parameters.AddWithValue(request.LeaveTypeId); await lockCommand.ExecuteNonQueryAsync(token);
            }
            await using (var overlap = new NpgsqlCommand("select exists(select 1 from business_hr.leave_policy where tenant_id=$1 and leave_type_id=$2 and daterange(effective_from,case when effective_to is null then null else effective_to+1 end,'[)') && daterange($3::date,case when $4::date is null then null else $4::date+1 end,'[)'))", context.Connection, context.Transaction))
            {
                overlap.Parameters.AddWithValue(tenant.Value); overlap.Parameters.AddWithValue(request.LeaveTypeId); overlap.Parameters.AddWithValue(request.EffectiveFrom); overlap.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value);
                if ((bool)(await overlap.ExecuteScalarAsync(token) ?? false)) throw new HrException("hr.leave_policy_overlap");
            }
            int version;
            await using (var next = new NpgsqlCommand("select coalesce(max(version),0)+1 from business_hr.leave_policy where tenant_id=$1 and leave_type_id=$2", context.Connection, context.Transaction))
            {
                next.Parameters.AddWithValue(tenant.Value); next.Parameters.AddWithValue(request.LeaveTypeId); version = (int)(await next.ExecuteScalarAsync(token) ?? 1);
            }
            var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var provider = requestedProvider;
            var accrualJson = JsonSerializer.Serialize(new { mode, amount = request.AccrualAmount, frequency = mode is "Monthly" or "Annual" ? mode : null });
            var carryoverJson = JsonSerializer.Serialize(new { quantity = request.CarryoverLimit, expiryMonths = request.CarryoverExpiryMonths });
            var eligibilityJson = JsonSerializer.Serialize(new { employmentTypes = eligibleTypes });
            await using (var insert = new NpgsqlCommand("insert into business_hr.leave_policy(id,tenant_id,legal_entity_id,leave_type_id,version,effective_from,effective_to,counting_method,approval_required,partial_day_allowed,negative_balance_policy,negative_balance_limit,payroll_treatment,accrual_rule,carryover_rule,eligibility_rules,country_provider_code,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14::jsonb,$15::jsonb,$16::jsonb,$17,$18,$19)", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(request.LeaveTypeId); insert.Parameters.AddWithValue(version); insert.Parameters.AddWithValue(request.EffectiveFrom); insert.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value); insert.Parameters.AddWithValue(counting!); insert.Parameters.AddWithValue(request.ApprovalRequired); insert.Parameters.AddWithValue(request.PartialDayAllowed); insert.Parameters.AddWithValue(negative!); insert.Parameters.AddWithValue(request.NegativeBalanceLimit); insert.Parameters.AddWithValue(payrollTreatment!); insert.Parameters.AddWithValue(accrualJson); insert.Parameters.AddWithValue(carryoverJson); insert.Parameters.AddWithValue(eligibilityJson); insert.Parameters.AddWithValue((object?)provider ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(token);
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "hr.leave_policy.created", "leave_policy", id, null, JsonSerializer.Serialize(new { request.LeaveTypeId, version, request.EffectiveFrom, request.EffectiveTo, countingMethod = counting, request.ApprovalRequired, request.PartialDayAllowed, negative, request.NegativeBalanceLimit, accrual = accrualJson, carryover = carryoverJson, eligibility = eligibilityJson, provider }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
            return new(id, request.LeaveTypeId, version, request.EffectiveFrom, request.EffectiveTo, counting!, request.ApprovalRequired, request.PartialDayAllowed, negative!, request.NegativeBalanceLimit, payrollTreatment!, mode!, request.AccrualAmount, request.CarryoverLimit, request.CarryoverExpiryMonths, eligibleTypes, provider);
        }, ct);
    }

    public async Task<IReadOnlyList<LeavePolicyView>> ListPoliciesAsync(TenantId tenant, Guid legalEntityId, Guid leaveTypeId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        await using var command = new NpgsqlCommand("select id,leave_type_id,version,effective_from,effective_to,counting_method,approval_required,partial_day_allowed,negative_balance_policy,negative_balance_limit,payroll_treatment,accrual_rule,carryover_rule,eligibility_rules,country_provider_code from business_hr.leave_policy where tenant_id=$1 and legal_entity_id=$2 and leave_type_id=$3 order by effective_from desc,version desc limit 100", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(leaveTypeId); var rows = new List<LeavePolicyView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(ReadPolicy(reader));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    private static LeavePolicyView ReadPolicy(NpgsqlDataReader reader)
    {
        using var accrualDoc = JsonDocument.Parse(reader.GetString(11)); using var carryoverDoc = JsonDocument.Parse(reader.GetString(12)); using var eligibilityDoc = JsonDocument.Parse(reader.GetString(13));
        var accrual = accrualDoc.RootElement; var carryover = carryoverDoc.RootElement; var eligibility = eligibilityDoc.RootElement;
        var types = eligibility.TryGetProperty("employmentTypes", out var list) ? list.EnumerateArray().Select(static item => item.GetString()!).ToArray() : [];
        var amount = accrual.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDecimal() : (decimal?)null;
        var carryQuantity = carryover.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetDecimal() : (decimal?)null;
        var expiry = carryover.TryGetProperty("expiryMonths", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : (int?)null;
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetFieldValue<DateOnly>(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4), reader.GetString(5), reader.GetBoolean(6), reader.GetBoolean(7), reader.GetString(8), reader.GetDecimal(9), reader.GetString(10), accrual.GetProperty("mode").GetString()!, amount, carryQuantity, expiry, types, reader.IsDBNull(14) ? null : reader.GetString(14));
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct);
    }
}
