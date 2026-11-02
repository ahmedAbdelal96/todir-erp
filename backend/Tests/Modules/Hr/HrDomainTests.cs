using Modules.Hr.Entities;
using Modules.Hr.Services;
using Xunit;

namespace Tests.Modules.Hr;

public sealed class HrDomainTests
{
    [Fact]
    public void EmployeeCanExistWithoutAPlatformMembership()
    {
        var employee = Employee.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "مريم حسن", "Mariam Hassan", null, null);

        Assert.Null(employee.MembershipId);
        Assert.Equal("مريم حسن", employee.DisplayName);
        Assert.Equal(EmployeeStatus.Active, employee.Status);
    }

    [Fact]
    public void EmployeeNamesAreNormalizedAndRequired()
    {
        var employee = Employee.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "  أحمد علي  ", "  Ahmed Ali  ", null, null);

        Assert.Equal("أحمد علي", employee.DisplayName);
        Assert.Throws<HrException>(() => Employee.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), " ", "Ali", null, null));
    }

    [Fact]
    public void ArabicSearchIgnoresDiacriticsAndAlefVariants()
    {
        Assert.Equal(HrSearchNormalization.Normalize("احمد علي"), HrSearchNormalization.Normalize("أَحْمَد عَلِي"));
        Assert.Equal(HrSearchNormalization.Normalize("على"), HrSearchNormalization.Normalize("علي"));
    }

    [Fact]
    public void EmployeeIdentifiersAreEncryptedMaskedAndUniquelySearchable()
    {
        var provider = Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create("hr-tests");
        var crypto = new HrIdentifierCrypto(provider);
        var raw = "29801011234567";
        var encrypted = crypto.Protect(raw);
        var key = HrIdentifierCrypto.CreateLookupKey();

        Assert.DoesNotContain(raw, encrypted, StringComparison.Ordinal);
        Assert.Equal(raw, crypto.Unprotect(encrypted));
        Assert.Equal(HrIdentifierCrypto.Hash(key, HrIdentifierCrypto.Normalize(raw)), HrIdentifierCrypto.Hash(key, HrIdentifierCrypto.Normalize("2980 1011 2345 67")));
        Assert.Equal(new string('•', 10) + "4567", HrIdentifierCrypto.Mask(raw));
        Assert.Equal("••••", HrIdentifierCrypto.Mask("1234"));
    }

    [Fact]
    public void EmploymentRequiresValidScopeAndDateRange()
    {
        var tenant = Guid.CreateVersion7();
        var employee = Guid.CreateVersion7();
        var entity = Guid.CreateVersion7();

        var employment = Employment.Create(Guid.CreateVersion7(), tenant, employee, entity, "EMP-000001-000001", EmploymentType.FullTime, new(2026, 11, 1), null, new(2026, 11, 2));

        Assert.Equal(EmploymentStatus.Active, employment.Status);
        Assert.Throws<HrException>(() => Employment.Create(Guid.CreateVersion7(), tenant, employee, entity, "EMP-000001-000002", EmploymentType.FixedTerm, new(2026, 11, 2), new(2026, 11, 1), new(2026, 11, 2)));
    }

    [Fact]
    public void EmploymentTerminationIsHistoricallyEffectiveAndTerminal()
    {
        var employment = Employment.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "EMP-000001", EmploymentType.FullTime, new(2026, 1, 1), null, new(2026, 11, 2));
        var suspended = employment.TransitionTo(EmploymentStatus.Suspended, new(2026, 10, 1), new(2026, 11, 2), "DISCIPLINARY_REVIEW");
        var terminated = suspended.TransitionTo(EmploymentStatus.Terminated, new(2026, 11, 1), new(2026, 11, 2), "VOLUNTARY_RESIGNATION");

        Assert.Equal(new DateOnly(2026, 11, 1), terminated.EndDate);
        Assert.Equal(3, terminated.Version);
        Assert.Throws<HrException>(() => terminated.TransitionTo(EmploymentStatus.Active, new(2026, 11, 2), new(2026, 11, 2), "REHIRE"));
        Assert.Throws<HrException>(() => employment.TransitionTo(EmploymentStatus.Terminated, new(2026, 11, 3), new(2026, 11, 2), "FUTURE_DATE"));
    }

    [Fact]
    public void ScheduledEmploymentTerminationKeepsCurrentStatusUntilEffectiveDate()
    {
        var employment = Employment.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "EMP-000002", EmploymentType.FullTime, new(2026, 1, 1), null, new(2026, 11, 2));

        var scheduled = employment.ScheduleTermination(EmploymentStatus.Terminated, new(2026, 11, 5), new(2026, 11, 2), "VOLUNTARY_RESIGNATION");

        Assert.Equal(EmploymentStatus.Active, scheduled.Status);
        Assert.Equal(new DateOnly(2026, 11, 5), scheduled.EndDate);
        Assert.Equal(employment.Version + 1, scheduled.Version);
        Assert.Throws<HrException>(() => employment.ScheduleTermination(EmploymentStatus.Suspended, new(2026, 11, 5), new(2026, 11, 2), "FUTURE_DATE"));
    }

    [Fact]
    public void ShiftScheduleSupportsOvernightWorkWithoutAllowingImpossibleDurations()
    {
        Assert.Equal(8 * 60, ShiftScheduleRules.ValidateAndGetDurationMinutes(new(9, 0), new(17, 0), false, 60, 10));
        Assert.Equal(8 * 60, ShiftScheduleRules.ValidateAndGetDurationMinutes(new(22, 0), new(6, 0), true, 45, 5));
        Assert.Throws<HrException>(() => ShiftScheduleRules.ValidateAndGetDurationMinutes(new(9, 0), new(9, 0), false, 0, 0));
        Assert.Throws<HrException>(() => ShiftScheduleRules.ValidateAndGetDurationMinutes(new(22, 0), new(6, 0), true, 480, 5));
        Assert.Throws<HrException>(() => ShiftScheduleRules.ValidateAndGetDurationMinutes(new(9, 0), new(17, 0), false, 0, 241));
    }

    [Fact]
    public void DisciplinaryNarrativeCannotBecomePayrollDeduction()
    {
        HrAdjustmentRules.ValidatePayrollImpact("DisciplinaryAction", "None", "Informational", null, null);
        Assert.Throws<HrException>(() => HrAdjustmentRules.ValidatePayrollImpact("DisciplinaryAction", "SendToPayroll", "OneTime", 100, "EGP"));
        Assert.Throws<HrException>(() => HrAdjustmentRules.ValidatePayrollImpact("Allowance", "SendToPayroll", "Informational", 100, "EGP"));
        HrAdjustmentRules.ValidatePayrollImpact("Allowance", "SendToPayroll", "Recurring", 1500, "EGP");
    }

    [Fact]
    public void LeaveRequestQuantityHonorsUnitsPartialDaysAndCalendarCharge()
    {
        Assert.Equal(3m, LeavePolicyRules.ValidateRequestedQuantity("Days", false, null, 3m));
        Assert.Equal(1.5m, LeavePolicyRules.ValidateRequestedQuantity("Days", true, 1.5m, 3m));
        Assert.Equal(4m, LeavePolicyRules.ValidateRequestedQuantity("Hours", true, 4m, 0m));
        Assert.Throws<HrException>(() => LeavePolicyRules.ValidateRequestedQuantity("Hours", true, null, 0m));
        Assert.Throws<HrException>(() => LeavePolicyRules.ValidateRequestedQuantity("Days", false, 1.5m, 3m));
        Assert.Throws<HrException>(() => LeavePolicyRules.ValidateRequestedQuantity("Days", true, 4m, 3m));
    }

    [Fact]
    public void LeaveReservationRespectsConfiguredNegativeBalancePolicy()
    {
        LeavePolicyRules.EnsureBalanceAllowsRequest("Disallow", 0m, 3m, 2m);
        LeavePolicyRules.EnsureBalanceAllowsRequest("AllowWithinLimit", 2m, 1m, 3m);
        LeavePolicyRules.EnsureBalanceAllowsRequest("Allow", 0m, 0m, 1m);
        Assert.Throws<HrException>(() => LeavePolicyRules.EnsureBalanceAllowsRequest("Disallow", 0m, 1m, 2m));
        Assert.Throws<HrException>(() => LeavePolicyRules.EnsureBalanceAllowsRequest("AllowWithinLimit", 1m, 1m, 3m));
    }

    [Fact]
    public void CountryProviderRegistryKeepsRegionalRulesBehindAStableProviderBoundary()
    {
        var provider = new TestCountryProvider();
        var registry = new HrCountryProviderRegistry([provider]);

        Assert.Null(registry.Find("MISSING"));
        Assert.Equal("TEST", registry.Find("TEST")!.Code);
        Assert.True(registry.Find("TEST")!.ValidateEmployeeIdentifier("EmployeeCode", "A-123"));
        Assert.Equal("TEST-ANNUAL", registry.Find("TEST")!.GetLeaveTypeTemplates().Single().Code);
        Assert.Equal(new DateOnly(2026, 1, 1), registry.Find("TEST")!.GetHolidays(2026).Single().Date);
        Assert.Throws<InvalidOperationException>(() => new HrCountryProviderRegistry([provider, provider]));
    }

    private sealed class TestCountryProvider : IHrCountryProvider
    {
        public string Code => "TEST";
        public string Version => "1.0";
        public bool ValidateEmployeeIdentifier(string identifierType, string value) => identifierType == "EmployeeCode" && value.StartsWith("A-", StringComparison.Ordinal);
        public IReadOnlyList<HrLeaveTypeTemplate> GetLeaveTypeTemplates() => [new("TEST-ANNUAL", "إجازة تجريبية", "Test leave", "Annual", "Days", "company-policy")];
        public IReadOnlyList<HrHolidayDefinition> GetHolidays(int year) => [new(new DateOnly(year, 1, 1), "عطلة تجريبية", null, "test-source")];
    }
}
