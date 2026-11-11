using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modules.FixedAssets;
using Modules.FixedAssets.Services;
using Shared.Primitives.Identifiers;

namespace Modules.FixedAssets.Controllers;

public static class FixedAssetsModuleExtensions
{
    public static IServiceCollection AddFixedAssetsModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, FixedAssetsModuleManifestProvider>();
        services.AddSingleton<FixedAssetRepository>();
        services.AddSingleton<FixedAssetService>();
        return services;
    }

    public static WebApplication MapFixedAssetsModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/fixed-assets").WithTags("Fixed Assets").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/categories", async (int? limit, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "assets.read", ct), access => service.ListCategoriesAsync(access.Tenant, limit ?? 100, ct)));
        group.MapPost("/categories", async (CreateAssetCategoryRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "assets.book.manage", ct), access => service.CreateCategoryAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapGet("/assets", async (Guid? legalEntityId, string? state, string? search, int? limit, int? offset, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "assets.read", ct), access => service.ListAssetsAsync(access.Tenant, legalEntityId, state, search, limit ?? 50, offset ?? 0, ct)));
        group.MapPost("/assets", async (CreateAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, subjects, authorizer, "assets.create", request.LegalEntityId, ct), access => service.CreateAssetAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/assignments", async (Guid assetId, AssignAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "assets.assign", ct), access => service.AssignAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapGet("/books", async (Guid? legalEntityId, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "assets.financial.read", ct), access => service.ListBooksAsync(access.Tenant, legalEntityId, ct)));
        group.MapPost("/books", async (CreateBookRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, subjects, authorizer, "assets.book.manage", request.LegalEntityId, ct), access => service.CreateBookAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/books", async (Guid assetId, CreateAssetBookRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, subjects, authorizer, "assets.book.manage", request.LegalEntityId, ct), access => service.AddAssetBookAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapPost("/depreciation-runs/prepare", async (CreateDepreciationRunRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, subjects, authorizer, "assets.depreciation.prepare", request.LegalEntityId, ct), access => service.PrepareDepreciationRunAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/depreciation-runs/{runId:guid}/post", async (Guid runId, Guid legalEntityId, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, subjects, authorizer, "assets.depreciation.post", legalEntityId, ct), access => service.PostDepreciationRunAsync(access.Tenant, access.ActorId, runId, ct)));
        group.MapPost("/assets/{assetId:guid}/disposal", async (Guid assetId, DisposeAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, subjects, authorizer, "assets.disposal.manage", request.LegalEntityId, ct), access => service.DisposeAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        return app;
    }

    private static async Task<IResult> Execute<T>(HttpContext http, Access? access, Func<Access, Task<T>> action)
    {
        if (access is null) return Results.Unauthorized();
        try { return Results.Ok(await action(access)); }
        catch (FixedAssetException exception) { return Results.Problem(statusCode: 400, title: exception.Code, extensions: new Dictionary<string, object?> { ["code"] = exception.Code }); }
    }

    private static async Task<Access?> TenantAccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, CancellationToken ct)
    {
        if (!TryIdentity(http, out var tenantId, out var membershipId, out var actorId)) return null;
        var tenant = new TenantId(tenantId); var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        return subject is not null && authorizer.Authorize(subject, permission, PermissionScope.ForTenant(tenant)) ? new(tenant, actorId, membershipId) : null;
    }

    private static async Task<Access?> EntityAccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, CancellationToken ct)
    {
        if (!TryIdentity(http, out var tenantId, out var membershipId, out var actorId)) return null;
        var tenant = new TenantId(tenantId); var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        return subject is not null && authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new LegalEntityId(entityId))) ? new(tenant, actorId, membershipId) : null;
    }

    private static bool TryIdentity(HttpContext http, out Guid tenantId, out Guid membershipId, out Guid actorId)
    {
        var tenantOk = Guid.TryParse(http.User.FindFirstValue("tenant_id"), out tenantId);
        var membershipOk = Guid.TryParse(http.User.FindFirstValue("membership_id"), out membershipId);
        var actorOk = Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
        return tenantOk && membershipOk && actorOk;
    }
    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId);
}

public sealed class FixedAssetsModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new(
        "fixed-assets", "1.0.0", 1, "L4", "business-module", true, "tenant",
        new(["platform-kernel", "finbase", "ledger"], ["catalog.product", "inventory.history", "hr.core", "platform.files", "platform.approvals", "platform.outbox"]),
        new("business_assets", ["asset_category", "asset_category_version", "fixed_asset", "asset_location", "asset_assignment", "asset_handover", "asset_book_definition", "asset_book", "asset_book_transaction", "asset_depreciation_run", "asset_depreciation_run_line", "asset_capitalization_candidate", "asset_source_allocation", "asset_cip_cost_source", "asset_verification_campaign", "asset_verification_line", "asset_disposal", "asset_opening_import", "asset_opening_import_line", "asset_audit_event", "asset_idempotency_key"]),
        ["assets.read", "assets.financial.read", "assets.create", "assets.edit", "assets.assign", "assets.capitalize", "assets.book.manage", "assets.depreciation.prepare", "assets.depreciation.post", "assets.depreciation.reverse", "assets.disposal.manage", "assets.verification.manage", "assets.opening.manage", "assets.audit.read", "assets.export"],
        new(["fixed-assets.registry", "fixed-assets.custody", "fixed-assets.books", "fixed-assets.depreciation", "fixed-assets.capitalization", "fixed-assets.reconciliation"], ["platform.organization", "platform.numbering", "finbase.financial-context", "ledger.semantic-facts", "catalog.product", "inventory.history", "hr.core", "platform.files", "platform.approvals", "platform.outbox"]),
        new(["FixedAssetCreatedV1", "FixedAssetAssignedV1", "FixedAssetCapitalizedV1", "FixedAssetDepreciatedV1", "FixedAssetDisposedV1"], ["StockIssuedV1", "InvoicePostedV1", "ExpenseFinalizedV1", "EmployeeOffboardingStartedV1"]), new(true, "blocked_if_dependents"));
}
