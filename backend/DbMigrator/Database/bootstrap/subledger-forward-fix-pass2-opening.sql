SET search_path = finance_subledger, public;
SET row_security = off;

CREATE TABLE IF NOT EXISTS finance_subledger.opening_batch (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    batch_identity text NOT NULL,
    go_live_date date NOT NULL,
    base_currency char(3) NOT NULL,
    control_configuration_reference text NOT NULL,
    idempotency_key text NOT NULL,
    payload_hash char(64) NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    created_by uuid NOT NULL,
    correlation_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    finalized_by uuid,
    finalization_correlation_id uuid,
    finalized_at timestamptz,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,legal_entity_id,id),
    UNIQUE (tenant_id,legal_entity_id,batch_identity),
    UNIQUE (tenant_id,legal_entity_id,idempotency_key),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (length(btrim(batch_identity)) BETWEEN 1 AND 120),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200),
    CHECK (base_currency ~ '^[A-Z]{3}$'),
    CHECK (length(btrim(control_configuration_reference)) BETWEEN 1 AND 240),
    CHECK (status IN ('Draft','Finalized')),
    CHECK ((status='Draft' AND finalized_at IS NULL AND finalized_by IS NULL AND finalization_correlation_id IS NULL) OR (status='Finalized' AND finalized_at IS NOT NULL AND finalized_by IS NOT NULL AND finalization_correlation_id IS NOT NULL))
);
CREATE TABLE IF NOT EXISTS finance_subledger.opening_batch_item (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    batch_id uuid NOT NULL,
    row_key text NOT NULL,
    payload_hash char(64) NOT NULL,
    created_open_item_id uuid,
    party_id uuid NOT NULL,
    direction text NOT NULL,
    polarity text NOT NULL,
    source_reference text NOT NULL,
    document_date date NOT NULL,
    due_date date NOT NULL,
    transaction_currency char(3) NOT NULL,
    transaction_amount numeric(20,6) NOT NULL,
    base_currency char(3) NOT NULL,
    base_amount numeric(20,6) NOT NULL,
    control_account_role text NOT NULL,
    control_account_reference text NOT NULL,
    control_configuration_reference text NOT NULL,
    historic_fx_snapshot jsonb,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,legal_entity_id,id),
    UNIQUE (tenant_id,legal_entity_id,batch_id,row_key),
    FOREIGN KEY (tenant_id,legal_entity_id,batch_id) REFERENCES finance_subledger.opening_batch(tenant_id,legal_entity_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id,created_open_item_id) REFERENCES finance_subledger.party_open_item(tenant_id,legal_entity_id,id),
    FOREIGN KEY (tenant_id,party_id) REFERENCES foundation_directory.party(tenant_id,id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (direction IN ('Receivable','Payable')),
    CHECK (polarity IN ('Debit','Credit')),
    CHECK (transaction_amount > 0 AND base_amount >= 0),
    CHECK (transaction_currency ~ '^[A-Z]{3}$' AND base_currency ~ '^[A-Z]{3}$'),
    CHECK (control_account_role IN ('Receivable','Payable')),
    CHECK (length(btrim(row_key)) BETWEEN 1 AND 120),
    CHECK (length(btrim(source_reference)) BETWEEN 1 AND 240),
    CHECK (length(btrim(control_account_reference)) BETWEEN 1 AND 240),
    CHECK (length(btrim(control_configuration_reference)) BETWEEN 1 AND 240)
);
CREATE INDEX IF NOT EXISTS ix_subledger_opening_batch_items
    ON finance_subledger.opening_batch_item (tenant_id,legal_entity_id,batch_id,row_key);

CREATE OR REPLACE FUNCTION finance_subledger.prevent_finalized_opening_batch_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE batch_status text;
BEGIN
    IF TG_TABLE_NAME='opening_batch' THEN
        IF OLD.status='Finalized' OR TG_OP='DELETE' THEN
            RAISE EXCEPTION 'A finalized Subledger opening batch is immutable';
        END IF;
        RETURN NEW;
    END IF;
    SELECT status INTO batch_status FROM finance_subledger.opening_batch
      WHERE tenant_id=OLD.tenant_id AND legal_entity_id=OLD.legal_entity_id AND id=OLD.batch_id;
    IF batch_status='Finalized' THEN
        RAISE EXCEPTION 'Items in a finalized Subledger opening batch are immutable';
    END IF;
    IF TG_OP='DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER tr_subledger_opening_batch_mutation
    BEFORE UPDATE OR DELETE ON finance_subledger.opening_batch
    FOR EACH ROW EXECUTE FUNCTION finance_subledger.prevent_finalized_opening_batch_mutation();
CREATE TRIGGER tr_subledger_opening_item_mutation
    BEFORE UPDATE OR DELETE ON finance_subledger.opening_batch_item
    FOR EACH ROW EXECUTE FUNCTION finance_subledger.prevent_finalized_opening_batch_mutation();

ALTER TABLE finance_subledger.opening_batch ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.opening_batch FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.opening_batch_item ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_subledger.opening_batch_item FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON finance_subledger.opening_batch
    USING (tenant_id=current_setting('app.tenant_id',true)::uuid)
    WITH CHECK (tenant_id=current_setting('app.tenant_id',true)::uuid);
CREATE POLICY tenant_isolation ON finance_subledger.opening_batch_item
    USING (tenant_id=current_setting('app.tenant_id',true)::uuid)
    WITH CHECK (tenant_id=current_setting('app.tenant_id',true)::uuid);
GRANT SELECT,INSERT,UPDATE ON finance_subledger.opening_batch TO erp_app;
GRANT SELECT,INSERT ON finance_subledger.opening_batch_item TO erp_app;

RESET row_security;
RESET search_path;
