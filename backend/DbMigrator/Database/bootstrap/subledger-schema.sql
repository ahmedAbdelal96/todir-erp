SET search_path = finance_subledger, public;
SET row_security = off;

CREATE SCHEMA IF NOT EXISTS finance_subledger AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA finance_subledger FROM PUBLIC;
GRANT USAGE ON SCHEMA finance_subledger TO erp_app;

CREATE TABLE finance_subledger.party_financial_profile (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    party_id uuid NOT NULL,
    default_receivable_payment_term_id uuid,
    default_payable_payment_term_id uuid,
    credit_limit_amount numeric(20,6),
    credit_limit_currency char(3),
    receivable_policy_code text,
    payable_policy_code text,
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, party_id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (credit_limit_amount IS NULL OR credit_limit_amount >= 0),
    CHECK ((credit_limit_amount IS NULL) = (credit_limit_currency IS NULL)),
    CHECK (credit_limit_currency IS NULL OR credit_limit_currency ~ '^[A-Z]{3}$'),
    CHECK (receivable_policy_code IS NULL OR length(btrim(receivable_policy_code)) BETWEEN 1 AND 80),
    CHECK (payable_policy_code IS NULL OR length(btrim(payable_policy_code)) BETWEEN 1 AND 80),
    CHECK (status IN ('Active','Inactive')),
    CHECK (version > 0)
);

CREATE TABLE finance_subledger.party_open_item (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    party_id uuid NOT NULL,
    direction text NOT NULL,
    polarity text NOT NULL,
    source_type text NOT NULL,
    source_reference text NOT NULL,
    source_schedule_reference text,
    source_display_number text,
    document_date date NOT NULL,
    posting_date date NOT NULL,
    due_date date NOT NULL,
    transaction_currency char(3) NOT NULL,
    original_transaction_amount numeric(20,6) NOT NULL,
    settled_transaction_amount numeric(20,6) NOT NULL DEFAULT 0,
    open_transaction_amount numeric(20,6) NOT NULL,
    base_currency char(3) NOT NULL,
    original_base_amount numeric(20,6) NOT NULL,
    settled_base_carrying_amount numeric(20,6) NOT NULL DEFAULT 0,
    open_base_carrying_amount numeric(20,6) NOT NULL,
    control_account_role text NOT NULL,
    control_account_reference text NOT NULL,
    control_configuration_reference text NOT NULL,
    historic_fx_snapshot jsonb,
    source_version bigint NOT NULL,
    source_payload_hash char(64) NOT NULL,
    status text NOT NULL DEFAULT 'Open',
    is_open boolean NOT NULL DEFAULT true,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    closed_at timestamptz,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (direction IN ('Receivable','Payable')),
    CHECK (polarity IN ('Debit','Credit')),
    CHECK (source_type IN ('Invoice','Bill','Payment','CreditNote','OpeningBalance','Adjustment')),
    CHECK (length(btrim(source_reference)) BETWEEN 1 AND 240),
    CHECK (source_schedule_reference IS NULL OR length(btrim(source_schedule_reference)) BETWEEN 1 AND 240),
    CHECK (source_display_number IS NULL OR length(btrim(source_display_number)) <= 120),
    CHECK (transaction_currency ~ '^[A-Z]{3}$' AND base_currency ~ '^[A-Z]{3}$'),
    CHECK (original_transaction_amount > 0 AND original_base_amount >= 0),
    CHECK (settled_transaction_amount >= 0 AND open_transaction_amount >= 0),
    CHECK (settled_base_carrying_amount >= 0 AND open_base_carrying_amount >= 0),
    CHECK (original_transaction_amount = settled_transaction_amount + open_transaction_amount),
    CHECK (original_base_amount = settled_base_carrying_amount + open_base_carrying_amount),
    CHECK (control_account_role IN ('Receivable','Payable')),
    CHECK (length(btrim(control_account_reference)) BETWEEN 1 AND 240),
    CHECK (length(btrim(control_configuration_reference)) BETWEEN 1 AND 240),
    CHECK (source_version > 0 AND row_version > 0),
    CHECK (status IN ('Open','PartiallySettled','Settled')),
    CHECK (is_open = (status IN ('Open','PartiallySettled'))),
    CHECK ((status = 'Settled') = (open_transaction_amount = 0)),
    CHECK ((status = 'Open') = (settled_transaction_amount = 0)),
    CHECK ((status IN ('Open','PartiallySettled')) = (closed_at IS NULL))
);

