namespace Modules.Hr.Entities;

public static class HrSearchNormalization
{
    public static string Normalize(string value)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var rune in (value ?? string.Empty).Normalize(System.Text.NormalizationForm.FormKC).EnumerateRunes())
        {
            var scalar = rune.Value;
            if (scalar is >= 0x064B and <= 0x065F or 0x0670 or 0x0640) continue;
            scalar = scalar switch { 0x0622 or 0x0623 or 0x0625 or 0x0671 => 0x0627, 0x0649 => 0x064A, _ => scalar };
            builder.Append(new System.Text.Rune(scalar).ToString().ToLowerInvariant());
        }
        return builder.ToString();
    }
}

public enum EmployeeStatus { Active, Inactive, Archived }
public enum EmploymentStatus { PreHire, Active, Suspended, NoticePeriod, Terminated, Ended }
public enum EmploymentType { FullTime, PartTime, Temporary, FixedTerm, Probationary, Intern, Seasonal, Other }

public static class ShiftScheduleRules
{
    public static int ValidateAndGetDurationMinutes(TimeOnly start, TimeOnly end, bool overnight, short breakMinutes, short graceMinutes)
    {
        if (breakMinutes is < 0 or > 720 || graceMinutes is < 0 or > 240 || (overnight ? end >= start : end <= start))
            throw new HrException("hr.shift_definition_invalid");
        var duration = (int)(end - start).TotalMinutes;
        if (breakMinutes >= duration) throw new HrException("hr.shift_break_exceeds_duration");
        return duration;
    }
}

public static class HrAdjustmentRules
{
    public static void ValidatePayrollImpact(string kind, string impact, string recurrence, decimal? amount, string? currencyCode)
    {
        if (impact is not ("None" or "SendToPayroll")) throw new HrException("hr.adjustment_invalid");
        if (impact == "SendToPayroll" && (kind != "Allowance" || amount is null or < 0 || string.IsNullOrWhiteSpace(currencyCode) || recurrence == "Informational"))
            throw new HrException(kind == "DisciplinaryAction" ? "hr.disciplinary_payroll_impact_not_configured" : "hr.adjustment_payroll_input_invalid");
        if (kind == "DisciplinaryAction" && amount is not null) throw new HrException("hr.disciplinary_amount_not_allowed");
    }
}

public sealed record Employee(
    Guid Id,
    Guid TenantId,
    string ArabicName,
    string? EnglishName,
    string? PreferredName,
    string DisplayName,
    EmployeeStatus Status,
    Guid? MembershipId,
    long Version)
{
    public static Employee Create(Guid id, Guid tenantId, string arabicName, string? englishName, string? preferredName, Guid? membershipId)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty) throw new HrException("hr.employee_identity_invalid");
        var arabic = NormalizeName(arabicName, "hr.employee_arabic_name_required");
        var english = string.IsNullOrWhiteSpace(englishName) ? null : NormalizeName(englishName, "hr.employee_english_name_invalid");
        var preferred = string.IsNullOrWhiteSpace(preferredName) ? null : NormalizeName(preferredName, "hr.employee_preferred_name_invalid");
        return new(id, tenantId, arabic, english, preferred, preferred ?? arabic, EmployeeStatus.Active, membershipId, 1);
    }

    public Employee UpdateNames(string arabicName, string? englishName, string? preferredName)
    {
        var arabic = NormalizeName(arabicName, "hr.employee_arabic_name_required");
        var english = string.IsNullOrWhiteSpace(englishName) ? null : NormalizeName(englishName, "hr.employee_english_name_invalid");
        var preferred = string.IsNullOrWhiteSpace(preferredName) ? null : NormalizeName(preferredName, "hr.employee_preferred_name_invalid");
        return this with
        {
            ArabicName = arabic,
            EnglishName = english,
            PreferredName = preferred,
            DisplayName = preferred ?? arabic,
            Version = checked(Version + 1)
        };
    }

    private static string NormalizeName(string value, string requiredCode)
    {
        var normalized = (value ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormKC);
        if (normalized.Length == 0) throw new HrException(requiredCode);
        if (normalized.Length > 200) throw new HrException("hr.employee_name_too_long");
        return normalized;
    }
}

public sealed record Employment(
    Guid Id,
    Guid TenantId,
    Guid EmployeeId,
    Guid LegalEntityId,
    string EmploymentNumber,
    EmploymentType Type,
    EmploymentStatus Status,
    DateOnly StartDate,
    DateOnly? EndDate,
    long Version)
{
    public static Employment Create(Guid id, Guid tenantId, Guid employeeId, Guid legalEntityId, string number,
        EmploymentType type, DateOnly startDate, DateOnly? endDate, DateOnly asOf)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || employeeId == Guid.Empty || legalEntityId == Guid.Empty)
            throw new HrException("hr.employment_identity_invalid");
        if (string.IsNullOrWhiteSpace(number) || number.Length > 40) throw new HrException("hr.employment_number_invalid");
        if (startDate == default || endDate < startDate) throw new HrException("hr.employment_date_range_invalid");
        if (!Enum.IsDefined(type)) throw new HrException("hr.employment_type_invalid");
        return new(id, tenantId, employeeId, legalEntityId, number, type,
            startDate > asOf ? EmploymentStatus.PreHire : EmploymentStatus.Active, startDate, endDate, 1);
    }

    public Employment TransitionTo(EmploymentStatus target, DateOnly effectiveOn, DateOnly asOf, string reasonCode)
    {
        if (!Enum.IsDefined(target) || effectiveOn == default || effectiveOn > asOf || effectiveOn < StartDate || EndDate is { } plannedEnd && effectiveOn > plannedEnd || string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Trim().Length > 80)
            throw new HrException("hr.employment_transition_invalid");
        var allowed = Status switch
        {
            EmploymentStatus.PreHire => target is EmploymentStatus.Active or EmploymentStatus.Terminated or EmploymentStatus.Ended,
            EmploymentStatus.Active => target is EmploymentStatus.Suspended or EmploymentStatus.NoticePeriod or EmploymentStatus.Terminated,
            EmploymentStatus.Suspended => target is EmploymentStatus.Active or EmploymentStatus.NoticePeriod or EmploymentStatus.Terminated,
            EmploymentStatus.NoticePeriod => target is EmploymentStatus.Active or EmploymentStatus.Terminated or EmploymentStatus.Ended,
            _ => false
        };
        if (!allowed) throw new HrException("hr.employment_transition_not_allowed");
        var endDate = target is EmploymentStatus.Terminated or EmploymentStatus.Ended ? effectiveOn : EndDate;
        return this with { Status = target, EndDate = endDate, Version = checked(Version + 1) };
    }

    public Employment ScheduleTermination(EmploymentStatus target, DateOnly effectiveOn, DateOnly asOf, string reasonCode)
    {
        if (target is not (EmploymentStatus.Terminated or EmploymentStatus.Ended) || effectiveOn <= asOf || effectiveOn < StartDate
            || EndDate is { } plannedEnd && effectiveOn > plannedEnd || string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Trim().Length > 80)
            throw new HrException("hr.employment_transition_invalid");
        if (Status is not (EmploymentStatus.PreHire or EmploymentStatus.Active or EmploymentStatus.Suspended or EmploymentStatus.NoticePeriod))
            throw new HrException("hr.employment_transition_not_allowed");
        return this with { EndDate = effectiveOn, Version = checked(Version + 1) };
    }
}

public sealed class HrException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
