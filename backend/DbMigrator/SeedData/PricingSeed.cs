namespace DbMigrator.SeedData;

public static class PricingSeed
{
    public static async Task<SeedPricingSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenant = context.Tenant.Id;
        var sellingList = SeedIds.Child(tenant, "pricing-list-selling-default", 1);
        var buyingList = SeedIds.Child(tenant, "pricing-list-buying-default", 1);
        var wholesaleList = SeedIds.Child(tenant, "pricing-list-selling-wholesale", 1);
        var usdList = SeedIds.Child(tenant, "pricing-list-selling-usd", 1);
        var sellingVersion = SeedIds.Child(tenant, "pricing-version-selling-default", 1);
        var buyingVersion = SeedIds.Child(tenant, "pricing-version-buying-default", 1);
        var wholesaleVersion = SeedIds.Child(tenant, "pricing-version-selling-wholesale", 1);
        var usdVersion = SeedIds.Child(tenant, "pricing-version-selling-usd", 1);
        var futureVersion = SeedIds.Child(tenant, "pricing-version-selling-scheduled", 1);
        var draftVersion = SeedIds.Child(tenant, "pricing-version-selling-draft", 1);
        var secondaryList = SeedIds.Child(tenant, "pricing-list-selling-secondary", 1);
        var secondaryVersion = SeedIds.Child(tenant, "pricing-version-selling-secondary", 1);
        var secondaryBuyingList = SeedIds.Child(tenant, "pricing-list-buying-secondary", 1);
        var secondaryBuyingVersion = SeedIds.Child(tenant, "pricing-version-buying-secondary", 1);
        var catalogRetailVersion = SeedIds.Child(tenant, "pricing-version-retail-catalog", 1);
        var catalogWholesaleVersion = SeedIds.Child(tenant, "pricing-version-wholesale-catalog", 1);
        var catalogBuyingVersion = SeedIds.Child(tenant, "pricing-version-buying-catalog", 1);
        var catalogUsdVersion = SeedIds.Child(tenant, "pricing-version-usd-catalog", 1);
        var groupId = SeedIds.Child(tenant, "pricing-group-wholesale", 1);
        var variantId = SeedIds.Variant(tenant, 2, "default");
        var eachId = SeedIds.Uom(tenant, "EA");
        var packId = SeedIds.Uom(tenant, "PACK");
        var cartonId = SeedIds.Uom(tenant, "CARTON");
        var customerId = SeedIds.Party(tenant, 2);
        var supplierId = SeedIds.Party(tenant, 2);
        var now = context.AsOf;

