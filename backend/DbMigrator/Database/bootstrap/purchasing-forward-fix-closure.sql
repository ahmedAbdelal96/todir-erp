-- Purchasing closure: durable receive-and-bill convergence, matching replay,
-- and supplier-credit linkage. This is additive and preserves prior baselines.

ALTER TABLE business_purchasing.purchase_order_match_result
    ADD COLUMN IF NOT EXISTS supplier_invoice_id uuid,
    ADD COLUMN IF NOT EXISTS idempotency_key text,
    ADD COLUMN IF NOT EXISTS payload_hash text;

CREATE UNIQUE INDEX IF NOT EXISTS ux_purchase_order_match_replay
    ON business_purchasing.purchase_order_match_result(tenant_id, legal_entity_id, purchase_order_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;

ALTER TABLE business_purchasing.supplier_return_request
    ADD COLUMN IF NOT EXISTS original_invoice_id uuid;

CREATE TABLE IF NOT EXISTS business_purchasing.purchase_order_workflow (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    workflow_type text NOT NULL,
    idempotency_key text NOT NULL,
    payload_hash text NOT NULL,
    state text NOT NULL,
    receipt_payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    invoice_payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    invoice_id uuid,
    last_error text,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, purchase_order_id, workflow_type, idempotency_key),
    FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_id)
        REFERENCES business_purchasing.purchase_order(tenant_id, legal_entity_id, id),
    CHECK (workflow_type IN ('ReceiveAndBill')),
    CHECK (state IN ('Started','ReceiptCompleted','InvoicePending','Completed','Failed')),
    CHECK (jsonb_typeof(receipt_payload) = 'object'),
    CHECK (jsonb_typeof(invoice_payload) = 'object')
);

ALTER TABLE business_purchasing.purchase_order_workflow ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_purchasing.purchase_order_workflow FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_purchasing.purchase_order_workflow;
CREATE POLICY tenant_isolation ON business_purchasing.purchase_order_workflow
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

CREATE INDEX IF NOT EXISTS ix_purchase_order_workflow_state
    ON business_purchasing.purchase_order_workflow(tenant_id, legal_entity_id, state, updated_at DESC);

GRANT SELECT, INSERT, UPDATE ON business_purchasing.purchase_order_match_result TO erp_app;
GRANT SELECT, INSERT, UPDATE ON business_purchasing.supplier_return_request TO erp_app;
GRANT SELECT, INSERT, UPDATE ON business_purchasing.purchase_order_workflow TO erp_app;
