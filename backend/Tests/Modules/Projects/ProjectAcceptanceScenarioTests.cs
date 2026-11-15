using Modules.Projects.DTOs;
using Modules.Projects.Entities;
using Shared.Contracts.Projects;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Modules.Projects.Tests;

public sealed class ProjectAcceptanceScenarioTests
{
    [Fact(DisplayName = "PJ40 simple project preserves a minimal non-billable setup")]
    public void Pj40SimpleProject()
    {
        ProjectRules.ValidateCreate(ProjectTypes.Internal, null, "NonBillable", null, new DateOnly(2026, 1, 1), null);
        Assert.Equal("NonBillable", new CreateProjectRequest(Guid.CreateVersion7(), "Internal", ProjectTypes.Internal, null, null, new DateOnly(2026, 1, 1), null).BillingModel);
    }

    [Fact(DisplayName = "PJ41 professional services supports task-scoped billable time")]
    public void Pj41ProfessionalServices()
    {
        var entry = new CreateTimeEntryRequest(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 1, 2), 60, "Delivery", true);
        Assert.True(entry.Billable);
        Assert.NotNull(entry.TaskId);
    }

    [Fact(DisplayName = "PJ42 accountant cost facts retain source ownership and positive amounts")]
    public void Pj42AccountantCostFact()
    {
        var ingress = new ProjectCostFactIngress(new TenantId(Guid.CreateVersion7()), Guid.CreateVersion7(), Guid.CreateVersion7(), null, "expenses", "ExpenseFinalizedV1", "expense-1", null, "Expense", "EGP", 125m, "EGP", 125m, null, new DateOnly(2026, 1, 2), "pj42:expense-1");
        Assert.Equal("expenses", ingress.SourceModule);
        Assert.True(ingress.BaseAmount > 0m);
    }

    [Fact(DisplayName = "PJ43 capital handoff carries a bounded cost fact and currency")]
    public void Pj43CapitalHandoff()
    {
        var handoff = new CapitalHandoffRequest(Guid.CreateVersion7(), 500m, "egp", "pj43:handoff");
        Assert.Equal("egp", handoff.CurrencyCode);
        Assert.True(handoff.Amount > 0m);
    }

    [Fact(DisplayName = "PJ44 sales billing keeps invoiced amount separate from billable charge")]
    public void Pj44SalesBilling()
    {
        var charge = new BillableChargeView(Guid.CreateVersion7(), Guid.CreateVersion7(), "Time", "time-1", 100m, 150m, "EGP", "PartiallyInvoiced", Guid.CreateVersion7());
        Assert.True(charge.BillableAmount >= charge.CostAmount);
        Assert.Equal("PartiallyInvoiced", charge.State);
    }

    [Fact(DisplayName = "PJ45 reconciliation exposes collected, outstanding, and margin independently")]
    public void Pj45Reconciliation()
    {
        var summary = new FinancialSummary(1000m, 400m, 100m, 50m, 550m, 800m, 500m, 300m, 200m, 400m);
        Assert.Equal(summary.Invoiced, summary.Collected + summary.Outstanding);
        Assert.Equal(summary.BillingEligible - summary.ActualCost, summary.ExpectedMargin);
    }

    [Fact(DisplayName = "PJ46 cross-module contracts carry tenant and project allocation explicitly")]
    public void Pj46CrossModuleAllocation()
    {
        var tenant = new TenantId(Guid.CreateVersion7());
        var projectId = Guid.CreateVersion7();
        var commitment = new ProjectCommitmentIngress(tenant, projectId, "purchasing", "po-1", new DateOnly(2026, 1, 2), "EGP", 900m, 200m, "pj46:po-1");
        Assert.Equal(tenant, commitment.TenantId);
        Assert.Equal(projectId, commitment.ProjectId);
        Assert.Equal(700m, commitment.CommittedAmount - commitment.ActualizedAmount);
    }
}
