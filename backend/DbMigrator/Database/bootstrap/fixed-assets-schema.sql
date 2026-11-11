SET search_path = business_assets, public;
SET row_security = off;

CREATE SCHEMA IF NOT EXISTS business_assets AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA business_assets FROM PUBLIC;
GRANT USAGE ON SCHEMA business_assets TO erp_app;
CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE TABLE business_assets.asset_category (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    asset_nature text NOT NULL,
    tracking_granularity text NOT NULL,
    requires_location boolean NOT NULL DEFAULT false,
    requires_custody boolean NOT NULL DEFAULT false,
    requires_depreciation boolean NOT NULL DEFAULT true,
    capitalization_threshold numeric(20,6),
    default_depreciation_method text NOT NULL DEFAULT 'StraightLine',
    default_useful_life_months integer,
    default_residual_value numeric(20,6) NOT NULL DEFAULT 0,
    active boolean NOT NULL DEFAULT true,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,code),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    CHECK (asset_nature IN ('Land','Building','LeaseholdImprovement','Vehicle','Equipment','Furniture','Intangible','CIP','Other')),
    CHECK (tracking_granularity IN ('Individual','Grouped')),
    CHECK (default_depreciation_method IN ('StraightLine','NoDepreciation','DecliningBalance')),
    CHECK (capitalization_threshold IS NULL OR capitalization_threshold >= 0),
    CHECK (default_residual_value >= 0),
    CHECK (default_useful_life_months IS NULL OR default_useful_life_months > 0),
    CHECK (default_depreciation_method = 'NoDepreciation' OR default_useful_life_months IS NOT NULL OR asset_nature IN ('Land','CIP'))
);

CREATE TABLE business_assets.asset_category_version (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    category_id uuid NOT NULL,
    version integer NOT NULL,
    effective_from date NOT NULL,
    effective_to date,
    policy jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,category_id,version),
    FOREIGN KEY (tenant_id,category_id) REFERENCES business_assets.asset_category(tenant_id,id),
    CHECK (version > 0), CHECK (effective_to IS NULL OR effective_to >= effective_from), CHECK (jsonb_typeof(policy) = 'object')
);

CREATE TABLE business_assets.fixed_asset (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    category_id uuid NOT NULL,
    asset_number text NOT NULL,
    name text NOT NULL,
    description text,
    asset_tag text,
    lifecycle_state text NOT NULL DEFAULT 'Draft',
    asset_nature text NOT NULL,
    tracking_granularity text NOT NULL,
    product_variant_ref text,
    inventory_serial_ref text,
    source_origin_type text,
    acquisition_date date,
    capitalization_date date,
    placed_in_service_date date,
    parent_asset_id uuid,
    quantity numeric(20,6) NOT NULL DEFAULT 1,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    updated_by uuid,
    updated_at timestamptz,
    row_version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,asset_number), UNIQUE (tenant_id,asset_tag),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    FOREIGN KEY (tenant_id,category_id) REFERENCES business_assets.asset_category(tenant_id,id),
    FOREIGN KEY (tenant_id,parent_asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    CHECK (lifecycle_state IN ('Draft','Operational','Capitalized','Disposed','Archived')),
    CHECK (tracking_granularity IN ('Individual','Grouped')),
    CHECK (quantity > 0), CHECK (row_version > 0),
    CHECK (capitalization_date IS NULL OR acquisition_date IS NULL OR capitalization_date >= acquisition_date),
    CHECK (placed_in_service_date IS NULL OR capitalization_date IS NULL OR placed_in_service_date >= capitalization_date)
);

CREATE TABLE business_assets.asset_location (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    parent_location_id uuid,
    code text NOT NULL,
    name text NOT NULL,
    location_type text NOT NULL DEFAULT 'Site',
    active boolean NOT NULL DEFAULT true,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,code),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    FOREIGN KEY (tenant_id,parent_location_id) REFERENCES business_assets.asset_location(tenant_id,id),
    CHECK (location_type IN ('Site','Building','Floor','Room','External','Other'))
);

