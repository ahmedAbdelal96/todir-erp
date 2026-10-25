using Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Modules.Subledger.Repositories;
using Modules.Subledger.Services;
using Shared.Contracts.Subledger;

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
}

public sealed class SubledgerModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new(
        "subledger", "1.0.0", 1, "L2", "module", false, "tenant",
        new(["platform-kernel", "ledger", "directory", "finbase"], []),
        new("finance_subledger", ["party_financial_profile", "party_open_item", "application_batch", "open_item_application", "application_reversal"]),
        ["subledger.read", "subledger.profile.manage", "subledger.application.create", "subledger.application.reverse"],
        new(["subledger.open-items", "subledger.application", "subledger.party-financial-profile", "subledger.settlement-state"], ["directory.party", "finbase.money", "finbase.payment-terms"]),
        new([], []),
        new(true, "blocked_if_dependents"));
}
