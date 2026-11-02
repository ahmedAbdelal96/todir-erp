CREATE INDEX IF NOT EXISTS journal_entry_browse_idx
    ON finance_ledger.journal_entry (tenant_id, legal_entity_id, posting_date DESC, entry_number DESC, id DESC);

CREATE INDEX IF NOT EXISTS journal_entry_line_entry_order_idx
    ON finance_ledger.journal_entry_line (tenant_id, legal_entity_id, journal_entry_id, sequence_no, id);

CREATE INDEX IF NOT EXISTS account_code_browse_idx
    ON finance_ledger.account (tenant_id, legal_entity_id, code, id);
