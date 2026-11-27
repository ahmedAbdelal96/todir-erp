-- BC02: every plan-owned row must remain immutable once its plan version is
-- submitted or published.  Owner seeding remains the only controlled bypass.

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
  if version_id is null and tg_table_name = 'planning_proposal' then
    select t.plan_version_id into version_id
      from business_planning.planning_target t
     where t.tenant_id=(row_data->>'tenant_id')::uuid
       and t.id=(row_data->>'target_id')::uuid;
  end if;
  if version_id is not null then
    select state into version_state
      from business_planning.plan_version
     where tenant_id=(row_data->>'tenant_id')::uuid and id=version_id;
    if version_state is null or version_state <> 'Draft' then
      raise exception 'planning.version_immutable' using errcode='P0001';
    end if;
  end if;
  if tg_op = 'DELETE' then return old; end if;
  return new;
end $$;

do $$ declare t text; begin
  foreach t in array array[
    'planning_coordinate','planning_target','planning_proposal'
  ] loop
    execute format('drop trigger if exists planning_version_row_guard on business_planning.%I', t);
    execute format('create trigger planning_version_row_guard before insert or update or delete on business_planning.%I for each row execute function business_planning.prevent_non_draft_version_row()', t);
  end loop;
end $$;
