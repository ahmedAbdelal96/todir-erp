using Npgsql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Shared.Primitives.Identifiers;
using Core.Entitlements;
using Core.Identity;
using Core.Modules;
using Core.Services;
using Core.Transactions;
using Core.Messaging;
using System.Text;
using Core.Security;
using Core.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using MassTransit;

namespace Core.Hosting;

public static class PlatformHostingExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration, Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var connectionString = configuration.GetRuntimeConnectionString();
        services.AddRabbitMq(configuration, configureConsumers);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            if (int.TryParse(configuration["TODIR_DB_MAX_POOL_SIZE"], out var maxPoolSize) && maxPoolSize > 0)
            {
                var boundedConnection = new NpgsqlConnectionStringBuilder(connectionString) { MaxPoolSize = maxPoolSize };
                connectionString = boundedConnection.ConnectionString;
            }
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.ConnectionStringBuilder.CommandTimeout = int.TryParse(configuration["TODIR_DB_COMMAND_TIMEOUT_SECONDS"], out var commandTimeout) && commandTimeout > 0 ? commandTimeout : 15;
            services.AddSingleton(dataSourceBuilder.Build());
        }
        services.AddSingleton<IIdentityStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlIdentityStore(dataSource) : new NullIdentityStore());
        services.AddSingleton<IUserAccountAdminStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource
            ? new NpgsqlUserAccountAdminStore(dataSource, provider.GetRequiredService<IAuditWriter>(), provider.GetRequiredService<TimeProvider>())
            : new NullUserAccountAdminStore());
        services.AddSingleton<IPlatformAdminSurfaceStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlPlatformAdminSurfaceStore(dataSource) : new NullPlatformAdminSurfaceStore());
        services.AddSingleton<ILegalEntityFinancialReadStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlLegalEntityFinancialReadStore(dataSource) : new NullLegalEntityFinancialReadStore());
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
        services.AddSingleton<IAuthenticationChallengeStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlAuthenticationChallengeStore(dataSource) : new InMemoryAuthenticationChallengeStore(provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<AuthenticationFlowService>();
        services.AddSingleton<FileUploadPolicy>();
        services.AddSingleton<SupportAccessService>();
        services.AddSingleton<ISupportAccessWorkflow>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource
            ? new NpgsqlSupportAccessWorkflow(dataSource, provider.GetRequiredService<IAuditWriter>(), provider.GetRequiredService<TimeProvider>())
            : new InMemorySupportAccessWorkflow(provider.GetRequiredService<SupportAccessService>()));
        services.AddSingleton<IImportCheckpointStore, InMemoryImportCheckpointStore>();
        var entitlementKey = configuration["TODIR_ENTITLEMENT_HMAC_KEY"];
        services.AddSingleton<IEntitlementVerifier>(_ => new HmacEntitlementVerifier(string.IsNullOrWhiteSpace(entitlementKey) ? new Dictionary<string, byte[]>() : new Dictionary<string, byte[]> { ["current"] = Encoding.UTF8.GetBytes(entitlementKey) }));
        services.AddSingleton<IEntitlementGuard, EntitlementGuard>();
        if (!string.IsNullOrWhiteSpace(connectionString)) services.AddSingleton<IEntitlementSnapshotStore, NpgsqlEntitlementSnapshotStore>();
        services.AddSingleton<ICapabilityResolver, CapabilityResolver>();
        services.AddSingleton<IPermissionAuthorizer, PermissionAuthorizer>();
        services.AddSingleton<IAuthorizationSubjectStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlAuthorizationSubjectStore(dataSource) : new NullAuthorizationSubjectStore());
        var fileSigningSecret = configuration["TODIR_FILE_SIGNING_SECRET"];
        if (!string.IsNullOrWhiteSpace(fileSigningSecret)) services.AddSingleton<IFileAccessSigner>(_ => new HmacFileAccessSigner(fileSigningSecret));
        services.AddSingleton<IAuditWriter>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlAuditWriter(dataSource) : new InMemoryAuditWriter());
        services.AddSingleton<ISequenceAllocator>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlSequenceAllocator(dataSource) : new InMemorySequenceAllocator());
        services.AddSingleton<IMessageRetryPolicy, ExponentialMessageRetryPolicy>();
        services.AddSingleton<IMessageErrorStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlMessageErrorStore(dataSource) : new InMemoryMessageErrorStore());
        services.AddScoped<IMessageReplayService>(provider =>
        {
            var dataSource = provider.GetService<NpgsqlDataSource>();
            var publisher = provider.GetService<IPublishEndpoint>();
            return dataSource is not null && publisher is not null
                ? new NpgsqlMessageReplayService(dataSource, publisher, provider.GetRequiredService<IAuditWriter>())
                : new UnavailableMessageReplayService();
        });
        if (!string.IsNullOrWhiteSpace(connectionString)) services.AddSingleton<IInboxStore, NpgsqlInboxStore>();
        services.AddSingleton<ISettingsResolver, SettingsResolver>();
        services.AddSingleton<ICustomFieldRegistry, CustomFieldRegistry>();
        services.AddSingleton<IImportJobScheduler>(provider => provider.GetService<NpgsqlDataSource>() is not null
            ? new NpgsqlImportJobScheduler(provider.GetRequiredService<ITransactionCoordinator>())
            : new InMemoryImportJobScheduler());
        services.AddSingleton<INotificationStore>(provider => provider.GetService<NpgsqlDataSource>() is { } dataSource ? new NpgsqlNotificationStore(dataSource) : new InMemoryNotificationStore());
        services.AddSingleton<NotificationDispatcher>();
        if (!string.IsNullOrWhiteSpace(connectionString)) services.AddSingleton<ITransactionCoordinator, NpgsqlTransactionCoordinator>();
        services.AddSingleton<IModuleManifestProvider>(new StaticModuleManifestProvider(new ModuleManifest("platform-kernel", "1.0.0", 1, "L0", "kernel", false, "tenant", new([], []), new("platform", ["tenant-context", "authorization", "transaction-coordination"]), ["platform.role.manage", "platform.module.view", "platform.module.activate", "platform.module.deactivate", "platform.settings.manage", "platform.settings.read", "platform.audit.view", "platform.support_access.request", "platform.support_access.approve", "platform.message.replay", "platform.organization.read", "platform.identity.read", "platform.identity.manage", "platform.tenancy.read", "platform.tenancy.manage"], new(["tenant-context", "authorization", "transaction-coordination"], []), new([], []), new(false, "never_disable"))));
        services.AddSingleton<IModuleRegistry>(provider => new ModuleRegistry(provider.GetRequiredService<IEnumerable<IModuleManifestProvider>>().Select(source => source.Manifest)));
        services.AddSingleton<ModuleLifecycleService>();
        return services;
    }

    public static WebApplication MapCore(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").RequireRateLimiting("api");
        group.MapGet("/auth/csrf", (Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        });
        group.MapPost("/auth/login", async (LoginRequest request, HttpContext context, AuthenticationFlowService flow, CancellationToken cancellationToken) =>
        {
            var result = await flow.BeginAsync(request.Email, request.Password, cancellationToken);
            return await RenderAuthenticationResult(result, context);
        }).RequireRateLimiting("auth");
        group.MapPost("/auth/mfa/verify", async (MfaVerificationRequest request, HttpContext context, AuthenticationFlowService flow, CancellationToken cancellationToken) =>
        {
            var result = await flow.VerifyMfaAsync(request.ChallengeToken, request.Code, cancellationToken);
            return await RenderAuthenticationResult(result, context);
        }).RequireRateLimiting("auth");
        group.MapPost("/auth/password/change", async (ChangePasswordRequest request, HttpContext context, IIdentityStore store, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var identityId)) return Results.Unauthorized();
            return await store.ChangePasswordAsync(new(identityId), request.CurrentPassword, request.NewPassword, cancellationToken)
                ? Results.NoContent()
                : Problem(StatusCodes.Status400BadRequest, "auth.password_change_failed", "Password change could not be completed");
        }).RequireAuthorization();
        group.MapPost("/auth/password/initial", async (InitialPasswordChangeRequest request, HttpContext context, AuthenticationFlowService flow, CancellationToken cancellationToken) =>
        {
            var result = await flow.ChangePasswordAsync(request.ChallengeToken, request.NewPassword, cancellationToken);
            return await RenderAuthenticationResult(result, context);
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
        group.MapGet("/session/context", async (HttpContext context, IPlatformAdminSurfaceStore surfaceStore, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId)) return Results.Unauthorized();
            var snapshot = await surfaceStore.LoadTenantContextAsync(new(tenantId), cancellationToken);
            if (snapshot is null) return Results.NotFound();
            return Results.Ok(new { authenticated = true, tenantId, context = snapshot });
        }).RequireAuthorization();
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
        group.MapPost("/context/organization", async (OrganizationSelection request, HttpContext context, IPlatformAdminSurfaceStore surfaceStore, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId)) return Results.Unauthorized();
            if (request.LegalEntityId is not null && !Guid.TryParse(request.LegalEntityId, out _)) return Problem(StatusCodes.Status400BadRequest, "organization.legal_entity_invalid", "Legal entity identifier is invalid");
            if (request.BranchId is not null && !Guid.TryParse(request.BranchId, out _)) return Problem(StatusCodes.Status400BadRequest, "organization.branch_invalid", "Branch identifier is invalid");
            var legalEntityId = request.LegalEntityId is null ? (Guid?)null : Guid.Parse(request.LegalEntityId);
            var branchId = request.BranchId is null ? (Guid?)null : Guid.Parse(request.BranchId);
            if (!await surfaceStore.OrganizationScopeExistsAsync(new(tenantId), legalEntityId, branchId, cancellationToken)) return Results.NotFound();
            var claims = context.User.Claims.Where(claim => claim.Type is not "legal_entity_id" and not "branch_id").ToList();
            if (legalEntityId is not null) claims.Add(new Claim("legal_entity_id", legalEntityId.Value.ToString("D")));
            if (branchId is not null) claims.Add(new Claim("branch_id", branchId.Value.ToString("D")));
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.NoContent();
        }).RequireAuthorization();
        group.MapPost("/tenants/{tenantId:guid}/users", async (Guid tenantId, CreateUserRequest request, HttpContext context, IUserAccountAdminStore users, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            var tenant = new TenantId(tenantId);
            if (!await HasTenantPermissionAsync(context, tenant, "platform.identity.manage", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
            try
            {
                var result = await users.CreateAsync(tenant, request.Name, request.EmailPrefix, request.RoleIds ?? [], cancellationToken);
                return Results.Created($"/api/v1/tenants/{tenantId:D}/users/{result.User.Id:D}", result);
            }
            catch (ArgumentException exception) when (exception.Message is "identity.email_prefix_invalid" or "identity.name_invalid") { return Problem(StatusCodes.Status400BadRequest, exception.Message, "User data is invalid"); }
            catch (InvalidOperationException exception) when (exception.Message is "identity.email_prefix_already_used" or "identity.role_assignment_invalid" or "tenant.unavailable") { return Problem(StatusCodes.Status409Conflict, exception.Message, "User could not be created"); }
        }).RequireAuthorization();
        group.MapGet("/tenants/{tenantId:guid}/users", async (Guid tenantId, string? search, bool? active, int? offset, int? limit, HttpContext context, IUserAccountAdminStore users, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            var tenant = new TenantId(tenantId);
            if (!await HasTenantPermissionAsync(context, tenant, "platform.identity.read", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
            try { return Results.Ok(await users.ListAsync(tenant, search, active, offset ?? 0, limit ?? 100, cancellationToken)); }
            catch (ArgumentOutOfRangeException exception) { return Problem(StatusCodes.Status400BadRequest, "identity.pagination_invalid", exception.Message); }
        }).RequireAuthorization();
        group.MapGet("/tenants/{tenantId:guid}/users/{identityId:guid}", async (Guid tenantId, Guid identityId, HttpContext context, IUserAccountAdminStore users, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            var tenant = new TenantId(tenantId);
            if (!await HasTenantPermissionAsync(context, tenant, "platform.identity.read", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
            return await users.FindAsync(tenant, new(identityId), cancellationToken) is { } user ? Results.Ok(user) : Results.NotFound();
        }).RequireAuthorization();
        group.MapPost("/tenants/{tenantId:guid}/users/{identityId:guid}/password-reset", async (Guid tenantId, Guid identityId, HttpContext context, IUserAccountAdminStore users, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            var tenant = new TenantId(tenantId);
            if (!await HasTenantPermissionAsync(context, tenant, "platform.identity.manage", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
            return await users.ResetPasswordAsync(tenant, new(identityId), cancellationToken) is { } password ? Results.Ok(new { temporaryPassword = password }) : Results.NotFound();
        }).RequireAuthorization();
        group.MapPost("/tenants/{tenantId:guid}/users/{identityId:guid}/deactivate", (Guid tenantId, Guid identityId, HttpContext context, IUserAccountAdminStore users, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) => SetUserActiveAsync(false, tenantId, identityId, context, users, subjectStore, authorizer, cancellationToken)).RequireAuthorization();
        group.MapPost("/tenants/{tenantId:guid}/users/{identityId:guid}/reactivate", (Guid tenantId, Guid identityId, HttpContext context, IUserAccountAdminStore users, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) => SetUserActiveAsync(true, tenantId, identityId, context, users, subjectStore, authorizer, cancellationToken)).RequireAuthorization();
        group.MapGet("/tenants/{tenantId:guid}/login-domain", async (Guid tenantId, HttpContext context, IUserAccountAdminStore users, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            var tenant = new TenantId(tenantId);
            if (!await HasTenantPermissionAsync(context, tenant, "platform.tenancy.read", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
            return await users.LoadLoginDomainAsync(tenant, cancellationToken) is { } domain ? Results.Ok(new { loginDomain = domain }) : Results.NotFound();
        }).RequireAuthorization();
        group.MapPut("/tenants/{tenantId:guid}/login-domain", async (Guid tenantId, LoginDomainRequest request, HttpContext context, IUserAccountAdminStore users, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            var tenant = new TenantId(tenantId);
            if (!await HasTenantPermissionAsync(context, tenant, "platform.tenancy.manage", subjectStore, authorizer, cancellationToken) || !string.Equals(context.User.FindFirstValue("mfa_verified"), "true", StringComparison.OrdinalIgnoreCase)) return Results.Forbid();
            try { return Results.Ok(new { loginDomain = await users.ChangeLoginDomainAsync(tenant, request.LoginDomain, cancellationToken) }); }
            catch (ArgumentException exception) { return Problem(StatusCodes.Status400BadRequest, "tenant.login_domain_invalid", exception.Message); }
            catch (InvalidOperationException exception) when (exception.Message is "tenant.login_domain_already_used" or "tenant.not_found") { return Problem(StatusCodes.Status409Conflict, exception.Message, "Login domain could not be changed"); }
        }).RequireAuthorization();
        group.MapGet("/tenants/{tenantId:guid}/organization", async (Guid tenantId, HttpContext context, IPlatformAdminSurfaceStore surfaceStore, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            if (!await HasTenantPermissionAsync(context, new(tenantId), "platform.organization.read", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
            return await surfaceStore.LoadTenantContextAsync(new(tenantId), cancellationToken) is { } snapshot ? Results.Ok(snapshot) : Results.NotFound();
        }).RequireAuthorization();
        group.MapGet("/tenants/{tenantId:guid}/roles", async (Guid tenantId, int? offset, int? limit, HttpContext context, IPlatformAdminSurfaceStore surfaceStore, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            if (!await HasTenantPermissionAsync(context, new(tenantId), "platform.role.manage", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
            try { return Results.Ok(await surfaceStore.ListRolesAsync(new(tenantId), offset ?? 0, limit ?? 100, cancellationToken)); }
            catch (ArgumentOutOfRangeException exception) { return Problem(StatusCodes.Status400BadRequest, "platform.pagination_invalid", exception.Message); }
        }).RequireAuthorization();
        group.MapGet("/tenants/{tenantId:guid}/modules", async (Guid tenantId, int? offset, int? limit, HttpContext context, IPlatformAdminSurfaceStore surfaceStore, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            if (!await HasTenantPermissionAsync(context, new(tenantId), "platform.module.view", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
            try { return Results.Ok(await surfaceStore.ListModuleInstancesAsync(new(tenantId), offset ?? 0, limit ?? 100, cancellationToken)); }
            catch (ArgumentOutOfRangeException exception) { return Problem(StatusCodes.Status400BadRequest, "platform.pagination_invalid", exception.Message); }
        }).RequireAuthorization();
        group.MapGet("/tenants/{tenantId:guid}/settings", async (Guid tenantId, int? offset, int? limit, HttpContext context, IPlatformAdminSurfaceStore surfaceStore, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            if (!await HasTenantPermissionAsync(context, new(tenantId), "platform.settings.read", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
            try { return Results.Ok(await surfaceStore.ListSettingsAsync(new(tenantId), offset ?? 0, limit ?? 100, cancellationToken)); }
            catch (ArgumentOutOfRangeException exception) { return Problem(StatusCodes.Status400BadRequest, "platform.pagination_invalid", exception.Message); }
        }).RequireAuthorization();
        group.MapGet("/tenants/{tenantId:guid}/audit", async (Guid tenantId, int? offset, int? limit, HttpContext context, IPlatformAdminSurfaceStore surfaceStore, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            if (!await HasTenantPermissionAsync(context, new(tenantId), "platform.audit.view", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
            try { return Results.Ok(await surfaceStore.ListAuditAsync(new(tenantId), offset ?? 0, limit ?? 100, cancellationToken)); }
            catch (ArgumentOutOfRangeException exception) { return Problem(StatusCodes.Status400BadRequest, "platform.pagination_invalid", exception.Message); }
        }).RequireAuthorization();
        group.MapGet("/tenants/{tenantId:guid}/entitlement", async (Guid tenantId, HttpContext context, IPlatformAdminSurfaceStore surfaceStore, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            if (!await HasTenantPermissionAsync(context, new(tenantId), "platform.module.view", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
            return await surfaceStore.LoadEntitlementAsync(new(tenantId), cancellationToken) is { } entitlement ? Results.Ok(entitlement) : Results.NotFound();
        }).RequireAuthorization();
        group.MapPost("/messaging/replay", async (MessageReplayRequest request, HttpContext context, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, IMessageReplayService replay, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(request.MessageId, out var messageId) || !Guid.TryParse(request.TenantId, out var tenantId)) return Problem(StatusCodes.Status400BadRequest, "message.replay_context_invalid", "Replay context is invalid");
            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var operatorId) || !Guid.TryParse(context.User.FindFirstValue("membership_id"), out var membershipId)) return Results.Unauthorized();
            if (!string.Equals(context.User.FindFirstValue("tenant_id"), tenantId.ToString("D"), StringComparison.OrdinalIgnoreCase)) return Results.Forbid();
            if (!string.Equals(context.User.FindFirstValue("mfa_verified"), "true", StringComparison.OrdinalIgnoreCase)) return Problem(StatusCodes.Status403Forbidden, "auth.mfa_required", "Multi-factor authentication is required");
            var subject = await subjectStore.LoadAsync(new(tenantId), membershipId, cancellationToken);
            if (subject is null || !authorizer.Authorize(subject, "platform.messaging.replay", PermissionScope.ForTenant(new(tenantId)))) return Results.Forbid();
            try
            {
                await replay.ReplayAsync(messageId, new(tenantId), request.ConsumerEndpoint, operatorId, request.Reason, cancellationToken);
                return Results.NoContent();
            }
            catch (KeyNotFoundException) { return Problem(StatusCodes.Status404NotFound, "message.replay_not_found", "Message replay target was not found"); }
            catch (InvalidOperationException exception) when (exception.Message == "message.replay_not_authorized") { return Problem(StatusCodes.Status409Conflict, exception.Message, "Message replay requires an unresolved error record"); }
            catch (InvalidOperationException exception) when (exception.Message == "message.replay_unavailable") { return Problem(StatusCodes.Status503ServiceUnavailable, exception.Message, "Message replay is unavailable while messaging is not configured"); }
        }).RequireAuthorization();
        if (app.Configuration.GetValue<bool>("TODIR_STAGING_VERIFICATION") && !string.IsNullOrWhiteSpace(app.Configuration["TODIR_STAGING_REPLAY_TOKEN"]))
        {
            group.MapPost("/internal/messages/{messageId:guid}/replay", async (Guid messageId, StagingReplayRequest request, HttpContext context, IMessageReplayService replay, IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                var expected = configuration["TODIR_STAGING_REPLAY_TOKEN"];
                var supplied = context.Request.Headers["X-Staging-Replay-Token"].ToString();
                if (string.IsNullOrWhiteSpace(expected) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied))) return Results.Unauthorized();
                if (!Guid.TryParse(request.TenantId, out var tenantId) || !Guid.TryParse(request.OperatorId, out var operatorId)) return Problem(StatusCodes.Status400BadRequest, "message.replay_context_invalid", "Replay context is invalid");
                try
                {
                    await replay.ReplayAsync(messageId, new(tenantId), OutboxPublishedMessageConsumer.EndpointName, operatorId, request.Reason, cancellationToken);
                    return Results.NoContent();
                }
                catch (KeyNotFoundException) { return Problem(StatusCodes.Status404NotFound, "message.replay_not_found", "Message replay target was not found"); }
                catch (InvalidOperationException exception) when (exception.Message == "message.replay_not_authorized") { return Problem(StatusCodes.Status409Conflict, exception.Message, "Message replay requires an unresolved error record"); }
                catch (InvalidOperationException exception) when (exception.Message == "message.replay_unavailable") { return Problem(StatusCodes.Status503ServiceUnavailable, exception.Message, "Message replay is unavailable while messaging is not configured"); }
            }).RequireRateLimiting("auth");
        }
        return app;
    }

    private sealed record OrganizationSelection(string? LegalEntityId, string? BranchId);
    private sealed record LoginRequest(string Email, string Password);
    private sealed record MfaVerificationRequest(string ChallengeToken, string Code);
    private sealed record InitialPasswordChangeRequest(string ChallengeToken, string NewPassword);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    private sealed record CreateUserRequest(string Name, string EmailPrefix, Guid[]? RoleIds);
    private sealed record LoginDomainRequest(string LoginDomain);
    private sealed record MessageReplayRequest(string MessageId, string TenantId, string ConsumerEndpoint, string Reason);
    private sealed record StagingReplayRequest(string TenantId, string OperatorId, string Reason);

    private static IResult Problem(int statusCode, string code, string title) => Results.Problem(statusCode: statusCode, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });

    private static async Task<IResult> RenderAuthenticationResult(AuthenticationFlowResult result, HttpContext context)
    {
        if (result.State == AuthenticationNextStep.Rejected) return Problem(StatusCodes.Status401Unauthorized, result.ErrorCode ?? "auth.invalid_credentials", "Authentication could not be completed");
        if (result.State == AuthenticationNextStep.Authenticated && result.Session is { } session)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, session.IdentityId.Value.ToString("D")),
                new Claim("membership_id", session.MembershipId.Value.ToString("D")),
                new Claim("tenant_id", session.TenantId.Value.ToString("D")),
                new Claim("security_stamp", session.SecurityStamp.ToString("D")),
                new Claim("mfa_verified", result.MfaVerified.ToString().ToLowerInvariant())
            };
            if (session.LegalEntityId is { } legalEntity) claims = [.. claims, new Claim("legal_entity_id", legalEntity.Value.ToString("D"))];
            if (session.BranchId is { } branch) claims = [.. claims, new Claim("branch_id", branch.Value.ToString("D"))];
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Ok(new { state = result.State.ToString(), authenticated = true, tenantId = session.TenantId.Value });
        }
        return Results.Ok(new
        {
            state = result.State.ToString(),
            challengeToken = result.ChallengeToken,
            errorCode = result.ErrorCode
        });
    }

    private static async Task<IResult> SetUserActiveAsync(bool active, Guid tenantId, Guid identityId, HttpContext context, IUserAccountAdminStore users, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken)
    {
        var tenant = new TenantId(tenantId);
        if (!await HasTenantPermissionAsync(context, tenant, "platform.identity.manage", subjectStore, authorizer, cancellationToken)) return Results.Forbid();
        return await users.SetActiveAsync(tenant, new(identityId), active, cancellationToken) ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<bool> HasTenantPermissionAsync(HttpContext context, TenantId tenant, string permission, IAuthorizationSubjectStore subjectStore, IPermissionAuthorizer authorizer, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("membership_id"), out var membershipId) || !string.Equals(context.User.FindFirstValue("tenant_id"), tenant.Value.ToString("D"), StringComparison.OrdinalIgnoreCase)) return false;
        var subject = await subjectStore.LoadAsync(tenant, membershipId, cancellationToken);
        return subject is not null && authorizer.Authorize(subject, permission, PermissionScope.ForTenant(tenant));
    }
}
