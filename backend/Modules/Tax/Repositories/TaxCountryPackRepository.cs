using System.Globalization;
using System.Text.Json;
using Core.Transactions;
using Modules.Tax.DTOs;
using Modules.Tax.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Tax.Repositories;

public sealed class TaxCountryPackRepository(NpgsqlDataSource dataSource, ITransactionCoordinator transactions)
{
    public async Task<IReadOnlyList<TaxCountryPackDto>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand(Sql + " where tenant_id=$1 order by country_code,pack_code,pack_version", connection, transaction);
        command.Parameters.AddWithValue(tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<TaxCountryPackDto>();
        while (await reader.ReadAsync(cancellationToken)) result.Add(Read(reader));
        return result;
    }

    public async Task<TaxCountryPackDto?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand(Sql + " where tenant_id=$1 and id=$2", connection, transaction);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<TaxCountryPackDto> InstallAsync(Guid tenantId, Guid actorId, Guid correlationId, InstallTaxCountryPackRequest request, CancellationToken cancellationToken)
    {
        ValidateInstallRequest(request);
        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<TaxCountryPackDto>(new TenantId(tenantId), async (context, token) =>
        {
            await using (var existing = new NpgsqlCommand(Sql + " where tenant_id=$1 and country_code=$2 and pack_code=$3 and pack_version=$4 for update", context.Connection, context.Transaction))
            {
                existing.Parameters.AddWithValue(tenantId);
                existing.Parameters.AddWithValue(request.CountryCode);
                existing.Parameters.AddWithValue(request.PackCode);
                existing.Parameters.AddWithValue(request.PackVersion);
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    var current = Read(reader);
                    if (!EquivalentInstall(current, request)) throw new TaxException("tax.country_pack_version_exists", "This Country Pack version is already installed with different immutable content.");
                    return current;
                }
            }

            var reviewStatus = "Unreviewed";
            const string sql = "insert into foundation_tax.tax_pack_activation (tenant_id,id,pack_id,country_code,pack_code,pack_version,engine_contract_version,source_authority,authority_reference,source_title,effective_from,effective_to,review_status,status,source_kind,capabilities,rule_set_references,golden_vector_count,golden_vector_status,validation_status,validation_issues,installed_at,installed_by) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16::jsonb,$17::jsonb,$18,$19,'NotValidated','[]'::jsonb,now(),$20)";
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(id);
            command.Parameters.AddWithValue(request.PackCode);
            command.Parameters.AddWithValue(request.CountryCode);
            command.Parameters.AddWithValue(request.PackCode);
            command.Parameters.AddWithValue(request.PackVersion);
            command.Parameters.AddWithValue(request.EngineContractVersion);
            command.Parameters.AddWithValue(request.SourceAuthority);
            command.Parameters.AddWithValue(request.SourceReference);
            command.Parameters.AddWithValue(request.SourceTitle);
            command.Parameters.AddWithValue(request.EffectiveFrom);
            command.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value);
            command.Parameters.AddWithValue(reviewStatus);
            command.Parameters.AddWithValue("Installed");
            command.Parameters.AddWithValue(request.SourceKind);
            command.Parameters.AddWithValue(JsonSerializer.Serialize(request.Capabilities));
            command.Parameters.AddWithValue(JsonSerializer.Serialize(request.RuleSetReferences));
            command.Parameters.AddWithValue(request.GoldenVectorCount);
            command.Parameters.AddWithValue(request.GoldenVectorStatus);
            command.Parameters.AddWithValue(actorId);
            await command.ExecuteNonQueryAsync(token);
            await AuditAsync(context, tenantId, actorId, correlationId, "CountryPackInstalled", id, null, new { request.PackCode, request.PackVersion, request.CountryCode, request.SourceKind }, token);
            return await ReadByIdAsync(context.Connection, context.Transaction, tenantId, id, token) ?? throw new TaxException("tax.country_pack_not_found", "The installed Country Pack could not be reloaded.");
        }, cancellationToken);
    }

    public async Task<TaxCountryPackValidationDto> ValidateAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid id, CancellationToken cancellationToken)
    {
        return await transactions.ExecuteAsync<TaxCountryPackValidationDto>(new TenantId(tenantId), async (context, token) =>
        {
            var pack = await ReadByIdAsync(context.Connection, context.Transaction, tenantId, id, token) ?? throw new TaxException("tax.country_pack_not_found", "Country Pack was not found.");
            var issues = await ValidatePersistedAsync(context, tenantId, pack, token);
            var before = new { pack.Status, pack.ValidationStatus };
            var nextStatus = issues.Count == 0 && pack.Status == "Installed" ? "Validated" : pack.Status;
            await using var update = new NpgsqlCommand("update foundation_tax.tax_pack_activation set status=$3,validation_status=$4,validation_issues=$5::jsonb,validated_at=now(),validated_by=$6 where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
            update.Parameters.AddWithValue(tenantId); update.Parameters.AddWithValue(id); update.Parameters.AddWithValue(nextStatus); update.Parameters.AddWithValue(issues.Count == 0 ? "Valid" : "Invalid"); update.Parameters.AddWithValue(JsonSerializer.Serialize(issues)); update.Parameters.AddWithValue(actorId);
            await update.ExecuteNonQueryAsync(token);
            await AuditAsync(context, tenantId, actorId, correlationId, "CountryPackValidated", id, before, new { status = nextStatus, valid = issues.Count == 0, issues }, token);
            return new(id, issues.Count == 0, issues.Select(MapIssue).ToArray());
        }, cancellationToken);
    }

    public async Task<TaxCountryPackDto> ScheduleAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid id, DateOnly? effectiveFrom, CancellationToken cancellationToken)
    {
        return await transactions.ExecuteAsync<TaxCountryPackDto>(new TenantId(tenantId), async (context, token) =>
        {
            var pack = await ReadByIdAsync(context.Connection, context.Transaction, tenantId, id, token) ?? throw new TaxException("tax.country_pack_not_found", "Country Pack was not found.");
            if (pack.Status != "Validated" || pack.ValidationStatus != "Valid") throw new TaxException("tax.country_pack_transition_invalid", "Only a valid Country Pack can be scheduled.");
            var date = effectiveFrom ?? pack.EffectiveFrom;
            EnsureEffectivePeriod(pack, date);
            await EnsureNoOverlapAsync(context, tenantId, pack, date, token);
            await using var update = new NpgsqlCommand("update foundation_tax.tax_pack_activation set status='Scheduled',effective_from=$3,scheduled_effective_from=$3,scheduled_at=now(),scheduled_by=$4 where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
            update.Parameters.AddWithValue(tenantId); update.Parameters.AddWithValue(id); update.Parameters.AddWithValue(date); update.Parameters.AddWithValue(actorId); await update.ExecuteNonQueryAsync(token);
            await AuditAsync(context, tenantId, actorId, correlationId, "CountryPackScheduled", id, new { pack.Status, pack.EffectiveFrom }, new { status = "Scheduled", effectiveFrom = date }, token);
            return await ReadByIdAsync(context.Connection, context.Transaction, tenantId, id, token) ?? throw new TaxException("tax.country_pack_not_found", "Country Pack was not found.");
        }, cancellationToken);
    }

    public async Task<TaxCountryPackDto> ActivateAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid id, DateOnly? effectiveFrom, bool forceFailureAfterMutation, CancellationToken cancellationToken)
    {
        return await transactions.ExecuteAsync<TaxCountryPackDto>(new TenantId(tenantId), async (context, token) =>
        {
            var pack = await ReadByIdAsync(context.Connection, context.Transaction, tenantId, id, token) ?? throw new TaxException("tax.country_pack_not_found", "Country Pack was not found.");
            if (pack.Status is not ("Validated" or "Scheduled") || pack.ValidationStatus != "Valid") throw new TaxException("tax.country_pack_transition_invalid", "Only a valid Country Pack can be activated.");
            var issues = await ValidatePersistedAsync(context, tenantId, pack, token);
            if (issues.Count != 0) throw new TaxException("tax.country_pack_validation_required", "Country Pack validation must pass before activation.");
            if (pack.SourceKind == "Statutory" && (!await HasApprovedReviewAsync(context, tenantId, id, token) || pack.GoldenVectorStatus != "Passed" || pack.GoldenVectorCount < 1))
                throw new TaxException("tax.country_pack_activation_gate", "Statutory Country Pack activation requires approved review and passed golden vectors.");
            var date = effectiveFrom ?? pack.ScheduledEffectiveFrom ?? pack.EffectiveFrom;
            EnsureEffectivePeriod(pack, date);
            await EnsureNoOverlapAsync(context, tenantId, pack, date, token);
            await using (var prior = new NpgsqlCommand("select id,pack_version,effective_from,effective_to,status from foundation_tax.tax_pack_activation where tenant_id=$1 and id<>$2 and country_code=$3 and status='Active' and effective_from<$4 and (effective_to is null or effective_to>$4) for update", context.Connection, context.Transaction))
            {
                prior.Parameters.AddWithValue(tenantId); prior.Parameters.AddWithValue(id); prior.Parameters.AddWithValue(pack.CountryCode); prior.Parameters.AddWithValue(date);
                await using var reader = await prior.ExecuteReaderAsync(token);
                var superseded = new List<Guid>();
                while (await reader.ReadAsync(token)) superseded.Add(reader.GetGuid(0));
                await reader.DisposeAsync();
                foreach (var previousId in superseded)
                {
                    await using var updatePrevious = new NpgsqlCommand("update foundation_tax.tax_pack_activation set status='Superseded',effective_to=$3,superseded_at=now(),superseded_by=$4 where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
                    updatePrevious.Parameters.AddWithValue(tenantId); updatePrevious.Parameters.AddWithValue(previousId); updatePrevious.Parameters.AddWithValue(date); updatePrevious.Parameters.AddWithValue(actorId); await updatePrevious.ExecuteNonQueryAsync(token);
                    await AuditAsync(context, tenantId, actorId, correlationId, "CountryPackSuperseded", previousId, new { status = "Active" }, new { status = "Superseded", effectiveTo = date }, token);
                }
            }
            await using (var update = new NpgsqlCommand("update foundation_tax.tax_pack_activation set status='Active',effective_from=$3,effective_to=null,activated_at=now(),activated_by=$4,superseded_at=null,superseded_by=null where tenant_id=$1 and id=$2", context.Connection, context.Transaction))
            {
                update.Parameters.AddWithValue(tenantId); update.Parameters.AddWithValue(id); update.Parameters.AddWithValue(date); update.Parameters.AddWithValue(actorId); await update.ExecuteNonQueryAsync(token);
            }
            await AuditAsync(context, tenantId, actorId, correlationId, "CountryPackActivated", id, new { status = pack.Status }, new { status = "Active", effectiveFrom = date }, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "TaxCountryPackActivatedV1", JsonSerializer.Serialize(new { country = pack.CountryCode, packCode = pack.PackCode, packVersion = pack.PackVersion, effectiveFrom = date, engineContractVersion = pack.EngineContractVersion }), new TenantId(tenantId), DateTimeOffset.UtcNow, correlationId, id, "tax"));
            if (forceFailureAfterMutation) throw new TaxException("tax.test_forced_activation_failure", "Activation was intentionally failed after state, audit, and Outbox mutation.");
            return await ReadByIdAsync(context.Connection, context.Transaction, tenantId, id, token) ?? throw new TaxException("tax.country_pack_not_found", "Country Pack was not found.");
        }, cancellationToken);
    }

    public async Task<TaxCountryPackDto> RetireAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid id, string? reason, CancellationToken cancellationToken)
    {
        return await transactions.ExecuteAsync<TaxCountryPackDto>(new TenantId(tenantId), async (context, token) =>
        {
            var pack = await ReadByIdAsync(context.Connection, context.Transaction, tenantId, id, token) ?? throw new TaxException("tax.country_pack_not_found", "Country Pack was not found.");
            if (pack.Status is not ("Scheduled" or "Active" or "Superseded")) throw new TaxException("tax.country_pack_transition_invalid", "Only a scheduled or used Country Pack can be retired.");
            await using var update = new NpgsqlCommand("update foundation_tax.tax_pack_activation set status='Retired',retired_at=now(),retired_by=$3,retirement_reason=$4 where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
            update.Parameters.AddWithValue(tenantId); update.Parameters.AddWithValue(id); update.Parameters.AddWithValue(actorId); update.Parameters.AddWithValue((object?)reason ?? DBNull.Value); await update.ExecuteNonQueryAsync(token);
            await AuditAsync(context, tenantId, actorId, correlationId, "CountryPackRetired", id, new { status = pack.Status }, new { status = "Retired", reason }, token);
            return await ReadByIdAsync(context.Connection, context.Transaction, tenantId, id, token) ?? throw new TaxException("tax.country_pack_not_found", "Country Pack was not found.");
        }, cancellationToken);
    }

    public async Task<TaxCountryPackDto> RollbackForFutureAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid id, DateOnly effectiveFrom, CancellationToken cancellationToken)
    {
        return await transactions.ExecuteAsync<TaxCountryPackDto>(new TenantId(tenantId), async (context, token) =>
        {
            var pack = await ReadByIdAsync(context.Connection, context.Transaction, tenantId, id, token) ?? throw new TaxException("tax.country_pack_not_found", "Country Pack was not found.");
            if (pack.Status != "Superseded" || pack.ValidationStatus != "Valid") throw new TaxException("tax.country_pack_rollback_invalid", "Rollback must select a retained, validated prior Country Pack version.");
            if (effectiveFrom <= pack.EffectiveFrom) throw new TaxException("tax.country_pack_rollback_invalid", "Rollback effective date must be after the retained Pack's original effective date.");
            await using (var current = new NpgsqlCommand("select id from foundation_tax.tax_pack_activation where tenant_id=$1 and id<>$2 and country_code=$3 and status='Active' and effective_from<$4 and (effective_to is null or effective_to>$4) for update", context.Connection, context.Transaction))
            {
                current.Parameters.AddWithValue(tenantId); current.Parameters.AddWithValue(id); current.Parameters.AddWithValue(pack.CountryCode); current.Parameters.AddWithValue(effectiveFrom);
                var currentId = await current.ExecuteScalarAsync(token) as Guid?;
                if (currentId is { } value)
                {
                    await using var close = new NpgsqlCommand("update foundation_tax.tax_pack_activation set status='Superseded',effective_to=$3,superseded_at=now(),superseded_by=$4 where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
                    close.Parameters.AddWithValue(tenantId); close.Parameters.AddWithValue(value); close.Parameters.AddWithValue(effectiveFrom); close.Parameters.AddWithValue(actorId); await close.ExecuteNonQueryAsync(token);
                    await AuditAsync(context, tenantId, actorId, correlationId, "CountryPackSuperseded", value, new { status = "Active" }, new { status = "Superseded", effectiveTo = effectiveFrom }, token);
                }
            }
            await using var activate = new NpgsqlCommand("update foundation_tax.tax_pack_activation set status='Active',effective_from=$3,effective_to=null,activated_at=now(),activated_by=$4,superseded_at=null,superseded_by=null where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
            activate.Parameters.AddWithValue(tenantId); activate.Parameters.AddWithValue(id); activate.Parameters.AddWithValue(effectiveFrom); activate.Parameters.AddWithValue(actorId); await activate.ExecuteNonQueryAsync(token);
            await AuditAsync(context, tenantId, actorId, correlationId, "CountryPackRollbackForFuture", id, new { status = pack.Status, packVersion = pack.PackVersion }, new { status = "Active", effectiveFrom }, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "TaxCountryPackActivatedV1", JsonSerializer.Serialize(new { country = pack.CountryCode, packCode = pack.PackCode, packVersion = pack.PackVersion, effectiveFrom, rollbackForFuture = true, engineContractVersion = pack.EngineContractVersion }), new TenantId(tenantId), DateTimeOffset.UtcNow, correlationId, id, "tax"));
            return await ReadByIdAsync(context.Connection, context.Transaction, tenantId, id, token) ?? throw new TaxException("tax.country_pack_not_found", "Country Pack was not found.");
        }, cancellationToken);
    }

    public async Task<TaxDraftImpactResult> GetDraftImpactAsync(Guid tenantId, TaxDraftImpactRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        string? packVersion = null;
        await using (var pack = new NpgsqlCommand("select pack_version from foundation_tax.tax_pack_activation where tenant_id=$1 and country_code=$2 and status in ('Active','Superseded') and effective_from<=$3 and (effective_to is null or effective_to>$3) and validation_status='Valid' order by effective_from desc limit 1", connection, transaction))
        {
            pack.Parameters.AddWithValue(tenantId); pack.Parameters.AddWithValue(request.CountryCode); pack.Parameters.AddWithValue(request.TaxDate); packVersion = await pack.ExecuteScalarAsync(cancellationToken) as string;
        }
        int? ruleVersion = null;
        await using (var rule = new NpgsqlCommand("select version from foundation_tax.tax_rule_set where tenant_id=$1 and (legal_entity_id is null or legal_entity_id=$2) and country_code=$3 and scheme_code is not distinct from $4 and status in ('Active','Superseded') and effective_from<=$5 and (effective_to is null or effective_to>$5) order by effective_from desc limit 1", connection, transaction))
        {
            rule.Parameters.AddWithValue(tenantId); rule.Parameters.AddWithValue(request.LegalEntityId); rule.Parameters.AddWithValue(request.CountryCode); rule.Parameters.AddWithValue((object?)request.SchemeCode ?? DBNull.Value); rule.Parameters.AddWithValue(request.TaxDate); var value = await rule.ExecuteScalarAsync(cancellationToken); if (value is int number) ruleVersion = number;
        }
        var packChanged = request.PreviousPackVersion is not null && !string.Equals(request.PreviousPackVersion, packVersion, StringComparison.Ordinal);
        var ruleChanged = request.PreviousRuleSetVersion is not null && request.PreviousRuleSetVersion != ruleVersion;
        var required = packChanged || ruleChanged;
        var reason = packChanged ? "CountryPackVersionChanged" : ruleChanged ? "RuleSetVersionChanged" : null;
        var fingerprint = packVersion is null && ruleVersion is null ? null : $"{packVersion ?? "missing"}:{ruleVersion?.ToString(CultureInfo.InvariantCulture) ?? "missing"}";
        return new(required, required ? "TaxRecalculationRequired" : "NoRecalculationRequired", reason, packVersion, ruleVersion, fingerprint);
    }

    private static readonly string Sql = "select id,pack_id,country_code,pack_code,pack_version,engine_contract_version,status,review_status,effective_from,effective_to,source_kind,source_authority,source_title,authority_reference,capabilities,rule_set_references,golden_vector_count,golden_vector_status,validation_status,validation_issues,installed_at,installed_by,validated_at,validated_by,scheduled_at,scheduled_by,scheduled_effective_from,activated_at,activated_by,superseded_at,superseded_by,retired_at,retired_by,retirement_reason from foundation_tax.tax_pack_activation";

    private static async Task<TaxCountryPackDto?> ReadByIdAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid id, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(Sql + " where tenant_id=$1 and id=$2", connection, transaction); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(id); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? Read(reader) : null;
    }

    private static TaxCountryPackDto Read(NpgsqlDataReader reader)
    {
        var issues = JsonSerializer.Deserialize<TaxCountryPackValidationIssueDto[]>(reader.GetString(19)) ?? [];
        var capabilities = JsonSerializer.Deserialize<TaxCountryPackCapabilitiesDto>(reader.GetString(14)) ?? new(false, false, false, false, false);
        var references = JsonSerializer.Deserialize<string[]>(reader.GetString(15)) ?? [];
        return new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetFieldValue<DateOnly>(8), reader.IsDBNull(9) ? null : reader.GetFieldValue<DateOnly>(9), reader.GetString(10), reader.GetString(11), reader.GetString(12), reader.GetString(13), capabilities, references, reader.GetInt32(16), reader.GetString(17), reader.GetString(18), issues, reader.GetFieldValue<DateTimeOffset>(20), NullableGuid(reader, 21), NullableInstant(reader, 22), NullableGuid(reader, 23), NullableInstant(reader, 24), NullableGuid(reader, 25), reader.IsDBNull(26) ? null : reader.GetFieldValue<DateOnly>(26), NullableInstant(reader, 27), NullableGuid(reader, 28), NullableInstant(reader, 29), NullableGuid(reader, 30), NullableInstant(reader, 31), NullableGuid(reader, 32), reader.IsDBNull(33) ? null : reader.GetString(33));
    }

    private static Guid? NullableGuid(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetGuid(index);
    private static DateTimeOffset? NullableInstant(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetFieldValue<DateTimeOffset>(index);

    private static bool EquivalentInstall(TaxCountryPackDto current, InstallTaxCountryPackRequest request) =>
        current.CountryCode == request.CountryCode && current.PackCode == request.PackCode && current.PackVersion == request.PackVersion && current.EngineContractVersion == request.EngineContractVersion && current.EffectiveFrom == request.EffectiveFrom && current.EffectiveTo == request.EffectiveTo && current.SourceKind == request.SourceKind && current.SourceAuthority == request.SourceAuthority && current.SourceTitle == request.SourceTitle && current.SourceReference == request.SourceReference && current.RuleSetReferences.SequenceEqual(request.RuleSetReferences) && current.Capabilities == request.Capabilities && current.GoldenVectorCount == request.GoldenVectorCount && current.GoldenVectorStatus == request.GoldenVectorStatus;

    private static async Task<List<TaxCountryPackValidationIssueDto>> ValidatePersistedAsync(ITransactionContext context, Guid tenantId, TaxCountryPackDto pack, CancellationToken token)
    {
        var issues = new List<TaxCountryPackValidationIssueDto>();
        if (pack.CountryCode.Length != 2 || pack.CountryCode.Any(character => !char.IsAsciiLetterUpper(character))) issues.Add(new("tax.country_pack_manifest_invalid", "Pack country code must be two uppercase letters."));
        if (string.IsNullOrWhiteSpace(pack.PackCode) || string.IsNullOrWhiteSpace(pack.PackVersion) || string.IsNullOrWhiteSpace(pack.EngineContractVersion)) issues.Add(new("tax.country_pack_manifest_invalid", "Pack identity and engine contract version are required."));
        if (pack.EngineContractVersion != TaxCalculator.CalculationVersion) issues.Add(new("tax.country_pack_incompatible", "The Country Pack requires a different Tax Engine contract version."));
        if (pack.EffectiveTo is { } to && to <= pack.EffectiveFrom) issues.Add(new("tax.country_pack_effective_period_invalid", "Pack effective end must be later than its start."));
        if (string.IsNullOrWhiteSpace(pack.SourceAuthority) || string.IsNullOrWhiteSpace(pack.SourceTitle) || string.IsNullOrWhiteSpace(pack.SourceReference)) issues.Add(new("tax.country_pack_evidence_missing", "Authority, title, and source reference are required."));
        if (pack.SourceKind == "Statutory" && (pack.ReviewStatus != "Approved" || pack.ValidatedBy is null)) issues.Add(new("tax.country_pack_review_required", "Statutory Pack content requires approved review before activation."));
        if (pack.SourceKind == "TestOnly" && pack.ReviewStatus is not ("Unreviewed" or "Approved")) issues.Add(new("tax.country_pack_test_label_required", "Test Pack content must be explicitly classified as TestOnly."));
        if (pack.RuleSetReferences.Count == 0) issues.Add(new("tax.country_pack_rule_set_missing", "At least one Rule Set reference is required."));
        foreach (var reference in pack.RuleSetReferences)
        {
            await using var rule = new NpgsqlCommand("select exists(select 1 from foundation_tax.tax_rule_set where tenant_id=$1 and (id::text=$2 or scheme_code=$2))", context.Connection, context.Transaction);
            rule.Parameters.AddWithValue(tenantId);
            rule.Parameters.AddWithValue(reference);
            if (!((bool)(await rule.ExecuteScalarAsync(token) ?? false))) issues.Add(new("tax.country_pack_rule_set_missing", "A declared Rule Set reference could not be resolved.", reference));
        }
        if (pack.GoldenVectorCount < 1 || pack.GoldenVectorStatus != "Passed") issues.Add(new("tax.country_pack_golden_vectors_missing", "At least one passed golden vector is required before validation."));
        return issues;
    }

    private static void ValidateInstallRequest(InstallTaxCountryPackRequest request)
    {
        if (request.SourceKind is not ("Statutory" or "TestOnly")) throw new TaxException("tax.country_pack_source_invalid", "Country Pack source kind must be Statutory or TestOnly.");
        if (request.CountryCode.Length != 2 || request.CountryCode.Any(character => !char.IsAsciiLetterUpper(character))) throw new TaxException("tax.country_code_invalid", "Country Code must be two uppercase letters.");
        if (string.IsNullOrWhiteSpace(request.PackCode) || string.IsNullOrWhiteSpace(request.PackVersion) || string.IsNullOrWhiteSpace(request.EngineContractVersion)) throw new TaxException("tax.country_pack_manifest_invalid", "Pack identity and engine contract version are required.");
        if (request.EffectiveTo is { } to && to <= request.EffectiveFrom) throw new TaxException("tax.country_pack_effective_period_invalid", "Pack effective period is invalid.");
        if (request.GoldenVectorCount < 0 || request.GoldenVectorStatus is not ("Missing" or "Pending" or "Passed" or "Failed")) throw new TaxException("tax.country_pack_vector_invalid", "Golden vector metadata is invalid.");
    }

    private static void EnsureEffectivePeriod(TaxCountryPackDto pack, DateOnly date)
    {
        if (date < pack.EffectiveFrom || pack.EffectiveTo is { } end && date >= end) throw new TaxException("tax.country_pack_effective_period_invalid", "Lifecycle effective date must be inside the Pack period.");
    }

    private static async Task EnsureNoOverlapAsync(ITransactionContext context, Guid tenantId, TaxCountryPackDto pack, DateOnly effectiveFrom, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from foundation_tax.tax_pack_activation where tenant_id=$1 and id<>$2 and country_code=$3 and status='Scheduled' and effective_from<$4 and (effective_to is null or effective_to>$4))", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(pack.Id); command.Parameters.AddWithValue(pack.CountryCode); command.Parameters.AddWithValue(effectiveFrom);
        var exists = (bool)(await command.ExecuteScalarAsync(token) ?? false);
        if (exists) throw new TaxException("tax.country_pack_overlap", "Another active or scheduled Country Pack overlaps the requested effective range.");
    }

    private static async Task<bool> HasApprovedReviewAsync(ITransactionContext context, Guid tenantId, Guid id, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from foundation_tax.tax_pack_activation where tenant_id=$1 and id=$2 and review_status='Approved' and reviewed_by is not null and reviewed_at is not null)", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(id); return (bool)(await command.ExecuteScalarAsync(token) ?? false);
    }

    private static TaxCountryPackValidationIssueDto MapIssue(TaxCountryPackValidationIssueDto issue) => issue;

    private static async Task AuditAsync(ITransactionContext context, Guid tenant, Guid actor, Guid correlation, string operation, Guid id, object? before, object? after, CancellationToken token)
    {
        await using var audit = new NpgsqlCommand("insert into foundation_tax.tax_audit(tenant_id,id,actor_id,operation,entity_type,entity_id,before_state,after_state,occurred_at,correlation_id) values($1,$2,$3,$4,'TaxCountryPack',$5,$6::jsonb,$7::jsonb,now(),$8)", context.Connection, context.Transaction);
        audit.Parameters.AddWithValue(tenant); audit.Parameters.AddWithValue(Guid.CreateVersion7()); audit.Parameters.AddWithValue(actor); audit.Parameters.AddWithValue(operation); audit.Parameters.AddWithValue(id); audit.Parameters.AddWithValue(before is null ? DBNull.Value : JsonSerializer.Serialize(before)); audit.Parameters.AddWithValue(after is null ? DBNull.Value : JsonSerializer.Serialize(after)); audit.Parameters.AddWithValue(correlation); await audit.ExecuteNonQueryAsync(token);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); command.Parameters.AddWithValue(tenantId.ToString("D")); await command.ExecuteNonQueryAsync(token);
    }
}
