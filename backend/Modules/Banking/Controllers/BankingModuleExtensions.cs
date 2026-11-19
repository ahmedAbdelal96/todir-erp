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
using Core.Transactions;
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
        services.AddSingleton<ITransactionalBankingTreasuryAccountCapability>(provider => provider.GetRequiredService<BankingService>());
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
        group.MapPost("/treasury-accounts/{accountId:guid}/activate", async (Guid legalEntityId, Guid accountId, StatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await ChangeAccountStatus(http, service, subjects, authorizer, legalEntityId, accountId, "Active", request.Version, "banking.accounts.activate", Correlation(http), ct));
        group.MapPost("/treasury-accounts/{accountId:guid}/suspend", async (Guid legalEntityId, Guid accountId, StatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await ChangeAccountStatus(http, service, subjects, authorizer, legalEntityId, accountId, "Suspended", request.Version, "banking.accounts.suspend", Correlation(http), ct));
        group.MapPost("/treasury-accounts/{accountId:guid}/close-request", async (Guid legalEntityId, Guid accountId, StatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await ChangeAccountStatus(http, service, subjects, authorizer, legalEntityId, accountId, "Closed", request.Version, "banking.accounts.close", Correlation(http), ct));
        group.MapPost("/treasury-accounts/{accountId:guid}/reopen", async (Guid legalEntityId, Guid accountId, StatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await ChangeAccountStatus(http, service, subjects, authorizer, legalEntityId, accountId, "Active", request.Version, "banking.accounts.activate", Correlation(http), ct));
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
        group.MapGet("/statement-lines/{lineId:guid}", async (Guid legalEntityId, Guid lineId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.statements.read", legalEntityId, ct) is { } access ? access.Allowed ? (await service.GetStatementLineAsync(access.Tenant, legalEntityId, lineId, ct) is { } result ? Results.Ok(result) : Results.NotFound()) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/statements/{statementId:guid}/validate", async (Guid legalEntityId, Guid statementId, ValidateStatementRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.statements.validate", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.ValidateStatementAsync(access.Tenant, access.ActorId, legalEntityId, statementId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/statements/{statementId:guid}/process", async (Guid legalEntityId, Guid statementId, ValidateStatementRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.statements.validate", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.ValidateStatementAsync(access.Tenant, access.ActorId, legalEntityId, statementId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/statement-lines/{lineId:guid}/classify", async (Guid legalEntityId, Guid lineId, ClassifyStatementLineRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.statements.validate", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.ClassifyStatementLineAsync(access.Tenant, access.ActorId, legalEntityId, lineId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/statement-lines/{lineId:guid}/create-payment-proposal", async (Guid legalEntityId, Guid lineId, CreatePaymentProposalRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.bank_originated.review", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/bank-originated-candidates", await service.CreatePaymentProposalAsync(access.Tenant, access.ActorId, legalEntityId, lineId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/transaction-code-mappings", async (Guid legalEntityId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.settings.manage", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListTransactionCodeMappingsAsync(access.Tenant, legalEntityId, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/transaction-code-mappings", async (Guid legalEntityId, CreateTransactionCodeMappingRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.settings.manage", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/transaction-code-mappings", await service.CreateTransactionCodeMappingAsync(access.Tenant, access.ActorId, legalEntityId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/reconciliation-rule-sets", async (Guid legalEntityId, CreateReconciliationRuleSetRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.settings.manage", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/reconciliation-rule-sets", await service.CreateReconciliationRuleSetAsync(access.Tenant, access.ActorId, legalEntityId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/bank-originated-candidates/{candidateId:guid}/approve", async (Guid legalEntityId, Guid candidateId, BankOriginatedDecisionRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.bank_originated.approve", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.DecideBankOriginatedCandidateAsync(access.Tenant, access.ActorId, access.MembershipId, legalEntityId, candidateId, request with { Decision = "Approved" }, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/treasury-accounts/{accountId:guid}/change-requests", async (Guid legalEntityId, Guid accountId, CreateAccountChangeRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.accounts.edit", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/treasury-account-change-requests", await service.CreateAccountChangeRequestAsync(access.Tenant, access.ActorId, legalEntityId, accountId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/treasury-account-change-requests/{requestId:guid}", async (Guid legalEntityId, Guid requestId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.accounts.edit", legalEntityId, ct) is { } access ? access.Allowed ? (await service.GetAccountChangeRequestAsync(access.Tenant, legalEntityId, requestId, ct) is { } result ? Results.Ok(result) : Results.NotFound()) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/treasury-account-change-requests/{requestId:guid}/approve", async (Guid legalEntityId, Guid requestId, ChangeRequestDecisionRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.accounts.change.approve", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.DecideAccountChangeRequestAsync(access.Tenant, access.ActorId, legalEntityId, requestId, request with { Decision = "Approved" }, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/treasury-account-change-requests/{requestId:guid}/reject", async (Guid legalEntityId, Guid requestId, ChangeRequestDecisionRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.accounts.change.approve", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.DecideAccountChangeRequestAsync(access.Tenant, access.ActorId, legalEntityId, requestId, request with { Decision = "Rejected" }, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/reconciliations", async (CreateReconciliationRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.reconciliation.read", request.LegalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/reconciliations", await service.CreateReconciliationAsync(access.Tenant, access.ActorId, request.LegalEntityId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/reconciliations", async (Guid legalEntityId, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reconciliation.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListReconciliationsAsync(access.Tenant, legalEntityId, page ?? 1, pageSize ?? 25, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/treasury-accounts/{accountId:guid}/reconciliations", async (Guid legalEntityId, Guid accountId, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reconciliation.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListReconciliationsAsync(access.Tenant, legalEntityId, accountId, page ?? 1, pageSize ?? 25, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/reconciliations/{reconciliationId:guid}/matches", async (Guid legalEntityId, Guid reconciliationId, CreateMatchRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.reconciliation.match", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.AddMatchAsync(access.Tenant, access.ActorId, legalEntityId, reconciliationId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/reconciliations/{reconciliationId:guid}/match", async (Guid legalEntityId, Guid reconciliationId, CreateMatchRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.reconciliation.match", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.AddMatchAsync(access.Tenant, access.ActorId, legalEntityId, reconciliationId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPut("/reconciliations/{reconciliationId:guid}/state", async (Guid legalEntityId, Guid reconciliationId, ReconciliationStatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var permission = request.TargetState.Equals("Reopened", StringComparison.OrdinalIgnoreCase) ? "banking.reconciliation.reopen" : "banking.reconciliation.finalize"; var access = await AccessAsync(http, subjects, authorizer, permission, legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.ChangeReconciliationStateAsync(access.Tenant, access.ActorId, legalEntityId, reconciliationId, request.TargetState, request.Version, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/reconciliations/{reconciliationId:guid}", async (Guid legalEntityId, Guid reconciliationId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reconciliation.read", legalEntityId, ct) is { } access ? access.Allowed ? (await service.GetReconciliationAsync(access.Tenant, legalEntityId, reconciliationId, ct) is { } result ? Results.Ok(result) : Results.NotFound()) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/reconciliations/{reconciliationId:guid}/suggestions", async (Guid legalEntityId, Guid reconciliationId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reconciliation.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.GetReconciliationSuggestionsAsync(access.Tenant, legalEntityId, reconciliationId, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/reconciliations/{reconciliationId:guid}/claims", async (Guid legalEntityId, Guid reconciliationId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reconciliation.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.GetReconciliationClaimsAsync(access.Tenant, legalEntityId, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/reconciliations/{reconciliationId:guid}/run-rules", async (Guid legalEntityId, Guid reconciliationId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reconciliation.match", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.GetReconciliationSuggestionsAsync(access.Tenant, legalEntityId, reconciliationId, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/reconciliations/{reconciliationId:guid}/unmatch", async (Guid legalEntityId, Guid reconciliationId, UnmatchRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.reconciliation.match", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.UnmatchAsync(access.Tenant, access.ActorId, legalEntityId, reconciliationId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/reconciliations/{reconciliationId:guid}/finalize", async (Guid legalEntityId, Guid reconciliationId, ReconciliationStatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await ChangeReconciliation(http, service, subjects, authorizer, legalEntityId, reconciliationId, "Finalized", request.Version, "banking.reconciliation.finalize", ct));
        group.MapPost("/reconciliations/{reconciliationId:guid}/reopen", async (Guid legalEntityId, Guid reconciliationId, ReconciliationStatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await ChangeReconciliation(http, service, subjects, authorizer, legalEntityId, reconciliationId, "Reopened", request.Version, "banking.reconciliation.reopen", ct));
        group.MapPost("/treasury-accounts/{accountId:guid}/cash-counts", async (Guid legalEntityId, Guid accountId, CreateCashCountRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.cash.count", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/cash-counts", await service.CreateCashCountAsync(access.Tenant, access.ActorId, legalEntityId, accountId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/cash-accounts/{accountId:guid}/counts", async (Guid legalEntityId, Guid accountId, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.cash.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListCashCountsAsync(access.Tenant, legalEntityId, accountId, page ?? 1, pageSize ?? 25, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/cash-accounts/{accountId:guid}/counts", async (Guid legalEntityId, Guid accountId, CreateCashCountRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.cash.count", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/cash-accounts", await service.CreateCashCountAsync(access.Tenant, access.ActorId, legalEntityId, accountId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/cash-counts/{cashCountId:guid}", async (Guid legalEntityId, Guid cashCountId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.cash.read", legalEntityId, ct) is { } access ? access.Allowed ? (await service.GetCashCountAsync(access.Tenant, legalEntityId, cashCountId, ct) is { } result ? Results.Ok(result) : Results.NotFound()) : Results.Forbid() : Results.Unauthorized());
        group.MapPut("/cash-counts/{cashCountId:guid}/state", async (Guid legalEntityId, Guid cashCountId, CashCountStatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var permission = request.TargetState.Equals("Approved", StringComparison.OrdinalIgnoreCase) || request.TargetState.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ? "banking.cash.count.approve" : "banking.cash.difference.resolve"; var access = await AccessAsync(http, subjects, authorizer, permission, legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.ChangeCashCountStateAsync(access.Tenant, access.ActorId, legalEntityId, cashCountId, request.TargetState, request.Version, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/cash-counts/{cashCountId:guid}/submit", async (Guid legalEntityId, Guid cashCountId, CashCountStatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await ChangeCashCount(http, service, subjects, authorizer, legalEntityId, cashCountId, "Submitted", request.Version, "banking.cash.count", ct));
        group.MapPost("/cash-counts/{cashCountId:guid}/approve", async (Guid legalEntityId, Guid cashCountId, CashCountStatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await ChangeCashCount(http, service, subjects, authorizer, legalEntityId, cashCountId, "Approved", request.Version, "banking.cash.count.approve", ct));
        group.MapPost("/cash-counts/{cashCountId:guid}/finalize", async (Guid legalEntityId, Guid cashCountId, CashCountStatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await ChangeCashCount(http, service, subjects, authorizer, legalEntityId, cashCountId, "Finalized", request.Version, "banking.cash.count.approve", ct));
        group.MapGet("/cash-position", async (Guid legalEntityId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.cash_position.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.GetCashPositionAsync(access.Tenant, legalEntityId, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/cash-position/by-entity", async (Guid legalEntityId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.cash_position.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.GetCashPositionAsync(access.Tenant, legalEntityId, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/cash-position/by-account", async (Guid legalEntityId, Guid treasuryAccountId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.cash_position.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok((await service.GetCashPositionAsync(access.Tenant, legalEntityId, ct)).Where(item => item.TreasuryAccountId == treasuryAccountId)) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/cash-position/by-currency", async (Guid legalEntityId, string currencyCode, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.cash_position.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok((await service.GetCashPositionAsync(access.Tenant, legalEntityId, ct)).Where(item => item.CurrencyCode.Equals(currencyCode.Trim(), StringComparison.OrdinalIgnoreCase))) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/cash-forecast", async (Guid legalEntityId, DateOnly? asOfDate, int? horizonDays, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.cash_forecast.read", legalEntityId, ct) is { } access ? access.Allowed ? (await service.GetForecastAsync(access.Tenant, legalEntityId, asOfDate, horizonDays, ct) is { } result ? Results.Ok(result) : Results.NotFound()) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/cash-forecast/versions/{versionId:guid}", async (Guid legalEntityId, Guid versionId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.cash_forecast.read", legalEntityId, ct) is { } access ? access.Allowed ? (await service.GetForecastVersionAsync(access.Tenant, legalEntityId, versionId, ct) is { } result ? Results.Ok(result) : Results.NotFound()) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/cash-forecast/versions", async (CreateForecastVersionRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.cash_forecast.manage", request.LegalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/cash-forecast", await service.CreateForecastVersionAsync(access.Tenant, access.ActorId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/cash-forecast/manual-items", async (CreateForecastItemRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.cash_forecast.manage", request.LegalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/cash-forecast", await service.CreateForecastItemAsync(access.Tenant, access.ActorId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/cash-counts/{cashCountId:guid}/difference-resolutions", async (Guid legalEntityId, Guid cashCountId, ResolveCashDifferenceRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.cash.difference.resolve", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/cash-difference-resolutions", await service.ResolveCashDifferenceAsync(access.Tenant, access.ActorId, legalEntityId, cashCountId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/cash-accounts/{accountId:guid}/custody-transfers", async (Guid legalEntityId, Guid accountId, TransferCustodyRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.cash.count", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/cash-custody-transfers", await service.TransferCashCustodyAsync(access.Tenant, access.ActorId, legalEntityId, accountId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/transfers", async (CreateTransferRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.transfers.initiate", request.LegalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault(); if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "banking.transfer_idempotency_required" }); return Results.Accepted("/api/v1/banking/transfers", await service.CreateTransferAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, key, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/transfers/reconciliation", async (Guid legalEntityId, Guid? accountId, string? state, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.transfers.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListTransferReconciliationsAsync(access.Tenant, legalEntityId, accountId, state, page ?? 1, pageSize ?? 50, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/transfers/{transferId:guid}/reconcile", async (Guid legalEntityId, Guid transferId, ReconcileTransferRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.reconciliation.match", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.ReconcileTransferAsync(access.Tenant, access.ActorId, legalEntityId, transferId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/opening-reconciliations", async (Guid legalEntityId, Guid? treasuryAccountId, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reconciliation.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListOpeningReconciliationsAsync(access.Tenant, legalEntityId, treasuryAccountId, page ?? 1, pageSize ?? 25, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/opening-reconciliations", async (CreateOpeningReconciliationRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.reconciliation.match", request.LegalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/opening-reconciliations", await service.CreateOpeningReconciliationAsync(access.Tenant, access.ActorId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/opening-reconciliations/{id:guid}", async (Guid legalEntityId, Guid id, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reconciliation.read", legalEntityId, ct) is { } access ? access.Allowed ? (await service.GetOpeningReconciliationAsync(access.Tenant, legalEntityId, id, ct) is { } result ? Results.Ok(result) : Results.NotFound()) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/opening-reconciliations/{id:guid}/finalize", async (Guid legalEntityId, Guid id, FinalizeOpeningReconciliationRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.reconciliation.finalize", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.FinalizeOpeningReconciliationAsync(access.Tenant, access.ActorId, legalEntityId, id, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/treasury-accounts/{accountId:guid}/connections", async (Guid legalEntityId, Guid accountId, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.connections.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListConnectionsAsync(access.Tenant, legalEntityId, accountId, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/treasury-accounts/{accountId:guid}/connections", async (Guid legalEntityId, Guid accountId, CreateConnectionRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.connections.manage", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created("/api/v1/banking/connections", await service.CreateConnectionAsync(access.Tenant, access.ActorId, legalEntityId, accountId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapPost("/connections/{connectionId:guid}/refresh", async (Guid legalEntityId, Guid connectionId, StatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await ChangeConnection(http, service, subjects, authorizer, legalEntityId, connectionId, "Connected", request.Version ?? 1, ct));
        group.MapPost("/connections/{connectionId:guid}/disconnect", async (Guid legalEntityId, Guid connectionId, StatusRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await ChangeConnection(http, service, subjects, authorizer, legalEntityId, connectionId, "Disconnected", request.Version ?? 1, ct));
        group.MapGet("/pos-cash-settlements", async (Guid legalEntityId, string? status, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.cash.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListPosCashSettlementsAsync(access.Tenant, legalEntityId, page ?? 1, pageSize ?? 25, status, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/pos-cash-settlements/{settlementId:guid}/confirm", async (Guid legalEntityId, Guid settlementId, ConfirmPosCashSettlementRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.cash.count.approve", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.ConfirmPosCashSettlementAsync(access.Tenant, access.ActorId, legalEntityId, settlementId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/provider-settlements", async (Guid legalEntityId, string? status, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reconciliation.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListProviderSettlementsAsync(access.Tenant, legalEntityId, page ?? 1, pageSize ?? 25, status, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapPost("/provider-settlements/{settlementId:guid}/match", async (Guid legalEntityId, Guid settlementId, MatchProviderSettlementRequest request, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "banking.reconciliation.match", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.MatchProviderSettlementAsync(access.Tenant, access.ActorId, legalEntityId, settlementId, request, ct)); } catch (BankingException ex) { return Problem(ex); }
        });
        group.MapGet("/reports/bank-activity", async (Guid legalEntityId, Guid? treasuryAccountId, string? classification, string? direction, DateOnly? fromDate, DateOnly? toDate, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reports.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListBankActivityAsync(access.Tenant, legalEntityId, treasuryAccountId, classification, direction, fromDate, toDate, page ?? 1, pageSize ?? 50, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/reports/cash-to-ledger", async (Guid legalEntityId, DateOnly? asOfDate, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reports.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.GetCashToLedgerReportAsync(access.Tenant, legalEntityId, asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow), ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/cheques/in-transit", async (Guid legalEntityId, int? page, int? pageSize, HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            await AccessAsync(http, subjects, authorizer, "banking.reports.read", legalEntityId, ct) is { } access ? access.Allowed ? Results.Ok(await service.ListChequeInTransitAsync(access.Tenant, legalEntityId, page ?? 1, pageSize ?? 50, ct)) : Results.Forbid() : Results.Unauthorized());
        group.MapGet("/target", async (HttpContext http, BankingService service, Microsoft.Extensions.Configuration.IConfiguration config, CancellationToken ct) =>
        {
            var expectedSecret = config["TODIR_TEST_PROBE_SECRET"];
            if (string.IsNullOrWhiteSpace(expectedSecret)) return Results.Unauthorized();
            var headerSecret = http.Request.Headers["X-Todir-Test-Secret"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(headerSecret) ||
                headerSecret.Length != expectedSecret.Length ||
                !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(headerSecret),
                    System.Text.Encoding.UTF8.GetBytes(expectedSecret)))
            {
                return Results.Unauthorized();
            }
            var tenant = Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tid) ? new TenantId(tid) : new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
            var db = await service.GetDatabaseNameAsync(tenant, ct);
            return Results.Ok(new { database = db });
        }).AllowAnonymous().ExcludeFromDescription();
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
    private static async Task<IResult> ChangeAccountStatus(HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Guid entityId, Guid accountId, string target, long? version, string permission, Guid correlation, CancellationToken ct)
    {
        var access = await AccessAsync(http, subjects, authorizer, permission, entityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
        try { return Results.Ok(await service.ChangeStatusAsync(access.Tenant, access.ActorId, access.MembershipId, correlation, entityId, accountId, target, version, ct)); } catch (BankingException ex) { return Problem(ex); }
    }
    private static async Task<IResult> ChangeConnection(HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Guid entityId, Guid connectionId, string target, long version, CancellationToken ct)
    {
        var access = await AccessAsync(http, subjects, authorizer, "banking.connections.manage", entityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
        try { return Results.Ok(await service.ChangeConnectionStateAsync(access.Tenant, access.ActorId, entityId, connectionId, target, version, ct)); } catch (BankingException ex) { return Problem(ex); }
    }
    private static async Task<IResult> ChangeReconciliation(HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Guid entityId, Guid id, string target, long version, string permission, CancellationToken ct)
    {
        var access = await AccessAsync(http, subjects, authorizer, permission, entityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
        try { return Results.Ok(await service.ChangeReconciliationStateAsync(access.Tenant, access.ActorId, entityId, id, target, version, ct)); } catch (BankingException ex) { return Problem(ex); }
    }
    private static async Task<IResult> ChangeCashCount(HttpContext http, BankingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Guid entityId, Guid id, string target, long version, string permission, CancellationToken ct)
    {
        var access = await AccessAsync(http, subjects, authorizer, permission, entityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
        try { return Results.Ok(await service.ChangeCashCountStateAsync(access.Tenant, access.ActorId, entityId, id, target, version, ct)); } catch (BankingException ex) { return Problem(ex); }
    }
    private static IResult Problem(BankingException ex) => Results.Problem(statusCode: ex.Code.Contains("concurrency", StringComparison.Ordinal) ? 409 : 400, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed);
}

public sealed class BankingModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("banking", "1.0.0", 1, "L3", "module", false, "tenant",
        new(["platform-kernel", "directory", "finbase", "ledger", "subledger", "payments", "pos", "payroll", "tax"], ["platform.files"]),
        new("finance_banking", ["treasury_account", "treasury_account_balance_pocket", "bank_account_profile", "cash_account_profile", "digital_wallet_profile", "settlement_account_profile", "treasury_account_change_request", "treasury_account_connection", "statement_import_batch", "external_statement", "external_statement_line", "external_feed_transaction", "bank_originated_event_candidate", "treasury_activity_projection", "treasury_account_balance_projection", "transfer_reconciliation_projection", "settlement_account_projection", "provider_settlement_projection", "pos_cash_settlement_projection", "statement_transaction_code_mapping", "reconciliation_rule_set", "reconciliation_rule_version", "reconciliation_session", "reconciliation_match", "reconciliation_match_item", "reconciliation_match_system_item", "reconciliation_system_claim", "reconciliation_adjustment", "cash_count", "cash_count_denomination", "cash_difference_resolution", "cash_custodian_history", "cash_position_projection", "cash_forecast_version", "cash_forecast_item", "cash_forecast_projection", "opening_reconciliation"]),
        ["banking.accounts.read", "banking.accounts.create", "banking.accounts.edit", "banking.accounts.activate", "banking.accounts.suspend", "banking.accounts.close", "banking.accounts.sensitive_data.read", "banking.accounts.change.approve", "banking.connections.read", "banking.connections.manage", "banking.statements.read", "banking.statements.import", "banking.statements.validate", "banking.reconciliation.read", "banking.reconciliation.match", "banking.reconciliation.finalize", "banking.reconciliation.reopen", "banking.bank_originated.review", "banking.bank_originated.approve", "banking.cash.read", "banking.cash.count", "banking.cash.count.approve", "banking.cash.difference.resolve", "banking.transfers.read", "banking.transfers.initiate", "banking.cash_position.read", "banking.cash_forecast.read", "banking.cash_forecast.manage", "banking.reports.read", "banking.audit.read", "banking.export", "banking.settings.manage"],
        new(["banking.treasury-accounts", "banking.statement-evidence", "banking.reconciliation", "banking.cash-position", "banking.cash-forecast"], ["payments.money-movement", "payments.internal-transfers", "directory.party", "finbase.money", "ledger.semantic-facts", "subledger.open-items", "platform.audit", "platform.files"]),
        new(["TreasuryAccountActivatedV1", "TreasuryAccountSuspendedV1", "TreasuryAccountClosedV1", "ExternalStatementImportedV1", "ReconciliationFinalizedV1", "CashCountFinalizedV1", "BankOriginatedFinancialEventApprovedV1"], ["PaymentCompletedV1", "PaymentReversedV1", "PaymentRefundedV1", "InternalTransferCompletedV1", "ExternalFeedTransactionReceivedV1", "ProviderSettlementImportedV1", "PosSessionClosedV1"]), new(false, "blocked_if_dependents"));
}
