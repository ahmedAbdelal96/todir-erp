using Core.Transactions;
using Modules.Ledger.Repositories;
using Shared.Contracts.Ledger;

namespace Modules.Ledger.Services;

public sealed class LedgerTransactionCapability(LedgerRepository repository) : ITransactionalLedgerPostingCapability, ITransactionalInvoiceLedgerPostingCapability, ITransactionalPaymentLedgerPostingCapability
{
    public Task<LedgerPostingResult> PostPaymentAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, PaymentFactPayload payload, CancellationToken cancellationToken = default) => repository.PostPaymentInTransactionAsync(context, fact, payload, cancellationToken);
    public Task<LedgerPostingResult> PostPaymentFeeAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, PaymentFeeFactPayload payload, CancellationToken cancellationToken = default) => repository.PostPaymentFeeInTransactionAsync(context, fact, payload, cancellationToken);
    public Task<LedgerPostingResult> PostInternalTransferAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, InternalTransferFactPayload payload, CancellationToken cancellationToken = default) => repository.PostInternalTransferInTransactionAsync(context, fact, payload, cancellationToken);
    public Task<Guid> GetActiveConfigurationVersionIdAsync(ITransactionContext context, Shared.Primitives.Identifiers.TenantId tenantId, Shared.Primitives.Identifiers.LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => LedgerRepository.GetActiveConfigurationVersionIdAsync(context, tenantId, legalEntityId, cancellationToken);
    public Task<LedgerPostingResult> PostInvoiceAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, InvoiceLedgerPostingContract invoice, CancellationToken cancellationToken = default) => repository.PostInvoiceInTransactionAsync(context, fact, invoice, cancellationToken);
    public Task<InvoiceAccountingPreview> PreviewInvoiceAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, InvoiceLedgerPostingContract invoice, CancellationToken cancellationToken = default) => repository.PreviewInvoiceInTransactionAsync(context, fact, invoice, cancellationToken);
    public Task<LedgerPostingResult> PostExplicitAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, ExplicitLedgerPostingPayload payload, CancellationToken cancellationToken = default) => repository.PostExplicitInTransactionAsync(context, fact, payload, cancellationToken: cancellationToken);
    public Task<LedgerPostingResult> PostSettlementFxDifferenceAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, SettlementFxDifferencePayload payload, CancellationToken cancellationToken = default) => repository.PostSettlementFxDifferenceInTransactionAsync(context, fact, payload, cancellationToken);
    public Task<LedgerPostingResult> PostSettlementWriteOffAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, SettlementWriteOffPayload payload, CancellationToken cancellationToken = default) => repository.PostSettlementWriteOffInTransactionAsync(context, fact, payload, cancellationToken);
}
