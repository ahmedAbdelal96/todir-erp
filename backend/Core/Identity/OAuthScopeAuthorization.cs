using System.Security.Claims;
using Core.Modules;
using Microsoft.AspNetCore.Http;

namespace Core.Identity;

public static class OAuthScopeAuthorization
{
    public const string ReadScope = "tenant.read";
    public const string WriteScope = "tenant.write";

    public static bool IsAllowed(HttpContext context, ClaimsPrincipal principal)
    {
        var operation = context.GetEndpoint()?.Metadata.GetMetadata<ModuleOperationMetadata>();
        var isMutation = operation is not null ? operation.IsMutation : IsMutatingMethod(context.Request.Method);
        var required = isMutation ? WriteScope : ReadScope;
        var scopes = principal.FindAll("scope").Concat(principal.FindAll("scp"))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
        return scopes.Contains(WriteScope) || (required == ReadScope && scopes.Contains(ReadScope));
    }

    private static bool IsMutatingMethod(string method) => HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}
