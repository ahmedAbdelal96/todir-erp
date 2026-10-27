using Core.Transactions;
using Modules.Subledger.Entities;
using Modules.Subledger.Repositories;
using Shared.Contracts.Subledger;
using Shared.Primitives.Identifiers;

namespace Modules.Subledger.Services;

public sealed class SubledgerTransactionCapability(SubledgerRepository repository) : ITransactionalSubledgerOpenItemCapability
{
    public async Task<Guid> CreatePaymentOpenItemAsync(ITransactionContext context, TenantId tenantId, CreateOpenItemsCommand command,
        SubledgerDirectionContract direction, SubledgerPolarityContract polarity, CancellationToken cancellationToken = default)
    {
        if (command.Items.Count != 1) throw new SubledgerException("subledger.payment_items_invalid", "A payment must register exactly one party open item.");
        var subledgerDirection = direction switch
        {
            SubledgerDirectionContract.Receivable => SubledgerDirection.Receivable,
            SubledgerDirectionContract.Payable => SubledgerDirection.Payable,
            _ => throw new SubledgerException("subledger.direction_invalid", "The financial direction is invalid.")
        };
        var itemPolarity = polarity switch
        {
            SubledgerPolarityContract.Debit => OpenItemPolarity.Debit,
            SubledgerPolarityContract.Credit => OpenItemPolarity.Credit,
            _ => throw new SubledgerException("subledger.polarity_invalid", "The payment polarity is invalid.")
        };
        var ids = await repository.CreateOpenItemsInTransactionAsync(context, tenantId, command.LegalEntityId, command.PartyId,
            subledgerDirection, itemPolarity, "Payment", command.Items, cancellationToken, command.CorrelationId, command.ActorId);
        return ids.Single();
    }

    public Task<IReadOnlyList<Guid>> CreateInvoiceOpenItemsAsync(ITransactionContext context, TenantId tenantId, CreateOpenItemsCommand command, SubledgerDirectionContract direction, CancellationToken cancellationToken = default)
    {
        var (subledgerDirection, polarity, sourceType) = direction switch
        {
            SubledgerDirectionContract.Receivable => (SubledgerDirection.Receivable, OpenItemPolarity.Debit, "Invoice"),
            SubledgerDirectionContract.Payable => (SubledgerDirection.Payable, OpenItemPolarity.Credit, "Bill"),
            _ => throw new SubledgerException("subledger.direction_invalid", "The financial direction is invalid.")
        };
        return repository.CreateOpenItemsInTransactionAsync(context, tenantId, command.LegalEntityId, command.PartyId,
            subledgerDirection, polarity, sourceType, command.Items, cancellationToken, command.CorrelationId, command.ActorId);
    }

    public Task<IReadOnlyList<Guid>> CreateInvoiceCorrectionOpenItemsAsync(ITransactionContext context, TenantId tenantId, CreateOpenItemsCommand command,
        SubledgerDirectionContract direction, SubledgerPolarityContract polarity, CancellationToken cancellationToken = default)
    {
        var subledgerDirection = direction switch
        {
            SubledgerDirectionContract.Receivable => SubledgerDirection.Receivable,
            SubledgerDirectionContract.Payable => SubledgerDirection.Payable,
            _ => throw new SubledgerException("subledger.direction_invalid", "The financial direction is invalid.")
        };
        var itemPolarity = polarity switch
        {
            SubledgerPolarityContract.Debit => OpenItemPolarity.Debit,
            SubledgerPolarityContract.Credit => OpenItemPolarity.Credit,
            _ => throw new SubledgerException("subledger.polarity_invalid", "The correction polarity is invalid.")
        };
        return repository.CreateOpenItemsInTransactionAsync(context, tenantId, command.LegalEntityId, command.PartyId,
            subledgerDirection, itemPolarity, "CreditNote", command.Items, cancellationToken, command.CorrelationId, command.ActorId);
    }
}
