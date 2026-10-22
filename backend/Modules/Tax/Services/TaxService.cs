using System.Globalization;
using Modules.Tax.DTOs;
using Modules.Tax.Entities;
using Modules.Tax.Repositories;
using Shared.Contracts.Catalog;
using Shared.Contracts.Directory;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Tax;
using Shared.Primitives.Financial;
using NodaTime;

namespace Modules.Tax.Services;

public sealed class TaxService(ITaxRepository repository, ICatalogCapabilities catalog, IQuantityConversion quantityConversion, IDirectoryCapabilities directory, IFinanceBaseCapabilities financeBase, ITaxCountryPackRegistry countryPacks) : ITaxCapabilities
{
    public async Task<TaxCapabilityDto?> GetCapabilityAsync(Guid tenantId, Guid legalEntityId, DateOnly at, CancellationToken cancellationToken) => await repository.GetCapabilityAsync(tenantId, legalEntityId, at, cancellationToken);

    public Task<TaxCapabilityDto> SetCapabilityAsync(Guid tenantId, Guid legalEntityId, Guid actorId, SetTaxCapabilityRequest request, Guid correlationId, CancellationToken cancellationToken) =>
        repository.SetCapabilityAsync(tenantId, legalEntityId, actorId, request.Capability, request.EffectiveFrom, correlationId, cancellationToken);

