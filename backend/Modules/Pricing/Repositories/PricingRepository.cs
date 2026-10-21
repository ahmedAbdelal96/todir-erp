using System.Globalization;
using System.Text.Json;
using Npgsql;
using Core.Transactions;
using Modules.Pricing.DTOs;
using Modules.Pricing.Entities;
using Shared.Contracts.Pricing;
using Shared.Primitives.Identifiers;

namespace Modules.Pricing.Repositories;

public sealed class PricingRepository(NpgsqlDataSource dataSource, ITransactionCoordinator transactions)
{
    private const int MaximumCandidateRows = 5000;

    public async Task<PriceListDto> CreateListAsync(TenantId tenant, Guid actorId, Guid membershipId, CreatePriceListRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        ValidateCode(request.Code, "pricing.list_code_invalid");
        var currency = NormalizeCurrency(request.CurrencyCode);
        var listId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using (var command = new NpgsqlCommand("insert into foundation_pricing.price_list(tenant_id,id,legal_entity_id,code,name,direction,currency_code,currency_policy,status,created_at,updated_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,'Active',$9,$9,1)", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, listId, request.LegalEntityId, request.Code.Trim().ToUpperInvariant(), RequiredText(request.Name, 160, "pricing.list_name_invalid"), request.Direction.ToString(), currency, request.CurrencyPolicy.ToString(), now); await command.ExecuteNonQueryAsync(token);
            }
            await using (var version = new NpgsqlCommand("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,version) values($1,$2,$3,1,'Draft',$4,$5,$6,$7,1)", context.Connection, context.Transaction))
            {
                Add(version, tenant.Value, versionId, listId, request.EffectiveFrom, DBNull.Value, actorId, now); await version.ExecuteNonQueryAsync(token);
            }
            if (request.IsDefault)
                await UpsertAssignmentAsync(context.Connection, context.Transaction, tenant.Value, request.LegalEntityId, request.Direction, listId, null, null, true, request.EffectiveFrom, token);
            await AuditAsync(context.Connection, context.Transaction, tenant.Value, request.LegalEntityId, listId, versionId, actorId, membershipId, "price_list_created", correlationId, now, new { request.Direction, currency }, token);
            return new PriceListDto(listId, request.LegalEntityId, request.Code.Trim().ToUpperInvariant(), request.Name.Trim(), request.Direction.ToString(), currency, request.CurrencyPolicy.ToString(), "Active", 1, null);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<PriceListDto>> ListAsync(TenantId tenant, Guid legalEntityId, CancellationToken cancellationToken)
    {
        await using var scope = await OpenTenantScopeAsync(tenant, cancellationToken);
        const string sql = "select l.id,l.legal_entity_id,l.code,l.name,l.direction,l.currency_code,l.currency_policy,l.status,l.version,(select v.id from foundation_pricing.price_list_version v where v.tenant_id=l.tenant_id and v.price_list_id=l.id and v.status='Active' order by v.effective_from desc limit 1) from foundation_pricing.price_list l where l.tenant_id=$1 and l.legal_entity_id=$2 order by l.direction,l.code limit 200";
        await using var command = new NpgsqlCommand(sql, scope.Connection, scope.Transaction); Add(command, tenant.Value, legalEntityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var result = new List<PriceListDto>();
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetInt64(8), reader.IsDBNull(9) ? null : reader.GetGuid(9)));
        return result;
    }

