-- Forward-only correction for the Redis directory freshness model.
-- PostgreSQL revisions are authoritative. An entity read observes both the
-- tenant-wide revision and its entity/branch revision, so a name/status/profile
-- change on employee cannot leave an entity-scoped directory warm.

create or replace function platform.hr_employment_cache_revision_trigger() returns trigger
language plpgsql
security definer
set search_path = pg_catalog, platform
as $$
begin
    if tg_op = 'DELETE' then
        perform platform.advance_cache_dependency_revision(old.tenant_id, 'hr.employee.directory', old.legal_entity_id::text, old.legal_entity_id, null);
        perform platform.advance_cache_dependency_revision(old.tenant_id, 'hr.employee.directory', 'all', null, null);
        return old;
    end if;

    if tg_op = 'UPDATE' and old.tenant_id is distinct from new.tenant_id then
        perform platform.advance_cache_dependency_revision(old.tenant_id, 'hr.employee.directory', old.legal_entity_id::text, old.legal_entity_id, null);
        perform platform.advance_cache_dependency_revision(old.tenant_id, 'hr.employee.directory', 'all', null, null);
    elsif tg_op = 'UPDATE' and old.legal_entity_id is distinct from new.legal_entity_id then
        perform platform.advance_cache_dependency_revision(old.tenant_id, 'hr.employee.directory', old.legal_entity_id::text, old.legal_entity_id, null);
    end if;

    perform platform.advance_cache_dependency_revision(new.tenant_id, 'hr.employee.directory', new.legal_entity_id::text, new.legal_entity_id, null);
    perform platform.advance_cache_dependency_revision(new.tenant_id, 'hr.employee.directory', 'all', null, null);
    return new;
end;
$$;

drop trigger if exists hr_employment_cache_revision on business_hr.employment;
create trigger hr_employment_cache_revision after insert or update or delete on business_hr.employment
for each row execute function platform.hr_employment_cache_revision_trigger();

create or replace function platform.hr_assignment_cache_revision_trigger() returns trigger
language plpgsql
security definer
set search_path = pg_catalog, platform
as $$
declare
    tenant_value uuid;
    entity_value uuid;
    branch_value uuid;
begin
    if tg_op = 'DELETE' then
        tenant_value := old.tenant_id;
        entity_value := old.legal_entity_id;
        branch_value := old.branch_id;
    else
        tenant_value := new.tenant_id;
        entity_value := new.legal_entity_id;
        branch_value := new.branch_id;
    end if;

    if tg_op = 'UPDATE' and (
        old.branch_id is distinct from new.branch_id
        or old.legal_entity_id is distinct from new.legal_entity_id
    ) then
        perform platform.advance_cache_dependency_revision(old.tenant_id, 'hr.employee.directory', old.legal_entity_id::text || ':' || coalesce(old.branch_id::text, 'all'), old.legal_entity_id, old.branch_id);
    end if;

    perform platform.advance_cache_dependency_revision(tenant_value, 'hr.employee.directory', entity_value::text || ':' || coalesce(branch_value::text, 'all'), entity_value, branch_value);
    if tg_op = 'DELETE' then return old; end if;
    return new;
end;
$$;

drop trigger if exists hr_assignment_cache_revision on business_hr.employment_assignment;
create trigger hr_assignment_cache_revision after insert or update or delete on business_hr.employment_assignment
for each row execute function platform.hr_assignment_cache_revision_trigger();

create index if not exists ix_platform_cache_dependency_revision_scope
    on platform.cache_dependency_revision(tenant_id, dependency, legal_entity_id, branch_id, revision desc);
