using System.Globalization;
using Npgsql;
using Modules.Catalog.DTOs;
using Modules.Catalog.Entities;
using Core.Transactions;
using Shared.Primitives.Identifiers;

namespace Modules.Catalog.Repositories;

public sealed class CatalogDetailRepository(ITransactionCoordinator coordinator, NpgsqlDataSource dataSource)
{
    public async Task<VariantDto> CreateVariantAsync(TenantId tenantId, Guid templateId, CreateVariantRequest request, CancellationToken ct, Guid? legalEntityId = null)
    {
        var variantId = Guid.CreateVersion7();
        var sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
        var normalized = sku is null ? null : CatalogNormalization.Text(sku);
        var suffix = string.IsNullOrWhiteSpace(request.DisplaySuffix) ? null : request.DisplaySuffix.Trim();
        var selectedValues = request.AttributeValues ?? new Dictionary<Guid, Guid>();
        var combinationSignature = string.Empty;

        await coordinator.ExecuteAsync(tenantId, async (context, token) =>
        {
            await EnsureVisibleAsync(context, tenantId, templateId, legalEntityId, token);
            var status = await ScalarAsync<string?>(context, "select status from foundation_catalog.product_template where tenant_id=$1 and id=$2", token, tenantId.Value, templateId);
            if (!string.Equals(status, "Active", StringComparison.Ordinal)) throw new CatalogException("catalog.product_not_active", "Variants can only be added to an active product.");

            var templateAttributes = new List<(Guid Id, bool IsRequired, bool IsVariantDefining)>();
            await using (var attributes = new NpgsqlCommand("select ta.attribute_id,ta.is_required,a.is_variant_defining from foundation_catalog.product_template_attribute ta join foundation_catalog.product_attribute a on a.tenant_id=ta.tenant_id and a.id=ta.attribute_id and a.status='Active' where ta.tenant_id=$1 and ta.template_id=$2 order by ta.attribute_id", context.Connection, context.Transaction))
            {
                attributes.Parameters.AddWithValue(tenantId.Value);
                attributes.Parameters.AddWithValue(templateId);
                await using var reader = await attributes.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) templateAttributes.Add((reader.GetGuid(0), reader.GetBoolean(1), reader.GetBoolean(2)));
            }

            var assignedIds = templateAttributes.Select(attribute => attribute.Id).ToHashSet();
            if (selectedValues.Keys.Any(attributeId => !assignedIds.Contains(attributeId)))
                throw new CatalogException("catalog.variant_attribute_not_assigned", "A selected attribute is not assigned to this product.");
            foreach (var attribute in templateAttributes)
            {
                if (attribute.IsRequired && !selectedValues.ContainsKey(attribute.Id))
                    throw new CatalogException("catalog.variant_attribute_required", "A required product attribute value is missing.");
            }

            var validated = new List<(Guid AttributeId, Guid ValueId)>();
            foreach (var selection in selectedValues.OrderBy(pair => pair.Key))
            {
                var validValue = await ScalarNullableGuidAsync(context,
                    "select id from foundation_catalog.product_attribute_value where tenant_id=$1 and id=$2 and attribute_id=$3 and status='Active'",
                    token, tenantId.Value, selection.Value, selection.Key);
                if (validValue is null) throw new CatalogException("catalog.attribute_value_invalid", "An attribute value is not valid for the selected product attribute.");
                validated.Add((selection.Key, selection.Value));
            }

            var variantSelections = validated.Where(selection => templateAttributes.Any(attribute => attribute.Id == selection.AttributeId && attribute.IsVariantDefining)).ToArray();
            if (variantSelections.Length != templateAttributes.Count(attribute => attribute.IsVariantDefining && attribute.IsRequired))
                throw new CatalogException("catalog.variant_combination_incomplete", "Every required variant-defining attribute must have a value.");
            combinationSignature = string.Join(";", variantSelections.Select(selection => $"{selection.AttributeId:N}={selection.ValueId:N}"));

            try
            {
                await ExecuteAsync(context, "insert into foundation_catalog.product_variant (tenant_id,id,product_template_id,sku,normalized_sku,is_default_variant,display_suffix,combination_signature,status,created_at,updated_at,version) values ($1,$2,$3,$4,$5,false,$6,$7,'Active',$8,$8,1)", token, tenantId.Value, variantId, templateId, sku ?? (object)DBNull.Value, normalized ?? (object)DBNull.Value, suffix ?? (object)DBNull.Value, combinationSignature, DateTimeOffset.UtcNow);
            }
            catch (PostgresException exception) when (exception.SqlState == "23505" && exception.ConstraintName == "ux_catalog_variant_combination")
            {
                throw new CatalogException("catalog.variant_combination_exists", "A variant with the same attribute combination already exists.");
            }
            catch (PostgresException exception) when (exception.SqlState == "23505" && exception.ConstraintName is "product_variant_tenant_id_normalized_sku_key" or "ux_catalog_variant_sku")
            {
                throw new CatalogException("catalog.sku_duplicate", "This SKU is already assigned to another variant.");
            }

            foreach (var selection in validated)
                await ExecuteAsync(context, "insert into foundation_catalog.product_variant_attribute_value (tenant_id,variant_id,attribute_id,attribute_value_id) values ($1,$2,$3,$4)", token, tenantId.Value, variantId, selection.AttributeId, selection.ValueId);
            var baseUomId = await ScalarAsync<Guid>(context, "select uom_id from foundation_catalog.product_uom where tenant_id=$1 and variant_id=(select id from foundation_catalog.product_variant where tenant_id=$1 and product_template_id=$2 and is_default_variant) and is_base and status='Active' and effective_to is null", token, tenantId.Value, templateId);
            await ExecuteAsync(context, "insert into foundation_catalog.product_uom (tenant_id,id,variant_id,uom_id,factor_to_base,is_base,effective_from,status,version) values ($1,$2,$3,$4,1,true,$5,'Active',1)", token, tenantId.Value, Guid.CreateVersion7(), variantId, baseUomId, DateTimeOffset.UtcNow);
            await RefreshProjectionAsync(context, tenantId, templateId, variantId, token);
            AddCatalogEvent(context, tenantId, "CatalogProductVariantCreatedV1", new { templateId, variantId, sku, combinationSignature });
            return true;
        }, ct);
        return new VariantDto(variantId, templateId, sku, suffix, false, CatalogStatus.Active, 1);
    }

    public async Task<CatalogAttributeDto> CreateAttributeAsync(TenantId tenantId, CreateAttributeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.NameAr) || string.IsNullOrWhiteSpace(request.NameEn))
            throw new CatalogException("catalog.attribute_required", "Attribute code and Arabic and English names are required.");
        var id = Guid.CreateVersion7();
        var code = CatalogNormalization.Text(request.Code);
        var now = DateTimeOffset.UtcNow;
        await coordinator.ExecuteAsync(tenantId, async (context, token) =>
        {
            await ExecuteAsync(context, "insert into foundation_catalog.product_attribute (tenant_id,id,code,normalized_code,name_ar,name_en,is_variant_defining,status,version,created_at,updated_at) values ($1,$2,$3,$3,$4,$5,$6,'Active',1,$7,$7)", token, tenantId.Value, id, code, request.NameAr.Trim(), request.NameEn.Trim(), request.IsVariantDefining, now);
            AddCatalogEvent(context, tenantId, "CatalogAttributeCreatedV1", new { attributeId = id, code, request.IsVariantDefining });
            return true;
        }, ct);
        return new CatalogAttributeDto(id, code, request.NameAr.Trim(), request.NameEn.Trim(), request.IsVariantDefining, CatalogStatus.Active, 1);
    }

    public async Task<CatalogAttributeValueDto> CreateAttributeValueAsync(TenantId tenantId, Guid attributeId, CreateAttributeValueRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.NameAr) || string.IsNullOrWhiteSpace(request.NameEn))
            throw new CatalogException("catalog.attribute_value_required", "Attribute value code and Arabic and English names are required.");
        var id = Guid.CreateVersion7();
        var code = CatalogNormalization.Text(request.Code);
        var now = DateTimeOffset.UtcNow;
        await coordinator.ExecuteAsync(tenantId, async (context, token) =>
        {
            if (await ScalarNullableGuidAsync(context, "select id from foundation_catalog.product_attribute where tenant_id=$1 and id=$2 and status='Active'", token, tenantId.Value, attributeId) is null)
                throw new CatalogException("catalog.attribute_not_found", "Product attribute was not found.");
            await ExecuteAsync(context, "insert into foundation_catalog.product_attribute_value (tenant_id,id,attribute_id,code,normalized_code,name_ar,name_en,status,version,created_at,updated_at) values ($1,$2,$3,$4,$4,$5,$6,'Active',1,$7,$7)", token, tenantId.Value, id, attributeId, code, request.NameAr.Trim(), request.NameEn.Trim(), now);
            AddCatalogEvent(context, tenantId, "CatalogAttributeValueCreatedV1", new { attributeId, attributeValueId = id, code });
            return true;
        }, ct);
        return new CatalogAttributeValueDto(id, attributeId, code, request.NameAr.Trim(), request.NameEn.Trim(), CatalogStatus.Active, 1);
    }

    public async Task<TemplateAttributeDto> AssignTemplateAttributeAsync(TenantId tenantId, Guid templateId, Guid attributeId, bool isRequired, int displayOrder, CancellationToken ct, Guid? legalEntityId = null)
    {
        if (displayOrder < 0) throw new CatalogException("catalog.attribute_order_invalid", "Attribute display order cannot be negative.");
        return await coordinator.ExecuteAsync(tenantId, async (context, token) =>
        {
            await EnsureVisibleAsync(context, tenantId, templateId, legalEntityId, token);
            if (await ScalarNullableGuidAsync(context, "select id from foundation_catalog.product_template where tenant_id=$1 and id=$2 and status='Active'", token, tenantId.Value, templateId) is null)
                throw new CatalogException("catalog.product_not_found", "Product was not found.");
            var attribute = await ReadAttributeAsync(context, tenantId, attributeId, token);
            if (attribute is null) throw new CatalogException("catalog.attribute_not_found", "Product attribute was not found.");
            await ExecuteAsync(context, "insert into foundation_catalog.product_template_attribute (tenant_id,template_id,attribute_id,display_order,is_required) values ($1,$2,$3,$4,$5) on conflict (tenant_id,template_id,attribute_id) do update set display_order=excluded.display_order,is_required=excluded.is_required", token, tenantId.Value, templateId, attributeId, displayOrder, isRequired);
            AddCatalogEvent(context, tenantId, "CatalogProductAttributesChangedV1", new { templateId, attributeId, isRequired, displayOrder });
            return new TemplateAttributeDto(attribute, isRequired, displayOrder, await ReadAttributeValuesAsync(context, tenantId, attributeId, token));
        }, ct);
    }

    public async Task<IReadOnlyList<TemplateAttributeDto>> ListTemplateAttributesAsync(TenantId tenantId, Guid templateId, CancellationToken ct, Guid? legalEntityId = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenantId, ct);
        var attributes = new List<(CatalogAttributeDto Attribute, bool Required, int Order)>();
        await using (var command = new NpgsqlCommand("select a.id,a.code,a.name_ar,a.name_en,a.is_variant_defining,a.status,a.version,ta.is_required,ta.display_order from foundation_catalog.product_template_attribute ta join foundation_catalog.product_attribute a on a.tenant_id=ta.tenant_id and a.id=ta.attribute_id where ta.tenant_id=$1 and ta.template_id=$2 and exists(select 1 from foundation_catalog.product_visibility visibility where visibility.tenant_id=ta.tenant_id and visibility.template_id=ta.template_id and (visibility.scope_type='TenantWide' or (visibility.scope_type='LegalEntity' and $3::uuid is not null and visibility.legal_entity_id=$3))) order by ta.display_order,a.id", connection))
        {
            command.Parameters.AddWithValue(tenantId.Value);
            command.Parameters.AddWithValue(templateId);
            command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) attributes.Add((new CatalogAttributeDto(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), Enum.Parse<CatalogStatus>(reader.GetString(5)), reader.GetInt64(6)), reader.GetBoolean(7), reader.GetInt32(8)));
        }
        var result = new List<TemplateAttributeDto>(attributes.Count);
        foreach (var attribute in attributes)
        {
            var values = new List<CatalogAttributeValueDto>();
            await using var command = new NpgsqlCommand("select id,attribute_id,code,name_ar,name_en,status,version from foundation_catalog.product_attribute_value where tenant_id=$1 and attribute_id=$2 order by code", connection);
            command.Parameters.AddWithValue(tenantId.Value);
            command.Parameters.AddWithValue(attribute.Attribute.Id);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) values.Add(new CatalogAttributeValueDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), Enum.Parse<CatalogStatus>(reader.GetString(5)), reader.GetInt64(6)));
            result.Add(new TemplateAttributeDto(attribute.Attribute, attribute.Required, attribute.Order, values));
        }
        return result;
    }

    public async Task<ProductUomDto> AddUomAsync(TenantId tenantId, Guid templateId, Guid variantId, AddProductUomRequest request, CancellationToken ct, Guid? legalEntityId = null)
    {
        if (request.FactorToBase <= 0 || (request.EffectiveTo is not null && request.EffectiveTo <= request.EffectiveFrom))
            throw new CatalogException("catalog.conversion_invalid", "Conversion factor and effective dates are invalid.");
        var id = Guid.CreateVersion7();
        return await coordinator.ExecuteAsync(tenantId, async (context, token) =>
        {
            await EnsureVisibleAsync(context, tenantId, templateId, legalEntityId, token);
            var variantTemplate = await ScalarNullableGuidAsync(context, "select product_template_id from foundation_catalog.product_variant where tenant_id=$1 and id=$2 and status='Active'", token, tenantId.Value, variantId);
            if (variantTemplate != templateId) throw new CatalogException("catalog.variant_not_found", "Variant was not found for this product.");
            var baseDimension = await ScalarAsync<string>(context, "select u.dimension from foundation_catalog.product_uom pu join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id where pu.tenant_id=$1 and pu.variant_id=$2 and pu.is_base and pu.status='Active' and pu.effective_to is null", token, tenantId.Value, variantId);
            var unitDimension = await ScalarAsync<string>(context, "select dimension from foundation_catalog.uom where tenant_id=$1 and id=$2 and status='Active'", token, tenantId.Value, request.UomId);
            if (!string.Equals(baseDimension, unitDimension, StringComparison.Ordinal) && baseDimension != "BusinessUnit" && unitDimension != "BusinessUnit")
                throw new CatalogException("catalog.uom_dimension_mismatch", "Units from different dimensions cannot be converted.");
            var overlap = await ScalarAsync<bool>(context, "select exists(select 1 from foundation_catalog.product_uom where tenant_id=$1 and variant_id=$2 and uom_id=$3 and status='Active' and not (coalesce(effective_to,'infinity'::timestamptz) <= $4 or coalesce($5,'infinity'::timestamptz) <= effective_from))", token, tenantId.Value, variantId, request.UomId, request.EffectiveFrom, request.EffectiveTo ?? (object)DBNull.Value);
            if (overlap) throw new CatalogException("catalog.conversion_overlap", "Effective conversion periods cannot overlap.");
            if (request.RoundingPrecision is < 0 or > 6) throw new CatalogException("catalog.uom_precision_invalid", "Unit rounding precision must be between 0 and 6.");
            var usageFlags = NormalizeUsageFlags(request.UsageFlags);
            try
            {
                await ExecuteAsync(context, "insert into foundation_catalog.product_uom (tenant_id,id,variant_id,uom_id,factor_to_base,is_base,usage_flags,rounding_precision,effective_from,effective_to,status,version) values ($1,$2,$3,$4,$5,false,$6,$7,$8,$9,'Active',1)", token, tenantId.Value, id, variantId, request.UomId, request.FactorToBase, usageFlags, (object?)request.RoundingPrecision ?? DBNull.Value, request.EffectiveFrom, request.EffectiveTo ?? (object)DBNull.Value);
            }
            catch (PostgresException exception) when (exception.SqlState == "23P01" && exception.ConstraintName == "product_uom_period_no_overlap")
            {
                throw new CatalogException("catalog.conversion_overlap", "Effective conversion periods cannot overlap.");
            }
            AddCatalogEvent(context, tenantId, "CatalogProductUomChangedV1", new { templateId, variantId, uomId = request.UomId, request.FactorToBase, UsageFlags = usageFlags, request.RoundingPrecision, request.EffectiveFrom, request.EffectiveTo });
            var code = await ScalarAsync<string>(context, "select code from foundation_catalog.uom where tenant_id=$1 and id=$2", token, tenantId.Value, request.UomId);
            return new ProductUomDto(id, variantId, request.UomId, code, request.FactorToBase, false, request.EffectiveFrom, request.EffectiveTo, CatalogStatus.Active, usageFlags, request.RoundingPrecision);
        }, ct);
    }

    public async Task<IReadOnlyList<ProductUomDto>> ListUomsAsync(TenantId tenantId, Guid variantId, CancellationToken ct, Guid? legalEntityId = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenantId, ct); await using var command = new NpgsqlCommand("select pu.id,pu.variant_id,pu.uom_id,u.code,pu.factor_to_base,pu.is_base,pu.effective_from,pu.effective_to,pu.status,pu.usage_flags,pu.rounding_precision from foundation_catalog.product_uom pu join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id join foundation_catalog.product_variant v on v.tenant_id=pu.tenant_id and v.id=pu.variant_id join foundation_catalog.product_visibility visibility on visibility.tenant_id=v.tenant_id and visibility.template_id=v.product_template_id where pu.tenant_id=$1 and pu.variant_id=$2 and (visibility.scope_type='TenantWide' or (visibility.scope_type='LegalEntity' and $3::uuid is not null and visibility.legal_entity_id=$3)) order by pu.is_base desc,pu.effective_from", connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(variantId); command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value); var result = new List<ProductUomDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result.Add(new ProductUomDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetDecimal(4), reader.GetBoolean(5), reader.GetFieldValue<DateTimeOffset>(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7), Enum.Parse<CatalogStatus>(reader.GetString(8)), reader.GetFieldValue<string[]>(9), reader.IsDBNull(10) ? null : reader.GetInt16(10))); return result;
    }

    public async Task<ProductVariantSnapshot> ResolveAsync(TenantId tenantId, string value, CancellationToken ct, Guid? legalEntityId = null)
    {
        var isGuid = Guid.TryParse(value, out var parsedId);
        var normalizedSku = CatalogNormalization.Text(value);
        var normalizedBarcode = IdentifierRules.RequireBarcode(value);
        var resolvedAt = DateTimeOffset.UtcNow;
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenantId, ct);
        const string sql = @"select v.id,p.id,v.sku,p.display_name,base.uom_id,base.factor_to_base,barcode.uom_id,coalesce(scan_uom.code,base_uom.code),coalesce(scan_unit.factor_to_base,base.factor_to_base),scan_unit.id 
