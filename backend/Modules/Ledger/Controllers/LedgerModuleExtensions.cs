using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Modules.Ledger.Repositories;
using Modules.Ledger.Services;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger;

public static class LedgerModuleExtensions
{
    public static IServiceCollection AddLedgerModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, LedgerModuleManifestProvider>();
        services.AddSingleton<LedgerRepository>();
        services.AddSingleton<LedgerService>();
        services.AddSingleton<ILedgerPostingEngine>(provider => provider.GetRequiredService<LedgerService>());
        return services;
    }

    public static WebApplication MapLedgerModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/ledger").WithTags("Ledger").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/legal-entities/{entityId:guid}/accounts", async (Guid entityId, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.account.read", entityId, ct) is { } access ? await Execute(access, () => service.ListAccountsAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized());
        group.MapPost("/legal-entities/{entityId:guid}/accounts", async (Guid entityId, CreateAccountRequest request, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.account.manage", entityId, ct) is { } access ? await Execute(access, () => service.CreateAccountAsync(access.Tenant, new(entityId), request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapPost("/legal-entities/{entityId:guid}/accounts/{accountId:guid}/archive", async (Guid entityId, Guid accountId, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.account.manage", entityId, ct) is { } access ? await Execute(access, () => service.ArchiveAccountAsync(access.Tenant, new(entityId), accountId, ct)) : Results.Unauthorized());
        group.MapGet("/legal-entities/{entityId:guid}/journals", async (Guid entityId, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.journal.read", entityId, ct) is { } access ? await Execute(access, () => service.ListJournalsAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized());
        group.MapPost("/legal-entities/{entityId:guid}/journals", async (Guid entityId, CreateJournalRequest request, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.journal.manage", entityId, ct) is { } access ? await Execute(access, () => service.CreateJournalAsync(access.Tenant, new(entityId), request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapGet("/legal-entities/{entityId:guid}/fiscal-years", async (Guid entityId, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.period.read", entityId, ct) is { } access ? await Execute(access, () => service.ListFiscalYearsAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized());
        group.MapPost("/legal-entities/{entityId:guid}/fiscal-years", async (Guid entityId, CreateFiscalYearRequest request, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.period.manage", entityId, ct) is { } access ? await Execute(access, () => service.CreateFiscalYearAsync(access.Tenant, new(entityId), request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapGet("/legal-entities/{entityId:guid}/periods", async (Guid entityId, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.period.read", entityId, ct) is { } access ? await Execute(access, () => service.ListPeriodsAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized());
        group.MapPost("/legal-entities/{entityId:guid}/periods", async (Guid entityId, CreatePeriodRequest request, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.period.manage", entityId, ct) is { } access ? await Execute(access, () => service.CreatePeriodAsync(access.Tenant, new(entityId), request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapPost("/legal-entities/{entityId:guid}/periods/{periodId:guid}/soft-close", async (Guid entityId, Guid periodId, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.period.soft_close", entityId, ct) is { } access ? await Execute(access, () => service.SetPeriodStatusAsync(access.Tenant, new(entityId), periodId, PeriodStatus.SoftClosed, ct)) : Results.Unauthorized());
        group.MapPost("/legal-entities/{entityId:guid}/periods/{periodId:guid}/close", async (Guid entityId, Guid periodId, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.period.close", entityId, ct) is { } access ? await Execute(access, () => service.SetPeriodStatusAsync(access.Tenant, new(entityId), periodId, PeriodStatus.Closed, ct)) : Results.Unauthorized());
        group.MapGet("/legal-entities/{entityId:guid}/locks", async (Guid entityId, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.lock.read", entityId, ct) is { } access ? await Execute(access, () => service.ListLocksAsync(access.Tenant, new(entityId), ct)) : Results.Unauthorized());
        group.MapPost("/legal-entities/{entityId:guid}/locks", async (Guid entityId, CreateFinancialLockRequest request, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.lock.manage", entityId, ct) is { } access ? await Execute(access, () => service.CreateLockAsync(access.Tenant, new(entityId), request, ct), StatusCodes.Status201Created) : Results.Unauthorized());
        group.MapGet("/legal-entities/{entityId:guid}/entries", async (Guid entityId, int? limit, HttpContext http, LedgerService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Authorized(http, subjects, authorizer, "ledger.entry.read", entityId, ct) is { } access ? await Execute(access, () => service.ListEntriesAsync(access.Tenant, new(entityId), limit ?? 50, ct)) : Results.Unauthorized());
        return app;
    }

    private static async Task<Access?> Authorized(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId) || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId) || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        var tenant = new TenantId(tenantId); var subject = await subjects.LoadAsync(tenant, membershipId, ct); if (subject is null) return null;
        return new Access(tenant, actorId, membershipId, authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new(entityId))));
    }

    private static async Task<IResult> Execute<T>(Access access, Func<Task<T>> action, int status = StatusCodes.Status200OK)
    {
        if (!access.Allowed) return Results.Forbid();
        try { var result = await action(); return status == StatusCodes.Status201Created ? Results.Created("", result) : Results.Ok(result); }
        catch (LedgerDomainException ex) { return Results.Problem(statusCode: 400, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }); }
    }
    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed);
}

public sealed class LedgerModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("ledger", "1.0.0", 1, "L1", "foundation", false, "tenant", new(["platform-kernel", "finbase"], []), new("finance_ledger", ["account_category", "account", "journal", "fiscal_year", "accounting_period", "financial_lock", "accounting_fact", "posting_rule", "posting_rule_version", "posting_rule_leg", "account_role", "account_mapping", "account_mapping_version", "accounting_configuration_version", "journal_entry", "journal_entry_line"]), ["ledger.account.read", "ledger.account.manage", "ledger.journal.read", "ledger.journal.manage", "ledger.period.read", "ledger.period.manage", "ledger.period.soft_close", "ledger.period.close", "ledger.period.reopen", "ledger.lock.read", "ledger.lock.manage", "ledger.mapping.read", "ledger.mapping.manage", "ledger.entry.read"], new(["ledger.chart-of-accounts", "ledger.posting-engine"], ["finbase.money"]), new(["JournalEntryPostedV1"], []), new(true, "blocked_if_dependents"));
}
