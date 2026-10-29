using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Purchasing.DTOs;
using Modules.Purchasing.Entities;
using Modules.Purchasing.Services;
using Shared.Primitives.Identifiers;

namespace Modules.Purchasing;

public static class PurchasingModuleExtensions
{
    public static IServiceCollection AddPurchasingModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, PurchasingModuleManifestProvider>();
        services.AddSingleton<Repositories.PurchasingRepository>();
        services.AddSingleton<PurchasingService>();
        services.AddSingleton<Shared.Contracts.Purchasing.IPurchasingSourceCapabilities>(p => p.GetRequiredService<PurchasingService>());
        services.AddSingleton<Shared.Contracts.Purchasing.IPurchasingReadCapabilities>(p => p.GetRequiredService<PurchasingService>());
        return services;
    }

    public static WebApplication MapPurchasingModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/purchasing").WithTags("Purchasing").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/legal-entities/{entityId:guid}/purchase-orders", async (Guid entityId, string? state, int? page, int? pageSize, HttpContext http, PurchasingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "purchasing.po.read", entityId, ct), () => service.ListAsync(Tenant(http), entityId, state, page ?? 1, pageSize ?? 50, ct)));
        group.MapPost("/legal-entities/{entityId:guid}/purchase-orders", async (Guid entityId, CreatePurchaseOrderRequest request, HttpContext http, PurchasingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            if (request.LegalEntityId != entityId) return Results.BadRequest(new { code = "purchasing.entity_mismatch" });
            var access = await AccessAsync(http, subjects, authorizer, "purchasing.po.create", entityId, ct);
            if (access is { Allowed: true } && request.Lines.Any(x => x.UnitPrice is not null) && !await HasPermissionAsync(http, subjects, authorizer, "purchasing.price.override", entityId, ct)) return Results.Forbid();
            var canOverride = access?.Allowed == true && await HasPriceOverride(http, subjects, authorizer, entityId, ct);
            return await Execute(http, access, () => service.CreateAsync(Tenant(http), Actor(http), request, canOverride, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/legal-entities/{entityId:guid}/purchase-orders/{id:guid}", async (Guid entityId, Guid id, HttpContext http, PurchasingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "purchasing.po.read", entityId, ct), () => service.GetAsync(Tenant(http), entityId, id, ct)));
        group.MapPut("/legal-entities/{entityId:guid}/purchase-orders/{id:guid}", async (Guid entityId, Guid id, UpdatePurchaseOrderRequest request, HttpContext http, PurchasingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "purchasing.po.edit", entityId, ct);
            if (access is { Allowed: true } && request.Lines.Any(x => x.UnitPrice is not null) && !await HasPermissionAsync(http, subjects, authorizer, "purchasing.price.override", entityId, ct)) return Results.Forbid();
            return await Execute(http, access, () => service.UpdateAsync(Tenant(http), Actor(http), entityId, id, request, false, Correlation(http), ct));
        });
        group.MapPost("/legal-entities/{entityId:guid}/purchase-orders/{id:guid}/amend", async (Guid entityId, Guid id, UpdatePurchaseOrderRequest request, HttpContext http, PurchasingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "purchasing.po.amend", entityId, ct), () => service.UpdateAsync(Tenant(http), Actor(http), entityId, id, request, true, Correlation(http), ct, true, "Controlled amendment")));
        group.MapPost("/legal-entities/{entityId:guid}/purchase-orders/{id:guid}/approve", async (Guid entityId, Guid id, PurchaseOrderCommandRequest request, HttpContext http, PurchasingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "purchasing.po.approve", entityId, ct), () => service.ApproveAsync(Tenant(http), Actor(http), id, request, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/purchase-orders/{id:guid}/reject", async (Guid entityId, Guid id, PurchaseOrderCommandRequest request, HttpContext http, PurchasingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "purchasing.po.reject", entityId, ct), () => service.RejectAsync(Tenant(http), Actor(http), id, request, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/purchase-orders/{id:guid}/cancel-remaining", async (Guid entityId, Guid id, PurchaseOrderCommandRequest request, HttpContext http, PurchasingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "purchasing.po.cancel", entityId, ct), () => service.CancelAsync(Tenant(http), Actor(http), id, request, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/purchase-orders/{id:guid}/close", async (Guid entityId, Guid id, PurchaseOrderCommandRequest request, HttpContext http, PurchasingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "purchasing.po.close", entityId, ct), () => service.CloseAsync(Tenant(http), Actor(http), id, request, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/purchase-orders/{id:guid}/create-supplier-invoice", async (Guid entityId, Guid id, CreateSupplierInvoiceRequest request, HttpContext http, PurchasingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "purchasing.invoice.create_from_po", entityId, ct), () => service.CreateSupplierInvoiceAsync(Tenant(http), Actor(http), Membership(http), entityId, id, request, Correlation(http), ct), StatusCodes.Status201Created));
        return app;
    }

    private static async Task<IResult> Execute<T>(HttpContext http, Access? access, Func<Task<T>> action, int status = StatusCodes.Status200OK)
    { if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid(); try { var result = await action(); return status == StatusCodes.Status201Created ? Results.Created(http.Request.Path, result) : Results.Ok(result); } catch (PurchasingException ex) { var statusCode = ex.Code.Contains("not_found", StringComparison.Ordinal) ? 404 : ex.Code.Contains("stale", StringComparison.Ordinal) || ex.Code.Contains("conflict", StringComparison.Ordinal) || ex.Code.Contains("already", StringComparison.Ordinal) ? 409 : 400; return Results.Problem(statusCode: statusCode, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }); } }
    private static async Task<Access?> AccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entity, CancellationToken ct)
    { if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenant) || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membership) || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return null; var subject = await subjects.LoadAsync(new TenantId(tenant), membership, ct); return subject is null ? null : new(new TenantId(tenant), actor, membership, authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(new TenantId(tenant), new LegalEntityId(entity)))); }
    private static async Task<bool> HasPermissionAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entity, CancellationToken ct) => (await AccessAsync(http, subjects, authorizer, permission, entity, ct))?.Allowed == true;
    private static Task<bool> HasPriceOverride(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Guid entity, CancellationToken ct) => HasPermissionAsync(http, subjects, authorizer, "purchasing.price.override", entity, ct);
    private static TenantId Tenant(HttpContext http) => new(Guid.Parse(http.User.FindFirstValue("tenant_id")!));
    private static Guid Actor(HttpContext http) => Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid Membership(HttpContext http) => Guid.Parse(http.User.FindFirstValue("membership_id")!);
    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.Request.Headers["X-Correlation-ID"], out var value) ? value : Guid.CreateVersion7();
    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed);
}