CREATE UNIQUE INDEX ux_subledger_open_item_source
    ON finance_subledger.party_open_item (tenant_id, legal_entity_id, source_type, source_reference, coalesce(source_schedule_reference, ''));
CREATE INDEX ix_subledger_open_party_due
    ON finance_subledger.party_open_item (tenant_id, legal_entity_id, party_id, direction, due_date, id)
    WHERE is_open;
CREATE INDEX ix_subledger_open_source
    ON finance_subledger.party_open_item (tenant_id, legal_entity_id, source_type, source_reference);

CREATE TABLE finance_subledger.application_batch (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    party_id uuid NOT NULL,
    direction text NOT NULL,
    operation text NOT NULL,
    idempotency_key text NOT NULL,
    payload_hash char(64) NOT NULL,
    application_date date NOT NULL,
    settlement_date date NOT NULL,
    status text NOT NULL DEFAULT 'Confirmed',
    actor_id uuid NOT NULL,
    correlation_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    confirmed_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, operation, idempotency_key),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (direction IN ('Receivable','Payable')),
    CHECK (operation IN ('Apply','Reverse')),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200),
    CHECK (status = 'Confirmed')
);

CREATE TABLE finance_subledger.open_item_application (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    batch_id uuid NOT NULL,
    debit_item_id uuid NOT NULL,
    credit_item_id uuid NOT NULL,
    debit_applied_amount numeric(20,6) NOT NULL,
    credit_applied_amount numeric(20,6) NOT NULL,
    base_currency char(3) NOT NULL,
    debit_base_carrying_amount_closed numeric(20,6) NOT NULL,
    credit_base_carrying_amount_closed numeric(20,6) NOT NULL,
    application_date date NOT NULL,
    settlement_date date NOT NULL,
    sequence_no bigint NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, batch_id) REFERENCES finance_subledger.application_batch(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, debit_item_id) REFERENCES finance_subledger.party_open_item(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, credit_item_id) REFERENCES finance_subledger.party_open_item(tenant_id, legal_entity_id, id),
    CHECK (debit_item_id <> credit_item_id),
    CHECK (debit_applied_amount > 0 AND credit_applied_amount > 0),
    CHECK (debit_applied_amount = credit_applied_amount),
    CHECK (base_currency ~ '^[A-Z]{3}$'),
    CHECK (debit_base_carrying_amount_closed >= 0 AND credit_base_carrying_amount_closed >= 0),
    CHECK (sequence_no > 0)
);
CREATE INDEX ix_subledger_application_debit ON finance_subledger.open_item_application (tenant_id, legal_entity_id, debit_item_id, application_date, id);
CREATE INDEX ix_subledger_application_credit ON finance_subledger.open_item_application (tenant_id, legal_entity_id, credit_item_id, application_date, id);
CREATE INDEX ix_subledger_application_batch ON finance_subledger.open_item_application (tenant_id, legal_entity_id, batch_id, sequence_no);

CREATE TABLE finance_subledger.application_reversal (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    original_application_id uuid NOT NULL,
    reversal_batch_id uuid NOT NULL,
    effective_date date NOT NULL,
    reason text NOT NULL,
    actor_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, original_application_id),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, original_application_id) REFERENCES finance_subledger.open_item_application(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, reversal_batch_id) REFERENCES finance_subledger.application_batch(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (length(btrim(reason)) BETWEEN 1 AND 1000)
);

