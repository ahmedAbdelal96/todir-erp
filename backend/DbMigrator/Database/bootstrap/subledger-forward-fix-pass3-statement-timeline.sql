SET search_path = finance_subledger, public;
SET row_security = off;

CREATE INDEX IF NOT EXISTS ix_subledger_application_batch_party_date
    ON finance_subledger.application_batch (tenant_id, legal_entity_id, party_id, direction, application_date, id);

CREATE INDEX IF NOT EXISTS ix_subledger_reversal_batch
    ON finance_subledger.application_reversal (tenant_id, legal_entity_id, reversal_batch_id);
