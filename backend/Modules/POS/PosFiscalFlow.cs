using Shared.Contracts.POS;

namespace Modules.POS;

public static class PosFiscalFlow
{
    public static async Task<PosFiscalSubmissionResult> SubmitAsync(
        IPosFiscalProvider provider,
        PosFiscalSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var result = await provider.SubmitAsync(request, cancellationToken);
        if (result is null || result.State is not ("Accepted" or "Deferred" or "Rejected"))
            throw new InvalidOperationException("pos.fiscal_provider_result_invalid");
        if (provider.Strategy == PosFiscalSubmissionStrategy.Synchronous && result.Deferred)
            throw new InvalidOperationException("pos.fiscal_sync_provider_deferred");
        if (provider.Strategy == PosFiscalSubmissionStrategy.Deferred && result.State == "Accepted" && result.Deferred)
            return result with { State = "Deferred" };
        return result;
    }
}
