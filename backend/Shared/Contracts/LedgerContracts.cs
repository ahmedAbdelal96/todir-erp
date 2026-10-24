using Shared.Primitives.Identifiers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Shared.Contracts.Ledger;

public static class AccountingFactTypes
{
    public const string CustomerInvoicePosted = "CustomerInvoicePosted";
    public const string CustomerCreditNotePosted = "CustomerCreditNotePosted";
    public const string VendorBillPosted = "VendorBillPosted";
    public const string VendorCreditNotePosted = "VendorCreditNotePosted";
    public const string PaymentReceived = "PaymentReceived";
    public const string PaymentMade = "PaymentMade";
    public const string InventoryReceiptValued = "InventoryReceiptValued";
    public const string InventoryIssueValued = "InventoryIssueValued";
    public const string InventoryCostAdjustment = "InventoryCostAdjustment";
    public const string ExpenseRecognized = "ExpenseRecognized";
    public const string PayrollPosted = "PayrollPosted";
    public const string TaxAdjustment = "TaxAdjustment";
    public const string SettlementFxDifference = "SettlementFxDifference";
    public const string UnrealizedFxRevaluation = "UnrealizedFxRevaluation";
    public const string OpeningBalancePosted = "OpeningBalancePosted";
    public const string ManualGeneralJournalPosted = "ManualGeneralJournalPosted";

    public static IReadOnlySet<string> Known { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        CustomerInvoicePosted, CustomerCreditNotePosted, VendorBillPosted, VendorCreditNotePosted,
        PaymentReceived, PaymentMade, InventoryReceiptValued, InventoryIssueValued, InventoryCostAdjustment,
        ExpenseRecognized, PayrollPosted, TaxAdjustment, SettlementFxDifference, UnrealizedFxRevaluation,
        OpeningBalancePosted, ManualGeneralJournalPosted
    };
}

public static class LedgerEventTypes
{
    public const string JournalEntryPostedV1 = "JournalEntryPostedV1";
    public const string JournalEntryReversedV1 = "JournalEntryReversedV1";
    public const string PeriodClosedV1 = "PeriodClosedV1";
    public const string PeriodReopenedV1 = "PeriodReopenedV1";
    public const string AccountingConfigurationActivatedV1 = "AccountingConfigurationActivatedV1";
    public const string LedgerConfigurationChanged = "LedgerConfigurationChanged";
}

public sealed record JournalEntryPostedEvent(Guid FactId, Guid JournalEntryId, Guid LegalEntityId, string FactType, int FactVersion, string EntryNumber);
public sealed record JournalEntryReversedEvent(Guid OriginalEntryId, Guid ReversalEntryId, Guid LegalEntityId, string Reason);
public sealed record PeriodStatusChangedEvent(Guid PeriodId, Guid FiscalYearId, Guid LegalEntityId, string PreviousStatus, string NewStatus);
public sealed record AccountingConfigurationActivatedEvent(Guid ConfigurationVersionId, Guid LegalEntityId, LedgerModeValue Mode, string TemplateCode);
public enum LedgerModeValue { Managed, Full }

public interface IAccountingFactPayload;

public sealed record InvoiceTaxComponent(string Code, decimal Rate, decimal Amount, string AccountRole) : IAccountingFactPayload;

/// <summary>Semantic invoice fact. It contains immutable business amounts, never ledger account identifiers.</summary>
public sealed record CustomerInvoicePostedPayload(
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount,
    string CurrencyCode,
    string ReceivablePartyGroup,
    string RevenueCategory,
    IReadOnlyList<InvoiceTaxComponent> Taxes,
    decimal? ExchangeRate = null,
    string? ExchangeRateReference = null) : IAccountingFactPayload;

