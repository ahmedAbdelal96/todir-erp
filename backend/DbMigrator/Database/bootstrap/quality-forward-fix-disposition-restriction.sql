alter table business_quality.quality_disposition
    add column if not exists restriction_id uuid;

create index if not exists ix_quality_disposition_restriction
    on business_quality.quality_disposition (tenant_id, restriction_id)
    where restriction_id is not null;

do $$
begin
    if not exists (
        select 1 from pg_constraint
        where conname = 'fk_quality_disposition_restriction'
    ) then
        alter table business_quality.quality_disposition
            add constraint fk_quality_disposition_restriction
            foreign key (tenant_id, restriction_id)
            references business_quality.quality_restriction (tenant_id, id);
    end if;
end $$;
