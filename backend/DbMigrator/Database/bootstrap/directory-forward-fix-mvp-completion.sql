-- Directory MVP completion. The squashed directory baseline remains immutable;
-- this forward fix adds the business fields and indexes required by the canonical pack.

-- The runtime finalization forces RLS on tenant tables. Metadata backfills are
-- intentionally executed by the migration owner before RLS is re-enabled.
alter table foundation_directory.party_role disable row level security;
alter table foundation_directory.party_role
    add column if not exists active_from date,
    add column if not exists active_to date;
update foundation_directory.party_role set active_from = coalesce(created_at::date, current_date) where active_from is null;
alter table foundation_directory.party_role alter column active_from set default current_date;
alter table foundation_directory.party_role alter column active_from set not null;
alter table foundation_directory.party_role enable row level security;
alter table foundation_directory.party_role force row level security;

alter table foundation_directory.address
    add column if not exists label text,
    add column if not exists address_type text not null default 'General',
    add column if not exists building_number text,
    add column if not exists additional_number text,
    add column if not exists short_address text,
    add column if not exists status text not null default 'Active',
    add column if not exists version bigint not null default 1;

alter table foundation_directory.contact_person
    add column if not exists first_name text,
    add column if not exists last_name text,
    add column if not exists department text,
    add column if not exists language_code text,
    add column if not exists notes text,
    add column if not exists status text not null default 'Active',
    add column if not exists version bigint not null default 1;

alter table foundation_directory.contact_point
    add column if not exists status text not null default 'Active',
    add column if not exists verified_at timestamp with time zone;
alter table foundation_directory.contact_point drop constraint if exists contact_point_contact_type_check;
alter table foundation_directory.contact_point add constraint contact_point_contact_type_check
    check (contact_type in ('Email','Phone','Mobile','WhatsApp','Website','Other'));

alter table foundation_directory.party_contact
    add column if not exists usage text not null default 'General',
    add column if not exists valid_from date,
    add column if not exists valid_to date,
    add column if not exists status text not null default 'Active';
alter table foundation_directory.party_contact drop constraint if exists party_contact_usage_check;
alter table foundation_directory.party_contact add constraint party_contact_usage_check
    check (usage in ('General','Billing','Shipping','Legal'));

alter table foundation_directory.party_address
    add column if not exists valid_from date,
    add column if not exists valid_to date;
alter table foundation_directory.party_address drop constraint if exists party_address_usage_check;
alter table foundation_directory.party_address add constraint party_address_usage_check
    check (usage in ('General','Billing','Shipping','Legal','Registered'));

alter table foundation_directory.party_alias
    add column if not exists language_code text;

alter table foundation_directory.party_merge_redirect
    add column if not exists reason text,
    add column if not exists conflict_details jsonb not null default '{}'::jsonb;

alter table foundation_directory.party_visibility drop constraint if exists party_visibility_scope_type_check;
alter table foundation_directory.party_visibility add constraint party_visibility_scope_type_check
    check (scope_type in ('TenantWide','SelectedLegalEntities','SingleLegalEntity','LegalEntity','Branch'));

alter table foundation_directory.duplicate_candidate
    add column if not exists resolved_by uuid,
    add column if not exists resolution_reason text;

create index if not exists ix_directory_party_identity_search
    on foundation_directory.party using gin (display_name_normalized public.gin_trgm_ops);
create index if not exists ix_directory_party_legal_trade_search
    on foundation_directory.party (tenant_id, legal_name, trade_name, name_ar, name_en);
create index if not exists ix_directory_party_alias_search
    on foundation_directory.party_alias (tenant_id, party_id, alias_normalized);
create index if not exists ix_directory_contact_person_search
    on foundation_directory.contact_person (tenant_id, display_name_normalized);
create index if not exists ix_directory_party_contact_active
    on foundation_directory.party_contact (tenant_id, party_id, status, usage);
create index if not exists ix_directory_party_address_usage
    on foundation_directory.party_address (tenant_id, party_id, usage, is_default);
create index if not exists ix_directory_open_duplicate_pair
    on foundation_directory.duplicate_candidate
    (tenant_id, least(party_id, candidate_party_id), greatest(party_id, candidate_party_id), status);

grant select, insert, update, delete on all tables in schema foundation_directory to erp_app;

create table if not exists foundation_directory.import_operation (
    tenant_id uuid not null,
    id uuid not null,
    idempotency_key text not null,
    operation_type text not null,
    response_json jsonb,
    created_at timestamp with time zone not null,
    completed_at timestamp with time zone,
    primary key (tenant_id, id),
    unique (tenant_id, idempotency_key),
    foreign key (tenant_id) references platform.tenant(id)
);
alter table foundation_directory.import_operation enable row level security;
alter table foundation_directory.import_operation force row level security;
drop policy if exists tenant_isolation on foundation_directory.import_operation;
create policy tenant_isolation on foundation_directory.import_operation
    using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
grant select, insert, update, delete on foundation_directory.import_operation to erp_app;
