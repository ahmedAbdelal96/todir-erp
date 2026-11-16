using System.Security.Cryptography;
using System.Text.Json;
using Modules.Invoicing.DTOs;
using Modules.Invoicing.Entities;
using Shared.Contracts.Invoicing;

namespace Modules.Invoicing.Services;

public sealed record MappedInvoiceSourceRequest(CreateInvoiceDraftRequest Draft, InvoiceDirection Direction, InvoiceSourceReferenceContract Reference);

public static class InvoiceSourceRequestMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static MappedInvoiceSourceRequest Map(InvoiceSourceDocumentContract request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourceModule is not ("sales" or "purchasing" or "pos" or "projects" or "service") || request.SourceDocumentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Purpose) || request.Purpose.Length > 80 ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200)
            throw new InvoiceException("invoicing.source_reference_invalid", "The source document reference or idempotency key is invalid.");
        var direction = request.Direction switch
        {
            "Receivable" when request.SourceModule is "sales" or "pos" or "projects" or "service" => InvoiceDirection.Receivable,
            "Payable" when request.SourceModule == "purchasing" => InvoiceDirection.Payable,
            _ => throw new InvoiceException("invoicing.source_direction_invalid", "Sales and POS sources create customer invoices; purchasing sources create supplier invoices.")
        };
        if (request.Lines is null || request.Lines.Count is < 1 or > 1000)
            throw new InvoiceException("invoicing.lines_invalid", "An invoice source must provide between one and 1000 lines.");
        var payloadHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions))).ToLowerInvariant();
        var reference = new InvoiceSourceReferenceContract(request.SourceModule, request.SourceDocumentId, request.Purpose.Trim(), request.IdempotencyKey.Trim(), payloadHash);
        var draft = new CreateInvoiceDraftRequest(request.LegalEntityId, request.BranchId, request.PartyId, request.AddressId, request.DocumentDate,
            request.PostingDate, request.TaxDate, request.SupplyDate, request.CurrencyCode, request.PaymentTermId, request.SupplierInvoiceNumber,
            request.Lines.Select(line => new InvoiceLineInput(line.ProductVariantId, line.UnitOfMeasureId, line.UnitCode, line.Description, line.Quantity,
                line.AgreedUnitPrice, null, line.SourceLineReference,
                line.AgreedUnitPrice is null ? line.PriceOverrideReason : line.PriceOverrideReason ?? "Agreed price supplied by the source document", line.BusinessCategoryCode)).ToArray(), request.DocumentDiscountAmount,
            (request.Charges ?? []).Select(charge => new InvoiceChargeInput(charge.Code, charge.Description, charge.Amount, charge.TaxTreatment, charge.BusinessCategoryCode)).ToArray(), request.PriceMode);
        return new(draft, direction, reference);
    }
}
