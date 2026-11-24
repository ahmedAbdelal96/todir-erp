using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;
using Shared.Primitives.Identifiers;

namespace Core.Identity;

/// <summary>
/// Requires every authenticated API request to prove that its server-side session
/// is still active. This keeps cookie and bearer authorization subject to the same
/// PostgreSQL security-stamp, tenant, account-status, and revocation authority.
/// </summary>
public sealed class SessionAuthorityRequirement : IAuthorizationRequirement;

public sealed class SessionAuthorityHandler(ISessionService sessions, IMobileSessionService mobileSessions) : AuthorizationHandler<SessionAuthorityRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SessionAuthorityRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue(OpenIddictConstants.Claims.Subject), out var identityId) ||
            !Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId))
        {
            return;
        }

        var securityStamp = Guid.TryParse(context.User.FindFirstValue("security_stamp"), out var parsedStamp) ? parsedStamp : Guid.Empty;
        var valid = Guid.TryParse(context.User.FindFirstValue("mobile_session_id"), out var mobileSessionId)
            ? await mobileSessions.ValidateAsync(mobileSessionId, new(identityId), new(tenantId), securityStamp, CancellationToken.None)
            : Guid.TryParse(context.User.FindFirstValue("session_id"), out var sessionId)
                && await sessions.ValidateAsync(sessionId, new(identityId), new(tenantId), securityStamp, CancellationToken.None);
        if (valid)
        {
            context.Succeed(requirement);
        }
    }
}
