using Core.Transactions;
using Modules.Ledger.Repositories;
using Shared.Contracts.Ledger;

namespace Modules.Ledger.Services;

public sealed class LedgerTransactionCapability(LedgerRepository repository) : ITransactionalLedgerPostingCapability
{
    public Task<LedgerPostingResult> PostExplicitAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, ExplicitLedgerPostingPayload payload, CancellationToken cancellationToken = default) => repository.PostExplicitInTransactionAsync(context, fact, payload, cancellationToken: cancellationToken);
    public Task<LedgerPostingResult> PostSettlementFxDifferenceAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, SettlementFxDifferencePayload payload, CancellationToken cancellationToken = default) => repository.PostSettlementFxDifferenceInTransactionAsync(context, fact, payload, cancellationToken);
    public Task<LedgerPostingResult> PostSettlementWriteOffAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, SettlementWriteOffPayload payload, CancellationToken cancellationToken = default) => repository.PostSettlementWriteOffInTransactionAsync(context, fact, payload, cancellationToken);
}