CREATE TABLE business_assets.asset_assignment (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_id uuid NOT NULL,
    assignment_type text NOT NULL,
    employee_id uuid,
    department_id uuid,
    custodian_name text,
    location_id uuid,
    effective_from date NOT NULL,
    effective_to date,
    condition text,
    accessories text,
    acknowledgement_reference text,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    FOREIGN KEY (tenant_id,location_id) REFERENCES business_assets.asset_location(tenant_id,id),
    CHECK (assignment_type IN ('Employee','Department','Pool','LocationOnly','External','Unassigned')),
    CHECK (effective_to IS NULL OR effective_to >= effective_from),
    CHECK (employee_id IS NOT NULL OR department_id IS NOT NULL OR custodian_name IS NOT NULL OR location_id IS NOT NULL OR assignment_type = 'Unassigned'),
    EXCLUDE USING gist (tenant_id WITH =, asset_id WITH =, daterange(effective_from, coalesce(effective_to,'infinity'::date), '[]') WITH &&)
);

CREATE TABLE business_assets.asset_handover (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_id uuid NOT NULL,
    assignment_id uuid NOT NULL,
    handover_type text NOT NULL,
    acknowledged_at timestamptz,
    condition text,
    evidence jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    FOREIGN KEY (tenant_id,assignment_id) REFERENCES business_assets.asset_assignment(tenant_id,id),
    CHECK (handover_type IN ('Issue','Return','Transfer','Acknowledgement')),
    CHECK (jsonb_typeof(evidence) = 'object')
);

CREATE TABLE business_assets.asset_book_definition (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    posts_to_ledger boolean NOT NULL DEFAULT true,
    currency_code char(3) NOT NULL,
    default_depreciation_method text NOT NULL DEFAULT 'StraightLine',
    default_convention text NOT NULL DEFAULT 'FullMonth',
    default_useful_life_months integer,
    active boolean NOT NULL DEFAULT true,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,code),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (default_depreciation_method IN ('StraightLine','NoDepreciation','DecliningBalance')),
    CHECK (default_convention IN ('FullMonth','HalfMonth','Daily')), CHECK (currency_code ~ '^[A-Z]{3}$'),
    CHECK (default_useful_life_months IS NULL OR default_useful_life_months > 0)
);

CREATE TABLE business_assets.asset_book (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_id uuid NOT NULL,
    book_definition_id uuid NOT NULL,
    original_cost numeric(20,6) NOT NULL DEFAULT 0,
    residual_value numeric(20,6) NOT NULL DEFAULT 0,
    useful_life_months integer,
    depreciation_method text NOT NULL,
    placed_in_service_date date,
    created_at timestamptz NOT NULL,
    row_version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,asset_id,book_definition_id),
    FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    FOREIGN KEY (tenant_id,book_definition_id) REFERENCES business_assets.asset_book_definition(tenant_id,id),
    CHECK (original_cost >= 0), CHECK (residual_value >= 0 AND residual_value <= original_cost),
    CHECK (depreciation_method IN ('StraightLine','NoDepreciation','DecliningBalance')), CHECK (useful_life_months IS NULL OR useful_life_months > 0)
);

CREATE TABLE business_assets.asset_book_transaction (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_book_id uuid NOT NULL,
    transaction_type text NOT NULL,
    transaction_date date NOT NULL,
    amount numeric(20,6) NOT NULL,
    accumulated_depreciation numeric(20,6) NOT NULL DEFAULT 0,
    source_reference text,
    idempotency_key text NOT NULL,
    ledger_fact_reference text,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,asset_book_id,idempotency_key),
    FOREIGN KEY (tenant_id,asset_book_id) REFERENCES business_assets.asset_book(tenant_id,id),
    CHECK (transaction_type IN ('Opening','Acquisition','CapitalAddition','CostReduction','Depreciation','Impairment','Revaluation','Disposal','Reversal')),
    CHECK (amount >= 0), CHECK (accumulated_depreciation >= 0)
);

