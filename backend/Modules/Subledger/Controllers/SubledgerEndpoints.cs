using System.Security.Claims;
using Core.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Modules.Subledger.Entities;
using Modules.Subledger.Services;
using Shared.Contracts.Subledger;
using Shared.BuildingBlocks.Observability;
using Shared.Primitives.Identifiers;

namespace Modules.Subledger;

public static class SubledgerEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/subledger").RequireAuthorization().RequireRateLimiting("api").WithTags("Subledger");
        group.MapGet("/open-items", async (Guid? partyId, Guid legalEntityId, string? direction, bool? openOnly, int? limit, string? cursor, string? search,
            DateOnly? dueFrom, DateOnly? dueTo, string? currencyCode, bool? overdueOnly, DateOnly? asOfDate, string? status,
            HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            SubledgerDirectionContract? parsedDirection = null;
            if (direction is not null)
            {
                if (!TryDirection(direction, out var parsed)) return ApiProblem(400, "subledger.direction_invalid", "The financial direction is invalid.");
                parsedDirection = parsed;
            }
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.ListOpenItemsAsync(access.Tenant,
                new(new LegalEntityId(legalEntityId), partyId, parsedDirection, openOnly ?? true, limit ?? 50, cursor, search, dueFrom, dueTo, currencyCode, overdueOnly ?? false, asOfDate, status), ct));
        });

        group.MapGet("/parties/{partyId:guid}/open-items", async (Guid partyId, Guid legalEntityId, string? direction, bool? openOnly, int? limit, string? cursor, string? search,
            DateOnly? dueFrom, DateOnly? dueTo, string? currencyCode, bool? overdueOnly, DateOnly? asOfDate, string? status,
            HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            SubledgerDirectionContract? parsedDirection = null;
            if (direction is not null)
            {
                if (!TryDirection(direction, out var parsed)) return ApiProblem(400, "subledger.direction_invalid", "The financial direction is invalid.");
                parsedDirection = parsed;
            }
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.ListOpenItemsAsync(access.Tenant,
                new(new LegalEntityId(legalEntityId), partyId, parsedDirection, openOnly ?? true, limit ?? 50, cursor, search, dueFrom, dueTo, currencyCode, overdueOnly ?? false, asOfDate, status), ct));
        });

        group.MapGet("/open-items/{openItemId:guid}", async (Guid openItemId, Guid legalEntityId, HttpContext http,
            IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.GetOpenItemAsync(access.Tenant, new LegalEntityId(legalEntityId), openItemId, ct));
        });

        group.MapPost("/applications/suggest", async (AutoApplySuggestionQuery query, HttpContext http, IAuthorizationSubjectStore subjects,
            IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.application.create", query.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.SuggestApplicationsAsync(access.Tenant, access.MembershipId, query, ct));
        });

        group.MapPost("/applications/preview", async (ApplyOpenItemsCommand command, HttpContext http, IAuthorizationSubjectStore subjects,
            IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.application.create", command.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            command = command with { ActorId = actorId, CorrelationId = CorrelationId(http) };
            return await Execute(() => subledger.PreviewAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapPost("/applications", async (ApplyOpenItemsCommand command, HttpContext http, IAuthorizationSubjectStore subjects,
            IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.application.create", command.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            if (command.Lines is null || command.Lines.Any(line => line.ExpectedDebitVersion is null || line.ExpectedCreditVersion is null))
                return ApiProblem(400, "subledger.item_version_required", "A current preview version is required for every application line.");
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            command = command with { ActorId = actorId, CorrelationId = CorrelationId(http) };
            return await Execute(() => subledger.ApplyAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapPost("/applications/{applicationId:guid}/reverse", async (Guid applicationId, ReverseApplicationCommand command, HttpContext http,
            IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.application.reverse", command.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            command = command with { ApplicationId = applicationId, ActorId = actorId, CorrelationId = CorrelationId(http) };
            return await Execute(() => subledger.ReverseAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapGet("/parties/{partyId:guid}/financial-profile", async (Guid partyId, Guid legalEntityId, HttpContext http,
            IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.profile.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.GetFinancialProfileAsync(access.Tenant, new LegalEntityId(legalEntityId), partyId, ct));
        });

        group.MapPut("/parties/{partyId:guid}/financial-profile", async (Guid partyId, UpdateFinancialProfileRequest request, HttpContext http,
            IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.profile.manage", request.LegalEntityId, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            var command = new PartyFinancialProfileCommand(new LegalEntityId(request.LegalEntityId), partyId, request.DefaultReceivablePaymentTermId,
                request.DefaultPayablePaymentTermId, request.CreditLimitAmount, request.CreditLimitCurrency, request.ReceivablePolicyCode,
                request.PayablePolicyCode, request.ExpectedVersion, actorId, CorrelationId(http));
            return await Execute(() => subledger.UpdateFinancialProfileAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapGet("/parties/{partyId:guid}/exposure", async (Guid partyId, Guid legalEntityId, string direction, DateOnly? asOfDate,
            HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            if (!TryDirection(direction, out var parsedDirection)) return ApiProblem(400, "subledger.direction_invalid", "The financial direction is invalid.");
            var access = parsedDirection == SubledgerDirectionContract.Receivable
                ? await ResolveAnyAsync(http.User, subjects, authorizer, ["subledger.read", "subledger.credit_limit.read"], legalEntityId, ct)
                : await ResolveAsync(http.User, subjects, authorizer, "subledger.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.GetPartyBalanceForMemberAsync(access.Tenant, access.MembershipId,
                new(new LegalEntityId(legalEntityId), partyId, parsedDirection, asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct));
        });

        group.MapGet("/reports/aging", async (Guid legalEntityId, Guid partyId, string direction, DateOnly asOfDate, string? currencyCode,
            HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            if (!TryDirection(direction, out var parsedDirection)) return ApiProblem(400, "subledger.direction_invalid", "The financial direction is invalid.");
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.aging.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.GetAgingAsync(access.Tenant, access.MembershipId, new(new LegalEntityId(legalEntityId), partyId, parsedDirection, asOfDate, currencyCode), ct));
        });

        group.MapGet("/parties/{partyId:guid}/statement", async (Guid partyId, Guid legalEntityId, string direction, DateOnly fromDate, DateOnly toDate, string? currencyCode, int? limit, int? page,
            HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            if (!TryDirection(direction, out var parsedDirection)) return ApiProblem(400, "subledger.direction_invalid", "The financial direction is invalid.");
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.statement.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.GetStatementAsync(access.Tenant, access.MembershipId,
                new(new LegalEntityId(legalEntityId), partyId, parsedDirection, fromDate, toDate, currencyCode, limit ?? 500, page ?? 1), ct));
        });

        group.MapPut("/write-off-policies", async (UpdateWriteOffPolicyRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.writeoff.policy.manage", request.LegalEntityId, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            var command = new WriteOffPolicyCommand(new LegalEntityId(request.LegalEntityId), request.Direction, request.CurrencyCode,
                request.MaximumAmount, request.ApprovalThreshold, request.ExpectedVersion, actorId, CorrelationId(http));
            return await Execute(() => subledger.UpdateWriteOffPolicyAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapPost("/write-offs/requests", async (CreateWriteOffCommand command, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.writeoff.create", command.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            command = command with { ActorId = actorId, CorrelationId = CorrelationId(http) };
            return await Execute(() => subledger.CreateWriteOffRequestAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapPost("/write-offs/requests/{requestId:guid}/approve", async (Guid requestId, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.writeoff.approve", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            var command = new ApproveWriteOffCommand(new LegalEntityId(legalEntityId), requestId, actorId, CorrelationId(http));
            return await Execute(() => subledger.ApproveWriteOffAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapPost("/reports/control-reconciliation", async (ReconciliationQuery query, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.reconciliation.run", query.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            return await Execute(() => subledger.ReconcileControlBalancesAsync(access.Tenant, access.MembershipId, query, actorId, CorrelationId(http), ct));
        });

        group.MapGet("/reconciliation/control-accounts", async (Guid legalEntityId, DateOnly asOfDate, HttpContext http,
            IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.reconciliation.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.GetReconciliationControlAccountsAsync(access.Tenant, access.MembershipId, new(new LegalEntityId(legalEntityId), asOfDate), ct));
        });

        group.MapPost("/reconciliation/run", async (ReconciliationQuery query, HttpContext http, IAuthorizationSubjectStore subjects,
            IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.reconciliation.run", query.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            return await Execute(() => subledger.ReconcileControlBalancesAsync(access.Tenant, access.MembershipId, query, actorId, CorrelationId(http), ct));
        });

        group.MapGet("/reconciliation/runs/{runId:guid}", async (Guid runId, Guid legalEntityId, HttpContext http,
            IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.reconciliation.read", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.GetReconciliationRunAsync(access.Tenant, access.MembershipId, new LegalEntityId(legalEntityId), runId, ct));
        });

        group.MapPost("/opening-batches", async (CreateOpeningSubledgerBatchCommand command, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.opening.manage", command.LegalEntityId.Value, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            command = command with { ActorId = actorId, CorrelationId = CorrelationId(http) };
            return await Execute(() => subledger.CreateOpeningSubledgerBatchAsync(access.Tenant, access.MembershipId, command, ct));
        });

        group.MapPost("/opening-batches/{batchId:guid}/lines", async (Guid batchId, Guid legalEntityId, IReadOnlyList<OpeningSubledgerLineCommand> lines, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.opening.manage", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            var correlationId = CorrelationId(http);
            return await Execute(async () => { await subledger.AddOpeningSubledgerLinesAsync(access.Tenant, access.MembershipId, new LegalEntityId(legalEntityId), batchId, lines, actorId, correlationId, ct); return new { batchId, added = lines.Count }; });
        });

        group.MapPost("/opening-batches/{batchId:guid}/validate", async (Guid batchId, Guid legalEntityId, HttpContext http,
            IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.opening.manage", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            return await Execute(() => subledger.ValidateOpeningSubledgerBatchAsync(access.Tenant, access.MembershipId, new LegalEntityId(legalEntityId), batchId, ct));
        });

        group.MapPost("/opening-batches/{batchId:guid}/finalize", async (Guid batchId, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, SubledgerService subledger, CancellationToken ct) =>
        {
            var access = await ResolveAsync(http.User, subjects, authorizer, "subledger.opening.finalize", legalEntityId, ct);
            if (access.Response is not null) return access.Response;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return ApiProblem(401, "authentication_required", "An authenticated user is required.");
            return await Execute(() => subledger.FinalizeOpeningSubledgerBatchAsync(access.Tenant, access.MembershipId, new LegalEntityId(legalEntityId), batchId, actorId, CorrelationId(http), ct));
        });
    }

    private static async Task<Access> ResolveAsync(ClaimsPrincipal user, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(user.FindFirstValue("membership_id"), out var membershipId)
            || !Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out _))
            return new(default, Guid.Empty, ApiProblem(401, "authentication_required", "An authenticated user is required."));
        var tenant = new TenantId(tenantId);
        var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return new(default, Guid.Empty, ApiProblem(401, "authorization_subject_unavailable", "The authenticated membership is unavailable."));
        if (!authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new LegalEntityId(entityId))))
            return new(default, Guid.Empty, ApiProblem(403, "permission_denied", "You do not have permission for this financial operation."));
        return new(tenant, membershipId, null);
    }

    private static async Task<Access> ResolveAnyAsync(ClaimsPrincipal user, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer,
        IReadOnlyList<string> permissions, Guid entityId, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(user.FindFirstValue("membership_id"), out var membershipId)
            || !Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out _))
            return new(default, Guid.Empty, ApiProblem(401, "authentication_required", "An authenticated user is required."));
        var tenant = new TenantId(tenantId);
        var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return new(default, Guid.Empty, ApiProblem(401, "authorization_subject_unavailable", "The authenticated membership is unavailable."));
        if (!permissions.Any(permission => authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new LegalEntityId(entityId)))))
            return new(default, Guid.Empty, ApiProblem(403, "permission_denied", "You do not have permission for this financial operation."));
        return new(tenant, membershipId, null);
    }

    private static bool TryDirection(string direction, out SubledgerDirectionContract result) => Enum.TryParse(direction, true, out result) && Enum.IsDefined(result);

    private static Guid CorrelationId(HttpContext http) => RequestCorrelationId.GetOrCreate(http);

    private static async Task<IResult> Execute<T>(Func<Task<T>> action)
    {
        try
        {
            var result = await action();
            return result is null ? ApiProblem(404, "subledger.resource_not_found", "The requested subledger record was not found.") : Results.Ok(result);
        }
        catch (SubledgerException exception) when (exception.Code is "subledger.item_changed" or "subledger.application_conflict" or "subledger.profile_changed")
        { return ApiProblem(409, exception.Code, exception.Message); }
        catch (SubledgerException exception) { return ApiProblem(400, exception.Code, exception.Message); }
    }

    private static IResult ApiProblem(int statusCode, string code, string detail) =>
        Results.Problem(statusCode: statusCode, title: code, detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private sealed record UpdateFinancialProfileRequest(Guid LegalEntityId, Guid? DefaultReceivablePaymentTermId, Guid? DefaultPayablePaymentTermId,
        string? CreditLimitAmount, string? CreditLimitCurrency, string? ReceivablePolicyCode, string? PayablePolicyCode, long ExpectedVersion);

    private sealed record UpdateWriteOffPolicyRequest(Guid LegalEntityId, SubledgerDirectionContract Direction, string CurrencyCode,
        string MaximumAmount, string ApprovalThreshold, long ExpectedVersion);

    private sealed record Access(TenantId Tenant, Guid MembershipId, IResult? Response);
}
