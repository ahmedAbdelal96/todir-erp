using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using Modules.Catalog.DTOs;
using Modules.Catalog.Entities;
using Core.Transactions;
using Shared.Primitives.Identifiers;

namespace Modules.Catalog.Repositories;

public sealed class CatalogImportRepository(ITransactionCoordinator coordinator, NpgsqlDataSource dataSource)
{
    private const int MaximumRows = 5000;
    private const int ChunkSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static readonly JsonSerializerOptions SqlJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<CatalogImportSummary> PreviewAsync(TenantId tenantId, Guid actorId, Guid membershipId, CatalogImportPreviewRequest request, CancellationToken cancellationToken)
    {
        var inputRows = request.Rows ?? throw new CatalogException("catalog.import_rows_missing", "Provide rows to preview.");
        if (inputRows.Count is < 1 or > MaximumRows) throw new CatalogException("catalog.import_row_limit", $"An import must contain between 1 and {MaximumRows} rows.");
        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > 255) throw new CatalogException("catalog.import_file_name_invalid", "Import file name is invalid.");
        var rows = inputRows.Select((row, index) => Normalize(index + 1, row)).ToArray();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await SetTenantAsync(connection, tenantId, cancellationToken);
        await LoadReferencesAsync(connection, tenantId, rows, cancellationToken);
        ValidateAdditionalUoms(rows);
        await ClassifyExistingAsync(connection, tenantId, rows, cancellationToken);
        await ClassifyExistingUomIntervalsAsync(connection, tenantId, rows, cancellationToken);
        var batchId = Guid.CreateVersion7();
        var groupedIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var key = string.IsNullOrWhiteSpace(row.Source.TemplateKey) ? $"row:{row.RowNumber}" : $"group:{row.Source.TemplateKey.Trim()}";
            if (!groupedIds.TryGetValue(key, out var templateId)) groupedIds[key] = templateId = Guid.CreateVersion7();
            row.TargetTemplateId = templateId;
            row.TargetVariantId = Guid.CreateVersion7();
        }
        ValidateGroups(rows);
        LinkGroupsToExistingTemplates(rows);
        ClassifyFileDuplicates(rows);
        var counts = Counts(rows);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var insertBatch = new NpgsqlCommand("insert into foundation_catalog.catalog_import_batch (tenant_id,id,file_name,status,total_rows,create_count,update_count,review_count,error_count,created_by,created_membership_id,created_at) values ($1,$2,$3,'Previewed',$4,$5,$6,$7,$8,$9,$10,now())", connection, transaction))
        {
            insertBatch.Parameters.AddWithValue(tenantId.Value); insertBatch.Parameters.AddWithValue(batchId); insertBatch.Parameters.AddWithValue(Path.GetFileName(request.FileName));
            insertBatch.Parameters.AddWithValue(rows.Length); insertBatch.Parameters.AddWithValue(counts.Create); insertBatch.Parameters.AddWithValue(counts.Update); insertBatch.Parameters.AddWithValue(counts.Review); insertBatch.Parameters.AddWithValue(counts.Error); insertBatch.Parameters.AddWithValue(actorId); insertBatch.Parameters.AddWithValue(membershipId);
            await insertBatch.ExecuteNonQueryAsync(cancellationToken);
        }
        var staged = rows.Select(row => new StagedRow(tenantId.Value, batchId, row.RowNumber, JsonSerializer.Serialize(new StoredImportData(row.Source, row.AdditionalUoms.Select(uom => new StoredImportUom(uom.UomId, uom.FactorToBase, uom.EffectiveFrom, uom.EffectiveTo, uom.Barcode, uom.NormalizedBarcode, uom.UsageFlags?.ToArray(), uom.RoundingPrecision)).ToArray(), row.ResolvedAttributes.Select(pair => new StoredImportAttribute(pair.Key, pair.Value)).ToArray()), JsonOptions), row.NormalizedSku, row.NormalizedBarcode, row.MatchTemplateId, row.MatchVariantId, row.CategoryId, row.BrandId, row.BaseUomId, row.TargetTemplateId, row.TargetVariantId, row.Classification, row.Issues.ToArray())).ToArray();
        var json = JsonSerializer.Serialize(staged, SqlJsonOptions);
        const string stageSql = "insert into foundation_catalog.catalog_import_row (tenant_id,batch_id,row_number,row_data,normalized_sku,normalized_barcode,match_template_id,match_variant_id,category_id,brand_id,base_uom_id,target_template_id,target_variant_id,classification,errors) select x.tenant_id,x.batch_id,x.row_number,x.row_data::jsonb,x.normalized_sku,x.normalized_barcode,x.match_template_id,x.match_variant_id,x.category_id,x.brand_id,x.base_uom_id,x.target_template_id,x.target_variant_id,x.classification,x.errors from jsonb_to_recordset($1) as x(tenant_id uuid,batch_id uuid,row_number integer,row_data text,normalized_sku text,normalized_barcode text,match_template_id uuid,match_variant_id uuid,category_id uuid,brand_id uuid,base_uom_id uuid,target_template_id uuid,target_variant_id uuid,classification text,errors text[])";
        await using (var stage = new NpgsqlCommand(stageSql, connection, transaction)) { stage.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json }); await stage.ExecuteNonQueryAsync(cancellationToken); }
        await transaction.CommitAsync(cancellationToken);
        return ToSummary(batchId, "Previewed", rows, counts.Create, counts.Update, counts.Review, counts.Error);
    }

    public async Task<CatalogImportSummary> GetSummaryAsync(TenantId tenantId, Guid batchId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await SetTenantAsync(connection, tenantId, cancellationToken);
        const string sql = "select b.status,b.total_rows,b.create_count,b.update_count,b.review_count,b.error_count,b.created_count,b.updated_count,count(r.row_number) filter (where r.classification='Review'),count(r.row_number) filter (where r.errors @> array['duplicate_sku_in_file']::text[]),count(r.row_number) filter (where r.errors @> array['duplicate_barcode_in_file']::text[]),count(r.row_number) filter (where r.errors && array['unknown_uom','invalid_conversion','uom_effective_period_conflict']::text[]),count(r.row_number) filter (where r.errors @> array['unknown_category']::text[]) from foundation_catalog.catalog_import_batch b left join foundation_catalog.catalog_import_row r on r.tenant_id=b.tenant_id and r.batch_id=b.id where b.tenant_id=$1 and b.id=$2 group by b.tenant_id,b.id";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(batchId);
        string status; int total; int creates; int updates; int reviews; int errors; int created; int updated; int skipped; int duplicateSkus; int duplicateBarcodes; int uomIssues; int categoryIssues;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) throw new KeyNotFoundException("catalog.import_not_found");
            status = reader.GetString(0); total = reader.GetInt32(1); creates = reader.GetInt32(2); updates = reader.GetInt32(3); reviews = reader.GetInt32(4); errors = reader.GetInt32(5); created = reader.GetInt32(6); updated = reader.GetInt32(7);
            skipped = checked((int)reader.GetInt64(8)); duplicateSkus = checked((int)reader.GetInt64(9)); duplicateBarcodes = checked((int)reader.GetInt64(10)); uomIssues = checked((int)reader.GetInt64(11)); categoryIssues = checked((int)reader.GetInt64(12));
        }
        var rows = new List<CatalogImportRowResult>(Math.Min(total, 200));
        await using (var rowCommand = new NpgsqlCommand("select row_number,classification,errors from foundation_catalog.catalog_import_row where tenant_id=$1 and batch_id=$2 order by row_number limit 200", connection))
        {
            rowCommand.Parameters.AddWithValue(tenantId.Value); rowCommand.Parameters.AddWithValue(batchId);
            await using var reader = await rowCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) rows.Add(new CatalogImportRowResult(reader.GetInt32(0), reader.GetString(1), reader.GetFieldValue<string[]>(2)));
        }
        return new CatalogImportSummary(batchId, status, total, creates, updates, reviews, errors, rows, status == "Committed" ? created : null, status == "Committed" ? updated : null, skipped, duplicateSkus, duplicateBarcodes, uomIssues, categoryIssues);
    }

    public async Task<CatalogImportSummary> CommitAsync(TenantId tenantId, Guid batchId, CancellationToken cancellationToken)
    {
        await using var lockConnection = await dataSource.OpenConnectionAsync(cancellationToken);
        await SetTenantAsync(lockConnection, tenantId, cancellationToken);
        var lockKey = $"catalog-import:{tenantId.Value:D}:{batchId:D}";
        await using (var acquire = new NpgsqlCommand("select pg_advisory_lock(hashtextextended($1,0))", lockConnection)) { acquire.Parameters.AddWithValue(lockKey); await acquire.ExecuteNonQueryAsync(cancellationToken); }
        try
        {
            var summary = await GetSummaryAsync(tenantId, batchId, cancellationToken);
            if (summary.Status == "Committed") return summary;
            if (summary.Status is not ("Previewed" or "Queued" or "Executing")) throw new CatalogException("catalog.import_not_previewed", "Only a reviewed import can be committed.");
            await coordinator.ExecuteAsync(tenantId, async (context, token) =>
            {
                await using var claim = new NpgsqlCommand("update foundation_catalog.catalog_import_batch set status='Executing' where tenant_id=$1 and id=$2 and status in ('Previewed','Queued')", context.Connection, context.Transaction);
                claim.Parameters.AddWithValue(tenantId.Value); claim.Parameters.AddWithValue(batchId); await claim.ExecuteNonQueryAsync(token);
                return true;
            }, cancellationToken);
            var staged = await ReadRowsAsync(tenantId, batchId, cancellationToken);
            var eligible = staged.Where(row => row.Classification is "Create" or "Update").ToArray();
            foreach (var chunk in eligible.Chunk(ChunkSize))
            {
                var creates = chunk.Where(row => row.Classification == "Create").ToArray();
                var updates = chunk.Where(row => row.Classification == "Update").ToArray();
                await coordinator.ExecuteAsync(tenantId, async (context, token) =>
                {
                    if (creates.Length > 0) await InsertCreatedAsync(context, tenantId, batchId, creates, token);
                    if (updates.Length > 0) await UpdateMatchedAsync(context, tenantId, batchId, updates, token);
                    var rowNumbers = chunk.Select(row => row.RowNumber).ToArray();
                    await using (var mark = new NpgsqlCommand("update foundation_catalog.catalog_import_row set processed_at=now() where tenant_id=$1 and batch_id=$2 and row_number=any($3) and processed_at is null", context.Connection, context.Transaction))
                    { mark.Parameters.AddWithValue(tenantId.Value); mark.Parameters.AddWithValue(batchId); mark.Parameters.AddWithValue(rowNumbers); await mark.ExecuteNonQueryAsync(token); }
                    await using var counts = new NpgsqlCommand("update foundation_catalog.catalog_import_batch set created_count=created_count+$3,updated_count=updated_count+$4 where tenant_id=$1 and id=$2 and status='Executing'", context.Connection, context.Transaction);
                    counts.Parameters.AddWithValue(tenantId.Value); counts.Parameters.AddWithValue(batchId); counts.Parameters.AddWithValue(creates.Length); counts.Parameters.AddWithValue(updates.Length); await counts.ExecuteNonQueryAsync(token);
                    return true;
                }, cancellationToken);
            }
            await coordinator.ExecuteAsync(tenantId, async (context, token) =>
            {
                await using var finish = new NpgsqlCommand("update foundation_catalog.catalog_import_batch set status='Committed',committed_at=now() where tenant_id=$1 and id=$2 and status='Executing'", context.Connection, context.Transaction);
                finish.Parameters.AddWithValue(tenantId.Value); finish.Parameters.AddWithValue(batchId);
                if (await finish.ExecuteNonQueryAsync(token) != 1) throw new CatalogException("catalog.import_state_conflict", "Import state changed before reconciliation.");
                return true;
            }, cancellationToken);
            return await GetSummaryAsync(tenantId, batchId, cancellationToken);
        }
        catch
        {
            await coordinator.ExecuteAsync(tenantId, async (context, token) =>
            {
                await using var reset = new NpgsqlCommand("update foundation_catalog.catalog_import_batch set status='Previewed' where tenant_id=$1 and id=$2 and status='Executing'", context.Connection, context.Transaction);
                reset.Parameters.AddWithValue(tenantId.Value); reset.Parameters.AddWithValue(batchId); await reset.ExecuteNonQueryAsync(token); return true;
            }, CancellationToken.None);
            throw;
        }
        finally
        {
            await using var release = new NpgsqlCommand("select pg_advisory_unlock(hashtextextended($1,0))", lockConnection);
            release.Parameters.AddWithValue(lockKey); await release.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    public async Task<CatalogImportSummary> RequestCommitAsync(TenantId tenantId, Guid batchId, CancellationToken cancellationToken)
    {
        var queued = await coordinator.ExecuteAsync(tenantId, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("update foundation_catalog.catalog_import_batch set status='Queued' where tenant_id=$1 and id=$2 and status='Previewed' returning id", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(batchId);
            var changed = await command.ExecuteScalarAsync(token);
            if (changed is Guid)
            {
                var now = DateTimeOffset.UtcNow; var correlation = Guid.CreateVersion7();
                context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "CatalogImportRequestedV1", JsonSerializer.Serialize(new { batchId }), tenantId, now, correlation, batchId, "catalog"));
                return true;
            }
            await using var status = new NpgsqlCommand("select status from foundation_catalog.catalog_import_batch where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
            status.Parameters.AddWithValue(tenantId.Value); status.Parameters.AddWithValue(batchId);
            var existing = await status.ExecuteScalarAsync(token) as string;
            return existing == "Queued" || existing == "Committed";
        }, cancellationToken);
        if (!queued) throw new CatalogException("catalog.import_not_previewed", "Only a reviewed import can be committed.");
        return await GetSummaryAsync(tenantId, batchId, cancellationToken);
    }

    public async Task WriteExportCsvAsync(TenantId tenantId, Guid? legalEntityId, Stream output, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await SetTenantAsync(connection, tenantId, cancellationToken);
        const string sql = """
            with active_products as (
                select p.tenant_id,p.id template_id,p.display_name,p.name_ar,p.name_en,p.product_nature,p.category_id,p.brand_id,v.id variant_id,v.sku,v.normalized_sku
                from foundation_catalog.product_template p join foundation_catalog.product_variant v on v.tenant_id=p.tenant_id and v.product_template_id=p.id and v.status='Active'
                where p.tenant_id=$1 and p.status='Active' and exists(select 1 from foundation_catalog.product_visibility x where x.tenant_id=p.tenant_id and x.template_id=p.id and (x.scope_type='TenantWide' or (x.scope_type='LegalEntity' and $2::uuid is not null and x.legal_entity_id=$2)))
            ),
            page_products as materialized (
                select * from active_products p
                where $3::text is null or (p.display_name,coalesce(p.normalized_sku,''),p.variant_id)>($3,$4,$5)
                order by p.display_name,coalesce(p.normalized_sku,''),p.variant_id
                limit $6
            ),
            primary_barcode as (
                select distinct on (b.tenant_id,b.variant_id) b.tenant_id,b.variant_id,b.value from foundation_catalog.product_barcode b join page_products p on p.tenant_id=b.tenant_id and p.variant_id=b.variant_id where b.status='Active' and b.uom_id is null order by b.tenant_id,b.variant_id,b.is_primary desc,b.normalized_value
            ),
            uom_barcode_ranked as (
                select b.tenant_id,b.variant_id,b.uom_id,b.value,row_number() over(partition by b.tenant_id,b.variant_id,b.uom_id order by b.is_primary desc,b.normalized_value) as position from foundation_catalog.product_barcode b join page_products p on p.tenant_id=b.tenant_id and p.variant_id=b.variant_id where b.status='Active' and b.uom_id is not null
            ),
            additional_uoms as (
                select conversion.tenant_id,conversion.variant_id,jsonb_agg(jsonb_build_object('uomCode',unit.code,'factorToBase',conversion.factor_to_base,'usageFlags',conversion.usage_flags,'roundingPrecision',conversion.rounding_precision,'effectiveFrom',conversion.effective_from,'effectiveTo',conversion.effective_to,'barcode',barcode.value) order by conversion.effective_from) as value
                from foundation_catalog.product_uom conversion join page_products p on p.tenant_id=conversion.tenant_id and p.variant_id=conversion.variant_id join foundation_catalog.uom unit on unit.tenant_id=conversion.tenant_id and unit.id=conversion.uom_id left join uom_barcode_ranked barcode on barcode.tenant_id=conversion.tenant_id and barcode.variant_id=conversion.variant_id and barcode.uom_id=conversion.uom_id and barcode.position=1 where not conversion.is_base and conversion.status='Active' group by conversion.tenant_id,conversion.variant_id
            ),
            base_uom as (
                select pu.tenant_id,pu.variant_id,u.code from foundation_catalog.product_uom pu join page_products p on p.tenant_id=pu.tenant_id and p.variant_id=pu.variant_id join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id where pu.is_base and pu.status='Active' and pu.effective_to is null
            ),
            attributes as (
                select assignment.tenant_id,assignment.variant_id,jsonb_object_agg(attribute.code,attribute_value.code order by attribute.code) as value from foundation_catalog.product_variant_attribute_value assignment join page_products p on p.tenant_id=assignment.tenant_id and p.variant_id=assignment.variant_id join foundation_catalog.product_attribute attribute on attribute.tenant_id=assignment.tenant_id and attribute.id=assignment.attribute_id join foundation_catalog.product_attribute_value attribute_value on attribute_value.tenant_id=assignment.tenant_id and attribute_value.attribute_id=assignment.attribute_id and attribute_value.id=assignment.attribute_value_id group by assignment.tenant_id,assignment.variant_id
            ),
            aliases as (
                select alias.tenant_id,alias.variant_id,jsonb_agg(jsonb_build_object('alias',alias.alias,'type',alias.alias_type,'languageCode',alias.language_code) order by alias.normalized_alias) as value from foundation_catalog.product_alias alias join page_products p on p.tenant_id=alias.tenant_id and p.variant_id=alias.variant_id group by alias.tenant_id,alias.variant_id
            ),
            reference_data as (
                select reference.tenant_id,reference.variant_id,jsonb_agg(jsonb_build_object('referenceType',reference.reference_type,'referenceCode',reference.reference_code) order by reference.reference_type,reference.reference_code) as value from foundation_catalog.product_reference reference join page_products p on p.tenant_id=reference.tenant_id and p.variant_id=reference.variant_id where reference.status='Active' group by reference.tenant_id,reference.variant_id
            ),
            translations as (
                select translation.tenant_id,translation.template_id,jsonb_agg(jsonb_build_object('languageCode',translation.language_code,'name',translation.name,'description',translation.description) order by translation.language_code) as value from foundation_catalog.product_translation translation join (select distinct tenant_id,template_id from page_products) p on p.tenant_id=translation.tenant_id and p.template_id=translation.template_id group by translation.tenant_id,translation.template_id
            )
            select p.display_name,p.name_ar,p.name_en,p.product_nature,p.sku,c.name,b.name,base.code,primary_barcode.value,coalesce(additional_uoms.value,'[]'::jsonb)::text,coalesce(attributes.value,'{}'::jsonb)::text,coalesce(aliases.value,'[]'::jsonb)::text,coalesce(reference_data.value,'[]'::jsonb)::text,coalesce(translations.value,'[]'::jsonb)::text,p.display_name,coalesce(p.normalized_sku,''),p.variant_id
            from page_products p left join foundation_catalog.product_category c on c.tenant_id=p.tenant_id and c.id=p.category_id left join foundation_catalog.brand b on b.tenant_id=p.tenant_id and b.id=p.brand_id left join base_uom base on base.tenant_id=p.tenant_id and base.variant_id=p.variant_id left join primary_barcode on primary_barcode.tenant_id=p.tenant_id and primary_barcode.variant_id=p.variant_id left join additional_uoms on additional_uoms.tenant_id=p.tenant_id and additional_uoms.variant_id=p.variant_id left join attributes on attributes.tenant_id=p.tenant_id and attributes.variant_id=p.variant_id left join aliases on aliases.tenant_id=p.tenant_id and aliases.variant_id=p.variant_id left join reference_data on reference_data.tenant_id=p.tenant_id and reference_data.variant_id=p.variant_id left join translations on translations.tenant_id=p.tenant_id and translations.template_id=p.template_id order by p.display_name,coalesce(p.normalized_sku,''),p.variant_id
            """;
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), 64 * 1024, leaveOpen: true);
        await writer.WriteLineAsync("Name,NameAr,NameEn,Type,SKU,Category,Brand,BaseUOM,Barcode,AdditionalUomsJson,AttributesJson,AliasesJson,ReferencesJson,TranslationsJson");
        string? afterName = null;
        string? afterSku = null;
        Guid? afterVariantId = null;
        while (true)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value); command.Parameters.AddWithValue(afterName ?? (object)DBNull.Value); command.Parameters.AddWithValue(afterSku ?? (object)DBNull.Value); command.Parameters.AddWithValue(afterVariantId ?? (object)DBNull.Value); command.Parameters.AddWithValue(250);
            await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken);
            var rows = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                var values = new string?[] { reader.GetString(0), NullableString(reader, 1), NullableString(reader, 2), reader.GetString(3), NullableString(reader, 4), NullableString(reader, 5), NullableString(reader, 6), NullableString(reader, 7), NullableString(reader, 8), reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12), reader.GetString(13) };
                await writer.WriteLineAsync(string.Join(',', values.Select(CsvCell)));
                afterName = reader.GetString(14); afterSku = reader.GetString(15); afterVariantId = reader.GetGuid(16); rows++;
            }
            if (rows < 250) break;
        }
        await writer.FlushAsync(cancellationToken);
    }

    private static async Task InsertCreatedAsync(ITransactionContext context, TenantId tenantId, Guid batchId, IReadOnlyList<StoredRow> rows, CancellationToken cancellationToken)
    {
        var createJson = JsonSerializer.Serialize(rows.Select(row => new { row.TargetTemplateId, row.TargetVariantId, row.Source.DisplayName, displayNameNormalized = CatalogNormalization.Search(row.Source.DisplayName), row.Source.NameAr, row.Source.NameEn, productNature = row.Source.Nature.ToString(), row.CategoryId, row.BrandId, row.BaseUomId, row.NormalizedSku, row.Source.Sku, row.Source.Barcode, row.NormalizedBarcode, row.Source.ExternalReference }), SqlJsonOptions);
        var grouped = rows.GroupBy(row => row.TargetTemplateId).Select(group => group.First()).ToArray();
        var templateJson = JsonSerializer.Serialize(grouped.Select(row => new { row.TargetTemplateId, row.Source.DisplayName, displayNameNormalized = CatalogNormalization.Search(row.Source.DisplayName), row.Source.NameAr, row.Source.NameEn, productNature = row.Source.Nature.ToString(), row.CategoryId, row.BrandId }), SqlJsonOptions);
        const string templateSql = "insert into foundation_catalog.product_template (tenant_id,id,display_name,display_name_normalized,name_ar,name_en,product_nature,category_id,brand_id,status,created_at,updated_at,version,product_source,source_batch_id) select $2,x.target_template_id,x.display_name,x.display_name_normalized,x.name_ar,x.name_en,x.product_nature,x.category_id,x.brand_id,'Active',now(),now(),1,'Import',$3 from jsonb_to_recordset($1) as x(target_template_id uuid,display_name text,display_name_normalized text,name_ar text,name_en text,product_nature text,category_id uuid,brand_id uuid) on conflict (tenant_id,id) do nothing";
        await using (var insert = new NpgsqlCommand(templateSql, context.Connection, context.Transaction)) { insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = templateJson }); insert.Parameters.AddWithValue(tenantId.Value); insert.Parameters.AddWithValue(batchId); await insert.ExecuteNonQueryAsync(cancellationToken); }
        await using (var visibility = new NpgsqlCommand("insert into foundation_catalog.product_visibility (tenant_id,template_id,scope_type) select $1,x.template_id,'TenantWide' from unnest($2::uuid[]) as x(template_id) on conflict do nothing", context.Connection, context.Transaction)) { visibility.Parameters.AddWithValue(tenantId.Value); visibility.Parameters.AddWithValue(grouped.Select(row => row.TargetTemplateId).ToArray()); await visibility.ExecuteNonQueryAsync(cancellationToken); }
        var templateAttributes = rows.SelectMany(row => row.Attributes.Select(attribute => new { row.TargetTemplateId, attribute.AttributeId })).Distinct().ToArray();
        if (templateAttributes.Length > 0)
        {
            const string templateAttributeSql = "insert into foundation_catalog.product_template_attribute (tenant_id,template_id,attribute_id,display_order,is_required) select $2,x.template_id,x.attribute_id,row_number() over(partition by x.template_id order by x.attribute_id)-1,true from jsonb_to_recordset($1) as x(template_id uuid,attribute_id uuid) on conflict (tenant_id,template_id,attribute_id) do update set is_required=true";
            await using var insertAttributes = new NpgsqlCommand(templateAttributeSql, context.Connection, context.Transaction);
            insertAttributes.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(templateAttributes.Select(item => new { templateId = item.TargetTemplateId, item.AttributeId }), SqlJsonOptions) });
            insertAttributes.Parameters.AddWithValue(tenantId.Value); await insertAttributes.ExecuteNonQueryAsync(cancellationToken);
        }
        const string variantSql = "insert into foundation_catalog.product_variant (tenant_id,id,product_template_id,sku,normalized_sku,is_default_variant,display_suffix,combination_signature,status,created_at,updated_at,version) select $2,x.target_variant_id,x.target_template_id,x.sku,x.normalized_sku,(row_number() over(partition by x.target_template_id order by x.row_number)=1 and not exists(select 1 from foundation_catalog.product_variant current_default where current_default.tenant_id=$2 and current_default.product_template_id=x.target_template_id and current_default.is_default_variant)),null,x.combination_signature,'Active',now(),now(),1 from jsonb_to_recordset($1) as x(target_template_id uuid,target_variant_id uuid,sku text,normalized_sku text,row_number integer,combination_signature text)";
        createJson = JsonSerializer.Serialize(rows.Select(row => new { row.TargetTemplateId, row.TargetVariantId, row.Source.Sku, row.NormalizedSku, row.RowNumber, combinationSignature = AttributeSignature(row.Attributes) }), SqlJsonOptions);
        await using (var insert = new NpgsqlCommand(variantSql, context.Connection, context.Transaction)) { insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = createJson }); insert.Parameters.AddWithValue(tenantId.Value); await insert.ExecuteNonQueryAsync(cancellationToken); }
        var variantAttributes = rows.SelectMany(row => row.Attributes.Select(attribute => new { variantId = row.TargetVariantId, attribute.AttributeId, attribute.AttributeValueId })).ToArray();
        if (variantAttributes.Length > 0)
        {
            const string variantAttributeSql = "insert into foundation_catalog.product_variant_attribute_value (tenant_id,variant_id,attribute_id,attribute_value_id) select $2,x.variant_id,x.attribute_id,x.attribute_value_id from jsonb_to_recordset($1) as x(variant_id uuid,attribute_id uuid,attribute_value_id uuid)";
            await using var insertVariantAttributes = new NpgsqlCommand(variantAttributeSql, context.Connection, context.Transaction);
            insertVariantAttributes.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(variantAttributes, SqlJsonOptions) });
            insertVariantAttributes.Parameters.AddWithValue(tenantId.Value); await insertVariantAttributes.ExecuteNonQueryAsync(cancellationToken);
        }
        const string uomSql = "insert into foundation_catalog.product_uom (tenant_id,id,variant_id,uom_id,factor_to_base,is_base,effective_from,status,version) select $2,gen_random_uuid(),x.target_variant_id,x.base_uom_id,1,true,now(),'Active',1 from jsonb_to_recordset($1) as x(target_variant_id uuid,base_uom_id uuid)";
        await using (var insert = new NpgsqlCommand(uomSql, context.Connection, context.Transaction)) { insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(rows.Select(row => new { row.TargetVariantId, row.BaseUomId }), SqlJsonOptions) }); insert.Parameters.AddWithValue(tenantId.Value); await insert.ExecuteNonQueryAsync(cancellationToken); }
        var barcodeRows = rows.Where(row => !string.IsNullOrWhiteSpace(row.Source.Barcode)).ToArray();
        if (barcodeRows.Length > 0)
        {
            const string barcodeSql = "insert into foundation_catalog.product_barcode (tenant_id,id,variant_id,barcode_type,value,normalized_value,is_primary,status,created_at) select $2,gen_random_uuid(),x.target_variant_id,'Other',x.barcode,x.normalized_barcode,true,'Active',now() from jsonb_to_recordset($1) as x(target_variant_id uuid,barcode text,normalized_barcode text)";
            await using var insert = new NpgsqlCommand(barcodeSql, context.Connection, context.Transaction); insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(barcodeRows.Select(row => new { row.TargetVariantId, barcode = row.Source.Barcode!.Trim(), row.NormalizedBarcode }), SqlJsonOptions) }); insert.Parameters.AddWithValue(tenantId.Value); await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        var additionalUoms = rows.SelectMany(row => row.AdditionalUoms.Select(uom => new { row.TargetVariantId, Uom = uom })).ToArray();
        if (additionalUoms.Length > 0)
        {
            const string additionalUomSql = "insert into foundation_catalog.product_uom (tenant_id,id,variant_id,uom_id,factor_to_base,is_base,usage_flags,rounding_precision,effective_from,effective_to,status,version) select $2,gen_random_uuid(),x.variant_id,x.uom_id,x.factor_to_base,false,coalesce(x.usage_flags,'{}'),x.rounding_precision,x.effective_from,x.effective_to,'Active',1 from jsonb_to_recordset($1) as x(variant_id uuid,uom_id uuid,factor_to_base numeric,usage_flags text[],rounding_precision smallint,effective_from timestamptz,effective_to timestamptz)";
            await using var insertUom = new NpgsqlCommand(additionalUomSql, context.Connection, context.Transaction);
            insertUom.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(additionalUoms.Select(item => new { variantId = item.TargetVariantId, uomId = item.Uom.UomId, item.Uom.FactorToBase, usageFlags = item.Uom.UsageFlags?.ToArray() ?? [], item.Uom.RoundingPrecision, item.Uom.EffectiveFrom, item.Uom.EffectiveTo }), SqlJsonOptions) });
            insertUom.Parameters.AddWithValue(tenantId.Value); await insertUom.ExecuteNonQueryAsync(cancellationToken);
            var uomBarcodes = additionalUoms.Where(item => item.Uom.NormalizedBarcode is not null).ToArray();
            if (uomBarcodes.Length > 0)
            {
                const string barcodeSql = "insert into foundation_catalog.product_barcode (tenant_id,id,variant_id,uom_id,barcode_type,value,normalized_value,is_primary,status,created_at) select $2,gen_random_uuid(),x.variant_id,x.uom_id,'Other',x.barcode,x.normalized_barcode,false,'Active',now() from jsonb_to_recordset($1) as x(variant_id uuid,uom_id uuid,barcode text,normalized_barcode text)";
                await using var insertBarcode = new NpgsqlCommand(barcodeSql, context.Connection, context.Transaction);
                insertBarcode.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(uomBarcodes.Select(item => new { variantId = item.TargetVariantId, uomId = item.Uom.UomId, barcode = item.Uom.Barcode!.Trim(), normalizedBarcode = item.Uom.NormalizedBarcode }), SqlJsonOptions) });
                insertBarcode.Parameters.AddWithValue(tenantId.Value); await insertBarcode.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        var externalRows = rows.Where(row => !string.IsNullOrWhiteSpace(row.Source.ExternalReference)).ToArray();
        if (externalRows.Length > 0)
        {
            const string referenceSql = "insert into foundation_catalog.product_reference (tenant_id,id,variant_id,reference_type,party_id,reference_code,status) select $2,gen_random_uuid(),x.target_variant_id,'External',null,x.external_reference,'Active' from jsonb_to_recordset($1) as x(target_variant_id uuid,external_reference text)";
            await using var insert = new NpgsqlCommand(referenceSql, context.Connection, context.Transaction); insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(externalRows.Select(row => new { row.TargetVariantId, externalReference = row.Source.ExternalReference!.Trim() }), SqlJsonOptions) }); insert.Parameters.AddWithValue(tenantId.Value); await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await RefreshProjectionAsync(context, tenantId.Value, rows.Select(row => row.TargetTemplateId).Distinct().ToArray(), cancellationToken);
        await WriteImportAuditAsync(context, tenantId, batchId, rows.Select(row => row.TargetTemplateId).Distinct().ToArray(), "catalog_products_imported", cancellationToken);
        foreach (var row in rows)
        {
            var now = DateTimeOffset.UtcNow; var correlation = Guid.CreateVersion7();
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "CatalogProductCreatedV1", JsonSerializer.Serialize(new { templateId = row.TargetTemplateId, variantId = row.TargetVariantId }), tenantId, now, correlation, batchId, "catalog"));
            if (row.Attributes.Length > 0) context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "CatalogProductAttributesChangedV1", JsonSerializer.Serialize(new { templateId = row.TargetTemplateId, variantId = row.TargetVariantId }), tenantId, now, correlation, batchId, "catalog"));
            if (!string.IsNullOrWhiteSpace(row.Source.ExternalReference)) context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "CatalogProductReferenceAddedV1", JsonSerializer.Serialize(new { templateId = row.TargetTemplateId, variantId = row.TargetVariantId, referenceType = "External" }), tenantId, now, correlation, batchId, "catalog"));
        }
    }

    private static async Task UpdateMatchedAsync(ITransactionContext context, TenantId tenantId, Guid batchId, IReadOnlyList<StoredRow> rows, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(rows.Select(row => new { row.MatchTemplateId, row.MatchVariantId, row.Source.DisplayName, displayNameNormalized = CatalogNormalization.Search(row.Source.DisplayName), row.Source.NameAr, row.Source.NameEn, productNature = row.Source.Nature.ToString(), row.CategoryId, row.BrandId, row.TargetTemplateId, row.Source.Sku, row.NormalizedSku }), SqlJsonOptions);
        const string auditSql = "insert into platform.audit_entry (id,tenant_id,actor_type,actor_id,membership_id,module,action,entity_type,entity_id,before_json,after_json,changed_fields,source,correlation_id,occurred_at,metadata_json) select gen_random_uuid(),p.tenant_id,'User',b.created_by,b.created_membership_id,'catalog','catalog_products_import_updated','product',p.id,jsonb_build_object('displayName',p.display_name,'nameAr',p.name_ar,'nameEn',p.name_en,'nature',p.product_nature,'sku',v.sku,'categoryId',p.category_id,'brandId',p.brand_id),jsonb_build_object('displayName',x.display_name,'nameAr',x.name_ar,'nameEn',x.name_en,'nature',x.product_nature,'sku',coalesce(x.sku,v.sku),'categoryId',coalesce(x.category_id,p.category_id),'brandId',coalesce(x.brand_id,p.brand_id)),to_jsonb(array_remove(array[case when p.display_name is distinct from x.display_name then 'displayName' end,case when p.name_ar is distinct from x.name_ar then 'nameAr' end,case when p.name_en is distinct from x.name_en then 'nameEn' end,case when p.product_nature is distinct from x.product_nature then 'nature' end,case when v.sku is distinct from x.sku and x.sku is not null then 'sku' end,case when x.category_id is not null and p.category_id is distinct from x.category_id then 'categoryId' end,case when x.brand_id is not null and p.brand_id is distinct from x.brand_id then 'brandId' end]::text[],null)), 'import',b.id,now(),jsonb_build_object('batchId',b.id,'source','Import') from foundation_catalog.product_template p join foundation_catalog.product_variant v on v.tenant_id=p.tenant_id and v.product_template_id=p.id join jsonb_to_recordset($1) as x(match_template_id uuid,match_variant_id uuid,display_name text,display_name_normalized text,name_ar text,name_en text,product_nature text,category_id uuid,brand_id uuid,target_template_id uuid,sku text,normalized_sku text) on x.match_template_id=p.id and x.match_variant_id=v.id join foundation_catalog.catalog_import_batch b on b.tenant_id=p.tenant_id and b.id=$3 where p.tenant_id=$2";
        await using (var audit = new NpgsqlCommand(auditSql, context.Connection, context.Transaction)) { audit.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json }); audit.Parameters.AddWithValue(tenantId.Value); audit.Parameters.AddWithValue(batchId); await audit.ExecuteNonQueryAsync(cancellationToken); }
        const string sql = "update foundation_catalog.product_template p set display_name=x.display_name,display_name_normalized=x.display_name_normalized,name_ar=x.name_ar,name_en=x.name_en,product_nature=x.product_nature,category_id=coalesce(x.category_id,p.category_id),brand_id=coalesce(x.brand_id,p.brand_id),product_source='Import',source_batch_id=$3,updated_at=now(),version=version+1 from jsonb_to_recordset($1) as x(match_template_id uuid,match_variant_id uuid,display_name text,display_name_normalized text,name_ar text,name_en text,product_nature text,category_id uuid,brand_id uuid,target_template_id uuid,sku text,normalized_sku text) where p.tenant_id=$2 and p.id=x.match_template_id";
        await using (var update = new NpgsqlCommand(sql, context.Connection, context.Transaction)) { update.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json }); update.Parameters.AddWithValue(tenantId.Value); update.Parameters.AddWithValue(batchId); await update.ExecuteNonQueryAsync(cancellationToken); }
        const string skuSql = "update foundation_catalog.product_variant v set sku=x.sku,normalized_sku=x.normalized_sku,updated_at=now(),version=version+1 from jsonb_to_recordset($1) as x(match_variant_id uuid,sku text,normalized_sku text) where v.tenant_id=$2 and v.id=x.match_variant_id and x.sku is not null";
        await using (var update = new NpgsqlCommand(skuSql, context.Connection, context.Transaction)) { update.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json }); update.Parameters.AddWithValue(tenantId.Value); await update.ExecuteNonQueryAsync(cancellationToken); }
        var templateAttributes = rows.SelectMany(row => row.Attributes.Select(attribute => new { TemplateId = row.MatchTemplateId!.Value, attribute.AttributeId })).Distinct().ToArray();
        if (templateAttributes.Length > 0)
        {
            const string templateAttributeSql = "insert into foundation_catalog.product_template_attribute (tenant_id,template_id,attribute_id,display_order,is_required) select $2,x.template_id,x.attribute_id,row_number() over(partition by x.template_id order by x.attribute_id)-1,true from jsonb_to_recordset($1) as x(template_id uuid,attribute_id uuid) on conflict (tenant_id,template_id,attribute_id) do update set is_required=true";
            await using var insertAttributes = new NpgsqlCommand(templateAttributeSql, context.Connection, context.Transaction);
            insertAttributes.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(templateAttributes.Select(item => new { templateId = item.TemplateId, item.AttributeId }), SqlJsonOptions) });
            insertAttributes.Parameters.AddWithValue(tenantId.Value); await insertAttributes.ExecuteNonQueryAsync(cancellationToken);
        }
        var variantAttributes = rows.SelectMany(row => row.Attributes.Select(attribute => new { variantId = row.MatchVariantId!.Value, attribute.AttributeId, attribute.AttributeValueId })).ToArray();
        if (variantAttributes.Length > 0)
        {
            const string variantAttributeSql = "insert into foundation_catalog.product_variant_attribute_value (tenant_id,variant_id,attribute_id,attribute_value_id) select $2,x.variant_id,x.attribute_id,x.attribute_value_id from jsonb_to_recordset($1) as x(variant_id uuid,attribute_id uuid,attribute_value_id uuid) on conflict (tenant_id,variant_id,attribute_id) do update set attribute_value_id=excluded.attribute_value_id";
            await using var insertVariantAttributes = new NpgsqlCommand(variantAttributeSql, context.Connection, context.Transaction);
            insertVariantAttributes.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(variantAttributes, SqlJsonOptions) });
            insertVariantAttributes.Parameters.AddWithValue(tenantId.Value); await insertVariantAttributes.ExecuteNonQueryAsync(cancellationToken);
            await using var signature = new NpgsqlCommand("update foundation_catalog.product_variant v set combination_signature=coalesce((select string_agg(replace(x.attribute_id::text,'-','')||'='||replace(x.attribute_value_id::text,'-',''),';' order by x.attribute_id) from foundation_catalog.product_variant_attribute_value x where x.tenant_id=v.tenant_id and x.variant_id=v.id),'') where v.tenant_id=$1 and v.id=any($2)", context.Connection, context.Transaction);
            signature.Parameters.AddWithValue(tenantId.Value); signature.Parameters.AddWithValue(variantAttributes.Select(item => item.variantId).Distinct().ToArray()); await signature.ExecuteNonQueryAsync(cancellationToken);
        }
        var additionalUoms = rows.SelectMany(row => row.AdditionalUoms.Select(uom => new { VariantId = row.MatchVariantId!.Value, Uom = uom })).ToArray();
        if (additionalUoms.Length > 0)
        {
            const string insertUomSql = "insert into foundation_catalog.product_uom (tenant_id,id,variant_id,uom_id,factor_to_base,is_base,usage_flags,rounding_precision,effective_from,effective_to,status,version) select $2,gen_random_uuid(),x.variant_id,x.uom_id,x.factor_to_base,false,coalesce(x.usage_flags,'{}'),x.rounding_precision,x.effective_from,x.effective_to,'Active',1 from jsonb_to_recordset($1) as x(variant_id uuid,uom_id uuid,factor_to_base numeric,usage_flags text[],rounding_precision smallint,effective_from timestamptz,effective_to timestamptz)";
            await using var insertUom = new NpgsqlCommand(insertUomSql, context.Connection, context.Transaction);
            insertUom.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(additionalUoms.Select(item => new { variantId = item.VariantId, uomId = item.Uom.UomId, item.Uom.FactorToBase, usageFlags = item.Uom.UsageFlags?.ToArray() ?? [], item.Uom.RoundingPrecision, item.Uom.EffectiveFrom, item.Uom.EffectiveTo }), SqlJsonOptions) });
            insertUom.Parameters.AddWithValue(tenantId.Value); await insertUom.ExecuteNonQueryAsync(cancellationToken);
            var uomBarcodes = additionalUoms.Where(item => item.Uom.NormalizedBarcode is not null).ToArray();
            if (uomBarcodes.Length > 0)
            {
                const string insertBarcodeSql = "insert into foundation_catalog.product_barcode (tenant_id,id,variant_id,uom_id,barcode_type,value,normalized_value,is_primary,status,created_at) select $2,gen_random_uuid(),x.variant_id,x.uom_id,'Other',x.barcode,x.normalized_barcode,false,'Active',now() from jsonb_to_recordset($1) as x(variant_id uuid,uom_id uuid,barcode text,normalized_barcode text)";
                await using var insertBarcode = new NpgsqlCommand(insertBarcodeSql, context.Connection, context.Transaction);
                insertBarcode.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(uomBarcodes.Select(item => new { variantId = item.VariantId, uomId = item.Uom.UomId, barcode = item.Uom.Barcode!.Trim(), normalizedBarcode = item.Uom.NormalizedBarcode }), SqlJsonOptions) });
                insertBarcode.Parameters.AddWithValue(tenantId.Value); await insertBarcode.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await RefreshProjectionAsync(context, tenantId.Value, rows.Select(row => row.MatchTemplateId!.Value).Distinct().ToArray(), cancellationToken);
        foreach (var templateId in rows.Select(row => row.MatchTemplateId!.Value).Distinct())
        {
            var now = DateTimeOffset.UtcNow; var correlation = Guid.CreateVersion7();
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "CatalogProductChangedV1", JsonSerializer.Serialize(new { templateId }), tenantId, now, correlation, batchId, "catalog"));
        }
    }

    private static async Task WriteImportAuditAsync(ITransactionContext context, TenantId tenantId, Guid batchId, IReadOnlyList<Guid> templateIds, string action, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("insert into platform.audit_entry (id,tenant_id,actor_type,actor_id,membership_id,module,action,entity_type,entity_id,after_json,source,correlation_id,occurred_at,metadata_json) select gen_random_uuid(),$1,'User',b.created_by,b.created_membership_id,'catalog',$2,'product',x.template_id,jsonb_build_object('source','Import','batchId',b.id), 'import',b.id,now(),jsonb_build_object('batchId',b.id) from foundation_catalog.catalog_import_batch b cross join unnest($3::uuid[]) as x(template_id) where b.tenant_id=$1 and b.id=$4", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(action); command.Parameters.AddWithValue(templateIds.ToArray()); command.Parameters.AddWithValue(batchId); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RefreshProjectionAsync(ITransactionContext context, Guid tenantId, IReadOnlyList<Guid> templateIds, CancellationToken cancellationToken)
    {
        await using (var delete = new NpgsqlCommand("delete from foundation_catalog.catalog_search_projection where tenant_id=$1 and template_id=any($2)", context.Connection, context.Transaction)) { delete.CommandTimeout = 0; delete.Parameters.AddWithValue(tenantId); delete.Parameters.AddWithValue(templateIds.ToArray()); await delete.ExecuteNonQueryAsync(cancellationToken); }
        const string sql = "insert into foundation_catalog.catalog_search_projection (tenant_id,variant_id,template_id,display_name,display_name_normalized,name_ar,name_en,sku,normalized_sku,category_id,category_name,base_uom_code,product_nature,status,updated_at) select p.tenant_id,v.id,p.id,p.display_name,p.display_name_normalized,p.name_ar,p.name_en,v.sku,v.normalized_sku,p.category_id,c.name,u.code,p.product_nature,p.status,p.updated_at from foundation_catalog.product_template p join foundation_catalog.product_variant v on v.tenant_id=p.tenant_id and v.product_template_id=p.id left join foundation_catalog.product_category c on c.tenant_id=p.tenant_id and c.id=p.category_id left join foundation_catalog.product_uom pu on pu.tenant_id=v.tenant_id and pu.variant_id=v.id and pu.is_base and pu.status='Active' and pu.effective_to is null left join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id where p.tenant_id=$1 and p.id=any($2)";
        await using var insert = new NpgsqlCommand(sql, context.Connection, context.Transaction); insert.CommandTimeout = 0; insert.Parameters.AddWithValue(tenantId); insert.Parameters.AddWithValue(templateIds.ToArray()); await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<StoredRow>> ReadRowsAsync(TenantId tenantId, Guid batchId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await SetTenantAsync(connection, tenantId, cancellationToken);
        const string sql = "select row_number,row_data::text,normalized_sku,normalized_barcode,match_template_id,match_variant_id,category_id,brand_id,base_uom_id,target_template_id,target_variant_id,classification,errors from foundation_catalog.catalog_import_row where tenant_id=$1 and batch_id=$2 and processed_at is null order by row_number";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(batchId);
        var rows = new List<StoredRow>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) { var payload = JsonSerializer.Deserialize<StoredImportData>(reader.GetString(1), JsonOptions)!; rows.Add(new StoredRow(reader.GetInt32(0), payload.Source, payload.AdditionalUoms, payload.Attributes, reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), NullableGuid(reader, 4), NullableGuid(reader, 5), NullableGuid(reader, 6), NullableGuid(reader, 7), NullableGuid(reader, 8), reader.GetGuid(9), reader.GetGuid(10), reader.GetString(11), reader.GetFieldValue<string[]>(12))); }
        return rows;
    }

    private static async Task LoadReferencesAsync(NpgsqlConnection connection, TenantId tenantId, PreviewRow[] rows, CancellationToken cancellationToken)
    {
        var categories = await ReadLookupAsync(connection, "select name_normalized,id from foundation_catalog.product_category where tenant_id=$1 and status='Active'", tenantId, cancellationToken);
        var brands = await ReadLookupAsync(connection, "select name_normalized,id from foundation_catalog.brand where tenant_id=$1 and status='Active'", tenantId, cancellationToken);
        var uoms = await ReadUomLookupAsync(connection, tenantId, cancellationToken);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row.Source.Category))
            {
                if (categories.TryGetValue(CatalogNormalization.Search(row.Source.Category), out var categoryId)) row.CategoryId = categoryId;
                else row.Issues.Add("unknown_category");
            }
            if (!string.IsNullOrWhiteSpace(row.Source.Brand) && brands.TryGetValue(CatalogNormalization.Search(row.Source.Brand), out var brandId)) row.BrandId = brandId;
            if (!string.IsNullOrWhiteSpace(row.Source.BaseUomCode) && uoms.TryGetValue(CatalogNormalization.Text(row.Source.BaseUomCode), out var uom)) { row.BaseUomId = uom.Id; row.BaseUomDimension = uom.Dimension; }
            else row.Issues.Add("unknown_uom");
            foreach (var additional in row.AdditionalUoms)
            {
                if (!uoms.TryGetValue(CatalogNormalization.Text(additional.UomCode), out var additionalUom)) { row.Issues.Add("unknown_uom"); continue; }
                if (row.BaseUomDimension is not null && additionalUom.Dimension != row.BaseUomDimension) row.Issues.Add("invalid_conversion_dimension");
                additional.UomId = additionalUom.Id;
            }
        }
        var attributeCodes = rows.SelectMany(row => row.Source.Attributes?.Keys ?? []).Select(CatalogNormalization.Text).Distinct(StringComparer.Ordinal).ToArray();
        if (attributeCodes.Length == 0) return;
        const string attributeSql = "select a.normalized_code,a.id,a.is_variant_defining,av.normalized_code,av.id from foundation_catalog.product_attribute a left join foundation_catalog.product_attribute_value av on av.tenant_id=a.tenant_id and av.attribute_id=a.id and av.status='Active' where a.tenant_id=$1 and a.status='Active' and a.normalized_code=any($2)";
        var attributes = new Dictionary<string, (Guid Id, bool IsVariantDefining, Dictionary<string, Guid> Values)>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand(attributeSql, connection))
        {
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(attributeCodes);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader.GetString(0);
                if (!attributes.TryGetValue(code, out var entry)) entry = (reader.GetGuid(1), reader.GetBoolean(2), new Dictionary<string, Guid>(StringComparer.Ordinal));
                if (!reader.IsDBNull(3)) entry.Values[reader.GetString(3)] = reader.GetGuid(4);
                attributes[code] = entry;
            }
        }
        foreach (var row in rows)
            foreach (var (code, valueCode) in row.Source.Attributes ?? new Dictionary<string, string>())
            {
                var normalizedCode = CatalogNormalization.Text(code); var normalizedValue = CatalogNormalization.Text(valueCode);
                if (!attributes.TryGetValue(normalizedCode, out var attribute)) { row.Issues.Add("unknown_attribute"); continue; }
                if (!attribute.IsVariantDefining) { row.Issues.Add("attribute_not_variant_defining"); continue; }
                if (!attribute.Values.TryGetValue(normalizedValue, out var valueId)) { row.Issues.Add("unknown_attribute_value"); continue; }
                row.ResolvedAttributes[attribute.Id] = valueId;
            }
    }

    private static async Task<Dictionary<string, (Guid Id, string Dimension)>> ReadUomLookupAsync(NpgsqlConnection connection, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select code,id,dimension from foundation_catalog.uom where tenant_id=$1 and status='Active'", connection); command.Parameters.AddWithValue(tenantId.Value);
        var result = new Dictionary<string, (Guid, string)>(StringComparer.Ordinal); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result[reader.GetString(0)] = (reader.GetGuid(1), reader.GetString(2)); return result;
    }

    private static void ValidateAdditionalUoms(PreviewRow[] rows)
    {
        foreach (var row in rows)
        {
            if (row.AdditionalUoms.Length > 20) { row.Issues.Add("too_many_additional_uoms"); continue; }
            if (row.Source.Attributes?.Count > 32 || row.Source.Attributes?.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 64 || string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length > 64) == true)
                row.Issues.Add("invalid_attributes");
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var uom in row.AdditionalUoms)
            {
                var normalizedCode = CatalogNormalization.Text(uom.UomCode ?? string.Empty);
                if (string.IsNullOrWhiteSpace(normalizedCode) || !codes.Add(normalizedCode) || uom.FactorToBase <= 0 || uom.EffectiveFrom == DateTimeOffset.MinValue || uom.EffectiveTo is not null && uom.EffectiveTo <= uom.EffectiveFrom || uom.RoundingPrecision is < 0 or > 6 || uom.UsageFlags is { Count: > 8 } || uom.UsageFlags?.Any(flag => string.IsNullOrWhiteSpace(flag) || flag.Trim().Length > 32) == true) row.Issues.Add("invalid_conversion");
                if (uom.Barcode is not null)
                {
                    try { uom.NormalizedBarcode = IdentifierRules.RequireBarcode(uom.Barcode); }
                    catch (ArgumentException) { row.Issues.Add("invalid_barcode"); }
                }
            }
        }
    }

    private static async Task ClassifyExistingUomIntervalsAsync(NpgsqlConnection connection, TenantId tenantId, PreviewRow[] rows, CancellationToken cancellationToken)
    {
        var matched = rows.Where(row => row.MatchVariantId is not null).Select(row => row.MatchVariantId!.Value).Distinct().ToArray();
        if (matched.Length == 0) return;
        const string sql = "select pu.variant_id,u.code,pu.effective_from,pu.effective_to from foundation_catalog.product_uom pu join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id where pu.tenant_id=$1 and pu.variant_id=any($2) and pu.status='Active'";
        var periods = new List<(Guid VariantId, string Code, DateTimeOffset From, DateTimeOffset? To)>();
        await using (var command = new NpgsqlCommand(sql, connection)) { command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(matched); await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) periods.Add((reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3))); }
        foreach (var row in rows.Where(row => row.MatchVariantId is not null))
            foreach (var added in row.AdditionalUoms)
            {
                var normalizedCode = CatalogNormalization.Text(added.UomCode ?? string.Empty);
                if (periods.Any(existing => existing.VariantId == row.MatchVariantId && CatalogNormalization.Text(existing.Code) == normalizedCode && IntervalsOverlap(existing.From, existing.To, added.EffectiveFrom, added.EffectiveTo))) row.Issues.Add("uom_effective_period_conflict");
            }
    }

    private static bool IntervalsOverlap(DateTimeOffset firstFrom, DateTimeOffset? firstTo, DateTimeOffset secondFrom, DateTimeOffset? secondTo) => (firstTo is null || secondFrom < firstTo) && (secondTo is null || firstFrom < secondTo);

    private static async Task<Dictionary<string, Guid>> ReadLookupAsync(NpgsqlConnection connection, string sql, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); var result = new Dictionary<string, Guid>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) result[reader.GetString(0)] = reader.GetGuid(1); return result;
    }

    private static async Task ClassifyExistingAsync(NpgsqlConnection connection, TenantId tenantId, PreviewRow[] rows, CancellationToken cancellationToken)
    {
        var skus = rows.Where(row => row.NormalizedSku is not null).Select(row => row.NormalizedSku!).Distinct().ToArray();
        var barcodes = rows.SelectMany(row => new[] { row.NormalizedBarcode }.Concat(row.AdditionalUoms.Select(uom => uom.NormalizedBarcode))).Where(value => value is not null).Select(value => value!).Distinct().ToArray();
        var names = rows.Where(row => !string.IsNullOrWhiteSpace(row.Source.DisplayName)).Select(row => CatalogNormalization.Search(row.Source.DisplayName)).Distinct().ToArray();
        var externalReferences = rows.Where(row => !string.IsNullOrWhiteSpace(row.Source.ExternalReference)).Select(row => row.Source.ExternalReference!.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        const string sql = "select v.normalized_sku,v.id,p.id,p.display_name_normalized,b.normalized_value,r.reference_code from foundation_catalog.product_variant v join foundation_catalog.product_template p on p.tenant_id=v.tenant_id and p.id=v.product_template_id left join foundation_catalog.product_barcode b on b.tenant_id=v.tenant_id and b.variant_id=v.id and b.status='Active' and b.normalized_value=any($3) left join foundation_catalog.product_reference r on r.tenant_id=v.tenant_id and r.variant_id=v.id and r.reference_type='External' and r.status='Active' and r.reference_code=any($5) where v.tenant_id=$1 and (v.normalized_sku=any($2) or p.display_name_normalized=any($4) or b.id is not null or r.id is not null)";
        var bySku = new Dictionary<string, (Guid Variant, Guid Template)>(StringComparer.Ordinal); var byBarcode = new Dictionary<string, (Guid Variant, Guid Template)>(StringComparer.Ordinal); var byExternal = new Dictionary<string, (Guid Variant, Guid Template)>(StringComparer.Ordinal); var byName = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(skus); command.Parameters.AddWithValue(barcodes); command.Parameters.AddWithValue(names); command.Parameters.AddWithValue(externalReferences);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) { if (!reader.IsDBNull(0)) bySku[reader.GetString(0)] = (reader.GetGuid(1), reader.GetGuid(2)); if (!reader.IsDBNull(4)) byBarcode[reader.GetString(4)] = (reader.GetGuid(1), reader.GetGuid(2)); if (!reader.IsDBNull(5)) byExternal[reader.GetString(5)] = (reader.GetGuid(1), reader.GetGuid(2)); byName.Add(reader.GetString(3)); }
        }
        foreach (var row in rows)
        {
            if (row.Issues.Count > 0) { row.Classification = "Error"; continue; }
            var skuMatch = row.NormalizedSku is not null && bySku.TryGetValue(row.NormalizedSku, out var skuFound) ? skuFound : ((Guid Variant, Guid Template)?)null;
            var rowBarcodes = new[] { row.NormalizedBarcode }.Concat(row.AdditionalUoms.Select(uom => uom.NormalizedBarcode)).Where(value => value is not null).Select(value => value!).Distinct().ToArray();
            var barcodeMatches = rowBarcodes.Where(byBarcode.ContainsKey).Select(value => byBarcode[value]).Distinct().ToArray();
            var barcodeMatch = barcodeMatches.Length == 1 ? barcodeMatches[0] : ((Guid Variant, Guid Template)?)null;
            var externalMatch = !string.IsNullOrWhiteSpace(row.Source.ExternalReference) && byExternal.TryGetValue(row.Source.ExternalReference.Trim(), out var externalFound) ? externalFound : ((Guid Variant, Guid Template)?)null;
            var matches = new[] { externalMatch, skuMatch, barcodeMatch }.Where(match => match is not null).Select(match => match!.Value).Distinct().ToArray();
            if (matches.Length > 1 || barcodeMatches.Length > 1) { row.Classification = "Review"; row.Issues.Add("identifier_match_conflict"); }
            else if (matches.Length == 1) { row.MatchVariantId = matches[0].Variant; row.MatchTemplateId = matches[0].Template; row.Classification = "Update"; }
            else if (!string.IsNullOrWhiteSpace(row.Source.DisplayName) && byName.Contains(CatalogNormalization.Search(row.Source.DisplayName))) { row.Classification = "Review"; row.Issues.Add("potential_duplicate"); }
            else row.Classification = "Create";
        }
    }

    private static void ValidateGroups(PreviewRow[] rows)
    {
        foreach (var group in rows.Where(row => !string.IsNullOrWhiteSpace(row.Source.TemplateKey)).GroupBy(row => row.Source.TemplateKey!.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var distinctIdentities = group.Select(row => string.Join('|', CatalogNormalization.Search(row.Source.DisplayName ?? string.Empty), row.Source.Nature, row.Source.NameAr?.Trim(), row.Source.NameEn?.Trim(), row.CategoryId, row.BrandId)).Distinct(StringComparer.Ordinal).Count();
            if (distinctIdentities > 1) foreach (var row in group) { row.Classification = "Error"; row.Issues.Add("inconsistent_variant_group"); }
            var attributeSets = group.Select(row => string.Join('|', row.ResolvedAttributes.Keys.Order().Select(id => id.ToString("N")))).Distinct(StringComparer.Ordinal).Count();
            if (attributeSets > 1) foreach (var row in group) { row.Classification = "Error"; row.Issues.Add("inconsistent_variant_attributes"); }
            if (group.Any(row => row.ResolvedAttributes.Count > 0))
                foreach (var duplicate in group.GroupBy(row => AttributeSignature(row.ResolvedAttributes), StringComparer.Ordinal).Where(items => items.Count() > 1))
                    foreach (var row in duplicate) { row.Classification = "Review"; row.Issues.Add("duplicate_variant_combination_in_file"); }
        }
    }

    private static void ClassifyFileDuplicates(PreviewRow[] rows)
    {
        foreach (var group in rows.Where(row => row.NormalizedSku is not null).GroupBy(row => row.NormalizedSku!, StringComparer.Ordinal).Where(group => group.Count() > 1))
            foreach (var row in group) { row.Classification = "Review"; row.Issues.Add("duplicate_sku_in_file"); }
        foreach (var group in rows.SelectMany(row => new[] { row.NormalizedBarcode }.Concat(row.AdditionalUoms.Select(uom => uom.NormalizedBarcode)).Where(value => value is not null).Select(value => (Row: row, Barcode: value!))).GroupBy(item => item.Barcode, StringComparer.Ordinal).Where(group => group.Count() > 1))
            foreach (var item in group) { item.Row.Classification = "Review"; item.Row.Issues.Add("duplicate_barcode_in_file"); }
        foreach (var group in rows.Where(row => !string.IsNullOrWhiteSpace(row.Source.ExternalReference)).GroupBy(row => row.Source.ExternalReference!.Trim(), StringComparer.Ordinal).Where(group => group.Count() > 1))
            foreach (var row in group) { row.Classification = "Review"; row.Issues.Add("duplicate_external_reference_in_file"); }
    }

    private static void LinkGroupsToExistingTemplates(PreviewRow[] rows)
    {
        foreach (var group in rows.Where(row => !string.IsNullOrWhiteSpace(row.Source.TemplateKey)).GroupBy(row => row.Source.TemplateKey!.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var existing = group.Where(row => row.MatchTemplateId is not null).Select(row => row.MatchTemplateId!.Value).Distinct().ToArray();
            if (existing.Length == 1) foreach (var row in group.Where(row => row.Classification == "Create")) row.TargetTemplateId = existing[0];
            if (existing.Length > 1) foreach (var row in group) { row.Classification = "Review"; row.Issues.Add("variant_group_matches_multiple_products"); }
        }
    }

    private static PreviewRow Normalize(int rowNumber, CatalogImportRowRequest source)
    {
        source = source with
        {
            TemplateKey = NullTrim(source.TemplateKey),
            DisplayName = source.DisplayName?.Trim() ?? string.Empty,
            Sku = NullTrim(source.Sku),
            NameAr = NullTrim(source.NameAr),
            NameEn = NullTrim(source.NameEn),
            Category = NullTrim(source.Category),
            Brand = NullTrim(source.Brand),
            BaseUomCode = source.BaseUomCode?.Trim() ?? string.Empty,
            Barcode = NullTrim(source.Barcode),
            ExternalReference = NullTrim(source.ExternalReference)
        };
        var row = new PreviewRow(rowNumber, source);
        if (string.IsNullOrWhiteSpace(source.DisplayName) || string.IsNullOrWhiteSpace(source.BaseUomCode)) row.Issues.Add("required_value_missing");
        if (source.DisplayName?.Length > 300 || source.NameAr?.Length > 300 || source.NameEn?.Length > 300 || source.Sku?.Length > 120 || source.Category?.Length > 200 || source.Brand?.Length > 200 || source.BaseUomCode?.Length > 32 || source.Barcode?.Length > 128 || source.ExternalReference?.Length > 200 || source.TemplateKey?.Length > 100) row.Issues.Add("field_length_exceeded");
        if (!Enum.IsDefined(source.Nature)) row.Issues.Add("invalid_nature");
        if (source.Sku is not null) row.NormalizedSku = CatalogNormalization.Text(source.Sku);
        if (source.Barcode is not null)
        {
            try { row.NormalizedBarcode = IdentifierRules.RequireBarcode(source.Barcode); }
            catch (ArgumentException) { row.Issues.Add("invalid_barcode"); }
        }
        if (row.Issues.Count > 0) row.Classification = "Error";
        return row;
    }

    private static (int Create, int Update, int Review, int Error) Counts(IEnumerable<PreviewRow> rows) => (rows.Count(row => row.Classification == "Create"), rows.Count(row => row.Classification == "Update"), rows.Count(row => row.Classification == "Review"), rows.Count(row => row.Classification == "Error"));
    private static CatalogImportSummary ToSummary(Guid batchId, string status, PreviewRow[] rows, int create, int update, int review, int error) => new(batchId, status, rows.Length, create, update, review, error, rows.Take(200).Select(row => new CatalogImportRowResult(row.RowNumber, row.Classification, row.Issues)).ToArray());
    private static Guid? NullableGuid(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetGuid(index);
    private static string? NullTrim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NullableString(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static string CsvCell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var safe = value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? $"'{value}" : value;
        return '"' + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
    private static async Task SetTenantAsync(NpgsqlConnection connection, TenantId tenantId, CancellationToken cancellationToken) { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenantId.Value.ToString("D")); await command.ExecuteNonQueryAsync(cancellationToken); }

    private sealed class PreviewRow(int rowNumber, CatalogImportRowRequest source)
    {
        public int RowNumber { get; } = rowNumber; public CatalogImportRowRequest Source { get; } = source; public ImportUom[] AdditionalUoms { get; } = (source.AdditionalUoms ?? []).Select(uom => new ImportUom(uom.UomCode?.Trim() ?? string.Empty, uom.FactorToBase, uom.EffectiveFrom, uom.EffectiveTo, NullTrim(uom.Barcode), uom.UsageFlags, uom.RoundingPrecision)).ToArray(); public Dictionary<Guid, Guid> ResolvedAttributes { get; } = []; public string? NormalizedSku { get; set; }
        public string? NormalizedBarcode { get; set; }
        public Guid? MatchTemplateId { get; set; }
        public Guid? MatchVariantId { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? BrandId { get; set; }
        public Guid? BaseUomId { get; set; }
        public string? BaseUomDimension { get; set; }
        public Guid TargetTemplateId { get; set; }
        public Guid TargetVariantId { get; set; }
        public string Classification { get; set; } = "Create"; public List<string> Issues { get; } = [];
    }
    private sealed class ImportUom(string uomCode, decimal factorToBase, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, string? barcode, IReadOnlyList<string>? usageFlags, int? roundingPrecision) { public string UomCode { get; } = uomCode; public decimal FactorToBase { get; } = factorToBase; public DateTimeOffset EffectiveFrom { get; } = effectiveFrom; public DateTimeOffset? EffectiveTo { get; } = effectiveTo; public string? Barcode { get; } = barcode; public IReadOnlyList<string>? UsageFlags { get; } = usageFlags; public int? RoundingPrecision { get; } = roundingPrecision; public string? NormalizedBarcode { get; set; } public Guid UomId { get; set; } }
    private sealed record StagedRow(Guid TenantId, Guid BatchId, int RowNumber, string RowData, string? NormalizedSku, string? NormalizedBarcode, Guid? MatchTemplateId, Guid? MatchVariantId, Guid? CategoryId, Guid? BrandId, Guid? BaseUomId, Guid TargetTemplateId, Guid TargetVariantId, string Classification, string[] Errors);
    private static string AttributeSignature(IReadOnlyDictionary<Guid, Guid> attributes) => string.Join(';', attributes.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key:N}={pair.Value:N}"));
    private static string AttributeSignature(IEnumerable<StoredImportAttribute> attributes) => string.Join(';', attributes.OrderBy(pair => pair.AttributeId).Select(pair => $"{pair.AttributeId:N}={pair.AttributeValueId:N}"));
    private sealed record StoredImportData(CatalogImportRowRequest Source, StoredImportUom[] AdditionalUoms, StoredImportAttribute[] Attributes);
    private sealed record StoredImportUom(Guid UomId, decimal FactorToBase, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, string? Barcode, string? NormalizedBarcode, string[]? UsageFlags = null, int? RoundingPrecision = null);
    private sealed record StoredImportAttribute(Guid AttributeId, Guid AttributeValueId);
    private sealed record StoredRow(int RowNumber, CatalogImportRowRequest Source, StoredImportUom[] AdditionalUoms, StoredImportAttribute[] Attributes, string? NormalizedSku, string? NormalizedBarcode, Guid? MatchTemplateId, Guid? MatchVariantId, Guid? CategoryId, Guid? BrandId, Guid? BaseUomId, Guid TargetTemplateId, Guid TargetVariantId, string Classification, string[] Errors);
}
