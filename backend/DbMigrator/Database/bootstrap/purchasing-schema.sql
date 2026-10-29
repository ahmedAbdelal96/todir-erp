SET search_path = business_purchasing, public;
SET row_security = off;

CREATE SCHEMA IF NOT EXISTS business_purchasing AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA business_purchasing FROM PUBLIC;
GRANT USAGE ON SCHEMA business_purchasing TO erp_app;

CREATE TABLE business_purchasing.supplier_purchase_profile (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    supplier_party_id uuid NOT NULL,
    supplier_code text,
    payment_term_id uuid,
    default_currency_code char(3),
    default_price_list_id uuid,
    status text NOT NULL DEFAULT 'Active',
    blocked_reason text,
    notes text,
    row_version bigint NOT NULL DEFAULT 1,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, legal_entity_id, supplier_party_id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (status IN ('Active','Blocked','Archived')),
    CHECK (default_currency_code IS NULL OR default_currency_code ~ '^[A-Z]{3}$'),
    CHECK (row_version > 0)
);

CREATE TABLE business_purchasing.purchase_settings (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    approval_required boolean NOT NULL DEFAULT false,
    approval_threshold numeric(20,6),
    over_receipt_policy text NOT NULL DEFAULT 'Disallow',
    over_receipt_tolerance numeric(20,6) NOT NULL DEFAULT 0,
    invoice_without_receipt boolean NOT NULL DEFAULT true,
    three_way_matching boolean NOT NULL DEFAULT false,
    buyer_approver_separation boolean NOT NULL DEFAULT false,
    updated_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, legal_entity_id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (over_receipt_policy IN ('Disallow','Tolerance','Approval','Allow')),
    CHECK (over_receipt_tolerance >= 0),
    CHECK (approval_threshold IS NULL OR approval_threshold >= 0)
);

CREATE TABLE business_purchasing.purchase_order (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    branch_id uuid,
    supplier_party_id uuid NOT NULL,
    currency_code char(3) NOT NULL,
    order_date date NOT NULL,
    state text NOT NULL DEFAULT 'Draft',
    approval_state text NOT NULL DEFAULT 'NotRequired',
    receipt_state text NOT NULL DEFAULT 'NotReceived',
    invoicing_state text NOT NULL DEFAULT 'NotInvoiced',
    matching_state text NOT NULL DEFAULT 'NotRequired',
    price_list_id uuid,
    net_total numeric(20,6) NOT NULL DEFAULT 0,
    tax_total numeric(20,6) NOT NULL DEFAULT 0,
    gross_total numeric(20,6) NOT NULL DEFAULT 0,
    created_by uuid NOT NULL,
    approved_by uuid,
    approved_at timestamptz,
    closed_reason text,
    cancelled_at timestamptz,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (currency_code ~ '^[A-Z]{3}$'),
    CHECK (state IN ('Draft','PendingApproval','Approved','Sent','Open','PartiallyCompleted','Completed','Cancelled','Closed')),
    CHECK (approval_state IN ('NotRequired','Pending','Approved','Rejected')),
    CHECK (receipt_state IN ('NotRequired','NotReceived','PartiallyReceived','Received','OverReceivedException','Closed')),
    CHECK (invoicing_state IN ('NotInvoiced','PartiallyInvoiced','Invoiced')),
    CHECK (matching_state IN ('NotRequired','Unmatched','PartiallyMatched','Matched','Variance','Blocked','ApprovedException')),
    CHECK (net_total >= 0 AND tax_total >= 0 AND gross_total >= 0),
    CHECK (row_version > 0),
    CHECK ((approval_state = 'Approved') = (approved_at IS NOT NULL))
);

