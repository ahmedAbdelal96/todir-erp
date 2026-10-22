using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Modules.Directory.Services;
using Modules.Directory.DTOs;
using Modules.Directory.Entities;
using Core.Authorization;
using Core.Modules;
using Shared.Primitives.Identifiers;

namespace Modules.Directory;

public static class DirectoryModuleExtensions
{
    public static IServiceCollection AddDirectoryModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, DirectoryModuleManifestProvider>();
        services.AddSingleton<Repositories.DirectoryRepository>();
        services.AddSingleton<Repositories.DirectoryAdvancedRepository>();
        services.AddSingleton<DirectoryService>();
        services.AddSingleton<Services.DirectoryCapabilities>();
        services.AddSingleton<Shared.Contracts.Directory.IDirectoryCapabilities>(provider => provider.GetRequiredService<Services.DirectoryCapabilities>());
        services.AddSingleton<Shared.Contracts.Directory.IDirectoryImportCapabilities>(provider => provider.GetRequiredService<Services.DirectoryCapabilities>());
        return services;
    }

    public static WebApplication MapDirectoryModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/directory").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/parties", async (HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string? search, string? partyType, string? role, string? status, int? page, int? pageSize, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.read", ct) is { } access
                ? await Execute(http, access, () => service.ListVisibleAsync(access.Tenant, search, Parse<PartyType>(partyType), Parse<PartyRoleCode>(role), Parse<PartyStatus>(status), access.LegalEntityId, page ?? 1, pageSize ?? 25, ct))
                : Results.Unauthorized());
        group.MapGet("/parties/{partyId:guid}", async (Guid partyId, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.read", ct) is { } access
                ? await Execute(http, access, () => service.GetVisibleAsync(access.Tenant, partyId, access.LegalEntityId, ct), nullAsNotFound: true)
                : Results.Unauthorized());
        group.MapPost("/parties", async (CreatePartyRequest request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.create", ct) is { } access
                ? await Execute(http, access, () => service.CreateAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ActivityId(http), access.DuplicateOverride, http.Request.Headers["Idempotency-Key"].ToString(), ct), StatusCodes.Status201Created)
                : Results.Unauthorized());
        group.MapPatch("/parties/{partyId:guid}", async (Guid partyId, UpdatePartyRequest request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.edit", ct) is { } access
                ? await Execute(http, access, () => service.UpdateAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, request, Correlation(http), ActivityId(http), ct))
                : Results.Unauthorized());
        group.MapPost("/parties/{partyId:guid}/roles", async (Guid partyId, AddRoleRequest request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.role.manage", ct) is { } access
                ? await Execute(http, access, () => service.AddRoleAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, request, Correlation(http), ActivityId(http), ct))
                : Results.Unauthorized());
        group.MapDelete("/parties/{partyId:guid}/roles/{role}", async (Guid partyId, string role, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.role.manage", ct) is { } access && Enum.TryParse<PartyRoleCode>(role, true, out var roleCode)
                ? await Execute(http, access, () => service.DisableRoleAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, roleCode, Correlation(http), ct))
                : Results.BadRequest());
        group.MapPost("/parties/{partyId:guid}/identifiers", async (Guid partyId, IdentifierInput request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.identifier.manage", ct) is { } access
                ? await Execute(http, access, () => service.AddIdentifierAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, request, Correlation(http), ct))
                : Results.Unauthorized());
        group.MapPost("/parties/{partyId:guid}/contacts", async (Guid partyId, ContactPointInput request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.contact.manage", ct) is { } access
                ? await Execute(http, access, () => service.AddContactPointAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, request, Correlation(http), ct))
                : Results.Unauthorized());
        group.MapPost("/parties/{partyId:guid}/addresses", async (Guid partyId, AddressInput request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.address.manage", ct) is { } access
                ? await Execute(http, access, () => service.AddAddressAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, request, Correlation(http), ct))
                : Results.Unauthorized());
        group.MapPost("/parties/{partyId:guid}/archive", async (Guid partyId, VersionRequest request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.archive", ct) is { } access
                ? await Execute(http, access, () => service.ChangeStatusAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, false, request.Version, Correlation(http), ActivityId(http), ct))
                : Results.Unauthorized());
        group.MapPost("/parties/{partyId:guid}/restore", async (Guid partyId, VersionRequest request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.edit", ct) is { } access
                ? await Execute(http, access, () => service.ChangeStatusAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, true, request.Version, Correlation(http), ActivityId(http), ct))
                : Results.Unauthorized());
        group.MapGet("/parties/{partyId:guid}/duplicate-candidates", async (Guid partyId, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.read", ct) is { } access
                ? await Execute(http, access, () => service.DuplicatesAsync(access.Tenant, partyId, ct))
                : Results.Unauthorized());
        group.MapGet("/parties/{partyId:guid}/readiness", async (Guid partyId, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.read", ct) is { } access
                ? await Execute(http, access, () => service.ReadinessFrameworkAsync(access.Tenant, partyId, ct), nullAsNotFound: true)
                : Results.Unauthorized());
        group.MapPost("/parties/{partyId:guid}/contact-persons", async (Guid partyId, AddContactPersonRequest request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.contact.manage", ct) is { } access
                ? await Execute(http, access, () => service.AddContactPersonAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, request.Person, request.Link, Correlation(http), ct))
                : Results.Unauthorized());
        group.MapPost("/parties/{partyId:guid}/aliases", async (Guid partyId, AliasInput request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.edit", ct) is { } access
                ? await Execute(http, access, () => service.AddAliasAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, request, Correlation(http), ct))
                : Results.Unauthorized());
        group.MapPut("/parties/{partyId:guid}/visibility", async (Guid partyId, VisibilityInput request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.visibility.manage", ct) is { } access
                ? await Execute(http, access, () => service.SetVisibilityAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, request, Correlation(http), ct))
                : Results.Unauthorized());
        group.MapPost("/parties/{partyId:guid}/merge", async (Guid partyId, MergePartyRequest request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.party.merge", ct) is { } access
                ? await Execute(http, access, () => service.MergeAsync(access.Tenant, access.ActorId, access.MembershipId, partyId, request, Correlation(http), ct))
                : Results.Unauthorized());
        group.MapGet("/parties/{partyId:guid}/audit", async (Guid partyId, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.audit.view", ct) is { } access
                ? await Execute(http, access, () => service.AuditAsync(access.Tenant, partyId, ct))
                : Results.Unauthorized());
        group.MapGet("/parties/export", async (HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string? search, bool? includeArchived, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.export", ct) is { } access
                ? await ExecuteCsv(http, access, () => service.ExportCsvAsync(access.Tenant, search, includeArchived ?? false, access.LegalEntityId, ct))
                : Results.Unauthorized());
        group.MapPost("/parties/import", async (DirectoryImportRequest request, HttpContext http, DirectoryService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await ReadPermission(http, subjects, authorizer, "directory.import", ct) is { } access
                ? await Execute(http, access, () => service.ImportAsync(access.Tenant, access.ActorId, access.MembershipId, request, http.Request.Headers["Idempotency-Key"].ToString(), Correlation(http), ct))
                : Results.Unauthorized());
        return app;
    }

    private static async Task<Access?> ReadPermission(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId) || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId) || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        var tenant = new TenantId(tenantId); var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null || !authorizer.Authorize(subject, permission, PermissionScope.ForTenant(tenant))) return new Access(tenant, actorId, membershipId, false, false, false);
        var identifierView = authorizer.Authorize(subject, "directory.party.identifier.view", PermissionScope.ForTenant(tenant));
        var duplicateOverride = authorizer.Authorize(subject, "directory.duplicate.override", PermissionScope.ForTenant(tenant));
        var legalEntityId = Guid.TryParse(http.User.FindFirstValue("legal_entity_id"), out var entityId) ? entityId : (Guid?)null;
        return new Access(tenant, actorId, membershipId, true, identifierView, duplicateOverride, legalEntityId);
    }

    private static async Task<IResult> Execute<T>(HttpContext http, Access access, Func<Task<T>> action, int successStatus = StatusCodes.Status200OK, bool nullAsNotFound = false)
    {
        if (!access.Allowed) return Results.Forbid();
        try { var result = await action(); if (nullAsNotFound && result is null) return Results.NotFound(); return successStatus == StatusCodes.Status201Created ? Results.Created(http.Request.Path, result) : Results.Ok(result); }
        catch (DirectoryException ex) { return Results.Problem(statusCode: ex.Code.Contains("concurrency", StringComparison.Ordinal) || ex.Code.Contains("duplicate", StringComparison.Ordinal) ? 409 : 400, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> ExecuteCsv(HttpContext http, Access access, Func<Task<string>> action)
    {
        if (!access.Allowed) return Results.Forbid();
        try { return Results.Text(await action(), "text/csv; charset=utf-8"); }
        catch (DirectoryException ex) { return Results.Problem(statusCode: 400, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }); }
    }

    private static T? Parse<T>(string? value) where T : struct, Enum => Enum.TryParse<T>(value, true, out var parsed) ? parsed : null;
    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.Request.Headers["X-Correlation-Id"], out var value) ? value : Guid.CreateVersion7();
    private static string? ActivityId(HttpContext http) => System.Diagnostics.Activity.Current?.Id;
    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed, bool IdentifierView, bool DuplicateOverride, Guid? LegalEntityId = null);
    public sealed record VersionRequest(long Version);
}

public sealed class DirectoryModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("directory", "1.0.0", 1, "L1", "foundation", false, "tenant", new(["platform-kernel"], []), new("foundation_directory", ["party", "party_role", "party_identifier", "contact_person", "contact_point", "party_contact", "address", "party_address", "party_alias", "party_relationship", "party_merge_redirect", "party_visibility", "duplicate_candidate", "external_reference", "import_operation"]), ["directory.party.read", "directory.party.create", "directory.party.edit", "directory.party.archive", "directory.party.role.manage", "directory.party.identifier.view", "directory.party.identifier.manage", "directory.contact.manage", "directory.address.manage", "directory.party.merge", "directory.party.visibility.manage", "directory.duplicate.override", "directory.import", "directory.export", "directory.audit.view", "directory.party.quick_create"], new(["directory.party"], []), new(["DirectoryPartyCreatedV1", "DirectoryPartyIdentityChangedV1", "DirectoryPartyArchivedV1", "DirectoryPartyRestoredV1", "DirectoryPartyRoleChangedV1", "DirectoryPartyIdentifierChangedV1", "DirectoryPartyContactPointChangedV1", "DirectoryPartyContactChangedV1", "DirectoryPartyAddressChangedV1", "DirectoryPartyAliasChangedV1", "DirectoryPartyVisibilityChangedV1", "DirectoryPartyMergedV1", "DirectoryPartyImportCompletedV1"], []), new(true, "blocked_if_dependents"));
}
