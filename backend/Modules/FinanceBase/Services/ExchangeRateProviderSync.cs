using Modules.FinanceBase.Entities;
using Modules.FinanceBase.Repositories;
using Shared.Primitives.Identifiers;

namespace Modules.FinanceBase.Services;

public sealed record ExchangeRateSourceConfig(Guid Id, TenantId TenantId, LegalEntityId LegalEntityId, string SourceCode, string FromCurrency, string ToCurrency, ExchangeRatePurpose Purpose, int Priority, string? CredentialReference, int SyncIntervalSeconds, int FailureCount);
public sealed record ExchangeRateProviderRequest(ExchangeRateSourceConfig Source, DateOnly RequestedThroughDate);
public sealed record ExchangeRateProviderQuote(string FromCurrency, string ToCurrency, decimal Rate, ExchangeRatePurpose Purpose, DateOnly EffectiveDate, string EventKey, string? ProviderReference);

public interface IExchangeRateProvider
{
    string SourceCode { get; }
    Task<ExchangeRateProviderQuote?> FetchAsync(ExchangeRateProviderRequest request, CancellationToken cancellationToken);
}

public interface IExchangeRateProviderRegistry
{
    IExchangeRateProvider? Find(string sourceCode);
}

public sealed class ExchangeRateProviderRegistry(IEnumerable<IExchangeRateProvider> providers) : IExchangeRateProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IExchangeRateProvider> byCode = providers.ToDictionary(provider => provider.SourceCode, StringComparer.Ordinal);
    public IExchangeRateProvider? Find(string sourceCode) => byCode.GetValueOrDefault(sourceCode);
}

public interface IFinanceBaseRateSyncService
{
    Task<int> SyncDueAsync(CancellationToken cancellationToken);
}

public sealed class FinanceBaseRateSyncService(FinanceBaseRepository repository, IExchangeRateProviderRegistry providers, TimeProvider clock) : IFinanceBaseRateSyncService
{
    public async Task<int> SyncDueAsync(CancellationToken cancellationToken)
    {
        var sources = await repository.ClaimDueRateSourcesAsync(50, cancellationToken);
        var imported = 0;
        foreach (var source in sources.OrderBy(item => item.Priority).ThenBy(item => item.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var provider = providers.Find(source.SourceCode);
            if (provider is null)
            {
                await repository.RecordRateSourceFailureAsync(source, "provider_not_registered", cancellationToken);
                continue;
            }

            try
            {
                var quote = await provider.FetchAsync(new(source, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)), cancellationToken);
                if (quote is not null && await repository.PersistProviderCandidateAsync(source, quote, cancellationToken)) imported++;
                await repository.RecordRateSourceSuccessAsync(source, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                var nextFailureCount = source.FailureCount + 1;
                var delaySeconds = Math.Min(source.SyncIntervalSeconds, Math.Min(3600, 15 * (1 << Math.Min(nextFailureCount - 1, 8))));
                await repository.RecordRateSourceFailureAsync(source, "provider_sync_failed", cancellationToken, TimeSpan.FromSeconds(delaySeconds));
            }
        }
        return imported;
    }
}
