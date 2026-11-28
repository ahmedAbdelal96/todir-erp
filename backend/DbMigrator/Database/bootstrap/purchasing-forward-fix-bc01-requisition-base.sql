-- The requisition history forward-fix is ordered before the original BC01
-- requisition definition.  Create the two parent tables first so a fresh
-- database can apply the checked-in migration chain deterministically.
-- The later budget-control requisition migration remains authoritative for
-- additional columns, child tables, policies, indexes and grants.

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
