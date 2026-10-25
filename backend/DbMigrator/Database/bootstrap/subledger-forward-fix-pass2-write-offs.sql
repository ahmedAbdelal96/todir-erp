SET search_path = finance_subledger, public;
SET row_security = off;

CREATE TABLE IF NOT EXISTS finance_subledger.write_off_policy (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    direction text NOT NULL,
    currency_code char(3) NOT NULL,
    maximum_amount numeric(20,6) NOT NULL,
    approval_threshold numeric(20,6) NOT NULL,
    automatic_write_off_enabled boolean NOT NULL DEFAULT false,
    version bigint NOT NULL DEFAULT 1,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, direction, currency_code),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (direction IN ('Receivable','Payable')),
    CHECK (currency_code ~ '^[A-Z]{3}$'),
    CHECK (maximum_amount > 0 AND approval_threshold >= 0 AND approval_threshold <= maximum_amount),
    CHECK (automatic_write_off_enabled = false),
    CHECK (version > 0)
);

CREATE TABLE IF NOT EXISTS finance_subledger.write_off_request (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    policy_id uuid NOT NULL,
    policy_version bigint NOT NULL,
    approval_threshold numeric(20,6) NOT NULL,
    rounding_scale smallint NOT NULL,
    rounding_mode text NOT NULL,
    open_item_id uuid NOT NULL,
    party_id uuid NOT NULL,
    direction text NOT NULL,
    amount numeric(20,6) NOT NULL,
    currency_code char(3) NOT NULL,
    reason_code text NOT NULL,
    reason_text text NOT NULL,
    effective_date date NOT NULL,
    idempotency_key text NOT NULL,
    payload_hash char(64) NOT NULL,
    created_by uuid NOT NULL,
    status text NOT NULL DEFAULT 'PendingApproval',
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, idempotency_key),
    FOREIGN KEY (tenant_id, legal_entity_id, policy_id) REFERENCES finance_subledger.write_off_policy(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, open_item_id) REFERENCES finance_subledger.party_open_item(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (direction IN ('Receivable','Payable')),
    CHECK (amount > 0 AND currency_code ~ '^[A-Z]{3}$'),
    CHECK (policy_version > 0 AND approval_threshold >= 0),
    CHECK (rounding_scale BETWEEN 0 AND 6 AND rounding_mode IN ('HalfUp','HalfEven','Down','Up')),
    CHECK (length(btrim(reason_code)) BETWEEN 1 AND 80),
    CHECK (length(btrim(reason_text)) BETWEEN 1 AND 1000),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200),
    CHECK (status IN ('PendingApproval','Approved','Rejected'))
);
CREATE INDEX IF NOT EXISTS ix_subledger_write_off_pending
    ON finance_subledger.write_off_request (tenant_id, legal_entity_id, status, effective_date, id);

CREATE TABLE IF NOT EXISTS finance_subledger.write_off (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    request_id uuid NOT NULL,
    open_item_id uuid NOT NULL,
    party_id uuid NOT NULL,
    direction text NOT NULL,
    amount numeric(20,6) NOT NULL,
    base_currency char(3) NOT NULL,
    base_amount numeric(20,6) NOT NULL,
    effective_date date NOT NULL,
    created_by uuid NOT NULL,
    approved_by uuid NOT NULL,
    ledger_fact_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, request_id),
    FOREIGN KEY (tenant_id, legal_entity_id, request_id) REFERENCES finance_subledger.write_off_request(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, open_item_id) REFERENCES finance_subledger.party_open_item(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (direction IN ('Receivable','Payable')),
    CHECK (amount > 0 AND base_amount >= 0 AND base_currency ~ '^[A-Z]{3}$'),
    CHECK (created_by <> approved_by)
);
CREATE INDEX IF NOT EXISTS ix_subledger_write_off_item_date
    ON finance_subledger.write_off (tenant_id, legal_entity_id, open_item_id, effective_date, id);
CREATE TRIGGER tr_subledger_write_off_immutable
    BEFORE UPDATE OR DELETE ON finance_subledger.write_off
    FOR EACH ROW EXECUTE FUNCTION finance_subledger.reject_immutable_change();

CREATE TABLE IF NOT EXISTS finance_subledger.write_off_request_audit (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    request_id uuid NOT NULL,
    action text NOT NULL,
    actor_id uuid NOT NULL,
    evidence jsonb NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, request_id) REFERENCES finance_subledger.write_off_request(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (action IN ('Requested','Approved'))
);
CREATE TRIGGER tr_subledger_write_off_audit_immutable
    BEFORE UPDATE OR DELETE ON finance_subledger.write_off_request_audit
    FOR EACH ROW EXECUTE FUNCTION finance_subledger.reject_immutable_change();

ALTER TABLE finance_subledger.write_off_policy ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.write_off_policy FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.write_off_request ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.write_off_request FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.write_off ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.write_off FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.write_off_request_audit ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.write_off_request_audit FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON finance_subledger.write_off_policy;
DROP POLICY IF EXISTS tenant_isolation ON finance_subledger.write_off_request;
DROP POLICY IF EXISTS tenant_isolation ON finance_subledger.write_off;
DROP POLICY IF EXISTS tenant_isolation ON finance_subledger.write_off_request_audit;
CREATE POLICY tenant_isolation ON finance_subledger.write_off_policy
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
CREATE POLICY tenant_isolation ON finance_subledger.write_off_request
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
CREATE POLICY tenant_isolation ON finance_subledger.write_off
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
CREATE POLICY tenant_isolation ON finance_subledger.write_off_request_audit
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
GRANT SELECT, INSERT, UPDATE ON finance_subledger.write_off_policy TO erp_app;
GRANT SELECT, INSERT, UPDATE ON finance_subledger.write_off_request TO erp_app;
GRANT SELECT, INSERT ON finance_subledger.write_off TO erp_app;
GRANT SELECT, INSERT ON finance_subledger.write_off_request_audit TO erp_app;

RESET row_security;
RESET search_path;
