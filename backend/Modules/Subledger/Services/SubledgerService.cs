using System.Globalization;
using System.Text.Json;
using Modules.Subledger.Entities;
using Modules.Subledger.Repositories;
using Shared.Contracts.Directory;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Subledger;
using Shared.Primitives.Financial;
using Shared.Primitives.Identifiers;
using Core.Services;

namespace Modules.Subledger.Services;

public sealed class SubledgerService(
    SubledgerRepository repository,
    IDirectoryCapabilities directory,
    IFinanceBaseCapabilities financeBase,
    ILegalEntityFinancialReadStore legalEntities,
    TimeProvider clock) : ISubledgerCapabilities
{
    public async Task<PartyFinancialProfileResult?> GetFinancialProfileAsync(TenantId tenantId, LegalEntityId entityId, Guid partyId, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, entityId, partyId);
        await EnsureEntityAsync(tenantId, entityId, cancellationToken);
        await EnsurePartyAsync(tenantId, entityId, partyId, cancellationToken);
        return await repository.GetProfileAsync(tenantId, entityId, partyId, cancellationToken);
    }

    public async Task<PartyFinancialProfileResult> UpdateFinancialProfileAsync(TenantId tenantId, PartyFinancialProfileCommand command, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.PartyId);
        if (command.ExpectedVersion < 0) throw new SubledgerException("subledger.profile_version_invalid", "The profile version is invalid.");
        await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        await EnsurePartyAsync(tenantId, command.LegalEntityId, command.PartyId, cancellationToken);
        var limit = ParseOptionalAmount(command.CreditLimitAmount);
        if (limit is < 0m) throw new SubledgerException("subledger.credit_limit_invalid", "Credit limit must be zero or greater.");
        var creditCurrency = command.CreditLimitCurrency is null ? null : NormalizeCurrency(command.CreditLimitCurrency);
        if ((limit is null) != (creditCurrency is null)) throw new SubledgerException("subledger.credit_limit_invalid", "Credit limit amount and currency must be provided together.");
        if (limit is { } configuredLimit) EnsurePrecision(configuredLimit);
        if (creditCurrency is not null) await EnsureCurrencyEnabledAsync(tenantId, command.LegalEntityId, creditCurrency, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), cancellationToken);
        ValidatePolicyCode(command.ReceivablePolicyCode);
        ValidatePolicyCode(command.PayablePolicyCode);
        var asOf = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        await ValidatePaymentTermAsync(tenantId, command.DefaultReceivablePaymentTermId, asOf, cancellationToken);
        await ValidatePaymentTermAsync(tenantId, command.DefaultPayablePaymentTermId, asOf, cancellationToken);
        return await repository.SaveProfileAsync(tenantId, command, limit, creditCurrency, cancellationToken);
    }

    public Task<IReadOnlyList<Guid>> CreateReceivableOpenItemsAsync(TenantId tenantId, CreateOpenItemsCommand command, CancellationToken cancellationToken = default) =>
        CreateSourceItemsAsync(tenantId, command, SubledgerDirection.Receivable, OpenItemPolarity.Debit, "Invoice", cancellationToken);

    public Task<IReadOnlyList<Guid>> CreatePayableOpenItemsAsync(TenantId tenantId, CreateOpenItemsCommand command, CancellationToken cancellationToken = default) =>
        CreateSourceItemsAsync(tenantId, command, SubledgerDirection.Payable, OpenItemPolarity.Credit, "Bill", cancellationToken);

    public async Task<Guid> RegisterPaymentCreditAsync(TenantId tenantId, RegisterPaymentCreditCommand command, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.PartyId);
        EnsureActor(command.ActorId, command.CorrelationId);
        var direction = MapDirection(command.Direction);
        var credit = command.Credit with { DueDate = command.Credit.PostingDate };
        var baseCurrency = await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        await EnsurePartyAsync(tenantId, command.LegalEntityId, command.PartyId, cancellationToken);
        await EnsureCurrencyEnabledAsync(tenantId, command.LegalEntityId, NormalizeCurrency(credit.TransactionCurrency), credit.PostingDate, cancellationToken);
        var normalized = ValidateSnapshot(credit, direction, baseCurrency);
        var ids = await repository.CreateOpenItemsAsync(tenantId, command.LegalEntityId, command.PartyId, direction, SubledgerRules.PaymentPolarity(direction), "Payment", [normalized], cancellationToken);
        return ids[0];
    }

    public async Task<SubledgerApplicationResult> ApplyAsync(TenantId tenantId, ApplyOpenItemsCommand command, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.PartyId);
        EnsureActor(command.ActorId, command.CorrelationId);
        if (command.Lines is null || command.Lines.Count is < 1 or > 200) throw new SubledgerException("subledger.application_lines_invalid", "An application must contain between one and 200 lines.");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Trim().Length > 200) throw new SubledgerException("subledger.idempotency_key_invalid", "A valid idempotency key is required.");
        var amounts = command.Lines.Select(line => ParseAmount(line.Amount)).ToArray();
        foreach (var amount in amounts) { SubledgerRules.ValidatePositiveAmount(amount); EnsurePrecision(amount); }
        var baseCurrency = await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        var financialContext = await financeBase.ResolveCalculationContextAsync(tenantId, command.LegalEntityId, baseCurrency, FinancialCalculationPurpose.Accounting, command.SettlementDate, cancellationToken)
            ?? throw new SubledgerException("subledger.financial_policy_unavailable", "The legal entity financial rounding policy is unavailable.");
        var rule = financialContext.RoundingPolicy.ContextRules.SingleOrDefault(item => item.Context == FinancialRoundingContext.PaymentAllocation)
            ?? throw new SubledgerException("subledger.financial_policy_unavailable", "The financial policy has no application rounding rule.");
        if (!Enum.TryParse<FinancialRoundingModeValue>(rule.Mode.ToString(), out var mode)) throw new SubledgerException("subledger.financial_policy_unavailable", "The application rounding rule is not supported.");
        return await repository.ApplyAsync(tenantId, command, amounts, financialContext.RoundingPolicy.MoneyScale, mode, cancellationToken);
    }

    public async Task<SubledgerApplicationResult> ReverseAsync(TenantId tenantId, ReverseApplicationCommand command, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.ApplicationId);
        EnsureActor(command.ActorId, command.CorrelationId);
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Trim().Length > 200) throw new SubledgerException("subledger.idempotency_key_invalid", "A valid idempotency key is required.");
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > 1000) throw new SubledgerException("subledger.reversal_reason_required", "A reason is required to reverse an application.");
        _ = await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        return await repository.ReverseAsync(tenantId, command, cancellationToken);
    }

    public async Task<OpenItemPage> ListOpenItemsAsync(TenantId tenantId, OpenItemListQuery query, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, query.LegalEntityId, query.PartyId);
        if (query.Limit is < 1 or > 200) throw new SubledgerException("subledger.page_size_invalid", "Page size must be between 1 and 200.");
        if (query.Cursor is not null && !Guid.TryParse(query.Cursor, out _)) throw new SubledgerException("subledger.cursor_invalid", "The open-item cursor is invalid.");
        if (query.Direction is not null && !Enum.IsDefined(query.Direction.Value)) throw new SubledgerException("subledger.direction_invalid", "The financial direction is invalid.");
        await EnsureEntityAsync(tenantId, query.LegalEntityId, cancellationToken);
        await EnsurePartyAsync(tenantId, query.LegalEntityId, query.PartyId, cancellationToken);
        return await repository.ListAsync(tenantId, query, cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> CreateSourceItemsAsync(TenantId tenantId, CreateOpenItemsCommand command, SubledgerDirection direction, OpenItemPolarity polarity, string requiredSourceType, CancellationToken cancellationToken)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.PartyId);
        EnsureActor(command.ActorId, command.CorrelationId);
        if (command.Items is null || command.Items.Count is < 1 or > 200) throw new SubledgerException("subledger.source_items_invalid", "A source must contain between one and 200 scheduled items.");
        var identities = command.Items.Select(item => (item.SourceReference.Trim(), Schedule: string.IsNullOrWhiteSpace(item.SourceScheduleReference) ? null : item.SourceScheduleReference.Trim())).ToArray();
        if (identities.Distinct().Count() != identities.Length) throw new SubledgerException("subledger.source_schedule_duplicate", "Source schedule identities must be unique.");
        var baseCurrency = await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        await EnsurePartyAsync(tenantId, command.LegalEntityId, command.PartyId, cancellationToken);
        foreach (var pair in command.Items.Select(item => (Currency: NormalizeCurrency(item.TransactionCurrency), item.PostingDate)).Distinct())
            await EnsureCurrencyEnabledAsync(tenantId, command.LegalEntityId, pair.Currency, pair.PostingDate, cancellationToken);
        var snapshots = new List<OpenItemSnapshotContract>(command.Items.Count);
        foreach (var item in command.Items) snapshots.Add(ValidateSnapshot(item, direction, baseCurrency));
        return await repository.CreateOpenItemsAsync(tenantId, command.LegalEntityId, command.PartyId, direction, polarity, requiredSourceType, snapshots, cancellationToken);
    }

    private static OpenItemSnapshotContract ValidateSnapshot(OpenItemSnapshotContract item, SubledgerDirection direction, string entityBase)
    {
        if (string.IsNullOrWhiteSpace(item.SourceReference) || item.SourceReference.Trim().Length > 240) throw new SubledgerException("subledger.source_reference_invalid", "A stable source reference is required.");
        if (item.SourceScheduleReference?.Trim().Length > 240 || item.SourceDisplayNumber?.Trim().Length > 120) throw new SubledgerException("subledger.source_reference_invalid", "A source reference exceeds its allowed length.");
        if (item.DocumentDate == DateOnly.MinValue || item.PostingDate == DateOnly.MinValue || item.DueDate == DateOnly.MinValue) throw new SubledgerException("subledger.date_invalid", "Document, posting, and due dates are required.");
        if (item.SourceVersion <= 0) throw new SubledgerException("subledger.source_version_invalid", "A positive source version is required.");
        var transactionCurrency = NormalizeCurrency(item.TransactionCurrency); var baseCurrency = NormalizeCurrency(item.BaseCurrency);
        if (!string.Equals(baseCurrency, entityBase, StringComparison.Ordinal)) throw new SubledgerException("subledger.base_currency_mismatch", "The open-item base currency must match the legal entity.");
        var amount = ParseAmount(item.OriginalTransactionAmount); var baseAmount = ParseAmount(item.OriginalBaseAmount);
        SubledgerRules.ValidatePositiveAmount(amount); if (baseAmount < 0m) throw new SubledgerException("subledger.amount_invalid", "The base carrying amount cannot be negative.");
        EnsurePrecision(amount); EnsurePrecision(baseAmount);
        if (string.IsNullOrWhiteSpace(item.ControlAccountReference) || item.ControlAccountReference.Trim().Length > 240 || string.IsNullOrWhiteSpace(item.ControlConfigurationReference) || item.ControlConfigurationReference.Trim().Length > 240)
            throw new SubledgerException("subledger.control_context_required", "A stable control-account mapping snapshot is required.");
        var expectedRole = direction == SubledgerDirection.Receivable ? "Receivable" : "Payable";
        if (!string.Equals(item.ControlAccountRole, expectedRole, StringComparison.Ordinal)) throw new SubledgerException("subledger.control_context_mismatch", "The control-account role does not match the financial direction.");
        if (item.HistoricFxSnapshot is { } fxSnapshot)
        {
            if (fxSnapshot.Length > 4000) throw new SubledgerException("subledger.fx_snapshot_invalid", "The historical currency snapshot is too large.");
            try { using var _ = JsonDocument.Parse(fxSnapshot); }
            catch (JsonException) { throw new SubledgerException("subledger.fx_snapshot_invalid", "The historical currency snapshot must contain valid JSON."); }
        }
        return item with
        {
            SourceReference = item.SourceReference.Trim(),
            SourceScheduleReference = Clean(item.SourceScheduleReference),
            SourceDisplayNumber = Clean(item.SourceDisplayNumber),
            TransactionCurrency = transactionCurrency,
            OriginalTransactionAmount = amount.ToString(CultureInfo.InvariantCulture),
            BaseCurrency = baseCurrency,
            OriginalBaseAmount = baseAmount.ToString(CultureInfo.InvariantCulture),
            ControlAccountReference = item.ControlAccountReference.Trim(),
            ControlConfigurationReference = item.ControlConfigurationReference.Trim()
        };
    }

    private async Task EnsureCurrencyEnabledAsync(TenantId tenant, LegalEntityId entity, string currency, DateOnly businessDate, CancellationToken ct)
    {
        if (await financeBase.ResolveCalculationContextAsync(tenant, entity, currency, FinancialCalculationPurpose.Accounting, businessDate, ct) is null)
            throw new SubledgerException("subledger.currency_not_allowed", "The transaction currency is not enabled for this legal entity.");
    }

    private async Task<string> EnsureEntityAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        var currency = await legalEntities.GetBaseCurrencyCodeAsync(tenant, entity, ct);
        return string.IsNullOrWhiteSpace(currency) ? throw new SubledgerException("subledger.entity_not_found", "The legal entity was not found.") : NormalizeCurrency(currency);
    }

    private async Task EnsurePartyAsync(TenantId tenant, LegalEntityId entity, Guid partyId, CancellationToken ct)
    {
        if (!await directory.IsActivePartyAsync(tenant, partyId, entity.Value, ct)) throw new SubledgerException("subledger.party_not_active", "The party is unavailable for this legal entity.");
    }

    private async Task ValidatePaymentTermAsync(TenantId tenant, Guid? termId, DateOnly asOf, CancellationToken ct)
    {
        if (termId is null) return;
        if (termId == Guid.Empty || await financeBase.ResolvePaymentTermAsync(tenant, termId.Value, asOf, ct) is null)
            throw new SubledgerException("subledger.payment_term_not_found", "The referenced payment term is unavailable.");
    }

    private static decimal? ParseOptionalAmount(string? amount) => string.IsNullOrWhiteSpace(amount) ? null : ParseAmount(amount);
    private static decimal ParseAmount(string text) => decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : throw new SubledgerException("subledger.amount_invalid", "Financial amounts must be invariant decimal values.");
    private static void EnsurePrecision(decimal value) { if (decimal.Round(value, 6, MidpointRounding.ToEven) != value) throw new SubledgerException("subledger.amount_precision_invalid", "Amounts may contain at most six decimal places."); }
    private static string NormalizeCurrency(string value)
    {
        try { return CurrencyCode.Normalize(value); }
        catch (FinancialValueException exception) { throw new SubledgerException("subledger.currency_invalid", exception.Message); }
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void ValidatePolicyCode(string? code)
    {
        if (code?.Trim().Length > 80) throw new SubledgerException("subledger.policy_code_invalid", "A financial policy code cannot exceed 80 characters.");
    }
    private static SubledgerDirection MapDirection(SubledgerDirectionContract direction) => direction switch
    {
        SubledgerDirectionContract.Receivable => SubledgerDirection.Receivable,
        SubledgerDirectionContract.Payable => SubledgerDirection.Payable,
        _ => throw new SubledgerException("subledger.direction_invalid", "The financial direction is invalid.")
    };
    private static void ValidateIdentity(TenantId tenant, LegalEntityId entity, Guid partyId)
    {
        if (tenant.Value == Guid.Empty || entity.Value == Guid.Empty || partyId == Guid.Empty) throw new SubledgerException("subledger.scope_required", "Tenant, legal entity, and party scope are required.");
    }
    private static void EnsureActor(Guid actorId, Guid correlationId)
    {
        if (actorId == Guid.Empty || correlationId == Guid.Empty) throw new SubledgerException("subledger.audit_context_required", "Actor and correlation context are required.");
    }
}