        await context.ExecuteAsync("insert into foundation_pricing.price_list(tenant_id,id,legal_entity_id,code,name,direction,currency_code,currency_policy,status,created_at,updated_at,version) values($1,$2,$3,'RETAIL','قائمة البيع العامة','Selling','EGP','ExactCurrency','Active',$4,$4,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, sellingList, context.EntityId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list(tenant_id,id,legal_entity_id,code,name,direction,currency_code,currency_policy,status,created_at,updated_at,version) values($1,$2,$3,'PURCHASE','قائمة الشراء العامة','Buying','EGP','AllowFinBaseConversion','Active',$4,$4,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, buyingList, context.EntityId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list(tenant_id,id,legal_entity_id,code,name,direction,currency_code,currency_policy,status,created_at,updated_at,version) values($1,$2,$3,'WHOLESALE','قائمة البيع بالجملة','Selling','EGP','ExactCurrency','Active',$4,$4,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, wholesaleList, context.EntityId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list(tenant_id,id,legal_entity_id,code,name,direction,currency_code,currency_policy,status,created_at,updated_at,version) values($1,$2,$3,'EXPORT-USD','قائمة بيع بالدولار','Selling','USD','AllowFinBaseConversion','Active',$4,$4,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, usdList, context.EntityId, now);
        var secondaryEntityId = SeedIds.TenantSecondaryEntity(tenant);
        await context.ExecuteAsync("insert into foundation_pricing.price_list(tenant_id,id,legal_entity_id,code,name,direction,currency_code,currency_policy,status,created_at,updated_at,version) values($1,$2,$3,'RETAIL-SECONDARY','قائمة البيع للمنشأة الثانية','Selling','EGP','ExactCurrency','Active',$4,$4,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, secondaryList, secondaryEntityId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list(tenant_id,id,legal_entity_id,code,name,direction,currency_code,currency_policy,status,created_at,updated_at,version) values($1,$2,$3,'PURCHASE-SECONDARY','قائمة الشراء للمنشأة الثانية','Buying','EGP','AllowFinBaseConversion','Active',$4,$4,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, secondaryBuyingList, secondaryEntityId, now);
        await context.ExecuteAsync("insert into foundation_pricing.pricing_policy(tenant_id,legal_entity_id,currency_policy,allow_uom_derived_price,version,updated_at) values($1,$2,'AllowFinBaseConversion',true,1,$3) on conflict(tenant_id,legal_entity_id) do update set currency_policy=excluded.currency_policy,allow_uom_derived_price=excluded.allow_uom_derived_price,updated_at=case when (foundation_pricing.pricing_policy.currency_policy,foundation_pricing.pricing_policy.allow_uom_derived_price) is distinct from (excluded.currency_policy,excluded.allow_uom_derived_price) then excluded.updated_at else foundation_pricing.pricing_policy.updated_at end,version=foundation_pricing.pricing_policy.version+case when (foundation_pricing.pricing_policy.currency_policy,foundation_pricing.pricing_policy.allow_uom_derived_price) is distinct from (excluded.currency_policy,excluded.allow_uom_derived_price) then 1 else 0 end", cancellationToken, tenant, context.EntityId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,activated_at,version) values($1,$2,$3,1,'Draft','2026-01-01',null,$4,$5,$5,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, sellingVersion, sellingList, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,activated_at,version) values($1,$2,$3,1,'Draft','2026-01-01',null,$4,$5,$5,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, buyingVersion, buyingList, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,activated_at,version) values($1,$2,$3,1,'Draft','2026-01-01',null,$4,$5,$5,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, wholesaleVersion, wholesaleList, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,activated_at,version) values($1,$2,$3,1,'Draft','2026-01-01',null,$4,$5,$5,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, usdVersion, usdList, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,activated_at,version) values($1,$2,$3,1,'Draft','2026-01-01',null,$4,$5,$5,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, secondaryVersion, secondaryList, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,activated_at,version) values($1,$2,$3,1,'Draft','2026-01-01',null,$4,$5,$5,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, secondaryBuyingVersion, secondaryBuyingList, context.AdministratorId, now);
        var currentEffectiveDate = DateOnly.FromDateTime(context.AsOf.UtcDateTime);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,version) values($1,$2,$3,4,'Draft',$4,null,$5,$6,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, catalogRetailVersion, sellingList, currentEffectiveDate, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,version) values($1,$2,$3,2,'Draft',$4,null,$5,$6,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, catalogWholesaleVersion, wholesaleList, currentEffectiveDate, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,version) values($1,$2,$3,2,'Draft',$4,null,$5,$6,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, catalogBuyingVersion, buyingList, currentEffectiveDate, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,version) values($1,$2,$3,2,'Draft',$4,null,$5,$6,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, catalogUsdVersion, usdList, currentEffectiveDate, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_group(tenant_id,id,code,name,status,created_at,updated_at,version) values($1,$2,'WHOLESALE','عملاء الجملة','Active',$3,$3,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, groupId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_group_member(tenant_id,id,price_group_id,party_id,valid_from) values($1,$2,$3,$4,'2026-01-01') on conflict(tenant_id,id) do update set price_group_id=excluded.price_group_id,party_id=excluded.party_id,valid_from=excluded.valid_from", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-wholesale-member", 1), groupId, customerId);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_assignment(tenant_id,id,legal_entity_id,direction,price_list_id,is_default,valid_from,created_at) values($1,$2,$3,'Selling',$4,true,'2026-01-01',$5) on conflict(tenant_id,legal_entity_id,direction) where is_default do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-list-assignment-selling", 1), context.EntityId, sellingList, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_assignment(tenant_id,id,legal_entity_id,direction,price_list_id,is_default,valid_from,created_at) values($1,$2,$3,'Buying',$4,true,'2026-01-01',$5) on conflict(tenant_id,legal_entity_id,direction) where is_default do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-list-assignment-buying", 1), context.EntityId, buyingList, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_assignment(tenant_id,id,legal_entity_id,direction,price_list_id,party_id,is_default,valid_from,created_at) values($1,$2,$3,'Selling',$4,$5,false,'2026-01-01',$6) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-list-assignment-usd-customer", 1), context.EntityId, usdList, customerId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_assignment(tenant_id,id,legal_entity_id,direction,price_list_id,price_group_id,is_default,valid_from,created_at) values($1,$2,$3,'Selling',$4,$5,false,'2026-01-01',$6) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-list-assignment-wholesale-group", 1), context.EntityId, wholesaleList, groupId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_assignment(tenant_id,id,legal_entity_id,direction,price_list_id,is_default,valid_from,created_at) values($1,$2,$3,'Selling',$4,true,'2026-01-01',$5) on conflict(tenant_id,legal_entity_id,direction) where is_default do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-list-assignment-selling-secondary", 1), secondaryEntityId, secondaryList, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_assignment(tenant_id,id,legal_entity_id,direction,price_list_id,is_default,valid_from,created_at) values($1,$2,$3,'Buying',$4,true,'2026-01-01',$5) on conflict(tenant_id,legal_entity_id,direction) where is_default do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-list-assignment-buying-secondary", 1), secondaryEntityId, secondaryBuyingList, now);

        await InsertEntry(context, sellingVersion, SeedIds.Child(tenant, "pricing-entry-retail", 1), variantId, eachId, null, null, 1250m, cancellationToken);
        await InsertEntry(context, sellingVersion, SeedIds.Child(tenant, "pricing-entry-customer", 1), variantId, eachId, customerId, null, 1100m, cancellationToken);
        await InsertEntry(context, sellingVersion, SeedIds.Child(tenant, "pricing-entry-pack", 1), variantId, packId, null, null, 700m, cancellationToken);
        await InsertEntry(context, sellingVersion, SeedIds.Child(tenant, "pricing-entry-carton", 1), variantId, cartonId, null, null, 25500m, cancellationToken);
        await InsertEntry(context, sellingVersion, SeedIds.Child(tenant, "pricing-entry-zero", 1), SeedIds.Variant(tenant, 4, "default"), eachId, null, null, 0m, cancellationToken);
        await InsertEntry(context, buyingVersion, SeedIds.Child(tenant, "pricing-entry-supplier", 1), variantId, eachId, supplierId, null, 900m, cancellationToken);
        await InsertEntry(context, wholesaleVersion, SeedIds.Child(tenant, "pricing-entry-wholesale", 1), variantId, eachId, null, null, 1125m, cancellationToken);
        await InsertEntry(context, wholesaleVersion, SeedIds.Child(tenant, "pricing-entry-wholesale-tier", 1), variantId, eachId, null, null, 1050m, cancellationToken, minQuantity: 12m);
        await InsertEntry(context, usdVersion, SeedIds.Child(tenant, "pricing-entry-usd", 1), variantId, eachId, customerId, null, 25m, cancellationToken);
        await InsertEntry(context, secondaryVersion, SeedIds.Child(tenant, "pricing-entry-secondary-retail", 1), variantId, eachId, null, null, 1450m, cancellationToken);
        await InsertEntry(context, secondaryBuyingVersion, SeedIds.Child(tenant, "pricing-entry-secondary-supplier", 1), variantId, eachId, supplierId, null, 900m, cancellationToken);

        const int retailProducts = 40;
        const int wholesaleProducts = 40;
        const int supplierProducts = 30;
        const int exportProducts = 20;
        if (await VersionStatusAsync(context, catalogRetailVersion, cancellationToken) == "Draft")
        {
            await InsertEntry(context, catalogRetailVersion, SeedIds.Child(tenant, "pricing-entry-retail-current", 1), variantId, eachId, null, null, 1250m, cancellationToken);
            await InsertEntry(context, catalogRetailVersion, SeedIds.Child(tenant, "pricing-entry-customer-current", 1), variantId, eachId, customerId, null, 1100m, cancellationToken);
            await InsertEntry(context, catalogRetailVersion, SeedIds.Child(tenant, "pricing-entry-pack-current", 1), variantId, packId, null, null, 700m, cancellationToken);
            await InsertEntry(context, catalogRetailVersion, SeedIds.Child(tenant, "pricing-entry-carton-current", 1), variantId, cartonId, null, null, 25500m, cancellationToken);
            await InsertEntry(context, catalogRetailVersion, SeedIds.Child(tenant, "pricing-entry-zero-current", 1), SeedIds.Variant(tenant, 4, "default"), eachId, null, null, 0m, cancellationToken);
            for (var productIndex = 1; productIndex <= retailProducts; productIndex++)
            {
                Guid? priceParty = productIndex == 4 ? customerId : null;
                var minimumQuantity = productIndex == 2 ? 24m : 0m;
                await InsertEntry(context, catalogRetailVersion, SeedIds.Child(tenant, "pricing-entry-retail-catalog", productIndex), SeedIds.Variant(tenant, productIndex, "default"), SeedIds.Uom(tenant, UnitCode(productIndex)), priceParty, null, 250m + productIndex * 37.5m, cancellationToken, minimumQuantity);
            }
        }
        if (await VersionStatusAsync(context, catalogWholesaleVersion, cancellationToken) == "Draft")
        {
            await InsertEntry(context, catalogWholesaleVersion, SeedIds.Child(tenant, "pricing-entry-wholesale-current", 1), variantId, eachId, null, null, 1125m, cancellationToken);
            await InsertEntry(context, catalogWholesaleVersion, SeedIds.Child(tenant, "pricing-entry-wholesale-tier-current", 1), variantId, eachId, null, null, 1050m, cancellationToken, minQuantity: 12m);
            for (var productIndex = 1; productIndex <= wholesaleProducts; productIndex++)
            {
                var minimumQuantity = productIndex == 2 ? 24m : productIndex % 5 == 0 ? 10m : 0m;
                Guid? groupIdForEntry = productIndex % 5 == 0 ? groupId : null;
                await InsertEntry(context, catalogWholesaleVersion, SeedIds.Child(tenant, "pricing-entry-wholesale-catalog", productIndex), SeedIds.Variant(tenant, productIndex, "default"), SeedIds.Uom(tenant, UnitCode(productIndex)), null, groupIdForEntry, 225m + productIndex * 31m, cancellationToken, minimumQuantity);
            }
        }
        if (await VersionStatusAsync(context, catalogBuyingVersion, cancellationToken) == "Draft")
        {
            await InsertEntry(context, catalogBuyingVersion, SeedIds.Child(tenant, "pricing-entry-supplier-current", 1), variantId, eachId, supplierId, null, 900m, cancellationToken);
            for (var productIndex = 1; productIndex <= supplierProducts; productIndex++)
                await InsertEntry(context, catalogBuyingVersion, SeedIds.Child(tenant, "pricing-entry-buying-catalog", productIndex), SeedIds.Variant(tenant, productIndex, "default"), SeedIds.Uom(tenant, UnitCode(productIndex)), null, null, 175m + productIndex * 24m, cancellationToken);
        }
        if (await VersionStatusAsync(context, catalogUsdVersion, cancellationToken) == "Draft")
        {
            await InsertEntry(context, catalogUsdVersion, SeedIds.Child(tenant, "pricing-entry-usd-current", 1), variantId, eachId, customerId, null, 25m, cancellationToken);
            for (var productIndex = 1; productIndex <= exportProducts; productIndex++)
                await InsertEntry(context, catalogUsdVersion, SeedIds.Child(tenant, "pricing-entry-export-catalog", productIndex), SeedIds.Variant(tenant, productIndex, "default"), SeedIds.Uom(tenant, UnitCode(productIndex)), null, null, 8m + productIndex * 2.5m, cancellationToken);
        }
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Active' where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, tenant, sellingVersion);
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Active' where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, tenant, buyingVersion);
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Active' where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, tenant, wholesaleVersion);
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Active' where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, tenant, usdVersion);
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Active' where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, tenant, secondaryVersion);
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Active' where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, tenant, secondaryBuyingVersion);
        await ActivateCatalogVersionAsync(context, sellingList, catalogRetailVersion, cancellationToken);
        await ActivateCatalogVersionAsync(context, wholesaleList, catalogWholesaleVersion, cancellationToken);
        await ActivateCatalogVersionAsync(context, buyingList, catalogBuyingVersion, cancellationToken);
        await ActivateCatalogVersionAsync(context, usdList, catalogUsdVersion, cancellationToken);

        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at) values($1,$2,$3,2,'Draft','2027-01-01',null,$4,$5) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, futureVersion, sellingList, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at) values($1,$2,$3,3,'Draft','2027-06-01',null,$4,$5) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, draftVersion, sellingList, context.AdministratorId, now);
        await InsertEntry(context, futureVersion, SeedIds.Child(tenant, "pricing-entry-scheduled-retail", 1), variantId, eachId, null, null, 1350m, cancellationToken, validFrom: new DateOnly(2027, 1, 1));
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Scheduled' where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, tenant, futureVersion);
        var lists = Convert.ToInt32(await context.ScalarAsync<long>("select count(*) from foundation_pricing.price_list where tenant_id=$1 and id in ($2,$3,$4,$5,$6,$7)", cancellationToken, tenant, sellingList, buyingList, wholesaleList, usdList, secondaryList, secondaryBuyingList), System.Globalization.CultureInfo.InvariantCulture);
        return new(lists, 21 + retailProducts + wholesaleProducts + supplierProducts + exportProducts, 1, 1);
    }

    private static Task<string> VersionStatusAsync(SeedContext context, Guid versionId, CancellationToken cancellationToken) =>
        context.ScalarAsync<string>("select status from foundation_pricing.price_list_version where tenant_id=$1 and id=$2", cancellationToken, context.Tenant.Id, versionId);

    private static async Task ActivateCatalogVersionAsync(SeedContext context, Guid priceListId, Guid versionId, CancellationToken cancellationToken)
    {
        if (await VersionStatusAsync(context, versionId, cancellationToken) != "Draft") return;
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Expired',version=version+1 where tenant_id=$1 and price_list_id=$2 and status='Active' and id<>$3", cancellationToken, context.Tenant.Id, priceListId, versionId);
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Active',activated_at=$3,version=version+1 where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, context.Tenant.Id, versionId, context.AsOf);
    }

    private static string UnitCode(int productIndex) => productIndex == 1 ? "PACK" : productIndex % 17 == 0 ? "HOUR" : productIndex % 2 == 0 ? "EA" : "KG";

    private static async Task InsertEntry(SeedContext context, Guid versionId, Guid entryId, Guid variantId, Guid uomId, Guid? partyId, Guid? groupId, decimal price, CancellationToken cancellationToken, decimal minQuantity = 0m, DateOnly? validFrom = null)
    {
        await context.ExecuteAsync("insert into foundation_pricing.price_entry(tenant_id,id,price_list_version_id,variant_id,uom_id,party_id,price_group_id,min_quantity,max_quantity,unit_price,valid_from,priority,status,version,created_at) select $1,$2,$3,$4,$5,$6,$7,$8,null,$9,$10,0,'Active',1,$11 where not exists(select 1 from foundation_pricing.price_entry where tenant_id=$1 and id=$2)", cancellationToken, context.Tenant.Id, entryId, versionId, variantId, uomId, partyId ?? (object)DBNull.Value, groupId ?? (object)DBNull.Value, minQuantity, price, validFrom ?? new DateOnly(2026, 1, 1), context.AsOf);
    }
}

public sealed record SeedPricingSummary(int PriceLists, int Entries, int ExplicitZeroPrices, int ScheduledVersions);
