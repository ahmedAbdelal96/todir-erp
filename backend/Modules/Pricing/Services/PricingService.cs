using System.Globalization;
using System.Text.Json;
using Core.Services;
using Modules.Pricing.DTOs;
using Modules.Pricing.Entities;
using Modules.Pricing.Repositories;
using Shared.Contracts.Catalog;
using Shared.Contracts.Directory;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Pricing;
using Shared.Primitives.Financial;
using Shared.Primitives.Identifiers;

namespace Modules.Pricing.Services;

public sealed class PricingService(PricingRepository repository, ICatalogCapabilities catalog, IDirectoryCapabilities directory, IFinanceBaseCapabilities financeBase, ILegalEntityFinancialReadStore organization, TimeProvider clock) : IPricingCapabilities
{
    public Task<PriceListDto> CreateListAsync(TenantId tenant, Guid actor, Guid membership, CreatePriceListRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        _ = CurrencyCode.Normalize(request.CurrencyCode);
        return CreateVerifiedListAsync(tenant, actor, membership, request, correlationId, cancellationToken);
    }

    private async Task<PriceListDto> CreateVerifiedListAsync(TenantId tenant, Guid actor, Guid membership, CreatePriceListRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        await financeBase.ConvertCommercialUnitPriceAsync(tenant, new LegalEntityId(request.LegalEntityId), "1", request.CurrencyCode, request.CurrencyCode, request.Direction == PricingDirection.Buying ? FinancialCalculationPurpose.Purchase : FinancialCalculationPurpose.Sales, request.EffectiveFrom, cancellationToken);
        return await repository.CreateListAsync(tenant, actor, membership, request, correlationId, cancellationToken);
    }

