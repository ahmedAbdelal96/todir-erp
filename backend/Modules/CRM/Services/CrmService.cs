using Modules.CRM.DTOs;
using Modules.CRM.Repositories;
using Shared.Contracts.Directory;
using Shared.Primitives.Identifiers;
using Todir.Sales.Contracts;

namespace Modules.CRM.Services;

public sealed class CrmService(CrmRepository repository, IDirectoryCapabilities directory, IDirectoryPartyWriteCapabilities directoryWrite, ISalesQuotationCapabilities? sales = null)
{
    public Task<CrmPage<LeadDto>> ListLeadsAsync(TenantId tenant, Guid entity, Guid? actor, string? status, string? search, int page, int size, string? scope, CancellationToken ct) => repository.ListLeadsAsync(tenant, entity, actor, status, search, page, size, scope, ct);
    public Task<LeadDto?> GetLeadAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.GetLeadAsync(tenant, entity, id, ct);
    public Task<LeadDto> CreateLeadAsync(TenantId tenant, Guid actor, CreateLeadRequest request, string key, CancellationToken ct) => repository.CreateLeadAsync(tenant, actor, request, key, ct);
    public Task<LeadDto> UpdateLeadAsync(TenantId tenant, Guid entity, Guid id, UpdateLeadRequest request, CancellationToken ct) => repository.UpdateLeadAsync(tenant, entity, id, request, ct);
    public Task<LeadDto> AssignLeadAsync(TenantId tenant, Guid actor, Guid entity, Guid id, AssignLeadRequest request, CancellationToken ct) => repository.AssignLeadAsync(tenant, actor, entity, id, request, ct);
    public async Task<LeadDto> QualifyLeadAsync(TenantId tenant, Guid actor, Guid entity, Guid id, QualifyLeadRequest request, CancellationToken ct)
    { if (request.PartyId is Guid party && !await directory.IsActivePartyAsync(tenant, party, entity, ct)) throw new CrmException("crm.party_not_active"); return await repository.QualifyLeadAsync(tenant, actor, entity, id, request, ct); }
    public Task<LeadDto> DisqualifyLeadAsync(TenantId tenant, Guid actor, Guid entity, Guid id, DisqualifyLeadRequest request, CancellationToken ct) => repository.DisqualifyLeadAsync(tenant, actor, entity, id, request, ct);

    public async Task<OpportunityDto> ConvertLeadAsync(TenantId tenant, Guid actor, Guid membership, Guid entity, Guid leadId, ConvertLeadRequest request, CancellationToken ct)
    {
        var lead = await repository.GetLeadAsync(tenant, entity, leadId, ct) ?? throw new CrmException("crm.lead_not_found");
        var existingConversion = await repository.FindConversionAsync(tenant, entity, leadId, ct);
        if (existingConversion is Guid existing) return await repository.GetOpportunityAsync(tenant, entity, existing, ct) ?? throw new CrmException("crm.conversion_missing");
        if (lead.Status != "Qualified") throw new CrmException("crm.lead_not_qualified");
        var partyId = request.ExistingPartyId;
        if (partyId is null)
        {
            if (string.IsNullOrWhiteSpace(request.NewPartyName) || string.IsNullOrWhiteSpace(request.NewPartyType)) throw new CrmException("crm.party_required");
            partyId = await directoryWrite.CreateCustomerPartyAsync(tenant, actor, membership, new(request.NewPartyType, request.NewPartyName, request.NewPartyName, null, null), request.IdempotencyKey, ct);
        }
        if (!await directory.IsActivePartyAsync(tenant, partyId.Value, entity, ct)) throw new CrmException("crm.party_not_active");
        await directoryWrite.EnsureCustomerRoleAsync(tenant, actor, membership, partyId.Value, entity, ct);
        var opportunity = await repository.CreateOpportunityAsync(tenant, actor, new(entity, partyId, leadId, request.PipelineId, request.StageId, request.OpportunityName, request.EstimatedAmount, request.CurrencyCode, request.ExpectedCloseDate), ct);
        await repository.CompleteLeadConversionAsync(tenant, actor, entity, leadId, partyId.Value, opportunity.Id, request.IdempotencyKey, request.Version, ct);
        return opportunity;
    }

