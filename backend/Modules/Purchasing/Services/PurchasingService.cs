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
using Shared.Contracts.Tax;
using Shared.Contracts.Inventory;
using Shared.Primitives.Identifiers;

namespace Modules.Purchasing.Services;

public sealed class PurchasingService(
    PurchasingRepository repository,
    IDirectoryPartyRoleCapabilities partyRoles,
    IPricingCapabilities pricing,
    IInvoicingSourceCapabilities invoicing,
    ITaxCapabilities tax,
    IInvoiceComplianceSnapshotCapability invoiceSnapshots,
    TimeProvider clock,
    IInventoryPurchasingCapabilities? inventory = null,
    IInvoicingCorrectionCapabilities? corrections = null) : Shared.Contracts.Purchasing.IPurchasingSourceCapabilities, Shared.Contracts.Purchasing.IPurchasingFixedAssetCapabilities, Shared.Contracts.Purchasing.IPurchasingReadCapabilities, Shared.Contracts.Purchasing.IPurchasingLandedCostCapabilities
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

    public Task<PurchaseOrderView?> GetAsync(TenantId tenant, Guid entity, Guid id, bool includePrice, CancellationToken ct) => repository.GetAsync(tenant, entity, id, includePrice, ct);
    public Task<PurchaseOrderPage> ListAsync(TenantId tenant, Guid entity, string? state, string? search, int page, int pageSize, CancellationToken ct) => repository.ListAsync(tenant, entity, state, search, page, pageSize, ct);
    public Task<long> ApproveAsync(TenantId tenant, Guid actor, Guid entity, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct) => repository.ApproveAsync(tenant, actor, entity, id, request, correlation, ct);
    public Task<long> RejectAsync(TenantId tenant, Guid actor, Guid entity, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct) => repository.RejectAsync(tenant, actor, entity, id, request, correlation, ct);
    public Task<long> CancelAsync(TenantId tenant, Guid actor, Guid entity, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct) => repository.CancelAsync(tenant, actor, entity, id, request, correlation, ct);
    public Task<long> CloseAsync(TenantId tenant, Guid actor, Guid entity, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct) => repository.CloseAsync(tenant, actor, entity, id, request, correlation, ct);
    public Task<long> CloseShortAsync(TenantId tenant, Guid actor, Guid entity, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct) => repository.CloseShortAsync(tenant, actor, entity, id, request, correlation, ct);
    public Task<bool> IsPurchaseOrderApprovedAsync(TenantId tenantId, Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default) => repository.IsApprovedAsync(tenantId, legalEntityId, purchaseOrderId, cancellationToken);
    public async Task<Shared.Contracts.Purchasing.PurchaseOrderInvoiceSource?> GetInvoiceSourceAsync(TenantId tenantId, Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var source = await GetFinalizedCapitalizationSourceAsync(tenantId, legalEntityId, purchaseOrderId, cancellationToken);
        return source is null
            ? null
            : new(source.PurchaseOrderId, "SupplierInvoice", $"purchasing-invoice:{source.SupplierInvoiceId:D}", source.LegalEntityId,
                source.BranchId, source.SupplierPartyId, source.DocumentDate, source.PostingDate, source.TaxDate, source.CurrencyCode,
                source.Lines.Select(line => new Shared.Contracts.Purchasing.PurchaseOrderSourceLine(line.ProductVariantId, line.UnitOfMeasureId,
                    line.UnitCode, line.Description, line.Quantity, line.FinalUnitPrice, line.SourceLineReference)).ToArray(), source.SupplierInvoiceNumber);
    }
    public async Task<Shared.Contracts.Purchasing.FixedAssetPurchasingSourceSnapshot?> GetFinalizedCapitalizationSourceAsync(TenantId tenantId, Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var order = await repository.GetAsync(tenantId, legalEntityId, purchaseOrderId, true, cancellationToken);
        if (order is null || order.State is PurchaseOrderStates.Draft or PurchaseOrderStates.PendingApproval or PurchaseOrderStates.Cancelled || order.ReceiptState is not ("Received" or "PartiallyReceived")) return null;
        var receiptIds = await repository.GetReceiptIdsAsync(tenantId, legalEntityId, purchaseOrderId, cancellationToken);
        if (receiptIds.Count == 0) return null;
        var orderLines = order.Lines.ToDictionary(line => line.Id);
        foreach (var invoiceId in await repository.GetLinkedInvoiceIdsAsync(tenantId, legalEntityId, purchaseOrderId, cancellationToken))
        {
            var evidence = await invoiceSnapshots.GetInvoiceComplianceSnapshotAsync(tenantId, legalEntityId, invoiceId, cancellationToken);
            if (evidence is null || !string.Equals(evidence.Direction, "Payable", StringComparison.OrdinalIgnoreCase) || evidence.Lines.Count == 0) continue;
            var lines = new List<Shared.Contracts.Purchasing.FixedAssetPurchasingSourceLine>();
            var total = 0m;
            var invalidEligibleLine = false;
            foreach (var line in evidence.Lines)
            {
                if (!decimal.TryParse(line.NetAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var net) || net <= 0m) { invalidEligibleLine = true; continue; }
                if (string.IsNullOrWhiteSpace(line.SourceLineReference) || !decimal.TryParse(line.Quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0m) { invalidEligibleLine = true; continue; }
                if (!Guid.TryParse(line.SourceLineReference, out var sourceLineId) || !orderLines.TryGetValue(sourceLineId, out var orderLine) || quantity > orderLine.ReceivedQuantity + 0.000001m) { invalidEligibleLine = true; continue; }
                lines.Add(new(line.ProductVariantId, line.UnitOfMeasureId, line.UnitCode, line.Description, line.Quantity, line.FinalUnitPrice, line.NetAmount, line.SourceLineReference, orderLine.ReceivedQuantity));
                total += net;
            }
            if (invalidEligibleLine || total <= 0m || lines.Count == 0) continue;
            return new(purchaseOrderId, invoiceId, receiptIds, legalEntityId, order.BranchId, order.SupplierPartyId,
                evidence.DocumentDate, evidence.PostingDate, evidence.TaxDate, evidence.CurrencyCode,
                evidence.DocumentNumber, total, lines, evidence, order.ReceiptState);
        }
        return null;
    }
    public async Task<Shared.Contracts.Purchasing.PurchaseLandedCostContext?> GetLandedCostContextAsync(TenantId tenantId, Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var order = await repository.GetAsync(tenantId, legalEntityId, purchaseOrderId, true, cancellationToken);
        return order is null ? null : new(order.Id, order.LegalEntityId, order.SupplierPartyId, order.CurrencyCode, order.Lines.Select(line => new Shared.Contracts.Purchasing.PurchaseLandedCostLine(line.Id, line.ProductVariantId, line.ReceivedQuantity, line.UnitPrice, order.CurrencyCode)).ToArray());
    }

    public async Task<Modules.Purchasing.DTOs.PurchaseReceiptResult> ReceiveAsync(TenantId tenant, Guid actor, Guid entity, Guid id, ReceivePurchaseOrderRequest request, Guid correlation, CancellationToken ct)
    {
        if (request.LegalEntityId != entity || request.Lines.Count == 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new PurchasingException("purchasing.receipt_request_invalid");
        var replay = await repository.GetReceiptReplayAsync(tenant, entity, id, request.IdempotencyKey, ct); if (replay is not null) return replay;
        var order = await repository.GetAsync(tenant, entity, id, ct) ?? throw new PurchasingException("purchasing.po_not_found");
        if (order.State is PurchaseOrderStates.Draft or PurchaseOrderStates.PendingApproval or PurchaseOrderStates.Cancelled or PurchaseOrderStates.Closed) throw new PurchasingException("purchasing.receipt_not_allowed");
        if (inventory is null) throw new PurchasingException("purchasing.inventory_not_available");
        var result = await inventory.ReceivePurchaseAsync(tenant.Value, actor, new(request.LegalEntityId, id, request.IdempotencyKey, request.OperationalDate, request.PostingDate, request.Lines.Select(line => new Shared.Contracts.Inventory.PurchaseReceiptLine(line.PurchaseOrderLineId, line.ProductVariantId, line.WarehouseId, line.LocationId, line.UnitOfMeasureId, line.Quantity, line.LotId, line.SerialId)).ToArray()), ct);
        return await repository.ApplyReceiptAsync(tenant, actor, entity, id, request, result, correlation, ct);
    }

    public async Task<Guid> ApproveAndBillAsync(TenantId tenant, Guid actor, Guid membership, Guid entity, Guid id, PurchaseOrderCommandRequest request, CreateSupplierInvoiceRequest invoice, Guid correlation, CancellationToken ct)
    {
        var order = await repository.GetAsync(tenant, entity, id, ct) ?? throw new PurchasingException("purchasing.po_not_found");
        if (order.State is PurchaseOrderStates.Draft or PurchaseOrderStates.PendingApproval) await ApproveAsync(tenant, actor, entity, id, request, correlation, ct);
        var approved = await repository.GetAsync(tenant, entity, id, ct) ?? throw new PurchasingException("purchasing.po_not_found");
        return await CreateSupplierInvoiceAsync(tenant, actor, membership, entity, id, invoice with { Version = approved.Version }, correlation, ct);
    }

    public async Task<Guid> ReceiveAndBillAsync(TenantId tenant, Guid actor, Guid membership, Guid entity, Guid id, ReceivePurchaseOrderRequest receipt, CreateSupplierInvoiceRequest invoice, Guid correlation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(receipt.IdempotencyKey) || receipt.IdempotencyKey.Length > 200) throw new PurchasingException("purchasing.idempotency_key_invalid");
        var workflowKey = receipt.IdempotencyKey;
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { receipt, invoice }, Json)))).ToLowerInvariant();
        var existing = await repository.GetWorkflowAsync(tenant, entity, id, workflowKey, ct);
        var workflow = existing ?? await repository.StartWorkflowAsync(tenant, entity, id, workflowKey, payloadHash, JsonSerializer.Serialize(receipt, Json), JsonSerializer.Serialize(invoice, Json), ct);
        if (!string.Equals(workflow.PayloadHash, payloadHash, StringComparison.Ordinal)) throw new PurchasingException("purchasing.idempotency_conflict");
        if (workflow.State == "Completed" && workflow.InvoiceId is { } completed) return completed;
        var receiptCompleted = workflow.State == "ReceiptCompleted" || workflow.State == "InvoicePending";
        try
        {
            if (!receiptCompleted)
            {
                await ReceiveAsync(tenant, actor, entity, id, receipt, correlation, ct);
                await repository.SetWorkflowStateAsync(tenant, entity, id, workflowKey, "ReceiptCompleted", null, null, ct);
                receiptCompleted = true;
            }
            var received = await repository.GetAsync(tenant, entity, id, ct) ?? throw new PurchasingException("purchasing.po_not_found");
            var invoiceId = await CreateSupplierInvoiceAsync(tenant, actor, membership, entity, id, invoice with { Version = received.Version }, correlation, ct);
            await repository.SetWorkflowStateAsync(tenant, entity, id, workflowKey, "Completed", invoiceId, null, ct);
            return invoiceId;
        }
        catch (Exception ex)
        {
            await repository.SetWorkflowStateAsync(tenant, entity, id, workflowKey, receiptCompleted ? "InvoicePending" : "Failed", null, ex.Message, ct);
            throw;
        }
    }

    public async Task<PurchaseMatchView> MatchAsync(TenantId tenant, Guid actor, Guid entity, Guid id, MatchPurchaseOrderRequest request, Guid correlation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200) throw new PurchasingException("purchasing.idempotency_key_invalid");
        var order = await repository.GetAsync(tenant, entity, id, ct) ?? throw new PurchasingException("purchasing.po_not_found");
        var invoice = await invoiceSnapshots.GetInvoiceComplianceSnapshotAsync(tenant, entity, request.SupplierInvoiceId, ct) ?? throw new PurchasingException("purchasing.invoice_not_found");
        var quantityVariance = 0m; var priceVariance = 0m; var blocking = new List<string>();
        if (!string.Equals(order.CurrencyCode, invoice.CurrencyCode, StringComparison.OrdinalIgnoreCase)) blocking.Add("currency_mismatch");
        var invoiceBySource = invoice.Lines.Where(x => Guid.TryParse(x.SourceLineReference, out _)).ToDictionary(x => Guid.Parse(x.SourceLineReference!), x => x);
        foreach (var line in order.Lines)
        {
            if (!invoiceBySource.TryGetValue(line.Id, out var invoiceLine)) { blocking.Add("line_missing:" + line.Id); continue; }
            if (line.ProductVariantId != invoiceLine.ProductVariantId) blocking.Add("product_mismatch:" + line.Id);
            if (line.UnitOfMeasureId != invoiceLine.UnitOfMeasureId) blocking.Add("uom_mismatch:" + line.Id);
            var invoiceQuantity = decimal.Parse(invoiceLine.Quantity, CultureInfo.InvariantCulture);
            var comparisonQuantity = request.Basis == "ThreeWay" ? line.ReceivedQuantity : line.OrderedQuantity;
            quantityVariance += Math.Abs(comparisonQuantity - invoiceQuantity);
            priceVariance += Math.Abs(line.UnitPrice * invoiceQuantity - decimal.Parse(invoiceLine.FinalUnitPrice, CultureInfo.InvariantCulture) * invoiceQuantity);
        }
        var taxVariance = Math.Abs(decimal.Parse(invoice.TaxAmount, CultureInfo.InvariantCulture));
        var variance = quantityVariance + priceVariance + taxVariance;
        var within = blocking.Count == 0 && quantityVariance <= request.QuantityTolerance && (priceVariance <= request.PriceAbsoluteTolerance || (order.NetTotal == 0 || priceVariance / order.NetTotal * 100m <= request.PricePercentageTolerance)) && taxVariance <= request.TaxTolerance;
        var state = request.Basis == "Disabled" && blocking.Count == 0 ? "Matched" : within ? "WithinTolerance" : request.ApproveException && !string.IsNullOrWhiteSpace(request.Reason) && blocking.Count == 0 ? "ApprovedException" : blocking.Count > 0 ? "Blocked" : "Variance";
        var details = JsonSerializer.Serialize(new { quantityVariance, priceVariance, taxVariance, blocking, request.Basis, request.QuantityTolerance, request.PriceAbsoluteTolerance, request.PricePercentageTolerance, request.TaxTolerance, invoice.SchemaVersion, invoice.CurrencyCode });
        return await repository.SaveMatchAsync(tenant, actor, entity, id, request, state, variance, details, correlation, ct);
    }

    public Task<Guid> CreateReturnAsync(TenantId tenant, Guid actor, Guid entity, Guid orderId, CreateSupplierReturnRequest request, CancellationToken ct) => repository.CreateReturnAsync(tenant, actor, entity, orderId, request, ct);
    public Task<SupplierReturnView?> GetReturnAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.GetReturnAsync(tenant, entity, id, ct);
    public Task<long> ApproveReturnAsync(TenantId tenant, Guid actor, Guid entity, Guid id, long version, CancellationToken ct) => repository.ApproveReturnAsync(tenant, actor, entity, id, version, ct);
    public async Task<SupplierReturnView> ExecuteReturnAsync(TenantId tenant, Guid actor, Guid membership, Guid entity, Guid id, CancellationToken ct)
    {
        var request = await repository.GetReturnAsync(tenant, entity, id, ct) ?? throw new PurchasingException("purchasing.return_not_found");
        if (request.State == "Executed") return request;
        var operations = new List<Guid>(); Guid? creditNoteId = null;
        if (request.Resolution == "PhysicalReturn")
        {
            if (inventory is null) throw new PurchasingException("purchasing.inventory_not_available");
            var returnLines = await repository.GetReturnLinesAsync(tenant, entity, id, ct);
            if (returnLines.Any(line => line.ProductVariantId is null || line.UnitOfMeasureId is null || line.WarehouseId is null)) throw new PurchasingException("purchasing.return_inventory_context_required");
            operations.AddRange(await inventory.ReturnPurchaseAsync(tenant.Value, actor, new(entity, request.Id, "return:" + request.Id.ToString("D"), DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), returnLines.Select(line => new Shared.Contracts.Inventory.PurchaseReturnLine(line.PurchaseOrderLineId, line.ProductVariantId!.Value, line.WarehouseId!.Value, line.LocationId, line.UnitOfMeasureId!.Value, line.Quantity, line.LotId, line.SerialId)).ToArray()), ct));
        }
        if (request.Resolution is "SupplierCredit" or "ServiceCredit")
        {
            if (corrections is null) throw new PurchasingException("purchasing.invoicing_correction_unavailable");
            var originalInvoiceId = await repository.GetReturnInvoiceAsync(tenant, entity, id, ct) ?? throw new PurchasingException("purchasing.return_invoice_required");
            var snapshot = await invoiceSnapshots.GetInvoiceComplianceSnapshotAsync(tenant, entity, originalInvoiceId, ct) ?? throw new PurchasingException("purchasing.invoice_not_found");
            var returnLines = await repository.GetReturnLinesAsync(tenant, entity, id, ct);
            var creditLines = returnLines.Select(line => snapshot.Lines.FirstOrDefault(invoiceLine => invoiceLine.SourceLineReference == line.PurchaseOrderLineId.ToString("D"))).ToArray();
            if (creditLines.Any(line => line is null)) throw new PurchasingException("purchasing.return_invoice_line_not_found");
            creditNoteId = await corrections.CreateCreditNoteDraftAsync(tenant, actor, membership, new InvoiceCreditNoteRequest(originalInvoiceId, entity, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), null, request.Reason, "return-credit:" + id.ToString("D"), returnLines.Zip(creditLines!, (line, invoiceLine) => new InvoiceCreditNoteLineContract(invoiceLine!.LineId, line.Quantity)).ToArray()), Guid.CreateVersion7(), ct);
        }
        await repository.CompleteReturnAsync(tenant, entity, id, operations, creditNoteId, ct);
        return await repository.GetReturnAsync(tenant, entity, id, ct) ?? request;
    }

    public async Task<Guid> CreateSupplierInvoiceAsync(TenantId tenant, Guid actor, Guid membership, Guid legalEntityId, Guid id, CreateSupplierInvoiceRequest request, Guid correlation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200) throw new PurchasingException("purchasing.idempotency_key_invalid");
        var order = await repository.GetAsync(tenant, legalEntityId, id, true, ct) ?? throw new PurchasingException("purchasing.po_not_found");
        if (order.State is not (PurchaseOrderStates.Approved or PurchaseOrderStates.Open or PurchaseOrderStates.PartiallyCompleted)) throw new PurchasingException("purchasing.po_not_approved");
        if (order.Version != request.Version) throw new PurchasingException("purchasing.po_stale");
        var payloadHash = PayloadHash(order, request);
        var existing = await repository.GetLinkedInvoiceAsync(tenant, legalEntityId, id, request.IdempotencyKey, ct);
        if (existing is { } existingLink)
        { if (!string.Equals(existingLink.PayloadHash, payloadHash, StringComparison.Ordinal)) throw new PurchasingException("purchasing.idempotency_conflict"); return existingLink.InvoiceId; }
        var requestedLines = request.Lines is { Count: > 0 } ? request.Lines.ToDictionary(x => x.PurchaseOrderLineId, x => x.Quantity) : order.Lines.ToDictionary(x => x.Id, x => x.OrderedQuantity - x.InvoicedQuantity - x.CancelledQuantity);
        if (requestedLines.Any(x => x.Value <= 0)) throw new PurchasingException("purchasing.invoice_quantity_invalid");
        var sourcePurpose = order.ProjectId is { } projectId ? $"SupplierInvoice:Project:{projectId:D}" : "SupplierInvoice";
        var source = new InvoiceSourceDocumentContract("purchasing", id, sourcePurpose, request.IdempotencyKey, "Payable", legalEntityId, order.BranchId, order.SupplierPartyId, null, request.DocumentDate, request.PostingDate, request.TaxDate, null, order.CurrencyCode, "Exclusive", null, request.SupplierInvoiceNumber, null, order.Lines.Where(line => requestedLines.ContainsKey(line.Id)).Select(line => new InvoiceSourceLineContract(line.ProductVariantId, line.UnitOfMeasureId, line.UnitCode, line.Description, requestedLines[line.Id].ToString(CultureInfo.InvariantCulture), line.UnitPrice.ToString(CultureInfo.InvariantCulture), null, null, line.Id.ToString("D"))).ToArray());
        var invoiceId = await invoicing.CreateDraftFromSourceAsync(tenant, actor, membership, source, correlation, ct);
        return await repository.LinkInvoiceAsync(tenant, actor, legalEntityId, id, invoiceId, "SupplierInvoice", request.IdempotencyKey, payloadHash, requestedLines, ct);
    }

    public Task<long> SaveProfileAsync(TenantId tenant, Guid actor, SupplierPurchaseProfileRequest request, CancellationToken ct) => SaveProfileCheckedAsync(tenant, actor, request, ct);
    private async Task<long> SaveProfileCheckedAsync(TenantId tenant, Guid actor, SupplierPurchaseProfileRequest request, CancellationToken ct)
    { await EnsureSupplierAsync(tenant, request.SupplierPartyId, request.LegalEntityId, ct); return await repository.SaveProfileAsync(tenant, actor, request, ct); }

    private async Task<IReadOnlyList<PurchasingRepository.ResolvedLine>> ResolveLinesAsync(TenantId tenant, CreatePurchaseOrderRequest request, bool canOverridePrice, CancellationToken ct)
    {
        var result = new List<PurchasingRepository.ResolvedLine>(request.Lines.Count);
        var resolvedPrices = new Dictionary<int, (decimal Price, string Evidence)>();
        var pricingInputs = request.Lines.Select((line, index) => (line, index)).Where(x => x.line.UnitPrice is null && x.line.ProductVariantId is { } && x.line.ProductTemplateId is { } && x.line.UnitOfMeasureId is { }).ToArray();
        var pricingContexts = pricingInputs.Select(x => new PricingContext(tenant, new(request.LegalEntityId), PricingDirection.Buying, request.OrderDate, request.CurrencyCode.Trim().ToUpperInvariant(), request.PriceListId, request.SupplierPartyId, null, x.line.ProductVariantId!.Value, x.line.ProductTemplateId!.Value, Array.Empty<Guid>(), null, x.line.UnitOfMeasureId!.Value, x.line.Quantity)).ToArray();
        if (pricingContexts.Length > 0)
        {
            var pricingResults = await pricing.ResolveBatchAsync(pricingContexts, ct);
            for (var index = 0; index < pricingResults.Count; index++)
            {
                var snapshot = pricingResults[index].Snapshot;
                if (snapshot is not null && decimal.TryParse(snapshot.FinalUnitPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)) resolvedPrices[pricingInputs[index].index] = (price, JsonSerializer.Serialize(snapshot));
            }
        }
        var taxContexts = new List<TaxContextContract>();
        for (var lineIndex = 0; lineIndex < request.Lines.Count; lineIndex++)
        {
            var line = request.Lines[lineIndex];
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
                if (!resolvedPrices.TryGetValue(lineIndex, out var resolved)) throw new PurchasingException("purchasing.buy_price_missing");
                unitPrice = resolved.Price; evidence = resolved.Evidence;
            }
            else throw new PurchasingException("purchasing.buy_price_required");
            taxContexts.Add(new(tenant, new(request.LegalEntityId), "Payable", "PurchaseOrder", "Purchase", request.OrderDate, request.OrderDate, null, request.OrderDate, request.SupplierPartyId, "EG", "EG", null, line.ProductTemplateId, line.ProductVariantId, line.Quantity, line.UnitOfMeasureId, "Exclusive", (line.Quantity * unitPrice).ToString(CultureInfo.InvariantCulture), "0", "0", request.CurrencyCode.Trim().ToUpperInvariant(), request.CurrencyCode.Trim().ToUpperInvariant(), null, DocumentSemantics: "PurchaseOrder"));
            result.Add(new(line.ProductVariantId, line.UnitOfMeasureId, line.UnitCode, line.Description.Trim(), line.Quantity, unitPrice, evidence, line.PriceOverrideReason));
        }
        if (taxContexts.Count > 0)
        {
            var taxes = await tax.CalculateBatchAsync(taxContexts, ct);
            for (var index = 0; index < result.Count && index < taxes.Count; index++) result[index] = result[index] with { TaxEvidence = JsonSerializer.Serialize(taxes[index]) };
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
