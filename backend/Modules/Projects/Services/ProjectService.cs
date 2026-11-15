using Modules.Projects.DTOs;
using Modules.Projects.Repositories;
using Modules.Projects.Entities;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Projects;
using Shared.Contracts.Invoicing;
using Shared.Contracts.Subledger;
using System.Globalization;

namespace Modules.Projects.Services;

public sealed class ProjectService(ProjectsRepository repository, IInvoicingSourceCapabilities invoicing, ISubledgerCapabilities? subledger = null) : IProjectCapitalizationCapability, IProjectCostFactReader, IProjectCostFactWriter, IProjectSourceIntegrationCapabilities
{
    public Task<ProjectDetail> CreateAsync(TenantId tenant, Guid actor, Guid membership, CreateProjectRequest request, Guid correlationId, CancellationToken ct) => repository.CreateAsync(tenant, actor, membership, request, correlationId, ct);
    public Task<PagedResult<ProjectListItem>> ListAsync(TenantId tenant, Guid legalEntityId, string? search, string? state, int page, int pageSize, CancellationToken ct) => repository.ListAsync(tenant, legalEntityId, search, state, page, pageSize, ct);
    public Task<ProjectDetail?> GetAsync(TenantId tenant, Guid projectId, CancellationToken ct) => repository.GetAsync(tenant, projectId, ct);
    public Task<ProjectDetail> UpdateAsync(TenantId tenant, Guid actor, Guid projectId, UpdateProjectRequest request, CancellationToken ct) => repository.UpdateAsync(tenant, actor, projectId, request, ct);
    public Task<ProjectDetail> TransitionAsync(TenantId tenant, Guid actor, Guid projectId, string state, long version, CancellationToken ct) => repository.TransitionAsync(tenant, actor, projectId, state, version, ct);
    public Task<ProjectTaskView> AddTaskAsync(TenantId tenant, Guid actor, Guid projectId, CreateProjectTaskRequest request, CancellationToken ct) => repository.AddTaskAsync(tenant, actor, projectId, request, ct);
    public Task<IReadOnlyList<ProjectTaskView>> ListTasksAsync(TenantId tenant, Guid projectId, CancellationToken ct) => repository.ListTasksAsync(tenant, projectId, ct);
    public Task<TaskDependencyView> AddDependencyAsync(TenantId tenant, Guid actor, Guid projectId, CreateTaskDependencyRequest request, CancellationToken ct) => repository.AddDependencyAsync(tenant, actor, projectId, request, ct);
    public Task<MilestoneView> AddMilestoneAsync(TenantId tenant, Guid actor, Guid projectId, CreateMilestoneRequest request, CancellationToken ct) => repository.AddMilestoneAsync(tenant, actor, projectId, request, ct);
    public Task<IReadOnlyList<MilestoneView>> ListMilestonesAsync(TenantId tenant, Guid projectId, CancellationToken ct) => repository.ListMilestonesAsync(tenant, projectId, ct);
    public Task<MilestoneView> MilestoneActionAsync(TenantId tenant, Guid actor, Guid milestoneId, string action, MilestoneActionRequest request, CancellationToken ct) => repository.MilestoneActionAsync(tenant, actor, milestoneId, action, request, ct);
    public Task<ProjectTeamMemberView> AddTeamMemberAsync(TenantId tenant, Guid actor, Guid projectId, AddProjectTeamMemberRequest request, CancellationToken ct) => repository.AddTeamMemberAsync(tenant, actor, projectId, request, ct);
    public Task<IReadOnlyList<ProjectTeamMemberView>> ListTeamAsync(TenantId tenant, Guid projectId, CancellationToken ct) => repository.ListTeamAsync(tenant, projectId, ct);
    public Task<TimeEntryView> CreateTimeAsync(TenantId tenant, Guid actor, CreateTimeEntryRequest request, CancellationToken ct) => repository.CreateTimeAsync(tenant, actor, request, ct);
    public Task<TimeEntryView> TimeActionAsync(TenantId tenant, Guid actor, Guid timeId, string action, TimeActionRequest request, CancellationToken ct) => repository.TimeActionAsync(tenant, actor, timeId, action, request.Version, request.Reason, ct);
    public Task<IReadOnlyList<TimeEntryView>> ListTimeAsync(TenantId tenant, Guid projectId, int page, int pageSize, CancellationToken ct) => repository.ListTimeAsync(tenant, projectId, page, pageSize, ct);
    public Task<RateCardView> CreateRateCardAsync(TenantId tenant, Guid actor, CreateRateCardRequest request, CancellationToken ct) => repository.CreateRateCardAsync(tenant, actor, request, ct);
    public Task<BudgetView> CreateBudgetAsync(TenantId tenant, Guid actor, Guid projectId, CreateBudgetRequest request, CancellationToken ct) => repository.CreateBudgetAsync(tenant, actor, projectId, request, ct);
    public Task<BudgetVersionView> CreateBudgetRevisionAsync(TenantId tenant, Guid actor, Guid projectId, Guid budgetId, CreateBudgetRevisionRequest request, CancellationToken ct) => repository.CreateBudgetRevisionAsync(tenant, actor, projectId, budgetId, request, ct);
    public Task<BudgetVersionView> ApproveBudgetVersionAsync(TenantId tenant, Guid actor, Guid budgetVersionId, CancellationToken ct) => repository.ApproveBudgetVersionAsync(tenant, actor, budgetVersionId, ct);
    public Task<ForecastView> CreateForecastAsync(TenantId tenant, Guid actor, Guid projectId, CreateForecastRequest request, CancellationToken ct) => repository.CreateForecastAsync(tenant, actor, projectId, request, ct);
    public Task<CostFactView> RecordCostFactAsync(TenantId tenant, Guid actor, RecordCostFactRequest request, Guid correlationId, CancellationToken ct) => repository.RecordCostFactAsync(tenant, actor, request, correlationId, ct);
    public Task<CommitmentView> RecordCommitmentAsync(TenantId tenant, RecordCommitmentRequest request, CancellationToken ct) => repository.UpsertCommitmentAsync(tenant, request, ct);
    public async Task<FinancialSummary> GetSummaryAsync(TenantId tenant, Guid projectId, CancellationToken ct)
    {
        var summary = await repository.SummaryAsync(tenant, projectId, ct);
        if (subledger is null) return summary;
        var collected = 0m;
        var outstanding = 0m;
        foreach (var (invoiceId, legalEntityId) in await repository.ListInvoiceSourcesAsync(tenant, projectId, ct))
        {
            var settlement = await subledger.GetSourceSettlementSummaryAsync(tenant, new(legalEntityId), "Invoice", invoiceId, ct);
            if (settlement is null) continue;
            collected += ParseMoney(settlement.SettledAmount);
            outstanding += ParseMoney(settlement.OpenAmount);
        }
        return summary with { Collected = collected, Outstanding = outstanding, ExpectedMargin = summary.BillingEligible - summary.ActualCost };
    }

