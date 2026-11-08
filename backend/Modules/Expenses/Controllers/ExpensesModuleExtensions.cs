using System.Security.Claims;
using Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modules.Expenses.DTOs;
using Modules.Expenses.Repositories;
using Modules.Expenses.Services;
using Shared.Primitives.Identifiers;

namespace Modules.Expenses;

public static class ExpensesModuleExtensions
{
    public static IServiceCollection AddExpensesModule(this IServiceCollection services) { services.AddSingleton<IModuleManifestProvider, ExpensesModuleManifestProvider>(); services.AddSingleton<ExpenseRepository>(); services.AddSingleton<ExpenseService>(); return services; }
    public static WebApplication MapExpensesModule(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/expenses").WithTags("Expenses").RequireAuthorization().RequireRateLimiting("api");
        g.MapGet("/legal-entities/{entityId:guid}/categories", async (Guid entityId, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, () => s.ListCategoriesAsync(Tenant(h), entityId, ct)));
        g.MapPost("/legal-entities/{entityId:guid}/categories", async (Guid entityId, CreateExpenseCategoryRequest r, HttpContext h, ExpenseService s, CancellationToken ct) => r.LegalEntityId != entityId ? Results.BadRequest(new { code = "expenses.entity_scope_invalid" }) : await Execute(h, entityId, () => s.CreateCategoryAsync(Tenant(h), r, ct), 201));
        g.MapPost("/legal-entities/{entityId:guid}/categories/{categoryId:guid}/versions", async (Guid entityId, Guid categoryId, CreateExpenseCategoryVersionRequest r, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, () => s.CreateCategoryVersionAsync(Tenant(h), entityId, categoryId, r, ct), 201));
        g.MapPost("/legal-entities/{entityId:guid}/category-versions/{versionId:guid}/publish", async (Guid entityId, Guid versionId, PublishExpenseCategoryVersionRequest r, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, () => s.PublishCategoryVersionAsync(Tenant(h), entityId, versionId, r, ct)));
        g.MapGet("/legal-entities/{entityId:guid}", async (Guid entityId, Guid? employeeId, string? state, string? search, int? page, int? pageSize, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, () => s.ListAsync(Tenant(h), entityId, employeeId, state, search, page ?? 1, pageSize ?? 50, ct)));
        g.MapPost("/legal-entities/{entityId:guid}", async (Guid entityId, CreateExpenseRequest r, HttpContext h, ExpenseService s, CancellationToken ct) => r.LegalEntityId != entityId ? Results.BadRequest(new { code = "expenses.entity_scope_invalid" }) : await Execute(h, entityId, () => s.CreateAsync(Tenant(h), r, ct), 201));
        g.MapGet("/legal-entities/{entityId:guid}/{id:guid}", async (Guid entityId, Guid id, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, async () => await s.GetAsync(Tenant(h), entityId, id, ct) ?? throw new ExpenseRepositoryException("expenses.expense_not_found")));
        g.MapPut("/legal-entities/{entityId:guid}/{id:guid}", async (Guid entityId, Guid id, UpdateExpenseRequest r, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, () => s.UpdateAsync(Tenant(h), entityId, id, r, ct)));
        foreach (var action in new[] { "submit", "approve", "reject", "finalize", "reverse" }) { var route = $"/legal-entities/{{entityId:guid}}/{{id:guid}}/{action}"; g.MapPost(route, async (Guid entityId, Guid id, ExpenseActionRequest r, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, () => s.TransitionAsync(Tenant(h), entityId, id, r, action, ct))); }
        g.MapGet("/legal-entities/{entityId:guid}/{id:guid}/evidence", async (Guid entityId, Guid id, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, () => s.ListEvidenceAsync(Tenant(h), entityId, id, ct)));
        g.MapGet("/legal-entities/{entityId:guid}/{id:guid}/history", async (Guid entityId, Guid id, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, () => s.ListHistoryAsync(Tenant(h), entityId, id, ct)));
        g.MapPost("/legal-entities/{entityId:guid}/{id:guid}/evidence", async (Guid entityId, Guid id, AddExpenseEvidenceRequest r, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, () => s.AddEvidenceAsync(Tenant(h), entityId, id, r, ct), 201));
        g.MapGet("/legal-entities/{entityId:guid}/{id:guid}/itemizations", async (Guid entityId, Guid id, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, () => s.ListItemizationsAsync(Tenant(h), entityId, id, ct)));
        g.MapPost("/legal-entities/{entityId:guid}/{id:guid}/itemizations", async (Guid entityId, Guid id, CreateExpenseItemizationRequest r, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, () => s.AddItemizationAsync(Tenant(h), entityId, id, r, ct), 201));
        g.MapDelete("/legal-entities/{entityId:guid}/{id:guid}/itemizations/{itemizationId:guid}", async (Guid entityId, Guid id, Guid itemizationId, HttpContext h, ExpenseService s, CancellationToken ct) => await Execute(h, entityId, async () => { await s.DeleteItemizationAsync(Tenant(h), entityId, id, itemizationId, ct); return new { deleted = true }; }));
        return app;
    }
    private static TenantId Tenant(HttpContext h) => new(Guid.Parse(h.User.FindFirstValue("tenant_id")!));
    private static async Task<IResult> Execute<T>(HttpContext h, Guid entity, Func<Task<T>> action, int status = 200)
    { if (!Guid.TryParse(h.User.FindFirstValue("tenant_id"), out _) || (Guid.TryParse(h.User.FindFirstValue("legal_entity_id"), out var claim) && claim != entity)) return Results.Forbid(); try { var value = await action(); return status == 201 ? Results.Created(h.Request.Path, value) : Results.Ok(value); } catch (ExpenseRepositoryException ex) { return Results.Problem(statusCode: ex.Code.Contains("not_found", StringComparison.Ordinal) ? 404 : ex.Code.Contains("concurrent", StringComparison.Ordinal) ? 409 : 400, title: ex.Code, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }); } }
}
public sealed class ExpensesModuleManifestProvider : IModuleManifestProvider
{ public ModuleManifest Manifest { get; } = new("expenses", "1.0.0", 1, "L4", "business-module", false, "tenant", new(["platform-kernel", "hr"], ["directory.party", "ledger.semantic-facts", "payments.money-movement", "platform.files", "platform.approvals"]), new("business_expenses", ["expense_category", "expense_category_version", "expense", "expense_history", "expense_evidence", "expense_itemization", "expense_reimbursement_obligation"]), ["expenses.read", "expenses.create", "expenses.edit", "expenses.submit", "expenses.approve", "expenses.finalize", "expenses.reverse", "expenses.reimbursement.read", "expenses.reimbursement.manage", "expenses.category.manage", "expenses.audit.read"], new(["expenses.claims", "expenses.reimbursements"], ["hr.employee", "directory.party", "ledger.semantic-facts", "payments.money-movement", "platform.files", "platform.approvals"]), new(["ExpenseFinalizedV1", "ExpenseReimbursementRequestedV1"], ["PaymentCompletedV1", "PaymentFailedV1"]), new(true, "blocked_if_dependents")); }
