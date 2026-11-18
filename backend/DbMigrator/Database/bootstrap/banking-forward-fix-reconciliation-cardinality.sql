SET row_security = off;

CREATE TABLE IF NOT EXISTS finance_banking.reconciliation_match_system_item (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, match_id uuid NOT NULL,
    system_reference text NOT NULL, system_amount numeric(30,12) NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,match_id,system_reference),
    FOREIGN KEY (tenant_id,legal_entity_id,match_id) REFERENCES finance_banking.reconciliation_match(tenant_id,legal_entity_id,id),
    CHECK (system_amount > 0)
);
CREATE INDEX IF NOT EXISTS ix_banking_match_system_item_scope ON finance_banking.reconciliation_match_system_item(tenant_id,legal_entity_id,match_id,id);
ALTER TABLE finance_banking.reconciliation_match_system_item ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_banking.reconciliation_match_system_item FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON finance_banking.reconciliation_match_system_item;
CREATE POLICY tenant_isolation ON finance_banking.reconciliation_match_system_item USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE ON finance_banking.reconciliation_match_system_item TO erp_app;

RESET row_security;
RESET search_path;
