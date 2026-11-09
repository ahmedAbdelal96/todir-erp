using Shared.Primitives.Identifiers;

namespace Modules.Expenses.Services;

public sealed record ExpenseOcrResult(System.Text.Json.JsonElement Suggestions);

/// <summary>Provider seam for asynchronous receipt suggestions; suggestions never mutate financial facts.</summary>
public interface IExpenseOcrProvider
{
    Task<ExpenseOcrResult> ExtractAsync(TenantId tenant, Guid fileReference, CancellationToken cancellationToken = default);
}

public sealed class UnavailableExpenseOcrProvider : IExpenseOcrProvider
{
    public Task<ExpenseOcrResult> ExtractAsync(TenantId tenant, Guid fileReference, CancellationToken cancellationToken = default) =>
        throw new ExpenseOcrProviderException("expenses.ocr_provider_unavailable", "No approved OCR provider is configured.");
}

public sealed class ExpenseOcrProviderException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