    public Task<IReadOnlyList<PriceListDto>> ListAsync(TenantId tenant, Guid legalEntityId, CancellationToken cancellationToken) => repository.ListAsync(tenant, legalEntityId, cancellationToken);
    public Task<PricingPolicyDto> GetPolicyAsync(TenantId tenant, Guid legalEntityId, CancellationToken cancellationToken) => repository.GetPolicyAsync(tenant, legalEntityId, cancellationToken);
    public Task<PricingPolicyDto> UpdatePolicyAsync(TenantId tenant, Guid actor, Guid membership, Guid legalEntityId, PricingPolicyRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.CurrencyPolicy)) throw new PricingException("pricing.policy_invalid", "Select a supported Pricing currency policy.");
        return repository.UpdatePolicyAsync(tenant, legalEntityId, actor, membership, request, correlationId, cancellationToken);
    }
    public Task<PriceVersionDto> CreateVersionAsync(TenantId tenant, Guid legalEntityId, Guid actor, Guid membership, Guid listId, CreatePriceVersionRequest request, Guid correlationId, CancellationToken cancellationToken) => repository.CreateVersionAsync(tenant, legalEntityId, actor, membership, listId, request, correlationId, cancellationToken);
    public Task<IReadOnlyList<PriceVersionDto>> ListVersionsAsync(TenantId tenant, Guid legalEntityId, Guid listId, CancellationToken cancellationToken) => repository.ListVersionsAsync(tenant, legalEntityId, listId, cancellationToken);
    public async Task<PriceVersionContentPageDto> ListVersionContentAsync(TenantId tenant, Guid legalEntityId, Guid listId, Guid versionId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var content = await repository.ListVersionContentAsync(tenant, legalEntityId, listId, versionId, page, pageSize, cancellationToken);
        var variantIds = content.Items.Where(item => item.VariantId is not null).Select(item => item.VariantId!.Value).Distinct().ToArray();
        if (variantIds.Length == 0) return content;

        var identities = await catalog.FindVariantsBatchAsync(tenant, variantIds, legalEntityId, cancellationToken);
        var enriched = content.Items.Select(item => item.VariantId is { } variantId && identities.TryGetValue(variantId, out var identity)
            ? item with { VariantSku = identity.Sku, ProductDisplayName = identity.DisplayName }
            : item).ToArray();
        return content with { Items = enriched };
    }

    public async Task AddEntryAsync(TenantId tenant, Guid legalEntityId, Guid actor, Guid membership, Guid listId, Guid versionId, CreatePriceEntryRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        if (request.MinQuantity < 0m || request.MaxQuantity is { } max && max <= request.MinQuantity) throw new PricingException("pricing.quantity_range_invalid", "Quantity ranges must have a nonnegative minimum and a greater exclusive maximum.");
        if (!decimal.TryParse(request.UnitPrice, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount < 0m) throw new PricingException("pricing.unit_price_invalid", "Unit price must be a nonnegative invariant decimal string.");
        var product = await catalog.GetPricingContextAsync(tenant, request.VariantId, legalEntityId, cancellationToken) ?? throw new PricingException("pricing.variant_not_found", "An active product variant was not found in Catalog.");
        var allowed = await catalog.GetAllowedUomsAsync(tenant, request.VariantId, DateTimeOffset.UtcNow, legalEntityId, cancellationToken);
        if (!allowed.Any(item => item.UnitId == request.UomId)) throw new PricingException("pricing.uom_not_allowed", "The unit is not currently allowed for this product variant in Catalog.");
        if (request.PartyId is { } party && !await directory.IsActivePartyAsync(tenant, party, legalEntityId, cancellationToken)) throw new PricingException("pricing.party_not_found", "An active party was not found in Directory.");
        _ = product;
        await repository.AddEntryAsync(tenant, legalEntityId, actor, membership, listId, versionId, request, amount, correlationId, cancellationToken);
    }

    public async Task AddRuleAsync(TenantId tenant, Guid legalEntityId, Guid actor, Guid membership, Guid listId, Guid versionId, CreatePriceRuleRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.ProductScope) || !Enum.IsDefined(request.PartyScope) || !Enum.IsDefined(request.ResultKind)) throw new PricingException("pricing.rule_invalid", "Rule scopes and result types must be supported values.");
        if (request.MinQuantity < 0m || request.MaxQuantity is { } max && max <= request.MinQuantity) throw new PricingException("pricing.quantity_range_invalid", "Quantity ranges must have a nonnegative minimum and a greater exclusive maximum.");
        if (!decimal.TryParse(request.ResultValue, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount < 0m || request.ResultKind == PricingResultKind.PercentageDiscount && amount > 100m) throw new PricingException("pricing.rule_result_invalid", "Rule value must be a valid nonnegative invariant decimal.");
        var hasProductScope = request.VariantId is not null || request.TemplateId is not null || request.CategoryId is not null || request.BrandId is not null;
        if ((request.ProductScope == PricingProductScope.AllProducts && hasProductScope) || (request.ProductScope != PricingProductScope.AllProducts && new[] { request.VariantId, request.TemplateId, request.CategoryId, request.BrandId }.Count(value => value is not null) != 1))
            throw new PricingException("pricing.rule_scope_invalid", "Choose exactly one identifier for the selected product scope.");
        if (request.ProductScope == PricingProductScope.Variant && request.VariantId is { } variant)
        {
            var product = await catalog.GetPricingContextAsync(tenant, variant, legalEntityId, cancellationToken) ?? throw new PricingException("pricing.variant_not_found", "An active product variant was not found in Catalog.");
            if (request.TemplateId is not null || product.ProductVariantId != variant) throw new PricingException("pricing.rule_scope_invalid", "Variant scope does not match Catalog identity.");
        }
        if (request.ProductScope == PricingProductScope.Template && await catalog.GetProductSummaryAsync(tenant, request.TemplateId!.Value, legalEntityId, cancellationToken) is null) throw new PricingException("pricing.template_not_found", "An active product template visible to this legal entity was not found.");
        if (request.ProductScope == PricingProductScope.Category && !await catalog.IsActiveCategoryAsync(tenant, request.CategoryId!.Value, cancellationToken)) throw new PricingException("pricing.category_not_found", "An active Catalog category was not found.");
        if (request.ProductScope == PricingProductScope.Brand && !await catalog.IsActiveBrandAsync(tenant, request.BrandId!.Value, cancellationToken)) throw new PricingException("pricing.brand_not_found", "An active Catalog brand was not found.");
        var validPartyScope = request.PartyScope switch
        {
            PricingPartyScope.SpecificParty => request.PartyId is not null && request.PriceGroupId is null,
            PricingPartyScope.PriceGroup => request.PriceGroupId is not null && request.PartyId is null,
            PricingPartyScope.AllParties => request.PartyId is null && request.PriceGroupId is null,
            _ => false
        };
        if (!validPartyScope) throw new PricingException("pricing.rule_party_scope_invalid", "Choose exactly one party, Price Group, or all parties scope.");
        if (request.PartyId is { } party && !await directory.IsActivePartyAsync(tenant, party, legalEntityId, cancellationToken)) throw new PricingException("pricing.party_not_found", "An active party was not found in Directory.");
        if (request.PriceGroupId is { } groupId && !(await repository.FindActiveGroupsAsync(tenant, [groupId], cancellationToken)).Contains(groupId)) throw new PricingException("pricing.group_not_found", "An active Price Group was not found.");
        await repository.AddRuleAsync(tenant, legalEntityId, actor, membership, listId, versionId, request, amount, correlationId, cancellationToken);
    }

    public async Task<PriceVersionDto> ActivateVersionAsync(TenantId tenant, Guid legalEntityId, Guid actor, Guid membership, Guid versionId, Guid correlationId, CancellationToken cancellationToken)
    {
        var timezone = await organization.GetTimeZoneAsync(tenant, new LegalEntityId(legalEntityId), cancellationToken) ?? throw new PricingException("pricing.legal_entity_not_found", "The legal entity was not found or has no business time zone.");
        DateOnly businessDate;
        try { businessDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(timezone)).DateTime); }
        catch (TimeZoneNotFoundException exception) { throw new PricingException("pricing.timezone_invalid", $"The legal entity time zone '{timezone}' is not recognized: {exception.Message}"); }
        return await repository.ActivateVersionAsync(tenant, legalEntityId, actor, membership, versionId, correlationId, businessDate, cancellationToken);
    }
    public Task<(int RuleCount, int EntryCount)> ValidateVersionAsync(TenantId tenant, Guid legalEntityId, Guid versionId, CancellationToken cancellationToken) => repository.ValidateVersionAsync(tenant, legalEntityId, versionId, cancellationToken);
    public Task ArchiveListAsync(TenantId tenant, Guid legalEntityId, Guid actor, Guid membership, Guid listId, long expectedVersion, Guid correlationId, CancellationToken cancellationToken) => repository.ArchiveListAsync(tenant, legalEntityId, actor, membership, listId, expectedVersion, correlationId, cancellationToken);
    public Task<Guid> CreateGroupAsync(TenantId tenant, Guid actor, Guid membership, CreatePriceGroupRequest request, Guid correlationId, CancellationToken cancellationToken) => repository.CreateGroupAsync(tenant, actor, membership, request, correlationId, cancellationToken);
    public async Task AddGroupMemberAsync(TenantId tenant, Guid actor, Guid membership, Guid groupId, AddPriceGroupMemberRequest request, Guid legalEntityId, Guid correlationId, CancellationToken cancellationToken)
    {
        if (!await directory.IsActivePartyAsync(tenant, request.PartyId, legalEntityId, cancellationToken)) throw new PricingException("pricing.party_not_found", "An active party visible to this legal entity was not found.");
        await repository.AddGroupMemberAsync(tenant, actor, membership, legalEntityId, groupId, request, correlationId, cancellationToken);
    }

    public async Task AssignListAsync(TenantId tenant, Guid actor, Guid membership, CreatePriceListAssignmentRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        if (request.PartyId is { } party && !await directory.IsActivePartyAsync(tenant, party, request.LegalEntityId, cancellationToken)) throw new PricingException("pricing.party_not_found", "An active party visible to this legal entity was not found.");
        if (request.PriceGroupId is { } groupId && !(await repository.FindActiveGroupsAsync(tenant, [groupId], cancellationToken)).Contains(groupId)) throw new PricingException("pricing.group_not_found", "An active Price Group was not found.");
        await repository.AssignListAsync(tenant, actor, membership, request, correlationId, cancellationToken);
    }

    public async Task<PricingImportPreviewDto> PreviewImportAsync(TenantId tenant, Guid actor, Guid membership, PricingImportRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        if (request.Csv.Length > 2_000_000) throw new PricingException("pricing.import_too_large", "CSV imports are limited to 2 MB.");
        var parsed = ParseCsv(request.Csv);
        if (parsed.Count is < 1 or > 5000) throw new PricingException("pricing.import_row_count_invalid", "CSV imports must contain between 1 and 5,000 data rows.");
        var headers = parsed[0].Select(value => value.Trim()).ToArray();
        var skuColumn = Header(headers, "SKU"); var uomColumn = Header(headers, "UOM"); var priceColumn = Header(headers, "Price"); if (priceColumn < 0) priceColumn = Header(headers, "Value"); var currencyColumn = Header(headers, "Currency");
        if (skuColumn < 0 || uomColumn < 0 || priceColumn < 0 || currencyColumn < 0) throw new PricingException("pricing.import_header_invalid", "CSV must include SKU, UOM, Price, and Currency columns.");
        var partyColumn = Header(headers, "PartyId"); var groupColumn = Header(headers, "PriceGroupId"); var minimumColumn = Header(headers, "MinQty"); var maximumColumn = Header(headers, "MaxQty"); var fromColumn = Header(headers, "ValidFrom"); var toColumn = Header(headers, "ValidTo"); var priorityColumn = Header(headers, "Priority"); var ruleColumn = Header(headers, "RuleType");
        var list = (await repository.ListAsync(tenant, request.LegalEntityId, cancellationToken)).FirstOrDefault(item => item.Id == request.PriceListId) ?? throw new PricingException("pricing.import_target_not_found", "The target price list was not found.");
        var version = (await repository.ListVersionsAsync(tenant, request.LegalEntityId, request.PriceListId, cancellationToken)).FirstOrDefault(item => item.Id == request.TargetVersionId) ?? throw new PricingException("pricing.import_target_not_found", "The target version was not found.");
        if (version.Status != "Draft") throw new PricingException("pricing.import_requires_draft", "Imports can only target a draft version.");
        var dataRows = parsed.Skip(1).ToArray();
        var skus = dataRows.Where(row => row.Length > skuColumn).Select(row => row[skuColumn]).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var variants = skus.Length == 0 ? new Dictionary<string, CatalogVariantIdentity>() : await catalog.ResolveVariantsBySkuAsync(tenant, skus, request.LegalEntityId, cancellationToken);
        var importDate = clock.GetUtcNow();
        var importVariantIds = variants.Values.Select(item => item.ProductVariantId).Distinct().ToArray();
        var allowedUoms = importVariantIds.Length == 0
            ? new Dictionary<Guid, IReadOnlyList<CatalogAllowedUom>>()
            : await catalog.GetAllowedUomsBatchAsync(tenant, importVariantIds, importDate, request.LegalEntityId, cancellationToken);
        var partyIds = dataRows.Select(row => ParseOptionalGuid(Cell(row, partyColumn))).Where(value => value is not null).Select(value => value!.Value).Distinct().ToArray();
        var activeParties = await directory.FindActivePartiesAsync(tenant, partyIds, request.LegalEntityId, cancellationToken);
        var groupIds = dataRows.Select(row => ParseOptionalGuid(Cell(row, groupColumn))).Where(value => value is not null).Select(value => value!.Value).Distinct().ToArray();
        var activeGroups = await repository.FindActiveGroupsAsync(tenant, groupIds, cancellationToken);
        await financeBase.ConvertCommercialUnitPriceAsync(tenant, new LegalEntityId(request.LegalEntityId), "1", list.CurrencyCode, list.CurrencyCode, list.Direction == "Buying" ? FinancialCalculationPurpose.Purchase : FinancialCalculationPurpose.Sales, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), cancellationToken);
        var rows = new List<PricingImportRowDto>(dataRows.Length);
        for (var index = 0; index < dataRows.Length; index++)
        {
            var row = dataRows[index]; var lineNumber = index + 2; var sku = Cell(row, skuColumn).Trim(); var uomCode = Cell(row, uomColumn).Trim(); var currency = Cell(row, currencyColumn).Trim().ToUpperInvariant();
            string? code = null; string? message = null; Guid? variantId = null; Guid? uomId = null; string? amountText = null;
            var partyId = ParseOptionalGuid(Cell(row, partyColumn)); var priceGroupId = ParseOptionalGuid(Cell(row, groupColumn));
            var minQuantity = ParseOptionalDecimal(Cell(row, minimumColumn), 0m, out var minValid); var maxQuantity = ParseOptionalNullableDecimal(Cell(row, maximumColumn), out var maxValid);
            var validFrom = ParseOptionalDate(Cell(row, fromColumn), out var fromValid); var validTo = ParseOptionalDate(Cell(row, toColumn), out var toValid);
            var priorityText = Cell(row, priorityColumn); var priority = string.IsNullOrWhiteSpace(priorityText) ? 0 : int.TryParse(priorityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPriority) ? parsedPriority : int.MinValue;
            var ruleType = Cell(row, ruleColumn).Trim(); if (ruleType.Length == 0) ruleType = string.Empty;
            if (row.Length <= Math.Max(Math.Max(skuColumn, uomColumn), Math.Max(priceColumn, currencyColumn))) { code = "pricing.import_columns_invalid"; message = "The row does not contain all required columns."; }
            else if (string.IsNullOrWhiteSpace(sku)) { code = "pricing.import_sku_required"; message = "SKU is required."; }
            else
            {
                var normalizedSku = sku.Trim();
                if (!variants.TryGetValue(normalizedSku, out var product)) { code = "pricing.import_unknown_sku"; message = "SKU was not found in active Catalog products."; }
                else
                {
                    variantId = product.ProductVariantId;
                    var allowed = allowedUoms.GetValueOrDefault(product.ProductVariantId, Array.Empty<CatalogAllowedUom>());
                    var unit = allowed.FirstOrDefault(item => string.Equals(item.UnitCode, uomCode, StringComparison.OrdinalIgnoreCase));
                    if (unit is null) { code = "pricing.import_unknown_uom"; message = "UOM is not valid for this product."; }
                    else uomId = unit.UnitId;
                    if (code is null && partyId is not null && !activeParties.Contains(partyId.Value)) { code = "pricing.import_unknown_party"; message = "Party was not found or is not visible to this legal entity."; }
                    if (code is null && priceGroupId is not null && !activeGroups.Contains(priceGroupId.Value)) { code = "pricing.import_unknown_group"; message = "Price Group was not found or is inactive."; }
                    if (code is null && partyId is not null && priceGroupId is not null) { code = "pricing.import_party_scope_invalid"; message = "Choose a party or a Price Group, not both."; }
                    if (code is null && !string.Equals(currency, list.CurrencyCode, StringComparison.Ordinal)) { code = "pricing.import_currency_mismatch"; message = "Currency must match the target price list."; }
                    if (code is null && (!minValid || !maxValid || !fromValid || !toValid || priority == int.MinValue || minQuantity < 0m || maxQuantity is { } max && max <= minQuantity || validFrom is { } start && validTo is { } end && end <= start)) { code = "pricing.import_range_invalid"; message = "Quantity, date, or priority fields are invalid."; }
                    if (code is null && ruleType.Length > 0 && !Enum.TryParse<PricingResultKind>(ruleType, true, out _)) { code = "pricing.import_rule_type_invalid"; message = "RuleType must be FixedUnitPrice, PercentageDiscount, or FixedDiscountPerUnit."; }
                    if (code is null && (!decimal.TryParse(Cell(row, priceColumn), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount < 0m || ruleType.Equals(nameof(PricingResultKind.PercentageDiscount), StringComparison.OrdinalIgnoreCase) && amount > 100m)) { code = "pricing.import_price_invalid"; message = "Price or rule value must be a valid nonnegative invariant decimal."; }
                    else if (code is null) amountText = decimal.Parse(Cell(row, priceColumn), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture).ToString("0.############################", CultureInfo.InvariantCulture);
                }
            }
            rows.Add(new PricingImportRowDto(lineNumber, sku, variantId, uomId, amountText, currency.Length == 3 ? currency : null, code, message, partyId, priceGroupId, minQuantity, maxQuantity, validFrom, validTo, priority == int.MinValue ? 0 : priority, ruleType.Length == 0 ? null : Enum.TryParse<PricingResultKind>(ruleType, true, out var parsedKind) ? parsedKind.ToString() : ruleType));
        }
        var duplicateKeys = rows.Where(row => row.ErrorCode is null).GroupBy(row => (row.VariantId, row.UomId, row.PartyId, row.PriceGroupId, row.MinQuantity, row.MaxQuantity, row.ValidFrom, row.ValidTo, row.Priority, row.RuleType)).Where(group => group.Count() > 1).SelectMany(group => group).Select(row => row.RowNumber).ToHashSet();
        rows = rows.Select(row => duplicateKeys.Contains(row.RowNumber) ? row with { ErrorCode = "pricing.import_duplicate_line", ErrorMessage = "This variant, unit, customer scope, quantity range, and date range appear more than once." } : row).ToList();
        var conflictRows = rows.Where(row => row.ErrorCode is null).Select(row => new PricingRuleCandidate(
            GuidFromRow(row.RowNumber), PricingProductScope.Variant, row.VariantId, null, null, 0, null,
            row.PartyId is not null ? PricingPartyScope.SpecificParty : row.PriceGroupId is not null ? PricingPartyScope.PriceGroup : PricingPartyScope.AllParties,
            row.PartyId, row.PriceGroupId, row.UomId, row.MinQuantity, row.MaxQuantity, row.ValidFrom, row.ValidTo, row.Priority,
            row.RuleType is null ? PricingResultKind.FixedUnitPrice : Enum.Parse<PricingResultKind>(row.RuleType), decimal.Parse(row.UnitPrice!, CultureInfo.InvariantCulture), row.RuleType is null)).ToArray();
        var conflictingIds = PricingResolver.FindConflicts(conflictRows).Select(rule => rule.Id).ToHashSet();
        rows = rows.Select(row => conflictingIds.Contains(GuidFromRow(row.RowNumber)) ? row with { ErrorCode = "pricing.import_overlap_conflict", ErrorMessage = "Overlapping rows have the same priority and scope but different results." } : row).ToList();
        return await repository.SaveImportPreviewAsync(tenant, actor, membership, request, rows, correlationId, cancellationToken);
    }

    public Task<PricingImportCommitDto> CommitImportAsync(TenantId tenant, Guid actor, Guid membership, Guid batchId, Guid correlationId, CancellationToken cancellationToken) => repository.CommitImportAsync(tenant, actor, membership, batchId, correlationId, cancellationToken);
    public async Task<string> ExportEntriesCsvAsync(TenantId tenant, Guid legalEntityId, Guid listId, Guid versionId, CancellationToken cancellationToken)
    {
        var rows = await repository.ListExportRowsAsync(tenant, legalEntityId, listId, versionId, cancellationToken);
        var variantIds = rows.Where(row => row.VariantId.HasValue).Select(row => row.VariantId!.Value).Distinct().ToArray();
        var variants = new Dictionary<Guid, CatalogVariantIdentity>();
        var uoms = new Dictionary<(Guid VariantId, Guid UomId), string>();
        foreach (var batch in variantIds.Chunk(500))
        {
            var identities = await catalog.FindVariantsBatchAsync(tenant, batch, legalEntityId, cancellationToken);
            foreach (var pair in identities) variants[pair.Key] = pair.Value;
            var allowed = await catalog.GetAllowedUomsBatchAsync(tenant, batch, DateTimeOffset.UtcNow, legalEntityId, cancellationToken);
            foreach (var item in allowed.SelectMany(pair => pair.Value.Select(unit => (VariantId: pair.Key, Unit: unit)))) uoms[(item.VariantId, item.Unit.UnitId)] = item.Unit.UnitCode;
        }
        static string Csv(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var output = new System.Text.StringBuilder("ProductScope,SKU,Product,Unit,PartyScope,MinQty,MaxQty,ValidFrom,ValidTo,Priority,RuleType,Value,Status\n");
        foreach (var row in rows)
        {
            variants.TryGetValue(row.VariantId ?? Guid.Empty, out var variant);
            var sku = variant?.Sku ?? string.Empty;
            var name = variant?.DisplayName ?? (row.ProductScope == "Variant" ? string.Empty : row.ProductScope);
            var unit = row.VariantId is { } variantId && row.UomId is { } uomId && uoms.TryGetValue((variantId, uomId), out var code) ? code : string.Empty;
            output.AppendJoin(',', Csv(row.ProductScope), Csv(sku), Csv(name), Csv(unit), Csv(row.PartyScope), Csv(row.MinQuantity.ToString(CultureInfo.InvariantCulture)), Csv(row.MaxQuantity?.ToString(CultureInfo.InvariantCulture)), Csv(row.ValidFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), Csv(row.ValidTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), Csv(row.Priority.ToString(CultureInfo.InvariantCulture)), Csv(row.ResultKind), Csv(row.ResultValue.ToString(CultureInfo.InvariantCulture)), Csv(row.Status)).Append('\n');
        }
        return output.ToString();
    }

    public async Task<PriceCoverageDto> GetCoverageAsync(TenantId tenant, Guid legalEntityId, Guid listId, DateOnly pricingDate, CancellationToken cancellationToken)
    {
        var list = (await repository.ListAsync(tenant, legalEntityId, cancellationToken)).FirstOrDefault(item => item.Id == listId) ?? throw new PricingException("pricing.price_list_not_found", "An active price list was not found for this legal entity.");
        var (listContext, version) = await repository.ResolveListAndVersionAsync(tenant, new PricingContext(tenant, new LegalEntityId(legalEntityId), Enum.Parse<PricingDirection>(list.Direction), pricingDate, list.CurrencyCode, listId, null, null, Guid.Empty, Guid.Empty, [], null, Guid.Empty, 1m), cancellationToken);
        var configuredCounts = await repository.GetCoverageCountsAsync(tenant, legalEntityId, listId, version.Id, cancellationToken);
        const int pageSize = 100;
        var firstPage = await catalog.SearchAsync(tenant, null, null, legalEntityId, 1, pageSize, cancellationToken);
        if (firstPage.TotalCount > 5000) throw new PricingException("pricing.coverage_bound_exceeded", "Coverage analysis is bounded to 5,000 active product variants per request.");
        var missing = 0; var explicitZero = 0;
        for (var page = 1; page <= Math.Max(1, (firstPage.TotalCount + pageSize - 1) / pageSize); page++)
        {
            var items = page == 1 ? firstPage : await catalog.SearchAsync(tenant, null, null, legalEntityId, page, pageSize, cancellationToken);
            foreach (var chunk in items.Items.Chunk(500))
            {
                if (chunk.Length == 0) continue;
                var ids = chunk.Select(item => item.ProductVariantId).Distinct().ToArray();
                var productContexts = await catalog.GetPricingContextsBatchAsync(tenant, ids, legalEntityId, cancellationToken);
                var baseUnits = await catalog.GetBaseUomsBatchAsync(tenant, ids, DateTimeOffset.UtcNow, legalEntityId, cancellationToken);
                var contexts = chunk.Where(item => productContexts.ContainsKey(item.ProductVariantId) && baseUnits.ContainsKey(item.ProductVariantId)).Select(item =>
                {
                    var product = productContexts[item.ProductVariantId];
                    return new PricingContext(tenant, new LegalEntityId(legalEntityId), Enum.Parse<PricingDirection>(list.Direction), pricingDate, list.CurrencyCode, listId, null, null, item.ProductVariantId, product.ProductTemplateId, product.CategoryPath, product.BrandId, baseUnits[item.ProductVariantId], 1m);
                }).ToArray();
                if (contexts.Length == 0) continue;
                var rules = await repository.GetCandidatesAsync(tenant, version.Id, contexts, cancellationToken);
                foreach (var context in contexts)
                {
                    var result = PricingResolver.Resolve(context, listContext, version, rules);
                    if (result.Status == "PriceMissing") missing++;
                    else if (result.Snapshot?.FinalUnitPrice == "0") explicitZero++;
                }
            }
        }
        return new(firstPage.TotalCount, configuredCounts.Rules, configuredCounts.Entries, explicitZero, missing);
    }

    private static int Header(IReadOnlyList<string> headers, string expected) => headers.Select((header, index) => (header, index)).Where(item => string.Equals(item.header, expected, StringComparison.OrdinalIgnoreCase)).Select(item => item.index).DefaultIfEmpty(-1).First();
    private static Guid GuidFromRow(int rowNumber) => Guid.Parse($"80000000-0000-0000-0000-{rowNumber:D12}");
    private static string Cell(string[] row, int index) => index >= 0 && row.Length > index ? row[index] : string.Empty;
    private static Guid? ParseOptionalGuid(string value) => string.IsNullOrWhiteSpace(value) ? null : Guid.TryParse(value.Trim(), out var result) ? result : Guid.Empty;
    private static decimal ParseOptionalDecimal(string value, decimal fallback, out bool valid) { if (string.IsNullOrWhiteSpace(value)) { valid = true; return fallback; } valid = decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result); return valid ? result : fallback; }
    private static decimal? ParseOptionalNullableDecimal(string value, out bool valid) { if (string.IsNullOrWhiteSpace(value)) { valid = true; return null; } valid = decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result); return valid ? result : null; }
    private static DateOnly? ParseOptionalDate(string value, out bool valid) { if (string.IsNullOrWhiteSpace(value)) { valid = true; return null; } valid = DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var result); return valid ? result : null; }
    private static List<string[]> ParseCsv(string csv)
    {
        var records = new List<string[]>(); var fields = new List<string>(); var value = new System.Text.StringBuilder(); var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (quoted && character == '"' && index + 1 < csv.Length && csv[index + 1] == '"') { value.Append('"'); index++; }
            else if (character == '"') quoted = !quoted;
            else if (!quoted && character == ',') { fields.Add(value.ToString()); value.Clear(); }
            else if (!quoted && (character == '\n' || character == '\r')) { if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n') index++; fields.Add(value.ToString()); value.Clear(); if (fields.Any(item => item.Length > 0)) records.Add(fields.ToArray()); fields.Clear(); }
            else value.Append(character);
        }
        if (quoted) throw new PricingException("pricing.import_csv_invalid", "CSV contains an unclosed quoted value.");
        fields.Add(value.ToString()); if (fields.Any(item => item.Length > 0)) records.Add(fields.ToArray()); return records;
    }

    public async Task<PriceResolution> ResolveAsync(PricingContext context, CancellationToken cancellationToken = default)
        => (await ResolveBatchAsync([context], cancellationToken))[0];

    public async Task<IReadOnlyList<PriceResolution>> ResolveBatchAsync(IReadOnlyList<PricingContext> contexts, CancellationToken cancellationToken = default)
    {
        if (contexts.Count is < 1 or > 500) throw new PricingException("pricing.batch_size_invalid", "Resolve batches must contain between 1 and 500 lines.");
        var tenant = contexts[0].TenantId;
        if (contexts.Any(context => context.TenantId != tenant)) throw new PricingException("pricing.batch_tenant_mismatch", "A Pricing batch must contain one tenant.");
        var output = new PriceResolution[contexts.Count];
        var contextsByEntity = contexts.Select((context, index) => (context, index)).GroupBy(item => item.context.LegalEntityId);
        foreach (var entityGroup in contextsByEntity)
        {
            var entityId = entityGroup.Key.Value;
            var materialized = entityGroup.ToArray();
            var policy = await repository.GetPolicyAsync(tenant, entityId, cancellationToken);
            var products = await catalog.GetPricingContextsBatchAsync(tenant, materialized.Select(item => item.context.ProductVariantId).Distinct().ToArray(), entityId, cancellationToken);
            if (materialized.Any(item => !products.ContainsKey(item.context.ProductVariantId))) throw new PricingException("pricing.variant_not_found", "An active product variant is not visible to this legal entity.");
            var partyIds = materialized.Where(item => item.context.PartyId is not null).Select(item => item.context.PartyId!.Value).Distinct().ToArray();
            var activeParties = await directory.FindActivePartiesAsync(tenant, partyIds, entityId, cancellationToken);
            if (partyIds.Any(id => !activeParties.Contains(id))) throw new PricingException("pricing.party_not_found", "An active party is not visible to this legal entity.");
            foreach (var dateGroup in materialized.GroupBy(item => item.context.PricingDate))
            {
                var allowedUoms = await catalog.GetAllowedUomsBatchAsync(tenant, dateGroup.Select(item => item.context.ProductVariantId).Distinct().ToArray(), dateGroup.First().context.PricingDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), entityId, cancellationToken);
                var partyGroups = await repository.ResolvePartyGroupsAsync(tenant, partyIds, dateGroup.Key, cancellationToken);
                if (dateGroup.Any(item => !allowedUoms.TryGetValue(item.context.ProductVariantId, out var units) || !units.Any(unit => unit.UnitId == item.context.UnitOfMeasureId))) throw new PricingException("pricing.uom_not_allowed", "The unit is not valid for this product at the pricing date.");
                var hydrated = dateGroup.Select(item =>
                {
                    var product = products[item.context.ProductVariantId];
                    Guid? groupId = item.context.PartyId is { } party && partyGroups.TryGetValue(party, out var assigned) ? assigned : null;
                    var context = item.context with { ProductTemplateId = product.ProductTemplateId, CategoryPath = product.CategoryPath, BrandId = product.BrandId, PriceGroupId = groupId, TransactionCurrencyCode = CurrencyCode.Normalize(item.context.TransactionCurrencyCode) };
                    return (context, item.index);
                }).ToArray();
                foreach (var selectionGroup in hydrated.GroupBy(item => (item.context.Direction, item.context.PriceListId, item.context.PartyId, item.context.PriceGroupId, item.context.TransactionCurrencyCode)))
                {
                    var first = selectionGroup.First().context;
                    var (list, version) = await repository.ResolveListAndVersionAsync(tenant, first, cancellationToken);
                    if (!string.Equals(list.CurrencyCode, first.TransactionCurrencyCode, StringComparison.Ordinal) && (list.CurrencyPolicy != PricingCurrencyPolicy.AllowFinBaseConversion || !Enum.TryParse<PricingCurrencyPolicy>(policy.CurrencyPolicy, out var tenantCurrencyPolicy) || tenantCurrencyPolicy != PricingCurrencyPolicy.AllowFinBaseConversion)) throw new PricingException("pricing.currency_mismatch", "This price list and legal entity require an exact transaction currency.");
                    var selectedContexts = selectionGroup.Select(item => item.context).ToArray();
                    var candidateContexts = selectedContexts.ToList();
                    if (policy.AllowUomDerivedPrice)
                        foreach (var context in selectedContexts)
                            if (allowedUoms.TryGetValue(context.ProductVariantId, out var units) && units.FirstOrDefault(unit => unit.IsBase) is { } baseUnit && baseUnit.UnitId != context.UnitOfMeasureId)
                                candidateContexts.Add(context with { UnitOfMeasureId = baseUnit.UnitId });
                    var rules = await repository.GetCandidatesAsync(tenant, version.Id, candidateContexts, cancellationToken);
                    decimal conversionFactor = 1m; string? fxSnapshot = null;
                    if (!string.Equals(list.CurrencyCode, first.TransactionCurrencyCode, StringComparison.Ordinal))
                    {
                        var conversion = await financeBase.ConvertCommercialUnitPriceAsync(tenant, first.LegalEntityId, "1", list.CurrencyCode, first.TransactionCurrencyCode, first.Direction == PricingDirection.Buying ? FinancialCalculationPurpose.Purchase : FinancialCalculationPurpose.Sales, first.PricingDate, cancellationToken);
                        if (!decimal.TryParse(conversion.TargetAmount, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out conversionFactor) || conversionFactor <= 0m) throw new PricingException("pricing.fx_invalid", "FinBase returned an invalid commercial conversion factor.");
                        fxSnapshot = conversion.ExchangeRate is null ? null : JsonSerializer.Serialize(conversion.ExchangeRate);
                    }
                    foreach (var item in selectionGroup)
                    {
                        var candidates = rules;
                        if (policy.AllowUomDerivedPrice && allowedUoms.TryGetValue(item.context.ProductVariantId, out var units) && units.FirstOrDefault(unit => unit.IsBase) is { } baseUnit && baseUnit.UnitId != item.context.UnitOfMeasureId)
                        {
                            var requestedUnit = units.FirstOrDefault(unit => unit.UnitId == item.context.UnitOfMeasureId);
                            if (requestedUnit is null) throw new PricingException("pricing.uom_not_allowed", "The unit is not valid for this product at the pricing date.");
                            var factor = requestedUnit.FactorToBase / baseUnit.FactorToBase;
                            candidates = rules.SelectMany(rule => rule.UomId == baseUnit.UnitId ? new[] { rule with { UomId = item.context.UnitOfMeasureId, ResultValue = rule.ResultKind == PricingResultKind.PercentageDiscount ? rule.ResultValue : rule.ResultValue * factor, IsDerivedUom = true } } : [rule]).ToArray();
                        }
                        output[item.index] = PricingResolver.Resolve(item.context, list, version, candidates, conversionFactor, fxSnapshot);
                    }
                }
            }
        }
        return output;
    }
}
