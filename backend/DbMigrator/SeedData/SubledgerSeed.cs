using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DbMigrator.SeedData;

public static class SubledgerSeed
{
    public static async Task<SeedSubledgerSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var customerId = SeedIds.Party(context.Tenant.Id, 1);
        var supplierId = SeedIds.Party(context.Tenant.Id, 2);
        var termId = await context.ScalarAsync<Guid>("select id from foundation_finbase.payment_term where tenant_id=$1 and code='INSTALL3' and status='Active'", cancellationToken, context.Tenant.Id);
        var termVersionId = await context.ScalarAsync<Guid>("select id from foundation_finbase.payment_term_version where tenant_id=$1 and payment_term_id=$2 and status='Active' order by version_number desc limit 1", cancellationToken, context.Tenant.Id, termId);
        var ledgerConfig = await context.ScalarAsync<Guid>("select active_configuration_version_id from finance_ledger.ledger_entity_configuration where tenant_id=$1 and legal_entity_id=$2", cancellationToken, context.Tenant.Id, context.EntityId);
        var usdRateId = await context.ScalarAsync<Guid>("select id from foundation_finbase.exchange_rate where tenant_id=$1 and legal_entity_id=$2 and from_currency='USD' and to_currency='EGP' and purpose='Accounting' and status='Approved' order by effective_date desc limit 1", cancellationToken, context.Tenant.Id, context.EntityId);
        var usdRate = await context.ScalarAsync<decimal>("select rate from foundation_finbase.exchange_rate where tenant_id=$1 and id=$2", cancellationToken, context.Tenant.Id, usdRateId);
        var usdSnapshot = JsonSerializer.Serialize(new { TenantId = context.Tenant.Id, LegalEntityId = context.EntityId, ApprovedRateId = usdRateId, FromCurrency = "USD", ToCurrency = "EGP", Rate = usdRate.ToString(System.Globalization.CultureInfo.InvariantCulture), Purpose = "Accounting", EffectiveDate = new DateOnly(2026, 1, 2), Source = "DevelopmentSeed", Version = 1, ManualOverride = true, CapturedAt = context.AsOf });

        await context.ExecuteAsync("insert into finance_subledger.party_financial_profile(tenant_id,legal_entity_id,id,party_id,default_receivable_payment_term_id,default_payable_payment_term_id,credit_limit_amount,credit_limit_currency,receivable_policy_code,payable_policy_code,status,version,created_at,updated_at) values($1,$2,$3,$4,$5,$5,250000,'EGP','STANDARD','STANDARD','Active',1,$6,$6) on conflict(tenant_id,legal_entity_id,party_id) do nothing",
            cancellationToken, context.Tenant.Id, context.EntityId, SeedIds.Child(context.Tenant.Id, "subledger-profile", 1), customerId, termId, context.AsOf);

        var installmentTerm = new
        {
            PaymentTerm = new { PaymentTermId = termId, VersionId = termVersionId, Code = "INSTALL3", VersionNumber = 1, EffectiveFrom = "2026-01-01" },
            RoundingPolicy = new { Code = "STANDARD", Version = 1, MoneyScale = 2, FxBaseAmount = "HalfUp", PaymentScheduleLine = "HalfUp" },
            Schedule = new[]
            {
                new { Sequence = 1, Allocation = "Percentage", Percentage = (string?)"0.3333", DueDate = "2026-01-01" },
                new { Sequence = 2, Allocation = "Percentage", Percentage = (string?)"0.3333", DueDate = "2026-01-31" },
                new { Sequence = 3, Allocation = "Balance", Percentage = (string?)null, DueDate = "2026-02-03" }
            }
        };
        var snapshot = JsonSerializer.Serialize(installmentTerm);
        var rows = new[]
        {
            Item(customerId, "Receivable", "Debit", "Invoice", "INV-DEV-1001", "100000", "100000", new(2026,1,1), new(2026,1,1), new(2026,1,10), null),
            Item(customerId, "Receivable", "Debit", "Invoice", "INV-DEV-1002", "10000", "10000", new(2026,1,1), new(2026,1,1), new(2026,1,1), snapshot, "0001"),
            Item(customerId, "Receivable", "Debit", "Invoice", "INV-DEV-1002", "10000", "10000", new(2026,1,1), new(2026,1,1), new(2026,1,31), snapshot, "0002"),
            Item(customerId, "Receivable", "Debit", "Invoice", "INV-DEV-1002", "10000", "10000", new(2026,1,1), new(2026,1,1), new(2026,2,3), snapshot, "0003"),
            Item(customerId, "Receivable", "Credit", "Advance", "ADV-DEV-1001", "20000", "20000", new(2026,1,2), new(2026,1,2), new(2026,1,2), null),
            Item(customerId, "Receivable", "Credit", "Payment", "PAY-DEV-1001", "3000", "3000", new(2026,1,2), new(2026,1,2), new(2026,1,2), null),
            Item(customerId, "Receivable", "Credit", "CreditNote", "CN-DEV-1001", "2500", "2500", new(2026,1,3), new(2026,1,3), new(2026,1,3), null),
            Item(customerId, "Receivable", "Debit", "Invoice", "INV-DEV-FX-1001", "100", "5000", new(2026,1,2), new(2026,1,2), new(2026,2,1), null, transactionCurrency: "USD", fxSnapshot: usdSnapshot),
            Item(customerId, "Receivable", "Credit", "Payment", "PAY-DEV-FX-1001", "5050", "5050", new(2026,1,2), new(2026,1,2), new(2026,1,2), null),
            Item(supplierId, "Payable", "Credit", "Bill", "BILL-DEV-2001", "45000", "45000", new(2026,1,1), new(2026,1,1), new(2026,1,15), null),
            Item(supplierId, "Payable", "Debit", "Advance", "ADV-DEV-2001", "5000", "5000", new(2026,1,2), new(2026,1,2), new(2026,1,2), null)
        };

