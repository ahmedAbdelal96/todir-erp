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

public static class ModuleEndpointExtensions
{
    public static RouteHandlerBuilder RequireModuleMutation(this RouteHandlerBuilder builder, string moduleId, string? permission = null)
        => builder.WithMetadata(new ModuleOperationMetadata(moduleId, ModuleOperationKind.Mutation, permission));

    public static RouteHandlerBuilder RequireModuleRead(this RouteHandlerBuilder builder, string moduleId, string? permission = null)
        => builder.WithMetadata(new ModuleOperationMetadata(moduleId, ModuleOperationKind.Read, permission));

    public static RouteGroupBuilder AddModuleLifecycleEnforcement(this RouteGroupBuilder builder)
        => builder.AddEndpointFilter<ModuleLifecycleEndpointFilter>();
}

public sealed class ModuleLifecycleEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var metadata = http.GetEndpoint()?.Metadata.GetMetadata<ModuleOperationMetadata>();
        if (metadata is null)
        {
            return await next(context);
        }

        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId))
        {
            return Results.Unauthorized();
        }

        Guid? legalEntityId = null;
        if (http.GetRouteValue("entityId") is string eStr && Guid.TryParse(eStr, out var eGuid))
        {
            legalEntityId = eGuid;
        }
        else if (http.GetRouteValue("legalEntityId") is string leStr && Guid.TryParse(leStr, out var leGuid))
        {
            legalEntityId = leGuid;
        }
        else if (http.Request.Query.TryGetValue("legalEntityId", out var qVal) && Guid.TryParse(qVal, out var qGuid))
        {
            legalEntityId = qGuid;
        }
        else if (http.Request.Headers.TryGetValue("X-Legal-Entity-Id", out var hVal) && Guid.TryParse(hVal, out var hGuid))
        {
            legalEntityId = hGuid;
        }

        if (legalEntityId is null)
        {
            foreach (var arg in context.Arguments)
            {
                if (arg is null) continue;
                var prop = arg.GetType().GetProperty("LegalEntityId");
                if (prop is not null && prop.PropertyType == typeof(Guid))
                {
                    var val = (Guid?)prop.GetValue(arg);
                    if (val.HasValue && val.Value != Guid.Empty)
                    {
                        legalEntityId = val.Value;
                        break;
                    }
                }
            }
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