    public async Task<PriceVersionDto> CreateVersionAsync(TenantId tenant, Guid legalEntityId, Guid actorId, Guid membershipId, Guid listId, CreatePriceVersionRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        if (request.EffectiveTo is { } end && end <= request.EffectiveFrom) throw new PricingException("pricing.version_date_invalid", "The effective end date must be after its start date.");
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var list = new NpgsqlCommand("select legal_entity_id,version,status from foundation_pricing.price_list where tenant_id=$1 and id=$2 and legal_entity_id=$3 for update", context.Connection, context.Transaction); Add(list, tenant.Value, listId, legalEntityId); await using var reader = await list.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new PricingException("pricing.list_not_found", "Price list was not found.");
            var entityId = reader.GetGuid(0); var listVersion = reader.GetInt64(1); var status = reader.GetString(2); await reader.CloseAsync();
            if (status != "Active") throw new PricingException("pricing.list_archived", "Archived price lists cannot receive new versions.");
            if (listVersion != request.ExpectedListVersion) throw new PricingException("pricing.concurrency_conflict", "The price list changed. Refresh it and try again.");
            await using var insert = new NpgsqlCommand("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,version) select $1,$2,$3,coalesce(max(version_no),0)+1,'Draft',$4,$5,$6,$7,1 from foundation_pricing.price_list_version where tenant_id=$1 and price_list_id=$3 returning version_no", context.Connection, context.Transaction);
            Add(insert, tenant.Value, id, listId, request.EffectiveFrom, request.EffectiveTo ?? (object)DBNull.Value, actorId, now); var number = Convert.ToInt32(await insert.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            await using (var bump = new NpgsqlCommand("update foundation_pricing.price_list set version=version+1,updated_at=$3 where tenant_id=$1 and id=$2 and version=$4", context.Connection, context.Transaction)) { Add(bump, tenant.Value, listId, now, request.ExpectedListVersion); if (await bump.ExecuteNonQueryAsync(token) != 1) throw new PricingException("pricing.concurrency_conflict", "The price list changed. Refresh it and try again."); }
            await AuditAsync(context.Connection, context.Transaction, tenant.Value, entityId, listId, id, actorId, membershipId, "price_version_created", correlationId, now, new { request.EffectiveFrom, request.EffectiveTo }, token);
            return new PriceVersionDto(id, listId, number, "Draft", request.EffectiveFrom, request.EffectiveTo, 1);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<PriceVersionDto>> ListVersionsAsync(TenantId tenant, Guid legalEntityId, Guid listId, CancellationToken cancellationToken)
    {
        await using var scope = await OpenTenantScopeAsync(tenant, cancellationToken);
        await using var command = new NpgsqlCommand("select v.id,v.price_list_id,v.version_no,v.status,v.effective_from,v.effective_to,v.version from foundation_pricing.price_list_version v join foundation_pricing.price_list l on l.tenant_id=v.tenant_id and l.id=v.price_list_id where v.tenant_id=$1 and l.legal_entity_id=$2 and v.price_list_id=$3 order by v.version_no desc limit 50", scope.Connection, scope.Transaction); Add(command, tenant.Value, legalEntityId, listId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var result = new List<PriceVersionDto>();
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetString(3), DateOnly.FromDateTime(reader.GetDateTime(4)), reader.IsDBNull(5) ? null : DateOnly.FromDateTime(reader.GetDateTime(5)), reader.GetInt64(6)));
        return result;
    }

    public Task AddEntryAsync(TenantId tenant, Guid legalEntityId, Guid actorId, Guid membershipId, Guid listId, Guid versionId, CreatePriceEntryRequest request, decimal unitPrice, Guid correlationId, CancellationToken cancellationToken)
        => AddVersionContentAsync(tenant, legalEntityId, actorId, membershipId, listId, versionId, "price_entry_created", correlationId, request, async (connection, transaction, token) =>
        {
            var ruleId = Guid.CreateVersion7();
            await using var command = new NpgsqlCommand("insert into foundation_pricing.price_entry(tenant_id,id,price_list_version_id,variant_id,uom_id,party_id,price_group_id,min_quantity,max_quantity,unit_price,valid_from,valid_to,priority,status,version,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,'Active',1,$14)", connection, transaction);
            Add(command, tenant.Value, ruleId, versionId, request.VariantId, request.UomId, request.PartyId ?? (object)DBNull.Value, request.PriceGroupId ?? (object)DBNull.Value, request.MinQuantity, request.MaxQuantity ?? (object)DBNull.Value, unitPrice, request.ValidFrom ?? (object)DBNull.Value, request.ValidTo ?? (object)DBNull.Value, request.Priority, DateTimeOffset.UtcNow); await command.ExecuteNonQueryAsync(token);
        }, cancellationToken);

    public Task AddRuleAsync(TenantId tenant, Guid legalEntityId, Guid actorId, Guid membershipId, Guid listId, Guid versionId, CreatePriceRuleRequest request, decimal resultValue, Guid correlationId, CancellationToken cancellationToken)
        => AddVersionContentAsync(tenant, legalEntityId, actorId, membershipId, listId, versionId, "price_rule_created", correlationId, request, async (connection, transaction, token) =>
        {
            var id = Guid.CreateVersion7();
            await using var command = new NpgsqlCommand("insert into foundation_pricing.price_rule(tenant_id,id,price_list_version_id,product_scope,variant_id,template_id,category_id,category_depth,brand_id,party_scope,party_id,price_group_id,uom_id,min_quantity,max_quantity,valid_from,valid_to,priority,result_kind,result_value,status,version,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,'Active',1,$21)", connection, transaction);
            Add(command, tenant.Value, id, versionId, request.ProductScope.ToString(), request.VariantId ?? (object)DBNull.Value, request.TemplateId ?? (object)DBNull.Value, request.CategoryId ?? (object)DBNull.Value, request.CategoryDepth, request.BrandId ?? (object)DBNull.Value, request.PartyScope.ToString(), request.PartyId ?? (object)DBNull.Value, request.PriceGroupId ?? (object)DBNull.Value, request.UomId ?? (object)DBNull.Value, request.MinQuantity, request.MaxQuantity ?? (object)DBNull.Value, request.ValidFrom ?? (object)DBNull.Value, request.ValidTo ?? (object)DBNull.Value, request.Priority, request.ResultKind.ToString(), resultValue, DateTimeOffset.UtcNow); await command.ExecuteNonQueryAsync(token);
        }, cancellationToken);

    private async Task AddVersionContentAsync(TenantId tenant, Guid legalEntityId, Guid actorId, Guid membershipId, Guid listId, Guid versionId, string action, Guid correlationId, object details, Func<NpgsqlConnection,NpgsqlTransaction,CancellationToken,Task> add, CancellationToken cancellationToken)
    {
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var entity = await ScalarGuidAsync(context.Connection, context.Transaction, "select l.legal_entity_id from foundation_pricing.price_list l join foundation_pricing.price_list_version v on v.tenant_id=l.tenant_id and v.price_list_id=l.id where l.tenant_id=$1 and l.id=$2 and l.legal_entity_id=$3 and l.status='Active' and v.id=$4 and v.status='Draft'", token, tenant.Value, listId, legalEntityId, versionId);
            await add(context.Connection, context.Transaction, token);
            await AuditAsync(context.Connection, context.Transaction, tenant.Value, entity, listId, versionId, actorId, membershipId, action, correlationId, DateTimeOffset.UtcNow, details, token);
            return true;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<PricingRuleCandidate>> GetCandidatesAsync(TenantId tenant, Guid versionId, IReadOnlyCollection<PricingContext> contexts, CancellationToken cancellationToken)
    {
        if (contexts.Count is < 1 or > 500) throw new PricingException("pricing.batch_size_invalid", "Resolve batches must contain between 1 and 500 lines.");
        var variants = contexts.Select(x => x.ProductVariantId).Distinct().ToArray(); var templates = contexts.Select(x => x.ProductTemplateId).Distinct().ToArray(); var categories = contexts.SelectMany(x => x.CategoryPath).Distinct().ToArray(); var brands = contexts.Where(x => x.BrandId is not null).Select(x => x.BrandId!.Value).Distinct().ToArray(); var parties = contexts.Where(x => x.PartyId is not null).Select(x => x.PartyId!.Value).Distinct().ToArray(); var groups = contexts.Where(x => x.PriceGroupId is not null).Select(x => x.PriceGroupId!.Value).Distinct().ToArray(); var units = contexts.Select(x => x.UnitOfMeasureId).Distinct().ToArray(); var pricingDate = contexts.Min(x => x.PricingDate);
        await using var scope = await OpenTenantScopeAsync(tenant, cancellationToken);
        const string ruleSql = "select id,product_scope,variant_id,template_id,category_id,category_depth,brand_id,party_scope,party_id,price_group_id,uom_id,min_quantity,max_quantity,valid_from,valid_to,priority,result_kind,result_value,false from foundation_pricing.price_rule where tenant_id=$1 and price_list_version_id=$2 and status='Active' and (valid_from is null or valid_from<=$3) and (valid_to is null or valid_to>$3) and (product_scope='AllProducts' or variant_id=any($4) or template_id=any($5) or category_id=any($6) or brand_id=any($7)) and (party_scope='AllParties' or party_id=any($8) or price_group_id=any($9)) and (uom_id is null or uom_id=any($10)) order by id limit $11";
        const string entrySql = "select id,'Variant',variant_id,null::uuid,null::uuid,0,null::uuid,case when party_id is not null then 'SpecificParty' when price_group_id is not null then 'PriceGroup' else 'AllParties' end,party_id,price_group_id,uom_id,min_quantity,max_quantity,valid_from,valid_to,priority,'FixedUnitPrice',unit_price,true from foundation_pricing.price_entry where tenant_id=$1 and price_list_version_id=$2 and status='Active' and (valid_from is null or valid_from<=$3) and (valid_to is null or valid_to>$3) and variant_id=any($4) and (party_id is null or party_id=any($8)) and (price_group_id is null or price_group_id=any($9)) and uom_id=any($10) order by id limit $11";
        var result = new List<PricingRuleCandidate>();
        await using (var rules = new NpgsqlCommand(ruleSql, scope.Connection, scope.Transaction))
        {
            Add(rules, tenant.Value, versionId, pricingDate, variants, templates, categories, brands, parties, groups, units, MaximumCandidateRows + 1);
            await using var reader = await rules.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) result.Add(ReadCandidate(reader));
        }
        if (result.Count > MaximumCandidateRows) throw new PricingException("pricing.candidate_bound_exceeded", "This pricing configuration exceeds the bounded candidate set. Narrow the list with more specific rules.");
        await using (var entries = new NpgsqlCommand(entrySql, scope.Connection, scope.Transaction))
        {
            Add(entries, tenant.Value, versionId, pricingDate, variants, templates, categories, brands, parties, groups, units, MaximumCandidateRows + 1);
            await using var reader = await entries.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) result.Add(ReadCandidate(reader));
        }
        if (result.Count > MaximumCandidateRows) throw new PricingException("pricing.candidate_bound_exceeded", "This pricing configuration exceeds the bounded candidate set. Narrow the list with more specific rules.");
        return result;
    }

