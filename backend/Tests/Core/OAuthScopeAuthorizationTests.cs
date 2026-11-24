using System.Security.Claims;
using Core.Identity;
using Core.Modules;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Core.Tests;

public sealed class OAuthScopeAuthorizationTests
{
    [Fact]
    public void ReadScopeAllowsReadsButCannotAuthorizeMutations()
    {
        var principal = Principal("tenant.read");

        Assert.True(OAuthScopeAuthorization.IsAllowed(Context(HttpMethods.Get), principal));
        Assert.False(OAuthScopeAuthorization.IsAllowed(Context(HttpMethods.Post), principal));
    }

    [Fact]
    public void WriteScopeIsAReadAndMutationScope()
    {
        var principal = Principal("tenant.write");

        Assert.True(OAuthScopeAuthorization.IsAllowed(Context(HttpMethods.Get), principal));
        Assert.True(OAuthScopeAuthorization.IsAllowed(Context(HttpMethods.Post), principal));
    }

    [Fact]
    public void UnknownOrMissingResourceScopesFailClosed()
    {
        Assert.False(OAuthScopeAuthorization.IsAllowed(Context(HttpMethods.Get), Principal("openid profile")));
        Assert.False(OAuthScopeAuthorization.IsAllowed(Context(HttpMethods.Post), Principal("tenant.admin")));
        Assert.False(OAuthScopeAuthorization.IsAllowed(Context(HttpMethods.Get), new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void ExplicitReadMetadataOverridesTransportVerbForReadOnlyPreviewEndpoints()
    {
        var context = Context(HttpMethods.Post);
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new ModuleOperationMetadata("inventory", ModuleOperationKind.Read)),
            "read-preview"));

        Assert.True(OAuthScopeAuthorization.IsAllowed(context, Principal("tenant.read")));
        Assert.False(OAuthScopeAuthorization.IsAllowed(context, Principal("tenant.admin")));
    }

    private static DefaultHttpContext Context(string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        return context;
    }

    private static ClaimsPrincipal Principal(string scope)
        => new(new ClaimsIdentity([new Claim("scope", scope)], "Bearer"));
}
