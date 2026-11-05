using System.Security.Cryptography;
using System.Text;
using Modules.CRM.DTOs;
using Modules.CRM.Repositories;
using Shared.Contracts.Directory;
using Shared.Primitives.Identifiers;
using Todir.Sales.Contracts;

namespace Modules.CRM.Services;

public sealed class CrmService(CrmRepository repository, IDirectoryCapabilities directory, IDirectoryPartyWriteCapabilities directoryWrite, ISalesQuotationCapabilities? sales = null)
{
    public Task<CrmPage<LeadDto>> ListLeadsAsync(TenantId tenant, Guid entity, Guid? actor, string? status, string? search, int page, int size, string? scope, CancellationToken ct) => repository.ListLeadsAsync(tenant, entity, actor, status, search, page, size, scope, ct);
    public async Task<LeadDto?> GetLeadAsync(TenantId tenant, Guid entity, Guid id, Guid actor, bool manager, CancellationToken ct) { var lead = await repository.GetLeadAsync(tenant, entity, id, ct); if (lead is not null) await EnsureVisibleAsync(tenant, entity, lead.OwnerId, lead.TeamId, actor, manager, ct); return lead; }
    public async Task<LeadDto> CreateLeadAsync(TenantId tenant, Guid actor, CreateLeadRequest request, string key, bool manager, CancellationToken ct) { if (!manager && request.OwnerId is Guid owner && owner != actor) throw new CrmException("crm.record_forbidden"); return await repository.CreateLeadAsync(tenant, actor, request with { OwnerId = request.OwnerId ?? actor }, key, ct); }
    public async Task<ImportLeadsResult> ImportLeadsAsync(TenantId tenant, Guid actor, Guid entity, ImportLeadsRequest request, bool manager, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Csv)) throw new CrmException("crm.import_empty");
        var rows = request.Csv.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (rows.Length < 2) throw new CrmException("crm.import_header_required");
        var headers = rows[0].Split(',').Select(x => x.Trim().ToLowerInvariant()).ToArray();
        var required = new[] { "displayname", "email", "phone", "source" };
        if (!required.Any(x => headers.Contains(x, StringComparer.Ordinal))) throw new CrmException("crm.import_contact_columns_required");
        var imported = 0; var duplicates = 0; var errors = new List<string>();
        for (var i = 1; i < rows.Length; i++)
        {
            var cells = rows[i].Split(','); string Value(string name) { var index = Array.IndexOf(headers, name); return index >= 0 && index < cells.Length ? cells[index].Trim().Trim('"') : string.Empty; }
            try
            {
                var result = await CreateLeadAsync(tenant, actor, new(entity, Value("displayname"), Value("companyname"), Null(Value("email")), Null(Value("phone")), string.IsNullOrWhiteSpace(Value("source")) ? "Import" : Value("source"), null, null, string.IsNullOrWhiteSpace(Value("priority")) ? "Normal" : Value("priority")), $"import-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Csv))).ToLowerInvariant()}-{i}", manager, ct);
                imported++; if (result.DuplicateCandidateIds.Count > 0) duplicates++;
            }
            catch (CrmException ex) { errors.Add($"row {i + 1}: {ex.Code}"); }
        }
        return new(imported, duplicates, errors);
        static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
    public async Task<LeadDto> UpdateLeadAsync(TenantId tenant, Guid actor, Guid entity, Guid id, UpdateLeadRequest request, bool manager, CancellationToken ct) { var current = await repository.GetLeadAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.lead_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, current.TeamId, actor, manager, ct); return await repository.UpdateLeadAsync(tenant, entity, id, request, ct); }
    public async Task<LeadDto> AssignLeadAsync(TenantId tenant, Guid actor, Guid entity, Guid id, AssignLeadRequest request, bool manager, CancellationToken ct) { var current = await repository.GetLeadAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.lead_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, current.TeamId, actor, manager, ct); return await repository.AssignLeadAsync(tenant, actor, entity, id, request, ct); }
    public async Task<LeadDto> QualifyLeadAsync(TenantId tenant, Guid actor, Guid entity, Guid id, QualifyLeadRequest request, bool manager, CancellationToken ct)
    { var current = await repository.GetLeadAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.lead_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, current.TeamId, actor, manager, ct); if (request.PartyId is Guid party && !await directory.IsActivePartyAsync(tenant, party, entity, ct)) throw new CrmException("crm.party_not_active"); return await repository.QualifyLeadAsync(tenant, actor, entity, id, request, ct); }
    public async Task<LeadDto> DisqualifyLeadAsync(TenantId tenant, Guid actor, Guid entity, Guid id, DisqualifyLeadRequest request, bool manager, CancellationToken ct) { var current = await repository.GetLeadAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.lead_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, current.TeamId, actor, manager, ct); return await repository.DisqualifyLeadAsync(tenant, actor, entity, id, request, ct); }

    public async Task<OpportunityDto> ConvertLeadAsync(TenantId tenant, Guid actor, Guid membership, Guid entity, Guid leadId, ConvertLeadRequest request, bool manager, CancellationToken ct)
    {
        var lead = await repository.GetLeadAsync(tenant, entity, leadId, ct) ?? throw new CrmException("crm.lead_not_found");
        await EnsureVisibleAsync(tenant, entity, lead.OwnerId, lead.TeamId, actor, manager, ct);
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
    public async Task<OpportunityDto?> GetOpportunityAsync(TenantId tenant, Guid entity, Guid id, Guid actor, bool manager, CancellationToken ct) { var opportunity = await repository.GetOpportunityAsync(tenant, entity, id, ct); if (opportunity is not null) await EnsureVisibleAsync(tenant, entity, opportunity.OwnerId, opportunity.TeamId, actor, manager, ct); return opportunity; }
    public async Task<OpportunityDto> CreateOpportunityAsync(TenantId tenant, Guid actor, CreateOpportunityRequest request, bool manager, CancellationToken ct) { if (!manager && request.OwnerId is Guid owner && owner != actor) throw new CrmException("crm.record_forbidden"); return await repository.CreateOpportunityAsync(tenant, actor, request with { OwnerId = request.OwnerId ?? actor }, ct); }
    public async Task<OpportunityDto> UpdateOpportunityAsync(TenantId tenant, Guid actor, Guid entity, Guid id, UpdateOpportunityRequest request, bool manager, CancellationToken ct) { var current = await repository.GetOpportunityAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.opportunity_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, current.TeamId, actor, manager, ct); return await repository.UpdateOpportunityAsync(tenant, entity, id, request, ct); }
    public async Task<OpportunityDto> MoveStageAsync(TenantId tenant, Guid actor, Guid entity, Guid id, MoveOpportunityStageRequest request, bool manager, CancellationToken ct) { var current = await repository.GetOpportunityAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.opportunity_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, current.TeamId, actor, manager, ct); return await repository.MoveStageAsync(tenant, actor, entity, id, request, ct); }
    public async Task<OpportunityDto> MarkWonAsync(TenantId tenant, Guid actor, Guid entity, Guid id, CloseOpportunityRequest request, bool manager, CancellationToken ct) { var current = await repository.GetOpportunityAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.opportunity_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, current.TeamId, actor, manager, ct); return await repository.MarkWonAsync(tenant, actor, entity, id, request, ct); }
    public async Task<OpportunityDto> MarkLostAsync(TenantId tenant, Guid actor, Guid entity, Guid id, CloseOpportunityRequest request, bool manager, CancellationToken ct) { var current = await repository.GetOpportunityAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.opportunity_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, current.TeamId, actor, manager, ct); return await repository.MarkLostAsync(tenant, actor, entity, id, request, ct); }
    public async Task<OpportunityDto> ReopenAsync(TenantId tenant, Guid actor, Guid entity, Guid id, long version, bool manager, CancellationToken ct) { var current = await repository.GetOpportunityAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.opportunity_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, current.TeamId, actor, manager, ct); return await repository.ReopenAsync(tenant, actor, entity, id, version, ct); }
    public Task<CrmPage<ActivityDto>> ListActivitiesAsync(TenantId tenant, Guid entity, Guid? actor, string? state, int page, int size, string? scope, CancellationToken ct) => repository.ListActivitiesAsync(tenant, entity, actor, state, page, size, scope, ct);
    public Task<CrmPage<ActivityDto>> ListSellerActivitiesAsync(TenantId tenant, Guid entity, Guid? actor, string mode, bool allAuthorized, int page, int size, CancellationToken ct) => repository.ListSellerActivitiesAsync(tenant, entity, actor, mode, allAuthorized, page, size, ct);
    public async Task<ActivityDto> CreateActivityAsync(TenantId tenant, Guid actor, CreateActivityRequest request, bool manager, CancellationToken ct) { if (!manager && request.OwnerId is Guid owner && owner != actor) throw new CrmException("crm.record_forbidden"); return await repository.CreateActivityAsync(tenant, actor, request with { OwnerId = request.OwnerId ?? actor }, ct); }
    public async Task<ActivityDto> UpdateActivityAsync(TenantId tenant, Guid actor, Guid entity, Guid id, UpdateActivityRequest request, bool manager, CancellationToken ct) { var current = await repository.GetActivityAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.activity_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, null, actor, manager, ct); return await repository.UpdateActivityAsync(tenant, entity, id, request, ct); }
    public async Task<ActivityDto> CompleteActivityAsync(TenantId tenant, Guid actor, Guid entity, Guid id, CompleteActivityRequest request, bool manager, CancellationToken ct) { var current = await repository.GetActivityAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.activity_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, null, actor, manager, ct); return await repository.CompleteActivityAsync(tenant, actor, entity, id, request, ct); }
    public async Task<ActivityDto> CancelActivityAsync(TenantId tenant, Guid actor, Guid entity, Guid id, long version, bool manager, CancellationToken ct) { var current = await repository.GetActivityAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.activity_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, null, actor, manager, ct); return await repository.CancelActivityAsync(tenant, entity, id, version, ct); }
    public Task<IReadOnlyList<PipelineDto>> ListPipelinesAsync(TenantId tenant, Guid entity, CancellationToken ct) => repository.ListPipelinesAsync(tenant, entity, ct);
    public Task<PipelineDto> CreatePipelineAsync(TenantId tenant, CreatePipelineRequest request, CancellationToken ct) => repository.CreatePipelineAsync(tenant, request, ct);
    public Task<PipelineStageDto> CreateStageAsync(TenantId tenant, Guid entity, Guid pipelineId, CreateStageRequest request, CancellationToken ct) => repository.CreateStageAsync(tenant, entity, pipelineId, request, ct);
    public Task<IReadOnlyList<TeamDto>> ListTeamsAsync(TenantId tenant, Guid entity, CancellationToken ct) => repository.ListTeamsAsync(tenant, entity, ct);
    public Task<TeamDto> CreateTeamAsync(TenantId tenant, CreateTeamRequest request, CancellationToken ct) => repository.CreateTeamAsync(tenant, request, ct);
    public Task<IReadOnlyList<TeamMemberDto>> ListTeamMembersAsync(TenantId tenant, Guid entity, Guid teamId, CancellationToken ct) => repository.ListTeamMembersAsync(tenant, entity, teamId, ct);
    public Task<TeamMemberDto> AddTeamMemberAsync(TenantId tenant, Guid entity, Guid teamId, AddTeamMemberRequest request, CancellationToken ct) => repository.AddTeamMemberAsync(tenant, entity, teamId, request, ct);
    public Task EndTeamMemberAsync(TenantId tenant, Guid entity, Guid teamId, Guid userId, CancellationToken ct) => repository.EndTeamMemberAsync(tenant, entity, teamId, userId, ct);
    public Task<ForecastDto> ForecastAsync(TenantId tenant, Guid entity, DateOnly? from, DateOnly? to, string currency, CancellationToken ct) => repository.ForecastAsync(tenant, entity, from, to, currency, ct);
    public async Task<Customer360Dto?> Customer360Async(TenantId tenant, Guid entity, Guid partyId, CancellationToken ct)
    { var workspace = await repository.Customer360Async(tenant, entity, partyId, ct); if (workspace is null) return null; var snapshot = await directory.ReadInvoiceSnapshotAsync(tenant, partyId, entity, ct); return snapshot is null ? workspace : workspace with { DisplayName = snapshot.DisplayName }; }
    public async Task<Guid> CreateQuotationAsync(TenantId tenant, Guid actor, Guid entity, Guid opportunityId, QuoteHandoffRequest request, bool manager, CancellationToken ct)
    { if (sales is null) throw new CrmException("crm.sales_capability_unavailable"); var opportunity = await repository.GetOpportunityAsync(tenant, entity, opportunityId, ct) ?? throw new CrmException("crm.opportunity_not_found"); await EnsureVisibleAsync(tenant, entity, opportunity.OwnerId, opportunity.TeamId, actor, manager, ct); if (opportunity.PartyId is null) throw new CrmException("crm.opportunity_party_required"); var quote = await sales.CreateDraftQuotationAsync(tenant, actor, new(entity, opportunity.PartyId.Value, request.CurrencyCode, request.QuoteDate, request.ValidUntil, request.Lines.Select(line => new SalesCommercialLine(line.ProductVariantId, line.UnitOfMeasureId, line.Description, line.Quantity, line.UnitPrice, line.DiscountAmount, line.UomCode)).ToArray(), request.CustomerReference, request.IdempotencyKey), ct); await repository.RecordQuoteLinkAsync(tenant, entity, opportunityId, quote, request.IdempotencyKey, ct); return quote; }
    public async Task<OpportunityDto> AssignOpportunityAsync(TenantId tenant, Guid actor, Guid entity, Guid id, AssignOpportunityRequest request, bool manager, CancellationToken ct) { var current = await repository.GetOpportunityAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.opportunity_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, current.TeamId, actor, manager, ct); return await repository.AssignOpportunityAsync(tenant, actor, entity, id, request, ct); }
    public async Task<OpportunityDto> SetForecastCategoryAsync(TenantId tenant, Guid actor, Guid entity, Guid id, ForecastCategoryRequest request, bool manager, CancellationToken ct) { var current = await repository.GetOpportunityAsync(tenant, entity, id, ct) ?? throw new CrmException("crm.opportunity_not_found"); await EnsureVisibleAsync(tenant, entity, current.OwnerId, current.TeamId, actor, manager, ct); return await repository.SetForecastCategoryAsync(tenant, entity, id, request, ct); }
    public async Task<LeadDto> MergeLeadsAsync(TenantId tenant, Guid actor, Guid entity, MergeLeadsRequest request, bool manager, CancellationToken ct) { var source = await repository.GetLeadAsync(tenant, entity, request.SourceLeadId, ct) ?? throw new CrmException("crm.lead_not_found"); await EnsureVisibleAsync(tenant, entity, source.OwnerId, source.TeamId, actor, manager, ct); var target = await repository.GetLeadAsync(tenant, entity, request.TargetLeadId, ct) ?? throw new CrmException("crm.lead_not_found"); await EnsureVisibleAsync(tenant, entity, target.OwnerId, target.TeamId, actor, manager, ct); return await repository.MergeLeadsAsync(tenant, actor, entity, request, ct); }

    private async Task EnsureVisibleAsync(TenantId tenant, Guid entity, Guid? ownerId, Guid? teamId, Guid actor, bool manager, CancellationToken ct)
    {
        if (manager || ownerId == actor) return;
        if (teamId is Guid team && await repository.IsActiveTeamMemberAsync(tenant, entity, team, actor, ct)) return;
        throw new CrmException("crm.record_forbidden");
    }
}
