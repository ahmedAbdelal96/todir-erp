ALTER TABLE finance_subledger.party_open_item
    ADD COLUMN IF NOT EXISTS payment_term_snapshot jsonb;

ALTER TABLE finance_subledger.party_open_item
    DROP CONSTRAINT IF EXISTS party_open_item_source_type_check;
ALTER TABLE finance_subledger.party_open_item
    ADD CONSTRAINT party_open_item_source_type_check
    CHECK (source_type IN ('Invoice','Bill','Payment','Advance','CreditNote','OpeningBalance','Adjustment'));

CREATE OR REPLACE FUNCTION finance_subledger.guard_open_item_update() RETURNS trigger
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
           NEW.historic_fx_snapshot, NEW.payment_term_snapshot, NEW.source_version, NEW.source_payload_hash, NEW.created_at)
       IS DISTINCT FROM
       ROW(OLD.tenant_id, OLD.legal_entity_id, OLD.id, OLD.party_id, OLD.direction, OLD.polarity,
           OLD.source_type, OLD.source_reference, OLD.source_schedule_reference, OLD.source_display_number,
           OLD.document_date, OLD.posting_date, OLD.due_date, OLD.transaction_currency,
           OLD.original_transaction_amount, OLD.base_currency, OLD.original_base_amount,
           OLD.control_account_role, OLD.control_account_reference, OLD.control_configuration_reference,
           OLD.historic_fx_snapshot, OLD.payment_term_snapshot, OLD.source_version, OLD.source_payload_hash, OLD.created_at) THEN
        RAISE EXCEPTION 'subledger.open_item_immutable' USING ERRCODE = '55000';
    END IF;
    IF NEW.row_version <> OLD.row_version + 1 THEN
        RAISE EXCEPTION 'subledger.open_item_version_invalid' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END
$$;

CREATE INDEX IF NOT EXISTS ix_subledger_posted_party_currency
    ON finance_subledger.party_open_item (tenant_id, legal_entity_id, party_id, direction, transaction_currency, posting_date, due_date, id);
