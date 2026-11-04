CREATE TABLE IF NOT EXISTS business_payroll.receivable_disbursement (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    receivable_id uuid NOT NULL,
    amount numeric(19,4) NOT NULL CHECK (amount > 0),
    currency_code char(3) NOT NULL,
    idempotency_key varchar(200) NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'Requested' CHECK (status IN ('Requested','Pending','Succeeded','Failed','Cancelled')),
    provider_reference varchar(200) NULL,
    failure_code varchar(100) NULL,
    requested_at timestamptz NOT NULL,
    completed_at timestamptz NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,receivable_id,idempotency_key)
);

CREATE INDEX IF NOT EXISTS ix_payroll_receivable_disbursement_status
    ON business_payroll.receivable_disbursement(tenant_id,legal_entity_id,status,requested_at DESC);

ALTER TABLE business_payroll.receivable_disbursement ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_payroll.receivable_disbursement FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_payroll.receivable_disbursement;
CREATE POLICY tenant_isolation ON business_payroll.receivable_disbursement
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
