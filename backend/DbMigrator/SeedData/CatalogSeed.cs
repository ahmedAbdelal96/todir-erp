using System.Globalization;
using System.Text;

namespace DbMigrator.SeedData;

public static class CatalogSeed
{
    public static async Task<SeedModuleSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        await EnsureMasterDataAsync(context, cancellationToken);
        var products = BuildProducts(context.Tenant, context.Tenant.Id == SeedIds.AtlasTenant ? 100 : 60);
        foreach (var product in products)
        {
            await context.ExecuteAsync("insert into foundation_catalog.product_template (tenant_id,id,display_name,display_name_normalized,name_ar,name_en,product_nature,category_id,brand_id,status,created_at,updated_at,version) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$11,1) on conflict (tenant_id,id) do update set display_name=excluded.display_name,display_name_normalized=excluded.display_name_normalized,name_ar=excluded.name_ar,name_en=excluded.name_en,product_nature=excluded.product_nature,category_id=excluded.category_id,brand_id=excluded.brand_id,status=excluded.status,updated_at=case when (foundation_catalog.product_template.display_name,foundation_catalog.product_template.display_name_normalized,foundation_catalog.product_template.name_ar,foundation_catalog.product_template.name_en,foundation_catalog.product_template.product_nature,foundation_catalog.product_template.category_id,foundation_catalog.product_template.brand_id,foundation_catalog.product_template.status) is distinct from (excluded.display_name,excluded.display_name_normalized,excluded.name_ar,excluded.name_en,excluded.product_nature,excluded.category_id,excluded.brand_id,excluded.status) then excluded.updated_at else foundation_catalog.product_template.updated_at end,version=foundation_catalog.product_template.version+case when (foundation_catalog.product_template.display_name,foundation_catalog.product_template.display_name_normalized,foundation_catalog.product_template.name_ar,foundation_catalog.product_template.name_en,foundation_catalog.product_template.product_nature,foundation_catalog.product_template.category_id,foundation_catalog.product_template.brand_id,foundation_catalog.product_template.status) is distinct from (excluded.display_name,excluded.display_name_normalized,excluded.name_ar,excluded.name_en,excluded.product_nature,excluded.category_id,excluded.brand_id,excluded.status) then 1 else 0 end", cancellationToken, context.Tenant.Id, product.Id, product.DisplayName, Normalize(product.DisplayName), product.NameAr, product.NameEn, product.Nature, SeedIds.Category(context.Tenant.Id, product.CategoryIndex), SeedIds.Brand(context.Tenant.Id, product.BrandIndex), product.Status, context.AsOf);
            if (product.Index == 8)
            {
                await context.ExecuteAsync("delete from foundation_catalog.product_visibility where tenant_id=$1 and template_id=$2", cancellationToken, context.Tenant.Id, product.Id);
                await context.ExecuteAsync("insert into foundation_catalog.product_visibility (tenant_id,template_id,scope_type,legal_entity_id,visibility_id) values ($1,$2,'LegalEntity',$3,$4)", cancellationToken, context.Tenant.Id, product.Id, SeedIds.TenantEntity(context.Tenant.Id), SeedIds.Child(context.Tenant.Id, "product-visibility", product.Index));
            }
            else
            {
                await context.ExecuteAsync("insert into foundation_catalog.product_visibility (tenant_id,template_id,scope_type,visibility_id) select $1,$2,'TenantWide',$3 where not exists (select 1 from foundation_catalog.product_visibility where tenant_id=$1 and template_id=$2)", cancellationToken, context.Tenant.Id, product.Id, SeedIds.Child(context.Tenant.Id, "product-visibility", product.Index));
            }
            await UpsertVariantAsync(context, product.Id, product.Index, "default", product.Sku, true, product.Status, cancellationToken);
            await UpsertProductUomAsync(context, product.Index, product.Index, "default", product.Nature == "Service" ? "HOUR" : product.CategoryIndex % 2 == 0 ? "EA" : "KG", 1m, true, cancellationToken);
            if (product.Index % 10 == 0)
            {
                await UpsertVariantAsync(context, product.Id, product.Index, "case", $"{product.Sku ?? $"MIX-{product.Index:000}"}-CASE", false, product.Status, cancellationToken);
                await UpsertProductUomAsync(context, product.Index, product.Index, "case", "BOX", 12m, false, cancellationToken);
            }
            var defaultVariantId = SeedIds.Variant(context.Tenant.Id, product.Index, "default");
            if (product.Index % 2 == 0)
                await context.ExecuteAsync("insert into foundation_catalog.product_barcode (tenant_id,id,variant_id,uom_id,barcode_type,value,normalized_value,is_primary,status,created_at) values ($1,$2,$3,$4,'Ean13',$5,$5,true,'Active',$6) on conflict (tenant_id,id) do update set value=excluded.value,normalized_value=excluded.normalized_value,status='Active'", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "barcode", product.Index), defaultVariantId, SeedIds.Uom(context.Tenant.Id, product.CategoryIndex % 2 == 0 ? "EA" : "KG"), $"622{context.Tenant.Id.ToString()[..4]}{product.Index:00000}", context.AsOf);
            if (product.Index == 2)
            {
                await UpsertAlternateUomAsync(context, product.Index, "PACK", 6m, "pack6", cancellationToken);
                await UpsertAlternateUomAsync(context, product.Index, "CARTON", 24m, "carton24", cancellationToken);
                await context.ExecuteAsync("insert into foundation_catalog.product_barcode (tenant_id,id,variant_id,uom_id,barcode_type,value,normalized_value,is_primary,status,created_at) values ($1,$2,$3,$4,'Ean13',$5,$5,false,'Active',$6) on conflict (tenant_id,id) do update set uom_id=excluded.uom_id,value=excluded.value,normalized_value=excluded.normalized_value,status='Active'", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "barcode-pack", product.Index), defaultVariantId, SeedIds.Uom(context.Tenant.Id, "PACK"), $"623{context.Tenant.Id.ToString()[..4]}{product.Index:00000}", context.AsOf);
            }
            if (product.Index % 4 == 0)
                await context.ExecuteAsync("insert into foundation_catalog.product_alias (tenant_id,id,variant_id,alias,normalized_alias,alias_type,language_code,created_at) values ($1,$2,$3,$4,$5,'Search','ar',$6) on conflict (tenant_id,id) do update set variant_id=excluded.variant_id,alias=excluded.alias,normalized_alias=excluded.normalized_alias,alias_type=excluded.alias_type,language_code=excluded.language_code", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "alias", product.Index), defaultVariantId, $"بديل {product.Index:000} {product.NameAr}", Normalize($"بديل {product.Index:000} {product.NameAr}"), context.AsOf);
            if (product.Index % 3 == 0)
                await context.ExecuteAsync("insert into foundation_catalog.product_translation (tenant_id,id,template_id,language_code,name,description) values ($1,$2,$3,'en',$4,$5) on conflict (tenant_id,template_id,language_code) do update set name=excluded.name,description=excluded.description", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "translation", product.Index), product.Id, product.NameEn, "Canonical development catalog description.");
            if (product.Index <= 6)
                await context.ExecuteAsync("insert into foundation_catalog.product_reference (tenant_id,id,variant_id,reference_type,party_id,reference_code,status) values ($1,$2,$3,$4,$5,$6,'Active') on conflict (tenant_id,id) do update set reference_type=excluded.reference_type,party_id=excluded.party_id,reference_code=excluded.reference_code,status='Active'", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "reference", product.Index), defaultVariantId, product.Index % 2 == 0 ? "Customer" : "Supplier", SeedIds.Party(context.Tenant.Id, product.Index), $"REF-{product.Index:0000}");
            await RefreshProjectionAsync(context, product, defaultVariantId, cancellationToken);
            if (product.Index % 10 == 0) await RefreshProjectionAsync(context, product, SeedIds.Variant(context.Tenant.Id, product.Index, "case"), cancellationToken);
            if (product.Index == 7) await EnsureFashionScenarioAsync(context, product, cancellationToken);
        }
        return new(products.Count, products.Count(product => product.Status == "Active"), products.Count(product => product.Status == "Archived"), products.Any(product => product.Index % 10 == 0), products.Any(product => product.Index % 3 == 0), products.Any(product => product.Index % 2 == 1));
    }

    private static async Task EnsureMasterDataAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var categories = new[] { "مواد مكتبية", "أدوات كهربائية", "مستلزمات تعبئة", "معدات تشغيل", "أجهزة قياس", "قطع غيار" };
        for (var index = 0; index < categories.Length; index++)
            await context.ExecuteAsync("insert into foundation_catalog.product_category (tenant_id,id,name,name_normalized,parent_id,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,'Active',1,$6,$6) on conflict (tenant_id,id) do update set name=excluded.name,name_normalized=excluded.name_normalized,parent_id=excluded.parent_id,status='Active',updated_at=case when (foundation_catalog.product_category.name,foundation_catalog.product_category.name_normalized,foundation_catalog.product_category.parent_id,foundation_catalog.product_category.status) is distinct from (excluded.name,excluded.name_normalized,excluded.parent_id,excluded.status) then excluded.updated_at else foundation_catalog.product_category.updated_at end,version=foundation_catalog.product_category.version+case when (foundation_catalog.product_category.name,foundation_catalog.product_category.name_normalized,foundation_catalog.product_category.parent_id,foundation_catalog.product_category.status) is distinct from (excluded.name,excluded.name_normalized,excluded.parent_id,excluded.status) then 1 else 0 end", cancellationToken, context.Tenant.Id, SeedIds.Category(context.Tenant.Id, index), categories[index], Normalize(categories[index]), index == 0 ? (object)DBNull.Value : SeedIds.Category(context.Tenant.Id, 0), context.AsOf);
        var brands = new[] { "النخبة", "أطلس", "دلتا", "الرواد" };
        for (var index = 0; index < brands.Length; index++)
            await context.ExecuteAsync("insert into foundation_catalog.brand (tenant_id,id,name,name_normalized,status,version,created_at,updated_at) values ($1,$2,$3,$4,'Active',1,$5,$5) on conflict (tenant_id,id) do update set name=excluded.name,name_normalized=excluded.name_normalized,status='Active',updated_at=case when (foundation_catalog.brand.name,foundation_catalog.brand.name_normalized,foundation_catalog.brand.status) is distinct from (excluded.name,excluded.name_normalized,excluded.status) then excluded.updated_at else foundation_catalog.brand.updated_at end,version=foundation_catalog.brand.version+case when (foundation_catalog.brand.name,foundation_catalog.brand.name_normalized,foundation_catalog.brand.status) is distinct from (excluded.name,excluded.name_normalized,excluded.status) then 1 else 0 end", cancellationToken, context.Tenant.Id, SeedIds.Brand(context.Tenant.Id, index), brands[index], Normalize(brands[index]), context.AsOf);
        foreach (var dimension in new[] { ("COUNT", "عدد"), ("MASS", "وزن"), ("BUSINESS", "وحدة أعمال") })
            await context.ExecuteAsync("insert into foundation_catalog.uom_dimension (tenant_id,id,code,name,status) values ($1,$2,$3,$4,'Active') on conflict (tenant_id,id) do update set code=excluded.code,name=excluded.name,status='Active'", cancellationToken, context.Tenant.Id, SeedIds.Stable($"{context.Tenant.Id:D}:dimension:{dimension.Item1}"), dimension.Item1, dimension.Item2);
        var uoms = new[] { ("EA", "قطعة", "Each", "Count", false, 0), ("BOX", "صندوق", "Box", "BusinessUnit", false, 0), ("PACK", "عبوة", "Pack", "BusinessUnit", false, 0), ("CARTON", "كرتونة", "Carton", "BusinessUnit", false, 0), ("KG", "كيلوجرام", "Kilogram", "Mass", true, 3), ("HOUR", "ساعة", "Hour", "Time", true, 2) };
        foreach (var uom in uoms)
            await context.ExecuteAsync("insert into foundation_catalog.uom (tenant_id,id,code,name_ar,name_en,dimension,allow_fraction,precision,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,'Active',1,$9,$9) on conflict (tenant_id,id) do update set code=excluded.code,name_ar=excluded.name_ar,name_en=excluded.name_en,dimension=excluded.dimension,allow_fraction=excluded.allow_fraction,precision=excluded.precision,status='Active',updated_at=case when (foundation_catalog.uom.code,foundation_catalog.uom.name_ar,foundation_catalog.uom.name_en,foundation_catalog.uom.dimension,foundation_catalog.uom.allow_fraction,foundation_catalog.uom.precision,foundation_catalog.uom.status) is distinct from (excluded.code,excluded.name_ar,excluded.name_en,excluded.dimension,excluded.allow_fraction,excluded.precision,excluded.status) then excluded.updated_at else foundation_catalog.uom.updated_at end,version=foundation_catalog.uom.version+case when (foundation_catalog.uom.code,foundation_catalog.uom.name_ar,foundation_catalog.uom.name_en,foundation_catalog.uom.dimension,foundation_catalog.uom.allow_fraction,foundation_catalog.uom.precision,foundation_catalog.uom.status) is distinct from (excluded.code,excluded.name_ar,excluded.name_en,excluded.dimension,excluded.allow_fraction,excluded.precision,excluded.status) then 1 else 0 end", cancellationToken, context.Tenant.Id, SeedIds.Uom(context.Tenant.Id, uom.Item1), uom.Item1, uom.Item2, uom.Item3, uom.Item4, uom.Item5, uom.Item6, context.AsOf);
    }

    private static async Task UpsertVariantAsync(SeedContext context, Guid productId, int index, string suffix, string? sku, bool isDefault, string status, CancellationToken cancellationToken)
    {
        var variantId = SeedIds.Variant(context.Tenant.Id, index, suffix);
        var normalized = sku is null ? (object)DBNull.Value : NormalizeSku(sku);
        await context.ExecuteAsync("insert into foundation_catalog.product_variant (tenant_id,id,product_template_id,sku,normalized_sku,is_default_variant,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,1,$8,$8) on conflict (tenant_id,id) do update set sku=excluded.sku,normalized_sku=excluded.normalized_sku,is_default_variant=excluded.is_default_variant,status=excluded.status,updated_at=case when (foundation_catalog.product_variant.sku,foundation_catalog.product_variant.normalized_sku,foundation_catalog.product_variant.is_default_variant,foundation_catalog.product_variant.status) is distinct from (excluded.sku,excluded.normalized_sku,excluded.is_default_variant,excluded.status) then excluded.updated_at else foundation_catalog.product_variant.updated_at end,version=foundation_catalog.product_variant.version+case when (foundation_catalog.product_variant.sku,foundation_catalog.product_variant.normalized_sku,foundation_catalog.product_variant.is_default_variant,foundation_catalog.product_variant.status) is distinct from (excluded.sku,excluded.normalized_sku,excluded.is_default_variant,excluded.status) then 1 else 0 end", cancellationToken, context.Tenant.Id, variantId, productId, sku ?? (object)DBNull.Value, normalized, isDefault, status, context.AsOf);
    }

    private static async Task UpsertProductUomAsync(SeedContext context, int productIndex, int ignored, string suffix, string uomCode, decimal factor, bool isBase, CancellationToken cancellationToken)
    {
        var variantId = SeedIds.Variant(context.Tenant.Id, productIndex, suffix);
        await context.ExecuteAsync("insert into foundation_catalog.product_uom (tenant_id,id,variant_id,uom_id,factor_to_base,is_base,effective_from,effective_to,status,version) values ($1,$2,$3,$4,$5,$6,$7,null,'Active',1) on conflict (tenant_id,id) do update set uom_id=excluded.uom_id,factor_to_base=excluded.factor_to_base,is_base=excluded.is_base,effective_from=excluded.effective_from,effective_to=null,status='Active',version=foundation_catalog.product_uom.version+case when (foundation_catalog.product_uom.uom_id,foundation_catalog.product_uom.factor_to_base,foundation_catalog.product_uom.is_base,foundation_catalog.product_uom.effective_from,foundation_catalog.product_uom.effective_to,foundation_catalog.product_uom.status) is distinct from (excluded.uom_id,excluded.factor_to_base,excluded.is_base,excluded.effective_from,excluded.effective_to,excluded.status) then 1 else 0 end", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, $"product-uom-{suffix}", productIndex), variantId, SeedIds.Uom(context.Tenant.Id, uomCode), factor, isBase, context.AsOf);
    }

    private static async Task UpsertAlternateUomAsync(SeedContext context, int productIndex, string uomCode, decimal factor, string recordSuffix, CancellationToken cancellationToken)
    {
        await context.ExecuteAsync("insert into foundation_catalog.product_uom (tenant_id,id,variant_id,uom_id,factor_to_base,is_base,effective_from,effective_to,status,version) values ($1,$2,$3,$4,$5,false,$6,null,'Active',1) on conflict (tenant_id,id) do update set uom_id=excluded.uom_id,factor_to_base=excluded.factor_to_base,effective_from=excluded.effective_from,effective_to=null,status='Active',version=foundation_catalog.product_uom.version+case when (foundation_catalog.product_uom.uom_id,foundation_catalog.product_uom.factor_to_base,foundation_catalog.product_uom.effective_from,foundation_catalog.product_uom.effective_to,foundation_catalog.product_uom.status) is distinct from (excluded.uom_id,excluded.factor_to_base,excluded.effective_from,excluded.effective_to,excluded.status) then 1 else 0 end", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, $"product-uom-{recordSuffix}", productIndex), SeedIds.Variant(context.Tenant.Id, productIndex, "default"), SeedIds.Uom(context.Tenant.Id, uomCode), factor, context.AsOf);
    }

    private static async Task EnsureFashionScenarioAsync(SeedContext context, ProductSpec product, CancellationToken cancellationToken)
    {
        var tenantId = context.Tenant.Id;
        var colorId = SeedIds.Stable($"{tenantId:D}:catalog:attribute:color");
        var sizeId = SeedIds.Stable($"{tenantId:D}:catalog:attribute:size");
        var redId = SeedIds.Stable($"{tenantId:D}:catalog:attribute-value:color:red");
        var blueId = SeedIds.Stable($"{tenantId:D}:catalog:attribute-value:color:blue");
        var smallId = SeedIds.Stable($"{tenantId:D}:catalog:attribute-value:size:small");
        var mediumId = SeedIds.Stable($"{tenantId:D}:catalog:attribute-value:size:medium");
        await context.ExecuteAsync("insert into foundation_catalog.product_attribute (tenant_id,id,code,normalized_code,name_ar,name_en,is_variant_defining,status,version,created_at,updated_at) values ($1,$2,'COLOR','COLOR','اللون','Color',true,'Active',1,$3,$3),($1,$4,'SIZE','SIZE','المقاس','Size',true,'Active',1,$3,$3) on conflict (tenant_id,id) do update set name_ar=excluded.name_ar,name_en=excluded.name_en,is_variant_defining=true,status='Active',updated_at=excluded.updated_at", cancellationToken, tenantId, colorId, context.AsOf, sizeId);
        await context.ExecuteAsync("insert into foundation_catalog.product_attribute_value (tenant_id,id,attribute_id,code,normalized_code,name_ar,name_en,status,version,created_at,updated_at) values ($1,$2,$3,'RED','RED','أحمر','Red','Active',1,$7,$7),($1,$4,$3,'BLUE','BLUE','أزرق','Blue','Active',1,$7,$7),($1,$5,$6,'S','S','صغير','Small','Active',1,$7,$7),($1,$8,$6,'M','M','متوسط','Medium','Active',1,$7,$7) on conflict (tenant_id,id) do update set name_ar=excluded.name_ar,name_en=excluded.name_en,status='Active',updated_at=excluded.updated_at", cancellationToken, tenantId, redId, colorId, blueId, smallId, sizeId, context.AsOf, mediumId);
        await context.ExecuteAsync("insert into foundation_catalog.product_template_attribute (tenant_id,template_id,attribute_id,display_order,is_required) values ($1,$2,$3,0,true),($1,$2,$4,1,true) on conflict (tenant_id,template_id,attribute_id) do update set display_order=excluded.display_order,is_required=true", cancellationToken, tenantId, product.Id, colorId, sizeId);

        var combos = new[]
        {
            ("default", "أحمر / صغير", "RED-S", redId, smallId),
            ("red-medium", "أحمر / متوسط", "RED-M", redId, mediumId),
            ("blue-small", "أزرق / صغير", "BLUE-S", blueId, smallId),
            ("blue-medium", "أزرق / متوسط", "BLUE-M", blueId, mediumId)
        };
        foreach (var combo in combos)
        {
            var variantId = SeedIds.Variant(tenantId, product.Index, combo.Item1);
            var signature = string.Join(';', new[] { (colorId, combo.Item4), (sizeId, combo.Item5) }.OrderBy(pair => pair.Item1).Select(pair => $"{pair.Item1:N}={pair.Item2:N}"));
            var sku = $"FASH-{tenantId.ToString()[..4]}-{combo.Item3}";
            if (combo.Item1 == "default")
            {
                await context.ExecuteAsync("update foundation_catalog.product_variant set sku=$3,normalized_sku=$4,display_suffix=$5,combination_signature=$6 where tenant_id=$1 and id=$2", cancellationToken, tenantId, variantId, sku, NormalizeSku(sku), combo.Item2, signature);
            }
            else
            {
                await context.ExecuteAsync("insert into foundation_catalog.product_variant (tenant_id,id,product_template_id,sku,normalized_sku,is_default_variant,display_suffix,combination_signature,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,false,$6,$7,'Active',1,$8,$8) on conflict (tenant_id,id) do update set sku=excluded.sku,normalized_sku=excluded.normalized_sku,display_suffix=excluded.display_suffix,combination_signature=excluded.combination_signature,status='Active',updated_at=excluded.updated_at", cancellationToken, tenantId, variantId, product.Id, sku, NormalizeSku(sku), combo.Item2, signature, context.AsOf);
                await UpsertProductUomAsync(context, product.Index, product.Index, combo.Item1, "EA", 1m, true, cancellationToken);
            }
            await context.ExecuteAsync("insert into foundation_catalog.product_variant_attribute_value (tenant_id,variant_id,attribute_id,attribute_value_id) values ($1,$2,$3,$4),($1,$2,$5,$6) on conflict (tenant_id,variant_id,attribute_id) do update set attribute_value_id=excluded.attribute_value_id", cancellationToken, tenantId, variantId, colorId, combo.Item4, sizeId, combo.Item5);
            await RefreshProjectionAsync(context, product, variantId, cancellationToken);
        }
    }

    private static async Task RefreshProjectionAsync(SeedContext context, ProductSpec product, Guid variantId, CancellationToken cancellationToken)
    {
        await context.ExecuteAsync("insert into foundation_catalog.catalog_search_projection (tenant_id,variant_id,template_id,display_name,display_name_normalized,name_ar,name_en,sku,normalized_sku,category_id,category_name,base_uom_code,product_nature,status,updated_at) select p.tenant_id,v.id,p.id,p.display_name,p.display_name_normalized,p.name_ar,p.name_en,v.sku,v.normalized_sku,p.category_id,c.name,u.code,p.product_nature,p.status,p.updated_at from foundation_catalog.product_template p join foundation_catalog.product_variant v on v.tenant_id=p.tenant_id and v.product_template_id=p.id left join foundation_catalog.product_category c on c.tenant_id=p.tenant_id and c.id=p.category_id left join foundation_catalog.product_uom pu on pu.tenant_id=v.tenant_id and pu.variant_id=v.id and pu.is_base and pu.status='Active' and pu.effective_to is null left join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id where p.tenant_id=$1 and p.id=$2 and v.id=$3 on conflict (tenant_id,variant_id) do update set template_id=excluded.template_id,display_name=excluded.display_name,display_name_normalized=excluded.display_name_normalized,name_ar=excluded.name_ar,name_en=excluded.name_en,sku=excluded.sku,normalized_sku=excluded.normalized_sku,category_id=excluded.category_id,category_name=excluded.category_name,base_uom_code=excluded.base_uom_code,product_nature=excluded.product_nature,status=excluded.status,updated_at=excluded.updated_at", cancellationToken, context.Tenant.Id, product.Id, variantId);
    }

    private static List<ProductSpec> BuildProducts(SeedTenant tenant, int count)
    {
        var names = new[] { ("ورق تصوير أبيض A4", "A4 White Copy Paper"), ("كابل كهرباء نحاسي 2.5 مم", "Copper Electrical Cable 2.5mm"), ("شريط تغليف شفاف", "Clear Packaging Tape"), ("جهاز قياس رقمي", "Digital Measuring Device"), ("فلتر هواء صناعي", "Industrial Air Filter"), ("خدمة تجهيز الطلبات", "Order Preparation Service") };
        var result = new List<ProductSpec>(count);
        for (var index = 1; index <= count; index++)
        {
            var named = names[(index - 1) % names.Length];
            var fashion = index == 7;
            var nameAr = fashion ? "قميص قطني كلاسيكي" : index <= names.Length ? named.Item1 : $"{named.Item1} — مقاس {index}";
            var nameEn = fashion ? "Classic Cotton Shirt" : index <= names.Length ? named.Item2 : $"{named.Item2} Size {index}";
            result.Add(new(SeedIds.Product(tenant.Id, index), index, index % 17 == 0 ? "خدمة تجهيز الطلبات" : $"{nameAr} / {nameEn}", nameAr, nameEn, index % 17 == 0 ? "Service" : "Goods", fashion ? 0 : index % 6, index % 4, index % 17 == 0 ? null : $"{(index % 2 == 0 ? "AT" : "مخ")}-{index:0000}", index == count));
        }
        return result;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormKD))
        {
            if (char.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark || character == '\u0640') continue;
            var folded = character switch
            {
                '\u0622' or '\u0623' or '\u0625' or '\u0671' => '\u0627',
                '\u0649' => '\u064A',
                '\u0660' or '\u06F0' => '0', '\u0661' or '\u06F1' => '1', '\u0662' or '\u06F2' => '2',
                '\u0663' or '\u06F3' => '3', '\u0664' or '\u06F4' => '4', '\u0665' or '\u06F5' => '5',
                '\u0666' or '\u06F6' => '6', '\u0667' or '\u06F7' => '7', '\u0668' or '\u06F8' => '8',
                '\u0669' or '\u06F9' => '9',
                _ => character
            };
            builder.Append(char.IsLetterOrDigit(folded) ? folded : ' ');
        }
        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormKC).ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeSku(string value) => string.Join(' ', value.Trim().Normalize(NormalizationForm.FormKC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private sealed record ProductSpec(Guid Id, int Index, string DisplayName, string NameAr, string NameEn, string Nature, int CategoryIndex, int BrandIndex, string? Sku, bool Archived)
    {
        public string Status => Archived ? "Archived" : "Active";
    }
}
