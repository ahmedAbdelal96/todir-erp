using Modules.Expenses.DTOs;
using Modules.Expenses.Repositories;
using Shared.Primitives.Identifiers;

namespace Modules.Expenses.Services;

public sealed class ExpenseService(ExpenseRepository repository)
{
    public Task<ExpensePage<ExpenseDto>> ListAsync(TenantId tenant, Guid entity, Guid? employeeId, string? state, string? search, int page, int pageSize, CancellationToken ct) => repository.ListAsync(tenant, entity, employeeId, state, search, page, pageSize, ct);
    public Task<ExpenseDto?> GetAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.GetAsync(tenant, entity, id, ct);
    public Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(TenantId tenant, Guid entity, CancellationToken ct) => repository.ListCategoriesAsync(tenant, entity, ct);
    public Task<ExpenseCategoryDto> CreateCategoryAsync(TenantId tenant, CreateExpenseCategoryRequest request, CancellationToken ct) => repository.CreateCategoryAsync(tenant, request, ct);
    public Task<ExpenseCategoryVersionDto> CreateCategoryVersionAsync(TenantId tenant, Guid entity, Guid categoryId, CreateExpenseCategoryVersionRequest request, CancellationToken ct) => repository.CreateCategoryVersionAsync(tenant, entity, categoryId, request, ct);
    public Task<ExpenseCategoryVersionDto> PublishCategoryVersionAsync(TenantId tenant, Guid entity, Guid versionId, PublishExpenseCategoryVersionRequest request, CancellationToken ct) => repository.PublishCategoryVersionAsync(tenant, entity, versionId, request, ct);
    public Task<ExpenseDto> CreateAsync(TenantId tenant, CreateExpenseRequest request, CancellationToken ct) => repository.CreateAsync(tenant, request, ct);
    public Task<ExpenseDto> UpdateAsync(TenantId tenant, Guid entity, Guid id, UpdateExpenseRequest request, CancellationToken ct) => repository.UpdateAsync(tenant, entity, id, request, ct);
    public Task<IReadOnlyList<ExpenseItemizationDto>> ListItemizationsAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.ListItemizationsAsync(tenant, entity, id, ct);
    public Task<ExpenseItemizationDto> AddItemizationAsync(TenantId tenant, Guid entity, Guid id, CreateExpenseItemizationRequest request, CancellationToken ct) => repository.AddItemizationAsync(tenant, entity, id, request, ct);
    public Task DeleteItemizationAsync(TenantId tenant, Guid entity, Guid id, Guid itemizationId, CancellationToken ct) => repository.DeleteItemizationAsync(tenant, entity, id, itemizationId, ct);
    public Task<ExpenseDto> TransitionAsync(TenantId tenant, Guid entity, Guid id, ExpenseActionRequest request, string action, CancellationToken ct) => repository.TransitionAsync(tenant, entity, id, request, action, ct);
    public Task<IReadOnlyList<ExpenseEvidenceDto>> ListEvidenceAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.ListEvidenceAsync(tenant, entity, id, ct);
    public Task<IReadOnlyList<ExpenseHistoryDto>> ListHistoryAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.ListHistoryAsync(tenant, entity, id, ct);
    public Task<ExpenseEvidenceDto> AddEvidenceAsync(TenantId tenant, Guid entity, Guid id, AddExpenseEvidenceRequest request, CancellationToken ct) => repository.AddEvidenceAsync(tenant, entity, id, request, ct);
}
