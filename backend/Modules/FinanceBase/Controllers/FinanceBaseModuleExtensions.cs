using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modules.FinanceBase.Services;
using Modules.FinanceBase.DTOs;
using Modules.FinanceBase.Entities;
using Core.Authorization;
using Shared.Primitives.Financial;
using Core.Modules;
using Core.Caching;
using Shared.Primitives.Identifiers;

namespace Modules.FinanceBase;

public static class FinanceBaseModuleExtensions
{
    public static IServiceCollection AddFinanceBaseModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, FinanceBaseModuleManifestProvider>();
        services.AddSingleton<Repositories.FinanceBaseRepository>();
        services.AddSingleton<FinanceBaseService>();
        services.AddSingleton<Services.FinanceBaseCapabilities>();
        services.AddSingleton<Shared.Contracts.FinanceBase.IFinanceBaseCapabilities>(provider => provider.GetRequiredService<Services.FinanceBaseCapabilities>());
        services.AddSingleton<Services.IExchangeRateProviderRegistry, Services.ExchangeRateProviderRegistry>();
        services.AddScoped<Services.IFinanceBaseRateSyncService, Services.FinanceBaseRateSyncService>();
        services.AddSingleton<Repositories.FinanceBasePlanningOwnerSource>();
        services.AddSingleton<Shared.Contracts.Planning.IPlanningOwnerReconciliationSource>(provider => provider.GetRequiredService<Repositories.FinanceBasePlanningOwnerSource>());
        return services;
    }

    public static WebApplication MapFinanceBaseModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/finbase").WithTags("FinBase").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/currencies", async (HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, IPlatformCache cache, PlatformCacheOptions cacheOptions, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.currency.read", ct) is { } access ? await Execute(access, () => cacheOptions.StableReferenceDataEnabled
            ? cache.GetOrCreateAsync(new CacheScope(access.Tenant, null, null, null, 0), PlatformCachePolicies.FinanceBaseCurrencyReference, CacheKeyFingerprint.For("active-currencies:v1"), TimeSpan.FromHours(1), _ => service.ListCurrenciesAsync(ct), ct)
            : service.ListCurrenciesAsync(ct)) : Results.Unauthorized()).Produces<IReadOnlyList<CurrencyDto>>();
        group.MapGet("/legal-entities/{entityId:guid}/currencies", async (Guid entityId, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.currency.read", ct, entityId) is { } access ? await Execute(access, () => service.ListEntityCurrenciesAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized()).Produces<IReadOnlyList<EntityCurrencyDto>>();
        group.MapPost("/legal-entities/{entityId:guid}/currencies/{code}/enable", async (Guid entityId, string code, EnableCurrencyRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.currency.enable", ct, entityId) is { } access ? await Execute(access, () => service.EnableCurrencyAsync(access.Tenant, access.ActorId, access.MembershipId, new(entityId), code, request, Correlation(http), ct), StatusCodes.Status201Created) : Results.Unauthorized()).Produces<EntityCurrencyDto>(StatusCodes.Status201Created);
        group.MapGet("/legal-entities/{entityId:guid}/exchange-rates", async (Guid entityId, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rate.read", ct, entityId) is { } access ? await Execute(access, () => service.ListRatesAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized()).Produces<IReadOnlyList<ExchangeRateDto>>();
        group.MapGet("/legal-entities/{entityId:guid}/rate-sources", async (Guid entityId, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rate.read", ct, entityId) is { } access ? await Execute(access, () => service.ListRateSourceConfigsAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized()).Produces<IReadOnlyList<ExchangeRateSourceConfigDto>>();
        group.MapPost("/legal-entities/{entityId:guid}/rate-sources", async (Guid entityId, CreateExchangeRateSourceConfigRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rate.manage", ct, entityId) is { } access ? await Execute(access, () => service.CreateRateSourceConfigAsync(access.Tenant, access.ActorId, access.MembershipId, new(entityId), request, Correlation(http), ct), StatusCodes.Status201Created) : Results.Unauthorized()).Produces<ExchangeRateSourceConfigDto>(StatusCodes.Status201Created);
        group.MapPost("/exchange-rates", async (CreateExchangeRateRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rate.manage", ct, request.LegalEntityId) is { } access ? await Execute(access, () => service.CreateRateAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct), StatusCodes.Status201Created) : Results.Unauthorized()).Produces<ExchangeRateDto>(StatusCodes.Status201Created);
        group.MapPost("/legal-entities/{entityId:guid}/exchange-rates/{id:guid}/approve", async (Guid entityId, Guid id, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rate.approve", ct, entityId) is { } access ? await Execute(access, () => service.ApproveRateAsync(access.Tenant, access.ActorId, access.MembershipId, new(entityId), id, Correlation(http), ct)) : Results.Unauthorized()).Produces<ExchangeRateDto>();
        group.MapPost("/conversion/preview", async (ConvertMoneyRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rate.read", ct, request.LegalEntityId) is { } access ? await Execute(access, () => service.ConvertAsync(access.Tenant, request, ct)) : Results.Unauthorized()).Produces<ConvertedMoneyDto>();
        group.MapGet("/legal-entities/{entityId:guid}/financial-policy", async (Guid entityId, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rounding_policy.read", ct, entityId) is { } access ? await Execute(access, () => service.GetPolicyAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized()).Produces<FinancialPolicyDto>();
        group.MapGet("/rounding-policies", async (HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rounding_policy.read", ct) is { } access ? await Execute(access, () => service.ListRoundingPoliciesAsync(access.Tenant, ct)) : Results.Unauthorized()).Produces<IReadOnlyList<RoundingPolicyDto>>();
        group.MapPost("/rounding-policies", async (CreateRoundingPolicyRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rounding_policy.manage", ct) is { } access ? await Execute(access, () => service.CreateRoundingPolicyAsync(access.Tenant, request, Correlation(http), ct), StatusCodes.Status201Created) : Results.Unauthorized()).Produces<RoundingPolicyDto>(StatusCodes.Status201Created);
        group.MapPut("/legal-entities/{entityId:guid}/financial-policy", async (Guid entityId, UpdateFinancialPolicyRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.financial_policy.manage", ct, entityId) is { } access ? await Execute(access, () => service.UpdatePolicyAsync(access.Tenant, new(entityId), request, Correlation(http), ct)) : Results.Unauthorized()).Produces<FinancialPolicyDto>();
        group.MapPost("/payment-terms", async (CreatePaymentTermRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.payment_term.manage", ct) is { } access ? await Execute(access, () => service.CreatePaymentTermAsync(access.Tenant, request, Correlation(http), ct), StatusCodes.Status201Created) : Results.Unauthorized()).Produces<PaymentTermDto>(StatusCodes.Status201Created);
        group.MapGet("/payment-terms", async (bool? includeArchived, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.payment_term.read", ct) is { } access ? await Execute(access, () => service.ListPaymentTermsAsync(access.Tenant, includeArchived == true, ct)) : Results.Unauthorized()).Produces<IReadOnlyList<PaymentTermListItemDto>>();
        group.MapGet("/payment-terms/{termId:guid}", async (Guid termId, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.payment_term.read", ct) is { } access ? await Execute(access, () => service.GetPaymentTermHistoryAsync(access.Tenant, termId, ct)) : Results.Unauthorized()).Produces<PaymentTermHistoryDto>();
        group.MapPost("/payment-terms/{termId:guid}/versions", async (Guid termId, CreatePaymentTermVersionRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.payment_term.manage", ct) is { } access ? await Execute(access, () => service.CreatePaymentTermVersionAsync(access.Tenant, termId, request, Correlation(http), ct), StatusCodes.Status201Created) : Results.Unauthorized()).Produces<PaymentTermVersionDto>(StatusCodes.Status201Created);
        group.MapPost("/payment-terms/{termId:guid}/versions/{versionId:guid}/activate", async (Guid termId, Guid versionId, PaymentTermLifecycleRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.payment_term.manage", ct) is { } access ? await Execute(access, () => service.ActivatePaymentTermVersionAsync(access.Tenant, termId, versionId, request.ExpectedTermVersion, Correlation(http), ct)) : Results.Unauthorized()).Produces<PaymentTermVersionDto>();
        group.MapPost("/payment-terms/{termId:guid}/archive", async (Guid termId, PaymentTermLifecycleRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.payment_term.manage", ct) is { } access ? await Execute(access, async () => new { version = await service.ArchivePaymentTermAsync(access.Tenant, termId, request.ExpectedTermVersion, Correlation(http), ct) }) : Results.Unauthorized()).Produces(StatusCodes.Status200OK);
        group.MapPost("/payment-schedule/preview", async (PaymentScheduleRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.payment_term.read", ct) is { } access ? await Execute(access, () => FinanceBaseService.PreviewScheduleAsync(request, request.Rules), StatusCodes.Status200OK) : Results.Unauthorized()).Produces<IReadOnlyList<PaymentScheduleLineDto>>();
        return app;
    }

    private static async Task<Access?> Authorized(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, CancellationToken ct, Guid? entityId = null)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId) || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId) || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        var tenant = new TenantId(tenantId); var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return null;
        var scope = entityId is { } legalEntityId ? PermissionScope.ForLegalEntity(tenant, new(legalEntityId)) : PermissionScope.ForTenant(tenant);
        return new Access(tenant, actorId, membershipId, authorizer.Authorize(subject, permission, scope));
    }
    private static async Task<IResult> Execute<T>(Access access, Func<Task<T>> action, int status = StatusCodes.Status200OK)
    {
        if (!access.Allowed) return Results.Forbid();
        try { var result = await action(); return result is null ? Results.NotFound() : status == StatusCodes.Status201Created ? Results.Created("", result) : Results.Ok(result); }
        catch (FinanceBaseException ex) { return Results.Problem(statusCode: ex.Code.Contains("not_found", StringComparison.Ordinal) ? 404 : 400, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }); }
        catch (FinanceBaseDomainException ex) { return Results.Problem(statusCode: 400, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }); }
        catch (FinancialValueException ex) { return Results.Problem(statusCode: 400, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }); }
    }
    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.Request.Headers["X-Correlation-Id"], out var id) ? id : Guid.CreateVersion7();
    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed);
}

public sealed class FinanceBaseModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("finbase", "1.0.0", 2, "L1", "foundation", false, "tenant", new(["platform-kernel"], ["platform.organization"]), new("foundation_finbase", ["currency_reference", "legal_entity_currency", "financial_rounding_policy", "financial_rounding_policy_rule", "financial_policy", "financial_policy_version", "exchange_rate", "exchange_rate_source_config", "payment_term", "payment_term_version", "payment_term_rule"]), ["finbase.currency.read", "finbase.currency.enable", "finbase.rate.read", "finbase.rate.manage", "finbase.rate.approve", "finbase.rounding_policy.read", "finbase.rounding_policy.manage", "finbase.payment_term.read", "finbase.payment_term.manage", "finbase.financial_policy.manage", "finbase.audit.view"], new(["finbase.currency", "finbase.money", "finbase.exchange-rate", "finbase.payment-terms", "finbase.payment-schedule-calculation"], ["platform.organization"]), new(["FinBaseCurrencyEnabledV1", "FinBaseRateApprovedV1", "FinBaseFinancialPolicyChangedV1", "FinBasePaymentTermChangedV1", "FinBaseRoundingPolicyActivatedV1", "FinBasePaymentTermActivatedV1", "FinBasePaymentTermArchivedV1", "FinBaseRateCandidateImportedV1", "FinBaseRateSourceConfiguredV1"], []), new(true, "blocked_if_dependents"));
}