public sealed class PurchasingModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("purchasing", "1.0.0", 1, "L3", "module", false, "tenant", new(["platform-kernel", "directory", "catalog", "finbase", "pricing", "tax", "invoicing"], ["inventory.receipts", "platform.outbox"]), new("business_purchasing", ["supplier_purchase_profile", "purchase_settings", "purchase_order", "purchase_order_line", "purchase_order_state_history", "purchase_order_approval", "purchase_order_amendment", "purchase_order_invoice_link", "purchase_order_receipt_link", "purchase_order_match_result", "purchase_order_exception"]), ["purchasing.po.read", "purchasing.po.create", "purchasing.po.edit", "purchasing.po.approve", "purchasing.po.reject", "purchasing.po.amend", "purchasing.po.cancel", "purchasing.po.close", "purchasing.price.read", "purchasing.price.override", "purchasing.receipt.request", "purchasing.receipt.view", "purchasing.invoice.create_from_po", "purchasing.invoice.view_link", "purchasing.match.read", "purchasing.match.override", "purchasing.supplier_profile.manage", "purchasing.settings.manage", "purchasing.history.read", "purchasing.audit.read"], new(["purchasing.purchase-orders", "purchasing.supplier-profile", "purchasing.invoice-source"], ["directory.party", "catalog.product-uom", "pricing.batch-resolution", "tax.batch-calculation", "finbase.money", "invoicing.source-draft", "platform.audit", "platform.transaction-coordination", "platform.outbox"]), new(["PurchaseOrderApprovedV1", "PurchaseOrderAmendedV1", "PurchaseOrderCancelledV1", "PurchaseOrderClosedV1", "PurchaseInvoiceRequestedV1", "PurchaseInvoiceLinkedV1"], []), new(false, "blocked_if_dependents"));
}
