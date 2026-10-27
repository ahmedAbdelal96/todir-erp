namespace DbMigrator.SeedData;

public static class InvoicingSeed
{
    private const int DraftCount = 120;

    public static async Task<SeedInvoiceSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var customers = 0;
        var suppliers = 0;
        for (var index = 1; index <= DraftCount; index++)
        {
            var receivable = index % 2 == 1;
            var direction = receivable ? "Receivable" : "Payable";
            var partyIndex = receivable ? 1 + 2 * ((index / 2) % 40) : 2 + 2 * ((index / 2) % 40);
            var partyId = SeedIds.Party(context.Tenant.Id, partyIndex);
            var invoiceId = SeedIds.Child(context.Tenant.Id, "invoice", index);
            var lineId = SeedIds.Child(context.Tenant.Id, "invoice-line", index);
            var date = DateOnly.FromDateTime(context.AsOf.UtcDateTime).AddDays(-((index * 7) % 240));
            var currency = context.Tenant.BaseCurrency;
            var supplierNumber = receivable ? null : $"SUP-{context.Tenant.Slug.ToUpperInvariant()}-{index:0000}";
            var description = receivable ? $"توريد أصناف وتجهيزات رقم {index:0000}" : $"فاتورة شراء مستلزمات تشغيل رقم {index:0000}";
            await context.ExecuteAsync("insert into business_invoicing.invoice(tenant_id,legal_entity_id,id,branch_id,direction,document_type,lifecycle_state,party_id,document_date,posting_date,tax_date,currency_code,supplier_invoice_number,supplier_invoice_number_key,row_version,created_at,updated_at) values($1,$2,$3,$4,$5,'Invoice','Draft',$6,$7,$7,$7,$8,$9,$10,1,$11,$11) on conflict(tenant_id,id) do update set party_id=excluded.party_id,document_date=excluded.document_date,posting_date=excluded.posting_date,tax_date=excluded.tax_date,currency_code=excluded.currency_code,supplier_invoice_number=excluded.supplier_invoice_number,supplier_invoice_number_key=excluded.supplier_invoice_number_key,updated_at=excluded.updated_at", cancellationToken,
                context.Tenant.Id, context.EntityId, invoiceId, context.BranchId, direction, partyId, date, currency,
                supplierNumber ?? (object)DBNull.Value, supplierNumber?.ToUpperInvariant() ?? (object)DBNull.Value, context.AsOf);
            var lineCount = index % 3 == 0 ? 1 : 2;
            for (var sequence = 1; sequence <= lineCount; sequence++)
            {
                var productIndex = 2 + ((index + sequence - 2) % 39);
                var unitCode = UnitCode(productIndex);
                var currentLineId = sequence == 1 ? lineId : SeedIds.Child(context.Tenant.Id, "invoice-line-extra", index);
                await context.ExecuteAsync("insert into business_invoicing.invoice_line(tenant_id,legal_entity_id,invoice_id,id,sequence,product_variant_id,unit_of_measure_id,unit_code_snapshot,description,quantity,line_discount) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11) on conflict(tenant_id,invoice_id,id) do update set sequence=excluded.sequence,product_variant_id=excluded.product_variant_id,unit_of_measure_id=excluded.unit_of_measure_id,unit_code_snapshot=excluded.unit_code_snapshot,description=excluded.description,quantity=excluded.quantity,line_discount=excluded.line_discount", cancellationToken,
                    context.Tenant.Id, context.EntityId, invoiceId, currentLineId, sequence, SeedIds.Variant(context.Tenant.Id, productIndex, "default"), SeedIds.Uom(context.Tenant.Id, unitCode), unitCode, sequence == 1 ? description : "مستلزمات تشغيل إضافية", sequence + index % 4, index % 4 == 0 ? 25m : 0m);
            }
            var partyName = await context.ScalarAsync<string>("select display_name from foundation_directory.party where tenant_id=$1 and id=$2", cancellationToken, context.Tenant.Id, partyId);
            var settlement = "Open";
            var regulatory = index % 3 == 0 ? "ReviewRequired" : "Pending";
            await context.ExecuteAsync("insert into business_invoicing.invoice_list_projection(tenant_id,legal_entity_id,invoice_id,party_display_name,settlement_summary,regulatory_summary,due_date,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8) on conflict(tenant_id,invoice_id) do update set party_display_name=excluded.party_display_name,settlement_summary=excluded.settlement_summary,regulatory_summary=excluded.regulatory_summary,due_date=excluded.due_date,updated_at=excluded.updated_at", cancellationToken,
                context.Tenant.Id, context.EntityId, invoiceId, partyName, settlement, regulatory, date.AddDays(30), context.AsOf);
            if (receivable) customers++; else suppliers++;
        }
        return new(DraftCount, customers, suppliers);
    }

    private static string UnitCode(int productIndex) => productIndex % 17 == 0 ? "HOUR" : productIndex % 2 == 0 ? "EA" : "KG";
}

public sealed record SeedInvoiceSummary(int Drafts, int CustomerDrafts, int SupplierDrafts);