CREATE TABLE business_assets.asset_depreciation_run (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    book_definition_id uuid NOT NULL,
    period_start date NOT NULL,
    period_end date NOT NULL,
    status text NOT NULL DEFAULT 'Prepared',
    idempotency_key text NOT NULL,
    total_amount numeric(20,6) NOT NULL DEFAULT 0,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    posted_at timestamptz,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,book_definition_id,period_start,period_end), UNIQUE (tenant_id,idempotency_key),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    FOREIGN KEY (tenant_id,book_definition_id) REFERENCES business_assets.asset_book_definition(tenant_id,id),
    CHECK (period_end >= period_start), CHECK (status IN ('Prepared','Calculated','Exceptions','Posted','Reversed')), CHECK (total_amount >= 0)
);

CREATE TABLE business_assets.asset_depreciation_run_line (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    run_id uuid NOT NULL,
    asset_book_id uuid NOT NULL,
    amount numeric(20,6) NOT NULL,
    calculation_basis jsonb NOT NULL,
    ledger_fact_reference text,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,run_id,asset_book_id),
    FOREIGN KEY (tenant_id,run_id) REFERENCES business_assets.asset_depreciation_run(tenant_id,id),
    FOREIGN KEY (tenant_id,asset_book_id) REFERENCES business_assets.asset_book(tenant_id,id),
    CHECK (amount >= 0), CHECK (jsonb_typeof(calculation_basis) = 'object')
);

CREATE TABLE business_assets.asset_capitalization_candidate (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    source_type text NOT NULL,
    source_reference text NOT NULL,
    source_snapshot jsonb NOT NULL,
    eligible_amount numeric(20,6) NOT NULL,
    allocated_amount numeric(20,6) NOT NULL DEFAULT 0,
    status text NOT NULL DEFAULT 'PendingReview',
    decision text,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,source_type,source_reference),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (eligible_amount >= 0 AND allocated_amount >= 0 AND allocated_amount <= eligible_amount),
    CHECK (status IN ('PendingReview','Approved','Rejected','Capitalized','Consumed')),
    CHECK (jsonb_typeof(source_snapshot) = 'object')
);

CREATE TABLE business_assets.asset_source_allocation (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    candidate_id uuid NOT NULL,
    asset_id uuid NOT NULL,
    amount numeric(20,6) NOT NULL,
    allocation_type text NOT NULL DEFAULT 'Acquisition',
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,candidate_id,asset_id),
    FOREIGN KEY (tenant_id,candidate_id) REFERENCES business_assets.asset_capitalization_candidate(tenant_id,id),
    FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    CHECK (amount > 0), CHECK (allocation_type IN ('Acquisition','CIPAddition','CapitalAddition','CostReduction'))
);

CREATE TABLE business_assets.asset_cip_cost_source (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_id uuid NOT NULL,
    source_type text NOT NULL,
    source_reference text NOT NULL,
    amount numeric(20,6) NOT NULL,
    reversal_of_id uuid,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,asset_id,source_type,source_reference),
    FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    FOREIGN KEY (tenant_id,reversal_of_id) REFERENCES business_assets.asset_cip_cost_source(tenant_id,id),
    CHECK (amount <> 0)
);

CREATE TABLE business_assets.asset_verification_campaign (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    name text NOT NULL,
    snapshot_at timestamptz NOT NULL,
    status text NOT NULL DEFAULT 'Open',
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id), CHECK (status IN ('Open','Review','Completed','Cancelled'))
);

CREATE TABLE business_assets.asset_verification_line (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    campaign_id uuid NOT NULL,
    asset_id uuid NOT NULL,
    expected_location_id uuid,
    observed_location_id uuid,
    result text NOT NULL DEFAULT 'Pending',
    notes text,
    resolved_by uuid,
    resolved_at timestamptz,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,campaign_id,asset_id),
    FOREIGN KEY (tenant_id,campaign_id) REFERENCES business_assets.asset_verification_campaign(tenant_id,id),
    FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    CHECK (result IN ('Pending','Found','Missing','WrongLocation','WrongCustodian','Damaged','Unexpected'))
);

CREATE TABLE business_assets.asset_disposal (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    disposal_type text NOT NULL,
    disposal_date date NOT NULL,
    proceeds numeric(20,6) NOT NULL DEFAULT 0,
    direct_costs numeric(20,6) NOT NULL DEFAULT 0,
    reason text NOT NULL,
    status text NOT NULL DEFAULT 'Posted',
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,asset_id),
    FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id), FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (disposal_type IN ('Sale','Scrap','Lost','Stolen','Destroyed','Donation')), CHECK (proceeds >= 0 AND direct_costs >= 0), CHECK (status IN ('Draft','Approved','Posted','Reversed'))
);

