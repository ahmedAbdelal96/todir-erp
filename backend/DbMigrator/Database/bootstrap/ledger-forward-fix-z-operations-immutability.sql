DROP TRIGGER IF EXISTS journal_line_dimension_immutable ON finance_ledger.journal_line_dimension;
CREATE TRIGGER journal_line_dimension_immutable BEFORE UPDATE OR DELETE ON finance_ledger.journal_line_dimension FOR EACH ROW EXECUTE FUNCTION finance_ledger.prevent_posted_mutation();
