using Modules.Payments.Repositories;
using Shared.Contracts.Payments;
using Shared.Primitives.Identifiers;

namespace Modules.Payments.Services;

public sealed class PayrollPaymentCapability(PaymentsRepository repository) : IPayrollPaymentCapability
{
    public Task<IReadOnlyList<Guid>> PreparePayrollPaymentsAsync(TenantId tenant, Guid actorId, Guid membershipId,
        Guid correlationId, Guid legalEntityId, Guid runId, IReadOnlyList<PayrollDisbursementInstruction> instructions,
        CancellationToken cancellationToken) => repository.PreparePayrollPaymentsAsync(tenant, actorId, membershipId,
        correlationId, legalEntityId, runId, instructions, cancellationToken);
}
