using Shared.Contracts.POS;
using Xunit;

namespace POSTests;

public sealed class PosFiscalBoundaryTests
{
    [Theory]
    [InlineData(PosFiscalSubmissionStrategy.Synchronous, "Accepted", false)]
    [InlineData(PosFiscalSubmissionStrategy.Deferred, "Deferred", true)]
    public async Task TestProviderCanProveBothFiscalStrategiesWithoutCoreCountryBranching(
        PosFiscalSubmissionStrategy strategy, string expectedState, bool expectedDeferred)
    {
        var provider = new TestFiscalProvider(strategy, expectedState, expectedDeferred);
        var result = await Modules.POS.PosFiscalFlow.SubmitAsync(provider,
            new PosFiscalSubmissionRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "EGP", 10m, "POS-1"));

        Assert.Equal(expectedState, result.State);
        Assert.Equal(expectedDeferred, result.Deferred);
    }

    private sealed class TestFiscalProvider(PosFiscalSubmissionStrategy strategy, string state, bool deferred) : IPosFiscalProvider
    {
        public string ProviderCode => "test-provider";
        public PosFiscalSubmissionStrategy Strategy => strategy;

        public Task<PosFiscalSubmissionResult> SubmitAsync(PosFiscalSubmissionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PosFiscalSubmissionResult(state, "FISCAL-1", deferred));
    }
}
