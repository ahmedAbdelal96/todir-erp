ALTER TABLE finance_payments.payment_fee
    ADD CONSTRAINT payment_fee_tenant_entity_id_unique UNIQUE (tenant_id, legal_entity_id, id);

CREATE TABLE IF NOT EXISTS finance_payments.payment_fee_disposition (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    original_payment_id uuid NOT NULL,
    reversal_payment_id uuid NOT NULL,
    payment_fee_id uuid NOT NULL,
    disposition text NOT NULL,
    evidence_reference text,
    reason_code text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    corrective_fact_id uuid,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, payment_fee_id),
    FOREIGN KEY (tenant_id, legal_entity_id, original_payment_id)
        REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, reversal_payment_id)
        REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, payment_fee_id)
        REFERENCES finance_payments.payment_fee(tenant_id, legal_entity_id, id),
    CHECK (disposition IN ('Retain','Reverse')),
    CHECK (length(btrim(reason_code)) BETWEEN 1 AND 120),
    CHECK (evidence_reference IS NULL OR length(btrim(evidence_reference)) <= 500),
    CHECK ((disposition = 'Retain' AND corrective_fact_id IS NULL)
        OR (disposition = 'Reverse' AND corrective_fact_id = id AND length(btrim(coalesce(evidence_reference,''))) > 0))
);

ALTER TABLE finance_payments.payment_fee_disposition ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_payments.payment_fee_disposition FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON finance_payments.payment_fee_disposition;
CREATE POLICY tenant_isolation ON finance_payments.payment_fee_disposition
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
GRANT SELECT, INSERT ON finance_payments.payment_fee_disposition TO erp_app;

CREATE OR REPLACE FUNCTION finance_payments.guard_fee_disposition_history()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'payment fee disposition history is immutable';
END;
$$;

CREATE TRIGGER tr_payment_fee_disposition_immutable
    BEFORE UPDATE OR DELETE ON finance_payments.payment_fee_disposition
    FOR EACH ROW EXECUTE FUNCTION finance_payments.guard_fee_disposition_history();

CREATE OR REPLACE FUNCTION finance_payments.guard_payment_fee_history()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'recognized payment fee history is immutable';
    END IF;
    IF OLD.recognition_state <> 'Pending'
       OR NEW.recognition_state <> 'Recognized'
       OR (to_jsonb(NEW) - 'recognition_state') IS DISTINCT FROM (to_jsonb(OLD) - 'recognition_state') THEN
        RAISE EXCEPTION 'recognized payment fee history is immutable';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS tr_payment_fee_history_immutable ON finance_payments.payment_fee;
CREATE TRIGGER tr_payment_fee_history_immutable
    BEFORE UPDATE OR DELETE ON finance_payments.payment_fee
    FOR EACH ROW EXECUTE FUNCTION finance_payments.guard_payment_fee_history();
