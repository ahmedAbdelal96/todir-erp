using System.Globalization;
using System.Text.Json;
using Core.Transactions;
using Core.Services;
using Modules.Invoicing.DTOs;
using Modules.Invoicing.Entities;
using Modules.Invoicing.Repositories;
using Shared.Contracts.Catalog;
using Shared.Contracts.Directory;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Ledger;
using Shared.Contracts.Invoicing;
using Shared.Contracts.Pricing;
using Shared.Contracts.Platform;
using Shared.Contracts.Subledger;
using Shared.Contracts.Tax;
using Shared.Primitives.Identifiers;

namespace Modules.Invoicing.Services;

public sealed class InvoicingService(
    InvoicingRepository repository,
    IDirectoryCapabilities directory,
    ICatalogCapabilities catalog,
    IPricingCapabilities pricing,
    ITaxCapabilities tax,
    IFinanceBaseCapabilities financeBase,
    ISubledgerCapabilities subledger,
    ITransactionCoordinator transactions,
    ITransactionalSequenceAllocator sequences,
    ITransactionalInvoiceLedgerPostingCapability ledgerPosting,
    ITransactionalSubledgerOpenItemCapability subledgerPosting,
    IPlatformOrganizationCapabilities organization,
        TimeProvider clock,
    ITransactionalPlanningBudgetControlCapability? budgetControl = null) : IInvoicingSourceCapabilities, IServiceInvoiceReadCapability, IPosInvoicingCapabilities, IInvoicingCorrectionCapabilities, IInvoiceComplianceSnapshotCapability, IInvoiceFileReferenceCapability
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Guid> CreateDraftAsync(TenantId tenant, Guid actorId, Guid membershipId, CreateInvoiceDraftRequest request, InvoiceDirection direction, Guid correlationId, CancellationToken ct)
        => await CreateDraftCoreAsync(tenant, actorId, membershipId, request, direction, null, correlationId, ct);

    public async Task<Guid> CreateDraftFromSourceAsync(TenantId tenantId, Guid actorId, Guid membershipId, InvoiceSourceDocumentContract request,
        Guid correlationId, CancellationToken cancellationToken = default)
    {
        var mapped = InvoiceSourceRequestMapper.Map(request);
        return await CreateDraftCoreAsync(tenantId, actorId, membershipId, mapped.Draft, mapped.Direction, mapped.Reference, correlationId, cancellationToken);
    }

    public async Task<ServiceInvoiceState?> GetServiceInvoiceStateAsync(TenantId tenantId, Guid legalEntityId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await GetDraftAsync(tenantId, legalEntityId, invoiceId, cancellationToken);
        var source = invoice.Source;
        return new ServiceInvoiceState(invoice.Id, invoice.LegalEntityId, invoice.PartyId, invoice.CurrencyCode,
            invoice.Direction.ToString(), invoice.Lifecycle.ToString(), source?.SourceDocumentId, source?.SourceModule);
    }

    public async Task<PosInvoiceResult> CreateAndFinalizeFromSourceAsync(TenantId tenantId, Guid actorId, Guid membershipId,
        InvoiceSourceDocumentContract request, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var invoiceId = await CreateDraftFromSourceAsync(tenantId, actorId, membershipId, request, correlationId, cancellationToken);
        var draft = await GetDraftAsync(tenantId, request.LegalEntityId, invoiceId, cancellationToken);
        var finalized = await FinalizeAsync(tenantId, actorId, membershipId, invoiceId, request.LegalEntityId,
            new InvoiceFinalizationRequest(draft.Version, request.IdempotencyKey + ":finalize"), correlationId, cancellationToken);
        return new(finalized.InvoiceId, finalized.DocumentNumber, finalized.JournalEntryId);
    }

    public async Task<PosInvoiceResult> CreateAndFinalizeCreditNoteAsync(TenantId tenantId, Guid actorId, Guid membershipId,
        InvoiceCreditNoteRequest request, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var invoiceId = await CreateCreditNoteDraftAsync(tenantId, actorId, membershipId, request, correlationId, cancellationToken);
        var draft = await GetDraftAsync(tenantId, request.LegalEntityId, invoiceId, cancellationToken);
        var finalized = await FinalizeAsync(tenantId, actorId, membershipId, invoiceId, request.LegalEntityId,
            new InvoiceFinalizationRequest(draft.Version, request.IdempotencyKey + ":finalize"), correlationId, cancellationToken);
        return new(finalized.InvoiceId, finalized.DocumentNumber, finalized.JournalEntryId);
    }

    public async Task<Guid> CreateCreditNoteDraftAsync(TenantId tenantId, Guid actorId, Guid membershipId,
        InvoiceCreditNoteRequest request, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var original = await repository.GetCorrectionSourceAsync(tenantId, request.LegalEntityId, request.OriginalInvoiceId, cancellationToken)
            ?? throw NotFound();
        if (!Enum.TryParse<InvoiceDirection>(original.Direction, ignoreCase: true, out var direction))
            throw new InvoiceException("invoicing.correction_not_allowed", "The original invoice has an invalid workflow direction.");

        return await CreateCorrectionDraftAsync(tenantId, actorId, membershipId, request.OriginalInvoiceId, request.LegalEntityId,
            new InvoiceCorrectionRequest("CreditNote", request.Reason, request.DocumentDate, request.PostingDate,
                request.TaxDate, request.SupplyDate, request.IdempotencyKey,
                request.Lines.Select(line => new InvoiceCorrectionLineInput(line.OriginalLineId,
                    line.Quantity.ToString("0.######", CultureInfo.InvariantCulture))).ToArray()),
            direction, correlationId, cancellationToken);
    }

    private async Task<Guid> CreateDraftCoreAsync(TenantId tenant, Guid actorId, Guid membershipId, CreateInvoiceDraftRequest request, InvoiceDirection direction,
        InvoiceSourceReferenceContract? source, Guid correlationId, CancellationToken ct)
    {
        if (source is not null && await repository.GetExistingSourceInvoiceIdAsync(tenant, request.LegalEntityId, source, ct) is { } existingInvoiceId)
            return existingInvoiceId;
        ValidateHeader(request.LegalEntityId, request.DocumentDate, request.PostingDate, request.TaxDate, request.CurrencyCode, request.PriceMode);
        await ValidateBranchAsync(tenant, request.LegalEntityId, request.BranchId, ct);
        var party = await RequirePartyAsync(tenant, request.PartyId, request.LegalEntityId, direction, ct);
        ValidateAddress(party, request.AddressId);
        var lines = ParseLines(request.Lines);
        ValidateLines(lines);
        var charges = ParseCharges(request.Charges);
        ValidateSupplierNumber(direction, request.SupplierInvoiceNumber);
        _ = new InvoiceAggregate(Guid.CreateVersion7(), tenant.Value, request.LegalEntityId, request.BranchId, direction, InvoiceDocumentType.Invoice, request.PartyId,
            request.AddressId, request.DocumentDate, request.PostingDate, request.TaxDate, request.SupplyDate, request.CurrencyCode, request.PaymentTermId,
            request.SupplierInvoiceNumber, 1, InvoiceLifecycle.Draft, lines, ParseOptionalAmount(request.DocumentDiscountAmount), charges, request.PriceMode, source);
        return await repository.CreateDraftAsync(tenant, actorId, membershipId, request, direction.ToString(), party.DisplayName, source, correlationId, ct);
    }

    public async Task<Guid> CreateCorrectionDraftAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid originalInvoiceId, Guid legalEntityId,
        InvoiceCorrectionRequest request, InvoiceDirection expectedDirection, Guid correlationId, CancellationToken ct)
    {
        var documentType = request.DocumentType switch
        {
            "CreditNote" => InvoiceDocumentType.CreditNote,
            "DebitNote" => InvoiceDocumentType.DebitNote,
            _ => throw new InvoiceException("invoicing.correction_type_invalid", "Choose a credit note or debit note correction.")
        };
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3 || request.Reason.Length > 500)
            throw new InvoiceException("invoicing.correction_reason_required", "A clear reason is required for a correction document.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 200)
            throw new InvoiceException("invoicing.idempotency_key_invalid", "A valid correction request key is required.");
        var original = await repository.GetCorrectionSourceAsync(tenant, legalEntityId, originalInvoiceId, ct) ?? throw NotFound();
        if (original.Lifecycle != InvoiceLifecycle.Finalized.ToString() || original.DocumentType == InvoiceDocumentType.CreditNote.ToString()
            || original.Direction != expectedDirection.ToString() || original.GrossTotal is null)
            throw new InvoiceException("invoicing.correction_not_allowed", "A correction must reference a finalized invoice or debit note in the same workflow.");
        ValidateHeader(legalEntityId, request.DocumentDate, request.PostingDate, request.TaxDate, original.CurrencyCode, original.PriceMode);
        if (request.Lines is null || request.Lines.Count is < 1 or > 1000)
            throw new InvoiceException("invoicing.correction_lines_invalid", "Select at least one original invoice line to correct.");
        if (request.Lines.Select(line => line.OriginalLineId).Distinct().Count() != request.Lines.Count)
            throw new InvoiceException("invoicing.correction_lines_invalid", "An original invoice line can appear only once in a correction.");
        var originalLines = original.Lines.ToDictionary(line => line.Id);
        var lines = new List<InvoiceLineInput>(request.Lines.Count);
        foreach (var corrected in request.Lines)
        {
            if (!originalLines.TryGetValue(corrected.OriginalLineId, out var sourceLine) || sourceLine.FinalUnitPrice is null)
                throw new InvoiceException("invoicing.correction_line_not_found", "Every correction line must reference an issued line with a saved price.");
            var quantity = ParseAmount(corrected.Quantity, "invoicing.quantity_invalid");
            if (quantity <= 0m || quantity > sourceLine.OriginalQuantity)
                throw new InvoiceException("invoicing.correction_quantity_invalid", "Correction quantity must be positive and cannot exceed the original issued quantity.");
            if (documentType == InvoiceDocumentType.CreditNote && quantity > sourceLine.OriginalQuantity - sourceLine.PreviouslyCreditedQuantity)
                throw new InvoiceException("invoicing.credit_quantity_exceeded", "The requested credit quantity exceeds the quantity that remains available on the original invoice.");
            var lineDiscount = sourceLine.OriginalQuantity == 0m ? 0m : sourceLine.LineDiscount * quantity / sourceLine.OriginalQuantity;
            lines.Add(new(sourceLine.ProductVariantId, sourceLine.UnitOfMeasureId, sourceLine.UnitCode, sourceLine.Description, D(quantity),
                D(sourceLine.FinalUnitPrice.Value), D(lineDiscount), sourceLine.Id.ToString("D"),
                $"Issued-document correction: {request.Reason.Trim()}", sourceLine.BusinessCategoryCode));
        }
        var party = await RequirePartyAsync(tenant, original.PartyId, legalEntityId, expectedDirection, ct);
        var draft = new CreateInvoiceDraftRequest(legalEntityId, original.BranchId, original.PartyId, original.AddressId, request.DocumentDate,
            request.PostingDate, request.TaxDate, request.SupplyDate, original.CurrencyCode, original.PaymentTermId, null, lines,
            request.DocumentDiscountAmount, request.Charges, original.PriceMode);
        await ValidateBranchAsync(tenant, legalEntityId, original.BranchId, ct);
        var parsed = ParseLines(draft.Lines);
        ValidateLines(parsed);
        _ = new InvoiceAggregate(Guid.CreateVersion7(), tenant.Value, legalEntityId, original.BranchId, expectedDirection, documentType,
            original.PartyId, original.AddressId, request.DocumentDate, request.PostingDate, request.TaxDate, request.SupplyDate,
            original.CurrencyCode, original.PaymentTermId, null, 1, InvoiceLifecycle.Draft, parsed, ParseOptionalAmount(request.DocumentDiscountAmount),
            ParseCharges(request.Charges), original.PriceMode);
        var payloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            originalInvoiceId,
            documentType = documentType.ToString(),
            reason = request.Reason.Trim(),
            request.DocumentDate,
            request.PostingDate,
            request.TaxDate,
            request.SupplyDate,
            lines,
            request.DocumentDiscountAmount,
            charges = request.Charges
        }, JsonOptions))).ToLowerInvariant();
        return await repository.CreateCorrectionDraftAsync(tenant, actorId, membershipId, originalInvoiceId, draft, expectedDirection.ToString(),
            documentType.ToString(), request.Reason, request.IdempotencyKey.Trim(), payloadHash, party.DisplayName, correlationId, ct);
    }

    public async Task<long> UpdateDraftAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid invoiceId, Guid legalEntityId, UpdateInvoiceDraftRequest request, Guid correlationId, CancellationToken ct)
    {
        var draft = await repository.GetAggregateAsync(tenant, legalEntityId, invoiceId, ct) ?? throw NotFound();
        draft.EnsureDraftVersion(request.Version);
        ValidateHeader(legalEntityId, request.DocumentDate, request.PostingDate, request.TaxDate, request.CurrencyCode, request.PriceMode);
        await ValidateBranchAsync(tenant, legalEntityId, draft.BranchId, ct);
        var party = await RequirePartyAsync(tenant, request.PartyId, legalEntityId, draft.Direction, ct);
        ValidateAddress(party, request.AddressId);
        var lines = ParseLines(request.Lines);
        ValidateLines(lines);
        var charges = ParseCharges(request.Charges);
        ValidateSupplierNumber(draft.Direction, request.SupplierInvoiceNumber);
        _ = new InvoiceAggregate(Guid.CreateVersion7(), tenant.Value, legalEntityId, draft.BranchId, draft.Direction, draft.DocumentType, request.PartyId,
            request.AddressId, request.DocumentDate, request.PostingDate, request.TaxDate, request.SupplyDate, request.CurrencyCode, request.PaymentTermId,
            request.SupplierInvoiceNumber, request.Version, InvoiceLifecycle.Draft, lines, ParseOptionalAmount(request.DocumentDiscountAmount), charges, request.PriceMode);
        return await repository.UpdateDraftAsync(tenant, invoiceId, legalEntityId, actorId, membershipId, request, party.DisplayName, draft.Direction.ToString(), correlationId, ct);
    }

    public Task CancelDraftAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid invoiceId, Guid legalEntityId, CancelInvoiceDraftRequest request, Guid correlationId, CancellationToken ct)
        => repository.CancelDraftAsync(tenant, invoiceId, legalEntityId, request.Version, actorId, membershipId, request.Reason, correlationId, ct);

    public Task<InvoicePage> ListAsync(TenantId tenant, InvoiceListQuery query, InvoiceDirection direction, CancellationToken ct)
        => repository.ListAsync(tenant, query, direction.ToString(), ct);

    public Task<Guid?> GetBranchIdAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, CancellationToken ct)
        => repository.GetBranchIdAsync(tenant, legalEntityId, invoiceId, ct);

    public Task<InvoiceComplianceSnapshotContract?> GetInvoiceComplianceSnapshotAsync(TenantId tenantId, Guid legalEntityId, Guid invoiceId,
        CancellationToken cancellationToken = default) => repository.GetComplianceSnapshotAsync(tenantId, legalEntityId, invoiceId, cancellationToken);

    public Task<Guid> AddAttachmentReferenceAsync(TenantId tenantId, Guid actorId, Guid membershipId, InvoiceAttachmentReferenceRequest request,
        Guid correlationId, CancellationToken cancellationToken = default)
    {
        var file = request.File ?? throw new InvoiceException("invoicing.file_reference_invalid", "A verified file reference from Platform Files is required for this tenant.");
        ValidateFileReference(tenantId, file);
        return repository.AddAttachmentReferenceAsync(tenantId, actorId, membershipId,
            request with { File = file with { Sha256 = file.Sha256.ToLowerInvariant() } }, correlationId, cancellationToken);
    }

    public Task<Guid> RecordIssuedArtifactReferenceAsync(TenantId tenantId, Guid actorId, InvoiceIssuedArtifactReferenceRequest request,
        Guid correlationId, CancellationToken cancellationToken = default)
    {
        var file = request.File ?? throw new InvoiceException("invoicing.file_reference_invalid", "A verified file reference from Platform Files is required for this tenant.");
        var languageCode = request.LanguageCode;
        ValidateFileReference(tenantId, file);
        if (string.IsNullOrWhiteSpace(request.TemplateCode) || request.TemplateCode.Trim().Length > 100 || string.IsNullOrWhiteSpace(request.TemplateVersion)
            || request.TemplateVersion.Trim().Length > 100 || !System.Text.RegularExpressions.Regex.IsMatch(languageCode ?? "", "^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$"))
            throw new InvoiceException("invoicing.artifact_metadata_invalid", "A valid render template version and language are required.");
        return repository.RecordIssuedArtifactReferenceAsync(tenantId, actorId,
            request with { File = file with { Sha256 = file.Sha256.ToLowerInvariant() }, TemplateCode = request.TemplateCode.Trim(), TemplateVersion = request.TemplateVersion.Trim(), LanguageCode = languageCode!.ToLowerInvariant() },
            correlationId, cancellationToken);
    }

    public Task<InvoiceIssuedArtifactRecord?> GetOriginalIssuedArtifactAsync(TenantId tenantId, Guid legalEntityId, Guid invoiceId, string templateCode,
        string templateVersion, string languageCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateCode) || templateCode.Trim().Length > 100 || string.IsNullOrWhiteSpace(templateVersion)
            || templateVersion.Trim().Length > 100 || string.IsNullOrWhiteSpace(languageCode)
            || !System.Text.RegularExpressions.Regex.IsMatch(languageCode, "^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$"))
            throw new InvoiceException("invoicing.artifact_metadata_invalid", "A valid artifact template version and language are required.");
        return repository.GetOriginalIssuedArtifactAsync(tenantId, legalEntityId, invoiceId, templateCode.Trim(), templateVersion.Trim(), languageCode.ToLowerInvariant(), cancellationToken);
    }

    private static void ValidateFileReference(TenantId tenant, Shared.Contracts.Platform.VerifiedPlatformFileReference? file)
    {
        if (file is null || file.TenantId != tenant || file.FileRef == Guid.Empty || file.SizeBytes < 0 || string.IsNullOrWhiteSpace(file.ContentType)
            || !System.Text.RegularExpressions.Regex.IsMatch(file.Sha256 ?? "", "^[0-9a-fA-F]{64}$"))
            throw new InvoiceException("invoicing.file_reference_invalid", "A verified file reference from Platform Files is required for this tenant.");
    }

    public async Task<bool> IsPaymentTermOverrideAsync(TenantId tenant, Guid legalEntityId, Guid partyId, Guid? requestedTermId, InvoiceDirection direction, CancellationToken ct)
    {
        if (requestedTermId is null) return false;
        var profile = await subledger.GetFinancialProfileAsync(tenant, new(legalEntityId), partyId, ct);
        var defaultTermId = direction == InvoiceDirection.Receivable ? profile?.DefaultReceivablePaymentTermId : profile?.DefaultPayablePaymentTermId;
        return requestedTermId != defaultTermId;
    }

    public async Task<InvoiceDetail> GetDetailAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, CancellationToken ct)
    {
        var detail = await repository.GetDetailAsync(tenant, legalEntityId, invoiceId, ct) ?? throw NotFound();
        if (detail.Lifecycle != InvoiceLifecycle.Finalized.ToString()) return detail;
        var sourceType = detail.DocumentType switch
        {
            "CreditNote" => "CreditNote",
            _ when detail.Direction == InvoiceDirection.Receivable.ToString() => "Invoice",
            _ => "Bill"
        };
        var settlement = await subledger.GetSourceSettlementSummaryAsync(tenant, new(legalEntityId), sourceType, invoiceId, ct);
        return detail with { SettlementSummary = settlement?.Status ?? "Settled" };
    }

    public async Task<InvoiceRenderModel> GetRenderModelAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, CancellationToken ct)
    {
        var invoice = await GetDetailAsync(tenant, legalEntityId, invoiceId, ct);
        if (invoice.Lifecycle != InvoiceLifecycle.Finalized.ToString() || invoice.DocumentNumber is null || invoice.Totals is null)
            throw new InvoiceException("invoicing.render_requires_finalized", "Only finalized invoices have a frozen render model.");
        return new(invoice.Id, invoice.DocumentNumber, invoice.Direction, invoice.DocumentType, invoice.PartyDisplayName, invoice.PartyLegalName,
            invoice.PartyTradeName, invoice.AddressEvidenceJson, invoice.DocumentDate, invoice.PostingDate, invoice.TaxDate, invoice.SupplyDate,
            invoice.CurrencyCode, invoice.PriceMode, invoice.Totals, invoice.Lines, invoice.PaymentSchedule, invoice.DocumentDiscountAmount,
            invoice.Charges ?? []);
    }

    public async Task<InvoiceAggregate> GetDraftAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, CancellationToken ct)
        => await repository.GetAggregateAsync(tenant, legalEntityId, invoiceId, ct) ?? throw NotFound();

    public async Task<InvoiceDraftResponse> GetDraftResponseAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, CancellationToken ct)
    {
        var invoice = await GetDraftAsync(tenant, legalEntityId, invoiceId, ct);
        return new(invoice.Id, invoice.LegalEntityId, invoice.BranchId, invoice.Direction.ToString(), invoice.PartyId, invoice.AddressId,
            invoice.DocumentDate, invoice.PostingDate, invoice.TaxDate, invoice.SupplyDate, invoice.CurrencyCode, invoice.PaymentTermId,
            invoice.SupplierInvoiceNumber, invoice.Lifecycle.ToString(), invoice.Version,
            invoice.Lines.Select(line => new InvoiceLineInput(line.ProductVariantId, line.UnitOfMeasureId, line.UnitCode, line.Description,
                D(line.Quantity), line.ExplicitUnitPrice, D(line.DiscountAmount), line.SourceLineReference, line.PriceOverrideReason, line.BusinessCategoryCode)).ToArray(), D(invoice.DocumentDiscountAmount),
            invoice.Charges.Select(charge => new InvoiceChargeInput(charge.Code, charge.Description, D(charge.Amount), charge.TaxTreatment, charge.BusinessCategoryCode)).ToArray(), invoice.PriceMode,
            invoice.Source is null ? null : new InvoiceSourceTrace(invoice.Source.SourceModule, invoice.Source.SourceDocumentId, invoice.Source.Purpose));
    }

    public async Task<InvoicePreview> PreviewAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, CancellationToken ct)
    {
        var draft = await repository.GetAggregateAsync(tenant, legalEntityId, invoiceId, ct) ?? throw NotFound();
        if (draft.Lifecycle != InvoiceLifecycle.Draft) throw new InvoiceException("invoicing.already_finalized", "A finalized invoice uses its frozen accounting evidence.");
        return await AddAccountingPreviewAsync(tenant, draft, await CalculateAsync(tenant, draft, ct), ct);
    }

    public async Task<InvoiceFinalizedResult> FinalizeAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid invoiceId, Guid legalEntityId,
        InvoiceFinalizationRequest request, Guid correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200)
            throw new InvoiceException("invoicing.idempotency_key_invalid", "A valid finalization request key is required.");
        var draft = await GetDraftAsync(tenant, legalEntityId, invoiceId, ct);
        var payloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{invoiceId:D}:{request.Version}"))).ToLowerInvariant();
        if (draft.Lifecycle != InvoiceLifecycle.Draft)
            return await repository.GetFinalizedResultAsync(tenant, legalEntityId, invoiceId, request.IdempotencyKey, payloadHash, ct);
        draft.EnsureDraftVersion(request.Version);
        var calculation = await CalculateAsync(tenant, draft, ct);
        if (!calculation.Preview.ReadyToFinalize)
        {
            var blocker = calculation.Preview.Blockers.Count == 0 ? null : calculation.Preview.Blockers[0];
            throw new InvoiceException(blocker?.Code ?? "invoicing.finalization_blocked", blocker?.Message ?? "The invoice is not ready to finalize.");
        }
        var data = ToFinalizationData(calculation);
        var periodKey = draft.DocumentDate.Year.ToString(CultureInfo.InvariantCulture);
        var sequenceKey = "invoicing.document_number";
        var sequencePrefix = draft.DocumentType switch
        {
            InvoiceDocumentType.CreditNote => $"CN-{periodKey}-",
            InvoiceDocumentType.DebitNote => $"DN-{periodKey}-",
            _ => draft.Direction == InvoiceDirection.Receivable ? $"INV-{periodKey}-" : $"BILL-{periodKey}-"
        };
        try
        {
            return await transactions.ExecuteAsync<InvoiceFinalizedResult>(tenant, async (context, token) =>
            {
                var prior = await InvoicingRepository.LockForFinalizationAsync(context, tenant, legalEntityId, invoiceId, request, payloadHash,
                    ParseAmount(data.Totals.GrossAmount, "invoicing.amount_invalid"), token);
                if (prior is not null)
                    return new(invoiceId, prior.DocumentNumber, "Finalized", prior.Totals, prior.Schedule, prior.JournalEntryId, prior.OpenItemIds,
                        "Submit this finalized document through the configured compliance workflow.");
                var documentNumber = await sequences.AllocateAsync(context,
                    new(sequenceKey, tenant, legalEntityId, draft.BranchId, draft.Direction.ToString(), periodKey, sequencePrefix, 6, 1), token);
                var configurationId = await ledgerPosting.GetActiveConfigurationVersionIdAsync(context, tenant, new(legalEntityId), token);
                var factType = InvoiceAccountingFactTypeResolver.Resolve(draft.Direction, draft.DocumentType);
                var ledgerPayload = BuildLedgerPayload(calculation);
                var fact = new AccountingFactEnvelope<IAccountingFactPayload>(Guid.CreateVersion7(), tenant, new(legalEntityId), factType, 1, "invoicing", invoiceId.ToString("D"),
                    request.IdempotencyKey, draft.DocumentDate, draft.PostingDate, ledgerPayload, correlationId, actorId);
                var posted = await ledgerPosting.PostInvoiceAsync(context, fact, ledgerPayload, token);
                await ApplyBudgetControlAsync(context, tenant, actorId, draft, invoiceId, request, data, token);
                var openItems = new List<Guid>();
                if (data.Schedule.Count > 0)
                {
                    var itemExchangeRate = calculation.Financial?.ExchangeRate is { } fx ? ParseAmount(fx.Rate, "invoicing.exchange_rate_invalid") : 1m;
                    var baseCurrency = calculation.Financial?.BaseCurrencyCode ?? draft.CurrencyCode;
                    var historicFx = calculation.Financial?.ExchangeRate is { } snapshot ? JsonSerializer.Serialize(snapshot) : JsonSerializer.Serialize(new { rate = "1", from = draft.CurrencyCode, to = baseCurrency });
                    var openItemSnapshots = data.Schedule.Select(line =>
                    {
                        var transactionAmount = ParseAmount(line.Amount, "invoicing.amount_invalid");
                        var baseAmount = RoundAmount(transactionAmount * itemExchangeRate, calculation.Financial?.RoundingPolicy.MoneyScale ?? 2,
                            calculation.Financial?.RoundingPolicy.ContextRules.FirstOrDefault(rule => rule.Context == FinancialRoundingContext.FxBaseAmount)?.Mode ?? FinancialRoundingMode.HalfEven);
                        return new OpenItemSnapshotContract(invoiceId.ToString("D"), line.Sequence.ToString(CultureInfo.InvariantCulture), documentNumber, draft.DocumentDate,
                            draft.PostingDate, line.DueDate, draft.CurrencyCode, line.Amount, baseCurrency, D(baseAmount), draft.Direction == InvoiceDirection.Receivable ? "Receivable" : "Payable",
                            draft.Direction == InvoiceDirection.Receivable ? "Receivable" : "Payable", $"configuration:{configurationId:D}", historicFx, draft.Version,
                            data.PaymentTermEvidenceJson);
                    }).ToArray();
                    var direction = draft.Direction == InvoiceDirection.Receivable ? SubledgerDirectionContract.Receivable : SubledgerDirectionContract.Payable;
                    var openItemCommand = new CreateOpenItemsCommand(new(legalEntityId), draft.PartyId, openItemSnapshots, actorId, correlationId);
                    if (draft.DocumentType == InvoiceDocumentType.CreditNote)
                        openItems.AddRange(await subledgerPosting.CreateInvoiceCorrectionOpenItemsAsync(context, tenant, openItemCommand, direction,
                            draft.Direction == InvoiceDirection.Receivable ? SubledgerPolarityContract.Credit : SubledgerPolarityContract.Debit, token));
                    else
                        openItems.AddRange(await subledgerPosting.CreateInvoiceOpenItemsAsync(context, tenant, openItemCommand, direction, token));
                }
                await repository.FinalizeInTransactionAsync(context, tenant, invoiceId, legalEntityId, draft.Direction.ToString(), draft.DocumentType.ToString(), documentNumber,
                    request, payloadHash, data, posted.JournalEntryId, openItems, actorId, membershipId, correlationId, sequenceKey, periodKey, token);
                return new(invoiceId, documentNumber, "Finalized", data.Totals, data.Schedule, posted.JournalEntryId, openItems,
                    "Submit this finalized document through the configured compliance workflow.");
            }, ct);
        }
        catch (Exception exception) when (InvoicingRepository.IsSupplierNumberConflict(exception))
        {
            throw new InvoiceException("invoicing.supplier_duplicate", "This supplier invoice number was already recorded for this supplier.", exception);
        }
    }

    private async Task ApplyBudgetControlAsync(ITransactionContext context, TenantId tenant, Guid actorId, InvoiceAggregate draft,
        Guid invoiceId, InvoiceFinalizationRequest request, InvoiceFinalizationData data, CancellationToken ct)
    {
        if (budgetControl is null || draft.Direction != InvoiceDirection.Payable) return;
        var source = await InvoicingRepository.GetBudgetControlSourceInTransactionAsync(context, tenant, draft.LegalEntityId, invoiceId, ct);
        if (source is null || !string.Equals(source.SourceModule, "purchasing", StringComparison.OrdinalIgnoreCase)) return;
        var amount = ParseAmount(data.Totals.NetAmount, "invoicing.amount_invalid");
        var sourceDocumentId = source.SourceDocumentId.ToString("D");
        if (draft.DocumentType == InvoiceDocumentType.CreditNote)
        {
            await budgetControl.ReverseConsumptionAsync(context, tenant, actorId,
                new(draft.LegalEntityId, "purchasing", sourceDocumentId, "order", "purchasing", invoiceId.ToString("D"), "order",
                    $"invoice-correction:{invoiceId:D}:{request.IdempotencyKey}", amount, draft.CurrencyCode), ct);
            return;
        }

        if (!await budgetControl.HasReservationAsync(context, tenant, draft.LegalEntityId, "purchasing", sourceDocumentId, "order", ct)) return;
        if (!await budgetControl.HasOpenReservationAsync(context, tenant, draft.LegalEntityId, "purchasing", sourceDocumentId, "order", ct))
            throw new PlanningBudgetControlException("planning.budget_reservation_not_open");
        await budgetControl.ConsumeReservationAsync(context, tenant, actorId,
            new(draft.LegalEntityId, "purchasing", sourceDocumentId, "order", $"invoice:{invoiceId:D}:{request.IdempotencyKey}", amount, draft.CurrencyCode), ct);
    }

    public async Task<InvoicePreview> PreviewDraftAsync(TenantId tenant, Guid legalEntityId, CreateInvoiceDraftRequest request, InvoiceDirection direction, CancellationToken ct)
    {
        ValidateHeader(legalEntityId, request.DocumentDate, request.PostingDate, request.TaxDate, request.CurrencyCode, request.PriceMode);
        await ValidateBranchAsync(tenant, legalEntityId, request.BranchId, ct);
        var party = await RequirePartyAsync(tenant, request.PartyId, legalEntityId, direction, ct);
        ValidateAddress(party, request.AddressId);
        var lines = ParseLines(request.Lines);
        ValidateLines(lines);
        var draft = new InvoiceAggregate(Guid.CreateVersion7(), tenant.Value, legalEntityId, request.BranchId, direction, InvoiceDocumentType.Invoice,
            request.PartyId, request.AddressId, request.DocumentDate, request.PostingDate, request.TaxDate, request.SupplyDate,
            request.CurrencyCode, request.PaymentTermId, request.SupplierInvoiceNumber, 1, InvoiceLifecycle.Draft, lines, ParseOptionalAmount(request.DocumentDiscountAmount), ParseCharges(request.Charges), request.PriceMode);
        return await AddAccountingPreviewAsync(tenant, draft, await CalculateAsync(tenant, draft, ct), ct);
    }

    private async Task<InvoicePreview> AddAccountingPreviewAsync(TenantId tenant, InvoiceAggregate draft, Calculation calculation, CancellationToken ct)
    {
        if (!calculation.Preview.ReadyToFinalize) return calculation.Preview;
        var payload = BuildLedgerPayload(calculation);
        var factType = InvoiceAccountingFactTypeResolver.Resolve(draft.Direction, draft.DocumentType);
        var actor = Guid.CreateVersion7();
        var correlation = Guid.CreateVersion7();
        var source = $"preview:{draft.Id:D}:{draft.Version}";
        var fact = new AccountingFactEnvelope<IAccountingFactPayload>(Guid.CreateVersion7(), tenant, new(draft.LegalEntityId), factType, 1,
            "invoicing", source, source, draft.DocumentDate, draft.PostingDate, payload, correlation, actor);
        try
        {
            await transactions.ExecuteAsync<InvoiceAccountingPreview>(tenant, async (context, token) =>
            {
                var preview = await ledgerPosting.PreviewInvoiceAsync(context, fact, payload, token);
                throw new AccountingPreviewRollbackException(preview);
            }, ct);
        }
        catch (AccountingPreviewRollbackException preview)
        {
            return calculation.Preview with { AccountingPreview = preview.Result };
        }
        throw new InvoiceException("invoicing.accounting_preview_unavailable", "Ledger did not return an accounting preview.");
    }

    private static InvoiceLedgerPostingContract BuildLedgerPayload(Calculation calculation)
    {
        var draft = calculation.Draft;
        var categoryLines = calculation.Lines.Select(line => new InvoiceLedgerLineContract(
            draft.Lines.Single(input => input.Id == line.Id).BusinessCategoryCode ?? "GENERAL", ParseAmount(line.NetAmount, "invoicing.amount_invalid"))).ToArray();
        var taxComponents = calculation.Taxes.OrderBy(pair => pair.Key).SelectMany(pair => pair.Value.TaxLines.Select(line =>
            new InvoiceTaxComponent(line.TaxCode, ParseOptionalAmount(line.Rate), ParseAmount(line.TaxAmount, "invoicing.tax_not_ready"),
                draft.Direction == InvoiceDirection.Receivable ? "OutputTax" : "InputTax"))).ToArray();
        return new(draft.Direction.ToString(), draft.DocumentType.ToString(), ParseAmount(calculation.Totals.NetAmount, "invoicing.amount_invalid"),
            ParseAmount(calculation.Totals.TaxAmount, "invoicing.amount_invalid"), ParseAmount(calculation.Totals.GrossAmount, "invoicing.amount_invalid"), draft.CurrencyCode,
            draft.Direction == InvoiceDirection.Receivable ? "CUSTOMER" : "SUPPLIER", categoryLines, taxComponents,
            calculation.Financial?.ExchangeRate is { } rate ? ParseAmount(rate.Rate, "invoicing.exchange_rate_invalid") : 1m,
            calculation.Financial?.ExchangeRate is { } exchange ? JsonSerializer.Serialize(exchange) : null);
    }

    private static TaxResultContract ToPositiveCorrectionTax(TaxResultContract result)
    {
        static string Positive(string value) => D(Math.Abs(ParseAmount(value, "invoicing.tax_not_ready")));
        return result with
        {
            NetAmount = Positive(result.NetAmount),
            TaxTotal = Positive(result.TaxTotal),
            GrossAmount = Positive(result.GrossAmount),
            TaxLines = result.TaxLines.Select(line => line with
            {
                TaxableBase = Positive(line.TaxableBase),
                TaxAmount = Positive(line.TaxAmount),
                Recoverability = line.Recoverability is { } recoverability ? recoverability with
                {
                    RecoverableAmount = Positive(recoverability.RecoverableAmount),
                    NonRecoverableAmount = Positive(recoverability.NonRecoverableAmount)
                } : null
            }).ToArray()
        };
    }

    private async Task<Calculation> CalculateAsync(TenantId tenant, InvoiceAggregate draft, CancellationToken ct)
    {
        var blockers = new List<InvoiceIssue>();
        var warnings = new List<InvoiceIssue>();
        var party = await RequirePartyAsync(tenant, draft.PartyId, draft.LegalEntityId, draft.Direction, ct);
        var selectedAddress = draft.AddressId is { } addressId ? party.Addresses.SingleOrDefault(address => address.AddressId == addressId) : party.Addresses.FirstOrDefault(address => address.IsDefault);
        if (draft.AddressId is not null && selectedAddress is null) blockers.Add(new("invoicing.party_not_ready", "The selected invoice address is unavailable for this legal entity.", "Blocker"));
        var financePurpose = draft.Direction == InvoiceDirection.Receivable ? FinancialCalculationPurpose.Sales : FinancialCalculationPurpose.Purchase;
        var financial = await financeBase.ResolveCalculationContextAsync(tenant, new(draft.LegalEntityId), draft.CurrencyCode, financePurpose, draft.DocumentDate, ct);
        if (financial is null) blockers.Add(new("invoicing.financial_context_unavailable", "Currency and financial policy are not configured for this invoice.", "Blocker"));
        else if (!string.Equals(financial.BaseCurrencyCode, draft.CurrencyCode, StringComparison.OrdinalIgnoreCase) && financial.ExchangeRate is null)
            blockers.Add(new("invoicing.exchange_rate_missing", "A foreign-currency invoice needs an approved exchange rate for its document date.", "Blocker"));

        var variantIds = draft.Lines.Where(line => line.ProductVariantId.HasValue).Select(line => line.ProductVariantId!.Value).Distinct().ToArray();
        var variantContexts = variantIds.Length == 0
            ? new Dictionary<Guid, CatalogVariantPricingContext>()
            : await catalog.GetPricingContextsBatchAsync(tenant, variantIds, draft.LegalEntityId, ct);
        var variants = variantIds.Length == 0
            ? new Dictionary<Guid, CatalogVariantIdentity>()
            : await catalog.FindVariantsBatchAsync(tenant, variantIds, draft.LegalEntityId, ct);
        var uoms = variantIds.Length == 0
            ? new Dictionary<Guid, IReadOnlyList<CatalogAllowedUom>>()
            : await catalog.GetAllowedUomsBatchAsync(tenant, variantIds, clock.GetUtcNow(), draft.LegalEntityId, ct);
        var priceContexts = new PricingContext?[draft.Lines.Count];
        var resolvedUnitIds = new Guid?[draft.Lines.Count];
        var resolvedUnitCodes = new string?[draft.Lines.Count];
        var productEvidenceByLine = new Dictionary<Guid, string>();
        var unitEvidenceByLine = new Dictionary<Guid, string>();
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            if (line.ProductVariantId is not { } variantId)
            {
                if (string.IsNullOrWhiteSpace(line.ExplicitUnitPrice) || string.IsNullOrWhiteSpace(line.PriceOverrideReason))
                    blockers.Add(new("invoicing.price_missing", "A service or expense line needs an approved price and a reason.", "Blocker", line.Id.ToString("D")));
                continue;
            }
            if (!variants.TryGetValue(variantId, out var variant) || variant.Status != "Active" || !variantContexts.TryGetValue(variantId, out var context))
            {
                blockers.Add(new("invoicing.product_not_ready", "A selected product is unavailable or inactive.", "Blocker", line.Id.ToString("D")));
                continue;
            }
            var unitId = line.UnitOfMeasureId ?? (uoms.TryGetValue(variantId, out var allowed) ? allowed.FirstOrDefault(unit => unit.IsBase)?.UnitId : null);
            if (unitId is null || !uoms.TryGetValue(variantId, out var allowedUoms) || allowedUoms.All(unit => unit.UnitId != unitId.Value))
            {
                blockers.Add(new("invoicing.product_not_ready", "The selected unit of measure is not valid for this product.", "Blocker", line.Id.ToString("D")));
                continue;
            }
            if (line.UnitCode is not null && !allowedUoms.Any(unit => unit.UnitId == unitId.Value && unit.UnitCode.Equals(line.UnitCode, StringComparison.OrdinalIgnoreCase)))
            {
                blockers.Add(new("invoicing.product_not_ready", "The unit of measure code does not match the selected unit.", "Blocker", line.Id.ToString("D")));
                continue;
            }
            resolvedUnitIds[index] = unitId.Value;
            var selectedUom = allowedUoms.Single(unit => unit.UnitId == unitId.Value);
            resolvedUnitCodes[index] = selectedUom.UnitCode;
            productEvidenceByLine[line.Id] = JsonSerializer.Serialize(new { variant.ProductTemplateId, variant.DisplayName, variant.Sku, variant.Nature });
            unitEvidenceByLine[line.Id] = JsonSerializer.Serialize(new
            {
                selectedUom.UnitId,
                selectedUom.UnitCode,
                selectedUom.FactorToBase,
                selectedUom.AllowFraction,
                selectedUom.Precision,
                selectedUom.RoundingPrecision,
                selectedUom.EffectiveFrom,
                selectedUom.EffectiveTo,
                selectedUom.UsageFlags
            });
            priceContexts[index] = new(tenant, new(draft.LegalEntityId), draft.Direction == InvoiceDirection.Receivable ? PricingDirection.Selling : PricingDirection.Buying,
                draft.DocumentDate, draft.CurrencyCode, null, draft.PartyId, null, variantId, variant.ProductTemplateId, context.CategoryPath, context.BrandId, unitId.Value, line.Quantity);
        }
        var pricingInputs = priceContexts.Where(context => context is not null).Select(context => context!).ToArray();
        var priceResults = pricingInputs.Length == 0
            ? Array.Empty<PriceResolution>()
            : await pricing.ResolveBatchAsync(pricingInputs, ct);
        var priceByLine = new Dictionary<Guid, PriceResolution>();
        if (priceResults.Count != pricingInputs.Length)
            blockers.Add(new("invoicing.pricing_response_invalid", "Pricing returned an incomplete set of results for the invoice lines.", "Blocker"));
        else
        {
            var priceIndex = 0;
            for (var index = 0; index < draft.Lines.Count; index++)
            {
                if (priceContexts[index] is null) continue;
                var resolution = priceResults[priceIndex++];
                priceByLine[draft.Lines[index].Id] = resolution;
                if (resolution.Snapshot is { } priceSnapshot && !string.Equals(priceSnapshot.TransactionCurrencyCode, draft.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                    blockers.Add(new("invoicing.price_currency_mismatch", "Pricing returned a price in a different invoice currency.", "Blocker", draft.Lines[index].Id.ToString("D")));
                if ((resolution.Status != "Resolved" || resolution.Snapshot is null) && (draft.Lines[index].ExplicitUnitPrice is null || string.IsNullOrWhiteSpace(draft.Lines[index].PriceOverrideReason)))
                    blockers.Add(new("invoicing.price_missing", resolution.Explanation, "Blocker", draft.Lines[index].Id.ToString("D")));
            }
        }

        var unitPrices = new decimal[draft.Lines.Count];
        var resolvedPrices = new decimal?[draft.Lines.Count];
        var commercialBases = new decimal[draft.Lines.Count];
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            decimal? resolved = null;
            if (priceByLine.TryGetValue(line.Id, out var resolution) && resolution.Snapshot is { } snapshot)
                resolved = ParseAmount(snapshot.FinalUnitPrice, "invoicing.price_missing");
            resolvedPrices[index] = resolved;
            var final = line.ExplicitUnitPrice is { } explicitPrice ? ParseAmount(explicitPrice, "invoicing.price_invalid") : resolved;
            if (final is null) continue;
            if (line.ExplicitUnitPrice is not null && (line.PriceOverrideReason is null || line.PriceOverrideReason.Trim().Length < 3))
                blockers.Add(new("invoicing.price_override_not_allowed", "A price override needs a clear reason.", "Blocker", line.Id.ToString("D")));
            if (line.ExplicitUnitPrice is not null && resolved is not null && final.Value != resolved.Value)
                warnings.Add(new("invoicing.price_override_review", "The entered price differs from the resolved commercial price.", "Warning", line.Id.ToString("D")));
            unitPrices[index] = final.Value;
            var commercial = final.Value * line.Quantity;
            commercialBases[index] = Math.Max(0m, commercial - line.DiscountAmount);
        }
        var roundingScale = financial?.RoundingPolicy.MoneyScale ?? 2;
        var roundingMode = financial?.RoundingPolicy.ContextRules.FirstOrDefault(rule => rule.Context == FinancialRoundingContext.LineNetAmount)?.Mode
            ?? FinancialRoundingMode.HalfEven;
        var documentDiscountAllocations = new decimal[draft.Lines.Count];
        var chargeAllocations = new decimal[draft.Charges.Count][];
        try
        {
            if (draft.DocumentDiscountAmount > commercialBases.Sum())
                blockers.Add(new("invoicing.document_discount_exceeds_basis", "The document discount cannot exceed the remaining line amount.", "Blocker"));
            else
                documentDiscountAllocations = InvoiceCommercialAllocation.Allocate(draft.DocumentDiscountAmount, commercialBases, roundingScale, roundingMode);
            var chargeBasis = commercialBases.Select((amount, index) => Math.Max(0m, amount - documentDiscountAllocations[index])).ToArray();
            for (var chargeIndex = 0; chargeIndex < draft.Charges.Count; chargeIndex++)
            {
                var charge = draft.Charges[chargeIndex];
                var eligibleBasis = string.IsNullOrWhiteSpace(charge.BusinessCategoryCode) ? chargeBasis : chargeBasis.Select((amount, index) =>
                    string.Equals(draft.Lines[index].BusinessCategoryCode, charge.BusinessCategoryCode, StringComparison.OrdinalIgnoreCase) ? amount : 0m).ToArray();
                chargeAllocations[chargeIndex] = InvoiceCommercialAllocation.Allocate(charge.Amount, eligibleBasis, roundingScale, roundingMode);
            }
        }
        catch (InvoiceException exception)
        {
            blockers.Add(new(exception.Code, exception.Message, "Blocker"));
        }
        var taxableChargeByLine = new decimal[draft.Lines.Count];
        var nonTaxableChargeByLine = new decimal[draft.Lines.Count];
        for (var chargeIndex = 0; chargeIndex < draft.Charges.Count; chargeIndex++)
        {
            var allocations = chargeAllocations[chargeIndex] ?? new decimal[draft.Lines.Count];
            for (var lineIndex = 0; lineIndex < allocations.Length; lineIndex++)
            {
                if (draft.Charges[chargeIndex].TaxTreatment == "Taxable") taxableChargeByLine[lineIndex] += allocations[lineIndex];
                else nonTaxableChargeByLine[lineIndex] += allocations[lineIndex];
            }
        }
        var taxInputs = new List<TaxContextContract>();
        var taxLineIndexes = new List<int>();
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            var final = unitPrices[index];
            if (resolvedPrices[index] is null && line.ExplicitUnitPrice is null) continue;
            var commercial = final * line.Quantity;
            var variantContext = line.ProductVariantId is { } v && variantContexts.TryGetValue(v, out var vc) ? vc : null;
            taxInputs.Add(new(tenant, new(draft.LegalEntityId), draft.Direction == InvoiceDirection.Receivable ? "Sales" : "Purchase", "Invoice", draft.Direction == InvoiceDirection.Receivable ? "Sale" : "Purchase",
                draft.DocumentDate, draft.TaxDate, draft.SupplyDate, draft.PostingDate, draft.PartyId, party.CountryCode, selectedAddress?.CountryCode,
                null, variantContext?.ProductTemplateId, line.ProductVariantId, line.Quantity, resolvedUnitIds[index] ?? line.UnitOfMeasureId,
                draft.PriceMode == "Inclusive" ? "TaxInclusive" : "TaxExclusive", D(commercial), D(line.DiscountAmount + documentDiscountAllocations[index]), D(taxableChargeByLine[index]), draft.CurrencyCode, financial?.BaseCurrencyCode ?? draft.CurrencyCode, null,
                DocumentSemantics: draft.DocumentType.ToString(), Dimensions: line.BusinessCategoryCode is null ? null : new Dictionary<string, string> { ["business_category"] = line.BusinessCategoryCode }));
            taxLineIndexes.Add(index);
        }
        IReadOnlyList<TaxResultContract> taxResults = [];
        if (taxInputs.Count > 0)
        {
            try { taxResults = await tax.CalculateBatchAsync(taxInputs, ct); }
            catch (Exception exception) when (exception is not OperationCanceledException) { blockers.Add(new("invoicing.tax_not_ready", "Tax cannot be resolved for one or more invoice lines.", "Blocker")); }
        }
        if (draft.DocumentType == InvoiceDocumentType.CreditNote) taxResults = taxResults.Select(ToPositiveCorrectionTax).ToArray();
        if (taxResults.Count != taxInputs.Count) blockers.Add(new("invoicing.tax_response_invalid", "Tax returned an incomplete set of results for the invoice lines.", "Blocker"));
        InvoiceDocumentValidationResult? countryDocumentValidation = null;
        var taxByIndex = new Dictionary<int, TaxResultContract>();
        for (var i = 0; i < taxResults.Count; i++)
        {
            var result = taxResults[i]; var index = taxLineIndexes[i]; taxByIndex[index] = result;
            if (result.EvaluationState is not ("Evaluated" or "NotEvaluatedBecauseCapabilityDisabled")) blockers.Add(new("invoicing.tax_not_ready", result.Explanation.Summary, "Blocker", draft.Lines[index].Id.ToString("D")));
        }
        if (!string.IsNullOrWhiteSpace(party.CountryCode))
        {
            try
            {
                var productSnapshots = draft.Lines.Where(line => line.ProductVariantId is { } productId && variants.ContainsKey(productId))
                    .ToDictionary(line => line.Id, line => variants[line.ProductVariantId!.Value]);
                var documentFields = BuildCountryDocumentFields(draft, party, selectedAddress, productSnapshots);
                countryDocumentValidation = await tax.ValidateInvoiceDocumentAsync(new(party.CountryCode, draft.TaxDate, draft.DocumentType.ToString(), draft.CurrencyCode,
                    draft.SupplyDate, draft.Lines.Select(line => line.Id.ToString("D")).ToArray(), documentFields), ct);
                foreach (var issue in countryDocumentValidation.Issues)
                    blockers.Add(new(issue.Code, $"Country Pack requires valid {issue.FieldCode} document evidence.", "Blocker", issue.Reference));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                blockers.Add(new("invoicing.country_pack_validation_unavailable", "Country Pack document requirements could not be verified.", "Blocker"));
            }
        }
        var lineResults = new List<InvoiceLineResult>();
        decimal net = 0, discount = draft.DocumentDiscountAmount, taxTotal = 0, gross = 0, taxable = 0;
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            if (!taxByIndex.TryGetValue(index, out var taxResult)) continue;
            var lineNet = ParseAmount(taxResult.NetAmount, "invoicing.tax_not_ready") + nonTaxableChargeByLine[index];
            var lineTax = ParseAmount(taxResult.TaxTotal, "invoicing.tax_not_ready");
            var lineGross = ParseAmount(taxResult.GrossAmount, "invoicing.tax_not_ready") + nonTaxableChargeByLine[index];
            var lineTaxable = taxResult.TaxLines.Sum(component => ParseAmount(component.TaxableBase, "invoicing.tax_not_ready"));
            net += lineNet; discount += line.DiscountAmount; taxTotal += lineTax; gross += lineGross; taxable += lineTaxable;
            lineResults.Add(new(line.Id, line.Sequence, line.ProductVariantId, resolvedUnitIds[index] ?? line.UnitOfMeasureId, resolvedUnitCodes[index] ?? line.UnitCode, line.Description,
                D(line.Quantity), resolvedPrices[index] is { } resolved ? D(resolved) : null, D(unitPrices[index]), D(lineNet), D(lineTax), D(lineGross),
                priceByLine.TryGetValue(line.Id, out var p) ? p.Explanation : "Ad-hoc service/expense line", taxResult.TaxLines));
        }
        if (gross <= 0m) blockers.Add(new("invoicing.total_mismatch", "A finalized invoice must have a positive total.", "Blocker"));
        if (financial is null) blockers.Add(new("invoicing.financial_context_unavailable", "Currency and financial policy are not configured for this invoice.", "Blocker"));
        var scheduleResolution = await ResolveScheduleAsync(tenant, draft, party, gross, blockers, ct);
        var schedule = scheduleResolution.Lines;
        var totals = new InvoiceTotals(D(net), D(discount), D(draft.Charges.Sum(charge => charge.Amount)), D(taxable), D(taxTotal), D(gross), "0", draft.CurrencyCode);
        var preview = new InvoicePreview(draft.Id, draft.Version, lineResults, totals, schedule, warnings, blockers, blockers.Count == 0);
        return new(draft, party, selectedAddress, financial, priceByLine, taxByIndex, lineResults, schedule, scheduleResolution.Term, totals, preview,
            productEvidenceByLine, unitEvidenceByLine, countryDocumentValidation);
    }

    private async Task<ScheduleResolution> ResolveScheduleAsync(TenantId tenant, InvoiceAggregate draft, InvoicePartySnapshotContract party,
        decimal total, List<InvoiceIssue> blockers, CancellationToken ct)
    {
        if (total <= 0m) return new([], null);
        var paymentTermId = draft.PaymentTermId;
        if (paymentTermId is null)
        {
            var profile = await subledger.GetFinancialProfileAsync(tenant, new(draft.LegalEntityId), draft.PartyId, ct);
            paymentTermId = draft.Direction == InvoiceDirection.Receivable ? profile?.DefaultReceivablePaymentTermId : profile?.DefaultPayablePaymentTermId;
        }
        if (paymentTermId is null) return new([new(1, draft.DocumentDate, D(total), draft.CurrencyCode)], null);
        var term = await financeBase.ResolvePaymentTermAsync(tenant, paymentTermId.Value, draft.DocumentDate, ct);
        if (term is null)
        {
            blockers.Add(new("invoicing.payment_term_unavailable", "The selected payment term is not active for this invoice date.", "Blocker"));
            return new([], null);
        }
        var schedule = await financeBase.CalculatePaymentScheduleAsync(tenant, new(draft.LegalEntityId), paymentTermId.Value, draft.CurrencyCode, D(total), draft.DocumentDate, ct);
        if (schedule is null || schedule.Lines.Count is < 1 or > 200 || schedule.Lines.Sum(line => ParseAmount(line.Amount.Amount, "invoicing.payment_schedule_invalid")) != total)
        {
            blockers.Add(new("invoicing.payment_schedule_invalid", "The payment schedule does not equal the invoice total.", "Blocker"));
            return new([], term);
        }
        return new(schedule.Lines.Select(line => new InvoiceScheduleLine(line.Sequence, line.DueDate, line.Amount.Amount, line.Amount.CurrencyCode)).ToArray(), term);
    }

    private async Task<InvoicePartySnapshotContract> RequirePartyAsync(TenantId tenant, Guid partyId, Guid entityId, InvoiceDirection direction, CancellationToken ct)
    {
        var party = await directory.ReadInvoiceSnapshotAsync(tenant, partyId, entityId, ct);
        if (party is null) throw new InvoiceException("invoicing.party_not_ready", "The selected customer or supplier is unavailable for this legal entity.");
        var requiredRole = direction == InvoiceDirection.Receivable ? "Customer" : "Supplier";
        if (!party.ActiveRoles.Contains(requiredRole, StringComparer.OrdinalIgnoreCase)) throw new InvoiceException("invoicing.party_not_ready", "The selected party does not have the required customer or supplier role.");
        return party;
    }

    private async Task ValidateBranchAsync(TenantId tenant, Guid legalEntityId, Guid? branchId, CancellationToken ct)
    {
        if (branchId is { } id && !await organization.BranchBelongsToLegalEntityAsync(tenant, new(legalEntityId), id, ct))
            throw new InvoiceException("invoicing.branch_not_available", "The selected branch is not active for this legal entity.");
    }

    private static void ValidateHeader(Guid legalEntityId, DateOnly documentDate, DateOnly postingDate, DateOnly taxDate, string currencyCode, string priceMode)
    {
        if (legalEntityId == Guid.Empty || documentDate == DateOnly.MinValue || postingDate == DateOnly.MinValue || taxDate == DateOnly.MinValue || string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3 || priceMode is not ("Exclusive" or "Inclusive"))
            throw new InvoiceException("invoicing.input_invalid", "Invoice dates, legal entity, and currency are required.");
    }
    private static InvoiceLineDraft[] ParseLines(IReadOnlyList<InvoiceLineInput> input)
    {
        if (input is null) throw new InvoiceException("invoicing.lines_invalid", "Invoice lines are required.");
        return input.Select((line, index) => new InvoiceLineDraft(Guid.CreateVersion7(), index + 1, line.ProductVariantId, line.UnitOfMeasureId, line.UnitCode,
            line.Description, ParseAmount(line.Quantity, "invoicing.quantity_invalid"), line.UnitPrice, ParseOptionalAmount(line.DiscountAmount), line.SourceLineReference,
            line.PriceOverrideReason, line.BusinessCategoryCode)).ToArray();
    }
    private void ValidateLines(IReadOnlyList<InvoiceLineDraft> lines)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        _ = new InvoiceAggregate(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), null, InvoiceDirection.Receivable, InvoiceDocumentType.Invoice,
            Guid.CreateVersion7(), null, today, today, today, null,
            "USD", null, null, 1, InvoiceLifecycle.Draft, lines);
        if (lines.Any(line => line.ProductVariantId is null && (line.UnitOfMeasureId is not null || string.IsNullOrWhiteSpace(line.UnitCode))))
            throw new InvoiceException("invoicing.product_not_ready", "A service or expense line requires a unit code and cannot reference a stock unit.");
    }
    private static void ValidateAddress(InvoicePartySnapshotContract party, Guid? addressId)
    {
        if (addressId.HasValue && party.Addresses.All(address => address.AddressId != addressId.Value)) throw new InvoiceException("invoicing.party_not_ready", "The selected invoice address is not available for this party.");
    }
    private static void ValidateSupplierNumber(InvoiceDirection direction, string? supplierNumber)
    {
        if (direction == InvoiceDirection.Payable && string.IsNullOrWhiteSpace(supplierNumber)) throw new InvoiceException("invoicing.supplier_number_required", "Supplier invoices require the supplier's document number.");
        if (direction == InvoiceDirection.Receivable && !string.IsNullOrWhiteSpace(supplierNumber)) throw new InvoiceException("invoicing.supplier_number_invalid", "A customer invoice cannot contain a supplier document number.");
    }
    private static InvoiceException NotFound() => new("invoicing.invoice_not_found", "Invoice was not found.");

    private static List<InvoiceDocumentField> BuildCountryDocumentFields(InvoiceAggregate draft, InvoicePartySnapshotContract party,
        InvoicePartyAddressContract? address, Dictionary<Guid, CatalogVariantIdentity> products)
    {
        var fields = new List<InvoiceDocumentField>
        {
            new("Header", "header", "document_type", draft.DocumentType.ToString()),
            new("Header", "header", "currency_code", draft.CurrencyCode),
            new("Header", "header", "supply_date", draft.SupplyDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new("Party", "party", "display_name", party.DisplayName),
            new("Party", "party", "legal_name", party.LegalName),
            new("Party", "party", "trade_name", party.TradeName),
            new("Party", "party", "country_code", party.CountryCode)
        };
        foreach (var identifier in party.TaxIdentifiers)
            fields.Add(new("Party", "party", $"tax_id:{identifier.Type}", identifier.Value));
        if (address is not null)
        {
            fields.Add(new("Address", address.AddressId.ToString("D"), "country_code", address.CountryCode));
            fields.Add(new("Address", address.AddressId.ToString("D"), "city", address.City));
            fields.Add(new("Address", address.AddressId.ToString("D"), "region", address.Region));
            fields.Add(new("Address", address.AddressId.ToString("D"), "postal_code", address.PostalCode));
            fields.Add(new("Address", address.AddressId.ToString("D"), "line1", address.Line1));
        }
        foreach (var line in draft.Lines)
        {
            var reference = line.Id.ToString("D");
            fields.Add(new("Product", reference, "business_category_code", line.BusinessCategoryCode));
            if (!products.TryGetValue(line.Id, out var product)) continue;
            fields.Add(new("Product", reference, "product_name", product.DisplayName));
            fields.Add(new("Product", reference, "sku", product.Sku));
            fields.Add(new("Product", reference, "product_nature", product.Nature));
        }
        return fields;
    }

    private static decimal ParseAmount(string amount, string code) => decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : throw new InvoiceException(code, "A financial amount returned by a connected capability was invalid.");
    private static decimal ParseOptionalAmount(string? amount) => string.IsNullOrWhiteSpace(amount) ? 0m : ParseAmount(amount, "invoicing.amount_invalid");
    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static decimal RoundAmount(decimal amount, int scale, FinancialRoundingMode mode)
    {
        var factor = (decimal)Math.Pow(10, scale);
        var scaled = amount * factor;
        var rounded = mode switch
        {
            FinancialRoundingMode.HalfUp => decimal.Round(scaled, 0, MidpointRounding.AwayFromZero),
            FinancialRoundingMode.HalfEven => decimal.Round(scaled, 0, MidpointRounding.ToEven),
            FinancialRoundingMode.Down => decimal.Truncate(scaled),
            FinancialRoundingMode.Up => decimal.Ceiling(scaled),
            _ => throw new InvoiceException("invoicing.rounding_policy_invalid", "The configured financial rounding mode is not supported.")
        };
        return rounded / factor;
    }

    private static InvoiceChargeDraft[] ParseCharges(IReadOnlyList<InvoiceChargeInput>? charges)
    {
        if (charges is null) return [];
        if (charges.Count > 100) throw new InvoiceException("invoicing.charges_invalid", "An invoice may contain at most 100 typed charges.");
        var allowedCodes = new HashSet<string>(["SHIPPING", "HANDLING", "INSURANCE", "SERVICE_FEE"], StringComparer.Ordinal);
        return charges.Select(charge =>
        {
            var code = charge.Code?.Trim().ToUpperInvariant();
            var amount = ParseOptionalAmount(charge.Amount);
            var treatment = charge.TaxTreatment?.Trim();
            if (code is null || !allowedCodes.Contains(code) || string.IsNullOrWhiteSpace(charge.Description) || charge.Description.Trim().Length > 500
                || amount <= 0m || treatment is not ("Taxable" or "NonTaxable"))
                throw new InvoiceException("invoicing.charge_invalid", "Each charge needs an approved type, positive amount, description, and tax treatment.");
            return new InvoiceChargeDraft(Guid.CreateVersion7(), code, charge.Description.Trim(), amount, treatment,
                string.IsNullOrWhiteSpace(charge.BusinessCategoryCode) ? null : charge.BusinessCategoryCode.Trim());
        }).ToArray();
    }

    private static InvoiceFinalizationData ToFinalizationData(Calculation calculation)
    {
        var prices = calculation.Lines.Select(line =>
        {
            calculation.Prices.TryGetValue(line.Id, out var resolution);
            return new InvoicePriceSnapshotData(line.Id, line.ResolvedUnitPrice, line.FinalUnitPrice,
                JsonSerializer.Serialize(new
                {
                    status = resolution?.Status ?? "Manual",
                    explanation = line.PriceExplanation,
                    snapshot = resolution?.Snapshot,
                    manualOverride = calculation.Draft.Lines.Single(input => input.Id == line.Id).ExplicitUnitPrice is not null,
                    overrideReason = calculation.Draft.Lines.Single(input => input.Id == line.Id).PriceOverrideReason
                }));
        }).ToArray();
        var units = calculation.Lines.Select(line => new InvoiceUnitSnapshotData(line.Id, line.UnitOfMeasureId, line.UnitCode,
            calculation.ProductEvidenceByLine.GetValueOrDefault(line.Id), calculation.UnitEvidenceByLine.GetValueOrDefault(line.Id))).ToArray();
        var lineAmounts = calculation.Lines.Select(line => new InvoiceLineAmountSnapshotData(line.Id, line.NetAmount, line.TaxAmount, line.GrossAmount)).ToArray();
        var charges = calculation.Draft.Charges.Select(charge => new InvoiceChargeSnapshotData(charge.Id, charge.Code, charge.Description,
            D(charge.Amount), charge.TaxTreatment, charge.BusinessCategoryCode)).ToArray();
        var discountAllocations = new List<InvoiceDiscountAllocationSnapshotData>();
        var chargeAllocations = new List<InvoiceChargeAllocationSnapshotData>();
        var scale = calculation.Financial?.RoundingPolicy.MoneyScale ?? 2;
        var mode = calculation.Financial?.RoundingPolicy.ContextRules.FirstOrDefault(rule => rule.Context == FinancialRoundingContext.LineNetAmount)?.Mode ?? FinancialRoundingMode.HalfEven;
        var basis = calculation.Lines.Select(line => Math.Max(0m,
            ParseAmount(line.FinalUnitPrice, "invoicing.price_invalid") * ParseAmount(line.Quantity, "invoicing.quantity_invalid")
            - calculation.Draft.Lines.Single(input => input.Id == line.Id).DiscountAmount)).ToArray();
        var documentAllocations = InvoiceCommercialAllocation.Allocate(calculation.Draft.DocumentDiscountAmount, basis, scale, mode);
        var chargeBasis = basis.Select((amount, index) => Math.Max(0m, amount - documentAllocations[index])).ToArray();
        for (var index = 0; index < calculation.Draft.Lines.Count; index++)
        {
            var line = calculation.Draft.Lines[index];
            if (line.DiscountAmount > 0m) discountAllocations.Add(new(line.Id, "Line", D(line.DiscountAmount), D(line.DiscountAmount)));
            if (documentAllocations[index] > 0m) discountAllocations.Add(new(line.Id, "Document", D(calculation.Draft.DocumentDiscountAmount), D(documentAllocations[index])));
        }
        for (var chargeIndex = 0; chargeIndex < calculation.Draft.Charges.Count; chargeIndex++)
        {
            var charge = calculation.Draft.Charges[chargeIndex];
            var eligibleBasis = string.IsNullOrWhiteSpace(charge.BusinessCategoryCode) ? chargeBasis : chargeBasis.Select((amount, index) =>
                string.Equals(calculation.Draft.Lines[index].BusinessCategoryCode, charge.BusinessCategoryCode, StringComparison.OrdinalIgnoreCase) ? amount : 0m).ToArray();
            var allocations = InvoiceCommercialAllocation.Allocate(charge.Amount, eligibleBasis, scale, mode);
            for (var index = 0; index < allocations.Length; index++)
                if (allocations[index] > 0m) chargeAllocations.Add(new(charge.Id, calculation.Draft.Lines[index].Id, D(allocations[index])));
        }
        var taxes = calculation.Taxes.OrderBy(pair => pair.Key).SelectMany(pair => pair.Value.TaxLines.Select((line, index) =>
            new InvoiceTaxSnapshotData(calculation.Draft.Lines[pair.Key].Id, index + 1, line.TaxCode, line.TaxTypeCode, line.Treatment, line.TaxableBase, line.TaxAmount,
                JsonSerializer.Serialize(new
                {
                    line.RuleId,
                    line.RuleVersion,
                    line.Rate,
                    line.JurisdictionCode,
                    line.LegalReasonCode,
                    line.LegalReasonText,
                    line.ReportingCode,
                    line.Recoverability,
                    calculation = pair.Value.CalculationVersion,
                    pair.Value.EvaluationState,
                    countryDocumentValidation = calculation.CountryDocumentValidation,
                    pair.Value.JurisdictionSnapshot,
                    pair.Value.RegistrationSnapshot,
                    result = pair.Value
                })))).ToArray();
        var termEvidence = calculation.PaymentTerm is null
            ? JsonSerializer.Serialize(new { kind = "Immediate", source = "NoPaymentTerm", effectiveDate = calculation.Draft.DocumentDate, lines = calculation.Schedule })
            : JsonSerializer.Serialize(calculation.PaymentTerm);
        return new(calculation.Totals, calculation.Party.DisplayName, JsonSerializer.Serialize(calculation.Party), calculation.Address is null ? null : JsonSerializer.Serialize(calculation.Address),
            units, prices, taxes, lineAmounts, charges, chargeAllocations, discountAllocations, calculation.Schedule, calculation.PaymentTerm?.PaymentTermId, calculation.PaymentTerm?.VersionId,
            termEvidence, "Open", "Pending", calculation.Financial);
    }

    private sealed record ScheduleResolution(IReadOnlyList<InvoiceScheduleLine> Lines, PaymentTermSnapshotContract? Term);
    private sealed class AccountingPreviewRollbackException(InvoiceAccountingPreview result) : Exception
    {
        public InvoiceAccountingPreview Result { get; } = result;
    }
    private sealed record Calculation(InvoiceAggregate Draft, InvoicePartySnapshotContract Party, InvoicePartyAddressContract? Address,
        FinancialCalculationContextSnapshotContract? Financial, IReadOnlyDictionary<Guid, PriceResolution> Prices, IReadOnlyDictionary<int, TaxResultContract> Taxes,
        IReadOnlyList<InvoiceLineResult> Lines, IReadOnlyList<InvoiceScheduleLine> Schedule, PaymentTermSnapshotContract? PaymentTerm, InvoiceTotals Totals,
        InvoicePreview Preview, IReadOnlyDictionary<Guid, string> ProductEvidenceByLine, IReadOnlyDictionary<Guid, string> UnitEvidenceByLine,
        InvoiceDocumentValidationResult? CountryDocumentValidation);
}
