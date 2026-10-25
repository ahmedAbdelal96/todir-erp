using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Modules.Subledger.Services;

public static class SubledgerObservability
{
    public static readonly ActivitySource ActivitySource = new("Subledger", "1.0.0");
    private static readonly Meter Meter = new("Subledger", "1.0.0");

    public static readonly Histogram<double> ApplicationDuration = Meter.CreateHistogram<double>("subledger_application_duration", "s");
    public static readonly Counter<long> ApplicationLines = Meter.CreateCounter<long>("subledger_application_lines", "{line}");
    public static readonly Histogram<double> OpenItemsQueryDuration = Meter.CreateHistogram<double>("subledger_open_items_query_duration", "s");
    public static readonly Histogram<double> AutoMatchDuration = Meter.CreateHistogram<double>("subledger_auto_match_duration", "s");
    public static readonly Counter<long> ApplicationConflicts = Meter.CreateCounter<long>("subledger_application_conflict_total", "{conflict}");
    public static readonly Counter<long> WriteOffs = Meter.CreateCounter<long>("subledger_writeoff_total", "{write_off}");
    public static readonly Counter<long> FxSettlements = Meter.CreateCounter<long>("subledger_fx_settlement_total", "{settlement}");
    public static readonly Histogram<double> ReconciliationDifference = Meter.CreateHistogram<double>("subledger_reconciliation_difference", "base_currency");
    public static readonly Histogram<double> ExposureQueryDuration = Meter.CreateHistogram<double>("subledger_exposure_query_duration", "s");
}
