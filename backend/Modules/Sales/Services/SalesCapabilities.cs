using Shared.Primitives.Identifiers;
using Todir.Sales.Contracts;
using Modules.Sales.DTOs;

namespace Modules.Sales.Services;

public sealed class SalesCapabilities(SalesService sales) : ISalesQuotationCapabilities
{
    public Task<Guid> CreateDraftQuotationAsync(TenantId tenantId, Guid actorId, SalesQuotationSourceContract request, CancellationToken cancellationToken = default)
        => sales.CreateQuotationAsync(tenantId, actorId, new CreateQuotationRequest(
            request.LegalEntityId,
            null,
            request.PartyId,
            request.CurrencyCode,
            request.QuoteDate,
            request.ValidUntil,
            request.Lines.Select(line => new QuotationLineRequest(line.ProductVariantId, line.UnitOfMeasureId, line.Description, line.Quantity, line.UnitPrice, line.DiscountAmount, line.UomCode)).ToArray(),
            request.CustomerReference), cancellationToken);

    public Task<SalesAcceptedQuotationContract?> GetAcceptedQuotationAsync(TenantId tenantId, Guid legalEntityId, Guid quotationId, int acceptedRevision, CancellationToken cancellationToken = default)
        => sales.GetAcceptedQuotationAsync(tenantId, legalEntityId, quotationId, acceptedRevision, cancellationToken);
}