CREATE TABLE business_purchasing.purchase_order_line (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    id uuid NOT NULL,
    sequence integer NOT NULL,
    product_variant_id uuid,
    unit_of_measure_id uuid,
    unit_code text,
    description text NOT NULL,
    ordered_quantity numeric(20,6) NOT NULL,
    unit_price numeric(20,6) NOT NULL,
    cancelled_quantity numeric(20,6) NOT NULL DEFAULT 0,
    closed_short_quantity numeric(20,6) NOT NULL DEFAULT 0,
    received_quantity numeric(20,6) NOT NULL DEFAULT 0,
    invoiced_quantity numeric(20,6) NOT NULL DEFAULT 0,
    price_evidence jsonb,
    price_override_reason text,
    tax_evidence jsonb,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, purchase_order_id, id),
    UNIQUE (tenant_id, legal_entity_id, purchase_order_id, id),
    UNIQUE (tenant_id, purchase_order_id, sequence),
    FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_id) REFERENCES business_purchasing.purchase_order(tenant_id, legal_entity_id, id),
    CHECK (sequence > 0 AND ordered_quantity > 0 AND unit_price >= 0),
    CHECK (cancelled_quantity >= 0 AND closed_short_quantity >= 0 AND received_quantity >= 0 AND invoiced_quantity >= 0),
    CHECK (cancelled_quantity + closed_short_quantity + received_quantity <= ordered_quantity),
    CHECK (invoiced_quantity <= ordered_quantity),
    CHECK (price_override_reason IS NULL OR length(btrim(price_override_reason)) BETWEEN 1 AND 500),
    CHECK (jsonb_typeof(price_evidence) IS NULL OR jsonb_typeof(price_evidence) = 'object'),
    CHECK (jsonb_typeof(tax_evidence) IS NULL OR jsonb_typeof(tax_evidence) = 'object')
);

CREATE TABLE business_purchasing.purchase_order_state_history (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    id uuid NOT NULL,
    from_state text,
    to_state text NOT NULL,
    reason text,
    actor_id uuid NOT NULL,
    correlation_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, purchase_order_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_id) REFERENCES business_purchasing.purchase_order(tenant_id, legal_entity_id, id),
    CHECK (length(btrim(to_state)) BETWEEN 1 AND 80)
);

CREATE TABLE business_purchasing.purchase_order_approval (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    id uuid NOT NULL,
    decision text NOT NULL,
    actor_id uuid NOT NULL,
    reason text,
    idempotency_key text NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, purchase_order_id, id),
    UNIQUE (tenant_id, purchase_order_id, idempotency_key),
    FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_id) REFERENCES business_purchasing.purchase_order(tenant_id, legal_entity_id, id),
    CHECK (decision IN ('Approved','Rejected')),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200)
);

CREATE TABLE business_purchasing.purchase_order_amendment (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    id uuid NOT NULL,
    amendment_number integer NOT NULL,
    reason text NOT NULL,
    before_snapshot jsonb NOT NULL,
    after_snapshot jsonb NOT NULL,
    actor_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, purchase_order_id, id),
    UNIQUE (tenant_id, purchase_order_id, amendment_number),
    FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_id) REFERENCES business_purchasing.purchase_order(tenant_id, legal_entity_id, id),
    CHECK (amendment_number > 0 AND jsonb_typeof(before_snapshot) = 'object' AND jsonb_typeof(after_snapshot) = 'object')
);

CREATE TABLE business_purchasing.purchase_order_invoice_link (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    purpose text NOT NULL,
    idempotency_key text NOT NULL,
    payload_hash char(64) NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, purchase_order_id, invoice_id),
    UNIQUE (tenant_id, legal_entity_id, purchase_order_id, purpose, idempotency_key),
    FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_id) REFERENCES business_purchasing.purchase_order(tenant_id, legal_entity_id, id),
    CHECK (purpose IN ('SupplierInvoice','ApproveAndBill')),
    CHECK (payload_hash ~ '^[0-9a-f]{64}$')
);

CREATE TABLE business_purchasing.purchase_order_receipt_link (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    receipt_id uuid NOT NULL,
    receipt_line_id uuid,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, purchase_order_id, receipt_id, receipt_line_id),
    FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_id) REFERENCES business_purchasing.purchase_order(tenant_id, legal_entity_id, id)
);

