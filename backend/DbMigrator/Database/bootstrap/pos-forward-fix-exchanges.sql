CREATE TABLE IF NOT EXISTS business_pos.pos_exchange_group (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    return_id uuid NOT NULL,
    new_checkout_id uuid NULL,
    state text NOT NULL,
    difference_amount numeric(19,4) NOT NULL,
    currency_code text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, return_id),
    FOREIGN KEY (tenant_id, return_id) REFERENCES business_pos.pos_return(tenant_id, id),
    FOREIGN KEY (tenant_id, new_checkout_id) REFERENCES business_pos.pos_checkout(tenant_id, id),
    CHECK (state IN ('Completed','NeedsCompletion'))
);

ALTER TABLE business_pos.pos_exchange_group ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_pos.pos_exchange_group FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_pos.pos_exchange_group;
CREATE POLICY tenant_isolation ON business_pos.pos_exchange_group
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

CREATE INDEX IF NOT EXISTS ix_pos_exchange_state ON business_pos.pos_exchange_group(tenant_id, state, created_at DESC);
