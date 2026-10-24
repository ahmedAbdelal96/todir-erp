using System.Text.Json;
using Core.Transactions;
using Modules.Ledger.DTOs;
using Modules.Ledger.Repositories;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger.Services;

public sealed class LedgerPreviewService(ITransactionCoordinator transactions, ITransactionalLedgerPostingCapability posting)
{
    public async Task<PostingPreviewDto> PreviewExplicitAsync(TenantId tenant, LegalEntityId entity, PostingPreviewRequest request, Guid actor, Guid correlation, CancellationToken ct)
    {
        try
        {
            await transactions.ExecuteAsync<object?>(tenant, async (context, token) =>
            {
                var payload = new ExplicitLedgerPostingPayload(request.JournalId, request.Description, request.BaseCurrencyCode, request.Lines.Select(line => new LedgerPostingLinePayload(line.AccountId, line.Debit, line.Credit, line.TransactionCurrencyCode, line.TransactionDebit, line.TransactionCredit, line.BaseDebit, line.BaseCredit, line.ExchangeRate, [], line.Description, null)).ToArray());
                var fact = new AccountingFactEnvelope<IAccountingFactPayload>(Guid.CreateVersion7(), tenant, entity, AccountingFactTypes.ManualGeneralJournalPosted, 1, "ledger-preview", $"preview:{Guid.CreateVersion7():N}", $"preview:{Guid.CreateVersion7():N}", request.PostingDate, request.PostingDate, payload, correlation, actor);
                var posted = await posting.PostExplicitAsync(context, fact, payload, token);
                throw new PreviewRollbackException(await LedgerPreviewRepository.ReadAsync(context, posted, token));
            }, ct);
        }
        catch (PreviewRollbackException preview)
        {
            return preview.Result;
        }
        throw new InvalidOperationException("ledger.preview_failed");
    }

    private sealed class PreviewRollbackException(PostingPreviewDto result) : Exception
    {
        public PostingPreviewDto Result { get; } = result;
    }
}
