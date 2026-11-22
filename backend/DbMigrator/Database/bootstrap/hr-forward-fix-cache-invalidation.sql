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

create or replace function platform.hr_employment_cache_revision_trigger() returns trigger
language plpgsql
security definer
set search_path = pg_catalog, platform
as $$
declare
    tenant_value uuid;
    entity_value uuid;
begin
    if tg_op = 'DELETE' then
        tenant_value := old.tenant_id;
        entity_value := old.legal_entity_id;
    else
        tenant_value := new.tenant_id;
        entity_value := new.legal_entity_id;
    end if;
    perform platform.advance_cache_dependency_revision(tenant_value, 'hr.employee.directory', coalesce(entity_value::text, 'all'), entity_value, null);
    perform platform.advance_cache_dependency_revision(tenant_value, 'hr.employee.directory', 'all', null, null);
    if tg_op = 'DELETE' then return old; end if;
    return new;
end;
$$;

drop trigger if exists hr_employee_cache_revision on business_hr.employee;
create trigger hr_employee_cache_revision after insert or update or delete on business_hr.employee
for each row execute function platform.hr_employee_cache_revision_trigger();

drop trigger if exists hr_employment_cache_revision on business_hr.employment;
create trigger hr_employment_cache_revision after insert or update or delete on business_hr.employment
for each row execute function platform.hr_employment_cache_revision_trigger();
