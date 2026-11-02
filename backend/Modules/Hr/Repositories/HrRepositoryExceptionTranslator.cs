using Modules.Hr.Entities;
using Npgsql;

namespace Modules.Hr.Repositories;

internal static class HrRepositoryExceptionTranslator
{
    public static HrException? Translate(Exception exception)
    {
        if (exception is not PostgresException postgres || postgres.SqlState is not ("23505" or "23P01" or "23514" or "23503"))
            return null;

        var code = postgres.SqlState switch
        {
            "23505" when postgres.ConstraintName == "ux_hr_employee_membership" => "hr.membership_already_linked",
            "23P01" when postgres.MessageText == "hr.contract_active_dates_overlap" => "hr.contract_active_dates_overlap",
            "23P01" when postgres.MessageText == "hr.compensation_dates_overlap" => "hr.compensation_dates_overlap",
            "23P01" when postgres.MessageText == "hr.insurance_dates_overlap" => "hr.insurance_dates_overlap",
            "23P01" => "hr.assignment_effective_dates_overlap",
            "23514" when postgres.MessageText == "hr.department_hierarchy_cycle" => "hr.department_hierarchy_cycle",
            "23514" when postgres.MessageText == "hr.manager_hierarchy_cycle" => "hr.manager_hierarchy_cycle",
            "23514" when postgres.MessageText == "hr.assignment_branch_entity_mismatch" => "hr.assignment_reference_invalid",
            "23514" => "hr.business_rule_violation",
            "23503" => "hr.reference_invalid",
            _ => "hr.duplicate_record"
        };

        return new HrException(code);
    }
}
