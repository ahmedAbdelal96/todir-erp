CREATE TABLE IF NOT EXISTS finance_ledger.general_ledger_search_projection (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    journal_entry_id uuid NOT NULL,
    journal_entry_line_id uuid NOT NULL,
    posting_date date NOT NULL,
    entry_number text NOT NULL,
    sequence_no integer NOT NULL,
    journal_id uuid NOT NULL,
    account_id uuid NOT NULL,
    account_code text NOT NULL,
    account_name_en text NOT NULL,
    debit numeric(28,8) NOT NULL,
    credit numeric(28,8) NOT NULL,
    base_debit numeric(28,8) NOT NULL,
    base_credit numeric(28,8) NOT NULL,
    currency_code text NOT NULL,
    source_module text NOT NULL,
    source_reference text NOT NULL,
    PRIMARY KEY (tenant_id, legal_entity_id, journal_entry_line_id),
    FOREIGN KEY (tenant_id, legal_entity_id, journal_entry_line_id)
        REFERENCES finance_ledger.journal_entry_line (tenant_id, legal_entity_id, id)
);

CREATE INDEX IF NOT EXISTS general_ledger_search_browse_idx
    ON finance_ledger.general_ledger_search_projection
    (tenant_id, legal_entity_id, posting_date, entry_number, sequence_no, journal_entry_line_id)
    INCLUDE (journal_entry_id, journal_id, account_id, account_code, account_name_en,
             debit, credit, base_debit, base_credit, currency_code, source_module, source_reference);

CREATE INDEX IF NOT EXISTS general_ledger_search_account_idx
    ON finance_ledger.general_ledger_search_projection
    (tenant_id, legal_entity_id, account_id, posting_date, entry_number, sequence_no, journal_entry_line_id)
    INCLUDE (journal_entry_id, journal_id, account_code, account_name_en,
             debit, credit, base_debit, base_credit, currency_code, source_module, source_reference);

ALTER TABLE finance_ledger.general_ledger_search_projection ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.general_ledger_search_projection FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS general_ledger_search_projection_tenant_policy ON finance_ledger.general_ledger_search_projection;
CREATE POLICY general_ledger_search_projection_tenant_policy
    ON finance_ledger.general_ledger_search_projection
    USING (current_setting('app.tenant_id', true) IS NOT NULL
           AND tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (current_setting('app.tenant_id', true) IS NOT NULL
           AND tenant_id = current_setting('app.tenant_id', true)::uuid);

GRANT SELECT ON finance_ledger.general_ledger_search_projection TO erp_app;

ALTER TABLE finance_ledger.general_ledger_search_projection NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.journal_entry NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.journal_entry_line NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.account NO FORCE ROW LEVEL SECURITY;

INSERT INTO finance_ledger.general_ledger_search_projection
    (tenant_id, legal_entity_id, journal_entry_id, journal_entry_line_id, posting_date,
     entry_number, sequence_no, journal_id, account_id, account_code, account_name_en,
     debit, credit, base_debit, base_credit, currency_code, source_module, source_reference)
SELECT e.tenant_id, e.legal_entity_id, e.id, l.id, e.posting_date,
       e.entry_number, l.sequence_no, e.journal_id, a.id, a.code, a.name_en,
       l.debit, l.credit, l.base_debit, l.base_credit, l.currency_code,
       e.source_module, e.source_reference
FROM finance_ledger.journal_entry e
JOIN finance_ledger.journal_entry_line l
  ON l.tenant_id = e.tenant_id
 AND l.legal_entity_id = e.legal_entity_id
 AND l.journal_entry_id = e.id
JOIN finance_ledger.account a
  ON a.tenant_id = l.tenant_id
 AND a.legal_entity_id = l.legal_entity_id
 AND a.id = l.account_id
ON CONFLICT (tenant_id, legal_entity_id, journal_entry_line_id) DO UPDATE
SET posting_date = EXCLUDED.posting_date,
    entry_number = EXCLUDED.entry_number,
    sequence_no = EXCLUDED.sequence_no,
    journal_id = EXCLUDED.journal_id,
    account_id = EXCLUDED.account_id,
    account_code = EXCLUDED.account_code,
    account_name_en = EXCLUDED.account_name_en,
    debit = EXCLUDED.debit,
    credit = EXCLUDED.credit,
    base_debit = EXCLUDED.base_debit,
    base_credit = EXCLUDED.base_credit,
    currency_code = EXCLUDED.currency_code,
    source_module = EXCLUDED.source_module,
    source_reference = EXCLUDED.source_reference;

CREATE OR REPLACE FUNCTION finance_ledger.sync_general_ledger_search_projection()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = finance_ledger, pg_catalog
AS $$
BEGIN
    INSERT INTO finance_ledger.general_ledger_search_projection
        (tenant_id, legal_entity_id, journal_entry_id, journal_entry_line_id, posting_date,
         entry_number, sequence_no, journal_id, account_id, account_code, account_name_en,
         debit, credit, base_debit, base_credit, currency_code, source_module, source_reference)
    SELECT NEW.tenant_id, NEW.legal_entity_id, NEW.journal_entry_id, NEW.id,
           e.posting_date, e.entry_number, NEW.sequence_no, e.journal_id,
           NEW.account_id, a.code, a.name_en, NEW.debit, NEW.credit,
           NEW.base_debit, NEW.base_credit, NEW.currency_code,
           e.source_module, e.source_reference
    FROM finance_ledger.journal_entry e
    JOIN finance_ledger.account a
      ON a.tenant_id = NEW.tenant_id
     AND a.legal_entity_id = NEW.legal_entity_id
     AND a.id = NEW.account_id
    WHERE e.tenant_id = NEW.tenant_id
      AND e.legal_entity_id = NEW.legal_entity_id
      AND e.id = NEW.journal_entry_id
    ON CONFLICT (tenant_id, legal_entity_id, journal_entry_line_id) DO UPDATE
    SET posting_date = EXCLUDED.posting_date,
        entry_number = EXCLUDED.entry_number,
        sequence_no = EXCLUDED.sequence_no,
        journal_id = EXCLUDED.journal_id,
        account_id = EXCLUDED.account_id,
        account_code = EXCLUDED.account_code,
        account_name_en = EXCLUDED.account_name_en,
        debit = EXCLUDED.debit,
        credit = EXCLUDED.credit,
        base_debit = EXCLUDED.base_debit,
        base_credit = EXCLUDED.base_credit,
        currency_code = EXCLUDED.currency_code,
        source_module = EXCLUDED.source_module,
        source_reference = EXCLUDED.source_reference;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS general_ledger_search_projection_after_insert ON finance_ledger.journal_entry_line;
CREATE TRIGGER general_ledger_search_projection_after_insert
    AFTER INSERT ON finance_ledger.journal_entry_line
    FOR EACH ROW
    EXECUTE FUNCTION finance_ledger.sync_general_ledger_search_projection();

ALTER TABLE finance_ledger.journal_entry FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.journal_entry_line FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.account FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.general_ledger_search_projection FORCE ROW LEVEL SECURITY;

ANALYZE finance_ledger.general_ledger_search_projection;
