using Core.Transactions;
using Modules.Manufacturing.DTOs;
using Modules.Manufacturing.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Manufacturing.Repositories;

public sealed partial class ManufacturingRepository
{
    public Task<IReadOnlyList<CapacityResponse>> GetCapacityAsync(TenantId tenant, Guid plantId, DateOnly from, DateOnly to, Guid? workCenterId, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (from > to || to.DayNumber - from.DayNumber > 366) throw new ManufacturingException("manufacturing.capacity_range_invalid");
            var result = new List<CapacityResponse>();
            await using var centers = Command(context, "select id,default_efficiency from business_manufacturing.work_center where tenant_id=$1 and plant_id=$2 and state='Active' and ($3::uuid is null or id=$3) order by id", tenant.Value, plantId, Db(workCenterId));
            await using var centerReader = await centers.ExecuteReaderAsync(token);
            var workCenters = new List<(Guid Id, decimal Efficiency)>();
            while (await centerReader.ReadAsync(token)) workCenters.Add((centerReader.GetGuid(0), centerReader.GetDecimal(1)));
            await centerReader.DisposeAsync();

            foreach (var workCenter in workCenters)
            {
                await using var calendarCommand = Command(context, "select weekday,shift_start,shift_end,break_minutes from business_manufacturing.work_center_calendar where tenant_id=$1 and work_center_id=$2 and effective_from <= $3 and (effective_to is null or effective_to >= $4)", tenant.Value, workCenter.Id, to, from);
                await using var calendarReader = await calendarCommand.ExecuteReaderAsync(token);
                var calendars = new Dictionary<short, (TimeOnly Start, TimeOnly End, decimal BreakMinutes)>();
                while (await calendarReader.ReadAsync(token)) calendars[calendarReader.GetInt16(0)] = (calendarReader.GetFieldValue<TimeOnly>(1), calendarReader.GetFieldValue<TimeOnly>(2), calendarReader.GetDecimal(3));
                await calendarReader.DisposeAsync();

                var scheduledMinutes = 0m;
                for (var day = from; day <= to; day = day.AddDays(1))
                    if (calendars.TryGetValue((short)day.DayOfWeek, out var calendar)) scheduledMinutes += (decimal)(calendar.End - calendar.Start).TotalMinutes - calendar.BreakMinutes;
                var availableHours = Math.Max(0m, scheduledMinutes) / 60m * workCenter.Efficiency;
                var downtimeMinutes = 0m;
                if (maintenanceDowntime is not null)
                {
                    var legalEntityId = await ScalarAsync<Guid>(context, "select legal_entity_id from business_manufacturing.plant where tenant_id=$1 and id=$2", token, tenant.Value, plantId);
                    var assetIds = new List<Guid>();
                    await using var assetCommand = Command(context, "select distinct r.fixed_asset_id from business_manufacturing.production_operation_resource por join business_manufacturing.production_operation op on op.tenant_id=por.tenant_id and op.id=por.production_operation_id join business_manufacturing.manufacturing_resource r on r.tenant_id=por.tenant_id and r.id=por.resource_id where por.tenant_id=$1 and op.work_center_id=$2 and r.fixed_asset_id is not null", tenant.Value, workCenter.Id);
                    await using var assetReader = await assetCommand.ExecuteReaderAsync(token);
                    while (await assetReader.ReadAsync(token)) assetIds.Add(assetReader.GetGuid(0));
                    if (assetIds.Count > 0)
                    {
                        var intervals = await maintenanceDowntime.ListDowntimeAsync(tenant, legalEntityId, assetIds, from, to, token);
                        for (var day = from; day <= to; day = day.AddDays(1))
                        {
                            if (!calendars.TryGetValue((short)day.DayOfWeek, out var calendar)) continue;
                            var shiftStart = new DateTimeOffset(day.ToDateTime(calendar.Start, DateTimeKind.Utc));
                            var shiftEnd = new DateTimeOffset(day.ToDateTime(calendar.End, DateTimeKind.Utc));
                            var clipped = intervals.Select(item => (Start: item.StartedAt > shiftStart ? item.StartedAt : shiftStart, End: (item.EndedAt ?? shiftEnd) < shiftEnd ? item.EndedAt ?? shiftEnd : shiftEnd))
                                .Where(item => item.End > item.Start).OrderBy(item => item.Start).ToList();
                            var mergedEnd = shiftStart;
                            foreach (var interval in clipped)
                            {
                                var start = interval.Start > mergedEnd ? interval.Start : mergedEnd;
                                if (interval.End > start) downtimeMinutes += (decimal)(interval.End - start).TotalMinutes;
                                if (interval.End > mergedEnd) mergedEnd = interval.End;
                            }
                        }
                    }
                }
                var downtimeHours = downtimeMinutes / 60m * workCenter.Efficiency;
                var plannedMinutes = await ScalarAsync<decimal>(context, "select coalesce(sum(op.setup_minutes + op.run_minutes_per_unit * o.ordered_qty),0) from business_manufacturing.production_operation op join business_manufacturing.production_order o on o.tenant_id=op.tenant_id and o.id=op.production_order_id where op.tenant_id=$1 and op.work_center_id=$2 and o.plant_id=$3 and o.need_date between $4 and $5 and o.state not in ('Cancelled','Closed')", token, tenant.Value, workCenter.Id, plantId, from, to);
                var plannedHours = plannedMinutes / 60m;
                var remainingHours = availableHours - downtimeHours - plannedHours;
                result.Add(new CapacityResponse(workCenter.Id, from, to, decimal.Round(availableHours, 4), decimal.Round(downtimeHours, 4), decimal.Round(plannedHours, 4), decimal.Round(remainingHours, 4), calendars.Count == 0 ? "Unknown" : remainingHours < 0 ? "Overloaded" : "Ready"));
            }
            return (IReadOnlyList<CapacityResponse>)result;
        }, ct);
}
