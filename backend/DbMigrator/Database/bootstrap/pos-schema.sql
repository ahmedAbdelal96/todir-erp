CREATE SCHEMA IF NOT EXISTS business_pos AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA business_pos FROM PUBLIC;
GRANT USAGE ON SCHEMA business_pos TO erp_app;

CREATE TABLE IF NOT EXISTS business_pos.pos_register (
    tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, branch_id uuid NOT NULL,
    code text NOT NULL, name text NOT NULL, currency_code text NOT NULL, status text NOT NULL DEFAULT 'Active',
    warehouse_id uuid NULL, selling_location_id uuid NULL, offline_enabled boolean NOT NULL DEFAULT false,
    row_version bigint NOT NULL DEFAULT 1, created_by uuid NOT NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id), UNIQUE (tenant_id, legal_entity_id, code), CHECK (status IN ('Active','Disabled','Retiring'))
);
CREATE TABLE IF NOT EXISTS business_pos.pos_terminal (
    tenant_id uuid NOT NULL, id uuid NOT NULL, register_id uuid NOT NULL, device_identity text NOT NULL,
    display_name text NOT NULL, status text NOT NULL DEFAULT 'Pending', credential_version integer NOT NULL DEFAULT 1,
    revoked_at timestamptz NULL, last_seen_at timestamptz NULL, row_version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id), UNIQUE (tenant_id, device_identity), FOREIGN KEY (tenant_id, register_id) REFERENCES business_pos.pos_register(tenant_id, id), CHECK (status IN ('Pending','Ready','Revoked','Disabled'))
);
CREATE TABLE IF NOT EXISTS business_pos.pos_session (
    tenant_id uuid NOT NULL, id uuid NOT NULL, register_id uuid NOT NULL, terminal_id uuid NULL, cashier_user_id uuid NOT NULL,
    business_date date NOT NULL, opened_at timestamptz NOT NULL, closed_at timestamptz NULL, status text NOT NULL DEFAULT 'Open',
    opening_float numeric(19,4) NOT NULL DEFAULT 0, expected_cash numeric(19,4) NOT NULL DEFAULT 0, counted_cash numeric(19,4) NULL,
    variance numeric(19,4) NULL, variance_reason text NULL, approved_by uuid NULL, row_version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id, id), FOREIGN KEY (tenant_id, register_id) REFERENCES business_pos.pos_register(tenant_id, id), FOREIGN KEY (tenant_id, terminal_id) REFERENCES business_pos.pos_terminal(tenant_id, id), CHECK (status IN ('Open','Reconciling','Closed','Reopened')), CHECK (opening_float >= 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_pos_open_session ON business_pos.pos_session(tenant_id, register_id) WHERE status IN ('Open','Reconciling');
CREATE TABLE IF NOT EXISTS business_pos.pos_cash_movement (
    tenant_id uuid NOT NULL, id uuid NOT NULL, session_id uuid NOT NULL, movement_type text NOT NULL, amount numeric(19,4) NOT NULL,
    currency_code text NOT NULL, reason text NOT NULL, source_ref uuid NULL, created_by uuid NOT NULL, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id), FOREIGN KEY (tenant_id, session_id) REFERENCES business_pos.pos_session(tenant_id, id), CHECK (amount > 0), CHECK (movement_type IN ('OpeningFloat','Sale','Refund','CashIn','CashOut','Drop','Adjustment'))
);
CREATE TABLE IF NOT EXISTS business_pos.pos_cart (
    tenant_id uuid NOT NULL, id uuid NOT NULL, session_id uuid NOT NULL, customer_party_id uuid NULL, state text NOT NULL DEFAULT 'Active',
    version integer NOT NULL DEFAULT 1, held_until timestamptz NULL, currency_code text NOT NULL, subtotal numeric(19,4) NOT NULL DEFAULT 0,
    tax_total numeric(19,4) NOT NULL DEFAULT 0, discount_total numeric(19,4) NOT NULL DEFAULT 0, grand_total numeric(19,4) NOT NULL DEFAULT 0,
    created_by uuid NOT NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, session_id) REFERENCES business_pos.pos_session(tenant_id, id), CHECK (state IN ('Active','Held','CheckedOut','Cancelled'))
);
CREATE TABLE IF NOT EXISTS business_pos.pos_cart_line (
    tenant_id uuid NOT NULL, id uuid NOT NULL, cart_id uuid NOT NULL, line_no integer NOT NULL, variant_id uuid NOT NULL,
    sku text NULL, description text NOT NULL, uom_code text NOT NULL, quantity numeric(19,6) NOT NULL, unit_price numeric(19,4) NOT NULL,
    tax_amount numeric(19,4) NOT NULL DEFAULT 0, discount_amount numeric(19,4) NOT NULL DEFAULT 0, line_total numeric(19,4) NOT NULL,
    lot_id uuid NULL, serial_ids uuid[] NULL, price_snapshot jsonb NOT NULL DEFAULT '{}'::jsonb, tax_snapshot jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id, id), UNIQUE (tenant_id, cart_id, line_no), FOREIGN KEY (tenant_id, cart_id) REFERENCES business_pos.pos_cart(tenant_id, id), CHECK (quantity > 0), CHECK (unit_price >= 0), CHECK (line_total >= 0)
);
CREATE TABLE IF NOT EXISTS business_pos.pos_checkout (
    tenant_id uuid NOT NULL, id uuid NOT NULL, cart_id uuid NOT NULL, idempotency_key text NOT NULL, payload_hash text NOT NULL,
    state text NOT NULL DEFAULT 'PendingValidation', commercial_state text NOT NULL DEFAULT 'Pending', payment_state text NOT NULL DEFAULT 'NotStarted',
    inventory_state text NOT NULL DEFAULT 'NotRequired', document_state text NOT NULL DEFAULT 'NotStarted', regulatory_state text NOT NULL DEFAULT 'NotRequired',
    accounting_state text NOT NULL DEFAULT 'NotStarted', total_amount numeric(19,4) NOT NULL, currency_code text NOT NULL,
    recovery_reason text NULL, provider_reference text NULL, completed_at timestamptz NULL, row_version bigint NOT NULL DEFAULT 1,
    created_by uuid NOT NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, idempotency_key), FOREIGN KEY (tenant_id, cart_id) REFERENCES business_pos.pos_cart(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS business_pos.pos_tender (
    tenant_id uuid NOT NULL, id uuid NOT NULL, checkout_id uuid NOT NULL, sequence_no integer NOT NULL, method text NOT NULL,
    requested_amount numeric(19,4) NOT NULL, captured_amount numeric(19,4) NOT NULL DEFAULT 0, change_amount numeric(19,4) NOT NULL DEFAULT 0,
    state text NOT NULL DEFAULT 'Pending', external_reference text NULL, provider_state text NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id), UNIQUE (tenant_id, checkout_id, sequence_no), FOREIGN KEY (tenant_id, checkout_id) REFERENCES business_pos.pos_checkout(tenant_id, id), CHECK (requested_amount > 0), CHECK (method IN ('Cash','Card','Credit','Other'))
);
CREATE TABLE IF NOT EXISTS business_pos.pos_sale (
    tenant_id uuid NOT NULL, id uuid NOT NULL, checkout_id uuid NOT NULL, register_id uuid NOT NULL, session_id uuid NOT NULL,
    business_date date NOT NULL, receipt_number text NOT NULL, customer_party_id uuid NULL, currency_code text NOT NULL,
    subtotal numeric(19,4) NOT NULL, tax_total numeric(19,4) NOT NULL, discount_total numeric(19,4) NOT NULL, grand_total numeric(19,4) NOT NULL,
    commercial_state text NOT NULL DEFAULT 'Completed', payment_state text NOT NULL, inventory_state text NOT NULL, document_state text NOT NULL,
    accounting_state text NOT NULL, created_by uuid NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, checkout_id), UNIQUE (tenant_id, receipt_number), FOREIGN KEY (tenant_id, checkout_id) REFERENCES business_pos.pos_checkout(tenant_id, id), FOREIGN KEY (tenant_id, register_id) REFERENCES business_pos.pos_register(tenant_id, id), FOREIGN KEY (tenant_id, session_id) REFERENCES business_pos.pos_session(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS business_pos.pos_sale_line (
    tenant_id uuid NOT NULL, id uuid NOT NULL, sale_id uuid NOT NULL, line_no integer NOT NULL, variant_id uuid NOT NULL, description text NOT NULL,
    uom_code text NOT NULL, quantity numeric(19,6) NOT NULL, unit_price numeric(19,4) NOT NULL, tax_amount numeric(19,4) NOT NULL, discount_amount numeric(19,4) NOT NULL, line_total numeric(19,4) NOT NULL,
    inventory_source_ref uuid NULL, document_source_ref uuid NULL, ledger_source_ref uuid NULL, price_snapshot jsonb NOT NULL, tax_snapshot jsonb NOT NULL,
    PRIMARY KEY (tenant_id, id), UNIQUE (tenant_id, sale_id, line_no), FOREIGN KEY (tenant_id, sale_id) REFERENCES business_pos.pos_sale(tenant_id, id), CHECK (quantity > 0)
);
CREATE TABLE IF NOT EXISTS business_pos.pos_return (
    tenant_id uuid NOT NULL, id uuid NOT NULL, original_sale_id uuid NOT NULL, session_id uuid NOT NULL, state text NOT NULL DEFAULT 'Pending',
    refund_state text NOT NULL DEFAULT 'NotRequested', disposition text NOT NULL, reason text NOT NULL, total_amount numeric(19,4) NOT NULL,
    created_by uuid NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id, id), FOREIGN KEY (tenant_id, original_sale_id) REFERENCES business_pos.pos_sale(tenant_id, id), FOREIGN KEY (tenant_id, session_id) REFERENCES business_pos.pos_session(tenant_id, id), CHECK (state IN ('Pending','Completed','Rejected','Cancelled'))
);
CREATE TABLE IF NOT EXISTS business_pos.pos_return_line (
    tenant_id uuid NOT NULL, id uuid NOT NULL, return_id uuid NOT NULL, original_sale_line_id uuid NOT NULL, quantity numeric(19,6) NOT NULL, amount numeric(19,4) NOT NULL,
    disposition text NOT NULL, serial_ids uuid[] NULL, PRIMARY KEY (tenant_id, id), FOREIGN KEY (tenant_id, return_id) REFERENCES business_pos.pos_return(tenant_id, id), CHECK (quantity > 0)
);
CREATE TABLE IF NOT EXISTS business_pos.pos_manager_override (
    tenant_id uuid NOT NULL, id uuid NOT NULL, scope text NOT NULL, target_id uuid NOT NULL, reason text NOT NULL, approved_by uuid NOT NULL, expires_at timestamptz NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id, id)
);
CREATE TABLE IF NOT EXISTS business_pos.pos_offline_package (
    tenant_id uuid NOT NULL, id uuid NOT NULL, register_id uuid NOT NULL, version bigint NOT NULL, valid_from timestamptz NOT NULL, expires_at timestamptz NOT NULL,
    revoked_at timestamptz NULL, payload jsonb NOT NULL, signature text NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id, id), UNIQUE (tenant_id, register_id, version), FOREIGN KEY (tenant_id, register_id) REFERENCES business_pos.pos_register(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS business_pos.pos_offline_envelope (
    tenant_id uuid NOT NULL, id uuid NOT NULL, register_id uuid NOT NULL, device_identity text NOT NULL, source_sequence bigint NOT NULL,
    idempotency_key text NOT NULL, package_version bigint NOT NULL, payload jsonb NOT NULL, signature text NOT NULL, state text NOT NULL DEFAULT 'Received', conflict_code text NULL, received_at timestamptz NOT NULL, processed_at timestamptz NULL,
    PRIMARY KEY (tenant_id, id), UNIQUE (tenant_id, idempotency_key), UNIQUE (tenant_id, register_id, device_identity, source_sequence), FOREIGN KEY (tenant_id, register_id) REFERENCES business_pos.pos_register(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS business_pos.pos_sync_conflict (
    tenant_id uuid NOT NULL, id uuid NOT NULL, envelope_id uuid NOT NULL, code text NOT NULL, details jsonb NOT NULL, state text NOT NULL DEFAULT 'Open', resolved_by uuid NULL, resolved_at timestamptz NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id, id), FOREIGN KEY (tenant_id, envelope_id) REFERENCES business_pos.pos_offline_envelope(tenant_id, id)
);

DO $$ DECLARE item text; BEGIN
  FOREACH item IN ARRAY ARRAY['pos_register','pos_terminal','pos_session','pos_cash_movement','pos_cart','pos_cart_line','pos_checkout','pos_tender','pos_sale','pos_sale_line','pos_return','pos_return_line','pos_manager_override','pos_offline_package','pos_offline_envelope','pos_sync_conflict'] LOOP
    EXECUTE format('ALTER TABLE business_pos.%I ENABLE ROW LEVEL SECURITY', item);
    EXECUTE format('ALTER TABLE business_pos.%I FORCE ROW LEVEL SECURITY', item);
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON business_pos.%I', item);
    EXECUTE format('CREATE POLICY tenant_isolation ON business_pos.%I USING (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', item);
  END LOOP;
END $$;

CREATE INDEX IF NOT EXISTS ix_pos_register_scope ON business_pos.pos_register(tenant_id, legal_entity_id, branch_id, status, code);
CREATE INDEX IF NOT EXISTS ix_pos_terminal_register ON business_pos.pos_terminal(tenant_id, register_id, status);
CREATE INDEX IF NOT EXISTS ix_pos_session_scope ON business_pos.pos_session(tenant_id, register_id, business_date DESC, opened_at DESC);
CREATE INDEX IF NOT EXISTS ix_pos_cart_session ON business_pos.pos_cart(tenant_id, session_id, state, updated_at DESC);
CREATE INDEX IF NOT EXISTS ix_pos_checkout_recovery ON business_pos.pos_checkout(tenant_id, state, updated_at DESC) WHERE state IN ('RecoveryRequired','PaymentUnknown','CompletionPending');
CREATE INDEX IF NOT EXISTS ix_pos_sale_report ON business_pos.pos_sale(tenant_id, business_date DESC, register_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_pos_return_sale ON business_pos.pos_return(tenant_id, original_sale_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_pos_offline_state ON business_pos.pos_offline_envelope(tenant_id, state, received_at);