    public async Task<(PricingListContext List, PricingVersionContext Version)> ResolveListAndVersionAsync(TenantId tenant, PricingContext context, CancellationToken cancellationToken)
    {
        await using var scope = await OpenTenantScopeAsync(tenant, cancellationToken);
        const string listSql = "with candidates as (select distinct l.id,l.legal_entity_id,l.direction,l.currency_code,l.currency_policy,l.status,case when l.id=$4 then 4 when a.party_id=$6 and $6::uuid is not null then 3 when a.price_group_id=$7 and $7::uuid is not null then 2 else 1 end as specificity from foundation_pricing.price_list l left join foundation_pricing.price_list_assignment a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.direction=l.direction and a.price_list_id=l.id and a.valid_from<=$5 and (a.valid_to is null or a.valid_to>$5) where l.tenant_id=$1 and l.legal_entity_id=$2 and l.direction=$3 and l.status='Active' and ((l.id=$4) or ($4::uuid is null and (a.party_id=$6 or a.price_group_id=$7 or a.is_default)))) select id,legal_entity_id,direction,currency_code,currency_policy,status from candidates where specificity=(select max(specificity) from candidates) order by id limit 2";
        await using var command = new NpgsqlCommand(listSql, scope.Connection, scope.Transaction); Add(command, tenant.Value, context.LegalEntityId.Value, context.Direction.ToString(), context.PriceListId ?? (object)DBNull.Value, context.PricingDate, context.PartyId ?? (object)DBNull.Value, context.PriceGroupId ?? (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new PricingException("pricing.price_list_not_found", "No active price list matches this legal entity and pricing direction.");
        var list = new PricingListContext(reader.GetGuid(0), reader.GetGuid(1), Enum.Parse<PricingDirection>(reader.GetString(2)), reader.GetString(3), Enum.Parse<PricingCurrencyPolicy>(reader.GetString(4)), reader.GetString(5));
        if (await reader.ReadAsync(cancellationToken)) throw new PricingException("pricing.list_selection_conflict", "More than one price list matches this pricing context. Set an explicit list or correct list assignments.");
        await reader.CloseAsync();
        await using var versionCommand = new NpgsqlCommand("select id,version_no,effective_from,effective_to,status from foundation_pricing.price_list_version where tenant_id=$1 and price_list_id=$2 and status in ('Active','Expired','Scheduled') and effective_from<=$3 and (effective_to is null or effective_to>$3) order by effective_from desc,version_no desc limit 2", scope.Connection, scope.Transaction); Add(versionCommand, tenant.Value, list.Id, context.PricingDate);
        await using var versionReader = await versionCommand.ExecuteReaderAsync(cancellationToken);
        if (!await versionReader.ReadAsync(cancellationToken)) throw new PricingException("pricing.version_not_effective", "No price-list version is effective for this pricing date.");
        var version = new PricingVersionContext(versionReader.GetGuid(0), versionReader.GetInt32(1), DateOnly.FromDateTime(versionReader.GetDateTime(2)), versionReader.IsDBNull(3) ? null : DateOnly.FromDateTime(versionReader.GetDateTime(3)), versionReader.GetString(4));
        if (await versionReader.ReadAsync(cancellationToken) && DateOnly.FromDateTime(versionReader.GetDateTime(2)) == version.EffectiveFrom) throw new PricingException("pricing.version_conflict", "More than one price-list version has the same effective date.");
        return (list, version);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> ResolvePartyGroupsAsync(TenantId tenant, IReadOnlyCollection<Guid> partyIds, DateOnly businessDate, CancellationToken cancellationToken)
    {
        if (partyIds.Count == 0) return new Dictionary<Guid, Guid>();
        await using var scope = await OpenTenantScopeAsync(tenant, cancellationToken);
        await using var command = new NpgsqlCommand("select m.party_id,m.price_group_id from foundation_pricing.price_group_member m join foundation_pricing.price_group g on g.tenant_id=m.tenant_id and g.id=m.price_group_id and g.status='Active' where m.tenant_id=$1 and m.party_id=any($2) and m.valid_from<=$3 and (m.valid_to is null or m.valid_to>$3) order by m.party_id,m.valid_from desc,m.price_group_id limit 1000", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, partyIds.Distinct().ToArray(), businessDate); await using var reader = await command.ExecuteReaderAsync(cancellationToken); var result = new Dictionary<Guid, Guid>();
        while (await reader.ReadAsync(cancellationToken)) if (!result.TryAdd(reader.GetGuid(0), reader.GetGuid(1))) throw new PricingException("pricing.party_group_conflict", "A party is assigned to more than one active Price Group for this date.");
        return result;
    }

    public async Task<IReadOnlySet<Guid>> FindActiveGroupsAsync(TenantId tenant, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0) return new HashSet<Guid>();
        if (groupIds.Count > 500) throw new PricingException("pricing.import_row_count_invalid", "A single import can reference at most 500 distinct Price Groups.");
        await using var scope = await OpenTenantScopeAsync(tenant, cancellationToken);
        await using var command = new NpgsqlCommand("select id from foundation_pricing.price_group where tenant_id=$1 and id=any($2) and status='Active'", scope.Connection, scope.Transaction); Add(command, tenant.Value, groupIds.Distinct().ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var result = new HashSet<Guid>(); while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetGuid(0)); return result;
    }

    public async Task<PricingPolicyDto> GetPolicyAsync(TenantId tenant, Guid legalEntityId, CancellationToken cancellationToken)
    {
        await using var scope = await OpenTenantScopeAsync(tenant, cancellationToken);
        await using var command = new NpgsqlCommand("select currency_policy,allow_uom_derived_price,version from foundation_pricing.pricing_policy where tenant_id=$1 and legal_entity_id=$2", scope.Connection, scope.Transaction); Add(command, tenant.Value, legalEntityId); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(legalEntityId, reader.GetString(0), reader.GetBoolean(1), reader.GetInt64(2)) : new(legalEntityId, PricingCurrencyPolicy.ExactCurrency.ToString(), false, 0);
    }

    public async Task<PricingPolicyDto> UpdatePolicyAsync(TenantId tenant, Guid legalEntityId, Guid actorId, Guid membershipId, PricingPolicyRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("insert into foundation_pricing.pricing_policy(tenant_id,legal_entity_id,currency_policy,allow_uom_derived_price,version,updated_at) values($1,$2,$3,$4,1,$5) on conflict(tenant_id,legal_entity_id) do update set currency_policy=excluded.currency_policy,allow_uom_derived_price=excluded.allow_uom_derived_price,updated_at=excluded.updated_at,version=foundation_pricing.pricing_policy.version+1 where foundation_pricing.pricing_policy.version=$6 returning version", context.Connection, context.Transaction);
            Add(command, tenant.Value, legalEntityId, request.CurrencyPolicy.ToString(), request.AllowUomDerivedPrice, now, request.ExpectedVersion);
            var version = await command.ExecuteScalarAsync(token);
            if (version is null) throw new PricingException("pricing.concurrency_conflict", "Pricing policy changed. Refresh before saving.");
            await AuditAsync(context.Connection, context.Transaction, tenant.Value, legalEntityId, null, null, actorId, membershipId, "pricing_policy_updated", correlationId, now, request, token);
            return new PricingPolicyDto(legalEntityId, request.CurrencyPolicy.ToString(), request.AllowUomDerivedPrice, Convert.ToInt64(version, CultureInfo.InvariantCulture));
        }, cancellationToken);
    }

