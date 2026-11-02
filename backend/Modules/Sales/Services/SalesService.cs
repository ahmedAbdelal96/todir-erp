using System.Globalization;
using System.Text.Json;
using Modules.Sales.DTOs;
using Modules.Sales.Entities;
using Modules.Sales.Repositories;
using Shared.Contracts.Catalog;
using Shared.Contracts.Directory;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Invoicing;
using Shared.Contracts.Pricing;
using Shared.Contracts.Subledger;
using Shared.Contracts.Tax;
using Shared.Primitives.Identifiers;
using Todir.Sales.Contracts;

namespace Modules.Sales.Services;

public sealed partial class SalesService(
    SalesRepository repository,
    IDirectoryCapabilities directory,
    ICatalogCapabilities catalog,
    IPricingCapabilities pricing,
    IInvoicingSourceCapabilities invoicing,
    ITaxCapabilities tax,
    IFinanceBaseCapabilities financeBase,
    ISubledgerCapabilities subledger,
    ISalesInventoryCapabilities? inventory = null,
    IInvoicingCorrectionCapabilities? correction = null)
{
    public async Task<Guid> CreateQuotationAsync(TenantId tenant, Guid actor, CreateQuotationRequest request, CancellationToken ct)
    {
        ValidateHeader(request.LegalEntityId, request.CurrencyCode);
        if (!await directory.IsActivePartyAsync(tenant, request.PartyId, request.LegalEntityId, ct)) throw new SalesException("sales.customer_not_found");
        var lines = await ResolvePricesAsync(tenant, request.LegalEntityId, request.PartyId, request.PriceListId, request.CurrencyCode, request.QuoteDate, request.Lines, ct);
        return await repository.CreateQuotationAsync(tenant, actor, request with { CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant() }, lines,
            await TaxEvidenceAsync(tenant, request.LegalEntityId, request.PartyId, request.CurrencyCode, request.QuoteDate, lines, ct), ct);
    }

    public async Task<long> UpdateQuotationAsync(TenantId tenant, Guid actor, Guid id, Guid legalEntityId, UpdateQuotationRequest request, CancellationToken ct)
    {
        ValidateHeader(legalEntityId, request.CurrencyCode);
        if (!await directory.IsActivePartyAsync(tenant, request.PartyId, legalEntityId, ct)) throw new SalesException("sales.customer_not_found");
        var lines = await ResolvePricesAsync(tenant, legalEntityId, request.PartyId, request.PriceListId, request.CurrencyCode, request.QuoteDate, request.Lines, ct);
        return await repository.UpdateQuotationAsync(tenant, actor, legalEntityId, id, request, lines,
            await TaxEvidenceAsync(tenant, legalEntityId, request.PartyId, request.CurrencyCode, request.QuoteDate, lines, ct), ct);
    }

    public Task<QuotationDetail?> GetQuotationAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.GetQuotationAsync(tenant, entity, id, ct);
    public Task<SalesPage<QuotationListItem>> ListQuotationsAsync(TenantId tenant, Guid entity, string? state, Guid? party, int page, int pageSize, CancellationToken ct, string? search = null) => repository.ListQuotationsAsync(tenant, entity, state, party, page, pageSize, ct, search);
    public Task<int> OpenQuotationAsync(TenantId tenant, Guid actor, Guid entity, Guid id, TransitionQuotationRequest request, CancellationToken ct) => repository.OpenQuotationAsync(tenant, actor, entity, id, request, ct);
    public Task<int> DeclineQuotationAsync(TenantId tenant, Guid actor, Guid entity, Guid id, TransitionQuotationRequest request, CancellationToken ct) => repository.DeclineQuotationAsync(tenant, actor, entity, id, request, ct);
    public Task<int> CancelQuotationAsync(TenantId tenant, Guid actor, Guid entity, Guid id, TransitionQuotationRequest request, CancellationToken ct) => repository.CancelQuotationAsync(tenant, actor, entity, id, request, ct);
    public Task<QuotationDetail> AcceptQuotationAsync(TenantId tenant, Guid actor, Guid entity, Guid id, AcceptQuotationRequest request, CancellationToken ct) => repository.AcceptQuotationAsync(tenant, actor, entity, id, request, ct);

    public async Task<Guid> ReviseQuotationAsync(TenantId tenant, Guid actor, Guid entity, Guid id, long version, IReadOnlyList<QuotationLineRequest> lines, CancellationToken ct)
    {
        var quote = await repository.GetQuotationAsync(tenant, entity, id, ct) ?? throw new SalesException("sales.quotation_not_found");
        var priced = await ResolvePricesAsync(tenant, entity, quote.PartyId, null, quote.CurrencyCode, quote.QuoteDate, lines, ct);
        return await repository.CreateRevisionAsync(tenant, actor, entity, id, version, priced,
            await TaxEvidenceAsync(tenant, entity, quote.PartyId, quote.CurrencyCode, quote.QuoteDate, priced, ct), ct);
    }

    public Task<ConversionResult> ConvertQuotationAsync(TenantId tenant, Guid actor, Guid membership, Guid entity, Guid id, ConvertQuotationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200) throw new SalesException("sales.idempotency_key_invalid");
        return request.DeliverNow ? ConvertAndDeliverQuotationAsync(tenant, actor, membership, entity, id, request, ct) : repository.ConvertToInvoiceAsync(tenant, actor, membership, entity, id, request, invoicing, ct);
    }

    public async Task<QuotationDetail> AcceptAndInvoiceAsync(TenantId tenant, Guid actor, Guid membership, Guid entity, Guid id, AcceptAndInvoiceQuotationRequest request, CancellationToken ct)
    {
        await AcceptQuotationAsync(tenant, actor, entity, id, new(request.RevisionNumber, request.AcceptanceMethod, request.EvidenceReference, request.Note, request.AcceptanceIdempotencyKey), ct);
        await ConvertQuotationAsync(tenant, actor, membership, entity, id, new(request.RevisionNumber, request.InvoiceIdempotencyKey, "CreateInvoiceDraft", request.DeliverNow, request.WarehouseId), ct);
        return (await repository.GetQuotationAsync(tenant, entity, id, ct))!;
    }

    public async Task<Guid> CreateOrderAsync(TenantId tenant, Guid actor, CreateOrderRequest request, CancellationToken ct)
    {
        ValidateHeader(request.LegalEntityId, request.CurrencyCode);
        if (!await directory.IsActivePartyAsync(tenant, request.PartyId, request.LegalEntityId, ct)) throw new SalesException("sales.customer_not_found");
        var lines = await ResolvePricesAsync(tenant, request.LegalEntityId, request.PartyId, null, request.CurrencyCode, request.OrderDate, request.Lines, ct);
        return await repository.CreateOrderAsync(tenant, actor, request with { CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant() }, lines, ct);
    }

    public Task<SalesOrderDetail?> GetOrderAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.GetOrderAsync(tenant, entity, id, ct);
    public Task<SalesPage<SalesOrderListItem>> ListOrdersAsync(TenantId tenant, Guid entity, int page, int pageSize, CancellationToken ct, string? search = null) => repository.ListOrdersAsync(tenant, entity, page, pageSize, ct, search);

    public Task<Guid> ConvertQuotationToOrderAsync(TenantId tenant, Guid actor, Guid entity, Guid id, ConvertQuotationToOrderRequest request, CancellationToken ct)
        => repository.ConvertQuotationToOrderAsync(tenant, actor, entity, id, request, ct);

    public async Task<int> AmendOrderAsync(TenantId tenant, Guid actor, Guid entity, Guid id, UpdateOrderRequest request, CancellationToken ct)
    {
        var order = await repository.GetOrderAsync(tenant, entity, id, ct) ?? throw new SalesException("sales.order_not_found");
        var lines = await ResolvePricesAsync(tenant, entity, order.PartyId, null, order.CurrencyCode, order.OrderDate, request.Lines, ct);
        return await repository.AmendOrderAsync(tenant, actor, entity, id, request, lines, ct);
    }

    public async Task<int> ConfirmOrderAsync(TenantId tenant, Guid actor, Guid entity, Guid id, OrderCommandRequest request, bool creditOverrideAllowed, CancellationToken ct)
    {
        var order = await repository.GetOrderAsync(tenant, entity, id, ct) ?? throw new SalesException("sales.order_not_found");
        if (order.Version != request.Version) throw new SalesException("sales.stale_order");
        var proposed = order.Lines.Sum(line => line.RemainingQuantity * line.UnitPrice);
        await EvaluateCreditAsync(tenant, actor, entity, order.PartyId, id, proposed, request.CreditOverrideReason, creditOverrideAllowed, ct);
        order = await repository.GetOrderAsync(tenant, entity, id, ct) ?? throw new SalesException("sales.order_not_found");
        await repository.ConfirmOrderAsync(tenant, actor, entity, id, request with { Version = order.Version }, ct);
        if (inventory is not null && order.WarehouseId is { } warehouse)
        {
            var reserved = new List<Guid>();
            try
            {
                foreach (var line in order.Lines.Where(line => line.ProductVariantId is not null && line.UnitOfMeasureId is not null && line.RemainingQuantity > 0))
                {
                    var reservation = await inventory.ReserveAsync(tenant.Value, actor, new SalesInventoryReservationRequest(entity, line.ProductVariantId!.Value, warehouse, line.RemainingQuantity, id, line.Id, $"confirm:{id:D}:{line.Id:D}"), ct);
                    reserved.Add(reservation);
                    await repository.RecordReservationAsync(tenant, entity, id, line.Id, reservation, line.RemainingQuantity, ct);
                }
            }
            catch
            {
                foreach (var reservation in reserved) await inventory.ReleaseAsync(tenant.Value, actor, entity, reservation, ct);
                throw new SalesException("sales.reservation_failed");
            }
        }
        return (int)order.Version + 1;
    }

    public async Task<int> CancelOrderAsync(TenantId tenant, Guid actor, Guid entity, Guid id, OrderCommandRequest request, CancellationToken ct)
    {
        await ReleaseOrderReservationsAsync(tenant, actor, entity, id, ct);
        return await repository.CancelOrderAsync(tenant, actor, entity, id, request, ct);
    }

    public async Task<int> ReleaseOrderReservationsAsync(TenantId tenant, Guid actor, Guid entity, Guid id, CancellationToken ct)
    {
        if (inventory is null) throw new SalesException("sales.inventory_capability_unavailable");
        var reservations = await repository.ListReservationsAsync(tenant, entity, id, ct);
        var released = 0;
        foreach (var reservation in reservations.Where(x => x.State == "Active"))
        {
            await inventory.ReleaseAsync(tenant.Value, actor, entity, reservation.InventoryReservationId, ct);
            await repository.MarkReservationReleasedAsync(tenant, entity, id, reservation.InventoryReservationId, "Released", ct);
            released++;
        }
        return released;
    }

    public Task<ConversionResult> ConvertOrderToInvoiceAsync(TenantId tenant, Guid actor, Guid membership, Guid entity, Guid id, OrderCommandRequest request, CancellationToken ct) => repository.ConvertOrderToInvoiceAsync(tenant, actor, membership, entity, id, request.IdempotencyKey, invoicing, ct);

    public async Task<SalesFulfillmentResult> FulfillOrderAsync(TenantId tenant, Guid actor, Guid entity, Guid id, FulfillOrderRequest request, CancellationToken ct)
    {
        if (inventory is null) throw new SalesException("sales.inventory_capability_unavailable");
        var order = await repository.GetOrderAsync(tenant, entity, id, ct) ?? throw new SalesException("sales.order_not_found");
        if (order.WarehouseId is not { } warehouse) throw new SalesException("sales.warehouse_required");
        var reservations = (await repository.ListReservationsAsync(tenant, entity, id, ct)).ToDictionary(x => x.OrderLineId);
        var issued = new List<(Guid lineId, decimal requested, decimal fulfilled, Guid? operationId)>();
        var consumption = new List<(Guid orderLineId, Guid inventoryReservationId, Guid inventoryOperationId, decimal quantity, string idempotencyKey)>();
        try
        {
            foreach (var requested in request.Lines)
            {
                var line = order.Lines.SingleOrDefault(x => x.Id == requested.OrderLineId) ?? throw new SalesException("sales.order_line_not_found");
                if (requested.Quantity <= 0 || requested.Quantity > line.RemainingQuantity) throw new SalesException("sales.fulfillment_quantity_invalid");
                if (line.ProductVariantId is null || line.UnitOfMeasureId is null || !reservations.TryGetValue(line.Id, out var reservation)) throw new SalesException("sales.reservation_required");
                if (requested.Quantity > reservation.RemainingQuantity) throw new SalesException("sales.fulfillment_quantity_invalid");
                var operation = await inventory.IssueAsync(tenant.Value, actor, new SalesInventoryIssueRequest(entity, reservation.InventoryReservationId, line.ProductVariantId.Value, line.UnitOfMeasureId.Value, warehouse, requested.Quantity, id, line.Id, $"fulfill:{id:D}:{request.IdempotencyKey}:{line.Id:D}", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow)), ct);
                if (operation.FulfilledQuantity > 0)
                    consumption.Add((line.Id, reservation.InventoryReservationId, operation.OperationId, operation.FulfilledQuantity, $"fulfill:{id:D}:{request.IdempotencyKey}:{line.Id:D}"));
                issued.Add((line.Id, requested.Quantity, operation.FulfilledQuantity, operation.OperationId));
            }
            await repository.RecordReservationConsumptionsAsync(tenant, entity, id, consumption, ct);
        }
        catch
        {
            foreach (var operation in issued.Where(x => x.operationId is not null)) await inventory.ReverseAsync(tenant.Value, actor, entity, operation.operationId!.Value, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), $"rollback:{request.IdempotencyKey}:{operation.operationId:D}", ct);
            throw new SalesException("sales.fulfillment_failed");
        }
        return await repository.RecordFulfillmentAsync(tenant, actor, entity, id, request.IdempotencyKey, issued, ct);
    }

    public async Task<Guid> CreateReturnAsync(TenantId tenant, Guid actor, CreateReturnRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty) throw new SalesException("sales.entity_required");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000) throw new SalesException("sales.return_reason_required");
        if (request.Lines.Any(line => line.Quantity <= 0)) throw new SalesException("sales.return_quantity_invalid");
        if (request.Lines.Select(line => line.SourceLineId).Where(id => id is not null).Distinct().Count() != request.Lines.Count(line => line.SourceLineId is not null)) throw new SalesException("sales.return_duplicate_source_line");
        var sourceType = request.SourceDocumentType.Trim();
        if (!sourceType.Equals("SalesOrder", StringComparison.OrdinalIgnoreCase) && !sourceType.Equals("Invoice", StringComparison.OrdinalIgnoreCase)) throw new SalesException("sales.return_source_invalid");
        if (request.SourceDocumentId is null) throw new SalesException("sales.return_source_required");
        if (!await directory.IsActivePartyAsync(tenant, request.PartyId, request.LegalEntityId, ct)) throw new SalesException("sales.customer_not_found");
        await repository.ValidateReturnableQuantitiesAsync(tenant, request, ct);
        return await repository.CreateReturnAsync(tenant, actor, request with { SourceDocumentType = sourceType.Equals("Invoice", StringComparison.OrdinalIgnoreCase) ? "Invoice" : "SalesOrder" }, ct);
    }
    public Task<SalesReturnView?> GetReturnAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.GetReturnAsync(tenant, entity, id, ct);
    public Task<long> ApproveReturnAsync(TenantId tenant, Guid actor, Guid entity, Guid id, ReturnCommandRequest request, CancellationToken ct) => repository.TransitionReturnAsync(tenant, actor, entity, id, "Approved", request, ct);
    public async Task<SalesReturnExecutionResult> ExecuteReturnAsync(TenantId tenant, Guid actor, Guid membership, Guid entity, Guid id, ReturnCommandRequest request, CancellationToken ct)
    {
        var work = await repository.PrepareReturnExecutionAsync(tenant, actor, entity, id, request, ct);
        if (work.State == "Executed") return await repository.GetReturnExecutionResultAsync(tenant, entity, id, ct);
        Guid? inventoryReceiptId = work.InventoryReceiptId;
        Guid? creditNoteId = work.CreditNoteId;
        try
        {
            foreach (var line in work.Lines.Where(line => line.ProductVariantId is not null))
            {
                if (line.SourceLineId is null || line.WarehouseId is null || line.UnitOfMeasureId is null)
                    throw new SalesException("sales.return_inventory_context_required");
                if (inventory is null) throw new SalesException("sales.inventory_capability_unavailable");
                var sourceLineId = line.SourceLineId.Value;
                var productVariantId = line.ProductVariantId ?? throw new SalesException("sales.return_inventory_context_required");
                var warehouseId = line.WarehouseId.Value;
                var unitOfMeasureId = line.UnitOfMeasureId.Value;
                var operationId = await inventory.ReceiveReturnAsync(tenant.Value, actor,
                    new SalesInventoryReturnRequest(entity, productVariantId, warehouseId, line.LocationId,
                        unitOfMeasureId, line.Quantity, id, sourceLineId,
                        $"return:{id:D}:{line.Id:D}", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow),
                        line.StockStatus, line.LotId, line.SerialId), ct);
                await repository.RecordReturnInventoryOperationAsync(tenant, entity, id, line.Id, operationId, ct);
                inventoryReceiptId ??= operationId;
            }

            if (work.SourceDocumentType.Equals("Invoice", StringComparison.OrdinalIgnoreCase))
            {
                if (work.SourceDocumentId is null || work.Lines.Any(line => line.SourceLineId is null)) throw new SalesException("sales.credit_note_source_required");
                if (correction is null) throw new SalesException("sales.credit_note_capability_unavailable");
                creditNoteId ??= await correction.CreateCreditNoteDraftAsync(tenant, actor, membership,
                    new InvoiceCreditNoteRequest(work.SourceDocumentId.Value, entity, DateOnly.FromDateTime(DateTime.UtcNow),
                        DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), null,
                        work.Reason, $"return-credit:{id:D}", work.Lines.Select(line => new InvoiceCreditNoteLineContract(line.SourceLineId!.Value, line.Quantity)).ToArray()), id, ct);
            }

            await repository.CompleteReturnAsync(tenant, actor, entity, id, inventoryReceiptId, creditNoteId, ct);
            return await repository.GetReturnExecutionResultAsync(tenant, entity, id, ct);
        }
        catch (SalesException)
        {
            await repository.FailReturnAsync(tenant, actor, entity, id, "sales.return_cross_module_failed", ct);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await repository.FailReturnAsync(tenant, actor, entity, id, "sales.return_cross_module_failed", ct);
            throw new SalesException("sales.return_cross_module_failed");
        }
    }
    public Task SaveCustomerProfileAsync(TenantId tenant, Guid actor, SalesCustomerProfileRequest request, CancellationToken ct) => repository.SaveProfileAsync(tenant, actor, request, ct);

    private async Task EvaluateCreditAsync(TenantId tenant, Guid actor, Guid entity, Guid partyId, Guid orderId, decimal proposed, string? overrideReason, bool overrideAllowed, CancellationToken ct)
    {
        var profile = await subledger.GetFinancialProfileAsync(tenant, new LegalEntityId(entity), partyId, ct);
        var policy = string.IsNullOrWhiteSpace(profile?.ReceivablePolicyCode) ? "OFF" : profile.ReceivablePolicyCode!.Trim().ToUpperInvariant();
        if (policy == "OFF") { await repository.RecordCreditDecisionAsync(tenant, actor, entity, orderId, "Allow", policy, null, proposed, null, null, ct); return; }
        var balance = await subledger.GetPartyBalanceAsync(tenant, new PartyBalanceQuery(new LegalEntityId(entity), partyId, SubledgerDirectionContract.Receivable, DateOnly.FromDateTime(DateTime.UtcNow)), ct);
        decimal? available = decimal.TryParse(balance.AvailableCreditAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
        var over = available is { } credit && proposed > credit;
        var decision = over ? policy switch { "WARN" => "Warn", "BLOCK" => "Block", "REQUIREAPPROVAL" or "REQUIRE_APPROVAL" => "RequireApproval", _ => "Allow" } : "Allow";
        if (decision is "Block" or "RequireApproval")
        {
            if (!overrideAllowed || string.IsNullOrWhiteSpace(overrideReason)) throw new SalesException(decision == "Block" ? "sales.credit_blocked" : "sales.credit_approval_required");
            decision = "Override";
        }
        await repository.RecordCreditDecisionAsync(tenant, actor, entity, orderId, decision, policy, available, proposed, balance.ExposureVersion, overrideReason, ct);
    }

    private async Task<ConversionResult> ConvertAndDeliverQuotationAsync(TenantId tenant, Guid actor, Guid membership, Guid entity, Guid id, ConvertQuotationRequest request, CancellationToken ct)
    {
        if (inventory is null || request.WarehouseId is null) throw new SalesException("sales.deliver_now_requires_inventory_capability");
        var quote = await repository.GetQuotationAsync(tenant, entity, id, ct) ?? throw new SalesException("sales.quotation_not_found");
        var warehouse = request.WarehouseId.Value;
        var reservationIds = new List<Guid>(); var operations = new List<Guid>();
        try
        {
            foreach (var line in quote.Lines.Where(x => x.ProductVariantId is not null && x.UnitOfMeasureId is not null))
            {
                var reservation = await inventory.ReserveAsync(tenant.Value, actor, new SalesInventoryReservationRequest(entity, line.ProductVariantId!.Value, warehouse, line.Quantity, id, line.Id, $"deliver:{id:D}:{line.Id:D}:{request.IdempotencyKey}"), ct);
                reservationIds.Add(reservation);
                var unitOfMeasureId = line.UnitOfMeasureId ?? throw new SalesException("sales.unit_of_measure_required");
                var operation = await inventory.IssueAsync(tenant.Value, actor, new SalesInventoryIssueRequest(entity, reservation, line.ProductVariantId.Value, unitOfMeasureId, warehouse, line.Quantity, id, line.Id, $"deliver-issue:{id:D}:{line.Id:D}:{request.IdempotencyKey}", quote.QuoteDate, quote.QuoteDate), ct);
                operations.Add(operation.OperationId);
            }
            return await repository.ConvertToInvoiceAsync(tenant, actor, membership, entity, id, request with { DeliverNow = false }, invoicing, ct);
        }
        catch
        {
            foreach (var operation in operations) await inventory.ReverseAsync(tenant.Value, actor, entity, operation, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), $"deliver-rollback:{request.IdempotencyKey}:{operation:D}", ct);
            foreach (var reservation in reservationIds) await inventory.ReleaseAsync(tenant.Value, actor, entity, reservation, ct);
            throw new SalesException("sales.deliver_now_failed");
        }
    }

    private async Task<string> TaxEvidenceAsync(TenantId tenant, Guid entity, Guid party, string currency, DateOnly date, IReadOnlyList<QuotationLineRequest> lines, CancellationToken ct)
    {
        var financial = await financeBase.ResolveCalculationContextAsync(tenant, new LegalEntityId(entity), currency.Trim().ToUpperInvariant(), FinancialCalculationPurpose.Tax, date, ct);
        var baseCurrency = financial?.BaseCurrencyCode ?? currency.Trim().ToUpperInvariant();
        var contexts = lines.Select(line => new TaxContextContract(tenant, new LegalEntityId(entity), "Sales", "Quotation", "Sale", date, date, null, date, party, null, null, null, null, line.ProductVariantId, line.Quantity, line.UnitOfMeasureId, "TaxExclusive", (line.Quantity * (line.UnitPrice ?? 0m) - line.DiscountAmount).ToString(CultureInfo.InvariantCulture), line.DiscountAmount.ToString(CultureInfo.InvariantCulture), "0", currency.Trim().ToUpperInvariant(), baseCurrency, null)).ToArray();
        var results = await tax.CalculateBatchAsync(contexts, ct);
        return JsonSerializer.Serialize(new { capturedAt = DateTimeOffset.UtcNow, results });
    }

    private async Task<IReadOnlyList<QuotationLineRequest>> ResolvePricesAsync(TenantId tenant, Guid entity, Guid party, Guid? priceList, string currency, DateOnly date, IReadOnlyList<QuotationLineRequest> input, CancellationToken ct)
    {
        if (input is null || input.Count is < 1 or > 500) throw new SalesException("sales.lines_invalid");
        if (input.Any(line => line.Quantity <= 0 || line.Description.Trim().Length is < 1 or > 1000 || line.DiscountAmount < 0 || line.UnitPrice is < 0)) throw new SalesException("sales.line_values_invalid");
        var priced = input.ToArray();
        var variantIds = priced.Where(line => line.ProductVariantId is not null).Select(line => line.ProductVariantId!.Value).Distinct().ToArray();
        var variants = variantIds.Length == 0 ? new Dictionary<Guid, CatalogVariantIdentity>() : new Dictionary<Guid, CatalogVariantIdentity>(await catalog.FindVariantsBatchAsync(tenant, variantIds, entity, ct));
        var pricingVariantIds = priced.Where(line => line.ProductVariantId is not null && line.UnitOfMeasureId is not null && line.UnitPrice is null).Select(line => line.ProductVariantId!.Value).Distinct().ToArray();
        var pricingContexts = pricingVariantIds.Length == 0 ? new Dictionary<Guid, CatalogVariantPricingContext>() : new Dictionary<Guid, CatalogVariantPricingContext>(await catalog.GetPricingContextsBatchAsync(tenant, pricingVariantIds, entity, ct));
        var contexts = new List<(int index, PricingContext context)>();
        foreach (var (line, index) in priced.Select((line, index) => (line, index)))
        {
            if (line.ProductVariantId is { } productId)
            {
                if (!variants.ContainsKey(productId)) throw new SalesException("sales.product_not_found");
                if (line.UnitPrice is null)
                {
                    if (priceList is null) throw new SalesException("sales.price_required");
                    if (line.UnitOfMeasureId is not { } uom || !pricingContexts.TryGetValue(productId, out var product)) throw new SalesException("sales.product_not_found");
                    contexts.Add((index, new PricingContext(tenant, new LegalEntityId(entity), PricingDirection.Selling, date, currency.Trim().ToUpperInvariant(), priceList, party, null, productId, product.ProductTemplateId, product.CategoryPath, product.BrandId, uom, line.Quantity)));
                }
            }
            else if (line.UnitPrice is null) throw new SalesException("sales.service_price_required");
        }
        if (contexts.Count > 0)
        {
            var resolutions = await pricing.ResolveBatchAsync(contexts.Select(item => item.context).ToArray(), ct);
            foreach (var item in contexts.Select((value, index) => (value, index)))
            {
                var resolved = resolutions[item.index];
                if (resolved.Snapshot is null || !decimal.TryParse(resolved.Snapshot.FinalUnitPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)) throw new SalesException("sales.price_not_resolved");
                priced[item.value.index] = priced[item.value.index] with { UnitPrice = amount };
            }
        }
        return priced;
    }

    private static void ValidateHeader(Guid entity, string currency) { if (entity == Guid.Empty || string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3) throw new SalesException("sales.header_invalid"); }
}
