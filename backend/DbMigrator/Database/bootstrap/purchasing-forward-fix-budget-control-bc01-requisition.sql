-- BC01: Purchasing owns the requisition-to-order precommitment chain.
alter table business_purchasing.purchase_order_line
    add column if not exists requisition_line_id uuid;

create table if not exists business_purchasing.purchase_requisition (
    tenant_id uuid not null,
    legal_entity_id uuid not null,
    id uuid not null,
    supplier_party_id uuid not null,
    currency_code char(3) not null,
    requested_date date not null,
    state text not null default 'Draft',
    approval_state text not null default 'NotRequired',
    total_amount numeric(20,6) not null default 0,
    approved_by uuid,
    approved_at timestamptz,
    approval_request_id uuid,
    cancelled_at timestamptz,
    closed_at timestamptz,
    row_version bigint not null default 1,
    created_by uuid not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (tenant_id,id),
    unique (tenant_id,legal_entity_id,id),
    foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id),
    check (state in ('Draft','PendingApproval','Approved','PartiallyConverted','FullyConverted','Cancelled','Closed')),
    check (approval_state in ('NotRequired','Pending','Approved','Rejected')),
    check (currency_code ~ '^[A-Z]{3}$'),
    check (total_amount >= 0),
    check (row_version > 0),
    check ((approval_state = 'Approved') = (approved_at is not null))
);

create table if not exists business_purchasing.purchase_requisition_line (
    tenant_id uuid not null,
    legal_entity_id uuid not null,
    requisition_id uuid not null,
    id uuid not null,
    sequence integer not null,
    product_variant_id uuid,
    unit_of_measure_id uuid,
    unit_code text,
    description text not null,
    requested_quantity numeric(20,6) not null,
    converted_quantity numeric(20,6) not null default 0,
    cancelled_quantity numeric(20,6) not null default 0,
    unit_price numeric(20,6) not null,
    created_at timestamptz not null default now(),
    primary key (tenant_id,requisition_id,id),
    unique (tenant_id,legal_entity_id,requisition_id,id),
    unique (tenant_id,legal_entity_id,id),
    unique (tenant_id,requisition_id,sequence),
    foreign key (tenant_id,legal_entity_id,requisition_id) references business_purchasing.purchase_requisition(tenant_id,legal_entity_id,id),
    check (sequence > 0 and requested_quantity > 0 and unit_price >= 0),
    check (converted_quantity >= 0 and cancelled_quantity >= 0 and converted_quantity + cancelled_quantity <= requested_quantity)
);

create table if not exists business_purchasing.purchase_requisition_approval (
    tenant_id uuid not null,
    legal_entity_id uuid not null,
    requisition_id uuid not null,
    id uuid not null,
    decision text not null,
    actor_id uuid not null,
    reason text,
    idempotency_key text not null,
    created_at timestamptz not null default now(),
    primary key (tenant_id,requisition_id,id),
    unique (tenant_id,requisition_id,idempotency_key),
    foreign key (tenant_id,legal_entity_id,requisition_id) references business_purchasing.purchase_requisition(tenant_id,legal_entity_id,id),
    check (decision in ('Approved','Rejected')),
    check (length(btrim(idempotency_key)) between 1 and 200)
);

create table if not exists business_purchasing.purchase_requisition_conversion (
    tenant_id uuid not null,
    legal_entity_id uuid not null,
    id uuid not null,
    requisition_id uuid not null,
    requisition_line_id uuid not null,
    purchase_order_id uuid not null,
    purchase_order_line_id uuid not null,
    quantity numeric(20,6) not null,
    amount numeric(20,6) not null,
    source_version text not null,
    idempotency_key text not null,
    created_by uuid not null,
    created_at timestamptz not null default now(),
    primary key (tenant_id,id),
    unique (tenant_id,requisition_id,requisition_line_id,idempotency_key),
    unique (tenant_id,purchase_order_id,purchase_order_line_id,requisition_line_id),
    foreign key (tenant_id,legal_entity_id,requisition_id) references business_purchasing.purchase_requisition(tenant_id,legal_entity_id,id),
    foreign key (tenant_id,legal_entity_id,requisition_id,requisition_line_id) references business_purchasing.purchase_requisition_line(tenant_id,legal_entity_id,requisition_id,id),
    foreign key (tenant_id,legal_entity_id,purchase_order_id,purchase_order_line_id) references business_purchasing.purchase_order_line(tenant_id,legal_entity_id,purchase_order_id,id),
    check (quantity > 0 and amount >= 0),
    check (length(btrim(source_version)) between 1 and 120),
    check (length(btrim(idempotency_key)) between 1 and 200)
);

do $$ begin
    if not exists (select 1 from pg_constraint where conname = 'purchase_order_line_requisition_fk') then
        alter table business_purchasing.purchase_order_line no force row level security;
        alter table business_purchasing.purchase_order_line
            add constraint purchase_order_line_requisition_fk
            foreign key (tenant_id,legal_entity_id,requisition_line_id)
            references business_purchasing.purchase_requisition_line(tenant_id,legal_entity_id,id);
        alter table business_purchasing.purchase_order_line force row level security;
    end if;
end $$;

do $$ declare t text; begin
    foreach t in array array['purchase_requisition','purchase_requisition_line','purchase_requisition_approval','purchase_requisition_conversion'] loop
        execute format('alter table business_purchasing.%I enable row level security', t);
        execute format('alter table business_purchasing.%I force row level security', t);
        execute format('drop policy if exists tenant_isolation on business_purchasing.%I', t);
        execute format('create policy tenant_isolation on business_purchasing.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    end loop;
end $$;

create index if not exists ix_purchase_requisition_scope on business_purchasing.purchase_requisition(tenant_id,legal_entity_id,state,requested_date desc,id desc);
create index if not exists ix_purchase_requisition_line_open on business_purchasing.purchase_requisition_line(tenant_id,legal_entity_id,requisition_id,requested_quantity,converted_quantity,cancelled_quantity);
create index if not exists ix_purchase_requisition_conversion_source on business_purchasing.purchase_requisition_conversion(tenant_id,requisition_id,requisition_line_id,created_at desc);
grant select,insert,update on business_purchasing.purchase_requisition, business_purchasing.purchase_requisition_line, business_purchasing.purchase_requisition_approval, business_purchasing.purchase_requisition_conversion to erp_app;
