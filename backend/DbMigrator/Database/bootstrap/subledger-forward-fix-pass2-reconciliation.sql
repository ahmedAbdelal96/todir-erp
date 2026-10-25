SET search_path = finance_subledger, public;
SET row_security = off;

CREATE TABLE IF NOT EXISTS finance_subledger.control_reconciliation_run (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    as_of_date date NOT NULL,
    base_currency char(3) NOT NULL,
    subledger_base_balance numeric(20,6) NOT NULL,
    ledger_base_balance numeric(20,6) NOT NULL,
    variance numeric(20,6) NOT NULL,
    is_balanced boolean NOT NULL,
    created_by uuid NOT NULL,
    correlation_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,legal_entity_id,id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (base_currency ~ '^[A-Z]{3}$'),
    CHECK (NOT is_balanced OR variance = 0)
);
CREATE INDEX IF NOT EXISTS ix_subledger_reconciliation_history
    ON finance_subledger.control_reconciliation_run (tenant_id,legal_entity_id,as_of_date DESC,created_at DESC);
CREATE TABLE IF NOT EXISTS finance_subledger.control_reconciliation_detail (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    run_id uuid NOT NULL,
    direction text NOT NULL,
    control_role text NOT NULL,
    control_account_reference text NOT NULL,
    control_configuration_reference text NOT NULL,
    ledger_account_id uuid,
    subledger_base_balance numeric(20,6) NOT NULL,
    ledger_base_balance numeric(20,6) NOT NULL,
    variance numeric(20,6) NOT NULL,
    context_resolved boolean NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id,run_id) REFERENCES finance_subledger.control_reconciliation_run(tenant_id,legal_entity_id,id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (direction IN ('Receivable','Payable')),
    CHECK (control_role IN ('Receivable','Payable')),
    CHECK (length(btrim(control_account_reference)) BETWEEN 1 AND 240),
    CHECK (length(btrim(control_configuration_reference)) BETWEEN 1 AND 240)
);
CREATE INDEX IF NOT EXISTS ix_subledger_reconciliation_detail_run
    ON finance_subledger.control_reconciliation_detail (tenant_id,legal_entity_id,run_id,direction,control_role);
CREATE TRIGGER tr_subledger_reconciliation_run_immutable
    BEFORE UPDATE OR DELETE ON finance_subledger.control_reconciliation_run
    FOR EACH ROW EXECUTE FUNCTION finance_subledger.reject_immutable_change();
CREATE TRIGGER tr_subledger_reconciliation_detail_immutable
    BEFORE UPDATE OR DELETE ON finance_subledger.control_reconciliation_detail
    FOR EACH ROW EXECUTE FUNCTION finance_subledger.reject_immutable_change();

ALTER TABLE finance_subledger.control_reconciliation_run ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.control_reconciliation_run FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.control_reconciliation_detail ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.control_reconciliation_detail FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON finance_subledger.control_reconciliation_run
    USING (tenant_id=current_setting('app.tenant_id',true)::uuid)
    WITH CHECK (tenant_id=current_setting('app.tenant_id',true)::uuid);
CREATE POLICY tenant_isolation ON finance_subledger.control_reconciliation_detail
    USING (tenant_id=current_setting('app.tenant_id',true)::uuid)
    WITH CHECK (tenant_id=current_setting('app.tenant_id',true)::uuid);
GRANT SELECT,INSERT ON finance_subledger.control_reconciliation_run TO erp_app;
GRANT SELECT,INSERT ON finance_subledger.control_reconciliation_detail TO erp_app;

RESET row_security;
RESET search_path;