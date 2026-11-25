using Npgsql;
using Shared.Contracts.Ledger;
using Shared.Contracts.Banking;
using Shared.Contracts.Subledger;
using Shared.Primitives.Identifiers;

namespace Core.Transactions;

public interface ITransactionContext : IAsyncDisposable
{
    NpgsqlConnection Connection { get; }
    NpgsqlTransaction Transaction { get; }
    void Enlist(NpgsqlCommand command);
    void AddOutbox(OutboxMessage message);
}

/// <summary>
/// Adapts a transaction opened by an owning module to a transactional capability.
/// The adapter deliberately does not own or commit the transaction; the caller remains
/// responsible for the unit-of-work boundary and its rollback semantics.
/// </summary>
public sealed class ExistingTransactionContext(NpgsqlConnection connection, NpgsqlTransaction transaction) : ITransactionContext
{
    public NpgsqlConnection Connection { get; } = connection;
    public NpgsqlTransaction Transaction { get; } = transaction;
    public void Enlist(NpgsqlCommand command) => command.Transaction = Transaction;
    public void AddOutbox(OutboxMessage message) => throw new InvalidOperationException("transaction.outbox_owner_required");
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public enum PlanningBudgetReservationKind
{
    Precommitment,
    Commitment
}

public sealed record PlanningBudgetReservationRequest(Guid LegalEntityId, Guid BucketId, string SourceModule, string SourceDocumentId,
    string SourceLineId, string SourceVersion, decimal Amount, string CurrencyCode, PlanningBudgetReservationKind Kind, DateOnly? ControlPeriodStart = null);

public sealed record PlanningBudgetConsumptionRequest(Guid LegalEntityId, string SourceModule, string SourceDocumentId, string SourceLineId,
    string SourceVersion, decimal Amount, string CurrencyCode);

public sealed record PlanningBudgetCorrectionRequest(Guid LegalEntityId, string SourceModule, string SourceDocumentId, string SourceLineId,
    string CorrectionSourceModule, string CorrectionSourceDocumentId, string CorrectionSourceLineId, string CorrectionSourceVersion,
    decimal Amount, string CurrencyCode);

public sealed record PlanningBudgetControlScope(Guid LegalEntityId, DateOnly PeriodStart, string MetricCode, string DimensionKeyJson, Guid? RequestedBucketId = null);

public sealed record PlanningBudgetReservationResult(Guid ReservationId, string Decision, decimal AvailableAfter);

public sealed class PlanningBudgetControlException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>Planning owns the budget-control ledger; source modules enlist it in their transaction.</summary>
public interface ITransactionalPlanningBudgetControlCapability
{
    Task<Guid?> ResolveApplicableBucketAsync(ITransactionContext context, TenantId tenantId, PlanningBudgetControlScope scope,
        CancellationToken cancellationToken = default);
    Task<bool> HasOpenReservationAsync(ITransactionContext context, TenantId tenantId, Guid legalEntityId, string sourceModule,
        string sourceDocumentId, string sourceLineId, CancellationToken cancellationToken = default);
    Task<PlanningBudgetReservationResult> UpsertReservationAsync(ITransactionContext context, TenantId tenantId,
        Guid actorId, PlanningBudgetReservationRequest request, CancellationToken cancellationToken = default);
    Task ReleaseReservationAsync(ITransactionContext context, TenantId tenantId, string sourceModule,
        string sourceDocumentId, string sourceLineId, CancellationToken cancellationToken = default);
    Task<PlanningBudgetReservationResult> ConsumeReservationAsync(ITransactionContext context, TenantId tenantId,
        Guid actorId, PlanningBudgetConsumptionRequest request, CancellationToken cancellationToken = default);
    Task<bool> ReverseConsumptionAsync(ITransactionContext context, TenantId tenantId, Guid actorId,
        PlanningBudgetCorrectionRequest request, CancellationToken cancellationToken = default);
}

public interface ITransactionCoordinator
{
    Task<T> ExecuteAsync<T>(TenantId tenantId, Func<ITransactionContext, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}

/// <summary>Banking owns the canonical treasury account and can enlist its write in a caller transaction.</summary>
public interface ITransactionalBankingTreasuryAccountCapability
{
    Task<TreasuryAccountReference> CreateInTransactionAsync(ITransactionContext context, TenantId tenantId, Guid actorId,
        Guid membershipId, Guid correlationId, TreasuryAccountCreateRequest request, CancellationToken cancellationToken = default);
    Task<TreasuryAccountReference> ChangeStatusInTransactionAsync(ITransactionContext context, TenantId tenantId, Guid actorId,
        Guid membershipId, Guid correlationId, Guid legalEntityId, Guid id, TreasuryAccountStatus status,
        long? expectedVersion = null, CancellationToken cancellationToken = default);
}

/// <summary>Payments owns the compatibility projection and exposes read-only reconciliation evidence inside a transaction.</summary>
public interface ITransactionalPaymentAccountCompatibilityCapability
{
    Task CreateCompatibilityInTransactionAsync(ITransactionContext context, TenantId tenantId, Guid legalEntityId,
        Guid accountId, Guid? branchId, string code, string name, string accountType, string currencyPolicy,
        string? currencyCode, string? bankName, string? accountHolder, string? maskedIdentifier, string? secretRef,
        string status, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task ChangeCompatibilityStatusInTransactionAsync(ITransactionContext context, TenantId tenantId, Guid legalEntityId,
        Guid accountId, string status, CancellationToken cancellationToken = default);
}

public sealed record PaymentReconciliationMovement(Guid PaymentId, Guid LegalEntityId, Guid PaymentAccountId,
    string Direction, decimal Amount, string CurrencyCode, string BusinessState);

public interface ITransactionalPaymentReconciliationReadCapability
{
    Task<PaymentReconciliationMovement?> GetForReconciliationAsync(ITransactionContext context, TenantId tenantId,
        Guid legalEntityId, Guid paymentId, CancellationToken cancellationToken = default);
}

/// <summary>Stable cross-module seam for Ledger work inside a caller-owned transaction.</summary>
public interface ITransactionalLedgerPostingCapability
{
    Task<LedgerPostingResult> PostExplicitAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, ExplicitLedgerPostingPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostPayrollAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, PayrollPostedPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostExpenseAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, ExpenseRecognizedPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> ReverseAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid originalEntryId, DateOnly reversalDate, DateOnly? autoReversalDate, string reason, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostSettlementFxDifferenceAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, SettlementFxDifferencePayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostSettlementWriteOffAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, SettlementWriteOffPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostPosCashSaleAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, PosCashSalePostingPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostBankOriginatedAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, BankOriginatedFinancialEventPayload payload, CancellationToken cancellationToken = default);
}

public interface ITransactionalFixedAssetLedgerPostingCapability
{
    Task<LedgerPostingResult> PostFixedAssetAcquisitionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetAcquisitionPostedPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostFixedAssetDepreciationAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetDepreciationPostedPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostFixedAssetDepreciationReversalAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetDepreciationReversedPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostFixedAssetDisposalAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetDisposalPostedPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostFixedAssetDisposalReversalAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetDisposalReversedPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostFixedAssetCostAddedAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetCostAdjustmentPostedPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostFixedAssetCostReducedAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetCostAdjustmentPostedPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostFixedAssetImpairmentAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetImpairmentPostedPayload payload, CancellationToken cancellationToken = default);
}

public interface ITransactionalPaymentLedgerPostingCapability
{
    Task<LedgerPostingResult> PostPaymentAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
        PaymentFactPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostPaymentFeeAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
        PaymentFeeFactPayload payload, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostPaymentFeeReversalAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
        Guid originalFeeFactId, string evidenceReference, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostInternalTransferAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
        InternalTransferFactPayload payload, CancellationToken cancellationToken = default);
}

