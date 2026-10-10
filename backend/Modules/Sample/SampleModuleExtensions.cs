using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Core.Authorization;
using Core.Entitlements;
using Core.Modules;
using Shared.Primitives.Identifiers;

namespace Modules.Sample;

public static class SampleModuleExtensions
{
    public static IServiceCollection AddSampleModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, SampleModuleManifestProvider>();
        services.AddSingleton<SampleItemService>();
        services.AddSingleton<SampleProjectionConsumer>();
        return services;
    }

    public static void ConfigureSampleMessaging(IBusRegistrationConfigurator bus)
    {
        bus.AddConsumer<SampleProjectionConsumer, SampleProjectionConsumerDefinition>();
    }

    public static WebApplication MapSampleModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/sample").RequireAuthorization().RequireRateLimiting("api");
        group.MapPost("/items", async (
            CreateSampleItemRequest request,
            HttpContext context,
            IEntitlementGuard entitlements,
            IEntitlementSnapshotStore? snapshotStore,
            IAuthorizationSubjectStore subjectStore,
            IPermissionAuthorizer authorizer,
            SampleItemService service,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId)
                || !Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)
                || !Guid.TryParse(context.User.FindFirstValue("membership_id"), out var membershipId)) return Results.Unauthorized();
            var tenant = new TenantId(tenantId);
            if (!entitlements.IsEntitled(tenant, "sample") && snapshotStore is not null && await snapshotStore.LoadLatestAsync(tenant, cancellationToken) is { } snapshot)
                entitlements.SetSnapshot(snapshot);
            if (!entitlements.IsEntitled(tenant, "sample")) return Results.Forbid();
            var subject = await subjectStore.LoadAsync(new TenantId(tenantId), membershipId, cancellationToken);
            if (subject is null || !authorizer.Authorize(subject, "sample.item.create", PermissionScope.ForTenant(new TenantId(tenantId)))) return Results.Forbid();
            try
            {
                var item = await service.CreateAsync(new TenantId(tenantId), actorId, membershipId, request.Name, GetCorrelationId(context), System.Diagnostics.Activity.Current?.Id, cancellationToken);
                return Results.Created($"/api/v1/sample/items/{item.Id:D}", item);
            }
            catch (ArgumentException exception)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message, extensions: new Dictionary<string, object?> { ["code"] = exception.Message });
            }
        });
        return app;
    }

    private static Guid GetCorrelationId(HttpContext context) => Guid.TryParse(context.Request.Headers["X-Correlation-Id"], out var correlationId) ? correlationId : Guid.CreateVersion7();
}
