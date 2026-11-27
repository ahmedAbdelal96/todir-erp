-- BC02 integrity closure: governed top-down targets, bottom-up proposals and
-- database-level protection for published planning truth and Actual snapshots.

create table if not exists business_planning.planning_target (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null,
  legal_entity_id uuid not null, period_start date not null, metric_code text not null,
  target_amount numeric(19,4) not null, currency_code char(3) not null,
  reason text, state text not null default 'Draft', created_by uuid not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id,id),
  unique (tenant_id,plan_version_id,legal_entity_id,period_start,metric_code),
  foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id),
  check (target_amount >= 0), check (state in ('Draft','Approved','Published','Closed'))
);

create table if not exists business_planning.planning_proposal (
  tenant_id uuid not null, id uuid not null, target_id uuid not null,
  department_ref uuid, proposed_amount numeric(19,4) not null, adjusted_amount numeric(19,4),
  currency_code char(3) not null, reason text, adjustment_reason text,
  state text not null default 'Draft', created_by uuid not null, adjusted_by uuid,
  created_at timestamptz not null default now(), adjusted_at timestamptz,
  primary key (tenant_id,id), unique (tenant_id,target_id,department_ref),
  foreign key (tenant_id,target_id) references business_planning.planning_target(tenant_id,id),
  check (proposed_amount >= 0), check (adjusted_amount is null or adjusted_amount >= 0),
  check (state in ('Draft','Submitted','Adjusted','Approved','Rejected'))
);

alter table business_planning.planning_actual_snapshot_fact
  add column if not exists source_version text,
  add column if not exists recognition_identity text,
  add column if not exists fiscal_basis text;

