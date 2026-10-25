SET search_path = finance_subledger, public;
SET row_security = off;

CREATE INDEX IF NOT EXISTS ix_subledger_exposure_current
    ON finance_subledger.party_open_item
       (tenant_id, legal_entity_id, party_id, direction, posting_date)
    INCLUDE (transaction_currency, polarity, due_date, open_transaction_amount, row_version);

CREATE INDEX IF NOT EXISTS ix_subledger_write_off_party_date
    ON finance_subledger.write_off (tenant_id, legal_entity_id, party_id, direction, effective_date);

RESET row_security;
RESET search_path;
