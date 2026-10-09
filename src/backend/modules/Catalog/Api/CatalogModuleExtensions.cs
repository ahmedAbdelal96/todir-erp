using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Todir.Modules.Catalog.Application;
using Todir.Modules.Catalog.Contracts;
using Todir.Modules.Catalog.Domain;
using Todir.Platform.Authorization;
using Todir.Platform.Modules;
using Todir.SharedKernel.Identifiers;

namespace Todir.Modules.Catalog;

public static class CatalogModuleExtensions
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, CatalogModuleManifestProvider>();
        services.AddSingleton<ProductService>();
        services.AddSingleton<CatalogMasterDataService>();
        services.AddSingleton<CatalogDetailService>();
        return services;
    }

    public static WebApplication MapCatalogModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/catalog").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/products", async (HttpContext http, ProductService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string? search, ProductNature? nature, CatalogStatus? status, Guid? categoryId, Guid? brandId, int? page, int? pageSize, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.read", ct) is { } access ? await Execute(http, access, () => service.ListAsync(access.Tenant, search, nature, status, categoryId, brandId, page ?? 1, pageSize ?? 25, ct)) : Results.Unauthorized());
        group.MapGet("/products/{productId:guid}", async (Guid productId, HttpContext http, ProductService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.read", ct) is { } access ? await Execute(http, access, () => service.GetAsync(access.Tenant, productId, ct)) : Results.Unauthorized());
        group.MapPost("/products", async (CreateProductRequest request, HttpContext http, ProductService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.create", ct) is { } access ? await Execute(http, access, () => service.CreateAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ActivityId(http), ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapPatch("/products/{productId:guid}", async (Guid productId, UpdateProductRequest request, HttpContext http, ProductService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.edit", ct) is { } access ? await Execute(http, access, () => service.UpdateAsync(access.Tenant, access.ActorId, access.MembershipId, productId, request, Correlation(http), ActivityId(http), ct)) : Results.Unauthorized());
        group.MapPost("/products/{productId:guid}/archive", async (Guid productId, CatalogVersionRequest request, HttpContext http, ProductService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.archive", ct) is { } access ? await Execute(http, access, () => service.ChangeStatusAsync(access.Tenant, access.ActorId, access.MembershipId, productId, false, request.Version, Correlation(http), ActivityId(http), ct)) : Results.Unauthorized());
        group.MapPost("/products/{productId:guid}/restore", async (Guid productId, CatalogVersionRequest request, HttpContext http, ProductService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.edit", ct) is { } access ? await Execute(http, access, () => service.ChangeStatusAsync(access.Tenant, access.ActorId, access.MembershipId, productId, true, request.Version, Correlation(http), ActivityId(http), ct)) : Results.Unauthorized());
        group.MapPost("/products/{productId:guid}/barcodes", async (Guid productId, AddBarcodeRequest request, HttpContext http, ProductService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.barcode.manage", ct) is { } access ? await Execute(http, access, () => service.AddBarcodeAsync(access.Tenant, access.ActorId, access.MembershipId, productId, null, request, Correlation(http), ct)) : Results.Unauthorized());
        group.MapPost("/products/{productId:guid}/variants/{variantId:guid}/barcodes", async (Guid productId, Guid variantId, AddBarcodeRequest request, HttpContext http, ProductService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.barcode.manage", ct) is { } access ? await Execute(http, access, () => service.AddBarcodeAsync(access.Tenant, access.ActorId, access.MembershipId, productId, variantId, request, Correlation(http), ct)) : Results.Unauthorized());
        group.MapGet("/products/{productId:guid}/readiness", async (Guid productId, HttpContext http, CatalogDetailService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.read", ct) is { } access ? await Execute(http, access, () => service.ReadinessAsync(access.Tenant, productId, ct)) : Results.Unauthorized());
        group.MapGet("/resolve/{value}", async (string value, HttpContext http, CatalogDetailService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.read", ct) is { } access ? await Execute(http, access, () => service.ResolveAsync(access.Tenant, value, ct)) : Results.Unauthorized());
        group.MapPost("/products/{productId:guid}/variants", async (Guid productId, CreateVariantRequest request, HttpContext http, CatalogDetailService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.edit", ct) is { } access ? await Execute(http, access, () => service.CreateVariantAsync(access.Tenant, productId, request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapPost("/products/{productId:guid}/variants/{variantId:guid}/uoms", async (Guid productId, Guid variantId, AddProductUomRequest request, HttpContext http, CatalogDetailService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.edit", ct) is { } access ? await Execute(http, access, () => service.AddUomAsync(access.Tenant, productId, variantId, request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapGet("/variants/{variantId:guid}/uoms", async (Guid variantId, HttpContext http, CatalogDetailService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.read", ct) is { } access ? await Execute(http, access, () => service.ListUomsAsync(access.Tenant, variantId, ct)) : Results.Unauthorized());
        group.MapPost("/products/{productId:guid}/aliases", async (Guid productId, AddAliasRequest request, HttpContext http, CatalogDetailService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.edit", ct) is { } access ? await Execute(http, access, () => service.AddAliasAsync(access.Tenant, productId, request, ct)) : Results.Unauthorized());
        group.MapPost("/products/{productId:guid}/translations", async (Guid productId, AddTranslationRequest request, HttpContext http, CatalogDetailService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.edit", ct) is { } access ? await Execute(http, access, () => service.AddTranslationAsync(access.Tenant, productId, request, ct)) : Results.Unauthorized());
        group.MapPost("/products/{productId:guid}/references", async (Guid productId, AddReferenceRequest request, HttpContext http, CatalogDetailService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.reference.manage", ct) is { } access ? await Execute(http, access, () => service.AddReferenceAsync(access.Tenant, productId, request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapGet("/categories", async (HttpContext http, CatalogMasterDataService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.read", ct) is { } access ? await Execute(http, access, () => service.ListCategoriesAsync(access.Tenant, ct)) : Results.Unauthorized());
        group.MapPost("/categories", async (CreateCategoryRequest request, HttpContext http, CatalogMasterDataService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.category.manage", ct) is { } access ? await Execute(http, access, () => service.CreateCategoryAsync(access.Tenant, request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapGet("/brands", async (HttpContext http, CatalogMasterDataService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.read", ct) is { } access ? await Execute(http, access, () => service.ListBrandsAsync(access.Tenant, ct)) : Results.Unauthorized());
        group.MapPost("/brands", async (CreateBrandRequest request, HttpContext http, CatalogMasterDataService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.brand.manage", ct) is { } access ? await Execute(http, access, () => service.CreateBrandAsync(access.Tenant, request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapGet("/uoms", async (HttpContext http, CatalogMasterDataService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.product.read", ct) is { } access ? await Execute(http, access, () => service.ListUomsAsync(access.Tenant, ct)) : Results.Unauthorized());
        group.MapPost("/uoms", async (CreateUomRequest request, HttpContext http, CatalogMasterDataService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await AccessAsync(http, subjects, authorizer, "catalog.uom.manage", ct) is { } access ? await Execute(http, access, () => service.CreateUomAsync(access.Tenant, request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        return app;
    }

    private static async Task<Access?> AccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId) || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId) || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        var tenant = new TenantId(tenantId); var subject = await subjects.LoadAsync(tenant, membershipId, ct); if (subject is null) return null; return new Access(tenant, actorId, membershipId, authorizer.Authorize(subject, permission, PermissionScope.ForTenant(tenant)));
    }
    private static async Task<IResult> Execute<T>(HttpContext http, Access access, Func<Task<T>> action, int successStatus = StatusCodes.Status200OK)
    {
        if (!access.Allowed) return Results.Forbid();
        try { var result = await action(); if (result is null) return Results.NotFound(); return successStatus == StatusCodes.Status201Created ? Results.Created(http.Request.Path, result) : Results.Ok(result); }
        catch (CatalogException ex) { var status = ex.Code.Contains("duplicate", StringComparison.Ordinal) || ex.Code.Contains("concurrency", StringComparison.Ordinal) ? 409 : 400; return Results.Problem(statusCode: status, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }
    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.Request.Headers["X-Correlation-Id"], out var value) ? value : Guid.CreateVersion7();
    private static string? ActivityId(HttpContext http) => System.Diagnostics.Activity.Current?.Id;
    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed);
}

public sealed class CatalogModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("catalog", "1.0.0", 1, "L1", "foundation", false, "tenant", new(["platform-kernel"], []), new("foundation_catalog", ["product_template", "product_variant", "product_category", "brand", "uom", "product_uom", "product_barcode", "product_alias", "product_translation", "product_reference", "product_visibility", "catalog_search_projection"]), ["catalog.product.read", "catalog.product.create", "catalog.product.edit", "catalog.product.archive", "catalog.category.manage", "catalog.brand.manage", "catalog.uom.manage", "catalog.barcode.manage", "catalog.reference.manage", "catalog.audit.view"], new(["catalog.product", "catalog.product-picker", "catalog.quantity-conversion"], []), new(["CatalogProductCreatedV1", "CatalogProductChangedV1", "CatalogProductArchivedV1", "CatalogProductRestoredV1", "CatalogProductUomChangedV1", "CatalogProductBarcodeChangedV1"], []), new(true, "blocked_if_dependents"));
}
