SET search_path = finance_subledger, public;
SET row_security = off;

ALTER TABLE finance_subledger.party_open_item
    ADD COLUMN IF NOT EXISTS source_display_number_normalized text
        GENERATED ALWAYS AS (regexp_replace(upper(coalesce(source_display_number, '')), '[^[:alnum:]]', '', 'g')) STORED,
    ADD COLUMN IF NOT EXISTS source_reference_normalized text
        GENERATED ALWAYS AS (regexp_replace(upper(source_reference), '[^[:alnum:]]', '', 'g')) STORED;

DROP INDEX IF EXISTS finance_subledger.ix_subledger_match_open_display_context;
DROP INDEX IF EXISTS finance_subledger.ix_subledger_match_open_source_context;
DROP INDEX IF EXISTS finance_subledger.ix_subledger_match_open_display_upper;
DROP INDEX IF EXISTS finance_subledger.ix_subledger_match_open_source_upper;

CREATE INDEX IF NOT EXISTS ix_subledger_match_open_display_normalized_context
    ON finance_subledger.party_open_item
       (tenant_id, legal_entity_id, party_id, direction, polarity, transaction_currency,
        control_account_role, control_account_reference, control_configuration_reference, base_currency,
        source_display_number_normalized, due_date, document_date, id)
    WHERE is_open;

CREATE INDEX IF NOT EXISTS ix_subledger_match_open_source_normalized_context
    ON finance_subledger.party_open_item
       (tenant_id, legal_entity_id, party_id, direction, polarity, transaction_currency,
        control_account_role, control_account_reference, control_configuration_reference, base_currency,
        source_reference_normalized, due_date, document_date, id)
    WHERE is_open;

RESET row_security;
RESET search_path;
