namespace DbMigrator.SeedData;

public static class PurchasingSeed
{
    public static async Task<SeedPurchasingSummary> SeedAsync(SeedContext context, CancellationToken ct)
    {
        await context.ExecuteAsync("insert into business_purchasing.purchase_settings(tenant_id,legal_entity_id,approval_required,approval_threshold,over_receipt_policy,over_receipt_tolerance,invoice_without_receipt,three_way_matching,buyer_approver_separation,updated_by,updated_at) values($1,$2,false,null,'Tolerance',0.05,true,false,true,$3,$4) on conflict(tenant_id,legal_entity_id) do update set approval_required=excluded.approval_required,over_receipt_policy=excluded.over_receipt_policy,over_receipt_tolerance=excluded.over_receipt_tolerance,updated_by=excluded.updated_by,updated_at=excluded.updated_at", ct, context.Tenant.Id, context.EntityId, context.AdministratorId, context.AsOf);
        var count = context.Tenant.Id == SeedIds.AtlasTenant ? 12 : 8;
        for (var index = 1; index <= count; index++)
        {
            var orderId = SeedIds.Child(context.Tenant.Id, "purchase-order", index);
            var lineId = SeedIds.Child(context.Tenant.Id, "purchase-order-line", index);
            var supplier = SeedIds.Party(context.Tenant.Id, index % 5 == 0 ? 5 : 3);
            var state = index % 5 == 0 ? "Approved" : index % 3 == 0 ? "PendingApproval" : "Draft";
            var approval = state == "PendingApproval" ? "Pending" : state == "Approved" ? "Approved" : "NotRequired";
            var quantity = index + 1m;
            var price = 75m + index;
            var date = DateOnly.FromDateTime(context.AsOf.UtcDateTime).AddDays(-index);
            await context.ExecuteAsync("insert into business_purchasing.supplier_purchase_profile(tenant_id,legal_entity_id,supplier_party_id,supplier_code,default_currency_code,status,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,'Active',$6,$7,$7) on conflict(tenant_id,legal_entity_id,supplier_party_id) do update set status='Active',updated_at=excluded.updated_at", ct, context.Tenant.Id, context.EntityId, supplier, $"SUP-{index:000}", context.Tenant.BaseCurrency, context.AdministratorId, context.AsOf);
            await context.ExecuteAsync("insert into business_purchasing.purchase_order(tenant_id,legal_entity_id,id,branch_id,supplier_party_id,currency_code,order_date,state,approval_state,receipt_state,invoicing_state,matching_state,net_total,gross_total,created_by,approved_by,approved_at,row_version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'NotReceived','NotInvoiced','NotRequired',$10,$10,$11,$12,$13,1,$13,$13) on conflict(tenant_id,id) do update set state=excluded.state,approval_state=excluded.approval_state,net_total=excluded.net_total,gross_total=excluded.gross_total,updated_at=excluded.updated_at", ct, context.Tenant.Id, context.EntityId, orderId, context.BranchId, supplier, context.Tenant.BaseCurrency, date, state, approval, quantity * price, context.AdministratorId, state == "Approved" ? context.AdministratorId : (object)DBNull.Value, state == "Approved" ? context.AsOf : (object)DBNull.Value);
            await context.ExecuteAsync("insert into business_purchasing.purchase_order_line(tenant_id,legal_entity_id,purchase_order_id,id,sequence,product_variant_id,unit_of_measure_id,unit_code,description,ordered_quantity,unit_price,price_evidence,created_at) values($1,$2,$3,$4,1,$5,$6,'EA',$7,$8,$9,$10::jsonb,$11) on conflict(tenant_id,purchase_order_id,id) do update set ordered_quantity=excluded.ordered_quantity,unit_price=excluded.unit_price,price_evidence=excluded.price_evidence", ct, context.Tenant.Id, context.EntityId, orderId, lineId, SeedIds.Variant(context.Tenant.Id, 2 + ((index - 1) % 39), "default"), SeedIds.Uom(context.Tenant.Id, "EA"), $"صنف مشتريات تجريبي {index:0000}", quantity, price, "{\"source\":\"development-seed\"}", context.AsOf);
            await context.ExecuteAsync("insert into business_purchasing.purchase_order_state_history(tenant_id,legal_entity_id,purchase_order_id,id,from_state,to_state,reason,actor_id,correlation_id,created_at) values($1,$2,$3,$4,null,$5,'development_seed',$6,$7,$8) on conflict(tenant_id,purchase_order_id,id) do nothing", ct, context.Tenant.Id, context.EntityId, orderId, SeedIds.Child(context.Tenant.Id, "purchase-history", index), state, context.AdministratorId, SeedIds.Child(context.Tenant.Id, "purchase-correlation", index), context.AsOf);
            if (state == "Approved") await context.ExecuteAsync("insert into business_purchasing.purchase_order_approval(tenant_id,legal_entity_id,purchase_order_id,id,decision,actor_id,reason,idempotency_key,created_at) values($1,$2,$3,$4,'Approved',$5,'development_seed',$6,$7) on conflict(tenant_id,purchase_order_id,idempotency_key) do nothing", ct, context.Tenant.Id, context.EntityId, orderId, SeedIds.Child(context.Tenant.Id, "purchase-approval", index), context.AdministratorId, $"seed-purchase-approve-{index}", context.AsOf);
        }
        return new(count, count / 3, count / 5);
    }
}

public sealed record SeedPurchasingSummary(int PurchaseOrders, int PendingApproval, int Approved);
