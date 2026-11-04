using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Payments;

public sealed record PayrollDisbursementInstruction(Guid InstructionId, Guid EmploymentId, decimal Amount, string CurrencyCode);

public interface IPayrollPaymentCapability
{
    Task<IReadOnlyList<Guid>> PreparePayrollPaymentsAsync(TenantId tenant, Guid actorId, Guid membershipId,
        Guid correlationId, Guid legalEntityId, Guid runId, IReadOnlyList<PayrollDisbursementInstruction> instructions,
        CancellationToken cancellationToken);
}
