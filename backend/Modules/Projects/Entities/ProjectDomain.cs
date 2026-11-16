namespace Modules.Projects.Entities;

public static class ProjectTypes
{
    public const string Customer = "CustomerProject";
    public const string Internal = "InternalProject";
    public const string Capital = "CapitalProject";
}

public static class ProjectStates
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string OnHold = "OnHold";
    public const string ExecutionCompleted = "ExecutionCompleted";
    public const string FinancialClosing = "FinancialClosing";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
}

public sealed class ProjectException(string code, string? message = null) : Exception(message ?? code)
{
    public string Code { get; } = code;
}

public sealed class ProjectAggregate
{
    private static readonly Dictionary<string, IReadOnlySet<string>> Transitions = new(StringComparer.Ordinal)
    {
        [ProjectStates.Draft] = new HashSet<string>([ProjectStates.Active, ProjectStates.Cancelled]),
        [ProjectStates.Active] = new HashSet<string>([ProjectStates.OnHold, ProjectStates.ExecutionCompleted, ProjectStates.Cancelled]),
        [ProjectStates.OnHold] = new HashSet<string>([ProjectStates.Active, ProjectStates.Cancelled]),
        [ProjectStates.ExecutionCompleted] = new HashSet<string>([ProjectStates.FinancialClosing, ProjectStates.Active]),
        [ProjectStates.FinancialClosing] = new HashSet<string>([ProjectStates.Closed, ProjectStates.ExecutionCompleted, ProjectStates.Active]),
        [ProjectStates.Closed] = new HashSet<string>(),
        [ProjectStates.Cancelled] = new HashSet<string>()
    };

    public ProjectAggregate(Guid id, string state) { Id = id; State = state; }
    public Guid Id { get; }
    public string State { get; private set; }

    public void TransitionTo(string nextState)
    {
        if (!Transitions.TryGetValue(State, out var allowed) || !allowed.Contains(nextState))
            throw new ProjectException("projects.invalid_lifecycle_transition");
        State = nextState;
    }
}

public static class ProjectRules
{
    public static void ValidateCreate(string projectType, Guid? customerPartyId, string billingModel, decimal? budgetAmount, DateOnly startDate, DateOnly? targetEndDate)
    {
        if (projectType is not (ProjectTypes.Customer or ProjectTypes.Internal or ProjectTypes.Capital)) throw new ProjectException("projects.project_type_invalid");
        if (projectType == ProjectTypes.Customer && customerPartyId is null) throw new ProjectException("projects.customer_required");
        if (projectType != ProjectTypes.Customer && customerPartyId is not null) throw new ProjectException("projects.customer_not_allowed");
        if (projectType == ProjectTypes.Capital && billingModel != "NonBillable") throw new ProjectException("projects.capital_billing_invalid");
        if (projectType == ProjectTypes.Internal && billingModel != "NonBillable") throw new ProjectException("projects.internal_billing_invalid");
        if (billingModel is not ("NonBillable" or "FixedPrice" or "Milestone" or "TimeAndMaterials" or "CostPlus" or "Mixed")) throw new ProjectException("projects.billing_model_invalid");
        if (budgetAmount is < 0) throw new ProjectException("projects.budget_invalid");
        if (targetEndDate is not null && targetEndDate < startDate) throw new ProjectException("projects.date_range_invalid");
    }

    public static void EnsureMutable(string state)
    {
        if (state is ProjectStates.Closed or ProjectStates.Cancelled) throw new ProjectException("projects.project_immutable");
    }
}
