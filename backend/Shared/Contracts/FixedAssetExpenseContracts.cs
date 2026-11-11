using System.Text.Json;
using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Expenses;

public sealed record FixedAssetExpenseSourceSnapshot(Guid ExpenseId, Guid LegalEntityId, decimal Amount, string CurrencyCode, DateOnly ExpenseDate, string MerchantName, string? BusinessPurpose, string? Description, Guid? AccountingFactId, JsonElement? FinancialContextSnapshot, JsonElement? TaxSnapshot);

public interface IExpenseFixedAssetCapabilities
{
    Task<FixedAssetExpenseSourceSnapshot?> GetFinalizedCapitalSourceAsync(TenantId tenantId, Guid legalEntityId, Guid expenseId, CancellationToken cancellationToken = default);
}
