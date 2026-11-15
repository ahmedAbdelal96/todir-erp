using Modules.Projects.Entities;
using Xunit;

namespace Modules.Projects.Tests;

public sealed class ProjectDomainTests
{
    [Fact]
    public void CustomerProjectRequiresCustomerAndKeepsBillingDistinct()
    {
        var customer = Guid.CreateVersion7();
        ProjectRules.ValidateCreate(ProjectTypes.Customer, customer, "FixedPrice", 100m, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1));
        Assert.Throws<ProjectException>(() => ProjectRules.ValidateCreate(ProjectTypes.Customer, null, "FixedPrice", null, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void LifecycleRejectsClosingActiveProjectWithoutExecutionCompletion()
    {
        var project = new ProjectAggregate(Guid.CreateVersion7(), ProjectStates.Active);
        Assert.Throws<ProjectException>(() => project.TransitionTo(ProjectStates.FinancialClosing));
        project.TransitionTo(ProjectStates.ExecutionCompleted);
        project.TransitionTo(ProjectStates.FinancialClosing);
        project.TransitionTo(ProjectStates.Closed);
        Assert.Equal(ProjectStates.Closed, project.State);
    }

    [Fact]
    public void ClosedProjectIsImmutableAndCancelledProjectCannotResume()
    {
        var closed = new ProjectAggregate(Guid.CreateVersion7(), ProjectStates.Closed);
        Assert.Throws<ProjectException>(() => closed.TransitionTo(ProjectStates.FinancialClosing));
        var cancelled = new ProjectAggregate(Guid.CreateVersion7(), ProjectStates.Cancelled);
        Assert.Throws<ProjectException>(() => cancelled.TransitionTo(ProjectStates.Active));
    }

    [Fact]
    public void CapitalProjectCannotUseCustomerBilling()
    {
        Assert.Throws<ProjectException>(() => ProjectRules.ValidateCreate(ProjectTypes.Capital, null, "FixedPrice", null, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void InternalProjectDoesNotRequireCustomer()
    {
        ProjectRules.ValidateCreate(ProjectTypes.Internal, null, "NonBillable", null, new DateOnly(2026, 1, 1), null);
    }

    [Fact]
    public void ProjectDatesCannotRunBackwards()
    {
        Assert.Throws<ProjectException>(() => ProjectRules.ValidateCreate(ProjectTypes.Internal, null, "NonBillable", null, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 31)));
    }
}
