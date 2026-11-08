using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Payments;

public sealed record ExpensePaymentResult(Guid PaymentId, decimal Amount, string CurrencyCode, string State);

public interface IExpensePaymentCapability
{
    Task<ExpensePaymentResult> PayExpenseReimbursementAsync(TenantId tenant, Guid actorId, Guid membershipId,
        Guid correlationId, Guid legalEntityId, Guid paymentAccountId, string method, decimal amount,
        string currencyCode, DateOnly paymentDate, DateOnly postingDate, string idempotencyKey,
        CancellationToken cancellationToken);
}

public sealed record EmployeeRepaymentPaymentResult(Guid PaymentId, decimal Amount, string CurrencyCode, string State);

public interface IEmployeeRepaymentPaymentCapability
{
    Task<EmployeeRepaymentPaymentResult> ReceiveEmployeeRepaymentAsync(TenantId tenant, Guid actorId, Guid membershipId,
        Guid correlationId, Guid legalEntityId, Guid repaymentId, Guid paymentAccountId, string method, decimal amount,
        string currencyCode, DateOnly paymentDate, DateOnly postingDate, string idempotencyKey,
        CancellationToken cancellationToken);
}
