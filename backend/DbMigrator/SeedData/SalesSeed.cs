using Npgsql;

namespace DbMigrator.SeedData;

public static class SalesSeed
{
    private const int QuoteCount = 80;

    public static async Task<SeedSalesSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var draft = 0; var open = 0; var accepted = 0;
        for (var index = 1; index <= QuoteCount; index++)
        {
            var quoteId = SeedIds.Child(context.Tenant.Id, "sales-quote", index);
            var revisionId = SeedIds.Child(context.Tenant.Id, "sales-quote-revision", index);
            var lineId = SeedIds.Child(context.Tenant.Id, "sales-quote-line", index);
            var date = DateOnly.FromDateTime(context.AsOf.UtcDateTime).AddDays(-(index % 120));
            var state = index % 7 == 0 ? "Accepted" : index % 3 == 0 ? "Open" : "Draft";
            var revisionState = state == "Draft" ? "Draft" : state;
            var partyId = SeedIds.Party(context.Tenant.Id, 1 + 2 * ((index - 1) % 40));
            var number = $"Q-{date.Year}-{index:000000}";
            var unitPrice = 100m + index;
            await context.ExecuteAsync("insert into business_sales.quotation(tenant_id,id,legal_entity_id,branch_id,number,party_id,quote_date,valid_until,currency_code,state,current_revision_number,version,customer_reference,requested_delivery_date,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,1,1,$11,$12,$13,$14,$14) on conflict(tenant_id,id) do update set state=excluded.state,updated_at=excluded.updated_at", cancellationToken,
                context.Tenant.Id, quoteId, context.EntityId, context.BranchId, number, partyId, date, date.AddDays(30), context.Tenant.BaseCurrency, state, $"PO-{index:0000}", date.AddDays(14), context.AdministratorId, context.AsOf);
            await context.ExecuteAsync("insert into business_sales.quotation_revision(tenant_id,id,legal_entity_id,quotation_id,revision_number,status,snapshot,net_total,discount_total,gross_total,created_by,created_at,sealed_at) values($1,$2,$3,$4,1,$5,$6,$7,0,$7,$8,$9,$10) on conflict(tenant_id,id) do update set status=excluded.status,net_total=excluded.net_total,gross_total=excluded.gross_total,sealed_at=excluded.sealed_at", cancellationToken,
                context.Tenant.Id, revisionId, context.EntityId, quoteId, revisionState, Jsonb("{}"), unitPrice * index % 10 + unitPrice, context.AdministratorId, context.AsOf, state == "Draft" ? DBNull.Value : context.AsOf);
            await context.ExecuteAsync("insert into business_sales.quotation_revision_line(tenant_id,id,legal_entity_id,revision_id,sequence,product_variant_id,unit_of_measure_id,uom_code,description,quantity,unit_price,discount_amount,net_amount) values($1,$2,$3,$4,1,$5,$6,'EA',$7,$8,$9,0,$10) on conflict(tenant_id,id) do update set quantity=excluded.quantity,unit_price=excluded.unit_price,net_amount=excluded.net_amount", cancellationToken,
                context.Tenant.Id, lineId, context.EntityId, revisionId, SeedIds.Variant(context.Tenant.Id, 2 + ((index - 1) % 39), "default"), SeedIds.Uom(context.Tenant.Id, "EA"), $"صنف مبيعات تجريبي {index:0000}", index % 10 + 1, unitPrice, unitPrice * (index % 10 + 1));
            await context.ExecuteAsync("insert into business_sales.sales_document_state_history(tenant_id,id,legal_entity_id,document_type,document_id,revision_number,from_state,to_state,actor_id,reason,occurred_at) values($1,$2,$3,'Quotation',$4,1,null,$5,$6,'development_seed',$7) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "sales-quote-history", index), context.EntityId, quoteId, state, context.AdministratorId, context.AsOf);
            if (state == "Accepted")
                await context.ExecuteAsync("insert into business_sales.quotation_acceptance(tenant_id,id,legal_entity_id,quotation_id,revision_number,accepted_at,acceptance_method,recorded_by,note,idempotency_key) values($1,$2,$3,$4,1,$5,'Email',$6,'development_seed',$7) on conflict(tenant_id,quotation_id,revision_number) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "sales-acceptance", index), context.EntityId, quoteId, context.AsOf, context.AdministratorId, $"seed-sales-accept-{index}");
            if (state == "Draft") draft++; else if (state == "Open") open++; else accepted++;
        }

        // Keep one confirmed line in the source module so Manufacturing can prove
        // accepted-demand refresh and line-level pegging against a real owner fact.
        var manufacturingOrderId = SeedIds.Child(context.Tenant.Id, "sales-order-manufacturing", 1);
        var manufacturingLineId = SeedIds.Child(context.Tenant.Id, "sales-order-line-manufacturing", 1);
        var manufacturingParty = SeedIds.Party(context.Tenant.Id, 1);
        var manufacturingProduct = SeedIds.Variant(context.Tenant.Id, 1, "default");
        await context.ExecuteAsync("insert into business_sales.sales_order(tenant_id,id,legal_entity_id,branch_id,number,quotation_id,quotation_revision,party_id,order_date,currency_code,warehouse_id,commercial_state,fulfillment_state,invoicing_state,credit_state,customer_reference,requested_delivery_date,version,created_by,created_at,updated_at) values($1,$2,$3,$4,'SO-MFG-0001',null,null,$5,$6,$7,$8,'Confirmed','Pending','NotInvoiced','Approved','development_seed',$9,1,$10,$11,$11) on conflict(tenant_id,id) do update set commercial_state='Confirmed',fulfillment_state='Pending',requested_delivery_date=excluded.requested_delivery_date,updated_at=excluded.updated_at", cancellationToken,
            context.Tenant.Id, manufacturingOrderId, context.EntityId, context.BranchId, manufacturingParty,
            DateOnly.FromDateTime(context.AsOf.UtcDateTime).AddDays(-2), context.Tenant.BaseCurrency, SeedIds.Stable($"{context.Tenant.Id:D}:inventory:warehouse-main"),
            DateOnly.FromDateTime(context.AsOf.UtcDateTime).AddDays(60), context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into business_sales.sales_order_line(tenant_id,id,legal_entity_id,order_id,sequence,product_variant_id,unit_of_measure_id,description,ordered_quantity,fulfilled_quantity,cancelled_quantity,invoiced_quantity,unit_price) values($1,$2,$3,$4,1,$5,$6,'Manufacturing acceptance finished product',600,0,0,0,100) on conflict(tenant_id,id) do update set ordered_quantity=excluded.ordered_quantity,fulfilled_quantity=0,cancelled_quantity=0", cancellationToken,
            context.Tenant.Id, manufacturingLineId, context.EntityId, manufacturingOrderId, manufacturingProduct, SeedIds.Uom(context.Tenant.Id, "EA"));
        return new(QuoteCount, draft, open, accepted);
    }

    private static NpgsqlParameter Jsonb(string value) => new() { Value = value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb };
}

public sealed record SeedSalesSummary(int Quotations, int Draft, int Open, int Accepted);
