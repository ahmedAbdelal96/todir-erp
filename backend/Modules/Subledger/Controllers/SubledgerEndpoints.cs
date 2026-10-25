using System.Security.Claims;
using Core.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Modules.Subledger.Entities;
using Modules.Subledger.Services;
using Shared.Contracts.Subledger;
using Shared.Primitives.Identifiers;

namespace Modules.Subledger;

public static class SubledgerEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/subledger").RequireAuthorization().RequireRateLimiting("api").WithTags("Subledger");
        group.MapGet("/parties/{partyId:guid}/exposure", async (Guid partyId, Guid legalEntityId, string direction, DateOnly? asOfDate,
            HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            if (!TryDirection(direction, out var parsedDirection)) return Results.BadRequest(new { code = "subledger.direction_invalid" });
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.GetPartyBalanceAsync(access.Tenant, new(new LegalEntityId(legalEntityId), partyId, parsedDirection, asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct));
        });

        group.MapGet("/reports/aging", async (Guid legalEntityId, Guid partyId, string direction, DateOnly asOfDate,
            HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            if (!TryDirection(direction, out var parsedDirection)) return Results.BadRequest(new { code = "subledger.direction_invalid" });
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.aging.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.GetAgingAsync(access.Tenant, access.MembershipId, new(new LegalEntityId(legalEntityId), partyId, parsedDirection, asOfDate), ct));
        });

        group.MapGet("/parties/{partyId:guid}/statement", async (Guid partyId, Guid legalEntityId, string direction, DateOnly fromDate, DateOnly toDate,
            HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            if (!TryDirection(direction, out var parsedDirection)) return Results.BadRequest(new { code = "subledger.direction_invalid" });
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.statement.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.GetStatementAsync(access.Tenant, access.MembershipId, new(new LegalEntityId(legalEntityId), partyId, parsedDirection, fromDate, toDate), ct));
        });

        group.MapPut("/write-off-policies", async (WriteOffPolicyCommand command, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.writeoff.policy.manage", command.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.UpdateWriteOffPolicyAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapPost("/write-offs/requests", async (CreateWriteOffCommand command, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.writeoff.create", command.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Results.Unauthorized();
            command = command with { ActorId = actorId, CorrelationId = Guid.CreateVersion7() };
            return await Execute(() => subledger.CreateWriteOffRequestAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapPost("/write-offs/requests/{requestId:guid}/approve", async (Guid requestId, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Results.Unauthorized();
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.writeoff.approve", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            var command = new ApproveWriteOffCommand(new LegalEntityId(legalEntityId), requestId, actorId, Guid.CreateVersion7());
            return await Execute(() => subledger.ApproveWriteOffAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapPost("/reports/control-reconciliation", async (ReconciliationQuery query, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.reconciliation.run", query.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Results.Unauthorized();
            return await Execute(() => subledger.ReconcileControlBalancesAsync(access.Tenant, access.MembershipId, query, actorId, Guid.CreateVersion7(), ct));
        });

        group.MapPost("/opening-batches", async (CreateOpeningSubledgerBatchCommand command, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.opening.manage", command.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Results.Unauthorized();
            command = command with { ActorId = actorId, CorrelationId = Guid.CreateVersion7() };
            return await Execute(() => subledger.CreateOpeningSubledgerBatchAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapPost("/opening-batches/{batchId:guid}/lines", async (Guid batchId, Guid legalEntityId, IReadOnlyList<OpeningSubledgerLineCommand> lines, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.opening.manage", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(async () => { await subledger.AddOpeningSubledgerLinesAsync(access.Tenant, access.MembershipId, new LegalEntityId(legalEntityId), batchId, lines, ct); return new { batchId, added = lines.Count }; });
        });

        group.MapPost("/opening-batches/{batchId:guid}/finalize", async (Guid batchId, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.opening.finalize", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Results.Unauthorized();
            return await Execute(() => subledger.FinalizeOpeningSubledgerBatchAsync(access.Tenant, access.MembershipId, new LegalEntityId(legalEntityId), batchId, actorId, Guid.CreateVersion7(), ct));
        });
    }

    private static async Task<Access> ResolveAsync(ClaimsPrincipal user, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(user.FindFirstValue("membership_id"), out var membershipId)
            || !Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out _))
            return new(default, Guid.Empty, Results.Unauthorized());
        var tenant = new TenantId(tenantId);
        var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return new(default, Guid.Empty, Results.Unauthorized());
        if (!authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new LegalEntityId(entityId))))
            return new(default, Guid.Empty, Results.Forbid());
        return new(tenant, membershipId, null);
    }

    private static bool TryDirection(string direction, out SubledgerDirectionContract result) => Enum.TryParse(direction, true, out result) && Enum.IsDefined(result);

    private static async Task<IResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(await action()); }
        catch (SubledgerException exception) { return Results.BadRequest(new { code = exception.Code, message = exception.Message }); }
    }

    private sealed record Access(TenantId Tenant, Guid MembershipId, IResult? Response);
}