/// <summary>Posts Inventory-owned valuation facts using Ledger-owned account determination.</summary>
public interface ITransactionalInventoryLedgerPostingCapability
{
    Task<string?> ValidateInventoryPostingDateAsync(ITransactionContext context, TenantId tenantId, LegalEntityId legalEntityId,
        DateOnly postingDate, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostInventoryValuationAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, InventoryValuationFactPayload payload,
        CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> ReverseInventoryValuationAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, InventoryValuationReversalPayload payload,
        CancellationToken cancellationToken = default);
}

public interface ITransactionalInvoiceLedgerPostingCapability
{
    Task<Guid> GetActiveConfigurationVersionIdAsync(ITransactionContext context, TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default);
    Task<LedgerPostingResult> PostInvoiceAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
        InvoiceLedgerPostingContract invoice, CancellationToken cancellationToken = default);
    Task<InvoiceAccountingPreview> PreviewInvoiceAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
        InvoiceLedgerPostingContract invoice, CancellationToken cancellationToken = default);
}

/// <summary>Creates invoice open items within the originating invoice transaction.</summary>
public interface ITransactionalSubledgerOpenItemCapability
{
    Task<IReadOnlyList<Guid>> CreateInvoiceOpenItemsAsync(ITransactionContext context, TenantId tenantId, CreateOpenItemsCommand command, SubledgerDirectionContract direction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> CreateInvoiceCorrectionOpenItemsAsync(ITransactionContext context, TenantId tenantId, CreateOpenItemsCommand command,
        SubledgerDirectionContract direction, SubledgerPolarityContract polarity, CancellationToken cancellationToken = default);
    Task<Guid> CreatePaymentOpenItemAsync(ITransactionContext context, TenantId tenantId, CreateOpenItemsCommand command,
        SubledgerDirectionContract direction, SubledgerPolarityContract polarity, CancellationToken cancellationToken = default);
}

public sealed record OutboxMessage(Guid MessageId, string MessageType, string Payload, TenantId TenantId, DateTimeOffset OccurredAt, Guid CorrelationId, Guid CausationId, string SourceModule, string? TraceParent = null);

public sealed class NpgsqlTransactionCoordinator(NpgsqlDataSource dataSource) : ITransactionCoordinator
{
    public async Task<T> ExecuteAsync<T>(TenantId tenantId, Func<ITransactionContext, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var contextCommand = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
        {
            contextCommand.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await contextCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var context = new TransactionContext(connection, transaction);
        try
        {
            var result = await action(context, cancellationToken);
            await context.FlushOutboxAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            throw;
        }
    }

    private sealed class TransactionContext(NpgsqlConnection connection, NpgsqlTransaction transaction) : ITransactionContext
    {
        private readonly List<OutboxMessage> messages = [];
        public NpgsqlConnection Connection => connection;
        public NpgsqlTransaction Transaction => transaction;
        public void Enlist(NpgsqlCommand command) => command.Transaction = transaction;
        public void AddOutbox(OutboxMessage message) => messages.Add(message);
        public async Task FlushOutboxAsync(CancellationToken cancellationToken)
        {
            foreach (var message in messages)
            {
                await using var command = new NpgsqlCommand("insert into platform.outbox_message (id, message_type, payload, tenant_id, occurred_at, correlation_id, causation_id, source_module, trace_parent, status) values ($1, $2, $3::jsonb, $4, $5, $6, $7, $8, $9, 'Pending')", connection, transaction);
                command.Parameters.AddWithValue(message.MessageId); command.Parameters.AddWithValue(message.MessageType); command.Parameters.AddWithValue(message.Payload); command.Parameters.AddWithValue(message.TenantId.Value); command.Parameters.AddWithValue(message.OccurredAt); command.Parameters.AddWithValue(message.CorrelationId); command.Parameters.AddWithValue(message.CausationId); command.Parameters.AddWithValue(message.SourceModule); command.Parameters.AddWithValue((object?)message.TraceParent ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
