using Modules.FinanceBase.DTOs;
using Modules.FinanceBase.Entities;
using Shared.Primitives.Identifiers;

namespace Modules.FinanceBase.Services;

public sealed class FinanceBaseService(Repositories.FinanceBaseRepository repository)
{
    public Task<IReadOnlyList<CurrencyDto>> ListCurrenciesAsync(CancellationToken ct) => repository.ListCurrenciesAsync(ct);
    public Task<IReadOnlyList<EntityCurrencyDto>> ListEntityCurrenciesAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => repository.ListEntityCurrenciesAsync(tenant, entity, ct);
    public Task<EntityCurrencyDto> EnableCurrencyAsync(TenantId tenant, Guid actor, Guid membership, LegalEntityId entity, string code, EnableCurrencyRequest request, Guid correlation, CancellationToken ct) => repository.EnableCurrencyAsync(tenant, actor, membership, entity, code, request, correlation, ct);
    public Task<IReadOnlyList<ExchangeRateDto>> ListRatesAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => repository.ListRatesAsync(tenant, entity, ct);
    public Task<ExchangeRateDto> CreateRateAsync(TenantId tenant, Guid actor, Guid membership, CreateExchangeRateRequest request, Guid correlation, CancellationToken ct) => repository.CreateRateAsync(tenant, actor, membership, request, correlation, ct);
    public Task<ExchangeRateDto> ApproveRateAsync(TenantId tenant, Guid actor, Guid membership, Guid id, Guid correlation, CancellationToken ct) => repository.ApproveRateAsync(tenant, actor, membership, id, correlation, ct);
    public Task<ConvertedMoneyDto> ConvertAsync(TenantId tenant, ConvertMoneyRequest request, CancellationToken ct) => repository.ConvertAsync(tenant, request, ct);
    public Task<FinancialPolicyDto?> GetPolicyAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => repository.GetPolicyAsync(tenant, entity, ct);
    public Task<RoundingPolicyDto> CreateRoundingPolicyAsync(TenantId tenant, CreateRoundingPolicyRequest request, CancellationToken ct) => repository.CreateRoundingPolicyAsync(tenant, request, ct);
    public Task<FinancialPolicyDto> UpdatePolicyAsync(TenantId tenant, LegalEntityId entity, UpdateFinancialPolicyRequest request, CancellationToken ct) => repository.UpdatePolicyAsync(tenant, entity, request, ct);
    public Task<PaymentTermDto> CreatePaymentTermAsync(TenantId tenant, CreatePaymentTermRequest request, CancellationToken ct) => repository.CreatePaymentTermAsync(tenant, request, ct);
    public static Task<IReadOnlyList<PaymentScheduleLineDto>> PreviewScheduleAsync(PaymentScheduleRequest request, IReadOnlyList<PaymentTermRuleRequest> rules) => Repositories.FinanceBaseRepository.PreviewScheduleAsync(request, rules);
}