        foreach (var row in rows)
        {
            var id = SeedIds.Stable($"{context.Tenant.Id:D}:subledger-item:{row.PartyId:D}:{row.SourceType}:{row.SourceReference}:{row.ScheduleReference ?? "single"}");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row)))).ToLowerInvariant();
            await context.ExecuteAsync("insert into finance_subledger.party_open_item(tenant_id,legal_entity_id,id,party_id,direction,polarity,source_type,source_reference,source_schedule_reference,source_display_number,document_date,posting_date,due_date,transaction_currency,original_transaction_amount,open_transaction_amount,base_currency,original_base_amount,open_base_carrying_amount,control_account_role,control_account_reference,control_configuration_reference,historic_fx_snapshot,source_version,source_payload_hash,status,is_open,row_version,created_at,payment_term_snapshot) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$8,$10,$11,$12,$19,$13,$13,'EGP',$14,$14,$5,'role:'||$5,$15,$20::jsonb,1,$16,'Open',true,1,$17,$18::jsonb) on conflict do nothing",
                cancellationToken, context.Tenant.Id, context.EntityId, id, row.PartyId, row.Direction, row.Polarity, row.SourceType, row.SourceReference, (object?)row.ScheduleReference ?? DBNull.Value,
                row.DocumentDate, row.PostingDate, row.DueDate, decimal.Parse(row.Amount, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(row.BaseAmount, System.Globalization.CultureInfo.InvariantCulture),
                $"configuration:{ledgerConfig:D}", hash, context.AsOf, (object?)row.PaymentTermSnapshot ?? DBNull.Value, row.TransactionCurrency, (object?)row.FxSnapshot ?? DBNull.Value);
        }

        foreach (var direction in new[] { "Receivable", "Payable" })
        {
            await context.ExecuteAsync("insert into finance_subledger.write_off_policy(tenant_id,legal_entity_id,id,direction,currency_code,maximum_amount,approval_threshold,automatic_write_off_enabled,version,updated_at) values($1,$2,$3,$4,'EGP',1000,100,false,1,$5) on conflict(tenant_id,legal_entity_id,direction,currency_code) do nothing",
                cancellationToken, context.Tenant.Id, context.EntityId, SeedIds.Child(context.Tenant.Id, $"subledger-write-off-policy-{direction.ToLowerInvariant()}", 1), direction, context.AsOf);
        }

        return new SeedSubledgerSummary(rows.Length, 3, 2);
    }

    private static SeedOpenItem Item(Guid partyId, string direction, string polarity, string sourceType, string sourceReference, string amount, string baseAmount,
        DateOnly documentDate, DateOnly postingDate, DateOnly dueDate, string? termSnapshot, string? scheduleReference = null, string transactionCurrency = "EGP", string? fxSnapshot = null) =>
        new(partyId, direction, polarity, sourceType, sourceReference, amount, baseAmount, documentDate, postingDate, dueDate, termSnapshot, scheduleReference, transactionCurrency, fxSnapshot);

    private sealed record SeedOpenItem(Guid PartyId, string Direction, string Polarity, string SourceType, string SourceReference, string Amount, string BaseAmount,
        DateOnly DocumentDate, DateOnly PostingDate, DateOnly DueDate, string? PaymentTermSnapshot, string? ScheduleReference, string TransactionCurrency, string? FxSnapshot);
}

public sealed record SeedSubledgerSummary(int OpenItems, int Installments, int FinancialProfiles);
