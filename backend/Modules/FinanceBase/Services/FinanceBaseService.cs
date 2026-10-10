using System.Globalization;
using System.Text.Json;
using Npgsql;
using Modules.FinanceBase.DTOs;
using Modules.FinanceBase.Entities;
using Core.Services;
using Core.Transactions;
using Shared.Primitives.Identifiers;

namespace Modules.FinanceBase.Services;

public sealed class FinanceBaseService(NpgsqlDataSource dataSource, ITransactionCoordinator coordinator, ILegalEntityFinancialReadStore organization, TimeProvider clock)
{
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
            while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetBoolean(2),reader.GetBoolean(3),reader.GetBoolean(4),reader.GetBoolean(5),reader.GetBoolean(6),reader.GetString(7),reader.GetInt64(8))); return result;
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
            context.AddOutbox(new(Guid.CreateVersion7(), "FinanceBaseCurrencyEnabledV1", JsonSerializer.Serialize(new { entity = entity.Value, currency }), tenant, now, correlation, correlation, "finbase"));
            return new EntityCurrencyDto(entity.Value, currency, true, request.AllowSales, request.AllowPurchase, request.AllowPayment, request.AllowManualRate, "Active", version);
        }, ct);
    }

    public async Task<IReadOnlyList<ExchangeRateDto>> ListRatesAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => await WithTenantAsync(tenant, async (connection, token) =>
    {
        await using var command = new NpgsqlCommand("select id,legal_entity_id,from_currency,to_currency,rate,purpose,effective_date,source_type,provider_reference,status,version from foundation_finbase.exchange_rate where tenant_id=$1 and legal_entity_id=$2 order by effective_date desc,id desc limit 200", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); await using var reader = await command.ExecuteReaderAsync(token); var result = new List<ExchangeRateDto>();
        while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0),reader.GetGuid(1),reader.GetString(2),reader.GetString(3),reader.GetDecimal(4).ToString("0.############",CultureInfo.InvariantCulture),Enum.Parse<ExchangeRatePurpose>(reader.GetString(5)),DateOnly.FromDateTime(reader.GetDateTime(6)),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetString(8),Enum.Parse<ExchangeRateStatus>(reader.GetString(9)),reader.GetInt64(10))); return result;
    }, ct);

    public async Task<ExchangeRateDto> CreateRateAsync(TenantId tenant, Guid actor, Guid membership, CreateExchangeRateRequest request, Guid correlation, CancellationToken ct)
    {
        var from = CurrencyCode.Normalize(request.FromCurrency); var to = CurrencyCode.Normalize(request.ToCurrency); if (from == to) throw new FinanceBaseException("finbase.exchange_pair_invalid", "Exchange rate currencies must differ.");
        if (!decimal.TryParse(request.Rate, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) || rate <= 0) throw new FinanceBaseException("finbase.exchange_rate_invalid", "Exchange rate must be a positive decimal string.");
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            const string sql = "insert into foundation_finbase.exchange_rate(tenant_id,id,legal_entity_id,from_currency,to_currency,rate,purpose,effective_date,source_type,provider_reference,status,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'Draft',$11)";
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command,tenant.Value,id,request.LegalEntityId,from,to,rate,request.Purpose.ToString(),request.EffectiveDate.ToDateTime(TimeOnly.MinValue),request.SourceType,request.ProviderReference ?? (object)DBNull.Value,now); await command.ExecuteNonQueryAsync(token); await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(),tenant,request.LegalEntityId,null,"User",actor,membership,"finbase","rate.created","exchange_rate",id,null,JsonSerializer.Serialize(request),null,"api",correlation,null,now,null),token); return new ExchangeRateDto(id,request.LegalEntityId,from,to,rate.ToString("0.############",CultureInfo.InvariantCulture),request.Purpose,request.EffectiveDate,request.SourceType,request.ProviderReference,ExchangeRateStatus.Draft,1);
        }, ct);
    }

    public async Task<ExchangeRateDto> ApproveRateAsync(TenantId tenant, Guid actor, Guid membership, Guid id, Guid correlation, CancellationToken ct) => await coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        const string sql = "update foundation_finbase.exchange_rate set status='Approved',approved_at=$3,approved_by=$4,version=version+1 where tenant_id=$1 and id=$2 and status='Draft' returning legal_entity_id,from_currency,to_currency,rate,purpose,effective_date,source_type,provider_reference,version";
        await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command,tenant.Value,id,clock.GetUtcNow(),actor); await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new FinanceBaseException("finbase.rate_not_approvable","Rate was not found or is not in Draft status."); var dto = new ExchangeRateDto(id,reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetDecimal(3).ToString("0.############",CultureInfo.InvariantCulture),Enum.Parse<ExchangeRatePurpose>(reader.GetString(4)),DateOnly.FromDateTime(reader.GetDateTime(5)),reader.GetString(6),reader.IsDBNull(7)?null:reader.GetString(7),ExchangeRateStatus.Approved,reader.GetInt64(8)); await reader.CloseAsync(); context.AddOutbox(new(Guid.CreateVersion7(),"FinanceBaseRateApprovedV1",JsonSerializer.Serialize(new { id }),tenant,clock.GetUtcNow(),correlation,correlation,"finbase")); return dto;
    }, ct);

    public async Task<ConvertedMoneyDto> ConvertAsync(TenantId tenant, ConvertMoneyRequest request, CancellationToken ct)
    {
        if (!decimal.TryParse(request.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)) throw new FinanceBaseException("finbase.money_invalid", "Amount must be a decimal string.");
        var from = CurrencyCode.Normalize(request.FromCurrency); var to = CurrencyCode.Normalize(request.ToCurrency); if (from == to) return new(request.Amount, from, request.Amount, to, "1", request.BusinessDate, request.Purpose);
        var maxAge = (await GetPolicyAsync(tenant, new(request.LegalEntityId), ct))?.ExchangeRateMaxAgeDays ?? 30;
        var candidates = await WithTenantAsync(tenant, async (connection, token) =>
        {
            await using var command = new NpgsqlCommand("select from_currency,to_currency,rate,purpose,effective_date,status from foundation_finbase.exchange_rate where tenant_id=$1 and legal_entity_id=$2 and from_currency=$3 and to_currency=$4 and purpose=$5 and status='Approved' and effective_date <= $6 order by effective_date desc", connection);
            Add(command,tenant.Value,request.LegalEntityId,from,to,request.Purpose.ToString(),request.BusinessDate.ToDateTime(TimeOnly.MinValue)); await using var reader=await command.ExecuteReaderAsync(token); var rows=new List<ExchangeRateCandidate>(); while(await reader.ReadAsync(token)) rows.Add(new(reader.GetString(0),reader.GetString(1),reader.GetDecimal(2),Enum.Parse<ExchangeRatePurpose>(reader.GetString(3)),DateOnly.FromDateTime(reader.GetDateTime(4)),ExchangeRateStatus.Approved)); return rows;
        },ct);
        var resolved=ExchangeRateResolver.Resolve(candidates,from,to,request.Purpose,request.BusinessDate,maxAge); var target=MoneyRounding.Round(amount*resolved.Rate,8,RoundingMode.HalfUp); return new(request.Amount,from,target.ToString("0.########",CultureInfo.InvariantCulture),to,resolved.Rate.ToString("0.############",CultureInfo.InvariantCulture),resolved.EffectiveDate,resolved.Purpose);
    }

    public async Task<FinancialPolicyDto?> GetPolicyAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => await WithTenantAsync(tenant, async (connection, token) => { await using var command = new NpgsqlCommand("select legal_entity_id,rounding_policy_id,exchange_rate_max_age_days,manual_rate_override_allowed,version from foundation_finbase.financial_policy where tenant_id=$1 and legal_entity_id=$2",connection); Add(command,tenant.Value,entity.Value); await using var reader=await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? new FinancialPolicyDto(reader.GetGuid(0),reader.GetGuid(1),reader.GetInt32(2),reader.GetBoolean(3),reader.GetInt64(4)) : null; }, ct);

    public async Task<RoundingPolicyDto> CreateRoundingPolicyAsync(TenantId tenant, CreateRoundingPolicyRequest request, CancellationToken ct)
    {
        if (!decimal.TryParse(request.CashIncrement, NumberStyles.Number, CultureInfo.InvariantCulture, out var increment) || increment <= 0) throw new FinanceBaseException("finbase.rounding_increment_invalid", "Cash increment must be a positive decimal string.");
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            const string sql = "insert into foundation_finbase.financial_rounding_policy(tenant_id,id,code,name_ar,name_en,mode,money_scale,cash_increment,status,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,'Active',$9,$9) returning version";
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command,tenant.Value,id,request.Code.Trim(),request.NameAr.Trim(),request.NameEn.Trim(),request.Mode.ToString(),request.MoneyScale,increment,now); var version=Convert.ToInt64(await command.ExecuteScalarAsync(token),CultureInfo.InvariantCulture); return new RoundingPolicyDto(id,request.Code.Trim(),request.NameAr.Trim(),request.NameEn.Trim(),request.Mode,request.MoneyScale,increment.ToString("0.########",CultureInfo.InvariantCulture),"Active",version);
        }, ct);
    }

    public async Task<FinancialPolicyDto> UpdatePolicyAsync(TenantId tenant, LegalEntityId entity, UpdateFinancialPolicyRequest request, CancellationToken ct)
    {
        if (request.ExchangeRateMaxAgeDays is < 0 or > 3650) throw new FinanceBaseException("finbase.rate_age_invalid", "Exchange-rate maximum age is out of range.");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            const string sql = "insert into foundation_finbase.financial_policy(tenant_id,legal_entity_id,rounding_policy_id,exchange_rate_max_age_days,manual_rate_override_allowed,version,updated_at) values($1,$2,$3,$4,$5,1,$6) on conflict(tenant_id,legal_entity_id) do update set rounding_policy_id=excluded.rounding_policy_id,exchange_rate_max_age_days=excluded.exchange_rate_max_age_days,manual_rate_override_allowed=excluded.manual_rate_override_allowed,version=foundation_finbase.financial_policy.version+1,updated_at=excluded.updated_at where foundation_finbase.financial_policy.version=$7 returning version";
            await using var command=new NpgsqlCommand(sql,context.Connection,context.Transaction); Add(command,tenant.Value,entity.Value,request.RoundingPolicyId,request.ExchangeRateMaxAgeDays,request.ManualRateOverrideAllowed,clock.GetUtcNow(),request.Version); var value=await command.ExecuteScalarAsync(token); if(value is null) throw new FinanceBaseException("finbase.policy_concurrency","Financial policy was changed by another user."); return new FinancialPolicyDto(entity.Value,request.RoundingPolicyId,request.ExchangeRateMaxAgeDays,request.ManualRateOverrideAllowed,Convert.ToInt64(value,CultureInfo.InvariantCulture));
        }, ct);
    }

    public async Task<PaymentTermDto> CreatePaymentTermAsync(TenantId tenant, CreatePaymentTermRequest request, CancellationToken ct)
    {
        if (request.Rules.Count == 0 || request.Rules.GroupBy(x=>x.Sequence).Any(x=>x.Count()>1) || request.Rules.Count(x=>x.AllocationType==PaymentAllocation.Balance)>1) throw new FinanceBaseException("finbase.payment_term_rules_invalid", "Payment term rules are invalid.");
        var termId=Guid.CreateVersion7(); var versionId=Guid.CreateVersion7(); var now=clock.GetUtcNow();
        return await coordinator.ExecuteAsync(tenant, async (context,token) =>
        {
            await using(var term=new NpgsqlCommand("insert into foundation_finbase.payment_term(tenant_id,id,code,display_name_ar,display_name_en,status,created_at,updated_at) values($1,$2,$3,$4,$5,'Active',$6,$6)",context.Connection,context.Transaction)){Add(term,tenant.Value,termId,request.Code.Trim(),request.DisplayNameAr.Trim(),request.DisplayNameEn.Trim(),now);await term.ExecuteNonQueryAsync(token);}
            await using(var version=new NpgsqlCommand("insert into foundation_finbase.payment_term_version(tenant_id,id,payment_term_id,version_number,effective_from,status,description) values($1,$2,$3,1,$4,'Active',$5)",context.Connection,context.Transaction)){Add(version,tenant.Value,versionId,termId,request.EffectiveFrom,request.Description ?? (object)DBNull.Value);await version.ExecuteNonQueryAsync(token);}
            foreach(var rule in request.Rules){decimal? fixedAmount=string.IsNullOrWhiteSpace(rule.FixedAmount)?null:decimal.Parse(rule.FixedAmount,CultureInfo.InvariantCulture);await using var command=new NpgsqlCommand("insert into foundation_finbase.payment_term_rule(tenant_id,id,version_id,sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)",context.Connection,context.Transaction);Add(command,tenant.Value,Guid.CreateVersion7(),versionId,rule.Sequence,rule.AllocationType.ToString(),rule.Percentage??(object)DBNull.Value,fixedAmount??(object)DBNull.Value,rule.DueDateBasis.ToString(),rule.Days,rule.DayOfMonth??(object)DBNull.Value,rule.MonthOffset);await command.ExecuteNonQueryAsync(token);}
            return new PaymentTermDto(termId,request.Code.Trim(),request.DisplayNameAr.Trim(),request.DisplayNameEn.Trim(),1,request.EffectiveFrom,request.Rules);
        },ct);
    }

    public static Task<IReadOnlyList<PaymentScheduleLineDto>> PreviewScheduleAsync(PaymentScheduleRequest request, IReadOnlyList<PaymentTermRuleRequest> rules) { if (!decimal.TryParse(request.TotalAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var total)) throw new FinanceBaseException("finbase.money_invalid", "Total amount must be a decimal string."); return Task.FromResult<IReadOnlyList<PaymentScheduleLineDto>>(PaymentScheduleCalculator.Calculate(total,request.BasisDate,rules.Select(x=>new PaymentTermRuleInput(x.Sequence,x.AllocationType,x.Percentage,string.IsNullOrWhiteSpace(x.FixedAmount)?null:decimal.Parse(x.FixedAmount,CultureInfo.InvariantCulture),x.DueDateBasis,x.Days,x.DayOfMonth,x.MonthOffset)).ToArray(),request.Scale).Select(x=>new PaymentScheduleLineDto(x.Sequence,x.Amount.ToString("0.########",CultureInfo.InvariantCulture),x.DueDate,x.Allocation)).ToArray()); }

    private async Task<bool> CurrencyExistsAsync(string code, CancellationToken ct) { await using var c=await dataSource.OpenConnectionAsync(ct); await using var q=new NpgsqlCommand("select exists(select 1 from foundation_finbase.currency_reference where code=$1 and status='Active')",c); q.Parameters.AddWithValue(code); return (bool)(await q.ExecuteScalarAsync(ct))!; }
    private async Task<T> WithTenantAsync<T>(TenantId tenant, Func<NpgsqlConnection,CancellationToken,Task<T>> action, CancellationToken ct) { await using var connection=await dataSource.OpenConnectionAsync(ct); await using var setting=new NpgsqlCommand("select set_config('app.tenant_id',$1,false)",connection); setting.Parameters.AddWithValue(tenant.Value.ToString("D")); await setting.ExecuteNonQueryAsync(ct); return await action(connection,ct); }
    private static void Add(NpgsqlCommand command, params object[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }
}
