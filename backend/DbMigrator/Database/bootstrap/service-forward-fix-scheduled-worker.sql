-- Service-owned scheduler scope. SECURITY DEFINER keeps tenant enumeration behind a narrow module capability.
create or replace function business_service.list_scheduled_scopes()
returns table(tenant_id uuid, legal_entity_id uuid)
language plpgsql
security definer
set search_path = business_service, platform, pg_catalog
as $$
begin
    return query
    select distinct scope.tenant_id, scope.legal_entity_id
    from (
        select p.tenant_id, p.legal_entity_id
        from business_service.maintenance_plan p
        where p.state = 'Active'
        union
        select c.tenant_id, w.legal_entity_id
        from business_service.sla_clock c
        join business_service.work_order w on w.tenant_id = c.tenant_id and w.id = c.work_order_id
        where c.state in ('Running', 'Paused')
    ) scope;
    return;
end;
$$;

revoke all on function business_service.list_scheduled_scopes() from public;
grant execute on function business_service.list_scheduled_scopes() to erp_app;

-- The scheduler must enumerate tenant scopes, while every application table remains FORCE RLS.
-- A no-login, BYPASSRLS dispatcher role is the only owner of this narrow read capability.
reset role;
alter function business_service.list_scheduled_scopes() owner to erp_dispatcher;
set role erp_owner;
