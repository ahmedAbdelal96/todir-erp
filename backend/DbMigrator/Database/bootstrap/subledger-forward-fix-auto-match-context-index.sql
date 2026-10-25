SET search_path = finance_subledger, public;
SET row_security = off;

DROP INDEX IF EXISTS finance_subledger.ix_subledger_match_open_display_normalized;
DROP INDEX IF EXISTS finance_subledger.ix_subledger_match_open_source_normalized;

CREATE INDEX IF NOT EXISTS ix_subledger_match_open_context_due
    ON finance_subledger.party_open_item
       (tenant_id, legal_entity_id, party_id, direction, polarity, transaction_currency,
        control_account_role, control_account_reference, control_configuration_reference, base_currency,
        due_date, document_date, id)
    WHERE is_open;

RESET row_security;
RESET search_path;