    private static decimal ParseMoney(string value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    public Task<BillingRuleView> CreateBillingRuleAsync(TenantId tenant, Guid actor, Guid projectId, CreateBillingRuleRequest request, CancellationToken ct) => repository.CreateBillingRuleAsync(tenant, actor, projectId, request, ct);
    public Task<BillableChargeView> CreateChargeAsync(TenantId tenant, Guid actor, CreateBillableChargeRequest request, CancellationToken ct) => repository.CreateChargeAsync(tenant, actor, request, ct);
    public async Task<InvoiceRequestView> RequestInvoiceAsync(TenantId tenant, Guid actor, Guid membership, RequestInvoiceRequest request, Guid correlationId, CancellationToken ct)
    {
        var project = await repository.GetAsync(tenant, request.ProjectId, ct) ?? throw new ProjectException("projects.project_not_found");
        if (project.CustomerPartyId is null) throw new ProjectException("projects.customer_required");
        var invoiceRequest = await repository.RequestInvoiceAsync(tenant, actor, request, correlationId, ct);
        if (invoiceRequest.State is "Accepted" or "Completed" or "Rejected" or "Cancelled") return invoiceRequest;
        var charges = await repository.ListInvoiceChargeSourcesAsync(tenant, request.ProjectId, request.ChargeIds, ct);
        var source = new InvoiceSourceDocumentContract("projects", request.ProjectId, "ProjectBilling", request.IdempotencyKey, "Receivable", project.LegalEntityId, null,
            project.CustomerPartyId.Value, null, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), null,
            request.CurrencyCode.ToUpperInvariant(), "Exclusive", null, null, null, charges.Select(charge => new InvoiceSourceLineContract(null, null, null, charge.Description, "1", charge.BillableAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), null, "ProjectBilling", charge.Id.ToString("D"))).ToArray());
        var invoiceId = await invoicing.CreateDraftFromSourceAsync(tenant, actor, membership, source, correlationId, ct);
        return await repository.ApplyInvoiceResultAsync(tenant, actor, invoiceRequest.Id, new ApplyInvoiceResultRequest("Accepted", invoiceId), ct);
    }
    public Task<InvoiceRequestView> ApplyInvoiceResultAsync(TenantId tenant, Guid actor, Guid invoiceRequestId, ApplyInvoiceResultRequest request, CancellationToken ct) => repository.ApplyInvoiceResultAsync(tenant, actor, invoiceRequestId, request, ct);
    public Task<ChangeRequestView> CreateChangeAsync(TenantId tenant, Guid actor, Guid projectId, CreateChangeRequest request, CancellationToken ct) => repository.CreateChangeAsync(tenant, actor, projectId, request, ct);
    public Task<ChangeRequestView> DecideChangeAsync(TenantId tenant, Guid actor, Guid projectId, Guid changeId, ChangeDecisionRequest request, CancellationToken ct) => repository.DecideChangeAsync(tenant, actor, projectId, changeId, request, ct);
    public Task<CloseView> GetCloseReadinessAsync(TenantId tenant, Guid projectId, CancellationToken ct) => repository.CloseReadinessAsync(tenant, projectId, ct);
    public Task<CloseView> CloseAsync(TenantId tenant, Guid actor, Guid projectId, CancellationToken ct) => repository.CloseAsync(tenant, actor, projectId, ct);
    public Task<CloseView> ReopenAsync(TenantId tenant, Guid actor, Guid projectId, CancellationToken ct) => repository.ReopenAsync(tenant, actor, projectId, ct);
    public Task<CapitalHandoffView> CreateCapitalHandoffAsync(TenantId tenant, Guid actor, Guid projectId, CapitalHandoffRequest request, Guid correlationId, CancellationToken ct) => repository.CreateCapitalHandoffAsync(tenant, actor, projectId, request, correlationId, ct);

    public async Task<ProjectCapitalizationResult> RequestAsync(ProjectCapitalizationRequest request, CancellationToken cancellationToken = default)
    {
        var handoff = await repository.CreateCapitalHandoffAsync(request.TenantId, request.ActorId, request.ProjectId,
            new CapitalHandoffRequest(request.CostFactId, request.Amount, request.CurrencyCode, request.IdempotencyKey),
            Guid.CreateVersion7(), cancellationToken);
        return new(handoff.State, handoff.ExternalReference, null);
    }

    public Task<IReadOnlyList<ProjectCostFactContract>> ListAsync(TenantId tenantId, Guid projectId, DateOnly? from = null, DateOnly? to = null,
        CancellationToken cancellationToken = default) => repository.ListCostFactsAsync(tenantId, projectId, from, to, cancellationToken);

    public async Task<ProjectCostFactContract> RecordAsync(ProjectCostFactIngress request, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var result = await repository.RecordCostFactAsync(request.TenantId, request.ActorId,
            new RecordCostFactRequest(request.ProjectId, request.TaskId, request.SourceModule, request.SourceType, request.SourceId, request.SourceLineRef,
                request.CostCategory, request.OriginalCurrency, request.OriginalAmount, request.BaseCurrency, request.BaseAmount, request.ExchangeRate,
                request.FactDate, request.IdempotencyKey, request.CorrectionOfId, request.LegalEntityId), correlationId, cancellationToken);
        return new(result.ProjectId, result.SourceModule, result.SourceType, result.SourceId, result.CostCategory, result.BaseAmount, result.BaseCurrency,
            request.FactDate, result.Id);
    }

    public Task<ProjectCostFactContract> RecordCostAsync(ProjectCostFactIngress request, Guid correlationId, CancellationToken cancellationToken = default) => RecordAsync(request, correlationId, cancellationToken);

    public async Task<ProjectCostFactContract?> RecordReversalAsync(TenantId tenant, Guid actor, Guid projectId, string sourceModule, string sourceId, decimal originalCurrencyAmount, string currencyCode, DateOnly factDate, string idempotencyKey, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var result = await repository.RecordReversalAsync(tenant, actor, projectId, sourceModule, sourceId, originalCurrencyAmount, currencyCode, factDate, idempotencyKey, correlationId, cancellationToken);
        return result is null ? null : new(result.ProjectId, result.SourceModule, result.SourceType, result.SourceId, result.CostCategory, result.BaseAmount, result.BaseCurrency, factDate, result.Id);
    }

    public async Task RecordCommitmentAsync(ProjectCommitmentIngress request, CancellationToken cancellationToken = default)
    {
        await repository.UpsertCommitmentAsync(request.TenantId,
            new RecordCommitmentRequest(request.ProjectId, request.SourceModule, request.SourceId, request.CommitmentDate,
                request.CurrencyCode, request.CommittedAmount, request.ActualizedAmount, request.IdempotencyKey, request.LegalEntityId), cancellationToken);
    }

    public Task CancelCommitmentAsync(TenantId tenant, Guid projectId, string sourceModule, string sourceId, CancellationToken cancellationToken = default) => repository.CancelCommitmentAsync(tenant, projectId, sourceModule, sourceId, cancellationToken);
    public Task ApplyCapitalHandoffResultAsync(TenantId tenant, Guid handoffId, string state, string? externalReference, CancellationToken cancellationToken = default) => repository.ApplyCapitalHandoffResultAsync(tenant, handoffId, state, externalReference, cancellationToken);
}
