using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shared.Primitives.Identifiers;

namespace Core.Modules;

public enum ModuleOperationKind
{
    Read,
    Mutation,
    Administrative
}

public sealed class ModuleOperationMetadata(string moduleId, ModuleOperationKind kind, string? permission = null)
{
    public string ModuleId { get; } = moduleId;
    public ModuleOperationKind Kind { get; } = kind;
    public string? Permission { get; } = permission;
    public bool IsMutation => Kind is ModuleOperationKind.Mutation or ModuleOperationKind.Administrative;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ModuleLifecycleEnforcedAttribute : Attribute { }

public static class ModuleEndpointExtensions
{
    public static RouteHandlerBuilder RequireModuleMutation(this RouteHandlerBuilder builder, string moduleId, string? permission = null)
        => builder.WithMetadata(new ModuleOperationMetadata(moduleId, ModuleOperationKind.Mutation, permission));

    public static RouteHandlerBuilder RequireModuleRead(this RouteHandlerBuilder builder, string moduleId, string? permission = null)
        => builder.WithMetadata(new ModuleOperationMetadata(moduleId, ModuleOperationKind.Read, permission));

    public static RouteGroupBuilder AddModuleLifecycleEnforcement(this RouteGroupBuilder builder)
    {
        builder.WithMetadata(new ModuleLifecycleEnforcedAttribute());
        return builder.AddEndpointFilter<ModuleLifecycleEndpointFilter>();
    }
}

public sealed class ModuleLifecycleEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var endpoint = http.GetEndpoint();
        var metadata = endpoint?.Metadata.GetMetadata<ModuleOperationMetadata>();
        if (metadata is null)
        {
            var path = http.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/api/v1/inventory", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/v1/ledger", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/v1/accounting", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/v1/banking", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/v1/crm", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/v1/sales", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/v1/purchasing", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/v1/pos", StringComparison.OrdinalIgnoreCase))
            {
                if (!path.EndsWith("/target", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "module.missing_metadata",
                        extensions: new Dictionary<string, object?> { ["code"] = "module.missing_metadata" });
                }
            }
            return await next(context);
        }

        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId))
        {
            return Results.Unauthorized();
        }

        Guid? routeEntityId = null;
        if (http.GetRouteValue("entityId") is string eStr && Guid.TryParse(eStr, out var eGuid) && eGuid != Guid.Empty)
        {
            routeEntityId = eGuid;
        }
        else if (http.GetRouteValue("legalEntityId") is string leStr && Guid.TryParse(leStr, out var leGuid) && leGuid != Guid.Empty)
        {
            routeEntityId = leGuid;
        }

        Guid? queryEntityId = null;
        if (http.Request.Query.TryGetValue("legalEntityId", out var qVal) && Guid.TryParse(qVal, out var qGuid) && qGuid != Guid.Empty)
        {
            queryEntityId = qGuid;
        }

        Guid? headerEntityId = null;
        if (http.Request.Headers.TryGetValue("X-Legal-Entity-Id", out var hVal) && Guid.TryParse(hVal, out var hGuid) && hGuid != Guid.Empty)
        {
            headerEntityId = hGuid;
        }

        Guid? dtoEntityId = null;
        foreach (var arg in context.Arguments)
        {
            if (arg is null) continue;
            var prop = arg.GetType().GetProperty("LegalEntityId");
            if (prop is not null && prop.PropertyType == typeof(Guid))
            {
                var val = (Guid?)prop.GetValue(arg);
                if (val.HasValue && val.Value != Guid.Empty)
                {
                    dtoEntityId = val.Value;
                    break;
                }
            }
        }

        var detectedIds = new HashSet<Guid>();
        if (routeEntityId.HasValue) detectedIds.Add(routeEntityId.Value);
        if (queryEntityId.HasValue) detectedIds.Add(queryEntityId.Value);
        if (headerEntityId.HasValue) detectedIds.Add(headerEntityId.Value);
        if (dtoEntityId.HasValue) detectedIds.Add(dtoEntityId.Value);

        if (detectedIds.Count > 1)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "scope.conflict",
                extensions: new Dictionary<string, object?> { ["code"] = "scope.conflict" });
        }

        var legalEntityId = detectedIds.Count == 1 ? (Guid?)detectedIds.First() : null;

        var routePattern = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? string.Empty;
        var expectsEntity = routePattern.Contains("{entityId", StringComparison.OrdinalIgnoreCase) ||
                            routePattern.Contains("{legalEntityId", StringComparison.OrdinalIgnoreCase);
        if (expectsEntity && legalEntityId is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "scope.missing_required_entity",
                extensions: new Dictionary<string, object?> { ["code"] = "scope.missing_required_entity" });
        }

        var lifecycle = http.RequestServices.GetRequiredService<IModuleLifecycleEnforcer>();
        var decision = await lifecycle.EvaluateAccessAsync(new TenantId(tenantId), metadata.ModuleId, legalEntityId, metadata.IsMutation, http.RequestAborted);
        if (!decision.Allowed)
        {
            var code = decision.DeniedReason ?? (metadata.IsMutation ? "module.lifecycle_readonly" : "module.disabled");
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
        }

        return await next(context);
    }
}
