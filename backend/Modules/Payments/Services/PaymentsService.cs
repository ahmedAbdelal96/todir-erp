using Core.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Modules.Payments.DTOs;
using Modules.Payments.Entities;
using Modules.Payments.Repositories;
using Shared.Contracts.Directory;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Platform;
using Shared.Contracts.Subledger;
using Shared.Primitives.Identifiers;

namespace Modules.Payments.Services;

public sealed class PaymentsService(PaymentsRepository repository, IDirectoryCapabilities directory,
    IFinanceBaseCapabilities financeBase, IPlatformOrganizationCapabilities organization, ISubledgerCapabilities subledger,
    IDirectoryPartyRoleCapabilities partyRoles, IReferencePaymentProvider referenceProvider, IProviderSecretResolver providerSecrets)
{
    public async Task<PaymentAccount> CreateAccountAsync(TenantId tenant, Guid actor, Guid membership, CreatePaymentAccountRequest request, Guid correlationId, CancellationToken ct)
    {
        if (request.BranchId is { } branch && !await organization.BranchBelongsToLegalEntityAsync(tenant, new(request.LegalEntityId), branch, ct))
            throw new PaymentException("payments.branch_scope_invalid", "The selected branch does not belong to this legal entity.");
        var account = await repository.CreateAccountAsync(tenant, actor, membership, correlationId, new(request.LegalEntityId, request.BranchId, request.Code, request.Name, request.Type,
            request.CurrencyPolicy, request.CurrencyCode, request.BankName, request.AccountHolder, request.MaskedIdentifier, request.SecretRef), ct);
        return account with { SecretRef = null };
    }

    public async Task<IReadOnlyList<PaymentAccount>> ListAccountsAsync(TenantId tenant, Guid legalEntityId, Guid? branchId, CancellationToken ct) => (await repository.ListAccountsAsync(tenant, legalEntityId, branchId, ct)).Select(account => account with { SecretRef = null }).ToArray();

    public Task UpdateAccountStatusAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId, Guid legalEntityId, Guid accountId,
        Guid? branchId, PaymentAccountStatus status, CancellationToken ct) => repository.UpdateAccountStatusAsync(tenant, actor, membership, correlationId, legalEntityId, accountId, branchId, status, ct);

    public Task<PaymentRecord> CreateDraftAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId, CreatePaymentRequest request,
        string idempotencyKey, CancellationToken ct) => CreateDraftCoreAsync(tenant, actor, membership, correlationId, request, idempotencyKey, null, null, ct);

    private async Task<PaymentRecord> CreateDraftCoreAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        CreatePaymentRequest request, string idempotencyKey, string? providerCode, string? providerTransactionId, CancellationToken ct)
    {
        var direction = request.Type is PaymentType.CustomerReceipt or PaymentType.CustomerAdvance or PaymentType.SupplierRefund
            ? PaymentDirection.Incoming : PaymentDirection.Outgoing;
        var command = new CreatePaymentCommand(request.LegalEntityId, request.BranchId, direction, request.Type, request.PartyId,
            request.Amount, request.CurrencyCode, request.Method, request.PaymentAccountId, request.PaymentDate, request.PostingDate,
            request.ValueDate, request.BankReference, request.CustomerReference, request.SupplierReference, request.SourceType,
            request.SourceReference, idempotencyKey, providerCode, providerTransactionId);
        PaymentRules.Validate(command);
        if (request.BranchId is { } branch && !await organization.BranchBelongsToLegalEntityAsync(tenant, new(request.LegalEntityId), branch, ct))
            throw new PaymentException("payments.branch_scope_invalid", "The selected branch does not belong to this legal entity.");
        if (request.PartyId is { } partyId)
        {
            var requiredRole = request.Type is PaymentType.CustomerReceipt or PaymentType.CustomerAdvance or PaymentType.CustomerRefund ? "Customer" : "Supplier";
            if (!await directory.IsActivePartyAsync(tenant, partyId, request.LegalEntityId, ct)
                || !await partyRoles.HasActiveRoleAsync(tenant, partyId, request.LegalEntityId, requiredRole, ct))
                throw new PaymentException("payments.party_scope_invalid", "The selected customer or supplier is unavailable for this legal entity.");
        }
        var context = await financeBase.ResolveCalculationContextAsync(tenant, new(request.LegalEntityId), PaymentRules.NormalizeCurrency(request.CurrencyCode),
            FinancialCalculationPurpose.Accounting, request.PostingDate, ct);
        if (context is null) throw new PaymentException("payments.currency_not_enabled", "The payment currency is not enabled for this legal entity and date.");
        return await repository.CreateDraftAsync(tenant, actor, membership, correlationId, command, ct);
    }

    public async Task<InternalTransferResult> CreateInternalTransferAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        CreateInternalTransferRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (request.SourcePaymentAccountId == request.DestinationPaymentAccountId)
            throw new PaymentException("payments.same_source_destination", "Choose two different payment accounts.");
        var sourceCurrency = PaymentRules.NormalizeCurrency(request.CurrencyCode);
        var destinationCurrency = PaymentRules.NormalizeCurrency(request.DestinationCurrencyCode ?? sourceCurrency);
        var sourceContext = await financeBase.ResolveCalculationContextAsync(tenant, new(request.LegalEntityId), sourceCurrency,
            FinancialCalculationPurpose.Accounting, request.PostingDate, ct)
            ?? throw new PaymentException("payments.currency_not_enabled", "The source currency is not enabled for this legal entity and date.");
        var destinationContext = sourceCurrency == destinationCurrency ? sourceContext
            : await financeBase.ResolveCalculationContextAsync(tenant, new(request.LegalEntityId), destinationCurrency,
                FinancialCalculationPurpose.Accounting, request.PostingDate, ct)
                ?? throw new PaymentException("payments.currency_not_enabled", "The destination currency is not enabled for this legal entity and date.");
        if (sourceContext.BaseCurrencyCode != destinationContext.BaseCurrencyCode)
            throw new PaymentException("payments.fx_context_invalid", "Both transfer currencies must use the same legal-entity base currency.");
        var amount = PaymentRules.ParseAmount(request.Amount);
        var sourceRate = ApprovedBaseRate(sourceContext, sourceCurrency);
        var destinationRate = ApprovedBaseRate(destinationContext, destinationCurrency);
        var sourceBaseAmount = RoundMoney(amount * sourceRate, sourceContext);
        var destinationAmount = sourceCurrency == destinationCurrency ? amount : RoundMoney(sourceBaseAmount / destinationRate, destinationContext);
        var destinationBaseAmount = RoundMoney(destinationAmount * destinationRate, destinationContext);
        if (sourceBaseAmount <= 0m || destinationAmount <= 0m || destinationBaseAmount <= 0m)
            throw new PaymentException("payments.fx_amount_invalid", "The approved exchange rates produced a transfer amount below the supported precision.");
        return await repository.CreateInternalTransferAsync(tenant, actor, membership, correlationId,
            request with { CurrencyCode = sourceCurrency, DestinationCurrencyCode = destinationCurrency }, idempotencyKey,
            sourceContext, destinationContext, amount, destinationAmount, sourceBaseAmount, destinationBaseAmount, ct);
    }

    public async Task<PaymentRecord> CreateRefundDraftAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId, Guid legalEntityId,
        Guid originalPaymentId, CreateRefundRequest request, string idempotencyKey, CancellationToken ct)
    {
        var context = await financeBase.ResolveCalculationContextAsync(tenant, new Shared.Primitives.Identifiers.LegalEntityId(legalEntityId),
            PaymentRules.NormalizeCurrency((await repository.GetAsync(tenant, legalEntityId, null, originalPaymentId, ct)
                ?? throw new PaymentException("payments.refund_original_unavailable", "The original payment was not found.")).CurrencyCode),
            FinancialCalculationPurpose.Accounting, request.PostingDate, ct);
        if (context is null) throw new PaymentException("payments.currency_not_enabled", "The refund currency is not enabled for this legal entity and date.");
        return await repository.CreateRefundDraftAsync(tenant, actor, membership, correlationId,
            new(legalEntityId, originalPaymentId, request.PaymentAccountId, request.Method, request.Amount, request.PaymentDate, request.PostingDate,
                request.ValueDate, request.ReasonCode, request.BankReference, idempotencyKey), ct);
    }

    public async Task<ProviderRefundResult> CreateProviderRefundAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        Guid legalEntityId, Guid originalPaymentId, CreateRefundRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (request.Method != PaymentMethod.PaymentGateway)
            throw new PaymentException("payments.provider_refund_method_invalid", "Choose the original provider payment method for a provider refund.");
        var refund = await CreateRefundDraftAsync(tenant, actor, membership, correlationId, legalEntityId, originalPaymentId, request, idempotencyKey, ct);
        var operation = await repository.CreateProviderRefundOperationAsync(tenant, actor, membership, correlationId,
            legalEntityId, originalPaymentId, refund.Id, idempotencyKey, ct);
        var result = await CallReferenceProviderRefundAsync(operation, ct);
        await repository.RecordProviderRefundResultAsync(tenant, operation, result, ct);
        if (result.Status == "Succeeded")
            refund = await CompleteManualAsync(tenant, actor, membership, legalEntityId, refund.Id,
                $"provider-refund-complete:{operation.OperationId:D}", operation.OperationId, ct);
        return new(refund, result.Status, operation.OperationId);
    }

    public async Task<ProviderRefundResult> SyncProviderRefundStatusAsync(TenantId tenant, Guid actor, Guid membership,
        Guid legalEntityId, Guid refundPaymentId, CancellationToken ct)
    {
        var operationId = await repository.GetProviderRefundOperationIdAsync(tenant, legalEntityId, refundPaymentId, ct)
            ?? throw new PaymentException("payments.provider_refund_not_found", "This refund has no provider operation to reconcile.");
        var operation = await repository.GetProviderRefundOperationAsync(tenant, operationId, ct)
            ?? throw new PaymentException("payments.provider_refund_not_found", "The provider refund is unavailable.");
        var result = await referenceProvider.GetRefundStatusAsync(operation.OperationId, ct);
        await repository.RecordProviderRefundResultAsync(tenant, operation, result, ct);
        var refund = await repository.GetAsync(tenant, legalEntityId, null, operation.RefundPaymentId, ct)
            ?? throw new PaymentException("payments.refund_not_found", "The refund payment was not found.");
        if (result.Status == "Succeeded")
            refund = await CompleteManualAsync(tenant, actor, membership, legalEntityId, refund.Id,
                $"provider-refund-complete:{operation.OperationId:D}", operation.OperationId, ct);
        return new(refund, result.Status, operation.OperationId);
    }

    private async Task<ReferenceProviderRefundResult> CallReferenceProviderRefundAsync(ProviderRefundOperation operation, CancellationToken ct)
    {
        try
        {
            return await referenceProvider.RefundPaymentAsync(operation.OperationId, operation.ProviderTransactionId,
                operation.Amount, operation.CurrencyCode, operation.IdempotencyKey, ct);
        }
        catch (TimeoutException)
        {
            return new("Unknown", string.Empty, DateTimeOffset.UtcNow);
        }
    }

    public async Task<PaymentRecord> ReverseAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId, ReversePaymentRequest request,
        Guid legalEntityId, Guid paymentId, string idempotencyKey, CancellationToken ct)
    {
        var original = await repository.GetAsync(tenant, legalEntityId, null, paymentId, ct)
            ?? throw new PaymentException("payments.payment_not_found", "The payment was not found.");
        var context = await financeBase.ResolveCalculationContextAsync(tenant, new(legalEntityId), original.CurrencyCode,
            FinancialCalculationPurpose.Accounting, request.PostingDate, ct)
            ?? throw new PaymentException("payments.currency_not_enabled", "The payment currency is not enabled for this legal entity and date.");
        return await repository.ReversePaymentAsync(tenant, actor, membership, correlationId,
            new(legalEntityId, paymentId, request.PaymentDate, request.PostingDate, request.ReasonCode, idempotencyKey), context, ct);
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        CreatePaymentIntentRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (request.PartyId is null || request.PartyId == Guid.Empty)
            throw new PaymentException("payments.party_required", "Select the customer whose payment is being requested.");
        if (request.BranchId is { } branch && !await organization.BranchBelongsToLegalEntityAsync(tenant, new(request.LegalEntityId), branch, ct))
            throw new PaymentException("payments.branch_scope_invalid", "The selected branch does not belong to this legal entity.");
        var context = await financeBase.ResolveCalculationContextAsync(tenant, new(request.LegalEntityId), PaymentRules.NormalizeCurrency(request.CurrencyCode),
            FinancialCalculationPurpose.Accounting, DateOnly.FromDateTime(DateTime.UtcNow), ct);
        if (context is null) throw new PaymentException("payments.currency_not_enabled", "The requested currency is not enabled for this legal entity.");
        if (request.PartyId is { } party && (!await directory.IsActivePartyAsync(tenant, party, request.LegalEntityId, ct)
            || !await partyRoles.HasActiveRoleAsync(tenant, party, request.LegalEntityId, "Customer", ct)))
            throw new PaymentException("payments.party_scope_invalid", "The selected customer is unavailable for this legal entity.");
        return await repository.CreatePaymentIntentAsync(tenant, actor, membership, correlationId, request, idempotencyKey, ct);
    }

    public Task<PaymentIntentResult> RetryPaymentIntentAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        Guid legalEntityId, Guid requestId, string idempotencyKey, CancellationToken ct) =>
        repository.RetryPaymentIntentAsync(tenant, actor, membership, correlationId, legalEntityId, requestId, idempotencyKey, ct);

    public Task<PaymentLinkResult> CreatePaymentLinkAsync(TenantId tenant, CreatePaymentLinkRequest request, Guid legalEntityId, CancellationToken ct) =>
        repository.CreatePaymentLinkAsync(tenant, legalEntityId, request, ct);

    public Task<PublicPaymentLinkDetails?> GetPublicPaymentLinkAsync(string token, CancellationToken ct) => repository.GetPublicPaymentLinkAsync(token, ct);

    public Task CancelPaymentLinkAsync(TenantId tenant, Guid legalEntityId, Guid requestId, Guid linkId, CancellationToken ct) =>
        repository.CancelPaymentLinkAsync(tenant, legalEntityId, requestId, linkId, ct);

    public Task<PaymentProviderResult> CreateProviderConfigAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        CreatePaymentProviderRequest request, CancellationToken ct) =>
        repository.CreateProviderConfigAsync(tenant, actor, membership, correlationId, request, ct);

    public Task<PaymentFeeResult> AddFeeAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId, Guid legalEntityId, Guid paymentId,
        CreatePaymentFeeRequest request, string idempotencyKey, CancellationToken ct) =>
        repository.AddFeeAsync(tenant, actor, membership, correlationId, legalEntityId, paymentId, request, idempotencyKey, ct);

    public async Task StartReferenceProviderOperationAsync(TenantId tenant, Guid operationId, CancellationToken ct)
    {
        var operation = await repository.GetReferenceProviderOperationAsync(tenant, operationId, ct)
            ?? throw new PaymentException("payments.provider_operation_not_found", "The reference provider operation is unavailable.");
        if (operation.ProviderTransactionId is not null) return;
        ReferenceProviderResult providerResult;
        try
        {
            providerResult = await referenceProvider.CreatePaymentRequestAsync(operation.RequestId, operation.MerchantReference,
                operation.Amount, operation.CurrencyCode, $"{operation.IdempotencyKey}:{operation.AttemptId:D}", ct);
        }
        catch (TimeoutException)
        {
            providerResult = new("Unknown", string.Empty, DateTimeOffset.UtcNow);
        }
        await repository.RecordReferenceProviderResultAsync(tenant, operation, providerResult, ct);
    }

    public async Task<PaymentIntentResult> SyncReferenceProviderStatusAsync(TenantId tenant, Guid legalEntityId, Guid requestId, CancellationToken ct)
    {
        var current = await repository.GetPaymentIntentAsync(tenant, legalEntityId, requestId, ct)
            ?? throw new PaymentException("payments.request_not_found", "The payment request was not found.");
        if (current.State == "Succeeded") return current;
        var operationId = await repository.GetLatestReferenceProviderOperationIdAsync(tenant, legalEntityId, requestId, ct)
            ?? throw new PaymentException("payments.provider_operation_not_found", "The payment request has no provider attempt to reconcile.");
        var operation = await repository.GetReferenceProviderOperationAsync(tenant, operationId, ct)
            ?? throw new PaymentException("payments.provider_operation_not_found", "The provider attempt is unavailable.");
        if (!operation.Capabilities.GetPaymentStatus)
            throw new PaymentException("payments.provider_operation_unsupported", "This provider does not support payment status checks.");
        var status = await referenceProvider.GetPaymentStatusAsync(operation.RequestId, operation.MerchantReference,
            operation.ProviderTransactionId, ct);
        await repository.RecordReferenceProviderResultAsync(tenant, operation, status, ct);
        if (status.Status == "Succeeded")
        {
            await CompleteReferencePaymentOperationAsync(tenant, operationId, status.ConfirmedAt, ct);
            await repository.MarkReferenceOperationWebhooksProcessedAsync(tenant, operation, ct);
        }
        return await repository.GetPaymentIntentAsync(tenant, legalEntityId, requestId, ct)
            ?? throw new PaymentException("payments.request_not_found", "The payment request was not found.");
    }

    public async Task<PaymentIntentResult> CancelProviderPaymentRequestAsync(TenantId tenant, Guid legalEntityId, Guid requestId, CancellationToken ct)
    {
        var current = await repository.GetPaymentIntentAsync(tenant, legalEntityId, requestId, ct)
            ?? throw new PaymentException("payments.request_not_found", "The payment request was not found.");
        if (current.State == "Succeeded") throw new PaymentException("payments.request_not_cancellable", "A confirmed provider payment cannot be cancelled.");
        if (current.State == "Cancelled") return current;
        var operationId = await repository.GetLatestReferenceProviderOperationIdAsync(tenant, legalEntityId, requestId, ct)
            ?? throw new PaymentException("payments.provider_operation_not_found", "The payment request has no provider attempt to cancel.");
        var operation = await repository.GetReferenceProviderOperationAsync(tenant, operationId, ct)
            ?? throw new PaymentException("payments.provider_operation_not_found", "The provider attempt is unavailable.");
        if (!operation.Capabilities.Cancel)
            throw new PaymentException("payments.provider_operation_unsupported", "This provider does not support payment request cancellation.");
        var result = await referenceProvider.CancelPaymentRequestAsync(operation.RequestId, operation.ProviderTransactionId,
            $"cancel:{operation.IdempotencyKey}", ct);
        await repository.RecordReferenceProviderResultAsync(tenant, operation,
            new(result.Status, operation.ProviderTransactionId ?? string.Empty, result.ConfirmedAt), ct);
        return await repository.GetPaymentIntentAsync(tenant, legalEntityId, requestId, ct)
            ?? throw new PaymentException("payments.request_not_found", "The payment request was not found.");
    }

    public async Task ReceiveReferenceWebhookAsync(TenantId tenant, Guid providerConfigId, byte[] rawBody, string signature, CancellationToken ct)
    {
        if (rawBody.Length is 0 or > 65536)
            throw new PaymentException("payments.webhook_invalid", "The provider event is invalid.");
        var secretRef = await repository.GetReferenceProviderSecretRefAsync(tenant, providerConfigId, ct);
        if (string.IsNullOrWhiteSpace(secretRef)) throw new PaymentException("payments.provider_config_unavailable", "The reference provider configuration is unavailable.");
        var signingSecret = providerSecrets.Resolve(secretRef);
        if (string.IsNullOrWhiteSpace(signingSecret)) throw new PaymentException("payments.webhook_secret_unavailable", "The configured provider signing secret is unavailable.");
        var provided = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase) ? signature[7..] : signature;
        byte[] providedDigest;
        try { providedDigest = Convert.FromHexString(provided); }
        catch (FormatException) { throw new PaymentException("payments.webhook_signature_invalid", "The provider signature is invalid."); }
        var expectedDigest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingSecret), rawBody);
        if (providedDigest.Length != expectedDigest.Length || !CryptographicOperations.FixedTimeEquals(providedDigest, expectedDigest))
            throw new PaymentException("payments.webhook_signature_invalid", "The provider signature is invalid.");

        ReferenceWebhookEnvelope webhook;
        try
        {
            using var document = JsonDocument.Parse(rawBody, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 5
                || !root.TryGetProperty("eventId", out var eventId) || !root.TryGetProperty("merchantReference", out var merchant)
                || !root.TryGetProperty("transactionId", out var transaction) || !root.TryGetProperty("eventType", out var eventType)
                || !root.TryGetProperty("occurredAt", out var occurredAt)
                || eventId.ValueKind != JsonValueKind.String || merchant.ValueKind != JsonValueKind.String
                || transaction.ValueKind != JsonValueKind.String || eventType.ValueKind != JsonValueKind.String
                || !occurredAt.TryGetDateTimeOffset(out var occurred))
                throw new JsonException("Webhook fields are invalid.");
            webhook = new ReferenceWebhookEnvelope(eventId.GetString()!, merchant.GetString()!, transaction.GetString()!, eventType.GetString()!, occurred);
            if (webhook.EventId.Length is 0 or > 240 || webhook.MerchantReference.Length is 0 or > 240
                || webhook.TransactionId.Length is 0 or > 240 || webhook.EventType is not ("payment.succeeded" or "payment.failed" or "payment.authorized"))
                throw new JsonException("Webhook values are invalid.");
        }
        catch (JsonException) { throw new PaymentException("payments.webhook_payload_invalid", "The provider event has an unsupported payload."); }

        var encrypted = EncryptWebhook(rawBody, signingSecret);
        var receipt = await repository.AcceptReferenceWebhookAsync(tenant, providerConfigId, webhook, encrypted,
            Convert.ToHexString(SHA256.HashData(rawBody)).ToLowerInvariant(), ct);
        var requestId = await repository.GetReferencePaymentRequestIdAsync(tenant, providerConfigId, webhook.MerchantReference, ct);
        if (requestId is { } verifiedRequestId) referenceProvider.ObserveVerifiedWebhook(verifiedRequestId, webhook.TransactionId, webhook.EventType, webhook.OccurredAt);
        if (!receipt.ShouldComplete || receipt.OperationId is not { } operationId) return;
        await CompleteReferencePaymentOperationAsync(tenant, operationId, webhook.OccurredAt, ct);
        await repository.MarkReferenceWebhookProcessedAsync(tenant, receipt.InboxId, ct);
    }

    private async Task CompleteReferencePaymentOperationAsync(TenantId tenant, Guid operationId, DateTimeOffset eventTime, CancellationToken ct)
    {
        var operation = await repository.GetReferenceProviderOperationAsync(tenant, operationId, ct)
            ?? throw new PaymentException("payments.provider_operation_not_found", "The reference provider operation is unavailable.");
        if (operation.ProviderTransactionId is null) throw new PaymentException("payments.provider_status_unknown", "The provider has not confirmed a payment transaction.");
        var membership = operation.CreatedByMembershipId
            ?? throw new PaymentException("payments.provider_actor_unavailable", "The payment request creator membership is unavailable for financial completion.");
        var date = DateOnly.FromDateTime(eventTime.UtcDateTime);
        var draft = await CreateDraftCoreAsync(tenant, operation.CreatedBy, membership, operation.OperationId,
            new(operation.LegalEntityId, operation.BranchId, PaymentType.CustomerReceipt, operation.PartyId,
                operation.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), operation.CurrencyCode, PaymentMethod.PaymentGateway,
                operation.PaymentAccountId, date, date, null, operation.ProviderTransactionId, operation.MerchantReference, null,
                "PaymentRequest", operation.RequestId.ToString("D")), $"provider-payment:{operation.RequestId:D}", "reference", operation.ProviderTransactionId, ct);
        var completed = await CompleteManualAsync(tenant, operation.CreatedBy, membership, operation.LegalEntityId, draft.Id,
            $"provider-complete:{operation.RequestId:D}", operation.OperationId, ct);
        if (completed.State != PaymentBusinessState.Completed)
            throw new PaymentException("payments.provider_completion_pending", "The provider confirmed the payment; internal financial completion will be retried.");
        await repository.MarkProviderRequestSucceededAsync(tenant, operation.LegalEntityId, operation.RequestId, completed.CompletedAt ?? DateTimeOffset.UtcNow, ct);
    }

    private static byte[] EncryptWebhook(byte[] payload, string secret)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes($"payments-webhook:{secret}"));
        var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16]; var ciphertext = new byte[payload.Length];
        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, payload, ciphertext, tag);
        return [.. nonce, .. tag, .. ciphertext];
    }

    public Task<PaymentPage> ListAsync(TenantId tenant, PaymentListQuery query, CancellationToken ct) => repository.ListAsync(tenant, query, ct);
    public Task<PaymentRecord?> GetAsync(TenantId tenant, Guid legalEntityId, Guid? branchId, Guid id, CancellationToken ct) => repository.GetAsync(tenant, legalEntityId, branchId, id, ct);
    public Task<PaymentRequestDetails?> GetPaymentRequestDetailsAsync(TenantId tenant, Guid legalEntityId, Guid requestId, CancellationToken ct) =>
        repository.GetPaymentRequestDetailsAsync(tenant, legalEntityId, requestId, ct);
    public Task<PaymentRequestPage> ListPaymentRequestsAsync(TenantId tenant, PaymentRequestListQuery query, CancellationToken ct) =>
        repository.ListPaymentRequestsAsync(tenant, query, ct);

    public async Task<PaymentRecord> CompleteManualAsync(TenantId tenant, Guid actor, Guid membership, Guid legalEntityId, Guid paymentId,
        string idempotencyKey, Guid correlationId, CancellationToken ct)
    {
        var payment = await repository.GetAsync(tenant, legalEntityId, null, paymentId, ct) ?? throw new PaymentException("payments.payment_not_found", "The payment was not found.");
        var context = await financeBase.ResolveCalculationContextAsync(tenant, new Shared.Primitives.Identifiers.LegalEntityId(legalEntityId), payment.CurrencyCode,
            FinancialCalculationPurpose.Accounting, payment.PostingDate, ct);
        if (context is null) throw new PaymentException("payments.currency_not_enabled", "The payment currency is no longer enabled for this legal entity and date.");
        return await repository.CompleteManualAsync(tenant, actor, membership, legalEntityId, paymentId, idempotencyKey, context, correlationId, ct);
    }

    public Task<PaymentRecord> CancelPendingAsync(TenantId tenant, Guid actor, Guid membership, Guid legalEntityId, Guid? branchId,
        Guid paymentId, string idempotencyKey, Guid correlationId, CancellationToken ct) =>
        repository.CancelPendingAsync(tenant, actor, membership, legalEntityId, branchId, paymentId, idempotencyKey, correlationId, ct);

    public async Task<SubledgerApplicationResult> ApplyToPaymentAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        Guid legalEntityId, Guid? branchId, Guid paymentId, ApplyPaymentRequest request, string idempotencyKey, CancellationToken ct)
    {
        var payment = await repository.GetAsync(tenant, legalEntityId, branchId, paymentId, ct)
            ?? throw new PaymentException("payments.payment_not_found", "The payment was not found.");
        if (payment.State != PaymentBusinessState.Completed || payment.PartyId is not { } partyId
            || payment.Type is not (PaymentType.CustomerReceipt or PaymentType.CustomerAdvance or PaymentType.SupplierPayment or PaymentType.SupplierAdvance))
            throw new PaymentException("payments.payment_not_applicable", "Only a completed customer receipt or supplier payment can be applied.");
        if (request.Lines.Count is < 1 or > 50 || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new PaymentException("payments.application_request_invalid", "Select at least one open item and enter a valid idempotency key.");

        var receivable = payment.Direction == PaymentDirection.Incoming;
        var direction = receivable ? SubledgerDirectionContract.Receivable : SubledgerDirectionContract.Payable;
        var paymentItems = await subledger.ListOpenItemsAsync(tenant, new(new(legalEntityId), partyId, direction, true, 20, null,
            Search: payment.Id.ToString("D"), CurrencyCode: payment.CurrencyCode), ct);
        var paymentItem = paymentItems.Items.SingleOrDefault(item => item.SourceType == "Payment" && item.SourceReference == payment.Id.ToString("D"))
            ?? throw new PaymentException("payments.payment_open_item_missing", "The completed payment's available balance is not available for application.");
        var lines = new List<ApplicationLineCommand>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            _ = PaymentRules.ParseAmount(line.Amount);
            var target = await subledger.GetOpenItemAsync(tenant, new(legalEntityId), line.TargetOpenItemId, ct)
                ?? throw new PaymentException("payments.application_target_unavailable", "One of the selected open items is no longer available.");
            if (target.PartyId != partyId || target.Direction != direction || target.Id == paymentItem.Id || target.TransactionCurrency != payment.CurrencyCode)
                throw new PaymentException("payments.application_target_invalid", "The selected open item does not match this payment's party and currency.");
            lines.Add(receivable
                ? new(target.Id, paymentItem.Id, line.Amount)
                : new(paymentItem.Id, target.Id, line.Amount));
        }
        var command = new ApplyOpenItemsCommand(new(legalEntityId), partyId, direction, idempotencyKey.Trim(), request.ApplicationDate,
            request.SettlementDate, lines, actor, correlationId);
        try { return await repository.ApplyWhilePaymentLockedAsync(tenant, legalEntityId, paymentId,
            token => subledger.ApplyAsync(tenant, membership, command, token), ct); }
        catch (Exception exception) when (exception.GetType().FullName == "Modules.Subledger.Entities.SubledgerException")
        {
            var subledgerCode = exception.GetType().GetProperty("Code")?.GetValue(exception) as string;
            var code = subledgerCode switch
            {
                "subledger.application_over_source" => "payments.application_over_available",
                "subledger.application_over_target" => "payments.application_over_target",
                "subledger.item_changed" => "payments.application_balance_changed",
                "subledger.application_conflict" => "payments.idempotency_conflict",
                "subledger.control_context_mismatch" => "payments.application_control_context_mismatch",
                _ => "payments.application_invalid"
            };
            throw new PaymentException(code, "The open-item balance or settlement rules changed. Refresh and try again.", exception);
        }
    }

    private static decimal ApprovedBaseRate(FinancialCalculationContextSnapshotContract context, string currency)
    {
        if (currency == context.BaseCurrencyCode) return 1m;
        var snapshot = context.ExchangeRate;
        if (snapshot is null || snapshot.Purpose != FinancialCalculationPurpose.Accounting
            || !string.Equals(snapshot.FromCurrency, currency, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.ToCurrency, context.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase)
            || !decimal.TryParse(snapshot.Rate, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var rate)
            || rate <= 0m)
            throw new PaymentException("payments.fx_rate_missing", "Approved exchange-rate snapshots are required for cross-currency transfers.");
        return rate;
    }

    private static decimal RoundMoney(decimal amount, FinancialCalculationContextSnapshotContract context)
    {
        var rounding = context.RoundingPolicy.ContextRules.SingleOrDefault(rule => rule.Context == FinancialRoundingContext.MoneySettlement)?.Mode
            ?? FinancialRoundingMode.HalfUp;
        var midpoint = rounding switch { FinancialRoundingMode.HalfEven => MidpointRounding.ToEven, FinancialRoundingMode.Down => MidpointRounding.ToZero, _ => MidpointRounding.AwayFromZero };
        return decimal.Round(amount, context.RoundingPolicy.MoneyScale, midpoint);
    }
}
