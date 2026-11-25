-- Actual refresh is keyed by the source watermark and mapping version.
-- Replaying the same source snapshot must not create a second refresh run.
create unique index if not exists ux_planning_actual_refresh_source
  on business_planning.actual_refresh_run(tenant_id, mapping_version, source_watermark)
  where source_watermark is not null;
