using System.Globalization;
using System.Text.Json;
using Npgsql;
using Modules.FinanceBase.DTOs;
using Modules.FinanceBase.Entities;
using Modules.FinanceBase.Services;
using Core.Services;
using Core.Transactions;
using Shared.Primitives.Financial;
using Shared.Primitives.Identifiers;

namespace Modules.FinanceBase.Repositories;

public sealed class FinanceBaseRepository(NpgsqlDataSource dataSource, ITransactionCoordinator coordinator, ILegalEntityFinancialReadStore organization, TimeProvider clock)
{
    private readonly FinanceBaseLookupCache lookupCache = new();
    public async Task<IReadOnlyList<CurrencyDto>> ListCurrenciesAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("select code,numeric_code,name_ar,name_en,minor_unit,status,dataset_version from foundation_finbase.currency_reference where status='Active' order by code", connection);
        await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<CurrencyDto>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetString(0), reader.GetInt16(1), reader.GetString(2), reader.GetString(3), reader.GetInt16(4), reader.GetString(5), reader.GetString(6)));
        return result;
    }

    public async Task<IReadOnlyList<EntityCurrencyDto>> ListEntityCurrenciesAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        return await WithTenantAsync(tenant, async (connection, token) =>
        {
            await using var command = new NpgsqlCommand("select legal_entity_id,currency_code,enabled,allow_sales,allow_purchase,allow_payment,allow_manual_rate,status,version from foundation_finbase.legal_entity_currency where tenant_id=$1 and legal_entity_id=$2 order by currency_code", connection);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); await using var reader = await command.ExecuteReaderAsync(token); var result = new List<EntityCurrencyDto>();
            while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5), reader.GetBoolean(6), reader.GetString(7), reader.GetInt64(8))); return result;
        }, ct);
    }

    public async Task<EntityCurrencyDto> EnableCurrencyAsync(TenantId tenant, Guid actor, Guid membership, LegalEntityId entity, string code, EnableCurrencyRequest request, Guid correlation, CancellationToken ct)
    {
        var currency = CurrencyCode.Normalize(code); _ = await organization.GetBaseCurrencyCodeAsync(tenant, entity, ct) ?? throw new FinanceBaseException("finbase.legal_entity_not_found", "Legal entity was not found.");
        if (!await CurrencyExistsAsync(currency, ct)) throw new FinanceBaseException("finbase.currency_not_found", "Currency is not in the controlled reference dataset.");
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            const string sql = "insert into foundation_finbase.legal_entity_currency(tenant_id,legal_entity_id,currency_code,enabled,allow_sales,allow_purchase,allow_payment,allow_manual_rate,status,created_at,updated_at) values($1,$2,$3,true,$4,$5,$6,$7,'Active',$8,$8) on conflict(tenant_id,legal_entity_id,currency_code) do update set enabled=true,allow_sales=excluded.allow_sales,allow_purchase=excluded.allow_purchase,allow_payment=excluded.allow_payment,allow_manual_rate=excluded.allow_manual_rate,status='Active',updated_at=excluded.updated_at,version=foundation_finbase.legal_entity_currency.version+1 returning version";
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, tenant.Value, entity.Value, currency, request.AllowSales, request.AllowPurchase, request.AllowPayment, request.AllowManualRate, now); var version = Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, entity.Value, null, "User", actor, membership, "finbase", "currency.enabled", "legal_entity_currency", id, null, JsonSerializer.Serialize(request), null, "api", correlation, null, now, null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "FinBaseCurrencyEnabledV1", JsonSerializer.Serialize(new { entity = entity.Value, currency }), tenant, now, correlation, correlation, "finbase"));
            return new EntityCurrencyDto(entity.Value, currency, true, request.AllowSales, request.AllowPurchase, request.AllowPayment, request.AllowManualRate, "Active", version);
        }, ct);
    }

    public async Task<IReadOnlyList<ExchangeRateDto>> ListRatesAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => await WithTenantAsync(tenant, async (connection, token) =>
    {
        await using var command = new NpgsqlCommand("select id,legal_entity_id,from_currency,to_currency,rate,purpose,effective_date,source_type,provider_reference,status,version from foundation_finbase.exchange_rate where tenant_id=$1 and legal_entity_id=$2 order by effective_date desc,id desc limit 200", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); await using var reader = await command.ExecuteReaderAsync(token); var result = new List<ExchangeRateDto>();
        while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4).ToString("0.############", CultureInfo.InvariantCulture), Enum.Parse<ExchangeRatePurpose>(reader.GetString(5)), DateOnly.FromDateTime(reader.GetDateTime(6)), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), Enum.Parse<ExchangeRateStatus>(reader.GetString(9)), reader.GetInt64(10))); return result;
    }, ct);

    public async Task<IReadOnlyList<ExchangeRateSourceConfigDto>> ListRateSourceConfigsAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => await WithTenantAsync(tenant, async (connection, token) =>
    {
        await using var command = new NpgsqlCommand("select id,legal_entity_id,source_code,from_currency,to_currency,purpose,priority,enabled,credential_reference,sync_interval_seconds,failure_count,version from foundation_finbase.exchange_rate_source_config where tenant_id=$1 and legal_entity_id=$2 order by priority,source_code,id", connection); Add(command, tenant.Value, entity.Value); await using var reader = await command.ExecuteReaderAsync(token); var result = new List<ExchangeRateSourceConfigDto>();
        while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), Enum.Parse<ExchangeRatePurpose>(reader.GetString(5)), reader.GetInt16(6), reader.GetBoolean(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetInt32(9), reader.GetInt32(10), reader.GetInt64(11)));
        return result;
    }, ct);

    public async Task<ExchangeRateSourceConfigDto> CreateRateSourceConfigAsync(TenantId tenant, Guid actor, Guid membership, LegalEntityId entity, CreateExchangeRateSourceConfigRequest request, Guid correlation, CancellationToken ct)
    {
        var sourceCode = request.SourceCode.Trim(); var from = CurrencyCode.Normalize(request.FromCurrency); var to = CurrencyCode.Normalize(request.ToCurrency);
        var credentialReference = request.CredentialReference?.Trim();
        if (sourceCode.Length is < 2 or > 64 || sourceCode[0] is < 'a' or > 'z' || sourceCode.Any(character => !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')) ||
            from == to || !Enum.IsDefined(request.Purpose) || request.Priority is < 1 or > 1000 || request.SyncIntervalSeconds is < 60 or > 86400 ||
            credentialReference is { Length: > 200 } || credentialReference is not null && !IsCredentialReference(credentialReference))
            throw new FinanceBaseException("finbase.provider_config_invalid", "Provider source configuration is invalid; credentials must be references, never secret values.");
        var baseCurrency = await organization.GetBaseCurrencyCodeAsync(tenant, entity, ct) ?? throw new FinanceBaseException("finbase.legal_entity_not_found", "Legal entity was not found.");
        await EnsureEntityCurrencyEnabledAsync(tenant, entity, baseCurrency, from, ct); await EnsureEntityCurrencyEnabledAsync(tenant, entity, baseCurrency, to, ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            const string sql = "insert into foundation_finbase.exchange_rate_source_config(tenant_id,id,legal_entity_id,source_code,from_currency,to_currency,purpose,priority,enabled,credential_reference,sync_interval_seconds) values($1,$2,$3,$4,$5,$6,$7,$8,true,$9,$10) returning version";
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, tenant.Value, id, entity.Value, sourceCode, from, to, request.Purpose.ToString(), request.Priority, credentialReference ?? (object)DBNull.Value, request.SyncIntervalSeconds); var version = Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, entity.Value, null, "User", actor, membership, "finbase", "rate_source.created", "exchange_rate_source_config", id, null, JsonSerializer.Serialize(request), null, "api", correlation, null, now, null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "FinBaseRateSourceConfiguredV1", JsonSerializer.Serialize(new { id, entityId = entity.Value, sourceCode }), tenant, now, correlation, correlation, "finbase"));
            return new ExchangeRateSourceConfigDto(id, entity.Value, sourceCode, from, to, request.Purpose, request.Priority, true, credentialReference, request.SyncIntervalSeconds, 0, version);
        }, ct);
    }

    public async Task<IReadOnlyList<ExchangeRateSourceConfig>> ClaimDueRateSourcesAsync(int limit, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("select tenant_id,id,legal_entity_id,source_code,from_currency,to_currency,purpose,priority,credential_reference,sync_interval_seconds,failure_count from foundation_finbase.claim_due_exchange_rate_sources($1)", connection); command.Parameters.AddWithValue(Math.Clamp(limit, 1, 100)); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<ExchangeRateSourceConfig>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(1), new(reader.GetGuid(0)), new(reader.GetGuid(2)), reader.GetString(3), reader.GetString(4), reader.GetString(5), Enum.Parse<ExchangeRatePurpose>(reader.GetString(6)), reader.GetInt16(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetInt32(9), reader.GetInt32(10)));
        return result;
    }

    public async Task<bool> PersistProviderCandidateAsync(ExchangeRateSourceConfig source, ExchangeRateProviderQuote quote, CancellationToken ct)
    {
        var from = CurrencyCode.Normalize(quote.FromCurrency); var to = CurrencyCode.Normalize(quote.ToCurrency);
        if (quote.Rate <= 0m || from != source.FromCurrency || to != source.ToCurrency || quote.Purpose != source.Purpose || string.IsNullOrWhiteSpace(quote.EventKey) || quote.EventKey.Length > 200 || quote.EffectiveDate > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            throw new FinanceBaseException("finbase.provider_quote_invalid", "Provider returned a quote that does not match its configured source or valid business date.");
        var now = clock.GetUtcNow(); var id = Guid.CreateVersion7(); var correlation = Guid.CreateVersion7();
        return await coordinator.ExecuteAsync(source.TenantId, async (context, token) =>
        {
            const string sql = "insert into foundation_finbase.exchange_rate(tenant_id,id,legal_entity_id,from_currency,to_currency,rate,purpose,effective_date,source_type,provider_reference,provider_event_key,status,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,'Provider',$9,$10,'Draft',$11) on conflict(tenant_id,legal_entity_id,provider_event_key) where provider_event_key is not null do nothing returning id";
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, source.TenantId.Value, id, source.LegalEntityId.Value, from, to, quote.Rate, quote.Purpose.ToString(), quote.EffectiveDate, quote.ProviderReference ?? source.SourceCode, quote.EventKey, now); var inserted = await command.ExecuteScalarAsync(token);
            if (inserted is null) return false;
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), source.TenantId, source.LegalEntityId.Value, null, "System", null, null, "finbase", "rate.candidate_imported", "exchange_rate", id, null, JsonSerializer.Serialize(new { source = source.SourceCode, quote.EventKey, quote.FromCurrency, quote.ToCurrency, quote.Rate, quote.EffectiveDate }), null, "provider", correlation, null, now, null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "FinBaseRateCandidateImportedV1", JsonSerializer.Serialize(new { id, legalEntityId = source.LegalEntityId.Value, sourceCode = source.SourceCode }), source.TenantId, now, correlation, correlation, "finbase"));
            return true;
        }, ct);
    }

    public async Task RecordRateSourceFailureAsync(ExchangeRateSourceConfig source, string errorCode, CancellationToken ct, TimeSpan? retryDelay = null)
    {
        await WithTenantAsync(source.TenantId, async (connection, token) =>
        {
            await using var command = new NpgsqlCommand("update foundation_finbase.exchange_rate_source_config set failure_count=failure_count+1,last_error_code=$4,next_attempt_at=$5,version=version+1 where tenant_id=$1 and id=$2 and legal_entity_id=$3 and enabled", connection); Add(command, source.TenantId.Value, source.Id, source.LegalEntityId.Value, errorCode, clock.GetUtcNow().Add(retryDelay ?? TimeSpan.FromSeconds(30))); await command.ExecuteNonQueryAsync(token); return true;
        }, ct);
    }

    public async Task RecordRateSourceSuccessAsync(ExchangeRateSourceConfig source, CancellationToken ct)
    {
        await WithTenantAsync(source.TenantId, async (connection, token) =>
        {
            var now = clock.GetUtcNow(); await using var command = new NpgsqlCommand("update foundation_finbase.exchange_rate_source_config set failure_count=0,last_error_code=null,last_synced_at=$4,next_attempt_at=$5,version=version+1 where tenant_id=$1 and id=$2 and legal_entity_id=$3 and enabled", connection); Add(command, source.TenantId.Value, source.Id, source.LegalEntityId.Value, now, now.AddSeconds(source.SyncIntervalSeconds)); await command.ExecuteNonQueryAsync(token); return true;
        }, ct);
    }

    public async Task<IReadOnlyList<PaymentTermListItemDto>> ListPaymentTermsAsync(TenantId tenant, bool includeArchived, CancellationToken ct) => await WithTenantAsync(tenant, async (connection, token) =>
    {
        const string sql = "select t.id,t.code,t.display_name_ar,t.display_name_en,t.status,t.version,v.version_number,v.effective_from from foundation_finbase.payment_term t left join lateral (select version_number,effective_from from foundation_finbase.payment_term_version where tenant_id=t.tenant_id and payment_term_id=t.id and status='Active' order by version_number desc limit 1) v on true where t.tenant_id=$1 and ($2 or t.status='Active') order by t.code,t.id limit 100";
        await using var command = new NpgsqlCommand(sql, connection); Add(command, tenant.Value, includeArchived); await using var reader = await command.ExecuteReaderAsync(token); var result = new List<PaymentTermListItemDto>();
        while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt64(5), reader.IsDBNull(6) ? null : reader.GetInt32(6), reader.IsDBNull(7) ? null : DateOnly.FromDateTime(reader.GetDateTime(7))));
        return result;
    }, ct);

    public async Task<PaymentTermHistoryDto?> GetPaymentTermHistoryAsync(TenantId tenant, Guid termId, CancellationToken ct) => await WithTenantAsync(tenant, async (connection, token) =>
    {
        await using var termCommand = new NpgsqlCommand("select id,code,display_name_ar,display_name_en,status,version from foundation_finbase.payment_term where tenant_id=$1 and id=$2", connection); Add(termCommand, tenant.Value, termId); await using var termReader = await termCommand.ExecuteReaderAsync(token);
        if (!await termReader.ReadAsync(token)) return null;
        var term = new PaymentTermHistoryDto(termReader.GetGuid(0), termReader.GetString(1), termReader.GetString(2), termReader.GetString(3), termReader.GetString(4), termReader.GetInt64(5), []);
        await termReader.CloseAsync();
        var versions = new List<PaymentTermVersionDto>();
        await using var versionCommand = new NpgsqlCommand("select id,version_number,effective_from,effective_to,status,description from foundation_finbase.payment_term_version where tenant_id=$1 and payment_term_id=$2 order by version_number", connection); Add(versionCommand, tenant.Value, termId); await using var versionReader = await versionCommand.ExecuteReaderAsync(token);
        var rows = new List<(Guid Id, int Number, DateOnly From, DateOnly? To, string Status, string? Description)>();
        while (await versionReader.ReadAsync(token)) rows.Add((versionReader.GetGuid(0), versionReader.GetInt32(1), DateOnly.FromDateTime(versionReader.GetDateTime(2)), versionReader.IsDBNull(3) ? null : DateOnly.FromDateTime(versionReader.GetDateTime(3)), versionReader.GetString(4), versionReader.IsDBNull(5) ? null : versionReader.GetString(5)));
        await versionReader.CloseAsync();
        foreach (var row in rows)
        {
            var rules = new List<PaymentTermRuleRequest>();
            await using var ruleCommand = new NpgsqlCommand("select sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset from foundation_finbase.payment_term_rule where tenant_id=$1 and version_id=$2 order by sequence_no", connection); Add(ruleCommand, tenant.Value, row.Id); await using var ruleReader = await ruleCommand.ExecuteReaderAsync(token);
            while (await ruleReader.ReadAsync(token)) rules.Add(new(ruleReader.GetInt32(0), Enum.Parse<PaymentAllocation>(ruleReader.GetString(1)), ruleReader.IsDBNull(2) ? null : ruleReader.GetDecimal(2).ToString("0.########", CultureInfo.InvariantCulture), ruleReader.IsDBNull(3) ? null : ruleReader.GetDecimal(3).ToString("0.########", CultureInfo.InvariantCulture), Enum.Parse<DueDateBasis>(ruleReader.GetString(4)), ruleReader.GetInt32(5), ruleReader.IsDBNull(6) ? null : ruleReader.GetInt16(6), ruleReader.GetInt32(7)));
            versions.Add(new(row.Id, row.Number, row.From, row.To, row.Status, row.Description, rules));
        }
        return term with { Versions = versions };
    }, ct);

    public async Task<ExchangeRateDto> CreateRateAsync(TenantId tenant, Guid actor, Guid membership, CreateExchangeRateRequest request, Guid correlation, CancellationToken ct)
    {
        var from = CurrencyCode.Normalize(request.FromCurrency); var to = CurrencyCode.Normalize(request.ToCurrency); if (from == to) throw new FinanceBaseException("finbase.exchange_pair_invalid", "Exchange rate currencies must differ.");
        if (!TryParseDecimal(request.Rate, out var rate) || rate <= 0) throw new FinanceBaseException("finbase.invalid_decimal", "Exchange rate must be a positive invariant decimal string.");
        if (!Enum.IsDefined(request.Purpose) || request.SourceType is not ("Manual" or "Provider" or "Bank" or "Contract")) throw new FinanceBaseException("finbase.rate_not_approved", "Exchange rate purpose or source is unsupported.");
        var baseCurrency = await organization.GetBaseCurrencyCodeAsync(tenant, new(request.LegalEntityId), ct) ?? throw new FinanceBaseException("finbase.legal_entity_not_found", "Legal entity was not found.");
        if (!await CurrencyExistsAsync(from, ct) || !await CurrencyExistsAsync(to, ct)) throw new FinanceBaseException("finbase.currency_not_enabled", "Both currencies must exist in the active currency reference.");
        await EnsureEntityCurrencyEnabledAsync(tenant, new(request.LegalEntityId), baseCurrency, from, ct);
        await EnsureEntityCurrencyEnabledAsync(tenant, new(request.LegalEntityId), baseCurrency, to, ct);
        var reason = request.Reason?.Trim();
        if (request.SourceType == "Manual")
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
                throw new FinanceBaseException("finbase.rate_override_not_allowed", "A manual exchange rate requires a reason of 1 to 500 characters.");
            var policy = await GetPolicyAsync(tenant, new(request.LegalEntityId), ct);
            if (policy is null || !policy.ManualRateOverrideAllowed)
                throw new FinanceBaseException("finbase.rate_override_not_allowed", "The legal entity policy does not allow manual exchange rates.");
        }
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            const string sql = "insert into foundation_finbase.exchange_rate(tenant_id,id,legal_entity_id,from_currency,to_currency,rate,purpose,effective_date,source_type,provider_reference,reason,status,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Draft',$12)";
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, tenant.Value, id, request.LegalEntityId, from, to, rate, request.Purpose.ToString(), request.EffectiveDate.ToDateTime(TimeOnly.MinValue), request.SourceType, request.ProviderReference ?? (object)DBNull.Value, reason ?? (object)DBNull.Value, now); await command.ExecuteNonQueryAsync(token); await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "User", actor, membership, "finbase", "rate.created", "exchange_rate", id, null, JsonSerializer.Serialize(request with { Reason = reason }), null, "api", correlation, null, now, null), token); return new ExchangeRateDto(id, request.LegalEntityId, from, to, rate.ToString("0.############", CultureInfo.InvariantCulture), request.Purpose, request.EffectiveDate, request.SourceType, request.ProviderReference, ExchangeRateStatus.Draft, 1);
        }, ct);
    }

    public async Task<ExchangeRateDto> ApproveRateAsync(TenantId tenant, Guid actor, Guid membership, LegalEntityId entity, Guid id, Guid correlation, CancellationToken ct) => await coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        const string sql = "update foundation_finbase.exchange_rate set status='Approved',approved_at=$4,approved_by=$5,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft' returning legal_entity_id,from_currency,to_currency,rate,purpose,effective_date,source_type,provider_reference,version";
        await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, tenant.Value, entity.Value, id, clock.GetUtcNow(), actor); await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new FinanceBaseException("finbase.rate_not_approvable", "Rate was not found or is not in Draft status for this legal entity."); var dto = new ExchangeRateDto(id, reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3).ToString("0.############", CultureInfo.InvariantCulture), Enum.Parse<ExchangeRatePurpose>(reader.GetString(4)), DateOnly.FromDateTime(reader.GetDateTime(5)), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), ExchangeRateStatus.Approved, reader.GetInt64(8)); await reader.CloseAsync(); lookupCache.InvalidateRates(tenant, entity, dto.FromCurrency, dto.ToCurrency, dto.Purpose); context.AddOutbox(new(Guid.CreateVersion7(), "FinBaseRateApprovedV1", JsonSerializer.Serialize(new { id }), tenant, clock.GetUtcNow(), correlation, correlation, "finbase")); return dto;
    }, ct);

    public async Task<ConvertedMoneyDto> ConvertAsync(TenantId tenant, ConvertMoneyRequest request, CancellationToken ct)
    {
        if (!TryParseDecimal(request.Amount, out var amount)) throw new FinanceBaseException("finbase.invalid_decimal", "Amount must be an invariant decimal string.");
        var from = CurrencyCode.Normalize(request.FromCurrency); var to = CurrencyCode.Normalize(request.ToCurrency);
        if (!Enum.IsDefined(request.Purpose)) throw new FinanceBaseException("finbase.rate_missing", "The requested exchange-rate purpose is unsupported.");
        if (!await CurrencyExistsAsync(from, ct) || !await CurrencyExistsAsync(to, ct)) throw new FinanceBaseException("finbase.currency_not_enabled", "Both currencies must exist in the active currency reference.");
        var baseCurrency = await organization.GetBaseCurrencyCodeAsync(tenant, new(request.LegalEntityId), ct) ?? throw new FinanceBaseException("finbase.legal_entity_not_found", "Legal entity was not found.");
        await EnsureEntityCurrencyEnabledAsync(tenant, new(request.LegalEntityId), baseCurrency, from, ct);
        await EnsureEntityCurrencyEnabledAsync(tenant, new(request.LegalEntityId), baseCurrency, to, ct);
        if (from == to) return new(amount.ToString("0.############################", CultureInfo.InvariantCulture), from, amount.ToString("0.############################", CultureInfo.InvariantCulture), to, "1", request.BusinessDate, request.Purpose);
        var entity = new LegalEntityId(request.LegalEntityId);
        var policy = await GetPolicyAsync(tenant, entity, ct);
        var maxAge = policy?.ExchangeRateMaxAgeDays ?? throw new FinanceBaseException("finbase.rounding_policy_missing", "Financial policy is not configured for this legal entity.");
        ExchangeRateCandidate resolved;
        if (!lookupCache.TryGetRate(tenant, entity, from, to, request.Purpose, request.BusinessDate, policy.Version, out var cachedRate) || cachedRate is null)
        {
            var candidates = await WithTenantAsync(tenant, async (connection, token) =>
            {
                await using var command = new NpgsqlCommand("select id,from_currency,to_currency,rate,purpose,effective_date,status,version,source_type from foundation_finbase.exchange_rate where tenant_id=$1 and legal_entity_id=$2 and from_currency=$3 and to_currency=$4 and purpose=$5 and status='Approved' and effective_date <= $6 order by effective_date desc,id desc limit 2", connection);
                Add(command, tenant.Value, request.LegalEntityId, from, to, request.Purpose.ToString(), request.BusinessDate.ToDateTime(TimeOnly.MinValue)); await using var reader = await command.ExecuteReaderAsync(token); var rows = new List<ExchangeRateCandidate>(); while (await reader.ReadAsync(token)) rows.Add(new(reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), Enum.Parse<ExchangeRatePurpose>(reader.GetString(4)), DateOnly.FromDateTime(reader.GetDateTime(5)), ExchangeRateStatus.Approved, reader.GetGuid(0), reader.GetInt64(7), reader.GetString(8), reader.GetString(8) == "Manual")); return rows;
            }, ct);
            resolved = ExchangeRateResolver.Resolve(candidates, from, to, request.Purpose, request.BusinessDate, maxAge);
            lookupCache.SetRate(tenant, entity, from, to, request.Purpose, request.BusinessDate, policy.Version, resolved);
        }
        else resolved = ExchangeRateResolver.Resolve([cachedRate], from, to, request.Purpose, request.BusinessDate, maxAge);
        var roundingPolicy = await GetRoundingPolicySnapshotAsync(tenant, policy.RoundingPolicyId, ct) ?? throw new FinanceBaseException("finbase.rounding_policy_missing", "The financial rounding policy is not available.");
        var rateSnapshot = ExchangeRateSnapshot.FromApprovedRate(tenant.Value, request.LegalEntityId, resolved, clock.GetUtcNow());
        var converted = CurrencyConverter.Convert(Money.Create(request.Amount, from), to, rateSnapshot, roundingPolicy);
        var snapshotDto = new ExchangeRateSnapshotDto(rateSnapshot.TenantId, rateSnapshot.LegalEntityId, rateSnapshot.ApprovedRateId, rateSnapshot.FromCurrency, rateSnapshot.ToCurrency, rateSnapshot.Rate.ToString("0.############", CultureInfo.InvariantCulture), rateSnapshot.Purpose, rateSnapshot.EffectiveDate, rateSnapshot.Source, rateSnapshot.Version, rateSnapshot.ManualOverride, rateSnapshot.CapturedAt);
        return new(amount.ToString("0.############################", CultureInfo.InvariantCulture), from, converted.Target.Amount.ToString($"0.{new string('#', roundingPolicy.Scale)}", CultureInfo.InvariantCulture), to, resolved.Rate.ToString("0.############", CultureInfo.InvariantCulture), resolved.EffectiveDate, resolved.Purpose, snapshotDto);
    }

    private async Task<RoundingPolicySnapshot?> GetRoundingPolicySnapshotAsync(TenantId tenant, Guid id, CancellationToken ct)
    {
        if (lookupCache.TryGetRoundingPolicy(tenant, id, out var cached)) return cached;
        var policy = await WithTenantAsync(tenant, async (connection, token) =>
        {
        Guid policyId; long version; int scale; var strategy = LineDocumentRoundingStrategy.RoundEachLineThenSum;
        await using (var command = new NpgsqlCommand("select id,version,money_scale,line_document_strategy from foundation_finbase.financial_rounding_policy where tenant_id=$1 and id=$2 and status='Active'", connection))
        {
            Add(command, tenant.Value, id);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) return null;
            policyId = reader.GetGuid(0); version = reader.GetInt64(1); scale = reader.GetInt16(2); strategy = Enum.Parse<LineDocumentRoundingStrategy>(reader.GetString(3));
        }
        var rules = new List<RoundingPolicyContextRule>();
        await using (var command = new NpgsqlCommand("select rounding_context,mode from foundation_finbase.financial_rounding_policy_rule where tenant_id=$1 and rounding_policy_id=$2 order by rounding_context", connection))
        {
            Add(command, tenant.Value, id);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) rules.Add(new(Enum.Parse<RoundingContext>(reader.GetString(0)), Enum.Parse<RoundingMode>(reader.GetString(1))));
        }
        return RoundingPolicySnapshot.Create(policyId, version, scale, strategy, rules);
        }, ct);
        if (policy is not null) lookupCache.SetRoundingPolicy(tenant, id, policy);
        return policy;
    }

    public async Task<FinancialPolicyDto?> GetPolicyAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        if (lookupCache.TryGetPolicy(tenant, entity, out var cached)) return cached;
        var policy = await WithTenantAsync(tenant, async (connection, token) => { await using var command = new NpgsqlCommand("select legal_entity_id,rounding_policy_id,exchange_rate_max_age_days,manual_rate_override_allowed,version from foundation_finbase.financial_policy where tenant_id=$1 and legal_entity_id=$2", connection); Add(command, tenant.Value, entity.Value); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? new FinancialPolicyDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetBoolean(3), reader.GetInt64(4)) : null; }, ct);
        if (policy is not null) lookupCache.SetPolicy(tenant, entity, policy);
        return policy;
    }

    public async Task<RoundingPolicyDto> CreateRoundingPolicyAsync(TenantId tenant, CreateRoundingPolicyRequest request, Guid correlation, CancellationToken ct)
    {
        var rules = request.ContextRules?.Select(rule => new RoundingPolicyContextRule(rule.Context, rule.Mode)).ToArray()
            ?? Enum.GetValues<RoundingContext>().Select(context => new RoundingPolicyContextRule(context, request.Mode)).ToArray();
        if (!TryParseDecimal(request.CashIncrement, out var increment) || increment <= 0 || request.MoneyScale is < 0 or > 8 || !Enum.IsDefined(request.Mode) || !Enum.IsDefined(request.LineDocumentStrategy) || rules.Length != Enum.GetValues<RoundingContext>().Length || rules.Any(rule => !Enum.IsDefined(rule.Context) || !Enum.IsDefined(rule.Mode)) || rules.Select(rule => rule.Context).Distinct().Count() != rules.Length)
            throw new FinanceBaseException("finbase.rounding_policy_invalid", "Rounding increment, context rules, strategy, or scale is invalid.");
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            const string sql = "insert into foundation_finbase.financial_rounding_policy(tenant_id,id,code,name_ar,name_en,mode,money_scale,cash_increment,status,line_document_strategy,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,'Active',$9,$10,$10) returning version";
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, tenant.Value, id, request.Code.Trim(), request.NameAr.Trim(), request.NameEn.Trim(), request.Mode.ToString(), request.MoneyScale, increment, request.LineDocumentStrategy.ToString(), now); var version = Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            foreach (var rule in rules)
            {
                await using var insertRule = new NpgsqlCommand("insert into foundation_finbase.financial_rounding_policy_rule(tenant_id,rounding_policy_id,rounding_context,mode) values($1,$2,$3,$4)", context.Connection, context.Transaction);
                Add(insertRule, tenant.Value, id, rule.Context.ToString(), rule.Mode.ToString()); await insertRule.ExecuteNonQueryAsync(token);
            }
            context.AddOutbox(new(Guid.CreateVersion7(), "FinBaseRoundingPolicyActivatedV1", JsonSerializer.Serialize(new { id, version, contextRuleCount = rules.Length, lineDocumentStrategy = request.LineDocumentStrategy }), tenant, now, correlation, correlation, "finbase"));
            var dtoRules = rules.Select(rule => new RoundingPolicyContextRuleRequest(rule.Context, rule.Mode)).ToArray();
            return new RoundingPolicyDto(id, request.Code.Trim(), request.NameAr.Trim(), request.NameEn.Trim(), request.Mode, request.MoneyScale, increment.ToString("0.########", CultureInfo.InvariantCulture), "Active", version, request.LineDocumentStrategy, dtoRules);
        }, ct);
    }

    public async Task<IReadOnlyList<RoundingPolicyDto>> ListRoundingPoliciesAsync(TenantId tenant, CancellationToken ct) => await WithTenantAsync(tenant, async (connection, token) =>
    {
        const string sql = "select p.id,p.code,p.name_ar,p.name_en,p.mode,p.money_scale,p.cash_increment,p.status,p.version,p.line_document_strategy,r.rounding_context,r.mode from foundation_finbase.financial_rounding_policy p join foundation_finbase.financial_rounding_policy_rule r on r.tenant_id=p.tenant_id and r.rounding_policy_id=p.id where p.tenant_id=$1 and p.status='Active' order by p.code,r.rounding_context limit 800";
        var policies = new Dictionary<Guid, (RoundingPolicyDto Policy, List<RoundingPolicyContextRuleRequest> Rules)>();
        await using var command = new NpgsqlCommand(sql, connection); Add(command, tenant.Value); await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var id = reader.GetGuid(0);
            if (!policies.TryGetValue(id, out var row))
            {
                var policy = new RoundingPolicyDto(id, reader.GetString(1), reader.GetString(2), reader.GetString(3), Enum.Parse<RoundingMode>(reader.GetString(4)), reader.GetInt16(5), reader.GetDecimal(6).ToString("0.########", CultureInfo.InvariantCulture), reader.GetString(7), reader.GetInt64(8), Enum.Parse<LineDocumentRoundingStrategy>(reader.GetString(9)), []);
                row = (policy, []);
                policies.Add(id, row);
            }
            row.Rules.Add(new(Enum.Parse<RoundingContext>(reader.GetString(10)), Enum.Parse<RoundingMode>(reader.GetString(11))));
        }
        return policies.Values.Select(row => row.Policy with { ContextRules = row.Rules }).OrderBy(policy => policy.Code, StringComparer.Ordinal).ToArray();
    }, ct);

    public async Task<FinancialPolicyDto> UpdatePolicyAsync(TenantId tenant, LegalEntityId entity, UpdateFinancialPolicyRequest request, Guid correlation, CancellationToken ct)
    {
        if (request.ExchangeRateMaxAgeDays is < 0 or > 3650) throw new FinanceBaseException("finbase.rate_age_invalid", "Exchange-rate maximum age is out of range.");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using (var current = new NpgsqlCommand("select version,rounding_policy_id,exchange_rate_max_age_days,manual_rate_override_allowed from foundation_finbase.financial_policy where tenant_id=$1 and legal_entity_id=$2 for update", context.Connection, context.Transaction))
            {
                Add(current, tenant.Value, entity.Value); await using var reader = await current.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    var oldVersion = reader.GetInt64(0); var oldRounding = reader.GetGuid(1); var oldMaxAge = reader.GetInt32(2); var oldManualAllowed = reader.GetBoolean(3); await reader.CloseAsync();
                    if (oldVersion != request.Version) throw new FinanceBaseException("finbase.policy_concurrency", "Financial policy was changed by another user.");
                    await using var history = new NpgsqlCommand("insert into foundation_finbase.financial_policy_version(tenant_id,legal_entity_id,version,rounding_policy_id,exchange_rate_max_age_days,manual_rate_override_allowed,captured_at) values($1,$2,$3,$4,$5,$6,$7) on conflict(tenant_id,legal_entity_id,version) do nothing", context.Connection, context.Transaction);
                    Add(history, tenant.Value, entity.Value, oldVersion, oldRounding, oldMaxAge, oldManualAllowed, clock.GetUtcNow()); await history.ExecuteNonQueryAsync(token);
                }
                else if (request.Version != 0) throw new FinanceBaseException("finbase.policy_concurrency", "Financial policy was not found at the expected version.");
            }
            const string sql = "insert into foundation_finbase.financial_policy(tenant_id,legal_entity_id,rounding_policy_id,exchange_rate_max_age_days,manual_rate_override_allowed,version,updated_at) values($1,$2,$3,$4,$5,1,$6) on conflict(tenant_id,legal_entity_id) do update set rounding_policy_id=excluded.rounding_policy_id,exchange_rate_max_age_days=excluded.exchange_rate_max_age_days,manual_rate_override_allowed=excluded.manual_rate_override_allowed,version=foundation_finbase.financial_policy.version+1,updated_at=excluded.updated_at where foundation_finbase.financial_policy.version=$7 returning version";
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, tenant.Value, entity.Value, request.RoundingPolicyId, request.ExchangeRateMaxAgeDays, request.ManualRateOverrideAllowed, clock.GetUtcNow(), request.Version); var value = await command.ExecuteScalarAsync(token); if (value is null) throw new FinanceBaseException("finbase.policy_concurrency", "Financial policy was changed by another user."); var version = Convert.ToInt64(value, CultureInfo.InvariantCulture); lookupCache.InvalidatePolicy(tenant, entity); context.AddOutbox(new(Guid.CreateVersion7(), "FinBaseFinancialPolicyChangedV1", JsonSerializer.Serialize(new { entityId = entity.Value, version }), tenant, clock.GetUtcNow(), correlation, correlation, "finbase")); return new FinancialPolicyDto(entity.Value, request.RoundingPolicyId, request.ExchangeRateMaxAgeDays, request.ManualRateOverrideAllowed, version);
        }, ct);
    }

    public async Task<PaymentTermDto> CreatePaymentTermAsync(TenantId tenant, CreatePaymentTermRequest request, Guid correlation, CancellationToken ct)
    {
        ValidatePaymentTermRules(request.Rules, request.EffectiveFrom);
        var termId = Guid.CreateVersion7(); var versionId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using (var term = new NpgsqlCommand("insert into foundation_finbase.payment_term(tenant_id,id,code,display_name_ar,display_name_en,status,created_at,updated_at) values($1,$2,$3,$4,$5,'Active',$6,$6)", context.Connection, context.Transaction)) { Add(term, tenant.Value, termId, request.Code.Trim(), request.DisplayNameAr.Trim(), request.DisplayNameEn.Trim(), now); await term.ExecuteNonQueryAsync(token); }
            await using (var version = new NpgsqlCommand("insert into foundation_finbase.payment_term_version(tenant_id,id,payment_term_id,version_number,effective_from,status,description) values($1,$2,$3,1,$4,'Active',$5)", context.Connection, context.Transaction)) { Add(version, tenant.Value, versionId, termId, request.EffectiveFrom, request.Description ?? (object)DBNull.Value); await version.ExecuteNonQueryAsync(token); }
            foreach (var rule in request.Rules) { var mappedRule = MapRule(rule); await using var command = new NpgsqlCommand("insert into foundation_finbase.payment_term_rule(tenant_id,id,version_id,sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", context.Connection, context.Transaction); Add(command, tenant.Value, Guid.CreateVersion7(), versionId, rule.Sequence, rule.AllocationType.ToString(), mappedRule.Percentage ?? (object)DBNull.Value, mappedRule.FixedAmount ?? (object)DBNull.Value, rule.DueDateBasis.ToString(), rule.Days, rule.DayOfMonth ?? (object)DBNull.Value, rule.MonthOffset); await command.ExecuteNonQueryAsync(token); }
            context.AddOutbox(new(Guid.CreateVersion7(), "FinBasePaymentTermActivatedV1", JsonSerializer.Serialize(new { termId, versionId, version = 1 }), tenant, now, correlation, correlation, "finbase"));
            return new PaymentTermDto(termId, request.Code.Trim(), request.DisplayNameAr.Trim(), request.DisplayNameEn.Trim(), 1, request.EffectiveFrom, request.Rules);
        }, ct);
    }

    public async Task<PaymentTermVersionDto> CreatePaymentTermVersionAsync(TenantId tenant, Guid termId, CreatePaymentTermVersionRequest request, Guid correlation, CancellationToken ct)
    {
        ValidatePaymentTermRules(request.Rules, request.EffectiveFrom);
        var versionId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var lockTerm = new NpgsqlCommand("select status,version from foundation_finbase.payment_term where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); Add(lockTerm, tenant.Value, termId); await using var termReader = await lockTerm.ExecuteReaderAsync(token);
            if (!await termReader.ReadAsync(token)) throw new FinanceBaseException("finbase.payment_term_not_found", "Payment term was not found.");
            var status = termReader.GetString(0); var version = termReader.GetInt64(1); await termReader.CloseAsync();
            if (status != "Active") throw new FinanceBaseException("finbase.payment_term_archived", "An archived payment term cannot receive a new version.");
            if (version != request.ExpectedTermVersion) throw new FinanceBaseException("finbase.payment_term_concurrency", "Payment term was changed by another user.");
            var newTermVersion = version + 1;
            await using (var bump = new NpgsqlCommand("update foundation_finbase.payment_term set version=$3,updated_at=$4 where tenant_id=$1 and id=$2 and version=$5", context.Connection, context.Transaction)) { Add(bump, tenant.Value, termId, newTermVersion, now, version); if (await bump.ExecuteNonQueryAsync(token) != 1) throw new FinanceBaseException("finbase.payment_term_concurrency", "Payment term was changed by another user."); }
            await using var nextVersion = new NpgsqlCommand("select coalesce(max(version_number),0)+1 from foundation_finbase.payment_term_version where tenant_id=$1 and payment_term_id=$2", context.Connection, context.Transaction); Add(nextVersion, tenant.Value, termId); var number = Convert.ToInt32(await nextVersion.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            await using (var insert = new NpgsqlCommand("insert into foundation_finbase.payment_term_version(tenant_id,id,payment_term_id,version_number,effective_from,status,description) values($1,$2,$3,$4,$5,'Draft',$6)", context.Connection, context.Transaction)) { Add(insert, tenant.Value, versionId, termId, number, request.EffectiveFrom, request.Description ?? (object)DBNull.Value); await insert.ExecuteNonQueryAsync(token); }
            await InsertPaymentTermRulesAsync(context.Connection, context.Transaction, tenant, versionId, request.Rules, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "FinBasePaymentTermChangedV1", JsonSerializer.Serialize(new { termId, versionId, versionNumber = number, termVersion = newTermVersion }), tenant, now, correlation, correlation, "finbase"));
            return new PaymentTermVersionDto(versionId, number, request.EffectiveFrom, null, "Draft", request.Description, request.Rules, newTermVersion);
        }, ct);
    }

    public async Task<PaymentTermVersionDto> ActivatePaymentTermVersionAsync(TenantId tenant, Guid termId, Guid versionId, long expectedTermVersion, Guid correlation, CancellationToken ct) => await coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        await using var term = new NpgsqlCommand("select status,version from foundation_finbase.payment_term where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); Add(term, tenant.Value, termId); await using var termReader = await term.ExecuteReaderAsync(token);
        if (!await termReader.ReadAsync(token)) throw new FinanceBaseException("finbase.payment_term_not_found", "Payment term was not found.");
        var status = termReader.GetString(0); var version = termReader.GetInt64(1); await termReader.CloseAsync();
        if (status != "Active") throw new FinanceBaseException("finbase.payment_term_archived", "An archived payment term cannot be activated.");
        if (version != expectedTermVersion) throw new FinanceBaseException("finbase.payment_term_concurrency", "Payment term was changed by another user.");
        DateOnly effectiveFrom; int versionNumber; string? description;
        await using (var candidate = new NpgsqlCommand("select effective_from,version_number,description from foundation_finbase.payment_term_version where tenant_id=$1 and payment_term_id=$2 and id=$3 and status='Draft'", context.Connection, context.Transaction))
        {
            Add(candidate, tenant.Value, termId, versionId); await using var reader = await candidate.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new FinanceBaseException("finbase.payment_term_version_not_activatable", "Only a draft version of this payment term can be activated.");
            effectiveFrom = DateOnly.FromDateTime(reader.GetDateTime(0)); versionNumber = reader.GetInt32(1); description = reader.IsDBNull(2) ? null : reader.GetString(2);
        }
        await using (var active = new NpgsqlCommand("select id,effective_from from foundation_finbase.payment_term_version where tenant_id=$1 and payment_term_id=$2 and status='Active' for update", context.Connection, context.Transaction))
        {
            Add(active, tenant.Value, termId); await using var reader = await active.ExecuteReaderAsync(token);
            if (await reader.ReadAsync(token))
            {
                var activeId = reader.GetGuid(0); var activeFrom = DateOnly.FromDateTime(reader.GetDateTime(1)); await reader.CloseAsync();
                if (effectiveFrom <= activeFrom) throw new FinanceBaseException("finbase.payment_term_effective_date_invalid", "A replacement version must take effect after the active version begins.");
                await using var retire = new NpgsqlCommand("update foundation_finbase.payment_term_version set status='Retired',effective_to=$3 - 1 where tenant_id=$1 and payment_term_id=$2 and id=$4 and status='Active'", context.Connection, context.Transaction); Add(retire, tenant.Value, termId, effectiveFrom, activeId); await retire.ExecuteNonQueryAsync(token);
            }
        }
        await using (var activate = new NpgsqlCommand("update foundation_finbase.payment_term_version set status='Active' where tenant_id=$1 and payment_term_id=$2 and id=$3 and status='Draft'", context.Connection, context.Transaction)) { Add(activate, tenant.Value, termId, versionId); if (await activate.ExecuteNonQueryAsync(token) != 1) throw new FinanceBaseException("finbase.payment_term_version_not_activatable", "Payment term version changed before activation."); }
        await using (var bump = new NpgsqlCommand("update foundation_finbase.payment_term set version=version+1,updated_at=$3 where tenant_id=$1 and id=$2 and version=$4", context.Connection, context.Transaction)) { Add(bump, tenant.Value, termId, clock.GetUtcNow(), expectedTermVersion); if (await bump.ExecuteNonQueryAsync(token) != 1) throw new FinanceBaseException("finbase.payment_term_concurrency", "Payment term was changed by another user."); }
        var now = clock.GetUtcNow(); context.AddOutbox(new(Guid.CreateVersion7(), "FinBasePaymentTermActivatedV1", JsonSerializer.Serialize(new { termId, versionId, versionNumber }), tenant, now, correlation, correlation, "finbase"));
        var rules = await LoadPaymentTermRulesAsync(context.Connection, context.Transaction, tenant, versionId, token);
        return new PaymentTermVersionDto(versionId, versionNumber, effectiveFrom, null, "Active", description, rules, expectedTermVersion + 1);
    }, ct);

    public async Task<long> ArchivePaymentTermAsync(TenantId tenant, Guid termId, long expectedVersion, Guid correlation, CancellationToken ct) => await coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var now = clock.GetUtcNow();
        await using var command = new NpgsqlCommand("update foundation_finbase.payment_term set status='Archived',version=version+1,updated_at=$4 where tenant_id=$1 and id=$2 and version=$3 and status='Active' returning version", context.Connection, context.Transaction); Add(command, tenant.Value, termId, expectedVersion, now); var result = await command.ExecuteScalarAsync(token);
        if (result is null) throw new FinanceBaseException("finbase.payment_term_concurrency", "Payment term was not found, archived, or changed by another user.");
        var version = Convert.ToInt64(result, CultureInfo.InvariantCulture); context.AddOutbox(new(Guid.CreateVersion7(), "FinBasePaymentTermArchivedV1", JsonSerializer.Serialize(new { termId, version }), tenant, now, correlation, correlation, "finbase")); return version;
    }, ct);

    public async Task<PaymentTermVersionDto?> ResolveActivePaymentTermVersionAsync(TenantId tenant, Guid termId, DateOnly businessDate, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using (var tenantSetting = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { tenantSetting.Parameters.AddWithValue(tenant.Value.ToString("D")); await tenantSetting.ExecuteNonQueryAsync(ct); }
        await using var command = new NpgsqlCommand("select v.id,v.version_number,v.effective_from,v.effective_to,v.status,v.description from foundation_finbase.payment_term t join foundation_finbase.payment_term_version v on v.tenant_id=t.tenant_id and v.payment_term_id=t.id where t.tenant_id=$1 and t.id=$2 and t.status='Active' and v.status='Active' and v.effective_from<=$3 and (v.effective_to is null or v.effective_to>=$3) order by v.version_number desc limit 1", connection); Add(command, tenant.Value, termId, businessDate); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var id = reader.GetGuid(0); var number = reader.GetInt32(1); var from = DateOnly.FromDateTime(reader.GetDateTime(2)); DateOnly? to = reader.IsDBNull(3) ? null : DateOnly.FromDateTime(reader.GetDateTime(3)); var status = reader.GetString(4); var description = reader.IsDBNull(5) ? null : reader.GetString(5); await reader.CloseAsync();
        var rules = await LoadPaymentTermRulesAsync(connection, null, tenant, id, ct);
        return new PaymentTermVersionDto(id, number, from, to, status, description, rules);
    }

    public static Task<IReadOnlyList<PaymentScheduleLineDto>> PreviewScheduleAsync(PaymentScheduleRequest request, IReadOnlyList<PaymentTermRuleRequest> rules) { if (!TryParseDecimal(request.TotalAmount, out var total)) throw new FinanceBaseException("finbase.invalid_decimal", "Total amount must be an invariant decimal string."); return Task.FromResult<IReadOnlyList<PaymentScheduleLineDto>>(PaymentScheduleCalculator.Calculate(total, request.BasisDate, rules.Select(MapRule).ToArray(), request.Scale).Select(x => new PaymentScheduleLineDto(x.Sequence, x.Amount.ToString("0.########", CultureInfo.InvariantCulture), x.DueDate, x.Allocation)).ToArray()); }

    private static PaymentTermRuleInput MapRule(PaymentTermRuleRequest rule)
    {
        decimal? fixedAmount = null;
        if (!string.IsNullOrWhiteSpace(rule.FixedAmount))
        {
            if (!TryParseDecimal(rule.FixedAmount, out var value)) throw new FinanceBaseException("finbase.invalid_decimal", "Fixed amount must be an invariant decimal string.");
            fixedAmount = value;
        }
        decimal? percentage = null;
        if (!string.IsNullOrWhiteSpace(rule.Percentage))
        {
            if (!TryParseDecimal(rule.Percentage, out var ratio)) throw new FinanceBaseException("finbase.invalid_decimal", "Payment percentage must be an invariant decimal string.");
            percentage = ratio;
        }
        return new(rule.Sequence, rule.AllocationType, percentage, fixedAmount, rule.DueDateBasis, rule.Days, rule.DayOfMonth, rule.MonthOffset);
    }

    private static void ValidatePaymentTermRules(IReadOnlyList<PaymentTermRuleRequest> rules, DateOnly effectiveFrom)
    {
        if (rules.Count == 0) throw new FinanceBaseException("finbase.payment_term_invalid", "At least one payment term rule is required.");
        try { _ = PaymentScheduleCalculator.Calculate(1m, effectiveFrom, rules.Select(MapRule).ToArray(), 8); }
        catch (FinanceBaseDomainException exception) { throw new FinanceBaseException(exception.Code, exception.Message); }
    }

    private static async Task InsertPaymentTermRulesAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, TenantId tenant, Guid versionId, IReadOnlyList<PaymentTermRuleRequest> rules, CancellationToken ct)
    {
        foreach (var rule in rules)
        {
            var mapped = MapRule(rule);
            await using var command = new NpgsqlCommand("insert into foundation_finbase.payment_term_rule(tenant_id,id,version_id,sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", connection, transaction);
            Add(command, tenant.Value, Guid.CreateVersion7(), versionId, rule.Sequence, rule.AllocationType.ToString(), mapped.Percentage ?? (object)DBNull.Value, mapped.FixedAmount ?? (object)DBNull.Value, rule.DueDateBasis.ToString(), rule.Days, rule.DayOfMonth ?? (object)DBNull.Value, rule.MonthOffset);
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<IReadOnlyList<PaymentTermRuleRequest>> LoadPaymentTermRulesAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, TenantId tenant, Guid versionId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset from foundation_finbase.payment_term_rule where tenant_id=$1 and version_id=$2 order by sequence_no", connection, transaction);
        Add(command, tenant.Value, versionId); await using var reader = await command.ExecuteReaderAsync(ct); var rules = new List<PaymentTermRuleRequest>();
        while (await reader.ReadAsync(ct)) rules.Add(new(reader.GetInt32(0), Enum.Parse<PaymentAllocation>(reader.GetString(1)), reader.IsDBNull(2) ? null : reader.GetDecimal(2).ToString("0.########", CultureInfo.InvariantCulture), reader.IsDBNull(3) ? null : reader.GetDecimal(3).ToString("0.########", CultureInfo.InvariantCulture), Enum.Parse<DueDateBasis>(reader.GetString(4)), reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetInt16(6), reader.GetInt32(7)));
        return rules;
    }

    private static bool TryParseDecimal(string value, out decimal result) => decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result);

    private static bool IsCredentialReference(string reference)
    {
        var colon = reference.IndexOf(':');
        if (colon <= 0 || reference.Length <= colon + 1) return false;
        var scheme = reference[..colon]; var name = reference[(colon + 1)..];
        return (scheme is "secret-ref" or "vault-ref" or "env-ref") && name.All(character => char.IsAsciiLetterOrDigit(character) || character is '/' or '_' or '-' or '.');
    }

    private async Task<bool> CurrencyExistsAsync(string code, CancellationToken ct)
    {
        if (lookupCache.TryGetCurrency(code, out var cached)) return cached;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var command = new NpgsqlCommand("select exists(select 1 from foundation_finbase.currency_reference where code=$1 and status='Active')", connection); command.Parameters.AddWithValue(code); var exists = (bool)(await command.ExecuteScalarAsync(ct))!; lookupCache.SetCurrency(code, exists); return exists;
    }
    private async Task EnsureEntityCurrencyEnabledAsync(TenantId tenant, LegalEntityId entity, string baseCurrency, string currency, CancellationToken ct)
    {
        if (string.Equals(baseCurrency, currency, StringComparison.Ordinal)) return;
        var enabled = await WithTenantAsync(tenant, async (connection, token) =>
        {
            await using var command = new NpgsqlCommand("select exists(select 1 from foundation_finbase.legal_entity_currency where tenant_id=$1 and legal_entity_id=$2 and currency_code=$3 and enabled and status='Active')", connection);
            Add(command, tenant.Value, entity.Value, currency);
            return (bool)(await command.ExecuteScalarAsync(token))!;
        }, ct);
        if (!enabled) throw new FinanceBaseException("finbase.currency_not_enabled", "Currency is not enabled for this legal entity.");
    }
    private async Task<T> WithTenantAsync<T>(TenantId tenant, Func<NpgsqlConnection, CancellationToken, Task<T>> action, CancellationToken ct) { await using var connection = await dataSource.OpenConnectionAsync(ct); await using var setting = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); setting.Parameters.AddWithValue(tenant.Value.ToString("D")); await setting.ExecuteNonQueryAsync(ct); return await action(connection, ct); }
    private static void Add(NpgsqlCommand command, params object[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }
}
