SET search_path = finance_subledger, public;
SET row_security = off;

CREATE INDEX IF NOT EXISTS ix_subledger_match_open_party_currency_due
    ON finance_subledger.party_open_item (tenant_id, legal_entity_id, party_id, direction, polarity, transaction_currency, due_date, document_date, id)
    WHERE is_open;

CREATE INDEX IF NOT EXISTS ix_subledger_open_party_page
    ON finance_subledger.party_open_item (tenant_id, legal_entity_id, party_id, direction, id)
    WHERE is_open;

CREATE INDEX IF NOT EXISTS ix_subledger_open_party_display_prefix
    ON finance_subledger.party_open_item (tenant_id, legal_entity_id, party_id, lower(source_display_number) text_pattern_ops)
    WHERE source_display_number IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_subledger_open_party_reference_prefix
    ON finance_subledger.party_open_item (tenant_id, legal_entity_id, party_id, lower(source_reference) text_pattern_ops);
