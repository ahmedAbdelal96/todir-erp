using System.Globalization;
using System.Text.Json;
using Npgsql;
using Modules.Catalog.DTOs;
using Modules.Catalog.Entities;
using Core.Messaging;
using Core.Services;
using Core.Transactions;
using Shared.Primitives.Identifiers;

namespace Modules.Catalog.Repositories;

public sealed class ProductRepository(ITransactionCoordinator coordinator, NpgsqlDataSource dataSource)
{
    public async Task<ProductDetails> CreateAsync(TenantId tenantId, Guid actorId, Guid membershipId, CreateProductRequest request, Guid correlationId, string? traceParent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)) throw new CatalogException("catalog.name_required", "Product name is required.");
        var templateId = Guid.CreateVersion7(); var variantId = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var sku = Null(request.Sku); var normalizedSku = sku is null ? null : CatalogNormalization.Text(sku);
        await coordinator.ExecuteAsync(tenantId, async (context, token) =>
        {
            await EnsureUomAsync(context, tenantId, request.BaseUomId, token);
            await ExecuteAsync(context, "insert into foundation_catalog.product_template (tenant_id,id,display_name,display_name_normalized,name_ar,name_en,product_nature,category_id,brand_id,status,created_at,updated_at,version) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,'Active',$10,$10,1)", token, tenantId.Value, templateId, request.DisplayName.Trim(), CatalogNormalization.Search(request.DisplayName), Null(request.NameAr) ?? (object)DBNull.Value, Null(request.NameEn) ?? (object)DBNull.Value, request.Nature.ToString(), (object?)request.CategoryId ?? DBNull.Value, (object?)request.BrandId ?? DBNull.Value, now);
            await InsertVariantAsync(context, tenantId, variantId, templateId, sku, normalizedSku, now, token);
            await ExecuteAsync(context, "insert into foundation_catalog.product_uom (tenant_id,id,variant_id,uom_id,factor_to_base,is_base,effective_from,status,version) values ($1,$2,$3,$4,1,true,$5,'Active',1)", token, tenantId.Value, Guid.CreateVersion7(), variantId, request.BaseUomId, now);
            await ExecuteAsync(context, "insert into foundation_catalog.product_visibility (tenant_id,template_id,scope_type) values ($1,$2,'TenantWide')", token, tenantId.Value, templateId);
            await RefreshProjectionAsync(context, tenantId, templateId, variantId, token);
            await AuditAsync(context, tenantId, actorId, membershipId, templateId, "product_created", correlationId, now, new { request.DisplayName, request.Nature });
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "CatalogProductCreatedV1", JsonSerializer.Serialize(new { templateId, variantId }), tenantId, now, correlationId, templateId, "catalog", traceParent));
            return true;
        }, ct);
        return await GetAsync(tenantId, templateId, null, ct) ?? throw new CatalogException("catalog.product_not_found", "Created product could not be read.");
    }

    public async Task<ProductDetails?> GetAsync(TenantId tenantId, Guid templateId, Guid? legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenantId, ct);
        const string sql = "select p.id,p.display_name,p.name_ar,p.name_en,p.product_nature,p.category_id,c.name,p.brand_id,b.name,p.status,p.created_at,p.updated_at,p.version,v.id,v.sku,u.code,p.product_template_code from foundation_catalog.product_template p left join foundation_catalog.product_category c on c.tenant_id=p.tenant_id and c.id=p.category_id left join foundation_catalog.brand b on b.tenant_id=p.tenant_id and b.id=p.brand_id join foundation_catalog.product_variant v on v.tenant_id=p.tenant_id and v.product_template_id=p.id and v.is_default_variant left join foundation_catalog.product_uom pu on pu.tenant_id=v.tenant_id and pu.variant_id=v.id and pu.is_base and pu.status='Active' and pu.effective_to is null left join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id where p.tenant_id=$1 and p.id=$2 and exists (select 1 from foundation_catalog.product_visibility visibility where visibility.tenant_id=p.tenant_id and visibility.template_id=p.id and (visibility.scope_type='TenantWide' or (visibility.scope_type='LegalEntity' and $3::uuid is not null and visibility.legal_entity_id=$3)))";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(templateId); command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null;
        var details = new ProductDetails(reader.GetGuid(0), reader.GetString(16), reader.GetGuid(13), reader.GetString(1), GetString(reader, 2), GetString(reader, 3), Enum.Parse<ProductNature>(reader.GetString(4)), GetString(reader, 14), GetString(reader, 15), GetGuid(reader, 5), GetString(reader, 6), GetGuid(reader, 7), GetString(reader, 8), Enum.Parse<CatalogStatus>(reader.GetString(9)), [], reader.GetFieldValue<DateTimeOffset>(10), reader.GetFieldValue<DateTimeOffset>(11), reader.GetInt64(12));
        await reader.DisposeAsync();
        var barcodes = new List<BarcodeDto>(); await using var barcode = new NpgsqlCommand("select id,variant_id,uom_id,barcode_type,value,is_primary from foundation_catalog.product_barcode where tenant_id=$1 and variant_id=$2 and status='Active' order by is_primary desc,value", connection); barcode.Parameters.AddWithValue(tenantId.Value); barcode.Parameters.AddWithValue(details.DefaultVariantId); await using var barcodeReader = await barcode.ExecuteReaderAsync(ct); while (await barcodeReader.ReadAsync(ct)) barcodes.Add(new BarcodeDto(barcodeReader.GetGuid(0), barcodeReader.GetGuid(1), GetGuid(barcodeReader, 2), barcodeReader.GetString(3), barcodeReader.GetString(4), barcodeReader.GetBoolean(5)));
        return details with { Barcodes = barcodes };
    }

    public async Task<PagedProducts> ListAsync(TenantId tenantId, string? search, ProductNature? nature, CatalogStatus? status, Guid? categoryId, Guid? brandId, Guid? legalEntityId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100); var normalized = string.IsNullOrWhiteSpace(search) ? null : CatalogNormalization.Search(search);
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenantId, ct);
        const string visibility = "exists(select 1 from foundation_catalog.product_visibility visibility where visibility.tenant_id=c.tenant_id and visibility.template_id=c.template_id and (visibility.scope_type='TenantWide' or (visibility.scope_type='LegalEntity' and $6::uuid is not null and visibility.legal_entity_id=$6)))";
        const string filters = "($2::text is null or c.product_nature=$2) and ($3::text is null or c.status=$3) and ($4::uuid is null or c.category_id=$4) and ($5::uuid is null or p.brand_id=$5)";
        const string selected = "select c.variant_id,c.template_id,c.display_name,c.name_ar,c.name_en,c.product_nature,c.sku,c.base_uom_code,c.category_name,b.name,c.status,c.updated_at,p.version from foundation_catalog.catalog_search_projection c join foundation_catalog.product_template p on p.tenant_id=c.tenant_id and p.id=c.template_id left join foundation_catalog.brand b on b.tenant_id=p.tenant_id and b.id=p.brand_id";
        string countSql; string pageSql;
        if (normalized is null)
        {
            countSql = $"select count(*) from foundation_catalog.catalog_search_projection c join foundation_catalog.product_template p on p.tenant_id=c.tenant_id and p.id=c.template_id where c.tenant_id=$1 and {filters} and {visibility}";
            pageSql = $"{selected} where c.tenant_id=$1 and {filters} and {visibility} order by c.updated_at desc,c.variant_id limit $7 offset $8";
        }
        else
        {
            const string indexedTextMatches = "select c.tenant_id,c.variant_id,c.template_id,2 as priority from foundation_catalog.catalog_search_projection c where c.tenant_id=$1 and c.search_text_normalized like $7 union all select c.tenant_id,c.variant_id,c.template_id,0 from foundation_catalog.catalog_search_projection c where c.tenant_id=$1 and c.normalized_sku=$9";
            var matches = $"""
                with matched_aliases as materialized (
                    select tenant_id,variant_id from foundation_catalog.product_alias where tenant_id=$1 and normalized_alias like $7
                ), matched_translations as materialized (
                    select tenant_id,template_id from foundation_catalog.product_translation where tenant_id=$1 and lower(name) like $7
                ), matched_brands as materialized (
                    select tenant_id,id from foundation_catalog.brand where tenant_id=$1 and lower(name) like $7
                ), candidates as materialized (
                    {indexedTextMatches}
                    union all
                    select c.tenant_id,c.variant_id,c.template_id,1 from foundation_catalog.product_barcode barcode join foundation_catalog.catalog_search_projection c on c.tenant_id=barcode.tenant_id and c.variant_id=barcode.variant_id where barcode.tenant_id=$1 and barcode.status='Active' and barcode.normalized_value=$8
                    union all
                    select c.tenant_id,c.variant_id,c.template_id,2 from matched_aliases alias join foundation_catalog.catalog_search_projection c on c.tenant_id=alias.tenant_id and c.variant_id=alias.variant_id
                    union all
                    select c.tenant_id,c.variant_id,c.template_id,2 from matched_translations translation join foundation_catalog.catalog_search_projection c on c.tenant_id=translation.tenant_id and c.template_id=translation.template_id
                    union all
                    select c.tenant_id,c.variant_id,c.template_id,2
                    from matched_brands brand
                    cross join lateral (
                        select translated.tenant_id,translated.id
                        from foundation_catalog.product_template translated
                        where translated.tenant_id=brand.tenant_id and translated.brand_id=brand.id
                        offset 0
                    ) translated
                    cross join lateral (
                        select c.tenant_id,c.variant_id,c.template_id
                        from foundation_catalog.catalog_search_projection c
                        where c.tenant_id=translated.tenant_id and c.template_id=translated.id
                        offset 0
                    ) c
                ), matches as materialized (
                    select distinct on (tenant_id,variant_id,template_id) tenant_id,variant_id,template_id,priority from candidates order by tenant_id,variant_id,template_id,priority
                ), filtered as materialized (
                    select c.variant_id,c.template_id,c.display_name,c.name_ar,c.name_en,c.product_nature,c.sku,c.base_uom_code,c.category_name,b.name as brand_name,c.status,c.updated_at,p.version,m.priority,c.display_name_normalized,c.normalized_sku
                    from matches m join foundation_catalog.catalog_search_projection c on c.tenant_id=m.tenant_id and c.variant_id=m.variant_id and c.template_id=m.template_id
                    join foundation_catalog.product_template p on p.tenant_id=c.tenant_id and p.id=c.template_id
                    left join foundation_catalog.brand b on b.tenant_id=p.tenant_id and b.id=p.brand_id
                    where c.tenant_id=$1 and {filters} and {visibility}
                ), page as materialized (
                    select * from filtered order by priority,updated_at desc,variant_id limit $10 offset $11
                )
                """;
            countSql = $"{matches} select count(*) from filtered";
            pageSql = $"{matches} select p.variant_id,p.template_id,p.display_name,p.name_ar,p.name_en,p.product_nature,p.sku,p.base_uom_code,p.category_name,p.brand_name,p.status,p.updated_at,p.version,(select count(*) from filtered),p.variant_id is not null from (values (1)) seed(n) left join lateral (select * from page) p on true order by p.priority,p.updated_at desc,p.variant_id";
        }
        var total = 0;
        if (normalized is null)
        {
            await using var count = new NpgsqlCommand(countSql, connection);
            Add(count, tenantId.Value, nature?.ToString() ?? (object)DBNull.Value, status?.ToString() ?? (object)DBNull.Value, categoryId ?? (object)DBNull.Value, brandId ?? (object)DBNull.Value, legalEntityId ?? (object)DBNull.Value);
            total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        }
        await using var command = new NpgsqlCommand(pageSql, connection);
        Add(command, tenantId.Value, nature?.ToString() ?? (object)DBNull.Value, status?.ToString() ?? (object)DBNull.Value, categoryId ?? (object)DBNull.Value, brandId ?? (object)DBNull.Value, legalEntityId ?? (object)DBNull.Value);
        if (normalized is not null) { command.Parameters.AddWithValue(string.Concat("%", normalized, "%")); command.Parameters.AddWithValue(CatalogNormalization.Barcode(search!)); command.Parameters.AddWithValue(CatalogNormalization.Text(search!)); }
        command.Parameters.AddWithValue(pageSize); command.Parameters.AddWithValue((page - 1) * pageSize);
        var items = new List<ProductSummary>(); await using var reader = await command.ExecuteReaderAsync(ct);
        if (normalized is not null && await reader.ReadAsync(ct))
        {
            total = checked((int)reader.GetInt64(13));
            if (!reader.IsDBNull(0))
            {
                items.Add(new ProductSummary(reader.GetGuid(1), reader.GetGuid(0), reader.GetString(2), GetString(reader, 3), GetString(reader, 4), Enum.Parse<ProductNature>(reader.GetString(5)), GetString(reader, 6), GetString(reader, 7), GetString(reader, 8), GetString(reader, 9), Enum.Parse<CatalogStatus>(reader.GetString(10)), reader.GetFieldValue<DateTimeOffset>(11), reader.IsDBNull(12) ? 1 : reader.GetInt64(12)));
                while (await reader.ReadAsync(ct)) items.Add(new ProductSummary(reader.GetGuid(1), reader.GetGuid(0), reader.GetString(2), GetString(reader, 3), GetString(reader, 4), Enum.Parse<ProductNature>(reader.GetString(5)), GetString(reader, 6), GetString(reader, 7), GetString(reader, 8), GetString(reader, 9), Enum.Parse<CatalogStatus>(reader.GetString(10)), reader.GetFieldValue<DateTimeOffset>(11), reader.IsDBNull(12) ? 1 : reader.GetInt64(12)));
            }
        }
        else if (normalized is null)
        {
            while (await reader.ReadAsync(ct)) items.Add(new ProductSummary(reader.GetGuid(1), reader.GetGuid(0), reader.GetString(2), GetString(reader, 3), GetString(reader, 4), Enum.Parse<ProductNature>(reader.GetString(5)), GetString(reader, 6), GetString(reader, 7), GetString(reader, 8), GetString(reader, 9), Enum.Parse<CatalogStatus>(reader.GetString(10)), reader.GetFieldValue<DateTimeOffset>(11), reader.IsDBNull(12) ? 1 : reader.GetInt64(12)));
        }
        return new PagedProducts(items, total, page, pageSize);
    }

    public async Task<ProductDetails> UpdateAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid id, Guid? legalEntityId, UpdateProductRequest request, Guid correlationId, string? traceParent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)) throw new CatalogException("catalog.name_required", "Product name is required."); var normalizedSku = string.IsNullOrWhiteSpace(request.Sku) ? null : CatalogNormalization.Text(request.Sku);
        await coordinator.ExecuteAsync(tenantId, async (context, token) => { await EnsureVisibleAsync(context, tenantId, id, legalEntityId, token); var changed = await ExecuteAsync(context, "update foundation_catalog.product_template set display_name=$3,display_name_normalized=$4,name_ar=$5,name_en=$6,category_id=$7,brand_id=$8,updated_at=$9,version=version+1 where tenant_id=$1 and id=$2 and version=$10 and status='Active'", token, tenantId.Value, id, request.DisplayName.Trim(), CatalogNormalization.Search(request.DisplayName), Null(request.NameAr) ?? (object)DBNull.Value, Null(request.NameEn) ?? (object)DBNull.Value, request.CategoryId ?? (object)DBNull.Value, request.BrandId ?? (object)DBNull.Value, DateTimeOffset.UtcNow, request.Version); if (changed == 0) throw new CatalogException("catalog.concurrency_conflict", "The product was changed by another request or is archived."); var updated = await UpdateDefaultVariantSkuAsync(context, tenantId, id, Null(request.Sku), normalizedSku, token); if (updated == 0) throw new CatalogException("catalog.variant_not_found", "Default variant was not found."); var variantId = await ScalarAsync<Guid>(context, "select id from foundation_catalog.product_variant where tenant_id=$1 and product_template_id=$2 and is_default_variant", token, tenantId.Value, id); await RefreshProjectionAsync(context, tenantId, id, variantId, token); await AuditAsync(context, tenantId, actorId, membershipId, id, "product_identity_changed", correlationId, DateTimeOffset.UtcNow, new { request.DisplayName }); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "CatalogProductChangedV1", JsonSerializer.Serialize(new { templateId = id, variantId }), tenantId, DateTimeOffset.UtcNow, correlationId, id, "catalog", traceParent)); return true; }, ct);
        return await GetAsync(tenantId, id, legalEntityId, ct) ?? throw new CatalogException("catalog.product_not_found", "Product was not found.");
    }

    public async Task<ProductDetails> ChangeStatusAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid id, Guid? legalEntityId, bool restore, long version, Guid correlationId, string? traceParent, CancellationToken ct)
    {
        var status = restore ? CatalogStatus.Active : CatalogStatus.Archived; await coordinator.ExecuteAsync(tenantId, async (context, token) => { await EnsureVisibleAsync(context, tenantId, id, legalEntityId, token); var changed = await ExecuteAsync(context, "update foundation_catalog.product_template set status=$3,updated_at=$4,version=version+1 where tenant_id=$1 and id=$2 and version=$5", token, tenantId.Value, id, status.ToString(), DateTimeOffset.UtcNow, version); if (changed == 0) throw new CatalogException("catalog.concurrency_conflict", "The product state changed or it was not found."); await ExecuteAsync(context, "update foundation_catalog.product_variant set status=$3,updated_at=$4,version=version+1 where tenant_id=$1 and product_template_id=$2", token, tenantId.Value, id, status.ToString(), DateTimeOffset.UtcNow); var variantId = await ScalarAsync<Guid>(context, "select id from foundation_catalog.product_variant where tenant_id=$1 and product_template_id=$2 and is_default_variant", token, tenantId.Value, id); await RefreshProjectionAsync(context, tenantId, id, variantId, token); await AuditAsync(context, tenantId, actorId, membershipId, id, restore ? "product_restored" : "product_archived", correlationId, DateTimeOffset.UtcNow, new { status }); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), restore ? "CatalogProductRestoredV1" : "CatalogProductArchivedV1", JsonSerializer.Serialize(new { templateId = id, variantId }), tenantId, DateTimeOffset.UtcNow, correlationId, id, "catalog", traceParent)); return true; }, ct); return await GetAsync(tenantId, id, legalEntityId, ct) ?? throw new CatalogException("catalog.product_not_found", "Product was not found.");
    }

    public async Task<IReadOnlyList<ProductVisibilityDto>> SetVisibilityAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid productId, ProductVisibilityRequest request, Guid correlationId, string? traceParent, CancellationToken ct)
    {
        if (request.ScopeType is not ("TenantWide" or "LegalEntity") || (request.ScopeType == "TenantWide" && request.LegalEntityId is not null) || (request.ScopeType == "LegalEntity" && request.LegalEntityId is null))
            throw new CatalogException("catalog.visibility_scope_invalid", "Choose tenant-wide visibility or one legal entity.");

        return await coordinator.ExecuteAsync(tenantId, async (context, token) =>
        {
            if (await ScalarNullableGuidAsync(context, "select id from foundation_catalog.product_template where tenant_id=$1 and id=$2", token, tenantId.Value, productId) is null)
                throw new CatalogException("catalog.product_not_found", "Product was not found.");
            var before = new List<ProductVisibilityDto>();
            await using (var current = new NpgsqlCommand("select scope_type,legal_entity_id from foundation_catalog.product_visibility where tenant_id=$1 and template_id=$2 order by scope_type,legal_entity_id", context.Connection, context.Transaction))
            {
                current.Parameters.AddWithValue(tenantId.Value);
                current.Parameters.AddWithValue(productId);
                await using var reader = await current.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) before.Add(new ProductVisibilityDto(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1)));
            }
            await ExecuteAsync(context, "delete from foundation_catalog.product_visibility where tenant_id=$1 and template_id=$2", token, tenantId.Value, productId);
            await ExecuteAsync(context, "insert into foundation_catalog.product_visibility (tenant_id,template_id,scope_type,legal_entity_id) values ($1,$2,$3,$4)", token, tenantId.Value, productId, request.ScopeType, request.LegalEntityId ?? (object)DBNull.Value);
            var now = DateTimeOffset.UtcNow;
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenantId, null, null, "User", actorId, membershipId, "catalog", "product_visibility_changed", "product", productId, JsonSerializer.Serialize(before), JsonSerializer.Serialize(new[] { new ProductVisibilityDto(request.ScopeType, request.LegalEntityId) }), null, "api", correlationId, System.Diagnostics.Activity.Current?.Id, now, null), token);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "CatalogProductVisibilityChangedV1", JsonSerializer.Serialize(new { productId, request.ScopeType, request.LegalEntityId }), tenantId, now, correlationId, productId, "catalog", traceParent));
            return (IReadOnlyList<ProductVisibilityDto>)[new ProductVisibilityDto(request.ScopeType, request.LegalEntityId)];
        }, ct);
    }

    public async Task<BarcodeDto> AddBarcodeAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid templateId, Guid? requestedVariantId, Guid? legalEntityId, AddBarcodeRequest request, Guid correlationId, CancellationToken ct)
    {
        var value = IdentifierRules.RequireBarcode(request.Value); return await coordinator.ExecuteAsync(tenantId, async (context, token) => { await EnsureVisibleAsync(context, tenantId, templateId, legalEntityId, token); var variantId = requestedVariantId ?? await ScalarAsync<Guid>(context, "select id from foundation_catalog.product_variant where tenant_id=$1 and product_template_id=$2 and is_default_variant and status='Active'", token, tenantId.Value, templateId); var validVariant = await ScalarNullableGuidAsync(context, "select id from foundation_catalog.product_variant where tenant_id=$1 and id=$2 and product_template_id=$3 and status='Active'", token, tenantId.Value, variantId, templateId); if (validVariant is null) throw new CatalogException("catalog.variant_not_found", "Variant was not found for this product."); if (request.UomId is not null && await ScalarNullableGuidAsync(context, "select id from foundation_catalog.product_uom where tenant_id=$1 and variant_id=$2 and uom_id=$3 and status='Active'", token, tenantId.Value, variantId, request.UomId.Value) is null) throw new CatalogException("catalog.uom_not_found", "The barcode unit is not assigned to this variant."); var id = Guid.CreateVersion7(); try { await ExecuteAsync(context, "insert into foundation_catalog.product_barcode (tenant_id,id,variant_id,uom_id,barcode_type,value,normalized_value,is_primary,status,created_at) values ($1,$2,$3,$4,$5,$6,$7,$8,'Active',$9)", token, tenantId.Value, id, variantId, request.UomId ?? (object)DBNull.Value, request.BarcodeType.ToString(), request.Value.Trim(), value, request.IsPrimary, DateTimeOffset.UtcNow); } catch (PostgresException ex) when (ex.SqlState == "23505") { throw new CatalogException("catalog.barcode_duplicate", "Barcode already belongs to another product."); } await AuditAsync(context, tenantId, actorId, membershipId, templateId, "product_barcode_added", correlationId, DateTimeOffset.UtcNow, new { request.Value, variantId }); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "CatalogProductBarcodeChangedV1", JsonSerializer.Serialize(new { templateId, variantId, barcodeId = id }), tenantId, DateTimeOffset.UtcNow, correlationId, templateId, "catalog")); return new BarcodeDto(id, variantId, request.UomId, request.BarcodeType.ToString(), request.Value.Trim(), request.IsPrimary); }, ct);
    }

    private static async Task EnsureVisibleAsync(ITransactionContext context, TenantId tenantId, Guid productId, Guid? legalEntityId, CancellationToken ct)
    {
        var visible = await ScalarAsync<bool>(context, "select exists(select 1 from foundation_catalog.product_visibility where tenant_id=$1 and template_id=$2 and (scope_type='TenantWide' or (scope_type='LegalEntity' and $3::uuid is not null and legal_entity_id=$3)))", ct, tenantId.Value, productId, legalEntityId ?? (object)DBNull.Value);
        if (!visible) throw new CatalogException("catalog.product_not_found", "Product was not found.");
    }

    private static async Task EnsureUomAsync(ITransactionContext context, TenantId tenantId, Guid uomId, CancellationToken ct) { if (await ScalarNullableGuidAsync(context, "select id from foundation_catalog.uom where tenant_id=$1 and id=$2 and status='Active'", ct, tenantId.Value, uomId) is null) throw new CatalogException("catalog.uom_not_found", "Base unit was not found."); }
    private static async Task InsertVariantAsync(ITransactionContext context, TenantId tenantId, Guid variantId, Guid templateId, string? sku, string? normalizedSku, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            await ExecuteAsync(context, "insert into foundation_catalog.product_variant (tenant_id,id,product_template_id,sku,normalized_sku,is_default_variant,status,created_at,updated_at,version) values ($1,$2,$3,$4,$5,true,'Active',$6,$6,1)", ct, tenantId.Value, variantId, templateId, sku ?? (object)DBNull.Value, normalizedSku ?? (object)DBNull.Value, now);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505" && exception.ConstraintName is "product_variant_tenant_id_normalized_sku_key" or "ux_catalog_variant_sku")
        {
            throw new CatalogException("catalog.sku_duplicate", "This SKU is already assigned to another variant.");
        }
    }
    private static async Task<int> UpdateDefaultVariantSkuAsync(ITransactionContext context, TenantId tenantId, Guid templateId, string? sku, string? normalizedSku, CancellationToken ct)
    {
        try
        {
            return await ExecuteAsync(context, "update foundation_catalog.product_variant set sku=$3,normalized_sku=$4,updated_at=$5,version=version+1 where tenant_id=$1 and product_template_id=$2 and is_default_variant", ct, tenantId.Value, templateId, sku ?? (object)DBNull.Value, normalizedSku ?? (object)DBNull.Value, DateTimeOffset.UtcNow);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505" && exception.ConstraintName is "product_variant_tenant_id_normalized_sku_key" or "ux_catalog_variant_sku")
        {
            throw new CatalogException("catalog.sku_duplicate", "This SKU is already assigned to another variant.");
        }
    }
    private static async Task RefreshProjectionAsync(ITransactionContext context, TenantId tenantId, Guid templateId, Guid variantId, CancellationToken ct) { await ExecuteAsync(context, "delete from foundation_catalog.catalog_search_projection where tenant_id=$1 and variant_id=$2", ct, tenantId.Value, variantId); await ExecuteAsync(context, "insert into foundation_catalog.catalog_search_projection (tenant_id,variant_id,template_id,display_name,display_name_normalized,name_ar,name_en,sku,normalized_sku,category_id,category_name,base_uom_code,product_nature,status,updated_at) select p.tenant_id,v.id,p.id,p.display_name,p.display_name_normalized,p.name_ar,p.name_en,v.sku,v.normalized_sku,p.category_id,c.name,u.code,p.product_nature,p.status,p.updated_at from foundation_catalog.product_template p join foundation_catalog.product_variant v on v.tenant_id=p.tenant_id and v.product_template_id=p.id left join foundation_catalog.product_category c on c.tenant_id=p.tenant_id and c.id=p.category_id left join foundation_catalog.product_uom pu on pu.tenant_id=v.tenant_id and pu.variant_id=v.id and pu.is_base and pu.status='Active' and pu.effective_to is null left join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id where p.tenant_id=$1 and p.id=$2 and v.id=$3", ct, tenantId.Value, templateId, variantId); }
    private static async Task AuditAsync(ITransactionContext context, TenantId tenantId, Guid actorId, Guid membershipId, Guid entityId, string action, Guid correlationId, DateTimeOffset now, object after) => await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenantId, null, null, "User", actorId, membershipId, "catalog", action, "product", entityId, null, JsonSerializer.Serialize(after), null, "api", correlationId, System.Diagnostics.Activity.Current?.Id, now, null));
    private static async Task SetTenantAsync(NpgsqlConnection connection, TenantId tenantId, CancellationToken ct) { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenantId.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); }
    private static async Task<int> ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); return await command.ExecuteNonQueryAsync(ct); }
    private static async Task<T> ScalarAsync<T>(ITransactionContext context, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); var value = await command.ExecuteScalarAsync(ct); if (value is null or DBNull) throw new KeyNotFoundException("catalog.not_found"); if (value is T result) return result; if (typeof(T) == typeof(Guid)) return (T)(object)Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!); return (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), CultureInfo.InvariantCulture); }
    private static async Task<Guid?> ScalarNullableGuidAsync(ITransactionContext context, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); var value = await command.ExecuteScalarAsync(ct); return value is null or DBNull ? null : (Guid)value; }
    private static void Add(NpgsqlCommand command, params object[] values) { for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i] ?? DBNull.Value); }
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? GetString(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static Guid? GetGuid(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetGuid(index);
}

