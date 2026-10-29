-- Purchasing continuation: persisted supplier profile, receipt replay, matching,
-- returns and read-model coverage. This is intentionally a forward fix so the
-- original purchasing baseline remains checksum-stable for existing databases.

ALTER TABLE business_purchasing.supplier_purchase_profile
    ADD COLUMN IF NOT EXISTS default_buyer_id uuid,
    ADD COLUMN IF NOT EXISTS default_warehouse_id uuid,
    ADD COLUMN IF NOT EXISTS lead_time_days integer,
    ADD COLUMN IF NOT EXISTS supplier_reference_required boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS purchase_price_context jsonb NOT NULL DEFAULT '{}'::jsonb;

ALTER TABLE business_purchasing.purchase_settings
    ADD COLUMN IF NOT EXISTS quantity_tolerance numeric(20,6) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS price_absolute_tolerance numeric(20,6) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS price_percentage_tolerance numeric(12,6) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS tax_tolerance numeric(20,6) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS invoice_eligibility_basis text NOT NULL DEFAULT 'PO';

ALTER TABLE business_purchasing.purchase_order_receipt_link
    ADD COLUMN IF NOT EXISTS idempotency_key text NOT NULL DEFAULT '';
DROP INDEX IF EXISTS business_purchasing.purchase_order_receipt_link_tenant_id_legal_entity_id_purchase_order_id_idempotency_key_key;
CREATE UNIQUE INDEX IF NOT EXISTS ux_purchase_order_receipt_replay
    ON business_purchasing.purchase_order_receipt_link(tenant_id, legal_entity_id, purchase_order_id, idempotency_key, receipt_line_id);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'supplier_purchase_profile_lead_time_days_check') THEN
        ALTER TABLE business_purchasing.supplier_purchase_profile ADD CONSTRAINT supplier_purchase_profile_lead_time_days_check CHECK (lead_time_days IS NULL OR lead_time_days >= 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'supplier_purchase_profile_purchase_price_context_check') THEN
        ALTER TABLE business_purchasing.supplier_purchase_profile ADD CONSTRAINT supplier_purchase_profile_purchase_price_context_check CHECK (jsonb_typeof(purchase_price_context) = 'object');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'purchase_settings_tolerance_check') THEN
        ALTER TABLE business_purchasing.purchase_settings ADD CONSTRAINT purchase_settings_tolerance_check CHECK (quantity_tolerance >= 0 AND price_absolute_tolerance >= 0 AND price_percentage_tolerance >= 0 AND tax_tolerance >= 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'purchase_settings_invoice_basis_check') THEN
        ALTER TABLE business_purchasing.purchase_settings ADD CONSTRAINT purchase_settings_invoice_basis_check CHECK (invoice_eligibility_basis IN ('PO','Receipt','Manual'));
    END IF;
END $$;

ALTER TABLE business_purchasing.purchase_order_match_result
    DROP CONSTRAINT IF EXISTS purchase_order_match_result_basis_state_variance_amount_check,
    DROP CONSTRAINT IF EXISTS purchase_order_match_result_state_check;

DO $$
DECLARE item record;
BEGIN
    FOR item IN
        SELECT conname FROM pg_constraint
        WHERE conrelid = 'business_purchasing.purchase_order_match_result'::regclass
          AND pg_get_constraintdef(oid) ILIKE '%state%matched%'
    LOOP
        EXECUTE format('ALTER TABLE business_purchasing.purchase_order_match_result DROP CONSTRAINT %I', item.conname);
    END LOOP;
END $$;
ALTER TABLE business_purchasing.purchase_order_match_result
    ADD CONSTRAINT purchase_order_match_result_state_check CHECK (state IN ('Matched','WithinTolerance','Variance','Blocked','ApprovedException'));

CREATE TABLE IF NOT EXISTS business_purchasing.supplier_return_request (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    supplier_party_id uuid NOT NULL,
    state text NOT NULL DEFAULT 'Draft',
    resolution text NOT NULL,
    reason text NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    created_by uuid NOT NULL,
    approved_by uuid,
    executed_at timestamptz,
    credit_note_id uuid,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_id) REFERENCES business_purchasing.purchase_order(tenant_id, legal_entity_id, id),
    CHECK (state IN ('Draft','Approved','Executed','Cancelled')),
    CHECK (resolution IN ('PhysicalReturn','SupplierCredit','ServiceCredit')),
    CHECK (length(btrim(reason)) BETWEEN 1 AND 1000)
);

CREATE TABLE IF NOT EXISTS business_purchasing.supplier_return_request_line (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    return_request_id uuid NOT NULL,
    id uuid NOT NULL,
    purchase_order_line_id uuid NOT NULL,
    quantity numeric(20,6) NOT NULL,
    product_variant_id uuid,
    unit_of_measure_id uuid,
    warehouse_id uuid,
    location_id uuid,
    lot_id uuid,
    serial_id uuid,
    PRIMARY KEY (tenant_id, return_request_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, return_request_id) REFERENCES business_purchasing.supplier_return_request(tenant_id, legal_entity_id, id),
    CHECK (quantity > 0)
);

CREATE TABLE IF NOT EXISTS business_purchasing.purchase_return_link (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    return_request_id uuid NOT NULL,
    inventory_operation_id uuid,
    invoice_id uuid,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, return_request_id),
    FOREIGN KEY (tenant_id, legal_entity_id, return_request_id) REFERENCES business_purchasing.supplier_return_request(tenant_id, legal_entity_id, id)
);

CREATE TABLE IF NOT EXISTS business_purchasing.purchase_order_list_projection (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    supplier_display_name text NOT NULL DEFAULT '',
    open_quantity numeric(20,6) NOT NULL DEFAULT 0,
    expected_receipt_date date,
    open_exception_count integer NOT NULL DEFAULT 0,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, purchase_order_id),
    FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_id) REFERENCES business_purchasing.purchase_order(tenant_id, legal_entity_id, id),
    CHECK (open_quantity >= 0 AND open_exception_count >= 0)
);

DO $$ DECLARE t text; BEGIN
    FOREACH t IN ARRAY ARRAY['supplier_return_request','supplier_return_request_line','purchase_return_link','purchase_order_list_projection'] LOOP
        EXECUTE format('ALTER TABLE business_purchasing.%I ENABLE ROW LEVEL SECURITY', t);
        EXECUTE format('ALTER TABLE business_purchasing.%I FORCE ROW LEVEL SECURITY', t);
        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON business_purchasing.%I', t);
        EXECUTE format('CREATE POLICY tenant_isolation ON business_purchasing.%I USING (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    END LOOP;
END $$;

CREATE INDEX IF NOT EXISTS ix_purchase_order_projection_open ON business_purchasing.purchase_order_list_projection(tenant_id, legal_entity_id, open_quantity DESC, purchase_order_id);
CREATE INDEX IF NOT EXISTS ix_purchase_return_supplier ON business_purchasing.supplier_return_request(tenant_id, legal_entity_id, supplier_party_id, created_at DESC);
GRANT SELECT, INSERT, UPDATE ON business_purchasing.supplier_return_request, business_purchasing.purchase_order_list_projection TO erp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON business_purchasing.supplier_return_request_line TO erp_app;
GRANT SELECT, INSERT ON business_purchasing.purchase_return_link TO erp_app;
