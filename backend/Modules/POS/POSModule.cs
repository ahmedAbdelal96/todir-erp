using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Core.Transactions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Catalog;
using Shared.Contracts.Pricing;
using Shared.Contracts.Tax;
using Shared.Contracts.Payments;
using MassTransit;

namespace Modules.POS;

public sealed record CreateRegisterRequest(Guid LegalEntityId, Guid BranchId, string Code, string Name, string CurrencyCode, bool OfflineEnabled = false);
public sealed record RegisterResponse(Guid Id, Guid LegalEntityId, Guid BranchId, string Code, string Name, string CurrencyCode, string Status, bool OfflineEnabled);
public sealed record OpenSessionRequest(Guid RegisterId, Guid? TerminalId, Guid CashierUserId, DateOnly BusinessDate, decimal OpeningFloat, string CurrencyCode);
public sealed record RegisterTerminalRequest(Guid RegisterId, string DeviceIdentity, string DisplayName);
public sealed record TerminalResponse(Guid Id, Guid RegisterId, string DeviceIdentity, string DisplayName, string Status, int CredentialVersion, DateTimeOffset? LastSeenAt);
public sealed record SessionResponse(Guid Id, Guid RegisterId, Guid? TerminalId, DateOnly BusinessDate, string Status, decimal OpeningFloat, decimal? CountedCash, decimal? Variance);
public sealed record CreateCartRequest(Guid SessionId, string CurrencyCode, Guid? CustomerPartyId = null);
public sealed record AddCartLineRequest(Guid VariantId, string Description, string UomCode, decimal Quantity, decimal UnitPrice, decimal TaxAmount = 0, decimal DiscountAmount = 0, int? ExpectedVersion = null, Guid? LegalEntityId = null, Guid? ProductTemplateId = null, Guid? UnitOfMeasureId = null, Guid? PriceListId = null, DateOnly? PricingDate = null);
public sealed record CartResponse(Guid Id, Guid SessionId, string State, int Version, decimal Subtotal, decimal TaxTotal, decimal DiscountTotal, decimal GrandTotal);
public sealed record TenderRequest(string Method, decimal Amount, string? ExternalReference = null, Guid? PaymentAccountId = null, Guid? ProviderConfigId = null);
public sealed record CheckoutRequest(Guid CartId, string IdempotencyKey, IReadOnlyList<TenderRequest> Tenders);
public sealed record CheckoutResponse(Guid CheckoutId, Guid? SaleId, string State, string PaymentState, string InventoryState, string DocumentState, string AccountingState, decimal Total, string CurrencyCode);
public sealed record CashMovementRequest(Guid SessionId, string MovementType, decimal Amount, string CurrencyCode, string Reason);
public sealed record CloseSessionRequest(Guid SessionId, decimal CountedCash, string? VarianceReason = null);
public sealed record CreateReturnRequest(Guid OriginalSaleId, Guid SessionId, IReadOnlyList<ReturnLineRequest> Lines, string Reason, string Disposition);
public sealed record ReturnLineRequest(Guid OriginalSaleLineId, decimal Quantity, decimal Amount, Guid? LotId = null, Guid? SerialId = null);
public sealed record OfflineEnvelopeRequest(Guid RegisterId, string DeviceIdentity, long SourceSequence, string IdempotencyKey, long PackageVersion, string Payload, string Signature);

