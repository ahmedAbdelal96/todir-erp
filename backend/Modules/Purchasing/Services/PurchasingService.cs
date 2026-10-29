using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Modules.Purchasing.DTOs;
using Modules.Purchasing.Entities;
using Modules.Purchasing.Repositories;
using Shared.Contracts.Directory;
using Shared.Contracts.Invoicing;
using Shared.Contracts.Pricing;
using Shared.Primitives.Identifiers;

namespace Modules.Purchasing.Services;

public sealed class PurchasingService(
    PurchasingRepository repository,
    IDirectoryPartyRoleCapabilities partyRoles,
    IPricingCapabilities pricing,
    IInvoicingSourceCapabilities invoicing) : Shared.Contracts.Purchasing.IPurchasingSourceCapabilities, Shared.Contracts.Purchasing.IPurchasingReadCapabilities
{
    public async Task<Guid> CreateAsync(TenantId tenant, Guid actor, CreatePurchaseOrderRequest request, bool canOverridePrice, Guid correlation, CancellationToken ct)
    {
        Validate(request.LegalEntityId, request.CurrencyCode, request.Lines);
        await EnsureSupplierAsync(tenant, request.SupplierPartyId, request.LegalEntityId, ct);
        var lines = await ResolveLinesAsync(tenant, request, canOverridePrice, ct);
        var approvalRequired = request.ApprovalRequired ?? await repository.ApprovalRequiredAsync(tenant, request.LegalEntityId, ct);
        return await repository.CreateAsync(tenant, actor, request with { CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant() }, approvalRequired, lines, correlation, ct);
    }

    public async Task<long> UpdateAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, UpdatePurchaseOrderRequest request, bool canOverridePrice, Guid correlation, CancellationToken ct, bool amendment = false, string? reason = null)
    {
        Validate(legalEntityId, request.CurrencyCode, request.Lines);
        await EnsureSupplierAsync(tenant, request.SupplierPartyId, legalEntityId, ct);
        var lines = await ResolveLinesAsync(tenant, new CreatePurchaseOrderRequest(legalEntityId, null, request.SupplierPartyId, request.CurrencyCode, request.OrderDate, null, false, request.Lines), canOverridePrice, ct);
        return await repository.UpdateAsync(tenant, actor, legalEntityId, id, request with { CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant() }, lines, correlation, ct, amendment, reason);
    }

    public Task<PurchaseOrderView?> GetAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.GetAsync(tenant, entity, id, ct);
    public Task<PurchaseOrderPage> ListAsync(TenantId tenant, Guid entity, string? state, int page, int pageSize, CancellationToken ct) => repository.ListAsync(tenant, entity, state, page, pageSize, ct);
    public Task<long> ApproveAsync(TenantId tenant, Guid actor, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct) => repository.ApproveAsync(tenant, actor, id, request, correlation, ct);
    public Task<long> RejectAsync(TenantId tenant, Guid actor, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct) => repository.RejectAsync(tenant, actor, id, request, correlation, ct);
    public Task<long> CancelAsync(TenantId tenant, Guid actor, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct) => repository.CancelAsync(tenant, actor, id, request, correlation, ct);
    public Task<long> CloseAsync(TenantId tenant, Guid actor, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct) => repository.CloseAsync(tenant, actor, id, request, correlation, ct);
    public Task<bool> IsPurchaseOrderApprovedAsync(TenantId tenantId, Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default) => repository.IsApprovedAsync(tenantId, legalEntityId, purchaseOrderId, cancellationToken);
    public Task<Shared.Contracts.Purchasing.PurchaseOrderInvoiceSource?> GetInvoiceSourceAsync(TenantId tenantId, Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default) => repository.GetInvoiceSourceAsync(tenantId, legalEntityId, purchaseOrderId, cancellationToken);

    public async Task<Guid> CreateSupplierInvoiceAsync(TenantId tenant, Guid actor, Guid membership, Guid legalEntityId, Guid id, CreateSupplierInvoiceRequest request, Guid correlation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200) throw new PurchasingException("purchasing.idempotency_key_invalid");
        var order = await repository.GetAsync(tenant, legalEntityId, id, ct) ?? throw new PurchasingException("purchasing.po_not_found");
        if (order.State is not (PurchaseOrderStates.Approved or PurchaseOrderStates.Open or PurchaseOrderStates.PartiallyCompleted)) throw new PurchasingException("purchasing.po_not_approved");
        var existing = await repository.GetLinkedInvoiceAsync(tenant, legalEntityId, id, request.IdempotencyKey, ct);
        if (existing is { } existingInvoice) return existingInvoice;
        var payloadHash = PayloadHash(order, request);
        var source = new InvoiceSourceDocumentContract("purchasing", id, "SupplierInvoice", request.IdempotencyKey, "Payable", legalEntityId, order.BranchId, order.SupplierPartyId, null, request.DocumentDate, request.PostingDate, request.TaxDate, null, order.CurrencyCode, "Exclusive", null, request.SupplierInvoiceNumber, null, order.Lines.Select(line => new InvoiceSourceLineContract(line.ProductVariantId, line.UnitOfMeasureId, line.UnitCode, line.Description, line.OrderedQuantity.ToString(CultureInfo.InvariantCulture), line.UnitPrice.ToString(CultureInfo.InvariantCulture), null, null, line.Id.ToString("D"))).ToArray());
        var invoiceId = await invoicing.CreateDraftFromSourceAsync(tenant, actor, membership, source, correlation, ct);
        await repository.LinkInvoiceAsync(tenant, actor, legalEntityId, id, invoiceId, "SupplierInvoice", request.IdempotencyKey, payloadHash, ct);
        return invoiceId;
    }

    private async Task<IReadOnlyList<PurchasingRepository.ResolvedLine>> ResolveLinesAsync(TenantId tenant, CreatePurchaseOrderRequest request, bool canOverridePrice, CancellationToken ct)
    {
        var result = new List<PurchasingRepository.ResolvedLine>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0 || line.Description.Trim().Length is < 1 or > 1000) throw new PurchasingException("purchasing.line_invalid");
            decimal unitPrice;
            string? evidence = null;
            if (line.UnitPrice is { } explicitPrice)
            {
                if (explicitPrice < 0) throw new PurchasingException("purchasing.price_invalid");
                if (string.IsNullOrWhiteSpace(line.PriceOverrideReason) || !canOverridePrice) throw new PurchasingException("purchasing.price_override_not_allowed");
                unitPrice = explicitPrice;
            }
            else if (line.ProductVariantId is { } variant && line.ProductTemplateId is { } template && line.UnitOfMeasureId is { } uom)
            {
                var resolution = await pricing.ResolveAsync(new(tenant, new(request.LegalEntityId), PricingDirection.Buying, request.OrderDate, request.CurrencyCode.Trim().ToUpperInvariant(), request.PriceListId, request.SupplierPartyId, null, variant, template, Array.Empty<Guid>(), null, uom, line.Quantity), ct);
                if (resolution.Snapshot is null || !decimal.TryParse(resolution.Snapshot.FinalUnitPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out unitPrice)) throw new PurchasingException("purchasing.buy_price_missing");
                evidence = JsonSerializer.Serialize(resolution.Snapshot);
            }
            else throw new PurchasingException("purchasing.buy_price_required");
            result.Add(new(line.ProductVariantId, line.UnitOfMeasureId, line.UnitCode, line.Description.Trim(), line.Quantity, unitPrice, evidence, line.PriceOverrideReason));
        }
        return result;
    }

    private async Task EnsureSupplierAsync(TenantId tenant, Guid partyId, Guid entity, CancellationToken ct)
    {
        if (partyId == Guid.Empty || !await partyRoles.HasActiveRoleAsync(tenant, partyId, entity, "Supplier", ct)) throw new PurchasingException("purchasing.supplier_not_ready");
    }

    private static void Validate(Guid entity, string currency, IReadOnlyList<PurchaseOrderLineRequest> lines)
    { if (entity == Guid.Empty) throw new PurchasingException("purchasing.entity_required"); if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3) throw new PurchasingException("purchasing.currency_invalid"); if (lines is null || lines.Count is < 1 or > 500) throw new PurchasingException("purchasing.lines_invalid"); }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string PayloadHash(PurchaseOrderView order, CreateSupplierInvoiceRequest request) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { order.Id, order.Version, request }, Json)))).ToLowerInvariant();
}
