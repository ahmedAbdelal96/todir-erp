using Modules.Payments.DTOs;
using Modules.Payments.Entities;
using Shared.Contracts.Payments;
using Shared.Primitives.Identifiers;

namespace Modules.Payments.Services;

/// <summary>Owns the incoming cash movement for an employee repayment obligation.</summary>
public sealed class EmployeeRepaymentPaymentCapability(PaymentsService service) : IEmployeeRepaymentPaymentCapability
{
    public async Task<EmployeeRepaymentPaymentResult> ReceiveEmployeeRepaymentAsync(TenantId tenant, Guid actorId, Guid membershipId,
        Guid correlationId, Guid legalEntityId, Guid repaymentId, Guid paymentAccountId, string method, decimal amount,
        string currencyCode, DateOnly paymentDate, DateOnly postingDate, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentMethod>(method, true, out var paymentMethod))
            throw new PaymentException("payments.method_invalid", "A supported payment method is required.");
        var draft = await service.CreateDraftAsync(tenant, actorId, membershipId, correlationId,
            new CreatePaymentRequest(legalEntityId, null, PaymentType.EmployeeRepayment, null,
                amount.ToString(System.Globalization.CultureInfo.InvariantCulture), currencyCode, paymentMethod,
                paymentAccountId, paymentDate, postingDate, null, null, null, null, "ExpensesEmployeeRepayment", $"expenses-repayment:{repaymentId:D}"),
            idempotencyKey, cancellationToken);
        var completed = await service.CompleteManualAsync(tenant, actorId, membershipId, legalEntityId, draft.Id,
            $"{idempotencyKey}:complete", correlationId, cancellationToken);
        return new(completed.Id, completed.Amount, completed.CurrencyCode, completed.State.ToString());
    }
}
