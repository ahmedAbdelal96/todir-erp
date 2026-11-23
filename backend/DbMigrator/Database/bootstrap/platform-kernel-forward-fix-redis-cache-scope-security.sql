-- Assignment triggers use entity:all when an assignment has no branch.
-- Preserve that established scope identity while keeping the function strict.
create or replace function platform.advance_cache_dependency_revision(
    p_tenant_id uuid,
    p_dependency text,
    p_scope_key text,
    p_legal_entity_id uuid,
    p_branch_id uuid
) returns bigint
language plpgsql
security definer
set search_path = pg_catalog, platform
as $$
declare
    next_revision bigint;
    message_id uuid := gen_random_uuid();
    payload jsonb;
    current_tenant uuid := nullif(current_setting('app.tenant_id', true), '')::uuid;
begin
    if current_tenant is null or current_tenant <> p_tenant_id then
        raise exception 'platform.cache_tenant_context_mismatch' using errcode = '42501';
    end if;

    if p_dependency not in ('hr.employee.directory', 'finbase.currency.reference') then
        raise exception 'platform.cache_dependency_not_approved' using errcode = '42501';
    end if;

    if p_scope_key is null or length(p_scope_key) not between 1 and 180 then
        raise exception 'platform.cache_scope_invalid' using errcode = '22023';
    end if;

    if p_legal_entity_id is null and p_branch_id is not null then
        raise exception 'platform.cache_scope_invalid' using errcode = '22023';
    end if;

    if p_legal_entity_id is null and (p_scope_key <> 'all' or p_branch_id is not null) then
        raise exception 'platform.cache_scope_invalid' using errcode = '22023';
    end if;

    if p_legal_entity_id is not null and p_branch_id is null and p_scope_key not in (p_legal_entity_id::text, p_legal_entity_id::text || ':all') then
        raise exception 'platform.cache_scope_invalid' using errcode = '22023';
    end if;

    if p_branch_id is not null and p_scope_key <> p_legal_entity_id::text || ':' || p_branch_id::text then
        raise exception 'platform.cache_scope_invalid' using errcode = '22023';
    end if;

    if p_legal_entity_id is not null and not exists (
        select 1 from platform.legal_entity
        where tenant_id = p_tenant_id and id = p_legal_entity_id
    ) then
        raise exception 'platform.cache_scope_entity_invalid' using errcode = '22023';
    end if;

    if p_branch_id is not null and not exists (
        select 1 from platform.branch
        where tenant_id = p_tenant_id and id = p_branch_id and legal_entity_id = p_legal_entity_id
    ) then
        raise exception 'platform.cache_scope_branch_invalid' using errcode = '22023';
    end if;

    perform pg_advisory_xact_lock(hashtextextended(p_tenant_id::text || ':' || p_dependency, 0));
    select coalesce(max(revision), 0) + 1
      into next_revision
      from platform.cache_dependency_revision
     where tenant_id = p_tenant_id
       and dependency = p_dependency;

    insert into platform.cache_dependency_revision(tenant_id, dependency, scope_key, legal_entity_id, branch_id, revision, updated_at)
    values (p_tenant_id, p_dependency, p_scope_key, p_legal_entity_id, p_branch_id, next_revision, now())
    on conflict (tenant_id, dependency, scope_key)
    do update set revision = excluded.revision,
                  updated_at = now(),
                  legal_entity_id = excluded.legal_entity_id,
                  branch_id = excluded.branch_id;

    payload := jsonb_build_object(
        'dependency', p_dependency,
        'scopeKey', p_scope_key,
        'legalEntityId', p_legal_entity_id,
        'branchId', p_branch_id,
        'revision', next_revision
    );
    insert into platform.outbox_message(id, message_type, payload, tenant_id, occurred_at, correlation_id, causation_id, source_module, status)
    values (message_id, 'PlatformCacheInvalidatedV1', payload, p_tenant_id, now(), message_id, message_id, 'platform', 'Pending');
    return next_revision;
end;
$$;

revoke execute on function platform.advance_cache_dependency_revision(uuid, text, text, uuid, uuid) from public;
grant execute on function platform.advance_cache_dependency_revision(uuid, text, text, uuid, uuid) to erp_app;
