SET search_path = finance_subledger, public;
SET row_security = off;

CREATE INDEX IF NOT EXISTS ix_subledger_match_open_display_normalized
    ON finance_subledger.party_open_item
       (tenant_id, legal_entity_id, party_id, direction, polarity, transaction_currency,
        (regexp_replace(upper(coalesce(source_display_number, '')), '[^[:alnum:]]', '', 'g')),
        due_date, document_date, id)
    WHERE is_open;

CREATE INDEX IF NOT EXISTS ix_subledger_match_open_source_normalized
    ON finance_subledger.party_open_item
       (tenant_id, legal_entity_id, party_id, direction, polarity, transaction_currency,
        (regexp_replace(upper(source_reference), '[^[:alnum:]]', '', 'g')),
        due_date, document_date, id)
    WHERE is_open;

RESET row_security;
RESET search_path;
