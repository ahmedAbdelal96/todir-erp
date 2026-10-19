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
    public Task<IReadOnlyList<ExchangeRateSourceConfigDto>> ListRateSourceConfigsAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => repository.ListRateSourceConfigsAsync(tenant, entity, ct);
    public Task<ExchangeRateSourceConfigDto> CreateRateSourceConfigAsync(TenantId tenant, Guid actor, Guid membership, LegalEntityId entity, CreateExchangeRateSourceConfigRequest request, Guid correlation, CancellationToken ct) => repository.CreateRateSourceConfigAsync(tenant, actor, membership, entity, request, correlation, ct);
    public Task<ExchangeRateDto> CreateRateAsync(TenantId tenant, Guid actor, Guid membership, CreateExchangeRateRequest request, Guid correlation, CancellationToken ct) => repository.CreateRateAsync(tenant, actor, membership, request, correlation, ct);
    public Task<ExchangeRateDto> ApproveRateAsync(TenantId tenant, Guid actor, Guid membership, LegalEntityId entity, Guid id, Guid correlation, CancellationToken ct) => repository.ApproveRateAsync(tenant, actor, membership, entity, id, correlation, ct);
    public Task<ConvertedMoneyDto> ConvertAsync(TenantId tenant, ConvertMoneyRequest request, CancellationToken ct) => repository.ConvertAsync(tenant, request, ct);
    public Task<FinancialPolicyDto?> GetPolicyAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => repository.GetPolicyAsync(tenant, entity, ct);
    public Task<RoundingPolicyDto> CreateRoundingPolicyAsync(TenantId tenant, CreateRoundingPolicyRequest request, Guid correlation, CancellationToken ct) => repository.CreateRoundingPolicyAsync(tenant, request, correlation, ct);
    public Task<IReadOnlyList<RoundingPolicyDto>> ListRoundingPoliciesAsync(TenantId tenant, CancellationToken ct) => repository.ListRoundingPoliciesAsync(tenant, ct);
    public Task<FinancialPolicyDto> UpdatePolicyAsync(TenantId tenant, LegalEntityId entity, UpdateFinancialPolicyRequest request, Guid correlation, CancellationToken ct) => repository.UpdatePolicyAsync(tenant, entity, request, correlation, ct);
    public Task<PaymentTermDto> CreatePaymentTermAsync(TenantId tenant, CreatePaymentTermRequest request, Guid correlation, CancellationToken ct) => repository.CreatePaymentTermAsync(tenant, request, correlation, ct);
    public Task<IReadOnlyList<PaymentTermListItemDto>> ListPaymentTermsAsync(TenantId tenant, bool includeArchived, CancellationToken ct) => repository.ListPaymentTermsAsync(tenant, includeArchived, ct);
    public Task<PaymentTermHistoryDto?> GetPaymentTermHistoryAsync(TenantId tenant, Guid termId, CancellationToken ct) => repository.GetPaymentTermHistoryAsync(tenant, termId, ct);
    public Task<PaymentTermVersionDto> CreatePaymentTermVersionAsync(TenantId tenant, Guid termId, CreatePaymentTermVersionRequest request, Guid correlation, CancellationToken ct) => repository.CreatePaymentTermVersionAsync(tenant, termId, request, correlation, ct);
    public Task<PaymentTermVersionDto> ActivatePaymentTermVersionAsync(TenantId tenant, Guid termId, Guid versionId, long expectedTermVersion, Guid correlation, CancellationToken ct) => repository.ActivatePaymentTermVersionAsync(tenant, termId, versionId, expectedTermVersion, correlation, ct);
    public Task<long> ArchivePaymentTermAsync(TenantId tenant, Guid termId, long expectedVersion, Guid correlation, CancellationToken ct) => repository.ArchivePaymentTermAsync(tenant, termId, expectedVersion, correlation, ct);
    public static Task<IReadOnlyList<PaymentScheduleLineDto>> PreviewScheduleAsync(PaymentScheduleRequest request, IReadOnlyList<PaymentTermRuleRequest> rules) => Repositories.FinanceBaseRepository.PreviewScheduleAsync(request, rules);
}
