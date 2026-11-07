namespace Modules.POS;

public static class PosOfflineCashSalePolicy
{
    public static void Validate(OfflineCashSalePayload request)
    {
        if (request.SchemaVersion != 1 || !string.Equals(request.Type, "CashSale", StringComparison.Ordinal) || request.SessionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.CurrencyCode) || request.PaymentAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.CustomerPartyId is null || request.Lines is null || request.Lines.Count == 0)
            throw new ArgumentException("pos.offline_cash_sale_invalid");

        if (request.Lines.Any(line => line.VariantId == Guid.Empty || string.IsNullOrWhiteSpace(line.Description) || string.IsNullOrWhiteSpace(line.UomCode) ||
            line.Quantity <= 0 || line.UnitPrice < 0 || line.TaxAmount < 0 || line.DiscountAmount < 0 || line.LegalEntityId == Guid.Empty ||
            line.ProductTemplateId == Guid.Empty || line.UnitOfMeasureId == Guid.Empty || line.PriceListId == Guid.Empty || line.PricingDate == default ||
            (line.SerialId is not null && line.Quantity != 1m)))
            throw new ArgumentException("pos.offline_cash_sale_line_invalid");
    }
}