public static class PosModuleExtensions
{
    public static IServiceCollection AddPosModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, PosModuleManifestProvider>();
        services.AddSingleton<PosService>();
        services.AddSingleton<PosRecoveryConsumer>();
        return services;
    }

    public static void ConfigurePosMessaging(IBusRegistrationConfigurator bus) => bus.AddConsumer<PosRecoveryConsumer, PosRecoveryConsumerDefinition>();

    public static WebApplication MapPosModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/pos").WithTags("POS").RequireAuthorization().RequireRateLimiting("api");
        group.MapPost("/legal-entities/{entityId:guid}/registers", async (Guid entityId, CreateRegisterRequest request, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "pos.register.manage", entityId, ct), () => service.CreateRegisterAsync(Tenant(http), Actor(http), entityId, request, ct), StatusCodes.Status201Created));
        group.MapGet("/legal-entities/{entityId:guid}/registers", async (Guid entityId, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "pos.register.read", entityId, ct), () => service.ListRegistersAsync(Tenant(http), entityId, ct)));
        group.MapPost("/registers/{registerId:guid}/terminals", async (Guid registerId, RegisterTerminalRequest request, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.register.manage", ct), () => service.RegisterTerminalAsync(Tenant(http), registerId, request, ct), StatusCodes.Status201Created));
        group.MapPost("/legal-entities/{entityId:guid}/sessions", async (Guid entityId, OpenSessionRequest request, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "pos.session.open", entityId, ct), () => service.OpenSessionAsync(Tenant(http), Actor(http), entityId, request, ct), StatusCodes.Status201Created));
        group.MapPost("/sessions/{sessionId:guid}/cash-movements", async (Guid sessionId, CashMovementRequest request, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.cash.manage", ct), () => service.AddCashMovementAsync(Tenant(http), Actor(http), sessionId, request, ct), StatusCodes.Status201Created));
        group.MapPost("/sessions/{sessionId:guid}/close", async (Guid sessionId, CloseSessionRequest request, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.session.close", ct), () => service.CloseSessionAsync(Tenant(http), Actor(http), sessionId, request, ct)));
        group.MapPost("/carts", async (CreateCartRequest request, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.cart.manage", ct), () => service.CreateCartAsync(Tenant(http), Actor(http), request, ct), StatusCodes.Status201Created));
        group.MapPost("/carts/{cartId:guid}/lines", async (Guid cartId, AddCartLineRequest request, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.cart.manage", ct), () => service.AddCartLineAsync(Tenant(http), cartId, request, ct), StatusCodes.Status201Created));
        group.MapPost("/carts/{cartId:guid}/hold", async (Guid cartId, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.cart.manage", ct), () => service.ChangeCartStateAsync(Tenant(http), cartId, "Held", ct)));
        group.MapPost("/carts/{cartId:guid}/resume", async (Guid cartId, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.cart.manage", ct), () => service.ChangeCartStateAsync(Tenant(http), cartId, "Active", ct)));
        group.MapPost("/carts/{cartId:guid}/cancel", async (Guid cartId, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.cart.manage", ct), () => service.ChangeCartStateAsync(Tenant(http), cartId, "Cancelled", ct)));
        group.MapPost("/checkout", async (CheckoutRequest request, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.checkout.create", ct), () => service.CheckoutAsync(Tenant(http), Actor(http), Membership(http), request, ct), StatusCodes.Status202Accepted));
        group.MapGet("/checkouts/{checkoutId:guid}", async (Guid checkoutId, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.checkout.create", ct), () => service.GetCheckoutAsync(Tenant(http), checkoutId, ct)));
        group.MapPost("/checkouts/{checkoutId:guid}/recover", async (Guid checkoutId, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.checkout.create", ct), () => service.RecoverCheckoutAsync(Tenant(http), checkoutId, ct)));
        group.MapPost("/returns", async (CreateReturnRequest request, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.return.create", ct), () => service.CreateReturnAsync(Tenant(http), Actor(http), request, ct), StatusCodes.Status201Created));
        group.MapPost("/returns/{returnId:guid}/recover", async (Guid returnId, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.return.create", ct), () => service.RecoverReturnAsync(Tenant(http), Actor(http), returnId, ct), StatusCodes.Status200OK));
        group.MapPost("/offline/envelopes", async (OfflineEnvelopeRequest request, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "pos.offline.sync", ct), () => service.ReceiveOfflineEnvelopeAsync(Tenant(http), request, ct), StatusCodes.Status202Accepted));
        group.MapGet("/legal-entities/{entityId:guid}/reports/sales", async (Guid entityId, DateOnly? from, DateOnly? to, int? page, int? pageSize, HttpContext http, PosService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "pos.report.read", entityId, ct), () => service.ListSalesAsync(Tenant(http), entityId, from, to, page ?? 1, pageSize ?? 50, ct)));
        return app;
    }

    private static TenantId Tenant(HttpContext context) => Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenant) ? new TenantId(tenant) : throw new UnauthorizedAccessException();
    private static Guid Actor(HttpContext context) => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) ? actor : throw new UnauthorizedAccessException();
    private static Guid Membership(HttpContext context) => Guid.TryParse(context.User.FindFirstValue("membership_id"), out var membership) ? membership : throw new UnauthorizedAccessException();
    private static async Task<IResult> Execute<T>(HttpContext context, Access? access, Func<Task<T>> action, int successCode = StatusCodes.Status200OK)
    {
        if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
        try { var value = await action(); return Results.Json(value, statusCode: successCode); }
        catch (ArgumentException ex) { return Results.Problem(ex.Message, statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: 409, extensions: new Dictionary<string, object?> { ["code"] = ex.Message }); }
    }
    private static async Task<Access?> AccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entity, CancellationToken ct)
    { if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenant) || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membership) || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return null; if (Guid.TryParse(http.User.FindFirstValue("legal_entity_id"), out var bound) && bound != entity) return new(new(tenant), actor, false, membership); var subject = await subjects.LoadAsync(new TenantId(tenant), membership, ct); return subject is null ? null : new(new TenantId(tenant), actor, authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(new TenantId(tenant), new LegalEntityId(entity))), membership); }
    private static async Task<Access?> TenantAccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, CancellationToken ct)
    { if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenant) || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membership) || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return null; var subject = await subjects.LoadAsync(new TenantId(tenant), membership, ct); return subject is null ? null : new(new TenantId(tenant), actor, authorizer.Authorize(subject, permission, PermissionScope.ForTenant(new TenantId(tenant))), membership); }
    private sealed record Access(TenantId Tenant, Guid Actor, bool Allowed, Guid MembershipId);
}

public sealed class PosModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("pos", "1.0.0", 1, "L4", "business-module", true, "tenant", new(["platform-kernel", "directory", "catalog", "pricing", "tax", "inventory", "payments", "invoicing", "ledger"], ["platform.audit", "platform.outbox", "platform.transaction-coordination"]), new("business_pos", ["pos_register", "pos_terminal", "pos_session", "pos_cash_movement", "pos_cart", "pos_cart_line", "pos_checkout", "pos_tender", "pos_sale", "pos_sale_line", "pos_return", "pos_return_line", "pos_manager_override", "pos_offline_package", "pos_offline_envelope", "pos_sync_conflict"]), ["pos.register.read", "pos.register.manage", "pos.session.open", "pos.session.close", "pos.cart.manage", "pos.checkout.create", "pos.return.create", "pos.cash.manage", "pos.report.read", "pos.offline.sync", "pos.override.approve"], new(["pos.checkout", "pos.cash-session", "pos.offline-sync"], ["catalog.lookup", "pricing.resolve", "tax.calculate", "inventory.issue", "payments.money-movement", "invoicing.documents", "ledger.semantic-facts", "directory.party"]), new(["PosSaleCompletedV1", "PosCheckoutRecoveryRequiredV1", "PosOfflineConflictV1"], ["PaymentCompletedV1"]), new(true, "blocked_if_dependents"));
}
