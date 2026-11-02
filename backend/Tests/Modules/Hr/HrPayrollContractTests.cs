using System.Text.Json;
using Shared.Contracts.Hr;
using Xunit;

namespace Tests.Modules.Hr;

public sealed class HrPayrollContractTests
{
    [Fact]
    public void PayrollSnapshotPreservesLeaveAndAbsenceFactsWithoutCalculatingMoney()
    {
        var leave = new HrPayrollLeaveContext(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "ANNUAL", "Approved", "Paid", 2,
            new(2026, 11, 10), new(2026, 11, 11), 2m, "Days");
        var finalExit = new HrPayrollFinalExitContext(Guid.CreateVersion7(), "Resignation", "VOLUNTARY_RESIGNATION", null,
            new(2026, 11, 30), new(2026, 11, 30), true, [new(Guid.CreateVersion7(), "ANNUAL", 4.5m, "Days")]);
        var input = new HrPayrollEmploymentInput(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "EMP-0001", "Active", [], [], [],
            new(20, 9_600, 15, 0, 120, 60, 1), [leave], new(2, 1), [], finalExit);
        var json = JsonSerializer.Serialize(input);
        var restored = JsonSerializer.Deserialize<HrPayrollEmploymentInput>(json);

        Assert.NotNull(restored);
        Assert.Equal("Paid", Assert.Single(restored.Leave).PayrollTreatment);
        Assert.Equal(2m, restored.Leave[0].ChargedQuantityInPeriod);
        Assert.Equal(2, restored.Absence.UnexcusedAbsenceDays);
        Assert.Equal(1, restored.Absence.ApprovedLeaveAttendanceDays);
        Assert.Equal(60, restored.Attendance.ApprovedOvertimeMinutes);
        Assert.Equal(4.5m, Assert.Single(restored.FinalExit!.UnusedLeaveBalances).Balance);
        Assert.DoesNotContain("amountDue", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("netPay", json, StringComparison.OrdinalIgnoreCase);
    }
}
