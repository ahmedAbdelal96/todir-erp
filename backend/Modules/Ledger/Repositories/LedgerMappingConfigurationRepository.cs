using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Modules.Ledger.Services;
using Npgsql;
using Shared.BuildingBlocks.Observability;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger.Repositories;

public sealed class LedgerMappingConfigurationRepository(
    NpgsqlDataSource dataSource,
    ITransactionCoordinator transactions,
    LedgerConfigurationCache cache,
    TimeProvider clock)
{
    public async Task<MappingConfigurationDto> CreateDraftAsync(TenantId tenant, LegalEntityId entity, CreateMappingConfigurationDraftRequest request, Guid actor, Guid correlationId, CancellationToken ct)
    {
        var result = await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var active = await LoadActiveConfigurationAsync(context, tenant, entity, token);
            var (mode, template) = await LoadLedgerModeAsync(context, tenant, entity, token);
            var nextVersion = await ScalarLongAsync(context, "select coalesce(max(version_number),0)+1 from finance_ledger.accounting_configuration_version where tenant_id=$1 and legal_entity_id=$2", token, tenant.Value, entity.Value);
            var id = Guid.CreateVersion7();
            await ExecuteAsync(context, "insert into finance_ledger.accounting_configuration_version (tenant_id,legal_entity_id,id,version_number,based_on_configuration_version_id,effective_from,status,snapshot_hash,created_at) values ($1,$2,$3,$4,$5,$6,'Draft','', $7)", token, tenant.Value, entity.Value, id, nextVersion, active, request.EffectiveFrom, clock.GetUtcNow());
            var source = await LoadItemsAsync(context.Connection, context.Transaction, tenant, entity, active, "Active", token);
            foreach (var item in source)
            {
                var version = await ScalarLongAsync(context, "select coalesce(max(version_number),0)+1 from finance_ledger.account_mapping_version where tenant_id=$1 and legal_entity_id=$2 and mapping_id=$3", token, tenant.Value, entity.Value, item.MappingId);
                await ExecuteAsync(context, "insert into finance_ledger.account_mapping_version (tenant_id,legal_entity_id,id,mapping_id,accounting_configuration_version_id,version_number,effective_from,effective_to,product_category_code,party_group_code,tax_code,journal_code,specificity_rank,target_account_id,status) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,'Draft')", token, tenant.Value, entity.Value, Guid.CreateVersion7(), item.MappingId, id, version, item.EffectiveFrom, item.EffectiveTo, item.ProductCategoryCode, item.PartyGroupCode, item.TaxCode, item.JournalCode, item.SpecificityRank, item.TargetAccountId);
            }
            await AuditAsync(context, tenant, entity, actor, "ledger.mapping_configuration.draft_created", id, new { basedOn = active }, new { version = nextVersion, effectiveFrom = request.EffectiveFrom }, correlationId, token);
            return id;
        }, ct);
        cache.Invalidate(tenant, entity);
        return await GetConfigurationAsync(tenant, entity, result, ct);
    }

    public async Task<MappingConfigurationItemDto> UpdateDraftMappingAsync(TenantId tenant, LegalEntityId entity, Guid configurationId, Guid mappingVersionId, UpdateMappingConfigurationDraftRequest request, Guid actor, Guid correlationId, CancellationToken ct)
    {
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await EnsureDraftAsync(context, tenant, entity, configurationId, token);
            var accountIsValid = await ScalarBoolAsync(context, "select exists(select 1 from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active' and allow_direct_posting=true)", token, tenant.Value, entity.Value, request.TargetAccountId);
            if (!accountIsValid) throw new LedgerDomainException("ledger.mapping_target_invalid", "The target account must be active and allow direct posting.");
            var changed = await ExecuteAsync(context, "update finance_ledger.account_mapping_version set target_account_id=$5 where tenant_id=$1 and legal_entity_id=$2 and accounting_configuration_version_id=$3 and id=$4 and status='Draft'", token, tenant.Value, entity.Value, configurationId, mappingVersionId, request.TargetAccountId);
            if (changed != 1) throw new LedgerDomainException("ledger.mapping_draft_missing", "The draft mapping was not found.");
            await AuditAsync(context, tenant, entity, actor, "ledger.mapping_configuration.draft_mapping_changed", mappingVersionId, null, new { configurationId, targetAccountId = request.TargetAccountId }, correlationId, token);
            return true;
        }, ct);
        cache.Invalidate(tenant, entity);
        var updated = await GetConfigurationAsync(tenant, entity, configurationId, ct);
        return updated.Mappings.Single(item => item.MappingVersionId == mappingVersionId);
    }

    public async Task<MappingConfigurationValidationDto> ValidateAsync(TenantId tenant, LegalEntityId entity, Guid configurationId, Guid actor, Guid correlationId, CancellationToken ct)
    {
        var result = await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await EnsureDraftAsync(context, tenant, entity, configurationId, token);
            var validation = await ValidateDraftAsync(context, tenant, entity, configurationId, token);
            await AuditAsync(context, tenant, entity, actor, "ledger.mapping_configuration.validated", configurationId, null, new { validation.Valid, validation.Errors, validation.SnapshotHash }, correlationId, token);
            return validation;
        }, ct);
        return result;
    }

    public async Task<MappingConfigurationDto> ActivateAsync(TenantId tenant, LegalEntityId entity, Guid configurationId, Guid actor, Guid correlationId, CancellationToken ct)
    {
        var result = await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var active = await LoadActiveConfigurationAsync(context, tenant, entity, token);
            var (mode, template) = await LoadLedgerModeAsync(context, tenant, entity, token);
            var basedOn = await ScalarGuidAsync(context, "select based_on_configuration_version_id from finance_ledger.accounting_configuration_version where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft' for update", token, tenant.Value, entity.Value, configurationId)
                ?? throw new LedgerDomainException("ledger.mapping_draft_missing", "The draft configuration was not found.");
            if (basedOn != active) throw new LedgerDomainException("ledger.mapping_configuration_stale", "Another configuration was activated after this draft was created. Create a fresh draft from the current configuration.");
            var validation = await ValidateDraftAsync(context, tenant, entity, configurationId, token);
            if (!validation.Valid) throw new LedgerDomainException("ledger.mapping_configuration_invalid", string.Join("; ", validation.Errors.Select(error => error.Code)));
            await ExecuteAsync(context, "update finance_ledger.accounting_configuration_version set status='Retired' where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active'", token, tenant.Value, entity.Value, active);
            await ExecuteAsync(context, "update finance_ledger.account_mapping_version set status='Active' where tenant_id=$1 and legal_entity_id=$2 and accounting_configuration_version_id=$3 and status='Draft'", token, tenant.Value, entity.Value, configurationId);
            await ExecuteAsync(context, "update finance_ledger.accounting_configuration_version set status='Active',snapshot_hash=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft'", token, tenant.Value, entity.Value, configurationId, validation.SnapshotHash);
            var updated = await ExecuteAsync(context, "update finance_ledger.ledger_entity_configuration set active_configuration_version_id=$3,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and active_configuration_version_id=$4", token, tenant.Value, entity.Value, configurationId, active);
            if (updated != 1) throw new LedgerDomainException("ledger.mapping_configuration_stale", "The active configuration changed during activation.");
            await AuditAsync(context, tenant, entity, actor, "ledger.mapping_configuration.activated", configurationId, new { previousConfigurationVersionId = active }, new { newConfigurationVersionId = configurationId, validation = "passed", snapshotHash = validation.SnapshotHash }, correlationId, token);
            var payload = JsonSerializer.Serialize(new AccountingConfigurationActivatedEvent(configurationId, entity.Value,
                mode == LedgerMode.Managed ? LedgerModeValue.Managed : LedgerModeValue.Full, template));
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), LedgerEventTypes.AccountingConfigurationActivatedV1, payload, tenant, clock.GetUtcNow(), correlationId, configurationId, "ledger"));
            return configurationId;
        }, ct);
        cache.Invalidate(tenant, entity);
        return await GetConfigurationAsync(tenant, entity, result, ct);
    }

    public async Task<MappingConfigurationDto> GetConfigurationAsync(TenantId tenant, LegalEntityId entity, Guid configurationId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select version_number,status,effective_from,based_on_configuration_version_id from finance_ledger.accounting_configuration_version where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(configurationId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new LedgerDomainException("ledger.mapping_configuration_missing", "The accounting configuration was not found.");
        var version = reader.GetInt64(0); var status = reader.GetString(1); var from = reader.GetFieldValue<DateOnly>(2); Guid? basedOn = reader.IsDBNull(3) ? null : reader.GetGuid(3); await reader.CloseAsync();
        var mappings = await LoadItemsAsync(connection, null, tenant, entity, configurationId, null, ct);
        return new(configurationId, version, status, from, basedOn, mappings);
    }

    public async Task<MappingSimulationResult> SimulateAsync(TenantId tenant, LegalEntityId entity, Guid configurationId, MappingSimulationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RoleCode) || request.EffectiveDate == DateOnly.MinValue)
            throw new LedgerDomainException("ledger.mapping_simulation_invalid", "A role and effective date are required for mapping simulation.");
        await using var connection = await OpenAsync(tenant, ct);
        var items = await LoadItemsAsync(connection, null, tenant, entity, configurationId, null, ct);
        var candidates = items.Where(item => string.Equals(item.RoleCode, request.RoleCode.Trim(), StringComparison.OrdinalIgnoreCase)
            && item.EffectiveFrom <= request.EffectiveDate && (item.EffectiveTo is null || item.EffectiveTo >= request.EffectiveDate)
            && Matches(item.ProductCategoryCode, request.ProductCategoryCode)
            && Matches(item.PartyGroupCode, request.PartyGroupCode)
            && Matches(item.TaxCode, request.TaxCode)
            && Matches(item.JournalCode, request.JournalCode))
            .OrderByDescending(item => item.SpecificityRank).ThenBy(item => item.MappingCode, StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return new(configurationId, "NoMatch", false, null, null, null, null, null, [new("ledger.account_mapping_missing", "No mapping matched the supplied business context.", "ledger")]);
        var highest = candidates[0].SpecificityRank;
        var winners = candidates.Where(item => item.SpecificityRank == highest).ToArray();
        if (winners.Length > 1) return new(configurationId, "Conflict", false, null, null, null, null, highest, [new("ledger.account_mapping_conflict", "More than one mapping matches at the same specificity.", "ledger")]);
        var winner = winners[0];
        return new(configurationId, winner.Status, winner.AccountValid, winner.MappingVersionId, winner.MappingCode, winner.TargetAccountId, winner.AccountCode, winner.SpecificityRank,
            winner.AccountValid ? [] : [new("ledger.mapping_target_invalid", "The selected target account is inactive or not postable.", "ledger")]);
    }

    private static async Task<MappingConfigurationValidationDto> ValidateDraftAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid config, CancellationToken ct)
    {
        var errors = new List<LedgerReadinessReason>();
        var items = await LoadItemsAsync(context.Connection, context.Transaction, tenant, entity, config, "Draft", ct);
        var roles = new List<string>();
        await using (var roleCommand = new NpgsqlCommand("select code from finance_ledger.account_role where tenant_id=$1 and legal_entity_id=$2 and status='Active' order by code", context.Connection, context.Transaction))
        {
            roleCommand.Parameters.AddWithValue(tenant.Value); roleCommand.Parameters.AddWithValue(entity.Value);
            await using var roleReader = await roleCommand.ExecuteReaderAsync(ct); while (await roleReader.ReadAsync(ct)) roles.Add(roleReader.GetString(0));
        }
        foreach (var role in roles)
            if (!items.Any(item => item.RoleCode == role)) errors.Add(new("ledger.account_mapping_missing", $"Role {role} has no mapping in the proposed configuration."));
        foreach (var item in items.Where(item => !item.AccountValid)) errors.Add(new("ledger.mapping_target_invalid", $"Mapping {item.MappingCode} targets an inactive or non-postable account."));
        for (var i = 0; i < items.Count; i++)
            for (var j = i + 1; j < items.Count; j++)
            {
                var left = items[i]; var right = items[j];
                if (left.RoleCode == right.RoleCode && left.SpecificityRank == right.SpecificityRank && OverlapDates(left, right) && Overlap(left.ProductCategoryCode, right.ProductCategoryCode) && Overlap(left.PartyGroupCode, right.PartyGroupCode) && Overlap(left.TaxCode, right.TaxCode) && Overlap(left.JournalCode, right.JournalCode))
                    errors.Add(new("ledger.account_mapping_conflict", $"Mappings {left.MappingCode} and {right.MappingCode} have overlapping conditions at the same specificity."));
            }
        var hash = SnapshotHash(items);
        return new(config, errors.Count == 0, errors.DistinctBy(error => (error.Code, error.Message)).ToArray(), hash);
    }

    private static async Task<List<MappingConfigurationItemDto>> LoadItemsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, TenantId tenant, LegalEntityId entity, Guid config, string? versionStatus, CancellationToken ct)
    {
        const string sql = "select v.id,m.id,m.code,r.code,v.version_number,v.target_account_id,a.code,v.status,case when a.status='Active' and a.allow_direct_posting then 'Valid' else 'Invalid' end,v.specificity_rank,v.product_category_code,v.party_group_code,v.tax_code,v.journal_code,v.effective_from,v.effective_to from finance_ledger.account_mapping_version v join finance_ledger.account_mapping m on m.tenant_id=v.tenant_id and m.legal_entity_id=v.legal_entity_id and m.id=v.mapping_id join finance_ledger.account_role r on r.tenant_id=m.tenant_id and r.legal_entity_id=m.legal_entity_id and r.id=m.role_id join finance_ledger.account a on a.tenant_id=v.tenant_id and a.legal_entity_id=v.legal_entity_id and a.id=v.target_account_id where v.tenant_id=$1 and v.legal_entity_id=$2 and v.accounting_configuration_version_id=$3 and ($4::text is null or v.status=$4) and ($4::text is null or (m.status='Active' and r.status='Active')) order by r.code,v.specificity_rank,m.code";
        await using var command = new NpgsqlCommand(sql, connection, transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(config); command.Parameters.AddWithValue((object?)versionStatus ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct); var items = new List<MappingConfigurationItemDto>();
        while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), reader.GetGuid(5), reader.GetString(6), reader.GetString(7), reader.GetString(8) == "Valid", reader.GetInt32(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetString(13), reader.GetFieldValue<DateOnly>(14), reader.IsDBNull(15) ? null : reader.GetFieldValue<DateOnly>(15)));
        return items;
    }

    private static async Task<Guid> LoadActiveConfigurationAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        var id = await ScalarGuidAsync(context, "select active_configuration_version_id from finance_ledger.ledger_entity_configuration where tenant_id=$1 and legal_entity_id=$2 for update", ct, tenant.Value, entity.Value);
        return id ?? throw new LedgerDomainException("ledger.setup_required", "Initialize the Ledger before creating a mapping configuration.");
    }

    private static async Task<(LedgerMode Mode, string Template)> LoadLedgerModeAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select mode,setup_template_code from finance_ledger.ledger_entity_configuration where tenant_id=$1 and legal_entity_id=$2", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new LedgerDomainException("ledger.setup_required", "Initialize the Ledger before activating a mapping configuration.");
        return (Enum.Parse<LedgerMode>(reader.GetString(0)), reader.GetString(1));
    }

    private static async Task EnsureDraftAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid config, CancellationToken ct)
    {
        var exists = await ScalarBoolAsync(context, "select exists(select 1 from finance_ledger.accounting_configuration_version where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft')", ct, tenant.Value, entity.Value, config);
        if (!exists) throw new LedgerDomainException("ledger.mapping_draft_missing", "The draft configuration was not found.");
    }

    private static bool Overlap(string? first, string? second) => first is null || second is null || string.Equals(first, second, StringComparison.Ordinal);
    private static bool Matches(string? configured, string? requested) => configured is null || string.Equals(configured, requested, StringComparison.Ordinal);
    private static bool OverlapDates(MappingConfigurationItemDto first, MappingConfigurationItemDto second) =>
        (first.EffectiveTo is null || second.EffectiveFrom <= first.EffectiveTo)
        && (second.EffectiveTo is null || first.EffectiveFrom <= second.EffectiveTo);
    private static string SnapshotHash(IReadOnlyList<MappingConfigurationItemDto> items) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(items))));
    private static async Task AuditAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid actor, string action, Guid id, object? before, object? after, Guid correlation, CancellationToken ct) => await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, entity.Value, null, "User", actor, null, "ledger", action, "accounting_configuration_version", id, before is null ? null : JsonSerializer.Serialize(before), after is null ? null : JsonSerializer.Serialize(after), null, "api", correlation, null, DateTimeOffset.UtcNow, null), ct);
    private async Task<NpgsqlConnection> OpenAsync(TenantId tenant, CancellationToken ct) { var connection = await dataSource.OpenConnectionAsync(ct); await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); return connection; }
    private static async Task<Guid?> ScalarGuidAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); var value = await command.ExecuteScalarAsync(ct); return value is null or DBNull ? null : (Guid)value; }
    private static async Task<long> ScalarLongAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); return Convert.ToInt64(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture); }
    private static async Task<bool> ScalarBoolAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); return (bool)(await command.ExecuteScalarAsync(ct) ?? false); }
    private static async Task<int> ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); return await command.ExecuteNonQueryAsync(ct); }
    private static void Add(NpgsqlCommand command, params object?[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }
}
