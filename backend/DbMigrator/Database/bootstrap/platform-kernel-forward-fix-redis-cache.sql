-- Redis is an acceleration layer. PostgreSQL owns revisions and invalidation intent.
-- The trigger keeps the revision and durable invalidation Outbox row in the same
-- transaction as the HR change, so a Redis outage cannot create stale truth.
create table if not exists platform.cache_dependency_revision (
    tenant_id uuid not null references platform.tenant(id),
    dependency text not null,
    scope_key text not null,
    legal_entity_id uuid,
    branch_id uuid,
    revision bigint not null default 1,
    updated_at timestamptz not null default now(),
    primary key (tenant_id, dependency, scope_key),
    constraint cache_dependency_revision_revision_check check (revision > 0),
    constraint cache_dependency_revision_dependency_check check (length(dependency) between 1 and 120),
    constraint cache_dependency_revision_scope_key_check check (length(scope_key) between 1 and 180)
);

alter table platform.cache_dependency_revision enable row level security;
alter table platform.cache_dependency_revision force row level security;
drop policy if exists tenant_isolation on platform.cache_dependency_revision;
create policy tenant_isolation on platform.cache_dependency_revision
    using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
grant select, insert, update on table platform.cache_dependency_revision to erp_app;

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
    insert into platform.cache_dependency_revision(tenant_id, dependency, scope_key, legal_entity_id, branch_id, revision, updated_at)
    values (p_tenant_id, p_dependency, p_scope_key, p_legal_entity_id, p_branch_id, 1, now())
    on conflict (tenant_id, dependency, scope_key)
    do update set revision = platform.cache_dependency_revision.revision + 1,
                  updated_at = now(),
                  legal_entity_id = excluded.legal_entity_id,
                  branch_id = excluded.branch_id
    returning revision into next_revision;

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
