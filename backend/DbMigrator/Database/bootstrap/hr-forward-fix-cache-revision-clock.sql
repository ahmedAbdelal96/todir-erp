-- Forward-only correction for dependency revision allocation.
-- A revision is a tenant/dependency logical clock. Independent counters per
-- scope are unsafe because an unrelated tenant-wide row could be ahead of an
-- entity row and hide a later entity mutation behind effective max().
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
begin
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
grant execute on function platform.advance_cache_dependency_revision(uuid, text, text, uuid, uuid) to erp_app;
