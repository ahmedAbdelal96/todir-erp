using System.Globalization;
using System.Text;
using System.Text.Json;
using Core.Transactions;
using Core.Services;
using Modules.Tax.DTOs;
using Modules.Tax.Entities;
using Npgsql;
using Shared.Contracts.Catalog;
using Shared.Contracts.Directory;
using Shared.Primitives.Identifiers;

namespace Modules.Tax.Repositories;

/// <summary>Persists preview reconciliation and applies only accepted rows in one transaction.</summary>
public sealed class TaxProfileImportRepository(
    NpgsqlDataSource dataSource,
    ITransactionCoordinator transactions,
    ICatalogCapabilities catalog,
    IDirectoryImportCapabilities directoryImports,
    ITaxCountryPackRegistry countryPacks,
    ILegalEntityFinancialReadStore legalEntities)
{
    private static readonly JsonSerializerOptions RowJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private sealed record StoredRow(int RowNumber, string Reference, string? ProductScope, string Outcome, string? Message, Guid PlannedId, Guid? PartyId,
        Guid? ProductTemplateId, Guid? ProductVariantId, Guid CategoryId, string? CountryCode, string? SchemeCode,
        DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? RegistrationCountryCode, string? RegistrationNumber,
        string? Treatment, bool ReverseChargeEligible, string? EvidenceReference, Guid? PriorId, Guid? SeriesId, long Version);

    public async Task<TaxProfileImportPreviewDto> PreviewAsync(Guid tenant, Guid actor, Guid correlation, TaxProfileImportPreviewRequest request, CancellationToken ct)
    {
        var party = string.Equals(request.Kind, "Party", StringComparison.OrdinalIgnoreCase);
        if ((!party && !string.Equals(request.Kind, "Product", StringComparison.OrdinalIgnoreCase)) || request.LegalEntityId == Guid.Empty || request.Rows.Count is < 1 or > 500)
            throw new TaxException("tax.import_request_invalid", "Choose Product or Party, a Legal Entity, and between one and five hundred rows.");
        if (!party && request.Rows.Any(row => row.ProductScope is not ("Template" or "Variant")))
            throw new TaxException("tax.product_scope_required", "Each Product import row must explicitly identify Template or Variant scope.");
        if (request.Rows.Any(row => row.RowNumber < 1 || row.EffectiveFrom == default || row.EffectiveTo is { } end && end <= row.EffectiveFrom))
            throw new TaxException("tax.import_row_invalid", "Each row needs a positive row number and a valid effective period.");
        if (request.Rows.Select(row => row.RowNumber).Distinct().Count() != request.Rows.Count)
            throw new TaxException("tax.import_row_invalid", "Each import row must have a unique row number.");

        var references = request.Rows.Select(row => row.Reference.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        var variantReferences = !party ? request.Rows.Where(row => row.ProductScope == "Variant").Select(row => row.Reference.Trim()).Distinct(StringComparer.Ordinal).ToArray() : [];
        var templateReferences = !party ? request.Rows.Where(row => row.ProductScope == "Template").Select(row => row.Reference.Trim()).Distinct(StringComparer.Ordinal).ToArray() : [];
        var products = variantReferences.Length > 0
            ? await catalog.ResolveVariantsBySkuAsync(new(tenant), variantReferences, request.LegalEntityId, ct)
            : new Dictionary<string, CatalogVariantIdentity>(StringComparer.Ordinal);
        var externalProducts = variantReferences.Length > 0
            ? await catalog.ResolveVariantsByExternalReferenceAsync(new(tenant), variantReferences, request.LegalEntityId, ct)
            : new Dictionary<string, IReadOnlyList<CatalogVariantIdentity>>(StringComparer.Ordinal);
        var templates = templateReferences.Length > 0
            ? await catalog.ResolveProductTemplatesByCodeAsync(new(tenant), templateReferences, request.LegalEntityId, ct)
            : new Dictionary<string, IReadOnlyList<Guid>>(StringComparer.Ordinal);
        var parties = party && references.Length > 0
            ? await directoryImports.ResolveExternalPartyReferencesAsync(new(tenant), references, request.LegalEntityId, ct)
            : new Dictionary<string, IReadOnlyList<Guid>>(StringComparer.Ordinal);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var readTx = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, readTx, tenant, ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var resolvedRows = new List<StoredRow>(request.Rows.Count);
        foreach (var row in request.Rows.OrderBy(row => row.RowNumber))
        {
            string outcome = "Create";
            string? message = null;
            Guid? partyId = null;
            Guid? templateId = null;
            Guid? variantId = null;
            Guid? categoryId = null;
            Guid? priorId = null;
            Guid? seriesId = null;
            long version = 1;
            var reference = row.Reference.Trim();
            var duplicateKey = string.Join("|", party ? "P" : $"C:{row.ProductScope}", reference, row.CountryCode, row.SchemeCode, row.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (!seen.Add(duplicateKey)) { outcome = "DuplicateInput"; message = "The same business reference and effective change appears more than once."; }
            else if (row.LegalEntityId is { } rowEntity && rowEntity != request.LegalEntityId) { outcome = "Conflict"; message = "The row's Legal Entity does not match the import's Legal Entity."; }
            else if (row.CountryCode is not { Length: 2 } country || country.Any(c => !char.IsAsciiLetterUpper(c))) { outcome = "InvalidScheme"; message = "A two-letter uppercase country code is required for this Tax Scheme."; }
            else if (party && row.RegistrationNumber is not null && (row.RegistrationCountryCode is not { Length: 2 } registrationCountry || registrationCountry.Any(c => !char.IsAsciiLetterUpper(c)))) { outcome = "InvalidTaxNumberFormat"; message = "Registration Country must be a two-letter uppercase country code."; }
            else if (party && row.Treatment is { } treatment && !Enum.TryParse<TaxTreatment>(treatment, true, out _)) { outcome = "InvalidTreatment"; message = "The Tax Treatment is not supported."; }
            else if (string.IsNullOrWhiteSpace(reference)) { outcome = party ? "UnknownParty" : "UnknownProduct"; message = "A business reference is required."; }
            else if (party)
            {
                if (!parties.TryGetValue(reference, out var matches) || matches.Count == 0) { outcome = "UnknownParty"; message = "No active Party matches this exact external reference."; }
                else if (matches.Count != 1) { outcome = "AmbiguousParty"; message = "More than one active Party uses this external reference."; }
                else partyId = matches[0];
            }
            else if (row.ProductScope == "Variant")
            {
                var matches = new List<CatalogVariantIdentity>();
                if (products.TryGetValue(reference, out var bySku)) matches.Add(bySku);
                if (externalProducts.TryGetValue(reference, out var byExternal)) matches.AddRange(byExternal);
                var unique = matches.DistinctBy(match => match.ProductVariantId).ToArray();
                if (unique.Length == 0) { outcome = "UnknownProduct"; message = "No active product variant matches this exact SKU or external reference."; }
                else if (unique.Length > 1) { outcome = "AmbiguousProduct"; message = "The exact product reference identifies more than one active variant."; }
                else { templateId = unique[0].ProductTemplateId; variantId = unique[0].ProductVariantId; }
            }
            else if (row.ProductScope == "Template")
            {
                if (!templates.TryGetValue(reference, out var matches) || matches.Count == 0) { outcome = "UnknownProduct"; message = "No active product template matches this exact Product Template Code."; }
                else if (matches.Count != 1) { outcome = "AmbiguousProduct"; message = "The exact Product Template Code identifies more than one active product template."; }
                else templateId = matches[0];
            }

            if (outcome is "Create")
            {
                categoryId = await ResolveCategoryAsync(connection, readTx, tenant, row.TaxCategoryCode, party ? "Party" : "Product", ct);
                if (categoryId is null) { outcome = "InvalidTaxCategory"; message = "The active Tax Category code was not found in this profile scope."; }
                else if (!await SchemeExistsAsync(connection, readTx, tenant, row.CountryCode, row.SchemeCode, ct)) { outcome = "InvalidScheme"; message = "The Tax Scheme is not active for the selected country."; }
                else
                {
                    if (party && row.RegistrationNumber is not null && row.RegistrationCountryCode is { } regCountry && countryPacks.Find(regCountry, NodaTime.LocalDate.FromDateTime(row.EffectiveFrom.ToDateTime(TimeOnly.MinValue))) is { } pack)
                    {
                        try
                        {
                            var validation = pack.ValidateRegistration(row.SchemeCode ?? string.Empty, row.RegistrationNumber, NodaTime.LocalDate.FromDateTime(row.EffectiveFrom.ToDateTime(TimeOnly.MinValue)));
                            if (!validation.IsValid) { outcome = "InvalidTaxNumberFormat"; message = validation.FailureCode ?? "Registration number format is invalid for the selected Country Pack."; }
                        }
                        catch (TaxException exception) { outcome = "InvalidTaxNumberFormat"; message = exception.Code; }
                    }
                    if (outcome == "Create")
                    {
                        var identityId = party ? partyId!.Value : variantId ?? templateId!.Value;
                        var prior = await FindPriorAsync(connection, readTx, tenant, party, identityId, templateId, variantId, request.LegalEntityId, row.CountryCode, row.SchemeCode, row.EffectiveFrom, row.EffectiveTo, ct);
                        if (prior.Overlap) { outcome = "EffectiveDateOverlap"; message = "The requested period overlaps another profile version."; }
                        else if (prior.Id is { } previous) { outcome = "CreateNewVersion"; priorId = previous; seriesId = prior.Series; version = prior.Version + 1; }
                    }
                }
            }
            resolvedRows.Add(new(row.RowNumber, reference, party ? null : row.ProductScope, outcome, message, Guid.CreateVersion7(), partyId, templateId, variantId, categoryId ?? Guid.Empty,
                row.CountryCode, row.SchemeCode, row.EffectiveFrom, row.EffectiveTo, row.RegistrationCountryCode, row.RegistrationNumber,
                row.Treatment, row.ReverseChargeEligible, row.EvidenceReference, priorId, seriesId, version));
        }
        var acceptedRows = resolvedRows.Where(row => row.Outcome is "Create" or "CreateNewVersion").OrderBy(row => row.RowNumber).ToArray();
        for (var index = 0; index < acceptedRows.Length; index++)
            for (var earlier = 0; earlier < index; earlier++)
            {
                var current = acceptedRows[index]; var previous = acceptedRows[earlier];
                var sameScope = party
                    ? current.PartyId == previous.PartyId
                    : current.ProductTemplateId == previous.ProductTemplateId && current.ProductVariantId == previous.ProductVariantId;
                sameScope &= current.CountryCode == previous.CountryCode && current.SchemeCode == previous.SchemeCode;
                var overlaps = (previous.EffectiveTo is null || current.EffectiveFrom < previous.EffectiveTo.Value)
                    && (current.EffectiveTo is null || previous.EffectiveFrom < current.EffectiveTo.Value);
                if (sameScope && overlaps)
                {
                    var position = resolvedRows.FindIndex(row => row.RowNumber == current.RowNumber);
                    resolvedRows[position] = resolvedRows[position] with { Outcome = "Conflict", Message = "Another row in this batch overlaps the same profile scope and effective period." };
                    break;
                }
            }
        foreach (var group in resolvedRows.Where(row => row.Outcome is "Create" or "CreateNewVersion").GroupBy(row => party
                     ? $"P|{row.PartyId}|{request.LegalEntityId}|{row.CountryCode}|{row.SchemeCode}"
                     : $"C|{row.ProductTemplateId}|{row.ProductVariantId}|{request.LegalEntityId}|{row.CountryCode}|{row.SchemeCode}"))
        {
            var ordered = group.OrderBy(row => row.EffectiveFrom).ThenBy(row => row.RowNumber).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var position = resolvedRows.FindIndex(row => row.RowNumber == ordered[index].RowNumber);
                var row = resolvedRows[position];
                if (index == 0)
                {
                    if (row.PriorId is null) resolvedRows[position] = row with { SeriesId = row.PlannedId, Version = 1 };
                    continue;
                }
                var prior = resolvedRows[resolvedRows.FindIndex(candidate => candidate.RowNumber == ordered[index - 1].RowNumber)];
                resolvedRows[position] = row with { Outcome = "CreateNewVersion", Message = null, PriorId = prior.PlannedId, SeriesId = prior.SeriesId ?? prior.PlannedId, Version = prior.Version + 1 };
            }
        }
        await readTx.CommitAsync(ct);

        var batchId = Guid.CreateVersion7();
        var accepted = resolvedRows.Count(row => row.Outcome is "Create" or "CreateNewVersion");
        var rejected = resolvedRows.Count - accepted;
        await transactions.ExecuteAsync<bool>(new(tenant), async (context, token) =>
        {
            await using (var batch = new NpgsqlCommand("insert into foundation_tax.tax_import_batch(tenant_id,id,legal_entity_id,import_type,status,created_by,created_at,source_reference,row_count,accepted_count,rejected_count,skipped_count) values($1,$2,$3,$4,'Previewed',$5,now(),$6,$7,$8,$9,0)", context.Connection, context.Transaction))
            {
                batch.Parameters.AddWithValue(tenant); batch.Parameters.AddWithValue(batchId); batch.Parameters.AddWithValue(request.LegalEntityId); batch.Parameters.AddWithValue(party ? "PartyTaxProfile" : "ProductTaxProfile"); batch.Parameters.AddWithValue(actor); batch.Parameters.AddWithValue((object?)request.SourceReference ?? DBNull.Value); batch.Parameters.AddWithValue(resolvedRows.Count); batch.Parameters.AddWithValue(accepted); batch.Parameters.AddWithValue(rejected); await batch.ExecuteNonQueryAsync(token);
            }
            foreach (var row in resolvedRows)
            {
                await using var insert = new NpgsqlCommand("insert into foundation_tax.tax_import_row(tenant_id,id,batch_id,row_number,classification,row_data,message) values($1,$2,$3,$4,$5,$6::jsonb,$7)", context.Connection, context.Transaction);
                var stored = row.Outcome is "Create" or "CreateNewVersion" ? row : row with { RegistrationNumber = null };
                insert.Parameters.AddWithValue(tenant); insert.Parameters.AddWithValue(Guid.CreateVersion7()); insert.Parameters.AddWithValue(batchId); insert.Parameters.AddWithValue(row.RowNumber); insert.Parameters.AddWithValue(row.Outcome); insert.Parameters.AddWithValue(JsonSerializer.Serialize(stored, RowJsonOptions)); insert.Parameters.AddWithValue((object?)row.Message ?? DBNull.Value); await insert.ExecuteNonQueryAsync(token);
            }
            await AuditAsync(context.Connection, context.Transaction, tenant, actor, correlation, "TaxProfileImportPreviewed", batchId, request.LegalEntityId, new { request.Kind, request.SourceReference, rows = resolvedRows.Count, accepted, rejected }, token);
            return true;
        }, ct);
        return new(batchId, "Previewed", resolvedRows.Select(row => new TaxImportRowResult(row.RowNumber, row.Reference, row.Outcome, row.Message, null, row.Outcome == "CreateNewVersion" ? row.Version : null, row.Reference, row.ProductScope)).ToArray(), accepted, rejected, 0);
    }

    public async Task<TaxProfileImportCommitDto> CommitAsync(Guid tenant, Guid actor, Guid correlation, Guid batchId, CancellationToken ct)
    {
        return await transactions.ExecuteAsync<TaxProfileImportCommitDto>(new(tenant), async (context, token) =>
        {
            await using var batch = new NpgsqlCommand("select legal_entity_id,import_type,status,created_by,row_count,accepted_count,rejected_count,skipped_count,source_reference from foundation_tax.tax_import_batch where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction);
            batch.Parameters.AddWithValue(tenant); batch.Parameters.AddWithValue(batchId);
            Guid entity; string kind; string status; int inputRows; int acceptedPreview; int rejectedPreview; string? source;
            await using (var reader = await batch.ExecuteReaderAsync(token))
            {
                if (!await reader.ReadAsync(token)) throw new TaxException("tax.import_batch_not_found", "The import preview was not found.");
                entity = reader.GetGuid(0); kind = reader.GetString(1); status = reader.GetString(2); inputRows = reader.GetInt32(4); acceptedPreview = reader.GetInt32(5); rejectedPreview = reader.GetInt32(6); source = reader.IsDBNull(8) ? null : reader.GetString(8);
            }
            if (status != "Previewed") throw new TaxException("tax.import_batch_not_ready", "Only a previewed batch can be committed.");
            if (await ScalarAsync<bool>(context, "select created_by=$3 from foundation_tax.tax_import_batch where tenant_id=$1 and id=$2", token, tenant, batchId, actor) == false) throw new TaxException("tax.import_actor_mismatch", "Only the user who previewed this batch can commit it.");
            var party = kind == "PartyTaxProfile";
            var rows = new List<StoredRow>();
            await using (var rowCommand = new NpgsqlCommand("select row_data::text from foundation_tax.tax_import_row where tenant_id=$1 and batch_id=$2 order by row_number", context.Connection, context.Transaction))
            {
                rowCommand.Parameters.AddWithValue(tenant); rowCommand.Parameters.AddWithValue(batchId); await using var reader = await rowCommand.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) rows.Add(JsonSerializer.Deserialize<StoredRow>(reader.GetString(0), RowJsonOptions) ?? throw new TaxException("tax.import_row_invalid", "A stored preview row could not be read."));
            }
            var scopes = rows.Where(row => row.Outcome is "Create" or "CreateNewVersion")
                .Select(row => party ? $"P|{tenant:D}|{row.PartyId:D}|{entity:D}|{row.CountryCode}|{row.SchemeCode}" : $"C|{tenant:D}|{row.ProductTemplateId:D}|{row.ProductVariantId:D}|{entity:D}|{row.CountryCode}|{row.SchemeCode}")
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            foreach (var scope in scopes)
            {
                await using var lockScope = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1,0))", context.Connection, context.Transaction);
                lockScope.Parameters.AddWithValue(scope); await lockScope.ExecuteNonQueryAsync(token);
            }
            var result = new List<TaxImportRowResult>(rows.Count);
            var created = 0; var versioned = 0; var accepted = 0;
            foreach (var row in rows)
            {
                if (row.Outcome is not ("Create" or "CreateNewVersion"))
                {
                    result.Add(new(row.RowNumber, row.Reference, row.Outcome, row.Message));
                    continue;
                }
                if (row.CategoryId == Guid.Empty || !await CategoryStillWritableAsync(context, tenant, row.CategoryId, party, token)) throw new TaxException("tax.import_conflict", "An accepted Tax Category changed after preview; the entire batch was rolled back.");
                if (!await SchemeStillActiveAsync(context, tenant, row.CountryCode, row.SchemeCode, token)) throw new TaxException("tax.import_conflict", "A Tax Scheme changed after preview; the entire batch was rolled back.");
                var id = row.PlannedId;
                var series = row.SeriesId ?? id;
                if (row.PriorId is { } priorId)
                {
                    var prior = await ReadPriorForUpdateAsync(context, tenant, party, priorId, token);
                    if (prior is null || row.EffectiveFrom <= prior.Value.From) throw new TaxException("tax.import_conflict", "A profile changed after preview; the entire batch was rolled back.");
                    if (prior.Value.To is null || prior.Value.To > row.EffectiveFrom)
                    {
                        await using var close = new NpgsqlCommand($"update foundation_tax.{(party ? "party_tax_profile" : "product_tax_profile")} set effective_to=$3 where tenant_id=$1 and id=$2 and (effective_to is null or effective_to>$3)", context.Connection, context.Transaction);
                        close.Parameters.AddWithValue(tenant); close.Parameters.AddWithValue(priorId); close.Parameters.AddWithValue(row.EffectiveFrom);
                        if (await close.ExecuteNonQueryAsync(token) != 1) throw new TaxException("tax.import_conflict", "A profile changed after preview; the entire batch was rolled back.");
                    }
                }
                if (await HasOverlapAsync(context, tenant, party, row, entity, token)) throw new TaxException("tax.import_conflict", "A profile period now overlaps the preview; the entire batch was rolled back.");
                if (party) await InsertPartyAsync(context, tenant, id, series, row, entity, token);
                else await InsertProductAsync(context, tenant, id, series, row, entity, token);
                await using (var updateRow = new NpgsqlCommand("update foundation_tax.tax_import_row set row_data=jsonb_set(row_data - 'registrationNumber','{targetId}',to_jsonb($4::text),true) where tenant_id=$1 and batch_id=$2 and row_number=$3", context.Connection, context.Transaction))
                { updateRow.Parameters.AddWithValue(tenant); updateRow.Parameters.AddWithValue(batchId); updateRow.Parameters.AddWithValue(row.RowNumber); updateRow.Parameters.AddWithValue(id.ToString("D")); await updateRow.ExecuteNonQueryAsync(token); }
                result.Add(new(row.RowNumber, row.Reference, row.Outcome, null, id, row.Version, row.Reference, row.ProductScope)); accepted++;
                if (row.Outcome == "CreateNewVersion") versioned++; else created++;
            }
            var skipped = inputRows - accepted - rejectedPreview;
            await using (var updateBatch = new NpgsqlCommand("update foundation_tax.tax_import_batch set status='Committed',accepted_count=$3,rejected_count=$4,skipped_count=$5,committed_at=now() where tenant_id=$1 and id=$2", context.Connection, context.Transaction))
            { updateBatch.Parameters.AddWithValue(tenant); updateBatch.Parameters.AddWithValue(batchId); updateBatch.Parameters.AddWithValue(accepted); updateBatch.Parameters.AddWithValue(rejectedPreview); updateBatch.Parameters.AddWithValue(skipped); await updateBatch.ExecuteNonQueryAsync(token); }
            await AuditAsync(context.Connection, context.Transaction, tenant, actor, correlation, "TaxProfileImportCommitted", batchId, entity, new { kind, source, inputRows, accepted, rejected = rejectedPreview, skipped, created, versioned }, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "TaxProfilesImportedV1", JsonSerializer.Serialize(new { batchId, kind, entity, accepted, rejected = rejectedPreview, skipped, created, versioned }), new(tenant), DateTimeOffset.UtcNow, correlation, batchId, "tax"));
            return new(batchId, "Committed", result, inputRows, accepted, rejectedPreview, skipped, created, versioned, "Accepted preview rows commit atomically; any persistence failure rolls back every accepted row.");
        }, ct);
    }

    public async Task<(string Kind, Guid LegalEntityId)> GetBatchTypeAsync(Guid tenant, Guid batchId, Guid actor, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        await using var cmd = new NpgsqlCommand("select import_type,legal_entity_id from foundation_tax.tax_import_batch where tenant_id=$1 and id=$2 and created_by=$3 and status='Previewed'", connection, tx);
        cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(batchId); cmd.Parameters.AddWithValue(actor); await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new TaxException("tax.import_batch_not_found", "The user's active import preview was not found.");
        var result = (reader.GetString(0), reader.GetGuid(1)); await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<TaxProfileExportDto> ExportAsync(Guid tenant, Guid entity, string kind, DateOnly? at, CancellationToken ct, bool includeRegistrationNumber = true)
    {
        var party = string.Equals(kind, "Party", StringComparison.OrdinalIgnoreCase);
        if (!party && !string.Equals(kind, "Product", StringComparison.OrdinalIgnoreCase)) throw new TaxException("tax.export_kind_invalid", "Choose Product or Party Tax Profiles.");
        var output = new StringBuilder();
        if (!party)
        {
            output.AppendLine("ProductScope,ProductReference,SKU,TaxCategoryCode,CountryCode,Scheme,LegalEntityCode,EffectiveFrom,EffectiveTo,Version");
            await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
            const string sql = "select p.product_template_id,p.product_variant_id,c.code,p.country_code,p.scheme_code,p.effective_from,p.effective_to,p.version from foundation_tax.product_tax_profile p join foundation_tax.tax_category c on c.tenant_id=p.tenant_id and c.id=p.category_id where p.tenant_id=$1 and p.legal_entity_id=$2 and ($3::date is null or (p.effective_from<=$3 and (p.effective_to is null or p.effective_to>$3))) order by p.product_template_id,p.product_variant_id,p.effective_from";
            await using var cmd = new NpgsqlCommand(sql, connection, tx); cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(entity); cmd.Parameters.AddWithValue((object?)at ?? DBNull.Value); await using var reader = await cmd.ExecuteReaderAsync(ct);
            var rows = new List<(Guid Template, Guid? Variant, string Category, string? Country, string? Scheme, DateOnly From, DateOnly? To, long Version)>();
            while (await reader.ReadAsync(ct)) rows.Add((reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetString(2), Text(reader, 3), Text(reader, 4), reader.GetFieldValue<DateOnly>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6), reader.GetInt64(7)));
            await reader.CloseAsync(); await tx.CommitAsync(ct);
            var templates = rows.Count == 0 ? new Dictionary<Guid, CatalogProductTemplateReferences>() : await catalog.GetProductTemplateReferencesBatchAsync(new(tenant), rows.Select(row => row.Template).Distinct().ToArray(), entity, ct);
            var variantIds = rows.Where(row => row.Variant is not null).Select(row => row.Variant!.Value).Distinct().ToArray();
            var variants = variantIds.Length == 0 ? new Dictionary<Guid, CatalogVariantReferences>() : await catalog.GetVariantReferencesBatchAsync(new(tenant), variantIds, entity, ct);
            var entityCode = await legalEntities.GetCodeAsync(new(tenant), new(entity), ct) ?? throw new TaxException("tax.legal_entity_not_found", "The selected Legal Entity has no active business code.");
            foreach (var row in rows)
            {
                var scope = row.Variant is null ? "Template" : "Variant";
                var template = templates.GetValueOrDefault(row.Template) ?? throw new TaxException("tax.product_reference_missing", "A Product Template business code could not be resolved for export.");
                var refs = row.Variant is { } variantId ? variants.GetValueOrDefault(variantId) : null;
                var reference = row.Variant is null ? template.ProductTemplateCode : refs?.Sku ?? (refs is { ExternalReferences.Count: > 0 } ? refs.ExternalReferences[0] : null);
                if (string.IsNullOrWhiteSpace(reference)) throw new TaxException("tax.product_reference_missing", "A stable Product Variant reference could not be resolved for export.");
                output.AppendJoin(',', Csv(scope), Csv(reference), Csv(refs?.Sku), Csv(row.Category), Csv(row.Country), Csv(row.Scheme), Csv(entityCode), Csv(row.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), Csv(row.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), Csv(row.Version.ToString(CultureInfo.InvariantCulture))).AppendLine();
            }
        }
        else
        {
            output.AppendLine("PartyReference,RegistrationCountry,RegistrationNumber,TaxTreatment,TaxCategoryCode,Scheme,LegalEntity,EffectiveFrom,EffectiveTo,Version");
            await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
            const string sql = "select p.party_id,p.registration_country_code,p.registration_number,p.treatment,c.code,p.scheme_code,p.effective_from,p.effective_to,p.version from foundation_tax.party_tax_profile p left join foundation_tax.tax_category c on c.tenant_id=p.tenant_id and c.id=p.category_id where p.tenant_id=$1 and p.legal_entity_id=$2 and ($3::date is null or (p.effective_from<=$3 and (p.effective_to is null or p.effective_to>$3))) order by p.party_id,p.effective_from";
            await using var cmd = new NpgsqlCommand(sql, connection, tx); cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(entity); cmd.Parameters.AddWithValue((object?)at ?? DBNull.Value); await using var reader = await cmd.ExecuteReaderAsync(ct);
            var rows = new List<(Guid Party, string? RegCountry, string? Registration, string? Treatment, string? Category, string? Scheme, DateOnly From, DateOnly? To, long Version)>();
            while (await reader.ReadAsync(ct)) rows.Add((reader.GetGuid(0), Text(reader, 1), Text(reader, 2), Text(reader, 3), Text(reader, 4), Text(reader, 5), reader.GetFieldValue<DateOnly>(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateOnly>(7), reader.GetInt64(8)));
            await reader.CloseAsync(); await tx.CommitAsync(ct);
            var references = rows.Count == 0 ? new Dictionary<Guid, string>() : await directoryImports.FindStablePartyReferencesAsync(new(tenant), rows.Select(row => row.Party).Distinct().ToArray(), entity, ct);
            foreach (var row in rows)
            {
                if (!references.TryGetValue(row.Party, out var reference)) continue;
                output.AppendJoin(',', Csv(reference), Csv(row.RegCountry), Csv(includeRegistrationNumber ? row.Registration : null), Csv(row.Treatment), Csv(row.Category), Csv(row.Scheme), Csv(entity.ToString("D")), Csv(row.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), Csv(row.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), Csv(row.Version.ToString(CultureInfo.InvariantCulture))).AppendLine();
            }
        }
        return new($"tax-{(party ? "party" : "product")}-profiles.csv", "text/csv; charset=utf-8", output.ToString());
    }

    private static async Task<Guid?> ResolveCategoryAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid tenant, string code, string scope, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("select id from foundation_tax.tax_category where tenant_id=$1 and code=$2 and scope=$3 and status='Active'", c, tx); cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(code.Trim()); cmd.Parameters.AddWithValue(scope); var value = await cmd.ExecuteScalarAsync(ct); return value is Guid id ? id : null;
    }
    private static async Task<bool> SchemeExistsAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid tenant, string? country, string? scheme, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(country) || country.Length != 2 || string.IsNullOrWhiteSpace(scheme)) return false;
        await using var cmd = new NpgsqlCommand("select exists(select 1 from foundation_tax.tax_scheme where tenant_id=$1 and code=$2 and country_code=$3 and status='Active')", c, tx); cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(scheme); cmd.Parameters.AddWithValue(country); return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }
    private static async Task<(Guid? Id, Guid? Series, long Version, bool Overlap)> FindPriorAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid tenant, bool party, Guid identity, Guid? template, Guid? variant, Guid entity, string? country, string? scheme, DateOnly from, DateOnly? to, CancellationToken ct)
    {
        var table = party ? "party_tax_profile" : "product_tax_profile";
        var identityFilter = party ? "party_id=$2" : "product_template_id=$2 and product_variant_id is not distinct from $3";
        var entityParam = party ? 3 : 4; var countryParam = party ? 4 : 5; var schemeParam = party ? 5 : 6; var fromParam = party ? 6 : 7; var toParam = party ? 7 : 8;
        var sql = $"select id,profile_series_id,version,effective_from,effective_to from foundation_tax.{table} where tenant_id=$1 and {identityFilter} and legal_entity_id is not distinct from ${entityParam} and " + (party ? $"country_code=${countryParam} and scheme_code is not distinct from ${schemeParam}" : $"country_code is not distinct from ${countryParam} and scheme_code is not distinct from ${schemeParam}") + $" and daterange(effective_from,effective_to,'[)') && daterange(${fromParam},${toParam},'[)') order by effective_from desc limit 2";
        await using var cmd = new NpgsqlCommand(sql, c, tx); cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(party ? identity : template!.Value); if (!party) cmd.Parameters.AddWithValue(variant ?? (object)DBNull.Value); cmd.Parameters.AddWithValue(entity); cmd.Parameters.AddWithValue(country ?? (object)DBNull.Value); cmd.Parameters.AddWithValue(scheme ?? (object)DBNull.Value); cmd.Parameters.AddWithValue(from); cmd.Parameters.AddWithValue(to ?? (object)DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct))
        {
            await reader.CloseAsync();
            var priorSql = $"select id,profile_series_id,version from foundation_tax.{table} where tenant_id=$1 and {identityFilter} and legal_entity_id is not distinct from ${entityParam} and " + (party ? $"country_code=${countryParam} and scheme_code is not distinct from ${schemeParam}" : $"country_code is not distinct from ${countryParam} and scheme_code is not distinct from ${schemeParam}") + $" and effective_from<${fromParam} order by effective_from desc limit 1";
            await using var previous = new NpgsqlCommand(priorSql, c, tx); previous.Parameters.AddWithValue(tenant); previous.Parameters.AddWithValue(party ? identity : template!.Value); if (!party) previous.Parameters.AddWithValue(variant ?? (object)DBNull.Value); previous.Parameters.AddWithValue(entity); previous.Parameters.AddWithValue(country ?? (object)DBNull.Value); previous.Parameters.AddWithValue(scheme ?? (object)DBNull.Value); previous.Parameters.AddWithValue(from); await using var previousReader = await previous.ExecuteReaderAsync(ct); return await previousReader.ReadAsync(ct) ? (previousReader.GetGuid(0), previousReader.GetGuid(1), previousReader.GetInt64(2), false) : (null, null, 1, false);
        }
        var id = reader.GetGuid(0); var series = reader.GetGuid(1); var version = reader.GetInt64(2); var oldFrom = reader.GetFieldValue<DateOnly>(3); var oldTo = reader.IsDBNull(4) ? (DateOnly?)null : reader.GetFieldValue<DateOnly>(4);
        if (reader.Read()) return (null, null, 1, true);
        return from > oldFrom ? (id, series, version, false) : (null, null, 1, true);
    }
    private static async Task<(DateOnly From, DateOnly? To)?> ReadPriorForUpdateAsync(Core.Transactions.ITransactionContext c, Guid tenant, bool party, Guid id, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand($"select effective_from,effective_to from foundation_tax.{(party ? "party_tax_profile" : "product_tax_profile")} where tenant_id=$1 and id=$2 for update", c.Connection, c.Transaction); cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(id); await using var reader = await cmd.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetFieldValue<DateOnly>(0), reader.IsDBNull(1) ? null : reader.GetFieldValue<DateOnly>(1)) : null;
    }
    private static async Task<bool> HasOverlapAsync(Core.Transactions.ITransactionContext c, Guid tenant, bool party, StoredRow row, Guid entity, CancellationToken ct)
    {
        var table = party ? "party_tax_profile" : "product_tax_profile"; var identitySql = party ? "party_id=$2" : "product_template_id=$2 and product_variant_id is not distinct from $3";
        var entityIndex = party ? 3 : 4; var countryIndex = party ? 4 : 5; var schemeIndex = party ? 5 : 6; var fromIndex = party ? 6 : 7; var toIndex = party ? 7 : 8; var excludeIndex = party ? 8 : 9;
        var cmd = new NpgsqlCommand($"select exists(select 1 from foundation_tax.{table} where tenant_id=$1 and {identitySql} and id is distinct from ${excludeIndex} and legal_entity_id is not distinct from ${entityIndex} and country_code is not distinct from ${countryIndex} and scheme_code is not distinct from ${schemeIndex} and daterange(effective_from,effective_to,'[)') && daterange(${fromIndex},${toIndex},'[)'))", c.Connection, c.Transaction);
        cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(party ? row.PartyId!.Value : row.ProductTemplateId!.Value); if (!party) cmd.Parameters.AddWithValue(row.ProductVariantId ?? (object)DBNull.Value); cmd.Parameters.AddWithValue(entity);
        cmd.Parameters.AddWithValue(row.CountryCode ?? (object)DBNull.Value); cmd.Parameters.AddWithValue(row.SchemeCode ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue(row.EffectiveFrom); cmd.Parameters.AddWithValue(row.EffectiveTo ?? (object)DBNull.Value); cmd.Parameters.AddWithValue(row.PriorId ?? (object)DBNull.Value);
        await using (cmd) return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }
    private static async Task<bool> CategoryStillWritableAsync(Core.Transactions.ITransactionContext c, Guid tenant, Guid id, bool party, CancellationToken ct)
    { await using var cmd = new NpgsqlCommand("select exists(select 1 from foundation_tax.tax_category where tenant_id=$1 and id=$2 and scope=$3 and status='Active')", c.Connection, c.Transaction); cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(party ? "Party" : "Product"); return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false); }
    private static async Task<bool> SchemeStillActiveAsync(Core.Transactions.ITransactionContext c, Guid tenant, string? country, string? scheme, CancellationToken ct)
    { await using var cmd = new NpgsqlCommand("select exists(select 1 from foundation_tax.tax_scheme where tenant_id=$1 and code=$2 and country_code=$3 and status='Active')", c.Connection, c.Transaction); cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(scheme ?? ""); cmd.Parameters.AddWithValue(country ?? ""); return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false); }
    private static async Task InsertPartyAsync(Core.Transactions.ITransactionContext c, Guid tenant, Guid id, Guid series, StoredRow r, Guid entity, CancellationToken ct)
    { await using var cmd = new NpgsqlCommand("insert into foundation_tax.party_tax_profile(tenant_id,id,profile_series_id,supersedes_id,party_id,legal_entity_id,country_code,scheme_code,category_id,treatment,registration_number,registration_country_code,reverse_charge_eligible,exemption_evidence_reference,effective_from,effective_to,version,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,now())", c.Connection, c.Transaction); cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(series); cmd.Parameters.AddWithValue((object?)r.PriorId ?? DBNull.Value); cmd.Parameters.AddWithValue(r.PartyId!.Value); cmd.Parameters.AddWithValue(entity); cmd.Parameters.AddWithValue(r.CountryCode!); cmd.Parameters.AddWithValue((object?)r.SchemeCode ?? DBNull.Value); cmd.Parameters.AddWithValue(r.CategoryId); cmd.Parameters.AddWithValue((object?)r.Treatment ?? DBNull.Value); cmd.Parameters.AddWithValue((object?)r.RegistrationNumber ?? DBNull.Value); cmd.Parameters.AddWithValue((object?)r.RegistrationCountryCode ?? DBNull.Value); cmd.Parameters.AddWithValue(r.ReverseChargeEligible); cmd.Parameters.AddWithValue((object?)r.EvidenceReference ?? DBNull.Value); cmd.Parameters.AddWithValue(r.EffectiveFrom); cmd.Parameters.AddWithValue((object?)r.EffectiveTo ?? DBNull.Value); cmd.Parameters.AddWithValue(r.Version); await cmd.ExecuteNonQueryAsync(ct); }
    private static async Task InsertProductAsync(Core.Transactions.ITransactionContext c, Guid tenant, Guid id, Guid series, StoredRow r, Guid entity, CancellationToken ct)
    { await using var cmd = new NpgsqlCommand("insert into foundation_tax.product_tax_profile(tenant_id,id,profile_series_id,supersedes_id,product_template_id,product_variant_id,legal_entity_id,country_code,scheme_code,category_id,effective_from,effective_to,version,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,now())", c.Connection, c.Transaction); cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(series); cmd.Parameters.AddWithValue((object?)r.PriorId ?? DBNull.Value); cmd.Parameters.AddWithValue(r.ProductTemplateId!.Value); cmd.Parameters.AddWithValue((object?)r.ProductVariantId ?? DBNull.Value); cmd.Parameters.AddWithValue(entity); cmd.Parameters.AddWithValue((object?)r.CountryCode ?? DBNull.Value); cmd.Parameters.AddWithValue((object?)r.SchemeCode ?? DBNull.Value); cmd.Parameters.AddWithValue(r.CategoryId); cmd.Parameters.AddWithValue(r.EffectiveFrom); cmd.Parameters.AddWithValue((object?)r.EffectiveTo ?? DBNull.Value); cmd.Parameters.AddWithValue(r.Version); await cmd.ExecuteNonQueryAsync(ct); }
    private static async Task AuditAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid tenant, Guid actor, Guid correlation, string action, Guid id, Guid entity, object after, CancellationToken ct)
    { await using var cmd = new NpgsqlCommand("insert into foundation_tax.tax_audit(tenant_id,id,legal_entity_id,actor_id,operation,entity_type,entity_id,after_state,occurred_at,correlation_id) values($1,$2,$3,$4,$5,'TaxProfileImportBatch',$6,$7::jsonb,now(),$8)", c, tx); cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(Guid.CreateVersion7()); cmd.Parameters.AddWithValue(entity); cmd.Parameters.AddWithValue(actor); cmd.Parameters.AddWithValue(action); cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(JsonSerializer.Serialize(after)); cmd.Parameters.AddWithValue(correlation); await cmd.ExecuteNonQueryAsync(ct); }
    private static async Task<T> ScalarAsync<T>(Core.Transactions.ITransactionContext c, string sql, CancellationToken ct, params object[] values)
    { await using var cmd = new NpgsqlCommand(sql, c.Connection, c.Transaction); foreach (var value in values) cmd.Parameters.AddWithValue(value ?? DBNull.Value); var result = await cmd.ExecuteScalarAsync(ct); return result is null or DBNull ? default! : (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture); }
    private static async Task SetTenantAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid tenant, CancellationToken ct)
    { await using var cmd = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", c, tx); cmd.Parameters.AddWithValue(tenant.ToString("D")); await cmd.ExecuteNonQueryAsync(ct); }
    private static string Csv(string? value) { var text = value ?? string.Empty; return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""; }
    private static string? Text(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}
