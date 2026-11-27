-- BC02: pin the Actual basis used by a published planning version.
-- A later rebuild may create a newer current snapshot, but it must not
-- rewrite the historical comparison basis of an already published version.

alter table business_planning.plan_version
  add column if not exists actual_snapshot_id uuid;

-- Foreign-key validation scans the referencing table.  The planning tables are
-- FORCE RLS, so suspend FORCE only for this metadata check and restore it
-- before the migration commits.
alter table business_planning.plan_version no force row level security;
alter table business_planning.planning_actual_snapshot no force row level security;

do $$
begin
  if not exists (select 1 from pg_constraint where conname='plan_version_actual_snapshot_fk') then
    alter table business_planning.plan_version
      add constraint plan_version_actual_snapshot_fk
      foreign key (tenant_id,actual_snapshot_id)
      references business_planning.planning_actual_snapshot(tenant_id,id);
  end if;
end $$;

alter table business_planning.plan_version force row level security;
alter table business_planning.planning_actual_snapshot force row level security;

create index if not exists ix_planning_version_actual_snapshot
  on business_planning.plan_version(tenant_id,actual_snapshot_id);

grant select,insert,update,delete on business_planning.plan_version to erp_app;
