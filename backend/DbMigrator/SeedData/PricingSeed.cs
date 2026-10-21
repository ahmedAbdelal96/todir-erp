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
        var groupId = SeedIds.Child(tenant, "pricing-group-wholesale", 1);
        var variantId = SeedIds.Variant(tenant, 2, "default");
        var eachId = SeedIds.Uom(tenant, "EA");
        var packId = SeedIds.Uom(tenant, "PACK");
        var cartonId = SeedIds.Uom(tenant, "CARTON");
        var customerId = SeedIds.Party(tenant, 2);
        var supplierId = SeedIds.Party(tenant, 3);
        var now = context.AsOf;

        await context.ExecuteAsync("insert into foundation_pricing.price_list(tenant_id,id,legal_entity_id,code,name,direction,currency_code,currency_policy,status,created_at,updated_at,version) values($1,$2,$3,'RETAIL','قائمة البيع العامة','Selling','EGP','ExactCurrency','Active',$4,$4,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, sellingList, context.EntityId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list(tenant_id,id,legal_entity_id,code,name,direction,currency_code,currency_policy,status,created_at,updated_at,version) values($1,$2,$3,'PURCHASE','قائمة الشراء العامة','Buying','EGP','AllowFinBaseConversion','Active',$4,$4,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, buyingList, context.EntityId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list(tenant_id,id,legal_entity_id,code,name,direction,currency_code,currency_policy,status,created_at,updated_at,version) values($1,$2,$3,'WHOLESALE','قائمة البيع بالجملة','Selling','EGP','ExactCurrency','Active',$4,$4,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, wholesaleList, context.EntityId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list(tenant_id,id,legal_entity_id,code,name,direction,currency_code,currency_policy,status,created_at,updated_at,version) values($1,$2,$3,'EXPORT-USD','قائمة بيع بالدولار','Selling','USD','AllowFinBaseConversion','Active',$4,$4,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, usdList, context.EntityId, now);
        await context.ExecuteAsync("insert into foundation_pricing.pricing_policy(tenant_id,legal_entity_id,currency_policy,allow_uom_derived_price,version,updated_at) values($1,$2,'AllowFinBaseConversion',true,1,$3) on conflict(tenant_id,legal_entity_id) do update set currency_policy=excluded.currency_policy,allow_uom_derived_price=excluded.allow_uom_derived_price,updated_at=case when (foundation_pricing.pricing_policy.currency_policy,foundation_pricing.pricing_policy.allow_uom_derived_price) is distinct from (excluded.currency_policy,excluded.allow_uom_derived_price) then excluded.updated_at else foundation_pricing.pricing_policy.updated_at end,version=foundation_pricing.pricing_policy.version+case when (foundation_pricing.pricing_policy.currency_policy,foundation_pricing.pricing_policy.allow_uom_derived_price) is distinct from (excluded.currency_policy,excluded.allow_uom_derived_price) then 1 else 0 end", cancellationToken, tenant, context.EntityId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,activated_at,version) values($1,$2,$3,1,'Draft','2026-01-01',null,$4,$5,$5,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, sellingVersion, sellingList, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,activated_at,version) values($1,$2,$3,1,'Draft','2026-01-01',null,$4,$5,$5,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, buyingVersion, buyingList, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,activated_at,version) values($1,$2,$3,1,'Draft','2026-01-01',null,$4,$5,$5,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, wholesaleVersion, wholesaleList, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at,activated_at,version) values($1,$2,$3,1,'Draft','2026-01-01',null,$4,$5,$5,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, usdVersion, usdList, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_group(tenant_id,id,code,name,status,created_at,updated_at,version) values($1,$2,'WHOLESALE','عملاء الجملة','Active',$3,$3,1) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, groupId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_group_member(tenant_id,id,price_group_id,party_id,valid_from) values($1,$2,$3,$4,'2026-01-01') on conflict(tenant_id,price_group_id,party_id,valid_from) do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-wholesale-member", 1), groupId, customerId);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_assignment(tenant_id,id,legal_entity_id,direction,price_list_id,is_default,valid_from,created_at) values($1,$2,$3,'Selling',$4,true,'2026-01-01',$5) on conflict(tenant_id,legal_entity_id,direction) where is_default do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-list-assignment-selling", 1), context.EntityId, sellingList, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_assignment(tenant_id,id,legal_entity_id,direction,price_list_id,is_default,valid_from,created_at) values($1,$2,$3,'Buying',$4,true,'2026-01-01',$5) on conflict(tenant_id,legal_entity_id,direction) where is_default do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-list-assignment-buying", 1), context.EntityId, buyingList, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_assignment(tenant_id,id,legal_entity_id,direction,price_list_id,party_id,is_default,valid_from,created_at) values($1,$2,$3,'Selling',$4,$5,false,'2026-01-01',$6) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-list-assignment-usd-customer", 1), context.EntityId, usdList, customerId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_assignment(tenant_id,id,legal_entity_id,direction,price_list_id,price_group_id,is_default,valid_from,created_at) values($1,$2,$3,'Selling',$4,$5,false,'2026-01-01',$6) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "pricing-list-assignment-wholesale-group", 1), context.EntityId, wholesaleList, groupId, now);

        await InsertEntry(context, sellingVersion, SeedIds.Child(tenant, "pricing-entry-retail", 1), variantId, eachId, null, null, 1250m, cancellationToken);
        await InsertEntry(context, sellingVersion, SeedIds.Child(tenant, "pricing-entry-customer", 1), variantId, eachId, customerId, null, 1100m, cancellationToken);
        await InsertEntry(context, sellingVersion, SeedIds.Child(tenant, "pricing-entry-pack", 1), variantId, packId, null, null, 700m, cancellationToken);
        await InsertEntry(context, sellingVersion, SeedIds.Child(tenant, "pricing-entry-carton", 1), variantId, cartonId, null, null, 25500m, cancellationToken);
        await InsertEntry(context, sellingVersion, SeedIds.Child(tenant, "pricing-entry-zero", 1), SeedIds.Variant(tenant, 4, "default"), eachId, null, null, 0m, cancellationToken);
        await InsertEntry(context, buyingVersion, SeedIds.Child(tenant, "pricing-entry-supplier", 1), variantId, eachId, supplierId, null, 900m, cancellationToken);
        await InsertEntry(context, wholesaleVersion, SeedIds.Child(tenant, "pricing-entry-wholesale", 1), variantId, eachId, null, null, 1125m, cancellationToken);
        await InsertEntry(context, wholesaleVersion, SeedIds.Child(tenant, "pricing-entry-wholesale-tier", 1), variantId, eachId, null, null, 1050m, cancellationToken, minQuantity: 12m);
        await InsertEntry(context, usdVersion, SeedIds.Child(tenant, "pricing-entry-usd", 1), variantId, eachId, customerId, null, 25m, cancellationToken);
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Active' where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, tenant, sellingVersion);
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Active' where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, tenant, buyingVersion);
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Active' where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, tenant, wholesaleVersion);
        await context.ExecuteAsync("update foundation_pricing.price_list_version set status='Active' where tenant_id=$1 and id=$2 and status='Draft'", cancellationToken, tenant, usdVersion);

        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at) values($1,$2,$3,2,'Scheduled','2027-01-01',null,$4,$5) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, futureVersion, sellingList, context.AdministratorId, now);
        await context.ExecuteAsync("insert into foundation_pricing.price_list_version(tenant_id,id,price_list_id,version_no,status,effective_from,effective_to,created_by,created_at) values($1,$2,$3,3,'Draft','2027-06-01',null,$4,$5) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, draftVersion, sellingList, context.AdministratorId, now);
        var lists = Convert.ToInt32(await context.ScalarAsync<long>("select count(*) from foundation_pricing.price_list where tenant_id=$1 and id in ($2,$3,$4,$5)", cancellationToken, tenant, sellingList, buyingList, wholesaleList, usdList), System.Globalization.CultureInfo.InvariantCulture);
        return new(lists, 9, 1, 1);
    }

    private static async Task InsertEntry(SeedContext context, Guid versionId, Guid entryId, Guid variantId, Guid uomId, Guid? partyId, Guid? groupId, decimal price, CancellationToken cancellationToken, decimal minQuantity = 0m)
    {
        await context.ExecuteAsync("insert into foundation_pricing.price_entry(tenant_id,id,price_list_version_id,variant_id,uom_id,party_id,price_group_id,min_quantity,max_quantity,unit_price,valid_from,priority,status,version,created_at) select $1,$2,$3,$4,$5,$6,$7,$8,null,$9,'2026-01-01',0,'Active',1,$10 where not exists(select 1 from foundation_pricing.price_entry where tenant_id=$1 and id=$2)", cancellationToken, context.Tenant.Id, entryId, versionId, variantId, uomId, partyId ?? (object)DBNull.Value, groupId ?? (object)DBNull.Value, minQuantity, price, context.AsOf);
    }
}

public sealed record SeedPricingSummary(int PriceLists, int Entries, int ExplicitZeroPrices, int ScheduledVersions);
