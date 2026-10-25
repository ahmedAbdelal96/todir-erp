using System.Globalization;
using System.Text.Json;
using System.Diagnostics;
using Modules.Subledger.Entities;
using Modules.Subledger.Repositories;
using Shared.Contracts.Directory;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Subledger;
using Shared.Contracts.Ledger;
using Shared.Primitives.Financial;
using Shared.Primitives.Identifiers;
using Core.Services;
using Core.Authorization;

namespace Modules.Subledger.Services;

public sealed class SubledgerService(
    SubledgerRepository repository,
    IDirectoryCapabilities directory,
    IFinanceBaseCapabilities financeBase,
    ILegalEntityFinancialReadStore legalEntities,
    IAuthorizationSubjectStore authorizationSubjects,
    IPermissionAuthorizer permissionAuthorizer,
    TimeProvider clock,
    ILedgerControlBalanceRead? ledgerControlRead = null) : ISubledgerCapabilities
{
    public async Task<PartyFinancialProfileResult?> GetFinancialProfileAsync(TenantId tenantId, LegalEntityId entityId, Guid partyId, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, entityId, partyId);
        await EnsureEntityAsync(tenantId, entityId, cancellationToken);
        await EnsurePartyAsync(tenantId, entityId, partyId, cancellationToken);
        return await repository.GetProfileAsync(tenantId, entityId, partyId, cancellationToken);
    }

    public async Task<PartyFinancialProfileResult> UpdateFinancialProfileAsync(TenantId tenantId, Guid membershipId, PartyFinancialProfileCommand command, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.PartyId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.profile.manage", command.LegalEntityId, cancellationToken);
        if (command.ExpectedVersion < 0) throw new SubledgerException("subledger.profile_version_invalid", "The profile version is invalid.");
        await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        await EnsurePartyAsync(tenantId, command.LegalEntityId, command.PartyId, cancellationToken);
        var limit = ParseOptionalAmount(command.CreditLimitAmount);
        if (limit is < 0m) throw new SubledgerException("subledger.credit_limit_invalid", "Credit limit must be zero or greater.");
        var creditCurrency = command.CreditLimitCurrency is null ? null : NormalizeCurrency(command.CreditLimitCurrency);
        if ((limit is null) != (creditCurrency is null)) throw new SubledgerException("subledger.credit_limit_invalid", "Credit limit amount and currency must be provided together.");
        var currentProfile = await repository.GetProfileAsync(tenantId, command.LegalEntityId, command.PartyId, cancellationToken);
        var currentLimit = ParseOptionalAmount(currentProfile?.CreditLimitAmount);
        var currentLimitCurrency = currentProfile?.CreditLimitCurrency is null ? null : NormalizeCurrency(currentProfile.CreditLimitCurrency);
        if (currentLimit != limit || currentLimitCurrency != creditCurrency)
            await RequirePermissionAsync(tenantId, membershipId, "subledger.credit_limit.manage", command.LegalEntityId, cancellationToken);
        if (limit is { } configuredLimit) EnsurePrecision(configuredLimit);
        if (creditCurrency is not null) await EnsureCurrencyEnabledAsync(tenantId, command.LegalEntityId, creditCurrency, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), cancellationToken);
        ValidatePolicyCode(command.ReceivablePolicyCode);
        ValidatePolicyCode(command.PayablePolicyCode);
        var asOf = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        await ValidatePaymentTermAsync(tenantId, command.DefaultReceivablePaymentTermId, asOf, cancellationToken);
        await ValidatePaymentTermAsync(tenantId, command.DefaultPayablePaymentTermId, asOf, cancellationToken);
        if (command.ActorId != Guid.Empty) EnsureActor(command.ActorId, command.CorrelationId);
        var creditLimitChanged = currentLimit != limit || currentLimitCurrency != creditCurrency;
        return await repository.SaveProfileAsync(tenantId, command, limit, creditCurrency, cancellationToken, creditLimitChanged);
    }

    public Task<IReadOnlyList<Guid>> CreateReceivableOpenItemsAsync(TenantId tenantId, CreateOpenItemsCommand command, CancellationToken cancellationToken = default) =>
        CreateSourceItemsAsync(tenantId, command, SubledgerDirection.Receivable, OpenItemPolarity.Debit, "Invoice", cancellationToken);

    public Task<IReadOnlyList<Guid>> CreatePayableOpenItemsAsync(TenantId tenantId, CreateOpenItemsCommand command, CancellationToken cancellationToken = default) =>
        CreateSourceItemsAsync(tenantId, command, SubledgerDirection.Payable, OpenItemPolarity.Credit, "Bill", cancellationToken);

    public Task<IReadOnlyList<Guid>> CreateScheduledReceivableOpenItemsAsync(TenantId tenantId, CreateScheduledOpenItemsCommand command, CancellationToken cancellationToken = default) =>
        CreateScheduledOpenItemsAsync(tenantId, command, SubledgerDirection.Receivable, OpenItemPolarity.Debit, "Invoice", cancellationToken);

    public Task<IReadOnlyList<Guid>> CreateScheduledPayableOpenItemsAsync(TenantId tenantId, CreateScheduledOpenItemsCommand command, CancellationToken cancellationToken = default) =>
        CreateScheduledOpenItemsAsync(tenantId, command, SubledgerDirection.Payable, OpenItemPolarity.Credit, "Bill", cancellationToken);

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
        var ids = await repository.CreateOpenItemsAsync(tenantId, command.LegalEntityId, command.PartyId, direction, SubledgerRules.PaymentPolarity(direction), "Payment", [normalized], cancellationToken, command.CorrelationId, command.ActorId);
        return ids[0];
    }

    public Task<Guid> RegisterAdvanceAsync(TenantId tenantId, Guid membershipId, RegisterPartyCreditCommand command, CancellationToken cancellationToken = default) =>
        RegisterTrustedCreditAsync(tenantId, membershipId, command, "Advance", cancellationToken);

    public Task<Guid> RegisterCreditNoteAsync(TenantId tenantId, Guid membershipId, RegisterPartyCreditCommand command, CancellationToken cancellationToken = default) =>
        RegisterTrustedCreditAsync(tenantId, membershipId, command, "CreditNote", cancellationToken);

    private async Task<Guid> RegisterTrustedCreditAsync(TenantId tenantId, Guid membershipId, RegisterPartyCreditCommand command, string sourceType, CancellationToken cancellationToken)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.PartyId);
        await RequirePermissionAsync(tenantId, membershipId, sourceType == "Advance" ? "subledger.advance.create" : "subledger.credit-note.create", command.LegalEntityId, cancellationToken);
        EnsureActor(command.ActorId, command.CorrelationId);
        var direction = MapDirection(command.Direction);
        var credit = command.Credit with { DueDate = command.Credit.PostingDate };
        var baseCurrency = await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        await EnsurePartyAsync(tenantId, command.LegalEntityId, command.PartyId, cancellationToken);
        await EnsureCurrencyEnabledAsync(tenantId, command.LegalEntityId, NormalizeCurrency(credit.TransactionCurrency), credit.PostingDate, cancellationToken);
        var normalized = ValidateSnapshot(credit, direction, baseCurrency);
        var ids = await repository.CreateOpenItemsAsync(tenantId, command.LegalEntityId, command.PartyId, direction, SubledgerRules.PaymentPolarity(direction), sourceType, [normalized], cancellationToken, command.CorrelationId, command.ActorId);
        return ids[0];
    }

    public async Task<SubledgerApplicationResult> ApplyAsync(TenantId tenantId, Guid membershipId, ApplyOpenItemsCommand command, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = SubledgerObservability.ActivitySource.StartActivity("application.confirm");
        activity?.SetTag("subledger.application.lines", command.Lines?.Count ?? 0);
        try
        {
            var resolved = await ResolveApplicationLinesAsync(tenantId, membershipId, command, cancellationToken);
            var result = await repository.ApplyAsync(tenantId, command, resolved.Lines, resolved.MoneyScale, resolved.RoundingMode, cancellationToken);
            Activity.Current?.AddEvent(new ActivityEvent("subledger.db_commit"));
            SubledgerObservability.ApplicationLines.Add(resolved.Lines.Count);
            if (resolved.Lines.Any(line => line.DebitFxSnapshot != line.CreditFxSnapshot))
                SubledgerObservability.FxSettlements.Add(resolved.Lines.Count);
            return result;
        }
        catch (SubledgerException exception) when (exception.Code is "subledger.item_changed" or "subledger.application_conflict" or "subledger.application_over_target" or "subledger.application_over_source")
        {
            SubledgerObservability.ApplicationConflicts.Add(1, new KeyValuePair<string, object?>("reason", exception.Code));
            throw;
        }
        finally { SubledgerObservability.ApplicationDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds); }
    }

    public async Task<ApplicationPreviewResult> PreviewAsync(TenantId tenantId, Guid membershipId, ApplyOpenItemsCommand command, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveApplicationLinesAsync(tenantId, membershipId, command, cancellationToken);
        return await repository.PreviewApplicationAsync(tenantId, command, resolved.Lines, resolved.MoneyScale, resolved.RoundingMode, cancellationToken);
    }

    private async Task<ResolvedApplicationBatch> ResolveApplicationLinesAsync(TenantId tenantId, Guid membershipId, ApplyOpenItemsCommand command, CancellationToken cancellationToken)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.PartyId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.application.create", command.LegalEntityId, cancellationToken);
        EnsureActor(command.ActorId, command.CorrelationId);
        if (command.Lines is null || command.Lines.Count is < 1 or > 200) throw new SubledgerException("subledger.application_lines_invalid", "An application must contain between one and 200 lines.");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Trim().Length > 200) throw new SubledgerException("subledger.idempotency_key_invalid", "A valid idempotency key is required.");
        var debitAmounts = command.Lines.Select(line => ParseAmount(line.Amount)).ToArray();
        var creditAmounts = command.Lines.Select((line, index) => string.IsNullOrWhiteSpace(line.CreditAmount) ? debitAmounts[index] : ParseAmount(line.CreditAmount)).ToArray();
        for (var index = 0; index < debitAmounts.Length; index++)
        {
            SubledgerRules.ValidatePositiveAmount(debitAmounts[index]); SubledgerRules.ValidatePositiveAmount(creditAmounts[index]);
            EnsurePrecision(debitAmounts[index]); EnsurePrecision(creditAmounts[index]);
        }
        var baseCurrency = await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        var financialContext = await ResolveFinancialContextAsync(tenantId, command.LegalEntityId, baseCurrency, FinancialCalculationPurpose.Accounting, command.SettlementDate, cancellationToken)
            ?? throw new SubledgerException("subledger.financial_policy_unavailable", "The legal entity financial rounding policy is unavailable.");
        var rule = financialContext.RoundingPolicy.ContextRules.SingleOrDefault(item => item.Context == FinancialRoundingContext.PaymentAllocation)
            ?? throw new SubledgerException("subledger.financial_policy_unavailable", "The financial policy has no application rounding rule.");
        if (!Enum.TryParse<FinancialRoundingModeValue>(rule.Mode.ToString(), out var mode)) throw new SubledgerException("subledger.financial_policy_unavailable", "The application rounding rule is not supported.");
        var baseRule = financialContext.RoundingPolicy.ContextRules.SingleOrDefault(item => item.Context == FinancialRoundingContext.FxBaseAmount)
            ?? throw new SubledgerException("subledger.financial_policy_unavailable", "The financial policy has no base-currency settlement rounding rule.");
        if (!Enum.TryParse<FinancialRoundingModeValue>(baseRule.Mode.ToString(), out var baseMode)) throw new SubledgerException("subledger.financial_policy_unavailable", "The base-currency settlement rounding rule is unsupported.");
        var itemIds = command.Lines.SelectMany(line => new[] { line.DebitItemId, line.CreditItemId }).Distinct().ToArray();
        var itemCurrencies = await repository.GetApplicationCurrenciesAsync(tenantId, command.LegalEntityId, itemIds, cancellationToken);
        if (itemCurrencies.Count != itemIds.Length) throw new SubledgerException("subledger.open_item_not_found", "One or more open items were not found in this legal entity.");
        Activity.Current?.AddEvent(new ActivityEvent("subledger.source_and_target_items_loaded"));
        if (command.Lines.Any(line => itemCurrencies[line.DebitItemId].TransactionCurrency != itemCurrencies[line.CreditItemId].TransactionCurrency))
            await RequirePermissionAsync(tenantId, membershipId, "subledger.application.cross_currency", command.LegalEntityId, cancellationToken);
        var currencyCodes = itemCurrencies.Values.Select(item => item.TransactionCurrency).Distinct(StringComparer.Ordinal).ToArray();
        var contexts = new Dictionary<string, FinancialCalculationContextSnapshotContract>(StringComparer.Ordinal);
        foreach (var currency in currencyCodes)
            contexts[currency] = await ResolveFinancialContextAsync(tenantId, command.LegalEntityId, currency, FinancialCalculationPurpose.Accounting, command.SettlementDate, cancellationToken)
                ?? throw new SubledgerException("subledger.currency_not_allowed", "A settlement currency is not enabled for this legal entity.");
        Activity.Current?.AddEvent(new ActivityEvent("subledger.fx_contexts_resolved"));
        decimal Rate(string currency)
        {
            var context = contexts[currency];
            if (string.Equals(currency, baseCurrency, StringComparison.Ordinal)) return 1m;
            if (context.ExchangeRate is null || !decimal.TryParse(context.ExchangeRate.Rate, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var resolvedRate) || resolvedRate <= 0m)
                throw new SubledgerException("subledger.fx_rate_missing", "An approved settlement exchange rate is unavailable.");
            return resolvedRate;
        }
        var resolvedLines = new List<ResolvedApplicationLineContract>(command.Lines.Count);
        if (currencyCodes.Any(currency => contexts[currency].ExchangeRate?.ManualOverride == true))
            await RequirePermissionAsync(tenantId, membershipId, "subledger.fx.manual-rate", command.LegalEntityId, cancellationToken);
        for (var index = 0; index < command.Lines.Count; index++)
        {
            var line = command.Lines[index];
            var debitCurrency = itemCurrencies[line.DebitItemId].TransactionCurrency;
            var creditCurrency = itemCurrencies[line.CreditItemId].TransactionCurrency;
            if (debitCurrency == creditCurrency && debitAmounts[index] != creditAmounts[index])
                throw new SubledgerException("subledger.same_currency_amount_mismatch", "Same-currency applications must use equal transaction amounts.");
            var debitBase = FinancialRounding.Round(debitAmounts[index] * Rate(debitCurrency), financialContext.RoundingPolicy.MoneyScale, baseMode);
            var creditBase = FinancialRounding.Round(creditAmounts[index] * Rate(creditCurrency), financialContext.RoundingPolicy.MoneyScale, baseMode);
            if (debitBase != creditBase) throw new SubledgerException("subledger.fx_equivalent_mismatch", "The applied amounts do not reconcile to the same settlement value in the legal entity base currency.");
            resolvedLines.Add(new(line.DebitItemId, line.CreditItemId, debitAmounts[index].ToString(CultureInfo.InvariantCulture), creditAmounts[index].ToString(CultureInfo.InvariantCulture),
                JsonSerializer.Serialize(contexts[debitCurrency]), JsonSerializer.Serialize(contexts[creditCurrency]), debitBase.ToString(CultureInfo.InvariantCulture)));
        }
        Activity.Current?.AddEvent(new ActivityEvent("subledger.amounts_validated"));
        return new(resolvedLines, financialContext.RoundingPolicy.MoneyScale, mode);
    }

    public async Task<SubledgerApplicationResult> ReverseAsync(TenantId tenantId, Guid membershipId, ReverseApplicationCommand command, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.ApplicationId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.application.reverse", command.LegalEntityId, cancellationToken);
        EnsureActor(command.ActorId, command.CorrelationId);
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Trim().Length > 200) throw new SubledgerException("subledger.idempotency_key_invalid", "A valid idempotency key is required.");
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > 1000) throw new SubledgerException("subledger.reversal_reason_required", "A reason is required to reverse an application.");
        _ = await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        return await repository.ReverseAsync(tenantId, command, cancellationToken);
    }

    public async Task<WriteOffPolicyResult> UpdateWriteOffPolicyAsync(TenantId tenantId, Guid membershipId, WriteOffPolicyCommand command, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, Guid.NewGuid());
        await RequirePermissionAsync(tenantId, membershipId, "subledger.writeoff.policy.manage", command.LegalEntityId, cancellationToken);
        EnsureActor(command.ActorId, command.CorrelationId);
        _ = MapDirection(command.Direction);
        if (command.ExpectedVersion < 0) throw new SubledgerException("subledger.writeoff_policy_version_invalid", "The write-off policy version is invalid.");
        var currency = NormalizeCurrency(command.CurrencyCode);
        var maximum = ParseAmount(command.MaximumAmount);
        var threshold = ParseAmount(command.ApprovalThreshold);
        EnsurePrecision(maximum); EnsurePrecision(threshold);
        if (maximum <= 0m || threshold < 0m || threshold > maximum)
            throw new SubledgerException("subledger.writeoff_policy_invalid", "The maximum must be positive and the approval threshold must be between zero and the maximum.");
        await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        await EnsureCurrencyEnabledAsync(tenantId, command.LegalEntityId, currency, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), cancellationToken);
        return await repository.SaveWriteOffPolicyAsync(tenantId, command, maximum, threshold, currency, cancellationToken);
    }

    public async Task<Guid> CreateWriteOffRequestAsync(TenantId tenantId, Guid membershipId, CreateWriteOffCommand command, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.OpenItemId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.writeoff.create", command.LegalEntityId, cancellationToken);
        EnsureActor(command.ActorId, command.CorrelationId);
        if (command.EffectiveDate == DateOnly.MinValue) throw new SubledgerException("subledger.effective_date_required", "An effective date is required.");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Trim().Length > 200) throw new SubledgerException("subledger.idempotency_key_invalid", "A valid idempotency key is required.");
        if (string.IsNullOrWhiteSpace(command.ReasonCode) || command.ReasonCode.Trim().Length > 80 || string.IsNullOrWhiteSpace(command.ReasonText) || command.ReasonText.Trim().Length > 1000)
            throw new SubledgerException("subledger.writeoff_reason_required", "A reason code and explanation are required.");
        var amount = ParseAmount(command.Amount); SubledgerRules.ValidatePositiveAmount(amount); EnsurePrecision(amount);
        _ = await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        var itemContext = await repository.GetOpenItemWriteOffContextAsync(tenantId, command.LegalEntityId, command.OpenItemId, cancellationToken)
            ?? throw new SubledgerException("subledger.open_item_not_found", "The open item was not found in this legal entity.");
        await EnsurePartyAsync(tenantId, command.LegalEntityId, itemContext.PartyId, cancellationToken);
        var currency = NormalizeCurrency(itemContext.Currency);
        var financial = await ResolveFinancialContextAsync(tenantId, command.LegalEntityId, currency, FinancialCalculationPurpose.Accounting, command.EffectiveDate, cancellationToken)
            ?? throw new SubledgerException("subledger.financial_policy_unavailable", "The legal entity financial rounding policy is unavailable.");
        var rule = financial.RoundingPolicy.ContextRules.SingleOrDefault(item => item.Context == FinancialRoundingContext.FxBaseAmount)
            ?? throw new SubledgerException("subledger.financial_policy_unavailable", "The financial policy has no carrying-value rounding rule.");
        if (!Enum.TryParse<FinancialRoundingModeValue>(rule.Mode.ToString(), out var mode)) throw new SubledgerException("subledger.financial_policy_unavailable", "The carrying-value rounding rule is unsupported.");
        var scale = financial.RoundingPolicy.MoneyScale;
        var requestId = await repository.CreateWriteOffRequestAsync(tenantId, command, amount, currency, checked((short)scale), mode.ToString(), cancellationToken);
        return requestId;
    }

    public async Task<WriteOffResult> ApproveWriteOffAsync(TenantId tenantId, Guid membershipId, ApproveWriteOffCommand command, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.RequestId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.writeoff.approve", command.LegalEntityId, cancellationToken);
        EnsureActor(command.ActorId, command.CorrelationId);
        _ = await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        var result = await repository.ApproveWriteOffAsync(tenantId, command, cancellationToken);
        if (!result.AlreadyCompleted) SubledgerObservability.WriteOffs.Add(1);
        return result;
    }

    public async Task<ReconciliationResult> ReconcileControlBalancesAsync(TenantId tenantId, Guid membershipId, ReconciliationQuery query, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, query.LegalEntityId, actorId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.reconciliation.run", query.LegalEntityId, cancellationToken);
        EnsureActor(actorId, correlationId);
        if (query.AsOfDate == DateOnly.MinValue) throw new SubledgerException("subledger.as_of_date_required", "An as-of date is required.");
        var baseCurrency = await EnsureEntityAsync(tenantId, query.LegalEntityId, cancellationToken);
        if (ledgerControlRead is null) throw new SubledgerException("subledger.ledger_capability_unavailable", "The Ledger control-balance read capability is unavailable.");
        var sources = await repository.GetReconciliationSourceContextsAsync(tenantId, query.LegalEntityId, query.AsOfDate, cancellationToken);
        var details = new List<ReconciliationDetail>(sources.Count);
        foreach (var source in sources)
        {
            Guid? accountId = null;
            var ledgerBalance = 0m;
            var configurationId = Guid.Empty;
            var resolved = source.ControlRole == source.Direction.ToString()
                && source.BaseCurrencyCode == baseCurrency
                && source.ControlConfigurationReference.StartsWith("configuration:", StringComparison.Ordinal)
                && Guid.TryParse(source.ControlConfigurationReference["configuration:".Length..], out configurationId);
            if (resolved)
            {
                try
                {
                    var snapshot = await ledgerControlRead.GetControlBalanceAsync(tenantId, query.LegalEntityId, source.ControlRole, configurationId, query.AsOfDate, cancellationToken);
                    if (snapshot.BaseCurrencyCode == baseCurrency)
                    {
                        accountId = snapshot.AccountId;
                        ledgerBalance = snapshot.SignedBaseBalance;
                    }
                    else resolved = false;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { resolved = false; }
            }
            var variance = source.SubledgerBaseBalance - ledgerBalance;
            details.Add(new(Guid.CreateVersion7(), source.Direction, source.ControlRole, source.ControlAccountReference, source.ControlConfigurationReference,
                accountId, source.SubledgerBaseBalance.ToString(CultureInfo.InvariantCulture), ledgerBalance.ToString(CultureInfo.InvariantCulture),
                variance.ToString(CultureInfo.InvariantCulture), resolved));
        }
        var result = await repository.SaveReconciliationRunAsync(tenantId, query.LegalEntityId, query.AsOfDate, baseCurrency, details, actorId, correlationId, clock.GetUtcNow(), cancellationToken);
        SubledgerObservability.ReconciliationDifference.Record(decimal.ToDouble(ParseAmount(result.Variance)));
        return result;
    }

    public async Task<IReadOnlyList<ReconciliationSourceContext>> GetReconciliationControlAccountsAsync(TenantId tenantId, Guid membershipId, ReconciliationQuery query, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, query.LegalEntityId, query.LegalEntityId.Value);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.reconciliation.read", query.LegalEntityId, cancellationToken);
        if (query.AsOfDate == DateOnly.MinValue) throw new SubledgerException("subledger.as_of_date_required", "An as-of date is required.");
        await EnsureEntityAsync(tenantId, query.LegalEntityId, cancellationToken);
        return await repository.GetReconciliationSourceContextsAsync(tenantId, query.LegalEntityId, query.AsOfDate, cancellationToken);
    }

    public async Task<ReconciliationResult?> GetReconciliationRunAsync(TenantId tenantId, Guid membershipId, LegalEntityId legalEntityId, Guid runId, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, legalEntityId, runId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.reconciliation.read", legalEntityId, cancellationToken);
        await EnsureEntityAsync(tenantId, legalEntityId, cancellationToken);
        return await repository.GetReconciliationRunAsync(tenantId, legalEntityId, runId, cancellationToken);
    }

    public async Task<Guid> CreateOpeningSubledgerBatchAsync(TenantId tenantId, Guid membershipId, CreateOpeningSubledgerBatchCommand command, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.ActorId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.opening.manage", command.LegalEntityId, cancellationToken);
        EnsureActor(command.ActorId, command.CorrelationId);
        if (string.IsNullOrWhiteSpace(command.BatchIdentity) || command.BatchIdentity.Trim().Length > 120
            || string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Trim().Length > 200
            || string.IsNullOrWhiteSpace(command.ControlConfigurationReference) || command.ControlConfigurationReference.Trim().Length > 240
            || command.GoLiveDate == DateOnly.MinValue)
            throw new SubledgerException("subledger.opening_batch_invalid", "Opening batch identity, effective date, accounting context, and idempotency key are required.");
        var baseCurrency = await EnsureEntityAsync(tenantId, command.LegalEntityId, cancellationToken);
        if (NormalizeCurrency(command.BaseCurrencyCode) != baseCurrency) throw new SubledgerException("subledger.base_currency_mismatch", "Opening balances must use the legal entity base currency.");
        if (!TryConfigurationReference(command.ControlConfigurationReference, out _)) throw new SubledgerException("subledger.opening_configuration_invalid", "The opening batch must reference a valid Ledger accounting configuration.");
        return await repository.CreateOpeningSubledgerBatchAsync(tenantId, command, baseCurrency, cancellationToken);
    }

    public async Task AddOpeningSubledgerLinesAsync(TenantId tenantId, Guid membershipId, LegalEntityId legalEntityId, Guid batchId, IReadOnlyList<OpeningSubledgerLineCommand> lines, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, legalEntityId, batchId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.opening.manage", legalEntityId, cancellationToken);
        EnsureActor(actorId, correlationId);
        if (lines is null || lines.Count is < 1 or > 500) throw new SubledgerException("subledger.opening_line_count_invalid", "Add between 1 and 500 opening rows at a time.");
        var batch = await repository.GetOpeningSubledgerBatchAsync(tenantId, legalEntityId, batchId, cancellationToken)
            ?? throw new SubledgerException("subledger.opening_batch_not_found", "The opening batch was not found in this legal entity.");
        if (batch.Status != "Draft") throw new SubledgerException("subledger.opening_batch_immutable", "Finalized opening batches cannot be changed.");
        if (lines.Select(line => line.RowKey.Trim()).Distinct(StringComparer.Ordinal).Count() != lines.Count)
            throw new SubledgerException("subledger.opening_row_key_duplicate", "Opening row keys must be unique within the batch.");
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.RowKey) || line.RowKey.Trim().Length > 120 || line.PartyId == Guid.Empty
                || string.IsNullOrWhiteSpace(line.SourceReference) || line.SourceReference.Trim().Length > 240
                || line.DocumentDate == DateOnly.MinValue || line.DueDate == DateOnly.MinValue || line.DocumentDate > batch.GoLiveDate)
                throw new SubledgerException("subledger.opening_line_invalid", "Opening rows require a stable key, party, source reference, and valid dates.");
            _ = MapDirection(line.Direction);
            if (!Enum.IsDefined(line.Polarity) || !Enum.IsDefined(line.Direction) || line.ControlAccountRole != line.Direction.ToString()
                || line.Polarity is not (SubledgerPolarityContract.Debit or SubledgerPolarityContract.Credit))
                throw new SubledgerException("subledger.opening_direction_invalid", "The opening row control role or polarity is invalid.");
            await EnsurePartyAsync(tenantId, legalEntityId, line.PartyId, cancellationToken);
            var currency = NormalizeCurrency(line.TransactionCurrency);
            if (NormalizeCurrency(line.BaseCurrency) != batch.BaseCurrencyCode || line.ControlConfigurationReference != batch.ControlConfigurationReference)
                throw new SubledgerException("subledger.opening_context_mismatch", "Every opening row must use the batch base currency and Ledger configuration.");
            var amount = ParseAmount(line.TransactionAmount); var baseAmount = ParseAmount(line.BaseAmount);
            SubledgerRules.ValidatePositiveAmount(amount); EnsurePrecision(amount); if (baseAmount < 0m) throw new SubledgerException("subledger.amount_invalid", "The carrying amount cannot be negative."); EnsurePrecision(baseAmount);
            await EnsureCurrencyEnabledAsync(tenantId, legalEntityId, currency, batch.GoLiveDate, cancellationToken);
            if (currency == batch.BaseCurrencyCode && amount != baseAmount)
                throw new SubledgerException("subledger.opening_base_amount_mismatch", "Base-currency opening items must have equal transaction and carrying amounts.");
            if (currency != batch.BaseCurrencyCode && string.IsNullOrWhiteSpace(line.HistoricFxSnapshot))
                throw new SubledgerException("subledger.opening_fx_snapshot_required", "Foreign-currency opening items require the historic conversion evidence.");
            if (!string.IsNullOrWhiteSpace(line.HistoricFxSnapshot))
            {
                try { using var _ = JsonDocument.Parse(line.HistoricFxSnapshot); }
                catch (JsonException exception) { throw new SubledgerException("subledger.fx_snapshot_invalid", "The opening FX evidence must be valid JSON.", exception); }
            }
        }
        await repository.AddOpeningSubledgerLinesAsync(tenantId, legalEntityId, batchId, lines, actorId, correlationId, cancellationToken);
    }

    public async Task<OpeningSubledgerBatchResult> FinalizeOpeningSubledgerBatchAsync(TenantId tenantId, Guid membershipId, LegalEntityId legalEntityId, Guid batchId, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, legalEntityId, batchId); EnsureActor(actorId, correlationId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.opening.finalize", legalEntityId, cancellationToken);
        var batch = await repository.GetOpeningSubledgerBatchAsync(tenantId, legalEntityId, batchId, cancellationToken)
            ?? throw new SubledgerException("subledger.opening_batch_not_found", "The opening batch was not found in this legal entity.");
        if (batch.Status == "Finalized") return await repository.FinalizeOpeningSubledgerBatchAsync(tenantId, legalEntityId, batchId, actorId, correlationId, cancellationToken);
        var lines = await repository.GetOpeningSubledgerLinesAsync(tenantId, legalEntityId, batchId, cancellationToken);
        if (lines.Count == 0) throw new SubledgerException("subledger.opening_batch_empty", "An opening batch must contain at least one outstanding item or credit.");
        var validation = await EvaluateOpeningBatchAsync(tenantId, legalEntityId, batch, lines, cancellationToken);
        if (!validation.IsBalanced) throw new SubledgerException("subledger.opening_reconciliation_mismatch", "The opening totals do not match their Ledger control balances.");
        return await repository.FinalizeOpeningSubledgerBatchAsync(tenantId, legalEntityId, batchId, actorId, correlationId, cancellationToken);
    }

    public async Task<OpeningSubledgerValidationResult> ValidateOpeningSubledgerBatchAsync(TenantId tenantId, Guid membershipId, LegalEntityId legalEntityId, Guid batchId, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, legalEntityId, batchId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.opening.manage", legalEntityId, cancellationToken);
        var batch = await repository.GetOpeningSubledgerBatchAsync(tenantId, legalEntityId, batchId, cancellationToken)
            ?? throw new SubledgerException("subledger.opening_batch_not_found", "The opening batch was not found in this legal entity.");
        var lines = await repository.GetOpeningSubledgerLinesAsync(tenantId, legalEntityId, batchId, cancellationToken);
        return await EvaluateOpeningBatchAsync(tenantId, legalEntityId, batch, lines, cancellationToken);
    }

    private async Task<OpeningSubledgerValidationResult> EvaluateOpeningBatchAsync(TenantId tenantId, LegalEntityId legalEntityId, OpeningSubledgerBatchContext batch,
        IReadOnlyList<OpeningSubledgerStoredLine> lines, CancellationToken cancellationToken)
    {
        if (ledgerControlRead is null || !TryConfigurationReference(batch.ControlConfigurationReference, out var configurationId))
            throw new SubledgerException("subledger.ledger_capability_unavailable", "The Ledger control-balance capability or configuration is unavailable.");
        var directions = new List<OpeningSubledgerValidationDirection>(2);
        foreach (var direction in Enum.GetValues<SubledgerDirectionContract>())
        {
            var subledgerTotal = lines.Select(row => row.Command).Where(line => line.Direction == direction).Sum(line =>
            {
                var amount = ParseAmount(line.BaseAmount);
                var normalPolarity = direction == SubledgerDirectionContract.Receivable ? SubledgerPolarityContract.Debit : SubledgerPolarityContract.Credit;
                return line.Polarity == normalPolarity ? amount : -amount;
            });
            var ledgerBalance = await ledgerControlRead.GetControlBalanceAsync(tenantId, legalEntityId, direction.ToString(), configurationId, batch.GoLiveDate, cancellationToken);
            var contextValid = ledgerBalance.BaseCurrencyCode == batch.BaseCurrencyCode;
            var variance = contextValid ? subledgerTotal - ledgerBalance.SignedBaseBalance : subledgerTotal;
            directions.Add(new(direction, subledgerTotal.ToString(CultureInfo.InvariantCulture),
                contextValid ? ledgerBalance.SignedBaseBalance.ToString(CultureInfo.InvariantCulture) : "0",
                variance.ToString(CultureInfo.InvariantCulture), contextValid && variance == 0m));
        }
        return new(batch.Id, batch.Status, lines.Count, lines.Count > 0 && directions.All(line => line.IsBalanced), directions);
    }

    public async Task<OpenItemPage> ListOpenItemsAsync(TenantId tenantId, OpenItemListQuery query, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        ValidateIdentity(tenantId, query.LegalEntityId, query.PartyId);
        if (query.Limit is < 1 or > 200) throw new SubledgerException("subledger.page_size_invalid", "Page size must be between 1 and 200.");
        if (query.Cursor is not null && !Guid.TryParse(query.Cursor, out _)) throw new SubledgerException("subledger.cursor_invalid", "The open-item cursor is invalid.");
        if (query.Direction is not null && !Enum.IsDefined(query.Direction.Value)) throw new SubledgerException("subledger.direction_invalid", "The financial direction is invalid.");
        if (query.DueFrom == DateOnly.MinValue || query.DueTo == DateOnly.MinValue || query.DueFrom is { } dueFrom && query.DueTo is { } dueTo && dueTo < dueFrom)
            throw new SubledgerException("subledger.due_range_invalid", "The due-date range is invalid.");
        if (query.OverdueOnly && (!query.AsOfDate.HasValue || query.AsOfDate.Value == DateOnly.MinValue))
            throw new SubledgerException("subledger.as_of_date_required", "Overdue search requires an as-of date.");
        if (query.Status is not null && query.Status is not ("Open" or "PartiallySettled" or "Settled"))
            throw new SubledgerException("subledger.status_invalid", "The open-item status filter is invalid.");
        if (query.Search?.Trim().Length > 120) throw new SubledgerException("subledger.search_too_long", "Search text cannot exceed 120 characters.");
        if (query.CurrencyCode is not null) _ = NormalizeCurrency(query.CurrencyCode);
        await EnsureEntityAsync(tenantId, query.LegalEntityId, cancellationToken);
        await EnsurePartyAsync(tenantId, query.LegalEntityId, query.PartyId, cancellationToken);
        query = query with { Search = Clean(query.Search), CurrencyCode = query.CurrencyCode is null ? null : NormalizeCurrency(query.CurrencyCode) };
        try { return await repository.ListAsync(tenantId, query, cancellationToken); }
        finally { SubledgerObservability.OpenItemsQueryDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds); }
    }

    public async Task<PartyOpenItemResult?> GetOpenItemAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid openItemId, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, legalEntityId, openItemId);
        _ = await EnsureEntityAsync(tenantId, legalEntityId, cancellationToken);
        var item = await repository.GetOpenItemAsync(tenantId, legalEntityId, openItemId, cancellationToken);
        if (item is not null) await EnsurePartyAsync(tenantId, legalEntityId, item.PartyId, cancellationToken);
        return item;
    }

    public async Task<AutoApplySuggestionResult> SuggestApplicationsAsync(TenantId tenantId, Guid membershipId, AutoApplySuggestionQuery query, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
        ValidateIdentity(tenantId, query.LegalEntityId, query.SourceOpenItemId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.application.create", query.LegalEntityId, cancellationToken);
        if (string.IsNullOrWhiteSpace(query.Policy)) throw new SubledgerException("subledger.match_policy_invalid", "A matching policy is required.");
        var policy = query.Policy.Trim();
        if (policy is not ("Manual" or "AutoOldestDue" or "AutoOldestDocument" or "ReferenceMatchFirst"))
            throw new SubledgerException("subledger.match_policy_invalid", "The application matching policy is not supported.");
        if (query.MaximumLines is < 1 or > 50) throw new SubledgerException("subledger.page_size_invalid", "A suggestion request may include between one and 50 lines.");

        var manual = policy == "Manual";
        var context = await repository.GetAutoApplyContextAsync(tenantId, query.LegalEntityId, query.SourceOpenItemId, query.Reference,
            manual ? 0 : 500, policy == "ReferenceMatchFirst", policy == "AutoOldestDocument", cancellationToken);
        _ = await EnsureEntityAsync(tenantId, query.LegalEntityId, cancellationToken);
        await EnsurePartyAsync(tenantId, query.LegalEntityId, context.Source.PartyId, cancellationToken);
        if (policy == "Manual")
            return new(context.Source.Id, context.Source.OpenTransactionAmount, context.Source.TransactionCurrency, [], context.Source.OpenTransactionAmount, true);

        var reference = NormalizeMatchReference(query.Reference ?? context.Source.SourceDisplayNumber ?? context.Source.SourceReference);
        var sourceAvailable = decimal.Parse(context.Source.OpenTransactionAmount, CultureInfo.InvariantCulture);
        var ordered = context.Candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                ReferenceMatch = reference.Length > 0 && (
                    NormalizeMatchReference(candidate.Item.SourceDisplayNumber ?? "") == reference
                    || NormalizeMatchReference(candidate.Item.SourceReference) == reference),
                AmountMatch = decimal.Parse(candidate.Item.OpenTransactionAmount, CultureInfo.InvariantCulture) == sourceAvailable
            })
            .OrderByDescending(item => policy == "ReferenceMatchFirst" && item.ReferenceMatch)
            .ThenByDescending(item => policy == "ReferenceMatchFirst" && item.ReferenceMatch && item.AmountMatch)
            .ThenBy(item => policy == "AutoOldestDocument" ? item.Candidate.Item.DocumentDate : item.Candidate.Item.DueDate)
            .ThenBy(item => item.Candidate.Item.DocumentDate)
            .ThenBy(item => item.Candidate.Item.Id)
            .ToArray();

        var suggestions = new List<AutoApplySuggestionLine>();
        var remainingSource = sourceAvailable;
        foreach (var candidate in ordered)
        {
            if (suggestions.Count >= query.MaximumLines || remainingSource <= 0m) break;
            var target = candidate.Candidate.Item;
            var targetRemaining = decimal.Parse(target.OpenTransactionAmount, CultureInfo.InvariantCulture);
            var applied = Math.Min(remainingSource, targetRemaining);
            var reason = candidate.ReferenceMatch
                ? candidate.AmountMatch ? "ExactAmountAndReference" : "ExactReference"
                : policy == "AutoOldestDocument" ? "OldestDocumentFallback" : "OldestDueFallback";
            var confidence = candidate.ReferenceMatch ? "High" : "Low";
            var debit = context.Source.Polarity == SubledgerPolarityContract.Debit ? context.Source : target;
            var credit = context.Source.Polarity == SubledgerPolarityContract.Credit ? context.Source : target;
            suggestions.Add(new(debit.Id, credit.Id, applied.ToString(CultureInfo.InvariantCulture), applied.ToString(CultureInfo.InvariantCulture),
                debit.RowVersion, credit.RowVersion, reason, confidence, context.Source.TransactionCurrency,
                (decimal.Parse(debit.OpenTransactionAmount, CultureInfo.InvariantCulture) - applied).ToString(CultureInfo.InvariantCulture),
                (decimal.Parse(credit.OpenTransactionAmount, CultureInfo.InvariantCulture) - applied).ToString(CultureInfo.InvariantCulture)));
            remainingSource -= applied;
        }

        return new(context.Source.Id, sourceAvailable.ToString(CultureInfo.InvariantCulture), context.Source.TransactionCurrency,
            suggestions, remainingSource.ToString(CultureInfo.InvariantCulture), true);
        }
        finally { SubledgerObservability.AutoMatchDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds); }
    }

    public async Task<PartyBalanceResult> GetPartyBalanceAsync(TenantId tenantId, PartyBalanceQuery query, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
        ValidateIdentity(tenantId, query.LegalEntityId, query.PartyId);
        _ = MapDirection(query.Direction);
        if (query.AsOfDate == DateOnly.MinValue) throw new SubledgerException("subledger.as_of_date_required", "An as-of date is required.");
        var baseCurrency = await EnsureEntityAsync(tenantId, query.LegalEntityId, cancellationToken);
        await EnsurePartyAsync(tenantId, query.LegalEntityId, query.PartyId, cancellationToken);
        var balance = await repository.GetPartyBalanceAsync(tenantId, query, cancellationToken);
        if (balance.CreditLimitAmount is null || balance.CreditLimitCurrency is null) return balance;
        var currencies = balance.Currencies.Select(item => item.CurrencyCode).Append(balance.CreditLimitCurrency).Distinct(StringComparer.Ordinal).ToArray();
        var contexts = new Dictionary<string, FinancialCalculationContextSnapshotContract>(StringComparer.Ordinal);
        foreach (var currency in currencies)
        {
            if (currency == baseCurrency) continue;
            var context = await ResolveFinancialContextAsync(tenantId, query.LegalEntityId, currency, FinancialCalculationPurpose.Reporting, query.AsOfDate, cancellationToken);
            if (context?.ExchangeRate is null) return balance with { AvailableCreditAmount = null };
            contexts[currency] = context;
        }
        decimal ToBase(string currency, decimal amount)
        {
            if (currency == baseCurrency) return amount;
            var rate = ParseAmount(contexts[currency].ExchangeRate!.Rate);
            return amount * rate;
        }
        var netExposureBase = balance.Currencies.Sum(item => ToBase(item.CurrencyCode, ParseAmount(item.NetBalance)));
        var creditLimit = ParseAmount(balance.CreditLimitAmount);
        var limitBase = ToBase(balance.CreditLimitCurrency, creditLimit);
        var limitRate = balance.CreditLimitCurrency == baseCurrency ? 1m : ParseAmount(contexts[balance.CreditLimitCurrency].ExchangeRate!.Rate);
        var rounding = await ResolveFinancialContextAsync(tenantId, query.LegalEntityId, baseCurrency, FinancialCalculationPurpose.Reporting, query.AsOfDate, cancellationToken);
        var roundingRule = rounding?.RoundingPolicy.ContextRules.SingleOrDefault(rule => rule.Context == FinancialRoundingContext.FxBaseAmount);
        if (rounding is null || roundingRule is null || !Enum.TryParse<FinancialRoundingModeValue>(roundingRule.Mode.ToString(), out var mode))
            return balance with { AvailableCreditAmount = null };
        var availableBase = FinancialRounding.Round(limitBase - netExposureBase, rounding.RoundingPolicy.MoneyScale, mode);
        var availableCredit = FinancialRounding.Round(availableBase / limitRate, rounding.RoundingPolicy.MoneyScale, mode);
        return balance with { AvailableCreditAmount = availableCredit.ToString(CultureInfo.InvariantCulture) };
        }
        finally { SubledgerObservability.ExposureQueryDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds); }
    }

    public async Task<PartyBalanceResult> GetPartyBalanceForMemberAsync(TenantId tenantId, Guid membershipId, PartyBalanceQuery query, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, query.LegalEntityId, query.PartyId);
        var permissions = query.Direction == SubledgerDirectionContract.Receivable
            ? new[] { "subledger.read", "subledger.credit_limit.read" }
            : ["subledger.read"];
        var subject = await authorizationSubjects.LoadAsync(tenantId, membershipId, cancellationToken);
        if (subject is null || !permissions.Any(permission => permissionAuthorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenantId, query.LegalEntityId))))
            throw new SubledgerException("subledger.permission_denied", "The selected role cannot read this party exposure.");
        return await GetPartyBalanceAsync(tenantId, query, cancellationToken);
    }

    public async Task<IReadOnlyList<AgingBucketResult>> GetAgingAsync(TenantId tenantId, Guid membershipId, AgingQuery query, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, query.LegalEntityId, query.PartyId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.aging.read", query.LegalEntityId, cancellationToken);
        _ = MapDirection(query.Direction);
        if (query.AsOfDate == DateOnly.MinValue) throw new SubledgerException("subledger.as_of_date_required", "An as-of date is required.");
        if (query.CurrencyCode is not null) query = query with { CurrencyCode = NormalizeCurrency(query.CurrencyCode) };
        await EnsureEntityAsync(tenantId, query.LegalEntityId, cancellationToken);
        await EnsurePartyAsync(tenantId, query.LegalEntityId, query.PartyId, cancellationToken);
        return await repository.GetAgingAsync(tenantId, query, cancellationToken);
    }

    public async Task<PartyStatementResult> GetStatementAsync(TenantId tenantId, Guid membershipId, PartyStatementQuery query, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, query.LegalEntityId, query.PartyId);
        await RequirePermissionAsync(tenantId, membershipId, "subledger.statement.read", query.LegalEntityId, cancellationToken);
        _ = MapDirection(query.Direction);
        if (query.FromDate == DateOnly.MinValue || query.ToDate < query.FromDate || query.ToDate.DayNumber - query.FromDate.DayNumber > 366)
            throw new SubledgerException("subledger.statement_range_invalid", "A valid statement date range of up to one year is required.");
        if (query.Limit is < 1 or > 500 || query.Page is < 1 or > 1000)
            throw new SubledgerException("subledger.page_size_invalid", "Statement pages must include between 1 and 500 lines and use a page number from 1 to 1000.");
        if (query.CurrencyCode is not null) query = query with { CurrencyCode = NormalizeCurrency(query.CurrencyCode) };
        await EnsureEntityAsync(tenantId, query.LegalEntityId, cancellationToken);
        await EnsurePartyAsync(tenantId, query.LegalEntityId, query.PartyId, cancellationToken);
        return await repository.GetStatementAsync(tenantId, query, cancellationToken);
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
        return await repository.CreateOpenItemsAsync(tenantId, command.LegalEntityId, command.PartyId, direction, polarity, requiredSourceType, snapshots, cancellationToken, command.CorrelationId, command.ActorId);
    }

    private async Task<IReadOnlyList<Guid>> CreateScheduledOpenItemsAsync(TenantId tenantId, CreateScheduledOpenItemsCommand command, SubledgerDirection direction, OpenItemPolarity polarity, string sourceType, CancellationToken ct)
    {
        ValidateIdentity(tenantId, command.LegalEntityId, command.PartyId);
        EnsureActor(command.ActorId, command.CorrelationId);
        if (command.PaymentTermId == Guid.Empty) throw new SubledgerException("subledger.payment_term_not_found", "A payment term is required for an installment schedule.");
        var source = command.Source;
        if (source.SourceScheduleReference is not null) throw new SubledgerException("subledger.source_schedule_invalid", "The source must describe the whole document before installments are generated.");
        var baseCurrency = await EnsureEntityAsync(tenantId, command.LegalEntityId, ct);
        await EnsurePartyAsync(tenantId, command.LegalEntityId, command.PartyId, ct);
        var transactionCurrency = NormalizeCurrency(source.TransactionCurrency);
        if (!string.Equals(NormalizeCurrency(source.BaseCurrency), baseCurrency, StringComparison.Ordinal)) throw new SubledgerException("subledger.base_currency_mismatch", "The source base currency must match the legal entity.");
        await EnsureCurrencyEnabledAsync(tenantId, command.LegalEntityId, transactionCurrency, source.PostingDate, ct);
        var total = ParseAmount(source.OriginalTransactionAmount);
        var baseTotal = ParseAmount(source.OriginalBaseAmount);
        var schedule = await financeBase.CalculatePaymentScheduleAsync(tenantId, command.LegalEntityId, command.PaymentTermId, transactionCurrency, total.ToString(CultureInfo.InvariantCulture), source.DocumentDate, ct)
            ?? throw new SubledgerException("subledger.payment_term_not_found", "The payment term is not active for the document date.");
        if (schedule.Lines.Count is < 1 or > 200 || schedule.Lines.Any(line => ParseAmount(line.Amount.Amount) <= 0m) || schedule.Lines.Sum(line => ParseAmount(line.Amount.Amount)) != total)
            throw new SubledgerException("subledger.payment_schedule_invalid", "The payment term must produce positive installments that exactly equal the source total.");
        var baseRule = schedule.RoundingPolicy.ContextRules.SingleOrDefault(rule => rule.Context == FinancialRoundingContext.FxBaseAmount)
            ?? throw new SubledgerException("subledger.financial_policy_unavailable", "The financial policy has no FX carrying-value rounding rule.");
        if (!Enum.TryParse<FinancialRoundingModeValue>(baseRule.Mode.ToString(), out var baseMode)) throw new SubledgerException("subledger.financial_policy_unavailable", "The FX carrying-value rounding rule is unsupported.");
        var normalized = new List<OpenItemSnapshotContract>(schedule.Lines.Count);
        var allocatedBase = 0m;
        for (var index = 0; index < schedule.Lines.Count; index++)
        {
            var line = schedule.Lines[index];
            var amount = ParseAmount(line.Amount.Amount);
            var baseAmount = index == schedule.Lines.Count - 1 ? baseTotal - allocatedBase : FinancialRounding.Round(baseTotal * amount / total, schedule.RoundingPolicy.MoneyScale, baseMode);
            allocatedBase += baseAmount;
            var termEvidence = JsonSerializer.Serialize(new { PaymentTerm = schedule.PaymentTerm, schedule.RoundingPolicy, Schedule = schedule.Lines, SelectedSequence = line.Sequence });
            normalized.Add(ValidateSnapshot(source with
            {
                SourceScheduleReference = line.Sequence.ToString("D4", CultureInfo.InvariantCulture),
                DueDate = line.DueDate,
                OriginalTransactionAmount = amount.ToString(CultureInfo.InvariantCulture),
                OriginalBaseAmount = baseAmount.ToString(CultureInfo.InvariantCulture),
                PaymentTermSnapshot = termEvidence
            }, direction, baseCurrency));
        }
        if (allocatedBase != baseTotal) throw new SubledgerException("subledger.payment_schedule_mismatch", "Installment carrying values do not reconcile to the source base amount.");
        return await repository.CreateOpenItemsAsync(tenantId, command.LegalEntityId, command.PartyId, direction, polarity, sourceType, normalized, ct, command.CorrelationId, command.ActorId);
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
        if (item.PaymentTermSnapshot is { } termSnapshot)
        {
            if (termSnapshot.Length > 8000) throw new SubledgerException("subledger.payment_term_snapshot_invalid", "The payment-term snapshot is too large.");
            try { using var _ = JsonDocument.Parse(termSnapshot); }
            catch (JsonException) { throw new SubledgerException("subledger.payment_term_snapshot_invalid", "The payment-term snapshot must contain valid JSON."); }
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

    private async Task RequirePermissionAsync(TenantId tenant, Guid membershipId, string permission, LegalEntityId entity, CancellationToken ct)
    {
        if (membershipId == Guid.Empty) throw new SubledgerException("subledger.authorization_required", "An authorized membership is required for this operation.");
        var subject = await authorizationSubjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null || !permissionAuthorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, entity)))
            throw new SubledgerException("subledger.permission_denied", "You do not have permission to perform this financial operation.");
    }

    private async Task<FinancialCalculationContextSnapshotContract?> ResolveFinancialContextAsync(TenantId tenant, LegalEntityId entity, string currency,
        FinancialCalculationPurpose purpose, DateOnly businessDate, CancellationToken ct)
    {
        try { return await financeBase.ResolveCalculationContextAsync(tenant, entity, currency, purpose, businessDate, ct); }
        catch (FinanceBaseCapabilityException exception)
        {
            var code = exception.Code.Contains("rate_stale", StringComparison.Ordinal) ? "subledger.fx_rate_stale"
                : exception.Code.Contains("rate", StringComparison.Ordinal) ? "subledger.fx_rate_missing"
                : exception.Code.Contains("currency", StringComparison.Ordinal) ? "subledger.currency_not_allowed"
                : "subledger.financial_policy_unavailable";
            throw new SubledgerException(code, exception.Message);
        }
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
    private static string NormalizeMatchReference(string value) => string.Concat(value.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit));
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
    private static bool TryConfigurationReference(string reference, out Guid configurationId)
    {
        configurationId = Guid.Empty;
        return reference.StartsWith("configuration:", StringComparison.Ordinal)
            && Guid.TryParse(reference["configuration:".Length..], out configurationId);
    }

    private sealed record ResolvedApplicationBatch(IReadOnlyList<ResolvedApplicationLineContract> Lines, int MoneyScale, FinancialRoundingModeValue RoundingMode);
}