    public async Task<Guid> CreateGroupAsync(TenantId tenant, Guid actorId, Guid membershipId, CreatePriceGroupRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        ValidateCode(request.Code, "pricing.group_code_invalid"); var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("insert into foundation_pricing.price_group(tenant_id,id,code,name,status,created_at,updated_at,version) values($1,$2,$3,$4,'Active',$5,$5,1)", context.Connection, context.Transaction); Add(command, tenant.Value, id, request.Code.Trim().ToUpperInvariant(), RequiredText(request.Name, 160, "pricing.group_name_invalid"), now); await command.ExecuteNonQueryAsync(token);
            await AuditAsync(context.Connection, context.Transaction, tenant.Value, Guid.Empty, null, null, actorId, membershipId, "price_group_created", correlationId, now, new { request.Code }, token); return true;
        }, cancellationToken); return id;
    }

    public Task AddGroupMemberAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid legalEntityId, Guid groupId, AddPriceGroupMemberRequest request, Guid correlationId, CancellationToken cancellationToken) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.ValidTo is { } end && end <= request.ValidFrom) throw new PricingException("pricing.group_member_dates_invalid", "The membership end date must be after its start date.");
        await using var command = new NpgsqlCommand("insert into foundation_pricing.price_group_member(tenant_id,id,price_group_id,party_id,valid_from,valid_to) values($1,$2,$3,$4,$5,$6)", context.Connection, context.Transaction); Add(command, tenant.Value, Guid.CreateVersion7(), groupId, request.PartyId, request.ValidFrom, request.ValidTo ?? (object)DBNull.Value); await command.ExecuteNonQueryAsync(token);
        await AuditAsync(context.Connection, context.Transaction, tenant.Value, legalEntityId, null, null, actorId, membershipId, "price_group_member_added", correlationId, DateTimeOffset.UtcNow, new { groupId, request.PartyId, request.ValidFrom, request.ValidTo }, token); return true;
    }, cancellationToken);

    public Task AssignListAsync(TenantId tenant, Guid actorId, Guid membershipId, CreatePriceListAssignmentRequest request, Guid correlationId, CancellationToken cancellationToken) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.ValidTo is { } end && end <= request.ValidFrom) throw new PricingException("pricing.assignment_dates_invalid", "The assignment end date must be after its start date.");
        if ((request.PartyId is null) == (request.PriceGroupId is null) && !request.IsDefault) throw new PricingException("pricing.assignment_scope_invalid", "Choose one customer or one customer group for this assignment.");
        if (request.IsDefault && (request.PartyId is not null || request.PriceGroupId is not null)) throw new PricingException("pricing.assignment_scope_invalid", "A default assignment cannot also target a customer or group.");
        await using var list = new NpgsqlCommand("select legal_entity_id,direction from foundation_pricing.price_list where tenant_id=$1 and id=$2 and status='Active' for update", context.Connection, context.Transaction); Add(list, tenant.Value, request.PriceListId); await using var reader = await list.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new PricingException("pricing.list_not_found", "An active price list was not found.");
        var entityId = reader.GetGuid(0); var direction = reader.GetString(1); await reader.CloseAsync();
        if (entityId != request.LegalEntityId || direction != request.Direction.ToString()) throw new PricingException("pricing.assignment_scope_invalid", "The list direction and legal entity must match the assignment.");
        if (request.IsDefault) { await using var remove = new NpgsqlCommand("delete from foundation_pricing.price_list_assignment where tenant_id=$1 and legal_entity_id=$2 and direction=$3 and is_default", context.Connection, context.Transaction); Add(remove, tenant.Value, request.LegalEntityId, request.Direction.ToString()); await remove.ExecuteNonQueryAsync(token); }
        await UpsertAssignmentAsync(context.Connection, context.Transaction, tenant.Value, request.LegalEntityId, request.Direction, request.PriceListId, request.PartyId, request.PriceGroupId, request.IsDefault, request.ValidFrom, token, request.ValidTo);
        await AuditAsync(context.Connection, context.Transaction, tenant.Value, entityId, request.PriceListId, null, actorId, membershipId, "price_list_assigned", correlationId, DateTimeOffset.UtcNow, request, token); return true;
    }, cancellationToken);

    public async Task<PricingImportPreviewDto> SaveImportPreviewAsync(TenantId tenant, Guid actorId, Guid membershipId, PricingImportRequest request, IReadOnlyList<PricingImportRowDto> rows, Guid correlationId, CancellationToken cancellationToken)
    {
        var batchId = Guid.CreateVersion7(); var errors = rows.Count(row => row.ErrorCode is not null); var now = DateTimeOffset.UtcNow;
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var scope = new NpgsqlCommand("select l.legal_entity_id,v.status from foundation_pricing.price_list l join foundation_pricing.price_list_version v on v.tenant_id=l.tenant_id and v.price_list_id=l.id where l.tenant_id=$1 and l.id=$2 and v.id=$3 and l.status='Active'", context.Connection, context.Transaction); Add(scope, tenant.Value, request.PriceListId, request.TargetVersionId); await using var reader = await scope.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new PricingException("pricing.import_target_not_found", "The target price list and version were not found.");
            var entityId = reader.GetGuid(0); var versionStatus = reader.GetString(1); await reader.CloseAsync();
            if (entityId != request.LegalEntityId) throw new PricingException("pricing.import_scope_invalid", "The import legal entity does not match the target price list.");
            if (versionStatus != "Draft") throw new PricingException("pricing.import_requires_draft", "Imports can only target a draft version.");
            await using (var insert = new NpgsqlCommand("insert into foundation_pricing.pricing_import_batch(tenant_id,id,legal_entity_id,price_list_id,target_version_id,status,row_count,error_count,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", context.Connection, context.Transaction)) { Add(insert, tenant.Value, batchId, entityId, request.PriceListId, request.TargetVersionId, errors == 0 ? "Ready" : "Previewed", rows.Count, errors, actorId, now); await insert.ExecuteNonQueryAsync(token); }
            foreach (var row in rows)
                await using (var insertRow = new NpgsqlCommand("insert into foundation_pricing.pricing_import_row(tenant_id,id,batch_id,row_number,raw_sku,variant_id,uom_id,unit_price,currency_code,party_id,price_group_id,min_quantity,max_quantity,valid_from,valid_to,priority,rule_kind,error_code,error_message) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19)", context.Connection, context.Transaction))
                { Add(insertRow, tenant.Value, Guid.CreateVersion7(), batchId, row.RowNumber, row.Sku, row.VariantId ?? (object)DBNull.Value, row.UomId ?? (object)DBNull.Value, row.UnitPrice is null ? DBNull.Value : decimal.Parse(row.UnitPrice, CultureInfo.InvariantCulture), row.CurrencyCode ?? (object)DBNull.Value, row.PartyId ?? (object)DBNull.Value, row.PriceGroupId ?? (object)DBNull.Value, row.MinQuantity, row.MaxQuantity ?? (object)DBNull.Value, row.ValidFrom ?? (object)DBNull.Value, row.ValidTo ?? (object)DBNull.Value, row.Priority, row.RuleType ?? (object)DBNull.Value, row.ErrorCode ?? (object)DBNull.Value, row.ErrorMessage ?? (object)DBNull.Value); await insertRow.ExecuteNonQueryAsync(token); }
            await AuditAsync(context.Connection, context.Transaction, tenant.Value, entityId, request.PriceListId, request.TargetVersionId, actorId, membershipId, "pricing_import_previewed", correlationId, now, new { batchId, rowCount = rows.Count, errorCount = errors }, token); return true;
        }, cancellationToken);
        return new(batchId, rows.Count, errors, rows);
    }

    public async Task<PricingImportCommitDto> CommitImportAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid batchId, Guid correlationId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var batch = new NpgsqlCommand("select legal_entity_id,price_list_id,target_version_id,status,error_count from foundation_pricing.pricing_import_batch where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); Add(batch, tenant.Value, batchId); await using var reader = await batch.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new PricingException("pricing.import_not_found", "The import batch was not found.");
            var entityId = reader.GetGuid(0); var listId = reader.GetGuid(1); var versionId = reader.GetGuid(2); var status = reader.GetString(3); var errors = reader.GetInt32(4); await reader.CloseAsync();
            if (status == "Committed") return new PricingImportCommitDto(batchId, 0, status);
            if (status != "Ready" || errors != 0) throw new PricingException("pricing.import_not_ready", "Correct every import row before committing this batch.");
            await using var draft = new NpgsqlCommand("select 1 from foundation_pricing.price_list_version where tenant_id=$1 and id=$2 and price_list_id=$3 and status='Draft' for update", context.Connection, context.Transaction); Add(draft, tenant.Value, versionId, listId);
            if (await draft.ExecuteScalarAsync(token) is null) throw new PricingException("pricing.import_requires_draft", "The target version is no longer a draft.");
            var entrySql = "insert into foundation_pricing.price_entry(tenant_id,id,price_list_version_id,variant_id,uom_id,party_id,price_group_id,min_quantity,max_quantity,unit_price,valid_from,valid_to,priority,status,version,created_at) select r.tenant_id,gen_random_uuid(),b.target_version_id,r.variant_id,r.uom_id,r.party_id,r.price_group_id,r.min_quantity,r.max_quantity,r.unit_price,r.valid_from,r.valid_to,r.priority,'Active',1,$3 from foundation_pricing.pricing_import_row r join foundation_pricing.pricing_import_batch b on b.tenant_id=r.tenant_id and b.id=r.batch_id where r.tenant_id=$1 and b.id=$2 and r.error_code is null and r.rule_kind is null";
            int createdEntries;
            await using (var insert = new NpgsqlCommand(entrySql, context.Connection, context.Transaction)) { Add(insert, tenant.Value, batchId, now); createdEntries = await insert.ExecuteNonQueryAsync(token); }
            var ruleSql = "insert into foundation_pricing.price_rule(tenant_id,id,price_list_version_id,product_scope,variant_id,party_scope,party_id,price_group_id,uom_id,min_quantity,max_quantity,valid_from,valid_to,priority,result_kind,result_value,status,version,created_at) select r.tenant_id,gen_random_uuid(),b.target_version_id,'Variant',r.variant_id,case when r.party_id is not null then 'SpecificParty' when r.price_group_id is not null then 'PriceGroup' else 'AllParties' end,r.party_id,r.price_group_id,r.uom_id,r.min_quantity,r.max_quantity,r.valid_from,r.valid_to,r.priority,r.rule_kind,r.unit_price,'Active',1,$3 from foundation_pricing.pricing_import_row r join foundation_pricing.pricing_import_batch b on b.tenant_id=r.tenant_id and b.id=r.batch_id where r.tenant_id=$1 and b.id=$2 and r.error_code is null and r.rule_kind is not null";
            int createdRules;
            await using (var insertRules = new NpgsqlCommand(ruleSql, context.Connection, context.Transaction)) { Add(insertRules, tenant.Value, batchId, now); createdRules = await insertRules.ExecuteNonQueryAsync(token); }
            var importedRules = await ReadVersionCandidatesAsync(context.Connection, context.Transaction, tenant, versionId, token);
            var conflicts = PricingResolver.FindConflicts(importedRules);
            if (conflicts.Count > 0) throw new PricingConfigurationConflictException("Imported rows overlap with contradictory draft prices. Correct the preview and retry.", conflicts.Select(rule => rule.Id).Distinct().ToArray());
            var created = createdEntries + createdRules;
            await using (var update = new NpgsqlCommand("update foundation_pricing.pricing_import_batch set status='Committed' where tenant_id=$1 and id=$2", context.Connection, context.Transaction)) { Add(update, tenant.Value, batchId); await update.ExecuteNonQueryAsync(token); }
            await AuditAsync(context.Connection, context.Transaction, tenant.Value, entityId, listId, versionId, actorId, membershipId, "pricing_import_committed", correlationId, now, new { batchId, created }, token);
            return new PricingImportCommitDto(batchId, created, "Committed");
        }, cancellationToken);
    }

    public async Task<string> ExportEntriesCsvAsync(TenantId tenant, Guid legalEntityId, Guid listId, Guid versionId, CancellationToken cancellationToken)
    {
        await using var scope = await OpenTenantScopeAsync(tenant, cancellationToken);
        await using var command = new NpgsqlCommand("select product_scope,variant_id,template_id,category_id,brand_id,uom_id,party_scope,party_id,price_group_id,min_quantity,max_quantity,valid_from,valid_to,priority,result_kind,result_value from (select 'Variant'::text product_scope,e.variant_id,null::uuid template_id,null::uuid category_id,null::uuid brand_id,e.uom_id,case when e.party_id is not null then 'SpecificParty' when e.price_group_id is not null then 'PriceGroup' else 'AllParties' end party_scope,e.party_id,e.price_group_id,e.min_quantity,e.max_quantity,e.valid_from,e.valid_to,e.priority,'FixedUnitPrice'::text result_kind,e.unit_price result_value from foundation_pricing.price_entry e where e.tenant_id=$1 and e.price_list_version_id=$4 union all select r.product_scope,r.variant_id,r.template_id,r.category_id,r.brand_id,r.uom_id,r.party_scope,r.party_id,r.price_group_id,r.min_quantity,r.max_quantity,r.valid_from,r.valid_to,r.priority,r.result_kind,r.result_value from foundation_pricing.price_rule r where r.tenant_id=$1 and r.price_list_version_id=$4) rules join foundation_pricing.price_list l on l.tenant_id=$1 and l.id=$2 and l.legal_entity_id=$3 join foundation_pricing.price_list_version v on v.tenant_id=$1 and v.id=$4 and v.price_list_id=l.id order by product_scope,variant_id,template_id,category_id,uom_id,party_id,price_group_id limit 10000", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, listId, legalEntityId, versionId);
        // Product, party, and unit masters remain owned by their respective modules; exports carry stable references.
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var output = new System.Text.StringBuilder("ProductScope,VariantId,TemplateId,CategoryId,BrandId,UomId,PartyScope,PartyId,PriceGroupId,MinQty,MaxQty,ValidFrom,ValidTo,Priority,RuleType,Value\n");
        while (await reader.ReadAsync(cancellationToken)) output.Append(reader.GetString(0)).Append(',').Append(reader.IsDBNull(1) ? "" : reader.GetGuid(1)).Append(',').Append(reader.IsDBNull(2) ? "" : reader.GetGuid(2)).Append(',').Append(reader.IsDBNull(3) ? "" : reader.GetGuid(3)).Append(',').Append(reader.IsDBNull(4) ? "" : reader.GetGuid(4)).Append(',').Append(reader.IsDBNull(5) ? "" : reader.GetGuid(5)).Append(',').Append(reader.GetString(6)).Append(',').Append(reader.IsDBNull(7) ? "" : reader.GetGuid(7)).Append(',').Append(reader.IsDBNull(8) ? "" : reader.GetGuid(8)).Append(',').Append(reader.GetDecimal(9).ToString(CultureInfo.InvariantCulture)).Append(',').Append(reader.IsDBNull(10) ? "" : reader.GetDecimal(10).ToString(CultureInfo.InvariantCulture)).Append(',').Append(reader.IsDBNull(11) ? "" : reader.GetDateTime(11).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',').Append(reader.IsDBNull(12) ? "" : reader.GetDateTime(12).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',').Append(reader.GetInt32(13).ToString(CultureInfo.InvariantCulture)).Append(',').Append(reader.GetString(14)).Append(',').Append(reader.GetDecimal(15).ToString(CultureInfo.InvariantCulture)).Append('\n');
        return output.ToString();
    }

    public async Task<(int Rules, int Entries, int ZeroEntries)> GetCoverageCountsAsync(TenantId tenant, Guid legalEntityId, Guid listId, Guid versionId, CancellationToken cancellationToken)
    {
        await using var scope = await OpenTenantScopeAsync(tenant, cancellationToken);
        const string sql = "select (select count(*) from foundation_pricing.price_rule r where r.tenant_id=$1 and r.price_list_version_id=$3 and r.status='Active')::int,(select count(*) from foundation_pricing.price_entry e where e.tenant_id=$1 and e.price_list_version_id=$3 and e.status='Active')::int,(select count(*) from foundation_pricing.price_entry e where e.tenant_id=$1 and e.price_list_version_id=$3 and e.status='Active' and e.unit_price=0)::int from foundation_pricing.price_list l join foundation_pricing.price_list_version v on v.tenant_id=l.tenant_id and v.price_list_id=l.id and v.id=$3 where l.tenant_id=$1 and l.id=$2 and l.legal_entity_id=$4";
        await using var command = new NpgsqlCommand(sql, scope.Connection, scope.Transaction); Add(command, tenant.Value, listId, versionId, legalEntityId); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new PricingException("pricing.version_not_found", "The requested price list version was not found for this legal entity.");
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    public async Task<(int RuleCount,int EntryCount)> ValidateVersionAsync(TenantId tenant, Guid legalEntityId, Guid versionId, CancellationToken cancellationToken)
    {
        await using var scope = await OpenTenantScopeAsync(tenant, cancellationToken);
        await using (var authorization = new NpgsqlCommand("select 1 from foundation_pricing.price_list_version v join foundation_pricing.price_list l on l.tenant_id=v.tenant_id and l.id=v.price_list_id where v.tenant_id=$1 and v.id=$2 and l.legal_entity_id=$3", scope.Connection, scope.Transaction)) { Add(authorization, tenant.Value, versionId, legalEntityId); if (await authorization.ExecuteScalarAsync(cancellationToken) is null) throw new PricingException("pricing.version_not_found", "Price-list version was not found for this legal entity."); }
        var rules = await ReadVersionCandidatesAsync(scope.Connection, scope.Transaction, tenant, versionId, cancellationToken);
        var conflicts = PricingResolver.FindConflicts(rules);
        if (conflicts.Count > 0) throw new PricingConfigurationConflictException($"Version contains {conflicts.Count} equally ranked, overlapping rules with contradictory outcomes.", conflicts.Select(x=>x.Id).Distinct().ToArray());
        await using var counts = new NpgsqlCommand("select (select count(*) from foundation_pricing.price_rule where tenant_id=$1 and price_list_version_id=$2 and status='Active'),(select count(*) from foundation_pricing.price_entry where tenant_id=$1 and price_list_version_id=$2 and status='Active')", scope.Connection, scope.Transaction); Add(counts, tenant.Value, versionId); await using var reader=await counts.ExecuteReaderAsync(cancellationToken); await reader.ReadAsync(cancellationToken); return (reader.GetInt32(0),reader.GetInt32(1));
    }

    public async Task<PriceVersionDto> ActivateVersionAsync(TenantId tenant, Guid legalEntityId, Guid actorId, Guid membershipId, Guid versionId, Guid correlationId, DateOnly today, CancellationToken cancellationToken, bool allowScheduledCutover = false)
    {
        var now=DateTimeOffset.UtcNow;
        return await transactions.ExecuteAsync(tenant,async(context,token)=>
        {
            await using var find=new NpgsqlCommand("select v.price_list_id,v.version_no,v.effective_from,v.effective_to,v.status,l.legal_entity_id,l.direction from foundation_pricing.price_list_version v join foundation_pricing.price_list l on l.tenant_id=v.tenant_id and l.id=v.price_list_id where v.tenant_id=$1 and v.id=$2 and l.legal_entity_id=$3 and l.status='Active' for update of v,l",context.Connection,context.Transaction); Add(find,tenant.Value,versionId,legalEntityId); await using var reader=await find.ExecuteReaderAsync(token); if(!await reader.ReadAsync(token))throw new PricingException("pricing.version_not_found","Price-list version was not found for this legal entity."); var listId=reader.GetGuid(0);var number=reader.GetInt32(1);var start=DateOnly.FromDateTime(reader.GetDateTime(2));var end=reader.IsDBNull(3)?(DateOnly?)null:DateOnly.FromDateTime(reader.GetDateTime(3));var status=reader.GetString(4);var entity=reader.GetGuid(5);var direction=reader.GetString(6);await reader.CloseAsync(); if(status!="Draft" && !(allowScheduledCutover && status=="Scheduled" && start<=today))throw new PricingException("pricing.version_not_activatable","Only draft versions or due scheduled versions can be activated.");
            var rules=await ReadVersionCandidatesAsync(context.Connection,context.Transaction,tenant,versionId,token);var conflicts=PricingResolver.FindConflicts(rules);if(conflicts.Count>0)throw new PricingConfigurationConflictException("Equal-rank overlapping rules produce contradictory prices.",conflicts.Select(x=>x.Id).Distinct().ToArray());
            var nextStatus=start>today?"Scheduled":"Active";
            if(nextStatus=="Active")await using(var expire=new NpgsqlCommand("update foundation_pricing.price_list_version set status='Expired',version=version+1 where tenant_id=$1 and price_list_id=$2 and status='Active' and id<>$3",context.Connection,context.Transaction)){Add(expire,tenant.Value,listId,versionId);await expire.ExecuteNonQueryAsync(token);}
            await using(var update=new NpgsqlCommand("update foundation_pricing.price_list_version set status=$3,activated_at=$4,version=version+1 where tenant_id=$1 and id=$2 and status=$5",context.Connection,context.Transaction)){Add(update,tenant.Value,versionId,nextStatus,now,status);if(await update.ExecuteNonQueryAsync(token)!=1)throw new PricingException("pricing.concurrency_conflict","The version changed while it was being activated.");}
            await AuditAsync(context.Connection,context.Transaction,tenant.Value,entity,listId,versionId,actorId,membershipId,nextStatus=="Active"?"price_version_activated":"price_version_scheduled",correlationId,now,new{versionNo=number,effectiveFrom=start,direction},token);
            if(nextStatus=="Active")context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(),"PriceListVersionActivatedV1",JsonSerializer.Serialize(new{priceListId=listId,priceListVersionId=versionId,legalEntityId=entity,direction,versionNo=number}),tenant,now,correlationId,versionId,"pricing"));
            return new PriceVersionDto(versionId,listId,number,nextStatus,start,end,2);
        },cancellationToken);
    }

    public async Task<int> ActivateDueVersionsAsync(CancellationToken cancellationToken)
    {
        var due = new List<(Guid TenantId, Guid EntityId, Guid VersionId, DateOnly BusinessDate)>();
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = new NpgsqlCommand("select tenant_id,legal_entity_id,version_id,business_date from foundation_pricing.claim_due_price_versions(100)", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) due.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), DateOnly.FromDateTime(reader.GetDateTime(3))));
        var activated = 0;
        foreach (var item in due)
        {
            try
            {
                await ActivateVersionAsync(new TenantId(item.TenantId), item.EntityId, Guid.Empty, Guid.Empty, item.VersionId, Guid.CreateVersion7(), item.BusinessDate, cancellationToken, allowScheduledCutover: true);
                activated++;
            }
            catch (PricingException)
            {
                // Invalid due schedules remain visible and unchanged for owner correction.
            }
        }
        return activated;
    }

    public async Task ArchiveListAsync(TenantId tenant, Guid legalEntityId, Guid actorId, Guid membershipId, Guid listId, long expectedVersion, Guid correlationId, CancellationToken cancellationToken)
    {
        var now=DateTimeOffset.UtcNow;await transactions.ExecuteAsync(tenant,async(context,token)=>{await using var update=new NpgsqlCommand("update foundation_pricing.price_list set status='Archived',version=version+1,updated_at=$5 where tenant_id=$1 and id=$2 and legal_entity_id=$3 and version=$4 and status='Active' returning legal_entity_id",context.Connection,context.Transaction);Add(update,tenant.Value,listId,legalEntityId,expectedVersion,now);var entity=await update.ExecuteScalarAsync(token);if(entity is null)throw new PricingException("pricing.concurrency_conflict","Price list was not found, archived, or changed.");await using(var assignments=new NpgsqlCommand("delete from foundation_pricing.price_list_assignment where tenant_id=$1 and price_list_id=$2",context.Connection,context.Transaction)){Add(assignments,tenant.Value,listId);await assignments.ExecuteNonQueryAsync(token);}await AuditAsync(context.Connection,context.Transaction,tenant.Value,(Guid)entity,listId,null,actorId,membershipId,"price_list_archived",correlationId,now,new{},token);return true;},cancellationToken);
    }

    private static async Task<IReadOnlyList<PricingRuleCandidate>> ReadVersionCandidatesAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,TenantId tenant,Guid versionId,CancellationToken token)
    {
        const string sql="select id,product_scope,variant_id,template_id,category_id,category_depth,brand_id,party_scope,party_id,price_group_id,uom_id,min_quantity,max_quantity,valid_from,valid_to,priority,result_kind,result_value,false from foundation_pricing.price_rule where tenant_id=$1 and price_list_version_id=$2 and status='Active' union all select id,'Variant',variant_id,null::uuid,null::uuid,0,null::uuid,case when party_id is not null then 'SpecificParty' when price_group_id is not null then 'PriceGroup' else 'AllParties' end,party_id,price_group_id,uom_id,min_quantity,max_quantity,valid_from,valid_to,priority,'FixedUnitPrice',unit_price,true from foundation_pricing.price_entry where tenant_id=$1 and price_list_version_id=$2 and status='Active' limit 10001";
        await using var command=new NpgsqlCommand(sql,connection,transaction);Add(command,tenant.Value,versionId);await using var reader=await command.ExecuteReaderAsync(token);var rules=new List<PricingRuleCandidate>();while(await reader.ReadAsync(token))rules.Add(ReadCandidate(reader));if(rules.Count>10000)throw new PricingException("pricing.validation_bound_exceeded","Version validation is bounded to 10,000 current rules and entries.");return rules;
    }

    private static PricingRuleCandidate ReadCandidate(NpgsqlDataReader reader)=>new(reader.GetGuid(0),Enum.Parse<PricingProductScope>(reader.GetString(1)),NullableGuid(reader,2),NullableGuid(reader,3),NullableGuid(reader,4),reader.GetInt32(5),NullableGuid(reader,6),Enum.Parse<PricingPartyScope>(reader.GetString(7)),NullableGuid(reader,8),NullableGuid(reader,9),NullableGuid(reader,10),reader.GetDecimal(11),reader.IsDBNull(12)?null:reader.GetDecimal(12),reader.IsDBNull(13)?null:DateOnly.FromDateTime(reader.GetDateTime(13)),reader.IsDBNull(14)?null:DateOnly.FromDateTime(reader.GetDateTime(14)),reader.GetInt32(15),Enum.Parse<PricingResultKind>(reader.GetString(16)),reader.GetDecimal(17),reader.GetBoolean(18));

    private static async Task<Guid> ScalarGuidAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string sql,CancellationToken token,params object?[] args){await using var command=new NpgsqlCommand(sql,connection,transaction);Add(command,args);var value=await command.ExecuteScalarAsync(token);return value is Guid id?id:throw new PricingException("pricing.list_not_found","Price list was not found.");}
    private static async Task AuditAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid tenant,Guid entity,Guid? list,Guid? version,Guid actor,Guid membership,string action,Guid correlation,DateTimeOffset now,object details,CancellationToken token){await using var command=new NpgsqlCommand("insert into foundation_pricing.pricing_audit(tenant_id,id,legal_entity_id,price_list_id,version_id,actor_id,membership_id,action,occurred_at,correlation_id,details) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11::jsonb)",connection,transaction);Add(command,tenant,Guid.CreateVersion7(),entity,list??(object)DBNull.Value,version??(object)DBNull.Value,actor,membership,action,now,correlation,JsonSerializer.Serialize(details));await command.ExecuteNonQueryAsync(token);}
    private static async Task UpsertAssignmentAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid tenant,Guid entity,PricingDirection direction,Guid list,Guid? party,Guid? group,bool isDefault,DateOnly from,CancellationToken token,DateOnly? to=null){if(isDefault){await using var remove=new NpgsqlCommand("delete from foundation_pricing.price_list_assignment where tenant_id=$1 and legal_entity_id=$2 and direction=$3 and is_default",connection,transaction);Add(remove,tenant,entity,direction.ToString());await remove.ExecuteNonQueryAsync(token);}await using var command=new NpgsqlCommand("insert into foundation_pricing.price_list_assignment(tenant_id,id,legal_entity_id,direction,price_list_id,party_id,price_group_id,is_default,valid_from,valid_to,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)",connection,transaction);Add(command,tenant,Guid.CreateVersion7(),entity,direction.ToString(),list,party??(object)DBNull.Value,group??(object)DBNull.Value,isDefault,from,to??(object)DBNull.Value,DateTimeOffset.UtcNow);await command.ExecuteNonQueryAsync(token);}
    private async Task<TenantScope> OpenTenantScopeAsync(TenantId tenant,CancellationToken token){var connection=await dataSource.OpenConnectionAsync(token);var transaction=await connection.BeginTransactionAsync(token);try{await using var command=new NpgsqlCommand("select set_config('app.tenant_id',$1,true)",connection,transaction);command.Parameters.AddWithValue(tenant.Value.ToString("D"));await command.ExecuteNonQueryAsync(token);return new(connection,transaction);}catch{await transaction.DisposeAsync();await connection.DisposeAsync();throw;}}
    private static void Add(NpgsqlCommand command,params object?[] values){foreach(var value in values)command.Parameters.AddWithValue(value??DBNull.Value);}
    private static Guid? NullableGuid(NpgsqlDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?null:reader.GetGuid(ordinal);
    private static string NormalizeCurrency(string value){if(string.IsNullOrWhiteSpace(value))throw new PricingException("pricing.currency_invalid","Price-list currency is required.");var code=value.Trim().ToUpperInvariant();if(code.Length!=3||code.Any(character=>character is < 'A' or > 'Z'))throw new PricingException("pricing.currency_invalid","Currency must be a three-letter ISO code.");return code;}
    private static void ValidateCode(string value,string code){if(string.IsNullOrWhiteSpace(value)||value.Trim().Length>50||!value.Trim().All(character=>char.IsAsciiLetterOrDigit(character)||character is '-' or '_'))throw new PricingException(code,"Use a short letter, number, dash, or underscore code.");}
    private static string RequiredText(string value,int max,string code)=>!string.IsNullOrWhiteSpace(value)&&value.Trim().Length<=max?value.Trim():throw new PricingException(code,"A valid name is required.");
    private sealed class TenantScope(NpgsqlConnection connection,NpgsqlTransaction transaction):IAsyncDisposable{public NpgsqlConnection Connection{get;}=connection;public NpgsqlTransaction Transaction{get;}=transaction;public async ValueTask DisposeAsync(){await Transaction.DisposeAsync();await Connection.DisposeAsync();}}
}
