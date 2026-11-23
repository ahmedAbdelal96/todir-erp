using Core.Modules;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modules.Manufacturing.DTOs;
using Modules.Manufacturing.Entities;
using Xunit;

namespace Modules.Manufacturing.Tests;

public sealed class ManufacturingLifecycleAndCostingTests
{
    [Fact]
    public void CommercialEntitlementPurchasableModuleRequiresOperationalActivation()
    {
        // When purchasable is true and module_instance row is absent, decision must FAIL CLOSED
        var decision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus(null, isPurchasable: true, isMutation: false);
        Assert.False(decision.Allowed);
        Assert.False(decision.IsReadOnly);
        Assert.Equal("Disabled", decision.Status);
        Assert.Equal("module.not_activated", decision.DeniedReason);
    }

    [Fact]
    public void CommercialEntitlementActivePurchasableModuleAllowsAccess()
    {
        var readDecision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus("Active", isPurchasable: true, isMutation: false);
        Assert.True(readDecision.Allowed);
        Assert.False(readDecision.IsReadOnly);
        Assert.Equal("Active", readDecision.Status);

        var mutationDecision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus("Active", isPurchasable: true, isMutation: true);
        Assert.True(mutationDecision.Allowed);
        Assert.False(mutationDecision.IsReadOnly);
        Assert.Equal("Active", mutationDecision.Status);
    }

    [Fact]
    public void CommercialEntitlementReadOnlyTenantAllowsReadButRejectsMutation()
    {
        var readDecision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus("ReadOnly", isPurchasable: true, isMutation: false);
        Assert.True(readDecision.Allowed);
        Assert.True(readDecision.IsReadOnly);
        Assert.Equal("ReadOnly", readDecision.Status);

        var mutationDecision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus("ReadOnly", isPurchasable: true, isMutation: true);
        Assert.False(mutationDecision.Allowed);
        Assert.True(mutationDecision.IsReadOnly);
        Assert.Equal("ReadOnly", mutationDecision.Status);
        Assert.Equal("module.lifecycle_readonly", mutationDecision.DeniedReason);
    }

    [Fact]
    public void CommercialEntitlementDisabledOrSuspendedTenantRejectsBothReadAndMutation()
    {
        var disabledRead = NpgsqlModuleLifecycleEnforcer.EvaluateStatus("Disabled", isPurchasable: true, isMutation: false);
        Assert.False(disabledRead.Allowed);
        Assert.Equal("Disabled", disabledRead.Status);
        Assert.Equal("module.disabled", disabledRead.DeniedReason);

        var disabledMutation = NpgsqlModuleLifecycleEnforcer.EvaluateStatus("Disabled", isPurchasable: true, isMutation: true);
        Assert.False(disabledMutation.Allowed);
        Assert.Equal("Disabled", disabledMutation.Status);
        Assert.Equal("module.disabled", disabledMutation.DeniedReason);
    }

    [Fact]
    public void CommercialEntitlementInvalidStatusFailsClosed()
    {
        var decision = NpgsqlModuleLifecycleEnforcer.EvaluateStatus("ArbitraryHackedState", isPurchasable: true, isMutation: false);
        Assert.False(decision.Allowed);
        Assert.Equal("Disabled", decision.Status);
        Assert.Equal("module.invalid_status", decision.DeniedReason);
    }

    [Fact]
    public void CostingSummaryResponseCarriesAuthoritativeCurrencyAndSegmentedMetrics()
    {
        var summary = new ManufacturingCostingSummaryResponse(
            AccumulatingOrdersCount: 5,
            TotalWipAmount: 142500.50m,
            CurrencyCode: "EGP",
            CompletedBlockedOrdersCount: 2,
            EligibleForCloseOrdersCount: 3,
            ClosedReconciledOrdersCount: 10
        );

        Assert.Equal("EGP", summary.CurrencyCode);
        Assert.Equal(5, summary.AccumulatingOrdersCount);
        Assert.Equal(142500.50m, summary.TotalWipAmount);
        Assert.Equal(2, summary.CompletedBlockedOrdersCount);
        Assert.Equal(3, summary.EligibleForCloseOrdersCount);
        Assert.Equal(10, summary.ClosedReconciledOrdersCount);
    }

    [Fact]
    public void ProductionOrderCloseReadinessBlocksWhenMaterialUnaccountedOrOutputIncomplete()
    {
        var orderId = Guid.CreateVersion7();
        var blockedReadiness = new ProductionCloseReadinessResponse(
            orderId,
            "Blocked",
            MaterialIssuedQty: 100m,
            MaterialAccountedQty: 60m,
            GoodOutputQty: 50m,
            ScrapQty: 0m,
            OperationsComplete: false,
            QualityState: "Passed",
            CostState: "Accumulating",
            WipAmount: 15000m,
            BlockReason: "material_unaccounted"
        );

        Assert.Equal("Blocked", blockedReadiness.State);
        Assert.Equal("material_unaccounted", blockedReadiness.BlockReason);
        Assert.True(blockedReadiness.MaterialIssuedQty > blockedReadiness.MaterialAccountedQty);
    }

    [Fact]
    public void CostingSummaryThrowsWhenLegalEntityBaseCurrencyIsMissing()
    {
        var ex = new ManufacturingException("manufacturing.legal_entity_currency_missing", "Authoritative base currency for legal entity is not configured.");
        Assert.Equal("manufacturing.legal_entity_currency_missing", ex.Code);
        Assert.Contains("Authoritative base currency", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManufacturingExecutionFailsClosedWhenLifecycleEnforcerIsMissing()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var access = new ManufacturingModuleExtensions.Access(Shared.Primitives.Identifiers.TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), true);

        var result = await ManufacturingModuleExtensions.Execute(context, access, () => Task.FromResult(new { ok = true }));
        Assert.NotNull(result);
        var problem = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
    }

    [Fact]
    public async Task ManufacturingExecutionFailsClosedWhenLifecycleEnforcerIsUnavailable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IModuleLifecycleEnforcer>(new UnavailableModuleLifecycleEnforcer());
        var sp = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = sp };
        var access = new ManufacturingModuleExtensions.Access(Shared.Primitives.Identifiers.TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), true);

        var result = await ManufacturingModuleExtensions.Execute(context, access, () => Task.FromResult(new { ok = true }));
        Assert.NotNull(result);
        var problem = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
    }
}
