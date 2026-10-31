using System.Globalization;
using System.Text.Json;
using Core.Transactions;
using Modules.Tax.DTOs;
using Modules.Tax.Entities;
using Npgsql;
using Shared.Contracts.Tax;

namespace Modules.Tax.Repositories;

public sealed class TaxRepository(NpgsqlDataSource dataSource, ITransactionCoordinator transactions) : ITaxRepository
{
    public sealed record TaxPolicyResolution(TaxDatePolicy DatePolicy, TaxRoundingStrategy RoundingStrategy, int MoneyScale);
    public sealed record TaxRegistrationEvidence(Guid Id, string CountryCode, string SchemeCode, string? RegistrationNumber, string Status, long Version, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? EvidenceReference);
    public sealed record TaxProfileEvidence(Guid Id, long Version, Guid? CategoryId, string? Treatment, string? CountryCode, string? SchemeCode, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? EvidenceReference);
    public sealed record TaxProfileResolution(Guid? RegistrationId, Guid? ProductCategoryId, Guid? PartyCategoryId, string? PartyTreatment, string RegistrationStatus, TaxRegistrationEvidence? RegistrationEvidence = null, TaxProfileEvidence? PartyEvidence = null, TaxProfileEvidence? ProductEvidence = null);

    public async Task<TaxSnapshotContract?> FindSnapshotAsync(Guid tenantId, string sourceDocumentKind, Guid sourceDocumentId, Guid? sourceLineId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select result::text from foundation_tax.tax_snapshot where tenant_id=$1 and source_document_kind=$2 and source_document_id=$3 and source_line_id is not distinct from $4::uuid", connection, transaction);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(sourceDocumentKind); command.Parameters.AddWithValue(sourceDocumentId); command.Parameters.AddWithValue((object?)sourceLineId ?? DBNull.Value);
        var json = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<TaxSnapshotContract>(json) ?? throw new TaxException("tax.snapshot_invalid", "The stored Tax Snapshot could not be read.");
    }

