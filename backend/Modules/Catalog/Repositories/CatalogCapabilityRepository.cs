using Npgsql;
using Shared.Contracts.Catalog;
using Shared.Primitives.Identifiers;
using Modules.Catalog.Entities;

namespace Modules.Catalog.Repositories;

/// <summary>Implements Catalog's repository-independent contract with tenant/entity scoped reads.</summary>
public sealed class CatalogCapabilityRepository(NpgsqlDataSource dataSource)
{
    public async Task<CatalogProductSummary?> GetProductSummaryAsync(TenantId tenantId, Guid productTemplateId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenTenantConnectionAsync(tenantId, cancellationToken);
        const string sql = "select p.id,p.display_name,p.name_ar,p.name_en,p.product_nature,p.status from foundation_catalog.product_template p where p.tenant_id=$1 and p.id=$2 and exists(select 1 from foundation_catalog.product_visibility x where x.tenant_id=p.tenant_id and x.template_id=p.id and (x.scope_type='TenantWide' or (x.scope_type='LegalEntity' and $3::uuid is not null and x.legal_entity_id=$3)))";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(productTemplateId); command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new CatalogProductSummary(reader.GetGuid(0), reader.GetString(1), Text(reader, 2), Text(reader, 3), reader.GetString(4), reader.GetString(5)) : null;
    }