    public async Task<TaxSnapshotContract> CaptureSnapshotAsync(TaxSnapshotCaptureContract snapshot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(snapshot.SourceDocumentKind) || snapshot.SourceDocumentKind.Length > 80 || snapshot.SourceDocumentId == Guid.Empty)
            throw new TaxException("tax.snapshot_source_invalid", "A source document type and identifier are required to capture a Tax Snapshot.");
        var tenantId = snapshot.Context.TenantId.Value;
        var legalEntityId = snapshot.Context.LegalEntityId.Value;
        var existing = await repository.FindSnapshotAsync(tenantId, snapshot.SourceDocumentKind, snapshot.SourceDocumentId, snapshot.SourceLineId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Input.LegalEntityId.Value != legalEntityId)
                throw new TaxException("tax.snapshot_source_conflict", "The source document reference is already bound to another legal entity.");
            return existing;
        }

        var result = await CalculateAsync(snapshot.Context, cancellationToken);
        var evidence = new TaxSnapshotEvidenceContract(
            result.EvaluationState,
            result.RegistrationSnapshot,
            result.Explanation.PartyEvidence ?? (snapshot.Context.PartyId is { } partyId ? System.Text.Json.JsonSerializer.Serialize(new { partyId, snapshot.Context.PartyCountryCode, treatment = result.Explanation.PartyTreatment, snapshot.Context.TaxSchemeCode }) : null),
            result.Explanation.ProductEvidence ?? (snapshot.Context.ProductTemplateId is { } productId ? System.Text.Json.JsonSerializer.Serialize(new { productId, snapshot.Context.ProductVariantId, category = result.Explanation.ProductCategory, snapshot.Context.TaxSchemeCode }) : null),
            result.JurisdictionSnapshot,
            result.TaxFxSnapshot,
            result.RoundingPolicyVersion,
            result.QuantitySnapshot);
        var captured = new TaxSnapshotContract(Guid.CreateVersion7(), snapshot.SourceDocumentKind, snapshot.SourceDocumentId, snapshot.SourceLineId,
            result.Explanation.TaxDate, result.EvaluationState, result.RuleSetVersion, result.CalculationVersion, snapshot.Context, result, DateTimeOffset.UtcNow, evidence);
        return await repository.SaveSnapshotAsync(tenantId, legalEntityId, captured, cancellationToken);
    }

    public async Task<TaxResultContract> CalculateAsync(TaxContextContract context, CancellationToken cancellationToken = default)
    {
        var domain = ToDomain(context);
        TaxRepository.TaxPolicyResolution? policy = null;
        string? placeOfSupplyReason = null;
        LocalDate date;
        if (domain.TaxDate is { } suppliedTaxDate) date = suppliedTaxDate;
        else
        {
            var preliminaryPack = domain.PlaceOfSupplyCountryCode is null ? null : countryPacks.Find(domain.PlaceOfSupplyCountryCode,domain.DocumentDate);
            if (preliminaryPack is not null) date = preliminaryPack.ResolveTaxDate(domain).Date;
            else
            {
                policy = await repository.GetPolicyAsync(context.TenantId.Value,context.LegalEntityId.Value,cancellationToken);
                date = TaxDateResolver.Resolve(domain,policy.DatePolicy).Date;
            }
        }
        var dateOnly = new DateOnly(date.Year, date.Month, date.Day);
        var capability = await repository.GetCapabilityAsync(context.TenantId.Value, context.LegalEntityId.Value, dateOnly, cancellationToken);
        if (capability?.Capability != "Enabled")
        {
            var disabled = TaxCalculator.Disabled(domain, FormatDate(date));
            return Map(disabled);
        }

        policy ??= await repository.GetPolicyAsync(context.TenantId.Value,context.LegalEntityId.Value,cancellationToken);

        var countryPack = domain.PlaceOfSupplyCountryCode is null ? null : countryPacks.Find(domain.PlaceOfSupplyCountryCode,date);
        if (countryPack is not null)
        {
            var jurisdiction = countryPack.ResolveJurisdiction(domain,date);
            domain = domain with { JurisdictionCode = jurisdiction.JurisdictionCode };
            placeOfSupplyReason = jurisdiction.PlaceOfSupplyReason;
        }
        else if (domain.JurisdictionCode is null)
            throw new TaxException("tax.jurisdiction_unresolved", "Tax needs an explicit jurisdiction or a reviewed Country Pack to resolve place of supply.");

        if (domain.PartyId is { } partyId && !await directory.IsActivePartyAsync(context.TenantId, partyId, domain.LegalEntityId, cancellationToken))
            throw new TaxException("tax.party_not_found", "The selected Party is not active for this legal entity.");
        if (domain.ProductVariantId is { } variantId)
        {
            var product = await catalog.GetPricingContextAsync(context.TenantId, variantId, domain.LegalEntityId, cancellationToken)
                ?? throw new TaxException("tax.product_not_found", "The selected product or service is not active in Catalog.");
            if (domain.ProductTemplateId is { } suppliedTemplate && suppliedTemplate != product.ProductTemplateId)
                throw new TaxException("tax.product_context_mismatch", "The product variant does not belong to the supplied product template.");
            if (domain.UnitOfMeasureId is { } uom)
            {
                var effectiveAt = date.AtStartOfDayInZone(DateTimeZone.Utc).ToDateTimeOffset();
                var conversion = await quantityConversion.ConvertAsync(context.TenantId, variantId, uom, domain.Quantity, effectiveAt, domain.LegalEntityId, cancellationToken);
                if (conversion is null) throw new TaxException("tax.uom_invalid", "The unit or quantity is not valid for this product at the Tax Date.");
                domain = domain with { Quantity = conversion.BaseQuantity, QuantitySnapshot = System.Text.Json.JsonSerializer.Serialize(conversion) };
            }
            domain = domain with { ProductTemplateId = product.ProductTemplateId };
        }
        else if (domain.ProductTemplateId is { } templateId && await catalog.GetProductSummaryAsync(context.TenantId, templateId, domain.LegalEntityId, cancellationToken) is null)
            throw new TaxException("tax.product_not_found", "The selected product or service is not active in Catalog.");

        var profiles = await repository.ResolveProfilesAsync(context.TenantId.Value, domain, dateOnly, cancellationToken);
        domain = domain with { RegistrationId = profiles.RegistrationId, ProductTaxCategoryId = profiles.ProductCategoryId, PartyTaxCategoryId = profiles.PartyCategoryId };
        if (domain.ProductTaxCategoryId is null)
            throw new TaxException("tax.product_classification_missing", "The product or service has no Tax Category for this Tax Date.");
        var finance = await financeBase.ResolveCalculationContextAsync(context.TenantId,context.LegalEntityId,context.TransactionCurrency,FinancialCalculationPurpose.Tax,dateOnly,cancellationToken)
            ?? throw new TaxException("tax.finbase_context_missing", "FinBase could not resolve the legal entity's financial policy for Tax.");
        if (!string.Equals(finance.BaseCurrencyCode,context.BaseCurrency,StringComparison.Ordinal)) throw new TaxException("tax.base_currency_mismatch", "The supplied base currency does not match the Legal Entity's FinBase currency.");
        var taxFxSnapshot = finance.ExchangeRate is null ? null : System.Text.Json.JsonSerializer.Serialize(finance.ExchangeRate);
        if (!string.Equals(finance.BaseCurrencyCode,finance.TransactionCurrencyCode,StringComparison.Ordinal) && finance.ExchangeRate is null)
            throw new TaxException("tax.fx_rate_missing", "FinBase did not provide an approved Tax-purpose exchange rate for the Tax Date.");
        var roundingContext = policy.RoundingStrategy switch { TaxRoundingStrategy.PerLine => FinancialRoundingContext.LineNetAmount, TaxRoundingStrategy.PerTaxComponent => FinancialRoundingContext.DocumentSubtotal, _ => FinancialRoundingContext.DocumentTotal };
        var roundingMode = finance.RoundingPolicy.ContextRules.FirstOrDefault(rule => rule.Context == roundingContext)?.Mode
            ?? throw new TaxException("tax.rounding_policy_missing", "FinBase has no rounding mode configured for the required Tax calculation context.");
        var mode = roundingMode;
        var selected = await repository.FindRuleAsync(context.TenantId.Value, domain, dateOnly, cancellationToken)
            ?? throw new TaxException("tax.rule_missing", "No approved tax rule matches this transaction and Tax Date.");
        var result = TaxCalculator.Calculate(domain, FormatDate(date), profiles.RegistrationStatus, selected.Rule, selected.PackCode, selected.PackVersion, profiles.PartyTreatment, domain.ProductTaxCategoryId?.ToString("D"), policy.RoundingStrategy, finance.RoundingPolicy.MoneyScale, mode, taxFxSnapshot);
        result = result with
        {
            RegistrationSnapshot = System.Text.Json.JsonSerializer.Serialize((object?)profiles.RegistrationEvidence ?? new { id = profiles.RegistrationId, scheme = domain.TaxSchemeCode, status = profiles.RegistrationStatus, effectiveTaxDate = FormatDate(date) }),
            RoundingPolicyVersion = finance.RoundingPolicy.Version.ToString(CultureInfo.InvariantCulture),
            Explanation = result.Explanation with { PlaceOfSupplyReason = placeOfSupplyReason, PartyEvidence = profiles.PartyEvidence is null ? null : System.Text.Json.JsonSerializer.Serialize(profiles.PartyEvidence), ProductEvidence = profiles.ProductEvidence is null ? null : System.Text.Json.JsonSerializer.Serialize(profiles.ProductEvidence) }
        };
        return Map(result);
    }

    public async Task<IReadOnlyList<TaxResultContract>> CalculateBatchAsync(IReadOnlyList<TaxContextContract> contexts, CancellationToken cancellationToken = default)
    {
        if (contexts.Count is < 1 or > 500) throw new TaxException("tax.batch_size_invalid", "Tax calculation batches must contain between one and five hundred lines.");
        var tenant = contexts[0].TenantId;
        if (contexts.Any(context => context.TenantId != tenant)) throw new TaxException("tax.batch_tenant_mismatch", "A Tax calculation batch must contain one tenant.");
        var output = new TaxResultContract[contexts.Count];
        foreach (var group in contexts.Select((context,index)=>(context,index)).GroupBy(item => item.context.LegalEntityId))
        {
            var materialized = group.ToArray();
            TaxRepository.TaxPolicyResolution? policy = null;
            var dates = new List<(int index, TaxContextContract context, TaxCalculationContext domain, LocalDate date)>(materialized.Length);
            foreach (var item in materialized)
            {
                var domain = ToDomain(item.context);
                LocalDate date;
                if (domain.TaxDate is { } suppliedTaxDate) date = suppliedTaxDate;
                else if (domain.PlaceOfSupplyCountryCode is { } countryCode && countryPacks.Find(countryCode, domain.DocumentDate) is { } pack)
                    date = pack.ResolveTaxDate(domain).Date;
                else
                {
                    policy ??= await repository.GetPolicyAsync(tenant.Value, group.Key.Value, cancellationToken);
                    date = TaxDateResolver.Resolve(domain, policy.DatePolicy).Date;
                }
                dates.Add((item.index,item.context,domain,date));
            }
            foreach (var dateGroup in dates.GroupBy(item => new DateOnly(item.date.Year,item.date.Month,item.date.Day)))
            {
                var capability = await repository.GetCapabilityAsync(tenant.Value, group.Key.Value, dateGroup.Key, cancellationToken);
                if (capability?.Capability != "Enabled")
                {
                    foreach (var item in dateGroup) output[item.index] = Map(TaxCalculator.Disabled(item.domain,FormatDate(item.date)));
                    continue;
                }
                policy ??= await repository.GetPolicyAsync(tenant.Value,group.Key.Value,cancellationToken);
                var lines = dateGroup.ToArray();
                var domainByIndex = lines.ToDictionary(item => item.index, item => item.domain);
                var jurisdictionReasons = new Dictionary<int, string?>();
                foreach (var item in lines)
                {
                    var countryPack = item.domain.PlaceOfSupplyCountryCode is null ? null : countryPacks.Find(item.domain.PlaceOfSupplyCountryCode, item.date);
                    if (countryPack is not null)
                    {
                        var jurisdiction = countryPack.ResolveJurisdiction(item.domain, item.date);
                        domainByIndex[item.index] = item.domain with { JurisdictionCode = jurisdiction.JurisdictionCode };
                        jurisdictionReasons[item.index] = jurisdiction.PlaceOfSupplyReason;
                    }
                    else if (item.domain.JurisdictionCode is null) throw new TaxException("tax.jurisdiction_unresolved", "Tax needs an explicit jurisdiction or a reviewed Country Pack to resolve place of supply.");
                }
                var activePartyIds = lines.Where(item => item.domain.PartyId is not null).Select(item => item.domain.PartyId!.Value).Distinct().ToArray();
                if (activePartyIds.Length > 0)
                {
                    var active = await directory.FindActivePartiesAsync(tenant, activePartyIds, group.Key.Value, cancellationToken);
                    if (active.Count != activePartyIds.Length) throw new TaxException("tax.party_not_found", "A selected Party is not active for this legal entity.");
                }
                var variantIds = lines.Where(item => item.domain.ProductVariantId is not null).Select(item => item.domain.ProductVariantId!.Value).Distinct().ToArray();
                var productContexts = variantIds.Length == 0 ? new Dictionary<Guid, CatalogVariantPricingContext>() : new Dictionary<Guid, CatalogVariantPricingContext>(await catalog.GetPricingContextsBatchAsync(tenant, variantIds, group.Key.Value, cancellationToken));
                if (productContexts.Count != variantIds.Length) throw new TaxException("tax.product_not_found", "A selected product or service is not active in Catalog.");
                var templateIds = lines.Where(item => item.domain.ProductVariantId is null && item.domain.ProductTemplateId is not null).Select(item => item.domain.ProductTemplateId!.Value).Distinct().ToArray();
                var templates = new Dictionary<Guid, CatalogProductSummary>();
                foreach (var templateId in templateIds) templates[templateId] = await catalog.GetProductSummaryAsync(tenant, templateId, group.Key.Value, cancellationToken) ?? throw new TaxException("tax.product_not_found", "A selected product or service is not active in Catalog.");
                foreach (var item in lines)
                {
                    if (item.domain.ProductVariantId is { } variant)
                    {
                        var product = productContexts[variant];
                        if (item.domain.ProductTemplateId is { } supplied && supplied != product.ProductTemplateId) throw new TaxException("tax.product_context_mismatch", "A product variant does not belong to the supplied product template.");
                        domainByIndex[item.index] = domainByIndex[item.index] with { ProductTemplateId = product.ProductTemplateId };
                    }
                }
                var uomLines = lines.Where(item => item.domain.ProductVariantId is not null && item.domain.UnitOfMeasureId is not null).ToArray();
                if (uomLines.Length > 0)
                {
                    var at = dateGroup.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    var allowed = await catalog.GetAllowedUomsBatchAsync(tenant, uomLines.Select(item => item.domain.ProductVariantId!.Value).Distinct().ToArray(), new DateTimeOffset(at), group.Key.Value, cancellationToken);
                    foreach (var item in uomLines)
                    {
                        var unit = allowed.GetValueOrDefault(item.domain.ProductVariantId!.Value)?.FirstOrDefault(unit => unit.UnitId == item.domain.UnitOfMeasureId);
                        if (unit is null || !unit.AllowFraction && item.domain.Quantity != decimal.Truncate(item.domain.Quantity)) throw new TaxException("tax.uom_invalid", "The unit or quantity is not valid for this product at the Tax Date.");
                        var precision = unit.RoundingPrecision ?? unit.Precision;
                        var baseQuantity = decimal.Round(checked(item.domain.Quantity * unit.FactorToBase), precision, MidpointRounding.ToEven);
                        domainByIndex[item.index] = domainByIndex[item.index] with { Quantity = baseQuantity, QuantitySnapshot = System.Text.Json.JsonSerializer.Serialize(new CatalogQuantityConversion(item.domain.ProductVariantId.Value, unit.UnitId, unit.UnitCode, item.domain.Quantity, unit.FactorToBase, baseQuantity, new DateTimeOffset(dateGroup.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)))) };
                    }
                }
                var batchContexts = lines.Select(item => new TaxBatchContext(item.index, domainByIndex[item.index], dateGroup.Key)).ToArray();
                var profiles = await repository.ResolveProfilesBatchAsync(tenant.Value, batchContexts, cancellationToken);
                var ruleContexts = new List<TaxBatchRuleContext>(lines.Length);
                foreach (var item in lines)
                {
                    if (!profiles.TryGetValue(item.index, out var profile)) throw new TaxException("tax.profile_resolution_missing", "Tax profile resolution did not return a result for every line.");
                    domainByIndex[item.index] = domainByIndex[item.index] with { RegistrationId = profile.RegistrationId, ProductTaxCategoryId = profile.ProductCategoryId, PartyTaxCategoryId = profile.PartyCategoryId };
                    if (domainByIndex[item.index].ProductTaxCategoryId is null) throw new TaxException("tax.product_classification_missing", "The product or service has no Tax Category for this Tax Date.");
                    ruleContexts.Add(new(item.index, domainByIndex[item.index], dateGroup.Key));
                }
                var rules = await repository.FindRulesBatchAsync(tenant.Value, ruleContexts, cancellationToken);
                var finances = new Dictionary<string, FinancialCalculationContextSnapshotContract>(StringComparer.Ordinal);
                foreach (var currency in lines.Select(item => item.context.TransactionCurrency).Distinct(StringComparer.Ordinal))
                    finances[currency] = await financeBase.ResolveCalculationContextAsync(tenant, group.Key, currency, FinancialCalculationPurpose.Tax, dateGroup.Key, cancellationToken)
                        ?? throw new TaxException("tax.finbase_context_missing", "FinBase could not resolve the legal entity's financial policy for Tax.");
                foreach (var item in lines)
                {
                    if (!rules.TryGetValue(item.index, out var selected)) throw new TaxException("tax.rule_missing", "No approved tax rule matches this transaction and Tax Date.");
                    var finance = finances[item.context.TransactionCurrency];
                    if (!string.Equals(finance.BaseCurrencyCode,item.context.BaseCurrency,StringComparison.Ordinal)) throw new TaxException("tax.base_currency_mismatch", "The supplied base currency does not match the Legal Entity's FinBase currency.");
                    var fx = finance.ExchangeRate is null ? null : System.Text.Json.JsonSerializer.Serialize(finance.ExchangeRate);
                    if (!string.Equals(finance.BaseCurrencyCode,finance.TransactionCurrencyCode,StringComparison.Ordinal) && finance.ExchangeRate is null) throw new TaxException("tax.fx_rate_missing", "FinBase did not provide an approved Tax-purpose exchange rate for the Tax Date.");
                    var roundingContext = policy.RoundingStrategy switch { TaxRoundingStrategy.PerLine => FinancialRoundingContext.LineNetAmount, TaxRoundingStrategy.PerTaxComponent => FinancialRoundingContext.DocumentSubtotal, _ => FinancialRoundingContext.DocumentTotal };
                    var roundingMode = finance.RoundingPolicy.ContextRules.FirstOrDefault(rule => rule.Context == roundingContext)?.Mode ?? throw new TaxException("tax.rounding_policy_missing", "FinBase has no rounding mode configured for the required Tax calculation context.");
                    var mode = roundingMode;
                    var profile = profiles[item.index];
                    var resolvedDomain = domainByIndex[item.index];
                    var result = TaxCalculator.Calculate(resolvedDomain, FormatDate(item.date), profile.RegistrationStatus, selected.Rule, selected.PackCode, selected.PackVersion, profile.PartyTreatment, resolvedDomain.ProductTaxCategoryId?.ToString("D"), policy.RoundingStrategy, finance.RoundingPolicy.MoneyScale, mode, fx, finance.RoundingPolicy.Version.ToString(CultureInfo.InvariantCulture));
                    result = result with { RegistrationSnapshot = System.Text.Json.JsonSerializer.Serialize((object?)profile.RegistrationEvidence ?? new { id = profile.RegistrationId, scheme = resolvedDomain.TaxSchemeCode, status = profile.RegistrationStatus, effectiveTaxDate = FormatDate(item.date) }), Explanation = result.Explanation with { PlaceOfSupplyReason = jurisdictionReasons.GetValueOrDefault(item.index), PartyEvidence = profile.PartyEvidence is null ? null : System.Text.Json.JsonSerializer.Serialize(profile.PartyEvidence), ProductEvidence = profile.ProductEvidence is null ? null : System.Text.Json.JsonSerializer.Serialize(profile.ProductEvidence) } };
                    output[item.index] = Map(result);
                }
                if (policy.RoundingStrategy == TaxRoundingStrategy.PerDocument)
                    foreach (var currencyGroup in lines.GroupBy(item => item.context.TransactionCurrency, StringComparer.Ordinal))
                        ReconcileDocumentTotals(currencyGroup.ToArray(), output, finances[currencyGroup.Key]);
            }
        }
        return output;
    }

    private static void ReconcileDocumentTotals((int index, TaxContextContract context, TaxCalculationContext domain, LocalDate date)[] lines, TaxResultContract[] outputs, FinancialCalculationContextSnapshotContract finance)
    {
        if (lines.Length == 0) return;
        var mode = finance.RoundingPolicy.ContextRules.FirstOrDefault(rule => rule.Context == FinancialRoundingContext.DocumentTotal)?.Mode
            ?? throw new TaxException("tax.rounding_policy_missing", "FinBase has no Document Total rounding rule for Tax reconciliation.");
        var scale = finance.RoundingPolicy.MoneyScale;
        var exactTotal = lines.Sum(item => decimal.Parse(outputs[item.index].RoundingEvidence?.UnroundedTaxTotal ?? outputs[item.index].TaxTotal, CultureInfo.InvariantCulture));
        var preTotal = lines.Sum(item => decimal.Parse(outputs[item.index].TaxTotal, CultureInfo.InvariantCulture));
        var finalTotal = FinancialRounding.Round(exactTotal, scale, Enum.Parse<FinancialRoundingModeValue>(mode.ToString(), true));
        var residual = finalTotal - preTotal;
        if (residual == 0m) return;
        var target = lines.LastOrDefault(item => outputs[item.index].TaxLines.Count > 0 && outputs[item.index].Treatment is not ("ZeroRated" or "Exempt" or "OutOfScope"));
        if (target.context is null) throw new TaxException("tax.rounding_residual_unallocated", "The document Tax rounding residual has no eligible Tax Line for deterministic allocation.");
        var result = outputs[target.index];
        var taxLines = result.TaxLines.ToArray();
        var last = taxLines[^1];
        var previousLineAmount = decimal.Parse(last.TaxAmount, CultureInfo.InvariantCulture);
        var adjustedLineAmount = previousLineAmount + residual;
        TaxRecoverabilityContract? recoverability = last.Recoverability;
        if (recoverability is not null)
        {
            var percentage = decimal.Parse(recoverability.RecoverablePercentage, CultureInfo.InvariantCulture);
            var recoverable = FinancialRounding.Round(adjustedLineAmount * percentage / 100m, scale, Enum.Parse<FinancialRoundingModeValue>(mode.ToString(), true));
            recoverability = recoverability with { RecoverableAmount = recoverable.ToString("0.############################", CultureInfo.InvariantCulture), NonRecoverableAmount = (adjustedLineAmount - recoverable).ToString("0.############################", CultureInfo.InvariantCulture) };
        }
        taxLines[^1] = last with { TaxAmount = adjustedLineAmount.ToString("0.############################", CultureInfo.InvariantCulture), Recoverability = recoverability };
        var taxTotal = (decimal.Parse(result.TaxTotal, CultureInfo.InvariantCulture) + residual).ToString("0.############################", CultureInfo.InvariantCulture);
        var inclusive = target.context.PriceMode == "TaxInclusive";
        var net = decimal.Parse(result.NetAmount, CultureInfo.InvariantCulture) + (inclusive ? -residual : 0m);
        var gross = decimal.Parse(result.GrossAmount, CultureInfo.InvariantCulture) + (inclusive ? 0m : residual);
        var evidence = result.RoundingEvidence! with { PreReconciliationTotal = preTotal.ToString("0.############################", CultureInfo.InvariantCulture), Residual = residual.ToString("0.############################", CultureInfo.InvariantCulture), AllocationTarget = $"{target.index}:{last.TaxCode}", FinalTotal = taxTotal };
        outputs[target.index] = result with { TaxLines = taxLines, TaxTotal = taxTotal, NetAmount = net.ToString("0.############################", CultureInfo.InvariantCulture), GrossAmount = gross.ToString("0.############################", CultureInfo.InvariantCulture), RoundingEvidence = evidence };
    }

    private static TaxCalculationContext ToDomain(TaxContextContract input)
    {
        if (!Enum.TryParse<TaxDirection>(input.Direction,true,out var direction) || !Enum.TryParse<TaxPriceMode>(input.PriceMode,true,out var priceMode))
            throw new TaxException("tax.context_invalid", "Direction and Price Mode must be supported values.");
        if (input.TenantId.Value == Guid.Empty || input.LegalEntityId.Value == Guid.Empty || input.Quantity <= 0m)
            throw new TaxException("tax.context_invalid", "Tenant, legal entity, and positive quantity are required.");
        decimal Parse(string value, string field)
        {
            if (!decimal.TryParse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var amount)) throw new TaxException("tax.amount_invalid", $"{field} must be an invariant decimal string.");
            return amount;
        }
        var commercial = Parse(input.CommercialAmount,"Commercial Amount");
        var discount = Parse(input.DiscountAmount,"Discount Amount");
        var charges = Parse(input.TaxableCharges,"Taxable Charges");
        if (commercial < 0m || discount < 0m || charges < 0m || discount > commercial + charges) throw new TaxException("tax.amount_invalid", "Commercial amount, discount, and charges must be nonnegative and the discount cannot exceed the available amount.");
        if (input.TransactionCurrency.Length != 3 || input.BaseCurrency.Length != 3 || input.TransactionCurrency.Any(character => !char.IsAsciiLetterUpper(character)) || input.BaseCurrency.Any(character => !char.IsAsciiLetterUpper(character))) throw new TaxException("tax.currency_invalid", "Transaction and base currency must be three-letter uppercase codes.");
        var semantics = Enum.TryParse<TaxDocumentSemantics>(input.DocumentSemantics ?? input.DocumentKind, true, out var parsedSemantics) ? parsedSemantics : TaxDocumentSemantics.Invoice;
        return new(input.TenantId.Value,input.LegalEntityId.Value,direction,input.DocumentKind,input.TransactionType,LocalDate.FromDateTime(input.DocumentDate.ToDateTime(TimeOnly.MinValue)),input.TaxDate is { } taxDate ? LocalDate.FromDateTime(taxDate.ToDateTime(TimeOnly.MinValue)) : null,input.SupplyDate is { } supplyDate ? LocalDate.FromDateTime(supplyDate.ToDateTime(TimeOnly.MinValue)) : null,input.PostingDate is { } postingDate ? LocalDate.FromDateTime(postingDate.ToDateTime(TimeOnly.MinValue)) : null,input.PartyId,input.PartyCountryCode,input.PlaceOfSupplyCountryCode,null,input.JurisdictionCode,input.ProductTemplateId,input.ProductVariantId,null,input.Quantity,input.UnitOfMeasureId,priceMode,commercial,discount,charges,input.TransactionCurrency,input.BaseCurrency,input.RegistrationId,null,input.TaxSchemeCode,semantics,null,input.Dimensions);
    }

    private static TaxResultContract Map(TaxResult result) => new(result.EvaluationState.ToString(),result.Treatment?.ToString(),result.NetAmount,result.TaxTotal,result.GrossAmount,result.TaxLines.Select(line => new TaxLineContract(line.TaxTypeCode,line.TaxCode,line.TreatmentCode,line.Rate,line.FixedAmountPerUnit,line.TaxableBase,line.TaxAmount,line.Currency,line.Jurisdiction,line.LegalReasonCode,line.LegalReasonText,line.ReportingCode,line.RuleId,line.RuleVersion,line.Recoverability is null ? null : new(line.Recoverability.Kind.ToString(),line.Recoverability.RecoverablePercentage,line.Recoverability.RecoverableAmount,line.Recoverability.NonRecoverableAmount))).ToArray(),result.JurisdictionSnapshot,result.RegistrationSnapshot,result.RuleSetVersion,result.CalculationVersion,new(result.Explanation.CountryPackCode,result.Explanation.CountryPackVersion,result.Explanation.TaxDate,result.Explanation.Jurisdiction,result.Explanation.RegistrationStatus,result.Explanation.PartyTreatment,result.Explanation.ProductCategory,result.Explanation.MatchedRuleId,result.Explanation.ComponentCodes,result.Explanation.Rounding,result.Explanation.Summary,result.Explanation.PlaceOfSupplyReason,result.Explanation.PartyEvidence,result.Explanation.ProductEvidence,result.Explanation.LegalReasonCode,result.Explanation.LegalReasonText),result.TaxFxSnapshot,result.RoundingPolicyVersion,result.RoundingEvidence is null ? null : new(result.RoundingEvidence.Strategy,result.RoundingEvidence.MoneyScale,result.RoundingEvidence.Mode,result.RoundingEvidence.UnroundedTaxTotal,result.RoundingEvidence.PreReconciliationTotal,result.RoundingEvidence.Residual,result.RoundingEvidence.AllocationTarget,result.RoundingEvidence.FinalTotal),result.QuantitySnapshot);
    private static string FormatDate(LocalDate date) => date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
}
