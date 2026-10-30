using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modules.Accounting.DTOs;
using Modules.Accounting.Repositories;
using Modules.Accounting.Services;
using Shared.Primitives.Identifiers;

namespace Modules.Accounting.Controllers;

public static class AccountingModuleExtensions
{
    public static IServiceCollection AddAccountingModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, AccountingModuleManifestProvider>();
        services.AddSingleton<AccountingRepository>();
        services.AddSingleton<AccountingService>();
        return services;
    }

    public static WebApplication MapAccountingModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/accounting").WithTags("Accounting").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/legal-entities/{entityId:guid}/health", async (Guid entityId, DateOnly? asOf, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.health.read", entityId, ct), access => service.GetHealthAsync(access.Tenant, access.MembershipId, entityId, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow), access.ActorId, Correlation(http), ct)));
        group.MapGet("/legal-entities/{entityId:guid}/readiness", async (Guid entityId, DateOnly? asOf, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.health.read", entityId, ct), access => service.GetHealthAsync(access.Tenant, access.MembershipId, entityId, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow), access.ActorId, Correlation(http), ct)));
        group.MapGet("/legal-entities/{entityId:guid}/owner-summary", async (Guid entityId, DateOnly? asOf, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.owner.read", entityId, ct), access => service.GetOwnerSummaryAsync(access.Tenant, access.MembershipId, entityId, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow), access.ActorId, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/reconciliations/{type}", async (Guid entityId, string type, DateOnly asOf, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.reconciliation.run", entityId, ct), access => service.ReconcileAsync(access.Tenant, access.MembershipId, entityId, type, asOf, access.ActorId, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/opening/approve-go-live", async (Guid entityId, ApproveGoLiveRequest request, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.opening.approve", entityId, ct), access => service.ApproveGoLiveAsync(access.Tenant, access.MembershipId, entityId, request, access.ActorId, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/fx-revaluations/preview", async (Guid entityId, FxRevaluationRequest request, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.fx-revaluation.preview", entityId, ct), access => service.PreviewFxRevaluationAsync(access.Tenant, entityId, request, access.ActorId, ct)));
        group.MapPost("/legal-entities/{entityId:guid}/fx-revaluations/{runId:guid}/post", async (Guid entityId, Guid runId, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.fx-revaluation.post", entityId, ct), access => service.PostFxRevaluationAsync(access.Tenant, entityId, runId, access.ActorId, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/fx-revaluations/{runId:guid}/reverse", async (Guid entityId, Guid runId, FxRevaluationReversalRequest request, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.fx-revaluation.reverse", entityId, ct), access => service.ReverseFxRevaluationAsync(access.Tenant, entityId, runId, request, access.ActorId, Correlation(http), ct)));
        group.MapGet("/legal-entities/{entityId:guid}/fx-revaluations/{runId:guid}", async (Guid entityId, Guid runId, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.fx-revaluation.read", entityId, ct), access => service.GetFxRevaluationAsync(access.Tenant, entityId, runId, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/trial-balance", async (Guid entityId, DateOnly? from, DateOnly? to, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.statement.read", entityId, ct), access => service.GetTrialBalanceAsync(access.Tenant, entityId, from, to, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/general-ledger", async (Guid entityId, DateOnly? from, DateOnly? to, Guid? accountId, string? cursor, int? limit, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.statement.read", entityId, ct), access => service.GetGeneralLedgerAsync(access.Tenant, entityId, from, to, accountId, cursor, limit ?? 100, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/statements/{kind}", async (Guid entityId, string kind, DateOnly from, DateOnly to, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.statement.read", entityId, ct), access => service.GetStatementAsync(access.Tenant, entityId, kind, from, to, ct)));
        group.MapGet("/legal-entities/{entityId:guid}/statement-definitions/{kind}", async (Guid entityId, string kind, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.statement.manage", entityId, ct), access => service.GetStatementDefinitionAsync(access.Tenant, entityId, kind, ct)));
        group.MapPut("/legal-entities/{entityId:guid}/statement-definitions", async (Guid entityId, FinancialStatementDefinitionRequest request, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.statement.manage", entityId, ct), access => service.SaveStatementDefinitionAsync(access.Tenant, entityId, request, access.ActorId, ct)));
        group.MapPost("/legal-entities/{entityId:guid}/close/preview", async (Guid entityId, ClosePreviewRequest request, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.close.preview", entityId, ct), access => service.PreviewCloseAsync(access.Tenant, access.MembershipId, entityId, request, access.ActorId, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/close", async (Guid entityId, ClosePreviewRequest request, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.close.execute", entityId, ct), access => service.CloseAsync(access.Tenant, access.MembershipId, entityId, request, access.ActorId, Correlation(http), ct)));
        group.MapPost("/legal-entities/{entityId:guid}/periods/{periodId:guid}/reopen", async (Guid entityId, Guid periodId, ReopenRequest request, HttpContext http, AccountingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await AccessAsync(http, subjects, authorizer, "accounting.close.reopen", entityId, ct), access => service.ReopenAsync(access.Tenant, entityId, periodId, request.Reason, request.IdempotencyKey, access.ActorId, Correlation(http), ct)));
        return app;
    }

    private static async Task<IResult> Execute<T>(HttpContext http, Access? resolved, Func<Access, Task<T>> action)
    {
        if (resolved is null) return Results.Unauthorized();
        try { return Results.Ok(await action(resolved)); }
        catch (AccountingException exception) { return Results.Problem(statusCode: exception.Code is "accounting.close_blocked" ? 409 : 400, title: exception.Code, extensions: new Dictionary<string, object?> { ["code"] = exception.Code }); }
        catch (AccountingRepositoryException exception) { return Results.Problem(statusCode: exception.Code == "accounting.idempotency_conflict" ? 409 : 503, title: exception.Code, extensions: new Dictionary<string, object?> { ["code"] = exception.Code }); }
    }
    private static async Task<Access?> AccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entity, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId) || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membership) || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return null;
        var tenant = new TenantId(tenantId); var subject = await subjects.LoadAsync(tenant, membership, ct);
        return subject is not null && authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new LegalEntityId(entity))) ? new(tenant, membership, actor) : null;
    }
    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.TraceIdentifier, out var correlation) ? correlation : Guid.CreateVersion7();
    public sealed record ReopenRequest(string Reason, string IdempotencyKey);
    private sealed record Access(TenantId Tenant, Guid MembershipId, Guid ActorId);
}

public sealed class AccountingModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("accounting", "1.0.0", 1, "L3", "module", true, "tenant", new(["platform-kernel", "finbase", "ledger", "subledger"], ["inventory.valuation"]), new("business_accounting", ["accounting_setup", "reconciliation_run", "reconciliation_issue", "close_run", "close_check", "financial_statement_definition", "financial_statement_version", "financial_statement_line", "report_snapshot_ref", "fx_revaluation_run", "fx_revaluation_line"]), ["accounting.home.read", "accounting.owner.read", "accounting.health.read", "accounting.reconciliation.read", "accounting.reconciliation.run", "accounting.opening.approve", "accounting.close.preview", "accounting.close.execute", "accounting.close.reopen", "accounting.statement.read", "accounting.statement.manage", "accounting.fx-revaluation.read", "accounting.fx-revaluation.preview", "accounting.fx-revaluation.post", "accounting.fx-revaluation.reverse"], new(["accounting.health", "accounting.reconciliation-evidence", "accounting.financial-statements", "accounting.period-close", "accounting.go-live", "accounting.fx-revaluation"], ["ledger.reports", "ledger.control-balance-read", "ledger.opening-batch-read", "ledger.opening-batch-lock", "subledger.control-reconciliation", "subledger.opening-batch-read", "subledger.open-item-fx-read", "inventory.valuation"]), new(["AccountingCloseCompletedV1", "AccountingReconciliationCompletedV1", "AccountingGoLiveApprovedV1"], ["JournalEntryPostedV1", "PeriodClosedV1", "PeriodReopenedV1"]), new(true, "blocked_if_dependents"));
}
