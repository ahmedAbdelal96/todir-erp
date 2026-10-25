using Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Modules.Subledger.Repositories;
using Modules.Subledger.Services;
using Shared.Contracts.Subledger;
using Microsoft.AspNetCore.Builder;

namespace Modules.Subledger;

public static class SubledgerModuleExtensions
{
    public static IServiceCollection AddSubledgerModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, SubledgerModuleManifestProvider>();
        services.AddSingleton<SubledgerRepository>();
        services.AddSingleton<SubledgerService>();
        services.AddSingleton<ISubledgerCapabilities>(provider => provider.GetRequiredService<SubledgerService>());
        return services;
    }

    public static WebApplication MapSubledgerModule(this WebApplication app)
    {
        SubledgerEndpoints.Map(app);
        return app;
    }
}

public sealed class SubledgerModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new(
        "subledger", "1.0.0", 6, "L2", "module", false, "tenant",
        new(["platform-kernel", "ledger", "directory", "finbase"], []),
        new("finance_subledger", ["party_financial_profile", "party_open_item", "application_batch", "open_item_application", "application_reversal", "write_off_policy", "write_off_request", "write_off", "write_off_request_audit", "control_reconciliation_run", "control_reconciliation_detail", "opening_batch", "opening_batch_item"]),
        ["subledger.read", "subledger.profile.read", "subledger.profile.manage", "subledger.credit_limit.read", "subledger.credit_limit.manage", "subledger.application.create", "subledger.application.reverse", "subledger.application.cross_currency", "subledger.advance.create", "subledger.credit-note.create", "subledger.aging.read", "subledger.statement.read", "subledger.reconciliation.read", "subledger.reconciliation.run", "subledger.writeoff.policy.manage", "subledger.writeoff.create", "subledger.writeoff.approve", "subledger.fx.manual-rate", "subledger.opening.manage", "subledger.opening.post", "subledger.opening.finalize"],
        new(["subledger.open-items", "subledger.application", "subledger.application-suggestions", "subledger.party-financial-profile", "subledger.settlement-state", "subledger.party-exposure", "subledger.aging", "subledger.statement", "subledger.write-off", "subledger.control-reconciliation", "subledger.opening-migration"], ["directory.party", "finbase.money", "finbase.exchange-rate", "finbase.payment-terms", "finbase.payment-schedule-calculation", "ledger.semantic-settlement-facts", "ledger.control-balance-read"]),
        new(["OpenItemCreatedV1", "ApplicationConfirmedV1", "ApplicationReversedV1", "PartyFinancialProfileChangedV1", "CreditLimitChangedV1", "WriteOffApprovedV1", "OpeningBatchFinalizedV1"], []),
        new(true, "blocked_if_dependents"));
}