do $$ declare t text; begin
  foreach t in array array['planning_target','planning_proposal'] loop
    execute format('alter table business_planning.%I enable row level security', t);
    execute format('alter table business_planning.%I force row level security', t);
    execute format('drop policy if exists tenant_isolation on business_planning.%I', t);
    execute format('create policy tenant_isolation on business_planning.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    execute format('grant select,insert,update,delete on business_planning.%I to erp_app', t);
  end loop;
end $$;

create or replace function business_planning.prevent_assumption_value_mutation()
returns trigger language plpgsql as $$
declare version_state text;
begin
  if current_user = 'erp_owner' then
    if tg_op='DELETE' then return old; end if;
    return new;
  end if;
  select v.state into version_state
    from business_planning.planning_assumption a
    join business_planning.plan_version v on v.tenant_id=a.tenant_id and v.id=a.plan_version_id
   where a.tenant_id=coalesce(new.tenant_id,old.tenant_id) and a.id=coalesce(new.assumption_id,old.assumption_id);
  if version_state is null or version_state <> 'Draft' then raise exception 'planning.version_immutable' using errcode='P0001'; end if;
  if tg_op='DELETE' then return old; end if;
  return new;
end $$;
drop trigger if exists planning_assumption_value_guard on business_planning.planning_assumption_value;
create trigger planning_assumption_value_guard before insert or update or delete on business_planning.planning_assumption_value
for each row execute function business_planning.prevent_assumption_value_mutation();

create or replace function business_planning.prevent_published_input_mutation()
returns trigger language plpgsql as $$
declare version_state text;
begin
  if current_user = 'erp_owner' then
    if tg_op = 'DELETE' then return old; end if;
    return new;
  end if;
  select v.state into version_state
  from business_planning.planning_coordinate c
  join business_planning.plan_version v on v.tenant_id=c.tenant_id and v.id=c.plan_version_id
  where c.tenant_id=coalesce(new.tenant_id,old.tenant_id)
    and c.id=coalesce(new.coordinate_id,old.coordinate_id);
  if version_state is not null and version_state <> 'Draft' then
    raise exception 'planning.version_immutable' using errcode='P0001';
  end if;
  if tg_op = 'DELETE' then return old; end if;
  return new;
end $$;

drop trigger if exists planning_input_fact_published_guard on business_planning.planning_input_fact;
create trigger planning_input_fact_published_guard before insert or update or delete on business_planning.planning_input_fact
for each row execute function business_planning.prevent_published_input_mutation();
drop trigger if exists planning_calculated_fact_published_guard on business_planning.planning_calculated_fact;
create trigger planning_calculated_fact_published_guard before insert or update or delete on business_planning.planning_calculated_fact
for each row execute function business_planning.prevent_published_input_mutation();

create or replace function business_planning.prevent_actual_snapshot_mutation()
returns trigger language plpgsql as $$
declare current_state text;
begin
  if current_user = 'erp_owner' then
    if tg_op='DELETE' then return old; end if;
    return new;
  end if;
  if tg_table_name = 'planning_actual_snapshot' then
    current_state := old.state;
    if tg_op = 'DELETE' or (tg_op = 'UPDATE' and not (old.state='Published' and new.state='Superseded') and current_state <> 'Staged') then
      raise exception 'planning.actual_snapshot_immutable' using errcode='P0001';
    end if;
  else
    if tg_op = 'INSERT' then
      select s.state into current_state
        from business_planning.planning_actual_snapshot s
       where s.tenant_id=new.tenant_id and s.id=new.snapshot_id;
    else
      select s.state into current_state
        from business_planning.planning_actual_snapshot s
       where s.tenant_id=old.tenant_id and s.id=old.snapshot_id;
    end if;
    if tg_op <> 'INSERT' or current_state is distinct from 'Staged' then
      raise exception 'planning.actual_snapshot_immutable' using errcode='P0001';
    end if;
  end if;
  if tg_op = 'DELETE' then return old; end if;
  return new;
end $$;

drop trigger if exists planning_actual_snapshot_immutable_guard on business_planning.planning_actual_snapshot;
create trigger planning_actual_snapshot_immutable_guard before update or delete on business_planning.planning_actual_snapshot
for each row execute function business_planning.prevent_actual_snapshot_mutation();
drop trigger if exists planning_actual_snapshot_fact_immutable_guard on business_planning.planning_actual_snapshot_fact;
create trigger planning_actual_snapshot_fact_immutable_guard before update or delete on business_planning.planning_actual_snapshot_fact
for each row execute function business_planning.prevent_actual_snapshot_mutation();

create unique index if not exists ux_planning_actual_refresh_identity
  on business_planning.actual_refresh_run(tenant_id,mapping_version,source_watermark)
  where source_watermark is not null;

-- A source fact may legitimately appear in more than one restatement snapshot.
-- Uniqueness belongs to a snapshot, not to the coordinate across all history.
alter table business_planning.planning_actual_snapshot_fact
  drop constraint if exists planning_actual_snapshot_fact_tenant_id_coordinate_id_sourc_key;
create unique index if not exists ux_planning_actual_snapshot_fact_identity
  on business_planning.planning_actual_snapshot_fact(tenant_id,snapshot_id,source_module,source_reference,source_line_id);

grant usage on schema business_planning to erp_app;

create or replace function business_planning.prevent_non_draft_version_row()
returns trigger language plpgsql as $$
declare row_data jsonb; version_state text; version_id uuid;
begin
  if current_user = 'erp_owner' then
    if tg_op='DELETE' then return old; end if;
    return new;
  end if;
  row_data := case when tg_op='DELETE' then to_jsonb(old) else to_jsonb(new) end;
  version_id := nullif(row_data->>'plan_version_id','')::uuid;
  if version_id is not null then
    select state into version_state from business_planning.plan_version where tenant_id=(row_data->>'tenant_id')::uuid and id=version_id;
    if version_state is null or version_state <> 'Draft' then raise exception 'planning.version_immutable' using errcode='P0001'; end if;
  end if;
  if tg_op='DELETE' then return old; end if;
  return new;
end $$;

do $$ declare t text; begin
  foreach t in array array['planning_assumption','planning_spread_rule','planning_allocation_rule','planning_allocation_result','workforce_plan_line','capex_plan_item','revenue_plan_driver','working_capital_assumption','planning_fx_rate','planning_comment'] loop
    execute format('drop trigger if exists planning_version_row_guard on business_planning.%I', t);
    execute format('create trigger planning_version_row_guard before insert or update or delete on business_planning.%I for each row execute function business_planning.prevent_non_draft_version_row()', t);
  end loop;
end $$;
