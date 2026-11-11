namespace DbMigrator.SeedData;

public static class FixedAssetsSeed
{
    public static async Task<FixedAssetsSeedSummary> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var categories = new[]
        {
            ("LAND", "Land", "Land", "Individual", false, false, false, (decimal?)null, "NoDepreciation", (int?)null, 0m),
            ("BUILDING", "Building", "Building", "Individual", true, false, true, (decimal?)null, "StraightLine", (int?)240, 0m),
            ("LAPTOP", "Laptop", "Equipment", "Individual", false, true, true, (decimal?)null, "StraightLine", (int?)36, 0m),
            ("FURNITURE", "Grouped Furniture", "Furniture", "Grouped", true, false, true, (decimal?)null, "StraightLine", (int?)60, 0m)
        };
        foreach (var category in categories)
        {
            await context.ExecuteAsync("""
                insert into business_assets.asset_category(tenant_id,id,code,name,asset_nature,tracking_granularity,requires_location,requires_custody,requires_depreciation,capitalization_threshold,default_depreciation_method,default_useful_life_months,default_residual_value,created_by,created_at)
                values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)
                on conflict(tenant_id,code) do update set name=excluded.name,asset_nature=excluded.asset_nature,tracking_granularity=excluded.tracking_granularity,requires_location=excluded.requires_location,requires_custody=excluded.requires_custody,requires_depreciation=excluded.requires_depreciation,default_depreciation_method=excluded.default_depreciation_method,default_useful_life_months=excluded.default_useful_life_months,default_residual_value=excluded.default_residual_value
                """, ct, context.Tenant.Id, SeedIds.AssetCategory(context.Tenant.Id, category.Item1), category.Item1, category.Item2, category.Item3, category.Item4, category.Item5, category.Item6, category.Item7, category.Item8, category.Item9, category.Item10, category.Item11, context.AdministratorId, context.AsOf);
        }

        var bookId = SeedIds.AssetBook(context.Tenant.Id, "corporate");
        await context.ExecuteAsync("""
            insert into business_assets.asset_book_definition(tenant_id,id,legal_entity_id,code,name,posts_to_ledger,currency_code,default_depreciation_method,default_convention,default_useful_life_months,created_by,created_at)
            values($1,$2,$3,'CORP','Corporate Book',true,$4,'StraightLine','FullMonth',null,$5,$6)
            on conflict(tenant_id,legal_entity_id,code) do update set name=excluded.name,posts_to_ledger=excluded.posts_to_ledger,currency_code=excluded.currency_code
            """, ct, context.Tenant.Id, bookId, context.EntityId, context.Tenant.BaseCurrency, context.AdministratorId, context.AsOf);

        var landId = SeedIds.Asset(context.Tenant.Id, "land");
        var laptopId = SeedIds.Asset(context.Tenant.Id, "laptop");
        await context.ExecuteAsync("""
            insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,asset_tag,lifecycle_state,asset_nature,tracking_granularity,acquisition_date,capitalization_date,placed_in_service_date,quantity,created_by,created_at,row_version)
            values($1,$2,$3,$4,'FA-SEED-LAND','Atlas Land Parcel','LAND-SEED','Capitalized','Land','Individual',$5,$5,$5,1,$6,$7,1)
            on conflict(tenant_id,asset_number) do update set name=excluded.name,lifecycle_state=excluded.lifecycle_state
            """, ct, context.Tenant.Id, landId, context.EntityId, SeedIds.AssetCategory(context.Tenant.Id, "LAND"), context.AsOf.Date, context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("""
            insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,asset_tag,lifecycle_state,asset_nature,tracking_granularity,product_variant_ref,source_origin_type,acquisition_date,capitalization_date,placed_in_service_date,quantity,created_by,created_at,row_version)
            values($1,$2,$3,$4,'FA-SEED-LAPTOP','Finance Laptop','LAPTOP-SEED','Operational','Equipment','Individual','seed:catalog:laptop','Opening',$5,$5,$5,1,$6,$7,1)
            on conflict(tenant_id,asset_number) do update set name=excluded.name,lifecycle_state=excluded.lifecycle_state,product_variant_ref=excluded.product_variant_ref
            """, ct, context.Tenant.Id, laptopId, context.EntityId, SeedIds.AssetCategory(context.Tenant.Id, "LAPTOP"), context.AsOf.Date, context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("""
            insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version)
            values($1,$2,$3,$4,$5,0,$6,'StraightLine',$7,$8,1)
            on conflict(tenant_id,asset_id,book_definition_id) do update set original_cost=excluded.original_cost,useful_life_months=excluded.useful_life_months
            """, ct, context.Tenant.Id, SeedIds.AssetBook(context.Tenant.Id, "laptop-corporate"), laptopId, bookId, 12000m, 36, context.AsOf.Date, context.AsOf);

        return new(4, 2, 1);
    }
}

public sealed record FixedAssetsSeedSummary(int Categories, int Assets, int Books);