CREATE FUNCTION finance_subledger.reject_immutable_change() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'subledger.immutable_history' USING ERRCODE = '55000';
END
$$;
CREATE FUNCTION finance_subledger.guard_open_item_update() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'subledger.open_item_immutable' USING ERRCODE = '55000';
    END IF;
    IF ROW(NEW.tenant_id, NEW.legal_entity_id, NEW.id, NEW.party_id, NEW.direction, NEW.polarity,
           NEW.source_type, NEW.source_reference, NEW.source_schedule_reference, NEW.source_display_number,
           NEW.document_date, NEW.posting_date, NEW.due_date, NEW.transaction_currency,
           NEW.original_transaction_amount, NEW.base_currency, NEW.original_base_amount,
           NEW.control_account_role, NEW.control_account_reference, NEW.control_configuration_reference,
           NEW.historic_fx_snapshot, NEW.source_version, NEW.source_payload_hash, NEW.created_at)
       IS DISTINCT FROM
       ROW(OLD.tenant_id, OLD.legal_entity_id, OLD.id, OLD.party_id, OLD.direction, OLD.polarity,
           OLD.source_type, OLD.source_reference, OLD.source_schedule_reference, OLD.source_display_number,
           OLD.document_date, OLD.posting_date, OLD.due_date, OLD.transaction_currency,
           OLD.original_transaction_amount, OLD.base_currency, OLD.original_base_amount,
           OLD.control_account_role, OLD.control_account_reference, OLD.control_configuration_reference,
           OLD.historic_fx_snapshot, OLD.source_version, OLD.source_payload_hash, OLD.created_at) THEN
        RAISE EXCEPTION 'subledger.open_item_immutable' USING ERRCODE = '55000';
    END IF;
    IF NEW.row_version <> OLD.row_version + 1 THEN
        RAISE EXCEPTION 'subledger.open_item_version_invalid' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END
$$;
CREATE TRIGGER tr_subledger_open_item_immutable
    BEFORE UPDATE OR DELETE ON finance_subledger.party_open_item
    FOR EACH ROW EXECUTE FUNCTION finance_subledger.guard_open_item_update();
CREATE TRIGGER tr_subledger_application_immutable
    BEFORE UPDATE OR DELETE ON finance_subledger.open_item_application
    FOR EACH ROW EXECUTE FUNCTION finance_subledger.reject_immutable_change();
CREATE TRIGGER tr_subledger_reversal_immutable
    BEFORE UPDATE OR DELETE ON finance_subledger.application_reversal
    FOR EACH ROW EXECUTE FUNCTION finance_subledger.reject_immutable_change();
CREATE TRIGGER tr_subledger_batch_immutable
    BEFORE UPDATE OR DELETE ON finance_subledger.application_batch
    FOR EACH ROW EXECUTE FUNCTION finance_subledger.reject_immutable_change();

ALTER TABLE finance_subledger.party_financial_profile ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.party_open_item ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.application_batch ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.open_item_application ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.application_reversal ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON finance_subledger.party_financial_profile
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
CREATE POLICY tenant_isolation ON finance_subledger.party_open_item
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
CREATE POLICY tenant_isolation ON finance_subledger.application_batch
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
CREATE POLICY tenant_isolation ON finance_subledger.open_item_application
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
CREATE POLICY tenant_isolation ON finance_subledger.application_reversal
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE ON finance_subledger.party_financial_profile TO erp_app;
GRANT SELECT, INSERT, UPDATE ON finance_subledger.party_open_item TO erp_app;
GRANT SELECT, INSERT ON finance_subledger.application_batch TO erp_app;
GRANT SELECT, INSERT ON finance_subledger.open_item_application TO erp_app;
GRANT SELECT, INSERT ON finance_subledger.application_reversal TO erp_app;
GRANT USAGE ON SCHEMA finance_subledger TO erp_owner;
RESET row_security;
RESET search_path;
