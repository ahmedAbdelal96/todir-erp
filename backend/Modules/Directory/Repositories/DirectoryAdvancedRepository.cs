using System.Globalization;
using System.Text;
using System.Text.Json;
using Npgsql;
using Modules.Directory.DTOs;
using Modules.Directory.Entities;
using Shared.Primitives.Identifiers;

namespace Modules.Directory.Repositories;

/// <summary>
/// Owns Directory capabilities that span more than the basic party aggregate.
/// It deliberately uses SQL against Directory-owned tables only; consuming modules
/// integrate through the public contracts and events rather than table access.
/// </summary>
public sealed class DirectoryAdvancedRepository(
    NpgsqlDataSource dataSource,
    DirectoryRepository parties,
    IEnumerable<IPartyMergeContributor> mergeContributors,
    IEnumerable<IPartyReadinessRule> readinessRules)
{
    public async Task<PartyDetails?> GetVisibleAsync(TenantId tenantId, Guid partyId, Guid? legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenantId, ct);
        var canonical = await ResolveCanonicalAsync(connection, transaction, tenantId, partyId, ct);
        if (canonical is null || !await IsVisibleAsync(connection, transaction, tenantId, canonical.Value, legalEntityId, ct))
        {
            await transaction.RollbackAsync(ct);
            return null;
        }
        await transaction.CommitAsync(ct);
        var details = await parties.GetAsync(tenantId, canonical.Value, true, ct);
        return details is null ? null : await EnrichAsync(tenantId, details, ct);
    }

    public async Task<PagedParties> ListVisibleAsync(TenantId tenantId, string? search, PartyType? partyType, PartyRoleCode? role, PartyStatus? status, Guid? legalEntityId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var normalized = DirectoryNormalization.Name(search);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenantId, ct);
        const string filter = "where p.tenant_id=$1 and (p.status <> 'Merged') and ($2='' or p.display_name_normalized like '%' || $2 || '%' or p.legal_name ilike '%' || $2 || '%' or p.trade_name ilike '%' || $2 || '%' or p.name_ar ilike '%' || $2 || '%' or p.name_en ilike '%' || $2 || '%' or exists (select 1 from foundation_directory.party_alias a where a.tenant_id=p.tenant_id and a.party_id=p.id and a.alias_normalized like '%' || $2 || '%') or exists (select 1 from foundation_directory.party_identifier i where i.tenant_id=p.tenant_id and i.party_id=p.id and i.normalized_value like '%' || $2 || '%') or exists (select 1 from foundation_directory.contact_point c where c.tenant_id=p.tenant_id and (c.party_id=p.id or exists (select 1 from foundation_directory.party_contact pc where pc.tenant_id=p.tenant_id and pc.party_id=p.id and pc.contact_person_id=c.contact_person_id)) and c.normalized_value like '%' || $2 || '%') or exists (select 1 from foundation_directory.party_contact pc join foundation_directory.contact_person cp on cp.tenant_id=pc.tenant_id and cp.id=pc.contact_person_id where pc.tenant_id=p.tenant_id and pc.party_id=p.id and cp.display_name_normalized like '%' || $2 || '%')) and ($3='' or p.party_type=$3) and ($4='' or exists (select 1 from foundation_directory.party_role r where r.tenant_id=p.tenant_id and r.party_id=p.id and r.role_code=$4 and r.status='Active')) and ($5='' or p.status=$5) and exists (select 1 from foundation_directory.party_visibility v where v.tenant_id=p.tenant_id and v.party_id=p.id and (v.scope_type='TenantWide' or (cast($6 as uuid) is not null and v.scope_type in ('SelectedLegalEntities','SingleLegalEntity','LegalEntity') and v.legal_entity_id=cast($6 as uuid))) )";
        await using var count = new NpgsqlCommand("select count(*) from foundation_directory.party p " + filter, connection, transaction);
        Add(count, tenantId.Value, normalized, partyType?.ToString() ?? "", role?.ToString() ?? "", status?.ToString() ?? "", legalEntityId ?? (object)DBNull.Value);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select p.id,p.party_type,p.status,p.display_name,p.legal_name,p.updated_at,p.version,coalesce(array_agg(r.role_code) filter (where r.status='Active'),'{}') from foundation_directory.party p left join foundation_directory.party_role r on r.tenant_id=p.tenant_id and r.party_id=p.id " + filter + " group by p.id,p.party_type,p.status,p.display_name,p.legal_name,p.updated_at,p.version order by p.updated_at desc,p.id limit $7 offset $8", connection, transaction);
        Add(command, tenantId.Value, normalized, partyType?.ToString() ?? "", role?.ToString() ?? "", status?.ToString() ?? "", legalEntityId ?? (object)DBNull.Value, pageSize, (page - 1) * pageSize);
        var items = new List<PartySummary>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), Enum.Parse<PartyType>(reader.GetString(1)), Enum.Parse<PartyStatus>(reader.GetString(2)), reader.GetString(3), Text(reader, 4), reader.GetFieldValue<string[]>(7).Select(code => new PartyRoleDto(code, "Active", 1)).ToArray(), reader.GetFieldValue<DateTimeOffset>(5), reader.GetInt64(6)));
        await reader.CloseAsync();
        await transaction.CommitAsync(ct);
        return new(items, total, page, pageSize);
    }

    public async Task<PartyDetails> AddContactPersonAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, ContactPersonInput input, ContactPersonLinkInput link, Guid correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.DisplayName)) throw new DirectoryException("directory.contact_person_name_required", "Contact person name is required.");
        var contactId = Guid.CreateVersion7();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenantId, ct);
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(connection, transaction, "insert into foundation_directory.contact_person (tenant_id,id,display_name,display_name_normalized,job_title,created_at,updated_at,first_name,last_name,department,language_code,status,version) values ($1,$2,$3,$4,$5,$6,$6,$7,$8,$9,$10,'Active',1)", ct, tenantId.Value, contactId, input.DisplayName.Trim(), DirectoryNormalization.Name(input.DisplayName), Null(input.JobTitle), now, Null(input.FirstName), Null(input.LastName), Null(input.Department), Null(input.LanguageCode));
        await ExecuteAsync(connection, transaction, "insert into foundation_directory.party_contact (tenant_id,party_id,contact_person_id,relationship_type,is_primary,usage,status) values ($1,$2,$3,$4,$5,$6,'Active') on conflict (tenant_id,party_id,contact_person_id) do update set relationship_type=excluded.relationship_type,is_primary=excluded.is_primary,usage=excluded.usage,status='Active'", ct, tenantId.Value, partyId, contactId, link.Usage, link.IsPrimary, link.Usage);
        await AuditAsync(connection, transaction, tenantId, actorId, membershipId, partyId, "party_contact_person_added", null, new { contactId, usage = link.Usage }, correlationId, now, ct);
        await OutboxAsync(connection, transaction, tenantId, "DirectoryPartyContactChangedV1", new { partyId, contactId }, correlationId, partyId, ct);
        await transaction.CommitAsync(ct);
        return await parties.GetAsync(tenantId, partyId, true, ct) is { } details ? await EnrichAsync(tenantId, details, ct) : throw new DirectoryException("directory.party_not_found", "Party was not found.");
    }

    public async Task<PartyDetails> AddAliasAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, AliasInput input, Guid correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Alias)) throw new DirectoryException("directory.alias_required", "Alias is required.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenantId, ct);
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(connection, transaction, "insert into foundation_directory.party_alias (tenant_id,id,party_id,alias,alias_normalized,alias_type,language_code,created_at) values ($1,$2,$3,$4,$5,$6,$7,$8)", ct, tenantId.Value, Guid.CreateVersion7(), partyId, input.Alias.Trim(), DirectoryNormalization.Name(input.Alias), input.AliasType, Null(input.LanguageCode), now);
        await AuditAsync(connection, transaction, tenantId, actorId, membershipId, partyId, "party_alias_added", null, new { alias = input.Alias, languageCode = input.LanguageCode }, correlationId, now, ct);
        await OutboxAsync(connection, transaction, tenantId, "DirectoryPartyAliasChangedV1", new { partyId, alias = input.Alias }, correlationId, partyId, ct);
        await transaction.CommitAsync(ct);
        return await parties.GetAsync(tenantId, partyId, true, ct) is { } details ? await EnrichAsync(tenantId, details, ct) : throw new DirectoryException("directory.party_not_found", "Party was not found.");
    }

    public async Task<PartyDetails> SetVisibilityAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, VisibilityInput input, Guid correlationId, CancellationToken ct)
    {
        if (input.ScopeType is not ("TenantWide" or "SelectedLegalEntities" or "SingleLegalEntity" or "LegalEntity" or "Branch")) throw new DirectoryException("directory.visibility_scope_invalid", "Visibility scope is not supported.");
        if (input.ScopeType is "SelectedLegalEntities" or "SingleLegalEntity" or "LegalEntity" && input.LegalEntityId is null) throw new DirectoryException("directory.visibility_entity_required", "A legal entity is required for this visibility scope.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenantId, ct);
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(connection, transaction, "delete from foundation_directory.party_visibility where tenant_id=$1 and party_id=$2", ct, tenantId.Value, partyId);
        await ExecuteAsync(connection, transaction, "insert into foundation_directory.party_visibility (tenant_id,visibility_id,party_id,scope_type,legal_entity_id) values ($1,$2,$3,$4,$5)", ct, tenantId.Value, Guid.CreateVersion7(), partyId, input.ScopeType, input.LegalEntityId ?? (object)DBNull.Value);
        await AuditAsync(connection, transaction, tenantId, actorId, membershipId, partyId, "party_visibility_changed", null, input, correlationId, now, ct);
        await OutboxAsync(connection, transaction, tenantId, "DirectoryPartyVisibilityChangedV1", new { partyId, scopeType = input.ScopeType, legalEntityId = input.LegalEntityId }, correlationId, partyId, ct);
        await transaction.CommitAsync(ct);
        return await parties.GetAsync(tenantId, partyId, true, ct) is { } details ? await EnrichAsync(tenantId, details, ct) : throw new DirectoryException("directory.party_not_found", "Party was not found.");
    }

    public async Task<DirectoryMergeResult> MergeAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid sourcePartyId, MergePartyRequest request, Guid correlationId, CancellationToken ct)
    {
        if (!request.ConfirmIrreversible || string.IsNullOrWhiteSpace(request.Reason)) throw new DirectoryException("directory.merge_confirmation_required", "Merge requires an explicit confirmation and reason.");
        if (sourcePartyId == request.MasterPartyId) throw new DirectoryException("directory.merge_same_party", "A party cannot be merged into itself.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenantId, ct);
        await LockPartyAsync(connection, transaction, tenantId, sourcePartyId, ct);
        await LockPartyAsync(connection, transaction, tenantId, request.MasterPartyId, ct);
        var sourceStatus = await ScalarAsync<string?>(connection, transaction, "select status from foundation_directory.party where tenant_id=$1 and id=$2", ct, tenantId.Value, sourcePartyId);
        var masterStatus = await ScalarAsync<string?>(connection, transaction, "select status from foundation_directory.party where tenant_id=$1 and id=$2", ct, tenantId.Value, request.MasterPartyId);
        if (sourceStatus is null || masterStatus is null) throw new DirectoryException("directory.party_not_found", "Both source and master parties must exist.");
        if (sourceStatus == "Merged") throw new DirectoryException("directory.party_already_merged", "The source party is already merged.");
        if (masterStatus is "Archived" or "Merged") throw new DirectoryException("directory.merge_master_inactive", "The master party must be active.");
        var conflict = await ScalarAsync<string?>(connection, transaction, "select i.identifier_type || ':' || i.normalized_value from foundation_directory.party_identifier i join foundation_directory.party_identifier m on m.tenant_id=i.tenant_id and m.identifier_type=i.identifier_type and m.normalized_value=i.normalized_value and m.party_id=$3 where i.tenant_id=$1 and i.party_id=$2 limit 1", ct, tenantId.Value, sourcePartyId, request.MasterPartyId);
        if (conflict is not null) throw new DirectoryException("directory.merge_identifier_conflict", $"The parties have a conflicting identifier: {conflict}.");
        foreach (var contributor in mergeContributors)
        {
            var issues = await contributor.ValidateAsync(tenantId.Value, sourcePartyId, request.MasterPartyId, ct);
            if (issues.Count > 0) throw new DirectoryException("directory.merge_blocked_by_contributor", string.Join("; ", issues));
        }
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(connection, transaction, "insert into foundation_directory.party_role (tenant_id,party_id,role_code,status,created_by,created_at,updated_at,version,active_from,active_to) select tenant_id,$3,role_code,status,created_by,created_at,updated_at,version,active_from,active_to from foundation_directory.party_role where tenant_id=$1 and party_id=$2 on conflict (tenant_id,party_id,role_code) do update set status='Active',updated_at=$4,version=foundation_directory.party_role.version+1", ct, tenantId.Value, sourcePartyId, request.MasterPartyId, now);
        await ExecuteAsync(connection, transaction, "insert into foundation_directory.party_contact (tenant_id,party_id,contact_person_id,relationship_type,is_primary,usage,valid_from,valid_to,status) select tenant_id,$3,contact_person_id,relationship_type,is_primary,usage,valid_from,valid_to,status from foundation_directory.party_contact where tenant_id=$1 and party_id=$2 on conflict (tenant_id,party_id,contact_person_id) do update set is_primary=foundation_directory.party_contact.is_primary or excluded.is_primary,usage=excluded.usage,status='Active'", ct, tenantId.Value, sourcePartyId, request.MasterPartyId);
        await ExecuteAsync(connection, transaction, "delete from foundation_directory.party_contact where tenant_id=$1 and party_id=$2", ct, tenantId.Value, sourcePartyId);
        await ExecuteAsync(connection, transaction, "update foundation_directory.party_identifier set party_id=$3 where tenant_id=$1 and party_id=$2", ct, tenantId.Value, sourcePartyId, request.MasterPartyId);
        await ExecuteAsync(connection, transaction, "update foundation_directory.contact_point set party_id=$3 where tenant_id=$1 and party_id=$2", ct, tenantId.Value, sourcePartyId, request.MasterPartyId);
        await ExecuteAsync(connection, transaction, "insert into foundation_directory.party_address (tenant_id,party_id,address_id,usage,is_default,valid_from,valid_to) select tenant_id,$3,address_id,usage,is_default,valid_from,valid_to from foundation_directory.party_address where tenant_id=$1 and party_id=$2 on conflict do nothing", ct, tenantId.Value, sourcePartyId, request.MasterPartyId);
        await ExecuteAsync(connection, transaction, "delete from foundation_directory.party_address where tenant_id=$1 and party_id=$2", ct, tenantId.Value, sourcePartyId);
        await ExecuteAsync(connection, transaction, "update foundation_directory.party_alias set party_id=$3 where tenant_id=$1 and party_id=$2", ct, tenantId.Value, sourcePartyId, request.MasterPartyId);
        await ExecuteAsync(connection, transaction, "insert into foundation_directory.party_merge_redirect (tenant_id,source_party_id,target_party_id,merged_at,merged_by,reason,conflict_details) values ($1,$2,$3,$4,$5,$6,'{}'::jsonb)", ct, tenantId.Value, sourcePartyId, request.MasterPartyId, now, actorId, request.Reason.Trim());
        await ExecuteAsync(connection, transaction, "update foundation_directory.party set status='Merged',updated_at=$3,updated_by=$4,version=version+1 where tenant_id=$1 and id=$2", ct, tenantId.Value, sourcePartyId, now, actorId);
        await ExecuteAsync(connection, transaction, "update foundation_directory.duplicate_candidate set status='Resolved',resolved_at=$3,resolved_by=$4,resolution_reason=$5 where tenant_id=$1 and (party_id=$2 or candidate_party_id=$2)", ct, tenantId.Value, sourcePartyId, now, actorId, "Merged into master party");
        await AuditAsync(connection, transaction, tenantId, actorId, membershipId, sourcePartyId, "party_merged", new { sourcePartyId }, new { request.MasterPartyId, request.Reason }, correlationId, now, ct);
        await OutboxAsync(connection, transaction, tenantId, "DirectoryPartyMergedV1", new { sourcePartyId, masterPartyId = request.MasterPartyId }, correlationId, request.MasterPartyId, ct);
        await transaction.CommitAsync(ct);
        foreach (var contributor in mergeContributors) await contributor.ContributeAsync(tenantId.Value, sourcePartyId, request.MasterPartyId, ct);
        return new(request.MasterPartyId, sourcePartyId, "Merged", mergeContributors.Select(x => x.Name).ToArray());
    }

    public async Task<PartyReadinessDto?> ReadinessAsync(TenantId tenantId, Guid partyId, CancellationToken ct)
    {
        var party = await parties.GetAsync(tenantId, partyId, true, ct);
        if (party is null) return null;
        party = await EnrichAsync(tenantId, party, ct);
        var capabilities = new List<ReadinessCapabilityDto>();
        capabilities.Add(new("CanSell", party.Roles.Any(x => x.Code == nameof(PartyRoleCode.Customer) && x.Status == "Active"), party.Roles.Any(x => x.Code == nameof(PartyRoleCode.Customer) && x.Status == "Active") ? [] : ["Customer role"], []));
        capabilities.Add(new("CanPurchase", party.Roles.Any(x => x.Code == nameof(PartyRoleCode.Supplier) && x.Status == "Active"), party.Roles.Any(x => x.Code == nameof(PartyRoleCode.Supplier) && x.Status == "Active") ? [] : ["Supplier role"], []));
        var hasLegalIdentifier = party.Identifiers.Any(x => x.Type.Contains("Tax", StringComparison.OrdinalIgnoreCase) || x.Type.Contains("Commercial", StringComparison.OrdinalIgnoreCase) || x.Type.Contains("Registration", StringComparison.OrdinalIgnoreCase));
        capabilities.Add(new("CanInvoice", party.Roles.Any(x => x.Code == nameof(PartyRoleCode.Customer) && x.Status == "Active") && hasLegalIdentifier, [.. (!party.Roles.Any(x => x.Code == nameof(PartyRoleCode.Customer) && x.Status == "Active") ? ["Customer role"] : Array.Empty<string>()), .. (!hasLegalIdentifier ? ["Legal identifier"] : Array.Empty<string>())], []));
        capabilities.Add(new("CanPay", party.Roles.Any(x => x.Code == nameof(PartyRoleCode.Supplier) && x.Status == "Active"), party.Roles.Any(x => x.Code == nameof(PartyRoleCode.Supplier) && x.Status == "Active") ? [] : ["Supplier role"], []));
        foreach (var rule in readinessRules) capabilities.Add(await rule.EvaluateAsync(tenantId.Value, party, ct));
        var missing = capabilities.Where(x => !x.Ready).SelectMany(x => x.Missing).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var warnings = capabilities.SelectMany(x => x.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(party.Id, missing.Length == 0, missing, warnings, capabilities);
    }

    public async Task<IReadOnlyList<AuditEntryDto>> AuditAsync(TenantId tenantId, Guid partyId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenantId, ct);
        await using var command = new NpgsqlCommand("select id,action,entity_type,entity_id,reason,occurred_at,after_json from platform.audit_entry where tenant_id=$1 and module='directory' and entity_id=$2 order by occurred_at desc", connection, transaction);
        Add(command, tenantId.Value, partyId);
        var result = new List<AuditEntryDto>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3), Text(reader, 4), reader.GetFieldValue<DateTimeOffset>(5), Text(reader, 6)));
        await reader.CloseAsync();
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<string> ExportCsvAsync(TenantId tenantId, string? search, bool includeArchived, Guid? legalEntityId, CancellationToken ct)
    {
        var builder = new StringBuilder("Id,PartyType,Status,DisplayName,LegalName,Roles\r\n");
        for (var pageNumber = 1; ; pageNumber++)
        {
            var page = await ListVisibleAsync(tenantId, search, null, null, includeArchived ? null : PartyStatus.Active, legalEntityId, pageNumber, 100, ct);
            foreach (var item in page.Items) builder.AppendLine(string.Join(',', Quote(item.Id), Quote(item.PartyType.ToString()), Quote(item.Status.ToString()), Quote(item.DisplayName), Quote(item.LegalName), Quote(string.Join('|', item.Roles.Select(x => x.Code)))));
            if (page.Items.Count == 0 || pageNumber * page.Items.Count >= page.TotalCount) break;
        }
        return builder.ToString();
    }

    public async Task<DirectoryImportResult> ImportAsync(TenantId tenantId, Guid actorId, Guid membershipId, DirectoryImportRequest request, string idempotencyKey, Guid correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new DirectoryException("directory.import_idempotency_required", "Import requires an Idempotency-Key.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenantId, ct);
        var existing = await JsonScalarAsync(connection, transaction, "select response_json from foundation_directory.import_operation where tenant_id=$1 and idempotency_key=$2", ct, tenantId.Value, idempotencyKey.Trim());
        if (existing is not null) return JsonSerializer.Deserialize<DirectoryImportResult>(existing) ?? throw new DirectoryException("directory.import_result_invalid", "Stored import result is invalid.");
        var operationId = Guid.CreateVersion7();
        var results = new List<DirectoryImportRowResult>();
        foreach (var row in request.Rows)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(row.ExternalReference)) errors.Add("external_reference_required");
            if (string.IsNullOrWhiteSpace(row.DisplayName)) errors.Add("display_name_required");
            var matches = await FindMatchesAsync(connection, transaction, tenantId, row, ct);
            var action = request.Commit ? (matches.Count == 1 ? "Updated" : matches.Count > 1 ? "Review" : "Created") : (matches.Count == 0 ? "Create" : matches.Count == 1 ? "Update" : "Review");
            if (errors.Count > 0 || !request.Commit || action == "Review")
            {
                results.Add(new(row.ExternalReference, errors.Count > 0 ? "Error" : action, matches.Count == 1 ? matches[0] : null, errors, matches));
                continue;
            }
            var partyId = matches.Count == 1 ? matches[0] : Guid.CreateVersion7();
            if (matches.Count == 1)
            {
                await ExecuteAsync(connection, transaction, "update foundation_directory.party set display_name=$3,display_name_normalized=$4,legal_name=$5,trade_name=$6,name_ar=$7,name_en=$8,updated_at=$9,version=version+1 where tenant_id=$1 and id=$2", ct, tenantId.Value, partyId, row.DisplayName.Trim(), DirectoryNormalization.Name(row.DisplayName), Null(row.LegalName), Null(row.TradeName), Null(row.NameAr), Null(row.NameEn), DateTimeOffset.UtcNow);
            }
            else
            {
                await ExecuteAsync(connection, transaction, "insert into foundation_directory.party (tenant_id,id,party_type,status,display_name,display_name_normalized,legal_name,trade_name,name_ar,name_en,country_code,created_by,updated_by,created_at,updated_at,version) values ($1,$2,$3,'Active',$4,$5,$6,$7,$8,$9,$10,$11,$11,$12,$12,1)", ct, tenantId.Value, partyId, row.PartyType.ToString(), row.DisplayName.Trim(), DirectoryNormalization.Name(row.DisplayName), Null(row.LegalName), Null(row.TradeName), Null(row.NameAr), Null(row.NameEn), Null(row.CountryCode), actorId, DateTimeOffset.UtcNow);
                var role = Enum.TryParse<PartyRoleCode>(row.Role, true, out var parsedRole) ? parsedRole : PartyRoleCode.Customer;
                await ExecuteAsync(connection, transaction, "insert into foundation_directory.party_role (tenant_id,party_id,role_code,status,created_by,created_at,updated_at,version,active_from) values ($1,$2,$3,'Active',$4,$5,$5,1,$5::date) on conflict do nothing", ct, tenantId.Value, partyId, role.ToString(), actorId, DateTimeOffset.UtcNow);
                await ExecuteAsync(connection, transaction, "insert into foundation_directory.party_visibility (tenant_id,visibility_id,party_id,scope_type) values ($1,$2,$3,'TenantWide') on conflict do nothing", ct, tenantId.Value, Guid.CreateVersion7(), partyId);
            }
            if (!string.IsNullOrWhiteSpace(row.IdentifierValue) && !string.IsNullOrWhiteSpace(row.IdentifierType)) await ExecuteAsync(connection, transaction, "insert into foundation_directory.party_identifier (tenant_id,id,party_id,identifier_type,country_code,value,normalized_value,is_primary,verification_status,created_at) values ($1,$2,$3,$4,$5,$6,$7,true,'Unverified',$8) on conflict (tenant_id,party_id,identifier_type,normalized_value) do nothing", ct, tenantId.Value, Guid.CreateVersion7(), partyId, row.IdentifierType, Null(row.IdentifierCountryCode), row.IdentifierValue.Trim(), DirectoryNormalization.Identifier(row.IdentifierValue), DateTimeOffset.UtcNow);
            if (!string.IsNullOrWhiteSpace(row.Email)) await ExecuteAsync(connection, transaction, "insert into foundation_directory.contact_point (tenant_id,id,party_id,contact_type,value,normalized_value,is_primary,created_at,status) values ($1,$2,$3,'Email',$4,$5,true,$6,'Active')", ct, tenantId.Value, Guid.CreateVersion7(), partyId, row.Email.Trim(), DirectoryNormalization.Email(row.Email), DateTimeOffset.UtcNow);
            if (!string.IsNullOrWhiteSpace(row.Phone)) await ExecuteAsync(connection, transaction, "insert into foundation_directory.contact_point (tenant_id,id,party_id,contact_type,value,normalized_value,is_primary,created_at,status) values ($1,$2,$3,'Phone',$4,$5,true,$6,'Active')", ct, tenantId.Value, Guid.CreateVersion7(), partyId, row.Phone.Trim(), DirectoryNormalization.Phone(row.Phone), DateTimeOffset.UtcNow);
            if (!string.IsNullOrWhiteSpace(row.AddressLine1))
            {
                var addressId = Guid.CreateVersion7();
                await ExecuteAsync(connection, transaction, "insert into foundation_directory.address (tenant_id,id,country_code,city,region,postal_code,line1,created_at,updated_at,status,version) values ($1,$2,$3,$4,$5,$6,$7,$8,$8,'Active',1)", ct, tenantId.Value, addressId, Null(row.CountryCode), Null(row.City), Null(row.Region), Null(row.PostalCode), row.AddressLine1.Trim(), DateTimeOffset.UtcNow);
                await ExecuteAsync(connection, transaction, "insert into foundation_directory.party_address (tenant_id,party_id,address_id,usage,is_default) values ($1,$2,$3,$4,true) on conflict do nothing", ct, tenantId.Value, partyId, addressId, row.AddressUsage ?? "General");
            }
            await ExecuteAsync(connection, transaction, "insert into foundation_directory.external_reference (tenant_id,id,party_id,system_code,external_key,created_at) values ($1,$2,$3,'directory-import',$4,$5) on conflict (tenant_id,system_code,external_key) do update set party_id=excluded.party_id", ct, tenantId.Value, Guid.CreateVersion7(), partyId, row.ExternalReference.Trim(), DateTimeOffset.UtcNow);
            results.Add(new(row.ExternalReference, action, partyId, [], []));
        }
        var result = new DirectoryImportResult(operationId, request.Commit, results);
        var json = JsonSerializer.Serialize(result);
        await ExecuteAsync(connection, transaction, "insert into foundation_directory.import_operation (tenant_id,id,idempotency_key,operation_type,response_json,created_at,completed_at) values ($1,$2,$3,'DirectoryPartyImport',$4::jsonb,$5,$5)", ct, tenantId.Value, operationId, idempotencyKey.Trim(), json, DateTimeOffset.UtcNow);
        await OutboxAsync(connection, transaction, tenantId, "DirectoryPartyImportCompletedV1", new { operationId, committed = request.Commit, count = results.Count }, correlationId, operationId, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private async Task<PartyDetails> EnrichAsync(TenantId tenantId, PartyDetails details, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenantId, ct);
        var persons = new List<ContactPersonDto>();
        await using (var command = new NpgsqlCommand("select cp.id,cp.display_name,cp.job_title,cp.department,cp.status,coalesce(array_agg(pc.usage order by pc.usage),'{}') from foundation_directory.party_contact pc join foundation_directory.contact_person cp on cp.tenant_id=pc.tenant_id and cp.id=pc.contact_person_id where pc.tenant_id=$1 and pc.party_id=$2 and pc.status='Active' group by cp.id,cp.display_name,cp.job_title,cp.department,cp.status", connection, transaction))
        {
            Add(command, tenantId.Value, details.Id);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) persons.Add(new(reader.GetGuid(0), reader.GetString(1), Text(reader, 2), Text(reader, 3), reader.GetString(4), reader.GetFieldValue<string[]>(5)));
        }
        var aliases = new List<PartyAliasDto>();
        await using (var command = new NpgsqlCommand("select id,alias,language_code,alias_type from foundation_directory.party_alias where tenant_id=$1 and party_id=$2 order by alias", connection, transaction))
        {
            Add(command, tenantId.Value, details.Id);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) aliases.Add(new(reader.GetGuid(0), reader.GetString(1), Text(reader, 2), reader.GetString(3)));
        }
        var visibility = new List<PartyVisibilityDto>();
        await using (var command = new NpgsqlCommand("select visibility_id,scope_type,legal_entity_id from foundation_directory.party_visibility where tenant_id=$1 and party_id=$2", connection, transaction))
        {
            Add(command, tenantId.Value, details.Id);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) visibility.Add(new(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetGuid(2)));
        }
        await transaction.CommitAsync(ct);
        return details with { ContactPersons = persons, Aliases = aliases, Visibility = visibility };
    }

    private static async Task<Guid?> ResolveCanonicalAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenantId, Guid partyId, CancellationToken ct)
    {
        var current = partyId;
        for (var i = 0; i < 10; i++)
        {
            var next = await ScalarAsync<Guid?>(c, t, "select target_party_id from foundation_directory.party_merge_redirect where tenant_id=$1 and source_party_id=$2", ct, tenantId.Value, current);
            if (next is null) return current;
            current = next.Value;
        }
        throw new DirectoryException("directory.merge_redirect_cycle", "Party merge redirects contain a cycle.");
    }

    private static async Task<bool> IsVisibleAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenantId, Guid partyId, Guid? legalEntityId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from foundation_directory.party_visibility where tenant_id=$1 and party_id=$2 and (scope_type='TenantWide' or (cast($3 as uuid) is not null and scope_type in ('SelectedLegalEntities','SingleLegalEntity','LegalEntity') and legal_entity_id=cast($3 as uuid))))", c, t);
        Add(command, tenantId.Value, partyId, legalEntityId ?? (object)DBNull.Value);
        return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
    }

    private static async Task<IReadOnlyList<Guid>> FindMatchesAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenantId, DirectoryImportRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select distinct p.id from foundation_directory.party p left join foundation_directory.party_identifier i on i.tenant_id=p.tenant_id and i.party_id=p.id where p.tenant_id=$1 and (($2<>'' and exists (select 1 from foundation_directory.external_reference e where e.tenant_id=p.tenant_id and e.party_id=p.id and e.system_code='directory-import' and e.external_key=$2)) or ($3<>'' and i.identifier_type=$4 and i.normalized_value=$5)) order by p.id", c, t);
        Add(command, tenantId.Value, row.ExternalReference ?? "", row.IdentifierValue ?? "", row.IdentifierType ?? "", DirectoryNormalization.Identifier(row.IdentifierValue ?? ""));
        var list = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) list.Add(reader.GetGuid(0));
        return list;
    }

    private static async Task LockPartyAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenantId, Guid partyId, CancellationToken ct) => await ExecuteAsync(c, t, "select 1 from foundation_directory.party where tenant_id=$1 and id=$2 for update", ct, tenantId.Value, partyId);
    private static async Task<string?> JsonScalarAsync(NpgsqlConnection c, NpgsqlTransaction t, string sql, CancellationToken ct, params object?[] values) => (await ScalarAsync<object?>(c, t, sql, ct, values)) is { } value && value is not DBNull ? value.ToString() : null;
    private static async Task<T?> ScalarAsync<T>(NpgsqlConnection c, NpgsqlTransaction t, string sql, CancellationToken ct, params object?[] values)
    {
        await using var command = new NpgsqlCommand(sql, c, t); Add(command, values); var result = await command.ExecuteScalarAsync(ct);
        if (result is null or DBNull) return default;
        if (typeof(T) == typeof(Guid?) || typeof(T) == typeof(Guid)) return (T?)(object)(result is Guid g ? g : Guid.Parse(result.ToString()!));
        return (T?)Convert.ChangeType(result, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), CultureInfo.InvariantCulture);
    }
    private static async Task<int> ExecuteAsync(NpgsqlConnection c, NpgsqlTransaction t, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, c, t); Add(command, values); return await command.ExecuteNonQueryAsync(ct); }
    private static async Task SetTenantAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenantId, CancellationToken ct) => await ExecuteAsync(c, t, "select set_config('app.tenant_id',$1,true)", ct, tenantId.Value.ToString("D"));
    private static async Task AuditAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenantId, Guid actorId, Guid membershipId, Guid entityId, string action, object? before, object? after, Guid correlationId, DateTimeOffset now, CancellationToken ct) => await Core.Services.NpgsqlAuditWriter.WriteAsync(c, t, new Core.Services.AuditEntry(Guid.CreateVersion7(), tenantId, null, null, "Human", actorId, membershipId, "directory", action, "party", entityId, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after), null, "directory-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), ct);
    private static async Task OutboxAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenantId, string messageType, object payload, Guid correlationId, Guid causationId, CancellationToken ct) => await ExecuteAsync(c, t, "insert into platform.outbox_message (id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,trace_parent,status,next_attempt_at) values ($1,$2,$3::jsonb,$4,$5,$6,$7,'directory',$8,'Pending',$5)", ct, Guid.CreateVersion7(), messageType, JsonSerializer.Serialize(payload), tenantId.Value, DateTimeOffset.UtcNow, correlationId, causationId, System.Diagnostics.Activity.Current?.Id);
    private static void Add(NpgsqlCommand command, params object?[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Text(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static string Quote(object? value) => $"\"{(value?.ToString() ?? string.Empty).Replace("\"", "\"\"")}\"";
}
