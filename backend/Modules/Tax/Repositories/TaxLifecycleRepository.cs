using System.Text.Json;
using Core.Transactions;
using Core.Services;
using Modules.Tax.DTOs;
using Modules.Tax.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Tax.Repositories;

public sealed class TaxLifecycleRepository(NpgsqlDataSource dataSource, ITransactionCoordinator transactions, ILegalEntityFinancialReadStore legalEntities)
{
    public async Task<TaxReferenceDataDto> ReadReferenceDataAsync(Guid tenantId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection=await dataSource.OpenConnectionAsync(ct);await using var tx=await connection.BeginTransactionAsync(ct);await SetTenantAsync(connection,tx,tenantId,ct);
        var schemes=await ReadRowsAsync(connection,tx,"select code,country_code,name,status,source_kind,mutable from foundation_tax.tax_scheme where tenant_id=$1 and status='Active' order by country_code,code limit 500",tenantId,ct);
        var types=await ReadRowsAsync(connection,tx,"select code,scheme_code,name,status,source_kind,mutable from foundation_tax.tax_type where tenant_id=$1 and status='Active' order by scheme_code,code limit 500",tenantId,ct);
        var categories=await ReadRowsAsync(connection,tx,"select id,code,name,scope,status,source_kind,mutable from foundation_tax.tax_category where tenant_id=$1 and status='Active' order by scope,code limit 500",tenantId,ct);
        var jurisdictions=await ReadRowsAsync(connection,tx,"select code,country_code,parent_code,subdivision_type,name,status,source_kind,mutable from foundation_tax.tax_jurisdiction where tenant_id=$1 and status='Active' order by country_code,code limit 500",tenantId,ct);
        var policies=await ReadRowsAsync(connection,tx,"select legal_entity_id,policy_code,rounding_strategy,money_scale,tax_date_basis,timezone_id,version,'TenantConfiguration' as source_kind,true as mutable from foundation_tax.tax_policy where tenant_id=$1 and legal_entity_id=$2",tenantId,ct,legalEntityId);
        var treatments=Enum.GetNames<TaxTreatment>().Select(code=>(object)new{code,sourceKind="SystemReference",mutable=false}).ToArray();
        return new(schemes,types,treatments,categories,jurisdictions,policies);
    }
    public async Task<TaxRuleSetDto> CreateRuleSetAsync(Guid tenantId, Guid actorId, Guid correlationId, TaxRuleSetRequest request, CancellationToken cancellationToken)
    {
        if (request.Version < 1 || request.EffectiveTo is { } to && to <= request.EffectiveFrom) throw new TaxException("tax.rule_set_period_invalid", "Rule Set version and effective period are invalid.");
        if (request.SourceKind != "TenantConfiguration" || !request.Mutable) throw new TaxException("tax.rule_set_source_locked", "Tenant administrators can create only mutable Tenant Configuration rule sets.");
        if(request.LegalEntityId is not { } entityId || await legalEntities.GetTimeZoneAsync(new(tenantId),new(entityId),cancellationToken) is null)throw new TaxException("tax.legal_entity_not_found","Rule Set requires a Legal Entity in this tenant.");
        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<TaxRuleSetDto>(new(tenantId), async (context, token) =>
        {
            if(request.CountryCode.Length!=2||request.CountryCode.Any(character=>!char.IsAsciiLetterUpper(character)))throw new TaxException("tax.country_code_invalid","Country Code must be two uppercase letters.");
            if(request.SchemeCode is { } scheme&&!await ScalarAsync<bool>(context,"select exists(select 1 from foundation_tax.tax_scheme where tenant_id=$1 and code=$2 and country_code=$3 and status='Active')",token,tenantId,scheme,request.CountryCode))throw new TaxException("tax.scheme_not_found","Tax Scheme was not found for this tenant and country.");
            const string sql = "insert into foundation_tax.tax_rule_set(tenant_id,id,legal_entity_id,country_code,scheme_code,version,status,effective_from,effective_to,source_kind,mutable,created_at) values($1,$2,$3,$4,$5,$6,'Draft',$7,$8,'TenantConfiguration',true,now()) returning status";
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue((object?)request.LegalEntityId ?? DBNull.Value); command.Parameters.AddWithValue(request.CountryCode); command.Parameters.AddWithValue((object?)request.SchemeCode ?? DBNull.Value); command.Parameters.AddWithValue(request.Version); command.Parameters.AddWithValue(request.EffectiveFrom); command.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value);
            await command.ExecuteScalarAsync(token);
            await AuditAsync(context, tenantId, actorId, correlationId, "RuleSetCreated", id, null, request, token, "TaxRuleSet", request.LegalEntityId);
            return new TaxRuleSetDto(id, request.LegalEntityId, request.CountryCode, request.SchemeCode, request.Version, "Draft", request.EffectiveFrom, request.EffectiveTo, "TenantConfiguration", true);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<TaxRuleSetDto>> ListRuleSetsAsync(Guid tenantId, Guid? legalEntityId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,country_code,scheme_code,version,status,effective_from,effective_to,source_kind,mutable from foundation_tax.tax_rule_set where tenant_id=$1 and ($2::uuid is null or legal_entity_id is null or legal_entity_id=$2) order by country_code,scheme_code,version desc limit 200", connection, transaction);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var results = new List<TaxRuleSetDto>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(ReadRuleSet(reader));
        return results;
    }

    public async Task<TaxRuleSetDto?> GetRuleSetAsync(Guid tenantId, Guid id, Guid legalEntityId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,country_code,scheme_code,version,status,effective_from,effective_to,source_kind,mutable from foundation_tax.tax_rule_set where tenant_id=$1 and id=$2 and (legal_entity_id is null or legal_entity_id=$3)", connection, transaction);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(legalEntityId); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRuleSet(reader) : null;
    }

    public async Task<IReadOnlyList<TaxRuleDto>> ListRulesAsync(Guid tenantId,Guid ruleSetId,Guid legalEntityId,CancellationToken ct)
    {
        await using var c=await dataSource.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);await SetTenantAsync(c,tx,tenantId,ct);await using var cmd=new NpgsqlCommand("select r.id,r.rule_set_id,r.stack_id,r.explicit_priority,v.treatment,r.tax_date_from,r.tax_date_to,s.source_kind,v.locked_by_country_pack from foundation_tax.tax_rule r join foundation_tax.tax_rule_version v on v.tenant_id=r.tenant_id and v.id=r.rule_version_id join foundation_tax.tax_rule_set s on s.tenant_id=r.tenant_id and s.id=r.rule_set_id where r.tenant_id=$1 and r.rule_set_id=$2 and (s.legal_entity_id is null or s.legal_entity_id=$3) order by r.explicit_priority desc,r.tax_date_from",c,tx);cmd.Parameters.AddWithValue(tenantId);cmd.Parameters.AddWithValue(ruleSetId);cmd.Parameters.AddWithValue(legalEntityId);await using var reader=await cmd.ExecuteReaderAsync(ct);var results=new List<TaxRuleDto>();while(await reader.ReadAsync(ct))results.Add(new(reader.GetGuid(0),reader.GetGuid(1),reader.IsDBNull(2)?null:reader.GetGuid(2),reader.GetInt32(3),reader.GetString(4),reader.GetFieldValue<DateOnly>(5),reader.IsDBNull(6)?null:reader.GetFieldValue<DateOnly>(6),reader.GetString(7),reader.GetBoolean(8)));return results;
    }

    public async Task<TaxRuleDto> UpdateRuleAsync(Guid tenantId,Guid actorId,Guid correlationId,Guid ruleId,Guid legalEntityId,TaxRuleRequest request,CancellationToken ct)
    {
        ValidateRuleRequest(request,out var treatment);
        return await transactions.ExecuteAsync<TaxRuleDto>(new(tenantId),async(context,token)=>
        {
            await using var read=new NpgsqlCommand("select r.rule_set_id,r.rule_version_id,s.source_kind,s.mutable,s.status,v.locked_by_country_pack from foundation_tax.tax_rule r join foundation_tax.tax_rule_set s on s.tenant_id=r.tenant_id and s.id=r.rule_set_id join foundation_tax.tax_rule_version v on v.tenant_id=r.tenant_id and v.id=r.rule_version_id where r.tenant_id=$1 and r.id=$2 and (s.legal_entity_id is null or s.legal_entity_id=$3) for update",context.Connection,context.Transaction);read.Parameters.AddWithValue(tenantId);read.Parameters.AddWithValue(ruleId);read.Parameters.AddWithValue(legalEntityId);await using var reader=await read.ExecuteReaderAsync(token);if(!await reader.ReadAsync(token))throw new TaxException("tax.rule_not_found","Tax Rule was not found.");var setId=reader.GetGuid(0);var versionId=reader.GetGuid(1);var source=reader.GetString(2);var mutable=reader.GetBoolean(3);var status=reader.GetString(4);var locked=reader.GetBoolean(5);await reader.CloseAsync();TaxRuleSetLifecycle.EnsureEditable(source,mutable,locked||request.LockedByCountryPack);if(status!="Draft")throw new TaxException("tax.rule_not_editable","Rules can be changed only while the Rule Set is Draft.");
            await using(var version=new NpgsqlCommand("update foundation_tax.tax_rule_version set treatment=$3,effective_from=$4,effective_to=$5,source_reference=$6,legal_reason_code=$7 where tenant_id=$1 and id=$2",context.Connection,context.Transaction)){version.Parameters.AddWithValue(tenantId);version.Parameters.AddWithValue(versionId);version.Parameters.AddWithValue(treatment.ToString());version.Parameters.AddWithValue(request.EffectiveFrom);version.Parameters.AddWithValue((object?)request.EffectiveTo??DBNull.Value);version.Parameters.AddWithValue(request.SourceReference);version.Parameters.AddWithValue((object?)request.LegalReasonCode??DBNull.Value);await version.ExecuteNonQueryAsync(token);}
            await using(var update=new NpgsqlCommand("update foundation_tax.tax_rule set stack_id=$3,explicit_priority=$4,registration_id=$5,jurisdiction_code=$6,direction=$7,party_category_id=$8,product_category_id=$9,document_kind=$10,transaction_type=$11,place_of_supply_country=$12,tax_date_from=$13,tax_date_to=$14,legal_reason_code=$15 where tenant_id=$1 and id=$2",context.Connection,context.Transaction)){update.Parameters.AddWithValue(tenantId);update.Parameters.AddWithValue(ruleId);update.Parameters.AddWithValue((object?)request.StackId??DBNull.Value);update.Parameters.AddWithValue(request.Priority);update.Parameters.AddWithValue((object?)request.RegistrationId??DBNull.Value);update.Parameters.AddWithValue((object?)request.JurisdictionCode??DBNull.Value);update.Parameters.AddWithValue((object?)request.Direction??DBNull.Value);update.Parameters.AddWithValue((object?)request.PartyCategoryId??DBNull.Value);update.Parameters.AddWithValue((object?)request.ProductCategoryId??DBNull.Value);update.Parameters.AddWithValue((object?)request.DocumentKind??DBNull.Value);update.Parameters.AddWithValue((object?)request.TransactionType??DBNull.Value);update.Parameters.AddWithValue((object?)request.PlaceOfSupplyCountryCode??DBNull.Value);update.Parameters.AddWithValue(request.EffectiveFrom);update.Parameters.AddWithValue((object?)request.EffectiveTo??DBNull.Value);update.Parameters.AddWithValue((object?)request.LegalReasonCode??DBNull.Value);await update.ExecuteNonQueryAsync(token);}
            await AuditAsync(context,tenantId,actorId,correlationId,"RuleChanged",ruleId,null,request,token,"TaxRule",legalEntityId);return new TaxRuleDto(ruleId,setId,request.StackId,request.Priority,treatment.ToString(),request.EffectiveFrom,request.EffectiveTo,source,locked);
        },ct);
    }

    public async Task<TaxRuleDto> AddRuleAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid ruleSetId, Guid legalEntityId, TaxRuleRequest request, CancellationToken cancellationToken)
    {
        ValidateRuleRequest(request,out var treatment);
        var ruleId = Guid.CreateVersion7(); var versionId = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<TaxRuleDto>(new(tenantId), async (context, token) =>
        {
            await using var set = new NpgsqlCommand("select source_kind,mutable,status,legal_entity_id,effective_from,effective_to from foundation_tax.tax_rule_set where tenant_id=$1 and id=$2 and (legal_entity_id is null or legal_entity_id=$3) for update", context.Connection, context.Transaction);
            set.Parameters.AddWithValue(tenantId); set.Parameters.AddWithValue(ruleSetId); set.Parameters.AddWithValue(legalEntityId); await using var reader = await set.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new TaxException("tax.rule_set_not_found", "Tax Rule Set was not found.");
            var source = reader.GetString(0); var mutable = reader.GetBoolean(1); var status = reader.GetString(2); var entityId = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3);var setFrom=reader.GetFieldValue<DateOnly>(4);DateOnly? setTo=reader.IsDBNull(5)?null:reader.GetFieldValue<DateOnly>(5); await reader.CloseAsync();
            TaxRuleSetLifecycle.EnsureEditable(source, mutable, request.LockedByCountryPack);
            if (status != "Draft") throw new TaxException("tax.rule_set_not_editable", "Rules can be changed only while the Rule Set is Draft.");
            if(request.LockedByCountryPack)throw new TaxException("tax.rule_locked","Only the Country Pack pipeline can create locked statutory rules.");
            if(request.EffectiveFrom<setFrom||setTo is{}setEnd&&(request.EffectiveTo is null||request.EffectiveTo>setEnd))throw new TaxException("tax.rule_period_invalid","Rule effective period must fit within its Rule Set period.");
            if(request.Direction is{}direction&&!Enum.TryParse<TaxDirection>(direction,true,out _))throw new TaxException("tax.direction_invalid","Rule Direction is not supported.");
            if(request.StackId is{}stackId&&!await ScalarAsync<bool>(context,"select exists(select 1 from foundation_tax.tax_stack_definition where tenant_id=$1 and id=$2 and rule_set_id=$3 and status='Draft')",token,tenantId,stackId,ruleSetId))throw new TaxException("tax.stack_not_found","Tax Stack was not found in this Draft Rule Set.");
            if(request.StackId is null&&treatment is not(TaxTreatment.ZeroRated or TaxTreatment.Exempt or TaxTreatment.OutOfScope))throw new TaxException("tax.rule_stack_required","This Tax Treatment requires an ordered Tax Stack.");
            if (request.RegistrationId is { } registration && !await ScalarAsync<bool>(context, "select exists(select 1 from foundation_tax.tax_registration where tenant_id=$1 and id=$2)", token, tenantId, registration)) throw new TaxException("tax.registration_not_found", "Referenced Tax Registration was not found in this tenant.");
            if (request.ProductCategoryId is { } productCategory && !await ScalarAsync<bool>(context, "select exists(select 1 from foundation_tax.tax_category where tenant_id=$1 and id=$2 and scope='Product')", token, tenantId, productCategory)) throw new TaxException("tax.category_not_found", "Referenced Product Tax Category was not found.");
            if (request.PartyCategoryId is { } partyCategory && !await ScalarAsync<bool>(context, "select exists(select 1 from foundation_tax.tax_category where tenant_id=$1 and id=$2 and scope='Party')", token, tenantId, partyCategory)) throw new TaxException("tax.category_not_found", "Referenced Party Tax Category was not found.");
            await using (var insertRule = new NpgsqlCommand("insert into foundation_tax.tax_rule(tenant_id,id,rule_set_id,stack_id,rule_version_id,explicit_priority,registration_id,jurisdiction_code,direction,party_category_id,product_category_id,document_kind,transaction_type,place_of_supply_country,tax_date_from,tax_date_to,legal_reason_code) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17)", context.Connection, context.Transaction))
            {
                insertRule.Parameters.AddWithValue(tenantId); insertRule.Parameters.AddWithValue(ruleId); insertRule.Parameters.AddWithValue(ruleSetId); insertRule.Parameters.AddWithValue((object?)request.StackId ?? DBNull.Value); insertRule.Parameters.AddWithValue(versionId); insertRule.Parameters.AddWithValue(request.Priority); insertRule.Parameters.AddWithValue((object?)request.RegistrationId ?? DBNull.Value); insertRule.Parameters.AddWithValue((object?)request.JurisdictionCode ?? DBNull.Value); insertRule.Parameters.AddWithValue((object?)request.Direction ?? DBNull.Value); insertRule.Parameters.AddWithValue((object?)request.PartyCategoryId ?? DBNull.Value); insertRule.Parameters.AddWithValue((object?)request.ProductCategoryId ?? DBNull.Value); insertRule.Parameters.AddWithValue((object?)request.DocumentKind ?? DBNull.Value); insertRule.Parameters.AddWithValue((object?)request.TransactionType ?? DBNull.Value); insertRule.Parameters.AddWithValue((object?)request.PlaceOfSupplyCountryCode ?? DBNull.Value); insertRule.Parameters.AddWithValue(request.EffectiveFrom); insertRule.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value); insertRule.Parameters.AddWithValue((object?)request.LegalReasonCode ?? DBNull.Value); await insertRule.ExecuteNonQueryAsync(token);
            }
            await using (var insertVersion = new NpgsqlCommand("insert into foundation_tax.tax_rule_version(tenant_id,id,rule_id,version,treatment,effective_from,effective_to,source_reference,legal_reason_code,locked_by_country_pack,immutable,created_at) values($1,$2,$3,1,$4,$5,$6,$7,$8,$9,$9,now())", context.Connection, context.Transaction))
            {
                insertVersion.Parameters.AddWithValue(tenantId); insertVersion.Parameters.AddWithValue(versionId); insertVersion.Parameters.AddWithValue(ruleId); insertVersion.Parameters.AddWithValue(treatment.ToString()); insertVersion.Parameters.AddWithValue(request.EffectiveFrom); insertVersion.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value); insertVersion.Parameters.AddWithValue(request.SourceReference); insertVersion.Parameters.AddWithValue((object?)request.LegalReasonCode ?? DBNull.Value); insertVersion.Parameters.AddWithValue(false); await insertVersion.ExecuteNonQueryAsync(token);
            }
            await AuditAsync(context, tenantId, actorId, correlationId, "RuleChanged", ruleId, null, request, token, "TaxRule", legalEntityId);
            return new(ruleId, ruleSetId, request.StackId, request.Priority, treatment.ToString(), request.EffectiveFrom, request.EffectiveTo, source, false);
        }, cancellationToken);
    }

    public async Task ValidateRuleSetAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid ruleSetId, Guid legalEntityId, CancellationToken cancellationToken)
    {
        await transactions.ExecuteAsync<bool>(new(tenantId), async (context, token) =>
        {
            var state = await ScalarAsync<string?>(context, "select status from foundation_tax.tax_rule_set where tenant_id=$1 and id=$2 and (legal_entity_id is null or legal_entity_id=$3) for update", token, tenantId, ruleSetId,legalEntityId) ?? throw new TaxException("tax.rule_set_not_found", "Tax Rule Set was not found.");
            if (state != "Draft" && state != "Approved") throw new TaxException("tax.rule_set_state_invalid", "Only a Draft or Approved Rule Set can be validated.");
            var count = await ScalarAsync<int>(context, "select count(*) from foundation_tax.tax_rule r join foundation_tax.tax_rule_version v on v.tenant_id=r.tenant_id and v.id=r.rule_version_id where r.tenant_id=$1 and r.rule_set_id=$2 and (r.stack_id is not null or v.treatment in ('ZeroRated','Exempt','OutOfScope'))", token, tenantId, ruleSetId);
            if (count == 0) throw new TaxException("tax.rule_set_empty", "A Rule Set needs at least one complete rule before activation.");
            var conflict = await TaxRuleConflictValidator.HasConflictsAsync(context.Connection, context.Transaction, tenantId, ruleSetId, token);
            if (conflict) throw new TaxException("tax.configuration_conflict", "Equal-rank Tax rules overlap and produce contradictory results.");
            await AuditAsync(context, tenantId, actorId, correlationId, "RuleSetValidated", ruleSetId, new { status = state }, new { valid = true }, token, "TaxRuleSet", legalEntityId);
            return true;
        }, cancellationToken);
    }

    public async Task<TaxRuleSetDto> TransitionRuleSetAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid id, Guid legalEntityId, string target, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TaxRuleSetStatus>(target, true, out var next)) throw new TaxException("tax.rule_set_transition_invalid", "Requested Rule Set state is not supported.");
        return await transactions.ExecuteAsync<TaxRuleSetDto>(new(tenantId), async (context, token) =>
        {
            await using var command = new NpgsqlCommand("select legal_entity_id,country_code,scheme_code,version,status,effective_from,effective_to,source_kind,mutable from foundation_tax.tax_rule_set where tenant_id=$1 and id=$2 and (legal_entity_id is null or legal_entity_id=$3) for update", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new TaxException("tax.rule_set_not_found", "Tax Rule Set was not found.");
            var entity = reader.IsDBNull(0) ? (Guid?)null : reader.GetGuid(0); var country = reader.GetString(1); var scheme = reader.IsDBNull(2)?null:reader.GetString(2); var version = reader.GetInt32(3); var current = Enum.Parse<TaxRuleSetStatus>(reader.GetString(4)); var from = reader.GetFieldValue<DateOnly>(5); DateOnly? to = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6); var source = reader.GetString(7); var mutable = reader.GetBoolean(8); await reader.CloseAsync();
            _ = TaxRuleSetLifecycle.Transition(current, next);
            if (next is TaxRuleSetStatus.Approved or TaxRuleSetStatus.Scheduled or TaxRuleSetStatus.Active) await ValidateRuleSetInTransactionAsync(context, tenantId, id, token);
            if (next == TaxRuleSetStatus.Active)
                await using (var prior = new NpgsqlCommand("update foundation_tax.tax_rule_set set status='Superseded',effective_to=$3 where tenant_id=$1 and id<>$2 and country_code=$4 and scheme_code is not distinct from $5 and legal_entity_id is not distinct from $6 and status='Active' and effective_from<$3", context.Connection, context.Transaction)) { prior.Parameters.AddWithValue(tenantId); prior.Parameters.AddWithValue(id); prior.Parameters.AddWithValue(from); prior.Parameters.AddWithValue(country); prior.Parameters.AddWithValue((object?)scheme??DBNull.Value); prior.Parameters.AddWithValue((object?)entity ?? DBNull.Value); await prior.ExecuteNonQueryAsync(token); }
            await using (var update = new NpgsqlCommand("update foundation_tax.tax_rule_set set status=$3,approved_by=case when $3='Approved' then $4 else approved_by end,approved_at=case when $3='Approved' then now() else approved_at end,activated_at=case when $3='Active' then now() else activated_at end where tenant_id=$1 and id=$2", context.Connection, context.Transaction)) { update.Parameters.AddWithValue(tenantId); update.Parameters.AddWithValue(id); update.Parameters.AddWithValue(next.ToString()); update.Parameters.AddWithValue(actorId); await update.ExecuteNonQueryAsync(token); }
            await AuditAsync(context, tenantId, actorId, correlationId, "RuleSet" + next, id, new { status = current.ToString() }, new { status = next.ToString() }, token, "TaxRuleSet", entity);
            if (next == TaxRuleSetStatus.Active) context.AddOutbox(new(Guid.CreateVersion7(), "TaxRuleSetActivatedV1", JsonSerializer.Serialize(new { id, entity, country, scheme, version, effectiveFrom = from }), new(tenantId), DateTimeOffset.UtcNow, correlationId, id, "tax"));
            return new TaxRuleSetDto(id, entity, country, scheme, version, next.ToString(), from, to, source, mutable);
        }, cancellationToken);
    }

    private static async Task ValidateRuleSetInTransactionAsync(Core.Transactions.ITransactionContext context, Guid tenant, Guid id, CancellationToken token)
    {
        var count = await ScalarAsync<int>(context, "select count(*) from foundation_tax.tax_rule r join foundation_tax.tax_rule_version v on v.tenant_id=r.tenant_id and v.id=r.rule_version_id where r.tenant_id=$1 and r.rule_set_id=$2 and (r.stack_id is not null or v.treatment in ('ZeroRated','Exempt','OutOfScope'))", token, tenant, id);
        if (count == 0) throw new TaxException("tax.rule_set_empty", "A Rule Set needs at least one complete rule before activation.");
        var conflict = await TaxRuleConflictValidator.HasConflictsAsync(context.Connection, context.Transaction, tenant, id, token);
        if (conflict) throw new TaxException("tax.configuration_conflict", "Equal-rank Tax rules overlap and produce contradictory results.");
    }

    private static async Task AuditAsync(ITransactionContext context, Guid tenant, Guid actor, Guid correlation, string operation, Guid id, object? before, object? after, CancellationToken token, string entityType = "TaxRuleSet", Guid? legalEntityId = null)
    {
        await using var audit = new NpgsqlCommand("insert into foundation_tax.tax_audit(tenant_id,id,legal_entity_id,actor_id,operation,entity_type,entity_id,before_state,after_state,occurred_at,correlation_id) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9::jsonb,now(),$10)", context.Connection, context.Transaction);
        audit.Parameters.AddWithValue(tenant); audit.Parameters.AddWithValue(Guid.CreateVersion7()); audit.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value); audit.Parameters.AddWithValue(actor); audit.Parameters.AddWithValue(operation); audit.Parameters.AddWithValue(entityType); audit.Parameters.AddWithValue(id); audit.Parameters.AddWithValue(before is null ? DBNull.Value : JsonSerializer.Serialize(before)); audit.Parameters.AddWithValue(after is null ? DBNull.Value : JsonSerializer.Serialize(after)); audit.Parameters.AddWithValue(correlation); await audit.ExecuteNonQueryAsync(token);
    }
    private static void ValidateRuleRequest(TaxRuleRequest request, out TaxTreatment treatment)
    {
        if (!Enum.TryParse(request.Treatment, true, out treatment) || !Enum.IsDefined(treatment))
            throw new TaxException("tax.treatment_invalid", "Tax Treatment is not supported.");
        if (request.EffectiveTo is { } to && to <= request.EffectiveFrom)
            throw new TaxException("tax.rule_period_invalid", "Rule effective period is invalid.");
        if (string.IsNullOrWhiteSpace(request.SourceReference))
            throw new TaxException("tax.source_reference_required", "A source reference is required for each Tax Rule.");
        if (request.Priority < 0)
            throw new TaxException("tax.rule_priority_invalid", "Rule priority cannot be negative.");
    }
    private static async Task<T> ScalarAsync<T>(ITransactionContext context, string sql, CancellationToken token, params object[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); for (var index=0; index<parameters.Length; index++) command.Parameters.AddWithValue(parameters[index] ?? DBNull.Value); var value = await command.ExecuteScalarAsync(token); if (value is null or DBNull) return default!; return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
    private static TaxRuleSetDto ReadRuleSet(NpgsqlDataReader reader) => new(reader.GetGuid(0),reader.IsDBNull(1)?null:reader.GetGuid(1),reader.GetString(2),reader.IsDBNull(3)?null:reader.GetString(3),reader.GetInt32(4),reader.GetString(5),reader.GetFieldValue<DateOnly>(6),reader.IsDBNull(7)?null:reader.GetFieldValue<DateOnly>(7),reader.GetString(8),reader.GetBoolean(9));
    private static async Task<IReadOnlyList<object>> ReadRowsAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string sql,Guid tenant,CancellationToken ct,params object[] args)
    {await using var command=new NpgsqlCommand(sql,connection,transaction);command.Parameters.AddWithValue(tenant);foreach(var arg in args)command.Parameters.AddWithValue(arg??DBNull.Value);await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<object>();while(await reader.ReadAsync(ct)){var row=new Dictionary<string,object?>();for(var index=0;index<reader.FieldCount;index++)row[reader.GetName(index)]=reader.IsDBNull(index)?null:reader.GetValue(index);rows.Add(row);}return rows;}
    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenant, CancellationToken token) { await using var command=new NpgsqlCommand("select set_config('app.tenant_id',$1,true)",connection,transaction); command.Parameters.AddWithValue(tenant.ToString("D")); await command.ExecuteNonQueryAsync(token); }
}