CREATE TABLE business_purchasing.purchase_order_match_result (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    id uuid NOT NULL,
    basis text NOT NULL,
    state text NOT NULL,
    variance_amount numeric(20,6) NOT NULL DEFAULT 0,
    source_version bigint NOT NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, purchase_order_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_id) REFERENCES business_purchasing.purchase_order(tenant_id, legal_entity_id, id),
    CHECK (basis IN ('TwoWay','ThreeWay','Disabled')), CHECK (state IN ('Matched','Variance','Blocked','ApprovedException')), CHECK (variance_amount >= 0), CHECK (jsonb_typeof(details) = 'object')
);

CREATE TABLE business_purchasing.purchase_order_exception (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    severity text NOT NULL,
    status text NOT NULL DEFAULT 'Open',
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL,
    resolved_at timestamptz,
    PRIMARY KEY (tenant_id, purchase_order_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_id) REFERENCES business_purchasing.purchase_order(tenant_id, legal_entity_id, id),
    CHECK (severity IN ('Info','Warning','Blocking')), CHECK (status IN ('Open','Resolved')), CHECK (jsonb_typeof(details) = 'object')
);

CREATE OR REPLACE FUNCTION business_purchasing.guard_purchase_order_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.state NOT IN ('Draft','PendingApproval') AND current_setting('app.purchasing_amendment', true) IS DISTINCT FROM 'true' AND (NEW.supplier_party_id <> OLD.supplier_party_id OR NEW.currency_code <> OLD.currency_code OR NEW.order_date <> OLD.order_date OR NEW.net_total <> OLD.net_total) THEN
        RAISE EXCEPTION 'purchasing.approved_requires_amendment' USING ERRCODE = 'check_violation';
    END IF;
    IF NEW.tenant_id <> OLD.tenant_id OR NEW.legal_entity_id <> OLD.legal_entity_id OR NEW.id <> OLD.id THEN
        RAISE EXCEPTION 'purchasing.identity_immutable' USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER purchase_order_guard BEFORE UPDATE ON business_purchasing.purchase_order FOR EACH ROW EXECUTE FUNCTION business_purchasing.guard_purchase_order_mutation();

DO $$ DECLARE t text; BEGIN
    FOREACH t IN ARRAY ARRAY['supplier_purchase_profile','purchase_settings','purchase_order','purchase_order_line','purchase_order_state_history','purchase_order_approval','purchase_order_amendment','purchase_order_invoice_link','purchase_order_receipt_link','purchase_order_match_result','purchase_order_exception'] LOOP
        EXECUTE format('ALTER TABLE business_purchasing.%I ENABLE ROW LEVEL SECURITY', t);
        EXECUTE format('ALTER TABLE business_purchasing.%I FORCE ROW LEVEL SECURITY', t);
        EXECUTE format('CREATE POLICY tenant_isolation ON business_purchasing.%I USING (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    END LOOP;
END $$;

CREATE INDEX ix_purchase_order_entity_date ON business_purchasing.purchase_order(tenant_id, legal_entity_id, order_date DESC, id DESC);
CREATE INDEX ix_purchase_order_entity_state ON business_purchasing.purchase_order(tenant_id, legal_entity_id, state, updated_at DESC, id DESC);
CREATE INDEX ix_purchase_order_supplier_date ON business_purchasing.purchase_order(tenant_id, legal_entity_id, supplier_party_id, order_date DESC, id DESC);
CREATE INDEX ix_purchase_order_line_parent ON business_purchasing.purchase_order_line(tenant_id, legal_entity_id, purchase_order_id, sequence);
CREATE INDEX ix_purchase_order_exception_open ON business_purchasing.purchase_order_exception(tenant_id, legal_entity_id, status, created_at DESC);

GRANT SELECT, INSERT, UPDATE ON business_purchasing.supplier_purchase_profile, business_purchasing.purchase_settings, business_purchasing.purchase_order TO erp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON business_purchasing.purchase_order_line TO erp_app;
GRANT SELECT, INSERT ON business_purchasing.purchase_order_state_history, business_purchasing.purchase_order_approval, business_purchasing.purchase_order_amendment, business_purchasing.purchase_order_invoice_link, business_purchasing.purchase_order_receipt_link, business_purchasing.purchase_order_match_result, business_purchasing.purchase_order_exception TO erp_app;
