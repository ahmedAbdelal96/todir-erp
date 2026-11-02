CREATE INDEX IF NOT EXISTS party_open_item_browse_idx
    ON finance_subledger.party_open_item (tenant_id, legal_entity_id, id)
    WHERE is_open;

CREATE INDEX IF NOT EXISTS party_open_item_search_idx
    ON finance_subledger.party_open_item (tenant_id, legal_entity_id, lower(source_display_number) text_pattern_ops, id);

CREATE INDEX IF NOT EXISTS party_open_item_reference_search_idx
    ON finance_subledger.party_open_item (tenant_id, legal_entity_id, lower(source_reference) text_pattern_ops, id);
