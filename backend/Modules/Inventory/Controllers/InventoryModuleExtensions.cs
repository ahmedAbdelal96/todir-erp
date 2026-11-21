using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modules.Inventory.DTOs;
using Modules.Inventory.Repositories;
using Modules.Inventory.Services;
using Modules.Inventory.Contracts;
using Todir.Inventory.Contracts;
using Todir.Sales.Contracts;
using Shared.Contracts.Accounting;
using Shared.Primitives.Identifiers;

namespace Modules.Inventory;

public static class InventoryModuleExtensions
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, InventoryModuleManifestProvider>();
        services.AddSingleton<InventoryRepository>();
        services.AddSingleton<InventoryService>();
        services.AddSingleton<Shared.Contracts.Inventory.IInventoryCapabilities, InventoryCapabilityAdapter>();
        services.AddSingleton<IInventoryCapabilities>(provider => provider.GetRequiredService<InventoryService>());
        services.AddSingleton<Shared.Contracts.Inventory.IInventoryPurchasingCapabilities>(provider => provider.GetRequiredService<InventoryService>());
        services.AddSingleton<Shared.Contracts.Inventory.IInventoryFixedAssetCapabilities>(provider => provider.GetRequiredService<InventoryService>());
        services.AddSingleton<IInventoryAccountingRead>(provider => provider.GetRequiredService<InventoryService>());
        services.AddSingleton<ISalesInventoryCapabilities, SalesInventoryCapabilities>();
        return services;
    }

    public static WebApplication MapInventoryModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/inventory").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/legal-entities/{entityId:guid}/warehouses", async (Guid entityId, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAnyAsync(http, subjects, authorizer, ["inventory.stock.read", "inventory.warehouse.manage", "inventory.count.create", "inventory.count.enter", "inventory.count.approve", "inventory.count.post", "inventory.transfer.create", "inventory.transfer.ship", "inventory.transfer.receive", "inventory.adjustment.create", "inventory.adjustment.approve", "inventory.adjustment.post", "inventory.traceability.manage"], entityId, ct), () => repository.ListWarehousesAsync(new TenantId(Tenant(http)), entityId, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/policy", async (Guid entityId, HttpContext http, InventoryRepository repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAnyAsync(http, subjects, authorizer, ["inventory.stock.read", "inventory.policy.manage", "inventory.adjustment.create", "inventory.count.create"], entityId, ct), () => repository.GetPolicyAsync(new TenantId(Tenant(http)), entityId, ct)));
        group.MapPut("/legal-entities/{entityId:guid}/policy", async (Guid entityId, UpdateInventoryPolicyRequest request, HttpContext http, InventoryRepository repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.policy.manage", entityId, ct), () => repository.SavePolicyAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), ct)));
        group.MapGet("/legal-entities/{entityId:guid}/negative-stock-conditions", async (Guid entityId, string? status, DateTimeOffset? beforeOpenedAt, Guid? beforeId, int? limit, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.stock.read", entityId, ct), () => repository.GetNegativeStockConditionsAsync(new TenantId(Tenant(http)), entityId, status ?? "Active", beforeOpenedAt, beforeId, limit ?? 100, ct)));
        group.MapPost("/legal-entities/{entityId:guid}/warehouses", async (Guid entityId, CreateWarehouseRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.warehouse.manage", entityId, ct), () => repository.CreateWarehouseAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), ct), StatusCodes.Status201Created));
        group.MapPut("/legal-entities/{entityId:guid}/warehouses/{warehouseId:guid}/status", async (Guid entityId, Guid warehouseId, ChangeWarehouseStatusRequest request, HttpContext http, InventoryRepository repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await WarehouseAccessAsync(http, repository, subjects, authorizer, "inventory.warehouse.manage", entityId, warehouseId, ct), () => repository.ChangeWarehouseStatusAsync(new TenantId(Tenant(http)), Actor(http), entityId, warehouseId, request.Status, Correlation(http), ct)));
        group.MapGet("/legal-entities/{entityId:guid}/warehouses/{warehouseId:guid}/locations", async (Guid entityId, Guid warehouseId, HttpContext http, InventoryRepository repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await WarehouseAccessAsync(http, repository, subjects, authorizer, "inventory.stock.read", entityId, warehouseId, ct), () => repository.ListLocationsAsync(new TenantId(Tenant(http)), entityId, warehouseId, ct)));
        group.MapPost("/legal-entities/{entityId:guid}/locations", async (Guid entityId, CreateLocationRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.location.manage", entityId, ct), () => repository.CreateLocationAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), ct), StatusCodes.Status201Created));
        group.MapPut("/legal-entities/{entityId:guid}/locations/{locationId:guid}/parent", async (Guid entityId, Guid locationId, MoveLocationRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.location.manage", entityId, ct), () => repository.MoveLocationAsync(new TenantId(Tenant(http)), Actor(http), entityId, locationId, request, Correlation(http), ct)));
        group.MapDelete("/legal-entities/{entityId:guid}/locations/{locationId:guid}", async (Guid entityId, Guid locationId, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.location.manage", entityId, ct), async () => { await repository.ArchiveLocationAsync(new TenantId(Tenant(http)), Actor(http), entityId, locationId, Correlation(http), ct); return true; }));
        group.MapPut("/legal-entities/{entityId:guid}/profiles/{productVariantId:guid}", async (Guid entityId, Guid productVariantId, CreateInventoryProfileRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            if (request.ProductVariantId != productVariantId) return Results.Problem(statusCode: 400, title: "Product profile identity does not match the route.", extensions: new Dictionary<string, object?> { ["code"] = "inventory.profile_identity_mismatch" });
            return await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.profile.manage", entityId, ct), () => repository.SaveProfileAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), ct));
        });
        group.MapPost("/legal-entities/{entityId:guid}/lots", async (Guid entityId, CreateLotRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.traceability.manage", entityId, ct), () => repository.CreateLotAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), ct), StatusCodes.Status201Created));
        group.MapPost("/legal-entities/{entityId:guid}/serials", async (Guid entityId, CreateSerialRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.traceability.manage", entityId, ct), () => repository.CreateSerialAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), ct), StatusCodes.Status201Created));
        group.MapGet("/legal-entities/{entityId:guid}/balances", async (Guid entityId, Guid? productVariantId, Guid? warehouseId, int? limit, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.stock.read", entityId, ct), () => repository.GetBalancesAsync(new TenantId(Tenant(http)), entityId, productVariantId, warehouseId, limit ?? 100, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/history", async (Guid entityId, Guid? productVariantId, Guid? warehouseId, Guid? locationId, Guid? lotId, Guid? serialId, Guid? sourceDocumentId, DateTimeOffset? beforeEffectiveAt, long? beforeSequence, Guid? beforeId, int? limit, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.history.read", entityId, ct), () => repository.GetMovementHistoryAsync(new TenantId(Tenant(http)), entityId, productVariantId, warehouseId, locationId, lotId, serialId, sourceDocumentId, beforeEffectiveAt, beforeSequence, beforeId, limit ?? 100, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/stock-as-of", async (Guid entityId, DateTimeOffset asOf, Guid? productVariantId, Guid? warehouseId, int? limit, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.stock.read", entityId, ct), () => repository.GetStockAsOfAsync(new TenantId(Tenant(http)), entityId, asOf, productVariantId, warehouseId, limit ?? 100, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/reconciliation", async (Guid entityId, DateOnly asOfDate, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await CostAccessAsync(http, subjects, authorizer, "inventory.reconciliation.read", entityId, ct), () => repository.ReconcileAsync(Tenant(http), entityId, asOfDate, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/products/{productVariantId:guid}/valuation", async (Guid entityId, Guid productVariantId, DateOnly asOfDate, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await CostAccessAsync(http, subjects, authorizer, "inventory.valuation.read", entityId, ct), () => repository.GetValuationAsync(Tenant(http), new InventoryValuationQuery(entityId, productVariantId, asOfDate), ct)));
        group.MapGet("/legal-entities/{entityId:guid}/products/{productVariantId:guid}/locator", async (Guid entityId, Guid productVariantId, int? limit, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.stock.read", entityId, ct), () => repository.LocateProductAsync(new TenantId(Tenant(http)), entityId, productVariantId, limit ?? 100, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/products/{productVariantId:guid}/putaway-suggestion", async (Guid entityId, Guid productVariantId, Guid? warehouseId, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.stock.read", entityId, ct), () => repository.SuggestPutawayAsync(Tenant(http), entityId, productVariantId, warehouseId, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/products/{productVariantId:guid}/picking-suggestions", async (Guid entityId, Guid productVariantId, Guid? warehouseId, int? limit, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.stock.read", entityId, ct), () => repository.SuggestPickingAsync(Tenant(http), entityId, productVariantId, warehouseId, limit ?? 100, ct)));
        group.MapPost("/legal-entities/{entityId:guid}/scan/resolve", async (Guid entityId, ResolveInventoryScanRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.stock.read", entityId, ct), () => repository.ResolveScanAsync(Tenant(http), entityId, request.Code, ct)));
        group.MapPost("/legal-entities/{entityId:guid}/receipts/confirm", async (Guid entityId, ConfirmStockMovementRequest request, HttpContext http, InventoryRepository repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await WarehouseAccessAsync(http, repository, subjects, authorizer, "inventory.receipt.confirm", entityId, request.WarehouseId, ct), () => repository.ReceiveAsync(new TenantId(Tenant(http)), Actor(http), entityId, request with { SourceModule = "inventory" }, Correlation(http), Activity(http), ct), StatusCodes.Status201Created));
        group.MapPost("/legal-entities/{entityId:guid}/issues/confirm", async (Guid entityId, ConfirmStockMovementRequest request, HttpContext http, InventoryRepository repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await WarehouseAccessAsync(http, repository, subjects, authorizer, "inventory.issue.confirm", entityId, request.WarehouseId, ct);
            var canOverrideNegative = await HasPermissionAsync(http, subjects, authorizer, "inventory.stock.negative.override", entityId, ct);
            return await Execute(http, access, () => repository.IssueAsync(new TenantId(Tenant(http)), Actor(http), entityId, request with { SourceModule = "inventory" }, Correlation(http), Activity(http), ct, canOverrideNegative), StatusCodes.Status201Created);
        });
        group.MapPost("/legal-entities/{entityId:guid}/opening-stock/post", async (Guid entityId, ConfirmStockMovementRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.opening.post", entityId, ct), () => repository.OpenAsync(new TenantId(Tenant(http)), Actor(http), entityId, request with { SourceModule = "inventory" }, Correlation(http), Activity(http), ct), StatusCodes.Status201Created));
        group.MapPost("/legal-entities/{entityId:guid}/stock-status/change", async (Guid entityId, ChangeStockStatusRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAsync(http, subjects, authorizer, "inventory.stock.status.change", entityId, ct), () => repository.ChangeStockStatusAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), Activity(http), ct), StatusCodes.Status201Created));
        group.MapPost("/legal-entities/{entityId:guid}/adjustments", async (Guid entityId, AdjustStockRequest request, HttpContext http, InventoryRepository repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
        {
            var access = await WarehouseAccessAsync(http, repository, subjects, authorizer, "inventory.adjustment.post", entityId, request.WarehouseId, ct);
            var canOverrideNegative = await HasPermissionAsync(http, subjects, authorizer, "inventory.stock.negative.override", entityId, ct);
            return await ExecuteMutation(http, access, lifecycle, entityId, () => repository.AdjustAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), Activity(http), ct, canOverrideNegative), StatusCodes.Status201Created);
        });
        group.MapPost("/legal-entities/{entityId:guid}/adjustment-requests", async (Guid entityId, CreateAdjustmentRequest request, HttpContext http, InventoryRepository repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await WarehouseAccessAsync(http, repository, subjects, authorizer, "inventory.adjustment.create", entityId, request.WarehouseId, ct), lifecycle, entityId, () => repository.CreateAdjustmentRequestAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), ct), StatusCodes.Status201Created));
        group.MapPost("/legal-entities/{entityId:guid}/adjustment-requests/{requestId:guid}/approve", async (Guid entityId, Guid requestId, HttpContext http, InventoryRepository repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.adjustment.approve", entityId, ct), lifecycle, entityId, () => repository.ApproveAdjustmentRequestAsync(new TenantId(Tenant(http)), Actor(http), entityId, requestId, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/adjustment-requests/{requestId:guid}/post", async (Guid entityId, Guid requestId, HttpContext http, InventoryRepository repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.adjustment.post", entityId, ct), lifecycle, entityId, () => repository.PostAdjustmentRequestAsync(new TenantId(Tenant(http)), Actor(http), entityId, requestId, Correlation(http), Activity(http), ct), StatusCodes.Status201Created));
        group.MapPost("/legal-entities/{entityId:guid}/operations/reverse", async (Guid entityId, ReverseInventoryOperationRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.operation.reverse", entityId, ct), lifecycle, entityId, () => repository.ReverseOperationAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), Activity(http), ct), StatusCodes.Status201Created));
        group.MapPost("/legal-entities/{entityId:guid}/transfers", async (Guid entityId, CreateTransferRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.transfer.create", entityId, ct), lifecycle, entityId, () => repository.CreateTransferAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), ct), StatusCodes.Status201Created));
        group.MapPost("/legal-entities/{entityId:guid}/transfers/{transferId:guid}/ship", async (Guid entityId, Guid transferId, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.transfer.ship", entityId, ct), lifecycle, entityId, () => repository.ShipTransferAsync(new TenantId(Tenant(http)), Actor(http), entityId, transferId, Correlation(http), Activity(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/transfers/{transferId:guid}/receipts", async (Guid entityId, Guid transferId, ReceiveTransferRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.transfer.receive", entityId, ct), lifecycle, entityId, () => repository.ReceiveTransferAsync(new TenantId(Tenant(http)), Actor(http), entityId, transferId, request, Correlation(http), Activity(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/reservations", async (Guid entityId, ReserveStockRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.reservation.create", entityId, ct), lifecycle, entityId, () => repository.ReserveAsync(new TenantId(Tenant(http)), Actor(http), entityId, request with { SourceModule = "inventory" }, Correlation(http), ct), StatusCodes.Status201Created));
        group.MapPost("/legal-entities/{entityId:guid}/reservations/{reservationId:guid}/release", async (Guid entityId, Guid reservationId, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.reservation.release", entityId, ct), lifecycle, entityId, async () => { await repository.ReleaseReservationAsync(new TenantId(Tenant(http)), Actor(http), entityId, reservationId, Correlation(http), ct); return true; }));
        group.MapPost("/legal-entities/{entityId:guid}/reservations/{reservationId:guid}/cancel", async (Guid entityId, Guid reservationId, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.reservation.release", entityId, ct), lifecycle, entityId, async () => { await repository.CancelReservationAsync(new TenantId(Tenant(http)), Actor(http), entityId, reservationId, Correlation(http), ct); return true; }));
        group.MapPost("/legal-entities/{entityId:guid}/reservations/expire", async (Guid entityId, int? limit, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.reservation.release", entityId, ct), lifecycle, entityId, () => repository.ExpireReservationsAsync(new TenantId(Tenant(http)), entityId, limit ?? 500, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/reservations/{reservationId:guid}/consume", async (Guid entityId, Guid reservationId, ConfirmStockMovementRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.issue.confirm", entityId, ct), lifecycle, entityId, () => repository.ConsumeReservationAsync(new TenantId(Tenant(http)), Actor(http), entityId, reservationId, request with { SourceModule = "inventory" }, Correlation(http), Activity(http), ct), StatusCodes.Status201Created));
        group.MapPost("/legal-entities/{entityId:guid}/counts", async (Guid entityId, CreatePhysicalCountRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.count.create", entityId, ct), lifecycle, entityId, () => repository.CreatePhysicalCountAsync(new TenantId(Tenant(http)), Actor(http), entityId, request, Correlation(http), ct), StatusCodes.Status201Created));
        group.MapGet("/legal-entities/{entityId:guid}/counts", async (Guid entityId, Guid? warehouseId, string? status, int? page, int? pageSize, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAnyAsync(http, subjects, authorizer, ["inventory.count.create", "inventory.count.enter", "inventory.count.approve", "inventory.count.post", "inventory.stock.read"], entityId, ct), () => repository.GetPhysicalCountsAsync(new TenantId(Tenant(http)), entityId, warehouseId, status, page ?? 1, pageSize ?? 25, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/counts/{countId:guid}/lines", async (Guid entityId, Guid countId, string? search, int? page, int? pageSize, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await Execute(http, await AccessAnyAsync(http, subjects, authorizer, ["inventory.count.create", "inventory.count.enter", "inventory.count.approve", "inventory.count.post", "inventory.stock.read"], entityId, ct), () => repository.GetPhysicalCountLinesAsync(new TenantId(Tenant(http)), entityId, countId, search, page ?? 1, pageSize ?? 50, ct)));
        group.MapPost("/legal-entities/{entityId:guid}/counts/{countId:guid}/lines", async (Guid entityId, Guid countId, AddCountLineRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.count.create", entityId, ct), lifecycle, entityId, () => repository.AddPhysicalCountLineAsync(new TenantId(Tenant(http)), Actor(http), entityId, countId, request, Correlation(http), ct), StatusCodes.Status201Created));
        group.MapPut("/legal-entities/{entityId:guid}/counts/{countId:guid}/lines/{lineId:guid}", async (Guid entityId, Guid countId, Guid lineId, EnterCountLineRequest request, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
            await ExecuteMutation(http, await AccessAsync(http, subjects, authorizer, "inventory.count.enter", entityId, ct), lifecycle, entityId, () => repository.EnterPhysicalCountLineAsync(new TenantId(Tenant(http)), Actor(http), entityId, countId, lineId, request, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/counts/{countId:guid}/post", async (Guid entityId, Guid countId, HttpContext http, InventoryService repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, IModuleLifecycleEnforcer lifecycle, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "inventory.count.post", entityId, ct);
            return await ExecuteMutation(http, access, lifecycle, entityId, () => repository.PostPhysicalCountAsync(new TenantId(Tenant(http)), Actor(http), entityId, countId, RequestIdempotencyKey(http), Correlation(http), Activity(http), ct));
        });
        return app;
    }

    private static async Task<Access?> AccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId)
            || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        if (Guid.TryParse(http.User.FindFirstValue("legal_entity_id"), out var boundEntity) && boundEntity != entityId) return new(new(tenantId), actorId, false);
        var tenant = new TenantId(tenantId);
        var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        return subject is null ? null : new(tenant, actorId, authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new LegalEntityId(entityId))));
    }

    private static async Task<Access?> AccessAnyAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string[] permissions, Guid entityId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId)
            || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        if (Guid.TryParse(http.User.FindFirstValue("legal_entity_id"), out var boundEntity) && boundEntity != entityId) return new(new(tenantId), actorId, false);
        var tenant = new TenantId(tenantId);
        var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return null;
        var scope = PermissionScope.ForLegalEntity(tenant, new LegalEntityId(entityId));
        var allowed = permissions.Any(p => authorizer.Authorize(subject, p, scope));
        return new(tenant, actorId, allowed);
    }

    private static async Task<Access?> WarehouseAccessAsync(HttpContext http, InventoryRepository repository, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, Guid warehouseId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId)
            || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        if (Guid.TryParse(http.User.FindFirstValue("legal_entity_id"), out var boundEntity) && boundEntity != entityId) return new(new(tenantId), actorId, false);
        var tenant = new TenantId(tenantId);
        var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return null;
        var branchId = await repository.GetWarehouseBranchAsync(tenant, entityId, warehouseId, ct);
        var scope = branchId is { } branch ? PermissionScope.ForBranch(tenant, new LegalEntityId(entityId), new BranchId(branch)) : PermissionScope.ForLegalEntity(tenant, new LegalEntityId(entityId));
        return new(tenant, actorId, authorizer.Authorize(subject, permission, scope));
    }

    private static async Task<IResult> Execute<T>(HttpContext http, Access? access, Func<Task<T>> action, int status = StatusCodes.Status200OK)
    {
        if (access is null) return Results.Unauthorized();
        if (!access.Allowed) return Results.Forbid();
        try
        {
            var result = await action();
            return status == StatusCodes.Status201Created ? Results.Created(http.Request.Path, result) : Results.Ok(result);
        }
        catch (Entities.InventoryException exception)
        {
            var code = exception.Code;
            var statusCode = code.Contains("not_found", StringComparison.Ordinal) ? 404 : code.Contains("conflict", StringComparison.Ordinal) || code.Contains("duplicate", StringComparison.Ordinal) ? 409 : 400;
            return Results.Problem(statusCode: statusCode, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
        }
    }

    private static async Task<IResult> ExecuteMutation<T>(HttpContext http, Access? access, IModuleLifecycleEnforcer lifecycle, Guid entityId, Func<Task<T>> action, int status = StatusCodes.Status200OK)
    {
        if (access is null) return Results.Unauthorized();
        if (!access.Allowed) return Results.Forbid();
        if (!await lifecycle.CanMutateAsync(access.Tenant, "inventory", entityId, http.RequestAborted))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Module is in read-only or wind-down mode. Mutations are prohibited.", extensions: new Dictionary<string, object?> { ["code"] = "module.lifecycle_readonly" });
        }
        try
        {
            var result = await action();
            return status == StatusCodes.Status201Created ? Results.Created(http.Request.Path, result) : Results.Ok(result);
        }
        catch (Entities.InventoryException exception)
        {
            var code = exception.Code;
            var statusCode = code.Contains("not_found", StringComparison.Ordinal) ? 404 : code.Contains("conflict", StringComparison.Ordinal) || code.Contains("duplicate", StringComparison.Ordinal) ? 409 : 400;
            return Results.Problem(statusCode: statusCode, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
        }
    }

    private static Guid Tenant(HttpContext http) => Guid.Parse(http.User.FindFirstValue("tenant_id")!);
    private static Guid Actor(HttpContext http) => Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static async Task<bool> HasPermissionAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId) || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId)) return false;
        var tenant = new TenantId(tenantId); var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        return subject is not null && authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new LegalEntityId(entityId)));
    }
    private static async Task<Access?> CostAccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, CancellationToken ct)
    {
        var access = await AccessAsync(http, subjects, authorizer, permission, entityId, ct);
        if (access is null || !access.Allowed) return access;
        return await HasPermissionAsync(http, subjects, authorizer, "inventory.cost.read", entityId, ct)
            ? access
            : access with { Allowed = false };
    }
    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.Request.Headers["X-Correlation-Id"], out var value) ? value : Guid.CreateVersion7();
    private static string? Activity(HttpContext http) => System.Diagnostics.Activity.Current?.Id;
    private static string RequestIdempotencyKey(HttpContext http) => http.Request.Headers["Idempotency-Key"].ToString();
    private sealed record Access(TenantId Tenant, Guid Actor, bool Allowed);
}

public sealed class InventoryModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("inventory", "1.0.0", 1, "L2", "module", false, "tenant",
        new(["platform-kernel", "catalog", "finbase"], []),
        new("business_inventory", ["warehouse", "movement_sequence", "location", "product_inventory_profile", "lot", "serial", "inventory_operation", "stock_movement_entry", "stock_balance", "reservation", "reservation_allocation", "reservation_event", "inventory_transfer", "transfer_receipt", "transfer_event", "valuation_entry", "cost_layer", "cost_layer_consumption", "cost_state", "physical_count", "count_line", "inventory_audit", "inventory_policy", "inventory_adjustment_request"]),
        ["inventory.stock.read", "inventory.history.read", "inventory.cost.read", "inventory.valuation.read", "inventory.receipt.create", "inventory.receipt.confirm", "inventory.receipt.reverse", "inventory.issue.create", "inventory.issue.confirm", "inventory.issue.reverse", "inventory.stock.negative.override", "inventory.transfer.create", "inventory.transfer.ship", "inventory.transfer.receive", "inventory.transfer.reverse", "inventory.reservation.create", "inventory.reservation.release", "inventory.reservation.override", "inventory.count.create", "inventory.count.enter", "inventory.count.approve", "inventory.count.post", "inventory.adjustment.create", "inventory.adjustment.approve", "inventory.adjustment.post", "inventory.revaluation.manage", "inventory.warehouse.manage", "inventory.location.manage", "inventory.profile.manage", "inventory.policy.manage", "inventory.traceability.manage", "inventory.opening.manage", "inventory.opening.post", "inventory.reconciliation.read", "inventory.audit.view"],
        new(["inventory.availability", "inventory.reservations", "inventory.receipts", "inventory.issues", "inventory.transfers", "inventory.history", "inventory.valuation"], ["catalog.product", "catalog.quantity-conversion", "finbase.money", "ledger.semantic-facts", "platform.transaction-coordination", "platform.outbox"]),
        new(["StockReceivedV1", "StockIssuedV1", "StockTransferredV1", "ReservationCreatedV1", "ReservationReleasedV1", "StockAdjustedV1", "PhysicalCountPostedV1", "InventoryProfileChangedV1"], []), new(true, "blocked_if_dependents"));
}