    public async Task<CatalogVariantIdentity?> FindVariantAsync(TenantId tenantId, Guid productVariantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenTenantConnectionAsync(tenantId, cancellationToken);
        const string sql = "select v.id,p.id,p.display_name,v.sku,p.product_nature,v.status from foundation_catalog.product_variant v join foundation_catalog.product_template p on p.tenant_id=v.tenant_id and p.id=v.product_template_id where v.tenant_id=$1 and v.id=$2 and exists(select 1 from foundation_catalog.product_visibility x where x.tenant_id=p.tenant_id and x.template_id=p.id and (x.scope_type='TenantWide' or (x.scope_type='LegalEntity' and $3::uuid is not null and x.legal_entity_id=$3)))";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(productVariantId); command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new CatalogVariantIdentity(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), Text(reader, 3), reader.GetString(4), reader.GetString(5)) : null;
    }

    public async Task<CatalogVariantSnapshot?> ResolveVariantAsync(TenantId tenantId, string skuOrBarcode, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(skuOrBarcode)) return null;
        var normalized = CatalogNormalization.Text(skuOrBarcode);
        var barcode = CatalogNormalization.Barcode(skuOrBarcode);
        await using var connection = await OpenTenantConnectionAsync(tenantId, cancellationToken);
        const string sql = "with candidates as (select id as variant_id,0 as priority,null::uuid as uom_id from foundation_catalog.product_variant where tenant_id=$1 and normalized_sku=$2 and status='Active' union all select variant_id,1 as priority,uom_id from foundation_catalog.product_barcode where tenant_id=$1 and normalized_value=$3 and status='Active'), chosen as (select variant_id,uom_id from candidates order by priority limit 1) select v.id,p.id,p.display_name,v.sku,base.uom_id,base_uom.code,p.product_nature,entered.uom_id,entered_uom.code,entered.factor_to_base from chosen candidate join foundation_catalog.product_variant v on v.tenant_id=$1 and v.id=candidate.variant_id join foundation_catalog.product_template p on p.tenant_id=v.tenant_id and p.id=v.product_template_id join foundation_catalog.product_uom base on base.tenant_id=v.tenant_id and base.variant_id=v.id and base.is_base and base.status='Active' and base.effective_from<=$4 and (base.effective_to is null or base.effective_to>$4) join foundation_catalog.uom base_uom on base_uom.tenant_id=base.tenant_id and base_uom.id=base.uom_id left join foundation_catalog.product_uom entered on entered.tenant_id=v.tenant_id and entered.variant_id=v.id and entered.uom_id=candidate.uom_id and entered.status='Active' and entered.effective_from<=$4 and (entered.effective_to is null or entered.effective_to>$4) left join foundation_catalog.uom entered_uom on entered_uom.tenant_id=entered.tenant_id and entered_uom.id=entered.uom_id where v.status='Active' and p.status='Active' and exists(select 1 from foundation_catalog.product_visibility x where x.tenant_id=p.tenant_id and x.template_id=p.id and (x.scope_type='TenantWide' or (x.scope_type='LegalEntity' and $5::uuid is not null and x.legal_entity_id=$5))) limit 1";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(normalized); command.Parameters.AddWithValue(barcode); command.Parameters.AddWithValue(effectiveAt); command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new CatalogVariantSnapshot(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), Text(reader, 3), reader.GetGuid(4), reader.GetString(5), reader.GetString(6), NullableGuid(reader, 7), Text(reader, 8), reader.IsDBNull(9) ? null : reader.GetDecimal(9)) : null;
    }

    public async Task<IReadOnlyList<CatalogAllowedUom>> GetAllowedUomsAsync(TenantId tenantId, Guid productVariantId, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenTenantConnectionAsync(tenantId, cancellationToken);
        const string sql = "select pu.uom_id,u.code,pu.factor_to_base,pu.is_base,u.allow_fraction,u.precision,pu.effective_from,pu.effective_to,pu.usage_flags,pu.rounding_precision from foundation_catalog.product_uom pu join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id join foundation_catalog.product_variant v on v.tenant_id=pu.tenant_id and v.id=pu.variant_id where pu.tenant_id=$1 and pu.variant_id=$2 and pu.status='Active' and u.status='Active' and pu.effective_from<=$3 and (pu.effective_to is null or pu.effective_to>$3) and exists(select 1 from foundation_catalog.product_visibility x where x.tenant_id=v.tenant_id and x.template_id=v.product_template_id and (x.scope_type='TenantWide' or (x.scope_type='LegalEntity' and $4::uuid is not null and x.legal_entity_id=$4))) order by pu.is_base desc,u.code";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(productVariantId); command.Parameters.AddWithValue(effectiveAt); command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
        var units = new List<CatalogAllowedUom>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) units.Add(new CatalogAllowedUom(reader.GetGuid(0), reader.GetString(1), reader.GetDecimal(2), reader.GetBoolean(3), reader.GetBoolean(4), reader.GetInt16(5), reader.GetFieldValue<DateTimeOffset>(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7), reader.GetFieldValue<string[]>(8), reader.IsDBNull(9) ? null : reader.GetInt16(9)));
        return units;
    }

    public async Task<CatalogSearchPage> SearchAsync(TenantId tenantId, string? query, Guid? categoryId, Guid? legalEntityId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100);
        var normalized = string.IsNullOrWhiteSpace(query) ? null : CatalogNormalization.Search(query);
        await using var connection = await OpenTenantConnectionAsync(tenantId, cancellationToken);
        const string browseVisibility = "exists(select 1 from foundation_catalog.product_visibility x where x.tenant_id=c.tenant_id and x.template_id=c.template_id and (x.scope_type='TenantWide' or (x.scope_type='LegalEntity' and $3::uuid is not null and x.legal_entity_id=$3)))";
        const string selected = "c.template_id,c.variant_id,c.display_name,c.name_ar,c.name_en,c.sku,c.category_name,c.base_uom_code,c.product_nature";
        var categoryFilter = categoryId is null ? string.Empty : "and c.category_id=$2";
        string countSql;
        string pageSql;
        if (normalized is null)
        {
            countSql = $"select count(*) from foundation_catalog.catalog_search_projection c where c.tenant_id=$1 {categoryFilter} and {browseVisibility}";
            pageSql = $"select {selected} from foundation_catalog.catalog_search_projection c where c.tenant_id=$1 {categoryFilter} and {browseVisibility} order by c.display_name_normalized,c.normalized_sku,c.variant_id limit $4 offset $5";
        }
        else
        {
            const string indexedTextMatches = "select c.tenant_id,c.variant_id,c.template_id,case when c.normalized_sku=$6 then 0 else 2 end as priority from foundation_catalog.catalog_search_projection c where c.tenant_id=$1 and c.search_text_normalized like $2";
            var matches = $"""
                with matched_aliases as materialized (
                    select tenant_id,variant_id from foundation_catalog.product_alias where tenant_id=$1 and normalized_alias like $2
                ), matched_translations as materialized (
                    select tenant_id,template_id from foundation_catalog.product_translation where tenant_id=$1 and lower(name) like $2
                ), matched_brands as materialized (
                    select tenant_id,id from foundation_catalog.brand where tenant_id=$1 and lower(name) like $2
                ), candidates as materialized (
                    {indexedTextMatches}
                    union all
                    select c.tenant_id,c.variant_id,c.template_id,1
                    from foundation_catalog.product_barcode b
                    join foundation_catalog.catalog_search_projection c on c.tenant_id=b.tenant_id and c.variant_id=b.variant_id
                    where b.tenant_id=$1 and b.status='Active' and b.normalized_value=$5
                    union all
                    select c.tenant_id,c.variant_id,c.template_id,2 from matched_aliases a join foundation_catalog.catalog_search_projection c on c.tenant_id=a.tenant_id and c.variant_id=a.variant_id
                    union all
                    select c.tenant_id,c.variant_id,c.template_id,2 from matched_translations t join foundation_catalog.catalog_search_projection c on c.tenant_id=t.tenant_id and c.template_id=t.template_id
                    union all
                    select c.tenant_id,c.variant_id,c.template_id,2
                    from matched_brands b
                    cross join lateral (
                        select p.tenant_id,p.id
                        from foundation_catalog.product_template p
                        where p.tenant_id=b.tenant_id and p.brand_id=b.id
                        offset 0
                    ) p
                    cross join lateral (
                        select c.tenant_id,c.variant_id,c.template_id
                        from foundation_catalog.catalog_search_projection c
                        where c.tenant_id=p.tenant_id and c.template_id=p.id
                        offset 0
                    ) c
                ), matches as materialized (
                    select distinct on (tenant_id,variant_id,template_id) tenant_id,variant_id,template_id,priority
                    from candidates order by tenant_id,variant_id,template_id,priority
                ), filtered as materialized (
                    select c.*,m.priority
                    from matches m
                    join foundation_catalog.catalog_search_projection c on c.tenant_id=m.tenant_id and c.variant_id=m.variant_id and c.template_id=m.template_id
                    where c.tenant_id=$1 and ($3::uuid is null or c.category_id=$3)
                      and exists(select 1 from foundation_catalog.product_visibility x where x.tenant_id=c.tenant_id and x.template_id=c.template_id and (x.scope_type='TenantWide' or (x.scope_type='LegalEntity' and $4::uuid is not null and x.legal_entity_id=$4)))
                ), page as materialized (
                    select * from filtered order by priority,display_name_normalized,normalized_sku,variant_id limit $7 offset $8
                )
                """;
            countSql = $"{matches} select count(*) from filtered";
            const string selectedPage = "p.template_id,p.variant_id,p.display_name,p.name_ar,p.name_en,p.sku,p.category_name,p.base_uom_code,p.product_nature";
            pageSql = $"{matches} select {selectedPage},(select count(*) from filtered),p.variant_id is not null from (values (1)) seed(n) left join lateral (select * from page) p on true order by p.priority,p.display_name_normalized,p.normalized_sku,p.variant_id";
        }

        var total = 0;
        if (normalized is null)
        {
            await using var count = new NpgsqlCommand(countSql, connection);
            count.Parameters.AddWithValue(tenantId.Value);
            count.Parameters.AddWithValue(categoryId ?? (object)DBNull.Value);
            count.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
            total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        }
        await using var command = new NpgsqlCommand(pageSql, connection);
        if (normalized is null)
        {
            command.Parameters.AddWithValue(tenantId.Value);
            command.Parameters.AddWithValue(categoryId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue(pageSize);
            command.Parameters.AddWithValue((page - 1) * pageSize);
        }
        else
        {
            BindSearch(command, tenantId, normalized, categoryId, legalEntityId);
            command.Parameters.AddWithValue(CatalogNormalization.Barcode(query!));
            command.Parameters.AddWithValue(CatalogNormalization.Text(query!));
            command.Parameters.AddWithValue(pageSize);
            command.Parameters.AddWithValue((page - 1) * pageSize);
        }
        var items = new List<CatalogSearchItem>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (normalized is not null && await reader.ReadAsync(cancellationToken))
        {
            total = checked((int)reader.GetInt64(9));
            if (!reader.IsDBNull(0))
            {
                items.Add(new CatalogSearchItem(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), Text(reader, 3), Text(reader, 4), Text(reader, 5), Text(reader, 6), Text(reader, 7), reader.GetString(8)));
                while (await reader.ReadAsync(cancellationToken)) items.Add(new CatalogSearchItem(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), Text(reader, 3), Text(reader, 4), Text(reader, 5), Text(reader, 6), Text(reader, 7), reader.GetString(8)));
            }
        }
        else if (normalized is null)
        {
            while (await reader.ReadAsync(cancellationToken)) items.Add(new CatalogSearchItem(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), Text(reader, 3), Text(reader, 4), Text(reader, 5), Text(reader, 6), Text(reader, 7), reader.GetString(8)));
        }
        return new CatalogSearchPage(items, total, page, pageSize);
    }

    private async Task<NpgsqlConnection> OpenTenantConnectionAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenantId.Value.ToString("D")); await command.ExecuteNonQueryAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static void BindSearch(NpgsqlCommand command, TenantId tenantId, string? normalized, Guid? categoryId, Guid? legalEntityId)
    { command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(normalized is null ? DBNull.Value : string.Concat("%", normalized, "%")); command.Parameters.AddWithValue(categoryId ?? (object)DBNull.Value); command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value); }
    private static string? Text(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static Guid? NullableGuid(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetGuid(index);
}