CREATE TABLE business_assets.asset_opening_import (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    source_system text NOT NULL,
    batch_reference text NOT NULL,
    opening_date date NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,source_system,batch_reference), FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id), CHECK (status IN ('Draft','Validated','Posted','Rejected'))
);

CREATE TABLE business_assets.asset_opening_import_line (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    import_id uuid NOT NULL,
    legacy_asset_number text NOT NULL,
    asset_id uuid,
    original_cost numeric(20,6) NOT NULL,
    accumulated_depreciation numeric(20,6) NOT NULL DEFAULT 0,
    net_book_value numeric(20,6) NOT NULL,
    outcome text NOT NULL DEFAULT 'Pending',
    error_code text,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,import_id,legacy_asset_number), FOREIGN KEY (tenant_id,import_id) REFERENCES business_assets.asset_opening_import(tenant_id,id), FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id), CHECK (original_cost >= 0 AND accumulated_depreciation >= 0 AND net_book_value >= 0), CHECK (outcome IN ('Pending','Valid','Created','Rejected'))
);

CREATE TABLE business_assets.asset_audit_event (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    entity_type text NOT NULL,
    entity_id uuid NOT NULL,
    action text NOT NULL,
    before_state jsonb,
    after_state jsonb,
    actor_id uuid NOT NULL,
    occurred_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id)
);

CREATE TABLE business_assets.asset_idempotency_key (
    tenant_id uuid NOT NULL,
    operation text NOT NULL,
    idempotency_key text NOT NULL,
    request_hash char(64) NOT NULL,
    response jsonb,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,operation,idempotency_key), CHECK (request_hash ~ '^[0-9a-f]{64}$')
);

DO $$ DECLARE item text; BEGIN
    FOREACH item IN ARRAY ARRAY['asset_category','asset_category_version','fixed_asset','asset_location','asset_assignment','asset_handover','asset_book_definition','asset_book','asset_book_transaction','asset_depreciation_run','asset_depreciation_run_line','asset_capitalization_candidate','asset_source_allocation','asset_cip_cost_source','asset_verification_campaign','asset_verification_line','asset_disposal','asset_opening_import','asset_opening_import_line','asset_audit_event','asset_idempotency_key'] LOOP
        EXECUTE format('ALTER TABLE business_assets.%I ENABLE ROW LEVEL SECURITY', item);
        EXECUTE format('ALTER TABLE business_assets.%I FORCE ROW LEVEL SECURITY', item);
        EXECUTE format('CREATE POLICY tenant_isolation ON business_assets.%I USING (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', item);
    END LOOP;
END $$;

CREATE INDEX ix_assets_register ON business_assets.fixed_asset(tenant_id,legal_entity_id,lifecycle_state,asset_number,id);
CREATE INDEX ix_assets_search ON business_assets.fixed_asset(tenant_id,asset_tag) WHERE asset_tag IS NOT NULL;
CREATE INDEX ix_assets_assignment_current ON business_assets.asset_assignment(tenant_id,asset_id,effective_from DESC,effective_to);
CREATE INDEX ix_assets_books_scope ON business_assets.asset_book_definition(tenant_id,legal_entity_id,code);
CREATE INDEX ix_assets_book_transactions ON business_assets.asset_book_transaction(tenant_id,asset_book_id,transaction_date,id);
CREATE INDEX ix_assets_runs_scope ON business_assets.asset_depreciation_run(tenant_id,legal_entity_id,book_definition_id,period_end DESC,id);
CREATE INDEX ix_assets_candidates_queue ON business_assets.asset_capitalization_candidate(tenant_id,legal_entity_id,status,created_at DESC,id);
CREATE INDEX ix_assets_verification_queue ON business_assets.asset_verification_line(tenant_id,campaign_id,result);
CREATE INDEX ix_assets_opening_outcomes ON business_assets.asset_opening_import_line(tenant_id,import_id,outcome);

GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA business_assets TO erp_app;
