CREATE OR REPLACE FUNCTION finance_payments.guard_payment_history() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'payments.payment_history_immutable' USING ERRCODE = '55000';
    END IF;
    IF OLD.business_state IN ('Completed','Reversed')
       AND ROW(NEW.tenant_id, NEW.legal_entity_id, NEW.id, NEW.payment_number, NEW.branch_id, NEW.direction, NEW.payment_type, NEW.party_id,
               NEW.amount, NEW.currency_code, NEW.method, NEW.payment_account_id, NEW.payment_date,
               NEW.posting_date, NEW.value_date, NEW.provider_event_time, NEW.bank_reference, NEW.normalized_reference,
               NEW.customer_reference, NEW.supplier_reference, NEW.remittance_reference, NEW.provider_code, NEW.provider_transaction_id,
               NEW.source_type, NEW.source_reference, NEW.completed_at, NEW.completed_by, NEW.subledger_item_id, NEW.accounting_fact_id, NEW.created_at)
           IS DISTINCT FROM
           ROW(OLD.tenant_id, OLD.legal_entity_id, OLD.id, OLD.payment_number, OLD.branch_id, OLD.direction, OLD.payment_type, OLD.party_id,
               OLD.amount, OLD.currency_code, OLD.method, OLD.payment_account_id, OLD.payment_date,
               OLD.posting_date, OLD.value_date, OLD.provider_event_time, OLD.bank_reference, OLD.normalized_reference,
               OLD.customer_reference, OLD.supplier_reference, OLD.remittance_reference, OLD.provider_code, OLD.provider_transaction_id,
               OLD.source_type, OLD.source_reference, OLD.completed_at, OLD.completed_by, OLD.subledger_item_id, OLD.accounting_fact_id, OLD.created_at) THEN
        RAISE EXCEPTION 'payments.completed_payment_immutable' USING ERRCODE = '55000';
    END IF;
    IF OLD.business_state IN ('Completed','Reversed') AND NEW.business_state NOT IN ('Completed','Reversed') THEN
        RAISE EXCEPTION 'payments.completed_payment_immutable' USING ERRCODE = '55000';
    END IF;
    IF OLD.business_state = 'Reversed' AND NEW.business_state <> 'Reversed' THEN
        RAISE EXCEPTION 'payments.completed_payment_immutable' USING ERRCODE = '55000';
    END IF;
    IF OLD.business_state = 'Completed' AND NEW.business_state = 'Reversed'
       AND NOT EXISTS (SELECT 1 FROM finance_payments.payment_reversal reversal
                       WHERE reversal.tenant_id = OLD.tenant_id AND reversal.legal_entity_id = OLD.legal_entity_id
                         AND reversal.original_payment_id = OLD.id AND reversal.status = 'Completed') THEN
        RAISE EXCEPTION 'payments.reversal_record_required' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END
$$;
