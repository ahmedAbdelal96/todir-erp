using Npgsql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Todir.SharedKernel.Identifiers;
using Todir.Platform.Entitlements;
using Todir.Platform.Identity;
using Todir.Platform.Modules;
using Todir.Platform.Services;
using Todir.Platform.Transactions;
using Todir.Platform.Messaging;
using System.Text;
using Todir.Platform.Security;
using Todir.Platform.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace Todir.Platform.Hosting;

public static class PlatformHostingExtensions
{
    public static IServiceCollection AddTodirPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Todir") ?? configuration["TODIR_DATABASE_CONNECTION"];
        services.AddTodirRabbitMq(configuration);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.ConnectionStringBuilder.CommandTimeout = int.TryParse(configuration["TODIR_DB_COMMAND_TIMEOUT_SECONDS"], out var commandTimeout) && commandTimeout > 0 ? commandTimeout : 15;
            services.AddSingleton(dataSourceBuilder.Build());
        }
        services.AddSingleton<IIdentityStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlIdentityStore(dataSource) : new NullIdentityStore());
        if (!string.IsNullOrWhiteSpace(connectionString)) services.AddSingleton<IIdentityMembershipAdminStore, NpgsqlIdentityMembershipAdminStore>();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "todir_session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
            options.Events.OnValidatePrincipal = async context =>
            {
                var identityClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var tenantClaim = context.Principal?.FindFirstValue("tenant_id");
                var stampClaim = context.Principal?.FindFirstValue("security_stamp");
                if (!Guid.TryParse(identityClaim, out var identityId) || !Guid.TryParse(tenantClaim, out var tenantId) || !Guid.TryParse(stampClaim, out var securityStamp) || !await context.HttpContext.RequestServices.GetRequiredService<IIdentityStore>().ValidateSessionAsync(new(identityId), new(tenantId), securityStamp, context.HttpContext.RequestAborted))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            };
        });
        services.AddAuthorization();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IMfaService, MfaService>();
        services.AddDataProtection();
        services.AddSingleton<IMfaCredentialStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlMfaCredentialStore(dataSource) : new InMemoryMfaCredentialStore());
        services.AddSingleton<MfaCredentialManager>();
        services.AddSingleton<FileUploadPolicy>();
        services.AddSingleton<SupportAccessService>();
        services.AddSingleton<ISupportAccessWorkflow>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource
            ? new NpgsqlSupportAccessWorkflow(dataSource, provider.GetRequiredService<IAuditWriter>(), provider.GetRequiredService<TimeProvider>())
            : new InMemorySupportAccessWorkflow(provider.GetRequiredService<SupportAccessService>()));
        services.AddSingleton<IImportCheckpointStore, InMemoryImportCheckpointStore>();
        var entitlementKey = configuration["TODIR_ENTITLEMENT_HMAC_KEY"];
        services.AddSingleton<IEntitlementVerifier>(_ => new HmacEntitlementVerifier(string.IsNullOrWhiteSpace(entitlementKey) ? new Dictionary<string, byte[]>() : new Dictionary<string, byte[]> { ["current"] = Encoding.UTF8.GetBytes(entitlementKey) }));
        services.AddSingleton<IEntitlementGuard, EntitlementGuard>();
        services.AddSingleton<ICapabilityResolver, CapabilityResolver>();
        services.AddSingleton<IPermissionAuthorizer, PermissionAuthorizer>();
        services.AddSingleton<IAuthorizationSubjectStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlAuthorizationSubjectStore(dataSource) : new NullAuthorizationSubjectStore());
        var fileSigningSecret = configuration["TODIR_FILE_SIGNING_SECRET"];
        if (!string.IsNullOrWhiteSpace(fileSigningSecret)) services.AddSingleton<IFileAccessSigner>(_ => new HmacFileAccessSigner(fileSigningSecret));
        services.AddSingleton<IAuditWriter>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlAuditWriter(dataSource) : new InMemoryAuditWriter());
        services.AddSingleton<ISequenceAllocator>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlSequenceAllocator(dataSource) : new InMemorySequenceAllocator());
        services.AddSingleton<IMessageRetryPolicy, ExponentialMessageRetryPolicy>();
        services.AddSingleton<IMessageErrorStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlMessageErrorStore(dataSource) : new InMemoryMessageErrorStore());
        if (!string.IsNullOrWhiteSpace(connectionString)) services.AddSingleton<IMessageReplayService, NpgsqlMessageReplayService>();
        if (!string.IsNullOrWhiteSpace(connectionString)) services.AddSingleton<IInboxStore, NpgsqlInboxStore>();
        services.AddSingleton<ISettingsResolver, SettingsResolver>();
        services.AddSingleton<ICustomFieldRegistry, CustomFieldRegistry>();
        services.AddSingleton<IImportJobScheduler>(provider => provider.GetService<NpgsqlDataSource>() is not null
            ? new NpgsqlImportJobScheduler(provider.GetRequiredService<ITransactionCoordinator>())
            : new InMemoryImportJobScheduler());
        services.AddSingleton<INotificationStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlNotificationStore(dataSource) : new InMemoryNotificationStore());
        services.AddSingleton<NotificationDispatcher>();
        if (!string.IsNullOrWhiteSpace(connectionString)) services.AddSingleton<ITransactionCoordinator, NpgsqlTransactionCoordinator>();
        services.AddSingleton<IModuleRegistry>(_ => new ModuleRegistry([
            new ModuleManifest("tenancy", "1.0.0", 1, "L0", "kernel", false, "tenant", new([], []), new("platform_tenancy", ["tenant"]), [], new([], []), new([], []), new(false, "never_disable")),
            new ModuleManifest("iam", "1.0.0", 1, "L0", "kernel", false, "tenant", new(["tenancy"], []), new("platform_iam", ["identity", "membership"]), [], new([], []), new([], []), new(false, "never_disable")),
            new ModuleManifest("org", "1.0.0", 1, "L0", "kernel", false, "tenant", new(["tenancy"], []), new("platform_org", ["legal_entity", "branch"]), [], new([], []), new([], []), new(false, "never_disable"))
        ]));
        services.AddSingleton<ModuleLifecycleService>();
        return services;
    }

    public static WebApplication MapTodirPlatform(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").RequireRateLimiting("api");
        group.MapGet("/auth/csrf", (Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        }).RequireRateLimiting("auth");
        group.MapPost("/auth/login", async (LoginRequest request, HttpContext context, IIdentityStore store, ISessionService sessions, MfaCredentialManager mfa, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var identity = await store.AuthenticateAsync(request.Email, request.Password, cancellationToken);
            if (identity is null) return Results.Unauthorized();
            if (!Guid.TryParse(request.TenantId, out var selectedTenant) || selectedTenant == Guid.Empty) return Problem(StatusCodes.Status400BadRequest, "tenant.required", "Tenant selection is required");
            var selected = await store.FindActiveMembershipAsync(identity.Id, new(selectedTenant), cancellationToken);
            if (selected is null) return Results.Unauthorized();
            var mfaEnabled = await mfa.IsEnabledAsync(identity.Id, cancellationToken);
            if (mfaEnabled && !await mfa.VerifyAsync(identity.Id, request.MfaCode, clock.GetUtcNow(), cancellationToken)) return Problem(StatusCodes.Status401Unauthorized, "auth.mfa_required", "Valid multi-factor authentication is required");
            var session = await sessions.CreateAsync(identity, selected, cancellationToken);
            if (session is null) return Results.Unauthorized();
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, identity.Id.Value.ToString("D")), new Claim("membership_id", selected.Id.Value.ToString("D")), new Claim("tenant_id", selected.TenantId.Value.ToString("D")), new Claim("security_stamp", identity.SecurityStamp.ToString("D")), new Claim("mfa_verified", mfaEnabled.ToString().ToLowerInvariant()) };
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Ok(new { authenticated = true, tenantId = selected.TenantId.Value });
        }).RequireRateLimiting("auth");
        group.MapPost("/auth/mfa/enroll", async (HttpContext context, MfaCredentialManager mfa, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var identityId)) return Results.Unauthorized();
            var enrollment = await mfa.EnrollAsync(new(identityId), cancellationToken);
            return Results.Ok(new { secretBase32 = enrollment.SecretBase32, recoveryCodes = enrollment.RecoveryCodes });
        }).RequireAuthorization();
        group.MapPost("/auth/logout", async (HttpContext context) => { await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.NoContent(); }).RequireAuthorization();
        group.MapPost("/auth/sessions/revoke", async (HttpContext context, IIdentityStore store, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var identityId)) return Results.Unauthorized();
            await store.RevokeSessionsAsync(new(identityId), cancellationToken);
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();
        group.MapGet("/modules", (IModuleRegistry registry) => Results.Ok(registry.GetAll()));
        group.MapGet("/session", (HttpContext context) => context.User.Identity?.IsAuthenticated == true ? Results.Ok(new { authenticated = true, tenantId = context.User.FindFirstValue("tenant_id") }) : Results.Unauthorized()).RequireAuthorization();
        group.MapGet("/authorization/check", async (string permission, HttpContext context, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId) || !Guid.TryParse(context.User.FindFirstValue("membership_id"), out var membershipId)) return Results.Unauthorized();
            var subject = await subjectStore.LoadAsync(new(tenantId), membershipId, cancellationToken);
            if (subject is null) return Results.Forbid();
            var scope = Guid.TryParse(context.User.FindFirstValue("branch_id"), out var branchId) && Guid.TryParse(context.User.FindFirstValue("legal_entity_id"), out var entityId)
                ? PermissionScope.ForBranch(new(tenantId), new(entityId), new(branchId))
                : Guid.TryParse(context.User.FindFirstValue("legal_entity_id"), out entityId)
                    ? PermissionScope.ForLegalEntity(new(tenantId), new(entityId))
                    : PermissionScope.ForTenant(new(tenantId));
            return authorizer.Authorize(subject, permission, scope) ? Results.Ok(new { authorized = true }) : Results.Forbid();
        }).RequireAuthorization();
        group.MapPost("/context/tenant", async (TenantSelection request, HttpContext context, IIdentityStore store, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(request.TenantId, out var tenantId) || tenantId == Guid.Empty) return Problem(StatusCodes.Status400BadRequest, "tenant.invalid", "Tenant identifier is invalid");
            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var identityId)) return Results.Unauthorized();
            var membership = await store.FindActiveMembershipAsync(new(identityId), new(tenantId), cancellationToken);
            if (membership is null) return Results.Forbid();
            var claims = context.User.Claims.Where(claim => claim.Type is not "tenant_id" and not "membership_id" and not "legal_entity_id" and not "branch_id").ToList();
            claims.Add(new Claim("tenant_id", membership.TenantId.Value.ToString("D")));
            claims.Add(new Claim("membership_id", membership.Id.Value.ToString("D")));
            if (membership.DefaultLegalEntityId is { } legalEntity) claims.Add(new Claim("legal_entity_id", legalEntity.Value.ToString("D")));
            if (membership.DefaultBranchId is { } branch) claims.Add(new Claim("branch_id", branch.Value.ToString("D")));
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.NoContent();
        }).RequireAuthorization();
        if (app.Configuration.GetValue<bool>("TODIR_STAGING_VERIFICATION"))
        {
            group.MapPost("/internal/messages/{messageId:guid}/replay", async (Guid messageId, ReplayRequest request, HttpContext context, IMessageReplayService replay, IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                var expected = configuration["TODIR_STAGING_REPLAY_TOKEN"];
                var supplied = context.Request.Headers["X-Staging-Replay-Token"].ToString();
                if (string.IsNullOrWhiteSpace(expected) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied))) return Results.Unauthorized();
                if (!Guid.TryParse(request.TenantId, out var tenantId) || !Guid.TryParse(request.OperatorId, out var operatorId)) return Problem(StatusCodes.Status400BadRequest, "message.replay_context_invalid", "Replay context is invalid");
                try
                {
                    await replay.ReplayAsync(messageId, new(tenantId), operatorId, request.Reason, cancellationToken);
                    return Results.NoContent();
                }
                catch (KeyNotFoundException) { return Problem(StatusCodes.Status404NotFound, "message.replay_not_found", "Message replay target was not found"); }
                catch (InvalidOperationException exception) when (exception.Message == "message.replay_not_authorized") { return Problem(StatusCodes.Status409Conflict, exception.Message, "Message replay requires an unresolved error record"); }
            }).RequireRateLimiting("auth");
        }
        return app;
    }

    private sealed record TenantSelection(string TenantId);
    private sealed record LoginRequest(string Email, string Password, string? TenantId, string? MfaCode);
    private sealed record ReplayRequest(string TenantId, string OperatorId, string Reason);

    private static IResult Problem(int statusCode, string code, string title) => Results.Problem(statusCode: statusCode, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
}
