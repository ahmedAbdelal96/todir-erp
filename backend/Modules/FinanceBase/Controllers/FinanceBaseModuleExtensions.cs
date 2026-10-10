using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modules.FinanceBase.Services;
using Modules.FinanceBase.DTOs;
using Modules.FinanceBase.Entities;
using Core.Authorization;
using Core.Modules;
using Shared.Primitives.Identifiers;

namespace Modules.FinanceBase;

public static class FinanceBaseModuleExtensions
{
    public static IServiceCollection AddFinanceBaseModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, FinanceBaseModuleManifestProvider>();
        services.AddSingleton<FinanceBaseService>();
        return services;
    }

    public static WebApplication MapFinanceBaseModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/finbase").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/currencies", async (HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.currency.read", ct) is { } access ? await Execute(access, () => service.ListCurrenciesAsync(ct)) : Results.Unauthorized());
        group.MapGet("/legal-entities/{entityId:guid}/currencies", async (Guid entityId, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.currency.read", ct) is { } access ? await Execute(access, () => service.ListEntityCurrenciesAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized());
        group.MapPost("/legal-entities/{entityId:guid}/currencies/{code}/enable", async (Guid entityId, string code, EnableCurrencyRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.currency.enable", ct) is { } access ? await Execute(access, () => service.EnableCurrencyAsync(access.Tenant, access.ActorId, access.MembershipId, new(entityId), code, request, Correlation(http), ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapGet("/legal-entities/{entityId:guid}/exchange-rates", async (Guid entityId, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rate.read", ct) is { } access ? await Execute(access, () => service.ListRatesAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized());
        group.MapPost("/exchange-rates", async (CreateExchangeRateRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rate.manage", ct) is { } access ? await Execute(access, () => service.CreateRateAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapPost("/exchange-rates/{id:guid}/approve", async (Guid id, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rate.approve", ct) is { } access ? await Execute(access, () => service.ApproveRateAsync(access.Tenant, access.ActorId, access.MembershipId, id, Correlation(http), ct)) : Results.Unauthorized());
        group.MapPost("/conversion/preview", async (ConvertMoneyRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rate.read", ct) is { } access ? await Execute(access, () => service.ConvertAsync(access.Tenant, request, ct)) : Results.Unauthorized());
        group.MapGet("/legal-entities/{entityId:guid}/financial-policy", async (Guid entityId, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rounding_policy.read", ct) is { } access ? await Execute(access, () => service.GetPolicyAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized());
        group.MapPost("/rounding-policies", async (CreateRoundingPolicyRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.rounding_policy.manage", ct) is { } access ? await Execute(access, () => service.CreateRoundingPolicyAsync(access.Tenant, request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapPut("/legal-entities/{entityId:guid}/financial-policy", async (Guid entityId, UpdateFinancialPolicyRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.financial_policy.manage", ct) is { } access ? await Execute(access, () => service.UpdatePolicyAsync(access.Tenant, new(entityId), request, ct)) : Results.Unauthorized());
        group.MapPost("/payment-terms", async (CreatePaymentTermRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.payment_term.manage", ct) is { } access ? await Execute(access, () => service.CreatePaymentTermAsync(access.Tenant, request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapPost("/payment-schedule/preview", async (PaymentScheduleRequest request, HttpContext http, FinanceBaseService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer auth, CancellationToken ct) => await Authorized(http, subjects, auth, "finbase.payment_term.read", ct) is { } access ? await Execute(access, () => FinanceBaseService.PreviewScheduleAsync(request, request.Rules), StatusCodes.Status200OK) : Results.Unauthorized());
        return app;
    }

    private static async Task<Access?> Authorized(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId) || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId) || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        var tenant = new TenantId(tenantId); var subject = await subjects.LoadAsync(tenant, membershipId, ct); return subject is null ? null : new Access(tenant, actorId, membershipId, authorizer.Authorize(subject, permission, PermissionScope.ForTenant(tenant)));
    }
    private static async Task<IResult> Execute<T>(Access access, Func<Task<T>> action, int status = StatusCodes.Status200OK)
    {
        if (!access.Allowed) return Results.Forbid();
        try { var result = await action(); return result is null ? Results.NotFound() : status == StatusCodes.Status201Created ? Results.Created("", result) : Results.Ok(result); }
        catch (FinanceBaseException ex) { return Results.Problem(statusCode: ex.Code.Contains("not_found", StringComparison.Ordinal) ? 404 : 400, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }); }
        catch (FinanceBaseDomainException ex) { return Results.Problem(statusCode: 400, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }); }
    }
    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.Request.Headers["X-Correlation-Id"], out var id) ? id : Guid.CreateVersion7();
    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed);
}

public sealed class FinanceBaseModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("finbase", "1.0.0", 1, "L1", "foundation", false, "tenant", new(["platform-kernel"], ["platform.organization"]), new("foundation_finbase", ["currency_reference", "legal_entity_currency", "financial_rounding_policy", "financial_policy", "exchange_rate", "payment_term", "payment_term_version", "payment_term_rule"]), ["finbase.currency.read", "finbase.currency.enable", "finbase.rate.read", "finbase.rate.manage", "finbase.rate.approve", "finbase.rounding_policy.read", "finbase.rounding_policy.manage", "finbase.payment_term.read", "finbase.payment_term.manage", "finbase.financial_policy.manage", "finbase.audit.view"], new(["finbase.currency", "finbase.money", "finbase.exchange-rate", "finbase.payment-terms"], ["platform.organization"]), new(["FinBaseCurrencyEnabledV1", "FinBaseRateApprovedV1", "FinBaseFinancialPolicyChangedV1", "FinBasePaymentTermChangedV1"], []), new(true, "blocked_if_dependents"));
}