public sealed record CustomerCreditNotePostedPayload(decimal NetAmount, decimal TaxAmount, decimal GrossAmount, string CurrencyCode, string ReceivablePartyGroup, string RevenueCategory, IReadOnlyList<InvoiceTaxComponent> Taxes) : IAccountingFactPayload;
public sealed record VendorBillPostedPayload(decimal NetAmount, decimal TaxAmount, decimal GrossAmount, string CurrencyCode, string PayablePartyGroup, string ExpenseCategory, IReadOnlyList<InvoiceTaxComponent> Taxes) : IAccountingFactPayload;
public sealed record VendorCreditNotePostedPayload(decimal NetAmount, decimal TaxAmount, decimal GrossAmount, string CurrencyCode, string PayablePartyGroup, string ExpenseCategory, IReadOnlyList<InvoiceTaxComponent> Taxes) : IAccountingFactPayload;
public sealed record PaymentFactPayload(decimal Amount, string CurrencyCode, string PartyGroup, string SettlementRole) : IAccountingFactPayload;
public sealed record InventoryValuationFactPayload(decimal Amount, string CurrencyCode, string InventoryGroup, string ValuationRole) : IAccountingFactPayload;
public sealed record ExpenseRecognizedPayload(decimal Amount, string CurrencyCode, string ExpenseCategory) : IAccountingFactPayload;
public sealed record PayrollPostedPayload(decimal GrossAmount, decimal PayableAmount, string CurrencyCode) : IAccountingFactPayload;
public sealed record TaxAdjustmentPayload(decimal Amount, string CurrencyCode, string TaxCode, string AdjustmentRole) : IAccountingFactPayload;
public sealed record FxDifferencePayload(decimal Amount, string CurrencyCode, string DifferenceRole) : IAccountingFactPayload;
public sealed record OpeningBalancePostedPayload(decimal Amount, string CurrencyCode, string SemanticRole) : IAccountingFactPayload;
public sealed record ManualGeneralJournalPostedPayload(string Description, string CurrencyCode, IReadOnlyList<string> SemanticRoles) : IAccountingFactPayload;

public sealed record LedgerDimensionAssignment(string Code, Guid ValueId, string ValueCode);

/// <summary>Ledger-owned line instruction used only by Ledger-owned workflows (manual and opening postings).</summary>
public sealed record LedgerPostingLinePayload(
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string TransactionCurrencyCode,
    decimal TransactionDebit,
    decimal TransactionCredit,
    decimal BaseDebit,
    decimal BaseCredit,
    decimal ExchangeRate,
    IReadOnlyList<LedgerDimensionAssignment> Dimensions,
    string? Description,
    string? LegacyReference);

public sealed record ExplicitLedgerPostingPayload(
    Guid JournalId,
    string Description,
    string BaseCurrencyCode,
    IReadOnlyList<LedgerPostingLinePayload> Lines,
    string? LegacySystem = null,
    string? LegacyBatchId = null,
    string? MigrationFileHash = null) : IAccountingFactPayload;

public sealed record AccountingFactEnvelope<TPayload>(
    Guid FactId,
    TenantId TenantId,
    LegalEntityId LegalEntityId,
    string FactType,
    int FactVersion,
    string SourceModule,
    string SourceReference,
    string IdempotencyKey,
    DateOnly BusinessDate,
    DateOnly PostingDate,
    TPayload Payload,
    Guid CorrelationId,
    Guid ActorId) where TPayload : IAccountingFactPayload;

public sealed record AccountDeterminationExplanation(
    string AccountRole,
    string MappingReference,
    long MappingVersion,
    Guid MappingId,
    Guid MappingVersionId,
    string SelectedAccountCode,
    Guid SelectedAccountId,
    Guid LegalEntityId,
    int SpecificityRank,
    string SpecificityContext);

public sealed record LedgerPostingResult(
    Guid FactId,
    Guid JournalEntryId,
    string EntryNumber,
    string Status,
    bool AlreadyPosted,
    IReadOnlyList<AccountDeterminationExplanation>? Determinations = null);

public static class AccountingFactFingerprint
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Compute<TPayload>(TPayload payload) where TPayload : IAccountingFactPayload
    {
        var node = JsonSerializer.SerializeToNode(payload, Options) ?? throw new InvalidOperationException("Accounting fact payload cannot be serialized.");
        var canonical = Canonicalize(node);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string Canonicalize(JsonNode? node)
    {
        if (node is null) return "null";
        if (node is JsonObject obj)
        {
            var values = obj.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{JsonSerializer.Serialize(pair.Key)}:{Canonicalize(pair.Value)}");
            return $"{{{string.Join(',', values)}}}";
        }
        if (node is JsonArray array) return $"[{string.Join(',', array.Select(Canonicalize))}]";
        return node.ToJsonString(Options);
    }
}

public interface ILedgerPostingEngine
{
    Task<LedgerPostingResult> PostCustomerInvoiceAsync(
        AccountingFactEnvelope<CustomerInvoicePostedPayload> fact,
        CancellationToken cancellationToken = default);

    Task<LedgerPostingResult> PostAsync(
        AccountingFactEnvelope<IAccountingFactPayload> fact,
        CancellationToken cancellationToken = default);

    Task<LedgerPostingResult> ReverseAsync(
        TenantId tenantId,
        LegalEntityId legalEntityId,
        Guid originalEntryId,
        DateOnly reversalDate,
        DateOnly? autoReversalDate,
        string reason,
        Guid actorId,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}
