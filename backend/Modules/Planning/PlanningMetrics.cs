using System.Diagnostics.Metrics;

namespace Modules.Planning;

/// <summary>Bounded Planning operational signals; dimensions are stable workflow/outcome values only.</summary>
public static class PlanningMetrics
{
    public static readonly Meter Meter = new("Todir.Planning", "1.0.0");
    public static readonly Histogram<double> ActualRefreshDuration = Meter.CreateHistogram<double>("todir.planning.actual_refresh.duration", "s");
    public static readonly Histogram<double> CalculationDuration = Meter.CreateHistogram<double>("todir.planning.calculation.duration", "s");
    public static readonly Histogram<double> BudgetCheckDuration = Meter.CreateHistogram<double>("todir.planning.budget_check.duration", "s");
    public static readonly Counter<long> WorkflowFailures = Meter.CreateCounter<long>("todir.planning.workflow.failures", "{failure}");
    public static readonly Counter<long> BlockDecisions = Meter.CreateCounter<long>("todir.planning.budget.block_decisions", "{decision}");
    public static readonly Counter<long> ImportFailures = Meter.CreateCounter<long>("todir.planning.import.failures", "{format}");
    public static readonly Counter<long> OverrideUsage = Meter.CreateCounter<long>("todir.planning.override.usage", "{outcome}");
    public static readonly Counter<long> ReconciliationDiscrepancies = Meter.CreateCounter<long>("todir.planning.reconciliation.discrepancies", "{outcome}");

    public static void Failure(string workflow, string code) => WorkflowFailures.Add(1, new KeyValuePair<string, object?>("workflow", workflow), new KeyValuePair<string, object?>("code", code));
    public static void Decision(string decision) => BlockDecisions.Add(1, new KeyValuePair<string, object?>("decision", decision));
}
