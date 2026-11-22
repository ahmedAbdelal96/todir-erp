-- Employee profile/status changes affect every legal-entity directory projection
-- for the tenant. A tenant-wide logical-clock revision is sufficient because
-- directory reads take the effective maximum of tenant and entity scopes.
create or replace function platform.hr_employee_cache_revision_trigger() returns trigger
language plpgsql
security definer
set search_path = pg_catalog, platform
as $$
begin
    if tg_op = 'DELETE' then
        perform platform.advance_cache_dependency_revision(old.tenant_id, 'hr.employee.directory', 'all', null, null);
        return old;
    end if;

    perform platform.advance_cache_dependency_revision(new.tenant_id, 'hr.employee.directory', 'all', null, null);
    return new;
end;
$$;

drop trigger if exists hr_employee_cache_revision on business_hr.employee;
create trigger hr_employee_cache_revision
after insert or delete or update of arabic_name, english_name, preferred_name, display_name, status
on business_hr.employee
for each row execute function platform.hr_employee_cache_revision_trigger();