    public Task<CrmPage<OpportunityDto>> ListOpportunitiesAsync(TenantId tenant, Guid entity, Guid? actor, string? status, Guid? pipeline, string? search, int page, int size, string? scope, CancellationToken ct) => repository.ListOpportunitiesAsync(tenant, entity, actor, status, pipeline, search, page, size, scope, ct);
    public Task<OpportunityDto?> GetOpportunityAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) => repository.GetOpportunityAsync(tenant, entity, id, ct);
    public Task<OpportunityDto> CreateOpportunityAsync(TenantId tenant, Guid actor, CreateOpportunityRequest request, CancellationToken ct) => repository.CreateOpportunityAsync(tenant, actor, request, ct);
    public Task<OpportunityDto> UpdateOpportunityAsync(TenantId tenant, Guid entity, Guid id, UpdateOpportunityRequest request, CancellationToken ct) => repository.UpdateOpportunityAsync(tenant, entity, id, request, ct);
    public Task<OpportunityDto> MoveStageAsync(TenantId tenant, Guid actor, Guid entity, Guid id, MoveOpportunityStageRequest request, CancellationToken ct) => repository.MoveStageAsync(tenant, actor, entity, id, request, ct);
    public Task<OpportunityDto> MarkWonAsync(TenantId tenant, Guid actor, Guid entity, Guid id, CloseOpportunityRequest request, CancellationToken ct) => repository.MarkWonAsync(tenant, actor, entity, id, request, ct);
    public Task<OpportunityDto> MarkLostAsync(TenantId tenant, Guid actor, Guid entity, Guid id, CloseOpportunityRequest request, CancellationToken ct) => repository.MarkLostAsync(tenant, actor, entity, id, request, ct);
    public Task<OpportunityDto> ReopenAsync(TenantId tenant, Guid actor, Guid entity, Guid id, long version, CancellationToken ct) => repository.ReopenAsync(tenant, actor, entity, id, version, ct);
    public Task<CrmPage<ActivityDto>> ListActivitiesAsync(TenantId tenant, Guid entity, Guid? actor, string? state, int page, int size, string? scope, CancellationToken ct) => repository.ListActivitiesAsync(tenant, entity, actor, state, page, size, scope, ct);
    public Task<ActivityDto> CreateActivityAsync(TenantId tenant, Guid actor, CreateActivityRequest request, CancellationToken ct) => repository.CreateActivityAsync(tenant, actor, request, ct);
    public Task<ActivityDto> CompleteActivityAsync(TenantId tenant, Guid actor, Guid entity, Guid id, CompleteActivityRequest request, CancellationToken ct) => repository.CompleteActivityAsync(tenant, actor, entity, id, request, ct);
    public Task<ActivityDto> CancelActivityAsync(TenantId tenant, Guid entity, Guid id, long version, CancellationToken ct) => repository.CancelActivityAsync(tenant, entity, id, version, ct);
    public Task<IReadOnlyList<PipelineDto>> ListPipelinesAsync(TenantId tenant, Guid entity, CancellationToken ct) => repository.ListPipelinesAsync(tenant, entity, ct);
    public Task<PipelineDto> CreatePipelineAsync(TenantId tenant, CreatePipelineRequest request, CancellationToken ct) => repository.CreatePipelineAsync(tenant, request, ct);
    public Task<PipelineStageDto> CreateStageAsync(TenantId tenant, Guid entity, Guid pipelineId, CreateStageRequest request, CancellationToken ct) => repository.CreateStageAsync(tenant, entity, pipelineId, request, ct);
    public Task<IReadOnlyList<TeamDto>> ListTeamsAsync(TenantId tenant, Guid entity, CancellationToken ct) => repository.ListTeamsAsync(tenant, entity, ct);
    public Task<TeamDto> CreateTeamAsync(TenantId tenant, CreateTeamRequest request, CancellationToken ct) => repository.CreateTeamAsync(tenant, request, ct);
    public Task<ForecastDto> ForecastAsync(TenantId tenant, Guid entity, DateOnly? from, DateOnly? to, string currency, CancellationToken ct) => repository.ForecastAsync(tenant, entity, from, to, currency, ct);
    public async Task<Customer360Dto?> Customer360Async(TenantId tenant, Guid entity, Guid partyId, CancellationToken ct)
    { var workspace = await repository.Customer360Async(tenant, entity, partyId, ct); if (workspace is null) return null; var snapshot = await directory.ReadInvoiceSnapshotAsync(tenant, partyId, entity, ct); return snapshot is null ? workspace : workspace with { DisplayName = snapshot.DisplayName }; }
    public async Task<Guid> CreateQuotationAsync(TenantId tenant, Guid actor, Guid entity, Guid opportunityId, QuoteHandoffRequest request, CancellationToken ct)
    { if (sales is null) throw new CrmException("crm.sales_capability_unavailable"); var opportunity = await repository.GetOpportunityAsync(tenant, entity, opportunityId, ct) ?? throw new CrmException("crm.opportunity_not_found"); if (opportunity.PartyId is null) throw new CrmException("crm.opportunity_party_required"); var quote = await sales.CreateDraftQuotationAsync(tenant, actor, new(entity, opportunity.PartyId.Value, request.CurrencyCode, request.QuoteDate, request.ValidUntil, request.Lines.Select(line => new SalesCommercialLine(line.ProductVariantId, line.UnitOfMeasureId, line.Description, line.Quantity, line.UnitPrice, line.DiscountAmount, line.UomCode)).ToArray(), request.CustomerReference, request.IdempotencyKey), ct); await repository.RecordQuoteLinkAsync(tenant, entity, opportunityId, quote, request.IdempotencyKey, ct); return quote; }
}