    public async Task<TaxSnapshotContract> SaveSnapshotAsync(Guid tenantId, Guid legalEntityId, TaxSnapshotContract snapshot, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        var serialized = JsonSerializer.Serialize(snapshot);
        await using (var insert = new NpgsqlCommand("insert into foundation_tax.tax_snapshot(tenant_id,id,legal_entity_id,source_document_kind,source_document_id,source_line_id,tax_date,calculation_version,rule_set_version,evaluation_state,treatment,result,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12::jsonb,$13) on conflict do nothing", connection, transaction))
        {
            insert.Parameters.AddWithValue(tenantId); insert.Parameters.AddWithValue(snapshot.SnapshotId); insert.Parameters.AddWithValue(legalEntityId);
            insert.Parameters.AddWithValue(snapshot.SourceDocumentKind); insert.Parameters.AddWithValue(snapshot.SourceDocumentId); insert.Parameters.AddWithValue((object?)snapshot.SourceLineId ?? DBNull.Value);
            insert.Parameters.AddWithValue(DateOnly.ParseExact(snapshot.TaxDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)); insert.Parameters.AddWithValue(snapshot.CalculationVersion);
            insert.Parameters.AddWithValue((object?)snapshot.RuleSetVersion ?? DBNull.Value); insert.Parameters.AddWithValue(snapshot.EvaluationState); insert.Parameters.AddWithValue((object?)snapshot.Result.Treatment ?? DBNull.Value);
            insert.Parameters.AddWithValue(serialized); insert.Parameters.AddWithValue(snapshot.CapturedAt);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var read = new NpgsqlCommand("select result::text from foundation_tax.tax_snapshot where tenant_id=$1 and source_document_kind=$2 and source_document_id=$3 and source_line_id is not distinct from $4::uuid", connection, transaction);
        read.Parameters.AddWithValue(tenantId); read.Parameters.AddWithValue(snapshot.SourceDocumentKind); read.Parameters.AddWithValue(snapshot.SourceDocumentId); read.Parameters.AddWithValue((object?)snapshot.SourceLineId ?? DBNull.Value);
        var stored = (string?)await read.ExecuteScalarAsync(cancellationToken) ?? throw new TaxException("tax.snapshot_persist_failed", "The Tax Snapshot could not be persisted.");
        await transaction.CommitAsync(cancellationToken);
        return JsonSerializer.Deserialize<TaxSnapshotContract>(stored) ?? throw new TaxException("tax.snapshot_invalid", "The persisted Tax Snapshot could not be read.");
    }

    public async Task<TaxPolicyResolution> GetPolicyAsync(Guid tenantId, Guid legalEntityId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select rounding_strategy,money_scale,tax_date_basis,timezone_id,version from foundation_tax.tax_policy where tenant_id=$1 and legal_entity_id=$2", connection, transaction);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(legalEntityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new TaxException("tax.policy_missing", "Tax Policy must be configured for this legal entity before Tax can be evaluated.");
        return new(new(Enum.Parse<TaxDateBasis>(reader.GetString(2)), reader.GetString(3), reader.GetInt64(4)), Enum.Parse<TaxRoundingStrategy>(reader.GetString(0)), reader.GetInt16(1));
    }

    public async Task<TaxCapabilityDto?> GetCapabilityAsync(Guid tenantId, Guid legalEntityId, DateOnly at, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select capability,effective_from,effective_to,version from foundation_tax.tax_capability_state where tenant_id=$1 and legal_entity_id=$2 and effective_from <= $3 and (effective_to is null or effective_to > $3) order by effective_from desc limit 1", connection, transaction);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(at);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(legalEntityId, reader.GetString(0), reader.GetFieldValue<DateOnly>(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2), reader.GetInt64(3)) : null;
    }

    public async Task<TaxProfileResolution> ResolveProfilesAsync(Guid tenantId, TaxCalculationContext context, DateOnly taxDate, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        Guid? resolvedRegistrationId = null; Guid? partyCategory = null; string? partyTreatment = null; string registrationStatus = "NotRegistered"; TaxRegistrationEvidence? registrationEvidence = null; TaxProfileEvidence? partyEvidence = null; TaxProfileEvidence? productEvidence = null;
        if (context.RegistrationId is { } requestedRegistration)
        {
            const string registrationSql = "select r.id,r.status,r.country_code,r.scheme_code,r.registration_number,r.version,r.effective_from,r.effective_to,r.evidence_reference from foundation_tax.tax_registration r join foundation_tax.tax_registration_member m on m.tenant_id=r.tenant_id and m.registration_id=r.id where r.tenant_id=$1 and r.id=$2 and m.legal_entity_id=$3 and m.membership_status='Active' and r.effective_from <= $4 and (r.effective_to is null or r.effective_to > $4) and m.effective_from <= $4 and (m.effective_to is null or m.effective_to > $4) and ($5::text is null or r.scheme_code=$5)";
            await using var registration = new NpgsqlCommand(registrationSql, connection, transaction);
            registration.Parameters.AddWithValue(tenantId); registration.Parameters.AddWithValue(requestedRegistration); registration.Parameters.AddWithValue(context.LegalEntityId); registration.Parameters.AddWithValue(taxDate); registration.Parameters.AddWithValue((object?)context.TaxSchemeCode ?? DBNull.Value);
            await using var reader = await registration.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new TaxException("tax.registration_not_available", "The selected Tax Registration is not active for this legal entity and Tax Date.");
            registrationEvidence = ReadRegistrationEvidence(reader); registrationStatus = registrationEvidence.Status; resolvedRegistrationId = registrationEvidence.Id;
        }
        else
        {
            const string registrationSql = "select r.id,r.status,r.country_code,r.scheme_code,r.registration_number,r.version,r.effective_from,r.effective_to,r.evidence_reference from foundation_tax.tax_registration r join foundation_tax.tax_registration_member m on m.tenant_id=r.tenant_id and m.registration_id=r.id where r.tenant_id=$1 and m.legal_entity_id=$2 and m.membership_status='Active' and r.effective_from <= $3 and (r.effective_to is null or r.effective_to > $3) and m.effective_from <= $3 and (m.effective_to is null or m.effective_to > $3) and ($4::text is null or r.scheme_code=$4) order by r.effective_from desc limit 2";
            await using var registration = new NpgsqlCommand(registrationSql, connection, transaction);
            registration.Parameters.AddWithValue(tenantId); registration.Parameters.AddWithValue(context.LegalEntityId); registration.Parameters.AddWithValue(taxDate); registration.Parameters.AddWithValue((object?)context.TaxSchemeCode ?? DBNull.Value);
            await using var reader = await registration.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken)) { registrationEvidence = ReadRegistrationEvidence(reader); resolvedRegistrationId = registrationEvidence.Id; registrationStatus = registrationEvidence.Status; if (await reader.ReadAsync(cancellationToken)) throw new TaxException("tax.registration_conflict", "More than one Tax Registration is effective for this legal entity and Tax Date."); }
        }
        if (context.PartyId is { } partyId)
        {
            const string partySql = "select id,category_id,treatment,(legal_entity_id is not null),(country_code is not null),(scheme_code is not null),version,country_code,scheme_code,effective_from,effective_to,exemption_evidence_reference from foundation_tax.party_tax_profile where tenant_id=$1 and party_id=$2 and (legal_entity_id is null or legal_entity_id=$3) and (country_code=$4 or country_code is null) and (scheme_code=$6 or scheme_code is null) and effective_from <= $5 and (effective_to is null or effective_to > $5) order by (legal_entity_id is not null) desc,(country_code is not null) desc,(scheme_code is not null) desc limit 500";
            await using var party = new NpgsqlCommand(partySql, connection, transaction);
            party.Parameters.AddWithValue(tenantId); party.Parameters.AddWithValue(partyId); party.Parameters.AddWithValue(context.LegalEntityId); party.Parameters.AddWithValue((object?)context.PlaceOfSupplyCountryCode ?? (object?)context.PartyCountryCode ?? DBNull.Value); party.Parameters.AddWithValue(taxDate); party.Parameters.AddWithValue((object?)context.TaxSchemeCode ?? DBNull.Value);
            await using var reader = await party.ExecuteReaderAsync(cancellationToken);
            var matches = new List<(TaxProfileEvidence Evidence, string? Treatment, (bool Entity, bool Country, bool Scheme) Rank)>();
            while (await reader.ReadAsync(cancellationToken)) matches.Add((new(reader.GetGuid(0), reader.GetInt64(6), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetFieldValue<DateOnly>(9), reader.IsDBNull(10) ? null : reader.GetFieldValue<DateOnly>(10), reader.IsDBNull(11) ? null : reader.GetString(11)), reader.IsDBNull(2) ? null : reader.GetString(2), (reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5))));
            if (matches.Count > 0)
            {
                partyEvidence = matches[0].Evidence; partyCategory = partyEvidence.CategoryId; partyTreatment = matches[0].Treatment; var rank = matches[0].Rank;
                if (matches.Any(match => match.Rank == rank && (match.Evidence.CategoryId != partyCategory || match.Treatment != partyTreatment)))
                    throw new TaxException("tax.party_profile_conflict", "More than one equally scoped Party Tax Profile matches this legal entity and Tax Date.");
            }
        }
        Guid? productCategory = null;
        if (context.ProductTemplateId is { } templateId)
        {
            const string productSql = "select id,category_id,(product_variant_id is not null),(legal_entity_id is not null),(country_code is not null),(scheme_code is not null),version,country_code,scheme_code,effective_from,effective_to,null::text from foundation_tax.product_tax_profile where tenant_id=$1 and product_template_id=$2 and (product_variant_id is null or product_variant_id=$3) and (legal_entity_id is null or legal_entity_id=$4) and (country_code=$5 or country_code is null) and (scheme_code=$7 or scheme_code is null) and effective_from <= $6 and (effective_to is null or effective_to > $6) order by (product_variant_id is not null) desc,(legal_entity_id is not null) desc,(country_code is not null) desc,(scheme_code is not null) desc limit 500";
            await using var product = new NpgsqlCommand(productSql, connection, transaction);
            product.Parameters.AddWithValue(tenantId); product.Parameters.AddWithValue(templateId); product.Parameters.AddWithValue((object?)context.ProductVariantId ?? DBNull.Value); product.Parameters.AddWithValue(context.LegalEntityId); product.Parameters.AddWithValue((object?)context.PlaceOfSupplyCountryCode ?? (object?)context.PartyCountryCode ?? DBNull.Value); product.Parameters.AddWithValue(taxDate); product.Parameters.AddWithValue((object?)context.TaxSchemeCode ?? DBNull.Value);
            await using var reader = await product.ExecuteReaderAsync(cancellationToken);
            var matches = new List<(TaxProfileEvidence Evidence, (bool Variant, bool Entity, bool Country, bool Scheme) Rank)>();
            while (await reader.ReadAsync(cancellationToken)) matches.Add((new(reader.GetGuid(0), reader.GetInt64(6), reader.IsDBNull(1) ? null : reader.GetGuid(1), null, reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetFieldValue<DateOnly>(9), reader.IsDBNull(10) ? null : reader.GetFieldValue<DateOnly>(10), reader.IsDBNull(11) ? null : reader.GetString(11)), (reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5))));
            if (matches.Count > 0)
            {
                productEvidence = matches[0].Evidence; productCategory = productEvidence.CategoryId; var rank = matches[0].Rank;
                if (matches.Any(match => match.Rank == rank && match.Evidence.CategoryId != productCategory))
                    throw new TaxException("tax.product_profile_conflict", "More than one equally scoped Product Tax Profile matches this legal entity and Tax Date.");
            }
        }
        return new(resolvedRegistrationId, productCategory, partyCategory, partyTreatment, registrationStatus, registrationEvidence, partyEvidence, productEvidence);
    }

    public async Task<IReadOnlyDictionary<int, TaxProfileResolution>> ResolveProfilesBatchAsync(Guid tenantId, IReadOnlyList<TaxBatchContext> contexts, CancellationToken cancellationToken)
    {
        if (contexts.Count == 0) return new Dictionary<int, TaxProfileResolution>();
        var json = JsonSerializer.Serialize(contexts.Select(item => new { idx = item.Index, entity = item.Context.LegalEntityId, tax_date = item.TaxDate, registration = item.Context.RegistrationId, scheme = item.Context.TaxSchemeCode, party = item.Context.PartyId, country = item.Context.PlaceOfSupplyCountryCode ?? item.Context.PartyCountryCode, template = item.Context.ProductTemplateId, variant = item.Context.ProductVariantId }));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        var rows = contexts.ToDictionary(item => item.Index, _ => new MutableProfileResolution());
        const string registrationSql = "with q as (select * from jsonb_to_recordset($2::jsonb) as x(idx int,entity uuid,tax_date date,registration uuid,scheme text)) select q.idx,r.id,r.status,r.country_code,r.scheme_code,r.registration_number,r.version,r.effective_from,r.effective_to,r.evidence_reference from q join foundation_tax.tax_registration r on r.tenant_id=$1 and (q.registration is null or r.id=q.registration) and (q.scheme is null or r.scheme_code=q.scheme) and r.effective_from<=q.tax_date and (r.effective_to is null or r.effective_to>q.tax_date) join foundation_tax.tax_registration_member m on m.tenant_id=r.tenant_id and m.registration_id=r.id and m.legal_entity_id=q.entity and m.membership_status='Active' and m.effective_from<=q.tax_date and (m.effective_to is null or m.effective_to>q.tax_date) order by q.idx,r.effective_from desc";
        await using (var command = new NpgsqlCommand(registrationSql, connection, transaction))
        {
            command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(json);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var matches = new Dictionary<int, TaxRegistrationEvidence>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var index = reader.GetInt32(0); var match = new TaxRegistrationEvidence(reader.GetGuid(1), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(2), reader.GetInt64(6), reader.GetFieldValue<DateOnly>(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateOnly>(8), reader.IsDBNull(9) ? null : reader.GetString(9));
                if (matches.ContainsKey(index)) throw new TaxException("tax.registration_conflict", "More than one Tax Registration is effective for this legal entity and Tax Date.");
                matches[index] = match;
            }
            foreach (var match in matches) { rows[match.Key].RegistrationId = match.Value.Id; rows[match.Key].RegistrationStatus = match.Value.Status; rows[match.Key].RegistrationEvidence = match.Value; }
        }
        const string partySql = "with q as (select * from jsonb_to_recordset($2::jsonb) as x(idx int,entity uuid,tax_date date,party uuid,scheme text,country text)) select q.idx,p.category_id,p.treatment,(p.legal_entity_id is not null),(p.country_code is not null),(p.scheme_code is not null),p.id,p.version,p.country_code,p.scheme_code,p.effective_from,p.effective_to,p.exemption_evidence_reference from q join foundation_tax.party_tax_profile p on p.tenant_id=$1 and p.party_id=q.party and (p.legal_entity_id is null or p.legal_entity_id=q.entity) and (p.country_code is null or p.country_code=q.country) and (p.scheme_code is null or p.scheme_code=q.scheme) and p.effective_from<=q.tax_date and (p.effective_to is null or p.effective_to>q.tax_date) order by q.idx,(p.legal_entity_id is not null) desc,(p.country_code is not null) desc,(p.scheme_code is not null) desc";
        await using (var command = new NpgsqlCommand(partySql, connection, transaction))
        {
            command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(json);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var best = new Dictionary<int, (int Entity, int Country, int Scheme, TaxProfileEvidence Evidence, string? Treatment)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var index = reader.GetInt32(0); var rank = (reader.GetBoolean(3) ? 1 : 0, reader.GetBoolean(4) ? 1 : 0, reader.GetBoolean(5) ? 1 : 0);
                var evidence = new TaxProfileEvidence(reader.GetGuid(6), reader.GetInt64(7), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetFieldValue<DateOnly>(10), reader.IsDBNull(11) ? null : reader.GetFieldValue<DateOnly>(11), reader.IsDBNull(12) ? null : reader.GetString(12));
                var value = (rank.Item1, rank.Item2, rank.Item3, evidence, reader.IsDBNull(2) ? null : reader.GetString(2));
                if (best.TryGetValue(index, out var current))
                {
                    if ((current.Entity, current.Country, current.Scheme) == rank && (current.Evidence.CategoryId != evidence.CategoryId || current.Treatment != value.Item5)) throw new TaxException("tax.party_profile_conflict", "More than one equally scoped Party Tax Profile matches this legal entity and Tax Date.");
                    continue;
                }
                best[index] = value;
            }
            foreach (var match in best) { rows[match.Key].PartyCategoryId = match.Value.Evidence.CategoryId; rows[match.Key].PartyTreatment = match.Value.Treatment; rows[match.Key].PartyEvidence = match.Value.Evidence; }
        }
        const string productSql = "with q as (select * from jsonb_to_recordset($2::jsonb) as x(idx int,entity uuid,tax_date date,template uuid,variant uuid,scheme text,country text)) select q.idx,p.category_id,(p.product_variant_id is not null),(p.legal_entity_id is not null),(p.country_code is not null),(p.scheme_code is not null),p.id,p.version,p.country_code,p.scheme_code,p.effective_from,p.effective_to from q join foundation_tax.product_tax_profile p on p.tenant_id=$1 and p.product_template_id=q.template and (p.product_variant_id is null or p.product_variant_id=q.variant) and (p.legal_entity_id is null or p.legal_entity_id=q.entity) and (p.country_code is null or p.country_code=q.country) and (p.scheme_code is null or p.scheme_code=q.scheme) and p.effective_from<=q.tax_date and (p.effective_to is null or p.effective_to>q.tax_date) order by q.idx,(p.product_variant_id is not null) desc,(p.legal_entity_id is not null) desc,(p.country_code is not null) desc,(p.scheme_code is not null) desc";
        await using (var command = new NpgsqlCommand(productSql, connection, transaction))
        {
            command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(json);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var best = new Dictionary<int, (int Variant, int Entity, int Country, int Scheme, TaxProfileEvidence Evidence)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var index = reader.GetInt32(0); var rank = (reader.GetBoolean(2) ? 1 : 0, reader.GetBoolean(3) ? 1 : 0, reader.GetBoolean(4) ? 1 : 0, reader.GetBoolean(5) ? 1 : 0); var evidence = new TaxProfileEvidence(reader.GetGuid(6), reader.GetInt64(7), reader.IsDBNull(1) ? null : reader.GetGuid(1), null, reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetFieldValue<DateOnly>(10), reader.IsDBNull(11) ? null : reader.GetFieldValue<DateOnly>(11), null);
                if (best.TryGetValue(index, out var current))
                {
                    if ((current.Variant, current.Entity, current.Country, current.Scheme) == rank && current.Evidence.CategoryId != evidence.CategoryId) throw new TaxException("tax.product_profile_conflict", "More than one equally scoped Product Tax Profile matches this legal entity and Tax Date.");
                    continue;
                }
                best[index] = (rank.Item1, rank.Item2, rank.Item3, rank.Item4, evidence);
            }
            foreach (var match in best) { rows[match.Key].ProductCategoryId = match.Value.Evidence.CategoryId; rows[match.Key].ProductEvidence = match.Value.Evidence; }
        }
        return rows.ToDictionary(pair => pair.Key, pair => new TaxProfileResolution(pair.Value.RegistrationId, pair.Value.ProductCategoryId, pair.Value.PartyCategoryId, pair.Value.PartyTreatment, pair.Value.RegistrationStatus, pair.Value.RegistrationEvidence, pair.Value.PartyEvidence, pair.Value.ProductEvidence));
    }

    public async Task<IReadOnlyDictionary<int, (TaxRuleCandidate Rule, string PackCode, string PackVersion)>> FindRulesBatchAsync(Guid tenantId, IReadOnlyList<TaxBatchRuleContext> contexts, CancellationToken cancellationToken)
    {
        if (contexts.Count == 0) return new Dictionary<int, (TaxRuleCandidate, string, string)>();
        var json = JsonSerializer.Serialize(contexts.Select(item => new { idx = item.Index, entity = item.Context.LegalEntityId, tax_date = item.TaxDate, registration = item.Context.RegistrationId, jurisdiction = item.Context.JurisdictionCode, direction = item.Context.Direction.ToString(), party_category = item.Context.PartyTaxCategoryId, product_category = item.Context.ProductTaxCategoryId, document_kind = item.Context.DocumentKind, transaction_type = item.Context.TransactionType, place = item.Context.PlaceOfSupplyCountryCode, scheme = item.Context.TaxSchemeCode, country = item.Context.PlaceOfSupplyCountryCode?.Split('-')[0] }));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        const string sql = "with q as (select * from jsonb_to_recordset($2::jsonb) as x(idx int,entity uuid,tax_date date,registration uuid,jurisdiction text,direction text,party_category uuid,product_category uuid,document_kind text,transaction_type text,place text,scheme text,country text)) select q.idx,r.id,v.treatment,r.explicit_priority,r.tax_date_from,r.tax_date_to,r.registration_id,r.jurisdiction_code,r.direction,r.party_category_id,r.product_category_id,r.document_kind,r.transaction_type,r.place_of_supply_country,s.source_kind,v.version::text,v.legal_reason_code,r.legal_reason_text,coalesce(a.pack_code,'tenant-config'),coalesce(a.pack_version,'tenant-config'),r.stack_id from q join foundation_tax.tax_rule r on r.tenant_id=$1 and (r.registration_id is null or r.registration_id=q.registration) and (r.jurisdiction_code is null or r.jurisdiction_code=q.jurisdiction) and (r.direction is null or r.direction=q.direction) and (r.party_category_id is null or r.party_category_id=q.party_category) and (r.product_category_id is null or r.product_category_id=q.product_category) and (r.document_kind is null or r.document_kind=q.document_kind) and (r.transaction_type is null or r.transaction_type=q.transaction_type) and (r.place_of_supply_country is null or r.place_of_supply_country=q.place) and q.tax_date>=r.tax_date_from and (r.tax_date_to is null or q.tax_date<r.tax_date_to) join foundation_tax.tax_rule_set s on s.tenant_id=r.tenant_id and s.id=r.rule_set_id and (s.legal_entity_id is null or s.legal_entity_id=q.entity) and (s.scheme_code is null or s.scheme_code=q.scheme) and (s.country_code is null or s.country_code=q.country) and s.status='Active' and q.tax_date>=s.effective_from and (s.effective_to is null or q.tax_date<s.effective_to) join foundation_tax.tax_rule_version v on v.tenant_id=r.tenant_id and v.id=r.rule_version_id and q.tax_date>=v.effective_from and (v.effective_to is null or q.tax_date<v.effective_to) left join foundation_tax.tax_pack_activation a on a.tenant_id=s.tenant_id and a.id=s.pack_activation_id where s.source_kind<>'CountryPack' or a.review_status='Approved'";
        var rawByIndex = new Dictionary<int, List<RawRule>>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(json);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var index = reader.GetInt32(0); var raw = new RawRule(reader.GetGuid(1), Enum.Parse<TaxTreatment>(reader.GetString(2)), reader.GetInt32(3), reader.GetFieldValue<DateOnly>(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : Enum.Parse<TaxDirection>(reader.GetString(8)), reader.IsDBNull(9) ? null : reader.GetGuid(9), reader.IsDBNull(10) ? null : reader.GetGuid(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetString(13), reader.GetString(14), reader.GetString(15), reader.IsDBNull(16) ? null : reader.GetString(16), reader.IsDBNull(17) ? null : reader.GetString(17), reader.GetString(18), reader.GetString(19), reader.IsDBNull(20) ? null : reader.GetGuid(20));
                if (!rawByIndex.TryGetValue(index, out var items)) rawByIndex[index] = items = [];
                items.Add(raw);
            }
        }
        var stackIds = rawByIndex.Values.SelectMany(items => items).Where(raw => raw.StackId is not null).Select(raw => raw.StackId!.Value).Distinct().ToArray();
        var components = new Dictionary<Guid, List<TaxComponent>>();
        if (stackIds.Length > 0)
        {
            const string componentSql = "select c.stack_id,c.id,c.tax_type_code,c.tax_code,c.treatment,c.calculation_method,c.rate,c.fixed_amount,c.currency_code,c.sequence,c.base_component_sequence,c.legal_reason_code,c.reporting_code,c.recoverable_percentage from foundation_tax.tax_component_definition c where c.tenant_id=$1 and c.stack_id=any($2) order by c.stack_id,c.sequence";
            await using var command = new NpgsqlCommand(componentSql, connection, transaction); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(stackIds);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var stackId = reader.GetGuid(0); if (!components.TryGetValue(stackId, out var items)) components[stackId] = items = [];
                items.Add(new(reader.GetGuid(1), reader.GetString(2), reader.GetString(3), Enum.Parse<TaxTreatment>(reader.GetString(4)), Enum.Parse<TaxCalculationMethod>(reader.GetString(5)), reader.IsDBNull(6) ? null : reader.GetDecimal(6), reader.IsDBNull(7) ? null : reader.GetDecimal(7), reader.GetString(8), reader.GetInt32(9), reader.IsDBNull(10) ? null : reader.GetInt32(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetDecimal(13)));
            }
        }
        var results = new Dictionary<int, (TaxRuleCandidate, string, string)>();
        foreach (var (index, raws) in rawByIndex)
        {
            var candidates = raws.Select(raw => new ResolvedTaxRuleCandidate(new(raw.Id, raw.Treatment, raw.Priority, raw.EffectiveFrom, raw.EffectiveTo, raw.RegistrationId, raw.JurisdictionCode, raw.Direction, raw.PartyCategoryId, raw.ProductCategoryId, raw.DocumentKind, raw.TransactionType, raw.PlaceOfSupplyCountryCode, raw.SourceKind == "CountryPack" ? TaxRuleMutability.CountryPack : TaxRuleMutability.TenantConfiguration, raw.RuleVersion, raw.LegalReasonCode, raw.LegalReasonText, raw.StackId is { } stack ? components.GetValueOrDefault(stack) ?? [] : []), raw.PackCode, raw.PackVersion));
            if (TaxRuleResolver.Select(candidates) is { } selected) results[index] = (selected.Rule, selected.PackCode, selected.PackVersion);
        }
        return results;
    }

    public async Task<TaxCapabilityDto> SetCapabilityAsync(Guid tenantId, Guid legalEntityId, Guid actorId, string capability, DateOnly effectiveFrom, Guid correlationId, CancellationToken cancellationToken)
    {
        if (capability is not ("Enabled" or "Disabled")) throw new TaxException("tax.capability_invalid", "Choose whether Tax is enabled for this legal entity.");
        return await transactions.ExecuteAsync(new(tenantId), async (context, token) =>
        {
            var prior = new List<(DateOnly From, DateOnly? To, string Capability, long Version)>();
            await using (var read = new NpgsqlCommand("select effective_from,effective_to,capability,version from foundation_tax.tax_capability_state where tenant_id=$1 and legal_entity_id=$2 and effective_to is null for update", context.Connection, context.Transaction))
            {
                read.Parameters.AddWithValue(tenantId); read.Parameters.AddWithValue(legalEntityId);
                await using var reader = await read.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) prior.Add((reader.GetFieldValue<DateOnly>(0), reader.IsDBNull(1) ? null : reader.GetFieldValue<DateOnly>(1), reader.GetString(2), reader.GetInt64(3)));
            }
            if (prior.Count > 1) throw new TaxException("tax.capability_history_conflict", "Tax capability history contains overlapping active periods.");
            var version = prior.Count == 0 ? 1 : prior[0].Version + 1;
            if (prior.Count == 1)
            {
                if (effectiveFrom <= prior[0].From) throw new TaxException("tax.capability_effective_date_invalid", "A capability change must start after the current effective period begins.");
                await using var close = new NpgsqlCommand("update foundation_tax.tax_capability_state set effective_to=$3,version=version+1,updated_by=$4,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and effective_to is null", context.Connection, context.Transaction);
                close.Parameters.AddWithValue(tenantId); close.Parameters.AddWithValue(legalEntityId); close.Parameters.AddWithValue(effectiveFrom); close.Parameters.AddWithValue(actorId); await close.ExecuteNonQueryAsync(token);
            }
            var id = Guid.CreateVersion7();
            await using (var insert = new NpgsqlCommand("insert into foundation_tax.tax_capability_state(tenant_id,legal_entity_id,capability,effective_from,effective_to,version,updated_by,updated_at) values($1,$2,$3,$4,null,$5,$6,now())", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(tenantId); insert.Parameters.AddWithValue(legalEntityId); insert.Parameters.AddWithValue(capability); insert.Parameters.AddWithValue(effectiveFrom); insert.Parameters.AddWithValue(version); insert.Parameters.AddWithValue(actorId); await insert.ExecuteNonQueryAsync(token);
            }
            await using (var audit = new NpgsqlCommand("insert into foundation_tax.tax_audit(tenant_id,id,legal_entity_id,actor_id,operation,entity_type,entity_id,before_state,after_state,occurred_at,correlation_id) values($1,$2,$3,$4,'CapabilityChanged','TaxCapability',$3,$5::jsonb,$6::jsonb,now(),$7)", context.Connection, context.Transaction))
            {
                audit.Parameters.AddWithValue(tenantId); audit.Parameters.AddWithValue(Guid.CreateVersion7()); audit.Parameters.AddWithValue(legalEntityId); audit.Parameters.AddWithValue(actorId);
                audit.Parameters.AddWithValue(prior.Count == 0 ? DBNull.Value : JsonSerializer.Serialize(new { prior[0].Capability, prior[0].From, prior[0].To })); audit.Parameters.AddWithValue(JsonSerializer.Serialize(new { capability, effectiveFrom, version })); audit.Parameters.AddWithValue(correlationId); await audit.ExecuteNonQueryAsync(token);
            }
            context.AddOutbox(new(Guid.CreateVersion7(), "TaxCapabilityChangedV1", JsonSerializer.Serialize(new { tenantId, legalEntityId, capability, effectiveFrom, version }), new(tenantId), DateTimeOffset.UtcNow, correlationId, correlationId, "tax"));
            return new TaxCapabilityDto(legalEntityId, capability, effectiveFrom, null, version);
        }, cancellationToken);
    }

    public async Task<(TaxRuleCandidate Rule, string PackCode, string PackVersion)?> FindRuleAsync(Guid tenantId, TaxCalculationContext context, DateOnly taxDate, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        const string sql = """
            select r.id,v.treatment,r.explicit_priority,r.tax_date_from,r.tax_date_to,r.registration_id,r.jurisdiction_code,r.direction,r.party_category_id,r.product_category_id,r.document_kind,r.transaction_type,r.place_of_supply_country,s.source_kind,v.version::text,v.legal_reason_code,r.legal_reason_text,
                   coalesce(a.pack_code,'tenant-config'),coalesce(a.pack_version,'tenant-config'),r.stack_id
            from foundation_tax.tax_rule r
            join foundation_tax.tax_rule_version v on v.tenant_id=r.tenant_id and v.id=r.rule_version_id
            join foundation_tax.tax_rule_set s on s.tenant_id=r.tenant_id and s.id=r.rule_set_id
            left join foundation_tax.tax_pack_activation a on a.tenant_id=s.tenant_id and a.id=s.pack_activation_id
            where r.tenant_id=$1 and (s.legal_entity_id is null or s.legal_entity_id=$2) and (s.country_code=$13 or s.country_code is null) and s.status='Active'
              and (s.scheme_code is null or s.scheme_code=$12)
              and $3 >= s.effective_from and (s.effective_to is null or $3 < s.effective_to)
              and (s.source_kind <> 'CountryPack' or a.review_status='Approved')
              and $3 >= v.effective_from and (v.effective_to is null or $3 < v.effective_to)
              and $3 >= r.tax_date_from and (r.tax_date_to is null or $3 < r.tax_date_to)
              and (r.registration_id is null or r.registration_id=$4)
              and (r.jurisdiction_code is null or r.jurisdiction_code=$5)
              and (r.direction is null or r.direction=$6)
              and (r.party_category_id is null or r.party_category_id=$7)
              and (r.product_category_id is null or r.product_category_id=$8)
              and (r.document_kind is null or r.document_kind=$9)
              and (r.transaction_type is null or r.transaction_type=$10)
              and (r.place_of_supply_country is null or r.place_of_supply_country=$11)
            order by r.explicit_priority desc, (r.registration_id is not null)::int desc,(r.jurisdiction_code is not null)::int desc,(r.direction is not null)::int desc,(r.party_category_id is not null)::int desc,(r.product_category_id is not null)::int desc,(r.document_kind is not null)::int desc
            """;
        var rawCandidates = new List<RawRule>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(context.LegalEntityId); command.Parameters.AddWithValue(new DateOnly(taxDate.Year, taxDate.Month, taxDate.Day));
            command.Parameters.AddWithValue((object?)context.RegistrationId ?? DBNull.Value); command.Parameters.AddWithValue((object?)context.JurisdictionCode ?? DBNull.Value);
            command.Parameters.AddWithValue(context.Direction.ToString()); command.Parameters.AddWithValue((object?)context.PartyTaxCategoryId ?? DBNull.Value);
            command.Parameters.AddWithValue((object?)context.ProductTaxCategoryId ?? DBNull.Value); command.Parameters.AddWithValue(context.DocumentKind); command.Parameters.AddWithValue(context.TransactionType); command.Parameters.AddWithValue((object?)context.PlaceOfSupplyCountryCode ?? DBNull.Value); command.Parameters.AddWithValue((object?)context.TaxSchemeCode ?? DBNull.Value); command.Parameters.AddWithValue((object?)context.PlaceOfSupplyCountryCode?.Split('-')[0] ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var ruleId = reader.GetGuid(0); var treatment = Enum.Parse<TaxTreatment>(reader.GetString(1)); var priority = reader.GetInt32(2);
                var from = reader.GetFieldValue<DateOnly>(3); DateOnly? to = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4);
                Guid? registration = reader.IsDBNull(5) ? null : reader.GetGuid(5); var jurisdiction = reader.IsDBNull(6) ? null : reader.GetString(6);
                TaxDirection? direction = reader.IsDBNull(7) ? null : Enum.Parse<TaxDirection>(reader.GetString(7)); Guid? partyCategory = reader.IsDBNull(8) ? null : reader.GetGuid(8); Guid? productCategory = reader.IsDBNull(9) ? null : reader.GetGuid(9);
                string? documentKind = reader.IsDBNull(10) ? null : reader.GetString(10); string? transactionType = reader.IsDBNull(11) ? null : reader.GetString(11); string? placeOfSupplyCountry = reader.IsDBNull(12) ? null : reader.GetString(12);
                var source = reader.GetString(13); var version = reader.GetString(14); var reasonCode = reader.IsDBNull(15) ? null : reader.GetString(15); var reasonText = reader.IsDBNull(16) ? null : reader.GetString(16);
                var packCode = reader.GetString(17); var packVersion = reader.GetString(18); Guid? stackId = reader.IsDBNull(19) ? null : reader.GetGuid(19);
                rawCandidates.Add(new(ruleId, treatment, priority, from, to, registration, jurisdiction, direction, partyCategory, productCategory, documentKind, transactionType, placeOfSupplyCountry, source, version, reasonCode, reasonText, packCode, packVersion, stackId));
            }
        }
        var candidates = new List<ResolvedTaxRuleCandidate>();
        foreach (var raw in rawCandidates)
        {
            var components = raw.StackId is null ? [] : await LoadComponentsAsync(connection, transaction, tenantId, raw.StackId.Value, cancellationToken);
            var candidate = new TaxRuleCandidate(raw.Id, raw.Treatment, raw.Priority, raw.EffectiveFrom, raw.EffectiveTo, raw.RegistrationId, raw.JurisdictionCode, raw.Direction, raw.PartyCategoryId, raw.ProductCategoryId, raw.DocumentKind, raw.TransactionType, raw.PlaceOfSupplyCountryCode, raw.SourceKind == "CountryPack" ? TaxRuleMutability.CountryPack : TaxRuleMutability.TenantConfiguration, raw.RuleVersion, raw.LegalReasonCode, raw.LegalReasonText, components);
            candidates.Add(new(candidate, raw.PackCode, raw.PackVersion));
        }
        return TaxRuleResolver.Select(candidates) is { } selected ? (selected.Rule, selected.PackCode, selected.PackVersion) : null;
    }

    private static async Task<IReadOnlyList<TaxComponent>> LoadComponentsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid stackId, CancellationToken cancellationToken)
    {
        const string sql = "select c.id,c.tax_type_code,c.tax_code,c.treatment,c.calculation_method,c.rate,c.fixed_amount,c.currency_code,c.sequence,c.base_component_sequence,c.legal_reason_code,c.reporting_code,c.recoverable_percentage from foundation_tax.tax_component_definition c where c.tenant_id=$1 and c.stack_id=$2 order by c.sequence";
        var components = new List<TaxComponent>();
        await using var command = new NpgsqlCommand(sql, connection, transaction); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(stackId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) components.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), Enum.Parse<TaxTreatment>(reader.GetString(3)), Enum.Parse<TaxCalculationMethod>(reader.GetString(4)), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6), reader.GetString(7), reader.GetInt32(8), reader.IsDBNull(9) ? null : reader.GetInt32(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetDecimal(12)));
        return components;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); command.Parameters.AddWithValue(tenantId.ToString("D")); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static TaxRegistrationEvidence ReadRegistrationEvidence(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(1), reader.GetInt64(5), reader.GetFieldValue<DateOnly>(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateOnly>(7), reader.IsDBNull(8) ? null : reader.GetString(8));

    private sealed record RawRule(Guid Id, TaxTreatment Treatment, int Priority, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid? RegistrationId, string? JurisdictionCode, TaxDirection? Direction, Guid? PartyCategoryId, Guid? ProductCategoryId, string? DocumentKind, string? TransactionType, string? PlaceOfSupplyCountryCode, string SourceKind, string RuleVersion, string? LegalReasonCode, string? LegalReasonText, string PackCode, string PackVersion, Guid? StackId);
    private sealed class MutableProfileResolution
    {
        public Guid? RegistrationId { get; set; }
        public string RegistrationStatus { get; set; } = "NotRegistered";
        public TaxRegistrationEvidence? RegistrationEvidence { get; set; }
        public Guid? ProductCategoryId { get; set; }
        public Guid? PartyCategoryId { get; set; }
        public string? PartyTreatment { get; set; }
        public TaxProfileEvidence? PartyEvidence { get; set; }
        public TaxProfileEvidence? ProductEvidence { get; set; }
    }
}