from foundation_catalog.product_variant v 
join foundation_catalog.product_template p on p.tenant_id=v.tenant_id and p.id=v.product_template_id 
left join foundation_catalog.product_uom base on base.tenant_id=v.tenant_id and base.variant_id=v.id and base.is_base and base.status='Active' and base.effective_from <= $4 and (base.effective_to is null or base.effective_to > $4) 
left join foundation_catalog.uom base_uom on base_uom.tenant_id=base.tenant_id and base_uom.id=base.uom_id 
left join foundation_catalog.product_barcode barcode on barcode.tenant_id=v.tenant_id and barcode.variant_id=v.id and barcode.normalized_value=$3 and barcode.status='Active' 
left join foundation_catalog.product_uom scan_unit on scan_unit.tenant_id=v.tenant_id and scan_unit.variant_id=v.id and scan_unit.uom_id=barcode.uom_id and scan_unit.status='Active' and scan_unit.effective_from <= $4 and (scan_unit.effective_to is null or scan_unit.effective_to > $4) 
left join foundation_catalog.uom scan_uom on scan_uom.tenant_id=scan_unit.tenant_id and scan_uom.id=scan_unit.uom_id 
where v.tenant_id=$1 and v.status='Active' and p.status='Active' 
and (v.normalized_sku=$2 or barcode.id is not null or ($6::uuid is not null and (v.id=$6 or p.id=$6))) 
and exists(select 1 from foundation_catalog.product_visibility visibility where visibility.tenant_id=p.tenant_id and visibility.template_id=p.id and (visibility.scope_type='TenantWide' or (visibility.scope_type='LegalEntity' and $5::uuid is not null and visibility.legal_entity_id=$5))) 
order by case when $6::uuid is not null and v.id=$6 then 0 when v.normalized_sku=$2 then 1 else 2 end limit 1";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(normalizedSku);
        command.Parameters.AddWithValue(normalizedBarcode);
        command.Parameters.AddWithValue(resolvedAt);
        command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(isGuid ? parsedId : (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new CatalogException("catalog.identifier_not_found", "No active product matches this SKU or barcode.");
        if (reader.IsDBNull(4) || reader.IsDBNull(5)) throw new CatalogException("catalog.base_uom_unavailable", "The product has no effective base unit conversion.");
        if (!reader.IsDBNull(6) && reader.IsDBNull(9)) throw new CatalogException("catalog.barcode_uom_conversion_unavailable", "The barcode unit has no effective conversion.");
        return new ProductVariantSnapshot(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetGuid(4), reader.GetDecimal(8), reader.IsDBNull(6) ? reader.GetGuid(4) : reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetString(7), resolvedAt);
    }

    public async Task<IReadOnlyList<ResolvedVariantIdentity>> ResolveBatchAsync(TenantId tenantId, IReadOnlyList<string> values, CancellationToken ct, Guid? legalEntityId = null)
    {
        if (values.Count == 0) return [];
        if (values.Count > 500) throw new CatalogException("catalog.batch_size_exceeded", "Batch size exceeds maximum limit of 500 items.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenantId, ct);
        var guids = new HashSet<Guid>();
        var texts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in values)
        {
            if (Guid.TryParse(v, out var g)) guids.Add(g);
            else if (!string.IsNullOrWhiteSpace(v)) texts.Add(CatalogNormalization.Text(v));
        }
        const string sql = @"select c.variant_id, c.template_id, c.sku, c.display_name, c.name_ar, c.name_en, c.base_uom_code 
from foundation_catalog.catalog_search_projection c 
where c.tenant_id=$1 
and c.status = 'Active'
and (c.variant_id = any($2) or c.template_id = any($2) or c.normalized_sku = any($3)) 
and exists(select 1 from foundation_catalog.product_visibility visibility where visibility.tenant_id=c.tenant_id and visibility.template_id=c.template_id and (visibility.scope_type='TenantWide' or (visibility.scope_type='LegalEntity' and $4::uuid is not null and visibility.legal_entity_id=$4)))";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(guids.ToArray());
        command.Parameters.AddWithValue(texts.ToArray());
        command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var list = new List<ResolvedVariantIdentity>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ResolvedVariantIdentity(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)
            ));
        }
        return list;
    }

    public async Task<ProductReadinessDto> ReadinessAsync(TenantId tenantId, Guid templateId, CancellationToken ct, Guid? legalEntityId = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenantId, ct); await using var command = new NpgsqlCommand("select p.id,p.display_name,p.product_nature,exists(select 1 from foundation_catalog.product_variant v join foundation_catalog.product_uom pu on pu.tenant_id=v.tenant_id and pu.variant_id=v.id and pu.is_base and pu.status='Active' and pu.effective_to is null where v.tenant_id=p.tenant_id and v.product_template_id=p.id) as has_base,exists(select 1 from foundation_catalog.product_variant v join foundation_catalog.product_barcode b on b.tenant_id=v.tenant_id and b.variant_id=v.id and b.status='Active' where v.tenant_id=p.tenant_id and v.product_template_id=p.id) as has_barcode from foundation_catalog.product_template p where p.tenant_id=$1 and p.id=$2 and exists(select 1 from foundation_catalog.product_visibility visibility where visibility.tenant_id=p.tenant_id and visibility.template_id=p.id and (visibility.scope_type='TenantWide' or (visibility.scope_type='LegalEntity' and $3::uuid is not null and visibility.legal_entity_id=$3)))", connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(templateId); command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new CatalogException("catalog.product_not_found", "Product was not found."); var missing = new List<string>(); var warnings = new List<string>(); if (!reader.GetBoolean(3)) missing.Add("base_uom"); if (reader.GetString(2) == "Goods" && !reader.GetBoolean(4)) warnings.Add("goods_without_barcode"); return new ProductReadinessDto(templateId, missing.Count == 0, missing, warnings);
    }

    public async Task<bool> AddAliasAsync(TenantId tenantId, Guid templateId, AddAliasRequest request, CancellationToken ct, Guid? legalEntityId = null) => await coordinator.ExecuteAsync(tenantId, async (context, token) => { await EnsureVisibleAsync(context, tenantId, templateId, legalEntityId, token); await EnsureVariantAsync(context, tenantId, templateId, request.VariantId, token); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into foundation_catalog.product_alias (tenant_id,id,variant_id,alias,normalized_alias,alias_type,language_code,created_at) values ($1,$2,$3,$4,$5,$6,$7,$8)", token, tenantId.Value, id, request.VariantId, request.Alias.Trim(), CatalogNormalization.Search(request.Alias), request.AliasType, request.LanguageCode ?? (object)DBNull.Value, DateTimeOffset.UtcNow); AddCatalogEvent(context, tenantId, "CatalogProductAliasAddedV1", new { templateId, variantId = request.VariantId, aliasId = id }); return true; }, ct);
    public async Task<bool> AddTranslationAsync(TenantId tenantId, Guid templateId, AddTranslationRequest request, CancellationToken ct, Guid? legalEntityId = null) => await coordinator.ExecuteAsync(tenantId, async (context, token) => { await EnsureVisibleAsync(context, tenantId, templateId, legalEntityId, token); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into foundation_catalog.product_translation (tenant_id,id,template_id,language_code,name,description) values ($1,$2,$3,$4,$5,$6)", token, tenantId.Value, id, templateId, request.LanguageCode.Trim(), request.Name.Trim(), request.Description ?? (object)DBNull.Value); AddCatalogEvent(context, tenantId, "CatalogProductTranslationChangedV1", new { templateId, translationId = id, languageCode = request.LanguageCode.Trim() }); return true; }, ct);
    public async Task<ProductReferenceDto> AddReferenceAsync(TenantId tenantId, Guid templateId, AddReferenceRequest request, CancellationToken ct, Guid? legalEntityId = null) { var id = Guid.CreateVersion7(); await coordinator.ExecuteAsync(tenantId, async (context, token) => { await EnsureVisibleAsync(context, tenantId, templateId, legalEntityId, token); await EnsureVariantAsync(context, tenantId, templateId, request.VariantId, token); await ExecuteAsync(context, "insert into foundation_catalog.product_reference (tenant_id,id,variant_id,reference_type,party_id,reference_code,status) values ($1,$2,$3,$4,$5,$6,'Active')", token, tenantId.Value, id, request.VariantId, request.ReferenceType, request.PartyId ?? (object)DBNull.Value, request.ReferenceCode.Trim()); AddCatalogEvent(context, tenantId, "CatalogProductReferenceAddedV1", new { templateId, referenceId = id, variantId = request.VariantId, request.ReferenceType }); return true; }, ct); return new ProductReferenceDto(id, request.VariantId, request.ReferenceType, request.PartyId, request.ReferenceCode.Trim(), CatalogStatus.Active); }

    public async Task<PagedProductReferences> ListReferencesAsync(TenantId tenantId, Guid templateId, int page, int pageSize, CancellationToken ct, Guid? legalEntityId = null)
    {
        page = Math.Clamp(page, 1, 100000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenantId, ct);
        const string sql = "select r.id,r.variant_id,r.reference_type,r.party_id,r.reference_code,r.status from foundation_catalog.product_reference r join foundation_catalog.product_variant v on v.tenant_id=r.tenant_id and v.id=r.variant_id where r.tenant_id=$1 and v.product_template_id=$2 and exists(select 1 from foundation_catalog.product_visibility x where x.tenant_id=v.tenant_id and x.template_id=v.product_template_id and (x.scope_type='TenantWide' or (x.scope_type='LegalEntity' and $3::uuid is not null and x.legal_entity_id=$3))) order by r.id limit $4 offset $5";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(templateId);
        command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(pageSize + 1);
        command.Parameters.AddWithValue((page - 1) * pageSize);
        var items = new List<ProductReferenceDto>(pageSize);
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (items.Count <= pageSize && await reader.ReadAsync(ct))
                items.Add(new ProductReferenceDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetString(4), Enum.Parse<CatalogStatus>(reader.GetString(5))));
        }
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new PagedProductReferences(items, page, pageSize, hasMore);
    }

    private static async Task<CatalogAttributeDto?> ReadAttributeAsync(ITransactionContext context, TenantId tenantId, Guid attributeId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,code,name_ar,name_en,is_variant_defining,status,version from foundation_catalog.product_attribute where tenant_id=$1 and id=$2 and status='Active'", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(attributeId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new CatalogAttributeDto(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), Enum.Parse<CatalogStatus>(reader.GetString(5)), reader.GetInt64(6))
            : null;
    }

    private static async Task<IReadOnlyList<CatalogAttributeValueDto>> ReadAttributeValuesAsync(ITransactionContext context, TenantId tenantId, Guid attributeId, CancellationToken ct)
    {
        var values = new List<CatalogAttributeValueDto>();
        await using var command = new NpgsqlCommand("select id,attribute_id,code,name_ar,name_en,status,version from foundation_catalog.product_attribute_value where tenant_id=$1 and attribute_id=$2 and status='Active' order by code", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(attributeId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) values.Add(new CatalogAttributeValueDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), Enum.Parse<CatalogStatus>(reader.GetString(5)), reader.GetInt64(6)));
        return values;
    }

    private static async Task EnsureVariantAsync(ITransactionContext context, TenantId tenantId, Guid templateId, Guid variantId, CancellationToken ct) { var found = await ScalarNullableGuidAsync(context, "select product_template_id from foundation_catalog.product_variant where tenant_id=$1 and id=$2", ct, tenantId.Value, variantId); if (found != templateId) throw new CatalogException("catalog.variant_not_found", "Variant was not found for this product."); }
    private static async Task EnsureVisibleAsync(ITransactionContext context, TenantId tenantId, Guid templateId, Guid? legalEntityId, CancellationToken ct) { var visible = await ScalarAsync<bool>(context, "select exists(select 1 from foundation_catalog.product_visibility where tenant_id=$1 and template_id=$2 and (scope_type='TenantWide' or (scope_type='LegalEntity' and $3::uuid is not null and legal_entity_id=$3)))", ct, tenantId.Value, templateId, legalEntityId ?? (object)DBNull.Value); if (!visible) throw new CatalogException("catalog.product_not_found", "Product was not found."); }
    private static async Task RefreshProjectionAsync(ITransactionContext context, TenantId tenantId, Guid templateId, Guid variantId, CancellationToken ct) { await ExecuteAsync(context, "delete from foundation_catalog.catalog_search_projection where tenant_id=$1 and variant_id=$2", ct, tenantId.Value, variantId); await ExecuteAsync(context, "insert into foundation_catalog.catalog_search_projection (tenant_id,variant_id,template_id,display_name,display_name_normalized,name_ar,name_en,sku,normalized_sku,category_id,category_name,base_uom_code,product_nature,status,updated_at) select p.tenant_id,v.id,p.id,p.display_name,p.display_name_normalized,p.name_ar,p.name_en,v.sku,v.normalized_sku,p.category_id,c.name,u.code,p.product_nature,p.status,p.updated_at from foundation_catalog.product_template p join foundation_catalog.product_variant v on v.tenant_id=p.tenant_id and v.product_template_id=p.id left join foundation_catalog.product_category c on c.tenant_id=p.tenant_id and c.id=p.category_id left join foundation_catalog.product_uom pu on pu.tenant_id=v.tenant_id and pu.variant_id=v.id and pu.is_base and pu.status='Active' and pu.effective_to is null left join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id where p.tenant_id=$1 and p.id=$2 and v.id=$3", ct, tenantId.Value, templateId, variantId); }
    private static void AddCatalogEvent(ITransactionContext context, TenantId tenantId, string messageType, object payload) { var now = DateTimeOffset.UtcNow; var correlationId = Guid.CreateVersion7(); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), messageType, System.Text.Json.JsonSerializer.Serialize(payload), tenantId, now, correlationId, correlationId, "catalog")); }
    private static async Task SetTenantAsync(NpgsqlConnection connection, TenantId tenantId, CancellationToken ct) { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenantId.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); }
    private static string[] NormalizeUsageFlags(IReadOnlyList<string>? usageFlags)
    {
        if (usageFlags is null) return [];
        var normalized = usageFlags.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (normalized.Length > 8 || normalized.Any(value => value.Length > 32)) throw new CatalogException("catalog.uom_usage_invalid", "Unit usage labels are invalid.");
        return normalized;
    }
    private static async Task<int> ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i] ?? DBNull.Value); return await command.ExecuteNonQueryAsync(ct); }
    private static async Task<T> ScalarAsync<T>(ITransactionContext context, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i] ?? DBNull.Value); var value = await command.ExecuteScalarAsync(ct); if (value is null or DBNull) throw new KeyNotFoundException("catalog.not_found"); if (value is T result) return result; if (typeof(T) == typeof(Guid)) return (T)(object)Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!); if (typeof(T) == typeof(Guid?)) return (T)(object)(Guid?)Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!); return (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), CultureInfo.InvariantCulture); }
    private static async Task<Guid?> ScalarNullableGuidAsync(ITransactionContext context, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i] ?? DBNull.Value); var value = await command.ExecuteScalarAsync(ct); return value is null or DBNull ? null : (Guid)value; }
}
