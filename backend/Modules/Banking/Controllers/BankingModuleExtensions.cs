using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Modules.Banking.DTOs;
using Modules.Banking.Entities;
using Modules.Banking.Repositories;
using Modules.Banking.Services;
using Shared.Contracts.Banking;
using Shared.Primitives.Identifiers;

namespace Modules.Banking;

public static class BankingModuleExtensions
{
    public static IServiceCollection AddBankingModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, BankingModuleManifestProvider>();
        services.AddSingleton<BankingRepository>();
        services.AddSingleton<BankingService>();
        services.AddSingleton<IBankingTreasuryAccountCapability>(provider => provider.GetRequiredService<BankingService>());
        return services;
    }

    public static WebApplication MapBankingModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/banking").WithTags("Banking & Cash Management").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/treasury-accounts", async (Guid legalEntityId, string? search, TreasuryAccountType? type, TreasuryAccountStatus? status, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.accounts.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListAccountsAsync(access.Tenant, legalEntityId, search, type, status, page ?? 1, pageSize ?? 25, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/treasury-accounts/{accountId:guid}", async (Guid legalEntityId, Guid accountId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.accounts.read", legalEntityId, ct) is { } access ? access.Allowed ? (await service.GetAccountAsync(access.Tenant, legalEntityId, accountId, ct) is { } result ? Results.Ok(result) : Results.NotFound()) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/treasury-accounts", async (CreateTreasuryAccountRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.accounts.create", request.LegalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { var result = await service.CreateAccountAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, ct); return Results.Created($"/api/v1/banking/treasury-accounts/{result.Id:D}", result); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPut("/treasury-accounts/{accountId:guid}", async (Guid legalEntityId, Guid accountId, UpdateTreasuryAccountRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.accounts.edit", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.UpdateAccountAsync(access.Tenant, legalEntityId, accountId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPut("/treasury-accounts/{accountId:guid}/status", async (Guid legalEntityId, Guid accountId, StatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var target = request.TargetState ?? request.Reason;
            var permission = target?.Equals("Suspended", StringComparison.OrdinalIgnoreCase) == true || target?.Equals("suspend", StringComparison.OrdinalIgnoreCase) == true ? "banking.accounts.suspend" : target?.Equals("Closed", StringComparison.OrdinalIgnoreCase) == true || target?.Equals("close", StringComparison.OrdinalIgnoreCase) == true ? "banking.accounts.close" : "banking.accounts.activate";
            var access = await AccessAsync(http, subjects, authorizer, permission, legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.ChangeStatusAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), legalEntityId, accountId, target?.Equals("Closed", StringComparison.OrdinalIgnoreCase) == true || target?.Equals("close", StringComparison.OrdinalIgnoreCase) == true ? "Closed" : target?.Equals("Suspended", StringComparison.OrdinalIgnoreCase) == true || target?.Equals("suspend", StringComparison.OrdinalIgnoreCase) == true ? "Suspended" : "Active", request.Version, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/treasury-accounts/{accountId:guid}/statements", async (Guid legalEntityId, Guid accountId, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.statements.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListStatementsAsync(access.Tenant, legalEntityId, accountId, page ?? 1, pageSize ?? 25, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/treasury-accounts/{accountId:guid}/statements/import", async (Guid legalEntityId, Guid accountId, ImportStatementRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.statements.import", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault(); if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "banking.import_idempotency_required" }); return Results.Ok(await service.ImportStatementAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), legalEntityId, accountId, key, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/statements/{statementId:guid}", async (Guid legalEntityId, Guid statementId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.statements.read", legalEntityId, ct) is { } access ? access.Allowed ? (await service.GetStatementAsync(access.Tenant, legalEntityId, statementId, ct) is { } result ? Results.Ok(result) : Results.NotFound()) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/statements/{statementId:guid}/lines", async (Guid legalEntityId, Guid statementId, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.statements.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListStatementLinesAsync(access.Tenant, legalEntityId, statementId, page ?? 1, pageSize ?? 25, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/reconciliations", async (CreateReconciliationRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.reconciliation.read", request.LegalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/reconciliations", await service.CreateReconciliationAsync(access.Tenant, access.ActorId, request.LegalEntityId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/treasury-accounts/{accountId:guid}/reconciliations", async (Guid legalEntityId, Guid accountId, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reconciliation.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListReconciliationsAsync(access.Tenant, legalEntityId, accountId, page ?? 1, pageSize ?? 25, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/reconciliations/{reconciliationId:guid}/matches", async (Guid legalEntityId, Guid reconciliationId, CreateMatchRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.reconciliation.match", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.AddMatchAsync(access.Tenant, access.ActorId, legalEntityId, reconciliationId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPut("/reconciliations/{reconciliationId:guid}/state", async (Guid legalEntityId, Guid reconciliationId, ReconciliationStatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var permission = request.TargetState.Equals("Reopened", StringComparison.OrdinalIgnoreCase) ? "banking.reconciliation.reopen" : "banking.reconciliation.finalize"; var access = await AccessAsync(http, subjects, authorizer, permission, legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.ChangeReconciliationStateAsync(access.Tenant, access.ActorId, legalEntityId, reconciliationId, request.TargetState, request.Version, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/treasury-accounts/{accountId:guid}/cash-counts", async (Guid legalEntityId, Guid accountId, CreateCashCountRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.cash.count", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/cash-counts", await service.CreateCashCountAsync(access.Tenant, access.ActorId, legalEntityId, accountId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/cash-counts/{cashCountId:guid}", async (Guid legalEntityId, Guid cashCountId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.cash.read", legalEntityId, ct) is { } access ? access.Allowed ? (await service.GetCashCountAsync(access.Tenant, legalEntityId, cashCountId, ct) is { } result ? Results.Ok(result) : Results.NotFound()) : Results.Forbid() : Results.Unauthorized());
        group.MapPut("/cash-counts/{cashCountId:guid}/state", async (Guid legalEntityId, Guid cashCountId, CashCountStatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var permission = request.TargetState.Equals("Approved", StringComparison.OrdinalIgnoreCase) || request.TargetState.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ? "banking.cash.count.approve" : "banking.cash.difference.resolve"; var access = await AccessAsync(http, subjects, authorizer, permission, legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.ChangeCashCountStateAsync(access.Tenant, access.ActorId, legalEntityId, cashCountId, request.TargetState, request.Version, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/cash-position", async (Guid legalEntityId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.cash_position.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.GetCashPositionAsync(access.Tenant, legalEntityId, ct)) : Results.Forbid() : Results.Unauthorized());
        return app;
    }

    private static async Task<Access?> AccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId) || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId) || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        var tenant = new TenantId(tenantId); var subject = await subjects.LoadAsync(tenant, membershipId, ct); if (subject is null) return null;
        var allowed = authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new(entityId)));
        return new(tenant, actorId, membershipId, allowed);
    }
    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.Request.Headers["X-Correlation-Id"].FirstOrDefault(), out var id) ? id : Guid.CreateVersion7();
    private static IResult Problem(BankingException ex) => Results.Problem(statusCode: ex.Code.Contains("concurrency", StringComparison.Ordinal) ? 409 : 400, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed);
}

public sealed class BankingModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("banking", "1.0.0", 1, "L3", "module", false, "tenant",
        new(["platform-kernel", "directory", "finbase", "ledger", "subledger", "payments", "pos", "payroll", "tax"], ["platform.files"]),
        new("finance_banking", ["treasury_account", "treasury_account_balance_pocket", "bank_account_profile", "cash_account_profile", "digital_wallet_profile", "settlement_account_profile", "treasury_account_change_request", "treasury_account_connection", "statement_import_batch", "external_statement", "external_statement_line", "statement_transaction_code_mapping", "reconciliation_rule_set", "reconciliation_rule_version", "reconciliation_session", "reconciliation_match", "reconciliation_match_item", "reconciliation_adjustment", "cash_count", "cash_count_denomination", "cash_difference_resolution", "cash_custodian_history", "cash_position_projection", "cash_forecast_version", "cash_forecast_item"]),
        ["banking.accounts.read", "banking.accounts.create", "banking.accounts.edit", "banking.accounts.activate", "banking.accounts.suspend", "banking.accounts.close", "banking.accounts.sensitive_data.read", "banking.accounts.change.approve", "banking.connections.read", "banking.connections.manage", "banking.statements.read", "banking.statements.import", "banking.statements.validate", "banking.reconciliation.read", "banking.reconciliation.match", "banking.reconciliation.finalize", "banking.reconciliation.reopen", "banking.bank_originated.review", "banking.bank_originated.approve", "banking.cash.read", "banking.cash.count", "banking.cash.count.approve", "banking.cash.difference.resolve", "banking.transfers.read", "banking.transfers.initiate", "banking.cash_position.read", "banking.cash_forecast.read", "banking.cash_forecast.manage", "banking.reports.read", "banking.audit.read", "banking.export", "banking.settings.manage"],
        new(["banking.treasury-accounts", "banking.statement-evidence", "banking.reconciliation", "banking.cash-position", "banking.cash-forecast"], ["payments.money-movement", "directory.party", "finbase.money", "ledger.semantic-facts", "subledger.open-items", "platform.audit", "platform.files"]),
        new(["TreasuryAccountActivatedV1", "TreasuryAccountSuspendedV1", "TreasuryAccountClosedV1", "ExternalStatementImportedV1", "ReconciliationFinalizedV1", "CashCountFinalizedV1"], ["PaymentCompletedV1", "PaymentReversedV1", "InternalTransferCompletedV1"]), new(false, "blocked_if_dependents"));
}
