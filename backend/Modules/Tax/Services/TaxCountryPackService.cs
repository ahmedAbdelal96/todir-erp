using Modules.Tax.DTOs;
using Modules.Tax.Repositories;

namespace Modules.Tax.Services;

public sealed class TaxCountryPackService(TaxCountryPackRepository repository)
{
    public Task<IReadOnlyList<TaxCountryPackDto>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => repository.ListAsync(tenantId, cancellationToken);
    public Task<TaxCountryPackDto?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => repository.GetAsync(tenantId, id, cancellationToken);
    public Task<TaxCountryPackDto> InstallAsync(Guid tenantId, Guid actorId, Guid correlationId, InstallTaxCountryPackRequest request, CancellationToken cancellationToken) => repository.InstallAsync(tenantId, actorId, correlationId, request, cancellationToken);
    public Task<TaxCountryPackValidationDto> ValidateAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid id, CancellationToken cancellationToken) => repository.ValidateAsync(tenantId, actorId, correlationId, id, cancellationToken);
    public Task<TaxCountryPackDto> ScheduleAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid id, DateOnly? effectiveFrom, CancellationToken cancellationToken) => repository.ScheduleAsync(tenantId, actorId, correlationId, id, effectiveFrom, cancellationToken);
    public Task<TaxCountryPackDto> ActivateAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid id, DateOnly? effectiveFrom, CancellationToken cancellationToken) => repository.ActivateAsync(tenantId, actorId, correlationId, id, effectiveFrom, false, cancellationToken);
    public Task<TaxCountryPackDto> RetireAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid id, string? reason, CancellationToken cancellationToken) => repository.RetireAsync(tenantId, actorId, correlationId, id, reason, cancellationToken);
    public Task<TaxCountryPackDto> RollbackForFutureAsync(Guid tenantId, Guid actorId, Guid correlationId, Guid id, DateOnly effectiveFrom, CancellationToken cancellationToken) => repository.RollbackForFutureAsync(tenantId, actorId, correlationId, id, effectiveFrom, cancellationToken);
    public Task<TaxDraftImpactResult> GetDraftImpactAsync(Guid tenantId, TaxDraftImpactRequest request, CancellationToken cancellationToken) => repository.GetDraftImpactAsync(tenantId, request, cancellationToken);
}
